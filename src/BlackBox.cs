#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Чорна скринька»: the last seconds of your ravens' flight are kept all the time, every frame: where each is
    // from its seat (in your frame), how it is turned, what it is doing (the director's act, the motor's plan, final
    // approach, landing %), its speed, wing pace and braking, and every bone of it. Something looked wrong: the key
    // (Num0 by default, admins only, it only listens) saves those seconds — a readable file for the analysis
    // (BepInEx/config/RavenWatch/rec/crooked_*.txt, sudden jumps marked) and a capture the «Політ» tab plays back on
    // its picture, frame by frame.
    [DefaultExecutionOrder(32000)]
    internal sealed class BlackBox : MonoBehaviour
    {
        internal static float Seconds = 15f;
        internal static KeyCode Key = KeyCode.Keypad0;
        private const int Cap = 4096; // frames kept per raven (15 s even at 270 fps)

        internal sealed class Frame
        {
            public float T, Dt, Speed, Flap, Flare;
            public Vector3 Pos;          // from the seat, in the owner's frame (x right, y up, z forward)
            public Quaternion Rot;       // the model's turn in the owner's frame
            // what it was doing, as numbers (the words are made only when shown or saved: no garbage each frame)
            public RavenAct Act;
            public string Plan = "";     // the motor's plan name (a constant: no new string)
            public bool Final, Arc;
            public sbyte LandPct = -1, BlendPct = -1;
            public string Sit = "";     // sitting: the sitting template it plays (its name: no new string), and how far into it
            public sbyte SitPct = -1;
            public string Phase
            {
                get
                {
                    StringBuilder s = new StringBuilder(Act.ToString());
                    if (Plan.Length > 0) s.Append(" «").Append(Plan).Append('»').Append(Final ? " final" : "");
                    if (LandPct >= 0) s.Append(" land ").Append(LandPct).Append('%');
                    else if (Arc) s.Append(" arc");
                    if (BlendPct >= 0) s.Append(" blend ").Append(BlendPct).Append('%');
                    if (Sit.Length > 0) s.Append(" sit «").Append(Sit).Append("» ").Append(SitPct).Append('%');
                    return s.ToString();
                }
            }
            public Quaternion[] BoneRot; // each skinned bone's local turn
            public Vector3[] BonePos;    // and place
        }

        internal sealed class Track
        {
            public bool Munin;
            public string[] Bones;       // the skinned bones' names (the replay finds them by name on its own model)
            public readonly List<Frame> Frames = new List<Frame>();
            public float Length { get { return Frames.Count > 1 ? Frames[Frames.Count - 1].T - Frames[0].T : 0f; } }
            // the frame at time t from the start (the last one at or before it)
            public int IndexAt(float t)
            {
                if (Frames.Count == 0) return -1;
                float t0 = Frames[0].T;
                int lo = 0, hi = Frames.Count - 1;
                while (lo < hi) { int mid = (lo + hi + 1) / 2; if (Frames[mid].T - t0 <= t) lo = mid; else hi = mid - 1; }
                return lo;
            }
        }

        internal sealed class Capture
        {
            public string When = "", File = "";
            public readonly Track[] Tracks = new Track[2];
        }

        internal static Capture Last;
        private static BlackBox _inst;

        // the recorder lives once; started with the shoulder ravens (Shoulder.Tick)
        internal static void Ensure()
        {
            if (_inst != null) return;
            GameObject go = new GameObject("RavenWatch_BlackBox");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<BlackBox>();
        }

        // one ring per raven: the frames, reused (no garbage per frame)
        private sealed class Ring
        {
            public GameObject Model;
            public Transform[] Bones;
            public string[] Names;
            public readonly Frame[] F = new Frame[Cap];
            public int Head, Count;
        }
        private readonly Ring[] _rings = { new Ring(), new Ring() };
        private float _adminAt;
        private bool _admin;

        private void LateUpdate()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Key == KeyCode.None) return; // off (the default): nothing is recorded
            // only an admin can save it: nobody else records (every bone of both ravens, each frame)
            if (Time.unscaledTime >= _adminAt) { _adminAt = Time.unscaledTime + 2f; _admin = Plugin.IsLocalAdmin(); }
            if (!_admin) return;
            for (int i = 0; i < 2; i++) Record(p, i == 1, _rings[i]);
            if (Input.GetKeyDown(Key) && Free())
            {
                string said = Save(p);
                p.Message(MessageHud.MessageType.TopLeft, said);
            }
        }

        private static bool Free()
        {
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible()) return false;
            return true;
        }

        private void Record(Player p, bool munin, Ring r)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            if (m == null) m = StrikeFlight.FlyerOf(p, munin);
            if (m == null) return;
            if (m != r.Model)
            {
                // another model (made again, a stand-in): its bones, and the ring starts afresh
                r.Model = m; r.Head = 0; r.Count = 0;
                SkinnedMeshRenderer smr = m.GetComponentInChildren<SkinnedMeshRenderer>(true);
                r.Bones = smr != null ? smr.bones : new Transform[0];
                r.Names = new string[r.Bones.Length];
                for (int b = 0; b < r.Bones.Length; b++) r.Names[b] = r.Bones[b] != null ? r.Bones[b].name : "";
            }
            Frame f = r.F[r.Head];
            if (f == null || f.BoneRot == null || f.BoneRot.Length != r.Bones.Length) { f = new Frame { BoneRot = new Quaternion[r.Bones.Length], BonePos = new Vector3[r.Bones.Length] }; r.F[r.Head] = f; }
            Transform t = m.transform;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            ShoulderLife life = m.GetComponent<ShoulderLife>();
            Vector3 fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up);
            Quaternion facing = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
            Vector3 seat = pin != null && pin.SeatNow != Vector3.zero ? pin.SeatNow : p.GetHeadPoint();
            Vector3 pos = Quaternion.Inverse(facing) * (t.position - seat);
            int prev = (r.Head - 1 + Cap) % Cap;
            Frame pf = r.Count > 0 ? r.F[prev] : null;
            f.T = Time.time; f.Dt = Time.deltaTime;
            f.Speed = pf != null && f.Dt > 0f ? (pos - pf.Pos).magnitude / f.Dt : 0f;
            f.Pos = pos;
            f.Rot = Quaternion.Inverse(facing) * t.rotation;
            f.Flap = life != null ? life.FlapRate : 0f;
            f.Flare = life != null ? life.Flare : 0f;
            Phase(f, m, pin, life);
            for (int b = 0; b < r.Bones.Length; b++)
                if (r.Bones[b] != null) { f.BoneRot[b] = r.Bones[b].localRotation; f.BonePos[b] = r.Bones[b].localPosition; }
            r.Head = (r.Head + 1) % Cap;
            if (r.Count < Cap) r.Count++;
        }

        // what it is doing, in a few words: the act, the motor's plan, the approach, the landing
        private static void Phase(Frame f, GameObject m, ShoulderPin pin, ShoulderLife life)
        {
            f.Act = AnimDirector.Of(m);
            RavenMotor mot = life != null ? life.Motor : null;
            bool air = mot != null && mot.Airborne;
            f.Plan = air ? mot.Plan : "";
            f.Final = air && mot.Body.Final;
            f.LandPct = (sbyte)(life != null && life.Landing ? Mathf.RoundToInt(life.LandU * 100f) : -1);
            f.Arc = f.LandPct < 0 && pin != null && pin.Arriving && !air;
            f.BlendPct = (sbyte)(life != null && life.BlendW >= 0f && life.BlendW < 1f ? Mathf.RoundToInt(life.BlendW * 100f) : -1);
            SitDirector d = life != null && life.SitNew ? life.SitDirectorNow : null;
            bool sits = d != null && d.Playing;
            f.Sit = sits ? d.Run.T.Name : "";
            f.SitPct = (sbyte)(sits ? Mathf.RoundToInt(Mathf.Clamp01(d.ActT / Mathf.Max(0.01f, d.Run.Seconds)) * 100f) : -1);
        }

        // the key (or the panel's button): the last seconds of both ravens, into a file and into the replay
        internal static string Save(Player p)
        {
            if (_inst == null) return "Чорна скринька ще не почала писати.";
            Capture cap = new Capture { When = System.DateTime.Now.ToString("HH:mm:ss") };
            for (int i = 0; i < 2; i++)
            {
                Ring r = _inst._rings[i];
                if (r.Count == 0 || r.Model == null) continue;
                Track tr = new Track { Munin = i == 1, Bones = r.Names };
                float from = Time.time - Seconds;
                for (int k = 0; k < r.Count; k++)
                {
                    Frame f = r.F[(r.Head - r.Count + k + Cap) % Cap];
                    if (f == null || f.T < from) continue;
                    tr.Frames.Add(new Frame { T = f.T, Dt = f.Dt, Speed = f.Speed, Flap = f.Flap, Flare = f.Flare, Pos = f.Pos, Rot = f.Rot, Act = f.Act, Plan = f.Plan, Final = f.Final, Arc = f.Arc, LandPct = f.LandPct, BlendPct = f.BlendPct, Sit = f.Sit, SitPct = f.SitPct,
                        BoneRot = (Quaternion[])f.BoneRot.Clone(), BonePos = (Vector3[])f.BonePos.Clone() });
                }
                if (tr.Frames.Count > 0) cap.Tracks[i] = tr;
            }
            if (cap.Tracks[0] == null && cap.Tracks[1] == null) return "Чорна скринька: нема чого записати (воронів не видно).";
            try { cap.File = Write(cap); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Black box failed: " + e.Message); }
            Last = cap;
            return "Чорна скринька: останні " + Seconds.ToString("0") + " с записано" + (cap.File.Length > 0 ? " (" + Path.GetFileName(cap.File) + ")" : "") + ". Подивитися — тестове вікно, «Політ».";
        }

        // a note to the last file (what looked wrong), from the panel
        internal static bool Note(string text)
        {
            if (Last == null || Last.File.Length == 0 || string.IsNullOrEmpty(text)) return false;
            try { File.AppendAllText(Last.File, "# NOTE: " + text.Replace('\n', ' ') + "\n", new UTF8Encoding(false)); return true; }
            catch { return false; }
        }

        private static readonly string[] Watch = { "Armature", "Hip", "Spine", "Spine1", "Neck", "Wing1.l", "Wing1.r", "wing2.l", "wing2.r", "Leg1.l", "Leg1.r", "Tail" };

        private static string Write(Capture cap)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# RavenWatch black box " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": the last " + Seconds.ToString("0") + " s");
            sb.AppendLine("# t s | dt | phase | pos from seat cm (right,up,fwd) | moved cm | yaw,pitch,roll vs owner | turned ° | speed m/s | flap ×, flare | bones turned ° this frame | JUMP");
            foreach (Track tr in cap.Tracks)
            {
                if (tr == null) continue;
                sb.AppendLine("## " + (tr.Munin ? "Munin" : "Hugin") + ": " + tr.Frames.Count + " frames, " + tr.Length.ToString("0.00", c) + " s");
                int[] wi = new int[Watch.Length];
                for (int w = 0; w < Watch.Length; w++) wi[w] = System.Array.IndexOf(tr.Bones, Watch[w]);
                int jumps = 0;
                float t0 = tr.Frames[0].T;
                for (int k = 0; k < tr.Frames.Count; k++)
                {
                    Frame f = tr.Frames[k], pf = k > 0 ? tr.Frames[k - 1] : null;
                    Quaternion body = f.Rot * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
                    Vector3 e = body.eulerAngles;
                    float moved = pf != null ? (f.Pos - pf.Pos).magnitude : 0f;
                    float turned = pf != null ? Quaternion.Angle(pf.Rot, f.Rot) : 0f;
                    bool jump = pf != null && (moved > 0.15f || turned > 15f);
                    StringBuilder bones = new StringBuilder();
                    for (int w = 0; w < Watch.Length; w++)
                    {
                        int b = wi[w];
                        if (b < 0 || pf == null) continue;
                        float d = Quaternion.Angle(pf.BoneRot[b], f.BoneRot[b]);
                        if (d >= 3f) bones.Append(' ').Append(Watch[w]).Append('=').Append(d.ToString("0.#", c));
                        if (d > 25f) jump = true;
                    }
                    if (jump) jumps++;
                    sb.Append((f.T - t0).ToString("0.000", c)).Append(" | ").Append(f.Dt.ToString("0.000", c)).Append(" | ").Append(f.Phase)
                      .Append(" | ").Append((f.Pos.x * 100f).ToString("0", c)).Append(',').Append((f.Pos.y * 100f).ToString("0", c)).Append(',').Append((f.Pos.z * 100f).ToString("0", c))
                      .Append(" | ").Append((moved * 100f).ToString("0.0", c))
                      .Append(" | ").Append(Mathf.DeltaAngle(0f, e.y).ToString("0", c)).Append(',').Append((-Mathf.DeltaAngle(0f, e.x)).ToString("0", c)).Append(',').Append((-Mathf.DeltaAngle(0f, e.z)).ToString("0", c))
                      .Append(" | ").Append(turned.ToString("0.0", c)).Append(" | ").Append(f.Speed.ToString("0.0", c))
                      .Append(" | ").Append(f.Flap.ToString("0.00", c)).Append(", ").Append(f.Flare.ToString("0.00", c))
                      .Append(" |").Append(bones.ToString()).Append(jump ? " | JUMP" : "").AppendLine();
                }
                sb.AppendLine("## " + (tr.Munin ? "Munin" : "Hugin") + " end: " + jumps + " jumps");
            }
            string dir = Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "rec");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "crooked_" + System.DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            if (Plugin.Instance != null) Plugin.Instance.Log("Black box saved: " + file);
            return file;
        }
    }
}
#endif

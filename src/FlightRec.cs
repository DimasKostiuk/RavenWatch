#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A frame-by-frame record of one raven coming home and landing (the landing panel's «Записати» buttons): every
    // frame after everything else has moved it, where it is from its seat (cm, in the owner's frame), how it is turned
    // from the owner's way, its speed, the phase, and how far each main bone turned since the last frame. Frames with
    // a sudden jump are marked. Written to BepInEx/config/RavenWatch/rec/, a summary into the log.
    [DefaultExecutionOrder(32000)]
    internal class FlightRec : MonoBehaviour
    {
        internal static float JumpMove = 0.15f, JumpTurn = 15f, JumpBone = 25f; // per frame: what counts as a jump
        private static readonly string[] BoneNames = { "Armature", "Hip", "Spine", "Spine1", "Neck", "Wing1.l", "Wing1.r", "wing2.l", "wing2.r", "Leg1.l", "Leg1.r", "Tail" };

        private static FlightRec _now;
        internal static bool Busy { get { return _now != null; } }
        internal static string Last = "";

        private Player _owner;
        private bool _munin;
        private string _label;
        private GameObject _model;
        private bool _waitNew;
        private bool _fixed;            // a fixed length: does not stop when it sits          // the fly-in: wait for a new model to be made
        private GameObject _old;
        private float _t0, _sitSince = -1f, _endAt;
        private Transform[] _bones;
        private Quaternion[] _prev;
        private Vector3 _prevPos;
        private Quaternion _prevRot;
        private bool _havePrev;
        private int _frames, _jumps, _flicks;
        private Quaternion[] _prev2;
        private bool _havePrev2;
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<string> _jumpNotes = new List<string>();

        internal static void Start(Player owner, bool munin, string label, bool flyIn) { Start(owner, munin, label, flyIn, 0f); }

        // seconds > 0: a fixed length (sitting on the shoulder), not until it sits
        internal static void Start(Player owner, bool munin, string label, bool flyIn, float seconds)
        {
            if (seconds <= 0f && _now != null) Destroy(_now.gameObject);
            GameObject go = new GameObject("RavenWatch_FlightRec");
            FlightRec r = go.AddComponent<FlightRec>();
            r._owner = owner; r._munin = munin; r._label = label;
            r._waitNew = flyIn;
            r._old = flyIn ? Shoulder.ModelOf(owner, munin) : null;
            r._t0 = Time.time;
            r._endAt = Time.time + (seconds > 0f ? seconds : 25f);
            r._fixed = seconds > 0f;
            if (!flyIn) r.Attach(Shoulder.ModelOf(owner, munin));
            if (seconds <= 0f) _now = r;
            r._sb.AppendLine("# RavenWatch landing record: " + label + " (" + (munin ? "Munin" : "Hugin") + ") " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            r._sb.AppendLine("# t s | dt | phase | pos from seat cm (right,up,fwd) | moved cm | yaw,pitch,roll from owner's way | turned ° | speed m/s | bones turned ° this frame | JUMP");
        }

        private void Attach(GameObject model)
        {
            _model = model;
            if (model == null) return;
            List<Transform> list = new List<Transform>();
            foreach (string n in BoneNames)
            {
                Transform found = null;
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) { found = t; break; }
                list.Add(found);
            }
            _bones = list.ToArray();
            _prev = new Quaternion[_bones.Length];
            _prev2 = new Quaternion[_bones.Length];
        }

        private void LateUpdate()
        {
            if (_owner == null || Time.time > _endAt) { Finish(_fixed ? "done" : "timeout"); return; }
            if (_waitNew)
            {
                GameObject m = Shoulder.ModelOf(_owner, _munin);
                if (m == null || m == _old) return;
                _waitNew = false;
                Attach(m);
            }
            if (_model == null) { Finish("the raven is gone"); return; }
            Record();
        }

        private void Record()
        {
            Transform t = _model.transform;
            ShoulderPin pin = _model.GetComponent<ShoulderPin>();
            ShoulderLife life = _model.GetComponent<ShoulderLife>();
            Vector3 fwd = Vector3.ProjectOnPlane(_owner.transform.forward, Vector3.up).normalized;
            Quaternion facing = Quaternion.LookRotation(fwd, Vector3.up);
            Vector3 seat = pin != null && pin.SeatNow != Vector3.zero ? pin.SeatNow : _owner.GetHeadPoint();
            Vector3 rel = Quaternion.Inverse(facing) * (t.position - seat);
            Quaternion body = Quaternion.Inverse(facing) * t.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 e = body.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, e.y), pitch = -Mathf.DeltaAngle(0f, e.x), roll = -Mathf.DeltaAngle(0f, e.z);

            string phase;
            bool flying = _model.GetComponent<StrikeFlight>() != null;
            int mi = _munin ? 1 : 0;
            if (flying) phase = RavenFlight.NowFinal[mi] ? "final" : "fly";
            else if (life != null && life.Landing) phase = "land " + Mathf.RoundToInt(life.LandU * 100f) + "%";
            else if (pin != null && pin.InAir) phase = "air";
            else phase = "sit";
            if (life != null && life.BlendW >= 0f) phase += " blend " + Mathf.RoundToInt(life.BlendW * 100f) + "%";

            float moved = _havePrev ? (t.position - _prevPos).magnitude : 0f;
            float turned = _havePrev ? Quaternion.Angle(_prevRot, t.rotation) : 0f;
            bool jump = _havePrev && (moved > JumpMove || turned > JumpTurn);
            StringBuilder bones = new StringBuilder();
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                Quaternion local = Quaternion.Inverse(t.rotation) * _bones[i].rotation; // the bone's turn within the bird
                float d = _havePrev ? Quaternion.Angle(_prev[i], local) : 0f;
                // there and back: this frame far from the last, but back near the one before it
                bool flick = _havePrev2 && d >= 4f && Quaternion.Angle(_prev2[i], local) < d * 0.35f;
                _prev2[i] = _prev[i];
                _prev[i] = local;
                if (d >= (_fixed ? 1.5f : 3f)) bones.Append(' ').Append(BoneNames[i]).Append('=').Append(d.ToString("0.#")).Append(flick ? "!" : "");
                if (d > JumpBone) jump = true;
                if (flick) { jump = true; _flicks++; }
            }
            float speed = Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
            CultureInfo c = CultureInfo.InvariantCulture;
            _sb.Append((Time.time - _t0).ToString("0.000", c)).Append(" | ").Append(Time.deltaTime.ToString("0.000", c)).Append(" | ").Append(phase)
               .Append(" | ").Append((rel.x * 100f).ToString("0", c)).Append(',').Append((rel.y * 100f).ToString("0", c)).Append(',').Append((rel.z * 100f).ToString("0", c))
               .Append(" | ").Append((moved * 100f).ToString("0.0", c))
               .Append(" | ").Append(yaw.ToString("0", c)).Append(',').Append(pitch.ToString("0", c)).Append(',').Append(roll.ToString("0", c))
               .Append(" | ").Append(turned.ToString("0.0", c)).Append(" | ").Append(speed.ToString("0.0", c))
               .Append(" |").Append(bones.ToString()).Append(jump ? " | JUMP" : "").AppendLine();
            if (jump) { _jumps++; if (_jumpNotes.Count < 12) _jumpNotes.Add((Time.time - _t0).ToString("0.00", c) + "s " + phase + (moved > JumpMove ? " moved " + (moved * 100f).ToString("0") + "cm" : "") + (turned > JumpTurn ? " turned " + turned.ToString("0") + "°" : "") + bones); }
            _havePrev2 = _havePrev;
            _prevPos = t.position; _prevRot = t.rotation; _havePrev = true;
            _frames++;

            // done a moment after it sits
            if (_fixed) { }
            else if (phase.StartsWith("sit")) { if (_sitSince < 0f) _sitSince = Time.time; if (Time.time - _sitSince > 1.5f) Finish("sitting"); }
            else _sitSince = -1f;
        }

        private void Finish(string why)
        {
            if (!_fixed && _now != this) { Destroy(gameObject); return; }
            if (_now == this) _now = null;
            try
            {
                string dir = Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch");
                dir = Path.Combine(dir, "rec");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "landing_" + _label + "_" + (_munin ? "munin" : "hugin") + "_" + System.DateTime.Now.ToString("HHmmss") + ".txt");
                _sb.AppendLine("# end: " + why + ", " + _frames + " frames, " + _jumps + " jumps, " + _flicks + " flicks (a bone there and back: marked !)");
                File.WriteAllText(file, _sb.ToString(), new UTF8Encoding(false));
                Last = _label + ": " + _frames + " кадрів, стрибків " + _jumps;
                if (Plugin.Instance != null) Plugin.Instance.Log("Landing record " + _label + " (" + (_munin ? "Munin" : "Hugin") + "): " + _frames + " frames, " + _jumps + " jumps (" + why + ") -> " + file + (_jumpNotes.Count > 0 ? "; first jumps: " + string.Join(" / ", _jumpNotes.ToArray()) : ""));
            }
            catch (System.Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Landing record failed: " + ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif

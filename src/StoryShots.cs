#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The author's shots (the user 2026-11-02: «a simple sequencer — I set the camera and the angles myself»): a shot is
    // a camera — where it stands, where it looks, how wide — kept in the frame of one of the story's places, so it is
    // the same in every world that place stands in (as the names of its pieces: HallParts).
    //   files   assets/story/blueprints/<place>.shots.txt (built in) · config/RavenWatch/blueprints/<place>.shots.txt (the
    //           author's, read first); written by the window «Режисер» into both (the mod's own folder when
    //           story/source.txt says where it is — StoryFiles.Source)
    //   line    «name x y z qx qy qz qw fov» (fov 0: the shot's default)
    //   a shot is named «<place>.<name>» in a scene: camera shot prologue_house.door · camera move <a> <b> 4
    // The moves made of them (Shot, Move, Orbit) are poses asked each frame by StoryCamera.
    internal static class StoryShots
    {
        internal sealed class Shot { public string Name = ""; public Vector3 At; public Quaternion Rot = Quaternion.identity; public float Fov; }

        private const string Kind = "shots";
        private static readonly Dictionary<string, List<Shot>> _tables = new Dictionary<string, List<Shot>>();

        // read the files again (the windows' «Перечитати»: StoryScenes.Reload)
        internal static void Again() { _tables.Clear(); }

        private static string AuthorFile(string of) { return Path.Combine(Blueprint.Root, of + "." + Kind + ".txt"); }

        internal static List<Shot> Of(string of)
        {
            List<Shot> t;
            if (_tables.TryGetValue(of, out t)) return t;
            t = new List<Shot>();
            if (Blueprint.On && File.Exists(AuthorFile(of))) Read(File.ReadAllLines(AuthorFile(of), Encoding.UTF8), t);
            else
                using (Stream st = typeof(StoryShots).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + of + "." + Kind + ".txt"))
                    if (st != null) using (StreamReader r = new StreamReader(st, Encoding.UTF8)) Read(r.ReadToEnd().Replace("\r", "").Split('\n'), t);
            _tables[of] = t;
            return t;
        }

        private static void Read(string[] lines, List<Shot> into)
        {
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                string[] w = l.Split(' ');
                if (w.Length < 9) continue;
                float[] n = new float[8];
                bool ok = true;
                for (int i = 0; i < 8 && ok; i++) ok = float.TryParse(w[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i]);
                if (ok) into.Add(new Shot { Name = w[0], At = new Vector3(n[0], n[1], n[2]), Rot = new Quaternion(n[3], n[4], n[5], n[6]), Fov = n[7] });
            }
        }

        private static string Write(string of)
        {
            StringBuilder b = new StringBuilder("# RavenWatch: кадри камери місця «" + of + "» (вікно F9 «Режисер»). name x y z qx qy qz qw fov — у рамці місця.\n");
            foreach (Shot s in Of(of))
                b.Append(s.Name).Append(' ').Append(F(s.At.x)).Append(' ').Append(F(s.At.y)).Append(' ').Append(F(s.At.z)).Append(' ')
                 .Append(F(s.Rot.x)).Append(' ').Append(F(s.Rot.y)).Append(' ').Append(F(s.Rot.z)).Append(' ').Append(F(s.Rot.w)).Append(' ').Append(F(s.Fov)).Append('\n');
            string text = b.ToString();
            try
            {
                Directory.CreateDirectory(Blueprint.Root);
                File.WriteAllText(AuthorFile(of), text, new UTF8Encoding(false));
                if (StoryFiles.Source != null) // (the mod's own folder: the shots go out with it)
                {
                    string dir = Path.Combine(StoryFiles.Source, "blueprints");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, of + "." + Kind + ".txt"), text, new UTF8Encoding(false));
                }
                return null;
            }
            catch (Exception e) { return "не записалось: " + e.Message; }
        }

        private static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- the author's
        // the camera as it is now kept as a shot of that place (the same name: moved there)
        internal static string Take(string of, string name, StoryCamera.Pose now)
        {
            Loc l = Locs.Get(of);
            if (l == null) return "нема місця «" + of + "»";
            if (!Blueprint.GoodName(name)) return "Назва кадру: латиниця, цифри, _ чи -.";
            Vector3 local; Quaternion turn;
            if (!l.ToLocal(now.At, out local) || !l.Turn(out turn)) return "«" + of + "» ще не стоїть у цьому світі — кадр нема до чого прив'язати.";
            List<Shot> t = Of(of);
            Shot s = t.Find(x => x.Name == name);
            if (s == null) { s = new Shot { Name = name }; t.Add(s); }
            s.At = local; s.Rot = Quaternion.Inverse(turn) * now.Rot; s.Fov = now.Fov;
            string why = Write(of);
            return why ?? "Кадр «" + of + "." + name + "» записано.";
        }

        internal static string Remove(string of, string name)
        {
            int n = Of(of).RemoveAll(x => x.Name == name);
            if (n == 0) return "нема кадру «" + name + "»";
            return Write(of) ?? "Кадр «" + of + "." + name + "» прибрано.";
        }

        // the place a point is in (nearest standing one within reach) — the window's default for a new shot
        internal static Loc Near(Vector3 p, float reach)
        {
            Loc best = null; float bd = reach;
            foreach (Loc l in Locs.All)
            {
                Vector3 at; Quaternion r;
                if (!l.Stands || !l.Anchor(out at, out r)) continue;
                float d = Utils.DistanceXZ(at, p);
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }

        // ---------------------------------------------------------------- in a scene
        // «<place>.<name>» in this world (false: no such shot, or the place not standing here)
        internal static bool Find(string full, out StoryCamera.Pose pose)
        {
            pose = new StoryCamera.Pose { Rot = Quaternion.identity };
            int dot = full.IndexOf('.');
            if (dot <= 0) return false;
            string of = full.Substring(0, dot), name = full.Substring(dot + 1);
            if (of == "hall") of = HallSpawn.Name; // (the Hall by its story word)
            Loc l = Locs.Get(of);
            Shot s = l != null ? Of(of).Find(x => x.Name == name) : null;
            Vector3 at; Quaternion turn;
            if (s == null || !l.ToWorld(s.At, out at) || !l.Turn(out turn)) return false;
            pose = new StoryCamera.Pose { At = at, Rot = turn * s.Rot, Fov = s.Fov };
            return true;
        }

        // how a move eases: none (even), in (slow start), out (slow end), both (the default)
        internal static float Ease(string how, float t)
        {
            t = Mathf.Clamp01(t);
            switch (how)
            {
                case "none": return t;
                case "in": return t * t;
                case "out": return 1f - (1f - t) * (1f - t);
                default: return t * t * (3f - 2f * t);
            }
        }

        // a shot standing still; looking at something that moves when «look» is given, else as it was taken
        internal static Func<StoryCamera.Pose> Still(StoryCamera.Pose p, Func<Vector3> look)
        {
            return () => look == null ? p : Aim(p, look());
        }

        // from one shot to another in so many seconds (where, the turn, the width), eased; or looking at something on the way
        internal static Func<StoryCamera.Pose> Move(StoryCamera.Pose a, StoryCamera.Pose b, float seconds, string ease, Func<Vector3> look)
        {
            float start = Time.unscaledTime, dur = Mathf.Max(0.05f, seconds);
            return () =>
            {
                float k = Ease(ease, (Time.unscaledTime - start) / dur);
                StoryCamera.Pose p = new StoryCamera.Pose { At = Vector3.Lerp(a.At, b.At, k), Rot = Quaternion.Slerp(a.Rot, b.Rot, k), Fov = Mathf.Lerp(Wide(a.Fov), Wide(b.Fov), k) };
                return look == null ? p : Aim(p, look());
            };
        }

        // round something (it may move) at a radius and a height over it, so many degrees in so many seconds, from where
        // the camera stands now
        internal static Func<StoryCamera.Pose> Orbit(Func<Vector3> centre, float radius, float height, float degrees, float seconds, string ease, float fov)
        {
            float start = Time.unscaledTime, dur = Mathf.Max(0.05f, seconds);
            Vector3 c0 = centre(), from = StoryCamera.Now().At - c0; from.y = 0f;
            float a0 = from.sqrMagnitude > 0.01f ? Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg : 0f;
            return () =>
            {
                float a = a0 + degrees * Ease(ease, (Time.unscaledTime - start) / dur);
                Vector3 c = centre();
                Vector3 at = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * radius + Vector3.up * height;
                return Aim(new StoryCamera.Pose { At = at, Fov = fov }, c);
            };
        }

        private static float Wide(float fov) { return fov > 1f ? fov : StoryCamera.Fov; }

        private static StoryCamera.Pose Aim(StoryCamera.Pose p, Vector3 to)
        {
            Vector3 d = to - p.At;
            if (d.sqrMagnitude > 0.0001f) p.Rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            return p;
        }

        // ---------------------------------------------------------------- the author's free camera
        // flown on its own while the player stands: right mouse held — look and the wheel for the width; WASD, Q/E down and
        // up, Shift fast, Ctrl slow (not while typing in the window)
        // flying = the camera is driven by it now: any other shot, a scene or the camera given back ends it by itself
        internal static bool Flying { get { return StoryCamera.Driving(FlyPose); } }
        private static Vector3 _at;
        private static float _yaw, _pitch, _fov;

        internal static void Fly(bool on)
        {
            if (on == Flying) return;
            if (!on) { StoryCamera.Off(0.4f); return; }
            StoryCamera.Pose now = StoryCamera.Now();
            _at = now.At; Vector3 e = now.Rot.eulerAngles; _yaw = e.y; _pitch = e.x > 180f ? e.x - 360f : e.x; _fov = now.Fov;
            StoryCamera.Shot(FlyPose, 0f);
        }

        private static StoryCamera.Pose FlyPose()
        {
            float dt = Time.unscaledDeltaTime;
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * 3f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3f, -89f, 89f);
                _fov = Mathf.Clamp(_fov - Input.mouseScrollDelta.y * 2f, 15f, 100f);
            }
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            if (GUIUtility.keyboardControl == 0)
            {
                Vector3 m = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) m += rot * Vector3.forward;
                if (Input.GetKey(KeyCode.S)) m -= rot * Vector3.forward;
                if (Input.GetKey(KeyCode.D)) m += rot * Vector3.right;
                if (Input.GetKey(KeyCode.A)) m -= rot * Vector3.right;
                if (Input.GetKey(KeyCode.E)) m += Vector3.up;
                if (Input.GetKey(KeyCode.Q)) m -= Vector3.up;
                float speed = Input.GetKey(KeyCode.LeftShift) ? 14f : Input.GetKey(KeyCode.LeftControl) ? 1f : 4f;
                _at += m * speed * dt;
            }
            return new StoryCamera.Pose { At = _at, Rot = rot, Fov = _fov };
        }
    }
}
#endif

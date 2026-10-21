#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The raven's wing beat: corrections to the game's flying clip at ten points of each stroke (1–5 from the top down
    // to the bottom, 6–10 back up), each a turn of the three wing bones (shoulder, elbow, hand): swept forward/back,
    // lifted up/down, twisted along the wing. All zero = the game's beat as it is. The right wing mirrors the left.
    // In flight they follow each of the clip's strokes, including slower beats while gliding; adaptive strokes
    // (WingMods) go on top.
    internal static class WingKeys
    {
        internal const int Keys = 10, Bones = 3, Half = 5; // keys 1–5 the downstroke from the top, 6–10 the upstroke from the bottom
        // [key][bone] = (sweep, lift, twist) in degrees
        // the beat the user set (2026-10-11, RavenTest): the raven's own wing beat for everyone, unless a saved file says otherwise
        private static readonly float[,] Base =
        {
            { 0f, 2f, -8f, -3f, -16f, -2f, -2f, 47f, 90f }, // 1
            { 7f, -15f, -11f, 7f, -5f, -3f, -7f, 67f, 90f }, // 2
            { 1f, -17f, 12f, 7f, -5f, -3f, -18f, 53f, 90f }, // 3
            { 1f, -17f, 12f, 7f, -5f, -3f, -18f, 53f, 90f }, // 4
            { -2f, -17f, 3f, 7f, -5f, -3f, -36f, 33f, 17f }, // 5
            { 12f, -25f, -21f, 7f, -5f, -3f, -36f, 33f, 17f }, // 6
            { -13f, 0f, -30f, 10f, -16f, 14f, -36f, -1f, 5f }, // 7
            { -13f, 0f, -30f, 24f, -14f, 22f, -36f, -1f, 5f }, // 8
            { -13f, 0f, -30f, 24f, -14f, 27f, -3f, -7f, 28f }, // 9
            { -13f, -3f, -16f, 12f, -14f, 15f, 3f, 19f, 40f } // 10
        };
        // the body, neck, head and tail at the same points (2026-10-11): body, head, tail, neck × (pitch, yaw, roll)
        private static readonly float[,] BodyBase =
        {
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 1
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 2
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 3
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 4
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 5
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 6
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 7
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 8
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f }, // 9
            { -3f, 0f, 0f, 15f, 0f, 0f, 8f, 0f, 0f, 1f, 0f, 0f } // 10
        };
        internal static readonly Vector3[,] Pose = BasePose();
        internal static bool Active = true; // used in flight

        private static Vector3[,] BasePose()
        {
            Vector3[,] p = new Vector3[Keys, Bones];
            for (int k = 0; k < Keys; k++) for (int b = 0; b < Bones; b++) p[k, b] = new Vector3(Base[k, b * 3], Base[k, b * 3 + 1], Base[k, b * 3 + 2]);
            return p;
        }

        // back to the raven's own beat (the base above)
        internal static void ToBase()
        {
            Vector3[,] p = BasePose();
            for (int k = 0; k < Keys; k++) for (int b = 0; b < Bones; b++) Pose[k, b] = p[k, b];
            for (int k = 0; k < Keys; k++) for (int b = 0; b < BodyBones; b++) BodyPose[k, b] = BodyBaseAt(k, b); // the base beat's body turns
        }

        // The body, the head and the tail at the same ten points of a stroke: (pitch + down, yaw + right, roll + right
        // wing down) in degrees, in the bird's own frame, on top of the clip (and, in flight, under a template's own).
        // The game's raven has no neck bone of its own: Spine (the body, the wings hang on it) → Spine1 (the neck's
        // root) → Neck (the head with the beak). Indexes kept for the files: 0 body, 1 head, 2 tail, 3 neck (added later).
        internal const int BodyBones = 4; // 0 body (Spine), 1 head (Neck), 2 tail (Tail), 3 neck (Spine1)
        internal static readonly int[] BodyShown = { 0, 3, 1, 2 };      // as shown and turned: body, neck, head, tail (parents first)
        internal static readonly string[] BodyNames = { "Тулуб", "Голова", "Хвіст", "Шия" };
        internal static readonly Vector3[,] BodyPose = BodyBasePose();
        private static Vector3 BodyBaseAt(int k, int b) { return new Vector3(BodyBase[k, b * 3], BodyBase[k, b * 3 + 1], BodyBase[k, b * 3 + 2]); }
        private static Vector3[,] BodyBasePose() { Vector3[,] p = new Vector3[Keys, BodyBones]; for (int k = 0; k < Keys; k++) for (int b = 0; b < BodyBones; b++) p[k, b] = BodyBaseAt(k, b); return p; }

        internal static Vector3 BodyAt(float x, int bone)
        {
            x = Mathf.Repeat(x, Keys);
            int a = Mathf.Clamp((int)x, 0, Keys - 1), b = (a + 1) % Keys;
            float f = Mathf.Clamp01(x - a);
            f = f * f * (3f - 2f * f);
            return Vector3.Lerp(BodyPose[a, bone], BodyPose[b, bone], f);
        }

        internal static Transform[] FindBody(Transform root)
        {
            Transform[] t = new Transform[BodyBones];
            foreach (Transform x in root.GetComponentsInChildren<Transform>(true))
            {
                if (t[0] == null && x.name == "Spine") t[0] = x;
                else if (t[1] == null && x.name == "Neck") t[1] = x;
                else if (t[2] == null && x.name == "Tail") t[2] = x;
                else if (t[3] == null && x.name == "Spine1") t[3] = x;
            }
            return t;
        }

        // the turns onto the body, head and tail (facing: the bird's own frame)
        internal static void ApplyBody(Transform[] body, Quaternion facing, Vector3[] add)
        {
            if (body == null || add == null) return;
            Vector3 right = facing * Vector3.right, up = facing * Vector3.up, fwd = facing * Vector3.forward;
            foreach (int i in BodyShown) // parents first: the body carries the neck, the neck the head
            {
                if (i >= body.Length || i >= add.Length) continue;
                if (body[i] == null) continue;
                Vector3 p = add[i];
                if (p.sqrMagnitude < 1e-6f) continue;
                body[i].rotation = Quaternion.AngleAxis(p.x, right) * Quaternion.AngleAxis(p.y, up) * Quaternion.AngleAxis(-p.z, fwd) * body[i].rotation;
            }
        }

        internal static string FilePath { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "wingkeys.txt"); } }

        // Where the keys sit: each of the clip's strokes on its own (they differ in length), keys 1–5 from its top to
        // its bottom, 6–10 from its bottom to the next top. x: the key coordinate, 0..10 (round).
        internal static float KeyX(float u)
        {
            float[] T = ShoulderLife.StrokeTops;
            if (T == null || T.Length == 0) return Mathf.Repeat(u, 1f) * Keys;
            u = Mathf.Repeat(u, 1f);
            int i = StrokeAt(u);
            float t0, bt, t1;
            Stroke(i, out t0, out bt, out t1);
            if (u < t0) u += 1f;
            return u < bt ? (u - t0) / Mathf.Max(1e-4f, bt - t0) * Half : Half + (u - bt) / Mathf.Max(1e-4f, t1 - bt) * Half;
        }

        // the point of the loop (0..1) where stroke s is at key coordinate x
        internal static float LoopU(int s, float x)
        {
            float[] T = ShoulderLife.StrokeTops;
            if (T == null || T.Length == 0) return Mathf.Repeat(x / Keys, 1f);
            float t0, bt, t1;
            Stroke(Mathf.Clamp(s, 0, T.Length - 1), out t0, out bt, out t1);
            x = Mathf.Repeat(x, Keys);
            return Mathf.Repeat(x < Half ? t0 + x / Half * (bt - t0) : bt + (x - Half) / Half * (t1 - bt), 1f);
        }

        // which stroke this point of the loop is in (the last top at or before it, round)
        internal static int StrokeAt(float u)
        {
            float[] T = ShoulderLife.StrokeTops;
            if (T == null || T.Length == 0) return 0;
            u = Mathf.Repeat(u, 1f);
            int i = T.Length - 1;
            for (int k = 0; k < T.Length; k++) if (T[k] <= u) i = k;
            return i;
        }

        // stroke i's top, bottom and next top, in loop points (made increasing across the loop's end)
        private static void Stroke(int i, out float t0, out float bt, out float t1)
        {
            float[] T = ShoulderLife.StrokeTops, B = ShoulderLife.StrokeBottoms;
            t0 = T[i]; t1 = i + 1 < T.Length ? T[i + 1] : T[0] + 1f;
            bt = B[i]; if (bt < t0) bt += 1f;
        }

        // the length of stroke s, as a share of the loop
        internal static float StrokeShare(int s)
        {
            float[] T = ShoulderLife.StrokeTops;
            if (T == null || T.Length == 0) return 1f;
            float t0, bt, t1;
            Stroke(Mathf.Clamp(s, 0, T.Length - 1), out t0, out bt, out t1);
            return t1 - t0;
        }

        // the correction at key coordinate x (0..10, round): smooth between the keys, from 10 back to 1
        internal static Vector3 At(float x, int bone)
        {
            x = Mathf.Repeat(x, Keys);
            int a = Mathf.Clamp((int)x, 0, Keys - 1), b = (a + 1) % Keys;
            float f = Mathf.Clamp01(x - a);
            f = f * f * (3f - 2f * f);
            return Vector3.Lerp(Pose[a, bone], Pose[b, bone], f);
        }

        // after the clip has posed the wings this frame: turn them by the correction; facing: the bird's own frame
        // add: the adaptive strokes' own turns (WingMods), per bone; the keys only while they are switched on
        internal static void Apply(Transform[] l, Transform[] r, Quaternion facing, float phase, Vector3[] addL, Vector3[] addR, float dive = 0f)
        {
            Vector3 fwd = facing * Vector3.forward, up = facing * Vector3.up;
            Wing(l, 1f, fwd, up, phase, addL, dive, true);
            Wing(r, -1f, fwd, up, phase, addR, dive, true);
        }

        // only these turns, without the base beat (a landing template over the game's landing clip)
        internal static void ApplyAdds(Transform[] l, Transform[] r, Quaternion facing, Vector3[] addL, Vector3[] addR)
        {
            Vector3 fwd = facing * Vector3.forward, up = facing * Vector3.up;
            Wing(l, 1f, fwd, up, 0f, addL, 0f, false);
            Wing(r, -1f, fwd, up, 0f, addR, 0f, false);
        }

        private static void Wing(Transform[] t, float side, Vector3 fwd, Vector3 up, float phase, Vector3[] add, float dive, bool withBase)
        {
            if (t == null) return;
            dive = Mathf.Clamp01(dive);
            for (int i = 0; i < Bones; i++) // parents first: the children follow
            {
                if (t[i] == null) continue;
                Vector3 p = (withBase && Active ? At(phase, i) : Vector3.zero) + (add != null ? add[i] : Vector3.zero);
                if (dive > 0f)
                {
                    float fold = i == 0 ? 18f : i == 1 ? 25f : 17f;
                    float lift = i == 0 ? 2f : i == 1 ? 1.5f : 1f;
                    p.x -= fold * dive;
                    p.y = p.y * Mathf.Lerp(1f, 0.1f, dive) + lift * dive + Mathf.Sin(phase * Mathf.PI * 2f / Keys) * 1.25f * dive;
                }
                if (p.sqrMagnitude < 1e-6f) continue;
                Vector3 along = i + 1 < Bones && t[i + 1] != null ? (t[i + 1].position - t[i].position) : (t[i].position - t[i].parent.position);
                Quaternion turn = Quaternion.AngleAxis(-side * p.y, fwd) * Quaternion.AngleAxis(side * p.x, up);
                t[i].rotation = turn * t[i].rotation;
                if (along.sqrMagnitude > 1e-8f) t[i].rotation = Quaternion.AngleAxis(side * p.z, (turn * along).normalized) * t[i].rotation;
            }
        }

        internal static void Find(Transform root, out Transform[] l, out Transform[] r)
        {
            l = new Transform[Bones]; r = new Transform[Bones];
            string[] names = { "wing1", "wing2", "wing3" };
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                for (int i = 0; i < Bones; i++)
                {
                    if (n == names[i] + ".l") l[i] = t;
                    else if (n == names[i] + ".r") r[i] = t;
                }
            }
        }

        internal static void Reset()
        {
            for (int k = 0; k < Keys; k++) for (int b = 0; b < Bones; b++) Pose[k, b] = Vector3.zero;
            for (int k = 0; k < Keys; k++) for (int b = 0; b < BodyBones; b++) BodyPose[k, b] = Vector3.zero;
        }

        internal static void Save()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder("# RavenWatch wing beat corrections: 10 keys (1–5 down from the top, 6–10 up from the bottom) × (shoulder, elbow, hand) × (sweep, lift, twist) in degrees, on top of the game's beat; first line: on 1|0\n");
            sb.Append(Active ? "1" : "0").Append('\n');
            for (int k = 0; k < Keys; k++)
            {
                for (int b = 0; b < Bones; b++) sb.Append(Pose[k, b].x.ToString("0.#", c)).Append(' ').Append(Pose[k, b].y.ToString("0.#", c)).Append(' ').Append(Pose[k, b].z.ToString("0.#", c)).Append(b < Bones - 1 ? "  " : "");
                sb.Append('\n');
            }
            for (int k = 0; k < Keys; k++)
            {
                bool any = false;
                for (int b = 0; b < BodyBones; b++) if (BodyPose[k, b] != Vector3.zero) any = true;
                if (!any) continue;
                sb.Append("body ").Append(k + 1);
                for (int b = 0; b < BodyBones; b++) sb.Append("  ").Append(BodyPose[k, b].x.ToString("0.#", c)).Append(' ').Append(BodyPose[k, b].y.ToString("0.#", c)).Append(' ').Append(BodyPose[k, b].z.ToString("0.#", c));
                sb.Append('\n');
            }
            sb.Append("body 0\n"); // the body is in this file (even all zero): the baked body beat is not used
            sb.Append("mods ").Append(WingMods.On ? "1" : "0").Append(' ').Append(WingMods.Depth.ToString("0.##", c)).Append(' ').Append(WingMods.Turn.ToString("0.##", c)).Append(' ').Append(WingMods.Variety.ToString("0.##", c)).Append(' ').Append(WingMods.Nature.ToString("0.##", c)).Append('\n');
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            if (Plugin.Instance != null) Plugin.Instance.Log("Wing keys saved: " + FilePath + "\n" + sb);
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                CultureInfo c = CultureInfo.InvariantCulture;
                List<string> lines = new List<string>();
                foreach (string l in File.ReadAllLines(FilePath)) if (l.Trim().Length > 0 && !l.TrimStart().StartsWith("#")) lines.Add(l.Trim());
                List<string> keyLines = new List<string>();
                foreach (string l in lines) if (!l.StartsWith("mods ") && !l.StartsWith("body ")) keyLines.Add(l);
                bool hasBody = false;
                foreach (string l in lines) if (l.StartsWith("body ")) { hasBody = true; break; }
                if (hasBody) for (int k = 0; k < Keys; k++) for (int b = 0; b < BodyBones; b++) BodyPose[k, b] = Vector3.zero; // (no body lines: the baked body beat stays)
                foreach (string l in lines)
                {
                    if (!l.StartsWith("body ")) continue;
                    string[] v = l.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    int key;
                    if (v.Length < 2 || !int.TryParse(v[1], out key) || key < 1 || key > Keys) continue;
                    for (int b = 0; b < BodyBones && 2 + b * 3 + 2 < v.Length; b++)
                    {
                        float x, y, z;
                        float.TryParse(v[2 + b * 3], NumberStyles.Float, c, out x); float.TryParse(v[3 + b * 3], NumberStyles.Float, c, out y); float.TryParse(v[4 + b * 3], NumberStyles.Float, c, out z);
                        BodyPose[key - 1, b] = new Vector3(x, y, z);
                    }
                }
                int n = keyLines.Count - 1 >= Keys ? Keys : keyLines.Count - 1 >= 5 ? 5 : 0;
                if (n == 0) return;
                string[] head = keyLines[0].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                Active = head.Length > 0 && head[0] == "1";
                int step = Keys / n;
                for (int k = 0; k < n; k++)
                {
                    string[] v = keyLines[k + 1].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int b = 0; b < Bones && b * 3 + 2 < v.Length; b++)
                    {
                        float x, y, z;
                        float.TryParse(v[b * 3], NumberStyles.Float, c, out x); float.TryParse(v[b * 3 + 1], NumberStyles.Float, c, out y); float.TryParse(v[b * 3 + 2], NumberStyles.Float, c, out z);
                        Pose[k * step, b] = new Vector3(x, y, z);
                    }
                }
                foreach (string l in lines)
                {
                    if (!l.StartsWith("mods ")) continue;
                    string[] m = l.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    float f;
                    if (m.Length > 1) WingMods.On = m[1] == "1";
                    if (m.Length > 2 && float.TryParse(m[2], NumberStyles.Float, c, out f)) WingMods.Depth = f;
                    if (m.Length > 3 && float.TryParse(m[3], NumberStyles.Float, c, out f)) WingMods.Turn = f;
                    if (m.Length > 4 && float.TryParse(m[4], NumberStyles.Float, c, out f)) WingMods.Variety = f;
                    if (m.Length > 5 && float.TryParse(m[5], NumberStyles.Float, c, out f)) WingMods.Nature = f;
                }
                if (step == 2) for (int k = 1; k < Keys; k += 2) for (int b = 0; b < Bones; b++) Pose[k, b] = (Pose[k - 1, b] + Pose[(k + 1) % Keys, b]) * 0.5f;
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Wing keys unreadable: " + e.Message); }
        }
    }

    // The panel's own raven: a copy far below the world, posed each frame (LateUpdate) from the game's flying clip at
    // the chosen point (levelled as in flight), the corrections on top, and filmed then by its own camera. Its meshes
    // are switched on by hand (the prefab hides them until the raven "appears"), its body held where the idle clip
    // has it (the prefab's own pose is far off), and the camera follows the hip.
    internal static class WingStage
    {
        private const int Size = 420;
        private static GameObject _stage, _model;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static Transform[] _l, _r, _body;
        private static readonly Vector3[] _bodyAdd = new Vector3[WingKeys.BodyBones];
        private static Animator _anim;
        private static AnimationClip _clip;
        private static Transform[] _roots;
        private static readonly List<GameObject> _meshes = new List<GameObject>(); // hidden in the prefab until the raven "appears"
        private static Vector3[] _rootPos;
        private static Vector3 _center; // from the hip (the camera follows the body)
        private static Transform _hip;
        private static float _radius = 0.5f;
        private static readonly List<Light> _lamps = new List<Light>();
        private static readonly Color Back = new Color(0.2f, 0.22f, 0.26f, 1f);
        internal static float Yaw = 180f, Pitch = 10f, Zoom = 1.3f;
        internal static string Problem = "";
        internal static RenderTexture Texture { get { return _rt; } }

        private static float _retryAt;

        internal static bool Show(bool munin)
        {
            if (_stage != null && _model != null) return true;
            if (Time.unscaledTime < _retryAt) return false;
            _retryAt = Time.unscaledTime + 1f;
            if (!ShoulderLife.BeatKnown)
            {
                // the beat is measured once on a shoulder raven (as on its first flight)
                Player p = Player.m_localPlayer;
                GameObject m = p != null ? (Shoulder.ModelOf(p, false) ?? Shoulder.ModelOf(p, true)) : null;
                ShoulderLife life = m != null ? m.GetComponent<ShoulderLife>() : null;
                if (life != null) life.EnsureLoop();
                if (!ShoulderLife.BeatKnown) { Problem = "Потрібен ворон на плечі: з нього міряється змах гри."; return false; }
            }
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) { Problem = "Не знайдено модель ворона."; return false; }
            int layer = RavenFx.FreeLayer();
            if (layer < 0) { Problem = "Немає вільного шару для картинки."; return false; }
            Problem = "";
            _stage = new GameObject("RavenWatch_WingStage");
            _stage.transform.position = new Vector3(0f, 9200f, 0f);
            _model = Object.Instantiate(src.m_visual, _stage.transform, false);
            AppearMute.Quiet(_model);
            _model.SetActive(true);
            Shared.RemoveExclamation(src, _model);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = ShoulderLife.LevelFix; // the clip's nose-up taken out, as in flight
            foreach (Collider c in _model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            _anim = null;
            foreach (Animator a in _model.GetComponentsInChildren<Animator>(true)) { a.enabled = false; if (_anim == null) _anim = a; }
            _clip = ShoulderLife.FlyClipOf(_anim);
            if (_anim == null || _clip == null) { Problem = "У моделі немає кліпу польоту."; Close(); return false; }
            List<Transform> roots = new List<Transform>();
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
            {
                if (t != _model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
                t.gameObject.layer = layer;
                string n = t.name.ToLowerInvariant();
                if (n == "armature" || n == "hip" || n == "hips" || n == "root" || n == "body") roots.Add(t);
            }
            _meshes.Clear();
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                r.enabled = true; _meshes.Add(r.gameObject);
                for (Transform t = r.transform; t != null && t != _model.transform; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
                if (sk != null) { sk.updateWhenOffscreen = true; sk.skinnedMotionVectors = false; }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            WingKeys.Find(_model.transform, out _l, out _r);
            _body = WingKeys.FindBody(_model.transform);
            // the clip lifts the body up and away: held where it sits, as the flight does; where it sits is the
            // game's idle clip (the prefab's own pose holds the body far off, as found on the shoulder)
            AnimationClip idle = null;
            if (_anim.runtimeAnimatorController != null)
                foreach (AnimationClip c in _anim.runtimeAnimatorController.animationClips)
                    if (c != null && c.name.ToLowerInvariant().Contains("idle") && c.isLooping) { idle = c; break; }
            if (idle != null) idle.SampleAnimation(_anim.gameObject, 0f);
            _hip = null;
            foreach (Transform t in roots) { string n = t.name.ToLowerInvariant(); if (n == "hip" || n == "hips") _hip = t; }
            if (_hip == null && roots.Count > 0) _hip = roots[0];
            _roots = roots.ToArray();
            _rootPos = new Vector3[_roots.Length];
            for (int i = 0; i < _roots.Length; i++) _rootPos[i] = _roots[i].localPosition;

            // framed around the spread wings, by the bones (a mesh's bounds are not right before its first render)
            PoseKey(0, 2f);
            Vector3 hp = _hip != null ? _hip.position : _model.transform.position;
            Bounds b = new Bounds(hp, Vector3.one * 0.1f);
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true)) if (t != _model.transform) b.Encapsulate(t.position);
            _center = b.center - hp; _radius = Mathf.Clamp(b.extents.magnitude * 1.15f, 0.2f, 6f);
            if (Plugin.Instance != null) Plugin.Instance.Log("Wing panel: raven at " + _model.transform.position + ", hip " + hp + " (idle clip " + (idle != null ? idle.name : "none") + "), framed +" + _center + " r " + _radius.ToString("0.00") + ", loop " + ShoulderLife.BeatStart.ToString("0.00") + "+" + ShoulderLife.LoopLength.ToString("0.00") + " s, " + ShoulderLife.StrokeCount + " strokes of " + _clip.name + ", layer " + layer + ", wing bones " + (_l[0] != null) + "/" + (_r[0] != null));

            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.cullingMask = 1 << layer; _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Back; _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.02f; _cam.farClipPlane = 50f; _cam.enabled = false;
            _cam.renderingPath = RenderingPath.Forward; _cam.allowHDR = false; _cam.allowMSAA = false;
            _lamps.Clear();
            Lamp("Key", 1.2f, Color.white, Quaternion.Euler(40f, 150f, 0f), layer);
            Lamp("Fill", 0.6f, new Color(0.8f, 0.85f, 1f), Quaternion.Euler(20f, -40f, 0f), layer);
            Lamp("Back", 0.4f, Color.white, Quaternion.Euler(-30f, 0f, 0f), layer);
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); _rt.Create();
            _cam.targetTexture = _rt;
            Aim();
            return true;
        }

        private static void Lamp(string name, float power, Color c, Quaternion turn, int layer)
        {
            Light l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(_stage.transform, false); l.type = LightType.Directional; l.intensity = power; l.color = c;
            l.cullingMask = 1 << layer; l.shadows = LightShadows.None; l.transform.rotation = turn; l.enabled = false;
            _lamps.Add(l);
        }

        internal static void Close()
        {
            if (_stage != null) Object.Destroy(_stage);
            _stage = null; _model = null; _cam = null; _anim = null;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
        }

        // stroke s at key coordinate x (0..10), then the corrections
        internal static void PoseKey(int s, float x) { PoseAt(WingKeys.LoopU(s, x), Mathf.Repeat(x, WingKeys.Keys)); }

        // the whole flying loop as the flight plays it (0..1), the corrections where each stroke is
        internal static void PoseLoop(float u) { u -= Mathf.Floor(u); PoseAt(u, WingKeys.KeyX(u)); }

        private static void PoseAt(float u, float x)
        {
            _u = u;
            Pose(ShoulderLife.BeatStart + u * ShoulderLife.LoopLength, x);
        }

        // the flight state the picture shows the adaptive strokes in (0: none, the keys alone)
        internal static int Preview;
        internal static readonly string[] PreviewNames = { "Без адаптиву", "Звичайний", "Підйом", "Ковзання вниз", "Віраж вправо", "Віраж вліво", "Ниряння" };
        private static readonly WingMods.Runner _mods = new WingMods.Runner();
        private static readonly Vector3[] _addL = new Vector3[WingKeys.Bones], _addR = new Vector3[WingKeys.Bones];
        private static float _u;
        private static int _previewWas = -1;
        internal static bool Still { set { _mods.Settled = value; } } // the panel paused on a key

        private static WingMods.State PreviewState()
        {
            WingMods.State st = new WingMods.State();
            st.MaxBank = RavenFlight.Hugin.Bank; st.Effort = 0.3f;
            if (Preview == 2) { st.Effort = 1f; st.Climb = 1f; }
            else if (Preview == 3) { st.Effort = 0f; st.Climb = -1f; }
            else if (Preview == 4) st.Bank = st.MaxBank;
            else if (Preview == 5) st.Bank = -st.MaxBank;
            else if (Preview == 6) { st.Effort = 0f; st.Climb = -1f; st.Dive = 1f; }
            return st;
        }

        private static void Pose(float time, float phase)
        {
            if (_model == null || _anim == null || _clip == null) return;
            Transform root = _anim.transform;
            Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale;
            _clip.SampleAnimation(_anim.gameObject, time);
            root.localPosition = lp; root.localRotation = lr; root.localScale = ls;
            for (int i = 0; i < _roots.Length; i++) if (_roots[i] != null) _roots[i].localPosition = _rootPos[i];
            foreach (GameObject g in _meshes) if (g != null && !g.activeSelf) g.SetActive(true); // the clip may hide it
            // the bird's own frame without the levelling, as in flight
            Quaternion facing = _stage.transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            if (Preview > 0 && WingMods.On)
            {
                if (Preview != _previewWas) { _mods.Reset(); _previewWas = Preview; }
                WingMods.State st = PreviewState();
                if (Preview == 4 || Preview == 5) _mods.Step(_u, phase, st, false, 1f, _addL, _addR); // the bank shown at once
                else _mods.Step(_u, phase, st, false, Time.unscaledDeltaTime, _addL, _addR);
                WingKeys.Apply(_l, _r, facing, phase, _addL, _addR, Preview == 6 ? st.Dive : 0f);
            }
            else WingKeys.Apply(_l, _r, facing, phase, null, null, Preview == 6 ? 1f : 0f);
            if (WingKeys.Active) { for (int i = 0; i < WingKeys.BodyBones; i++) _bodyAdd[i] = WingKeys.BodyAt(phase, i); WingKeys.ApplyBody(_body, facing, _bodyAdd); }
            Aim();
            if (_cam == null) return;
            foreach (Light l in _lamps) if (l != null) l.enabled = true;
            try { _cam.Render(); }
            finally { foreach (Light l in _lamps) if (l != null) l.enabled = false; }
        }

        private static void Aim()
        {
            if (_cam == null) return;
            Quaternion look = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 c = (_hip != null ? _hip.position : _model.transform.position) + _center;
            _cam.transform.position = c - look * Vector3.forward * (_radius / Mathf.Tan(15f * Mathf.Deg2Rad)) / Zoom;
            _cam.transform.rotation = look;
        }
    }

    // The «Змах крил» tab of the test window (DevPanel; console: raven wingui): the wing beat's ten points and the
    // adaptive strokes, set by hand on a turning picture of the raven. Enabled only while its tab is shown.
    internal class WingPanel : MonoBehaviour
    {
        private int _key;
        private int _stroke; // which of the clip's strokes the keys are shown on
        private bool _play, _whole; // playing: one beat round and round / the whole flying loop
        private float _phase, _speed = 0.5f;
        private string _note = "";
        private Vector2 _scroll;

        // the tab left or the window closed: the picture goes
        internal void Closed() { enabled = false; WingStage.Close(); }

        private void Update()
        {
            if (_play) _phase += Time.unscaledDeltaTime * _speed / Mathf.Max(0.05f, ShoulderLife.LoopLength * (_whole ? 1f : WingKeys.StrokeShare(_stroke)));
        }

        // posed after everything else this frame; the stage camera renders it in the game's own order
        private void LateUpdate()
        {
            if (WingStage.Show(false))
            {
                WingStage.Still = !_play;
                if (_play && _whole) WingStage.PoseLoop(_phase);
                else WingStage.PoseKey(_stroke, _play ? (_phase - Mathf.Floor(_phase)) * WingKeys.Keys : _key);
            }
        }

        // the tab's body (inside the test window)
        internal void Body()
        {
            GUILayout.BeginHorizontal();

            // the picture, turned by dragging it
            GUILayout.BeginVertical(GUILayout.Width(430f));
            Rect pic = GUILayoutUtility.GetRect(420f, 420f, GUILayout.Width(420f), GUILayout.Height(420f));
            if (WingStage.Texture != null) GUI.DrawTexture(pic, WingStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, WingStage.Problem);
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition)) { WingStage.Yaw += e.delta.x * 0.6f; WingStage.Pitch = Mathf.Clamp(WingStage.Pitch + e.delta.y * 0.4f, -80f, 80f); e.Use(); }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { WingStage.Zoom = Mathf.Clamp(WingStage.Zoom - e.delta.y * 0.05f, 0.5f, 4f); e.Use(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { WingStage.Yaw = 0f; WingStage.Pitch = 5f; }
            if (GUILayout.Button("Збоку")) { WingStage.Yaw = 90f; WingStage.Pitch = 5f; }
            if (GUILayout.Button("Ззаду")) { WingStage.Yaw = 180f; WingStage.Pitch = 5f; }
            if (GUILayout.Button("Згори")) { WingStage.Pitch = 80f; }
            GUILayout.EndHorizontal();
            GUILayout.Label("Тягни картинку — крутити, коліщатко — ближче/далі.");
            GUILayout.BeginHorizontal();
            bool beat = GUILayout.Toggle(_play && !_whole, _play && !_whole ? " ⏸ Пауза" : " ▶ Один змах", "Button");
            bool whole = GUILayout.Toggle(_play && _whole, _play && _whole ? " ⏸ Пауза" : " ▶ Увесь політ (" + ShoulderLife.StrokeCount + " змахів)", "Button");
            if (beat != (_play && !_whole)) { _play = beat; _whole = false; _phase = 0f; }
            else if (whole != (_play && _whole)) { _play = whole; _whole = whole; _phase = 0f; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Темп", GUILayout.Width(40f));
            _speed = GUILayout.HorizontalSlider(_speed, 0.1f, 1.5f, GUILayout.Width(200f));
            GUILayout.Label("×" + _speed.ToString("0.0"), GUILayout.Width(40f));
            if (_play && _whole) GUILayout.Label("змах " + (WingKeys.StrokeAt(_phase) + 1) + " з " + ShoulderLife.StrokeCount);
            GUILayout.EndHorizontal();
            // the clip's strokes differ (slower first, quicker later): the keys are shown on one of them
            GUILayout.BeginHorizontal();
            int n = ShoulderLife.StrokeCount;
            GUILayout.Label("Показувати на змаху:", GUILayout.Width(150f));
            if (GUILayout.Button("‹", GUILayout.Width(28f))) _stroke = (_stroke + n - 1) % n;
            GUILayout.Label((Mathf.Clamp(_stroke, 0, n - 1) + 1) + " з " + n + " (" + (WingKeys.StrokeShare(_stroke) * ShoulderLife.LoopLength).ToString("0.00") + " с)", GUILayout.Width(110f));
            if (GUILayout.Button("›", GUILayout.Width(28f))) _stroke = (_stroke + 1) % n;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // the switch and saving first (always in sight), then the keys and the bones
            GUILayout.BeginVertical();
            WingKeys.Active = GUILayout.Toggle(WingKeys.Active, WingKeys.Active ? " У польоті: ГРА + МОЇ ПРАВКИ (натисни — вимкнути)" : " У польоті: тільки змах гри (натисни — увімкнути правки)", "Button");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти")) { WingKeys.Save(); _note = WingKeys.Active ? "Збережено. Відправ ворона з плеча й подивись." : "Збережено (правки кадрів у польоті вимкнені)."; }
            if (GUILayout.Button("До основи")) { WingKeys.ToBase(); _note = "Повернуто основний змах ворона (не збережено, доки не натиснеш «Зберегти»)."; }
            if (GUILayout.Button("Змах гри (усе 0)")) { WingKeys.Reset(); _note = "Усі правки 0: змах як у гри (не збережено)."; }
            GUILayout.EndHorizontal();
            GUILayout.Label(_note.Length > 0 ? _note : "Основа = змах, який ти підібрав. 0 скрізь = змах самої гри.");
            GUILayout.Label("Кадр:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Опускання (з верху донизу):");
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
            for (int k = 0; k < WingKeys.Keys; k++)
            {
                if (k == WingKeys.Half) { GUILayout.EndHorizontal(); GUILayout.Label("Підйом (з низу догори):"); GUILayout.BeginHorizontal(); }
                string name = k == 0 ? "1 верх" : k == WingKeys.Half ? (k + 1) + " низ" : (k + 1).ToString();
                if (GUILayout.Toggle(_key == k && !_play, name, "Button")) { if (_key != k || _play) { _key = k; _play = false; } }
            }
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            string[] bones = { "Плече", "Лікоть", "Кисть" };
            for (int b = 0; b < WingKeys.Bones; b++)
            {
                GUILayout.Label(bones[b] + ":");
                Vector3 v = WingKeys.Pose[_key, b];
                v.x = Row("вперед-назад", v.x);
                v.y = Row("вгору-вниз", v.y);
                v.z = Row("поворот уздовж", v.z);
                if (WingKeys.Pose[_key, b] != v) _play = false; // a change shows its own key
                WingKeys.Pose[_key, b] = v;
            }
            foreach (int b in WingKeys.BodyShown)
            {
                GUILayout.Label(WingKeys.BodyNames[b] + ":");
                Vector3 v = WingKeys.BodyPose[_key, b];
                v.x = Row("нахил (+ вниз)", v.x);
                v.y = Row("поворот (+ вправо)", v.y);
                v.z = Row("крен (+ вправо)", v.z);
                if (WingKeys.BodyPose[_key, b] != v) _play = false;
                WingKeys.BodyPose[_key, b] = v;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копія з попереднього")) { int p = (_key + WingKeys.Keys - 1) % WingKeys.Keys; for (int b = 0; b < WingKeys.Bones; b++) WingKeys.Pose[_key, b] = WingKeys.Pose[p, b]; for (int b = 0; b < WingKeys.BodyBones; b++) WingKeys.BodyPose[_key, b] = WingKeys.BodyPose[p, b]; }
            if (GUILayout.Button("Скинути кадр")) { for (int b = 0; b < WingKeys.Bones; b++) WingKeys.Pose[_key, b] = Vector3.zero; for (int b = 0; b < WingKeys.BodyBones; b++) WingKeys.BodyPose[_key, b] = Vector3.zero; }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            WingMods.On = GUILayout.Toggle(WingMods.On, WingMods.On ? " Адаптивний змах: УВІМКНЕНО (натисни — вимкнути)" : " Адаптивний змах: вимкнено (натисни — увімкнути)", "Button");
            GUILayout.Label("Кожен змах трохи інший — за тим, що робить ворон. Сила: 0 = вимкнено, 1 = звичайно, 2 = удвічі.");
            WingMods.Depth = Strength("Глибина (підйом / ковзання)", WingMods.Depth);
            WingMods.Turn = Strength("Віраж (внутрішнє крило менше)", WingMods.Turn);
            WingMods.Variety = Strength("Різнобій змахів", WingMods.Variety);
            WingMods.Nature = Strength("Характер (Хугін / Мунін)", WingMods.Nature);
            GUILayout.Label("Показати на картинці, як змах виглядає:");
            WingStage.Preview = GUILayout.SelectionGrid(WingStage.Preview, WingStage.PreviewNames, 3);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private static float Strength(string name, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(210f));
            v = GUILayout.HorizontalSlider(v, 0f, 2f, GUILayout.Width(150f));
            v = Mathf.Round(v * 10f) / 10f;
            GUILayout.Label("×" + v.ToString("0.0"), GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return v;
        }

        private static float Row(string name, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(120f));
            if (GUILayout.Button("−5", GUILayout.Width(30f))) v -= 5f;
            if (GUILayout.Button("−", GUILayout.Width(24f))) v -= 1f;
            v = GUILayout.HorizontalSlider(v, -90f, 90f, GUILayout.Width(130f));
            if (GUILayout.Button("+", GUILayout.Width(24f))) v += 1f;
            if (GUILayout.Button("+5", GUILayout.Width(30f))) v += 5f;
            v = Mathf.Clamp(Mathf.Round(v), -90f, 90f);
            GUILayout.Label(v.ToString("0") + "°", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HuginWhispers
{
    // «Система анімацій персонажа»: our own animations for the player, played over the game's.
    //   Clip    — keys in time; each key turns some bones (degrees, in the bone's own axes) and may turn and move the
    //             whole body ("body": pitch/yaw/roll round a pivot at the chest, "move": metres). Between keys the
    //             values flow on a smooth curve (Catmull-Rom); a looped clip wraps round. Its base is the game's live
    //             animation (turns added on top) or the game's pose frozen when the clip came in (the arms and legs
    //             stop following the game). Its place in time may come from a motion value instead of the clock
    //             ("drive turn -120 120": a bank keyed left at the start, level in the middle, right at the end).
    //             Sounds at given moments (events).
    //   Layers  — up to four per player (slot 0 the base, the higher on top), each fading in and out by its weight.
    //   Values  — each game measures every player's motion itself (speed, forward, side, vertical, turn, on the
    //             ground, in water, seconds in the air, height over the ground) plus tags set by code (ZDO).
    //   Rules   — "in slot N play clip C while <condition>": each game picks every player's clips from those values
    //             itself, so a rule-driven animation costs nothing on the net. Code may also play a clip outright
    //             (PlayerAnims.Play): that one is written to the player's ZDO for the others.
    // Clips and rules: built in (below) and config/RavenWatch/player_anims (<clip>.txt, rules.txt; a file wins).
    // A clip another player plays that this game lacks (made in his test window) is asked from him and kept.
    // Tuned in the test window, tab «Персонаж» (console raven panim): for the author only, not in the README.
    internal sealed class PAnimKey
    {
        public float T;
        public Dictionary<string, Vector3> V = new Dictionary<string, Vector3>();
    }

    internal sealed class PAnimEvent
    {
        public float T;
        public string Sound;
        public float Volume = 1f;
    }

    internal sealed class PAnimClip
    {
        public string Name;
        public float Length = 1f;
        public bool Loop = true;
        public float Pivot = 1.1f;                  // metres up from the feet: what the body turns round
        public bool Frozen;                         // the base: false the game's live animation, true its pose when the clip came in
        public string Drive;                        // null: by the clock; else a motion value gives the moment
        public float DriveMin, DriveMax = 1f;       // that value from .. to = the clip's start .. end
        public readonly List<string> Bones = new List<string>();
        // how a bone's three numbers are meant (PlayerAnims.Turn, Frame, Aim); a bone not here: its own axes
        public readonly Dictionary<string, int> Modes = new Dictionary<string, int>();
        private List<string> _ordered;
        internal int ModeOf(string bone) { int m; return Modes.TryGetValue(bone, out m) ? m : PlayerAnims.Turn; }
        internal void SetMode(string bone, int mode) { if (mode == PlayerAnims.Turn) Modes.Remove(bone); else Modes[bone] = mode; }
        // the bones parents first (a child is reckoned from its turned parent)
        internal List<string> Ordered()
        {
            if (_ordered != null && _ordered.Count == Bones.Count) return _ordered;
            _ordered = new List<string>(Bones);
            _ordered.Sort((a, b) => PlayerAnims.Rank(a).CompareTo(PlayerAnims.Rank(b)));
            return _ordered;
        }
        public readonly List<PAnimKey> Keys = new List<PAnimKey>();
        public readonly List<PAnimEvent> Events = new List<PAnimEvent>();
        public bool FromFile, FromNet;

        internal void Sort() { Keys.Sort((a, b) => a.T.CompareTo(b.T)); Events.Sort((a, b) => a.T.CompareTo(b.T)); }

        // a bone this clip turns: every key gets it (zero), so the curve has it everywhere
        internal void AddBone(string bone)
        {
            if (Bones.Contains(bone)) return;
            Bones.Add(bone);
            _ordered = null;
            foreach (PAnimKey k in Keys) if (!k.V.ContainsKey(bone)) k.V[bone] = Vector3.zero;
        }

        internal void RemoveBone(string bone)
        {
            Bones.Remove(bone);
            Modes.Remove(bone);
            _ordered = null;
            foreach (PAnimKey k in Keys) k.V.Remove(bone);
        }

        // a new key at t with the values the curve has there now
        internal PAnimKey AddKey(float t)
        {
            t = Mathf.Clamp(t, 0f, Length);
            foreach (PAnimKey k in Keys) if (Mathf.Abs(k.T - t) < 0.005f) return k;
            PAnimKey n = new PAnimKey { T = t };
            foreach (string b in Bones) n.V[b] = Keys.Count > 0 ? Value(b, t) : Vector3.zero;
            Keys.Add(n);
            Sort();
            return n;
        }

        internal PAnimClip Copy(string name)
        {
            PAnimClip c = PlayerAnims.Parse(PlayerAnims.Write(this), name);
            c.Name = name;
            return c;
        }

        // the moment in the clip for a time since it started (by the clock), or for the driving value
        internal float MomentFor(float seconds, float driveValue, bool driven)
        {
            if (driven)
            {
                float u = Mathf.Abs(DriveMax - DriveMin) > 1e-5f ? Mathf.InverseLerp(DriveMin, DriveMax, driveValue) : 0f;
                return u * Length;
            }
            return Loop ? Mathf.Repeat(seconds, Mathf.Max(0.001f, Length)) : Mathf.Clamp(seconds, 0f, Length);
        }

        // a bone's value at t (seconds into the clip)
        internal Vector3 Value(string bone, float t)
        {
            int n = Keys.Count;
            if (n == 0) return Vector3.zero;
            if (n == 1) return Get(Keys[0], bone);
            bool loop = Loop && Drive == null && Length > 0.001f; // a driven clip runs from end to end, never round
            if (loop) { t %= Length; if (t < 0f) t += Length; }
            else t = Mathf.Clamp(t, 0f, Length);
            // the segment t is in: key i1 to key i2 (looped: the last to the first, across the clip's end)
            int i0, i1 = -1, i2, i3;
            float t1, t2;
            for (int k = 0; k < n; k++) if (Keys[k].T <= t) i1 = k;
            if (loop)
            {
                if (i1 < 0) { i1 = n - 1; t1 = Keys[n - 1].T - Length; i2 = 0; t2 = Keys[0].T; }
                else { t1 = Keys[i1].T; i2 = (i1 + 1) % n; t2 = i2 == 0 ? Keys[0].T + Length : Keys[i2].T; }
                i0 = (i1 - 1 + n) % n;
                i3 = (i2 + 1) % n;
            }
            else
            {
                if (i1 < 0) return Get(Keys[0], bone);
                if (i1 >= n - 1) return Get(Keys[n - 1], bone);
                i2 = i1 + 1;
                t1 = Keys[i1].T; t2 = Keys[i2].T;
                i0 = Mathf.Max(i1 - 1, 0);
                i3 = Mathf.Min(i2 + 1, n - 1);
            }
            float u = t2 - t1 > 0.0001f ? Mathf.Clamp01((t - t1) / (t2 - t1)) : 0f;
            Vector3 p0 = Get(Keys[i0], bone), p1 = Get(Keys[i1], bone), p2 = Get(Keys[i2], bone), p3 = Get(Keys[i3], bone);
            // Catmull-Rom: through every key, smooth across it
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        private static Vector3 Get(PAnimKey k, string bone) { Vector3 v; return k.V.TryGetValue(bone, out v) ? v : Vector3.zero; }
    }

    // "in slot N play clip C while <condition>"; the first rule of a slot whose condition holds wins
    internal sealed class PAnimRule
    {
        public string Name = "rule";
        public bool On = true;
        public int Slot;
        public string Clip = "";
        public float FadeIn = 0.25f, FadeOut = 0.35f, Speed = 1f;
        public string When = "";
        private string _parsedFrom;
        private List<PAnimCond> _conds;

        internal bool Holds(PlayerAnimator a)
        {
            if (_conds == null || _parsedFrom != When) { _conds = PAnimCond.Parse(When); _parsedFrom = When; }
            if (_conds == null) return false; // unreadable: never
            foreach (PAnimCond c in _conds) if (!c.Holds(a)) return false;
            return true;
        }

        internal bool Readable() { if (_conds == null || _parsedFrom != When) { _conds = PAnimCond.Parse(When); _parsedFrom = When; } return _conds != null; }

        internal bool Uses(string value) { return When != null && When.IndexOf(value, StringComparison.Ordinal) >= 0; }

        // it can only hold for a player with some tag (a gift's): the others need not be measured for it
        internal bool NeedsTag()
        {
            if (!Readable()) return true; // never holds
            foreach (PAnimCond c in _conds) if (c.IsTag) return true;
            return false;
        }
    }

    // one part of a rule's condition: "speed>4", "air>=0.3", "ground=1", "tag:odinwings", "!tag:x"; '&' between parts
    internal sealed class PAnimCond
    {
        private string _tag, _op;
        private int _index = -1; // the motion value, by its place in PlayerAnims.Values
        private float _num;
        private bool _not;

        internal static List<PAnimCond> Parse(string text)
        {
            List<PAnimCond> list = new List<PAnimCond>();
            if (string.IsNullOrEmpty(text)) return list; // no condition: always
            foreach (string raw in text.Split('&'))
            {
                string s = raw.Trim().Replace(" ", "");
                if (s.Length == 0) continue;
                PAnimCond c = new PAnimCond();
                if (s.StartsWith("!")) { c._not = true; s = s.Substring(1); }
                if (s.StartsWith("tag:")) { c._tag = s.Substring(4); if (c._tag.Length == 0) return null; list.Add(c); continue; }
                string[] ops = { ">=", "<=", "!=", ">", "<", "=" };
                bool found = false;
                foreach (string op in ops)
                {
                    int i = s.IndexOf(op, StringComparison.Ordinal);
                    if (i <= 0) continue;
                    c._index = Array.IndexOf(PlayerAnims.Values, s.Substring(0, i));
                    c._op = op;
                    if (!float.TryParse(s.Substring(i + op.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out c._num)) return null;
                    if (c._index < 0) return null;
                    found = true;
                    break;
                }
                if (!found) return null;
                list.Add(c);
            }
            return list;
        }

        internal bool IsTag { get { return _tag != null && !_not; } }

        internal bool Holds(PlayerAnimator a)
        {
            bool r;
            if (_tag != null) r = a.HasTag(_tag);
            else
            {
                float v = a.ValueAt(_index);
                switch (_op)
                {
                    case ">=": r = v >= _num; break;
                    case "<=": r = v <= _num; break;
                    case "!=": r = Mathf.Abs(v - _num) > 1e-4f; break;
                    case ">": r = v > _num; break;
                    case "<": r = v < _num; break;
                    default: r = Mathf.Abs(v - _num) <= 1e-4f; break;
                }
            }
            return _not ? !r : r;
        }
    }

    internal static class PlayerAnims
    {
        internal const string Body = "body", Move = "move"; // the whole body: turned round the pivot / moved

        // A bone's three numbers mean (per bone of a clip):
        //   Turn  — degrees round the bone's own axes, on top of the game's pose (they differ bone to bone)
        //   Frame — degrees round the body's axes: X pitch (+ forward/down), Y yaw (+ right), Z roll — the same for
        //           every bone and both sides, whatever the game's animation does with them
        //   Aim   — where the bone points, in the body: up (−90 down .. 90 up) and aside (0 ahead, 90 right, −90 left,
        //           180 back), then a twist round itself; the game's pose of it does not matter (an arm out to the side
        //           stays out to the side). Needs the next bone of the chain; a bone at a chain's end turns as Frame.
        internal const int Turn = 0, Frame = 1, Aim = 2;
        internal static readonly string[] ModeWords = { "bone", "body", "aim" };
        internal static string[] ModeNames { get { return Lang.Arr(ModeNamesSrc); } } // (translated on every read)
        private static readonly string[] ModeNamesSrc = Lang.Keys("свої осі", "повернути", "куди дивиться");

        // the bone each one points at (for Aim)
        private static readonly Dictionary<string, string> Next = new Dictionary<string, string>
        {
            { "Hips", "Spine" }, { "Spine", "Spine1" }, { "Spine1", "Spine2" }, { "Spine2", "Neck" }, { "Neck", "Head" },
            { "LeftShoulder", "LeftArm" }, { "LeftArm", "LeftForeArm" }, { "LeftForeArm", "LeftHand" },
            { "RightShoulder", "RightArm" }, { "RightArm", "RightForeArm" }, { "RightForeArm", "RightHand" },
            { "LeftUpLeg", "LeftLeg" }, { "LeftLeg", "LeftFoot" }, { "LeftFoot", "LeftToeBase" },
            { "RightUpLeg", "RightLeg" }, { "RightLeg", "RightFoot" }, { "RightFoot", "RightToeBase" },
        };
        internal static string NextOf(string bone) { string n; return Next.TryGetValue(bone, out n) ? n : null; }

        // what a bone new in a clip is meant as: the limbs by where they point, the rest round the body's axes
        internal static int DefaultMode(string bone)
        {
            if (bone == Body || bone == Move) return Turn;
            return NextOf(bone) != null && (bone.Contains("Arm") || bone.Contains("Leg") || bone.Contains("Foot")) ? Aim : Frame;
        }

        // a direction in the body from "up" and "aside" degrees
        internal static Vector3 Pointing(float up, float aside)
        {
            float e = up * Mathf.Deg2Rad, a = aside * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }

        // parents before children: the order of the groups below
        private static Dictionary<string, int> _rank;
        internal static int Rank(string bone)
        {
            if (_rank == null)
            {
                _rank = new Dictionary<string, int>();
                int i = 0;
                foreach (string[] g in Groups) foreach (string b in g) _rank[b] = i++;
            }
            int r;
            return _rank.TryGetValue(bone, out r) ? r : 1000;
        }
        internal const int Slots = 4, PreviewSlot = 3;
        internal static float MinHold = 0.6f; // a state that took over holds at least this long (seconds)
        internal static float EndFade = 0.35f; // a clip played once fades out at least this long after its end
        internal static float Smoothing = 0.12f; // seconds: our bone turns ease after what the clips want (0: none)
        internal const string NetKey = "RavenWatch_PAnim", TagKey = "RavenWatch_PAnimTags";
        private const string RpcAsk = "RavenWatch_PAnimAsk", RpcClip = "RavenWatch_PAnimClip";

        // the motion values each game measures for every player (rules, driven clips, the test window)
        internal static readonly string[] Values = { "speed", "fwd", "side", "vy", "turn", "ground", "water", "air", "height", "attack" };
        internal static string[] ValueNames { get { return Lang.Arr(ValueNamesSrc); } } // (translated on every read)
        private static readonly string[] ValueNamesSrc = Lang.Keys("швидкість по землі, м/с", "уперед (−назад), м/с", "вбік (+вправо), м/с", "вгору (−вниз), м/с", "поворот (+вправо), °/с", "на землі (1/0)", "у воді (1/0)", "у повітрі, с", "висота над землею, м", "б'є зброєю (1/0)");
        internal static bool IsValue(string v) { return Array.IndexOf(Values, v) >= 0; }

        // the player's bones by part (the names of the game's player skeleton)
        internal static string[] GroupNames { get { return Lang.Arr(GroupNamesSrc); } } // (translated on every read)
        private static readonly string[] GroupNamesSrc = Lang.Keys("Тіло цілком", "Корпус", "Голова", "Ліва рука", "Права рука", "Ліва нога", "Права нога");
        internal static readonly string[][] Groups =
        {
            new[] { Body, Move },
            new[] { "Hips", "Spine", "Spine1", "Spine2" },
            new[] { "Neck", "Head" },
            new[] { "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand" },
            new[] { "RightShoulder", "RightArm", "RightForeArm", "RightHand" },
            new[] { "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase" },
            new[] { "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase" },
        };

        internal static string Mirror(string bone)
        {
            if (bone.StartsWith("Left", StringComparison.Ordinal)) return "Right" + bone.Substring(4);
            if (bone.StartsWith("Right", StringComparison.Ordinal)) return "Left" + bone.Substring(5);
            return null;
        }

        internal static readonly Dictionary<string, PAnimClip> Clips = new Dictionary<string, PAnimClip>();
        internal static readonly List<PAnimRule> Rules = new List<PAnimRule>();
        internal static string Dir { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "player_anims"); } }
        private static string RulesFile { get { return Path.Combine(Dir, "rules.txt"); } }

        // ---------------------------------------------------------------- built in

        // the built-in clips: a first try at a glide (to be shaped in the test window and baked in here)
        // the built-in clips: «Крила Одіна» (OdinWings and the rules below), first shapes to be tuned in the test window
        private static readonly string[] Builtin =
        {
            "clip beast_wake\nlength 3\nloop 0\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 body 0 0 0\n0.35 body 6 0 0\n0.7 body -5 0 0\n2.3 body -4 0 0\n3 body 0 0 0\n0 move 0 0 0\n0.35 move 0 -0.12 0\n0.7 move 0 -0.06 0\n2.3 move 0 -0.06 0\n3 move 0 0 0\n0 Spine1 0 0 0\n0.35 Spine1 12 0 0\n0.7 Spine1 -16 0 0\n1.1 Spine1 -15 0 0\n1.5 Spine1 -17 0 0\n1.9 Spine1 -15 0 0\n2.3 Spine1 -16 0 0\n3 Spine1 0 0 0\n0 Spine2 0 0 0\n0.35 Spine2 6 0 0\n0.7 Spine2 -8 0 0\n2.3 Spine2 -8 0 0\n3 Spine2 0 0 0\n0 Neck 0 0 0\n0.35 Neck 8 0 0\n0.7 Neck -16 0 0\n2.3 Neck -15 0 0\n3 Neck 0 0 0\n0 Head 0 0 0\n0.35 Head 10 0 0\n0.7 Head -20 0 0\n1 Head -21 2 0\n1.3 Head -20 -2 0\n1.6 Head -21 1 0\n2.3 Head -19 0 0\n3 Head 0 0 0\n0 LeftShoulder 0 0 0\n0.35 LeftShoulder 0 0 4\n0.7 LeftShoulder 0 0 -10\n2.3 LeftShoulder 0 0 -10\n3 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0.35 RightShoulder 0 0 -4\n0.7 RightShoulder 0 0 10\n2.3 RightShoulder 0 0 10\n3 RightShoulder 0 0 0\n0 LeftArm 0 0 0\n0.35 LeftArm -20 0 6\n0.7 LeftArm 10 0 -50\n1 LeftArm 11 0 -52\n1.3 LeftArm 10 0 -49\n1.6 LeftArm 11 0 -52\n1.9 LeftArm 10 0 -50\n2.3 LeftArm 10 0 -51\n3 LeftArm 0 0 0\n0 RightArm 0 0 0\n0.35 RightArm -20 0 -6\n0.7 RightArm 10 0 50\n1 RightArm 11 0 52\n1.3 RightArm 10 0 49\n1.6 RightArm 11 0 52\n1.9 RightArm 10 0 50\n2.3 RightArm 10 0 51\n3 RightArm 0 0 0\n0 LeftForeArm 0 0 0\n0.35 LeftForeArm -55 0 0\n0.7 LeftForeArm -6 0 0\n2.3 LeftForeArm -6 0 0\n3 LeftForeArm 0 0 0\n0 RightForeArm 0 0 0\n0.35 RightForeArm -55 0 0\n0.7 RightForeArm -6 0 0\n2.3 RightForeArm -6 0 0\n3 RightForeArm 0 0 0\n",
            "clip beast_idle\nlength 4.2\nloop 1\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 body 0 0 0\n1.4 body 0.6 0 1.2\n2.8 body 0.2 0 -1\n4.2 body 0 0 0\n0 Spine1 10 0 0\n1.3 Spine1 13 0 0\n2.4 Spine1 10.5 0 0\n3.3 Spine1 12.5 0 0\n4.2 Spine1 10 0 0\n0 Spine2 8 0 0\n1.3 Spine2 6 0 0\n2.4 Spine2 8.5 0 0\n3.3 Spine2 6.5 0 0\n4.2 Spine2 8 0 0\n0 Neck -9 0 0\n2.1 Neck -10 3 0\n4.2 Neck -9 0 0\n0 Head -6 0 0\n1.1 Head -7 7 1\n2.1 Head -5 1 0\n3.2 Head -7 -8 -1\n4.2 Head -6 0 0\n0 LeftShoulder 0 0 8\n1.3 LeftShoulder 0 0 13\n2.4 LeftShoulder 0 0 8.5\n3.3 LeftShoulder 0 0 12\n4.2 LeftShoulder 0 0 8\n0 RightShoulder 0 0 8\n1.3 RightShoulder 0 0 12\n2.4 RightShoulder 0 0 9\n3.3 RightShoulder 0 0 13\n4.2 RightShoulder 0 0 8\n0 LeftArm -6 0 -22\n1.3 LeftArm -8 0 -27\n2.4 LeftArm -5 0 -22\n3.3 LeftArm -7 0 -25\n4.2 LeftArm -6 0 -22\n0 RightArm -6 0 22\n1.3 RightArm -8 0 25\n2.4 RightArm -5 0 23\n3.3 RightArm -7 0 27\n4.2 RightArm -6 0 22\n0 LeftForeArm -14 0 0\n2.1 LeftForeArm -18 0 -2\n4.2 LeftForeArm -14 0 0\n0 RightForeArm -14 0 0\n2.1 RightForeArm -18 0 2\n4.2 RightForeArm -14 0 0\n",
            "clip beast_walk\nlength 1.8\nloop 1\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 body 0 0 2.5\n0.9 body 0 0 -2.5\n1.8 body 0 0 2.5\n0 Spine1 12 4 0\n0.9 Spine1 12 -4 0\n1.8 Spine1 12 4 0\n0 Spine2 8 -3 0\n0.9 Spine2 8 3 0\n1.8 Spine2 8 -3 0\n0 Neck -10 -2 -2\n0.9 Neck -10 2 2\n1.8 Neck -10 -2 -2\n0 Head -6 0 0\n1.8 Head -6 0 0\n0 LeftShoulder 0 0 10\n0.9 LeftShoulder 0 0 6\n1.8 LeftShoulder 0 0 10\n0 RightShoulder 0 0 6\n0.9 RightShoulder 0 0 10\n1.8 RightShoulder 0 0 6\n0 LeftArm -4 0 -22\n0.45 LeftArm -2 0 -20\n0.9 LeftArm -4 0 -15\n1.35 LeftArm -6 0 -18\n1.8 LeftArm -4 0 -22\n0 RightArm -4 0 15\n0.45 RightArm -2 0 18\n0.9 RightArm -4 0 22\n1.35 RightArm -6 0 20\n1.8 RightArm -4 0 15\n0 LeftForeArm -16 0 0\n1.8 LeftForeArm -16 0 0\n0 RightForeArm -16 0 0\n1.8 RightForeArm -16 0 0\n",
            "clip beast_run\nlength 1\nloop 1\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 body 3 0 1.5\n0.5 body 4 0 -1.5\n1 body 3 0 1.5\n0 Spine1 15 3 0\n0.5 Spine1 16 -3 0\n1 Spine1 15 3 0\n0 Spine2 11 0 0\n1 Spine2 11 0 0\n0 Neck -15 0 0\n0.5 Neck -14 0 0\n1 Neck -15 0 0\n0 Head -10 0 0\n1 Head -10 0 0\n0 LeftShoulder 0 0 -10\n0.5 LeftShoulder 0 0 -13\n1 LeftShoulder 0 0 -10\n0 RightShoulder 0 0 10\n0.5 RightShoulder 0 0 13\n1 RightShoulder 0 0 10\n0 LeftArm 6 0 -15\n0.5 LeftArm 4 0 -17\n1 LeftArm 6 0 -15\n0 RightArm 6 0 15\n0.5 RightArm 4 0 17\n1 RightArm 6 0 15\n0 LeftForeArm -24 0 0\n1 LeftForeArm -24 0 0\n0 RightForeArm -24 0 0\n1 RightForeArm -24 0 0\n",
            "clip beast_air\nlength 2\nloop 1\npivot 1\nmode Spine1 body\nmode Neck body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 Spine1 3 0 0\n2 Spine1 3 0 0\n0 Neck -12 0 0\n2 Neck -12 0 0\n0 LeftShoulder 0 0 -14\n1 LeftShoulder 0 0 -17\n2 LeftShoulder 0 0 -14\n0 RightShoulder 0 0 14\n1 RightShoulder 0 0 17\n2 RightShoulder 0 0 14\n0 LeftArm -8 0 -38\n1 LeftArm -10 0 -42\n2 LeftArm -8 0 -38\n0 RightArm -8 0 38\n1 RightArm -10 0 42\n2 RightArm -8 0 38\n0 LeftForeArm -20 0 0\n2 LeftForeArm -20 0 0\n0 RightForeArm -20 0 0\n2 RightForeArm -20 0 0\n",
            "clip beast_attack\nlength 2\nloop 1\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode LeftArm body\nmode RightArm body\n0 Spine1 12 0 0\n2 Spine1 12 0 0\n0 Spine2 8 0 0\n2 Spine2 8 0 0\n0 Neck -9 0 0\n2 Neck -9 0 0\n0 LeftArm 0 0 -6\n2 LeftArm 0 0 -6\n0 RightArm 0 0 6\n2 RightArm 0 0 6\n",
            "clip beast_roar\nlength 2.2\nloop 0\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 body 0 0 0\n0.5 body -4 0 0\n1.6 body -4 0 0\n2.2 body 0 0 0\n0 Spine1 0 0 0\n0.5 Spine1 -14 0 0\n1.6 Spine1 -13 0 0\n2.2 Spine1 0 0 0\n0 Spine2 0 0 0\n0.5 Spine2 -6 0 0\n1.6 Spine2 -6 0 0\n2.2 Spine2 0 0 0\n0 Neck 0 0 0\n0.5 Neck -14 0 0\n1 Neck -16 0 0\n1.6 Neck -14 0 0\n2.2 Neck 0 0 0\n0 Head 0 0 0\n0.5 Head -12 0 0\n0.8 Head -14 3 0\n1.1 Head -12 -3 0\n1.6 Head -12 0 0\n2.2 Head 0 0 0\n0 LeftShoulder 0 0 0\n0.5 LeftShoulder 0 0 -10\n1.6 LeftShoulder 0 0 -10\n2.2 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0.5 RightShoulder 0 0 10\n1.6 RightShoulder 0 0 10\n2.2 RightShoulder 0 0 0\n0 LeftArm 0 0 0\n0.5 LeftArm -10 0 -24\n1.6 LeftArm -10 0 -22\n2.2 LeftArm 0 0 0\n0 RightArm 0 0 0\n0.5 RightArm -10 0 24\n1.6 RightArm -10 0 22\n2.2 RightArm 0 0 0\n0 LeftForeArm 0 0 0\n0.5 LeftForeArm -30 0 0\n1.6 LeftForeArm -28 0 0\n2.2 LeftForeArm 0 0 0\n0 RightForeArm 0 0 0\n0.5 RightForeArm -30 0 0\n1.6 RightForeArm -28 0 0\n2.2 RightForeArm 0 0 0\n",
            "clip beast_shake\nlength 1.4\nloop 0\npivot 1\nmode Neck body\nmode Head body\n0 Neck 0 0 0\n0.2 Neck 0 6 0\n0.4 Neck 0 -6 0\n0.6 Neck 0 4 0\n0.8 Neck 0 -3 0\n1.4 Neck 0 0 0\n0 Head 0 0 0\n0.2 Head 2 14 4\n0.4 Head 2 -14 -4\n0.6 Head 1 10 3\n0.8 Head 1 -7 -2\n1 Head 0 3 0\n1.4 Head 0 0 0\n",
            "clip beast_shoulders\nlength 2\nloop 0\npivot 1\nmode Spine2 body\nmode LeftShoulder body\nmode RightShoulder body\nmode Head body\n0 Spine2 0 0 0\n0.5 Spine2 0 -6 3\n1.1 Spine2 0 6 -3\n2 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0.5 LeftShoulder 4 0 -14\n0.9 LeftShoulder -2 0 -4\n2 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0.7 RightShoulder 0 0 0\n1.1 RightShoulder 4 0 14\n1.5 RightShoulder -2 0 4\n2 RightShoulder 0 0 0\n0 Head 0 0 0\n0.5 Head 0 0 5\n1.1 Head 0 0 -5\n2 Head 0 0 0\n",
            "clip beast_crack\nlength 1.8\nloop 0\npivot 1\nmode Neck body\nmode Head body\n0 Neck 0 0 0\n0.4 Neck 0 0 8\n0.7 Neck 0 0 8\n1.1 Neck 0 0 -7\n1.3 Neck 0 0 -7\n1.8 Neck 0 0 0\n0 Head 0 0 0\n0.4 Head 0 0 16\n0.7 Head 0 0 17\n1.1 Head 0 0 -15\n1.3 Head 0 0 -16\n1.8 Head 0 0 0\n",
            "clip beast_huff\nlength 1.8\nloop 0\npivot 1\nmode Spine1 body\nmode Neck body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\n0 Spine1 0 0 0\n0.7 Spine1 -7 0 0\n1.2 Spine1 5 0 0\n1.8 Spine1 0 0 0\n0 Neck 0 0 0\n0.7 Neck -5 0 0\n1.2 Neck 4 0 0\n1.8 Neck 0 0 0\n0 LeftShoulder 0 0 0\n0.7 LeftShoulder 0 0 -9\n1.2 LeftShoulder 0 0 3\n1.8 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0.7 RightShoulder 0 0 9\n1.2 RightShoulder 0 0 -3\n1.8 RightShoulder 0 0 0\n0 LeftArm 0 0 0\n0.7 LeftArm 0 0 -8\n1.2 LeftArm 0 0 3\n1.8 LeftArm 0 0 0\n0 RightArm 0 0 0\n0.7 RightArm 0 0 8\n1.2 RightArm 0 0 -3\n1.8 RightArm 0 0 0\n",
            "clip beast_flex\nlength 2\nloop 0\npivot 1\nmode Spine1 body\nmode Head body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\n0 Spine1 0 0 0\n0.5 Spine1 4 0 0\n1.4 Spine1 4 0 0\n2 Spine1 0 0 0\n0 Head 0 0 0\n0.5 Head 5 0 0\n1.4 Head 5 0 0\n2 Head 0 0 0\n0 LeftShoulder 0 0 0\n0.5 LeftShoulder 0 0 -6\n1.4 LeftShoulder 0 0 -7\n2 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0.5 RightShoulder 0 0 6\n1.4 RightShoulder 0 0 7\n2 RightShoulder 0 0 0\n0 LeftArm 0 0 0\n0.5 LeftArm -16 0 -6\n0.9 LeftArm -17 0 -7\n1.4 LeftArm -16 0 -6\n2 LeftArm 0 0 0\n0 RightArm 0 0 0\n0.5 RightArm -16 0 6\n0.9 RightArm -17 0 7\n1.4 RightArm -16 0 6\n2 RightArm 0 0 0\n0 LeftForeArm 0 0 0\n0.5 LeftForeArm -45 0 14\n0.9 LeftForeArm -48 0 15\n1.4 LeftForeArm -45 0 14\n2 LeftForeArm 0 0 0\n0 RightForeArm 0 0 0\n0.5 RightForeArm -45 0 -14\n0.9 RightForeArm -48 0 -15\n1.4 RightForeArm -45 0 -14\n2 RightForeArm 0 0 0\n",
            "clip beast_stance\nlength 3\nloop 1\npivot 1\nmode Spine1 body\nmode Spine2 body\nmode Neck body\nmode Head body\n0 Spine1 9 0 0\n1.5 Spine1 11 0 0\n0 Spine2 8 0 0\n1.5 Spine2 10 0 0\n0 Neck -8 0 0\n1.5 Neck -9 0 0\n0 Head -6 0 0\n1.5 Head -7 0 0\n",
            // the beast's crouch (slot 0, the whole gift): knees bent, the body lowered by as much, breathing a little deeper
            "clip beast_crouch\nlength 4\nloop 1\npivot 1\nmode LeftUpLeg body\nmode RightUpLeg body\nmode LeftLeg body\nmode RightLeg body\nmode LeftFoot body\nmode RightFoot body\n0 move 0 -0.075 0\n2 move 0 -0.09 0\n0 LeftUpLeg -24 0 0\n2 LeftUpLeg -27 0 0\n0 RightUpLeg -24 0 0\n2 RightUpLeg -27 0 0\n0 LeftLeg 48 0 0\n2 LeftLeg 54 0 0\n0 RightLeg 48 0 0\n2 RightLeg 54 0 0\n0 LeftFoot -24 0 0\n2 LeftFoot -27 0 0\n0 RightFoot -24 0 0\n2 RightFoot -27 0 0\n",
            "clip wings_hover\nlength 7.2\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 6 0 1.3\n0 move 0 0 0\n0 Neck -4.6 0 0\n0 Head -3.2 3.1 0\n0 LeftArm -28 -93 0\n0 RightArm -26.4 93 0\n0 LeftForeArm -25 -95 0\n0 RightForeArm -23.4 95 0\n0 LeftUpLeg -81.3 0 0\n0 RightUpLeg -77.1 0 0\n0 LeftLeg -95.2 0 0\n0 RightLeg -97.7 0 0\n0 LeftFoot -48.1 0 0\n0 RightFoot -45.3 0 0\n0.3 body 6.6 0 1.5\n0.3 move 0 0 0\n0.3 Neck -4.2 0 0\n0.3 Head -3.8 3.7 0\n0.3 LeftArm -26.7 -93 0\n0.3 RightArm -25.6 93 0\n0.3 LeftForeArm -23.7 -95 0\n0.3 RightForeArm -22.6 95 0\n0.3 LeftUpLeg -78.4 0 0\n0.3 RightUpLeg -79.5 0 0\n0.3 LeftLeg -97 0 0\n0.3 RightLeg -96.3 0 0\n0.3 LeftFoot -45 0 0\n0.3 RightFoot -47.8 0 0\n0.6 body 7 0 1.5\n0.6 move 0 0 0\n0.6 Neck -3.9 0 0\n0.6 Head -4.8 4 0\n0.6 LeftArm -25.8 -93 0\n0.6 RightArm -25.5 93 0\n0.6 LeftForeArm -22.8 -95 0\n0.6 RightForeArm -22.5 95 0\n0.6 LeftUpLeg -79.5 0 0\n0.6 RightUpLeg -83.2 0 0\n0.6 LeftLeg -96.3 0 0\n0.6 RightLeg -94.1 0 0\n0.6 LeftFoot -47 0 0\n0.6 RightFoot -51.6 0 0\n0.9 body 7.2 0 1.4\n0.9 move 0 0 0\n0.9 Neck -3.7 0 0\n0.9 Head -5.8 4 0\n0.9 LeftArm -25.5 -93 0\n0.9 RightArm -26.1 93 0\n0.9 LeftForeArm -22.5 -95 0\n0.9 RightForeArm -23.1 95 0\n0.9 LeftUpLeg -81.7 0 0\n0.9 RightUpLeg -85.8 0 0\n0.9 LeftLeg -95 0 0\n0.9 RightLeg -93 0 0\n0.9 LeftFoot -51.9 0 0\n0.9 RightFoot -54.5 0 0\n1.2 body 7 0 1.3\n1.2 move 0 0 0\n1.2 Neck -3.5 0 0\n1.2 Head -6.6 3.7 0\n1.2 LeftArm -25.8 -93 0\n1.2 RightArm -27.1 93 0\n1.2 LeftForeArm -22.8 -95 0\n1.2 RightForeArm -24.1 95 0\n1.2 LeftUpLeg -82.1 0 0\n1.2 RightUpLeg -85.5 0 0\n1.2 LeftLeg -94.8 0 0\n1.2 RightLeg -93 0 0\n1.2 LeftFoot -55 0 0\n1.2 RightFoot -54.7 0 0\n1.5 body 6.6 0 1\n1.5 move 0 0 0\n1.5 Neck -3.5 0 0\n1.5 Head -7 3.2 0\n1.5 LeftArm -26.7 -93 0\n1.5 RightArm -28.4 93 0\n1.5 LeftForeArm -23.7 -95 0\n1.5 RightForeArm -25.4 95 0\n1.5 LeftUpLeg -80.9 0 0\n1.5 RightUpLeg -82.2 0 0\n1.5 LeftLeg -95.5 0 0\n1.5 RightLeg -94.7 0 0\n1.5 LeftFoot -53 0 0\n1.5 RightFoot -52.2 0 0\n1.8 body 6 0 0.7\n1.8 move 0 0 0\n1.8 Neck -3.6 0 0\n1.8 Head -6.8 2.5 0\n1.8 LeftArm -28 -93 0\n1.8 RightArm -29.6 93 0\n1.8 LeftForeArm -25 -95 0\n1.8 RightForeArm -26.6 95 0\n1.8 LeftUpLeg -81.3 0 0\n1.8 RightUpLeg -78.1 0 0\n1.8 LeftLeg -95.2 0 0\n1.8 RightLeg -97.1 0 0\n1.8 LeftFoot -48.1 0 0\n1.8 RightFoot -48.4 0 0\n2.1 body 5.4 0 0.3\n2.1 move 0 0 0\n2.1 Neck -3.7 0 0\n2.1 Head -6.2 1.6 0\n2.1 LeftArm -29.2 -93 0\n2.1 RightArm -30.4 93 0\n2.1 LeftForeArm -26.2 -95 0\n2.1 RightForeArm -27.4 95 0\n2.1 LeftUpLeg -85.4 0 0\n2.1 RightUpLeg -75.8 0 0\n2.1 LeftLeg -93 0 0\n2.1 RightLeg -98.5 0 0\n2.1 LeftFoot -45 0 0\n2.1 RightFoot -45.5 0 0\n2.4 body 5 0 -0.1\n2.4 move 0 0 0\n2.4 Neck -4 0 0\n2.4 Head -5.2 0.6 0\n2.4 LeftArm -30.2 -93 0\n2.4 RightArm -30.5 93 0\n2.4 LeftForeArm -27.2 -95 0\n2.4 RightForeArm -27.5 95 0\n2.4 LeftUpLeg -91.7 0 0\n2.4 RightUpLeg -76.9 0 0\n2.4 LeftLeg -93 0 0\n2.4 RightLeg -97.8 0 0\n2.4 LeftFoot -47 0 0\n2.4 RightFoot -45.3 0 0\n2.7 body 4.8 0 -0.5\n2.7 move 0 0 0\n2.7 Neck -4.3 0 0\n2.7 Head -4.2 -0.5 0\n2.7 LeftArm -30.5 -93 0\n2.7 RightArm -29.9 93 0\n2.7 LeftForeArm -27.5 -95 0\n2.7 RightForeArm -26.9 95 0\n2.7 LeftUpLeg -95.7 0 0\n2.7 RightUpLeg -81.5 0 0\n2.7 LeftLeg -93 0 0\n2.7 RightLeg -95.1 0 0\n2.7 LeftFoot -51.9 0 0\n2.7 RightFoot -47.8 0 0\n3 body 5 0 -0.8\n3 move 0 0 0\n3 Neck -4.7 0 0\n3 Head -3.4 -1.5 0\n3 LeftArm -30.2 -93 0\n3 RightArm -28.9 93 0\n3 LeftForeArm -27.2 -95 0\n3 RightForeArm -25.9 95 0\n3 LeftUpLeg -94.2 0 0\n3 RightUpLeg -87.4 0 0\n3 LeftLeg -93 0 0\n3 RightLeg -93 0 0\n3 LeftFoot -55 0 0\n3 RightFoot -51.6 0 0\n3.3 body 5.4 0 -1.1\n3.3 move 0 0 0\n3.3 Neck -5.1 0 0\n3.3 Head -3 -2.4 0\n3.3 LeftArm -29.3 -93 0\n3.3 RightArm -27.6 93 0\n3.3 LeftForeArm -26.3 -95 0\n3.3 RightForeArm -24.6 95 0\n3.3 LeftUpLeg -87.9 0 0\n3.3 RightUpLeg -91.8 0 0\n3.3 LeftLeg -93 0 0\n3.3 RightLeg -93 0 0\n3.3 LeftFoot -53 0 0\n3.3 RightFoot -54.5 0 0\n3.6 body 6 0 -1.3\n3.6 move 0 0 0\n3.6 Neck -5.4 0 0\n3.6 Head -3.2 -3.1 0\n3.6 LeftArm -28 -93 0\n3.6 RightArm -26.4 93 0\n3.6 LeftForeArm -25 -95 0\n3.6 RightForeArm -23.4 95 0\n3.6 LeftUpLeg -81.3 0 0\n3.6 RightUpLeg -92.9 0 0\n3.6 LeftLeg -95.2 0 0\n3.6 RightLeg -93 0 0\n3.6 LeftFoot -48.1 0 0\n3.6 RightFoot -54.7 0 0\n3.9 body 6.6 0 -1.5\n3.9 move 0 0 0\n3.9 Neck -5.8 0 0\n3.9 Head -3.8 -3.7 0\n3.9 LeftArm -26.8 -93 0\n3.9 RightArm -25.6 93 0\n3.9 LeftForeArm -23.8 -95 0\n3.9 RightForeArm -22.6 95 0\n3.9 LeftUpLeg -78.4 0 0\n3.9 RightUpLeg -90.5 0 0\n3.9 LeftLeg -97 0 0\n3.9 RightLeg -93 0 0\n3.9 LeftFoot -45 0 0\n3.9 RightFoot -52.2 0 0\n4.2 body 7 0 -1.5\n4.2 move 0 0 0\n4.2 Neck -6.1 0 0\n4.2 Head -4.8 -4 0\n4.2 LeftArm -25.8 -93 0\n4.2 RightArm -25.5 93 0\n4.2 LeftForeArm -22.8 -95 0\n4.2 RightForeArm -22.5 95 0\n4.2 LeftUpLeg -79.5 0 0\n4.2 RightUpLeg -86.8 0 0\n4.2 LeftLeg -96.3 0 0\n4.2 RightLeg -93 0 0\n4.2 LeftFoot -47 0 0\n4.2 RightFoot -48.4 0 0\n4.5 body 7.2 0 -1.4\n4.5 move 0 0 0\n4.5 Neck -6.3 0 0\n4.5 Head -5.8 -4 0\n4.5 LeftArm -25.5 -93 0\n4.5 RightArm -26.1 93 0\n4.5 LeftForeArm -22.5 -95 0\n4.5 RightForeArm -23.1 95 0\n4.5 LeftUpLeg -81.7 0 0\n4.5 RightUpLeg -84.2 0 0\n4.5 LeftLeg -95 0 0\n4.5 RightLeg -93.5 0 0\n4.5 LeftFoot -51.9 0 0\n4.5 RightFoot -45.5 0 0\n4.8 body 7 0 -1.3\n4.8 move 0 0 0\n4.8 Neck -6.5 0 0\n4.8 Head -6.6 -3.7 0\n4.8 LeftArm -25.8 -93 0\n4.8 RightArm -27.1 93 0\n4.8 LeftForeArm -22.8 -95 0\n4.8 RightForeArm -24.1 95 0\n4.8 LeftUpLeg -82.1 0 0\n4.8 RightUpLeg -84.5 0 0\n4.8 LeftLeg -94.8 0 0\n4.8 RightLeg -93.3 0 0\n4.8 LeftFoot -55 0 0\n4.8 RightFoot -45.3 0 0\n5.1 body 6.6 0 -1\n5.1 move 0 0 0\n5.1 Neck -6.5 0 0\n5.1 Head -7 -3.2 0\n5.1 LeftArm -26.7 -93 0\n5.1 RightArm -28.4 93 0\n5.1 LeftForeArm -23.7 -95 0\n5.1 RightForeArm -25.4 95 0\n5.1 LeftUpLeg -80.9 0 0\n5.1 RightUpLeg -87.8 0 0\n5.1 LeftLeg -95.5 0 0\n5.1 RightLeg -93 0 0\n5.1 LeftFoot -53 0 0\n5.1 RightFoot -47.8 0 0\n5.4 body 6 0 -0.7\n5.4 move 0 0 0\n5.4 Neck -6.4 0 0\n5.4 Head -6.8 -2.5 0\n5.4 LeftArm -28 -93 0\n5.4 RightArm -29.6 93 0\n5.4 LeftForeArm -25 -95 0\n5.4 RightForeArm -26.6 95 0\n5.4 LeftUpLeg -81.3 0 0\n5.4 RightUpLeg -91.9 0 0\n5.4 LeftLeg -95.2 0 0\n5.4 RightLeg -93 0 0\n5.4 LeftFoot -48.1 0 0\n5.4 RightFoot -51.6 0 0\n5.7 body 5.4 0 -0.3\n5.7 move 0 0 0\n5.7 Neck -6.3 0 0\n5.7 Head -6.2 -1.6 0\n5.7 LeftArm -29.2 -93 0\n5.7 RightArm -30.4 93 0\n5.7 LeftForeArm -26.2 -95 0\n5.7 RightForeArm -27.4 95 0\n5.7 LeftUpLeg -85.4 0 0\n5.7 RightUpLeg -94.2 0 0\n5.7 LeftLeg -93 0 0\n5.7 RightLeg -93 0 0\n5.7 LeftFoot -45 0 0\n5.7 RightFoot -54.5 0 0\n6 body 5 0 0.1\n6 move 0 0 0\n6 Neck -6 0 0\n6 Head -5.2 -0.6 0\n6 LeftArm -30.2 -93 0\n6 RightArm -30.5 93 0\n6 LeftForeArm -27.2 -95 0\n6 RightForeArm -27.5 95 0\n6 LeftUpLeg -91.7 0 0\n6 RightUpLeg -93.1 0 0\n6 LeftLeg -93 0 0\n6 RightLeg -93 0 0\n6 LeftFoot -47 0 0\n6 RightFoot -54.7 0 0\n6.3 body 4.8 0 0.5\n6.3 move 0 0 0\n6.3 Neck -5.7 0 0\n6.3 Head -4.2 0.5 0\n6.3 LeftArm -30.5 -93 0\n6.3 RightArm -29.9 93 0\n6.3 LeftForeArm -27.5 -95 0\n6.3 RightForeArm -26.9 95 0\n6.3 LeftUpLeg -95.7 0 0\n6.3 RightUpLeg -88.5 0 0\n6.3 LeftLeg -93 0 0\n6.3 RightLeg -93 0 0\n6.3 LeftFoot -51.9 0 0\n6.3 RightFoot -52.2 0 0\n6.6 body 5 0 0.8\n6.6 move 0 0 0\n6.6 Neck -5.3 0 0\n6.6 Head -3.4 1.5 0\n6.6 LeftArm -30.2 -93 0\n6.6 RightArm -28.9 93 0\n6.6 LeftForeArm -27.2 -95 0\n6.6 RightForeArm -25.9 95 0\n6.6 LeftUpLeg -94.2 0 0\n6.6 RightUpLeg -82.6 0 0\n6.6 LeftLeg -93 0 0\n6.6 RightLeg -94.4 0 0\n6.6 LeftFoot -55 0 0\n6.6 RightFoot -48.4 0 0\n6.9 body 5.4 0 1.1\n6.9 move 0 0 0\n6.9 Neck -4.9 0 0\n6.9 Head -3 2.4 0\n6.9 LeftArm -29.3 -93 0\n6.9 RightArm -27.6 93 0\n6.9 LeftForeArm -26.3 -95 0\n6.9 RightForeArm -24.6 95 0\n6.9 LeftUpLeg -87.9 0 0\n6.9 RightUpLeg -78.2 0 0\n6.9 LeftLeg -93 0 0\n6.9 RightLeg -97.1 0 0\n6.9 LeftFoot -53 0 0\n6.9 RightFoot -45.5 0 0\n",
            "clip wings_fly\nlength 3.6\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 36 0 0\n1.8 body 38 0 0\n0 move 0 0 0\n1.8 move 0 0.04 0\n0 Neck -12 0 0\n0 Head -18 0 0\n1.8 Neck -12 0 0\n1.8 Head -19 0 0\n0 LeftArm 5 -98 0\n0 RightArm 5 98 0\n0 LeftForeArm 4 -98 0\n0 RightForeArm 4 98 0\n1.8 LeftArm 9 -97 0\n1.8 RightArm 9 97 0\n1.8 LeftForeArm 8 -97 0\n1.8 RightForeArm 8 97 0\n0 LeftUpLeg -68 0 0\n0 RightUpLeg -86 178 0\n0 LeftLeg -58 -178 0\n0 RightLeg -88 178 0\n0 LeftFoot -45 -178 0\n0 RightFoot -60 178 0\n1.8 LeftUpLeg -66 0 0\n1.8 RightUpLeg -86 178 0\n1.8 LeftLeg -56 -178 0\n1.8 RightLeg -88 178 0\n1.8 LeftFoot -45 -178 0\n1.8 RightFoot -60 178 0\n",
            "clip wings_climb\nlength 2.8\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 28 0 0\n1.4 body 31 0 0\n0 Neck -15 0 0\n0 Head -20 0 0\n1.4 Neck -15 0 0\n1.4 Head -22 0 0\n0 LeftArm 14 -92 0\n0 RightArm 14 92 0\n0 LeftForeArm 12 -92 0\n0 RightForeArm 12 92 0\n1.4 LeftArm 8 -93 0\n1.4 RightArm 8 93 0\n1.4 LeftForeArm 7 -93 0\n1.4 RightForeArm 7 93 0\n0 LeftUpLeg -68 0 0\n0 RightUpLeg -86 178 0\n0 LeftLeg -58 -178 0\n0 RightLeg -88 178 0\n0 LeftFoot -45 -178 0\n0 RightFoot -50 178 0\n1.4 LeftUpLeg -66 0 0\n1.4 RightUpLeg -86 178 0\n1.4 LeftLeg -56 -178 0\n1.4 RightLeg -88 178 0\n1.4 LeftFoot -45 -178 0\n1.4 RightFoot -50 178 0\n",
            "clip wings_dive\nlength 3\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 78 0 0\n1.5 body 80 0 0\n0 Neck -30 0 0\n0 Head -40 0 0\n1.5 Neck -30 0 0\n1.5 Head -40 0 0\n0 LeftArm -30 -135 0\n0 RightArm -30 135 0\n0 LeftForeArm -32 -135 0\n0 RightForeArm -32 135 0\n1.5 LeftArm -26 -132 0\n1.5 RightArm -26 132 0\n1.5 LeftForeArm -28 -132 0\n1.5 RightForeArm -28 132 0\n0 LeftUpLeg -88 -178 0\n0 RightUpLeg -88 178 0\n0 LeftLeg -88 -178 0\n0 RightLeg -88 178 0\n0 LeftFoot -60 -178 0\n0 RightFoot -60 178 0\n1.5 LeftUpLeg -88 -178 0\n1.5 RightUpLeg -88 178 0\n1.5 LeftLeg -88 -178 0\n1.5 RightLeg -88 178 0\n1.5 LeftFoot -60 -178 0\n1.5 RightFoot -60 178 0\n",
            "clip wings_bank\nlength 1\nloop 0\npivot 1.1\ndrive turn -180 180\n0 body 0 0 20\n0.5 body 0 0 0\n1 body 0 0 -20\n",
            "clip wings_takeoff\nlength 1.4\nloop 0\npivot 1.1\nevent 0.42 sfx_raven_flap 1.4\nevent 0.85 sfx_raven_flap 0.7\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n0 move 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0 LeftArm -75 -100 0\n0 RightArm -75 100 0\n0 LeftForeArm -78 -100 0\n0 RightForeArm -78 100 0\n0 LeftUpLeg -90 0 0\n0 RightUpLeg -90 0 0\n0 LeftLeg -90 0 0\n0 RightLeg -90 0 0\n0 LeftFoot -30 0 0\n0 RightFoot -30 0 0\n0.25 body 10 0 0\n0.25 move 0 -0.18 0\n0.25 Neck 8 0 0\n0.25 Head 8 0 0\n0.25 LeftArm -65 -145 0\n0.25 RightArm -65 145 0\n0.25 LeftForeArm -68 -145 0\n0.25 RightForeArm -68 145 0\n0.25 LeftUpLeg -50 0 0\n0.25 RightUpLeg -50 0 0\n0.25 LeftLeg -125 0 0\n0.25 RightLeg -125 0 0\n0.25 LeftFoot -25 0 0\n0.25 RightFoot -25 0 0\n0.42 body -3 0 0\n0.42 move 0 0.02 0\n0.42 Neck -10 0 0\n0.42 Head -12 0 0\n0.42 LeftArm 35 -85 0\n0.42 RightArm 35 85 0\n0.42 LeftForeArm 38 -85 0\n0.42 RightForeArm 38 85 0\n0.42 LeftUpLeg -92 0 0\n0.42 RightUpLeg -92 0 0\n0.42 LeftLeg -95 0 0\n0.42 RightLeg -95 0 0\n0.42 LeftFoot -75 0 0\n0.42 RightFoot -75 0 0\n0.8 body 3 0 0\n0.8 move 0 0 0\n0.8 Neck -6 0 0\n0.8 Head -6 0 0\n0.8 LeftArm 5 -93 0\n0.8 RightArm 5 93 0\n0.8 LeftForeArm 6 -95 0\n0.8 RightForeArm 6 95 0\n0.8 LeftUpLeg -78 0 0\n0.8 RightUpLeg -78 0 0\n0.8 LeftLeg -108 0 0\n0.8 RightLeg -108 0 0\n0.8 LeftFoot -55 0 0\n0.8 RightFoot -55 0 0\n1.4 body 6 0 0\n1.4 move 0 0 0\n1.4 Neck -4.6 0 0\n1.4 Head -3.2 0 0\n1.4 LeftArm -26 -93 0\n1.4 RightArm -26 93 0\n1.4 LeftForeArm -24 -95 0\n1.4 RightForeArm -24 95 0\n1.4 LeftUpLeg -81 0 0\n1.4 RightUpLeg -81 0 0\n1.4 LeftLeg -96 0 0\n1.4 RightLeg -96 0 0\n1.4 LeftFoot -48 0 0\n1.4 RightFoot -48 0 0\n",
            "clip wings_sink\nlength 3.4\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 10 0 0\n1.7 body 8 0 0\n0 move 0 0 0\n1.7 move 0 0.04 0\n0 Neck 8 0 0\n0 Head 10 0 0\n1.7 Neck 8 0 0\n1.7 Head 10 0 0\n0 LeftArm -16 -94 0\n0 RightArm -16 94 0\n0 LeftForeArm -16 -94 0\n0 RightForeArm -16 94 0\n1.7 LeftArm -21 -95 0\n1.7 RightArm -21 95 0\n1.7 LeftForeArm -21 -95 0\n1.7 RightForeArm -21 95 0\n0 LeftUpLeg -86 -180 0\n0 RightUpLeg -86 180 0\n0 LeftLeg -88 -180 0\n0 RightLeg -88 180 0\n0 LeftFoot -40 -180 0\n0 RightFoot -40 180 0\n1.7 LeftUpLeg -86 -180 0\n1.7 RightUpLeg -86 180 0\n1.7 LeftLeg -88 -180 0\n1.7 RightLeg -88 180 0\n1.7 LeftFoot -40 -180 0\n1.7 RightFoot -40 180 0\n",
            "clip wings_down\nlength 3\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 40 0 0\n1.5 body 43 0 0\n0 Neck -15 0 0\n0 Head -22 0 0\n1.5 Neck -15 0 0\n1.5 Head -23 0 0\n0 LeftArm -4 -108 0\n0 RightArm -4 108 0\n0 LeftForeArm -5 -108 0\n0 RightForeArm -5 108 0\n1.5 LeftArm 0 -106 0\n1.5 RightArm 0 106 0\n1.5 LeftForeArm -1 -106 0\n1.5 RightForeArm -1 106 0\n0 LeftUpLeg -68 0 0\n0 RightUpLeg -86 178 0\n0 LeftLeg -58 -178 0\n0 RightLeg -88 178 0\n0 LeftFoot -45 -178 0\n0 RightFoot -55 178 0\n1.5 LeftUpLeg -66 0 0\n1.5 RightUpLeg -86 178 0\n1.5 LeftLeg -56 -178 0\n1.5 RightLeg -88 178 0\n1.5 LeftFoot -45 -178 0\n1.5 RightFoot -55 178 0\n",
            "clip wings_land\nlength 0.9\nloop 0\npivot 1.1\nevent 0.05 sfx_raven_flap 0.6\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body -6 0 0\n0 move 0 0 0\n0 Neck -5 0 0\n0 Head -5 0 0\n0 LeftArm 12 -105 0\n0 RightArm 12 105 0\n0 LeftForeArm 14 -105 0\n0 RightForeArm 14 105 0\n0 LeftUpLeg -78 0 0\n0 RightUpLeg -78 0 0\n0 LeftLeg -102 0 0\n0 RightLeg -102 0 0\n0 LeftFoot -20 0 0\n0 RightFoot -20 0 0\n0.22 body 8 0 0\n0.22 move 0 -0.2 0\n0.22 Neck 6 0 0\n0.22 Head 6 0 0\n0.22 LeftArm -10 -100 0\n0.22 RightArm -10 100 0\n0.22 LeftForeArm -12 -100 0\n0.22 RightForeArm -12 100 0\n0.22 LeftUpLeg -52 0 0\n0.22 RightUpLeg -52 0 0\n0.22 LeftLeg -124 0 0\n0.22 RightLeg -124 0 0\n0.22 LeftFoot -24 0 0\n0.22 RightFoot -24 0 0\n0.5 body 4 0 0\n0.5 move 0 -0.08 0\n0.5 Neck 2 0 0\n0.5 Head 2 0 0\n0.5 LeftArm -45 -95 0\n0.5 RightArm -45 95 0\n0.5 LeftForeArm -48 -95 0\n0.5 RightForeArm -48 95 0\n0.5 LeftUpLeg -76 0 0\n0.5 RightUpLeg -76 0 0\n0.5 LeftLeg -104 0 0\n0.5 RightLeg -104 0 0\n0.5 LeftFoot -28 0 0\n0.5 RightFoot -28 0 0\n0.9 body 0 0 0\n0.9 move 0 0 0\n0.9 Neck 0 0 0\n0.9 Head 0 0 0\n0.9 LeftArm -75 -100 0\n0.9 RightArm -75 100 0\n0.9 LeftForeArm -78 -100 0\n0.9 RightForeArm -78 100 0\n0.9 LeftUpLeg -90 0 0\n0.9 RightUpLeg -90 0 0\n0.9 LeftLeg -90 0 0\n0.9 RightLeg -90 0 0\n0.9 LeftFoot -30 0 0\n0.9 RightFoot -30 0 0\n",
            "clip wings_flare\nlength 2.4\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body -6 0 0\n0 move 0 0 0\n0 Neck -4 0 0\n0 Head -4 0 0\n0 LeftArm 12 -105 0\n0 RightArm 12 105 0\n0 LeftForeArm 14 -105 0\n0 RightForeArm 14 105 0\n0 LeftUpLeg -76 0 0\n0 RightUpLeg -80 0 0\n0 LeftLeg -104 0 0\n0 RightLeg -100 0 0\n0 LeftFoot -22 0 0\n0 RightFoot -22 0 0\n1.2 body -8 0 0\n1.2 move 0 0.03 0\n1.2 Neck -5 0 0\n1.2 Head -5 0 0\n1.2 LeftArm 16 -108 0\n1.2 RightArm 16 108 0\n1.2 LeftForeArm 18 -108 0\n1.2 RightForeArm 18 108 0\n1.2 LeftUpLeg -78 0 0\n1.2 RightUpLeg -76 0 0\n1.2 LeftLeg -102 0 0\n1.2 RightLeg -106 0 0\n1.2 LeftFoot -20 0 0\n1.2 RightFoot -20 0 0\n",
            "clip wings_beat\nlength 1.6\nloop 0\npivot 1.1\nevent 0.5 sfx_raven_flap 0.5\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\n0 body 0 0 0\n0.6 body -4 0 0\n1.6 body 0 0 0\n0 move 0 0 0\n0.6 move 0 0.08 0\n1.6 move 0 0 0\n0 LeftArm 6 -97 0\n0 RightArm 6 97 0\n0 LeftForeArm 5 -97 0\n0 RightForeArm 5 97 0\n0.4 LeftArm 20 -94 0\n0.4 RightArm 20 94 0\n0.4 LeftForeArm 18 -94 0\n0.4 RightForeArm 18 94 0\n0.8 LeftArm -2 -99 0\n0.8 RightArm -2 99 0\n0.8 LeftForeArm -2 -99 0\n0.8 RightForeArm -2 99 0\n1.6 LeftArm 6 -97 0\n1.6 RightArm 6 97 0\n1.6 LeftForeArm 5 -97 0\n1.6 RightForeArm 5 97 0\n",
            "clip wings_dip\nlength 1.8\nloop 0\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\n0 body 0 0 0\n0.6 body -5 0 0\n1.8 body 0 0 0\n0 move 0 0 0\n0.6 move 0 -0.25 0\n1.2 move 0 0.04 0\n1.8 move 0 0 0\n0 LeftArm 6 -97 0\n0 RightArm 6 97 0\n0 LeftForeArm 5 -97 0\n0 RightForeArm 5 97 0\n0.6 LeftArm 18 -92 0\n0.6 RightArm 18 92 0\n0.6 LeftForeArm 18 -92 0\n0.6 RightForeArm 18 92 0\n1.8 LeftArm 6 -97 0\n1.8 RightArm 6 97 0\n1.8 LeftForeArm 5 -97 0\n1.8 RightForeArm 5 97 0\n",
            "clip wings_stretch\nlength 2.4\nloop 0\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n1.0 body -3 0 0\n2.4 body 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n1 Neck -6 0 0\n1 Head -8 0 0\n2.4 Neck 0 0 0\n2.4 Head 0 0 0\n0 LeftArm 6 -97 0\n0 RightArm 6 97 0\n0 LeftForeArm 5 -97 0\n0 RightForeArm 5 97 0\n1 LeftArm 12 -88 0\n1 RightArm 12 88 0\n1 LeftForeArm 14 -88 0\n1 RightForeArm 14 88 0\n1.6 LeftArm 12 -88 0\n1.6 RightArm 12 88 0\n1.6 LeftForeArm 14 -88 0\n1.6 RightForeArm 14 88 0\n2.4 LeftArm 6 -97 0\n2.4 RightArm 6 97 0\n2.4 LeftForeArm 5 -97 0\n2.4 RightForeArm 5 97 0\n",
        };

        // the built-in rules: the states of «Крила Одіна» (the jolts, the take-off and the landing are played by OdinWings on slot 2)
        private static readonly string BuiltinRules =
            "rule wings_flare slot 0 clip wings_flare in 0.35 out 0.4 speed 1 when tag:odinwings_land\n" +
            "rule wings_sink slot 0 clip wings_sink in 0.7 out 0.7 speed 1 when tag:odinwings_sink\n" +
            "rule wings_climb slot 0 clip wings_climb in 0.7 out 0.7 speed 1 when tag:odinwings & vy>2.5\n" +
            "rule wings_dive slot 0 clip wings_dive in 0.7 out 0.7 speed 1 when tag:odinwings & vy<-3 & speed>8\n" +
            "rule wings_down slot 0 clip wings_down in 0.7 out 0.7 speed 1 when tag:odinwings & vy<-1.5 & speed>3\n" +
            "rule wings_drop slot 0 clip wings_sink in 0.7 out 0.7 speed 1 when tag:odinwings & vy<-1.5\n" +
            "rule wings_fly slot 0 clip wings_fly in 0.7 out 0.7 speed 1 when tag:odinwings & speed>3\n" +
            "rule wings_hover slot 0 clip wings_hover in 0.7 out 0.7 speed 1 when tag:odinwings\n" +
            "rule beast_crouch slot 0 clip beast_crouch in 0.9 out 0.9 speed 1 when tag:beast\n" +
            "rule beast_attack slot 1 clip beast_attack in 0.2 out 0.4 speed 1 when tag:beast & attack>0.5\n" +
            "rule beast_air slot 1 clip beast_air in 0.4 out 0.5 speed 1 when tag:beast & ground<0.5 & water<0.5 & air>0.35\n" +
            "rule beast_run slot 1 clip beast_run in 0.6 out 0.6 speed 1 when tag:beast & speed>5.5\n" +
            "rule beast_walk slot 1 clip beast_walk in 0.7 out 0.7 speed 1 when tag:beast & speed>1\n" +
            "rule beast_idle slot 1 clip beast_idle in 1.2 out 1.2 speed 1 when tag:beast\n" +
            "rule wings_bank slot 1 clip wings_bank in 0.7 out 0.7 speed 1 when tag:odinwings & speed>3 & !tag:odinwings_sink & !tag:odinwings_land\n";

        internal static void Load()
        {
            Clips.Clear();
            foreach (string text in Builtin) { PAnimClip c = Parse(text, null); if (c != null) Clips[c.Name] = c; }
            Rules.Clear();
            Rules.AddRange(ParseRules(BuiltinRules));
            try
            {
                if (!Directory.Exists(Dir)) return;
                foreach (string path in Directory.GetFiles(Dir, "*.txt"))
                {
                    if (Path.GetFileName(path) == "rules.txt") continue;
                    PAnimClip c = Parse(File.ReadAllText(path, Encoding.UTF8), Path.GetFileNameWithoutExtension(path));
                    if (c == null) continue;
                    c.FromFile = true;
                    Clips[c.Name] = c;
                }
                if (File.Exists(RulesFile)) { Rules.Clear(); Rules.AddRange(ParseRules(File.ReadAllText(RulesFile, Encoding.UTF8))); }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Player animations: reading failed: " + e.Message); }
        }

        internal static PAnimClip Find(string name) { PAnimClip c; return name != null && Clips.TryGetValue(name, out c) ? c : null; }

        // ---------------------------------------------------------------- text

        // clip <name> / length s / loop 0|1 / pivot m / base game|frozen / drive <value> <from> <to> /
        // event <time> <sound> [volume] / then "<time> <bone> x y z" lines; # comments. Kept within sane limits
        // (a clip may come from another player): 60 s, 40 bones, 200 keys, 32 sounds, turns ±360°, moves ±5 m.
        internal const int MaxBones = 40, MaxKeys = 200, MaxSounds = 32;
        internal static PAnimClip Parse(string text, string name)
        {
            PAnimClip c = new PAnimClip { Name = name };
            Dictionary<float, PAnimKey> keys = new Dictionary<float, PAnimKey>();
            foreach (string raw in text.Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                string[] w = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                float f, g;
                switch (w[0])
                {
                    case "clip": if (w.Length > 1 && c.Name == null) c.Name = w[1]; continue;
                    case "length": if (w.Length > 1 && Num(w[1], out f)) c.Length = Mathf.Clamp(f, 0.05f, 60f); continue;
                    case "loop": if (w.Length > 1) c.Loop = w[1] != "0"; continue;
                    case "pivot": if (w.Length > 1 && Num(w[1], out f)) c.Pivot = Mathf.Clamp(f, -1f, 3f); continue;
                    case "base": if (w.Length > 1) c.Frozen = w[1] == "frozen"; continue;
                    case "mode": if (w.Length > 2) { int m = Array.IndexOf(ModeWords, w[2]); if (m > 0) c.Modes[w[1]] = m; } continue;
                    case "drive":
                        if (w.Length > 3 && IsValue(w[1]) && Num(w[2], out f) && Num(w[3], out g)) { c.Drive = w[1]; c.DriveMin = f; c.DriveMax = g; }
                        continue;
                    case "event":
                        if (w.Length > 2 && Num(w[1], out f) && c.Events.Count < MaxSounds) c.Events.Add(new PAnimEvent { T = Mathf.Max(0f, f), Sound = w[2], Volume = Mathf.Clamp(w.Length > 3 && Num(w[3], out g) ? g : 1f, 0f, 3f) });
                        continue;
                }
                float t, x, y, z;
                if (w.Length >= 5 && Num(w[0], out t) && Num(w[2], out x) && Num(w[3], out y) && Num(w[4], out z))
                {
                    t = Mathf.Round(t * 1000f) / 1000f;
                    if (!c.Bones.Contains(w[1]) && c.Bones.Count >= MaxBones) continue;
                    PAnimKey k;
                    if (!keys.TryGetValue(t, out k)) { if (c.Keys.Count >= MaxKeys) continue; k = new PAnimKey { T = Mathf.Max(0f, t) }; keys[t] = k; c.Keys.Add(k); }
                    float lim = w[1] == Move ? 5f : 360f;
                    k.V[w[1]] = new Vector3(Mathf.Clamp(x, -lim, lim), Mathf.Clamp(y, -lim, lim), Mathf.Clamp(z, -lim, lim));
                    if (!c.Bones.Contains(w[1])) c.Bones.Add(w[1]);
                }
            }
            if (string.IsNullOrEmpty(c.Name)) return null;
            foreach (PAnimKey k in c.Keys) foreach (string b in c.Bones) if (!k.V.ContainsKey(b)) k.V[b] = Vector3.zero;
            c.Sort();
            return c;
        }

        internal static string Write(PAnimClip c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Lang.T("# RavenWatch: анімація персонажа. Рядки: час кістка x y z (градуси поверх анімації гри; move — метри)\n"));
            sb.Append("clip ").Append(c.Name).Append('\n');
            sb.Append("length ").Append(N(c.Length)).Append('\n');
            sb.Append("loop ").Append(c.Loop ? "1" : "0").Append('\n');
            sb.Append("pivot ").Append(N(c.Pivot)).Append('\n');
            if (c.Frozen) sb.Append("base frozen\n");
            foreach (KeyValuePair<string, int> m in c.Modes) sb.Append("mode ").Append(m.Key).Append(' ').Append(ModeWords[m.Value]).Append('\n');
            if (c.Drive != null) sb.Append("drive ").Append(c.Drive).Append(' ').Append(N(c.DriveMin)).Append(' ').Append(N(c.DriveMax)).Append('\n');
            foreach (PAnimEvent e in c.Events) sb.Append("event ").Append(N(e.T)).Append(' ').Append(e.Sound).Append(' ').Append(N(e.Volume)).Append('\n');
            foreach (PAnimKey k in c.Keys)
                foreach (string b in c.Bones)
                {
                    Vector3 v = k.V.ContainsKey(b) ? k.V[b] : Vector3.zero;
                    sb.Append(N(k.T)).Append(' ').Append(b).Append(' ').Append(N(v.x)).Append(' ').Append(N(v.y)).Append(' ').Append(N(v.z)).Append('\n');
                }
            return sb.ToString();
        }

        // the clip as a line of C# for Builtin above (the test window copies it for baking)
        internal static string AsCode(PAnimClip c)
        {
            string text = Write(c);
            StringBuilder sb = new StringBuilder("            \"");
            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0 || line.StartsWith("#")) continue;
                sb.Append(line.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\\n");
            }
            return sb.Append("\",").ToString();
        }

        // rule <name> slot <n> clip <c> in <s> out <s> speed <x> [off] when <condition>
        internal static List<PAnimRule> ParseRules(string text)
        {
            List<PAnimRule> list = new List<PAnimRule>();
            foreach (string raw in text.Split('\n'))
            {
                string l = raw.Trim();
                if (!l.StartsWith("rule ")) continue;
                PAnimRule r = new PAnimRule();
                int when = l.IndexOf(" when ", StringComparison.Ordinal);
                if (when >= 0) { r.When = l.Substring(when + 6).Trim(); l = l.Substring(0, when); }
                string[] w = l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (w.Length > 1) r.Name = w[1];
                for (int i = 2; i < w.Length; i++)
                {
                    float f;
                    switch (w[i])
                    {
                        case "slot": if (i + 1 < w.Length) { int s; if (int.TryParse(w[++i], out s)) r.Slot = Mathf.Clamp(s, 0, Slots - 2); } break;
                        case "clip": if (i + 1 < w.Length) r.Clip = w[++i]; break;
                        case "in": if (i + 1 < w.Length && Num(w[++i], out f)) r.FadeIn = f; break;
                        case "out": if (i + 1 < w.Length && Num(w[++i], out f)) r.FadeOut = f; break;
                        case "speed": if (i + 1 < w.Length && Num(w[++i], out f)) r.Speed = f; break;
                        case "off": r.On = false; break;
                    }
                }
                list.Add(r);
            }
            return list;
        }

        internal static string WriteRules()
        {
            StringBuilder sb = new StringBuilder(Lang.T("# RavenWatch: стани анімацій персонажа. Перше правило шару, умова якого справджується, грає свій кліп.\n"));
            sb.Append(Lang.T("# Умови: ")).Append(string.Join(", ", Values)).Append(Lang.T(" з > < >= <= = !=, а також tag:назва і !tag:назва; між ними &\n"));
            foreach (PAnimRule r in Rules)
                sb.Append("rule ").Append(r.Name).Append(" slot ").Append(r.Slot).Append(" clip ").Append(r.Clip).Append(" in ").Append(N(r.FadeIn))
                  .Append(" out ").Append(N(r.FadeOut)).Append(" speed ").Append(N(r.Speed)).Append(r.On ? "" : " off").Append(" when ").Append(r.When).Append('\n');
            return sb.ToString();
        }

        internal static string SaveRules()
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllText(RulesFile, WriteRules(), new UTF8Encoding(false)); return RulesFile; }
            catch (Exception e) { return Lang.F("не вдалося: {0}", e.Message); }
        }

        internal static string Save(PAnimClip c)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, c.Name + ".txt");
                File.WriteAllText(path, Write(c), new UTF8Encoding(false));
                c.FromFile = true;
                c.FromNet = false;
                return path;
            }
            catch (Exception e) { return Lang.F("не вдалося: {0}", e.Message); }
        }

        internal static void Delete(PAnimClip c)
        {
            try { string path = Path.Combine(Dir, c.Name + ".txt"); if (File.Exists(path)) File.Delete(path); } catch { }
            Clips.Remove(c.Name);
        }

        // the mod's own (built-in) text of a clip, or null
        private static string BuiltinText(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string head = "clip " + name + "\n";
            foreach (string text in Builtin) if (text.StartsWith(head, StringComparison.Ordinal)) return text;
            return null;
        }

        internal static bool HasBuiltin(string name) { return BuiltinText(name) != null; }

        // back to the built-in clip: the saved file is moved to player_anims_backup (with the time), the clip reloaded
        internal static string Revert(string name)
        {
            string text = BuiltinText(name);
            if (text == null) return Lang.T("цей кліп не вбудований — нема до чого повертати");
            string kept = "";
            try
            {
                string path = Path.Combine(Dir, name + ".txt");
                if (File.Exists(path))
                {
                    string backup = Path.Combine(Path.GetDirectoryName(Dir), "player_anims_backup");
                    Directory.CreateDirectory(backup);
                    string to = Path.Combine(backup, name + "_" + DateTime.Now.ToString("MMdd_HHmmss") + ".txt");
                    File.Copy(path, to, true);
                    File.Delete(path);
                    kept = Lang.F(" (твоя версія — у {0})", to);
                }
            }
            catch (Exception e) { return Lang.F("не вдалося: {0}", e.Message); }
            PAnimClip c = Parse(text, null);
            if (c != null) Clips[c.Name] = c;
            return Lang.F("«{0}» знову як у моді{1}", name, kept);
        }

        private static bool Num(string s, out float f) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f); }
        internal static string N(float f) { return (Mathf.Round(f * 1000f) / 1000f).ToString("0.###", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- playing

        internal static double Now { get { return WorldTime.Now; } }

        // on the local player: plays at once and tells the others (ZDO)
        internal static void Play(Player p, int slot, string clip, float fade, float speed)
        {
            PlayerAnimator a = PlayerAnimator.For(p);
            if (a == null || Find(clip) == null) return;
            a.Play(slot, clip, Now, speed, fade, PlayerAnimator.Source.Code);
            if (p == Player.m_localPlayer) PAnimLog.Event(Lang.F("код: шар {0} грає «{1}»", slot, clip));
            Publish(p, a);
        }

        internal static void Stop(Player p, int slot, float fade)
        {
            PlayerAnimator a = PlayerAnimator.Of(p);
            if (a == null) return;
            a.Stop(slot, fade, PlayerAnimator.Source.Code);
            Publish(p, a);
        }

        private static void Publish(Player p, PlayerAnimator a)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo != null) zdo.Set(NetKey, a.Code());
        }

        // a tag on the local player for the rules ("odinwings": in flight on Odin's wings); everybody's rules see it
        internal static void Tag(Player p, string tag, bool on)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null || string.IsNullOrEmpty(tag)) return;
            List<string> tags = new List<string>(zdo.GetString(TagKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (on == tags.Contains(tag)) return;
            if (on) tags.Add(tag); else tags.Remove(tag);
            zdo.Set(TagKey, string.Join(",", tags.ToArray()));
        }

        // ---------------------------------------------------------------- the others

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<string>(RpcAsk, new Action<long, string>(OnAsk));
            ZRoutedRpc.instance.Register<string, string>(RpcClip, new Action<long, string, string>(OnClip));
        }

        private static readonly HashSet<string> Asked = new HashSet<string>();

        // a clip someone plays that this game lacks: asked from that player once
        internal static void Ask(Player owner, string clip)
        {
            if (owner == null || ZRoutedRpc.instance == null || string.IsNullOrEmpty(clip) || !Asked.Add(clip)) return;
            ZNetView nv = owner.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(nv.GetZDO().GetOwner(), RpcAsk, clip);
        }

        private static void OnAsk(long sender, string clip)
        {
            PAnimClip c = Find(clip);
            if (c == null || ZRoutedRpc.instance == null) return;
            string text = Write(c);
            if (text.Length > 60000) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcClip, clip, text);
        }

        private static void OnClip(long sender, string clip, string text)
        {
            if (!Asked.Contains(clip) || text == null || text.Length > 60000) return; // only what we asked for
            if (Find(clip) != null) return; // ours wins
            PAnimClip c = Parse(text, clip);
            if (c == null) return;
            c.Name = clip;
            c.FromNet = true;
            Clips[clip] = c;
        }

        // from the skills' Update: every player gets his animator; the others' code-played layers come from their ZDO
        private static float _next;
        private static readonly Dictionary<Player, string> Seen = new Dictionary<Player, string>();

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.2f;
            CheckNeeds();
            Player me = Player.m_localPlayer;
            bool rules = Rules.Count > 0;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null) continue;
                if (rules) PlayerAnimator.For(pl); // the rules run on everyone
                if (pl == me) continue;
                ZDO zdo = NetFlag.Zdo(pl, false);
                string code = zdo != null ? zdo.GetString(NetKey, "") : "";
                string was;
                if (Seen.TryGetValue(pl, out was) && was == code) continue;
                Seen[pl] = code;
                if (code.Length == 0 && PlayerAnimator.Of(pl) == null) continue;
                PlayerAnimator a = PlayerAnimator.For(pl);
                if (a != null) a.Take(code);
            }
            if (Seen.Count > 64) { List<Player> gone = new List<Player>(); foreach (Player k in Seen.Keys) if (k == null) gone.Add(k); foreach (Player k in gone) Seen.Remove(k); }
        }

        internal static void Reset() { Seen.Clear(); Asked.Clear(); }

        // something wants the height over the ground (a rule's condition or a clip driven by it): only then the rays
        internal static bool NeedsHeight;
        // every rule needs a tag (all of them a gift's): a player without tags is left alone
        internal static bool AllNeedTag;
        private static void CheckNeeds()
        {
            bool all = true;
            foreach (PAnimRule r in Rules) if (r.On && !r.NeedsTag()) { all = false; break; }
            AllNeedTag = all;
            bool h = false;
            foreach (PAnimRule r in Rules) if (r.On && r.Uses("height")) { h = true; break; }
            if (!h) foreach (PAnimClip c in Clips.Values) if (c.Drive == "height") { h = true; break; }
            NeedsHeight = h;
        }

        // a clip's sound: the game's effect, louder or quieter than its own
        internal static void Sound(string name, Vector3 at, float volume)
        {
            GameObject made = RavenFx.PlayStretched(name, at, 1f, Mathf.Max(1f, volume));
            if (made == null || volume >= 0.999f) return;
            foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>(true))
            {
                ZSFX z = a.GetComponent<ZSFX>();
                if (z != null) z.SetVolumeModifier(volume); else a.volume *= volume;
            }
        }
    }

    // One player's layers, applied after the game's animation every frame (early in LateUpdate: whatever sits on the
    // bones, the shoulder ravens among them, follows the turned bones). With nothing to play and no rules it does
    // nothing at all; a player far from the camera is left to the game unless code plays something on him.
    [DefaultExecutionOrder(-1000)]
    internal class PlayerAnimator : MonoBehaviour
    {
        internal enum Source { Code, Rule }

        // one clip on a layer: the one playing, or the one it is crossfading from
        private sealed class Run
        {
            public string Clip;
            public PAnimClip C;      // looked up once a frame
            public double Start;
            public float Speed = 1f, Weight;
            public float LastMoment = -1f; // for the sounds: the moment of the frame before
            public Dictionary<string, Quaternion> FrozenPose = new Dictionary<string, Quaternion>();
            public bool NeedFreeze;
            public float Moment; // this frame's moment in the clip

            public Dictionary<string, Quaternion> Aims = new Dictionary<string, Quaternion>(); // an aimed bone as it stood last frame, in the body's frame
            public void Clear() { Clip = null; C = null; Weight = 0f; LastMoment = -1f; FrozenPose.Clear(); Aims.Clear(); NeedFreeze = false; }

            // the other one's state into this one (the dictionaries swapped, nothing copied)
            public void From(Run o)
            {
                Clip = o.Clip; C = o.C; Start = o.Start; Speed = o.Speed; Weight = o.Weight; LastMoment = o.LastMoment; NeedFreeze = o.NeedFreeze;
                Dictionary<string, Quaternion> d = FrozenPose; FrozenPose = o.FrozenPose; o.FrozenPose = d;
                d = Aims; Aims = o.Aims; o.Aims = d;
            }
        }

        private sealed class Layer
        {
            public readonly Run Now = new Run(), Was = new Run(); // Was fades out while Now fades in
            public float Target, FadeSeconds = 0.25f;
            public float Hold = -1f; // the test window holds one moment of the clip (seconds in it); −1: plays
            public Source From;
            public PAnimRule Rule;
            public float RuleSince; // when the rule took the layer
            public string Clip { get { return Now.Clip; } }
            public bool Busy { get { return Now.Clip != null && (Target > 0f || Now.Weight > 0.001f); } }
        }

        // a bone turned over the game's animation; when the game did not set it this frame, from its last pose
        private sealed class Bone
        {
            public Transform T;
            public Quaternion Base, Written;
            public bool Wrote;
            public Quaternion Offset = Quaternion.identity; // our turn over the game's pose as shown (eased after what the clips want)
            public bool HasOffset;
            public Quaternion PrevBase;                     // the game's pose the frame before (the log: how much the game itself moved it)
            public bool HadBase;
            public float GameMove;
            // back to the game's pose of this frame (or our last one taken off, when the game did not set it)
            public void Begin()
            {
                Quaternion l = T.localRotation;
                if (Wrote && Quaternion.Angle(l, Written) < 0.01f) l = Base;
                Base = l;
                T.localRotation = l;
                GameMove = HadBase ? Quaternion.Angle(PrevBase, l) : 0f;
                PrevBase = l; HadBase = true;
            }
            // our turn eased: a key that jumps, a quick switch of clips, a jolt from a fade — all come in over a moment
            public void Ease(float k)
            {
                Quaternion want = Quaternion.Inverse(Base) * T.localRotation;
                Offset = HasOffset ? Quaternion.Slerp(Offset, want, k) : want;
                HasOffset = true;
                T.localRotation = Base * Offset;
            }
            public void Commit() { Written = T.localRotation; Wrote = true; }
            public void Release() { if (Wrote && T != null && Quaternion.Angle(T.localRotation, Written) < 0.01f) T.localRotation = Base; Wrote = false; HasOffset = false; Offset = Quaternion.identity; HadBase = false; }
        }

        internal static float FarFromCamera = 80f; // further than this, the rules leave a player to the game

        private static readonly Dictionary<Player, PlayerAnimator> All = new Dictionary<Player, PlayerAnimator>();
        private readonly Run _scratch = new Run(); // (for swapping two runs)
        private readonly Layer[] _layers = new Layer[PlayerAnims.Slots];
        private readonly Dictionary<string, Bone> _bones = new Dictionary<string, Bone>();
        private readonly HashSet<string> _missing = new HashSet<string>();
        private Player _p;
        private Transform _visual;
        private Animator _anim;
        private Vector3 _visualPos;
        private Quaternion _visualRot;
        private bool _visualMoved, _resting = true;

        // the motion values, measured here for this player (any player)
        private readonly float[] _values = new float[PlayerAnims.Values.Length];
        private Vector3 _lastPos;
        private float _lastYaw, _airSince = -1f, _nextHeight, _nextTags;
        private bool _measured;
        private string _tagsRaw = "";
        private readonly HashSet<string> _tags = new HashSet<string>();

        internal static PlayerAnimator Of(Player p) { PlayerAnimator a; return p != null && All.TryGetValue(p, out a) && a != null ? a : null; }

        internal static PlayerAnimator For(Player p)
        {
            if (p == null) return null;
            PlayerAnimator a = Of(p);
            if (a != null) return a;
            a = p.gameObject.GetComponent<PlayerAnimator>();
            if (a == null) a = p.gameObject.AddComponent<PlayerAnimator>();
            a._p = p;
            All[p] = a;
            return a;
        }

        private void Awake() { for (int i = 0; i < _layers.Length; i++) _layers[i] = new Layer(); }

        private void OnDestroy()
        {
            Rest();
            if (_p != null) All.Remove(_p);
        }

        internal void Play(int slot, string clip, double start, float speed, float fade, Source from)
        {
            if (slot < 0 || slot >= _layers.Length || clip == null) return;
            Layer l = _layers[slot];
            Run now = l.Now;
            if (now.Clip != clip && l.Was.Clip == clip && l.Was.Weight > 0.001f)
            {
                // back to the clip still fading out: it comes back from its weight and its own time (no jump), and
                // the one that was coming in fades out instead
                _scratch.From(l.Was); l.Was.From(now); now.From(_scratch); _scratch.Clear();
                if (l.Was.Weight <= 0.001f) l.Was.Clear();
                l.Target = 1f; l.FadeSeconds = Mathf.Max(0.01f, fade); l.Hold = -1f; l.From = from;
                if (from == Source.Code) l.Rule = null;
                return;
            }
            if (now.Clip != clip)
            {
                // another clip on this layer: the old one fades out under it while the new one fades in; switched
                // again before a fade is done: the stronger of the two keeps fading, the weaker goes (it shows least)
                if (now.Clip != null && now.Weight > 0.001f && now.Weight >= l.Was.Weight) l.Was.From(now);
                now.Clear();
                now.NeedFreeze = true;
            }
            else if (now.Weight < 0.05f) { now.NeedFreeze = true; now.FrozenPose.Clear(); now.LastMoment = -1f; } // back after it faded: the pose of now
            now.Clip = clip; now.Start = start; now.Speed = speed;
            l.Target = 1f; l.FadeSeconds = Mathf.Max(0.01f, fade); l.Hold = -1f; l.From = from;
            if (from == Source.Code) l.Rule = null;
        }

        internal void Stop(int slot, float fade, Source from)
        {
            if (slot < 0 || slot >= _layers.Length) return;
            Layer l = _layers[slot];
            if (l.From != from && l.Clip != null) return; // a rule does not stop what code plays, nor the other way
            l.Target = 0f;
            l.FadeSeconds = Mathf.Max(0.01f, fade);
            l.Hold = -1f;
        }

        // the frozen base taken again at the next frame (the test window switched the base)
        internal void Refreeze(int slot) { if (slot >= 0 && slot < _layers.Length) { _layers[slot].Now.NeedFreeze = true; _layers[slot].Now.FrozenPose.Clear(); } }

        // the test window: one moment of the clip held (or −1: playing again)
        internal void Hold(int slot, float seconds) { if (slot >= 0 && slot < _layers.Length) _layers[slot].Hold = seconds; }

        internal float TimeIn(int slot) { Layer l = _layers[slot]; return MomentOf(l.Now, l.Hold); }

        private float MomentOf(Run r, float hold)
        {
            PAnimClip c = r.C ?? PlayerAnims.Find(r.Clip);
            if (c == null) return 0f;
            if (hold >= 0f) return hold;
            return c.MomentFor((float)(PlayerAnims.Now - r.Start) * r.Speed, c.Drive != null ? ValueOf(c.Drive) : 0f, c.Drive != null);
        }

        internal bool Playing(int slot) { return slot >= 0 && slot < _layers.Length && _layers[slot].Busy; }
        internal string ClipIn(int slot) { return slot >= 0 && slot < _layers.Length ? _layers[slot].Clip : null; }
        internal float WeightIn(int slot) { return slot >= 0 && slot < _layers.Length ? _layers[slot].Now.Weight : 0f; }
        internal string RuleIn(int slot) { return slot >= 0 && slot < _layers.Length && _layers[slot].Rule != null && _layers[slot].Clip != null ? _layers[slot].Rule.Name : null; }
        internal bool Missing(string bone) { return _missing.Contains(bone); }

        internal float ValueOf(string name) { return ValueAt(Array.IndexOf(PlayerAnims.Values, name)); }
        internal float ValueAt(int i) { return i >= 0 && i < _values.Length ? _values[i] : 0f; }
        internal bool HasTag(string tag) { return _tags.Contains(tag); }

        // what code plays, for the others: "slot,clip,start,speed,fade" per slot, ';' between (the rules' are not sent)
        internal string Code()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _layers.Length; i++)
            {
                Layer l = _layers[i];
                if (l.Clip == null || l.Target <= 0f || l.From != Source.Code) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append(',').Append(l.Clip).Append(',').Append(l.Now.Start.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                  .Append(PlayerAnims.N(l.Now.Speed)).Append(',').Append(PlayerAnims.N(l.FadeSeconds));
            }
            return sb.ToString();
        }

        internal void Take(string code)
        {
            bool[] on = new bool[_layers.Length];
            foreach (string part in code.Split(';'))
            {
                string[] w = part.Split(',');
                int slot; double start; float speed, fade;
                if (w.Length < 5 || !int.TryParse(w[0], out slot) || slot < 0 || slot >= _layers.Length || w[1].Length == 0) continue;
                if (!double.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out start)) continue;
                if (!float.TryParse(w[3], NumberStyles.Float, CultureInfo.InvariantCulture, out speed)) speed = 1f;
                if (!float.TryParse(w[4], NumberStyles.Float, CultureInfo.InvariantCulture, out fade)) fade = 0.25f;
                on[slot] = true;
                if (PlayerAnims.Find(w[1]) == null) PlayerAnims.Ask(_p, w[1]); // made in his test window: ask him for it
                Layer l = _layers[slot];
                if (l.Clip == w[1] && Math.Abs(l.Now.Start - start) < 0.01 && l.Target > 0f && l.From == Source.Code) continue;
                Play(slot, w[1], start, Mathf.Clamp(speed, 0.05f, 10f), fade, Source.Code);
            }
            for (int i = 0; i < on.Length; i++) if (!on[i] && _layers[i].Target > 0f && _layers[i].From == Source.Code) Stop(i, _layers[i].FadeSeconds, Source.Code);
        }

        private Bone BoneOf(string name)
        {
            Bone b;
            if (_bones.TryGetValue(name, out b))
            {
                if (b.T != null) return b;
                _bones.Remove(name); // the model was rebuilt: look again
            }
            if (_missing.Contains(name) || _visual == null) return null;
            Transform t = Utils.FindChild(_visual, name, Utils.IterativeSearchType.DepthFirst);
            if (t == null) { _missing.Add(name); return null; }
            b = new Bone { T = t };
            _bones[name] = b;
            return b;
        }

        // ---------------------------------------------------------------- measuring

        private static readonly int GroundHash = Animator.StringToHash("onGround"), WaterHash = Animator.StringToHash("inWater");
        private const int Ground = 5, Water = 6, Air = 7, Height = 8, Attack = 9;

        private void Measure(float dt)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;
            if (!_measured || dt <= 0f) { _lastPos = pos; _lastYaw = yaw; _measured = true; _airSince = -1f; return; }
            Vector3 vel = (pos - _lastPos) / dt;
            if (vel.sqrMagnitude > 2500f) vel = Vector3.zero; // a teleport
            float turn = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastPos = pos; _lastYaw = yaw;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float k = 1f - Mathf.Exp(-10f * dt); // smoothed: no jitter from the frame times
            Smooth(0, flat.magnitude, k);
            Smooth(1, Vector3.Dot(flat, transform.forward), k);
            Smooth(2, Vector3.Dot(flat, transform.right), k);
            Smooth(3, vel.y, k);
            Smooth(4, Mathf.Clamp(turn, -720f, 720f), 1f - Mathf.Exp(-6f * dt));
            // on the ground / in water: for the others the game's own animator flags (synced to every game; the
            // physics flags are the owner's only)
            bool mine = _p == Player.m_localPlayer;
            bool ground = mine || _anim == null ? _p.IsOnGround() : _anim.GetBool(GroundHash);
            bool water = mine || _anim == null ? _p.InWater() : _anim.GetBool(WaterHash);
            _values[Ground] = ground ? 1f : 0f;
            _values[Water] = water ? 1f : 0f;
            if (ground || water) _airSince = -1f;
            else if (_airSince < 0f) _airSince = Time.time;
            _values[Air] = _airSince < 0f ? 0f : Time.time - _airSince;
            _values[Attack] = _p.InAttack() ? 1f : 0f; // (the game's animator: the same in every game)
            // the height over the ground: a ray ten times a second, and only while something asks for it
            if (ground) _values[Height] = 0f;
            else if (Time.time >= _nextHeight && PlayerAnims.NeedsHeight)
            {
                _nextHeight = Time.time + 0.1f;
                RaycastHit hit;
                _values[Height] = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out hit, 200f, RavenMotor.Mask) ? Mathf.Max(0f, hit.distance - 0.2f) : 200f;
            }
            ReadTags();
        }

        // the tags, from the player's ZDO five times a second
        private void ReadTags()
        {
            if (Time.time < _nextTags) return;
            _nextTags = Time.time + 0.2f;
            ZDO zdo = NetFlag.Zdo(_p, false);
            string tags = zdo != null ? zdo.GetString(PlayerAnims.TagKey, "") : "";
            if (tags == _tagsRaw) return;
            _tagsRaw = tags;
            _tags.Clear();
            foreach (string t in tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) _tags.Add(t);
        }

        private void Smooth(int i, float v, float k) { _values[i] = Mathf.Lerp(_values[i], v, k); }

        // the rules: for each slot they drive, the first whose condition holds plays (code-played layers win)
        private void RunRules()
        {
            bool rules = PlayerAnims.Rules.Count > 0;
            for (int slot = 0; slot < PlayerAnims.Slots - 1; slot++)
            {
                Layer l = _layers[slot];
                if (l.From == Source.Code && l.Busy) continue;
                PAnimRule win = null;
                if (rules)
                    foreach (PAnimRule r in PlayerAnims.Rules)
                        if (r.On && r.Slot == slot && PlayerAnims.Find(r.Clip) != null && r.Holds(this)) { win = r; break; }
                if (l.From == Source.Rule && l.Rule != null && l.Rule != win && l.Target > 0f && Time.time - l.RuleSince < PlayerAnims.MinHold) continue; // it holds a moment first
                if (win == null)
                {
                    if (l.From == Source.Rule && l.Clip != null && l.Target > 0f)
                    {
                        Note(Lang.F("шар {0}: «{1}» згасає (жодне правило)", slot, (l.Rule != null ? l.Rule.Name : l.Clip)));
                        l.Target = 0f; l.FadeSeconds = l.Rule != null ? Mathf.Max(0.01f, l.Rule.FadeOut) : 0.3f;
                    }
                    continue;
                }
                if (l.Clip == win.Clip && l.Target > 0f) { l.Rule = win; continue; } // the same clip under another rule: it plays on, no restart
                Note(Lang.F("шар {0}: {1}«{2}»", slot, (l.Rule != null && l.Clip != null ? "«" + l.Rule.Name + "» → " : ""), win.Name));
                Play(slot, win.Clip, PlayerAnims.Now, win.Speed, win.FadeIn, Source.Rule);
                l.Rule = win;
                l.RuleSince = Time.time;
            }
        }

        // ---------------------------------------------------------------- applying

        private static readonly HashSet<string> Touched = new HashSet<string>();

        private bool AnyCode() { foreach (Layer l in _layers) if (l.From == Source.Code && l.Busy) return true; return false; }

        private void LateUpdate()
        {
            if (_p == null) { Destroy(this); return; }
            bool rules = PlayerAnims.Rules.Count > 0;
            if (rules && _p != Player.m_localPlayer && !AnyCode())
            {
                // far from the camera: the rules let him be (nobody sees the difference)
                Camera cam = Utils.GetMainCamera();
                if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > FarFromCamera * FarFromCamera) rules = false;
            }
            if (rules && PlayerAnims.AllNeedTag) { ReadTags(); if (_tags.Count == 0 && !AnyBusy()) rules = false; } // no tag, no rule can hold
            if (!rules && !AnyBusy()) { if (!_resting) Rest(); return; } // nothing to play: no measuring, no work
            _resting = false;
            float dt = Time.deltaTime;
            if (_visual == null)
            {
                _anim = _p.GetComponentInChildren<Animator>();
                _visual = _anim != null ? _anim.transform : null;
                if (_visual == null) return;
                _missing.Clear();
            }
            Measure(dt);
            if (rules) RunRules(); else StopRules();

            bool any = false;
            foreach (Layer l in _layers)
            {
                Run now = l.Now, was = l.Was;
                if (was.Clip != null)
                {
                    was.C = PlayerAnims.Find(was.Clip);
                    was.Weight = Mathf.MoveTowards(was.Weight, 0f, dt / l.FadeSeconds);
                    if (was.Weight <= 0.001f || was.C == null) was.Clear(); else any = true;
                }
                if (now.Clip == null) continue;
                now.C = PlayerAnims.Find(now.Clip);
                now.Weight = Mathf.MoveTowards(now.Weight, l.Target, dt / l.FadeSeconds);
                // a clip played once (by the clock) ends by itself: it fades out after its last moment
                PAnimClip c = now.C;
                if (c != null && !c.Loop && c.Drive == null && l.Hold < 0f && l.Target > 0f && (PlayerAnims.Now - now.Start) * now.Speed >= c.Length) { l.Target = 0f; l.FadeSeconds = Mathf.Max(l.FadeSeconds, PlayerAnims.EndFade); }
                if (now.Weight <= 0.001f && l.Target <= 0f) { now.Clear(); l.Rule = null; continue; }
                if (c != null) any = true; // (a clip still on its way from its owner: nothing to draw yet)
            }
            if (!any) { Release(); return; } // (the measuring goes on: the rules need it)

            // the bones the clips touch, back to the game's pose of this frame
            Touched.Clear();
            foreach (Layer l in _layers) { Mark(l.Was); Mark(l.Now); }
            foreach (KeyValuePair<string, Bone> kv in _bones) if (!Touched.Contains(kv.Key)) kv.Value.Release();
            foreach (string name in Touched) { Bone b = BoneOf(name); if (b != null) b.Begin(); }

            // first the whole body: turned round the pivot (pitch forward, yaw, roll to the side), then moved; the
            // bones after it are reckoned in its frame
            Quaternion bodyQ = Quaternion.identity;
            Vector3 move = Vector3.zero;
            float pivot = 1.1f, pivotW = 0f;
            foreach (Layer l in _layers) { Whole(l.Was, -1f, ref bodyQ, ref move, ref pivot, ref pivotW); Whole(l.Now, l.Hold, ref bodyQ, ref move, ref pivot, ref pivotW); }
            Vector3 body = bodyQ.eulerAngles;
            if (Quaternion.Angle(bodyQ, Quaternion.identity) > 0.01f || move.sqrMagnitude > 0.000001f)
            {
                if (!_visualMoved) { _visualPos = _visual.localPosition; _visualRot = _visual.localRotation; _visualMoved = true; }
                Quaternion r = bodyQ;
                Vector3 pv = Vector3.up * pivot;
                _visual.localRotation = _visualRot * r;
                _visual.localPosition = _visualPos + (pv - r * pv) + move;
            }
            else RestoreVisual();

            // then the bones, layer by layer (within a layer the old clip first, the new one over it), parents first,
            // each written at once: a child is reckoned from its parent as it is now
            Quaternion frame = _visual.rotation;
            foreach (Layer l in _layers) { Bones(l.Was, frame, false, l); Bones(l.Now, frame, true, l); }
            // our turns eased (parents first, as written); the game's own motion under them is never held back
            float ease = PlayerAnims.Smoothing > 0.001f && !Held() ? 1f - Mathf.Exp(-dt / PlayerAnims.Smoothing) : 1f;
            foreach (string name in Touched) { Bone b; if (_bones.TryGetValue(name, out b) && b.T != null) { b.Ease(ease); b.Commit(); } }
            if (PAnimLog.On && _p == Player.m_localPlayer) Record(body, dt);
        }

        // ---------------------------------------------------------------- the log (local player)

        private readonly Dictionary<string, Quaternion> _prevBone = new Dictionary<string, Quaternion>();
        private Vector3 _prevBody;
        private bool _hadBody;
        private readonly StringBuilder _line = new StringBuilder();

        private void Note(string text) { if (PAnimLog.On && _p == Player.m_localPlayer) PAnimLog.Event(text + "  [speed " + PAnimLog.N(_values[0]) + " vy " + PAnimLog.N(_values[3]) + " turn " + PAnimLog.N(_values[4]) + "]"); }

        private void Record(Vector3 body, float dt)
        {
            float scale = dt > 0.0001f ? Mathf.Min(1f, (1f / 60f) / dt) : 1f; // a long frame counted as at 60 a second (a short one as it is)
            string worst = "-", gameWorst = "-";
            float most = 0f, gameMost = 0f;
            foreach (string name in Touched)
            {
                Bone b;
                if (!_bones.TryGetValue(name, out b) || b.T == null) continue;
                if (b.GameMove * scale > gameMost) { gameMost = b.GameMove * scale; gameWorst = name; }
                Quaternion q = b.T.localRotation, was;
                if (_prevBone.TryGetValue(name, out was))
                {
                    float d = Quaternion.Angle(was, q) * scale;
                    if (d > most) { most = d; worst = name; }
                }
                _prevBone[name] = q;
            }
            float bodyJump = _hadBody ? Quaternion.Angle(Quaternion.Euler(_prevBody), Quaternion.Euler(body)) * scale : 0f;
            _prevBody = body; _hadBody = true;
            _line.Length = 0;
            _line.Append(OdinWings.PhaseName()).Append(" | ")
                 .Append(PAnimLog.N(_values[0])).Append(' ').Append(PAnimLog.N(_values[1])).Append(' ').Append(PAnimLog.N(_values[3])).Append(' ')
                 .Append(PAnimLog.N(_values[4])).Append(' ').Append(PAnimLog.N(_values[7])).Append(' ').Append(PAnimLog.N(_values[8])).Append(" a").Append(PAnimLog.N(_values[9])).Append(" |");
            for (int i = 0; i < _layers.Length; i++)
            {
                Layer l = _layers[i];
                if (l.Now.Clip == null && l.Was.Clip == null) continue;
                _line.Append(' ').Append(i).Append('=').Append(l.Now.Clip ?? "-").Append(':').Append(PAnimLog.N(l.Now.Weight * 100f));
                if (l.Was.Clip != null) _line.Append("(<-").Append(l.Was.Clip).Append(':').Append(PAnimLog.N(l.Was.Weight * 100f)).Append(')');
            }
            _line.Append(" | ").Append(PAnimLog.N(body.x)).Append('/').Append(PAnimLog.N(body.y)).Append('/').Append(PAnimLog.N(body.z))
                 .Append(" | ").Append(worst).Append(' ').Append(PAnimLog.N(most)).Append('°')
                 .Append(Lang.T(" | гра: ")).Append(gameWorst).Append(' ').Append(PAnimLog.N(gameMost)).Append('°'); // (the game's own animation under ours)
            PAnimLog.Frame(_line.ToString());
            if (most > PAnimLog.BoneJump) PAnimLog.Glitch(Lang.F("{0} на {1}° за кадр", worst, PAnimLog.N(most)));
            if (bodyJump > PAnimLog.BodyJump) PAnimLog.Glitch(Lang.F("тіло на {0}° за кадр", PAnimLog.N(bodyJump)));
        }

        private bool AnyBusy() { foreach (Layer l in _layers) if (l.Busy || l.Was.Clip != null) return true; return false; }

        // the test window holds a moment: the pose shown at once, not eased
        private bool Held() { foreach (Layer l in _layers) if (l.Hold >= 0f && l.Clip != null) return true; return false; }

        // no rules (any more): what they played fades out
        private void StopRules()
        {
            foreach (Layer l in _layers)
                if (l.From == Source.Rule && l.Clip != null && l.Target > 0f) { l.Target = 0f; l.FadeSeconds = l.Rule != null ? Mathf.Max(0.01f, l.Rule.FadeOut) : 0.3f; }
        }

        private static void Mark(Run r)
        {
            if (r.C == null || r.Weight <= 0.001f) return;
            foreach (string b in r.C.Bones) if (b != PlayerAnims.Body && b != PlayerAnims.Move) Touched.Add(b);
        }

        // a run's share of the whole body (and its moment, for the bones after)
        private void Whole(Run r, float hold, ref Quaternion body, ref Vector3 move, ref float pivot, ref float pivotW)
        {
            PAnimClip c = r.C;
            if (c == null || r.Weight <= 0.001f) return;
            r.Moment = MomentOf(r, hold);
            float w = r.Weight;
            if (c.Bones.Contains(PlayerAnims.Body)) { body = body * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(c.Value(PlayerAnims.Body, r.Moment)), w); if (w > pivotW) { pivot = c.Pivot; pivotW = w; } }
            if (c.Bones.Contains(PlayerAnims.Move)) move += c.Value(PlayerAnims.Move, r.Moment) * w;
        }

        // a run's bones, by each one's mode, weighted by the run
        private void Bones(Run r, Quaternion frame, bool sounds, Layer l)
        {
            PAnimClip c = r.C;
            if (c == null || r.Weight <= 0.001f) return;
            float t = r.Moment, w = r.Weight;
            if (c.Frozen && r.NeedFreeze)
            {
                r.NeedFreeze = false;
                foreach (string name in c.Bones) { Bone fb; if (_bones.TryGetValue(name, out fb) && fb.T != null) r.FrozenPose[name] = fb.T.localRotation; }
            }
            foreach (string bone in c.Ordered())
            {
                if (bone == PlayerAnims.Body || bone == PlayerAnims.Move) continue;
                Bone b;
                if (!_bones.TryGetValue(bone, out b) || b.T == null) continue;
                Transform tr = b.T;
                Vector3 v = c.Value(bone, t);
                Quaternion frozen;
                if (c.Frozen && r.FrozenPose.TryGetValue(bone, out frozen)) tr.localRotation = Quaternion.Slerp(tr.localRotation, frozen, w);
                int mode = c.ModeOf(bone);
                Transform next = null;
                if (mode == PlayerAnims.Aim)
                {
                    string nn = PlayerAnims.NextOf(bone);
                    Bone nb = nn != null ? BoneOf(nn) : null;
                    next = nb != null ? nb.T : null;
                    if (next == null) mode = PlayerAnims.Frame; // the end of a chain: round the body's axes instead
                }
                if (mode == PlayerAnims.Turn) tr.localRotation = tr.localRotation * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(v), w);
                else if (mode == PlayerAnims.Frame)
                {
                    Quaternion q = frame * Quaternion.Euler(v) * Quaternion.Inverse(frame);
                    tr.rotation = Quaternion.Slerp(Quaternion.identity, q, w) * tr.rotation;
                }
                else
                {
                    // From where this run had the bone last frame (the first time: the pose under it), turned only as
                    // far as needed to point where it should: the bone never spins round itself because the pose
                    // under it changed, and no turn is ever nearly opposite (no flip). The twist is put on after.
                    Vector3 along = next.localPosition;
                    if (along.sqrMagnitude < 1e-8f) continue;
                    Quaternion held;
                    Quaternion world = r.Aims.TryGetValue(bone, out held) ? frame * held : tr.rotation;
                    Vector3 want = frame * PlayerAnims.Pointing(v.x, v.y);
                    world = Quaternion.FromToRotation(world * along, want) * world;
                    r.Aims[bone] = Quaternion.Inverse(frame) * world;
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.AngleAxis(v.z, want) * world, w);
                }
            }
            if (sounds) Sounds(r, c, t, l.Hold >= 0f || l.Target <= 0f);
        }

        // the clip's sounds whose moment passed since the last frame (round the end of a looped clip too)
        private void Sounds(Run r, PAnimClip c, float t, bool quiet)
        {
            float was = r.LastMoment;
            r.LastMoment = t;
            if (c.Events.Count == 0 || was < 0f || quiet) return;
            bool wrapped = t < was && c.Loop && c.Drive == null;
            foreach (PAnimEvent e in c.Events)
            {
                bool hit = wrapped ? (e.T > was || e.T <= t) : (e.T > was && e.T <= t);
                if (hit) PlayerAnims.Sound(e.Sound, _p.transform.position + Vector3.up * 1.2f, e.Volume);
            }
        }

        // the bones and the body back to the game
        private void Release()
        {
            foreach (Bone b in _bones.Values) b.Release();
            RestoreVisual();
        }

        // all of it back to the game, and the measuring starts anew next time
        private void Rest()
        {
            Release();
            _measured = false;
            _resting = true;
        }

        private void RestoreVisual()
        {
            if (!_visualMoved || _visual == null) { _visualMoved = false; return; }
            _visual.localPosition = _visualPos;
            _visual.localRotation = _visualRot;
            _visualMoved = false;
        }
    }
}
#endif

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
        internal const int Slots = 6, PreviewSlot = 5;
        internal static float MinHold = 0.6f; // a state that took over holds at least this long (seconds)
        internal static float EndFade = 0.35f; // a clip played once fades out at least this long after its end
        internal static float Smoothing = 0.12f; // seconds: our bone turns ease after what the clips want (0: none)
        internal const string NetKey = "RavenWatch_PAnim", TagKey = "RavenWatch_PAnimTags";
        private const string RpcAsk = "RavenWatch_PAnimAsk", RpcClip = "RavenWatch_PAnimClip";

        // the motion values each game measures for every player (rules, driven clips, the test window)
        internal static readonly string[] Values = { "speed", "fwd", "side", "vy", "turn", "ground", "water", "air", "height", "attack", "accel" };
        internal static string[] ValueNames { get { return Lang.Arr(ValueNamesSrc); } } // (translated on every read)
        private static readonly string[] ValueNamesSrc = Lang.Keys("швидкість по землі, м/с", "уперед (−назад), м/с", "вбік (+вправо), м/с", "вгору (−вниз), м/с", "поворот (+вправо), °/с", "на землі (1/0)", "у воді (1/0)", "у повітрі, с", "висота над землею, м", "б'є зброєю (1/0)", "розгін уперед (−гальмує), м/с²");
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

        // a bone of the player's own skeleton (the body mesh's bones): the ravens sit on his bones and have a Neck, a
        // Head, a Spine of their own, which a plain search by name could find first
        internal static Transform SkeletonBone(Player p, Transform visual, string name)
        {
            VisEquipment ve = p != null ? p.GetComponent<VisEquipment>() : null;
            if (ve != null && ve.m_bodyModel != null && ve.m_bodyModel.bones != null)
                foreach (Transform t in ve.m_bodyModel.bones) if (t != null && t.name == name) return t;
            return visual != null ? Utils.FindChild(visual, name, Utils.IterativeSearchType.BreadthFirst) : null;
        }

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
            "clip wings_hover\nlength 7.2\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 6 0 1.3\n0 move 0 0 0\n0 Neck -4.6 0 0\n0 Head -3.2 3.1 0\n0 LeftArm -40 -93 0\n0 RightArm -38.4 93 0\n0 LeftForeArm -30 -47 0\n0 RightForeArm -24 47 0\n0 LeftUpLeg -81.3 0 0\n0 RightUpLeg -77.1 0 0\n0 LeftLeg -95.2 0 0\n0 RightLeg -97.7 0 0\n0 LeftFoot -48.1 0 0\n0 RightFoot -45.3 0 0\n0.3 body 6.6 0 1.5\n0.3 move 0 0 0\n0.3 Neck -4.2 0 0\n0.3 Head -3.8 3.7 0\n0.3 LeftArm -38.7 -93 0\n0.3 RightArm -37.6 93 0\n0.3 LeftForeArm -29 -47 0\n0.3 RightForeArm -24 47 0\n0.3 LeftUpLeg -78.4 0 0\n0.3 RightUpLeg -79.5 0 0\n0.3 LeftLeg -97 0 0\n0.3 RightLeg -96.3 0 0\n0.3 LeftFoot -45 0 0\n0.3 RightFoot -47.8 0 0\n0.6 body 7 0 1.5\n0.6 move 0 0 0\n0.6 Neck -3.9 0 0\n0.6 Head -4.8 4 0\n0.6 LeftArm -37.8 -93 0\n0.6 RightArm -37.5 93 0\n0.6 LeftForeArm -28 -47 0\n0.6 RightForeArm -23 47 0\n0.6 LeftUpLeg -79.5 0 0\n0.6 RightUpLeg -83.2 0 0\n0.6 LeftLeg -96.3 0 0\n0.6 RightLeg -94.1 0 0\n0.6 LeftFoot -47 0 0\n0.6 RightFoot -51.6 0 0\n0.9 body 7.2 0 1.4\n0.9 move 0 0 0\n0.9 Neck -3.7 0 0\n0.9 Head -5.8 4 0\n0.9 LeftArm -37.5 -93 0\n0.9 RightArm -38.1 93 0\n0.9 LeftForeArm -27 -47 0\n0.9 RightForeArm -24 47 0\n0.9 LeftUpLeg -81.7 0 0\n0.9 RightUpLeg -85.8 0 0\n0.9 LeftLeg -95 0 0\n0.9 RightLeg -93 0 0\n0.9 LeftFoot -51.9 0 0\n0.9 RightFoot -54.5 0 0\n1.2 body 7 0 1.3\n1.2 move 0 0 0\n1.2 Neck -3.5 0 0\n1.2 Head -6.6 3.7 0\n1.2 LeftArm -37.8 -93 0\n1.2 RightArm -39.1 93 0\n1.2 LeftForeArm -28 -47 0\n1.2 RightForeArm -25 47 0\n1.2 LeftUpLeg -82.1 0 0\n1.2 RightUpLeg -85.5 0 0\n1.2 LeftLeg -94.8 0 0\n1.2 RightLeg -93 0 0\n1.2 LeftFoot -55 0 0\n1.2 RightFoot -54.7 0 0\n1.5 body 6.6 0 1\n1.5 move 0 0 0\n1.5 Neck -3.5 0 0\n1.5 Head -7 3.2 0\n1.5 LeftArm -38.7 -93 0\n1.5 RightArm -40.4 93 0\n1.5 LeftForeArm -29 -47 0\n1.5 RightForeArm -26 47 0\n1.5 LeftUpLeg -80.9 0 0\n1.5 RightUpLeg -82.2 0 0\n1.5 LeftLeg -95.5 0 0\n1.5 RightLeg -94.7 0 0\n1.5 LeftFoot -53 0 0\n1.5 RightFoot -52.2 0 0\n1.8 body 6 0 0.7\n1.8 move 0 0 0\n1.8 Neck -3.6 0 0\n1.8 Head -6.8 2.5 0\n1.8 LeftArm -40 -93 0\n1.8 RightArm -41.6 93 0\n1.8 LeftForeArm -30 -47 0\n1.8 RightForeArm -28 47 0\n1.8 LeftUpLeg -81.3 0 0\n1.8 RightUpLeg -78.1 0 0\n1.8 LeftLeg -95.2 0 0\n1.8 RightLeg -97.1 0 0\n1.8 LeftFoot -48.1 0 0\n1.8 RightFoot -48.4 0 0\n2.1 body 5.4 0 0.3\n2.1 move 0 0 0\n2.1 Neck -3.7 0 0\n2.1 Head -6.2 1.6 0\n2.1 LeftArm -41.2 -93 0\n2.1 RightArm -42.4 93 0\n2.1 LeftForeArm -31 -47 0\n2.1 RightForeArm -28 47 0\n2.1 LeftUpLeg -85.4 0 0\n2.1 RightUpLeg -75.8 0 0\n2.1 LeftLeg -93 0 0\n2.1 RightLeg -98.5 0 0\n2.1 LeftFoot -45 0 0\n2.1 RightFoot -45.5 0 0\n2.4 body 5 0 -0.1\n2.4 move 0 0 0\n2.4 Neck -4 0 0\n2.4 Head -5.2 0.6 0\n2.4 LeftArm -42.2 -93 0\n2.4 RightArm -42.5 93 0\n2.4 LeftForeArm -32 -47 0\n2.4 RightForeArm -28 47 0\n2.4 LeftUpLeg -91.7 0 0\n2.4 RightUpLeg -76.9 0 0\n2.4 LeftLeg -93 0 0\n2.4 RightLeg -97.8 0 0\n2.4 LeftFoot -47 0 0\n2.4 RightFoot -45.3 0 0\n2.7 body 4.8 0 -0.5\n2.7 move 0 0 0\n2.7 Neck -4.3 0 0\n2.7 Head -4.2 -0.5 0\n2.7 LeftArm -42.5 -93 0\n2.7 RightArm -41.9 93 0\n2.7 LeftForeArm -32 -47 0\n2.7 RightForeArm -28 47 0\n2.7 LeftUpLeg -95.7 0 0\n2.7 RightUpLeg -81.5 0 0\n2.7 LeftLeg -93 0 0\n2.7 RightLeg -95.1 0 0\n2.7 LeftFoot -51.9 0 0\n2.7 RightFoot -47.8 0 0\n3 body 5 0 -0.8\n3 move 0 0 0\n3 Neck -4.7 0 0\n3 Head -3.4 -1.5 0\n3 LeftArm -42.2 -93 0\n3 RightArm -40.9 93 0\n3 LeftForeArm -32 -47 0\n3 RightForeArm -27 47 0\n3 LeftUpLeg -94.2 0 0\n3 RightUpLeg -87.4 0 0\n3 LeftLeg -93 0 0\n3 RightLeg -93 0 0\n3 LeftFoot -55 0 0\n3 RightFoot -51.6 0 0\n3.3 body 5.4 0 -1.1\n3.3 move 0 0 0\n3.3 Neck -5.1 0 0\n3.3 Head -3 -2.4 0\n3.3 LeftArm -41.3 -93 0\n3.3 RightArm -39.6 93 0\n3.3 LeftForeArm -31 -47 0\n3.3 RightForeArm -26 47 0\n3.3 LeftUpLeg -87.9 0 0\n3.3 RightUpLeg -91.8 0 0\n3.3 LeftLeg -93 0 0\n3.3 RightLeg -93 0 0\n3.3 LeftFoot -53 0 0\n3.3 RightFoot -54.5 0 0\n3.6 body 6 0 -1.3\n3.6 move 0 0 0\n3.6 Neck -5.4 0 0\n3.6 Head -3.2 -3.1 0\n3.6 LeftArm -40 -93 0\n3.6 RightArm -38.4 93 0\n3.6 LeftForeArm -30 -47 0\n3.6 RightForeArm -24 47 0\n3.6 LeftUpLeg -81.3 0 0\n3.6 RightUpLeg -92.9 0 0\n3.6 LeftLeg -95.2 0 0\n3.6 RightLeg -93 0 0\n3.6 LeftFoot -48.1 0 0\n3.6 RightFoot -54.7 0 0\n3.9 body 6.6 0 -1.5\n3.9 move 0 0 0\n3.9 Neck -5.8 0 0\n3.9 Head -3.8 -3.7 0\n3.9 LeftArm -38.8 -93 0\n3.9 RightArm -37.6 93 0\n3.9 LeftForeArm -29 -47 0\n3.9 RightForeArm -24 47 0\n3.9 LeftUpLeg -78.4 0 0\n3.9 RightUpLeg -90.5 0 0\n3.9 LeftLeg -97 0 0\n3.9 RightLeg -93 0 0\n3.9 LeftFoot -45 0 0\n3.9 RightFoot -52.2 0 0\n4.2 body 7 0 -1.5\n4.2 move 0 0 0\n4.2 Neck -6.1 0 0\n4.2 Head -4.8 -4 0\n4.2 LeftArm -37.8 -93 0\n4.2 RightArm -37.5 93 0\n4.2 LeftForeArm -28 -47 0\n4.2 RightForeArm -23 47 0\n4.2 LeftUpLeg -79.5 0 0\n4.2 RightUpLeg -86.8 0 0\n4.2 LeftLeg -96.3 0 0\n4.2 RightLeg -93 0 0\n4.2 LeftFoot -47 0 0\n4.2 RightFoot -48.4 0 0\n4.5 body 7.2 0 -1.4\n4.5 move 0 0 0\n4.5 Neck -6.3 0 0\n4.5 Head -5.8 -4 0\n4.5 LeftArm -37.5 -93 0\n4.5 RightArm -38.1 93 0\n4.5 LeftForeArm -27 -47 0\n4.5 RightForeArm -24 47 0\n4.5 LeftUpLeg -81.7 0 0\n4.5 RightUpLeg -84.2 0 0\n4.5 LeftLeg -95 0 0\n4.5 RightLeg -93.5 0 0\n4.5 LeftFoot -51.9 0 0\n4.5 RightFoot -45.5 0 0\n4.8 body 7 0 -1.3\n4.8 move 0 0 0\n4.8 Neck -6.5 0 0\n4.8 Head -6.6 -3.7 0\n4.8 LeftArm -37.8 -93 0\n4.8 RightArm -39.1 93 0\n4.8 LeftForeArm -28 -47 0\n4.8 RightForeArm -25 47 0\n4.8 LeftUpLeg -82.1 0 0\n4.8 RightUpLeg -84.5 0 0\n4.8 LeftLeg -94.8 0 0\n4.8 RightLeg -93.3 0 0\n4.8 LeftFoot -55 0 0\n4.8 RightFoot -45.3 0 0\n5.1 body 6.6 0 -1\n5.1 move 0 0 0\n5.1 Neck -6.5 0 0\n5.1 Head -7 -3.2 0\n5.1 LeftArm -38.7 -93 0\n5.1 RightArm -40.4 93 0\n5.1 LeftForeArm -29 -47 0\n5.1 RightForeArm -26 47 0\n5.1 LeftUpLeg -80.9 0 0\n5.1 RightUpLeg -87.8 0 0\n5.1 LeftLeg -95.5 0 0\n5.1 RightLeg -93 0 0\n5.1 LeftFoot -53 0 0\n5.1 RightFoot -47.8 0 0\n5.4 body 6 0 -0.7\n5.4 move 0 0 0\n5.4 Neck -6.4 0 0\n5.4 Head -6.8 -2.5 0\n5.4 LeftArm -40 -93 0\n5.4 RightArm -41.6 93 0\n5.4 LeftForeArm -30 -47 0\n5.4 RightForeArm -28 47 0\n5.4 LeftUpLeg -81.3 0 0\n5.4 RightUpLeg -91.9 0 0\n5.4 LeftLeg -95.2 0 0\n5.4 RightLeg -93 0 0\n5.4 LeftFoot -48.1 0 0\n5.4 RightFoot -51.6 0 0\n5.7 body 5.4 0 -0.3\n5.7 move 0 0 0\n5.7 Neck -6.3 0 0\n5.7 Head -6.2 -1.6 0\n5.7 LeftArm -41.2 -93 0\n5.7 RightArm -42.4 93 0\n5.7 LeftForeArm -31 -47 0\n5.7 RightForeArm -28 47 0\n5.7 LeftUpLeg -85.4 0 0\n5.7 RightUpLeg -94.2 0 0\n5.7 LeftLeg -93 0 0\n5.7 RightLeg -93 0 0\n5.7 LeftFoot -45 0 0\n5.7 RightFoot -54.5 0 0\n6 body 5 0 0.1\n6 move 0 0 0\n6 Neck -6 0 0\n6 Head -5.2 -0.6 0\n6 LeftArm -42.2 -93 0\n6 RightArm -42.5 93 0\n6 LeftForeArm -32 -47 0\n6 RightForeArm -28 47 0\n6 LeftUpLeg -91.7 0 0\n6 RightUpLeg -93.1 0 0\n6 LeftLeg -93 0 0\n6 RightLeg -93 0 0\n6 LeftFoot -47 0 0\n6 RightFoot -54.7 0 0\n6.3 body 4.8 0 0.5\n6.3 move 0 0 0\n6.3 Neck -5.7 0 0\n6.3 Head -4.2 0.5 0\n6.3 LeftArm -42.5 -93 0\n6.3 RightArm -41.9 93 0\n6.3 LeftForeArm -32 -47 0\n6.3 RightForeArm -28 47 0\n6.3 LeftUpLeg -95.7 0 0\n6.3 RightUpLeg -88.5 0 0\n6.3 LeftLeg -93 0 0\n6.3 RightLeg -93 0 0\n6.3 LeftFoot -51.9 0 0\n6.3 RightFoot -52.2 0 0\n6.6 body 5 0 0.8\n6.6 move 0 0 0\n6.6 Neck -5.3 0 0\n6.6 Head -3.4 1.5 0\n6.6 LeftArm -42.2 -93 0\n6.6 RightArm -40.9 93 0\n6.6 LeftForeArm -32 -47 0\n6.6 RightForeArm -27 47 0\n6.6 LeftUpLeg -94.2 0 0\n6.6 RightUpLeg -82.6 0 0\n6.6 LeftLeg -93 0 0\n6.6 RightLeg -94.4 0 0\n6.6 LeftFoot -55 0 0\n6.6 RightFoot -48.4 0 0\n6.9 body 5.4 0 1.1\n6.9 move 0 0 0\n6.9 Neck -4.9 0 0\n6.9 Head -3 2.4 0\n6.9 LeftArm -41.3 -93 0\n6.9 RightArm -39.6 93 0\n6.9 LeftForeArm -31 -47 0\n6.9 RightForeArm -26 47 0\n6.9 LeftUpLeg -87.9 0 0\n6.9 RightUpLeg -78.2 0 0\n6.9 LeftLeg -93 0 0\n6.9 RightLeg -97.1 0 0\n6.9 LeftFoot -53 0 0\n6.9 RightFoot -45.5 0 0\n",
            "clip wings_fly\nlength 3.6\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 18 0 0\n1.8 body 20 0 0\n0 move 0 0 0\n1.8 move 0 0.03 0\n0 Neck -6 0 0\n0 Head -8 0 0\n1.8 Neck -6 0 0\n1.8 Head -9 0 0\n0 LeftArm -48 -100 0\n0 RightArm -48 100 0\n0 LeftForeArm -30 -62 0\n0 RightForeArm -26 56 0\n1.8 LeftArm -44 -98 0\n1.8 RightArm -44 98 0\n1.8 LeftForeArm -26 -60 0\n1.8 RightForeArm -22 54 0\n0 LeftUpLeg -82 -178 0\n0 LeftLeg -80 -178 0\n0 LeftFoot -70 -178 0\n0 RightUpLeg -52 0 0\n0 RightLeg -74 178 0\n0 RightFoot -60 178 0\n1.8 LeftUpLeg -85 -178 0\n1.8 LeftLeg -80 -178 0\n1.8 LeftFoot -70 -178 0\n1.8 RightUpLeg -55 0 0\n1.8 RightLeg -77 178 0\n1.8 RightFoot -60 178 0\n",
            "clip wings_climb\nlength 2.8\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 5 0 0\n1.4 body 7 0 0\n0 move 0 0 0\n1.4 move 0 0.03 0\n0 Neck -8 0 0\n0 Head -10 0 0\n1.4 Neck -8 0 0\n1.4 Head -11 0 0\n0 LeftArm -40 -96 0\n0 RightArm -40 96 0\n0 LeftForeArm -22 -58 0\n0 RightForeArm -18 52 0\n1.4 LeftArm -36 -95 0\n1.4 RightArm -36 95 0\n1.4 LeftForeArm -18 -57 0\n1.4 RightForeArm -14 51 0\n0 LeftUpLeg -82 -178 0\n0 LeftLeg -80 -178 0\n0 LeftFoot -70 -178 0\n0 RightUpLeg -52 0 0\n0 RightLeg -74 178 0\n0 RightFoot -60 178 0\n1.4 LeftUpLeg -85 -178 0\n1.4 LeftLeg -80 -178 0\n1.4 LeftFoot -70 -178 0\n1.4 RightUpLeg -55 0 0\n1.4 RightLeg -77 178 0\n1.4 RightFoot -60 178 0\n",
            "clip wings_dive\nlength 3\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 55 0 0\n0 Neck -14 0 0\n0 Head -20 0 0\n0 LeftArm -68 -118 0\n0 LeftForeArm -62 -100 0\n0 RightArm -72 122 0\n0 RightForeArm -66 104 0\n0 LeftUpLeg -87 -178 0\n0 LeftLeg -82 -178 0\n0 LeftFoot -70 -178 0\n0 RightUpLeg -61 0 0\n0 RightLeg -81 178 0\n0 RightFoot -62 178 0\n1.5 body 58 0 0\n1.5 Neck -14 0 0\n1.5 Head -20 0 0\n1.5 LeftArm -72 -121 0\n1.5 LeftForeArm -66 -103 0\n1.5 RightArm -69 119 0\n1.5 RightForeArm -63 101 0\n1.5 LeftUpLeg -84 -178 0\n1.5 LeftLeg -82 -178 0\n1.5 LeftFoot -70 -178 0\n1.5 RightUpLeg -58 0 0\n1.5 RightLeg -78 178 0\n1.5 RightFoot -62 178 0\n",
            "clip wings_lean\nlength 1\nloop 0\npivot 1.1\ndrive accel -8 8\n0 body -14 0 0\n0.5 body 0 0 0\n1 body 14 0 0\n",
            "clip wings_speed\nlength 1\nloop 0\npivot 1.1\ndrive speed 4 14\nmode LeftArm body\nmode RightArm body\nmode LeftForeArm body\nmode RightForeArm body\nmode LeftUpLeg body\nmode RightUpLeg body\nmode RightLeg body\n0 LeftArm 0 0 0\n1 LeftArm 0 -40 0\n0 RightArm 0 0 0\n1 RightArm 0 40 0\n0 LeftForeArm 0 0 0\n1 LeftForeArm 0 -20 0\n0 RightForeArm 0 0 0\n1 RightForeArm 0 20 0\n0 LeftUpLeg 0 0 0\n1 LeftUpLeg 10 0 0\n0 RightUpLeg 0 0 0\n1 RightUpLeg 12 0 0\n0 RightLeg 0 0 0\n1 RightLeg -16 0 0\n",
            "clip wings_bank\nlength 1\nloop 0\npivot 1.1\ndrive turn -180 180\nmode LeftArm body\nmode RightArm body\nmode Neck body\nmode Head body\n0 body 0 0 20\n0.5 body 0 0 0\n1 body 0 0 -20\n0 LeftArm 0 0 14\n0.5 LeftArm 0 0 0\n1 LeftArm 0 0 -14\n0 RightArm 0 0 14\n0.5 RightArm 0 0 0\n1 RightArm 0 0 -14\n0 Neck 0 -9 0\n0.5 Neck 0 0 0\n1 Neck 0 9 0\n0 Head 0 -18 0\n0.5 Head 0 0 0\n1 Head 0 18 0\n",
            "clip wings_takeoff_b\nlength 1.6\nloop 0\npivot 1.1\nevent 0.46 sfx_raven_flap 1.4\nevent 0.95 sfx_raven_flap 0.7\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n0 move 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 LeftUpLeg -61 16 0\n0 RightUpLeg -73 105 0\n0 LeftLeg -81 -70 0\n0 RightLeg -60 -142 0\n0 LeftFoot -30 0 0\n0 RightFoot -30 0 0\n0.22 body 16 0 0\n0.22 move 0 -0.22 0\n0.22 Neck 8 0 0\n0.22 Head 10 0 0\n0.22 LeftArm -55 -165 0\n0.22 LeftForeArm -45 -175 0\n0.22 RightArm -55 165 0\n0.22 RightForeArm -48 178 0\n0.22 LeftUpLeg -48 0 0\n0.22 RightUpLeg -50 0 0\n0.22 LeftLeg -128 0 0\n0.22 RightLeg -126 0 0\n0.22 LeftFoot -22 0 0\n0.22 RightFoot -22 0 0\n0.34 body 8 0 0\n0.34 move 0 -0.12 0\n0.34 Neck 2 0 0\n0.34 Head 2 0 0\n0.34 LeftArm -72 -40 0\n0.34 LeftForeArm -55 -10 0\n0.34 RightArm -70 35 0\n0.34 RightForeArm -52 8 0\n0.34 LeftUpLeg -62 0 0\n0.34 RightUpLeg -64 0 0\n0.34 LeftLeg -112 0 0\n0.34 RightLeg -110 0 0\n0.34 LeftFoot -40 0 0\n0.34 RightFoot -40 0 0\n0.46 body -4 0 0\n0.46 move 0 0.03 0\n0.46 Neck -8 0 0\n0.46 Head -10 0 0\n0.46 LeftArm 22 -26 0\n0.46 LeftForeArm 38 -10 0\n0.46 RightArm 38 18 0\n0.46 RightForeArm 56 6 0\n0.46 LeftUpLeg -96 0 0\n0.46 RightUpLeg -94 0 0\n0.46 LeftLeg -98 0 0\n0.46 RightLeg -97 0 0\n0.46 LeftFoot -75 0 0\n0.46 RightFoot -75 0 0\n0.7 body 2 0 0\n0.7 move 0 0 0\n0.7 Neck -6 0 0\n0.7 Head -8 0 0\n0.7 LeftArm 2 -46 0\n0.7 LeftForeArm 18 -18 0\n0.7 RightArm 14 40 0\n0.7 RightForeArm 32 14 0\n0.7 LeftUpLeg -62 0 0\n0.7 RightUpLeg -68 0 0\n0.7 LeftLeg -112 0 0\n0.7 RightLeg -106 0 0\n0.7 LeftFoot -58 0 0\n0.7 RightFoot -58 0 0\n1 body 5 0 0\n1 move 0 0 0\n1 Neck -5 0 0\n1 Head -5 0 0\n1 LeftArm -24 -74 0\n1 LeftForeArm -8 -38 0\n1 RightArm -18 70 0\n1 RightForeArm -2 34 0\n1 LeftUpLeg -72 0 0\n1 RightUpLeg -78 0 0\n1 LeftLeg -100 0 0\n1 RightLeg -98 0 0\n1 LeftFoot -50 0 0\n1 RightFoot -50 0 0\n1.6 body 6 0 0\n1.6 move 0 0 0\n1.6 Neck -4.6 0 0\n1.6 Head -3.2 0 0\n1.6 LeftArm -40 -93 0\n1.6 LeftForeArm -30 -33 0\n1.6 RightArm -39 93 0\n1.6 RightForeArm -25 27 0\n1.6 LeftUpLeg -81 0 0\n1.6 RightUpLeg -81 0 0\n1.6 LeftLeg -96 0 0\n1.6 RightLeg -96 0 0\n1.6 LeftFoot -48 0 0\n1.6 RightFoot -48 0 0\n",
            "clip wings_takeoff_c\nlength 1.6\nloop 0\npivot 1.1\nevent 0.46 sfx_raven_flap 1.4\nevent 0.95 sfx_raven_flap 0.7\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n0 move 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 LeftUpLeg -61 16 0\n0 RightUpLeg -73 105 0\n0 LeftLeg -81 -70 0\n0 RightLeg -60 -142 0\n0 LeftFoot -30 0 0\n0 RightFoot -30 0 0\n0.22 body 16 0 0\n0.22 move 0 -0.22 0\n0.22 Neck 8 0 0\n0.22 Head 10 0 0\n0.22 LeftArm -55 -165 0\n0.22 LeftForeArm -45 -175 0\n0.22 RightArm -55 165 0\n0.22 RightForeArm -48 178 0\n0.22 LeftUpLeg -48 0 0\n0.22 RightUpLeg -50 0 0\n0.22 LeftLeg -128 0 0\n0.22 RightLeg -126 0 0\n0.22 LeftFoot -22 0 0\n0.22 RightFoot -22 0 0\n0.34 body 8 0 0\n0.34 move 0 -0.12 0\n0.34 Neck 2 0 0\n0.34 Head 2 0 0\n0.34 LeftArm -72 -40 0\n0.34 LeftForeArm -55 -10 0\n0.34 RightArm -70 35 0\n0.34 RightForeArm -52 8 0\n0.34 LeftUpLeg -62 0 0\n0.34 RightUpLeg -64 0 0\n0.34 LeftLeg -112 0 0\n0.34 RightLeg -110 0 0\n0.34 LeftFoot -40 0 0\n0.34 RightFoot -40 0 0\n0.46 body -4 0 0\n0.46 move 0 0.03 0\n0.46 Neck -8 0 0\n0.46 Head -10 0 0\n0.46 LeftArm 38 -18 0\n0.46 LeftForeArm 56 -6 0\n0.46 RightArm 22 26 0\n0.46 RightForeArm 38 10 0\n0.46 LeftUpLeg -96 0 0\n0.46 RightUpLeg -94 0 0\n0.46 LeftLeg -98 0 0\n0.46 RightLeg -97 0 0\n0.46 LeftFoot -75 0 0\n0.46 RightFoot -75 0 0\n0.7 body 2 0 0\n0.7 move 0 0 0\n0.7 Neck -6 0 0\n0.7 Head -8 0 0\n0.7 LeftArm 14 -40 0\n0.7 LeftForeArm 32 -14 0\n0.7 RightArm 2 46 0\n0.7 RightForeArm 18 18 0\n0.7 LeftUpLeg -62 0 0\n0.7 RightUpLeg -68 0 0\n0.7 LeftLeg -112 0 0\n0.7 RightLeg -106 0 0\n0.7 LeftFoot -58 0 0\n0.7 RightFoot -58 0 0\n1 body 5 0 0\n1 move 0 0 0\n1 Neck -5 0 0\n1 Head -5 0 0\n1 LeftArm -18 -70 0\n1 LeftForeArm -2 -34 0\n1 RightArm -24 74 0\n1 RightForeArm -8 38 0\n1 LeftUpLeg -72 0 0\n1 RightUpLeg -78 0 0\n1 LeftLeg -100 0 0\n1 RightLeg -98 0 0\n1 LeftFoot -50 0 0\n1 RightFoot -50 0 0\n1.6 body 6 0 0\n1.6 move 0 0 0\n1.6 Neck -4.6 0 0\n1.6 Head -3.2 0 0\n1.6 LeftArm -40 -93 0\n1.6 LeftForeArm -30 -33 0\n1.6 RightArm -39 93 0\n1.6 RightForeArm -25 27 0\n1.6 LeftUpLeg -81 0 0\n1.6 RightUpLeg -81 0 0\n1.6 LeftLeg -96 0 0\n1.6 RightLeg -96 0 0\n1.6 LeftFoot -48 0 0\n1.6 RightFoot -48 0 0\n",
            "clip wings_takeoff_d\nlength 1.6\nloop 0\npivot 1.1\nevent 0.46 sfx_raven_flap 1.4\nevent 0.95 sfx_raven_flap 0.7\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n0 move 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 LeftUpLeg -61 16 0\n0 RightUpLeg -73 105 0\n0 LeftLeg -81 -70 0\n0 RightLeg -60 -142 0\n0 LeftFoot -30 0 0\n0 RightFoot -30 0 0\n0.22 body 16 0 0\n0.22 move 0 -0.22 0\n0.22 Neck 8 0 0\n0.22 Head 10 0 0\n0.22 LeftArm -55 -165 0\n0.22 LeftForeArm -45 -175 0\n0.22 RightArm -55 165 0\n0.22 RightForeArm -48 178 0\n0.22 LeftUpLeg -48 0 0\n0.22 RightUpLeg -50 0 0\n0.22 LeftLeg -128 0 0\n0.22 RightLeg -126 0 0\n0.22 LeftFoot -22 0 0\n0.22 RightFoot -22 0 0\n0.34 body 8 0 0\n0.34 move 0 -0.12 0\n0.34 Neck 2 0 0\n0.34 Head 2 0 0\n0.34 LeftArm -72 -40 0\n0.34 LeftForeArm -55 -10 0\n0.34 RightArm -70 35 0\n0.34 RightForeArm -52 8 0\n0.34 LeftUpLeg -62 0 0\n0.34 RightUpLeg -64 0 0\n0.34 LeftLeg -112 0 0\n0.34 RightLeg -110 0 0\n0.34 LeftFoot -40 0 0\n0.34 RightFoot -40 0 0\n0.46 body -4 0 0\n0.46 move 0 0.03 0\n0.46 Neck -8 0 0\n0.46 Head -10 0 0\n0.46 LeftArm 12 -30 0\n0.46 LeftForeArm 30 -12 0\n0.46 RightArm 14 28 0\n0.46 RightForeArm 32 12 0\n0.46 LeftUpLeg -96 0 0\n0.46 RightUpLeg -94 0 0\n0.46 LeftLeg -98 0 0\n0.46 RightLeg -97 0 0\n0.46 LeftFoot -75 0 0\n0.46 RightFoot -75 0 0\n0.7 body 2 0 0\n0.7 move 0 0 0\n0.7 Neck -6 0 0\n0.7 Head -8 0 0\n0.7 LeftArm -8 -50 0\n0.7 LeftForeArm 10 -20 0\n0.7 RightArm -10 52 0\n0.7 RightForeArm 8 22 0\n0.7 LeftUpLeg -62 0 0\n0.7 RightUpLeg -68 0 0\n0.7 LeftLeg -112 0 0\n0.7 RightLeg -106 0 0\n0.7 LeftFoot -58 0 0\n0.7 RightFoot -58 0 0\n1 body 5 0 0\n1 move 0 0 0\n1 Neck -5 0 0\n1 Head -5 0 0\n1 LeftArm -28 -78 0\n1 LeftForeArm -12 -40 0\n1 RightArm -30 80 0\n1 RightForeArm -14 42 0\n1 LeftUpLeg -72 0 0\n1 RightUpLeg -78 0 0\n1 LeftLeg -100 0 0\n1 RightLeg -98 0 0\n1 LeftFoot -50 0 0\n1 RightFoot -50 0 0\n1.6 body 6 0 0\n1.6 move 0 0 0\n1.6 Neck -4.6 0 0\n1.6 Head -3.2 0 0\n1.6 LeftArm -40 -93 0\n1.6 LeftForeArm -30 -33 0\n1.6 RightArm -39 93 0\n1.6 RightForeArm -25 27 0\n1.6 LeftUpLeg -81 0 0\n1.6 RightUpLeg -81 0 0\n1.6 LeftLeg -96 0 0\n1.6 RightLeg -96 0 0\n1.6 LeftFoot -48 0 0\n1.6 RightFoot -48 0 0\n",
            "clip wings_land_b\nlength 0.9\nloop 0\npivot 1.1\nevent 0.05 sfx_raven_flap 0.6\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body -6 0 0\n0 move 0 0 0\n0 Neck -5 0 0\n0 Head -5 0 0\n0 LeftArm -28 -72 0\n0 LeftForeArm -8 -46 0\n0 RightArm -36 62 0\n0 RightForeArm -16 36 0\n0 LeftUpLeg -78 0 0\n0 RightUpLeg -78 0 0\n0 LeftLeg -102 0 0\n0 RightLeg -102 0 0\n0 LeftFoot -20 0 0\n0 RightFoot -20 0 0\n0.22 body 8 0 0\n0.22 move 0 -0.2 0\n0.22 Neck 6 0 0\n0.22 Head 6 0 0\n0.22 LeftArm -40 -60 0\n0.22 LeftForeArm -18 -32 0\n0.22 RightArm -46 52 0\n0.22 RightForeArm -26 24 0\n0.22 LeftUpLeg -52 0 0\n0.22 RightUpLeg -52 0 0\n0.22 LeftLeg -124 0 0\n0.22 RightLeg -124 0 0\n0.22 LeftFoot -24 0 0\n0.22 RightFoot -24 0 0\n0.5 body 4 0 0\n0.5 move 0 -0.08 0\n0.5 Neck 2 0 0\n0.5 Head 2 0 0\n0.5 LeftArm -55 -50 0\n0.5 LeftForeArm -38 -16 0\n0.5 RightArm -60 46 0\n0.5 RightForeArm -44 12 0\n0.5 LeftUpLeg -76 0 0\n0.5 RightUpLeg -76 0 0\n0.5 LeftLeg -104 0 0\n0.5 RightLeg -104 0 0\n0.5 LeftFoot -28 0 0\n0.5 RightFoot -28 0 0\n0.9 body 0 0 0\n0.9 move 0 0 0\n0.9 Neck 0 0 0\n0.9 Head 0 0 0\n0.9 LeftArm -70 -101 0\n0.9 LeftForeArm -49 1 0\n0.9 RightArm -50 167 0\n0.9 RightForeArm -66 84 0\n0.9 LeftUpLeg -61 16 0\n0.9 RightUpLeg -73 105 0\n0.9 LeftLeg -81 -70 0\n0.9 RightLeg -60 -142 0\n0.9 LeftFoot -30 0 0\n0.9 RightFoot -30 0 0\n",
            "clip wings_takeoff\nlength 1.6\nloop 0\npivot 1.1\nevent 0.46 sfx_raven_flap 1.4\nevent 0.95 sfx_raven_flap 0.7\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n0 move 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 LeftUpLeg -61 16 0\n0 RightUpLeg -73 105 0\n0 LeftLeg -81 -70 0\n0 RightLeg -60 -142 0\n0 LeftFoot -30 0 0\n0 RightFoot -30 0 0\n0.22 body 16 0 0\n0.22 move 0 -0.22 0\n0.22 Neck 8 0 0\n0.22 Head 10 0 0\n0.22 LeftArm -55 -165 0\n0.22 LeftForeArm -45 -175 0\n0.22 RightArm -55 165 0\n0.22 RightForeArm -48 178 0\n0.22 LeftUpLeg -48 0 0\n0.22 RightUpLeg -50 0 0\n0.22 LeftLeg -128 0 0\n0.22 RightLeg -126 0 0\n0.22 LeftFoot -22 0 0\n0.22 RightFoot -22 0 0\n0.34 body 8 0 0\n0.34 move 0 -0.12 0\n0.34 Neck 2 0 0\n0.34 Head 2 0 0\n0.34 LeftArm -72 -40 0\n0.34 LeftForeArm -55 -10 0\n0.34 RightArm -70 35 0\n0.34 RightForeArm -52 8 0\n0.34 LeftUpLeg -62 0 0\n0.34 RightUpLeg -64 0 0\n0.34 LeftLeg -112 0 0\n0.34 RightLeg -110 0 0\n0.34 LeftFoot -40 0 0\n0.34 RightFoot -40 0 0\n0.46 body -4 0 0\n0.46 move 0 0.03 0\n0.46 Neck -8 0 0\n0.46 Head -10 0 0\n0.46 LeftArm 30 -22 0\n0.46 LeftForeArm 48 -8 0\n0.46 RightArm 32 20 0\n0.46 RightForeArm 50 6 0\n0.46 LeftUpLeg -96 0 0\n0.46 RightUpLeg -94 0 0\n0.46 LeftLeg -98 0 0\n0.46 RightLeg -97 0 0\n0.46 LeftFoot -75 0 0\n0.46 RightFoot -75 0 0\n0.7 body 2 0 0\n0.7 move 0 0 0\n0.7 Neck -6 0 0\n0.7 Head -8 0 0\n0.7 LeftArm 10 -40 0\n0.7 LeftForeArm 28 -15 0\n0.7 RightArm 6 44 0\n0.7 RightForeArm 24 18 0\n0.7 LeftUpLeg -62 0 0\n0.7 RightUpLeg -68 0 0\n0.7 LeftLeg -112 0 0\n0.7 RightLeg -106 0 0\n0.7 LeftFoot -58 0 0\n0.7 RightFoot -58 0 0\n1 body 5 0 0\n1 move 0 0 0\n1 Neck -5 0 0\n1 Head -5 0 0\n1 LeftArm -20 -70 0\n1 LeftForeArm -5 -35 0\n1 RightArm -24 74 0\n1 RightForeArm -8 38 0\n1 LeftUpLeg -72 0 0\n1 RightUpLeg -78 0 0\n1 LeftLeg -100 0 0\n1 RightLeg -98 0 0\n1 LeftFoot -50 0 0\n1 RightFoot -50 0 0\n1.6 body 6 0 0\n1.6 move 0 0 0\n1.6 Neck -4.6 0 0\n1.6 Head -3.2 0 0\n1.6 LeftArm -40 -93 0\n1.6 LeftForeArm -30 -33 0\n1.6 RightArm -39 93 0\n1.6 RightForeArm -25 27 0\n1.6 LeftUpLeg -81 0 0\n1.6 RightUpLeg -81 0 0\n1.6 LeftLeg -96 0 0\n1.6 RightLeg -96 0 0\n1.6 LeftFoot -48 0 0\n1.6 RightFoot -48 0 0\n",
            "clip wings_sink\nlength 3.4\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 3 0 0\n1.7 body 4 0 0\n0 move 0 0 0\n1.7 move 0 0.04 0\n0 Neck 8 0 0\n0 Head 10 0 0\n1.7 Neck 8 0 0\n1.7 Head 10 0 0\n0 LeftArm -28 -94 0\n0 RightArm -28 94 0\n0 LeftForeArm -18 -48 0\n0 RightForeArm -14 48 0\n1.7 LeftArm -33 -95 0\n1.7 RightArm -33 95 0\n1.7 LeftForeArm -23 -49 0\n1.7 RightForeArm -19 49 0\n0 LeftUpLeg -84 0 0\n0 RightUpLeg -70 0 0\n0 LeftLeg -96 0 0\n0 RightLeg -104 0 0\n0 LeftFoot -55 0 0\n0 RightFoot -50 0 0\n1.7 LeftUpLeg -86 0 0\n1.7 RightUpLeg -72 0 0\n1.7 LeftLeg -98 0 0\n1.7 RightLeg -106 0 0\n1.7 LeftFoot -57 0 0\n1.7 RightFoot -52 0 0\n",
            "clip wings_down\nlength 3\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body 26 0 0\n1.5 body 28 0 0\n0 move 0 0 0\n1.5 move 0 0.03 0\n0 Neck -10 0 0\n0 Head -14 0 0\n1.5 Neck -10 0 0\n1.5 Head -15 0 0\n0 LeftArm -38 -106 0\n0 RightArm -38 106 0\n0 LeftForeArm -20 -68 0\n0 RightForeArm -16 62 0\n1.5 LeftArm -35 -104 0\n1.5 RightArm -35 104 0\n1.5 LeftForeArm -17 -66 0\n1.5 RightForeArm -13 60 0\n0 LeftUpLeg -82 -178 0\n0 LeftLeg -80 -178 0\n0 LeftFoot -70 -178 0\n0 RightUpLeg -52 0 0\n0 RightLeg -74 178 0\n0 RightFoot -60 178 0\n1.5 LeftUpLeg -85 -178 0\n1.5 LeftLeg -80 -178 0\n1.5 LeftFoot -70 -178 0\n1.5 RightUpLeg -55 0 0\n1.5 RightLeg -77 178 0\n1.5 RightFoot -60 178 0\n",
            "clip wings_land\nlength 0.9\nloop 0\npivot 1.1\nevent 0.05 sfx_raven_flap 0.6\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body -6 0 0\n0 move 0 0 0\n0 Neck -5 0 0\n0 Head -5 0 0\n0 LeftArm -34 -66 0\n0 LeftForeArm -14 -40 0\n0 RightArm -30 70 0\n0 RightForeArm -10 44 0\n0 LeftUpLeg -78 0 0\n0 RightUpLeg -78 0 0\n0 LeftLeg -102 0 0\n0 RightLeg -102 0 0\n0 LeftFoot -20 0 0\n0 RightFoot -20 0 0\n0.22 body 8 0 0\n0.22 move 0 -0.2 0\n0.22 Neck 6 0 0\n0.22 Head 6 0 0\n0.22 LeftArm -44 -56 0\n0.22 LeftForeArm -24 -28 0\n0.22 RightArm -42 60 0\n0.22 RightForeArm -20 32 0\n0.22 LeftUpLeg -52 0 0\n0.22 RightUpLeg -52 0 0\n0.22 LeftLeg -124 0 0\n0.22 RightLeg -124 0 0\n0.22 LeftFoot -24 0 0\n0.22 RightFoot -24 0 0\n0.5 body 4 0 0\n0.5 move 0 -0.08 0\n0.5 Neck 2 0 0\n0.5 Head 2 0 0\n0.5 LeftArm -58 -48 0\n0.5 LeftForeArm -42 -14 0\n0.5 RightArm -55 52 0\n0.5 RightForeArm -40 16 0\n0.5 LeftUpLeg -76 0 0\n0.5 RightUpLeg -76 0 0\n0.5 LeftLeg -104 0 0\n0.5 RightLeg -104 0 0\n0.5 LeftFoot -28 0 0\n0.5 RightFoot -28 0 0\n0.9 body 0 0 0\n0.9 move 0 0 0\n0.9 Neck 0 0 0\n0.9 Head 0 0 0\n0.9 LeftArm -70 -101 0\n0.9 LeftForeArm -49 1 0\n0.9 RightArm -50 167 0\n0.9 RightForeArm -66 84 0\n0.9 LeftUpLeg -61 16 0\n0.9 RightUpLeg -73 105 0\n0.9 LeftLeg -81 -70 0\n0.9 RightLeg -60 -142 0\n0.9 LeftFoot -30 0 0\n0.9 RightFoot -30 0 0\n",
            "clip wings_flare\nlength 2.4\nloop 1\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode LeftUpLeg aim\nmode RightUpLeg aim\nmode LeftLeg aim\nmode RightLeg aim\nmode LeftFoot aim\nmode RightFoot aim\nmode Neck body\nmode Head body\n0 body -3 0 0\n0 move 0 0 0\n0 Neck -4 0 0\n0 Head -4 0 0\n0 LeftArm -36 -74 0\n0 RightArm -32 78 0\n0 LeftForeArm -16 -48 0\n0 RightForeArm -12 52 0\n0 LeftUpLeg -78 0 0\n0 RightUpLeg -74 0 0\n0 LeftLeg -98 0 0\n0 RightLeg -102 0 0\n0 LeftFoot -40 0 0\n0 RightFoot -40 0 0\n1.2 body -4 0 0\n1.2 move 0 0.03 0\n1.2 Neck -5 0 0\n1.2 Head -5 0 0\n1.2 LeftArm -32 -78 0\n1.2 RightArm -36 74 0\n1.2 LeftForeArm -12 -50 0\n1.2 RightForeArm -16 48 0\n1.2 LeftUpLeg -76 0 0\n1.2 RightUpLeg -77 0 0\n1.2 LeftLeg -100 0 0\n1.2 RightLeg -99 0 0\n1.2 LeftFoot -42 0 0\n1.2 RightFoot -38 0 0\n",
            "clip wings_beat\nlength 1.6\nloop 0\npivot 1.1\nevent 0.5 sfx_raven_flap 0.5\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\n0 body 0 0 0\n0.6 body -4 0 0\n1.6 body 0 0 0\n0 move 0 0 0\n0.6 move 0 0.08 0\n1.6 move 0 0 0\n0 LeftArm -46 -99 0\n0 RightArm -46 99 0\n0 LeftForeArm -28 -61 0\n0 RightForeArm -24 55 0\n0.4 LeftArm -28 -96 0\n0.4 RightArm -28 96 0\n0.4 LeftForeArm -10 -58 0\n0.4 RightForeArm -6 52 0\n0.8 LeftArm -54 -101 0\n0.8 RightArm -54 101 0\n0.8 LeftForeArm -36 -63 0\n0.8 RightForeArm -32 57 0\n1.6 LeftArm -46 -99 0\n1.6 RightArm -46 99 0\n1.6 LeftForeArm -28 -61 0\n1.6 RightForeArm -24 55 0\n",
            "clip wings_dip\nlength 1.8\nloop 0\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\n0 body 0 0 0\n0.6 body -5 0 0\n1.8 body 0 0 0\n0 move 0 0 0\n0.6 move 0 -0.25 0\n1.2 move 0 0.04 0\n1.8 move 0 0 0\n0 LeftArm -46 -99 0\n0 RightArm -46 99 0\n0 LeftForeArm -28 -61 0\n0 RightForeArm -24 55 0\n0.6 LeftArm -34 -95 0\n0.6 RightArm -34 95 0\n0.6 LeftForeArm -16 -57 0\n0.6 RightForeArm -12 51 0\n1.8 LeftArm -46 -99 0\n1.8 RightArm -46 99 0\n1.8 LeftForeArm -28 -61 0\n1.8 RightForeArm -24 55 0\n",
            "clip wings_stretch\nlength 2.4\nloop 0\npivot 1.1\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 body 0 0 0\n1.0 body -3 0 0\n2.4 body 0 0 0\n0 Neck 0 0 0\n0 Head 0 0 0\n1 Neck -6 0 0\n1 Head -8 0 0\n2.4 Neck 0 0 0\n2.4 Head 0 0 0\n0 LeftArm -46 -99 0\n0 RightArm -46 99 0\n0 LeftForeArm -28 -61 0\n0 RightForeArm -24 55 0\n1 LeftArm -36 -90 0\n1 RightArm -36 90 0\n1 LeftForeArm -18 -52 0\n1 RightForeArm -14 46 0\n1.6 LeftArm -36 -90 0\n1.6 RightArm -36 90 0\n1.6 LeftForeArm -18 -52 0\n1.6 RightForeArm -14 46 0\n2.4 LeftArm -46 -99 0\n2.4 RightArm -46 99 0\n2.4 LeftForeArm -28 -61 0\n2.4 RightForeArm -24 55 0\n",
            // the Fate stone raised to the raven on that shoulder (FateRite; tuned by eye: the test window, tab «Персонаж»)
            "clip shoulder_meet_r\nlength 1.3\nloop 0\npivot 1.1\nmode RightShoulder body\nmode Neck body\nmode Head body\nmode Spine2 body\n0 RightShoulder 0 0 0.0\n0 Neck 0 0.0 0\n0 Head 0.0 0.0 0\n0 Spine2 0 0 0.0\n0.18 RightShoulder 0 0 -14.0\n0.18 Neck 0 5.0 0\n0.18 Head 2.4 28.0 0\n0.18 Spine2 0 0 -1.5\n0.38 RightShoulder 0 0 -7.7\n0.38 Neck 0 8.0 0\n0.38 Head 3.6 28.0 0\n0.38 Spine2 0 0 -3.0\n0.7 RightShoulder 0 0 -4.9\n0.7 Neck 0 7.0 0\n0.7 Head 2.4 23.8 0\n0.7 Spine2 0 0 -1.8\n1.3 RightShoulder 0 0 0.0\n1.3 Neck 0 0.0 0\n1.3 Head 0.0 0.0 0\n1.3 Spine2 0 0 0.0\n",
            "clip shoulder_meet_l\nlength 1.3\nloop 0\npivot 1.1\nmode LeftShoulder body\nmode Neck body\nmode Head body\nmode Spine2 body\n0 LeftShoulder 0 0 0.0\n0 Neck 0 0.0 0\n0 Head 0.0 0.0 0\n0 Spine2 0 0 0.0\n0.18 LeftShoulder 0 0 14.0\n0.18 Neck 0 -5.0 0\n0.18 Head 2.4 -28.0 0\n0.18 Spine2 0 0 1.5\n0.38 LeftShoulder 0 0 7.7\n0.38 Neck 0 -8.0 0\n0.38 Head 3.6 -28.0 0\n0.38 Spine2 0 0 3.0\n0.7 LeftShoulder 0 0 4.9\n0.7 Neck 0 -7.0 0\n0.7 Head 2.4 -23.8 0\n0.7 Spine2 0 0 1.8\n1.3 LeftShoulder 0 0 0.0\n1.3 Neck 0 0.0 0\n1.3 Head 0.0 0.0 0\n1.3 Spine2 0 0 0.0\n",
            "clip feed_r\nlength 2.3\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 1 2 1\n0.28 Spine2 2 3 2\n0.28 RightShoulder -1 0 -3.8\n0.28 RightArm -60.8 53.2 0\n0.28 RightForeArm -32.9 36.4 0\n0.28 Neck 0.9 3.7 0\n0.28 Head 3.3 8.3 0\n0.3 Spine1 1 2 1\n0.3 Spine2 2 3 2\n0.3 RightShoulder -1 0 -4\n0.3 RightArm -61.3 47 0\n0.3 RightForeArm -30.5 33 0\n0.3 Neck 1 3.8 0\n0.3 Head 3.5 8.9 0\n0.32 Spine1 1.1 2.1 1.1\n0.32 Spine2 2 3 2\n0.32 RightShoulder -1 0 -4\n0.32 RightArm -61.8 42.6 0\n0.32 RightForeArm -28.5 30 0\n0.32 Neck 1 4 0\n0.32 Head 3.7 9.3 0\n0.34 Spine1 1.1 2.1 1.1\n0.34 Spine2 2.1 3.1 2.1\n0.34 RightShoulder -1 0 -4.1\n0.34 RightArm -62 40.3 0\n0.34 RightForeArm -26.8 27.7 0\n0.34 Neck 1 4 0\n0.34 Head 3.9 9.7 0\n0.35 Spine1 1.1 2.1 1.1\n0.35 Spine2 2.1 3.1 2.1\n0.35 RightShoulder -1 0 -4.1\n0.35 RightArm -62 40 0\n0.35 RightForeArm -26.2 26.7 0\n0.35 Neck 1 4 0\n0.35 Head 3.9 9.8 0\n0.38 Spine1 1.3 2.3 1.3\n0.38 Spine2 2.2 3.3 2.3\n0.38 RightShoulder -1.1 0 -4.4\n0.38 RightArm -61.4 40.3 0\n0.38 RightForeArm -25.1 25.1 0\n0.38 Neck 1.1 4.2 0\n0.38 Head 4 10 0\n0.39 Spine1 1.3 2.3 1.3\n0.39 Spine2 2.2 3.4 2.4\n0.39 RightShoulder -1.1 0 -4.6\n0.39 RightArm -61 40.6 0\n0.39 RightForeArm -25 25 0\n0.39 Neck 1.1 4.2 0\n0.39 Head 4 10 0\n0.7 Spine1 3 4 3\n0.7 Spine2 4 6 5\n0.7 RightShoulder -2 0 -11.8\n0.7 RightArm -25 60.4 0\n0.7 RightForeArm 47.2 19.2 0\n0.7 Neck 3.8 9.7 0\n0.7 Head 10 22 0\n0.72 Spine1 3 4 3\n0.72 Spine2 4 6 5\n0.72 RightShoulder -2 0 -11.9\n0.72 RightArm -23.6 61.1 0\n0.72 RightForeArm 51.7 18.8 0\n0.72 Neck 3.9 9.8 0\n0.72 Head 10.3 22.7 0\n0.74 Spine1 3 4 3\n0.74 Spine2 4 6 5\n0.74 RightShoulder -2 0 -12\n0.74 RightArm -22.6 61.7 0\n0.74 RightForeArm 55.6 18.5 0\n0.74 Neck 4 10 0\n0.74 Head 10.6 23.2 0\n0.76 Spine1 3 4 3\n0.76 Spine2 4 6 5\n0.76 RightShoulder -2 0 -12\n0.76 RightArm -22.1 62 0\n0.76 RightForeArm 58.6 18.3 0\n0.76 Neck 4 10 0\n0.76 Head 10.8 23.6 0\n0.77 Spine1 3 4 3\n0.77 Spine2 4 6 5\n0.77 RightShoulder -2 0 -12\n0.77 RightArm -22 62 0\n0.77 RightForeArm 59.8 18.2 0\n0.77 Neck 4 10 0\n0.77 Head 10.9 23.8 0\n0.8 Spine1 3 4 3\n0.8 Spine2 4 6 5\n0.8 RightShoulder -2 0 -12\n0.8 RightArm -22 62 0\n0.8 RightForeArm 61.9 18 0\n0.8 Neck 4 10 0\n0.8 Head 11 24 0\n0.81 Spine1 3 4 3\n0.81 Spine2 4 6 5\n0.81 RightShoulder -2 0 -12\n0.81 RightArm -22 62 0\n0.81 RightForeArm 62 18 0\n0.81 Neck 4 10 0\n0.81 Head 11 24 0\n1.4 Spine1 3 4 3\n1.4 Spine2 4 6 5\n1.4 RightShoulder -2 0 -12\n1.4 RightArm -23.9 62 0\n1.4 RightForeArm 60.1 18 0\n1.4 Neck 4 10 0\n1.4 Head 11 24 0\n1.42 Spine1 3 4 3\n1.42 Spine2 4 6 5\n1.42 RightShoulder -2 0 -12\n1.42 RightArm -24 62 0\n1.42 RightForeArm 60.1 18 0\n1.42 Neck 4 10 0\n1.42 Head 11 24 0\n1.44 Spine1 3 4 3\n1.44 Spine2 4 6 5\n1.44 RightShoulder -2 0 -12\n1.44 RightArm -24 62 0\n1.44 RightForeArm 60.1 18 0\n1.44 Neck 4 10 0\n1.44 Head 11 24 0\n1.46 Spine1 2.9 3.9 2.9\n1.46 Spine2 4 5.9 4.9\n1.46 RightShoulder -2 0 -12\n1.46 RightArm -24 62 0\n1.46 RightForeArm 60 18 0\n1.46 Neck 4 10 0\n1.46 Head 11 24 0\n1.47 Spine1 2.9 3.9 2.9\n1.47 Spine2 3.9 5.9 4.9\n1.47 RightShoulder -2 0 -11.9\n1.47 RightArm -24 62 0\n1.47 RightForeArm 60 18 0\n1.47 Neck 4 10 0\n1.47 Head 11 24 0\n1.5 Spine1 2.7 3.7 2.7\n1.5 Spine2 3.8 5.7 4.7\n1.5 RightShoulder -2 0 -11.6\n1.5 RightArm -24.5 61.7 0\n1.5 RightForeArm 60 18 0\n1.5 Neck 3.9 9.9 0\n1.5 Head 11 24 0\n1.51 Spine1 2.7 3.7 2.7\n1.51 Spine2 3.8 5.7 4.7\n1.51 RightShoulder -1.9 0 -11.5\n1.51 RightArm -24.8 61.5 0\n1.51 RightForeArm 60 18 0\n1.51 Neck 3.9 9.8 0\n1.51 Head 11 24 0\n1.85 Spine1 1 2 1\n1.85 Spine2 2 3 2\n1.85 RightShoulder -1 0 -4.2\n1.85 RightArm -59.5 41.4 0\n1.85 RightForeArm -12.2 23.9 0\n1.85 Neck 1.1 4.3 0\n1.85 Head 4.9 10 0\n1.87 Spine1 1 2 1\n1.87 Spine2 2 3 2\n1.87 RightShoulder -1 0 -4\n1.87 RightArm -60.7 40.8 0\n1.87 RightForeArm -16.2 24.3 0\n1.87 Neck 1.1 4.1 0\n1.87 Head 4.6 9.3 0\n1.89 Spine1 1 2 1\n1.89 Spine2 2 3 2\n1.89 RightShoulder -1 0 -4\n1.89 RightArm -61.5 40.3 0\n1.89 RightForeArm -19.5 24.5 0\n1.89 Neck 1 4 0\n1.89 Head 4.3 8.8 0\n1.91 Spine1 1 1.9 1\n1.91 Spine2 2 2.9 2\n1.91 RightShoulder -1 0 -4\n1.91 RightArm -61.9 40 0\n1.91 RightForeArm -22.1 24.8 0\n1.91 Neck 1 4 0\n1.91 Head 4.2 8.4 0\n1.92 Spine1 0.9 1.9 0.9\n1.92 Spine2 1.9 2.9 1.9\n1.92 RightShoulder -1 0 -3.9\n1.92 RightArm -62 40 0\n1.92 RightForeArm -23.1 24.8 0\n1.92 Neck 1 4 0\n1.92 Head 4.1 8.2 0\n1.95 Spine1 0.9 1.7 0.9\n1.95 Spine2 1.8 2.7 1.8\n1.95 RightShoulder -0.9 0 -3.8\n1.95 RightArm -61.8 42.2 0\n1.95 RightForeArm -24.9 25 0\n1.95 Neck 1 3.9 0\n1.95 Head 4 8 0\n1.96 Spine1 0.8 1.7 0.8\n1.96 Spine2 1.8 2.7 1.8\n1.96 RightShoulder -0.9 0 -3.7\n1.96 RightArm -61.6 43.9 0\n1.96 RightForeArm -25 25 0\n1.96 Neck 1 3.8 0\n1.96 Head 4 8 0\n2.3 Spine1 0 0 0\n2.3 Spine2 0 0 0\n2.3 RightShoulder 0 0 0\n2.3 RightArm -50 167 0\n2.3 RightForeArm -66 84 0\n2.3 Neck 0 0 0\n2.3 Head 0 0 0\n",
            "clip feed_l\nlength 2.3\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 1 -2 -1\n0.28 Spine2 2 -3 -2\n0.28 LeftShoulder -1 0 3.8\n0.28 LeftArm -62.8 -46.3 0\n0.28 LeftForeArm -29.7 -20 0\n0.28 Neck 0.9 -3.7 0\n0.28 Head 3.3 -8.3 0\n0.3 Spine1 1 -2 -1\n0.3 Spine2 2 -3 -2\n0.3 LeftShoulder -1 0 4\n0.3 LeftArm -62.4 -43.4 0\n0.3 LeftForeArm -28.2 -21.5 0\n0.3 Neck 1 -3.8 0\n0.3 Head 3.5 -8.9 0\n0.32 Spine1 1.1 -2.1 -1.1\n0.32 Spine2 2 -3 -2\n0.32 LeftShoulder -1 0 4\n0.32 LeftArm -62.2 -41.3 0\n0.32 LeftForeArm -27 -22.8 0\n0.32 Neck 1 -4 0\n0.32 Head 3.7 -9.3 0\n0.34 Spine1 1.1 -2.1 -1.1\n0.34 Spine2 2.1 -3.1 -2.1\n0.34 LeftShoulder -1 0 4.1\n0.34 LeftArm -62 -40.1 0\n0.34 LeftForeArm -26.1 -23.8 0\n0.34 Neck 1 -4 0\n0.34 Head 3.9 -9.7 0\n0.35 Spine1 1.1 -2.1 -1.1\n0.35 Spine2 2.1 -3.1 -2.1\n0.35 LeftShoulder -1 0 4.1\n0.35 LeftArm -62 -40 0\n0.35 LeftForeArm -25.7 -24.2 0\n0.35 Neck 1 -4 0\n0.35 Head 3.9 -9.8 0\n0.38 Spine1 1.3 -2.3 -1.3\n0.38 Spine2 2.2 -3.3 -2.3\n0.38 LeftShoulder -1.1 0 4.4\n0.38 LeftArm -61.4 -40.3 0\n0.38 LeftForeArm -25 -24.9 0\n0.38 Neck 1.1 -4.2 0\n0.38 Head 4 -10 0\n0.39 Spine1 1.3 -2.3 -1.3\n0.39 Spine2 2.2 -3.4 -2.4\n0.39 LeftShoulder -1.1 0 4.6\n0.39 LeftArm -61 -40.6 0\n0.39 LeftForeArm -25 -25 0\n0.39 Neck 1.1 -4.2 0\n0.39 Head 4 -10 0\n0.7 Spine1 3 -4 -3\n0.7 Spine2 4 -6 -5\n0.7 LeftShoulder -2 0 11.8\n0.7 LeftArm -25 -60.4 0\n0.7 LeftForeArm 47.2 -19.2 0\n0.7 Neck 3.8 -9.7 0\n0.7 Head 10 -22 0\n0.72 Spine1 3 -4 -3\n0.72 Spine2 4 -6 -5\n0.72 LeftShoulder -2 0 11.9\n0.72 LeftArm -23.6 -61.1 0\n0.72 LeftForeArm 51.7 -18.8 0\n0.72 Neck 3.9 -9.8 0\n0.72 Head 10.3 -22.7 0\n0.74 Spine1 3 -4 -3\n0.74 Spine2 4 -6 -5\n0.74 LeftShoulder -2 0 12\n0.74 LeftArm -22.6 -61.7 0\n0.74 LeftForeArm 55.6 -18.5 0\n0.74 Neck 4 -10 0\n0.74 Head 10.6 -23.2 0\n0.76 Spine1 3 -4 -3\n0.76 Spine2 4 -6 -5\n0.76 LeftShoulder -2 0 12\n0.76 LeftArm -22.1 -62 0\n0.76 LeftForeArm 58.6 -18.3 0\n0.76 Neck 4 -10 0\n0.76 Head 10.8 -23.6 0\n0.77 Spine1 3 -4 -3\n0.77 Spine2 4 -6 -5\n0.77 LeftShoulder -2 0 12\n0.77 LeftArm -22 -62 0\n0.77 LeftForeArm 59.8 -18.2 0\n0.77 Neck 4 -10 0\n0.77 Head 10.9 -23.8 0\n0.8 Spine1 3 -4 -3\n0.8 Spine2 4 -6 -5\n0.8 LeftShoulder -2 0 12\n0.8 LeftArm -22 -62 0\n0.8 LeftForeArm 61.9 -18 0\n0.8 Neck 4 -10 0\n0.8 Head 11 -24 0\n0.81 Spine1 3 -4 -3\n0.81 Spine2 4 -6 -5\n0.81 LeftShoulder -2 0 12\n0.81 LeftArm -22 -62 0\n0.81 LeftForeArm 62 -18 0\n0.81 Neck 4 -10 0\n0.81 Head 11 -24 0\n1.4 Spine1 3 -4 -3\n1.4 Spine2 4 -6 -5\n1.4 LeftShoulder -2 0 12\n1.4 LeftArm -23.9 -62 0\n1.4 LeftForeArm 60.1 -18 0\n1.4 Neck 4 -10 0\n1.4 Head 11 -24 0\n1.42 Spine1 3 -4 -3\n1.42 Spine2 4 -6 -5\n1.42 LeftShoulder -2 0 12\n1.42 LeftArm -24 -62 0\n1.42 LeftForeArm 60.1 -18 0\n1.42 Neck 4 -10 0\n1.42 Head 11 -24 0\n1.44 Spine1 3 -4 -3\n1.44 Spine2 4 -6 -5\n1.44 LeftShoulder -2 0 12\n1.44 LeftArm -24 -62 0\n1.44 LeftForeArm 60.1 -18 0\n1.44 Neck 4 -10 0\n1.44 Head 11 -24 0\n1.46 Spine1 2.9 -3.9 -2.9\n1.46 Spine2 4 -5.9 -4.9\n1.46 LeftShoulder -2 0 12\n1.46 LeftArm -24 -62 0\n1.46 LeftForeArm 60 -18 0\n1.46 Neck 4 -10 0\n1.46 Head 11 -24 0\n1.47 Spine1 2.9 -3.9 -2.9\n1.47 Spine2 3.9 -5.9 -4.9\n1.47 LeftShoulder -2 0 11.9\n1.47 LeftArm -24 -62 0\n1.47 LeftForeArm 60 -18 0\n1.47 Neck 4 -10 0\n1.47 Head 11 -24 0\n1.5 Spine1 2.7 -3.7 -2.7\n1.5 Spine2 3.8 -5.7 -4.7\n1.5 LeftShoulder -2 0 11.6\n1.5 LeftArm -24.5 -61.7 0\n1.5 LeftForeArm 60 -18 0\n1.5 Neck 3.9 -9.9 0\n1.5 Head 11 -24 0\n1.51 Spine1 2.7 -3.7 -2.7\n1.51 Spine2 3.8 -5.7 -4.7\n1.51 LeftShoulder -1.9 0 11.5\n1.51 LeftArm -24.8 -61.5 0\n1.51 LeftForeArm 60 -18 0\n1.51 Neck 3.9 -9.8 0\n1.51 Head 11 -24 0\n1.85 Spine1 1 -2 -1\n1.85 Spine2 2 -3 -2\n1.85 LeftShoulder -1 0 4.2\n1.85 LeftArm -59.5 -41.4 0\n1.85 LeftForeArm -12.2 -23.9 0\n1.85 Neck 1.1 -4.3 0\n1.85 Head 4.9 -10 0\n1.87 Spine1 1 -2 -1\n1.87 Spine2 2 -3 -2\n1.87 LeftShoulder -1 0 4\n1.87 LeftArm -60.7 -40.8 0\n1.87 LeftForeArm -16.2 -24.3 0\n1.87 Neck 1.1 -4.1 0\n1.87 Head 4.6 -9.3 0\n1.89 Spine1 1 -2 -1\n1.89 Spine2 2 -3 -2\n1.89 LeftShoulder -1 0 4\n1.89 LeftArm -61.5 -40.3 0\n1.89 LeftForeArm -19.5 -24.5 0\n1.89 Neck 1 -4 0\n1.89 Head 4.3 -8.8 0\n1.91 Spine1 1 -1.9 -1\n1.91 Spine2 2 -2.9 -2\n1.91 LeftShoulder -1 0 4\n1.91 LeftArm -61.9 -40 0\n1.91 LeftForeArm -22.1 -24.8 0\n1.91 Neck 1 -4 0\n1.91 Head 4.2 -8.4 0\n1.92 Spine1 0.9 -1.9 -0.9\n1.92 Spine2 1.9 -2.9 -1.9\n1.92 LeftShoulder -1 0 3.9\n1.92 LeftArm -62 -40 0\n1.92 LeftForeArm -23.1 -24.8 0\n1.92 Neck 1 -4 0\n1.92 Head 4.1 -8.2 0\n1.95 Spine1 0.9 -1.7 -0.9\n1.95 Spine2 1.8 -2.7 -1.8\n1.95 LeftShoulder -0.9 0 3.8\n1.95 LeftArm -62.1 -41.1 0\n1.95 LeftForeArm -24.9 -25 0\n1.95 Neck 1 -3.9 0\n1.95 Head 4 -8 0\n1.96 Spine1 0.8 -1.7 -0.8\n1.96 Spine2 1.8 -2.7 -1.8\n1.96 LeftShoulder -0.9 0 3.7\n1.96 LeftArm -62.2 -41.9 0\n1.96 LeftForeArm -25 -25 0\n1.96 Neck 1 -3.8 0\n1.96 Head 4 -8 0\n2.3 Spine1 0 0 0\n2.3 Spine2 0 0 0\n2.3 LeftShoulder 0 0 0\n2.3 LeftArm -70 -101 0\n2.3 LeftForeArm -49 1 0\n2.3 Neck 0 0 0\n2.3 Head 0 0 0\n",
            "clip order_send_r\nlength 1.6\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.22 Spine1 1 2 0\n0.22 Spine2 2 3.9 0\n0.22 RightShoulder -2.8 0 -2.8\n0.22 RightArm -58.5 50.1 0\n0.22 RightForeArm -39.3 36.6 0\n0.22 Neck 0 1.8 0\n0.22 Head -1.5 1.5 0\n0.24 Spine1 1 2.1 0\n0.24 Spine2 2 4 0\n0.24 RightShoulder -2.9 0 -2.9\n0.24 RightArm -59.2 40.8 0\n0.24 RightForeArm -36.6 31.7 0\n0.24 Neck 0 1.9 0\n0.24 Head -1.7 1.7 0\n0.26 Spine1 1.1 2.2 0\n0.26 Spine2 2 4.1 0\n0.26 RightShoulder -3 0 -3\n0.26 RightArm -59.7 34.1 0\n0.26 RightForeArm -34.2 27.4 0\n0.26 Neck 0 2 0\n0.26 Head -1.8 1.8 0\n0.28 Spine1 1.3 2.5 0\n0.28 Spine2 2.2 4.3 0\n0.28 RightShoulder -3.1 0 -3.1\n0.28 RightArm -60 30.5 0\n0.28 RightForeArm -32.2 24 0\n0.28 Neck 0 2 0\n0.28 Head -1.9 1.9 0\n0.29 Spine1 1.3 2.7 0\n0.29 Spine2 2.2 4.5 0\n0.29 RightShoulder -3.2 0 -3.1\n0.29 RightArm -60 30 0\n0.29 RightForeArm -31.5 22.6 0\n0.29 Neck 0 2 0\n0.29 Head -2 2 0\n0.32 Spine1 1.7 3.3 0\n0.32 Spine2 2.6 5.2 0\n0.32 RightShoulder -3.6 0 -3.5\n0.32 RightArm -58.6 30 0\n0.32 RightForeArm -30.1 20.2 0\n0.32 Neck -0.1 2.1 0\n0.32 Head -2 2 0\n0.33 Spine1 1.8 3.6 0\n0.33 Spine2 2.7 5.5 0\n0.33 RightShoulder -3.8 0 -3.7\n0.33 RightArm -57.5 30 0\n0.33 RightForeArm -30 20 0\n0.33 Neck -0.1 2.2 0\n0.33 Head -2 2 0\n0.55 Spine1 4 8 0\n0.55 Spine2 6 11.9 0\n0.55 RightShoulder -9.7 0 -8.8\n0.55 RightArm -5.2 30 0\n0.55 RightForeArm 5.6 11.1 0\n0.55 Neck -1.8 4.7 0\n0.55 Head -6.7 5.1 0\n0.57 Spine1 4 8 0\n0.57 Spine2 6 12 0\n0.57 RightShoulder -9.9 0 -8.9\n0.57 RightArm -1.8 30 0\n0.57 RightForeArm 9.2 10.2 0\n0.57 Neck -1.9 4.9 0\n0.57 Head -7.1 5.4 0\n0.59 Spine1 4 7.9 0\n0.59 Spine2 6 12 0\n0.59 RightShoulder -10 0 -9\n0.59 RightArm 0.6 30 0\n0.59 RightForeArm 12.4 9.4 0\n0.59 Neck -2 5 0\n0.59 Head -7.5 5.7 0\n0.61 Spine1 3.9 7.8 0\n0.61 Spine2 6 11.9 0\n0.61 RightShoulder -10 0 -9\n0.61 RightArm 1.8 30 0\n0.61 RightForeArm 15 8.7 0\n0.61 Neck -2 5 0\n0.61 Head -7.8 5.8 0\n0.62 Spine1 3.9 7.8 0\n0.62 Spine2 5.9 11.9 0\n0.62 RightShoulder -10 0 -9\n0.62 RightArm 2 30 0\n0.62 RightForeArm 16.1 8.5 0\n0.62 Neck -2 5 0\n0.62 Head -7.9 5.9 0\n0.65 Spine1 3.8 7.6 0\n0.65 Spine2 5.9 11.7 0\n0.65 RightShoulder -9.9 0 -8.9\n0.65 RightArm 2 30 0\n0.65 RightForeArm 17.9 8 0\n0.65 Neck -2 5 0\n0.65 Head -8 6 0\n0.66 Spine1 3.8 7.5 0\n0.66 Spine2 5.8 11.7 0\n0.66 RightShoulder -9.9 0 -8.9\n0.66 RightArm 1.9 30 0\n0.66 RightForeArm 18 8 0\n0.66 Neck -2 4.9 0\n0.66 Head -8 6 0\n0.9 Spine1 3 6 0\n0.9 Spine2 5 10 0\n0.9 RightShoulder -9 0 -8\n0.9 RightArm 0.2 30 0\n0.9 RightForeArm 16.5 8 0\n0.9 Neck -2 4.1 0\n0.9 Head -7.2 5.2 0\n0.92 Spine1 3 6 0\n0.92 Spine2 5 10 0\n0.92 RightShoulder -9 0 -8\n0.92 RightArm 0.1 30 0\n0.92 RightForeArm 16.3 8 0\n0.92 Neck -2 4 0\n0.92 Head -7.1 5.1 0\n0.94 Spine1 2.9 5.9 0\n0.94 Spine2 5 9.9 0\n0.94 RightShoulder -9 0 -8\n0.94 RightArm 0 30 0\n0.94 RightForeArm 16.2 8 0\n0.94 Neck -2 4 0\n0.94 Head -7.1 5.1 0\n0.96 Spine1 2.8 5.7 0\n0.96 Spine2 4.9 9.8 0\n0.96 RightShoulder -8.9 0 -8\n0.96 RightArm 0 30 0\n0.96 RightForeArm 16.1 8 0\n0.96 Neck -2 4 0\n0.96 Head -7 5 0\n0.97 Spine1 2.8 5.6 0\n0.97 Spine2 4.8 9.7 0\n0.97 RightShoulder -8.9 0 -7.9\n0.97 RightArm 0 30 0\n0.97 RightForeArm 16.1 8 0\n0.97 Neck -2 4 0\n0.97 Head -7 5 0\n1 Spine1 2.6 5.2 0\n1 Spine2 4.6 9.2 0\n1 RightShoulder -8.5 0 -7.6\n1 RightArm -1.2 30 0\n1 RightForeArm 16 8 0\n1 Neck -1.9 3.9 0\n1 Head -7 5 0\n1.01 Spine1 2.5 5.1 0\n1.01 Spine2 4.5 9 0\n1.01 RightShoulder -8.4 0 -7.5\n1.01 RightArm -2.2 30 0\n1.01 RightForeArm 16 8 0\n1.01 Neck -1.9 3.9 0\n1.01 Head -7 5 0\n1.25 Spine1 1 2 0\n1.25 Spine2 2 4.1 0\n1.25 RightShoulder -3.2 0 -3.2\n1.25 RightArm -53.8 30 0\n1.25 RightForeArm -19.2 17.2 0\n1.25 Neck -0.2 2.2 0\n1.25 Head -3 2.6 0\n1.27 Spine1 1 2 0\n1.27 Spine2 2 4 0\n1.27 RightShoulder -3.1 0 -3\n1.27 RightArm -56.7 30 0\n1.27 RightForeArm -22.4 18 0\n1.27 Neck -0.1 2.1 0\n1.27 Head -2.7 2.4 0\n1.29 Spine1 1 1.9 0\n1.29 Spine2 2 4 0\n1.29 RightShoulder -3 0 -3\n1.29 RightArm -58.8 30 0\n1.29 RightForeArm -25.2 18.8 0\n1.29 Neck 0 2 0\n1.29 Head -2.4 2.2 0\n1.31 Spine1 0.9 1.8 0\n1.31 Spine2 1.9 3.8 0\n1.31 RightShoulder -3 0 -3\n1.31 RightArm -59.9 30 0\n1.31 RightForeArm -27.5 19.3 0\n1.31 Neck 0 2 0\n1.31 Head -2.2 2.1 0\n1.32 Spine1 0.9 1.8 0\n1.32 Spine2 1.9 3.8 0\n1.32 RightShoulder -2.9 0 -2.9\n1.32 RightArm -60 30 0\n1.32 RightForeArm -28.3 19.6 0\n1.32 Neck 0 2 0\n1.32 Head -2.1 2.1 0\n1.35 Spine1 0.8 1.6 0\n1.35 Spine2 1.7 3.4 0\n1.35 RightShoulder -2.7 0 -2.7\n1.35 RightArm -59.7 34.4 0\n1.35 RightForeArm -29.9 20 0\n1.35 Neck 0 1.9 0\n1.35 Head -2 2 0\n1.36 Spine1 0.8 1.5 0\n1.36 Spine2 1.6 3.3 0\n1.36 RightShoulder -2.6 0 -2.6\n1.36 RightArm -59.4 37.6 0\n1.36 RightForeArm -30 20 0\n1.36 Neck 0 1.8 0\n1.36 Head -2 2 0\n1.6 Spine1 0 0 0\n1.6 Spine2 0 0 0\n1.6 RightShoulder 0 0 0\n1.6 RightArm -50 167 0\n1.6 RightForeArm -66 84 0\n1.6 Neck 0 0 0\n1.6 Head 0 0 0\n",
            "clip order_send_l\nlength 1.6\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.22 Spine1 1 -2 0\n0.22 Spine2 2 -3.9 0\n0.22 LeftShoulder -2.8 0 2.8\n0.22 LeftArm -61.5 -40.4 0\n0.22 LeftForeArm -34.9 -14.6 0\n0.22 Neck 0 -1.8 0\n0.22 Head -1.5 -1.5 0\n0.24 Spine1 1 -2.1 0\n0.24 Spine2 2 -4 0\n0.24 LeftShoulder -2.9 0 2.9\n0.24 LeftArm -60.8 -35.6 0\n0.24 LeftForeArm -33.5 -16.2 0\n0.24 Neck 0 -1.9 0\n0.24 Head -1.7 -1.7 0\n0.26 Spine1 1.1 -2.2 0\n0.26 Spine2 2 -4.1 0\n0.26 LeftShoulder -3 0 3\n0.26 LeftArm -60.3 -32.1 0\n0.26 LeftForeArm -32.2 -17.6 0\n0.26 Neck 0 -2 0\n0.26 Head -1.8 -1.8 0\n0.28 Spine1 1.3 -2.5 0\n0.28 Spine2 2.2 -4.3 0\n0.28 LeftShoulder -3.1 0 3.1\n0.28 LeftArm -60 -30.2 0\n0.28 LeftForeArm -31.2 -18.7 0\n0.28 Neck 0 -2 0\n0.28 Head -1.9 -1.9 0\n0.29 Spine1 1.3 -2.7 0\n0.29 Spine2 2.2 -4.5 0\n0.29 LeftShoulder -3.2 0 3.1\n0.29 LeftArm -60 -30 0\n0.29 LeftForeArm -30.8 -19.1 0\n0.29 Neck 0 -2 0\n0.29 Head -2 -2 0\n0.32 Spine1 1.7 -3.3 0\n0.32 Spine2 2.6 -5.2 0\n0.32 LeftShoulder -3.6 0 3.5\n0.32 LeftArm -58.6 -30 0\n0.32 LeftForeArm -30.1 -19.9 0\n0.32 Neck -0.1 -2.1 0\n0.32 Head -2 -2 0\n0.33 Spine1 1.8 -3.6 0\n0.33 Spine2 2.7 -5.5 0\n0.33 LeftShoulder -3.8 0 3.7\n0.33 LeftArm -57.5 -30 0\n0.33 LeftForeArm -30 -20 0\n0.33 Neck -0.1 -2.2 0\n0.33 Head -2 -2 0\n0.55 Spine1 4 -8 0\n0.55 Spine2 6 -11.9 0\n0.55 LeftShoulder -9.7 0 8.8\n0.55 LeftArm -5.2 -30 0\n0.55 LeftForeArm 5.6 -11.1 0\n0.55 Neck -1.8 -4.7 0\n0.55 Head -6.7 -5.1 0\n0.57 Spine1 4 -8 0\n0.57 Spine2 6 -12 0\n0.57 LeftShoulder -9.9 0 8.9\n0.57 LeftArm -1.8 -30 0\n0.57 LeftForeArm 9.2 -10.2 0\n0.57 Neck -1.9 -4.9 0\n0.57 Head -7.1 -5.4 0\n0.59 Spine1 4 -7.9 0\n0.59 Spine2 6 -12 0\n0.59 LeftShoulder -10 0 9\n0.59 LeftArm 0.6 -30 0\n0.59 LeftForeArm 12.4 -9.4 0\n0.59 Neck -2 -5 0\n0.59 Head -7.5 -5.7 0\n0.61 Spine1 3.9 -7.8 0\n0.61 Spine2 6 -11.9 0\n0.61 LeftShoulder -10 0 9\n0.61 LeftArm 1.8 -30 0\n0.61 LeftForeArm 15 -8.7 0\n0.61 Neck -2 -5 0\n0.61 Head -7.8 -5.8 0\n0.62 Spine1 3.9 -7.8 0\n0.62 Spine2 5.9 -11.9 0\n0.62 LeftShoulder -10 0 9\n0.62 LeftArm 2 -30 0\n0.62 LeftForeArm 16.1 -8.5 0\n0.62 Neck -2 -5 0\n0.62 Head -7.9 -5.9 0\n0.65 Spine1 3.8 -7.6 0\n0.65 Spine2 5.9 -11.7 0\n0.65 LeftShoulder -9.9 0 8.9\n0.65 LeftArm 2 -30 0\n0.65 LeftForeArm 17.9 -8 0\n0.65 Neck -2 -5 0\n0.65 Head -8 -6 0\n0.66 Spine1 3.8 -7.5 0\n0.66 Spine2 5.8 -11.7 0\n0.66 LeftShoulder -9.9 0 8.9\n0.66 LeftArm 1.9 -30 0\n0.66 LeftForeArm 18 -8 0\n0.66 Neck -2 -4.9 0\n0.66 Head -8 -6 0\n0.9 Spine1 3 -6 0\n0.9 Spine2 5 -10 0\n0.9 LeftShoulder -9 0 8\n0.9 LeftArm 0.2 -30 0\n0.9 LeftForeArm 16.5 -8 0\n0.9 Neck -2 -4.1 0\n0.9 Head -7.2 -5.2 0\n0.92 Spine1 3 -6 0\n0.92 Spine2 5 -10 0\n0.92 LeftShoulder -9 0 8\n0.92 LeftArm 0.1 -30 0\n0.92 LeftForeArm 16.3 -8 0\n0.92 Neck -2 -4 0\n0.92 Head -7.1 -5.1 0\n0.94 Spine1 2.9 -5.9 0\n0.94 Spine2 5 -9.9 0\n0.94 LeftShoulder -9 0 8\n0.94 LeftArm 0 -30 0\n0.94 LeftForeArm 16.2 -8 0\n0.94 Neck -2 -4 0\n0.94 Head -7.1 -5.1 0\n0.96 Spine1 2.8 -5.7 0\n0.96 Spine2 4.9 -9.8 0\n0.96 LeftShoulder -8.9 0 8\n0.96 LeftArm 0 -30 0\n0.96 LeftForeArm 16.1 -8 0\n0.96 Neck -2 -4 0\n0.96 Head -7 -5 0\n0.97 Spine1 2.8 -5.6 0\n0.97 Spine2 4.8 -9.7 0\n0.97 LeftShoulder -8.9 0 7.9\n0.97 LeftArm 0 -30 0\n0.97 LeftForeArm 16.1 -8 0\n0.97 Neck -2 -4 0\n0.97 Head -7 -5 0\n1 Spine1 2.6 -5.2 0\n1 Spine2 4.6 -9.2 0\n1 LeftShoulder -8.5 0 7.6\n1 LeftArm -1.2 -30 0\n1 LeftForeArm 16 -8 0\n1 Neck -1.9 -3.9 0\n1 Head -7 -5 0\n1.01 Spine1 2.5 -5.1 0\n1.01 Spine2 4.5 -9 0\n1.01 LeftShoulder -8.4 0 7.5\n1.01 LeftArm -2.2 -30 0\n1.01 LeftForeArm 16 -8 0\n1.01 Neck -1.9 -3.9 0\n1.01 Head -7 -5 0\n1.25 Spine1 1 -2 0\n1.25 Spine2 2 -4.1 0\n1.25 LeftShoulder -3.2 0 3.2\n1.25 LeftArm -53.8 -30 0\n1.25 LeftForeArm -19.2 -17.2 0\n1.25 Neck -0.2 -2.2 0\n1.25 Head -3 -2.6 0\n1.27 Spine1 1 -2 0\n1.27 Spine2 2 -4 0\n1.27 LeftShoulder -3.1 0 3\n1.27 LeftArm -56.7 -30 0\n1.27 LeftForeArm -22.4 -18 0\n1.27 Neck -0.1 -2.1 0\n1.27 Head -2.7 -2.4 0\n1.29 Spine1 1 -1.9 0\n1.29 Spine2 2 -4 0\n1.29 LeftShoulder -3 0 3\n1.29 LeftArm -58.8 -30 0\n1.29 LeftForeArm -25.2 -18.8 0\n1.29 Neck 0 -2 0\n1.29 Head -2.4 -2.2 0\n1.31 Spine1 0.9 -1.8 0\n1.31 Spine2 1.9 -3.8 0\n1.31 LeftShoulder -3 0 3\n1.31 LeftArm -59.9 -30 0\n1.31 LeftForeArm -27.5 -19.3 0\n1.31 Neck 0 -2 0\n1.31 Head -2.2 -2.1 0\n1.32 Spine1 0.9 -1.8 0\n1.32 Spine2 1.9 -3.8 0\n1.32 LeftShoulder -2.9 0 2.9\n1.32 LeftArm -60 -30 0\n1.32 LeftForeArm -28.3 -19.6 0\n1.32 Neck 0 -2 0\n1.32 Head -2.1 -2.1 0\n1.35 Spine1 0.8 -1.6 0\n1.35 Spine2 1.7 -3.4 0\n1.35 LeftShoulder -2.7 0 2.7\n1.35 LeftArm -60.3 -32.3 0\n1.35 LeftForeArm -29.9 -20 0\n1.35 Neck 0 -1.9 0\n1.35 Head -2 -2 0\n1.36 Spine1 0.8 -1.5 0\n1.36 Spine2 1.6 -3.3 0\n1.36 LeftShoulder -2.6 0 2.6\n1.36 LeftArm -60.6 -33.9 0\n1.36 LeftForeArm -30 -20 0\n1.36 Neck 0 -1.8 0\n1.36 Head -2 -2 0\n1.6 Spine1 0 0 0\n1.6 Spine2 0 0 0\n1.6 LeftShoulder 0 0 0\n1.6 LeftArm -70 -101 0\n1.6 LeftForeArm -49 1 0\n1.6 Neck 0 0 0\n1.6 Head 0 0 0\n",
            "clip call_r\nlength 1.9\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.25 Spine1 1 1 1\n0.25 Spine2 1 2 1\n0.25 RightShoulder -1.9 0 -3.8\n0.25 RightArm -57 55.6 0\n0.25 RightForeArm -34.1 42 0\n0.25 Neck 0 1.8 0\n0.25 Head -1.6 2.4 0\n0.27 Spine1 1 1 1\n0.27 Spine2 1 2 1\n0.27 RightShoulder -2 0 -3.9\n0.27 RightArm -57.5 48.3 0\n0.27 RightForeArm -31.4 38.4 0\n0.27 Neck 0 1.9 0\n0.27 Head -1.7 2.6 0\n0.29 Spine1 1 1.1 1\n0.29 Spine2 1 2 1\n0.29 RightShoulder -2 0 -4\n0.29 RightArm -57.8 43.1 0\n0.29 RightForeArm -29 35.3 0\n0.29 Neck 0 2 0\n0.29 Head -1.8 2.8 0\n0.31 Spine1 1.1 1.2 1\n0.31 Spine2 1.1 2.1 1\n0.31 RightShoulder -2 0 -4\n0.31 RightArm -58 40.4 0\n0.31 RightForeArm -27.2 32.8 0\n0.31 Neck 0 2 0\n0.31 Head -1.9 2.9 0\n0.32 Spine1 1.1 1.2 1\n0.32 Spine2 1.1 2.1 1.1\n0.32 RightShoulder -2.1 0 -4.1\n0.32 RightArm -58 40 0\n0.32 RightForeArm -26.4 31.9 0\n0.32 Neck 0 2 0\n0.32 Head -2 2.9 0\n0.35 Spine1 1.2 1.4 1\n0.35 Spine2 1.3 2.3 1.1\n0.35 RightShoulder -2.3 0 -4.3\n0.35 RightArm -57.4 40.1 0\n0.35 RightForeArm -25.1 30.1 0\n0.35 Neck 0 2 0\n0.35 Head -2 3 0\n0.36 Spine1 1.2 1.5 1\n0.36 Spine2 1.3 2.3 1.2\n0.36 RightShoulder -2.4 0 -4.4\n0.36 RightArm -57 40.2 0\n0.36 RightForeArm -25 30 0\n0.36 Neck -0.1 2.1 0\n0.36 Head -2 3 0\n0.6 Spine1 2 3 1\n0.6 Spine2 3 4 2\n0.6 RightShoulder -5.9 0 -7.9\n0.6 RightArm -32.9 44.5 0\n0.6 RightForeArm 17.1 37.7 0\n0.6 Neck -0.9 2.9 0\n0.6 Head -4.4 4.6 0\n0.62 Spine1 2 3 1\n0.62 Spine2 3 4 2\n0.62 RightShoulder -6 0 -8\n0.62 RightArm -31.6 44.7 0\n0.62 RightForeArm 21 38.4 0\n0.62 Neck -1 3 0\n0.62 Head -4.6 4.7 0\n0.64 Spine1 2 3 1\n0.64 Spine2 3 4 2\n0.64 RightShoulder -6 0 -8\n0.64 RightArm -30.6 44.9 0\n0.64 RightForeArm 24.3 39 0\n0.64 Neck -1 3 0\n0.64 Head -4.8 4.8 0\n0.66 Spine1 1.9 2.9 1\n0.66 Spine2 2.9 4 2\n0.66 RightShoulder -6 0 -8\n0.66 RightArm -30.1 45 0\n0.66 RightForeArm 27 39.4 0\n0.66 Neck -1 3 0\n0.66 Head -4.9 4.9 0\n0.67 Spine1 1.9 2.9 1\n0.67 Spine2 2.9 4 2\n0.67 RightShoulder -6 0 -8\n0.67 RightArm -30 45 0\n0.67 RightForeArm 28 39.6 0\n0.67 Neck -1 3 0\n0.67 Head -4.9 5 0\n0.7 Spine1 1.8 2.8 1\n0.7 Spine2 2.8 3.9 2\n0.7 RightShoulder -5.8 0 -7.9\n0.7 RightArm -30.1 44.9 0\n0.7 RightForeArm 29.9 40 0\n0.7 Neck -1 3 0\n0.7 Head -5 5 0\n0.71 Spine1 1.8 2.8 1\n0.71 Spine2 2.7 3.9 2\n0.71 RightShoulder -5.8 0 -7.8\n0.71 RightArm -30.1 44.8 0\n0.71 RightForeArm 30 40 0\n0.71 Neck -1 3 0\n0.71 Head -5 5 0\n1 Spine1 1 2 1\n1 Spine2 1 3 2\n1 RightShoulder -3.1 0 -6.1\n1 RightArm -33.7 38.6 0\n1 RightForeArm 38.1 -13 0\n1 Neck -1 3 0\n1 Head -5 5 0\n1.02 Spine1 1 2 1\n1.02 Spine2 1 3 2\n1.02 RightShoulder -3 0 -6\n1.02 RightArm -33.8 38.3 0\n1.02 RightForeArm 38.7 -16.6 0\n1.02 Neck -1 3 0\n1.02 Head -5 5 0\n1.04 Spine1 1 2 1\n1.04 Spine2 1 3 2\n1.04 RightShoulder -3 0 -6\n1.04 RightArm -33.9 38.1 0\n1.04 RightForeArm 39.2 -19.7 0\n1.04 Neck -1 3 0\n1.04 Head -5 5 0\n1.06 Spine1 1 2 1\n1.06 Spine2 1 3 2\n1.06 RightShoulder -3 0 -6\n1.06 RightArm -34 38 0\n1.06 RightForeArm 39.6 -22.2 0\n1.06 Neck -1 3 0\n1.06 Head -5 5 0\n1.07 Spine1 0.9 1.9 1\n1.07 Spine2 1 3 2\n1.07 RightShoulder -3 0 -6\n1.07 RightArm -34 38 0\n1.07 RightForeArm 39.7 -23.2 0\n1.07 Neck -1 3 0\n1.07 Head -5 5 0\n1.1 Spine1 0.9 1.9 1\n1.1 Spine2 0.9 2.9 1.9\n1.1 RightShoulder -2.9 0 -5.9\n1.1 RightArm -34.3 38 0\n1.1 RightForeArm 40 -24.9 0\n1.1 Neck -1 3 0\n1.1 Head -5 5 0\n1.11 Spine1 0.8 1.8 1\n1.11 Spine2 0.9 2.9 1.9\n1.11 RightShoulder -2.9 0 -5.8\n1.11 RightArm -34.5 38 0\n1.11 RightForeArm 40 -25 0\n1.11 Neck -1 3 0\n1.11 Head -5 5 0\n1.45 Spine1 0 1 1\n1.45 Spine2 0 2 1\n1.45 RightShoulder -1 0 -3.1\n1.45 RightArm -56.4 39.9 0\n1.45 RightForeArm -15.2 13.2 0\n1.45 Neck 0 2 0\n1.45 Head -2.4 3.3 0\n1.47 Spine1 0 1 1\n1.47 Spine2 0 2 1\n1.47 RightShoulder -1 0 -3\n1.47 RightArm -57.2 39.9 0\n1.47 RightForeArm -18.2 15.3 0\n1.47 Neck 0 2 0\n1.47 Head -2.3 3.2 0\n1.49 Spine1 0 1 1\n1.49 Spine2 0 2 1\n1.49 RightShoulder -1 0 -3\n1.49 RightArm -57.7 40 0\n1.49 RightForeArm -20.8 17.1 0\n1.49 Neck 0 2 0\n1.49 Head -2.1 3.1 0\n1.51 Spine1 0 1 1\n1.51 Spine2 0 2 1\n1.51 RightShoulder -1 0 -3\n1.51 RightArm -58 40 0\n1.51 RightForeArm -22.8 18.5 0\n1.51 Neck 0 2 0\n1.51 Head -2.1 3 0\n1.52 Spine1 0 0.9 0.9\n1.52 Spine2 0 1.9 1\n1.52 RightShoulder -1 0 -3\n1.52 RightArm -58 40 0\n1.52 RightForeArm -23.6 19 0\n1.52 Neck 0 2 0\n1.52 Head -2 3 0\n1.55 Spine1 0 0.9 0.9\n1.55 Spine2 0 1.8 0.9\n1.55 RightShoulder -0.9 0 -2.8\n1.55 RightArm -57.9 42.2 0\n1.55 RightForeArm -24.9 19.9 0\n1.55 Neck 0 1.9 0\n1.55 Head -2 3 0\n1.56 Spine1 0 0.8 0.8\n1.56 Spine2 0 1.8 0.9\n1.56 RightShoulder -0.9 0 -2.8\n1.56 RightArm -57.8 43.9 0\n1.56 RightForeArm -25 20 0\n1.56 Neck 0 1.9 0\n1.56 Head -2 3 0\n1.9 Spine1 0 0 0\n1.9 Spine2 0 0 0\n1.9 RightShoulder 0 0 0\n1.9 RightArm -50 167 0\n1.9 RightForeArm -66 84 0\n1.9 Neck 0 0 0\n1.9 Head 0 0 0\n",
            "clip call_l\nlength 1.9\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.25 Spine1 1 -1 -1\n0.25 Spine2 1 -2 -1\n0.25 LeftShoulder -1.9 0 3.8\n0.25 LeftArm -59.5 -47.5 0\n0.25 LeftForeArm -30.4 -23.1 0\n0.25 Neck 0 -1.8 0\n0.25 Head -1.6 -2.4 0\n0.27 Spine1 1 -1 -1\n0.27 Spine2 1 -2 -1\n0.27 LeftShoulder -2 0 3.9\n0.27 LeftArm -58.8 -44 0\n0.27 LeftForeArm -28.7 -25.2 0\n0.27 Neck 0 -1.9 0\n0.27 Head -1.7 -2.6 0\n0.29 Spine1 1 -1.1 -1\n0.29 Spine2 1 -2 -1\n0.29 LeftShoulder -2 0 4\n0.29 LeftArm -58.3 -41.5 0\n0.29 LeftForeArm -27.4 -26.9 0\n0.29 Neck 0 -2 0\n0.29 Head -1.8 -2.8 0\n0.31 Spine1 1.1 -1.2 -1\n0.31 Spine2 1.1 -2.1 -1\n0.31 LeftShoulder -2 0 4\n0.31 LeftArm -58 -40.2 0\n0.31 LeftForeArm -26.3 -28.4 0\n0.31 Neck 0 -2 0\n0.31 Head -1.9 -2.9 0\n0.32 Spine1 1.1 -1.2 -1\n0.32 Spine2 1.1 -2.1 -1.1\n0.32 LeftShoulder -2.1 0 4.1\n0.32 LeftArm -58 -40 0\n0.32 LeftForeArm -25.8 -28.9 0\n0.32 Neck 0 -2 0\n0.32 Head -2 -2.9 0\n0.35 Spine1 1.2 -1.4 -1\n0.35 Spine2 1.3 -2.3 -1.1\n0.35 LeftShoulder -2.3 0 4.3\n0.35 LeftArm -57.4 -40.1 0\n0.35 LeftForeArm -25.1 -29.9 0\n0.35 Neck 0 -2 0\n0.35 Head -2 -3 0\n0.36 Spine1 1.2 -1.5 -1\n0.36 Spine2 1.3 -2.3 -1.2\n0.36 LeftShoulder -2.4 0 4.4\n0.36 LeftArm -57 -40.2 0\n0.36 LeftForeArm -25 -30 0\n0.36 Neck -0.1 -2.1 0\n0.36 Head -2 -3 0\n0.6 Spine1 2 -3 -1\n0.6 Spine2 3 -4 -2\n0.6 LeftShoulder -5.9 0 7.9\n0.6 LeftArm -32.9 -44.5 0\n0.6 LeftForeArm 17.1 -37.7 0\n0.6 Neck -0.9 -2.9 0\n0.6 Head -4.4 -4.6 0\n0.62 Spine1 2 -3 -1\n0.62 Spine2 3 -4 -2\n0.62 LeftShoulder -6 0 8\n0.62 LeftArm -31.6 -44.7 0\n0.62 LeftForeArm 21 -38.4 0\n0.62 Neck -1 -3 0\n0.62 Head -4.6 -4.7 0\n0.64 Spine1 2 -3 -1\n0.64 Spine2 3 -4 -2\n0.64 LeftShoulder -6 0 8\n0.64 LeftArm -30.6 -44.9 0\n0.64 LeftForeArm 24.3 -39 0\n0.64 Neck -1 -3 0\n0.64 Head -4.8 -4.8 0\n0.66 Spine1 1.9 -2.9 -1\n0.66 Spine2 2.9 -4 -2\n0.66 LeftShoulder -6 0 8\n0.66 LeftArm -30.1 -45 0\n0.66 LeftForeArm 27 -39.4 0\n0.66 Neck -1 -3 0\n0.66 Head -4.9 -4.9 0\n0.67 Spine1 1.9 -2.9 -1\n0.67 Spine2 2.9 -4 -2\n0.67 LeftShoulder -6 0 8\n0.67 LeftArm -30 -45 0\n0.67 LeftForeArm 28 -39.6 0\n0.67 Neck -1 -3 0\n0.67 Head -4.9 -5 0\n0.7 Spine1 1.8 -2.8 -1\n0.7 Spine2 2.8 -3.9 -2\n0.7 LeftShoulder -5.8 0 7.9\n0.7 LeftArm -30.1 -44.9 0\n0.7 LeftForeArm 29.9 -40 0\n0.7 Neck -1 -3 0\n0.7 Head -5 -5 0\n0.71 Spine1 1.8 -2.8 -1\n0.71 Spine2 2.7 -3.9 -2\n0.71 LeftShoulder -5.8 0 7.8\n0.71 LeftArm -30.1 -44.8 0\n0.71 LeftForeArm 30 -40 0\n0.71 Neck -1 -3 0\n0.71 Head -5 -5 0\n1 Spine1 1 -2 -1\n1 Spine2 1 -3 -2\n1 LeftShoulder -3.1 0 6.1\n1 LeftArm -33.7 -38.6 0\n1 LeftForeArm 38.1 13 0\n1 Neck -1 -3 0\n1 Head -5 -5 0\n1.02 Spine1 1 -2 -1\n1.02 Spine2 1 -3 -2\n1.02 LeftShoulder -3 0 6\n1.02 LeftArm -33.8 -38.3 0\n1.02 LeftForeArm 38.7 16.6 0\n1.02 Neck -1 -3 0\n1.02 Head -5 -5 0\n1.04 Spine1 1 -2 -1\n1.04 Spine2 1 -3 -2\n1.04 LeftShoulder -3 0 6\n1.04 LeftArm -33.9 -38.1 0\n1.04 LeftForeArm 39.2 19.7 0\n1.04 Neck -1 -3 0\n1.04 Head -5 -5 0\n1.06 Spine1 1 -2 -1\n1.06 Spine2 1 -3 -2\n1.06 LeftShoulder -3 0 6\n1.06 LeftArm -34 -38 0\n1.06 LeftForeArm 39.6 22.2 0\n1.06 Neck -1 -3 0\n1.06 Head -5 -5 0\n1.07 Spine1 0.9 -1.9 -1\n1.07 Spine2 1 -3 -2\n1.07 LeftShoulder -3 0 6\n1.07 LeftArm -34 -38 0\n1.07 LeftForeArm 39.7 23.2 0\n1.07 Neck -1 -3 0\n1.07 Head -5 -5 0\n1.1 Spine1 0.9 -1.9 -1\n1.1 Spine2 0.9 -2.9 -1.9\n1.1 LeftShoulder -2.9 0 5.9\n1.1 LeftArm -34.3 -38 0\n1.1 LeftForeArm 40 24.9 0\n1.1 Neck -1 -3 0\n1.1 Head -5 -5 0\n1.11 Spine1 0.8 -1.8 -1\n1.11 Spine2 0.9 -2.9 -1.9\n1.11 LeftShoulder -2.9 0 5.8\n1.11 LeftArm -34.5 -38 0\n1.11 LeftForeArm 40 25 0\n1.11 Neck -1 -3 0\n1.11 Head -5 -5 0\n1.45 Spine1 0 -1 -1\n1.45 Spine2 0 -2 -1\n1.45 LeftShoulder -1 0 3.1\n1.45 LeftArm -56.4 -39.9 0\n1.45 LeftForeArm -15.2 -13.2 0\n1.45 Neck 0 -2 0\n1.45 Head -2.4 -3.3 0\n1.47 Spine1 0 -1 -1\n1.47 Spine2 0 -2 -1\n1.47 LeftShoulder -1 0 3\n1.47 LeftArm -57.2 -39.9 0\n1.47 LeftForeArm -18.2 -15.3 0\n1.47 Neck 0 -2 0\n1.47 Head -2.3 -3.2 0\n1.49 Spine1 0 -1 -1\n1.49 Spine2 0 -2 -1\n1.49 LeftShoulder -1 0 3\n1.49 LeftArm -57.7 -40 0\n1.49 LeftForeArm -20.8 -17.1 0\n1.49 Neck 0 -2 0\n1.49 Head -2.1 -3.1 0\n1.51 Spine1 0 -1 -1\n1.51 Spine2 0 -2 -1\n1.51 LeftShoulder -1 0 3\n1.51 LeftArm -58 -40 0\n1.51 LeftForeArm -22.8 -18.5 0\n1.51 Neck 0 -2 0\n1.51 Head -2.1 -3 0\n1.52 Spine1 0 -0.9 -0.9\n1.52 Spine2 0 -1.9 -1\n1.52 LeftShoulder -1 0 3\n1.52 LeftArm -58 -40 0\n1.52 LeftForeArm -23.6 -19 0\n1.52 Neck 0 -2 0\n1.52 Head -2 -3 0\n1.55 Spine1 0 -0.9 -0.9\n1.55 Spine2 0 -1.8 -0.9\n1.55 LeftShoulder -0.9 0 2.8\n1.55 LeftArm -58.2 -41.1 0\n1.55 LeftForeArm -24.9 -19.9 0\n1.55 Neck 0 -1.9 0\n1.55 Head -2 -3 0\n1.56 Spine1 0 -0.8 -0.8\n1.56 Spine2 0 -1.8 -0.9\n1.56 LeftShoulder -0.9 0 2.8\n1.56 LeftArm -58.4 -41.9 0\n1.56 LeftForeArm -25 -20 0\n1.56 Neck 0 -1.9 0\n1.56 Head -2 -3 0\n1.9 Spine1 0 0 0\n1.9 Spine2 0 0 0\n1.9 LeftShoulder 0 0 0\n1.9 LeftArm -70 -101 0\n1.9 LeftForeArm -49 1 0\n1.9 Neck 0 0 0\n1.9 Head 0 0 0\n",
            "clip order_circle_r\nlength 2.5\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 0 1 1\n0.28 Spine2 -1 2 1\n0.28 RightShoulder -1 0 -5.7\n0.28 RightArm -54.5 66.6 0\n0.28 RightForeArm -28.9 44.5 0\n0.28 Neck -0.9 1.8 0\n0.28 Head -2.5 2.5 0\n0.3 Spine1 0 1 1\n0.3 Spine2 -1 2 1\n0.3 RightShoulder -1 0 -5.9\n0.3 RightArm -54.7 61.2 0\n0.3 RightForeArm -26.2 41.6 0\n0.3 Neck -1 1.9 0\n0.3 Head -2.7 2.7 0\n0.32 Spine1 0 1 1\n0.32 Spine2 -1 2 1\n0.32 RightShoulder -1 0 -6\n0.32 RightArm -54.9 57.3 0\n0.32 RightForeArm -23.9 39.2 0\n0.32 Neck -1 2 0\n0.32 Head -2.8 2.8 0\n0.34 Spine1 -0.1 1.1 1.1\n0.34 Spine2 -1 2 1.1\n0.34 RightShoulder -1 0 -6.1\n0.34 RightArm -55 55.3 0\n0.34 RightForeArm -22.1 37.2 0\n0.34 Neck -1 2 0\n0.34 Head -2.9 2.9 0\n0.35 Spine1 -0.1 1.1 1.1\n0.35 Spine2 -1 2 1.1\n0.35 RightShoulder -1 0 -6.1\n0.35 RightArm -55 55 0\n0.35 RightForeArm -21.4 36.4 0\n0.35 Neck -1 2 0\n0.35 Head -2.9 2.9 0\n0.38 Spine1 -0.2 1.2 1.2\n0.38 Spine2 -1.1 2.1 1.2\n0.38 RightShoulder -1.1 0 -6.4\n0.38 RightArm -54.1 55.4 0\n0.38 RightForeArm -20.1 35.1 0\n0.38 Neck -1.1 2 0\n0.38 Head -3 3 0\n0.39 Spine1 -0.2 1.2 1.2\n0.39 Spine2 -1.1 2.1 1.3\n0.39 RightShoulder -1.1 0 -6.6\n0.39 RightArm -53.5 55.7 0\n0.39 RightForeArm -20 35 0\n0.39 Neck -1.1 2 0\n0.39 Head -3 3 0\n0.65 Spine1 -1 2 2\n0.65 Spine2 -2 3 3\n0.65 RightShoulder -2 0 -11.8\n0.65 RightArm -12.4 75.8 0\n0.65 RightForeArm 35.1 38.9 0\n0.65 Neck -2.9 2.9 0\n0.65 Head -8.7 5.5 0\n0.67 Spine1 -1 2 2\n0.67 Spine2 -2 3 3\n0.67 RightShoulder -2 0 -11.9\n0.67 RightArm -10.3 76.9 0\n0.67 RightForeArm 39.6 39.3 0\n0.67 Neck -2.9 3 0\n0.67 Head -9.2 5.6 0\n0.69 Spine1 -1 2 2\n0.69 Spine2 -2 3 3\n0.69 RightShoulder -2 0 -12\n0.69 RightArm -8.9 77.6 0\n0.69 RightForeArm 43.4 39.5 0\n0.69 Neck -3 3 0\n0.69 Head -9.5 5.8 0\n0.71 Spine1 -1 2 2\n0.71 Spine2 -2 3 3\n0.71 RightShoulder -2 0 -12\n0.71 RightArm -8.1 78 0\n0.71 RightForeArm 46.5 39.8 0\n0.71 Neck -3 3 0\n0.71 Head -9.8 5.9 0\n0.72 Spine1 -1 2 2\n0.72 Spine2 -2 3 3\n0.72 RightShoulder -2 0 -12\n0.72 RightArm -8 78 0\n0.72 RightForeArm 47.7 39.8 0\n0.72 Neck -3 3 0\n0.72 Head -9.9 5.9 0\n0.75 Spine1 -1 2 2\n0.75 Spine2 -2 3 3\n0.75 RightShoulder -2 0 -12.1\n0.75 RightArm -7.8 78.2 0\n0.75 RightForeArm 49.8 40 0\n0.75 Neck -3 3.1 0\n0.75 Head -10 6 0\n0.76 Spine1 -1 2 2\n0.76 Spine2 -2 3 3\n0.76 RightShoulder -2 0 -12.2\n0.76 RightArm -7.7 78.3 0\n0.76 RightForeArm 50 40 0\n0.76 Neck -3 3.1 0\n0.76 Head -10 6 0\n0.9 Spine1 -1 2 2\n0.9 Spine2 -2 3 3\n0.9 RightShoulder -2 0 -12.9\n0.9 RightArm -4.8 81.2 0\n0.9 RightForeArm 53.5 50.6 0\n0.9 Neck -3 3.9 0\n0.9 Head -10.6 7.3 0\n0.92 Spine1 -1 2 2\n0.92 Spine2 -2 3 3\n0.92 RightShoulder -2 0 -13\n0.92 RightArm -4.4 81.6 0\n0.92 RightForeArm 54.2 52.7 0\n0.92 Neck -3 3.9 0\n0.92 Head -10.8 7.5 0\n0.94 Spine1 -1 2 2\n0.94 Spine2 -2 3 3\n0.94 RightShoulder -2 0 -13\n0.94 RightArm -4.2 81.8 0\n0.94 RightForeArm 54.9 54.6 0\n0.94 Neck -3 4 0\n0.94 Head -10.9 7.7 0\n0.96 Spine1 -1 2 2\n0.96 Spine2 -2 3 3\n0.96 RightShoulder -2 0 -13\n0.96 RightArm -4 82 0\n0.96 RightForeArm 55.4 56.1 0\n0.96 Neck -3 4 0\n0.96 Head -10.9 7.9 0\n0.97 Spine1 -1 2 2\n0.97 Spine2 -2 3 3\n0.97 RightShoulder -2 0 -13\n0.97 RightArm -4 82 0\n0.97 RightForeArm 55.6 56.8 0\n0.97 Neck -3 4 0\n0.97 Head -11 7.9 0\n1 Spine1 -1 2 2\n1 Spine2 -2 3 3\n1 RightShoulder -2 0 -13.1\n1 RightArm -3.8 81.8 0\n1 RightForeArm 56 57.9 0\n1 Neck -3 4 0\n1 Head -11 8 0\n1.01 Spine1 -1 2 2\n1.01 Spine2 -2 3 3\n1.01 RightShoulder -2 0 -13.2\n1.01 RightArm -3.7 81.7 0\n1.01 RightForeArm 56 58 0\n1.01 Neck -3 4 0\n1.01 Head -11 8 0\n1.15 Spine1 -1 2 2\n1.15 Spine2 -2 3 3\n1.15 RightShoulder -2 0 -13.9\n1.15 RightArm -0.8 78.8 0\n1.15 RightForeArm 59.5 48.6 0\n1.15 Neck -3 4 0\n1.15 Head -11.6 7.4 0\n1.17 Spine1 -1 2 2\n1.17 Spine2 -2 3 3\n1.17 RightShoulder -2 0 -14\n1.17 RightArm -0.4 78.4 0\n1.17 RightForeArm 60.2 46.7 0\n1.17 Neck -3 4 0\n1.17 Head -11.8 7.2 0\n1.19 Spine1 -1 2 2\n1.19 Spine2 -2 3 3\n1.19 RightShoulder -2 0 -14\n1.19 RightArm -0.2 78.2 0\n1.19 RightForeArm 60.9 45.1 0\n1.19 Neck -3 4 0\n1.19 Head -11.9 7.1 0\n1.21 Spine1 -1 2 2\n1.21 Spine2 -2 3 3\n1.21 RightShoulder -2 0 -14\n1.21 RightArm 0 78 0\n1.21 RightForeArm 61.4 43.7 0\n1.21 Neck -3 4 0\n1.21 Head -11.9 7.1 0\n1.22 Spine1 -1 2 2\n1.22 Spine2 -2 3 3\n1.22 RightShoulder -2 0 -14\n1.22 RightArm 0 78 0\n1.22 RightForeArm 61.6 43.1 0\n1.22 Neck -3 4 0\n1.22 Head -12 7 0\n1.25 Spine1 -1 2 2\n1.25 Spine2 -2 3 3\n1.25 RightShoulder -2 0 -13.9\n1.25 RightArm -0.2 77.8 0\n1.25 RightForeArm 62 42.1 0\n1.25 Neck -3 3.9 0\n1.25 Head -12 7 0\n1.26 Spine1 -1 2 2\n1.26 Spine2 -2 3 3\n1.26 RightShoulder -2 0 -13.8\n1.26 RightArm -0.3 77.7 0\n1.26 RightForeArm 62 42 0\n1.26 Neck -3 3.9 0\n1.26 Head -12 7 0\n1.4 Spine1 -1 2 2\n1.4 Spine2 -2 3 3\n1.4 RightShoulder -2 0 -13.1\n1.4 RightArm -3.2 74.8 0\n1.4 RightForeArm 58.5 32.6 0\n1.4 Neck -3 3.1 0\n1.4 Head -11.4 5.7 0\n1.42 Spine1 -1 2 2\n1.42 Spine2 -2 3 3\n1.42 RightShoulder -2 0 -13\n1.42 RightArm -3.6 74.4 0\n1.42 RightForeArm 57.8 30.7 0\n1.42 Neck -3 3.1 0\n1.42 Head -11.2 5.5 0\n1.44 Spine1 -1 2 2\n1.44 Spine2 -2 3 3\n1.44 RightShoulder -2 0 -13\n1.44 RightArm -3.8 74.2 0\n1.44 RightForeArm 57.1 29.1 0\n1.44 Neck -3 3 0\n1.44 Head -11.1 5.3 0\n1.46 Spine1 -1 2 2\n1.46 Spine2 -2 3 3\n1.46 RightShoulder -2 0 -13\n1.46 RightArm -4 74 0\n1.46 RightForeArm 56.6 27.7 0\n1.46 Neck -3 3 0\n1.46 Head -11.1 5.1 0\n1.47 Spine1 -1 2 2\n1.47 Spine2 -2 3 3\n1.47 RightShoulder -2 0 -13\n1.47 RightArm -4 74 0\n1.47 RightForeArm 56.4 27.1 0\n1.47 Neck -3 3 0\n1.47 Head -11 5.1 0\n1.5 Spine1 -1 2 2\n1.5 Spine2 -2 3 3\n1.5 RightShoulder -2 0 -12.9\n1.5 RightArm -4.2 74.2 0\n1.5 RightForeArm 56 26.1 0\n1.5 Neck -3 3 0\n1.5 Head -11 5 0\n1.51 Spine1 -1 2 2\n1.51 Spine2 -2 3 3\n1.51 RightShoulder -2 0 -12.8\n1.51 RightArm -4.3 74.3 0\n1.51 RightForeArm 56 26 0\n1.51 Neck -3 3 0\n1.51 Head -11 5 0\n1.65 Spine1 -1 2 2\n1.65 Spine2 -2 3 3\n1.65 RightShoulder -2 0 -12.1\n1.65 RightArm -7.2 77.2 0\n1.65 RightForeArm 52.5 34.3 0\n1.65 Neck -3 3 0\n1.65 Head -10.4 5.6 0\n1.67 Spine1 -1 2 2\n1.67 Spine2 -2 3 3\n1.67 RightShoulder -2 0 -12\n1.67 RightArm -7.6 77.6 0\n1.67 RightForeArm 51.8 35.9 0\n1.67 Neck -3 3 0\n1.67 Head -10.2 5.8 0\n1.69 Spine1 -1 2 2\n1.69 Spine2 -2 3 3\n1.69 RightShoulder -2 0 -12\n1.69 RightArm -7.8 77.8 0\n1.69 RightForeArm 51.1 37.3 0\n1.69 Neck -3 3 0\n1.69 Head -10.1 5.9 0\n1.71 Spine1 -0.9 1.9 1.9\n1.71 Spine2 -2 3 2.9\n1.71 RightShoulder -2 0 -11.9\n1.71 RightArm -8 78 0\n1.71 RightForeArm 50.6 38.5 0\n1.71 Neck -3 3 0\n1.71 Head -10.1 5.9 0\n1.72 Spine1 -0.9 1.9 1.9\n1.72 Spine2 -2 3 2.9\n1.72 RightShoulder -2 0 -11.9\n1.72 RightArm -8 78 0\n1.72 RightForeArm 50.4 39 0\n1.72 Neck -3 3 0\n1.72 Head -10 6 0\n1.75 Spine1 -0.8 1.8 1.8\n1.75 Spine2 -1.9 2.9 2.8\n1.75 RightShoulder -1.9 0 -11.6\n1.75 RightArm -8.8 77.6 0\n1.75 RightForeArm 50 39.9 0\n1.75 Neck -2.9 3 0\n1.75 Head -10 6 0\n1.76 Spine1 -0.8 1.8 1.8\n1.76 Spine2 -1.9 2.9 2.7\n1.76 RightShoulder -1.9 0 -11.4\n1.76 RightArm -9.3 77.4 0\n1.76 RightForeArm 50 40 0\n1.76 Neck -2.9 3 0\n1.76 Head -10 6 0\n2.05 Spine1 0 1 1\n2.05 Spine2 -1 2 1\n2.05 RightShoulder -1 0 -5.2\n2.05 RightArm -51.2 56.9 0\n2.05 RightForeArm -7 35.9 0\n2.05 Neck -1.1 2.1 0\n2.05 Head -4.1 3.5 0\n2.07 Spine1 0 1 1\n2.07 Spine2 -1 2 1\n2.07 RightShoulder -1 0 -5.1\n2.07 RightArm -53 56 0\n2.07 RightForeArm -11 35.6 0\n2.07 Neck -1.1 2 0\n2.07 Head -3.7 3.3 0\n2.09 Spine1 0 1 1\n2.09 Spine2 -1 2 1\n2.09 RightShoulder -1 0 -5\n2.09 RightArm -54.2 55.4 0\n2.09 RightForeArm -14.3 35.4 0\n2.09 Neck -1 2 0\n2.09 Head -3.4 3.2 0\n2.11 Spine1 0 1 1\n2.11 Spine2 -1 2 1\n2.11 RightShoulder -1 0 -5\n2.11 RightArm -54.9 55 0\n2.11 RightForeArm -17 35.2 0\n2.11 Neck -1 2 0\n2.11 Head -3.2 3.1 0\n2.12 Spine1 0 0.9 0.9\n2.12 Spine2 -1 1.9 1\n2.12 RightShoulder -1 0 -4.9\n2.12 RightArm -55 55 0\n2.12 RightForeArm -18 35.1 0\n2.12 Neck -1 2 0\n2.12 Head -3.1 3 0\n2.15 Spine1 0 0.9 0.9\n2.15 Spine2 -0.9 1.8 0.9\n2.15 RightShoulder -0.9 0 -4.7\n2.15 RightArm -54.9 57 0\n2.15 RightForeArm -19.9 35 0\n2.15 Neck -1 1.9 0\n2.15 Head -3 3 0\n2.16 Spine1 0 0.8 0.8\n2.16 Spine2 -0.9 1.8 0.9\n2.16 RightShoulder -0.9 0 -4.6\n2.16 RightArm -54.8 58.5 0\n2.16 RightForeArm -20 35 0\n2.16 Neck -1 1.9 0\n2.16 Head -3 3 0\n2.5 Spine1 0 0 0\n2.5 Spine2 0 0 0\n2.5 RightShoulder 0 0 0\n2.5 RightArm -50 167 0\n2.5 RightForeArm -66 84 0\n2.5 Neck 0 0 0\n2.5 Head 0 0 0\n",
            "clip order_circle_l\nlength 2.5\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 0 -1 -1\n0.28 Spine2 -1 -2 -1\n0.28 LeftShoulder -1 0 5.7\n0.28 LeftArm -56.6 -59.8 0\n0.28 LeftForeArm -25.6 -28 0\n0.28 Neck -0.9 -1.8 0\n0.28 Head -2.5 -2.5 0\n0.3 Spine1 0 -1 -1\n0.3 Spine2 -1 -2 -1\n0.3 LeftShoulder -1 0 5.9\n0.3 LeftArm -55.8 -57.5 0\n0.3 LeftForeArm -23.9 -30.1 0\n0.3 Neck -1 -1.9 0\n0.3 Head -2.7 -2.7 0\n0.32 Spine1 0 -1 -1\n0.32 Spine2 -1 -2 -1\n0.32 LeftShoulder -1 0 6\n0.32 LeftArm -55.3 -56 0\n0.32 LeftForeArm -22.5 -31.9 0\n0.32 Neck -1 -2 0\n0.32 Head -2.8 -2.8 0\n0.34 Spine1 -0.1 -1.1 -1.1\n0.34 Spine2 -1 -2 -1.1\n0.34 LeftShoulder -1 0 6.1\n0.34 LeftArm -55 -55.1 0\n0.34 LeftForeArm -21.3 -33.4 0\n0.34 Neck -1 -2 0\n0.34 Head -2.9 -2.9 0\n0.35 Spine1 -0.1 -1.1 -1.1\n0.35 Spine2 -1 -2 -1.1\n0.35 LeftShoulder -1 0 6.1\n0.35 LeftArm -55 -55 0\n0.35 LeftForeArm -20.9 -33.9 0\n0.35 Neck -1 -2 0\n0.35 Head -2.9 -2.9 0\n0.38 Spine1 -0.2 -1.2 -1.2\n0.38 Spine2 -1.1 -2.1 -1.2\n0.38 LeftShoulder -1.1 0 6.4\n0.38 LeftArm -54.1 -55.4 0\n0.38 LeftForeArm -20.1 -34.9 0\n0.38 Neck -1.1 -2 0\n0.38 Head -3 -3 0\n0.39 Spine1 -0.2 -1.2 -1.2\n0.39 Spine2 -1.1 -2.1 -1.3\n0.39 LeftShoulder -1.1 0 6.6\n0.39 LeftArm -53.5 -55.7 0\n0.39 LeftForeArm -20 -35 0\n0.39 Neck -1.1 -2 0\n0.39 Head -3 -3 0\n0.65 Spine1 -1 -2 -2\n0.65 Spine2 -2 -3 -3\n0.65 LeftShoulder -2 0 11.8\n0.65 LeftArm -12.4 -75.8 0\n0.65 LeftForeArm 35.1 -38.9 0\n0.65 Neck -2.9 -2.9 0\n0.65 Head -8.7 -5.5 0\n0.67 Spine1 -1 -2 -2\n0.67 Spine2 -2 -3 -3\n0.67 LeftShoulder -2 0 11.9\n0.67 LeftArm -10.3 -76.9 0\n0.67 LeftForeArm 39.6 -39.3 0\n0.67 Neck -2.9 -3 0\n0.67 Head -9.2 -5.6 0\n0.69 Spine1 -1 -2 -2\n0.69 Spine2 -2 -3 -3\n0.69 LeftShoulder -2 0 12\n0.69 LeftArm -8.9 -77.6 0\n0.69 LeftForeArm 43.4 -39.5 0\n0.69 Neck -3 -3 0\n0.69 Head -9.5 -5.8 0\n0.71 Spine1 -1 -2 -2\n0.71 Spine2 -2 -3 -3\n0.71 LeftShoulder -2 0 12\n0.71 LeftArm -8.1 -78 0\n0.71 LeftForeArm 46.5 -39.8 0\n0.71 Neck -3 -3 0\n0.71 Head -9.8 -5.9 0\n0.72 Spine1 -1 -2 -2\n0.72 Spine2 -2 -3 -3\n0.72 LeftShoulder -2 0 12\n0.72 LeftArm -8 -78 0\n0.72 LeftForeArm 47.7 -39.8 0\n0.72 Neck -3 -3 0\n0.72 Head -9.9 -5.9 0\n0.75 Spine1 -1 -2 -2\n0.75 Spine2 -2 -3 -3\n0.75 LeftShoulder -2 0 12.1\n0.75 LeftArm -7.8 -78.2 0\n0.75 LeftForeArm 49.8 -40 0\n0.75 Neck -3 -3.1 0\n0.75 Head -10 -6 0\n0.76 Spine1 -1 -2 -2\n0.76 Spine2 -2 -3 -3\n0.76 LeftShoulder -2 0 12.2\n0.76 LeftArm -7.7 -78.3 0\n0.76 LeftForeArm 50 -40 0\n0.76 Neck -3 -3.1 0\n0.76 Head -10 -6 0\n0.9 Spine1 -1 -2 -2\n0.9 Spine2 -2 -3 -3\n0.9 LeftShoulder -2 0 12.9\n0.9 LeftArm -4.8 -81.2 0\n0.9 LeftForeArm 53.5 -50.6 0\n0.9 Neck -3 -3.9 0\n0.9 Head -10.6 -7.3 0\n0.92 Spine1 -1 -2 -2\n0.92 Spine2 -2 -3 -3\n0.92 LeftShoulder -2 0 13\n0.92 LeftArm -4.4 -81.6 0\n0.92 LeftForeArm 54.2 -52.7 0\n0.92 Neck -3 -3.9 0\n0.92 Head -10.8 -7.5 0\n0.94 Spine1 -1 -2 -2\n0.94 Spine2 -2 -3 -3\n0.94 LeftShoulder -2 0 13\n0.94 LeftArm -4.2 -81.8 0\n0.94 LeftForeArm 54.9 -54.6 0\n0.94 Neck -3 -4 0\n0.94 Head -10.9 -7.7 0\n0.96 Spine1 -1 -2 -2\n0.96 Spine2 -2 -3 -3\n0.96 LeftShoulder -2 0 13\n0.96 LeftArm -4 -82 0\n0.96 LeftForeArm 55.4 -56.1 0\n0.96 Neck -3 -4 0\n0.96 Head -10.9 -7.9 0\n0.97 Spine1 -1 -2 -2\n0.97 Spine2 -2 -3 -3\n0.97 LeftShoulder -2 0 13\n0.97 LeftArm -4 -82 0\n0.97 LeftForeArm 55.6 -56.8 0\n0.97 Neck -3 -4 0\n0.97 Head -11 -7.9 0\n1 Spine1 -1 -2 -2\n1 Spine2 -2 -3 -3\n1 LeftShoulder -2 0 13.1\n1 LeftArm -3.8 -81.8 0\n1 LeftForeArm 56 -57.9 0\n1 Neck -3 -4 0\n1 Head -11 -8 0\n1.01 Spine1 -1 -2 -2\n1.01 Spine2 -2 -3 -3\n1.01 LeftShoulder -2 0 13.2\n1.01 LeftArm -3.7 -81.7 0\n1.01 LeftForeArm 56 -58 0\n1.01 Neck -3 -4 0\n1.01 Head -11 -8 0\n1.15 Spine1 -1 -2 -2\n1.15 Spine2 -2 -3 -3\n1.15 LeftShoulder -2 0 13.9\n1.15 LeftArm -0.8 -78.8 0\n1.15 LeftForeArm 59.5 -48.6 0\n1.15 Neck -3 -4 0\n1.15 Head -11.6 -7.4 0\n1.17 Spine1 -1 -2 -2\n1.17 Spine2 -2 -3 -3\n1.17 LeftShoulder -2 0 14\n1.17 LeftArm -0.4 -78.4 0\n1.17 LeftForeArm 60.2 -46.7 0\n1.17 Neck -3 -4 0\n1.17 Head -11.8 -7.2 0\n1.19 Spine1 -1 -2 -2\n1.19 Spine2 -2 -3 -3\n1.19 LeftShoulder -2 0 14\n1.19 LeftArm -0.2 -78.2 0\n1.19 LeftForeArm 60.9 -45.1 0\n1.19 Neck -3 -4 0\n1.19 Head -11.9 -7.1 0\n1.21 Spine1 -1 -2 -2\n1.21 Spine2 -2 -3 -3\n1.21 LeftShoulder -2 0 14\n1.21 LeftArm 0 -78 0\n1.21 LeftForeArm 61.4 -43.7 0\n1.21 Neck -3 -4 0\n1.21 Head -11.9 -7.1 0\n1.22 Spine1 -1 -2 -2\n1.22 Spine2 -2 -3 -3\n1.22 LeftShoulder -2 0 14\n1.22 LeftArm 0 -78 0\n1.22 LeftForeArm 61.6 -43.1 0\n1.22 Neck -3 -4 0\n1.22 Head -12 -7 0\n1.25 Spine1 -1 -2 -2\n1.25 Spine2 -2 -3 -3\n1.25 LeftShoulder -2 0 13.9\n1.25 LeftArm -0.2 -77.8 0\n1.25 LeftForeArm 62 -42.1 0\n1.25 Neck -3 -3.9 0\n1.25 Head -12 -7 0\n1.26 Spine1 -1 -2 -2\n1.26 Spine2 -2 -3 -3\n1.26 LeftShoulder -2 0 13.8\n1.26 LeftArm -0.3 -77.7 0\n1.26 LeftForeArm 62 -42 0\n1.26 Neck -3 -3.9 0\n1.26 Head -12 -7 0\n1.4 Spine1 -1 -2 -2\n1.4 Spine2 -2 -3 -3\n1.4 LeftShoulder -2 0 13.1\n1.4 LeftArm -3.2 -74.8 0\n1.4 LeftForeArm 58.5 -32.6 0\n1.4 Neck -3 -3.1 0\n1.4 Head -11.4 -5.7 0\n1.42 Spine1 -1 -2 -2\n1.42 Spine2 -2 -3 -3\n1.42 LeftShoulder -2 0 13\n1.42 LeftArm -3.6 -74.4 0\n1.42 LeftForeArm 57.8 -30.7 0\n1.42 Neck -3 -3.1 0\n1.42 Head -11.2 -5.5 0\n1.44 Spine1 -1 -2 -2\n1.44 Spine2 -2 -3 -3\n1.44 LeftShoulder -2 0 13\n1.44 LeftArm -3.8 -74.2 0\n1.44 LeftForeArm 57.1 -29.1 0\n1.44 Neck -3 -3 0\n1.44 Head -11.1 -5.3 0\n1.46 Spine1 -1 -2 -2\n1.46 Spine2 -2 -3 -3\n1.46 LeftShoulder -2 0 13\n1.46 LeftArm -4 -74 0\n1.46 LeftForeArm 56.6 -27.7 0\n1.46 Neck -3 -3 0\n1.46 Head -11.1 -5.1 0\n1.47 Spine1 -1 -2 -2\n1.47 Spine2 -2 -3 -3\n1.47 LeftShoulder -2 0 13\n1.47 LeftArm -4 -74 0\n1.47 LeftForeArm 56.4 -27.1 0\n1.47 Neck -3 -3 0\n1.47 Head -11 -5.1 0\n1.5 Spine1 -1 -2 -2\n1.5 Spine2 -2 -3 -3\n1.5 LeftShoulder -2 0 12.9\n1.5 LeftArm -4.2 -74.2 0\n1.5 LeftForeArm 56 -26.1 0\n1.5 Neck -3 -3 0\n1.5 Head -11 -5 0\n1.51 Spine1 -1 -2 -2\n1.51 Spine2 -2 -3 -3\n1.51 LeftShoulder -2 0 12.8\n1.51 LeftArm -4.3 -74.3 0\n1.51 LeftForeArm 56 -26 0\n1.51 Neck -3 -3 0\n1.51 Head -11 -5 0\n1.65 Spine1 -1 -2 -2\n1.65 Spine2 -2 -3 -3\n1.65 LeftShoulder -2 0 12.1\n1.65 LeftArm -7.2 -77.2 0\n1.65 LeftForeArm 52.5 -34.3 0\n1.65 Neck -3 -3 0\n1.65 Head -10.4 -5.6 0\n1.67 Spine1 -1 -2 -2\n1.67 Spine2 -2 -3 -3\n1.67 LeftShoulder -2 0 12\n1.67 LeftArm -7.6 -77.6 0\n1.67 LeftForeArm 51.8 -35.9 0\n1.67 Neck -3 -3 0\n1.67 Head -10.2 -5.8 0\n1.69 Spine1 -1 -2 -2\n1.69 Spine2 -2 -3 -3\n1.69 LeftShoulder -2 0 12\n1.69 LeftArm -7.8 -77.8 0\n1.69 LeftForeArm 51.1 -37.3 0\n1.69 Neck -3 -3 0\n1.69 Head -10.1 -5.9 0\n1.71 Spine1 -0.9 -1.9 -1.9\n1.71 Spine2 -2 -3 -2.9\n1.71 LeftShoulder -2 0 11.9\n1.71 LeftArm -8 -78 0\n1.71 LeftForeArm 50.6 -38.5 0\n1.71 Neck -3 -3 0\n1.71 Head -10.1 -5.9 0\n1.72 Spine1 -0.9 -1.9 -1.9\n1.72 Spine2 -2 -3 -2.9\n1.72 LeftShoulder -2 0 11.9\n1.72 LeftArm -8 -78 0\n1.72 LeftForeArm 50.4 -39 0\n1.72 Neck -3 -3 0\n1.72 Head -10 -6 0\n1.75 Spine1 -0.8 -1.8 -1.8\n1.75 Spine2 -1.9 -2.9 -2.8\n1.75 LeftShoulder -1.9 0 11.6\n1.75 LeftArm -8.8 -77.6 0\n1.75 LeftForeArm 50 -39.9 0\n1.75 Neck -2.9 -3 0\n1.75 Head -10 -6 0\n1.76 Spine1 -0.8 -1.8 -1.8\n1.76 Spine2 -1.9 -2.9 -2.7\n1.76 LeftShoulder -1.9 0 11.4\n1.76 LeftArm -9.3 -77.4 0\n1.76 LeftForeArm 50 -40 0\n1.76 Neck -2.9 -3 0\n1.76 Head -10 -6 0\n2.05 Spine1 0 -1 -1\n2.05 Spine2 -1 -2 -1\n2.05 LeftShoulder -1 0 5.2\n2.05 LeftArm -51.2 -56.9 0\n2.05 LeftForeArm -7 -35.9 0\n2.05 Neck -1.1 -2.1 0\n2.05 Head -4.1 -3.5 0\n2.07 Spine1 0 -1 -1\n2.07 Spine2 -1 -2 -1\n2.07 LeftShoulder -1 0 5.1\n2.07 LeftArm -53 -56 0\n2.07 LeftForeArm -11 -35.6 0\n2.07 Neck -1.1 -2 0\n2.07 Head -3.7 -3.3 0\n2.09 Spine1 0 -1 -1\n2.09 Spine2 -1 -2 -1\n2.09 LeftShoulder -1 0 5\n2.09 LeftArm -54.2 -55.4 0\n2.09 LeftForeArm -14.3 -35.4 0\n2.09 Neck -1 -2 0\n2.09 Head -3.4 -3.2 0\n2.11 Spine1 0 -1 -1\n2.11 Spine2 -1 -2 -1\n2.11 LeftShoulder -1 0 5\n2.11 LeftArm -54.9 -55 0\n2.11 LeftForeArm -17 -35.2 0\n2.11 Neck -1 -2 0\n2.11 Head -3.2 -3.1 0\n2.12 Spine1 0 -0.9 -0.9\n2.12 Spine2 -1 -1.9 -1\n2.12 LeftShoulder -1 0 4.9\n2.12 LeftArm -55 -55 0\n2.12 LeftForeArm -18 -35.1 0\n2.12 Neck -1 -2 0\n2.12 Head -3.1 -3 0\n2.15 Spine1 0 -0.9 -0.9\n2.15 Spine2 -0.9 -1.8 -0.9\n2.15 LeftShoulder -0.9 0 4.7\n2.15 LeftArm -55.3 -55.8 0\n2.15 LeftForeArm -19.9 -35 0\n2.15 Neck -1 -1.9 0\n2.15 Head -3 -3 0\n2.16 Spine1 0 -0.8 -0.8\n2.16 Spine2 -0.9 -1.8 -0.9\n2.16 LeftShoulder -0.9 0 4.6\n2.16 LeftArm -55.5 -56.4 0\n2.16 LeftForeArm -20 -35 0\n2.16 Neck -1 -1.9 0\n2.16 Head -3 -3 0\n2.5 Spine1 0 0 0\n2.5 Spine2 0 0 0\n2.5 LeftShoulder 0 0 0\n2.5 LeftArm -70 -101 0\n2.5 LeftForeArm -49 1 0\n2.5 Neck 0 0 0\n2.5 Head 0 0 0\n",
            "clip order_follow_r\nlength 1.9\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.25 Spine1 1 2 1\n0.25 Spine2 1 3 1\n0.25 RightShoulder -1.9 0 -3.8\n0.25 RightArm -58.8 51.2 0\n0.25 RightForeArm -38 38.2 0\n0.25 Neck 0 2.7 0\n0.25 Head 1.6 4 0\n0.27 Spine1 1 2 1\n0.27 Spine2 1 3 1\n0.27 RightShoulder -2 0 -3.9\n0.27 RightArm -59.3 43.7 0\n0.27 RightForeArm -35.6 34.2 0\n0.27 Neck 0 2.9 0\n0.27 Head 1.7 4.3 0\n0.29 Spine1 1 1.8 1\n0.29 Spine2 1 2.9 1\n0.29 RightShoulder -2 0 -4\n0.29 RightArm -59.8 38.3 0\n0.29 RightForeArm -33.6 30.8 0\n0.29 Neck 0 3 0\n0.29 Head 1.8 4.6 0\n0.31 Spine1 1.1 1.6 1\n0.31 Spine2 1 2.7 1\n0.31 RightShoulder -2 0 -4\n0.31 RightArm -60 35.4 0\n0.31 RightForeArm -31.9 28.1 0\n0.31 Neck 0 3 0\n0.31 Head 1.9 4.8 0\n0.32 Spine1 1.1 1.5 1\n0.32 Spine2 1.1 2.6 1\n0.32 RightShoulder -2.1 0 -4.1\n0.32 RightArm -60 35 0\n0.32 RightForeArm -31.2 27 0\n0.32 Neck 0 3 0\n0.32 Head 2 4.9 0\n0.35 Spine1 1.2 1 1\n0.35 Spine2 1.1 1.9 1\n0.35 RightShoulder -2.3 0 -4.3\n0.35 RightArm -59.3 34.9 0\n0.35 RightForeArm -30.1 25.1 0\n0.35 Neck 0 2.8 0\n0.35 Head 2 5 0\n0.36 Spine1 1.2 0.8 1\n0.36 Spine2 1.2 1.7 1\n0.36 RightShoulder -2.4 0 -4.4\n0.36 RightArm -58.8 34.8 0\n0.36 RightForeArm -30 25 0\n0.36 Neck 0 2.7 0\n0.36 Head 2 5 0\n0.6 Spine1 2 -3 1\n0.6 Spine2 2 -4.9 1\n0.6 RightShoulder -5.9 0 -7.9\n0.6 RightArm -31.3 30.5 0\n0.6 RightForeArm -7 -5.6 0\n0.6 Neck 0 -1.6 0\n0.6 Head 2.8 -2.2 0\n0.62 Spine1 2 -3 1\n0.62 Spine2 2 -5 1\n0.62 RightShoulder -6 0 -8\n0.62 RightArm -29.8 30.3 0\n0.62 RightForeArm -4.9 -8.4 0\n0.62 Neck 0 -1.8 0\n0.62 Head 2.9 -2.8 0\n0.64 Spine1 2 -2.8 1\n0.64 Spine2 2 -4.9 1\n0.64 RightShoulder -6 0 -8\n0.64 RightArm -28.7 30.1 0\n0.64 RightForeArm -3.1 -10.8 0\n0.64 Neck 0 -2 0\n0.64 Head 2.9 -3.3 0\n0.66 Spine1 2 -2.6 1\n0.66 Spine2 2 -4.7 1\n0.66 RightShoulder -6 0 -8\n0.66 RightArm -28.1 30 0\n0.66 RightForeArm -1.7 -12.8 0\n0.66 Neck 0 -2 0\n0.66 Head 3 -3.7 0\n0.67 Spine1 2 -2.5 1.1\n0.67 Spine2 2 -4.6 1.1\n0.67 RightShoulder -5.9 0 -8\n0.67 RightArm -28 30 0\n0.67 RightForeArm -1.1 -13.6 0\n0.67 Neck 0 -2 0\n0.67 Head 3 -3.8 0\n0.7 Spine1 2 -2 1.1\n0.7 Spine2 2 -3.9 1.2\n0.7 RightShoulder -5.8 0 -8\n0.7 RightArm -28 30.7 0\n0.7 RightForeArm -0.1 -14.9 0\n0.7 Neck 0 -1.8 0\n0.7 Head 3 -4 0\n0.71 Spine1 2 -1.8 1.2\n0.71 Spine2 2 -3.6 1.2\n0.71 RightShoulder -5.7 0 -8\n0.71 RightArm -28 31.2 0\n0.71 RightForeArm 0 -15 0\n0.71 Neck 0 -1.7 0\n0.71 Head 3 -4 0\n1.05 Spine1 2 5 2\n1.05 Spine2 2 7.9 3\n1.05 RightShoulder -2.1 0 -8\n1.05 RightArm -29.9 81.4 0\n1.05 RightForeArm -4.2 57.2 0\n1.05 Neck 0 7.5 0\n1.05 Head 3.9 17 0\n1.07 Spine1 2 5 2\n1.07 Spine2 2 8 3\n1.07 RightShoulder -2 0 -8\n1.07 RightArm -29.9 83.1 0\n1.07 RightForeArm -4.5 61.2 0\n1.07 Neck 0 7.8 0\n1.07 Head 3.9 18 0\n1.09 Spine1 2 4.9 2\n1.09 Spine2 2 8 3\n1.09 RightShoulder -2 0 -8\n1.09 RightArm -30 84.3 0\n1.09 RightForeArm -4.7 64.5 0\n1.09 Neck 0 7.9 0\n1.09 Head 4 18.8 0\n1.11 Spine1 1.9 4.9 1.9\n1.11 Spine2 2 7.9 3\n1.11 RightShoulder -2 0 -8\n1.11 RightArm -30 84.9 0\n1.11 RightForeArm -4.8 67.1 0\n1.11 Neck 0 8 0\n1.11 Head 4 19.5 0\n1.12 Spine1 1.9 4.8 1.9\n1.12 Spine2 2 7.8 3\n1.12 RightShoulder -2 0 -7.9\n1.12 RightArm -30 85 0\n1.12 RightForeArm -4.9 68.1 0\n1.12 Neck 0 8 0\n1.12 Head 4 19.7 0\n1.15 Spine1 1.8 4.7 1.8\n1.15 Spine2 1.9 7.6 2.9\n1.15 RightShoulder -1.9 0 -7.8\n1.15 RightArm -30.4 84.4 0\n1.15 RightForeArm -5 69.9 0\n1.15 Neck 0 7.9 0\n1.15 Head 4 20 0\n1.16 Spine1 1.8 4.6 1.8\n1.16 Spine2 1.9 7.5 2.9\n1.16 RightShoulder -1.9 0 -7.7\n1.16 RightArm -30.8 84 0\n1.16 RightForeArm -5 70 0\n1.16 Neck 0 7.8 0\n1.16 Head 4 20 0\n1.45 Spine1 1 3 1\n1.45 Spine2 1 4 2\n1.45 RightShoulder -1 0 -4.1\n1.45 RightArm -55.7 52.8 0\n1.45 RightForeArm -25.4 45.6 0\n1.45 Neck 0 4.2 0\n1.45 Head 2.3 11.6 0\n1.47 Spine1 1 3 1\n1.47 Spine2 1 4 2\n1.47 RightShoulder -1 0 -4\n1.47 RightArm -56.8 51.5 0\n1.47 RightForeArm -26.8 43.9 0\n1.47 Neck 0 4.1 0\n1.47 Head 2.2 11 0\n1.49 Spine1 1 2.9 1\n1.49 Spine2 1 4 2\n1.49 RightShoulder -1 0 -4\n1.49 RightArm -57.6 50.6 0\n1.49 RightForeArm -28 42.4 0\n1.49 Neck 0 4 0\n1.49 Head 2.1 10.6 0\n1.51 Spine1 1 2.9 1\n1.51 Spine2 1 3.9 2\n1.51 RightShoulder -1 0 -4\n1.51 RightArm -57.9 50.1 0\n1.51 RightForeArm -28.9 41.3 0\n1.51 Neck 0 4 0\n1.51 Head 2.1 10.3 0\n1.52 Spine1 0.9 2.8 0.9\n1.52 Spine2 1 3.9 1.9\n1.52 RightShoulder -1 0 -3.9\n1.52 RightArm -58 50 0\n1.52 RightForeArm -29.3 40.8 0\n1.52 Neck 0 4 0\n1.52 Head 2 10.2 0\n1.55 Spine1 0.9 2.6 0.9\n1.55 Spine2 0.9 3.6 1.8\n1.55 RightShoulder -0.9 0 -3.8\n1.55 RightArm -57.9 52.1 0\n1.55 RightForeArm -30 40.1 0\n1.55 Neck 0 3.9 0\n1.55 Head 2 10 0\n1.56 Spine1 0.8 2.5 0.8\n1.56 Spine2 0.9 3.5 1.8\n1.56 RightShoulder -0.9 0 -3.7\n1.56 RightArm -57.8 53.6 0\n1.56 RightForeArm -30 40 0\n1.56 Neck 0 3.8 0\n1.56 Head 2 10 0\n1.9 Spine1 0 0 0\n1.9 Spine2 0 0 0\n1.9 RightShoulder 0 0 0\n1.9 RightArm -50 167 0\n1.9 RightForeArm -66 84 0\n1.9 Neck 0 0 0\n1.9 Head 0 0 0\n",
            "clip order_follow_l\nlength 1.9\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.25 Spine1 1 -2 -1\n0.25 Spine2 1 -3 -1\n0.25 LeftShoulder -1.9 0 3.8\n0.25 LeftArm -61.2 -43.1 0\n0.25 LeftForeArm -34.2 -19.2 0\n0.25 Neck 0 -2.7 0\n0.25 Head 1.6 -4 0\n0.27 Spine1 1 -2 -1\n0.27 Spine2 1 -3 -1\n0.27 LeftShoulder -2 0 3.9\n0.27 LeftArm -60.7 -39.3 0\n0.27 LeftForeArm -33 -20.9 0\n0.27 Neck 0 -2.9 0\n0.27 Head 1.7 -4.3 0\n0.29 Spine1 1 -1.8 -1\n0.29 Spine2 1 -2.9 -1\n0.29 LeftShoulder -2 0 4\n0.29 LeftArm -60.2 -36.6 0\n0.29 LeftForeArm -31.9 -22.4 0\n0.29 Neck 0 -3 0\n0.29 Head 1.8 -4.6 0\n0.31 Spine1 1.1 -1.6 -1\n0.31 Spine2 1 -2.7 -1\n0.31 LeftShoulder -2 0 4\n0.31 LeftArm -60 -35.2 0\n0.31 LeftForeArm -31 -23.6 0\n0.31 Neck 0 -3 0\n0.31 Head 1.9 -4.8 0\n0.32 Spine1 1.1 -1.5 -1\n0.32 Spine2 1.1 -2.6 -1\n0.32 LeftShoulder -2.1 0 4.1\n0.32 LeftArm -60 -35 0\n0.32 LeftForeArm -30.7 -24.1 0\n0.32 Neck 0 -3 0\n0.32 Head 2 -4.9 0\n0.35 Spine1 1.2 -1 -1\n0.35 Spine2 1.1 -1.9 -1\n0.35 LeftShoulder -2.3 0 4.3\n0.35 LeftArm -59.3 -34.9 0\n0.35 LeftForeArm -30 -24.9 0\n0.35 Neck 0 -2.8 0\n0.35 Head 2 -5 0\n0.36 Spine1 1.2 -0.8 -1\n0.36 Spine2 1.2 -1.7 -1\n0.36 LeftShoulder -2.4 0 4.4\n0.36 LeftArm -58.8 -34.8 0\n0.36 LeftForeArm -30 -25 0\n0.36 Neck 0 -2.7 0\n0.36 Head 2 -5 0\n0.6 Spine1 2 3 -1\n0.6 Spine2 2 4.9 -1\n0.6 LeftShoulder -5.9 0 7.9\n0.6 LeftArm -31.3 -30.5 0\n0.6 LeftForeArm -7 5.6 0\n0.6 Neck 0 1.6 0\n0.6 Head 2.8 2.2 0\n0.62 Spine1 2 3 -1\n0.62 Spine2 2 5 -1\n0.62 LeftShoulder -6 0 8\n0.62 LeftArm -29.8 -30.3 0\n0.62 LeftForeArm -4.9 8.4 0\n0.62 Neck 0 1.8 0\n0.62 Head 2.9 2.8 0\n0.64 Spine1 2 2.8 -1\n0.64 Spine2 2 4.9 -1\n0.64 LeftShoulder -6 0 8\n0.64 LeftArm -28.7 -30.1 0\n0.64 LeftForeArm -3.1 10.8 0\n0.64 Neck 0 2 0\n0.64 Head 2.9 3.3 0\n0.66 Spine1 2 2.6 -1\n0.66 Spine2 2 4.7 -1\n0.66 LeftShoulder -6 0 8\n0.66 LeftArm -28.1 -30 0\n0.66 LeftForeArm -1.7 12.8 0\n0.66 Neck 0 2 0\n0.66 Head 3 3.7 0\n0.67 Spine1 2 2.5 -1.1\n0.67 Spine2 2 4.6 -1.1\n0.67 LeftShoulder -5.9 0 8\n0.67 LeftArm -28 -30 0\n0.67 LeftForeArm -1.1 13.6 0\n0.67 Neck 0 2 0\n0.67 Head 3 3.8 0\n0.7 Spine1 2 2 -1.1\n0.7 Spine2 2 3.9 -1.2\n0.7 LeftShoulder -5.8 0 8\n0.7 LeftArm -28 -30.7 0\n0.7 LeftForeArm -0.1 14.9 0\n0.7 Neck 0 1.8 0\n0.7 Head 3 4 0\n0.71 Spine1 2 1.8 -1.2\n0.71 Spine2 2 3.6 -1.2\n0.71 LeftShoulder -5.7 0 8\n0.71 LeftArm -28 -31.2 0\n0.71 LeftForeArm 0 15 0\n0.71 Neck 0 1.7 0\n0.71 Head 3 4 0\n1.05 Spine1 2 -5 -2\n1.05 Spine2 2 -7.9 -3\n1.05 LeftShoulder -2.1 0 8\n1.05 LeftArm -29.9 -81.4 0\n1.05 LeftForeArm -4.2 -57.2 0\n1.05 Neck 0 -7.5 0\n1.05 Head 3.9 -17 0\n1.07 Spine1 2 -5 -2\n1.07 Spine2 2 -8 -3\n1.07 LeftShoulder -2 0 8\n1.07 LeftArm -29.9 -83.1 0\n1.07 LeftForeArm -4.5 -61.2 0\n1.07 Neck 0 -7.8 0\n1.07 Head 3.9 -18 0\n1.09 Spine1 2 -4.9 -2\n1.09 Spine2 2 -8 -3\n1.09 LeftShoulder -2 0 8\n1.09 LeftArm -30 -84.3 0\n1.09 LeftForeArm -4.7 -64.5 0\n1.09 Neck 0 -7.9 0\n1.09 Head 4 -18.8 0\n1.11 Spine1 1.9 -4.9 -1.9\n1.11 Spine2 2 -7.9 -3\n1.11 LeftShoulder -2 0 8\n1.11 LeftArm -30 -84.9 0\n1.11 LeftForeArm -4.8 -67.1 0\n1.11 Neck 0 -8 0\n1.11 Head 4 -19.5 0\n1.12 Spine1 1.9 -4.8 -1.9\n1.12 Spine2 2 -7.8 -3\n1.12 LeftShoulder -2 0 7.9\n1.12 LeftArm -30 -85 0\n1.12 LeftForeArm -4.9 -68.1 0\n1.12 Neck 0 -8 0\n1.12 Head 4 -19.7 0\n1.15 Spine1 1.8 -4.7 -1.8\n1.15 Spine2 1.9 -7.6 -2.9\n1.15 LeftShoulder -1.9 0 7.8\n1.15 LeftArm -30.4 -84.4 0\n1.15 LeftForeArm -5 -69.9 0\n1.15 Neck 0 -7.9 0\n1.15 Head 4 -20 0\n1.16 Spine1 1.8 -4.6 -1.8\n1.16 Spine2 1.9 -7.5 -2.9\n1.16 LeftShoulder -1.9 0 7.7\n1.16 LeftArm -30.8 -84 0\n1.16 LeftForeArm -5 -70 0\n1.16 Neck 0 -7.8 0\n1.16 Head 4 -20 0\n1.45 Spine1 1 -3 -1\n1.45 Spine2 1 -4 -2\n1.45 LeftShoulder -1 0 4.1\n1.45 LeftArm -55.7 -52.8 0\n1.45 LeftForeArm -25.4 -45.6 0\n1.45 Neck 0 -4.2 0\n1.45 Head 2.3 -11.6 0\n1.47 Spine1 1 -3 -1\n1.47 Spine2 1 -4 -2\n1.47 LeftShoulder -1 0 4\n1.47 LeftArm -56.8 -51.5 0\n1.47 LeftForeArm -26.8 -43.9 0\n1.47 Neck 0 -4.1 0\n1.47 Head 2.2 -11 0\n1.49 Spine1 1 -2.9 -1\n1.49 Spine2 1 -4 -2\n1.49 LeftShoulder -1 0 4\n1.49 LeftArm -57.6 -50.6 0\n1.49 LeftForeArm -28 -42.4 0\n1.49 Neck 0 -4 0\n1.49 Head 2.1 -10.6 0\n1.51 Spine1 1 -2.9 -1\n1.51 Spine2 1 -3.9 -2\n1.51 LeftShoulder -1 0 4\n1.51 LeftArm -57.9 -50.1 0\n1.51 LeftForeArm -28.9 -41.3 0\n1.51 Neck 0 -4 0\n1.51 Head 2.1 -10.3 0\n1.52 Spine1 0.9 -2.8 -0.9\n1.52 Spine2 1 -3.9 -1.9\n1.52 LeftShoulder -1 0 3.9\n1.52 LeftArm -58 -50 0\n1.52 LeftForeArm -29.3 -40.8 0\n1.52 Neck 0 -4 0\n1.52 Head 2 -10.2 0\n1.55 Spine1 0.9 -2.6 -0.9\n1.55 Spine2 0.9 -3.6 -1.8\n1.55 LeftShoulder -0.9 0 3.8\n1.55 LeftArm -58.2 -50.9 0\n1.55 LeftForeArm -30 -40.1 0\n1.55 Neck 0 -3.9 0\n1.55 Head 2 -10 0\n1.56 Spine1 0.8 -2.5 -0.8\n1.56 Spine2 0.9 -3.5 -1.8\n1.56 LeftShoulder -0.9 0 3.7\n1.56 LeftArm -58.4 -51.6 0\n1.56 LeftForeArm -30 -40 0\n1.56 Neck 0 -3.8 0\n1.56 Head 2 -10 0\n1.9 Spine1 0 0 0\n1.9 Spine2 0 0 0\n1.9 LeftShoulder 0 0 0\n1.9 LeftArm -70 -101 0\n1.9 LeftForeArm -49 1 0\n1.9 Neck 0 0 0\n1.9 Head 0 0 0\n",
            "clip pet_r\nlength 3.6\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.3 Spine1 1 2 1\n0.3 Spine2 2 3 2\n0.3 RightShoulder -1 0 -3.8\n0.3 RightArm -60.9 51.9 0\n0.3 RightForeArm -32.3 35.5 0\n0.3 Neck 0.9 3.7 0\n0.3 Head 3.4 8.4 0\n0.32 Spine1 1 2 1\n0.32 Spine2 2 3 2\n0.32 RightShoulder -1 0 -4\n0.32 RightArm -61.4 46.3 0\n0.32 RightForeArm -30.1 32.3 0\n0.32 Neck 1 3.9 0\n0.32 Head 3.6 9 0\n0.34 Spine1 1 2 1\n0.34 Spine2 2 3 2\n0.34 RightShoulder -1 0 -4\n0.34 RightArm -61.8 42.4 0\n0.34 RightForeArm -28.2 29.6 0\n0.34 Neck 1 4 0\n0.34 Head 3.8 9.4 0\n0.36 Spine1 1 2.1 1.1\n0.36 Spine2 2 3.1 2.1\n0.36 RightShoulder -1 0 -4.1\n0.36 RightArm -62 40.3 0\n0.36 RightForeArm -26.7 27.4 0\n0.36 Neck 1 4 0\n0.36 Head 3.9 9.7 0\n0.37 Spine1 1.1 2.1 1.1\n0.37 Spine2 2 3.1 2.1\n0.37 RightShoulder -1 0 -4.1\n0.37 RightArm -62 40 0\n0.37 RightForeArm -26.1 26.6 0\n0.37 Neck 1 4 0\n0.37 Head 3.9 9.8 0\n0.4 Spine1 1.1 2.3 1.3\n0.4 Spine2 2.1 3.3 2.3\n0.4 RightShoulder -1 0 -4.4\n0.4 RightArm -61.5 40.3 0\n0.4 RightForeArm -25.1 25.1 0\n0.4 Neck 1.1 4.1 0\n0.4 Head 4 10 0\n0.41 Spine1 1.2 2.3 1.3\n0.41 Spine2 2.1 3.3 2.3\n0.41 RightShoulder -1.1 0 -4.6\n0.41 RightArm -61.1 40.4 0\n0.41 RightForeArm -25 25 0\n0.41 Neck 1.1 4.2 0\n0.41 Head 4 10 0\n0.75 Spine1 2 4 3\n0.75 Spine2 3 6 5\n0.75 RightShoulder -2 0 -12.8\n0.75 RightArm -22.7 58.7 0\n0.75 RightForeArm 52.3 22.5 0\n0.75 Neck 3.9 9.7 0\n0.75 Head 11 24 0\n0.77 Spine1 2 4 3\n0.77 Spine2 3 6 5\n0.77 RightShoulder -2 0 -12.9\n0.77 RightArm -21.4 59.3 0\n0.77 RightForeArm 56.5 22.3 0\n0.77 Neck 3.9 9.9 0\n0.77 Head 11.3 24.7 0\n0.79 Spine1 2 4 3\n0.79 Spine2 3 6 5\n0.79 RightShoulder -2 0 -13\n0.79 RightArm -20.5 59.7 0\n0.79 RightForeArm 60.1 22.2 0\n0.79 Neck 4 10 0\n0.79 Head 11.6 25.2 0\n0.81 Spine1 2 4 3\n0.81 Spine2 3 6 5\n0.81 RightShoulder -2 0 -13\n0.81 RightArm -20.1 60 0\n0.81 RightForeArm 62.9 22.1 0\n0.81 Neck 4 10 0\n0.81 Head 11.8 25.6 0\n0.82 Spine1 2 4 3\n0.82 Spine2 3 6 5\n0.82 RightShoulder -2 0 -13\n0.82 RightArm -20 60 0\n0.82 RightForeArm 64 22.1 0\n0.82 Neck 4 10 0\n0.82 Head 11.9 25.8 0\n0.85 Spine1 2 4 3\n0.85 Spine2 3 6 5\n0.85 RightShoulder -2 0 -13\n0.85 RightArm -20 59.9 0\n0.85 RightForeArm 65.9 22 0\n0.85 Neck 4 10 0\n0.85 Head 12 26 0\n0.86 Spine1 2 4 3\n0.86 Spine2 3 6 5\n0.86 RightShoulder -2.1 0 -13.1\n0.86 RightArm -19.9 59.9 0\n0.86 RightForeArm 66 22 0\n0.86 Neck 4 10 0\n0.86 Head 12 26 0\n1.25 Spine1 2 4 3\n1.25 Spine2 3 6 5\n1.25 RightShoulder -3 0 -14\n1.25 RightArm -16.2 52.4 0\n1.25 RightForeArm 69.5 6.2 0\n1.25 Neck 4 10 0\n1.25 Head 12 26.9 0\n1.27 Spine1 2 4 3\n1.27 Spine2 3 6 5\n1.27 RightShoulder -3 0 -14\n1.27 RightArm -16.1 52.2 0\n1.27 RightForeArm 69.7 5.5 0\n1.27 Neck 4 10 0\n1.27 Head 12 26.9 0\n1.29 Spine1 2 4 3\n1.29 Spine2 3 6 5\n1.29 RightShoulder -3 0 -14\n1.29 RightArm -16 52.1 0\n1.29 RightForeArm 69.8 5 0\n1.29 Neck 4 10 0\n1.29 Head 12 27 0\n1.31 Spine1 2 4 3\n1.31 Spine2 3 6 5\n1.31 RightShoulder -3 0 -14\n1.31 RightArm -16 52 0\n1.31 RightForeArm 69.9 4.5 0\n1.31 Neck 4 10 0\n1.31 Head 12 27 0\n1.32 Spine1 2 4 3\n1.32 Spine2 3 6 5\n1.32 RightShoulder -3 0 -14\n1.32 RightArm -16 52 0\n1.32 RightForeArm 69.9 4.3 0\n1.32 Neck 4 10 0\n1.32 Head 12 27 0\n1.35 Spine1 2 4 3\n1.35 Spine2 3 6 5\n1.35 RightShoulder -3 0 -14\n1.35 RightArm -16 52.1 0\n1.35 RightForeArm 70 4 0\n1.35 Neck 4 10 0\n1.35 Head 12 27 0\n1.36 Spine1 2 4 3\n1.36 Spine2 3 6 5\n1.36 RightShoulder -2.9 0 -13.9\n1.36 RightArm -16.1 52.1 0\n1.36 RightForeArm 70 4 0\n1.36 Neck 4 10 0\n1.36 Head 12 27 0\n1.75 Spine1 2 4 3\n1.75 Spine2 3 6 5\n1.75 RightShoulder -2 0 -13\n1.75 RightArm -19.8 59.6 0\n1.75 RightForeArm 66.5 19.8 0\n1.75 Neck 4 10 0\n1.75 Head 12 26.1 0\n1.77 Spine1 2 4 3\n1.77 Spine2 3 6 5\n1.77 RightShoulder -2 0 -13\n1.77 RightArm -19.9 59.8 0\n1.77 RightForeArm 66.3 20.5 0\n1.77 Neck 4 10 0\n1.77 Head 12 26.1 0\n1.79 Spine1 2 4 3\n1.79 Spine2 3 6 5\n1.79 RightShoulder -2 0 -13\n1.79 RightArm -20 59.9 0\n1.79 RightForeArm 66.2 21 0\n1.79 Neck 4 10 0\n1.79 Head 12 26 0\n1.81 Spine1 2 4 3\n1.81 Spine2 3 6 5\n1.81 RightShoulder -2 0 -13\n1.81 RightArm -20 60 0\n1.81 RightForeArm 66.1 21.5 0\n1.81 Neck 4 10 0\n1.81 Head 12 26 0\n1.82 Spine1 2 4 3\n1.82 Spine2 3 6 5\n1.82 RightShoulder -2 0 -13\n1.82 RightArm -20 60 0\n1.82 RightForeArm 66.1 21.7 0\n1.82 Neck 4 10 0\n1.82 Head 12 26 0\n1.85 Spine1 2 4 3\n1.85 Spine2 3 6 5\n1.85 RightShoulder -2 0 -13\n1.85 RightArm -20 59.9 0\n1.85 RightForeArm 66 22 0\n1.85 Neck 4 10 0\n1.85 Head 12 26 0\n1.86 Spine1 2 4 3\n1.86 Spine2 3 6 5\n1.86 RightShoulder -2.1 0 -13.1\n1.86 RightArm -19.9 59.9 0\n1.86 RightForeArm 66 22 0\n1.86 Neck 4 10 0\n1.86 Head 12 26 0\n2.25 Spine1 2 4 3\n2.25 Spine2 3 6 5\n2.25 RightShoulder -3 0 -14\n2.25 RightArm -16.2 52.4 0\n2.25 RightForeArm 69.5 6.2 0\n2.25 Neck 4 10 0\n2.25 Head 12 26.9 0\n2.27 Spine1 2 4 3\n2.27 Spine2 3 6 5\n2.27 RightShoulder -3 0 -14\n2.27 RightArm -16.1 52.2 0\n2.27 RightForeArm 69.7 5.5 0\n2.27 Neck 4 10 0\n2.27 Head 12 26.9 0\n2.29 Spine1 2 4 3\n2.29 Spine2 3 6 5\n2.29 RightShoulder -3 0 -14\n2.29 RightArm -16 52.1 0\n2.29 RightForeArm 69.8 5 0\n2.29 Neck 4 10 0\n2.29 Head 12 27 0\n2.31 Spine1 2 4 3\n2.31 Spine2 3 6 5\n2.31 RightShoulder -3 0 -14\n2.31 RightArm -16 52 0\n2.31 RightForeArm 69.9 4.5 0\n2.31 Neck 4 10 0\n2.31 Head 12 27 0\n2.32 Spine1 2 4 3\n2.32 Spine2 3 6 5\n2.32 RightShoulder -3 0 -14\n2.32 RightArm -16 52 0\n2.32 RightForeArm 69.9 4.3 0\n2.32 Neck 4 10 0\n2.32 Head 12 27 0\n2.35 Spine1 2 4 3\n2.35 Spine2 3 6 5\n2.35 RightShoulder -3 0 -14\n2.35 RightArm -16.1 52.1 0\n2.35 RightForeArm 70 4 0\n2.35 Neck 4 10 0\n2.35 Head 12 27 0\n2.36 Spine1 2 4 3\n2.36 Spine2 3 6 5\n2.36 RightShoulder -2.9 0 -13.9\n2.36 RightArm -16.1 52.2 0\n2.36 RightForeArm 70 4 0\n2.36 Neck 4 10 0\n2.36 Head 12 27 0\n2.7 Spine1 2 4 3\n2.7 Spine2 3 6 5\n2.7 RightShoulder -2 0 -13\n2.7 RightArm -19.7 59.5 0\n2.7 RightForeArm 66.6 19.3 0\n2.7 Neck 4 10 0\n2.7 Head 12 26.1 0\n2.72 Spine1 2 4 3\n2.72 Spine2 3 6 5\n2.72 RightShoulder -2 0 -13\n2.72 RightArm -19.9 59.7 0\n2.72 RightForeArm 66.4 20.1 0\n2.72 Neck 4 10 0\n2.72 Head 12 26.1 0\n2.74 Spine1 2 4 3\n2.74 Spine2 3 6 5\n2.74 RightShoulder -2 0 -13\n2.74 RightArm -19.9 59.9 0\n2.74 RightForeArm 66.3 20.8 0\n2.74 Neck 4 10 0\n2.74 Head 12 26 0\n2.76 Spine1 2 3.9 2.9\n2.76 Spine2 3 5.9 4.9\n2.76 RightShoulder -2 0 -12.9\n2.76 RightArm -20 60 0\n2.76 RightForeArm 66.1 21.4 0\n2.76 Neck 4 10 0\n2.76 Head 12 26 0\n2.77 Spine1 1.9 3.9 2.9\n2.77 Spine2 3 5.9 4.9\n2.77 RightShoulder -2 0 -12.9\n2.77 RightArm -20 60 0\n2.77 RightForeArm 66.1 21.6 0\n2.77 Neck 4 10 0\n2.77 Head 12 26 0\n2.8 Spine1 1.9 3.7 2.7\n2.8 Spine2 2.9 5.7 4.7\n2.8 RightShoulder -2 0 -12.6\n2.8 RightArm -20.5 59.7 0\n2.8 RightForeArm 66 22 0\n2.8 Neck 3.9 9.9 0\n2.8 Head 12 26 0\n2.81 Spine1 1.8 3.7 2.7\n2.81 Spine2 2.9 5.7 4.7\n2.81 RightShoulder -1.9 0 -12.4\n2.81 RightArm -20.9 59.6 0\n2.81 RightForeArm 66 22 0\n2.81 Neck 3.9 9.8 0\n2.81 Head 12 26 0\n3.15 Spine1 1 2 1\n3.15 Spine2 2 3 2\n3.15 RightShoulder -1 0 -4.2\n3.15 RightArm -59.3 41.3 0\n3.15 RightForeArm -11.3 24.5 0\n3.15 Neck 1.1 4.3 0\n3.15 Head 5 10.3 0\n3.17 Spine1 1 2 1\n3.17 Spine2 2 3 2\n3.17 RightShoulder -1 0 -4.1\n3.17 RightArm -60.6 40.7 0\n3.17 RightForeArm -15.5 24.7 0\n3.17 Neck 1.1 4.1 0\n3.17 Head 4.7 9.5 0\n3.19 Spine1 1 2 1\n3.19 Spine2 2 3 2\n3.19 RightShoulder -1 0 -4\n3.19 RightArm -61.5 40.3 0\n3.19 RightForeArm -19.1 24.8 0\n3.19 Neck 1 4 0\n3.19 Head 4.4 8.9 0\n3.21 Spine1 1 1.9 1\n3.21 Spine2 2 2.9 2\n3.21 RightShoulder -1 0 -4\n3.21 RightArm -61.9 40 0\n3.21 RightForeArm -21.9 24.9 0\n3.21 Neck 1 4 0\n3.21 Head 4.2 8.4 0\n3.22 Spine1 0.9 1.9 0.9\n3.22 Spine2 1.9 2.9 1.9\n3.22 RightShoulder -1 0 -3.9\n3.22 RightArm -62 40 0\n3.22 RightForeArm -23 24.9 0\n3.22 Neck 1 4 0\n3.22 Head 4.1 8.2 0\n3.25 Spine1 0.9 1.7 0.9\n3.25 Spine2 1.8 2.7 1.8\n3.25 RightShoulder -0.9 0 -3.8\n3.25 RightArm -61.8 42.2 0\n3.25 RightForeArm -24.9 25 0\n3.25 Neck 1 3.9 0\n3.25 Head 4 8 0\n3.26 Spine1 0.8 1.7 0.8\n3.26 Spine2 1.8 2.7 1.8\n3.26 RightShoulder -0.9 0 -3.7\n3.26 RightArm -61.6 43.9 0\n3.26 RightForeArm -25 25 0\n3.26 Neck 1 3.8 0\n3.26 Head 4 8 0\n3.6 Spine1 0 0 0\n3.6 Spine2 0 0 0\n3.6 RightShoulder 0 0 0\n3.6 RightArm -50 167 0\n3.6 RightForeArm -66 84 0\n3.6 Neck 0 0 0\n3.6 Head 0 0 0\n",
            "clip pet_l\nlength 3.6\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.3 Spine1 1 -2 -1\n0.3 Spine2 2 -3 -2\n0.3 LeftShoulder -1 0 3.8\n0.3 LeftArm -62.8 -45.7 0\n0.3 LeftForeArm -29.3 -20.4 0\n0.3 Neck 0.9 -3.7 0\n0.3 Head 3.4 -8.4 0\n0.32 Spine1 1 -2 -1\n0.32 Spine2 2 -3 -2\n0.32 LeftShoulder -1 0 4\n0.32 LeftArm -62.4 -43 0\n0.32 LeftForeArm -28 -21.8 0\n0.32 Neck 1 -3.9 0\n0.32 Head 3.6 -9 0\n0.34 Spine1 1 -2 -1\n0.34 Spine2 2 -3 -2\n0.34 LeftShoulder -1 0 4\n0.34 LeftArm -62.1 -41.1 0\n0.34 LeftForeArm -26.9 -23 0\n0.34 Neck 1 -4 0\n0.34 Head 3.8 -9.4 0\n0.36 Spine1 1 -2.1 -1.1\n0.36 Spine2 2 -3.1 -2.1\n0.36 LeftShoulder -1 0 4.1\n0.36 LeftArm -62 -40.1 0\n0.36 LeftForeArm -26 -23.9 0\n0.36 Neck 1 -4 0\n0.36 Head 3.9 -9.7 0\n0.37 Spine1 1.1 -2.1 -1.1\n0.37 Spine2 2 -3.1 -2.1\n0.37 LeftShoulder -1 0 4.1\n0.37 LeftArm -62 -40 0\n0.37 LeftForeArm -25.6 -24.3 0\n0.37 Neck 1 -4 0\n0.37 Head 3.9 -9.8 0\n0.4 Spine1 1.1 -2.3 -1.3\n0.4 Spine2 2.1 -3.3 -2.3\n0.4 LeftShoulder -1 0 4.4\n0.4 LeftArm -61.5 -40.3 0\n0.4 LeftForeArm -25 -25 0\n0.4 Neck 1.1 -4.1 0\n0.4 Head 4 -10 0\n0.41 Spine1 1.2 -2.3 -1.3\n0.41 Spine2 2.1 -3.3 -2.3\n0.41 LeftShoulder -1.1 0 4.6\n0.41 LeftArm -61.1 -40.4 0\n0.41 LeftForeArm -25 -25 0\n0.41 Neck 1.1 -4.2 0\n0.41 Head 4 -10 0\n0.75 Spine1 2 -4 -3\n0.75 Spine2 3 -6 -5\n0.75 LeftShoulder -2 0 12.8\n0.75 LeftArm -22.7 -58.7 0\n0.75 LeftForeArm 52.3 -22.5 0\n0.75 Neck 3.9 -9.7 0\n0.75 Head 11 -24 0\n0.77 Spine1 2 -4 -3\n0.77 Spine2 3 -6 -5\n0.77 LeftShoulder -2 0 12.9\n0.77 LeftArm -21.4 -59.3 0\n0.77 LeftForeArm 56.5 -22.3 0\n0.77 Neck 3.9 -9.9 0\n0.77 Head 11.3 -24.7 0\n0.79 Spine1 2 -4 -3\n0.79 Spine2 3 -6 -5\n0.79 LeftShoulder -2 0 13\n0.79 LeftArm -20.5 -59.7 0\n0.79 LeftForeArm 60.1 -22.2 0\n0.79 Neck 4 -10 0\n0.79 Head 11.6 -25.2 0\n0.81 Spine1 2 -4 -3\n0.81 Spine2 3 -6 -5\n0.81 LeftShoulder -2 0 13\n0.81 LeftArm -20.1 -60 0\n0.81 LeftForeArm 62.9 -22.1 0\n0.81 Neck 4 -10 0\n0.81 Head 11.8 -25.6 0\n0.82 Spine1 2 -4 -3\n0.82 Spine2 3 -6 -5\n0.82 LeftShoulder -2 0 13\n0.82 LeftArm -20 -60 0\n0.82 LeftForeArm 64 -22.1 0\n0.82 Neck 4 -10 0\n0.82 Head 11.9 -25.8 0\n0.85 Spine1 2 -4 -3\n0.85 Spine2 3 -6 -5\n0.85 LeftShoulder -2 0 13\n0.85 LeftArm -20 -59.9 0\n0.85 LeftForeArm 65.9 -22 0\n0.85 Neck 4 -10 0\n0.85 Head 12 -26 0\n0.86 Spine1 2 -4 -3\n0.86 Spine2 3 -6 -5\n0.86 LeftShoulder -2.1 0 13.1\n0.86 LeftArm -19.9 -59.9 0\n0.86 LeftForeArm 66 -22 0\n0.86 Neck 4 -10 0\n0.86 Head 12 -26 0\n1.25 Spine1 2 -4 -3\n1.25 Spine2 3 -6 -5\n1.25 LeftShoulder -3 0 14\n1.25 LeftArm -16.2 -52.4 0\n1.25 LeftForeArm 69.5 -6.2 0\n1.25 Neck 4 -10 0\n1.25 Head 12 -26.9 0\n1.27 Spine1 2 -4 -3\n1.27 Spine2 3 -6 -5\n1.27 LeftShoulder -3 0 14\n1.27 LeftArm -16.1 -52.2 0\n1.27 LeftForeArm 69.7 -5.5 0\n1.27 Neck 4 -10 0\n1.27 Head 12 -26.9 0\n1.29 Spine1 2 -4 -3\n1.29 Spine2 3 -6 -5\n1.29 LeftShoulder -3 0 14\n1.29 LeftArm -16 -52.1 0\n1.29 LeftForeArm 69.8 -5 0\n1.29 Neck 4 -10 0\n1.29 Head 12 -27 0\n1.31 Spine1 2 -4 -3\n1.31 Spine2 3 -6 -5\n1.31 LeftShoulder -3 0 14\n1.31 LeftArm -16 -52 0\n1.31 LeftForeArm 69.9 -4.5 0\n1.31 Neck 4 -10 0\n1.31 Head 12 -27 0\n1.32 Spine1 2 -4 -3\n1.32 Spine2 3 -6 -5\n1.32 LeftShoulder -3 0 14\n1.32 LeftArm -16 -52 0\n1.32 LeftForeArm 69.9 -4.3 0\n1.32 Neck 4 -10 0\n1.32 Head 12 -27 0\n1.35 Spine1 2 -4 -3\n1.35 Spine2 3 -6 -5\n1.35 LeftShoulder -3 0 14\n1.35 LeftArm -16 -52.1 0\n1.35 LeftForeArm 70 -4 0\n1.35 Neck 4 -10 0\n1.35 Head 12 -27 0\n1.36 Spine1 2 -4 -3\n1.36 Spine2 3 -6 -5\n1.36 LeftShoulder -2.9 0 13.9\n1.36 LeftArm -16.1 -52.1 0\n1.36 LeftForeArm 70 -4 0\n1.36 Neck 4 -10 0\n1.36 Head 12 -27 0\n1.75 Spine1 2 -4 -3\n1.75 Spine2 3 -6 -5\n1.75 LeftShoulder -2 0 13\n1.75 LeftArm -19.8 -59.6 0\n1.75 LeftForeArm 66.5 -19.8 0\n1.75 Neck 4 -10 0\n1.75 Head 12 -26.1 0\n1.77 Spine1 2 -4 -3\n1.77 Spine2 3 -6 -5\n1.77 LeftShoulder -2 0 13\n1.77 LeftArm -19.9 -59.8 0\n1.77 LeftForeArm 66.3 -20.5 0\n1.77 Neck 4 -10 0\n1.77 Head 12 -26.1 0\n1.79 Spine1 2 -4 -3\n1.79 Spine2 3 -6 -5\n1.79 LeftShoulder -2 0 13\n1.79 LeftArm -20 -59.9 0\n1.79 LeftForeArm 66.2 -21 0\n1.79 Neck 4 -10 0\n1.79 Head 12 -26 0\n1.81 Spine1 2 -4 -3\n1.81 Spine2 3 -6 -5\n1.81 LeftShoulder -2 0 13\n1.81 LeftArm -20 -60 0\n1.81 LeftForeArm 66.1 -21.5 0\n1.81 Neck 4 -10 0\n1.81 Head 12 -26 0\n1.82 Spine1 2 -4 -3\n1.82 Spine2 3 -6 -5\n1.82 LeftShoulder -2 0 13\n1.82 LeftArm -20 -60 0\n1.82 LeftForeArm 66.1 -21.7 0\n1.82 Neck 4 -10 0\n1.82 Head 12 -26 0\n1.85 Spine1 2 -4 -3\n1.85 Spine2 3 -6 -5\n1.85 LeftShoulder -2 0 13\n1.85 LeftArm -20 -59.9 0\n1.85 LeftForeArm 66 -22 0\n1.85 Neck 4 -10 0\n1.85 Head 12 -26 0\n1.86 Spine1 2 -4 -3\n1.86 Spine2 3 -6 -5\n1.86 LeftShoulder -2.1 0 13.1\n1.86 LeftArm -19.9 -59.9 0\n1.86 LeftForeArm 66 -22 0\n1.86 Neck 4 -10 0\n1.86 Head 12 -26 0\n2.25 Spine1 2 -4 -3\n2.25 Spine2 3 -6 -5\n2.25 LeftShoulder -3 0 14\n2.25 LeftArm -16.2 -52.4 0\n2.25 LeftForeArm 69.5 -6.2 0\n2.25 Neck 4 -10 0\n2.25 Head 12 -26.9 0\n2.27 Spine1 2 -4 -3\n2.27 Spine2 3 -6 -5\n2.27 LeftShoulder -3 0 14\n2.27 LeftArm -16.1 -52.2 0\n2.27 LeftForeArm 69.7 -5.5 0\n2.27 Neck 4 -10 0\n2.27 Head 12 -26.9 0\n2.29 Spine1 2 -4 -3\n2.29 Spine2 3 -6 -5\n2.29 LeftShoulder -3 0 14\n2.29 LeftArm -16 -52.1 0\n2.29 LeftForeArm 69.8 -5 0\n2.29 Neck 4 -10 0\n2.29 Head 12 -27 0\n2.31 Spine1 2 -4 -3\n2.31 Spine2 3 -6 -5\n2.31 LeftShoulder -3 0 14\n2.31 LeftArm -16 -52 0\n2.31 LeftForeArm 69.9 -4.5 0\n2.31 Neck 4 -10 0\n2.31 Head 12 -27 0\n2.32 Spine1 2 -4 -3\n2.32 Spine2 3 -6 -5\n2.32 LeftShoulder -3 0 14\n2.32 LeftArm -16 -52 0\n2.32 LeftForeArm 69.9 -4.3 0\n2.32 Neck 4 -10 0\n2.32 Head 12 -27 0\n2.35 Spine1 2 -4 -3\n2.35 Spine2 3 -6 -5\n2.35 LeftShoulder -3 0 14\n2.35 LeftArm -16.1 -52.1 0\n2.35 LeftForeArm 70 -4 0\n2.35 Neck 4 -10 0\n2.35 Head 12 -27 0\n2.36 Spine1 2 -4 -3\n2.36 Spine2 3 -6 -5\n2.36 LeftShoulder -2.9 0 13.9\n2.36 LeftArm -16.1 -52.2 0\n2.36 LeftForeArm 70 -4 0\n2.36 Neck 4 -10 0\n2.36 Head 12 -27 0\n2.7 Spine1 2 -4 -3\n2.7 Spine2 3 -6 -5\n2.7 LeftShoulder -2 0 13\n2.7 LeftArm -19.7 -59.5 0\n2.7 LeftForeArm 66.6 -19.3 0\n2.7 Neck 4 -10 0\n2.7 Head 12 -26.1 0\n2.72 Spine1 2 -4 -3\n2.72 Spine2 3 -6 -5\n2.72 LeftShoulder -2 0 13\n2.72 LeftArm -19.9 -59.7 0\n2.72 LeftForeArm 66.4 -20.1 0\n2.72 Neck 4 -10 0\n2.72 Head 12 -26.1 0\n2.74 Spine1 2 -4 -3\n2.74 Spine2 3 -6 -5\n2.74 LeftShoulder -2 0 13\n2.74 LeftArm -19.9 -59.9 0\n2.74 LeftForeArm 66.3 -20.8 0\n2.74 Neck 4 -10 0\n2.74 Head 12 -26 0\n2.76 Spine1 2 -3.9 -2.9\n2.76 Spine2 3 -5.9 -4.9\n2.76 LeftShoulder -2 0 12.9\n2.76 LeftArm -20 -60 0\n2.76 LeftForeArm 66.1 -21.4 0\n2.76 Neck 4 -10 0\n2.76 Head 12 -26 0\n2.77 Spine1 1.9 -3.9 -2.9\n2.77 Spine2 3 -5.9 -4.9\n2.77 LeftShoulder -2 0 12.9\n2.77 LeftArm -20 -60 0\n2.77 LeftForeArm 66.1 -21.6 0\n2.77 Neck 4 -10 0\n2.77 Head 12 -26 0\n2.8 Spine1 1.9 -3.7 -2.7\n2.8 Spine2 2.9 -5.7 -4.7\n2.8 LeftShoulder -2 0 12.6\n2.8 LeftArm -20.5 -59.7 0\n2.8 LeftForeArm 66 -22 0\n2.8 Neck 3.9 -9.9 0\n2.8 Head 12 -26 0\n2.81 Spine1 1.8 -3.7 -2.7\n2.81 Spine2 2.9 -5.7 -4.7\n2.81 LeftShoulder -1.9 0 12.4\n2.81 LeftArm -20.9 -59.6 0\n2.81 LeftForeArm 66 -22 0\n2.81 Neck 3.9 -9.8 0\n2.81 Head 12 -26 0\n3.15 Spine1 1 -2 -1\n3.15 Spine2 2 -3 -2\n3.15 LeftShoulder -1 0 4.2\n3.15 LeftArm -59.3 -41.3 0\n3.15 LeftForeArm -11.3 -24.5 0\n3.15 Neck 1.1 -4.3 0\n3.15 Head 5 -10.3 0\n3.17 Spine1 1 -2 -1\n3.17 Spine2 2 -3 -2\n3.17 LeftShoulder -1 0 4.1\n3.17 LeftArm -60.6 -40.7 0\n3.17 LeftForeArm -15.5 -24.7 0\n3.17 Neck 1.1 -4.1 0\n3.17 Head 4.7 -9.5 0\n3.19 Spine1 1 -2 -1\n3.19 Spine2 2 -3 -2\n3.19 LeftShoulder -1 0 4\n3.19 LeftArm -61.5 -40.3 0\n3.19 LeftForeArm -19.1 -24.8 0\n3.19 Neck 1 -4 0\n3.19 Head 4.4 -8.9 0\n3.21 Spine1 1 -1.9 -1\n3.21 Spine2 2 -2.9 -2\n3.21 LeftShoulder -1 0 4\n3.21 LeftArm -61.9 -40 0\n3.21 LeftForeArm -21.9 -24.9 0\n3.21 Neck 1 -4 0\n3.21 Head 4.2 -8.4 0\n3.22 Spine1 0.9 -1.9 -0.9\n3.22 Spine2 1.9 -2.9 -1.9\n3.22 LeftShoulder -1 0 3.9\n3.22 LeftArm -62 -40 0\n3.22 LeftForeArm -23 -24.9 0\n3.22 Neck 1 -4 0\n3.22 Head 4.1 -8.2 0\n3.25 Spine1 0.9 -1.7 -0.9\n3.25 Spine2 1.8 -2.7 -1.8\n3.25 LeftShoulder -0.9 0 3.8\n3.25 LeftArm -62.1 -41.1 0\n3.25 LeftForeArm -24.9 -25 0\n3.25 Neck 1 -3.9 0\n3.25 Head 4 -8 0\n3.26 Spine1 0.8 -1.7 -0.8\n3.26 Spine2 1.8 -2.7 -1.8\n3.26 LeftShoulder -0.9 0 3.7\n3.26 LeftArm -62.2 -41.9 0\n3.26 LeftForeArm -25 -25 0\n3.26 Neck 1 -3.8 0\n3.26 Head 4 -8 0\n3.6 Spine1 0 0 0\n3.6 Spine2 0 0 0\n3.6 LeftShoulder 0 0 0\n3.6 LeftArm -70 -101 0\n3.6 LeftForeArm -49 1 0\n3.6 Neck 0 0 0\n3.6 Head 0 0 0\n",
            "clip pair_whirl\nlength 2.2\nloop 0\npivot 1.1\nmode Spine body\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine 0 0 0\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 RightArm -50 167 0\n0 LeftForeArm -49 1 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.34 Spine 14 1.2 0\n0.34 Spine1 16 1.4 0\n0.34 Spine2 15.86 1.39 0\n0.34 LeftShoulder 0 0 -3.88\n0.34 RightShoulder 0 0 3.88\n0.34 LeftArm -63.35 -4.56 0\n0.34 RightArm -57.38 3.72 0\n0.34 LeftForeArm -27.01 45.74 0\n0.34 RightForeArm -22.65 -34.14 0\n0.34 Neck 6.88 1.53 0\n0.34 Head 9.58 2.13 0\n0.36 Spine 14.18 1.2 0\n0.36 Spine1 16.21 1.4 0\n0.36 Spine2 16 1.4 0\n0.36 LeftShoulder 0 0 -3.97\n0.36 RightShoulder 0 0 3.97\n0.36 LeftArm -62.97 0.94 0\n0.36 RightArm -57.67 -2.74 0\n0.36 LeftForeArm -25.55 48.71 0\n0.36 RightForeArm -20.3 -40.54 0\n0.36 Neck 7.08 1.57 0\n0.36 Head 10.03 2.23 0\n0.38 Spine 14.62 1.2 0\n0.38 Spine1 16.7 1.4 0\n0.38 Spine2 16.21 1.4 0\n0.38 LeftShoulder 0 0 -4\n0.38 RightShoulder 0 0 4\n0.38 LeftArm -62.64 5.69 0\n0.38 RightArm -57.88 -7.3 0\n0.38 LeftForeArm -24.21 51.44 0\n0.38 RightForeArm -18.32 -45.96 0\n0.38 Neck 7.19 1.6 0\n0.38 Head 10.4 2.31 0\n0.39 Spine 14.88 1.2 0\n0.39 Spine1 17 1.4 0\n0.39 Spine2 16.43 1.4 0\n0.39 LeftShoulder 0 0 -4.06\n0.39 RightShoulder 0 0 4.06\n0.39 LeftArm -62.5 7.76 0\n0.39 RightArm -57.94 -8.78 0\n0.39 LeftForeArm -23.59 52.69 0\n0.39 RightForeArm -17.48 -48.25 0\n0.39 Neck 7.2 1.6 0\n0.39 Head 10.54 2.34 0\n0.41 Spine 15.37 1.2 0\n0.41 Spine1 17.57 1.4 0\n0.41 Spine2 17 1.4 0\n0.41 LeftShoulder 0 0 -4.43\n0.41 RightShoulder 0 0 4.43\n0.41 LeftArm -62.26 11.19 0\n0.41 RightArm -58 -10 0\n0.41 LeftForeArm -22.48 54.95 0\n0.41 RightForeArm -16.14 -51.9 0\n0.41 Neck 7.28 1.6 0\n0.41 Head 10.73 2.38 0\n0.43 Spine 15.7 1.2 0\n0.43 Spine1 17.94 1.4 0\n0.43 Spine2 17.57 1.4 0\n0.43 LeftShoulder 0 0 -5\n0.43 RightShoulder 0 0 5\n0.43 LeftArm -62.1 13.58 0\n0.43 RightArm -58.21 -10.52 0\n0.43 LeftForeArm -21.55 56.85 0\n0.43 RightForeArm -15.29 -54.2 0\n0.43 Neck 7.48 1.6 0\n0.43 Head 10.8 2.4 0\n0.44 Spine 15.75 1.2 0\n0.44 Spine1 18 1.4 0\n0.44 Spine2 17.79 1.4 0\n0.44 LeftShoulder 0 0 -5.3\n0.44 RightShoulder 0 0 5.3\n0.44 LeftArm -62.04 14.36 0\n0.44 RightArm -58.43 -11.08 0\n0.44 LeftForeArm -21.15 57.66 0\n0.44 RightForeArm -15.07 -54.8 0\n0.44 Neck 7.6 1.6 0\n0.44 Head 10.83 2.4 0\n0.45 Spine 15.57 1.19 0\n0.45 Spine1 17.79 1.38 0\n0.45 Spine2 17.94 1.4 0\n0.45 LeftShoulder 0 0 -5.57\n0.45 RightShoulder 0 0 5.57\n0.45 LeftArm -62.01 14.84 0\n0.45 RightArm -58.7 -11.76 0\n0.45 LeftForeArm -20.81 58.35 0\n0.45 RightForeArm -15 -55 0\n0.45 Neck 7.72 1.6 0\n0.45 Head 10.92 2.4 0\n0.46 Spine 15.05 1.15 0\n0.46 Spine1 17.2 1.34 0\n0.46 Spine2 18 1.4 0\n0.46 LeftShoulder 0 0 -5.79\n0.46 RightShoulder 0 0 5.79\n0.46 LeftArm -62 15 0\n0.46 RightArm -59 -12.5 0\n0.46 LeftForeArm -20.53 58.93 0\n0.46 RightForeArm -15.08 -55.14 0\n0.46 Neck 7.83 1.6 0\n0.46 Head 11.06 2.4 0\n0.48 Spine 13.17 1.01 0\n0.48 Spine1 15.05 1.18 0\n0.48 Spine2 17.2 1.34 0\n0.48 LeftShoulder 0 0 -6\n0.48 RightShoulder 0 0 6\n0.48 LeftArm -62.21 15.52 0\n0.48 RightArm -59.57 -13.92 0\n0.48 LeftForeArm -20.14 59.72 0\n0.48 RightForeArm -15.65 -56.08 0\n0.48 Neck 7.98 1.6 0\n0.48 Head 11.4 2.4 0\n0.49 Spine 11.87 0.92 0\n0.49 Spine1 13.56 1.07 0\n0.49 Spine2 16.27 1.27 0\n0.49 LeftShoulder 0 0 -5.78\n0.49 RightShoulder 0 0 5.75\n0.49 LeftArm -62.43 16.08 0\n0.49 RightArm -59.79 -14.48 0\n0.49 LeftForeArm -20.03 59.93 0\n0.49 RightForeArm -16.06 -56.76 0\n0.49 Neck 8 1.6 0\n0.49 Head 11.58 2.4 0\n0.5 Spine 10.38 0.81 0\n0.5 Spine1 11.87 0.95 0\n0.5 Spine2 15.05 1.18 0\n0.5 LeftShoulder 0 0 -5.16\n0.5 RightShoulder 0 0 5.05\n0.5 LeftArm -62.7 16.76 0\n0.5 RightArm -59.94 -14.86 0\n0.5 LeftForeArm -20 60 0\n0.5 RightForeArm -16.5 -57.5 0\n0.5 Neck 7.86 1.58 0\n0.5 Head 11.74 2.4 0\n0.51 Spine 8.75 0.69 0\n0.51 Spine1 10 0.81 0\n0.51 Spine2 13.56 1.07 0\n0.51 LeftShoulder 0 0 -4.18\n0.51 RightShoulder 0 0 3.93\n0.51 LeftArm -63 17.5 0\n0.51 RightArm -60 -15 0\n0.51 LeftForeArm -20.11 60.11 0\n0.51 RightForeArm -16.94 -58.24 0\n0.51 Neck 7.46 1.53 0\n0.51 Head 11.88 2.4 0\n0.53 Spine 5.18 0.43 0\n0.53 Spine1 5.92 0.51 0\n0.53 Spine2 10 0.81 0\n0.53 LeftShoulder 0 0 -1.31\n0.53 RightShoulder 0 0 0.69\n0.53 LeftArm -63.57 18.92 0\n0.53 RightArm -56.98 -12.96 0\n0.53 LeftForeArm -20.86 60.86 0\n0.53 RightForeArm -17.69 -59.48 0\n0.53 Neck 6 1.35 0\n0.53 Head 12 2.4 0\n0.55 Spine 1.47 0.17 0\n0.55 Spine1 1.68 0.19 0\n0.55 Spine2 5.92 0.51 0\n0.55 LeftShoulder 0 0 2.45\n0.55 RightShoulder 0 0 -3.58\n0.55 LeftArm -63.94 19.86 0\n0.55 RightArm -48.77 -7.41 0\n0.55 LeftForeArm -22 62 0\n0.55 RightForeArm -18 -60 0\n0.55 Neck 3.85 1.08 0\n0.55 Head 11.19 2.3 0\n0.56 Spine -0.35 0.03 0\n0.56 Spine1 -0.4 0.04 0\n0.56 Spine2 3.8 0.35 0\n0.56 LeftShoulder 0 0 4.56\n0.56 RightShoulder 0 0 -5.97\n0.56 LeftArm -64 20 0\n0.56 RightArm -43.12 -3.59 0\n0.56 LeftForeArm -22.59 62.59 0\n0.56 RightForeArm -17.35 -59.24 0\n0.56 Neck 2.59 0.92 0\n0.56 Head 10.25 2.18 0\n0.6 Spine -6.52 -0.41 0\n0.6 Spine1 -7.45 -0.48 0\n0.6 Spine2 -4.27 -0.25 0\n0.6 LeftShoulder 0 0 13.44\n0.6 RightShoulder 0 0 -16.03\n0.6 LeftArm -53.39 11.47 0\n0.6 RightArm -14.07 16.04 0\n0.6 LeftForeArm -24 64 0\n0.6 RightForeArm -3.94 -43.59 0\n0.6 Neck -3.04 0.22 0\n0.6 Head 3.89 1.39 0\n0.64 Spine -9.1 -0.6 0\n0.64 Spine1 -10.4 -0.7 0\n0.64 Spine2 -9.6 -0.64 0\n0.64 LeftShoulder 0 0 20.88\n0.64 RightShoulder 0 0 -24.46\n0.64 LeftArm -28.1 -8.86 0\n0.64 RightArm 17.57 37.43 0\n0.64 LeftForeArm -15.06 52.14 0\n0.64 RightForeArm 20.27 -15.35 0\n0.64 Neck -8.2 -0.43 0\n0.64 Head -4.55 0.33 0\n0.66 Spine -9.06 -0.61 0\n0.66 Spine1 -10.35 -0.71 0\n0.66 Spine2 -10.4 -0.7 0\n0.66 LeftShoulder 0 0 23.16\n0.66 RightShoulder 0 0 -27.05\n0.66 LeftArm -13 -21 0\n0.66 RightArm 31.13 46.59 0\n0.66 LeftForeArm -5.42 39.38 0\n0.66 RightForeArm 33.73 0.35 0\n0.66 Neck -10.03 -0.65 0\n0.66 Head -8.69 -0.19 0\n0.68 Spine -8.93 -0.62 0\n0.68 Spine1 -10.21 -0.73 0\n0.68 Spine2 -10.35 -0.71 0\n0.68 LeftShoulder 0 0 24\n0.68 RightShoulder 0 0 -28\n0.68 LeftArm 2.1 -33.14 0\n0.68 RightArm 41.44 53.57 0\n0.68 LeftForeArm 6.27 23.87 0\n0.68 RightForeArm 46.64 15.42 0\n0.68 Neck -11.06 -0.78 0\n0.68 Head -12.3 -0.64 0\n0.69 Spine -8.85 -0.64 0\n0.69 Spine1 -10.11 -0.74 0\n0.69 Spine2 -10.29 -0.72 0\n0.69 LeftShoulder 0 0 23.99\n0.69 RightShoulder 0 0 -27.98\n0.69 LeftArm 9.26 -38.9 0\n0.69 RightArm 44.98 55.96 0\n0.69 LeftForeArm 12.57 15.52 0\n0.69 RightForeArm 52.56 22.32 0\n0.69 Neck -11.2 -0.8 0\n0.69 Head -13.8 -0.83 0\n0.71 Spine -8.63 -0.67 0\n0.71 Spine1 -9.87 -0.78 0\n0.71 Spine2 -10.11 -0.74 0\n0.71 LeftShoulder 0 0 23.91\n0.71 RightShoulder 0 0 -27.81\n0.71 LeftArm 22.06 -49.19 0\n0.71 RightArm 48 58 0\n0.71 LeftForeArm 25.43 -1.52 0\n0.71 RightForeArm 62.64 34.08 0\n0.71 Neck -11.17 -0.82 0\n0.71 Head -15.99 -1.1 0\n0.73 Spine -8.39 -0.7 0\n0.73 Spine1 -9.59 -0.82 0\n0.73 Spine2 -9.87 -0.78 0\n0.73 LeftShoulder 0 0 23.76\n0.73 RightShoulder 0 0 -27.52\n0.73 LeftArm 31.8 -57.02 0\n0.73 RightArm 47.74 58.26 0\n0.73 LeftForeArm 37.77 -17.88 0\n0.73 RightForeArm 69.48 42.06 0\n0.73 Neck -11.07 -0.86 0\n0.73 Head -16.8 -1.2 0\n0.75 Spine -8.12 -0.74 0\n0.75 Spine1 -9.28 -0.86 0\n0.75 Spine2 -9.59 -0.82 0\n0.75 LeftShoulder 0 0 23.56\n0.75 RightShoulder 0 0 -27.11\n0.75 LeftArm 37.26 -61.41 0\n0.75 RightArm 47.04 58.96 0\n0.75 LeftForeArm 48.56 -32.19 0\n0.75 RightForeArm 72 45 0\n0.75 Neck -10.93 -0.93 0\n0.75 Head -16.75 -1.23 0\n0.76 Spine -7.98 -0.76 0\n0.76 Spine1 -9.12 -0.89 0\n0.76 Spine2 -9.43 -0.84 0\n0.76 LeftShoulder 0 0 23.44\n0.76 RightShoulder 0 0 -26.88\n0.76 LeftArm 38 -62 0\n0.76 RightArm 46.55 59.45 0\n0.76 LeftForeArm 53.06 -38.14 0\n0.76 RightForeArm 71.92 45.09 0\n0.76 Neck -10.85 -0.98 0\n0.76 Head -16.69 -1.26 0\n0.8 Spine -7.47 -0.83 0\n0.8 Spine1 -8.53 -0.97 0\n0.8 Spine2 -8.81 -0.93 0\n0.8 LeftShoulder 0 0 22.93\n0.8 RightShoulder 0 0 -25.87\n0.8 LeftArm 37.36 -62.96 0\n0.8 RightArm 43.93 62.07 0\n0.8 LeftForeArm 62 -50 0\n0.8 RightForeArm 70.3 47.06 0\n0.8 Neck -10.45 -1.17 0\n0.8 Head -16.27 -1.47 0\n0.87 Spine -7 -0.9 0\n0.87 Spine1 -8 -1.05 0\n0.87 Spine2 -8.05 -1.04 0\n0.87 LeftShoulder 0 0 22.16\n0.87 RightShoulder 0 0 -24.32\n0.87 LeftArm 34.26 -67.61 0\n0.87 RightArm 38.66 67.34 0\n0.87 LeftForeArm 59.78 -53.54 0\n0.87 RightForeArm 64.54 54.05 0\n0.87 Neck -9.79 -1.5 0\n0.87 Head -15.21 -1.99 0\n0.89 Spine -6.9 -0.89 0\n0.89 Spine1 -7.89 -1.04 0\n0.89 Spine2 -8 -1.05 0\n0.89 LeftShoulder 0 0 22.04\n0.89 RightShoulder 0 0 -24.09\n0.89 LeftArm 33.22 -69.17 0\n0.89 RightArm 37.45 68.55 0\n0.89 LeftForeArm 58.6 -55.43 0\n0.89 RightForeArm 62.75 56.23 0\n0.89 Neck -9.67 -1.56 0\n0.89 Head -14.93 -2.13 0\n0.91 Spine -6.62 -0.86 0\n0.91 Spine1 -7.56 -1 0\n0.91 Spine2 -7.89 -1.04 0\n0.91 LeftShoulder 0 0 22\n0.91 RightShoulder 0 0 -24\n0.91 LeftArm 32.23 -70.65 0\n0.91 RightArm 36.56 69.44 0\n0.91 LeftForeArm 57.33 -57.48 0\n0.91 RightForeArm 61.1 58.23 0\n0.91 Neck -9.61 -1.6 0\n0.91 Head -14.69 -2.25 0\n0.92 Spine -6.41 -0.84 0\n0.92 Spine1 -7.33 -0.97 0\n0.92 Spine2 -7.75 -1.02 0\n0.92 LeftShoulder 0 0 21.97\n0.92 RightShoulder 0 0 -23.97\n0.92 LeftArm 31.77 -71.34 0\n0.92 RightArm 36.26 69.74 0\n0.92 LeftForeArm 56.67 -58.52 0\n0.92 RightForeArm 60.36 59.13 0\n0.92 Neck -9.6 -1.6 0\n0.92 Head -14.59 -2.3 0\n0.94 Spine -5.89 -0.78 0\n0.94 Spine1 -6.73 -0.91 0\n0.94 Spine2 -7.33 -0.97 0\n0.94 LeftShoulder 0 0 21.74\n0.94 RightShoulder 0 0 -23.74\n0.94 LeftArm 30.97 -72.55 0\n0.94 RightArm 36 70 0\n0.94 LeftForeArm 55.4 -60.57 0\n0.94 RightForeArm 59.12 60.64 0\n0.94 Neck -9.52 -1.58 0\n0.94 Head -14.45 -2.37 0\n0.96 Spine -5.24 -0.71 0\n0.96 Spine1 -5.99 -0.82 0\n0.96 Spine2 -6.73 -0.91 0\n0.96 LeftShoulder 0 0 21.31\n0.96 RightShoulder 0 0 -23.31\n0.96 LeftArm 30.37 -73.44 0\n0.96 RightArm 35.55 69.94 0\n0.96 LeftForeArm 54.22 -62.46 0\n0.96 RightForeArm 58.3 61.64 0\n0.96 Neck -9.29 -1.53 0\n0.96 Head -14.4 -2.4 0\n0.98 Spine -4.47 -0.62 0\n0.98 Spine1 -5.11 -0.73 0\n0.98 Spine2 -5.99 -0.82 0\n0.98 LeftShoulder 0 0 20.7\n0.98 RightShoulder 0 0 -22.7\n0.98 LeftArm 30.04 -73.93 0\n0.98 RightArm 34.26 69.78 0\n0.98 LeftForeArm 53.21 -64.06 0\n0.98 RightForeArm 58 62 0\n0.98 Neck -8.92 -1.45 0\n0.98 Head -14.28 -2.37 0\n0.99 Spine -4.05 -0.58 0\n0.99 Spine1 -4.63 -0.67 0\n0.99 Spine2 -5.56 -0.78 0\n0.99 LeftShoulder 0 0 20.34\n0.99 RightShoulder 0 0 -22.34\n0.99 LeftArm 30 -74 0\n0.99 RightArm 33.34 69.66 0\n0.99 LeftForeArm 52.8 -64.72 0\n0.99 RightForeArm 57.89 61.98 0\n0.99 Neck -8.69 -1.41 0\n0.99 Head -14.13 -2.34 0\n1.03 Spine -2.2 -0.37 0\n1.03 Spine1 -2.51 -0.43 0\n1.03 Spine2 -3.6 -0.56 0\n1.03 LeftShoulder 0 0 18.54\n1.03 RightShoulder 0 0 -20.54\n1.03 LeftArm 28.32 -73.61 0\n1.03 RightArm 28 68.97 0\n1.03 LeftForeArm 52 -66 0\n1.03 RightForeArm 55.51 61.57 0\n1.03 Neck -7.52 -1.16 0\n1.03 Head -13.04 -2.11 0\n1.27 Spine 6.65 0.6 0\n1.27 Spine1 7.6 0.7 0\n1.27 Spine2 7.49 0.69 0\n1.27 LeftShoulder 0 0 6.45\n1.27 RightShoulder 0 0 -8.45\n1.27 LeftArm -17.04 -63.02 0\n1.27 RightArm -20.97 62.65 0\n1.27 LeftForeArm 15.06 -55.63 0\n1.27 RightForeArm 10.75 53.85 0\n1.27 Neck 1.12 0.7 0\n1.27 Head 0.23 0.74 0\n1.29 Spine 6.64 0.6 0\n1.29 Spine1 7.59 0.7 0\n1.29 Spine2 7.6 0.7 0\n1.29 LeftShoulder 0 0 6.12\n1.29 RightShoulder 0 0 -8.12\n1.29 LeftArm -20.63 -62.19 0\n1.29 RightArm -23.34 62.34 0\n1.29 LeftForeArm 11.06 -54.51 0\n1.29 RightForeArm 7.49 53.29 0\n1.29 Neck 1.42 0.76 0\n1.29 Head 1.04 0.91 0\n1.31 Spine 6.6 0.59 0\n1.31 Spine1 7.55 0.69 0\n1.31 Spine2 7.59 0.7 0\n1.31 LeftShoulder 0 0 6\n1.31 RightShoulder 0 0 -8\n1.31 LeftArm -23.76 -61.46 0\n1.31 RightArm -25.01 62.13 0\n1.31 LeftForeArm 7.31 -53.46 0\n1.31 RightForeArm 4.71 52.81 0\n1.31 Neck 1.58 0.8 0\n1.31 Head 1.68 1.05 0\n1.32 Spine 6.58 0.59 0\n1.32 Spine1 7.52 0.69 0\n1.32 Spine2 7.57 0.7 0\n1.32 LeftShoulder 0 0 6\n1.32 RightShoulder 0 0 -7.99\n1.32 LeftArm -25.13 -61.14 0\n1.32 RightArm -25.55 62.06 0\n1.32 LeftForeArm 5.56 -52.96 0\n1.32 RightForeArm 3.52 52.61 0\n1.32 Neck 1.6 0.8 0\n1.32 Head 1.93 1.1 0\n1.34 Spine 6.51 0.58 0\n1.34 Spine1 7.44 0.68 0\n1.34 Spine2 7.52 0.69 0\n1.34 LeftShoulder 0 0 5.96\n1.34 RightShoulder 0 0 -7.95\n1.34 LeftArm -27.42 -60.6 0\n1.34 RightArm -26 62 0\n1.34 LeftForeArm 2.36 -52.07 0\n1.34 RightForeArm 1.62 52.28 0\n1.34 Neck 1.6 0.8 0\n1.34 Head 2.28 1.17 0\n1.36 Spine 6.43 0.57 0\n1.36 Spine1 7.35 0.66 0\n1.36 Spine2 7.44 0.68 0\n1.36 LeftShoulder 0 0 5.9\n1.36 RightShoulder 0 0 -7.86\n1.36 LeftArm -29.04 -60.22 0\n1.36 RightArm -26.08 61.97 0\n1.36 LeftForeArm -0.37 -51.3 0\n1.36 RightForeArm 0.42 52.07 0\n1.36 Neck 1.58 0.79 0\n1.36 Head 2.4 1.2 0\n1.38 Spine 6.33 0.55 0\n1.38 Spine1 7.24 0.65 0\n1.38 Spine2 7.35 0.66 0\n1.38 LeftShoulder 0 0 5.8\n1.38 RightShoulder 0 0 -7.74\n1.38 LeftArm -29.89 -60.03 0\n1.38 RightArm -26.31 61.89 0\n1.38 LeftForeArm -2.55 -50.69 0\n1.38 RightForeArm 0 52 0\n1.38 Neck 1.56 0.78 0\n1.38 Head 2.39 1.2 0\n1.39 Spine 6.28 0.55 0\n1.39 Spine1 7.18 0.64 0\n1.39 Spine2 7.3 0.66 0\n1.39 LeftShoulder 0 0 5.75\n1.39 RightShoulder 0 0 -7.67\n1.39 LeftArm -30 -60 0\n1.39 RightArm -26.48 61.83 0\n1.39 LeftForeArm -3.4 -50.45 0\n1.39 RightForeArm -0.02 51.99 0\n1.39 Neck 1.55 0.77 0\n1.39 Head 2.38 1.19 0\n1.43 Spine 6.04 0.51 0\n1.43 Spine1 6.91 0.6 0\n1.43 Spine2 7.05 0.62 0\n1.43 LeftShoulder 0 0 5.47\n1.43 RightShoulder 0 0 -7.3\n1.43 LeftArm -30.27 -59.89 0\n1.43 RightArm -27.46 61.48 0\n1.43 LeftForeArm -5 -50 0\n1.43 RightForeArm -0.48 51.83 0\n1.43 Neck 1.48 0.74 0\n1.43 Head 2.32 1.16 0\n1.72 Spine 4.55 0.3 0\n1.72 Spine1 5.2 0.35 0\n1.72 Spine2 5.21 0.35 0\n1.72 LeftShoulder 0 0 3.07\n1.72 RightShoulder 0 0 -4.09\n1.72 LeftArm -39.9 -55.88 0\n1.72 RightArm -39.09 57.33 0\n1.72 LeftForeArm -14.24 -46.45 0\n1.72 RightForeArm -11.9 47.75 0\n1.72 Neck 0.83 0.41 0\n1.72 Head 1.32 0.66 0\n1.74 Spine 4.53 0.3 0\n1.74 Spine1 5.17 0.35 0\n1.74 Spine2 5.2 0.35 0\n1.74 LeftShoulder 0 0 3.02\n1.74 RightShoulder 0 0 -4.02\n1.74 LeftArm -40.49 -55.63 0\n1.74 RightArm -39.52 57.17 0\n1.74 LeftForeArm -15.01 -46.15 0\n1.74 RightForeArm -12.54 47.52 0\n1.74 Neck 0.81 0.41 0\n1.74 Head 1.28 0.64 0\n1.76 Spine 4.46 0.29 0\n1.76 Spine1 5.1 0.34 0\n1.76 Spine2 5.17 0.35 0\n1.76 LeftShoulder 0 0 3\n1.76 RightShoulder 0 0 -4\n1.76 LeftArm -41 -55.42 0\n1.76 RightArm -39.82 57.06 0\n1.76 LeftForeArm -15.72 -45.88 0\n1.76 RightForeArm -13.09 47.33 0\n1.76 Neck 0.8 0.4 0\n1.76 Head 1.24 0.62 0\n1.77 Spine 4.41 0.29 0\n1.77 Spine1 5.04 0.34 0\n1.77 Spine2 5.14 0.35 0\n1.77 LeftShoulder 0 0 3\n1.77 RightShoulder 0 0 -3.99\n1.77 LeftArm -41.22 -55.33 0\n1.77 RightArm -39.92 57.03 0\n1.77 LeftForeArm -16.05 -45.75 0\n1.77 RightForeArm -13.32 47.24 0\n1.77 Neck 0.8 0.4 0\n1.77 Head 1.23 0.61 0\n1.79 Spine 4.29 0.28 0\n1.79 Spine1 4.9 0.33 0\n1.79 Spine2 5.03 0.34 0\n1.79 LeftShoulder 0 0 2.96\n1.79 RightShoulder 0 0 -3.95\n1.79 LeftArm -41.59 -55.17 0\n1.79 RightArm -40 57 0\n1.79 LeftForeArm -16.65 -45.52 0\n1.79 RightForeArm -13.69 47.11 0\n1.79 Neck 0.79 0.4 0\n1.79 Head 1.21 0.6 0\n1.81 Spine 4.13 0.27 0\n1.81 Spine1 4.72 0.32 0\n1.81 Spine2 4.88 0.33 0\n1.81 LeftShoulder 0 0 2.89\n1.81 RightShoulder 0 0 -3.86\n1.81 LeftArm -41.85 -55.06 0\n1.81 RightArm -40.07 57.76 0\n1.81 LeftForeArm -17.15 -45.33 0\n1.81 RightForeArm -13.92 47.03 0\n1.81 Neck 0.78 0.39 0\n1.81 Head 1.2 0.6 0\n1.83 Spine 3.94 0.26 0\n1.83 Spine1 4.51 0.3 0\n1.83 Spine2 4.68 0.32 0\n1.83 LeftShoulder 0 0 2.8\n1.83 RightShoulder 0 0 -3.73\n1.83 LeftArm -41.98 -55.01 0\n1.83 RightArm -40.27 59.94 0\n1.83 LeftForeArm -17.55 -45.17 0\n1.83 RightForeArm -14 47 0\n1.83 Neck 0.76 0.38 0\n1.83 Head 1.19 0.6 0\n1.84 Spine 3.84 0.25 0\n1.84 Spine1 4.39 0.3 0\n1.84 Spine2 4.57 0.31 0\n1.84 LeftShoulder 0 0 2.74\n1.84 RightShoulder 0 0 -3.65\n1.84 LeftArm -42 -55 0\n1.84 RightArm -40.41 61.51 0\n1.84 LeftForeArm -17.71 -45.11 0\n1.84 RightForeArm -14.11 47.08 0\n1.84 Neck 0.74 0.37 0\n1.84 Head 1.18 0.59 0\n1.88 Spine 3.37 0.22 0\n1.88 Spine1 3.85 0.26 0\n1.88 Spine2 4.05 0.27 0\n1.88 LeftShoulder 0 0 2.45\n1.88 RightShoulder 0 0 -3.27\n1.88 LeftArm -42.96 -56.58 0\n1.88 RightArm -41.23 70.57 0\n1.88 LeftForeArm -18 -45 0\n1.88 RightForeArm -16.59 48.84 0\n1.88 Neck 0.67 0.33 0\n1.88 Head 1.1 0.55 0\n2.2 Spine 0 0 0\n2.2 Spine1 0 0 0\n2.2 Spine2 0 0 0\n2.2 LeftShoulder 0 0 0\n2.2 RightShoulder 0 0 0\n2.2 LeftArm -70 -101 0\n2.2 RightArm -50 167 0\n2.2 LeftForeArm -49 1 0\n2.2 RightForeArm -66 84 0\n2.2 Neck 0 0 0\n2.2 Head 0 0 0\n",
            "clip pair_load\nlength 2.4\nloop 0\npivot 1.1\nmode Spine body\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine 0 0 0\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 RightArm -50 167 0\n0 LeftForeArm -49 1 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.37 Spine 7.35 0 0\n0.37 Spine1 8.4 0 0\n0.37 Spine2 8.34 0 0\n0.37 LeftShoulder 0 0 1.95\n0.37 RightShoulder 0 0 -1.95\n0.37 LeftArm -44.52 -49.18 0\n0.37 RightArm -38.81 50.48 0\n0.37 LeftForeArm 4.93 -23.23 0\n0.37 RightForeArm 11.99 35.47 0\n0.37 Neck 3.07 0 0\n0.37 Head 4.32 0 0\n0.39 Spine 7.26 0 0\n0.39 Spine1 8.3 0 0\n0.39 Spine2 8.4 0 0\n0.39 LeftShoulder 0 0 1.99\n0.39 RightShoulder 0 0 -1.99\n0.39 LeftArm -43.24 -46.58 0\n0.39 RightArm -38.43 46.48 0\n0.39 LeftForeArm 8.1 -24.65 0\n0.39 RightForeArm 15.69 33.17 0\n0.39 Neck 3.15 0 0\n0.39 Head 4.5 0 0\n0.41 Spine 7 0 0\n0.41 Spine1 8 0 0\n0.41 Spine2 8.3 0 0\n0.41 LeftShoulder 0 0 2\n0.41 RightShoulder 0 0 -2\n0.41 LeftArm -42.14 -44.35 0\n0.41 RightArm -38.16 43.66 0\n0.41 LeftForeArm 10.99 -25.95 0\n0.41 RightForeArm 18.82 31.23 0\n0.41 Neck 3.19 0 0\n0.41 Head 4.64 0 0\n0.42 Spine 6.81 0 0\n0.42 Spine1 7.78 0 0\n0.42 Spine2 8.17 0 0\n0.42 LeftShoulder 0 0 2.04\n0.42 RightShoulder 0 0 -2.04\n0.42 LeftArm -41.66 -43.38 0\n0.42 RightArm -38.07 42.75 0\n0.42 LeftForeArm 12.32 -26.55 0\n0.42 RightForeArm 20.13 30.41 0\n0.42 Neck 3.2 0 0\n0.42 Head 4.7 0 0\n0.44 Spine 6.33 0 0\n0.44 Spine1 7.23 0 0\n0.44 Spine2 7.78 0 0\n0.44 LeftShoulder 0 0 2.32\n0.44 RightShoulder 0 0 -2.32\n0.44 LeftArm -40.87 -41.78 0\n0.44 RightArm -38 42 0\n0.44 LeftForeArm 14.71 -27.62 0\n0.44 RightForeArm 22.23 29.1 0\n0.44 Neck 3.11 0 0\n0.44 Head 4.77 0 0\n0.46 Spine 5.72 0 0\n0.46 Spine1 6.54 0 0\n0.46 Spine2 7.23 0 0\n0.46 LeftShoulder 0 0 2.86\n0.46 RightShoulder 0 0 -2.86\n0.46 LeftArm -40.32 -40.66 0\n0.46 RightArm -37.48 41.94 0\n0.46 LeftForeArm 16.71 -28.52 0\n0.46 RightForeArm 23.54 28.28 0\n0.46 Neck 2.86 0 0\n0.46 Head 4.8 0 0\n0.48 Spine 5.02 0 0\n0.48 Spine1 5.73 0 0\n0.48 Spine2 6.54 0 0\n0.48 LeftShoulder 0 0 3.62\n0.48 RightShoulder 0 0 -3.62\n0.48 LeftArm -40.04 -40.08 0\n0.48 RightArm -35.98 41.78 0\n0.48 LeftForeArm 18.27 -29.22 0\n0.48 RightForeArm 24 28 0\n0.48 Neck 2.47 0 0\n0.48 Head 4.67 0 0\n0.49 Spine 4.63 0 0\n0.49 Spine1 5.29 0 0\n0.49 Spine2 6.15 0 0\n0.49 LeftShoulder 0 0 4.08\n0.49 RightShoulder 0 0 -4.08\n0.49 LeftArm -40 -40 0\n0.49 RightArm -34.91 41.66 0\n0.49 LeftForeArm 18.88 -29.5 0\n0.49 RightForeArm 24.1 27.97 0\n0.49 Neck 2.23 0 0\n0.49 Head 4.51 0 0\n0.53 Spine 2.91 0 0\n0.53 Spine1 3.33 0 0\n0.53 Spine2 4.34 0 0\n0.53 LeftShoulder 0 0 6.32\n0.53 RightShoulder 0 0 -6.32\n0.53 LeftArm -38.04 -39.89 0\n0.53 RightArm -28.71 40.97 0\n0.53 LeftForeArm 20 -30 0\n0.53 RightForeArm 26.41 27.31 0\n0.53 Neck 0.98 0 0\n0.53 Head 3.34 0 0\n0.77 Spine -5.25 0 0\n0.77 Spine1 -6 0 0\n0.77 Spine2 -5.9 0 0\n0.77 LeftShoulder 0 0 21.44\n0.77 RightShoulder 0 0 -21.44\n0.77 LeftArm 14.88 -36.86 0\n0.77 RightArm 28.16 34.65 0\n0.77 LeftForeArm 57.58 -19.63 0\n0.77 RightForeArm 69.62 14.96 0\n0.77 Neck -8.28 0 0\n0.77 Head -10.88 0 0\n0.79 Spine -5.25 0 0\n0.79 Spine1 -5.99 0 0\n0.79 Spine2 -6 0 0\n0.79 LeftShoulder 0 0 21.86\n0.79 RightShoulder 0 0 -21.85\n0.79 LeftArm 19.06 -36.62 0\n0.79 RightArm 30.91 34.34 0\n0.79 LeftForeArm 61.66 -18.51 0\n0.79 RightForeArm 72.77 14.07 0\n0.79 Neck -8.61 0 0\n0.79 Head -11.74 0 0\n0.81 Spine -5.23 0 0\n0.81 Spine1 -5.98 0 0\n0.81 Spine2 -5.99 0 0\n0.81 LeftShoulder 0 0 22\n0.81 RightShoulder 0 0 -22\n0.81 LeftArm 22.72 -36.42 0\n0.81 RightArm 32.85 34.13 0\n0.81 LeftForeArm 65.47 -17.46 0\n0.81 RightForeArm 75.46 13.3 0\n0.81 Neck -8.78 0 0\n0.81 Head -12.43 0 0\n0.82 Spine -5.22 0 0\n0.82 Spine1 -5.97 0 0\n0.82 Spine2 -5.99 0 0\n0.82 LeftShoulder 0 0 22.01\n0.82 RightShoulder 0 0 -22.01\n0.82 LeftArm 24.32 -36.32 0\n0.82 RightArm 33.48 34.06 0\n0.82 LeftForeArm 67.25 -16.96 0\n0.82 RightForeArm 76.6 12.97 0\n0.82 Neck -8.8 0 0\n0.82 Head -12.7 0 0\n0.84 Spine -5.2 0 0\n0.84 Spine1 -5.94 0 0\n0.84 Spine2 -5.97 0 0\n0.84 LeftShoulder 0 0 22.06\n0.84 RightShoulder 0 0 -22.06\n0.84 LeftArm 26.99 -36.17 0\n0.84 RightArm 34 34 0\n0.84 LeftForeArm 70.51 -16.07 0\n0.84 RightForeArm 78.43 12.45 0\n0.84 Neck -8.81 0 0\n0.84 Head -13.07 0 0\n0.86 Spine -5.17 0 0\n0.86 Spine1 -5.91 0 0\n0.86 Spine2 -5.94 0 0\n0.86 LeftShoulder 0 0 22.15\n0.86 RightShoulder 0 0 -22.15\n0.86 LeftArm 28.88 -36.06 0\n0.86 RightArm 34.03 33.97 0\n0.86 LeftForeArm 73.29 -15.3 0\n0.86 RightForeArm 79.59 12.12 0\n0.86 Neck -8.84 0 0\n0.86 Head -13.2 0 0\n0.88 Spine -5.14 0 0\n0.88 Spine1 -5.88 0 0\n0.88 Spine2 -5.91 0 0\n0.88 LeftShoulder 0 0 22.28\n0.88 RightShoulder 0 0 -22.28\n0.88 LeftArm 29.87 -36.01 0\n0.88 RightArm 34.1 33.9 0\n0.88 LeftForeArm 75.51 -14.69 0\n0.88 RightForeArm 80 12 0\n0.88 Neck -8.88 0 0\n0.88 Head -13.22 0 0\n0.89 Spine -5.13 0 0\n0.89 Spine1 -5.86 0 0\n0.89 Spine2 -5.9 0 0\n0.89 LeftShoulder 0 0 22.35\n0.89 RightShoulder 0 0 -22.35\n0.89 LeftArm 30 -36 0\n0.89 RightArm 34.15 33.85 0\n0.89 LeftForeArm 76.38 -14.45 0\n0.89 RightForeArm 80.01 11.99 0\n0.89 Neck -8.91 0 0\n0.89 Head -13.23 0 0\n0.93 Spine -5.06 0 0\n0.93 Spine1 -5.78 0 0\n0.93 Spine2 -5.82 0 0\n0.93 LeftShoulder 0 0 22.7\n0.93 RightShoulder 0 0 -22.7\n0.93 LeftArm 30.19 -35.9 0\n0.93 RightArm 34.43 33.57 0\n0.93 LeftForeArm 78 -14 0\n0.93 RightForeArm 80.3 11.85 0\n0.93 Neck -9.04 0 0\n0.93 Head -13.37 0 0\n1.07 Spine -4.9 0 0\n1.07 Spine1 -5.6 0 0\n1.07 Spine2 -5.61 0 0\n1.07 LeftShoulder 0 0 23.9\n1.07 RightShoulder 0 0 -23.9\n1.07 LeftArm 32.59 -34.7 0\n1.07 RightArm 35.72 32.28 0\n1.07 LeftForeArm 79.8 -13.1 0\n1.07 RightForeArm 82.78 10.61 0\n1.07 Neck -9.54 0 0\n1.07 Head -14.14 0 0\n1.09 Spine -4.8 0 0\n1.09 Spine1 -5.49 0 0\n1.09 Spine2 -5.6 0 0\n1.09 LeftShoulder 0 0 23.97\n1.09 RightShoulder 0 0 -23.97\n1.09 LeftArm 32.96 -34.52 0\n1.09 RightArm 35.85 32.15 0\n1.09 LeftForeArm 80.2 -12.9 0\n1.09 RightForeArm 83.14 10.43 0\n1.09 Neck -9.58 0 0\n1.09 Head -14.23 0 0\n1.11 Spine -4.53 0 0\n1.11 Spine1 -5.17 0 0\n1.11 Spine2 -5.49 0 0\n1.11 LeftShoulder 0 0 24\n1.11 RightShoulder 0 0 -24\n1.11 LeftArm 33.3 -34.35 0\n1.11 RightArm 35.94 32.06 0\n1.11 LeftForeArm 80.59 -12.7 0\n1.11 RightForeArm 83.45 10.28 0\n1.11 Neck -9.6 0 0\n1.11 Head -14.31 0 0\n1.12 Spine -4.33 0 0\n1.12 Spine1 -4.95 0 0\n1.12 Spine2 -5.36 0 0\n1.12 LeftShoulder 0 0 23.94\n1.12 RightShoulder 0 0 -23.94\n1.12 LeftArm 33.45 -34.28 0\n1.12 RightArm 35.97 32.03 0\n1.12 LeftForeArm 80.78 -12.61 0\n1.12 RightForeArm 83.58 10.21 0\n1.12 Neck -9.6 0 0\n1.12 Head -14.34 0 0\n1.14 Spine -3.82 0 0\n1.14 Spine1 -4.37 0 0\n1.14 Spine2 -4.95 0 0\n1.14 LeftShoulder 0 0 23.52\n1.14 RightShoulder 0 0 -23.52\n1.14 LeftArm 33.7 -34.15 0\n1.14 RightArm 36 32 0\n1.14 LeftForeArm 81.14 -12.43 0\n1.14 RightForeArm 83.81 10.1 0\n1.14 Neck -9.5 0 0\n1.14 Head -14.38 0 0\n1.16 Spine -3.18 0 0\n1.16 Spine1 -3.64 0 0\n1.16 Spine2 -4.37 0 0\n1.16 LeftShoulder 0 0 22.71\n1.16 RightShoulder 0 0 -22.71\n1.16 LeftArm 33.89 -34.06 0\n1.16 RightArm 35.29 31.99 0\n1.16 LeftForeArm 81.45 -12.28 0\n1.16 RightForeArm 83.95 10.03 0\n1.16 Neck -9.22 0 0\n1.16 Head -14.4 0 0\n1.18 Spine -2.44 0 0\n1.18 Spine1 -2.78 0 0\n1.18 Spine2 -3.64 0 0\n1.18 LeftShoulder 0 0 21.57\n1.18 RightShoulder 0 0 -21.57\n1.18 LeftArm 33.99 -34.01 0\n1.18 RightArm 33.26 31.94 0\n1.18 LeftForeArm 81.7 -12.15 0\n1.18 RightForeArm 84 10 0\n1.18 Neck -8.77 0 0\n1.18 Head -14.25 0 0\n1.19 Spine -2.03 0 0\n1.19 Spine1 -2.32 0 0\n1.19 Spine2 -3.23 0 0\n1.19 LeftShoulder 0 0 20.88\n1.19 RightShoulder 0 0 -20.88\n1.19 LeftArm 34 -34 0\n1.19 RightArm 31.79 31.91 0\n1.19 LeftForeArm 81.81 -12.1 0\n1.19 RightForeArm 83.77 10.03 0\n1.19 Neck -8.5 0 0\n1.19 Head -14.07 0 0\n1.23 Spine -0.22 0 0\n1.23 Spine1 -0.25 0 0\n1.23 Spine2 -1.32 0 0\n1.23 LeftShoulder 0 0 17.52\n1.23 RightShoulder 0 0 -17.52\n1.23 LeftArm 31.26 -33.83 0\n1.23 RightArm 23.35 31.74 0\n1.23 LeftForeArm 82 -12 0\n1.23 RightForeArm 78.59 10.6 0\n1.23 Neck -7.08 0 0\n1.23 Head -12.74 0 0\n1.47 Spine 8.4 0 0\n1.47 Spine1 9.6 0 0\n1.47 Spine2 9.49 0 0\n1.47 LeftShoulder 0 0 -5.16\n1.47 RightShoulder 0 0 5.16\n1.47 LeftArm -42.83 -29.3 0\n1.47 RightArm -54.05 30.16 0\n1.47 LeftForeArm 0.35 -18.48 0\n1.47 RightForeArm -18.65 21.41 0\n1.47 Neck 3.42 0 0\n1.47 Head 3.37 0 0\n1.49 Spine 8.36 0 0\n1.49 Spine1 9.56 0 0\n1.49 Spine2 9.6 0 0\n1.49 LeftShoulder 0 0 -5.78\n1.49 RightShoulder 0 0 5.78\n1.49 LeftArm -48.69 -28.94 0\n1.49 RightArm -57.79 30.09 0\n1.49 LeftForeArm -8.5 -19.18 0\n1.49 RightForeArm -25.73 22.19 0\n1.49 Neck 3.78 0 0\n1.49 Head 4.34 0 0\n1.51 Spine 8.25 0 0\n1.51 Spine1 9.43 0 0\n1.51 Spine2 9.56 0 0\n1.51 LeftShoulder 0 0 -6\n1.51 RightShoulder 0 0 6\n1.51 LeftArm -53.81 -28.62 0\n1.51 RightArm -60.43 30.03 0\n1.51 LeftForeArm -16.78 -19.84 0\n1.51 RightForeArm -31.77 22.86 0\n1.51 Neck 3.97 0 0\n1.51 Head 5.12 0 0\n1.52 Spine 8.17 0 0\n1.52 Spine1 9.34 0 0\n1.52 Spine2 9.5 0 0\n1.52 LeftShoulder 0 0 -5.99\n1.52 RightShoulder 0 0 5.99\n1.52 LeftArm -56.05 -28.49 0\n1.52 RightArm -61.29 30.01 0\n1.52 LeftForeArm -20.65 -20.15 0\n1.52 RightForeArm -34.35 23.15 0\n1.52 Neck 4 0 0\n1.52 Head 5.43 0 0\n1.54 Spine 7.97 0 0\n1.54 Spine1 9.11 0 0\n1.54 Spine2 9.34 0 0\n1.54 LeftShoulder 0 0 -5.94\n1.54 RightShoulder 0 0 5.94\n1.54 LeftArm -59.79 -28.26 0\n1.54 RightArm -62 30 0\n1.54 LeftForeArm -27.73 -20.71 0\n1.54 RightForeArm -38.47 23.61 0\n1.54 Neck 3.98 0 0\n1.54 Head 5.85 0 0\n1.56 Spine 7.72 0 0\n1.56 Spine1 8.83 0 0\n1.56 Spine2 9.11 0 0\n1.56 LeftShoulder 0 0 -5.83\n1.56 RightShoulder 0 0 5.83\n1.56 LeftArm -62.43 -28.1 0\n1.56 RightArm -62.03 30.01 0\n1.56 LeftForeArm -33.77 -21.19 0\n1.56 RightForeArm -41.09 23.9 0\n1.56 Neck 3.93 0 0\n1.56 Head 6 0 0\n1.58 Spine 7.43 0 0\n1.58 Spine1 8.49 0 0\n1.58 Spine2 8.83 0 0\n1.58 LeftShoulder 0 0 -5.68\n1.58 RightShoulder 0 0 5.68\n1.58 LeftArm -63.82 -28.01 0\n1.58 RightArm -62.11 30.06 0\n1.58 LeftForeArm -38.59 -21.57 0\n1.58 RightForeArm -42 24 0\n1.58 Neck 3.85 0 0\n1.58 Head 5.97 0 0\n1.59 Spine 7.27 0 0\n1.59 Spine1 8.3 0 0\n1.59 Spine2 8.66 0 0\n1.59 LeftShoulder 0 0 -5.58\n1.59 RightShoulder 0 0 5.58\n1.59 LeftArm -64 -28 0\n1.59 RightArm -62.17 30.09 0\n1.59 LeftForeArm -40.47 -21.72 0\n1.59 RightForeArm -42.01 24 0\n1.59 Neck 3.81 0 0\n1.59 Head 5.94 0 0\n1.63 Spine 6.55 0 0\n1.63 Spine1 7.49 0 0\n1.63 Spine2 7.91 0 0\n1.63 LeftShoulder 0 0 -5.14\n1.63 RightShoulder 0 0 5.14\n1.63 LeftArm -64.11 -28.06 0\n1.63 RightArm -62.52 30.26 0\n1.63 LeftForeArm -44 -22 0\n1.63 RightForeArm -42.17 23.91 0\n1.63 Neck 3.56 0 0\n1.63 Head 5.71 0 0\n1.87 Spine 3.15 0 0\n1.87 Spine1 3.6 0 0\n1.87 Spine2 3.64 0 0\n1.87 LeftShoulder 0 0 -2.11\n1.87 RightShoulder 0 0 2.11\n1.87 LeftArm -67.14 -29.57 0\n1.87 RightArm -65.68 31.84 0\n1.87 LeftForeArm -46.59 -20.7 0\n1.87 RightForeArm -45.26 22.37 0\n1.87 Neck 1.7 0 0\n1.87 Head 2.86 0 0\n1.89 Spine 3.14 0 0\n1.89 Spine1 3.59 0 0\n1.89 Spine2 3.6 0 0\n1.89 LeftShoulder 0 0 -2.03\n1.89 RightShoulder 0 0 2.03\n1.89 LeftArm -67.37 -29.69 0\n1.89 RightArm -65.83 31.91 0\n1.89 LeftForeArm -46.87 -20.56 0\n1.89 RightForeArm -45.48 22.26 0\n1.89 Neck 1.64 0 0\n1.89 Head 2.69 0 0\n1.91 Spine 3.1 0 0\n1.91 Spine1 3.54 0 0\n1.91 Spine2 3.58 0 0\n1.91 LeftShoulder 0 0 -2\n1.91 RightShoulder 0 0 2\n1.91 LeftArm -67.58 -29.79 0\n1.91 RightArm -65.94 31.97 0\n1.91 LeftForeArm -47.14 -20.43 0\n1.91 RightForeArm -45.68 22.16 0\n1.91 Neck 1.6 0 0\n1.91 Head 2.55 0 0\n1.92 Spine 3.07 0 0\n1.92 Spine1 3.51 0 0\n1.92 Spine2 3.56 0 0\n1.92 LeftShoulder 0 0 -2\n1.92 RightShoulder 0 0 2\n1.92 LeftArm -67.68 -29.84 0\n1.92 RightArm -65.97 31.99 0\n1.92 LeftForeArm -47.26 -20.37 0\n1.92 RightForeArm -45.76 22.12 0\n1.92 Neck 1.6 0 0\n1.92 Head 2.5 0 0\n1.94 Spine 3 0 0\n1.94 Spine1 3.43 0 0\n1.94 Spine2 3.5 0 0\n1.94 LeftShoulder 0 0 -1.98\n1.94 RightShoulder 0 0 1.98\n1.94 LeftArm -67.83 -29.91 0\n1.94 RightArm -66 32 0\n1.94 LeftForeArm -47.48 -20.26 0\n1.94 RightForeArm -45.89 22.06 0\n1.94 Neck 1.59 0 0\n1.94 Head 2.43 0 0\n1.96 Spine 2.91 0 0\n1.96 Spine1 3.32 0 0\n1.96 Spine2 3.42 0 0\n1.96 LeftShoulder 0 0 -1.94\n1.96 RightShoulder 0 0 1.94\n1.96 LeftArm -67.94 -29.97 0\n1.96 RightArm -65.91 32.74 0\n1.96 LeftForeArm -47.68 -20.16 0\n1.96 RightForeArm -45.97 22.01 0\n1.96 Neck 1.57 0 0\n1.96 Head 2.4 0 0\n1.98 Spine 2.8 0 0\n1.98 Spine1 3.2 0 0\n1.98 Spine2 3.3 0 0\n1.98 LeftShoulder 0 0 -1.89\n1.98 RightShoulder 0 0 1.89\n1.98 LeftArm -67.99 -30 0\n1.98 RightArm -65.66 34.88 0\n1.98 LeftForeArm -47.83 -20.09 0\n1.98 RightForeArm -46 22 0\n1.98 Neck 1.53 0 0\n1.98 Head 2.39 0 0\n1.99 Spine 2.74 0 0\n1.99 Spine1 3.13 0 0\n1.99 Spine2 3.24 0 0\n1.99 LeftShoulder 0 0 -1.86\n1.99 RightShoulder 0 0 1.86\n1.99 LeftArm -68 -30 0\n1.99 RightArm -65.47 36.44 0\n1.99 LeftForeArm -47.89 -20.06 0\n1.99 RightForeArm -46.03 22.1 0\n1.99 Neck 1.51 0 0\n1.99 Head 2.37 0 0\n2.03 Spine 2.46 0 0\n2.03 Spine1 2.81 0 0\n2.03 Spine2 2.94 0 0\n2.03 LeftShoulder 0 0 -1.7\n2.03 RightShoulder 0 0 1.7\n2.03 LeftArm -68.05 -31.9 0\n2.03 RightArm -64.4 45.48 0\n2.03 LeftForeArm -48 -20 0\n2.03 RightForeArm -46.78 24.43 0\n2.03 Neck 1.39 0 0\n2.03 Head 2.24 0 0\n2.4 Spine 0 0 0\n2.4 Spine1 0 0 0\n2.4 Spine2 0 0 0\n2.4 LeftShoulder 0 0 0\n2.4 RightShoulder 0 0 0\n2.4 LeftArm -70 -101 0\n2.4 RightArm -50 167 0\n2.4 LeftForeArm -49 1 0\n2.4 RightForeArm -66 84 0\n2.4 Neck 0 0 0\n2.4 Head 0 0 0\n",
            "clip pair_clear\nlength 2.8\nloop 0\npivot 1.1\nmode Spine body\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode RightShoulder body\nmode LeftArm aim\nmode RightArm aim\nmode LeftForeArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine 0 0 0\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 RightShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 RightArm -50 167 0\n0 LeftForeArm -49 1 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.42 Spine -2.1 4.2 0\n0.42 Spine1 -2.4 4.9 0\n0.42 Spine2 -2.39 4.87 0\n0.42 LeftShoulder 0 0 0\n0.42 RightShoulder 0 0 -13.7\n0.42 LeftArm -63.01 -43.33 0\n0.42 RightArm 4.79 89.54 0\n0.42 LeftForeArm -41.68 -3.88 0\n0.42 RightForeArm 35.31 66.23 0\n0.42 Neck -9.3 5.42 0\n0.42 Head -13.21 7.71 0\n0.44 Spine -2.12 4.2 0\n0.44 Spine1 -2.42 4.9 0\n0.44 Spine2 -2.4 4.9 0\n0.44 LeftShoulder 0 0 0\n0.44 RightShoulder 0 0 -13.92\n0.44 LeftArm -62.72 -40.95 0\n0.44 RightArm 6.31 87.39 0\n0.44 LeftForeArm -41.32 -4.12 0\n0.44 RightForeArm 39.25 65.53 0\n0.44 Neck -9.49 5.53 0\n0.44 Head -13.66 7.97 0\n0.46 Spine -2.17 4.21 0\n0.46 Spine1 -2.48 4.92 0\n0.46 Spine2 -2.42 4.9 0\n0.46 LeftShoulder 0 0 0\n0.46 RightShoulder 0 0 -14\n0.46 LeftArm -62.47 -38.92 0\n0.46 RightArm 7.37 85.88 0\n0.46 LeftForeArm -41 -4.34 0\n0.46 RightForeArm 42.56 64.95 0\n0.46 Neck -9.59 5.59 0\n0.46 Head -14.01 8.17 0\n0.47 Spine -2.21 4.22 0\n0.47 Spine1 -2.52 4.92 0\n0.47 Spine2 -2.45 4.91 0\n0.47 LeftShoulder 0 0 0\n0.47 RightShoulder 0 0 -14.02\n0.47 LeftArm -62.37 -38.04 0\n0.47 RightArm 7.72 85.4 0\n0.47 LeftForeArm -40.85 -4.43 0\n0.47 RightForeArm 43.95 64.71 0\n0.47 Neck -9.6 5.6 0\n0.47 Head -14.15 8.25 0\n0.49 Spine -2.3 4.24 0\n0.49 Spine1 -2.63 4.95 0\n0.49 Spine2 -2.52 4.92 0\n0.49 LeftShoulder 0 0 0.03\n0.49 RightShoulder 0 0 -14.15\n0.49 LeftArm -62.19 -36.59 0\n0.49 RightArm 8 85 0\n0.49 LeftForeArm -40.58 -4.61 0\n0.49 RightForeArm 46.15 64.32 0\n0.49 Neck -9.63 5.6 0\n0.49 Head -14.34 8.36 0\n0.51 Spine -2.43 4.26 0\n0.51 Spine1 -2.77 4.97 0\n0.51 Spine2 -2.63 4.95 0\n0.51 LeftShoulder 0 0 0.07\n0.51 RightShoulder 0 0 -14.41\n0.51 LeftArm -62.07 -35.59 0\n0.51 RightArm 8.24 84.94 0\n0.51 LeftForeArm -40.36 -4.76 0\n0.51 RightForeArm 47.53 64.08 0\n0.51 Neck -9.71 5.62 0\n0.51 Head -14.4 8.4 0\n0.53 Spine -2.57 4.29 0\n0.53 Spine1 -2.94 5.01 0\n0.53 Spine2 -2.77 4.97 0\n0.53 LeftShoulder 0 0 0.13\n0.53 RightShoulder 0 0 -14.78\n0.53 LeftArm -62.01 -35.07 0\n0.53 RightArm 8.94 84.75 0\n0.53 LeftForeArm -40.19 -4.87 0\n0.53 RightForeArm 48 64 0\n0.53 Neck -9.83 5.64 0\n0.53 Head -14.44 8.41 0\n0.54 Spine -2.65 4.31 0\n0.54 Spine1 -3.03 5.02 0\n0.54 Spine2 -2.85 4.99 0\n0.54 LeftShoulder 0 0 0.17\n0.54 RightShoulder 0 0 -15\n0.54 LeftArm -62 -35 0\n0.54 RightArm 9.44 84.62 0\n0.54 LeftForeArm -40.12 -4.92 0\n0.54 RightForeArm 48.05 63.97 0\n0.54 Neck -9.91 5.65 0\n0.54 Head -14.49 8.42 0\n0.58 Spine -3.01 4.37 0\n0.58 Spine1 -3.44 5.1 0\n0.58 Spine2 -3.23 5.06 0\n0.58 LeftShoulder 0 0 0.35\n0.58 RightShoulder 0 0 -16.1\n0.58 LeftArm -61.87 -35.33 0\n0.58 RightArm 12.37 83.86 0\n0.58 LeftForeArm -40 -5 0\n0.58 RightForeArm 49.1 63.25 0\n0.58 Neck -10.32 5.72 0\n0.58 Head -14.87 8.48 0\n0.87 Spine -5.25 4.8 0\n0.87 Spine1 -6 5.6 0\n0.87 Spine2 -5.98 5.6 0\n0.87 LeftShoulder 0 0 1.96\n0.87 RightShoulder 0 0 -25.73\n0.87 LeftArm -57.05 -47.37 0\n0.87 RightArm 47.27 74.72 0\n0.87 LeftForeArm -35.74 -15.66 0\n0.87 RightForeArm 75.2 45.3 0\n0.87 Neck -14.24 6.37 0\n0.87 Head -20.85 9.48 0\n0.89 Spine -5.25 4.78 0\n0.89 Spine1 -6 5.57 0\n0.89 Spine2 -6 5.6 0\n0.89 LeftShoulder 0 0 1.99\n0.89 RightShoulder 0 0 -25.93\n0.89 LeftArm -56.76 -48.11 0\n0.89 RightArm 48.56 74.38 0\n0.89 LeftForeArm -35.38 -16.55 0\n0.89 RightForeArm 76.67 44.29 0\n0.89 Neck -14.34 6.39 0\n0.89 Head -21.13 9.52 0\n0.91 Spine -5.25 4.72 0\n0.91 Spine1 -6 5.5 0\n0.91 Spine2 -6 5.57 0\n0.91 LeftShoulder 0 0 2\n0.91 RightShoulder 0 0 -26\n0.91 LeftArm -56.5 -48.75 0\n0.91 RightArm 49.46 74.14 0\n0.91 LeftForeArm -35.05 -17.37 0\n0.91 RightForeArm 77.92 43.43 0\n0.91 Neck -14.39 6.4 0\n0.91 Head -21.35 9.56 0\n0.92 Spine -5.25 4.67 0\n0.92 Spine1 -6 5.45 0\n0.92 Spine2 -6 5.54 0\n0.92 LeftShoulder 0 0 2\n0.92 RightShoulder 0 0 -26\n0.92 LeftArm -56.39 -49.02 0\n0.92 RightArm 49.76 74.06 0\n0.92 LeftForeArm -34.9 -17.75 0\n0.92 RightForeArm 78.45 43.07 0\n0.92 Neck -14.4 6.4 0\n0.92 Head -21.44 9.57 0\n0.94 Spine -5.25 4.55 0\n0.94 Spine1 -6 5.31 0\n0.94 Spine2 -6 5.45 0\n0.94 LeftShoulder 0 0 2.01\n0.94 RightShoulder 0 0 -26\n0.94 LeftArm -56.21 -49.49 0\n0.94 RightArm 50 74 0\n0.94 LeftForeArm -34.62 -18.44 0\n0.94 RightForeArm 79.29 42.49 0\n0.94 Neck -14.4 6.37 0\n0.94 Head -21.56 9.59 0\n0.96 Spine -5.25 4.39 0\n0.96 Spine1 -6 5.12 0\n0.96 Spine2 -6 5.31 0\n0.96 LeftShoulder 0 0 2.03\n0.96 RightShoulder 0 0 -26\n0.96 LeftArm -56.08 -49.81 0\n0.96 RightArm 50.01 73.87 0\n0.96 LeftForeArm -34.39 -19.02 0\n0.96 RightForeArm 79.82 42.13 0\n0.96 Neck -14.4 6.29 0\n0.96 Head -21.6 9.6 0\n0.98 Spine -5.25 4.2 0\n0.98 Spine1 -6 4.9 0\n0.98 Spine2 -6 5.12 0\n0.98 LeftShoulder 0 0 2.06\n0.98 RightShoulder 0 0 -26\n0.98 LeftArm -56.01 -49.98 0\n0.98 RightArm 50.04 73.47 0\n0.98 LeftForeArm -34.21 -19.49 0\n0.98 RightForeArm 80 42 0\n0.98 Neck -14.4 6.15 0\n0.98 Head -21.6 9.56 0\n0.99 Spine -5.25 4.1 0\n0.99 Spine1 -6 4.78 0\n0.99 Spine2 -6 5.02 0\n0.99 LeftShoulder 0 0 2.07\n0.99 RightShoulder 0 0 -26\n0.99 LeftArm -56 -50 0\n0.99 RightArm 50.06 73.18 0\n0.99 LeftForeArm -34.13 -19.67 0\n0.99 RightForeArm 80 41.97 0\n0.99 Neck -14.4 6.06 0\n0.99 Head -21.6 9.5 0\n1.03 Spine -5.25 3.6 0\n1.03 Spine1 -6 4.21 0\n1.03 Spine2 -6 4.51 0\n1.03 LeftShoulder 0 0 2.16\n1.03 RightShoulder 0 0 -26\n1.03 LeftArm -55.98 -50.09 0\n1.03 RightArm 50.18 71.46 0\n1.03 LeftForeArm -34 -20 0\n1.03 RightForeArm 80.03 41.27 0\n1.03 Neck -14.4 5.6 0\n1.03 Head -21.6 9.1 0\n1.57 Spine -5.25 -4.2 0\n1.57 Spine1 -6 -4.9 0\n1.57 Spine2 -6 -4.87 0\n1.57 LeftShoulder 0 0 3.98\n1.57 RightShoulder 0 0 -26\n1.57 LeftArm -54.16 -59.22 0\n1.57 RightArm 53.89 19.57 0\n1.57 LeftForeArm -30.53 -28.67 0\n1.57 RightForeArm 81.87 -4.68 0\n1.57 Neck -14.4 -5.43 0\n1.57 Head -21.6 -7.58 0\n1.59 Spine -5.23 -4.22 0\n1.59 Spine1 -5.98 -4.92 0\n1.59 Spine2 -6 -4.9 0\n1.59 LeftShoulder 0 0 4\n1.59 RightShoulder 0 0 -26\n1.59 LeftArm -54.11 -59.45 0\n1.59 RightArm 53.94 18.82 0\n1.59 LeftForeArm -30.42 -28.96 0\n1.59 RightForeArm 81.91 -5.73 0\n1.59 Neck -14.4 -5.54 0\n1.59 Head -21.6 -7.9 0\n1.61 Spine -5.18 -4.26 0\n1.61 Spine1 -5.92 -4.97 0\n1.61 Spine2 -5.98 -4.92 0\n1.61 LeftShoulder 0 0 4\n1.61 RightShoulder 0 0 -26\n1.61 LeftArm -54.07 -59.64 0\n1.61 RightArm 53.98 18.3 0\n1.61 LeftForeArm -30.31 -29.22 0\n1.61 RightForeArm 81.94 -6.6 0\n1.61 Neck -14.4 -5.59 0\n1.61 Head -21.6 -8.14 0\n1.62 Spine -5.14 -4.29 0\n1.62 Spine1 -5.87 -5.01 0\n1.62 Spine2 -5.95 -4.94 0\n1.62 LeftShoulder 0 0 4\n1.62 RightShoulder 0 0 -25.99\n1.62 LeftArm -54.06 -59.72 0\n1.62 RightArm 53.99 18.13 0\n1.62 LeftForeArm -30.27 -29.34 0\n1.62 RightForeArm 81.96 -6.96 0\n1.62 Neck -14.4 -5.6 0\n1.62 Head -21.6 -8.23 0\n1.64 Spine -5.05 -4.37 0\n1.64 Spine1 -5.77 -5.1 0\n1.64 Spine2 -5.88 -5.01 0\n1.64 LeftShoulder 0 0 4\n1.64 RightShoulder 0 0 -25.88\n1.64 LeftArm -54.03 -59.85 0\n1.64 RightArm 54 18 0\n1.64 LeftForeArm -30.18 -29.55 0\n1.64 RightForeArm 81.98 -7.53 0\n1.64 Neck -14.36 -5.62 0\n1.64 Head -21.6 -8.36 0\n1.66 Spine -4.95 -4.46 0\n1.66 Spine1 -5.66 -5.2 0\n1.66 Spine2 -5.77 -5.1 0\n1.66 LeftShoulder 0 0 4\n1.66 RightShoulder 0 0 -25.69\n1.66 LeftArm -54.01 -59.95 0\n1.66 RightArm 53.89 17.78 0\n1.66 LeftForeArm -30.11 -29.72 0\n1.66 RightForeArm 82 -7.88 0\n1.66 Neck -14.23 -5.68 0\n1.66 Head -21.6 -8.4 0\n1.68 Spine -4.85 -4.54 0\n1.68 Spine1 -5.54 -5.3 0\n1.68 Spine2 -5.66 -5.2 0\n1.68 LeftShoulder 0 0 4\n1.68 RightShoulder 0 0 -25.44\n1.68 LeftArm -54 -59.99 0\n1.68 RightArm 53.58 17.17 0\n1.68 LeftForeArm -30.06 -29.85 0\n1.68 RightForeArm 82 -8 0\n1.68 Neck -14.05 -5.77 0\n1.68 Head -21.53 -8.43 0\n1.69 Spine -4.8 -4.59 0\n1.69 Spine1 -5.48 -5.35 0\n1.69 Spine2 -5.6 -5.25 0\n1.69 LeftShoulder 0 0 4\n1.69 RightShoulder 0 0 -25.3\n1.69 LeftArm -54 -60 0\n1.69 RightArm 53.38 16.75 0\n1.69 LeftForeArm -30.04 -29.91 0\n1.69 RightForeArm 81.96 -8.04 0\n1.69 Neck -13.95 -5.83 0\n1.69 Head -21.45 -8.47 0\n1.73 Spine -4.62 -4.74 0\n1.73 Spine1 -5.28 -5.53 0\n1.73 Spine2 -5.37 -5.45 0\n1.73 LeftShoulder 0 0 4\n1.73 RightShoulder 0 0 -24.7\n1.73 LeftArm -54.1 -60.21 0\n1.73 RightArm 52.3 14.6 0\n1.73 LeftForeArm -30 -30 0\n1.73 RightForeArm 81.06 -8.94 0\n1.73 Neck -13.48 -6.06 0\n1.73 Head -20.92 -8.74 0\n1.77 Spine -4.55 -4.8 0\n1.77 Spine1 -5.2 -5.6 0\n1.77 Spine2 -5.22 -5.58 0\n1.77 LeftShoulder 0 0 4\n1.77 RightShoulder 0 0 -24.21\n1.77 LeftArm -54.35 -60.7 0\n1.77 RightArm 51.13 12.25 0\n1.77 LeftForeArm -30.21 -30.21 0\n1.77 RightForeArm 79.45 -10.55 0\n1.77 Neck -13.05 -6.27 0\n1.77 Head -20.22 -9.09 0\n1.79 Spine -4.54 -4.79 0\n1.79 Spine1 -5.18 -5.58 0\n1.79 Spine2 -5.2 -5.6 0\n1.79 LeftShoulder 0 0 4\n1.79 RightShoulder 0 0 -24.06\n1.79 LeftArm -54.5 -61 0\n1.79 RightArm 50.63 11.25 0\n1.79 LeftForeArm -30.43 -30.43 0\n1.79 RightForeArm 78.55 -11.45 0\n1.79 Neck -12.9 -6.35 0\n1.79 Head -19.88 -9.26 0\n1.81 Spine -4.5 -4.75 0\n1.81 Spine1 -5.14 -5.54 0\n1.81 Spine2 -5.18 -5.58 0\n1.81 LeftShoulder 0 0 4\n1.81 RightShoulder 0 0 -24\n1.81 LeftArm -54.65 -61.3 0\n1.81 RightArm 50.24 10.49 0\n1.81 LeftForeArm -30.7 -30.7 0\n1.81 RightForeArm 77.69 -12.31 0\n1.81 Neck -12.81 -6.39 0\n1.81 Head -19.57 -9.41 0\n1.82 Spine -4.47 -4.72 0\n1.82 Spine1 -5.11 -5.5 0\n1.82 Spine2 -5.17 -5.56 0\n1.82 LeftShoulder 0 0 4\n1.82 RightShoulder 0 0 -23.98\n1.82 LeftArm -54.72 -61.44 0\n1.82 RightArm 50.11 10.22 0\n1.82 LeftForeArm -30.85 -30.85 0\n1.82 RightForeArm 77.3 -12.7 0\n1.82 Neck -12.8 -6.4 0\n1.82 Head -19.45 -9.48 0\n1.84 Spine -4.39 -4.64 0\n1.84 Spine1 -5.02 -5.41 0\n1.84 Spine2 -5.11 -5.5 0\n1.84 LeftShoulder 0 0 3.97\n1.84 RightShoulder 0 0 -23.85\n1.84 LeftArm -54.84 -61.69 0\n1.84 RightArm 50 10 0\n1.84 LeftForeArm -31.15 -31.15 0\n1.84 RightForeArm 76.62 -13.38 0\n1.84 Neck -12.77 -6.38 0\n1.84 Head -19.27 -9.57 0\n1.86 Spine -4.3 -4.54 0\n1.86 Spine1 -4.91 -5.3 0\n1.86 Spine2 -5.02 -5.41 0\n1.86 LeftShoulder 0 0 3.91\n1.86 RightShoulder 0 0 -23.58\n1.86 LeftArm -54.94 -61.88 0\n1.86 RightArm 49.68 10.12 0\n1.86 LeftForeArm -31.44 -31.44 0\n1.86 RightForeArm 76.17 -13.83 0\n1.86 Neck -12.67 -6.33 0\n1.86 Head -19.2 -9.6 0\n1.88 Spine -4.19 -4.43 0\n1.88 Spine1 -4.78 -5.16 0\n1.88 Spine2 -4.91 -5.3 0\n1.88 LeftShoulder 0 0 3.82\n1.88 RightShoulder 0 0 -23.2\n1.88 LeftArm -54.99 -61.99 0\n1.88 RightArm 48.76 10.45 0\n1.88 LeftForeArm -31.69 -31.69 0\n1.88 RightForeArm 76 -14 0\n1.88 Neck -12.51 -6.24 0\n1.88 Head -19.15 -9.57 0\n1.89 Spine -4.12 -4.36 0\n1.89 Spine1 -4.71 -5.09 0\n1.89 Spine2 -4.85 -5.23 0\n1.89 LeftShoulder 0 0 3.77\n1.89 RightShoulder 0 0 -22.97\n1.89 LeftArm -55 -62 0\n1.89 RightArm 48.09 10.7 0\n1.89 LeftForeArm -31.79 -31.79 0\n1.89 RightForeArm 75.93 -13.95 0\n1.89 Neck -12.41 -6.19 0\n1.89 Head -19.09 -9.54 0\n1.93 Spine -3.83 -4.06 0\n1.93 Spine1 -4.38 -4.74 0\n1.93 Spine2 -4.55 -4.92 0\n1.93 LeftShoulder 0 0 3.51\n1.93 RightShoulder 0 0 -21.8\n1.93 LeftArm -55.14 -61.74 0\n1.93 RightArm 44.13 12.15 0\n1.93 LeftForeArm -32 -32 0\n1.93 RightForeArm 74.42 -12.7 0\n1.93 Neck -11.88 -5.9 0\n1.93 Head -18.61 -9.28 0\n2.32 Spine -1.05 -1.2 0\n2.32 Spine1 -1.2 -1.4 0\n2.32 Spine2 -1.22 -1.42 0\n2.32 LeftShoulder 0 0 0.06\n2.32 RightShoulder 0 0 -6.27\n2.32 LeftArm -62.9 -47.07 0\n2.32 RightArm -28.35 38.67 0\n2.32 LeftForeArm -39.95 -14.5 0\n2.32 RightForeArm 15.07 36.18 0\n2.32 Neck -4.2 -1.71 0\n2.32 Head -6.94 -2.92 0\n2.34 Spine -1.04 -1.19 0\n2.34 Spine1 -1.19 -1.39 0\n2.34 Spine2 -1.2 -1.4 0\n2.34 LeftShoulder 0 0 0.02\n2.34 RightShoulder 0 0 -6.07\n2.34 LeftArm -63.22 -46.48 0\n2.34 RightArm -30.09 39.3 0\n2.34 LeftForeArm -40.39 -13.55 0\n2.34 RightForeArm 12.87 37.99 0\n2.34 Neck -4.08 -1.64 0\n2.34 Head -6.59 -2.72 0\n2.36 Spine -1.03 -1.18 0\n2.36 Spine1 -1.18 -1.37 0\n2.36 Spine2 -1.19 -1.39 0\n2.36 LeftShoulder 0 0 0\n2.36 RightShoulder 0 0 -6\n2.36 LeftArm -63.48 -45.97 0\n2.36 RightArm -31.29 39.74 0\n2.36 LeftForeArm -40.78 -12.68 0\n2.36 RightForeArm 11.02 39.51 0\n2.36 Neck -4.01 -1.6 0\n2.36 Head -6.31 -2.57 0\n2.37 Spine -1.02 -1.16 0\n2.37 Spine1 -1.16 -1.36 0\n2.37 Spine2 -1.19 -1.38 0\n2.37 LeftShoulder 0 0 0\n2.37 RightShoulder 0 0 -5.99\n2.37 LeftArm -63.6 -45.76 0\n2.37 RightArm -31.68 39.88 0\n2.37 LeftForeArm -40.96 -12.29 0\n2.37 RightForeArm 10.25 40.15 0\n2.37 Neck -4 -1.6 0\n2.37 Head -6.2 -2.51 0\n2.39 Spine -0.99 -1.13 0\n2.39 Spine1 -1.13 -1.32 0\n2.39 Spine2 -1.16 -1.35 0\n2.39 LeftShoulder 0 0 0\n2.39 RightShoulder 0 0 -5.92\n2.39 LeftArm -63.79 -45.4 0\n2.39 RightArm -32 40 0\n2.39 LeftForeArm -41.28 -11.57 0\n2.39 RightForeArm 9.03 41.15 0\n2.39 Neck -3.97 -1.59 0\n2.39 Head -6.05 -2.43 0\n2.41 Spine -0.95 -1.09 0\n2.41 Spine1 -1.09 -1.27 0\n2.41 Spine2 -1.13 -1.31 0\n2.41 LeftShoulder 0 0 0\n2.41 RightShoulder 0 0 -5.79\n2.41 LeftArm -63.92 -45.15 0\n2.41 RightArm -32.12 40.88 0\n2.41 LeftForeArm -41.56 -10.98 0\n2.41 RightForeArm 8.26 41.78 0\n2.41 Neck -3.9 -1.56 0\n2.41 Head -6 -2.4 0\n2.43 Spine -0.91 -1.04 0\n2.43 Spine1 -1.04 -1.21 0\n2.43 Spine2 -1.08 -1.26 0\n2.43 LeftShoulder 0 0 0\n2.43 RightShoulder 0 0 -5.59\n2.43 LeftArm -63.99 -45.02 0\n2.43 RightArm -32.48 43.39 0\n2.43 LeftForeArm -41.77 -10.51 0\n2.43 RightForeArm 8 42 0\n2.43 Neck -3.79 -1.52 0\n2.43 Head -5.95 -2.38 0\n2.44 Spine -0.89 -1.01 0\n2.44 Spine1 -1.01 -1.18 0\n2.44 Spine2 -1.05 -1.23 0\n2.44 LeftShoulder 0 0 0\n2.44 RightShoulder 0 0 -5.48\n2.44 LeftArm -64 -45 0\n2.44 RightArm -32.74 45.21 0\n2.44 LeftForeArm -41.85 -10.33 0\n2.44 RightForeArm 7.84 42.09 0\n2.44 Neck -3.72 -1.49 0\n2.44 Head -5.9 -2.36 0\n2.48 Spine -0.78 -0.89 0\n2.48 Spine1 -0.89 -1.04 0\n2.48 Spine2 -0.93 -1.09 0\n2.48 LeftShoulder 0 0 0\n2.48 RightShoulder 0 0 -4.9\n2.48 LeftArm -64.21 -46.92 0\n2.48 RightArm -34.22 55.67 0\n2.48 LeftForeArm -42 -10 0\n2.48 RightForeArm 4.31 44.09 0\n2.48 Neck -3.35 -1.34 0\n2.48 Head -5.49 -2.2 0\n2.8 Spine 0 0 0\n2.8 Spine1 0 0 0\n2.8 Spine2 0 0 0\n2.8 LeftShoulder 0 0 0\n2.8 RightShoulder 0 0 0\n2.8 LeftArm -70 -101 0\n2.8 RightArm -50 167 0\n2.8 LeftForeArm -49 1 0\n2.8 RightForeArm -66 84 0\n2.8 Neck 0 0 0\n2.8 Head 0 0 0\n",
            "clip listen_r\nlength 2.7\nloop 0\npivot 1.1\nmode Neck body\nmode Head body\n0 Neck 0 0 0\n0 Head 0 0 0\n0.35 Neck 2 10 0\n0.35 Head 3 26 4\n0.9 Neck 3 11 0\n0.9 Head 5 28 5\n1.25 Neck 3 11 0\n1.25 Head 9 27 5\n1.6 Neck 3 11 0\n1.6 Head 4 28 4\n2.1 Neck 2 9 0\n2.1 Head 3 24 3\n2.7 Neck 0 0 0\n2.7 Head 0 0 0\n",
            "clip listen_l\nlength 2.7\nloop 0\npivot 1.1\nmode Neck body\nmode Head body\n0 Neck 0 0 0\n0 Head 0 0 0\n0.35 Neck 2 -10 0\n0.35 Head 3 -26 -4\n0.9 Neck 3 -11 0\n0.9 Head 5 -28 -5\n1.25 Neck 3 -11 0\n1.25 Head 9 -27 -5\n1.6 Neck 3 -11 0\n1.6 Head 4 -28 -4\n2.1 Neck 2 -9 0\n2.1 Head 3 -24 -3\n2.7 Neck 0 0 0\n2.7 Head 0 0 0\n",
            "clip fate_offer_r\nlength 2.7\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode RightShoulder body\nmode RightArm aim\nmode RightForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 RightShoulder 0 0 0\n0 RightArm -50 167 0\n0 RightForeArm -66 84 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 1 2 1\n0.28 Spine2 1 3 2\n0.28 RightShoulder -1 0 -4.8\n0.28 RightArm -60.8 55 0\n0.28 RightForeArm -32.9 38.9 0\n0.28 Neck 1.8 3.7 0\n0.28 Head 4.1 6.6 0\n0.3 Spine1 1 2 1\n0.3 Spine2 1 3 2\n0.3 RightShoulder -1 0 -4.9\n0.3 RightArm -61.3 48.9 0\n0.3 RightForeArm -30.5 35.6 0\n0.3 Neck 1.9 3.8 0\n0.3 Head 4.4 7.1 0\n0.32 Spine1 1 2.1 1.1\n0.32 Spine2 1 3 2\n0.32 RightShoulder -1 0 -5\n0.32 RightArm -61.8 44.6 0\n0.32 RightForeArm -28.5 32.8 0\n0.32 Neck 2 4 0\n0.32 Head 4.7 7.5 0\n0.34 Spine1 1.1 2.1 1.1\n0.34 Spine2 1.1 3.1 2.1\n0.34 RightShoulder -1 0 -5.1\n0.34 RightArm -62 42.3 0\n0.34 RightForeArm -26.8 30.5 0\n0.34 Neck 2 4 0\n0.34 Head 4.8 7.8 0\n0.35 Spine1 1.1 2.1 1.1\n0.35 Spine2 1.1 3.1 2.1\n0.35 RightShoulder -1 0 -5.2\n0.35 RightArm -62 42 0\n0.35 RightForeArm -26.2 29.6 0\n0.35 Neck 2 4 0\n0.35 Head 4.9 7.9 0\n0.38 Spine1 1.1 2.3 1.3\n0.38 Spine2 1.2 3.3 2.3\n0.38 RightShoulder -1.1 0 -5.6\n0.38 RightArm -61.3 42.3 0\n0.38 RightForeArm -25.1 28.1 0\n0.38 Neck 2.1 4.1 0\n0.38 Head 5 8 0\n0.39 Spine1 1.2 2.3 1.3\n0.39 Spine2 1.2 3.4 2.4\n0.39 RightShoulder -1.1 0 -5.8\n0.39 RightArm -60.8 42.5 0\n0.39 RightForeArm -25 28 0\n0.39 Neck 2.1 4.2 0\n0.39 Head 5 8 0\n0.7 Spine1 2 4 3\n0.7 Spine2 3 6 5\n0.7 RightShoulder -2 0 -15.7\n0.7 RightArm -17.6 60.5 0\n0.7 RightForeArm 53.9 21.4 0\n0.7 Neck 4.8 8.7 0\n0.7 Head 11.9 16.6 0\n0.72 Spine1 2 4 3\n0.72 Spine2 3 6 5\n0.72 RightShoulder -2 0 -15.9\n0.72 RightArm -15.9 61.2 0\n0.72 RightForeArm 58.8 20.9 0\n0.72 Neck 4.9 8.9 0\n0.72 Head 12.2 17 0\n0.74 Spine1 2 4 3\n0.74 Spine2 3 6 5\n0.74 RightShoulder -2 0 -16\n0.74 RightArm -14.7 61.7 0\n0.74 RightForeArm 63 20.6 0\n0.74 Neck 5 9 0\n0.74 Head 12.6 17.4 0\n0.76 Spine1 1.9 4 3\n0.76 Spine2 3 6 5\n0.76 RightShoulder -2 0 -16\n0.76 RightArm -14.1 62 0\n0.76 RightForeArm 66.3 20.3 0\n0.76 Neck 5 9 0\n0.76 Head 12.8 17.7 0\n0.77 Spine1 1.9 4 3\n0.77 Spine2 2.9 6 5\n0.77 RightShoulder -2 0 -16.1\n0.77 RightArm -14 62 0\n0.77 RightForeArm 67.6 20.2 0\n0.77 Neck 5 9 0\n0.77 Head 12.9 17.9 0\n0.8 Spine1 1.7 4 3\n0.8 Spine2 2.8 6 5\n0.8 RightShoulder -2 0 -16.3\n0.8 RightArm -13.8 62 0\n0.8 RightForeArm 69.8 20 0\n0.8 Neck 5 9 0\n0.8 Head 13 18 0\n0.81 Spine1 1.7 4 3\n0.81 Spine2 2.8 6 5\n0.81 RightShoulder -2 0 -16.4\n0.81 RightArm -13.7 62 0\n0.81 RightForeArm 70 20 0\n0.81 Neck 4.9 9 0\n0.81 Head 13 18 0\n1 Spine1 1 4 3\n1 Spine2 2 6 5\n1 RightShoulder -2 0 -18.9\n1 RightArm -8.8 62 0\n1 RightForeArm 75.6 20 0\n1 Neck 4.1 9 0\n1 Head 11.5 18 0\n1.02 Spine1 1 4 3\n1.02 Spine2 2 6 5\n1.02 RightShoulder -2 0 -19\n1.02 RightArm -8.4 62 0\n1.02 RightForeArm 76.3 20 0\n1.02 Neck 4 9 0\n1.02 Head 11.4 18 0\n1.04 Spine1 1 4 3\n1.04 Spine2 2 6 5\n1.04 RightShoulder -2 0 -19\n1.04 RightArm -8.2 62 0\n1.04 RightForeArm 76.9 20 0\n1.04 Neck 4 9 0\n1.04 Head 11.2 18 0\n1.06 Spine1 1 4 3\n1.06 Spine2 2 6 5\n1.06 RightShoulder -2 0 -19\n1.06 RightArm -8 62 0\n1.06 RightForeArm 77.4 20 0\n1.06 Neck 4 9 0\n1.06 Head 11.1 18 0\n1.07 Spine1 1 4 3\n1.07 Spine2 2 6 5\n1.07 RightShoulder -2 0 -19\n1.07 RightArm -8 62 0\n1.07 RightForeArm 77.6 20 0\n1.07 Neck 4 9 0\n1.07 Head 11.1 18 0\n1.1 Spine1 1.1 4 3\n1.1 Spine2 2 6 5\n1.1 RightShoulder -2 0 -18.9\n1.1 RightArm -8.1 62 0\n1.1 RightForeArm 78 20 0\n1.1 Neck 4 9 0\n1.1 Head 11 18 0\n1.11 Spine1 1.1 4 3\n1.11 Spine2 2.1 6 5\n1.11 RightShoulder -2 0 -18.9\n1.11 RightArm -8.1 62 0\n1.11 RightForeArm 78 20 0\n1.11 Neck 4 9 0\n1.11 Head 11 18 0\n1.6 Spine1 2 4 3\n1.6 Spine2 3 6 5\n1.6 RightShoulder -2 0 -16\n1.6 RightArm -14.7 62 0\n1.6 RightForeArm 68.9 20 0\n1.6 Neck 5 9 0\n1.6 Head 12.9 18 0\n1.62 Spine1 2 4 3\n1.62 Spine2 3 6 5\n1.62 RightShoulder -2 0 -16\n1.62 RightArm -14.9 62 0\n1.62 RightForeArm 68.6 20 0\n1.62 Neck 5 9 0\n1.62 Head 12.9 18 0\n1.64 Spine1 2 4 3\n1.64 Spine2 3 6 5\n1.64 RightShoulder -2 0 -16\n1.64 RightArm -14.9 62 0\n1.64 RightForeArm 68.4 20 0\n1.64 Neck 5 9 0\n1.64 Head 12.9 18 0\n1.66 Spine1 2 3.9 2.9\n1.66 Spine2 3 6 5\n1.66 RightShoulder -2 0 -16\n1.66 RightArm -15 62 0\n1.66 RightForeArm 68.2 20 0\n1.66 Neck 5 9 0\n1.66 Head 13 18 0\n1.67 Spine1 2 3.9 2.9\n1.67 Spine2 3 5.9 4.9\n1.67 RightShoulder -2 0 -15.9\n1.67 RightArm -15 62 0\n1.67 RightForeArm 68.1 20 0\n1.67 Neck 5 9 0\n1.67 Head 13 18 0\n1.7 Spine1 1.9 3.8 2.8\n1.7 Spine2 2.9 5.8 4.8\n1.7 RightShoulder -2 0 -15.6\n1.7 RightArm -15.4 61.8 0\n1.7 RightForeArm 68 20 0\n1.7 Neck 5 8.9 0\n1.7 Head 13 18 0\n1.71 Spine1 1.9 3.8 2.8\n1.71 Spine2 2.9 5.8 4.8\n1.71 RightShoulder -2 0 -15.5\n1.71 RightArm -15.7 61.7 0\n1.71 RightForeArm 68 20 0\n1.71 Neck 4.9 8.9 0\n1.71 Head 13 18 0\n2.15 Spine1 1 2 1\n2.15 Spine2 1 3 2\n2.15 RightShoulder -1 0 -5.2\n2.15 RightArm -59.9 42.9 0\n2.15 RightForeArm -15.3 27.2 0\n2.15 Neck 2.1 4.2 0\n2.15 Head 5.7 8.9 0\n2.17 Spine1 1 2 1\n2.17 Spine2 1 3 2\n2.17 RightShoulder -1 0 -5\n2.17 RightArm -60.9 42.5 0\n2.17 RightForeArm -18.3 27.4 0\n2.17 Neck 2 4.1 0\n2.17 Head 5.5 8.6 0\n2.19 Spine1 1 2 1\n2.19 Spine2 1 3 2\n2.19 RightShoulder -1 0 -5\n2.19 RightArm -61.6 42.2 0\n2.19 RightForeArm -20.9 27.6 0\n2.19 Neck 2 4 0\n2.19 Head 5.3 8.3 0\n2.21 Spine1 1 1.9 1\n2.21 Spine2 1 3 2\n2.21 RightShoulder -1 0 -5\n2.21 RightArm -62 42 0\n2.21 RightForeArm -22.8 27.8 0\n2.21 Neck 2 4 0\n2.21 Head 5.1 8.2 0\n2.22 Spine1 1 1.9 1\n2.22 Spine2 1 2.9 1.9\n2.22 RightShoulder -1 0 -5\n2.22 RightArm -62 42 0\n2.22 RightForeArm -23.6 27.9 0\n2.22 Neck 2 4 0\n2.22 Head 5.1 8.1 0\n2.25 Spine1 0.9 1.8 0.9\n2.25 Spine2 0.9 2.8 1.9\n2.25 RightShoulder -1 0 -4.8\n2.25 RightArm -61.9 43.4 0\n2.25 RightForeArm -24.9 28 0\n2.25 Neck 2 3.9 0\n2.25 Head 5 8 0\n2.26 Spine1 0.9 1.8 0.9\n2.26 Spine2 0.9 2.8 1.8\n2.26 RightShoulder -0.9 0 -4.7\n2.26 RightArm -61.8 44.5 0\n2.26 RightForeArm -25 28 0\n2.26 Neck 1.9 3.9 0\n2.26 Head 5 8 0\n2.7 Spine1 0 0 0\n2.7 Spine2 0 0 0\n2.7 RightShoulder 0 0 0\n2.7 RightArm -50 167 0\n2.7 RightForeArm -66 84 0\n2.7 Neck 0 0 0\n2.7 Head 0 0 0\n",
            "clip fate_offer_l\nlength 2.7\nloop 0\npivot 1.1\nmode Spine1 body\nmode Spine2 body\nmode LeftShoulder body\nmode LeftArm aim\nmode LeftForeArm aim\nmode Neck body\nmode Head body\n0 Spine1 0 0 0\n0 Spine2 0 0 0\n0 LeftShoulder 0 0 0\n0 LeftArm -70 -101 0\n0 LeftForeArm -49 1 0\n0 Neck 0 0 0\n0 Head 0 0 0\n0.28 Spine1 1 -2 -1\n0.28 Spine2 1 -3 -2\n0.28 LeftShoulder -1 0 4.8\n0.28 LeftArm -62.8 -48.1 0\n0.28 LeftForeArm -29.7 -22.4 0\n0.28 Neck 1.8 -3.7 0\n0.28 Head 4.1 -6.6 0\n0.3 Spine1 1 -2 -1\n0.3 Spine2 1 -3 -2\n0.3 LeftShoulder -1 0 4.9\n0.3 LeftArm -62.4 -45.3 0\n0.3 LeftForeArm -28.2 -24.1 0\n0.3 Neck 1.9 -3.8 0\n0.3 Head 4.4 -7.1 0\n0.32 Spine1 1 -2.1 -1.1\n0.32 Spine2 1 -3 -2\n0.32 LeftShoulder -1 0 5\n0.32 LeftArm -62.2 -43.2 0\n0.32 LeftForeArm -27 -25.5 0\n0.32 Neck 2 -4 0\n0.32 Head 4.7 -7.5 0\n0.34 Spine1 1.1 -2.1 -1.1\n0.34 Spine2 1.1 -3.1 -2.1\n0.34 LeftShoulder -1 0 5.1\n0.34 LeftArm -62 -42.1 0\n0.34 LeftForeArm -26.1 -26.7 0\n0.34 Neck 2 -4 0\n0.34 Head 4.8 -7.8 0\n0.35 Spine1 1.1 -2.1 -1.1\n0.35 Spine2 1.1 -3.1 -2.1\n0.35 LeftShoulder -1 0 5.2\n0.35 LeftArm -62 -42 0\n0.35 LeftForeArm -25.7 -27.1 0\n0.35 Neck 2 -4 0\n0.35 Head 4.9 -7.9 0\n0.38 Spine1 1.1 -2.3 -1.3\n0.38 Spine2 1.2 -3.3 -2.3\n0.38 LeftShoulder -1.1 0 5.6\n0.38 LeftArm -61.3 -42.3 0\n0.38 LeftForeArm -25 -27.9 0\n0.38 Neck 2.1 -4.1 0\n0.38 Head 5 -8 0\n0.39 Spine1 1.2 -2.3 -1.3\n0.39 Spine2 1.2 -3.4 -2.4\n0.39 LeftShoulder -1.1 0 5.8\n0.39 LeftArm -60.8 -42.5 0\n0.39 LeftForeArm -25 -28 0\n0.39 Neck 2.1 -4.2 0\n0.39 Head 5 -8 0\n0.7 Spine1 2 -4 -3\n0.7 Spine2 3 -6 -5\n0.7 LeftShoulder -2 0 15.7\n0.7 LeftArm -17.6 -60.5 0\n0.7 LeftForeArm 53.9 -21.4 0\n0.7 Neck 4.8 -8.7 0\n0.7 Head 11.9 -16.6 0\n0.72 Spine1 2 -4 -3\n0.72 Spine2 3 -6 -5\n0.72 LeftShoulder -2 0 15.9\n0.72 LeftArm -15.9 -61.2 0\n0.72 LeftForeArm 58.8 -20.9 0\n0.72 Neck 4.9 -8.9 0\n0.72 Head 12.2 -17 0\n0.74 Spine1 2 -4 -3\n0.74 Spine2 3 -6 -5\n0.74 LeftShoulder -2 0 16\n0.74 LeftArm -14.7 -61.7 0\n0.74 LeftForeArm 63 -20.6 0\n0.74 Neck 5 -9 0\n0.74 Head 12.6 -17.4 0\n0.76 Spine1 1.9 -4 -3\n0.76 Spine2 3 -6 -5\n0.76 LeftShoulder -2 0 16\n0.76 LeftArm -14.1 -62 0\n0.76 LeftForeArm 66.3 -20.3 0\n0.76 Neck 5 -9 0\n0.76 Head 12.8 -17.7 0\n0.77 Spine1 1.9 -4 -3\n0.77 Spine2 2.9 -6 -5\n0.77 LeftShoulder -2 0 16.1\n0.77 LeftArm -14 -62 0\n0.77 LeftForeArm 67.6 -20.2 0\n0.77 Neck 5 -9 0\n0.77 Head 12.9 -17.9 0\n0.8 Spine1 1.7 -4 -3\n0.8 Spine2 2.8 -6 -5\n0.8 LeftShoulder -2 0 16.3\n0.8 LeftArm -13.8 -62 0\n0.8 LeftForeArm 69.8 -20 0\n0.8 Neck 5 -9 0\n0.8 Head 13 -18 0\n0.81 Spine1 1.7 -4 -3\n0.81 Spine2 2.8 -6 -5\n0.81 LeftShoulder -2 0 16.4\n0.81 LeftArm -13.7 -62 0\n0.81 LeftForeArm 70 -20 0\n0.81 Neck 4.9 -9 0\n0.81 Head 13 -18 0\n1 Spine1 1 -4 -3\n1 Spine2 2 -6 -5\n1 LeftShoulder -2 0 18.9\n1 LeftArm -8.8 -62 0\n1 LeftForeArm 75.6 -20 0\n1 Neck 4.1 -9 0\n1 Head 11.5 -18 0\n1.02 Spine1 1 -4 -3\n1.02 Spine2 2 -6 -5\n1.02 LeftShoulder -2 0 19\n1.02 LeftArm -8.4 -62 0\n1.02 LeftForeArm 76.3 -20 0\n1.02 Neck 4 -9 0\n1.02 Head 11.4 -18 0\n1.04 Spine1 1 -4 -3\n1.04 Spine2 2 -6 -5\n1.04 LeftShoulder -2 0 19\n1.04 LeftArm -8.2 -62 0\n1.04 LeftForeArm 76.9 -20 0\n1.04 Neck 4 -9 0\n1.04 Head 11.2 -18 0\n1.06 Spine1 1 -4 -3\n1.06 Spine2 2 -6 -5\n1.06 LeftShoulder -2 0 19\n1.06 LeftArm -8 -62 0\n1.06 LeftForeArm 77.4 -20 0\n1.06 Neck 4 -9 0\n1.06 Head 11.1 -18 0\n1.07 Spine1 1 -4 -3\n1.07 Spine2 2 -6 -5\n1.07 LeftShoulder -2 0 19\n1.07 LeftArm -8 -62 0\n1.07 LeftForeArm 77.6 -20 0\n1.07 Neck 4 -9 0\n1.07 Head 11.1 -18 0\n1.1 Spine1 1.1 -4 -3\n1.1 Spine2 2 -6 -5\n1.1 LeftShoulder -2 0 18.9\n1.1 LeftArm -8.1 -62 0\n1.1 LeftForeArm 78 -20 0\n1.1 Neck 4 -9 0\n1.1 Head 11 -18 0\n1.11 Spine1 1.1 -4 -3\n1.11 Spine2 2.1 -6 -5\n1.11 LeftShoulder -2 0 18.9\n1.11 LeftArm -8.1 -62 0\n1.11 LeftForeArm 78 -20 0\n1.11 Neck 4 -9 0\n1.11 Head 11 -18 0\n1.6 Spine1 2 -4 -3\n1.6 Spine2 3 -6 -5\n1.6 LeftShoulder -2 0 16\n1.6 LeftArm -14.7 -62 0\n1.6 LeftForeArm 68.9 -20 0\n1.6 Neck 5 -9 0\n1.6 Head 12.9 -18 0\n1.62 Spine1 2 -4 -3\n1.62 Spine2 3 -6 -5\n1.62 LeftShoulder -2 0 16\n1.62 LeftArm -14.9 -62 0\n1.62 LeftForeArm 68.6 -20 0\n1.62 Neck 5 -9 0\n1.62 Head 12.9 -18 0\n1.64 Spine1 2 -4 -3\n1.64 Spine2 3 -6 -5\n1.64 LeftShoulder -2 0 16\n1.64 LeftArm -14.9 -62 0\n1.64 LeftForeArm 68.4 -20 0\n1.64 Neck 5 -9 0\n1.64 Head 12.9 -18 0\n1.66 Spine1 2 -3.9 -2.9\n1.66 Spine2 3 -6 -5\n1.66 LeftShoulder -2 0 16\n1.66 LeftArm -15 -62 0\n1.66 LeftForeArm 68.2 -20 0\n1.66 Neck 5 -9 0\n1.66 Head 13 -18 0\n1.67 Spine1 2 -3.9 -2.9\n1.67 Spine2 3 -5.9 -4.9\n1.67 LeftShoulder -2 0 15.9\n1.67 LeftArm -15 -62 0\n1.67 LeftForeArm 68.1 -20 0\n1.67 Neck 5 -9 0\n1.67 Head 13 -18 0\n1.7 Spine1 1.9 -3.8 -2.8\n1.7 Spine2 2.9 -5.8 -4.8\n1.7 LeftShoulder -2 0 15.6\n1.7 LeftArm -15.4 -61.8 0\n1.7 LeftForeArm 68 -20 0\n1.7 Neck 5 -8.9 0\n1.7 Head 13 -18 0\n1.71 Spine1 1.9 -3.8 -2.8\n1.71 Spine2 2.9 -5.8 -4.8\n1.71 LeftShoulder -2 0 15.5\n1.71 LeftArm -15.7 -61.7 0\n1.71 LeftForeArm 68 -20 0\n1.71 Neck 4.9 -8.9 0\n1.71 Head 13 -18 0\n2.15 Spine1 1 -2 -1\n2.15 Spine2 1 -3 -2\n2.15 LeftShoulder -1 0 5.2\n2.15 LeftArm -59.9 -42.9 0\n2.15 LeftForeArm -15.3 -27.2 0\n2.15 Neck 2.1 -4.2 0\n2.15 Head 5.7 -8.9 0\n2.17 Spine1 1 -2 -1\n2.17 Spine2 1 -3 -2\n2.17 LeftShoulder -1 0 5\n2.17 LeftArm -60.9 -42.5 0\n2.17 LeftForeArm -18.3 -27.4 0\n2.17 Neck 2 -4.1 0\n2.17 Head 5.5 -8.6 0\n2.19 Spine1 1 -2 -1\n2.19 Spine2 1 -3 -2\n2.19 LeftShoulder -1 0 5\n2.19 LeftArm -61.6 -42.2 0\n2.19 LeftForeArm -20.9 -27.6 0\n2.19 Neck 2 -4 0\n2.19 Head 5.3 -8.3 0\n2.21 Spine1 1 -1.9 -1\n2.21 Spine2 1 -3 -2\n2.21 LeftShoulder -1 0 5\n2.21 LeftArm -62 -42 0\n2.21 LeftForeArm -22.8 -27.8 0\n2.21 Neck 2 -4 0\n2.21 Head 5.1 -8.2 0\n2.22 Spine1 1 -1.9 -1\n2.22 Spine2 1 -2.9 -1.9\n2.22 LeftShoulder -1 0 5\n2.22 LeftArm -62 -42 0\n2.22 LeftForeArm -23.6 -27.9 0\n2.22 Neck 2 -4 0\n2.22 Head 5.1 -8.1 0\n2.25 Spine1 0.9 -1.8 -0.9\n2.25 Spine2 0.9 -2.8 -1.9\n2.25 LeftShoulder -1 0 4.8\n2.25 LeftArm -62.1 -42.7 0\n2.25 LeftForeArm -24.9 -28 0\n2.25 Neck 2 -3.9 0\n2.25 Head 5 -8 0\n2.26 Spine1 0.9 -1.8 -0.9\n2.26 Spine2 0.9 -2.8 -1.8\n2.26 LeftShoulder -0.9 0 4.7\n2.26 LeftArm -62.2 -43.2 0\n2.26 LeftForeArm -25 -28 0\n2.26 Neck 1.9 -3.9 0\n2.26 Head 5 -8 0\n2.7 Spine1 0 0 0\n2.7 Spine2 0 0 0\n2.7 LeftShoulder 0 0 0\n2.7 LeftArm -70 -101 0\n2.7 LeftForeArm -49 1 0\n2.7 Neck 0 0 0\n2.7 Head 0 0 0\n",
        };

        // the built-in rules: the states of «Крила Одіна» (the jolts, the take-off and the landing are played by OdinWings on slot 2)
        private static readonly string BuiltinRules =
            "rule wings_flare slot 0 clip wings_flare in 0.6 out 0.4 speed 1 when tag:odinwings_land\n" +
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
            "rule wings_speed slot 4 clip wings_speed in 0.7 out 0.9 speed 1 when tag:odinwings & !tag:odinwings_sink\n" +
            "rule wings_lean slot 3 clip wings_lean in 0.7 out 0.7 speed 1 when tag:odinwings & !tag:odinwings_sink & !tag:odinwings_land\n" +
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

        // a small gesture of the hand on the side of that raven (feed_, order_send_, order_circle_, order_follow_, call_, pet_ + r/l): your own game only,
        // never over another gesture (a gift's, the stone's, one already under way), not in flight or a swing
        internal static bool Gesture(Player p, bool munin, string clip)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || !RavenSkills.Animations || OdinWings.Flying() || p.InAttack() || p.IsBlocking()) return false;
            PlayerAnimator a = PlayerAnimator.For(p);
            if (a == null) return false;
            if (a.Playing(2)) return false; // one gesture at a time: an order to both ravens, both fed — the first one plays on, never cut into the other hand
            GameObject m = Shoulder.ModelOf(p, munin);
            bool right = m != null ? Vector3.Dot(m.transform.position - p.transform.position, p.transform.right) >= 0f : !munin;
            Play(p, 2, clip + (right ? "r" : "l"), 0.2f, UnityEngine.Random.Range(0.92f, 1.08f));
            return true;
        }

        // the head turns to the raven on that shoulder as it speaks, a little nod in the middle (its own layer: over a
        // gift's or a gesture's arms; not in flight, not mid-swing, not while a gesture already turns the head)
        internal static float ListenChance = 0.75f, ListenEvery = 5f;
        private static float _listenAt;
        internal static void Listen(Player p, bool munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer) || !RavenSkills.Animations || OdinWings.Flying() || p.InAttack()) return;
            if (Time.time < _listenAt || UnityEngine.Random.value > ListenChance) return;
            GameObject m = Shoulder.ModelOf(p, munin);
            if (m == null || AnimDirector.Of(m) != RavenAct.Sit) return; // (only a bird on you: not one flying off or away)
            PlayerAnimator a = PlayerAnimator.For(p);
            if (a == null || a.Playing(2) || a.Playing(ListenSlot)) return;
            _listenAt = Time.time + ListenEvery;
            bool right = Vector3.Dot(m.transform.position - p.transform.position, p.transform.right) >= 0f;
            Play(p, ListenSlot, right ? "listen_r" : "listen_l", 0.3f, UnityEngine.Random.Range(0.9f, 1.1f));
        }
        internal const int ListenSlot = 3;

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
        private float _lastFwd, _pull, _leanV;
        internal static float LeanSpring = 5f, LeanDamp = 0.5f; // the pull's spring: how quick (1/s) and how damped (1: no swing past)
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
            Transform t = PlayerAnims.SkeletonBone(_p, _visual, name);
            if (t == null) { _missing.Add(name); return null; }
            b = new Bone { T = t };
            _bones[name] = b;
            return b;
        }

        // ---------------------------------------------------------------- measuring

        private static readonly int GroundHash = Animator.StringToHash("onGround"), WaterHash = Animator.StringToHash("inWater");
        private const int Ground = 5, Water = 6, Air = 7, Height = 8, Attack = 9, Accel = 10;

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
            // the pull ahead (+) or the braking (−), felt by the body with its weight: a soft spring after the
            // change of the pace, a little late and swinging past (a lean that settles, not one that snaps)
            float pull = (_values[1] - _lastFwd) / dt;
            _lastFwd = _values[1];
            _pull = Mathf.Lerp(_pull, Mathf.Clamp(pull, -20f, 20f), 1f - Mathf.Exp(-(_p == Player.m_localPlayer ? 8f : 3f) * dt)); // (another player's pace comes over the network in steps: read more softly)
            float sdt = Mathf.Min(dt, 0.05f); // (a long frame — a hitch, a load — must not throw the spring)
            _leanV += ((_pull - _values[Accel]) * LeanSpring * LeanSpring - 2f * LeanDamp * LeanSpring * _leanV) * sdt;
            _values[Accel] = Mathf.Clamp(_values[Accel] + _leanV * sdt, -20f, 20f);
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
            long t0 = Perf.On ? Perf.Now : 0;
            Lay();
            if (Perf.On) Perf.Add(t0, Perf.Anim);
        }

        private void Lay()
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

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The motor of a body's carriage, for any body (the user 2026-11-03: «walking belongs in the core, and it must differ»; 2026-11-05:
    // «one standard pose, a motor over it, as the ravens' has, so they differ»): everyone stands and walks the game's one way — on top
    // of it a body holds itself in its own way, put on its bones after the game's animation, on every game, nothing sent. The one place
    // for how a body holds itself standing and walking; who it is and what it wants now come from outside, the motor says how:
    //   its kind     a line of assets/gaits.txt (Style), in its own measure (Made: from the person's seed — the same after a restart)
    //   its wishes   how it is now (Mix): rows «mood_<name>» of the same table, each added by its strength 0…1 (NpcGait.Moods says
    //                which and how strong: cold, fright, grief, tired…), within limits (Mix.Clamp); eased in, never snapped
    //   standing     a quiet life of its own, only a person's (Style.Seed) and only near (IdleNear): its weight from foot to foot,
    //                its breath, its head drifting a little — every moment from the world's clock and its seed, so every game shows
    //                the same with nothing sent; no clips
    //   its top half  seated, or under a pose of its own (NpcStance; Pose «upper»): its back, its head, its breath and its wishes only — its legs,
    //                its hips are the game's or the clip's; under a pose its arms too (arms true: a clip holds them)
    // Who drives it: NpcGait (a person), StoryGait (the player in a scene: its kind only, no quiet life, no wishes).
    // The pace of the legs follows the speed (no feet sliding on the ground). Bones are turned the way the player's own
    // animations are written (BodyClips): round the body's axes (X forward/down, Z a roll), or pointed in the body (an arm
    // hanging back, a forearm across the back). Off on a seat, in a blow, lying, in a gesture, while a clip plays: eased
    // out and back, never snapped. A body too far to see: the game's own. Its cost: a few bone turns a frame, no physics; the
    // table read anew only when its file changed (a look at its stamp once a second).
    internal sealed class BodyGait
    {
        internal sealed class Style
        {
            public string Kind = Plain;
            public float Speed = 1f;  // × its walk
            public float Stoop;       // 0 straight … 1 bent (≈ 24° of its back); below 0 the chest out
            public float Head;        // degrees: + looks down, − chin up
            public float Swing = 1f;  // × its arms' swing
            public float Sway;        // degrees its hips roll a step
            public float Bob;         // metres its step springs
            public float Limp;        // 0 … 1: one leg spared
            public bool ArmsBack;     // its hands behind its back as it strolls
            public float Shoulders;   // degrees its shoulders come forward (below 0: drawn back)
            public float Shrug;       // degrees its shoulders are drawn up
            public float Out;         // degrees its arms hang away from its body (below 0: close to it)
            public float Toes;        // degrees its feet turn out (below 0: in)
            public float Narrow;      // degrees each thigh comes in toward its middle as it walks: its feet nearer one line (a woman's step)
            public float Twist;       // degrees its body turns with each step
            public float Nod;         // degrees its head nods with each step
            public float Rest;        // degrees its hips tilt as it stands with its weight on one foot (its quiet life)
            public float Tilt;        // degrees its head leans to one side (its own: which side, from its measure)
            public float Breath;      // degrees its chest rises with a breath
            public uint Seed;         // a person's lasting number (0: no quiet life — the player in a scene)
            internal float Spread, BackChance; // (the table's: how unlike two of a kind are; how many walk with hands behind)
        }

        // how it is now, over its kind: the wishes added up (Add), held within bounds (Clamp). Speed and swing are factors, the rest added
        internal sealed class Mix
        {
            public float Speed = 1f, Stoop, Head, Swing = 1f, Bob, Shrug, Out, Shoulders, Rest, Breath, Limp; // (Limp: a wish may spare a leg too — mood_hurt, a story person limping home)
            internal static readonly Mix None = new Mix();
            internal void Clear() { Speed = 1f; Swing = 1f; Stoop = Head = Bob = Shrug = Out = Shoulders = Rest = Breath = Limp = 0f; }
            // a wish of its table row by its strength (0 … 1)
            internal void Add(Style m, float k)
            {
                if (m == null || k <= 0f) return;
                k = Mathf.Clamp01(k);
                Speed *= Mathf.Lerp(1f, m.Speed, k); Swing *= Mathf.Lerp(1f, m.Swing, k);
                Stoop += m.Stoop * k; Head += m.Head * k; Bob += m.Bob * k; Shrug += m.Shrug * k; Out += m.Out * k; Shoulders += m.Shoulders * k; Rest += m.Rest * k; Breath += m.Breath * k; Limp += m.Limp * k;
            }
            internal void Clamp()
            {
                Speed = Mathf.Clamp(Speed, 0.6f, 1.3f); Swing = Mathf.Clamp(Swing, 0.2f, 1.6f); Stoop = Mathf.Clamp(Stoop, -0.6f, 1.2f); Head = Mathf.Clamp(Head, -25f, 28f);
                Bob = Mathf.Clamp(Bob, 0f, 0.03f); Shrug = Mathf.Clamp(Shrug, -5f, 28f); Out = Mathf.Clamp(Out, -16f, 20f); Shoulders = Mathf.Clamp(Shoulders, -12f, 18f);
                Rest = Mathf.Clamp(Rest, -8f, 4f); Breath = Mathf.Clamp(Breath, -1f, 2f); Limp = Mathf.Clamp01(Limp);
            }
        }

        // ---------------------------------------------------------------- the kinds: assets/gaits.txt
        // A kind is a line of the file by its name: the shared ones (NpcGait hands them out by age, body, ways) and the unique
        // ones (only who has it written: «gait: heid»). New ones: a line, no code. In the dll (assets/gaits.txt); on the author's machine
        // the mod's own assets/gaits.txt itself (StoryFiles.Source: one copy), elsewhere config/RavenWatch/gaits.txt, wins wholly and is read
        // anew when saved — a look at its stamp once a second, only while bodies walk. Ver: what was made from it is made anew.
        internal const string Plain = "plain";
        internal static int Ver;
        private static Dictionary<string, Style> _table;
        private static List<string> _names = new List<string>();
        private static DateTime _stamp;
        private static float _nextLook;
        private static string AuthorFile
        {
            get
            {
                if (StoryFiles.Source != null) { System.IO.DirectoryInfo up = System.IO.Directory.GetParent(StoryFiles.Source); if (up != null) return System.IO.Path.Combine(up.FullName, "gaits.txt"); } // (assets/story → assets)
                return System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "gaits.txt");
            }
        }

        internal static void Look()
        {
            if (_table != null && Time.time < _nextLook) return;
            _nextLook = Time.time + 1f;
            string f = AuthorFile;
            DateTime at = System.IO.File.Exists(f) ? System.IO.File.GetLastWriteTimeUtc(f) : DateTime.MinValue;
            if (_table != null && at == _stamp) return;
            _stamp = at;
            string text = null, why = null;
            if (at != DateTime.MinValue) try { text = System.IO.File.ReadAllText(f, System.Text.Encoding.UTF8); } catch (Exception e) { Npcs.Log("gaits.txt: " + e.Message); }
            List<string> names = null;
            Dictionary<string, Style> t = text != null ? Parse(text, out names, out why) : null;
            if (t == null)
            {
                if (text != null) Npcs.Log("gaits.txt: " + why + (_table != null ? " — what was read stays" : " — the mod's own"));
                if (_table != null && text != null) return; // (a slip while it is written)
                t = Parse(Own(), out names, out why);
                if (t == null) { Npcs.Log("gaits (the mod's own): " + why); t = new Dictionary<string, Style>(); names = new List<string>(); }
            }
            if (_table != null) Npcs.Log("gaits: read anew (" + names.Count + ")");
            if (!t.ContainsKey(Plain)) { t[Plain] = new Style(); names.Insert(0, Plain); }
            _table = t; _names = names; Ver++;
        }

        private static string Own()
        {
            try { using (System.IO.Stream st = typeof(BodyGait).Assembly.GetManifestResourceStream("RavenWatch.gaits.txt")) if (st != null) using (System.IO.StreamReader r = new System.IO.StreamReader(st, System.Text.Encoding.UTF8)) return r.ReadToEnd(); }
            catch (Exception e) { Npcs.Log("gaits (the mod's own): " + e.Message); }
            return "";
        }

        private static Dictionary<string, Style> Parse(string text, out List<string> names, out string why)
        {
            why = null; names = new List<string>();
            Dictionary<string, Style> t = new Dictionary<string, Style>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string name = w[0].ToLowerInvariant();
                if (t.ContainsKey(name)) { why = "«" + name + "» twice"; return null; }
                Style s = new Style { Kind = name };
                for (int i = 1; i < w.Length; i++)
                {
                    int eq = w[i].IndexOf('=');
                    float v;
                    if (eq <= 0 || !float.TryParse(w[i].Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) { why = name + ": «" + w[i] + "»"; return null; }
                    switch (w[i].Substring(0, eq))
                    {
                        case "speed": s.Speed = v; break; case "stoop": s.Stoop = v; break; case "head": s.Head = v; break;
                        case "swing": s.Swing = v; break; case "sway": s.Sway = v; break; case "bob": s.Bob = v; break;
                        case "limp": s.Limp = v; break; case "back": s.BackChance = v; break; case "shoulders": s.Shoulders = v; break;
                        case "shrug": s.Shrug = v; break; case "out": s.Out = v; break; case "toes": s.Toes = v; break; case "narrow": s.Narrow = v; break;
                        case "twist": s.Twist = v; break; case "nod": s.Nod = v; break; case "spread": s.Spread = v; break;
                        case "rest": s.Rest = v; break; case "tilt": s.Tilt = v; break; case "breath": s.Breath = v; break;
                        default: why = name + ": unknown «" + w[i] + "»"; return null;
                    }
                }
                t[name] = s;
                if (!name.StartsWith(MoodPrefix, StringComparison.Ordinal)) names.Add(name); // (a wish is not a kind anyone walks)
            }
            return t;
        }

        // a wish's row («mood_<name>»); null: none in the table — that wish moves nothing
        internal const string MoodPrefix = "mood_";
        internal static Style Mood(string name) { Look(); Style s; return _table.TryGetValue(MoodPrefix + name, out s) ? s : null; }

        // a kind of that name in the table (a template's «gait:», a scene's «player gait»)
        internal static bool Known(string name) { Look(); if (string.IsNullOrEmpty(name)) return false; string n = name.Trim().ToLowerInvariant(); return !n.StartsWith(MoodPrefix, StringComparison.Ordinal) && _table.ContainsKey(n); }
        internal static string Kinds { get { Look(); return string.Join("|", _names.ToArray()); } }
        internal static string[] Names { get { Look(); return _names.ToArray(); } }

        // a kind in its measure (j, j2: 0…1, a little more or less of it by its spread; plain: its own stoop on top; j3: which side its head
        // leans and how much; seed: a person's lasting number — its quiet life standing, 0 none)
        internal static Style Made(string kind, float j, float j2, float plainStoop, float j3 = 0.5f, uint seed = 0u)
        {
            Look();
            Style b;
            if (string.IsNullOrEmpty(kind) || !_table.TryGetValue(kind.ToLowerInvariant(), out b)) b = _table[Plain]; // (a kind no longer in the table: plain)
            bool plain = b.Kind == Plain;
            float m = 1f + (j - 0.5f) * 2f * b.Spread, m2 = 1f + (j2 - 0.5f) * 2f * b.Spread; // (more or less of it)
            return new Style
            {
                Kind = b.Kind, Speed = 1f + (b.Speed - 1f) * m, Stoop = b.Stoop * m2 + (plain ? plainStoop : 0f), Head = b.Head * m + (plain ? (j - 0.5f) * 6f : 0f),
                Swing = 1f + (b.Swing - 1f) * m2, Sway = b.Sway * m, Bob = b.Bob * m2, Limp = Mathf.Clamp01(b.Limp * m), ArmsBack = j2 < b.BackChance,
                Shoulders = b.Shoulders * m, Shrug = b.Shrug * m2, Out = b.Out * m, Toes = b.Toes * m2, Narrow = b.Narrow * m, Twist = b.Twist * m, Nod = b.Nod * m2,
                Rest = b.Rest * m, Breath = b.Breath * m2, Tilt = b.Tilt * (j3 < 0.5f ? -1f : 1f) * (0.5f + Mathf.Abs(j3 * 2f - 1f) * 0.5f), Seed = seed, // (its side, at least half of its kind's: a draw near the middle left Sigrid's head all but straight)
            };
        }

        // ---------------------------------------------------------------- its body
        internal static float OnRate = 4f; // its carriage eased in or out in a quarter of a second
        private readonly MonoBehaviour _me;
        private readonly Humanoid _h;
        private Animator _anim;
        private bool _looked;
        private Transform _hips, _spine1, _spine2, _neck, _head, _lArm, _lFore, _lHand, _rArm, _rFore, _rHand, _lSh, _rSh, _lUp, _rUp;
        private Quaternion _lAvg = Quaternion.identity, _rAvg = Quaternion.identity;
        private bool _avg, _wrote, _wasSet;
        private Quaternion _mine; // (its back as we left it: the same next frame — the game did not pose it anew)
        private Vector3 _was; private float _speed; // (its ground speed as seen here, eased)
        private float _animOwn = 1f, _back, _strolling, _on; // (the pace it gave the animation; hands behind it 0…1; strolling 0…1; on 0…1)

        internal BodyGait(MonoBehaviour me, Character c) { _me = me; _h = c as Humanoid; }

        // the body's own skeleton (the bones its mesh is skinned to), not a thing of the same name it wears or holds
        private void Bones()
        {
            _looked = true;
            Transform v = _anim.transform;
            _hips = B(v, "Hips"); _spine1 = B(v, "Spine1"); _spine2 = B(v, "Spine2"); _neck = B(v, "Neck"); _head = B(v, "Head");
            _lArm = B(v, "LeftArm"); _lFore = B(v, "LeftForeArm"); _lHand = B(v, "LeftHand");
            _rArm = B(v, "RightArm"); _rFore = B(v, "RightForeArm"); _rHand = B(v, "RightHand");
            _lSh = B(v, "LeftShoulder"); _rSh = B(v, "RightShoulder"); _lUp = B(v, "LeftUpLeg"); _rUp = B(v, "RightUpLeg");
            _wrote = false;
        }
        private Transform B(Transform v, string name) { return BodyClips.SkeletonBone(_me, v, name); }

        // its bones lost: destroyed or no longer under its animator (a body rebuilt by new clothes)
        internal static bool Stale(Transform hips, Animator anim) { return (object)hips != null && (hips == null || !hips.IsChildOf(anim.transform)); }

        // the body found and near enough to see; else the game's own
        internal bool Ready()
        {
            if (_anim == null) _anim = _me.GetComponentInChildren<Animator>();
            if (_anim == null) return false;
            if (!_looked || Stale(_hips, _anim)) Bones();
            Camera cam = Camera.main;
            float far = cam != null ? (cam.transform.position - _me.transform.position).sqrMagnitude : 0f;
            if (far > 70f * 70f) { Rest(); return false; }
            _near = far < IdleNear * IdleNear;
            return true;
        }

        // its quiet life standing (a person's only): within IdleNear of the eye; its weight on one foot for ShiftMin…ShiftMax s, the shift
        // over ShiftFor s; its head drifting by at most Drift degrees
        internal static float IdleNear = 40f, ShiftMin = 6f, ShiftMax = 14f, ShiftFor = 1.6f, Drift = 3f;
        private bool _near;
        private float _idle; // (0…1: its quiet life eased in as it stops, out as it walks)

        // one frame: its style, how it is now (legs: the pace of its walk, 0 — its legs left to the game, as a player's whose
        // speed is the game's own; now: its wishes over its style), off: it gives way now
        // how far down it put the head this frame, in degrees (its stoop and its head, as the head ends up): one that talks to someone lifts it (NpcSpeech)
        internal float HeadDown;

        internal void Pose(Style s, float legs, Mix now, bool off, bool upper = false, bool arms = false)
        {
            HeadDown = 0f;
            if (now == null) now = Mix.None;
            float stoopNow = now.Stoop, headNow = now.Head, swingNow = now.Swing, bobNow = now.Bob, shrugNow = now.Shrug, outNow = now.Out;
            float limp = Mathf.Clamp01(s.Limp + now.Limp); // (its own spared leg, and a wish's on top)
            float dt = Time.deltaTime;
            _on = Mathf.MoveTowards(_on, off ? 0f : 1f, dt * OnRate); // (off a seat, into a blow, a gesture: eased out and back, never snapped)
            if (_on <= 0f) { Rest(); return; }
            // the game did not pose its bones this frame (off screen, culled): they still hold ours — nothing more on top
            if (_spine1 != null && _wrote && _spine1.localRotation == _mine) return;
            float v = Speed(dt); // (from how it moves on this game: on another's its body is only placed, its velocity none)
            bool stroll = !off && !upper && v > 0.2f && v < 2.6f; // (a walk: a run, a jog keeps the game's own)
            _strolling = Mathf.MoveTowards(_strolling, stroll ? 1f : 0f, dt * 3f);
            Quaternion body = _me.transform.rotation;
            float phase = Phase();
            float wave = Mathf.Sin(phase * 2f * Mathf.PI);

            // the pace of its legs, so the feet keep to the ground; a limp: the bad leg's half of the step slower
            if (legs > 0f)
            {
                float pace = Mathf.Clamp(1f + (legs - 1f) * 0.8f, 0.7f, 1.3f);
                if (limp > 0f) pace *= 1f - limp * 0.4f * Mathf.Max(0f, wave) * _strolling;
                float target = Mathf.Lerp(1f, pace, _strolling);
                if ((Mathf.Abs(target - 1f) > 0.002f || Mathf.Abs(_animOwn - 1f) > 0.002f) && Mathf.Abs(_anim.speed - target) > 0.002f) _anim.speed = target; // (only a pace it gave or gives: the game's own left alone)
                _animOwn = target;
            }

            // its back, its head (standing too)
            float stoop = (s.Stoop + stoopNow) * _on;
            Turn(_spine1, body, stoop * 14f, 0f, 0f);
            Turn(_spine2, body, stoop * 10f, 0f, 0f);
            Turn(_neck, body, -stoop * 9f, 0f, 0f); // (it looks ahead, not at its feet)
            Turn(_head, body, (s.Head + headNow + s.Nod * Mathf.Cos(phase * 4f * Mathf.PI) * _strolling) * _on, 0f, 0f); // (a nod: once a step)
            HeadDown = stoop * 15f + (s.Head + headNow) * _on; // (14 + 10 − 9 of its back and neck, then its head)
            // its shoulders: forward and up (Y: forward for the left one, Z: up) — round them, draw them back, hunch
            // its quiet life standing: eased in as it stops (a person's, near only)
            _idle = Mathf.MoveTowards(_idle, s.Seed != 0u && _near ? 1f - _strolling : 0f, dt * 1.5f);
            float idle = _idle * _on, breath = 0f;
            if (idle > 0.001f) breath = Mathf.Max(0f, s.Breath + now.Breath) * Breathe(s.Seed) * idle;
            float shrug = s.Shrug + shrugNow + breath * 0.6f; // (how it is now: cold, afraid — NpcGait; a breath lifts them a little)
            float fore = s.Shoulders + now.Shoulders;
            if (!arms && (Mathf.Abs(fore) > 0.01f || Mathf.Abs(shrug) > 0.01f)) { Turn(_lSh, body, 0f, fore * _on, -shrug * _on); Turn(_rSh, body, 0f, -fore * _on, shrug * _on); }
            // …its arms kept hanging as they were: the collarbone carries the whole arm, and shoulders drawn up spread its arms like wings
            // (Knud, 2026-11-05) — the arm turned back by as much, so only the shoulder rises
            if (!arms && Mathf.Abs(shrug) > 0.01f) { Turn(_lArm, body, 0f, 0f, shrug * _on); Turn(_rArm, body, 0f, 0f, -shrug * _on); }
            // its feet turned out (each thigh round the up axis): only as it walks, the game's stance standing
            if (Mathf.Abs(s.Toes) > 0.01f && _strolling > 0.01f) { Turn(_lUp, body, 0f, -s.Toes * _strolling * _on, 0f); Turn(_rUp, body, 0f, s.Toes * _strolling * _on, 0f); }

            // its feet nearer one line (each thigh round the forward axis, in: the left one +, as an arm's «out» is − on the left)
            if (Mathf.Abs(s.Narrow) > 0.01f && _strolling > 0.01f) { float n = s.Narrow * _strolling * _on; Turn(_lUp, body, 0f, 0f, n); Turn(_rUp, body, 0f, 0f, -n); }

            // its step: hips rolling, a spring, a spared leg (only as it walks)
            if (_strolling > 0.01f)
            {
                float roll = (s.Sway * wave + limp * 6f * Mathf.Max(0f, wave)) * _strolling;
                if (Mathf.Abs(roll) > 0.01f) { Turn(_hips, body, 0f, 0f, roll); Turn(_spine1, body, 0f, 0f, -roll * 0.6f); }
                float twist = s.Twist * wave * _strolling; // (its chest turns against its legs: the shoulder of the arm swung forward)
                if (Mathf.Abs(twist) > 0.01f) Turn(_spine2, body, 0f, twist, 0f);
                float bob = (s.Bob + bobNow) * (0.5f + 0.5f * Mathf.Cos(phase * 4f * Mathf.PI)) - limp * 0.03f * Mathf.Max(0f, wave);
                if (_hips != null && Mathf.Abs(bob) > 0.0005f) _hips.position += Vector3.up * bob * _strolling;
            }

            // standing: its weight on one foot (hips tilt, its legs kept straight under it, its chest back level), its breath, its head
            if (idle > 0.001f)
            {
                float rest = upper ? 0f : Mathf.Max(0f, s.Rest + now.Rest) * Weight(s) * idle; // (seated or in a pose: its hips stay)
                if (Mathf.Abs(rest) > 0.01f) { Turn(_hips, body, 0f, 0f, rest); Turn(_lUp, body, 0f, 0f, -rest); Turn(_rUp, body, 0f, 0f, -rest); Turn(_spine1, body, 0f, 0f, -rest * 0.6f); Turn(_spine2, body, 0f, 0f, -rest * 0.4f); }
                if (breath > 0.001f) Turn(_spine2, body, -breath, 0f, 0f);
                Turn(_head, body, 0f, Drift * Wander(s.Seed) * idle, 0f);
            }
            if (Mathf.Abs(s.Tilt) > 0.01f) Turn(_head, body, 0f, 0f, s.Tilt * _on); // (its own lean of the head, standing and walking)

            // its arms: hands behind its back (an elder strolling, its hands free), else its swing wider or smaller
            bool free = _h == null || _h.m_unarmedWeapon == null || _h.GetCurrentWeapon() == _h.m_unarmedWeapon.m_itemData; // (nothing in its hands: no weapon, no tool)
            _back = Mathf.MoveTowards(_back, !upper && s.ArmsBack && free && _strolling > 0.5f ? 1f : 0f, dt * 1.5f);
            float back = _back * _on;
            if (back > 0.01f)
            {
                Aim(_lArm, _lFore, body, -62f, -150f, back); Aim(_lFore, _lHand, body, -12f, 105f, back);
                Aim(_rArm, _rFore, body, -62f, 150f, back); Aim(_rFore, _rHand, body, -12f, -105f, back);
                _avg = false;
            }
            else
            {
                float swing = s.Swing * swingNow;
                if (_strolling > 0.01f && Mathf.Abs(swing - 1f) > 0.02f) { SwingArm(_lArm, ref _lAvg, swing, dt); SwingArm(_rArm, ref _rAvg, swing, dt); _avg = true; }
                else _avg = false;
            }
            // its arms away from its body (round the forward axis: the left one out to the left)
            float outs = s.Out + outNow;
            if (!upper && Mathf.Abs(outs) > 0.01f && back < 0.99f) { float o = outs * _on * (1f - back); Turn(_lArm, body, 0f, 0f, -o); Turn(_rArm, body, 0f, 0f, o); }
            if (_spine1 != null) { _mine = _spine1.localRotation; _wrote = true; }
        }

        // its ground speed from its moves here (every game sees it walk the same; eased, a net step is not a sprint)
        private float Speed(float dt)
        {
            Vector3 now = _me.transform.position;
            if (!_wasSet || dt <= 0f) { _was = now; _wasSet = true; return _speed; }
            float step = new Vector2(now.x - _was.x, now.z - _was.z).magnitude / dt;
            _was = now;
            if (step > 20f) return _speed; // (put somewhere: not a walk)
            _speed = Mathf.Lerp(_speed, step, Mathf.Clamp01(dt * 6f));
            return _speed;
        }

        // its motor now, in words (raven npc gait <who>)
        internal string Now(Style s, Mix now)
        {
            float w = s.Seed != 0u ? Weight(s) : 0f, rest = Mathf.Max(0f, s.Rest + (now != null ? now.Rest : 0f)) * w * _idle * _on;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "увімкнено {0:0.00}, іде {1:0.00}, тихе життя {2:0.00} ({3}), вага {4} ({5:0.00}), таз {6:0.0}°",
                _on, _strolling, _idle, s.Seed == 0u ? "нема зерна" : _near ? "близько" : "далі " + IdleNear.ToString("0") + " м", w < -0.3f ? "ліва" : w > 0.3f ? "права" : "обидві", w, rest);
        }

        // where its joints are now, in its own frame (x right, y up, z ahead): to reckon a clip's hand targets from, seated above all (raven npc gait <who>); read outside the pose pass: the game's pose, without our clips
        internal string Joints()
        {
            if (_anim == null) return "-";
            Transform root = _me.transform, v = _anim.transform;
            System.Text.StringBuilder t = new System.Text.StringBuilder();
            foreach (string j in new[] { "Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftArm", "RightArm", "LeftHand", "RightHand" })
            {
                Transform x = B(v, j);
                if (x == null) continue;
                Vector3 p = root.InverseTransformPoint(x.position);
                t.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, " {0} {1:0.00} {2:0.00} {3:0.00};", j, p.x, p.y, p.z));
            }
            return t.ToString();
        }

        // the black box's look at it (NpcPoseWatch, on a jump of the pose)
        internal string Trace() { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "gait on {0:0.00} stroll {1:0.00} back {2:0.00} swing-avg {3} anim {4:0.00} idle {5:0.00}", _on, _strolling, _back, _avg, _anim != null ? _anim.speed : 0f, _idle); }

        // the game's own again (a seat, a blow, too far): its legs' pace back
        internal void Rest()
        {
            _avg = false; _back = 0f; _strolling = 0f; _wrote = false; _on = 0f; _idle = 0f;
            if (_anim != null && Mathf.Abs(_animOwn - 1f) > 0.001f) { _anim.speed = 1f; _animOwn = 1f; }
        }

        // ---------------------------------------------------------------- its quiet life: every moment from the world's clock and its seed
        // (every game the same, nothing sent; a slow person — its kind's speed — shifts more slowly)
        private static float H(uint seed, long i)
        {
            uint h = seed ^ (uint)i * 2654435761u ^ (uint)(i >> 32) * 40503u;
            h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return (h & 0xFFFFFFu) / 16777215f;
        }
        private static float Side(uint seed, long i) { float r = H(seed, i); return r < 0.4f ? -1f : r < 0.8f ? 1f : 0f; } // (a foot, the other, or both)

        // which foot its weight is on now: −1 … 1, eased from the last over ShiftFor s
        private static float Weight(Style s)
        {
            double t = NpcRescue.Now + H(s.Seed, 7) * 100f;
            double period = (ShiftMin + (ShiftMax - ShiftMin) * H(s.Seed, 1)) / Mathf.Clamp(s.Speed, 0.6f, 1.4f);
            long i = (long)Math.Floor(t / period);
            float into = (float)(t - i * period), k = Mathf.Clamp01(into / ShiftFor);
            return Mathf.Lerp(Side(s.Seed, i - 1), Side(s.Seed, i), k * k * (3f - 2f * k));
        }

        // its breath: −1 … 1, a breath in some four seconds of its own
        private static float Breathe(uint seed) { return (float)Math.Sin(NpcRescue.Now * (2.0 * Math.PI / (3.4 + 1.2 * H(seed, 2))) + H(seed, 3) * 6.283); }

        // its head drifting: −1 … 1, two slow waves of its own
        private static float Wander(uint seed) { double t = NpcRescue.Now; return (float)(0.6 * Math.Sin(t * (0.17 + 0.1 * H(seed, 4)) + H(seed, 5) * 6.283) + 0.4 * Math.Sin(t * (0.05 + 0.04 * H(seed, 6)) + H(seed, 8) * 6.283)); }

        // where it is in its step (0…1 a cycle of two steps): the game's walk's own time
        private float Phase()
        {
            AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
            float t = st.normalizedTime;
            return t - Mathf.Floor(t);
        }

        // a bone turned round the body's axes (X forward/down, Y right, Z a roll), on top of the game's pose
        internal static void Turn(Transform bone, Quaternion body, float x, float y, float z) // (NpcSpeech turns the same way)
        {
            if (bone == null || (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(z) < 0.01f)) return;
            bone.rotation = body * Quaternion.Euler(x, y, z) * Quaternion.Inverse(body) * bone.rotation;
        }

        // a bone pointed in the body (up: −90 down … 90 up; aside: 0 ahead, 90 right, −90 left, 180 back), by a share
        private static void Aim(Transform bone, Transform next, Quaternion body, float up, float aside, float share)
        {
            if (bone == null || next == null) return;
            Vector3 now = next.position - bone.position;
            if (now.sqrMagnitude < 1e-6f) return;
            Vector3 want = body * BodyClips.Pointing(up, aside);
            // in two turns, each short and one way only: from where the game has it to hanging straight down, then to
            // where we want it — one turn of nearly half a circle (the arm swung forward, wanted behind the back) has no
            // one way to go and may flip from frame to frame
            Vector3 down = body * Vector3.down;
            Quaternion to = Quaternion.FromToRotation(down, want) * Quaternion.FromToRotation(now, down) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, to, share);
        }

        // its arm's swing wider or smaller: round where it swings about (its slow average), by a share
        private void SwingArm(Transform arm, ref Quaternion avg, float swing, float dt)
        {
            if (arm == null) return;
            Quaternion now = arm.localRotation;
            avg = _avg ? Quaternion.Slerp(avg, now, Mathf.Clamp01(dt * 1.6f)) : now;
            arm.localRotation = Quaternion.SlerpUnclamped(avg, now, Mathf.Lerp(1f, swing, _strolling));
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's gait (npc-plan.md, «хода»): who it is and how it is now; the work on its bones is BodyGait's, for any body:
    //   a kind of gait   a line of assets/gaits.txt (BodyGait): its template's («gait: heid» — a shared kind or its own),
    //                    else a shared one from its story (a lame leg), its age, its nature, its body, its way of fighting, its ways;
    //                    else a steady draw from its id
    //   its own measure  a little more or less of each, which side its head leans, its quiet life standing (its seed): no two alike
    //   how it is now    its wishes, once a second, from what every game knows of it (its ZDO), so every game shows the same: each
    //                    source (Moods: a module's line, NpcModules) names a wish and its strength 0…1, the motor adds the rows
    //                    «mood_<name>» of gaits.txt (BodyGait.Mix). Its own sources here: tired, hungry, low, glad (its toil, its
    //                    hunger) and cold (NpcPlaces.Cold out of doors — not under a roof nor in a fire's warmth; that looked for every
    //                    RoofEvery s, within RoofNear of you). The feelings: NpcFeel's own source (fright, joy, awe, grief)
    internal sealed class NpcGait : MonoBehaviour
    {
        // ---------------------------------------------------------------- its style (once per person, kept on it)
        internal static BodyGait.Style Of(NpcActor a)
        {
            if (a == null || a.Def == null || a.Zdo == null) return Plain;
            NpcPersona w = NpcPersona.Of(a);
            BodyGait.Look();
            if (a.GaitKept != null && a.GaitFor == w && a.GaitVer == BodyGait.Ver) return a.GaitKept;
            a.GaitKept = Make(a, w); a.GaitFor = w; a.GaitVer = BodyGait.Ver;
            return a.GaitKept;
        }

        private static readonly BodyGait.Style Plain = new BodyGait.Style();

        private static BodyGait.Style Make(NpcActor a, NpcPersona w)
        {
            uint seed = NpcKit.Seed(a.Zdo) * 2246822519u ^ 0x5bd1e995u; if (seed == 0u) seed = 1u; // (its lasting number: the same gait after a restart)
            string k = Pick(a, w, ref seed);
            float j = Roll(ref seed), j2 = Roll(ref seed), j3 = Roll(ref seed); // (its own measure of its kind; j3 after the two: the gaits already given stay)
            uint own = NpcKit.Seed(a.Zdo); if (own == 0u) own = 1u;
            return BodyGait.Made(k, j, j2, w.Stoop * 0.6f, j3, own);
        }

        // its kind: what is written of it first, then what it is (its story, its age, its body, its fight), then its ways
        private static string Pick(NpcActor a, NpcPersona w, ref uint seed)
        {
            if (BodyGait.Known(a.GaitForced)) return a.GaitForced; // (a test: raven npc gait, raven stage gait)
            if (BodyGait.Known(a.Def.Gait)) return a.Def.Gait; // (written of it: a shared kind or its own)
            if (NpcBackstory.Fact(a, "кульгає")) return "limp";
            NpcSettlers.Look look = NpcSettlers.Of(a);
            float r = Roll(ref seed);
            if (look != null && look.Age == 2) return r < 0.7f ? "elder" : r < 0.85f ? "meek" : "plain";
            if (w.Nature != null && w.Nature.Gait.Length > 0 && r < 0.65f) return w.Nature.Gait; // (its nature shows in its step: NpcNature)
            if (w.Build >= 0.45f && r < 0.6f) return "waddle";                      // (a heavy one: rolling from foot to foot)
            if (w.Height >= 1.09f && w.Build <= -0.25f && r < 0.5f) return "spry"; // (a tall lean one: a long springy step)
            if (look != null && look.Age == 0 && r < 0.55f) return "spry";
            if (a.Def.Fight == "defend" || a.Def.Fight == "hunt") return r < 0.7f ? "soldier" : "plain";
            if (w.Bravery < 0.35f && w.Chatty < 0.45f && r < 0.6f) return "meek";
            r = Roll(ref seed);
            return r < 0.4f ? "plain" : r < 0.6f ? "waddle" : r < 0.75f ? "soldier" : r < 0.9f ? "meek" : "spry";
        }

        // a test (console «raven npc gait <who> <kind>», the stage): its kind on this game only, till the world is left; the
        // table read anew keeps it (a kind tuned as it is watched)
        //   raven npc gait <who>          its motor now: its kind and measure, its quiet life, its wishes and their strength (the log too)
        //   raven npc gait <who> show     each wish in turn at its full strength for ShowFor s, its name said over its head (this game only)
        internal static string Force(NpcActor a, string word)
        {
            if (word == "show") { Shows[a] = Time.time; Shown.Remove(a); return a.Name + ": показує бажання по черзі, по " + ShowFor.ToString("0") + " с: " + string.Join(", ", ShowWishes); }
            if (!BodyGait.Known(word)) { NpcGait g = a.GetComponent<NpcGait>(); string t = (g != null ? g.Describe(a) : a.Name + ": " + Of(a).Kind) + "\nraven npc gait <хто> show | " + BodyGait.Kinds; Npcs.Log(t); return t; }
            a.GaitForced = word.Trim().ToLowerInvariant(); a.GaitKept = null;
            return a.Name + ": " + Of(a).Kind;
        }

        private static float Roll(ref uint seed) { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed % 10000u) / 10000f; }

        // its gait in a word (its card)
        internal static string Word(NpcActor a)
        {
            switch (Of(a).Kind)
            {
                case "elder": return Lang.T("ходить по-старечому, руки за спиною");
                case "soldier": return Lang.T("ступає, як вояк");
                case "spry": return Lang.T("ходить пружно");
                case "waddle": return Lang.T("ходить перевальцем");
                case "limp": return Lang.T("накульгує");
                case "meek": return Lang.T("ходить тихо, похнюпившись");
            }
            return "";
        }

        // ---------------------------------------------------------------- how it is now: its wishes (eased, so nothing jumps)
        // a source of wishes (a module's line in NpcModules): for this person, once a second, on every game, from what every game knows of
        // it (its ZDO) — a wish's name (a row «mood_<name>» of gaits.txt) and its strength 0…1 (Wish: the stronger of two counts)
        internal static readonly List<Action<NpcActor, Dictionary<string, float>>> Moods = new List<Action<NpcActor, Dictionary<string, float>>>();
        internal static void Wish(Dictionary<string, float> into, string name, float k) { if (k <= 0f) return; float had; into[name] = into.TryGetValue(name, out had) ? Mathf.Max(had, k) : Mathf.Min(1f, k); }
        private static readonly HashSet<int> Failed = new HashSet<int>(); // session: a source that threw, told once
        private readonly Dictionary<string, float> _wishes = new Dictionary<string, float>(); // (the same one each second: nothing new made)
        private readonly BodyGait.Mix _want = new BodyGait.Mix(), _now = new BodyGait.Mix();
        private float _nextState;

        internal static float ShowFor = 6f; // s a wish of the show
        private static readonly string[] ShowWishes = { "cold", "fright", "grief", "joy", "awe", "tired" };
        private static readonly Dictionary<NpcActor, float> Shows = new Dictionary<NpcActor, float>(); // session: a show going on (when it began)
        private static readonly Dictionary<NpcActor, int> Shown = new Dictionary<NpcActor, int>();    // session: the wish it has said

        // how strong that wish is now (NpcStance: how it stands)
        internal float Wished(string name) { float k; return _wishes.TryGetValue(name, out k) ? k : 0f; }

        // its motor now, in words (raven npc gait <who>)
        internal string Describe(NpcActor a)
        {
            BodyGait.Style s = Of(a);
            System.Text.StringBuilder t = new System.Text.StringBuilder(a.Name).Append(": хода ").Append(s.Kind);
            t.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, " · манера: вага на ногу {0:0.0}°, голова набік {1:0.0}°, дихання {2:0.0}°, зерно {3}", s.Rest, s.Tilt, s.Breath, s.Seed));
            t.Append("\nмотор: ").Append(_b != null ? _b.Now(s, _now) : "-");
            t.Append("\nсуглоби:").Append(_b != null ? _b.Joints() : "-").Append(_body != null ? " · сидить: " + _body.SeatsNow + " (" + _body.AttachedSeen + ") · поза: " + _body.InPose : "");
            t.Append("\nбажання:");
            if (_wishes.Count == 0) t.Append(" немає");
            foreach (KeyValuePair<string, float> w in _wishes) t.Append(' ').Append(w.Key).Append(' ').Append(w.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(BodyGait.Mood(w.Key) == null ? " (нема рядка mood_" + w.Key + ")" : "");
            t.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "\nзараз: плечі вгору {0:0.0}°, руки {1:0.0}°, спина {2:0.00}, голова {3:0.0}°, темп ×{4:0.00}", _now.Shrug, _now.Out, _now.Stoop, _now.Head, _now.Speed));
            return t.ToString();
        }

        internal static float SlowestPace = 0.7f; // its walk never slower than this × its own: its legs' pace (BodyGait) goes no lower, else its feet slide (Torvald floated, 2026-11-05)
        internal static float RoofEvery = 5f, RoofNear = 40f; // s between its looks for warmth in the cold · m from you: further off its hunch is not seen, its last answer stays
        private float _nextRoof; private bool _warm;

        private void State(NpcActor a, bool off)
        {
            if (Time.time < _nextState) return;
            _nextState = Time.time + 1f;
            _wishes.Clear();
            Own(a, off);
            for (int i = 0; i < Moods.Count; i++)
            {
                try { Moods[i](a, _wishes); }
                catch (Exception e) { if (Failed.Add(i)) Npcs.Log("gait wishes: source " + i + " failed — " + e); }
            }
            float since;
            if (Shows.TryGetValue(a, out since)) // (the show: one wish at a time over all the others, its name said once)
            {
                int i = (int)((Time.time - since) / ShowFor);
                if (i >= ShowWishes.Length) { Shows.Remove(a); Shown.Remove(a); }
                else
                {
                    _wishes.Clear(); Wish(_wishes, ShowWishes[i], 1f);
                    int was; if (!Shown.TryGetValue(a, out was) || was != i) { Shown[a] = i; a.Say(ShowWishes[i]); }
                }
            }
            _want.Clear();
            foreach (KeyValuePair<string, float> w in _wishes) _want.Add(BodyGait.Mood(w.Key), w.Value);
            _want.Clamp();
            if (a.Owned) a.Pace(Mathf.Max(SlowestPace, Of(a).Speed * _want.Speed)); // (its walk: only where it is run — its speed goes to the others with it)
        }

        // its own sources: its toil and hunger (its ZDO), the cold out of doors
        private void Own(NpcActor a, bool off)
        {
            Wish(_wishes, "tired", Mathf.Clamp01((NpcToil.Tired(a) / 100f - 0.4f) / 0.6f));
            if (NpcResident.Settled(a)) // (a person of the story carries itself as written)
            {
                float mood = NpcToil.Mood(a) / 100f;
                Wish(_wishes, "hungry", NpcHunger.Full(a) < 35f ? 1f : 0f);
                Wish(_wishes, "low", Mathf.Clamp01((0.3f - mood) / 0.15f));
                Wish(_wishes, "glad", Mathf.Clamp01((mood - 0.75f) / 0.1f));
            }
            // cold: most nights are (the game's «cold at night»), so warmth — a fire's, a roof — is looked for rarely: every RoofEvery s,
            // only when the motor is on (in bed, in a gesture its body is not the gait's) and within RoofNear of you
            float cold = NpcPlaces.Cold();
            if (cold > 0f)
            {
                Player me = Player.m_localPlayer;
                if (!off && Time.time >= _nextRoof && me != null && (me.transform.position - a.transform.position).sqrMagnitude < RoofNear * RoofNear)
                {
                    _nextRoof = Time.time + RoofEvery;
                    Vector3 at = a.transform.position;
                    _warm = EffectArea.IsPointInsideArea(at, EffectArea.Type.Heat, 0.5f) != null || NpcToil.Roof(at + Vector3.up * 1.6f); // heavy: the game's warmth areas, then its cover rays — every RoofEvery s a person within RoofNear of you, only when it is cold or wet
                }
                if (_warm) cold = 0f;
            }
            Wish(_wishes, "cold", cold);
        }

        // what it wants, reached at its own pace: a fright's shoulders quickly, its back slowly
        private void Ease(float dt)
        {
            float k = dt * 0.5f;
            _now.Speed = Mathf.MoveTowards(_now.Speed, _want.Speed, k * 0.5f);
            _now.Stoop = Mathf.MoveTowards(_now.Stoop, _want.Stoop, k * 0.6f);
            _now.Head = Mathf.MoveTowards(_now.Head, _want.Head, k * 14f);
            _now.Swing = Mathf.MoveTowards(_now.Swing, _want.Swing, k);
            _now.Bob = Mathf.MoveTowards(_now.Bob, _want.Bob, k * 0.02f);
            _now.Shrug = Mathf.MoveTowards(_now.Shrug, _want.Shrug, k * 24f);
            _now.Out = Mathf.MoveTowards(_now.Out, _want.Out, k * 20f);
            _now.Shoulders = Mathf.MoveTowards(_now.Shoulders, _want.Shoulders, k * 14f);
            _now.Rest = Mathf.MoveTowards(_now.Rest, _want.Rest, k * 4f);
            _now.Breath = Mathf.MoveTowards(_now.Breath, _want.Breath, k * 2f);
        }

        // ---------------------------------------------------------------- its body
        private NpcActor _a;
        private Character _c;
        private NpcFall _fall;
        private NpcBody _body;
        private BodyGait _b;

        private void Awake() { _a = GetComponent<NpcActor>(); _c = GetComponent<Character>(); _body = GetComponent<NpcBody>(); if (_c != null) _b = new BodyGait(this, _c); }

        // a clip of its own on it now moves its legs (looked up in its clips: BodyAnimator.LegsHeld)
        private bool LegsHeld() { BodyAnimator a = BodyAnimator.Of(_c); return a != null && a.LegsHeld; }

        // its bones lost (BodyGait): for the other layers of its pose
        internal static bool Stale(Transform hips, Animator anim) { return BodyGait.Stale(hips, anim); }

        // a layer of the pose (NpcPose: after the game's animation, before a fall and a meal)
        internal void Pose()
        {
            if (_a == null || _c == null || _b == null || _a.Def == null || _a.Zdo == null || !_b.Ready()) return;
            BodyGait.Style s = Of(_a);
            bool seat = _body != null && _body.SeatsNow, pose = _body != null && _body.InPose; // (seated, or a pose of its own: its top half only)
            bool off = (_body != null && ((_body.AttachedSeen.Length > 0 && !seat) || (_body.ClipBusy && !pose))) || _c.InAttack() || NpcVitals.IsDown(_a) || ((_fall != null || (_fall = GetComponent<NpcFall>()) != null) && _fall.Busy) || (_a.Zdo.GetString(ZDOVars.s_emote, "").Length > 0 && !seat) || _c.IsDead();
            State(_a, off);
            Ease(Time.deltaTime);
            // (on a bench or a chair, and no clip of its own moving its legs — leg over leg, a slap on a knee: its knees in, by its gait's «sitknees»)
            bool bench = seat && _body.AttachedSeen == "attach_chair" && !LegsHeld(); // (any clip that moves its legs, a pose of its own or a gesture asked of it)
            _b.Pose(s, NpcPersona.Of(_a).Walk * Mathf.Max(SlowestPace, s.Speed * _now.Speed), _now, off, seat || pose, pose, bench ? 1f : 0f, bench && !_body.ClipBusy); // (its hands on its knees go in with them, unless a clip of its own has its arms)
        }

        // the black box's look at it (NpcPoseWatch, on a jump of the pose)
        internal string Trace() { return _b != null ? _b.Trace() : ""; }

        // how far down its carriage put its head this frame (degrees): NpcSpeech lifts it to whom it talks to
        internal float HeadDown { get { return _b != null ? _b.HeadDown : 0f; } }
    }
}
#endif

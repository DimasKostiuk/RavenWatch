#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person speaking and listening, with no voice (the user 2026-11-09: «make talk look like talk without the voice package»;
    // the body has no jaw and no face bones, only its head, neck and chest). One place for every line a person says:
    //   over its head   NpcActor.Say → RPC_Say on every game (small talk, a scene, a greeting, one who butts in)
    //   in your talk    the talk window shows its lines one by one (RavenBookTalk → NpcTalk.LineShown → Window): one small message
    //                   to every game (NpcActor.Speak), no bubble, so those near see it speak to you
    // The line's words become its plan (Make): syllables at the person's own pace, its stressed words, a pause at each mark
    // (, . … ? !), a breath before each sentence. The plan moves the bodies:
    //   the speaker     its head and neck bob a little with its syllables (what a mouth would show from afar), nod on a stressed
    //                   word, dip at a full stop, tilt at a question, lean in at «!», look aside at «…», its chest rises with a breath;
    //                   its head turns to the one it speaks to (named; else who spoke to it last; else the nearest person; else a player near)
    //   the listeners   the nearest one or two people turn their heads to it while it speaks and ListenFor after; they nod at its full
    //                   stops, tilt at its question, draw back a little at «!»; the one spoken to «thinks» after the line before it
    //                   answers — a glance aside (Think: the same time on every game, and the small talk waits it out: NpcChatter)
    //   sounds          a breath before a sentence, a «hm» in the thinking: only names written in assets/story/sounds (speech_breath,
    //                   speech_hm); none written — silence, nothing asked (AudioBank.Has). Pitch from the person's seed
    // Every game makes the same plan from the same words and the person's seed and finds the same people round it, so nothing is
    // sent but the line. Its own layer of the pose (NpcPose, after the gait): small turns over what the game, the clips and the
    // gait left on the bones; a clip that holds the head (BodyAnimator.HeadFree) wins over it. The game's own look-at does not
    // turn a sitting person's head (CharacterAnimEvent: no weight for a non-player that cannot move), and most talks sit: hence ours.
    // Hands: one gesture for a long line with no mark of its own (Hands, asked by NpcBody.Spoken), standing and empty-handed.
    // Its pose (the user 2026-11-09, «both»): the nearest listener of a long line now and then takes its chin in its hand and holds it
    // while the line lasts (listen_chin.listen, with a term); one asked a question, with no pose on, thinks before it answers (think_chin
    // whole: the hand comes down as it begins). Standing, hands free, not in a scene or a fight, not again within PoseEvery; ordered
    // where it is run only (the clip goes to the other games in its ZDO). It speaks itself: a listening pose is let go.
    // Cost: a plan and one look round the people once a line; bones only while it speaks, listens or eases back, and near the eye
    // (SeenNear); nothing at all otherwise.
    [DisallowMultipleComponent]
    internal sealed class NpcSpeech : MonoBehaviour
    {
        // ---------------------------------------------------------------- numbers (raven npc tune NpcSpeech.<name>)
        internal static float Syllables = 4.6f;                                         // a second, by an average speaker (× 0.85 … 1.15 by its chattiness)
        internal static float CommaPause = 0.22f, StopPause = 0.45f, TrailPause = 0.6f;  // s after «,» · «. ? !» · «…»
        internal static float Bob = 1.76f, Nod = 4.8f, Dip = 5.2f, Tilt = 7.2f, Lean = 4f, Aside = 10.4f, Breath = 2.5f; // degrees, at strength 1 (the user 2026-11-09, Тюра: «barely moves» at the first numbers)
        internal static float Heed = 0.48f; // a listener's nods and tilts × the speaker's
        internal static float Lift = 0.7f;  // looking at someone, this share of how far its carriage bowed its head is taken back (hungry, low: it still looks at you, a little from under)
        internal static float ThinkMin = 0.4f, ThinkMax = 1.2f, ThinkAside = 12f, ThinkChance = 0.6f; // s · degrees · how often the one spoken to glances aside
        internal static float ListenNear = 4f, ListenFor = 6f, ReplyWithin = 12f;       // m · s a listener keeps looking · s a reply looks back at the one before
        internal static float LookEase = 0.3f, LookYaw = 60f, LookPitch = 25f;          // s to turn · most degrees aside · up or down
        internal static float GameShare = 0.5f; // standing, the game already turns the head toward where it looks (CharacterAnimEvent): this share of that turn is not added again (NOT measured: tune live)
        internal static float OnRate = 4f, SeenNear = 30f;                              // its speech eased in and out in a quarter of a second · m from the eye
        internal static float SoundVol = 0.5f, SoundRange = 8f, BreathChance = 0.5f, HmChance = 0.4f;
        internal static int HandsWords = 7;                                             // words: a line this long may get its hands
        internal static float HandsChance = 0.5f;                                       // × (0.4 + chattiness)
        internal static bool Trace = false;                                             // (each line's plan into the log: raven npc tune NpcSpeech.Trace 1)
        internal const string SoundBreath = "speech_breath", SoundHm = "speech_hm";
        internal const string ThinkClip = "think_chin", ListenClip = "listen_chin.listen", ListenSitClip = "listen_sit_chin.listen"; // (standing · on a bench: its own body there; on the ground — none)
        internal static float ListenPoseMin = 2.5f, ListenPoseChance = 0.35f, ThinkPoseChance = 0.5f, ThinkPoseFrom = 0.7f, PoseEvery = 25f; // s of a line · × (0.5 + curiosity) · the same · s of thinking · s

        // ---------------------------------------------------------------- one line's plan
        private struct Mark { public float T; public char Kind; } // '.', ',', '?', '!', '~' (… trailing off)
        private sealed class Plan
        {
            public float Length, Rate, Phase, Side, Strength, Think;
            public string Text; // (its words as they came: the small talk asks its length of the same line — Seconds)
            public bool Thinks, Hm;
            public readonly List<float> From = new List<float>(), To = new List<float>(); // (its words: when it is voicing)
            public readonly List<float> Beats = new List<float>(), BeatK = new List<float>();
            public readonly List<float> Breaths = new List<float>();
            public readonly List<bool> Breathed = new List<bool>(); // (that breath heard)
            public readonly List<Mark> Marks = new List<Mark>();
        }

        private const string Vowels = "аеєиіїоуюяaeiouyАЕЄИІЇОУЮЯAEIOUY";

        // the words with what is not said taken out: tags, a form's braces, a gesture's mark
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '<' || c == '{') { depth++; continue; }
                if (c == '>' || c == '}') { if (depth > 0) depth--; continue; }
                if (depth == 0) sb.Append(c);
            }
            return sb.ToString().Replace("...", "…");
        }

        // how long this person speaks these words (as every game got them): the small talk waits it out before the next line (NpcChatter);
        // the plan its RPC_Say has just made here, else made now (nobody near enough saw it)
        internal static float Seconds(NpcActor a, string text)
        {
            if (a == null || string.IsNullOrEmpty(text)) return 0f;
            NpcSpeech me = a.Speech;
            if (me != null && me._plan != null && me._plan.Text == text) return me._plan.Length;
            return PlanFor(a, text).Length;
        }

        private static uint SeedOf(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcKit.Seed(z) : 0u; }
        private static Plan PlanFor(NpcActor a, string text) { return Make(text, SeedOf(a), NpcPersona.Of(a).Chatty, RavenSpeech.Seconds(text, RavenSpeech.Pace.Bubble)); }
        // its voice's pitch for its sounds: the person's own, from its seed
        private static float PitchOf(NpcActor a) { uint s = SeedOf(a); return Mathf.Lerp(0.9f, 1.1f, Rnd(ref s)); }

        // how long the one spoken to thinks after this line before it answers: the same on every game (NpcChatter waits it out)
        internal static float Think(string text) { return ThinkClean(Clean(text)); }
        private static float ThinkClean(string t) { uint r = (uint)Hash(t) ^ 0x9E3779B9u; return Mathf.Lerp(ThinkMin, ThinkMax, Rnd(ref r)); }

        // its plan: the same words and the same person give the same plan on every game
        private static Plan Make(string text, uint seed, float chatty, float most)
        {
            string t = Clean(text);
            uint r = seed ^ (uint)Hash(t);
            Plan p = new Plan { Rate = Syllables * Mathf.Lerp(0.85f, 1.15f, chatty), Phase = Rnd(ref r) * 6.283f, Side = Rnd(ref r) < 0.5f ? -1f : 1f, Strength = Mathf.Lerp(0.75f, 1.25f, chatty), Think = ThinkClean(t), Text = text };
            p.Thinks = Rnd(ref r) < ThinkChance; p.Hm = Rnd(ref r) < HmChance;
            float per = 1f / p.Rate, now = 0.25f;
            bool first = true;
            foreach (string word in t.Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int v = 0, digits = 0;
                foreach (char c in word) { if (Vowels.IndexOf(c) >= 0) v++; else if (char.IsDigit(c)) digits++; }
                if (v == 0 && digits > 0) v = 2; // (a number said aloud)
                if (v > 0)
                {
                    if (first) { p.Breaths.Add(Mathf.Max(0f, now - 0.3f)); p.Breathed.Add(Rnd(ref r) > BreathChance); } // (not every breath heard)
                    float w = v * per;
                    if (first || (v >= 3 && Rnd(ref r) < 0.6f) || word.IndexOf('!') >= 0) { p.Beats.Add(now + w * 0.45f); p.BeatK.Add(word.IndexOf('!') >= 0 ? 1.3f : 0.7f + Rnd(ref r) * 0.3f); }
                    p.From.Add(now); p.To.Add(now + w);
                    now += w;
                    first = false;
                }
                string bare = word.TrimEnd('»', '"', '\'', ')', '”', '“');
                char end = bare.Length > 0 ? bare[bare.Length - 1] : ' '; // (a sentence closed in quotes: «…кажу.» ends at its stop)
                if (word.IndexOf('…') >= 0) { p.Marks.Add(new Mark { T = now, Kind = '~' }); now += TrailPause; first = true; }
                else if (word.IndexOf('?') >= 0) { p.Marks.Add(new Mark { T = now, Kind = '?' }); now += StopPause; first = true; }
                else if (end == '!') { p.Marks.Add(new Mark { T = now, Kind = '!' }); now += StopPause; first = true; }
                else if (end == '.') { p.Marks.Add(new Mark { T = now, Kind = '.' }); now += StopPause; first = true; }
                else if (end == ',' || end == ';' || end == ':' || word == "—" || word == "-") { p.Marks.Add(new Mark { T = now, Kind = ',' }); now += CommaPause; }
                else now += per * 0.25f; // (between two words)
            }
            p.Length = Mathf.Min(now + 0.2f, most);
            return p;
        }

        private static int Hash(string s) { int h = 17; foreach (char c in s) h = h * 31 + c; return h; }
        private static float Rnd(ref uint s) { if (s == 0u) s = 0x6D2B79F5u; s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / 16777216f; }

        // a bump: up in «up» s from its moment, down again over «down» s (0 before and after)
        private static float Bump(float x, float up, float down)
        {
            if (x <= 0f) return 0f;
            if (x < up) { float k = x / up; return k * k * (3f - 2f * k); }
            x -= up;
            if (x < down) { float k = 1f - x / down; return k * k * (3f - 2f * k); }
            return 0f;
        }
        // held: up in «up», held «hold», down over «down»
        private static float Held(float x, float up, float hold, float down) { return x > up && x < up + hold ? 1f : Bump(x > up ? x - hold : x, up, down); }

        // ---------------------------------------------------------------- who says, who hears (every game)
        private sealed class Spoke { public Character Who; public Vector3 At; public float When; }
        private static readonly List<Spoke> Lately = new List<Spoke>(); // session: who spoke here lately (the reply looks back)

        // a person said this line over its head (RPC_Say)
        internal static void Said(NpcActor a, string text) { Said(a, text, null); }

        // ...in your talk window (on your game): every game hears it from the person (NpcActor.Speak → Said with you named)
        internal static void Window(NpcActor a, string text, Player you)
        {
            if (a == null || string.IsNullOrEmpty(text) || you == null) return;
            a.Speak(text, you);
        }

        // a person says this line, to «to» when it is known (your talk), else to whoever it finds near
        internal static void Said(NpcActor a, string text, Character to)
        {
            if (a == null || string.IsNullOrEmpty(text)) return;
            NpcSpeech me = a.Speech;
            Character c = me != null ? me.Body : null;
            if (me == null || c == null) return;
            Vector3 at = a.transform.position;
            float now = Time.time;
            Character back = null;
            for (int i = Lately.Count - 1; i >= 0; i--)
            {
                Spoke s = Lately[i];
                if (s.Who == null || now - s.When > ReplyWithin) { Lately.RemoveAt(i); continue; }
                if (back == null && s.Who != c && (s.At - at).sqrMagnitude < ListenNear * ListenNear * 2.25f) back = s.Who;
            }
            Lately.Add(new Spoke { Who = c, At = at, When = now });
            if (Lately.Count > 32) Lately.RemoveAt(0);
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - at).sqrMagnitude > SeenNear * SeenNear) return; // (nobody here sees it: its plan not even made)
            Plan plan = PlanFor(a, text);
            me._plan = plan; me._start = now; me._prevT = -1f; me._heardCut = true; // (it speaks now: what it was listening to eases off, never snapped)
            // those who hear it: the nearest one or two people, free to look up (not lying, not senseless)
            NpcSpeech l1 = null, l2 = null; float d1 = ListenNear * ListenNear, d2 = d1; NpcActor near = null;
            foreach (NpcActor o in NpcActor.Live) // heavy: once a line, the people loaded here (a base: some tens); the far ones out at once
            {
                if (o == null || o == a) continue;
                float d = (o.transform.position - at).sqrMagnitude;
                if (d >= d2) continue; // (the far ones out first: their ZDO not read)
                NpcSpeech os = o.Speech;
                if (os == null || !Free(o)) continue;
                if (d < d1) { l2 = l1; d2 = d1; l1 = os; d1 = d; near = o; } else { l2 = os; d2 = d; }
            }
            if (to == null) to = back;
            if (to == null && near != null) to = near.GetComponent<Character>();
            if (to == null) { Player p = Player.GetClosestPlayer(at, ListenNear); if (p != null) to = p; }
            float until = now + plan.Length;
            me.LookAt(to, until + 1.5f);
            Character addressed = to;
            // the one it speaks to always hears it, nearest or not (by a fire they sit across it, up to 5 m: 2026-11-09, «heard by -»)
            NpcActor toPerson = to != null ? NpcActor.Of(to) : null;
            NpcSpeech toEar = toPerson != null && Free(toPerson) ? toPerson.Speech : null;
            if (toEar != null && toEar != l1 && toEar != l2) { l2 = l1; l1 = toEar; near = toPerson; }
            if (l1 != null) l1.Hear(c, plan, now, l1.Body == addressed);
            if (l2 != null) l2.Hear(c, plan, now, l2.Body == addressed);
            Pose(a, c, near, to, plan);
            if (Trace) Npcs.Log(a.Id + " speaks " + plan.Length.ToString("0.0") + " s: " + plan.From.Count + " words, " + plan.Beats.Count + " beats, marks «" + Marks(plan) + "», to " + (to != null ? to.GetHoverName() : "-") + ", heard by " + (l1 != null ? l1.name : "-") + (l2 != null ? ", " + l2.name : "") + (plan.Thinks ? ", thinks " + plan.Think.ToString("0.0") + " s" : ""));
        }
        private static string Marks(Plan p) { System.Text.StringBuilder sb = new System.Text.StringBuilder(); foreach (Mark m in p.Marks) sb.Append(m.Kind); return sb.ToString(); }

        // its listening pose let go as it speaks; the nearest listener's pose; the asked one's thinking (on the game that runs each)
        private static readonly Dictionary<NpcActor, float> PosedTill = new Dictionary<NpcActor, float>(); // session: no pose again before this
        private static void Pose(NpcActor a, Character c, NpcActor near, Character to, Plan plan)
        {
            if (a.Owned) { BodyAnimator ba = BodyAnimator.Of(c); string on = ba != null ? ba.ClipIn(NpcBody.ClipSlot) : null; if (on == ListenClip || on == ListenSitClip) a.Order("clip none"); }
            if (near != null && plan.Length >= ListenPoseMin && MayPose(near, true) && Random.value < ListenPoseChance * (0.5f + NpcPersona.Of(near).Curiosity))
            {
                near.Order("clip " + (OnBench(near) ? ListenSitClip : ListenClip) + " " + (plan.Length + 0.8f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                Posed(near);
                return;
            }
            NpcActor asked = to != null ? NpcActor.Of(to) : null;
            bool question = plan.Marks.Count > 0 && plan.Marks[plan.Marks.Count - 1].Kind == '?';
            if (asked == null || !question || !plan.Thinks || plan.Think < ThinkPoseFrom || !MayPose(asked, false) || Random.value > ThinkPoseChance * (0.5f + NpcPersona.Of(asked).Curiosity)) return;
            NpcSpeech its = asked.Speech;
            if (its == null) return;
            Posed(asked);
            its.StartCoroutine(ThinkAfter(asked, Mathf.Max(0f, plan.Length - 0.3f))); // (as the question ends)
        }

        private static System.Collections.IEnumerator ThinkAfter(NpcActor who, float wait)
        {
            yield return new WaitForSeconds(wait);
            if (who != null && Ready(who, false)) who.Order("clip " + ThinkClip);
        }

        private static bool MayPose(NpcActor o, bool bench)
        {
            float till;
            if (o == null || (PosedTill.TryGetValue(o, out till) && Time.time < till)) return false;
            return Ready(o, bench);
        }
        // bench: a pose of its own there too (a seat's attach, not the ground: the hips half a metre lower, another clip)
        private static bool OnBench(NpcActor o) { NpcBody b = o.Body; return b != null && b.AttachedSeen == "attach_chair"; }
        private static bool Ready(NpcActor o, bool bench)
        {
            if (!o.Owned || NpcMind.EatingOf(o) || (NpcMind.SeatedOf(o) && !(bench && OnBench(o))) || NpcGear.HandsOf(o) != NpcGear.HandsFree || !Free(o)) return false;
            NpcMind.Layer l = NpcMind.LayerNow(o);
            if (l == NpcMind.Layer.Scene || l == NpcMind.Layer.Fight) return false;
            NpcBody b = o.Body;
            return b != null && b.ClipFree;
        }
        private static void Posed(NpcActor o) { if (PosedTill.Count > 64) PosedTill.Clear(); PosedTill[o] = Time.time + PoseEvery; }

        // up and about enough to turn its head (any game: its ZDO)
        private static bool Free(NpcActor o)
        {
            if (NpcVitals.IsDown(o)) return false;
            NpcBody b = o.Body;
            return b == null || b.AttachedSeen != "attach_bed";
        }

        // a long line with no gesture of its own: its hands, now and then (NpcBody.Spoken, on the game that says it); "" — none
        internal static string Hands(NpcActor a, string text)
        {
            if (a == null || string.IsNullOrEmpty(text)) return "";
            if (Clean(text).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length < HandsWords) return "";
            if (NpcMind.SeatedOf(a) || NpcMind.LayerNow(a) == NpcMind.Layer.Scene || NpcGear.HandsOf(a) != NpcGear.HandsFree) return ""; // (a scene says its own; a seat, a weapon: no)
            NpcBody b = a.Body;
            if (b == null || !b.ClipFree || Random.value > HandsChance * (0.4f + NpcPersona.Of(a).Chatty)) return "";
            string t = text.TrimEnd();
            return t.EndsWith("?") || t.EndsWith("…") || t.EndsWith("...") ? "arms_open" : "talk_hands";
        }

        // ---------------------------------------------------------------- its body
        private Plan _plan; private float _start, _prevT = -1f;       // (what it says)
        private Plan _heard; private float _heardStart; private bool _addressed, _heardCut; private float _heardPrev = -1f; // (what it hears)
        private Character _to; private float _toUntil;
        private float _yaw, _pitch, _aimOn, _on, _ear;
        private Humanoid _h;
        private Animator _anim;
        private Transform _spine2, _neck, _head;
        private Quaternion _wrote; private bool _wroteOn;
        private NpcActor _me; private float _stateAt; private bool _upright = true; private float _seatedK = 1f; // (its ZDO read four times a second, not each frame)
        internal static float StateEvery = 0.25f;

        internal Humanoid Body { get { if (_h == null) _h = GetComponent<Humanoid>(); return _h; } } // (asked before its first frame of speech too)

        private void LookAt(Character who, float until)
        {
            if (who == null || who == Body) return;
            _to = who; _toUntil = until;
        }

        private void Hear(Character speaker, Plan p, float start, bool addressed)
        {
            _heard = p; _heardStart = start; _addressed = addressed; _heardPrev = -1f; _heardCut = false;
            LookAt(speaker, start + p.Length + (addressed && p.Thinks ? p.Think : 0f) + ListenFor);
        }

        private bool Ready()
        {
            if (_h == null) _h = GetComponent<Humanoid>();
            if (_anim == null && _h != null) _anim = GetComponentInChildren<Animator>();
            if (_h == null || _anim == null) return false;
            if (_head == null || BodyGait.Stale(_head, _anim))
            {
                Transform v = _anim.transform;
                _spine2 = BodyClips.SkeletonBone(this, v, "Spine2");
                _neck = BodyClips.SkeletonBone(this, v, "Neck"); _head = BodyClips.SkeletonBone(this, v, "Head");
                _wroteOn = false;
            }
            return _head != null && _neck != null;
        }

        private void Sound(string name)
        {
            if (!AudioBank.Has(name) || _head == null) return;
            Audio.Sound(name, _head.position, new SoundAsk { Volume = SoundVol, Pitch = PitchOf(_me), Range = SoundRange, Follow = _head, Owner = "speech" });
        }

        // one frame (NpcPose, after its gait)
        internal void Pose()
        {
            float now = Time.time;
            bool speaking = _plan != null && now - _start < _plan.Length;
            bool hearing = _heard != null && !_heardCut && now - _heardStart < _heard.Length + _heard.Think + 1f;
            bool looking = _to != null && now < _toUntil;
            if (!speaking && !hearing && !looking && _on <= 0f && _ear <= 0f && _aimOn <= 0f) { _plan = null; _heard = null; _to = null; _wroteOn = false; return; } // (the usual frame: nothing)
            if (!Ready()) return;
            if (_wroteOn && _neck.localRotation == _wrote) return; // (the game did not pose its bones this frame: they still hold ours — the neck, which no later layer of the pose turns: a meal bows the head after us)
            Camera cam = Camera.main;
            bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < SeenNear * SeenNear;
            if (_me == null) _me = NpcActor.Of(_h);
            NpcActor me = _me;
            if (now >= _stateAt) { _stateAt = now + StateEvery; _upright = me == null || Free(me); _seatedK = NpcMind.SeatedOf(me) ? 0.85f : 1f; }
            bool may = near && _upright && !_h.IsDead() && !_h.InAttack();
            BodyAnimator clips = BodyAnimator.Of(_h);
            float w = 1f - (clips != null ? clips.HeadFree : 0f); // (a clip that holds the head: it says where it looks)
            float dt = Time.deltaTime;
            _on = Mathf.MoveTowards(_on, speaking && may ? 1f : 0f, dt * OnRate);
            _ear = Mathf.MoveTowards(_ear, hearing && may ? 1f : 0f, dt * OnRate);
            Quaternion body = transform.rotation;
            float seated = _seatedK;

            // where its head turns: to the one it speaks to or listens to, within what a neck turns; behind it: not at all
            float yawWant = 0f, pitchWant = 0f; bool aim = false;
            if (looking && may)
            {
                Vector3 d = Quaternion.Inverse(body) * (_to.GetEyePoint() - _head.position);
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                if (seated >= 1f) { Vector3 g = GameLook(); if (g.sqrMagnitude > 0.01f) { Vector3 gl = Quaternion.Inverse(body) * g; yaw -= Mathf.Atan2(gl.x, gl.z) * Mathf.Rad2Deg * GameShare; } } // (standing: the game turns its head too; seated it does not)
                if (Mathf.Abs(yaw) < 110f)
                {
                    aim = true;
                    yawWant = Mathf.Clamp(yaw, -LookYaw, LookYaw);
                    pitchWant = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) * Mathf.Rad2Deg - Down() * Lift, -LookPitch - 25f, LookPitch); // (its head bowed by its carriage: lifted to the face)
                }
            }
            float k = 1f - Mathf.Exp(-dt / LookEase);
            _aimOn = Mathf.MoveTowards(_aimOn, aim ? 1f : 0f, dt * 2f);
            if (aim) { _yaw = Mathf.Lerp(_yaw, yawWant, k); _pitch = Mathf.Lerp(_pitch, pitchWant, k); }

            float nod = 0f, roll = 0f, aside = 0f, lean = 0f, breath = 0f;
            // its own line this moment
            if (_plan != null && _on > 0f)
            {
                Plan p = _plan;
                float t = now - _start, s = p.Strength * _on * seated;
                float voiced = 0f;
                for (int i = 0; i < p.From.Count && p.From[i] < t; i++) voiced = Mathf.Max(voiced, Mathf.Clamp01(Mathf.Min(t - p.From[i], p.To[i] - t) / 0.06f)); // (its words in order: none after this moment counts)
                float n0 = Bob * voiced * Mathf.Sin(t * p.Rate * Mathf.PI + p.Phase), r0 = 0f, a0 = 0f, l0 = 0f, b0 = 0f; // (once up and down every two syllables)
                for (int i = 0; i < p.Beats.Count; i++) n0 += Nod * p.BeatK[i] * Bump(t - p.Beats[i] + 0.12f, 0.12f, 0.3f);
                for (int i = 0; i < p.Breaths.Count; i++)
                {
                    b0 += Breath * Bump(t - p.Breaths[i], 0.25f, 0.35f);
                    if (p.Breathed[i] && _prevT < p.Breaths[i] && t >= p.Breaths[i]) Sound(SoundBreath);
                }
                foreach (Mark m in p.Marks)
                {
                    float x = t - m.T;
                    switch (m.Kind)
                    {
                        case '.': n0 += Dip * Bump(x, 0.2f, 0.5f); break;
                        case ',': n0 += Dip * 0.4f * Bump(x, 0.1f, 0.25f); break;
                        case '?': { float h = Held(x, 0.2f, 0.7f, 0.4f); r0 += Tilt * p.Side * h; n0 -= 1.5f * h; } break;
                        case '!': l0 += Lean * Bump(x + 0.3f, 0.2f, 0.4f); break;
                        case '~': { float h = Held(x, 0.3f, 0.5f, 0.5f); a0 += Aside * p.Side * h; n0 += 2f * h; } break;
                    }
                }
                _prevT = t;
                nod += n0 * s; roll += r0 * s; aside += a0 * s; lean += l0 * s; breath += b0 * s;
            }
            else _prevT = -1f;
            // what it hears: a nod at the other's full stop, a tilt at its question, a little back at «!»; spoken to, it thinks before it answers
            if (_heard != null && _ear > 0f)
            {
                Plan p = _heard;
                float t = now - _heardStart, s = Heed * _ear * seated;
                float n0 = 0f, r0 = 0f, l0 = 0f, think = 0f;
                foreach (Mark m in p.Marks)
                {
                    float x = t - m.T - 0.2f; // (a beat after it: it heard it)
                    switch (m.Kind)
                    {
                        case '.': n0 += Dip * Bump(x, 0.18f, 0.4f); break;
                        case '?': r0 -= Tilt * p.Side * Held(x, 0.25f, 0.5f, 0.4f); break; // (the other way: as one answering a look)
                        case '!': l0 -= Lean * Bump(x, 0.15f, 0.4f); break;
                        case '~': n0 += Dip * 0.6f * Bump(x, 0.35f, 0.6f); break;
                    }
                }
                if (_addressed && p.Thinks)
                {
                    float x = t - p.Length, h = Held(x, 0.25f, Mathf.Max(0f, p.Think - 0.5f), 0.3f);
                    think = h; // (a glance away from the one it looks at, a little up: its own strength, not a listener's half)
                    if (p.Hm && _heardPrev < p.Length + 0.3f && t >= p.Length + 0.3f) Sound(SoundHm);
                }
                _heardPrev = t;
                nod += n0 * s - 2f * think * _ear; roll += r0 * s; lean += l0 * s; aside += ThinkAside * -p.Side * think * _ear;
            }

            float yawNow = _yaw * _aimOn, pitchNow = _pitch * _aimOn;
            BodyGait.Turn(_spine2, body, (lean - breath) * w, yawNow * 0.15f * w, 0f); // (not Spine1: the gait knows by it whether the game posed the bones)
            BodyGait.Turn(_neck, body, (pitchNow * 0.35f + nod * 0.3f - breath * 0.4f) * w, (yawNow * 0.35f + aside * 0.3f) * w, roll * 0.3f * w);
            BodyGait.Turn(_head, body, (pitchNow * 0.65f + nod * 0.7f) * w, (yawNow * 0.5f + aside * 0.7f) * w, roll * 0.7f * w);
            _wrote = _neck.localRotation; _wroteOn = true;
        }

        // where the game turns its head (its own look-at: after where its body looks, on every game from the ZDO); zero — unknown
        private static readonly System.Reflection.FieldInfo FHeadDir = HarmonyLib.AccessTools.Field(typeof(CharacterAnimEvent), "m_headLookDir");
        private CharacterAnimEvent _game; private bool _gameLooked;
        private NpcGait _gait; private bool _gaitLooked;
        private float Down() { if (!_gaitLooked) { _gaitLooked = true; _gait = GetComponent<NpcGait>(); } return _gait != null ? _gait.HeadDown : 0f; }
        private Vector3 GameLook()
        {
            if (!_gameLooked) { _gameLooked = true; _game = GetComponentInChildren<CharacterAnimEvent>(); } // heavy: once a body, its first look
            return _game != null && FHeadDir != null ? (Vector3)FHeadDir.GetValue(_game) : Vector3.zero;
        }

        // what it does now, one line (the black box, raven npc gait)
        internal string Now()
        {
            float now = Time.time;
            return "speech " + (_plan != null && now - _start < _plan.Length ? (now - _start).ToString("0.0") + "/" + _plan.Length.ToString("0.0") + " s" : "-") + " on " + _on.ToString("0.00")
                + "; hears " + (_heard != null ? (now - _heardStart).ToString("0.0") + "/" + _heard.Length.ToString("0.0") + " s" + (_addressed ? " (to it)" : "") : "-") + " on " + _ear.ToString("0.00")
                + "; looks at " + (_to != null && now < _toUntil ? _to.GetHoverName() : "-") + " yaw " + _yaw.ToString("0") + " pitch " + _pitch.ToString("0") + " on " + _aimOn.ToString("0.00");
        }
    }
}
#endif

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
    //   the listeners   the nearest one or two people (and, in a scene, all its people however far: SceneOf; the hero's own lines
    //                   in a scene too: HeroSaid) turn their heads to it while it speaks and ListenFor after; they nod at its full
    //                   stops, tilt at its question, draw back a little at «!»; the one spoken to «thinks» after the line before it
    //                   answers — a glance aside (Think: the same time on every game, and the small talk waits it out: NpcChatter)
    //   sounds          its wordless sounds, where a person would make them — the names on «speech_» of assets/story/sounds (none written:
    //                   silence, nothing asked), in the person's own timbre and pitch (NpcTimbre) and of its own takes (NpcTimbre.Takes):
    //                     the speaker    a breath before a LONG sentence, and not again for a while (in the game, 2026-11-13, the user:
    //                                    «найчастіше вдих робить» — it stood before every sentence at one in two, and nothing else of a
    //                                    speaker's came half as often) · its throat cleared before a long line (instead of that breath) · a
    //                                    sigh where it trails off («…») · the sounds its own words write: «Хм», «Ем…», «Ага», «О!», «Ох»,
    //                                    «Ха-ха» and their English (Written) — said where they stand, always
    //                     to you         what it makes of your answer in its talk (React, from NpcTalk.Pick: the user, the same day —
    //                                    «коли кажу, що виконаю завдання, можна ж давати "О"», «коли прощаюсь — іноді звук»): glad «о!» when
    //                                    you take its request on or give it something, «угу» to a kind word and now and then to your
    //                                    goodbye, a chuckle to a joke, a sigh to a cold answer, «хм?» to a rude one. Decided on your game,
    //                                    by chance (React… numbers), then heard by all (NpcActor.React) — once the hero has said his answer
    //                                    aloud in his own voice (NpcTalk.Pick, NpcTimbre.OfHero): it hears you out
    //                     the listener   «угу» at the speaker's full stop mid-line · «о!» at its «!» · spoken to: «хм» or «ем…» while it
    //                                    thinks before it answers · «хм?» when the speaker trailed off and it waits for the rest
    //                   How often is the person's own (Ways: from its ways and age — the talkative agree aloud more, the curious wonder,
    //                   the timid say «ем…» where the bold hum, the old breathe and clear their throats more, the lazy sigh). Each is
    //                   decided from the line's words and the person's number (Roll): the same on every game, nothing sent
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
        internal static float SoundVol = 0.5f, SoundRange = 8f, BreathChance = 0.25f, HmChance = 0.4f;
        internal static int BreathWords = 6;                           // words: a sentence this long may begin with a breath that is heard
        internal static float BreathEvery = 20f, QuietAfter = 2f;      // s: not two heard breaths of one person nearer than this · no breath right after another sound of its own
        internal static float ReactGlad = 0.7f, ReactWarm = 0.35f, ReactLaugh = 0.6f, ReactSour = 0.5f, ReactHurt = 0.8f, ReactBye = 0.35f; // how often it answers your answer aloud
        internal static float AgreeChance = 0.18f, WonderChance = 0.25f, PromptChance = 0.3f, SighChance = 0.3f, KhmChance = 0.12f; // by an average person; × its ways (Ways)
        internal static int KhmWords = 9; // words: a line this long may begin with the throat cleared
        internal static int HandsWords = 7;                                             // words: a line this long may get its hands
        internal static float HandsChance = 0.5f;                                       // × (0.4 + chattiness)
        internal static bool Trace = false;                                             // (each line's plan into the log: raven npc tune NpcSpeech.Trace 1)
        internal const string SoundBreath = "speech_breath", SoundHm = "speech_hm", SoundEm = "speech_em", SoundUhu = "speech_uhu", SoundOh = "speech_oh", SoundHmAsk = "speech_hm_ask",
            SoundSigh = "speech_sigh", SoundKhm = "speech_khm", SoundChuckle = "speech_chuckle";
        internal const string ThinkClip = "think_chin", ListenClip = "listen_chin.listen", ListenSitClip = "listen_sit_chin.listen"; // (standing · on a bench: its own body there; on the ground — none)
        internal const string HandsClip = "talk_hands", AskHandsClip = "arms_open"; // (its hands with a long line · with a question or a trailing one)
        internal static readonly string[] TalkClips = { HandsClip, AskHandsClip, ThinkClip, ListenClip, ListenSitClip }; // (what a talk's own body does: they fit a talk, NpcMoves.TalkFits)
        internal static float ListenPoseMin = 2.5f, ListenPoseChance = 0.35f, ThinkPoseChance = 0.5f, ThinkPoseFrom = 0.7f, PoseEvery = 25f; // s of a line · × (0.5 + curiosity) · the same · s of thinking · s

        // ---------------------------------------------------------------- one line's plan
        private struct Mark { public float T; public char Kind; } // '.', ',', '?', '!', '~' (… trailing off)
        private sealed class Plan
        {
            public float Length, Rate, Phase, Side, Strength, Think;
            public string Text; // (its words as they came: the small talk asks its length of the same line — Seconds)
            public uint Key;    // (the line's own number: what a person's chances are rolled with — Roll)
            public bool Thinks;
            public readonly List<KeyValuePair<float, string>> Says = new List<KeyValuePair<float, string>>(); // (the sounds its own words write: when, which)
            public readonly List<int> Words = new List<int>(); // (how many words each sentence has: a breath is for a long one)
            public readonly List<float> From = new List<float>(), To = new List<float>(); // (its words: when it is voicing)
            public readonly List<float> Beats = new List<float>(), BeatK = new List<float>();
            public readonly List<float> Breaths = new List<float>();
            public readonly List<Mark> Marks = new List<Mark>();
        }

        private const string Vowels = "аеєиіїоуюяaeiouyАЕЄИІЇОУЮЯAEIOUY";
        private static readonly string[] Laughter = { "ха-ха", "хе-хе", "ги-ги", "ha-ha", "haha", "he-he", "hehe" }; // (how a line's words laugh, in the languages the lines are written in)

        // the sound a word of a line writes (null: none): «Хм.», «Ем…», «Ага,», «О!», «Ох…», «Кхм.», «Ха-ха» and their English. A one-letter «О», «А» only
        // with its mark on it («О!», «О,»): bare it is a word like any other
        private static string Written(string word)
        {
            string w = word.TrimStart('«', '"', '\'', '(', '„', '“').ToLowerInvariant();
            foreach (string laugh in Laughter) if (w.StartsWith(laugh, System.StringComparison.Ordinal)) return SoundChuckle;
            string bare = w.TrimEnd('.', ',', '!', '?', '…', ':', ';', '»', '"', '”', ')', '-', '—');
            bool marked = bare.Length < w.Length;
            switch (bare)
            {
                case "хм": case "гм": case "хмм": case "хммм": case "hm": case "hmm": case "hmmm": return SoundHm;
                case "ем": case "е-е": case "е-е-е": case "ее": case "ммм": case "мм": case "um": case "uh": case "er": case "erm": case "umm": return SoundEm;
                case "угу": case "ага": case "мгм": case "aha": case "mhm": case "uh-huh": case "mm-hm": return SoundUhu;
                case "ого": case "ой": case "oh": case "ooh": case "whoa": case "wow": return SoundOh;
                case "о": case "а": case "ах": case "ah": return marked ? SoundOh : null;
                case "ох": case "ех": case "eh": case "ugh": case "sigh": return SoundSigh;
                case "кхм": case "кхе": case "кхе-кхе": case "ahem": return SoundKhm;
                case "ха": case "хе": case "ha": case "heh": return marked ? SoundChuckle : null;
            }
            return null;
        }

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

        // how long the one spoken to thinks after this line before it answers: the same on every game (NpcChatter waits it out)
        internal static float Think(string text) { return ThinkClean(Clean(text)); }
        private static float ThinkClean(string t) { uint r = (uint)Hash(t) ^ 0x9E3779B9u; return Mathf.Lerp(ThinkMin, ThinkMax, Rnd(ref r)); }

        // its plan: the same words and the same person give the same plan on every game
        private static Plan Make(string text, uint seed, float chatty, float most)
        {
            string t = Clean(text);
            uint r = seed ^ (uint)Hash(t);
            Plan p = new Plan { Rate = Syllables * Mathf.Lerp(0.85f, 1.15f, chatty), Phase = Rnd(ref r) * 6.283f, Side = Rnd(ref r) < 0.5f ? -1f : 1f, Strength = Mathf.Lerp(0.75f, 1.25f, chatty), Think = ThinkClean(t), Text = text, Key = (uint)Hash(t) };
            p.Thinks = Rnd(ref r) < ThinkChance;
            float per = 1f / p.Rate, now = 0.25f;
            bool first = true;
            foreach (string word in t.Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int v = 0, digits = 0;
                foreach (char c in word) { if (Vowels.IndexOf(c) >= 0) v++; else if (char.IsDigit(c)) digits++; }
                if (v == 0 && digits > 0) v = 2; // (a number said aloud)
                if (v > 0)
                {
                    if (first) { p.Breaths.Add(Mathf.Max(0f, now - 0.3f)); p.Words.Add(0); }
                    p.Words[p.Words.Count - 1]++;
                    string written = Written(word);
                    if (written != null) p.Says.Add(new KeyValuePair<float, string>(now, written));
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

        // a chance of this person at this place of this line: 0…1, the same on every game (the line's number, the person's, which chance)
        private static float Roll(uint line, uint person, uint which) { uint s = line * 2654435761u ^ person ^ (which * 0x9E3779B9u); Rnd(ref s); return Rnd(ref s); }

        // a person's ways with its wordless sounds: how often each, from its ways and age (the header says who does what more)
        private sealed class Ways { public float Hm, Breath, Agree, Wonder, Prompt, Sigh, Khm; public bool Em; public uint Seed, Takes; }
        private static Ways WaysOf(NpcActor a)
        {
            NpcPersona w = NpcPersona.Of(a);
            NpcSettlers.Look look = NpcSettlers.Of(a);
            int age = look != null ? look.Age : 1; // (young · grown · old; one written by hand: grown)
            uint seed = SeedOf(a), s = seed ^ 0x51ED270Bu;
            Ways m = new Ways { Seed = seed, Takes = NpcTimbre.Takes(a) };
            m.Hm = Mathf.Clamp01(HmChance * (0.5f + w.Chatty) * (0.7f + 0.6f * Rnd(ref s)));
            m.Em = Rnd(ref s) < Mathf.Lerp(0.6f, 0.1f, w.Bravery);
            m.Breath = Mathf.Clamp01(BreathChance * (age == 2 ? 1.4f : age == 0 ? 0.7f : 1f) * (0.8f + 0.4f * Rnd(ref s)));
            m.Agree = Mathf.Clamp01(AgreeChance * (0.4f + w.Chatty + 0.5f * w.Curiosity) * (0.6f + 0.8f * Rnd(ref s)));
            m.Wonder = Mathf.Clamp01(WonderChance * (0.5f + w.Curiosity) * (0.6f + 0.8f * Rnd(ref s)));
            m.Prompt = Mathf.Clamp01(PromptChance * (0.5f + w.Curiosity));
            m.Sigh = Mathf.Clamp01(SighChance * (0.6f + w.Laziness) * (age == 2 ? 1.5f : 1f));
            m.Khm = Mathf.Clamp01(KhmChance * (age == 2 ? 2f : age == 0 ? 0.4f : 1f) * (0.5f + Rnd(ref s)));
            return m;
        }
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
            me._ways = null; // (its card and ways anew for this line: MyWays)
            me._babbling = me.Babble(a, text, plan.Length, to); // (the line hummed in its voice: NpcBabble — its own short sounds keep out of it)
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
            long scene = SceneOf != null ? SceneOf(a) : 0L;
            if (scene != 0L) foreach (NpcSpeech w in InScene(scene, a)) if (w != l1 && w != l2) w.Hear(c, plan, now, w.Body == addressed); // (its scene's people, however far)
            Pose(a, c, near, to, plan);
            if (Trace) Npcs.Log(a.Id + " speaks " + plan.Length.ToString("0.0") + " s: " + plan.From.Count + " words, " + plan.Beats.Count + " beats, marks «" + Marks(plan) + "», to " + (to != null ? to.GetHoverName() : "-") + ", heard by " + (l1 != null ? l1.name : "-") + (l2 != null ? ", " + l2.name : "") + (plan.Thinks ? ", thinks " + plan.Think.ToString("0.0") + " s" : ""));
        }
        // A scene's people hear every line said in it (the user 2026-11-18: Eyvind on his bench, 10 m off the fire in hall_how, looked nowhere while
        // the others spoke — only the nearest one or two within ListenNear heard a line): whose scene holds a person now (the story fills it with its
        // lock, StoryScenes.Holder — the player whose scene it is; 0 none; null in a build with no story). They hear one another, and the hero's
        // lines (HeroSaid), at any distance, as a listener ever does: the head to the speaker, its nods and sounds
        internal static System.Func<NpcActor, long> SceneOf;
        private static readonly List<NpcSpeech> _inScene = new List<NpcSpeech>();
        private static List<NpcSpeech> InScene(long scene, NpcActor not)
        {
            _inScene.Clear();
            if (scene == 0L || SceneOf == null) return _inScene;
            foreach (NpcActor o in NpcActor.Live) // heavy: once a line said in a scene, one ZDO string of each person loaded here
            {
                if (o == null || o == not || SceneOf(o) != scene) continue;
                NpcSpeech os = o.Speech;
                if (os != null && Free(o)) _inScene.Add(os);
            }
            return _inScene;
        }

        // the hero says a line aloud in a scene (its step «player say»; his own hum is the step's, NpcBabble): the people of his scene hear it as
        // a person's line — the head to him, the nods at its stops — and a reply looks back at him (Lately). Not in a scene: the nearest one or two.
        // On the game that plays the scene (where its step runs); those near see the subtitle, not the heads (the step is the director's)
        internal static void HeroSaid(Player p, string text, float seconds)
        {
            if (p == null || string.IsNullOrEmpty(text)) return;
            Vector3 at = p.transform.position;
            float now = Time.time;
            Lately.Add(new Spoke { Who = p, At = at, When = now });
            if (Lately.Count > 32) Lately.RemoveAt(0);
            long id = p.GetPlayerID();
            Plan plan = Make(text, (uint)id ^ (uint)(id >> 32), 1f, seconds);
            List<NpcSpeech> heard = new List<NpcSpeech>(InScene(id, null));
            if (heard.Count == 0)
                foreach (NpcActor o in NpcActor.Live) // heavy: once a line of his, the people loaded here
                {
                    if (o == null || (o.transform.position - at).sqrMagnitude >= ListenNear * ListenNear) continue;
                    NpcSpeech os = o.Speech;
                    if (os != null && Free(o)) heard.Add(os);
                    if (heard.Count >= 2) break;
                }
            foreach (NpcSpeech w in heard) w.Hear(p, plan, now, false);
        }
        private static string Marks(Plan p) { System.Text.StringBuilder sb = new System.Text.StringBuilder(); foreach (Mark m in p.Marks) sb.Append(m.Kind); return sb.ToString(); }

        // its listening pose let go as it speaks; the nearest listener's pose; the asked one's thinking (on the game that runs each)
        private static readonly Dictionary<NpcActor, float> PosedTill = new Dictionary<NpcActor, float>(); // session: no pose again before this
        private static void Pose(NpcActor a, Character c, NpcActor near, Character to, Plan plan)
        {
            if (a.Owned) { BodyAnimator ba = BodyAnimator.Of(c); string on = ba != null ? ba.ClipIn(NpcBody.ClipSlot) : null; if (on != null && on.StartsWith("dv_", System.StringComparison.Ordinal)) on = on.Substring(3); if (on == ListenClip || on == ListenSitClip) a.Order("clip none"); }
            string listens = near != null && plan.Length >= ListenPoseMin && MayPose(near, true) ? NpcMoves.Clip(near, OnBench(near) ? ListenSitClip : ListenClip) : ""; // (its gate: its body's own of it, not one it never makes)
            if (listens.Length > 0 && Random.value < ListenPoseChance * (0.5f + NpcPersona.Of(near).Curiosity) * NpcPersona.Of(near).Lively)
            {
                near.Order("clip " + listens + " " + (plan.Length + 0.8f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                Posed(near);
                return;
            }
            NpcActor asked = to != null ? NpcActor.Of(to) : null;
            bool question = plan.Marks.Count > 0 && plan.Marks[plan.Marks.Count - 1].Kind == '?';
            if (asked == null || !question || !plan.Thinks || plan.Think < ThinkPoseFrom || !MayPose(asked, false) || Random.value > ThinkPoseChance * (0.5f + NpcPersona.Of(asked).Curiosity) * NpcPersona.Of(asked).Lively) return;
            NpcSpeech its = asked.Speech;
            if (its == null) return;
            Posed(asked);
            its.StartCoroutine(ThinkAfter(asked, Mathf.Max(0f, plan.Length - 0.3f))); // (as the question ends)
        }

        private static System.Collections.IEnumerator ThinkAfter(NpcActor who, float wait)
        {
            yield return new WaitForSeconds(wait);
            string thinks = who != null && Ready(who, false) ? NpcMoves.Clip(who, ThinkClip) : "";
            if (thinks.Length > 0) who.Order("clip " + thinks);
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
            if (b == null || !b.ClipFree || Random.value > HandsChance * (0.4f + NpcPersona.Of(a).Chatty) * NpcPersona.Of(a).Lively) return "";
            string t = text.TrimEnd();
            string h = t.EndsWith("?") || t.EndsWith("…") || t.EndsWith("...") ? AskHandsClip : HandsClip;
            return NpcMoves.Clip(a, h); // (its gate: its body's own of it, not one it never makes)
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
        private NpcTimbre.Card _card; private Ways _ways; // (its timbre and its ways with its sounds: worked out once a line it says or hears — a kept timbre that had not reached this game
                                                          // yet, a chance tuned by the author, its ways changed: all right from the next line on)
        private readonly List<KeyValuePair<float, string>> _reacts = new List<KeyValuePair<float, string>>(); // (what it says to the line it hears: when, which sound)
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
            // what it will say to this line, decided now: at most one «угу» and one «о!», a «хм?» to one who trailed off, its «хм» while it thinks
            _reacts.Clear();
            _ways = null;
            Ways w = MyWays();
            if (w == null) return;
            bool agreed = false, wondered = false, prompted = false;
            for (int i = 0; i < p.Marks.Count; i++)
            {
                Mark m = p.Marks[i];
                bool last = i == p.Marks.Count - 1;
                if (m.Kind == '.' && !last && !agreed && Roll(p.Key + (uint)i, w.Seed, 1u) < w.Agree) { _reacts.Add(new KeyValuePair<float, string>(m.T + 0.25f, SoundUhu)); agreed = true; }
                else if (m.Kind == '!' && !wondered && Roll(p.Key + (uint)i, w.Seed, 2u) < w.Wonder) { _reacts.Add(new KeyValuePair<float, string>(m.T + 0.2f, SoundOh)); wondered = true; }
                else if (m.Kind == '~' && last && addressed && Roll(p.Key, w.Seed, 3u) < w.Prompt) { _reacts.Add(new KeyValuePair<float, string>(m.T + 0.5f, SoundHmAsk)); prompted = true; }
            }
            if (!prompted && addressed && p.Thinks && Roll(p.Key, w.Seed, 4u) < w.Hm) _reacts.Add(new KeyValuePair<float, string>(p.Length + 0.3f, w.Em ? SoundEm : SoundHm)); // (one hum for one line: none after its «хм?»)
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

        // its card and its ways (null: it has no body here yet)
        private Ways MyWays()
        {
            if (_me == null) { Humanoid h = Body; _me = h != null ? NpcActor.Of(h) : null; }
            if (_me == null) return null;
            if (_ways == null) { _card = NpcTimbre.Of(_me); _ways = WaysOf(_me); }
            return _ways;
        }

        private float _soundAt = -99f, _breathAt = -99f; // (its last sound of any kind · its last heard breath)

        // What a person makes of your answer in its talk: which sound (null: none this time). On the game that answers, by what the answer does —
        // its deeds, your tone, or that it is your goodbye; the strongest of them counts. NpcTalk.Pick asks, NpcActor.React sends it to every game.
        private static readonly string[] GladDeeds = { "promise", "taskdo", "favour", "goalgive", "giverequest", "givefood", "givemead", "healmead", "givebackitem", "hire", "settle" };
        private static readonly string[] SourDeeds = { "unpromise", "taskdrop", "unhire", "goaway", "unsettle" };
        internal static string ReactionTo(List<string> acts, bool bye)
        {
            int rank = 0; // hurt 5 · glad 4 · sour 3 · laugh 2 · warm 1 · bye 0
            foreach (string act in acts)
            {
                string[] w = act.Split(' ');
                int r = w[0] == "tone" && w.Length > 1 ? (w[1] == "rude" ? 5 : w[1] == "cold" ? 3 : w[1] == "warm" ? (w.Length > 2 && w[2] == "joke" ? 2 : 1) : 0)
                    : System.Array.IndexOf(GladDeeds, w[0]) >= 0 ? 4 : System.Array.IndexOf(SourDeeds, w[0]) >= 0 ? 3 : 0;
                if (r > rank) rank = r;
            }
            if (rank == 0 && !bye) return null;
            float chance = rank == 5 ? ReactHurt : rank == 4 ? ReactGlad : rank == 3 ? ReactSour : rank == 2 ? ReactLaugh : rank == 1 ? ReactWarm : ReactBye;
            if (Random.value >= chance) return null;
            return rank == 5 ? SoundHmAsk : rank == 4 ? SoundOh : rank == 3 ? SoundSigh : rank == 2 ? SoundChuckle : SoundUhu;
        }

        // ...and it is heard (every game: NpcActor.RPC_React), from the person, in its timbre
        internal static void Reacted(NpcActor a, string sound, float after)
        {
            NpcSpeech me = a != null ? a.Speech : null;
            if (me == null || string.IsNullOrEmpty(sound) || !sound.StartsWith(NpcTimbre.Prefix, System.StringComparison.Ordinal)) return; // (a voice sound only: the word comes from another game)
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - a.transform.position).sqrMagnitude > SeenNear * SeenNear) return;
            if (!me.Ready()) return;
            me._ways = null;
            me.Sound(sound, after);
        }

        // the line as a wordless hum in its own voice (NpcBabble); false: not hummed (no voice for it, the voice package speaks, its files on their way)
        private bool _babbling;
        private bool Babble(NpcActor a, string text, float seconds, Character to)
        {
            Ways w = MyWays();
            if (_head == null || w == null || _card == null) return false;
            return NpcBabble.Say(_card, _head, text, SeedOf(a), seconds, to != null && to == Player.m_localPlayer) > 0f;
        }

        private void Sound(string name, float after = 0f) // (after: seconds before it starts)
        {
            Ways w = MyWays();
            if (_head == null || w == null) { Told(name, "its body is not here yet"); return; }
            if (_card == null) { Told(name, "no voice suits it: silent"); return; }
            string its = NpcTimbre.Name(_card, name);
            if (!AudioBank.Has(its)) { Told(name, "its voice «" + _card.Timbre + "» has no such sound"); return; }
            _soundAt = Time.time + after;
            bool heard = Audio.Sound(its, _head.position, new SoundAsk { Volume = SoundVol, Pitch = _card.Pitch, Range = SoundRange, Follow = _head, Owner = "speech", Own = w.Takes, Delay = after });
            Told(name, heard ? null : "the sound core did not play «" + its + "» (the log: Audio:)");
        }

        // ---------------------------------------------------------------- what its voice did (the author's look: raven npc voice <who>)
        // A sound played leaves no line in the log, and one not played left nothing at all: «he makes no sound in a talk» (the user 2026-11-14) could not be
        // told from «his voice has no breath and his chances are low». So every sound this person was to make on this game is kept with what came of it —
        // the last ToldMost of them, a session only (nothing of it is sent or saved) — and the console word says them with its voice and its chances.
        private struct ToldOne { public float At; public string Sound, Why; }
        private readonly List<ToldOne> _told = new List<ToldOne>(); // session: read by Report alone
        internal static int ToldMost = 12;
        private static readonly string[] AllSounds = { SoundBreath, SoundKhm, SoundSigh, SoundHm, SoundEm, SoundHmAsk, SoundUhu, SoundOh, SoundChuckle };

        private void Told(string sound, string why)
        {
            if (_told.Count >= ToldMost) _told.RemoveAt(0);
            _told.Add(new ToldOne { At = Time.time, Sound = sound, Why = why });
            if (Trace) Npcs.Log((_me != null ? _me.Id : name) + " sound " + sound + (why == null ? ": played" : ": NOT played — " + why));
        }

        // its voice in words (the author's console, Ukrainian as the rest of it): which timbre and why, which sounds that timbre has, how often it makes
        // each by its ways, and the last sounds it was to make here
        internal static string Report(NpcActor a)
        {
            NpcSpeech me = a != null ? a.Speech : null;
            if (me == null) return "У цієї людини нема мовлення (NpcSpeech).";
            me._ways = null;
            Ways w = me.MyWays();
            if (w == null) return "Її тіла тут ще нема.";
            NpcTimbre.Card c = me._card;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string kept = NpcTimbre.Kept(a), written = a.Def != null ? a.Def.Timbre : "";
            sb.Append(a.Name).Append(" (").Append(a.Id).Append("): ");
            if (c == null) sb.Append("голосу НЕМА — жоден тембр їй не пасує, мовчить завжди");
            else sb.Append("голос «").Append(c.Timbre).Append("», тон ×").Append(c.Pitch.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                .Append(written.Length > 0 ? " (написаний у її файлі)" : kept.Length > 0 ? " (тримає: «" + kept + "», пул " + NpcTimbre.Pool + ")" : " (ще не записаний у шафку)");
            if (c != null)
            {
                sb.Append("\n  вигуки цього голосу: ");
                System.Text.StringBuilder none = new System.Text.StringBuilder();
                foreach (string s in AllSounds)
                {
                    if (AudioBank.Has(NpcTimbre.Name(c, s))) sb.Append(s.Substring(NpcTimbre.Prefix.Length)).Append(' ');
                    else none.Append(s.Substring(NpcTimbre.Prefix.Length)).Append(' ');
                }
                if (none.Length > 0) sb.Append("· НЕМА: ").Append(none);
            }
            sb.Append("\n  як часто (її вдача й вік): вдих ").Append(Pct(w.Breath)).Append(" перед реченням від ").Append(BreathWords).Append(" слів і не частіше ніж раз на ").Append(BreathEvery.ToString("0")).Append(" с")
                .Append(" · кхм ").Append(Pct(w.Khm)).Append(" перед реплікою від ").Append(KhmWords).Append(" слів")
                .Append(" · зітхання ").Append(Pct(w.Sigh)).Append(" на «…»")
                .Append("\n  слухаючи іншу людину: угу ").Append(Pct(w.Agree)).Append(" · о! ").Append(Pct(w.Wonder)).Append(" · хм? ").Append(Pct(w.Prompt)).Append(" · ").Append(w.Em ? "ем…" : "хм").Append(" поки думає ").Append(Pct(w.Hm))
                .Append("\n  на твою відповідь у розмові (для всіх однаково): о! ").Append(Pct(ReactGlad)).Append(" · угу ").Append(Pct(ReactWarm)).Append(" · смішок ").Append(Pct(ReactLaugh)).Append(" · зітхання ").Append(Pct(ReactSour)).Append(" · хм? ").Append(Pct(ReactHurt)).Append(" · на прощання ").Append(Pct(ReactBye));
            sb.Append("\n  останні вигуки на цій грі (").Append(me._told.Count).Append("):");
            if (me._told.Count == 0) sb.Append(" жодного не просила, відколи ти в грі");
            for (int i = me._told.Count - 1; i >= 0; i--)
            {
                ToldOne t = me._told[i];
                sb.Append("\n    ").Append((Time.time - t.At).ToString("0")).Append(" с тому: ").Append(t.Sound.Substring(NpcTimbre.Prefix.Length)).Append(t.Why == null ? " — зіграно" : " — НЕ зіграно: " + t.Why);
            }
            string all = sb.ToString();
            Npcs.Log("[npc voice]\n" + all);
            return all;
        }
        private static string Pct(float k) { return Mathf.RoundToInt(Mathf.Clamp01(k) * 100f) + "%"; }

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
                if (_prevT < 0f && t > 0.3f) _prevT = t; // (it comes back into its line midway — the eye came near, a blow ended: what has passed is not said now, all at once)
                float voiced = 0f;
                for (int i = 0; i < p.From.Count && p.From[i] < t; i++) voiced = Mathf.Max(voiced, Mathf.Clamp01(Mathf.Min(t - p.From[i], p.To[i] - t) / 0.06f)); // (its words in order: none after this moment counts)
                float n0 = Bob * voiced * Mathf.Sin(t * p.Rate * Mathf.PI + p.Phase), r0 = 0f, a0 = 0f, l0 = 0f, b0 = 0f; // (once up and down every two syllables)
                for (int i = 0; i < p.Beats.Count; i++) n0 += Nod * p.BeatK[i] * Bump(t - p.Beats[i] + 0.12f, 0.12f, 0.3f);
                Ways mine = MyWays();
                for (int i = 0; i < p.Breaths.Count; i++)
                {
                    b0 += Breath * Bump(t - p.Breaths[i], 0.25f, 0.35f);
                    if (mine == null || _babbling || _prevT >= p.Breaths[i] || t < p.Breaths[i]) continue; // (hummed: no breath or cleared throat over the hum)
                    if (now - _soundAt < QuietAfter) continue; // (it has just made a sound — its «о!» to your answer, a word's own: no breath on top of it)
                    bool wrote = false;
                    foreach (KeyValuePair<float, string> sy in p.Says) if (Mathf.Abs(sy.Key - p.Breaths[i]) < 0.6f) wrote = true; // (the sentence opens with a sound of its own: that one)
                    if (wrote) continue;
                    if (i == 0 && p.From.Count >= KhmWords && Roll(p.Key, mine.Seed, 5u) < mine.Khm) Sound(SoundKhm); // (a long line: its throat cleared, not a breath)
                    else if (p.Words[i] >= BreathWords && now - _breathAt > BreathEvery && Roll(p.Key + (uint)i, mine.Seed, 6u) < mine.Breath) { Sound(SoundBreath); _breathAt = now; } // (before a long sentence, now and then)
                }
                if (!_babbling) foreach (KeyValuePair<float, string> sy in p.Says) if (_prevT < sy.Key && t >= sy.Key) Sound(sy.Value);
                foreach (Mark m in p.Marks)
                {
                    float x = t - m.T;
                    switch (m.Kind)
                    {
                        case '.': n0 += Dip * Bump(x, 0.2f, 0.5f); break;
                        case ',': n0 += Dip * 0.4f * Bump(x, 0.1f, 0.25f); break;
                        case '?': { float h = Held(x, 0.2f, 0.7f, 0.4f); r0 += Tilt * p.Side * h; n0 -= 1.5f * h; } break;
                        case '!': l0 += Lean * Bump(x + 0.3f, 0.2f, 0.4f); break;
                        case '~':
                            {
                                float h = Held(x, 0.3f, 0.5f, 0.5f); a0 += Aside * p.Side * h; n0 += 2f * h;
                                if (mine != null && !_babbling && _prevT < m.T + 0.15f && t >= m.T + 0.15f && Roll(p.Key + (uint)(m.T * 100f), mine.Seed, 7u) < mine.Sigh) Sound(SoundSigh); // (it trails off: a sigh)
                            }
                            break;
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
                if (_heardPrev < 0f && t > 0.3f) _heardPrev = t; // (the same for what it hears)
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
                }
                for (int i = 0; i < _reacts.Count; i++) if (_heardPrev < _reacts[i].Key && t >= _reacts[i].Key) Sound(_reacts[i].Value);
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

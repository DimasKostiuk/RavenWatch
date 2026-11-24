#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person speaking and listening, with no voice (the user 2026-11-09: «make talk look like talk without the voice package»;
    // the body has no jaw and no face bones, only its head, neck and chest). One place for every line a person says:
    //   over its head   NpcActor.Say → RPC_Say on every game (small talk, a scene, a greeting, one who butts in); for this game's
    //                   player alone — NpcActor.SayHere, no message (StoryRoad: a talk on the way, each player hears his own)
    //   in your talk    the talk window shows its lines one by one (RavenBookTalk → NpcTalk.LineShown → Window): one small message
    //                   to every game (NpcActor.Speak), no bubble, so those near see it speak to you
    // The line's words become its plan (Make): syllables at the person's own pace, its stressed words, a pause at each mark
    // (, . … ? !), a breath before each sentence. The plan moves the bodies:
    //   the speaker     its head and neck bob a little with its syllables (what a mouth would show from afar), nod on a stressed
    //                   word, dip at a full stop, tilt at a question, lean in at «!», look aside at «…», its chest rises with a breath;
    //                   its head turns to the one it speaks to (named; else who spoke to it last; else the nearest person; else a player near) —
    //                   unless that one stands past SpeakReach from where its body faces: then its body was turned elsewhere on purpose, the head stays
    //   the listeners   the nearest one or two people (and, in a scene, all its people however far: SceneOf; the hero's own lines
    //                   in a scene too: HeroSays) turn their heads to it while it speaks and ListenFor after; they nod at its full
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
    // Its head's own life in a talk (the user 2026-11-19: «оживити більше… щоб голови рухались гарніше або різноманітніше», Life):
    //   what its words mean   «ні», «ніколи», «нема» (and now and then a bare «не») — its head shaken; «так,», «авжеж», «гаразд.» — a
    //                         double nod; «мабуть», «здається», «не знаю» — its shoulders up and its head aside (Cue)
    //   its eyes              it does not stare: at a comma, a stop, now and then, a glance away and back — the timid oftener and
    //                         down, the bold seldom; never just before its own question (Glances); a listener glances away too
    //   variety               each mark's move is one of three (a stop: a dip · a chin lifted · a dip with a tilt; a question: a tilt ·
    //                         a tilt leaning in · a tilt chin up; «!»: a lean · a lean with a nod · a draw back first; «…»: aside ·
    //                         down · up and aside), picked by the line itself
    //   its manner            a timid one holds its chin lower, a bold one higher (Chin); a lively one moves more
    //   between the lines     while it looks at someone its head and chest drift a little, slowly (Drift, on the world's clock)
    //   a listener            a small nod at the speaker's comma now and then, leaning in through a long line
    // All of it from the same words, the same seed and the world's clock: the same on every game. Off: LifeOn.
    // Every game makes the same plan from the same words and the person's seed and finds the same people round it, so nothing is
    // sent but the line. Its own layer of the pose (NpcPose, after the gait): small turns over what the game, the clips and the
    // gait left on the bones; a clip that holds the head (BodyAnimator.HeadFree) wins over it. The game's own look-at does not
    // turn a sitting person's head (CharacterAnimEvent: no weight for a non-player that cannot move), and most talks sit: hence ours.
    // A dvergr's head is ours alone, standing too: the game's look is off on that body (Npcs.BuildDvergr — it aimed tens of degrees astray).
    // Hands: one gesture for a line with no mark of its own (Hands, asked by NpcBody.Spoken), empty-handed; on a bench — its talking hands
    // over its knees at a long line (sit_talk_hands); standing — by what the
    // line says: a hand waved off at a «no», its head scratched at a doubt, a fist in its palm at a firm «!», open arms at a question,
    // else its talking hands at a long line; one that calls for your ear («слухай», «зауваж») — a finger raised; a line that speaks of itself («я», «мені») — a hand to its chest, a short plain one — a palm
    // up, now and then; on a bench the seated ones of these; each through NpcMoves' gate (its body, what it shuns), never the same twice running.
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
        internal static float SpeakReach = 75f; // the one it speaks to farther aside than this from where its body faces: its head is not turned to it (a scene sends its lines with no
                                                // listener named: «npc lit say» to Eyvind had its head pulled toward the player beside it, 2026-11-20); one it listens to: 110
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
        // its head's own life in a talk (the header's «Life»): degrees, seconds, chances
        internal static bool LifeOn = true;
        internal static float Shake = 6f, ShakeFor = 0.62f, NotChance = 0.35f, CueGap = 1.4f; // a «no»: degrees each way · s · how often a bare «не» · s between two such moves
        internal static float DoubleNod = 5.2f, Shrug = 7f, ShrugTilt = 4f;                    // a «yes» · a doubt: its shoulders up, its head aside
        internal static float GlanceChance = 0.3f, GlanceYaw = 11f, GlanceDown = 4f, GlanceFor = 0.55f, GlanceQuiet = 1.2f, GlanceGap = 2f; // a glance away at a mark · degrees aside · down · s held · none this long before its question · s between two
        internal static float Chin = 2.5f;                                                     // degrees its chin is held down (timid) or up (bold) while it speaks
        internal static float Drift = 2.2f, DriftRate = 0.13f, Sway = 1.2f;                    // between the lines: degrees its head drifts · how fast · degrees its chest sways
        internal static float HeedComma = 0.3f, HeedLean = 2f, HeedLong = 4f, HeedGlance = 0.2f; // a listener: a nod at a comma (chance) · degrees it leans in · through a line this long (s) · a glance away at a stop (chance)
        internal static int HandsSmall = 3;                                                    // words: a line this long may get a small gesture for what it says
        internal static float HandsSmallChance = 0.45f;                                        // × (0.4 + chattiness) × liveliness
        internal const string SoundBreath = "speech_breath", SoundHm = "speech_hm", SoundEm = "speech_em", SoundUhu = "speech_uhu", SoundOh = "speech_oh", SoundHmAsk = "speech_hm_ask",
            SoundSigh = "speech_sigh", SoundKhm = "speech_khm", SoundChuckle = "speech_chuckle";
        internal const string ThinkClip = "think_chin", ListenClip = "listen_chin.listen", ListenSitClip = "listen_sit_chin.listen"; // (standing · on a bench: its own body there; on the ground — none)
        internal const string HandsClip = "talk_hands", AskHandsClip = "arms_open"; // (its hands with a long line · with a question or a trailing one)
        internal const string HandsSitClip = "sit_talk_hands"; // (… on a bench: the user saw it on the mannequin 2026-11-19)
        // the small gestures of one hand (the user took each on the mannequin, 2026-11-19): a palm up — «ну», «от» —, a hand to the chest — «я»,
        // «мені» —, standing and on a bench; each list has another to take its turn (NpcMoves.Pick: never the same twice running)
        private const string SmallHands = "palm_up", SelfHands = "hand_chest|palm_up";
        // a finger raised — «слухай», «зауваж» (the user 2026-11-19: «з пальцем прикольна анімація… вставляй це у гру»): standing and on a bench, and not by a
        // timid one (bravery under HeedBold) — a teaching gesture; a ghost and the Wanderer never make it (their templates' «never:»)
        private const string HeedHands = "finger_up", SitHeedHands = "sit_finger_up"; // (… and on a bench: the user took it on the mannequin too)
        internal static float HeedBold = 0.4f;
        private const string SitLongHands = "sit_talk_hands|sit_palm_up", SitSmallHands = "sit_palm_up", SitSelfHands = "sit_hand_chest|sit_palm_up";
        internal static float SelfChance = 0.5f, PlainChance = 0.3f; // of a line that speaks of itself · of a short line that says nothing of these (each × its small-gesture chance)
        internal const string NoHandsClip = "wave_off", DoubtHandsClip = "scratch_head", FirmHandsClip = "fist_palm"; // (a «no» · a doubt · a firm «!»)
        private const string NoHands = NoHandsClip + "|shrug_small", DoubtHands = DoubtHandsClip + "|shrug_small", FirmHands = FirmHandsClip + "|" + HandsClip; // (each with another to take its turn: NpcMoves.Pick never makes the same one twice running)
        internal static readonly string[] TalkClips = { HandsClip, AskHandsClip, ThinkClip, ListenClip, ListenSitClip }; // (what a talk's own body does: they fit a talk, NpcMoves.TalkFits — the hands for what a line says are not of them: the line asks for those itself, its own life may not make them in a talk)
        internal static float ListenPoseMin = 2.5f, ListenPoseChance = 0.35f, ThinkPoseChance = 0.5f, ThinkPoseFrom = 0.7f, PoseEvery = 25f; // s of a line · × (0.5 + curiosity) · the same · s of thinking · s

        // ---------------------------------------------------------------- one line's plan
        private struct Mark { public float T; public char Kind; public byte V; } // '.', ',', '?', '!', '~' (… trailing off); V: which of its three moves · a cue: 'n' a no, 'y' a yes, 'd' a doubt
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
            public readonly List<Mark> Cues = new List<Mark>();         // (what its words mean: a no, a yes, a doubt)
            public readonly List<Vector4> Glances = new List<Vector4>(); // (when · how long · aside, signed · down, signed)
            public float ChinDown;                                       // (-1 bold … 1 timid)
            public char Says1;                                           // (what the line mostly says, for its hands: 'n', 'd', 0)
        }

        // what a word means for the head (0: nothing): a no, a yes (only where it stands by itself, with its mark: «Так,» — not «так само»),
        // a doubt; «не знаю» is a doubt, not a no
        private static char Cue(string core, bool marked, string prev, ref uint r)
        {
            switch (core)
            {
                case "ні": case "ніколи": case "нізащо": case "нема": case "немає": case "ніхто": case "нічого": case "ніщо": case "жоден": case "жодного": case "жодна":
                case "no": case "never": case "nothing": case "nobody": case "none": case "nope": return 'n';
                case "не": case "not": return Rnd(ref r) < NotChance ? 'n' : '\0';
                case "так": case "авжеж": case "атож": case "еге": case "гаразд": case "добре": case "згода": case "звісно": case "звичайно":
                case "yes": case "aye": case "sure": case "indeed": case "agreed": case "certainly": case "right": return marked ? 'y' : '\0';
                case "мабуть": case "можливо": case "напевно": case "здається": case "хтозна": case "навряд": case "певно":
                case "maybe": case "perhaps": case "probably": case "dunno": case "seems": return 'd';
                case "може": return marked ? 'd' : '\0';
                case "знаю": case "know": return prev == "не" || prev == "don't" || prev == "dont" || prev == "not" ? 'd' : '\0';
            }
            return '\0';
        }
        // a word that calls for the other's ear or teaches: «слухай», «зауваж», «запам'ятай», «знай»
        private static bool Calls(string core)
        {
            switch (core)
            {
                case "слухай": case "слухайте": case "послухай": case "послухайте": case "зауваж": case "зауважте": case "запам'ятай": case "запам'ятайте": case "запам’ятай": case "запам’ятайте":
                case "дивись": case "дивіться": case "знай": case "знайте": case "затям": case "затямте":
                case "listen": case "mind": case "remember": case "mark": case "heed": return true;
            }
            return false;
        }
        // a word by which one speaks of oneself
        private static bool Self(string core)
        {
            switch (core)
            {
                case "я": case "мені": case "мене": case "мною": case "мій": case "моя": case "моє": case "мої": case "мого": case "моєї": case "моїх":
                case "i": case "me": case "my": case "mine": case "myself": return true;
            }
            return false;
        }
        private static readonly char[] WordEnds = { '.', ',', '!', '?', '…', ':', ';', '»', '"', '”', ')', '-', '—' }, WordStarts = { '«', '"', '\'', '(', '„', '“' };

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
        internal static string Plain(string text) { return Clean(text).Trim(); } // (the words alone: no rich text, no «{…}»)
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
        private static Plan PlanFor(NpcActor a, string text) { NpcPersona w = NpcPersona.Of(a); return Make(text, SeedOf(a), w.Chatty, RavenSpeech.Seconds(text, RavenSpeech.Pace.Bubble), 1f - w.Bravery, w.Lively); }

        // how long the one spoken to thinks after this line before it answers: the same on every game (NpcChatter waits it out)
        internal static float Think(string text) { return ThinkClean(Clean(text)); }
        private static float ThinkClean(string t) { uint r = (uint)Hash(t) ^ 0x9E3779B9u; return Mathf.Lerp(ThinkMin, ThinkMax, Rnd(ref r)); }

        // its plan: the same words and the same person give the same plan on every game
        // timid: 0 bold … 1 timid (its glances, its chin); lively: its liveliness (how much it moves) — one who is no person of ours (the hero): the middle
        private static Plan Make(string text, uint seed, float chatty, float most, float timid = 0.5f, float lively = 1f)
        {
            string t = Clean(text);
            uint r = seed ^ (uint)Hash(t);
            Plan p = new Plan { Rate = Syllables * Mathf.Lerp(0.85f, 1.15f, chatty), Phase = Rnd(ref r) * 6.283f, Side = Rnd(ref r) < 0.5f ? -1f : 1f, Strength = Mathf.Lerp(0.75f, 1.25f, chatty) * Mathf.Clamp(0.75f + 0.25f * lively, 0.8f, 1.1f), Think = ThinkClean(t), Text = text, Key = (uint)Hash(t) };
            p.Thinks = Rnd(ref r) < ThinkChance;
            p.ChinDown = Mathf.Lerp(-1f, 1f, Mathf.Clamp01(timid));
            float per = 1f / p.Rate, now = 0.25f;
            bool first = true;
            string prev = ""; float cueAt = -99f; int nos = 0, doubts = 0, selfs = 0, heeds = 0;
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
                    {
                        // what the word means: a no, a yes, a doubt (Cue) — not two such moves on top of each other, but a doubt takes a «не» back
                        string lw = word.TrimStart(WordStarts).ToLowerInvariant(), core = lw.TrimEnd(WordEnds);
                        char cue = Cue(core, core.Length < lw.Length, prev, ref r);
                        prev = core;
                        if (Self(core)) selfs++; // (it speaks of itself: a hand to its chest may go with the line — Hands)
                        if (Calls(core)) heeds++; // (it calls for the other's ear: a finger raised may go with it)
                        if (cue == 'd' && p.Cues.Count > 0 && p.Cues[p.Cues.Count - 1].Kind == 'n' && now - p.Cues[p.Cues.Count - 1].T < 1f) { p.Cues.RemoveAt(p.Cues.Count - 1); nos--; cueAt = -99f; }
                        if (cue != '\0' && now - cueAt >= CueGap) { p.Cues.Add(new Mark { T = now + w * 0.3f, Kind = cue }); cueAt = now; if (cue == 'n') nos++; else if (cue == 'd') doubts++; }
                    }
                    if (first || (v >= 3 && Rnd(ref r) < 0.6f) || word.IndexOf('!') >= 0) { p.Beats.Add(now + w * 0.45f); p.BeatK.Add(word.IndexOf('!') >= 0 ? 1.3f : 0.7f + Rnd(ref r) * 0.3f); }
                    p.From.Add(now); p.To.Add(now + w);
                    now += w;
                    first = false;
                }
                string bare = word.TrimEnd('»', '"', '\'', ')', '”', '“');
                char end = bare.Length > 0 ? bare[bare.Length - 1] : ' '; // (a sentence closed in quotes: «…кажу.» ends at its stop)
                if (word.IndexOf('…') >= 0) { p.Marks.Add(new Mark { T = now, Kind = '~', V = Which(ref r) }); now += TrailPause; first = true; }
                else if (word.IndexOf('?') >= 0) { p.Marks.Add(new Mark { T = now, Kind = '?', V = Which(ref r) }); now += StopPause; first = true; }
                else if (end == '!') { p.Marks.Add(new Mark { T = now, Kind = '!', V = Which(ref r) }); now += StopPause; first = true; }
                else if (end == '.') { p.Marks.Add(new Mark { T = now, Kind = '.', V = Which(ref r) }); now += StopPause; first = true; }
                else if (end == ',' || end == ';' || end == ':' || word == "—" || word == "-") { p.Marks.Add(new Mark { T = now, Kind = ',' }); now += CommaPause; }
                else now += per * 0.25f; // (between two words)
            }
            p.Length = Mathf.Min(now + 0.2f, most);
            p.Says1 = nos > 0 && nos >= doubts ? 'n' : doubts > 0 ? 'd' : heeds > 0 ? 'a' : selfs > 0 ? 'i' : '\0';
            // its eyes: a glance away at a comma or a stop within the line, now and then — the timid oftener; none just before its own question
            // (one who asks looks at whom it asks), none right after another
            float often = GlanceChance * Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(timid)), last = -99f;
            for (int i = 0; i < p.Marks.Count - 1; i++)
            {
                Mark m = p.Marks[i];
                if ((m.Kind != ',' && m.Kind != '.') || Rnd(ref r) >= often || m.T - last < GlanceGap) continue;
                bool asks = false;
                for (int j = i + 1; j < p.Marks.Count; j++) if (p.Marks[j].Kind == '?' && p.Marks[j].T - m.T < GlanceQuiet + GlanceFor) asks = true;
                if (asks) continue;
                float down = timid > 0.6f ? 1f : Rnd(ref r) < 0.3f ? -0.5f : 0.3f; // (the timid look down; others aside, now and then up)
                p.Glances.Add(new Vector4(m.T + 0.1f, GlanceFor * (0.8f + 0.6f * Rnd(ref r)), Rnd(ref r) < 0.5f ? -1f : 1f, down));
                last = m.T;
            }
            return p;
        }

        private static byte Which(ref uint r) { return (byte)Mathf.Min(2, (int)(Rnd(ref r) * 3f)); } // (which of a mark's three moves)
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

        // a person says this line, to «to» when it is known (your talk), else to whoever it finds near. talk: a line of your talk window —
        // its plan (and its hum) about as long as it is read there (RavenSpeech.Pace.TalkHum), not as long as it would be said aloud
        internal static void Said(NpcActor a, string text, Character to, bool talk = false)
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
            long scene = SceneOf != null ? SceneOf(a) : 0L;
            if (scene != 0L) TurnToSpeaker(a, c, scene, (talk ? RavenSpeech.Seconds(text, RavenSpeech.Pace.TalkHum) : RavenSpeech.Seconds(text, RavenSpeech.Pace.Scene)) + 1.5f); // (before the eye's look below: who turns is not this game's camera's business)
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - at).sqrMagnitude > SeenNear * SeenNear) return; // (nobody here sees it: its plan not even made)
            Plan plan = talk ? Make(text, SeedOf(a), NpcPersona.Of(a).Chatty, RavenSpeech.Seconds(text, RavenSpeech.Pace.TalkHum), 1f - NpcPersona.Of(a).Bravery, NpcPersona.Of(a).Lively) : PlanFor(a, text);
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
            if (to == null && scene != 0L) to = Faced(a, c, scene); // (a scene's line: to the one the scene turned it to — before «who spoke last»)
            if (to == null) to = back;
            if (to == null && near != null) to = near.GetComponent<Character>();
            if (to == null) { Player p = Player.GetClosestPlayer(at, ListenNear); if (p != null) to = p; }
            float until = now + plan.Length;
            me.LookAt(to, until + 1.5f, true);
            Character addressed = to;
            // the one it speaks to always hears it, nearest or not (by a fire they sit across it, up to 5 m: 2026-11-09, «heard by -»)
            NpcActor toPerson = to != null ? NpcActor.Of(to) : null;
            NpcSpeech toEar = toPerson != null && Free(toPerson) ? toPerson.Speech : null;
            if (toEar != null && toEar != l1 && toEar != l2) { l2 = l1; l1 = toEar; near = toPerson; }
            if (l1 != null) l1.Hear(c, plan, now, l1.Body == addressed);
            if (l2 != null) l2.Hear(c, plan, now, l2.Body == addressed);
            if (scene != 0L) foreach (NpcSpeech w in InScene(scene, a)) if (w != l1 && w != l2) w.Hear(c, plan, now, w.Body == addressed); // (its scene's people, however far)
            Pose(a, c, near, to, plan);
            if (Trace) Npcs.Log(a.Id + " speaks " + plan.Length.ToString("0.0") + " s: " + plan.From.Count + " words, " + plan.Beats.Count + " beats, marks «" + Marks(plan) + "», to " + (to != null ? to.GetHoverName() : "-") + ", heard by " + (l1 != null ? l1.name : "-") + (l2 != null ? ", " + l2.name : "") + (plan.Thinks ? ", thinks " + plan.Think.ToString("0.0") + " s" : ""));
        }
        // A scene's people hear every line said in it (the user 2026-11-18: Eyvind on his bench, 10 m off the fire in hall_how, looked nowhere while
        // the others spoke — only the nearest one or two within ListenNear heard a line): whose scene holds a person now (the story fills it with its
        // lock, StoryScenes.Holder — the player whose scene it is; 0 none; null in a build with no story). They hear one another, and the hero's
        // lines (HeroSaid), at any distance, as a listener ever does: the head to the speaker, its nods and sounds
        internal static System.Func<NpcActor, long> SceneOf;

        // A scene's line, from its first word (the user 2026-11-23: «бачу, як персонажі і наш повертаються до того, хто говорив, надто пізно, ніби
        // після того, як той договорив… якщо хтось до когось звертається, по можливості відразу повертатись до того, хто говорить»): every person
        // of the scene who stands free (no walk, no seat, no clip or work on its body: Working — a scene's «busy» between its rounds too) and has the
        // speaker farther than TurnPast deg off its front turns its body to it at once, for the line and a moment after, then back to what the scene
        // had it face (the order «hear»: NpcMind.Hear decides where the person is run); within that, its head alone, as before. Told by the
        // scene's director's game alone (scene = his player's id: his game plays it and knows its works), sent to each one's owner (NpcActor.Order).
        // The hero: HeroHears (StoryLook — his head at once, his body when the speaker is behind him)
        internal static float TurnPast = 60f;
        internal static System.Action<Character, long> HeroHears;
        internal static System.Func<NpcActor, bool> Working; // (the story: a scene's «busy» on it)
        private static void TurnToSpeaker(NpcActor speaker, Character c, long scene, float seconds)
        {
            Player me = Player.m_localPlayer;
            if (c == null || me == null || me.GetPlayerID() != scene) return;
            string order = "hear " + NpcKit.Ref(c.GetZDOID()) + " " + seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            foreach (NpcActor o in NpcActor.Live) // heavy: once a line of a scene, the people loaded here (its own scene's few are told)
            {
                if (o == null || o == speaker || SceneOf(o) != scene || (Working != null && Working(o))) continue;
                Vector3 to = c.transform.position - o.transform.position; to.y = 0f;
                if (to.sqrMagnitude < 0.04f || Vector3.Angle(o.transform.forward, to) < TurnPast) continue; // (a first look here; its owner looks again: NpcMind.Hear)
                o.Order(order, NpcMind.Layer.Scene);
            }
            if (HeroHears != null) HeroHears(c, scene);
        }

        // whom a scene's line is said to: of its people and its player, the one nearest the way the speaker's body faces (the scene turned it
        // there: «face»), within half of SpeakReach; none — null (then as ever: who spoke to it last, the nearest). Read from bodies: the same on every game
        private static Character Faced(NpcActor a, Character c, long scene)
        {
            Vector3 fwd = c.transform.forward; fwd.y = 0f;
            Character best = null; float bestDeg = SpeakReach * 0.5f;
            foreach (NpcActor o in NpcActor.Live) // heavy: once a line of a scene (the same pass as InScene's)
            {
                if (o == null || o == a || SceneOf(o) != scene || o.Speech == null) continue;
                Character oc = o.Speech.Body;
                if (oc == null) continue;
                Vector3 d = oc.transform.position - c.transform.position; d.y = 0f;
                float deg = Vector3.Angle(fwd, d);
                if (d.sqrMagnitude > 0.04f && deg < bestDeg) { bestDeg = deg; best = oc; }
            }
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null || pl.GetPlayerID() != scene) continue;
                Vector3 d = pl.transform.position - c.transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.04f && Vector3.Angle(fwd, d) < bestDeg) best = pl;
            }
            return best;
        }
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

        // The hero says a line aloud — the ONE way for both (the review 2026-11-18: two of them, each with its own length and tag stripping): a
        // scene's step «player say», and your answer in a talk (NpcTalk.Pick). Hummed in his own voice, the «hero» timbre of his sex
        // (NpcTimbre.OfHero, NpcBabble: clip — the name it goes by in the sound core, to be cut short), as long as RavenSpeech says for its kind;
        // the people of his scene hear it as a person's line — the head to him, the nods at its stops — and a reply looks back at him (Lately);
        // not in a scene: the nearest one or two. On his game (a scene: the director's — those near see the subtitle, not the heads). The
        // seconds it is hummed (0: not hummed)
        // scene: said in a scene — hummed quieter than his answer in a plain talk (NpcBabble.VolOf)
        internal static float HeroSays(Player p, string text, float seconds, string clip, uint salt, bool scene = false)
        {
            if (p == null || string.IsNullOrEmpty(text)) return 0f;
            string said = Plain(text);
            long id = p.GetPlayerID();
            float hum = said.Length > 0 ? NpcBabble.Say(NpcTimbre.OfHero(p), HuginWhispers.Body.HeadBone(p), said, (uint)id ^ (uint)(id >> 32) ^ salt, seconds, NpcBabble.VolOf(true, scene), clip) : 0f;
            HeroSaid(p, said, seconds);
            return hum;
        }
        private static void HeroSaid(Player p, string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Vector3 at = p.transform.position;
            float now = Time.time;
            Lately.Add(new Spoke { Who = p, At = at, When = now });
            if (Lately.Count > 32) Lately.RemoveAt(0);
            long id = p.GetPlayerID();
            Plan plan = Make(text, (uint)id ^ (uint)(id >> 32), 1f, seconds);
            List<NpcSpeech> heard = InScene(id, null); // (its list, filled anew: no list a line)
            if (heard.Count == 0)
                foreach (NpcActor o in NpcActor.Live) // heavy: once a line of his, the people loaded here
                {
                    if (o == null || (o.transform.position - at).sqrMagnitude >= ListenNear * ListenNear) continue;
                    NpcSpeech os = o.Speech;
                    if (os != null && Free(o)) heard.Add(os);
                    if (heard.Count >= 2) break;
                }
            foreach (NpcSpeech w in heard) w.Hear(p, plan, now, false);
            TurnToSpeaker(null, p, id, seconds + 1.5f); // (his scene's people turn to him at his first word too: his own game is his scene's director)
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

        // what the line mostly says ('n' a no, 'd' a doubt, 'a' a call for the ear, 'i' of itself, 0): the plan it speaks by, if this line's is
        // made already — else made for this (once a line either way)
        private static char Says(NpcActor a, string text)
        {
            NpcSpeech me = a.Speech;
            return me != null && me._plan != null && me._plan.Text == text ? me._plan.Says1 : PlanFor(a, text).Says1;
        }

        // a long line with no gesture of its own: its hands, now and then (NpcBody.Spoken, on the game that says it); "" — none
        internal static string Hands(NpcActor a, string text)
        {
            if (a == null || string.IsNullOrEmpty(text)) return "";
            int words = Clean(text).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            if (words < (LifeOn ? HandsSmall : HandsWords)) return "";
            if (NpcMind.LayerNow(a) == NpcMind.Layer.Scene || NpcGear.HandsOf(a) != NpcGear.HandsFree) return ""; // (a scene says its own; a weapon: no)
            NpcBody b = a.Body;
            if (b == null || !b.ClipFree) return "";
            NpcPersona w = NpcPersona.Of(a);
            float small = HandsSmallChance * (0.4f + w.Chatty) * w.Lively;
            if (NpcMind.SeatedOf(a)) // (seated: on a bench only — most talks sit; on the ground, in a bed: none yet)
            {
                if (!OnBench(a)) return "";
                if (!LifeOn) return words >= HandsWords && Random.value <= HandsChance * (0.4f + w.Chatty) * w.Lively ? NpcMoves.Clip(a, HandsSitClip, true) : "";
                string sit = "";
                char said = Says(a, text);
                if (said == 'a' && w.Bravery >= HeedBold && Random.value < small) sit = SitHeedHands;                    // (it calls for your ear: a finger raised)
                else if (said == 'i' && Random.value < small * SelfChance) sit = SitSelfHands;                           // (of itself: a hand to its chest)
                else if (words >= HandsWords) { if (Random.value <= HandsChance * (0.4f + w.Chatty) * w.Lively) sit = SitLongHands; } // (a long line: its talking hands, or a palm)
                else if (Random.value < small * PlainChance) sit = SitSmallHands;                                        // (a short one: a palm up, now and then)
                return sit.Length > 0 ? NpcMoves.Pick(a, sit, false, false, true) : ""; // (asked: the line asks for it; its gate, never the same twice running)
            }
            string t = text.TrimEnd();
            // what the line says picks its hands (the header's «Hands»): a «no» · a doubt · a firm «!» — a short line may have these; a
            // question's open arms and its talking hands want a long one, as before
            string h = "";
            if (LifeOn)
            {
                char says = Says(a, text); // (the same reading of its words its head goes by)
                if (says == 'n') h = NoHands; else if (says == 'd') h = DoubtHands; else if (t.EndsWith("!") && words >= 4) h = FirmHands;
                if (h.Length > 0 && Random.value > small) h = "";
                if (h.Length == 0 && says == 'a' && w.Bravery >= HeedBold && Random.value < small) h = HeedHands;            // (it calls for your ear: a finger raised)
                if (h.Length == 0 && says == 'i' && Random.value < small * SelfChance) h = SelfHands;                     // (of itself: a hand to its chest)
                if (h.Length == 0 && says == '\0' && words < HandsWords && Random.value < small * PlainChance) h = SmallHands; // (a short line that says none of these: a palm up, now and then)
                if (h.Length > 0) { string own = NpcMoves.Pick(a, h, false, false, true); if (own.Length > 0) return own; } // (its gate — its body, what it shuns; asked: the line itself asks for it; not the same one twice running)
            }
            if (words < HandsWords || Random.value > HandsChance * (0.4f + w.Chatty) * w.Lively) return "";
            h = t.EndsWith("?") || t.EndsWith("…") || t.EndsWith("...") ? AskHandsClip : HandsClip;
            return NpcMoves.Clip(a, h); // (its gate: its body's own of it, not one it never makes)
        }

        // ---------------------------------------------------------------- its body
        private Plan _plan; private float _start, _prevT = -1f;       // (what it says)
        private Plan _heard; private float _heardStart; private bool _addressed, _heardCut; private float _heardPrev = -1f; // (what it hears)
        private Character _to; private float _toUntil; private bool _toMine; // (_toMine: the one it speaks to, not the one it listens to)
        private float _yaw, _pitch, _aimOn, _on, _ear;
        private Humanoid _h;
        private Animator _anim;
        private Transform _spine2, _neck, _head, _lSh, _rSh, _lArm, _rArm; // (its shoulders and upper arms: a doubt's shrug)
        private bool _armsFree = true; private float _lifeSeed = -1f;
        private Quaternion _wrote; private bool _wroteOn;
        private NpcTimbre.Card _card; private Ways _ways; // (its timbre and its ways with its sounds: worked out once a line it says or hears — a kept timbre that had not reached this game
                                                          // yet, a chance tuned by the author, its ways changed: all right from the next line on)
        private readonly List<KeyValuePair<float, string>> _reacts = new List<KeyValuePair<float, string>>(); // (what it says to the line it hears: when, which sound)
        private NpcActor _me; private float _stateAt; private bool _upright = true; private float _seatedK = 1f; // (its ZDO read four times a second, not each frame)
        internal static float StateEvery = 0.25f;

        internal Humanoid Body { get { if (_h == null) _h = GetComponent<Humanoid>(); return _h; } } // (asked before its first frame of speech too)

        private void LookAt(Character who, float until, bool mine = false)
        {
            if (who == null || who == Body) return;
            _to = who; _toUntil = until; _toMine = mine;
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
                _lSh = BodyClips.SkeletonBone(this, v, "LeftShoulder"); _rSh = BodyClips.SkeletonBone(this, v, "RightShoulder");
                _lArm = BodyClips.SkeletonBone(this, v, "LeftArm"); _rArm = BodyClips.SkeletonBone(this, v, "RightArm");
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

        // the line as a wordless hum in its own voice (NpcBabble); false: not hummed (no voice for it, the voice package speaks, its files on their way).
        // Its hum goes by a name of its own person in the sound core (said to you: TalkClip, else «babble» — and the person's number). One mouth:
        // a line it begins cuts its own hum still going (the user 2026-11-18 evening: the talk window comes at reading pace now, no longer waiting
        // for a hum — a hum never lies over its own last one), and your answer cuts what it
        // still hums to you (HushTalk)
        internal const string TalkClip = "babble_talk";
        private bool _babbling;
        private string _hum; // (the name its last hum goes by; null — none yet)
        private bool Babble(NpcActor a, string text, float seconds, Character to)
        {
            Ways w = MyWays();
            if (_head == null || w == null || _card == null) return false;
            bool toYou = to != null && to == Player.m_localPlayer;
            if (_hum != null) NpcBabble.Hush(_hum); // (its own last hum, if it still goes: eased off — nothing when it is over)
            _hum = (toYou ? TalkClip : "babble") + "." + SeedOf(a);
            bool scene = SceneOf != null && SceneOf(a) != 0L; // (a line said in a scene: quieter than a plain talk — NpcBabble.VolOf)
            return NpcBabble.Say(_card, _head, text, SeedOf(a), seconds, NpcBabble.VolOf(toYou, scene), _hum) > 0f;
        }
        internal static void HushTalk(NpcActor a) { NpcSpeech me = a != null ? a.Speech : null; if (me != null && me._hum != null) NpcBabble.Hush(me._hum); }

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
            if (now >= _stateAt) { _stateAt = now + StateEvery; _upright = me == null || Free(me); _seatedK = NpcMind.SeatedOf(me) ? 0.85f : 1f; _armsFree = me == null || me.Body == null || me.Body.ClipFree; }
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
                if (Mathf.Abs(yaw) < (_toMine ? SpeakReach : 110f)) // (the one it speaks to past its neck: its body was turned elsewhere on purpose — a scene's «face», its mind — and its head stays with it)
                {
                    aim = true;
                    yawWant = Mathf.Clamp(yaw, -LookYaw, LookYaw);
                    pitchWant = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) * Mathf.Rad2Deg - Down() * Lift, -LookPitch - 25f, LookPitch); // (its head bowed by its carriage: lifted to the face)
                }
            }
            float k = 1f - Mathf.Exp(-dt / LookEase);
            _aimOn = Mathf.MoveTowards(_aimOn, aim ? 1f : 0f, dt * 2f);
            if (aim) { _yaw = Mathf.Lerp(_yaw, yawWant, k); _pitch = Mathf.Lerp(_pitch, pitchWant, k); }

            float nod = 0f, roll = 0f, aside = 0f, lean = 0f, breath = 0f, shrug = 0f, sway = 0f;
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
                        // (each mark's move is one of three — m.V, the line's own pick; Life off: the first, as it was)
                        case '.':
                            {
                                int v = LifeOn ? m.V : 0;
                                if (v == 1) n0 -= Dip * 0.55f * Bump(x, 0.18f, 0.45f);                                       // (its chin lifted)
                                else { float d = Bump(x, 0.2f, 0.5f); n0 += Dip * d; if (v == 2) r0 += Tilt * 0.35f * p.Side * d; } // (a dip · a dip with a tilt)
                            }
                            break;
                        case ',': n0 += Dip * 0.4f * Bump(x, 0.1f, 0.25f); break;
                        case '?':
                            {
                                float h = Held(x, 0.2f, 0.7f, 0.4f); r0 += Tilt * p.Side * h; n0 -= 1.5f * h;
                                if (LifeOn && m.V == 1) l0 += Lean * 0.6f * h;        // (leaning in to its question)
                                else if (LifeOn && m.V == 2) n0 -= 2.5f * h;         // (its chin up with it)
                            }
                            break;
                        case '!':
                            l0 += Lean * Bump(x + 0.3f, 0.2f, 0.4f);
                            if (LifeOn && m.V == 1) n0 += Nod * Bump(x + 0.1f, 0.1f, 0.25f);          // (a nod with it)
                            else if (LifeOn && m.V == 2) l0 -= Lean * 0.5f * Bump(x + 0.6f, 0.12f, 0.2f); // (drawn back a moment first)
                            break;
                        case '~':
                            {
                                float h = Held(x, 0.3f, 0.5f, 0.5f);
                                int v = LifeOn ? m.V : 0;
                                if (v == 1) { a0 += Aside * 0.3f * p.Side * h; n0 += 6f * h; }      // (down, to itself)
                                else if (v == 2) { a0 += Aside * p.Side * h; n0 -= 3f * h; }        // (up and aside)
                                else { a0 += Aside * p.Side * h; n0 += 2f * h; }
                                if (mine != null && !_babbling && _prevT < m.T + 0.15f && t >= m.T + 0.15f && Roll(p.Key + (uint)(m.T * 100f), mine.Seed, 7u) < mine.Sigh) Sound(SoundSigh); // (it trails off: a sigh)
                            }
                            break;
                    }
                }
                if (LifeOn)
                {
                    float sh0 = 0f;
                    foreach (Mark c in p.Cues) // (what its words mean)
                    {
                        float x = t - c.T;
                        if (x <= 0f || x > 1.5f) continue;
                        switch (c.Kind)
                        {
                            case 'n': a0 += Shake * Bump(x, 0.12f, ShakeFor) * Mathf.Sin(x * 22f); break; // (a no: twice each way)
                            case 'y': n0 += DoubleNod * (Bump(x, 0.1f, 0.17f) + 0.75f * Bump(x - 0.3f, 0.1f, 0.2f)); break;
                            case 'd': { float h = Held(x, 0.2f, 0.45f, 0.4f); sh0 += h; r0 += ShrugTilt * p.Side * h; n0 += 1.5f * h; } break;
                        }
                    }
                    foreach (Vector4 g in p.Glances) // (its eyes away and back)
                    {
                        float x = t - g.x;
                        if (x <= 0f || x > g.y + 0.6f) continue;
                        float h = Held(x, 0.18f, g.y, 0.28f);
                        a0 += GlanceYaw * g.z * h; n0 += GlanceDown * g.w * h;
                    }
                    n0 += Chin * p.ChinDown; // (its manner: a timid chin down, a bold one up, while it speaks)
                    shrug += sh0 * s;
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
                float n0 = 0f, r0 = 0f, l0 = 0f, think = 0f, a1 = 0f;
                Ways heard = LifeOn ? MyWays() : null;
                for (int mi = 0; mi < p.Marks.Count; mi++)
                {
                    Mark m = p.Marks[mi];
                    float x = t - m.T - 0.2f; // (a beat after it: it heard it)
                    switch (m.Kind)
                    {
                        case '.':
                            n0 += Dip * Bump(x, 0.18f, 0.4f);
                            if (heard != null && mi < p.Marks.Count - 1 && Roll(p.Key + (uint)mi, heard.Seed, 12u) < HeedGlance) a1 += GlanceYaw * 0.8f * -p.Side * Held(x - 0.3f, 0.2f, 0.5f, 0.3f); // (its own eyes away a moment, mid-line)
                            break;
                        case ',': if (heard != null && Roll(p.Key + (uint)mi, heard.Seed, 11u) < HeedComma) n0 += Dip * 0.45f * Bump(x, 0.1f, 0.22f); break; // (a small nod: it follows)
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
                nod += n0 * s - 2f * think * _ear; roll += r0 * s; lean += l0 * s; aside += ThinkAside * -p.Side * think * _ear + a1 * _ear * seated;
                if (LifeOn && p.Length > HeedLong) lean += HeedLean * Held(t - 1f, 0.8f, p.Length - 2f, 0.6f) * _ear * seated; // (a long line: it leans in to it)
            }
            // between the lines, and under them: while it looks at someone its head and chest drift a little, slowly — on the world's clock
            // and its own number, so every game shows the same; calmer under its own speaking and listening, which move it already
            if (LifeOn && _aimOn > 0f)
            {
                if (_lifeSeed < 0f) { Ways mw = MyWays(); _lifeSeed = mw != null ? mw.Seed % 97u : 0f; }
                float ck = (float)(SitNet.Seconds % 4096.0) * DriftRate, calm = _aimOn * (1f - 0.5f * Mathf.Max(_on, _ear));
                aside += (Mathf.PerlinNoise(ck, _lifeSeed) - 0.5f) * 2f * Drift * calm;
                nod += (Mathf.PerlinNoise(_lifeSeed, ck * 0.8f) - 0.5f) * 1.2f * Drift * calm;
                roll += (Mathf.PerlinNoise(ck * 0.7f + 31f, _lifeSeed + 7f) - 0.5f) * Drift * calm;
                sway = (Mathf.PerlinNoise(ck * 0.5f + 11f, _lifeSeed + 3f) - 0.5f) * 2f * Sway * _aimOn;
            }

            float yawNow = _yaw * _aimOn, pitchNow = _pitch * _aimOn;
            BodyGait.Turn(_spine2, body, (lean - breath) * w, yawNow * 0.15f * w, sway * w); // (not Spine1: the gait knows by it whether the game posed the bones)
            if (shrug > 0.01f && _armsFree) // (a doubt: its shoulders up, its arms left hanging — as the gait lifts them, BodyGait.Pose; not under a clip of its arms)
            {
                float up = Shrug * shrug * w;
                BodyGait.Turn(_lSh, body, 0f, 0f, -up); BodyGait.Turn(_rSh, body, 0f, 0f, up);
                BodyGait.Turn(_lArm, body, 0f, 0f, up); BodyGait.Turn(_rArm, body, 0f, 0f, -up);
            }
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
            return _game != null && _game.m_headRotation && FHeadDir != null ? (Vector3)FHeadDir.GetValue(_game) : Vector3.zero; // (its look off — the dvergr, Npcs.BuildDvergr: the field keeps the way it faced when made, not a look)
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

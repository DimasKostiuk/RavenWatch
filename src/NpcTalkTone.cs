#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;

namespace HuginWhispers
{
    // How you answer what a person says (the user 2026-11-06: an answer must weigh something, and you may choose to be rude on purpose). An
    // answer's deed «!tone warm|cold|rude [weight]» in a talk; an answer with none is plain and costs nothing.
    //   warm [soft|blunt|joke]   (the air: see Missed below — how this person takes it)
    //   warm   you took its words kindly: more patience and, once a game day a person (ToneRow), warmer to you. A worry's own warm answer
    //          counts once a game day a worry (Warms)
    //   cold   you had no time for what weighs on it: less patience, it remembers (brushed_off); a chatty one takes it harder, a grumpy one less
    //   rude   on purpose: it is angry (NpcPatience.Irritate, seen by all for a while), colder each time, remembers (insulted) and opens its
    //          next talk with that (place c_mem_insulted: an apology lets it go)
    // Its ways bend all of it (Warmth, Hurt), and a line in the window (RavenBook.TalkNote) tells you when its feeling to you moved. Everything goes through the
    // same patience, feeling and memory as the rest of it: nothing of its own is kept but the day's row in your character (NpcTalkMemo).
    internal static partial class NpcTalk
    {
        internal static float TonePatience = 3f, ToneWarm = 3f, /* (till 2026-11-12 1.5: the day's first kind word, the user: twice as much) */ ToneCold = 4f, ToneIrk = 12f, ToneRude = 3f;
        private const string ToneRow = "~tone", ColdRow = "~cold";

        // once a game day a person: true the first time today (its row in your character), so a line «it feels so» says what really moved
        private static bool FirstToday(NpcActor a, Player p, string row)
        {
            if (DoneToday(a, p, row)) return false;
            Row r = PutRow(p, "g", a, row);
            r.Count = Today; r.At = NowSec;
            SaveRows(p);
            return true;
        }

        // …only asked (a condition: nothing written)
        private static bool DoneToday(NpcActor a, Player p, string row) { Row r = FindRow(p, "g", a, row); return r != null && !Elsewhere(r) && r.Count == Today; }

        // a line in the window: its feeling to you moved (the one wording for every system that moves it)
        private static void Warmer(NpcActor a) { RavenBook.TalkNote(Lang.F("{0}: ставлення до тебе теплішає", a.Name), true); }
        private static void Colder(NpcActor a, bool sharp) { RavenBook.TalkNote(sharp ? Lang.F("{0}: ставлення до тебе різко гіршає", a.Name) : Lang.F("{0}: ставлення до тебе гіршає", a.Name), false); }

        // how much a kind word means to it
        private static float Warmth(NpcActor a)
        {
            HashSet<string> t = NpcPhrases.Tones(a);
            return (t.Contains("бурч") ? 0.6f : 1f) * (NpcPersona.Of(a).Chatty > 0.6f ? 1.3f : 1f);
        }

        // how much an ill word hurts it: a cheerful one lets it go, a brave or grumpy one bites back, a chatty one wanted to be heard
        private static float Hurt(NpcActor a, bool rude)
        {
            HashSet<string> t = NpcPhrases.Tones(a);
            float k = NpcPersona.Of(a).Chatty > 0.6f ? 1.4f : 1f;
            if (t.Contains("весел")) k *= 0.7f;
            if (t.Contains("бурч")) k *= rude ? 1.2f : 0.5f; // (a short answer it respects, an insult it does not)
            if (rude && t.Contains("смілив")) k *= 1.3f;
            return k;
        }

        // A kind word has an air (the user 2026-11-12: the right answer is by the person, not by the row it stands in): «!tone warm soft|blunt|joke». How this
        // person takes that air: NpcNature.TakesBy. From TakeHit it is to its liking (more of everything, and the window says so); at TakeMiss or below it
        // misses: no warmth, a little patience gone, the window says why, and its words are of that (take_miss_<air>) instead of the place's own. A warm
        // answer with no air is as before. The kind answers of different airs change places from day to day (NpcTalk.Mixed): the top row is not «the right one»
        internal static float TakeHit = 1.3f, TakeMiss = 0.65f, MissPatience = 2f;

        private static void Missed(NpcActor a, Player p, string air, float weight)
        {
            NpcPatience.Spend(a, p, MissPatience * weight);
            string why = air == "soft" ? Lang.F("{0} не любить, коли з ним|нею панькаються", a.Name) : air == "joke" ? Lang.F("{0}: йому|їй зараз не до жартів", a.Name) : Lang.F("{0}: для нього|неї це надто різко", a.Name);
            RavenBook.TalkNote(a.Form(why), false);
            if (a == With) { string s = NpcPhrases.Compose(a, "take_miss_" + air, p); if (!string.IsNullOrEmpty(s)) _instead = s; } // (its answer is to how you said it)
        }

        private static void Tone(NpcActor a, Player p, string[] w)
        {
            if (a == null || p == null || w.Length < 2) return;
            float weight = 1f;
            string air = null; // («!tone warm blunt 1.5»: the air and the weight, either or none, in any order)
            for (int i = 2; i < w.Length; i++) { float v; if (float.TryParse(w[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) weight = v; else if (System.Array.IndexOf(NpcNature.Airs, w[i]) >= 0) air = w[i]; else Npcs.Log("talk: tone «" + w[i] + "» is no air (soft blunt joke) and no number"); }
            switch (w[1])
            {
                case "warm":
                {
                    float take = air != null ? NpcNature.TakesBy(a, air) : 1f;
                    if (take <= TakeMiss) { Missed(a, p, air, weight); return; }
                    float k = Warmth(a) * weight * take;
                    NpcPatience.Spend(a, p, -TonePatience * k);
                    bool first = FirstToday(a, p, ToneRow); // (its feeling moves once a game day: kind words are not a way to farm it)
                    if (first) NpcAttitude.Tell(a, p.GetPlayerID(), ToneWarm * k);
                    if (take >= TakeHit) RavenBook.TalkNote(a.Form(first ? Lang.F("{0}: такі слова йому|їй до душі, ставлення до тебе теплішає", a.Name) : Lang.F("{0}: такі слова йому|їй до душі", a.Name)), true);
                    else if (first) Warmer(a);
                    break;
                }
                case "cold":
                    NpcPatience.Spend(a, p, ToneCold * Hurt(a, false) * weight);
                    NpcMemory.Tell(a, "brushed_off", p.GetPlayerID(), ""); // (its feeling moves by it once a game day: NpcMemory)
                    if (FirstToday(a, p, ColdRow)) Colder(a, false);
                    break;
                case "rude":
                {
                    float k = Hurt(a, true) * weight;
                    NpcPatience.Irritate(a, p, ToneIrk * k);
                    NpcAttitude.Tell(a, p.GetPlayerID(), -ToneRude * k);
                    NpcMemory.Tell(a, "insulted", p.GetPlayerID(), "");
                    Colder(a, true);
                    break;
                }
                default: Npcs.Log("talk: no tone «" + w[1] + "»"); break;
            }
        }
    }
}
#endif

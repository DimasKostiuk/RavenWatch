#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;

namespace HuginWhispers
{
    // How you answer what a person says (the user 2026-11-06: an answer must weigh something, and you may choose to be rude on purpose). An
    // answer's deed «!tone warm|cold|rude [weight]» in a talk; an answer with none is plain and costs nothing.
    //   warm   you took its words kindly: more patience and, once a game day a person (ToneRow), warmer to you. A worry's own warm answer
    //          counts once a game day a worry (Warms)
    //   cold   you had no time for what weighs on it: less patience, it remembers (brushed_off); a chatty one takes it harder, a grumpy one less
    //   rude   on purpose: it is angry (NpcPatience.Irritate, seen by all for a while), colder each time, remembers (insulted) and opens its
    //          next talk with that (place c_mem_insulted: an apology lets it go)
    // Its ways bend all of it (Warmth, Hurt), and a line in the window (RavenBook.TalkNote) tells you when its feeling to you moved. Everything goes through the
    // same patience, feeling and memory as the rest of it: nothing of its own is kept but the day's row in your character (NpcTalkMemo).
    internal static partial class NpcTalk
    {
        internal static float TonePatience = 3f, ToneWarm = 1.5f, ToneCold = 4f, ToneIrk = 12f, ToneRude = 3f;
        private const string ToneRow = "~tone", ColdRow = "~cold";

        // once a game day a person: true the first time today (its row in your character), so a line «it feels so» says what really moved
        private static bool FirstToday(NpcActor a, Player p, string row)
        {
            int today = (int)(NpcRescue.Now / (GameHour * 24f));
            Row r = FindRow(p, "g", a, row);
            if (r != null && !Elsewhere(r) && r.Count == today) return false;
            r = PutRow(p, "g", a, row);
            r.Count = today; r.At = NowSec;
            SaveRows(p);
            return true;
        }

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

        private static void Tone(NpcActor a, Player p, string[] w)
        {
            if (a == null || p == null || w.Length < 2) return;
            float weight;
            if (w.Length < 3 || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out weight)) weight = 1f;
            switch (w[1])
            {
                case "warm":
                {
                    float k = Warmth(a) * weight;
                    NpcPatience.Spend(a, p, -TonePatience * k);
                    if (!FirstToday(a, p, ToneRow)) return; // (its feeling moves once a game day: kind words are not a way to farm it)
                    NpcAttitude.Tell(a, p.GetPlayerID(), ToneWarm * k);
                    RavenBook.TalkNote(Lang.F("{0}: ставлення до тебе теплішає", a.Name), true);
                    break;
                }
                case "cold":
                    NpcPatience.Spend(a, p, ToneCold * Hurt(a, false) * weight);
                    NpcMemory.Tell(a, "brushed_off", p.GetPlayerID(), ""); // (its feeling moves by it once a game day: NpcMemory)
                    if (FirstToday(a, p, ColdRow)) RavenBook.TalkNote(Lang.F("{0}: ставлення до тебе гіршає", a.Name), false);
                    break;
                case "rude":
                {
                    float k = Hurt(a, true) * weight;
                    NpcPatience.Irritate(a, p, ToneIrk * k);
                    NpcAttitude.Tell(a, p.GetPlayerID(), -ToneRude * k);
                    NpcMemory.Tell(a, "insulted", p.GetPlayerID(), "");
                    RavenBook.TalkNote(Lang.F("{0}: ставлення до тебе різко гіршає", a.Name), false);
                    break;
                }
                default: Npcs.Log("talk: no tone «" + w[1] + "»"); break;
            }
        }
    }
}
#endif

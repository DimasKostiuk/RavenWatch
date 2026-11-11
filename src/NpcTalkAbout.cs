#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Що скажеш про когось із наших?» (the user 2026-11-07): you ask a settler about one of its neighbours, and it tells the truth of what is between them — and what you heard
    // you may carry to the one it was about (the ground for the line «Шепіт», later).
    //   the list    a made place (Places «folkabout»): the neighbours of its home, the nearest first, «А <ім'я>?» each
    //   its words   by how it feels about that one (NpcAttitude.OfPerson: about_person_close … _hostile) and their last quarrel (about_person_quarrel, {why}: NpcAttitude.QuarrelWord);
    //               its nature bends them: a loner keeps it short whatever it is (about_person_loner), a nosy one gossips, a bully scolds (about_person_<nosy|bully>_<good|bad>).
    //               The same one again within AboutAgain game hours: about_again, a little of its patience
    //   heard       the one it is about stands near (OverheardNear) and may speak: it answers into the window (RavenBook.SayOther: third_overheard_good|bad, then its reply
    //               third_reply_overheard) — praise warms it to the speaker (praised_by), ill words are a quarrel (NpcAttitude.Quarrel «gossip»). Nothing left for you to carry then
    //   carried     else your character keeps what you heard (NpcTalkMemo row h: the one it is about, who said it, good or ill; CarryDays). In that one's talk: «Знаєш, що про тебе
    //               каже <ім'я>?» (?carried, {carried}, !passon, {passon}) — told, good words warm it to the speaker and to you, ill words are their quarrel; a nosy one is glad of
    //               gossip, a caring one or a loner does not thank you for it. Kept silent: nothing, it fades
    // Runs on your game (the talk's); what changes the people goes to the games that run them (NpcAttitude, NpcMemory).
    internal static partial class NpcTalk
    {
        internal const string AboutPlace = "folkabout";
        internal static float CarryDays = 3f, OverheardNear = 8f, PraiseWarm = 6f, AboutAgain = 1f;
        private const int AboutMost = 6;
        internal static string PassonSaid = "";

        // its neighbours you may ask about: of its home, settlers, the nearest first
        private static List<NpcActor> Folk(NpcActor a, int most = AboutMost)
        {
            List<NpcActor> l = new List<NpcActor>();
            ZDOID heart = a != null && a.Zdo != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None;
            if (heart.IsNone()) return l;
            foreach (NpcActor o in NpcHeart.Residents(heart)) if (o != null && o != a && o.Def != null && o.Def.Settler) l.Add(o);
            Vector3 at = a.transform.position;
            l.Sort((x, y) => (x.transform.position - at).sqrMagnitude.CompareTo((y.transform.position - at).sqrMagnitude));
            if (l.Count > most) l.RemoveRange(most, l.Count - most);
            return l;
        }

        // one at least (the condition: no list made, nothing sorted)
        private static bool HasFolk(NpcActor a)
        {
            ZDOID heart = a != null && a.Zdo != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None;
            if (heart.IsNone()) return false;
            foreach (NpcActor o in NpcHeart.Residents(heart)) if (o != null && o != a && o.Def != null && o.Def.Settler) return true;
            return false;
        }

        private static void ShowFolkAbout(Player p)
        {
            NpcActor a = With;
            List<KeyValuePair<string, Action>> rows = new List<KeyValuePair<string, Action>>();
            foreach (NpcActor o in Folk(a)) { NpcActor x = o; string you = Lang.F("А {0}?", o.Name); rows.Add(new KeyValuePair<string, Action>(you, () => ShowAbout(p, x, you))); }
            rows.Add(new KeyValuePair<string, Action>(Lang.T("Та ні, нікого."), () => Show("talk", p)));
            Rows(p, NpcPhrases.Compose(a, "about_pick", p) ?? a.Form(Lang.T("Про кого саме?")), rows);
        }

        // what it says of that one, and what comes of it
        private static void ShowAbout(Player p, NpcActor b, string you)
        {
            NpcActor a = With;
            if (a == null || b == null || b.Zdo == null) { Show("talk", p); return; }
            int tone = 0;
            string why = NpcAttitude.QuarrelWord(a, b);
            string theme = AskedAgain(a, b, p) ? "about_again" : AboutTheme(a, b, why, out tone);
            if (theme == "about_again") { tone = 0; NpcPatience.Spend(a, p, 2f); }
            Dictionary<string, string> v = new Dictionary<string, string> { { "other", b.Name } };
            if (why.Length > 0) v["why"] = why;
            string line = NpcPhrases.Compose(a, theme, p, v) ?? NpcPhrases.Compose(a, "about_person_even", p, v) ?? "…";
            List<RavenBook.TalkSay> lines = RavenBook.Says(NpcBody.Spoken(a, line));
            if (tone != 0 && Overheard(a, b, p, tone > 0, lines)) tone = 0; // (it heard it itself: nothing for you to carry)
            if (tone != 0) Carry(p, b, a, tone);
            List<KeyValuePair<string, Action>> rows = new List<KeyValuePair<string, Action>>();
            rows.Add(new KeyValuePair<string, Action>(Lang.T("А ще про когось?"), () => ShowFolkAbout(p)));
            if (tone < 0) rows.Add(new KeyValuePair<string, Action>(Lang.T("Негарно так про своїх."), () => { Do("tone cold 0.5", p); Show("talk", p); }));
            rows.Add(new KeyValuePair<string, Action>(Grammar.Fix(Lang.T("Зрозум{ів|іла}."), p), () => Show("talk", p)));
            Rows(p, lines, rows, you);
        }

        // the theme of its words about b and whether they are good (1), ill (−1) or nothing to carry (0)
        // asked about b within AboutAgain game hours (row b in your character), and this ask kept
        private static bool AskedAgain(NpcActor a, NpcActor b, Player p)
        {
            string key = SeedOf(b).ToString(CultureInfo.InvariantCulture);
            Row r = FindRow(p, "b", a, key);
            bool again = r != null && !Elsewhere(r) && NowSec - r.At < AboutAgain * GameHour;
            r = PutRow(p, "b", a, key);
            r.At = NowSec; SaveRows(p);
            return again;
        }

        private static string AboutTheme(NpcActor a, NpcActor b, string why, out int tone)
        {
            tone = 0;
            if (NpcNature.Is(a, "loner")) return "about_person_loner";
            string level = NpcAttitude.Level(NpcAttitude.OfPerson(a, b));
            bool good = level == "close" || level == "warm", bad = why.Length > 0 || level == "cold" || level == "hostile";
            if (bad) good = false;
            tone = good ? 1 : bad ? -1 : 0;
            string nature = NpcNature.Is(a, "nosy") ? "nosy" : NpcNature.Is(a, "bully") ? "bully" : null;
            if (nature != null && tone != 0 && NpcPhrases.Has(a, "about_person_" + nature + (good ? "_good" : "_bad"))) return "about_person_" + nature + (good ? "_good" : "_bad");
            return why.Length > 0 ? "about_person_quarrel" : "about_person_" + level;
        }

        // b stands near and hears it: its word into the window, then a's; praise warms b to a, ill words are b's quarrel with a
        private static bool Overheard(NpcActor a, NpcActor b, Player p, bool good, List<RavenBook.TalkSay> lines)
        {
            if (Utils.DistanceXZ(a.transform.position, b.transform.position) > OverheardNear || !NpcVoice.FreeAnywhere(b)) return false;
            string said = NpcPhrases.Compose(b, good ? "third_overheard_good" : "third_overheard_bad", p, new Dictionary<string, string> { { "other", a.Name } });
            if (string.IsNullOrEmpty(said)) return false;
            lines.AddRange(Others(b, said));
            string reply = NpcPhrases.Compose(a, "third_reply_overheard", p, new Dictionary<string, string> { { "other", b.Name } });
            if (!string.IsNullOrEmpty(reply)) lines.Add(new RavenBook.TalkSay { Kind = RavenBook.SayPerson, Text = NpcBody.Spoken(a, reply) });
            WordsReached(b, a, good);
            Npcs.Log(a.Id + ": " + b.Id + " heard what was said of it (" + (good ? "good" : "ill") + ")");
            return true;
        }

        // what one said of b reached b (heard, or carried by you)
        private static void WordsReached(NpcActor b, NpcActor speaker, bool good)
        {
            if (good) { if (WarmPair(b, speaker)) NpcAttitude.TellPerson(b, speaker, PraiseWarm); NpcMemory.Tell(b, "praised_by", 0L, speaker.Name); } // (warmer once a game day a pair: WarmPair)
            else NpcAttitude.Quarrel(b, speaker, "gossip");
        }

        // ---------------------------------------------------------------- carried (your character: row h, one for the one it is about)
        private static void Carry(Player p, NpcActor about, NpcActor speaker, int tone)
        {
            Row old = CarriedRow(p, about);
            if (old != null) Rows(p).Remove(old);
            Row r = PutRow(p, "h", about, SeedOf(speaker).ToString(CultureInfo.InvariantCulture));
            r.Words = Clean(speaker.Name, 40); r.Count = tone; r.At = NowSec;
            SaveRows(p);
        }

        private static Row CarriedRow(Player p, NpcActor about)
        {
            if (p == null || about == null) return null;
            uint seed = SeedOf(about);
            foreach (Row r in Rows(p)) if (r.Kind == "h" && r.Seed == seed && !Elsewhere(r) && NowSec - r.At < CarryDays * 24f * GameHour) return r;
            return null;
        }

        // «!passon»: you tell it what the other said of it
        private static void PassOn(NpcActor a, Player p, string[] w)
        {
            PassonSaid = "…";
            Row r = CarriedRow(p, a);
            if (r == null) return;
            Rows(p).Remove(r); SaveRows(p);
            bool good = r.Count > 0;
            NpcActor from = null;
            uint seed;
            if (uint.TryParse(r.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) foreach (NpcActor o in Folk(a, int.MaxValue)) if (SeedOf(o) == seed) from = o;
            PassonSaid = NpcPhrases.Compose(a, good ? "passon_good" : "passon_bad", p, new Dictionary<string, string> { { "other", r.Words } }) ?? "…";
            if (from == null) return; // (gone from its home: words, nothing between them now)
            WordsReached(a, from, good);
            if (good) Tone(a, p, new[] { "tone", "warm" }); // (a kind word brought: you too)
            else if (NpcNature.Is(a, "nosy")) Tone(a, p, new[] { "tone", "warm", "0.5" }); // (it loves to know)
            else if (NpcNature.Is(a, "caring") || NpcNature.Is(a, "loner")) Tone(a, p, new[] { "tone", "cold", "0.5" }); // (it does not thank you for gossip)
            Npcs.Log(a.Id + ": told what " + from.Id + " said of it (" + (good ? "good" : "ill") + ")");
        }

        private static void WireAbout()
        {
            Places[AboutPlace] = ShowFolkAbout;
            Deeds["passon"] = PassOn;
            Words["{carried}"] = (a, p) => { Row r = CarriedRow(p, a); return r != null ? r.Words : "…"; };
            Words["{passon}"] = (a, p) => PassonSaid;
            StoryCond.Register("hasfolk", new StoryCond.Kind { Usage = "hasfolk   (в його оселі є ще поселенці)", Test = (p, w) => HasFolk(Him) });
            StoryCond.Register("carried", new StoryCond.Kind { Usage = "carried   (ти чув, що про нього казав хтось із його оселі)", Test = (p, w) => CarriedRow(p, Him) != null });
        }
    }
}
#endif

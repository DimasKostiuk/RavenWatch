#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;

namespace HuginWhispers
{
    // «Пам'ятаєш, як ми…?» (the user 2026-11-07): you bring up something the two of you went through, and it tells it its own way — from its memory of you (NpcMemory:
    // you saved it, brought it round, took it in, brought what it asked, its dream came true, you kept your word, it told you its secret, you talked it into staying,
    // a gift it liked). Its words recall_<kind>, with what it remembers in them ({m_<kind>}, {ago_<kind>}: NpcMemory.Values).
    //   which      the memory least lately brought up (one never brought up first); every one brought up within a game day: recall_again, a little of its patience
    //   warmth     once a memory (its moment: the row m keeps it in your character) — the same deed is not a way to farm its feeling; a new deed of that kind warms again
    // ?shared · !recall · {recall}. Runs on your game; its feeling to you goes to the game that runs it (NpcAttitude.Tell).
    internal static partial class NpcTalk
    {
        private static readonly string[] Shared = { "rescued", "revived", "hired", "request_done", "goal_done", "promise_kept", "secret_told", "stayed", "gift_liked" };
        internal static float RecallWarm = 3f;
        internal static string RecallSaid = "";

        // what of its memory of you it has words for
        private static List<NpcMemory.Entry> SharedOf(NpcActor a, Player p)
        {
            List<NpcMemory.Entry> l = new List<NpcMemory.Entry>();
            if (a == null || p == null) return l;
            long me = p.GetPlayerID();
            foreach (NpcMemory.Entry e in NpcMemory.About(a, me)) if (e.Player == me && Array.IndexOf(Shared, e.Kind) >= 0 && NpcPhrases.Has(a, "recall_" + e.Kind)) l.Add(e);
            return l;
        }

        private static int Moment(NpcMemory.Entry e) { return (int)(e.At & 0x7FFFFFFF); }

        // «!recall»: it picks the memory and tells it ({recall})
        private static void Recall(NpcActor a, Player p, string[] w)
        {
            RecallSaid = "…";
            List<NpcMemory.Entry> l = SharedOf(a, p);
            if (l.Count == 0) return;
            NpcMemory.Entry pick = null;
            long pickAt = long.MaxValue;
            foreach (NpcMemory.Entry e in l)
            {
                Row r = FindRow(p, "m", a, e.Kind);
                long at = r == null || Elsewhere(r) ? -1L : r.At;
                if (at < pickAt) { pickAt = at; pick = e; }
            }
            Row row = FindRow(p, "m", a, pick.Kind);
            if (row != null && !Elsewhere(row) && NowSec - row.At < 24f * GameHour)
            {
                RecallSaid = NpcPhrases.Compose(a, "recall_again", p) ?? a.Form(Lang.T("Ми ж про це вже згадували."));
                NpcPatience.Spend(a, p, 2f);
                return;
            }
            bool warmed = row != null && !Elsewhere(row) && row.Count == Moment(pick);
            row = PutRow(p, "m", a, pick.Kind);
            row.At = NowSec; row.Count = Moment(pick);
            SaveRows(p);
            RecallSaid = NpcPhrases.Compose(a, "recall_" + pick.Kind, p) ?? "…";
            if (warmed) return;
            NpcAttitude.Tell(a, p.GetPlayerID(), RecallWarm);
            NpcPatience.Spend(a, p, -TonePatience);
            Warmer(a);
            Npcs.Log(a.Id + ": recalls «" + pick.Kind + "» with you");
        }

        private static void WireRecall()
        {
            Deeds["recall"] = Recall;
            Words["{recall}"] = (a, p) => RecallSaid;
            StoryCond.Register("shared", new StoryCond.Kind { Usage = "shared   (він пам'ятає щось, що ви пережили разом, і має про це слова)", Test = (p, w) => SharedOf(Him, p).Count > 0 });
        }
    }
}
#endif

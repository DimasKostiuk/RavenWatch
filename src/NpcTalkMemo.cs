#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What your character remembers of its talks with people (docs/plans/talk-plan.md; the user 2026-11-05: a reload keeps it): kept in
    // the character (player customData RavenWatch_talks), never in the world — it is yours, the other players have their own. A person is
    // known by its seed (NpcKit.Seed: the same after a restart), never by its id of the session. Rows, one a «;»:
    //   r|seed|key|count|sec|words   a topic asked of it: how many times lately (one fewer each game hour since), when, its last words
    //                                (the repeat guard, NpcTalk.Repeat — «я ж казав» says them again)
    //   t|seed|theme|0|sec|          a worry it told you (NpcVoice.Concerns: it weighs less a while)
    //   f|seed|favour|n|sec|         a favour asked: n 0 refused (off a while), 1 done (a keepsake, a secret: never again)
    //   a|seed||day|sec|             the game day its feeling to you last fell for your asking again and again
    //   w|seed||0|sec|               when it last told you what happened while you were away (NpcAway: what came before is told)
    //   k|seed||0|sec|marks          the tales it told you («Розкажи щось»: a short mark each — NpcTalk.Untold)
    //   g|seed|theme|day|sec|        the game day your kind answer to this worry last warmed it (NpcTalk.WarmedToday)
    // A character walks between worlds and a person written by hand is the same person in every one (NpcKit.Seed), so a row may come from a
    // world whose clock ran further: Elsewhere — it says nothing of now, whatever it counted (a later time would read as «asked ever more»).
    // At most MostRows, a person at most MostOfOne; what goes first when there is no room (Spare): a worry told long enough ago to weigh
    // again, then the oldest of the rest, and a favour done once for good (a keepsake, a secret, a place on your map) only last of all —
    // else it would be done again. Read once per change of the string (Rows), written on a change.
    internal static partial class NpcTalk
    {
        internal const string MemoKey = "RavenWatch_talks";
        private const int MostRows = 160, MostOfOne = 16, MostWords = 160;

        private sealed class Row { public string Kind = "", Key = "", Words = ""; public uint Seed; public int Count; public long At; }
        private static List<Row> _rows;
        private static string _rowsRaw;

        private static List<Row> Rows(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(MemoKey, out raw)) raw = "";
            if (_rows != null && raw == _rowsRaw) return _rows;
            _rowsRaw = raw; _rows = new List<Row>();
            foreach (string line in raw.Split(';'))
            {
                string[] w = line.Split('|');
                uint seed; int n; long at;
                if (w.Length < 6 || !uint.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed) || !int.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || !long.TryParse(w[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out at)) continue;
                _rows.Add(new Row { Kind = w[0], Seed = seed, Key = w[2], Count = n, At = at, Words = w[5] });
            }
            return _rows;
        }

        private static void SaveRows(Player p)
        {
            if (p == null || _rows == null) return;
            while (_rows.Count > MostRows) _rows.Remove(Spare(_rows));
            StringBuilder sb = new StringBuilder();
            foreach (Row r in _rows)
                sb.Append(r.Kind).Append('|').Append(r.Seed.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.Key).Append('|')
                  .Append(r.Count.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.At.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.Words).Append(';');
            p.m_customData[MemoKey] = sb.ToString();
            _rowsRaw = p.m_customData[MemoKey];
        }

        private static uint SeedOf(NpcActor a) { return a != null && a.Zdo != null ? NpcKit.Seed(a.Zdo) : 0u; }

        private static Row FindRow(Player p, string kind, NpcActor a, string key)
        {
            uint seed = SeedOf(a);
            key = Clean(key, 60); // (as it was kept)
            foreach (Row r in Rows(p)) if (r.Seed == seed && r.Kind == kind && r.Key == key) return r;
            return null;
        }

        // the row of this kind for this person and key, made when there is none (the oldest of that person goes past MostOfOne)
        private static Row PutRow(Player p, string kind, NpcActor a, string key)
        {
            Row r = FindRow(p, kind, a, key);
            if (r != null) return r;
            uint seed = SeedOf(a);
            List<Row> rows = Rows(p);
            List<Row> its = rows.FindAll(x => x.Seed == seed);
            if (its.Count >= MostOfOne) rows.Remove(Spare(its));
            r = new Row { Kind = kind, Seed = seed, Key = Clean(key, 60) };
            rows.Add(r);
            return r;
        }

        // the row to let go of: by how little it is worth keeping, then its age
        private static Row Spare(List<Row> among)
        {
            Row best = null; int bestRank = int.MaxValue;
            foreach (Row r in among)
            {
                int rank = r.Kind == "t" && (Elsewhere(r) || NowSec - r.At >= ToldFor * GameHour) ? 0 : r.Kind == "f" && r.Count == 1 ? 2 : 1;
                if (best == null || rank < bestRank || (rank == bestRank && r.At < best.At)) { best = r; bestRank = rank; }
            }
            return best;
        }

        private static string Clean(string s, int most)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c == '|' ? '/' : c == ';' ? ',' : c == '\n' || c == '\r' ? ' ' : c);
            string t = sb.ToString();
            return t.Length > most ? t.Substring(0, most) : t;
        }

        private static long NowSec { get { return (long)NpcRescue.Now; } }
        private static bool Elsewhere(Row r) { return r.At > NowSec; } // (written under another clock: another world, a world put back from a copy)
        private static float GameHour { get { return (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f) / 24f; } }

        // ---------------------------------------------------------------- what the others ask of it
        // a worry it told you lately (NpcVoice.Concerns weighs it less): within ToldFor game hours
        internal static float ToldFor = 3f;
        internal static bool ToldLately(NpcActor a, Player p, string theme)
        {
            Row r = FindRow(p, "t", a, theme);
            return r != null && !Elsewhere(r) && NowSec - r.At < ToldFor * GameHour;
        }

        // what happened while you were away: told up to then (0: never)
        internal static long AwayToldAt(NpcActor a, Player p) { Row r = FindRow(p, "w", a, ""); return r != null && !Elsewhere(r) ? r.At : 0L; }
        internal static void AwayTold(NpcActor a, Player p) { Row r = PutRow(p, "w", a, ""); r.At = NowSec; SaveRows(p); }

        // a worry told you outside the window's own showing (NpcAway: what it told of your absence weighs less a while)
        internal static void MarkTold(Player p, NpcActor a, string theme) { Told(p, a, theme); }

        private static void Told(Player p, NpcActor a, string theme)
        {
            Row r = PutRow(p, "t", a, theme);
            r.At = NowSec;
            SaveRows(p);
        }

        // a favour: refused within FavourOff game hours (asked again too soon), or done once for good
        internal static float FavourOff = 1f;
        internal static bool FavourShut(NpcActor a, Player p, string id, bool once)
        {
            Row r = FindRow(p, "f", a, id);
            if (r == null) return false;
            if (r.Count == 1) return once;
            return !Elsewhere(r) && NowSec - r.At < FavourOff * GameHour;
        }

        internal static void FavourAsked(NpcActor a, Player p, string id, bool done)
        {
            Row r = PutRow(p, "f", a, id);
            r.Count = done ? 1 : 0; r.At = NowSec;
            SaveRows(p);
        }
    }
}
#endif

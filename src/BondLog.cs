#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Знайомі» (the user, 2026-10-23): what a raven of yours remembers of each raven of another player it has met — who
    // it is (its name, kind, person, character), when they met first and last, and what they went through together
    // (talks, warm words, quarrels, a boss, a fire, food brought either way, thefts either way, mockery, visits). Kept in
    // the raven item beside its bond scores (Bonds), so it is there in a game alone too, after the meetings online. The
    // book shows it (RavenBookKin): the list, the bond in words once it has shown itself, and on hover the story and why.
    internal static class BondLog
    {
        internal const string Key = "RavenWatch_bondLog";
        internal const int Talks = 0, Warm = 1, Quarrel = 2, Boss = 3, Fire = 4, FedThem = 5, FedUs = 6, StoleThem = 7, StoleUs = 8,
            MockedUs = 9, MockedThem = 10, Visits = 11, Pack = 12, Counters = 13; // Pack: fought as one («Зграя»)
        private const int Most = 40; // ravens kept (the ones not met longest fall off)

        internal sealed class Rec
        {
            public string Rid = "", Nick = "", Owner = "", Char = "";
            public bool Munin, Female;
            public int First = -1, Last = -1;
            public int Told = -1, ToldDay = -1; // the bond last told to you (Group: 0 enemies, 1 neither, 2 friends; -1 never), and the day
            public bool Fresh;                  // changed since you last opened the card: the seal breathes, the row says «нове»
            public readonly int[] N = new int[Counters];
            // it has shown how it gets along (else the book says they are still looking each other over)
            internal bool Shown { get { return N[Talks] >= 3 || N[Warm] + N[Quarrel] + N[FedThem] + N[FedUs] + N[StoleThem] + N[StoleUs] + N[MockedUs] + N[MockedThem] + N[Visits] + N[Pack] > 0; } }
        }

        // "rid|munin|nick|owner|female|char|first|last|n0,n1,…|told|toldDay|fresh;…", the latest met first; parsed once per change
        private sealed class Parsed { public string Raw; public List<Rec> L; }
        private static readonly Dictionary<ItemDrop.ItemData, Parsed> Cache = new Dictionary<ItemDrop.ItemData, Parsed>();

        internal static List<Rec> Of(ItemDrop.ItemData item)
        {
            string raw = null;
            if (item != null && item.m_customData != null) item.m_customData.TryGetValue(Key, out raw);
            raw = raw ?? "";
            Parsed p;
            if (item != null && Cache.TryGetValue(item, out p) && p.Raw == raw) return p.L;
            List<Rec> l = Parse(raw);
            if (item != null) { if (Cache.Count > 8) Cache.Clear(); Cache[item] = new Parsed { Raw = raw, L = l }; }
            return l;
        }

        internal static int Count(ItemDrop.ItemData item) { return item == null ? 0 : Of(item).Count; }
        internal static Rec Find(ItemDrop.ItemData item, string rid) { if (item == null || string.IsNullOrEmpty(rid)) return null; foreach (Rec r in Of(item)) if (r.Rid == rid) return r; return null; }

        private static List<Rec> Parse(string raw)
        {
            List<Rec> l = new List<Rec>();
            if (raw.Length == 0) return l;
            foreach (string e in raw.Split(';'))
            {
                string[] f = e.Split('|');
                if (f.Length < 9 || f[0].Length == 0) continue;
                Rec r = new Rec { Rid = f[0], Munin = f[1] == "1", Nick = f[2], Owner = f[3], Female = f[4] == "1", Char = f[5] };
                int.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.First);
                int.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Last);
                string[] n = f[8].Split(',');
                for (int i = 0; i < n.Length && i < Counters; i++) int.TryParse(n[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.N[i]);
                if (f.Length >= 12)
                {
                    int.TryParse(f[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Told);
                    int.TryParse(f[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.ToldDay);
                    r.Fresh = f[11] == "1";
                }
                l.Add(r);
            }
            return l;
        }

        private static string Safe(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace("|", "").Replace(";", "").Replace(",", "").Replace("<", "").Replace(">", ""); } // (names from the network: no rich-text tags in the book)

        private static void Write(ItemDrop.ItemData item, List<Rec> l)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < Most; i++)
            {
                Rec r = l[i];
                if (sb.Length > 0) sb.Append(';');
                sb.Append(r.Rid).Append('|').Append(r.Munin ? '1' : '0').Append('|').Append(Safe(r.Nick)).Append('|').Append(Safe(r.Owner)).Append('|')
                  .Append(r.Female ? '1' : '0').Append('|').Append(Safe(r.Char)).Append('|').Append(r.First.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(r.Last.ToString(CultureInfo.InvariantCulture)).Append('|');
                for (int k = 0; k < Counters; k++) { if (k > 0) sb.Append(','); sb.Append(r.N[k].ToString(CultureInfo.InvariantCulture)); }
                sb.Append('|').Append(r.Told.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.ToldDay.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.Fresh ? '1' : '0');
            }
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[Key] = sb.ToString();
        }

        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }

        // my raven met that player's raven (what = one of the counters, or -1: only met): who it is now, when, and that
        internal static void Note(ItemDrop.ItemData mine, Player o, bool theirMunin, int what)
        {
            if (mine == null || o == null || o == Player.m_localPlayer) return;
            string rid = Bonds.RidOf(o, theirMunin);
            if (rid.Length == 0) return;
            List<Rec> l = new List<Rec>(Of(mine)); // (a copy: the cached one is not changed under a reader)
            Rec r = l.Find(x => x.Rid == rid);
            if (r == null) r = new Rec { Rid = rid, First = Today };
            else { l.Remove(r); r = Copy(r); }
            r.Munin = theirMunin;
            r.Nick = Nicks.Name(o, theirMunin);
            r.Owner = o.GetPlayerName();
            r.Female = Grammar.Female(o);
            Norov.Character c = Norov.OnPlayer(o, theirMunin);
            if (c != null) r.Char = c.Id;
            r.Last = Today;
            if (what >= 0 && what < Counters) r.N[what] = Mathf.Min(r.N[what] + 1, 9999);
            l.Insert(0, r);
            Write(mine, l);
            Check(mine, o, theirMunin);
        }

        // the names as they are now: a raven renamed by its owner (or a person renamed) since the last meeting. Only the
        // players in this game are asked (one look at each of their two ravens), written only when a name differs
        internal static void Rename(ItemDrop.ItemData mine)
        {
            if (mine == null || Of(mine).Count == 0) return;
            List<Rec> l = null;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == Player.m_localPlayer) continue;
                foreach (bool theirMunin in new[] { false, true })
                {
                    string rid = Bonds.RidOf(o, theirMunin);
                    if (rid.Length == 0) continue;
                    List<Rec> from = l ?? Of(mine);
                    int i = from.FindIndex(x => x.Rid == rid);
                    if (i < 0) continue;
                    // a name it really has (not the plain «Хугін» of a moment when its nickname is not told yet)
                    string nick, call; Nicks.Kind k;
                    if (!Nicks.Of(o, theirMunin, out nick, out call, out k)) nick = from[i].Nick;
                    string owner = o.GetPlayerName();
                    if (string.IsNullOrEmpty(owner)) owner = from[i].Owner; // (not known yet: kept)
                    if (from[i].Nick == nick && from[i].Owner == owner) continue;
                    if (l == null) l = new List<Rec>(Of(mine)); // (a copy: the cached one is not changed under a reader)
                    Rec c = Copy(l[i]);
                    c.Nick = nick; c.Owner = owner;
                    l[i] = c;
                }
            }
            if (l != null) Write(mine, l);
        }

        // ---------------------------------------------------------------- the bond changed: told, not quietly

        internal const int Enemies = 0, Neither = 1, Friends = 2;
        internal static int Group(Bonds.State s) { return s == Bonds.State.Enemy ? Enemies : s >= Bonds.State.Pals ? Friends : Neither; }

        // after a meeting or a change of the score (Bonds.Change): the bond crossed into friends or enemies, made up after
        // enmity, or cooled from friendship — once it has shown itself, and at most once a game day for these two
        internal static void Check(ItemDrop.ItemData mine, Player o, bool theirMunin)
        {
            Player me = Player.m_localPlayer;
            if (mine == null || o == null || me == null) return;
            string rid = Bonds.RidOf(o, theirMunin);
            List<Rec> l = Of(mine);
            int at = l.FindIndex(x => x.Rid == rid);
            float score, start;
            if (at < 0 || !l[at].Shown || !Bonds.Stored(mine, rid, out score, out start)) return;
            Rec r = l[at];
            int g = Group(Bonds.StateOf(score));
            if (g == r.Told) return;
            if (r.Told < 0 && g == Neither) { Set(mine, at, g, false); return; } // (first known as neither: nothing to tell)
            if (r.ToldDay == Today) return; // (told about these two today already: the next change waits a day, no flicker on the edge)
            int was = r.Told;
            Set(mine, at, g, true);
            bool myMunin = RavenSeat.On(me, true) == mine;
            Bonds.Told(me, myMunin, o, theirMunin, was, g);
            RavenBook.Refresh();
        }

        private static void Set(ItemDrop.ItemData mine, int at, int g, bool told)
        {
            List<Rec> l = new List<Rec>(Of(mine));
            Rec r = Copy(l[at]);
            r.Told = g;
            if (told) { r.ToldDay = Today; r.Fresh = true; }
            l[at] = r;
            Write(mine, l);
        }

        internal static bool AnyFresh(ItemDrop.ItemData item) { foreach (Rec r in Of(item)) if (r.Fresh) return true; return false; }

        // the card was opened: nothing new any more
        internal static void Seen(ItemDrop.ItemData item)
        {
            if (!AnyFresh(item)) return;
            List<Rec> l = new List<Rec>();
            foreach (Rec r in Of(item)) { Rec c = Copy(r); c.Fresh = false; l.Add(c); }
            Write(item, l);
        }

        private static Rec Copy(Rec a)
        {
            Rec b = new Rec { Rid = a.Rid, Munin = a.Munin, Nick = a.Nick, Owner = a.Owner, Female = a.Female, Char = a.Char, First = a.First, Last = a.Last, Told = a.Told, ToldDay = a.ToldDay, Fresh = a.Fresh };
            System.Array.Copy(a.N, b.N, Counters);
            return b;
        }


        // ---------------------------------------------------------------- told in the book

        // "сьогодні", "вчора", "3 дні тому"
        internal static string When(int day)
        {
            int d = Today - day;
            if (day < 0) return "";
            return d <= 0 ? Lang.T("сьогодні") : d == 1 ? Lang.T("вчора") : Lang.F("{0} тому", Lang.N(d, "день|дні|днів"));
        }

        // the story on hover: when they met, what they went through, the chases, and why they get along so (or not)
        internal static string Story(ItemDrop.ItemData mine, bool myMunin, Rec r)
        {
            StringBuilder sb = new StringBuilder();
            string who = Plugin.RavenName(myMunin);
            sb.Append(Lang.F("<b>{0}</b> · {1} гравця {2}", r.Nick, Plugin.RavenName(r.Munin), r.Owner)).Append('\n');
            if (r.First >= 0) sb.Append(Lang.F("Познайомились {0}, востаннє бачились {1}.", When(r.First), When(r.Last))).Append('\n');
            Norov.Character c = Norov.CharById(r.Char);
            if (c != null) sb.Append(Lang.F("Норов: {0}.", c.Name)).Append('\n');
            sb.Append('\n');
            Line(sb, r.N[Talks], Lang.T("Розмовляли"), "раз|рази|разів");
            Line(sb, r.N[Fire], Lang.T("Сиділи разом біля вогню"), "раз|рази|разів");
            Line(sb, r.N[Boss], Lang.T("Разом бачили, як падає бос"), "раз|рази|разів");
            Line(sb, r.N[Pack], Lang.T("Билися зграєю"), "раз|рази|разів");
            Line(sb, r.N[Warm], Lang.T("Тепло вітались"), "раз|рази|разів");
            Line(sb, r.N[Visits], Lang.F("{0} літав до нього в гості", who), "раз|рази|разів");
            Line(sb, r.N[FedUs], Lang.F("Приносив {0} поїсти", Plugin.RavenNameDat(myMunin)), "раз|рази|разів");
            Line(sb, r.N[FedThem], Lang.F("{0} носив йому поїсти", who), "раз|рази|разів");
            Line(sb, r.N[Quarrel], Lang.T("Сварились"), "раз|рази|разів");
            Line(sb, r.N[StoleUs], Lang.T("Цупив у тебе їжу"), "раз|рази|разів");
            Line(sb, r.N[StoleThem], Lang.F("{0} цупив їжу в його людини", who), "раз|рази|разів");
            Line(sb, r.N[MockedUs], Lang.T("Глузував із вас"), "раз|рази|разів");
            Line(sb, r.N[MockedThem], Lang.F("{0} глузував із нього", who), "раз|рази|разів");
            int w, l;
            if (JointPlay.ChaseWith(mine, r.Rid, out w, out l) && w + l > 0) sb.Append(Lang.F("Догонялки: {0} : {1}", w, l)).Append('\n');
            string why = Why(mine, r);
            if (why != null) sb.Append('\n').Append(why);
            return sb.ToString().TrimEnd();
        }

        private static void Line(StringBuilder sb, int n, string what, string forms) { if (n > 0) sb.Append(what).Append(": ").Append(Lang.N(n, forms)).Append('\n'); }

        // the biggest reason of the bond, in its direction (a friend: what brought them together; an enemy: what parted them)
        internal static string Why(ItemDrop.ItemData mine, Rec r)
        {
            float score, start;
            if (!r.Shown || !Bonds.Stored(mine, r.Rid, out score, out start)) return null;
            bool good = score >= 0f;
            string best = null; float bw = 4f;
            Pick(ref best, ref bw, good, start, start >= 0f ? Lang.T("Від природи ладнають: норови пасують одне одному.") : Lang.T("Від природи не ладнають: норови не зійшлися."));
            Pick(ref best, ref bw, good, (r.N[Talks] + r.N[Fire]) * Bonds.TalkGain + r.N[Boss] * Bonds.BossGain, Lang.T("Багато пережили разом: розмови, вогнища, боси."));
            Pick(ref best, ref bw, good, r.N[Warm] * Bonds.FriendGain * 4f + r.N[Visits] * 4f, Lang.T("Часто бачаться й навідують одне одного."));
            Pick(ref best, ref bw, good, r.N[Pack] * HuginWhispers.Pack.PairBond * 2f, Lang.T("Пліч-о-пліч билися зграєю."));
            Pick(ref best, ref bw, good, r.N[FedUs] * Bonds.ShareGain + r.N[FedThem] * Bonds.ShareGiverGain, Lang.T("Ділилися їжею, коли інший був голодний."));
            Pick(ref best, ref bw, good, -r.N[Quarrel] * Bonds.EnemyLoss * 3f, Lang.T("Що не зустріч, то сварка."));
            Pick(ref best, ref bw, good, -(r.N[StoleUs] + r.N[StoleThem]) * Bonds.StealLoss, Lang.T("Крадіжки їжі такого не прощають."));
            Pick(ref best, ref bw, good, -(r.N[MockedUs] * Bonds.MockLoss + r.N[MockedThem] * Bonds.MockerLoss), Lang.T("Глузування запам'ятались."));
            return best != null ? "<i>" + best + "</i>" : null;
        }

        private static void Pick(ref string best, ref float bw, bool good, float weight, string text)
        {
            float w = good ? weight : -weight;
            if (w > bw) { bw = w; best = text; }
        }

        internal static void Reset() { Cache.Clear(); Bonds.ResetMoments(); }
    }
}
#endif

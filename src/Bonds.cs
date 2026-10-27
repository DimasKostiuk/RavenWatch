#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Стосунки» (the user, 2026-10-22): every two ravens of different players get along or not. Hidden: you find out by
    // what they do, and a friendship is heard in the thoughts and in your two's own bubble.
    //   - A pair is two ravens (not two players): your Hugin may like Vasya's Munin and hate his Hugin. Each game keeps,
    //     in its raven item, a score -100…100 toward every raven it has met (by their ids, Backup.Id).
    //   - It starts from their natures (Base: the two characters and a chemistry of their own, the same on both games)
    //     and moves with what they live through: talks together, a boss, a fire (+), a spell of mockery on their people
    //     or a quarrel (−), a theft (−); each game day it drifts a little back toward its start.
    //   - Near each other (Near) now and then (the deciding game: the lower player id; the score of the two sides
    //     averaged) friends have a warm word and one flies over to sit beside the other a while; then your two may
    //     speak of that friend in their bubble. Enemies quarrel, feathers fly, and sometimes one steals a bite of food
    //     from the other player's bag (that player's own game takes it out).
    //   - A friend of one of their ravens is a theme of the thought about that player (AboutTalk).
    //   - Visits (VisitEvery): a raven of yours goes by itself to sit by a friend of it (on that player's free shoulder
    //     or head) a minute or so, has a word with it there, watches what that player does (builds, fights, sleeps,
    //     gets hurt), and back on your shoulder tells you — that, or the gossip of that player's real doings.
    internal static partial class Bonds
    {
        internal static bool Enabled = true;
        internal static float FriendAt = 40f, EnemyAt = -40f;
        internal static float Near = 10f, MomentChance = 0.5f, StealChance = 0.4f, VisitChance = 0.5f, PairTalkChance = 0.4f;
        internal static float PairMinutesMin = 4f, PairMinutesMax = 8f, VisitSeconds = 25f;
        internal static float VisitEveryMin = 6f, VisitEveryMax = 12f, OwnVisitChance = 0.35f, VisitRange = 25f, LongMin = 40f, LongMax = 70f, BackLineAfter = 5f;
        internal static float TalkGain = 3f, BossGain = 8f, FireGain = 3f, FriendGain = 1f, EnemyLoss = 2f, MockLoss = 8f, MockerLoss = 3f, StealLoss = 10f, DayDrift = 0.15f;
        internal static float ShareGain = 6f, ShareGiverGain = 3f, ShareStay = 10f; // a bite brought to a hungry friend (Share): the friend warms to it more than the giver; s it stays
        internal const string ItemKey = "RavenWatch_bonds", RidH = "RavenWatch_RidH", RidM = "RavenWatch_RidM";
        private const string StealRpc = "RavenWatch_BondSteal", StolenRpc = "RavenWatch_BondStolen";

        internal enum State { Enemy, Wary, Known, Pals, Close }

        // ---------------------------------------------------------------- the score of one raven toward others

        private sealed class Book
        {
            public readonly Dictionary<string, float> Score = new Dictionary<string, float>(), Start = new Dictionary<string, float>();
            public int Day = -1;
        }

        // "day;rid=score:start;…" in the raven item (start: where the two began — the day's drift goes back toward it)
        // read once per change (the moments, the publishing and the thoughts ask often): the same text, the same book
        private sealed class Parsed { public string Raw; public Book B; }
        private static readonly Dictionary<ItemDrop.ItemData, Parsed> Books = new Dictionary<ItemDrop.ItemData, Parsed>();
        private static Book Read(ItemDrop.ItemData item)
        {
            string raw = null;
            if (item != null && item.m_customData != null) item.m_customData.TryGetValue(ItemKey, out raw);
            raw = raw ?? "";
            Parsed p;
            if (item != null && Books.TryGetValue(item, out p) && p.Raw == raw) return p.B;
            Book b = Parse(raw);
            if (item != null) { if (Books.Count > 8) Books.Clear(); Books[item] = new Parsed { Raw = raw, B = b }; }
            return b;
        }

        private static Book Parse(string raw)
        {
            Book b = new Book();
            if (string.IsNullOrEmpty(raw)) return b;
            string[] parts = raw.Split(';');
            int d; if (parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out d)) b.Day = d;
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string id = parts[i].Substring(0, eq);
                string[] vs = parts[i].Substring(eq + 1).Split(':');
                float v, st;
                if (!float.TryParse(vs[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                b.Score[id] = v;
                b.Start[id] = vs.Length > 1 && float.TryParse(vs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out st) ? st : 0f;
            }
            return b;
        }

        private static Book Copy(Book b)
        {
            Book c = new Book { Day = b.Day };
            foreach (KeyValuePair<string, float> kv in b.Score) c.Score[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, float> kv in b.Start) c.Start[kv.Key] = kv.Value;
            return c;
        }

        private static void Write(ItemDrop.ItemData item, Book b)
        {
            if (item == null) return;
            StringBuilder sb = new StringBuilder(b.Day.ToString(CultureInfo.InvariantCulture));
            int n = 0;
            foreach (KeyValuePair<string, float> kv in b.Score)
            {
                if (++n > 60) break; // (the first 60 met: enough)
                float st; b.Start.TryGetValue(kv.Key, out st);
                sb.Append(';').Append(kv.Key).Append('=').Append(Mathf.RoundToInt(kv.Value).ToString(CultureInfo.InvariantCulture)).Append(':').Append(Mathf.RoundToInt(st).ToString(CultureInfo.InvariantCulture));
            }
            item.m_customData[ItemKey] = sb.ToString();
        }

        // a raven's id as every game knows it: its own item's (Backup), or what its player sent
        internal static string RidOf(Player p, bool munin)
        {
            if (p == null) return "";
            if (p == Player.m_localPlayer) { ItemDrop.ItemData r = RavenSeat.On(p, munin); return r != null ? Backup.Id(r) ?? "" : ""; }
            ZDO z = NetFlag.Zdo(p, false);
            return z != null ? z.GetString(munin ? RidM : RidH, "") : "";
        }

        // where two ravens start: their characters, and a chemistry of the two ids (the same on both games, whoever counts)
        internal static float Base(string charA, string ridA, string charB, string ridB)
        {
            float s = Pair(charA, charB);
            string a = string.CompareOrdinal(ridA, ridB) < 0 ? ridA + ridB : ridB + ridA;
            int h = a.GetStableHashCode();
            s += ((h & 0xFFFF) / 65535f * 2f - 1f) * 25f;
            return Mathf.Clamp(s, -90f, 90f);
        }

        // two characters: who gets along (the same both ways)
        private static float Pair(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
            if (a == b) return a == "bold" ? -35f : a == "calm" ? 20f : a == "fidget" ? 15f : a == "watchful" ? -5f : 10f;
            if (a == "devoted" || b == "devoted") return 15f;
            string x = string.CompareOrdinal(a, b) < 0 ? a + "+" + b : b + "+" + a;
            switch (x)
            {
                case "calm+fidget": return 25f;
                case "fidget+wanderer": return 20f;
                case "calm+homebody": return 20f;
                case "homebody+wanderer": return -15f;
                case "bold+watchful": return -25f;
                case "bold+calm": return -10f;
                case "bold+homebody": return -10f;
            }
            if (a == "watchful" || b == "watchful") return -10f;
            return 0f;
        }

        private static string CharOn(Player p, bool munin)
        {
            Norov.Character c = p == Player.m_localPlayer ? Norov.CharOf(RavenSeat.On(p, munin)) : Norov.OnPlayer(p, munin);
            return c != null ? c.Id : "";
        }

        // my raven's score toward that player's raven (met now: from its start)
        internal static float Score(Player me, bool myMunin, Player o, bool theirMunin)
        {
            ItemDrop.ItemData item = RavenSeat.On(me, myMunin);
            string mine = RidOf(me, myMunin), theirs = RidOf(o, theirMunin);
            if (item == null || mine.Length == 0 || theirs.Length == 0) return 0f;
            Book b = Read(item);
            float v;
            if (b.Score.TryGetValue(theirs, out v)) return v;
            return Base(CharOn(me, myMunin), mine, CharOn(o, theirMunin), theirs);
        }

        // what my raven keeps toward the raven with this id: its score and where the two started (false: never met)
        internal static bool Stored(ItemDrop.ItemData item, string rid, out float score, out float start)
        {
            score = start = 0f;
            if (item == null || string.IsNullOrEmpty(rid)) return false;
            Book b = Read(item);
            if (!b.Score.TryGetValue(rid, out score)) return false;
            b.Start.TryGetValue(rid, out start);
            return true;
        }

        // my raven toward one it has met, by this much (a visit: Guests; its owner need not be here)
        internal static void Bump(ItemDrop.ItemData item, string rid, float by)
        {
            if (item == null || string.IsNullOrEmpty(rid)) return;
            Book b = Copy(Read(item));
            float v;
            if (!b.Score.TryGetValue(rid, out v)) return;
            b.Score[rid] = Mathf.Clamp(v + by, -100f, 100f);
            Write(item, b);
        }

        // it has a friend or an enemy among the players' ravens (Requests: «Познайом мене»)
        internal static bool HasFriendOrEnemy(ItemDrop.ItemData item)
        {
            if (item == null) return false;
            foreach (float v in Read(item).Score.Values) if (v >= FriendAt || v <= EnemyAt) return true;
            return false;
        }

        internal static void Change(Player me, bool myMunin, Player o, bool theirMunin, float by)
        {
            ItemDrop.ItemData item = RavenSeat.On(me, myMunin);
            string theirs = RidOf(o, theirMunin);
            if (item == null || theirs.Length == 0 || RidOf(me, myMunin).Length == 0) return;
            Book b = Copy(Read(item));
            float v = Score(me, myMunin, o, theirMunin);
            if (!b.Start.ContainsKey(theirs)) b.Start[theirs] = Base(CharOn(me, myMunin), RidOf(me, myMunin), CharOn(o, theirMunin), theirs);
            b.Score[theirs] = Mathf.Clamp(v + by * BreedTraits.BondMulOf(item) * Habits.BondMul(item), -100f, 100f); // (the breed «Альбінос»: faster both ways)
            Write(item, b);
            BondLog.Check(item, o, theirMunin); // crossed into friends or enemies: told
        }

        // the bond of my raven toward that one crossed over (BondLog.Check): a line in the corner and, when the dialogue
        // director lets it, a word of my raven on it
        internal static void Told(Player me, bool my, Player o, bool tm, int was, int now)
        {
            string nick = Nicks.Name(o, tm), owner = o.GetPlayerName(), mine = Plugin.RavenName(my);
            string text, tag;
            if (now == BondLog.Friends) { text = Lang.F("{0} і {1} ({2}) тепер друзі", mine, nick, owner); tag = "bond_now_friends"; }
            else if (now == BondLog.Enemies) { text = Lang.F("{0} і {1} ({2}) тепер вороги", mine, nick, owner); tag = "bond_now_enemies"; }
            else if (was == BondLog.Enemies) { text = Lang.F("{0} і {1} ({2}) помирились", mine, nick, owner); tag = "bond_made_up"; }
            else { text = Lang.F("{0} і {1} ({2}) уже не друзі", mine, nick, owner); tag = "bond_cooled"; }
            me.Message(MessageHud.MessageType.TopLeft, text, 0, RavenItem.Icon(my));
            if (now != BondLog.Neither) People.OnBondTold(me, my, o, now == BondLog.Friends); // (a friend or a foe of a raven: its person too, a little)
            if (!Free(me, my) || RavenSkills.Hushed(me) || !Director.May(Director.Voice.Shoulder)) return;
            string line = Line(WhoOf(me, my), WhoOf(o, tm), tag);
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(me, my, line);
            Director.Spoke(Director.Voice.Shoulder, RavenSpeech.ReadSeconds(line));
        }

        internal static State StateOf(float s) { return s <= EnemyAt ? State.Enemy : s < -10f ? State.Wary : s < FriendAt ? State.Known : s < 75f ? State.Pals : State.Close; }

        // the two sides together (what the deciding game goes by): mine and what theirs sent, averaged
        internal static float PairScore(Player me, bool myMunin, Player o, bool theirMunin)
        {
            float mine = Score(me, myMunin, o, theirMunin);
            float theirs;
            return TheirScore(o, theirMunin, RidOf(me, myMunin), out theirs) ? (mine + theirs) * 0.5f : mine;
        }

        // a raven of mine is friends with one of that player's ravens: which one (the thoughts speak of it)
        internal static bool FriendOf(Player me, bool myMunin, Player o, out bool theirMunin)
        {
            theirMunin = false;
            float best = FriendAt;
            bool found = false;
            foreach (bool m in new[] { false, true })
            {
                if (Shoulder.ModelOf(o, m) == null) continue;
                float s = PairScore(me, myMunin, o, m);
                if (s >= best) { best = s; theirMunin = m; found = true; }
            }
            return found;
        }

        // ---------------------------------------------------------------- what they live through

        private static readonly Dictionary<string, float> TalkAt = new Dictionary<string, float>();
        internal static float TalkGap = 30f; // s: one kind of talk with one raven counts at most this often

        // a talk of the players' ravens (CrossTalk.Play, on every game): my ravens in it, toward the others in it
        internal static void OnTalk(string kind, List<CrossTalk.Who> whos)
        {
            Player me = Player.m_localPlayer;
            if (me == null || whos == null) return;
            float by = kind == "boss" ? BossGain : kind == "fire" ? FireGain : kind == "bfriend" ? FriendGain : kind == "benemy" ? -EnemyLoss
                : kind == "intro" || kind == "greet" || kind == "pals" ? TalkGain : 0f;
            for (int i = 0; i < whos.Count; i++)
            {
                if (whos[i].P != me) continue;
                for (int j = 0; j < whos.Count; j++)
                {
                    if (whos[j].P == me || whos[j].P == null) continue;
                    float d = by;
                    if (kind == "victim") d = i == 0 ? -MockerLoss : -MockLoss; // (0 mocked, 1 stood up for its people)
                    string key = kind + "|" + whos[i].Munin + "|" + RidOf(whos[j].P, whos[j].Munin);
                    float at;
                    if (TalkAt.TryGetValue(key, out at) && Time.time - at < TalkGap) continue; // (one talk of a kind counts once in a while)
                    TalkAt[key] = Time.time;
                    if (TalkAt.Count > 64) TalkAt.Clear();
                    BondLog.Note(RavenSeat.On(me, whos[i].Munin), whos[j].P, whos[j].Munin, LogOf(kind, i, j));
                    if (d != 0f) Change(me, whos[i].Munin, whos[j].P, whos[j].Munin, d);
                }
            }
            if (kind == "benemy") Squabble(whos);
        }

        // which line of the acquaintance a talk is (me: my raven's place in it, them: the other's)
        private static int LogOf(string kind, int me, int them)
        {
            switch (kind)
            {
                case "fire": return BondLog.Fire;
                case "boss": return BondLog.Boss;
                case "bfriend": return BondLog.Warm;
                case "benemy": return BondLog.Quarrel;
                case "victim": return me == 0 ? BondLog.MockedThem : them == 0 ? BondLog.MockedUs : BondLog.Talks;
                default: return BondLog.Talks;
            }
        }

        // a raven of that player picked mine to mock (Victim, told to me): my ravens remember it
        internal static void OnMocked(string who, bool munin)
        {
            Player me = Player.m_localPlayer, o = Find(who);
            if (me == null || o == null) return;
            foreach (bool m in new[] { false, true }) if (RavenSeat.On(me, m) != null) Change(me, m, o, munin, -MockLoss);
            People.OnMocked(o); // (and that raven's person)
        }

        private static Player Find(string name) { foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetPlayerName() == name) return o; return null; }

        // a new game day: every score drifts a little back toward where the two started (their natures, their chemistry)
        private static int _driftDay = -1;
        private static void Drift(Player me)
        {
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (day == _driftDay) return; // (looked at once a day; a raven put on later gets it the next day)
            _driftDay = day;
            foreach (bool m in new[] { false, true })
            {
                ItemDrop.ItemData item = RavenSeat.On(me, m);
                if (item == null) continue;
                Book b = Read(item);
                if (b.Day == day) continue;
                b = Copy(b);
                bool first = b.Day < 0;
                b.Day = day;
                if (!first)
                    foreach (string k in new List<string>(b.Score.Keys)) { float st; b.Start.TryGetValue(k, out st); b.Score[k] = Mathf.Lerp(b.Score[k], st, DayDrift); }
                Write(item, b);
            }
        }

        // ---------------------------------------------------------------- told to everyone near

        private static void Publish(Player me)
        {
            ZDO z = NetFlag.Zdo(me, true);
            if (z == null) return;
            string h = RidOf(me, false), m = RidOf(me, true);
            if (z.GetString(RidH, "") != h) z.Set(RidH, h);
            if (z.GetString(RidM, "") != m) z.Set(RidM, m);
            // my ravens' scores toward the ravens of the players near: "h:rid=s,rid=s|m:rid=s"
            StringBuilder sb = new StringBuilder();
            foreach (bool my in new[] { false, true })
            {
                if (RavenSeat.On(me, my) == null) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append(my ? "m:" : "h:");
                bool first = true;
                foreach (Player o in Player.GetAllPlayers())
                {
                    if (o == null || o == me || Vector3.Distance(o.transform.position, me.transform.position) > 40f) continue;
                    foreach (bool tm in new[] { false, true })
                    {
                        string rid = RidOf(o, tm);
                        if (rid.Length == 0) continue;
                        if (!first) sb.Append(',');
                        sb.Append(rid).Append('=').Append(Mathf.RoundToInt(Score(me, my, o, tm)).ToString(CultureInfo.InvariantCulture));
                        first = false;
                    }
                }
            }
            NearCast.Tell(me, NearCast.Bonds, sb.ToString()); // (to the players near: not on the ZDO, it grew with every player)
        }

        // what that player's raven thinks of the raven with this id (as their game sent it)
        private static bool TheirScore(Player o, bool theirMunin, string myRid, out float score)
        {
            score = 0f;
            string raw = NearCast.Of(o, NearCast.Bonds);
            if (raw.Length == 0 || myRid.Length == 0) return false;
            string head = theirMunin ? "m:" : "h:";
            foreach (string side in raw.Split('|'))
            {
                if (!side.StartsWith(head, StringComparison.Ordinal)) continue;
                foreach (string kv in side.Substring(2).Split(','))
                {
                    int eq = kv.IndexOf('=');
                    if (eq > 0 && kv.Substring(0, eq) == myRid) return float.TryParse(kv.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out score);
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the moments (the deciding game)

        private static readonly Dictionary<string, float> PairNext = new Dictionary<string, float>();
        private static float _tickAt;

        // from the twice-a-second sync (RavenSkills)
        internal static void Tick(Player me)
        {
            if (me == null || Time.time < _tickAt) return;
            _tickAt = Time.time + 5f;
            Drift(me);
            Publish(me);
            Visit(me);
            Tell(me);
            Moments(me);
            if (Enabled && !me.IsDead()) MaybeVisit(me);
            if (!Enabled || me.IsDead() || Victim.Busy || (Senses.Ready && (Senses.Fight || Senses.Sleeping))) return;
            if (RavenSkills.Hushed(me) || !Director.May(Director.Voice.Talk)) return;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || o.IsDead() || Shadow.Flagged(o) || Vector3.Distance(o.transform.position, me.transform.position) > Near) continue;
                if (!CrossTalk.Decides(me, o)) continue;
                foreach (bool my in new[] { false, true })
                {
                    if (!Free(me, my)) continue;
                    foreach (bool tm in new[] { false, true })
                    {
                        if (!CrossTalk.CanTalk(o, tm)) continue;
                        string key = RidOf(me, my) + "|" + RidOf(o, tm);
                        if (key.Length < 3) continue;
                        float next;
                        if (!PairNext.TryGetValue(key, out next)) { if (PairNext.Count > 64) PairNext.Clear(); PairNext[key] = Time.time + UnityEngine.Random.Range(60f, 180f); if (Reunion(me, my, o, tm)) return; continue; } // (just met: a little while first — or, apart for days, a greeting of its own)
                        if (Time.time < next) continue;
                        PairNext[key] = Time.time + UnityEngine.Random.Range(PairMinutesMin, PairMinutesMax) * 60f;
                        State st = StateOf(PairScore(me, my, o, tm));
                        if ((st != State.Pals && st != State.Close && st != State.Enemy) || UnityEngine.Random.value > MomentChance) continue;
                        if (st == State.Enemy ? Quarrel(me, my, o, tm) : Friends(me, my, o, tm)) return; // (one moment at a time)
                    }
                }
            }
        }

        // a raven of mine that may do something: on the shoulder, awake, mine, not away
        private static bool Free(Player me, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(me, munin);
            return r != null && Trust.Active(r) && !Trust.Stranger(r, me) && !Trust.Away(r) && CrossTalk.CanTalk(me, munin);
        }

        private static CrossTalk.Who WhoOf(Player p, bool munin) { return new CrossTalk.Who { P = p, Munin = munin, Level = Mathf.Max(1, p == Player.m_localPlayer ? Trust.LevelWorn(munin) : Shoulder.LevelOn(p, munin)) }; }

        // one line of s to t, as the talks put it: {name} its people, {friend} the other's, {raven} the other raven, {addr}
        // how it is called, {me} itself, {kin} the other's kind; extra: more values
        private static string Line(CrossTalk.Who s, CrossTalk.Who t, string tag, Dictionary<string, string> extra = null)
        {
            if (Plugin.Instance == null) return null;
            Dictionary<string, string> v = extra != null ? new Dictionary<string, string>(extra) : new Dictionary<string, string>();
            v["name"] = s.P.GetPlayerName(); v["friend"] = t.P.GetPlayerName();
            v["raven"] = Nicks.Name(t.P, t.Munin); v["addr"] = Nicks.Call(t.P, t.Munin); v["me"] = Nicks.Name(s.P, s.Munin);
            v["kin"] = Plugin.RavenName(t.Munin);
            return Composer.ComposeCross(Plugin.Instance.PhraseDir, s.Munin, tag, s.Level, Grammar.Female(s.P), Grammar.Female(t.P), v, Voice.CodeOn(s.P, s.Munin));
        }

        // friends: a warm word both ways; then one flies over to sit beside the other a while
        private static bool Friends(Player me, bool my, Player o, bool tm)
        {
            CrossTalk.Who a = WhoOf(me, my), b = WhoOf(o, tm);
            string l1 = Line(a, b, "bond_friend_open"), l2 = l1 != null ? Line(b, a, "bond_friend_reply") : null;
            if (l2 == null) return false;
            List<CrossTalk.Line> lines = new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } };
            if (UnityEngine.Random.value < 0.5f) { string l3 = Line(a, b, "bond_friend_close"); if (l3 != null) lines.Add(new CrossTalk.Line { Who = 0, To = 1, Text = l3 }); }
            CrossTalk.Script("bfriend", new List<CrossTalk.Who> { a, b }, lines);
            if (UnityEngine.Random.value < VisitChance) StartVisit(me, my, o, tm);
            return true;
        }

        // enemies: a quarrel (feathers fly on every game: OnTalk), and sometimes one steals a bite from the other's bag
        private static bool Quarrel(Player me, bool my, Player o, bool tm)
        {
            CrossTalk.Who a = WhoOf(me, my), b = WhoOf(o, tm);
            string l1 = Line(a, b, "bond_enemy_open"), l2 = l1 != null ? Line(b, a, "bond_enemy_reply") : null;
            if (l2 == null) return false;
            List<CrossTalk.Line> lines = new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } };
            if (UnityEngine.Random.value < 0.6f) { string l3 = Line(a, b, "bond_enemy_last"); if (l3 != null) lines.Add(new CrossTalk.Line { Who = 0, To = 1, Text = l3 }); }
            CrossTalk.Script("benemy", new List<CrossTalk.Who> { a, b }, lines);
            AfterQuarrel(my, o, tm); // (your two may talk that one over a little later)
            if (UnityEngine.Random.value < StealChance)
            {
                if (UnityEngine.Random.value < 0.5f) AskSteal(o, my);           // mine steals from them: their game takes it out
                else Robbed(me, tm, o.GetPlayerName(), PeerOf(o)); // theirs steals from me (their game is told)
            }
            return true;
        }

        // the feathers of a quarrel, over both ravens (on every game that sees the talk)
        private static void Squabble(List<CrossTalk.Who> whos)
        {
            foreach (CrossTalk.Who w in whos)
            {
                if (w.P == null) continue;
                GameObject model = Shoulder.ModelOf(w.P, w.Munin);
                Vector3 at = model != null ? model.transform.position : w.P.transform.position + Vector3.up * 1.6f;
                BeastLook.Puffs(Feathers.Feather(), at, ParticleSystemShapeType.Sphere, 0.15f, 10, 0.08f, 0.16f, 1.6f, 1.6f, 1.2f, w.Munin ? new Color(0.25f, 0.2f, 0.2f) : new Color(0.15f, 0.17f, 0.22f));
                ShoulderLife life = model != null ? model.GetComponent<ShoulderLife>() : null;
                if (life != null) { life.Act("Наїжачився"); }
            }
            if (whos.Count > 0 && whos[0].P != null) ScreamFx.Caw(whos[0].P, whos[0].Munin, whos[0].Munin ? 0.9f : 1.3f, 0.6f);
            if (whos.Count > 1 && whos[1].P != null) ScreamFx.Caw(whos[1].P, whos[1].Munin, whos[1].Munin ? 0.85f : 1.25f, 0.6f);
        }

        private static long PeerOf(Player o) { ZNetView nv = o != null ? o.GetComponent<ZNetView>() : null; return nv != null && nv.IsValid() ? nv.GetZDO().GetOwner() : 0L; }

        // ---------------------------------------------------------------- a theft between enemies

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(StealRpc, new Action<long, ZPackage>(OnAskSteal));
            ZRoutedRpc.instance.Register<ZPackage>(StolenRpc, new Action<long, ZPackage>(OnStolen));
        }

        // my raven (thiefMunin) wants a bite from that player's bag: their game decides and takes it out
        private static void AskSteal(Player victim, bool thiefMunin)
        {
            long peer = PeerOf(victim);
            Player me = Player.m_localPlayer;
            if (peer == 0L || me == null || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(me.GetPlayerName());
            pkg.Write(thiefMunin);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, StealRpc, pkg);
        }

        private static readonly Dictionary<long, float> StealAt = new Dictionary<long, float>();

        // their raven steals from me — asked by their game: only from a raven my ravens really are at odds with, near
        private static void OnAskSteal(long sender, ZPackage pkg)
        {
            pkg.ReadString(); // (the sender's name: not trusted, the sender is)
            bool thiefMunin = pkg.ReadBool();
            Player me = Player.m_localPlayer, o = BySender(sender);
            string who = o != null ? o.GetPlayerName() : null;
            if (me == null || o == null || Vector3.Distance(o.transform.position, me.transform.position) > Near * 2f) return;
            float last;
            if (StealAt.TryGetValue(sender, out last) && Time.time - last < 60f) return; // (once a minute from one game at most)
            StealAt[sender] = Time.time;
            if (StealAt.Count > 16) StealAt.Clear();
            bool odds = false;
            foreach (bool m in new[] { false, true }) if (RavenSeat.On(me, m) != null && Score(me, m, o, thiefMunin) <= EnemyAt * 0.5f) odds = true;
            if (!odds) return;
            Robbed(me, thiefMunin, who, sender);
        }

        // a raven of that player (thiefMunin) takes the best bite of my bag it eats; my ravens are cross; its game is told
        private static void Robbed(Player me, bool thiefMunin, string thiefOwner, long peer)
        {
            Trust.Food pick = null;
            foreach (Trust.Food f in Trust.Foods(thiefMunin))
                if (Trust.Have(me, f.Prefab) > 0 && (pick == null || f.Class > pick.Class || (f.Class == pick.Class && f.Points > pick.Points))) pick = f;
            if (pick == null) return;
            me.GetInventory().RemoveItem(Trust.FindName(me, pick.Prefab), 1, -1, false);
            string said = Lang.F("{0} гравця {1} поцупив у тебе: {2}", Plugin.RavenName(thiefMunin), thiefOwner, pick.Name);
            RavenMsg.Center(me, said);
            me.Message(MessageHud.MessageType.TopLeft, said);
            Player o = Find(thiefOwner);
            if (o != null) People.OnStole(o);
            if (o != null)
                foreach (bool m in new[] { false, true }) if (RavenSeat.On(me, m) != null) { Change(me, m, o, thiefMunin, -StealLoss); BondLog.Note(RavenSeat.On(me, m), o, thiefMunin, BondLog.StoleUs); }
            // one of mine says what it thinks of that
            bool mine = Free(me, true) && (!Free(me, false) || UnityEngine.Random.value < 0.6f);
            if (o != null && Free(me, mine))
            {
                Dictionary<string, string> v = new Dictionary<string, string>(); v["food"] = pick.Name;
                string line = Line(WhoOf(me, mine), WhoOf(o, thiefMunin), "bond_robbed", v);
                if (line != null) RavenSkills.SayFromShoulder(me, mine, line);
            }
            if (ZRoutedRpc.instance != null && peer != 0L && peer != ZDOMan.GetSessionID())
            {
                ZPackage pkg = new ZPackage();
                pkg.Write(me.GetPlayerName());
                pkg.Write(thiefMunin);
                pkg.Write(pick.Name);
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, StolenRpc, pkg);
            }
        }

        // the player whose game sent this (not a name from the packet: names can be alike or made up)
        private static Player BySender(long sender)
        {
            foreach (Player x in Player.GetAllPlayers()) if (x != null && x != Player.m_localPlayer && JointPlay.PeerOf(x) == sender) return x;
            return null;
        }

        // my raven got its bite: it is pleased with itself
        private static void OnStolen(long sender, ZPackage pkg)
        {
            pkg.ReadString(); // (the robbed one's name: not trusted, the sender is)
            bool thiefMunin = pkg.ReadBool();
            string food = pkg.ReadString();
            Player me = Player.m_localPlayer, o = BySender(sender);
            ItemDrop.ItemData r = me != null ? RavenSeat.On(me, thiefMunin) : null;
            if (r == null || o == null) return;
            Satiety.Change(r, 8f);
            Nicks.OnTheft(me, thiefMunin);
            foreach (bool tm in new[] { false, true }) if (RidOf(o, tm).Length > 0) BondLog.Note(r, o, tm, BondLog.StoleThem); // (from that player's bag: both its ravens saw it)
            Dictionary<string, string> v = new Dictionary<string, string>(); v["food"] = food;
            string line = Line(WhoOf(me, thiefMunin), WhoOf(o, false), "bond_steal", v);
            if (line != null) RavenSkills.SayFromShoulder(me, thiefMunin, line);
        }

        // ---------------------------------------------------------------- a friend's visit

        private sealed class Stay
        {
            public bool Munin; public Player To; public bool ToMunin; public PerchView Spot; public float Until, From; public bool Landed;
            public bool Long, Share; public float Hp = -1f; public readonly List<string> Saw = new List<string>(); // a visit of its own: what it saw there
        }
        // after a visit of its own, back home: what it tells you (a moment after it lands)
        private sealed class Tale { public bool Munin; public Player Of; public bool OfMunin; public List<string> Saw; public float At, Until; }
        private static Tale _tale;
        private static readonly float[] VisitNext = { -1f, -1f };
        private static Stay _visit;

        private static bool StartVisit(Player me, bool my, Player o, bool tm, bool longStay = false)
        {
            if (_visit != null || Victim.Busy) return false;
            PerchView v;
            string why = SitOn.Go(me, my, o, false, -1, out v);
            if (why != null || v == null) return false;
            _visit = new Stay { Munin = my, To = o, ToMunin = tm, Spot = v, From = Time.time, Until = Time.time + 20f, Long = longStay };
            return true;
        }

        internal static bool Visiting { get { return _visit != null; } }

        // a bite for a hungry friend (Share): the same visit, short; handed over once it sits there
        internal static bool StartShare(Player me, bool my, Player o, bool tm)
        {
            if (!StartVisit(me, my, o, tm)) return false;
            _visit.Share = true;
            return true;
        }

        // a visit of its own: a raven of mine, free, now and then (VisitEvery) goes to a friend of it near (VisitRange)
        private static void MaybeVisit(Player me)
        {
            if (_visit != null || _tale != null || Victim.Busy || (Senses.Ready && (Senses.Fight || Senses.Sleeping || Senses.Underground || Senses.Swimming))) return;
            foreach (bool my in new[] { false, true })
            {
                int i = my ? 1 : 0;
                if (VisitNext[i] < 0f) { VisitNext[i] = Time.time + UnityEngine.Random.Range(VisitEveryMin, VisitEveryMax) * 60f; continue; }
                if (Time.time < VisitNext[i] || !Free(me, my)) continue;
                VisitNext[i] = Time.time + UnityEngine.Random.Range(VisitEveryMin, VisitEveryMax) * 60f;
                if (UnityEngine.Random.value > OwnVisitChance) continue;
                Player best = null; bool bestM = false; float bestS = FriendAt;
                foreach (Player o in Player.GetAllPlayers())
                {
                    if (o == null || o == me || o.IsDead() || Shadow.Flagged(o) || Vector3.Distance(o.transform.position, me.transform.position) > VisitRange) continue;
                    foreach (bool tm in new[] { false, true })
                    {
                        if (Shoulder.ModelOf(o, tm) == null) continue;
                        float s = PairScore(me, my, o, tm);
                        if (s >= bestS) { bestS = s; best = o; bestM = tm; }
                    }
                }
                if (best != null && StartVisit(me, my, best, bestM, true)) return;
            }
        }

        // what it sees of that player while it sits there (every look: 5 s)
        private static void Watch(Stay s)
        {
            Player t = s.To;
            if (t == null) return;
            ZNetView nv = t.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (z != null && z.GetInt(ZDOVars.s_rightItem, 0) == HammerHash) Saw(s, "build");
            if (BaseAI.HaveEnemyInRange(t, t.transform.position, 10f)) Saw(s, "fight");
            if (t.InBed()) Saw(s, "sleep");
            float hp = t.GetHealthPercentage();
            if (s.Hp >= 0f && s.Hp - hp >= 0.15f) Saw(s, "hurt");
            s.Hp = hp;
        }
        private static void Saw(Stay s, string what) { if (!s.Saw.Contains(what)) s.Saw.Add(what); }
        private static readonly int HammerHash = "Hammer".GetStableHashCode();

        // the visit goes on: landed, it stays VisitSeconds; then home, and now and then your two speak of that friend
        private static void Visit(Player me)
        {
            Stay s = _visit;
            if (s == null) return;
            ItemDrop.ItemData item = RavenSeat.On(me, s.Munin);
            bool lost = item == null || s.Spot == null || !s.Spot.Valid || s.To == null || (!Perches.OnSpot(item) && Time.time - s.From > 6f);
            if (!lost && !s.Landed && Perches.OnSpot(item) && s.Spot.Settled(0))
            {
                s.Landed = true;
                s.Until = Time.time + (s.Share ? ShareStay : s.Long ? UnityEngine.Random.Range(LongMin, LongMax) : VisitSeconds);
                if (s.Share) { Shared(me, s); return; }
                BondLog.Note(item, s.To, s.ToMunin, BondLog.Visits);
                if (s.Long && UnityEngine.Random.value < 0.5f && !RavenSkills.Hushed(me) && Director.May(Director.Voice.Talk)) Hello(me, s); // a word with the friend there
            }
            if (!lost && s.Landed && s.Long) Watch(s);
            if (!lost && Time.time < s.Until && !(Senses.Ready && Senses.Fight)) return;
            _visit = null;
            if (item != null) Perches.CallQuiet(me, item, s.Munin);
            if (s.Share) return; // (a bite brought: said there already)
            if (!lost && s.Landed && s.Long && s.To != null) // home with what it saw: told a moment after it lands
            {
                _tale = new Tale { Munin = s.Munin, Of = s.To, OfMunin = s.ToMunin, Saw = s.Saw, At = Time.time + BackLineAfter, Until = Time.time + 90f };
                return;
            }
            if (lost || !s.Landed || s.To == null || UnityEngine.Random.value > PairTalkChance) return;
            // your two, in their bubble, of that friend
            bool other = !s.Munin;
            if (!Free(me, other)) return;
            CrossTalk.Who a = WhoOf(me, s.Munin), b = WhoOf(me, other), f = WhoOf(s.To, s.ToMunin);
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["raven"] = Nicks.Name(f.P, f.Munin); v["friend"] = f.P.GetPlayerName();
            string l1 = PairLine(a, "bond_pair_open", v), l2 = l1 != null ? PairLine(b, "bond_pair_reply", v) : null;
            if (l2 == null) return;
            List<TalkBubble.Line> lines = new List<TalkBubble.Line> { new TalkBubble.Line { Munin = a.Munin, Text = l1 }, new TalkBubble.Line { Munin = b.Munin, Text = l2 } };
            TalkBubble.Play(me, lines, true);
            RavenTalk.Send(me, lines);
            Director.Spoke(Director.Voice.Talk, TalkBubble.Pace(l1) + TalkBubble.Pace(l2) + 2f);
        }

        // there by the hungry friend: the bite handed over, a word each (seen by everyone near)
        private static void Shared(Player me, Stay s)
        {
            if (s.To == null || !Share.HungryOn(s.To, s.ToMunin)) return; // fed meanwhile: it just sat by its friend a moment, its bite kept
            Share.Hand(me, s.Munin, s.To, s.ToMunin);
            Change(me, s.Munin, s.To, s.ToMunin, ShareGiverGain);
            BondLog.Note(RavenSeat.On(me, s.Munin), s.To, s.ToMunin, BondLog.FedThem);
            if (RavenSkills.Hushed(me) || !Director.May(Director.Voice.Talk)) return;
            CrossTalk.Who a = WhoOf(me, s.Munin), b = WhoOf(s.To, s.ToMunin);
            string l1 = Line(a, b, "bond_share_give"), l2 = l1 != null ? Line(b, a, "bond_share_thanks") : null;
            if (l2 == null) return;
            CrossTalk.Script("bfriend", new List<CrossTalk.Who> { a, b }, new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } });
        }

        // there: a word with the friend it came to (both seen by everyone near)
        private static void Hello(Player me, Stay s)
        {
            CrossTalk.Who a = WhoOf(me, s.Munin), b = WhoOf(s.To, s.ToMunin);
            string l1 = Line(a, b, "bond_visit_open"), l2 = l1 != null ? Line(b, a, "bond_visit_reply") : null;
            if (l2 == null) return;
            CrossTalk.Script("bfriend", new List<CrossTalk.Who> { a, b }, new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } });
        }

        // A visit to a friend raven sitting on a perch (Guests), its owner here: the same scene as a visit to the shoulder
        // (bond_visit_open / _reply), noted in the book, the friendship grown as by a talk. level: the friend's own trust.
        internal static bool PerchVisit(Player me, bool my, Player o, bool tm, int level, out string l1, out string l2)
        {
            CrossTalk.Who a = WhoOf(me, my), b = new CrossTalk.Who { P = o, Munin = tm, Level = Mathf.Max(1, level) };
            l1 = Line(a, b, "bond_visit_open");
            l2 = l1 != null ? Line(b, a, "bond_visit_reply") : null;
            BondLog.Note(RavenSeat.On(me, my), o, tm, BondLog.Visits);
            Change(me, my, o, tm, TalkGain);
            return l2 != null;
        }

        // home from a friend on a perch: the gossip of that player's real doings, or just that it was there
        internal static string PerchTale(Player me, bool my, Player o, bool tm)
        {
            if (o == null || Plugin.Instance == null) return null;
            Dictionary<string, string> v = new Dictionary<string, string>();
            Gossip.Fill(v, o, tm, "g_their_", Plugin.Instance.PhraseDir);
            return Line(WhoOf(me, my), WhoOf(o, tm), "bond_visit_back", v);
        }

        // back home: what it saw there (now and then), else the gossip of that player's real doings, else just that it was there
        private static void Tell(Player me)
        {
            Tale t = _tale;
            if (t == null || Time.time < t.At) return;
            if (Time.time > t.Until || t.Of == null || !Free(me, t.Munin)) { _tale = null; return; }
            if (RavenSkills.Hushed(me) || !Director.May(Director.Voice.Shoulder)) { t.At = Time.time + 5f; return; }
            _tale = null;
            Dictionary<string, string> v = new Dictionary<string, string>();
            if (Plugin.Instance != null) Gossip.Fill(v, t.Of, t.OfMunin, "g_their_", Plugin.Instance.PhraseDir);
            CrossTalk.Who a = WhoOf(me, t.Munin), f = WhoOf(t.Of, t.OfMunin);
            string line = null;
            if (t.Saw.Count > 0 && UnityEngine.Random.value < 0.6f) line = Line(a, f, "bond_visit_back_" + t.Saw[UnityEngine.Random.Range(0, t.Saw.Count)], v);
            if (line == null) line = Line(a, f, "bond_visit_back", v);
            if (line == null) return;
            RavenSkills.SayFromShoulder(me, t.Munin, line);
            Director.Spoke(Director.Voice.Shoulder, RavenSpeech.ReadSeconds(line));
        }

        private static string PairLine(CrossTalk.Who s, string tag, Dictionary<string, string> v)
        {
            if (Plugin.Instance == null) return null;
            Dictionary<string, string> w = new Dictionary<string, string>(v);
            w["name"] = s.P.GetPlayerName();
            return Composer.ComposeCross(Plugin.Instance.PhraseDir, s.Munin, tag, s.Level, Grammar.Female(s.P), false, w, Voice.CodeOn(s.P, s.Munin));
        }

        // ---------------------------------------------------------------- the console: raven bond [<player> [h|m] [<score>|friend|enemy]]
        internal static string Cmd(Player me, string[] a)
        {
            if (me == null) return Lang.T("Нема гравця.");
            if (a.Length < 2)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Player o in Player.GetAllPlayers())
                {
                    if (o == null || o == me) continue;
                    foreach (bool my in new[] { false, true })
                        foreach (bool tm in new[] { false, true })
                        {
                            if (RavenSeat.On(me, my) == null || RidOf(o, tm).Length == 0) continue;
                            float s = Score(me, my, o, tm), pr = PairScore(me, my, o, tm);
                            sb.Append(Plugin.RavenName(my)).Append(" → ").Append(o.GetPlayerName()).Append(' ').Append(Plugin.RavenName(tm)).Append(": ")
                              .Append(Mathf.RoundToInt(s)).Append(" (разом ").Append(Mathf.RoundToInt(pr)).Append(", ").Append(StateOf(pr)).Append(")\n");
                        }
                }
                return sb.Length > 0 ? sb.ToString().TrimEnd() : Lang.T("Поруч нема чужих воронів (або твої не на плечі).");
            }
            Player t = Find(a[1]);
            if (t == null) return Lang.F("Нема гравця «{0}» поруч.", a[1]);
            bool their = a.Length > 2 && a[2].ToLowerInvariant().StartsWith("m");
            bool mine = Free(me, true) && !Free(me, false);
            if (a.Length > 3)
            {
                string w = a[3].ToLowerInvariant();
                float target = w == "friend" ? 60f : w == "enemy" ? -60f : 0f;
                float n; if (w != "friend" && w != "enemy" && float.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) target = n;
                foreach (bool my in new[] { false, true }) if (RavenSeat.On(me, my) != null) Change(me, my, t, their, target - Score(me, my, t, their));
                PairNext.Clear();
                return Lang.F("Твої ворони до {0}: {1}.", Plugin.RavenName(their), Mathf.RoundToInt(target));
            }
            State st = StateOf(PairScore(me, mine, t, their));
            bool ok = st == State.Enemy ? Quarrel(me, mine, t, their) : Friends(me, mine, t, their);
            return ok ? Lang.F("Момент: {0}.", st) : Lang.T("Не вийшло (фраз нема чи ворон зайнятий).");
        }
    }
}
#endif

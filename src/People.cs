#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Терпимість» (the user, 2026-10-23): how a raven of yours takes to each other player — not their ravens, the people.
    //   - Kept in the raven (Hugin and Munin each their own), like its bonds with ravens: a score toward each player it
    //     has been near, from where it started (its character × the player's manner, and a chemistry of the two) and
    //     moved by what that player does around you; each game day it drifts a little back to the start.
    //   - A player's manner (Style) is their own game's reckoning of their life (kills, buildings, roads, crafts, deaths),
    //     told to the others as one number on the player.
    //   - It can't stand someone: now and then it flies onto them and pecks (PeckDamage, never to death, no consent:
    //     their own game takes it off), it calls them names more often, and it picks them as its victim more often
    //     (Victim: «grudge»). It likes someone: a warm word now and then, an alarm when they are nearly dead.
    //   - A crossing (likes / can't stand / made up / cooled) is told in the corner, and the raven has its word.
    //   - The book shows the people beside the ravens it knows (RavenBookKin).
    // Cheap: one look a second at the players near (their emote count only), the rest every LookEvery seconds.
    internal static partial class People
    {
        internal static bool Enabled = true;
        internal static float LookEvery = 5f, Near = 25f, PeckRange = 8f, PeckDamage = 10f;
        internal static float PeckMinMinutes = 10f, PeckMaxMinutes = 20f, PeckChanceHate = 0.6f, PeckChanceDislike = 0.3f;
        internal static float JabHateMin = 6f, JabHateMax = 12f, JabDislikeMin = 12f, JabDislikeMax = 20f, WarmMin = 15f, WarmMax = 25f;
        internal static float GrudgeChance = 0.35f, AlarmGap = 180f, DayDrift = 0.15f, PeckReach = 40f;
        internal const float HateAt = -70f, DislikeAt = -40f, RespectAt = 40f, AdoreAt = 75f;
        internal const string Key = "RavenWatch_people", StyleKey = "RavenWatch_Style";
        private const string PeckRpc = "RavenWatch_PeoplePeck", ShookRpc = "RavenWatch_PeopleShook";
        private const int Most = 40;

        // what they went through (the book's story counts them)
        internal const int Fought = 0, Boss = 1, Fire = 2, Travel = 3, Ship = 4, Friendly = 5, Guarded = 6, FedUs = 7,
            Hit = 8, Grave = 9, Shook = 10, Mocked = 11, Stole = 12, Rude = 13, PeckedUs = 14, Pecked = 15, Thought = 16, Counters = 17;

        // a player's manner
        internal const int Warrior = 0, Builder = 1, Roamer = 2, Keeper = 3, Daredevil = 4;
        private static readonly string[] StyleSrc = Lang.Keys("Вояк", "Будівничий", "Блукач", "Господар", "Шибайголова");
        internal static string StyleName(int s) { return s >= 0 && s < StyleSrc.Length ? Lang.T(StyleSrc[s]) : ""; }

        internal enum State { Hate, Dislike, Tolerate, Respect, Adore }
        internal static State StateOf(float s) { return s <= HateAt ? State.Hate : s <= DislikeAt ? State.Dislike : s < RespectAt ? State.Tolerate : s < AdoreAt ? State.Respect : State.Adore; }
        internal const int Hostile = 0, Neither = 1, Liked = 2;
        internal static int Group(State s) { return s <= State.Dislike ? Hostile : s >= State.Respect ? Liked : Neither; }

        // ---------------------------------------------------------------- the record in the raven

        internal sealed class Rec
        {
            public long Pid; public string Name = ""; public int Style = -1; public bool Female;
            public int First = -1, Last = -1, Told = -1, ToldDay = -1, Day = -1; // Day: the last day it drifted
            public float Score, Start; public bool Fresh;
            public readonly int[] N = new int[Counters];
            internal bool Shown { get { for (int i = 0; i < Counters; i++) if (N[i] > 0) return true; return false; } }
        }

        // "pid|name|style|female|first|last|score|start|told|toldDay|fresh|day|n0,n1,…;…", the latest met first
        private sealed class Parsed { public string Raw; public List<Rec> L; }
        private static readonly Dictionary<ItemDrop.ItemData, Parsed> Cache = new Dictionary<ItemDrop.ItemData, Parsed>();

        internal static List<Rec> Of(ItemDrop.ItemData item)
        {
            string raw = null;
            if (item != null && item.m_customData != null) item.m_customData.TryGetValue(Key, out raw);
            raw = raw ?? "";
            Parsed p;
            if (item != null && Cache.TryGetValue(item, out p) && p.Raw == raw) return p.L;
            List<Rec> l = new List<Rec>();
            CultureInfo ci = CultureInfo.InvariantCulture;
            if (raw.Length > 0)
                foreach (string e in raw.Split(';'))
                {
                    string[] f = e.Split('|');
                    if (f.Length < 13) continue;
                    Rec r = new Rec { Name = f[1], Female = f[3] == "1", Fresh = f[10] == "1" };
                    if (!long.TryParse(f[0], NumberStyles.Integer, ci, out r.Pid)) continue;
                    int.TryParse(f[2], NumberStyles.Integer, ci, out r.Style);
                    int.TryParse(f[4], NumberStyles.Integer, ci, out r.First);
                    int.TryParse(f[5], NumberStyles.Integer, ci, out r.Last);
                    float.TryParse(f[6], NumberStyles.Float, ci, out r.Score);
                    float.TryParse(f[7], NumberStyles.Float, ci, out r.Start);
                    int.TryParse(f[8], NumberStyles.Integer, ci, out r.Told);
                    int.TryParse(f[9], NumberStyles.Integer, ci, out r.ToldDay);
                    int.TryParse(f[11], NumberStyles.Integer, ci, out r.Day);
                    string[] n = f[12].Split(',');
                    for (int i = 0; i < n.Length && i < Counters; i++) int.TryParse(n[i], NumberStyles.Integer, ci, out r.N[i]);
                    l.Add(r);
                }
            if (item != null) { if (Cache.Count > 8) Cache.Clear(); Cache[item] = new Parsed { Raw = raw, L = l }; }
            return l;
        }

        private static void Write(ItemDrop.ItemData item, List<Rec> l)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < Most; i++)
            {
                Rec r = l[i];
                if (sb.Length > 0) sb.Append(';');
                sb.Append(r.Pid.ToString(ci)).Append('|').Append(r.Name.Replace("|", "").Replace(";", "")).Append('|').Append(r.Style.ToString(ci)).Append('|')
                  .Append(r.Female ? '1' : '0').Append('|').Append(r.First.ToString(ci)).Append('|').Append(r.Last.ToString(ci)).Append('|')
                  .Append(Mathf.RoundToInt(r.Score).ToString(ci)).Append('|').Append(Mathf.RoundToInt(r.Start).ToString(ci)).Append('|')
                  .Append(r.Told.ToString(ci)).Append('|').Append(r.ToldDay.ToString(ci)).Append('|').Append(r.Fresh ? '1' : '0').Append('|').Append(r.Day.ToString(ci)).Append('|');
                for (int k = 0; k < Counters; k++) { if (k > 0) sb.Append(','); sb.Append(r.N[k].ToString(ci)); }
            }
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[Key] = sb.ToString();
        }

        private static Rec Copy(Rec a)
        {
            Rec b = new Rec { Pid = a.Pid, Name = a.Name, Style = a.Style, Female = a.Female, First = a.First, Last = a.Last, Told = a.Told, ToldDay = a.ToldDay, Day = a.Day, Score = a.Score, Start = a.Start, Fresh = a.Fresh };
            Array.Copy(a.N, b.N, Counters);
            return b;
        }

        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }

        internal static Rec Find(ItemDrop.ItemData item, long pid) { foreach (Rec r in Of(item)) if (r.Pid == pid) return r; return null; }

        // ---------------------------------------------------------------- where a raven starts toward someone

        // its character and their manner: liked, can't stand, or nothing; a watchful or devoted one a little cooler to all
        private static float Base(string ch, int style, string rid, long pid)
        {
            float s = 0f;
            int like = -1, hate = -1;
            switch (ch)
            {
                case "homebody": like = Builder; hate = Roamer; break;
                case "wanderer": like = Roamer; hate = Builder; break;
                case "fidget": like = Daredevil; hate = Keeper; break;
                case "calm": like = Keeper; hate = Daredevil; break;
                case "watchful": like = Warrior; hate = Daredevil; s -= 10f; break;
                case "bold": like = Warrior; hate = Keeper; break;
                case "devoted": s -= 5f; break;
                case "curious": like = Roamer; hate = Keeper; break;
                case "proud": like = Builder; hate = Daredevil; break;
                case "tender": like = Keeper; s += 10f; break;
                case "grumpy": like = Keeper; hate = Daredevil; s -= 10f; break;
                case "huntsman": like = Warrior; hate = Builder; break;
                case "sweettooth": like = Builder; hate = Roamer; break;
            }
            if (style >= 0 && style == like) s += 25f;
            if (style >= 0 && style == hate) s -= 25f;
            int h = (rid + "/" + pid.ToString(CultureInfo.InvariantCulture)).GetStableHashCode();
            s += ((h & 0xFFFF) / 65535f * 2f - 1f) * 15f; // the two of them: a chemistry of their own
            return Mathf.Clamp(s, -60f, 60f);
        }

        // how much a deed moves this character: a fidget either way, a calm one hardly by the bad, a watchful one the bad
        // more and the good less; a bold one and a devoted one by a fight beside you
        private static float Mul(string ch, float by, int what)
        {
            if (by > 0f)
            {
                if (ch == "fidget") return 1.3f;
                if (ch == "watchful") return 0.7f;
                if (ch == "bold" && what == Fought) return 1.5f;
                if (ch == "devoted" && (what == Fought || what == Guarded)) return 2f;
                if (ch == "tender") return 1.3f;
                if (ch == "grumpy") return 0.8f;
                if (ch == "huntsman" && what == Fought) return 1.5f;
                if (ch == "sweettooth") return 1.2f;
                return 1f;
            }
            if (ch == "fidget") return 1.3f;
            if (ch == "calm") return 0.6f;
            if (ch == "watchful") return 1.5f;
            if (ch == "grumpy") return 1.3f;
            if (ch == "tender" || ch == "proud") return 1.2f;
            return 1f;
        }

        private static string CharId(ItemDrop.ItemData item) { Norov.Character c = Norov.CharOf(item); return c != null ? c.Id : ""; }

        // ---------------------------------------------------------------- a deed of that player toward you

        // my raven (myMunin) takes this from that player: by (before its character's measure), what (a counter, or -1)
        internal static void Change(ItemDrop.ItemData mine, bool myMunin, Player o, float by, int what)
        {
            Player me = Player.m_localPlayer;
            if (!Enabled || mine == null || o == null || me == null || o == me || Dummy.Is(o) || !Trust.Active(mine) || Trust.Stranger(mine, me)) return;
            long pid = o.GetPlayerID();
            if (pid == 0L) return;
            List<Rec> l = new List<Rec>(Of(mine));
            int at = l.FindIndex(x => x.Pid == pid);
            Rec r = at >= 0 ? Copy(l[at]) : null;
            if (at >= 0) l.RemoveAt(at);
            string ch = CharId(mine);
            int style = StyleOf(o);
            if (r == null) { r = new Rec { Pid = pid, First = Today, Day = Today }; r.Start = Base(ch, style, Backup.Id(mine) ?? "", pid); r.Score = r.Start; }
            else if (style != r.Style) r.Start = Base(ch, style, Backup.Id(mine) ?? "", pid); // a new manner: a new start, reached by the drift (no jump)
            r.Name = o.GetPlayerName(); r.Style = style; r.Female = Grammar.Female(o); r.Last = Today;
            if (r.Day != Today) { r.Score = Mathf.Lerp(r.Score, r.Start, DayDrift * Mathf.Clamp(Today - r.Day, 1, 5)); r.Day = Today; } // the days since: back toward the start
            if (by != 0f) r.Score = Mathf.Clamp(r.Score + by * Mul(ch, by, what), -100f, 100f);
            if (what >= 0 && what < Counters) r.N[what] = Mathf.Min(r.N[what] + 1, 9999);
            l.Insert(0, r);
            Write(mine, l);
            Check(me, myMunin, mine, o);
        }

        // every raven of mine (worn, awake) toward that player
        internal static void ChangeAll(Player o, float by, int what)
        {
            Player me = Player.m_localPlayer;
            if (me == null || o == null) return;
            foreach (bool m in new[] { false, true }) Change(RavenSeat.On(me, m), m, o, by, what);
        }

        internal static float ScoreOf(Player me, bool munin, Player o)
        {
            Rec r = o != null ? Find(RavenSeat.On(me, munin), o.GetPlayerID()) : null;
            return r != null ? r.Score : 0f;
        }
        internal static State StateTo(Player me, bool munin, Player o) { Rec r = o != null ? Find(RavenSeat.On(me, munin), o.GetPlayerID()) : null; return r != null && r.Shown ? StateOf(r.Score) : State.Tolerate; }

        // ---------------------------------------------------------------- the crossings: told

        private static void Check(Player me, bool myMunin, ItemDrop.ItemData mine, Player o)
        {
            List<Rec> l = Of(mine);
            int at = l.FindIndex(x => x.Pid == o.GetPlayerID());
            if (at < 0 || !l[at].Shown) return;
            Rec r = l[at];
            int g = Group(StateOf(r.Score));
            if (g == r.Told) return;
            if (r.Told < 0 && g == Neither) { Set(mine, at, g, false); return; }
            if (r.ToldDay == Today) return;
            int was = r.Told;
            Set(mine, at, g, true);
            string raven = Plugin.RavenName(myMunin), who = o.GetPlayerName(), text, tag;
            if (g == Liked) { text = Lang.F("{0} тепер поважає гравця {1}", raven, who); tag = "people_now_liked"; }
            else if (g == Hostile) { text = Lang.F("{0} терпіти не може гравця {1}", raven, who); tag = "people_now_hated"; }
            else if (was == Hostile) { text = Lang.F("{0} більше не тримає зла на гравця {1}", raven, who); tag = "people_made_up"; }
            else { text = Lang.F("{0} охолов до гравця {1}", raven, who); tag = "people_cooled"; }
            me.Message(MessageHud.MessageType.TopLeft, text, 0, RavenItem.Icon(myMunin));
            Say(me, myMunin, o, tag, Director.Voice.Shoulder);
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
        internal static void Seen(ItemDrop.ItemData item)
        {
            if (!AnyFresh(item)) return;
            List<Rec> l = new List<Rec>();
            foreach (Rec r in Of(item)) { Rec c = Copy(r); c.Fresh = false; l.Add(c); }
            Write(item, l);
        }

        // a line of my raven about that player (their name {friend}, their forms {>він|вона}); false: nothing said
        private static bool Say(Player me, bool munin, Player o, string tag, Director.Voice voice)
        {
            if (Plugin.Instance == null || RavenSkills.Hushed(me) || !Director.May(voice)) return false;
            ItemDrop.ItemData item = RavenSeat.On(me, munin);
            if (item == null) return false;
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["name"] = me.GetPlayerName(); v["friend"] = o.GetPlayerName();
            string line = Composer.ComposeCross(Plugin.Instance.PhraseDir, munin, tag, Mathf.Max(1, Trust.LevelWorn(munin)), Grammar.Female(me), Grammar.Female(o), v, Voice.Code(item));
            if (string.IsNullOrEmpty(line)) return false;
            RavenSkills.SayFromShoulder(me, munin, line);
            Director.Spoke(voice, RavenSpeech.ReadSeconds(line));
            return true;
        }

        // ---------------------------------------------------------------- a player's manner (their own game reckons it)

        internal static int StyleOf(Player o)
        {
            ZDO z = NetFlag.Zdo(o, false);
            return z != null ? z.GetInt(StyleKey, -1) : -1;
        }

        private static float _styleAt;
        private static void PublishStyle(Player me)
        {
            if (Time.time < _styleAt) return;
            _styleAt = Time.time + 120f;
            PlayerProfile prof = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            ZDO z = NetFlag.Zdo(me, true);
            if (prof == null || z == null) return;
            float[] w =
            {
                Stats.Get(prof, PlayerStatType.EnemyKills) / 150f,
                Stats.Get(prof, PlayerStatType.BuiltPieces) / 400f,
                (Stats.Get(prof, PlayerStatType.DistanceTraveled) + Stats.Get(prof, PlayerStatType.DistanceSail)) / 25000f,
                (Stats.Get(prof, PlayerStatType.CraftsOrUpgrades) + Stats.Get(prof, PlayerStatType.HarvestBerry) / 4f + Stats.Get(prof, PlayerStatType.HarvestCrop) / 2f + Stats.Get(prof, PlayerStatType.FishCaught)) / 150f,
                Stats.Get(prof, PlayerStatType.Deaths) / 12f,
            };
            int best = -1; float bw = 0.3f; // (a life barely begun: no manner yet)
            for (int i = 0; i < w.Length; i++) if (w[i] > bw) { bw = w[i]; best = i; }
            if (z.GetInt(StyleKey, -1) != best) z.Set(StyleKey, best);
        }

        // ---------------------------------------------------------------- the look at the players near

        private sealed class Near1
        {
            public int Emote = -1; public float Fight, Fire, Travel, Ship;
            public Vector3 Was; public bool Seen; // where they were at the last look (another player's own speed is not known here)
            public int TravelDay = -1, ShipDay = -1, FoughtDay = -1, FoughtToday, GrudgeDay = -1;
            public float GuardNext, EmoteNext, PeckNext = -1f, JabNext = -1f, WarmNext = -1f, AlarmNext;
        }
        private static readonly Dictionary<long, Near1> Seen1 = new Dictionary<long, Near1>();
        private static float _emoteAt, _lookAt;

        private static Near1 Of1(Player o)
        {
            long id = o.GetPlayerID();
            Near1 n;
            if (!Seen1.TryGetValue(id, out n)) { if (Seen1.Count > 32) Seen1.Clear(); n = new Near1(); Seen1[id] = n; }
            return n;
        }

        // From the sync (RavenSkills, twice a second)
        internal static void Tick(Player me)
        {
            if (me == null) return;
            PeckRun(me);
            if (!Enabled || me.IsDead()) return;
            PublishStyle(me);
            float now = Time.time;
            if (now >= _emoteAt) { _emoteAt = now + 1f; Emotes(me); }
            if (now < _lookAt) return;
            _lookAt = now + LookEvery;
            if (RavenSeat.On(me, false) == null && RavenSeat.On(me, true) == null) return;
            Ship ship = me.GetStandingOnShip();
            bool moving = me.GetVelocity().magnitude > 2f;
            int today = Today;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || o.IsDead() || Dummy.Is(o) || o.GetPlayerID() == 0L) continue;
                float d = Vector3.Distance(o.transform.position, me.transform.position);
                if (d > Near) continue;
                Near1 n = Of1(o);
                if (!n.Seen) Back(me, o); // (in sight again this session: one not met for days gets its word)
                // side by side in a fight, standing by you when you are nearly dead
                if (Senses.Ready && Senses.Fight && d <= 15f)
                {
                    n.Fight += LookEvery;
                    if (n.FoughtDay != today) { n.FoughtDay = today; n.FoughtToday = 0; }
                    if (n.Fight >= 30f && n.FoughtToday < 5) { n.Fight = 0f; n.FoughtToday++; ChangeAll(o, 2f, Fought); }
                    if (Senses.Health01 < 0.3f && d <= 10f && now >= n.GuardNext) { n.GuardNext = now + 120f; ChangeAll(o, 4f, Guarded); }
                }
                // a fire, a road, a ship together
                if (Cozy.Cosy && d <= 8f) { n.Fire += LookEvery; if (n.Fire >= 600f) { n.Fire = 0f; ChangeAll(o, 2f, Fire); } }
                bool going = n.Seen && Vector3.Distance(o.transform.position, n.Was) > 2f * LookEvery; // they move too, not only you
                n.Was = o.transform.position; n.Seen = true;
                if (moving && going && d <= 20f && n.TravelDay != today) { n.Travel += LookEvery; if (n.Travel >= 600f) { n.Travel = 0f; n.TravelDay = today; ChangeAll(o, 3f, Travel); } }
                if (ship != null && o.GetStandingOnShip() == ship && n.ShipDay != today) { n.Ship += LookEvery; if (n.Ship >= 300f) { n.Ship = 0f; n.ShipDay = today; ChangeAll(o, 3f, Ship); } }
                if (!Shadow.On(me) && !Shadow.Flagged(o)) Act(me, o, n, d); // (Hugin's shadow: no peck or word that would give either of you away)
            }
        }

        // the emotes of the players near: friendly ones at your raven warm it, rude ones sour it (one look a second: their count)
        private static readonly HashSet<string> Kindly = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "wave", "bow", "kneel", "thumbsup", "cheer", "blowkiss", "loveyou", "toast" };
        private static readonly HashSet<string> Rude1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "roar", "challenge", "nonono", "point" }; // (not a laugh: they laugh at plenty besides the raven)
        private static void Emotes(Player me)
        {
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || o.GetPlayerID() == 0L) continue;
                float d = Vector3.Distance(o.transform.position, me.transform.position);
                if (d > Near) continue;
                ZDO z = NetFlag.Zdo(o, false);
                if (z == null) continue;
                Near1 n = Of1(o);
                int id = z.GetInt(ZDOVars.s_emoteID, 0);
                if (n.Emote < 0) { n.Emote = id; continue; }
                if (id == n.Emote) continue;
                n.Emote = id; // (kept up to date within Near: one made far off is not taken for a fresh one when they come close)
                if (d > 6f || Time.time < n.EmoteNext) continue;
                string e = z.GetString(ZDOVars.s_emote, "");
                bool good = Kindly.Contains(e), bad = Rude1.Contains(e);
                if (!good && !bad) continue;
                n.EmoteNext = Time.time + 180f;
                ChangeAll(o, good ? 2f : -2f, good ? Friendly : Rude);
            }
        }

        // what my ravens do about that player now (one thing at a time)
        private static void Act(Player me, Player o, Near1 n, float d)
        {
            float now = Time.time;
            foreach (bool m in new[] { true, false }) // (Munin first: he is the quicker to act on a grudge)
            {
                ItemDrop.ItemData item = RavenSeat.On(me, m);
                Rec r = item != null ? Find(item, o.GetPlayerID()) : null;
                if (r == null || !r.Shown || !Free(me, m)) continue;
                State st = StateOf(r.Score);
                if (st <= State.Dislike)
                {
                    bool hate = st == State.Hate;
                    if (n.PeckNext < 0f) n.PeckNext = now + UnityEngine.Random.Range(60f, 240f); // (just met again: not at once)
                    if (d <= PeckRange && now >= n.PeckNext && _peck == null && !o.InBed() && !BossNear(me))
                    {
                        n.PeckNext = now + UnityEngine.Random.Range(PeckMinMinutes, PeckMaxMinutes) * 60f;
                        if (UnityEngine.Random.value < (hate ? PeckChanceHate : PeckChanceDislike)) { Peck(me, m, o); return; }
                    }
                    if (hate && n.GrudgeDay != Today && !Victim.Busy && d <= Victim.Range)
                    {
                        n.GrudgeDay = Today;
                        if (UnityEngine.Random.value < GrudgeChance && Victim.Grudge(me, o)) return;
                    }
                    if (n.JabNext < 0f) n.JabNext = now + UnityEngine.Random.Range(60f, 300f);
                    if (d <= 12f && now >= n.JabNext)
                    {
                        n.JabNext = now + (hate ? UnityEngine.Random.Range(JabHateMin, JabHateMax) : UnityEngine.Random.Range(JabDislikeMin, JabDislikeMax)) * 60f;
                        if (Say(me, m, o, "people_jab", Director.Voice.Shoulder)) return;
                    }
                }
                else if (st >= State.Respect)
                {
                    if (o.GetHealthPercentage() < 0.3f && now >= n.AlarmNext)
                    {
                        n.AlarmNext = now + AlarmGap;
                        ScreamFx.CawAll(me, m, m ? 0.85f : 1.3f, 0.7f);
                        me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} тривожиться: {1} ледь тримається", Plugin.RavenName(m), o.GetPlayerName()), 0, RavenItem.Icon(m));
                        Say(me, m, o, "people_alarm", Director.Voice.Event);
                        return;
                    }
                    if (n.WarmNext < 0f) n.WarmNext = now + UnityEngine.Random.Range(120f, 600f);
                    if (d <= 10f && now >= n.WarmNext)
                    {
                        n.WarmNext = now + UnityEngine.Random.Range(WarmMin, WarmMax) * 60f;
                        if (Say(me, m, o, "people_warm", Director.Voice.Shoulder)) return;
                    }
                }
            }
        }

        // a raven of mine that may do something now: on the shoulder a moment already, awake, mine, here
        private static bool Free(Player me, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(me, munin);
            return r != null && Trust.Active(r) && !Trust.Stranger(r, me) && !Trust.Away(r) && !Perches.On(r) && Shoulder.Settled(me, munin, 3f) && Quirks.Sulk(me, munin) == null;
        }

        // a boss fight near: no pecking of anybody then
        private static bool BossNear(Player me)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && c.IsBoss() && !c.IsDead() && Vector3.Distance(c.transform.position, me.transform.position) < 50f) return true;
            return false;
        }

        // ---------------------------------------------------------------- the peck

        private sealed class Run1 { public bool Munin; public Player To; public PerchView Spot; public float From, Until = -1f; public bool Done; }
        private static Run1 _peck;

        // onto their head and a peck (a free flight of «Сісти тут» onto a player); where it may not sit (their «not on me»):
        // a lunge from your shoulder and the peck all the same
        private static void Peck(Player me, bool munin, Player o)
        {
            ScreamFx.CawAll(me, munin, munin ? 0.8f : 1.25f, 0.7f);
            PerchView v;
            string why = SitOn.Go(me, munin, o, false, SitOn.Head, out v);
            if (why == null && v != null) { _peck = new Run1 { Munin = munin, To = o, Spot = v, From = Time.time }; return; }
            ShoulderLife life = Shoulder.LifeOf(me, munin);
            if (life != null) life.Act("Штурхає");
            Hand(me, munin, o);
        }

        private static void PeckRun(Player me)
        {
            Run1 s = _peck;
            if (s == null) return;
            ItemDrop.ItemData item = RavenSeat.On(me, s.Munin);
            bool on = item != null && Perches.OnSpot(item);
            if (!s.Done && s.To != null && on && s.Spot != null && s.Spot.Valid && s.Spot.Settled(0)) { s.Done = true; s.Until = Time.time + 2.5f; Hand(me, s.Munin, s.To); return; }
            if ((s.Done && Time.time < s.Until) || (!s.Done && Time.time - s.From < 15f && s.To != null && !s.To.IsDead())) return;
            _peck = null;
            if (item != null && on) Perches.CallQuiet(me, item, s.Munin);
        }

        // the peck itself: their game takes it off (never to death), my raven says its word
        private static void Hand(Player me, bool munin, Player o)
        {
            long peer = JointPlay.PeerOf(o);
            if (peer != 0L && ZRoutedRpc.instance != null)
            {
                ZPackage pkg = new ZPackage();
                pkg.Write(PeckDamage);
                pkg.Write(munin);
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, PeckRpc, pkg);
            }
            Change(RavenSeat.On(me, munin), munin, o, 0f, Pecked);
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} дзьобнув гравця {1}", Plugin.RavenName(munin), o.GetPlayerName()), 0, RavenItem.Icon(munin));
            Say(me, munin, o, "people_peck", Director.Voice.Event);
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(PeckRpc, new Action<long, ZPackage>(OnPeck));
            ZRoutedRpc.instance.Register<ZPackage>(ShookRpc, new Action<long, ZPackage>(OnShook));
        }

        private static Player BySender(long sender)
        {
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o != Player.m_localPlayer && JointPlay.PeerOf(o) == sender) return o;
            return null;
        }

        private static readonly Dictionary<long, float> PeckedAt = new Dictionary<long, float>();

        // a raven of that player pecked me: a little health (never to death), and my ravens remember who did it
        private static void OnPeck(long sender, ZPackage pkg)
        {
            float dmg = Mathf.Clamp(pkg.ReadSingle(), 0f, PeckDamage);
            bool munin = pkg.ReadBool();
            Player me = Player.m_localPlayer, o = BySender(sender);
            if (me == null || o == null || me.IsDead() || Vector3.Distance(o.transform.position, me.transform.position) > PeckReach) return; // (its raven may have ridden on you a while)
            float last;
            if (PeckedAt.TryGetValue(sender, out last) && Time.time - last < 60f) return; // (one player's ravens: once a minute at most)
            PeckedAt[sender] = Time.time;
            if (PeckedAt.Count > 16) PeckedAt.Clear();
            float hp = me.GetHealth(), took = Mathf.Min(dmg, hp - 1f);
            if (took > 0f)
            {
                me.SetHealth(hp - took);
                if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, me.GetTopPoint(), took, true);
            }
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} гравця {1} дзьобнув тебе", Plugin.RavenName(munin), o.GetPlayerName()), 0, RavenItem.Icon(munin));
            ChangeAll(o, -5f, PeckedUs);
        }

        // shaken off by a roll (SitOn.Shake): that player shook my raven off them
        internal static void SendShook(Perches.Occupant o)
        {
            if (o == null || ZRoutedRpc.instance == null || o.Uid == 0L) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(o.Munin);
            ZRoutedRpc.instance.InvokeRoutedRPC(o.Uid, ShookRpc, pkg);
        }

        private static void OnShook(long sender, ZPackage pkg)
        {
            bool munin = pkg.ReadBool();
            Player me = Player.m_localPlayer, o = BySender(sender);
            if (me == null || o == null || _peck != null && _peck.To == o) return; // (shaken off while pecking them: fair enough)
            Change(RavenSeat.On(me, munin), munin, o, -4f, Shook);
        }

        // ---------------------------------------------------------------- the hooks

        private static readonly Dictionary<long, float> HitAt = new Dictionary<long, float>();
        internal static void OnHit(Player attacker)
        {
            if (attacker == null) return;
            float last;
            long id = attacker.GetPlayerID();
            if (HitAt.TryGetValue(id, out last) && Time.time - last < 3f) return;
            HitAt[id] = Time.time;
            if (HitAt.Count > 16) HitAt.Clear();
            ChangeAll(attacker, -8f, Hit);
        }

        // «Думка» about the player you look at (AboutTalk): thinking of them sets the raven's mind a little — what the
        // thought was about (a blow from them, envy: worse; old pals, a friend raven: better), else it leans further
        // the way it already leans
        internal static void OnThought(Player o, bool munin, string theme)
        {
            Player me = Player.m_localPlayer;
            if (me == null || o == null) return;
            float by;
            switch (theme)
            {
                case "about_hityou": case "about_envy": case "about_hate": case "about_dislike": by = -3f; break;
                case "about_pals": case "about_bond_friend": case "about_since": case "about_respect": case "about_adore": case "about_chase_lead": by = 2f; break;
                default: { float s = ScoreOf(me, munin, o); by = s > 10f ? 1f : s < -10f ? -1f : 0f; break; }
            }
            Change(RavenSeat.On(me, munin), munin, o, by, Thought);
        }

        // the theme of the thought about that player by how this raven takes to them (AboutTalk), or null
        internal static string AboutTheme(Player me, bool munin, Player o)
        {
            Rec r = o != null ? Find(RavenSeat.On(me, munin), o.GetPlayerID()) : null;
            if (r == null || !r.Shown) return null;
            switch (StateOf(r.Score)) { case State.Hate: return "about_hate"; case State.Dislike: return "about_dislike"; case State.Respect: return "about_respect"; case State.Adore: return "about_adore"; default: return null; }
        }

        internal static void OnGrave(Player thief) { ChangeAll(thief, -25f, Grave); }
        internal static void OnMocked(Player o) { ChangeAll(o, -6f, Mocked); }
        internal static void OnStole(Player o) { ChangeAll(o, -8f, Stole); }
        internal static void OnFed(ItemDrop.ItemData mine, bool munin, Player o) { Change(mine, munin, o, 4f, FedUs); }
        internal static void OnBondTold(Player me, bool my, Player o, bool friends) { Change(RavenSeat.On(me, my), my, o, friends ? 5f : -5f, -1); }

        // a boss fell: everybody who was there with you
        internal static void OnBoss()
        {
            Player me = Player.m_localPlayer;
            if (me == null) return;
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o != me && !o.IsDead() && Vector3.Distance(o.transform.position, me.transform.position) <= 30f) ChangeAll(o, 10f, Boss);
        }

        // ---------------------------------------------------------------- told in the book

        internal static string StateWord(State s)
        {
            switch (s)
            {
                case State.Hate: return Lang.T("ненавидить");
                case State.Dislike: return Lang.T("не терпить");
                case State.Respect: return Lang.T("поважає");
                case State.Adore: return Lang.T("обожнює");
                default: return Lang.T("терпить");
            }
        }

        internal static string Story(ItemDrop.ItemData mine, bool myMunin, Rec r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<b>").Append(r.Name).Append("</b>");
            if (r.Style >= 0) sb.Append(" · ").Append(StyleName(r.Style));
            sb.Append('\n');
            if (r.First >= 0) sb.Append(Lang.F("Уперше поруч {0}, востаннє {1}.", BondLog.When(r.First), BondLog.When(r.Last))).Append('\n');
            sb.Append('\n');
            L(sb, r.N[Fought], Lang.T("Бились поруч"));
            L(sb, r.N[Boss], Lang.T("Разом бачили, як падає бос"));
            L(sb, r.N[Guarded], Lang.T("Стояли поруч, коли ти ледь жи{в|ла}"));
            L(sb, r.N[Fire], Lang.T("Сиділи біля одного вогню"));
            L(sb, r.N[Travel], Lang.T("Мандрували разом"));
            L(sb, r.N[Ship], Lang.T("Пливли одним кораблем"));
            L(sb, r.N[Friendly], Lang.F("Привітно махали {0}", Plugin.RavenNameDat(myMunin)));
            L(sb, r.N[FedUs], Lang.F("Їхній ворон приносив {0} поїсти", Plugin.RavenNameDat(myMunin)));
            L(sb, r.N[Hit], Lang.T("Били тебе"));
            L(sb, r.N[Grave], Lang.T("Нишпорили в твоїй могилі"));
            L(sb, r.N[Shook], Lang.F("Струшували {0} з себе", Plugin.RavenNameGen(myMunin)));
            L(sb, r.N[Mocked], Lang.T("Їхній ворон глузував із тебе"));
            L(sb, r.N[Stole], Lang.T("Їхній ворон цупив у тебе їжу"));
            L(sb, r.N[Rude], Lang.F("Гарчали чи тицяли на {0}", Plugin.RavenNameGen(myMunin)));
            L(sb, r.N[PeckedUs], Lang.T("Їхній ворон дзьобав тебе"));
            L(sb, r.N[Pecked], Lang.F("{0} дзьобав їх", Plugin.RavenName(myMunin)));
            L(sb, r.N[Thought], Lang.F("Ти пита{{в|ла}} {0} думки про цю людину", Plugin.RavenNameGen(myMunin)));
            string why = Why(mine, r);
            if (why != null) sb.Append('\n').Append("<i>").Append(why).Append("</i>");
            return Grammar.Fix(sb.ToString().TrimEnd(), Player.m_localPlayer);
        }

        private static void L(StringBuilder sb, int n, string what) { if (n > 0) sb.Append(what).Append(": ").Append(Lang.N(n, "раз|рази|разів")).Append('\n'); }

        // the biggest reason, in the bond's direction
        private static string Why(ItemDrop.ItemData mine, Rec r)
        {
            if (!r.Shown) return null;
            bool good = r.Score >= 0f;
            string best = null; float bw = 4f;
            Norov.Character c = Norov.CharOf(mine);
            string style = StyleName(r.Style);
            if (c != null && style.Length > 0)
                Pick(ref best, ref bw, good, r.Start, r.Start >= 0f ? Lang.F("{0} до вподоби такі, як {1}: {2}.", c.Name, r.Name, style) : Lang.F("{0} не до вподоби такі, як {1}: {2}.", c.Name, r.Name, style));
            Pick(ref best, ref bw, good, r.N[Fought] * 2f + r.N[Boss] * 10f + r.N[Guarded] * 4f, Lang.T("Бились пліч-о-пліч. Таке зближує."));
            Pick(ref best, ref bw, good, r.N[Fire] * 2f + r.N[Travel] * 3f + r.N[Ship] * 3f, Lang.T("Багато часу провели разом."));
            Pick(ref best, ref bw, good, r.N[Friendly] * 2f + r.N[FedUs] * 4f, Lang.T("Ця людина добра до ворона."));
            Pick(ref best, ref bw, good, -(r.N[Hit] * 8f + r.N[Grave] * 25f), Lang.T("Підняти руку на свого — такого не забувають."));
            Pick(ref best, ref bw, good, -(r.N[Shook] * 4f + r.N[Rude] * 2f), Lang.T("Ганяє ворона й не поважає."));
            Pick(ref best, ref bw, good, -(r.N[Mocked] * 6f + r.N[Stole] * 8f + r.N[PeckedUs] * 5f), Lang.T("Їхній ворон — паскуда, і людина така ж."));
            return best;
        }

        private static void Pick(ref string best, ref float bw, bool good, float weight, string text)
        {
            float w = good ? weight : -weight;
            if (w > bw) { bw = w; best = text; }
        }

        // the console: raven people [player [h|m] <score>]
        internal static string Cmd(Player me, string[] a)
        {
            if (me == null) return "No player.";
            if (a.Length < 2)
            {
                StringBuilder sb = new StringBuilder("Your manner: " + StyleName(StyleOf(me)) + "\n");
                foreach (bool m in new[] { false, true })
                    foreach (Rec r in Of(RavenSeat.On(me, m)))
                        sb.Append(Plugin.RavenName(m)).Append(" → ").Append(r.Name).Append(": ").Append(Mathf.RoundToInt(r.Score)).Append(" (").Append(StateOf(r.Score)).Append(", start ").Append(Mathf.RoundToInt(r.Start)).Append(")\n");
                return sb.ToString().TrimEnd();
            }
            Player t = null;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o != me && o.GetPlayerName().Equals(a[1], StringComparison.OrdinalIgnoreCase)) t = o;
            if (t == null) return "No player «" + a[1] + "» near.";
            bool mun = a.Length > 2 && a[2].ToLowerInvariant().StartsWith("m");
            float target;
            if (a.Length > 3 && float.TryParse(a[3], NumberStyles.Float, CultureInfo.InvariantCulture, out target))
            {
                float cur = ScoreOf(me, mun, t);
                ItemDrop.ItemData it = RavenSeat.On(me, mun);
                Rec r = Find(it, t.GetPlayerID());
                Change(it, mun, t, 0f, Friendly); // (met: there is a record, it has shown itself)
                r = Find(it, t.GetPlayerID());
                if (r != null) { Change(it, mun, t, (target - r.Score) / Mathf.Max(0.1f, Mul(CharId(it), target - r.Score, -1)), -1); }
                Seen1.Clear(); // (the next look acts at once)
                return Plugin.RavenName(mun) + " → " + t.GetPlayerName() + ": " + Mathf.RoundToInt(ScoreOf(me, mun, t)) + " (was " + Mathf.RoundToInt(cur) + ")";
            }
            return Plugin.RavenName(mun) + " → " + t.GetPlayerName() + ": " + Mathf.RoundToInt(ScoreOf(me, mun, t)) + " (" + StateTo(me, mun, t) + ")";
        }

        internal static void Reset() { Cache.Clear(); Seen1.Clear(); _peck = null; _styleAt = 0f; }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Звички» (the user, 2026-10-27): what a raven picks up from how you play these days — and loses again. On top of its
    // character, knack and quirk (Norov) and its blessing and burden (Perks), which it is born with.
    //   - Each habit has a lean (0–100) kept in the raven. Each game day its lean goes Keep × what it was + Gain × that
    //     day's score of the habit (0–1: how much of it the day had, with the raven by you) − Push × the opposite habit's
    //     score. A strong day: 25 → 46 → 64: on the third, the habit comes (On); it stays LockDays at least, then goes once
    //     its lean is under Off. At most MaxOn habits; an opposite one never beside it.
    //   - The day's doings are counted only while the raven is with you (worn, awake, yours); a raven left at home keeps
    //     what it has. Days that passed while you were out of the game are not counted.
    //   - Each habit gives a plus and a minus (small multipliers read where the thing itself is reckoned) and its own
    //     doings and words (HabitPatterns). Coming and going: a line in the corner, the raven's word, the history kept.
    //   - The book (RavenBook «Звички»): the habits with their strength, the next one forming; on hover how it came, what
    //     it gives, how to lose it and how to keep it.
    internal static class Habits
    {
        internal sealed class Def
        {
            public string Id, Opp, NameSrc, FromSrc, PlusSrc, MinusSrc, LoseSrc, KeepSrc;
            public string Name { get { return Lang.T(NameSrc); } }
        }

        private static Def D(string id, string opp, string name, string from, string plus, string minus, string lose, string keep)
        {
            return new Def { Id = id, Opp = opp, NameSrc = name, FromSrc = from, PlusSrc = plus, MinusSrc = minus, LoseSrc = lose, KeepSrc = keep };
        }

        internal static readonly Def[] All =
        {
            D("mead", null, Lang.K("Медолюб"), Lang.K("ти часто ділишся з ним медовухою"), Lang.K("п'яний б'є на 15% сильніше"),
                Lang.K("день без меду — і зранку похмілля: удар і політ на 10% слабші"), Lang.K("кілька днів не пити медовуху при ньому"), Lang.K("пити медовуху при ньому хоча б раз на день-два")),
            D("home", "roam", Lang.K("Хатній"), Lang.K("ви багато часу вдома: під дахом чи біля твого ліжка"), Lang.K("удома сила відновлюється на 15% швидше"),
                Lang.K("перші 10 хвилин далеко від дому ниє: два випадкові дари недоступні"), Lang.K("частіше брати його в довгі мандри"), Lang.K("проводити більшу частину дня вдома")),
            D("roam", "home", Lang.K("Мандрій"), Lang.K("ви багато ходите далеко від дому"), Lang.K("розвідка на 15% ширша, а в дорозі він на 10% швидший"),
                Lang.K("удома нудьгує: удвічі частіше відлітає сам"), Lang.K("посидіти кілька днів удома"), Lang.K("щодня проходити чималу дорогу")),
            D("brawl", null, Lang.K("Вояка"), Lang.K("ви багато б'єтеся"), Lang.K("удар на 10% сильніший"),
                Lang.K("не вміє сидіти тихо: каркає, коли ти скрадаєшся"), Lang.K("кілька мирних днів без бійок"), Lang.K("битися щодня")),
            D("sweet", "thin", Lang.K("Гурман"), Lang.K("ти часто годуєш його улюбленою їжею"), Lang.K("голодніє на 15% повільніше"),
                Lang.K("простою їжею часом гордує"), Lang.K("годувати звичайною їжею, не улюбленою"), Lang.K("давати улюблене хоча б раз на день")),
            D("thin", "sweet", Lang.K("Худорба"), Lang.K("він часто ходить голодний"), Lang.K("сам знаходить їжу вдвічі частіше"),
                Lang.K("краде в тебе їжу вдвічі частіше"), Lang.K("годувати вчасно"), Lang.K("не поспішати з годуванням")),
            D("night", "early", Lang.K("Нічна пташка"), Lang.K("ви не спите ночами"), Lang.K("уночі на 10% швидший, а дари на 10% дешевші"),
                Lang.K("удень дари на 10% дорожчі"), Lang.K("спати ночами"), Lang.K("не спати вночі")),
            D("early", "night", Lang.K("Ранній птах"), Lang.K("ти спиш ночами"), Lang.K("удень сила відновлюється на 10% швидше"),
                Lang.K("уночі дари на 10% дорожчі"), Lang.K("кілька ночей не лягати спати"), Lang.K("спати щоночі")),
            D("scared", null, Lang.K("Наляканий"), Lang.K("ти часто гинеш, коли він з тобою"), Lang.K("каркає, щойно ворог іде на тебе"),
                Lang.K("у бою дари на 15% дорожчі"), Lang.K("кілька днів не гинути"), Lang.K("гинути частіше (навряд чи захочеш)")),
            D("pet", null, Lang.K("Мазунчик"), Lang.K("ви часто спокійно стоїте разом, і ти його гладиш"), Lang.K("довіра росте на 15% швидше"),
                Lang.K("день без пестощів — дметься: довіра росте на 15% повільніше"), Lang.K("менше стояти без діла, поки він на плечі"), Lang.K("щодня трохи постояти спокійно — тоді ти його гладиш")),
            D("loner", "social", Lang.K("Відлюдько"), Lang.K("коли в світі є інші гравці, ви тримаєтеся осторонь"), Lang.K("коли поруч нікого, удар на 10% сильніший"),
                Lang.K("з чужими воронами дружить удвічі повільніше"), Lang.K("частіше бути поруч з іншими гравцями"), Lang.K("триматися подалі від інших")),
            D("social", "loner", Lang.K("Компанійський"), Lang.K("ви часто поруч з іншими гравцями"), Lang.K("з чужими воронами дружить у півтора раза швидше"),
                Lang.K("пів години без людей поруч — довіра росте на 10% повільніше"), Lang.K("довше бути самому"), Lang.K("проводити час з іншими")),
            D("sailor", null, Lang.K("Моряк"), Lang.K("ви багато плаваєте"), Lang.K("частіше пірнає по рибу й лізе на щоглу"),
                Lang.K("перші три хвилини на суші летить на 10% повільніше"), Lang.K("кілька днів не виходити в море"), Lang.K("плавати щодня")),
            D("under", null, Lang.K("Підземник"), Lang.K("ви багато часу в підземеллях"), Lang.K("під землею дари на 15% дешевші"),
                Lang.K("надворі вдень мружиться: політ на 5% повільніший"), Lang.K("кілька днів не лізти під землю"), Lang.K("ходити в підземелля щодня")),
            D("hunter", null, Lang.K("Ловчий"), Lang.K("ти часто полюєш на звірину"), Lang.K("приносить здобич у півтора раза частіше"),
                Lang.K("коли ти скрадаєшся біля звірини, часом каркає"), Lang.K("кілька днів не полювати"), Lang.K("полювати щодня")),
            D("lazy", "worker", Lang.K("Ледащо"), Lang.K("ти майже не кличеш його дарів"), Lang.K("сила відновлюється на 15% швидше"),
                Lang.K("перший дар у кожному бою на 20% дорожчий"), Lang.K("частіше кликати його дари"), Lang.K("рідко кликати дари")),
            D("worker", "lazy", Lang.K("Трудяга"), Lang.K("ти часто кличеш його дари"), Lang.K("дари перезаряджаються на 10% швидше"),
                Lang.K("сила відновлюється на 10% повільніше"), Lang.K("дати йому перепочити"), Lang.K("кликати його дари щодня")),
        };

        internal static Def Find(string id) { foreach (Def d in All) if (d.Id == id) return d; return null; }

        // the pace (see above) and the lines
        internal static float Keep = 0.85f, Gain = 25f, Push = 20f, On = 60f, Off = 30f, Forming = 20f;
        internal static int LockDays = 5, MaxOn = 2, HistoryKept = 8;
        internal const string Key = "RavenWatch_habits";

        // ---------------------------------------------------------------- what a raven keeps
        internal sealed class State
        {
            public readonly Dictionary<string, float> Lean = new Dictionary<string, float>();
            public readonly List<KeyValuePair<string, int>> OnSince = new List<KeyValuePair<string, int>>(); // the habits it has, since which day
            public readonly List<string> History = new List<string>(); // "id@from-to"
            public int PetDay = -999, MeadDay = -999, HangSaid = -999, SulkSaid = -999; // (the days its hangover, its sulk were said)
            public bool Has(string id) { foreach (KeyValuePair<string, int> kv in OnSince) if (kv.Key == id) return true; return false; }
            public int Since(string id) { foreach (KeyValuePair<string, int> kv in OnSince) if (kv.Key == id) return kv.Value; return 0; }
            public float LeanOf(string id) { float v; return Lean.TryGetValue(id, out v) ? v : 0f; }
        }

        // read once per text (the item keeps it as a string: parsed again only when it changed)
        private static readonly Dictionary<ItemDrop.ItemData, KeyValuePair<string, State>> Cache = new Dictionary<ItemDrop.ItemData, KeyValuePair<string, State>>();
        internal static State Of(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            string raw;
            if (item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw)) raw = "";
            KeyValuePair<string, State> c;
            if (Cache.TryGetValue(item, out c) && c.Key == raw) return c.Value;
            State s = Parse(raw);
            if (Cache.Count > 32) Cache.Clear();
            Cache[item] = new KeyValuePair<string, State>(raw, s);
            return s;
        }

        private static State Parse(string raw)
        {
            State s = new State();
            foreach (string part in raw.Split('|'))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                string k = part.Substring(0, c), v = part.Substring(c + 1);
                foreach (string e in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (k == "lean") { int eq = e.IndexOf('='); float f; if (eq > 0 && float.TryParse(e.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.Lean[e.Substring(0, eq)] = f; }
                    else if (k == "on") { int at = e.IndexOf('@'); int d; if (at > 0 && int.TryParse(e.Substring(at + 1), out d) && Find(e.Substring(0, at)) != null) s.OnSince.Add(new KeyValuePair<string, int>(e.Substring(0, at), d)); }
                    else if (k == "hist") s.History.Add(e);
                }
                int n;
                if (k == "pet" && int.TryParse(v, out n)) s.PetDay = n;
                else if (k == "mead" && int.TryParse(v, out n)) s.MeadDay = n;
                else if (k == "sulk" && int.TryParse(v, out n)) s.SulkSaid = n;
                else if (k == "hang" && int.TryParse(v, out n)) s.HangSaid = n;
            }
            return s;
        }

        private static void Write(ItemDrop.ItemData item, State s)
        {
            if (item == null || item.m_customData == null) return;
            StringBuilder b = new StringBuilder("lean:");
            bool first = true;
            foreach (KeyValuePair<string, float> kv in s.Lean)
            {
                if (kv.Value < 0.5f) continue;
                if (!first) b.Append(','); first = false;
                b.Append(kv.Key).Append('=').Append(kv.Value.ToString("0.#", CultureInfo.InvariantCulture));
            }
            b.Append("|on:");
            for (int i = 0; i < s.OnSince.Count; i++) b.Append(i > 0 ? "," : "").Append(s.OnSince[i].Key).Append('@').Append(s.OnSince[i].Value);
            b.Append("|hist:").Append(string.Join(",", s.History.ToArray()));
            b.Append("|pet:").Append(s.PetDay).Append("|mead:").Append(s.MeadDay).Append("|sulk:").Append(s.SulkSaid).Append("|hang:").Append(s.HangSaid);
            item.m_customData[Key] = b.ToString();
        }

        internal static bool Has(ItemDrop.ItemData item, string id)
        {
            if (item == null || !Trust.Active(item)) return false;
            State s = Of(item);
            return s != null && s.Has(id);
        }

        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }
        private static bool Night { get { return EnvMan.instance != null && EnvMan.IsNight(); } }

        // ---------------------------------------------------------------- the day's doings, per raven with you
        private sealed class Tally
        {
            public float Worn, Home, Travel, Night, Sail, Under, Hungry, Solo, Company;
            public int Sips, Kills, Animals, Deaths, Pets, Favs, Gifts;
            public bool Slept;
        }
        private static readonly Dictionary<ItemDrop.ItemData, Tally> Day = new Dictionary<ItemDrop.ItemData, Tally>();
        private static Tally T(ItemDrop.ItemData item)
        {
            Tally t;
            if (!Day.TryGetValue(item, out t)) { t = new Tally(); Day[item] = t; }
            return t;
        }

        // a raven with you now: awake, yours, on you (not away)
        private static ItemDrop.ItemData With(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return item != null && Trust.Active(item) && !Trust.Away(item) && !Trust.Stranger(item, p) ? item : null;
        }

        private static readonly bool[] Both = { false, true };
        private static void Each(Action<ItemDrop.ItemData> a)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            foreach (bool m in Both) { ItemDrop.ItemData it = With(p, m); if (it != null) a(it); }
        }

        // the events (their hooks): counted for the ravens with you
        internal static void Sipped(ItemDrop.ItemData item) { if (item == null) return; T(item).Sips++; State s = Of(item); s.MeadDay = Today; Write(item, s); }
        internal static void Killed(bool animal) { Each(it => { if (animal) T(it).Animals++; else T(it).Kills++; }); }
        internal static void Fed(ItemDrop.ItemData item, bool fav) { if (item != null && fav) T(item).Favs++; }
        internal static void Gift(bool munin) { Player p = Player.m_localPlayer; ItemDrop.ItemData it = p != null ? With(p, munin) : null; if (it != null) T(it).Gifts++; }
        // you stroked it (Pet: the stroke when you stand calmly)
        internal static void Stroked(Player p, bool munin)
        {
            ItemDrop.ItemData it = With(p, munin);
            if (it == null) return;
            T(it).Pets++;
            State s = Of(it); s.PetDay = Today; Write(it, s);
        }

        internal static void Happened(Ev type)
        {
            switch (type)
            {
                case Ev.Death: Each(it => T(it).Deaths++); break;
                case Ev.Sleep: if (Night) Each(it => T(it).Slept = true); break;
            }
        }

        // ---------------------------------------------------------------- twice a second (RavenSkills)
        private static float _nextSample, _nextLook;
        private static int _day = -1;
        private static Vector3 _lastPos;
        private static bool _hadPos;
        private static float _aloneSince = -1f, _landedAt = -999f, _tripFrom = -999f, _homeSince = -1f;
        private static Vector3 _homePos;
        private static bool _wasOnShip, _fought;
        internal static float TripFar = 150f, HomeSettle = 120f; // m from home a trip begins; s at home before the next one counts (_tripFrom −1: ready)
        private const float SampleEvery = 10f;

        internal static void Tick(Player p)
        {
            if (p == null || EnvMan.instance == null) return;
            int today = Today;
            if (_day < 0 || today < _day) { _day = today; Day.Clear(); } // (first look, or another world: nothing to weigh)
            if (today != _day) { NewDay(p, _day, today); _day = today; }
            float now = Time.time;
            // the moments of these minutes: a trip from home begun, ashore again, alone among players
            Vector3 pos = p.transform.position;
            if (AtHome(p))
            {
                if (_homeSince < 0f) _homeSince = now;
                _homePos = pos;
                if (now - _homeSince >= HomeSettle) _tripFrom = -1f; // (home a while: the next trip counts)
            }
            else
            {
                _homeSince = -1f;
                if (_tripFrom == -1f && Vector3.Distance(pos, _homePos) > TripFar) _tripFrom = now;
            }
            if (Senses.Fight) _fought = true; else if (_fought) { _fought = false; FightOver(); } // (the fight's first gift: «Ледащо»)
            bool ship = Senses.OnShip;
            if (_wasOnShip && !ship) _landedAt = now;
            _wasOnShip = ship;
            bool others = Others(p, 40f);
            if (others) _aloneSince = -1f; else if (_aloneSince < 0f) _aloneSince = now;
            if (now >= _nextSample) { _nextSample = now + SampleEvery; Sample(p, others); }
            if (now >= _nextLook) { _nextLook = now + 1f; Doings(p); }
        }

        private static void Sample(Player p, bool others)
        {
            float min = SampleEvery / 60f;
            Vector3 pos = p.transform.position;
            float moved = _hadPos ? Vector3.Distance(pos, _lastPos) : 0f;
            _lastPos = pos; _hadPos = true;
            bool home = AtHome(p), crowd = ZNet.instance != null && ZNet.instance.GetNrOfPlayers() > 1, sleeping = p.IsSleeping() || p.InBed();
            foreach (bool m in Both)
            {
                ItemDrop.ItemData it = With(p, m);
                if (it == null) continue;
                Tally t = T(it);
                t.Worn += min;
                if (home) t.Home += min; else if (!Senses.OnShip && moved < 60f) t.Travel += moved;
                if (Night && !sleeping) t.Night += min;
                if (Senses.OnShip) t.Sail += min;
                if (Senses.Underground) t.Under += min;
                if (Satiety.Zone(it) <= Satiety.Hungry) t.Hungry += min;
                if (crowd) { if (others) t.Company += min; else t.Solo += min; }
            }
        }

        // home: under a roof, or by your bed (the spawn point you chose)
        internal static bool AtHome(Player p)
        {
            if (p == null) return false;
            if (Senses.Underground) return false; // (a dungeon is a roof, not a home)
            if (Senses.Indoors) return true;
            PlayerProfile pr = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            return pr != null && pr.HaveCustomSpawnPoint() && Vector3.Distance(pr.GetCustomSpawnPoint(), p.transform.position) < 60f;
        }

        private static bool Others(Player p, float r)
        {
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o != p && Vector3.Distance(o.transform.position, p.transform.position) < r) return true;
            return false;
        }

        // a day's scores (0–1), as long as the raven was with you a while; the game day may be set other than the usual 30 min
        private static float Score(string id, Tally t)
        {
            if (t == null || t.Worn < 5f) return 0f;
            float day = Mathf.Max(5f, Power.GameDaySeconds() / 60f) / 30f; // (× the minutes for a day of other length)
            switch (id)
            {
                case "mead": return C(t.Sips / 2f);
                case "home": return C(t.Home / (12f * day));
                case "roam": return C(t.Travel / 4000f);
                case "brawl": return C(t.Kills / 15f);
                case "sweet": return C(t.Favs / 2f);
                case "thin": return C(t.Hungry / (10f * day));
                case "night": return t.Slept ? 0f : C(t.Night / (8f * day));
                case "early": return t.Slept ? 0.6f : 0f; // (most sleep every night: slower, about six nights)
                case "scared": return C(t.Deaths / 2f);
                case "pet": return C(t.Pets / 4f);
                case "loner": return C(t.Solo / (20f * day));
                case "social": return C(t.Company / (15f * day));
                case "sailor": return C(t.Sail / (8f * day));
                case "under": return C(t.Under / (8f * day));
                case "hunter": return C(t.Animals / 6f);
                case "lazy": return t.Worn >= 15f * day && t.Gifts == 0 ? 1f : 0f;
                case "worker": return C(t.Gifts / 8f);
            }
            return 0f;
        }
        private static float C(float v) { return Mathf.Clamp01(v); }

        // a day over (or more of them, away): each raven that was with you weighs it up
        private static void NewDay(Player p, int was, int now)
        {
            List<ItemDrop.ItemData> ravens = new List<ItemDrop.ItemData>(Day.Keys);
            foreach (bool m in Both) { ItemDrop.ItemData it = With(p, m); if (it != null && !ravens.Contains(it)) ravens.Add(it); }
            List<ItemDrop.ItemData> shelved = RavenSeat.Shelved(p);
            foreach (ItemDrop.ItemData it in ravens)
            {
                bool munin;
                if (!RavenItem.IsRaven(it, out munin) || Trust.Stranger(it, p)) continue;
                if (!p.GetInventory().ContainsItem(it) && !shelved.Contains(it)) continue; // (given away, dropped: not yours to weigh)
                Tally t;
                Day.TryGetValue(it, out t);
                Weigh(p, it, munin, t, now);
            }
            Day.Clear();
        }

        private static void Weigh(Player p, ItemDrop.ItemData item, bool munin, Tally t, int today)
        {
            State s = Of(item);
            for (int i = 0; i < s.OnSince.Count; i++) if (s.OnSince[i].Value > today) s.OnSince[i] = new KeyValuePair<string, int>(s.OnSince[i].Key, today); // (a raven from another world)
            if (s.MeadDay > today) s.MeadDay = today;
            if (s.PetDay > today) s.PetDay = today;
            Dictionary<string, float> score = new Dictionary<string, float>();
            foreach (Def d in All) score[d.Id] = Score(d.Id, t);
            foreach (Def d in All)
            {
                float opp = d.Opp != null ? score[d.Opp] : 0f;
                s.Lean[d.Id] = Mathf.Clamp(s.LeanOf(d.Id) * Keep + Gain * score[d.Id] - Push * opp, 0f, 100f);
            }
            // going: under Off, past its lock
            for (int i = s.OnSince.Count - 1; i >= 0; i--)
            {
                KeyValuePair<string, int> h = s.OnSince[i];
                if (s.LeanOf(h.Key) >= Off || today - h.Value < LockDays) continue;
                s.OnSince.RemoveAt(i);
                Remember(s, h.Key, h.Value, today);
                Told(p, munin, item, h.Key, false);
            }
            // coming: the strongest over On first; never beside its opposite; more than MaxOn: the weakest past its lock gives way
            List<Def> rising = new List<Def>();
            foreach (Def d in All) if (!s.Has(d.Id) && s.LeanOf(d.Id) >= On) rising.Add(d);
            rising.Sort((a, b) => s.LeanOf(b.Id).CompareTo(s.LeanOf(a.Id)));
            foreach (Def d in rising)
            {
                if (d.Opp != null && s.Has(d.Opp)) continue;
                if (s.OnSince.Count >= MaxOn)
                {
                    int weakest = -1;
                    for (int i = 0; i < s.OnSince.Count; i++)
                        if (today - s.OnSince[i].Value >= LockDays && s.LeanOf(s.OnSince[i].Key) < s.LeanOf(d.Id) && (weakest < 0 || s.LeanOf(s.OnSince[i].Key) < s.LeanOf(s.OnSince[weakest].Key))) weakest = i;
                    if (weakest < 0) continue;
                    KeyValuePair<string, int> gone = s.OnSince[weakest];
                    s.OnSince.RemoveAt(weakest);
                    Remember(s, gone.Key, gone.Value, today);
                    Told(p, munin, item, gone.Key, false);
                }
                s.OnSince.Add(new KeyValuePair<string, int>(d.Id, today));
                Told(p, munin, item, d.Id, true);
            }
            Write(item, s);
            if (Plugin.Instance != null && t != null)
                Plugin.Instance.Log("Habits: " + Plugin.RavenName(munin) + " day " + today + " — " + Leans(s) + "; has " + Names(s));
        }

        private static void Remember(State s, string id, int from, int to)
        {
            s.History.Add(id + "@" + from + "-" + to);
            while (s.History.Count > HistoryKept) s.History.RemoveAt(0);
        }

        private static void Told(Player p, bool munin, ItemDrop.ItemData item, string id, bool came)
        {
            Def d = Find(id);
            if (d == null) return;
            p.Message(MessageHud.MessageType.TopLeft, came ? Lang.F("{0} набув звички: «{1}» (книга воронів)", Plugin.RavenName(munin), d.Name) : Lang.F("{0} відвик: «{1}»", Plugin.RavenName(munin), d.Name));
            if (came) PerkFx.Say(munin, "habit_gain_" + id, 1f, 0f); else PerkFx.Say(munin, "habit_lost", 1f, 0f, "habit", d.Name);
            RavenJournal.Note(p, item, (came ? "took up the habit " : "dropped the habit ") + id);
        }

        private static string Leans(State s)
        {
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, float> kv in s.Lean) if (kv.Value >= 1f) l.Add(kv.Key + " " + Mathf.RoundToInt(kv.Value));
            return l.Count > 0 ? string.Join(", ", l.ToArray()) : "nothing leaning";
        }
        private static string Names(State s)
        {
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, int> kv in s.OnSince) l.Add(kv.Key);
            return l.Count > 0 ? string.Join(", ", l.ToArray()) : "none";
        }

        // ---------------------------------------------------------------- their doings (once a second)
        private static readonly float[] NextCaw = new float[2], NextWarn = new float[2];

        private static void Doings(Player p)
        {
            if (p.IsDead()) return;
            float now = Time.time;
            foreach (bool m in Both)
            {
                ItemDrop.ItemData it = With(p, m);
                if (it == null) continue;
                State s = Of(it);
                int k = m ? 1 : 0;
                // «Вояка»: you sneak, he will not keep quiet; «Ловчий»: you stalk the game, he caws at it
                if (p.IsCrouching() && now >= NextCaw[k])
                {
                    if (s.Has("brawl") && UnityEngine.Random.value < 0.25f) { NextCaw[k] = now + UnityEngine.Random.Range(30f, 60f); ScreamFx.CawAll(p, m, m ? 0.9f : 1.25f, 0.8f); PerkFx.Say(m, "habit_brawl_sneak", 0.4f, 3f); }
                    else if (s.Has("hunter") && GameNear(p) && UnityEngine.Random.value < 0.2f) { NextCaw[k] = now + UnityEngine.Random.Range(30f, 60f); ScreamFx.CawAll(p, m, m ? 0.9f : 1.25f, 0.8f); PerkFx.Say(m, "habit_hunter_spook", 0.5f, 3f); }
                }
                // «Наляканий»: a foe coming for you — a caw at once
                if (s.Has("scared") && now >= NextWarn[k] && now - Senses.HitFoeAt > 6f && now - Senses.HurtByFoeAt > 6f)
                {
                    Character foe = Coming(p);
                    if (foe != null)
                    {
                        NextWarn[k] = now + 60f;
                        ScreamFx.CawAll(p, m, m ? 0.85f : 1.2f, 0.9f);
                        p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0}: ворог іде — {1}!", Plugin.RavenName(m), Lang.Game(foe.m_name)));
                        PerkFx.Say(m, "habit_scared_warn", 0.6f, 2f);
                    }
                }
                // «Медолюб»: no mead yesterday — the morning's hangover is said once
                if (s.Has("mead") && Hangover(it) && s.HangSaid != Today) { s.HangSaid = Today; Write(it, s); PerkFx.Say(m, "habit_mead_hangover", 1f, 0f); }
                // «Мазунчик»: not stroked today, by noon he sulks (said once a day)
                if (s.Has("pet") && Unpetted(it) && s.SulkSaid != Today) { s.SulkSaid = Today; Write(it, s); PerkFx.Say(m, "habit_pet_sulk", 1f, 0f); }
            }
        }

        private static bool GameNear(Player p)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && !c.IsTamed() && c.m_faction == Character.Faction.AnimalsVeg && Vector3.Distance(c.transform.position, p.transform.position) < 25f) return true;
            return false;
        }

        private static Character Coming(Player p)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.IsBoss() || !BaseAI.IsEnemy(p, c)) continue;
                if (Vector3.Distance(c.transform.position, p.transform.position) > 30f) continue;
                MonsterAI ai = c.GetBaseAI() as MonsterAI;
                if (ai != null && ai.GetTargetCreature() == p) return c;
            }
            return null;
        }

        // the morning after a day with no mead (dawn to noon)
        private static bool Hangover(ItemDrop.ItemData item)
        {
            State s = Of(item);
            float f = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0f;
            return Today - s.MeadDay >= 2 && f > 0.25f && f < 0.5f;
        }

        // not stroked since yesterday, past noon
        private static bool Unpetted(ItemDrop.ItemData item)
        {
            State s = Of(item);
            float f = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0f;
            return s.PetDay < Today && f > 0.5f;
        }

        // ---------------------------------------------------------------- what the habits do (read where the thing is reckoned)

        private static bool MuninOf(ItemDrop.ItemData item) { bool m; return RavenItem.IsRaven(item, out m) && m; }

        // the strike, % on top
        internal static float StrikePct(ItemDrop.ItemData item)
        {
            if (item == null) return 0f;
            State s = Of(item);
            if (s == null || s.OnSince.Count == 0 || !Trust.Active(item)) return 0f;
            float v = 0f;
            if (s.Has("mead") && Mead.Tipsy(MuninOf(item))) v += 15f;
            if (s.Has("mead") && Hangover(item)) v -= 10f;
            if (s.Has("brawl")) v += 10f;
            if (s.Has("loner") && Player.m_localPlayer != null && !Others(Player.m_localPlayer, 40f)) v += 10f;
            return v;
        }

        // the flight's pace, ×
        internal static float WingMul(ItemDrop.ItemData item)
        {
            if (item == null) return 1f;
            State s = Of(item);
            if (s == null || s.OnSince.Count == 0 || !Trust.Active(item)) return 1f;
            Player p = Player.m_localPlayer;
            float v = 1f;
            if (s.Has("mead") && Hangover(item)) v *= 0.9f;
            if (s.Has("roam") && p != null && !AtHome(p)) v *= 1.1f;
            if (s.Has("night") && Night) v *= 1.1f;
            if (s.Has("sailor") && Time.time - _landedAt < 180f) v *= 0.9f;
            if (s.Has("under") && !Night && !Senses.Underground && !Senses.Indoors) v *= 0.95f;
            return v;
        }

        // a gift's price, × (munin: whose gift)
        private static readonly bool[] FirstInFight = new bool[2];
        internal static float CostMul(bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            State s = item != null && Trust.Active(item) ? Of(item) : null;
            if (s == null || s.OnSince.Count == 0) return 1f;
            float v = 1f;
            if (s.Has("night")) v *= Night ? 0.9f : 1.1f;
            if (s.Has("early") && Night) v *= 1.1f;
            if (s.Has("scared") && Senses.Fight) v *= 1.15f;
            if (s.Has("under") && Senses.Underground) v *= 0.85f;
            if (s.Has("lazy") && Senses.Fight && !FirstInFight[munin ? 1 : 0]) v *= 1.2f;
            return v;
        }

        // a gift paid (Power.Spend): counted for «Трудяга»/«Ледащо», and the fight's first one gone
        internal static void Spent(bool munin)
        {
            Gift(munin);
            if (Senses.Fight) FirstInFight[munin ? 1 : 0] = true;
        }
        internal static void FightOver() { FirstInFight[0] = FirstInFight[1] = false; }

        // strength coming in, ×
        internal static float RegenMul(ItemDrop.ItemData item)
        {
            State s = item != null && Trust.Active(item) ? Of(item) : null;
            if (s == null || s.OnSince.Count == 0) return 1f;
            Player p = Player.m_localPlayer;
            float v = 1f;
            if (s.Has("home") && p != null && AtHome(p)) v *= 1.15f;
            if (s.Has("early") && !Night) v *= 1.1f;
            if (s.Has("lazy")) v *= 1.15f;
            if (s.Has("worker")) v *= 0.9f;
            return v;
        }

        // a gift's pause, × (by whose gift it is)
        internal static float CooldownMul(string giftId)
        {
            bool h = Has(Trust.Worn(false), "worker"), m = Has(Trust.Worn(true), "worker");
            if (!h && !m) return 1f;
            RavenAbility a = RavenAbilities.Find(giftId);
            return a != null && (a.Munin ? m : h) ? 0.9f : 1f;
        }

        // trust coming in, ×
        internal static float TrustMul(ItemDrop.ItemData item)
        {
            State s = item != null && Trust.Active(item) ? Of(item) : null;
            if (s == null || s.OnSince.Count == 0) return 1f;
            float v = 1f;
            if (s.Has("pet")) v *= Unpetted(item) ? 0.85f : 1.15f;
            if (s.Has("social") && _aloneSince > 0f && Time.time - _aloneSince > 1800f) v *= 0.9f;
            return v;
        }

        internal static float BondMul(ItemDrop.ItemData item)
        {
            if (Has(item, "loner")) return 0.5f;
            if (Has(item, "social")) return 1.5f;
            return 1f;
        }

        internal static float LootMul(ItemDrop.ItemData item)
        {
            float v = 1f;
            if (Has(item, "thin")) v *= 2f;
            if (Has(item, "hunter")) v *= 1.5f;
            return v;
        }

        internal static float HungerMul(ItemDrop.ItemData item) { return Has(item, "sweet") ? 0.85f : 1f; }
        internal static float StealWaitMul(ItemDrop.ItemData item) { return Has(item, "thin") ? 0.5f : 1f; }
        internal static float ScoutMul(ItemDrop.ItemData item) { return Has(item, "roam") ? 1.15f : 1f; }
        internal static float SeaMul(ItemDrop.ItemData item) { return Has(item, "sailor") ? 0.6f : 1f; } // (the pause before the next dive or mast)

        // how often it flies off by itself, ×
        internal static float OutMul(ItemDrop.ItemData item) { return Has(item, "roam") && AtHome(Player.m_localPlayer) ? 2f : 1f; }

        // «Гурман»: plain food now and then turned down (a word, and why)
        internal static string Refuses(Player p, bool munin, ItemDrop.ItemData item, Trust.Food food)
        {
            if (food == null || !Has(item, "sweet") || Satiety.IsFav(item, food.Prefab) || UnityEngine.Random.value >= 0.25f) return null;
            PerkFx.Say(munin, "habit_sweet_refuse", 1f, 0f);
            return Lang.F("{0} гордує: {1}. Гурман — хоче чогось смачнішого.", Plugin.RavenName(munin), food.Name);
        }

        // «Хатній»: in the first minutes away from home two of its gifts are not for you (picked anew each trip)
        private static readonly List<string>[] Locked = { new List<string>(), new List<string>() };
        private static readonly float[] LockedFor = { -1f, -1f };
        internal static string Blocks(Player p, RavenAbility a)
        {
            if (a == null || a.Pair || p == null) return null;
            ItemDrop.ItemData item = Trust.Worn(a.Munin);
            if (_tripFrom < 0f || Time.time - _tripFrom > 600f || !Has(item, "home") || AtHome(p)) return null;
            int k = a.Munin ? 1 : 0;
            if (LockedFor[k] != _tripFrom)
            {
                LockedFor[k] = _tripFrom;
                Locked[k].Clear();
                List<string> pool = new List<string>();
                foreach (RavenAbility g in RavenAbilities.All) if (g.Munin == a.Munin && !g.Pair && g.N > 0 && RavenAbilities.Unlocked(g)) pool.Add(g.Id);
                for (int i = 0; i < 2 && pool.Count > 0; i++) { int r = UnityEngine.Random.Range(0, pool.Count); Locked[k].Add(pool[r]); pool.RemoveAt(r); }
            }
            if (!Locked[k].Contains(a.Id)) return null;
            PerkFx.Say(a.Munin, "habit_home_whine", 0.7f, 1f);
            return Lang.F("{0} ниє, що хоче додому: «{1}» буде ще {2}.", Plugin.RavenName(a.Munin), a.Name, RavenAbilities.Span(600f - (Time.time - _tripFrom)));
        }

        // ---------------------------------------------------------------- the book: the line and the cards

        // its habits now, and the one forming (the strongest lean under On, over Forming), for the book's row
        internal static List<Def> Now(ItemDrop.ItemData item)
        {
            List<Def> l = new List<Def>();
            State s = Of(item);
            if (s == null) return l;
            foreach (KeyValuePair<string, int> kv in s.OnSince) { Def d = Find(kv.Key); if (d != null) l.Add(d); }
            return l;
        }

        internal static Def Rising(ItemDrop.ItemData item)
        {
            State s = Of(item);
            if (s == null) return null;
            Def best = null;
            foreach (Def d in All)
            {
                if (s.Has(d.Id) || (d.Opp != null && s.Has(d.Opp))) continue;
                float v = s.LeanOf(d.Id);
                if (v >= Forming && (best == null || v > s.LeanOf(best.Id))) best = d;
            }
            return best;
        }

        // ●●●: how strong it is (its lean over On), the rest faded (no other glyph has to be in the font)
        internal static string Dots(ItemDrop.ItemData item, string id)
        {
            float v = Of(item).LeanOf(id);
            return Pips(v >= 90f ? 3 : v >= 75f ? 2 : 1, 3);
        }

        // ●●●●: how far a forming one is to On
        internal static string Bar(ItemDrop.ItemData item, string id) { return Pips(Mathf.Clamp(Mathf.FloorToInt(Of(item).LeanOf(id) / On * 4f), 0, 4), 4); }

        private static string Pips(int n, int of) { return new string('●', n) + (of > n ? "<alpha=#40>" + new string('●', of - n) + "<alpha=#FF>" : ""); }

        // the whole of it on hover: whence, what it gives, how to lose it, how to keep it, how it stands, what it had before
        internal static string Tip(ItemDrop.ItemData item, Def d, bool has, string good, string bad)
        {
            State s = Of(item);
            int today = Today;
            float lean = s.LeanOf(d.Id);
            StringBuilder b = new StringBuilder();
            b.Append("<b>").Append(d.Name).Append("</b>").Append(has ? " " + Dots(item, d.Id) : Lang.T(" — назріває")).Append('\n');
            b.Append(Lang.F("Звідки: {0}.", Lang.T(d.FromSrc))).Append('\n');
            b.Append("<color=").Append(good).Append(">+ ").Append(Lang.T(d.PlusSrc)).Append("</color>\n");
            b.Append("<color=").Append(bad).Append(">− ").Append(Lang.T(d.MinusSrc)).Append("</color>\n\n");
            if (has)
            {
                int left = Mathf.Min(LockDays, LockDays - (today - s.Since(d.Id))); // (another world's day: no more than the lock)
                b.Append(left > 0 ? Lang.F("Тримається щонайменше ще {0}.", Lang.N(left, "день|дні|днів")) : Lang.F("Сила звички: {0} зі 100 (нижче {1} — зникне).", Mathf.RoundToInt(lean), Mathf.RoundToInt(Off))).Append('\n');
                b.Append(Lang.F("Щоб позбутися: {0}.", Lang.T(d.LoseSrc))).Append('\n');
                b.Append(Lang.F("Щоб зберегти: {0}.", Lang.T(d.KeepSrc)));
            }
            else
            {
                b.Append(Lang.F("Назріває: {0} зі 100 (від {1} — з'явиться).", Mathf.RoundToInt(lean), Mathf.RoundToInt(On))).Append('\n');
                b.Append(Lang.F("Щоб з'явилась: {0}.", Lang.T(d.KeepSrc))).Append('\n');
                b.Append(Lang.F("Щоб не з'явилась: {0}.", Lang.T(d.LoseSrc)));
            }
            string hist = History(s);
            if (hist.Length > 0) b.Append("\n\n").Append(hist);
            return b.ToString();
        }

        // no habits yet: what they are
        internal static string Explain(ItemDrop.ItemData item)
        {
            string hist = History(Of(item));
            return Lang.T("Звички ворон набуває від того, як ви граєте останніми днями: медовуха, мандри чи дім, бої, годування, пестощі, ніч чи день, море, підземелля… Кожна дає і плюс, і мінус. Звичка з'являється за кілька днів такої поведінки, тримається щонайменше п'ять днів і минає, якщо поведінка зміниться. Не більше двох разом.")
                + (hist.Length > 0 ? "\n\n" + hist : "");
        }

        private static string History(State s)
        {
            if (s == null || s.History.Count == 0) return "";
            List<string> l = new List<string>();
            for (int i = s.History.Count - 1; i >= 0; i--)
            {
                string e = s.History[i];
                int at = e.IndexOf('@'), dash = e.LastIndexOf('-');
                Def d = at > 0 ? Find(e.Substring(0, at)) : null;
                if (d == null || dash <= at) continue;
                l.Add(Lang.F("{0} (дні {1}–{2})", d.Name, e.Substring(at + 1, dash - at - 1), e.Substring(dash + 1)));
            }
            return l.Count > 0 ? Lang.F("Колись був: {0}.", string.Join(", ", l.ToArray())) : "";
        }

        // the console: raven habits [h|m] [lean <id> <0-100> | clear | day]
        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            int at = a.Length > 1 && (a[1] == "h" || a[1] == "m" || a[1] == "hugin" || a[1] == "munin") ? 2 : 1;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
            State s = Of(item);
            string what = a.Length > at ? a[at] : "";
            if (what == "lean" && a.Length > at + 2 && Find(a[at + 1]) != null)
            {
                float v;
                if (float.TryParse(a[at + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) s.Lean[a[at + 1]] = Mathf.Clamp(v, 0f, 100f);
                Write(item, s);
            }
            else if (what == "clear") { item.m_customData.Remove(Key); return Plugin.RavenName(munin) + ": звички стерто."; }
            else if (what == "day") { Tally t; Day.TryGetValue(item, out t); Day.Remove(item); Weigh(p, item, munin, t, Today); } // (that day's doings spent)
            s = Of(item);
            return Plugin.RavenName(munin) + ": " + Leans(s) + "; має: " + Names(s) + ". raven habits [h|m] [lean <id> <0-100> | day | clear]";
        }
    }
}
#endif

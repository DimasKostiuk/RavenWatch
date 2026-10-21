#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A raven's strength (0 to Max), kept in the item itself, spent by its abilities.
    // Hugin, the thought, gathers it in peace: new things learned, runestones, skills, rest by the fire, new lands, talks.
    // Munin, the memory, gathers it in blood: your kills, your narrow escapes, your deaths.
    // Only a worn raven charges. Not durability on purpose: a 0-durability item cannot even be worn.
    internal static class Power
    {
        private const string Key = "RavenWatch_power";
        // the most strength a raven holds grows with its trust level (Trust.Strength)
        internal static float MaxWorn(bool munin) { ItemDrop.ItemData item = WornItem(munin); return item != null ? Trust.Strength(item) : Trust.MinStrength; }
        private const float Start = 50f;

        internal static float MuninThoughtCost = 1f;
        internal static float ThoughtCost = 1f, ScoutCost = 40f, ScreamCost = 60f, FuryCost = 60f, LightCost = 20f, WingsCost = 30f, LeapCost = 50f, HealCost = 40f, ShadowCost = 60f, DomeCost = 60f, WardCost = 40f, FlightCost = 80f, BeastCost = 100f, DecoyCost = 35f;

        private static readonly string HowHuginSrc = Lang.K("щодня +10, нові предмети й рецепти, нові руни, моноліти Path of Valheiman і Вегвізири (+20, повторне читання +5, до 5 каменів на день), рівні навичок, сон у ліжку, відпочинок біля вогню (у затишку швидше), ремесло й готування, врожай, ягоди й мед, робота (рубка дерев, видобуток, риболовля; уночі в 1,5 раза більше), плавання на кораблі, піші подорожі (до 30 на день), нові біоми й підземелля, розмови з торговцями");
        internal static string HowHugin { get { string s = Lang.T(HowHuginSrc); return _pov ? Lang.F("новий рівень персонажа (+{0}), {1}", Mathf.RoundToInt(PovLevelGain), s) : s; } }

        // with Path of Valheiman installed, its character levels charge Hugin too: say so in the tooltip
        private static bool _povNoticed, _pov;
        internal static void NoticePov()
        {
            if (_povNoticed) return;
            _povNoticed = true;
            if (AccessTools.TypeByName("PassiveTree.Progress") != null)
                _pov = true; // (said in the tooltip: HowHugin)
        }
        private static readonly string HowMuninSrc = Lang.K("щодня +10, твої вбивства (звичайний ворог +1, сильний — троль, елітний драугр, ґ'ялль тощо — від 2 до 6, боси від 30 до 100, своя тварина +1; зірки додають), удари по ворогах і рани від них, парирування (+2), знищені гнізда (+5), відбитий рейд (+20), медовуха (+2), трофеї (+1), нові руни й Вегвізири (+10), коли ледве виживаєш (+10), і твоя смерть (+40); уночі все в 1,5 раза більше");
        internal static string HowMunin { get { return Lang.T(HowMuninSrc); } }

        // ---------------------------------------------------------------- the number

        internal static float Get(ItemDrop.ItemData item)
        {
            string raw;
            float v;
            if (item != null && item.m_customData != null && item.m_customData.TryGetValue(Key, out raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return Mathf.Clamp(v, 0f, Trust.Strength(item));
            return Start;
        }

        private static void Set(ItemDrop.ItemData item, float v)
        {
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[Key] = Mathf.Clamp(v, 0f, Trust.Strength(item)).ToString("0.##", CultureInfo.InvariantCulture);
        }

        internal static void SetValue(ItemDrop.ItemData item, float v) { Set(item, v); }

        internal static int Current(bool munin)
        {
            ItemDrop.ItemData item = WornItem(munin);
            return item != null ? Mathf.FloorToInt(Get(item)) : 0;
        }

        private static ItemDrop.ItemData WornItem(bool munin)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(Player.m_localPlayer, out h, out m);
            return munin ? m : h;
        }

        // a gain for the worn raven; the bigger ones are told in the corner
        private static void Gain(bool munin, float amount, string why) { Gain(munin, amount, why, true); }

        // for the chores at home (HuginChores): the same gain, with or without trust
        internal static void Earn(bool munin, float amount, string why, bool deed) { Gain(munin, amount, why, deed); }

        // back from an outing that ran its time: rested wings (strength only, no trust, the same by day and night)
        internal static void Rested(bool munin, float amount) { Gain(munin, amount, Lang.T("відпочив"), false, false); }

        internal static float NightMul = 1.5f; // Munin's blood runs hotter at night; Hugin works by moonlight better

        private static void Gain(bool munin, float amount, string why, bool deed) { Gain(munin, amount, why, deed, munin); }

        private static void Gain(bool munin, float amount, string why, bool deed, bool night)
        {
            if (night && EnvMan.instance != null && EnvMan.IsNight()) amount *= NightMul;
            ItemDrop.ItemData item = WornItem(munin);
            if (deed) Trust.Deed(munin, amount, why); // what you do together builds trust too (a raven away gets none: Trust.DeedFor)
            if (item != null && Trust.Away(item)) return; // flown off: it did not see it, no strength
            if (item == null || amount <= 0f || !Trust.Active(item)) return; // an asleep raven does not charge
            amount *= Satiety.GainMul(munin); // hungry: none comes; fed: faster
            if (amount <= 0f) return;
            float max = Trust.Strength(item);
            float before = Get(item);
            if (before >= max) return;
            Set(item, before + amount);
            GainNote.Strength(munin, Mathf.Min(amount, max - before), why); // a short line in the corner
        }

        // what a gift really costs now: a hungry raven's gifts cost more (rounded as shown)
        internal static float Price(bool munin, float cost) { float mul = Satiety.CostMul(munin); return mul > 1f ? Mathf.Round(cost * mul) : cost; }

        // part of a gift's price given back (a strike that found nobody): strength only, no trust, no note; returns how much came back
        internal static float Refund(ItemDrop.ItemData item, float amount)
        {
            if (item == null || amount <= 0f) return 0f;
            float before = Get(item);
            Set(item, before + amount);
            return Get(item) - before;
        }

        internal static bool Spend(Player p, bool munin, float cost)
        {
            ItemDrop.ItemData item = WornItem(munin);
            if (item == null) return false;
            float have = Get(item);
            float mul = Satiety.CostMul(munin), real = Price(munin, cost);
            if (have < real)
            {
                RavenMsg.Center(p, Lang.F("{0}/{1} сили.{2}", ((munin ? Lang.T("Мунін видихся: ") : Lang.T("Хугін знесилений: ")) + Mathf.FloorToInt(have)), Mathf.RoundToInt(real), (mul > 1f ? Lang.F("\nГолодний ворон — дорогі дари (×{0}). Нагодуй його.", Satiety.Num(mul))
                : munin ? Lang.T("\nЙди когось убий — тоді поговоримо.") : Lang.T("\nДай йому перепочити, сили прийдуть.")))); // how it charges: in the book
                return false;
            }
            Set(item, have - real);
            Satiety.UsedGift(munin, cost); // every gift makes it hungrier
            return true;
        }

        internal static string Describe(ItemDrop.ItemData item, bool munin, bool owned)
        {
            string costs = munin
                ? Lang.F("Думка −{0}, Крик −{1}, Лють −{2}, Крила −{3}, Зцілення −{4}, Заслона −{5}, Звір −{6}, Кігті −{7}", Mathf.RoundToInt(MuninThoughtCost), Mathf.RoundToInt(ScreamCost), Mathf.RoundToInt(FuryCost), Mathf.RoundToInt(WingsCost), Mathf.RoundToInt(HealCost), Mathf.RoundToInt(WardCost), Mathf.RoundToInt(BeastCost), Mathf.RoundToInt(Strike.CostOf(true, Trust.Worn(true))))
                : Lang.F("Думка −{0}, Розвідка −{1}, Світло −{2}, Злет −{3}, Тінь −{4}, Під крилом −{5}, Крила Одіна −{6}, Удар в очі −{7}, Обманка −{8}", Mathf.RoundToInt(ThoughtCost), Mathf.RoundToInt(ScoutCost), Mathf.RoundToInt(LightCost), Mathf.RoundToInt(LeapCost), Mathf.RoundToInt(ShadowCost), Mathf.RoundToInt(DomeCost), Mathf.RoundToInt(FlightCost), Mathf.RoundToInt(Strike.CostOf(false, Trust.Worn(false))), Mathf.RoundToInt(DecoyCost));
            string head = owned ? Lang.F("Сила: <color=orange>{0}/{1}</color> ({2}){3}", Mathf.FloorToInt(Get(item)), Mathf.RoundToInt(Trust.Strength(item)), costs, Satiety.CostNote(munin)) : Lang.F("Сила витрачається на дари ({0})", costs);
            return Lang.F("{0}\nЗаряджається{1}: {2}.", head, (munin ? Lang.T(" в бою") : Lang.T(" у мирі")), (munin ? HowMunin : HowHugin));
        }

        // ---------------------------------------------------------------- Munin: blood

        // What a kill is worth to Munin: an ordinary creature of any biome 1 (+1 a star); the strong ones (a troll, an
        // elite draugr, a gjall...) their own number from the list, ×1.5 a star; the bosses grow in their order; a
        // boss of another mod 30; your own tamed animal only 1. Lists by prefab name, set in the config.
        internal static string KillSpecial = "Greydwarf_Elite:3,Greydwarf_Shaman:2,Troll:4,Bjorn:3,Skeleton_Poison:2,Draugr_Elite:3,Wraith:3,BlobElite:2,Abomination:4,"
            + "Fenring_Cultist:3,StoneGolem:4,Unbjorn:4,GoblinBrute:3,GoblinShaman:3,Lox:3,Serpent:4,BonemawSerpent:5,SeekerBrute:4,Gjall:4,DvergerMage:3,DvergerMageFire:3,"
            + "Charred_Mage:3,Morgen:5,FallenValkyrie:5,Charred_Melee_Dyrnwyn:6,DvergerMageIce:3,Greydwarf_Shaman_Frozen:2,JotunWitch:3,TrollFrost:6,Barka:6";
        internal static string KillBosses = "Eikthyr:30,gd_king:40,Bonemass:50,Dragon:60,GoblinKing:70,SeekerQueen:80,Fader:90,FrozenKing:100,"
            + "Skeleton_Hildir:15,Fenring_Cultist_Hildir:15,GoblinBrute_Hildir:15,GoblinShaman_Hildir:15";
        internal static float OtherBoss = 30f;
        private static Dictionary<string, float> _special, _bosses;
        private static string _specialFrom, _bossesFrom;

        private static Dictionary<string, float> KillList(string from, ref Dictionary<string, float> cache, ref string cachedFrom)
        {
            if (cache != null && cachedFrom == from) return cache;
            Dictionary<string, float> d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in (from ?? "").Split(','))
            {
                string[] kv = part.Split(':');
                float v;
                if (kv.Length == 2 && float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) d[kv[0].Trim()] = Mathf.Max(0f, v);
            }
            cache = d; cachedFrom = from;
            return d;
        }

        // a boss in phases (FrozenKing, FrozenKing_p2, FrozenKing_p3) is one boss: its phases go by the first name,
        // and once paid it is not paid again for a few minutes
        private const float BossAgainSeconds = 600f;
        private static string _bossPaid;
        private static float _bossPaidAt = -9999f;

        private static string BossBase(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return prefab;
            int i = prefab.LastIndexOf("_p", StringComparison.Ordinal);
            int n;
            return i > 0 && int.TryParse(prefab.Substring(i + 2), out n) ? prefab.Substring(0, i) : prefab;
        }

        // the strength a kill gives Munin, and what the corner line calls it
        internal static float KillWorth(int boss, int stars, string prefab, bool tamed, string token, out string why)
        {
            stars = Mathf.Clamp(stars, 0, 5);
            float v = 0f;
            string name = string.IsNullOrEmpty(token) ? null : Localization.instance != null ? Lang.Game("$" + token) : token;
            if (tamed) { why = Lang.T("своя тварина"); return 1f; }
            string bossBase = BossBase(prefab);
            bool listed = !string.IsNullOrEmpty(bossBase) && KillList(KillBosses, ref _bosses, ref _bossesFrom).TryGetValue(bossBase, out v);
            if (!listed) v = OtherBoss;
            if (listed || boss > 0)
            {
                why = name ?? Lang.T("бос");
                string key = string.IsNullOrEmpty(bossBase) ? token : bossBase;
                if (key == _bossPaid && Time.time - _bossPaidAt < BossAgainSeconds && Time.time >= _bossPaidAt) return 0f; // another phase of the same fight
                _bossPaid = key; _bossPaidAt = Time.time;
                return v;
            }
            if (!string.IsNullOrEmpty(prefab) && KillList(KillSpecial, ref _special, ref _specialFrom).TryGetValue(prefab, out v)) { why = name ?? Lang.T("сильний ворог"); return Mathf.Round(v * (1f + 0.5f * stars)); }
            why = Lang.T("вбивство");
            return 1f + stars;
        }

        internal static void OnKill(int boss, int stars, string prefab, bool tamed, string token)
        {
            string why;
            float worth = KillWorth(boss, stars, prefab, tamed, token, out why);
            if (worth > 0f) Gain(true, worth, why);
            Satiety.OnKill(); // a hungry Munin pecks at it
            Beast.OnKill(boss > 0); // the beast's Munin howls over it
        }

        internal static void OnNearDeath() { Gain(true, 10f, Lang.T("ледве {вижив|вижила}")); }
        internal static void OnNest() { Gain(true, 5f, Lang.T("гніздо")); }
        internal static void OnRaid() { Gain(true, 20f, Lang.T("рейд")); }
        internal static float DeathGainMinutes = 10f;
        private const string DeathGainKey = "RavenWatch_deathPower";

        // Munin's strength from your death: once in a while, so dying over and over by the bed charges nothing
        internal static void OnDeath()
        {
            Trust.OnDeath();
            Player p = Player.m_localPlayer;
            if (p != null && DeathGainMinutes > 0f)
            {
                if (WorldTime.Left(p.m_customData, DeathGainKey) > 0f) return;
                WorldTime.Set(p.m_customData, DeathGainKey, DeathGainMinutes * 60f);
            }
            Gain(true, 40f, Lang.T("твоя смерть"), false);
        }

        // ---------------------------------------------------------------- Hugin: thought

        private static float _knowledgeWindow;
        private static float _knowledgeGiven;
        private const float KnowledgePerMinute = 20f;

        // new things learned come in bursts (a new station unlocks many recipes): at most 20 a minute
        internal static void OnLearned(string what)
        {
            if (Time.time - _knowledgeWindow > 60f) { _knowledgeWindow = Time.time; _knowledgeGiven = 0f; }
            float give = Mathf.Min(5f, KnowledgePerMinute - _knowledgeGiven);
            if (give <= 0f) return;
            _knowledgeGiven += give;
            Gain(false, give, what);
        }

        internal static void OnSkillLevel() { Gain(false, 3f, Lang.T("навичка")); }

        internal static float PovLevelGain = 15f;
        private const string PovMaxKey = "RavenWatch_povmax";

        // Path of Valheiman, when installed: a new character level. Only above the highest one ever reached,
        // so a level lost on death and won back does not count twice. The first look just remembers it.
        internal static void OnPovLevel(Player p, int level)
        {
            string raw;
            int max;
            if (!p.m_customData.TryGetValue(PovMaxKey, out raw) || !int.TryParse(raw, out max))
            {
                p.m_customData[PovMaxKey] = level.ToString();
                return;
            }
            if (level <= max) return;
            p.m_customData[PovMaxKey] = level.ToString();
            if (PovLevelGain > 0f) Gain(false, PovLevelGain * (level - max), Lang.T("новий рівень"));
        }

        // ---------------------------------------------------------------- runestones and the Vegvisir
        // Nothing new is written per stone: the game itself keeps every text the character ever read (known texts),
        // and the boss kinds a Vegvisir showed are a short list. New lore: Hugin +NewStoneHugin, Munin +NewStoneMunin,
        // and trust. Reading again what you already know: Hugin +RereadHugin for each of the first RereadPerDay
        // different stones of a game day (one timer and today's few spots in the character: logging out gains nothing).
        // Path of Valheiman's rune monoliths count the same (PovMonoliths): drained = new runes, a drained one = read again.
        internal static float NewStoneHugin = 20f, NewStoneMunin = 10f, RereadHugin = 5f;
        private const string RereadKey = "RavenWatch_reread", VegvisirKey = "RavenWatch_vegvisir";

        internal static float GameDaySeconds() { return EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0 ? EnvMan.instance.m_dayLengthSec : 1800f; }

        internal static int RereadPerDay = 5;
        private const string RereadSpotsKey = "RavenWatch_rereadSpots";

        internal static void OnLore(Player p, bool fresh, string what, Vector3 at)
        {
            if (p == null) return;
            if (fresh)
            {
                Nicks.OnLore(p); // a deed for Hugin's nickname
                Gain(false, NewStoneHugin, what);
                Gain(true, NewStoneMunin, what, true, false); // memory, not blood: no night bonus
                return;
            }
            // the first RereadPerDay different stones in a game day; the day starts with the first of them.
            // Only today's few spots are kept, so the same stone read twice counts once.
            if (RereadHugin <= 0f || RereadPerDay <= 0) return;
            string spots;
            if (WorldTime.Left(p.m_customData, RereadKey) <= 0f)
            {
                WorldTime.Set(p.m_customData, RereadKey, GameDaySeconds());
                spots = "|";
            }
            else if (!p.m_customData.TryGetValue(RereadSpotsKey, out spots) || string.IsNullOrEmpty(spots)) spots = "|";
            string token = Spot(at) + "|";
            if (spots.Contains("|" + token)) return;
            if (spots.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Length >= RereadPerDay) return;
            p.m_customData[RereadSpotsKey] = spots + token;
            Gain(false, RereadHugin, what, false);
        }

        // the boss kinds (location names) a Vegvisir already showed this character: a handful at most
        internal static bool NewVegvisir(Player p, Vegvisir v)
        {
            string raw;
            p.m_customData.TryGetValue(VegvisirKey, out raw);
            raw = raw ?? "";
            bool fresh = false;
            foreach (Vegvisir.VegvisrLocation l in v.m_locations)
            {
                if (l == null || string.IsNullOrEmpty(l.m_locationName)) continue;
                string token = "|" + l.m_locationName + "|";
                if (raw.Contains(token)) continue;
                raw = (raw.Length == 0 ? "|" : raw) + l.m_locationName + "|";
                fresh = true;
            }
            if (fresh) p.m_customData[VegvisirKey] = raw;
            return fresh;
        }

        // the traders: a talk once a game day each (a few names, kept in the character)
        internal static void OnTrader(Player p, string name)
        {
            if (p == null) return;
            string key = "RavenWatch_trader_" + name;
            if (WorldTime.Left(p.m_customData, key) > 0f) return;
            WorldTime.Set(p.m_customData, key, GameDaySeconds());
            Gain(false, 10f, Lang.T("торговець"));
        }

        internal static string Spot(Vector3 p) { return Mathf.RoundToInt(p.x / 4f) + "," + Mathf.RoundToInt(p.z / 4f); }

        // the map: every so many new pixels of fog cleared
        internal static bool Scouting; // Hugin's own scouting does not count
        private static int _explored;
        internal static void OnExplored()
        {
            if (Scouting) return;
            if (++_explored < 400) return;
            _explored = 0;
            Gain(false, 1f, Lang.T("мапа"));
        }

        private const string PlacesKey = "RavenWatch_places";

        // a first visit: a biome or a dungeon this character never saw (kept in the character)
        internal static void OnPlace(Player p, string place, string why)
        {
            string raw;
            p.m_customData.TryGetValue(PlacesKey, out raw);
            raw = raw ?? "";
            string token = "|" + place + "|";
            if (raw.Contains(token)) return;
            if (raw.Length > 6000) raw = Trim(raw); // old dungeons fall out, nothing breaks
            p.m_customData[PlacesKey] = raw + token;
            Gain(false, 15f, why);
        }

        // raven forget: the land's first visit can pay again
        internal static bool ForgetPlace(Player p, string biome)
        {
            string raw;
            string lands;
            if (p.m_customData.TryGetValue("RavenWatch_lands", out lands) && lands != null) p.m_customData["RavenWatch_lands"] = lands.Replace("|" + biome + "|", "|"); // the land lines too
            if (!p.m_customData.TryGetValue(PlacesKey, out raw) || string.IsNullOrEmpty(raw)) return false;
            string token = "|biome:" + biome + "|";
            if (!raw.Contains(token)) return false;
            p.m_customData[PlacesKey] = raw.Replace(token, "|");
            if (_lastBiome.ToString() == biome) _lastBiome = Heightmap.Biome.None;
            return true;
        }

        // the older half of the dungeons goes; biomes always stay, or they would pay again
        private static string Trim(string raw)
        {
            List<string> biomes = new List<string>(), dungeons = new List<string>();
            foreach (string part in raw.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                (part.StartsWith("biome:") ? biomes : dungeons).Add("|" + part + "|");
            dungeons.RemoveRange(0, dungeons.Count / 2);
            return string.Join("", biomes.ToArray()) + string.Join("", dungeons.ToArray());
        }

        private static float _nextRest;
        internal static float RestSeconds = 20f; // a point of Hugin's strength this often by the fire
        private static Heightmap.Biome _lastBiome;
        private static readonly int CampFire = "CampFire".GetStableHashCode();
        private static readonly int Resting = "Resting".GetStableHashCode();

        // a new game day: each worn raven gathers a little by itself (kept in the raven, so once a day each)
        internal static float DailyGain = 10f;
        private const string DailyKey = "RavenWatch_dailyDay";

        private static void Daily(bool munin)
        {
            ItemDrop.ItemData item = WornItem(munin);
            if (item == null || !Trust.Active(item) || Trust.Away(item) || DailyGain <= 0f) return;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            string raw; int last;
            if (item.m_customData.TryGetValue(DailyKey, out raw) && int.TryParse(raw, out last) && last == day) return;
            item.m_customData[DailyKey] = day.ToString();
            Gain(munin, DailyGain, Lang.T("новий день"), false, false); // the same by day and by night
        }

        // twice a second from the skills tick
        internal static void Tick(Player p)
        {
            GainNote.Tick(p);
            Daily(false);
            Daily(true);
            if (WornItem(false) == null) return;
            Heightmap.Biome biome = p.GetCurrentBiome();
            if (biome != _lastBiome && biome != Heightmap.Biome.None)
            {
                _lastBiome = biome;
                OnPlace(p, "biome:" + biome, Lang.T("новий біом"));
            }
            HuginChores.Tick(p); // a night slept through
            if (Time.time < _nextRest) return;
            _nextRest = Time.time + HuginChores.RestSeconds(p, RestSeconds); // faster in a cosy place
            SEMan se = p.GetSEMan();
            // strength only: sitting idle by the fire is no deed, and would fill the day's trust without playing
            if (se.HaveStatusEffect(CampFire) && (p.IsSitting() || se.HaveStatusEffect(Resting))) Gain(false, 1f, Lang.T("вогонь"), false);
        }

        internal static void Reset()
        {
            HuginChores.Reset();
            GainNote.Reset();
            _explored = 0;
            _lastBiome = Heightmap.Biome.None;
            Scouting = false;
        }
    }

    // ---------------------------------------------------------------- where Hugin learns

    [HarmonyPatch(typeof(Player), "AddKnownItem")]
    internal static class PowerKnownItemPatch
    {
        private static readonly FieldInfo Known = AccessTools.Field(typeof(Player), "m_knownMaterial");
        private static void Prefix(Player __instance, ItemDrop.ItemData item, out bool __state)
        {
            HashSet<string> known = Known != null ? Known.GetValue(__instance) as HashSet<string> : null;
            bool raven;
            __state = __instance == Player.m_localPlayer && known != null && item != null && !known.Contains(item.m_shared.m_name)
                && !RavenItem.IsRaven(item, out raven) && !RavenItem.IsReroll(item);
        }
        private static void Postfix(bool __state) { if (__state) Power.OnLearned(Lang.T("нова знахідка")); }
    }

    [HarmonyPatch(typeof(Player), "AddKnownRecipe")]
    internal static class PowerKnownRecipePatch
    {
        private static readonly FieldInfo Known = AccessTools.Field(typeof(Player), "m_knownRecipes");
        private static void Prefix(Player __instance, Recipe recipe, out bool __state)
        {
            HashSet<string> known = Known != null ? Known.GetValue(__instance) as HashSet<string> : null;
            __state = __instance == Player.m_localPlayer && known != null && recipe != null && recipe.m_item != null
                && !known.Contains(recipe.m_item.m_itemData.m_shared.m_name);
        }
        private static void Postfix(bool __state) { if (__state) Power.OnLearned(Lang.T("новий рецепт")); }
    }

    [HarmonyPatch(typeof(Player), "OnSkillLevelup")]
    internal static class PowerSkillPatch
    {
        private static void Postfix(Player __instance) { if (__instance == Player.m_localPlayer) { Power.OnSkillLevel(); Nicks.OnSkillUp(__instance); } }
    }

    // the game always returns false from reading a stone; whether it was new is told by the known texts growing
    [HarmonyPatch(typeof(RuneStone), "Interact")]
    internal static class PowerRuneStonePatch
    {
        private static void Prefix(Humanoid character, bool hold, out int __state)
        {
            Player p = character as Player;
            __state = !hold && p != null && p == Player.m_localPlayer ? p.GetKnownTexts().Count : -1;
        }

        private static void Postfix(RuneStone __instance, Humanoid character, int __state)
        {
            if (__state < 0) return;
            Player p = (Player)character;
            Power.OnLore(p, p.GetKnownTexts().Count > __state, Lang.T("руни"), __instance.transform.position);
        }
    }

    // Path of Valheiman's rune monoliths (patched when the game starts, whatever order the mods loaded in; without
    // Path of Valheiman nothing happens). Draining one gives the character the key "pt.mono.<x>_<z>", once for good;
    // a drained one still takes E (it only says its power is gone): that is reading it again.
    internal static class PovMonoliths
    {
        private static bool _tried;

        internal static void Patch()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type monoliths = AccessTools.TypeByName("PassiveTree.Monoliths"), stone = AccessTools.TypeByName("PassiveTree.RuneMonolith");
                MethodInfo drain = monoliths != null ? AccessTools.Method(monoliths, "Drain", new Type[] { typeof(Player), typeof(Vector3) }) : null;
                MethodInfo interact = stone != null ? AccessTools.Method(stone, "Interact") : null;
                if (drain == null || interact == null) return;
                Harmony h = new Harmony(Plugin.Guid + ".monoliths");
                h.Patch(drain, prefix: new HarmonyMethod(typeof(PovMonoliths), "DrainPrefix"), postfix: new HarmonyMethod(typeof(PovMonoliths), "DrainPostfix"));
                h.Patch(interact, prefix: new HarmonyMethod(typeof(PovMonoliths), "InteractPrefix"));
                if (Plugin.Instance != null) Plugin.Instance.Log("Path of Valheiman: rune monoliths charge the ravens");
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Path of Valheiman monoliths failed: " + e.Message); }
        }

        // the same key Path of Valheiman gives for the stone at that place
        private static string Key(Vector3 at) { return "pt.mono." + Mathf.RoundToInt(at.x) + "_" + Mathf.RoundToInt(at.z); }

        private static void DrainPrefix(Player p, Vector3 at, out bool __state) { __state = p != null && p == Player.m_localPlayer && !p.HaveUniqueKey(Key(at)); }

        private static void DrainPostfix(Player p, Vector3 at, bool __state)
        {
            if (__state && p.HaveUniqueKey(Key(at))) Power.OnLore(p, true, Lang.T("моноліт"), at);
        }

        private static void InteractPrefix(MonoBehaviour __instance, Humanoid user, bool hold)
        {
            Player p = Player.m_localPlayer;
            if (hold || p == null || user != p) return;
            Vector3 at = __instance.transform.position;
            if (p.HaveUniqueKey(Key(at))) Power.OnLore(p, false, Lang.T("моноліт"), at);
        }
    }

    [HarmonyPatch(typeof(Vegvisir), "Interact")]
    internal static class PowerVegvisirPatch
    {
        private static void Postfix(Vegvisir __instance, Humanoid character, bool hold, bool __result)
        {
            if (!hold && __result && character == Player.m_localPlayer) Power.OnLore(Player.m_localPlayer, Power.NewVegvisir(Player.m_localPlayer, __instance), Lang.T("Вегвізир"), __instance.transform.position);
        }
    }

    [HarmonyPatch(typeof(Trader), "Interact")]
    internal static class PowerTraderPatch
    {
        private static void Postfix(Trader __instance, Humanoid character, bool hold)
        {
            if (!hold && character == Player.m_localPlayer) Power.OnTrader(Player.m_localPlayer, __instance.m_name);
        }
    }

    [HarmonyPatch(typeof(Minimap), "Explore", new Type[] { typeof(int), typeof(int) })]
    internal static class PowerExplorePatch
    {
        private static void Postfix(bool __result) { if (__result) Power.OnExplored(); }
    }
}
#endif

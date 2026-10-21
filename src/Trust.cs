#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Trust: a raven gets used to the one it travels with. Kept in the raven itself (level and points),
    // shown only in the Book of ravens.
    //   - Points come from feeding a hungry raven (Satiety: only the food it likes) and from what you do
    //     together: every raven learns from both the peaceful and the fighting life, its own side a bit more,
    //     with a daily limit, so neither runs ahead by farming one thing.
    //   - A raven just bought is not awake yet (level 0): it sits and talks, but has no abilities, passives,
    //     bonus or strength until the first ritual (Eikthyr's trophies) wakes it.
    //   - With the bar full, a ritual at the Sacrificial stones (where the boss trophies are left) raises the level:
    //     the raven eats the next boss's trophies.
    //   - Trust is lost: on death (both worn ravens), when the raven is taken off, and while it starves (satiety 0).
    //     A full bar is frozen: nothing is lost until the ritual, which starts the next bar from zero.
    //   - A level raises the raven's strength (100 at the first, 300 at the last) and its abilities.
    internal static class Trust
    {
        private const string LevelKey = "RavenWatch_trustLevel";
        private const string PointsKey = "RavenWatch_trust";
        private const string DeedDayKey = "RavenWatch_deedDay";
        private const string DeedKey = "RavenWatch_deedToday";

        internal static float MinStrength = 100f, MaxStrength = 300f;
        internal static float PerLevelPoints = 100f;     // level 1 -> 2 takes 100, each next 100 more
        internal static float DeedOwnShare = 0.5f, DeedOtherShare = 0.35f, DeedDailyCap = 40f;
        internal static float DeathLoss = 20f, TakeOffLoss = 0.30f, HungryLossPerDay = 10f; // death: points; taking off: share of the bar
        internal static float RitualRange = 25f;
        // after a ritual the raven finishes its words, flies off, and comes back a little later, grown
        internal static float WakeAwaySeconds = 60f;    // after the waking ritual: a short trip
        internal static float RitualAwaySeconds = 1800f; // after every later one: half an hour at Odin's
        private const float RitualLeaveDelay = 7f; // the ritual's message has faded and the raven has had its say before it takes off
        private const string LeaveKey = "RavenWatch_leaveAt"; // world time it takes off after a ritual
        private const string AwayWhyKey = "RavenWatch_awayWhy"; // "ritual" or "shy": what it says while away and when back
        // a shy raven (trust level 1, less often at level 2) now and then flies off for a few minutes
        internal static float WanderCheckMinutes = 10f;
        internal static float[] WanderChance = { 0.45f, 0.15f };     // at each check, for level 1 and level 2
        internal static float[] WanderMinMinutes = { 3f, 2f }, WanderMaxMinutes = { 6f, 4f };
        private const string AwayKey = "RavenWatch_awayUntil";
        private const string WanderKey = "RavenWatch_wanderCheck";
        internal const string OldHeads = "TrophyTheElder:4,TrophyBonemass:3,TrophyDragonQueen:3,TrophyGoblinKing:3,TrophySeekerQueen:3,TrophyFader:3";
        internal const string ThreeHeads = "TrophyEikthyr:3,TrophyTheElder:4,TrophyBonemass:3,TrophyDragonQueen:3,TrophyGoblinKing:3,TrophySeekerQueen:3,TrophyFader:3"; // until 2026-10-07
        // two of the boss's trophies for each ritual: a boss is killed twice per raven, not three or four times
        internal static string Heads = "TrophyEikthyr:2,TrophyTheElder:2,TrophyBonemass:2,TrophyDragonQueen:2,TrophyGoblinKing:2,TrophySeekerQueen:2,TrophyFader:2";
        internal static string HuginFoods = "Raspberry:5,Blueberries:6,Cloudberry:10,Honey:8,Mushroom:5,MushroomYellow:8,MushroomBlue:12,MushroomJotunPuffs:12,MushroomMagecap:14,Fish1:6,Fish2:8,Fish5:10,Fish4_cave:12,Fish6:12,Fish7:12,Fish8:12,Fish9:14,Fish10:14,Fish11:16,Fish12:16";
        internal static string MuninFoods = "RawMeat:5,DeerMeat:6,NeckTail:5,WolfMeat:10,LoxMeat:12,SerpentMeat:14,HareMeat:12,ChickenMeat:12,BugMeat:14,AsksvinMeat:16,VoltureMeat:16,BoneMawSerpentMeat:16";

        // ---------------------------------------------------------------- the numbers

        // level 0: not awake yet; each ritual one level up, one per entry of the heads list
        internal static int MaxLevel { get { return Steps().Count; } }

        private static string Read(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        private static void Write(ItemDrop.ItemData item, string key, string v)
        {
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[key] = v;
        }

        internal static int Level(ItemDrop.ItemData item)
        {
            int l;
            // a raven from before trust existed has no level written: it counts as awake already
            return int.TryParse(Read(item, LevelKey), out l) ? Mathf.Clamp(l, 0, MaxLevel) : 1;
        }

        internal static float Points(ItemDrop.ItemData item)
        {
            float v;
            return float.TryParse(Read(item, PointsKey), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? Mathf.Max(0f, v) : 0f;
        }

        // points to the next level (0 for waking up, and at the last level)
        internal static float Need(ItemDrop.ItemData item)
        {
            int l = Level(item);
            return l <= 0 || l >= MaxLevel ? 0f : PerLevelPoints * l;
        }

        internal static bool Active(ItemDrop.ItemData item) { return item != null && Level(item) >= 1; }

        // ready for the ritual: asleep (it only needs the heads), or the bar is full; a full bar is frozen
        internal static bool Full(ItemDrop.ItemData item)
        {
            int l = Level(item);
            if (l >= MaxLevel) return false;
            if (l <= 0) return true;
            return Points(item) >= Need(item);
        }

        // a raven just bought starts asleep; the Fate stone and splits never touch a level already there
        internal static void InitNew(ItemDrop.ItemData item) { if (Read(item, LevelKey) == null) Write(item, LevelKey, "0"); }

        private static void SetPoints(ItemDrop.ItemData item, float v)
        {
            float need = Need(item);
            Write(item, PointsKey, Mathf.Clamp(v, 0f, need > 0f ? need : 0f).ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static void SetLevel(ItemDrop.ItemData item, int level, float points)
        {
            Write(item, LevelKey, Mathf.Clamp(level, 0, MaxLevel).ToString());
            SetPoints(item, points);
        }

        // the raven's most strength: from 100 at the first level up to 300 at the last, in equal steps
        internal static float Strength(ItemDrop.ItemData item)
        {
            int steps = Mathf.Max(1, MaxLevel - 1);
            return Mathf.Round(MinStrength + (MaxStrength - MinStrength) * Mathf.Max(0, Level(item) - 1) / steps);
        }

        internal static ItemDrop.ItemData Worn(bool munin)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(Player.m_localPlayer, out h, out m);
            return munin ? m : h;
        }

        // the trust level as a title, each raven its own (and the number beside it)
        private static string[] HuginRanks { get { return Lang.Arr(HuginRanksSrc); } } // (translated on every read)
        private static readonly string[] HuginRanksSrc = Lang.Keys("Чужий", "Насторожений", "Придивляється", "Знайомий", "Супутник", "Довірений", "Побратим", "Око Одіна");
        private static string[] MuninRanks { get { return Lang.Arr(MuninRanksSrc); } } // (translated on every read)
        private static readonly string[] MuninRanksSrc = Lang.Keys("Чужий", "Терпить тебе", "Бурчить менше", "Майже свій", "Бойовий кум", "Кровний брат", "Кат твоїх ворогів", "Пам'ять Одіна");

        internal static string Rank(bool munin, int level)
        {
            string[] ranks = munin ? MuninRanks : HuginRanks;
            return ranks[Mathf.Clamp(level, 0, ranks.Length - 1)];
        }

        // "Довіра: Супутник" — a "name: value" line, so tooltip mods (SeneaL UI) show it as a stat row (the number is only in the book)
        internal static string RankLine(bool munin, ItemDrop.ItemData item)
        {
            int level = Level(item);
            return Lang.F("Довіра: <color=#FFD54F>{0}</color>", Rank(munin, level));
        }

        // "2 трофеї Ейктюра": what the waking ritual asks, as the config has it (for the texts that tell it)
        internal static string WakeText()
        {
            int n = 2;
            string first = Heads.Split(',')[0];
            int colon = first.IndexOf(':');
            if (colon > 0) int.TryParse(first.Substring(colon + 1).Trim(), out n);
            return Lang.F("{0} Ейктюра", Lang.N(n, "трофей|трофеї|трофеїв"));
        }

        internal static int LevelWorn(bool munin) { if (LevelAs > 0) return LevelAs; ItemDrop.ItemData item = Worn(munin); return item != null ? Level(item) : 1; }

        // the book asks "what would the gifts be at level N": for that moment the worn raven counts as being on it
        internal static int LevelAs;

        // the abilities grow with the level: x(1 + per level), and the waits shorten
        // a value set for level 1 and for the top level; the levels between in equal steps
        internal static float Lerp(bool munin, float atFirst, float atTop) { return LerpFrom(munin, 1, atFirst, atTop); }

        // the same for a gift that opens at a later level: atFirst at that level, atTop at the top one
        internal static float LerpFrom(bool munin, int from, float atFirst, float atTop)
        {
            int steps = Mathf.Max(1, MaxLevel - from);
            return Mathf.Lerp(atFirst, atTop, Mathf.Clamp01((LevelWorn(munin) - (float)from) / steps));
        }

        internal static float Mul(bool munin, float perLevel) { return 1f + perLevel * Mathf.Max(0, LevelWorn(munin) - 1); }
        // a wait shown and kept in round numbers: whole minutes from two minutes on, else whole seconds
        internal static float RoundTime(float seconds)
        {
            return seconds >= 120f ? Mathf.Floor(seconds / 60f + 0.5f) * 60f : Mathf.Floor(seconds + 0.5f);
        }

        // a base cooldown shortened by the trust of the worn raven, rounded
        internal static float Sooner(bool munin, float seconds) { return RoundTime(seconds * CooldownMul(munin)); }

        internal static float CooldownMul(bool munin) { return Mathf.Max(0.5f, 1f - 0.04f * Mathf.Max(0, LevelWorn(munin) - 1)); }

        private static int Day() { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; }

        // ---------------------------------------------------------------- gaining

        private static void Add(ItemDrop.ItemData item, bool munin, float amount, bool tell) { Add(item, munin, amount, tell, null); }

        // note: the reason for the line in the corner (null: no line, the caller tells it itself)
        // returns the points really added (the bar stops at full)
        private static float Add(ItemDrop.ItemData item, bool munin, float amount, bool tell, string note)
        {
            if (item == null || amount <= 0f || Need(item) <= 0f) return 0f;
            amount *= RuneStars.TrustMul(item); // («Мох»: faster)
            bool wasFull = Full(item);
            float before = Points(item);
            SetPoints(item, before + amount);
            float added = Points(item) - before;
            if (note != null) GainNote.Trust(munin, added, note);
            if (!wasFull && Full(item) && tell && Player.m_localPlayer != null)
                RavenMsg.Center(Player.m_localPlayer, munin ? Lang.T("Мунін готовий, як ніколи.\nТягни трофеї до каменів, не барись.") : Lang.T("Хугін довіряє тобі повністю.\nЧас для ритуалу біля каменів."));
            return added;
        }

        // ---------------------------------------------------------------- whose raven it is
        // A raven belongs to whoever woke it (a sleeping one can be bought for a friend and given away). In anyone
        // else's hands it still gives its gifts and bonus, but it is as hostile as it gets: no trust, no memory, it only
        // swears and belittles, and every minute it pecks the one wearing it. An awake raven from before this had no
        // owner: it takes the one who wears it.
        private const string OwnerKey = "RavenWatch_owner", OwnerNameKey = "RavenWatch_ownerName";
        internal static float PeckMinutes = 1f, PeckDamage = 10f;
        private static readonly float[] NextPeck = new float[2];
        private static readonly ItemDrop.ItemData[] PeckItem = new ItemDrop.ItemData[2];

        internal static string OwnerName(ItemDrop.ItemData item) { return Read(item, OwnerKey) != null ? Read(item, OwnerNameKey) : null; }

        private static void Bind(ItemDrop.ItemData item, Player p)
        {
            if (item == null || p == null || p.GetPlayerID() == 0L) return; // not in the world yet: bound on the next look
            Write(item, OwnerKey, p.GetPlayerID().ToString());
            Write(item, OwnerNameKey, p.GetPlayerName());
            if (ReferenceEquals(p, Player.m_localPlayer)) p.m_customData[BondedKey] = "1";
        }

        // Odin's gift (Trial): this one's raven from the start, though still asleep — no one else can wake it, so it
        // cannot be won on another character and handed over. (Not "bonded": the keys wait for it to wake.)
        internal static void Promise(ItemDrop.ItemData item, Player p)
        {
            if (item == null || p == null || p.GetPlayerID() == 0L) return;
            Write(item, OwnerKey, p.GetPlayerID().ToString());
            Write(item, OwnerNameKey, p.GetPlayerName());
        }

        // This character has ever had a raven of its own (woken and bound to it): until then the keys (book, gifts,
        // orders) do nothing, there is nobody to send, only G → Ворони opens them. Kept on the character; one look at
        // the bag (and the shoulder cells) every few seconds finds ravens bound before this mark existed.
        private const string BondedKey = "RavenWatch_bonded";
        private static float _bondedLook;
        internal static bool Bonded(Player p)
        {
            if (p == null) return false;
            if (p.m_customData.ContainsKey(BondedKey)) return true;
            if (Time.time < _bondedLook) return false;
            _bondedLook = Time.time + 3f;
            Inventory inv = p.GetInventory();
            if (inv == null) return false;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
                if (IsOwner(item, p)) { p.m_customData[BondedKey] = "1"; return true; }
            // with SeneaL UI's shoulder cells a raven on the shoulder is not in the bag at all
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p))
                if (IsOwner(item, p)) { p.m_customData[BondedKey] = "1"; return true; }
            return false;
        }

        internal static bool Stranger(ItemDrop.ItemData item, Player p)
        {
            string id = Read(item, OwnerKey);
            return p != null && item != null && !string.IsNullOrEmpty(id) && id != p.GetPlayerID().ToString();
        }

        // the one who woke it
        internal static bool IsOwner(ItemDrop.ItemData item, Player p)
        {
            string id = Read(item, OwnerKey);
            return p != null && !string.IsNullOrEmpty(id) && id == p.GetPlayerID().ToString();
        }

        // brought back from its copy: the same level, an empty bar, at home (not flown off, not leaving after a ritual)
        internal static void Returned(ItemDrop.ItemData item)
        {
            foreach (string key in new string[] { AwayKey, AwayWhyKey, LeaveKey, WanderKey }) item.m_customData.Remove(key);
            SetLevel(item, Level(item), 0f);
            Satiety.Fresh(item); // not starving from the copy's old clock
        }

        // ---------------------------------------------------------------- starving
        // Satiety down to 0: the raven droops on the shoulder (seen by
        // everyone, a net flag), its strike is weaker (Strike.StarveMul), and now and then it pecks you for it.
        internal static float StarveStrikePct = 50f, StarvePeckMinutes = 30f, StarvePeckDamage = 10f;
        internal const string StarveKeyH = "RavenWatch_StarveH", StarveKeyM = "RavenWatch_StarveM";
        private static readonly ItemDrop.ItemData[] StarveItem = new ItemDrop.ItemData[2];
        private static readonly float[] NextStarvePeck = new float[2];
        private static readonly int[] LastStarveLine = { -1, -1 };

        // the console's "raven starve": satiety 0 at once, the first peck soon
        internal static void StarveNow(bool munin)
        {
            ItemDrop.ItemData item = Worn(munin);
            if (item == null) return;
            Satiety.Set(item, 0f);
            StarveItem[munin ? 1 : 0] = item;
            NextStarvePeck[munin ? 1 : 0] = Time.time + 15f;
        }

        internal static bool Starving(ItemDrop.ItemData item)
        {
            return Satiety.IsStarving(item);
        }

        private static string[] HuginStarveLines { get { return Lang.Pool(HuginStarveLinesSrc); } } // (translated on every read)
        private static readonly string[] HuginStarveLinesSrc = Lang.Keys("Це тобі за порожній живіт. Ворони пам'ятають, хто їх годує.", "Дзьоб — то нагадування. Я голодний, якщо ти раптом забу{в|ла}.", "Ти сьогодні ї{в|ла}? А я ні. Ось і рахуйся.", "Мудрість натщесерце гірчить. І дзьобає.", "Одін своїх воронів годував. Просто кажу.", "Клюю не зі зла — з голоду. Зміниш одне, минеться й друге.", "Жменька ягід — і я знову чемний. Домовились?", "Голодний розум погано радить. І боляче клює.");

        private static string[] MuninStarveLines { get { return Lang.Pool(MuninStarveLinesSrc); } } // (translated on every read)
        private static readonly string[] MuninStarveLinesSrc = Lang.Keys("На тобі, жмот! Це за те, шо я досі не жрав.", "Сам жереш, а мені шо? Кістку хоч кинь, скнаро, бля!", "Нагодуєш — перестану. Не нагодуєш — ше дам. Все чесно.", "Я тобі шо, прикраса на плечі? Я ЖЕРТИ хочу, курва!", "Ше раз забудеш — виклюю шось важливіше за вухо. Вкурив?", "Голодний ворон — злий ворон. А я, бля, дуже голодний ворон.", "Оце за вчора. А завтра буде за сьодні, якшо нічо не даси.", "Шлунок пустий, терпець теж. На, тримай, сука!");

        private static void Starve(Player p, bool munin, ItemDrop.ItemData item)
        {
            int slot = munin ? 1 : 0;
            bool on = Starving(item) && !Away(item);
            NetFlag.Set(p, munin ? StarveKeyM : StarveKeyH, on);
            if (!on) { StarveItem[slot] = null; return; }
            // the first peck comes a full interval after it starts starving (or after a relog)
            if (!ReferenceEquals(StarveItem[slot], item)) { StarveItem[slot] = item; NextStarvePeck[slot] = Time.time + StarvePeckMinutes * 60f; return; }
            if (Time.time < NextStarvePeck[slot]) return;
            string off;
            if (RavenSkills.OffShoulder(p, munin, out off)) return; // out striking or scouting: once it is back
            if (RavenSkills.Hushed(p)) { NextStarvePeck[slot] = Time.time + 15f; return; } // not over a talk: a little later
            NextStarvePeck[slot] = Time.time + Mathf.Max(60f, StarvePeckMinutes * 60f);
            if (p.IsDead() || p.GetHealth() <= StarvePeckDamage) return; // you are too weak: it holds back this time
            p.SetHealth(p.GetHealth() - StarvePeckDamage);
            if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, p.GetTopPoint(), StarvePeckDamage, true);
            ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 0.5f);
            string made = Satiety.Say(p, munin, "starve_peck"); // phrase file, by trust tone
            if (!string.IsNullOrEmpty(made)) { RavenSkills.SayFromShoulder(p, munin, made); return; }
            string[] lines = munin ? MuninStarveLines : HuginStarveLines;
            int pick = UnityEngine.Random.Range(0, lines.Length);
            if (pick == LastStarveLine[slot]) pick = (pick + 1) % lines.Length; // never the same line twice in a row
            LastStarveLine[slot] = pick;
            RavenSkills.SayFromShoulder(p, munin, lines[pick]);
        }

        // A starving raven told to do something: it will not, and below trust level OrderPeckBelow it pecks you for it
        // (once in OrderPeckGap s, never the last of your health), with its starving words
        internal static float OrderPeckBelow = 6f, OrderPeckGap = 10f;
        private static readonly float[] NextOrderPeck = new float[2];
        internal static bool OrderPeck(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (p == null || item == null || p.IsDead() || SettledLevel(item) >= OrderPeckBelow) return false;
            int slot = munin ? 1 : 0;
            if (Time.time < NextOrderPeck[slot]) return false;
            NextOrderPeck[slot] = Time.time + OrderPeckGap;
            if (p.GetHealth() <= StarvePeckDamage) return false;
            p.SetHealth(p.GetHealth() - StarvePeckDamage);
            if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, p.GetTopPoint(), StarvePeckDamage, true);
            ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 0.5f);
            string made = Satiety.Say(p, munin, "starve_peck");
            if (string.IsNullOrEmpty(made))
            {
                string[] lines = munin ? MuninStarveLines : HuginStarveLines;
                made = lines[UnityEngine.Random.Range(0, lines.Length)];
            }
            RavenSkills.SayFromShoulder(p, munin, made);
            return true;
        }

        // awake and on a stranger's shoulder (yours): it only spits venom
        internal static bool Hostile(ItemDrop.ItemData item) { return Active(item) && Stranger(item, Player.m_localPlayer); }

        // on a stranger's shoulder: a peck every PeckMinutes, never when it would take the last of the health
        private static void Peck(Player p, bool munin, ItemDrop.ItemData item)
        {
            int slot = munin ? 1 : 0;
            if (!Stranger(item, p) || !Active(item) || Away(item)) { PeckItem[slot] = null; return; }
            if (!ReferenceEquals(PeckItem[slot], item)) { PeckItem[slot] = item; NextPeck[slot] = Time.time + PeckMinutes * 60f; return; }
            if (Time.time < NextPeck[slot]) return;
            NextPeck[slot] = Time.time + Mathf.Max(10f, PeckMinutes * 60f);
            if (p.IsDead() || p.GetHealth() <= PeckDamage) return; // too weak: it holds back this time
            p.SetHealth(p.GetHealth() - PeckDamage);
            if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, p.GetTopPoint(), PeckDamage, true);
            ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 0.5f);
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, "stranger_peck") : null;
            RavenSkills.SayFromShoulder(p, munin, string.IsNullOrEmpty(line) ? (munin ? Lang.T("Ти не мій. На!") : Lang.T("Ти не мій господар.")) : line);
        }

        // something done together (the same events that give strength): both worn ravens learn, the raven
        // of that side more; a daily limit per raven
        internal static void Deed(bool sideMunin, float amount) { Deed(sideMunin, amount, null); }

        internal static void Deed(bool sideMunin, float amount, string why)
        {
            if (amount <= 0f) return;
            foreach (bool munin in new bool[] { false, true })
                DeedFor(munin, amount * (munin == sideMunin ? DeedOwnShare : DeedOtherShare), why ?? Lang.T("спільна справа"));
        }

        // trust from food: at most this much a game day per raven (kept in the raven)
        internal static float FoodDailyCap = 24f;
        private const string FoodWorldKey = "RavenWatch_foodWorld", FoodDayKey = "RavenWatch_foodDay", FoodKey = "RavenWatch_foodToday";

        private static string FoodWorld()
        {
            return ZNet.instance != null ? ZNet.instance.GetWorldUID().ToString() : null;
        }

        internal static float FoodLeftToday(ItemDrop.ItemData item)
        {
            string world = FoodWorld();
            int d;
            float today = 0f;
            if (item != null && !string.IsNullOrEmpty(world) && Read(item, FoodWorldKey) == world
                && int.TryParse(Read(item, FoodDayKey), out d) && d == Day())
                float.TryParse(Read(item, FoodKey), NumberStyles.Float, CultureInfo.InvariantCulture, out today);
            return Mathf.Max(0f, FoodDailyCap - today);
        }

        private static void FoodTaken(ItemDrop.ItemData item, float amount)
        {
            string world = FoodWorld();
            if (item == null || amount <= 0f || string.IsNullOrEmpty(world)) return;
            float today = FoodDailyCap - FoodLeftToday(item);
            Write(item, FoodWorldKey, world);
            Write(item, FoodDayKey, Day().ToString());
            Write(item, FoodKey, (today + amount).ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static float TalkTrust = 1f, TalkDailyCap = 10f;
        private const string TalkDayKey = "RavenWatch_talkDay", TalkKey = "RavenWatch_talkToday";
        internal static float OrderTrust = 1f, OrderDailyCap = 8f;
        private const string OrderDayKey = "RavenWatch_orderDay", OrderKey = "RavenWatch_orderToday";

        // a thought asked for is a talk together: trust for that raven only, at most TalkDailyCap a game day
        // (talking by the fire is cheap: it must not fill the whole day's trust), and within the deeds' daily limit too
        internal static void Talked(bool munin) { Together(munin, TalkTrust, TalkDailyCap, TalkDayKey, TalkKey, Lang.T("розмова")); }
        // an order flown (Orders): play together, as a talk is; its own little limit a game day
        internal static void Ordered(bool munin) { Together(munin, OrderTrust, OrderDailyCap, OrderDayKey, OrderKey, Lang.T("наказ")); }

        private static void Together(bool munin, float amount, float cap, string dayKey, string key, string why)
        {
            ItemDrop.ItemData item = Worn(munin);
            if (item == null || amount <= 0f) return;
            int day = Day(), d;
            float today = 0f;
            if (int.TryParse(Read(item, dayKey), out d) && d >= day) float.TryParse(Read(item, key), NumberStyles.Float, CultureInfo.InvariantCulture, out today);
            float give = DeedFor(munin, Mathf.Min(amount, cap - today), why);
            if (give <= 0f) return; // nothing taken in (a full bar, the day's deeds used up, not its owner): this limit stays
            Write(item, dayKey, Mathf.Max(d, day).ToString());
            Write(item, key, (today + give).ToString("0.##", CultureInfo.InvariantCulture));
        }

        // the trust really taken in
        private static float DeedFor(bool munin, float amount, string why)
        {
            ItemDrop.ItemData item = Worn(munin);
            if (item == null || amount <= 0f || Stranger(item, Player.m_localPlayer) || Need(item) <= 0f || Full(item)) return 0f; // not its owner, or nothing to fill: no trust grows
            if (Away(item)) return 0f; // flown off (to Odin, wandering, on a perch): it did not see it
            int day = Day();
            float today = 0f;
            int d;
            if (int.TryParse(Read(item, DeedDayKey), out d) && d >= day) float.TryParse(Read(item, DeedKey), NumberStyles.Float, CultureInfo.InvariantCulture, out today);
            float give = Mathf.Min(amount, DeedDailyCap - today);
            if (give <= 0f) return 0f;
            give = Add(item, munin, give, true, why);
            if (give <= 0f) return 0f;
            Write(item, DeedDayKey, day.ToString());
            Write(item, DeedKey, (today + give).ToString("0.##", CultureInfo.InvariantCulture));
            return give;
        }

        // ---------------------------------------------------------------- feeding

        // Class: where it comes from (1 Meadows .. 5 Ashlands); Fill: how much satiety it gives
        internal sealed class Food { public string Prefab; public int Points; public string Name; public int Class; public float Fill; }

        private static readonly List<Food>[] _foods = new List<Food>[2];
        private static readonly string[] _foodsFrom = new string[2];
        private static readonly ObjectDB[] _foodsDb = new ObjectDB[2];

        // what each raven eats; made once per config list and item database (the book asks every second). Do not change the list.
        internal static List<Food> Foods(bool munin)
        {
            int i = munin ? 1 : 0;
            string from = munin ? MuninFoods : HuginFoods;
            if (_foods[i] != null && _foodsFrom[i] == from && _foodsDb[i] == ObjectDB.instance && ObjectDB.instance != null) return _foods[i];
            List<Food> list = new List<Food>();
            foreach (string part in from.Split(','))
            {
                string[] kv = part.Split(':');
                int pts;
                if (kv.Length < 2 || kv.Length > 3 || !int.TryParse(kv[1].Trim(), out pts)) continue;
                int cls = 0;
                if (kv.Length == 3) int.TryParse(kv[2].Trim(), out cls);
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(kv[0].Trim()) : null;
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null) continue; // not in this game (or a mod's name): skipped
                Food f = new Food();
                f.Prefab = kv[0].Trim(); f.Points = pts;
                f.Class = Satiety.ClassOf(f.Prefab, pts, cls); f.Fill = Satiety.FillOfClass(f.Class);
                f.Name = Localization.instance != null ? Lang.Game(drop.m_itemData.m_shared.m_name) : drop.m_itemData.m_shared.m_name;
                list.Add(f);
            }
            _foods[i] = list; _foodsFrom[i] = from; _foodsDb[i] = ObjectDB.instance;
            return list;
        }

        internal static bool CanEat(ItemDrop.ItemData item, Food food)
        {
            if (item == null || food == null || Level(item) < 1) return false;
            int level = Level(item);
            int minClass = level <= 2 ? 1 : 2;
            int maxClass = level <= 2 ? 3 : level <= 5 ? 4 : 5;
            return food.Class >= minClass && food.Class <= maxClass;
        }

        internal static List<Food> EdibleFoods(bool munin, ItemDrop.ItemData item)
        {
            List<Food> foods = new List<Food>();
            foreach (Food food in Foods(munin)) if (CanEat(item, food) && !Quirks.Dislikes(item, food.Prefab)) foods.Add(food); // («Перебірливий»: not what it will not eat)
            return foods;
        }

        internal static string FoodRuleText(int level)
        {
            if (level <= 2) return Lang.T("довіра 1–2: їжа класів 1–3");
            if (level <= 5) return Lang.T("довіра 3–5: їжа класів 2–4");
            return Lang.T("довіра 6–7: їжа класів 2–5");
        }

        // it asks for food: awake, yours, here, and its satiety 40 or less (food matters even with the trust bar full)
        internal static bool Hungry(ItemDrop.ItemData item, Player p)
        {
            return item != null && Active(item) && !Stranger(item, p) && !Away(item) && Satiety.Value(item) <= Satiety.AskAt;
        }

        // fed enough not to ask (the name from the once-a-day feeding)
        internal static bool FedToday(ItemDrop.ItemData item) { return Satiety.Value(item) > Satiety.AskAt; }

        internal static int Have(Player p, string prefab)
        {
            int n = 0;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) n += it.m_stack;
            return n;
        }

        internal static string Feed(Player p, bool munin, Food food) { bool ate; return Feed(p, munin, food, false, false, out ate); }

        // fed where it sits in the Raven's Nest (from the bowl's window): it is not on the shoulder, and that is fine there
        internal static string FeedInNest(Player p, bool munin, Food food) { bool ate; return Feed(p, munin, food, false, true, out ate); }

        // The raven eats from its own pouch (RavenPouch), by itself: the caller takes the piece out of the pouch when it
        // ate. Quiet when it will not eat it (no refusal said); a line of its own now and then; the message for the corner.
        internal static string FeedFromPouch(Player p, bool munin, Food food, out bool ate) { return Feed(p, munin, food, true, false, out ate); }

        private static string Feed(Player p, bool munin, Food food, bool pouch, bool nest, out bool ate)
        {
            ate = false;
            ItemDrop.ItemData item = Worn(munin);
            if (item == null) return Lang.F("{0} зараз не на тобі.", Plugin.RavenName(munin));
            if (!Active(item)) return Lang.F("{0} ще спить: спершу ритуал.", Plugin.RavenName(munin));
            if (Stranger(item, p)) return Lang.F("{0} не твій — з твоїх рук не їсть.\nЙого господар: {1}.", Plugin.RavenName(munin), OwnerName(item));
            if (nest ? AwayLeft(item) > 0f : Away(item)) return AwayAction(munin, item, Lang.T("нема кого годувати")); // (in the nest it sits on a perch: fed there)
            string off, sulk = pouch ? null : Quirks.Sulk(p, munin); // «Ревнивий»: not even from your hand (its own pouch is not your hand)
            if (sulk != null) return Lang.F("{0}: навіть їсти з твоїх рук не хоче.", sulk);
            if (!nest && RavenSkills.OffShoulder(p, munin, out off)) return Lang.F("{0} зараз {1}.\nНагодуєш, коли повернеться на плече.", Plugin.RavenName(munin), off);
            if (food == null) return Lang.F("{0} не розуміє, що ти йому пропонуєш.", Plugin.RavenName(munin));
            if (pouch && (!CanEat(item, food) || Quirks.Dislikes(item, food.Prefab) || Satiety.Value(item) >= 99.5f)) return null;
            if (!CanEat(item, food))
            {
                string refusal = Satiety.Say(p, munin, "food_refused");
                RavenSkills.SayFromShoulder(p, munin, !string.IsNullOrEmpty(refusal) ? refusal : munin
                    ? Lang.T("Не тицяй мені це. Ще не доріс до такого харчу.")
                    : Lang.T("Дякую, але це поки не моя їжа. Доведеться тобі її з'їсти."));
                return Lang.F("{0} відмовився від {1}: {2}.", Plugin.RavenName(munin), food.Name, FoodRuleText(Level(item)));
            }
            string picky = Quirks.PickyRefuses(p, munin, item, food); // «Перебірливий»: this one it will not eat
            if (picky != null) { Quirks.Act(p, munin, Lang.T("Воротить дзьоб")); return picky; }
            float before = Satiety.Value(item);
            if (before >= 99.5f) return Lang.F("{0} більше не влізе: ситий по самий дзьоб.", Plugin.RavenName(munin));
            if (!pouch)
            {
                if (Have(p, food.Prefab) <= 0) return Lang.F("У тебе нема: {0}.", food.Name);
                p.GetInventory().RemoveItem(FindName(p, food.Prefab), 1, -1, false);
            }
            ate = true;
            Nicks.OnFed(item); // a deed for its nickname
            bool fav = Satiety.IsFav(item, food.Prefab);
            bool first = fav && !Satiety.FavKnown(item);
            float trust = Satiety.TrustOf(item, food, fav); // counted before it fills
            Satiety.Change(item, Satiety.FillOf(food, fav));
            float after = Satiety.Value(item);
            Strike.Fed(item, fav ? Satiety.FavFedMul : 1f); // a fed raven strikes harder for a while; after its favourite longer
            float added = trust > 0f ? Add(item, munin, trust, true, null) : 0f;
            if (added > 0f) FoodTaken(item, added);
            bool capped = trust <= 0f && FoodLeftToday(item) <= 0f && Need(item) > 0f && !Full(item) && before <= Satiety.AskAt;
            if (first) Satiety.Reveal(item);
            RavenTalk.Fed(munin, food.Name); // the other one may have something to say about it
            if (!pouch) Quirks.Fed(p, munin); // «Ревнивий»: the other one takes offence (not when it helps itself)
            string trustText = added >= 0.05f ? Lang.F(", довіра +{0}", Lang.Num(added, "0.#"))
                : Need(item) <= 0f || Full(item) ? "" : capped ? Lang.F(", довіри — ні (на сьогодні з їжі вже {0})", RavenAbilities.R(FoodDailyCap)) : Lang.T(", довіри — ні (не був голодний)");
            if (pouch)
            {
                if (UnityEngine.Random.value < RavenPouch.SayChance)
                {
                    string own = first ? Satiety.FavLineSaid(p, munin, true, food.Name) : Satiety.Say(p, munin, "pouch_ate", "food", food.Name);
                    if (!string.IsNullOrEmpty(own)) RavenSkills.SayFromShoulder(p, munin, own);
                }
                return Lang.F("{0} сам поїв із торби: {1}{2}. Ситість {3} → {4}{5}.{6}", Plugin.RavenName(munin), food.Name, (fav && !first ? " ★" : ""), Mathf.RoundToInt(before), Mathf.RoundToInt(after), trustText, (first ? Lang.T("\nЦе його улюблена їжа!") : ""));
            }
            string line = fav ? Satiety.FavLineSaid(p, munin, first, food.Name)
                : before <= Satiety.StuffedAt && after > Satiety.StuffedAt ? Satiety.StuffedSaid(p, munin)
                : before > Satiety.AskAt ? Satiety.Say(p, munin, "fed_full") // not hungry: eats for the company
                : null;
            if (string.IsNullOrEmpty(line) && Plugin.Instance != null) line = Plugin.Instance.ComposeTagged(p, munin, "fed");
            RavenSkills.SayFromShoulder(p, munin, string.IsNullOrEmpty(line) ? (munin ? Lang.T("Ням. Більше давай, скупердяю.") : Lang.T("Дякую. Смачно.")) : line);
            return Lang.F("{0} з'їв: {1}{2}. Ситість {3} → {4}{5}.{6}", Plugin.RavenName(munin), food.Name, (fav ? " ★" : ""), Mathf.RoundToInt(before), Mathf.RoundToInt(after), trustText, (first ? Lang.T("\nЦе його улюблена їжа!") : ""));
        }

        // Inventory.RemoveItem takes the item's name (the $ key), not the prefab's
        internal static string FindName(Player p, string prefab)
        {
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) return it.m_shared.m_name;
            return prefab;
        }

        // ---------------------------------------------------------------- the ritual

        internal sealed class Step { public string Prefab; public int Count; public string Name; }

        // what each level asks for, from level 2 on: "TrophyTheElder:4,..."
        private static List<Step> _steps;
        private static string _stepsFrom;
        private static ObjectDB _stepsDb;

        internal static List<Step> Steps()
        {
            if (_steps != null && _stepsFrom == Heads && _stepsDb == ObjectDB.instance) return _steps;
            List<Step> list = new List<Step>();
            foreach (string part in Heads.Split(','))
            {
                string[] kv = part.Split(':');
                int n;
                if (kv.Length != 2 || !int.TryParse(kv[1].Trim(), out n) || n <= 0) continue;
                Step s = new Step();
                s.Prefab = kv[0].Trim(); s.Count = n;
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(s.Prefab) : null;
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                s.Name = drop != null && Localization.instance != null ? Lang.Game(drop.m_itemData.m_shared.m_name) : s.Prefab;
                list.Add(s);
            }
            _steps = list; _stepsFrom = Heads; _stepsDb = ObjectDB.instance;
            return list;
        }

        internal static Step NextStep(ItemDrop.ItemData item)
        {
            List<Step> steps = Steps();
            int i = Level(item);
            return i >= 0 && i < steps.Count ? steps[i] : null;
        }

        // the stone of the boss whose trophy is offered, or else the nearest one in reach
        private static BossStone[] _stones;
        private static float _stonesAt = -1f;

        // every Sacrificial stone the game has loaded, found at most twice a second (the book and the ritual ask often)
        internal static BossStone[] Stones()
        {
            if (_stones == null || Time.unscaledTime - _stonesAt > 0.5f || Time.unscaledTime < _stonesAt)
            {
                _stones = UnityEngine.Object.FindObjectsByType<BossStone>(FindObjectsSortMode.None);
                _stonesAt = Time.unscaledTime;
            }
            return _stones;
        }

        internal static BossStone StoneFor(Player p, string trophy)
        {
            BossStone best = null, any = null;
            float bestD = RitualRange + 5f, anyD = RitualRange + 5f;
            foreach (BossStone s in Stones())
            {
                if (s == null) continue;
                float d = Vector3.Distance(s.transform.position, p.transform.position);
                if (d < anyD) { anyD = d; any = s; }
                if (d < bestD && trophy != null && s.m_itemStand != null && s.m_itemStand.m_supportedItems != null)
                    foreach (ItemDrop it in s.m_itemStand.m_supportedItems)
                        if (it != null && it.gameObject.name == trophy) { bestD = d; best = s; break; }
            }
            return best != null ? best : any;
        }

        // the stone of this trophy's boss within the ritual's reach, or null
        internal static BossStone ExactStone(Player p, string trophy)
        {
            if (p == null || trophy == null) return null;
            foreach (BossStone s in Stones())
            {
                if (s == null || s.m_itemStand == null || s.m_itemStand.m_supportedItems == null) continue;
                if (Vector3.Distance(s.transform.position, p.transform.position) > RitualRange) continue;
                foreach (ItemDrop it in s.m_itemStand.m_supportedItems)
                    if (it != null && it.gameObject.name == trophy) return s;
            }
            return null;
        }

        // The boss's own trophy hangs on its stone: the raven is offered the heads of a boss you have truly brought
        // down and honoured. null: you are not at the stones (nothing to tell); otherwise yes or no.
        internal static bool? TrophyHung(Player p, string trophy)
        {
            if (!AtStones(p)) return null;
            BossStone s = ExactStone(p, trophy);
            return s != null && s.m_itemStand.HaveAttachment();
        }

        // What hangs on the stones is only known while they are near (their part of the world loaded): it is
        // remembered on the character whenever you are there (every few seconds), and the book shows the last seen
        // state from afar. The ritual itself always looks at the stone (it is held there).
        private const string HungPrefix = "RavenWatch_Hung_";
        private static float _watchAt;

        internal static void WatchStones(Player p)
        {
            if (p == null || Time.time < _watchAt) return;
            _watchAt = Time.time + 3f;
            if (!AtStones(p)) return;
            foreach (BossStone s in Stones())
            {
                if (s == null || s.m_itemStand == null || s.m_itemStand.m_supportedItems == null) continue;
                if (Vector3.Distance(s.transform.position, p.transform.position) > RitualRange) continue;
                ZNetView nv = s.m_itemStand.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue; // not loaded yet: nothing to tell
                string hung = s.m_itemStand.HaveAttachment() ? "1" : "0";
                foreach (ItemDrop it in s.m_itemStand.m_supportedItems)
                    if (it != null) p.m_customData[HungPrefix + it.gameObject.name] = hung;
            }
        }

        // for the book: at the stones as it is now (and remembered), elsewhere as last seen there; null: never seen
        internal static bool? TrophyHungKnown(Player p, string trophy, out bool live)
        {
            bool? now = TrophyHung(p, trophy);
            live = now != null;
            if (p == null || trophy == null) return now;
            if (now != null) { p.m_customData[HungPrefix + trophy] = now.Value ? "1" : "0"; return now; }
            string v;
            if (p.m_customData.TryGetValue(HungPrefix + trophy, out v)) return v == "1";
            return null;
        }

        internal static bool AtStones(Player p)
        {
            foreach (BossStone s in Stones())
                if (s != null && Vector3.Distance(s.transform.position, p.transform.position) <= RitualRange) return true;
            return false;
        }

        internal static string Ritual(Player p, bool munin)
        {
            ItemDrop.ItemData item = Worn(munin);
            if (item == null) return Lang.F("{0} зараз не на тобі.", Plugin.RavenName(munin));
            if (Away(item)) return AwayAction(munin, item, Lang.T("ритуал без нього не проведеш"));
            string off;
            if (RavenSkills.OffShoulder(p, munin, out off)) return Lang.F("{0} зараз {1}.\nРитуал — коли повернеться на плече.", Plugin.RavenName(munin), off);
            string sulk = Quirks.Sulk(p, munin);
            if (sulk != null) return Lang.F("{0}.\nРитуал — коли відійде.", sulk);
            if (Stranger(item, p)) return Lang.F("{0} не твій — обряд з ним проведе лише {1}.", Plugin.RavenName(munin), OwnerName(item));
            Step step = NextStep(item);
            if (step == null) return Lang.F("{0} уже довіряє тобі цілком.", Plugin.RavenName(munin));
            if (!Full(item)) return Lang.F("{0} ще не готовий: довіра {1}/{2}.", Plugin.RavenName(munin), Mathf.FloorToInt(Points(item)), Mathf.RoundToInt(Need(item)));
            if (!AtStones(p)) return munin ? Lang.T("Тут Мунін обряду не прийме, йолопе.\nДо Жертовних каменів!") : Lang.T("Хугін чекатиме біля Жертовних каменів.\nТут обряду не буде.");
            bool? hung = TrophyHung(p, step.Prefab);
            if (hung != true) return munin
                ? Lang.F("Мунін на голі камені не клює.\nСпершу повісь {0} на камінь цього боса, тоді й поговоримо.", step.Name)
                : Lang.F("Хугін дивиться на порожній камінь.\nСпершу повісь {0} на камінь цього боса — тоді обряд.", step.Name);
            if (Have(p, step.Prefab) < step.Count) return Lang.F("Потрібно: {0} ×{1} (є {2}).", step.Name, step.Count, Have(p, step.Prefab));
            p.GetInventory().RemoveItem(FindName(p, step.Prefab), step.Count, -1, false);
            bool waking = Level(item) == 0;
            if (waking) Bind(item, p); // from now on it is this one's raven
            SetLevel(item, Level(item) + 1, 0f);
#if RAVEN_STORY
            StoryBus.Raise("trust", munin ? "munin" : "hugin", Level(item));
#endif
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            RitualFx.Play(p, munin, step.Prefab); // from the head on the stone to the raven, here and for everyone nearby
            LastChance.Flash(p, chest, munin ? new Color(1f, 0.3f, 0.2f) : new Color(0.6f, 0.8f, 1f));
            LastChance.Ring(chest, new Color(1f, 0.9f, 0.6f, 1f), munin ? new Color(0.7f, 0.1f, 0.1f, 1f) : new Color(0.3f, 0.5f, 0.9f, 1f));
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, waking ? "awake" : "levelup") : null;
            RavenSkills.SayFromShoulder(p, munin, string.IsNullOrEmpty(line) ? Lang.T("Тепер я з тобою по-справжньому.") : line);
            Write(item, LeaveKey, (WorldNow + RitualLeaveDelay).ToString("R", CultureInfo.InvariantCulture));
            // the flight to Odin is told on its own when the raven takes off (RitualLeaveText), not here as well
            Backup.Save(p, item); // a new level is worth a fresh copy at once
            if (waking) return munin ? Lang.T("Мунін прокинувся й вилаявся!") : Lang.T("Хугін розплющив очі й упізнав тебе!");
            return Lang.F("{0} із {1}.", ((munin ? Lang.T("Мунін, бля, визнав тебе: довіра ") : Lang.T("Хугін довіряє тобі більше: ")) + Level(item)), MaxLevel);
        }

        // ---------------------------------------------------------------- losing

        // returns the points really lost (the bar stops at empty)
        private static float Lose(ItemDrop.ItemData item, float points)
        {
            if (item == null || Full(item) || points <= 0f) return 0f; // a full bar is frozen until the ritual
            if (Away(item) || Stranger(item, Player.m_localPlayer)) return 0f; // not here, or not yours to lose
            float before = Points(item);
            SetPoints(item, before - points);
            return before - Points(item);
        }

        // starving: trust melts (from Satiety, by the time it spent at 0); a full bar is frozen, the top level has none
        internal static void Melt(ItemDrop.ItemData item, float points)
        {
            if (item == null || points <= 0f || Full(item) || !Active(item) || Stranger(item, Player.m_localPlayer)) return;
            SetPoints(item, Points(item) - points);
        }

        // A death costs less on the first levels (new players die most and grow slowest). What it took is written on the
        // raven: come back for it to the grave soon (RavenSeat, from the grave onto the shoulder) and it gives a share
        // back. A raven that stayed on the shoulder («Вірність») never lay in the grave: nothing comes back to it.
        internal static float DeathLossEarly = 10f, GraveBackShare = 0.5f, GraveBackMinutes = 10f;
        internal static int DeathEarlyLevels = 2;
        private const string GraveBackKey = "RavenWatch_graveBack"; // "<world time of the death>|<points lost>"

        internal static void OnDeath()
        {
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Worn(munin);
                if (item == null) continue;
                float loss = (Level(item) <= DeathEarlyLevels ? DeathLossEarly : DeathLoss) * GearFx.DeathMul(item); // (Захисний ошейник: half)
                float lost = Lose(item, loss);
                if (lost > 0f) Write(item, GraveBackKey, WorldNow.ToString("R", CultureInfo.InvariantCulture) + "|" + lost.ToString("0.##", CultureInfo.InvariantCulture));
                else if (item.m_customData != null) item.m_customData.Remove(GraveBackKey);
            }
        }

        // the raven is back on your shoulder from your grave: in time, it gives back a share of what the death took
        internal static void BackFromGrave(Player p, ItemDrop.ItemData item, bool munin)
        {
            string v = Read(item, GraveBackKey);
            if (v == null) return;
            item.m_customData.Remove(GraveBackKey); // once, in time or not
            if (p == null || !Active(item) || Stranger(item, p)) return;
            string[] parts = v.Split('|');
            double at; float lost;
            if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out at)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lost)) return;
            double since = WorldNow - at;
            if (since < 0.0 || since > GraveBackMinutes * 60.0) return; // too late (or a clock from another world)
            Add(item, munin, lost * GraveBackShare, false, Lang.T("вчасно забрали з могили"));
        }

        internal static void OnTakenOff(ItemDrop.ItemData item) { if (item != null) Lose(item, Need(item) * TakeOffLoss); }

        // a paid new name (Nicks.Choose): the points it takes, worked out there so the book shows the very same number
        internal static void OnRenamed(ItemDrop.ItemData item, int points) { if (item != null && points > 0) Lose(item, points); }

        // "raven ready": the worn ravens are hungry enough to eat (testing)
        internal static void ResetFeeding()
        {
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Worn(munin);
                if (item != null) Satiety.Set(item, 30f);
            }
        }

        // ---------------------------------------------------------------- the shy raven (level 1)

        private static double WorldNow { get { return WorldTime.Now; } }

        // seconds until it is back; 0 when it sits on the shoulder
        internal static float AwayLeft(ItemDrop.ItemData item)
        {
            double at;
            if (item == null || !double.TryParse(Read(item, AwayKey), NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return 0f;
            return Mathf.Max(0f, WorldTime.Sane(at - WorldNow));
        }

        // off for this many seconds, for this reason (the nest: "errand"); back, Wander tells it (and Nest.Back)
        internal static void SendAway(ItemDrop.ItemData item, float seconds, string why)
        {
            if (item == null) return;
            Write(item, AwayKey, (WorldNow + Mathf.Max(1f, seconds)).ToString("R", CultureInfo.InvariantCulture));
            Write(item, AwayWhyKey, why);
        }

        internal static string AwayWhy(ItemDrop.ItemData item) { return item != null ? Read(item, AwayWhyKey) : ""; }

        // where it is while away, in a word or two: "у Одіна", "несе лист", "на жердині", "відлетів" — for every status line
        internal static string AwayWord(bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return Lang.T("відлетів");
            if (Perches.On(item) && AwayLeft(item) <= 0f) return Perches.OnSpot(item) ? (WorldVisit.Active(munin) ? Lang.T("роздивляється") : Lang.T("сидить окремо")) : Perches.OnWord(item);
            string why = Read(item, AwayWhyKey);
            if (why == "ritual" || why == Nest.Why) return Lang.T("у Одіна");
            if (why == Post.Why) return Post.AwayWord(Player.m_localPlayer, munin);
            return Lang.T("відлетів");
        }

        // flown off for a while, or sitting on a perch (Perches) until you call it back
        internal static bool Away(ItemDrop.ItemData item) { return AwayLeft(item) > 0f || Perches.On(item); }

        // "повернеться за 4 хв" or, on a perch, "сидить на жердині"
        internal static string BackIn(ItemDrop.ItemData item)
        {
            return Perches.On(item) && AwayLeft(item) <= 0f ? (Perches.OnSpot(item) ? Lang.T("сидить не на тобі") : Perches.InNest(item) ? Lang.T("сидить у гнізді") : Lang.T("сидить на жердині")) : Lang.F("повернеться за {0}", RavenAbilities.Span(AwayLeft(item)));
        }

        // "Хугін відлетів — нема кого годувати. Повернеться за 4 хв 10 с."
        internal static string AwayAction(bool munin, ItemDrop.ItemData item, string what)
        {
            if (Perches.On(item) && AwayLeft(item) <= 0f) return Perches.Text(munin, item) + ": " + what + ".\n" + Perches.CallHint(item);
            if (Read(item, AwayWhyKey) == Post.Why) return Lang.F("{0} {1}: {2}.", Plugin.RavenName(munin), AwayWord(munin, item), what); // (how long: as the post goes)
            return Lang.F("{0} {1}: {2}.\nПовернеться за {3}.", Plugin.RavenName(munin), AwayWord(munin, item), what, RavenAbilities.Span(AwayLeft(item)));
        }

        // the level it has settled at: after a ritual the new one counts once it is back from Odin
        internal static int SettledLevel(ItemDrop.ItemData item)
        {
            if (item == null) return 0;
            int level = Level(item);
            bool trip = !string.IsNullOrEmpty(Read(item, LeaveKey)) || Read(item, AwayWhyKey) == "ritual";
            return trip ? Mathf.Max(0, level - 1) : level;
        }

        // the level the raven looks like: right after a ritual it is still the old size until it flies off
        internal static int ShownLevel(bool munin, ItemDrop.ItemData item)
        {
            int level = Level(item);
            return !string.IsNullOrEmpty(Read(item, LeaveKey)) ? Mathf.Max(0, level - 1) : level;
        }

        // after a ritual each raven goes to Odin in its own way
        private static string RitualLeaveText(bool munin)
        {
            return munin ? Lang.T("Мунін полетів до Одіна хизуватися.") : Lang.T("Хугін полетів до Одіна з новиною.");
        }

        internal static string AwayText(bool munin, ItemDrop.ItemData item)
        {
            if (Perches.On(item) && AwayLeft(item) <= 0f) return Perches.Text(munin, item) + ".\n" + Perches.CallHint(item);
            if (Read(item, AwayWhyKey) == "ritual")
                return Lang.F("{0} у Одіна, ще {1}.", Plugin.RavenName(munin), RavenAbilities.Span(AwayLeft(item)));
            if (Read(item, AwayWhyKey) == Nest.Why)
                return Lang.F("{0} полетів до Одіна по доручення. Повернеться за {1}.", Plugin.RavenName(munin), RavenAbilities.Span(AwayLeft(item)));
            if (Read(item, AwayWhyKey) == Post.Why) return Post.AwayText(Player.m_localPlayer, munin, item); // (how long: as the post goes)
            string went = Level(item) <= 1
                ? (munin ? Lang.T("Мунін ще тебе не терпить і відлетів.") : Lang.T("Хугін ще тебе соромиться й відлетів."))
                : (munin ? Lang.T("Мунін полетів кудись у своїх справах.") : Lang.T("Хугін полетів провітритися."));
            return Lang.F("{0}\nПовернеться за {1}.", went, RavenAbilities.Span(AwayLeft(item)));
        }

        // now and then a level-1 raven takes off for a few minutes; it is told when it goes and when it is back
        // All of it is kept in the raven itself, not in the game session: taken off while away, it is still away when
        // put on again; another raven of the same kind is not mixed up with it; a ritual's flight-off waits for it too.
        // a raven on the shoulder takes off only under the sky and in peace: not in a dungeon, a cave or a house,
        // not in a fight, not mid-portal. One on a perch is not with you: it may go whatever you are doing.
        internal static float FreeRetrySeconds = 60f;
        private static bool FreeToLeave(Player p, bool perched)
        {
            if (perched) return true;
            if (p == null || !Senses.Ready || p.IsDead() || p.IsTeleporting()) return false; // (right after login nothing is known yet)
            if (Senses.Underground || Senses.Indoors || Senses.Fight) return false; // (gathered twice a second: no roof rays of our own)
            return EnvMan.instance == null || !EnvMan.instance.IsEnvironment("Caves"); // a cave in the world, under its dark weather
        }

        private static void Wander(Player p, bool munin, ItemDrop.ItemData item)
        {
            bool away = Away(item);
            bool perched = Perches.On(item);  // on a perch it is not "away" from this: a shy one flies off from there too
            bool gone = AwayLeft(item) > 0f;  // really flown off (to Odin, or shy)
            bool free = FreeToLeave(p, perched);
            double leave;
            bool leaving = double.TryParse(Read(item, LeaveKey), NumberStyles.Float, CultureInfo.InvariantCulture, out leave);
            if (leaving && !gone && free && (WorldNow >= leave || leave - WorldNow > WorldTime.MaxAhead)) // (to Odin: it waits for the sky)
            {
                item.m_customData.Remove(LeaveKey);
                Write(item, AwayKey, (WorldNow + Mathf.Max(1f, Level(item) <= 1 ? WakeAwaySeconds : RitualAwaySeconds)).ToString("R", CultureInfo.InvariantCulture));
                Write(item, AwayWhyKey, "ritual");
                Outfits.OdinGift(munin, item); // comes back wearing it
                if (perched) Perches.ToOdin(p, item, munin, RitualLeaveText(munin)); // straight off the perch
                else if (p != null) RavenMsg.Center(p, RitualLeaveText(munin), 2);
                return;
            }
            string why = Read(item, AwayWhyKey);
            if (!away && !string.IsNullOrEmpty(why))
            {
                // back (seen as soon as it is on the shoulder again, if it came back while in the bag)
                item.m_customData.Remove(AwayWhyKey);
                string gift = why == "ritual" ? Outfits.TakeNewGift(item) : null;
                if (why == "ritual") RavenAura.BackFromOdin(p, munin); // a ringing caw, heard by everyone nearby
                if (p != null && why == "ritual")
                {
                    int lv = Level(item);
                    if (lv == 1) Guide.Queue(p, Guide.Woke, 20f);
                }
                if (why == Nest.Why) Nest.Back(p, munin, item); // with Odin's errand
                else if (why == Post.Why) Post.Back(p, munin, item); // from the post: handed over or not
                else if (p != null) RavenMsg.Center(p, why == "ritual"
                    ? Plugin.RavenName(munin) + (Level(item) > 1 ? Lang.T(" повернувся і підріс.") : Lang.T(" повернувся.")) + (gift != null ? Lang.F("\nОдін передав йому: {0}.", gift) : "")
                    : Lang.F("{0} повернувся на плече.", Plugin.RavenName(munin)));
            }
            int level = Level(item);
            if (gone || leaving || level < 1 || level > WanderChance.Length) return; // (sitting on a perch: it may still fly off)
            int i = level - 1;
            double now = WorldNow, check;
            if (!double.TryParse(Read(item, WanderKey), NumberStyles.Float, CultureInfo.InvariantCulture, out check)) check = 0;
            if (now < check && check - now <= WorldTime.MaxAhead) return;
            if (check > 0 && !free) { Write(item, WanderKey, (now + FreeRetrySeconds).ToString("R", CultureInfo.InvariantCulture)); return; } // looks again a minute after you are out
            Write(item, WanderKey, (now + WanderCheckMinutes * 60f).ToString("R", CultureInfo.InvariantCulture));
            if (check <= 0 || UnityEngine.Random.value >= WanderChance[i]) return; // the first look only starts the clock
            float minutes = UnityEngine.Random.Range(Mathf.Min(WanderMinMinutes[i], WanderMaxMinutes[i]), Mathf.Max(WanderMinMinutes[i], WanderMaxMinutes[i]));
            Write(item, AwayKey, (now + minutes * 60f).ToString("R", CultureInfo.InvariantCulture));
            Write(item, AwayWhyKey, "shy");
            string went = level == 1
                ? (munin ? Lang.T("Мунін ще тебе не терпить і відлетів.") : Lang.T("Хугін ще тебе соромиться й відлетів."))
                : (munin ? Lang.T("Мунін полетів кудись у своїх справах.") : Lang.T("Хугін полетів провітритися."));
            if (perched) Perches.ToOdin(p, item, munin, Lang.F("{0}\nЗгодом повернеться на плече.", went)); // off the perch it sat on
            else if (p != null) RavenMsg.Center(p, Lang.F("{0}\nЗгодом повернеться.", went), 3); // ~16 s on screen
        }

        internal static void ResetAway()
        {
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Worn(munin);
                if (item != null && item.m_customData != null) { item.m_customData.Remove(AwayKey); item.m_customData.Remove(AwayWhyKey); item.m_customData.Remove(LeaveKey); }
            }
        }

        // twice a second, for each worn raven
        internal static void Tick(Player p)
        {
            RavenPouch.Guard(); // the pouch window shuts once its raven or the pouch is gone
            foreach (bool munin in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Worn(munin);
                if (item == null) { NetFlag.Set(p, munin ? StarveKeyM : StarveKeyH, false); StarveItem[munin ? 1 : 0] = null; continue; }
                if (Active(item) && string.IsNullOrEmpty(Read(item, OwnerKey))) Bind(item, p); // awake from before owners: it takes its wearer
                Wander(p, munin, item);
                Peck(p, munin, item);
                RavenAura.Hungry(p, munin, item);
                Starve(p, munin, item);
                Outfits.SeenStart(item); // the book's "new outfit" marks start from the raven's first moment on you
                RavenPouch.Tick(p, munin, item); // hungry: it eats from its own pouch first
                Satiety.Tick(p, munin, item); // hunger melts, starving melts trust, a hungry raven steals
            }
        }
    }

    // The ritual heard and seen: the offering on you with the "level up" sound and the raven's caw (for everyone
    // nearby), and on the stone of that boss its own glow (the game's look for a trophy put on the stone).
    // A stream of motes from the head on the stone to the wearer's shoulder, following the wearer, then gone.
    internal class RitualStream : MonoBehaviour
    {
        internal static float Seconds = 1.4f;
        private Vector3 _from;
        private Player _to;
        private bool _munin;
        private float _t;
        private ParticleSystem _ps;

        internal static void Fly(Vector3 from, Player to, bool munin)
        {
            if (to == null) return;
            try
            {
                GameObject g = new GameObject("RavenWatch_RitualStream");
                g.transform.position = from;
                RitualStream rs = g.AddComponent<RitualStream>();
                rs._from = from; rs._to = to; rs._munin = munin;
                rs.Build();
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ritual stream failed: " + e.Message); }
        }

        private void Build()
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // a trail behind the head of the stream
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 200;
            main.startColor = _munin
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.2f, 1f), new Color(0.75f, 0.08f, 0.05f, 1f))
                : new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.9f, 1f, 1f), new Color(0.35f, 0.55f, 0.95f, 1f));
            ParticleSystem.EmissionModule em = _ps.emission;
            em.rateOverTime = 40f;
            em.rateOverDistance = 12f;
            ParticleSystem.ShapeModule sh = _ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f;
            Gradient fade = new Gradient();
            fade.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);
            Material m = RavenAura.Dot();
            if (m != null) GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            _ps.Play();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Seconds);
            if (_to != null && k < 1f)
            {
                Vector3 target = _to.transform.position + Vector3.up * 1.6f; // the shoulder
                float e = k * k * (3f - 2f * k);
                transform.position = Vector3.Lerp(_from, target, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f; // an arc over the ground
                return;
            }
            if (_ps != null && _ps.isEmitting) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_t > Seconds + 1.5f) Destroy(gameObject);
        }
    }

    internal static class RitualFx
    {
        private const string Rpc = "RavenWatch_RitualFx";
        private static readonly Dictionary<string, string> StoneGlow = new Dictionary<string, string>
        {
            { "TrophyEikthyr", "fx_bossstone_activate_Eikthyr" }, { "TrophyTheElder", "fx_bossstone_activate_TheElder" },
            { "TrophyBonemass", "fx_bossstone_activate_Bonemass" }, { "TrophyDragonQueen", "fx_bossstone_activate_DragonQueen" },
            { "TrophyGoblinKing", "fx_bossstone_activate_Yagluth" },
        };

        private const string RpcStone = "RavenWatch_RitualStone";

        internal static void Register()
        {
            RavenFx.Register(Rpc, Spawn);
            ZRoutedRpc.instance.Register<ZPackage>(RpcStone, new Action<long, ZPackage>(OnStone));
        }

        // the offering on you, and the head on the stone answering: here and on every game nearby
        internal static void Play(Player p, bool munin, string trophy)
        {
            RavenFx.Broadcast(Rpc, p, Spawn);
            Stone(p, munin, trophy);
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetPlayerName());
            pkg.Write(p.transform.position);
            pkg.Write(munin);
            pkg.Write(trophy ?? "");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcStone, pkg);
        }

        private static void OnStone(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return;
            string name = pkg.ReadString();
            Vector3 pos = pkg.ReadVector3();
            bool munin = pkg.ReadBool();
            string trophy = pkg.ReadString();
            Player me = Player.m_localPlayer;
            if (me == null || Vector3.Distance(me.transform.position, pos) > 100f) return;
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != me && other.GetPlayerName() == name) { Stone(other, munin, trophy); return; }
        }

        private static void Spawn(Player p)
        {
            Vector3 at = p.transform.position + Vector3.up * 1.2f;
            RavenFx.Play("vfx_offering", at, p.transform.rotation);
            RavenFx.Play("sfx_offering", at, p.transform.rotation);
            RavenFx.Play("sfx_levelup", at, p.transform.rotation);
            RavenFx.Play("vfx_skilllevelup", p.transform.position, p.transform.rotation);
            RavenFx.Play("sfx_raven_kaw", at, p.transform.rotation);
        }

        // The head on the stone answers: its boss's own glow and sound at the head itself, and its power flows from
        // the head to the raven on your shoulder.
        internal static void Stone(Player p, bool munin, string trophy)
        {
            BossStone s = Trust.StoneFor(p, string.IsNullOrEmpty(trophy) ? null : trophy);
            if (s == null) return;
            Vector3 head = s.m_itemStand != null ? s.m_itemStand.transform.position : s.transform.position + Vector3.up;
            string glow;
            if (string.IsNullOrEmpty(trophy) || !StoneGlow.TryGetValue(trophy, out glow) || !RavenFx.Play(glow, head, s.transform.rotation))
                RavenFx.Play("fx_bossstone_attach", head, s.transform.rotation);
            RavenFx.Play("sfx_bossstone_attach_done", head, s.transform.rotation);
            RitualStream.Fly(head, p, munin);
        }
    }
}
#endif

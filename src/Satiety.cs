#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Satiety: how full the raven is, 0–100, kept in the raven itself (with the world time it was last counted, so
    // it goes on melting in the bag, in a chest or with the game off, at most a few days' worth at once).
    //   - It melts by itself (PerDay a game day) and with every gift used (its strength cost × GiftShare).
    //   - Zones: 0 starving (the old «голодує»: droops, strike weaker, pecks, trust melts), up to 15 hungry (gifts
    //     dearer, no strength comes, steals from the bag), up to 40 asks for food, up to 90 fed (strength comes
    //     faster), above that stuffed (lazy: no outings, strike weaker).
    //   - Food fills by its class (where it comes from, 1 Meadows .. 5 Ashlands); trust from food only for the
    //     part of it that fills a real hunger (below 40).
    //   - Every raven has its own favourite food, rolled once and hidden until it has eaten it or let slip enough hints.
    internal static class Satiety
    {
        private const string Key = "RavenWatch_sat", AtKey = "RavenWatch_satAt";
        private const string FavKey = "RavenWatch_fav", FavKnownKey = "RavenWatch_favKnown", FavHintKey = "RavenWatch_favHints";
        private const string OldFedKey = "RavenWatch_fedDay"; // the once-a-day feeding before satiety

        internal static float PerDay = 25f;          // melts by itself in a game day
        internal static float GiftShare = 0.25f;     // a gift's strength cost × this comes off satiety
        internal const float HungryAt = 15f, AskAt = 40f, StuffedAt = 90f;
        internal static float HungryCostMul = 1.5f, StarveCostMul = 2f;
        internal static float FedGainMul = 1.25f;   // strength comes this much faster when fed (41–90)
        internal static float StuffedStrikePct = 25f;
        internal static float FavFillMul = 1.5f, FavTrustMul = 2f, FavFedMul = 2f;
        internal static float StealMinutes = 5f, CoinChance = 25f;
        internal static int CoinTake = 100;
        internal static float CorpseFill = 8f, CorpseChance = 50f;
        internal static float StartValue = 60f;
        private const float MaxDays = 3f; // most game days counted at once (a raven from another world, a long break)

        // zones, from starving to stuffed
        internal const int Starving = 0, Hungry = 1, Empty = 2, Fed = 3, Stuffed = 4;

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

        private static float Rate(ItemDrop.ItemData item) { return PerDay * HungerMul(item) / Power.GameDaySeconds(); } // per second

        // how fast it gets hungry: knack «Невибагливий» slower, a starred rune of Фрея slower («Іній») or faster («Кров»)
        internal static float HungerMul(ItemDrop.ItemData item) { return Norov.SatietyMul(item) * RuneStars.HungerMul(item); }

        // only an awake raven on its owner melts: a sleeping one, one on a stranger, or one flown off (to Odin after a
        // ritual, or a shy one) keeps what it has; one resting on a perch gets hungry as usual (it is not a way to stop it)
        private static bool Draining(ItemDrop.ItemData item)
        {
            return item != null && Trust.Active(item) && !Trust.Stranger(item, Player.m_localPlayer) && (!Trust.Away(item) || Perches.On(item));
        }

        private static bool Stored(ItemDrop.ItemData item, out float v, out double at)
        {
            at = 0;
            return float.TryParse(Read(item, Key), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                & double.TryParse(Read(item, AtKey), NumberStyles.Float, CultureInfo.InvariantCulture, out at);
        }

        // seconds since it was last counted; another world's clock (ahead or far behind) counts as nothing / a few days
        private static float Since(double at)
        {
            double d = WorldTime.Now - at;
            if (d <= 0) return 0f;
            return (float)Math.Min(d, MaxDays * Power.GameDaySeconds());
        }

        // a raven from before satiety: fed today is well fed, else hungry-ish
        private static float Initial(ItemDrop.ItemData item)
        {
            int d;
            if (!int.TryParse(Read(item, OldFedKey), out d)) return StartValue;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            return d >= day ? 70f : 30f;
        }

        private static void Store(ItemDrop.ItemData item, float v)
        {
            Write(item, Key, Mathf.Clamp(v, 0f, 100f).ToString("0.####", CultureInfo.InvariantCulture));
            Write(item, AtKey, WorldTime.Now.ToString("R", CultureInfo.InvariantCulture));
        }

        // how full it is now
        internal static float Value(ItemDrop.ItemData item)
        {
            if (item == null) return StartValue;
            float v; double at;
            if (!Stored(item, out v, out at)) return Initial(item);
            if (!Draining(item)) return Mathf.Clamp(v, 0f, 100f);
            return Mathf.Clamp(v - Rate(item) * Since(at), 0f, 100f);
        }

        // counted and written down; the seconds it spent at nothing since the last count (trust melts for those)
        private static float Sync(ItemDrop.ItemData item)
        {
            float v; double at;
            if (!Stored(item, out v, out at)) { Store(item, Initial(item)); return 0f; }
            if (!Draining(item)) { Store(item, v); return 0f; }
            float since = Since(at), rate = Mathf.Max(1e-6f, Rate(item));
            float raw = v - rate * since;
            Store(item, raw);
            return raw < 0f ? Mathf.Min(since, -raw / rate) : 0f;
        }

        internal static void Change(ItemDrop.ItemData item, float by)
        {
            if (item == null) return;
            Sync(item);
            Store(item, Value(item) + by);
            _zoneFrame[0] = _zoneFrame[1] = -1; // the zone may have changed this frame
        }

        internal static void Set(ItemDrop.ItemData item, float v) { if (item != null) { Sync(item); Store(item, v); } }

        // a raven brought back from its copy: the copy's satiety and clock are old, it comes back fed as a new one
        internal static void Fresh(ItemDrop.ItemData item) { if (item != null) Store(item, StartValue); }

        // minutes until a stuffed raven is merely fed again
        internal static float StuffedMinutes(ItemDrop.ItemData item)
        {
            float over = Value(item) - StuffedAt;
            return over <= 0f || PerDay <= 0f ? 0f : over / Rate(item) / 60f;
        }

        internal static int Zone(float v) { return v <= 0f ? Starving : v <= HungryAt ? Hungry : v <= AskAt ? Empty : v <= StuffedAt ? Fed : Stuffed; }
        internal static int Zone(ItemDrop.ItemData item) { return Zone(Value(item)); }

        // the zone that counts: only an awake raven of yours; anything else is just "fed".
        // Asked for every gift's cost every frame while the menu or the book is open: worked out once a frame.
        private static readonly int[] _zoneFrame = { -1, -1 }, _zoneWorn = { Fed, Fed };

        private static int ZoneWorn(bool munin)
        {
            int i = munin ? 1 : 0;
            if (_zoneFrame[i] == Time.frameCount) return _zoneWorn[i];
            ItemDrop.ItemData item = Trust.Worn(munin);
            _zoneWorn[i] = Draining(item) ? Zone(item) : Fed;
            _zoneFrame[i] = Time.frameCount;
            return _zoneWorn[i];
        }

        internal static bool IsStarving(ItemDrop.ItemData item) { return Draining(item) && Value(item) <= 0f; }
        internal static bool IsStuffed(ItemDrop.ItemData item) { return Draining(item) && Value(item) > StuffedAt; }
        internal static bool StuffedWorn(bool munin) { return ZoneWorn(munin) == Stuffed; }

        internal static string[] ZoneWords { get { return Lang.Arr(ZoneWordsSrc); } } // (translated on every read)
        private static readonly string[] ZoneWordsSrc = Lang.Keys("голодує", "голодний", "просить їсти", "ситий", "об'ївся");
        internal static readonly string[] ZoneHex = { "#FF5A5A", "#E8843C", "#FFD54F", "#9FD49F", "#B48CE6" };

        internal static string ZoneWord(ItemDrop.ItemData item) { return ZoneWords[Zone(item)]; }
        internal static string ZoneHexOf(ItemDrop.ItemData item) { return ZoneHex[Zone(item)]; }

        // ---------------------------------------------------------------- what it does

        // gifts cost more strength when it is hungry
        internal static float CostMul(bool munin)
        {
            int z = ZoneWorn(munin);
            return z == Starving ? StarveCostMul : z == Hungry ? HungryCostMul : 1f;
        }

        // " (×1.5 — голодний)" by a cost, or nothing
        internal static string CostNote(bool munin)
        {
            int z = ZoneWorn(munin);
            if (z == Starving) return Lang.F(" <color=#FF5A5A>(×{0} — голодує)</color>", Num(StarveCostMul));
            if (z == Hungry) return Lang.F(" <color=#E8843C>(×{0} — голодний)</color>", Num(HungryCostMul));
            return "";
        }

        // strength gained: none when hungry, faster when fed
        internal static float GainMul(bool munin)
        {
            int z = ZoneWorn(munin);
            return z <= Hungry ? 0f : z == Fed ? FedGainMul : 1f;
        }

        // flying about on your word (Orders: the play costs food, not strength): this much, less for an undemanding one
        internal static void Flew(bool munin, float amount)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (Draining(item) && amount > 0f) Change(item, -amount * HungerMul(item));
        }

        // a gift used: the raven gets hungrier (by its plain cost, not the dearer hungry one)
        internal static void UsedGift(bool munin, float cost)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (Draining(item) && cost > 0f) Change(item, -cost * GiftShare);
        }

        internal static string Num(float v) { return Lang.Num(v, "0.##"); }

        // ---------------------------------------------------------------- food

        // classes: where the food comes from, 1 Meadows .. 5 Ashlands and the far sea
        private static readonly Dictionary<string, int> Classes = new Dictionary<string, int>
        {
            { "Raspberry", 1 }, { "Honey", 1 }, { "Mushroom", 1 }, { "Fish1", 1 },
            { "Blueberries", 2 }, { "MushroomYellow", 2 }, { "Fish2", 2 },
            { "MushroomBlue", 3 }, { "Fish5", 3 }, { "Fish4_cave", 3 }, { "Fish6", 3 },
            { "Cloudberry", 4 }, { "MushroomJotunPuffs", 4 }, { "MushroomMagecap", 4 }, { "Fish7", 4 }, { "Fish8", 4 }, { "Fish9", 4 },
            { "Fish10", 5 }, { "Fish11", 5 }, { "Fish12", 5 },
            { "RawMeat", 1 }, { "DeerMeat", 1 }, { "NeckTail", 1 },
            { "WolfMeat", 3 }, { "SerpentMeat", 3 },
            { "LoxMeat", 4 }, { "HareMeat", 4 }, { "ChickenMeat", 4 }, { "BugMeat", 4 },
            { "AsksvinMeat", 5 }, { "VoltureMeat", 5 }, { "BoneMawSerpentMeat", 5 },
        };

        // a food's class: from the config list (Item:points:class), else the table, else from its points
        internal static int ClassOf(string prefab, int points, int given)
        {
            if (given >= 1) return Mathf.Clamp(given, 1, 5);
            int c;
            if (prefab != null && Classes.TryGetValue(prefab, out c)) return c;
            return points <= 6 ? 1 : points <= 9 ? 2 : points <= 11 ? 3 : points <= 14 ? 4 : 5;
        }

        internal static float FillOfClass(int c) { return 10f + 5f * Mathf.Clamp(c, 1, 5); } // 15 .. 35

        internal static float FillOf(Trust.Food f, bool fav) { return f == null ? 0f : f.Fill * (fav ? FavFillMul : 1f); }

        // trust from this food now: only for the part of it that fills a real hunger (below AskAt); a full bar takes none
        internal static float TrustOf(ItemDrop.ItemData item, Trust.Food f, bool fav)
        {
            if (item == null || f == null || Trust.Need(item) <= 0f || Trust.Full(item)) return 0f;
            float before = Value(item), fill = FillOf(f, fav);
            float under = Mathf.Clamp(Mathf.Min(before + fill, AskAt) - before, 0f, fill);
            return Mathf.Min(f.Points * under / AskAt * (fav ? FavTrustMul : 1f), Trust.FoodLeftToday(item)); // the day's limit from food
        }

        // the best food in the bag for it now: one that does not stuff it, the most trust, then the most filling;
        // when everything would stuff it, the lightest
        internal static Trust.Food Best(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (p == null) return null;
            Trust.Food best = null;
            float bestScore = float.MinValue;
            float before = Value(item);
            foreach (Trust.Food f in Trust.EdibleFoods(munin, item))
            {
                if (Trust.Have(p, f.Prefab) <= 0) continue;
                bool fav = KnownFav(item, f.Prefab);
                float fill = FillOf(f, fav);
                bool fits = before + fill <= StuffedAt + 0.01f;
                float score = (fits ? 100000f : 0f) + TrustOf(item, f, fav) * 100f + (fits ? fill : -fill);
                if (score > bestScore) { bestScore = score; best = f; }
            }
            return best;
        }

        // "ситість 30 → 55 · довіра +6" for a food: what feeding it would do (a hidden favourite is not given away)
        internal static string Preview(ItemDrop.ItemData item, Trust.Food f)
        {
            if (item == null || f == null) return "";
            bool fav = KnownFav(item, f.Prefab);
            float before = Value(item), after = Mathf.Min(100f, before + FillOf(f, fav));
            float t = TrustOf(item, f, fav);
            return Lang.F("ситість {0} → {1}{2}", Mathf.RoundToInt(before), Mathf.RoundToInt(after), (t >= 0.05f ? Lang.F(" · довіра +{0}", Lang.Num(t, "0.#")) : ""));
        }

        // ---------------------------------------------------------------- the favourite

        internal static string FavOf(ItemDrop.ItemData item) { return Read(item, FavKey); }
        internal static bool IsFav(ItemDrop.ItemData item, string prefab) { string f = FavOf(item); return !string.IsNullOrEmpty(f) && f == prefab; }
        internal static bool FavKnown(ItemDrop.ItemData item) { return Read(item, FavKnownKey) == "1"; }
        internal static bool KnownFav(ItemDrop.ItemData item, string prefab) { return FavKnown(item) && IsFav(item, prefab); }
        internal static void Reveal(ItemDrop.ItemData item) { if (item != null) Write(item, FavKnownKey, "1"); }

        // rolled once, the first time it sits on you awake (the Fate stone and a return keep it)
        private static void RollFav(ItemDrop.ItemData item, bool munin)
        {
            if (item == null || ObjectDB.instance == null) return;
            List<Trust.Food> foods = Trust.EdibleFoods(munin, item);
            if (foods.Count == 0) return;
            string current = FavOf(item);
            if (!string.IsNullOrEmpty(current))
            {
                foreach (Trust.Food food in foods) if (food.Prefab == current) return;
                Write(item, FavKnownKey, "0");
                Write(item, FavHintKey, "0");
            }
            Write(item, FavKey, foods[UnityEngine.Random.Range(0, foods.Count)].Prefab);
        }

        internal static Trust.Food FavFood(ItemDrop.ItemData item, bool munin)
        {
            RollFav(item, munin);
            string f = FavOf(item);
            if (string.IsNullOrEmpty(f)) return null;
            foreach (Trust.Food x in Trust.EdibleFoods(munin, item)) if (x.Prefab == f) return x;
            return null;
        }

        // "Улюблена: Хмарка" or "Улюблена: ???" (with the hints let slip so far)
        internal static string FavLine(ItemDrop.ItemData item, bool munin)
        {
            Trust.Food f = FavFood(item, munin);
            if (f == null) return "";
            if (FavKnown(item)) return Lang.F("Улюблена їжа: <color=#FFD54F>★ {0}</color>", f.Name);
            int hints = Hints(item);
            List<string> known = new List<string>();
            if (!munin && hints >= 1) known.Add(Lang.T(KindOf(f.Prefab)));
            if (hints >= (munin ? 1 : 2)) known.Add(ClassWords[Mathf.Clamp(f.Class, 1, 5)]);
            if (hints >= (munin ? 2 : 3)) known.Add(Lang.F("на «{0}»", First(f.Name)));
            return Lang.F("Улюблена їжа: <color=#FFD54F>???</color>{0}", (known.Count > 0 ? " <color=#9a9a9a>(" + string.Join(", ", known.ToArray()) + ")</color>" : ""));
        }

        private static int Hints(ItemDrop.ItemData item) { int n; return int.TryParse(Read(item, FavHintKey), out n) ? n : 0; }

        private static string[] ClassWords { get { return Lang.Arr(ClassWordsSrc); } } // (translated on every read)
        private static readonly string[] ClassWordsSrc = Lang.Keys("", "щось просте, з початку шляху", "щось із темного лісу", "щось із холодних чи мокрих країв", "щось із далеких земель", "щось із самого краю світу");

        private static string KindOf(string prefab)
        {
            if (prefab == null) return "щось";
            if (prefab.StartsWith("Fish")) return "рибне";
            if (prefab.StartsWith("Mushroom")) return "грибне";
            if (prefab == "Honey") return "солодке";
            if (prefab.EndsWith("berry") || prefab.EndsWith("berries") || prefab.EndsWith("Berries")) return "ягідне";
            return "м'ясне";
        }

        private static string First(string name) { return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(); }

        // a line from the phrase files (patterns/quips, by trust tone) with these values; null when none fits
        internal static string Say(Player p, bool munin, string tag, params string[] values)
        {
            if (Plugin.Instance == null) return null;
            Composer.Extra.Clear();
            for (int i = 0; i + 1 < values.Length; i += 2) Composer.Extra[values[i]] = values[i + 1];
            try { return Plugin.Instance.ComposeTagged(p, munin, tag); }
            finally { Composer.Extra.Clear(); }
        }

        // "100 монет", "21 монету", "3 монети"
        private static string Coins(int n) { return Lang.N(n, "монету|монети|монет"); }

        // a hint about its favourite, in its own voice; null when it has nothing more to give away
        internal static string Hint(Player p, ItemDrop.ItemData item, bool munin)
        {
            Trust.Food f = FavFood(item, munin);
            if (f == null || FavKnown(item)) return null;
            int n = Hints(item);
            int max = munin ? 2 : 3;
            if (n >= max) n = max - 1; // all said: repeats the last one
            else Write(item, FavHintKey, (n + 1).ToString());
            string cls = ClassWords[Mathf.Clamp(f.Class, 1, 5)];
            string tag = munin ? (n == 0 ? "fav_hint_place" : "fav_hint_letter") : (n == 0 ? "fav_hint_kind" : n == 1 ? "fav_hint_place" : "fav_hint_letter");
            string made = Say(p, munin, tag, "kind", Gen(KindOf(f.Prefab)), "place", cls, "letter", First(f.Name));
            if (!string.IsNullOrEmpty(made)) return made;
            if (!munin)
            {
                if (n == 0) return Pick(Lang.F("Знаєш, чого мені хочеться? Чогось {0}. Це підказка, якщо що.", Gen(KindOf(f.Prefab))),
                                        Lang.F("Не те щоб я натякав, але зараз би щось {0}. Отак.", Lang.T(KindOf(f.Prefab))));
                if (n == 1) return Pick(Lang.F("Моя улюблена їжа — {0}. Більше не скажу. Ну, може, згодом.", cls),
                                        Lang.F("Підказка друга: шукай {0}. Я ж не можу все за тебе.", cls));
                return Pick(Lang.F("Гаразд, востаннє: назва на «{0}». Далі сам{{|а}}.", First(f.Name)),
                            Lang.F("Усе, здаюся: на «{0}». Принесеш — побачиш, яким я буваю вдячним.", First(f.Name)));
            }
            if (n == 0) return Pick(Lang.F("Є одне м'ясо, за яке я б тебе навіть похвалив. Шукай {0}.", cls),
                                    Lang.F("Хочеш знати, що я люблю? М'ясо. А саме — {0}. Шукай, ледащо.", cls));
            return Pick(Lang.F("Добре, тупице, підкажу: на «{0}». Тепер навіть ти знайдеш.", First(f.Name)),
                        Lang.F("На «{0}». Принесеш — я, може, навіть не клюну тебе тиждень.", First(f.Name)));
        }

        private static string Gen(string kind)
        {
            switch (kind) { case "рибне": return Lang.T("рибного"); case "грибне": return Lang.T("грибного"); case "солодке": return Lang.T("солодкого"); case "ягідне": return Lang.T("ягідного"); default: return Lang.T("м'ясного"); }
        }

        private static string Pick(string a, string b) { return UnityEngine.Random.value < 0.5f ? a : b; }

        // what it says on eating its favourite: the first time (now you know) and after
        internal static string FavLineSaid(Player p, bool munin, bool first, string food)
        {
            string made = Say(p, munin, first ? "fav_first" : "fav_again", "food", food);
            if (!string.IsNullOrEmpty(made)) return made;
            if (first) return munin
                ? Pick(Lang.T("О! ОЦЕ інша справа. Запам'ятай, що ти мені да{в|ла}, бо вдруге підказувати не буду."), Lang.T("Нарешті щось путнє! Ось це я люблю. Не кажи нікому, що я радів."))
                : Pick(Lang.T("Це ж моя улюблена! Ти вгада{в|ла}... чи підслуха{в|ла}? Байдуже. Дякую!"), Lang.T("Ох. Оце смак. Тепер ти знаєш мою таємницю — бережи її."));
            return munin
                ? Pick(Lang.T("Моє улюблене. Цього разу не клюну. Мабуть."), Lang.T("Ось це я поважаю. Давай ще колись."))
                : Pick(Lang.T("Улюблене! Ти мене розбещуєш. Продовжуй."), Lang.T("Знову моє улюблене. Ти найкращ{ий|а} з людей. Ну, з тих, кого я знаю."));
        }

        internal static string StuffedSaid(Player p, bool munin)
        {
            string made = Say(p, munin, "stuffed");
            if (!string.IsNullOrEmpty(made)) return made;
            return munin
                ? Pick(Lang.T("Уф. Більше не лізе. Я тепер не ворон, а бочка."), Lang.T("Нажерся. Нікуди не полечу, не проси."))
                : Pick(Lang.T("Ох, я об'ївся. Літати сьогодні буду хіба що в думках."), Lang.T("Досить. Ще ложку — і я впаду з твого плеча."));
        }

        // ---------------------------------------------------------------- stealing
        // Hungry (15 or less) and on your shoulder: every few minutes it helps itself from your bag (a stranger-minded
        // raven takes the best, a fond one the worst and warns first), and now and then it takes coins too.

        private static readonly float[] NextSteal = { -1f, -1f };
        private static readonly bool[] Warned = new bool[2];

        private static void Steal(Player p, bool munin, ItemDrop.ItemData item)
        {
            int i = munin ? 1 : 0;
            if (Zone(item) > Hungry || !Trust.Active(item) || Trust.Stranger(item, p) || Trust.Away(item)) { NextSteal[i] = -1f; Warned[i] = false; return; }
            if (NextSteal[i] < 0f) { NextSteal[i] = Time.time + StealMinutes * 30f * GearFx.StealWaitMul(item); return; } // the first a little sooner
            if (Time.time < NextSteal[i]) return;
            string off;
            if (RavenSkills.OffShoulder(p, munin, out off)) return;
            if (RavenSkills.Hushed(p)) { NextSteal[i] = Time.time + 15f; return; } // a talk going on (or the other raven just spoke): a little later
            int tier = Tone.TierOf(Trust.Level(item));
            if (tier >= 3 && !Warned[i])
            {
                Warned[i] = true;
                NextSteal[i] = Time.time + 60f;
                string warn = Say(p, munin, "steal_warn");
                RavenSkills.SayFromShoulder(p, munin, !string.IsNullOrEmpty(warn) ? warn : munin ? Lang.T("Даю тобі хвилину. Потім сам полізу в сумку.") : Lang.T("Я дуже голодний. Ще хвилина — і я візьму сам, вибач наперед."));
                return;
            }
            NextSteal[i] = Time.time + Mathf.Max(1f, StealMinutes) * 60f * GearFx.StealWaitMul(item); // (Монета на удачу: less often)
            Warned[i] = false;
            string name = Plugin.RavenName(munin);
            Trust.Food f = StealPick(p, munin, tier);
            string said = null;
            if (f != null)
            {
                // a hungry raven grabs 2–3 pieces (as many as the bag has), but fills up as on the first one only
                bool fav = IsFav(item, f.Prefab);
                float fill = FillOf(f, fav);
                int want = UnityEngine.Random.Range(2, 4);
                List<string> order = new List<string>();
                Dictionary<string, int> took = new Dictionary<string, int>();
                for (Trust.Food g = f; g != null && want > 0; want--)
                {
                    p.GetInventory().RemoveItem(Trust.FindName(p, g.Prefab), 1, -1, false);
                    int n;
                    if (!took.TryGetValue(g.Name, out n)) order.Add(g.Name);
                    took[g.Name] = n + 1;
                    g = want > 1 ? StealPick(p, munin, tier) : null;
                }
                List<string> parts = new List<string>();
                foreach (string s in order) parts.Add(took[s] > 1 ? s + " ×" + took[s] : s);
                string loot = string.Join(", ", parts.ToArray());
                Change(item, fill);
                p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} поцупив із сумки: {1} (ситість +{2}, довіри — ні)", name, loot, Mathf.RoundToInt(fill)));
                said = Say(p, munin, "steal_food", "food", loot);
                if (string.IsNullOrEmpty(said)) said = munin
                    ? Pick(Lang.T("Сам узяв. Чекати на тебе — здохнеш."), Lang.T("Не дивись так. Ти мене не годува{в|ла} — я сам себе нагодував."))
                    : Pick(Lang.T("Пробач, я взяв трохи. Ти, здається, забу{в|ла} про мене."), Lang.T("Я позичив у тебе з сумки. Неповернення гарантую."));
            }
            int coins = UnityEngine.Random.value * 100f < CoinChance ? TakeCoins(p) : 0;
            if (f != null || coins > 0) Nicks.OnTheft(p, munin); // a theft (for the nicknames)
            if (coins > 0)
            {
                bool all = Trust.Have(p, "Coins") == 0;
                p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} забрав {1}", name, Coins(coins)));
                said = Say(p, munin, all ? "steal_coins_all" : "steal_coins", "coins", Coins(coins));
                if (string.IsNullOrEmpty(said)) said = all
                    ? (munin ? Lang.F("Забрав усе, що було: {0}. Злидар. Куплю собі м'яса, раз ти не годуєш.", Coins(coins))
                             : Lang.F("Узяв {0}. Усі, що були. Куплю собі поїсти, раз ти не годуєш.", Coins(coins)))
                    : (munin ? Lang.F("{0} — мої. На жратву. Бо від тебе не дочекаєшся.", Coins(coins))
                             : Lang.F("Забрав собі {0}. Куплю на них їжі в торговця. Ну, колись.", Coins(coins)));
            }
            if (said == null) return; // nothing to take: it just goes on being hungry
            ScreamFx.Caw(p, munin, munin ? 0.85f : 1.3f, 0.5f);
            RavenSkills.SayFromShoulder(p, munin, said);
        }

        // rude: the best; in between: any; fond: the plainest
        private static Trust.Food StealPick(Player p, bool munin, int tier)
        {
            List<Trust.Food> have = new List<Trust.Food>();
            foreach (Trust.Food f in Trust.EdibleFoods(munin, Trust.Worn(munin))) if (Trust.Have(p, f.Prefab) > 0) have.Add(f);
            if (have.Count == 0) return null;
            have.Sort((a, b) => a.Points.CompareTo(b.Points));
            return tier <= 1 ? have[have.Count - 1] : tier == 2 ? have[UnityEngine.Random.Range(0, have.Count)] : have[0];
        }

        private static int TakeCoins(Player p)
        {
            int n = Mathf.Min(CoinTake, Trust.Have(p, "Coins"));
            if (n <= 0) return 0;
            p.GetInventory().RemoveItem(Trust.FindName(p, "Coins"), n, -1, false);
            return n;
        }

        // Munin, hungry (40 or less), now and then pecks at what you have just killed
        private static float _corpseAt = -999f;

        internal static void OnKill()
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(true);
            if (p == null || !Draining(item) || Trust.Away(item) || Zone(item) > Empty) return;
            if (Time.time - _corpseAt < 60f || UnityEngine.Random.value * 100f >= CorpseChance) return;
            string off;
            if (RavenSkills.OffShoulder(p, true, out off)) return;
            _corpseAt = Time.time;
            Change(item, CorpseFill);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Мунін поклював здобич: ситість +{0}", Mathf.RoundToInt(CorpseFill)));
            string said = Say(p, true, "corpse");
            RavenSkills.SayFromShoulder(p, true, !string.IsNullOrEmpty(said) ? said : Pick(Lang.T("Поклював трохи. Свіженьке. Не дякуй."), Lang.T("Ням. Хоч хтось тут мене годує — ці трупи.")));
        }

        // ---------------------------------------------------------------- twice a second, for each worn raven
        // The value is worked out from the clock whenever asked, so it is written down only every few seconds
        // (and at once on every change): no strings made twice a second for nothing.

        private const float SyncSeconds = 5f;
        private static readonly float[] NextSync = { 0f, 0f };
        private static readonly ItemDrop.ItemData[] SyncItem = new ItemDrop.ItemData[2];

        internal static void Tick(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return;
            int i = munin ? 1 : 0;
            bool draining = Draining(item);
            if (draining) RollFav(item, munin);
            if (!ReferenceEquals(SyncItem[i], item) || Time.time >= NextSync[i] || Time.time < NextSync[i] - SyncSeconds)
            {
                SyncItem[i] = item;
                NextSync[i] = Time.time + SyncSeconds;
                float starved = Sync(item);
                if (starved > 0f && draining) Trust.Melt(item, Trust.HungryLossPerDay * starved / Power.GameDaySeconds());
            }
            if (p != null) Steal(p, munin, item);
        }

        // a new world or character: nothing kept from the last one
        internal static void Reset()
        {
            NextSteal[0] = NextSteal[1] = -1f;
            Warned[0] = Warned[1] = false;
            _corpseAt = -999f;
            NextSync[0] = NextSync[1] = 0f;
            SyncItem[0] = SyncItem[1] = null;
            _zoneFrame[0] = _zoneFrame[1] = -1;
        }

        // "▮▮▮▮▮▯▯▯▯▯ 54 · ситий" for tooltips (plain bars: every font has them)
        internal static string Scale(ItemDrop.ItemData item)
        {
            float v = Value(item);
            int bars = Mathf.Clamp(Mathf.RoundToInt(v / 10f), 0, 10);
            return "<b><color=" + ZoneHexOf(item) + ">" + new string('|', bars * 2) + "</color><color=#555555>" + new string('|', (10 - bars) * 2) + "</color></b> "
                + Mathf.RoundToInt(v) + " · <color=" + ZoneHexOf(item) + ">" + ZoneWord(item) + "</color>";
        }

        // what each zone does, for the book
        internal static string ZoneText(int z)
        {
            switch (z)
            {
                case Starving: return Lang.F("Голодує (0): дари коштують ×{0} сили, сила не набирається, сидить понурий, удар −{1}%, раз на {2} хв клює тебе на {3} здоров'я, довіра тане на {4} за добу, краде їжу й монети.", Num(StarveCostMul), RavenAbilities.R(Trust.StarveStrikePct), RavenAbilities.R(Trust.StarvePeckMinutes), RavenAbilities.R(Trust.StarvePeckDamage), RavenAbilities.R(Trust.HungryLossPerDay));
                case Hungry: return Lang.F("Голодний (1–{0}): дари коштують ×{1} сили, сила не набирається, раз на {2} хв краде 2–3 шматки їжі із сумки (а наїдається, як з одного), іноді й монети (до {3}).", RavenAbilities.R(HungryAt), Num(HungryCostMul), RavenAbilities.R(StealMinutes), CoinTake);
                case Empty: return Lang.F("Просить їсти ({0}–{1}): усе як завжди, але нагадує про себе. Їжа зараз дає найбільше довіри.", RavenAbilities.R(HungryAt + 1), RavenAbilities.R(AskAt));
                case Fed: return Lang.F("Ситий ({0}–{1}): сила набирається на {2}% швидше. Найкращий стан.", RavenAbilities.R(AskAt + 1), RavenAbilities.R(StuffedAt), RavenAbilities.R((FedGainMul - 1f) * 100f));
                default: return Lang.F("Об'ївся ({0}–100): лінивий — не відлітає погуляти, удар −{1}%.", RavenAbilities.R(StuffedAt + 1), RavenAbilities.R(StuffedStrikePct));
            }
        }
    }
}
#endif

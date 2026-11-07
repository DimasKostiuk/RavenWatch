#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // One random bonus and one random drawback per raven, rolled when it is bought and kept in the item itself
    // (ItemData.m_customData, saved with the inventory). Worn, they act as a status effect with its own icon.
    internal static class Perks
    {
        private const string BonusKey = "RavenWatch_bonus";
        private const string MinusKey = "RavenWatch_minus";

        private sealed class Perk
        {
            public string Id;
            public string Stat;      // two perks on the same stat never come together
            public string Text;      // {0} is the rolled number
            public float Min, Max;
            public bool Percent;
            public bool Gold;        // a rare golden blessing (GoldChance), its numbers fixed
            public Action<SE_Stats, float> Apply; // null: its own mechanics (PerkFx), nothing on the effect
            public Perk(string id, string stat, string text, float min, float max, bool percent, Action<SE_Stats, float> apply)
            { Id = id; Stat = stat; Text = text; Min = min; Max = max; Percent = percent; Apply = apply; }
        }

        // a blessing or burden with its own mechanics (PerkFx reads its number through Of)
        private static Perk Own(string id, string stat, string text, float min, float max, bool percent) { return new Perk(id, stat, text, min, max, percent, null); }
        private static Perk Gold(string id, string text, Action<SE_Stats, float> apply) { Perk p = new Perk(id, id, text, 1f, 1f, false, apply); p.Gold = true; return p; }

        internal static float GoldChance = 0.1f; // a bought raven's blessing is a golden one this often

        private static readonly Perk[] HuginBonus =
        {
            new Perk("skill", "skill", Lang.K("+{0}% до прокачки всіх навичок"), 0.05f, 0.15f, true, (s, v) => { s.m_raiseSkill = Skills.SkillType.All; s.m_raiseSkillModifier += v; }),
            new Perk("stealth", "stealth", Lang.K("+{0}% до скритності"), 0.10f, 0.25f, true, (s, v) => s.m_stealthModifier += v),
            new Perk("runstam", "runstam", Lang.K("−{0}% витривалості на біг"), 0.05f, 0.15f, true, (s, v) => s.m_runStaminaDrainModifier -= v),
            new Perk("carry", "carry", Lang.K("+{0} до вантажопідйомності"), 25f, 75f, false, (s, v) => s.m_addMaxCarryWeight += v),
            new Perk("fall", "fall", Lang.K("−{0}% шкоди від падіння"), 0.20f, 0.50f, true, (s, v) => s.m_fallDamageModifier -= v),
            new Perk("swim", "swim", Lang.K("+{0}% до швидкості плавання"), 0.10f, 0.20f, true, (s, v) => s.m_swimSpeedModifier += v),
            new Perk("jumpstam", "jumpstam", Lang.K("−{0}% витривалості на стрибки"), 0.10f, 0.20f, true, (s, v) => s.m_jumpStaminaUseModifier -= v),
            new Perk("dodgestam", "dodgestam", Lang.K("−{0}% витривалості на перекат"), 0.10f, 0.20f, true, (s, v) => s.m_dodgeStaminaUseModifier -= v),
            new Perk("eitr", "eitr", Lang.K("+{0}% до відновлення ейтру"), 0.05f, 0.15f, true, (s, v) => s.m_eitrRegenMultiplier *= 1f + v),
            // with their own mechanics (PerkFx)
            Own("farsight", "explore", Lang.K("Зіркий: карта відкривається на {0}% ширше"), 0.30f, 0.60f, true),
            Own("wander", "rested", Lang.K("Мандрівник: «Відпочинок» триває на {0}% довше"), 0.10f, 0.20f, true),
            Own("picker", "pickup", Lang.K("Підбирач: автопідбір на {0}% ширший"), 0.50f, 1.00f, true),
            Own("sweet", "food", Lang.K("Ласун: їжа діє на {0}% довше"), 0.05f, 0.10f, true),
            Own("sniff", "sniff", Lang.K("Нюх на скарби: чує нерозкриті скрині до {0} м"), 15f, 25f, false),
            Own("whisper", "raid", Lang.K("Шепіт вітру: під час набігу показує на мапі нападників до {0} м"), 50f, 70f, false),
            Own("tongue", "trade", Lang.K("Язикатий: у торговців на {0}% дешевше"), 0.05f, 0.10f, true),
            Own("sailor", "sail", Lang.K("Мореплавець: під вітрилом корабель на {0}% швидший"), 0.05f, 0.10f, true),
        };

        private static readonly Perk[] HuginMinus =
        {
            new Perk("noise", "noise", Lang.K("+{0}% шуму: вороги чують тебе далі"), 0.10f, 0.25f, true, (s, v) => s.m_noiseModifier += v),
            new Perk("stamregen", "stamregen", Lang.K("−{0}% відновлення витривалості"), 0.05f, 0.10f, true, (s, v) => s.m_staminaRegenMultiplier *= 1f - v),
            new Perk("weak", "damage", Lang.K("−{0}% шкоди"), 0.03f, 0.08f, true, (s, v) => { s.m_modifyAttackSkill = Skills.SkillType.All; s.m_damageModifier *= 1f - v; }),
            new Perk("slow", "speed", Lang.K("−{0}% швидкості"), 0.02f, 0.05f, true, (s, v) => s.m_speedModifier -= v),
            Own("chatter", "noise", Lang.K("Балакун: раз на 20–30 хв бовкне вголос, і вороги до {0} м тебе помітять"), 15f, 25f, false),
            Own("sleepy", "rested", Lang.K("Сонливий: «Відпочинок» на {0}% коротший"), 0.10f, 0.15f, true),
            Own("seasick", "seasick", Lang.K("Морська хвороба: на кораблі витривалість відновлюється на {0}% повільніше"), 0.20f, 0.30f, true),
        };

        private static readonly Perk[] HuginGold =
        {
            Gold("g_eye", Lang.K("Око Одіна: +30% до скритності, а карта відкривається вдвічі ширше"), (s, v) => s.m_stealthModifier += 0.30f),
            Gold("g_trail", Lang.K("Слід Хугіна: «Відпочинок» триває на 30% довше, автопідбір удвічі ширший"), null),
            Gold("g_whisper", Lang.K("Шепіт Одіна: під час набігу всі нападники до 150 м — на мапі, а Хугін каже, звідки вони йдуть"), null),
        };

        private static readonly Perk[] MuninBonus =
        {
            new Perk("damage", "damage", Lang.K("+{0}% шкоди"), 0.03f, 0.08f, true, (s, v) => { s.m_modifyAttackSkill = Skills.SkillType.All; s.m_damageModifier *= 1f + v; }),
            new Perk("attackstam", "attackstam", Lang.K("−{0}% витривалості на удари"), 0.05f, 0.15f, true, (s, v) => s.m_attackStaminaUseModifier -= v),
            new Perk("blockstam", "blockstam", Lang.K("−{0}% витривалості на блок"), 0.05f, 0.15f, true, (s, v) => s.m_blockStaminaUseModifier -= v),
            new Perk("dodgestam", "dodgestam", Lang.K("−{0}% витривалості на ривок"), 0.10f, 0.20f, true, (s, v) => s.m_dodgeStaminaUseModifier -= v),
            new Perk("hpregen", "hpregen", Lang.K("+{0}% до відновлення здоров'я"), 0.05f, 0.15f, true, (s, v) => s.m_healthRegenMultiplier *= 1f + v),
            // with their own mechanics (PerkFx)
            Own("grudge", "grudge", Lang.K("Злопам'ятний: +{0}% шкоди тим, хто вбив тебе останнім"), 0.10f, 0.15f, true),
            Own("hunter", "trophy", Lang.K("Мисливець: трофеї з твоїх убитих падають на {0}% частіше"), 0.10f, 0.20f, true),
            Own("bloodthirst", "killheal", Lang.K("Кровожер: кожне вбивство лікує на {0} здоров'я (бос — на 25)"), 2f, 4f, false),
            Own("scav", "loot", Lang.K("Стерв'ятник: {0}% шанс на зайву здобич з твого вбитого"), 0.08f, 0.12f, true),
            Own("revenge", "deathdmg", Lang.K("Месник: 10 хв після смерті +{0}% шкоди"), 0.10f, 0.15f, true),
            Own("finisher", "finisher", Lang.K("Добивайло: +{0}% шкоди по ворогах, у яких лишилось ≤30% здоров'я"), 0.08f, 0.12f, true),
            Own("bodymem", "skillloss", Lang.K("Пам'ять тіла: після смерті навички падають на {0}% менше"), 0.25f, 0.50f, true),
            Own("grave", "corpserun", Lang.K("До могили: після смерті бігаєш на {0}% швидше, поки не забереш речі"), 0.08f, 0.12f, true),
        };

        private static readonly Perk[] MuninMinus =
        {
            new Perk("hpregen-", "hpregen", Lang.K("−{0}% відновлення здоров'я: Мунін виїдає нерви"), 0.05f, 0.15f, true, (s, v) => s.m_healthRegenMultiplier *= 1f - v),
            new Perk("noise", "noise", Lang.K("+{0}% шуму: Мунін лається на весь ліс"), 0.10f, 0.25f, true, (s, v) => s.m_noiseModifier += v),
            new Perk("carry-", "carry", Lang.K("−{0} вантажопідйомності"), 25f, 50f, false, (s, v) => s.m_addMaxCarryWeight -= v),
            Own("gloat", "deathdmg", Lang.K("Зловтішний: 5 хв після смерті −{0}% шкоди"), 0.05f, 0.08f, true),
            Own("bored", "bored", Lang.K("Занудьгував: пів години без убивств — −{0}% шкоди, поки когось не вб'єш"), 0.05f, 0.05f, true),
            Own("glutton", "food", Lang.K("Ненажера: їжа діє на {0}% коротше"), 0.05f, 0.10f, true),
            Own("boor", "trade", Lang.K("Хамло: у торговців на {0}% дорожче"), 0.05f, 0.10f, true),
        };

        private static readonly Perk[] MuninGold =
        {
            Gold("g_memory", Lang.K("Пам'ять Одіна: +25% шкоди трьом останнім видам, що тебе вбили"), null),
            Gold("g_feast", Lang.K("Кривава учта: кожне вбивство — +10 здоров'я і +10 витривалості"), null),
            Gold("g_deathless", Lang.K("Безсмертна пам'ять: після смерті навички падають на 75% менше"), null),
        };

        private static Perk[] Bonuses(bool munin) { return munin ? MuninBonus : HuginBonus; }
        private static Perk[] Minuses(bool munin) { return munin ? MuninMinus : HuginMinus; }
        private static Perk[] Golds(bool munin) { return munin ? MuninGold : HuginGold; }

        // a blessing as it is read back: the plain ones and the golden ones (made once: Sync reads it twice a second)
        private static readonly Perk[][] AllB = new Perk[2][];
        private static Perk[] AllBonuses(bool munin)
        {
            int i = munin ? 1 : 0;
            if (AllB[i] != null) return AllB[i];
            Perk[] a = Bonuses(munin), g = Golds(munin);
            Perk[] all = new Perk[a.Length + g.Length];
            a.CopyTo(all, 0); g.CopyTo(all, a.Length);
            return AllB[i] = all;
        }

        private static readonly System.Random Rng = new System.Random();

        // ---------------------------------------------------------------- rolling

        internal static bool Rolled(ItemDrop.ItemData item)
        {
            return item.m_customData != null && item.m_customData.ContainsKey(BonusKey) && item.m_customData.ContainsKey(MinusKey);
        }

        internal static void Roll(ItemDrop.ItemData item, bool munin)
        {
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            Norov.Character c = Norov.EnsureChar(item, munin); // its character first: the blessings and burdens that suit it come twice as often
            Perk bonus = Pick(Rng.NextDouble() < GoldChance ? Golds(munin) : Bonuses(munin), null, null, c);
            Perk minus = Pick(Minuses(munin), bonus.Stat, NoMinusStat(item), c);
            item.m_customData[BonusKey] = bonus.Id + "=" + RollValue(bonus).ToString("R", CultureInfo.InvariantCulture);
            item.m_customData[MinusKey] = minus.Id + "=" + RollValue(minus).ToString("R", CultureInfo.InvariantCulture);
        }

        // a golden blessing (the book writes it in gold)
        internal static bool Golden(ItemDrop.ItemData item)
        {
            string id = Id(item, BonusKey);
            return id.StartsWith("g_");
        }

        // how lucky a roll came out: the blessing's number high in its range, the burden's low (−1 bad … 1 good); 2 golden
        internal static float Luck(ItemDrop.ItemData item)
        {
            if (Golden(item)) return 2f;
            bool munin;
            RavenItem.IsRaven(item, out munin);
            Perk b, m; float bv, mv;
            if (!Read(item, BonusKey, AllBonuses(munin), out b, out bv) || !Read(item, MinusKey, Minuses(munin), out m, out mv)) return 0f;
            return Share(b, bv) - Share(m, mv);
        }
        private static float Share(Perk p, float v) { return p.Max - p.Min > 1e-5f ? UnityEngine.Mathf.Clamp01((v - p.Min) / (p.Max - p.Min)) : 0.5f; }

        // the Fate stone: a new fate, never the same blessing and burden it had (a stone spent on the same roll looks broken)
        internal static void Reroll(ItemDrop.ItemData item, bool munin)
        {
            string b0 = Id(item, BonusKey), m0 = Id(item, MinusKey);
            for (int i = 0; i < 12; i++)
            {
                Roll(item, munin);
                if (Id(item, BonusKey) != b0 && Id(item, MinusKey) != m0) return;
            }
        }

        private static string Id(ItemDrop.ItemData item, string key)
        {
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(key, out raw)) return "";
            int eq = raw.IndexOf('=');
            return eq >= 0 ? raw.Substring(0, eq) : raw;
        }

        private static Perk Pick(Perk[] pool, string notStat, string notStat2, Norov.Character c)
        {
            List<Perk> ok = new List<Perk>();
            float sum = 0f;
            foreach (Perk p in pool) if (p.Stat != notStat && p.Stat != notStat2) { ok.Add(p); sum += Weight(p, c); }
            float r = (float)Rng.NextDouble() * sum;
            foreach (Perk p in ok) { r -= Weight(p, c); if (r <= 0f) return p; }
            return ok[ok.Count - 1];
        }

        private static float Weight(Perk p, Norov.Character c) { return c != null && Array.IndexOf(c.Perks, p.Id) >= 0 ? 2f : 1f; }

        // a loud raven (its quirk «Галасливий») never also gets the noise burden: one is enough
        private static string NoMinusStat(ItemDrop.ItemData item) { return Norov.HasQuirk(item, "loud") ? "noise" : null; }

        // the burden's stat of this raven (Norov: a raven with the noise burden is not rolled «Галасливий»)
        internal static bool NoiseBurden(ItemDrop.ItemData item)
        {
            string raw;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(MinusKey, out raw) && (raw.StartsWith("noise=") || raw.StartsWith("chatter="));
        }

        // a raven rolled with a blessing or burden that is gone now (the armour ones, 1.7.0): only that one is rolled again
        private static bool Mend(ItemDrop.ItemData item, bool munin)
        {
            Perk b, m; float bv, mv;
            bool hasB = Read(item, BonusKey, AllBonuses(munin), out b, out bv);
            bool hasM = Read(item, MinusKey, Minuses(munin), out m, out mv);
            if (hasB && hasM) return false;
            Norov.Character c = Norov.CharOf(item);
            if (!hasB) { b = Pick(Bonuses(munin), hasM ? m.Stat : null, null, c); item.m_customData[BonusKey] = b.Id + "=" + RollValue(b).ToString("R", CultureInfo.InvariantCulture); }
            if (!hasM) { m = Pick(Minuses(munin), b.Stat, NoMinusStat(item), c); item.m_customData[MinusKey] = m.Id + "=" + RollValue(m).ToString("R", CultureInfo.InvariantCulture); }
            return true;
        }

        // whole percents and whole numbers: the tooltip shows exactly what applies
        private static float RollValue(Perk p)
        {
            float v = p.Min + (float)Rng.NextDouble() * (p.Max - p.Min);
            return p.Percent ? Mathf.Round(v * 100f) / 100f : Mathf.Round(v);
        }

        private static bool Read(ItemDrop.ItemData item, string key, Perk[] pool, out Perk perk, out float value)
        {
            perk = null; value = 0f;
            string raw;
            if (item.m_customData == null || !item.m_customData.TryGetValue(key, out raw)) return false;
            int eq = raw.IndexOf('=');
            if (eq <= 0 || !float.TryParse(raw.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            string id = raw.Substring(0, eq);
            foreach (Perk p in pool) if (p.Id == id) { perk = p; return true; }
            return false;
        }

        private static string Line(Perk p, float v)
        {
            return string.Format(Lang.T(p.Text), p.Percent ? Mathf.RoundToInt(v * 100f) : Mathf.RoundToInt(v));
        }

        // the Book of ravens: every bonus and drawback with its range
        internal static string Catalog()
        {
            return Lang.F("<b>Хугін</b>\n<color=#7CFC7C>{0}</color>\n<color=#E6C77C>{1}</color>\n<color=#FF6B6B>{2}</color>\n\n<b>Мунін</b>\n<color=#7CFC7C>{3}</color>\n<color=#E6C77C>{4}</color>\n<color=#FF6B6B>{5}</color>",
                Ranges(HuginBonus), Ranges(HuginGold), Ranges(HuginMinus), Ranges(MuninBonus), Ranges(MuninGold), Ranges(MuninMinus));
        }

        private static string Ranges(Perk[] pool)
        {
            List<string> lines = new List<string>();
            foreach (Perk p in pool)
            {
                if (p.Gold) { lines.Add("★ " + Lang.T(p.Text)); continue; }
                string lo = p.Percent ? Mathf.RoundToInt(p.Min * 100f).ToString() : Mathf.RoundToInt(p.Min).ToString();
                string hi = p.Percent ? Mathf.RoundToInt(p.Max * 100f).ToString() : Mathf.RoundToInt(p.Max).ToString();
                lines.Add("• " + string.Format(Lang.T(p.Text), lo == hi ? lo : lo + "–" + hi));
            }
            return string.Join("\n", lines.ToArray());
        }

        internal static string ListIds()
        {
            return Lang.F("Хугін, блага: {0}\nХугін, золоті: {1}\nХугін, тягарі: {2}\nМунін, блага: {3}\nМунін, золоті: {4}\nМунін, тягарі: {5}", Ids(HuginBonus), Ids(HuginGold), Ids(HuginMinus), Ids(MuninBonus), Ids(MuninGold), Ids(MuninMinus));
        }

        private static string Ids(Perk[] pool)
        {
            List<string> ids = new List<string>();
            foreach (Perk p in pool) ids.Add(p.Id);
            return string.Join(", ", ids.ToArray());
        }

        // the console's exact choice, with a random value in the perk's range
        internal static string SetExact(ItemDrop.ItemData item, bool munin, string bonusId, string minusId)
        {
            Perk b = Array.Find(AllBonuses(munin), x => x.Id == bonusId);
            Perk m = Array.Find(Minuses(munin), x => x.Id == minusId);
            if (b == null || m == null) return Lang.T("Немає такого блага чи тягаря. Список: raven perks");
            if (b.Stat == m.Stat) return Lang.T("Благо й тягар на одну характеристику — так не можна.");
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[BonusKey] = b.Id + "=" + RollValue(b).ToString("R", CultureInfo.InvariantCulture);
            item.m_customData[MinusKey] = m.Id + "=" + RollValue(m).ToString("R", CultureInfo.InvariantCulture);
            return null;
        }

        internal static string Describe(ItemDrop.ItemData item)
        {
            bool munin;
            RavenItem.IsRaven(item, out munin);
            Perk b, m; float bv, mv;
            bool hasB = Read(item, BonusKey, AllBonuses(munin), out b, out bv);
            bool hasM = Read(item, MinusKey, Minuses(munin), out m, out mv);
            if (!hasB || !hasM) return ""; // (the blessing still hidden: said with the sleep, RavenTooltipPatch)
            return Lang.F("\n\n<color=#7CFC7C>Благо: {0}</color>\n<color=#FF6B6B>Тягар: {1}</color>", Line(b, bv), Line(m, mv));
        }

        // the book: the bonus and the drawback as two plain lines; false while still hidden (not bought)
        internal static bool Lines(ItemDrop.ItemData item, out string bonus, out string minus)
        {
            bonus = minus = null;
            bool munin;
            if (item == null || !RavenItem.IsRaven(item, out munin)) return false;
            Perk b, m; float bv, mv;
            if (!Read(item, BonusKey, AllBonuses(munin), out b, out bv) || !Read(item, MinusKey, Minuses(munin), out m, out mv)) return false;
            bonus = Line(b, bv); minus = Line(m, mv);
            return true;
        }

        internal static string Short(ItemDrop.ItemData item)
        {
            bool munin;
            RavenItem.IsRaven(item, out munin);
            Perk b, m; float bv, mv;
            if (!Read(item, BonusKey, AllBonuses(munin), out b, out bv) || !Read(item, MinusKey, Minuses(munin), out m, out mv)) return "";
            return Lang.F("благо {0}, тягар {1}", Line(b, bv), Line(m, mv));
        }

        // ---------------------------------------------------------------- worn: the status effect

        private static readonly string[] Applied = new string[2];

        // where the raven is, in its effect's name: on the shoulder, out scouting, up over the dome
        internal static string ShoulderLine(bool munin)
        {
            return (munin ? Lang.T("Мунін") : Lang.T("Хугін")) + " " + FlightWord(Player.m_localPlayer, munin);
        }

        // what it does when not away: on the shoulder, or up in the air on an order, a gift or its own outing
        internal static string FlightWord(Player p, bool munin) { return OffWord(p, munin) ?? Lang.T("на плечі"); }

        // the same, null when it sits on the shoulder (the state lines: a visit or an outing of its own is no "на плечі")
        internal static string OffWord(Player p, bool munin)
        {
            if (Strike.Busy(p, munin)) return Lang.T("в атаці");
            if (!munin && Decoy.Mine()) return Lang.T("дражнить ворога");
            int o = Orders.Of(p, munin);
            if (o == Orders.Follow) return Lang.T("летить поруч");
            if (o == Orders.Circle) return Lang.T("кружляє");
            if (o == Orders.Scout) return Lang.T("облітає");
            if (o == Orders.Chase) return Lang.T("у догонялках");
            if (o != Orders.None) return Lang.T("в польоті");
            string visit = WorldVisit.Word(munin);
            if (visit != null) return visit;
            int out_ = Outing.Of(p, munin);
            if (out_ == Outing.Hunt) return Lang.T("полює");
            if (out_ == Outing.Fish) return Lang.T("пірнає по рибу");
            if (out_ != Outing.None) return Lang.T("гуляє");
            if (!munin && RavenSkills.Scouting()) return Lang.T("на розвідці");
            if (!munin && Dome.Mine()) return Lang.T("над куполом");
            if (munin && Tame.Mine()) return Lang.T("приборкує звіра");
            if (Whirl.On()) return Lang.T("у вихорі");
            if (Load.On()) return Lang.T("несе ношу");
            if (ClearSky.Mine()) return Lang.T("розганяє хмари");
            return Shoulder.AirWord(p, munin); // (anything else that flies it)
        }

        // twice a second: the name follows him as he comes and goes, and says when he is hungry
        internal static void ShoulderName(Player p)
        {
            if (p == null) return;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            foreach (bool munin in new bool[] { false, true })
            {
                StatusEffect se = p.GetSEMan().GetStatusEffect(EffectName(munin).GetStableHashCode());
                if (se == null) continue;
                ItemDrop.ItemData item = munin ? m : h;
                string want = item != null && Trust.Away(item) ? (munin ? Lang.T("Мунін") : Lang.T("Хугін")) + " " + Trust.AwayWord(munin, item) : ShoulderLine(munin);
                if (item != null && !Trust.Away(item) && Trust.Active(item) && !Trust.Stranger(item, p) && Satiety.Zone(item) != Satiety.Fed)
                    want += " <color=" + Satiety.ZoneHexOf(item) + ">(" + Satiety.ZoneWord(item) + ")</color>";
                if (se.m_name != want) se.m_name = want;
            }
        }

        // "Ситість: |||||||||| 54 · ситий" for the effect's tooltip, with what the hunger does now
        internal static string Fullness(ItemDrop.ItemData item)
        {
            if (item == null || !Trust.Active(item)) return "";
            int z = Satiety.Zone(item);
            string what = z == Satiety.Starving ? Lang.T(" — дари дорожчі, довіра тане, краде")
                : z == Satiety.Hungry ? Lang.T(" — дари дорожчі, сила не йде, краде")
                : z == Satiety.Empty ? Lang.T(" — нагодуй (J → «Погодувати» чи книга)")
                : z == Satiety.Stuffed ? Lang.T(" — лінивий, удар слабший") : "";
            return Lang.F("\nСитість: {0}{1}", Satiety.Scale(item), what);
        }

        private static readonly HashSet<ItemDrop.ItemData> Checked = new HashSet<ItemDrop.ItemData>(); // ravens already looked at by Mend

        // from the twice-a-second sync: new ravens get their roll, worn ones their effect
        private static readonly List<ItemDrop.ItemData> _all = new List<ItemDrop.ItemData>(8); // Sync's own, reused

        internal static void Sync(Player p)
        {
            // the bag and the shoulders: with SeneaL UI's shoulder cells a seated raven is not in the bag at all
            _all.Clear();
            _all.AddRange(p.GetInventory().GetAllItems());
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (!_all.Contains(item)) _all.Add(item);
            foreach (ItemDrop.ItemData item in _all)
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin)) continue;
                if (!Trust.Stranger(item, p))
                {
                    if (!p.m_customData.ContainsKey(Trial.HadKey)) p.m_customData[Trial.HadKey] = "1"; // (Odin's trial is for those who never had one)
                    string kind = munin ? Trial.HadMuninKey : Trial.HadHuginKey;
                    if (!p.m_customData.ContainsKey(kind)) p.m_customData[kind] = "1"; // (a raven of this kind is yours: no big one of it flies to you any more)
                }
                bool fresh = !Rolled(item);
                if (Wing.Ensure(item, fresh) && !fresh && !Trust.Stranger(item, p)) Backup.Save(p, item); // its pace in the air (an old raven's from its id)
                if (fresh)
                {
                    Roll(item, munin);
                    if (Golden(item)) PerkFx.GoldRolled(p, munin, item); // a golden blessing: said aloud
                    Trust.InitNew(item);
                    if (!Trust.Active(item)) Guide.Queue(p, Guide.Bought, 15f); // a new, sleeping raven: Hugin comes to explain
#if RAVEN_STORY
                    if (!Trust.Active(item) && !item.m_customData.ContainsKey(StoryOwn.OwnKey) && !RavenBook.Given(p)) Guide.Queue(p, Guide.NoBook, 15f); // (bought before the book: told that only the prologue gives it; the prologue's own raven comes in its story)
#endif
                }
                if (Checked.Count > 32) Checked.Clear(); // (every copy of a raven — back from a grave, a chest — is a new item: never a growing pile)
                if (Checked.Add(item) && !Trust.Stranger(item, p) && Mend(item, munin)) Backup.Save(p, item); // a blessing or burden no longer there: rolled again (once a session)
                if (!Norov.Rolled(item)) Norov.Ensure(p, item, munin); // its norov (old ravens too, once)
            }
            ItemDrop.ItemData hugin, munin2;
            RavenItem.Worn(p, out hugin, out munin2);
            Apply(p, false, hugin);
            Apply(p, true, munin2);
            Refresh(hugin, munin2); // (PerkFx reads it: RavenSkills ticks it right after)
            Wing.Publish(p, hugin, munin2); // their pace now, for every game that flies them
            OnTop(p);
            Label(p, false, hugin);
            Label(p, true, munin2);
        }

        // a raven that flew off: its effect says so, and the number beside it becomes the time until it is back
        private static void Label(Player p, bool munin, ItemDrop.ItemData item)
        {
            RavenPassive se = p.GetSEMan().GetStatusEffect(EffectName(munin).GetStableHashCode()) as RavenPassive;
            if (se == null) return;
            bool away = item != null && Trust.Away(item);
            se.AwayUntil = away ? Time.time + Trust.AwayLeft(item) : 0f; // the name itself: ShoulderName
        }

        // the two ravens head the list of effects (Hugin, then Munin), however late they were put on
        private static void OnTop(Player p)
        {
            List<StatusEffect> list = p.GetSEMan().GetStatusEffects();
            int at = 0;
            foreach (bool munin in new bool[] { false, true })
            {
                int hash = EffectName(munin).GetStableHashCode();
                int i = list.FindIndex(se => se != null && se.NameHash() == hash);
                if (i < 0) continue;
                if (i != at) { StatusEffect se = list[i]; list.RemoveAt(i); list.Insert(at, se); }
                at++;
            }
        }

        internal static void Forget() { Applied[0] = null; Applied[1] = null; }

        private static void Apply(Player p, bool munin, ItemDrop.ItemData item)
        {
            int slot = munin ? 1 : 0;
            string bonusRaw = null, minusRaw = null;
            if (item != null && item.m_customData != null) { item.m_customData.TryGetValue(BonusKey, out bonusRaw); item.m_customData.TryGetValue(MinusKey, out minusRaw); }
            string sig = item != null ? bonusRaw + "|" + minusRaw + "|" + (Trust.Active(item) ? "awake" : "asleep") : null;
            SEMan seman = p.GetSEMan();
            int hash = EffectName(munin).GetStableHashCode();
            if (sig == Applied[slot] && (sig == null || seman.HaveStatusEffect(hash))) return;
            if (seman.HaveStatusEffect(hash)) seman.RemoveStatusEffect(hash, true);
            Applied[slot] = sig;
            if (item == null || !Trust.Active(item)) return; // asleep: no bonus, no drawback yet

            Perk b, m; float bv, mv;
            if (!Read(item, BonusKey, AllBonuses(munin), out b, out bv) || !Read(item, MinusKey, Minuses(munin), out m, out mv)) return;
            RavenPassive se = ScriptableObject.CreateInstance<RavenPassive>();
            se.name = EffectName(munin);
            se.m_name = ShoulderLine(munin);
            se.m_icon = RavenItem.Icon(munin);
            se.m_ttl = 0f;
            if (b.Apply != null) b.Apply(se, bv);
            if (m.Apply != null) m.Apply(se, mv);
            se.Munin = munin;
            se.Text = Lang.F("<color={0}>Благо: {1}</color>\n<color=#FF6B6B>Тягар: {2}</color>", (b.Gold ? "#E6C77C" : "#7CFC7C"), Line(b, bv), Line(m, mv));
            seman.AddStatusEffect(se, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- the ones with their own mechanics

        // what the worn, awake ravens give now: id → its number (golden ones: 1); PerkFx reads it, made again on every Sync
        private static readonly Dictionary<string, float> Live = new Dictionary<string, float>();

        internal static float Of(string id) { float v; return Live.TryGetValue(id, out v) ? v : 0f; }
        internal static bool Has(string id) { return Live.ContainsKey(id); }

        private static string _live0 = "?", _live1 = "?";

        private static void Refresh(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            if (Applied[0] == _live0 && Applied[1] == _live1) return; // (Apply's own signature of each: the same, nothing to read again)
            _live0 = Applied[0]; _live1 = Applied[1];
            Live.Clear();
            Take(hugin, false);
            Take(munin, true);
        }

        private static void Take(ItemDrop.ItemData item, bool munin)
        {
            if (item == null || !Trust.Active(item)) return; // asleep: nothing yet (as the effect)
            Perk b, m; float bv, mv;
            if (Read(item, BonusKey, AllBonuses(munin), out b, out bv)) Live[b.Id] = bv;
            if (Read(item, MinusKey, Minuses(munin), out m, out mv)) Live[m.Id] = mv;
        }

        // the blessing's own name as the raven announces it (a golden one when it is bought)
        internal static string BonusLine(ItemDrop.ItemData item)
        {
            bool munin;
            Perk b; float bv;
            if (item == null || !RavenItem.IsRaven(item, out munin) || !Read(item, BonusKey, AllBonuses(munin), out b, out bv)) return "";
            return Line(b, bv);
        }

        // the raven's passives with when they work again: in the effect's tooltip and in the item's
        internal static string Passives(bool munin, bool onShoulder)
        {
            string text = "";
            int n = 0;
            foreach (RavenAbility a in RavenAbilities.All)
            {
                if (!a.Passive || a.Munin != munin) continue;
                if ((a.Id == "lastchance" && !LastChance.Enabled) || (a.Id == "bloodfury" && !BloodFury.Enabled) || ((a.Id == "anger" || a.Id == "lightstep") && !WeakPassive.Enabled) || (Loyal.Is(a) && !Loyal.Enabled) || (BloodRevenge.Is(a) && !BloodRevenge.Enabled) || (Omen.Is(a) && !Omen.Enabled)) continue;
                // a line each, no "name: value": tooltip mods (SeneaL UI) split those into separate blocks
                text += Lang.F("\n• <color=orange><b>{0}</b></color> — {1} <color=#9a9a9a>{2} · {3}</color>", a.Plain, a.Short.TrimEnd('.'),
                    (RavenAbilities.Free(a) ? Lang.T("без сили") : Lang.F("{0} сили{1}, раз на {2}", Mathf.RoundToInt(a.Cost()), Satiety.CostNote(munin), RavenAbilities.Span(a.Cooldown()))),
                    RavenAbilities.PassiveReady(a));
                n++;
            }
            if (n == 0) return "";
            return "\n\n" + (onShoulder ? Lang.T("Діють самі") : Lang.T("Діють самі, поки ворон на плечі")) + text;
        }

        private static string EffectName(bool munin) { return munin ? "RavenWatch_MuninPassive" : "RavenWatch_HuginPassive"; }
    }

    // the effect lists the raven's own two lines instead of vanilla's generic stat text
    internal class RavenPassive : SE_Stats
    {
        public string Text = "";
        public bool Munin;
        public float AwayUntil; // while the raven is away: when it comes back
        public override string GetTooltipString()
        {
            return Lang.F("{0}\nСила: {1}/{2}{3}{4}", Text, Power.Current(Munin), Mathf.RoundToInt(Power.MaxWorn(Munin)), Perks.Fullness(Trust.Worn(Munin)), Perks.Passives(Munin, true));
        }
        // the number next to the icon (SeneaL UI shows it too): how much strength is left
        // in the raven's own colour, bold, to stand out among the other effects
        // the game asks for it every frame: the text is made again only when the number changed
        private int _shown = int.MinValue;
        private string _text = "";
        public override string GetIconText()
        {
            int v = AwayUntil > Time.time ? -1 - Mathf.CeilToInt(AwayUntil - Time.time) : Power.Current(Munin); // negative: seconds away
            if (v == _shown) return _text;
            _shown = v;
            _text = v < 0
                ? "<size=150%><b><color=#FF5A5A>" + RavenAbilities.Clock(-1 - v) + "</color></b></size>"
                : "<size=150%><b><color=" + (Munin ? "#FF6A4D" : "#7FD0FF") + ">" + v + "</color></b></size>";
            return _text;
        }
    }

    // Path of Valheiman, by default, stops the game's own skill growth and turns it into tree experience before the
    // game applies any "+% to skill gain" effect: Hugin's bonus was lost there. With the game's growth turned off,
    // the raven's bonus is put into what PoV counts, so it speeds up the tree experience instead.
    [HarmonyLib.HarmonyPatch(typeof(Skills), "RaiseSkill")]
    [HarmonyLib.HarmonyBefore("treextr.pathofvalheiman")]
    internal static class RavenSkillGainPatch
    {
        private static System.Reflection.FieldInfo _flag;
        private static bool _looked;

        // PoV is there and has the game's own growth turned off
        private static bool PovTakesOver()
        {
            if (!_looked)
            {
                _looked = true;
                Type t = HarmonyLib.AccessTools.TypeByName("PassiveTree.Plugin");
                _flag = t != null ? HarmonyLib.AccessTools.Field(t, "DisableVanillaSkills") : null;
            }
            BepInEx.Configuration.ConfigEntry<bool> e = _flag != null ? _flag.GetValue(null) as BepInEx.Configuration.ConfigEntry<bool> : null;
            return e != null && e.Value;
        }

        private static void Prefix(Skills __instance, Skills.SkillType skillType, ref float factor)
        {
            Player me = Player.m_localPlayer;
            if (__instance == null || me == null || !ReferenceEquals(__instance, me.GetSkills()) || !PovTakesOver()) return;
            float mul = 1f;
            foreach (StatusEffect se in me.GetSEMan().GetStatusEffects())
                if (se is RavenPassive) se.ModifyRaiseSkill(skillType, ref mul);
            factor *= mul;
        }
    }
}
#endif
#if RAVEN_ITEM
namespace HuginWhispers
{
    // The game's own effect list writes the effect's name and the number under its icon with tags off: "(голодний)" and
    // the strength showed their tags as text. They are turned on for both (SeneaL UI has its own list, where they work).
    [HarmonyLib.HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
    internal static class RavenEffectNameColourPatch
    {
        private static readonly System.Reflection.FieldInfo List = HarmonyLib.AccessTools.Field(typeof(Hud), "m_statusEffects");
        private static readonly System.Collections.Generic.HashSet<UnityEngine.RectTransform> Done = new System.Collections.Generic.HashSet<UnityEngine.RectTransform>();

        private static void Postfix(Hud __instance)
        {
            System.Collections.Generic.List<UnityEngine.RectTransform> rows = List != null ? List.GetValue(__instance) as System.Collections.Generic.List<UnityEngine.RectTransform> : null;
            if (rows == null) return;
            // every frame: each row looked into once (the game makes new rows when the list of effects changes)
            foreach (UnityEngine.RectTransform row in rows)
            {
                if (row == null || Done.Contains(row)) continue;
                TMPro.TMP_Text name = row.GetComponentInChildren<TMPro.TMP_Text>();
                if (name != null && !name.richText) name.richText = true;
                // the number under the icon too: our ravens' strength comes in size/colour tags (RavenPassive.GetIconText),
                // printed as text they made one long, tiny line
                UnityEngine.Transform time = row.Find("TimeText");
                TMPro.TMP_Text tt = time != null ? time.GetComponent<TMPro.TMP_Text>() : null;
                if (tt != null && !tt.richText) tt.richText = true;
                Done.Add(row);
            }
            if (Done.Count > 64) Done.RemoveWhere(r => r == null); // rows the game has destroyed
        }
    }
}
#endif

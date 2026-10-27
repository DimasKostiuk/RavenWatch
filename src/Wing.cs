#if RAVEN_ITEM
using System;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Крило»: each raven's own pace in the air, rolled when it is bought (old ravens: from their id, the same in every
    // game) and kept in the item. It is a share of its kind's speed (±Spread) and goes into every flight through the
    // flight body (FlightBody.Pace): the ways out and home, the strike there and back, the chase, the scouting. Its
    // manner — how it turns, banks, swings round — stays its character's. A hungry or stuffed raven flies slower now,
    // the knack «Швидке крило» a little faster, its training (Strike's «Вишкіл») TrainPer a level faster. The pace now of
    // both worn ravens rides on the owner's ZDO, so every game flies them alike.
    // The wing is kept in the item on a ±StoredSpread scale (as first rolled) and flown at ±Spread (half of it,
    // ±10%, so Munin is the quicker kind nearly always and the training makes the rest): a «стрімкий» stays the swiftest.
    // Its whole range (fed .9–1.1 × knack 1.06 × training 1.087; hungry .85) stays inside .76–1.27 — the motor's braking
    // goes by the distance left, not the pace, so a quick raven still lands softly.
    internal static class Wing
    {
        internal const string Key = "RavenWatch_wing";    // the item: its own pace (×)
        internal const string NetKey = "RavenWatch_Wing"; // the owner's ZDO: Hugin's pace now in the low byte, Munin's in the next
        internal static float Spread = 0.1f;              // ± its kind's speed, as flown
        private const float StoredSpread = 0.2f;          // ± as kept in the item (the roll's scale: older ravens too)
        internal static float TrainPer = 0.003f;          // × a level of training above the first («Вишкіл»: +8.7% at 30)
        internal static float HungryMul = 0.85f;          // hungry or starving
        internal static float StuffedMul = 0.9f;          // stuffed
        internal static float QuickMul = 1.06f;           // the knack «Швидке крило»

        // ---------------------------------------------------------------- its own pace

        internal static bool Rolled(ItemDrop.ItemData item) { return item != null && item.m_customData != null && item.m_customData.ContainsKey(Key); }

        // its own wing, −1 (the slowest) … 1 (the swiftest) of its kind
        private static float Share(ItemDrop.ItemData item)
        {
            string raw;
            float v;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw)
                || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return 0f;
            return Mathf.Clamp((v - 1f) / StoredSpread, -1f, 1f);
        }

        // its own wing as flown (×)
        internal static float Base(ItemDrop.ItemData item) { return 1f + Share(item) * Spread; }

        // a raven without its pace gets one: a new one at random, an old one from its id (so it is the same raven in
        // every game and after a restore); true when it was given now
        internal static bool Ensure(ItemDrop.ItemData item, bool fresh)
        {
            if (item == null || Rolled(item)) return false;
            if (item.m_customData == null) item.m_customData = new System.Collections.Generic.Dictionary<string, string>();
            string id = Backup.Id(item);
            float u = !fresh && !string.IsNullOrEmpty(id) ? Hash01(id) : UnityEngine.Random.value;
            float pace = Mathf.Round((1f - StoredSpread + u * StoredSpread * 2f) * 100f) / 100f;
            item.m_customData[Key] = pace.ToString("0.00", CultureInfo.InvariantCulture);
            return true;
        }

        // the console: its wing as flown (×), kept on the stored scale
        internal static void Set(ItemDrop.ItemData item, float pace)
        {
            if (item == null) return;
            if (item.m_customData == null) item.m_customData = new System.Collections.Generic.Dictionary<string, string>();
            float share = Mathf.Clamp((pace - 1f) / Spread, -1f, 1f);
            item.m_customData[Key] = (1f + share * StoredSpread).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static float Hash01(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return (h % 10000u) / 9999f;
        }

        // its pace now: its own, its knack, its training, its hunger
        internal static float Now(ItemDrop.ItemData item)
        {
            if (item == null) return 1f;
            return Own(item) * HungerMul(item) * GearFx.WingMul(item) * BreedTraits.WingMul(item) * Habits.WingMul(item); // (Вітряна підкова; the breed «Зоряний» at night; «Звички»)
        }

        // what its training adds (×)
        private static float TrainMul(ItemDrop.ItemData item) { return 1f + TrainPer * Trained(item); }

        // the levels of training past the first (a raven starts at 1: nothing yet)
        private static int Trained(ItemDrop.ItemData item) { return Mathf.Max(0, Strike.Level(item) - 1); }

        private static float HungerMul(ItemDrop.ItemData item)
        {
            if (!Trust.Active(item)) return 1f; // (a sleeping raven has no hunger yet)
            int z = Satiety.Zone(item);
            return z <= Satiety.Hungry ? HungryMul : z == Satiety.Stuffed ? StuffedMul : 1f;
        }

        // ---------------------------------------------------------------- the network

        // the worn ravens' pace now on your ZDO (written only when it changes)
        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            int v = Pack(hugin) | (Pack(munin) << 8) | (RuneStars.StormByte(hugin) << 16) | (RuneStars.StormByte(munin) << 24); // («Буря» in the two high bytes)
            if (z.GetInt(NetKey, 0) != v) z.Set(NetKey, v);
        }

        // «Буря» on this player's raven, as their game sent it (RuneStars.StormByte), 0 none
        internal static int StormOf(Player owner, bool munin)
        {
            ZDO z = owner != null ? NetFlag.Zdo(owner, false) : null;
            return z != null ? (z.GetInt(NetKey, 0) >> (munin ? 24 : 16)) & 0xFF : 0;
        }

        // a byte: (pace − 0.5) × 200, 0 for none
        private static int Pack(ItemDrop.ItemData item) { return item == null ? 0 : Mathf.Clamp(Mathf.RoundToInt((Now(item) - 0.5f) * 200f), 1, 255); }

        // this player's raven's pace now, as every game knows it (1: none known)
        internal static float Of(Player owner, bool munin)
        {
            ZDO z = owner != null ? NetFlag.Zdo(owner, false) : null;
            if (z == null) return 1f;
            int b = (z.GetInt(NetKey, 0) >> (munin ? 8 : 0)) & 0xFF;
            return b == 0 ? 1f : 0.5f + b / 200f;
        }

        // ---------------------------------------------------------------- the book

        private static readonly string[] Words = Lang.Keys("важкий", "неквапливий", "рівний", "прудкий", "стрімкий");

        // its own wing in a word, against its kind (the book's «Швидкість»)
        internal static string Word(ItemDrop.ItemData item)
        {
            float d = Share(item); // −1 … 1
            int i = d < -0.6f ? 0 : d < -0.2f ? 1 : d <= 0.2f ? 2 : d <= 0.6f ? 3 : 4;
            return Lang.T(Words[i]);
        }

        // its own pace with its knack and training, before hunger (what it flies when fed)
        internal static float Own(ItemDrop.ItemData item)
        {
            Norov.Knack k = Norov.KnackOf(item);
            return Base(item) * (k != null && k.Id == "quick" ? QuickMul : 1f) * TrainMul(item);
        }

        // its speed in the air now, m/s as shown (its kind's top × its pace now)
        internal static string Speed(bool munin, ItemDrop.ItemData item) { return (Mathf.Round(RavenFlight.For(munin).Top * Now(item) * Storm(munin, item) * 10f) / 10f).ToString("0.#"); }

        // a spark of «Буря» on the worn raven: faster on every flight (RuneStars.FlyMul, put on its style by the motor)
        private static float Storm(bool munin, ItemDrop.ItemData item) { return item != null && item == Trust.Worn(munin) ? RuneStars.FlyMul(Player.m_localPlayer, munin, false) : 1f; }

        // the book's «Швидкість»: what it is now and what makes it so
        internal static string Tip(ItemDrop.ItemData item, bool munin)
        {
            float top = RavenFlight.For(munin).Top;
            int own = Mathf.RoundToInt(Base(item) * 100f);
            string s = Lang.F("<b>Швидкість · {0} м/с</b> <color=#8a7a66>({1})</color>\n\n", Speed(munin, item), Word(item));
            s += Lang.F("{0} літає до {1} м/с.", munin ? Lang.T("Звичайний Мунін") : Lang.T("Звичайний Хугін"), top.ToString("0.#"));
            s += "\n" + Lang.F("Від природи {1}: {0}% звичайної швидкості.", own, Word(item));
            Norov.Knack k = Norov.KnackOf(item);
            if (k != null && k.Id == "quick") s += "\n" + Lang.F("<color=#2f5a2a>Хист «{0}»: +{1}%.</color>", k.Name(munin), Mathf.RoundToInt((QuickMul - 1f) * 100f));
            int up = Trained(item);
            if (up > 0) s += "\n" + Lang.F("<color=#2f5a2a>Вишкіл {0}: +{1}%.</color>", up + 1, (TrainPer * up * 100f).ToString("0.#"));
            if (GearFx.WingMul(item) > 1f) s += "\n" + Lang.F("<color=#2f5a2a>«Вітряна підкова»: +{0}%.</color>", Mathf.RoundToInt((GearFx.WingMul(item) - 1f) * 100f));
            float storm = Storm(munin, item);
            if (storm > 1f) s += "\n" + Lang.F("<color=#2f5a2a>Іскра «Бурі»: +{0}%.</color>", Mathf.RoundToInt((storm - 1f) * 100f));
            float h = HungerMul(item);
            if (h < 1f) s += "\n" + Lang.F("<color=#8E1E1E>{0}: −{1}%, поки не нагодуєш як слід.</color>", Lang.UpperFirst(Satiety.ZoneWord(item)), Mathf.RoundToInt((1f - h) * 100f));
            s += "\n\n" + Lang.T("З цією швидкістю він летить усюди — до цілі й назад, в ударі, на розвідці, у догонялках. Як він повертає, вирішує його характер.");
            return s;
        }
    }
}
#endif

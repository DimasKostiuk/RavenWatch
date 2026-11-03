#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // Each raven's own ways on your arm (the user, 2026-10-29: «кожен птах ще різноманітніший»): ten in all (a stroke and
    // eye to eye among them), each raven opens five, one at every trust from 3 to 7, in an order of its own (by its id: the
    // same raven always the same, another raven others). Nothing to win from them: the look of it and its character.
    // On J: the second row, dropped by the arm row's «Звички» button (ArmPerch.ActTrick + slot), only the open ones.
    // Now and then, sitting on your arm, it does one by itself — any of the ten, open or not (more often with trust).
    internal static class ArmTricks
    {
        internal const int Scratch = 0, Blow = 1, Peck = 2, World = 3, Other = 4, Wings = 5, Swing = 6, Paw = 7, Pet = 8, Eyes = 9, Count = 10;
        internal const int Slots = 5, FirstTrust = 3;

        internal static string[] Names { get { return Lang.Arr(NamesSrc); } } // (translated on every read)
        private static readonly string[] NamesSrc = Lang.Keys("Почухати під дзьобом", "Подути на пір'я", "Дати дзьобнути палець", "Показати світ", "На іншу руку", "Розправити крила", "Погойдати", "Дай лапу", "Погладити", "Очі в очі");
        internal static string[] Desc { get { return Lang.Arr(DescSrc); } }
        private static readonly string[] DescSrc = Lang.Keys(
            "Чухаєш його пальцем під дзьобом, а він заплющує очі й тягне шию.",
            "Підносиш ближче й дмухаєш на пір'я: воно стає дибки, і ворон обтрушується.",
            "Підставляєш палець під дзьоб. Як дзьобне — залежить від ворона.",
            "Піднімаєш руку й повільно повертаєшся довкола: ворон роздивляється місцину й каже, що про неї думає.",
            "Ворон перепурхує на другу руку й лишається там. Ще раз — назад.",
            "Піднімаєш руку, і ворон розкриває крила, хизуючись.",
            "Легенько гойдаєш рукою, як вітер гілку, а ворон тримає рівновагу.",
            "Ворон піднімає лапу, а ти торкаєшся її пальцем.",
            "Другою рукою гладиш ворона на руці.",
            "Підносиш ворона до обличчя, і він дивиться тобі в очі й щось тобі каже.");
        internal static readonly string[] Icons = { "o-scratch", "o-blow", "o-peck", "o-world", "o-swap", "o-wings", "o-swing", "o-paw", "o-pet", "o-eyes" };
        private static readonly string[] Ids = { "scratch", "blow", "peck", "world", "other", "wings", "swing", "paw", "pet", "eyes" };

        // this raven's five, in the order they open (a stable hash of its id: the same in every game and session)
        private static readonly System.Collections.Generic.Dictionary<string, int[]> OrderCache = new System.Collections.Generic.Dictionary<string, int[]>();
        internal static int[] Order(ItemDrop.ItemData item, bool munin)
        {
            string id = item != null ? Backup.Id(item) : null;
            int[] own;
            string key = (id ?? "") + (munin ? "m" : "h");
            if (OrderCache.TryGetValue(key, out own)) return own; // (asked a few times a second by the circle: worked out once)
            uint h = 2166136261u;
            foreach (char c in (id ?? "") + (munin ? "m" : "h")) { h ^= c; h *= 16777619u; }
            int[] all = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            for (int i = all.Length - 1; i > 0; i--)
            {
                h ^= h << 13; h ^= h >> 17; h ^= h << 5; // (xorshift: the shuffle's own dice)
                int j = (int)(h % (uint)(i + 1));
                int t = all[i]; all[i] = all[j]; all[j] = t;
            }
            own = new int[Slots];
            System.Array.Copy(all, own, Slots);
            OrderCache[key] = own;
            return own;
        }

        // how many it has opened by now (one at each trust from FirstTrust)
        internal static int Open(ItemDrop.ItemData item) { return item == null || !Trust.Active(item) ? 0 : Mathf.Clamp(Trust.Level(item) - FirstTrust + 1, 0, Slots); }
        internal static int TrustOfSlot(int slot) { return FirstTrust + slot; }

        // the trick in this slot of its row (−1: not open yet)
        internal static int At(ItemDrop.ItemData item, bool munin, int slot) { return slot >= 0 && slot < Open(item) ? Order(item, munin)[slot] : -1; }

        // for the book: what it does on your arm (null: nothing yet)
        internal static string Known(ItemDrop.ItemData item, bool munin)
        {
            int n = Open(item);
            if (n <= 0) return null;
            int[] o = Order(item, munin);
            string[] names = Names;
            string s = "";
            for (int i = 0; i < n; i++) s += (i > 0 ? ", " : "") + names[o[i]];
            return s;
        }

        // a new one opened: said once (the count told kept in the raven), the newest only (a raven that had trust
        // already before this came opens all its share at once, quietly but for the last)
        private const string ToldKey = "RavenWatch_armTricksTold";
        internal static bool Announce(Player p, bool munin, ItemDrop.ItemData item)
        {
            int n = Open(item), told = 0;
            string v;
            if (item.m_customData != null && item.m_customData.TryGetValue(ToldKey, out v)) int.TryParse(v, out told);
            if (n <= told) return false;
            if (!PerkFx.Say(munin, "arm_new_" + Ids[Order(item, munin)[n - 1]], 1f, 0f)) return false;
            if (item.m_customData == null) item.m_customData = new System.Collections.Generic.Dictionary<string, string>();
            item.m_customData[ToldKey] = n.ToString();
            return true;
        }

        // the trick itself, the bird on your arm (ArmPerch: put there first if it was not); yours (not its own whim): a
        // point of trust, ten a day (Trust.OnArm)
        internal static void Do(Player p, bool munin, int trick, bool yours)
        {
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            bool sits = life != null && life.SitNew;
            switch (trick)
            {
                case Scratch:
                    if (!ArmPerch.Gesture(p, munin, "arm_scratch_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Підставляє шию", 0.6f);
                    break;
                case Blow:
                    if (!ArmPerch.Gesture(p, munin, "arm_blow_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Струшується", 1.2f);
                    break;
                case Peck:
                    if (!ArmPerch.Gesture(p, munin, munin ? "arm_peckhard_" : "arm_peck_", Gestures.Pri.Answer)) return; // (Munin pecks for real)
                    if (sits) life.SitDirectorNow.React(life, "Штурхає", 0.8f);
                    break;
                case World:
                    if (!ArmPerch.Gesture(p, munin, "arm_world_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Косить оком", 1f);
                    break;
                case Other:
                    if (!ArmPerch.SwapArm(p, munin)) return;
                    break;
                case Wings:
                    if (!ArmPerch.Gesture(p, munin, "arm_wings_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Розправляє крило", 0.5f);
                    if (life != null) life.Flap(1.8f); // (held up high: room for the whole spread, beat a while)
                    break;
                case Swing:
                    if (!ArmPerch.Gesture(p, munin, "arm_swing_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Тримається", 0.3f);
                    break;
                case Paw:
                    if (!ArmPerch.Gesture(p, munin, "arm_paw_", Gestures.Pri.Answer)) return;
                    if (sits) life.SitDirectorNow.React(life, "Підібрав лапу", 0.5f);
                    break;
                case Pet: case Eyes: // (they say their own words)
                    if (ArmPerch.Core(p, munin, trick == Pet ? ArmPerch.ActPet : ArmPerch.ActEyes, yours) && yours) Trust.OnArm(munin);
                    return;
                default: return;
            }
            PerkFx.Say(munin, "arm_t_" + Ids[trick], 0.6f, 1.5f);
            if (yours) Trust.OnArm(munin);
        }
    }
}
#endif

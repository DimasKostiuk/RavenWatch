#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // Things got by your own hand for the running errand (LootStep): the item carries the errand's stamp in its
    // custom data. While that errand runs, such a thing is «live»: a raven mark on its slot, a line in its tooltip (ItemMarks: ErrandMarkKind), it
    // never stacks with the same things without the mark (picked up, dragged, put in a chest), and the raven takes
    // them first when you hand in. The errand over: the stamp means nothing (and is taken off the bag's things).
    internal static class ErrandMark
    {
        internal const string Key = "RavenWatch_errandItem";

        // the running errand's stamp ("" none); asked very often (every piece of a stack being added): once a frame
        private static string _stamp = "";
        private static int _frame = -1;
        internal static string Stamp
        {
            get
            {
                if (_frame == Time.frameCount) return _stamp;
                _frame = Time.frameCount;
                ErrandRun r = Player.m_localPlayer != null ? Errands.Of(Player.m_localPlayer) : null;
                _stamp = r != null && r.Def != null ? Of(r) : "";
                return _stamp;
            }
        }

        internal static string Of(ErrandRun r) { return r.Id + "@" + r.Given.ToString("0", CultureInfo.InvariantCulture); }

        internal static bool Live(ItemDrop.ItemData it)
        {
            if (it == null || it.m_customData == null || it.m_customData.Count == 0) return false;
            string v, s = Stamp;
            return s.Length > 0 && it.m_customData.TryGetValue(Key, out v) && v == s;
        }

        internal static void Put(ItemDrop.ItemData it)
        {
            string s = Stamp;
            if (it != null && s.Length > 0) it.m_customData[Key] = s;
        }

        // the errand ended: the marks off the bag's things (they stack again)
        internal static void Clear(Player p)
        {
            if (p == null || p.GetInventory() == null) return;
            bool any = false;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_customData != null && it.m_customData.Remove(Key)) any = true;
            _frame = -1;
            if (any) Errands.Log("errand marks taken off the bag's things");
        }

        // handed in: the marked ones first, then any of that name
        internal static void Take(Player p, string prefab, int n)
        {
            Inventory inv = p.GetInventory();
            string name = Trust.FindName(p, prefab);
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (n <= 0) break;
                if (it.m_shared.m_name != name || !Live(it)) continue;
                int take = Mathf.Min(n, it.m_stack);
                inv.RemoveItem(it, take);
                n -= take;
            }
            if (n > 0) inv.RemoveItem(name, n, -1, false);
        }

        // the thing being added to an inventory right now (its stacks are looked for)
        [ThreadStatic] internal static ItemDrop.ItemData Adding;
        internal static bool Any { get { return Stamp.Length > 0; } }
    }

    // ---------------------------------------------------------------- stacks: live with live, plain with plain

    [HarmonyPatch(typeof(ItemDrop.ItemData), "IsSameType")]
    internal static class ErrandMarkSameType
    {
        private static void Postfix(ItemDrop.ItemData __instance, ItemDrop.ItemData other, ref bool __result)
        {
            if (__result && ErrandMark.Any && ErrandMark.Live(__instance) != ErrandMark.Live(other)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData) })]
    internal static class ErrandMarkAdd
    {
        private static void Prefix(ItemDrop.ItemData item, out ItemDrop.ItemData __state) { __state = ErrandMark.Adding; ErrandMark.Adding = item; }
        private static Exception Finalizer(Exception __exception, ItemDrop.ItemData __state) { ErrandMark.Adding = __state; return __exception; }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
    internal static class ErrandMarkAddAt
    {
        private static void Prefix(ItemDrop.ItemData item, out ItemDrop.ItemData __state) { __state = ErrandMark.Adding; ErrandMark.Adding = item; }
        private static Exception Finalizer(Exception __exception, ItemDrop.ItemData __state) { ErrandMark.Adding = __state; return __exception; }
    }

    [HarmonyPatch(typeof(Inventory), "CanAddItem", new[] { typeof(ItemDrop.ItemData), typeof(int) })]
    internal static class ErrandMarkCanAdd
    {
        private static void Prefix(ItemDrop.ItemData item, out ItemDrop.ItemData __state) { __state = ErrandMark.Adding; ErrandMark.Adding = item; }
        private static Exception Finalizer(Exception __exception, ItemDrop.ItemData __state) { ErrandMark.Adding = __state; return __exception; }
    }

    // (while no errand runs nothing is live: the game's own search)
    [HarmonyPatch(typeof(Inventory), "FindFreeStackItem")]
    internal static class ErrandMarkFreeStack
    {
        private static bool Prefix(List<ItemDrop.ItemData> ___m_inventory, string name, int quality, float worldLevel, ref ItemDrop.ItemData __result)
        {
            if (!ErrandMark.Any) return true;
            bool live = ErrandMark.Live(ErrandMark.Adding);
            __result = null;
            foreach (ItemDrop.ItemData it in ___m_inventory)
                if (it.m_shared.m_name == name && it.m_quality == quality && it.m_stack < it.m_shared.m_maxStackSize && it.m_worldLevel == worldLevel && ErrandMark.Live(it) == live)
                { __result = it; break; }
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), "FindFreeStackSpace")]
    internal static class ErrandMarkFreeSpace
    {
        private static bool Prefix(List<ItemDrop.ItemData> ___m_inventory, string name, float worldLevel, ref int __result)
        {
            if (!ErrandMark.Any) return true;
            bool live = ErrandMark.Live(ErrandMark.Adding);
            int n = 0;
            foreach (ItemDrop.ItemData it in ___m_inventory)
                if (it.m_shared.m_name == name && it.m_stack < it.m_shared.m_maxStackSize && it.m_worldLevel == worldLevel && ErrandMark.Live(it) == live)
                    n += it.m_shared.m_maxStackSize - it.m_stack;
            __result = n;
            return false;
        }
    }

    // ---------------------------------------------------------------- what you see: its kind of mark (ItemMarks — the slot's raven, the tooltip's line)
    internal static class ErrandMarkKind
    {
        internal static void Marks()
        {
            ItemMarks.Kinds.Add(new ItemMarks.Kind
            {
                Name = "errand",
                Any = () => ErrandMark.Any,
                Icon = it => { if (!ErrandMark.Live(it)) return null; ErrandRun r = Errands.Of(Player.m_localPlayer); return r != null ? RavenItem.Icon(r.Munin) : null; },
                Line = it =>
                {
                    if (!ErrandMark.Live(it)) return null;
                    ErrandRun r = Errands.Of(Player.m_localPlayer);
                    return r == null || r.Def == null ? null : "<color=#E8C04A>" + Lang.F("Добуто для доручення Одіна «{0}»", Lang.T(r.Def.Name)) + "</color>";
                }
            });
        }
    }
}
#endif

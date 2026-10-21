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
    // custom data. While that errand runs, such a thing is «live»: a raven mark on its slot, a line in its tooltip, it
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

    // ---------------------------------------------------------------- what you see

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class ErrandMarkTooltip
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            try
            {
                if (!ErrandMark.Live(item)) return;
                ErrandRun r = Errands.Of(Player.m_localPlayer);
                if (r == null || r.Def == null) return;
                __result += "\n\n<color=#E8C04A>" + Lang.F("Добуто для доручення Одіна «{0}»", Lang.T(r.Def.Name)) + "</color>";
            }
            catch (Exception e) { PatchGuard.Fail("ErrandMarkTooltip", e); }
        }
    }

    // a small raven in the slot's corner (the errand's raven); hidden on every other slot
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class ErrandMarkSlots
    {
        private const string Name = "RavenWatch_ErrandMark";
        private static readonly Dictionary<InventoryGrid, int> Shown = new Dictionary<InventoryGrid, int>(); // grids with marks up
        private static readonly List<bool> Want = new List<bool>();

        private static void Postfix(InventoryGrid __instance, Inventory ___m_inventory, List<InventoryElement> ___m_elements)
        {
            try
            {
                bool any = ErrandMark.Any;
                int was;
                Shown.TryGetValue(__instance, out was);
                if (!any && was == 0) return; // (nothing up, nothing to show: most frames)
                if (___m_inventory == null || ___m_elements == null) return;
                int width = ___m_inventory.GetWidth();
                Want.Clear();
                for (int i = 0; i < ___m_elements.Count; i++) Want.Add(false);
                if (any)
                    foreach (ItemDrop.ItemData it in ___m_inventory.GetAllItems())
                    {
                        int idx = it.m_gridPos.y * width + it.m_gridPos.x;
                        if (idx >= 0 && idx < Want.Count && ErrandMark.Live(it)) Want[idx] = true;
                    }
                int up = 0;
                ErrandRun r = any ? Errands.Of(Player.m_localPlayer) : null;
                Sprite icon = r != null ? RavenItem.Icon(r.Munin) : null;
                for (int i = 0; i < ___m_elements.Count; i++)
                {
                    InventoryElement el = ___m_elements[i];
                    if (el == null) continue;
                    Transform t = el.transform.Find(Name);
                    if (!Want[i]) { if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                    if (t == null) t = Make(el);
                    Image img = t.GetComponent<Image>();
                    if (img.sprite != icon) img.sprite = icon;
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    up++;
                }
                if (up > 0) Shown[__instance] = up; else Shown.Remove(__instance);
                if (Shown.Count > 16) Shown.Clear(); // (grids of closed chests)
            }
            catch (Exception e) { PatchGuard.Fail("ErrandMarkSlots", e); }
        }

        private static Transform Make(InventoryElement el)
        {
            GameObject g = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(el.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // the top left corner
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(2f, -2f);
            rt.sizeDelta = new Vector2(22f, 22f);
            Image img = g.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            return rt;
        }
    }
}
#endif

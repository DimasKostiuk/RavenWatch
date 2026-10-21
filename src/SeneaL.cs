#if RAVEN_ITEM
using System;
using System.Reflection;
using HarmonyLib;

namespace HuginWhispers
{
    // SeneaL UI writes an item's kind under its name in the tooltip, by the slot it goes to: a raven (no slot of the game)
    // would get a plain kind. With SeneaL UI there, a raven is called a raven instead. Patched once the game
    // starts (when every mod is loaded, whatever their order); without SeneaL UI nothing happens.
    internal static class SeneaL
    {
        private static bool _tried;

        internal static void Patch()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type tips = AccessTools.TypeByName("SeneaLUI.Inv.Tips");
                MethodInfo category = tips != null ? AccessTools.Method(tips, "Category", new Type[] { typeof(ItemDrop.ItemData) }) : null;
                if (category == null) return;
                new Harmony(Plugin.Guid + ".seneal").Patch(category, postfix: new HarmonyMethod(typeof(SeneaL), "CategoryPostfix"));
                if (Plugin.Instance != null) Plugin.Instance.Log("SeneaL UI: ravens are called ravens in the tooltip");
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("SeneaL UI tooltip kind failed: " + e.Message); }
        }

        private static void CategoryPostfix(ItemDrop.ItemData item, ref string __result)
        {
            bool munin;
            if (RavenItem.IsRaven(item, out munin)) __result = munin ? Lang.T("Ворон пам'яті") : Lang.T("Ворон думки");
        }
    }
}
#endif

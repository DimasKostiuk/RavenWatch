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
                if (category != null)
                {
                    new Harmony(Plugin.Guid + ".seneal").Patch(category, postfix: new HarmonyMethod(typeof(SeneaL), "CategoryPostfix"));
                    if (Plugin.Instance != null) Plugin.Instance.Log("SeneaL UI: ravens are called ravens in the tooltip");
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("SeneaL UI tooltip kind failed: " + e.Message); }
            // its recipe materials have their own hover (CraftView.ChipTip): a good Haldor sells says so in its amber note
            try
            {
                Type craft = AccessTools.TypeByName("SeneaLUI.Inv.CraftView");
                Type tip = AccessTools.TypeByName("SeneaLUI.Components.Tip");
                MethodInfo chip = craft != null ? AccessTools.Method(craft, "ChipTip", new Type[] { typeof(int) }) : null;
                TipNote = tip != null ? AccessTools.Field(tip, "Note") : null;
                TipKey = tip != null ? AccessTools.Field(tip, "VneiKey") : null;
                TipBody = tip != null ? AccessTools.Field(tip, "Body") : null;
                if (chip == null || TipNote == null || TipKey == null) return;
                Harmony h = new Harmony(Plugin.Guid + ".seneal");
                h.Patch(chip, postfix: new HarmonyMethod(typeof(SeneaL), "ChipTipPostfix"));
                // and on the item's own hover (its words lose their colour there): the same amber note
                Type tips = AccessTools.TypeByName("SeneaLUI.Inv.Tips");
                MethodInfo parse = tips != null ? AccessTools.Method(tips, "Parse", new Type[] { tip, typeof(string), typeof(ItemDrop.ItemData) }) : null;
                if (parse != null) h.Patch(parse, postfix: new HarmonyMethod(typeof(SeneaL), "ParsePostfix"));
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("SeneaL UI material hover failed: " + e.Message); }
        }

        private static FieldInfo TipNote, TipKey, TipBody;

        private static void ChipTipPostfix(object __result)
        {
            if (__result == null) return;
            string sold = WsItems.SoldNote(TipKey.GetValue(__result) as string);
            if (sold != null) TipNote.SetValue(__result, sold);
        }

        private static void ParsePostfix(object t, ItemDrop.ItemData item)
        {
            if (t == null || item == null || item.m_shared == null) return;
            string words, sold = WsItems.SoldNote(item.m_shared.m_name, out words);
            if (sold == null) return;
            // SeneaL takes the first line of the words for the body: that is the gold line now, so the body gets the
            // item's own words back and the gold line goes to the amber note (unless it has its own, e.g. a cheated item's)
            string body = TipBody != null ? TipBody.GetValue(t) as string : null;
            if (body == sold && !string.IsNullOrEmpty(words)) TipBody.SetValue(t, words);
            if (string.IsNullOrEmpty(TipNote.GetValue(t) as string)) TipNote.SetValue(t, sold);
        }

        private static void CategoryPostfix(ItemDrop.ItemData item, ref string __result)
        {
            bool munin;
            if (RavenItem.IsRaven(item, out munin)) __result = munin ? Lang.T("Ворон пам'яті") : Lang.T("Ворон думки");
        }
    }
}
#endif

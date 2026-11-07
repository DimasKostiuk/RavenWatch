#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // What the game does to a piece of a place, in one file: chests, stations, beds, comfort, stands, fires, doors, mending, the game's raven.
    // a chest of a place that keeps them shut (Loc.LockChests): nobody opens it, the author neither (his own is open)
    [HarmonyPatch(typeof(Container), "Interact")]
    internal static class LocChestPatch
    {
        // (quietly, and no «[E] open» on it either: just a chest standing there — the user 2026-11-01)
        internal static bool Shut(Container c)
        {
            ZNetView nv = c != null ? c.GetComponentInParent<ZNetView>() : null;
            if (!Blueprint.Copy(nv)) return false;
            Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
            return l != null && l.LockChests;
        }

        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (!Shut(__instance)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), "GetHoverText")]
    internal static class LocChestHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result) { if (LocChestPatch.Shut(__instance)) __result = ""; }
    }

    [HarmonyPatch(typeof(Container), "GetHoverName")]
    internal static class LocChestNamePatch
    {
        private static void Postfix(Container __instance, ref string __result) { if (LocChestPatch.Shut(__instance)) __result = ""; }
    }

    // a guide point of a piece a blueprint put into the world — or of the author's own building it is saved from (the user 2026-11-04:
    // Hugin sat on the cartography table of his test Hall): out of the game's raven's list as soon as it comes in
    [HarmonyPatch(typeof(GuidePoint), "Start")]
    internal static class LocGuidePatch
    {
        private static void Postfix(GuidePoint __instance)
        {
            try { if (Blueprint.Copy(__instance.GetComponentInParent<ZNetView>()) || Locs.InAuthorCopy(__instance.transform.position)) Raven.UnregisterStaticText(__instance.m_text); }
            catch (System.Exception e) { PatchGuard.Fail("LocGuidePatch", e); }
        }
    }

    // a workbench of a place that keeps no stations (Loc.LockStations): no crafting window, no «[E]» on it — for anyone
    [HarmonyPatch(typeof(CraftingStation), "Interact")]
    internal static class LocStationPatch
    {
        internal static bool Dead(CraftingStation s)
        {
            ZNetView nv = s != null ? s.GetComponentInParent<ZNetView>() : null;
            if (!Blueprint.Copy(nv)) return false;
            Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
            return l != null && l.LockStations;
        }

        private static bool Prefix(CraftingStation __instance, ref bool __result)
        {
            try { if (!Dead(__instance)) return true; }
            catch (System.Exception e) { PatchGuard.Fail("LocStationPatch", e); return true; }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "GetHoverText")]
    internal static class LocStationHoverPatch
    {
        private static void Postfix(CraftingStation __instance, ref string __result)
        {
            try { if (LocStationPatch.Dead(__instance)) __result = ""; }
            catch (System.Exception e) { PatchGuard.Fail("LocStationHoverPatch", e); }
        }
    }

    // a bed of a place that keeps them empty (Loc.LockBeds): no sleep, no spawn point — for anyone
    [HarmonyPatch(typeof(Bed), "Interact")]
    internal static class LocBedPatch
    {
        private static bool Prefix(Bed __instance, Humanoid human, ref bool __result)
        {
            ZNetView nv = __instance.GetComponentInParent<ZNetView>();
            if (!Blueprint.Copy(nv)) return true;
            Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
            if (l == null || !l.LockBeds) return true;
            if (human == Player.m_localPlayer) human.Message(MessageHud.MessageType.Center, Lang.T("Це ліжко не для тебе: тут спав хтось інший."));
            __result = false;
            return false;
        }
    }

    // a hidden piece gives no comfort (a rug of the lived-in house in its ruin): the game counts every comfort piece in reach
    [HarmonyPatch(typeof(Piece), "GetAllComfortPiecesInRadius")]
    internal static class LocComfortPatch
    {
        private static void Postfix(List<Piece> pieces)
        {
            try { if (LocState.AnyHidden && pieces != null) pieces.RemoveAll(LocState.IsHidden); }
            catch (System.Exception e) { PatchGuard.Fail("LocComfortPatch", e); }
        }
    }

    // the thing an item stand holds, made by the stand after the stand was hidden: hidden at once (no waiting, no looking)
    [HarmonyPatch(typeof(ItemStand), "SetVisualItem")]
    internal static class LocStandItemPatch
    {
        private static void Postfix(ItemStand __instance) { LocState.StandMade(__instance.GetComponent<ZNetView>()); }
    }

    // a fire of the Hall kept out (LocState.Lights): not burning, whatever its fuel
    [HarmonyPatch(typeof(Fireplace), "IsBurning")]
    internal static class LocFirePatch
    {
        private static void Postfix(Fireplace __instance, ref bool __result)
        {
            int id = __instance.GetInstanceID();
            if (LocState.Lit.Contains(id)) { __result = true; return; } // (burning for you as the story says: fuel or not)
            if (!__result) return;
            if (LocState.Unlit.Contains(id) || LocPreview.Unlit.Contains(id)) __result = false;
        }
    }

    // a place's great door does not open till the story opens it, nor doors locked for you (LocDoors)
    [HarmonyPatch(typeof(Door), "Interact")]
    internal static class LocDoorPatch
    {
        private static bool Prefix(Door __instance, Humanoid character, ref bool __result)
        {
            string why = LocState.Locked(__instance);
            if (why == null)
            {
                LocDoorView view = character == Player.m_localPlayer ? __instance.GetComponent<LocDoorView>() : null;
                if (view == null || !view.Open()) return true;
                __result = true; // (open in the world, only drawn shut for him: now it shows that)
                return false;
            }
            if (character != null) character.Message(MessageHud.MessageType.Center, Lang.T(why));
            __result = false;
            return false;
        }
    }

    // the Hall is not mended by a hammer, piece by piece: the story mends it whole
    [HarmonyPatch(typeof(WearNTear), "Repair")]
    internal static class LocRepairPatch
    {
        private static float _told;
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            if (!BlueprintWearPatch.IsMarked(__instance) || Perches.StoryAuthor) return true; // (the author mends his own Hall as he builds it)
            if (Player.m_localPlayer != null && Time.time - _told > 2f) { _told = Time.time; Player.m_localPlayer.Message(MessageHud.MessageType.Center, Lang.T("Цього так не полагодиш: воно належить цьому місцю.")); }
            __result = false;
            return false;
        }
    }
}
#endif

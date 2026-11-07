#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // No raid of the game's in the story's places (the user 2026-11-04: «why can there be a raid in the Hall; in the core, for all of
    // them, unless it is the story's own»). The game picks where a raid comes in one place, RandEventSystem.GetValidEventPoints (the
    // spots where its players stand; both its random raids and those on their own clock ask it): a spot in a place of the story, or
    // within RaidClear m of it, is taken off the list, on the server, where the game asks. A place with no spot left: no raid there,
    // the roll goes to whoever else may have it. A raid of the story's own (RandEventSystem.SetRandomEventByName) does not ask.
    // The author's and a player's call of a raid by another way asks LocNoRaid.At first (OmenFate).
    internal static class LocNoRaid
    {
        internal static float RaidClear = 40f; // m round a place's own reach: a raid's men come in round the spot it starts on

        internal static bool At(Vector3 point)
        {
            foreach (Loc l in Locs.All)
            {
                Vector3 at; Quaternion rot;
                if (!l.Where(out at, out rot)) continue;
                float far = Mathf.Max(l.NoBuild, l.Reach()) + RaidClear;
                if (Utils.DistanceXZ(point, at) < far) return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(RandEventSystem), "GetValidEventPoints")]
    internal static class LocNoRaidPatch
    {
        private static void Postfix(List<Vector3> __result)
        {
            try
            {
                if (__result == null || __result.Count == 0) return;
                int was = __result.Count;
                __result.RemoveAll(LocNoRaid.At);
                if (__result.Count != was && Plugin.Instance != null) Plugin.Instance.Log("Raid: " + (was - __result.Count) + " spot(s) in a place of the story not offered to it");
            }
            catch (Exception e) { PatchGuard.Fail("LocNoRaidPatch", e); }
        }
    }
}
#endif

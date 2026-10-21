#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The game's own birds (gulls, crows: RandomFlyingBird), kept as they come and go, for Munin's hunt (Outing.Hunt).
    // Only the ones every game knows (a ZNetView), so all see the same chase.
    internal static class WorldBirds
    {
        internal static float Range = 40f;
        internal static readonly List<RandomFlyingBird> All = new List<RandomFlyingBird>();

        // the nearest such bird within Range of you, in the open
        internal static bool Near(Player p, out ZDOID id)
        {
            id = ZDOID.None;
            All.RemoveAll(b => b == null);
            float best = Range * Range;
            foreach (RandomFlyingBird b in All)
            {
                if (!b.gameObject.activeInHierarchy) continue;
                float d = (b.transform.position - p.transform.position).sqrMagnitude;
                if (d >= best) continue;
                ZNetView nv = b.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                best = d; id = nv.GetZDO().m_uid;
            }
            return id != ZDOID.None;
        }
    }

    // (Awake, or Start if the bird has none; neither: off, quietly)
    [HarmonyPatch]
    internal static class WorldBirdsPatch
    {
        private static System.Reflection.MethodBase TargetMethod() { return AccessTools.Method(typeof(RandomFlyingBird), "Awake") ?? AccessTools.Method(typeof(RandomFlyingBird), "Start"); }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Postfix(RandomFlyingBird __instance) { if (__instance != null && !WorldBirds.All.Contains(__instance)) WorldBirds.All.Add(__instance); }
    }
}
#endif

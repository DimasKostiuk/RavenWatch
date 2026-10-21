using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The comfort as the game itself counts it, for the ravens. Path of Valheiman's postfix on this very method adds the
    // LOCAL player's tree bonuses to every call (flat comfort, a torch, a fire, a linked ally's gift): a perch measured by
    // whoever holds it would get that player's tree, and the fire's strength for Hugin would grow without end. Our postfix
    // runs before every other one and keeps the value the game made; the game's own result stays as the mods make it.
    internal static class RawComfort
    {
        private static bool _asking;
        private static int _seen;
        private const int None = int.MinValue;

        internal static int At(bool roof, Vector3 pos)
        {
            _asking = true; _seen = None;
            int got;
            try { got = SE_Rested.CalculateComfortLevel(roof, pos); }
            finally { _asking = false; }
            return _seen != None ? _seen : got;
        }

        // the player's own, without the mods' bonuses (the game counts it the same way, from where they stand)
        internal static int Of(Player p) { return p != null ? At(p.InShelter(), p.transform.position) : 0; }

        internal static void Seen(int value) { if (_asking) _seen = value; }
    }

    [HarmonyPatch(typeof(SE_Rested), "CalculateComfortLevel", new System.Type[] { typeof(bool), typeof(Vector3) })]
    internal static class RawComfortPatch
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore("treextr.pathofvalheiman")]
        private static void Postfix(int __result) { RawComfort.Seen(__result); }
    }
}

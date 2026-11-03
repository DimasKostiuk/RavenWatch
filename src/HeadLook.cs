#if RAVEN_ITEM
using System;
using HarmonyLib;

namespace HuginWhispers
{
    // The game's look-at (the head after the camera, the others' after their look data) gives way while one of our clips
    // moves the head (BodyAnimator.HeadFree): its weight eased off on a smooth curve, and back after.
    [HarmonyPatch(typeof(CharacterAnimEvent), "UpdateLookat")]
    internal static class HeadLookPatch
    {
        private static void Prefix(CharacterAnimEvent __instance, Character ___m_character, ref float __state)
        {
            __state = -1f;
            try
            {
                BodyAnimator a = BodyAnimator.Of(___m_character); // (a player or a person: the same clips)
                if (a == null || a.HeadFree <= 0.001f) return;
                __state = __instance.m_lookWeight;
                float f = a.HeadFree;
                __instance.m_lookWeight = __state * (1f - f * f * (3f - 2f * f));
            }
            catch (Exception e) { PatchGuard.Fail("HeadLookPatch", e); }
        }

        private static void Postfix(CharacterAnimEvent __instance, float __state) { if (__state >= 0f) __instance.m_lookWeight = __state; }
    }
}
#endif

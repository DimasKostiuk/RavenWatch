#if RAVEN_ITEM
using System;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens on your shoulder in your fight (2026-10-22): your swing makes them duck, a blow you land makes Munin
    // cheer, a blow on your shield jolts them both, a parry has them triumph. Your own game only: their acts travel as
    // any sitting act does (SitDirector.Publish).
    internal static class Combat
    {
        internal const string DuckName = "Пригинається", CheerName = "Підбадьорює", JoltName = "Здригається", TriumphName = "Тріумфує";
        // chance a raven reacts (Hugin, Munin)
        internal static float DuckH = 0.55f, DuckM = 0.3f, CheerH = 0f, CheerM = 0.35f, JoltH = 1f, JoltM = 1f, TriumphH = 0.7f, TriumphM = 0.8f;

        internal static float SwingAt = -10f; // your last swing: its own shove of your shoulder is ducked, not beaten against (ShoulderLife)
        internal static void Swing() { SwingAt = Time.time; Both(DuckName, DuckH, DuckM, 0.08f); }
        internal static float DrankAt = -1000f; // your last mead (its voice goes a little tipsy: RavenVoice)
        internal static void Drank() { DrankAt = Time.time; Both("Кривиться", 0.5f, 0.75f, 0.5f); }
        internal static void Landed() { Both(CheerName, CheerH, CheerM, 0.15f); }
        internal static void Blocked(bool parry) { if (parry) Both(TriumphName, TriumphH, TriumphM, 0.12f); else Both(JoltName, JoltH, JoltM, 0.05f); }

        private static void Both(string template, float h, float m, float lag)
        {
            One(false, template, h, lag);
            One(true, template, m, lag);
        }
        private static void One(bool munin, string template, float chance, float lag)
        {
            if (chance <= 0f || UnityEngine.Random.value > chance) return;
            ShoulderLife life = Shoulder.LocalLife(munin);
            if (life == null || !life.SitNew) return;
            life.SitDirectorNow.React(life, template, UnityEngine.Random.Range(0f, lag) + (munin ? 0f : 0.04f));
            if (Studio.On && Plugin.Instance != null) Plugin.Instance.Log("Combat: " + (munin ? "Munin " : "Hugin ") + template);
        }
    }

    // your swing
    [HarmonyPatch(typeof(Humanoid), "StartAttack")]
    internal static class CombatSwingPatch
    {
        private static void Postfix(Humanoid __instance, bool __result)
        {
            try { if (__result && ReferenceEquals(__instance, Player.m_localPlayer)) Combat.Swing(); }
            catch (Exception e) { PatchGuard.Fail("CombatSwingPatch", e); }
        }
    }

    // a blow you land (on something alive that is not a player)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class CombatLandedPatch
    {
        private static float _last;
        private static void Postfix(Character __instance, HitData hit)
        {
            try
            {
                Player me = Player.m_localPlayer;
                if (me == null || hit == null || __instance == null || __instance.IsPlayer() || Time.time - _last < 0.4f) return;
                if (hit.m_attacker != me.GetZDOID() || hit.m_skillRaiseAmount <= Strike.Marker || !Marks.RealBlow(hit)) return; // (yours: not a raven's blow, not a bleed or burn ticking)
                _last = Time.time;
                Combat.Landed();
            }
            catch (Exception e) { PatchGuard.Fail("CombatLandedPatch", e); }
        }
    }

    // a blow on your shield; a parry (the game's own timed block, within a quarter second)
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class CombatBlockPatch
    {
        private static readonly System.Reflection.FieldInfo BlockTimer = AccessTools.Field(typeof(Humanoid), "m_blockTimer");
        private static readonly System.Reflection.MethodInfo Blocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");

        private static void Prefix(Humanoid __instance, ref bool __state)
        {
            __state = false;
            try
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;
                ItemDrop.ItemData b = (ItemDrop.ItemData)Blocker.Invoke(__instance, null);
                float t = (float)BlockTimer.GetValue(__instance);
                __state = b != null && b.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
            }
            catch (Exception e) { PatchGuard.Fail("CombatBlockPatch", e); }
        }

        private static void Postfix(Humanoid __instance, bool __result, bool __state)
        {
            try { if (__result && ReferenceEquals(__instance, Player.m_localPlayer)) Combat.Blocked(__state); }
            catch (Exception e) { PatchGuard.Fail("CombatBlockPatch", e); }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Where Munin's newer strength comes from: a nest broken, a raid outlasted, a perfect block, mead, a trophy.
    internal static class MuninDeeds
    {
        // a raid you stood through: inside its area when it ends, after at least a minute of it
        private static readonly FieldInfo ActiveEvent = AccessTools.Field(typeof(RandEventSystem), "m_activeEvent");
        private static readonly MethodInfo InsideArea = AccessTools.Method(typeof(RandEventSystem), "IsInsideRandomEventArea");
        private static object _raid;
        private static float _raidSince;

        internal static void Tick(Player p)
        {
            if (p == null || RandEventSystem.instance == null || ActiveEvent == null) return;
            RandomEvent ev = ActiveEvent.GetValue(RandEventSystem.instance) as RandomEvent;
            bool inside = ev != null && !p.IsDead() && Inside(ev, p.transform.position);
            if (ev != null && inside)
            {
                if (!ReferenceEquals(_raid, ev)) { _raid = ev; _raidSince = Time.time; }
                return;
            }
            if (ev == null && _raid != null)
            {
                if (!p.IsDead() && Time.time - _raidSince >= 60f) { Power.OnRaid(); Nicks.OnRaid(p); }
                _raid = null;
            }
            else if (ev != null && !inside) _raid = null; // walked away from it: it is not yours to win
        }

        private static bool Inside(RandomEvent ev, Vector3 at)
        {
            try
            {
                if (InsideArea != null)
                {
                    ParameterInfo[] ps = InsideArea.GetParameters();
                    if (ps.Length == 2) return (bool)InsideArea.Invoke(InsideArea.IsStatic ? null : RandEventSystem.instance, new object[] { ev, at });
                }
            }
            catch { }
            return Vector3.Distance(at, ev.m_pos) < 96f;
        }
    }

    // a nest, a spawner pile, a totem with spawns: broken by you (or beside you)
    [HarmonyPatch]
    internal static class NestDestroyedPatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo m in typeof(Destructible).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "Destroy" || m.DeclaringType != typeof(Destructible)) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(HitData)) return m;
            }
            return null;
        }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Prefix(Destructible __instance, HitData hit)
        {
            Player p = Player.m_localPlayer;
            if (p == null || __instance == null || __instance.GetComponent<SpawnArea>() == null) return;
            Character by = hit != null ? hit.GetAttacker() : null;
            if (by == p || (by == null && Vector3.Distance(p.transform.position, __instance.transform.position) < 15f)) Power.OnNest();
        }
    }

    // a perfect block: the block came within the shield's timed window
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class ParryPatch
    {
        private static readonly FieldInfo BlockTimer = AccessTools.Field(typeof(Humanoid), "m_blockTimer");
        private static readonly MethodInfo Blocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");
        private static void Prefix(Humanoid __instance, out bool __state)
        { __state = default(bool); try {
            __state = false;
            if (__instance != Player.m_localPlayer || BlockTimer == null || Blocker == null) return;
            ItemDrop.ItemData shield = Blocker.Invoke(__instance, null) as ItemDrop.ItemData;
            float t = (float)BlockTimer.GetValue(__instance);
            __state = shield != null && shield.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
        } catch (System.Exception e) { PatchGuard.Fail("ParryPatch", e); } }
        private static void Postfix(Character attacker, bool __result, bool __state)
        { try {
            if (__state && __result && attacker != null && !(attacker is Player)) { HuginChores.OnParry(); Diary.Add(Ev.Parry, "", 0); }
        } catch (System.Exception e) { PatchGuard.Fail("ParryPatch", e); } }
    }

    // a mead drunk (the game's meads are all "Mead…")
    [HarmonyPatch(typeof(Player), "ConsumeItem")]
    internal static class MeadPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (__result && __instance == Player.m_localPlayer && item != null && item.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith("Mead")) { HuginChores.OnMead(); Nicks.OnMead(__instance); }
        }
    }

    // a trophy picked up
    [HarmonyPatch]
    internal static class TrophyPickupPatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo m in typeof(Humanoid).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "Pickup" || m.DeclaringType != typeof(Humanoid) || m.ReturnType != typeof(bool)) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(GameObject)) return m;
            }
            return null;
        }
        private static bool Prepare() { return TargetMethod() != null; }
        private static void Prefix(GameObject go, out bool __state)
        {
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            __state = drop != null && drop.m_itemData != null && drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }
        private static void Postfix(Humanoid __instance, bool __result, bool __state)
        {
            if (__state && __result && __instance == Player.m_localPlayer) { HuginChores.OnTrophy(); Diary.Add(Ev.Trophy, "", 0); }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Where Munin's newer strength comes from: a nest broken, a raid outlasted, a perfect block, mead, a trophy,
    // meat eaten, a monster killed in a base, a kill in harsh weather, a kill from a ship's deck.
    internal static class MuninDeeds
    {
        internal static float SeaMul = 2f;
        private static readonly string[] MeatWords = { "Meat", "Jerky", "Sausage", "BloodPudding", "Chicken", "LoxPie", "SerpentStew", "Misthare", "NeckTail" };

        internal static bool OnShip(Player p) { return p != null && (p.GetStandingOnShip() != null || p.GetControlledShip() != null); }

        // a meat dish (the game's names: CookedMeat, BoarJerky, Sausages, BloodPudding, LoxPie…), not raw meat
        internal static bool Meat(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null || item.m_shared.m_food <= 0f) return false;
            string n = item.m_dropPrefab.name;
            foreach (string w in MeatWords) if (n.IndexOf(w, System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        internal static int BaseValueNeeded = 3; // the game's own base count (raids want 3 too): a lone campfire is no base

        // a kill of yours (own or told by the game that held it): a monster in a base, a kill in harsh weather
        internal static void OnKill(Player p, string prefab, bool tamed)
        {
            if (p == null || tamed || !Monster(prefab)) return;
            if (p.GetBaseValue() >= BaseValueNeeded) HuginChores.OnDefended();
            if (Harsh(p)) HuginChores.OnHarshKill();
        }

        // not a deer or a hare (an unknown prefab counts as a monster)
        internal static bool Monster(string prefab)
        {
            GameObject go = !string.IsNullOrEmpty(prefab) && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            Character c = go != null ? go.GetComponent<Character>() : null;
            return c == null || c.m_faction != Character.Faction.AnimalsVeg;
        }

        // a storm or a blizzard, freezing, or the Ashlands and the Deep North themselves
        // (not «Cold»: any night away from a fire gives it, and the night has its own bonus)
        private static bool Harsh(Player p)
        {
            Heightmap.Biome b = p.GetCurrentBiome();
            if (b == Heightmap.Biome.AshLands || b == Heightmap.Biome.DeepNorth) return true;
            SEMan se = p.GetSEMan();
            if (se != null && se.HaveStatusEffect(SEMan.s_statusEffectFreezing)) return true;
            EnvSetup e = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            string n = e != null ? e.m_name ?? "" : "";
            return n.IndexOf("storm", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("thunder", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // a raid you stood through: inside its area when it ends, after at least a minute of it, and you or your raven
        // struck a monster in it (waiting it out beside the fight is not holding it)
        private static readonly FieldInfo ActiveEvent = AccessTools.Field(typeof(RandEventSystem), "m_activeEvent");
        private static readonly MethodInfo InsideArea = AccessTools.Method(typeof(RandEventSystem), "IsInsideRandomEventArea");
        private static object _raid;
        private static float _raidSince;
        private static bool _insideFailed;

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
                if (!p.IsDead() && Time.time - _raidSince >= 60f && Senses.HitMonsterAt >= _raidSince) { Power.OnRaid(); Nicks.OnRaid(p); }
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
            catch (System.Exception e) { if (!_insideFailed && Plugin.Instance != null) Plugin.Instance.Log("Raid area check failed, going by distance: " + e.Message); _insideFailed = true; }
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

    // a mead drunk (the game's meads are all "Mead…"), or a meat dish eaten
    [HarmonyPatch(typeof(Player), "ConsumeItem")]
    internal static class MeadPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer || item == null || item.m_dropPrefab == null) return;
            if (item.m_dropPrefab.name.StartsWith("Mead")) { HuginChores.OnMead(); Nicks.OnMead(__instance); }
            else if (MuninDeeds.Meat(item)) HuginChores.OnMeat();
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
        private static string _prefab;
        private static double _fresh; // (the world time it fell off a kill; 0: not from a kill)
        private static void Prefix(GameObject go, out bool __state)
        {
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            __state = drop != null && drop.m_itemData != null && drop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
            _prefab = null; _fresh = 0;
            if (!__state) return;
            _prefab = Utils.GetPrefabName(go);
#if RAVEN_ITEM
            _fresh = FreshTrophy.Of(go);
#endif
        }
        private static void Postfix(Humanoid __instance, bool __result, bool __state)
        {
            if (!__state || !__result || __instance != Player.m_localPlayer) return;
            HuginChores.OnTrophy();
            Diary.Add(Ev.Trophy, "", 0);
#if RAVEN_ITEM
            if (_fresh > 0) try { Errands.OnFreshTrophy(_prefab, _fresh); } catch (System.Exception e) { PatchGuard.Fail("TrophyPickupPatch fresh", e); }
#endif
        }
    }

#if RAVEN_ITEM
    // A trophy that fell off a kill (the game's own loot drop, on the game that owns the dead): marked with the world
    // time on its ground item, so the one who picks it up knows it is fresh — not one taken off a stand or dropped
    // from a bag (Odin's errand «Трофеї на стіну» wants heads won, not the old wall hung again).
    [HarmonyPatch(typeof(CharacterDrop), "DropItems")]
    internal static class FreshTrophy
    {
        private const string Key = "RavenWatch_freshAt";
        private static readonly FieldInfo All = AccessTools.Field(typeof(ItemDrop), "s_instances");

        internal static double Of(GameObject go)
        {
            ZNetView nv = go != null ? go.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return 0;
            float at = nv.GetZDO().GetFloat(Key, 0f);
            return at > 0f ? at : 0;
        }

        private static void Prefix(out int __state)
        {
            System.Collections.IList l = All != null ? All.GetValue(null) as System.Collections.IList : null;
            __state = l != null ? l.Count : -1;
        }

        private static void Postfix(int __state)
        {
            if (__state < 0) return;
            try
            {
                System.Collections.IList l = All.GetValue(null) as System.Collections.IList;
                for (int i = __state; l != null && i < l.Count; i++)
                {
                    ItemDrop d = l[i] as ItemDrop;
                    if (d == null || d.m_itemData == null || d.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy) continue;
                    ZNetView nv = d.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid()) nv.GetZDO().Set(Key, Mathf.Max(1f, (float)WorldTime.Now));
                }
            }
            catch (System.Exception e) { PatchGuard.Fail("FreshTrophy", e); }
        }
    }
#endif
}
#endif

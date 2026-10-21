#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A place of the world (a crypt, a cave, a camp) and the creatures its spawners hold. The game keeps it in the
    // spawners' ZDOs: a spawner that has woken is linked to its creature (ZDO connection «Spawned»), and the link stays
    // after the creature dies (its ZDO gone). That is the world's own record, so worlds older than the mod are read the
    // same way.
    //   Left  (in the place, with real spawners): how many enemies are still there to kill
    //   Fought (on the server, ZDOs only): someone has already killed there; errands do not send to it again
    internal static class PlaceClear
    {
        internal const float Outdoor = 40f; // m round the place above ground that belong to it
        internal const float Reach = 70f;   // m past the step's ring that still count as in (a dungeon below fills its zone)
        private static readonly FieldInfo SpawnersField = AccessTools.Field(typeof(CreatureSpawner), "m_creatureSpawners");
        private static readonly FieldInfo ById = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");

        // a dungeon's rooms are 5000 m up, spread over the place's whole zone; above ground: near the place
        internal static bool In(Vector3 place, Vector3 at)
        {
            if (at.y > 3000f) return ZoneSystem.GetZone(at).Equals(ZoneSystem.GetZone(place));
            float dx = at.x - place.x, dz = at.z - place.z;
            return dx * dx + dz * dz <= Outdoor * Outdoor;
        }

        private static bool Spawned(ZDO z)
        {
            ZDOConnection c = z.GetConnection();
            return c != null && c.m_type == ZDOExtraData.ConnectionType.Spawned;
        }

        private static bool Alive(ZDO z)
        {
            ZDOID id = z.GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned);
            return !id.IsNone() && ZDOMan.instance.GetZDO(id) != null;
        }

        // ---------------------------------------------------------------- in the place (the clear step)
        // enemies still to kill there: woken ones alive, and those that will wake as you go in (those that come back by
        // themselves too, until their first creature is dead; a workbench beside them does not excuse them). Not asked:
        // ones whose group already has its creatures, ones the world's keys or the time of day keep asleep. -1: the place is not loaded yet (nothing known). No spawners at all: the hostile
        // creatures standing there. (Every 2 s while you are in the place: one list kept.)
        private static readonly List<CreatureSpawner> Here = new List<CreatureSpawner>();
        internal static int Left(Vector3 place, out int seen)
        {
            seen = 0;
            if (ZNetScene.instance == null || ZDOMan.instance == null || !ZNetScene.instance.IsAreaReady(place)) return -1;
            List<CreatureSpawner> all = SpawnersField != null ? SpawnersField.GetValue(null) as List<CreatureSpawner> : null;
            List<CreatureSpawner> here = Here;
            here.Clear();
            if (all != null) foreach (CreatureSpawner s in all) if (s != null && In(place, s.transform.position)) here.Add(s);
            int left = 0;
            foreach (CreatureSpawner s in here)
            {
                ZNetView nv = s.GetComponent<ZNetView>();
                ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
                if (z == null) continue;
                seen++;
                if (Spawned(z)) { if (Alive(z)) left++; continue; }
                if (!Excused(s, here)) left++;
            }
            here.Clear();
            if (seen == 0) left = Monsters(place);
            return left;
        }

        private static bool Excused(CreatureSpawner s, List<CreatureSpawner> here)
        {
            if (!s.m_spawnAtDay && EnvMan.IsDay() || !s.m_spawnAtNight && EnvMan.IsNight()) return true;
            if (ZoneSystem.instance != null)
            {
                if (!string.IsNullOrEmpty(s.m_blockingGlobalKey) && ZoneSystem.instance.GetGlobalKey(s.m_blockingGlobalKey)) return true;
                if (!string.IsNullOrEmpty(s.m_requiredGlobalKey) && !ZoneSystem.instance.GetGlobalKey(s.m_requiredGlobalKey)) return true;
            }
            if (s.m_spawnGroupRadius > 0f && s.m_maxGroupSpawned >= 1 && GroupWoke(s, here) >= s.m_maxGroupSpawned) return true;
            return false;
        }

        // the spawners of its group as the game joins them (same group id, rings touching, hop by hop: CreatureSpawner.
        // CheckGroupSpawnBlocked) that have ever woken
        private static readonly List<CreatureSpawner> Open = new List<CreatureSpawner>(), Left2 = new List<CreatureSpawner>();
        private static int GroupWoke(CreatureSpawner s, List<CreatureSpawner> here)
        {
            Open.Clear(); Left2.Clear();
            foreach (CreatureSpawner o in here) if (o != s && o.m_spawnGroupID == s.m_spawnGroupID) Left2.Add(o);
            Open.Add(s);
            int woke = 0;
            while (Open.Count > 0)
            {
                CreatureSpawner c = Open[Open.Count - 1];
                Open.RemoveAt(Open.Count - 1);
                for (int i = Left2.Count - 1; i >= 0; i--)
                {
                    CreatureSpawner o = Left2[i];
                    if (Vector3.Distance(c.transform.position, o.transform.position) > c.m_spawnGroupRadius + o.m_spawnGroupRadius) continue;
                    Left2.RemoveAt(i);
                    Open.Add(o);
                    ZNetView nv = o.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid() && Spawned(nv.GetZDO())) woke++;
                }
            }
            Open.Clear(); Left2.Clear();
            return woke;
        }

        private static int Monsters(Vector3 place)
        {
            int n = 0;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.GetFaction() == Character.Faction.Players) continue;
                if (c.GetComponent<MonsterAI>() == null || !In(place, c.transform.position)) continue; // (not a deer passing by)
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- on the server (where errands send)
        // the spawners of the world by zone: one pass over every ZDO, kept a few seconds (one ask looks at many places).
        // Not longer: a destroyed ZDO goes back to the game's pool and comes again as another object (checked on use too)
        private static Dictionary<Vector2s, List<ZDO>> _byZone;
        private static float _byZoneAt = -999f;
        private static HashSet<int> _kinds;

        private static Dictionary<Vector2s, List<ZDO>> ByZone()
        {
            if (_byZone != null && Time.time - _byZoneAt < 10f) return _byZone;
            _byZoneAt = Time.time;
            _byZone = new Dictionary<Vector2s, List<ZDO>>();
            if (ZNetScene.instance == null || ZDOMan.instance == null || ById == null) return _byZone;
            if (_kinds == null)
            {
                _kinds = new HashSet<int>();
                foreach (GameObject g in ZNetScene.instance.m_prefabs) if (g != null && g.GetComponent<CreatureSpawner>() != null) _kinds.Add(g.name.GetStableHashCode());
            }
            Dictionary<ZDOID, ZDO> all = ById.GetValue(ZDOMan.instance) as Dictionary<ZDOID, ZDO>;
            if (all == null) return _byZone;
            foreach (ZDO z in all.Values)
            {
                if (z == null || !_kinds.Contains(z.GetPrefab())) continue;
                Vector2s zone = ZoneSystem.GetZone(z.GetPosition());
                List<ZDO> l;
                if (!_byZone.TryGetValue(zone, out l)) _byZone[zone] = l = new List<ZDO>();
                l.Add(z);
            }
            return _byZone;
        }

        // someone killed what woke there (two of its creatures, or one of a place with three spawners at most)
        internal static bool Fought(Vector3 place)
        {
            Dictionary<Vector2s, List<ZDO>> idx = ByZone();
            if (idx.Count == 0) return false;
            int dead = 0, total = 0;
            HashSet<Vector2s> done = new HashSet<Vector2s>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector2s zone = ZoneSystem.GetZone(place + new Vector3(dx * 64f, 0f, dz * 64f));
                    List<ZDO> l;
                    if (!done.Add(zone) || !idx.TryGetValue(zone, out l)) continue;
                    foreach (ZDO z in l)
                    {
                        if (!z.IsValid() || !_kinds.Contains(z.GetPrefab()) || !In(place, z.GetPosition())) continue;
                        total++;
                        if (Spawned(z) && !Alive(z)) dead++;
                    }
                }
            return dead >= 2 || dead >= 1 && total <= 3;
        }
    }

    // ---------------------------------------------------------------- what errands see in the world

    // things of the world you hit that an errand wants broken (DestroyStep): watched a few seconds for their ZDO to go
    // (broken where it is simulated, maybe another PC; whoever's blow was the last, you were in it)
    internal static class DestroyWatch
    {
        private const float Keep = 15f; // s after your last blow
        private struct Hit { public string Prefab; public float At; }
        private static readonly Dictionary<ZDOID, Hit> Hits = new Dictionary<ZDOID, Hit>();
        private static readonly List<ZDOID> Done = new List<ZDOID>();

        internal static void Struck(ZDOID id, string prefab)
        {
            if (Hits.Count > 32) Hits.Clear();
            Hits[id] = new Hit { Prefab = prefab, At = Time.time };
        }

        // twice a second (Errands.Tick)
        internal static void Tick()
        {
            if (Hits.Count == 0 || ZDOMan.instance == null) return;
            Done.Clear();
            foreach (KeyValuePair<ZDOID, Hit> kv in Hits)
            {
                if (ZDOMan.instance.GetZDO(kv.Key) == null) { Done.Add(kv.Key); Errands.OnDestroyed(kv.Value.Prefab);
#if RAVEN_STORY
                    StoryBus.Raise("destroyed", kv.Value.Prefab, 0);
#endif
                }
                else if (Time.time - kv.Value.At > Keep) Done.Add(kv.Key);
            }
            foreach (ZDOID id in Done) Hits.Remove(id);
            Done.Clear();
        }
    }

    [HarmonyPatch(typeof(Destructible), "Damage")]
    internal static class DestroyHitPatch
    {
        private static void Prefix(Destructible __instance, HitData hit)
        {
            try
            {
                if (hit == null || Player.m_localPlayer == null || hit.m_attacker != Player.m_localPlayer.GetZDOID()) return;
                string prefab = Utils.GetPrefabName(__instance.gameObject);
                if (!Errands.DestroyWants(prefab)) return;
                ZNetView nv = __instance.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) DestroyWatch.Struck(nv.GetZDO().m_uid, prefab);
            }
            catch (Exception e) { PatchGuard.Fail("DestroyHitPatch", e); }
        }
    }

    // a runestone read (its words shown on E)
    [HarmonyPatch(typeof(RuneStone), "Interact")]
    internal static class RuneReadPatch
    {
        private static void Postfix(RuneStone __instance, Humanoid character, bool hold)
        {
            try { if (!hold && Mine.Me(character)) Errands.OnRead(__instance.transform.position); }
            catch (Exception e) { PatchGuard.Fail("RuneReadPatch", e); }
#if RAVEN_STORY
            try { if (!hold && Mine.Me(character)) StoryBus.Raise("read", "", 0, __instance.transform.position); }
            catch (Exception e) { Story.Log("read: " + e.Message); }
#endif
        }
    }

    // fuel put into a fire, a torch, a brazier (by E or dragged from the bag): one deed per piece, its prefab the arg
    [HarmonyPatch(typeof(Fireplace), "Interact")]
    internal static class FuelPatch
    {
        internal static int Count(Fireplace f, Humanoid user)
        {
            if (!Mine.Me(user) || f == null || f.m_fuelItem == null || user.GetInventory() == null) return -1;
            return user.GetInventory().CountItems(f.m_fuelItem.m_itemData.m_shared.m_name);
        }

        internal static void Added(Fireplace f, Humanoid user, int before)
        {
            if (before < 0) return;
            int added = before - Mathf.Max(0, Count(f, user));
            for (int n = 0; n < added; n++) Diary.Add(Ev.Fuel, f.m_fuelItem.gameObject.name, 0);
        }

        private static void Prefix(Fireplace __instance, Humanoid user, out int __state) { __state = Count(__instance, user); }
        private static void Postfix(Fireplace __instance, Humanoid user, int __state) { try { Added(__instance, user, __state); } catch (Exception e) { PatchGuard.Fail("FuelPatch", e); } }
    }

    [HarmonyPatch(typeof(Fireplace), "UseItem")]
    internal static class FuelUsePatch
    {
        private static void Prefix(Fireplace __instance, Humanoid user, out int __state) { __state = FuelPatch.Count(__instance, user); }
        private static void Postfix(Fireplace __instance, Humanoid user, int __state) { try { FuelPatch.Added(__instance, user, __state); } catch (Exception e) { PatchGuard.Fail("FuelUsePatch", e); } }
    }

    // a thing thrown down by a player (from the bag, out of a broken chest): marked, so picking it up is not «got by
    // your own hand» (LootStep)
    [HarmonyPatch(typeof(ItemDrop), "DropItem", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(Vector3), typeof(Quaternion) })]
    internal static class LootMark
    {
        internal const string Key = "RavenWatch_thrown";
        private static void Postfix(ItemDrop __result)
        {
            try
            {
                ZNetView nv = __result != null ? __result.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid()) nv.GetZDO().Set(Key, true);
            }
            catch (Exception e) { PatchGuard.Fail("LootMark", e); }
        }
    }

    // a thing taken down from an item stand or an armour stand comes back with the data kept in the stand: it was
    // someone's, not new loot (else one wolf pelt hung on and dropped again would count for ever)
    [HarmonyPatch(typeof(ItemDrop), "LoadFromExternalZDO")]
    internal static class LootMarkStand
    {
        private static void Postfix(ItemDrop __instance)
        {
            try
            {
                ZNetView nv = __instance != null ? __instance.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid()) nv.GetZDO().Set(LootMark.Key, true);
            }
            catch (Exception e) { PatchGuard.Fail("LootMarkStand", e); }
        }
    }

    // a lying stack takes in the same things lying round it (once, when it lands): when one of them was thrown by a
    // player, the whole stack counts as thrown (else 50 thrown pieces would ride into a creature's drop of one)
    [HarmonyPatch(typeof(ItemDrop), "AutoStackItems")]
    internal static class LootMarkStack
    {
        private static int _mask;
        private static readonly Collider[] Hits = new Collider[32];

        private static void Prefix(ItemDrop __instance, bool ___m_haveAutoStacked, ZNetView ___m_nview, out int __state)
        {
            __state = -1;
            try
            {
                if (___m_haveAutoStacked || ___m_nview == null || !___m_nview.IsValid() || ___m_nview.GetZDO().GetBool(LootMark.Key)) return;
                ItemDrop.ItemData d = __instance.m_itemData;
                if (d == null || d.m_shared.m_maxStackSize <= 1 || d.m_stack >= d.m_shared.m_maxStackSize) return;
                if (_mask == 0) _mask = LayerMask.GetMask("item");
                int n = Physics.OverlapSphereNonAlloc(__instance.transform.position, 4f, Hits, _mask);
                for (int i = 0; i < n; i++)
                {
                    Rigidbody rb = Hits[i] != null ? Hits[i].attachedRigidbody : null;
                    ItemDrop o = rb != null ? rb.GetComponent<ItemDrop>() : null;
                    if (o == null || o == __instance || o.m_itemData == null || o.m_itemData.m_shared.m_name != d.m_shared.m_name) continue;
                    ZNetView nv = o.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid() && nv.GetZDO().GetBool(LootMark.Key)) { __state = d.m_stack; break; }
                }
            }
            catch (Exception e) { PatchGuard.Fail("LootMarkStack", e); }
        }

        private static void Postfix(ItemDrop __instance, ZNetView ___m_nview, int __state)
        {
            if (__state < 0 || __instance.m_itemData == null || __instance.m_itemData.m_stack <= __state) return;
            try { if (___m_nview != null && ___m_nview.IsValid()) ___m_nview.GetZDO().Set(LootMark.Key, true); }
            catch (Exception e) { PatchGuard.Fail("LootMarkStack", e); }
        }
    }

    // picked up from the world by you: counted for an errand that wants it (only while one does)
    [HarmonyPatch(typeof(Humanoid), "Pickup")]
    internal static class LootPickupPatch
    {
        internal struct Was { public string Prefab, Name; public int Count; }

        private static void Prefix(Humanoid __instance, GameObject go, out Was __state)
        {
            __state = default(Was);
            try
            {
                if (go == null || !Mine.Me(__instance) || !Errands.WantsLoot) return;
                ItemDrop d = go.GetComponent<ItemDrop>();
                ZNetView nv = go.GetComponent<ZNetView>();
                if (d == null || d.m_itemData == null || nv == null || !nv.IsValid() || nv.GetZDO().GetBool(LootMark.Key)) return;
                string prefab = d.m_itemData.m_dropPrefab != null ? d.m_itemData.m_dropPrefab.name : go.name.Replace("(Clone)", "");
                if (!Errands.LootWants(prefab)) return;
                ErrandMark.Put(d.m_itemData); // (marked before it goes in: it finds only marked stacks)
                __state = new Was { Prefab = prefab, Name = d.m_itemData.m_shared.m_name, Count = __instance.GetInventory().CountItems(d.m_itemData.m_shared.m_name) };
            }
            catch (Exception e) { PatchGuard.Fail("LootPickupPatch", e); }
        }

        // (by what the bag gained, not by the answer: a bag too full takes part of a stack and still says false)
        private static void Postfix(Humanoid __instance, Was __state)
        {
            if (__state.Prefab == null) return;
            try
            {
                int gained = __instance.GetInventory().CountItems(__state.Name) - __state.Count;
                if (gained > 0) Errands.OnGain(__state.Prefab, gained);
#if RAVEN_STORY
                if (gained > 0) StoryBus.Raise("loot", __state.Prefab, gained);
#endif
            }
            catch (Exception e) { PatchGuard.Fail("LootPickupPatch", e); }
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The story's people (story-plan.md, «NPC»). Two bodies, one prefab each, made once from the game's own:
    //   RavenWatch_npc_human — the game's Player with the player's scripts taken out and a Humanoid put in their place
    //                          (the way VanillaNPC does it): walks, runs, wears gear, holds things, can fight later;
    //                          our own mind on it (NpcMind, a BaseAI of ours: goes, follows, looks).
    //   RavenWatch_npc_odin  — the game's «odin» without the script that makes him vanish when you come near.
    // Who it is (NpcDef) is a word in its ZDO, so one prefab serves every person. Everyone sees the same NPC (a networked
    // object); its mind runs where it is owned, and whoever spawns it owns it.
    internal static class Npcs
    {
        internal const string HumanPrefab = "RavenWatch_npc_human", OdinPrefab = "RavenWatch_npc_odin";
        internal static readonly int KId = "RavenWatch_npc".GetStableHashCode();      // string: the NpcDef id
        internal static readonly int KBy = "RavenWatch_npcBy".GetStableHashCode();    // long: whose story it serves (0: the world's)
        internal static readonly int KTag = "RavenWatch_npcTag".GetStableHashCode();  // string: its name in a scene ("" none)

        private static GameObject _holder, _human, _odin;
        internal static string NextId = "";  // the spawner's word for the NPC being made (its Awake reads it before the ZDO has one)

        private static readonly HashSet<string> PlayerOnly = new HashSet<string> { "PlayerController", "Talker", "Skills", "Player" };

        // ---------------------------------------------------------------- the prefabs (ZNetScene.Awake)
        internal static void Register(ZNetScene scene)
        {
            if (scene == null) return;
            if (_holder == null)
            {
                _holder = new GameObject("RavenWatch_Npcs");
                _holder.SetActive(false); // (the prefabs inside never wake)
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            if (_human == null) { try { _human = BuildHuman(scene); } catch (Exception e) { Log("human body: " + e); } }
            if (_odin == null) { try { _odin = BuildOdin(scene); } catch (Exception e) { Log("odin body: " + e); } }
            Add(scene, _human, HumanPrefab);
            Add(scene, _odin, OdinPrefab);
        }

        private static void Add(ZNetScene scene, GameObject go, string name)
        {
            if (go == null) return;
            if (!scene.m_prefabs.Contains(go)) scene.m_prefabs.Add(go);
            Dictionary<int, GameObject> named = Traverse.Create(scene).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
            if (named != null) named[name.GetStableHashCode()] = go;
        }

        private static GameObject BuildHuman(ZNetScene scene)
        {
            GameObject player = scene.GetPrefab("Player");
            if (player == null) { Log("the game's Player prefab not found: no human NPCs"); return null; }
            GameObject go = UnityEngine.Object.Instantiate(player, _holder.transform, false);
            go.name = HumanPrefab;
            Player pl = go.GetComponent<Player>();
            if (pl == null) { Log("Player component missing on the copy"); UnityEngine.Object.Destroy(go); return null; }
            // the body's settings as the player has them (eyes, hands, speeds, effects…), before the Player goes
            List<KeyValuePair<FieldInfo, object>> keep = new List<KeyValuePair<FieldInfo, object>>();
            keep.AddRange(Serialized(pl, typeof(Character)));
            keep.AddRange(Serialized(pl, typeof(Humanoid)));
            for (int i = 0; i < 5; i++)
            {
                List<Component> drop = new List<Component>();
                foreach (Component c in go.GetComponents<Component>()) if (c != null && PlayerOnly.Contains(c.GetType().Name)) drop.Add(c);
                if (drop.Count == 0) break;
                foreach (Component c in drop) UnityEngine.Object.DestroyImmediate(c); // (one that others need goes on a later round)
            }
            foreach (Component c in go.GetComponents<Component>()) if (c != null && PlayerOnly.Contains(c.GetType().Name)) { Log("could not take out " + c.GetType().Name + ": no human NPCs"); UnityEngine.Object.Destroy(go); return null; }
            Humanoid h = go.AddComponent<Humanoid>();
            foreach (KeyValuePair<FieldInfo, object> kv in keep) { try { kv.Key.SetValue(h, kv.Value); } catch { } }
            h.m_name = "NPC";
            h.m_group = "ravenwatch_npc";
            h.m_faction = Character.Faction.Players;
            h.m_boss = false;
            h.m_health = 200f;
            h.m_defaultItems = new GameObject[0];
            h.m_randomWeapon = new GameObject[0];
            h.m_randomArmor = new GameObject[0];
            h.m_randomShield = new GameObject[0];
            h.m_randomSets = new Humanoid.ItemSet[0];
            h.m_randomItems = new Humanoid.RandomItem[0];
            NpcMind mind = go.AddComponent<NpcMind>();
            mind.m_pathAgentType = Pathfinding.AgentType.Humanoid;
            mind.m_viewRange = 30f;
            mind.m_viewAngle = 140f;
            mind.m_hearRange = 15f;
            mind.m_idleSoundChance = 0f;
            mind.m_randomMoveInterval = 9999f;
            mind.m_jumpInterval = 0f;
            mind.m_avoidWater = false;
            mind.m_avoidFire = true;
            mind.m_afraidOfFire = false;
            go.AddComponent<NpcActor>();
            ZNetView nv = go.GetComponent<ZNetView>();
            if (nv != null) nv.m_persistent = true; // (the world's people stay; a scene's own are taken away by their scene)
            int character = LayerMask.NameToLayer("character");
            if (go.layer != character && go.layer != LayerMask.NameToLayer("character_net")) go.layer = character;
            StringBuilder sb = new StringBuilder();
            foreach (Component c in go.GetComponents<Component>()) if (c != null) sb.Append(c.GetType().Name).Append(' ');
            Log("human body made: " + keep.Count + " fields from the player; " + sb.ToString().Trim());
            return go;
        }

        private static GameObject BuildOdin(ZNetScene scene)
        {
            GameObject odin = scene.GetPrefab("odin");
            Odin script = odin != null ? odin.GetComponent<Odin>() : null;
            if (script == null) { Log("the game's odin not found: no Odin NPC"); return null; }
            GameObject go = UnityEngine.Object.Instantiate(odin, _holder.transform, false);
            go.name = OdinPrefab;
            Odin copy = go.GetComponent<Odin>();
            NpcActor a = go.AddComponent<NpcActor>();
            a.m_vanish = script.m_despawn; // (his own puff of smoke, for coming and going)
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            ZNetView nv = go.GetComponent<ZNetView>();
            if (nv != null) nv.m_persistent = false;
            // something to point at (E): a capsule on the character layer when he has no solid collider of his own
            bool solid = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) solid = true;
            if (!solid)
            {
                CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 1f, 0f); cap.height = 2f; cap.radius = 0.4f;
                go.layer = LayerMask.NameToLayer("character");
            }
            StringBuilder sb = new StringBuilder();
            foreach (Component c in go.GetComponentsInChildren<Component>(true)) if (c != null) sb.Append(c.GetType().Name).Append(' ');
            Log("odin body made" + (solid ? "" : " (capsule added)") + ": " + sb.ToString().Trim());
            return go;
        }

        private static IEnumerable<KeyValuePair<FieldInfo, object>> Serialized(Component from, Type declaring)
        {
            foreach (FieldInfo f in declaring.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!f.IsInitOnly && !f.IsNotSerialized && (f.IsPublic || f.IsDefined(typeof(SerializeField), false)))
                    yield return new KeyValuePair<FieldInfo, object>(f, f.GetValue(from));
        }

        // ---------------------------------------------------------------- spawning
        // a person at a place, serving this player's story (by: 0 the world's); null when it cannot be made
        internal static NpcActor Spawn(string id, Vector3 at, Quaternion turn, long by, string tag)
        {
            NpcDef d = NpcDefs.Find(id);
            if (d == null) { Log("no npc «" + id + "»"); return null; }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(d.Odin ? OdinPrefab : HumanPrefab) : null;
            if (prefab == null) { Log("no body for «" + id + "» (" + (d.Odin ? OdinPrefab : HumanPrefab) + " not made)"); return null; }
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(at, out h)) at.y = Mathf.Max(at.y, h);
            NextId = id;
            GameObject go;
            try { go = UnityEngine.Object.Instantiate(prefab, at, turn); }
            finally { NextId = ""; }
            NpcActor a = go != null ? go.GetComponent<NpcActor>() : null;
            if (a == null) return null;
            ZDO z = a.Zdo;
            if (z != null) { z.Set(KBy, by); z.Set(KTag, tag ?? ""); z.Set(NpcRules.KHome, at); }
            a.Appear();
            Log("spawned " + id + (string.IsNullOrEmpty(tag) ? "" : " as «" + tag + "»") + " at " + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.z) + (by != 0 ? " for " + by : " (the world's)"));
            return a;
        }

        // the live ones (loaded near someone)
        internal static NpcActor Find(string idOrTag)
        {
            NpcActor best = null;
            float bd = float.MaxValue;
            Player p = Player.m_localPlayer;
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || (a.Id != idOrTag && a.Tag != idOrTag)) continue;
                float d = p != null ? Vector3.Distance(a.transform.position, p.transform.position) : 0f;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        internal static NpcActor Nearest(Vector3 at, float range)
        {
            NpcActor best = null;
            float bd = range;
            foreach (NpcActor a in NpcActor.Live) { if (a == null) continue; float d = Vector3.Distance(a.transform.position, at); if (d < bd) { bd = d; best = a; } }
            return best;
        }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Npc: " + s); }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven npc · list · spawn <id> [tag] · go <who> [run] · come <who> · follow <who> · stay <who> · look <who> · say <who> <text> · tp <who> · remove <who>|all · reload
        // <who>: an id or tag, or «near» (the nearest within 30 m)
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            NpcActor who = a.Length > 2 ? (a[2] == "near" ? Nearest(p.transform.position, 30f) : Find(a[2])) : null;
            Vector3 aim = Aim(p);
            switch (what)
            {
                case "list":
                    StringBuilder l = new StringBuilder("NPC (" + NpcDefs.All().Count + "):");
                    foreach (NpcDef d in NpcDefs.All()) l.Append("\n  ").Append(d.Id).Append(" — ").Append(d.Name).Append(" · ").Append(d.Body);
                    l.Append("\nЖиві поруч (" + NpcActor.Live.Count + "):");
                    foreach (NpcActor n in NpcActor.Live) if (n != null) l.Append("\n  ").Append(n.Describe(p));
                    return l.ToString();
                case "spawn":
                    if (a.Length < 3) return "raven npc spawn <id> [тег]";
                    Vector3 at = p.transform.position + p.transform.forward * 3f;
                    NpcActor s = Spawn(a[2], at, Quaternion.LookRotation(-p.transform.forward), p.GetPlayerID(), a.Length > 3 ? a[3] : "");
                    return s != null ? "З'явився: " + s.Describe(p) : "Не вийшло (див. лог «Npc:»).";
                case "reload": NpcDefs.Reload(); return "Перечитано: " + NpcDefs.All().Count + ".";
                case "removeall": int gone = 0; foreach (NpcActor x in NpcActor.Live.ToArray()) if (x != null) { x.Remove(); gone++; } return "Прибрано: " + gone + ".";
            }
            if (what.Length > 0 && who == null) return "Нема такого NPC поруч (raven npc list).";
            switch (what)
            {
                case "go": who.Order("go " + V(aim) + (a.Length > 3 && a[3] == "run" ? " run" : "")); return "Іде туди, куди ти дивишся.";
                case "come": who.Order("go " + V(p.transform.position) + " run"); return "Іде до тебе.";
                case "follow": who.Order("follow " + p.GetZDOID().UserID + ":" + p.GetZDOID().ID + " 3"); return "Іде за тобою.";
                case "stay": who.Order("stay"); return "Стоїть.";
                case "look": who.Order("look " + p.GetZDOID().UserID + ":" + p.GetZDOID().ID); return "Дивиться на тебе.";
                case "tp": who.Order("tp " + V(aim)); return "Переставлено.";
                case "say": who.Say(a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "…"); return "Сказав.";
                case "remove": who.Remove(); return "Пішов.";
                case "wander": who.Order("wander " + (a.Length > 3 ? a[3] : "8") + " " + V(aim)); return "Походжає там, куди ти дивишся.";
                case "sit": who.Order("sit " + V(aim)); return "Сідає (на лаву чи стілець біля того місця, куди ти дивишся, або на землю).";
                case "stand": who.Order("stand"); return "Встав.";
                case "emote": if (a.Length < 4) return "raven npc emote <хто> <wave|point|bow|sit|relax|cheer|nonono|thumbsup|…> [loop]"; who.Order("emote " + a[3] + (a.Length > 4 && a[4] == "loop" ? " loop" : "")); return "Жест: " + a[3] + ".";
                case "fight": who.Order("fight " + (a.Length > 3 ? a[3] : "defend")); return "Бій: " + (a.Length > 3 ? a[3] : "defend") + ".";
                case "attack":
                    Character foe = null; float fd = 15f;
                    foreach (Character c in Character.GetAllCharacters()) { if (c == null || c.IsPlayer() || c.IsDead() || c.GetComponent<NpcActor>() != null) continue; float d = Vector3.Distance(c.transform.position, aim); if (d < fd) { fd = d; foe = c; } }
                    if (foe == null) return "Нема ворога біля того місця, куди ти дивишся.";
                    who.Order("attack " + NpcMind.IdOf(foe.GetZDOID())); return "Атакує: " + foe.m_name + ".";
            }
            return "raven npc list | spawn <id> [тег] | go <хто> [run] | come | follow | stay | look | wander <хто> [r] | sit | stand | emote <хто> <жест> [loop] | fight <хто> off|defend|hunt | attack <хто> | say <хто> <текст> | tp | remove <хто> | removeall | reload  (хто: id, тег або near)";
        }

        // where you look, on the ground, up to 40 m
        private static Vector3 Aim(Player p)
        {
            Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : p.transform;
            RaycastHit hit;
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            if (Physics.Raycast(cam.position, cam.forward, out hit, 60f, mask)) return hit.point;
            return p.transform.position + p.transform.forward * 10f;
        }

        internal static string V(Vector3 v)
        {
            return v.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " + v.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " + v.z.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class NpcScenePatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            try { Npcs.Register(__instance); }
            catch (Exception e) { Npcs.Log("register: " + e); }
        }
    }

    // an NPC that is not mortal: blows do nothing
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class NpcDamagePatch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (NpcActor.Live.Count == 0) return true;
            try
            {
                if (__instance.GetComponent<NpcActor>() == null) return true;
                NpcActor.Hit(__instance, hit != null ? hit.GetAttacker() : null);
                return !NpcActor.Immortal(__instance);
            }
            catch { return true; }
        }
    }

    // no enemy health bar over a person who cannot be hurt
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    internal static class NpcHudPatch
    {
        private static void Postfix(Character c, ref bool __result)
        {
            if (__result && c != null && NpcActor.Live.Count > 0 && NpcActor.Immortal(c)) __result = false;
        }
    }
}
#endif

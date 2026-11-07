#if RAVEN_ITEM && RAVEN_NPC
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

        private static GameObject _holder, _human, _odin, _locker;
        internal static string NextId = "";  // the spawner's word for the NPC being made (its Awake reads it before the ZDO has one)

        private static readonly HashSet<string> PlayerOnly = new HashSet<string> { "PlayerController", "Talker", "Skills", "Player" };

        // ---------------------------------------------------------------- the prefabs (ZNetScene.Awake)
        internal static void Register(ZNetScene scene)
        {
            if (scene == null) return;
            NpcModules.Wire(); // (the modules hung on the core, once: NpcModules)
            if (_holder == null)
            {
                _holder = new GameObject("RavenWatch_Npcs");
                _holder.SetActive(false); // (the prefabs inside never wake)
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            if (_human == null) { try { _human = BuildHuman(scene); } catch (Exception e) { Log("human body: " + e); } }
            if (_odin == null) { try { _odin = BuildOdin(scene); } catch (Exception e) { Log("odin body: " + e); } }
            if (_locker == null) _locker = NpcLocker.Build(_holder.transform);
            Add(scene, _locker, NpcLocker.Prefab);
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
            mind.m_pathAgentType = Pathfinding.AgentType.HumanoidNoSwim; // (the game's own BaseAI asks with this; our feet ask as their own kind: NpcKit.Walker, made when a world loads)
            mind.m_viewRange = 30f;
            mind.m_viewAngle = 140f;
            mind.m_hearRange = 15f;
            mind.m_idleSoundChance = 0f;
            mind.m_randomMoveInterval = 9999f;
            mind.m_jumpInterval = 0f;
            mind.m_avoidWater = true; // (round water, not through it: NpcKit.Walker)
            mind.m_avoidFire = true;
            mind.m_afraidOfFire = false;
            go.AddComponent<NpcBody>();
            go.AddComponent<NpcGait>(); // (its own way of carrying itself: after the game's animation)
            go.AddComponent<NpcEat>(); // (its bite in its hand at the table, raised to its mouth: after the game's animation too)
            go.AddComponent<NpcPose>(); // (the one pass that puts the layers of its pose in order: gait, fall, meal)
            go.AddComponent<NpcFall>(); // (its own fall when it drops senseless, its lying, its getting up: after the game's animation too)
            NpcActor actor = go.AddComponent<NpcActor>();
            GameObject odin = scene.GetPrefab("odin"); Odin os = odin != null ? odin.GetComponent<Odin>() : null;
            if (os != null) actor.m_vanish = os.m_despawn; // (Odin's puff of smoke: a person put somewhere as the last safety goes and comes in it)
            go.AddComponent<NpcResident>();
            go.AddComponent<NpcWorker>();
            // its gear's cells: the game's chest on its body (its window, its keeping in the ZDO); after NpcActor, so E is still its talk
            Container box = go.AddComponent<Container>();
            box.m_name = "Спорядження"; box.m_width = NpcGear.Width; box.m_height = NpcGear.Height;
            box.m_privacy = Container.PrivacySetting.Public; box.m_checkGuardStone = false;
            GameObject chest = scene.GetPrefab("piece_chest_wood");
            Container like = chest != null ? chest.GetComponent<Container>() : null;
            if (like != null) { box.m_bkg = like.m_bkg; box.m_openEffects = like.m_openEffects; box.m_closeEffects = like.m_closeEffects; }
            go.AddComponent<NpcGear>();
            go.AddComponent<NpcVitals>();
            go.AddComponent<NpcHunger>();
            go.AddComponent<NpcToil>(); // (tiredness, mood, pay, its craft)
            go.AddComponent<NpcRescueSite>(); // (a person in need: its scene where it is owned)
            ZNetView nv = go.GetComponent<ZNetView>();
            if (nv != null)
            {
                nv.m_persistent = true; // (the world's people stay; a scene's own are taken away by their scene)
                nv.m_type = ZDO.ObjectType.Default; // (the Player prefab's is Prioritized, measured: ZDOMan.ServerSendCompare sends such a ZDO of another owner before everything else, however long the rest has waited, so walking people kept a base from a second player)
            }
            // the player's animator runs in the physics step, and about one frame in six has no step at 58 frames a second: its bones are not put back, and our layers (clips, gait) lay themselves over their own last result: the head 9-17 cm off for a frame and back, measured (JoltWatch). A person always has layers on it: its animator every frame
            Animator bones = go.GetComponentInChildren<Animator>(true);
            if (bones != null) bones.updateMode = AnimatorUpdateMode.Normal;
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
            at = Free(at);
            at = NpcKit.Floor(at); // (on the floor there, not on the roof over it)
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

        // a spot not inside another person: the asked one, else a step aside round it (1.2 m apart at least)
        private static Vector3 Free(Vector3 at)
        {
            if (!Taken(at)) return at;
            for (int ring = 1; ring <= 2; ring++)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = at + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (1.3f * ring);
                    if (!Taken(p)) return p;
                }
            return at;
        }

        private static bool Taken(Vector3 p)
        {
            foreach (NpcActor n in NpcActor.Live) if (n != null && Utils.DistanceXZ(n.transform.position, p) < 1.2f) return true;
            return false;
        }

        // the live ones (loaded near someone)
        internal static NpcActor Find(string idOrTag)
        {
            NpcActor best = null;
            float bd = float.MaxValue;
            Player p = Player.m_localPlayer;
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || (a.Id != idOrTag && a.Tag != idOrTag && !string.Equals(a.Name, idOrTag, System.StringComparison.OrdinalIgnoreCase))) continue; // (its template, its tag in a scene, or its own name: «Кнуд»)
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
        // a module's console words for one person («raven npc <word> <who> …»: you, it, the words, where you look) — NpcModules
        internal static readonly Dictionary<string, Func<Player, NpcActor, string[], Vector3, string>> Commands = new Dictionary<string, Func<Player, NpcActor, string[], Vector3, string>>();

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
                case "rescue": return NpcRescue.Test(p.transform.position, a.Length > 2 ? a[2] : "");
                case "net":
                {
                    if (who != null) { string d = NetSize.Dump(who.Zdo, who.Id) + "\n" + NetSize.Dump(NpcLocker.Of(who, false), "шафка"); Log("[npc net]\n" + d); return d; } // (one person, every key: what to move off it)
                    System.Text.StringBuilder sb = new System.Text.StringBuilder("Вага NPC у мережі (одне надсилання; поки рухається — до 20 разів на секунду кожному гравцю поруч):\n");
                    int sum = 0;
                    foreach (NpcActor n in NpcActor.Live)
                    {
                        if (n == null || n.Zdo == null) continue;
                        string big; int b = NetSize.Weigh(n.Zdo, out big);
                        sum += b;
                        sb.Append("  ").Append(n.Id).Append(" [").Append(n.Zdo.Type).Append("]: ").Append(b).Append(" Б (").Append(NetSize.PerSecond(b).ToString("0.0")).Append(" КБ/с у русі)").Append(big.Length > 0 ? "  — " + big : "").Append("\n");
                    }
                    sb.Append("Разом: ").Append(sum).Append(" Б, до ").Append(NetSize.PerSecond(sum).ToString("0.0")).Append(" КБ/с, якщо всі рухаються.");
                    Log("[npc net]\n" + sb);
                    return sb.ToString();
                }
                case "self": return NpcSelf.Check(); // (who each one is, against the last run: the same after a restart?)
                case "reload": NpcDefs.Reload(); NpcTalk.Reload(); NpcChatter.Reload(); NpcPhrases.Reload(); return "Перечитано: " + NpcDefs.All().Count + ".";
                case "walk": return NpcWalk.Command(p, a); // (its feet, for everyone: NpcWalk)
                case "stress": return NpcStress.Cmd(p, a); // (the author's load test: NpcStress)
                case "perf": return NpcPerf.Command(a); // (what the people cost: NpcPerf)
                case "tune": return a.Length > 2 ? Studio.Field(a[2], a.Length > 3 ? a[3] : null) : "raven npc tune Клас.Поле [значення] — число на цій грі до перезапуску (тест без нової збірки)"; // (the author's live numbers: Studio.Field)
                case "removeall": int gone = 0; foreach (NpcActor x in NpcActor.Live.ToArray()) if (x != null) { x.Remove(); gone++; } return "Прибрано: " + gone + ".";
            }
            if (what.Length > 0 && who == null) return "Нема такого NPC поруч (raven npc list).";
            Func<Player, NpcActor, string[], Vector3, string> own;
            if (Commands.TryGetValue(what, out own)) return own(p, who, a, aim); // (a module's word: NpcModules)
            switch (what)
            {
                case "go": who.Order("go " + V(aim) + (a.Length > 3 && a[3] == "run" ? " run" : "")); return "Іде туди, куди ти дивишся.";
                case "come": who.Order("go " + V(p.transform.position) + " run"); return "Іде до тебе.";
                case "follow": who.Order("follow " + NpcKit.Ref(p.GetZDOID()) + " 3"); return "Іде за тобою.";
                case "stay": who.Order("stay"); return "Стоїть.";
                case "free": who.Order("free"); return "Відпущено: повертається до своїх справ.";
                case "look": who.Order("look " + NpcKit.Ref(p.GetZDOID())); return "Дивиться на тебе.";
                case "tp": who.Order("tp " + V(aim)); return "Переставлено.";
                case "say": who.Say(a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "…"); return "Сказав.";
                case "clip": if (a.Length < 4) return "raven npc clip <хто> <кліп>|none [секунд]"; who.Order("clip " + a[3] + (a.Length > 4 ? " " + a[4] : "")); return "Кліп: " + a[3];
                case "remove": who.Remove(); return "Пішов.";
                case "down": if (!who.Owned || who.Vitals == null) return "Його веде інша гра."; who.Vitals.KnockOut(); return "Упав без тями (тест падіння, NpcFall).";
                case "up": who.Act("revive"); return "Приходить до тями.";
                case "wander": who.Order("wander " + (a.Length > 3 ? a[3] : "8") + " " + V(aim)); return "Походжає там, куди ти дивишся.";
                case "sit": who.Order("sit " + V(aim)); return "Сідає (на лаву чи стілець біля того місця, куди ти дивишся, або на землю).";
                case "stand": who.Order("stand"); return "Встав.";
                case "emote": if (a.Length < 4) return "raven npc emote <хто> <wave|point|bow|sit|relax|cheer|nonono|thumbsup|…> [loop]"; who.Order("emote " + a[3] + (a.Length > 4 && a[4] == "loop" ? " loop" : "")); return "Жест: " + a[3] + ".";
                case "fight": who.Order("fight " + (a.Length > 3 ? a[3] : "flee")); return "Бій: " + (a.Length > 3 ? a[3] : "flee") + ".";
                case "attack":
                    Character foe = null; float fd = 15f;
                    foreach (Character c in Character.GetAllCharacters()) { if (c == null || c.IsPlayer() || c.IsDead() || c.GetComponent<NpcActor>() != null) continue; float d = Vector3.Distance(c.transform.position, aim); if (d < fd) { fd = d; foe = c; } }
                    if (foe == null) return "Нема ворога біля того місця, куди ти дивишся.";
                    who.Order("attack " + NpcKit.Ref(foe.GetZDOID())); return "Атакує: " + foe.m_name + ".";
            }
            return "raven npc list | spawn <id> [тег] | go <хто> [run] | come | follow | stay | free | settle <хто> [here] | unsettle | job <хто> stoker|smelter|gatherer|cleaner|none | chest <хто> (ящик, куди дивишся) | bed <хто> (ліжко, куди дивишся) | base | whim <хто> [id] (eat — поїсти) | hunger <хто> [0–100] | hall <хто> (до Дому воронів) | rescue [surrounded|captive|hungry|wounded|lost] (подія біля серця) | net (вага в мережі) | perf [reset] (вартість NPC, черга перевірок) | lever <хто> <важіль…> | vars <хто> | request <хто> (попросить зараз) | chat <хто> (заговорить із сусідом зараз) | toil <хто> (втома, настрій, ремесло, платня) | pay <хто> (іде по платню зараз) | gear <хто> | look | wander <хто> [r] | sit | stand | emote <хто> <жест> [loop] | fight <хто> flee|wary|off|defend|hunt | attack <хто> | say <хто> <текст> | tp | remove <хто> | removeall | reload  (хто: id, тег або near)";
        }

        // where you look, on the ground, up to 40 m
        internal static Vector3 Aim(Player p)
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
                if (NpcRescueHits.Take(__instance, hit)) return false; // (one in need: blows counted, not damage)
                return !NpcActor.Immortal(__instance);
            }
            catch (Exception e) { PatchGuard.Fail("NpcDamagePatch", e); return true; }
        }
    }

    // the words under the crosshair: a creature is pointed at as its body (the game's Character is itself «hoverable»,
    // and says something only for a tamed animal) — for our people it says ours: its name, E, Q
    [HarmonyPatch(typeof(Character), "GetHoverText")]
    internal static class NpcHoverTextPatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            if (NpcActor.Live.Count == 0) return;
            try { NpcActor a = NpcActor.Of(__instance); if (a != null) __result = a.GetHoverText(); }
            catch (Exception e) { PatchGuard.Fail("NpcHoverTextPatch", e); }
        }
    }

    [HarmonyPatch(typeof(Character), "GetHoverName")]
    internal static class NpcHoverNamePatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            if (NpcActor.Live.Count == 0) return;
            try { NpcActor a = NpcActor.Of(__instance); if (a != null) __result = a.GetHoverName(); }
            catch (Exception e) { PatchGuard.Fail("NpcHoverNamePatch", e); }
        }
    }

    // no enemy health bar over a person who cannot be hurt
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    internal static class NpcHudPatch
    {
        private static void Postfix(Character c, ref bool __result)
        {
            try { if (__result && c != null && NpcActor.Live.Count > 0 && NpcActor.Immortal(c) && !(NpcRescueHits.InNeed(NpcActor.Of(c)) && !NpcVitals.IsDown(c))) __result = false; }
            catch (Exception e) { PatchGuard.Fail("NpcHudPatch", e); } // (one in need: its bar is its blows left)
        }
    }
}
#endif

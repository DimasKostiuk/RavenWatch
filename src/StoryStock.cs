#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // What a stage sends you to find at a place of the game is there when you come (the user 2026-11-17, chapter 2: Eyvind's mushrooms in a troll
    // cave — «головне щоб в тій печері точно був троль, бо іноді в печерах він може бути відсутній»). A cave's own spawner makes its troll once
    // ever (CreatureSpawner: no respawn time, and it has spawned — never again), and a picked mushroom there is gone from the world for good
    // (Pickable: no regrowth — its ZDO destroyed). The ONE way to keep things to pick at a place (the review 2026-11-18: Heid's thistle had a
    // planting of its own, once, and a talk's «!replant» against a soft lock — now its stages say «stock:» too, and what is picked and lost
    // grows again on the next visit). A stage's line:
    //   stock: <place> <m> <Prefab> <N|A-B> [<Prefab> <N|A-B> …]
    //                                             A-B: at least A, at most B — more standing there: the extras taken away, the game's own word
    //                                             (the user 2026-11-17: fewer than five mushrooms in Eyvind's cave, «а троль там точно»); a thing
    //                                             to pick only (a creature: its A alone). <Prefab>: the game's, or an item of ours with «grows:»
    //                                             (its plant, WsItems.GrowOf: RavenWatch_item_budyak_heid)
    // While the stage stands, each time this player comes within m + Near of the place and its ground is loaded, a game's dungeon there done laying its rooms (once a visit: out beyond m + Leave
    // and back is a new one), the core sees that at least N of each stand within m:
    //   a creature (its prefab has a Character)   fewer alive there — one more at a spawner of the game's for that creature within m, as the
    //                                             game's own spawner makes it (on the floor under the spawner — the cave's, not the land over it;
    //                                             its patrol point); no such spawner — said in the log, nothing made. Alive: those standing
    //                                             there now (NpcCensus.Near: the zones' own lists) and the one each spawner is tied to (not a second one);
    //   a thing to pick (a Pickable)              picked ones that grow again told to grow now (the game's RPC_SetPicked); still fewer — new ones
    //                                             round those of its kind there, else round a creature spawner of the place, else round the place
    //                                             itself (said with what of it is farther out: FarOut) — on the land or on a cave's floor, each where a plant would stand (Sow).
    // One game acts for a line, whoever else is near: the one that owns the first of the place's spawners (by ZDOID — the same on every game;
    // none: the first plant of its kind standing there; nothing yet — the player nearest the place), so two players coming at once make one troll, not two. A creature is made by the
    // spawner itself where this game owns it (its own Spawn: its floor, its level, its patrol point, and the spawned one tied to it — the game
    // makes no second one later), else as it would (on the floor under it). What is made is the game's own thing, kept with the world;
    // nothing of ours is written. The troll may be killed or left alone: the next visit sees again.
    // What it found there is said in the log once a visit (how many of each, wanted, made, taken away): what a cave of the game holds, seen.
    // Cost: twice a second the stages' few words (most have none); the look (the zones round the place: NpcCensus.Near; the loaded spawners and dungeons)
    // once a visit, again each Every s only till it is done (the ground loading) and at most Tries times.
    internal static class StoryStock
    {
        internal static float Leave = 40f, Near = 30f, Every = 5f, Around = 4f; // (Near: it acts this much before he is at the place — what is made there never comes up before his eyes; Leave > Near)
        private const int Tries = 6;
        private static float _nextLook;
        private static ZNet _world; // session: the world the visits below are of
        private static readonly Dictionary<string, int> _visit = new Dictionary<string, int>(); // session: a line in this visit → the looks so far (Tries: done)
        private static readonly Dictionary<string, HashSet<string>> Told = new Dictionary<string, HashSet<string>>(); // session: a visit's words said in the log (once a visit)
        private static string _now = ""; // (the visit being looked at: Say's)
        internal static float Tall = 15f; // m: a creature or a plant counted only this near the height of a spawner there (a creature: one of its own), else of the place — a troll or a mushroom on the hill over the cave is not the cave's, and one too many there would be taken away for good
        private static bool Level(float y, List<CreatureSpawner> its, float placeY)
        {
            if (its.Count == 0) return Mathf.Abs(y - placeY) <= Tall;
            foreach (CreatureSpawner sp in its) if (Mathf.Abs(y - sp.transform.position.y) <= Tall) return true;
            return false;
        }

        internal const string Usage = "stock: <місце> <м> <Prefab> <N|A-B> [<Prefab> <N|A-B> …]";

        // the stage line's own check (StoryQuestsRead): null — good
        internal static string Why(string val)
        {
            string[] w = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float m; int n;
            if (w.Length < 4 || w.Length % 2 != 0 || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m) || m <= 0f) return Usage;
            for (int i = 2; i + 1 < w.Length; i += 2) { int most; if (!Count(w[i + 1], out n, out most) || n < 1 && most < 0) return Usage; }
            return null;
        }

        // «N» — at least N (most -1: no top); «A-B» — at least A, at most B (A may be 0)
        private static bool Count(string s, out int least, out int most)
        {
            most = -1;
            int dash = s.IndexOf('-');
            if (dash < 0) return int.TryParse(s, out least) && least >= 1;
            return int.TryParse(s.Substring(0, dash), out least) && int.TryParse(s.Substring(dash + 1), out most) && least >= 0 && most >= Mathf.Max(1, least);
        }

        internal static void Look(Player p)
        {
            if (p == null || ZNetScene.instance == null || ZoneSystem.instance == null) return;
            if (!ReferenceEquals(_world, ZNet.instance)) { _world = ZNet.instance; _visit.Clear(); Told.Clear(); _nextLook = 0f; } // (another world: its visits are not this one's)
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s != null) foreach (string line in s.Stock) Check(p, q, s, line);
            }
        }

        private static void Check(Player p, StoryQuests.QuestDef q, StoryQuests.StageDef s, string line)
        {
            string[] w = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Vector3 at; float m;
            if (w.Length < 4 || !StoryWhere.Find(w[0], out at) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return;
            string key = q.Id + " " + line; // (the quest's line, not its stage's: the same line on stage after stage in the cave is one visit)
            float d = StoryWhere.Apart(at, p.transform.position);
            if (d > m + Leave) { _visit.Remove(key); Told.Remove(key); return; } // (gone off: the next coming is a new visit, said anew)
            int looks;
            _visit.TryGetValue(key, out looks);
            if (d > m + Near || looks >= Tries || Time.time < _nextLook) return;
            _now = key;
            if (!ZNetScene.instance.IsAreaReady(at)) { _nextLook = Time.time + Every; _visit[key] = looks + 1; if (looks + 1 >= Tries) Say(q.Id + "." + s.Id + ": the ground there never said it was ready (ZNetScene.IsAreaReady) — nothing looked at"); return; }
            if (RoomsComing(at, m)) { _nextLook = Time.time + Every; _visit[key] = looks + 1; if (looks + 1 >= Tries) Say(q.Id + "." + s.Id + ": a dungeon of the game there was still laying its rooms — nothing looked at"); return; } // (its things come with its rooms: none of them seen yet is not «none there»)
            _nextLook = Time.time + Every;
            float ground;
            if (ZoneSystem.instance.GetGroundHeight(at, out ground) && at.y < ground) at.y = ground; // (a place kept as a point of no height of its own — a glade's: never under the land)
            bool done = true;
            List<CreatureSpawner> spawners = Spawners(at, m);
            for (int i = 2; i + 1 < w.Length; i += 2)
            {
                int n, most;
                if (Count(w[i + 1], out n, out most) && !Keep(q.Id + "." + s.Id, w[i], n, most, at, m, spawners, looks + 1 >= Tries)) done = false;
            }
            _visit[key] = done ? Tries : looks + 1;
        }

        // true: enough there, or nothing more this game can do (said); false — look again (not the owner here, the ground still coming)
        private static bool Keep(string of, string prefab, int n, int most, Vector3 at, float m, List<CreatureSpawner> spawners, bool last)
        {
            GameObject pf = ZNetScene.instance.GetPrefab(prefab);
            string grow = WsItems.GrowOf(prefab); // (an item of ours with «grows:»: its plant — Heid's thistle)
            GameObject gp = grow != null ? ZNetScene.instance.GetPrefab(grow) : null;
            if (gp != null) pf = gp;
            if (pf == null) { Say(of + ": no prefab «" + prefab + "» in the game"); return true; }
            if (pf.GetComponent<Character>() != null) return Creature(of, pf, n, at, m, spawners, last);
            if (pf.GetComponent<Pickable>() != null) return Plant(of, pf, n, most, at, m, spawners, last);
            Say(of + ": «" + prefab + "» is neither a creature nor a thing to pick");
            return true;
        }

        private static bool Creature(string of, GameObject pf, int n, Vector3 at, float m, List<CreatureSpawner> spawners, bool last)
        {
            List<CreatureSpawner> its = spawners.FindAll(sp => sp.m_creaturePrefab != null && sp.m_creaturePrefab.name == pf.name);
            HashSet<ZDOID> seen = new HashSet<ZDOID>();
            foreach (ZDO z in NpcCensus.Near(pf.name, at, m)) if (StoryWhere.Apart(z.GetPosition(), at) <= m && Level(z.GetPosition().y, its, at.y)) seen.Add(z.m_uid);
            foreach (CreatureSpawner sp in its) // (the one its spawner made, however new: the census reads the world every 20 s — the review 2026-11-18, the game's troll and ours, two)
            {
                ZDO sz = ZdoOf(sp);
                ZDOID id = sz != null ? sz.GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned) : ZDOID.None;
                if (!id.IsNone() && ZDOMan.instance.GetZDO(id) != null) seen.Add(id);
            }
            int alive = seen.Count;
            if (alive >= n) { Say(of + ": «" + pf.name + "» — " + alive + " there (" + n + " wanted)"); return true; }
            if (its.Count == 0) { if (last) Say(of + ": no spawner of «" + pf.name + "» within " + Mathf.RoundToInt(m) + " m of the place: none made. " + FarOut(pf.name, at)); return last; }
            if (!Mine(spawners[0])) { if (last) Say(of + ": «" + pf.name + "» — " + alive + " there, another game acts for the place"); return last; } // (another game acts for the place)
            List<CreatureSpawner> mine = its.FindAll(Mine); // (made only by a spawner this game owns: tied to it, so the game makes no second one later)
            if (mine.Count == 0) { if (last) Say(of + ": «" + pf.name + "» — " + alive + " there, its spawners are another game's: none made here"); return last; }
            int made = 0;
            for (int k = 0; alive + made < n && k < n; k++)
            {
                CreatureSpawner sp = mine[k % mine.Count];
                bool refused;
                ZNetView nv = BySpawner(sp, out refused);
                if (refused) { Say(of + ": the game's spawner made no «" + pf.name + "» (its own «no spawning»): none made"); break; }
                if (nv == null) nv = Made(pf, sp.transform.position);
                if (nv == null || !nv.IsValid()) continue;
                NpcCensus.Added(nv.GetZDO());
                made++;
            }
            if (made > 0) Story.Log("stock: " + of + ": " + made + " «" + pf.name + "» made at the game's own spawner (" + alive + " there, " + n + " wanted)");
            return alive + made >= n || last;
        }

        // the spawner's own Spawn (private in the game): null — not there in this version or it threw (made by hand then), or it made none: refused
        // (the game's «no spawning» on — then none is made by hand either)
        private static readonly MethodInfo GameSpawn = AccessTools.Method(typeof(CreatureSpawner), "Spawn");
        private static ZNetView BySpawner(CreatureSpawner sp, out bool refused)
        {
            refused = false;
            if (GameSpawn == null) { Say("CreatureSpawner.Spawn is not in this game: made by hand"); return null; }
            try { ZNetView v = GameSpawn.Invoke(sp, null) as ZNetView; refused = v == null; return v; }
            catch (Exception e) { Story.Log("stock: CreatureSpawner.Spawn failed (" + (e.InnerException ?? e).Message + "): made by hand"); return null; } // (at most once a visit: the look is done after it)
        }

        // as the game's spawner makes one: on the floor just under the point (in a cave, the cave's), turned anyhow, its patrol point there
        private static ZNetView Made(GameObject pf, Vector3 pos)
        {
            float h;
            if (ZoneSystem.instance.FindFloor(pos, out h)) pos.y = h;
            GameObject go = UnityEngine.Object.Instantiate(pf, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            BaseAI ai = go.GetComponent<BaseAI>();
            if (ai != null) ai.SetPatrolPoint();
            return go.GetComponent<ZNetView>();
        }

        private static bool Plant(string of, GameObject pf, int n, int most, Vector3 at, float m, List<CreatureSpawner> spawners, bool last)
        {
            int up = 0;
            List<ZDO> picked = new List<ZDO>(), standing = new List<ZDO>();
            foreach (ZDO z in NpcCensus.Near(pf.name, at, m))
            {
                if (StoryWhere.Apart(z.GetPosition(), at) > m || !Level(z.GetPosition().y, spawners, at.y)) continue;
                if (z.GetBool(ZDOVars.s_picked, false)) picked.Add(z); else { up++; standing.Add(z); }
            }
            standing.Sort((a, b) => Order(a.m_uid, b.m_uid));
            if (most >= 0 && up > most) return TooMany(of, pf, up, most, standing, last);
            if (up >= n) { Say(of + ": «" + pf.name + "» — " + up + " standing, " + picked.Count + " picked (" + n + (most >= 0 ? "-" + most : "") + " wanted)"); return true; }
            ZDO lead = spawners.Count > 0 ? ZdoOf(spawners[0]) : standing.Count > 0 ? standing[0] : null;
            if (lead == null && !Nearest(at, m)) { if (last) Say(of + ": «" + pf.name + "» — none there yet, a player nearer sows them"); return last; }
            if (lead != null && !lead.IsOwner()) { if (last) Say(of + ": «" + pf.name + "» — " + up + " standing, another game acts for the place"); return last; } // (another game acts for the place)
            int grown = 0;
            foreach (ZDO z in picked) // (those that grow again keep their ZDO: grown now, by the game's own word to every game)
            {
                if (up >= n) break;
                ZNetView v = ZNetScene.instance.FindInstance(z);
                if (v == null || !v.IsValid() || v.GetComponent<Pickable>() == null) continue;
                v.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", false);
                up++; grown++;
            }
            // still fewer: new ones round those of its kind standing there, else round a spawner of the place, else round the place itself (Sow)
            List<Vector3> anchors = new List<Vector3>(), taken = new List<Vector3>();
            foreach (ZDO z in standing) { anchors.Add(z.GetPosition()); taken.Add(z.GetPosition()); }
            foreach (ZDO z in picked) taken.Add(z.GetPosition());
            foreach (CreatureSpawner sp in spawners) anchors.Add(sp.transform.position);
            if (anchors.Count == 0 && up < n)
            {
                // none of its kind and no spawner there: round the place itself — and what of it is round farther out said (FarOut): a place whose own
                // things lie beyond m shows in the log (the user 2026-11-20: Eyvind's cave got one on the land at its mouth)
                Say(of + ": «" + pf.name + "» — none of its kind and no spawner within " + Mathf.RoundToInt(m) + " m: sown round the place itself. " + FarOut(pf.name, at));
                anchors.Add(at);
            }
            int made = up < n ? Sow(pf, anchors, taken, at, m, n - up) : 0;
            up += made;
            if (grown + made > 0) Story.Log("stock: " + of + ": «" + pf.name + "» — " + grown + " grown again, " + made + " new (" + n + " wanted)");
            return up >= n || last;
        }

        // New plants of a kind, as many as wanted, round the anchors (ones of its kind standing there, the place's spawners; none — the place
        // itself), within m of the place and Around m of an anchor — each one made is one more: a patch, not a ring. Each where a plant would stand:
        //   on the land (an anchor on the heightmap: a glade, the forest's edge) — the land itself under its middle (no tree's root or rock taken
        //     for ground; the user 2026-11-14, the black forest is hilly: «не під землею і не в повітрі»), PlantSink m into it, not steeper than
        //     PlantSlope m across PlantFoot m round it, not in water;
        //   on a floor (an anchor under or over the land: a cave's) — that floor, the anchor's own (FindFloor: not into a wall, not down a drop);
        // no trunk, rock or wall where it stands, PlantApart m from another. Too few so: a second round, PlantLoose times steeper ground allowed,
        // nearer together (a quest waits on them: never none for a hillside). Each made is told to the census at once (NpcCensus.Added)
        internal static float PlantApart = 1.6f, PlantSlope = 0.35f, PlantFoot = 0.35f, PlantSink = 0.05f, PlantLoose = 2f;
        private static int Sow(GameObject pf, List<Vector3> anchors, List<Vector3> taken, Vector3 at, float m, int want)
        {
            int made = 0, mask = LayerMask.GetMask("Default", "static_solid", "piece");
            float water = ZoneSystem.instance.m_waterLevel;
            for (int round = 0; round < 2 && made < want; round++)
            {
                float slope = round == 0 ? PlantSlope : PlantSlope * PlantLoose, apart = round == 0 ? PlantApart : PlantApart * 0.6f;
                for (int tries = 0; made < want && tries < want * 12 + 12; tries++) // heavy: at most a dozen floor rays and checks a plant, twice, once a visit
                {
                    Vector3 a = anchors[UnityEngine.Random.Range(0, anchors.Count)];
                    Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.2f, Around);
                    Vector3 q = new Vector3(a.x + c.x, a.y, a.z + c.y);
                    if (StoryWhere.Apart(q, at) > m || taken.Exists(o => Utils.DistanceXZ(o, q) < apart)) continue; // flat: plants of one patch kept apart on the ground
                    float g, mid, lo, hi, h;
                    bool land = ZoneSystem.instance.GetGroundHeight(a, out g) && Mathf.Abs(g - a.y) < 0.5f;
                    if (land)
                    {
                        if (!Ground(q, out mid, out lo, out hi) || lo < water + 0.3f || hi - lo > slope) continue;
                        q.y = mid - PlantSink;
                    }
                    else
                    {
                        q.y = a.y + 0.5f;
                        if (!ZoneSystem.instance.FindFloor(q, out h) || Mathf.Abs(h - a.y) > 1.2f) continue;
                        q.y = h;
                    }
                    if (Physics.CheckCapsule(q + Vector3.up * 0.3f, q + Vector3.up * 0.9f, 0.2f, mask, QueryTriggerInteraction.Ignore)) continue; // (a trunk, a root, a rock, a wall where it would stand)
                    GameObject go = UnityEngine.Object.Instantiate(pf, q, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                    ZNetView nv = go.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid()) NpcCensus.Added(nv.GetZDO());
                    taken.Add(q); anchors.Add(q); made++;
                }
            }
            return made;
        }

        // the land itself under a point (its middle) and the lowest and highest of it PlantFoot m round. False: its land not made here
        private static bool Ground(Vector3 q, out float mid, out float lo, out float hi)
        {
            lo = hi = 0f;
            if (!ZoneSystem.instance.GetGroundHeight(q, out mid)) return false;
            lo = hi = mid;
            for (int a = 0; a < 4; a++)
            {
                float deg = a * 90f * Mathf.Deg2Rad, h;
                if (!ZoneSystem.instance.GetGroundHeight(new Vector3(q.x + Mathf.Cos(deg) * PlantFoot, 0f, q.z + Mathf.Sin(deg) * PlantFoot), out h)) return false;
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            return true;
        }

        // no plant of its kind there yet and no spawner (an open glade): who sows it — the player nearest the place, of those within m (every game
        // sees the same players; after it, the first plant standing there leads)
        private static bool Nearest(Vector3 at, float m)
        {
            Player me = Player.m_localPlayer, best = null;
            float bd = float.MaxValue;
            foreach (Player o in Player.GetAllPlayers())
            {
                float d = StoryWhere.Apart(o.transform.position, at);
                if (d <= m && (d < bd - 0.5f || (Mathf.Abs(d - bd) <= 0.5f && best != null && o.GetPlayerID() < best.GetPlayerID()))) { best = o; bd = d; }
            }
            return best != null && best == me;
        }

        // more standing than «A-B» lets: the last ones (by ZDOID: the same choice on every game) taken away by the game that owns each, once a visit
        private static bool TooMany(string of, GameObject pf, int up, int most, List<ZDO> standing, bool last)
        {
            int gone = 0, theirs = 0;
            for (int i = standing.Count - 1; i >= most; i--)
            {
                ZDO z = standing[i];
                if (!z.IsOwner()) { theirs++; continue; } // (another game's: it takes its own away)
                ZNetView v = ZNetScene.instance.FindInstance(z);
                if (v != null && v.IsValid()) ZNetScene.instance.Destroy(v.gameObject); else ZDOMan.instance.DestroyZDO(z);
                gone++;
            }
            Say(of + ": «" + pf.name + "» — " + up + " standing, at most " + most + " wanted: " + gone + " taken away" + (theirs > 0 ? ", " + theirs + " another game's" : ""));
            return theirs == 0 || last;
        }

        // the game's spawners within m of the place, loaded here
        private static List<CreatureSpawner> Spawners(Vector3 at, float m)
        {
            List<CreatureSpawner> l = new List<CreatureSpawner>();
            foreach (CreatureSpawner sp in UnityEngine.Object.FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None)) // heavy: once a visit (and each Every s till the ground is loaded, at most Tries)
                if (sp != null && ZdoOf(sp) != null && StoryWhere.Apart(sp.transform.position, at) <= m) l.Add(sp);
            l.Sort((a, b) => Order(ZdoOf(a).m_uid, ZdoOf(b).m_uid)); // (the same order on every game: the first one's owner acts)
            return l;
        }

        // a dungeon of the game within m of the place still laying its rooms (DungeonGenerator: the rooms it read wait for their prefabs — m_loadedRooms,
        // cleared once they are placed): its things are not in the world yet. Heavy: once a look, a look only while a visit's few
        private static readonly FieldInfo RoomsWaiting = AccessTools.Field(typeof(DungeonGenerator), "m_loadedRooms");
        private static bool RoomsComing(Vector3 at, float m)
        {
            if (RoomsWaiting == null) return false;
            foreach (DungeonGenerator g in UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None)) // heavy: see above
                if (g != null && Utils.DistanceXZ(g.transform.position, at) <= m && RoomsWaiting.GetValue(g) != null) return true; // flat: as the line's m
            return false;
        }

        // what of that prefab is round the place out to SeenFar m, for the log when none is within the line's m: how many, the nearest — flat metres and
        // height against the place; and the spawners of it (a creature's). Heavy: only as it is said — none of it found within m (a few times a visit at most)
        internal static float SeenFar = 150f;
        private static string FarOut(string prefab, Vector3 at)
        {
            int n = 0; float best = float.MaxValue, dy = 0f;
            foreach (ZDO z in NpcCensus.Near(prefab, at, SeenFar)) { float d = Utils.DistanceXZ(z.GetPosition(), at); if (d > SeenFar) continue; n++; if (d < best) { best = d; dy = z.GetPosition().y - at.y; } } // flat: the log's own measure
            string s = "Within " + Mathf.RoundToInt(SeenFar) + " m: " + n + (n > 0 ? ", the nearest " + best.ToString("0") + " m off, " + dy.ToString("+0;-0") + " m high" : "");
            int sn = 0; float sb = float.MaxValue, sdy = 0f;
            foreach (CreatureSpawner sp in UnityEngine.Object.FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None)) // heavy: see above
            {
                if (sp == null || sp.m_creaturePrefab == null || sp.m_creaturePrefab.name != prefab) continue;
                float d = Utils.DistanceXZ(sp.transform.position, at); // flat: the log's own measure
                if (d > SeenFar) continue;
                sn++; if (d < sb) { sb = d; sdy = sp.transform.position.y - at.y; }
            }
            if (sn > 0) s += "; its spawners: " + sn + ", the nearest " + sb.ToString("0") + " m off, " + sdy.ToString("+0;-0") + " m high";
            return s + " (the place at " + at.ToString("F0") + ")";
        }

        private static ZDO ZdoOf(CreatureSpawner sp) { ZNetView v = sp != null ? sp.GetComponent<ZNetView>() : null; return v != null && v.IsValid() ? v.GetZDO() : null; }
        private static bool Mine(CreatureSpawner sp) { ZDO z = ZdoOf(sp); return z != null && z.IsOwner(); }
        private static int Order(ZDOID a, ZDOID b) { int c = a.UserID.CompareTo(b.UserID); return c != 0 ? c : a.ID.CompareTo(b.ID); }

        private static void Say(string what) { HashSet<string> t; if (!Told.TryGetValue(_now, out t)) Told[_now] = t = new HashSet<string>(); if (t.Add(what)) Story.Log("stock: " + what); }
    }
}
#endif

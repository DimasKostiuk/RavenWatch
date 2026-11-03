#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Meals (npc-plan.md, «Голод і їдальня»): where residents eat and what. Nothing to build or set: found by itself.
    //   the pantry   a cook's chest in the base (what it cooks goes there — its chest is marked «Комора»); else a chest
    //                round the dining table, or beside any table; else the nearest chest of the base — with cooked food in it
    //   the food     only cooked (off a cooking station, an oven, a cauldron) — and the plainest there: the better is
    //                left for the players (the user)
    //   the table    the heart's dining table when the player picked one («Їдальня», NpcHeart), else a table near the
    //                pantry: it sits at it (a bench or a chair by it, else the ground beside it); a chest by the dining
    //                table is the first pantry after the cook's
    // A meal is one plan (NpcMind Task.Eat): to the pantry → a bite out of it → to the table → sits and eats → full.
    // Taken in the evening before the fire when it is not full, and in the day when it is hungry (NpcResident).
    // Light: the chests of a base come from the workers' shared look (NpcWorker.InBaseOf), the tables of a base are
    // looked for once in half a minute; nothing runs while nobody eats.
    internal static class NpcMeals
    {
        private const float TableNear = 5f, DiningReach = 12f, EatMin = 30f, EatMax = 55f, SitWalk = 40f, TablesAgain = 30f;

        // ---------------------------------------------------------------- what is cooked food (from the game's own lists, once a world)
        private static HashSet<string> _cooked;
        private static object _cookedWorld;

        private static HashSet<string> Cooked
        {
            get
            {
                if (_cooked != null && ReferenceEquals(_cookedWorld, ZNet.instance)) return _cooked;
                HashSet<string> set = new HashSet<string>();
                if (ZNetScene.instance != null)
                    foreach (GameObject go in ZNetScene.instance.m_prefabs)
                    {
                        CookingStation s = go != null ? go.GetComponent<CookingStation>() : null;
                        if (s == null || s.m_conversion == null) continue;
                        foreach (CookingStation.ItemConversion c in s.m_conversion) if (c != null && c.m_to != null) set.Add(c.m_to.m_itemData.m_shared.m_name);
                    }
                if (ObjectDB.instance != null)
                    foreach (Recipe r in ObjectDB.instance.m_recipes)
                        if (r != null && r.m_item != null && r.m_craftingStation != null && r.m_craftingStation.m_name == "$piece_cauldron") set.Add(r.m_item.m_itemData.m_shared.m_name);
                if (set.Count == 0) return set; // (the game not ready yet: asked again)
                _cooked = set; _cookedWorld = ZNet.instance;
                return set;
            }
        }

        private static float Worth(ItemDrop.ItemData it) { return it.m_shared.m_food + it.m_shared.m_foodStamina + it.m_shared.m_foodEitr; }

        // the plainest cooked food in a chest (null: none)
        internal static ItemDrop.ItemData Plainest(Inventory inv)
        {
            if (inv == null) return null;
            HashSet<string> cooked = Cooked;
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData it in inv.GetAllItems())
                if (it.m_shared.m_food > 0f && cooked.Contains(it.m_shared.m_name) && (best == null || Worth(it) < Worth(best))) best = it;
            return best;
        }

        // how much a bite fills (better food: more)
        private static float Fill(ItemDrop.ItemData it) { return it == null ? 40f : Mathf.Clamp(25f + Worth(it) * 0.4f, 35f, 90f); }

        // ---------------------------------------------------------------- the pantry and the tables
        // a cook's chest (its own word for it: «Комора»)
        internal static bool IsCooksChest(ZDOID chest, out NpcActor cook)
        {
            cook = null;
            if (chest.IsNone()) return false;
            foreach (NpcActor o in NpcActor.Live)
                if (o != null && o.Worker != null && o.Worker.JobId == "cook" && o.Worker.ChestId == chest) { cook = o; return true; }
            return false;
        }

        private static readonly Dictionary<string, KeyValuePair<float, List<Transform>>> Tables = new Dictionary<string, KeyValuePair<float, List<Transform>>>();
        private static object _tablesWorld;

        private static List<Transform> TablesOf(NpcBase b)
        {
            if (b.Tables != null) return b.Tables; // (the base's own look at its things: NpcBase)
            if (!ReferenceEquals(_tablesWorld, ZNet.instance)) { Tables.Clear(); _tablesWorld = ZNet.instance; }
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<Transform>> had;
            if (Tables.TryGetValue(key, out had) && Time.time - had.Key < TablesAgain) return had.Value;
            List<Transform> l = new List<Transform>();
            List<Piece> pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(b.Centre, NpcBase.Reach, pieces);
            foreach (Piece pc in pieces)
            {
                if (pc == null || !Utils.GetPrefabName(pc.gameObject).ToLowerInvariant().Contains("table")) continue; // (piece_table, _round, _oak — and other mods' tables)
                if (pc.GetComponentInChildren<CraftingStation>() != null || pc.GetComponentInChildren<MapTable>() != null) continue; // (a workbench, a prep table, a map table: not for eating)
                if (b.Inside(pc.transform.position)) l.Add(pc.transform);
            }
            if (Tables.Count > 50) Tables.Clear(); // (kept small: bases come and go)
            Tables[key] = new KeyValuePair<float, List<Transform>>(Time.time, l);
            return l;
        }

        private static Transform TableNearest(NpcBase b, Vector3 at, float range)
        {
            Transform best = null; float bd = range * range;
            foreach (Transform t in TablesOf(b)) { if (t == null) continue; float d = (t.position - at).sqrMagnitude; if (d < bd) { bd = d; best = t; } }
            return best;
        }

        // a table of the base round a spot (the heart's «Їдальня» pick)
        internal static Transform TableAt(NpcBase b, Vector3 at, float range) { return b != null ? TableNearest(b, at, range) : null; }

        // where it eats: a cook's chest in its base with cooked food, else a chest beside a table with some (nearest first)
        private static readonly HashSet<ZDOID> Pantries = new HashSet<ZDOID>(); // (chests found as a pantry: their hover says so)
        internal static bool IsPantry(ZDOID id) { return Pantries.Contains(id); }

        internal static Container Pantry(NpcBase b, Vector3 me)
        {
            if (b == null || !b.Ready) return null;
            List<Container> all = NpcWorker.InBaseOf<Container>(b, me);
            Transform dining = NpcHeart.DiningOf(b);
            System.Func<Container, bool> byTable = c => dining != null && (Vector3.Distance(dining.position, c.transform.position) <= 6f || NpcKit.OnFoot(dining.position, c.transform.position, DiningReach) <= DiningReach); // (near the dining table on foot: not through a wall into the next building)
            // the dining table you picked (2026-11-01): the cook's chest by it first — that is the pantry — then any chest by it
            // with food; only then the cook's chest elsewhere in the base (not food fetched across the base for a table here)
            List<Container> cooks = new List<Container>();
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Worker == null || o.Worker.JobId != "cook") continue;
                Container c = o.Worker.Chest;
                if (c != null && b.Inside(c.transform.position) && Open(c) && Plainest(c.GetInventory()) != null) cooks.Add(c);
            }
            cooks.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
            if (dining != null) foreach (Container c in cooks) if (byTable(c)) return Mark(c);
            Container c1 = dining != null ? Nearest(all, me, byTable) : null;
            if (c1 != null) return Mark(c1);
            if (cooks.Count > 0) return Mark(cooks[0]);
            Container c2 = TablesOf(b).Count > 0 ? Nearest(all, me, c => { Transform t = TableNearest(b, c.transform.position, TableNear); return t != null && (NpcKit.OnFoot(t.position, c.transform.position, TableNear + 2f) <= TableNear + 2f || Vector3.Distance(t.position, c.transform.position) <= 3f); }) : null;
            if (c2 != null) return Mark(c2);
            return Nearest(all, me, c => true); // (no pantry anywhere: the nearest chest of the base with something cooked in it — the plainest taken from it, as ever)
        }

        // of these chests, those that also pass ok: the nearest it can walk to (straight, or through a door) with cooked food
        // in it first — else the nearest of them all (the way finder builds its map in parts: «no way» may only be «not yet»)
        private static Container Nearest(List<Container> all, Vector3 me, System.Func<Container, bool> ok)
        {
            List<Container> l = new List<Container>();
            foreach (Container c in all) if (Open(c) && Plainest(c.GetInventory()) != null && ok(c)) l.Add(c); // (food first: the way finder only for chests with some)
            l.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
            foreach (Container c in l) if (Reachable(me, c.transform.position)) return c;
            if (l.Count > 0 && Time.time >= _nextWhy) { _nextWhy = Time.time + 20f; System.Text.StringBuilder sb = new System.Text.StringBuilder(); for (int i = 0; i < l.Count && i < 4; i++) sb.Append(" · ").Append(l[i].m_name).Append(" ").Append(Mathf.RoundToInt(Vector3.Distance(me, l[i].transform.position))).Append(" m: ").Append(Why(me, l[i].transform.position)); Npcs.Log("meals: no way found to the chests with food (the way finder may not know that part yet) — the nearest taken" + sb); } // (why: what the way finder said)
            return l.Count > 0 ? l[0] : null; // (the way finder knows a base only in parts, asked: none known is no proof — the nearest, and if it is stuck on its way it is stepped by it)
        }

        private static float _nextWhy;
        private static string Why(Vector3 me, Vector3 at)
        {
            if (Pathfinding.instance == null) return "?";
            Vector3 spot, from;
            if (!NpcKit.Valid(out spot, at, 2.5f, NpcKit.Walker)) return "no spot to stand by it";
            bool start = NpcKit.Valid(out from, me, 2f, NpcKit.Walker);
            if (!start) from = me;
            if (Pathfinding.instance.HavePath(from, spot, NpcKit.Walker)) return "a way (?)";
            return (start ? "" : "it stands off the walkable, ") + "no way" + (NpcDoors.Toward(from, spot, NpcKit.Walker) != null ? ", a door" : ", no door") + " (spot " + Npcs.V(spot) + ", from " + Npcs.V(from) + ")";
        }

        private static bool Reachable(Vector3 me, Vector3 at)
        {
            if (Pathfinding.instance == null) return true;
            Vector3 spot, from;
            if (!NpcKit.Valid(out spot, at, 2.5f, NpcKit.Walker)) return false; // (nowhere to stand by it)
            if (NpcKit.Valid(out from, me, 2f, NpcKit.Walker)) me = from; // (it may stand where no way starts: in a corner it was stepped into)
            return Pathfinding.instance.HavePath(me, spot, NpcKit.Walker) || NpcDoors.Toward(me, spot, NpcKit.Walker) != null;
        }

        // a chest it may take from: a built one (not a ship's hold, not a person's cells), not a private one
        private static bool Open(Container c) { return c != null && c.GetComponent<Piece>() != null && c.m_privacy != Container.PrivacySetting.Private && !Busy(c); }

        // a chest a player has open: passed over for half a minute (the next one with food in it is taken, not stood by)
        private static readonly Dictionary<Container, float> BusyUntil = new Dictionary<Container, float>();
        private static bool Busy(Container c) { float t; return BusyUntil.TryGetValue(c, out t) && Time.time < t; }
        private static void SetBusy(Container c) { if (BusyUntil.Count > 100) BusyUntil.Clear(); BusyUntil[c] = Time.time + 30f; }

        // why there is no pantry now, in the player's words (the heart's board, the log): what is missing, the nearest first —
        // no table · no chest by a table · the chest by the table is personal · only what is not cooked in it (named)
        internal static string Why(NpcBase b)
        {
            if (b == null || !b.Ready) return Lang.T("база ще оглядається");
            if (TablesOf(b).Count == 0) return Lang.T("на базі нема столу (їжа — у ящику біля столу або в коморі кухаря)");
            List<string> raw = new List<string>();
            bool chest = false, personal = false;
            foreach (Container c in NpcWorker.InBaseOf<Container>(b, b.Centre))
            {
                if (c.GetComponent<Piece>() == null || TableNearest(b, c.transform.position, TableNear) == null) continue;
                if (c.m_privacy == Container.PrivacySetting.Private) { personal = true; continue; }
                chest = true;
                if (c.GetInventory() == null) continue;
                foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
                    if (it.m_shared.m_food > 0f && !Cooked.Contains(it.m_shared.m_name))
                    {
                        string n = Localization.instance != null ? Localization.instance.Localize(it.m_shared.m_name) : it.m_shared.m_name;
                        if (!raw.Contains(n) && raw.Count < 4) raw.Add(n);
                    }
            }
            if (!chest) return personal ? Lang.T("ящик біля столу особистий — потрібен звичайний") : Lang.F("біля столу (до {0} м) нема ящика", Mathf.RoundToInt(TableNear));
            if (raw.Count > 0) return Lang.F("у ящику біля столу лише не готове: {0} — їдять тільки страви з вогнища, печі чи казана", string.Join(", ", raw.ToArray()));
            return Lang.T("у ящику біля столу нема страв (з вогнища, печі чи казана)");
        }

        private static Container Mark(Container c)
        {
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid()) { if (Pantries.Count > 200) Pantries.Clear(); Pantries.Add(nv.GetZDO().m_uid); }
            return c;
        }

        internal static NpcMind.Plan Plan() { return new NpcMind.Plan { Kind = NpcMind.Task.Eat }; }

        // ---------------------------------------------------------------- the meal, step by step (true: over)
        //   0 find the pantry · 1 to it, a bite out · 2 to the table, sits · 3 eats
        // its meal's steps in the log as they change (where a meal stops is seen, not guessed)
        private static readonly Dictionary<NpcMind, string> Was = new Dictionary<NpcMind, string>();
        private static void Trace(NpcMind m, NpcMind.Plan p, Container c)
        {
            string now = p.Stage + " " + m.Status + (c != null ? " (" + Mathf.RoundToInt(Utils.DistanceXZ(m.transform.position, c.transform.position) * 10f) / 10f + " m from " + c.m_name + (c.IsInUse() ? ", open" : "") + ")" : "");
            string was;
            if (Was.TryGetValue(m, out was) && was.Substring(0, Mathf.Min(was.Length, 12)) == now.Substring(0, Mathf.Min(now.Length, 12))) return;
            if (Was.Count > 50) Was.Clear();
            Was[m] = now;
            Npcs.Log((m.Actor != null ? m.Actor.Id : "?") + ": meal — " + now);
        }

        // its way to a table: a free seat round it, else on foot to the table (a door the way finder cannot pass: none)
        // the dining table you picked: it walks there from anywhere in its base (up to Picked); any other table: not further than SitWalk
        private const float Picked = 80f;
        private static bool CanWalkTo(NpcMind m, Transform table, bool picked)
        {
            if (NpcMind.MealSeat(table.position) != null) return true; // (a free seat at it: it goes — stuck on the way, it is put on the seat; the way finder's «no way» to a place not mapped yet is no answer)
            float far = picked ? Picked : SitWalk;
            return NpcKit.OnFoot(m.transform.position, table.position, far) <= far;
        }

        internal static bool Step(NpcMind m, NpcMind.Plan p, float dt)
        {
            try { return StepOf(m, p, dt); }
            finally { GameObject tg = p.Stage == 1 ? NpcKit.Find(p.Who) : null; Trace(m, p, tg != null ? tg.GetComponentInChildren<Container>() : null); }
        }

        private static bool StepOf(NpcMind m, NpcMind.Plan p, float dt)
        {
            NpcActor a = m.Actor;
            NpcResident r = a != null ? a.Resident : null;
            if (a == null || r == null || !NpcResident.Is(a)) return true;
            switch (p.Stage)
            {
                case 0:
                {
                    if (!r.Base.Ready) { m.Status = "думає, де поїсти"; return Time.time - p.BestAt > 30f; } // (its base still being looked over)
                    Container c = Pantry(r.Base, m.transform.position);
                    if (c == null) { NpcChatter.MutterOnce(a, "nofood"); Npcs.Log(a.Id + ": nothing to eat — " + Why(r.Base)); return true; }
                    ZNetView nv = c.GetComponentInParent<ZNetView>();
                    if (nv == null || !nv.IsValid()) return true; // (not a saved thing: nothing to come back to)
                    p.Who = nv.GetZDO().m_uid; p.To = c.transform.position; p.Stage = 1; p.Since = Time.time;
                    Npcs.Log(a.Id + ": goes to eat (" + Mathf.RoundToInt(NpcHunger.Full(a)) + ") from " + c.m_name + " " + Mathf.RoundToInt(Vector3.Distance(m.transform.position, c.transform.position)) + " m off");
                    m.NewGoal();
                    return false;
                }
                case 1:
                {
                    GameObject go = NpcKit.Find(p.Who);
                    Container c = go != null ? go.GetComponentInChildren<Container>() : null;
                    if (c == null) { p.Stage = 0; return p.Lost++ > 2f; } // (gone: another pantry, a few tries)
                    m.Status = "іде їсти";
                    if (!m.Walk(dt, c.transform.position, 1.8f, false)) return false;
                    ZNetView nv = c.GetComponentInParent<ZNetView>();
                    if (nv == null || !nv.IsValid()) return true;
                    if (nv.GetZDO().GetInt(ZDOVars.s_inUse, 0) != 0 && !NpcLid.Mine(c, a)) { m.Status = "чекає біля комори"; if (Time.time - p.Since > 25f) { SetBusy(c); p.Stage = 0; return p.Lost++ > 2f; } return false; } // (a player has it open: it waits a while — most close it soon — then another chest with food)
                    // its hand into the chest, as a player reaches into one: a moment facing it, then the bite
                    if (!m.Reach(c, ref p.Hand)) { m.Status = "бере їжу"; return false; } // (its hand into it: NpcMind.Reach)
                    ItemDrop.ItemData bite = Plainest(c.GetInventory());
                    if (bite == null) { p.Stage = 0; return p.Lost++ > 2f; } // (eaten up meanwhile)
                    p.Food = bite.m_dropPrefab != null ? bite.m_dropPrefab.name : bite.m_shared.m_name; // (its prefab: its look in the hand, its name when eaten)
                    p.Radius = Fill(bite); // (how much it will fill)
                    c.GetInventory().RemoveItem(bite, 1);
                    NpcLid.Close(c); // (the bite in hand: its lid down, with its sound)
                    m.Face(c.transform.position);
                    Transform picked = NpcHeart.DiningOf(r.Base);
                    Transform table = picked ?? TableNearest(r.Base, c.transform.position, 12f); // (the picked dining table, however far the pantry)
                    if (table != null && !CanWalkTo(m, table, table == picked)) { Npcs.Log(a.Id + ": its dining table is out of its way on foot (" + NpcKit.FootWhy + ") — eats by the pantry"); table = TableNearest(r.Base, c.transform.position, 12f); if (table != null && !CanWalkTo(m, table, false)) table = null; } // (behind a door it finds no way through, in another house: not a crawl along a wall)
                    if (p.Until > 0f) p.Until = Mathf.Max(p.Until, Time.time + EatMax + 120f); // (its bite in hand: time enough to sit down and eat it, the whim's end put off)
                    p.To = table != null ? table.position : c.transform.position + m.transform.forward * -1.5f; // (a spot to sit round: the nearest free seat within a few steps of it — NpcMind.Sitting — else the ground by it)
                    p.Started = false; p.Seated = false; p.Stage = 2;
                    m.NewGoal();
                    return false;
                }
                case 2:
                    m.SitDown(dt, p);
                    if (!p.Seated) return false;
                    p.Stage = 3; p.Since = Time.time; p.Keep = Random.Range(EatMin, EatMax);
                    NpcEat.Set(a, p.Food); // (the bite in its hand, to its mouth: seen on every game)
                    if (Random.value < 0.3f) NpcChatter.Mutter(a, "meal");
                    return false;
                default:
                    if (!p.Seated) { NpcEat.Set(a, ""); p.Stage = 2; p.Started = false; return false; } // (stood up: sits again, the bite still in hand)
                    m.SitDown(dt, p);
                    m.Status = "їсть";
                    if (Time.time - p.Since < p.Keep) return false;
                    if (a.Hunger != null) { GameObject fp = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(p.Food) : null; ItemDrop fd = fp != null ? fp.GetComponent<ItemDrop>() : null; a.Hunger.Eat(p.Radius, fd != null && Localization.instance != null ? Localization.instance.Localize(fd.m_itemData.m_shared.m_name).ToLowerInvariant() : ""); }
                    NpcEat.Set(a, "");
                    Npcs.Log(a.Id + ": ate " + p.Food + " (+" + Mathf.RoundToInt(p.Radius) + ", now " + Mathf.RoundToInt(NpcHunger.Full(a)) + ")");
                    return true;
            }
        }
    }

    // a pantry's hover: whose it is and what it is for
    [HarmonyLib.HarmonyPatch(typeof(Container), "GetHoverText")]
    internal static class NpcPantryHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (NpcActor.Live.Count == 0 || string.IsNullOrEmpty(__result)) return;
            ZNetView nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            ZDOID id = nv.GetZDO().m_uid;
            NpcActor cook;
            if (NpcMeals.IsCooksChest(id, out cook)) __result += "\n<color=#e0b860>" + Lang.F("Комора кухаря {0}", cook.Name) + "</color>\n<size=80%>" + Lang.T("Готове звідси їдять мешканці бази") + "</size>";
            else if (NpcMeals.IsPantry(id)) __result += "\n<color=#e0b860>" + Lang.T("Їдальня") + "</color>\n<size=80%>" + Lang.T("Готове звідси їдять мешканці бази") + "</size>";
        }
    }
}
#endif

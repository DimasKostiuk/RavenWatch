#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Meals (npc-plan.md, «Голод і їдальня»): where residents eat and what. Nothing to build or set: found by itself.
    //   the pantry   a cook's pantry with a dish in it, wherever it stands, first of all (NpcKitchen: «Комора» is where the base eats). Else, with a dining table
    //                picked: a chest with cooked food up to DiningStraight m from it in a straight line, no wall between (one ray, table to
    //                chest); none: it stays hungry. With no table picked: a cook's chest in the base (what
    //                it cooks goes there — its chest is marked «Комора»), a chest beside any table, the nearest chest of the base — with cooked food in it.
    //                Found once for the whole base and remembered (Larder): looked for again only when that chest is gone, closed to them or out of food;
    //                found none: not looked for again for LookAgain s
    //   the food     only cooked (off a cooking station, an oven, a cauldron) — and the plainest there: the better is
    //                left for the players (the user)
    //   the table    the heart's dining table when the player picked one («Їдальня», NpcHeart), else a table near the
    //                pantry: it sits at it (a bench or a chair by it, else the ground beside it)
    // A meal is one plan (NpcMind Task.Eat): to the pantry → a bite out of it → to the table → sits and eats → full.
    // Taken in the evening before the fire when it is not full, and in the day when it is hungry (NpcResident). The last bite
    // of a pantry taken while a hungry neighbour stands by: that one holds it against it (NpcAttitude.Quarrel «food»).
    // Light: the chests of a base come from the workers' shared look (NpcWorker.InBaseOf), its tables from the base's own (NpcBase.Tables);
    // nothing runs while nobody eats.
    internal static class NpcMeals
    {
        internal static float DiningStraight = 15f, LookAgain = 30f;
        private const float RayUp = 1.2f; // m over table and chest: above the things that stand in a room, under the height of a wall
        private const float TableNear = 5f, EatMin = 30f, EatMax = 55f, SitWalk = 40f;

        // ---------------------------------------------------------------- what is cooked food: a kitchen's dish (NpcKitchen.IsDish — the game's own lists, once a
        // world: off a rack or an oven, or made at a cooking tool; the one answer for what a cook carries to its pantry and what its people eat from it)
        private static float Worth(ItemDrop.ItemData it) { return it.m_shared.m_food + it.m_shared.m_foodStamina + it.m_shared.m_foodEitr; }

        // the plainest cooked food in a chest (null: none)
        internal static ItemDrop.ItemData Plainest(Inventory inv)
        {
            if (inv == null) return null;
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData it in inv.GetAllItems())
                if (it.m_shared.m_food > 0f && NpcKitchen.IsDish(it.m_shared.m_name) && (best == null || Worth(it) < Worth(best))) best = it;
            return best;
        }

        // how much a bite fills (better food: more)
        internal static float Fill(ItemDrop.ItemData it) { return it == null ? 40f : Mathf.Clamp(25f + Worth(it) * 0.4f, 35f, 90f); }

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

        private static readonly List<Transform> NoTables = new List<Transform>();

        private static List<Transform> TablesOf(NpcBase b)
        {
            return b.Tables ?? NoTables; // (the base's own look at its things: NpcBase; not made yet — none till it is)
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

        // the pantry's last bite taken by «a»: one of its people, hungry and near, holds it against it (the first such one)
        private const float LastBiteSeen = 15f;
        private static void LastBite(NpcActor a, Vector3 at)
        {
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
                if (o != null && o != a && NpcHunger.IsHungry(o) && Utils.DistanceXZ(o.transform.position, at) < LastBiteSeen) { NpcAttitude.Quarrel(o, a, "food"); return; }
        }

        // nothing cooked to be had at its home (asked now and then, not a person's every look: it goes over its base's chests)
        internal static bool NothingToEat(NpcActor a) { NpcResident r = a != null ? a.GetComponent<NpcResident>() : null; NpcBase b = r != null ? r.Base : null; return b != null && b.Ready && Pantry(b, a.transform.position) == null; } // (its base not looked over yet: not known, so not «nothing»)

        internal static Container Pantry(NpcBase b, Vector3 me)
        {
            if (b == null || !b.Ready) return null;
            Larder l;
            if (!Larders.TryGetValue(b, out l)) { if (Larders.Count > 8) Larders.Clear(); l = new Larder(); Larders[b] = l; } // (a base's map let go — forgotten, another world — leaves its entry here, and the entry holds the whole map: a few at most, a larder is found again in one look)
            Transform dining = NpcHeart.DiningOf(b);
            if (dining != l.Table) { l.Table = dining; l.Chest = null; l.NextLook = 0f; } // (another table picked: its own chest)
            if (l.Chest != null && Open(l.Chest) && Plainest(l.Chest.GetInventory()) != null) return l.Chest; // (the one found: nothing more asked)
            l.Chest = null;
            if (Time.time < l.NextLook) return null;
            l.Chest = dining != null ? AtTable(b, dining) : Anywhere(b, me);
            if (l.Chest == null) { l.NextLook = Time.time + LookAgain; return null; }
            return Mark(l.Chest);
        }

        // the chest a base eats from, once found
        private sealed class Larder { public Container Chest; public Transform Table; public float NextLook; }
        private static readonly Dictionary<NpcBase, Larder> Larders = new Dictionary<NpcBase, Larder>();

        private static readonly int Walls = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");
        private static bool InZone(Transform dining, Container c) { return (dining.position - c.transform.position).sqrMagnitude <= DiningStraight * DiningStraight; }
        private static bool NoWall(Transform dining, Container c) { return !Physics.Linecast(dining.position + Vector3.up * RayUp, c.transform.position + Vector3.up * RayUp, Walls, QueryTriggerInteraction.Ignore); }

        private static readonly List<Component> NoChests = new List<Component>();
        private static List<Component> ChestsRound(NpcBase b) { return b.Round(typeof(Container)) ?? NoChests; } // (the base's own look, in it or by its edge)

        private static List<Container> CooksChests(NpcBase b, Vector3 near)
        {
            List<Container> cooks = new List<Container>();
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Worker == null || o.Worker.JobId != "cook") continue;
                Container c = o.Worker.Chest;
                if (c != null && b.Holds(c.transform.position) && Open(c) && Plainest(c.GetInventory()) != null) cooks.Add(c);
            }
            cooks.Sort((x, y) => (x.transform.position - near).sqrMagnitude.CompareTo((y.transform.position - near).sqrMagnitude));
            return cooks;
        }

        // the dining table you picked: the nearest chest by it with cooked food and no wall between (the food looked at first, a ray only for those that have some); the cook's goes first
        private static Container AtTable(NpcBase b, Transform dining)
        {
            List<Container> cooks = CooksChests(b, dining.position);
            if (cooks.Count > 0) return cooks[0]; // (a cook's pantry with food, wherever it stands: the base eats from it first — a picked table only says where they sit; it used to lose to any chest nearer the table, and «нема що їсти» came with a full pantry a wall away: the user 2026-11-12)
            List<Container> l = new List<Container>();
            foreach (Component k in ChestsRound(b)) { Container c = k as Container; if (Open(c) && InZone(dining, c) && Plainest(c.GetInventory()) != null) l.Add(c); }
            l.Sort((x, y) => (x.transform.position - dining.position).sqrMagnitude.CompareTo((y.transform.position - dining.position).sqrMagnitude));
            foreach (Container c in l) if (NoWall(dining, c)) return c;
            return null;
        }

        private static Container Anywhere(NpcBase b, Vector3 me)
        {
            List<Container> all = NpcWorker.InBaseOf<Container>(b, me);
            List<Container> cooks = CooksChests(b, me);
            if (cooks.Count > 0) return cooks[0];
            Container c2 = TablesOf(b).Count > 0 ? Nearest(all, me, c => { Transform t = TableNearest(b, c.transform.position, TableNear); return t != null && (NpcKit.OnFoot(t.position, c.transform.position, TableNear + 2f) <= TableNear + 2f || Vector3.Distance(t.position, c.transform.position) <= 3f); }) : null;
            if (c2 != null) return c2;
            return Nearest(all, me, c => true); // (no pantry anywhere: the nearest chest of the base with something cooked in it — the plainest taken from it, as ever)
        }

        // what the base has to eat (the board, a heart's notes): how much a meal's worth of fullness lies in the chests Pantry would take from — no ways asked (Pantry
        // comes to the nearest chest with food whether a way is known or not: so «nothing to eat» is the same answer). A look at each chest's list: only when asked,
        // a heart at most once in NpcBoard.Every s while you are near; a ray only for a chest with food in it, a dining table picked
        internal static float Supply(NpcBase b)
        {
            if (b == null || !b.Ready) return 0f;
            float sum = 0f;
            Transform dining = NpcHeart.DiningOf(b);
            foreach (Container c in Chests(b, dining))
            {
                if (!Open(c) || c.GetInventory() == null) continue;
                float here = 0f;
                foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems()) if (it.m_shared.m_food > 0f && NpcKitchen.IsDish(it.m_shared.m_name)) here += Fill(it) * it.m_stack;
                if (here > 0f && (dining == null || IsCooks(c) || (InZone(dining, c) && NoWall(dining, c)))) sum += here;
            }
            return sum;
        }

        private static bool IsCooks(Container c)
        {
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.Worker != null && o.Worker.JobId == "cook" && o.Worker.Chest == c) return true;
            return false;
        }

        // the chests a base's meals look over: round its dining table's side of the edge too, if one is picked
        private static List<Container> Chests(NpcBase b, Transform dining)
        {
            if (dining == null) return NpcWorker.InBaseOf<Container>(b, b.Centre);
            List<Container> l = new List<Container>();
            foreach (Component k in ChestsRound(b)) { Container c = k as Container; if (c != null) l.Add(c); }
            return l;
        }

        // for how many game days that feeds these people (0: no one to feed or nothing)
        internal static float DaysOf(float supply, List<NpcActor> who)
        {
            float need = 0f;
            foreach (NpcActor a in who) if (a != null) need += NpcHunger.NeedPerDay(a);
            return need > 0f ? supply / need : 0f;
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
            if (NpcWalk.Have(from, spot)) return "a way (?)";
            return (start ? "" : "it stands off the walkable, ") + "no way" + (NpcDoors.Toward(from, spot, NpcKit.Walker) != null ? ", a door" : ", no door") + " (spot " + Npcs.V(spot) + ", from " + Npcs.V(from) + ")";
        }

        private static bool Reachable(Vector3 me, Vector3 at)
        {
            if (Pathfinding.instance == null) return true;
            Vector3 spot, from;
            if (!NpcKit.Valid(out spot, at, 2.5f, NpcKit.Walker)) return false; // (nowhere to stand by it)
            if (NpcKit.Valid(out from, me, 2f, NpcKit.Walker)) me = from; // (it may stand where no way starts: in a corner it was stepped into)
            return NpcWalk.Have(me, spot) || NpcDoors.Toward(me, spot, NpcKit.Walker) != null;
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
            Transform dining = NpcHeart.DiningOf(b);
            List<string> raw = new List<string>();
            bool chest = false, personal = false;
            foreach (Container c in Chests(b, dining))
            {
                // (with a table picked: the chests by it; with none: every chest of the base — the nearest with a dish in it is taken, table or no table. It
                // said «на базі нема столу», though no table is needed: read 2026-11-12)
                if (c.GetComponent<Piece>() == null || (dining != null && (!InZone(dining, c) || !NoWall(dining, c)))) continue;
                if (c.m_privacy == Container.PrivacySetting.Private) { personal = true; continue; }
                chest = true;
                if (c.GetInventory() == null) continue;
                foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
                    if (it.m_shared.m_food > 0f && !NpcKitchen.IsDish(it.m_shared.m_name))
                    {
                        string n = Localization.instance != null ? Localization.instance.Localize(it.m_shared.m_name) : it.m_shared.m_name;
                        if (!raw.Contains(n) && raw.Count < 4) raw.Add(n);
                    }
            }
            if (!chest) return personal ? Lang.T("ящик біля столу особистий — потрібен звичайний") : dining != null ? Lang.F("біля столу їдальні (до {0} м, не за стіною) нема ящика й нема комори кухаря зі стравами", Mathf.RoundToInt(DiningStraight)) : Lang.T("на базі нема жодного ящика зі стравами");
            if (raw.Count > 0) return Lang.F("у ящиках лише не готове: {0} — їдять тільки страви, зроблені на кухні", string.Join(", ", raw.ToArray()));
            return Lang.T("у ящиках нема страв, зроблених на кухні");
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
        private static readonly Dictionary<NpcMind, KeyValuePair<int, string>> Was = new Dictionary<NpcMind, KeyValuePair<int, string>>();
        private static void Trace(NpcMind m, NpcMind.Plan p)
        {
            KeyValuePair<int, string> was;
            if (Was.TryGetValue(m, out was) && was.Key == p.Stage && was.Value == m.Status) return; // (every frame of every meal: its text made only when it changed)
            if (Was.Count > 50) Was.Clear();
            Was[m] = new KeyValuePair<int, string>(p.Stage, m.Status);
            GameObject tg = p.Stage == 1 ? NpcKit.Find(p.Who) : null;
            Container c = tg != null ? tg.GetComponentInChildren<Container>() : null;
            string now = p.Stage + " " + m.Status + (c != null ? " (" + Mathf.RoundToInt(Utils.DistanceXZ(m.transform.position, c.transform.position) * 10f) / 10f + " m from " + c.m_name + (c.IsInUse() ? ", open" : "") + ")" : "");
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
            finally { Trace(m, p); }
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
                    if (!r.Base.Ready) { m.Status = Lang.K("думає, де поїсти"); return Time.time - p.BestAt > 30f; } // (its base still being looked over)
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
                    if (c == null) { p.Stage = 0; return p.Tries++ > 2f; } // (gone: another pantry, a few tries)
                    m.Status = Lang.K("іде їсти");
                    if (!m.Walk(dt, c.transform.position, 1.8f)) return false;
                    ZNetView nv = c.GetComponentInParent<ZNetView>();
                    if (nv == null || !nv.IsValid()) return true;
                    if (nv.GetZDO().GetInt(ZDOVars.s_inUse, 0) != 0 && !NpcLid.Mine(c, a)) { m.Status = Lang.K("чекає біля комори"); if (Time.time - p.Since > 25f) { SetBusy(c); p.Stage = 0; return p.Tries++ > 2f; } return false; } // (a player has it open: it waits a while — most close it soon — then another chest with food)
                    // its hand into the chest, as a player reaches into one: a moment facing it, then the bite
                    if (!m.Reach(c, ref p.Hand)) { m.Status = Lang.K("бере їжу"); return false; } // (its hand into it: NpcMind.Reach)
                    ItemDrop.ItemData bite = Plainest(c.GetInventory());
                    if (bite == null) { p.Stage = 0; return p.Tries++ > 2f; } // (eaten up meanwhile)
                    p.Food = bite.m_dropPrefab != null ? bite.m_dropPrefab.name : bite.m_shared.m_name; // (its prefab: its look in the hand, its name when eaten)
                    p.Count = Fill(bite); // (how much it will fill)
                    c.GetInventory().RemoveItem(bite, 1);
                    if (Plainest(c.GetInventory()) == null) LastBite(a, c.transform.position); // (the pantry empty now: one hungry near holds it against it)
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
                    p.Stage = 3; p.Since = Time.time; p.Clock = Random.Range(EatMin, EatMax);
                    NpcEat.Set(a, p.Food); // (the bite in its hand, to its mouth: seen on every game)
                    if (Random.value < 0.3f) NpcChatter.Mutter(a, "meal");
                    return false;
                default:
                    if (!p.Seated) { NpcEat.Set(a, ""); p.Stage = 2; p.Started = false; return false; } // (stood up: sits again, the bite still in hand)
                    m.SitDown(dt, p);
                    m.Status = Lang.K("їсть");
                    if (Time.time - p.Since < p.Clock) return false;
                    if (a.Hunger != null) { GameObject fp = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(p.Food) : null; ItemDrop fd = fp != null ? fp.GetComponent<ItemDrop>() : null; a.Hunger.Eat(p.Count, fd != null && Localization.instance != null ? Localization.instance.Localize(fd.m_itemData.m_shared.m_name).ToLowerInvariant() : ""); }
                    NpcEat.Set(a, "");
                    Npcs.Log(a.Id + ": ate " + p.Food + " (+" + Mathf.RoundToInt(p.Count) + ", now " + Mathf.RoundToInt(NpcHunger.Full(a)) + ")");
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

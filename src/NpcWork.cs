#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A resident's work (npc-plan.md, stage 2): its job and its supply chest (both given by a player, kept in its ZDO);
    // in the day (NpcResident) the mind's Job layer holds a «work» plan and this does it, one errand after another:
    //   find something to do in its base (the job's own rule) → take what it needs from its chest → go there → do it
    //   (a little at a time) → pick up what came of it → carry it to the chest → a short breath → again.
    // Jobs (each one entry, NpcJobs.cs):
    //   stoker    fires and torches in the base: fuel from the chest when they burn low
    //   smelter   smelters, kilns, furnaces: empties what is done, adds fuel and ore from the chest, bars to the chest
    //   gatherer  bushes and plants in the base that grow back (berries, mushrooms…): picks them, all to the chest
    //             (crops that do not grow back are left for the player: it does not replant yet)
    //   cleaner   things lying about in the base: picks them up, to the chest
    //   cook      cooking stations and ovens: the done off first (it hurries), fuel, the raw from the chest over a fire
    //   keeper    beehives (honey) and fermenting barrels (tapped when ready, filled from the chest when empty)
    //   guard     a fighter walking the base's edge, looking out (day and night; only for fighters)
    // Materials only from its own chest (the user's rule: nothing for free). What it carries is kept in its ZDO, not in
    // its body's pockets (a body's inventory is not saved), written once a step: nothing is lost when its owner leaves or
    // another game takes it over. A chest a player has open is left alone. Two workers never take the same thing, on
    // any game (Reserve: each one's thing of the moment in its own ZDO).
    // Runs where the NPC is owned.
    internal sealed class NpcWorker : MonoBehaviour
    {
        internal static readonly int KJob = "RavenWatch_npcJob".GetStableHashCode();     // string: the job id ("" none)
        internal static readonly int KChest = "RavenWatch_npcChest".GetStableHashCode(); // string "user:id" of its chest
        internal static readonly int KCarry = "RavenWatch_npcCarry".GetStableHashCode(); // string: what it carries (base64)
        internal static readonly int KPost = "RavenWatch_npcPost".GetStableHashCode();     // vec3: a guard's post (none: zero)
        internal static readonly int KHouse = "RavenWatch_npcHouse".GetStableHashCode();   // vec3: a servant's house (the spot pointed at; none: zero)
        internal static readonly int KStore = "RavenWatch_npcStore".GetStableHashCode();   // vec3: a servant's storeroom (the spot pointed at; none: zero)
        internal const float StoreReach = 6f; // (m round its spot: the storeroom's chests)
        internal static readonly int KInbox = "RavenWatch_npcInbox".GetStableHashCode();   // link: a servant's chest to sort out (what you bring for it to put away)
        internal static readonly int KNowhere = "RavenWatch_npcNowhere".GetStableHashCode(); // string: the things it found no place for («$item_…,…»)
        internal static readonly int KPatrol = "RavenWatch_npcPatrol".GetStableHashCode(); // bool: a guard walks the whole base (not its edge)
        internal const float PostRound = 10f, PostReach = 25f; // m: a guard walks round its post this far; fights foes this near it
        internal static readonly int KTarget = "RavenWatch_npcTarget".GetStableHashCode(); // string "user:id": the thing it works on now (Reserve)

        private const float Breath = 4f, StepEvery = 0.7f, PickupTime = 5f, Rescan = 6f;

        private NpcActor _a;
        private NpcMind _mind;
        private Inventory _carry;
        private string _carryLoaded;
        private bool _carryDirty;          // changed here, not yet written (one write a step, not one a log)
        private Container _chestNow;       // its chest, found once a thought
        private string _targetSeen; private ZDOID _target = ZDOID.None;

        // the errand under way
        private enum Stage { Find, ToChest, ToTarget, Use, Collect, Store, Rest }
        private Stage _stage = Stage.Find;
        private Errand _e;
        private float _next, _stageSince;
        internal string Doing = "";

        internal sealed class Errand
        {
            public ZDOID Target = ZDOID.None;
            public Vector3 At;
            public float Near = 2f;
            public string Take; public int Count;       // to fetch from the chest first ("" nothing)
            public Func<NpcWorker, Errand, bool> Use;   // a step at the target; true: done
            public HashSet<string> Collect;            // after: drops of these (prefab names) near At go into its carry
            public string Word = "";
            public bool Urgent;                         // it must not wait (a fire about to go out): a run to it, not a walk
        }

        private void Awake() { _a = GetComponent<NpcActor>(); _mind = GetComponent<NpcMind>(); }

        // ---------------------------------------------------------------- its job and chest
        internal string JobId { get { ZDO z = _a != null ? _a.Zdo : null; return z != null ? z.GetString(KJob, "") : ""; } }
        internal Job Job { get { return Jobs.Find(JobId); } }
        internal bool Works { get { return Job != null; } }

        internal ZDOID ChestId { get { return NpcKit.GetRef(_a != null ? _a.Zdo : null, KChest); } }

        internal Container Chest { get { GameObject go = NpcKit.Find(ChestId); return go != null ? go.GetComponentInChildren<Container>() : null; } }

        // its buttons on the person's round menu (NpcMenu)
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 80, Title = "Робота", IconNow = a => { Job j = a.Worker != null ? a.Worker.Job : null; return j != null ? j.Icon : "n-job"; },
                Desc = a => Lang.T("Зараз: ") + Jobs.Word(a.Worker != null ? a.Worker.JobId : "") + "." + (a.Worker != null && a.Worker.Works ? " " + Lang.F("Ремесло: {0}%. Платня: {1} раз на 5 днів.", Mathf.RoundToInt(NpcToil.Craft(a, a.Worker.JobId) * 100f), NpcToil.Wage(a)) : ""),
                Now = a => a.Worker != null && a.Worker.Works, Not = a => NpcResident.Is(a) ? null : Lang.T("Спершу оселі його: працюють на своїй базі."),
                Sub = a => JobRing() };
            // a cook's chest is the base's pantry (NpcMeals): what it cooks goes there, the residents eat from it
            yield return new NpcMenu.Button { Order = 90, Title = "Ящик", Icon = "n-chest", Place = "на ящик", Shown = a => NpcResident.Is(a) && a.Worker != null && a.Worker.Job != null && a.Worker.Job.NeedsChest, Where = a => a.Worker != null ? NpcKit.WhereIs(a.Worker.ChestId) : null,
                TitleNow = a => IsCook(a) ? Lang.T("Комора") : Lang.T("Ящик"), IconNow = a => IsCook(a) ? "n-pantry" : "n-chest",
                Desc = a => IsCook(a) ? Lang.T("Комора кухаря: звідти бере сире, туди складає готове — а мешканці бази звідти їдять.") : Lang.T("Його ящик запасів: звідти бере, туди складає."), Now = a => a.Worker != null && !a.Worker.ChestId.IsNone(),
                AtPlace = SetChest };
            // a guard's post: where it stands its watch (round it it walks; foes it fights only near it)
            yield return new NpcMenu.Button { Order = 92, Title = "Пост", Icon = "n-post", Place = "на місце посту", Shown = a => a.Worker != null && a.Worker.JobId == "guard",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.Post(out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Де він вартує: стоїть там і обходить довкола (до {0} м). Б'є лише ворогів, що підходять до посту (до {1} м) — далі за ними не женеться; мирних звірів не чіпає.", Mathf.RoundToInt(PostRound), Mathf.RoundToInt(PostReach)),
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.Post(out at); }, AtPlace = SetPost };
            // a servant's storeroom: where it takes its supplies from and puts away what you bring
            yield return new NpcMenu.Button { Order = 97, Title = "Склад", Icon = "n-store", Place = "на місце складу", Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.StoreAt(out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Скрині довкола місця, яке покажеш (до {0} м): звідси він бере пальне для дому у свою скриню, сюди розкладає те, що ти приніс у його скриню. Нема місця на складі — шукає по скринях бази.", Mathf.RoundToInt(StoreReach)),
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.StoreAt(out at); }, AtPlace = SetStore };
            // a servant's chest to sort out: what you put in it, it carries off to the storeroom
            yield return new NpcMenu.Button { Order = 98, Title = "Розбір", Icon = "n-inbox", Place = "на скриню для розбору", Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Container c = a.Worker != null ? a.Worker.Inbox : null; return c != null ? (Vector3?)c.transform.position : null; },
                Desc = a => { string n = Nowhere(a); return Lang.T("Скриня, куди кладеш речі для нього: час від часу він її перевіряє і розносить усе на склад — туди, де лежить те саме, в будь-яку вільну скриню складу, а нема місця — по скринях бази (не в доми інших гравців). Сюжетні й особливі речі не чіпає.") + (n.Length > 0 ? "\n" + Lang.F("Не знайшов місця: {0}", n) : ""); },
                Now = a => a.Worker != null && a.Worker.Inbox != null, AtPlace = SetInbox };
            // a servant's house: another one, or the same again to see its edges
            yield return new NpcMenu.Button { Order = 94, Title = "Дім", Icon = "n-house", Place = "на дім, який доглядатиме", Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.HouseAt(out at) ? (Vector3?)at : null; },
                Desc = a => { NpcHouse h = a.Worker != null ? a.Worker.House : null; return Lang.T("Дім, який він доглядає: вогнища й світильники, підлога, двері, побиті стіни.") + (h != null ? h.Trouble() : ""); },
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.HouseAt(out at); }, AtPlace = SetHouse };
            // a lumberjack's, a miner's plot (NpcHew)
            yield return new NpcMenu.Button { Order = 96, Title = "Ділянка", Icon = "n-area", Place = "на ділянку", Shown = a => a.Worker != null && (a.Worker.JobId == "lumber" || a.Worker.JobId == "miner"),
                Where = a => { Vector3 at; return NpcHew.Area(a, out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Де він працює: {0} м довкола місця, на яке наведеш (до {1} м від бази). Поза базою він без її захисту.", Mathf.RoundToInt(NpcHew.Radius), Mathf.RoundToInt(NpcHew.FarFromBase)),
                Now = a => { Vector3 at; return NpcHew.Area(a, out at); }, AtPlace = NpcHew.SetArea };
            // a cleaner's (a servant's) choice of what it picks up (NpcTidy)
            yield return NpcTidy.Button();
            // …or a round of the whole base: anywhere in it, every foe that turns up in it
            yield return new NpcMenu.Button { Order = 93, Title = "Обхід бази", Icon = "n-patrol", Shown = a => a.Worker != null && a.Worker.JobId == "guard",
                Desc = a => Patrols(a) ? Lang.T("Зараз обходить усю базу. Натисни — знову межею (або дай йому пост).") : Lang.T("Ходить усією базою, куди схоче, і валить кожного ворога, що з'явиться в її межах. Пост тоді знімається."),
                Now = a => Patrols(a), Do = (a, p) => SetPatrol(a, !Patrols(a)) };
        }

        private static bool IsCook(NpcActor a) { return a.Worker != null && a.Worker.JobId == "cook"; }

        internal static string SetJob(NpcActor a, string id)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a) && !string.IsNullOrEmpty(id)) return "Спершу оселі його: працюють на своїй базі.";
            Job j = Jobs.Find(id);
            string cannot = j != null ? NpcLevers.NoJob(a, j.Id) ?? j.Can(a) : null; // (the story may forbid a job: NpcLevers)
            if (cannot != null) return cannot;
            a.Zdo.Set(KJob, j != null ? j.Id : "");
            // a settler fights by its role: on guard it fights; else it is wary (runs, fights back when caught)
            if (a.Def != null && a.Def.Generated && a.Mind != null) a.Mind.Do("fight " + (j != null && j.Id == "guard" ? "defend" : a.Def.Fight), NpcMind.Layer.Routine);
            a.Redress(); // (on guard: its weapons in hand; off it: on its back — NpcGear)
            NpcWorker w = a.GetComponent<NpcWorker>();
            if (w != null) { w.Reset(); if (j == null) w.DropCarry(); }
            return j != null ? "Робота: " + j.Word + (w != null && w.Chest == null && j.NeedsChest ? ". Дай йому ящик запасів." : ".") : "Без роботи.";
        }

        internal NpcActor Actor { get { return _a; } }

        internal bool Post(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? z.GetVec3(KPost, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }

        internal bool HouseAt(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? z.GetVec3(KHouse, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }
        internal NpcHouse House { get { Vector3 at; return HouseAt(out at) ? NpcHouse.For(at) : null; } }

        internal Container Inbox { get { GameObject go = NpcKit.Find(NpcKit.GetRef(_a != null ? _a.Zdo : null, KInbox)); return go != null ? go.GetComponentInChildren<Container>() : null; } }

        // its chest to sort out: the built chest pointed at (not its own)
        internal static string SetInbox(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його.");
            Container best = NpcKit.Nearest<Container>(aim, 3f, ct => ct.GetComponent<Piece>() != null);
            if (best == null) return Lang.T("Не бачу скрині там, куди ти дивишся.");
            ZNetView nv = best.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return Lang.T("Ця скриня ще не готова.");
            if (a.Worker != null && nv.GetZDO().m_uid == a.Worker.ChestId) return Lang.T("Це його робоча скриня — для розбору потрібна інша.");
            NpcKit.SetRef(a.Zdo, KInbox, nv.GetZDO().m_uid);
            a.Zdo.Set(KNowhere, "");
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return Lang.T("Скриня для розбору: що покладеш сюди, він рознесе на склад.");
        }

        // what it found no place for (its words for them), kept so every game can show it
        internal static string Nowhere(NpcActor a)
        {
            string s = a != null && a.Zdo != null ? a.Zdo.GetString(KNowhere, "") : "";
            if (s.Length == 0) return "";
            System.Collections.Generic.List<string> l = new System.Collections.Generic.List<string>();
            foreach (string n in s.Split(',')) if (n.Length > 0) l.Add(Localization.instance != null ? Localization.instance.Localize(n) : n);
            return string.Join(", ", l.ToArray());
        }

        internal void SetNowhere(System.Collections.Generic.ICollection<string> names)
        {
            if (_a == null || _a.Zdo == null || !_a.Owned) return;
            string s = string.Join(",", new System.Collections.Generic.List<string>(names).ToArray());
            if (_a.Zdo.GetString(KNowhere, "") != s) _a.Zdo.Set(KNowhere, s);
        }

        // what it carries now (a servant putting things away)
        internal List<ItemDrop.ItemData> Carried() { return new List<ItemDrop.ItemData>(Carry.GetAllItems()); }

        // a thing it never moves about: the story's, the ravens', a keepsake, one marked by a mod as its own, the game's
        // quest things — left where they lie
        internal static bool Untouchable(ItemDrop.ItemData it)
        {
            if (it == null || it.m_shared == null || it.m_shared.m_questItem || it.m_dropPrefab == null) return true;
            if (it.m_dropPrefab.name.StartsWith("RavenWatch_")) return true;
            if (it.m_customData != null) foreach (string k in it.m_customData.Keys) if (k.StartsWith("RavenWatch")) return true;
            return false;
        }

        internal bool StoreAt(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? z.GetVec3(KStore, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }

        // its storeroom: the chests round the spot pointed at (in its base)
        internal static string SetStore(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його.");
            if (!NpcBase.For(NpcRules.Home(a)).Inside(aim)) return Lang.T("Це поза його базою — склад має бути в ній.");
            int n = 0;
            foreach (Container c in a.Worker != null ? a.Worker.InBase<Container>() : new List<Container>()) if (c.GetComponent<Piece>() != null && Utils.DistanceXZ(c.transform.position, aim) <= StoreReach) n++;
            a.Zdo.Set(KStore, aim);
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return n > 0 ? Lang.F("Тут склад: скринь довкола — {0}.", n) : Lang.T("Тут склад — але скринь довкола поки нема.");
        }

        internal static string SetHouse(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return "Спершу оселі його: служать у своїй базі.";
            if (Utils.DistanceXZ(aim, NpcRules.Home(a)) > NpcBase.Reach) return "Цей дім далеко від його бази.";
            Player me = Player.m_localPlayer;
            if (me != null) { Vector3 back = me.transform.position - aim; back.y = 0f; if (back.sqrMagnitude > 0.01f) aim += back.normalized * 0.7f; } // (pointed at a wall: the floor before it)
            a.Zdo.Set(KHouse, aim);
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            NpcHouse.For(aim).ShowSoon(); // (its edges, to see which house it took)
            return "Цей дім тепер його турбота. Межі дому — бірюзові вогники.";
        }

        internal static bool Patrols(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null && z.GetBool(KPatrol, false); }

        internal static string SetPatrol(NpcActor a, bool on)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            a.Zdo.Set(KPatrol, on);
            if (on) a.Zdo.Set(KPost, Vector3.zero); // (one or the other)
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return on ? "Обходить усю базу." : "Знову обходить межу бази.";
        }

        internal static string SetPost(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcBase.For(NpcRules.Home(a)).Inside(aim)) return "Це поза його базою — пост має бути в ній.";
            a.Zdo.Set(KPost, aim);
            a.Zdo.Set(KPatrol, false); // (one or the other)
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return "Тут його пост.";
        }

        internal static string SetChest(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return "Спершу оселі його.";
            Container best = NpcKit.Nearest<Container>(aim, 3f, ct => ct.GetComponent<Piece>() != null); // (a built chest, not a ship's hold or a grave)
            if (best == null) return "Не бачу ящика там, куди ти дивишся.";
            ZNetView nv = best.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return "Цей ящик ще не готовий.";
            NpcKit.SetRef(a.Zdo, KChest, nv.GetZDO().m_uid);
            NpcWorker w = a.GetComponent<NpcWorker>();
            if (w != null) w.Reset();
            return "Тепер це його ящик запасів.";
        }

        // the jobs' ring (Робота): one button a job, and «Без роботи»
        private static List<NpcMenu.Button> _jobRing;
        private static List<NpcMenu.Button> JobRing()
        {
            if (_jobRing != null) return _jobRing;
            _jobRing = new List<NpcMenu.Button>();
            for (int i = 0; i < Jobs.All.Count; i++)
            {
                Job j = Jobs.All[i];
                _jobRing.Add(new NpcMenu.Button { Order = i, Title = j.Word.Substring(0, 1).ToUpperInvariant() + j.Word.Substring(1), Icon = j.Icon, Desc = a => Lang.T(j.Desc),
                    Now = a => a.Worker != null && a.Worker.JobId == j.Id, Not = a => NpcLevers.NoJob(a, j.Id) ?? j.Can(a), Do = (a, p) => SetJob(a, j.Id),
                    Place = j.Place, AtPlace = j.Place != null ? (Func<NpcActor, Vector3, string>)((a, at) => j.Assign(a, at)) : null }); // (a servant: pointed at its house)
            }
            _jobRing.Add(new NpcMenu.Button { Order = 99, Title = "Без роботи", Icon = "n-none", Desc = a => Lang.T("Не працює: удень походжає базою."),
                Now = a => a.Worker == null || !a.Worker.Works, Do = (a, p) => SetJob(a, "") });
            return _jobRing;
        }

        // ---------------------------------------------------------------- what it carries (kept in its locker: NpcLocker)
        private Inventory Carry
        {
            get
            {
                string s = NpcLocker.Get(_a, KCarry);
                if (_carry == null) _carry = new Inventory("npc_carry", null, 8, 2);
                if (s != null && s != _carryLoaded && !_carryDirty) // (not here yet: as it was; its own unwritten changes are newer)
                {
                    _carryLoaded = s;
                    _carry.RemoveAll();
                    if (s.Length > 0) { try { _carry.Load(new ZPackage(s)); } catch (Exception e) { Npcs.Log(Name + ": what it carries could not be read: " + e.Message); } }
                }
                return _carry;
            }
        }

        private void FlushCarry() { if (_carryDirty) SaveCarry(); }

        private void SaveCarry()
        {
            if (_a == null || _a.Zdo == null || _carry == null) return;
            if (!NpcLocker.Ready(_a) || (_carryLoaded == null && NpcLocker.Has(_a))) { _carryDirty = true; return; } // (its locker not read here yet: written later, never over it unread)
            _carryDirty = false;
            ZPackage p = new ZPackage();
            _carry.Save(p);
            string s = _carry.NrOfItems() > 0 ? p.GetBase64() : "";
            if (s == _carryLoaded) return;
            if (NpcLocker.Set(_a, KCarry, s)) _carryLoaded = s; else _carryDirty = true; // (its locker not here yet: written later)
        }

        internal int Carrying(string name) { return Carry.CountItems(name); }
        internal bool CarriesAnything { get { return Carry.NrOfItems() > 0; } }
        internal bool Room { get { return Carry.GetEmptySlots() >= 2; } }

        // what it carries put down at its feet (taken away, sent off, its job taken from it: nothing vanishes with it)
        internal void DropCarry()
        {
            if (_a == null || !_a.Owned || !NpcLocker.Ready(_a) || !CarriesAnything) return;
            Vector3 at = transform.position + transform.forward * 0.8f + Vector3.up * 0.5f;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(Carry.GetAllItems())) ItemDrop.DropItem(it, it.m_stack, at, Quaternion.identity);
            Carry.RemoveAll();
            SaveCarry();
            Npcs.Log(Name + ": put down what it carried");
        }

        // one of what it carries used up (fuel into a fire, ore into a smelter)
        internal bool Spend(string name)
        {
            if (Carry.CountItems(name) <= 0) return false;
            Carry.RemoveItem(name, 1);
            _carryDirty = true; // (written when the step ends)
            return true;
        }

        // a drop on the ground into its carry (asks for it first when another game owns it); true when it has it
        internal bool PickUp(ItemDrop d)
        {
            if (d == null) return true;
            ZNetView nv = d.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return true;
            if (!d.CanPickup(false)) { d.RequestOwn(); return false; }
            d.Load();
            ItemDrop.ItemData it = d.m_itemData.Clone();
            if (!Carry.CanAddItem(it)) return false;
            Carry.AddItem(it);
            _carryDirty = true; // (written when the step ends)
            ZNetScene.instance.Destroy(d.gameObject);
            return true;
        }

        // ---------------------------------------------------------------- the chest (left alone while a player has it open)
        private bool OpenChest(Container c)
        {
            return _mind != null && _mind.Open(c, 1.2f); // (open by a player or another: not now — NpcMind.Open, the one way)
        }

        internal int InChest(string name) { Container c = _chestNow != null ? _chestNow : Chest; return c != null ? c.GetInventory().CountItems(name) : 0; }

        // how much of a thing fits in an inventory: room on its stacks of the same, and its empty cells
        internal static int Space(Inventory inv, ItemDrop.ItemData it)
        {
            if (inv == null || it == null) return 0;
            int max = Mathf.Max(1, it.m_shared.m_maxStackSize);
            return (max > 1 ? inv.FindFreeStackSpace(it.m_shared.m_name, it.m_worldLevel) : 0) + inv.GetEmptySlots() * max;
        }

        // some of it moved from one inventory to another, as much as fits: how many went
        private static int Move(Inventory from, Inventory to, ItemDrop.ItemData it, int count)
        {
            int k = Mathf.Min(Mathf.Min(count, it.m_stack), Space(to, it));
            if (k <= 0) return 0;
            ItemDrop.ItemData part = it.Clone(); part.m_stack = k;
            if (!to.AddItem(part)) return 0;
            from.RemoveItem(it, k);
            return k;
        }

        private int TakeFromChest(Container c, string name, int count)
        {
            if (!OpenChest(c)) return -1;
            Inventory chest = c.GetInventory(), carry = Carry;
            int took = 0;
            while (took < count)
            {
                ItemDrop.ItemData it = chest.GetItem(name);
                if (it == null) break;
                int k = Move(chest, carry, it, count - took);
                if (k <= 0) break; // (its hands full)
                took += k;
            }
            if (took > 0) SaveCarry();
            return took;
        }

        // another chest than its own (a servant's sorting, refilling: NpcJobs): some of a thing out of it into its carry,
        // all it carries of a thing into it (its count; −1 the chest not to be had now — open, someone else's)
        internal int TakeFrom(Container c, string name, int count) { return TakeFromChest(c, name, count); }

        internal int PutInto(Container c, string name)
        {
            if (!OpenChest(c)) return -1;
            Inventory chest = c.GetInventory(), carry = Carry;
            int put = 0;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(carry.GetAllItems()))
                if (it.m_shared.m_name == name) put += Move(carry, chest, it, it.m_stack); // (as much as fits: the rest it keeps, found a place next)
            if (put > 0) SaveCarry();
            return put;
        }

        // a chest a worker of its base keeps its supplies in
        internal static bool IsWorkChest(ZDOID id)
        {
            if (id.IsNone()) return false;
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.Worker != null && o.Worker.Works && o.Worker.ChestId == id) return true;
            return false;
        }

        private bool StoreInChest(Container c)
        {
            if (!OpenChest(c)) return false;
            Inventory chest = c.GetInventory(), carry = Carry;
            bool full = false;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(carry.GetAllItems()))
            { int had = it.m_stack; if (Move(carry, chest, it, had) < had) full = true; } // (as much as fits)
            SaveCarry();
            if (full) Npcs.Log(Name + ": its chest is full");
            return !full;
        }

        // ---------------------------------------------------------------- the errands (NpcMind: Task.Work, every thought)
        internal void Reset()
        {
            if (_e != null && _stage != Stage.Find && Time.time - _stageSince < 0.5f) return; // (an errand just found by Ready(): kept)
            FlushCarry();
            SetTarget(ZDOID.None);
            NpcHew.PutAway(_a); // (an axe, a pickaxe in its hand: away)
            _e = null; _stage = Stage.Find; _stageSince = Time.time; _next = 0f;
        }

        private void Go(Stage s) { FlushCarry(); _stage = s; _stageSince = Time.time; _next = 0f; if (_mind != null) _mind.NewGoal(); }

        // nothing it can do now (no job, no chest, nothing to do and nothing to carry): its mind lets its day go on meanwhile
        internal bool Idle;
        internal string Why; // (a job's word why it finds nothing: «потрібна сокира…» — shown instead of «нема роботи»)

        // a look whether there is work now (while it stepped aside): true when there is — the errand is taken up at once
        internal bool Ready()
        {
            Job job = Job;
            Container chest = _chestNow = Chest;
            if (job == null || (chest == null && job.NeedsChest)) return false;
            if (CarriesAnything) { _e = null; _stage = Stage.Store; Idle = false; return true; }
            NpcWorker.Errand e = job.Find(this);
            if (e == null) return false;
            _e = e; SetTarget(e.Target); Doing = e.Word; Idle = false;
            _stage = !string.IsNullOrEmpty(e.Take) && Carrying(e.Take) < e.Count ? Stage.ToChest : Stage.ToTarget;
            _stageSince = Time.time; _next = 0f;
            return true;
        }

        internal void Step(float dt)
        {
            if (_mind == null || _a == null) return;
            Idle = false;
            Job job = Job;
            Container chest = _chestNow = Chest;
            if (job == null) { _mind.Halt(); Doing = "без роботи"; Idle = true; return; }
            if (job.NeedsChest && !NpcLocker.Ready(_a)) { _mind.Halt(); Doing = "чекає свої речі"; Idle = true; return; } // (its locker on its way here)
            if (chest == null && job.NeedsChest) { _mind.Halt(); Doing = ChestId.IsNone() ? "чекає на ящик запасів" : "ящик запасів далеко"; Idle = true; return; }
            switch (_stage)
            {
                case Stage.Find:
                    if (Time.time < _next) { _mind.Halt(); return; }
                    _next = Time.time + Rescan;
                    Why = null;
                    _e = job.Find(this);
                    if (_e == null)
                    {
                        if (CarriesAnything) { Go(Stage.Store); return; }
                        _mind.Halt(); Doing = Why ?? "нема роботи"; Idle = true; return;
                    }
                    SetTarget(_e.Target);
                    Doing = _e.Word;
                    Go(!string.IsNullOrEmpty(_e.Take) && Carrying(_e.Take) < _e.Count ? Stage.ToChest : Stage.ToTarget);
                    return;
                case Stage.ToChest:
                    Doing = "іде до ящика";
                    if (!_mind.Walk(dt, chest.transform.position, 2f, _e != null && _e.Urgent)) return;
                    if (CarriesAnything && _e != null) StoreExcept(chest, _e.Take); // (leftovers of another errand back first)
                    int want = _e != null ? _e.Count - Carrying(_e.Take) : 0;
                    int got = want > 0 ? TakeFromChest(chest, _e.Take, want) : 0;
                    if (got < 0) { Doing = "ящик відкритий — чекає"; _mind.Halt(); return; } // (a player is in it)
                    if (got == 0 && Carrying(_e.Take) == 0) { Drop(); return; }
                    Go(Stage.ToTarget);
                    return;
                case Stage.ToTarget:
                    Doing = _e.Word;
                    if (!_mind.Walk(dt, _e.At, _e.Near, _e.Urgent)) return;
                    Go(Stage.Use);
                    return;
                case Stage.Use:
                    _mind.Halt(); _mind.Face(_e.At + Vector3.up);
                    if (Time.time < _next) return;
                    _next = Time.time + StepEvery * Ways.WorkStep * NpcHunger.Slow(_a) / NpcKnack.Of(_a, JobId) / NpcToil.Pace(_a); // (a diligent one quicker, a hungry one slower, one with a knack for it quicker still; its mood and its craft: NpcToil)
                    if (Time.time - _stageSince > 60f || _e.Use == null || _e.Use(this, _e)) Go(_e.Collect != null ? Stage.Collect : Stage.Rest);
                    return;
                case Stage.Collect:
                    _mind.Halt();
                    if (Time.time < _next) return;
                    _next = Time.time + 0.4f;
                    bool left = false;
                    foreach (ItemDrop d in Drops(_e.At, 3.5f))
                    {
                        if (!_e.Collect.Contains(d.gameObject.name.Replace("(Clone)", "").Trim())) continue;
                        if (!Room) break;
                        if (!PickUp(d)) left = true;
                    }
                    if ((left && Time.time - _stageSince < PickupTime) || Time.time - _stageSince < 1f) return; // (the drops a moment to land / to be handed over)
                    Go(!Room || job.StoreAfterEach ? Stage.Store : Stage.Rest);
                    return;
                case Stage.Store:
                    Doing = "несе до ящика";
                    if (!CarriesAnything || chest == null) { Go(Stage.Rest); return; } // (no chest: a job that needs none — what it still carries from an old job stays carried)
                    if (!_mind.Walk(dt, chest.transform.position, 2f, false)) return;
                    if (!StoreInChest(chest) && Time.time - _stageSince < 30f) { Doing = CarriesAnything ? "ящик повний або відкритий" : Doing; _mind.Halt(); return; }
                    Go(Stage.Rest);
                    return;
                default: // Rest: a breath between errands
                    _mind.Halt();
                    if (_e != null) { SetTarget(ZDOID.None); _e = null; }
                    if (Time.time - _stageSince < Breath * Ways.WorkBreath) return; // (a diligent one rests less)
                    _stage = Stage.Find; _next = 0f;
                    return;
            }
        }

        private void Drop() { SetTarget(ZDOID.None); _e = null; _stage = Stage.Rest; _stageSince = Time.time; Doing = "у ящику нема потрібного"; }

        private void StoreExcept(Container chest, string keep)
        {
            if (!OpenChest(chest)) return;
            Inventory c = chest.GetInventory();
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(Carry.GetAllItems()))
                if (it.m_shared.m_name != keep && c.CanAddItem(it)) { c.AddItem(it.Clone()); Carry.RemoveItem(it); }
            SaveCarry();
        }

        // the thing it works on now (every game reads it: Reserve), parsed only when it changed
        internal ZDOID Target
        {
            get
            {
                string t = _a != null && _a.Zdo != null ? _a.Zdo.GetString(KTarget, "") : "";
                if (t != _targetSeen) { _targetSeen = t; _target = NpcKit.Resolve(t); }
                return _target;
            }
        }

        private void SetTarget(ZDOID id)
        {
            if (_a == null || _a.Zdo == null || !_a.Owned || Target == id) return;
            NpcKit.SetRef(_a.Zdo, KTarget, id);
        }

        private NpcPersona Ways { get { return NpcPersona.Of(_a); } }

        internal ZDOID Me { get { return _a != null && _a.Zdo != null ? _a.Zdo.m_uid : ZDOID.None; } }
        private string Name { get { return _a != null ? _a.Id : "npc"; } }
        internal Vector3 Pos { get { return transform.position; } }
        internal NpcBase Base { get { return NpcBase.For(NpcRules.Home(_a)); } }

        internal string Describe()
        {
            Job j = Job;
            if (j == null) return "без роботи";
            return j.Word + (Chest == null && j.NeedsChest ? (ChestId.IsNone() ? ", без ящика" : ", ящик далеко") : "") + (Doing.Length > 0 ? " (" + Doing + ")" : "") + (CarriesAnything ? ", несе " + Carry.NrOfItemsIncludingStacks() : "");
        }

        // ---------------------------------------------------------------- the world round it
        internal static List<ItemDrop> Drops(Vector3 at, float r)
        {
            List<ItemDrop> l = new List<ItemDrop>();
            HashSet<ItemDrop> seen = new HashSet<ItemDrop>();
            foreach (Collider c in Physics.OverlapSphere(at, r, LayerMask.GetMask("item")))
            {
                ItemDrop d = c.GetComponentInParent<ItemDrop>();
                if (d != null && seen.Add(d)) l.Add(d);
            }
            return l;
        }

        // the things of this kind in its base, nearest first. One look per base and kind every few seconds, shared by
        // everyone working there (ten workers do not search ten times): built things (fires, smelters) from the game's own
        // list of pieces, no physics; the rest (bushes, things on the ground) by the layers they lie on only.
        private const float LookAgain = 5f;
        private static readonly Dictionary<string, KeyValuePair<float, List<Component>>> Seen = new Dictionary<string, KeyValuePair<float, List<Component>>>();
        private static object _seenWorld;

        internal List<T> InBase<T>() where T : Component { return InBaseOf<T>(Base, Pos); }

        // the same for anyone (meals: a base's chests and tables), nearest to «me» first
        internal static List<T> InBaseOf<T>(NpcBase b, Vector3 me) where T : Component
        {
            if (!ReferenceEquals(_seenWorld, ZNet.instance)) { Seen.Clear(); _seenWorld = ZNet.instance; } // (another world: nothing of the old one kept)
            string key = typeof(T).Name + "@" + Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<Component>> had;
            List<Component> all = b.Things(typeof(T)); // (the base's own look at its things: NpcBase — one for everyone there)
            if (all == null && Seen.TryGetValue(key, out had) && Time.time - had.Key < LookAgain) all = had.Value;
            else if (all == null)
            {
                all = new List<Component>();
                HashSet<Component> seen = new HashSet<Component>();
                if (typeof(T) == typeof(Fireplace) || typeof(T) == typeof(Smelter) || typeof(T) == typeof(CookingStation) || typeof(T) == typeof(Beehive) || typeof(T) == typeof(Fermenter) || typeof(T) == typeof(Container) || typeof(T) == typeof(Bed) || typeof(T) == typeof(Chair))
                {
                    List<Piece> pieces = new List<Piece>();
                    Piece.GetAllPiecesInRadius(b.Centre, NpcBase.Reach, pieces);
                    foreach (Piece pc in pieces) { T t = pc != null ? pc.GetComponentInChildren<T>() : null; if (t != null && seen.Add(t) && b.Inside(t.transform.position)) all.Add(t); }
                }
                else
                {
                    int mask = typeof(T) == typeof(ItemDrop) ? LayerMask.GetMask("item") : LayerMask.GetMask("piece_nonsolid", "Default", "Default_small", "item");
                    foreach (Collider c in Physics.OverlapSphere(b.Centre, NpcBase.Reach, mask))
                    {
                        T t = c.GetComponentInParent<T>();
                        if (t != null && seen.Add(t) && b.Inside(t.transform.position)) all.Add(t);
                    }
                }
                Seen[key] = new KeyValuePair<float, List<Component>>(Time.time, all);
            }
            List<T> l = new List<T>(all.Count);
            foreach (Component c in all) if (c != null) l.Add((T)c); // (gone since: left out)
            l.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
            return l;
        }

    }

    // ---------------------------------------------------------------- two workers never on one thing
    // A worker's thing of the moment is in its own ZDO (NpcWorker.KTarget, written by its owner once an errand, cleared
    // after): every game sees every worker's, with no message of its own. Free = no other worker near (loaded here)
    // has it.
    internal static class Reserve
    {
        internal static bool IsFree(ZDOID thing, ZDOID me)
        {
            if (thing.IsNone()) return true;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Zdo == null || o.Zdo.m_uid == me) continue;
                NpcWorker w = o.Worker;
                if (w != null && w.Target == thing) return false;
            }
            return true;
        }
    }
}
#endif

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
    //   cook      cooking stations and ovens: the done off first, fuel, the raw from the chest over a fire
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
        internal static readonly int KSupply = "RavenWatch_npcSupply".GetStableHashCode(); // link: its second chest, where it takes from (a cook's «Інгредієнти»: Job.HasSupply) — none: its chest serves both
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
        private Container _chestNow;       // its chest, looked up once a second (a link read, its piece found: not every frame); told another: at once
        private Container _supplyNow;      // its second chest, looked up with it (only for a job that has one: Job.HasSupply)
        private float _chestLook;
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
            public string Take; public int Count;       // to fetch from the chest first ("" nothing): as much of it as there is, up to Count
            public List<KeyValuePair<string, int>> Also; // …and these, each in full (a dish's ingredients: NpcKitchen) — one short: the errand is dropped
            public bool Made;                           // it made something into its hands at the thing (a dish): to its chest next, not on to another errand
            public Func<NpcWorker, Errand, bool> Use;   // a step at the target; true: done
            public HashSet<string> Collect;            // after: drops of these (prefab names) near At go into its carry
            public string Word = "";
            public bool Urgent;                         // it must not wait (a fire about to go out): first among its errands; it still walks there (NpcMind.PaceRule)
            public string Move;                         // its body's move there (a clip); null: its job's (Job.Move), "" none
        }

        private void Awake() { _a = GetComponent<NpcActor>(); _mind = GetComponent<NpcMind>(); }

        // ---------------------------------------------------------------- its job and chest
        internal string JobId { get { ZDO z = _a != null ? _a.Zdo : null; return z != null ? NpcLocker.Text(z, KJob) : ""; } }
        internal Job Job { get { return Jobs.Find(JobId); } }
        internal bool Works { get { return Job != null; } }

        internal ZDOID ChestId { get { return NpcLocker.Ref(_a != null ? _a.Zdo : null, KChest); } }

        internal Container Chest { get { GameObject go = NpcKit.Find(ChestId); return go != null ? go.GetComponentInChildren<Container>() : null; } }

        // its second chest (Job.HasSupply: a cook's ingredients): what its errands take comes from it, its own chest only receives what the job keeps
        // (Job.Keeps: the dishes), the rest of its hands goes back to this one. Not set, or not its job's: its chest serves both, as for every other job
        internal ZDOID SupplyId { get { Job j = Job; return j != null && j.HasSupply ? NpcLocker.Ref(_a != null ? _a.Zdo : null, KSupply) : ZDOID.None; } }
        internal Container Supply { get { ZDOID id = SupplyId; GameObject go = id.IsNone() ? null : NpcKit.Find(id); return go != null ? go.GetComponentInChildren<Container>() : null; } }
        // where it takes from — in its errands (looked up once a second: From), asked from outside them (its card, on any game: Source)
        private Container From { get { return _supplyNow != null ? _supplyNow : _chestNow != null ? _chestNow : Chest; } }
        internal Container Source { get { Container s = Supply; return s != null ? s : Chest; } }
        internal Container ChestNow { get { return _chestNow != null ? _chestNow : Chest; } }

        // its button on the person's round menu (NpcMenu): «Робота» — its ring is the job's own settings (set once: its chest,
        // post, house…) with «Змінити роботу» first; no job, or a job with nothing to set: the jobs' ring at once
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 50, Part = "work", Title = Lang.K("Робота"), IconNow = a => { Job j = a.Worker != null ? a.Worker.Job : null; return j != null ? j.Icon : "n-job"; },
                Desc = a => Lang.T("Зараз: ") + Jobs.Word(a.Worker != null ? a.Worker.JobId : "") + "." + (a.Worker != null && a.Worker.Works ? " " + Lang.F("Ремесло: {0}%. Платня: {1} раз на 5 днів.", Mathf.RoundToInt(NpcToil.Craft(a, a.Worker.JobId) * 100f), NpcToil.Wage(a)) : ""),
                Now = a => a.Worker != null && a.Worker.Works, Not = a => NpcResident.Is(a) ? null : Lang.T("Спершу оселі його: працюють на своїй базі."),
                Sub = WorkRing };
        }

        private static readonly NpcMenu.Button ChangeJob = new NpcMenu.Button { Order = 0, Title = Lang.K("Змінити роботу"), Icon = "n-job",
            Desc = a => Lang.T("Інша робота або без роботи."), Sub = a => JobRing() };
        private static List<NpcMenu.Button> _settings;

        // the ring under «Робота»: the settings its job has (made on each opening — a few buttons, read once)
        private static List<NpcMenu.Button> WorkRing(NpcActor a)
        {
            if (_settings == null) { _settings = new List<NpcMenu.Button>(Settings()); _settings.Sort((x, y) => x.Order.CompareTo(y.Order)); }
            List<NpcMenu.Button> ring = null;
            foreach (NpcMenu.Button b in _settings)
                if (b.Shown == null || b.Shown(a)) { if (ring == null) ring = new List<NpcMenu.Button> { ChangeJob }; ring.Add(b); }
            return ring ?? JobRing();
        }

        // a job's own settings, each for the jobs it names (Shown)
        private static IEnumerable<NpcMenu.Button> Settings()
        {
            // a cook's chest is the base's pantry (NpcMeals): what it cooks goes there, the residents eat from it
            yield return new NpcMenu.Button { Order = 90, Title = Lang.K("Ящик"), Icon = "n-chest", Place = Lang.K("на ящик"), Shown = a => NpcResident.Is(a) && a.Worker != null && a.Worker.Job != null && a.Worker.Job.HasChest, Where = a => a.Worker != null ? NpcKit.WhereIs(a.Worker.ChestId) : null,
                TitleNow = a => IsCook(a) ? Lang.T("Комора") : Lang.T("Ящик"), IconNow = a => IsCook(a) ? "n-pantry" : "n-chest",
                Desc = a => IsCook(a) ? Lang.F("Комора кухаря: туди складає готове, звідти їдять мешканці. Кухня — знаряддя до {0} м від комори. Без окремої скрині інгредієнтів бере сире теж звідси.", Mathf.RoundToInt(NpcKitchen.Reach)) : Lang.T("Його ящик запасів: звідти бере, туди складає."), Now = a => a.Worker != null && !a.Worker.ChestId.IsNone(),
                AtPlace = (a, at) => NpcMenu.OnOwner(a, "chest", at, "") };
            // a cook's second chest: what it cooks from (anywhere); and what to cook (NpcKitchen)
            yield return new NpcMenu.Button { Order = 91, Title = Lang.K("Інгредієнти"), Icon = "n-ingredients", Place = Lang.K("на скриню інгредієнтів"), Shown = a => NpcResident.Is(a) && IsCook(a), Where = a => a.Worker != null ? NpcKit.WhereIs(a.Worker.SupplyId) : null,
                Desc = a => Lang.T("Скриня, з якої кухар бере все для страв і пальне: стояти може де завгодно. Готує з усього, що в ній лежить. Покажи її ще раз — і він знову братиме з комори."), Now = a => a.Worker != null && !a.Worker.SupplyId.IsNone(),
                AtPlace = (a, at) => NpcMenu.OnOwner(a, "supply", at, "") };
            yield return NpcKitchen.Button();
            // a guard's post: where it stands its watch (round it it walks; foes it fights only near it)
            yield return new NpcMenu.Button { Order = 92, Title = Lang.K("Пост"), Icon = "n-post", Place = Lang.K("на місце посту"), Shown = a => a.Worker != null && a.Worker.JobId == "guard",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.Post(out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Де він вартує: стоїть там і обходить довкола (до {0} м). Б'є лише ворогів, що підходять до посту (до {1} м) — далі за ними не женеться; мирних звірів не чіпає.", Mathf.RoundToInt(PostRound), Mathf.RoundToInt(PostReach)),
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.Post(out at); }, AtPlace = (a, at) => NpcMenu.OnOwner(a, "post", at, "") };
            // a servant's storeroom: where it takes its supplies from and puts away what you bring
            yield return new NpcMenu.Button { Order = 97, Title = Lang.K("Склад"), Icon = "n-store", Place = Lang.K("на місце складу"), Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.StoreAt(out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Скрині довкола місця, яке покажеш (до {0} м): звідси він бере пальне для дому у свою скриню, сюди розкладає те, що ти приніс у його скриню. Нема місця на складі — шукає по скринях бази.", Mathf.RoundToInt(StoreReach)),
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.StoreAt(out at); }, AtPlace = (a, at) => NpcMenu.OnOwner(a, "store", at, "") };
            // a servant's chest to sort out: what you put in it, it carries off to the storeroom
            yield return new NpcMenu.Button { Order = 98, Title = Lang.K("Розбір"), Icon = "n-inbox", Place = Lang.K("на скриню для розбору"), Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Container c = a.Worker != null ? a.Worker.Inbox : null; return c != null ? (Vector3?)c.transform.position : null; },
                Desc = a => { string n = Nowhere(a); return Lang.T("Скриня, куди кладеш речі для нього: час від часу він її перевіряє і розносить усе на склад — туди, де лежить те саме, в будь-яку вільну скриню складу, а нема місця — по скринях бази (не в доми інших гравців). Сюжетні й особливі речі не чіпає.") + (n.Length > 0 ? "\n" + Lang.F("Не знайшов місця: {0}", n) : ""); },
                Now = a => a.Worker != null && a.Worker.Inbox != null, AtPlace = (a, at) => NpcMenu.OnOwner(a, "inbox", at, "") };
            // a servant's house: another one, or the same again to see its edges
            yield return new NpcMenu.Button { Order = 94, Title = Lang.K("Дім"), Icon = "n-house", Place = Lang.K("на дім, який доглядатиме"), Shown = a => a.Worker != null && a.Worker.JobId == "servant",
                Where = a => { Vector3 at; return a.Worker != null && a.Worker.HouseAt(out at) ? (Vector3?)at : null; },
                Desc = a => { NpcHouse h = a.Worker != null ? a.Worker.House : null; return Lang.T("Дім, який він доглядає: вогнища й світильники, підлога, двері, побиті стіни.") + (h != null ? h.Trouble() : ""); },
                Now = a => { Vector3 at; return a.Worker != null && a.Worker.HouseAt(out at); }, AtPlace = (a, at) => NpcMenu.OnOwner(a, "house", at, "") };
            // a lumberjack's, a miner's plot (NpcHew)
            yield return new NpcMenu.Button { Order = 96, Title = Lang.K("Ділянка"), Icon = "n-area", Place = Lang.K("на ділянку"), Shown = a => a.Worker != null && (a.Worker.JobId == "lumber" || a.Worker.JobId == "miner"),
                Where = a => { Vector3 at; return NpcHew.Area(a, out at) ? (Vector3?)at : null; },
                Desc = a => Lang.F("Де він працює: {0} м довкола місця, на яке наведеш (до {1} м від бази). Поза базою він без її захисту.", Mathf.RoundToInt(NpcHew.Radius), Mathf.RoundToInt(NpcHew.FarFromBase)),
                Now = a => { Vector3 at; return NpcHew.Area(a, out at); }, AtPlace = (a, at) => NpcMenu.OnOwner(a, "area", at, "") };
            // a cleaner's (a servant's) choice of what it picks up (NpcTidy)
            yield return NpcTidy.Button();
            // …or a round of the whole base: anywhere in it, every foe that turns up in it
            yield return new NpcMenu.Button { Order = 93, Title = Lang.K("Обхід бази"), Icon = "n-patrol", Shown = a => a.Worker != null && a.Worker.JobId == "guard",
                Desc = a => Patrols(a) ? Lang.T("Зараз обходить усю базу. Натисни — знову межею (або дай йому пост).") : Lang.T("Ходить усією базою, куди схоче, і валить кожного ворога, що з'явиться в її межах. Пост тоді знімається."),
                Now = a => Patrols(a), Do = (a, p) => NpcMenu.OnOwner(a, "patrol", p.transform.position, "") };
        }

        private static bool IsCook(NpcActor a) { return a.Worker != null && a.Worker.JobId == "cook"; }

        // why this one will not take this job (null: it will): the story's word (NpcLevers), who it is (NpcNature: however it stands with you), the job's own rule. The button and SetJob both ask here
        internal static string WhyNot(NpcActor a, Job j) { return j == null ? null : NpcLevers.NoJob(a, j.Id) ?? NpcNature.WontTake(a, j.Id) ?? j.Can(a); }

        // giver: the player who gave it this work from its menu (a wish to learn it comes true by him); none — the console, the story, its own leaving: nobody's gift
        internal static string SetJob(NpcActor a, string id, Player giver = null)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a) && !string.IsNullOrEmpty(id)) return Lang.T("Спершу оселі його: працюють на своїй базі.");
            Job j = Jobs.Find(id);
            string cannot = WhyNot(a, j);
            if (cannot != null) return cannot;
            NpcLocker.Put(a.Zdo, KJob, j != null ? j.Id : "");
            // a settler fights by its role: on guard it fights; else by its nature (NpcNature.Manner)
            if (a.Def != null && a.Def.Generated && a.Mind != null) a.Mind.Do("fight " + (j != null && j.Id == "guard" ? "defend" : NpcNature.Manner(a)), NpcMind.Layer.Routine);
            a.Redress(); // (on guard: its weapons in hand; off it: on its back — NpcGear)
            NpcWorker w = a.GetComponent<NpcWorker>();
            if (w != null) { w.Reset(); if (j == null) w.DropCarry(); }
            if (j != null && giver != null) NpcGoals.JobGiven(a, giver, j.Id); // (the work it wished to learn, from you)
            return j == null ? Lang.T("Без роботи.") : w != null && w.Chest == null && j.NeedsChest ? Lang.F("Робота: {0}. Дай йому ящик запасів.", Lang.T(j.Word)) : Lang.F("Робота: {0}.", Lang.T(j.Word));
        }

        internal NpcActor Actor { get { return _a; } }

        internal bool Post(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? NpcLocker.Vec(z, KPost, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }

        internal bool HouseAt(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? NpcLocker.Vec(z, KHouse, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }
        internal NpcHouse House { get { Vector3 at; return HouseAt(out at) ? NpcHouse.For(at) : null; } }

        internal Container Inbox { get { GameObject go = NpcKit.Find(NpcLocker.Ref(_a != null ? _a.Zdo : null, KInbox)); return go != null ? go.GetComponentInChildren<Container>() : null; } }

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
            NpcLocker.PutRef(a.Zdo, KInbox, nv.GetZDO().m_uid);
            NpcLocker.Put(a.Zdo, KNowhere, "");
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return Lang.T("Скриня для розбору: що покладеш сюди, він рознесе на склад.");
        }

        // what it found no place for (its words for them), kept so every game can show it
        internal static string Nowhere(NpcActor a)
        {
            string s = a != null && a.Zdo != null ? NpcLocker.Text(a.Zdo, KNowhere) : "";
            if (s.Length == 0) return "";
            System.Collections.Generic.List<string> l = new System.Collections.Generic.List<string>();
            foreach (string n in s.Split(',')) if (n.Length > 0) l.Add(Localization.instance != null ? Localization.instance.Localize(n) : n);
            return string.Join(", ", l.ToArray());
        }

        internal void SetNowhere(System.Collections.Generic.ICollection<string> names)
        {
            if (_a == null || _a.Zdo == null || !_a.Owned) return;
            string s = string.Join(",", new System.Collections.Generic.List<string>(names).ToArray());
            if (NpcLocker.Text(_a.Zdo, KNowhere) != s) NpcLocker.Put(_a.Zdo, KNowhere, s);
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

        // the same of a thing lying on the ground (a cleaner, a servant: Cleaner.Tidy): by what lies there — its prefab is the object's own name
        internal static bool Untouchable(ItemDrop d)
        {
            if (d == null || d.m_itemData == null || d.m_itemData.m_shared == null || d.m_itemData.m_shared.m_questItem) return true;
            if (d.gameObject.name.StartsWith("RavenWatch_", StringComparison.Ordinal)) return true;
            if (d.m_itemData.m_customData != null) foreach (string k in d.m_itemData.m_customData.Keys) if (k.StartsWith("RavenWatch", StringComparison.Ordinal)) return true;
            return false;
        }

        internal bool StoreAt(out Vector3 at) { ZDO z = _a != null ? _a.Zdo : null; at = z != null ? NpcLocker.Vec(z, KStore, Vector3.zero) : Vector3.zero; return at != Vector3.zero; }

        // its storeroom: the chests round the spot pointed at (in its base)
        internal static string SetStore(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його.");
            string outside = NpcBase.For(NpcRules.Home(a)).Refuse(aim); if (outside != null) return outside; // (the storeroom must be in it)
            int n = 0;
            foreach (Container c in a.Worker != null ? a.Worker.InBase<Container>() : new List<Container>()) if (c.GetComponent<Piece>() != null && Utils.DistanceXZ(c.transform.position, aim) <= StoreReach) n++;
            NpcLocker.Put(a.Zdo, KStore, aim);
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return n > 0 ? Lang.F("Тут склад: скринь довкола — {0}.", n) : Lang.T("Тут склад — але скринь довкола поки нема.");
        }

        internal static string SetHouse(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його: служать у своїй базі.");
            if (Utils.DistanceXZ(aim, NpcRules.Home(a)) > NpcBase.Reach) return Lang.T("Цей дім далеко від його бази.");
            Player me = NpcMenu.By;
            if (me != null) { Vector3 back = me.transform.position - aim; back.y = 0f; if (back.sqrMagnitude > 0.01f) aim += back.normalized * 0.7f; } // (pointed at a wall: the floor before it)
            NpcLocker.Put(a.Zdo, KHouse, aim);
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            NpcHouse.For(aim).ShowSoon(); // (its edges, to see which house it took)
            return Lang.T("Цей дім тепер його турбота. Межі дому — бірюзові вогники.");
        }

        internal static bool Patrols(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null && NpcLocker.Flag(z, KPatrol); }

        internal static string SetPatrol(NpcActor a, bool on)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            NpcLocker.Put(a.Zdo, KPatrol, on);
            if (on) NpcLocker.Put(a.Zdo, KPost, Vector3.zero); // (one or the other)
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return on ? Lang.T("Обходить усю базу.") : Lang.T("Знову обходить межу бази.");
        }

        internal static string SetPost(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            string outside = NpcBase.For(NpcRules.Home(a)).Refuse(aim); if (outside != null) return outside; // (the post must be in it)
            NpcLocker.Put(a.Zdo, KPost, aim);
            NpcLocker.Put(a.Zdo, KPatrol, false); // (one or the other)
            NpcWorker w = a.Worker; if (w != null) w.Reset();
            return Lang.T("Тут його пост.");
        }

        internal static string SetChest(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його.");
            Container best = NpcKit.Nearest<Container>(aim, 3f, ct => ct.GetComponent<Piece>() != null); // (a built chest, not a ship's hold or a grave)
            if (best == null) return Lang.T("Не бачу ящика там, куди ти дивишся.");
            ZNetView nv = best.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return Lang.T("Цей ящик ще не готовий.");
            NpcLocker.PutRef(a.Zdo, KChest, nv.GetZDO().m_uid);
            NpcWorker w = a.GetComponent<NpcWorker>();
            if (w != null) w.Reset();
            return Lang.T("Тепер це його ящик запасів.");
        }

        // its second chest, where it takes from (a cook's ingredients): any built chest, anywhere — the same again takes it off (one chest for both, as before)
        internal static string SetSupply(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (!NpcResident.Is(a)) return Lang.T("Спершу оселі його.");
            Container best = NpcKit.Nearest<Container>(aim, 3f, ct => ct.GetComponent<Piece>() != null);
            if (best == null) return Lang.T("Не бачу ящика там, куди ти дивишся.");
            ZNetView nv = best.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return Lang.T("Цей ящик ще не готовий.");
            NpcWorker w = a.GetComponent<NpcWorker>();
            ZDOID id = nv.GetZDO().m_uid;
            bool off = w != null && (id == w.ChestId || id == w.SupplyId); // (its own chest, or the one it has: no second one)
            NpcLocker.PutRef(a.Zdo, KSupply, off ? ZDOID.None : id);
            if (w != null) w.Reset();
            return off ? Lang.T("Окремої скрині інгредієнтів більше нема: бере з комори.") : Lang.T("Тепер це його скриня інгредієнтів: звідси бере, а готове несе в комору.");
        }

        // what it lacks for its work, in the player's words (null: nothing — all is well as far as this game sees): no chest, a full one, its job's own (Job.Lacks).
        // Its card «Побут» and the heart's board ask here; on any game, only while they are shown (a look at its chests and its base's things)
        internal static List<string> Lacks(NpcActor a)
        {
            NpcWorker w = a != null ? a.Worker : null;
            Job j = w != null ? w.Job : null;
            if (j == null) return null;
            List<string> l = new List<string>();
            Container c = w.Chest;
            if (j.NeedsChest && w.ChestId.IsNone()) { l.Add(Lang.T("нема ящика запасів: Q → Робота → Ящик")); return l; } // (the rest is asked of its chest)
            if (c != null && c.GetInventory() != null && c.GetInventory().GetEmptySlots() == 0) l.Add(Lang.T("ящик повний: забери з нього щось"));
            try { j.Lacks(w, l); } catch (Exception e) { Npcs.Log(a.Id + ": what its work lacks could not be told: " + e.Message); }
            return l.Count > 0 ? l : null;
        }

        // the jobs' ring (Робота): one button a job, and «Без роботи»
        // its menu's deeds, done where it is run, for the one who pressed (NpcMenu.OnOwner, By)
        internal static void Deeds()
        {
            NpcMenu.Deeds["supply"] = (a, at, arg) => SetSupply(a, at);
            NpcMenu.Deeds["chest"] = (a, at, arg) => SetChest(a, at);
            NpcMenu.Deeds["post"] = (a, at, arg) => SetPost(a, at);
            NpcMenu.Deeds["store"] = (a, at, arg) => SetStore(a, at);
            NpcMenu.Deeds["inbox"] = (a, at, arg) => SetInbox(a, at);
            NpcMenu.Deeds["house"] = (a, at, arg) => SetHouse(a, at);
            NpcMenu.Deeds["area"] = (a, at, arg) => NpcHew.SetArea(a, at);
            NpcMenu.Deeds["patrol"] = (a, at, arg) => SetPatrol(a, !Patrols(a));
            NpcMenu.Deeds["job"] = (a, at, arg) => SetJob(a, arg, NpcMenu.By);
            NpcMenu.Deeds["jobat"] = (a, at, arg) => { Job j = Jobs.Find(arg); return j != null ? j.Assign(a, at) : null; };
        }

        private static List<NpcMenu.Button> _jobRing;
        private static List<NpcMenu.Button> JobRing()
        {
            if (_jobRing != null) return _jobRing;
            _jobRing = new List<NpcMenu.Button>();
            for (int i = 0; i < Jobs.All.Count; i++)
            {
                Job j = Jobs.All[i];
                _jobRing.Add(new NpcMenu.Button { Order = i, Title = j.Word, TitleNow = a => { string w = Jobs.Word(j.Id); return w.Substring(0, 1).ToUpperInvariant() + w.Substring(1); }, Icon = j.Icon, Desc = a => Lang.T(j.Desc),
                    Now = a => a.Worker != null && a.Worker.JobId == j.Id, Not = a => WhyNot(a, j), Do = (a, p) => NpcMenu.OnOwner(a, "job", p.transform.position, j.Id),
                    Place = j.Place, AtPlace = j.Place != null ? (Func<NpcActor, Vector3, string>)((a, at) => NpcMenu.OnOwner(a, "jobat", at, j.Id)) : null }); // (a servant: pointed at its house)
            }
            _jobRing.Add(new NpcMenu.Button { Order = 99, Title = Lang.K("Без роботи"), Icon = "n-none", Desc = a => Lang.T("Не працює: удень походжає базою."),
                Now = a => a.Worker == null || !a.Worker.Works, Do = (a, p) => NpcMenu.OnOwner(a, "job", p.transform.position, "") });
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

        internal int InChest(string name) { Container c = From; return c != null ? c.GetInventory().CountItems(name) : 0; } // (where it takes from: its second chest when it has one)
        internal int QualityOf(string name) { ItemDrop.ItemData it = Carry.GetItem(name); return it != null ? it.m_quality : 1; } // (of what it carries: a fish's size counts in what it gives)

        // a player brought it this for its work (NpcRequests): into what it carries, and from there to its chest as all it carries goes; what does not fit: at its feet
        internal void Receive(GameObject prefab, int count)
        {
            ItemDrop d = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (d == null) return;
            Inventory carry = Carry;
            ItemDrop.ItemData it = d.m_itemData.Clone();
            it.m_dropPrefab = prefab;
            int max = Mathf.Max(1, it.m_shared.m_maxStackSize);
            while (count > 0)
            {
                int k = Mathf.Min(Mathf.Min(count, max), Space(carry, it));
                if (k <= 0) break;
                ItemDrop.ItemData part = it.Clone(); part.m_stack = k;
                if (!carry.AddItem(part)) break;
                count -= k;
            }
            SaveCarry();
            if (count > 0) ItemDrop.DropItem(it, count, transform.position + transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
        }

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
            if (took > 0) { SaveCarry(); if (c == Chest) NpcRequests.Used(_a, name, took); } // (from its own chest, for its work: what you brought for it is being used)
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

        // the chests workers keep their supplies in, in one look (a servant asks this of every chest for every thing it holds: asked of each, it was chests × things × people)
        private static readonly HashSet<ZDOID> Work = new HashSet<ZDOID>();
        internal static HashSet<ZDOID> WorkChests()
        {
            Work.Clear();
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.Worker != null && o.Worker.Works) { ZDOID c = o.Worker.ChestId; if (!c.IsNone()) Work.Add(c); c = o.Worker.SupplyId; if (!c.IsNone()) Work.Add(c); }
            return Work;
        }

        private bool StoreInChest(Container c, Func<ItemDrop.ItemData, bool> which = null) // (which: only these of what it carries — a job with two chests)
        {
            if (!OpenChest(c)) return false;
            Inventory chest = c.GetInventory(), carry = Carry;
            bool full = false;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(carry.GetAllItems()))
            { if (which != null && !which(it)) continue; int had = it.m_stack; if (Move(carry, chest, it, had) < had) full = true; } // (as much as fits)
            SaveCarry();
            if (full) Npcs.Log(Name + ": its chest is full");
            return !full;
        }

        // ---------------------------------------------------------------- the errands (NpcMind: Task.Work, every thought)
        internal void Reset()
        {
            _chestLook = 0f;
            if (_e != null && _stage != Stage.Find && Time.time - _stageSince < 0.5f) return; // (an errand just found by Ready(): kept)
            FlushCarry();
            SetTarget(ZDOID.None);
            NpcHew.PutAway(_a); // (an axe, a pickaxe in its hand: away)
            if (_a != null) NpcChores.Hammer(_a, false); // (a servant cut off mending: into a fight it went with the hammer)
            _e = null; _stage = Stage.Find; _stageSince = Time.time; _next = 0f;
            _loop = null; // (cut off in a move that lasts: its clip ends by its term — kept, the loop was played on at the next errand, stirring the air at a chest)
        }

        private void Go(Stage s) { if (_stage == Stage.Use && s != Stage.Use) LoopOff(); FlushCarry(); _stage = s; _stageSince = Time.time; _next = 0f; _moved = false; _inAt = -1f; _inGo = null; _inThing = null; if (_mind != null) _mind.NewGoal(); }

        // its body's move at an errand (a clip of ours, NpcBody.Clip: every game sees it): only while a player sees it (NpcClock.Seen), its walk
        // waits for the clip to go off (NpcBody.ClipFirst). MoveAt: s till its hand is there (the clips' «mark in»/«got» 1.1–1.4)
        internal static float MoveAt = 1.2f, BrowFrom = 40f, BrowChance = 0.3f;
        private bool _moved;
        private bool Moves(string clip, float seconds = 0f) { return !string.IsNullOrEmpty(clip) && _a.Body != null && NpcClock.Seen(_a) && _a.Body.Move(clip, seconds); } // (its gate: NpcMoves — a clip refused, its loop never starts)

        // a move that lasts (its clip has parts «in» and «loop», maybe «rest» and «out»): its way in, then a round of its loop for LoopMin…LoopMax s,
        // now and then a rest for RestMin…RestMax s (RestChance), each length drawn anew — no two stirs alike —, as long as the errand lasts; its way out
        // as it leaves. Each part played anew gets its own «vary» too (BodyAnimator.Vary). A clip with no «loop» part: played once (Moves above)
        internal static float LoopMin = 2f, LoopMax = 6f, RestMin = 0.8f, RestMax = 2.5f, RestChance = 0.45f;
        private string _loop, _loopRest, _loopOut; private float _loopNext; private bool _resting;

        // a step in before its hands move (the user 2026-11-07): its walk ends 1.6–2.2 m from a fire or a smelter, its arm reaches 0.85; while
        // a player sees it, it walks on to StepInTo m off the thing's nearest surface, StepInFor s at most (under the stuck guard's 12): not there
        // by then, it works from where it stands, its hands down. Unseen: as ever. The thing's collider looked up once a step
        internal static float StepInTo = 0.5f, StepInFor = 3f;
        private float _inAt = -1f; private Component _inThing; private GameObject _inGo; // (the errand's thing, found once a step in)
        private bool StepIn(float dt, Component thing)
        {
            if (thing == null || _a.Body == null || !NpcClock.Seen(_a)) return false;
            if (_inThing != thing) { _inThing = thing; _inAt = Time.time; }
            if (Time.time - _inAt > StepInFor) return false;
            Vector3 me = transform.position, p = NpcMind.Nearest(thing, me);
            if (Utils.DistanceXZ(me, p) <= NpcMind.ArmReach) return false; // (at its hand already)
            Vector3 dir = me - p; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            Vector3 to = p + dir.normalized * StepInTo; to.y = me.y;
            return !_mind.Walk(dt, to, 0.15f);
        }
        // the thing it works on within its arm (NpcMind.AtArm): a move of its hands only then — from a step off its hands stay down
        private bool AtTarget() { GameObject go = _inGo != null ? _inGo : _e != null && !_e.Target.IsNone() ? NpcKit.Find(_e.Target) : null; return go != null && _mind.AtArm(go.transform); }
        private bool LoopStart(string clip)
        {
            if (string.IsNullOrEmpty(clip) || BodyClips.Find(clip + ".loop") == null) return false;
            PAnimClip intro = BodyClips.Find(clip + ".in");
            if (!Moves(intro != null ? clip + ".in" : clip + ".loop", intro != null ? NpcBody.WayIn(intro) : LoopMax + 1f)) return false; // (its way in's own term: NpcBody.WayIn — with none it counted as a loop with no end)
            _loop = clip + ".loop"; _loopRest = BodyClips.Find(clip + ".rest") != null ? clip + ".rest" : null; _loopOut = BodyClips.Find(clip + ".out") != null ? clip + ".out" : ""; // (its parts' names made once a round of work)
            _resting = false; _loopNext = Time.time + (intro != null ? intro.Length : 0f);
            return true;
        }
        private void LoopTick()
        {
            if (_loop == null || Time.time < _loopNext || _a.Body == null) return;
            bool rest = !_resting && _loopRest != null && UnityEngine.Random.value < RestChance;
            float len = rest ? UnityEngine.Random.Range(RestMin, RestMax) : UnityEngine.Random.Range(LoopMin, LoopMax);
            _a.Body.Clip(rest ? _loopRest : _loop, len + 1f);
            _resting = rest; _loopNext = Time.time + len;
        }
        private void LoopOff()
        {
            if (_loop == null) return;
            if (_a.Body != null) _a.Body.Clip(_loopOut); // (its way out, or none: the clip ends)
            _loop = null;
        }

        // someone talks to it (NpcMind, at the talk's first moment): its move that lasts goes out by its way out, and is made anew after the talk (NpcMoves «a talk»)
        internal void Hush() { if (_loop == null) return; LoopOff(); _moved = false; }

        // nothing it can do now (no job, no chest, nothing to do and nothing to carry): its mind lets its day go on meanwhile
        internal bool Idle;
        internal string Why; // (a job's word why it finds nothing: «потрібна сокира…» — shown instead of «нема роботи»)

        // a look whether there is work now (while it stepped aside): true when there is — the errand is taken up at once
        internal bool Ready()
        {
            Job job = Job;
            Container chest = _chestNow = Chest;
            _supplyNow = job != null && job.HasSupply ? Supply : null;
            if (job == null || (chest == null && job.NeedsChest)) return false;
            if (CarriesAnything) { _e = null; _stage = Stage.Store; Idle = false; return true; }
            NpcWorker.Errand e = job.Find(this);
            if (e == null) return false;
            _e = e; SetTarget(e.Target); Doing = e.Word; Idle = false; _loop = null; // (no round of the last errand's work played on at this one)
            _stage = Short(e) ? Stage.ToChest : Stage.ToTarget;
            _stageSince = Time.time; _next = 0f;
            return true;
        }

        // something its errand needs is not in its hands yet
        private bool Short(Errand e)
        {
            if (!string.IsNullOrEmpty(e.Take) && Carrying(e.Take) < e.Count) return true;
            if (e.Also != null) foreach (KeyValuePair<string, int> t in e.Also) if (Carrying(t.Key) < t.Value) return true;
            return false;
        }
        private static bool Needs(Errand e, string name)
        {
            if (e.Take == name) return true;
            if (e.Also != null) foreach (KeyValuePair<string, int> t in e.Also) if (t.Key == name) return true;
            return false;
        }

        internal void Step(float dt)
        {
            if (_mind == null || _a == null) return;
            Idle = false;
            Job job = Job;
            if (Time.time >= _chestLook) { _chestLook = Time.time + 1f; _chestNow = Chest; _supplyNow = job != null && job.HasSupply ? Supply : null; }
            Container chest = _chestNow;
            if (job == null) { _mind.Halt(); Doing = Lang.K("без роботи"); Idle = true; return; }
            if (job.NeedsChest && !NpcLocker.Ready(_a)) { _mind.Halt(); Doing = Lang.K("чекає свої речі"); Idle = true; return; } // (its locker on its way here)
            if (chest == null && job.NeedsChest) { _mind.Halt(); Doing = ChestId.IsNone() ? Lang.K("чекає на ящик запасів") : Lang.K("ящик запасів далеко"); Idle = true; return; }
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
                        _mind.Halt(); Doing = Why ?? Lang.K("нема роботи"); Idle = true; return;
                    }
                    SetTarget(_e.Target);
                    Doing = _e.Word;
                    Go(Short(_e) ? Stage.ToChest : Stage.ToTarget);
                    return;
                case Stage.ToChest:
                    Container from = _supplyNow != null ? _supplyNow : chest; // (where it takes from: its second chest when it has one)
                    if (from == null || _e == null) { Drop(); return; } // (a job that needs none has a chest here too — a servant — and it may be taken away while it walks)
                    Doing = Lang.K("іде до ящика");
                    if (!_mind.Walk(dt, from.transform.position, 2f)) return;
                    if (StepIn(dt, from)) return; // (to its rim, its hand going in: NpcMind.Open)
                    if (CarriesAnything) StoreExcept(from, _e, from != chest ? job : null); // (leftovers of another errand back first; what belongs in its own chest stays in its hands for that one)
                    int want = !string.IsNullOrEmpty(_e.Take) ? _e.Count - Carrying(_e.Take) : 0;
                    int got = want > 0 ? TakeFromChest(from, _e.Take, want) : 0;
                    if (got < 0) { Doing = Lang.K("ящик відкритий — чекає"); _mind.Halt(); return; } // (a player is in it)
                    if (!string.IsNullOrEmpty(_e.Take) && got == 0 && Carrying(_e.Take) == 0) { Drop(); return; }
                    if (_e.Also != null)
                        foreach (KeyValuePair<string, int> t in _e.Also)
                        {
                            int more = t.Value - Carrying(t.Key);
                            if (more > 0 && TakeFromChest(from, t.Key, more) < 0) { Doing = Lang.K("ящик відкритий — чекає"); _mind.Halt(); return; }
                            if (Carrying(t.Key) < t.Value) { Drop(); return; } // (gone from the chest since it looked: what it took goes back with the next errand)
                        }
                    Go(Stage.ToTarget);
                    return;
                case Stage.ToTarget:
                    if (Gone) { Go(Stage.Rest); return; }
                    Doing = _e.Word;
                    if (!_mind.Walk(dt, _e.At, _e.Near)) return;
                    Go(Stage.Use);
                    return;
                case Stage.Use:
                    if (Gone) { Go(Stage.Rest); return; }
                    if (!_moved && !string.IsNullOrEmpty(_e.Move ?? job.Move) && !_e.Target.IsNone()) { if (_inGo == null) _inGo = NpcKit.Find(_e.Target); if (_inGo != null && StepIn(dt, _inGo.transform)) return; } // (in to the thing its hands work on)
                    _mind.Halt(); _mind.Face(_e.At + Vector3.up);
                    if (!_moved) { _moved = true; string move = AtTarget() ? _e.Move ?? job.Move : null; if (LoopStart(move)) { _next = Time.time + MoveAt; return; } if (Moves(move)) { _next = Time.time + MoveAt; return; } } // (its hand there first: the work happens as it reaches; a move that lasts loops while it works)
                    LoopTick();
                    if (Time.time < _next) return;
                    _next = Time.time + StepEvery * Ways.WorkStep * NpcHunger.Slow(_a) / NpcKnack.Of(_a, JobId) / NpcToil.Pace(_a); // (a diligent one quicker, a hungry one slower, one with a knack for it quicker still; its mood and its craft: NpcToil)
                    if (Time.time - _stageSince > 60f || _e.Use == null || _e.Use(this, _e)) Go(_e.Collect != null ? Stage.Collect : _e.Made && CarriesAnything ? Stage.Store : Stage.Rest);
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
                    Doing = Lang.K("несе до ящика");
                    if (!CarriesAnything || chest == null) { Go(Stage.Rest); return; } // (no chest: a job that needs none — what it still carries from an old job stays carried)
                    // two chests (a cook): what its job keeps — the dishes — to its own chest first, then the rest of its hands back to where it takes from
                    bool two = _supplyNow != null && _supplyNow != chest, back = two && !CarriesKept(job);
                    Container to = back ? _supplyNow : chest;
                    if (!_mind.Walk(dt, to.transform.position, 2f)) return;
                    if (!StoreInChest(to, two ? (Func<ItemDrop.ItemData, bool>)(it => job.Keeps(it) != back) : null) && Time.time - _stageSince < 30f) { Doing = CarriesAnything ? Lang.K("ящик повний або відкритий") : Doing; _mind.Halt(); return; }
                    if (two && !back && CarriesAnything && Time.time - _stageSince < 30f) { Go(Stage.Store); return; } // (its own chest has its share: the rest to the other one)
                    Go(Stage.Rest);
                    return;
                default: // Rest: a breath between errands
                    _mind.Halt();
                    if (!_moved) { _moved = true; if (NpcToil.Tired(_a) >= BrowFrom && UnityEngine.Random.value < BrowChance) Moves(UnityEngine.Random.value < 0.5f ? "wipe_brow" : "back_ache"); } // (worn: its brow wiped or its back stretched, now and then)
                    if (_e != null) { SetTarget(ZDOID.None); _e = null; }
                    if (Time.time - _stageSince < Breath * Ways.WorkBreath) return; // (a diligent one rests less)
                    _stage = Stage.Find; _next = 0f;
                    return;
            }
        }

        // the thing of its errand no more (taken down, picked up — or its zone let go while the worker still stands: the thing's ZDO lives on, its object does
        // not, and an errand's Use holds that object: a smelter, a hive, a barrel asked through it threw, 2026-11-14 review): the one word for every job — its
        // Use need not ask. The game's own table of what is loaded here (two lookups)
        private bool Gone { get { return _e != null && !_e.Target.IsNone() && NpcKit.Find(_e.Target) == null; } }

        private void Drop() { SetTarget(ZDOID.None); _e = null; _stage = Stage.Rest; _stageSince = Time.time; Doing = Lang.K("у ящику нема потрібного"); }

        // what it carries that its errand does not need, back into the chest it stands at (kept: a job with two chests, at the one it takes from —
        // what belongs in its own chest stays in its hands)
        private void StoreExcept(Container chest, Errand e, Job kept)
        {
            if (!OpenChest(chest)) return;
            Inventory c = chest.GetInventory();
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(Carry.GetAllItems()))
                if (!Needs(e, it.m_shared.m_name) && (kept == null || !kept.Keeps(it)) && c.CanAddItem(it)) { c.AddItem(it.Clone()); Carry.RemoveItem(it); }
            SaveCarry();
        }

        private bool CarriesKept(Job job) { foreach (ItemDrop.ItemData it in Carry.GetAllItems()) if (job.Keeps(it)) return true; return false; }

        // the thing it works on now (every game reads it: Reserve), parsed only when it changed
        internal ZDOID Target
        {
            get
            {
                string t = _a != null && _a.Zdo != null ? NpcLocker.Text(_a.Zdo, KTarget) : "";
                if (t != _targetSeen) { _targetSeen = t; _target = NpcKit.Resolve(t); }
                return _target;
            }
        }

        private void SetTarget(ZDOID id)
        {
            if (_a == null || _a.Zdo == null || !_a.Owned || Target == id) return;
            NpcLocker.PutRef(_a.Zdo, KTarget, id);
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
        // everyone working there (ten workers do not search ten times): built things (fires, smelters) from the base's own look (NpcBase.Things);
        // the rest (bushes, things on the ground) by the layers they lie on only.
        private const float LookAgain = 5f;
        private static readonly Dictionary<string, KeyValuePair<float, List<Component>>> Seen = new Dictionary<string, KeyValuePair<float, List<Component>>>();
        private static object _seenWorld;

        internal List<T> InBase<T>() where T : Component { return InBaseOf<T>(Base, Pos); }

        // the same for anyone (meals: a base's chests and tables), nearest to «me» first
        internal static List<T> InBaseOf<T>(NpcBase b, Vector3 me) where T : Component
        {
            List<Component> all = b.Things(typeof(T)); // (the base's own look at its things: NpcBase — one for everyone there)
            if (all == null) all = System.Array.IndexOf(NpcBase.Kinds, typeof(T)) >= 0 ? NoneLooked : Looked<T>(b); // (a built kind it keeps, not made yet: none till it is — never a second look over its pieces)
            List<T> l = new List<T>(all.Count);
            foreach (Component c in all) if (c != null) l.Add((T)c); // (gone since: left out)
            l.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
            return l;
        }

        private static readonly List<Component> NoneLooked = new List<Component>();
        // what the base's look does not hold (bushes, things on the ground): looked over here, a shared look every LookAgain s; measured («work look»)
        private static readonly int PerfLook = NpcPerf.Id("work look (речі довкола бази)");
        private static List<Component> Looked<T>(NpcBase b) where T : Component
        {
            if (!ReferenceEquals(_seenWorld, ZNet.instance)) { Seen.Clear(); _seenWorld = ZNet.instance; } // (another world: nothing of the old one kept)
            string key = typeof(T).Name + "@" + Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<Component>> had;
            if (Seen.TryGetValue(key, out had) && Time.time - had.Key < LookAgain) return had.Value;
            List<Component> all = new List<Component>();
            HashSet<Component> seen = new HashSet<Component>();
            long perf = NpcPerf.Now();
            {
                int mask = typeof(T) == typeof(ItemDrop) ? LayerMask.GetMask("item") : LayerMask.GetMask("piece_nonsolid", "Default", "Default_small", "item");
                foreach (Collider c in Physics.OverlapSphere(b.Centre, NpcBase.Reach, mask))
                {
                    T t = c.GetComponentInParent<T>();
                    if (t != null && seen.Add(t) && b.Holds(t.transform.position)) all.Add(t);
                }
            }
            NpcPerf.Add(PerfLook, perf);
            Seen[key] = new KeyValuePair<float, List<Component>>(Time.time, all);
            return all;
        }

    }

    // ---------------------------------------------------------------- two workers never on one thing
    // A worker's thing of the moment is in its locker (NpcWorker.KTarget, written by its owner once an errand, cleared
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

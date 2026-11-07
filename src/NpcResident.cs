#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person who lives at a base (npc-plan.md, stage 1): settled at a place (its home: the centre of its base,
    // NpcBase), with a bed given by a player (only by hand), and a day of its own on the Routine layer of its mind:
    //   6–18   its duties: its job (NpcWorker) when it has one, else its own going from place to place (NpcPlaces); hungry, it goes to eat
    //   18–22  supper first when it is not full and the base has cooked food (NpcMeals: the pantry, a table), then its evening among its
    //          places (NpcPlaces: the fire, a seat, the table, its people, and longer at each)
    // Its hunger (NpcHunger): hungry it works slower, starving it drops its work. Sitting by someone, it may talk
    // (NpcChatter).
    //   22–6   in its bed; with no bed (or it is gone), its places as in the evening
    // Now and then a whim (NpcWhims) takes it off its duties for a little while.
    // Kept in its ZDO (resident, bed); the plan itself is the mind's. Where no player is near, its zone sleeps and so
    // does it: when someone comes back it takes up what fits the hour (nothing is made up for the time away).
    // Runs where it is owned, from NpcActor every half second.
    internal sealed class NpcResident : MonoBehaviour
    {
        internal static readonly int KResident = "RavenWatch_npcResident".GetStableHashCode(); // bool
        internal static readonly int KBed = "RavenWatch_npcBed".GetStableHashCode();           // string "user:id" of its bed ("" none)
        internal static readonly int KSettledAt = "RavenWatch_npcSettledAt".GetStableHashCode(); // long: world seconds it settled here
        internal static readonly int KHeart = "RavenWatch_npcHeart".GetStableHashCode();       // string "user:id" of its «Серце оселі» ("" none)
        internal static readonly int KMovedOut = "RavenWatch_npcMovedOut".GetStableHashCode(); // Vector3: the home it was moved out of, still hired — it stays by it (NpcStray brings it back there, not to the Hall); zero: none
        internal enum Part { None, Day, Evening, Night }
        internal const int PerBase = 10; // (the user: up to ten people at one base)

        private NpcActor _a;
        private NpcMind _mind;
        private Part _part = Part.None;
        private bool _hadBed;
        private float _nextWhim, _nextMeal, _nextChat;
        private NpcWorker _worker;

        internal NpcActor Actor { get { return _a; } }
        internal NpcMind Mind { get { return _mind; } }

        private void Awake()
        {
            _a = GetComponent<NpcActor>();
            _mind = GetComponent<NpcMind>();
            _nextWhim = Time.time + Random.Range(60f, 120f);
        }

        internal static bool Is(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null && z.GetBool(KResident, false); }
        internal Vector3 Home { get { return NpcRules.Home(_a); } }
        internal NpcBase Base { get { return NpcBase.For(Home); } }

        internal ZDOID Bed { get { return NpcLocker.Ref(_a.Zdo, KBed); } }

        // its button on the person's round menu (NpcMenu): «Оселити тут» while it lives nowhere; living at a base, «Оселя»
        // with its own ring — the bed, moving out (asked twice: it takes its home, bed, day and work), an admin's edges
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 60, Title = Lang.K("Оселити тут"), Icon = "n-settle", Now = a => Is(a),
                Where = a => Is(a) ? (Vector3?)NpcRules.Home(a) : null,
                TitleNow = a => Is(a) ? Lang.T("Оселя") : Lang.T("Оселити тут"),
                Desc = a => Is(a) ? a.Form(Lang.T("Де він|вона живе: дошка оселі, ліжко, своє місце, переселення.")) : SettleWhere(a),
                Not = a => Is(a) ? null : SettleNot(a),
                Sub = a => Is(a) ? HomeRing : null,
                Do = (a, p) => Settle(a, p.transform.position) };
        }

        private static readonly List<NpcMenu.Button> HomeRing = new List<NpcMenu.Button>
        {
            new NpcMenu.Button { Order = 0, Title = Lang.K("Дошка оселі"), Icon = "n-inbox", AnyTime = true,
                Desc = a => Lang.T("Як живе ця оселя: що треба зробити, хто з ким, твої справи."),
                Not = a => HeartOf(a) == null ? Lang.T("Серце оселі далеко: дошку видно лише поруч із ним.") : null,
                Do = (a, p) => { NpcBoard.Show(HeartOf(a)); return null; } },
            new NpcMenu.Button { Order = 1, Title = Lang.K("Ліжко"), Icon = "n-bed", Place = Lang.K("на ліжко"), Where = a => NpcKit.WhereIs(NpcLocker.Ref(a.Zdo, KBed)),
                Desc = a => a.Form(Lang.T("Його|Її ліжко: тут спить уночі.")), Now = a => NpcLocker.HasRef(a.Zdo, KBed),
                AtPlace = GiveBed },
            new NpcMenu.Button { Order = 2, Title = Lang.K("Своє місце"), Icon = "n-post", Shown = a => { Vector3 at; return NpcPlaces.Favourite(a, out at); },
                Where = a => { Vector3 at; return NpcPlaces.Favourite(a, out at) ? (Vector3?)at : null; },
                Desc = a => Lang.T("Де любить бувати у вільний час. Покаже на екрані й на мапі."),
                Do = (a, p) => { Vector3 at; if (NpcPlaces.Favourite(a, out at)) NpcMenu.ShowPlace(at, null, a.Form(Lang.F("Місце, де любить бувати {0}", a.Name))); return null; } },
            new NpcMenu.Button { Order = 3, Title = Lang.K("Переселити"), Icon = "n-house", Not = SettleNot,
                Desc = a => Lang.T("До іншої оселі: сам дійде. Там ліжко й робота будуть нові."),
                Do = (a, p) => { AskMove(a); return null; } },
            new NpcMenu.Button { Order = 4, Title = Lang.K("Виселити"), Icon = "n-unsettle", Where = a => (Vector3?)NpcRules.Home(a),
                Desc = a => Lang.T("Більше ніде не живе: ні дому, ні ліжка, ні розпорядку, ні роботи."), Not = SettleNot,
                Confirm = Lang.K("Він|Вона втратить дім, ліжко, розпорядок і роботу. Натисни ще раз, щоб виселити."),
                Do = (a, p) => Unsettle(a) },
        };

        // where to move it: the named hearts of the world (the server's list, as for a hire), nearest first, its own and the full ones left out
        private static void AskMove(NpcActor a)
        {
            NpcHeart.Ask(list =>
            {
                Player me = Player.m_localPlayer;
                if (a == null || me == null) return;
                ZDOID own = NpcKit.GetRef(a.Zdo, KHeart);
                list.RemoveAll(e => e.Id == own);
                list.Sort((x, y) => Utils.DistanceXZ(x.Pos, me.transform.position).CompareTo(Utils.DistanceXZ(y.Pos, me.transform.position)));
                RavenBook.Sheet s = new RavenBook.Sheet { Title = a.Name, Who = Lang.T("Куди переселити?"), Stay = () => a != null && a.Zdo != null };
                List<RavenBook.SheetRow> rows = new List<RavenBook.SheetRow>();
                foreach (NpcHeart.Entry e in list)
                {
                    NpcHeart.Entry to = e;
                    float m = Utils.DistanceXZ(e.Pos, me.transform.position);
                    bool full = e.Folk.Count >= PerBase;
                    rows.Add(RavenBook.SheetRow.Pair(m >= 1000f ? Lang.F("{0} км", (m / 1000f).ToString("0.#")) : Lang.F("{0} м", Mathf.RoundToInt(m)), e.Name, full ? RavenBook.ToneBad : RavenBook.ToneNone, full ? Lang.T("Там уже живе десятеро: місця нема.") : null, full ? (System.Action)null : () => { RavenBook.HideSheet(); string said = Resettle(a, to); if (said != null && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, said); }, Lang.F("{0} із {1}", e.Folk.Count, PerBase)));
                }
                if (rows.Count == 0) rows.Add(RavenBook.SheetRow.Plain(Lang.T("Іншої оселі з назвою нема. Постав ще одне Серце оселі.")));
                s.Tabs.Add(new RavenBook.SheetTab { Rows = () => rows });
                RavenBook.ShowSheet(s);
            });
        }

        // it leaves this home (its people hear of it; bed and work stay behind, as when moved out) and walks to that one (NpcTravel settles it there)
        internal static string Resettle(NpcActor a, NpcHeart.Entry to)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (NpcLevers.IsProtected(a)) return a.Form(Lang.T("Його|Її тримає сюжет: зараз не переселити."));
            NpcMemory.TellHome(a, "neighbour_left", 0L, a.Name);
            Unsettle(a);
            a.Order("travel " + Npcs.V(to.Pos) + " " + NpcTravel.ToHeart + " " + NpcKit.Ref(to.Id)); // session: read at once there, as the hire's
            a.Say(a.Form(Lang.T("Добре. Дійду сам|сама.")));
            return a.Form(Lang.F("{0} іде до оселі «{1}».", a.Name, to.Name));
        }

        // ---------------------------------------------------------------- settling (the console, the window; later the talk)
        // by a «Серце оселі» near (NpcHeart): its home is the heart — one base for all who live at it. A player settles people
        // only by a heart (the menu, the way to a heart: needHeart); the author's tools and the console may anywhere (a story
        // person living at its own place)
        internal static string Settle(NpcActor a, Vector3 home) { return Settle(a, home, true); }

        internal static string Settle(NpcActor a, Vector3 home, bool needHeart) { return Settle(a, home, needHeart, true); }
        // room: its base's cap of PerBase people asked (not by the author's load test: NpcStress)
        internal static string Settle(NpcActor a, Vector3 home, bool needHeart, bool room, bool told = true) // (told: those who live there hear of a new face — not a load test's people)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            ZDO z = a.Zdo;
            NpcHeart heart = NpcHeart.Near(home, NpcBase.Reach);
            if (heart != null) home = heart.transform.position;
            else if (needHeart) return NoHeart;
            string full = room ? NoRoom(a, home, heart) : null; if (full != null) return full;
            NpcMind.Plan way = a.Mind != null ? a.Mind.Of(NpcMind.Layer.Order) : null;
            if (a.Mind != null) a.Mind.ForgetWay(); // (settled while an order held its way: that way is over too)
            if (way != null && way.Kind == NpcMind.Task.Travel) a.Order("free"); // (settled on its way: here, not at the heart it was walking to)
            z.Set(KResident, true);
            NpcLocker.Put(z, KMovedOut, Vector3.zero);
            z.Set(NpcRules.KHome, home);
            NpcKit.SetRef(z, KHeart, heart != null ? heart.Id : ZDOID.None);
            NpcLocker.Put(z, KSettledAt, (long)NpcRescue.Now); // (new here: its words for a while — NpcVoice)
            NpcResident r = a.GetComponent<NpcResident>();
            if (r != null) r._part = Part.None; // (its day taken up afresh)
            NpcBase.For(home); // (its base starts being worked out now, a few frames at a time)
            if (told) NpcMemory.TellHome(a, "news_newcomer", 0L, a.Name); // (those who live there hear of it: a new face — NpcNews)
            if (heart == null) return a.Form(Lang.T("Оселився|Оселилася тут."));
            return string.IsNullOrEmpty(heart.Name) ? a.Form(Lang.T("Оселився|Оселилася біля Серця оселі.")) : a.Form(Lang.F("Оселився|Оселилася в оселі «{0}».", heart.Name));
        }

        private static string NoHeart { get { return Lang.T("Поруч нема Серця оселі — оселити можна лише біля нього."); } }

        // no room for it at that home (its heart's ten, or the ten round that spot): why; null — there is
        private static string NoRoom(NpcActor a, Vector3 home, NpcHeart heart)
        {
            int there = 0;
            if (heart != null) there = NpcHeart.HowMany(heart.Id, a);
            else foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && Is(o) && Utils.DistanceXZ(NpcRules.Home(o), home) <= NpcBase.Reach) there++;
            return there >= PerBase ? Lang.F("Тут уже живе {0} — більше на одну оселю не можна.", there) : null;
        }

        // the settle button's line: where it would live if you pressed it now (by you)
        private static string SettleWhere(NpcActor a)
        {
            Player p = Player.m_localPlayer;
            NpcHeart heart = p != null ? NpcHeart.Near(p.transform.position, NpcBase.Reach) : null;
            if (heart == null) return Lang.T("Житиме біля Серця оселі: база довкола, розпорядок дня.");
            return string.IsNullOrEmpty(heart.Name) ? Lang.T("Житиме біля Серця оселі поруч: база довкола, розпорядок дня.") : Lang.F("Житиме в оселі «{0}»: база довкола, розпорядок дня.", heart.Name);
        }

        private static string SettleNot(NpcActor a)
        {
            if (Is(a)) return NpcLevers.IsProtected(a) ? Lang.T("Його тримає сюжет — зараз не виселити.") : null;
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            NpcHeart heart = NpcHeart.Near(p.transform.position, NpcBase.Reach);
            return heart == null ? NoHeart : NoRoom(a, heart.transform.position, heart);
        }

        internal static string Unsettle(NpcActor a)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            if (NpcLevers.IsProtected(a)) return a.Form(Lang.T("Його|Її тримає сюжет: зараз не виселити."));
            ZDO z = a.Zdo;
            if (Is(a)) NpcLocker.Put(z, KMovedOut, NpcRules.Home(a)); // (moved out, not sent off: it stays by the heart it lived at — NpcTalk.Unhire clears it)
            z.Set(KResident, false);
            NpcLocker.Put(z, KBed, "");
            z.Set(KHeart, "");
            NpcWorker.SetJob(a, ""); // (no base, no work)
            if (a.Mind != null) a.Mind.Put(NpcMind.Layer.Routine, null, "day");
            return Lang.T("Більше ніде не живе.");
        }

        // the bed you look at (a bed nobody sleeps in as their own: not a player's spawn point, not another NPC's)
        internal static string GiveBed(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            ZDO z = a.Zdo;
            if (!Is(a)) return a.Form(Lang.T("Спершу оселі його|її: людина має десь жити."));
            Bed bed = NpcKit.Nearest<Bed>(aim, 3f, null);
            if (bed == null) return Lang.T("Не бачу ліжка там, куди ти дивишся.");
            ZNetView nv = bed.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return Lang.T("Це ліжко ще не готове.");
            if (nv.GetZDO().GetLong(ZDOVars.s_owner, 0L) != 0L) return Lang.T("Це чиєсь ліжко (точка появи гравця): дай вільне.");
            ZDOID id = nv.GetZDO().m_uid;
            foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && NpcLocker.Ref(o.Zdo, KBed) == id) return Lang.F("Це ліжко вже має {0}.", o.Name);
            if (!NpcBase.For(NpcRules.Home(a)).Inside(bed.transform.position)) return Lang.T("Це ліжко поза базою людини.");
            NpcLocker.PutRef(z, KBed, id);
            NpcResident r = a.GetComponent<NpcResident>();
            if (r != null) r._part = Part.None;
            return a.Form(Lang.T("Тепер це його|її ліжко."));
        }

        // ---------------------------------------------------------------- its day (every half second, owned)
        private static readonly int PerfPantry = NpcPerf.Id("resident: pantry (де поїсти)"), PerfChat = NpcPerf.Id("resident: chatter (розмови)"),
            PerfWhim = NpcPerf.Id("resident: whims (примхи)"), PerfAsk = NpcPerf.Id("resident: requests (прохання)"), PerfPlan = NpcPerf.Id("resident: plan (розпорядок)");

        private float _nextFar;

        // far from all its own places: its base, its work plot (a lumberjack's, a miner's: up to NpcHew.FarFromBase off — not
        // sent home from its work and back again), a guard's post
        private bool Away(Vector3 home)
        {
            Vector3 me = transform.position, at;
            if (Utils.DistanceXZ(me, home) <= NpcBase.Reach * 2f) return false;
            if (NpcHew.Area(_a, out at) && Utils.DistanceXZ(me, at) <= NpcHew.Radius + 30f) return false;
            at = _a.Zdo != null ? NpcLocker.Vec(_a.Zdo, NpcWorker.KPost, Vector3.zero) : Vector3.zero;
            return at == Vector3.zero || Utils.DistanceXZ(me, at) > 30f;
        }

        internal void Tick()
        {
            if (_mind == null || !Is(_a) || EnvMan.instance == null) return;
            float hour = Mathf.Repeat(EnvMan.instance.GetDayFraction() * 24f - Shift(_a), 24f); // (an early bird's day starts sooner, a night owl's later)
            float bedtime = Bedtime(_a);
            Part part = hour >= 6f && hour < 18f ? Part.Day : hour >= 18f && hour < bedtime ? Part.Evening : Part.Night;
            ZDOID bed = Bed;
            bool hasBed = !bed.IsNone() && ZDOMan.instance != null && ZDOMan.instance.GetZDO(bed) != null && !BedAbsent(bed); // (taken down: as if none)
            Vector3 home = Home;
            bool moved = (home - _homeSeen).sqrMagnitude > 4f; // (its home changed while it was here: the story moved it — StoryPeople — its day planned from the new one)
            if (part != _part || hasBed != _hadBed || moved || _mind.Of(NpcMind.Layer.Routine) == null)
            {
                _homeSeen = home;
                if (part != _part) _nextWhim = Time.time + Random.Range(60f, 120f); // (no whim the moment it wakes or the evening starts)
                _part = part; _hadBed = hasBed;
                long pp = NpcPerf.Now();
                _mind.Put(NpcMind.Layer.Routine, PlanFor(part, hasBed ? bed : ZDOID.None), "day");
                NpcPerf.Add(PerfPlan, pp);
            }
            // far out of its base and free (led off and left there — the user 2026-11-03: Кнуд, 170 m off, found no way back
            // and stood there for good): home the long way (NpcTravel «back»: the game's paths, on out of sight), unless an
            // order, a scene or a way of its own holds it. Looked at every 10 s
            if (Time.time >= _nextFar)
            {
                _nextFar = Time.time + 10f;
                if (Away(home) && _mind.Of(NpcMind.Layer.Order) == null && _mind.Of(NpcMind.Layer.Scene) == null)
                {
                    Npcs.Log(_a.Id + ": far from its base (" + Mathf.RoundToInt(Utils.DistanceXZ(transform.position, home)) + " m) — goes home");
                    _a.Order("travel " + Npcs.V(home) + " " + NpcTravel.ToBack);
                }
            }
            // its work: in the day, on the Job layer (over its walk about, under orders and whims)
            NpcWorker w = _worker != null ? _worker : (_worker = GetComponent<NpcWorker>());
            bool work = w != null && w.Works && (part == Part.Day || w.Job.AllDay || ((w.Job.Evening || NpcNature.WorksLate(_a)) && part == Part.Evening)) && !NpcHunger.IsStarving(_a); // (a guard: day and night; starving: no work)
            NpcMind.Plan job = _mind.Of(NpcMind.Layer.Job);
            if (work && job == null) _mind.Put(NpcMind.Layer.Job, new NpcMind.Plan { Kind = NpcMind.Task.Work }, "work");
            else if (!work && job != null) _mind.Put(NpcMind.Layer.Job, null, "work");
            // hungry: a meal (over its work, as a whim is), when there is food — in the day, the evening, and the night of one
            // with no bed to sleep it off in (hungry at night it sat by the fire till morning)
            if (!(part == Part.Night && hasBed) && Time.time >= _nextMeal && NpcHunger.IsHungry(_a) && _mind.WhimFree("meal"))
            {
                _nextMeal = Time.time + 90f;
                if (!Base.Ready) _nextMeal = Time.time + 5f; // (its base still being looked over: soon again)
                else if (Pantry()) { _mind.TakeWhim(NpcMeals.Plan(), 300f, "meal"); }
                else { NpcChatter.MutterOnce(_a, "nofood"); if (Time.time >= _nextWhyLog) { _nextWhyLog = Time.time + 120f; Npcs.Log(_a.Id + ": hungry (" + Mathf.RoundToInt(NpcHunger.Full(_a)) + ") — nothing to eat: " + NpcMeals.Why(Base)); } }
            }
            long pr = NpcPerf.Now(); NpcRequests.Tick(_a); NpcPerf.Add(PerfAsk, pr); // (now and then it asks for something: throttled inside)
            if (Time.time >= _nextChat) { _nextChat = Time.time + Random.Range(6f, 12f); long pc = NpcPerf.Now(); NpcChatter.Tick(this); NpcPerf.Add(PerfChat, pc); }
            if (part == Part.Day && Time.time >= _nextWhim && _mind.Of(NpcMind.Layer.Whim) == null) { _nextWhim = Time.time + Random.Range(60f, 160f) * NpcPersona.Of(_a).WhimEvery; long pw = NpcPerf.Now(); NpcWhims.Maybe(this); NpcPerf.Add(PerfWhim, pw); }
        }

        // its bed a piece of a place whose state has it not now (the camp gone, its person not moved yet): as if none
        private static bool BedAbsent(ZDOID bed) { GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(bed) : null; return g != null && LocState.Absent(g.GetComponent<ZNetView>()); }

        // how its day is shifted and when it goes to bed by its own clock: one answer for its day (Tick) and its card (DayOf)
        private static float Shift(NpcActor a) { return NpcPersona.Of(a).Chrono + (NpcBackstory.Fact(a, "зорі") ? 1f : 0f); } // (its story: it counts the stars — its day an hour later)
        private static float Bedtime(NpcActor a) { return NpcBackstory.Fact(a, "без_вогню") ? 23f : 22f; } // (its story: no sleep without a fire — longer by it)

        // when it gets up and goes to bed by the world's clock (its card: the same as it lives)
        internal static void DayOf(NpcActor a, out float up, out float bed) { float s = Shift(a); up = 6f + s; bed = Bedtime(a) + s; }

        // the heart it lives at, when that is loaded here (null: none, or far)
        internal static NpcHeart HeartOf(NpcActor a)
        {
            GameObject go = a != null && a.Zdo != null ? NpcKit.Find(NpcKit.GetRef(a.Zdo, KHeart)) : null;
            return go != null ? go.GetComponent<NpcHeart>() : null;
        }

        private bool Pantry() { long p = NpcPerf.Now(); bool has = NpcMeals.Pantry(Base, transform.position) != null; NpcPerf.Add(PerfPantry, p); return has; }

        private float _nextWhyLog;
        private Vector3 _homeSeen = new Vector3(float.MaxValue, 0f, 0f);
        internal Part PartNow { get { return _part; } } // (its day, its evening, its night: NpcPlaces weighs by it)

        private NpcMind.Plan PlanFor(Part part, ZDOID bed)
        {
            Vector3 home = Home;
            if (part == Part.Night && !bed.IsNone()) return new NpcMind.Plan { Kind = NpcMind.Task.Sleep, Who = bed };
            if (part == Part.Evening && Supper()) return NpcMeals.Plan();
            return new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = transform.position }; // (it stands between its places, day and evening, and a night with no bed: NpcPlaces says where)
        }

        // supper now: not full (a second helping only when still hungry), and something cooked in the base
        private bool Supper()
        {
            float full = NpcHunger.Full(_a);
            NpcHunger h = _a.Hunger;
            if (full >= 80f || (h != null && Time.time - h.LastMeal < 600f && full >= 40f)) return false;
            if (!Base.Ready) return false; // (its base still being looked over: by the fire this time)
            if (NpcMeals.Pantry(Base, transform.position) != null) return true;
            NpcChatter.MutterOnce(_a, "nofood");
            if (Time.time >= _nextWhyLog) { _nextWhyLog = Time.time + 120f; Npcs.Log(_a.Id + ": no supper (" + Mathf.RoundToInt(full) + ") — " + NpcMeals.Why(Base)); }
            return false;
        }

        internal string Describe()
        {
            if (!Is(_a)) return "ніде не живе";
            return "мешканець (" + (_part == Part.Day ? "день" : _part == Part.Evening ? "вечір" : _part == Part.Night ? "ніч" : "—") + ", " + (Bed.IsNone() ? "без ліжка" : "має ліжко") + ", " + NpcHunger.Word(_a) + " " + Mathf.RoundToInt(NpcHunger.Full(_a)) + "); " + Base.Describe();
        }
    }
}
#endif

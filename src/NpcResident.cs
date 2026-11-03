#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person who lives at a base (npc-plan.md, stage 1): settled at a place (its home: the centre of its base,
    // NpcBase), with a bed given by a player (only by hand), and a day of its own on the Routine layer of its mind:
    //   6–18   its duties: its job (NpcWorker) when it has one, else a walk about the base; hungry, it goes to eat
    //   18–22  supper first when it is not full and the base has cooked food (NpcMeals: the pantry, a table), then by
    //          the fire: on a bench near a burning fire in its base, else on the ground beside it, else at home
    // Its hunger (NpcHunger): hungry it works slower, starving it drops its work. Sitting by someone, it may talk
    // (NpcChatter).
    //   22–6   in its bed; with no bed (or it is gone), as in the evening
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

        internal ZDOID Bed { get { return NpcKit.GetRef(_a.Zdo, KBed); } }

        // its buttons on the person's round menu (NpcMenu)
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 60, Title = "Оселити тут", Icon = "n-settle", IconNow = a => Is(a) ? "n-unsettle" : "n-settle", Now = a => Is(a),
                Where = a => Is(a) ? (Vector3?)NpcRules.Home(a) : null,
                TitleNow = a => Is(a) ? Lang.T("Виселити") : Lang.T("Оселити тут"),
                Desc = a => Is(a) ? Lang.T("Більше ніде не живе: ні дому, ні ліжка, ні розпорядку, ні роботи.") : SettleWhere(a),
                Not = SettleNot,
                Do = (a, p) => Is(a) ? Unsettle(a) : Settle(a, p.transform.position) };
            yield return new NpcMenu.Button { Order = 65, Title = "Межі бази", Icon = "n-edges", Shown = a => Plugin.IsLocalAdmin() && Is(a), // (an admin's tool for now: to see what it counts as its base)
                Desc = a => Lang.T("Показує на 30 с, що він вважає своєю базою: золоті вогники по межі на землі й позначки на мапі."),
                Do = (a, p) => { NpcBase b = NpcBase.For(NpcRules.Home(a)); if (!b.Ready) return Lang.T("Межі ще рахуються — спробуй за мить."); b.Show(30f); return Lang.T("Межі бази: ") + b.Describe(); } };
            yield return new NpcMenu.Button { Order = 70, Title = "Ліжко", Icon = "n-bed", Place = "на ліжко", Shown = Is, Where = a => NpcKit.WhereIs(NpcKit.GetRef(a.Zdo, KBed)), // (a bed only where it lives)
                Desc = a => Lang.T("Його ліжко: тут спить уночі."), Now = a => NpcKit.HasRef(a.Zdo, KBed),
                AtPlace = GiveBed };
        }

        // ---------------------------------------------------------------- settling (the console, the window; later the talk)
        // by a «Серце оселі» near (NpcHeart): its home is the heart — one base for all who live at it. A player settles people
        // only by a heart (the menu, the way to a heart: needHeart); the author's tools and the console may anywhere (a story
        // person living at its own place)
        internal static string Settle(NpcActor a, Vector3 home) { return Settle(a, home, true); }

        internal static string Settle(NpcActor a, Vector3 home, bool needHeart)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            ZDO z = a.Zdo;
            NpcHeart heart = NpcHeart.Near(home, NpcBase.Reach);
            if (heart != null) home = heart.transform.position;
            else if (needHeart) return NoHeart;
            string full = NoRoom(a, home, heart); if (full != null) return full;
            NpcMind.Plan way = a.Mind != null ? a.Mind.Of(NpcMind.Layer.Order) : null;
            if (a.Mind != null) a.Mind.ForgetWay(); // (settled while an order held its way: that way is over too)
            if (way != null && way.Kind == NpcMind.Task.Travel) a.Order("free"); // (settled on its way: here, not at the heart it was walking to)
            z.Set(KResident, true);
            z.Set(NpcRules.KHome, home);
            NpcKit.SetRef(z, KHeart, heart != null ? heart.Id : ZDOID.None);
            z.Set(KSettledAt, (long)NpcRescue.Now); // (new here: its words for a while — NpcVoice)
            NpcResident r = a.GetComponent<NpcResident>();
            if (r != null) r._part = Part.None; // (its day taken up afresh)
            NpcBase.For(home); // (its base starts being worked out now, a few frames at a time)
            NpcMemory.TellHome(a, "news_newcomer", 0L, a.Name); // (those who live there hear of it: a new face — NpcNews)
            if (heart == null) return a.Form(Lang.T("Оселився|Оселилася тут."));
            return string.IsNullOrEmpty(heart.Name) ? a.Form(Lang.T("Оселився|Оселилася біля Серця оселі.")) : a.Form(Lang.F("Оселився|Оселилася в оселі «{0}».", heart.Name));
        }

        private static string NoHeart { get { return Lang.T("Поруч нема Серця оселі — оселити можна лише біля нього."); } }

        // no room for it at that home (its heart's ten, or the ten round that spot): why; null — there is
        private static string NoRoom(NpcActor a, Vector3 home, NpcHeart heart)
        {
            int there = 0;
            if (heart != null) { foreach (NpcActor o in NpcHeart.Residents(heart.Id)) if (o != a) there++; }
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
            if (NpcLevers.IsProtected(a)) return "Його тримає сюжет — зараз не виселити.";
            ZDO z = a.Zdo;
            z.Set(KResident, false);
            z.Set(KBed, "");
            z.Set(KHeart, "");
            NpcWorker.SetJob(a, ""); // (no base, no work)
            if (a.Mind != null) a.Mind.Put(NpcMind.Layer.Routine, null, "day");
            return "Більше ніде не живе.";
        }

        // the bed you look at (a bed nobody sleeps in as their own: not a player's spawn point, not another NPC's)
        internal static string GiveBed(NpcActor a, Vector3 aim)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            ZDO z = a.Zdo;
            if (!Is(a)) return "Спершу оселі його (він має десь жити).";
            Bed bed = NpcKit.Nearest<Bed>(aim, 3f, null);
            if (bed == null) return "Не бачу ліжка там, куди ти дивишся.";
            ZNetView nv = bed.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return "Це ліжко ще не готове.";
            if (nv.GetZDO().GetLong(ZDOVars.s_owner, 0L) != 0L) return "Це чиєсь ліжко (точка появи гравця) — дай йому вільне.";
            ZDOID id = nv.GetZDO().m_uid;
            foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && NpcKit.GetRef(o.Zdo, KBed) == id) return "Це ліжко вже має " + o.Name + ".";
            if (!NpcBase.For(NpcRules.Home(a)).Inside(bed.transform.position)) return "Це ліжко поза його базою.";
            NpcKit.SetRef(z, KBed, id);
            NpcResident r = a.GetComponent<NpcResident>();
            if (r != null) r._part = Part.None;
            return "Тепер це його ліжко.";
        }

        // ---------------------------------------------------------------- its day (every half second, owned)
        private static readonly int PerfPantry = NpcPerf.Id("resident: pantry (де поїсти)"), PerfChat = NpcPerf.Id("resident: chatter (розмови)"),
            PerfWhim = NpcPerf.Id("resident: whims (примхи)"), PerfAsk = NpcPerf.Id("resident: requests (прохання)"), PerfPlan = NpcPerf.Id("resident: plan (розпорядок)");

        internal void Tick()
        {
            if (_mind == null || !Is(_a) || EnvMan.instance == null) return;
            float owl = NpcBackstory.Fact(_a, "зорі") ? 1f : 0f; // (its story: it counts the stars — its day an hour later)
            float hour = Mathf.Repeat(EnvMan.instance.GetDayFraction() * 24f - NpcPersona.Of(_a).Chrono - owl, 24f); // (an early bird's day starts sooner, a night owl's later)
            float bedtime = NpcBackstory.Fact(_a, "без_вогню") ? 23f : 22f; // (its story: no sleep without a fire — longer by it)
            Part part = hour >= 6f && hour < 18f ? Part.Day : hour >= 18f && hour < bedtime ? Part.Evening : Part.Night;
            ZDOID bed = Bed;
            bool hasBed = !bed.IsNone() && ZDOMan.instance != null && ZDOMan.instance.GetZDO(bed) != null; // (taken down: as if none)
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
            // its work: in the day, on the Job layer (over its walk about, under orders and whims)
            NpcWorker w = _worker != null ? _worker : (_worker = GetComponent<NpcWorker>());
            bool work = w != null && w.Works && (part == Part.Day || w.Job.AllDay || (w.Job.Evening && part == Part.Evening)) && !NpcHunger.IsStarving(_a); // (a guard: day and night; starving: no work)
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
            if (part == Part.Day && Time.time >= _nextWhim) { _nextWhim = Time.time + Random.Range(60f, 160f) * NpcPersona.Of(_a).WhimEvery; long pw = NpcPerf.Now(); NpcWhims.Maybe(this); NpcPerf.Add(PerfWhim, pw); }
        }

        private bool Pantry() { long p = NpcPerf.Now(); bool has = NpcMeals.Pantry(Base, transform.position) != null; NpcPerf.Add(PerfPantry, p); return has; }

        private float _nextWhyLog;
        private Vector3 _homeSeen = new Vector3(float.MaxValue, 0f, 0f);
        private const float YardFire = 35f;

        // its evening with no fire to sit by: a free seat of its base (a bench, a chair — one of the nearest few, not in a
        // player's house, not one another is going to), else a spot of its own somewhere in the base. Before, it was the
        // heart's very spot — everyone sat in a ring round it (2026-10-31)
        private Vector3 EveningSpot(NpcBase b)
        {
            if (b == null || !b.Ready) return Home + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 6f);
            List<Chair> seats = NpcWorker.InBaseOf<Chair>(b, transform.position);
            seats.RemoveAll(c => c == null || NpcPrivate.Off(_a, b, c.transform.position) || SeatTaken(c.transform.position));
            if (seats.Count > 0) return seats[Random.Range(0, Mathf.Min(seats.Count, 4))].transform.position;
            return NpcPrivate.Spot(_a, b);
        }

        private bool SeatTaken(Vector3 at)
        {
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == _a || o.Mind == null) continue;
                NpcMind.Plan p = o.Mind.Current;
                if (p != null && p.Kind == NpcMind.Task.Sit && (p.To - at).sqrMagnitude < 1f) return true;
            }
            return false;
        }

        private NpcMind.Plan PlanFor(Part part, ZDOID bed)
        {
            Vector3 home = Home;
            if (part == Part.Night && !bed.IsNone()) return new NpcMind.Plan { Kind = NpcMind.Task.Sleep, Who = bed };
            if (part == Part.Day) return new NpcMind.Plan { Kind = NpcMind.Task.Wander, Home = home, InBase = true };
            NpcBase b = Base;
            if (part == Part.Evening && Supper()) return NpcMeals.Plan();
            // the evening at the dining table when the player picked one — now and then by a fire outdoors instead (a
            // talker seldom, a quiet one more often), once an evening
            Transform dining = part == Part.Evening ? NpcHeart.DiningOf(b) : null;
            if (dining != null && Random.value >= Mathf.Lerp(0.5f, 0.15f, NpcPersona.Of(_a).Chatty)) return new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = dining.position, Guest = true }; // (picked by the player: let in even in a player's house)
            Vector3 none = new Vector3(float.MaxValue, 0f, 0f);
            System.Func<Vector3, bool> ours = p => (b.Inside(p) || (Utils.DistanceXZ(p, home) <= YardFire && NpcKit.UnderSky(p))) && !NpcPrivate.Off(_a, b, p); // (its base's — or a fire in the open yard near its heart: the base's own line of 20 m and the strip by the houses leave a yard's middle out)
            Vector3 by = NpcKit.ByFire(home, NpcBase.Reach, transform.position, none, ours);
            if (by == none) { by = EveningSpot(b); Npcs.Log(_a.Id + ": no fire of the base to sit by — sits elsewhere" + NpcKit.FiresWhy(home, NpcBase.Reach, transform.position, ours, p => NpcPrivate.Off(_a, b, p))); }
            return new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = by }; // (by a burning fire in its base — a bench near it is found by «sit» — else a seat or a spot of its own)
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

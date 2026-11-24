#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // An NPC's mind (Npcs.cs): our own BaseAI, so the game's path finding walks it (its navmesh, MoveTo, LookAt), and
    // nothing of a monster's own wants (hunting, fleeing, wandering) gets in the way. The body under it (NpcBody) does
    // the physical side: seats, beds, gestures.
    //
    // What it is busy with comes in LAYERS, each holding at most one plan; the highest one with a plan is done, the
    // others wait and go on from where they were when it is over:
    //   Scene    a story scene's director (nothing else moves it while a scene has it; no fights either)
    //   Fight    danger, met by its character (Fight below): a fighter fights, a peaceful one runs (to its base if it lives
    //            somewhere), a wary one runs but fights back when caught. Over: back to what it was doing, an order too
    //   Order    a player's order (follow me, go there, sit) — until done, or until it is let go («free»)
    //   Whim     something it feels like now, outside its duties (a rest in work time, a stretch…): NpcWhims; short,
    //            never over an order or a fight, and its work goes on after it
    //   Job      its work at a base (stage 2: the stoker, the smelter…)
    //   Routine  its day: a resident's schedule (NpcResident: day / evening by the fire / night in its bed), or the
    //            template's habits («on day: wander home») for one who lives nowhere
    //   Leisure  nothing to do: stands, looks about
    // The one rule: a lower layer never interrupts a higher one. A one-off gesture or a line is not a plan: it plays
    // over whatever it does and undoes nothing.
    //
    // Who writes a layer (the user 2026-11-03: the core says it, not each module): every plan has its owner (Plan.Whose:
    // «story», «rescue», «stage», «player», «day», «work», «travel»… — Do/Put/Order say whose, else the layer's own,
    // Owner). «free» and Put(null) let go only of the plan of the one who asks — and of what that one set on the layer:
    // its look, its way of fighting (both kept per layer and per who set them: the highest one set wins, so a scene's «fight off» or «look
    // keep» ends with the scene, and what was under it is back). Over another's plan only by the layer's rule (May):
    //   Scene    stage (the author's test) > story > rescue: the story takes one in need, never the other way round
    //   Whim     what it must do (pay, a meal, a rest worn out) over a whim of its own pleasure (Leisure), not over another
    //   others   the last word stands (one player's order over another's, its day picked anew by the hour)
    //
    // Plans (one task each):
    //   idle    stands where it is; looks at whom/where it was told, or at a player close by
    //   go      walks (or runs) to a point; there: idle on the same layer (it waits there until let go)
    //   follow  keeps near someone; runs when they run or when left behind; very far → steps in beside them
    //   wander  strolls about a point (a resident's day goes from place to place instead: NpcPlaces, as whims)
    //   sit     to a chair/bench near a point and sits on it (none: on the ground there)
    //   sleep   to its bed and lies in it
    //   pose    a held gesture where it stands (relax, rest…)
    //   attack  one foe until it falls or gets away
    //   flee    away from a foe (to its base when it has one) until it is safe for a few seconds
    //   work    its job (NpcWorker: errand after errand), on the Job layer
    //   chore   a small useful thing of its own (NpcChores: mend a piece, feed a beast or the ravens' feeder), as a whim
    //   travel  a long way by itself (NpcTravel: to a settlement's heart, to the ravens' home), on the Order layer
    //   eat     a meal (NpcMeals: to the pantry, a cooked bite from it, to a table, sits and eats); over, its layer is free
    //
    // Orders are words (Do), the same from the console, a scene, a rule — and the layer they are for:
    //   go x y z [run|ease] [near m] [exact] · follow <user:id> [m] · stay|stand · wander [r] [x y z] · sit [x y z] · sleep <bed user:id> · emote <name> [loop]
    //   attack <user:id> · free (this layer's plan let go)
    //   look <user:id>|none · lookat x y z · tp x y z · fight flee|wary|off|defend|hunt   (not plans: settings, kept on the layer told and by whoever set them)
    // The Order, Job and Routine plans are kept in its ZDO: after a reload, or on a new owner's game, it goes on.
    // Runs where the NPC is owned (BaseAI.UpdateAI: only the owner).
    //
    // Its pace (Pace, one rule for every walk): the game's three — a walk, a jog, a run. At its ease (leisure, its day,
    // a whim) and at work it walks, a jog only to somewhere far; never a run about its base, whatever waits there; told
    // to go it walks near, jogs or runs far (a «go … ease» — the story's word, StoryHold — at its ease: it is not a player calling); following it keeps the leader's pace (and catches up); fights and flights
    // run. Its own walking speed: its template's, and a little of its own (NpcActor).
    //
    // Never stuck: a walk that has not come closer for StuckSeconds steps the NPC near its goal (and says so in the
    // log and on the bus): a scene waiting for it never hangs.
    internal sealed class NpcMind : BaseAI
    {
        internal enum Layer { Leisure, Routine, Job, Whim, Order, Fight, Scene }
        internal enum Task { Idle, Go, Follow, Wander, Sit, Pose, Attack, Sleep, Flee, Work, Eat, Travel, Chore }

        // a module's own task (Eat, Travel, Chore…): a step of it (true: over) and what is put right when it is left off —
        // hung here by NpcModules; the mind runs it without knowing whose it is
        // (Unkept: after a reload, the task not among its kept plans — whatever mark of it is left on the person, off)
        internal sealed class TaskOf { public Func<NpcMind, Plan, float, bool> Step; public Action<NpcMind, Plan> Left; public Action<NpcMind> Unkept; }
        internal static readonly Dictionary<Task, TaskOf> Tasks = new Dictionary<Task, TaskOf>();

        private void Unkept(Plan order)
        {
            foreach (KeyValuePair<Task, TaskOf> t in Tasks)
                if (t.Value.Unkept != null && (order == null || order.Kind != t.Key)) t.Value.Unkept(this);
        }
        // how it meets a foe — its character in a fight (the template's «fight:»):
        //   off     does nothing (a scene's figure)
        //   flee    peaceful (the default): only runs, even when caught
        //   wary    runs, but turns on a foe that caught up with it (CornerRange) or hit it; it backs off again when the
        //           foe falls back past BackOff
        //   defend  a fighter: fights a foe that comes near it (or whom it follows), or that hit it
        //   hunt    a fighter: fights any foe it senses
        internal enum Fight { Off, Flee, Wary, Defend, Hunt }
        internal static float StuckSeconds = 12f; // (no gain this long: put by its goal where nobody sees it — the last resort but one, not a habit: its feet ask again, step round, slide a step long before; tuned live: raven npc walk set stuck)
        internal const float WanderWalk = 35f, MealSeats = 8f, FarAway = 50f, DefendRange = 15f, Leash = 30f, FleeRange = 10f, SafeRange = 16f, CornerRange = 2.5f, BackOff = 8f;
        private const int Layers = 7;
        internal static readonly int KPlans = "RavenWatch_npcPlans".GetStableHashCode(); // string: the kept layers' plans — in its locker

        // one plan: what to do (kept) and how far it got (not kept: set again when it is taken up)
        internal sealed class Plan
        {
            public Task Kind;
            public Vector3 To, Home;
            public bool Run;
            public bool Ease;        // go: at its ease, whatever layer it is on (the story's word: StoryHold — a guest walking in to the fire does not run; kept: «2» in Run's field)
            public bool Hurry;       // go: in a hurry but never a sprint — a jog, a walk the last HurryWalk m (StoryHold «hurry»: the user 2026-11-18, Torvald left behind on the way home — «добігав, але не спринтом, аж до зали, і вже від неї собі йшов спокійно»; kept: «3»)
            public float Near = 1.2f, Keep = 3f, Radius = 6f;
            public ZDOID Who = ZDOID.None;
            public long Leader;      // follow: the player it follows by its own number (kept): its body's id is another after a death or a reload
            public string Pose = "";
            public float SetOff; public Vector3 AimAt; // before it sets off, till then: it stands and looks where it means to go (NpcPlaces; not kept)
            public string Word = ""; // what it is said to be doing, over the task's own word (NpcPlaces; not kept)
            public float Until;      // a whim's end (Time.time; not kept)
            public string Whose = ""; // a whim's owner (TakeWhim; not kept)
            public bool Exact;       // go: to within Near, no slack (a clip follows that touches a thing: «go … exact»)
            public float Hand;       // a reach into a chest ends (Time.time; its own — Until is the whim's end)
            public bool Yield;       // nothing to do on its layer just now (no work, no supplies): it steps aside for the
                                     // layers under it (its day: a walk, the fire) until there is (not kept)
            public bool Looks; public Vector3 Look; // idle: looking out at this spot (not kept)
            public bool Guest;       // let into a player's house (a visit, the dining table there: NpcPrivate; not kept)
            public bool Speaks;      // a scene's: its own unasked words go on (a rescue's calls for help); else a scene keeps
                                     // it quiet while it holds (NpcVoice.Free; not kept)
            // how far it got
            public Chair Seat;
            public Vector3 Side; public bool HasSide; // (the spot by its seat it walks to: the free side nearest it — not kept)
            public bool Seated, Started, OnPoint; // (OnPoint: seated on a bench/bed through the body, not on the ground)
            public float Best, BestAt, NextWander, Since, FoeHealth = 1f, Lost;
            public float SteppedAt; // session: when its long way was last stepped (NpcTravel: a way left off a while — a talk, an order over it — is not «stuck» the moment it goes on)
            public bool Waits; public Vector3 WaitAt; // session: a follow whose feet got nowhere — it waits where it is till its leader moves on or comes back (NpcMind.FollowWaits); where the leader stood then
            public int Stage; public string Food = ""; // (a meal's step and its bite: NpcMeals)
            public float Count, Clock, Tries; // (a meal, a chore: how much it holds, how long it goes on, how many tries or done: not kept)

            internal string Code()
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                return Kind.ToString().ToLowerInvariant() + "|" + Npcs.V(To) + "|" + Npcs.V(Home) + "|" + (Run ? 1 : Ease ? 2 : Hurry ? 3 : 0) + "|" + Near.ToString("0.##", ci) + "|" + Keep.ToString("0.##", ci)
                    + "|" + Radius.ToString("0.##", ci) + "|" + NpcKit.Save(Who) + "|" + Pose + "|0|" + Leader.ToString(ci); // (the kind by its name: a kind added later does not turn old saves into another)
            }

            internal static Plan Parse(string s)
            {
                string[] f = s.Split('|');
                if (f.Length < 9) return null;
                Task kind = Task.Idle; bool known = false;
                foreach (Task t in Enum.GetValues(typeof(Task))) if (t.ToString().ToLowerInvariant() == f[0]) { kind = t; known = true; }
                if (!known) return null;
                // a lasting link has six fields of its own, «|» between them (NpcKit.Save): one field here — or all after it are
                // read wrong (a way's word was read from its link's numbers, a follow's player lost after a reload)
                int more = f[7].StartsWith("~", StringComparison.Ordinal) && f.Length >= 14 ? 5 : 0;
                if (f.Length < 9 + more) return null;
                Plan p = new Plan { Kind = kind, Run = f[3] == "1", Ease = f[3] == "2", Hurry = f[3] == "3", Pose = f[8 + more] };
                Vec(f[1].Split(' '), 0, out p.To);
                Vec(f[2].Split(' '), 0, out p.Home);
                p.Near = Errands.F(f[4], 1.2f); p.Keep = Errands.F(f[5], 3f); p.Radius = Errands.F(f[6], 6f);
                p.Who = Id(new[] { more > 0 ? string.Join("|", f, 7, 6) : f[7] }, 0);
                if (f.Length > 10 + more) long.TryParse(f[10 + more], NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Leader);
                return p;
            }
        }

        private readonly Plan[] _plans = new Plan[Layers];
        private Plan _cur;               // the plan being done (the top one)
        private Layer _curLayer = Layer.Leisure;
        private bool _loaded, _kept; // (_kept: its ZDO held a way of fighting of its own — every Save writes one; plans alone, given to one never run yet (NpcTravel.Give), leave it its template's)
        // its way of fighting and where it looks: one per layer (Do «fight», «look», «lookat»), the highest set one counts
        private readonly bool[] _fightSet = new bool[Layers];
        private readonly Fight[] _fightBy = new Fight[Layers];
        private Fight _fight { get { for (int i = Layers - 1; i >= 0; i--) if (_fightSet[i]) return _fightBy[i]; return Fight.Defend; } }
        private readonly ZDOID[] _lookWho = new ZDOID[Layers];
        private readonly float[] _lookTill = new float[Layers];
        private readonly Vector3[] _lookPt = new Vector3[Layers];
        private readonly bool[] _lookPtOn = new bool[Layers];
        private readonly string[] _fightOwner = new string[Layers], _lookOwner = new string[Layers]; // (who set them: only that one takes them off)
        // what one owner set on a layer, off (null: whatever is there — a new owner's game starting clean)
        private void Unset(Layer l, string whose)
        {
            int i = (int)l;
            if (whose == null || _fightOwner[i] == whose) _fightSet[i] = false;
            if (whose == null || _lookOwner[i] == whose) { _lookWho[i] = ZDOID.None; _lookPtOn[i] = false; }
        }

        // the layer's owner when none is said
        internal static string Owner(Layer l) { return l == Layer.Scene ? "story" : l == Layer.Order ? "player" : l == Layer.Job ? "work" : l == Layer.Routine ? "day" : l == Layer.Whim ? Leisure : "core"; }
        private static int SceneRank(string whose) { return whose == "stage" ? 3 : whose == "story" ? 2 : whose == "rescue" ? 1 : 0; }

        // may «whose» write this layer now (over the plan there, or let go of it)
        internal bool May(Layer l, string whose)
        {
            Plan cur = _plans[(int)l];
            if (cur == null || cur.Whose == whose || string.IsNullOrEmpty(cur.Whose)) return true;
            if (l == Layer.Scene) return SceneRank(whose) >= SceneRank(cur.Whose);
            if (l == Layer.Whim) return cur.Whose == Leisure && whose != Leisure;
            return true;
        }
        // a plan given to it on a game that does not run it: never done there, gone when its owner changes — refused and said
        // (orders from anywhere go through NpcActor.Order: sent to its owner)
        private bool NotHere(string what)
        {
            ZNetView nv = Nv;
            if (nv == null || !nv.IsValid() || nv.IsOwner()) return false;
            Npcs.Log(Name() + ": «" + what + "» given on a game that does not run it — refused (NpcActor.Order sends it to its owner)");
            return true;
        }
        private string _doWhose; // (the owner of the order being done: its plans are stamped with it in Set)
        private float _nextFoeCheck;
        private Character _hurtBy;
        private float _hurtAt = -100f;
        internal string Status = Lang.K("стоїть");

        internal Task Now { get { return _cur != null ? _cur.Kind : Task.Idle; } }
        internal Layer NowLayer { get { return _curLayer; } }
        internal bool AtLeisure { get { return _curLayer <= Layer.Routine || (_curLayer == Layer.Whim && _cur != null && _cur.Whose == Leisure); } } // (nothing over its own day but a whim of its pleasure)
        internal Plan Current { get { return _cur; } }
        private bool Runs { get { return _fight == Fight.Flee || _fight == Fight.Wary; } } // (its first answer to a foe: away)
        internal bool Fighter { get { return _fight == Fight.Defend || _fight == Fight.Hunt; } } // (a guard's job is for these)
        internal string OwnWay { get { return _fightSet[(int)Layer.Routine] ? _fightBy[(int)Layer.Routine].ToString().ToLowerInvariant() : ""; } } // (its own way, the one kept: not a scene's, not an order's)
        internal bool Ready { get { return Loaded(); } } // (its kept plans read: a word before them would be overwritten by the kept one — the review of 2026-11-23)

        // its way in a fight on every game (the owner's own; elsewhere as kept in its ZDO) — for its card
        internal Fight Style
        {
            get
            {
                ZNetView nv = Nv;
                if (nv == null || !nv.IsValid() || nv.IsOwner()) return _fight;
                string s = NpcLocker.Text(nv.GetZDO(), KPlans);
                int at = s.IndexOf("fight=");
                if (at < 0) return _fight;
                int end = s.IndexOf(';', at); string w = s.Substring(at + 6, (end < 0 ? s.Length : end) - at - 6);
                foreach (Fight f in Enum.GetValues(typeof(Fight))) if (f.ToString().ToLowerInvariant() == w) return f;
                return _fight;
            }
        }
        internal bool Asleep { get { return _cur != null && _cur.Kind == Task.Sleep && _cur.Seated; } }
        // ...on any game: its own mind where it is run, else its bed's attach as its owner wrote it (as SeatedOf for a seat)
        internal static bool AsleepOf(NpcActor a)
        {
            if (a == null) return false;
            if (a.Owned) return a.Mind != null && a.Mind.Asleep;
            return a.Body != null && a.Body.AttachedSeen == "attach_bed";
        }

        // a sleeper woken (NpcWake): a whim of its duty over its night, taken up this very step — off its bed now (Pick → Leave), so what
        // follows finds it up (its words: NpcVoice.Free asks Asleep). Asleep under an order or a scene: not woken (false)
        internal bool Wake(Plan p, float seconds, string whose)
        {
            if (!Asleep || !TakeWhim(p, seconds, whose)) return false;
            Pick();
            return true;
        }

        // ---------------------------------------------------------------- its feet on a bend (every walk goes through here)
        // The game's own MoveTowards for creatures: its speed is 1 − (the angle between where it faces and where it must
        // go) / m_moveMinAngle (10°) — any bend of its way over ten degrees stops it dead while it turns on the spot, then
        // it walks on straight: a person walked a line, halted a moment, walked another line (the user 2026-11-02). Here:
        // where it walks turns towards where it must go at most so fast (an arc, as a player's feet), the body faces where
        // it walks, a sharp bend slows it a little, never to a halt; the game's own speed-up and slow-down (Character
        // m_acceleration) stay — the same inertia as a player's. Tuned live: raven npc walk set turn|turnrun|slowfrom|slowto|slowmin.
        internal static float TurnWalk = 96f, TurnRun = 68f;             // °/s its way bends walking / running (true seconds since 2026-11-07: the 240 and 170 written before ran at 0.4 of a second's worth — Dt; these keep the turn the user saw and tuned)
        internal static float SlowFrom = 35f, SlowTo = 130f, SlowMin = 0.3f; // a bend sharper than SlowFrom slows it, to SlowMin at SlowTo
        internal static float ArriveTurn = 2.5f; // × its turn on its last stretch to a goal (Steer «straight»: NpcWalk's coming)
        private Vector3 _heading;
        private float _headingAt = -10f;

        // straight: its last stretch to a goal (NpcWalk's coming) — it turns quicker and is not slowed for the bend: by a goal every step
        // aside is a sharp bend, and a bend's slowing on top of its braking made the crawl (0.1 m/s) and a ring walked round the spot
        internal void Steer(Vector3 dir, bool run, float pace = 1f, bool straight = false) // (pace: a share of its speed — slowing as it comes to its goal: NpcWalk)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) { StopMoving(); return; }
            dir.Normalize();
            float dt = Mathf.Min(Dt, 0.1f); // (the mind's step, not a frame's: Dt)
            if (Time.time - _headingAt > 0.25f || _heading.sqrMagnitude < 0.5f) // (it stood a moment: it sets off from where it faces)
            {
                Vector3 f = transform.forward; f.y = 0f;
                _heading = f.sqrMagnitude > 1e-4f ? f.normalized : dir;
            }
            _headingAt = Time.time;
            _heading = Vector3.RotateTowards(_heading, dir, (run ? TurnRun : TurnWalk) * (straight ? ArriveTurn : 1f) * Mathf.Deg2Rad * dt, 0f);
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 1e-4f) _heading = dir; else _heading.Normalize();
            float bend = Vector3.Angle(_heading, dir);
            float k = Mathf.InverseLerp(SlowFrom, SlowTo, bend); k = k * k * (3f - 2f * k);
            LookTowards(_heading);
            m_character.SetMoveDir(_heading * (straight ? 1f : Mathf.Lerp(1f, SlowMin, k)) * pace);
            m_character.SetRun(run && bend < SlowFrom); // (no running through a sharp bend)
        }

        // for the schedule, jobs and whims (NpcResident, NpcWhims): a plan on a layer, or none
        internal Plan Of(Layer l) { return _plans[(int)l]; }
        internal void Put(Layer l, Plan p, string whose)
        {
            if (NotHere("put " + l)) return;
            Loaded();
            if (l == Layer.Whim && p != null) { Npcs.Log(Name() + ": a whim put with no term — refused (TakeWhim)"); return; }
            if (!May(l, whose)) { Npcs.Log(Name() + ": [" + l + "] «" + whose + "» may not take it from «" + _plans[(int)l].Whose + "»"); return; }
            if (p == null) { Release(l, whose); return; }
            p.Whose = whose;
            Set(l, p);
        }

        // a layer let go by its owner: its plan, what it set on the layer, a clip a scene left looping
        private void Release(Layer l, string whose)
        {
            Set(l, null);
            Unset(l, whose);
            if (l == Layer.Scene && Body != null && Body.ClipBusy && (KeepClip == null || !KeepClip(Actor))) Body.Clip("");
        }
        internal static Func<NpcActor, bool> KeepClip; // (a scene over: a clip on it another word of the story holds on: StoryHold)

        // a whim: the one way onto its layer, for every module. Each has an owner and a term (none lasts for ever, and what
        // ends one is only its own: EndWhim). Never over an order, a fight or a scene. A whim for its own pleasure (Leisure:
        // a rest, a stretch, a look round — NpcWhims) gives way to one it must do (its pay, a meal, a rest worn out): those
        // chained one after another and its pay never came (2026-11-03); one it must do waits for no other. over: for a test
        internal const string Leisure = "whim";
        internal bool WhimFree(string whose) { Plan w = _plans[(int)Layer.Whim]; return _curLayer <= Layer.Whim && (w == null || (w.Whose == Leisure && whose != Leisure)); }
        internal bool TakeWhim(Plan p, float seconds, string whose, bool over = false)
        {
            if (NotHere("whim " + whose)) return false;
            Loaded();
            if (p == null || seconds <= 0f) { Npcs.Log(Name() + ": whim of «" + whose + "» with no plan or term — refused"); return false; }
            if (!over && !WhimFree(whose)) return false;
            Plan was = _plans[(int)Layer.Whim];
            if (was != null && was.Whose == Leisure && whose != Leisure) Npcs.Log(Name() + ": leaves its «" + was.Kind + "» for its «" + whose + "»");
            p.Until = Time.time + seconds; p.Whose = whose ?? "";
            Set(Layer.Whim, p);
            return true;
        }

        // a whim given up from inside (no way there, stuck): let go with what it set on its layer, its look and its way of fighting — as its own end does
        private void DropWhim() { Plan w = _plans[(int)Layer.Whim]; if (w == null) return; Unset(Layer.Whim, w.Whose); Set(Layer.Whim, null); }

        internal void EndWhim(string whose) { Plan w = _plans[(int)Layer.Whim]; if (w != null && w.Whose == whose) { Unset(Layer.Whim, whose); Set(Layer.Whim, null); } }

        // it walks with a player a while, as a whim with its term (a favour at night, a talk's «Ходімо, покажу…»): not an order kept till it is let go
        internal bool WalkWith(Player p, float seconds, string whose)
        {
            return p != null && TakeWhim(new Plan { Kind = Task.Follow, Who = p.GetZDOID(), Leader = p.GetPlayerID(), Keep = 3f }, seconds, whose);
        }

        // run by another game now: nothing here is true any more (its ZDO is read again if it comes back). Not «left» as a
        // plan left off: the one who runs it now decides, nothing is written from here
        internal void OwnerChanged(bool here)
        {
            if (here) // (its plans are read at its first thought: _loaded)
            {
                Humanoid me = m_character as Humanoid; // (a bow left drawn by the game that ran it: its mind there knew, this one does not — let down)
                ItemDrop.ItemData w = me != null ? me.GetCurrentWeapon() : null;
                if (w != null && Body != null) Body.AnimFlag(w.m_shared.m_attack.m_drawAnimationState, false);
                return;
            }
            for (int i = 0; i < Layers; i++) _plans[i] = null;
            _cur = null; _curLayer = Layer.Leisure; _wayLeft = null; _loaded = false; _kept = false;
            _via = null; _viaOpened = false; _hurtBy = null; _holdUntil = 0f; _drawn = 0f;
            for (int i = 0; i < Layers; i++) Unset((Layer)i, null);
            if (_feet != null) _feet.Forget();
        }

        private ZNetView _nvL;
        private ZNetView Nv { get { if (_nvL == null) _nvL = GetComponent<ZNetView>(); return _nvL; } } // (found once: its card asks every second)
        private NpcActor _actor;
        internal NpcActor Actor { get { if (_actor == null) _actor = GetComponent<NpcActor>(); return _actor; } }
        private NpcBody _bodyL;
        internal NpcBody Body { get { if (_bodyL == null) _bodyL = GetComponent<NpcBody>(); return _bodyL; } }

        // The mind's clock (2026-11-07, read in the game's MonoUpdaters): the game runs every creature's mind twenty times a second, from its
        // physics step, and hands it 0.05 s. Inside it a frame's time (Time.deltaTime) is the physics step's 0.02 s — what was reckoned with
        // it (its turn on a bend, a slide, the walk report's seconds) ran 2.5 times slow. Anything stepped by the mind (its feet, its plans,
        // a module's task) takes its time from here; a timer of its own reads Time.time
        internal float Dt { get; private set; }

        public override bool UpdateAI(float dt)
        {
            Dt = dt;
            if (!base.UpdateAI(dt)) return false;
            if (Actor == null || Actor.Def == null) { StopMoving(); return true; } // (no template here — another version: it stands still, as its log says)
            if (Time.time < _thinkOff) { StopMoving(); return true; }
            try { if (_feet != null) _feet.DoorTick(); Think(dt); }
            catch (Exception e) { ThinkFailed(e); }
            if (Time.time >= _alertAt)
            {
                _alertAt = Time.time + 1f; // (once a second)
                bool on = NpcAlert.On(Actor); if (IsAlerted() != on) base.SetAlerted(on); // (written to its ZDO only when it changes)
                if (Body != null && !Body.Attached && !Body.Sliding && Time.time >= _fireWait && NpcBody.Burns(transform.position)) { Body.Slide(transform.position, 0.6f, "із вогню"); if (!Body.Sliding) _fireWait = Time.time + 30f; } // (standing in a fire: out of it, to the nearest spot clear of it — NpcBody.Unburnt; none: not asked again for a while)
            }
            return true;
        }

        // The game's own «on its guard» — the red «!» over it, «alert» in its ZDO — is the core's answer (NpcAlert): a fight, a fright, just fought.
        // The game sets it on a blow, an arrow landing near, a neighbour alerted, and only a monster's mind ever takes it off: ours never did,
        // and a person kept its «!» for good (the user 2026-11-08, nobody near). Its own setting: only when NpcAlert agrees; off: by UpdateAI above
        private float _alertAt, _fireWait;
        protected override void SetAlerted(bool alert) { base.SetAlerted(alert && Actor != null && NpcAlert.On(Actor)); }

        // a thought that throws: said, the plan it was on let go (the layer under it goes on: not the same throw every frame,
        // not a person standing dead till the world is reloaded), a short pause before it thinks again
        private const float ThinkPause = 2f;
        private float _thinkOff;
        private void ThinkFailed(Exception e)
        {
            _thinkOff = Time.time + ThinkPause;
            Plan p = _cur;
            Npcs.Log(Name() + ": mind failed on " + (p != null ? _curLayer + "/" + p.Kind : "no plan") + ", plan let go: " + e);
            if (p == null) return;
            bool held = false;
            for (int i = 0; i < Layers; i++) if (_plans[i] == p) { _plans[i] = null; held = true; }
            if (!held) _cur = null; // (it failed again while being left: dropped without that)
            Save();
        }

        private static readonly int PerfMind = NpcPerf.Id("mind (думка, кадр)"), PerfFoe = NpcPerf.Id("foe (пошук ворога)");

        private void Think(float dt)
        {
            long perf = NpcPerf.Now();
            try { Think0(dt); } finally { NpcPerf.Add(PerfMind, perf); }
        }

        // a look now and then, where it is run, at what holds it from outside its mind: hung here by the story (StoryScenes:
        // a scene's lock nobody renews), the mind runs it without knowing whose it is. One string read per person per SceneLook.
        // a player's «Стій» lets go by itself after StayHours game hours (the user 2026-11-03: a forgotten one held Ейнар and
        // Кнуд for good): its end in its ZDO (it outlives a restart and a new owner); an old one with none gets it from now.
        // So does a «Туди» that came and waits there (Arrived: idle on the same layer): its term from when it came. The term is that
        // plan's own — another plan on the layer, or none, ends it (Set → StayOver): a «Стій» let go early left its end behind, and a
        // later «Туди» took it for its own and was let go five seconds after it came (read 2026-11-12). The menu says the term (NpcMenu)
        internal static float StayHours = 4f;
        internal static readonly int KStayUntil = "RavenWatch_npcStayUntil".GetStableHashCode(); // long: world seconds — in its locker
        private float _stayLookAt;
        private static long StayEnd() { return (long)(NpcRescue.Now + StayHours * (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f) / 24f); }
        private void StayFrom() { ZDO z = Nv != null && Nv.IsValid() ? Nv.GetZDO() : null; if (z != null) NpcLocker.Put(z, KStayUntil, StayEnd()); }
        private void StayOver() { ZDO z = Nv != null && Nv.IsValid() && Nv.IsOwner() && _loaded ? Nv.GetZDO() : null; if (z != null && NpcLocker.Long(z, KStayUntil, 0L) != 0L) NpcLocker.Put(z, KStayUntil, 0L); }
        private void StayTerm()
        {
            Plan o = _plans[(int)Layer.Order];
            if (o == null || o.Kind != Task.Idle) return;
            ZDO z = Nv != null && Nv.IsValid() ? Nv.GetZDO() : null;
            if (z == null) return;
            long until = NpcLocker.Long(z, KStayUntil, 0L);
            if (until <= 0L) { NpcLocker.Put(z, KStayUntil, StayEnd()); return; } // (given before there were terms: from now)
            if (NpcRescue.Now < until) return;
            NpcLocker.Put(z, KStayUntil, 0L);
            Npcs.Log(Name() + ": stood its «Стій» out — back to its own day");
            Release(Layer.Order, o.Whose);
        }

        // still a moment, its plan kept (a gesture: the game's one moves the legs too — NpcBody.Gesture)
        // its plan held up by something else (a talk, a gesture, out cold, a clip going off): the clock of «no gain» (BestAt, StuckSeconds) stands as well. It went on, and the
        // first step after a talk longer than StuckSeconds counted as stuck: put by its goal from where it stood, not having tried (2026-11-09). Asked each step of the
        // pause; walking again ends it (Move)
        private Plan _pausedPlan;
        private float _stalledFor;
        private void Paused()
        {
            if (_cur == null || _cur.BestAt <= 0f) return; // (0: a flight's own mark)
            if (_pausedPlan != _cur) { _pausedPlan = _cur; _stalledFor = Time.time - _cur.BestAt; }
            _cur.BestAt = Time.time - _stalledFor;
        }

        private float _stillUntil;
        internal void Still(float seconds) { _stillUntil = Mathf.Max(_stillUntil, Time.time + seconds); }

        // its own pace of turning (a template's «turn:»): up to it softly, down from it softly as it comes round, so the turn starts and
        // ends in its body and its feet (BodyTurn) keep up. With no «turn:» — the game's own pace of its body, eased the same way but quickly
        // (the user 2026-11-23: «щоб всі нпс повертались трошки плавніше, при тому не збільшуючи надто час повороту» — Heid turning to the
        // kitchen hearth in torv_back snapped round): its top speed is the game's, only the start and the end are soft (90° ≈ 0.65 s)
        internal static float TurnIn = 0.7f, TurnOut = 1.5f, TurnLeast = 12f; // s up to its pace · deg/s per deg left · deg/s at the least
        internal static float QuickIn = 0.18f, QuickOut = 8f; // the same for the game's own pace: s up to it · deg/s per deg left
        private float _turnNow, _gameTurn = -1f, _gameRunTurn;
        private void TurnPace(float dt)
        {
            if (_gameTurn < 0f) { _gameTurn = m_character.m_turnSpeed; _gameRunTurn = m_character.m_runTurnSpeed; } // (the body's own, read before it is first written here)
            NpcActor me = Actor;
            float pace = me != null && me.Def != null ? me.Def.Turn : 0f, rise = TurnIn, fall = TurnOut, run = 1f;
            if (pace <= 0f) { pace = _gameTurn; rise = QuickIn; fall = QuickOut; run = _gameTurn > 0f ? _gameRunTurn / _gameTurn : 1f; }
            if (pace <= 0f) return;
            Vector3 want = m_character.GetLookDir(), now = transform.forward;
            want.y = 0f; now.y = 0f;
            float left = Vector3.Angle(now, want);
            _turnNow = left < 1.5f ? TurnLeast : Mathf.MoveTowards(_turnNow, pace, pace / rise * dt);
            float speed = Mathf.Clamp(Mathf.Min(_turnNow, left * fall), TurnLeast, pace);
            m_character.m_turnSpeed = speed; m_character.m_runTurnSpeed = speed * run;
        }

        internal static Action<NpcActor> SceneWatch;
        internal static readonly List<Action<NpcActor, string>> FightOver = new List<Action<NpcActor, string>>(); // a module's word on how a fight went («fight_won», «fight_lost»)
        internal static readonly List<Action<NpcActor, Character>> Hurt = new List<Action<NpcActor, Character>>(); // a module's word on a hit by a beast (where it is run: a friend called to its side — NpcNeighbours)
        internal static Func<NpcActor, Vector3, float> SeatLean; // a module's lean on a seat a person picks (a meal, the fire): metres added to its way (NpcNeighbours: to a friend's side, away from a foe)
        internal static Action<NpcActor> PoseReached; // a whim's walk ended in its pose (a wash at the water): a module's word on it, now and then
        internal static float SceneLook = 5f;
        private float _sceneLookAt;

        private void Think0(float dt)
        {
            if (m_character.IsDead()) return;
            Loaded();
            TurnPace(dt);
            NpcActor me0 = Actor;
            if (SceneWatch != null && Time.time >= _sceneLookAt) { _sceneLookAt = Time.time + SceneLook; SceneWatch(me0); }
            if (Time.time >= _stayLookAt) { _stayLookAt = Time.time + 5f; StayTerm(); }
            if (me0 != null && me0.Vitals != null && me0.Vitals.Down) { Paused(); StopMoving(); Status = Lang.K("без тями"); return; } // (senseless: NpcVitals)
            if (Time.time < _stillUntil && _plans[(int)Layer.Fight] == null) { Paused(); StopMoving(); return; } // (a gesture plays: still for it, its plan kept — NpcBody.Gesture)
            if (Time.time < _holdUntil && _plans[(int)Layer.Fight] == null && _curLayer != Layer.Scene) // (a talk holds it — never a scene: its walk, its places go on)
            {
                Paused();
                if (!_heldNow) { _heldNow = true; if (_workerL == null) _workerL = GetComponent<NpcWorker>(); if (_workerL != null) _workerL.Hush(); } // (a talk begins: its work's long move goes out by its way out — NpcMoves «a talk»)
                Plan wh = _plans[(int)Layer.Whim];
                if (wh != null) wh.Until = Mathf.Max(wh.Until + dt, Time.time + AfterTalk); // (its stay waits while it talks, and it does not jump up from its seat the moment you go)
                if (!SeatedNow && !Asleep) StopMoving();
                GameObject by = Find(_holdBy);
                if (by != null && !Asleep) LookAt(Eye(by));
                Status = Lang.K("слухає");
                return;
            }
            _heldNow = false;
            if (me0 != null && me0.Gear != null && me0.Gear.InUse) { StopMoving(); Player you = Player.GetClosestPlayer(transform.position, 6f); if (you != null) LookAt(you.GetEyePoint()); Status = Lang.K("показує спорядження"); return; } // (its cells open: it stands for you)
            Plan whim = _plans[(int)Layer.Whim];
            if (whim != null && Time.time > whim.Until) { Unset(Layer.Whim, whim.Whose); _plans[(int)Layer.Whim] = null; } // (the whim is over: back to its duties — and what it set, its way of fighting, its look, with it)
            // work that stepped aside: now and then a look whether there is some again
            Plan job = _plans[(int)Layer.Job];
            if (job != null && job.Yield && Time.time >= _nextWorkLook)
            {
                _nextWorkLook = Time.time + WorkLook;
                if (_workerL == null) _workerL = GetComponent<NpcWorker>();
                if (_workerL != null && _workerL.Ready()) job.Yield = false;
            }
            bool still = Asleep; // (asleep it sees nothing, a blow wakes it; on a seat, at a meal it still sees a foe come and gets up: a fighter stood up to it only when hit, and a guard ate on while greydwarfs went round its base, 2026-11-04)
            if (_plans[(int)Layer.Fight] == null && _plans[(int)Layer.Scene] == null && !still && Time.time >= _nextFoeCheck && NpcClock.Take()) { _nextFoeCheck = Time.time + 0.5f * NpcClock.Slow(Actor); long pf = NpcPerf.Now(); LookForFoe(); NpcPerf.Add(PerfFoe, pf); } // (its turn on the clock)
            Pick();
            Plan p = _cur;
            if (p == null) { Idling(null, 6f); Status = Lang.K("стоїть"); return; }
            if (p.SetOff > 0f)
            {
                if (Time.time < p.SetOff) { StopMoving(); LookAt(p.AimAt); if (p.Word.Length > 0) Status = p.Word; return; }
                p.SetOff = 0f;
            }
            switch (p.Kind)
            {
                case Task.Go: Going(dt, p); break;
                case Task.Follow: Following(dt, p); break;
                case Task.Wander: Wandering(dt, p); break;
                case Task.Sit: Sitting(dt, p); break;
                case Task.Pose: Posing(p); break;
                case Task.Attack: Attacking(dt, p); break;
                case Task.Sleep: Sleeping(dt, p); break;
                case Task.Flee: Fleeing(dt, p); break;
                case Task.Work: Working(dt, p); break;
                default: // (a module's own task: NpcModules)
                    TaskOf own;
                    if (Tasks.TryGetValue(p.Kind, out own) && own.Step != null) { if (own.Step(this, p, dt)) Done(p); }
                    else Idling(p, 6f);
                    break;
            }
            if (p.Word.Length > 0 && _cur == p) Status = p.Word;
        }

        // the top plan; when it changes, the old one is left as it was (stood up from its seat, its pose ended) and the
        // new one starts afresh from where it is
        private void Pick()
        {
            Plan top = null; Layer at = Layer.Leisure;
            for (int i = Layers - 1; i >= 0; i--) if (_plans[i] != null && !_plans[i].Yield) { top = _plans[i]; at = (Layer)i; break; }
            if (top == _cur) return;
            GiveUpVia(); // (a door on the old plan's way)
            bool down = _curLayer >= Layer.Order && at >= Layer.Order && StaysDown(_cur, top); // (the story's words — a command, a scene — over each other: its own day sits down where it chooses, as ever)
            Chair seat = down ? _cur.Seat : null; bool onPoint = down && _cur.OnPoint; // (on a bench: the same seat, still attached)
            if (down) { _cur.Seated = false; _cur.Started = false; _cur.Seat = null; _cur.OnPoint = false; } // (left sitting: no getting up — the new plan sits on, below)
            else Leave(_cur);
            _cur = top; _curLayer = at;
            _doorTries = 0; _doorsTried.Clear(); _walkLabel = top != null ? at + "/" + top.Kind : ""; // (a new plan: its doors afresh; its label for the black box, made once)
            if (top != null) { top.Started = down; top.Seated = down && top.Kind == Task.Sit; top.OnPoint = onPoint; top.Seat = seat; top.HasSide = false; top.Best = float.MaxValue; top.BestAt = Time.time; }
        }

        // Sitting and told to sit right there again: it stays down, not up and down again in the shot (the user 2026-11-15, lit_hall: a scene
        // takes Heid and Torvald the story sat by the fire — its «sit» at their own spot over their word, and that word again when the scene is
        // over). On a seat (a bench: «hold … sit»): a sitting plan whose seats round its spot take in the one it is on — that seat kept, attached
        // as it is (it would pick its own: the nearest free one). On the ground: a sitting plan or the gesture «sit» at the same spot — but not
        // while a seat is free round a sitting plan's spot: that one is for the seat (the bench came after it sat down: the Hall's state rose
        // behind the scenes, 2026-11-15 — they sat on the floor by it for good)
        internal static float SitStill = 0.6f, SeatRange = 4f; // m: the same spot · the seats a sitting plan looks for round its spot
        private bool StaysDown(Plan from, Plan to)
        {
            if (from == null || to == null || Body == null) return false;
            if (Body.Attached)
            {
                if (from.Kind != Task.Sit || !from.Seated || !from.OnPoint || from.Seat == null || to.Kind != Task.Sit) return false;
                Transform on = from.Seat.m_attachPoint != null ? from.Seat.m_attachPoint : from.Seat.transform;
                return Vector3.Distance(on.position, to.To) < SeatRange;
            }
            if (Body.HeldGesture != "sit") return false;
            bool sits = from.Kind == Task.Sit ? from.Seated && !from.OnPoint : from.Kind == Task.Pose && from.Pose == "sit";
            bool will = to.Kind == Task.Sit || (to.Kind == Task.Pose && to.Pose == "sit");
            return sits && will && Utils.DistanceXZ(to.To, transform.position) < SitStill && (to.Kind != Task.Sit || !SeatNear(to.To));
        }

        // a seat free round that spot, as a sitting plan would look for one (StoryHold: one sat on the ground by its word gets up for it)
        internal bool SeatNear(Vector3 at) { return NearestChair(at, SeatRange, false, this, true) != null; }

        private void Leave(Plan p)
        {
            if (p == null) return;
            if (p.Seated) { if (Body != null && Body.Attached) Body.Detach(); else if (Body != null) Body.Gesture("", false); } // (off its seat, its bed, the ground it sat on)
            if (p.Kind == Task.Pose && Body != null) Body.Gesture("", false);
            TaskOf left; if (Tasks.TryGetValue(p.Kind, out left) && left.Left != null) left.Left(this, p); // (a module's task: what it puts right — NpcModules)
            p.Seated = false; p.OnPoint = false; p.Seat = null; p.Started = false;
            StopMoving();
        }

        // a plan that finished by itself (a meal eaten): its layer is free, the one under it goes on
        private void Done(Plan p)
        {
            for (int i = 0; i < Layers; i++) if (_plans[i] == p) Set((Layer)i, null);
        }

        // on a seat or the ground, sitting (by the fire, at a table): for a chat (NpcChatter)
        internal bool StandsFree { get { return _cur == null || _cur.Kind == Task.Idle; } } // (standing, no walk, no pose, no seat: its body turns to whatever it looks at — StoryScenes.Facing)
        internal bool SeatedNow { get { return _cur != null && (_cur.Kind == Task.Sit || _cur.Kind == Task.Eat) && _cur.Seated; } }
        // ...on any game: its own mind where it is run, else its body's sitting as its owner wrote it (a seat's attach, not its bed)
        internal static bool SeatedOf(NpcActor a)
        {
            if (a == null) return false;
            if (a.Owned) return a.Mind != null && a.Mind.SeatedNow;
            string at = a.Body != null ? a.Body.AttachedSeen : "";
            return at.Length > 0 && at != "attach_bed";
        }
        internal bool AtTable { get { return _cur != null && _cur.Kind == Task.Eat && _cur.Seated; } }

        // a meal's sitting down (NpcMeals): the same as «sit» — a seat near p.To, else the ground
        internal void SitDown(float dt, Plan p) { Sitting(dt, p); }

        // a long way left off for an order (follow me, stay, go there, sit): taken up again once that order is let go
        private Plan _wayLeft;

        // its way forgotten (settled on its way, sent off): «free» then leaves it where it is
        internal void ForgetWay() { if (_wayLeft == null) return; _wayLeft = null; Save(); }

        private void Set(Layer layer, Plan p)
        {
            if (p != null && string.IsNullOrEmpty(p.Whose)) p.Whose = _doWhose ?? Owner(layer); // (its owner: who gave the order, else the layer's own)
            if (layer == Layer.Order)
            {
                Plan was = _plans[(int)layer];
                if (was != p) StayOver(); // (a wait's term is that plan's own: StayTerm)
                if (p != null && p.Kind == Task.Travel) _wayLeft = null; // (a new way: the old one is over)
                else if (p != null && was != null && was.Kind == Task.Travel) _wayLeft = was; // (an order over its way: kept for later)
                else if (p == null && _wayLeft != null && (was == null || was.Kind != Task.Travel)) { p = _wayLeft; _wayLeft = null; } // (the order let go: on its way again)
            }
            _plans[(int)layer] = p;
            if (layer == Layer.Order || layer == Layer.Job || layer == Layer.Routine) Save();
        }

        // ---------------------------------------------------------------- the plans
        private void Idling(Plan p, float near)
        {
            if (OutOf() || Aside()) return; // (it stands in someone: a step out of them · someone walks at it: a step out of their way)
            StopMoving();
            if (p != null && p.Looks) LookAt(p.Look); else LookAround(near);
            if (p != null && !p.Started) { p.Started = true; Status = Lang.K("стоїть"); }
        }

        // Standing in another person's body: people's bodies do not meet (NpcBody: nothing pushes two who stopped on one spot apart any more), so
        // the one that stands steps out of the other — away from it, for half a second, looked at three times a second (both standing: each steps
        // away from the other). Not on a seat or in a bed, not in a scene (it puts people where it wants them: a clip of two stands them 0.8 m apart,
        // face to face — more than InsideAt), not while a clip plays on it; a «Стій» steps out too: two bodies in one is never meant. One walking
        // through it is let pass (Aside steps out of its way). Nowhere free round it (NoRoom): it stays where it is, in the other.
        internal static float InsideAt = 0.5f; // m between the middles of two standing bodies: in each other (each 0.4 m round: 0.8 — they only touch)
        private float _outLook;
        private bool OutOf()
        {
            float now = Time.time;
            if (SeatedNow || Asleep || _curLayer == Layer.Scene || (Body != null && Body.Attached)) { _asideUntil = 0f; return false; } // (held where it is — as Aside is: a step begun, this one's or Aside's, which shares it, stops; a scene that took it mid-step gets it where it stood)
            if (now < _asideUntil) { m_character.SetWalk(true); Steer(_asideDir, false); return true; } // (a step out, under way: Aside's or its own)
            if (now < _outLook || (Body != null && Body.ClipBusy)) return false; // (a clip plays on it: no step of its own out — Aside still steps out of a walker's way, as before)
            _outLook = now + 0.3f;
            Vector3 me = transform.position;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.gameObject == gameObject) continue;
                Vector3 d = me - o.transform.position;
                if (Mathf.Abs(d.y) > 1f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m >= InsideAt) continue;
                Character c = o.GetComponent<Character>();
                if (c != null && c.GetVelocity().magnitude >= 0.5f) continue; // (walking through it: it passes on)
                Vector3 dir = m > 0.05f ? d / m : (GetInstanceID() < o.GetInstanceID() ? Vector3.right : Vector3.left); // (on the very same spot: the two to opposite sides)
                if (NoRoom(me + dir * 0.6f))
                {
                    Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                    if (!NoRoom(me + side * 0.6f)) dir = side; else if (!NoRoom(me - side * 0.6f)) dir = -side; else continue; // (no room any way: it stays)
                }
                _asideDir = dir; _asideUntil = now + 0.5f;
                m_character.SetWalk(true); Steer(dir, false);
                return true;
            }
            return false;
        }

        // whom/where it was told to look at, else a player close by (the first bit of a mind: it notices you)
        // standing about, someone walking straight at it close by: a step to the side it already stands on, for under a
        // second (a seat, a bed, a talk keep it where it is)
        private float _asideUntil, _asideLook;
        private Vector3 _asideDir;
        private bool Aside()
        {
            float now = Time.time;
            if (now < _asideUntil) { m_character.SetWalk(true); Steer(_asideDir, false); return true; }
            if (now < _asideLook || SeatedNow || Asleep || _curLayer == Layer.Order || _curLayer == Layer.Scene) return false; // (told to stand here, placed by a scene: it stays)
            _asideLook = now + 0.3f;
            Vector3 me = transform.position;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Mind == null || o.Mind == this) continue;
                Vector3 d = me - o.transform.position; d.y = 0f;
                float m = d.magnitude;
                if (m > 1.8f || m < 0.05f) continue;
                Character c = o.GetComponent<Character>();
                if (c == null || c.GetVelocity().magnitude < 0.5f) continue; // (it is walking, not standing about too)
                Vector3 f = o.transform.forward; f.y = 0f; f.Normalize();
                if (Vector3.Dot(d / m, f) < 0.75f) continue; // (at it)
                Vector3 right = new Vector3(f.z, 0f, -f.x);
                Vector3 dir = right * (Vector3.Dot(d, right) >= 0f ? 1f : -1f);
                if (NoRoom(me + dir)) { dir = -dir; if (NoRoom(me + dir)) continue; }
                _asideDir = dir; _asideUntil = now + 0.8f;
                Steer(dir, false);
                return true;
            }
            return false;
        }

        // a look at someone (E, Q, a «near» rule, an order) lasts this long after the last word, then it is let go
        private const float LookFor = 8f;

        // «hear <who> <s>» (NpcSpeech.TurnToSpeaker: a scene's line from past its shoulder): its body to the speaker for those seconds, over every
        // layer's look, then the look its layers had comes back by itself — a scene's «face» is not lost (the review of 2026-11-23: a «keep» look
        // left it facing the last speaker till the next «face»). Only standing free with nothing in its hands and not seated, and only if the
        // speaker really is past NpcSpeech.TurnPast from its front here, where it is run
        private ZDOID _hearWho = ZDOID.None; private float _hearTill;
        private void Hear(string[] w)
        {
            NpcActor me = Actor;
            if (w.Length < 3 || !StandsFree || me == null || (me.Body != null && !me.Body.ClipFree) || SeatedOf(me)) return;
            ZDOID id = Id(w, 1);
            GameObject g = Find(id);
            if (g == null) return;
            Vector3 d = g.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.04f || Vector3.Angle(transform.forward, d) < NpcSpeech.TurnPast) return;
            float s;
            if (!float.TryParse(w[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s)) return;
            _hearWho = id; _hearTill = Time.time + Mathf.Clamp(s, 0.5f, 30f);
        }
        private float _glanceUntil, _nextGlance;
        private void LookAround(float near)
        {
            if (Time.time < _hearTill) { GameObject h = Find(_hearWho); if (h != null) { LookAt(Eye(h)); return; } } // (a line heard: Hear)
            for (int i = Layers - 1; i >= 0; i--) // (the highest layer's look: a scene's over a talk's over its raven's)
            {
                if (_lookPtOn[i]) { LookAt(_lookPt[i]); return; }
                if (_lookWho[i].IsNone()) continue;
                if (Time.time > _lookTill[i]) { _lookWho[i] = ZDOID.None; continue; } // (the talk is over: you are no longer its business)
                GameObject who = Find(_lookWho[i]);
                if (who != null) { LookAt(Eye(who)); return; }
            }
            // at its ease it minds its own: now and then a glance at a player close by, short (the user: they stared at you
            // from afar as if you were in their way — they stop and look only when you speak to them, E or Q)
            float now = Time.time;
            if (now >= _nextGlance)
            {
                _nextGlance = now + UnityEngine.Random.Range(6f, 14f);
                Player near0 = Player.GetClosestPlayer(transform.position, Mathf.Min(5f, Actor != null ? NpcPersona.Of(Actor).Notice : near));
                if (near0 != null && UnityEngine.Random.value < 0.35f) { _glanceUntil = now + UnityEngine.Random.Range(1.5f, 3f); _glanceAt = near0; }
            }
            if (now < _glanceUntil && _glanceAt != null) LookAt(_glanceAt.GetEyePoint());
        }
        private Player _glanceAt;

        // «there»: within its plan's Near and a little (its feet stop a step short) — one rule for it and for whoever waits
        // on it (StoryScenes)
        internal const float ArriveSlack = 0.6f;
        internal static float Reach(float near) { return near + ArriveSlack; }

        private void Going(float dt, Plan p)
        {
            float d = Utils.DistanceXZ(transform.position, p.To);
            float reach = p.Exact ? p.Near : Reach(p.Near);
            if (d <= reach || (!p.Exact && AtSnap(p.To, reach))) { Arrived(p); return; }
            if (d < p.Best - 0.5f) { p.Best = d; p.BestAt = Time.time; }
            if (_feet != null && _feet.CantReach(p.To) && _curLayer == Layer.Whim) { Npcs.Log(Name() + ": no way to its whim's place — lets it go"); Lost(p.To); DropWhim(); return; } // (a whim: another one, not half a minute at a wall)
            bool noWay = _feet != null && _feet.CantReach(p.To);
            if (Time.time - p.BestAt > StuckSeconds || noWay)
            {
                if (noWay) _feet.Forgive(); // (told once: a scene, an order keep trying — not this every frame)
                if (!Unstick(p.To, "stuck " + Mathf.RoundToInt(d) + " m from its goal", false, OwnGoal)) // (seen: not there — not «arrived»)
                {
                    if (_curLayer == Layer.Whim) { Lost(p.To); DropWhim(); return; } // (a whim let go: its day goes on)
                    if (_curLayer == Layer.Scene || _curLayer == Layer.Order) return; // (a scene, your order: it keeps trying; the last safety takes it home)
                }
                Arrived(p); return;
            }
            Pace pace = p.Run ? Pace.Run : PaceFor(d);
            Move(dt, p.To, p.Near, pace);
            Status = pace == Pace.Run ? Lang.K("біжить") : pace == Pace.Jog ? Lang.K("поспішає") : Lang.K("іде");
        }

        private void Following(float dt, Plan p)
        {
            GameObject who = Find(p.Who);
            if (who == null && p.Leader != 0L && Time.time >= _leaderLook) // (the same player in a new body: after a death, a reload — looked for once a second)
            {
                _leaderLook = Time.time + 1f;
                foreach (Player pl in Player.GetAllPlayers()) if (pl != null && pl.GetPlayerID() == p.Leader && !pl.IsDead()) { p.Who = pl.GetZDOID(); who = pl.gameObject; break; }
            }
            if (who == null)
            {
                // it waits a little (they may come back in range); gone for good (left, logged out): let go, on whatever layer it was told
                if (p.Lost <= 0f) p.Lost = Time.time;
                if (Time.time - p.Lost > 30f) { Npcs.Log(Name() + ": whom it followed is gone — let go"); Done(p); return; }
                Status = Lang.K("загубив|загубила, кого вів|вела"); Idling(p, 6f); return;
            }
            p.Lost = 0f;
            Vector3 at = who.transform.position;
            float d = Utils.DistanceXZ(transform.position, at);
            if (p.Waits) // (its feet found no way to its leader: it waits where it is, no pushing at a wall, no way asked — FollowWaits)
            {
                if (d <= p.Keep + 1f || Vector3.Distance(at, p.WaitAt) > WaitMoved) { p.Waits = false; p.Best = float.MaxValue; p.BestAt = Time.time; Npcs.Log(Name() + ": whom it follows " + (d <= p.Keep + 1f ? "came back to it" : "moved on") + " — after them again"); }
                else { StopMoving(); LookAt(Eye(who)); p.Best = d; p.BestAt = Time.time; Status = Lang.K("не може дійти слідом"); return; }
            }
            if (d > FarAway && Time.time >= _farTry) { _farTry = Time.time + 10f; if (Unstick(at - who.transform.forward * 2f, "too far behind (" + Mathf.RoundToInt(d) + " m)")) return; } // (seen: on foot after them, asked again in ten seconds)
            if (d > p.Keep)
            {
                if (d < p.Best - 0.5f || d < p.Keep + 2f) { p.Best = d; p.BestAt = Time.time; }
                if (Time.time - p.BestAt > StuckSeconds) { Unstick(at - who.transform.forward * 2f, "could not keep up"); return; }
                Character lead = who.GetComponent<Character>();
                float speed = lead != null ? Utils.DistanceXZ(lead.GetVelocity(), Vector3.zero) : 0f;
                Pace pace = speed > 5.5f ? Pace.Run : speed > 2.5f ? Pace.Jog : Pace.Walk; // (the leader's own pace)
                if (d > p.Keep + 6f && pace != Pace.Run) pace = pace + 1; // (left behind: one pace up, to catch up)
                Move(dt, at, p.Keep, pace);
                Status = Lang.K("іде слідом");
            }
            else { p.Best = d; p.BestAt = Time.time; Status = Lang.K("поруч"); if (OutOf()) return; StopMoving(); LookAt(Eye(who)); } // (two come after one: out of each other's body)
        }

        private float _farTry, _leaderLook;

        // Its feet got nowhere after the one it follows (HomeFromStuck: a roof, a rock, an island no way leads to). Taken home it walked back
        // after them into the same trap and was taken home again, round and round through its house; one not settled yet just pushed on for
        // good. So a follow is never taken home for getting nowhere: it stands where it got to, turned to its leader, and goes after them again
        // once they have moved WaitMoved m from where they stood (up or down counts) or come back to it. A body held inside a thing is still freed
        internal static float WaitMoved = 4f;
        private void FollowWaits(Plan p, string why)
        {
            GameObject who = Find(p.Who);
            p.Waits = true; p.WaitAt = who != null ? who.transform.position : transform.position;
            p.Best = float.MaxValue; p.BestAt = Time.time;
            StopMoving();
            Npcs.Log(Name() + ": " + why + " after the one it follows — waits where it is till they move on");
            StoryBus.Raise("npc_stuck", Name(), 1, transform.position);
        }

        private void Wandering(float dt, Plan p)
        {
            if (!p.Started) { p.Started = true; p.To = transform.position; p.NextWander = Time.time + 1f; }
            float d = Utils.DistanceXZ(transform.position, p.To);
            bool walking = d > 1.2f && Time.time - p.BestAt < StuckSeconds;
            if (walking) { if (d < p.Best - 0.5f) { p.Best = d; p.BestAt = Time.time; } Move(dt, p.To, 1f, PaceFor(d)); Status = Lang.K("походжає"); return; }
            StopMoving();
            LookAround(8f);
            Status = Lang.K("походжає");
            if (Time.time < p.NextWander) return;
            p.NextWander = Time.time + UnityEngine.Random.Range(10f, 25f); // (a while where it is before the next spot)
            Vector2 r = UnityEngine.Random.insideUnitCircle * p.Radius;
            Vector3 spot = NpcKit.Floor(p.Home + new Vector3(r.x, 0f, r.y));
            if (NpcKit.OnFoot(transform.position, spot, WanderWalk) > WanderWalk && ++p.Lost < 6) { p.NextWander = Time.time + 1f; return; } // (a few more spots first; then it goes all the same — no whole way from where it stands at all (a room the way finder keeps apart): it walks, steps round, is put there if stuck — not kept in it for good)
            p.Lost = 0; // (a whole way there on foot, not long: not a spot behind a house it walks into the wall of and stands — the game's «a path» is also half of one)
            p.To = spot; p.Best = float.MaxValue; p.BestAt = Time.time;
        }

        internal static float SeatStall = 1.2f, SeatFrom = 1.7f; // s with no half metre gained · m from its seat: it sits from there
        private void Sitting(float dt, Plan p)
        {
            if (!p.Started)
            {
                p.Started = true;
                Chair under = Body != null && Body.SittingLeft ? NearestChair(transform.position, 2f, false, this) : null; // (come to be run here while it sits: the seat it is on, not the nearest free one round its plan)
                if (under != null && Vector3.Distance(SeatPoint(under), transform.position) < 0.5f) p.Seat = under;
                else p.Seat = p.Kind == Task.Eat || p.Guest ? NearestChair(p.To, MealSeats, true, this, true) : NearestChair(p.To, SeatRange, false, this, true); // (at a table — a meal, an evening at the dining table: anywhere round it in its room; a long table's seats stand far from its middle) // (a meal: anywhere round its dining table — on foot, not in the next building)
                if (p.Seat == null) { p.To = ClearGround(p.To); Npcs.Log(Name() + ": no seat near — sits on the ground"); } // (not on a bed, a table, a wall)
                else p.HasSide = SideOf(p.Seat, transform.position, out p.Side);
            }
            if (p.Seated)
            {
                if (p.OnPoint && (Body == null || !Body.Attached)) { p.Seated = false; p.Started = false; p.OnPoint = false; return; } // (its seat was taken down under it: finds another)
                StopMoving(); return; // (on a seat the body holds it; on the ground the pose does)
            }
            Transform seat = p.Seat != null ? (p.Seat.m_attachPoint != null ? p.Seat.m_attachPoint : p.Seat.transform) : null;
            Vector3 goal = seat != null && p.HasSide ? p.Side : seat != null ? seat.position : p.To;
            float d = Utils.DistanceXZ(transform.position, goal);
            bool there = seat != null && p.HasSide ? d <= 0.5f || Utils.DistanceXZ(transform.position, seat.position) <= 0.9f : d <= 1.4f || AtSnap(goal, 1.4f); // (by its seat: at the side it came to, or right at it)
            // by its seat and no nearer for a while — its back, its end, a table beside it stand between: it sits from where it is (the body glides onto
            // it, as from the side it meant to come to). Till 2026-11-20 its feet «stepped up» onto the bench there and it sat from on top of it; now
            // that nobody steps on furniture (NpcKit.Furniture), Eyvind pushed at his own bench for a minute («pushes, no move», halts.txt)
            if (!there && seat != null && Time.time - p.BestAt > SeatStall && m_character.GetVelocity().magnitude < 0.15f && Utils.DistanceXZ(transform.position, seat.position) <= SeatFrom) there = true; // (standing against it: one still coming at its ease is let come)
            if (!there)
            {
                if (d < p.Best - 0.5f) { p.Best = d; p.BestAt = Time.time; }
                if (seat != null && _feet != null && _feet.CantReach(goal)) { if (Body != null && Body.OffFurniture()) return; /* (no way from the top of a workbench it was put on: down first — not its seat kept off for everyone) */ NpcWalk.KeepOff(seat.position); Npcs.Log(Name() + ": no way to its seat — another"); p.Started = false; p.Seat = null; p.HasSide = false; return; } // (behind a table, up on a floor apart: it is kept off it a while — NpcWalk.Avoided)
                if (seat == null && _feet != null && _feet.CantReach(goal)) there = true; // (its spot on the ground: here will do)
                else if (Time.time - p.BestAt <= StuckSeconds) { Move(dt, goal, seat != null && p.HasSide ? 0.3f : 1f, PaceFor(d)); Status = Lang.K("іде сісти"); return; } // (to its side of the seat itself: it sits from half a metre — stopped a metre off, it stood «not there» till the stuck rule put it on)
                if (seat != null && !Unstick(goal, "could not reach its seat", false, OwnGoal)) { seat = null; p.Seat = null; } // (seen: no jump onto it — it sits on the ground where it is)
            }
            StopMoving();
            if (seat == null && Body != null && Body.Sliding) return; // (not sat down on the ground mid-slide: where the slide ends)
            if (seat == null && Body != null && Body.OffFurniture()) { p.Started = false; return; } // (to sit on the ground where it stands — the top of a workbench it was put on, no way off it: 2026-11-20, Eyvind; down first, then it looks for its seat anew: OffFurniture below)
            p.Seated = true;
            if (_feet != null) _feet.There(seat != null ? "ok (seat)" : "ok (ground)");
            if (seat != null && Taken(seat, this)) { seat = null; Npcs.Log(Name() + ": its seat was taken — sits on the ground"); } // (someone sat there while it walked)
            if (seat != null && Body != null)
            {
                p.OnPoint = true;
                Piece piece = p.Seat.GetComponentInParent<Piece>();
                Body.Attach(seat, p.Seat.m_attachAnimation, piece != null ? piece.gameObject : p.Seat.gameObject, p.Seat.m_detachOffset);
                Status = Lang.K("сидить");
            }
            else { FaceFire(); if (Body != null) Body.Gesture("sit", true); Status = Lang.K("сидить на землі"); }
            StoryBus.Raise("npc_sat", Name(), 0, transform.position);
        }

        // to its bed (Who: the bed's ZDO) and into it, as the game puts a player to bed; no bed there: it stands and says so
        private void Sleeping(float dt, Plan p)
        {
            if (p.Seated)
            {
                if (p.OnPoint && (Body == null || !Body.Attached)) { p.Seated = false; p.OnPoint = false; } // (the bed was taken down under it)
                else { StopMoving(); return; }
            }
            GameObject go = Find(p.Who);
            Bed bed = go != null ? go.GetComponent<Bed>() : null;
            if (bed == null || bed.m_spawnPoint == null) { StopMoving(); Status = Lang.K("не знайшов|знайшла ліжка"); return; }
            Vector3 at = bed.m_spawnPoint.position;
            float d = Utils.DistanceXZ(transform.position, at);
            if (d > 1.6f)
            {
                if (d < p.Best - 0.5f) { p.Best = d; p.BestAt = Time.time; }
                if (Time.time - p.BestAt <= StuckSeconds) { Move(dt, at, 1f, PaceFor(d)); Status = Lang.K("іде спати"); return; }
                if (!Unstick(at, "could not reach its bed")) { Move(dt, at, 1f, PaceFor(d)); return; } // (seen: tries on — a bed is never sat by from afar)
            }
            StopMoving();
            p.Seated = true;
            if (_feet != null) _feet.There("ok (bed)");
            if (Body != null) { p.OnPoint = true; Body.Attach(bed.m_spawnPoint, "attach_bed", bed.gameObject, new Vector3(0f, 0.5f, 0f)); } // (the game's own way off a bed)
            Status = Lang.K("спить");
            StoryBus.Raise("npc_slept", Name(), 0, transform.position);
        }

        // its job: the worker does the errands, this lends it its feet (Walk/Halt/Face)
        private NpcWorker _workerL;
        private const float WorkLook = 6f;
        private float _nextWorkLook;
        private void Working(float dt, Plan p)
        {
            if (_workerL == null) _workerL = GetComponent<NpcWorker>();
            if (_workerL == null) { Idling(p, 6f); return; }
            if (!p.Started) { p.Started = true; _workerL.Reset(); } // (taken up again after a fight, an order, a whim: the errand afresh)
            _workerL.Step(dt);
            Status = _workerL.Doing.Length > 0 ? _workerL.Doing : Lang.K("працює");
            if (_workerL.Idle) { p.Yield = true; _nextWorkLook = Time.time + WorkLook; Npcs.Log(Name() + ": no work just now (" + _workerL.Doing + ") — its own day meanwhile"); } // (nothing to do: not standing about)
        }

        // for the worker: to a spot (true once there; a walk that gets nowhere steps it there, as every walk does)
        internal bool Walk(float dt, Vector3 to, float near)
        {
            float d = Utils.DistanceXZ(transform.position, to);
            if (d <= near || AtSnap(to, near)) { StopMoving(); if (_feet != null) _feet.There("ok (work)"); return true; } // (at the walkable spot by it: there)
            if (_cur != null)
            {
                if (d < _cur.Best - 0.5f) { _cur.Best = d; _cur.BestAt = Time.time; }
                if (Time.time - _cur.BestAt > StuckSeconds) { Vector3 by = Beside(to, near); if (!Unstick(by, "could not reach its work (goal " + Npcs.V(to) + ", it at " + Npcs.V(transform.position) + ", spot by it " + Npcs.V(by) + ")") && d > near + 2f) return false; return true; } // (seen and far: no reaching into a chest eleven metres off — it keeps trying; the last safety takes it home if it never gets there)
            }
            Move(dt, to, near * 0.8f, PaceFor(d));
            return false;
        }

        // ---------------------------------------------------------------- its pace
        internal enum Pace { Walk, Jog, Run }

        // the one rule: by what it is busy with (the layer) and how far
        // the pace is picked as a way begins and kept along it (no run → jog → walk at every few metres as it nears): up
        // only when it has fallen far behind (a leader walked off), down to a walk only for its last steps
        private Plan _pacePlan; private Pace _pace;
        private Pace PaceFor(float dist)
        {
            Pace want = PaceRule(dist);
            if (!ReferenceEquals(_cur, _pacePlan)) { _pacePlan = _cur; _pace = want; }
            else if (want > _pace) _pace = want;
            else if (want < _pace && (dist < 4f || (_cur != null && _cur.Hurry && dist < HurryWalk))) _pace = want; // (in a hurry: a walk already its last HurryWalk m)
            return _pace;
        }

        internal static float HurryWalk = 16f; // (m from its goal one in a hurry walks: Plan.Hurry — inside the Hall, from its gate to the fire)
        private Pace PaceRule(float dist)
        {
            switch (_curLayer)
            {
                case Layer.Fight: return Pace.Run;
                case Layer.Scene: return Pace.Walk;                                                  // (a scene says «run» itself)
                case Layer.Order: if (_cur != null && _cur.Ease) goto default; if (_cur != null && _cur.Hurry) return dist > HurryWalk ? Pace.Jog : Pace.Walk; return dist > 30f ? Pace.Run : dist > 12f ? Pace.Jog : Pace.Walk;
                default: return dist > 80f ? Pace.Jog : Pace.Walk;                                   // (at its ease and at its work: a walk — about its base never a run, the user 2026-11-08)
            }
        }

        private NpcWalk _feet;
        private string _walkLabel = "";
        internal NpcWalk Feet { get { if (_feet == null) _feet = new NpcWalk(this); return _feet; } }

        private void Move(float dt, Vector3 to, float near, Pace pace)
        {
            if (ThroughDoor(dt, to, pace)) return; // (no way there but through a shut door: NpcDoors)
            m_character.SetWalk(pace == Pace.Walk); // (the game: walk / jog / run — not walking is a jog)
            if (Body != null && (Body.Attached || Body.HeldGesture.Length > 0)) // (it walks: no seat under it, no pose held — a body held sitting does not walk, it was slid along seated, 2026-11-01)
            {
                Npcs.Log(Name() + ": walks off " + (Body.Attached ? "attached to a seat" : "in a held pose «" + Body.HeldGesture + "»") + " (" + _curLayer + "/" + (_cur != null ? _cur.Kind.ToString() : "-") + ") — let go");
                if (Body.Attached) Body.Detach(); else Body.Gesture("", false);
            }
            if (Body != null && _curLayer != Layer.Scene) Body.LoopOff(); // (a scene may walk it in a loop the walk can carry — arms, a look: its own to end; one that cannot goes in ClipFirst below)
            if (Body != null && _curLayer != Layer.Fight && Body.ClipFirst()) { Paused(); StopMoving(); return; } // (a clip of ours still on it — a key turned, a pose held — goes off first by its own way out: it walks once that has faded. Never in a fight or a flight: no standing still under a blow; going into one already takes its clip off)
            _pausedPlan = null;
            Feet.Step(dt, to, near, pace == Pace.Run, _walkLabel); // (its feet: NpcWalk — the one way it walks)
        }

        // standing on the walkable spot by a goal it could not stand on itself (a chest, a seat in a row: Move went there
        // instead): as good as there — else it stood by it «not there yet», and was stepped at last
        // where to step it when it could not reach a goal: the walkable spot by it (its own side of a wall, not the side it
        // stood on), else a little short of it on its way
        private Vector3 Beside(Vector3 to, float near)
        {
            Vector3 v;
            if (Pathfinding.instance != null && (NpcKit.SameFloor(out v, to, Mathf.Max(1.5f, near + 1f), NpcKit.Walker) || NpcKit.FloorBy(out v, to, NpcKit.Walker))) return v;
            return to + (transform.position - to).normalized * near * 0.8f;
        }

        private bool AtSnap(Vector3 to, float near)
        {
            return Feet.By(to, near); // (its follower stopped by it, within reach)
        }

        // room for a body here: nothing built or a rock in it, nobody standing on it
        private static readonly int Solids = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");
        private bool NoRoom(Vector3 p)
        {
            if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.3f, Solids, QueryTriggerInteraction.Ignore)) return true;
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.gameObject != gameObject && Utils.DistanceXZ(o.transform.position, p) < 0.6f) return true;
            return false;
        }

        // ---------------------------------------------------------------- through a door (NpcDoors): to the spot before it,
        // open it, straight through, shut it behind (only one it opened); asked once a second while it has no way
        private Door _via;
        private Vector3 _viaNear, _viaFar;
        private int _viaStage;
        private float _viaSince, _nextWayLook;
        private bool _viaOpened;
        // the doors tried for one goal: a way through a door that still left no way there was taken again and again, and
        // each try told it it was not stuck — it never stepped on either (2026-10-31: residents stood by a door for
        // minutes). Three tries for a goal, then the doors are left for it and its own «stuck» rule steps it on
        private Vector3 _doorGoal; private int _doorTries;
        private readonly HashSet<Door> _doorsTried = new HashSet<Door>();
        private float _doorAt;
        private const int DoorTries = 3; // (three doors a goal, never the same one twice: a house door, then the yard's gate — one door only left it standing at the gate, 2026-11-01; the same door again sent it back and forth, 2026-10-31)

        private static readonly int PerfDoorWay = NpcPerf.Id("door way (чи треба двері)");
        private bool ThroughDoor(float dt, Vector3 to, Pace pace)
        {
            if (_via == null)
            {
                if (Time.time < _nextWayLook || Pathfinding.instance == null) return false;
                if (_curLayer == Layer.Fight && _cur != null && _cur.Kind == Task.Attack) return false; // (no gate opened to chase a foe out — nor to let it in)
                _nextWayLook = Time.time + 1f;
                // the walkable spot by its goal: a chest, a table, a seat stands where nobody can stand — a way «into» it is
                // never found, an open door or not, and it went looking for doors inside the very room it was in
                if (Utils.DistanceXZ(to, _doorGoal) > 2f || Time.time - _doorAt > 60f) { _doorGoal = to; _doorTries = 0; _doorsTried.Clear(); } // (another goal, or a minute since: its doors afresh)
                if (_doorTries >= DoorTries || Utils.DistanceXZ(transform.position, to) < 1.5f) return false; // (its doors led nowhere for this goal: no more — asked before the way finder, which is the costly part)
                // its feet know whether their way gets there (a whole way asked here every second cost up to ten
                // milliseconds a time — the people's worst frames, 2026-11-02): a door looked for only when it does not
                if (_feet == null || !_feet.NeedsDoor(to)) return false;
                Vector3 goal;
                if (!NpcKit.SameFloor(out goal, to, 2.5f, NpcKit.Walker)) goal = to;
                long pw = NpcPerf.Now();
                Door d = NpcDoors.Toward(transform.position, goal, NpcKit.Walker, _doorsTried, this);
                NpcPerf.Add(PerfDoorWay, pw);
                if (d == null) { _doorTries = DoorTries; _doorAt = Time.time; return false; } // (no door leads there: not looked for again for this goal — it was asked every second)
                _doorAt = Time.time;
                if (++_doorTries >= DoorTries) Npcs.Log(Name() + ": its doors lead nowhere for this goal — the last try");
                _doorsTried.Add(d);
                _via = d; _viaStage = 0; _viaSince = Time.time; _viaOpened = false;
                NpcDoors.Sides(d, transform.position, out _viaNear, out _viaFar);
                Npcs.Log(Name() + ": no way there — through a door");
            }
            if (_via == null || Time.time - _viaSince > 20f) { GiveUpVia(); return false; }
            NewGoal(); // (a way round a door is no being stuck)
            m_character.SetWalk(pace == Pace.Walk);
            if (_viaStage == 0)
            {
                if (Utils.DistanceXZ(transform.position, _viaNear) > 0.9f) { Feet.Step(dt, _viaNear, 0.5f, pace == Pace.Run, "door"); return true; }
                _viaOpened = NpcDoors.Open(_via, transform.position);
                if (!_viaOpened && NpcDoors.Closed(_via)) { StopMoving(); LookAt(_via.transform.position + Vector3.up); return true; } // (someone told it a moment ago: its state on its way — it waits, within its 20 s)
                _viaStage = 1; _viaSince = Time.time;
            }
            if (Time.time - _viaSince < 0.6f && _viaOpened) { StopMoving(); LookAt(_via.transform.position + Vector3.up); return true; } // (the door swings open)
            Vector3 dir = _viaFar - transform.position; dir.y = 0f;
            if (dir.magnitude > 0.7f) { Steer(dir.normalized, pace == Pace.Run); return true; }
            if (_viaOpened && !NpcDoors.Shut(_via, this)) Feet.Track(_via, _viaNear); // (someone in the doorway: its feet shut it once it is free)
            EndVia();
            _nextWayLook = Time.time + 5f; // (through: a while on foot before any other door)
            return false;
        }

        private void EndVia() { _via = null; _viaOpened = false; _nextWayLook = Time.time + 0.5f; if (_feet != null) _feet.Forget(); } // (through: its way from here, not the one from before the door)

        // its way through given up (another plan took over, it took too long): a door it opened is not left open
        private void GiveUpVia() { if (_via != null && _viaOpened && !NpcDoors.Shut(_via, this)) Feet.Track(_via, _viaNear); EndVia(); }

        // ---------------------------------------------------------------- held by someone talking to it (its menu, its talk
        // window): it stops and looks at them (a few seconds, renewed while the window is open: NpcActor «hold»)
        private float _holdUntil;
        private ZDOID _holdBy = ZDOID.None;
        internal bool InTalk { get { return Time.time < _holdUntil; } } // (someone talks to it now: what the story has next for it waits)
        internal static float AfterTalk = 8f; // s at least its stay (a whim's) goes on after a talk
        private bool _heldNow; // (its talk's first moment seen: Think0)
        internal void Hold(ZDOID by)
        {
            _holdUntil = Time.time + 4f; _holdBy = by;
            for (int i = 0; i < Layers; i++) if (_lookWho[i] == by) _lookTill[i] = Mathf.Max(_lookTill[i], Time.time + LookFor);
            ZDO z = Actor != null ? Actor.Zdo : null;
            if (z == null || ZNet.instance == null || (by == _heldMarked && Time.time < _heldMarkNext)) return; // (written at most every HeldMarkEvery s: one talk, a few small sends)
            _heldMarked = by; _heldMarkNext = Time.time + HeldMarkEvery;
            z.Set(KHeldBy, NpcKit.Ref(by)); // session: the holder by its id now, read only within HeldFresh s
            z.Set(KHeldAt, (long)ZNet.instance.GetTimeSeconds());
        }

        // the one holding it let go (its talk closed): free for another at once
        internal void Unhold(ZDOID by)
        {
            if (_holdBy != by) return;
            _holdUntil = 0f; _heldMarked = ZDOID.None;
            ZDO z = Actor != null ? Actor.Zdo : null;
            if (z != null && z.GetString(KHeldBy, "").Length > 0) z.Set(KHeldBy, "");
        }

        // who holds it now, as every game sees it (its talk is one player's at a time: NpcTalk.Open): a player's character id, or none
        internal static readonly int KHeldBy = "RavenWatch_npcHeldBy".GetStableHashCode(), KHeldAt = "RavenWatch_npcHeldAt".GetStableHashCode();
        internal const float HeldMarkEvery = 2.5f, HeldFresh = 7f;
        private ZDOID _heldMarked = ZDOID.None; private float _heldMarkNext;
        internal static ZDOID HeldBy(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || ZNet.instance == null) return ZDOID.None;
            string s = z.GetString(KHeldBy, "");
            return s.Length > 0 && ZNet.instance.GetTimeSeconds() - z.GetLong(KHeldAt, 0L) < HeldFresh ? NpcKit.Parse(s) : ZDOID.None;
        }

        // its hand into a chest, as a player reaches into one: facing it, its lid up (NpcLid) and the game's «interact»
        // move; true once the hand is in (ReachFor s later) — the one way every module takes from or puts into a chest (a
        // meal's bite, its pay); hand: the caller's own moment, 0 before
        internal const float ReachFor = 1.1f;
        internal bool Reach(Container c, ref float hand)
        {
            if (c == null) return false;
            Face(c.transform.position);
            if (hand <= 0f) { if (!Open(c, ReachFor + 2f)) return false; hand = Time.time + ReachFor; return false; }
            if (Time.time < hand) return false;
            hand = 0f;
            return true;
        }

        // a chest opened by it now, as a player opens one (the one way for every module: a worker's chest, a chore's, a
        // meal's, its pay): not one a player or another person has open; its owner's game keeps its contents, so taken over
        // first; its lid up for so many seconds (NpcLid), facing it, its hand into it as the lid comes up (reach_in where seen, else the game's «interact»). False:
        // not now (open by someone, not ours to take)
        internal bool Open(Container c, float seconds)
        {
            ZNetView nv = c != null ? c.GetComponentInParent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return false;
            if (nv.GetZDO().GetInt(ZDOVars.s_inUse, 0) != 0 && !NpcLid.Mine(c, Actor)) return false;
            if (!nv.IsOwner()) { nv.ClaimOwnership(); if (!nv.IsOwner()) return false; }
            bool up = NpcLid.Mine(c, Actor);
            NpcLid.Peek(c, Actor, seconds);
            Face(c.transform.position);
            if (!up && !(Body != null && Actor != null && NpcClock.Seen(Actor) && AtArm(c) && Body.Move("reach_in")) && m_animator != null) m_animator.SetTrigger("interact"); // (our reach where a player sees it and the chest is at its hand; from further off the game's own gesture)
            return true;
        }

        // the thing within its arm: its nearest surface no further than ArmReach ahead of it (a chest opened from a step off: no hand into the air)
        internal static float ArmReach = 0.85f;
        internal bool AtArm(Component c) { return c != null && Utils.DistanceXZ(transform.position, Nearest(c, transform.position)) <= ArmReach; }

        // a thing's nearest point to «from» at a hand's height: its solid collider (not a trigger: a fire's heat, a smoke), found once a thing
        // and kept (Solid); a mesh that is not convex by its bounds (Physics.ClosestPoint takes box, sphere, capsule, convex mesh only — else a
        // warning in the log each call)
        private static readonly Dictionary<int, Collider> Solid = new Dictionary<int, Collider>(); // session: a thing's instance → its collider
        internal static Vector3 Nearest(Component c, Vector3 from)
        {
            int id = c.GetInstanceID();
            Collider col;
            if (!Solid.TryGetValue(id, out col) || (col == null && !ReferenceEquals(col, null)))
            {
                col = null;
                foreach (Collider k in c.GetComponentsInChildren<Collider>()) if (k != null && !k.isTrigger && k.enabled) { col = k; break; } // heavy: once a thing
                if (Solid.Count > 512) Solid.Clear();
                Solid[id] = col;
            }
            Vector3 at = from + Vector3.up * 0.8f;
            if (col == null) return c.transform.position;
            MeshCollider mc = col as MeshCollider;
            return mc != null && !mc.convex ? col.bounds.ClosestPoint(at) : col.ClosestPoint(at);
        }

        internal void NewGoal() { if (_cur != null) { _cur.Best = float.MaxValue; _cur.BestAt = Time.time; } }
        internal void Halt() { StopMoving(); }
        internal void Face(Vector3 at) { LookAt(at); }

        // a long way (NpcTravel): a jog towards a point a little ahead on the way, by the game's paths where there are
        // some, else straight on (round nothing: over water it swims)
        private Vector3 _legTo = new Vector3(float.MaxValue, 0f, 0f), _legFor;
        private float _legUntil;
        internal void Head(float dt, Vector3 to, float ahead, bool walk = false)
        {
            if (Utils.DistanceXZ(transform.position, to) < 60f && ThroughDoor(dt, to, walk ? Pace.Walk : Pace.Jog)) return; // (near its goal: a gate on the way is opened)
            Vector3 dir = to - transform.position; dir.y = 0f;
            float d = dir.magnitude;
            if (d < 0.1f) { StopMoving(); return; }
            // a leg at a time: a point «ahead» on the line, kept till it is half walked or its far goal changes (its feet
            // follow the way finder to it; a point moved every frame would be a new walk every frame)
            if (_legTo.x == float.MaxValue || (to - _legFor).sqrMagnitude > 1f || Utils.DistanceXZ(transform.position, _legTo) < ahead * 0.5f || Time.time > _legUntil)
            {
                _legFor = to;
                _legTo = d > ahead ? transform.position + dir / d * ahead : to;
                float h;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(_legTo, out h)) _legTo.y = h;
                _legUntil = Time.time + ahead / 1.5f + 5f;
            }
            m_character.SetWalk(walk); // (a jog; a walk when it limps)
            Feet.Step(dt, _legTo, 1f, false, "travel");
        }

        private void Posing(Plan p)
        {
            StopMoving();
            if (p.Looks) LookAt(p.Look); else LookAround(6f);
            if (p.Started) return;
            p.Started = true;
            if (Body != null) Body.Gesture(p.Pose, true);
            Status = Lang.K("стоїть") + " (" + p.Pose + ")";
        }

        // ---------------------------------------------------------------- fighting
        // a foe for its way of fighting
        private static readonly List<Character> Seen = new List<Character>(); // (who is about now: NpcFoes, one list for everyone)

        private void LookForFoe()
        {
            if (_fight == Fight.Off || Now == Task.Travel) return;
            Character foe = null;
            if (_hurtBy != null && !_hurtBy.IsDead() && !_hurtBy.IsPlayer() && Time.time - _hurtAt < 20f) foe = _hurtBy;
            if (foe == null)
            {
                Plan below = Below();
                GameObject leader = below != null && below.Kind == Task.Follow ? Find(below.Who) : null;
                float best = _fight == Fight.Hunt ? m_viewRange : Runs ? (Actor != null ? NpcPersona.Of(Actor).FleeAt * FleeScale(Actor) : FleeRange) : DefendRange; // (a timid one runs sooner; a module may say sooner still)
                float lookout = Runs ? 0f : LookOut(Actor); // (on watch: as far as it sees — a module's word)
                bool watch = lookout > 0f;
                if (watch) best = Mathf.Max(best, lookout);
                Seen.Clear();
                NpcFoes.Near(transform.position, best, Seen); // (only the cells within its sight: NpcFoes)
                if (leader != null) NpcFoes.Near(leader.transform.position, best, Seen); // (and round the one it follows)
                foreach (Character c in Seen)
                {
                    if (c == null || c == m_character || c.IsDead() || c.IsPlayer() || !IsEnemy(m_character, c) || Neutral(c)) continue;
                    if (Time.time < _noReachUntil && c.GetZDOID() == _noReach && Vector3.Distance(c.transform.position, transform.position) > NoReachNear) continue; // (it could not get to that one a moment ago: not after it again at once — unless it comes close, or hits it: _hurtBy above)
                    if (!Runs && !InWatch(c.transform.position)) continue; // (a guard's post, a resident's base: not after foes far off)
                    float d = Vector3.Distance(c.transform.position, transform.position);
                    if (leader != null) d = Mathf.Min(d, Vector3.Distance(c.transform.position, leader.transform.position));
                    if (d >= best) continue;
                    if ((_fight == Fight.Hunt || (watch && d > DefendRange)) && !CanSenseTarget(c)) continue;
                    best = d; foe = c;
                }
            }
            if (foe != null) StartFight(foe);
        }

        // the modules' words on its fighting (NpcModules): where it keeps watch (true/false; null — not theirs: the first
        // that says wins — a guard by its post before a resident by its base; none: anywhere), how far it looks out on watch
        // (0: not on watch), how much sooner a peaceful one runs (×)
        internal static readonly List<Func<NpcActor, Vector3, bool?>> Watches = new List<Func<NpcActor, Vector3, bool?>>();
        internal static readonly List<Func<NpcActor, float>> LookOuts = new List<Func<NpcActor, float>>();
        internal static readonly List<Func<NpcActor, float>> FleeScales = new List<Func<NpcActor, float>>();

        private bool InWatch(Vector3 at)
        {
            NpcActor a = Actor;
            if (a == null) return true;
            for (int i = 0; i < Watches.Count; i++) { bool? w = Watches[i](a, at); if (w.HasValue) return w.Value; }
            return true;
        }

        private static float LookOut(NpcActor a)
        {
            float far = 0f;
            if (a != null) for (int i = 0; i < LookOuts.Count; i++) far = Mathf.Max(far, LookOuts[i](a));
            return far;
        }

        private static float FleeScale(NpcActor a)
        {
            float k = 1f;
            for (int i = 0; i < FleeScales.Count; i++) k *= FleeScales[i](a);
            return k;
        }

        // no foe unless it means harm: a peaceful beast (a deer, a hare), one that is calm until angered (the dwarves)
        private static bool Neutral(Character c)
        {
            BaseAI ai = c.GetBaseAI();
            if (ai == null) return false;
            if (ai is AnimalAI) return true;
            return ai.IsAggravatable() && !ai.IsAggravated();
        }

        // A foe it could not get to in 40 s (behind a wall, up a rock, over water): its fight ended, and half a second later the same foe was the
        // nearest again — the same 40 s over and over, a word on the bus and in the log each time (read in the code 2026-11-11, not measured).
        // That one is left alone NoReachFor s, unless it comes within NoReachNear m or hits it
        internal static float NoReachFor = 30f, NoReachNear = 4f;
        private ZDOID _noReach = ZDOID.None; private float _noReachUntil;
        private void NoReach(ZDOID foe) { _noReach = foe; _noReachUntil = Time.time + NoReachFor; }

        // the plan a fight put aside (the top one under the Fight layer)
        private Plan Below() { for (int i = (int)Layer.Fight - 1; i >= 0; i--) if (_plans[i] != null) return _plans[i]; return null; }

        private void StartFight(Character foe)
        {
            if (_plans[(int)Layer.Scene] != null) return; // (a scene has it: it does not run off to fight)
            ZNetView nv = foe.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            bool flee = Runs;
            Plan f = new Plan { Kind = flee ? Task.Flee : Task.Attack, Who = nv.GetZDO().m_uid, Home = transform.position, Since = Time.time, FoeHealth = foe.GetHealthPercentage() };
            Set(Layer.Fight, f);
            Pick();
            if (Body != null && Body.ClipBusy) Body.Clip(""); // (into a fight or a flight: no gesture left on its body)
            Status = flee ? Lang.K("тікає") : Lang.K("б'ється");
            Npcs.Log(Name() + (flee ? ": runs from " : ": fights ") + foe.m_name);
        }

        private void Attacking(float dt, Plan p)
        {
            GameObject go = Find(p.Who);
            Character foe = go != null ? go.GetComponent<Character>() : null;
            Humanoid me = m_character as Humanoid;
            if (foe == null || foe.IsDead() || me == null) { EndFight((foe != null && foe.IsDead()) || (foe == null && p.FoeHealth < 0.35f) ? "won" : "lost sight"); return; } // (a slain foe is gone from the world at once: low health just before = won)
            p.FoeHealth = foe.GetHealthPercentage();
            Status = Lang.K("б'ється");
            Plan below = Below();
            GameObject leader = below != null && below.Kind == Task.Follow ? Find(below.Who) : null;
            Vector3 anchor = leader != null ? leader.transform.position : p.Home;
            if (Vector3.Distance(foe.transform.position, anchor) > Leash && _fight != Fight.Hunt) { EndFight("it ran too far"); return; }
            float d = Vector3.Distance(transform.position, foe.transform.position);
            bool farWeapon = NpcGear.Ranged(me != null ? me.GetCurrentWeapon() : null);
            if (!InWatch(foe.transform.position) && !(_hurtBy == foe && Time.time - _hurtAt < 5f) && (d > (farWeapon ? 22f : 8f) || !InWatch(transform.position))) { EndFight("left its watch"); return; } // (back to its post / its base — not the moment a foe steps over its edge: one with a bow shoots on while it is in range, the user saw a guard aim and walk off)
            if (_fight == Fight.Wary && d > BackOff) { p.Kind = Task.Flee; p.BestAt = 0f; p.Since = Time.time; Npcs.Log(Name() + ": backs off again"); return; } // (the foe fell back: away again)
            NpcActor self = Actor;
            if (self != null && self.Vitals != null && self.Vitals.Retreats) { DrawStop(me); p.Kind = Task.Flee; p.BestAt = 0f; p.Since = Time.time; Npcs.Log(Name() + ": hurt — falls back"); return; } // (healed, it comes back: NpcVitals)
            if (self != null && self.Gear != null) { self.Gear.Draw(me); self.Gear.Wield(me, d > 3f); } // (off its back; the far weapon unless the foe is on it, the near one then)
            ItemDrop.ItemData weapon = me.GetCurrentWeapon();
            if (NpcGear.Ranged(weapon)) { Shoot(dt, p, foe, me, weapon, d); return; }
            DrawStop(me);
            float reach = weapon != null ? Mathf.Max(1.6f, weapon.m_shared.m_aiAttackRange) : 1.6f;
            if (d > reach)
            {
                Move(dt, foe.transform.position, reach * 0.8f, Pace.Run);
                if (Time.time - p.Since > 40f) { NoReach(p.Who); EndFight("could not reach it"); }
                return;
            }
            StopMoving();
            LookAt(foe.GetCenterPoint());
            float interval = weapon != null ? Mathf.Max(0.8f, weapon.m_shared.m_aiAttackInterval) : 1.5f;
            if (weapon != null && !m_character.InAttack() && Time.time - weapon.m_lastAttackTime > interval && IsLookingAt(foe.GetCenterPoint(), 25f))
                m_character.StartAttack(foe, false);
        }

        // ---------------------------------------------------------------- shooting (a bow drawn as a player draws it; a crossbow
        // shot when its reload time is up). Not too near (it steps back), not too far, and only with a clear line to the foe.
        private static readonly System.Reflection.FieldInfo DrawTimeField = HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_attackDrawTime");
        private float _drawn;
        private static int _sightMask;

        private void Shoot(float dt, Plan p, Character foe, Humanoid me, ItemDrop.ItemData weapon, float d)
        {
            Status = Lang.K("стріляє");
            bool bow = weapon.m_shared.m_attack.m_bowDraw;
            float far = bow ? 18f : 22f;
            Vector3 aim = foe.GetCenterPoint();
            if (_sightMask == 0) _sightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            bool clear = !Physics.Linecast(m_character.GetEyePoint(), aim, _sightMask, QueryTriggerInteraction.Ignore);
            if ((d > far || !clear) && Time.time - p.Since > 40f) { DrawStop(me); NoReach(p.Who); EndFight("could not reach it"); return; } // (no line to it all that while: given up)
            if (d > far || !clear) { DrawStop(me); Move(dt, foe.transform.position, Mathf.Min(far * 0.6f, d - 1f), Pace.Run); return; } // (nearer, or round to where it can see it)
            if (d < 5f) { DrawStop(me); Vector3 away = transform.position - foe.transform.position; away.y = 0f; Move(dt, transform.position + away.normalized * 5f, 0.5f, Pace.Run); return; } // (too near for a shot: a step back)
            StopMoving();
            LookAt(aim);
            if (!IsLookingAt(aim, 12f) || m_character.InAttack()) return;
            if (!bow)
            {
                float reload = Mathf.Max(1.5f, weapon.m_shared.m_attack.m_reloadTime > 0f ? weapon.m_shared.m_attack.m_reloadTime * 1.2f : 3f);
                if (Time.time - weapon.m_lastAttackTime > reload) m_character.StartAttack(foe, false);
                return;
            }
            if (_drawn <= 0f)
            {
                if (Time.time - weapon.m_lastAttackTime < 0.6f) return;
                if (!weapon.m_shared.m_attack.StartDraw(me, weapon)) return; // (no arrows left: its near weapon takes over — NpcGear)
                _drawn = 0.001f;
            }
            _drawn += dt * NpcKnack.Of(Actor, "bow"); // (a good archer draws quicker)
            if (DrawTimeField != null) DrawTimeField.SetValue(me, _drawn);
            string state = weapon.m_shared.m_attack.m_drawAnimationState;
            if (Body != null && !string.IsNullOrEmpty(state)) { Body.AnimFlag(state, true); Body.AnimValue("drawpercent", me.GetAttackDrawPercentage()); } // (through its body: the game's own SetBool may leave its ZDO as it was)
            if (me.GetAttackDrawPercentage() >= 0.98f) { m_character.StartAttack(foe, false); DrawStop(me); }
        }

        private void DrawStop(Humanoid me)
        {
            if (_drawn <= 0f || me == null) return;
            _drawn = 0f;
            if (DrawTimeField != null) DrawTimeField.SetValue(me, 0f);
            ItemDrop.ItemData w = me.GetCurrentWeapon();
            if (w != null && Body != null) Body.AnimFlag(w.m_shared.m_attack.m_drawAnimationState, false);
        }

        // its health ran out (NpcVitals): whatever it did ends, a fight or a flight first
        internal void Downed()
        {
            DrawStop(m_character as Humanoid);
            NpcGear.AtEase(Actor, m_character as Humanoid); // (down: its weapons away — not held when it gets up)
            _plans[(int)Layer.Fight] = null; _hurtBy = null;
            Leave(_cur); _cur = null;
            if (Body != null && Body.ClipBusy) Body.Clip(""); // (it falls: no gesture left on it when it gets up)
            StopMoving();
        }

        // away from the foe: a resident towards its home when home is the other way, else straight away; safe (the foe
        // gone, or SafeRange off for 4 s): back to what it was doing; never longer than a minute
        private void Fleeing(float dt, Plan p)
        {
            GameObject go = Find(p.Who);
            Character foe = go != null ? go.GetComponent<Character>() : null;
            if (foe == null || foe.IsDead()) { EndFight(FoeFell); return; } // (fallen — a body killed is gone from the world at once, and one it runs from is never so far as to unload: those who stood beat it off, 2026-11-05)
            Vector3 me = transform.position, from = foe.transform.position;
            float d = Utils.DistanceXZ(me, from);
            if (d > SafeRange)
            {
                if (p.BestAt <= 0f) p.BestAt = Time.time;
                if (Time.time - p.BestAt > 4f) { EndFight("safe"); return; }
                StopMoving(); LookAt(foe.GetCenterPoint()); Status = Lang.K("озирається");
                return;
            }
            p.BestAt = 0f;
            if (_fight == Fight.Wary && (d < (Actor != null ? NpcPersona.Of(Actor).CornerAt : CornerRange) || (_hurtBy == foe && Time.time - _hurtAt < 1.5f)))
            {
                p.Kind = Task.Attack; p.Since = Time.time; p.Home = me; // (caught: it turns and fights)
                Status = Lang.K("відбивається"); Npcs.Log(Name() + ": cornered — fights back " + foe.m_name);
                return;
            }
            if (Time.time - p.Since > 60f) { EndFight("ran long enough"); return; }
            Vector3 away = me - from; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            Vector3 to = me + away.normalized * 8f;
            NpcActor a = Actor;
            Vector3 shelter;
            if (Shelter(me, from, out shelter)) { Move(dt, shelter, 2.5f, Pace.Run); Status = Lang.K("біжить до захисту"); return; }
            if (a != null && NpcResident.Is(a))
            {
                Vector3 home = NpcRules.Home(a);
                if (Utils.DistanceXZ(home, from) > d + 2f) to = home; // (home is the other way from the foe: run home)
            }
            Move(dt, to, 1f, Pace.Run);
            Status = Lang.K("тікає");
        }

        // someone to run to: a player or a fighter (not down) within 30 m, not on the foe's side of it; looked for once a second
        private float _shelterAt; private bool _shelterOk; private Vector3 _shelter;
        private bool Shelter(Vector3 me, Vector3 foe, out Vector3 at)
        {
            if (Time.time - _shelterAt < 1f) { at = _shelter; return _shelterOk; }
            _shelterAt = Time.time; _shelterOk = false;
            Vector3 toFoe = foe - me; toFoe.y = 0f; toFoe.Normalize();
            float best = 30f * 30f;
            foreach (Player pl in Player.GetAllPlayers()) if (pl != null && !pl.IsDead()) Consider(pl.transform.position, me, toFoe, ref best);
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Mind == null || o.Mind == this || (o.Vitals != null && o.Vitals.Down)) continue;
                Fight f = o.Mind.Style;
                if (f == Fight.Defend || f == Fight.Hunt) Consider(o.transform.position, me, toFoe, ref best);
            }
            at = _shelter;
            return _shelterOk;
        }

        private void Consider(Vector3 p, Vector3 me, Vector3 toFoe, ref float best)
        {
            Vector3 d = p - me; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq >= best || (sq > 4f && Vector3.Dot(d.normalized, toFoe) > 0.6f)) return; // (past the foe: not that way)
            best = sq; _shelter = p; _shelterOk = true;
        }

        private float FoughtAt = -1000f; private bool FoughtWon, FoughtRan; // (its last fight where it is run: read through FoughtOf)
        private const string FoeFell = "safe: its foe fell";
        internal static float FightMemo = 120f; // s: the same fight's end not remembered again
        internal static readonly int KFought = "RavenWatch_npcFought".GetStableHashCode(); // string «worldsec|w|l|r» (won, lost, ran): its last fight's end — in its locker, at most once in FoughtKeepEvery s
        internal static float FoughtKeepEvery = 30f;
        private float _foughtKept = -1000f;

        // its last fight on any game (2026-11-08: a talk on a second player's game read the mind's own fields, which stand still there, and «after a fight» never came):
        // seconds since it ended (MaxValue: none known), whether it ran, whether it won. Where it is run its own fields, else what its owner wrote
        internal static float FoughtOf(NpcActor a, out bool ran, out bool won)
        {
            ran = won = false;
            if (a == null) return float.MaxValue;
            if (a.Owned && a.Mind != null && a.Mind.FoughtAt > -999f) { ran = a.Mind.FoughtRan; won = a.Mind.FoughtWon; return Time.time - a.Mind.FoughtAt; }
            string s = a.Zdo != null ? NpcLocker.Text(a.Zdo, KFought) : "";
            int bar = s.IndexOf('|');
            double at;
            if (bar <= 0 || !double.TryParse(s.Substring(0, bar), NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return float.MaxValue;
            string how = s.Substring(bar + 1);
            ran = how == "r"; won = how == "w";
            return Mathf.Max(0f, (float)(NpcRescue.Now - at));
        }

        // eating now, on any game: its own plan where it is run, else the meal its owner wrote on its body (NpcEat.KEat: a seated meal)
        internal static bool EatingOf(NpcActor a)
        {
            if (a == null) return false;
            if (a.Owned) return a.Mind != null && a.Mind.Now == Task.Eat;
            return a.Zdo != null && a.Zdo.GetString(NpcEat.KEat, "").Length > 0;
        }

        private void EndFight(string why)
        {
            FoughtAt = Time.time; FoughtWon = why == "won"; FoughtRan = Runs;
            if (Actor != null && Actor.Zdo != null && Time.time - _foughtKept > FoughtKeepEvery) { _foughtKept = Time.time; NpcLocker.Put(Actor.Zdo, KFought, NpcRescue.Now.ToString("0", CultureInfo.InvariantCulture) + "|" + (FoughtWon ? "w" : FoughtRan ? "r" : "l")); } // (for the other games' talks: a raid's back and forth is not a locker sent each time)
            NpcGear.AtEase(Actor, m_character as Humanoid); // (its weapons back on its back — a guard keeps them in hand)
            Npcs.Log(Name() + ": fight over (" + why + ")");
            Set(Layer.Fight, null);
            _hurtBy = null;
            StoryBus.Raise("npc_fight_over", Name(), why == "won" ? 1 : 0, transform.position);
            // how it went: won — it beat the foe, or ran and the foe fell (its people beat it off); lost — it ran and the foe is still about;
            // one that only lost sight of its foe, nothing. Who minds it: FightOver (NpcModules)
            string kind = why == "won" || why == FoeFell ? "fight_won" : FoughtRan ? "fight_lost" : null;
            if (Actor != null && kind != null) foreach (Action<NpcActor, string> f in FightOver) f(Actor, kind);
            if (Actor != null) Actor.FireRule("fight_over");
        }

        // hit (even when it cannot be hurt: NpcDamagePatch tells it): a foe to answer, unless it is a player
        internal void HurtBy(Character attacker)
        {
            if (attacker == null || attacker == m_character || Now == Task.Travel) return; // (on its way: nothing reaches it)
            _hurtBy = attacker; _hurtAt = Time.time;
            if (!attacker.IsPlayer() && _fight != Fight.Off && _plans[(int)Layer.Fight] == null) StartFight(attacker); // (a fighter answers, a peaceful or wary one runs — a wary one turns on it once caught)
            if (!attacker.IsPlayer() && Actor != null) foreach (Action<NpcActor, Character> f in Hurt) f(Actor, attacker);
        }

        // ---------------------------------------------------------------- small things
        private void Arrived(Plan p)
        {
            StopMoving();
            if (_feet != null) _feet.There("ok");
            // a whim's walk to somewhere to do a thing there (wash: kneels at the water): its pose there, until the whim ends
            if ((_curLayer == Layer.Whim || _curLayer == Layer.Scene) && !string.IsNullOrEmpty(p.Pose)) { bool whim = _curLayer == Layer.Whim; p.Kind = Task.Pose; p.Started = false; if (whim && Actor != null && UnityEngine.Random.value < 0.4f && PoseReached != null) PoseReached(Actor); return; } // (a scene's walk there too: one in need hiding where it was shown — NpcRescue)
            p.Kind = Task.Idle; p.Started = false; // (it waits there, on the same layer, until let go)
            if (_curLayer == Layer.Order || _curLayer == Layer.Job || _curLayer == Layer.Routine) Save();
            Status = Lang.K("прийшов|прийшла");
            StoryBus.Raise("npc_arrived", Name(), 0, transform.position);
        }

        // the last safety (NpcWalk: it meant to walk a long while and got nowhere — an island, a nook nothing eases it out
        // of): home, by its heart, with the game's puff before and after where someone sees it; what it was after when it
        // stuck is let go (a whim ended, its errand afresh) so it does not walk into the same trap — then its day goes on
        internal void HomeFromStuck(string why, bool inThing = false)
        {
            NpcActor a = Actor;
            if (a == null || _curLayer == Layer.Scene) return; // (a scene holds it: the story says where it goes)
            if (!inThing && _cur != null && _cur.Kind == Task.Follow && _curLayer != Layer.Whim) { FollowWaits(_cur, why); return; } // (after the one it follows and no way to them: it waits, not home and back again — a walk with you, a whim, is let go below)
            if (!NpcResident.Is(a)) return;
            if (_curLayer == Layer.Fight && _cur != null && _cur.Kind == Task.Attack) return; // (pushing at a wall after a foe it cannot get to: its fight ends by its own clock — NoReach — not by a puff home from the middle of it)
            Vector3 home = NpcKit.Floor(NpcRules.Home(a)) + Vector3.up * 0.1f;
            Vector3 v;
            if (Pathfinding.instance != null && NpcKit.SameFloor(out v, home, 4f, NpcKit.Walker)) home = v + Vector3.up * 0.1f;
            // the last safety (NpcWalk: it got nowhere Home s unseen, HomeSeen s seen): home — seen, it goes and comes in a
            // puff of smoke (the user 2026-11-02: a body held in a wall or a thing is never left there)
            if (Body == null) return;
            bool seen = Body.Put(home, "додому (застряг надовго)");
            if (_feet != null) _feet.Forget(); // (its old way led into the trap: asked afresh from home, whatever called this)
            GaveUp(why);
            Npcs.Log(Name() + ": " + why + " — home to its heart" + (seen ? " (seen: in a puff)" : ""));
            StoryBus.Raise("npc_home_from_stuck", Name(), seen ? 1 : 0, home);
        }

        // Its feet gave up on where its plan goes (got nowhere for good: HomeFromStuck). What becomes of the plan is decided here, once, by its
        // layer (2026-11-07: an order to a spot no way leads to took it home, the order stood, it walked back into the same trap and was taken
        // home again, for good):
        //   Whim     let go: its day goes on
        //   Job      its errand afresh (NpcWorker picks again)
        //   Order    to a place (go, sit, sleep, wander, a pose there): let go, said in the log. A follow is kept and never comes here for getting
        //            nowhere (it waits where it is: FollowWaits; only a body held inside a thing is taken home), a long way too (NpcTravel steps it on by itself). A standing word of the story (StoryHold) gives it again:
        //            then its place is the trouble, and the log says so each time
        //   Routine  nothing: its day picks anew by the hour · Scene: never here (the story says where it goes)
        private void GaveUp(string why)
        {
            Plan p = _cur;
            if (_curLayer == Layer.Whim) { if (p != null) Lost(p.To); DropWhim(); }
            else if (_curLayer == Layer.Job && _workerL != null) _workerL.Reset();
            else if (_curLayer == Layer.Order && p != null && p.Kind != Task.Follow && p.Kind != Task.Travel)
            {
                Npcs.Log(Name() + ": its order (" + p.Kind + ", «" + p.Whose + "») leads where it cannot get — let go: " + why);
                Release(Layer.Order, p.Whose);
            }
            if (_cur != null) { _cur.Best = float.MaxValue; _cur.BestAt = Time.time; }
        }

        // the safety (the user 2026-11-06: on a crooked base put it on a safe spot it can go on from, not let it dither before you): a spot by its
        // goal with room for a body, on the way finder's map (NpcWalk.SafeBy) — seen, it goes and comes in a puff of smoke; no such spot: it
        // makes do where it is (a story's told «tp» is always done). True: it was put there; false: it stays where it is (its caller makes do:
        // sits where it is, lets its whim go)
        // once a goal (2026-11-06, measured: a seat up on a floor no way leads to — put by it, still no way, put again 2 m off, again and again in
        // puffs): a goal it was put by within UnstickAgain s is given up instead (false, kept off a while: its caller takes another seat, lets
        // its whim go). A short step (under PuffFrom m) is no sight worth a puff
        // goOn (a goal of its own choosing — OwnGoal: its ease, its day, a whim): only a spot the map has a whole way to from where it stands
        // (NpcWalk.SafeBy) — put inside walls it could not walk out again, and every walk it began there failed (2026-11-11). An order, a
        // scene, its bed and its work put it by the goal as before: there it has to be
        internal static float UnstickAgain = 90f; // (PuffFrom: NpcBody, the one way a body is moved)
        private Vector3 _unstuckGoal = new Vector3(float.MaxValue, 0f, 0f);
        private float _unstuckAt = -999f;
        private bool OwnGoal { get { return _curLayer < Layer.Order; } }
        // put by that goal a moment ago: it did not walk there (NpcPlaces: no visit of its own to count, and the place is left alone a while)
        internal bool PutBy(Vector3 to) { return Time.time - _unstuckAt < 10f && Utils.DistanceXZ(to, _unstuckGoal) < 2f; }
        // a whim let go because its feet found no way to its place (not one that merely ran out, or gave way to a duty): whoever gave the whim
        // reads it a moment later (NpcPlaces: that place is left alone)
        private Vector3 _lostGoal = new Vector3(float.MaxValue, 0f, 0f);
        private float _lostAt = -999f;
        private void Lost(Vector3 to) { _lostGoal = to; _lostAt = Time.time; }
        internal bool GaveUpOn(Vector3 to) { return Time.time - _lostAt < 10f && Utils.DistanceXZ(to, _lostGoal) < 2f; }
        // a long way's own word for it (NpcTravel: stuck before a player's eyes) — the same safety, by the same rules
        internal bool StepTo(Vector3 to, string why) { return Unstick(to, why); }
        private bool Unstick(Vector3 to, string why, bool told = false, bool goOn = false)
        {
            if (!told && Utils.DistanceXZ(to, _unstuckGoal) < 2f && Time.time - _unstuckAt < UnstickAgain)
            {
                if (_cur != null) { _cur.Best = float.MaxValue; _cur.BestAt = Time.time; }
                NpcWalk.KeepOff(to);
                Npcs.Log(Name() + ": " + why + " — put by it once already, still no way: gives it up");
                StoryBus.Raise("npc_stuck", Name(), 1, transform.position);
                return false;
            }
            Vector3 goal = to, safe = to; // (the goal itself is what «once a goal» remembers: the safe spot lies up to SafeReach m off it)
            if (!told && !NpcWalk.SafeBy(to, out safe, goOn ? transform.position : (Vector3?)null))
            {
                if (_cur != null) { _cur.Best = float.MaxValue; _cur.BestAt = Time.time; }
                if (goOn) NpcWalk.KeepOff(to);
                Npcs.Log(Name() + ": " + why + " — no safe spot by it" + (goOn ? " it could walk on from" : "") + "; makes do where it is");
                StoryBus.Raise("npc_stuck", Name(), 1, transform.position);
                return false;
            }
            to = told ? NpcKit.Floor(to) + Vector3.up * 0.1f : safe; // (the floor there, not the roof over it)
            if (!told) { _unstuckGoal = goal; _unstuckAt = Time.time; }
            if (Body == null) return false;
            bool seen = Body.Put(to, told ? "перенесено (наказ tp)" : "перенесено (застряг)", !told);
            if (_feet != null) _feet.Stuck("put there" + (seen ? " (seen: in a puff)" : "") + ": " + why);
            if (_cur != null) { _cur.Best = float.MaxValue; _cur.BestAt = Time.time; }
            Npcs.Log(Name() + ": " + why + " — stepped to " + Mathf.RoundToInt(to.x) + "," + Mathf.RoundToInt(to.z));
            StoryBus.Raise("npc_stuck", Name(), 0, to);
            return true;
        }

        // ---------------------------------------------------------------- kept in its ZDO (Order, Job, Routine)
        private static readonly Layer[] Kept = { Layer.Routine, Layer.Job, Layer.Order }; // (a fight or a scene is never taken up again after a reload)

        private void Save()
        {
            ZNetView nv = Nv;
            if (nv == null || !nv.IsValid() || !nv.IsOwner() || !_loaded) return; // (its kept plans not read yet: none written over them)
            // by the layer's name, not its number: a layer added or moved later does not turn old saves into another one
            Fight kept = Fight.Defend; foreach (Layer l in Kept) if (_fightSet[(int)l]) kept = _fightBy[(int)l]; // (its own way of fighting: a scene's is not kept — Kept goes up from its day)
            StringBuilder sb = new StringBuilder("fight=").Append(kept.ToString().ToLowerInvariant());
            foreach (Layer l in Kept)
            {
                Plan p = _plans[(int)l];
                if (p != null) sb.Append(';').Append(l.ToString().ToLowerInvariant()).Append('=').Append(p.Code());
            }
            if (_wayLeft != null) sb.Append(";way=").Append(_wayLeft.Code());
            NpcLocker.Put(nv.GetZDO(), KPlans, sb.ToString());
        }

        // its kept plans read, once: they are in its locker (NpcLocker.Lives), so only when that is here — a new owner reading before it
        // came would start with nothing and save that over them. False: not yet (asked again at its next thought)
        private bool Loaded()
        {
            if (_loaded) return true;
            if (!NpcLocker.Ready(Actor)) return false;
            _loaded = true;
            Load();
            return true;
        }

        private void Load()
        {
            ZNetView nv = Nv;
            string s = nv != null && nv.IsValid() ? NpcLocker.Text(nv.GetZDO(), KPlans) : "";
            if (s.Length == 0) { Unkept(null); return; } // (nothing kept: no task's marks left on it either)
            foreach (string part in s.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq), val = part.Substring(eq + 1);
                if (key == "fight") { _kept = true; foreach (Fight f in Enum.GetValues(typeof(Fight))) if (f.ToString().ToLowerInvariant() == val) { _fightBy[(int)Layer.Routine] = f; _fightSet[(int)Layer.Routine] = true; _fightOwner[(int)Layer.Routine] = Owner(Layer.Routine); } continue; }
                if (key == "way") { Plan w = Plan.Parse(val); if (w != null && w.Kind == Task.Travel) _wayLeft = w; continue; }
                foreach (Layer l in Kept)
                {
                    if (l.ToString().ToLowerInvariant() != key) continue;
                    if (l == Layer.Routine && NpcResident.Is(Actor)) continue; // (a resident's day comes from the hour: picked anew — a spot kept from the last time is stale, the fire it found then is not the one now)
                    Plan p = Plan.Parse(val);
                    if (p != null && p.Kind == Task.Follow) { if (p.Leader == 0L) continue; p.Who = ZDOID.None; } // (a body's id means nothing after a reload: the player it followed is looked for by its number; one with no number: not kept)
                    if (p != null && _plans[(int)l] == null) _plans[(int)l] = p; // (one given before its plans could be read stands over the kept one)
                }
            }
            if (_plans[(int)Layer.Order] == null && _wayLeft != null) { _plans[(int)Layer.Order] = _wayLeft; _wayLeft = null; } // (the order over its way not kept — whom it followed: on its way again)
            Plan order = _plans[(int)Layer.Order];
            Unkept(order); // (a mark left from a task not kept)
            Npcs.Log(Name() + ": went on with what it was doing (" + s + ")");
        }

        // ---------------------------------------------------------------- orders
        internal void Do(string order) { Do(order, Layer.Order); }

        // its template's way of fighting, when it has none kept of its own (a fresh NPC; not one reloaded)
        internal bool Default(string fight)
        {
            if (!Loaded()) return false; // (its kept way of fighting not read yet: asked again)
            if (!_kept) Do("fight " + fight, Layer.Routine);
            return true;
        }

        internal void Do(string order, Layer layer) { Do(order, layer, null); }

        private static readonly HashSet<string> PlanWords = new HashSet<string> { "go", "follow", "wander", "sit", "sleep", "stand", "stay", "free", "travel" };
        internal void Do(string order, Layer layer, string whose)
        {
            if (string.IsNullOrEmpty(order) || NotHere(order)) return;
            string[] w = order.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            Loaded(); // (an order before its first thought: what it kept first, the order over it)
            if (string.IsNullOrEmpty(whose)) whose = Owner(layer);
            bool plan = PlanWords.Contains(w[0]) || (w[0] == "emote" && Array.IndexOf(w, "loop") > 1);
            if (plan && !May(layer, whose)) { Npcs.Log(Name() + ": [" + layer + "] «" + whose + "» may not take it from «" + _plans[(int)layer].Whose + "»: " + order); return; }
            _doWhose = whose;
            try { Do0(w, order, layer); } finally { _doWhose = null; }
        }

        private void Do0(string[] w, string order, Layer layer)
        {
            Vector3 v;
            switch (w[0])
            {
                case "go":
                    if (!Vec(w, 1, out v)) return;
                    int ni = Array.IndexOf(w, "near");
                    Set(layer, new Plan { Kind = Task.Go, To = v, Run = Array.IndexOf(w, "run") > 0, Ease = Array.IndexOf(w, "ease") > 0, Hurry = Array.IndexOf(w, "hurry") > 0, Exact = Array.IndexOf(w, "exact") > 0, Near = ni > 0 && w.Length > ni + 1 ? Mathf.Clamp(Errands.F(w[ni + 1], 1.2f), Array.IndexOf(w, "exact") > 0 ? 0.1f : 0.3f, 10f) : 1.2f });
                    break;
                case "follow":
                    ZDOID who = Id(w, 1); if (who.IsNone()) return;
                    GameObject led = Find(who); Player leader = led != null ? led.GetComponent<Player>() : null;
                    Set(layer, new Plan { Kind = Task.Follow, Who = who, Leader = leader != null ? leader.GetPlayerID() : 0L, Keep = w.Length > 2 ? Mathf.Clamp(Errands.F(w[2], 3f), 1.5f, 20f) : 3f });
                    break;
                case "wander":
                    Set(layer, new Plan { Kind = Task.Wander, Radius = w.Length > 1 ? Mathf.Clamp(Errands.F(w[1], 6f), 2f, 40f) : 6f, Home = Vec(w, 2, out v) ? v : transform.position });
                    break;
                case "sit":
                    Set(layer, new Plan { Kind = Task.Sit, To = Vec(w, 1, out v) ? v : transform.position });
                    break;
                case "sleep":
                    ZDOID bedId = Id(w, 1); if (bedId.IsNone()) return;
                    Set(layer, new Plan { Kind = Task.Sleep, Who = bedId });
                    break;
                case "stand": case "stay":
                    Set(layer, new Plan { Kind = Task.Idle, To = transform.position });
                    if (layer == Layer.Order) StayFrom(); // (a player's «Стій»: with its term)
                    break;
                case "free": Release(layer, _doWhose); break; // (its plan, its look and way of fighting on this layer, a clip a scene left looping)
                case "travel": // travel x y z heart|hall [user:id]
                    if (!Vec(w, 1, out v)) return;
                    Set(layer, new Plan { Kind = Task.Travel, To = v, Pose = w.Length > 4 ? w[4] : "hall", Who = Id(w, 5) }); // (where to: its module's word — NpcTravel)
                    break;
                case "clip": // clip <name>|none [seconds] — one of our clips on its body (NpcBody.Clip), over what it does
                    if (w.Length < 2 || Body == null) return;
                    if (layer == Layer.Routine && w[1] != "none") { w[1] = NpcMoves.Clip(Actor, w[1]); if (w[1].Length == 0) return; } // (a habit of its own life — its raven stroked, a laugh by the fire: through its gate, NpcMoves — its body's own of it; not now)
                    if (!Body.Clip(w[1], w.Length > 2 ? Errands.F(w[2], 0f) : 0f)) Npcs.Log(Name() + ": no clip «" + w[1] + "»");
                    break;
                case "emote":
                    if (w.Length < 2) return;
                    if (Array.IndexOf(w, "loop") > 1) { Set(layer, new Plan { Kind = Task.Pose, Pose = w[1], To = transform.position }); break; }
                    // a one-off gesture is not a plan: it plays over what it does (not on a seat, not mid-fight)
                    bool sat = _cur != null && _cur.Kind == Task.Sit && _cur.Seated;
                    if (!sat && Now != Task.Attack && Body != null) Body.Gesture(w[1] == "none" ? "" : w[1], false);
                    break;
                case "attack":
                    GameObject go = Find(Id(w, 1));
                    Character c = go != null ? go.GetComponent<Character>() : null;
                    if (c != null && !c.IsDead()) StartFight(c);
                    break;
                case "look": { if (layer == Layer.Scene) _hearTill = 0f; int i = (int)layer; _lookWho[i] = w.Length > 1 && w[1] == "none" ? ZDOID.None : Id(w, 1); _lookPtOn[i] = false; _lookOwner[i] = _doWhose; _lookTill[i] = Array.IndexOf(w, "keep") > 1 ? float.MaxValue : Time.time + LookFor; break; } // (a look at someone is a while, not for good: «keep» — a scene's; on its layer)
                case "lookat": if (Vec(w, 1, out v)) { int i = (int)layer; _lookPt[i] = v; _lookPtOn[i] = true; _lookWho[i] = ZDOID.None; _lookOwner[i] = _doWhose; if (layer == Layer.Scene) _hearTill = 0f; } break;
                case "hear": Hear(w); break;
                case "tp": if (Vec(w, 1, out v)) Unstick(v, "told to step", true); break;
                case "fight":
                    string fw = w.Length > 1 ? w[1] : "flee";
                    _fightBy[(int)layer] = fw == "off" ? Fight.Off : fw == "hunt" ? Fight.Hunt : fw == "defend" ? Fight.Defend : fw == "wary" ? Fight.Wary : Fight.Flee; _fightOwner[(int)layer] = _doWhose; _fightSet[(int)layer] = true;
                    if (_fight == Fight.Off && _plans[(int)Layer.Fight] != null) EndFight("told to stop");
                    Save();
                    break;
                default: Npcs.Log(Name() + ": unknown order «" + order + "»"); return;
            }
            Npcs.Log(Name() + ": [" + layer + "] " + order);
        }

        // a free bench or chair (not on a ship: those move) near the spot
        // the nearest free seat round a spot: every seat of every piece there (a chair, a stool, a bench's places, a throne —
        // a seat is often a part of its piece apart from what you hit, so the pieces are asked, not what is hit)
        private static readonly List<Piece> SeatPieces = new List<Piece>();
        // a seat of any kind there, taken or not (its own, while it sits on it: NpcToil)
        internal static bool SeatNear(Vector3 at, float range)
        {
            NpcBase b = NpcBase.Covering(at);
            if (b != null) { foreach (Component c in b.Round(typeof(Chair))) if (c != null && (c.transform.position - at).sqrMagnitude < range * range) return true; return false; }
            SeatPieces.Clear();
            Piece.GetAllPiecesInRadius(at, range, SeatPieces);
            foreach (Piece pc in SeatPieces) if (pc != null && Seatable(pc.GetComponentInChildren<Chair>())) return true; // (a toppled bench is no seat)
            return false;
        }


        // a meal's seat round its dining table (NpcMeals asks before it sets off: one it can walk to)
        internal static Chair MealSeat(Vector3 table) { return NearestChair(table, MealSeats, true); }

        // a seat in the same room as the table: nothing tall (a wall, a door) between them at chest height — the table,
        // the seat and the furniture round it do not count. Not the way finder's paths: it maps the land a bit at a time,
        // only where someone walked lately, and answers «no way» to a place it has not mapped yet (2026-10-31)
        private static readonly int Walls = LayerMask.GetMask("piece", "Default", "static_solid");
        private static readonly RaycastHit[] WallHits = new RaycastHit[16];
        private static bool SameRoom(Vector3 table, Vector3 seat, Chair ch)
        {
            Vector3 a = table + Vector3.up * 1.2f, b = seat + Vector3.up * 0.9f;
            Vector3 dir = b - a; float len = dir.magnitude;
            if (len < 0.1f) return true;
            int n = Physics.RaycastNonAlloc(a, dir / len, WallHits, len, Walls, QueryTriggerInteraction.Ignore);
            Piece own = ch.GetComponentInParent<Piece>();
            for (int i = 0; i < n; i++)
            {
                Collider c = WallHits[i].collider;
                if (c == null || (own != null && c.GetComponentInParent<Piece>() == own)) continue;
                if (c.bounds.size.y >= 1.6f) return false; // (tall: a wall, a door, a pillar — furniture is lower)
            }
            return true;
        }

        private static readonly Collider[] Near = new Collider[16];
        private static bool Crowded(Vector3 p, Piece own)
        {
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.3f, Near, Walls, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Near[i] != null && (own == null || Near[i].GetComponentInParent<Piece>() != own)) return true;
            return false;
        }

        // the spot by a seat to walk to: before it, beside it or behind it — the free one nearest «from» (a bench against
        // a table is sat on from its open side; a chair in the middle of a room from wherever it comes)
        private static bool SideOf(Chair ch, Vector3 from, out Vector3 side)
        {
            Transform pt = ch.m_attachPoint != null ? ch.m_attachPoint : ch.transform;
            Vector3 at = pt.position, fwd = Vector3.ProjectOnPlane(pt.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            side = at; bool any = false; float best = float.MaxValue;
            Piece own = ch.GetComponentInParent<Piece>();
            foreach (float ang in new[] { 0f, 90f, -90f, 180f })
            {
                Vector3 p = NpcKit.Floor(at + Quaternion.Euler(0f, ang, 0f) * fwd * 0.75f);
                if (Mathf.Abs(p.y - at.y) > 1.2f) continue; // (off a ledge, up a wall)
                if (Crowded(p, own)) continue; // (a table, a wall there: no room to stand — the seat itself not counted)
                float d = (ang == 0f ? 0f : 0.4f) + Utils.DistanceXZ(p, from); // (before it a little rather)
                if (d < best) { best = d; side = p; any = true; }
            }
            return any;
        }
        internal static Vector3 SeatPoint(Chair ch) { return ch.m_attachPoint != null ? ch.m_attachPoint.position : ch.transform.position; }

        private static readonly int PerfSeat = NpcPerf.Id("seat search (пошук сидіння)");
        private static Chair NearestChair(Vector3 at, float range, bool onFoot, NpcMind me = null, bool lean = false) // (lean: a fresh pick — SeatLean; never the seat it is on, looked for after a reload)
        {
            long perf = NpcPerf.Now();
            try { return NearestChair0(at, range, onFoot, me, lean); } finally { NpcPerf.Add(PerfSeat, perf); }
        }

        private static Chair NearestChair0(Vector3 at, float range, bool onFoot, NpcMind me, bool lean)
        {
            List<KeyValuePair<float, Chair>> free = new List<KeyValuePair<float, Chair>>();
            NpcBase b = NpcBase.Covering(at); // (a base's own look round it: its seats, no going over every piece near)
            if (b != null) foreach (Component c in b.Round(typeof(Chair))) Free(c as Chair, at, range, me, free, lean);
            else
            {
                SeatPieces.Clear();
                Piece.GetAllPiecesInRadius(at, range, SeatPieces); // heavy: only where no base looks round (a camp, the wild)
                foreach (Piece pc in SeatPieces) if (pc != null) foreach (Chair ch in pc.GetComponentsInChildren<Chair>()) Free(ch, at, range, me, free, lean);
            }
            free.Sort((x, y) => x.Key.CompareTo(y.Key));
            foreach (KeyValuePair<float, Chair> f in free)
            {
                if (!onFoot) return f.Value;
                Transform pt = f.Value.m_attachPoint != null ? f.Value.m_attachPoint : f.Value.transform;
                if (SameRoom(at, pt.position, f.Value)) return f.Value; // (round the table itself: surely its own — a way from a table's middle is not found, it stands in it; further: near on foot, the next house not)
            }
            return null;
        }

        // a seat that leans more than this is no seat (cos of the angle off upright): a toppled bench of a ruin, a throne fallen askew (the user 2026-11-16:
        // the Hall's seats differ by its state — in the abandoned one a bench lies on its side, and one would sit on it sideways); none other near: the ground
        internal static float SeatUpright = 0.9f;
        // a seat one may sit on at all: not a ship's, its piece upright (every seat picker asks this: Free, SeatNear, NpcBase's look round)
        internal static bool Seatable(Chair ch) { if (ch == null || ch.m_inShip) return false; ZNetView nv = ch.GetComponentInParent<ZNetView>(); return Upright(nv != null ? nv.transform : ch.transform); }
        private static bool Upright(Transform piece) { return Vector3.Dot(piece.up, Vector3.up) >= SeatUpright; }
        private static void Free(Chair ch, Vector3 at, float range, NpcMind me, List<KeyValuePair<float, Chair>> free, bool lean)
        {
            if (ch == null || ch.m_inShip) return;
            Transform pt = ch.m_attachPoint != null ? ch.m_attachPoint : ch.transform;
            float d = Vector3.Distance(pt.position, at);
            if (d >= range || Taken(pt, me) || NpcWalk.Avoided(pt.position) || NpcWalk.Avoided(ch.transform.position)) return;
            ZNetView piece = ch.GetComponentInParent<ZNetView>();
            if (LocState.Absent(piece) || !Upright(piece != null ? piece.transform : ch.transform)) return; // (a bench its place's state has not: no sitting on air · the piece itself askew: its seat is no seat)
            free.Add(new KeyValuePair<float, Chair>(d + (lean && me != null && me.Actor != null && SeatLean != null ? SeatLean(me.Actor, pt.position) : 0f), ch));
        }

        // its body was taken down off furniture (NpcBody.OffFurniture): a sit it was on its way to, or sat on the ground up there for want of a
        // way to its seat, begins anew from the floor — its seat looked for again, its ground spot reckoned again (the way it had was from the top)
        internal void OffFurniture()
        {
            Plan p = _cur;
            if (p == null || p.Kind != Task.Sit || (p.Seated && p.OnPoint)) return; // (a meal's own steps are NpcMeals': its sitting goes through here again on its own)
            p.Seated = false; p.Started = false; p.Seat = null; p.HasSide = false; p.Best = float.MaxValue; p.BestAt = Time.time;
        }

        // a spot of bare ground near this one: nothing built round it (a bed, a table, a wall), not the top of a thing of the house (its own look
        // let the top of the Hall's workbench by: nothing built round a body up there — 2026-11-20), else the spot. The one way a body's spot is
        // found by a spot: NpcKit.FloorBeside (the floor it is on — a paved yard, a deck — not the land under it, nor a workbench's top)
        private static Vector3 ClearGround(Vector3 at)
        {
            int built = LayerMask.GetMask("piece", "piece_nonsolid");
            Vector3 v;
            return NpcKit.FloorBeside(out v, at, 3.1f, p => !Busy(p, built)) ? v : at;
        }

        // sat on the ground by a fire: facing it, not its back to it
        private void FaceFire()
        {
            Fireplace best = null; float bd = 4f;
            NpcBase b = NpcBase.Covering(transform.position); // (a base's own list: no look over everything within reach)
            IEnumerable<Component> near = b != null ? (IEnumerable<Component>)b.Round(typeof(Fireplace)) : NearFires(); // heavy: only where no base looks round (a camp, the wild), as it sits down
            foreach (Component c in near)
            {
                Fireplace f = c as Fireplace;
                if (f == null || !f.IsBurning() || !NpcKit.Warms(f)) continue;
                float d = Utils.DistanceXZ(f.transform.position, transform.position);
                if (d < bd) { bd = d; best = f; }
            }
            if (best == null) return;
            Vector3 d2 = best.transform.position - transform.position; d2.y = 0f;
            if (d2.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.LookRotation(d2.normalized); // (turned at once as it sits down: a turn by turns needs it to stand)
            Rigidbody rb = GetComponent<Rigidbody>(); if (rb != null) rb.rotation = transform.rotation;
        }

        private IEnumerable<Component> NearFires() { foreach (Collider c in Physics.OverlapSphere(transform.position, 4f, NpcKit.Built)) { Fireplace f = c.GetComponentInParent<Fireplace>(); if (f != null) yield return f; } }

        // something built in the way of sitting here — a fire is not (one sits by it), the floor under it neither
        private static readonly Collider[] Round = new Collider[16];
        private static bool Busy(Vector3 p, int mask)
        {
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.35f, p + Vector3.up * 1.2f, 0.4f, Round, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Round[i] != null && Round[i].GetComponentInParent<Fireplace>() == null) return true;
            return false;
        }

        // taken: someone sits there or goes to sit there — or next to it, when the two would not fit side by side (a heavy
        // or a tall one takes more of a bench: two of them on neighbouring seats sat in each other, the user 2026-11-03)
        private static bool Taken(Transform seat, NpcMind me)
        {
            float mine = me != null && me.Body != null ? me.Body.Half : NpcBody.NormalHalf;
            foreach (NpcActor o in NpcActor.Live) // (on its way to it: taken too — two went for one seat, the second put on the ground at last)
            {
                if (o == null || o.Mind == null || o.Mind == me) continue;
                Plan q = o.Mind.Current;
                if (q != null && q.Seat != null && Beside(seat, q.Seat.m_attachPoint != null ? q.Seat.m_attachPoint : q.Seat.transform, mine, o.Body != null ? o.Body.Half : NpcBody.NormalHalf)) return true;
            }
            NpcBody own = me != null ? me.Body : null; // (not in its own way: rising off one seat for the next)
            foreach (KeyValuePair<Character, NpcBody> kv in NpcBody.Held) if (kv.Value != null && kv.Value != own && kv.Value.Point != null && Beside(seat, kv.Value.Point, mine, kv.Value.Half)) return true;
            foreach (Player p in Player.GetAllPlayers()) if (p != null && p.IsAttached() && (Vector3.Distance(p.transform.position, seat.position) < 0.3f || Beside(seat, p.transform, mine, NpcBody.NormalHalf))) return true;
            return false;
        }

        // the same seat, or one beside it with no room for both (only when one of them is wider than a person of size 1:
        // two such keep to the seats as the game made them)
        private static bool Beside(Transform seat, Transform other, float mine, float theirs)
        {
            if (other == seat) return true;
            float both = mine + theirs;
            if (both <= 2f * NpcBody.NormalHalf + 0.01f) return false;
            Vector3 d = other.position - seat.position;
            return Mathf.Abs(d.y) < 0.6f && new Vector2(d.x, d.z).magnitude < both;
        }

        private static bool Vec(string[] w, int at, out Vector3 v)
        {
            v = Vector3.zero;
            if (w.Length < at + 3) return false;
            float x, y, z;
            if (!float.TryParse(w[at], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return false;
            if (!float.TryParse(w[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return false;
            if (!float.TryParse(w[at + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }

        private static ZDOID Id(string[] w, int at) { return w.Length > at ? NpcKit.Resolve(w[at]) : ZDOID.None; } // ("user:id")

        private static GameObject Find(ZDOID id) { return NpcKit.Find(id); }

        private static Vector3 Eye(GameObject go)
        {
            Character c = go.GetComponent<Character>();
            return c != null ? c.GetEyePoint() : go.transform.position + Vector3.up * 1.6f;
        }

        internal string Name() { NpcActor a = Actor; return a != null ? (a.Tag.Length > 0 ? a.Tag : a.Id) : name; }

        // for the log, the window and the studio: «Order: іде слідом»
        internal string Describe() { return (_cur != null ? LayerWord(_curLayer) + ": " : "") + Status; }

        // what a player reads: Describe without its layer («сцена: стоїть» → «стоїть»)
        internal static string WithoutLayer(string said)
        {
            int colon = said.IndexOf(": ", StringComparison.Ordinal);
            if (colon <= 0) return said;
            string head = said.Substring(0, colon);
            for (Layer l = Layer.Leisure; l <= Layer.Scene; l++) if (LayerWord(l) == head) return said.Substring(colon + 2);
            return said;
        }

        // its layer now on any game: its own mind where it is run, else the word its owner last wrote (NpcActor.StatusNow, at most 3 s old)
        internal static Layer LayerNow(NpcActor a)
        {
            if (a == null) return Layer.Leisure;
            if (a.Owned && a.Mind != null) return a.Mind.NowLayer;
            string said = a.StatusNow ?? "";
            int colon = said.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0) { string head = said.Substring(0, colon); for (Layer l = Layer.Leisure; l <= Layer.Scene; l++) if (LayerWord(l) == head) return l; }
            return Layer.Leisure;
        }

        // its order going on, on any game (the menu's ring: which button is lit, what the middle lets go) — its own plan where it is run, elsewhere
        // as kept (KPlans, «order=…»: Save writes it at every change of that layer, a «go» that came as idle too). null: none. Its plans on a game
        // that does not run it are always empty: the ring read them there, lit nothing and gave the order anew instead of letting it go (read 2026-11-12)
        private static string _orderSaid; private static Task? _orderKind; // session: the kept plans last read here, parsed once (an open menu asks each frame)
        internal static Task? OrderOf(NpcActor a)
        {
            if (a == null) return null;
            if (a.Owned) { Plan own = a.Mind != null ? a.Mind.Of(Layer.Order) : null; return own != null ? (Task?)own.Kind : null; }
            string s = NpcLocker.Text(a.Zdo, KPlans);
            if (!ReferenceEquals(s, _orderSaid))
            {
                _orderSaid = s; _orderKind = null;
                foreach (string part in s.Split(';'))
                {
                    if (!part.StartsWith("order=", StringComparison.Ordinal)) continue;
                    Plan p = Plan.Parse(part.Substring(6));
                    if (p != null) _orderKind = p.Kind;
                }
            }
            return _orderKind;
        }

        internal static string LayerWord(Layer l)
        {
            switch (l)
            {
                case Layer.Scene: return Lang.K("сцена");
                case Layer.Order: return Lang.K("наказ");
                case Layer.Fight: return Lang.K("бій");
                case Layer.Job: return Lang.K("робота");
                case Layer.Whim: return Lang.K("примха");
                case Layer.Routine: return Lang.K("розпорядок");
                default: return Lang.K("дозвілля");
            }
        }
    }
}
#endif

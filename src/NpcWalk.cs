#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet (npc-plan.md, «хода без ривків»): its own way follower, the one way it walks. Its parts:
    //   here              the walk: the way asked, by all together at most AsksAFrame a frame (again in a third of a second when it fails: the way finder's map is built
    //                     a piece at a time), a point a little ahead followed, others near pushing it gently aside, the
    //                     last steps straight at its goal, «there» within reach of it
    //   NpcWalkRoom       the way kept off corners, walls, things, drops and steep banks; no corner cut; gaps in the map bridged
    //   NpcWalkDoors      a shut door on its way opened, gone through, shut behind
    //   NpcWalkStuck      the one ladder when it gets nowhere: asked afresh → a step round (or it waits behind someone) → eased
    //                     out → a body in a thing freed → «no way there» to its caller → home; before a player's eyes only
    //                     in a puff of smoke, and last
    //   NpcWalkBox        what it did, measured and written (raven npc walk report; the black box where the studio is)
    //   NpcMind.Steer     where it turns its body: an arc, never a halt on a bend
    // Where nobody sees it (NpcClock.Slow) its way is asked and its eyes ahead look less often: a bump there costs nothing, and its ladder still holds.
    // Tuned while the game runs: raven npc walk set <key> <value> (Set below).
    internal sealed partial class NpcWalk
    {
        // ---------------------------------------------------------------- tuning (live: raven npc walk set …)
        internal static float Ahead = 0.9f, AheadRun = 1.6f; // how far ahead on its way it aims (a walk, a run)
        internal static float Margin = 0.7f, Steep = 30f;  // its way's corners moved this far off what they wrap, not onto ground steeper than this
        internal static float Berth = 0.9f, Dense = 1.5f, ShapeFar = 15f; // every point of its way (one each Dense m, ShapeFar m ahead) kept Berth m off walls, things, drops and steep slopes
        internal static float See = 3f, Wide = 1f, Avoid = 1.3f; // someone in its way this far ahead and this near its line: round them early, this hard
        internal static float Corner = 0.55f;            // a corner counts passed this near
        internal static float Retry = 0.3f;              // a failed ask asked again after
        internal static float Repath = 2.5f;             // a way asked afresh while walking (the map grows, things move)
        internal static float Reach = 1.6f;              // «there» when its way ends this much past its own «near»
        internal static float Final = 2.5f;              // the last steps straight at its goal from this near
        internal static float Room = 0.9f, Push = 1f;    // others nearer than this push it aside, this hard
        internal static float NoGain = 1.5f;             // no gain this long: its way asked afresh; twice that: a step round
        internal static float Seen = 40f;                // a player nearer than this sees it: never put anywhere by a jump
        internal static float Home = 30f, HomeSeen = 45f; // got nowhere (2 m) this long meaning to walk: home to its heart (unseen / seen)

        internal static string Set(string key, string value)
        {
            float v;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return "число?";
            switch (key)
            {
                case "ahead": Ahead = v; break;
                case "aheadrun": AheadRun = v; break;
                case "corner": Corner = v; break;
                case "retry": Retry = v; break;
                case "repath": Repath = v; break;
                case "reach": Reach = v; break;
                case "final": Final = v; break;
                case "room": Room = v; break;
                case "push": Push = v; break;
                case "nogain": NoGain = v; break;
                case "margin": Margin = v; break;
                case "clear": Berth = v; break;
                case "dense": Dense = Mathf.Max(0.5f, v); break;
                case "shapefar": ShapeFar = v; break;
                case "steep": Steep = v; break;
                case "see": See = v; break;
                case "wide": Wide = v; break;
                case "avoid": Avoid = v; break;
                case "look": Look = v; break;
                case "gap": Gap = v; break;
                case "gapdown": GapDown = v; break;
                case "gapup": GapUp = v; break;
                case "seen": Seen = v; break;
                case "home": Home = v; break;
                case "homeseen": HomeSeen = v; break;
                case "stuck": NpcMind.StuckSeconds = v; break;
                case "turn": NpcMind.TurnWalk = v; break;
                case "turnrun": NpcMind.TurnRun = v; break;
                case "slowfrom": NpcMind.SlowFrom = v; break;
                case "slowto": NpcMind.SlowTo = v; break;
                case "slowmin": NpcMind.SlowMin = v; break;
                default: return "ключі: ahead aheadrun corner retry repath reach final room push nogain seen stuck gap gapdown gapup margin steep see wide avoid look home homeseen turn turnrun slowfrom slowto slowmin clear dense shapefar";
            }
            return Get();
        }

        internal static string Get()
        {
            return string.Format(CultureInfo.InvariantCulture, "ahead {0} aheadrun {1} corner {2} retry {3} repath {4} reach {5} final {6} room {7} push {8} nogain {9} seen {10} stuck {11} margin {12} steep {13} see {14} wide {15} avoid {16} look {17} gap {18} gapdown {19} gapup {20} home {21} homeseen {22} · turn {23} turnrun {24} slowfrom {25} slowto {26} slowmin {27} · clear {28} dense {29} shapefar {30}",
                Ahead, AheadRun, Corner, Retry, Repath, Reach, Final, Room, Push, NoGain, Seen, NpcMind.StuckSeconds, Margin, Steep, See, Wide, Avoid, Look, Gap, GapDown, GapUp, Home, HomeSeen, NpcMind.TurnWalk, NpcMind.TurnRun, NpcMind.SlowFrom, NpcMind.SlowTo, NpcMind.SlowMin, Berth, Dense, ShapeFar);
        }

        // a player near enough to see a jump (any player: another game's camera is not known here)
        internal static bool Watched(Vector3 at)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Vector3.Distance(p.transform.position, at) < Seen) return true;
            return false;
        }

        // ---------------------------------------------------------------- one person's feet
        private readonly NpcMind _m;
        internal NpcWalk(NpcMind m) { _m = m; }

        private Vector3 _goal = new Vector3(float.MaxValue, 0f, 0f);
        private float _near;
        private string _what = "";
        private readonly List<Vector3> _path = new List<Vector3>(), _ask = new List<Vector3>();
        private bool _hasPath, _partial;
        private int _next;
        private float _askAt, _gainAt, _bestLeft, _stepUntil;
        private Vector3 _stepDir, _pushFrom;
        private float _pushAt, _lastGo, _wedgeNext;

        // someone (a person, a player) right in front: what holds it is them — it waits for them, no slide past
        private static bool PeopleAhead(Vector3 me, Vector3 dir)
        {
            dir.y = 0f; dir.Normalize(); // (a goal far off gave a long line: anyone in the half circle ahead counted, and it never stepped round)
            foreach (NpcActor o in NpcActor.Live) if (o != null && Near(me, dir, o.transform.position)) return true;
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Near(me, dir, p.transform.position)) return true;
            return false;
        }
        private static bool Near(Vector3 me, Vector3 dir, Vector3 at) { Vector3 d = at - me; if (Mathf.Abs(d.y) > 1.5f) return false; d.y = 0f; float m = d.magnitude; return m > 0.05f && m < 1.3f && Vector3.Dot(d / m, dir) > 0.3f; }
        internal bool Done;   // at its goal (within reach)

        // No way there: its way ends short of its goal ask after ask (a floor the way finder keeps apart, a seat behind a
        // table) for ShortGiveUp s — its caller lets the goal go (a whim ends, another seat is taken), and the spot is kept
        // AvoidFor s so that nobody picks it again meanwhile (the user 2026-11-02: Knud stood half a minute at a slope)
        internal static float ShortGiveUp = 8f, AvoidFor = 300f, ShortGain = 0.3f;
        internal bool Unreachable;
        // its way does not get there (no way after asks, or a way that ends short): a door may be what stands between —
        // the one thing that makes its mind look for one (NpcMind.ThroughDoor; it asked the whole way itself every second)
        internal bool NeedsDoor(Vector3 to) { return (to - _goal).sqrMagnitude < 9f && ((!_hasPath && _fails >= 3) || (_shortSince >= 0f && Time.time - _shortSince > 1f)); } // (it stands where its way ends short — not a way to a chest that ends by it, as every such way does)
        internal void Forgive() { Unreachable = false; _shortSince = -1f; } // (its caller has heard it: judged afresh after ShortGiveUp)
        internal bool CantReach(Vector3 to) { return Unreachable && (to - _goal).sqrMagnitude < 9f; } // (this goal: not one from before)
        private float _shortSince = -1f, _shortBest = float.MaxValue; // (_shortBest: how near it came since its way first ended short)
        private int _wayAsks;
        private static readonly List<KeyValuePair<Vector3, float>> _avoid = new List<KeyValuePair<Vector3, float>>();
        internal static void KeepOff(Vector3 at) { _avoid.RemoveAll(k => k.Value < Time.time); if (_avoid.Count < 200) _avoid.Add(new KeyValuePair<Vector3, float>(at, Time.time + AvoidFor)); }
        internal static bool Avoided(Vector3 at) { foreach (KeyValuePair<Vector3, float> k in _avoid) if (k.Value > Time.time && (k.Key - at).sqrMagnitude < 1.2f * 1.2f) return true; return false; }
        internal string Doing = "";

        // a walk towards a goal (every frame while it means to go there); true when there
        private float _slow = 1f; // (× its looks' intervals, this step)
        internal bool Step(float dt, Vector3 to, float near, bool run, string what)
        {
            Vector3 me = _m.transform.position;
            _slow = NpcClock.Slow(_m.Actor);
            if (Stranded(me)) return false;
            float moved = (to - _goal).sqrMagnitude;
            if (moved > 9f || what != _what) Begin(to, near, what); // (a new goal: a new walk)
            else if (moved > 0.25f) // (the same goal moving — a foe, the one it follows, a spot ahead as it flees: its way kept, asked afresh twice a second at most; its gain counted afresh — a goal moving with it is no «getting nowhere»)
            {
                _goal = to; _near = near; _bestLeft = float.MaxValue; _gainAt = Time.time; _thing = null; // (a moving goal is no thing to stand by)
                if ((to - _askedFor).sqrMagnitude > 1f && _askAt > Time.time + 0.5f) _askAt = Time.time + 0.5f;
            }
            float left = HowFar(me, to);
            _left = left;
            if (left <= _near) { Arrive("ok"); return true; }
            if (Done && left > _near + Reach + 0.5f) Done = false; // (its goal moved off — the one it follows walked on: not there any more)
            float now = Time.time;
            if (_m.Body != null && _m.Body.Sliding) { _m.StopMoving(); _pushFrom = me; _pushAt = now; _gainAt = now; if (_askAt < now + 0.1f) _askAt = now + 0.1f; return false; } // (its body on its way by NpcBody.Slide — a step up, eased out of a nook, up off a seat: its feet wait, it is no being stuck, and its way is asked from where the slide ends, not from half way)
            if (now >= _askAt && !Done && MayAsk()) Ask(me, to); // (there: no way wanted. None free this frame: the next one)

            // getting nowhere: its way afresh, then a step round
            float along = Left(me, to);
            if (along < _bestLeft - 0.3f) { _bestLeft = along; _gainAt = now; _reasked = false; _m.NewGoal(); } // (on along its way, round a house too: no «stuck» — that rule measured as the crow flies)
            if (now < _stepUntil) { Go(_stepDir, false, "steps round"); return false; }
            if (now - _gainAt > NoGain * 2f && _hasPath && _next < _path.Count && !PeopleAhead(me, to - me)) { _gainAt = now; _reasked = false; StepRound(me, to); return false; } // (on its way and held by a thing: round it — not while it waits for its way, nor behind someone)
            if (now - _gainAt > NoGain && !_reasked) { _reasked = true; _askAt = now; } // (its way asked again at once — once)

            if (!_hasPath) // (no way yet: the map is being built — it turns to its goal and steps on where nothing is in front)
            {
                Vector3 dir = to - me; dir.y = 0f;
                if (left < Final && ClearNow(me, to)) { Go(dir.normalized, false, "straight (no way yet)"); return false; }
                _m.StopMoving(); _m.Face(to); Halt("no way yet (" + _fails + " asks)", me, dir);
                return false;
            }
            while (_next < _path.Count && (Utils.DistanceXZ(me, _path[_next]) < Corner || Passed(me, _next))) _next++;
            if (_next >= _path.Count) // (its way ended)
            {
                Vector3 dir = to - me; dir.y = 0f;
                if (left < Final && ClearNow(me, to) && now - _gainAt < NoGain) { Go(dir.normalized, false, "last steps"); return false; } // (on up to its goal while it gains: its caller wants it nearer than its way ends — a seat from half a metre)
                if (left <= _near + Reach && ClearNow(me, to)) { Arrive(_partial ? "ok (by it)" : "ok"); return true; }
                if (_askAt > now + 1f) _askAt = now + 1f; // (ended short: asked again soon — once a second, a bridge looked for each time)
                if (_shortSince < 0f) _shortSince = now;
                else if (!Unreachable && now - _shortSince > ShortGiveUp && _wayAsks >= 2) { Unreachable = true; KeepOff(to); if (_trip != null) End("unreachable (its way ends short)"); } // (asked and asked, still short: no way there — its caller lets it go)
                _m.StopMoving(); _m.Face(to); Halt("its way ends " + left.ToString("0.0", CultureInfo.InvariantCulture) + " m short", me, dir);
                return false;
            }
            Vector3 aim = SafeCarrot(me, run ? AheadRun : Ahead, now);
            Vector3 way = aim - me; way.y = 0f;
            if (way.sqrMagnitude < 0.0001f) way = _path[_next] - me;
            way.y = 0f;
            if (now >= _apartAt) { _apartAt = now + 0.1f * _slow; _apart = Apart(me, way.normalized); } // (others looked at ten times a second, not every frame)
            Vector3 want = (way.normalized + _apart * Push).normalized;
            if (Yield(me, way.normalized, ref want, now)) return false; // (a player in its way: it waits for him, then goes round him)
            if (DoorOnWay(me, want, now)) return false; // (a shut door right in its way: opened, then through)
            if (left > Final) want = RoundThings(me, want); // (by its goal it walks up to it: its seat, its chest stand there)
            Go(want, run, _turn != 0f ? "walks round a thing" : "walks");
            return false;
        }

        // standing on the spot its way ended at, by its goal (for the callers' «there» rule: AtSnap)
        internal bool By(Vector3 to, float near)
        {
            return (to - _goal).sqrMagnitude <= 0.25f && Done && HowFar(_m.transform.position, to) <= near + Reach;
        }

        // A goal inside a thing (2026-11-06, measured: a test, a job, a whim sent people to the middle of a bathtub, a workbench, a chest in a
        // nook — no body stands there, the way ended 1.6–2.4 m short, and the stuck rule put them by it though they stood beside it): how far it
        // is is how far to that thing's surface, and only when nothing else (a wall) stands between; a goal in the open: as the crow flies.
        // The thing is found once a walk (Begin).
        private Collider _thing;
        private Vector3 _aim; // (the floor by that thing: what the way is asked to)
        private bool _hasAim;
        private static readonly Collider[] _at = new Collider[6];
        private static Collider ThingAt(Vector3 to)
        {
            int n = Physics.OverlapSphereNonAlloc(to + Vector3.up * 0.5f, 0.15f, _at, Things2, QueryTriggerInteraction.Ignore); // heavy: one small sphere a walk, at its start
            for (int i = 0; i < n; i++)
            {
                Collider c = _at[i];
                if (c == null) continue;
                Piece p = c.GetComponentInParent<Piece>();
                if (p == null) continue; // (a rock, the ground: no thing to stand by)
                string name = p.gameObject.name;
                if (name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0) continue; // (walked on)
                return c;
            }
            return null;
        }

        private float HowFar(Vector3 me, Vector3 to)
        {
            if (_thing == null || (to - _goal).sqrMagnitude > 0.25f) return Utils.DistanceXZ(me, to);
            Vector3 at = _thing.ClosestPoint(me + Vector3.up * 0.9f);
            float d = Utils.DistanceXZ(me, at);
            if (d > _near + Reach) return Mathf.Max(d, Utils.DistanceXZ(me, to) - 3f); // (far yet: no look for walls)
            RaycastHit h; // (beside it, not on the other side of a wall)
            Vector3 from = me + Vector3.up * 0.9f, way = at - from; float l = way.magnitude;
            if (l > 0.05f && Physics.Raycast(from, way / l, out h, l, Things2, QueryTriggerInteraction.Ignore) && h.collider != _thing && h.collider.GetComponentInParent<Piece>() != _thing.GetComponentInParent<Piece>()) return Utils.DistanceXZ(me, to);
            return d;
        }

        // its caller says it is there (its own rule: a seat sat on, a goal within its «near»): the walk written now
        internal void There(string how) { if (_trip != null && !Done) { Done = true; Doing = "there"; End(how); } }

        private void Begin(Vector3 to, float near, string what)
        {
            if (_trip != null) End(Done ? "ok" : "left off");
            _goal = to; _near = near; _what = what; Done = false; _thing = ThingAt(to); _hasAim = false;
            _hasPath = false; _partial = false; _path.Clear(); _next = 0; _fails = 0; _shortSince = -1f; _shortBest = float.MaxValue; _wayAsks = 0; Unreachable = false;
            _askAt = Time.time; _gainAt = Time.time; _bestLeft = float.MaxValue; _stepUntil = 0f; _turn = 0f; _turnKeep = 0f; _clearAt = 0f; _apartAt = 0f; _reasked = false; // (a new goal: looked at afresh)
            _trip = Watch ? new Trip { What = what, From = _m.transform.position, To = to, At = Time.time } : null;
        }

        private void Arrive(string how) { _m.StopMoving(); if (!Done) { Done = true; Doing = "there"; if (_trip != null) End(how); } }

        private int _fails;
        private bool _reasked;
        private Vector3 _askedFor;
        private static readonly int PerfPath = NpcPerf.Id("path (пошук шляху)"), PerfShape = NpcPerf.Id("path room (відступ від країв)");

        // A way asked is the dearest thing feet do, and a whole base sets off in the same frame (a morning gathering): the asks
        // of everyone together stay within AsksAFrame; the one left out asks next frame, as the ones served have their next ask ahead
        private const int AsksAFrame = 2;
        private static int _asksFrame, _asksNow;
        private static bool MayAsk()
        {
            if (_asksFrame != Time.frameCount) { _asksFrame = Time.frameCount; _asksNow = 0; }
            return _asksNow < AsksAFrame;
        }

        private void Ask(Vector3 me, Vector3 to)
        {
            Pathfinding pf = Pathfinding.instance;
            if (pf == null) { _askAt = Time.time + 1f; return; }
            _ask.Clear();
            _asksNow++;
            long perf = NpcPerf.Now();
            if (_thing != null && !_hasAim && (to - _goal).sqrMagnitude <= 0.25f) _hasAim = NpcKit.FloorBy(out _aim, to, NpcKit.Walker); // (a few looks at the map, until it has an answer; the map near it may be still being built)
            Vector3 aim = _hasAim && (to - _goal).sqrMagnitude <= 0.25f ? _aim : to;
            bool ok = pf.GetPath(me, aim, _ask, NpcKit.Walker, false, true, false);
            NpcPerf.Add(PerfPath, perf);
            if (_trip != null) _trip.Asks++;
            if (!ok || _ask.Count == 0)
            {
                _fails++;
                if (_trip != null) _trip.Fails++;
                _askAt = Time.time + Mathf.Min(2f, Retry * (1f + _fails / 4)); // (the map near it is being built: the next ask finds it — less often when it never does)
                return;
            }
            _path.Clear(); _path.AddRange(_ask);
            _hasPath = true; _askedFor = to; _wayAsks++;
            _next = 0;
            while (_next < _path.Count - 1 && Utils.DistanceXZ(me, _path[_next]) < Corner) _next++;
            _partial = Utils.DistanceXZ(_path[_path.Count - 1], aim) > _near + 0.5f;
            if (_partial) Bridge(pf, aim); // (its way ends short: a gap in the map — a threshold, a raised floor, the foot of stairs — crossed on foot when short)
            long ps = NpcPerf.Now();
            Widen();
            if (NpcClock.Seen(_m.Actor)) Shape(); // (room round its way: only where a player can see it bump — the one rule of «seen»)
            NpcPerf.Add(PerfShape, ps);
            if (_trip != null && _partial) _trip.Partial++;
            _askAt = Time.time + Repath * _slow;
        }

        // past that corner along its way — it went by wider than Corner (a widened corner, its aim cutting inside): on to the next
        private bool Passed(Vector3 me, int i)
        {
            if (i + 1 >= _path.Count) return false;
            Vector3 seg = _path[i + 1] - _path[i], d = me - _path[i]; seg.y = 0f; d.y = 0f;
            return seg.sqrMagnitude > 0.01f && d.sqrMagnitude < 9f && Vector3.Dot(d, seg) > 0f;
        }

        // the point some way ahead on its way (it walks a curve through the corners, not to each one and turn)
        private Vector3 Carrot(Vector3 me, float ahead)
        {
            Vector3 p = me; float rest = ahead;
            for (int i = _next; i < _path.Count; i++)
            {
                Vector3 seg = _path[i] - p; seg.y = 0f;
                float l = seg.magnitude;
                if (l >= rest) return p + seg / l * rest;
                rest -= l; p = _path[i];
            }
            return _path[_path.Count - 1];
        }

        // how far it still has to go along its way (its gain is measured on this, not as the crow flies)
        private float Left(Vector3 me, Vector3 to)
        {
            if (!_hasPath || _next >= _path.Count) return Utils.DistanceXZ(me, to);
            float l = Utils.DistanceXZ(me, _path[_next]);
            for (int i = _next + 1; i < _path.Count; i++) l += Utils.DistanceXZ(_path[i - 1], _path[i]);
            return l + Utils.DistanceXZ(_path[_path.Count - 1], to);
        }

        // others near: a push away from each, harder the nearer; one right in front: a little to its right as well
        private float _apartAt, _clearAt;
        private Vector3 _apart;
        private bool _clear;

        // the straight way there free (a sphere cast five times a second at most, not every frame)
        private bool ClearNow(Vector3 me, Vector3 to)
        {
            if (Time.time < _clearAt) return _clear;
            _clearAt = Time.time + 0.2f * _slow;
            _clear = Clear(me, to);
            return _clear;
        }

        private Vector3 Apart(Vector3 me, Vector3 fwd)
        {
            Vector3 push = Vector3.zero;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.gameObject != _m.gameObject) push += Away(me, o.transform.position, fwd, right, Room);
            foreach (Player p in Player.GetAllPlayers()) if (p != null) push += Away(me, p.transform.position, fwd, right, PlayerRoom) * 1.5f; // (a player is given more room, and gone round earlier, than a person)
            return push;
        }

        // someone close: a push away; someone in its way ahead (within See, nearer its line than Wide): round them early on
        // the side away from them — right, when they are straight ahead (both keep right: they pass)
        private static Vector3 Away(Vector3 me, Vector3 at, Vector3 fwd, Vector3 right, float room)
        {
            Vector3 d = at - me;
            if (Mathf.Abs(d.y) > 1.5f) return Vector3.zero;
            d.y = 0f;
            float m = d.magnitude;
            if (m < 0.01f || m > See) return Vector3.zero;
            Vector3 v = Vector3.zero;
            if (m < room) v -= d / m * (room - m) / room;
            float ahead = Vector3.Dot(d, fwd), side = Vector3.Dot(d, right);
            if (ahead > 0f && Mathf.Abs(side) < Wide) v += right * (Mathf.Abs(side) < 0.25f ? 1f : -Mathf.Sign(side)) * Avoid * (1f - ahead / See) * (1f - Mathf.Abs(side) / Wide);
            return v;
        }

        // nothing built in the straight way there (its last steps; no way yet and its goal near)
        private static readonly int Solids = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");
        private static bool Clear(Vector3 me, Vector3 to)
        {
            Vector3 a = me + Vector3.up * 0.9f, b = to + Vector3.up * 0.9f;
            Vector3 d = b - a;
            float l = d.magnitude;
            if (l < 0.3f) return true;
            return !Physics.SphereCast(a, 0.25f, d / l, out _hit, Mathf.Max(0f, l - 0.6f), Solids, QueryTriggerInteraction.Ignore); // (the last bit is the goal itself: a chest, a seat)
        }
        private static RaycastHit _hit;

        // A player in its way (the user 2026-11-06: «they push into me because they see they must get past»): never a step into a player —
        // the part of its step towards anyone nearer than NoPushAt is taken out, whatever it walks for (its way, its last steps, a step round);
        // one standing right in its way within YieldAt: it stops and waits for him YieldFor s (the player mostly walks on), its stuck clocks
        // held meanwhile, then goes round him on the side away from him (and Apart keeps PlayerRoom off him). People push each other as before
        internal static float NoPushAt = 0.9f, YieldAt = 1.5f, YieldFor = 2.5f, PlayerRoom = 1.4f;
        private float _yieldSince = -1f;
        private static Vector3 NoPush(Vector3 me, Vector3 dir)
        {
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                Vector3 d = p.transform.position - me;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m < 0.01f || m >= NoPushAt) continue;
                float into = Vector3.Dot(dir, d / m);
                if (into > 0f) dir -= d / m * into;
            }
            return dir;
        }

        private bool Yield(Vector3 me, Vector3 way, ref Vector3 want, float now)
        {
            Player block = null; float best = YieldAt;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                Vector3 d = p.transform.position - me;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m < 0.01f || m >= best || Vector3.Dot(d / m, way) < 0.6f) continue;
                if (Utils.DistanceXZ(p.transform.position, _goal) < 2.5f) continue; // (he is where it goes — «to me», «follow me»: no waiting for him nor going round him; NoPush still keeps it off him)
                best = m; block = p;
            }
            if (block == null) { _yieldSince = -1f; return false; }
            if (_yieldSince < 0f) _yieldSince = now;
            if (now - _yieldSince < YieldFor) // (only while it waits for him: waiting is no being stuck — nor the last safety's «got nowhere», a player standing long in a doorway sent it home in a puff. Going round him after, its stuck rules hold again: a step up, a slide out, home)
            {
                _gainAt = now; _pushAt = now; _anchorAt = now; _m.NewGoal();
                _m.StopMoving(); _m.Face(block.transform.position); Doing = "waits for a player in its way"; return true;
            }
            Vector3 to = block.transform.position - me; to.y = 0f;
            Vector3 right = new Vector3(way.z, 0f, -way.x);
            float side = Vector3.Dot(to, right) > 0f ? -1f : 1f; // (round him on the side away from him; right when he stands square ahead)
            want = (want + right * side * 1.2f).normalized;
            return false;
        }

        internal static float EaseFrom = 2f, EaseLeast = 0.4f; // m before its goal it starts to slow; the share of its speed at the end
        private float _left = 99f;

        private void Go(Vector3 dir, bool run, string doing)
        {
            dir = NoPush(_m.transform.position, dir);
            if (dir.sqrMagnitude < 0.01f) { _m.StopMoving(); _anchorAt = Time.time; Doing = "waits: a player right in its way"; return; } // (all of its step was into a player: no being stranded either)
            Doing = doing;
            if (_left < _shortBest - ShortGain) { _shortSince = -1f; _shortBest = _left; } // (nearer by a step: not standing short of it. A shuffle at the same spot, a way asked again and walked a little, is no gain: else its clock began anew each time and it pushed till it was put home, 2026-11-05)
            _m.Steer(dir, run, run ? 1f : Mathf.Lerp(EaseLeast, 1f, Mathf.SmoothStep(0f, 1f, (_left - _near) / EaseFrom))); // (a walk slows as it comes, no sudden stop; a run — to a foe, away from one — keeps its speed)
            if (_trip != null) { if (_trip.HaltSince >= 0f) Flush(); _trip.Moving(_m); Dips(); } // (a halt over: written now)
            Vector3 at = _m.transform.position; float now = Time.time;
            if (now - _lastGo > 0.3f) { _pushFrom = at; _pushAt = now; } // (it stood meanwhile of its own will — aimed, worked, waited: counted afresh, not «held fast»)
            _lastGo = now;
            if ((at - _pushFrom).sqrMagnitude > 0.04f) { _pushFrom = at; _pushAt = now; }
            else if (now - _pushAt > StepAfter && now >= _stepUpNext) { bool up = StepUp(at, dir); _stepUpNext = now + (up ? 1f : 0.25f); if (up) return; } // (a step it does not climb by itself: up onto it now — not after the ladder's six seconds)
            else if (now - _pushAt > 6f && now >= _wedgeNext && !PeopleAhead(at, dir)) { _wedgeNext = now + (Inside(at) ? 2f : 6f); Unwedge(at, dir, now - _pushAt > 20f || (Inside(at) && now - _pushAt > 8f) ? 3f : 1f); } // (in a thing: tried again sooner — at eight seconds the safety, Free) // (held fast six seconds — a step's slide; twenty — further. Not while someone stands in its way: they move on, it waits)
            else if (now - _pushAt > 2f && _trip != null && !_trip.Pushed) // (it means to walk and does not move: what holds it — once a walk)
            {
                _trip.Pushed = true;
                NpcBody b = _m.Body; Character c = _m.GetComponent<Character>();
                Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\tpushes, no move 2s\tat {3}\tattached {4}\tseated {5}\tground {6}\tspeed {7:0.00}\tstatus {8}\tin front: {9}\tnear: {10}",
                    DateTime.Now, _m.Name(), _trip.What, Npcs.V(at), b != null && b.Attached, _m.SeatedNow, c != null && c.IsOnGround(), c != null ? c.GetVelocity().magnitude : 0f, _m.Status, Things(at, dir), People(at)));
            }
        }
    }
}
#endif

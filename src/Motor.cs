#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // «Мотор ворона»: the one layer that flies a raven, whoever drives it (an outing, the dance, a strike, the dome,
    // leaving, later the commands). Drivers only say where they want it (Steer) and when it goes home (Home); the
    // motor keeps the same body with its speed for as long as the bird is in the air, so one driver taking it from
    // another (an outing cut by a strike) does not stop it dead and push it off again. A new aim is eased in (no
    // sudden turn), each raven flies a little its own way (a stable personal touch, more restless at low trust), and
    // it keeps clear of the ground, walls and trees. Tuned in the test window, tab «Політ» (flight.txt).
    internal sealed class RavenMotor
    {
        // ---------------------------------------------------------------- tuning (the «Політ» tab)
        internal static float BlendSeconds = 0.6f;   // a new aim eases in over this long
        internal static float Spread = 0.08f;        // each raven's own touch: ± this share on its speeds, turns, beats
        internal static float NervesWeight = 0.35f;  // how much trust shows in its flight (low: twitchy, high: calm)
        internal static bool AvoidOn = true;
        internal static float LookAhead = 0.6f;      // s of flight it looks ahead for something in the way
        internal static float PushStrength = 4f;     // m it swerves at most from what is in the way
        internal static float GroundClear = 1.5f;    // m it keeps over the ground and water
        internal static float ProbeRadius = 0.45f;   // the look ahead (Avoid): wide, to swerve early
        internal static float BodyRadius = 0.14f;    // the bird itself (ClearMove): it must still pass by a wall, a perch's bar, a ceiling
        internal static float ProbeMinReach = 2f;
        internal static float ProbeMaxReach = 12f;
        internal static float ProbeInterval = 0.08f;
        internal static float OwnerQuiet = 3f;       // m: this near you it does not swerve (the landing, the dance)
        internal static float FarProbe = 60f;        // m: another player's raven farther than this from you flies without probes
        internal static int Version;                 // bumped by the panel: personal styles are made again

        private readonly Transform _t;
        private readonly ShoulderLife _life;
        private readonly bool _munin;
        private readonly Player _owner;
        internal FlightBody Body;
        internal string Plan = "";
        private int _lastFrame = -100;
        private Vector3 _aimShown, _blendFrom;
        private bool _aimSet;
        private float _blendAt = -1f;
        private float _clock;       // its own time (easing, waves): a slowed picture slows them too
        private float _wavePh;      // the template wave's phase, in turns (added up: its frequency may change in a blend)
        private Vector3 _push, _pushWant;
        private float _probeAt, _aimFloor = float.MinValue;
        internal float Flare;
        private FlightStyle _style;
        private readonly FlightStyle _eff = new FlightStyle(); // its style with the template's motion on it
        private int _sit = -2;      // the flight director: the situation it last picked for
        private float _nextPick;
        private int _styleVer = -1, _styleLevel = -1;
        private bool _diving;       // the director's "dive": in at a steep fall, out only once it has eased off, held a moment
        private float _divingSince;
        internal static float DiveHold = 0.6f; // s: at least this long in or out of a dive (waves would flip it each second)
        internal bool Diving { get { return _diving; } }

        private RavenMotor(Transform t, ShoulderLife life, bool munin, Player owner) { _t = t; _life = life; _munin = munin; _owner = owner; }

        // the motor of this bird (one kept on its ShoulderLife; a bird without one gets a passing motor)
        internal static RavenMotor Of(GameObject model, bool munin, Player owner)
        {
            if (model == null) return null;
            ShoulderLife life = model.GetComponent<ShoulderLife>();
            if (life == null) return new RavenMotor(model.transform, null, munin, owner);
            if (life.Motor == null) life.Motor = new RavenMotor(model.transform, life, life.Munin, life.Player != null ? life.Player : owner);
            return life.Motor;
        }

        // in the air right now (someone flew it this frame or a moment ago)
        internal bool Airborne { get { return Body != null && Time.frameCount - _lastFrame <= 3; } }
        internal bool DivePoseTest
        {
            get
            {
                return FlightTemplates.DiveTestPoseOnly && FlightTemplates.DiveTestName.Length > 0 &&
                    Plan == "пікірування" && _owner != null && ReferenceEquals(_owner, Player.m_localPlayer);
            }
        }
        internal float Avoiding { get { return _push.magnitude; } }
        internal FlightStyle StyleNow { get { return Style(); } } // this raven's own style (the panel shows it)
        internal Vector3 Aim { get { return _aimShown; } }

        // A new way through the air. Already flying: it keeps its speed and goes on (no second take-off); on the
        // shoulder: it pushes off with 'vel'. True when it really took off now (the take-off flap is the caller's).
        internal bool Launch(Vector3 vel, string plan, float accelMul)
        {
            bool fresh = !Airborne;
            if (fresh) { Body = new FlightBody(_t, _life, _munin, vel, plan); _aimSet = false; _push = _pushWant = Vector3.zero; Flare = 0f; _sit = -2; _diving = false; _divingSince = _clock; if (_life != null && plan != "приліт") _life.PrepareTakeoff(); }
            else Body.NewLeg();
            LandNow = null; Body.Land = null; // a new way: its landing is picked afresh when it comes down
            Body.Style = Style();
            Body.Char = Char();
            Body.AccelMul = accelMul;
            if (fresh && plan != "приліт" && plan != "перегляд") BeginTakeoff(vel, plan); // (a fly-in is already in the air; the test picture flies at once)
            else if (fresh) _holdUntil = -1f;
            else if (_holdUntil >= 0f && _take != null) _launchVel = ClearWay(Quaternion.AngleAxis(_take.Veer, Vector3.up) * vel * _take.Push); // taken while still crouching: it springs off the new way
            SetPlan(plan);
            _lastFrame = Time.frameCount;
            return fresh;
        }

        internal Vector3 TakeoffVelocity(Vector3 aim, Vector3 carry, string plan)
        {
            Vector3 route = aim - _t.position;
            route = Vector3.ProjectOnPlane(route, Vector3.up);
            if (route.sqrMagnitude > 0.0001f) route.Normalize();
            Vector3 facing = _t.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation)) * Vector3.forward;
            facing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
            if (route.sqrMagnitude < 0.0001f) route = facing;
            if (facing.sqrMagnitude > 0.0001f)
                route = Vector3.Slerp(facing, route, FlightBody.QuickLaunch(plan) ? 0.4f : 0.28f).normalized;
            bool quick = FlightBody.QuickLaunch(plan);
            Vector3 vel = route * (quick ? 2.5f : 1.6f) + Vector3.up * (quick ? 3f : 2.7f) + carry * 0.35f;
            return Vector3.ClampMagnitude(vel, quick ? 4.6f : 4.1f);
        }

        // it sits (the landing took it): the next flight starts afresh
        internal void Grounded() { _holdUntil = -1f; LandNow = null; Body = null; Plan = ""; Flare = 0f; _aimSet = false; _blendAt = -1f; _push = _pushWant = Vector3.zero; _sit = -2; _diving = false; if (_life != null) { _life.Mix.Clear(); _life.FinishTakeoff(); } }

        private void TrackDive()
        {
            if (Body == null) { _diving = false; return; }
            bool want = _diving ? Body.Speed > 5.5f && Body.Vel.y < -1f : Body.Speed > 7f && Body.Vel.y < -2.5f;
            if (want != _diving && _clock - _divingSince >= DiveHold) { _diving = want; _divingSince = _clock; }
        }

        // what it does now changes: the new aim is eased in from the one it flew to
        internal void SetPlan(string plan)
        {
            if (plan == Plan) return;
            Plan = plan ?? "";
            if (_aimSet) { _blendFrom = _aimShown; _blendAt = _clock; }
        }

        // this raven's own way of flying: its kind, its personal touch (stable, from its owner), its trust
        private FlightStyle Style()
        {
            int level = _life != null ? _life.Level : 1, norov = NorovIndex();
            float nerves = _life != null ? _life.Nerves : 1f; // (trust and its character's restlessness, each on its own clock)
            int nervesKey = Mathf.RoundToInt(nerves * 100f);
            float fly = RuneStars.FlyMul(_owner, _munin, false);
            if (_style != null && _styleVer == Version && _styleLevel == level && _styleNorov == norov && _styleNerves == nervesKey && _styleFly == fly) return _style;
            int seed = _munin ? 7 : 3;
            try { if (_owner != null) { long id = _owner.GetPlayerID(); seed = (int)(id ^ (id >> 32)) * 31 + seed; } } catch { }
            _style = RavenFlight.Personal(_munin, seed, nerves);
            _styleFly = fly;
            if (fly != 1f) { _style.Cruise *= fly; _style.Top *= fly; _style.Accel *= fly; } // («Буря»: faster)
            _styleVer = Version; _styleLevel = level; _styleNorov = norov; _styleNerves = nervesKey;
            return _style;
        }
        private int _styleNorov = -2, _styleNerves = -1;
        private float _styleFly = 1f;

        // its character in the norov (the owner's ZDO), looked up once a second: Style and Char are asked every frame
        private int _norov = -1;
        private float _norovAt = -1f;
        private int NorovIndex()
        {
            if (Time.time >= _norovAt) { _norovAt = Time.time + 1f; _norov = Norov.IndexOnPlayer(_owner, _munin); }
            return _norov;
        }

        // its character at its trust (RavenFlight.CharFor) with its own norov, made again when the trust, the norov
        // (it comes over the network a little after the bird) or the panel changes it
        private FlightCharacter _char;
        private int _charVer = -1, _charLevel = -1, _charNorov = -2;
        internal FlightCharacter Char()
        {
            int level = _life != null ? _life.Level : 1, norov = NorovIndex();
            if (_char == null || _charVer != RavenFlight.CharVersion || _charLevel != level || _charNorov != norov)
            {
                _char = RavenFlight.CharFor(_munin, level, norov >= 0 ? Norov.Characters[norov] : null);
                _charVer = RavenFlight.CharVersion; _charLevel = level; _charNorov = norov;
            }
            return _char;
        }

        // One frame towards 'aim' (FlightBody.Toward), eased from the last aim, swerving from what is in the way
        // (avoid: 0 none … 1 full). Returns the distance left to the aim itself.
        internal float Steer(Vector3 aim, float speed, bool arrive, float sharp, float dt, Vector3 carry, float avoid, bool pursueTarget = false)
        {
            _lastFrame = Time.frameCount;
            _clock += dt;
            if (Body == null) return 0f;
            if (_holdUntil >= 0f) { if (_clock < _holdUntil) { HoldStep(); return Vector3.Distance(_t.position, aim); } Push(); } // the take-off's crouch, then the spring
            if (_styleVer != Version) Style(); // tuned in the panel: at once, in the air
            Body.Char = Char();
            Direct();
            Body.Style = Effective();
            Vector3 use = aim;
            if (_blendAt >= 0f)
            {
                float u = (_clock - _blendAt) / Mathf.Max(0.05f, BlendSeconds);
                if (u >= 1f) _blendAt = -1f;
                else { u = u * u * (3f - 2f * u); use = Vector3.Lerp(_blendFrom, aim, u); }
            }
            _aimShown = use; _aimSet = true;
            // by you it does not swerve (the landing, the dance), except in its first moment off the shoulder: a wall or a
            // branch right by you is seen then (your own body is never in its way: Probe)
            if (_owner != null && Body.Age > LaunchAvoid && (_t.position - _owner.transform.position).sqrMagnitude < OwnerQuiet * OwnerQuiet) avoid = 0f;
            // another player's raven far from you: nobody sees it closely, no probes for it
            Player me = Player.m_localPlayer;
            if (avoid > 0f && _owner != null && me != null && !ReferenceEquals(_owner, me) && (_t.position - me.transform.position).sqrMagnitude > FarProbe * FarProbe) avoid = 0f;
            if (Body.Final) { avoid = 0f; _push = Vector3.zero; Body.Gust = 0f; } // the landing goes exactly where it aims
            else
            {
                // the template's waves: up and down along the way (not on the final approach)
                TemplateMix mx = _life != null ? _life.Mix : null;
                float wave = mx != null && !DivePoseTest ? mx.Wave : 0f;
                if (wave > 0.01f) { _wavePh += dt * mx.WaveHz; use += Vector3.up * Mathf.Sin((_wavePh + (_munin ? 0.27f : 0f)) * Mathf.PI * 2f) * wave; }
                // a young raven does not keep its line yet: its aim wobbles (not by your shoulder, where it would only shake)
                float wob = Body.Char.Wobble;
                if (wob > 0.01f && _owner != null)
                {
                    wob *= Mathf.Clamp01(((_t.position - _owner.transform.position).magnitude - 1.5f) / 3f);
                    float s0 = _munin ? 11.3f : 4.1f;
                    use += new Vector3(Mathf.PerlinNoise(_clock * 0.9f, s0) - 0.5f, (Mathf.PerlinNoise(s0, _clock * 0.7f) - 0.5f) * 0.6f, Mathf.PerlinNoise(_clock * 0.8f + 3f, s0 + 2f) - 0.5f) * (2f * wob);
                }
            }
            if (!Body.Final)
            {
                use += Separate();      // the other raven: not into it
                use += Gusts(dt);       // the wind throws it about a little
            }
            use += Avoid(use, avoid, dt, pursueTarget);
            Vector3 from = _t.position;
            Body.Toward(use, speed, arrive, sharp, dt, carry);
            Vector3 normal;
            Vector3 safe = ClearMove(from, _t.position, out normal);
            if (safe != _t.position)
            {
                _t.position = safe;
                Body.Slide(normal); // on along the surface: what went into it is lost, the rest stays (no stop in mid-air)
            }
            KeepOutOfWater(dt);
            return Vector3.Distance(_t.position, aim);
        }

        // A frame of a way flown by hand (a figure, a hop to a perch): where it should be and how it faces ('look', the
        // bird's own frame, without the model's turn); wings: 0 as the flight goes, 1 gliding, 2 hard beats (a figure
        // says). The body stays in sync for whoever flies it next. The bird does not jump onto the way it is given: it follows it as a body (a spring on the way's own speed
        // and pull), so it starts from how it flew (where it is, how fast, which way: the way in to a figure, a hop to a
        // perch) and settles onto the way within a moment, and a kink in a way (a figure drawn with a jump in speed)
        // is taken as a firm swing, never a jerk: no more pull than DriveAccel. Its turn eases in from how it was turned.
        // False: something is in the way (a tree, a hillside): the bird stops short of it and the figure must end
        // there; flying on would have it jump the whole gap at once when the way is clear again.
        internal static float DriveTop = 25f;     // m/s: a hitch in the frames never throws it off faster than this
        internal static float DriveFollow = 7f;   // how firmly it keeps to the way (1/s): settles onto it in ~0.6 s
        internal static float DriveAccel = 80f;   // m/s²: the hardest pull (a tight spiral needs ~60)
        internal static float DriveTurnIn = 0.4f; // s: its turn eases into the way's over this long
        private int _driveFrame = -100;
        private float _driveAge;
        private int _wayFrames;
        private Vector3 _wayPrev, _wayVel;
        private Quaternion _driveFrom = Quaternion.identity;
        internal bool Drive(Vector3 pos, Quaternion look, float dt, int wings = 0)
        {
            _lastFrame = Time.frameCount;
            _clock += dt;
            if (Body == null) return true;
            if (_holdUntil >= 0f) { if (_clock < _holdUntil) { HoldStep(); return true; } Push(); } // the take-off's crouch, then the spring
            Vector3 from = _t.position;
            if (!Body.Final && ZoneSystem.instance != null) pos.y = Mathf.Max(pos.y, ZoneSystem.instance.m_waterLevel + WaterClear); // a figure off a ship's side: over the water, not into it (the follower eases onto it)
            if (Time.frameCount - _driveFrame > 2)
            {
                // a new way by hand: from how the bird goes now
                _wayFrames = 0; _wayPrev = pos; _wayVel = Body.Vel; _driveAge = 0f;
                _driveFrom = _t.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            }
            _driveFrame = Time.frameCount;
            _driveAge += dt;
            Vector3 next = pos;
            if (dt > 0f)
            {
                // the way's own speed and pull (from the points it is given; the pull only once two speeds are known)
                Vector3 wv = _wayFrames > 0 ? (pos - _wayPrev) / dt : _wayVel;
                Vector3 wa = _wayFrames > 1 ? (wv - _wayVel) / dt : Vector3.zero;
                _wayPrev = pos; _wayVel = wv; _wayFrames++;
                float k = DriveFollow;
                Vector3 v = Body.Vel;
                Vector3 pull = wa + (pos - from) * (k * k) + (wv - v) * (2f * k);
                v += Vector3.ClampMagnitude(pull, DriveAccel) * dt;
                next = from + v * dt;
            }
            Vector3 normal;
            Vector3 safe = ClearMove(from, next, out normal);
            bool clear = (safe - next).sqrMagnitude < 0.0004f;
            if (dt > 0f) Body.Drove(Vector3.ClampMagnitude((safe - from) / dt, DriveTop), dt);
            if (Plan == "пікірування") Direct();
            _t.position = safe;
            KeepOutOfWater(dt);
            float tw = Mathf.Clamp01(_driveAge / Mathf.Max(0.01f, DriveTurnIn));
            tw = tw * tw * (3f - 2f * tw);
            _t.rotation = (tw >= 1f ? look : Quaternion.Slerp(_driveFrom, look, tw)) * Quaternion.Euler(Shoulder.Rotation);
            _aimShown = safe; _aimSet = true; _blendAt = -1f;
            Body.Synced();
            Body.Gust = 0f; // (a figure is flown as drawn)
            Body.WingMode = wings;
            Body.Wings(dt, 0f);
            Body.WingMode = 0;
            HoldDivePose();
            return clear;
        }

        // nothing solid between two points of a figure's way (TrickPlan looks ahead on it); a little wider than the bird,
        // the same world as ClearMove; none for another player's raven far from you
        internal static float FigureRadius = 0.25f;
        internal bool WayClear(Vector3 a, Vector3 b)
        {
            if (!AvoidOn) return true;
            Player me = Player.m_localPlayer;
            if (_owner != null && me != null && !ReferenceEquals(_owner, me) && (_t.position - me.transform.position).sqrMagnitude > FarProbe * FarProbe) return true;
            Vector3 d = b - a;
            float l = d.magnitude;
            if (l < 0.01f) return true;
            RaycastHit hit;
            return !Probe(a, d / l, l, FigureRadius, _t, _owner != null ? _owner.transform : null, out hit);
        }

        // facing and wings for this frame (flare: braking to land, eased)
        internal void Finish(float dt, float flareWant)
        {
            if (Body == null || _holdUntil >= 0f) return; // (crouching: it is placed by the hold)
            Flare = Mathf.MoveTowards(Flare, flareWant, dt * 3f);
            Body.Face(dt, Flare);
            Body.Wings(dt, Flare);
            HoldDivePose();
        }

        private void HoldDivePose()
        {
            if (!DivePoseTest && _life != null && Plan == "пікірування" && Body != null && Body.Vel.y < -0.5f)
                _life.Flight.Dive = Mathf.Max(_life.Flight.Dive, Mathf.Clamp01((-Body.Vel.y - 0.5f) / 3f));
        }

        // Home onto the seat, the same for every driver: round to its side of you, then the hover over the shoulder,
        // turning your way, braking; true when the game's landing may take it (the caller hands it to the pin).
        // pre: a first point to pass (down from the dance: a little up and back first), until it is final.
        internal bool Home(Vector3 seat, Player owner, float speed, float dt, bool wave, float seed, bool hasPre, Vector3 pre)
        {
            SetPlan(Plan == "приліт" ? "приліт" : "додому"); // the fly-in stays the fly-in all the way down
            speed *= RuneStars.FlyMul(owner, _munin, true); // («Буря»)
            if (Body == null) return true;
            LandStyle land = Landing();
            float seatD = Vector3.Distance(_t.position, seat);
            WatchProgress(seatD);
            Vector3 aim, way = Vector3.zero;
            bool round = owner != null && Detour(seat + Vector3.up * 0.3f, owner.transform.position, out way); // you are behind a wall: the way round (a door), not through it
            if (round && Body.Final && seatD > 3f) Body.Unfinal();
            if (round) { aim = way; speed = Mathf.Min(speed, DetourSpeed); } // looking for the way: slower, its inertia as ever
            else if (hasPre && !Body.Final)
            {
                aim = pre;
                if (Vector3.Distance(_t.position, pre) < 0.35f) Body.MarkFinal();
            }
            else aim = Body.HomeAim(seat, owner);
            if (Body.Final) aim += RavenFlight.FlyFix(seatD, owner); // the landing panel's nudges to the way in
            else if (wave) aim += Body.Wave(seatD, seed);
            if (owner != null && ReferenceEquals(owner, Player.m_localPlayer)) RavenFlight.Note(_munin, seatD, Body.Speed, Body.Final);
            float near = Vector3.Distance(_t.position, aim);
            float flare = Body.Final ? Mathf.Clamp01(1f - near / Mathf.Max(0.5f, land.Flare)) : 0f;
            Vector3 carry = owner != null ? owner.GetVelocity() : Vector3.zero;
            if (owner != null)
            {
                float d = Vector3.Distance(_t.position, owner.transform.position);
                if (!round && (Body.Final || d < RavenFlight.TurnFrom)) Body.FaceHint(owner.transform.forward, 1f - Mathf.Min(near, d) / RavenFlight.TurnFrom); // turns your way from 6 m on
            }
            // round: on through each turn of the way, no braking at it, and only a little swerving (the way is already
            // clear; a full swerve pushes it off a door frame and up into a low ceiling: GroundClear is over DetourLift)
            float left = Steer(aim, speed, !round, Body.Final && near < 4f ? 2f : 0f, dt, carry, round ? DetourAvoid : 1f);
            Finish(dt, flare);
            return !round && ReadyToLand(left, carry);
        }

        // ---------------------------------------------------------------- the way round (you behind a wall)
        // Out of sight of the seat (a perch in another room, you went indoors) it does not fly straight through the
        // walls: it takes the way a man would walk (the game's own path finding, the way round through a door), a
        // little over the floor, cutting to the farthest turn it can already see. Cheap: a sight line 4 times a second,
        // a path once a second, only while it cannot see you. No path (a cliff top, no ground way): it goes on by the
        // walls and the swerving as before; stuck for long, the pin brings it to you in a puff (Stuck).
        internal static bool DetourOn = true;
        internal static float DetourSpeed = 4.5f;   // m/s at most while it looks for the way round
        internal static float DetourLift = 1.4f;    // m over the walked way (less under a low ceiling)
        internal static float StuckSeconds = 8f;    // s without getting nearer: stuck (the soft return to you)
        internal static float DetourAvoid = 0.3f;   // share of the usual swerving while on the way round
        private readonly List<Vector3> _way = new List<Vector3>();
        private int _wayAt, _homeFrame = -100;
        private float _seeAt, _wayAskAt, _bestD, _bestAt, _blindSince;
        private bool _blind;
        private Vector3 _wayAim;
        // true: out of sight of you, it has not come nearer for StuckSeconds (behind closed doors, in a hole); the
        // flight that drives it hands it to the pin's ComeBack (a puff, flown in again)
        internal bool Stuck { get { return _blind && Time.frameCount - _homeFrame <= 3 && _clock - Mathf.Max(_bestAt, _blindSince) > StuckSeconds && _bestD > 1.5f; } }

        private void WatchProgress(float seatD)
        {
            if (Time.frameCount - _homeFrame > 3) { _bestD = seatD; _bestAt = _clock; _blind = false; _way.Clear(); _seeAt = _wayAskAt = 0f; } // a new way home
            _homeFrame = Time.frameCount;
            if (seatD < _bestD - 0.75f) { _bestD = seatD; _bestAt = _clock; }
        }

        // another flight's way to a place it must see (a perch, a spot to sit): true and the next turn of the way round
        // while it is out of sight; called every frame of that flight (its progress is watched for Stuck too)
        internal bool Round(Vector3 see, Vector3 walkTo, out Vector3 aim)
        {
            WatchProgress(Vector3.Distance(_t.position, see));
            return Detour(see, walkTo, out aim);
        }

        // see: the point it must have in sight (else it goes round); walkTo: where the walked way leads
        private bool Detour(Vector3 see, Vector3 walkTo, out Vector3 aim)
        {
            aim = Vector3.zero;
            if (!DetourOn || !AvoidOn) return false;
            Player me = Player.m_localPlayer;
            if (me == null || (_t.position - me.transform.position).sqrMagnitude > FarProbe * FarProbe) return false; // nobody sees it closely
            Vector3 pos = _t.position;
            if (_clock >= _seeAt)
            {
                _seeAt = _clock + 0.25f;
                bool was = _blind;
                _blind = Physics.Linecast(pos, see, Mask, QueryTriggerInteraction.Ignore);
                if (_blind && !was) { _way.Clear(); _wayAskAt = 0f; _blindSince = _clock; }
                if (_blind) PickWayPoint(pos);
            }
            if (!_blind) return false;
            if (_clock >= _wayAskAt)
            {
                _wayAskAt = _clock + 1f;
                AskWay(pos, walkTo);
                PickWayPoint(pos);
            }
            if (_way.Count == 0) return false; // no way known: straight on, by the walls (and Stuck if it never gets through)
            aim = _wayAim;
            return true;
        }

        private void AskWay(Vector3 pos, Vector3 to)
        {
            _way.Clear(); _wayAt = 0;
            if (Pathfinding.instance == null) return;
            RaycastHit hit;
            Vector3 from = Physics.Raycast(pos, Vector3.down, out hit, 8f, Mask, QueryTriggerInteraction.Ignore) ? hit.point : pos; // the floor under it: where a walker would stand
            try { if (!Pathfinding.instance.GetPath(from, to, _way, Pathfinding.AgentType.Humanoid)) _way.Clear(); } catch { _way.Clear(); }
            for (int i = 0; i < _way.Count; i++) _way[i] = Lifted(_way[i]);
        }

        // over the walked point, kept under a ceiling
        private static Vector3 Lifted(Vector3 p)
        {
            RaycastHit hit;
            float up = DetourLift;
            if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.up, out hit, DetourLift + 0.6f, Mask, QueryTriggerInteraction.Ignore))
                up = Mathf.Clamp(hit.distance - 0.4f, 0.5f, DetourLift);
            return p + Vector3.up * up;
        }

        // the farthest turn of the way it can see from here (a few looks at most): it cuts the corners, not each step
        private void PickWayPoint(Vector3 pos)
        {
            if (_way.Count == 0) return;
            while (_wayAt < _way.Count - 1 && (_way[_wayAt] - pos).sqrMagnitude < 1f) _wayAt++;
            int pick = _wayAt;
            for (int i = _wayAt + 1; i < _way.Count && i <= _wayAt + 4; i++)
            {
                if (Physics.Linecast(pos, _way[i], Mask, QueryTriggerInteraction.Ignore)) break; // round the corner: not yet
                pick = i;
            }
            _wayAt = pick;
            _wayAim = _way[pick];
        }

        // ---------------------------------------------------------------- the landing (Landings)
        internal LandStyle LandNow; // this way home's landing: its own, unlike the other raven's about now
        internal LandStyle Landing()
        {
            if (LandNow == null) LandNow = Landings.Pick(_owner, _munin, Char());
            if (Body != null) Body.Land = LandNow;
            return LandNow;
        }

        // near, slow and turned to sit (FlightBody.CanLand), and its turn by the other raven (the dispatcher); the
        // landing's style goes with the bird to the landing clip
        internal bool ReadyToLand(float left, Vector3 carry)
        {
            if (Body == null) return true;
            if (!Body.CanLand(left, carry)) return false;
            return MayTouchNow();
        }

        // its turn to touch down by the other raven (the dispatcher): the landing's style goes with the bird to the clip
        // (also for a hop to a perch, which has no approach to judge)
        internal bool MayTouchNow()
        {
            LandStyle land = Landing();
            if (!Landings.MayTouch(_owner, _munin, land, ShoulderLife.LandLeadSeconds / Mathf.Max(0.3f, land.ClipRate))) return false;
            if (_life != null) _life.NextLand = land;
            if (AnimDirector.LogOn) AnimDirector.Note(_t.gameObject, "landing: " + land.Describe());
            return true;
        }

        // ---------------------------------------------------------------- the take-off (Landings.PickTakeoff)
        // Off the shoulder (or a perch) it does not jump at full speed in one frame: it crouches a moment where it sits
        // (going along with you), its wings unfolding (ShoulderLife.TakeoffHold), then springs off into its first
        // downstroke, turning out of the crouch (FlightBody.Synced), the launch ramp from there.
        private TakeoffStyle _take;
        private float _holdFrom, _holdUntil = -1f;
        private Vector3 _launchVel, _anchorPos;
        private Quaternion _anchorRot;
        private bool _anchorOwner;
        internal bool Holding { get { return _holdUntil >= 0f && Body != null; } }
        internal TakeoffStyle TakeoffNow { get { return _take; } }
        internal Vector3 LaunchVel { get { return _launchVel; } } // the way it will spring off (the head looks there while it crouches)
        internal static float CrouchLean = 8f; // ° it leans forward into the spring

        private void BeginTakeoff(Vector3 vel, string plan)
        {
            _take = Landings.PickTakeoff(_owner, _munin, Char(), plan);
            _launchVel = ClearWay(Quaternion.AngleAxis(_take.Veer, Vector3.up) * vel * _take.Push); // a wall ahead: off where it is open
            _holdFrom = _clock; _holdUntil = _clock + _take.Hold;
            _anchorOwner = _owner != null && (_t.position - _owner.transform.position).sqrMagnitude < 6.25f; // on you: it goes along with you
            if (_anchorOwner) { _anchorPos = _owner.transform.InverseTransformPoint(_t.position); _anchorRot = Quaternion.Inverse(_owner.transform.rotation) * _t.rotation; }
            else { _anchorPos = _t.position; _anchorRot = _t.rotation; }
            Body.Vel = Vector3.zero;
            if (_life != null) _life.TakeoffHold(_take.Hold, _take.Crouch, _take.Unfold);
            if (AnimDirector.LogOn) AnimDirector.Note(_t.gameObject, "take-off (" + plan + "): " + _take.Describe());
        }

        // a frame of the crouch: where it sits (with you), down into it and up a touch just before the spring
        private void HoldStep()
        {
            float u = Mathf.Clamp01((_clock - _holdFrom) / Mathf.Max(0.01f, _take.Crouch));
            float c = u < 0.7f ? Smooth(u / 0.7f) : 1f - 0.35f * Smooth((u - 0.7f) / 0.3f);
            Vector3 pos = _anchorOwner && _owner != null ? _owner.transform.TransformPoint(_anchorPos) : _anchorPos;
            Quaternion rot = _anchorOwner && _owner != null ? _owner.transform.rotation * _anchorRot : _anchorRot;
            Quaternion facing = rot * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            _t.position = pos - Vector3.up * (_take.Depth * c);
            // turned in the crouch towards where it will spring off (a wall ahead: it looks round and goes the other way)
            float turn = Mathf.Clamp(_holdYaw, -90f, 90f) * Smooth(u);
            _t.rotation = Quaternion.AngleAxis(turn, Vector3.up) * Quaternion.AngleAxis(CrouchLean * c, facing * Vector3.right) * rot;
            _aimShown = _t.position; _aimSet = true;
        }

        // the spring: off at its push speed, turning out of the crouch (not snapped level)
        private void Push()
        {
            _holdUntil = -1f;
            Body.Vel = _launchVel;
            Body.Synced();
        }

        // Where it springs off to: the way it wants if open for TakeoffClear m, else the nearest open way round it (both
        // sides by turns, its veer side first), else straight up if there is room. One sweep a take-off, usually the first
        // try; none for another player's raven far from you.
        internal static float TakeoffClear = 2.2f, LaunchAvoid = 0.5f;
        private static readonly float[] Tries = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
        private float _holdYaw;
        private Vector3 ClearWay(Vector3 v)
        {
            _holdYaw = 0f;
            if (!AvoidOn) return v;
            Player me = Player.m_localPlayer;
            if (_owner != null && me != null && !ReferenceEquals(_owner, me) && (_t.position - me.transform.position).sqrMagnitude > FarProbe * FarProbe) return v;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float fs = flat.magnitude;
            if (fs < 0.05f) return v;
            Vector3 dir0 = flat / fs, origin = _t.position + Vector3.up * 0.05f;
            Transform own = _owner != null ? _owner.transform : null;
            float side = _take != null && _take.Veer < 0f ? -1f : 1f;
            RaycastHit hit;
            foreach (float a0 in Tries)
            {
                float a = a0 * side;
                Vector3 way = (Quaternion.AngleAxis(a, Vector3.up) * dir0 * fs + Vector3.up * v.y).normalized;
                if (Probe(origin, way, TakeoffClear, BodyRadius * 1.5f, _t, own, out hit)) continue;
                _holdYaw = a;
                return Quaternion.AngleAxis(a, Vector3.up) * v;
            }
            if (!Probe(origin, Vector3.up, TakeoffClear, BodyRadius * 1.5f, _t, own, out hit)) return Vector3.up * v.magnitude + dir0 * 0.3f; // walled in all round: straight up
            return v;
        }

        // ---------------------------------------------------------------- the pair in the air
        // The other raven of the same owner, looked up twice a second: closer than PairGap, both swerve apart (and one a
        // little up, the other down), so they never fly through each other. A distance, no physics.
        internal static float PairGap = 1.2f, PairPush = 2f;
        private RavenMotor _partner;
        private float _partnerAt;
        private Vector3 Separate()
        {
            if (_owner == null || PairGap <= 0f) return Vector3.zero;
            if (Time.time >= _partnerAt)
            {
                _partnerAt = Time.time + 0.5f;
                GameObject g = Shoulder.ModelOf(_owner, !_munin);
                ShoulderLife l = g != null ? g.GetComponent<ShoulderLife>() : null;
                _partner = l != null ? l.Motor : null;
            }
            if (_partner == null || !_partner.Airborne || _partner.Body == null || _partner.Body.Final) return Vector3.zero;
            Vector3 d = _t.position - _partner._t.position;
            float m = d.magnitude;
            if (m >= PairGap) return Vector3.zero;
            d = m > 0.01f ? d / m : (_munin ? -1f : 1f) * _t.right;
            float k = 1f - m / PairGap;
            return (d * PairPush + Vector3.up * (_munin ? -0.4f : 0.4f)) * k;
        }

        // ---------------------------------------------------------------- the wind
        // In a strong wind its way is thrown about by gusts along the wind and up and down, it beats more and glides less
        // (FlightBody.Gust) and rolls a little. Not by your shoulder. The wind is the game's own, computed on every
        // machine anyway (grass, trees, waves): read here, nothing sent; once every WindEvery s for all ravens together,
        // eased from one reading to the next (the wind turns slowly).
        internal static float WindEffect = 1f, WindFrom = 0.35f, GustMetres = 2.4f, WindEvery = 2f;
        private static float _windAt = -100f, _windFrom, _windTo;
        private static Vector3 _windDir = Vector3.forward;
        internal static float Wind
        {
            get
            {
                if (Time.time >= _windAt + WindEvery)
                {
                    _windFrom = _windAt < 0f ? 0f : _windTo;
                    _windAt = Time.time;
                    try { if (EnvMan.instance != null) { _windTo = EnvMan.instance.GetWindIntensity(); _windDir = EnvMan.instance.GetWindDir(); } else _windTo = 0f; }
                    catch { _windTo = 0f; }
                }
                float u = Mathf.Clamp01((Time.time - _windAt) / Mathf.Max(0.1f, WindEvery));
                return Mathf.Lerp(_windFrom, _windTo, u) * WindEffect;
            }
        }
        private Vector3 Gusts(float dt)
        {
            float gust = Mathf.Clamp01((Wind - WindFrom) / Mathf.Max(0.05f, 1f - WindFrom));
            if (_owner != null) gust *= Mathf.Clamp01(((_t.position - _owner.transform.position).magnitude - 1.5f) / 3f);
            Body.Gust = gust;
            if (gust <= 0.01f) return Vector3.zero;
            float s0 = _munin ? 7.7f : 2.3f;
            float along = Mathf.PerlinNoise(_clock * 0.8f, s0) - 0.5f, lift = Mathf.PerlinNoise(s0, _clock * 1.1f) - 0.5f;
            Vector3 w = new Vector3(_windDir.x, 0f, _windDir.z);
            if (w.sqrMagnitude > 0.0001f) w.Normalize();
            return (w * along * GustMetres + Vector3.up * lift * GustMetres * 0.5f) * gust;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ---------------------------------------------------------------- the flight director
        // «Режисер польоту»: which flight template the bird flies now. A new situation: a pick at once (or none);
        // then now and then (FlightTemplates.SwitchMin..Max s) it may keep it, take another or fly plain. The final
        // approach and the landing go without any. The wings take a pick in at a stroke's top (ShoulderLife).
        private void Direct()
        {
            TemplateMix mx = _life != null ? _life.Mix : null;
            if (mx == null) return;
            TrackDive();
            // the last way in: this landing's own template (or plain), picked unlike the other raven's (Landings)
            if (Body != null && Body.Final && LandNow != null && Plan != "перегляд") { if (_sit != -3) { _sit = -3; mx.Request(LandNow.Arrive); } return; }
            FlightTemplate forced = Plan == "перегляд" ? FlightTemplates.PreviewForce : null;
            int sit = FlightTemplates.SitOf(this);
            bool diveTest = Plan == "пікірування" && _owner != null && ReferenceEquals(_owner, Player.m_localPlayer) && FlightTemplates.DiveTestName.Length > 0;
            if (diveTest) { forced = FlightTemplates.Find(FlightTemplates.DiveTestName); sit = (int)FlightSit.Dive; }
            if (forced == null && FlightTemplates.ForceName.Length > 0 && _owner != null && ReferenceEquals(_owner, Player.m_localPlayer)) forced = FlightTemplates.Find(FlightTemplates.ForceName);
            if (forced != null && sit >= 0) { mx.Request(forced); _sit = sit; return; }
            if (sit != _sit)
            {
                _sit = sit;
                mx.Request(FlightTemplates.Pick(sit, _munin, null));
                _nextPick = _clock + Random.Range(FlightTemplates.SwitchMin, FlightTemplates.SwitchMax) * Char().SwitchMul;
                return;
            }
            if (sit < 0 || _clock < _nextPick) return;
            FlightCharacter ch = Char();
            _nextPick = _clock + Random.Range(FlightTemplates.SwitchMin, FlightTemplates.SwitchMax) * ch.SwitchMul; // Hugin keeps a way of flying long, Munin changes it often
            if (Random.value < Mathf.Clamp01(FlightTemplates.KeepChance + ch.KeepAdd)) return; // it keeps what it has
            FlightTemplate next = FlightTemplates.Pick(sit, _munin, mx.Wanted);
            if (next != null || sit != (int)FlightSit.Dive) mx.Request(next);
        }

        // its own style with the template's motion on it (one kept object, no garbage)
        private FlightStyle Effective()
        {
            FlightStyle b = _style ?? Style();
            TemplateMix mx = _life != null ? _life.Mix : null;
            if (mx == null || !mx.Active || DivePoseTest) return b;
            // a template never takes the raven past its character (Hugin never as sharp as Munin)
            FlightCharacter ch = Char();
            float sp = Mathf.Min(mx.Speed, ch.TplSpeed);
            _eff.Cruise = b.Cruise * sp; _eff.Top = b.Top * sp; _eff.Accel = b.Accel * Mathf.Lerp(1f, sp, 0.5f);
            _eff.Turn = b.Turn * Mathf.Min(mx.Turn, ch.TplTurn); _eff.Bank = b.Bank * Mathf.Min(mx.Bank, ch.TplBank);
            _eff.Glide = Mathf.Clamp01(b.Glide * mx.Glide); _eff.Beat = b.Beat * Mathf.Min(mx.Beat, ch.TplBeat);
            return _eff;
        }

        // ---------------------------------------------------------------- keeping clear
        // A few times a second: a sphere cast along its way (how far by its speed) and a look at the ground under it
        // and under the aim; what is in the way pushes it off along the surface and up, eased in and out.
        // The solid world, named as the game names it for its own sight and building checks: ground, rocks, trees,
        // buildings, ships and carts. Not creatures (a strike would stop on its prey's hide), not the game's unseen
        // helpers (blockers for the monsters' paths, the build ghost under the hammer), not dropped items.
        private static int _mask;
        private static readonly RaycastHit[] ProbeHits = new RaycastHit[32];
        internal static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle"); return _mask; } }

        // something solid between two points (the way in to the shoulder by a wall)
        internal static bool Walled(Vector3 a, Vector3 b) { return AvoidOn && Physics.Linecast(a, b, Mask, QueryTriggerInteraction.Ignore); }

        private static bool Probe(Vector3 origin, Vector3 direction, float reach, float radius, Transform bird, Transform owner, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, ProbeHits, reach, Mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider c = ProbeHits[i].collider;
                if (c == null || c.transform.IsChildOf(bird) || (owner != null && c.transform.IsChildOf(owner))) continue;
                // touching already where the sweep starts (distance 0, no point): not in the way ahead, or a bird by a
                // wall would never move again
                if (ProbeHits[i].distance <= 0f && ProbeHits[i].point == Vector3.zero) continue;
                if (ProbeHits[i].distance < best) { best = ProbeHits[i].distance; nearest = ProbeHits[i]; }
            }
            return best < float.MaxValue;
        }

        // A raven never goes under the water: coming down onto it, it may fall only as fast as the height left allows
        // (WaterBrake m/s per metre), so it levels out over the last metres as out of a stoop, no stop in mid-air; the
        // surface is eased back to if something put it lower. Not on the final: the landing on a swimmer's shoulder
        // goes where it aims.
        internal static float WaterClear = 0.3f, WaterBrake = 2.5f;
        private void KeepOutOfWater(float dt)
        {
            if (Body == null || Body.Final || ZoneSystem.instance == null) return;
            float low = ZoneSystem.instance.m_waterLevel + WaterClear;
            Vector3 p = _t.position;
            float h = p.y - low;
            if (Body.Vel.y < 0f) Body.Vel.y = Mathf.Max(Body.Vel.y, -Mathf.Max(0f, h) * WaterBrake);
            if (h < 0f) { p.y = Mathf.MoveTowards(p.y, low, Mathf.Max(1.5f, -h * 6f) * dt); _t.position = p; } // eased up, not snapped (its fall is already stopped above)
        }

        // where it may go this frame: short of what is in the way, then the rest of the move along its surface (as a
        // bird brushes past a wall), if that is clear; 'normal': the surface met (zero: nothing)
        private Vector3 ClearMove(Vector3 from, Vector3 to, out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 move = to - from;
            float distance = move.magnitude;
            if (!AvoidOn || distance < 0.001f) return to;
            if (Body != null && Body.Final) return to; // the landing goes exactly where it aims (a shoulder by a wall, a perch's bar)
            Transform owner = _owner != null ? _owner.transform : null;
            RaycastHit hit;
            if (!Probe(from, move / distance, distance, BodyRadius, _t, owner, out hit)) return to;
            normal = hit.normal;
            Vector3 stop = from + move / distance * Mathf.Max(0f, hit.distance - 0.05f);
            Vector3 rest = Vector3.ProjectOnPlane(to - stop, normal);
            float rl = rest.magnitude;
            if (rl < 0.001f || Probe(stop, rest / rl, rl, BodyRadius, _t, owner, out hit)) return stop;
            return stop + rest;
        }

        internal static float ClearAvoid = 0.25f; // share of the swerving while the way to the aim is clear for its body
        private bool _clearAhead;
        private Vector3 Avoid(Vector3 aim, float weight, float dt, bool pursueTarget)
        {
            if (!AvoidOn || weight <= 0.001f) { _push = Vector3.MoveTowards(_push, Vector3.zero, dt * 6f); return _push; }
            if (Time.time >= _probeAt)
            {
                _probeAt = Time.time + ProbeInterval;
                _pushWant = Vector3.zero;
                Vector3 pos = _t.position;
                Vector3 v = Body.Vel;
                float sp = v.magnitude;
                Vector3 targetDir = (aim - pos).normalized;
                Vector3 dir = pursueTarget && targetDir.sqrMagnitude > 0.5f ? targetDir : sp > 1f ? v / sp : targetDir;
                float reach = Mathf.Clamp(sp * LookAhead, ProbeMinReach, ProbeMaxReach);
                RaycastHit hit;
                if (dir.sqrMagnitude > 0.5f && Probe(pos, dir, reach, ProbeRadius, _t, _owner != null ? _owner.transform : null, out hit))
                {
                    float k = 1f - hit.distance / reach;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                    if (right.sqrMagnitude < 0.01f) right = _t.right;
                    RaycastHit leftHit, rightHit, upHit;
                    bool leftBlocked = Probe(pos, Quaternion.AngleAxis(-35f, Vector3.up) * dir, reach, ProbeRadius, _t, _owner != null ? _owner.transform : null, out leftHit);
                    bool rightBlocked = Probe(pos, Quaternion.AngleAxis(35f, Vector3.up) * dir, reach, ProbeRadius, _t, _owner != null ? _owner.transform : null, out rightHit);
                    bool upBlocked = Probe(pos, Quaternion.AngleAxis(-35f, right) * dir, reach, ProbeRadius, _t, _owner != null ? _owner.transform : null, out upHit);
                    float leftClear = leftBlocked ? leftHit.distance : reach;
                    float rightClear = rightBlocked ? rightHit.distance : reach;
                    float upClear = upBlocked ? upHit.distance : reach;
                    bool preferRight = rightClear > leftClear + 0.15f || (Mathf.Abs(rightClear - leftClear) <= 0.15f && !_munin);
                    Vector3 side = preferRight ? right : -right;
                    Vector3 surface = Vector3.ProjectOnPlane(hit.normal, dir);
                    if (surface.sqrMagnitude < 0.01f) surface = Vector3.up;
                    Vector3 evade;
                    if (pursueTarget && upClear > Mathf.Max(leftClear, rightClear) + 0.5f)
                    {
                        evade = (Vector3.up * 0.8f + side * 0.2f).normalized;
                    }
                    else
                    {
                        evade = (side + surface.normalized * 0.35f + Vector3.up * 0.35f).normalized;
                    }
                    _pushWant += evade * PushStrength * k;
                }
                // the way to the aim clear for the bird itself (its body, not the wide look ahead): it hardly swerves —
                // the wide look sees a door frame in the way of a bird that fits through the door, and swerved it up and
                // over the house
                Vector3 to = aim - pos;
                float dl = to.magnitude;
                RaycastHit block;
                _clearAhead = dl < 0.3f || !Probe(pos, to / dl, Mathf.Min(dl, ProbeMaxReach * 2f), BodyRadius * 1.5f, _t, _owner != null ? _owner.transform : null, out block);
                // the ground (and the sea) under it
                float floor = Floor(pos);
                if (pos.y < floor + GroundClear) _pushWant += Vector3.up * Mathf.Min(PushStrength, (floor + GroundClear - pos.y) * 2f);
                _aimFloor = Floor(aim) + GroundClear;
            }
            _push = Vector3.Lerp(_push, _pushWant * weight * (_clearAhead ? ClearAvoid : 1f), Mathf.Clamp01(dt * 5f));
            Vector3 lift = aim.y < _aimFloor ? Vector3.up * (_aimFloor - aim.y) * weight : Vector3.zero; // an aim in a hillside: over it
            return _push + lift;
        }

        private static float Floor(Vector3 p)
        {
            float y = float.MinValue;
            try { if (ZoneSystem.instance != null) y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(p), ZoneSystem.instance.m_waterLevel); } catch { }
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, GroundClear + 3f, Mask, QueryTriggerInteraction.Ignore)) y = Mathf.Max(y, hit.point.y);
            return y;
        }

        // ---------------------------------------------------------------- saved tuning (config/RavenWatch/flight.txt)
        internal static string FilePath { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "flight.txt"); } }

        private static readonly FlightStyle HuginBuilt = Copy(RavenFlight.Hugin), MuninBuilt = Copy(RavenFlight.Munin);
        private static readonly FlightCharacter HuginCharBuilt = RavenFlight.HuginChar.Copy(), MuninCharBuilt = RavenFlight.MuninChar.Copy();
        private static readonly System.Reflection.FieldInfo[] CharFields = typeof(FlightCharacter).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        private static void PutChar(FlightCharacter to, FlightCharacter from) { foreach (System.Reflection.FieldInfo f in CharFields) f.SetValue(to, f.GetValue(from)); }
        private const float BuiltBlend = 0.6f, BuiltSpread = 0.08f, BuiltNerves = 0.35f, BuiltLook = 0.6f, BuiltPush = 4f, BuiltClear = 1.5f;

        private static FlightStyle Copy(FlightStyle s) { return new FlightStyle { Cruise = s.Cruise, Top = s.Top, Accel = s.Accel, Turn = s.Turn, Bank = s.Bank, Glide = s.Glide, Beat = s.Beat }; }
        private static void Put(FlightStyle to, FlightStyle from) { to.Cruise = from.Cruise; to.Top = from.Top; to.Accel = from.Accel; to.Turn = from.Turn; to.Bank = from.Bank; to.Glide = from.Glide; to.Beat = from.Beat; }

        internal static void ToBuilt()
        {
            Put(RavenFlight.Hugin, HuginBuilt); Put(RavenFlight.Munin, MuninBuilt);
            PutChar(RavenFlight.HuginChar, HuginCharBuilt); PutChar(RavenFlight.MuninChar, MuninCharBuilt); RavenFlight.CharVersion++;
            BlendSeconds = BuiltBlend; Spread = BuiltSpread; NervesWeight = BuiltNerves; AvoidOn = true;
            LookAhead = BuiltLook; PushStrength = BuiltPush; GroundClear = BuiltClear; FlightBody.LaunchSeconds = 1.2f;
            FlightBody.BendLimit = true; FlightBody.Coast = 0.35f;
            Landings.MaxHold = 0.8f; Landings.TogetherChance = 0.25f; Landings.ApartMin = 0.25f; Landings.ApartMax = 0.7f; Landings.LeaveApartMin = 0.12f; Landings.LeaveApartMax = 0.4f; Landings.LandBeatHz = 5f; CrouchLean = 8f; DriveFollow = 7f; DriveAccel = 80f; DriveTurnIn = 0.4f;
            FlightTemplates.On = true; FlightTemplates.SwitchMin = 3f; FlightTemplates.SwitchMax = 8f; FlightTemplates.KeepChance = 0.35f; FlightTemplates.NoneWeight = 1f; FlightTemplates.BlendSeconds = 0.8f;
            ShoulderPin.FlyInUp = 5f; ShoulderPin.FlyInBack = 4f; ShoulderPin.FlyInSide = 3f; ShoulderPin.FlyInSpeed = 7f; ShoulderPin.ArriveWake = 0.3f; ShoulderPin.FlyInSeconds = 1.6f;
            Version++;
        }

        internal static void Save()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            List<string> lines = new List<string> { "# RavenWatch: the ravens' flight (test window, tab «Політ»). Delete the file for the built-in values." };
            foreach (bool m in new[] { false, true })
            {
                FlightStyle s = RavenFlight.For(m);
                string k = m ? "munin." : "hugin.";
                lines.Add(k + "cruise=" + s.Cruise.ToString("0.###", c)); lines.Add(k + "top=" + s.Top.ToString("0.###", c));
                lines.Add(k + "accel=" + s.Accel.ToString("0.###", c)); lines.Add(k + "turn=" + s.Turn.ToString("0.###", c));
                lines.Add(k + "bank=" + s.Bank.ToString("0.###", c)); lines.Add(k + "glide=" + s.Glide.ToString("0.###", c));
                lines.Add(k + "beat=" + s.Beat.ToString("0.###", c));
                FlightCharacter ch = RavenFlight.CharOf(m); // its character: hugin.c.turncap=…
                foreach (System.Reflection.FieldInfo f in CharFields) lines.Add(k + "c." + f.Name.ToLowerInvariant() + "=" + ((float)f.GetValue(ch)).ToString("0.###", c));
            }
            lines.Add("blend=" + BlendSeconds.ToString("0.###", c)); lines.Add("spread=" + Spread.ToString("0.###", c));
            lines.Add("nerves=" + NervesWeight.ToString("0.###", c)); lines.Add("launch=" + FlightBody.LaunchSeconds.ToString("0.###", c));
            lines.Add("avoid=" + (AvoidOn ? "1" : "0")); lines.Add("look=" + LookAhead.ToString("0.###", c));
            lines.Add("push=" + PushStrength.ToString("0.###", c)); lines.Add("clear=" + GroundClear.ToString("0.###", c));
            lines.Add("flyin.up=" + ShoulderPin.FlyInUp.ToString("0.###", c)); lines.Add("flyin.back=" + ShoulderPin.FlyInBack.ToString("0.###", c));
            lines.Add("flyin.side=" + ShoulderPin.FlyInSide.ToString("0.###", c)); lines.Add("flyin.speed=" + ShoulderPin.FlyInSpeed.ToString("0.###", c));
            lines.Add("tpl.on=" + (FlightTemplates.On ? "1" : "0")); lines.Add("tpl.min=" + FlightTemplates.SwitchMin.ToString("0.###", c)); lines.Add("tpl.max=" + FlightTemplates.SwitchMax.ToString("0.###", c));
            lines.Add("tpl.keep=" + FlightTemplates.KeepChance.ToString("0.###", c)); lines.Add("tpl.none=" + FlightTemplates.NoneWeight.ToString("0.###", c)); lines.Add("tpl.blend=" + FlightTemplates.BlendSeconds.ToString("0.###", c));
            lines.Add("bend=" + (FlightBody.BendLimit ? "1" : "0")); lines.Add("coast=" + FlightBody.Coast.ToString("0.###", c));
            lines.Add("land.maxhold=" + Landings.MaxHold.ToString("0.###", c)); lines.Add("land.together=" + Landings.TogetherChance.ToString("0.###", c));
            lines.Add("land.apartmin=" + Landings.ApartMin.ToString("0.###", c)); lines.Add("land.apartmax=" + Landings.ApartMax.ToString("0.###", c));
            lines.Add("leave.apartmin=" + Landings.LeaveApartMin.ToString("0.###", c)); lines.Add("leave.apartmax=" + Landings.LeaveApartMax.ToString("0.###", c));
            lines.Add("land.beathz=" + Landings.LandBeatHz.ToString("0.###", c)); lines.Add("takeoff.lean=" + CrouchLean.ToString("0.###", c));
            lines.Add("drive.follow=" + DriveFollow.ToString("0.###", c)); lines.Add("drive.accel=" + DriveAccel.ToString("0.###", c)); lines.Add("drive.turnin=" + DriveTurnIn.ToString("0.###", c));
            lines.Add("flyin.wake=" + ShoulderPin.ArriveWake.ToString("0.###", c)); lines.Add("flyin.pace=" + ShoulderPin.FlyInSeconds.ToString("0.###", c));
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, lines.ToArray());
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven flight tuning saved: " + string.Join(" ", lines.GetRange(1, lines.Count - 1).ToArray()));
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    float v;
                    if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    FlightStyle s = key.StartsWith("munin.") ? RavenFlight.Munin : key.StartsWith("hugin.") ? RavenFlight.Hugin : null;
                    string k = s != null ? key.Substring(6) : key;
                    if (s != null && k.StartsWith("c."))
                    {
                        FlightCharacter ch = RavenFlight.CharOf(s == RavenFlight.Munin);
                        foreach (System.Reflection.FieldInfo f in CharFields) if (f.Name.ToLowerInvariant() == k.Substring(2)) f.SetValue(ch, v);
                        continue;
                    }
                    if (s != null)
                    {
                        switch (k)
                        {
                            case "cruise": s.Cruise = v; break; case "top": s.Top = v; break; case "accel": s.Accel = v; break;
                            case "turn": s.Turn = v; break; case "bank": s.Bank = v; break; case "glide": s.Glide = v; break; case "beat": s.Beat = v; break;
                        }
                        continue;
                    }
                    switch (k)
                    {
                        case "blend": BlendSeconds = v; break; case "spread": Spread = v; break; case "nerves": NervesWeight = v; break;
                        case "launch": FlightBody.LaunchSeconds = v; break; case "avoid": AvoidOn = v > 0.5f; break; case "look": LookAhead = v; break;
                        case "push": PushStrength = v; break; case "clear": GroundClear = v; break;
                        case "flyin.up": ShoulderPin.FlyInUp = v; break; case "flyin.back": ShoulderPin.FlyInBack = v; break; case "flyin.side": ShoulderPin.FlyInSide = v; break;
                        case "tpl.on": FlightTemplates.On = v > 0.5f; break; case "tpl.min": FlightTemplates.SwitchMin = v; break; case "tpl.max": FlightTemplates.SwitchMax = v; break;
                        case "tpl.keep": FlightTemplates.KeepChance = v; break; case "tpl.none": FlightTemplates.NoneWeight = v; break; case "tpl.blend": FlightTemplates.BlendSeconds = v; break;
                        case "bend": FlightBody.BendLimit = v > 0.5f; break; case "coast": FlightBody.Coast = Mathf.Clamp(v, 0.05f, 1f); break;
                        case "land.maxhold": Landings.MaxHold = v; break; case "land.together": Landings.TogetherChance = v; break;
                        case "land.apartmin": Landings.ApartMin = v; break; case "land.apartmax": Landings.ApartMax = v; break;
                        case "leave.apartmin": Landings.LeaveApartMin = v; break; case "leave.apartmax": Landings.LeaveApartMax = v; break;
                        case "land.beathz": Landings.LandBeatHz = v; break; case "takeoff.lean": CrouchLean = v; break;
                        case "drive.follow": DriveFollow = v; break; case "drive.accel": DriveAccel = v; break; case "drive.turnin": DriveTurnIn = v; break;
                        case "flyin.speed": ShoulderPin.FlyInSpeed = v; break; case "flyin.wake": ShoulderPin.ArriveWake = v; break; case "flyin.pace": ShoulderPin.FlyInSeconds = v; break;
                    }
                }
                Version++; RavenFlight.CharVersion++;
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven flight tuning loaded from flight.txt");
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven flight tuning unreadable: " + e.Message); }
        }
    }
}
#endif

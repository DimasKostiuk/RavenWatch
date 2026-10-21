#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a raven in the air wants (a «поведінка»): only where to and how fast. The motor (RavenMotor) does the
    // flying: speed, turns, easing from the last aim, keeping clear. Each plan is a small class; the commands for
    // the ravens will be plans too. One set lives on each shoulder raven (ShoulderPin), so a plan may keep state.
    internal abstract class FlightPlan
    {
        internal abstract string Name { get; }
        internal abstract void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt);
        internal virtual Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.Player.transform.position + facing * (Vector3.forward * 5f + Vector3.up * 4f); }
        internal virtual void Begin() { } // a new flight on this plan: its own state afresh
        protected static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        // the ground (or the sea) under a point
        protected static float Ground(Vector3 at)
        {
            float g = at.y - 20f;
            try { if (ZoneSystem.instance != null) g = Mathf.Max(ZoneSystem.instance.GetGroundHeight(at), ZoneSystem.instance.m_waterLevel); } catch { }
            return g;
        }
    }

    // flying along behind you: a little behind and above its own shoulder, drifting; it speeds up the farther it has
    // fallen behind (gently, with the body's own inertia), facing the way it flies. You stand: a raven cannot hang
    // in the air for minutes, so it goes slowly round you, low and close, and back behind you once you are off.
    internal sealed class FollowPlan : FlightPlan
    {
        private float _still; // 0 → 1: how long you have stood (eased, both ways)
        internal static float StillRadiusHugin = 2.6f, StillRadiusMunin = 3.4f, StillSpeedHugin = 4.2f, StillSpeedMunin = 5f;
        internal override string Name { get { return "слідом"; } }
        internal override void Begin() { _still = 0f; }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return Trail(pin, facing); }
        private static Vector3 Trail(ShoulderPin pin, Quaternion facing)
        {
            float t = Time.time + pin.Ring;
            Vector3 drift = facing * new Vector3(Mathf.Sin(t * 0.4f) * 0.8f, Mathf.Sin(t * 0.55f) * 0.5f, Mathf.Sin(t * 0.3f) * 0.6f);
            bool wings = OdinWings.Shown(pin.Player); // «Крила Одіна»: right at your back, a little apart
            Vector3 side = facing * (wings ? new Vector3(pin.Munin ? -0.9f : 0.9f, 0.4f, -1.6f) : new Vector3(pin.Munin ? -1.2f : 1.2f, 0f, 0f));
            return pin.HoverPoint(facing) + side + facing * new Vector3(0f, ShoulderPin.OutUp, -ShoulderPin.OutBack) + drift;
        }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            float run = carry.magnitude;
            bool still = run < 1.5f && !OdinWings.Shown(pin.Player); // (hanging in the air on the wings: they stay at your back)
            _still = Mathf.MoveTowards(_still, still ? 1f : 0f, dt * (still ? 0.6f : 1.5f));
            Vector3 trail = Trail(pin, facing);
            float behind = Vector3.Distance(pin.transform.position, trail);
            float speed = Mathf.Max(4f, run * 1.15f + 1.5f) * (1f + Mathf.Clamp01((behind - 3f) / 8f) * 0.8f);
            if (_still > 0.001f)
            {
                // round you, each on its own ring and its own way round (so they do not meet head on)
                bool munin = pin.Munin;
                Vector3 mid = pin.Player.transform.position + Vector3.up * (munin ? 2.9f : 2.4f);
                Vector3 round = CirclePlan.Ring(pin.transform.position, mid, munin ? StillRadiusMunin : StillRadiusHugin, 40f, munin ? -1f : 1f);
                float k = S(_still);
                trail = Vector3.Lerp(trail, round, k);
                speed = Mathf.Lerp(speed, munin ? StillSpeedMunin : StillSpeedHugin, k);
            }
            m.Steer(trail, speed, _still < 0.5f, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }
    }

    // circling above you: it chases a point ahead of itself on a ring that slowly breathes, riding up and down;
    // Hugin wide and smooth, Munin quicker and tighter; over a ship under sail wider and higher, over the mast
    internal sealed class CirclePlan : FlightPlan
    {
        internal override string Name { get { return "коло"; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return Aim(pin); }
        // the ring's width (m): close round you / over the sea; never so tight that it cannot hold the turn
        internal static float RingMin = 3f, RingMax = 5f, WideMin = 5f, WideMax = 8f;
        private static Vector3 Aim(ShoulderPin pin)
        {
            bool munin = pin.Munin;
            float t = Time.time + pin.Ring;
            bool wide = Outing.IsWide(pin.OutMode);
            Vector3 mid = pin.Player.transform.position + Vector3.up * (wide ? (munin ? 5.5f : 6.5f) : (munin ? 3.6f : 4.2f));
            float r = wide ? Mathf.Lerp(WideMin, WideMax, Mathf.PerlinNoise(t * (munin ? 0.12f : 0.07f), pin.Ring)) : Mathf.Lerp(RingMin, RingMax, Mathf.PerlinNoise(t * (munin ? 0.18f : 0.1f), pin.Ring));
            float dir = Outing.Clockwise(pin.OutMode) ? 1f : -1f;
            float h = Mathf.Sin(t * (munin ? 0.7f : 0.45f)) * (munin ? 0.6f : 1f) + Mathf.Sin(t * 1.9f) * 0.15f;
            return Ring(pin.transform.position, mid, r, munin ? 40f : 32f, dir) + Vector3.up * h;
        }
        // a point 'lead' degrees ahead of where the bird is, on a ring of 'r' round 'mid' (dir 1 clockwise from above)
        internal static Vector3 Ring(Vector3 at, Vector3 mid, float r, float lead, float dir)
        {
            Vector3 rel = at - mid;
            float a = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
            return mid + Quaternion.AngleAxis(a + dir * lead, Vector3.up) * (Vector3.forward * r);
        }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            bool munin = pin.Munin;
            Vector3 aim = Aim(pin);
            // (no faster than it can turn round the ring: the motor's bend rule, FlightBody.BendSpeed)
            m.Steer(aim, m.Body.Style.Cruise * (munin ? 0.8f : 0.65f), false, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }
    }

    // scouting on the road: a quick dart ahead along your way, high, looking about; there a wide turn and straight
    // back (a raven does not hang in the air out there), then along with you until home (the owner's game ends it
    // and the raven says what it saw)
    internal sealed class ScoutPlan : FlightPlan
    {
        private bool _back;
        private readonly FollowPlan _along = new FollowPlan();
        internal override string Name { get { return "розвідка"; } }
        internal override void Begin() { _back = false; _along.Begin(); }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return Target(pin, facing); }
        private Vector3 Target(ShoulderPin pin, Quaternion facing)
        {
            float t = Time.time + pin.Ring;
            return pin.Player.transform.position + facing * new Vector3(Mathf.Sin(t * 0.8f) * 3f, 0f, ShoulderPin.ScoutAhead) + Vector3.up * ShoulderPin.ScoutUp;
        }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (!_back)
            {
                Vector3 aim = Target(pin, facing);
                float left = m.Steer(aim, m.Body.Style.Top, false, 0f, dt, carry, 1f);
                m.Finish(dt, 0f);
                float ahead = Vector3.Dot(pin.transform.position - pin.Player.transform.position, facing * Vector3.forward);
                if (left < 4f || ahead > ShoulderPin.ScoutAhead) _back = true; // there (or past it): round and back
                return;
            }
            Vector3 me = pin.HoverPoint(facing) + Vector3.up * 1.5f;
            if (Vector3.Distance(pin.transform.position, me) < 8f) { _along.Step(m, pin, facing, carry, dt); return; }
            m.Steer(me, m.Body.Style.Top, false, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }
    }

    // Scouting on your word (Orders): off the way you look, nearly straight (a little wander, as a bird keeps a line),
    // high over the ground, as far as its training lets it fly; there it looks about (the owner's game marks what is
    // ahead and ends the order) and comes home round (the pin's way home, the motor swinging it about). A copy of
    // another player's raven goes along with them meanwhile, till the order is over there too.
    internal sealed class ScoutRunPlan : FlightPlan
    {
        internal static float Up = 6f, Wander = 2.5f, Carrot = 12f, Pace = 0.85f; // m over the ground, ± m off the line, m ahead it aims, × its top speed
        private readonly FollowPlan _after = new FollowPlan();
        private Vector3 _from, _dir, _side;
        private float _reach;
        private bool _set, _there;
        internal override string Name { get { return Orders.Plans[Orders.Scout]; } }
        internal override void Begin() { _set = false; _there = false; _after.Begin(); }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.Player.transform.position + facing * new Vector3(0f, Up, 10f); }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (!_set)
            {
                _set = true;
                _from = pin.Player.transform.position;
                _dir = facing * Vector3.forward; _dir.y = 0f; _dir = _dir.sqrMagnitude > 0.0001f ? _dir.normalized : Vector3.forward;
                _side = Vector3.Cross(Vector3.up, _dir);
                _reach = Orders.ScoutReach(pin.Player, pin.Munin);
            }
            if (_there) { m.SetPlan(_after.Name); _after.Step(m, pin, facing, carry, dt); return; }
            Vector3 at = pin.transform.position;
            float along = Vector3.Dot(at - _from, _dir);
            float t = Time.time + pin.Ring;
            // a point a little ahead on its line, the line itself wandering a little (slow noise: no zigzag)
            Vector3 aim = _from + _dir * Mathf.Min(_reach, Mathf.Max(0f, along) + Carrot) + _side * ((Mathf.PerlinNoise(t * 0.15f, pin.Ring) - 0.5f) * 2f * Wander);
            aim.y = Mathf.Max(_from.y, Ground(aim)) + Up + (Mathf.PerlinNoise(pin.Ring, t * 0.2f) - 0.5f) * 1.5f;
            m.Steer(aim, m.Body != null ? m.Body.Style.Top * Pace : 8f, false, 0f, dt, Vector3.zero, 1f);
            m.Finish(dt, 0f);
            if (along >= _reach - 1.5f)
            {
                _there = true;
                Orders.ScoutTurned(pin.Player, pin.Munin); // (the owner's game: it looks about, the order is over)
            }
        }
    }

    // «Принести» (RavenFetch): over to the thing you looked at, a little above it; then down onto it, braking, and a
    // peck — on the owner's game that takes one piece (RavenFetch.Grab), and the order is over: home it flies with it
    // in its beak (the shoulder's way home and its landing). Every game flies it to the same spot (the player's ZDO).
    internal sealed class FetchPlan : FlightPlan
    {
        internal static float Above = 1.6f, Low = 0.35f, Down = 2.5f, Peck = 2.5f;
        private int _phase;
        private float _lowAt;
        private Vector3 _to;
        private bool _toSet;
        internal override string Name { get { return Orders.Plans[Orders.Fetch]; } }
        internal override void Begin() { _phase = 0; _toSet = false; }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return RavenFetch.Target(pin.Player, pin.Munin) + Vector3.up * (Above + 2f); }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (!_toSet) { _to = RavenFetch.Target(pin.Player, pin.Munin); _toSet = true; } // (written with the order: there already)
            Vector3 t = _to, at = pin.transform.position;
            if (_phase == 0)
            {
                Vector3 over = t + Vector3.up * Above;
                m.Steer(over, m.Body != null ? m.Body.Style.Cruise : 8f, true, 0f, dt, Vector3.zero, 1f);
                m.Finish(dt, 0f);
                if (Vector3.Distance(at, over) < 1.5f) { _phase = 1; _lowAt = Time.time; }
                return;
            }
            // down onto it, slowly, wings flared; a moment there, the peck
            Vector3 low = t + Vector3.up * Low;
            m.Steer(low, Down, true, 0f, dt, Vector3.zero, 0.3f);
            m.Finish(dt, 0.6f);
            if (_phase == 1 && (Vector3.Distance(at, low) < 0.6f || Time.time - _lowAt > Peck) && RavenFetch.Grab(pin.Player, pin.Munin)) _phase = 2;
        }
    }

    // The hunt (Outing.Hunt): Munin after one of the game's birds — at its top speed, aiming a little ahead of where the
    // bird is going; the bird gone (or never found on this game): it circles over you until the outing is over.
    internal sealed class HuntPlan : FlightPlan
    {
        internal static float Lead = 0.5f; // s ahead of the bird it aims
        private Transform _prey;
        private Vector3 _was;
        private bool _looked, _wasSet;
        internal override string Name { get { return "полювання"; } }
        internal override void Begin() { _looked = false; _wasSet = false; _prey = null; }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { Transform t = Outing.Prey(pin.Player, pin.Munin); return t != null ? t.position : pin.Player.transform.position + Vector3.up * 6f; }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (!_looked) { _looked = true; _prey = Outing.Prey(pin.Player, pin.Munin); }
            Vector3 aim;
            if (_prey != null)
            {
                Vector3 now = _prey.position;
                Vector3 vel = _wasSet && dt > 0f ? (now - _was) / dt : Vector3.zero;
                _was = now; _wasSet = true;
                aim = now + Vector3.ClampMagnitude(vel, 20f) * Lead;
            }
            else aim = pin.Player.transform.position + Vector3.up * 6f + facing * (Quaternion.Euler(0f, Time.time * 40f, 0f) * Vector3.forward) * 5f;
            m.Steer(aim, m.Body != null ? m.Body.Style.Top : 12f, false, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }
    }

    // Hanging over the shoulder (an emote): chasing its spot there, with your pace. Now and then a few strokes to
    // another spot by the shoulder; the spot itself glides there (no jump of the target), and the bird keeps facing
    // your way. Still, it lives: looks about, rolls on the air (AliveInAir).
    internal sealed class HoverPlan : FlightPlan
    {
        private Vector3 _spot, _spotNow;
        private float _spotNext;
        private Quaternion _airRot;
        private bool _airSet;

        internal override string Name { get { return "танець"; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.HoverPoint(facing); }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (Time.time >= _spotNext)
            {
                _spotNext = Time.time + Random.Range(3f, 6f);
                float out_ = pin.Munin ? -1f : 1f;
                _spot = new Vector3(Random.Range(-0.15f, 0.35f) * out_, Random.Range(-0.1f, 0.35f), Random.Range(-0.4f, 0.3f));
            }
            _spotNow = Vector3.MoveTowards(_spotNow, _spot, dt * 0.35f);
            m.Steer(pin.HoverPoint(facing) + facing * _spotNow, 3f, true, 0f, dt, carry, 1f);
            m.Body.FaceHint(pin.Player.transform.forward, 1f);
            m.Finish(dt, 0f);
            if (m.Holding) { _airSet = false; return; } // still crouching to take off: the hold places it
            float rel = (m.Body.Vel - carry).magnitude;
            AliveInAir(pin, facing, 1f - Mathf.Clamp01((rel - 0.5f) / 2f));
        }

        // the whole bird, not only its head: turns slowly where it looks, rolls a little on the air, and catches up
        // with your turns a moment late (each raven in its own rhythm)
        private void AliveInAir(ShoulderPin pin, Quaternion facing, float k)
        {
            if (k <= 0.001f) { _airSet = false; return; }
            float t = Time.time, seed = pin.Munin ? 3.7f : 0f;
            float yaw = (Mathf.PerlinNoise(t * 0.18f, seed) - 0.5f) * 70f;
            float roll = Mathf.Sin(t * 0.9f + seed) * 5f + Mathf.Sin(t * 2.3f + seed * 1.3f) * 2f;
            float pitch = Mathf.Sin(t * (ShoulderLife.HoverVanilla ? ShoulderLife.BeatHz : ShoulderLife.HoverHz) * Mathf.PI * 2f + seed) * 2.5f;
            Quaternion want = facing * Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.Euler(Shoulder.Rotation);
            _airRot = _airSet ? Quaternion.Slerp(_airRot, want, Mathf.Clamp01(Time.deltaTime * 3f)) : want;
            _airSet = true;
            pin.transform.rotation = Quaternion.Slerp(pin.transform.rotation, _airRot, k);
        }
    }

    // A trick (the orders): a «фігура». First up to its start by the motor (none for the ones flown round you), then
    // the figure flown by hand (RavenMotor.Drive): each figure is a way through the air in time (Pos), the bird facing
    // along it, banked by how it turns (or turned as the figure says: over the top of a loop, the roll). The way in is
    // eased by the motor (RavenMotor.Drive follows the way as a body, from how the bird came), so nothing jumps; every figure ends
    // flying level, and the motor takes it on from there at that speed. A figure begun is flown to its end, even when
    // the order is over meanwhile (ShoulderPin). The fixed ones go along with you (your pace added).
    //   Roll       бочка: a barrel roll along your way
    //   Loop       петля: a full circle up and over
    //   Dive       пікірування: up high, steeply down ahead of you and out level just over the ground
    //   Eight      вісімка: a figure eight over you, banked into each turn
    //   Candle     свічка: a tight spiral up, then a long glide down round a wide one
    //   Leaf       падаючий лист: falls swinging side to side like a leaf, then catches the air
    //   Immelmann  імельман: half a loop up, a half roll on top: back the way it came, higher
    //   Hammer     гірка: straight up till it stops, a turn on the spot, down again and out back to you
    //   Skim       бриючий проліт: low over the ground ahead, a climbing turn, back to you
    //   Waves      хвилі: round you low, diving and climbing as on a swing
    internal sealed class TrickPlan : FlightPlan
    {
        private readonly int _kind;
        private readonly FollowPlan _after = new FollowPlan();
        private int _phase;          // 0 to the start, 1 the figure, 2 done
        private float _t0, _t, _tf;  // when it began; seconds since the figure began; how far into the figure (its own time)
        private Vector3 _o, _f, _r, _drift, _low, _carry;
        private float _side, _a0, _rate0;
        private readonly Spline _path = new Spline();
        // the way in: the figure is drawn starting at the bird's own speed and direction, eased onto its line
        // (gap · t · e^(−t/τ): from the bird's velocity at t 0 back onto the drawn way, no pull spike, nothing shifted)
        internal static float EntryTau = 0.6f, EntryMax = 12f;
        private Vector3 _gap;
        // a look ahead along its own way now and then: something there and the figure ends at once, still flying
        internal static float LookAheadSeconds = 0.6f;
        private float _lookAt;
        internal TrickPlan(int kind) { _kind = kind; }
        internal override string Name { get { return Orders.Plans[_kind]; } }
        internal bool InFigure { get { return _phase == 1; } }
        internal float Left { get { return _phase == 1 ? Mathf.Max(0f, _len - _tf) + 0.3f : 0f; } } // s of the figure still to fly (a little over: the warp)
        internal override void Begin() { _phase = 0; _t0 = Time.time; _after.Begin(); }
        // the figure's own time starts at the bird's pace (no sudden speed-up) and reaches full pace over this long
        internal static float Warp = 0.6f;

        internal static float RollLength = 27f, LoopRadius = 3.5f, DiveUp = 7f, DiveAhead = 9f;
        // the barrel: a stretch level first and after; between, a wide helix round a line along your way, flown at an
        // even pace (eased in and out over RollEase of it, never faster than ×1.43), its back always to the line (as
        // its lift holds it on the helix): no twist the wrong way at the sides
        private const float RollFrom = 0.1f, RollTo = 0.9f, RollRing = 1.8f, RollEase = 0.3f;
        private float RollTurn(float u) { return Ramp((u - _rollFrom) / (_rollTo - _rollFrom), RollEase); }
        // 0 → 1 at an even pace, eased in over the first share 'a' and out over the last (speed: smoothstep, no jolt)
        private static float Ramp(float x, float a)
        {
            x = Mathf.Clamp01(x);
            float total = 1f - a;
            if (x < a) { float y = x / a; return a * (y * y * y - 0.5f * y * y * y * y) / total; }
            if (x > 1f - a) { float y = (1f - x) / a; return 1f - a * (y * y * y - 0.5f * y * y * y * y) / total; }
            return (x - 0.5f * a) / total;
        }
        // this time's own touch (rolled once as the figure begins, its character's TrickVary): the barrel's stretches
        // before and after and which way it rolls, the loop's lean; a calm raven keeps to its own way more often
        private float _rollFrom = RollFrom, _rollTo = RollTo, _spin = 1f;
        private Vector3 _u = Vector3.up;
        // «Гірка» (4 s): quarter circle up | slowing climb 2.9 m | the turn on the spot | the fall | quarter out (shares)
        private const float HammerRise = 2.9f, HammerQ = 0.196f, HammerUp = 0.438f, HammerTurn = 0.563f, HammerFall = 0.805f;
        // «Імельман» (4.2 s, even pace): a straight run in | half a loop up | a half roll on top | on back the way it came
        private const float ImIn = 0.15f, ImTop = 0.6f, ImRoll = 0.85f, ImRad = 4f;

        // how long each figure flies (s)
        internal static float Length(int kind)
        {
            switch (kind)
            {
                case Orders.Roll: return 4.5f; // ~6 m/s along, the helix 1.8 m round
                case Orders.Loop: return 2.8f;
                case Orders.Dive: return 1.6f;
                case Orders.Eight: return 3.4f;
                case Orders.Candle: return 5f;
                case Orders.Leaf: return 4.4f;
                case Orders.Immelmann: return 4.2f;
                case Orders.Hammer: return 4f; // an even 6 m/s in and out of the stop (HammerQ…)
                case Orders.Skim: return 7f; // ~63 m: out low, round, back
                case Orders.Waves: return 9f; // (the user: slow, a swing, not a jitter)
                default: return 2f;
            }
        }
        // round you, from wherever it is (no way up to a start first)
        private static bool Attached(int kind) { return kind == Orders.Waves; }
        // s at most to get to the start (a high one takes longer)
        internal static float StartTime(int kind) { return Attached(kind) ? 0f : kind == Orders.Dive || kind == Orders.Leaf ? 3f : 1.8f; }
        // the whole order: the way up, the figure, a little after
        internal static float Seconds(int kind) { return StartTime(kind) + Length(kind) * TempoMax + 0.4f; }
        // the slowest figure tempo of the two ravens (the order lasts that long; a figure begun is flown to its end anyway)
        private static float TempoMax { get { return Mathf.Max(1f, Mathf.Max(RavenFlight.HuginChar.TrickTempo, RavenFlight.MuninChar.TrickTempo)); } }
        // each raven its own figures (its character): Hugin wider and slower, Munin tighter and quicker; the same pace
        // through the air (size and length go together). Not the dive (timed in seconds, down to the ground), not the
        // skim (over the ground); the waves round you only in tempo.
        private float _len = 2f, _size = 1f;
        private bool Sized { get { return _kind != Orders.Dive && _kind != Orders.Skim && !Attached(_kind); } }
        private bool Timed { get { return _kind != Orders.Dive; } }

        private Vector3 StartAt(Player p, Quaternion facing, float side)
        {
            Vector3 at = p.transform.position;
            switch (_kind)
            {
                case Orders.Dive: return at + Vector3.up * DiveUp + facing * new Vector3(side * 1.5f, 0f, 2f);
                case Orders.Leaf: return at + Vector3.up * 8f + facing * new Vector3(side * 1.5f, 0f, 3f);
                case Orders.Candle: return at + Vector3.up * 2.5f + facing * new Vector3(side * 1.5f, 0f, 2f);
                case Orders.Eight: return at + Vector3.up * 3.5f + facing * new Vector3(side * 0.5f, 0f, 1f);
                case Orders.Skim: return at + Vector3.up * 3f + facing * new Vector3(side * 1f, 0f, 1f);
                default: return at + Vector3.up * 3f + facing * new Vector3(side * 1.2f, 0f, 2f);
            }
        }

        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return StartAt(pin.Player, facing, pin.Munin ? -1f : 1f); }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            Player p = pin.Player;
            float side = pin.Munin ? -1f : 1f;
            if (_phase == 0)
            {
                bool go = Attached(_kind);
                if (!go)
                {
                    Vector3 start = StartAt(p, facing, side);
                    float up = start.y - pin.transform.position.y;
                    // flown through, not braked onto: the figure takes the bird on at its pace
                    float left = m.Steer(start, Mathf.Max(up > 3f ? 8f : 6f, carry.magnitude + 4f), false, 1f, dt, carry, 1f);
                    m.Finish(dt, 0f);
                    go = left < Mathf.Max(1.2f, m.Body != null ? m.Body.Speed * 0.2f : 0f) || Time.time - _t0 > StartTime(_kind);
                }
                if (go) Start(m, pin, facing, side, carry);
                if (!Attached(_kind)) return;
            }
            if (_phase == 2) { m.SetPlan(_after.Name); _after.Step(m, pin, facing, carry, dt); return; }
            _t += dt;
            _carry = carry;
            _tf += dt * (_rate0 >= 1f ? 1f : Mathf.Lerp(_rate0, 1f, S(_t / Warp)));
            if (!Attached(_kind)) _drift += carry * dt; // the figure goes along with you
            float len = _len;
            float t = Mathf.Min(_tf, len);
            // a rock in the skim's way, a tree in the loop's: the figure is over here, at its speed, and the motor swerves
            // on (Avoid) as in any flight: shorter, no stop against it
            if (_tf < len - 0.1f && Time.time >= _lookAt)
            {
                _lookAt = Time.time + 0.1f;
                Vector3 a = pin.transform.position, b1 = Pos(Mathf.Min(len, t + LookAheadSeconds / 3f), pin), b2 = Pos(Mathf.Min(len, t + LookAheadSeconds * 2f / 3f), pin), b3 = Pos(Mathf.Min(len, t + LookAheadSeconds), pin);
                if (!m.WayClear(a, b1) || !m.WayClear(b1, b2) || !m.WayClear(b2, b3))
                {
                    if (AnimDirector.LogOn) AnimDirector.Note(pin.gameObject, "figure " + Name + " cut short: something ahead");
                    _phase = 2; m.SetPlan(_after.Name); _after.Step(m, pin, facing, carry, dt); return;
                }
            }
            Vector3 pos = Pos(t, pin);
            Quaternion look = Look(t, pin, pos);
            // (the way in, from where the bird is, how fast and which way it is turned: the motor, RavenMotor.Drive)
            if (!m.Drive(pos, look, dt, Wings(t / len))) { _phase = 2; return; } // something in the way: the motor flies on from here
            if (_tf >= len) _phase = 2;
        }

        private void Start(RavenMotor m, ShoulderPin pin, Quaternion facing, float side, Vector3 carry)
        {
            _phase = 1; _t = 0f; _tf = 0f; _drift = Vector3.zero; _side = side; _carry = carry; _gap = Vector3.zero; _lookAt = 0f;
            FlightCharacter ch = m.Char();
            _len = Length(_kind) * (Timed ? ch.TrickTempo : 1f);
            _size = Sized ? ch.TrickSize : 1f;
            float v = ch.TrickVary;
            float a = Random.Range(-v, v), pace = Random.Range(-v, v) * 0.5f; // bigger goes with longer (the same pace), a little quicker or slower besides
            if (Sized) _size *= 1f + a;
            if (Timed) _len *= 1f + a + (Sized ? pace : 0f);
            _rollFrom = RollFrom + Random.Range(-0.5f, 0.6f) * v; _rollTo = RollTo + Random.Range(-0.6f, 0.5f) * v;
            _spin = Random.value < Mathf.Clamp01(v * 3f) ? -1f : 1f;
            Transform b = pin.transform;
            _o = b.position;
            Vector3 v0 = m.Body != null ? m.Body.Vel : Vector3.zero;
            _f = facing * Vector3.forward;
            _r = facing * Vector3.right * side; // each raven to its own side (Munin mirrored)
            _u = _kind == Orders.Loop ? Quaternion.AngleAxis(Random.Range(-100f, 100f) * v, _f) * Vector3.up : Vector3.up;
            Vector3 rel = b.position - pin.Player.transform.position;
            _a0 = Mathf.Atan2(rel.x, rel.z);
            if (_kind == Orders.Dive)
            {
                // the dive's bottom and the run out of it: just over the ground (or the sea) ahead, never into it
                _low = _o + _f * DiveAhead;
                float g = Mathf.Max(Ground(_low), Ground(_low + _f * 7f));
                _low.y = g + 1.2f;
            }
            if (_kind == Orders.Skim)
            {
                // low ahead over the ground, a climbing turn to its side, back to you (heights over the ground there)
                _path.Clear();
                _path.Add(_o);
                _path.Add(Over(_o + _f * 5f, 1.4f));
                _path.Add(Over(_o + _f * 12f, 1f));
                _path.Add(Over(_o + _f * 20f, 1f));
                _path.Add(Over(_o + _f * 27f, 1f));
                _path.Add(Over(_o + _f * 31f + _r * 2.5f, 2.5f));
                _path.Add(Over(_o + _f * 28f + _r * 5f, 3.2f));
                _path.Add(Over(_o + _f * 19f + _r * 4f, 2.8f));
                _path.Add(Over(_o + _f * 9f + _r * 2.5f, 2.5f));
                _path.Add(Over(_o + _f * 2f + _r * 1.2f, 3f));
            }
            // the figure's time starts at the bird's own pace (the motor eases where it is and which way it goes)
            Vector3 p0 = Pos(0f, pin), vFig = (Pos(0.05f, pin) - p0) / 0.05f;
            float vf = vFig.magnitude;
            _rate0 = Attached(_kind) || vf < 0.5f ? 1f : Mathf.Clamp(v0.magnitude / vf, 0.35f, 1f);
            // and from its own direction: what the bird has beyond the figure's start (in the figure's time; you go along)
            _gap = Vector3.ClampMagnitude((v0 - carry) / _rate0 - vFig, EntryMax);
        }

        private static Vector3 Over(Vector3 at, float h) { at.y = Ground(at) + h; return at; }


        // ---- where the figure has the bird at t seconds in
        private Vector3 Pos(float t, ShoulderPin pin)
        {
            Vector3 p = Raw(t, pin);
            if (_size != 1f) { Vector3 o = _o + _drift; p = o + (p - o) * _size; } // the figure drawn bigger or smaller round where it began
            return p + _gap * (t * Mathf.Exp(-t / EntryTau)); // the way in (EntryTau)
        }

        private Vector3 Raw(float t, ShoulderPin pin)
        {
            float len = _len, u = Mathf.Clamp01(t / len);
            Vector3 F = _f, R = _r, U = Vector3.up, O = _o + _drift;
            const float Tau = Mathf.PI * 2f;
            switch (_kind)
            {
                case Orders.Roll:
                {
                    // along your way on a helix round a line (a barrel, not on the spot), level before and after
                    float th = Tau * RollTurn(u);
                    return O + F * (RollLength * u) + U * (Mathf.Sin(u * Mathf.PI) * 0.3f) + (R * (-_spin * Mathf.Sin(th)) + U * (1f - Mathf.Cos(th))) * RollRing;
                }
                case Orders.Loop:
                {
                    // nearly even pace all the way round (it no longer stops before and after)
                    float th = Tau * (0.85f * u + 0.15f * S(u));
                    Vector3 L = _u; // (up, leaned a little to a side this time)
                    return O + L * LoopRadius + (F * Mathf.Sin(th) - L * Mathf.Cos(th)) * LoopRadius + F * (2.5f * u);
                }
                case Orders.Dive:
                {
                    const float down = 1.1f;
                    float run = 0.5f, speed = 3f * 3.5f * 1.4f / down; // the speed it comes out of the curve with
                    if (t <= down)
                    {
                        float a = t / down, s = 0.4f * a * a + 0.6f * a; // gathering speed as it falls
                        Vector3 p0 = O, p1 = O + F * 2f, p3 = _low + _drift, p2 = p3 - F * 3.5f;
                        float k = 1f - s;
                        return k * k * k * p0 + 3f * k * k * s * p1 + 3f * k * s * s * p2 + s * s * s * p3;
                    }
                    return _low + _drift + F * (speed * Mathf.Min(run, t - down)); // out level along the ground
                }
                case Orders.Eight:
                {
                    float th = Tau * u, a = 4f;
                    return O + R * (a * Mathf.Sin(th)) + F * (a * 0.5f * Mathf.Sin(2f * th)) + U * (0.4f * Mathf.Sin(2f * th));
                }
                case Orders.Candle:
                {
                    // two tight turns up 12 m, slow at the top; then a wide turn gliding down to where it began
                    float th, r, h;
                    // the turning slows a little towards the top and the glide starts at that same pace (no jump in speed)
                    if (u < 0.45f) { float a = u / 0.45f; th = Tau * 2f * (a + 0.28f * (a - a * a)); r = 1.6f; h = 12f * S(a); }
                    else { float b = (u - 0.45f) / 0.55f; th = Tau * (2f + 1.1f * (b + 0.6f * (b - b * b))); r = Mathf.Lerp(1.6f, 4f, S(b * 4f)); h = 12f * (1f - S(b)); }
                    Vector3 c = O + R * 1.6f;
                    return c - R * (Mathf.Cos(th) * r) + F * (Mathf.Sin(th) * r) + U * h;
                }
                case Orders.Leaf:
                {
                    // two swings down 5.5 m (growing in, dying away at the end), then it catches the air and flies on,
                    // gathering speed: the fall and the way out meet at the same pace (no jerk where it catches the air)
                    const float fall = 0.8f;
                    float a = Mathf.Min(u, fall) / fall;
                    float env = S(a * 3f) * (1f - S(a)), drop = a * a * (2.64f - 1.64f * a);
                    Vector3 pos = O + R * (1.6f * Mathf.Sin(Tau * 2f * a) * env) + F * (1.2f * a) - U * (5.5f * drop);
                    if (u > fall) { float k = (u - fall) / (1f - fall); pos += F * (0.3f * k + 2.7f * k * k) - U * (0.5f * k * (1f - k)); }
                    return pos;
                }
                case Orders.Immelmann:
                {
                    float pace = Mathf.PI * ImRad / (ImTop - ImIn); // m per share of the figure: one pace all through
                    if (u < ImIn) return O + F * (pace * u);
                    Vector3 c = O + F * (pace * ImIn) + U * ImRad;
                    if (u < ImTop) { float th = Mathf.PI * (u - ImIn) / (ImTop - ImIn); return c + (F * Mathf.Sin(th) - U * Mathf.Cos(th)) * ImRad; }
                    return c + U * ImRad - F * (pace * (u - ImTop));
                }
                case Orders.Hammer:
                {
                    // the parts timed so it goes 6 m/s into the climb and out of the fall (no jump in speed)
                    const float rad = 3f, rise = HammerRise;
                    Vector3 c = O + U * rad;
                    if (u < HammerQ) { float ph = Mathf.PI * 0.5f * (u / HammerQ); return c + (F * Mathf.Sin(ph) - U * Mathf.Cos(ph)) * rad; }
                    if (u < HammerUp) { float b = (u - HammerQ) / (HammerUp - HammerQ); return c + F * rad + U * (rise * (1f - (1f - b) * (1f - b))); } // slowing to a stop
                    if (u < HammerTurn) return c + F * rad + U * rise;                                                                                   // the turn on the spot
                    if (u < HammerFall) { float b = (u - HammerTurn) / (HammerFall - HammerTurn); return c + F * rad + U * (rise * (1f - b * b)); }     // falling back
                    { float ps = Mathf.PI * 0.5f * ((u - HammerFall) / (1f - HammerFall)); return c + (F * Mathf.Cos(ps) - U * Mathf.Sin(ps)) * rad; }  // out level, back to you
                }
                case Orders.Skim:
                    return _path.At(u) + _drift;
                case Orders.Waves:
                {
                    // round you, live: low, diving and climbing three times a turn
                    Vector3 me = pin.Player.transform.position;
                    float th = _a0 + _side * Tau * 1.3f * u;
                    return me + new Vector3(Mathf.Sin(th), 0f, Mathf.Cos(th)) * 3.5f + U * (3f + 1.3f * Mathf.Sin(3f * (th - _a0)));
                }
            }
            return O;
        }

        // ---- how it is turned: along the way, banked by its turn; some figures turn it their own way
        private Quaternion Look(float t, ShoulderPin pin, Vector3 pos)
        {
            float len = _len, u = Mathf.Clamp01(t / len);
            const float h = 0.03f;
            Vector3 a = Pos(Mathf.Max(0f, t - h), pin), b = Pos(Mathf.Min(len, t + h), pin);
            Vector3 tan = b - a;
            if (tan.sqrMagnitude < 1e-6f) tan = _f;
            // you run: it flies the figure and your pace together, so its nose takes your pace in too (never more
            // than half the figure's own, or over the top of a loop above a running owner it would point nowhere)
            if (!Attached(_kind) && _carry.sqrMagnitude > 0.01f)
            {
                float span = Mathf.Min(len, t + h) - Mathf.Max(0f, t - h);
                Vector3 vFig = span > 0.0001f ? tan / span : Vector3.zero;
                tan = vFig + Vector3.ClampMagnitude(_carry, vFig.magnitude * 0.5f);
                if (tan.sqrMagnitude < 1e-6f) tan = _f;
            }
            Vector3 up = Vector3.up;
            bool bank = true;
            Quaternion leaf = Quaternion.identity;
            float leafW = 0f;
            switch (_kind)
            {
                case Orders.Roll:
                {
                    // its back to the helix's line (from the bottom, where that is straight up, round once)
                    float th = Mathf.PI * 2f * RollTurn(u);
                    up = _r * (_spin * Mathf.Sin(th)) + Vector3.up * Mathf.Cos(th);
                    bank = false;
                    break;
                }
                case Orders.Loop:
                {
                    float th = Mathf.PI * 2f * (0.85f * u + 0.15f * S(u));
                    up = -(_f * Mathf.Sin(th) - _u * Mathf.Cos(th)); // belly out of the circle: over the top upside down
                    bank = false;
                    break;
                }
                case Orders.Immelmann:
                {
                    if (u < ImIn) break; // the run in: along the way
                    if (u < ImTop) { float th = Mathf.PI * (u - ImIn) / (ImTop - ImIn); up = -(_f * Mathf.Sin(th) - Vector3.up * Mathf.Cos(th)); bank = false; break; }
                    float k = S((u - ImTop) / (ImRoll - ImTop)); // the half roll on top: upright again, flying back
                    return Quaternion.LookRotation(-_f, Vector3.down) * Quaternion.AngleAxis(_side * 180f * k, Vector3.forward);
                }
                case Orders.Hammer:
                {
                    Vector3 c = _o + _drift + Vector3.up * (3f * _size);
                    if (u < HammerQ || u >= HammerFall) { up = (c - pos).normalized; bank = false; }
                    else if (u < HammerUp) return Quaternion.LookRotation(Vector3.up, -_f);
                    else if (u < HammerTurn) return Quaternion.AngleAxis(_side * 180f * S((u - HammerUp) / (HammerTurn - HammerUp)), -_f) * Quaternion.LookRotation(Vector3.up, -_f); // round on the spot
                    else return Quaternion.LookRotation(Vector3.down, -_f);
                    break;
                }
                case Orders.Leaf:
                {
                    if (u < 0.9f)
                    {
                        // a leaf: the body level, turned a little with each swing, rolled into it, the nose up a touch;
                        // handed over to its way's own line round where it catches the air (no flip of the nose there)
                        float fa = Mathf.Min(u / 0.8f, 1f), sw = Mathf.Cos(Mathf.PI * 4f * fa) * S(fa * 3f) * (1f - S(fa)); // the swings as in Pos
                        Quaternion q = Quaternion.LookRotation(Quaternion.AngleAxis(sw * 22f * _side, Vector3.up) * _f, Vector3.up);
                        leaf = q * Quaternion.Euler(-10f, 0f, -sw * 28f * _side);
                        leafW = 1f - S((u - 0.6f) / 0.3f);
                    }
                    break;
                }
            }
            if (Mathf.Abs(Vector3.Dot(tan.normalized, up.normalized)) > 0.98f) up = -_f; // straight up or down: the back towards you
            Quaternion look = Quaternion.LookRotation(tan, up);
            if (bank)
            {
                // its pull from three points round t, always both sides (at the ends a one-sided step read the
                // speed itself as a pull, a false bank on the first and last frame)
                float tc = Mathf.Clamp(t, h, Mathf.Max(h, len - h));
                Vector3 acc = tc == t ? (a + b - 2f * pos) / (h * h) : (Pos(tc - h, pin) + Pos(tc + h, pin) - 2f * Pos(tc, pin)) / (h * h);
                float lat = Vector3.Dot(acc, look * Vector3.right);
                float roll = Mathf.Clamp(Mathf.Atan2(lat, 9.81f) * Mathf.Rad2Deg * 1.3f, -55f, 55f);
                look = look * Quaternion.Euler(0f, 0f, -roll);
            }
            if (leafW > 0f) look = leafW >= 1f ? leaf : Quaternion.Slerp(look, leaf, leafW);
            return look;
        }

        // the wings: 0 as the flight goes, 1 gliding, 2 hard beats
        private int Wings(float u)
        {
            switch (_kind)
            {
                case Orders.Dive: return u > 1.1f / 1.6f ? 2 : 0;
                case Orders.Candle: return u < 0.45f ? 2 : 1;
                case Orders.Leaf: return u < 0.8f ? 1 : 2;
                case Orders.Hammer: return u < HammerTurn || u >= HammerFall ? 2 : 1; // beating through the stall on top, gliding down
                case Orders.Immelmann: return u >= ImIn && u < ImTop ? 2 : 0;
                case Orders.Roll: return u > _rollFrom + 0.05f && u < _rollTo - 0.05f ? 1 : 0; // held out through the roll
                default: return 0;
            }
        }
    }

    // a way through points, flown at an even pace (the time between points by their distance)
    internal sealed class Spline
    {
        private readonly List<Vector3> _p = new List<Vector3>();
        private readonly List<float> _d = new List<float>();
        internal void Clear() { _p.Clear(); _d.Clear(); }
        internal void Add(Vector3 v) { if (_p.Count > 0) _d.Add(_d.Count > 0 ? _d[_d.Count - 1] + Vector3.Distance(_p[_p.Count - 1], v) : Vector3.Distance(_p[0], v)); _p.Add(v); }
        internal Vector3 At(float u)
        {
            if (_p.Count < 2) return _p.Count == 1 ? _p[0] : Vector3.zero;
            float total = _d[_d.Count - 1], want = Mathf.Clamp01(u) * total;
            int i = 0;
            while (i < _d.Count - 1 && _d[i] < want) i++;
            float from = i > 0 ? _d[i - 1] : 0f, seg = Mathf.Max(0.0001f, _d[i] - from);
            float t = Mathf.Clamp01((want - from) / seg);
            Vector3 p0 = _p[Mathf.Max(0, i - 1)], p1 = _p[i], p2 = _p[i + 1], p3 = _p[Mathf.Min(_p.Count - 1, i + 2)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }

    // «Догонялки» (an order for both): a game of tag round you, flown by the motor as any flight (its turns, its
    // banks, the pair kept apart). One flees round a wide ring, the other goes after it, a little faster each second
    // it has not caught it yet. Caught: the roles change. The one that caught turns round and flees the other way, the
    // caught one swings round after it; a moment's start for the new runaway, and on. The ring is as wide as the place
    // round you lets it be (looked at now and then, a few rays), widening and narrowing smoothly.
    internal sealed class ChasePlan : FlightPlan
    {
        internal static float RingMin = 6f, RingMax = 10f, RingTight = 3.5f; // m: the ring wanted / the least where it is cramped
        internal static float Height = 3.5f, Swing = 1.2f;   // m over you, ± up and down as it flies
        internal static float FleeShare = 0.85f;             // × its cruise speed as it flees
        internal static float EdgeMin = 1.2f, EdgeMax = 3f, EdgeGrow = 0.2f; // m/s the chaser is faster (+ this each second)
        internal static float Lead = 0.5f, CatchAt = 1.5f, CatchAfter = 3f, Burst = 0.3f, BurstSeconds = 1.5f;
        internal override string Name { get { return Orders.Plans[Orders.Chase]; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.Player.transform.position + facing * new Vector3(pin.Munin ? -4f : 4f, Height, 0f); }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            ChaseGame g = ChaseGame.Of(pin.Player);
            int me = pin.Munin ? 1 : 0, other = 1 - me;
            Vector3 at = pin.transform.position;
            g.Pos[me] = at; g.Vel[me] = m.Body != null ? m.Body.Vel : carry; g.Seen[me] = Time.time;
            FlightStyle st = m.Body != null ? m.Body.Style : RavenFlight.For(pin.Munin);
            bool pair = Time.time - g.Seen[other] < 0.5f;
            float since = Time.time - g.SwappedAt;
            if (pair && g.MuninChases == pin.Munin)
            {
                // after it: aimed a little ahead of where it goes (never onto the spot itself: no braking on top of it)
                Vector3 run = g.Vel[other] - carry;
                Vector3 ahead = run.sqrMagnitude > 0.25f ? run.normalized : (g.Pos[other] - at).normalized;
                Vector3 aim = g.Pos[other] + run * Lead + ahead * 1.5f;
                float speed = Mathf.Min(g.Flee[other] + g.Edge + Mathf.Max(0f, since) * EdgeGrow, st.Top * 1.2f);
                m.Steer(aim, speed, false, 0f, dt, carry, 1f);
                if (since > CatchAfter && (g.Pos[other] - at).sqrMagnitude < CatchAt * CatchAt) g.Swap();
            }
            else
            {
                // away round the ring (just turned the other way after a catch: the motor swings it round)
                g.Breathe(pin.Player, dt);
                float t = Time.time + pin.Ring;
                Vector3 mid = pin.Player.transform.position + Vector3.up * (Height + Mathf.Sin(t * 0.5f) * Swing);
                Vector3 aim = CirclePlan.Ring(at, mid, g.RingNow, 45f, g.Dir);
                float flee = st.Cruise * FleeShare * (1f + Burst * (1f - S(since / BurstSeconds)));
                g.Flee[me] = flee;
                m.Steer(aim, flee, false, 0f, dt, carry, 1f);
            }
            m.Finish(dt, 0f);
        }
    }

    // the game of tag of one owner's two ravens: who is after whom, the ring, where each is (kept by the two plans;
    // one per owner, made when a game starts and dropped with its player)
    internal sealed class ChaseGame
    {
        internal Player Owner;
        internal bool MuninChases;
        internal float SwappedAt, Dir = 1f, Edge, Ring, RingNow;
        internal readonly Vector3[] Pos = new Vector3[2], Vel = new Vector3[2];
        internal readonly float[] Seen = { -100f, -100f }, Flee = { 8f, 8f };
        private float _lookAt;
        private static readonly List<ChaseGame> Games = new List<ChaseGame>(2);

        internal static ChaseGame Of(Player owner)
        {
            ChaseGame g = null;
            for (int i = Games.Count - 1; i >= 0; i--)
            {
                if (Games[i].Owner == null) { Games.RemoveAt(i); continue; } // (its player gone)
                if (ReferenceEquals(Games[i].Owner, owner)) g = Games[i];
            }
            if (g == null) { g = new ChaseGame { Owner = owner }; Games.Add(g); }
            if (Time.time - Mathf.Max(g.Seen[0], g.Seen[1]) > 1f) g.Start(); // neither flew it a moment ago: a new game
            return g;
        }

        private void Start()
        {
            MuninChases = Random.value < 0.5f;
            Dir = Random.value < 0.5f ? 1f : -1f;
            Ring = RingNow = ChasePlan.RingTight;
            _lookAt = 0f;
            Round();
            SwappedAt += 1.5f; // off the shoulders first
        }

        // caught: the other one is after it now, and it runs the other way round (and maybe a word on it: Orders)
        internal void Swap() { bool catcher = MuninChases; MuninChases = !MuninChases; Dir = -Dir; Round(); Orders.ChaseCaught(Owner, catcher); }

        private void Round()
        {
            SwappedAt = Time.time;
            Edge = Random.Range(ChasePlan.EdgeMin, ChasePlan.EdgeMax);
        }

        // the ring: as wide as wanted, narrower where something stands round you (8 rays every 3 s), eased
        internal void Breathe(Player owner, float dt)
        {
            if (Time.time >= _lookAt && owner != null)
            {
                _lookAt = Time.time + 3f;
                float want = Random.Range(ChasePlan.RingMin, ChasePlan.RingMax), room = want;
                Vector3 mid = owner.transform.position + Vector3.up * ChasePlan.Height;
                RaycastHit hit;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 d = Quaternion.AngleAxis(k * 45f, Vector3.up) * Vector3.forward;
                    if (Physics.Raycast(mid, d, out hit, want + 1.5f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) room = Mathf.Min(room, hit.distance - 1.5f);
                }
                Ring = Mathf.Max(ChasePlan.RingTight, room);
            }
            RingNow = Mathf.MoveTowards(RingNow, Ring, dt * 1.2f);
        }
    }

    // the dance is over but you are off at once: it flies along a while, a little behind and above your shoulder,
    // facing the way it flies, and lands only after that (or as soon as you stop)
    internal sealed class TrailPlan : FlightPlan
    {
        internal override string Name { get { return "за тобою"; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.HoverPoint(facing) + facing * new Vector3(0f, ShoulderPin.FollowUp, -ShoulderPin.FollowBack); }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            Vector3 side = facing * new Vector3(pin.Munin ? -0.35f : 0.35f, 0f, 0f);
            Vector3 trail = pin.HoverPoint(facing) + facing * new Vector3(0f, ShoulderPin.FollowUp, -ShoulderPin.FollowBack) + side;
            m.Steer(trail, Mathf.Max(6f, carry.magnitude * 1.3f + 2f), true, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }
    }
}
#endif

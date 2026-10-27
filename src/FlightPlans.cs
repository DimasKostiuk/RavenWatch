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
        // a share 0..1 the same on every game for this raven and this stretch of the network clock (who, what, which slot)
        protected static float Dice(ShoulderPin pin, int what, long slot)
        {
            long id = pin.Player != null ? pin.Player.GetPlayerID() : 0;
            unchecked
            {
                ulong h = (ulong)(id * 73856093L ^ (pin.Munin ? 19349663L : 83492791L) ^ what * 2654435761L ^ slot * 40503L);
                h ^= h >> 33; h *= 0xff51afd7ed558ccdUL; h ^= h >> 33;
                return (h % 10000UL) / 10000f;
            }
        }
        protected static double Net { get { return SitNet.Seconds; } }
        // now and then in an outing's flight, far enough from you: a barrel roll or a flip (Munin more often)
        internal static float PlayEvery = 14f, PlayChanceH = 0.25f, PlayChanceM = 0.4f;
        protected static void MaybePlay(RavenMotor m, ShoulderPin pin)
        {
            if (m.Body == null || m.Body.Final || m.Body.Speed < 4f) return;
            if (pin.Player != null && (pin.transform.position - pin.Player.transform.position).sqrMagnitude < 25f) return;
            long slot = (long)(Net / PlayEvery);
            float start = (float)(slot * PlayEvery) + Dice(pin, 7, slot) * (PlayEvery - 2f);
            if (Net < start || Net > start + 0.5f) return;
            if (Dice(pin, 8, slot) > (pin.Munin ? PlayChanceM : PlayChanceH)) return;
            m.Body.Play(Dice(pin, 9, slot) < 0.65f ? 1 : 2);
        }
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
            float t = pin.Clock + pin.Ring;
            Vector3 drift = facing * new Vector3(Mathf.Sin(t * 0.4f) * 0.8f, Mathf.Sin(t * 0.55f) * 0.5f, Mathf.Sin(t * 0.3f) * 0.6f);
            bool wings = OdinWings.Shown(pin.Player); // «Крила Одіна»: right at your back, a little apart
            Vector3 side = facing * (wings ? new Vector3(pin.Munin ? -0.9f : 0.9f, 0.4f, -1.6f) : new Vector3(pin.Munin ? -1.2f : 1.2f, 0f, 0f));
            return pin.HoverPoint(facing) + side + facing * new Vector3(0f, ShoulderPin.OutUp, -ShoulderPin.OutBack) + drift;
        }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (PairFx.Loaded(pin.Player)) { Heavy(m, pin, facing, carry, dt); return; }
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

        // «Подвійна ноша»: the two side by side over you, a little behind, the load hanging between them — lower than
        // they fly, slower, hard beats all the way, bobbing with each, and now and then sagging under it and jerking
        // back up (PairFx.Dip); they do not circle you when you stand
        internal static float HeavyUp = 2.4f, HeavySide = 0.75f, HeavyBack = 0.7f, HeavyPace = 0.85f;
        private void Heavy(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            _still = 0f;
            float t = pin.Clock + pin.Ring;
            Vector3 flat = facing * Vector3.forward; flat.y = 0f;
            Quaternion yaw = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat.normalized) : facing;
            float bob = Mathf.Sin(t * 2.3f) * 0.12f + PairFx.Dip(pin.Player, pin.Munin);
            Vector3 aim = pin.Player.transform.position + yaw * new Vector3(pin.Munin ? -HeavySide : HeavySide, 0f, -HeavyBack) + Vector3.up * (HeavyUp + bob);
            float run = carry.magnitude, behind = Vector3.Distance(pin.transform.position, aim);
            float speed = Mathf.Max(3f, run * 1.1f + 1f) * HeavyPace * (1f + Mathf.Clamp01((behind - 2f) / 6f));
            m.Steer(aim, speed, true, 0f, dt, carry, 1f);
            m.Body.WingMode = 2; // (hard beats: it is carrying)
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
            float t = pin.Clock + pin.Ring;
            bool wide = Outing.IsWide(pin.OutMode);
            Vector3 mid = pin.Player.transform.position + Vector3.up * (wide ? (munin ? 5.5f : 6.5f) : (munin ? 3.6f : 4.2f));
            float r = wide ? Mathf.Lerp(WideMin, WideMax, Mathf.PerlinNoise(t * (munin ? 0.12f : 0.07f), pin.Ring)) : Mathf.Lerp(RingMin, RingMax, Mathf.PerlinNoise(t * (munin ? 0.18f : 0.1f), pin.Ring));
            float dir = Outing.Clockwise(pin.OutMode) ? 1f : -1f;
            float h = Mathf.Sin(t * (munin ? 0.7f : 0.45f)) * (munin ? 0.6f : 1f) + Mathf.Sin(t * 1.9f) * 0.15f;
            // «Розігнати хмари»: high and wide over you, as high as the sky over you lets them (a roof, a crown, a cliff: lower)
            float sky = ClearSky.Room(pin.Player);
            float high = Mathf.Min(sky - 1.5f, munin ? ClearSky.HighMunin : ClearSky.HighHugin), low = mid.y - pin.Player.transform.position.y;
            if (high > low)
            {
                float k = Mathf.Clamp01((high - low) / Mathf.Max(0.1f, (munin ? ClearSky.HighMunin : ClearSky.HighHugin) - low)); // (a low sky: the ring grows only as much)
                mid = pin.Player.transform.position + Vector3.up * high;
                r = Mathf.Lerp(r, Mathf.Lerp(ClearSky.RingMin, ClearSky.RingMax, Mathf.PerlinNoise(t * (munin ? 0.12f : 0.07f), pin.Ring)), k);
            }
            return Ring(pin.transform.position, mid, r, munin ? 40f : 32f, dir) + Vector3.up * h;
        }
        // a point 'lead' degrees ahead of where the bird is, on a ring of 'r' round 'mid' (dir 1 clockwise from above)
        internal static Vector3 Ring(Vector3 at, Vector3 mid, float r, float lead, float dir)
        {
            Vector3 rel = at - mid;
            float a = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
            return mid + Quaternion.AngleAxis(a + dir * lead, Vector3.up) * (Vector3.forward * r);
        }
        // a thermal: now and then it finds rising air and climbs round it on still wings, wider, slower,
        // up to ThermalUp metres, then glides back down to its ring; the same moments on every game (the network clock)
        internal static float ThermalEvery = 30f, ThermalChanceH = 0.55f, ThermalChanceM = 0.3f, ThermalUp = 7f, ThermalSeconds = 12f;
        private float _lift;
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            bool munin = pin.Munin;
            long slot = (long)(Net / ThermalEvery);
            double into = Net - slot * ThermalEvery;
            bool thermal = Dice(pin, 3, slot) < (munin ? ThermalChanceM : ThermalChanceH) && into < ThermalSeconds && ClearSky.Room(pin.Player) <= 0f; // (over the clouds they are high already)
            float liftWant = thermal ? ThermalUp * Mathf.Clamp01((float)into / ThermalSeconds * 1.4f) : 0f;
            _lift = Mathf.MoveTowards(_lift, liftWant, dt * (thermal ? 0.8f : 1.2f));
            float th = Mathf.Clamp01(_lift / 2f);
            Vector3 aim = Aim(pin) + Vector3.up * _lift;
            if (th > 0f) { Vector3 mid = pin.Player.transform.position; Vector3 rel = Vector3.ProjectOnPlane(aim - mid, Vector3.up); aim += rel.normalized * th * 2.5f; } // (a wider ring in the thermal)
            // (no faster than it can turn round the ring: the motor's bend rule, FlightBody.BendSpeed)
            m.Steer(aim, m.Body.Style.Cruise * (munin ? 0.8f : 0.65f) * Mathf.Lerp(1f, 0.75f, th), false, 0f, dt, carry, 1f);
            m.Body.WingMode = thermal && th > 0.3f ? 1 : 0; // (still wings in the rising air)
            m.Finish(dt, 0f);
            if (!thermal) MaybePlay(m, pin);
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
            float t = pin.Clock + pin.Ring;
            return pin.Player.transform.position + facing * new Vector3(Mathf.Sin(t * 0.8f) * 3f, 0f, ShoulderPin.ScoutAhead) + Vector3.up * ShoulderPin.ScoutUp;
        }
        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (!_back)
            {
                Vector3 aim = Target(pin, facing);
                float left = m.Steer(aim, m.Body.Style.Top, false, 0f, dt, carry, 1f);
                m.Finish(dt, 0f);
                MaybePlay(m, pin);
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
            float t = pin.Clock + pin.Ring;
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

    // The hunt (Outing.Hunt): a raven (Munin by himself, either on a visit: WorldVisit) after one of the game's birds — at its top speed, aiming a little ahead of where the
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
        private long _spotSec = -1;
        private Quaternion _airRot;
        private bool _airSet;

        // its wings in bursts and glides, its legs now and then down as if to
        // sit on you; and when you lie or sit, by chance it comes down by you on the ground (hops about, pecks, preens)
        // or, lying, onto your chest. Each raven picks its own way each time, so one may sit on you while the other
        // flies about; now and then it changes its mind. The owner's game decides and writes it in the player's data
        // (one number a raven: Net); every other game reads it and plays the same (where it went, each hop, glides,
        // legs; its pecks and dozes are sitting acts, sent as those are).
        internal enum Way { Air, Ground, OnYou }
        internal static float GroundChance = 0.35f, OnYouChance = 0.3f, RethinkMin = 18f, RethinkMax = 40f, RethinkChance = 0.35f;
        private Way _way = Way.Air;
        private int _phase;            // ground / on you: 0 flying there, 1 down
        private float _rethinkAt, _beatUntil, _glideUntil, _dangleAt, _dangleUntil, _hopAt, _peckAt;
        private Vector3 _foot, _rel;  // where it stands (ground: its offset from you; on you: from your chest)
        private float _yaw, _sink;
        private static readonly Dictionary<Player, bool> OnChest = new Dictionary<Player, bool>(); // who sits on whom (one raven at most)
        internal bool OnGround { get { return _way != Way.Air && _phase == 1; } }

        internal override string Name { get { return "танець"; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.HoverPoint(facing); }
        internal override void Begin() { Unclaim(); _way = Way.Air; _phase = 0; _rethinkAt = 0f; _sink = 0f; _spotSec = -1; }
        // its claim on your chest let go (it flew home, or something else took it while it sat there)
        private Player _claimed; private bool _claimedMunin;
        private void Unclaim() { bool m; if (_claimed != null && OnChest.TryGetValue(_claimed, out m) && m == _claimedMunin) OnChest.Remove(_claimed); _claimed = null; }

        private static bool Lying(Player p)
        {
            if (p == null) return false;
            if (p.IsSleeping()) return false; // (a bed: not on the ground)
            ZDO z = NetFlag.Zdo(p, false);
            string e = z != null ? z.GetString(ZDOVars.s_emote, "") : "";
            return string.Equals(e, "rest", System.StringComparison.OrdinalIgnoreCase) || string.Equals(e, "relax", System.StringComparison.OrdinalIgnoreCase);
        }
        private static bool Still(Player p) { return p != null && !p.IsSleeping() && (Lying(p) || p.IsSitting()) && p.GetVelocity().sqrMagnitude < 0.25f; }

        private void Rethink(RavenMotor m, ShoulderPin pin, Vector3 carry)
        {
            Player p = pin.Player;
            _rethinkAt = Time.time + Random.Range(RethinkMin, RethinkMax);
            Way want = Way.Air;
            if (Still(p))
            {
                bool other; bool taken = OnChest.TryGetValue(p, out other) && other != pin.Munin;
                float r = Random.value;
                if (Lying(p) && !taken && r < OnYouChance) want = Way.OnYou;
                else if (r < OnYouChance + GroundChance) want = Way.Ground;
            }
            if (want == _way) return; // (down already: it keeps its spot, checked when it chose it)
            Vector3 rel = Vector3.zero;
            if (want == Way.Ground && !GroundSpot(p, out rel)) want = Way.Air; // (no floor by you: it stays over the shoulder)
            if (want == _way) return;
            TakeOff(m, pin, carry); // (off the floor or your chest first: its standing pose let go, a push up)
            Leave(pin);
            _way = want; _phase = 0;
            if (want == Way.OnYou) { Prune(); OnChest[p] = pin.Munin; _claimed = p; _claimedMunin = pin.Munin; }
            if (want == Way.Ground) _rel = rel;
        }
        // players gone from the game (their claim left behind) out of the list
        private static readonly List<Player> Gone = new List<Player>();
        private static void Prune()
        {
            Gone.Clear();
            foreach (Player k in OnChest.Keys) if (k == null) Gone.Add(k);
            foreach (Player k in Gone) OnChest.Remove(k);
        }
        private void Leave(ShoulderPin pin)
        {
            Unclaim();
        }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            ShoulderLife life = pin.Life;
            bool mine = ReferenceEquals(pin.Player, Player.m_localPlayer);
            if (!mine) Read(m, pin, carry);
            else
            {
                if (Time.time >= _rethinkAt && (_rethinkAt <= 0f || Random.value < RethinkChance || (_way != Way.Air && !Still(pin.Player)))) Rethink(m, pin, carry);
                else if (Time.time >= _rethinkAt) _rethinkAt = Time.time + Random.Range(RethinkMin, RethinkMax);
                if (_way != Way.Air && !Still(pin.Player)) TakeOff(m, pin, carry);
            }
            if (_way != Way.Air) { Down(m, pin, facing, carry, dt); if (mine) Write(pin); return; }
            if (life != null) { life.Hovering = true; }
            // where over you it drifts, a new spot about every 3..6 s: rolled on the world's second, alike in every game
            long sec = (long)Net;
            if (sec != _spotSec)
            {
                bool first = _spotSec < 0;
                _spotSec = sec;
                if (first || Dice(pin, 31, sec) < 1f / 4.5f)
                {
                    float out_ = pin.Munin ? -1f : 1f;
                    _spot = new Vector3(Mathf.Lerp(-0.15f, 0.35f, Dice(pin, 32, sec)) * out_, Mathf.Lerp(-0.1f, 0.35f, Dice(pin, 33, sec)), Mathf.Lerp(-0.4f, 0.3f, Dice(pin, 34, sec)));
                }
            }
            _spotNow = Vector3.MoveTowards(_spotNow, _spot, dt * 0.35f);
            Rhythm(life, dt, mine);
            if (mine) Write(pin);
            m.Steer(pin.HoverPoint(facing) + facing * _spotNow + Vector3.down * _sink, 3f, true, 0f, dt, carry, 1f);
            m.Body.FaceHint(pin.Player.transform.forward, 1f);
            m.Finish(dt, 0f);
            if (m.Holding) { _airSet = false; return; } // still crouching to take off: the hold places it
            float rel = (m.Body.Vel - carry).magnitude;
            AliveInAir(pin, facing, 1f - Mathf.Clamp01((rel - 0.5f) / 2f));
        }

        // the wings: a few strong beats, then a glide on spread wings sinking a little, beating up again; its own each
        // time (Munin beats more and sharper, Hugin glides longer); the legs now and then let down for a moment
        private bool _gliding, _dangling;
        private void Rhythm(ShoulderLife life, float dt, bool mine)
        {
            if (life == null) return;
            bool munin = life.Munin;
            float now = Time.time;
            if (!mine)
            {
                life.HoverGlide = Mathf.MoveTowards(life.HoverGlide, _gliding ? 1f : 0f, dt * (_gliding ? 1.2f : 1.8f));
                _sink = Mathf.MoveTowards(_sink, _gliding ? 0.3f : 0f, dt * (_gliding ? 0.35f : 0.6f));
                life.Dangle = _dangling ? 1f : 0f;
                return;
            }
            if (now >= _glideUntil && now >= _beatUntil)
            {
                _beatUntil = now + Random.Range(munin ? 0.9f : 0.7f, munin ? 2.2f : 1.6f);
                _glideUntil = _beatUntil + (Random.value < (munin ? 0.55f : 0.8f) ? Random.Range(munin ? 0.35f : 0.6f, munin ? 0.8f : 1.5f) : 0f);
                life.HoverPace = Random.Range(0.85f, 1.2f) * (munin ? 1.1f : 0.95f);
            }
            bool gliding = now >= _beatUntil && now < _glideUntil;
            _gliding = gliding;
            life.HoverGlide = Mathf.MoveTowards(life.HoverGlide, gliding ? 1f : 0f, dt * (gliding ? 1.2f : 1.8f)); // (eased: the beat slows and quickens, never at once)
            _sink = Mathf.MoveTowards(_sink, gliding ? 0.3f : 0f, dt * (gliding ? 0.35f : 0.6f));
            if (now >= _dangleAt) { _dangleAt = now + Random.Range(4f, 10f); _dangleUntil = Random.value < 0.4f ? now + Random.Range(1.2f, 2.8f) : 0f; }
            life.Dangle = now < _dangleUntil ? 1f : 0f;
            _dangling = life.Dangle > 0.5f;
        }

        // ---- the network: way 2 bits, glide 1, legs 1, pace 4, hop count 8, where it is about you (angle 8, from you
        // 6), its facing 8
        private static readonly int KeyH = "RavenWatch_HoverH".GetStableHashCode(), KeyM = "RavenWatch_HoverM".GetStableHashCode();
        private int _hopN, _hopSeen = -1;
        private long _sent = -1;
        private void Write(ShoulderPin pin)
        {
            Vector3 rel = Vector3.ProjectOnPlane(_rel, Vector3.up);
            int ang = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg, 360f) / 360f * 255f) & 255;
            int dist = Mathf.Clamp(Mathf.RoundToInt((rel.magnitude - 0.5f) / 1.2f * 63f), 0, 63);
            int yaw = Mathf.RoundToInt(Mathf.Repeat(_yaw, 360f) / 360f * 255f) & 255;
            int pace = Mathf.Clamp(Mathf.RoundToInt((pin.Life != null ? pin.Life.HoverPace - 0.8f : 0.2f) / 0.45f * 15f), 0, 15);
            long v = ((long)_way & 3) | ((_gliding ? 1L : 0L) << 2) | ((_dangling ? 1L : 0L) << 3) | ((long)pace << 4) | ((long)(_hopN & 255) << 8) | ((long)ang << 16) | ((long)dist << 24) | ((long)yaw << 30) | ((long)(_actKind & 3) << 38);
            if (v == _sent) return;
            ZDO z = NetFlag.Zdo(pin.Player, true);
            if (z == null) return;
            z.Set(pin.Munin ? KeyM : KeyH, v);
            _sent = v;
        }
        private void Read(RavenMotor m, ShoulderPin pin, Vector3 carry)
        {
            ZDO z = NetFlag.Zdo(pin.Player, false);
            if (z == null) return;
            long v = z.GetLong(pin.Munin ? KeyM : KeyH, 0L);
            Way way = (Way)(int)(v & 3);
            _gliding = ((v >> 2) & 1) != 0; _dangling = ((v >> 3) & 1) != 0;
            if (pin.Life != null) pin.Life.HoverPace = 0.8f + ((v >> 4) & 15) / 15f * 0.45f;
            int hop = (int)((v >> 8) & 255);
            float ang = ((v >> 16) & 255) / 255f * 360f, dist = 0.5f + ((v >> 24) & 63) / 63f * 1.2f;
            _yaw = ((v >> 30) & 255) / 255f * 360f;
            Vector3 rel = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * dist;
            if (way != _way)
            {
                TakeOff(m, pin, carry); // (from the floor to your chest too: up first, as on the owner's game)
                if (way != Way.Air) { _way = way; _phase = 0; _rel = rel; _hopSeen = hop; }
            }
            if (_way == Way.Ground && _phase == 1 && hop != _hopSeen)
            {
                _hopSeen = hop;
                int kind = (int)((v >> 38) & 3);
                if (kind == 0) kind = 3;
                if (_act != 0) { _foot = _to; _yawCur = _yawGo; } // (the last one cut short: from where it would be)
                Begin(pin, kind, rel, _yaw);
            }
        }

        // ---- down by you (the ground) or on you (your chest)
        private Vector3 Spot(ShoulderPin pin)
        {
            Player p = pin.Player;
            if (_way == Way.OnYou)
            {
                // on your body, down from the chest towards the belly (higher up it would sit nearly on
                // your head); its height over you found by a ray (armour, a lying pose): just above what is there
                Transform chest = Chest(p), hips = Hips(p);
                Vector3 body = chest != null && hips != null ? Vector3.Lerp(chest.position, hips.position, OnYouAlong) : chest != null ? chest.position : p.transform.position + Vector3.up * 0.3f;
                return body + Vector3.up * OnYouUp;
            }
            Vector3 at = p.transform.position + _rel;
            if (_floorFor != _rel || Mathf.Abs(p.transform.position.y - _floorNear) > 0.5f) { _floorFor = _rel; _floorNear = p.transform.position.y; _floorY = Floor(at, _floorNear); }
            at.y = _floorY;
            return at;
        }
        private Vector3 _floorFor = new Vector3(999f, 0f, 0f);
        private float _floorY, _floorNear;
        private static readonly Dictionary<Player, Transform> Chests = new Dictionary<Player, Transform>();
        internal static float OnYouAlong = 0.6f, OnYouUp = 0.14f; // share of the way chest → hips; m above that point
        private static readonly Dictionary<Player, Transform> HipsOf = new Dictionary<Player, Transform>();
        private static Transform Hips(Player p)
        {
            Transform t;
            if (HipsOf.TryGetValue(p, out t) && t != null) return t;
            if (HipsOf.Count > 16) HipsOf.Clear();
            t = null;
            foreach (Transform c in p.GetComponentsInChildren<Transform>()) if (c.name == "Hips") { t = c; break; }
            HipsOf[p] = t;
            return t;
        }
        private static Transform Chest(Player p)
        {
            Transform t;
            if (Chests.TryGetValue(p, out t) && t != null) return t;
            if (Chests.Count > 16) { Chests.Clear(); OnChest.Clear(); } // (players gone: their entries with them)
            t = null;
            foreach (Transform c in p.GetComponentsInChildren<Transform>()) if (c.name == "Spine2") { t = c; break; }
            Chests[p] = t;
            return t;
        }
        private static int _mask;
        internal static float FloorUp = 0.6f, FloorDrop = 1f; // looked for from this far over you, down to this far under you
        private static float Floor(Vector3 at, float near)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(at.x, near + FloorUp, at.z), Vector3.down, out hit, FloorUp + FloorDrop, _mask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return Mathf.Max(Ground(at), near - 0.5f); // (another game's raven, its spot checked by its owner: never stuck for a ray)
        }

        // a real floor at you + rel for a raven down by you: level, no lower than FloorDrop under you, and nothing
        // between you and it — past a ledge's edge (a loft, a bed up high) or behind a wall is no place to walk
        private static bool FloorOk(Player p, Vector3 rel)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            Vector3 you = p.transform.position, at = you + Vector3.ProjectOnPlane(rel, Vector3.up);
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(at.x, you.y + FloorUp, at.z), Vector3.down, out hit, FloorUp + FloorDrop, _mask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.7f) return false; // (a slope, a wall's foot)
            return !Physics.Linecast(you + Vector3.up * 0.5f, hit.point + Vector3.up * 0.15f, _mask, QueryTriggerInteraction.Ignore);
        }

        // a spot round you with a floor (a few tries); false: none here
        private static bool GroundSpot(Player p, out Vector3 rel)
        {
            for (int i = 0; i < 8; i++)
            {
                rel = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(0.7f, 1.4f);
                if (FloorOk(p, rel)) return true;
            }
            rel = Vector3.zero;
            return false;
        }

        private void Down(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            ShoulderLife life = pin.Life;
            Vector3 spot = Spot(pin);
            if (_phase == 0)
            {
                // flying there: over the spot, slowing, legs out
                if (life != null) { life.Hovering = true; life.Dangle = 1f; life.HoverGlide = 0f; }
                Vector3 aim = spot + Vector3.up * 0.35f;
                m.Steer(aim, 3f, true, 0f, dt, carry, 1f);
                m.Body.FaceHint(Vector3.ProjectOnPlane(pin.Player.transform.position - spot, Vector3.up), 0.6f);
                m.Finish(dt, Mathf.Clamp01(1f - Vector3.Distance(pin.transform.position, aim) / 1.5f));
                if (Vector3.Distance(pin.transform.position, aim) < 0.25f || (Vector3.Distance(pin.transform.position, aim) < 0.6f && m.Body.Speed < 1f))
                {
                    _phase = 1;
                    m.Grounded();
                    if (life != null) { life.Hovering = false; life.Dangle = 0f; life.HoverGlide = 0f; life.VanillaFly = false; life.Flap(0.3f); }
                    _foot = spot; _act = 0; _downAt = Time.time;
                    if (ReferenceEquals(pin.Player, Player.m_localPlayer)) _yaw = YawTo(pin, spot);
                    _yawCur = _yaw;
                    _hopAt = Time.time + Random.Range(1.5f, 4f);
                    _peckAt = Time.time + Random.Range(1f, 3f);
                }
                return;
            }
            // down: it stands there and lives — turns round stepping, walks a few steps, hops (one to three), pecks the
            // ground, looks about; each move its own, played the same on every game (Net: its kind, where to, facing)
            if (life != null && life.VanillaFly) life.VanillaFly = false;
            if (life != null) life.OnFloor = true;
            if (_way == Way.Ground) Choose(pin, life);
            else _foot = spot;
            float up = 0f, pitch = 0f;
            if (_act != 0) Play(pin, life, out up, out pitch);
            else if (life != null) { float c = (Time.time - _downAt) / 0.4f; life.GroundCrouch = c < 1f ? Mathf.Sin(c * Mathf.PI) * 0.6f : 0f; life.GroundLiftL = life.GroundLiftR = 0f; } // (just down: the blow taken in its legs)
            if (_way == Way.Ground && _act == 0) _foot.y = _floorY;
            if (_act == 0) _yawCur = Mathf.MoveTowardsAngle(_yawCur, _yaw, dt * 40f); // (a last little settle of its facing)
            pin.transform.position = _foot + Vector3.up * up;
            pin.transform.rotation = Quaternion.Euler(0f, _yawCur, 0f) * Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.Euler(Shoulder.Rotation);
            if (life != null && life.SitNew && _act == 0 && Time.time >= _peckAt)
            {
                _peckAt = Time.time + Random.Range(2.5f, 7f) * (pin.Munin ? 0.7f : 1f);
                string what = _way == Way.OnYou ? (Random.value < 0.5f ? "Дрімає" : "Досліджує тебе") : (Random.value < 0.6f ? "Клює землю" : Random.value < 0.5f ? "Чистить пір'я" : "Розглядає знахідку");
                life.SitDirectorNow.React(life, what, 0f);
            }
        }
        private float _yawCur;
        private static float YawTo(ShoulderPin pin, Vector3 from)
        {
            Vector3 d = Vector3.ProjectOnPlane(pin.Player.transform.position - from, Vector3.up);
            float toYou = d.sqrMagnitude > 0.001f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
            return toYou + Random.Range(-70f, 70f);
        }

        // ---- its moves on the ground. A turn: in steps of ~35°, a foot up at each, by turns. A walk: it turns to its
        // way first, then small steps, left, right, the body bobbing and swaying. A hop (one to three, both feet): it
        // crouches, leans in and pushes off (a flick of the wings), the legs drawn up in the air, it lands and takes
        // the blow in its legs. All worked out from where and how far, so every game plays it alike.
        internal static float StepTurnDeg = 35f, StepGo = 0.07f, HopLong = 0.25f, GroundStepH = 0.22f, GroundStepM = 0.16f, HopTimeH = 0.42f, HopTimeM = 0.34f, HopHigh = 0.08f;
        private int _act; // 0 none, 1 turn, 2 walk, 3 hop
        private float _actAt, _turnLen, _goLen, _stepT, _hopT;
        private int _turnSteps, _goSteps;
        private Vector3 _from, _to;
        private float _yawFrom, _yawGo;
        private void Begin(ShoulderPin pin, int kind, Vector3 toRel, float yawEnd)
        {
            Vector3 you = pin.Player.transform.position;
            _from = _foot; _to = you + toRel; _to.y = Floor(_to, you.y); _rel = toRel;
            _yawFrom = _yawCur; _yaw = yawEnd;
            bool m = pin.Munin;
            _stepT = m ? GroundStepM : GroundStepH; _hopT = m ? HopTimeM : HopTimeH;
            Vector3 d = Vector3.ProjectOnPlane(_to - _from, Vector3.up);
            float dist = d.magnitude;
            _yawGo = kind == 1 || dist < 0.01f ? yawEnd : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float turn = Mathf.Abs(Mathf.DeltaAngle(_yawFrom, _yawGo));
            _turnSteps = turn > 12f ? Mathf.CeilToInt(turn / StepTurnDeg) : 0;
            _turnLen = _turnSteps * _stepT;
            _goSteps = kind == 2 ? Mathf.Max(2, Mathf.CeilToInt(dist / StepGo)) : kind == 3 ? Mathf.Clamp(Mathf.RoundToInt(dist / HopLong), 1, 3) : 0;
            _goLen = kind == 2 ? _goSteps * _stepT : kind == 3 ? _goSteps * _hopT : 0f;
            _act = kind; _actAt = Time.time;
            _floorFor = _rel; _floorNear = you.y; _floorY = _to.y;
        }
        private void Play(ShoulderPin pin, ShoulderLife life, out float up, out float pitch)
        {
            up = 0f; pitch = 0f;
            float t = Time.time - _actAt;
            float liftL = 0f, liftR = 0f, crouch = 0f;
            if (t < _turnLen)
            {
                // turning to its way (or round on the spot): a step a turn, the feet by turns
                int i = Mathf.Min(_turnSteps - 1, (int)(t / _stepT));
                float u = (t - i * _stepT) / _stepT;
                float done = (i + u * u * (3f - 2f * u)) / Mathf.Max(1, _turnSteps);
                _yawCur = Mathf.LerpAngle(_yawFrom, _yawGo, done);
                float lift = Mathf.Sin(u * Mathf.PI) * 0.55f;
                if (i % 2 == 0) liftL = lift; else liftR = lift;
                up = Mathf.Sin(u * Mathf.PI) * 0.005f;
            }
            else if (_act == 1) { _yawCur = _yawGo; End(); }
            else
            {
                _yawCur = _yawGo;
                float g = t - _turnLen;
                if (_act == 2)
                {
                    // walking: small steps, the body forward a little at each foot down, a sway to the standing foot
                    int i = Mathf.Min(_goSteps - 1, (int)(g / _stepT));
                    float u = Mathf.Clamp01((g - i * _stepT) / _stepT);
                    float k = (i + u * u * (3f - 2f * u)) / _goSteps;
                    _foot = Vector3.Lerp(_from, _to, k);
                    float lift = Mathf.Sin(u * Mathf.PI) * 0.75f;
                    if (i % 2 == 0) liftL = lift; else liftR = lift;
                    up = Mathf.Abs(Mathf.Sin(u * Mathf.PI)) * 0.008f;
                    pitch = 4f + Mathf.Sin(u * Mathf.PI * 2f) * 2f;
                    if (g >= _goLen) End();
                }
                else
                {
                    // hopping: crouch, push, the legs up in the air, down, the blow taken in the legs
                    int i = Mathf.Min(_goSteps - 1, (int)(g / _hopT));
                    float u = Mathf.Clamp01((g - i * _hopT) / _hopT);
                    Vector3 a = Vector3.Lerp(_from, _to, (float)i / _goSteps), b = Vector3.Lerp(_from, _to, (float)(i + 1) / _goSteps);
                    if (u < 0.25f) { float c = u / 0.25f; crouch = Mathf.Sin(c * Mathf.PI * 0.5f) * 0.7f; pitch = 12f * c; _foot = a; }
                    else if (u < 0.75f)
                    {
                        float air = (u - 0.25f) / 0.5f;
                        _foot = Vector3.Lerp(a, b, air * air * (3f - 2f * air));
                        up = Mathf.Sin(air * Mathf.PI) * HopHigh * (pin.Munin ? 1.15f : 1f);
                        liftL = liftR = Mathf.Sin(Mathf.Min(1f, air * 1.6f) * Mathf.PI * 0.5f) * 0.85f * (1f - Mathf.Clamp01((air - 0.7f) / 0.3f));
                        pitch = Mathf.Lerp(12f, -8f, air);
                        if (air < 0.08f && life != null && !_flicked) { _flicked = true; life.Flap(0.16f); }
                    }
                    else { float c = (u - 0.75f) / 0.25f; crouch = Mathf.Sin(c * Mathf.PI) * 0.6f; pitch = Mathf.Lerp(-8f, 0f, c); _foot = b; _flicked = false; }
                    if (g >= _goLen) End();
                }
            }
            if (life != null) { life.GroundLiftL = liftL; life.GroundLiftR = liftR; life.GroundCrouch = crouch; }
        }
        private bool _flicked;
        private float _downAt = -10f;
        private void End() { _act = 0; _foot = _to; _foot.y = _floorY; _hopAt = Time.time + Random.Range(1.2f, 3.5f) * (_munin ? 0.7f : 1.1f); }
        private bool _munin;

        // the owner's game: what next (a turn, a walk, a hop), now and then
        private void Choose(ShoulderPin pin, ShoulderLife life)
        {
            _munin = pin.Munin;
            if (!ReferenceEquals(pin.Player, Player.m_localPlayer)) return; // (the others: Read starts each move the owner made)
            if (_act != 0 || Time.time < _hopAt) return;
            Vector3 you = pin.Player.transform.position;
            Vector3 rel = Vector3.ProjectOnPlane(_foot - you, Vector3.up);
            float r = Random.value, kindBias = pin.Munin ? 0.15f : 0f; // (Munin hops more)
            int kind = r < 0.3f ? 1 : r < 0.65f - kindBias ? 2 : 3;
            Vector3 toRel = rel;
            if (kind != 1)
            {
                float a = Random.Range(-45f, 45f), d = kind == 2 ? Random.Range(0.12f, 0.4f) : Random.Range(0.2f, 0.7f);
                Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                toRel = rel + dir * d;
                float rr = toRel.magnitude;
                if (rr < 0.6f || rr > 1.6f) toRel = (rel.sqrMagnitude > 0.01f ? Quaternion.Euler(0f, a, 0f) * rel.normalized : Vector3.forward) * Mathf.Clamp(rr, 0.6f, 1.6f); // (never onto you, never far)
            }
            if (kind != 1 && !FloorOk(pin.Player, toRel)) { kind = 1; toRel = rel; } // (no floor there: it only turns about)
            float yawEnd = kind == 1 ? YawTo(pin, _foot) : _yawCur;
            if (kind != 1) { Vector3 dd = Vector3.ProjectOnPlane(you + toRel - _foot, Vector3.up); if (dd.sqrMagnitude > 0.0001f) yawEnd = Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg; }
            _actKind = kind; _hopN++;
            Begin(pin, kind, toRel, yawEnd);
        }
        private int _actKind;

        // off the ground (you got up, it changed its mind): a push up, the wings out, flying again
        internal void TakeOff(RavenMotor m, ShoulderPin pin, Vector3 carry)
        {
            if (_way == Way.Air) return;
            bool down = _phase == 1;
            Leave(pin);
            _way = Way.Air; _phase = 0;
            ShoulderLife life = pin.Life;
            _act = 0;
            if (life != null) { life.Hovering = true; life.VanillaFly = ShoulderLife.HoverVanilla; life.OnFloor = false; life.GroundCrouch = 0f; life.GroundLiftL = life.GroundLiftR = 0f; }
            if (down) m.Launch(m.TakeoffVelocity(pin.HoverPoint(Quaternion.Euler(0f, pin.Player.transform.eulerAngles.y, 0f)), carry, Name), Name, 1f);
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

        private static float Roll(System.Random dice, float min, float max) { return min + (float)dice.NextDouble() * (max - min); }

        private void Start(RavenMotor m, ShoulderPin pin, Quaternion facing, float side, Vector3 carry)
        {
            _phase = 1; _t = 0f; _tf = 0f; _drift = Vector3.zero; _side = side; _carry = carry; _gap = Vector3.zero; _lookAt = 0f;
            FlightCharacter ch = m.Char();
            _len = Length(_kind) * (Timed ? ch.TrickTempo : 1f);
            _size = Sized ? ch.TrickSize : 1f;
            float v = ch.TrickVary;
            System.Random dice = new System.Random(Mathf.RoundToInt(pin.Ring * 1000f) * 31 + _kind); // (the order's seed: the same figure in every game)
            float a = Roll(dice, -v, v), pace = Roll(dice, -v, v) * 0.5f; // bigger goes with longer (the same pace), a little quicker or slower besides
            if (Sized) _size *= 1f + a;
            if (Timed) _len *= 1f + a + (Sized ? pace : 0f);
            _rollFrom = RollFrom + Roll(dice, -0.5f, 0.6f) * v; _rollTo = RollTo + Roll(dice, -0.6f, 0.5f) * v;
            _spin = Roll(dice, 0f, 1f) < Mathf.Clamp01(v * 3f) ? -1f : 1f;
            Transform b = pin.transform;
            _o = b.position;
            Vector3 v0 = m.Body != null ? m.Body.Vel : Vector3.zero;
            _f = facing * Vector3.forward;
            _r = facing * Vector3.right * side; // each raven to its own side (Munin mirrored)
            _u = _kind == Orders.Loop ? Quaternion.AngleAxis(Roll(dice, -100f, 100f) * v, _f) * Vector3.up : Vector3.up;
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
                    // nearly even pace all the way round, no stop before or after
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
    // banks, the pair kept apart). One flees, the other goes after it, a little faster each second it has not caught
    // it yet. Caught: the roles change and the shape of the flight changes (Shapes: the ring, an eight, a snake, a hill —
    // never the same twice running). The one that caught turns round and flees the other way, the caught one swings
    // round after it; a moment's start for the new runaway, and on. The shapes are as wide as the place round you lets
    // them be (looked at now and then, a few rays), widening and narrowing smoothly.
    internal sealed class ChasePlan : FlightPlan
    {
        internal static float RingMin = 6f, RingMax = 10f, RingTight = 3.5f; // m: the ring wanted / the least where it is cramped
        internal static float Height = 3.5f, Swing = 1.2f;   // m over you, ± up and down as it flies
        internal static float FleeShare = 0.85f;             // × its cruise speed as it flees
        internal static float EdgeMin = 1.2f, EdgeMax = 3f, EdgeGrow = 0.2f; // m/s the chaser is faster (+ this each second)
        internal static float Lead = 0.5f, CatchAt = 1.5f, CatchAfter = 3f, Burst = 0.3f, BurstSeconds = 1.5f;
        internal static float CutIn = 0.25f, ShapeCut = 0.4f; // share of the way the chaser cuts across: to the ring's middle / to where the runner is going on a shape
        internal static float LiftClear = 1.6f, LiftMax = 7f, LiftSpeed = 1.5f; // m over the highest roof round; m raised at most; m/s it rises and sinks

        // Among houses (a village, a yard full of sheds) the game is flown over the roofs: how much higher than over open
        // ground it must go so the lowest of its swing stays LiftClear over everything round the middle (rays down from
        // high above, the middle and two rings). Open ground: 0. The same in every game: the same world under it.
        internal static float RoofLift(Vector3 mid, float ring)
        {
            float top = float.MinValue;
            RaycastHit hit;
            for (int k = 0; k < 17; k++)
            {
                float r = k == 0 ? 0f : k <= 8 ? ring * 0.5f : ring + 1.5f;
                Vector3 p = mid + Quaternion.AngleAxis(k * 45f + (k > 8 ? 22.5f : 0f), Vector3.up) * Vector3.forward * r;
                if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out hit, 45f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) top = Mathf.Max(top, hit.point.y);
            }
            if (top == float.MinValue) return 0f;
            float low = mid.y + Height - 2.2f; // (the lowest of a hill's swing)
            return Mathf.Clamp(top + LiftClear - low, 0f, LiftMax);
        }
        internal override string Name { get { return Orders.Plans[Orders.Chase]; } }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { return pin.Player.transform.position + facing * new Vector3(pin.Munin ? -4f : 4f, Height, 0f); }

        // ---------------------------------------------------------------- the shapes
        internal const int Shapes = 4; // 0 the ring, 1 an eight, 2 a snake, 3 a hill
        internal static int NextShape(int now) { int n = Random.Range(0, Shapes - 1); return n >= now ? n + 1 : n; } // (never the same again)

        // a point of a shape round the centre (its long side along the axis), at phase ph
        internal static Vector3 Shape(int shape, Vector3 centre, Vector3 axis, float ring, float ph)
        {
            float r = Mathf.Clamp(ring, 3f, 8f);
            Vector3 z = Vector3.Cross(Vector3.up, axis);
            float a, b, y;
            switch (shape)
            {
                case 1: a = Mathf.Sin(ph) * r * 1.2f; b = Mathf.Sin(2f * ph) * r * 0.55f; y = Mathf.Sin(2f * ph) * 0.6f; break; // an eight
                case 2: a = Mathf.Sin(ph * 0.5f) * r * 1.3f; b = Mathf.Sin(ph * 2.5f) * r * 0.45f; y = Mathf.Sin(ph * 1.25f) * 0.5f; break; // a snake: to and fro, weaving
                default: a = Mathf.Cos(ph) * r; b = Mathf.Sin(ph) * r; y = Mathf.Sin(2f * ph) * 2.2f; break; // a hill: round, up and down
            }
            return centre + Vector3.up * (Height + y) + axis * a + z * b;
        }

        // where on a shape a point is (its angle round the centre: near enough to pick the shape up from)
        internal static float ShapeAt(Vector3 centre, Vector3 axis, Vector3 at)
        {
            Vector3 z = Vector3.Cross(Vector3.up, axis), rel = at - centre;
            return Mathf.Atan2(Vector3.Dot(rel, z), Vector3.Dot(rel, axis));
        }

        // the runner on a shape: on along it from where it is, at that speed (m/s)
        private static Vector3 OnShape(int shape, Vector3 centre, Vector3 axis, float ring, float dir, ref float phase, ref bool on, Vector3 at, float speed, float dt)
        {
            if (!on) { phase = ShapeAt(centre, axis, at); on = true; }
            phase += dir * speed * dt / (Mathf.Max(3f, ring) * 1.3f);
            return Shape(shape, centre, axis, ring, phase + dir * 0.6f);
        }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            int jslot;
            JointPlay.Game jg = JointPlay.ChaseOf(pin.Player, pin.Munin, out jslot);
            if (jg != null) { PairStep(m, pin, jg, jslot, carry, dt); return; } // with another player's raven (JointPlay)
            ChaseGame g = ChaseGame.Of(pin.Player);
            int me = pin.Munin ? 1 : 0, other = 1 - me;
            Vector3 at = pin.transform.position;
            g.Pos[me] = at; g.Vel[me] = m.Body != null ? m.Body.Vel : carry; g.Seen[me] = Time.time;
            // another player's two: flown after where their owner's game says each one is (its dice, not ours: the same
            // flight for everyone); nothing heard for a moment: our own reckoning meanwhile
            if (pin.Player != Player.m_localPlayer && g.Heard(me))
            {
                Follow(m, at, g.NetPos[me], g.NetVel[me], g.NetAt[me], RavenFlight.For(pin.Munin).Top * FollowMax, m.Body != null ? Mathf.Max(0.1f, m.Body.Pace) : 1f, carry, dt);
                m.Finish(dt, 0f);
                return;
            }
            if (pin.Player == Player.m_localPlayer) g.Send(me, at, g.Vel[me]);
            FlightStyle st = m.Body != null ? m.Body.Style : RavenFlight.For(pin.Munin);
            bool pair = Time.time - g.Seen[other] < 0.5f;
            float since = Time.time - g.SwappedAt;
            float pace = m.Body != null ? Mathf.Max(0.1f, m.Body.Pace) : 1f; // (its own pace: the motor puts it on every speed asked)
            Vector3 centre = pin.Player.transform.position + Vector3.up * g.LiftNow; // (over the roofs among houses)
            if (pair && g.MuninChases == pin.Munin)
            {
                // after it: aimed a little ahead of where it goes (never onto the spot itself: no braking on top of it),
                // and a little inside — it cuts across instead of following the runner round the bend, where the bend
                // rule would hold it back (a slower raven could never catch a faster one so)
                Vector3 run = g.Vel[other] - carry;
                Vector3 ahead = run.sqrMagnitude > 0.25f ? run.normalized : (g.Pos[other] - at).normalized;
                Vector3 aim = g.Pos[other] + run * Lead + ahead * 1.5f;
                float gap = (g.Pos[other] - at).magnitude; // (close to it: straight at it, or it would hang inside and never catch)
                bool shape = g.Shape != 0 && g.PhaseOn[other];
                Vector3 cut = shape ? Shape(g.Shape, centre, g.Axis, g.RingNow, g.Phase[other] + g.Dir * 0.9f) : centre + Vector3.up * Height;
                aim = Vector3.Lerp(aim, cut, (shape ? ShapeCut : CutIn) * Mathf.Clamp01((gap - 3f) / 5f));
                float want = g.Flee[other] + g.Edge + Mathf.Max(0f, since) * EdgeGrow; // m/s: what the runner really flies, and the edge
                float speed = Mathf.Min(want, Mathf.Max(st.Top * 1.2f * pace, g.Flee[other] + EdgeMax)) / pace;
                m.Steer(aim, speed, false, 1f, dt, carry, 1f); // (sharp: it turns hard after its prey)
                if (since > CatchAfter && (g.Pos[other] - at).sqrMagnitude < CatchAt * CatchAt) g.Swap();
            }
            else
            {
                // away along the shape (just turned the other way after a catch: the motor swings it round)
                g.Breathe(pin.Player, dt);
                float flee = st.Cruise * FleeShare * (1f + Burst * (1f - S(since / BurstSeconds)));
                g.Flee[me] = flee * pace; // (what it really flies: the chaser goes by that)
                Vector3 aim;
                if (g.Shape == 0)
                {
                    float t = pin.Clock + pin.Ring;
                    aim = CirclePlan.Ring(at, centre + Vector3.up * (Height + Mathf.Sin(t * 0.5f) * Swing), g.RingNow, 45f, g.Dir);
                }
                else aim = OnShape(g.Shape, centre, g.Axis, g.RingNow, g.Dir, ref g.Phase[me], ref g.PhaseOn[me], at, flee * pace, dt);
                m.Steer(aim, flee, false, 0f, dt, carry, 1f);
            }
            m.Finish(dt, 0f);
        }

        // the chase with another player's raven (JointPlay): round the middle between the two players, each at its own
        // speed (JointPlay.SpeedOf) — a faster runner gets away until cut off across the shape, and a chaser that has not
        // caught it for a while grows a little faster each second (PairCatchUp), so a round always ends. Each catch: the
        // roles change and so does the shape; on a shape other than the ring both fly at the slower one's speed. Now and
        // then the faster raven, fleeing, gives way (g.Give: it flies below the other) — the slower one catches it too.
        // Only the host's game counts a catch (JointPlay sends it to all, so every game changes the roles at once).
        internal static float PairRun = 0.85f, PairChase = 0.95f, PairGive = 0.8f; // × its speed fleeing / chasing; × the other's chase when giving way
        internal static float PairCatchUpAfter = 10f, PairCatchUp = 0.15f, PairCatchUpMax = 5f; // s a round before the chaser grows faster; m/s each second after; m/s at most
        private void PairStep(RavenMotor m, ShoulderPin pin, JointPlay.Game g, int me, Vector3 carry, float dt)
        {
            int other = 1 - me;
            Vector3 at = pin.transform.position;
            g.Pos[me] = at; g.Vel[me] = m.Body != null ? m.Body.Vel : carry; g.Seen[me] = Time.time;
            Vector3 middle;
            if (!g.Middle(out middle)) middle = pin.Player.transform.position; // (the other one not here: round its own)
            Vector3 axis = g.Axis();
            bool pair = Time.time - g.Seen[other] < 0.5f;
            float since = Time.time - g.SwappedAt;
            float pace = m.Body != null ? Mathf.Max(0.1f, m.Body.Pace) : 1f; // (the motor puts it on every speed asked: asked = real / pace)
            g.Breathe(middle, dt);
            // another player's raven: flown after where its owner's game says it is (the same flight for all)
            if (pin.Player != Player.m_localPlayer && g.Heard(me)) { PairFollow(m, g, me, at, pace, carry, since, dt); return; }
            if (pin.Player == Player.m_localPlayer) JointPlay.Send(g, me, at, g.Vel[me]);
            middle += Vector3.up * g.LiftNow; // (over the roofs among houses)
            float mine = g.Speed[me] > 0f ? g.Speed[me] : RavenFlight.For(pin.Munin).Top * pace, theirs = g.Speed[other] > 0f ? g.Speed[other] : mine;
            float own = g.Shape != 0 ? Mathf.Min(mine, theirs) : mine; // a shape: both at the slower one's speed
            if (pair && g.Chaser == me && since > 0f)
            {
                Vector3 run = g.Vel[other] - carry;
                Vector3 ahead = run.sqrMagnitude > 0.25f ? run.normalized : (g.Pos[other] - at).normalized;
                Vector3 aim = g.Pos[other] + run * Lead + ahead * 1.5f;
                float gap = (g.Pos[other] - at).magnitude; // (close to it: straight at it)
                bool shape = g.Shape != 0 && g.PhaseOn[other];
                Vector3 cut = shape ? Shape(g.Shape, middle, axis, g.RingNow, g.Phase[other] + g.Dir * 0.9f) : middle + Vector3.up * Height;
                aim = Vector3.Lerp(aim, cut, (shape ? ShapeCut : CutIn) * Mathf.Clamp01((gap - 3f) / 5f));
                float speed = own * PairChase + Mathf.Min(PairCatchUpMax, Mathf.Max(0f, since - PairCatchUpAfter) * PairCatchUp);
                m.Steer(aim, speed / pace, false, 1f, dt, carry, 1f);
                if (g.Host && !g.Pending && since > CatchAfter && (g.Pos[other] - at).sqrMagnitude < CatchAt * CatchAt) JointPlay.HostCaught(g, me);
            }
            else
            {
                float flee = own * PairRun * (1f + Burst * (1f - S(Mathf.Max(0f, since) / BurstSeconds)));
                if (g.Give == me) flee = Mathf.Min(flee, theirs * PairChase * PairGive); // gives way: lets the slower one catch it
                Vector3 aim;
                if (g.Shape == 0)
                {
                    float t = pin.Clock + pin.Ring;
                    aim = CirclePlan.Ring(at, middle + Vector3.up * (Height + Mathf.Sin(t * 0.5f) * Swing), g.RingNow, 45f, g.Dir);
                }
                else aim = OnShape(g.Shape, middle, axis, g.RingNow, g.Dir, ref g.Phase[me], ref g.PhaseOn[me], at, flee, dt);
                m.Steer(aim, flee / pace, false, 0f, dt, carry, 1f);
            }
            m.Finish(dt, 0f);
        }

        // after the place its owner's game sent: a little ahead of it (where it is by now, the send's age on), as fast
        // as it flies and faster while behind — never a jump (the motor eases it). Little swerving: its owner's flight
        // already went round what is there. The host still sees to the catches when this one is the chaser; the speed is
        // held to what this raven can fly (FollowMax), so a game sending false places cannot fly it onto the other one.
        internal static float FollowLead = 0.15f, FollowGain = 2.5f, FollowAvoid = 0.3f; // s ahead; m/s more for each m behind; share of the swerving
        internal static float FollowAhead = 0.4f, FollowMax = 1.6f;                     // s a sent place is carried on at most; × its own speed (+ the catch-up) at most
        private void PairFollow(RavenMotor m, JointPlay.Game g, int me, Vector3 at, float pace, Vector3 carry, float since, float dt)
        {
            int other = 1 - me;
            float top = (g.Speed[me] > 0f ? g.Speed[me] : RavenFlight.For(g.Munin[me]).Top) * FollowMax + PairCatchUpMax;
            Follow(m, at, g.NetPos[me], g.NetVel[me], g.NetAt[me], top, pace, carry, dt);
            if (g.Host && !g.Pending && g.Chaser == me && since > CatchAfter && Time.time - g.Seen[other] < 0.5f && (g.Pos[other] - at).sqrMagnitude < CatchAt * CatchAt) JointPlay.HostCaught(g, me);
            m.Finish(dt, 0f);
        }

        // steered after a place sent by its owner's game, at most top m/s (the pair chase and one owner's two alike)
        private static void Follow(RavenMotor m, Vector3 at, Vector3 pos, Vector3 vel, float sentAt, float top, float pace, Vector3 carry, float dt)
        {
            float age = Mathf.Min(Time.time - sentAt, FollowAhead);
            Vector3 there = pos + vel * (age + FollowLead);
            Vector3 dir = vel.sqrMagnitude > 0.25f ? vel.normalized : (there - at).normalized;
            float behind = Vector3.Distance(at, there);
            float speed = Mathf.Min(vel.magnitude + behind * FollowGain, top);
            m.Steer(there + dir * 0.8f, speed / pace, false, 1f, dt, carry, FollowAvoid);
        }
    }

    // the game of tag of one owner's two ravens: who is after whom, the shape, where each is (kept by the two plans;
    // one per owner, made when a game starts and dropped with its player)
    internal sealed class ChaseGame
    {
        internal Player Owner;
        internal bool MuninChases;
        internal float SwappedAt, Dir = 1f, Edge, Ring, RingNow, Lift, LiftNow; // (Lift: over the roofs round you, ChasePlan.RoofLift)
        internal int Shape;
        internal Vector3 Axis = Vector3.forward;       // the shapes' long side (the way you faced when the game began)
        internal readonly Vector3[] Pos = new Vector3[2], Vel = new Vector3[2];
        internal readonly float[] Seen = { -100f, -100f }, Flee = { 8f, 8f }, Phase = new float[2];
        internal readonly bool[] PhaseOn = new bool[2];
        private float _lookAt;
        private static readonly List<ChaseGame> Games = new List<ChaseGame>(2);
        // where another player's two are, as their owner's game sent it (PairFlyRpc), and when; when mine were sent
        internal readonly Vector3[] NetPos = new Vector3[2], NetVel = new Vector3[2];
        internal readonly float[] NetAt = { -100f, -100f }, SentAt = { -100f, -100f };
        internal bool Heard(int slot) { return Time.time - NetAt[slot] < JointPlay.FlyFresh; }
        private const string PairFlyRpc = "RavenWatch_PairFly";

        internal static ChaseGame Of(Player owner)
        {
            ChaseGame g = Find(owner);
            if (Time.time - Mathf.Max(g.Seen[0], g.Seen[1]) > 1f) g.Start(); // neither flew it a moment ago: a new game
            return g;
        }

        private static ChaseGame Find(Player owner)
        {
            ChaseGame g = null;
            for (int i = Games.Count - 1; i >= 0; i--)
            {
                if (Games[i].Owner == null) { Games.RemoveAt(i); continue; } // (its player gone)
                if (ReferenceEquals(Games[i].Owner, owner)) g = Games[i];
            }
            if (g == null) { g = new ChaseGame { Owner = owner }; Games.Add(g); }
            return g;
        }

        // my raven (slot 0 Hugin, 1 Munin): where it is now, to the games near (as JointPlay.Send)
        internal void Send(int slot, Vector3 pos, Vector3 vel)
        {
            if (ZRoutedRpc.instance == null || Time.time - SentAt[slot] < JointPlay.FlyEvery) return;
            SentAt[slot] = Time.time;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            ZPackage pkg = null;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || Vector3.Distance(o.transform.position, pos) > JointPlay.FlyRange) continue;
                long peer = JointPlay.PeerOf(o);
                if (peer == 0L) continue;
                if (pkg == null) { pkg = new ZPackage(); pkg.Write(slot); pkg.Write(pos); pkg.Write(vel); }
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, PairFlyRpc, pkg);
            }
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(PairFlyRpc, new System.Action<long, ZPackage>(OnFly));
        }

        private static void OnFly(long sender, ZPackage pkg)
        {
            int slot = pkg.ReadInt(); Vector3 pos = pkg.ReadVector3(), vel = pkg.ReadVector3();
            if (slot < 0 || slot > 1) return;
            Player owner = null; // (only its owner says where it is: the player whose game sent it)
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o != Player.m_localPlayer && JointPlay.PeerOf(o) == sender) { owner = o; break; }
            if (owner == null) return;
            ChaseGame g = Find(owner);
            g.NetPos[slot] = pos; g.NetVel[slot] = vel; g.NetAt[slot] = Time.time;
        }

        private void Start()
        {
            MuninChases = Random.value < 0.5f;
            Dir = Random.value < 0.5f ? 1f : -1f;
            Ring = RingNow = ChasePlan.RingTight;
            Shape = Random.Range(0, ChasePlan.Shapes);
            Vector3 f = Owner != null ? Owner.transform.forward : Vector3.forward;
            f.y = 0f;
            Axis = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            _lookAt = 0f;
            Round();
            SwappedAt += 1.5f; // off the shoulders first
        }

        // caught: the other one is after it now, it runs the other way, on another shape (and maybe a word on it: Orders)
        internal void Swap() { bool catcher = MuninChases; MuninChases = !MuninChases; Dir = -Dir; Shape = ChasePlan.NextShape(Shape); Round(); Orders.ChaseCaught(Owner, catcher); }

        private void Round()
        {
            SwappedAt = Time.time;
            Edge = Random.Range(ChasePlan.EdgeMin, ChasePlan.EdgeMax);
            PhaseOn[0] = PhaseOn[1] = false;
        }

        // the ring: as wide as wanted, narrower where something stands round you (8 rays every 3 s), eased
        internal void Breathe(Player owner, float dt)
        {
            if (Time.time >= _lookAt && owner != null)
            {
                _lookAt = Time.time + 3f;
                float want = Random.Range(ChasePlan.RingMin, ChasePlan.RingMax), room = want;
                Lift = ChasePlan.RoofLift(owner.transform.position, want);
                Vector3 mid = owner.transform.position + Vector3.up * (ChasePlan.Height + Lift);
                RaycastHit hit;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 d = Quaternion.AngleAxis(k * 45f, Vector3.up) * Vector3.forward;
                    if (Physics.Raycast(mid, d, out hit, want + 1.5f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) room = Mathf.Min(room, hit.distance - 1.5f);
                }
                Ring = Mathf.Max(ChasePlan.RingTight, room);
            }
            RingNow = Mathf.MoveTowards(RingNow, Ring, dt * 1.2f);
            LiftNow = Mathf.MoveTowards(LiftNow, Lift, dt * ChasePlan.LiftSpeed);
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

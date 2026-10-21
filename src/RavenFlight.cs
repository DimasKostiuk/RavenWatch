#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Система польоту» (the flight system): every raven flight goes through here (strike, dome, take-off, landing),
    // and new flights are built on it, not drawn as paths of their own.
    // How each raven flies: Hugin calm and wide (glides a lot, gentle banks), Munin quick and sharp (beats more,
    // steep turns). Speeds in m/s, acceleration m/s², turning °/s at speed, bank °, glide share 0..1, beat pace ×.
    internal sealed class FlightStyle
    {
        public float Cruise, Top, Accel, Turn, Bank, Glide, Beat;
    }

    // «Характер польоту»: how one raven handles itself in the air, beyond its speeds (FlightStyle): how hard and how
    // smoothly it turns, how wide it swings round, how soon its bank and nose follow, how restless it is with flight
    // templates, its figures and its manner in a strike. Hugin: calm, wide, smooth, a hunter stooping from above.
    // Munin: sharp, tight, quick, a brawler straight in and back again. The motor reads it instead of shared limits.
    // Trust shows in it: a young raven is still clumsy and twitchy and its character only half there; by trust 7 it
    // is wholly itself and smooth (RavenFlight.CharFor). Tuned in the test window, tab «Політ» (flight.txt).
    internal sealed class FlightCharacter
    {
        public float TurnCap, SharpCap;       // °/s: the hardest turn in ordinary flight / in a strike's last dive
        public float TurnAccel;               // °/s²: how fast its facing gathers and eases a turn
        public float CurveMin, CurveMax;      // m: the radius of its wide turns round (gentle → about-turn)
        public float BankEase, PitchEase;     // 1/s: how soon its bank and its nose follow
        public float Bend;                    // share of its turn rate it plans a bend with
        public float SwitchMul, KeepAdd;      // templates: × the director's pause between picks; + its chance to keep one
        public float TplSpeed, TplTurn, TplBank, TplBeat; // a template never takes it past these multipliers
        public float TrickTempo, TrickSize;   // figures: × their length, × their size
        public float TrickVary;               // figures: ± this share flown otherwise each time (size, tempo, the barrel's side, the loop's lean)
        // the strike: climbs this high over the prey and comes down onto it from above (0: straight in), swinging out
        // this far to the side on the way; after a blow up and back this far for the next dive, bounced this hard
        public float StoopUp, ArcSide, ReUp, ReBack, BounceBack, BounceUp, StrikeSharp;
        // trust 1 (fades out by 7): its aim wobbles this many metres, its body twitches this many degrees
        public float Wobble, Twitch;
        // landing (Landings.Pick): the approach (m added to the flight's, × its side), where braking starts (m), the
        // clip's pace (×), a tilt (°), and what it does once down (weights: nothing, a hop, a beat, a step aside, both)
        public float LandBackMin, LandBackMax, LandUpMin, LandUpMax, LandSideMin, LandSideMax, FlareMin, FlareMax, ClipMin, ClipMax, LandTilt;
        public float SettleNone, SettleHop, SettleFlap, SettleStep, SettleHopFlap;
        // take-off (Landings.PickTakeoff): the crouch (s, m), the push (×), off to the side (°), the unfold (s)
        public float CrouchMin, CrouchMax, CrouchDepth, PushMul, VeerMax, Unfold;

        internal FlightCharacter Copy() { return (FlightCharacter)MemberwiseClone(); }

        internal static readonly System.Reflection.FieldInfo[] Fields = typeof(FlightCharacter).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        internal static FlightCharacter Lerp(FlightCharacter a, FlightCharacter b, float t)
        {
            FlightCharacter r = new FlightCharacter();
            foreach (System.Reflection.FieldInfo f in Fields) { float x = (float)f.GetValue(a), y = (float)f.GetValue(b); f.SetValue(r, x + (y - x) * t); }
            return r;
        }
    }

    internal static class RavenFlight
    {
        internal static readonly FlightCharacter HuginChar = new FlightCharacter
        {
            TurnCap = 130f, SharpCap = 200f, TurnAccel = 160f, CurveMin = 5f, CurveMax = 8f, BankEase = 3f, PitchEase = 3f, Bend = 0.75f,
            SwitchMul = 2f, KeepAdd = 0.3f, TplSpeed = 1.1f, TplTurn = 1.15f, TplBank = 1.25f, TplBeat = 1.15f, TrickTempo = 1.15f, TrickSize = 1.15f, TrickVary = 0.08f,
            StoopUp = 3.5f, ArcSide = 2f, ReUp = 3f, ReBack = 2.2f, BounceBack = 0.3f, BounceUp = 4.5f, StrikeSharp = 2f, Wobble = 0.35f, Twitch = 5f,
            LandBackMin = -0.2f, LandBackMax = 1.2f, LandUpMin = -0.1f, LandUpMax = 0.9f, LandSideMin = 0.8f, LandSideMax = 1.3f, FlareMin = 2.6f, FlareMax = 4f,
            ClipMin = 0.85f, ClipMax = 1.02f, LandTilt = 6f, SettleNone = 1.4f, SettleHop = 0.6f, SettleFlap = 0.6f, SettleStep = 1f, SettleHopFlap = 0.2f,
            CrouchMin = 0.2f, CrouchMax = 0.32f, CrouchDepth = 0.025f, PushMul = 0.9f, VeerMax = 12f, Unfold = 0.08f
        };
        internal static readonly FlightCharacter MuninChar = new FlightCharacter
        {
            TurnCap = 220f, SharpCap = 280f, TurnAccel = 420f, CurveMin = 2.5f, CurveMax = 5f, BankEase = 7f, PitchEase = 6f, Bend = 0.9f,
            SwitchMul = 0.75f, KeepAdd = -0.1f, TplSpeed = 1.35f, TplTurn = 1.6f, TplBank = 1.6f, TplBeat = 1.5f, TrickTempo = 0.9f, TrickSize = 0.9f, TrickVary = 0.14f,
            StoopUp = 0f, ArcSide = 0f, ReUp = 0.9f, ReBack = 0.5f, BounceBack = 0.15f, BounceUp = 2.5f, StrikeSharp = 3.5f, Wobble = 0.5f, Twitch = 8f,
            LandBackMin = -0.3f, LandBackMax = 0.8f, LandUpMin = -0.3f, LandUpMax = 0.6f, LandSideMin = 0.6f, LandSideMax = 1.1f, FlareMin = 2f, FlareMax = 3.2f,
            ClipMin = 0.98f, ClipMax = 1.2f, LandTilt = 16f, SettleNone = 0.6f, SettleHop = 1.2f, SettleFlap = 1f, SettleStep = 0.8f, SettleHopFlap = 0.8f,
            CrouchMin = 0.1f, CrouchMax = 0.18f, CrouchDepth = 0.035f, PushMul = 1.15f, VeerMax = 25f, Unfold = 0.04f
        };
        internal static FlightCharacter CharOf(bool munin) { return munin ? MuninChar : HuginChar; }
        internal static float TrustShows = 0.35f; // share of its character a trust-1 raven already has (the rest comes by trust 7)

        // this raven's character at this trust: between the two ravens' middle and its own, by trust; the clumsiness
        // (wobble, twitch, restless templates, snatchy turns) fades out as trust grows
        internal static FlightCharacter CharFor(bool munin, int level)
        {
            FlightCharacter own = CharOf(munin), mid = FlightCharacter.Lerp(HuginChar, MuninChar, 0.5f);
            float m = Mathf.Clamp01((level - 1) / 6f);
            FlightCharacter c = FlightCharacter.Lerp(mid, own, Mathf.Lerp(TrustShows, 1f, m));
            float clumsy = 1f - m;
            c.Wobble = own.Wobble * clumsy; c.Twitch = own.Twitch * clumsy;
            c.TurnAccel *= 1f + 0.5f * clumsy;   // snatchy: it jerks into and out of its turns
            c.BankEase *= 1f + 0.6f * clumsy;
            c.SwitchMul *= 1f - 0.4f * clumsy;   // restless: it changes its way of flying more often
            c.TrickVary *= 1f + 0.5f * clumsy;   // and never flies a figure twice alike
            return c;
        }
        // … and this one raven's own touch on it (Norov: its character, as far as its trust shows it)
        internal static FlightCharacter CharFor(bool munin, int level, Norov.Character norov)
        {
            FlightCharacter c = CharFor(munin, level);
            Norov.Shape(c, norov, level);
            return c;
        }
        internal static int CharVersion; // bumped by the panel
        internal static readonly FlightStyle Hugin = new FlightStyle { Cruise = 9f, Top = 13f, Accel = 13f, Turn = 160f, Bank = 40f, Glide = 0.7f, Beat = 0.57f }; // slower, calmer beats (−40%)
        internal static readonly FlightStyle Munin = new FlightStyle { Cruise = 11f, Top = 15f, Accel = 19f, Turn = 240f, Bank = 60f, Glide = 0.2f, Beat = 1.15f };

        internal static FlightStyle For(bool munin) { return munin ? Munin : Hugin; }

        // One raven's own way of flying (RavenMotor): its kind's style with a small stable personal touch (seed: its
        // owner) and its trust (nerves above 1: quicker turns and beats, fewer glides; below 1: calmer)
        internal static FlightStyle Personal(bool munin, int seed, float nerves)
        {
            FlightStyle b = For(munin);
            System.Random r = new System.Random(seed);
            float sp = RavenMotor.Spread, n = (nerves - 1f) * RavenMotor.NervesWeight;
            System.Func<float> k = () => 1f + sp * ((float)r.NextDouble() * 2f - 1f);
            return new FlightStyle
            {
                Cruise = b.Cruise * k(), Top = b.Top * k(), Accel = b.Accel * k() * (1f + n * 0.5f), Turn = b.Turn * k() * (1f + n),
                Bank = b.Bank * k(), Glide = Mathf.Clamp01(b.Glide * k() * (1f - n)), Beat = b.Beat * k() * (1f + n * 0.5f)
            };
        }

        // The landing panel's nudges to the way in, by distance off the seat (4, 2, 1, 0.5 m), in the owner's frame
        // (x right, y up, z forward): nothing beyond 6 m, nothing at the seat (the landing's own nudges take over)
        internal static readonly float[] FixAt = { 4f, 2f, 1f, 0.5f };
        internal static readonly Vector3[] FlyFixes = new Vector3[4];
        internal static Vector3 FlyFix(float d)
        {
            if (d >= 6f) return Vector3.zero;
            if (d >= FixAt[0]) return Vector3.Lerp(FlyFixes[0], Vector3.zero, (d - FixAt[0]) / (6f - FixAt[0]));
            for (int i = 0; i < FixAt.Length - 1; i++)
                if (d >= FixAt[i + 1]) return Vector3.Lerp(FlyFixes[i + 1], FlyFixes[i], (d - FixAt[i + 1]) / (FixAt[i] - FixAt[i + 1]));
            return Vector3.Lerp(Vector3.zero, FlyFixes[FixAt.Length - 1], d / FixAt[FixAt.Length - 1]);
        }
        internal static Vector3 FlyFix(float d, Player owner)
        {
            if (owner == null) return Vector3.zero;
            Vector3 f = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up);
            return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized, Vector3.up) * FlyFix(d) : Vector3.zero;
        }

        // what each raven coming home is doing now, for the panel: distance off the seat (-1: not coming home), speed
        internal static readonly float[] NowDist = { -1f, -1f };
        internal static readonly float[] NowSpeed = { 0f, 0f };
        internal static readonly bool[] NowFinal = { false, false };
        internal static void Note(bool munin, float dist, float speed, bool final) { int i = munin ? 1 : 0; NowDist[i] = dist; NowSpeed[i] = speed; NowFinal[i] = final; }

        // braking before it lands: wings forward and beating hard, nose up, legs out
        internal static float FlareDistance = 3f;
        // from this far off the owner it starts turning to face their way (for the landing)
        internal static float TurnFrom = 6f;
    }

    // A raven in the air as a body with speed: it speeds up and slows down, cannot turn on the spot at speed (so it
    // swings round in curves), banks into its turns by how hard it turns, lifts the nose climbing and drops it
    // diving. The wings follow what the body does: hard beats climbing or speeding up, slower beats in level flight,
    // held in a dive. The model stays the shoulder raven (ShoulderLife).
    internal sealed class FlightBody
    {
        private readonly Transform _t;
        private readonly ShoulderLife _life;
        private FlightStyle _s;
        internal Vector3 Vel;
        private Vector3 _acc;
        private float _bank, _pitch, _yaw;
        private float _yawRate;
        private float _curveRadiusScale = 1f;
        private bool _wasCurvedTurn;
        private float _cycleUntil;
        private float _clock;       // its own time (the wings' beat-and-glide rhythm), so a slowed picture slows it too
        private bool _gliding, _working;
        internal bool NoGlide; // the test picture: beats all the time (no glides, no held dive), so the stroke is always seen
        private float _age;         // seconds in the air: the take-off is gentle, the full pace comes after
        private bool _final;        // coming home: past the approach, now onto the shoulder
        private float _hintYaw = float.NaN, _hintWeight; // which way it should face now (landing: the owner's way), and how much
        private float _seatYaw = float.NaN;               // the owner's way, last told (the landing waits until it faces near it)
        private readonly bool _munin;

        // leaving the shoulder: it pushes off softly and gathers speed over this long (hard beats meanwhile)
        internal static float LaunchSeconds = 1.2f;
        internal float LaunchDuration { get; private set; }
        private float _launchBeat;
        internal static float TurnBelow = 0.8f; // m/s: slower than this it turns to face your way for landing
        internal float Gust;           // 0..1 how hard the wind throws it about now (the motor sets it)
        internal static float GustRoll = 6f; // ° it rolls in a full gale
        internal LandStyle Land;       // this landing's own way (Landings.Pick, set by the motor on the way home)
        internal FlightCharacter Char; // its character (RavenFlight.CharFor, set by the motor): how it turns, banks, swings round
        internal static float BrakeGain = 3f; // m/s per metre left, in the last 3 m of an arrival
        // In flight it slows down only by its drag and a little check of the wings: this share of how hard it speeds up.
        // Coming in to stop (arrive) and a strike's last dive (sharp) brake fully. Its speed carries on after a figure
        // or when a plan wants less (no stop in mid-air).
        internal static float Coast = 0.35f;
        internal static float HoverBeat = 1.15f; // × its beat pace while hovering to land: steady, holding itself up
        // Coming home it comes in from the outer side of its own shoulder (Hugin right, Munin left), a little above
        // it: in sight of the camera behind you and away from it (a bird passing close by the camera seems to spin),
        // turning to face your way over the last metre. Back > 0 is behind you.
        internal static float ApproachBack = 0.3f, ApproachUp = 0.7f, ApproachSide = 1.6f;

        internal float Speed { get { return Vel.magnitude; } }
        internal float Age { get { return _age; } } // seconds in the air (a takeover in the air keeps counting)
        internal float AccelMul = 1f; // below 1: more inertia (a strike's flight swings wider)
        // it met something (Motor.ClearMove): the part of its speed into the surface is gone, along it goes on
        internal void Slide(Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.0001f) return;
            float into = Vector3.Dot(Vel, normal);
            if (into < 0f) Vel -= normal * into;
        }
        // a long way through the air rides up and down in waves (as over the dome), calming near the end
        internal Vector3 Wave(float near, float seed)
        {
            float fade = Mathf.Clamp01((near - 4f) / 6f);
            if (fade <= 0f) return Vector3.zero;
            float amp = _munin ? 0.6f : 1f, hz = _munin ? 0.6f : 0.4f;
            return Vector3.up * Mathf.Sin((_clock + seed) * hz * Mathf.PI * 2f) * amp * fade;
        }
        internal FlightStyle Style { get { return _s; } set { if (value != null) _s = value; } }

        // a new way for the same flight (another driver took it): its speed and pace stay, the way home starts afresh
        internal void NewLeg() { _final = false; _nearSince = -1f; _hintYaw = float.NaN; _hintWeight = 0f; _seatYaw = float.NaN; ResetLandWait(); }

        internal static bool QuickLaunch(string plan)
        {
            return plan == "удар" || plan == "купол" || plan == "пікірування" || plan == "бочка" || plan == "петля";
        }

        internal static float LaunchDurationFor(string plan)
        {
            float factor = plan == "слідом" || plan == "коло" || plan == "танець" || plan == "на жердину" // (the scouting is a quick dart: its own pace)
                ? 1.45f
                : QuickLaunch(plan) ? 0.65f : 1f;
            return Mathf.Clamp(LaunchSeconds * factor, 0.2f, 3f);
        }

        internal FlightBody(Transform t, ShoulderLife life, bool munin, Vector3 vel, string plan)
        {
            _t = t; _life = life; _s = RavenFlight.For(munin); Char = RavenFlight.CharOf(munin); Vel = vel; _munin = munin;
            ResetLandWait();
            LaunchDuration = LaunchDurationFor(plan);
            _launchBeat = QuickLaunch(plan) ? 1f : plan == "слідом" || plan == "коло" || plan == "танець" ? 0.3f : 0.6f;
            Quaternion facing = t.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 f = facing * Vector3.forward;
            _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            _cycleUntil = Random.Range(0.6f, 1.2f);
        }

        // One frame towards 'target' at up to 'speed'. 'arrive': slows down to stop on it; 'sharp' (0 = its own)
        // lets it turn harder, for the last metres of a dive at prey. Returns the distance left.
        internal float Toward(Vector3 target, float speed, bool arrive, float sharp, float dt) { return Toward(target, speed, arrive, sharp, dt, Vector3.zero); }

        // the same, to a target that itself moves ('carry': its speed, e.g. the running owner): it stops on it
        // matching its pace, not standing still in the air behind it
        internal float Toward(Vector3 target, float speed, bool arrive, float sharp, float dt, Vector3 carry)
        {
            if (dt <= 0f) return Vector3.Distance(_t.position, target);
            _age += dt;
            if (_kickLeft > 0f)
            {
                _kickLeft -= dt;
                Vel = Vector3.MoveTowards(Vel, _kickTo, _kickRate * dt);
                if (_kickLeft > 0f) { _acc = Vector3.Lerp(_acc, Vector3.zero, Mathf.Clamp01(dt * 8f)); _t.position += Vel * dt; return Vector3.Distance(_t.position, target); } // the knock has it this moment
            }
            float launch = Mathf.Clamp01(_age / Mathf.Max(0.1f, LaunchDuration));
            launch = launch * launch * (3f - 2f * launch);
            speed *= Mathf.Lerp(0.45f, 1f, launch);
            Vector3 to = target - _t.position;
            float dist = to.magnitude;
            float want = speed;
            if (arrive) want = Mathf.Min(speed, Mathf.Sqrt(2f * _s.Accel * (dist < 2f ? 0.4f : 0.7f) * Mathf.Max(0f, dist - 0.05f)) + 0.1f); // can still stop in time (the last metres gently)
            if (arrive && dist < 3f) want = Mathf.Min(want, dist * BrakeGain + 0.05f); // and softly into the very spot, no overshoot
            want = Mathf.Min(want, BendSpeed(to, dist, carry, sharp, arrive));
            Vector3 desired = (dist > 0.001f ? to / dist * want : Vector3.zero) + carry;
            float maxA = _s.Accel * (arrive && dist < 3f ? 1.8f : 1f) * (1f + sharp * 0.5f) * Mathf.Lerp(0.5f, 1f, launch) * AccelMul;
            Vector3 currentFlat = Vector3.ProjectOnPlane(Vel, Vector3.up);
            Vector3 desiredFlat = Vector3.ProjectOnPlane(desired - carry, Vector3.up);
            bool broadTurn = sharp < 1f && currentFlat.sqrMagnitude > 0.25f && desiredFlat.sqrMagnitude > 0.25f;
            float reversal = broadTurn ? Mathf.InverseLerp(65f, 155f, Vector3.Angle(currentFlat, desiredFlat)) : 0f;
            bool curvedTurn = broadTurn && reversal > 0.01f && (!arrive || dist > 3f);
            if (curvedTurn && !_wasCurvedTurn) _curveRadiusScale = Random.Range(0.85f, 1.15f);
            _wasCurvedTurn = curvedTurn;
            Vector3 nv;
            if (curvedTurn)
            {
                float currentSpeed = Vel.magnitude;
                // round a corner it keeps its speed; coming in to stop (arrive) it brakes along the curve all the same,
                // or it would sweep past the spot at full speed
                float targetSpeed = arrive ? desired.magnitude : Mathf.Max(desired.magnitude, currentSpeed * reversal);
                float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (targetSpeed < currentSpeed && !arrive && sharp <= 0f ? maxA * Coast : maxA) * dt);
                float radius = Mathf.Lerp(Char.CurveMin, Char.CurveMax, reversal) * _curveRadiusScale; // Hugin swings round wide, Munin tight
                float curveRate = nextSpeed / Mathf.Max(0.1f, radius) * Mathf.Rad2Deg;
                Vector3 direction = Vector3.RotateTowards(Vel.normalized, desired.normalized, curveRate * Mathf.Deg2Rad * dt, maxA * dt);
                nv = direction * nextSpeed;
            }
            else
            {
                Vector3 dv = Vector3.ClampMagnitude((desired - Vel) / 0.25f, maxA) * dt;
                if (!arrive && sharp <= 0f && Vel.sqrMagnitude > 1f)
                {
                    // what of it slows the bird down: no more than its drag (Coast); turning it stays as it was
                    Vector3 dir = Vel.normalized;
                    float along = Vector3.Dot(dv, dir), most = maxA * Coast * dt;
                    if (along < -most) dv -= dir * (along + most);
                }
                nv = Vel + dv;
            }
            // a bird swings round: at speed it turns only so fast (slow, it can turn sharply)
            float sp = nv.magnitude;
            if (!curvedTurn && sp > 2f && Vel.sqrMagnitude > 1f)
                nv = Vector3.RotateTowards(Vel, nv, TurnNow(sp, sharp) * Mathf.Deg2Rad * dt, maxA * dt);
            _acc = Vector3.Lerp(_acc, (nv - Vel) / dt, Mathf.Clamp01(dt * 8f));
            Vel = nv;
            _t.position += Vel * dt;
            return Vector3.Distance(_t.position, target);
        }

        // how fast it can turn now (°/s) at this speed: its own style, quicker when slow, never past the motor's limit,
        // gentler just after the take-off
        private float TurnNow(float sp, float sharp)
        {
            float turn = _s.Turn * (1f + sharp) * Mathf.Lerp(2.5f, 1f, Mathf.InverseLerp(2f, _s.Top, sp)) * Mathf.Lerp(1f, AccelMul, 0.5f);
            float turnLimit = Mathf.Lerp(Char.TurnCap, Char.SharpCap, Mathf.Clamp01(sharp * 0.5f));
            float takeoffEase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / 0.85f));
            return Mathf.Min(turn * Mathf.Lerp(0.55f, 1f, takeoffEase), Mathf.Lerp(90f, turnLimit, takeoffEase));
        }

        // A bend ahead: no faster than it can turn round it, the one rule for every plan (a ring, an eight, the way
        // home), so nothing swings out wide and wanders back. The bend is the arc from where it heads now to the aim
        // (r = distance / 2 sin(angle off)). Not for the wide turns (the curved turn has its own radius), not for the
        // last metres of a landing (braking already) and not for a strike's last dive at prey (sharp).
        internal static bool BendLimit = true;
        internal static float BendFloor = 3f;     // m/s: it never slows below this for a bend
        private float BendSpeed(Vector3 to, float dist, Vector3 carry, float sharp, bool arrive)
        {
            if (!BendLimit || sharp >= 1f || (arrive && dist < 3f) || dist < 0.5f) return float.MaxValue;
            Vector3 rel = Vel - carry; // against a moving aim (the running owner) the bend is in its frame
            float sp = rel.magnitude;
            if (sp < 1f) return float.MaxValue;
            float off = Vector3.Angle(rel, to);
            if (off < 5f || off > 65f) return float.MaxValue;
            float r = dist / (2f * Mathf.Sin(off * Mathf.Deg2Rad));
            return Mathf.Max(BendFloor, r * TurnNow(sp, sharp) * Char.Bend * Mathf.Deg2Rad);
        }

        // Home to a running owner: always a little faster than them, and the longer the chase the faster
        // (from 4 s on, up to +70% by 10 s), so it catches up instead of trailing behind for ever
        internal static float ChaseFrom = 4f, ChaseFull = 10f, ChaseBoost = 0.7f;
        internal float HomeSpeed(float baseSpeed, Vector3 ownerVel, float chasing)
        {
            float s = Mathf.Max(baseSpeed, ownerVel.magnitude * 1.25f + 3f);
            return s * (1f + Mathf.Clamp01((chasing - ChaseFrom) / Mathf.Max(0.1f, ChaseFull - ChaseFrom)) * ChaseBoost);
        }

        // Where to fly when going home: first a point out to the side of its shoulder (Munin left, Hugin right), then,
        // once there or already behind the owner, the hover point over the seat. 'Final' says which.
        internal bool Final { get { return _final; } }
        internal Vector3 HomeAim(Vector3 shoulder, Player owner)
        {
            if (owner == null) _final = true;
            if (_final) return OverSeat(shoulder, owner);
            Vector3 fwd = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd) * (_munin ? -1f : 1f);
            float back = Land != null ? Land.Back : ApproachBack, up = Land != null ? Land.Up : ApproachUp, out_ = Land != null ? Land.Side : ApproachSide;
            Vector3 approach = shoulder - fwd * back + Vector3.up * up + side * out_; // (this landing's own way in: Landings)
            Vector3 rel = _t.position - shoulder;
            // already behind you: it comes straight in from there, facing your way (no round to the side point,
            // which it would only overshoot and come back from). Only near and in sight: the final flies straight,
            // through anything (no wall checks), so from afar or behind a wall it would cut through the house
            bool near = rel.sqrMagnitude < FinalReach * FinalReach && !RavenMotor.Walled(_t.position, shoulder + Vector3.up * LandAbove);
            if (near && Vector3.Dot(rel, fwd) < -0.5f) _final = true;
            if (Vector3.Distance(_t.position, approach) < 1f) _final = true; // facing your way is the FaceHint's job
            if (!_final && near && RavenMotor.Walled(shoulder, approach)) _final = true; // you stand by a wall: that side is no way in
            return _final ? OverSeat(shoulder, owner) : approach;
        }
        internal static float FinalReach = 6f; // m: the straight last way in only from this near (and in sight)
        // you went round a corner while it came in straight: it flies by its eyes again (walls, the way round)
        internal void Unfinal() { _final = false; _nearSince = -1f; ResetLandWait(); }

        // how far its facing is from this direction, in degrees (the landing log)
        internal float YawOff(Vector3 forward) { return Mathf.Abs(Mathf.DeltaAngle(_yaw, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg)); }

        // close enough and at the owner's pace: it can sit down
        private float _nearSince = -1f; // since when (its own time) it has been close to the seat
        private float _landReadyAt = -1f;
        private float _landStagger;
        internal static float LandReach = 0.45f; // m off the point over the seat where the game's own landing takes over
        // like a helicopter, not a plane: it brakes to a hover by that point, turns there to face your way, and only
        // then the game's landing sets it down (the switch waits until it is this still and this well turned)
        internal static float HoverStill = 1.8f, HoverTurned = 35f, HoverWait = 0.8f; // a short hover (the user: it hung too long)
        // the last way in goes to a point a little above and behind the seat; from there the game's landing drops
        // it down and forward onto the shoulder (as a bird comes down onto a perch), not along it
        internal static float LandAbove = 0.3f, LandBehind = 0.4f; // the recordings: 0.5 m up had it switch over the head
        internal static float LandOut = 0.2f; // and this far out to its own side: over the shoulder, not behind the head
        private Vector3 OverSeat(Vector3 seat, Player owner)
        {
            if (owner == null) return seat + Vector3.up * LandAbove;
            Vector3 f = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, f) * (_munin ? -1f : 1f);
            return seat + Vector3.up * LandAbove - f * LandBehind + side * LandOut;
        }
        internal bool CanLand(float left, Vector3 ownerVel)
        {
            if (left >= 1.5f) _nearSince = -1f; else if (_nearSince < 0f) _nearSince = _clock;
            float nearFor = _nearSince < 0f ? 0f : _clock - _nearSince;
            // the game's landing takes it from here, once it is near, slowed and nearly facing your way (or has tried a while)
            float rel = (Vel - ownerVel).magnitude;
            float off = float.IsNaN(_seatYaw) ? 0f : Mathf.Abs(Mathf.DeltaAngle(_yaw, _seatYaw));
            bool ready = (left < LandReach && rel < HoverStill && off < HoverTurned) || nearFor > HoverWait;
            if (!_final || !ready) return false;
            if (_landReadyAt < 0f) _landReadyAt = _clock + _landStagger;
            return _clock >= _landReadyAt;
        }

        private void ResetLandWait()
        {
            _landReadyAt = -1f;
            _landStagger = Random.Range(0f, _munin ? 0.2f : 0.15f); // a breath of its own; the two ravens apart: Landings.MayTouch
        }

        // driven by hand a moment (a trick): its facing taken from where the model now points, no bank or pitch left over
        internal void Synced()
        {
            Quaternion facing = _t.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 f = facing * Vector3.forward;
            if (new Vector2(f.x, f.z).sqrMagnitude > 0.0001f) _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            // its nose and roll as the figure has them now (upright): the flight after it goes on from there, no snap level
            Vector3 up = facing * Vector3.up;
            if (up.y > 0.3f)
            {
                Vector3 e = facing.eulerAngles;
                _pitch = -Mathf.DeltaAngle(0f, e.x); _bank = -Mathf.DeltaAngle(0f, e.z);
            }
            else { _bank = 0f; _pitch = 0f; }
            _yawRate = 0f; // the turn it had before the figure is not carried on after it
            // whoever flies it next (the figure over early, a strike), it turns from how it is now, not snapped level
            _heldRot = facing; _recover = 1f;
        }

        // driven by hand a frame (a figure): its speed and pull from the way it moved; its time in the air counts on
        // (or a figure flown right off the shoulder would beat as at the take-off and be slowed after it)
        internal void Drove(Vector3 vel, float dt)
        {
            if (dt <= 0f) return;
            _age += dt;
            _acc = Vector3.ClampMagnitude(Vector3.Lerp(_acc, (vel - Vel) / dt, Mathf.Clamp01(dt * 8f)), 60f);
            Vel = vel;
        }
        private Quaternion _heldRot = Quaternion.identity;
        private float _recover;            // 1 → 0: how much of the figure's last turn is still in its facing
        internal static float RecoverSeconds = 0.4f;

        // already flying when it appears (a fly-in from afar): no gentle take-off
        internal void Airborne() { _age = LaunchDuration; }
        // straight onto the seat, no round behind the owner (down from hanging over the shoulder)
        internal void MarkFinal() { _final = true; }

        // a knock (the blow on prey): bounced back and up
        internal void Bounce(float back, float up) { Kick(-Vel * back + Vector3.up * up); }

        // a knock changes its way over a moment (KickSeconds), not in one frame: the speed eases to the new one
        internal static float KickSeconds = 0.12f;
        private Vector3 _kickTo;
        private float _kickLeft, _kickRate;
        internal void Kick(Vector3 to)
        {
            _kickTo = to; _kickLeft = KickSeconds;
            _kickRate = (to - Vel).magnitude / Mathf.Max(0.01f, KickSeconds);
        }

        // Landing: face this way (the owner's), the more the closer. Slow and near, the way it drifts means nothing
        // (tiny corrections forward and back would spin it round); it settles facing as it will sit. Set each frame.
        internal void FaceHint(Vector3 forward, float weight)
        {
            Vector3 f = new Vector3(forward.x, 0f, forward.z);
            if (f.sqrMagnitude < 0.0001f) return;
            _hintYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            _hintWeight = Mathf.Clamp01(weight);
            _seatYaw = _hintYaw;
        }

        // facing the way it flies, banked into the turn, nose with the climb (and up when braking)
        internal void Face(float dt, float flare)
        {
            Vector3 flat = new Vector3(Vel.x, 0f, Vel.z);
            float hs = flat.magnitude;
            float moving = Mathf.Clamp01((hs - 0.5f) / 2.5f); // how much its drift tells where it heads
            float velYaw = hs > 0.01f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : _yaw;
            float want = Mathf.LerpAngle(_yaw, velYaw, moving);
            float hw = 0f;
            // it faces the way it flies while it flies (turning your way at speed, it came in sideways, drifting);
            // only once it has slowed to the hover does it turn round to face your way
            float slowed = 1f - Mathf.Clamp01((hs - TurnBelow) / 2.2f);
            if (!float.IsNaN(_hintYaw)) { hw = Mathf.Max(_hintWeight * slowed, 1f - moving); want = Mathf.LerpAngle(want, _hintYaw, hw); }
            float launchEase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / 0.85f));
            float calm = Char.TurnCap / 175f; // its own pace of turning (Hugin slower, Munin quicker)
            float yawRate = Mathf.Lerp(90f, 240f, launchEase) * calm;
            yawRate = Mathf.Lerp(yawRate, 420f * calm, hw);
            float yawError = Mathf.DeltaAngle(_yaw, want);
            float yawSign = Mathf.Sign(yawError);
            float stoppingRate = Mathf.Sqrt(Mathf.Max(0f, 2f * Char.TurnAccel * Mathf.Abs(yawError)));
            float rateTarget = yawSign == 0f ? 0f : yawSign * Mathf.Min(yawRate, stoppingRate);
            _yawRate = Mathf.MoveTowards(_yawRate, rateTarget, Char.TurnAccel * dt);
            float yawStep = _yawRate * dt;
            if (Mathf.Abs(yawStep) > Mathf.Abs(yawError)) { _yaw = want; _yawRate = 0f; }
            else _yaw = Mathf.Repeat(_yaw + yawStep + 180f, 360f) - 180f;
            _hintYaw = float.NaN; _hintWeight = 0f;
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            float lat = Vector3.Dot(_acc, right);
            // landing: the nearer the seat, the more it already sits as it will sit (level, no bank), so nothing is
            // left to straighten once it touches
            float settle = flare * flare * (3f - 2f * flare);
            float bankWant = Mathf.Clamp(Mathf.Atan2(lat, 9.81f) * Mathf.Rad2Deg * 1.3f, -_s.Bank, _s.Bank) * (1f - settle);
            _bank = Mathf.Lerp(_bank, bankWant, Mathf.Clamp01(dt * (Char.BankEase + settle * 5f)));
            float pitchWant = (Mathf.Clamp(Mathf.Atan2(Vel.y, Mathf.Max(hs, 1.5f)) * Mathf.Rad2Deg * 0.7f, -40f, 28f) + flare * 20f) * (1f - settle);
            _pitch = Mathf.Lerp(_pitch, pitchWant, Mathf.Clamp01(dt * (Char.PitchEase + settle * 5f)));
            // Euler: yaw, then pitch (minus = nose up), then roll (minus = right wing down, into a right turn)
            // a young raven twitches in the air (fading with trust; never on the way onto the seat)
            float tw = (Char.Twitch + Gust * GustRoll) * (1f - settle); // a young raven's twitch, the wind's buffeting
            float twRoll = 0f, twYaw = 0f;
            if (tw > 0.05f)
            {
                float s0 = _munin ? 5.3f : 1.7f;
                twRoll = (Mathf.PerlinNoise(_clock * 1.6f, s0) - 0.5f) * 2f * tw;
                twYaw = (Mathf.PerlinNoise(s0, _clock * 1.3f) - 0.5f) * tw;
            }
            Quaternion rot = Quaternion.Euler(-_pitch, _yaw + twYaw, -_bank - twRoll);
            if (_recover > 0f)
            {
                _recover = Mathf.Max(0f, _recover - dt / Mathf.Max(0.05f, RecoverSeconds));
                float w = _recover * _recover * (3f - 2f * _recover);
                rot = Quaternion.Slerp(rot, _heldRot, w);
            }
            _t.rotation = rot * Quaternion.Euler(Shoulder.Rotation);
        }

        // what the wings do now, from what the body does
        internal int WingMode; // a figure's say for this frame: 0 as the flight goes, 1 gliding, 2 hard beats
        internal void Wings(float dt, float flare)
        {
            if (_life == null) return;
            _clock += dt;
            float sp = Vel.magnitude;
            float push = sp > 0.1f ? Vector3.Dot(_acc, Vel / sp) : 0f;
            float effort = Mathf.Clamp01(Vel.y / 3f) + Mathf.Clamp01(push / (_s.Accel * 0.5f)) + flare + (sp < 4f ? 0.6f : 0f) + Gust * 0.35f;
            float rate;
            if (_life.Motor != null && _life.Motor.DivePoseTest) { _working = false; _gliding = false; rate = _s.Beat; }
            else if (WingMode == 1) { _working = false; _gliding = true; rate = Mathf.Max(_s.Beat * 0.8f, 0.45f); }
            else if (WingMode == 2) { _working = true; _gliding = false; rate = _s.Beat * 1.7f; }
            else if (flare > 0.5f) { _working = false; _gliding = false; rate = _s.Beat * HoverBeat; } // hovering to land: steady beats
            else if (_age < LaunchDuration && _life.Flight.Dive < 0.25f)
            {
                _working = false;
                _gliding = false;
                float u = Mathf.Clamp01(_age / Mathf.Max(0.1f, LaunchDuration));
                float ease = u * u * (3f - 2f * u);
                rate = _s.Beat * (1.1f + _launchBeat * 0.55f * (1f - ease));
            }
            else if (_life.Flight.Dive > 0.25f || (Vel.y < -2.5f && sp > 7f)) // (the dive pose test is taken above)
            {
                _working = false;
                _gliding = false;
                rate = _s.Beat * 0.28f;
            }
            else if (effort > 0.35f)
            {
                _working = true;
                _gliding = false;
                rate = _s.Beat * (1f + Mathf.Clamp01(effort) * 0.8f);
            }
            else if (NoGlide) { _working = false; _gliding = false; rate = _s.Beat; }
            else
            {
                if (_working) { _working = false; _cycleUntil = _clock + Random.Range(0.8f, 1.5f); }
                if (_clock >= _cycleUntil)
                {
                    // beats, then a glide now and then (Hugin glides far more often than Munin)
                    _gliding = !_gliding && Random.value < (_s.Glide + 0.2f) * (1f - Gust * 0.6f); // (a gale: it hardly glides)
                    _cycleUntil = _clock + (_gliding ? Random.Range(0.6f, 1.4f) * (0.6f + _s.Glide) : Random.Range(0.9f, 1.8f) * (1.4f - _s.Glide));
                }
                // A stopped clip can freeze the wings at the bottom of a stroke. Gliding slows the
                // beat instead of pausing it, with a floor that stays safe across templates.
                rate = _gliding ? Mathf.Max(_s.Beat * 0.8f, 0.45f) : _s.Beat;
            }
            _life.FlapRate = Mathf.MoveTowards(_life.FlapRate, rate, dt * 4f);
            _life.Flare = flare;
            // the adaptive wing beat reads this (WingMods)
            _life.Flight.Effort = Mathf.Clamp01(effort);
            _life.Flight.Climb = Mathf.Clamp(Vel.y / 3f, -1f, 1f);
            _life.Flight.Thrust = sp > 0.5f ? Mathf.Clamp(Vector3.Dot(_acc, Vel / sp) / Mathf.Max(1f, _s.Accel), -1f, 1f) : 0f;
            _life.Flight.Dive = _life.Motor != null && _life.Motor.DivePoseTest ? 0f : Mathf.Clamp01((-Vel.y - 1.5f) / 4f) * Mathf.Clamp01((sp - 5f) / 4f);
            _life.Flight.Bank = _bank;
            _life.Flight.MaxBank = _s.Bank;
        }
    }
}
#endif

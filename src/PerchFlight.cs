#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // ---------------------------------------------------------------- the flight to the perch

    // Off the shoulder and onto a perch by the raven's motor. Near (a hop, up to HopRange m): a push off, a low arc
    // with a few beats, turning in the air to how it will sit, braking, and the game's landing. Farther: the motor flies
    // to a point behind and above the seat on the side it comes from, then the final way in and the landing. It sits
    // facing the way it came (either way along the bar: the seat's Flip). The perch then draws its own sitting copy
    // there (PerchView.Landed) and this bird goes.
    internal class PerchFlight : MonoBehaviour, IRavenUser
    {
        internal PerchView View;
        internal int Seat;
        internal bool Munin;
        internal Player Owner;
        internal ShoulderPin Pin;
        internal ShoulderLife Life;
        internal RavenMotor Motor;
        internal float Wait;
        internal static float PreBack = 1.6f, PreUp = 1.2f, GiveUp = 25f;
        internal static float HopRange = 5f, HopSpeed = 3.2f, HopMin = 0.55f, HopMax = 1.6f, HopArc = 0.3f, HopArcPerM = 0.06f, HopArcMax = 0.6f;
        private float _t, _since;
        private bool _off, _flip, _hop;
        private int _phase;     // 0 to the point before the seat (or the hop), 1 the final way in, 2 the landing
        private Vector3 _landOff, _p0, _p1;
        private Quaternion _landFromRot, _look0;
        private float _settle = -1f, _hopT, _hopLen;
        private bool _clip;

        public void Yield() { } // nothing takes a bird leaving the shoulder

        // Called back while it flies here: it is not dropped (no gap in the air), it flies on toward its owner
        // until the shoulder's new model takes its place right where it is, at its speed (Perches.TakeFrom).
        private bool _back;
        internal void TurnBack(long pid, bool munin)
        {
            _back = true; View = null;
            if (_off && _phase == 2) // it was landing: up again, on its way
            {
                _phase = 0;
                if (Motor == null) Motor = RavenMotor.Of(gameObject, Munin, Owner);
                if (Life != null) Life.VanillaFly = true;
                if (Motor != null) Motor.Launch(Vector3.up * 2f, "додому", 1f);
            }
            Perches.NoteLeft(pid, munin, transform.position, transform.rotation, gameObject);
        }

        // its speed now, if it is in the air (for the shoulder's model to fly on with)
        internal Vector3 AirVel { get { return _off && _phase != 2 && Motor != null && Motor.Body != null ? Motor.Body.Vel : Vector3.zero; } }

        private void Coast(float dt)
        {
            if (!_off || _phase == 2 || Motor == null || Motor.Body == null) return; // not in the air: it waits where it is
            Vector3 aim = Owner != null ? Owner.transform.position + Vector3.up * 2.2f : transform.position + Motor.Body.Vel;
            Motor.Steer(aim, Motor.Body.Style.Cruise, true, 0f, dt, Vector3.zero, 1f);
            Motor.Finish(dt, 0f);
        }

        internal void Launched()
        {
            _off = true; _since = Time.time;
            if (Life != null) { Life.VanillaFly = true; Life.Still = true; }
            Perches.Occupant o = View != null ? View.At(Seat) : null;
            _flip = o != null && o.Flip;
        }

        private Transform Face { get { return View.Facing(Seat, _flip); } }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        // where the game's landing takes over: a little above and behind the seat
        private Vector3 LandPoint(Transform s) { return s.position + Vector3.up * FlightBody.LandAbove - Flat(s.forward) * FlightBody.LandBehind; }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_t < Wait) return;
            if (_back) { Coast(dt); return; } // (taken over by the shoulder, or puffed by Perches.Sweep after HandOver)
            if (View == null) { Gone(); return; }
            if (!_off)
            {
                if (Pin != null) { Pin.enabled = false; Destroy(Pin); }
                transform.SetParent(null, true);
                if (Motor == null) Motor = RavenMotor.Of(gameObject, Munin, Owner);
                if (Motor == null) { Gone(); return; }
                Launched();
                Transform launchSeat = Face;
                Vector3 launchAim = LandPoint(launchSeat);
                if (Vector3.Distance(transform.position, launchAim) > HopRange)
                    launchAim = launchSeat.position - Flat(launchSeat.forward) * PreBack + Vector3.up * PreUp;
                Motor.Launch(Motor.TakeoffVelocity(launchAim, Vector3.zero, "на жердину"), "на жердину", 1f);
                BeginHop();
            }
            Transform s = Face;
            if (_phase == 2) { Land(s); return; }
            if (Motor.Body == null || Time.time - _since > GiveUp) { GiveUpNow(s); return; }
            if (_hop) { Hop(s, dt); return; }
            Vector3 seat = s.position, front = Flat(s.forward);
            // the seat out of sight (a table inside, a perch in another room): the way a man would walk, through the
            // door, slower; stuck behind closed doors: a puff here and on the seat
            Vector3 round;
            if (Motor.Round(LandPoint(s), seat, out round))
            {
                if (_phase == 1 && Vector3.Distance(transform.position, seat) > 3f) { Motor.Body.Unfinal(); _phase = 0; }
                Motor.Steer(round, Mathf.Min(Motor.Body.Style.Cruise, RavenMotor.DetourSpeed), false, 0f, dt, Vector3.zero, RavenMotor.DetourAvoid);
                Motor.Finish(dt, 0f);
                if (Motor.Stuck) GiveUpNow(s);
                return;
            }
            if (_phase == 0)
            {
                // behind the seat on the side it sits facing from (it comes from there), not round the perch; above the
                // seat and never below where it flies now by much, so a low perch is glided down to, a high one climbed to
                Vector3 pre = seat - front * PreBack + Vector3.up * PreUp;
                Motor.Steer(pre, Motor.Body.Style.Cruise, true, 0f, dt, Vector3.zero, 1f);
                Motor.Finish(dt, 0f);
                if (Vector3.Distance(transform.position, pre) < 1.2f || Vector3.Distance(transform.position, seat) < 2f) { Motor.Body.MarkFinal(); _phase = 1; }
                return;
            }
            Vector3 aim = LandPoint(s);
            float near = Vector3.Distance(transform.position, aim);
            Motor.Body.FaceHint(front, 1f - Mathf.Min(near, RavenFlight.TurnFrom) / RavenFlight.TurnFrom);
            float left = Motor.Steer(aim, Motor.Body.HomeSpeed(5f, Vector3.zero, 0f), true, near < 4f ? 2f : 0f, dt, Vector3.zero, 1f);
            Motor.Finish(dt, Mathf.Clamp01(1f - near / Mathf.Max(0.5f, Motor.Landing().Flare)));
            if (Motor.ReadyToLand(left, Vector3.zero)) BeginLand(s);
        }

        // ---- the hop: a short arc driven by hand through the motor (its wings beat by the speed)

        private static int _mask;
        private static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small"); return _mask; } }

        private void BeginHop()
        {
            Transform s = Face;
            Vector3 to = LandPoint(s);
            _p0 = transform.position;
            float dist = Vector3.Distance(_p0, to);
            _hop = dist <= HopRange;
            if (!_hop) return;
            _hopLen = Mathf.Clamp(dist / HopSpeed, HopMin, HopMax);
            float arc = Mathf.Min(HopArcMax, HopArc + dist * HopArcPerM);
            Vector3 mid = (_p0 + to) * 0.5f;
            // something in the way (a post, the bar's end, a chair): the arc goes over it
            RaycastHit hit;
            if (Physics.Linecast(_p0, to, out hit, Mask, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(View.transform) && hit.distance < dist - 0.3f)
                arc = Mathf.Max(arc, hit.point.y - mid.y + 0.6f);
            _p1 = mid + Vector3.up * arc * 2f; // the curve's top is half way to this point
            _look0 = Quaternion.LookRotation(Flat(to - _p0), Vector3.up);
            _hopT = 0f;
        }

        private void Hop(Transform s, float dt)
        {
            if (Motor.Holding) { Motor.Drive(transform.position, _look0, dt); return; } // the take-off's crouch first (the hop starts after it)
            Vector3 to = LandPoint(s); // the seat may sway a little (a ship): followed
            _hopT = Mathf.Min(1f, _hopT + dt / _hopLen);
            float t = _hopT, e = t * t * (3f - 2f * t); // a push off, the glide, braking at the end
            float a = 1f - e;
            Vector3 pos = a * a * _p0 + 2f * a * e * _p1 + e * e * to;
            // turned in the air from the way it hops to the way it will sit, mostly in the second half
            Quaternion sitLook = Quaternion.LookRotation(Flat(s.forward), Vector3.up);
            float turn = Mathf.Clamp01((t - 0.2f) / 0.7f);
            turn = turn * turn * (3f - 2f * turn);
            Motor.Drive(pos, Quaternion.Slerp(_look0, sitLook, turn), dt);
            if (t >= 1f && Motor.MayTouchNow()) BeginLand(s); // (its turn by the other raven: Landings)
        }

        private void BeginLand(Transform s)
        {
            _phase = 2;
            Motor.Grounded();
            if (Life != null) { Life.VanillaFly = false; Life.Still = false; }
            Quaternion seatRot = s.rotation * Quaternion.Euler(Shoulder.Rotation);
            _landOff = Quaternion.Inverse(seatRot) * (transform.position - s.position);
            _landFromRot = Quaternion.Inverse(seatRot) * transform.rotation;
            _clip = Life != null && Life.BeginLand();
            _settle = Time.time;
        }

        private void Land(Transform s)
        {
            Quaternion seatRot = s.rotation * Quaternion.Euler(Shoulder.Rotation);
            float way;
            bool still;
            if (_clip) still = Life.StepLand(out way);
            else { way = Mathf.Clamp01((Time.time - _settle) / 0.35f); still = way < 1f; way = way * way * (3f - 2f * way); }
            transform.position = s.position + seatRot * (_landOff * (1f - way));
            transform.rotation = Quaternion.Slerp(seatRot * _landFromRot, seatRot, Mathf.Clamp01(way * ShoulderPin.LandTurnRate));
            if (!still || Time.time - _settle > 4f) Done();
        }

        // on the seat: this bird sits there itself (the perch puts the sitting look on it); the flight is over
        private void Done()
        {
            enabled = false;
            AnimDirector.Release(gameObject, this); // not flying any more: the bird is nobody's act (the perch has it)
            Destroy(this);
            if (View != null) View.Landed(Seat, gameObject);
            else Destroy(gameObject);
        }

        // it never got there (stuck, lost): far off it is not jumped onto the seat in plain sight, but goes in a puff of
        // feathers and shows up on the seat in another, as the game's ravens come and go
        internal static float GiveUpNear = 2f;
        private void GiveUpNow(Transform s)
        {
            if (s != null && Vector3.Distance(transform.position, s.position) > GiveUpNear)
            {
                Raven tpl = Shoulder.Template(Munin);
                if (tpl != null && tpl.m_despawnEffect != null)
                {
                    RavenFx.Own(tpl.m_despawnEffect, transform.position, transform.rotation, null, 0.6f, -1);
                    RavenFx.Own(tpl.m_despawnEffect, s.position, s.rotation, null, 0.5f, -1);
                }
                Shoulder.PuffNote(Owner, Munin, "never got to its perch: a puff there and on the seat");
            }
            Done();
        }

        // the perch is gone while it flew: it vanishes in a puff, as the game's ravens
        private void Gone()
        {
            Raven tpl = Shoulder.Template(Munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, transform.position, transform.rotation, null, 0.6f, -1);
            Destroy(gameObject);
        }

        // in the air: the flying clip and the motor's wings (Apply returns early while VanillaFly). Not during the
        // landing: Apply's sitting part belongs to the shoulder (its owner's facing and pace, the take-off beats, the
        // shoulder leg bend); StepLand poses the landing alone.
        private void LateUpdate() { if (_off && Life != null && _phase != 2) Life.Apply(); }
    }

}
#endif

#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A raven fishing (the user, 2026-10-28: «not landing in the air as on a perch»), flown as an outing (Outing.Fish):
    // off your shoulder to a point over the water, it hangs there on its wings with some inertia — swaying, drifting a
    // little and caught back, tilting into the drift — then the aim (up a little), the dive head first onto the water, a
    // moment beating on it with a splash, and up again; then home with the fish (WorldVisit.FishCaught). Every game flies
    // it from the spot on the owner's ZDO (Outing.FishSpot); the splash each game its own.
    internal sealed class FishPlan : FlightPlan
    {
        internal static float Hang = 1.1f, Sway = 0.3f, SwayUp = 0.12f, Spring = 7f, Damp = 3.2f, Gust = 1.4f;
        internal static float Wind = 0.35f, Fall = 0.4f, Under = 0.5f, Rise = 0.8f, Raise = 0.15f, Ahead = 0.3f;
        private int _phase;            // 0 flying there, 1 hanging, 2 the aim, 3 the dive, 4 in the water, 5 up again, 6 done
        private float _at, _hangFor, _water, _nextAim;
        private Vector3 _spot, _p, _v, _off, _from, _dir;
        private bool _told;

        internal override string Name { get { return "риболовля"; } }
        internal override void Begin() { _phase = 0; _told = false; }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing)
        {
            Vector3 at; float hang;
            return Outing.FishSpot(pin.Player, pin.Munin, out at, out hang) ? at + Vector3.up * Hang : base.TakeoffAim(pin, facing);
        }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            if (_phase == 0 && !_told)
            {
                _told = true;
                if (!Outing.FishSpot(pin.Player, pin.Munin, out _spot, out _hangFor)) { _spot = pin.Player.transform.position + facing * Vector3.forward * 6f; _hangFor = 3f; }
                _water = WaterFx.SurfaceAt(_spot);
                _spot.y = _water;
                Vector3 d = Vector3.ProjectOnPlane(_spot - pin.Player.transform.position, Vector3.up);
                _dir = d.sqrMagnitude > 0.01f ? d.normalized : facing * Vector3.forward;
            }
            Vector3 hangAt = _spot + Vector3.up * Hang, me = pin.transform.position;
            float now = Time.time;
            switch (_phase)
            {
                case 0: // there
                    if (m.Steer(hangAt, m.Body != null ? m.Body.Style.Top * 0.7f : 8f, true, 0f, dt, carry, 1f) < 0.8f || (m.Body != null && m.Body.Age > 8f))
                    {
                        _phase = 1; _at = now; _p = me; _v = m.Body != null ? m.Body.Vel : Vector3.zero; _off = Vector3.zero; _nextAim = now;
                    }
                    else m.Finish(dt, 0f);
                    return;
                case 1: // hanging: a spring to a point that wanders about the spot, and gusts on top
                    if (now >= _nextAim)
                    {
                        _nextAim = now + Random.Range(0.6f, 1.4f);
                        Vector2 c = Random.insideUnitCircle * Sway;
                        _off = new Vector3(c.x, Random.Range(-SwayUp, SwayUp), c.y);
                    }
                    Hold(hangAt + _off, dt, Gust);
                    Fly(m, _p, _dir, 18f, dt, 2);
                    if (now - _at >= _hangFor) { _phase = 2; _at = now; _from = _p; }
                    return;
                case 2: // the aim: up a little, the body back
                {
                    float u = (now - _at) / Wind;
                    _p = Vector3.Lerp(_from, _from + Vector3.up * Raise, S(u));
                    Fly(m, _p, _dir, Mathf.Lerp(18f, -12f, S(u)), dt, 2);
                    if (u >= 1f) { _phase = 3; _at = now; _from = _p; }
                    return;
                }
                case 3: // the dive, faster and faster, head first
                {
                    float u = Mathf.Clamp01((now - _at) / Fall);
                    Vector3 to = new Vector3(_spot.x, _water, _spot.z) + _dir * Ahead;
                    _p = Vector3.Lerp(_from, to, u * u);
                    Fly(m, _p, _dir, Mathf.Lerp(-12f, 65f, Mathf.Min(1f, u * 1.8f)), dt, 1);
                    if (u >= 1f) { _phase = 4; _at = now; Splash(pin, true); }
                    return;
                }
                case 4: // on the water: beating on it, the head under
                    Fly(m, _p, _dir, 40f, dt, 2);
                    if (now - _at >= Under) { _phase = 5; _at = now; _from = _p; Splash(pin, false); }
                    return;
                case 5: // up again on hard beats
                {
                    float u = (now - _at) / Rise;
                    _p = Vector3.Lerp(_from, hangAt, S(u));
                    Fly(m, _p, _dir, Mathf.Lerp(40f, -15f, S(u)), dt, 2);
                    if (u >= 1f) { _phase = 6; Outing.FishDone(pin.Player, pin.Munin); }
                    return;
                }
                default: // done: hanging a moment till the outing lets it go home
                    Hold(hangAt, dt, Gust * 0.5f);
                    Fly(m, _p, _dir, 5f, dt, 2);
                    return;
            }
        }

        // the bird's own point, pulled to where it wants to be by a soft spring (it overshoots and is caught back), with gusts
        private void Hold(Vector3 want, float dt, float gust)
        {
            Vector3 a = (want - _p) * Spring - _v * Damp + Random.insideUnitSphere * gust;
            _v += a * dt;
            _p += _v * dt;
        }

        // flown along its point: facing out over the water, the nose down by pitch, tilted into its sideways drift
        private void Fly(RavenMotor m, Vector3 at, Vector3 dir, float pitch, float dt, int wings)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float roll = Mathf.Clamp(-Vector3.Dot(_v, side) * 30f, -25f, 25f), lean = Mathf.Clamp(Vector3.Dot(_v, dir) * 15f, -12f, 12f);
            m.Drive(at, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch + lean, 0f, roll), dt, wings);
        }

        // into the water and out of it: the splash, the rings, the drops (each game its own, near you)
        private void Splash(ShoulderPin pin, bool into)
        {
            Vector3 at = new Vector3(_p.x, _water, _p.z);
            Player me = Player.m_localPlayer;
            if (me == null || (me.transform.position - at).sqrMagnitude > RavenFx.HearRange * RavenFx.HearRange) return;
            if (into) { WorldVisit.SplashSmall(at, true, true);WaterFx.Ring(at, 1.6f); WaterFx.Ring(at, 1.2f); WaterFx.Spray(at, 14, 1.3f); }
            else WaterFx.Spray(at, 8, 0.9f);
        }
    }
}
#endif

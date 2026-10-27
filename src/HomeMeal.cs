#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A meal at the feeder (Home): the raven at home flies from its perch to a spot on the feeder (its model's @seat,
    // "eat_<n>"), sits there and pecks a few seconds, then flies back to its seat (PerchFlight). Only what is seen: the
    // food was taken by the perch's holder already. Every game near flies its own copy the same way (the mark "f").
    // Never stuck: the way blocked (a wall, a closed room), the spot cramped, the flight too long or going nowhere —
    // a puff of feathers, and it is on the feeder (the way back: the perch's flight gives up with a puff too).
    internal class HomeMeal : MonoBehaviour, IRavenUser
    {
        internal static float EatSeconds = 4.5f, GiveUp = 7f, StallSeconds = 2f, StallGain = 0.4f, Above = 0.6f, Touch = 0.3f;

        private PerchView _view;
        private int _seat, _level;
        private long _pid;
        private GameObject _anchor; // where it sits to eat: a point of its own at the feeder's spot, not a part of the feeder
        private bool _munin;
        private Transform _spot;
        private Player _owner;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private float _born, _eatUntil = -1f, _stallAt, _stallBest = float.MaxValue;
        private int _phase; // 0 above the spot, 1 down onto it, 2 eating

        public void Yield() { Home(); } // something else takes the bird: back to its seat

        // false: no bird to fly here
        internal static bool Launch(PerchView v, int seat, FeederView feeder, int spot)
        {
            Perches.Occupant o = v != null ? v.At(seat) : null;
            Player me = Player.m_localPlayer;
            Transform at = feeder != null ? feeder.Spot(spot) : null;
            if (o == null || at == null || me == null) return false;
            GameObject b = v.TakeBird(seat, GiveUp + EatSeconds + 25f);
            if (b == null) return false;
            HomeMeal m = b.AddComponent<HomeMeal>();
            m._view = v; m._seat = seat; m._munin = o.Munin; m._level = Mathf.Max(1, o.Level); m._spot = at; m._owner = PerchView.PlayerOf(o.Pid); m._pid = o.Pid; m._born = Time.time;
            m.ToFlight(o.Munin, me);
            if (m._motor == null || !m.Clear(b.transform.position)) { m.Arrive(true); return true; } // no way there: straight onto it
            AnimDirector.Take(b, RavenAct.FlyOff, m, "to the feeder");
            Vector3 to = m.AbovePoint() - b.transform.position;
            m._motor.Launch((to.sqrMagnitude > 0.01f ? to.normalized : b.transform.forward) * 2.5f + Vector3.up * 2f, "на жердину", 1f);
            return true;
        }

        // the sitting bird made a flying one (the same bird: its sitting look off)
        private void ToFlight(bool munin, Player me)
        {
            foreach (PerchLife l in GetComponents<PerchLife>()) DestroyImmediate(l);
            foreach (PerchPose pp in GetComponents<PerchPose>()) DestroyImmediate(pp);
            Raven src = Shoulder.Template(munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(gameObject, src.m_visual); Perches.Reveal(gameObject, false); }
            transform.SetParent(null, true);
            _life = gameObject.GetComponent<ShoulderLife>() ?? gameObject.AddComponent<ShoulderLife>();
            _life.Init(_owner != null ? _owner : me, munin, _level);
            _motor = RavenMotor.Of(gameObject, munin, _owner);
        }

        private Vector3 AbovePoint() { return _spot.position + Vector3.up * Above; }

        // a free way from here to over the spot and room on the spot (the feeder and the perch themselves not counted)
        private static int _mask;
        private static readonly RaycastHit[] _hits = new RaycastHit[16];
        private static readonly Collider[] _cols = new Collider[16];
        private bool Clear(Vector3 from)
        {
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            Transform feeder = _spot.root, perch = _view != null ? _view.transform : null;
            Vector3 to = AbovePoint(), d = to - from;
            float len = d.magnitude;
            if (len > 0.01f)
            {
                int n = Physics.SphereCastNonAlloc(from, 0.12f, d / len, _hits, len, _mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) { Transform t = _hits[i].collider != null ? _hits[i].collider.transform : null; if (t != null && !t.IsChildOf(feeder) && (perch == null || !t.IsChildOf(perch))) return false; }
            }
            int c = Physics.OverlapSphereNonAlloc(_spot.position + Vector3.up * 0.3f, 0.18f, _cols, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < c; i++) if (_cols[i] != null && !_cols[i].transform.IsChildOf(feeder)) return false; // cramped there: something else in the way
            return true;
        }

        private void Puff(Vector3 at)
        {
            Raven tpl = Shoulder.Template(_munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, at, transform.rotation, null, 0.5f, -1);
        }

        private void LateUpdate()
        {
            if (!enabled) return;
            if (_view == null || !_view.Valid) { HuginWhispers.Home.PerchLost(gameObject, _pid, _munin, true); Destroy(gameObject); return; } // its perch broke: to you (near) or gone
            if (_spot == null) { Home(); return; } // the feeder broke: back to its perch
            if (_phase == 2) { if (Time.time >= _eatUntil) Home(); return; }
            float dt = Time.deltaTime;
            Vector3 aim = _phase == 0 ? AbovePoint() : _spot.position;
            float left = Vector3.Distance(transform.position, aim);
            // going nowhere (circling a post, pressed to a wall) or too long: onto it in a puff
            if (left < _stallBest - StallGain) { _stallBest = left; _stallAt = Time.time; }
            if (_motor == null || _motor.Body == null || Time.time - _born > GiveUp || Time.time - _stallAt > StallSeconds || _motor.Stuck) { Arrive(true); return; }
            if (_phase == 0)
            {
                _motor.Steer(aim, Mathf.Min(_motor.Body.Style.Cruise, 6f), true, 1f, dt, Vector3.zero, 1f);
                _motor.Finish(dt, 0f);
                if (left < 0.5f) { _phase = 1; _stallBest = float.MaxValue; _stallAt = Time.time; _motor.Body.MarkFinal(); }
            }
            else
            {
                _motor.Body.FaceHint(_spot.forward, 1f);
                float l = _motor.Steer(aim, 1.5f, true, 2f, dt, Vector3.zero, 0f);
                _motor.Finish(dt, 1f);
                if (l < Touch) Arrive(false);
            }
            if (this != null && enabled && _phase < 2 && _life != null) _life.Apply();
        }

        // on the spot: it sits there as on a perch seat (its sitting look) and pecks
        private void Arrive(bool puff)
        {
            if (puff) { Puff(transform.position); Puff(_spot.position); }
            AnimDirector.Release(gameObject, this);
            if (_motor != null) _motor.Grounded();
            foreach (ShoulderLife sl in GetComponents<ShoulderLife>()) DestroyImmediate(sl);
            float size = Shoulder.Scale * Shoulder.SizeFor(_level);
            if (_anchor == null) _anchor = new GameObject("RavenWatch_meal");
            _anchor.transform.SetPositionAndRotation(_spot.position, _spot.rotation);
            GameObject r = Perches.Seat(gameObject, _anchor.transform, _munin, size, -1, _view != null ? _view.SitPose : null, _view != null ? _view.StandPose : null);
            if (r == null) { Destroy(gameObject); return; }
            PerchLife life = r.AddComponent<PerchLife>();
            life.Munin = _munin; life.Level = _level; life.Owner = _pid; life.View = null; life.Seat = -1;
            PerchPose pose = r.GetComponent<PerchPose>();
            if (pose != null) { pose.Life = life; life.Pose = pose; }
            life.Peck(EatSeconds);
            _phase = 2; _eatUntil = Time.time + EatSeconds + 0.4f;
        }

        private void OnDestroy() { if (_anchor != null) Destroy(_anchor); }

        // back to its seat by the perch's own flight (stuck on the way: it gives up with a puff onto the seat)
        private void Home()
        {
            if (this == null || !enabled) return;
            enabled = false;
            AnimDirector.Release(gameObject, this);
            if (_view == null || !_view.Valid) { HuginWhispers.Home.PerchLost(gameObject, _pid, _munin, true); Destroy(gameObject); return; }
            Player me = Player.m_localPlayer;
            if (_phase == 2 && me != null) ToFlight(_munin, me); // sitting on the feeder: up again
            transform.SetParent(null, true); // (off its eating point before that goes)
            if (_anchor != null) { Destroy(_anchor); _anchor = null; }
            if (_motor == null) { Puff(transform.position); _view.Landed(_seat, gameObject); Destroy(this); return; } // cannot fly: on its seat in a puff
            PerchFlight f = gameObject.AddComponent<PerchFlight>();
            f.View = _view; f.Seat = _seat; f.Munin = _munin; f.Owner = _owner; f.Life = _life; f.Motor = _motor; f.Wait = 0f;
            f.Launched();
            _motor.Launch(Vector3.up * 2f, "на жердину", 1f);
            AnimDirector.Take(gameObject, RavenAct.FlyOff, f, "back to its perch from the feeder");
            _view.Incoming(_seat, f);
            Destroy(this);
        }
    }
}
#endif

#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A blow from the perch (Home, on guard or at home): the sitting bird springs off at the enemy by its motor, strikes
    // (the perch's holder deals the blow: Home.Hit; every other game only sees it) and flies back to its seat by the
    // perch's own flight (PerchFlight → PerchView.Landed). Each game flies its own copy of the bird the same way.
    internal class HomeStrike : MonoBehaviour, IRavenUser
    {
        internal static float GiveUp = 10f; // s: never got there (it ran, it hid): back to the seat

        private PerchView _view;
        private int _seat;
        private long _pid;
        private bool _munin, _struck;
        private Character _target;
        private Player _owner;
        private ShoulderLife _life;
        private RavenMotor _motor;
        private float _born;

        public void Yield() { Back(); } // something else takes the bird: it goes back to its seat

        // false: no bird to fly here (not drawn in this game, already off the seat)
        internal static bool Launch(PerchView v, int seat, Character target)
        {
            Perches.Occupant o = v != null ? v.At(seat) : null;
            Player me = Player.m_localPlayer;
            if (o == null || target == null || me == null) return false;
            GameObject b = v.TakeBird(seat, GiveUp + 15f);
            if (b == null) return false;
            foreach (PerchLife l in b.GetComponents<PerchLife>()) DestroyImmediate(l);
            foreach (PerchPose pp in b.GetComponents<PerchPose>()) DestroyImmediate(pp);
            Raven src = Shoulder.Template(o.Munin);
            if (src != null && src.m_visual != null) { Perches.ToTemplate(b, src.m_visual); Perches.Reveal(b, false); } // the perch's leg pose off its bones
            Player owner = PerchView.PlayerOf(o.Pid);
            ShoulderLife life = b.AddComponent<ShoulderLife>();
            life.Init(owner != null ? owner : me, o.Munin, Mathf.Max(1, o.Level));
            HomeStrike f = b.AddComponent<HomeStrike>();
            f._view = v; f._seat = seat; f._munin = o.Munin; f._target = target; f._owner = owner; f._life = life; f._born = Time.time; f._pid = o.Pid;
            f._motor = RavenMotor.Of(b, o.Munin, owner);
            if (f._motor == null) { f.Back(); return true; }
            AnimDirector.Take(b, RavenAct.Strike, f, "a blow from its perch");
            Vector3 to = Body.Center(target) - b.transform.position;
            f._motor.Launch((to.sqrMagnitude > 0.01f ? to.normalized : b.transform.forward) * 3f + Vector3.up * 2f, "удар", 0.7f);
            RavenFx.Play("sfx_raven_flap", b.transform.position, Quaternion.identity);
            return true;
        }

        private float Contact() { return 0.8f + (_target != null ? Mathf.Min(Body.Radius(_target) * 0.7f, 2f) : 0f); }

        private void Step()
        {
            float dt = Time.deltaTime;
            if (_view == null || !_view.Valid) { Home.PerchLost(gameObject, _pid, _munin, true); Destroy(gameObject); return; } // its perch broke: to you (near) or gone
            if (_target == null || _target.IsDead() || Time.time - _born > GiveUp) { Back(); return; }
            Vector3 aim = Body.Center(_target);
            float near = Vector3.Distance(transform.position, aim);
            float speed = Strike.Speed * 1.1f;
            if (near < 4f) speed = Mathf.Lerp(6f, speed, near / 4f);
            float sharp = near < 6f ? _motor.Char().StrikeSharp : 0.3f;
            float left = _motor.Steer(aim, speed, false, sharp, dt, Vector3.zero, Mathf.Clamp01((near - 3f) / 4f), true);
            if (left < Contact()) { Impact(aim); return; }
            _motor.Finish(dt, 0f);
        }

        private void Impact(Vector3 at)
        {
            _struck = true;
            Raven tpl = Shoulder.Template(_munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, at, Quaternion.identity, null, 0.35f, -1); // a puff of feathers
            RavenFx.Play("sfx_raven_kaw", at, Quaternion.identity);
            if (_life != null) _life.Flap(0.35f);
            if (_view != null && _view.Owner) Home.Hit(_view, _seat, _target); // (only the holder's bird deals it)
            FlightCharacter ch = _motor.Char();
            if (_motor.Body != null) _motor.Body.Bounce(ch.BounceBack, ch.BounceUp);
            Back();
        }

        // back to its seat by the perch's flight
        private void Back()
        {
            if (this == null || !enabled) return;
            enabled = false;
            AnimDirector.Release(gameObject, this);
            if (_view == null || !_view.Valid) { Home.PerchLost(gameObject, _pid, _munin, true); Destroy(gameObject); return; } // its perch broke: to you (near) or gone
            if (!_struck && _view.Owner && _target != null && !_target.IsDead() && Vector3.Distance(transform.position, Body.Center(_target)) < Contact() + 1.5f) Home.Hit(_view, _seat, _target); // (right by it when it gave up)
            if (_motor == null) _motor = RavenMotor.Of(gameObject, _munin, _owner);
            PerchFlight f = gameObject.AddComponent<PerchFlight>();
            f.View = _view; f.Seat = _seat; f.Munin = _munin; f.Owner = _owner; f.Life = _life; f.Motor = _motor; f.Wait = 0f;
            f.Launched();
            AnimDirector.Take(gameObject, RavenAct.FlyOff, f, "back to its perch after a blow");
            _view.Incoming(_seat, f);
            _view.BackFromBlow(_seat);
            Destroy(this);
        }

        private void LateUpdate()
        {
            if (!enabled) return;
            Step();
            if (this != null && enabled && _life != null) _life.Apply();
        }
    }
}
#endif

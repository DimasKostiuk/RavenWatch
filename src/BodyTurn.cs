#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A body turning where it stands (the user 2026-11-04: «not the whole model sliding round on ice, but from foot to foot»), for any
    // body. Whoever turns it turns it as ever (the game after its look, StoryWalk.Face); this only sees that it turns and puts it on
    // the bones, on every game, nothing sent:
    //   its feet stay where they stand on the ground while the body turns over them;
    //   one gets too twisted (StepAt from where the game's pose would have it): it lifts and steps there, in an arc. Only one at a time,
    //   the next only when that one stands (the rule of all our steps), turn and turn about, and the last ones after the turn has stopped;
    //   its hips go over the foot it stands on while the other is in the air;
    //   its shoulders lead the turn, its hips follow, by how fast it turns (Spine), so it starts and stops in the body, not all at once.
    // Off walking, on a seat, in a blow, under a clip that holds it: its host says. Eased in and out, nothing is ever snapped; nothing
    // at all is done while the body is not turning. Hosts: NpcPose (a person), StoryGait (the player, in a scene only).
    // A walk's end the same way (SettleOnStop, its host's choice: the player in a scene; Settle() said by whoever ends the walk): as it stops, its feet stay where they stand
    // and step, one at a time, to where the game's standing pose has them — the game itself pulls them there at once, a stride's foot
    // slid 0.3-0.5 m on the ground (gate_shut 2026-11-04: at the end of the walk to the gate and of the steps back). LocalSettling: the
    // local player's feet are settling now (StoryWalk waits for it before the scene goes on).
    // Measured: a body named by ReportFor (the stage's mannequin) and the player say each turn in the log when it ends.
    internal sealed class BodyTurn
    {
        internal static float TurnFrom = 18f, Settled = 6f, MoveMost = 0.3f; // deg/s it counts as turning · as stopped · m/s: faster, it is walking
        internal static float StepAt = 0.11f, StepLast = 0.035f, StepFor = 0.32f, Lift = 0.07f; // m twisted to step · m left to tidy up after the turn · seconds a step · m its arc
        internal static float OnRate = 5f, RateEase = 0.08f, LeadPer = 0.1f, LeadMost = 14f, LeadEase = 0.14f, HipShift = 0.03f; // /s in and out · s · deg of lead per deg/s · deg · s · m
        internal static float FarOff = 70f, Jump = 1f; // m from the camera: the game's own · m in one frame: it was put there, not walked
        internal static Character ReportFor;
        internal static float SettleLook = 0.45f; // s a stop holds the feet at least
        internal static float SettleWithin = 1.2f, SettleStepFor = 0.5f; // s after the walk ended it may still begin (the body slows down a while: at 0.4 it often never began) · s a step of a stop (measured: at 0.32 a stop's step flew 9 m/s) // s after it was walking: a stop that settles its feet
        internal bool SettleOnStop;
        internal static bool LocalSettling;
        private float _settleAsked = -9f;
        internal void Settle() { _settleAsked = Time.time; }
        private bool _settling;

        private readonly Character _c;
        private Animator _anim;
        private Transform _hips, _spine1, _spine2;
        private readonly Transform[] _up = new Transform[2], _low = new Transform[2], _foot = new Transform[2];
        private bool _on;
        internal string Trace() { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "turn {0:0} deg/s on {1} w {2:0.00} step {3} lead {4:0.0}", _rate, _on, _w, _step, _lead); } // (the black box: NpcPoseWatch)
        private float _w, _rate, _yaw, _lead, _k;
        private Vector3 _pos;
        private bool _seen;
        private readonly Vector3[] _plant = new Vector3[2], _rest = new Vector3[2];
        private readonly Quaternion[] _turn = new Quaternion[2];
        private int _step = -1, _last = 1;
        private Vector3 _from;
        private Quaternion _fromTurn;
        // the report
        private float _bornAt, _yawWent, _slide;
        private int _steps;

        internal BodyTurn(Character c) { _c = c; }

        private bool Bones()
        {
            if (_anim == null) _anim = _c.GetComponentInChildren<Animator>(); // heavy: once a body, and only when it first turns
            if (_anim == null) return false;
            if (_hips != null && !BodyGait.Stale(_hips, _anim) && _foot[1] != null) return true;
            Transform vis = _anim.transform;
            _hips = BodyClips.SkeletonBone(_c, vis, "Hips"); _spine1 = BodyClips.SkeletonBone(_c, vis, "Spine1"); _spine2 = BodyClips.SkeletonBone(_c, vis, "Spine2");
            string[] side = { "Left", "Right" };
            for (int i = 0; i < 2; i++) { _up[i] = BodyClips.SkeletonBone(_c, vis, side[i] + "UpLeg"); _low[i] = BodyClips.SkeletonBone(_c, vis, side[i] + "Leg"); _foot[i] = BodyClips.SkeletonBone(_c, vis, side[i] + "Foot"); }
            return _hips != null && _up[0] != null && _low[0] != null && _foot[0] != null && _up[1] != null && _low[1] != null && _foot[1] != null;
        }

        // one frame, after the body's other layers, every frame even when it may not (it keeps up with where the body is, so a
        // later turn starts from now). may: it is free to (no clip holds it, not on a seat, not down…)
        internal void Pose(bool may)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Transform root = _c.transform;
            float yaw = root.eulerAngles.y;
            Vector3 pos = root.position;
            if (!_seen) { _seen = true; _yaw = yaw; _pos = pos; return; }
            float went = Mathf.DeltaAngle(_yaw, yaw);
            float speed = new Vector2(pos.x - _pos.x, pos.z - _pos.z).magnitude / dt;
            _yaw = yaw; _pos = pos;
            if (speed * dt > Jump) { _rate = 0f; if (_on) { _on = false; _step = -1; _w = 0f; } return; } // (a teleport: the feet stood far away)
            _rate = Mathf.Lerp(_rate, went / dt, 1f - Mathf.Exp(-dt / RateEase));
            float turning = Mathf.Abs(_rate);
            if (!may || speed > MoveMost) { if (_on) End(dt, true); Local(); return; }
            if (!_on)
            {
                bool stop = SettleOnStop && Time.time - _settleAsked < SettleWithin; // (asked by who walked it, as its walk ends: by its speed, a slow walk back «stopped» at every step and its feet were caught mid-walk)
                if (turning < TurnFrom && !stop) { Local(); return; }
                _settling = turning < TurnFrom;
                Camera cam = Camera.main;
                if (cam != null && (cam.transform.position - pos).sqrMagnitude > FarOff * FarOff) return;
                if (!Bones()) return;
                _on = true; _w = _settling ? 1f : 0f; _step = -1; _lead = 0f; // (a stop: its feet are where its pose has them now, held at once — nothing jumps)
                for (int i = 0; i < 2; i++) { _plant[i] = _foot[i].position; _turn[i] = _foot[i].rotation; }
                _bornAt = Time.time; _yawWent = 0f; _slide = 0f; _steps = 0;
            }
            _yawWent += Mathf.Abs(went);
            if (_foot[0] == null || _foot[1] == null) { _on = false; return; }

            // where its pose would have the feet now: where they go when they step
            float most = 0f;
            for (int i = 0; i < 2; i++)
            {
                _rest[i] = _foot[i].position;
                if (i != _step) most = Mathf.Max(most, Flat(_plant[i] - _rest[i]));
            }
            bool stopped = turning < Settled;
            if (_step < 0)
            {
                float need = stopped ? StepLast : StepAt;
                int next = 1 - _last; // (turn and turn about)
                if (Flat(_plant[next] - _rest[next]) < need) next = _last;
                if (Flat(_plant[next] - _rest[next]) >= need) { _step = next; _k = 0f; _from = _plant[next]; _fromTurn = _turn[next]; _steps++; }
            }
            bool watching = _settling && Time.time - _bornAt < SettleLook; // (a stop: its feet stand where they stood while the game pulls its pose to its stance — at once there is nothing yet to step for)
            float wantW = !watching && stopped && _step < 0 && most < StepLast ? 0f : 1f;
            _w = Mathf.MoveTowards(_w, wantW, dt * OnRate);
            if (_w <= 0f && wantW <= 0f) { End(dt, false); Local(); return; }
            float w = _w * _w * (3f - 2f * _w);

            // its shoulders ahead of the turn
            _lead = Mathf.Lerp(_lead, Mathf.Clamp(_rate * LeadPer, -LeadMost, LeadMost), 1f - Mathf.Exp(-dt / LeadEase));
            if (_spine1 != null) _spine1.rotation = Quaternion.AngleAxis(_lead * 0.45f * w, Vector3.up) * _spine1.rotation;
            if (_spine2 != null) _spine2.rotation = Quaternion.AngleAxis(_lead * 0.55f * w, Vector3.up) * _spine2.rotation;

            // the step on now, its hips over the other foot
            float arc = 0f, s = 0f;
            if (_step >= 0)
            {
                _k = Mathf.Min(1f, _k + dt / (_settling ? SettleStepFor : StepFor));
                s = _k * _k * (3f - 2f * _k);
                arc = Mathf.Sin(_k * Mathf.PI);
                Vector3 toStand = _plant[1 - _step] - _hips.position; toStand.y = 0f;
                if (toStand.sqrMagnitude > 0.0001f) _hips.position += toStand.normalized * HipShift * arc * w;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 at; Quaternion turn;
                if (i == _step)
                {
                    at = Vector3.Lerp(_from, _rest[i], s) + Vector3.up * Lift * arc;
                    turn = Quaternion.Slerp(_fromTurn, _foot[i].rotation, s);
                }
                else { at = _plant[i]; turn = _turn[i]; }
                Vector3 point = Vector3.Lerp(_foot[i].position, at, w);
                Vector3 chord = (_foot[i].position - _up[i].position).normalized, bent = _low[i].position - _up[i].position;
                bent -= chord * Vector3.Dot(bent, chord);
                Quaternion was = _foot[i].rotation;
                BodyAnimator.Solve(_up[i], _low[i], _foot[i], point, bent.sqrMagnitude > 0.0001f ? bent.normalized : root.forward);
                _foot[i].rotation = Quaternion.Slerp(was, turn, w);
                if (i != _step) _slide = Mathf.Max(_slide, Flat(_foot[i].position - _plant[i]) * w);
            }
            if (_step >= 0 && _k >= 1f)
            {
                _plant[_step] = _rest[_step]; _turn[_step] = _foot[_step].rotation;
                _last = _step; _step = -1;
            }
            Local();
        }

        private void Local() { if (ReferenceEquals(_c, Player.m_localPlayer)) LocalSettling = _on && _settling; }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // given up (walking, a clip): the feet back to the pose, softly. Done: said
        private void End(float dt, bool cut)
        {
            if (cut)
            {
                _w = Mathf.MoveTowards(_w, 0f, dt * OnRate);
                float w = _w * _w * (3f - 2f * _w);
                if (_w > 0f && _foot[0] != null && _foot[1] != null)
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 chord = (_foot[i].position - _up[i].position).normalized, bent = _low[i].position - _up[i].position;
                        bent -= chord * Vector3.Dot(bent, chord);
                        BodyAnimator.Solve(_up[i], _low[i], _foot[i], Vector3.Lerp(_foot[i].position, _plant[i], w), bent.sqrMagnitude > 0.0001f ? bent.normalized : _c.transform.forward);
                    }
                if (_w > 0f) return;
            }
            _on = false; _step = -1;
            if (Blueprint.On && _settling && (_c == Player.m_localPlayer || (ReportFor != null && ReportFor == _c))) Story.Log("feet settled after a walk: " + _steps + " steps in " + (Time.time - _bornAt).ToString("0.0") + " s, a standing foot off by at most " + _slide.ToString("0.000") + " m" + (cut ? " (given up)" : ""));
            _settling = false;
            if (Blueprint.On && _yawWent >= 1f && ((ReportFor != null && ReportFor == _c) || _c == Player.m_localPlayer || (!(_c is Player) && _yawWent >= 30f)))
                Story.Log("turn of " + _c.name.Replace("(Clone)", "") + ": " + Mathf.RoundToInt(_yawWent) + " deg in " + (Time.time - _bornAt).ToString("0.0") + " s, " + _steps + " steps, a standing foot off by at most " + _slide.ToString("0.000") + " m" + (cut ? " (given up)" : ""));
        }
    }
}
#endif

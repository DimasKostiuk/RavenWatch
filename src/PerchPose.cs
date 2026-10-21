#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // ---------------------------------------------------------------- legs round the perch

    // The sitting raven's look over the game's idle: the idle clip played by hand (the animator off: it would hide the
    // copy or fly it about), then the legs bent by the model's pose. Pose: left hip, knee, ankle, toes; right the same;
    // the body's lean (degrees, about the raven's own side axis, as ShoulderLife bends the legs).
    internal class PerchPose : MonoBehaviour
    {
        internal const int Count = 11; // 0-3 left hip, knee, ankle, front toes; 4-7 the right; 8 lean; 9, 10 the left / right back toe
        internal float[] Pose;                         // sitting
        internal float[] Stand;                        // standing (none: the game's own stance)
        internal float Sit = 1f;                       // 0 standing … 1 sitting (PerchLife eases it)
        internal float Rate = 1f;                      // how fast the idle clip runs (asleep: down to still)
        internal bool Neutral;                         // held in the idle's first moment: no breath, no look about (the book's «Стоп»)
        internal float Yaw, Lift, Crouch;              // turning round on the bar (°), a little hop (m), crouched to push off (0..1)
        internal float LeanAdd;                        // ° more lean (the turn's keys)
        internal float StepL, StepR;                   // a foot up as it steps round (0..1+, PerchTurn.StepFold each)
        internal float Roll;                           // ° the body leans over the standing leg
        private Transform _chest;                      // Spine1: what leans over the standing foot
        internal static readonly float[] CrouchAdd = { -15f, 35f, -20f, 0f, -15f, 35f, -20f, 0f, 8f, 0f, 0f };
        private float _clock;
        private readonly float[] _mix = new float[Count];
        internal PerchLife Life;                       // the moves over the pose (on a built perch; not in the Workshop)
        private AnimationClip _idle;
        private GameObject _animGo;
        private Vector3 _rootPos;
        private Quaternion _rootRot, _baseRot;
        private float _phase;
        private sealed class Leg { public Transform T; public int Kind; public bool Left; public Quaternion Rest; public bool Foot; }
        private readonly List<Leg> _legs = new List<Leg>();
        private Vector3 _basePos;
        private Transform _baseParent; // what _basePos / _baseRot are kept in
        // a squat on the perch (the «Присісти» button): legs folded, toes round the bar; the body sinks by itself
        internal static readonly float[] Squat = { -50f, 90f, -45f, 25f, -50f, 90f, -45f, 25f, 0f, -25f, -25f };

        internal void Init(float[] pose)
        {
            Pose = pose ?? new float[Count]; // no pose: the game's own stance (the turn and the moves still go)
            _baseRot = transform.localRotation;
            _basePos = transform.localPosition;
            _baseParent = transform.parent;
            _phase = UnityEngine.Random.Range(0f, 10f);
            Animator a = GetComponentInChildren<Animator>(true);
            if (a != null)
            {
                a.enabled = false;
                _animGo = a.gameObject;
                _rootPos = _animGo.transform.localPosition; _rootRot = _animGo.transform.localRotation;
                if (a.runtimeAnimatorController != null)
                    foreach (AnimationClip c in a.runtimeAnimatorController.animationClips)
                    {
                        if (c == null) continue;
                        string cn = c.name.ToLowerInvariant();
                        if (cn.Contains("idle2") || cn.Contains("idle3") || cn.Contains("idle4")) continue; // (the busy idles: the sitting system's)
                        if (_idle == null && cn.Contains("idle") && c.isLooping) _idle = c;
                    }
                if (_idle != null) Sample(0f);
            }
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.EndsWith(".l") && !n.EndsWith(".r")) continue;
                string b = n.Substring(0, n.Length - 2);
                int kind = b == "leg1" ? 0 : b == "leg2" ? 1 : b == "leg3" ? 2 : (b == "index1" || b == "mid1" || b == "ring1" || b == "index2" || b == "mid2" || b == "ring2") ? 3 : (b == "backtalon1" || b == "backtalon2") ? 4 : -1;
                if (_chest == null && n == "spine1") _chest = t;
                if (kind >= 0) _legs.Add(new Leg { T = t, Kind = kind, Left = n.EndsWith(".l"), Rest = t.localRotation, Foot = b == "index1" || b == "mid1" || b == "ring1" || b == "backtalon1" });
            }
        }

        // Let go of the seat into the world (called back: the bird waits there for the shoulder to take it): its place
        // of rest stays where it is in the world. (Kept in the seat's terms, the next pose put it that far from the
        // world's centre: hundreds of metres off, and the shoulder flew in a new one with a puff.) Moved to another
        // parent (turned to the bar's other side, PerchView): the same place in the seat's terms, as meant.
        private void OnTransformParentChanged()
        {
            Transform np = transform.parent;
            if (np == _baseParent) return;
            if (np == null && _baseParent != null) { _basePos = _baseParent.TransformPoint(_basePos); _baseRot = _baseParent.rotation * _baseRot; }
            _baseParent = np;
        }

        private void Sample(float t)
        {
            _idle.SampleAnimation(_animGo, t);
            RestoreRoot();
        }

        // the clip's root motion taken out again. The game's raven has its animator on the bird's own root: that goes
        // back to its place of rest (the seat; once let go of it, where it sat in the world), never to where it was
        // first made (for a bird let go of the seat that was the world's centre: it flew in from the stone circle)
        private void RestoreRoot()
        {
            Transform t = _animGo.transform;
            if (t == transform) { t.localPosition = _basePos; t.localRotation = _baseRot; }
            else { t.localPosition = _rootPos; t.localRotation = _rootRot; }
        }

        private bool _still; // nobody near: the last pose is kept as it is (nothing is reset, so nothing moves)

        private Renderer _body; // the raven's mesh: not seen by any camera → nothing to pose this frame
        private bool _posed;

        private void LateUpdate()
        {
            if (_body == null) _body = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_posed && _body != null && !_body.isVisible && Life != null && !Life.Stage) return; // off screen: it stays as it was posed (the test pictures always pose)
            if (Life != null && !Life.Active && Mathf.Abs(Sit - Mathf.Round(Sit)) < 0.001f && Mathf.Abs(Yaw) < 0.01f && Crouch < 0.001f && Mathf.Abs(LeanAdd) < 0.01f && StepL < 0.001f && StepR < 0.001f)
            {
                if (_still) return;
                _still = true; // one more full pass, then frozen
            }
            else _still = false;
            foreach (Leg l in _legs) l.T.localRotation = l.Rest; // turned again from the same start each frame
            transform.localPosition = _basePos;
            transform.localRotation = _baseRot;
            if (Life != null) Life.ResetBones();
            if (Life != null && Life.Composes) Life.ComposeStep(); // the sitting system's clips (its own clock, its acts)
            else if (_idle != null) // (the Workshop's and the book's pictures, a pose held still: the game's idle as it is)
            {
                _clock += Time.deltaTime * Rate;
                Sample(Neutral ? 0f : Mathf.Repeat(_clock + _phase, Mathf.Max(0.1f, _idle.length)));
            }
            if (Pose == null || Pose.Length < Count) return;
            float w = Sit * Sit * (3f - 2f * Sit);
            for (int k = 0; k < Count; k++) _mix[k] = Mathf.Lerp(Stand != null && Stand.Length > k ? Stand[k] : 0f, Pose[k], w) + CrouchAdd[k] * Crouch;
            for (int k = 0; k < 4; k++) { _mix[k] += PerchTurn.StepFold[k] * StepL; _mix[4 + k] += PerchTurn.StepFold[k] * StepR; } // a foot up: that leg folded
            float[] p = _mix;
            // turned round on the bar first (the turn's yaw): the bends below go about the bird's own side as it faces now
            Quaternion yaw = Quaternion.AngleAxis(Yaw, Vector3.up);
            transform.localRotation = yaw * _baseRot;
            // where the toes grip, standing: they stay there, the body sinks into the bent legs; a foot up does not hold
            float wl = 1f - 0.95f * Mathf.Clamp01(StepL), wr = 1f - 0.95f * Mathf.Clamp01(StepR);
            Vector3 feet = Feet(wl, wr);
            Transform parent = transform.parent;
            Vector3 side = (parent != null ? parent.rotation : Quaternion.identity) * (yaw * Vector3.right);
            transform.localRotation = yaw * Quaternion.AngleAxis(p[8] + LeanAdd, Vector3.right) * _baseRot;
            foreach (Leg l in _legs)
            {
                int o = l.Left ? 0 : 4;
                float a = l.Kind == 0 ? p[o] : l.Kind == 1 ? p[o + 1] : l.Kind == 2 ? p[o + 2] : l.Kind == 3 ? p[o + 3] : p[l.Left ? 9 : 10];
                if (Mathf.Abs(a) > 0.01f) l.T.rotation = Quaternion.AngleAxis(a, side) * l.T.rotation;
            }
            transform.position += feet - Feet(wl, wr) + Vector3.up * Lift;
            if (Roll > 0.01f && _chest != null) LeanOver(feet + Vector3.up * Lift, (parent != null ? parent.rotation : Quaternion.identity) * (yaw * Vector3.forward)); // (the standing foot is back there now)
            if (Life != null) Life.Apply(side);
            _posed = true;
        }


        // the left foot is further along 'dir' than the right one (which steps first going that way)
        internal bool LeftFootToward(Vector3 dir)
        {
            Vector3 l = Vector3.zero, r = Vector3.zero; int nl = 0, nr = 0;
            foreach (Leg g in _legs) if (g.Foot) { if (g.Left) { l += g.T.position; nl++; } else { r += g.T.position; nr++; } }
            if (nl == 0 || nr == 0) return true;
            return Vector3.Dot(l / nl - r / nr, dir) > 0f;
        }

        // the toes' middle, each foot by its weight (a foot up holds little)
        private Vector3 Feet(float wl, float wr)
        {
            Vector3 sum = Vector3.zero;
            float n = 0f;
            foreach (Leg l in _legs) if (l.Foot) { float w = l.Left ? wl : wr; sum += l.T.position * w; n += w; }
            return n > 0.0001f ? sum / n : transform.position;
        }

        // the body leans over the standing foot: turned about its own length round that foot, the way that brings the
        // chest nearer above it
        private void LeanOver(Vector3 foot, Vector3 along)
        {
            if (along.sqrMagnitude < 0.0001f) return;
            Vector3 c = _chest.position - foot;
            float a = Side(Quaternion.AngleAxis(Roll, along) * c, along), b = Side(Quaternion.AngleAxis(-Roll, along) * c, along);
            transform.RotateAround(foot, along, a <= b ? Roll : -Roll);
        }
        private static float Side(Vector3 v, Vector3 along) { return Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(v, along), Vector3.up).magnitude; }
    }

}
#endif

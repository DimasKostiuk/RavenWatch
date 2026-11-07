#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // One body measured every frame against the mistakes of a pose that the eye catches and a whole-body picture hides (the user
    // 2026-11-04: «a fist instead of a palm», «folded arms with a fist sticking out», a knee under the ground — each seen too late,
    // by him). The ONE set of rules: the stage's check (AnimStage) and a scene's check (StudioScene) both measure with this:
    //   palm    a hand its clip turns («@LeftHand.f/.n»), at its full weight: how far its palm faces off what the clip says;
    //           a hand held on a fixture without «.n» (a clip made with wrist angles): off facing the fixture's surface
    //   fist    the same hands, their fingers curled shut (a palm meant flat on a door, round a key, under a letter)
    //   inside  a hand or a forearm inside the body (the trunk as a round column from the hips up the spine to the neck), or inside the
    //           head (a ball HeadUp over its bone, HeadRound × the shoulders' width: wipe_brow's arm sank into the head, 2026-11-07)
    //   past    a hand crossed over to the other side, sticking out past that side's elbow (folded arms: the hand belongs on the arm)
    //   elbow   an elbow far behind the chest while its hand is held somewhere; an arm folded tight
    //   knee    a knee under the ground; a knee across the body's middle
    //   miss    a limb's target out of reach at its full weight
    //   speed   a hand or a foot flying, a bone turning in a jerk (only while one of our clips holds the body: the game's own gestures are not ours)
    //   slide   a foot going along the ground while the body stands (near the lowest it has been, not in a step: a step is no slide)
    //   shoulder  a collarbone turned far off how it stood (a reach out of the arm's length pulls it)
    //   neck    the head turned far off the chest
    //   free hand  swinging far behind the body as it bends
    //   prop    the head into the thing, fingers inside it, a forearm, an arm, a thigh, the trunk through it (Limbs)
    //   grip    a hand the clip has on the thing («@LeftHand.on» at full weight): does it hold it — its palm on the thing and turned to it, at
    //           least two fingertips on its surface, not shut to a fist — or what touches instead (knuckles, a fist before it, nothing): the
    //           user 2026-11-07, a left fist before the book passed every rule above. The pose's words say it each frame (GripOf)
    //   touch   the stage's fixtures (AnimStageFixtures: boxes in the body's frame): a hand or a foot whose target lies at a fixture's
    //           surface (within Meant of it), at its full weight, is off that surface or into it; the hips onto a seat; a hand to a point
    //   real    in a scene (Real): a hand its clip says touches something («touch wall 0.55», «touch piece <prefab> 0.55»), its target at that
    //           surface, at full weight: how far the world's real surface is, by a ray along its palm (a stage's box is not the gate)
    // Each is a number kept with the moment it was worst; Verdict says the ones over their limit, with what to change, and Moments
    // says when, so that frame can be looked at.
    // The clip's own «want» lines (the user's words as measures, BodyClips «say», «want»): Wants reads them, Measure judges them, WantsSaid
    // says how each came out. Times are the whole clip's, so only on a whole clip played on the stage.
    //   wrist   a hand off its forearm beyond what a wrist turns or bends (its rest pose against the forearm, from the skeleton): the palm
    //           rule reads back what the core sets, so a twisted wrist under a right palm passed it (review 2026-11-07)
    // Words: the pose now in words (lean, head, weight, hands, knees), the reader's way into a frame. Every reference is the body's own
    // build, never the first frame measured (a scene starts mid-pose: «trunk straight» on a bent body, 2026-11-07): the trunk's lean from
    // the skeleton's rest (bind) pose, the hips' standing height from its legs at the game's stance knee (StanceKnee); the head on the
    // body and on the chest. Stance numbers said in the log once a body (Stance).
    // Light: a few bones a frame, found once; the fixtures a handful of boxes.
    internal sealed class BodyCheck
    {
        internal static float PalmOff = 25f, FistCurl = 60f, InsideDeep = 0.2f, Past = 0.03f, ElbowBack = 0.2f, KneeUnder = 0.02f, Full = 0.95f; // deg · deg · how deep into the trunk's column (0 its surface, 1 the spine) · m · m · m · a target's weight that counts as held
        internal static float TouchFull = 0.99f, Meant = 0.15f, TouchFar = 0.07f, TouchIn = 0.03f, Palm = 0.03f, Sole = 0.06f, SeatHover = 0.1f, PointNear = 0.05f; // a target's weight a touch is measured at (at 0.95 the core still holds a limb 5% back to the game's pose: 2-4 cm short on its way in and out, no mistake) · m from a surface its target means it · m off · m in · the wrist off the palm's face · the ankle off the sole · m the hips hover over a seat · m a hand from a point
        internal static float FootFast = 1.5f, HandFast = 3f, BoneJerk = 600f, MissFar = 0.05f, KneeCross = 0.05f, HandBehindFar = 0.28f, BentDown = 0.2f, HeadNear = 0.3f, FingerDeep = 0.02f; // m/s · m/s · deg/s · m · m · m · m the hips down to count as bent · m · m
        internal static float FootOnGround = 0.03f, FootSlide = 0.2f, StandSpeed = 0.2f, StepMost = 0.1f; // m over its lowest it is still on the ground · m along the ground in one run is a slide · m/s the body counts as standing · m a frame: more, it was put there
        internal static float ShoulderBent = 25f, NeckMost = 75f, ElbowFold = 25f; // deg
        internal static float HeadUp = 0.1f, HeadRound = 0.4f, HeadDeep = 0.05f, HeadNearSay = 0.1f; // m the head's middle over its bone · its radius × the shoulders' width · how deep in counts
        internal static float RealBack = 0.4f, RealFar = 1.4f, WantAround = 0.2f, WantShare = 0.9f; // m a ray starts behind the hand · m it goes · s round a want's one moment · the share of frames a want's stretch must hold

        // a thing the body touches, a box in the body's frame (x right, y up, z ahead), set by the stage
        internal sealed class Fixture
        {
            public string Name = "", Kind = "";  // (kind: wall, table, seat, step, point, piece)
            public Vector3 Center, Size;
            internal float Distance(Vector3 p) // (signed: + outside, − inside)
            {
                Vector3 q = p - Center, h = Size * 0.5f;
                Vector3 d = new Vector3(Mathf.Abs(q.x) - h.x, Mathf.Abs(q.y) - h.y, Mathf.Abs(q.z) - h.z);
                Vector3 o = Vector3.Max(d, Vector3.zero);
                return o.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
            }
            internal Vector3 Closest(Vector3 p) { Vector3 h = Size * 0.5f, q = p - Center; return Center + new Vector3(Mathf.Clamp(q.x, -h.x, h.x), Mathf.Clamp(q.y, -h.y, h.y), Mathf.Clamp(q.z, -h.z, h.z)); }
        }
        internal readonly List<Fixture> Fixtures = new List<Fixture>();
        internal bool Real; // (in a scene: touches measured against the world)

        private readonly Humanoid _h;
        private readonly Transform _vis;
        private readonly Transform[] _b;
        private static readonly string[] Names = { "Hips", "Spine2", "Neck", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand", "LeftLeg", "RightLeg", "LeftHandMiddle1", "LeftHandMiddle3", "RightHandMiddle1", "RightHandMiddle3", "LeftFoot", "RightFoot", "Head", "LeftShoulder", "RightShoulder", "LeftUpLeg", "RightUpLeg", "Spine1", "LeftToeBase", "RightToeBase" };
        private const int Hips = 0, Chest = 1, Neck = 2, ArmL = 3, ArmR = 4, ForeL = 5, ForeR = 6, HandL = 7, HandR = 8, KneeL = 9, KneeR = 10, M1L = 11, M3L = 12, M1R = 13, M3R = 14, FootL = 15, FootR = 16, Head = 17, ShL = 18, ShR = 19, UpL = 20, UpR = 21, Spine = 22, ToeL = 23, ToeR = 24;
        private static readonly int[] Turning = { Hips, Spine, Chest, Neck, Head, ArmL, ForeL, HandL, ArmR, ForeR, HandR, UpL, KneeL, FootL, UpR, KneeR, FootR }; // (the bones whose speed is kept)
        private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        private readonly Transform[,,] _fing = new Transform[2, 5, 2]; // (side, finger, joint 2 · 3)
        private readonly Quaternion[] _wristRest = { Quaternion.identity, Quaternion.identity }; private readonly bool[] _wristKnown = new bool[2];

        // a finger's tip in the world: its last joint and on by most of the bone before it (the last bone's pivot is the last joint, ~2 cm short)
        private bool Tip(int side, int finger, out Vector3 tip)
        {
            Transform a = _fing[side, finger, 0], b = _fing[side, finger, 1];
            tip = b != null ? b.position : Vector3.zero;
            if (a == null || b == null) return b != null;
            tip = b.position + (b.position - a.position) * 0.8f;
            return true;
        }

        private readonly float[] _palm = new float[2], _palmAt = new float[2], _curl = new float[2], _curlAt = new float[2], _past = new float[2], _pastAt = new float[2], _elbow = new float[2], _elbowAt = new float[2];
        private float _inside, _insideAt, _knee = 9f, _kneeAt, _inHead, _inHeadAt, _headGap = 9f, _headGapAt; private string _inHeadWhat = "", _headGapWhat = "";
        private string _insideWhat = "";
        // a touch's worst, by limb and fixture («LeftHand@wall»)
        private sealed class Touch { public float Far = -9f, FarAt, In = 9f, InAt; public Vector3 FarWant, InWant; }
        private readonly Dictionary<string, Touch> _touch = new Dictionary<string, Touch>();
        private float _hipsLow = 9f, _hipsHigh = -9f, _hipsGap = 9f, _hipsIn = 9f, _hipsInAt; private string _seat = "";
        private readonly Dictionary<string, float> _point = new Dictionary<string, float>(); // (a hand's nearest to each point)
        // moved here from the stage and the scene's own checks
        private sealed class Worst { public float V, At; public string Clip; public float ClipAt; }
        private readonly Dictionary<string, Worst> _miss = new Dictionary<string, Worst>(), _fast = new Dictionary<string, Worst>();
        private readonly Dictionary<int, Worst> _spin = new Dictionary<int, Worst>();
        private readonly Quaternion[] _was = new Quaternion[Names.Length];
        private readonly bool[] _wasSeen = new bool[Names.Length];
        private readonly Vector3[] _limb = new Vector3[4]; private bool _limbSeen;
        private readonly Vector3[] _footWorld = new Vector3[2]; private readonly float[] _slid = new float[2], _lowest = { 9f, 9f };
        private float _slideStep, _slideAt; private string _slideFoot = "";
        private bool _first = true;
        private readonly Quaternion[] _shRest = { Quaternion.identity, Quaternion.identity }; private readonly bool[] _shKnown = new bool[2];
        private Vector3 _chestAhead = Vector3.forward, _chestUp = Vector3.up; private bool _chestKnown;
        private float _shoulder, _shoulderAt; private string _shoulderSide = "";
        private float _fold = 180f, _foldAt; private string _foldSide = "";
        private float _neck, _neckAt, _kneeCross, _kneeCrossAt, _handBehind, _handBehindAt, _hipsStood, _headProp = 99f, _headPropAt, _through, _throughAt;
        private Vector3 _faceAxis; private float _lean0, _roll0, _neck0;
        private float _sp; // (the fastest limb this frame, m/s)
        private float _spin0; // (the fastest bone this frame, deg/s)
        // this frame a limb flew or a bone jerked near its limit (a scene's check films the body then)
        internal static float JoltShare = 0.8f;
        internal bool Jolt { get { return _clippedNow && (_sp > HandFast * JoltShare || _spin0 > BoneJerk * JoltShare); } } // (only while one of ours holds it and it is not walking — the game's own moves are not ours: a person standing idle «jolted» at every shot of the player, echo 2026-11-07)
        private bool _clippedNow;
        private readonly float[] _realGap = { 9f, 9f }; // (this frame's gap to the world's real surface, each hand; 9: not measured)

        internal BodyCheck(Humanoid h)
        {
            _h = h;
            Animator an = h != null ? h.GetComponentInChildren<Animator>() : null; // heavy: once a body measured
            _vis = an != null ? an.transform : null;
            _b = new Transform[Names.Length];
            if (_vis == null) return;
            for (int i = 0; i < Names.Length; i++) _b[i] = BodyClips.SkeletonBone(h, _vis, Names[i]);
            for (int s = 0; s < 2; s++)
                for (int k = 0; k < 5; k++)
                    for (int j = 0; j < 2; j++) _fing[s, k, j] = BodyClips.SkeletonBone(h, _vis, (s == 0 ? "Left" : "Right") + "Hand" + Fingers[k] + (j + 2));
        }

        // the head as it is: the skin's points bound to the head bone (its bind pose), their middle and size in the bone's frame — once a
        // body; a skin that cannot be read: a ball guessed from the shoulders (HeadUp, HeadRound), and the report says which
        private bool _headRead; private Vector3 _headC; private float _headR; private string _headFrom = "";
        private void HeadShape(float width)
        {
            _headRead = true;
            VisEquipment ve = _h.GetComponent<VisEquipment>();
            SkinnedMeshRenderer smr = ve != null ? ve.m_bodyModel : null;
            Mesh m = smr != null ? smr.sharedMesh : null;
            if (m != null && m.isReadable && smr.bones != null && _b[Head] != null)
            {
                int hi = -1; for (int i = 0; i < smr.bones.Length; i++) if (smr.bones[i] != null && BodyBuild.Real(smr.bones[i]) == _b[Head]) { hi = i; break; }
                if (hi >= 0 && hi < m.bindposes.Length)
                {
                    Vector3[] v = m.vertices; BoneWeight[] bw = m.boneWeights; Matrix4x4 bind = m.bindposes[hi]; // heavy: once a body measured
                    Bounds b = new Bounds(); bool any = false;
                    for (int i = 0; i < v.Length && i < bw.Length; i++)
                    {
                        if (bw[i].boneIndex0 != hi || bw[i].weight0 < 0.5f) continue;
                        Vector3 p = bind.MultiplyPoint3x4(v[i]);
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                    }
                    if (any) { _headC = b.center; _headR = (b.extents.x + b.extents.y + b.extents.z) / 3f; _headFrom = "the skin (" + N(_headR) + " m)"; return; }
                }
            }
            _headC = Vector3.zero; _headR = 0f; _headFrom = "a guess from the shoulders (the skin could not be read)";
        }

        private Vector3 L(int i) { Transform root = _h.transform; return _b[i] != null ? root.InverseTransformPoint(_b[i].position) : Vector3.zero; }

        // the fixture a target lies at (within Meant of its surface), else null
        private Fixture At(Vector3 target)
        {
            Fixture best = null; float bd = Meant;
            foreach (Fixture f in Fixtures) { if (f.Kind == "point") continue; float d = Mathf.Abs(f.Distance(target)); if (d < bd) { bd = d; best = f; } }
            return best;
        }

        // one frame (t: the clip's or the scene's time, for the report). walked: the scene is moving this body itself (StoryWalk: its easing on the spot is no slide)
        internal void Measure(float t, bool walked = false)
        {
            if (_h == null || _vis == null || _b[Hips] == null || _b[Chest] == null || _b[Neck] == null) return;
            BodyAnimator an = BodyAnimator.Of(_h);
            Transform root = _h.transform;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            bool clipped = an != null && an.CodeBusy && _h.GetVelocity().magnitude < MoveLimit; // (one of ours holds it, and it is not walking)
            _clippedNow = clipped;
            if (_first) First(root);
            _realGap[0] = _realGap[1] = 9f; _gripOn[0] = _gripOn[1] = false;
            Vector3 hips = L(Hips);
            _hipsHigh = Mathf.Max(_hipsHigh, hips.y); if (hips.y < _hipsLow) _hipsLow = hips.y;
            for (int s = 0; s < 2; s++)
            {
                bool left = s == 0;
                string limb = left ? "@LeftHand" : "@RightHand";
                Transform hand = _b[left ? HandL : HandR];
                if (hand == null) continue;
                Vector3 tgt = Vector3.zero, n = Vector3.zero, fd; float tw = 0f, nw, fw;
                bool held = an != null && an.ChannelNow(limb, out tgt, out tw) && tw >= Full;
                Vector3 hl = L(left ? HandL : HandR);
                Vector3 onProp; float onW = 0f;
                bool onThing = an != null && an.PropOn && an.ChannelNow(limb + ".on", out onProp, out onW) && onW >= 0.5f; // (on the thing in its hands: the thing is what it touches, not the table under it — GripOf judges it)
                bool turned = held && !onThing && an.ChannelNow(limb + ".f", out fd, out fw) && an.ChannelNow(limb + ".n", out n, out nw) && n.sqrMagnitude > 1e-6f; // (the core turns a hand by .f and .n together, and on a thing by its grip: BodyAnimator.Facing)
                Fixture on = held && !onThing ? At(tgt) : null; // (a hand meant on a fixture: its palm belongs on it, turned or not — the fist of a clip made with wrist angles)
                // palm: where the clip turns the hand («.n»), else on a fixture (its palm to the surface)
                if (turned || on != null)
                {
                    Vector3 along, palm;
                    if (an.HandFacing(left, hand, out along, out palm) && palm.sqrMagnitude > 1e-8f)
                    {
                        Vector3 want = turned ? root.rotation * n : root.TransformDirection(on.Closest(hl) - hl);
                        float swing = turned && _nWas[s].sqrMagnitude > 1e-6f ? Vector3.Angle(n, _nWas[s]) / dt : 0f;
                        if (turned) _nWas[s] = n;
                        if (want.sqrMagnitude > 1e-6f && swing < PalmSwing) { float off = Vector3.Angle(palm, want); if (off > _palm[s]) { _palm[s] = off; _palmAt[s] = t; } } // (while the clip turns the hand fast, the wrist follows a little late: only a palm meant to stay is judged)
                    }
                    Transform m1 = _b[left ? M1L : M1R], m3 = _b[left ? M3L : M3R];
                    Vector3 asked; float aw;
                    bool curls = an.ChannelNow((left ? "LeftHand" : "RightHand") + "Middle1", out asked, out aw) && aw > 0.5f && asked.x > CurlAsked; // (the clip closes these fingers itself — it takes, holds, sorts: its fist is meant; heid_search 2026-11-04)
                    if (m1 != null && m3 != null && !curls)
                    {
                        float curl = Vector3.Angle(m1.position - hand.position, m3.position - m1.position);
                        if (curl > _curl[s]) { _curl[s] = curl; _curlAt[s] = t; }
                    }
                }
                // past the other arm: crossed over and out beyond that side's elbow
                Vector3 otherElbow = L(left ? ForeR : ForeL);
                float over = left ? hl.x - otherElbow.x : otherElbow.x - hl.x;
                bool crossed = left ? hl.x > 0.05f : hl.x < -0.05f;
                if (crossed && !held && !onThing && _b[left ? ForeR : ForeL] != null && over > _past[s]) { _past[s] = over; _pastAt[s] = t; } // (a hand held somewhere, on a thing: reaching across is meant)
                // the elbow behind the chest while the hand is held
                if (held && _b[left ? ForeL : ForeR] != null)
                {
                    Vector3 ahead = _chestKnown ? root.InverseTransformDirection(_b[Chest].rotation * _chestAhead).normalized : Vector3.ProjectOnPlane(Vector3.forward, (L(Neck) - L(Hips)).normalized).normalized; // (the chest's own ahead: bent forward, the root's would put a normal elbow «behind»)
                    float back = Vector3.Dot(L(Chest) - L(left ? ForeL : ForeR), ahead);
                    if (back > _elbow[s]) { _elbow[s] = back; _elbowAt[s] = t; }
                }
                // the arm folded tight (the elbow's inner angle: 180 straight)
                Transform up = _b[left ? ArmL : ArmR], fore = _b[left ? ForeL : ForeR];
                if (up != null && fore != null)
                {
                    float e = Vector3.Angle(up.position - fore.position, hand.position - fore.position);
                    if (e < _fold) { _fold = e; _foldAt = t; _foldSide = left ? "left" : "right"; }
                }
                // on a fixture: off its surface or into it (the palm's face, a little off the wrist)
                if (on != null && tw >= TouchFull) Keep((left ? "LeftHand@" : "RightHand@") + on.Name, on.Distance(hl) - Palm, t, tgt);
                // to a point
                foreach (Fixture f in Fixtures)
                {
                    if (f.Kind != "point" || !held) continue;
                    float d = Vector3.Distance(hl, f.Center), was;
                    if (!_point.TryGetValue(f.Name, out was) || d < was) _point[f.Name] = d;
                }
                // the wrist off its rest against the forearm: twist round the forearm, and bend
                if (_wristKnown[s] && fore != null && (held || onThing))
                {
                    Quaternion now = Quaternion.Inverse(fore.rotation) * hand.rotation, d = Quaternion.Inverse(_wristRest[s]) * now;
                    Vector3 axis = (Quaternion.Inverse(_wristRest[s]) * fore.InverseTransformPoint(hand.position)).normalized; // (the forearm's length, in the hand's rest frame: d turns there)
                    Vector3 dq = new Vector3(d.x, d.y, d.z), proj = Vector3.Project(dq, axis);
                    Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, d.w);
                    float twisted = Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(Vector3.Dot(dq, axis), d.w) * Mathf.Rad2Deg); // (signed round the forearm, from the rest)
                    float bend = Quaternion.Angle(Quaternion.identity, d * Quaternion.Inverse(Normal(twist)));
                    if (twisted > _wristTwist[s]) { _wristTwist[s] = twisted; _wristTwistAt[s] = t; }
                    if (twisted < _wristTwistLow[s]) { _wristTwistLow[s] = twisted; _wristTwistLowAt[s] = t; }
                    if (bend > _wristBend[s]) { _wristBend[s] = bend; _wristBendAt[s] = t; }
                }
                // the world's real surface, where its clip says it touches one
                if (Real && tw >= TouchFull && an != null) RealTouch(an, left, hand, root, tgt, turned ? n : Vector3.forward, t);
            }
            // the feet: a target on a fixture (a step) — off it or into it
            for (int s = 0; s < 2; s++)
            {
                string limb = s == 0 ? "@LeftFoot" : "@RightFoot";
                Vector3 tgt; float tw;
                if (_b[s == 0 ? FootL : FootR] == null || an == null || !an.ChannelNow(limb, out tgt, out tw) || tw < TouchFull) continue;
                Fixture on = At(tgt + Vector3.down * Sole);
                if (on != null) Keep((s == 0 ? "LeftFoot@" : "RightFoot@") + on.Name, on.Distance(L(s == 0 ? FootL : FootR)) - Sole, t, tgt);
            }
            // the hips onto a seat
            foreach (Fixture f in Fixtures)
            {
                if (f.Kind != "seat") continue;
                float d = f.Distance(hips) - 0.1f; // (the hips' bone sits about this far over what it sits on)
                if (d < _hipsIn) { _hipsIn = d; _hipsInAt = t; _seat = f.Name; }
                if (hips.y <= _hipsLow) { _hipsGap = d; _seat = f.Name; }
            }
            // inside the trunk: hands and the forearms' middles against the column hips → chest → neck
            float width = _b[ArmL] != null && _b[ArmR] != null ? Vector3.Distance(_b[ArmL].position, _b[ArmR].position) : 0.36f;
            float r = 0.32f * width;
            CheckIn(HandL, -1, r, t, "left hand"); CheckIn(HandR, -1, r, t, "right hand");
            CheckIn(ForeL, HandL, r, t, "left forearm"); CheckIn(ForeR, HandR, r, t, "right forearm");
            if (_b[Head] != null)
            {
                if (!_headRead) HeadShape(width);
                Vector3 hc = _headR > 0f ? _b[Head].TransformPoint(_headC) : _b[Head].position + root.up * HeadUp;
                float hr = _headR > 0f ? _headR * _b[Head].lossyScale.x : HeadRound * width;
                InHead(hc, hr, _b[HandL], null, t, "left hand"); InHead(hc, hr, _b[HandR], null, t, "right hand");
                InHead(hc, hr, _b[ForeL], _b[HandL], t, "left forearm"); InHead(hc, hr, _b[ForeR], _b[HandR], t, "right forearm");
            }
            Vector3 lk = L(KneeL), rk = L(KneeR);
            if (_b[KneeL] != null && lk.y < _knee) { _knee = lk.y; _kneeAt = t; }
            if (_b[KneeR] != null && rk.y < _knee) { _knee = rk.y; _kneeAt = t; }
            // the collarbones off how they stood; the head off the chest
            for (int s = 0; s < 2; s++) { Transform sh = _b[s == 0 ? ShL : ShR]; if (sh == null) continue; float a = Quaternion.Angle(Quaternion.Inverse(_b[Chest].rotation) * sh.rotation, _shRest[s]); if (a > _shoulder) { _shoulder = a; _shoulderAt = t; _shoulderSide = s == 0 ? "left" : "right"; } }
            if (_b[Head] != null) { float y = Mathf.Abs(Mathf.DeltaAngle(_neck0, NeckYaw())); if (y > _neck) { _neck = y; _neckAt = t; } }
            Feet(root, t, walked);
            Speeds(an, root, t, dt, clipped);
            if (clipped)
            {
                float cross = Mathf.Max(lk.x, -rk.x);
                if (cross > _kneeCross) { _kneeCross = cross; _kneeCrossAt = t; }
                float ig;
                if (hips.y < _hipsStood - BentDown)
                {
                    if (an != null && !an.Missed("@LeftHand", out ig) && -L(HandL).z > _handBehind) { _handBehind = -L(HandL).z; _handBehindAt = t; }
                    if (an != null && !an.Missed("@RightHand", out ig) && -L(HandR).z > _handBehind) { _handBehind = -L(HandR).z; _handBehindAt = t; }
                }
            }
            if (an != null)
                foreach (BodyClips.Limb m in BodyClips.Limbs)
                {
                    float e;
                    if (!an.Missed(m.Name, out e)) continue;
                    Worst w;
                    if (!_miss.TryGetValue(m.Name, out w)) _miss[m.Name] = w = new Worst { V = -1f };
                    if (e > w.V) { w.V = e; w.At = t; }
                }
            if (an != null && an.PropOn) PropChecks(an, t);
            // the look reached: the face (its own axis from the rest, not the animator's) to the point the clip sends the head to, once fully on
            {
                Vector3 lookAt; float lw;
                if (an != null && _b[Head] != null && _faceAxis.sqrMagnitude > 0.01f && an.LookNow(out lookAt, out lw) && lw >= Full)
                {
                    float off = Vector3.Angle(_b[Head].TransformDirection(_faceAxis), lookAt - _b[Head].position);
                    if (off > _lookOff) { _lookOff = off; _lookOffAt = t; }
                }
            }
            if (_wants != null) foreach (Want w in _wants) w.Look(this, t);
        }

        // the twist from the skin's rest (arms out, palms down: one end of a forearm's turn, not its middle): its range is said, not judged, until
        // known poses set WristTwistFrom/To (deg, signed, each side as it reads: the sign's way round is not known yet). Bend: off the forearm, judged
        internal static float WristTwistFrom = 0f, WristTwistTo = 0f, WristBend = 80f; // (From = To: the twist not judged) · deg a wrist bends, a guess from the human wrist (flexion about 80), NOT measured in the game
        private readonly float[] _wristTwist = { -999f, -999f }, _wristTwistAt = new float[2], _wristTwistLow = { 999f, 999f }, _wristTwistLowAt = new float[2], _wristBend = new float[2], _wristBendAt = new float[2];
        private static Quaternion Normal(Quaternion q) { float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w); return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }
        internal static float CurlAsked = 15f, PalmSwing = 60f; // deg the clip bends the middle finger itself: a fist it means · deg/s its «.n» turns: faster, the palm is on its way, not judged
        private readonly Vector3[] _nWas = new Vector3[2];
        internal static float MoveLimit = 0.3f; // m/s: faster, it is walking, and its arms swing as the game swings them

        internal static float StanceKnee = 145f; // deg: the knee in the game's own stance (the stage's first frame, 2026-11-07: 144-147)

        private void First(Transform root)
        {
            _first = false;
            float hipsNow = L(Hips).y, leanNow, rollNow;
            Lean(out leanNow, out rollNow, false);
            _hipsStood = hipsNow; _lean0 = leanNow; _roll0 = rollNow;
            Vector3 hb, nb, ub, kb, fb; Quaternion ig;
            string from = "the first frame (no skeleton to read)", hipsFrom = "the first frame";
            if (Bind(Hips, out hb, out ig) && Bind(Neck, out nb, out ig))
            {
                Vector3 up = nb - hb; // (the rest pose stands upright: its back is the body's straight)
                _lean0 = Mathf.Atan2(up.z, Mathf.Max(0.01f, up.y)) * Mathf.Rad2Deg; _roll0 = Mathf.Atan2(up.x, Mathf.Max(0.01f, up.y)) * Mathf.Rad2Deg;
                from = "the skeleton";
                if (Bind(UpL, out ub, out ig) && Bind(KneeL, out kb, out ig) && Bind(FootL, out fb, out ig))
                {
                    float a = Vector3.Distance(ub, kb), b = Vector3.Distance(kb, fb);
                    _hipsStood = FootRest + Mathf.Sqrt(a * a + b * b - 2f * a * b * Mathf.Cos(StanceKnee * Mathf.Deg2Rad)) + (hb.y - ub.y);
                    hipsFrom = "the legs at " + N(StanceKnee) + " deg";
                }
            }
            Vector3 cb; Quaternion chestBind, shBind = Quaternion.identity;
            _chestKnown = Bind(Chest, out cb, out chestBind);
            if (_chestKnown) { _chestAhead = Quaternion.Inverse(chestBind) * Vector3.forward; _chestUp = Quaternion.Inverse(chestBind) * Vector3.up; } // (the root's ahead and up as the chest bone holds them at rest)
            for (int s = 0; s < 2; s++)
            {
                Transform sh = _b[s == 0 ? ShL : ShR];
                if (sh == null) continue;
                _shKnown[s] = _chestKnown && Bind(s == 0 ? ShL : ShR, out cb, out shBind);
                _shRest[s] = _shKnown[s] ? Quaternion.Inverse(chestBind) * shBind : Quaternion.Inverse(_b[Chest].rotation) * sh.rotation; // (against the chest: as the skin was made, else as it stands now)
            }
            if (_b[Head] != null) { _faceAxis = BodyClips.RestFace(_h, _b[Head]); _neck0 = _chestKnown ? 0f : NeckYaw(); }
            for (int s = 0; s < 2; s++)
            {
                Quaternion fr, hr; Vector3 ig2;
                if (Bind(s == 0 ? ForeL : ForeR, out ig2, out fr) && Bind(s == 0 ? HandL : HandR, out ig2, out hr)) { _wristRest[s] = Quaternion.Inverse(fr) * hr; _wristKnown[s] = true; }
            }
            for (int s = 0; s < 2; s++) { Vector3 fa, ta; if (Bind(s == 0 ? FootL : FootR, out fa, out ig) && Bind(s == 0 ? ToeL : ToeR, out ta, out ig)) { _sole0[s] = ta.y - fa.y; _soleKnown[s] = true; } }
            Stance = "references: straight from " + from + " " + N(_lean0) + " deg (this frame " + N(leanNow) + "), hips standing from " + hipsFrom + " " + N(_hipsStood) + " m (this frame " + N(hipsNow) + "), the toe over the foot at rest " + (_soleKnown[0] ? N(_sole0[0]) : "-") + " / " + (_soleKnown[1] ? N(_sole0[1]) : "-") + " m, collarbones " + (_shKnown[0] && _shKnown[1] ? "from the skeleton" : "from the first frame") + ", chest and neck " + (_chestKnown ? "from the skeleton" : "from the first frame") + ", face " + (_faceAxis.sqrMagnitude > 0.01f ? "from the skeleton" : "-");
        }
        internal static float SoleTilt = 0.035f, SoleKnee = 20f; // m the toe off its rest height over the foot while it stands (about 15 deg on a 13 cm toe) · deg its knee may bend past the stance's and the leg still stands on it: kneeling, a heel up is meant (NOT measured)
        private readonly float[] _sole0 = new float[2], _sole = new float[2], _soleAt = new float[2]; private readonly bool[] _soleKnown = new bool[2];
        internal string Stance = ""; // (what the references were taken from: said in the report, so a wrong one shows)

        // a bone of the body as its skin was made (its bind pose: the model stands straight, arms out), in the body's frame (false: no skin to read)
        private bool Bind(int bone, out Vector3 at, out Quaternion turn)
        {
            at = Vector3.zero; turn = Quaternion.identity;
            VisEquipment ve = _h.GetComponent<VisEquipment>();
            SkinnedMeshRenderer smr = ve != null ? ve.m_bodyModel : null;
            if (smr == null || smr.bones == null || smr.sharedMesh == null || _b[bone] == null) return false;
            Matrix4x4[] bind = smr.sharedMesh.bindposes;
            for (int i = 0; i < smr.bones.Length && i < bind.Length; i++)
            {
                if (smr.bones[i] == null || BodyBuild.Real(smr.bones[i]) != _b[bone]) continue;
                Matrix4x4 m = smr.transform.localToWorldMatrix * bind[i].inverse; // (the bone's world as the skin was bound, the skin where it is now)
                Transform root = _h.transform;
                at = root.InverseTransformPoint(m.GetColumn(3));
                turn = Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                return true;
            }
            return false;
        }


        // the head's face off the chest, round the chest's own up, from its own ahead (both from the skeleton's rest)
        private float NeckYaw()
        {
            if (!_chestKnown) { Vector3 h0 = Vector3.ProjectOnPlane(_b[Head].forward, Vector3.up), c0 = Vector3.ProjectOnPlane(_b[Chest].forward, Vector3.up); return Vector3.SignedAngle(c0, h0, Vector3.up); } // (no skin: the bones' own axes on the world's level, from the first frame)
            Vector3 up = _b[Chest].rotation * _chestUp, ahead = Vector3.ProjectOnPlane(_b[Chest].rotation * _chestAhead, up), face = Vector3.ProjectOnPlane(_b[Head].TransformDirection(_faceAxis), up);
            return face.sqrMagnitude < 1e-6f ? 0f : Vector3.SignedAngle(ahead, face, up); // (straight up or down off the chest: no turn to read)
        }

        // forward and to its right, degrees, the trunk off how it stood (or as it is: rel false)
        private void Lean(out float forward, out float right, bool rel)
        {
            Vector3 up = L(Neck) - L(Hips); // (the whole back: the chest bone alone hardly moves as the spine above it bends — a 26° bend read 7°)
            forward = Mathf.Atan2(up.z, Mathf.Max(0.01f, up.y)) * Mathf.Rad2Deg; right = Mathf.Atan2(up.x, Mathf.Max(0.01f, up.y)) * Mathf.Rad2Deg;
            if (rel) { forward -= _lean0; right -= _roll0; }
        }

        // up and to its right, degrees, the face (its axis from the skeleton's rest) off the body's own forward
        private void HeadAim(out float up, out float right)
        {
            up = right = 0f;
            if (_b[Head] == null || _faceAxis.sqrMagnitude < 0.01f) return;
            Vector3 d = _h.transform.InverseTransformDirection(_b[Head].TransformDirection(_faceAxis)).normalized;
            up = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg; right = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // along the ground while the body stands: near the lowest this foot has been (over it: a step in the air, no slide)
        internal static float TurnFast = 20f; // deg/s the body turns round: its feet step round with it (the game's turn on the spot), no slide — echo 2026-11-07 read 3.9 m
        private Quaternion _rootWas; private bool _rootSeen;
        private void Feet(Transform root, float t, bool walked)
        {
            bool turning = _rootSeen && Quaternion.Angle(_rootWas, root.rotation) / Mathf.Max(Time.deltaTime, 0.001f) > TurnFast;
            _rootWas = root.rotation; _rootSeen = true;
            walked |= turning;
            for (int s = 0; s < 2; s++)
            {
                Transform f = _b[s == 0 ? FootL : FootR];
                if (f == null) continue;
                float y = root.InverseTransformPoint(f.position).y;
                _lowest[s] = Mathf.Min(_lowest[s], y);
                Vector3 d = f.position - _footWorld[s]; d.y = 0f;
                float step = d.magnitude;
                if (_limbSeen && !walked && _h.GetVelocity().magnitude < StandSpeed && step < StepMost && y - _lowest[s] < FootOnGround)
                {
                    _slid[s] += step;
                    if (step > _slideStep) { _slideStep = step; _slideAt = t; _slideFoot = s == 0 ? "left" : "right"; }
                }
                // the sole on the ground: the toe off the foot against the skin's own rest, while it stands (a heel down and the toe up in the air, or up on the toes)
                Transform toe = _b[s == 0 ? ToeL : ToeR];
                if (toe != null && _soleKnown[s] && !walked && _h.GetVelocity().magnitude < StandSpeed && y - _lowest[s] < FootOnGround && KneeAngle(s == 0 ? UpL : UpR, s == 0 ? KneeL : KneeR, s == 0 ? FootL : FootR) >= StanceKnee - SoleKnee)
                {
                    float tilt = root.InverseTransformPoint(toe.position).y - y - _sole0[s];
                    if (Mathf.Abs(tilt) > Mathf.Abs(_sole[s])) { _sole[s] = tilt; _soleAt[s] = t; }
                }
                _footWorld[s] = f.position;
            }
        }

        private static readonly int[] SpeedLimbs = { FootL, FootR, HandL, HandR };
        private static readonly string[] SpeedNames = { "LeftFoot", "RightFoot", "LeftHand", "RightHand" };
        private void Speeds(BodyAnimator an, Transform root, float t, float dt, bool clipped)
        {
            _sp = 0f; _spin0 = 0f;
            float moment = 0f; PAnimClip c = an != null ? an.CodeClip(out moment) : null;
            for (int i = 0; i < 4; i++)
            {
                if (_b[SpeedLimbs[i]] == null) continue;
                Vector3 now = L(SpeedLimbs[i]);
                if (_limbSeen)
                {
                    float sp = (now - _limb[i]).magnitude / dt;
                    _sp = Mathf.Max(_sp, sp);
                    if (clipped)
                    {
                        Worst w;
                        if (!_fast.TryGetValue(SpeedNames[i], out w)) _fast[SpeedNames[i]] = w = new Worst { V = -1f };
                        if (sp > w.V) { w.V = sp; w.At = t; w.Clip = c != null ? c.Name : ""; w.ClipAt = moment; }
                    }
                }
                _limb[i] = now;
            }
            _limbSeen = true;
            foreach (int i in Turning)
            {
                if (_b[i] == null) continue;
                Quaternion q = Quaternion.Inverse(root.rotation) * _b[i].rotation;
                if (_wasSeen[i])
                {
                    float sp = Quaternion.Angle(_was[i], q) / dt;
                    _spin0 = Mathf.Max(_spin0, sp);
                    if (clipped)
                    {
                        Worst w;
                        if (!_spin.TryGetValue(i, out w)) _spin[i] = w = new Worst { V = -1f };
                        if (sp > w.V) { w.V = sp; w.At = t; }
                    }
                }
                _was[i] = q; _wasSeen[i] = true;
            }
            Tempo(t);
        }

        // the limbs a thing in the hands may go through: from joint to joint, how thick (m, the skin from the bone's line: NOT measured on the model)
        private struct Limb { public int A, B; public float Thick; public string Name; }
        private static readonly Limb[] Limbs =
        {
            new Limb { A = ForeL, B = HandL, Thick = 0.035f, Name = "left forearm" }, new Limb { A = ForeR, B = HandR, Thick = 0.035f, Name = "right forearm" },
            new Limb { A = ArmL, B = ForeL, Thick = 0.045f, Name = "left upper arm" }, new Limb { A = ArmR, B = ForeR, Thick = 0.045f, Name = "right upper arm" },
            new Limb { A = UpL, B = KneeL, Thick = 0.07f, Name = "left thigh" }, new Limb { A = UpR, B = KneeR, Thick = 0.07f, Name = "right thigh" },
            new Limb { A = Hips, B = Chest, Thick = 0.1f, Name = "trunk" }, new Limb { A = Chest, B = Neck, Thick = 0.09f, Name = "upper chest" },
        };
        internal static float LookMiss = 15f; // deg the face may stay off its @Look point once that is fully on (the head's limit cuts it: BodyClips.LookMost; NOT measured)
        private float _lookOff, _lookOffAt;
        internal static float BodyDeep = 0.02f; // m into the skin before it counts (a thing held against the chest touches it: that is no fault)
        private float _limbDeep, _limbDeepAt; private string _limbDeepOf = "";

        // a thing in the hands (StoryProps «free»): the head into it, fingers inside it, a limb through it, each hand's grip (GripOf: a palm turned away too)
        private void PropChecks(BodyAnimator an, float t)
        {
            StoryPropView view = _h.GetComponent<StoryPropView>();
            if (view == null) return;
            if (_b[Head] != null) { float d = view.Distance(_b[Head].position); if (d < _headProp) { _headProp = d; _headPropAt = t; } }
            bool into = false;
            for (int s = 0; s < 2 && !into; s++) for (int k = 0; k < 5 && !into; k++) { Vector3 tip; if (Tip(s, k, out tip) && view.Distance(tip) < -FingerDeep) into = true; }
            if (into) { _through++; _throughAt = t; } // (each finger's real tip, the thumb and the little one too)
            // the body's own limbs and trunk through it: a joint's centre nearer its surface than the limb is thick, by more than BodyDeep
            for (int i = 0; i < Limbs.Length; i++)
            {
                Transform a = _b[Limbs[i].A], b = _b[Limbs[i].B];
                if (a == null || b == null) continue;
                int last = Limbs[i].B == HandL || Limbs[i].B == HandR ? 1 : 2; // (not the wrist: a palm on the thing has it a couple of cm off its surface)
                for (int k = 0; k <= last; k++)
                {
                    float deep = Limbs[i].Thick - view.Distance(Vector3.Lerp(a.position, b.position, k * 0.5f));
                    if (deep > BodyDeep && deep > _limbDeep) { _limbDeep = deep; _limbDeepAt = t; _limbDeepOf = Limbs[i].Name; }
                }
            }
            for (int s = 0; s < 2; s++)
            {
                string why; float bad;
                if (!GripOf(s == 0, an, view, out why, out bad)) continue; // (not on the thing now)
                _gripOn[s] = true; _gripNowWhy[s] = why; _gripNowBad[s] = bad;
                _gripFrames[s]++;
                if (why == null) continue;
                _gripBad[s]++;
                if (bad > _gripWorst[s]) { _gripWorst[s] = bad; _gripAt[s] = t; _gripWhy[s] = why; }
            }
        }

        // ---------------------------------------------------------------- grip
        internal static float GripPalm = 0.045f, GripTip = 0.025f, GripShut = 0.045f, GripShare = 0.1f; // m the palm's face off the thing · m a real fingertip off its surface still on it · m a fingertip this near its own palm and off the thing: curled into it · the share of frames on it that may not hold (NOT measured in the game)
        private readonly int[] _gripFrames = new int[2], _gripBad = new int[2];
        private readonly float[] _gripWorst = new float[2], _gripAt = new float[2];
        private readonly string[] _gripWhy = new string[2], _gripNowWhy = new string[2];
        private readonly bool[] _gripOn = new bool[2]; private readonly float[] _gripNowBad = new float[2]; // (this frame's, for «want … holds»)

        // a hand the clip has on the thing now (false: it has not): why = null when it holds it, else what touches instead; bad: how far from a grip (the worst kept)
        private bool GripOf(bool left, BodyAnimator an, StoryPropView view, out string why, out float bad)
        {
            why = null; bad = 0f;
            Vector3 on; float ow;
            Transform hand = _b[left ? HandL : HandR];
            if (an == null || view == null || hand == null || !an.ChannelNow((left ? "@LeftHand" : "@RightHand") + ".on", out on, out ow) || ow < Full) return false;
            Vector3 along, palm;
            if (!an.HandFacing(left, hand, out along, out palm) || palm.sqrMagnitude < 1e-8f) return false;
            palm.Normalize();
            Vector3 face = hand.position + along * 0.45f + palm * 0.015f; // (the palm's middle, on its skin: between the wrist and the knuckles)
            float palmGap = view.Distance(face);
            Vector3 near = view.Closest(face);
            bool turned = palmGap < 0.005f || Vector3.Dot(palm, (near - face).normalized) > 0.2f; // (to the nearest of its surface, not its middle: a palm on a book's face near the rim)
            int side = left ? 0 : 1, tips = 0, shut = 0, fingers = 0;
            bool thumb = false;
            for (int k = 0; k < 5; k++)
            {
                Vector3 tip;
                if (!Tip(side, k, out tip)) continue;
                float d = view.Distance(tip);
                bool onIt = d <= GripTip && d >= -FingerDeep;
                if (k == 0) { thumb = onIt; continue; }
                fingers++;
                if (onIt) tips++;
                else if (Vector3.Distance(tip, face) < GripShut) shut++; // (curled back to its own palm and not on the thing: a fist)
            }
            List<string> no = new List<string>();
            if (palmGap > GripPalm) no.Add("palm " + N(palmGap) + " m off it");
            if (!turned) no.Add("palm turned away from it");
            if (tips + (thumb ? 1 : 0) < 2) no.Add("fingertips on it " + tips + " of " + fingers + (thumb ? ", the thumb on it" : ", the thumb off it"));
            if (shut >= 3) no.Add("shut to a fist before it (" + shut + " fingers curled to the palm)");
            if (no.Count == 0) return true;
            why = string.Join(", ", no.ToArray());
            bad = Mathf.Max(0f, palmGap - GripPalm) + (turned ? 0f : 0.1f) + Mathf.Max(0, 2 - tips - (thumb ? 1 : 0)) * 0.03f + (shut >= 3 ? 0.05f : 0f);
            return true;
        }

        private static int _realMask;
        // how far the world's surface is from a hand its clip says touches something: a ray along its palm, from a little behind it
        private void RealTouch(BodyAnimator an, bool left, Transform hand, Transform root, Vector3 tgt, Vector3 way, float t)
        {
            float moment; PAnimClip c = an.CodeClip(out moment);
            if (c == null || string.IsNullOrEmpty(c.Touch)) return;
            string[] w = c.Touch.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float m;
            if (w[0] == "wall" && w.Length > 1) { if (!float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return; }
            else if (w[0] == "piece" && w.Length > 2) { if (!float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return; }
            else return;
            if (Mathf.Abs(tgt.z - m) > Meant) return; // (this hand is not the one at that surface)
            if (_realMask == 0) _realMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "terrain", "vehicle");
            Vector3 dir = root.TransformDirection(way).normalized;
            RaycastHit hit;
            float gap = Physics.Raycast(hand.position - dir * RealBack, dir, out hit, RealFar, _realMask, QueryTriggerInteraction.Ignore) ? hit.distance - RealBack - Palm : RealFar;
            Keep((left ? "LeftHand" : "RightHand") + "@real surface", gap, t, tgt);
            _realGap[left ? 0 : 1] = gap;
        }

        private void Keep(string key, float gap, float t, Vector3 want)
        {
            Touch k;
            if (!_touch.TryGetValue(key, out k)) _touch[key] = k = new Touch();
            if (gap > k.Far) { k.Far = gap; k.FarAt = t; k.FarWant = want; }
            if (gap < k.In) { k.In = gap; k.InAt = t; k.InWant = want; }
        }

        // how deep a hand, or a forearm (its nearest point to the head's middle), is in the head, as a share of its radius
        private void InHead(Vector3 c, float r, Transform a, Transform b, float t, string what)
        {
            if (a == null) return;
            float d = b != null ? ToSegment(c, a.position, b.position) : Vector3.Distance(c, a.position);
            float deep = 1f - d / Mathf.Max(0.01f, r);
            if (deep > _inHead) { _inHead = deep; _inHeadAt = t; _inHeadWhat = what; }
            if (d - r < _headGap) { _headGap = d - r; _headGapAt = t; _headGapWhat = what; } // (how near it comes, inside or not: the frames to look at)
        }

        // how deep a point (a bone, or the middle of two) is in the trunk's column, as a share of its radius
        private void CheckIn(int a, int b, float r, float t, string what)
        {
            if (_b[a] == null || (b >= 0 && _b[b] == null)) return;
            Vector3 p = b >= 0 ? (_b[a].position + _b[b].position) * 0.5f : _b[a].position;
            float d = Mathf.Min(ToSegment(p, _b[Hips].position, _b[Chest].position), ToSegment(p, _b[Chest].position, _b[Neck].position));
            float deep = 1f - d / Mathf.Max(0.01f, r); // (0 on the surface, 1 on the spine)
            if (deep > _inside) { _inside = deep; _insideAt = t; _insideWhat = what; }
        }

        private static float ToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float k = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * k);
        }

        // the moments of the mistakes Verdict said, each with its line: what to look at (the stage films them)
        internal readonly List<KeyValuePair<float, string>> Moments = new List<KeyValuePair<float, string>>();
        private void Say(List<string> bad, string line, float at) { bad.Add(line); if (at >= 0f) Moments.Add(new KeyValuePair<float, string>(at, line)); }

        // the ones over their limit, each with what to change (who: «» on the stage, the person's name in a scene)
        internal void Verdict(List<string> bad, string who)
        {
            Moments.Clear();
            string w = who.Length > 0 ? who + ": " : "";
            string[] side = { "left", "right" }, Side = { "Left", "Right" };
            for (int s = 0; s < 2; s++)
            {
                if (_palm[s] > PalmOff) Say(bad, w + side[s] + " palm faces " + N(_palm[s]) + " deg off what @" + Side[s] + "Hand.n says (or off the surface it is held on) at " + N(_palmAt[s]) + " s: the core turns the hand itself, so this is its weight short or a fixture's surface — see the wrist line for what the arm can do", _palmAt[s]);
                if (WristTwistTo > WristTwistFrom && _wristTwistLow[s] <= _wristTwist[s])
                {
                    if (_wristTwist[s] > WristTwistTo) Say(bad, w + side[s] + " wrist twisted " + N(_wristTwist[s]) + " deg round the forearm at " + N(_wristTwistAt[s]) + " s, past " + N(WristTwistTo) + ": no wrist turns so — turn @" + Side[s] + "Hand.n / .on.n back, or move the hand", _wristTwistAt[s]);
                    if (_wristTwistLow[s] < WristTwistFrom) Say(bad, w + side[s] + " wrist twisted " + N(_wristTwistLow[s]) + " deg round the forearm at " + N(_wristTwistLowAt[s]) + " s, past " + N(WristTwistFrom) + ": no wrist turns so — turn @" + Side[s] + "Hand.n / .on.n back, or move the hand", _wristTwistLowAt[s]);
                }
                if (_wristBend[s] > WristBend) Say(bad, w + side[s] + " wrist bent " + N(_wristBend[s]) + " deg off the forearm at " + N(_wristBendAt[s]) + " s: no wrist bends so — point @" + Side[s] + "Hand.f / .on.f more along the forearm, or move the hand", _wristBendAt[s]);
                if (_curl[s] > FistCurl) Say(bad, w + side[s] + " hand closed to a fist (" + N(_curl[s]) + " deg) at " + N(_curlAt[s]) + " s while its palm is set: open the fingers (" + Side[s] + "HandIndex1/2, Middle1/2, Ring1/2, Pinky1/2 about −25)", _curlAt[s]);
                if (_past[s] > Past) Say(bad, w + side[s] + " hand sticks out " + N(_past[s]) + " m past the other elbow at " + N(_pastAt[s]) + " s: bring its x " + N(_past[s] + 0.03f) + " m nearer the middle (onto the other arm)", _pastAt[s]);
                if (_elbow[s] > ElbowBack) Say(bad, w + side[s] + " elbow " + N(_elbow[s]) + " m behind the chest at " + N(_elbowAt[s]) + " s: turn its pole (@" + Side[s] + "Hand.p) out and forward", _elbowAt[s]);
            }
            foreach (KeyValuePair<string, Worst> m in _miss) if (m.Value.V > MissFar) Say(bad, w + m.Key + " misses by " + N(m.Value.V) + " m at " + N(m.Value.At) + " s with its weight on: out of reach", m.Value.At);
            foreach (KeyValuePair<string, Worst> m in _fast)
            {
                bool foot = m.Key.EndsWith("Foot", System.StringComparison.Ordinal);
                float limit = foot ? FootFast : HandFast;
                if (m.Value.V > limit) Say(bad, w + m.Key + " flies at " + N(m.Value.V) + " m/s at " + N(m.Value.At) + " s: too sudden" + Slower(BodyClips.Find(m.Value.Clip), "@" + m.Key, m.Value.ClipAt, limit), m.Value.At);
            }
            foreach (KeyValuePair<int, Worst> m in _spin) if (m.Value.V > BoneJerk) Say(bad, w + Names[m.Key] + " at " + m.Value.V.ToString("0", CultureInfo.InvariantCulture) + " deg/s at " + N(m.Value.At) + " s: a jerk", m.Value.At);
            if (Mathf.Max(_slid[0], _slid[1]) > FootSlide) Say(bad, w + "a foot slid along the ground (L " + N(_slid[0]) + " m, R " + N(_slid[1]) + " m in the whole run; most: the " + _slideFoot + " at " + N(_slideAt) + " s): on ice — lift it before it moves, and let its key before it lands stand over the spot it lands on", _slideAt);
            if (_kneeCross > KneeCross) Say(bad, w + "a knee crossed the body's middle by " + N(_kneeCross) + " m at " + N(_kneeCrossAt) + " s: set @LeftFoot.p / @RightFoot.p", _kneeCrossAt);
            if (_handBehind > HandBehindFar) Say(bad, w + "a free hand swings " + N(_handBehind) + " m behind the body while bent at " + N(_handBehindAt) + " s: give it a target (the knee)", _handBehindAt);
            if (_headProp < HeadNear) Say(bad, w + "the head comes within " + N(_headProp) + " m of the prop at " + N(_headPropAt) + " s: hold it farther", _headPropAt);
            for (int s = 0; s < 2; s++) if (Mathf.Abs(_sole[s]) > SoleTilt) Say(bad, w + "the " + (s == 0 ? "left" : "right") + " foot stands " + (_sole[s] > 0f ? "on its heel, the toe " + N(_sole[s]) + " m up in the air" : "up on its toes, the heel " + N(-_sole[s]) + " m up") + " at " + N(_soleAt[s]) + " s: the sole flat on the ground (the foot's turn, or its target lower)", _soleAt[s]);
            if (_lookOff > LookMiss) Say(bad, w + "the face stays " + N(_lookOff) + " deg off its @Look point at " + N(_lookOffAt) + " s: past what the head turns (BodyClips.LookMost) — turn the body toward it, or bring the point nearer the face's way", _lookOffAt);
            if (_limbDeep > 0f) Say(bad, w + "the " + _limbDeepOf + " goes " + N(_limbDeep) + " m into the prop at " + N(_limbDeepAt) + " s: hold it off the body", _limbDeepAt);
            if (_through > 0f) Say(bad, w + "fingers inside the prop in " + _through.ToString("0", CultureInfo.InvariantCulture) + " frames (last at " + N(_throughAt) + " s): move the wrist out past its edge", _throughAt);
            for (int s = 0; s < 2; s++)
                if (_gripFrames[s] > 0 && _gripBad[s] > _gripFrames[s] * GripShare)
                    Say(bad, w + side[s] + " hand does not hold the prop in " + _gripBad[s] + " of " + _gripFrames[s] + " frames it is on it (worst at " + N(_gripAt[s]) + " s: " + _gripWhy[s] + "): its palm on the thing, turned to it (@" + Side[s] + "Hand.on, .on.n), the fingers round it (" + Side[s] + "HandIndex1/2 … Pinky1/2), not a fist", _gripAt[s]);
            if (_shoulder > ShoulderBent) Say(bad, w + _shoulderSide + " shoulder bent " + _shoulder.ToString("0", CultureInfo.InvariantCulture) + " deg off its stance at " + N(_shoulderAt) + " s: a reach out of the arm's length pulls it", _shoulderAt);
            if (_fold < ElbowFold) Say(bad, w + _foldSide + " elbow folded to " + _fold.ToString("0", CultureInfo.InvariantCulture) + " deg at " + N(_foldAt) + " s", _foldAt);
            if (_neck > NeckMost) Say(bad, w + "head turned " + _neck.ToString("0", CultureInfo.InvariantCulture) + " deg off the chest at " + N(_neckAt) + " s", _neckAt);
            foreach (KeyValuePair<string, Touch> k in _touch)
            {
                string limb = k.Key.Substring(0, k.Key.IndexOf('@')), on = k.Key.Substring(k.Key.IndexOf('@') + 1);
                if (k.Value.Far > TouchFar) Say(bad, w + limb + " " + N(k.Value.Far) + " m off the " + on + " at " + N(k.Value.FarAt) + " s: its target " + V(k.Value.FarWant) + " — bring it " + N(k.Value.Far) + " m onto the surface", k.Value.FarAt);
                if (k.Value.In < -TouchIn) Say(bad, w + limb + " " + N(-k.Value.In) + " m into the " + on + " at " + N(k.Value.InAt) + " s: its target " + V(k.Value.InWant) + " — move it " + N(-k.Value.In) + " m out", k.Value.InAt);
            }
            if (_seat.Length > 0 && _hipsIn < -TouchIn) Say(bad, w + "the hips " + N(-_hipsIn) + " m into the " + _seat + " at " + N(_hipsInAt) + " s: lower the body less («move» y)", _hipsInAt);
            if (_seat.Length > 0 && _hipsGap > SeatHover) Say(bad, w + "the hips hover " + N(_hipsGap) + " m over the " + _seat + " at their lowest: lower the body that much more («move» y)", -1f);
            foreach (KeyValuePair<string, float> p in _point) if (p.Value > PointNear) Say(bad, w + "no hand comes nearer than " + N(p.Value) + " m to the " + p.Key, -1f);
            if (_inside > InsideDeep) Say(bad, w + _insideWhat + " inside the body (" + N(_inside * 100f) + "% of the way to the spine) at " + N(_insideAt) + " s: move it out in front of the chest", _insideAt);
            if (_inHead > HeadDeep) Say(bad, w + _inHeadWhat + " inside the head (" + N(_inHead * 100f) + "% of the way to its middle) at " + N(_inHeadAt) + " s: move it out onto the head's surface (in front of the brow, beside the temple)", _inHeadAt);
            if (_knee < -KneeUnder) Say(bad, w + "a knee " + N(-_knee) + " m under the ground at " + N(_kneeAt) + " s: the body goes lower than the leg folds — raise «move» y there", _kneeAt);
        }

        // what to change for a limb too fast: the two keys of its target round that moment, how far it goes between them, and the
        // time it needs at the limit (a quarter under it); its way in or out when no key of it is there
        private static string Slower(PAnimClip c, string target, float at, float limit)
        {
            if (c == null || !c.Bones.Contains(target)) return " (not a target of the clip: the way in or out — a longer «fade», or its first key nearer the game's stance)";
            float t1 = -1f, t2 = -1f;
            foreach (PAnimKey k in c.Keys) { if (!k.V.ContainsKey(target)) continue; if (k.T <= at) t1 = k.T; else if (t2 < 0f) t2 = k.T; }
            if (t1 < 0f || t2 < 0f) return " (outside its keys: the way in or out)";
            float d = Vector3.Distance(c.Value(target, t1), c.Value(target, t2)), need = d / (limit * 0.75f);
            return need > t2 - t1 ? " — its move " + N(t1) + "–" + N(t2) + " s goes " + N(d) + " m: give it " + N(need) + " s or more (now " + N(t2 - t1) + ")" : " — keys " + N(t1) + "–" + N(t2) + " s are long enough: the curve overshoots between them (a key in the middle)";
        }

        // the numbers, one line (the report)
        private string Range(int s) { return _wristTwistLow[s] > _wristTwist[s] ? "-" : N(_wristTwistLow[s]) + ".." + N(_wristTwist[s]); }

        internal string Numbers()
        {
            StringBuilder s = new StringBuilder("palm off L " + N(_palm[0]) + " R " + N(_palm[1]) + " deg · wrist twist L " + Range(0) + " R " + Range(1) + " deg, bend L " + N(_wristBend[0]) + " R " + N(_wristBend[1]) + " deg · fingers curl L " + N(_curl[0]) + " R " + N(_curl[1]) + " deg · past the other elbow L " + N(_past[0]) + " R " + N(_past[1]) + " m · elbow behind L " + N(_elbow[0]) + " R " + N(_elbow[1]) + " m · tightest elbow " + N(_fold) + " deg · shoulder " + N(_shoulder) + " deg · neck " + N(_neck) + " deg · deepest in the body " + N(_inside * 100f) + "% (" + (_insideWhat.Length > 0 ? _insideWhat : "-") + ") · knee lowest " + N(_knee) + " m · feet lowest L " + N(_lowest[0]) + " R " + N(_lowest[1]) + " m · hips " + N(_hipsLow) + " .. " + N(_hipsHigh) + " m · feet slid L " + N(_slid[0]) + " R " + N(_slid[1]) + " m");
            foreach (KeyValuePair<string, Worst> m in _miss) s.Append(" · ").Append(m.Key).Append(" missed by ").Append(N(m.Value.V)).Append(" m");
            foreach (KeyValuePair<string, Touch> k in _touch) s.Append(" · ").Append(k.Key).Append(": off ").Append(N(k.Value.Far)).Append(", in ").Append(N(-k.Value.In)).Append(" m");
            if (_seat.Length > 0) s.Append(" · hips over the ").Append(_seat).Append(" at their lowest ").Append(N(_hipsGap)).Append(" m");
            if (_headGap < HeadNearSay) s.Append(" · nearest to the head: ").Append(_headGapWhat).Append(' ').Append(N(_headGap)).Append(" m from its surface at ").Append(N(_headGapAt)).Append(" s (head from ").Append(_headFrom).Append(") — look at the head views");
            foreach (KeyValuePair<string, float> p in _point) s.Append(" · nearest hand to the ").Append(p.Key).Append(' ').Append(N(p.Value)).Append(" m");
            return s.ToString();
        }

        // the fastest of each bone and limb (the report's table)
        internal string Fastest()
        {
            StringBuilder s = new StringBuilder();
            foreach (KeyValuePair<int, Worst> m in _spin) s.Append("  ").Append(Names[m.Key].PadRight(13)).Append(m.Value.V.ToString("0", CultureInfo.InvariantCulture).PadLeft(6)).Append(" deg/s @").Append(N(m.Value.At)).Append('\n');
            foreach (KeyValuePair<string, Worst> m in _fast) s.Append("  ").Append(m.Key.PadRight(13)).Append(N(m.Value.V).PadLeft(6)).Append(" m/s   @").Append(N(m.Value.At)).Append('\n');
            return s.ToString();
        }

        // the pose now, one row of numbers (m, body frame; degrees)
        internal string Row()
        {
            float lf, lr, hu, hr;
            Lean(out lf, out lr, true); HeadAim(out hu, out hr);
            return "hips " + N(L(Hips).y) + "  lean " + N(lf) + "," + N(lr) + "  head " + N(hu) + "," + N(hr) + "  Lfoot " + P(L(FootL)) + "  Rfoot " + P(L(FootR)) + "  Lknee " + P(L(KneeL)) + "  Rknee " + P(L(KneeR)) + "  Lhand " + P(L(HandL)) + "  Rhand " + P(L(HandR)) + "  Lelbow " + P(L(ForeL)) + "  Relbow " + P(L(ForeR)) + "  Lshoulder " + P(L(ArmL)) + "  Rshoulder " + P(L(ArmR));
        }

        // the pose now in words: what a frame shows, to hold against what was asked
        internal string Words()
        {
            float lf, lr, hu, hr;
            Lean(out lf, out lr, true); HeadAim(out hu, out hr);
            StringBuilder s = new StringBuilder();
            s.Append("trunk ").Append(Dir(lf, "forward", "back", 4f)).Append(Mathf.Abs(lr) >= 4f ? " " + Dir(lr, "right", "left", 4f) : "");
            s.Append("; head ").Append(Dir(hu, "up", "down", 5f)).Append(Mathf.Abs(hr) >= 5f ? " " + Dir(hr, "right", "left", 5f) : "").Append(" (").Append(Dir(hu + lf, "up", "down", 5f)).Append(" on the chest)"); // (a straight neck on a bent back: «down 40», but straight on the chest)
            s.Append("; hips ").Append(N(_hipsStood - L(Hips).y)).Append(" m down");
            s.Append("; weight ").Append(WeightWord());
            s.Append("; L hand ").Append(HandWord(true)).Append("; R hand ").Append(HandWord(false));
            s.Append("; knees L ").Append(KneeAngle(UpL, KneeL, FootL)).Append(" R ").Append(KneeAngle(UpR, KneeR, FootR)).Append(" deg");
            float y0 = OverGround(FootL), y1 = OverGround(FootR);
            if (y0 > 0.05f) s.Append("; L foot up ").Append(N(y0));
            if (y1 > 0.05f) s.Append("; R foot up ").Append(N(y1));
            return s.ToString();
        }

        // a foot's height over the ground right under it, its stance's own height off (a step's foot is on the step)
        internal static float FootRest = 0.12f;
        private float OverGround(int foot)
        {
            if (_b[foot] == null || ZoneSystem.instance == null) return 0f;
            Vector3 p = _b[foot].position;
            return p.y - ZoneSystem.instance.GetSolidHeight(p) - FootRest; // heavy: one ray, only when the pose is put in words (a key frame)
        }

        private static string Dir(float v, string plus, string minus, float none) { return Mathf.Abs(v) < none ? "straight" : (v > 0f ? plus : minus) + " " + Mathf.RoundToInt(Mathf.Abs(v)); }

        internal static float WeightSide = 0.04f, WeightAhead = 0.06f; // m the hips off the feet's middle that counts
        // the hips over the feet: to a side, ahead or back of their middle (the stance has the feet one before the other: a lean forward
        // read «on the left» on the line between them, review 2026-11-07)
        private string WeightWord()
        {
            Vector3 c = (L(FootL) + L(FootR)) * 0.5f, h = L(Hips);
            float x = h.x - c.x, z = h.z - c.z;
            string side = x < -WeightSide ? "on the left" : x > WeightSide ? "on the right" : "", along = z > WeightAhead ? "ahead" : z < -WeightAhead ? "back" : "";
            return side.Length + along.Length == 0 ? "between the feet" : (side + (side.Length > 0 && along.Length > 0 ? ", " : "") + along);
        }

        private string HandWord(bool left)
        {
            Vector3 h = L(left ? HandL : HandR);
            string w = "at " + N(h.y) + " high, " + N(h.z) + " ahead, " + N(h.x) + " right";
            BodyAnimator an = BodyAnimator.Of(_h);
            Transform hand = _b[left ? HandL : HandR];
            Vector3 along, palm;
            if (an != null && hand != null && an.HandFacing(left, hand, out along, out palm) && palm.sqrMagnitude > 1e-8f) w += ", palm " + Facing(_h.transform.InverseTransformDirection(palm).normalized);
            string why; float ig;
            if (an != null && an.PropOn && GripOf(left, an, _h.GetComponent<StoryPropView>(), out why, out ig)) return w + (why == null ? ", HOLDS the prop" : ", on the prop but not holding it: " + why); // (on the thing: what the table under it says is not about this hand)
            foreach (Fixture f in Fixtures) { if (f.Kind == "point") continue; float d = f.Distance(h) - Palm; if (d <= TouchFar && d >= -TouchIn) { w += ", ON the " + f.Name; break; } }
            return w;
        }

        private static string Facing(Vector3 d)
        {
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y), z = Mathf.Abs(d.z);
            return x >= y && x >= z ? (d.x > 0f ? "right" : "left") : y >= z ? (d.y > 0f ? "up" : "down") : (d.z > 0f ? "forward" : "back");
        }

        private int KneeAngle(int up, int knee, int foot)
        {
            if (_b[up] == null || _b[knee] == null || _b[foot] == null) return 0;
            return Mathf.RoundToInt(Vector3.Angle(_b[up].position - _b[knee].position, _b[foot].position - _b[knee].position));
        }

        // ---------------------------------------------------------------- the tempo: how each part of the body moves, a mark every Beat s
        internal static float Beat = 0.25f;
        private static readonly string Bars = "▁▂▃▄▅▆▇█";
        private readonly StringBuilder _tempoHands = new StringBuilder(), _tempoFeet = new StringBuilder(), _tempoTrunk = new StringBuilder(), _tempoHead = new StringBuilder();
        private float _beatAt = -1f, _bh, _bf, _bt, _bx;
        private readonly Vector3[] _tempoLimb = new Vector3[4];
        private Vector3 _leanWas, _aimWas;

        private void Tempo(float t)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 hl = L(HandL), hr = L(HandR), fl = L(FootL), fr = L(FootR);
            float lf, lr, hu, hx; Lean(out lf, out lr, true); HeadAim(out hu, out hx);
            Vector3 aim = new Vector3(hu, hx, 0f), lean = new Vector3(lf, lr, L(Hips).y * 100f); // (degrees, and the hips' height: cm a second counts like a degree)
            if (_beatAt >= 0f)
            {
                _bh = Mathf.Max(_bh, (hl - _tempoLimb[0]).magnitude / dt, (hr - _tempoLimb[1]).magnitude / dt);
                _bf = Mathf.Max(_bf, (fl - _tempoLimb[2]).magnitude / dt, (fr - _tempoLimb[3]).magnitude / dt);
                _bt = Mathf.Max(_bt, (lean - _leanWas).magnitude / dt);
                _bx = Mathf.Max(_bx, (aim - _aimWas).magnitude / dt);
            }
            else _beatAt = Time.time;
            _tempoLimb[0] = hl; _tempoLimb[1] = hr; _tempoLimb[2] = fl; _tempoLimb[3] = fr; _leanWas = lean; _aimWas = aim;
            if (Time.time - _beatAt < Beat) return;
            _beatAt = Time.time;
            _tempoHands.Append(Bar(_bh, 2f)); _tempoFeet.Append(Bar(_bf, 1.5f)); _tempoTrunk.Append(Bar(_bt, 60f)); _tempoHead.Append(Bar(_bx, 120f));
            _bh = _bf = _bt = _bx = 0f;
        }
        private static char Bar(float v, float most) { return Bars[Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Mathf.Clamp01(v / most)) * (Bars.Length - 1)), 0, Bars.Length - 1)]; }

        // the strips: where each part moves and rests, side by side (a pause, all starting at once, one after the other: the eye's «wooden»)
        internal string TempoSaid()
        {
            return "tempo, a mark every " + N(Beat) + " s:\n  hands " + _tempoHands + "\n  feet  " + _tempoFeet + "\n  trunk " + _tempoTrunk + "\n  head  " + _tempoHead + "\n";
        }

        // ---------------------------------------------------------------- the clip's wants: the user's words as measures
        //   want <t|t0-t1> head up|down|left|right <deg>       the face that far that way off how it stood (at least)
        //   want <t|t0-t1> lean forward|back|left|right <deg>  the trunk
        //   want <t|t0-t1> hips low <m>                        the hips that far down from how they stood
        //   want <t|t0-t1> hand left|right|both on             on a fixture (the clip's «touch»)
        //   want <t|t0-t1> hand left|right|both holds          holds the thing in the hands (GripOf: palm on it and turned to it, two tips on it, no fist)
        //   want <t|t0-t1> hand left|right|both near <x y z> [m]   within m (0.1) of that point in the body's frame
        //   want <t|t0-t1> palm left|right|both forward|back|up|down|left|right   the palm that way, within 45 deg
        //   want <t|t0-t1> foot left|right up <m>|down         that far off the ground · on it
        //   want <t|t0-t1> knee left|right bent <deg>          bent that much at least (0 straight)
        //   want <t|t0-t1> weight left|right                   the hips over that foot
        //   want <t0-t1> still [deg/s]                         nothing of the body turning faster
        //   want <t0-t1> slow <m/s>                            no hand or foot faster
        //   want <t|t0-t1> finger left|right|both index|middle|ring|pinky straight [deg]   that finger straight and in the hand's line: its
        //                                                      bends knuckle + joints + tip at most deg (FingerStraight, 20)
        // One moment holds if it is so within WantAround s of it; a stretch, for WantShare of its frames.
        private sealed class Want
        {
            public string Text = ""; public float From, To; public bool Stretch; public string[] W = new string[0];
            public int Frames, Good; public float Best; public bool Seen;
            public void Look(BodyCheck c, float t)
            {
                float from = Stretch ? From : From - WantAround, to = Stretch ? To : To + WantAround;
                if (t < from || t > to) return;
                float value;
                bool ok = c.Holds(W, out value);
                Frames++; if (ok) Good++;
                if (!Seen || value > Best) { Best = value; Seen = true; }
            }
            public bool Passed { get { return Stretch ? Frames > 0 && Good >= Frames * WantShare : Good > 0; } }
        }
        private List<Want> _wants;

        // the wants of the whole clip played from its start (the stage), each judged from now on
        internal void Wants(PAnimClip c) { _wants = new List<Want>(); AddWants(c, 0f); }

        // a scene's: the wants of a clip begun at that moment of the scene (its times moved by it; a part has none of its own)
        internal void AddWants(PAnimClip c, float at)
        {
            if (_wants == null) _wants = new List<Want>();
            if (c == null) return;
            foreach (string line in c.Wants)
            {
                string[] w = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (w.Length < 2) continue;
                Want x = new Want { Text = line, W = new string[w.Length - 1] };
                System.Array.Copy(w, 1, x.W, 0, x.W.Length);
                if (x.W.Length < WantWords(x.W[0])) { Story.Log("clip " + c.Name + ": want «" + line + "» left out: too few words (tools/clipcheck.js says how)"); continue; } // (the author's live copy is not checked before it plays)
                int dash = w[0].IndexOf('-');
                float a, b;
                if (dash > 0 && float.TryParse(w[0].Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && float.TryParse(w[0].Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b)) { x.From = a + at; x.To = b + at; x.Stretch = true; }
                else if (float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) { x.From = x.To = a + at; }
                else continue;
                if (at > 0f) x.Text = c.Name + " " + line;
                _wants.Add(x);
            }
        }

        private static int WantWords(string kind) { return kind == "head" || kind == "lean" || kind == "knee" || kind == "weight" ? 2 : kind == "hand" || kind == "palm" || kind == "foot" ? 3 : kind == "finger" ? 4 : 1; }

        // how the wants came out: a line each; bad gets the ones that did not, Moments their moments
        internal string WantsSaid(List<string> bad)
        {
            if (_wants == null || _wants.Count == 0) return "";
            StringBuilder s = new StringBuilder();
            int good = 0;
            foreach (Want x in _wants)
            {
                bool ok = x.Passed;
                if (ok) good++;
                string got = x.Frames == 0 ? "never reached in the run" : (x.Stretch ? Mathf.RoundToInt(100f * x.Good / x.Frames) + "% of the frames, " : "") + "best " + N(x.Best);
                s.Append(ok ? "  yes  " : "  NO   ").Append(x.Text).Append(" (").Append(got).Append(")\n");
                if (!ok) Say(bad, "not what was asked: want " + x.Text + " (" + got + ")", x.From);
            }
            return "asked (want): " + good + " of " + _wants.Count + "\n" + s;
        }

        private bool Holds(string[] w, out float value)
        {
            value = 0f;
            float a, b;
            switch (w[0])
            {
                case "head":
                    {
                        HeadAim(out a, out b);
                        float need = WArg(w, 2, 10f);
                        value = w[1] == "up" ? a : w[1] == "down" ? -a : w[1] == "right" ? b : -b;
                        return value >= need;
                    }
                case "lean":
                    {
                        Lean(out a, out b, true);
                        float need = WArg(w, 2, 5f);
                        value = w[1] == "forward" ? a : w[1] == "back" ? -a : w[1] == "right" ? b : -b;
                        return value >= need;
                    }
                case "hips":
                    value = _hipsStood - L(Hips).y;
                    return value >= WArg(w, 2, 0.1f);
                case "hand":
                    {
                        float lv, rv;
                        bool l = HandAsked(w[1], true, w, out lv), r = HandAsked(w[1], false, w, out rv);
                        value = w[1] == "both" ? Mathf.Min(lv, rv) : w[1] == "left" ? lv : rv;
                        return l && r;
                    }
                case "palm":
                    {
                        float lv, rv;
                        bool l = PalmAsked(w[1], true, w[2], out lv), r = PalmAsked(w[1], false, w[2], out rv);
                        value = w[1] == "both" ? Mathf.Min(lv, rv) : w[1] == "left" ? lv : rv;
                        return l && r;
                    }
                case "foot":
                    {
                        int s = w[1] == "left" ? 0 : 1;
                        float y = L(s == 0 ? FootL : FootR).y - _lowest[s];
                        value = y;
                        return w[2] == "down" ? y < FootOnGround : y >= WArg(w, 3, 0.05f);
                    }
                case "knee":
                    value = 180f - KneeAngle(w[1] == "left" ? UpL : UpR, w[1] == "left" ? KneeL : KneeR, w[1] == "left" ? FootL : FootR);
                    return value >= WArg(w, 3, 20f);
                case "weight":
                    value = WeightWord().Contains("on the " + w[1]) ? 1f : 0f;
                    return value > 0f;
                case "still":
                    value = -_spin0;
                    return _spin0 <= WArg(w, 1, 60f);
                case "slow":
                    value = -_sp;
                    return _sp <= WArg(w, 1, 1f);
                case "finger":
                    {
                        float lv = 0f, rv = 0f;
                        bool l = w[1] == "right" || FingerBend(true, w[2], out lv), r = w[1] == "left" || FingerBend(false, w[2], out rv);
                        if (!l || !r) { value = -999f; return false; } // (a bone not found: never a «straight» 0)
                        float bend = Mathf.Max(w[1] == "right" ? 0f : lv, w[1] == "left" ? 0f : rv);
                        value = -bend;
                        return bend <= WArg(w, 4, FingerStraight);
                    }
            }
            return false;
        }

        // how far a finger is from its straight rest, degrees (the user 2026-11-06: «палець кривий», three times, while the check said nothing and
        // the eye on a small frame saw nothing), the sum of its three joints, each against the skin's bind pose (the model's own lie of the
        // finger; whether that is quite straight: the threshold is set from takes): the knuckle — the first segment's way in the hand's frame
        // (lifted off the hand's line, as a thumb up, or curled); the middle joint — the second segment's way in the first bone's frame; the
        // tip, which has no bone of its own after it — the last bone's turn against the one before. Directions, not sizes of angles: a bend to
        // the other side reads as a bend. Its bones and rest found once; no bone or no skin to read: said once, and it cannot pass
        internal static float FingerStraight = 20f;
        private sealed class Finger { public Transform[] B; public Vector3 Rest1, Rest2; public Quaternion RestTip; public bool Cannot; }
        private readonly Dictionary<string, Finger>[] _fingers = { new Dictionary<string, Finger>(), new Dictionary<string, Finger>() };
        private bool FingerBend(bool left, string finger, out float bend)
        {
            bend = 0f;
            Finger f;
            Dictionary<string, Finger> side = _fingers[left ? 0 : 1];
            if (!side.TryGetValue(finger, out f))
            {
                string s = left ? "Left" : "Right", n = char.ToUpperInvariant(finger[0]) + finger.Substring(1);
                f = new Finger { B = new[] { _b[left ? HandL : HandR], BodyClips.SkeletonBone(_h, _vis, s + "Hand" + n + "1"), BodyClips.SkeletonBone(_h, _vis, s + "Hand" + n + "2"), BodyClips.SkeletonBone(_h, _vis, s + "Hand" + n + "3") } };
                foreach (Transform t in f.B) if (t == null) f.Cannot = true;
                if (!f.Cannot && !FingerRest(f)) f.Cannot = true;
                if (f.Cannot) Story.Log("check: the " + s.ToLowerInvariant() + " " + finger + " finger has no bones or no skin to read here: its «want … finger» cannot be judged");
                side[finger] = f;
            }
            if (f.Cannot) return false;
            Transform[] b = f.B;
            bend = Vector3.Angle(Quaternion.Inverse(b[0].rotation) * (b[2].position - b[1].position), f.Rest1)
                 + Vector3.Angle(Quaternion.Inverse(b[1].rotation) * (b[3].position - b[2].position), f.Rest2)
                 + Quaternion.Angle(Quaternion.Inverse(b[2].rotation) * b[3].rotation, f.RestTip);
            return true;
        }

        // the same in the skin's bind pose (bindposes[i].inverse: the bone in the mesh's frame at rest)
        private bool FingerRest(Finger f)
        {
            VisEquipment ve = _h.GetComponent<VisEquipment>();
            SkinnedMeshRenderer smr = ve != null ? ve.m_bodyModel : null;
            if (smr == null || smr.sharedMesh == null) return false;
            Transform[] bones = smr.bones; // heavy: once a finger, the first time a want asks
            Matrix4x4[] bind = smr.sharedMesh.bindposes;
            if (bones == null) return false;
            Matrix4x4[] at = new Matrix4x4[4];
            int found = 0;
            for (int i = 0; i < bones.Length && i < bind.Length; i++)
            {
                if (bones[i] == null) continue;
                Transform real = BodyBuild.Real(bones[i]);
                for (int k = 0; k < 4; k++) if (real == f.B[k]) { at[k] = bind[i].inverse; found |= 1 << k; }
            }
            if (found != 15) return false;
            Vector3 j1 = at[1].MultiplyPoint3x4(Vector3.zero), j2 = at[2].MultiplyPoint3x4(Vector3.zero), j3 = at[3].MultiplyPoint3x4(Vector3.zero);
            f.Rest1 = Quaternion.Inverse(at[0].rotation) * (j2 - j1);
            f.Rest2 = Quaternion.Inverse(at[1].rotation) * (j3 - j2);
            f.RestTip = Quaternion.Inverse(at[2].rotation) * at[3].rotation;
            return true;
        }

        private static float WArg(string[] w, int i, float def) { float v; return w.Length > i && float.TryParse(w[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        // (value: higher is nearer)
        private bool HandAsked(string which, bool left, string[] w, out float value)
        {
            value = 1f;
            if (which != "both" && (which == "left") != left) return true;
            Vector3 h = L(left ? HandL : HandR);
            if (w[2] == "holds") // (the thing in the hands: GripOf's own measure — palm on it, turned to it, fingertips on it, no fist)
            {
                int g = left ? 0 : 1;
                if (!_gripOn[g]) { value = -9f; return false; } // (this frame's GripOf: no thing, or this hand not on it)
                value = -_gripNowBad[g];
                return _gripNowWhy[g] == null;
            }
            if (w[2] == "on")
            {
                if (Fixtures.Count == 0 && Real) { float g = _realGap[left ? 0 : 1]; value = -Mathf.Abs(g); return g <= TouchFar && g >= -TouchIn; } // (a scene: the world's own surface)
                value = -9f;
                foreach (Fixture f in Fixtures) { if (f.Kind == "point") continue; value = Mathf.Max(value, -Mathf.Abs(f.Distance(h) - Palm)); }
                return value >= -TouchFar;
            }
            Vector3 at = new Vector3(WArg(w, 3, 0f), WArg(w, 4, 1f), WArg(w, 5, 0.3f));
            value = -Vector3.Distance(h, at);
            return value >= -WArg(w, 6, 0.1f);
        }

        private bool PalmAsked(string which, bool left, string way, out float value)
        {
            value = 180f;
            if (which != "both" && (which == "left") != left) return true;
            value = 0f;
            BodyAnimator an = BodyAnimator.Of(_h);
            Transform hand = _b[left ? HandL : HandR];
            Vector3 along, palm;
            if (an == null || hand == null || !an.HandFacing(left, hand, out along, out palm) || palm.sqrMagnitude < 1e-8f) return false;
            Vector3 d = _h.transform.InverseTransformDirection(palm).normalized;
            Vector3 want = way == "forward" ? Vector3.forward : way == "back" ? Vector3.back : way == "up" ? Vector3.up : way == "down" ? Vector3.down : way == "right" ? Vector3.right : Vector3.left;
            value = 180f - Vector3.Angle(d, want);
            return value >= 135f;
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static string V(Vector3 v) { return N(v.x) + " " + N(v.y) + " " + N(v.z); }
        private static string P(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    //   moved   a limb's end, at the frame's END, off where the pose's own solve put it (BodyAnimator.SolvedAt) by more than MovedFar: something moved
    //           it after our pose in the same frame — the game's own step on that body, or a later layer of ours. «miss» is read off the solve and is
    //           blind to it: the game drew a woman player's shoulders in after the pose, both her hands stood 3.65 cm inward and every rule here said
    //           nothing (take_book in its scene, 2026-11-12: found only by the grip sheet's «wrist» against «asked»). Not the clip's to mend: the core's
    //   speed   a hand or a foot flying, a bone turning in a jerk (only while one of our clips holds the body: the game's own gestures are not ours)
    //   slide   a foot going along the ground while the body stands (near the lowest it has been, not in a step: a step is no slide)
    //   shoulder  a collarbone turned far off how it stood (a reach out of the arm's length pulls it)
    //   neck    the head turned far off the chest
    //   free hand  swinging far behind the body as it bends
    //   prop    the head into the thing, fingers inside it, a forearm, an arm, a thigh, the trunk through it (Limbs)
    //   grip    a hand the clip has on the thing («@LeftHand.on» at full weight): does it hold it — its palm on the thing and turned to it,
    //           three of its four fingers LYING on its surface (the tip on it and the last bone along it, not end on: a hook pokes), the thumb
    //           on it too (or the thing resting on a palm turned up), not shut to a fist — or what touches instead: the user 2026-11-07, a left
    //           fist before the book passed every rule above; 2026-11-11, «two fingertips near it» said HOLDS of hooks. The pose's words say
    //           every digit each frame, m off its surface (GripOf, DigitsSaid); the full report carries the hand's bones (GripSheet) for
    //           tools/grip.js, which reckons the wrist's place and each finger's bend: a grip is not turned by eye
    //   touch-fingers  a palm held on a fixture (its target at the surface, full weight, the clip not closing the fingers itself): three of its
    //           four fingers lie on that surface too — the same measure as a grip's (Digits), said in the pose's words for a hand ON a fixture
    //   touch   the stage's fixtures (AnimStageFixtures: boxes in the body's frame): a hand or a foot whose target lies at a fixture's
    //           surface (within Meant of it), at its full weight, is off that surface or into it; the hips onto a seat; a hand to a point
    //   still   in a scene's shot (InShot): the body in the picture and nothing of it moving for longer than StillFor s — no bone, no limb: an actor
    //           left standing like a post while the shot holds on it (the game's own idle breathes: that is no stillness)
    //   start   in a scene (InScene): a clip of ours came on a body that was not in the pose the clip begins from — over the clip's way in the hips
    //           or the back went much farther than the clip's own keys ask there, so its fade carried the body (a «rises» played on one who stands,
    //           a part that kneels begun without the part that goes down)
    //   real    in a scene (Real): a hand its clip says touches something («touch wall 0.55», «touch piece <prefab> 0.55»), its target at that
    //           surface, at full weight: how far the world's real surface is, by a ray along its palm (a stage's box is not the gate)
    // Each is a number kept with the moment it was worst; Verdict says the ones over their limit, with what to change, and Moments
    // says when, so that frame can be looked at. Each line begins with its rule's tag («[miss]», «[bend]» …: tools/anim.js approved lists
    // by it) and, where the core can reckon the change itself, ends with a ready command «fix: node tools/anim.js …» (Cmd: the key of that
    // channel nearest the moment, in the clip's one copy; a part of a clip is its whole clip's file). The numbers in it are the core's
    // own (the shoulder, the arm's reach, how a spine key read as a lean here), near, not exact: the next take says how near (the user
    // 2026-11-10, review 2: a weaker model took a take a guess, four takes a change). Nothing here is hidden or loosened because a clip the
    // user accepted trips it (the user 2026-11-10: what he accepted may be wrong, the point is to find it): a limit moves only by what a
    // body can do, or by the user's word over a frame; tools/anim.js approved lists what fires on accepted clips for him to judge.
    // Fold: those lines for a reader, one line an event (fall_back 2026-11-11: 18 lines for 10 events, six of them both hands flying once,
    // and a weaker model set to mend each line): a jerk the flight of its own limb made is a tail of that flight's line («(+ jerks with it:
    // LeftArm 4616, … deg/s at 0.64–0.68 s)»: an arm's bone under that hand's flight, a leg's under that foot's — a bone of another limb
    // stays a line a reader can doubt), lines of one rule with one explanation near in time (a left and a right limb: FoldNear, FoldJerk)
    // are one line naming each with its number, every «fix:» kept under it once, the lines by the time of the event; in a scene no event
    // goes across a step's edge. The stage and a scene both say their VERDICT through it; the lines as measured, a line a rule and a
    // bone, stay in their full reports (nothing is dropped, only said once).
    // Subject: a line's tag and what it is about, for a row that points at it (the key sheet's rows do not say the VERDICT again).
    // Marks: for a shot of the stage, what the eye cannot tell on 20 px of a hand, drawn as lines (Studio.Mark): the fingers' way (yellow),
    // the palm's normal (cyan), the normal the clip asks (magenta), each target with weight as a red cross with a line from its limb.
    // The clip's own «want» lines (the user's words as measures, BodyClips «say», «want»): Wants reads them, Measure judges them, WantsSaid
    // says how each came out. Times are the whole clip's, so only on a whole clip played on the stage. The words (Holds; tools/clipcheck.js spells them):
    //   head up|down|left|right <deg> · lean forward|back|left|right <deg> · hips low <m> · hand left|right|both on [body [m]] | holds | near <x y z> [m] | touches prop | below shoulder ·
    //   palm left|right|both forward|back|up|down|left|right | to prop · foot left|right up <m> | down · knee left|right bent <deg> · elbow left|right bent <deg> · weight left|right ·
    //   look prop | <x y z> · <t0-t1> still [deg/s] · <t0-t1> slow <m/s> · <t0-t1> smooth · finger left|right|both index|middle|ring|pinky straight [deg]  (a side is a word, never L/R)
    //   (the user's words go in as they are: «права рука торкається кошика» is «hand right touches prop», no reckoning by the model — review 2 2026-11-10, Г12)
    //   body    how far a palm (the hand and its middle knuckle) is off the trunk's surface — the same column as «inside»: said in the pose's
    //           words for a hand within BodySay of it, judged by «want … hand left on body [m]» (the user 2026-11-09: a hand meant on the belly
    //           hung 30–40 cm before it, and nothing said so — a front view shows no depth)
    //   wrist   a hand off its forearm beyond what a wrist turns or bends (its rest pose against the forearm, from the skeleton): the palm
    //           rule reads back what the core sets, so a twisted wrist under a right palm passed it (review 2026-11-07)
    // Words: the pose now in words (lean, head, weight, hands, knees), the reader's way into a frame. Every reference is the body's own
    // build, never the first frame measured (a scene starts mid-pose: «trunk straight» on a bent body, 2026-11-07): the trunk's lean from
    // the skeleton's rest (bind) pose, the hips' standing height from its legs at the game's stance knee (StanceKnee); the head on the
    // body and on the chest. Stance numbers said in the log once a body (Stance).
    // Differs: a report's later rows say only the parts of those words («trunk», «L hand», «knees» …) that differ from the pose as the reader
    // has it; a part with the same words and its numbers within SameDeg / SameM of the ones said is the same (the full report has every row whole).
    // Light: a few bones a frame, found once; the fixtures a handful of boxes.
    internal sealed class BodyCheck
    {
        internal static float PalmOff = 25f, FistCurl = 60f, InsideDeep = 0.2f, Past = 0.03f, ElbowBack = 0.2f, KneeUnder = 0.02f, Full = 0.95f; // deg · deg · how deep into the trunk's column (0 its surface, 1 the spine) · m · m · m · a target's weight that counts as held
        internal static float TouchFull = 0.99f, Meant = 0.15f, TouchFar = 0.07f, TouchIn = 0.03f, Palm = 0.03f, Sole = 0.06f, SeatHover = 0.1f, PointNear = 0.05f; // a target's weight a touch is measured at (at 0.95 the core still holds a limb 5% back to the game's pose: 2-4 cm short on its way in and out, no mistake) · m from a surface its target means it · m off · m in · the wrist off the palm's face · the ankle off the sole · m the hips hover over a seat · m a hand from a point
        internal static float FootFast = 1.5f, HandFast = 3f, BoneJerk = 600f, MissFar = 0.05f, KneeCross = 0.05f, HandBehindFar = 0.28f, BentDown = 0.2f, HeadNear = 0.3f, FingerDeep = 0.02f; // m/s · m/s · deg/s · m · m · m · m the hips down to count as bent · m · m
        internal static float MovedFar = 0.01f; // m a limb's end may stand off its own solve at the frame's end (reasoned from two readings, 2026-11-12: 0.000 on the mannequin, 0.035 with the game's shoulder step after the pose — nothing between them measured yet)
        internal static float FootOnGround = 0.03f, FootSlide = 0.2f, StandSpeed = 0.2f, StepMost = 0.1f; // m over its lowest it is still on the ground · m along the ground in one run is a slide · m/s the body counts as standing · m a frame: more, it was put there
        internal static float ShoulderBent = 25f, NeckMost = 75f, ElbowFold = 25f; // deg
        internal static float HeadUp = 0.1f, HeadRound = 0.4f, HeadDeep = 0.05f, HeadNearSay = 0.1f; // m the head's middle over its bone · its radius × the shoulders' width · how deep in counts
        internal static float BodyOn = 0.05f, BodyInMost = 0.03f, BodySay = 0.5f; // m a palm off the trunk still on it · m into it still on it · m: nearer than this, the pose's words say how far
        internal static float RealBack = 0.4f, RealFar = 1.4f, WantAround = 0.2f, WantShare = 0.9f; // m a ray starts behind the hand · m it goes · s round a want's one moment · the share of frames a want's stretch must hold
        internal static float SpineMost = 45f, HoldNear = 0.35f, SpineBent = 58f, MoveMost = 0.3f; // deg a spine key a lean fix gives at most · s round a key its hold's keys lie (a lean fix sets them all) · deg of spine keys in all past which more gives no lean · m a fix lowers the body at most: more is a new pose, not a key
        internal static float WantDeg = 3f; // deg a «head»/«lean» want may fall short and still hold: the user's words are a near number, and take_book's 24.5 for an asked 25 sent a model after half a degree (2026-11-10)

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
            internal Vector3 Normal(Vector3 p) // (the way out of its surface near p, body frame: the slope of Distance)
            {
                const float e = 0.004f;
                Vector3 g = new Vector3(Distance(p + Vector3.right * e) - Distance(p - Vector3.right * e), Distance(p + Vector3.up * e) - Distance(p - Vector3.up * e), Distance(p + Vector3.forward * e) - Distance(p - Vector3.forward * e));
                return g.sqrMagnitude > 1e-10f ? g.normalized : Vector3.up;
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

        private readonly Transform[,] _knuckle = new Transform[2, 5], _fingEnd = new Transform[2, 5]; // (joint 1, where the finger leaves the palm · the skeleton's own end of the finger past joint 3, where it has one)

        // a finger's tip in the world: the skeleton's own end of it where the last bone has one (TipFrom..TipTo m past the last joint: anything else is
        // not a fingertip), else its last joint and on by most of the bone before it (the last bone's pivot is the last joint, ~2 cm short). The grip
        // sheet says which a body has (GripSheet)
        internal static float TipFrom = 0.005f, TipTo = 0.05f;
        private bool Tip(int side, int finger, out Vector3 tip)
        {
            Transform a = _fing[side, finger, 0], b = _fing[side, finger, 1], end = _fingEnd[side, finger];
            tip = b != null ? b.position : Vector3.zero;
            if (b != null && end != null) { tip = end.position; return true; }
            if (a == null || b == null) return b != null;
            tip = b.position + (b.position - a.position) * 0.8f;
            return true;
        }

        private readonly float[] _palm = new float[2], _palmAt = new float[2], _curl = new float[2], _curlAt = new float[2], _past = new float[2], _pastAt = new float[2], _elbow = new float[2], _elbowAt = new float[2];
        private float _inside, _insideAt, _knee = 9f, _kneeAt, _inHead, _inHeadAt, _headGap = 9f, _headGapAt; private string _inHeadWhat = "", _headGapWhat = "";
        private string _insideWhat = "";
        // a touch's worst, by limb and fixture («LeftHand@wall»)
        private sealed class Touch { public float Far = -9f, FarAt, In = 9f, InAt; public Vector3 FarWant, InWant, FarNear, InNear; public string Limb = "", FarClip, InClip; public float FarClipAt, InClipAt; } // (Near: the fixture's nearest point to the target then — the way a fix moves it)
        private readonly Dictionary<string, Touch> _touch = new Dictionary<string, Touch>();
        private float _hipsLow = 9f, _hipsHigh = -9f, _hipsGap = 9f, _hipsIn = 9f, _hipsInAt; private string _seat = "";
        private readonly Dictionary<string, float> _point = new Dictionary<string, float>(); // (a hand's nearest to each point)
        // moved here from the stage and the scene's own checks
        private sealed class Worst { public float V, At; public string Clip; public float ClipAt; public Vector3 Tgt, Joint; public float Reach; public bool OnThing; }
        private string _clipNow; private float _momentNow; // (the topmost clip of ours this frame and its moment: what a fix names)
        private readonly Dictionary<string, Worst> _miss = new Dictionary<string, Worst>(), _fast = new Dictionary<string, Worst>(), _moved = new Dictionary<string, Worst>();
        private readonly Dictionary<string, int> _movedFrames = new Dictionary<string, int>(), _solvedFrames = new Dictionary<string, int>(); // (frames a limb stood over MovedFar off its solve · frames it was solved at all)
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
                {
                    for (int j = 0; j < 2; j++) _fing[s, k, j] = BodyClips.SkeletonBone(h, _vis, (s == 0 ? "Left" : "Right") + "Hand" + Fingers[k] + (j + 2));
                    _knuckle[s, k] = BodyClips.SkeletonBone(h, _vis, (s == 0 ? "Left" : "Right") + "Hand" + Fingers[k] + "1");
                    Transform last = _fing[s, k, 1];
                    for (int i = 0; last != null && i < last.childCount; i++) // (its own end bone, if the skeleton has one: «…Index4», «…Index3_end»)
                    {
                        Transform e = last.GetChild(i); float far = Vector3.Distance(e.position, last.position);
                        if (e.name.IndexOf(Fingers[k], StringComparison.Ordinal) >= 0 && far >= TipFrom && far <= TipTo) { _fingEnd[s, k] = e; break; }
                    }
                }
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

        // the head as a ball, in the world: its middle and its radius (read from the skin, else guessed from the shoulders: HeadShape)
        private void HeadBall(Transform root, float width, out Vector3 c, out float r)
        {
            c = _headR > 0f ? _b[Head].TransformPoint(_headC) : _b[Head].position + root.up * HeadUp;
            r = _headR > 0f ? _headR * _b[Head].lossyScale.x : HeadRound * width;
        }
        // … for a row of the report: «headball x,y,z r» in the body's frame (tools/body.js fits the head of its model to these)
        private string HeadSaid()
        {
            if (_b[Head] == null || !_headRead) return "";
            float width = _b[ArmL] != null && _b[ArmR] != null ? Vector3.Distance(_b[ArmL].position, _b[ArmR].position) : 0.36f;
            Vector3 c; float r; HeadBall(_h.transform, width, out c, out r);
            return "  headball " + P(_h.transform.InverseTransformPoint(c)) + " r " + r.ToString("0.000", CultureInfo.InvariantCulture) + (_headR > 0f ? "" : " guessed");
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
            PAnimClip nowClip;
            { float m = 0f; nowClip = an != null ? an.CodeClip(out m) : null; _clipNow = nowClip != null ? nowClip.Name : null; _momentNow = m; }
            if (_first) First(root);
            _realGap[0] = _realGap[1] = 9f; _gripOn[0] = _gripOn[1] = false;
            Vector3 hips = L(Hips);
            if (InScene) Began(nowClip, hips.y, t);
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
                        if (curl > _curl[s]) { _curl[s] = curl; _curlAt[s] = t; _curlClip[s] = _clipNow; _curlClipAt[s] = _momentNow; }
                    }
                    if (on != null && tw >= TouchFull && !curls) TouchDigits(s, on, an, t);
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
                if (on != null && tw >= TouchFull) Keep((left ? "LeftHand@" : "RightHand@") + on.Name, on.Distance(hl) - Palm, t, tgt, limb, on);
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
                    if (d.w < 0f) d = new Quaternion(-d.x, -d.y, -d.z, -d.w); // (q and −q are one turn: with w below 0 a near-straight wrist read as ±180, 2026-11-10)
                    Vector3 axis = (Quaternion.Inverse(_wristRest[s]) * fore.InverseTransformPoint(hand.position)).normalized; // (the forearm's length, in the hand's rest frame: d turns there)
                    Vector3 dq = new Vector3(d.x, d.y, d.z), proj = Vector3.Project(dq, axis);
                    Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, d.w);
                    float twisted = Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(Vector3.Dot(dq, axis), d.w) * Mathf.Rad2Deg); // (signed round the forearm, from the rest)
                    float bend = Quaternion.Angle(Quaternion.identity, d * Quaternion.Inverse(Normal(twist)));
                    if (twisted > _wristTwist[s]) { _wristTwist[s] = twisted; _wristTwistAt[s] = t; }
                    if (twisted < _wristTwistLow[s]) { _wristTwistLow[s] = twisted; _wristTwistLowAt[s] = t; }
                    if (bend > WristBend && !onThing) // (every frame over the limit, for the stretch the line says and the keys its fix mends: BendOver)
                    {
                        Vector3 f = Vector3.zero; float fWeight = 0f;
                        bool hasF = an.ChannelNow(limb + ".f", out f, out fWeight) && f.sqrMagnitude > 1e-6f;
                        _wristOver[s].Add(new BendAt { T = t, Clip = _clipNow, ClipAt = _momentNow, Bend = bend, HasF = hasF, F = hasF ? f.normalized : Vector3.zero, Fore = root.InverseTransformDirection(hand.position - fore.position).normalized });
                    }
                    if (bend > _wristBend[s])
                    {
                        _wristBend[s] = bend; _wristBendAt[s] = t; _wristBendClip[s] = _clipNow; _wristBendClipAt[s] = _momentNow; _wristOn[s] = onThing;
                        _wristFore[s] = root.InverseTransformDirection(hand.position - fore.position).normalized; // (the forearm's way, in the body's frame: what .f is turned toward)
                        Vector3 askedF = Vector3.zero; float askedFw = 0f;
                        _wristHasF[s] = !onThing && an.ChannelNow(limb + ".f", out askedF, out askedFw) && askedF.sqrMagnitude > 1e-6f; _wristF[s] = _wristHasF[s] ? askedF.normalized : Vector3.zero;
                        Vector3 al, pm; _wristPalm[s] = an.HandFacing(left, hand, out al, out pm) && pm.sqrMagnitude > 1e-8f ? root.InverseTransformDirection(pm).normalized : Vector3.up;
                    }
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
                if (on != null) Keep((s == 0 ? "LeftFoot@" : "RightFoot@") + on.Name, on.Distance(L(s == 0 ? FootL : FootR)) - Sole, t, tgt, limb, on);
            }
            // the hips onto a seat
            foreach (Fixture f in Fixtures)
            {
                if (f.Kind != "seat") continue;
                float d = f.Distance(hips) - 0.1f; // (the hips' bone sits about this far over what it sits on)
                if (d < _hipsIn) { _hipsIn = d; _hipsInAt = t; _seat = f.Name; _hipsInClip = _clipNow; _hipsInClipAt = _momentNow; }
                if (hips.y <= _hipsLow) { _hipsGap = d; _seat = f.Name; _hipsGapClip = _clipNow; _hipsGapClipAt = _momentNow; }
            }
            // inside the trunk: hands and the forearms' middles against the column hips → chest → neck
            float width = _b[ArmL] != null && _b[ArmR] != null ? Vector3.Distance(_b[ArmL].position, _b[ArmR].position) : 0.36f;
            float r = 0.32f * width;
            CheckIn(HandL, -1, r, t, "left hand"); CheckIn(HandR, -1, r, t, "right hand");
            CheckIn(ForeL, HandL, r, t, "left forearm"); CheckIn(ForeR, HandR, r, t, "right forearm");
            if (_b[Head] != null)
            {
                if (!_headRead) HeadShape(width);
                Vector3 hc; float hr; HeadBall(root, width, out hc, out hr);
                InHead(hc, hr, _b[HandL], null, t, "left hand"); InHead(hc, hr, _b[HandR], null, t, "right hand");
                InHead(hc, hr, _b[ForeL], _b[HandL], t, "left forearm"); InHead(hc, hr, _b[ForeR], _b[HandR], t, "right forearm");
            }
            Vector3 lk = L(KneeL), rk = L(KneeR);
            if (_b[KneeL] != null && lk.y < _knee) { _knee = lk.y; _kneeAt = t; _kneeClip = _clipNow; _kneeClipAt = _momentNow; }
            if (_b[KneeR] != null && rk.y < _knee) { _knee = rk.y; _kneeAt = t; _kneeClip = _clipNow; _kneeClipAt = _momentNow; }
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
                    if (e > w.V)
                    {
                        w.V = e; w.At = t; w.Clip = _clipNow; w.ClipAt = _momentNow;
                        // what a fix reckons with: the limb's root (the shoulder, the hip) in the body's frame, how far the limb reaches, the target it was sent to
                        int up = m.Name == "@LeftHand" ? ArmL : m.Name == "@RightHand" ? ArmR : m.Name == "@LeftFoot" ? UpL : UpR, low = m.Name == "@LeftHand" ? ForeL : m.Name == "@RightHand" ? ForeR : m.Name == "@LeftFoot" ? KneeL : KneeR, end = m.Name == "@LeftHand" ? HandL : m.Name == "@RightHand" ? HandR : m.Name == "@LeftFoot" ? FootL : FootR;
                        w.Joint = L(up);
                        w.Reach = _b[up] != null && _b[low] != null && _b[end] != null ? Vector3.Distance(_b[up].position, _b[low].position) + Vector3.Distance(_b[low].position, _b[end].position) : 0f;
                        Vector3 tg; float tgw, ow; Vector3 ig;
                        w.OnThing = an.PropOn && an.ChannelNow(m.Name + ".on", out ig, out ow) && ow >= 0.5f;
                        w.Tgt = an.ChannelNow(m.Name, out tg, out tgw) ? tg : Vector3.zero;
                    }
                }
            // moved: each solved limb's end now, at the frame's end, against where its solve left it — both in the body's frame (the head of this file)
            if (an != null)
                foreach (BodyClips.Limb m in BodyClips.Limbs)
                {
                    Vector3 solved;
                    int end = m.Name == "@LeftHand" ? HandL : m.Name == "@RightHand" ? HandR : m.Name == "@LeftFoot" ? FootL : FootR;
                    if (_b[end] == null || !an.SolvedAt(m.Name, out solved)) continue;
                    float off = Vector3.Distance(L(end), solved);
                    int n; _solvedFrames.TryGetValue(m.Name, out n); _solvedFrames[m.Name] = n + 1;
                    if (off > MovedFar) { _movedFrames.TryGetValue(m.Name, out n); _movedFrames[m.Name] = n + 1; }
                    Worst w;
                    if (!_moved.TryGetValue(m.Name, out w)) _moved[m.Name] = w = new Worst { V = -1f };
                    if (off > w.V) { w.V = off; w.At = t; w.Tgt = L(end) - solved; } // (Tgt: which way it went, in the body's frame — x right, y up, z ahead)
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
        // the user sets WristTwistFrom/To by what a forearm turns (deg, signed, each side as it reads). Bend: off the forearm, judged
        internal static float WristTwistFrom = 0f, WristTwistTo = 0f, WristBend = 80f; // (From = To: the twist not judged) · deg a wrist bends, a guess from the human wrist (flexion about 80), NOT measured in the game
        private readonly float[] _wristTwist = { -999f, -999f }, _wristTwistAt = new float[2], _wristTwistLow = { 999f, 999f }, _wristTwistLowAt = new float[2], _wristBend = new float[2], _wristBendAt = new float[2];
        private readonly string[] _wristBendClip = new string[2], _curlClip = new string[2]; private readonly float[] _wristBendClipAt = new float[2], _curlClipAt = new float[2]; // (what a fix names)
        private readonly Vector3[] _wristFore = new Vector3[2], _wristF = new Vector3[2], _wristPalm = new Vector3[2]; private readonly bool[] _wristHasF = new bool[2], _wristOn = new bool[2];
        // A wrist over its limit, frame by frame (BendOver): the worst moment alone said «at 2.67 s» of a wrist bent the whole scratch long, its fix
        // mended that one moment, and the next take showed «a new bend» at 1.83 s that had been there all along (scratch_head 2026-11-11)
        private struct BendAt { public float T, ClipAt, Bend; public string Clip; public bool HasF; public Vector3 F, Fore; }
        private readonly List<BendAt>[] _wristOver = { new List<BendAt>(), new List<BendAt>() };
        internal static float BendGap = 0.25f, BendMoment = 0.15f, BendKeyPad = 0.15f, BendKeyNear = 0.2f; // s: two frames over the limit this near are one stretch · stretches shorter than this in all are a moment, not said · a key this near a stretch is the stretch's · a key is mended by a frame this near it
        private string _hipsInClip, _hipsGapClip, _kneeClip; private float _hipsInClipAt, _hipsGapClipAt, _kneeClipAt;
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
                        if (sp > w.V) { w.V = sp; w.At = t; w.Clip = _clipNow ?? ""; w.ClipAt = _momentNow; }
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
                        if (sp > w.V) { w.V = sp; w.At = t; w.Clip = _clipNow ?? ""; w.ClipAt = _momentNow; }
                    }
                }
                _was[i] = q; _wasSeen[i] = true;
            }
            Tempo(t);
            // «still»: in the picture of a shot and nothing of it moves
            if (InShot && _limbSeen && _sp < StillM && _spin0 < StillDeg)
            {
                if (_stillFrom < 0f) _stillFrom = t;
                if (t - _stillFrom > _still) { _still = t - _stillFrom; _stillAt = _stillFrom; }
            }
            else _stillFrom = -1f;
        }

        // the scene's check says these (StudioScene): the body is one of a scene · it is in the picture of a shot this frame
        internal bool InScene, InShot;
        internal static float StillFor = 3f, StillDeg = 2f, StillM = 0.01f; // s nothing of it moves in a shot before it is said · deg/s and m/s under which a bone and a limb do not move (unmeasured: first numbers; a body frozen outright reads 0)
        private float _stillFrom = -1f, _still, _stillAt;
        internal static float StartHips = 0.12f, StartLean = 20f, StartLeast = 0.2f; // m the hips, deg the back may go over a clip's way in past what its own keys ask there · s: a way in is at least this long (unmeasured: first numbers)
        private PAnimClip _startClip; private string _clipBefore; private float _startHips, _startLean, _startT, _hipsBefore, _leanBefore; private bool _poseWas;
        private string _startSaid; private float _startAt, _startOff;
        // a clip of ours comes on: the pose just before it kept, and at the end of its way in — did the body go where the clip's own keys do not send it?
        private void Began(PAnimClip now, float hipsY, float t)
        {
            float lean, side; Lean(out lean, out side, false);
            if (now != null && now.Name != _clipBefore && _poseWas) { _startClip = now; _startHips = _hipsBefore; _startLean = _leanBefore; _startT = t; }
            _clipBefore = now != null ? now.Name : null;
            if (_startClip != null && now != _startClip) _startClip = null; // (gone before its way in ended: nothing to say)
            if (_startClip != null && _momentNow >= Mathf.Max(StartLeast, _startClip.FadeIn))
            {
                PAnimClip c = _startClip; float m = _momentNow;
                _startClip = null;
                float askedY = c.Bones.Contains(BodyClips.Move) ? Mathf.Abs(c.Value(BodyClips.Move, m).y - c.Value(BodyClips.Move, 0f).y) : 0f, askedLean = 0f;
                foreach (string b in new[] { "Spine1", "Spine2", BodyClips.Body }) if (c.Bones.Contains(b)) askedLean += Mathf.Abs(c.Value(b, m).x - c.Value(b, 0f).x);
                float down = _startHips - hipsY, hipsOff = Mathf.Abs(down) - askedY, leanOff = Mathf.Abs(lean - _startLean) - askedLean;
                float off = Mathf.Max(hipsOff / StartHips, leanOff / StartLean);
                if (off > 1f && off > _startOff)
                {
                    _startOff = off; _startAt = _startT;
                    _startSaid = c.Name + " came on at " + N(_startT) + " s on a body not in the pose it begins from: over its first " + N(m) + " s the hips went " + N(Mathf.Abs(down)) + " m " + (down > 0f ? "down" : "up") + " and the back turned " + N(Mathf.Abs(lean - _startLean)) + " deg, where its own keys ask " + N(askedY) + " m and " + N(askedLean) + " deg — its fade carried the body there: play first the clip or the part that brings the body to that pose (its row in docs/CLIPS.md, «від → до»), or a clip that begins from how the body stands";
                }
            }
            _hipsBefore = hipsY; _leanBefore = lean; _poseWas = true;
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
                GripLook(s, an, view, t);
                if (why == null) continue;
                _gripBad[s]++;
                if (bad > _gripWorst[s]) { _gripWorst[s] = bad; _gripAt[s] = t; _gripWhy[s] = why; _gripClip[s] = _clipNow; }
            }
        }

        // ---------------------------------------------------------------- grip
        // A hand on the thing, digit by digit (the user 2026-11-11: «ти явно не бачиш як пальці виглядають на книзі» — a grip turned by eye take after
        // take, and «two fingertips near it» had said HOLDS of the hooks he called fists). Each digit against the thing's surface: its tip's distance
        // (below zero: inside), its middle joints' (a knuckle through it), and how its last bone meets the surface — along it (it lies: the pad on the
        // thing) or end on (it pokes: a hook). A hand holds when its palm is on the thing and turned to it, GripLie of its four fingers lie on it and
        // its thumb is on it too (or the thing rests on a palm turned up), and it is not shut to a fist. These limits are a hand's, set by reasoning,
        // NOT measured in the game — and never to be fitted to a clip someone accepted. The pose's words say every digit each frame (DigitsSaid);
        // the full report carries a sheet of the hand's own bones at the middle of its time on the thing (GripSheet): tools/grip.js reckons from it
        // where the wrist goes and how far each finger bends, so a grip is not mended a take a guess.
        internal static float GripPalm = 0.045f, GripTip = 0.025f, GripShut = 0.045f, GripShare = 0.1f, GripSteep = 50f, GripUnder = 0.7f, GripSheetEvery = 0.5f; // m the palm's face off the thing · m a real fingertip off its surface still on it · m a fingertip this near its own palm and off the thing: curled into it · the share of frames on it that may not hold · deg a finger's last bone into the surface past which it pokes, not lies · the palm's way up (its cosine) from which the thing rests on it · s between the sheet's looks
        internal static int GripLie = 3; // of its four fingers
        private readonly int[] _gripFrames = new int[2], _gripBad = new int[2];
        private readonly float[] _gripWorst = new float[2], _gripAt = new float[2];
        private readonly string[] _gripWhy = new string[2], _gripNowWhy = new string[2], _gripClip = new string[2];
        private readonly bool[] _gripOn = new bool[2]; private readonly float[] _gripNowBad = new float[2]; // (this frame's, for «want … holds»)
        private static readonly string[] DigitWord = { "thumb", "index", "middle", "ring", "pinky" };
        private readonly float[,] _digTip = new float[2, 5], _digMid = new float[2, 5], _digSteep = new float[2, 5]; // (as GripOf last measured them)
        private readonly bool[,] _digHas = new bool[2, 5];
        private readonly float[] _digPalm = new float[2];

        // a digit as last measured, in a word: IN the thing · lies on it · pokes at it (on it, end on) · stands on it (its tip on it, the finger steeply off it) · on it (the thumb) · in the air
        private string DigitState(int side, int k)
        {
            float d = _digTip[side, k];
            if (d < -FingerDeep || _digMid[side, k] < -FingerDeep) return "IN";
            if (d > GripTip) return "air";
            return k == 0 ? "on" : _digSteep[side, k] > GripSteep ? "pokes" : _digSteep[side, k] < -GripSteep ? "stands" : "lies"; // (stands: its last bone goes steeply OFF the surface, only its tip near it — take_book's first measured take read «lies -64», «lies -82» of fingers standing up from the book's edge, 2026-11-11)
        }
        // … all five, for the pose's words (m off the thing's surface, below zero inside it; after a finger's word the degrees its last bone goes into the surface)
        private string DigitsSaid(int side)
        {
            StringBuilder s = new StringBuilder("palm ").Append(M(_digPalm[side]));
            for (int k = 0; k < 5; k++)
            {
                if (!_digHas[side, k]) continue;
                string st = DigitState(side, k);
                s.Append(" · ").Append(DigitWord[k]).Append(' ').Append(M(_digTip[side, k])).Append(' ').Append(st);
                if (st == "IN" && _digMid[side, k] < _digTip[side, k]) s.Append(" (a joint ").Append(M(_digMid[side, k])).Append(')');
                else if (k > 0 && st != "air") s.Append(' ').Append(Mathf.RoundToInt(_digSteep[side, k]));
            }
            return s.ToString();
        }
        private static string M(float v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        // every digit of a hand against a surface (dist: m off it, below zero inside · normal: its way out there), kept for the words (_dig…): how many
        // of the four fingers lie on it, is the thumb on it, how many are curled back to the palm (face: the palm's middle); off: each finger that does not lie, in words.
        // One measure for the thing in the hands (GripOf) and for what a palm is held on — a wall, a table, a door (TouchDigits)
        private void Digits(int side, Vector3 face, Func<Vector3, float> dist, Func<Vector3, Vector3> normal, List<string> off, out int lie, out int fingers, out bool thumb, out int shut)
        {
            lie = 0; fingers = 0; shut = 0; thumb = false;
            for (int k = 0; k < 5; k++)
            {
                Vector3 tip;
                _digHas[side, k] = Tip(side, k, out tip);
                if (!_digHas[side, k]) continue;
                Transform j2 = _fing[side, k, 0], j3 = _fing[side, k, 1];
                float d = dist(tip);
                _digTip[side, k] = d;
                _digMid[side, k] = Mathf.Min(j2 != null ? dist(j2.position) : 9f, j3 != null ? dist(j3.position) : 9f);
                Vector3 last = j3 != null ? tip - j3.position : Vector3.zero;
                _digSteep[side, k] = last.sqrMagnitude > 1e-8f ? Mathf.Asin(Mathf.Clamp(Vector3.Dot(last.normalized, -normal(tip)), -1f, 1f)) * Mathf.Rad2Deg : 0f; // (how far its last bone goes INTO the surface: 0 along it, 90 end on)
                string st = DigitState(side, k);
                if (k == 0) { thumb = st == "on"; continue; }
                fingers++;
                if (st == "lies") lie++;
                else
                {
                    off.Add(DigitWord[k] + (st == "IN" ? " " + N(-Mathf.Min(d, _digMid[side, k])) + " m inside it" : st == "pokes" ? " pokes at it end on (" + Mathf.RoundToInt(_digSteep[side, k]) + " deg into its surface)" : st == "stands" ? " stands off it on its tip (" + Mathf.RoundToInt(-_digSteep[side, k]) + " deg off its surface)" : " " + N(d) + " m in the air"));
                    if (st == "air" && Vector3.Distance(tip, face) < GripShut) shut++; // (curled back to its own palm and not on the surface: a fist)
                }
            }
        }

        // a palm held on a fixture (the stage's wall, table, door: a hand whose target lies at its surface, at its full weight, and whose clip does not
        // close its fingers itself): its fingers belong on that surface too — a palm on a door with the fingers standing off it, or poking at it, is the
        // same mistake as a fist, a size smaller (the user's «гарно лягати», 2026-11-11, for every surface a hand is on, not the book alone)
        private readonly int[] _touchFrames = new int[2], _touchBad = new int[2], _touchLeast = { 9, 9 };
        private readonly float[] _touchDigAt = new float[2]; private readonly string[] _touchDigWhy = new string[2], _touchDigOn = new string[2];
        private bool OnFixture(int side, Fixture f, BodyAnimator an, List<string> off, out int lie, out int fingers)
        {
            lie = 0; fingers = 0;
            Transform root = _h.transform, hand = _b[side == 0 ? HandL : HandR];
            Vector3 along, palm;
            if (an == null || hand == null || !an.HandFacing(side == 0, hand, out along, out palm) || palm.sqrMagnitude < 1e-8f) return false;
            Vector3 face = hand.position + along * 0.45f + palm.normalized * 0.015f;
            bool thumb; int shut;
            _digPalm[side] = f.Distance(root.InverseTransformPoint(face));
            Digits(side, face, p => f.Distance(root.InverseTransformPoint(p)), p => root.TransformDirection(f.Normal(root.InverseTransformPoint(p))), off, out lie, out fingers, out thumb, out shut);
            return true;
        }
        private void TouchDigits(int side, Fixture f, BodyAnimator an, float t)
        {
            List<string> off = new List<string>(); int lie, fingers;
            if (!OnFixture(side, f, an, off, out lie, out fingers) || fingers == 0) return;
            _touchFrames[side]++;
            if (lie >= Mathf.Min(GripLie, fingers)) return;
            _touchBad[side]++;
            if (lie < _touchLeast[side]) { _touchLeast[side] = lie; _touchDigAt[side] = t; _touchDigOn[side] = f.Name; _touchDigWhy[side] = lie + " of " + fingers + " fingers lying on it: " + string.Join(", ", off.ToArray()); }
        }

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
            int side = left ? 0 : 1, lie, shut, fingers;
            bool thumb;
            _digPalm[side] = palmGap;
            List<string> off = new List<string>();
            Digits(side, face, view.Distance, view.Normal, off, out lie, out fingers, out thumb, out shut);
            bool under = Vector3.Dot(palm, Vector3.up) > GripUnder && palmGap <= GripPalm; // (the thing rests on the palm: no thumb needed to hold it)
            List<string> no = new List<string>();
            if (palmGap > GripPalm) no.Add("palm " + N(palmGap) + " m off it");
            if (!turned) no.Add("palm turned away from it");
            if (lie < Mathf.Min(GripLie, fingers)) no.Add("fingers lying on it " + lie + " of " + fingers + " (" + string.Join(", ", off.ToArray()) + ")");
            if (!thumb && !under && _digHas[side, 0]) no.Add("the thumb " + (_digTip[side, 0] < -FingerDeep || _digMid[side, 0] < -FingerDeep ? "inside it" : N(_digTip[side, 0]) + " m off it"));
            if (shut >= 3) no.Add("shut to a fist before it (" + shut + " fingers curled to the palm)");
            if (no.Count == 0) return true;
            why = string.Join(", ", no.ToArray());
            bad = Mathf.Max(0f, palmGap - GripPalm) + (turned ? 0f : 0.1f) + Mathf.Max(0, Mathf.Min(GripLie, fingers) - lie) * 0.03f + (thumb || under ? 0f : 0.03f) + (shut >= 3 ? 0.05f : 0f);
            return true;
        }

        // ---------------------------------------------------------------- the grip sheet (the head of the grip above)
        // The hand's own bones while it is on the thing, a look every GripSheetEvery s: each digit's three joints (place and turn) and its tip in the
        // HAND's frame (from the wrist bone; z the fingers' way — wrist to the middle knuckle —, y the palm's way made square to it, x = y × z: the frame
        // the core turns by «.on.f» and «.on.n», BodyAnimator.HandFacing), the keys its clip had on those bones then, and in the THING's frame (as «.on»
        // is said: BodyAnimator.PropAt) the wrist, the fingers' and the palm's way, the thing's box. A finger key turns its bone over the pose under it
        // (BodyClips.Turn), so the same key reads otherwise on the left hand and under another stance: the sheet is the measure of that.
        private sealed class GripShot
        {
            public float T, PalmGap, BoxOff, ClipAt; public string Clip; public Vector3 Wrist, Asked, F, N, BoxAt; // (BoxOff, BoxAt: deg the thing's look stood off the turn «.on» is said in, then, and its box's middle in that frame — a look moved aside with no turn reads «off 0»)
            public readonly Vector3[] P = new Vector3[15], Key = new Vector3[15], Tip = new Vector3[5]; public readonly Quaternion[] Q = new Quaternion[15]; public readonly float[] KeyW = new float[15]; public readonly bool[] Has = new bool[5];
        }
        private readonly List<GripShot>[] _gripShots = { new List<GripShot>(), new List<GripShot>() };
        private const int GripShotsMost = 120;
        private void GripLook(int side, BodyAnimator an, StoryPropView view, float t)
        {
            List<GripShot> list = _gripShots[side];
            if (list.Count >= GripShotsMost || (list.Count > 0 && t >= list[list.Count - 1].T && t - list[list.Count - 1].T < GripSheetEvery)) return;
            bool left = side == 0;
            Transform hand = _b[left ? HandL : HandR];
            Vector3 along, palm, pp; Quaternion pr;
            if (hand == null || !an.HandFacing(left, hand, out along, out palm) || along.sqrMagnitude < 1e-8f || palm.sqrMagnitude < 1e-8f) return;
            an.PropAt(out pp, out pr);
            Quaternion hq = Quaternion.Inverse(Quaternion.LookRotation(along, palm)), tq = Quaternion.Inverse(pr);
            GripShot g = new GripShot { T = t, Clip = _clipNow, ClipAt = _momentNow, PalmGap = _digPalm[side], Wrist = tq * (hand.position - pp), F = tq * along.normalized, N = tq * palm.normalized };
            float ig; an.ChannelNow((left ? "@LeftHand" : "@RightHand") + ".on", out g.Asked, out ig);
            Vector3 bc, bs; Quaternion bq; bool round;
            if (view.Box(out bc, out bq, out bs, out round)) { g.BoxOff = Quaternion.Angle(pr, bq); g.BoxAt = tq * (bc - pp); }
            for (int k = 0; k < 5; k++)
            {
                Vector3 tip;
                Transform[] j = { _knuckle[side, k], _fing[side, k, 0], _fing[side, k, 1] };
                g.Has[k] = j[0] != null && j[1] != null && j[2] != null && Tip(side, k, out tip);
                if (!g.Has[k]) continue;
                Tip(side, k, out tip);
                g.Tip[k] = hq * (tip - hand.position);
                for (int i = 0; i < 3; i++)
                {
                    g.P[k * 3 + i] = hq * (j[i].position - hand.position); g.Q[k * 3 + i] = hq * j[i].rotation;
                    if (!an.ChannelNow((left ? "Left" : "Right") + "Hand" + Fingers[k] + (i + 1), out g.Key[k * 3 + i], out g.KeyW[k * 3 + i])) { g.Key[k * 3 + i] = Vector3.zero; g.KeyW[k * 3 + i] = -1f; } // (−1: the clip does not key that bone)
                }
            }
            list.Add(g);
        }

        private static string V4(Vector3 v) { return v.x.ToString("0.####", CultureInfo.InvariantCulture) + " " + v.y.ToString("0.####", CultureInfo.InvariantCulture) + " " + v.z.ToString("0.####", CultureInfo.InvariantCulture); }
        // the sheet, for the full report ("" when no hand was on a thing): each hand at the look nearest the middle of its time on the thing
        internal string GripSheet(StoryPropView view, BodyAnimator an)
        {
            StringBuilder s = new StringBuilder();
            if (view != null && an != null) // (the thing itself, whether a hand was on it or not: a movement of the script lays a hand on it by these — tools/grip.js forScript)
            {
                Vector3 pp, bc, bs; Quaternion pr, bq; bool round;
                an.PropAt(out pp, out pr);
                if (view.Box(out bc, out bq, out bs, out round))
                {
                    Quaternion tq = Quaternion.Inverse(pr);
                    s.Append("grip sheet (node tools/anim.js grip <clip> reckons from it; m. The hand's frame: from the wrist bone, z its fingers' way — wrist to the middle knuckle —, y its palm's way, x = y × z; turns are quaternions x y z w in that frame. The thing's frame: as «.on» is said):\n");
                    s.Append("grip thing: centre ").Append(V4(tq * (bc - pp))).Append(" size ").Append(V4(bs)).Append(round ? " round" : " box").Append(" off ").Append(N(Quaternion.Angle(pr, bq))).Append(" deg\n");
                    List<Vector3> pc = new List<Vector3>(), ps = new List<Vector3>();
                    view.Parts(pc, ps);
                    if (pc.Count > 1) for (int i = 0; i < pc.Count; i++) s.Append("grip part: centre ").Append(V4(tq * (pc[i] - pp))).Append(" size ").Append(V4(ps[i])).Append('\n');
                }
            }
            // (a scene's body: by its report the thing is put back and its look gone — the hands' looks are said all the same, each with how far the
            //  thing stood off the turn «.on» is said in, and how far the wrist was off its asked point: what a scene's grip differs by from the stage's)
            bool headed = s.Length > 0;
            for (int side = 0; side < 2; side++)
            {
                List<GripShot> list = _gripShots[side];
                if (list.Count == 0) continue;
                float mid = (list[0].T + list[list.Count - 1].T) * 0.5f;
                GripShot g = list[0];
                foreach (GripShot x in list) if (Mathf.Abs(x.T - mid) < Mathf.Abs(g.T - mid)) g = x;
                if (!headed) { s.Append("grip sheet (the thing's look was gone by the report: the hands as they were on it; m, the hand's frame as on the stage):\n"); headed = true; }
                string name = side == 0 ? "left" : "right";
                s.Append("grip ").Append(name).Append(" at ").Append(N(g.T)).Append(" s (on the thing ").Append(N(list[0].T)).Append("–").Append(N(list[list.Count - 1].T)).Append(" s, the thing then ").Append(N(g.BoxOff)).Append(" deg off its said turn, its middle ").Append(V4(g.BoxAt)).Append(" off its said place").Append(g.Clip != null ? ", the clip " + g.Clip + " at " + N(g.ClipAt) + " s" : "").Append("): wrist ").Append(V4(g.Wrist)).Append(" asked ").Append(V4(g.Asked)).Append(" f ").Append(V4(g.F)).Append(" n ").Append(V4(g.N)).Append(" palm ").Append(M(g.PalmGap)).Append(" tips ").Append(_fingEnd[side, 2] != null ? "bones" : "reckoned").Append('\n');
                for (int k = 0; k < 5; k++)
                {
                    if (!g.Has[k]) continue;
                    s.Append("grip ").Append(name).Append(' ').Append(Fingers[k]).Append(':');
                    for (int i = 0; i < 3; i++)
                    {
                        Quaternion q = g.Q[k * 3 + i];
                        s.Append(" | p ").Append(V4(g.P[k * 3 + i])).Append(" q ").Append(q.x.ToString("0.#####", CultureInfo.InvariantCulture)).Append(' ').Append(q.y.ToString("0.#####", CultureInfo.InvariantCulture)).Append(' ').Append(q.z.ToString("0.#####", CultureInfo.InvariantCulture)).Append(' ').Append(q.w.ToString("0.#####", CultureInfo.InvariantCulture));
                        s.Append(" key ").Append(g.KeyW[k * 3 + i] < 0f ? "-" : V4(g.Key[k * 3 + i]) + " w " + N(g.KeyW[k * 3 + i]));
                    }
                    s.Append(" | tip ").Append(V4(g.Tip[k])).Append(_fingEnd[side, k] != null ? " bones" : " reckoned").Append('\n');
                }
            }
            return s.ToString();
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
            Keep((left ? "LeftHand" : "RightHand") + "@real surface", gap, t, tgt, left ? "@LeftHand" : "@RightHand", null);
            _realGap[left ? 0 : 1] = gap;
        }

        private void Keep(string key, float gap, float t, Vector3 want, string limb, Fixture on)
        {
            Touch k;
            if (!_touch.TryGetValue(key, out k)) _touch[key] = k = new Touch { Limb = limb };
            Vector3 near = on != null ? (on.Distance(want) > 0f ? on.Closest(want) : on.Center) : want; // (inside the box its nearest point is itself: then out from its middle; a scene's real surface keeps no point: no fix)
            if (gap > k.Far) { k.Far = gap; k.FarAt = t; k.FarWant = want; k.FarNear = near; k.FarClip = _clipNow; k.FarClipAt = _momentNow; }
            if (gap < k.In) { k.In = gap; k.InAt = t; k.InWant = want; k.InNear = near; k.InClip = _clipNow; k.InClipAt = _momentNow; }
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

        // how far a palm is off the trunk's surface (− inside), the column «inside» measures by; 9: no bones
        private float PalmToBody(bool left)
        {
            int hand = left ? HandL : HandR, mid = left ? M1L : M1R;
            if (_b[hand] == null || _b[Hips] == null || _b[Chest] == null || _b[Neck] == null) return 9f;
            Vector3 p = _b[mid] != null ? (_b[hand].position + _b[mid].position) * 0.5f : _b[hand].position; // (the palm's middle, not the wrist)
            float width = _b[ArmL] != null && _b[ArmR] != null ? Vector3.Distance(_b[ArmL].position, _b[ArmR].position) : 0.36f;
            float d = Mathf.Min(ToSegment(p, _b[Hips].position, _b[Chest].position), ToSegment(p, _b[Chest].position, _b[Neck].position));
            return d - 0.32f * width;
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
                string hand = "@" + Side[s] + "Hand";
                if (_palm[s] > PalmOff) Say(bad, w + "[palm] " + side[s] + " palm faces " + N(_palm[s]) + " deg off what " + hand + ".n says (or off the surface it is held on) at " + N(_palmAt[s]) + " s: the core turns the hand itself, so this is its weight short or a fixture's surface — see the wrist line for what the arm can do", _palmAt[s]);
                if (WristTwistTo > WristTwistFrom && _wristTwistLow[s] <= _wristTwist[s])
                {
                    if (_wristTwist[s] > WristTwistTo) Say(bad, w + "[twist] " + side[s] + " wrist twisted " + N(_wristTwist[s]) + " deg round the forearm at " + N(_wristTwistAt[s]) + " s, past " + N(WristTwistTo) + ": no wrist turns so — turn " + hand + ".n / .on.n back, or move the hand", _wristTwistAt[s]);
                    if (_wristTwistLow[s] < WristTwistFrom) Say(bad, w + "[twist] " + side[s] + " wrist twisted " + N(_wristTwistLow[s]) + " deg round the forearm at " + N(_wristTwistLowAt[s]) + " s, past " + N(WristTwistFrom) + ": no wrist turns so — turn " + hand + ".n / .on.n back, or move the hand", _wristTwistLowAt[s]);
                }
                if (_wristBend[s] > WristBend) Say(bad, w + "[bend] " + side[s] + " wrist bent " + N(_wristBend[s]) + " deg off the forearm at " + N(_wristBendAt[s]) + " s: no wrist bends so" + BendOver(s) + " — " + BendFix(s, hand), _wristBendAt[s]);
                if (_curl[s] > FistCurl) Say(bad, w + "[fist] " + side[s] + " hand closed to a fist (" + N(_curl[s]) + " deg) at " + N(_curlAt[s]) + " s while its palm is set: open the fingers (" + Side[s] + "HandIndex1/2, Middle1/2, Ring1/2, Pinky1/2 about −25)" + FistFix(s, Side[s]), _curlAt[s]);
                if (_past[s] > Past) Say(bad, w + "[past] " + side[s] + " hand sticks out " + N(_past[s]) + " m past the other elbow at " + N(_pastAt[s]) + " s: bring its x " + N(_past[s] + 0.03f) + " m nearer the middle (onto the other arm)", _pastAt[s]);
                if (_elbow[s] > ElbowBack) Say(bad, w + "[elbow] " + side[s] + " elbow " + N(_elbow[s]) + " m behind the chest at " + N(_elbowAt[s]) + " s: turn its pole (" + hand + ".p) out and forward", _elbowAt[s]);
            }
            foreach (KeyValuePair<string, Worst> m in _miss) if (m.Value.V > MissFar) Say(bad, w + "[miss] " + m.Key + " misses by " + N(m.Value.V) + " m at " + N(m.Value.At) + " s with its weight on: out of reach" + MissFix(m.Key, m.Value), m.Value.At);
            foreach (KeyValuePair<string, Worst> m in _moved)
            {
                if (m.Value.V <= MovedFar) continue;
                int over, all; _movedFrames.TryGetValue(m.Key, out over); _solvedFrames.TryGetValue(m.Key, out all);
                Say(bad, w + "[moved] " + m.Key + " stands " + M(m.Value.V) + " m off where the pose put it at " + N(m.Value.At) + " s (by " + V4(m.Value.Tgt) + " in the body's frame: x right, y up, z ahead; over the limit in " + over + " of the " + all + " frames it was solved): something moves that limb AFTER our pose in the same frame — the game's own step on this body, or a later layer. No key of the clip mends it and every number of that limb here is of the moved one: it is the core's to find (BodyAnimator against what runs after it), say it before judging the clip", m.Value.At);
            }
            foreach (KeyValuePair<string, Worst> m in _fast)
            {
                bool foot = m.Key.EndsWith("Foot", System.StringComparison.Ordinal);
                float limit = foot ? FootFast : HandFast;
                if (m.Value.V > limit) Say(bad, w + "[fast] " + m.Key + " flies at " + N(m.Value.V) + " m/s at " + N(m.Value.At) + " s: too sudden" + Slower(BodyClips.Find(m.Value.Clip), "@" + m.Key, m.Value.ClipAt, limit, m.Value.V), m.Value.At);
            }
            foreach (KeyValuePair<int, Worst> m in _spin) if (m.Value.V > BoneJerk) Say(bad, w + "[jerk] " + Names[m.Key] + " at " + m.Value.V.ToString("0", CultureInfo.InvariantCulture) + " deg/s at " + N(m.Value.At) + " s: a jerk" + JerkFix(m.Key, m.Value), m.Value.At);
            if (Mathf.Max(_slid[0], _slid[1]) > FootSlide) Say(bad, w + "[slide] a foot slid along the ground (L " + N(_slid[0]) + " m, R " + N(_slid[1]) + " m in the whole run; most: the " + _slideFoot + " at " + N(_slideAt) + " s): on ice — lift it before it moves, and let its key before it lands stand over the spot it lands on", _slideAt);
            if (_kneeCross > KneeCross) Say(bad, w + "[knee] a knee crossed the body's middle by " + N(_kneeCross) + " m at " + N(_kneeCrossAt) + " s: set @LeftFoot.p / @RightFoot.p", _kneeCrossAt);
            if (_handBehind > HandBehindFar) Say(bad, w + "[free] a free hand swings " + N(_handBehind) + " m behind the body while bent at " + N(_handBehindAt) + " s: give it a target (the knee)", _handBehindAt);
            if (_headProp < HeadNear) Say(bad, w + "[prop-head] the head comes within " + N(_headProp) + " m of the prop at " + N(_headPropAt) + " s: hold it farther", _headPropAt);
            for (int s = 0; s < 2; s++) if (Mathf.Abs(_sole[s]) > SoleTilt) Say(bad, w + "[sole] the " + (s == 0 ? "left" : "right") + " foot stands " + (_sole[s] > 0f ? "on its heel, the toe " + N(_sole[s]) + " m up in the air" : "up on its toes, the heel " + N(-_sole[s]) + " m up") + " at " + N(_soleAt[s]) + " s: the sole flat on the ground (the foot's turn, or its target lower)", _soleAt[s]);
            if (_lookOff > LookMiss) Say(bad, w + "[look] the face stays " + N(_lookOff) + " deg off its @Look point at " + N(_lookOffAt) + " s: past what the head turns (BodyClips.LookMost) — turn the body toward it, or bring the point nearer the face's way", _lookOffAt);
            if (_limbDeep > 0f) Say(bad, w + "[prop-limb] the " + _limbDeepOf + " goes " + N(_limbDeep) + " m into the prop at " + N(_limbDeepAt) + " s: hold it off the body", _limbDeepAt);
            for (int s = 0; s < 2; s++)
                if (_touchFrames[s] > 0 && _touchBad[s] > _touchFrames[s] * GripShare)
                    Say(bad, w + "[touch-fingers] " + side[s] + " hand is held on the " + _touchDigOn[s] + " with its fingers not on it in " + _touchBad[s] + " of " + _touchFrames[s] + " frames (worst at " + N(_touchDigAt[s]) + " s: " + _touchDigWhy[s] + "): a palm on a surface lays its fingers on it — their keys (" + Side[s] + "HandIndex1/2 … Pinky1/2; node tools/anim.js grip presets says the keys that straighten this hand's fingers)", _touchDigAt[s]);
            if (_through > 0f) Say(bad, w + "[prop-fingers] fingers inside the prop in " + _through.ToString("0", CultureInfo.InvariantCulture) + " frames (last at " + N(_throughAt) + " s): move the wrist out past its edge", _throughAt);
            for (int s = 0; s < 2; s++)
                if (_gripFrames[s] > 0 && _gripBad[s] > _gripFrames[s] * GripShare)
                    Say(bad, w + "[grip] " + side[s] + " hand does not hold the prop in " + _gripBad[s] + " of " + _gripFrames[s] + " frames it is on it (worst at " + N(_gripAt[s]) + " s: " + _gripWhy[s] + "): its palm on the thing, turned to it (@" + Side[s] + "Hand.on, .on.n), the fingers lying on it (" + Side[s] + "HandIndex1/2 … Pinky1/2), the thumb on it, not a fist — node tools/anim.js grip " + (_gripClip[s] ?? "<clip>").Split('.')[0] + " reckons the wrist's place and each finger's bend from this take's sheet: do not turn them by eye", _gripAt[s]);
            if (_shoulder > ShoulderBent) Say(bad, w + "[shoulder] " + _shoulderSide + " shoulder bent " + _shoulder.ToString("0", CultureInfo.InvariantCulture) + " deg off its stance at " + N(_shoulderAt) + " s: a reach out of the arm's length pulls it", _shoulderAt);
            if (_fold < ElbowFold) Say(bad, w + "[fold] " + _foldSide + " elbow folded to " + _fold.ToString("0", CultureInfo.InvariantCulture) + " deg at " + N(_foldAt) + " s", _foldAt);
            if (_neck > NeckMost) Say(bad, w + "[neck] head turned " + _neck.ToString("0", CultureInfo.InvariantCulture) + " deg off the chest at " + N(_neckAt) + " s", _neckAt);
            foreach (KeyValuePair<string, Touch> k in _touch)
            {
                string limb = k.Key.Substring(0, k.Key.IndexOf('@')), on = k.Key.Substring(k.Key.IndexOf('@') + 1);
                if (k.Value.Far >= RealFar - 0.01f && on == "real surface") Say(bad, w + "[touch] " + limb + " has NOTHING to touch at " + N(k.Value.FarAt) + " s: no surface within " + N(RealFar - RealBack) + " m along its palm — the thing is not there in this world (a door open, a piece gone or not built): not the clip; the scene's «test:» line sets the world it is checked in (gate_shut 2026-11-10: the hall's gate stood open)", k.Value.FarAt);
                else if (k.Value.Far > TouchFar) Say(bad, w + "[touch] " + limb + " " + N(k.Value.Far) + " m off the " + on + " at " + N(k.Value.FarAt) + " s: its target " + V(k.Value.FarWant) + " — bring it " + N(k.Value.Far) + " m onto the surface" + TouchFix(k.Value, true), k.Value.FarAt);
                if (k.Value.In < -TouchIn) Say(bad, w + "[touch] " + limb + " " + N(-k.Value.In) + " m into the " + on + " at " + N(k.Value.InAt) + " s: its target " + V(k.Value.InWant) + " — move it " + N(-k.Value.In) + " m out" + TouchFix(k.Value, false), k.Value.InAt);
            }
            if (_seat.Length > 0 && _hipsIn < -TouchIn) Say(bad, w + "[seat] the hips " + N(-_hipsIn) + " m into the " + _seat + " at " + N(_hipsInAt) + " s: lower the body less («move» y)" + MoveFix(_hipsInClip, _hipsInClipAt, -_hipsIn), _hipsInAt);
            if (_seat.Length > 0 && _hipsGap > SeatHover) Say(bad, w + "[seat] the hips hover " + N(_hipsGap) + " m over the " + _seat + " at their lowest: lower the body that much more («move» y)" + MoveFix(_hipsGapClip, _hipsGapClipAt, -_hipsGap), -1f);
            foreach (KeyValuePair<string, float> p in _point) if (p.Value > PointNear) Say(bad, w + "[point] no hand comes nearer than " + N(p.Value) + " m to the " + p.Key, -1f);
            if (_inside > InsideDeep) Say(bad, w + "[inside] " + _insideWhat + " inside the body (" + N(_inside * 100f) + "% of the way to the spine) at " + N(_insideAt) + " s: move it out in front of the chest", _insideAt);
            if (_still > StillFor) Say(bad, w + "[still] nothing of the body moves for " + N(_still) + " s from " + N(_stillAt) + " s while it is in the picture of a shot: an actor stood like a post — a clip, a look, a turn of the head there, or a shot that is not of it", _stillAt);
            if (_startSaid != null) Say(bad, w + "[start] " + _startSaid, _startAt);
            if (_inHead > HeadDeep) Say(bad, w + "[head] " + _inHeadWhat + " inside the head (" + N(_inHead * 100f) + "% of the way to its middle) at " + N(_inHeadAt) + " s: move it out onto the head's surface (in front of the brow, beside the temple)", _inHeadAt);
            if (_knee < -KneeUnder) Say(bad, w + "[knee-under] a knee " + N(-_knee) + " m under the ground at " + N(_kneeAt) + " s: the body goes lower than the leg folds — raise «move» y there" + MoveFix(_kneeClip, _kneeClipAt, -_knee + KneeUnder), _kneeAt);
        }

        // ---------------------------------------------------------------- the ready command (the head of this file)
        internal static string FixLine(string text) { return "\n      fix: node tools/anim.js " + text; }

        // the clip's file and the moment in it: a part plays as a clip of its own, its file is the whole clip's (false: no such clip, no command)
        internal static bool WholeOf(ref string clip, ref float at)
        {
            if (string.IsNullOrEmpty(clip)) return false;
            int dot = clip.IndexOf('.');
            if (dot > 0)
            {
                PAnimClip whole = BodyClips.Find(clip.Substring(0, dot));
                string part = clip.Substring(dot + 1);
                if (whole != null) foreach (PAnimPart p in whole.Parts) if (p.Name == part) { at += p.From; break; }
                clip = clip.Substring(0, dot);
            }
            return BodyClips.Find(clip) != null;
        }

        // a limb's target the clip has riding on a bone («follow»): its keys are in that bone's frame, not the body's — a fix or a mark reckoned in
        // the body's frame would lie (guard_atgeir_carry); and a clip that has no such channel at all (a scene: the limb driven by a lower layer)
        private static bool Fixable(string clip, string channel)
        {
            if (string.IsNullOrEmpty(clip)) return false;
            PAnimClip c = BodyClips.Find(clip.Split('.')[0]);
            string limb = channel.Length > 0 && channel[0] == '@' ? (channel.IndexOf('.') > 0 ? channel.Substring(0, channel.IndexOf('.')) : channel) : channel;
            return c != null && c.Bones.Contains(channel) && !c.Follow.ContainsKey(limb);
        }

        // the key of that channel nearest the moment (none: the moment itself, to the hundredth)
        internal static float KeyNear(string clip, string channel, float at)
        {
            PAnimClip c = BodyClips.Find(clip);
            float best = Mathf.Round(at * 100f) / 100f, gap = 9f;
            if (c != null) foreach (PAnimKey k in c.Keys) if (k.Has(channel) && Mathf.Abs(k.T - at) < gap) { gap = Mathf.Abs(k.T - at); best = k.T; }
            return best;
        }

        // the clip's «want» that puts this limb at a point near that moment (its words; null: none): a fix leaves such a target where it is
        private static string WantPins(string clip, string limb, float at)
        {
            PAnimClip c = BodyClips.Find(clip);
            if (c == null || !limb.EndsWith("Hand", StringComparison.Ordinal)) return null;
            string side = limb.IndexOf("Left", StringComparison.Ordinal) >= 0 ? "left" : "right";
            foreach (string line in c.Wants)
            {
                string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (w.Length < 4 || w[1] != "hand" || (w[2] != side && w[2] != "both") || (w[3] != "near" && w[3] != "touches")) continue;
                int dash = w[0].IndexOf('-');
                float a, b;
                if (!float.TryParse(dash > 0 ? w[0].Substring(0, dash) : w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) continue;
                if (!(dash > 0 && float.TryParse(w[0].Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b))) b = a;
                if (at >= a - 0.5f && at <= b + 0.5f) return line; // (half a second round it: the hand held there a while)
            }
            return null;
        }

        // out of reach: the target that far from the limb's root, the limb this long — the target brought within it, the same way; a target the
        // user's own «want» puts there stays, and the body is brought to it instead (WantPins)
        private string MissFix(string limb, Worst m)
        {
            if (m.Reach <= 0f) return "";
            float d = Vector3.Distance(m.Tgt, m.Joint), over = d - m.Reach;
            string root = limb.EndsWith("Hand", System.StringComparison.Ordinal) ? "shoulder" : "hip";
            string s = " (the " + root + " at " + V(m.Joint) + ", the limb reaches " + N(m.Reach) + " m, the target " + V(m.Tgt) + " is " + N(d) + " m from it)";
            if (over <= 0f) return s + ": not its length — the pole, the solve: look at the frame";
            if (m.OnThing) return s + ": the hand is on the thing — bring @Prop " + N(over + 0.03f) + " m nearer the body, or the body to it";
            string clip = m.Clip; float at = m.ClipAt;
            if (!WholeOf(ref clip, ref at) || !Fixable(clip, limb)) return s + (clip != null && BodyClips.Find(clip) != null && BodyClips.Find(clip).Follow.ContainsKey(limb) ? " — the target rides on a bone («follow»): reckon it in that bone's frame" : "");
            string pin = WantPins(clip, limb, at);
            if (pin != null) // (the user's own words put the hand there: the body goes to it, the target stays — basket_set 2026-11-10, a fix lifted the hand to the waist)
                return s + ": the target is what «want " + pin + "» asks, so it stays — the body comes " + N(over + 0.03f) + " m nearer: lower it («move» y), or lean more (Spine1/Spine2), or both"
                    + (over + 0.03f <= MoveMost ? MoveFix(clip, at, -(over + 0.03f)) : " — too far for one key (lowered more than " + N(MoveMost) + " m the knees go into the ground): a new shape of the pose, a crouch and a lean from the hips; look at the frame, no command");
            Vector3 near = m.Joint + (m.Tgt - m.Joint).normalized * (m.Reach - 0.03f);
            return s + ": the target " + N(over + 0.03f) + " m nearer the " + root + " — or «move» y " + N(-(over + 0.03f)) + " there, or the body leant so the " + root + " comes that much nearer" + FixLine("set " + clip + " " + limb + " " + N(KeyNear(clip, limb, at)) + " " + V(near));
        }

        // how long a wrist is over its limit, in the run's time: its stretches (frames BendGap apart are one), said when they make more than a
        // moment — «the pose it holds», not a point to mend
        private string BendOver(int s)
        {
            List<BendAt> o = _wristOver[s];
            if (o.Count == 0) return "";
            StringBuilder b = new StringBuilder(); float from = o[0].T, to = o[0].T, sum = 0f; int n = 0;
            for (int i = 1; i <= o.Count; i++)
            {
                if (i < o.Count && o[i].T - to <= BendGap) { to = o[i].T; continue; }
                sum += to - from;
                if (n++ > 0) b.Append(", ");
                b.Append(N(from)).Append("–").Append(N(to));
                if (i < o.Count) from = to = o[i].T;
            }
            if (sum < BendMoment) return "";
            return ", and it is over " + N(WristBend) + " deg for " + N(sum) + " s in all (" + b + " s): the pose it holds, not a moment";
        }

        // a wrist bent past what it bends: .f turned toward the forearm's way by what is over (and a little), or given to a hand that has none
        private string BendFix(int s, string hand)
        {
            string clip = _wristBendClip[s]; float at = _wristBendClipAt[s];
            float turn = _wristBend[s] - WristBend + 10f;
            if (_wristOn[s]) return "turn " + hand + ".on.f about " + N(turn) + " deg more along the forearm (in the thing's frame), or move the hand";
            if (!WholeOf(ref clip, ref at) || !Fixable(clip, hand)) return "point " + hand + ".f " + N(turn) + " deg more along the forearm, or move the hand";
            if (_wristHasF[s])
            {
                // every key of «.f» the clip's file has in the stretches it is over (BendKeyPad round them), each turned by what the wrist was over
                // at the frame nearest it — one key mended left the rest of the stretch bent (the head of BendAt)
                PAnimClip c = BodyClips.Find(clip); float shift = at - _wristBendClipAt[s]; // (a part's moments are its whole clip's less where it begins: WholeOf)
                StringBuilder ops = new StringBuilder(); int keys = 0;
                if (c != null) foreach (PAnimKey k in c.Keys)
                {
                    if (!k.Has(hand + ".f")) continue;
                    float gap = BendKeyNear; int best = -1; bool inside = false;
                    for (int i = 0; i < _wristOver[s].Count; i++)
                    {
                        BendAt b = _wristOver[s][i];
                        if (b.Clip != _wristBendClip[s] || !b.HasF) continue;
                        float d = Mathf.Abs(b.ClipAt + shift - k.T);
                        if (d <= BendKeyPad) inside = true;
                        if (d < gap) { gap = d; best = i; }
                    }
                    if (!inside || best < 0) continue;
                    BendAt w = _wristOver[s][best];
                    Vector3 to = Vector3.RotateTowards(w.F, w.Fore, (w.Bend - WristBend + 10f) * Mathf.Deg2Rad, 0f).normalized;
                    if (keys++ > 0) ops.Append("; ");
                    ops.Append("set ").Append(hand).Append(".f ").Append(N(k.T)).Append(' ').Append(V(to));
                }
                if (keys > 1) return "point " + hand + ".f along the forearm at each of its " + keys + " keys in that stretch (worst: " + N(turn) + " deg more, the forearm's way " + V(_wristFore[s]) + "), or move the hand" + FixLine("batch " + clip + " \"" + ops + "\"");
                Vector3 f = Vector3.RotateTowards(_wristF[s], _wristFore[s], turn * Mathf.Deg2Rad, 0f).normalized;
                return "point " + hand + ".f " + N(turn) + " deg more along the forearm (its way " + V(_wristFore[s]) + "), or move the hand" + FixLine("set " + clip + " " + hand + ".f " + N(KeyNear(clip, hand + ".f", at)) + " " + V(f));
            }
            Vector3 n = Vector3.ProjectOnPlane(_wristPalm[s], _wristFore[s]); // (no .f: the hand keeps the game's turn on the forearm — directions: the fingers along it, the palm where it faces now)
            if (n.sqrMagnitude < 1e-4f) n = Vector3.ProjectOnPlane(Vector3.down, _wristFore[s]);
            float t = KeyNear(clip, hand, at);
            return "the hand keeps the game's turn on it: give it " + hand + ".f (the fingers along the forearm) and .n (the palm)" + FixLine("batch " + clip + " \"set " + hand + ".f " + N(t) + " " + V(_wristFore[s]) + "; set " + hand + ".n " + N(t) + " " + V(n.normalized) + "\"");
        }

        // a fist where a palm was set: the eight finger joints opened at the nearest key
        private string FistFix(int s, string Side)
        {
            string clip = _curlClip[s]; float at = _curlClipAt[s];
            if (!WholeOf(ref clip, ref at)) return "";
            StringBuilder b = new StringBuilder();
            foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky" }) for (int j = 1; j <= 2; j++) { string ch = Side + "Hand" + f + j; if (b.Length > 0) b.Append("; "); b.Append("set ").Append(ch).Append(' ').Append(N(KeyNear(clip, ch, at))).Append(" -25 0 0"); }
            return FixLine("batch " + clip + " \"" + b + "\"");
        }

        // what to change for a limb too fast: the two keys of its target round that moment, how far it goes between them, and the
        // time it needs at the limit (a quarter under it) — stretched; keys fast enough to overshoot — a key between them; keys that
        // move it far slower than it flew — its weight's way in or out, when that is short (WeightRamp); else not the keys: said so, no command
        // (a wrong command costs a take)
        private static string Slower(PAnimClip c, string target, float at, float limit, float v)
        {
            string ramp = WeightRamp(c, target, at, v / limit);
            if (ramp != null) return ramp;
            if (c == null || !c.Bones.Contains(target)) return " (not a target of the clip: the way in or out — a longer «fade», or its first key nearer the game's stance)";
            float t1 = -1f, t2 = -1f;
            foreach (PAnimKey k in c.Keys) { if (!k.Has(target)) continue; if (k.T <= at) t1 = k.T; else if (t2 < 0f) t2 = k.T; }
            if (t1 < 0f || t2 < 0f) return " (outside its keys: the way in or out)";
            Vector3 a = c.Value(target, t1), b = c.Value(target, t2);
            float d = Vector3.Distance(a, b), need = d / (limit * 0.75f), own = d / Mathf.Max(0.01f, t2 - t1);
            string clip = c.Name; float ig = 0f; bool file = !c.IsPart && WholeOf(ref clip, ref ig);
            if (need > t2 - t1) return " — its move " + N(t1) + "–" + N(t2) + " s goes " + N(d) + " m: give it " + N(need) + " s or more (now " + N(t2 - t1) + ")" + (file ? FixLine("stretch " + clip + " " + N(t1) + " " + N(t2) + " " + N(need)) : "");
            if (own >= limit * 0.4f) return " — keys " + N(t1) + "–" + N(t2) + " s are long enough: the curve overshoots between them (a key in the middle)" + (file ? FixLine("set " + clip + " " + target + " " + N((t1 + t2) * 0.5f) + " " + V((a + b) * 0.5f)) : "");
            return " — its keys " + N(t1) + "–" + N(t2) + " s move it only " + N(own) + " m/s: the flight is not from them (the way in or out of its weight, the spring, the elbow flipping over): look at the frame, no key to change";
        }

        // a limb's target weight («.w») going from held to let go (or back) round that moment faster than the limb can follow: the limb drops
        // into the game's pose (or snaps to the target) in a jerk. over: how far past its limit it went. The weight's way made that much
        // longer (a quarter under the limit): taking hold — its start sooner (the touch stays when it was); letting go — its end later (the
        // hold stays), else the other end, where there is room. null: no such way there
        internal static float WeightTurn = 0.5f; // of a weight's 0..1: a change this big is a take or a let-go, not a lean of it
        private static string WeightRamp(PAnimClip c, string target, float at, float over)
        {
            string wch = target + ".w";
            if (c == null || !c.Bones.Contains(wch)) return null;
            float tp = -1f, t1 = -1f, t2 = -1f, tn = -1f;
            foreach (PAnimKey k in c.Keys)
            {
                if (!k.Has(wch)) continue;
                if (k.T <= at) { tp = t1; t1 = k.T; } else if (t2 < 0f) t2 = k.T; else if (tn < 0f) tn = k.T;
            }
            if (t1 < 0f || t2 < 0f) return null;
            float w1 = c.Value(wch, t1).x, w2 = c.Value(wch, t2).x, span = t2 - t1;
            if (Mathf.Abs(w2 - w1) < WeightTurn) return null;
            float need = Mathf.Max(span * over / 0.75f, span + 0.15f), extra = need - span;
            bool sooner = t1 - extra >= (tp >= 0f ? tp + 0.05f : 0f), later = t2 + extra <= (tn >= 0f ? tn - 0.05f : c.Length);
            foreach (PAnimPart p in c.Parts) foreach (float e in new[] { p.From, p.To }) // (never over a part's edge: basket_set's «down» ends at 2 with the hand let go — a key moved to 2.16 would end it held)
            {
                if (e >= t1 - extra - 0.01f && e <= t1 + 0.001f) sooner = false;
                if (e >= t2 - 0.001f && e <= t2 + extra + 0.01f) later = false;
            }
            bool takes = w2 > w1, moveStart = takes ? sooner : !later && sooner;
            string what = " — its weight " + wch + " goes " + N(w1) + " → " + N(w2) + " in " + N(t1) + "–" + N(t2) + " s, and the limb " + (takes ? "snaps to the target" : "drops into the game's pose") + " that fast: give that way " + N(need) + " s";
            if (!moveStart && !later) return what + " (no room between its keys: move the keys round it)";
            string clip = c.Name; float off = 0f;
            if (!WholeOf(ref clip, ref off)) return what;
            float from = moveStart ? t1 : t2, to = moveStart ? t1 - extra : t2 + extra, val = moveStart ? w1 : w2;
            return what + (moveStart ? " (its start sooner)" : " (its end later)") + FixLine("batch " + clip + " \"del " + wch + " " + N(from + off) + "; set " + wch + " " + N(to + off) + " " + N(val) + "\"");
        }

        // the limb a bone is of, by its target's name less the «@» («RightForeArm» → RightHand, «LeftUpLeg» → LeftFoot); null: the trunk, the head
        private static string LimbOf(string bone)
        {
            string side = bone.StartsWith("Left", StringComparison.Ordinal) ? "Left" : bone.StartsWith("Right", StringComparison.Ordinal) ? "Right" : null;
            return side == null ? null : side + (bone.IndexOf("Leg", StringComparison.Ordinal) >= 0 || bone.IndexOf("Foot", StringComparison.Ordinal) >= 0 || bone.IndexOf("Toe", StringComparison.Ordinal) >= 0 ? "Foot" : "Hand");
        }
        internal static float JerkWith = 0.2f; // s round a limb's flight a bone's jerk is said to be with it (Fold holds a tail to the same)

        // a bone's jerk: with a limb's flight then (that line's fix), from its own keys (stretched), or neither (said so). The first limb that
        // flew then is named, whatever the bone (a leg's bone «with the LeftHand's flight»): Fold makes such a jerk a tail of a [fast] line
        // only of the bone's own limb. It knows these lines by their very words (WithRe; the lines' starts: SpeedRe): change them together
        private string JerkFix(int bone, Worst m)
        {
            foreach (KeyValuePair<string, Worst> f in _fast) if (f.Value.V > (f.Key.EndsWith("Foot", System.StringComparison.Ordinal) ? FootFast : HandFast) && Mathf.Abs(f.Value.At - m.At) < JerkWith) return " (with the " + f.Key + "'s flight: that line's fix)";
            PAnimClip c = BodyClips.Find(m.Clip); string name = Names[bone];
            if (c == null || !c.Bones.Contains(name))
            {
                // (a bone of an arm or a leg with no keys of its own: the limb's target letting it go or taking it fast — basket_set's RightArm, 2026-11-10)
                string limb = LimbOf(name);
                string ramp = limb != null ? WeightRamp(c, "@" + limb, m.ClipAt, m.V / BoneJerk) : null;
                if (ramp != null) return ramp;
                return " (no key of this bone in the clip and no limb flew then: the way in or out — a longer «fade» — or a target's pole flipping over)";
            }
            float t1 = -1f, t2 = -1f;
            foreach (PAnimKey k in c.Keys) { if (!k.Has(name)) continue; if (k.T <= m.ClipAt) t1 = k.T; else if (t2 < 0f) t2 = k.T; }
            if (t1 < 0f || t2 < 0f) return " (outside its keys: the way in or out)";
            float deg = (c.Value(name, t2) - c.Value(name, t1)).magnitude, need = deg / (BoneJerk * 0.75f);
            string clip = c.Name; float ig = 0f; bool file = !c.IsPart && WholeOf(ref clip, ref ig);
            if (need > t2 - t1) return " — its keys " + N(t1) + "–" + N(t2) + " s turn it " + N(deg) + " deg: give it " + N(need) + " s or more (now " + N(t2 - t1) + ")" + (file ? FixLine("stretch " + clip + " " + N(t1) + " " + N(t2) + " " + N(need)) : "");
            return " — its keys " + N(t1) + "–" + N(t2) + " s turn it only " + N(deg / Mathf.Max(0.01f, t2 - t1)) + " deg/s: the jerk is not from them (a target's solve, the way in or out): look at the frame";
        }

        // a hand or a foot off a fixture's surface or into it: its target moved by the gap, toward the surface's nearest point (out from the box's middle when inside)
        private string TouchFix(Touch k, bool far)
        {
            string clip = far ? k.FarClip : k.InClip; float at = far ? k.FarClipAt : k.InClipAt;
            if (k.Limb.Length == 0 || !WholeOf(ref clip, ref at)) return "";
            Vector3 want = far ? k.FarWant : k.InWant, way = (far ? k.FarNear : k.InNear) - want;
            if (way.sqrMagnitude < 1e-6f || !Fixable(clip, k.Limb)) return "";
            way.Normalize();
            Vector3 to = far ? want + way * k.Far : want - way * (-k.In + 0.01f);
            return FixLine("set " + clip + " " + k.Limb + " " + N(KeyNear(clip, k.Limb, at)) + " " + V(to));
        }

        // «move» y that much at the clip's nearest «move» key (none: a new channel, said)
        private static string MoveFix(string clip, float at, float dy)
        {
            if (!WholeOf(ref clip, ref at)) return "";
            PAnimClip c = BodyClips.Find(clip);
            float t = KeyNear(clip, BodyClips.Move, at);
            Vector3 v = (c != null && c.Bones.Contains(BodyClips.Move) ? c.Value(BodyClips.Move, t) : Vector3.zero) + Vector3.up * dy;
            return FixLine("set " + clip + " move " + N(t) + " " + V(v)) + (c != null && c.Bones.Contains(BodyClips.Move) ? "" : " (a new «move» channel: give it 0 0 0 at the start and the end too, or the whole clip shifts)");
        }

        // a VERDICT line's rule tag («[miss]» → miss): the key sheet picks its views by it (AnimStage.KeyViews)
        internal static string TagOf(string line) { int a = line.IndexOf('['), b = a >= 0 ? line.IndexOf(']', a) : -1; return a >= 0 && b > a ? line.Substring(a + 1, b - a - 1) : null; }

        // a line's tag and what it is about, for a row that points at it and does not say it again (the key sheet's rows): up to its first
        // «: » — a want: its own words, they tell it from the others — and SubjectMost characters at most
        internal static int SubjectMost = 60;
        internal static string Subject(string line)
        {
            const string want = "[want] not what was asked: want ";
            int nl = line.IndexOf('\n');
            if (nl >= 0) line = line.Substring(0, nl);
            if (line.StartsWith(want, StringComparison.Ordinal)) { line = "[want] " + line.Substring(want.Length); int p = line.IndexOf(" (", StringComparison.Ordinal); if (p > 0) line = line.Substring(0, p); }
            else { int c = line.IndexOf(": ", StringComparison.Ordinal); if (c > 0) line = line.Substring(0, c); }
            return line.Length > SubjectMost ? line.Substring(0, SubjectMost) + "…" : line;
        }

        // ---------------------------------------------------------------- one line an event (the head of this file)
        // bad: the lines as Verdict and WantsSaid made them, the stage's and a scene's own words among them; when: each line's moment (Moments,
        // of every body measured). What it reads are the words made above («[fast] X flies at … m/s at … s», «[jerk] X at … deg/s at … s»,
        // JerkFix's «(with the X's flight: that line's fix)»): change those and SpeedRe / WithRe together. A line of no rule («DO NOT TRUST»,
        // a scene's own) and a line with no moment stay as and where they are. edges: moments no event goes across (a scene: where each
        // step begins and ends — its report shows a line under the step of its first moment, so a line of the next step folded into it
        // would be gone from under its own); the stage gives none.
        internal static float FoldNear = 0.1f, FoldJerk = 0.3f; // s: two lines of one rule with one explanation this near are one event (a left and a right limb) · the same for «[jerk]» (a jerk runs down a limb bone after bone)
        private sealed class Said
        {
            public string Raw, Who = "", Tag, Head, Why, Fix; public float At = -1f; public int Order; public bool Gone; // (Tag null: no rule's line · Head: up to its first «: » · Why: after it, null none · Fix: its «\n      fix: …» lines)
            public List<Said> With, Jerks; // (folded into this one: lines of its rule and explanation · the bones that jerked with its flight)
        }
        private static readonly Regex SaidRe = new Regex(@"^((?:[^\[\]:]+: )?)\[([\w-]+)\] (.*)$", RegexOptions.Compiled | RegexOptions.Singleline); // («who: [tag] the rest», its fix lines and all)
        private static readonly Regex SpeedRe = new Regex(@"^(\S+) (?:flies )?at ([\d.]+) (m/s|deg/s) at [\d.]+ s$", RegexOptions.Compiled); // (the start of a [fast] or a [jerk] line: what, how fast)
        private static readonly Regex WithRe = new Regex(@"^a jerk \(with the \w+'s flight: that line's fix\)$", RegexOptions.Compiled);
        private static int ByTime(Said p, Said q) { return p.At != q.At ? p.At.CompareTo(q.At) : p.Order.CompareTo(q.Order); }
        // two moments with an edge between them (the later one at the edge is past it: a step has the lines from its beginning on). Held as
        // the lines say them, to the hundredth: a scene puts a line under its step by the moment in its words
        private static bool Across(List<float> edges, float a, float b)
        {
            if (edges == null) return false;
            float lo = AsSaid(Mathf.Min(a, b)), hi = AsSaid(Mathf.Max(a, b));
            foreach (float e in edges) if (lo < e && e <= hi) return true;
            return false;
        }
        private static float AsSaid(float t) { float v; return float.TryParse(N(t), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : t; }

        internal static List<string> Fold(List<string> bad, List<KeyValuePair<float, string>> when, List<float> edges = null)
        {
            Dictionary<string, float> at = new Dictionary<string, float>();
            if (when != null) foreach (KeyValuePair<float, string> m in when) if (m.Key >= 0f && !at.ContainsKey(m.Value)) at[m.Value] = m.Key;
            List<Said> all = new List<Said>();
            foreach (string line in bad)
            {
                Said x = new Said { Raw = line, Order = all.Count };
                Match m = SaidRe.Match(line);
                if (m.Success)
                {
                    string body = m.Groups[3].Value;
                    int nl = body.IndexOf('\n');
                    if (nl >= 0) { x.Fix = body.Substring(nl); body = body.Substring(0, nl); }
                    int colon = body.IndexOf(": ", StringComparison.Ordinal);
                    x.Who = m.Groups[1].Value; x.Tag = m.Groups[2].Value;
                    x.Head = colon >= 0 ? body.Substring(0, colon) : body; x.Why = colon >= 0 ? body.Substring(colon + 2) : null;
                    float t;
                    if (at.TryGetValue(line, out t)) x.At = t;
                }
                all.Add(x);
            }
            // a jerk a limb's flight made: a tail of the [fast] line of its own limb — an arm's bone under that hand's flight, a leg's under
            // that foot's — when that limb flew then (JerkWith). A bone of another limb than any that flew, the trunk, the head stay lines:
            // fall_back 2026-11-11, two leg bones stood under the hands' line, where a reader could no longer doubt them
            foreach (Said x in all)
            {
                if (x.Tag != "jerk" || x.Why == null || x.At < 0f || !WithRe.IsMatch(x.Why)) continue;
                Match bone = SpeedRe.Match(x.Head);
                string limb = bone.Success ? LimbOf(bone.Groups[1].Value) : null;
                if (limb == null) continue;
                foreach (Said f in all)
                {
                    if (f.Tag != "fast" || f.Who != x.Who || f.At < 0f || !f.Head.StartsWith(limb + " ", StringComparison.Ordinal) || Mathf.Abs(f.At - x.At) >= JerkWith || Across(edges, f.At, x.At)) continue;
                    if (f.Jerks == null) f.Jerks = new List<Said>();
                    f.Jerks.Add(x); x.Gone = true;
                    break;
                }
            }
            // lines of one body, one rule, one explanation, near in time: one line (the earliest holds the others)
            List<Said> timed = new List<Said>(), heads = new List<Said>();
            foreach (Said x in all) if (!x.Gone && x.At >= 0f) timed.Add(x);
            timed.Sort(ByTime);
            foreach (Said x in timed)
            {
                Said h = null;
                float near = (x.Tag == "jerk" ? FoldJerk : FoldNear) + 0.001f;
                if (x.Why != null) foreach (Said g in heads) if (g.Tag == x.Tag && g.Who == x.Who && g.Why == x.Why && x.At - g.At <= near && !Across(edges, g.At, x.At)) { h = g; break; }
                if (h == null) { heads.Add(x); continue; }
                if (h.With == null) h.With = new List<Said>();
                h.With.Add(x); x.Gone = true;
            }
            // by the time of the event: each body's lines with a moment among themselves, in the places they had
            List<Said> kept = new List<Said>(); List<string> whos = new List<string>();
            foreach (Said x in all) { if (x.Gone) continue; kept.Add(x); if (x.At >= 0f && !whos.Contains(x.Who)) whos.Add(x.Who); }
            foreach (string who in whos)
            {
                List<int> place = new List<int>(); List<Said> mine = new List<Said>();
                for (int i = 0; i < kept.Count; i++) if (kept[i].At >= 0f && kept[i].Who == who) { place.Add(i); mine.Add(kept[i]); }
                mine.Sort(ByTime);
                for (int i = 0; i < place.Count; i++) kept[place[i]] = mine[i];
            }
            List<string> one = new List<string>();
            foreach (Said x in kept) one.Add(Folded(x));
            return one;
        }

        // a line with what was folded into it: «[fast] A 8.77 and B 8.27 m/s at 0.52 s: why (+ jerks with it: C 4616, D 6054 deg/s at
        // 0.64–0.68 s)», then every «fix:» of them, each once (two limbs on one pair of keys give one «stretch»: run twice it stretches twice)
        private static string Folded(Said x)
        {
            if (x.With == null && x.Jerks == null) return x.Raw;
            List<Said> g = new List<Said> { x }, jerks = new List<Said>();
            if (x.With != null) g.AddRange(x.With);
            g.Sort((p, q) => p.Order.CompareTo(q.Order)); // (as they were said: left before right, down a limb)
            foreach (Said y in g) if (y.Jerks != null) jerks.AddRange(y.Jerks);
            StringBuilder s = new StringBuilder(x.Who).Append('[').Append(x.Tag).Append("] ").Append(g.Count > 1 ? SpeedsOf(g) ?? HeadsOf(g) : x.Head);
            if (x.Why != null) s.Append(": ").Append(x.Why);
            if (jerks.Count > 0) s.Append(" (+ jerks with it: ").Append(SpeedsOf(jerks) ?? HeadsOf(jerks)).Append(')');
            List<string> fixes = new List<string>();
            foreach (Said y in g) FixesOf(y, fixes);
            foreach (Said y in jerks) FixesOf(y, fixes);
            foreach (string f in fixes) s.Append('\n').Append(f);
            return s.ToString();
        }

        // «A 8.77 and B 8.27 m/s at 0.52 s» of [fast] or [jerk] lines (null: not all such, or not of one unit — HeadsOf says them whole)
        private static string SpeedsOf(List<Said> g)
        {
            StringBuilder s = new StringBuilder();
            string unit = null; float t0 = float.MaxValue, t1 = float.MinValue;
            foreach (Said y in g)
            {
                Match m = SpeedRe.Match(y.Head);
                if (!m.Success || (unit != null && unit != m.Groups[3].Value)) return null;
                unit = m.Groups[3].Value;
                s.Append(s.Length == 0 ? "" : g.Count == 2 ? " and " : ", ").Append(m.Groups[1].Value).Append(' ').Append(m.Groups[2].Value);
                t0 = Mathf.Min(t0, y.At); t1 = Mathf.Max(t1, y.At);
            }
            return s.Append(' ').Append(unit).Append(" at ").Append(N(t0)).Append(N(t1) != N(t0) ? "–" + N(t1) : "").Append(" s").ToString();
        }
        private static string HeadsOf(List<Said> g) { StringBuilder s = new StringBuilder(); foreach (Said y in g) s.Append(s.Length == 0 ? "" : " and ").Append(y.Head); return s.ToString(); }
        private static void FixesOf(Said y, List<string> into) { if (y.Fix == null) return; foreach (string f in y.Fix.Split('\n')) if (f.Length > 0 && !into.Contains(f)) into.Add(f); }

        // ---------------------------------------------------------------- marks on a shot (the head of this file)
        internal static float MarkLen = 0.08f; // m a drawn direction is long
        internal void Marks(List<Studio.Mark> into)
        {
            BodyAnimator an = BodyAnimator.Of(_h);
            if (an == null) return;
            Transform root = _h.transform;
            float ig; PAnimClip top = an.CodeClip(out ig); // (a target riding on a bone — «follow» — is not where its keys say in the body's frame: not drawn)
            Color32 fingers = new Color32(255, 220, 40, 255), palmC = new Color32(40, 230, 255, 255), asked = new Color32(255, 60, 255, 255), target = new Color32(255, 50, 50, 255);
            for (int s = 0; s < 2; s++)
            {
                Transform hand = _b[s == 0 ? HandL : HandR];
                if (hand == null) continue;
                string limb = s == 0 ? "@LeftHand" : "@RightHand";
                Vector3 along, palm;
                if (an.HandFacing(s == 0, hand, out along, out palm) && palm.sqrMagnitude > 1e-8f)
                {
                    into.Add(new Studio.Mark { A = hand.position, B = hand.position + along.normalized * MarkLen, C = fingers });
                    into.Add(new Studio.Mark { A = hand.position, B = hand.position + palm.normalized * MarkLen, C = palmC });
                }
                Vector3 n; float nw;
                bool rides = top != null && top.Follow.ContainsKey(limb);
                if (!rides && an.ChannelNow(limb + ".n", out n, out nw) && nw > 0.5f && n.sqrMagnitude > 1e-6f) into.Add(new Studio.Mark { A = hand.position, B = hand.position + root.TransformDirection(n).normalized * MarkLen, C = asked });
                Vector3 on; float ow;
                if (an.PropOn && an.ChannelNow(limb + ".on.n", out on, out ow) && ow > 0.5f && on.sqrMagnitude > 1e-6f) { Vector3 pp; Quaternion pr; an.PropAt(out pp, out pr); into.Add(new Studio.Mark { A = hand.position, B = hand.position + (pr * on).normalized * MarkLen, C = asked }); }
            }
            foreach (BodyClips.Limb m in BodyClips.Limbs)
            {
                Vector3 tgt; float tw;
                if (!an.ChannelNow(m.Name, out tgt, out tw) || tw < 0.5f || (top != null && top.Follow.ContainsKey(m.Name))) continue;
                Transform end = _b[m.Name == "@LeftHand" ? HandL : m.Name == "@RightHand" ? HandR : m.Name == "@LeftFoot" ? FootL : FootR];
                Vector3 p = root.TransformPoint(tgt);
                into.Add(new Studio.Mark { A = p, Cross = true, C = target });
                if (end != null) into.Add(new Studio.Mark { A = end.position, B = p, C = target });
            }
        }

        // the numbers, one line (the report)
        private string Range(int s) { return _wristTwistLow[s] > _wristTwist[s] ? "-" : N(_wristTwistLow[s]) + ".." + N(_wristTwist[s]); }

        internal string Numbers()
        {
            StringBuilder s = new StringBuilder("palm off L " + N(_palm[0]) + " R " + N(_palm[1]) + " deg · wrist twist L " + Range(0) + " R " + Range(1) + " deg, bend L " + N(_wristBend[0]) + " R " + N(_wristBend[1]) + " deg · fingers curl L " + N(_curl[0]) + " R " + N(_curl[1]) + " deg · past the other elbow L " + N(_past[0]) + " R " + N(_past[1]) + " m · elbow behind L " + N(_elbow[0]) + " R " + N(_elbow[1]) + " m · tightest elbow " + N(_fold) + " deg · shoulder " + N(_shoulder) + " deg · neck " + N(_neck) + " deg · deepest in the body " + N(_inside * 100f) + "% (" + (_insideWhat.Length > 0 ? _insideWhat : "-") + ") · knee lowest " + N(_knee) + " m · feet lowest L " + N(_lowest[0]) + " R " + N(_lowest[1]) + " m · hips " + N(_hipsLow) + " .. " + N(_hipsHigh) + " m · feet slid L " + N(_slid[0]) + " R " + N(_slid[1]) + " m");
            foreach (KeyValuePair<string, Worst> m in _miss) s.Append(" · ").Append(m.Key).Append(" missed by ").Append(N(m.Value.V)).Append(" m");
            foreach (KeyValuePair<string, Worst> m in _moved) s.Append(" · ").Append(m.Key).Append(" moved after the pose ").Append(M(m.Value.V)).Append(" m"); // (said always, under the limit too: the number that would have named 2026-11-12's fault at a glance)
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
            return "hips " + N(L(Hips).y) + "  lean " + N(lf) + "," + N(lr) + "  head " + N(hu) + "," + N(hr) + "  Lfoot " + P(L(FootL)) + "  Rfoot " + P(L(FootR)) + "  Lknee " + P(L(KneeL)) + "  Rknee " + P(L(KneeR)) + "  Lhand " + P(L(HandL)) + "  Rhand " + P(L(HandR)) + "  Lelbow " + P(L(ForeL)) + "  Relbow " + P(L(ForeR)) + "  Lshoulder " + P(L(ArmL)) + "  Rshoulder " + P(L(ArmR)) + HeadSaid();
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
            if (an != null && hand != null && an.HandFacing(left, hand, out along, out palm) && palm.sqrMagnitude > 1e-8f)
            {
                string got = Facing(_h.transform.InverseTransformDirection(palm).normalized);
                w += ", palm " + got;
                Vector3 askN; float askW; // (what the clip itself asks of the palm, by its «.n» at full weight: the words say when the two differ — a palm no one looked at, door_push 2026-11-04)
                if (an.ChannelNow((left ? "@LeftHand" : "@RightHand") + ".n", out askN, out askW) && askW >= Full && askN.sqrMagnitude > 1e-6f) { string ask = Facing(askN.normalized); if (ask != got) w += " (asked " + ask + " ✗)"; }
            }
            string why; float ig;
            if (an != null && an.PropOn && GripOf(left, an, _h.GetComponent<StoryPropView>(), out why, out ig)) return w + (why == null ? ", HOLDS the prop" : ", on the prop but not holding it: " + why) + " [" + DigitsSaid(left ? 0 : 1) + "]"; // (on the thing: what the table under it says is not about this hand · every digit against its surface, m: the eye does not see 2 cm of a finger on a frame)
            foreach (Fixture f in Fixtures) { if (f.Kind == "point") continue; float d = f.Distance(h) - Palm; if (d <= TouchFar && d >= -TouchIn) { w += ", ON the " + f.Name; List<string> ig2 = new List<string>(); int l2, f2; if (OnFixture(left ? 0 : 1, f, an, ig2, out l2, out f2) && f2 > 0) w += " [" + DigitsSaid(left ? 0 : 1) + "]"; break; } }
            float body = PalmToBody(left);
            if (body < BodySay) w += body <= BodyOn && body >= -BodyInMost ? ", ON the body" : body < -BodyInMost ? ", " + N(-body) + " m INTO the body" : ", " + N(body) + " m off the body"; // (a front view shows no depth)
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

        // ---------------------------------------------------------------- those words against the pose said before (the head of this file)
        // The parts of Words (split by «; », each by what it is about: «trunk», «L hand», «knees») held against the pose as the reader has
        // it (known: each part as last SAID, brought up to date here): the parts that differ, in their order — "" none, the words whole when
        // all do. Against what was said, not the row before: a slow drift is said once it is past SameDeg / SameM. A part the words no
        // longer have (a foot back on the ground) is said gone. (fall_back 2026-11-11: seven rows of 1818 bytes, the last four the body lying still.)
        internal static float SameDeg = 3f, SameM = 0.02f; // deg (trunk, head, knees) · m (the rest): a number this near the one said is the same; 0: every digit that moves is said
        internal static string Differs(string words, Dictionary<string, string> known)
        {
            StringBuilder s = new StringBuilder();
            List<string> seen = new List<string>();
            foreach (string part in words.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string key = PartKey(part), was;
                seen.Add(key);
                if (known.TryGetValue(key, out was) && PartSame(key, was, part)) continue;
                known[key] = part;
                s.Append(s.Length > 0 ? "; " : "").Append(part);
            }
            foreach (string key in new List<string>(known.Keys)) if (!seen.Contains(key)) { known.Remove(key); s.Append(s.Length > 0 ? "; " : "").Append(key).Append(": not in this row"); }
            return s.ToString();
        }
        private static string PartKey(string part) { string[] w = part.Split(' '); return w.Length > 1 && (w[0] == "L" || w[0] == "R") ? w[0] + " " + w[1] : w[0]; }
        private static bool PartSame(string key, string a, string b)
        {
            if (a == b) return true;
            string[] x = a.Split(' '), y = b.Split(' ');
            if (x.Length != y.Length) return false;
            float most = (key == "trunk" || key == "head" || key == "knees" ? SameDeg : SameM) + 0.001f, p, q; // (a little over: 0.06 − 0.04 is not 0.02 in a float)
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i] && (!float.TryParse(x[i], NumberStyles.Float, CultureInfo.InvariantCulture, out p) || !float.TryParse(y[i], NumberStyles.Float, CultureInfo.InvariantCulture, out q) || Mathf.Abs(p - q) > most)) return false;
            return true;
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
        //   want <t|t0-t1> hand left|right|both on body [m]    its palm on the trunk: within m (BodyOn) of its surface, not deep in it
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
            public float MissFrom = -1f, MissTo = -1f, LastT = -9f; // (a stretch: the first and the last moment it did not hold — where to look, said with it)
            // the body at its best frame: what a fix reckons from (Snap)
            public string Clip; public float ClipAt, LeanF, LeanR, HeadU, HeadR; public bool HasLook; public Vector3 HeadPos, LookPt;
            public readonly Vector3[] Hand = new Vector3[2], Along = new Vector3[2], PalmN = new Vector3[2];
            public void Look(BodyCheck c, float t)
            {
                float from = Stretch ? From : From - WantAround, to = Stretch ? To : To + WantAround;
                if (t < from || t > to) return;
                if (Mathf.Abs(t - LastT) < 1e-4f) return; // (the clip's time standing: its end held while it fades out — take_book's «holds» lost 12% at 8 s, all after the clip — one moment counted once)
                LastT = t;
                float value;
                bool ok = c.Holds(W, out value);
                Frames++; if (ok) Good++; else { if (MissFrom < 0f) MissFrom = t; MissTo = t; }
                if (!Seen || value > Best) { Best = value; Seen = true; c.Snap(this); }
            }
            public bool Passed { get { return Stretch ? Frames > 0 && Good >= Frames * WantShare : Good > 0; } }
        }

        private void Snap(Want x)
        {
            x.Clip = _clipNow; x.ClipAt = _momentNow;
            x.Hand[0] = L(HandL); x.Hand[1] = L(HandR); x.HeadPos = L(Head);
            BodyAnimator an = BodyAnimator.Of(_h);
            Transform root = _h.transform;
            Vector3 lk = Vector3.zero; float lw = 0f;
            x.HasLook = an != null && an.LookNow(out lk, out lw) && lw > 0.001f; x.LookPt = x.HasLook ? root.InverseTransformPoint(lk) : Vector3.zero;
            for (int s = 0; s < 2; s++) { Transform hand = _b[s == 0 ? HandL : HandR]; Vector3 al, pm; if (an != null && hand != null && an.HandFacing(s == 0, hand, out al, out pm) && pm.sqrMagnitude > 1e-8f) { x.Along[s] = root.InverseTransformDirection(al).normalized; x.PalmN[s] = root.InverseTransformDirection(pm).normalized; } }
            Lean(out x.LeanF, out x.LeanR, true); HeadAim(out x.HeadU, out x.HeadR);
        }

        // what to change for a want that did not hold, from the body at its best frame and the clip's own keys there (the head of this file)
        private string WantFix(Want x)
        {
            string[] w = x.W; string clip = x.Clip; float at = x.ClipAt;
            if (w.Length < 2 || !WholeOf(ref clip, ref at)) return "";
            PAnimClip c = BodyClips.Find(clip);
            switch (w[0])
            {
                case "lean":
                    {
                        float need = WArg(w, 2, 5f), got = w[1] == "forward" ? x.LeanF : w[1] == "back" ? -x.LeanF : w[1] == "right" ? x.LeanR : -x.LeanR;
                        if (w[1] == "left" || w[1] == "right") return " — asked " + N(need) + " → got " + N(got) + ": a lean aside is the z of Spine1/Spine2 (body mode)";
                        // (the lean more, shared out over the spine channels the clip has, each no more than SpineMost, into every key of the hold
                        //  round that moment: one bone driven alone went 38 → 54 → 67 and still read short, basket_set 2026-11-10 — a spine reads
                        //  less the more it bends; what the spine cannot give is the hips' and the knees', said so)
                        List<string> sp = new List<string>(); float sum = 0f;
                        foreach (string s in new[] { "Spine1", "Spine2" }) if (c.Bones.Contains(s)) { sp.Add(s); sum += Mathf.Abs(c.Value(s, KeyNear(clip, s, at)).x); }
                        if (sp.Count == 0) return " — asked " + N(need) + " → got " + N(got) + ": no Spine1/Spine2 channel — add «mode Spine1 body» and Spine1 keys (about " + N(need * 1.7f) + " for " + N(need) + " deg of lean here)";
                        if (sum >= SpineBent) // (the spine bent that far already gives no more lean: 54+14 and 45+22 both read 28-29, basket_set 2026-11-10 — no command, the pose's shape)
                            return " — asked " + N(need) + " → got " + N(got) + " (the spine keys already " + N(sum) + " deg in all: more spine gives no more lean): the rest from the hips — the body lowered («move» y) and the knees bent; a new shape of the pose, look at the frame, no key to change";
                        float sign = w[1] == "back" ? -1f : 1f, per = got > 2f && sum >= 1f ? got / sum : 0.6f; // (lean per key degree, as it read here)
                        float more = Mathf.Max(0f, (need + 1f - got) / per) / sp.Count;
                        StringBuilder ops = new StringBuilder(); bool capped = false;
                        foreach (string s in sp)
                        {
                            float t = KeyNear(clip, s, at); Vector3 kv = c.Value(s, t);
                            float to = Mathf.Abs(kv.x) + more;
                            if (to > SpineMost) { to = SpineMost; capped = true; }
                            foreach (PAnimKey k in c.Keys) // (the hold: the keys round it with the same value)
                            {
                                Vector3 v; if (!k.Has(s) || !k.V.TryGetValue(s, out v) || Mathf.Abs(k.T - t) > HoldNear || Mathf.Abs(v.x - kv.x) > 0.5f) continue;
                                if (ops.Length > 0) ops.Append("; ");
                                ops.Append("set ").Append(s).Append(' ').Append(N(k.T)).Append(' ').Append(V(new Vector3(sign * to, v.y, v.z)));
                            }
                        }
                        string said = " — asked " + N(need) + " → got " + N(got) + " (the spine keys, " + N(sum) + " deg in all, read as " + N(got) + " here; near, the next take says how near)";
                        if (capped) said += "; the spine alone cannot give it (each bone at most " + N(SpineMost) + "): the rest from the hips — lower «move» y and let the knees bend";
                        return ops.Length == 0 ? said : said + FixLine("batch " + clip + " \"" + ops + "\"");
                    }
                case "head":
                    {
                        float need = WArg(w, 2, 10f), got = w[1] == "up" ? x.HeadU : w[1] == "down" ? -x.HeadU : w[1] == "right" ? x.HeadR : -x.HeadR;
                        if (!x.HasLook || !c.Bones.Contains(BodyClips.LookAt)) return " — asked " + N(need) + " → got " + N(got) + ": no @Look in the clip, the head is the game's (give it @Look + @Look.w)";
                        Vector3 dir = x.LookPt - x.HeadPos;
                        if (dir.sqrMagnitude < 1e-4f) return "";
                        float delta = need - got + 2f;
                        Quaternion q = w[1] == "up" ? Quaternion.AngleAxis(-delta, Vector3.right) : w[1] == "down" ? Quaternion.AngleAxis(delta, Vector3.right) : w[1] == "right" ? Quaternion.AngleAxis(delta, Vector3.up) : Quaternion.AngleAxis(-delta, Vector3.up);
                        return " — asked " + N(need) + " → got " + N(got) + " (@Look " + V(x.LookPt) + " from the head at " + V(x.HeadPos) + ": the point turned " + N(delta) + " deg " + w[1] + ")" + FixLine("set " + clip + " @Look " + N(KeyNear(clip, BodyClips.LookAt, at)) + " " + V(x.HeadPos + q * dir));
                    }
                case "hand":
                    {
                        if (w.Length < 6 || w[2] != "near") return "";
                        Vector3 asked = new Vector3(WArg(w, 3, 0f), WArg(w, 4, 1f), WArg(w, 5, 0.3f));
                        float tol = WArg(w, 6, 0.1f);
                        string s = "";
                        for (int side = 0; side < 2; side++)
                        {
                            if (w[1] != "both" && (w[1] == "left") != (side == 0)) continue;
                            string limb = side == 0 ? "@LeftHand" : "@RightHand";
                            if (c.Follow.ContainsKey(limb)) { s += " — the target rides on a bone («follow»): reckon it in that bone's frame"; continue; }
                            Vector3 key = c.Bones.Contains(limb) ? c.Value(limb, at) : Vector3.zero;
                            if (c.Bones.Contains(limb) && Vector3.Distance(key, asked) <= tol) s += " — the clip's key already asks " + V(key) + " and the hand was at " + V(x.Hand[side]) + ": it cannot get there (see the [miss] line)";
                            else s += " — the hand was at " + V(x.Hand[side]) + ", its key " + (c.Bones.Contains(limb) ? V(key) : "none") + FixLine("set " + clip + " " + limb + " " + N(KeyNear(clip, limb, at)) + " " + V(asked));
                        }
                        return s;
                    }
                case "palm":
                    {
                        if (w.Length < 3) return "";
                        Vector3 want = w[2] == "forward" ? Vector3.forward : w[2] == "back" ? Vector3.back : w[2] == "up" ? Vector3.up : w[2] == "down" ? Vector3.down : w[2] == "right" ? Vector3.right : Vector3.left;
                        string s = "";
                        for (int side = 0; side < 2; side++)
                        {
                            if (w[1] != "both" && (w[1] == "left") != (side == 0)) continue;
                            string limb = side == 0 ? "@LeftHand" : "@RightHand";
                            if (c.Bones.Contains(limb + ".on.n")) { s += " — the " + (side == 0 ? "left" : "right") + " hand is on the thing: turn " + limb + ".on.n (in the thing's frame)"; continue; }
                            if (c.Follow.ContainsKey(limb)) { s += " — the hand rides on a bone («follow»): its .f/.n are in that bone's frame"; continue; }
                            Vector3 f = Vector3.ProjectOnPlane(x.Along[side].sqrMagnitude > 1e-6f ? x.Along[side] : Vector3.forward, want); // (the fingers as they go now, made square to the palm asked: .f and .n on one line say no turn)
                            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(Vector3.forward, want);
                            f.Normalize();
                            if (c.Bones.Contains(limb + ".n"))
                            {
                                float t = KeyNear(clip, limb + ".n", at);
                                Vector3 fNow = c.Bones.Contains(limb + ".f") ? c.Value(limb + ".f", t) : Vector3.zero;
                                bool fOk = fNow.sqrMagnitude > 1e-6f && Mathf.Abs(Vector3.Dot(fNow.normalized, want)) < 0.9f;
                                s += " — the palm faced " + Facing(x.PalmN[side]) + FixLine(fOk ? "set " + clip + " " + limb + ".n " + N(t) + " " + V(want) : "batch " + clip + " \"set " + limb + ".f " + N(t) + " " + V(f) + "; set " + limb + ".n " + N(t) + " " + V(want) + "\"");
                            }
                            else
                            {
                                float t = KeyNear(clip, limb, at);
                                s += " — the palm faced " + Facing(x.PalmN[side]) + " and nothing turns it: give the hand .f (the fingers) and .n (the palm)" + FixLine("batch " + clip + " \"set " + limb + ".f " + N(t) + " " + V(f) + "; set " + limb + ".n " + N(t) + " " + V(want) + "\"");
                            }
                        }
                        return s;
                    }
                case "hips":
                    {
                        float need = WArg(w, 2, 0.1f), got = x.Best; // (Holds: the value is how far down the hips came)
                        return " — asked " + N(need) + " m down → got " + N(got) + MoveFix(x.Clip, x.ClipAt, -(need - got + 0.01f));
                    }
            }
            return "";
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
                if (dash > 0 && float.TryParse(w[0].Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && float.TryParse(w[0].Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                {
                    // (a stretch is judged where the clip is on in full: not on its way in from the game's stance, nor out — basket_carry's «0-4» read
                    //  «not held 0–0.51 s» in its 0.7 s fade, 2026-11-10; the user's words stay as he said them)
                    float on = Mathf.Max(a, c.FadeIn), off = c.Loop ? b : Mathf.Min(b, c.Length - c.FadeOut);
                    if (off <= on) { on = a; off = b; }
                    x.From = on + at; x.To = off + at; x.Stretch = true;
                }
                else if (float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) { x.From = x.To = a + at; }
                else continue;
                if (at > 0f) x.Text = c.Name + " " + line;
                _wants.Add(x);
            }
        }

        private static int WantWords(string kind) { return kind == "head" || kind == "lean" || kind == "knee" || kind == "weight" || kind == "look" ? 2 : kind == "hand" || kind == "palm" || kind == "foot" || kind == "elbow" ? 3 : kind == "finger" ? 4 : 1; }

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
                string got = x.Frames == 0 ? "never reached in the run" : (x.Stretch ? Mathf.RoundToInt(100f * x.Good / x.Frames) + "% of the frames" + (x.MissFrom >= 0f ? " (not held at " + N(x.MissFrom) + (x.MissTo > x.MissFrom + 0.05f ? "–" + N(x.MissTo) : "") + " s)" : "") + ", " : "") + "best " + N(x.Best);
                s.Append(ok ? "  yes  " : "  NO   ").Append(x.Text).Append(" (").Append(got).Append(")\n");
                string how = x.Frames == 0 ? "" : x.Stretch && x.Good > 0 ? " — reached, but not for the whole stretch: look at when it was not (a key there, or the way in and out)" : WantFix(x); // (a pose reached is not out of reach: basket_carry 2026-11-10 was told «cannot get there, see [miss]» with no [miss] and its best on the spot)
                if (!ok) Say(bad, "[want] not what was asked: want " + x.Text + " (" + got + ")" + how, x.Stretch && x.MissFrom >= 0f ? x.MissFrom : x.From);
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
                        return value >= need - WantDeg;
                    }
                case "lean":
                    {
                        Lean(out a, out b, true);
                        float need = WArg(w, 2, 5f);
                        value = w[1] == "forward" ? a : w[1] == "back" ? -a : w[1] == "right" ? b : -b;
                        return value >= need - WantDeg;
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
                case "elbow": // elbow left|right bent <deg>: bent that much from straight
                    {
                        bool l = w[1] == "left";
                        Vector3 fore = L(l ? ForeL : ForeR);
                        value = 180f - Vector3.Angle(L(l ? ArmL : ArmR) - fore, L(l ? HandL : HandR) - fore);
                        return value >= WArg(w, 3, 30f);
                    }
                case "smooth": // <t0-t1> smooth: no jolt in the stretch (the hand's speed, a bone's turn, the jerk rules' own limits)
                    value = -Mathf.Max(_sp / HandFast, _spin0 / BoneJerk);
                    return _sp <= HandFast && _spin0 <= BoneJerk;
                case "look": // look prop | look <x y z>: the face (its own axis) on the thing in the hands, or on a point of the body's frame
                    {
                        Vector3 at;
                        if (_b[Head] == null || _faceAxis.sqrMagnitude < 0.01f || !LookPoint(w, out at)) { value = -999f; return false; }
                        value = -Vector3.Angle(_b[Head].TransformDirection(_faceAxis), at - _b[Head].position);
                        return value >= -LookMiss;
                    }
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

        // «look prop» — the thing in the hands (BodyAnimator.PropAt); «look <x y z>» — a point in the body's frame; false: nothing to look at
        private bool LookPoint(string[] w, out Vector3 at)
        {
            at = Vector3.zero;
            if (w.Length > 1 && w[1] == "prop") { BodyAnimator an = BodyAnimator.Of(_h); Quaternion r; if (an == null || !an.PropOn) return false; an.PropAt(out at, out r); return true; }
            if (w.Length < 4) return false;
            at = _h.transform.TransformPoint(new Vector3(WArg(w, 1, 0f), WArg(w, 2, 1.5f), WArg(w, 3, 1f)));
            return true;
        }

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
            if (w[2] == "on" && w.Length > 3 && w[3] == "body") { float b = PalmToBody(left); value = -Mathf.Abs(b); return b <= WArg(w, 4, BodyOn) && b >= -BodyInMost; } // (its palm on the trunk: PalmToBody)
            if (w[2] == "on")
            {
                if (Fixtures.Count == 0 && Real) { float g = _realGap[left ? 0 : 1]; value = -Mathf.Abs(g); return g <= TouchFar && g >= -TouchIn; } // (a scene: the world's own surface)
                value = -9f;
                foreach (Fixture f in Fixtures) { if (f.Kind == "point") continue; value = Mathf.Max(value, -Mathf.Abs(f.Distance(h) - Palm)); }
                return value >= -TouchFar;
            }
            if (w[2] == "touches") // hand … touches prop: the hand at the thing's surface (StoryPropView, the thing a scene's «free» prop or the stage's is)
            {
                StoryPropView view = _h.GetComponent<StoryPropView>();
                Transform hb = _b[left ? HandL : HandR];
                if (view == null || hb == null) { value = -9f; return false; }
                float d = view.Distance(hb.position);
                value = -Mathf.Abs(d);
                return d <= TouchFar && d >= -FingerDeep;
            }
            if (w[2] == "below") { float dy = L(left ? ShL : ShR).y - h.y; value = dy; return dy > 0f; } // hand … below shoulder
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
            Vector3 want;
            if (way == "to") { Vector3 pp; Quaternion pr; if (!an.PropOn) { value = -999f; return false; } an.PropAt(out pp, out pr); want = _h.transform.InverseTransformDirection(pp - hand.position).normalized; } // (palm … to prop: the palm turned to the thing in the hands)
            else want = way == "forward" ? Vector3.forward : way == "back" ? Vector3.back : way == "up" ? Vector3.up : way == "down" ? Vector3.down : way == "right" ? Vector3.right : Vector3.left;
            value = 180f - Vector3.Angle(d, want);
            return value >= 135f;
        }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static string V(Vector3 v) { return N(v.x) + " " + N(v.y) + " " + N(v.z); }
        private static string P(Vector3 v) { return v.x.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
#endif

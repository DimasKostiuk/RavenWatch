#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The raven on the shoulder is alive. After the game's own animation, every frame, its head is turned to what
    // matters: a boss, the nearest enemy, what you are looking at, another player; with nothing around it glances
    // about now and then. A raven not yet woken keeps its head low and moves slowly, and lifts it only to answer.
    // Ravens on other players live the same way, on every game, from what is around their wearer.
    internal class ShoulderLife : MonoBehaviour, ISitBird
    {
        internal static float LookRange = 20f, BossRange = 40f, PlayerRange = 6f;
        internal static float SleepyDegrees = 35f;   // how low an unwoken raven holds its head
        internal static float HunchDegrees = 15f;    // and how it hunches its back
        private const float MaxYaw = 75f, MaxPitch = 35f;

        internal Player Player;
        internal bool Munin;
        internal int Level;

        private Transform _head, _spine;
        internal static float FluffScale = 0.06f; // how much bigger it looks puffed up (WorldReact)
        private float _fluff, _fluffWant, _fluffAt;
        private Quaternion _spineBase, _spineWritten;
        private bool _spineWrote;
        private Animator _anim;
        private Quaternion _look = Quaternion.identity; // the extra turn of the head, in the world
        private Quaternion _lookMid = Quaternion.identity; // its first eased step
        private Quaternion _base, _written;             // the head's own pose and what was written over it
        private bool _wrote;
        private Character _foe;
        private Transform _thing;
        private float _nextSearch, _nextGlance, _glanceUntil, _awakeUntil, _nextCaw, _nextStartle;
        private float _glanceYaw, _glancePitch;
        private ZDOID _bossSeen = ZDOID.None;
        private int _eyes; // the EyeGlow version these eyes show
        internal void EyesAgain() { _eyes = 0; } // its materials changed (RavenWear): the eyes are made again on them
        private static bool _bonesLogged;

        // ---------------------------------------------------------------- wings
        // A wing is three bones (Wing1 at the shoulder, wing2 the elbow, wing3 the hand). It is turned in the world,
        // by the raven's own axes: "out" swings it from along the back out to the side, "up" lifts it, "elbow" opens
        // the folded part. Signs and sizes can be tried live with the console: raven wings <out> <up> [elbow].
        internal static bool TestWings;
        internal static float TestOut, TestUp, TestElbow;
        internal static float FlapOut = 55f, FlapUp = 35f, FlapBeat = 40f, FlapHz = 3f;
        internal static float HoverHz = 2.1f; // hanging in the air during an emote: calmer beats than a startled flap (30% slower)
        internal static float GlideOut = 70f, GlideUp = 10f, GlideElbow = 45f;
        // Munin's Wings while running: spread, but swept back and down (the "ninja run"), beating lightly
        internal static float RunOut = 50f, RunUp = -20f, RunElbow = 10f, RunSpeed = 5f;
        internal static float RunBeat = 7f, RunBeatHz = 1.8f;
        internal static float TakeOffUp = 20f, TakeOffBeat = 75f, TakeOffHz = 2.4f;
        private sealed class Wing { public BoneTurn A, B, C; }
        private Wing _left, _right;
        private float _flapUntil, _out, _up, _elbow, _inOut, _inUp, _inElbow;

        // ---------------------------------------------------------------- wings for the gifts
        // Both wings, plus what the inner wing (the one by your head) adds on top, plus a tremble. Munin's fury holds
        // its pose while it lasts (a net flag); the scream and the healing are a moment, sent to everyone nearby.
        // Tried live with: raven pose <name> [out up elbow [inner out up elbow [tremble]]]
        internal sealed class GiftPose
        {
            public float Out, Up, Elbow, InOut, InUp, InElbow, Shake;
            public GiftPose(float o, float u, float e, float io, float iu, float ie, float shake)
            { Out = o; Up = u; Elbow = e; InOut = io; InUp = iu; InElbow = ie; Shake = shake; }
        }

        internal static readonly Dictionary<string, GiftPose> Poses = new Dictionary<string, GiftPose>
        {
            { "fury",   new GiftPose(65f, 30f, 50f, 0f, 0f, 0f, 4f) },  // (the console only: the fury itself is FuryPose)
            { "scream", new GiftPose(85f, 50f, 70f, 0f, 0f, 0f, 2f) },  // wide open for a moment
            { "heal",   new GiftPose(0f, 0f, 0f, 70f, 15f, 70f, 0f) },  // one wing round your head
            { "dress",  new GiftPose(55f, 35f, 45f, 0f, 0f, 0f, 3f) },  // a thing put on: wings up, settling it with a shiver
        };
        internal static float ScreamSeconds = 1.3f, HealSeconds = 2.5f;
        private const string RpcHeal = "RavenWatch_PoseHeal";
        private string _moment;
        private float _momentUntil;

        internal static void Register() { RavenFx.Register(RpcHeal, p => Moment(p, true, "heal", HealSeconds)); }

        // Munin puts a wing round you: here and on every game nearby
        internal static void Hug(Player p) { RavenFx.Broadcast(RpcHeal, p, x => Moment(x, true, "heal", HealSeconds)); }

        // a gift's pose for a moment on that player's raven (any player's)
        internal static void Moment(Player p, bool munin, string pose, float seconds)
        {
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            if (life == null || life.Asleep || !Poses.ContainsKey(pose)) return;
            life._moment = pose;
            life._momentUntil = Time.time + seconds;
        }
        private Vector3 _lastPos;
        private float _speed; // the wearer's speed over the ground, smoothed (works for other players too)

        // a bone turned over the game's animation; when the animation did not set it this frame, from its last pose
        private sealed class BoneTurn
        {
            public Transform T;
            private Quaternion _base, _written;
            private bool _wrote;
            public BoneTurn(Transform t) { T = t; }
            public void Begin()
            {
                if (T == null) return;
                Quaternion l = T.localRotation;
                if (_wrote && Quaternion.Angle(l, _written) < 0.01f) l = _base;
                _base = l;
                T.localRotation = l;
            }
            public void Turn(Quaternion world) { if (T != null) T.rotation = world * T.rotation; }
            public void End() { if (T == null) return; _written = T.localRotation; _wrote = true; }
        }

        // a few quick beats of the wings (a jump, a blow)
        internal void Flap(float seconds)
        {
            if (Asleep) return;
            // a new series (not one going on): its own phase and a pace of its own, so two ravens never beat as one
            if (Time.time >= _flapUntil) { _flapFrom = Time.time - Random.value / FlapHz; _flapRate = Random.Range(0.85f, 1.15f); }
            _flapUntil = Mathf.Max(_flapUntil, Time.time + seconds);
        }
        private float _flapFrom, _flapRate = 1f;

        // Munin in his fury: wings folded but lifted, bristling with a fine tremble, and every couple of seconds (never
        // the same gap) a sharp threat: the wings snap wide and fold again; now and then an angry croak with it.
        // Each game draws it on its own: the same restlessness, the moments its own.
        internal static float FuryFlareSeconds = 0.5f, FuryGapMin = 1.6f, FuryGapMax = 3.2f, FuryCawChance = 0.25f, FuryHunch = 10f;
        private float _furyNext, _furyFlareFrom = -1f;

        // Hugin's leap carries you: each raven glides with its own slow bob, and at moments of its own throws in a
        // series of one to three beats at its own pace — Hugin calmer, Munin quicker — so the two are never in step
        private float _lgNext, _lgUntil, _lgFrom, _lgHz, _lgBob, _lgBobPhase;

        private void LeapWings(out float o, out float u, out float e, out float hz, out float amp)
        {
            hz = 0f; amp = 0f;
            float t = Time.time;
            if (_lgBob <= 0f) { _lgBob = Random.Range(1.8f, 3.2f); _lgBobPhase = Random.value * Mathf.PI * 2f; _lgNext = t + Random.Range(0.1f, 0.9f); _lgUntil = 0f; }
            if (t >= _lgNext && t >= _lgUntil)
            {
                _lgHz = Munin ? Random.Range(3f, 3.9f) : Random.Range(2.3f, 3f);
                _lgFrom = t;
                _lgUntil = t + Random.Range(1, 4) / _lgHz;
                _lgNext = _lgUntil + Random.Range(0.5f, 1.8f);
            }
            if (t < _lgUntil)
            {
                hz = _lgHz;
                if (Munin) { o = FlapOut * 0.45f; u = FlapUp * 0.6f; e = FlapOut * 0.25f; amp = FlapBeat * 0.8f; }
                else { o = FlapOut; u = FlapUp; e = FlapOut * 0.6f; amp = FlapBeat * 0.9f; }
                return;
            }
            o = GlideOut; u = GlideUp + Mathf.Sin(t * _lgBob + _lgBobPhase) * 4f; e = GlideElbow;
        }

        private void FuryPose(out float o, out float u, out float e)
        {
            float t = Time.time;
            if (_furyNext <= 0f) _furyNext = t + Random.Range(0.3f, 1f); // the first a moment after it starts
            if (t >= _furyNext)
            {
                _furyFlareFrom = t;
                _furyNext = t + Random.Range(FuryGapMin, FuryGapMax);
                if (Random.value < FuryCawChance) Caw(Random.Range(0.8f, 0.95f), 0.55f);
            }
            float f = 0f; // how open the threat is: snaps open in a quarter, folds back in the rest
            if (_furyFlareFrom >= 0f)
            {
                float x = (t - _furyFlareFrom) / Mathf.Max(0.1f, FuryFlareSeconds);
                if (x >= 1f) _furyFlareFrom = -1f;
                else f = x < 0.25f ? x / 0.25f : 1f - (x - 0.25f) / 0.75f;
            }
            float bristle = Mathf.Sin(t * 23f) * 2.5f * (1f - f);
            o = Mathf.Lerp(12f, 72f, f);
            u = Mathf.Lerp(10f, 42f, f) + bristle;
            e = Mathf.Lerp(8f, 58f, f);
        }

        // leaving the shoulder: wide, strong beats until it is gone (even a sleepy raven wakes up for that)
        private bool _takingOff;
        // In flight (the dome, a strike, hanging in the air during a dance): the game's own flying clip of the raven,
        // played round and round by hand instead of our bone-turned beats. The animator is paused meanwhile.
        internal bool VanillaFly
        {
            get { return _vanillaFly; }
            set
            {
                if (_vanillaFly == value) return;
                if (value && _landing && _landState != 0) { PendingState = _landState; _landState = 0; } // its game state is still to be ended after the next landing
                if (value && _landing) { _landing = false; if (_rootBones != null) { for (int i = 0; i < _rootBones.Length; i++) if (_rootBones[i] != null) _rootBones[i].localPosition = _rootPos[i]; _rootBones = null; } }
                bool held = value && Time.time < _flyHoldUntil; // (the take-off's crouch was set first: it keeps its stroke top and unfold)
                RavenSkeleton sk = Skel;
                if (sk != null)
                {
                    if (_anim != null && _anim.enabled) sk.Active = false; // (the game's animator had it: from its pose)
                    // from the pose it has now into the flight (or the sitting), eased; the crouch holds the change, the wing unfolds
                    sk.SetBase(value ? FlyD : null, value ? BlendTakeOff : BlendLand);
                    sk.Begin();
                    if (held) sk.Stretch(_flyHoldUntil - Time.time + BlendTakeOff, _flyUnfold);
                }
                Flight = new WingMods.State(); _mods.Reset();
                _mixX = -100f; // no stroke seen yet in this flight (a template waits for the first top)
                _vanillaFly = value;
                if (_anim != null) _anim.enabled = sk == null && (!value || FlyClip(_anim) == null); // (the skeleton has it: the game's animator stays off)
                if (!value) { FinishTakeoff(); Unlevel(); _tuck = 0f; _reach = 0f; FlapRate = 1f; Flare = 0f; Mix.Clear(); } // the next flight pulls the legs in again from the perch (and starts without a template)
                if (!value && _rootBones != null) { for (int i = 0; i < _rootBones.Length; i++) if (_rootBones[i] != null) _rootBones[i].localPosition = _rootPos[i]; _rootBones = null; }
                if (!held) _flyT = UnityEngine.Random.Range(0f, 1f);
            }
        }
        private bool _vanillaFly;
        private float _flyT;
        private float _flyClock;
        private float _takeoffStarted = -1f;
        private TakeoffKeys.Rig _takeoffRig;
        internal static float TakeoffEntrySeconds = 0.14f;
        internal static float TakeoffBlendSeconds = 0.22f;
        private bool TakeoffActive
        {
            get
            {
                if (!_takingOff) return false;
                if (Time.time - _takeoffStarted < TakeoffKeys.Duration) return true;
                _takingOff = false;
                // only the take-off keys' pose has anything to ease out of: without them the flight's beat already
                // runs, and a snapshot here would damp it for a moment (a hitch 0.9 s after every take-off)
                if (TakeoffKeys.Active && _skel != null) _skel.EaseFromNow(TakeoffBlendSeconds);
                return false;
            }
        }

        // ---------------------------------------------------------------- the game's own landing
        // The last metre is not drawn by us: the game's raven has a fine landing of its own (the end of its swoop-in
        // clip: braking with the wings, the touch, folding). That part is played here, the body's travel in it held
        // in place, while the pin carries the bird the last way onto the shoulder at the clip's own pace (Progress).
        // It ends in the game's sitting pose, so the hand-back to the sitting animation is seamless.
        internal static float LandLead = 0.5f, LandAfter = 3f, LandSpeed = 1f; // lead: s before the touch, from where it beats to brake; after: s to the clip's end, wings folded
        private static AnimationClip _landClip;
        private static bool _landLooked;
        private static float _landA = -1f, _landB = -1f;
        private static float _landTouch = -1f; // when in the clip it touches down
        private float _landRate = 1f;          // the clip's pace until the touch, to keep the speed it came in at
        private float _landStyleRate = 1f;     // a small per-landing tempo difference; the vanilla pose stays intact
        private static float[] _landWay;      // how far along the way down (0..1) over the played part, from the clip itself
        private static Dictionary<string, Vector3> _landRoot; // top bones at the clip's end (sitting)
        private bool _landing;
        private float _landT;
        private int _landState;             // the animator state that played this clip (the fly-in): sent to its end after
        private float _landWay0;            // the way already done when the landing was picked up midway
        internal bool Landing { get { return _landing; } }
        // s from the start of the played part of the landing clip to its touch-down (at pace 1)
        internal static float LandLeadSeconds { get { return _landA >= 0f && _landTouch > _landA ? _landTouch - _landA : LandLead; } }
        internal float LandU { get { return Mathf.Clamp01(_landT); } }
        // the landing panel (LandPanel): held still, stepped by hand
        internal static bool LandHold;
        internal static float LandNudge;
        internal static void ReLand() { _landLooked = false; _landClip = null; _landA = _landB = -1f; }

        private AnimationClip LandClip()
        {
            if (_landLooked) return _landClip;
            if (_anim == null || _anim.runtimeAnimatorController == null) return null;
            _landLooked = true;
            foreach (AnimationClip c in _anim.runtimeAnimatorController.animationClips)
            {
                if (c == null) continue;
                string n = c.name.ToLowerInvariant();
                if (n.Contains("swoopin") || n.Contains("flyin") || (n.Contains("land") && !n.Contains("island"))) { _landClip = c; break; }
            }
            if (_landClip != null) { long t0 = Slow.Start(); FindLanding(_landClip); Slow.End(t0, "raven landing analysis"); }
            else Log("Landing clip: none found, our own settle is used");
            return _landClip;
        }

        // where in the clip it touches down (the body reaches its perch), and how its way there goes
        private void FindLanding(AnimationClip clip)
        {
            Transform hip = null;
            foreach (Transform t in _anim.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "hip" || n == "hips") { hip = t; break; }
                if (hip == null && n == "armature") hip = t;
            }
            Transform[] all = _anim.GetComponentsInChildren<Transform>(true);
            Quaternion[] kr = new Quaternion[all.Length]; Vector3[] kp = new Vector3[all.Length];
            for (int i = 0; i < all.Length; i++) { kr[i] = all[i].localRotation; kp[i] = all[i].localPosition; }
            const int N = 240;
            float len = clip.length, dt = len / (N - 1);
            Vector3[] p = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                clip.SampleAnimation(_anim.gameObject, i * dt);
                p[i] = hip != null ? _anim.transform.InverseTransformPoint(hip.position) : Vector3.zero;
            }
            // where the body's top bones sit when perched: the clip's end, then the game's idle (sitting) clip over it
            // (if the idle moves them, its values win; if not, they stay as the landing leaves them, as in the game)
            clip.SampleAnimation(_anim.gameObject, len);
            AnimationClip idle = null;
            foreach (AnimationClip c in _anim.runtimeAnimatorController.animationClips)
                if (c != null && c.name.ToLowerInvariant().Contains("idle") && c.isLooping) { idle = c; break; }
            if (idle != null) idle.SampleAnimation(_anim.gameObject, 0f);
            _landRoot = new Dictionary<string, Vector3>();
            string rootLog = "";
            foreach (Transform t in all)
            {
                string n = t.name.ToLowerInvariant();
                if ((n == "armature" || n == "hip" || n == "hips" || n == "root" || n == "body") && !_landRoot.ContainsKey(n)) { _landRoot[n] = t.localPosition; rootLog += " " + t.name + " " + t.localPosition.ToString("F3"); }
            }
            if (LogLandings) Log("Landing perch roots (" + (idle != null ? idle.name : "no idle clip") + "):" + rootLog);
            for (int i = 0; i < all.Length; i++) { all[i].localRotation = kr[i]; all[i].localPosition = kp[i]; }
            Vector3 end = p[N - 1];
            float most = 0f;
            for (int i = 0; i < N; i++) most = Mathf.Max(most, (p[i] - end).magnitude);
            int touch = N - 1;
            if (most > 1e-4f) for (int i = 0; i < N; i++) if ((p[i] - end).magnitude <= most * 0.04f) { touch = i; break; }
            float tt = touch * dt;
            _landTouch = tt;
            _landA = Mathf.Max(0f, tt - LandLead);
            _landB = Mathf.Min(len, tt + LandAfter);
            if (_landB - _landA < 0.2f) { _landA = Mathf.Max(0f, len - 1.5f); _landB = len; }
            _landWay = new float[33];
            float fromD = (Sample(p, dt, _landA) - end).magnitude;
            float best = 0f;
            for (int k = 0; k <= 32; k++)
            {
                float t = Mathf.Lerp(_landA, _landB, k / 32f);
                float w = fromD > 1e-4f ? Mathf.Clamp01(1f - (Sample(p, dt, t) - end).magnitude / fromD) : Mathf.SmoothStep(0f, 1f, k / 32f);
                best = Mathf.Max(best, w); // never back
                _landWay[k] = best;
            }
            _landWay[32] = 1f;
            Log("Landing clip: " + clip.name + " (" + len.ToString("0.00") + " s), touches down at " + tt.ToString("0.00") + " s, played " + _landA.ToString("0.00") + "–" + _landB.ToString("0.00") + " s");
        }

        private static Vector3 Sample(Vector3[] p, float dt, float t)
        {
            float x = t / dt;
            int a = Mathf.Clamp((int)x, 0, p.Length - 1), b = Mathf.Min(a + 1, p.Length - 1);
            return Vector3.Lerp(p[a], p[b], x - a);
        }

        private static float Way(float u)
        {
            if (_landWay == null) return u * u * (3f - 2f * u);
            float x = Mathf.Clamp01(u) * 32f;
            int a = Mathf.Min((int)x, 32), b = Mathf.Min(a + 1, 32);
            return Mathf.Lerp(_landWay[a], _landWay[b], x - a);
        }

        // from the pin, right after the flight hands it over; false: no clip, the pin settles it its own way
        internal int PendingState; // an animator state stopped midway (the fly-in): sent to its end after the next landing
        internal bool BeginLand() { return BeginLand(-1f); }

        // 'from': where in the clip to pick it up (the fly-in already plays this very clip), or before its part
        internal bool BeginLand(float from)
        {
            _land = NextLand ?? Landings.Pick(Player, Munin, Motor != null ? Motor.Char() : null); NextLand = null; // (given by the motor; picked here if it came another way)
            if (_anim == null || LandClip() == null || _landA < 0f) return false;
            _landing = true;
            _landRate = 1f;
            _landStyleRate = _land.ClipRate;
            _landT = from > _landA ? Mathf.Clamp01((from - _landA) / Mathf.Max(0.05f, _landB - _landA)) : 0f;
            _landState = 0;
            if (from >= 0f && _anim.isInitialized) _landState = _anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            else if (PendingState != 0) _landState = PendingState; // the fly-in stopped the game's clip midway (the motor flew it on)
            PendingState = 0;
            _landWay0 = Way(_landT); // picked up midway: the way so far is not jumped over
            bool animHad = _anim.enabled; // (the game's fly-in clip had it: the skeleton starts from its pose)
            _anim.enabled = false;
            if (_rootBones == null) HoldRoots();
            RavenSkeleton sk = Skel;
            if (sk != null) { if (animHad) sk.Active = false; sk.SetBase(LandD, BlendLand); sk.Begin(); } // out of the flight's pose into the clip's, eased
            // the body's top bones are held where they are when it sits (the idle clip's), all the way down: its travel
            // through the air is the pin's, and it ends at the very height it sits at afterwards
            if (_landRoot != null)
                for (int i = 0; i < _rootBones.Length; i++)
                {
                    Vector3 v;
                    if (_rootBones[i] != null && _landRoot.TryGetValue(_rootBones[i].name.ToLowerInvariant(), out v)) _rootPos[i] = v;
                }
            return true;
        }

        // ---- this landing's own way over the game's clip (Landings)
        internal LandStyle NextLand; // handed over by the motor as it lets the landing take the bird
        private LandStyle _land;
        internal string LandInfo { get { return _land != null ? _land.Describe() : "—"; } }

        // The landing template's pose (situation «Посадка», the «Шаблони» tab) over the clip, and this landing's tilt:
        // in at once, held until the touch, then let go as the clip folds the wings (its end, the sitting pose, is the
        // clip's own). The template's keys are a beat of their own here (Landings.LandBeatHz).
        private void LandWings(float u, float len)
        {
            if (_land == null) return;
            FlightTemplate t = _land.Pose;
            bool pose = t != null && t.UsePose;
            if (!pose && Mathf.Abs(_land.Tilt) < 0.5f) return;
            float uTouch = _landTouch > _landA ? Mathf.Clamp01((_landTouch - _landA) / len) : 0.3f;
            float fadeIn = Mathf.Clamp01(u / 0.12f), fadeOut = u <= uTouch ? 1f : 1f - Mathf.Clamp01((u - uTouch) / 0.35f);
            float env = fadeIn * fadeIn * (3f - 2f * fadeIn) * fadeOut * fadeOut * (3f - 2f * fadeOut);
            if (env <= 0.001f) return;
            if (_kl == null) WingKeys.Find(transform, out _kl, out _kr);
            float hz = Landings.LandBeatHz * (t != null && t.UseMotion ? t.Beat : 1f);
            float x = Mathf.Repeat((Time.time * hz + _land.Phase / (Mathf.PI * 2f)) * WingKeys.Keys, WingKeys.Keys);
            for (int i = 0; i < WingKeys.Bones; i++) { Vector3 p = pose ? t.At(x, i) * env : Vector3.zero; _addL[i] = p; _addR[i] = p; }
            _addL[0].y += _land.Tilt * env; _addR[0].y -= _land.Tilt * env; // one wing up, the other down
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            WingKeys.ApplyAdds(_kl, _kr, facing, _addL, _addR);
            if (pose)
            {
                if (_bodyBones == null) _bodyBones = WingKeys.FindBody(transform);
                for (int i = 0; i < WingKeys.BodyBones; i++) _bodyAdd[i] = t.At(x, 3 + i) * env;
                WingKeys.ApplyBody(_bodyBones, facing, _bodyAdd);
            }
        }

        // just down: its little settle comes a moment later, when the director lets it (nothing else going on)
        private int _settleKind;
        private float _settleAt;
        internal void SettleLater()
        {
            if (_land == null || Asleep) return;
            _settleKind = _land.Settle;
            _settleAt = Time.time + Random.Range(0.05f, 0.25f);
        }
        private void SettleStep()
        {
            if (_settleKind <= 0 || Time.time < _settleAt) return;
            int kind = _settleKind;
            _settleKind = 0;
            if (Droopy || !AnimDirector.MayIdle(this, AnimDirector.Idle.Settle)) return; // busy: it just sits
            switch (kind)
            {
                case 1: _hopAt = Time.time; break;
                case 2: Flap(Munin ? Random.Range(0.3f, 0.5f) : Random.Range(0.25f, 0.4f)); break;
                case 3: _stepFrom = _stepNow; _stepTo = Random.Range(-StepSize, StepSize); _stepAt = Time.time; break;
                case 4: _hopAt = Time.time; Flap(Random.Range(0.3f, 0.45f)); break;
            }
            AnimDirector.DidIdle(this, AnimDirector.Idle.Settle, 0.5f);
        }

        // ---- the take-off (RavenMotor): the crouch holds the clip at a stroke's top, its wings unfolding into it
        private float _flyHoldFrom, _flyHoldUntil = -1f, _flyCrouch = 0.2f, _flyUnfold;
        internal void TakeoffHold(float hold, float crouch, float unfold)
        {
            _flyHoldFrom = Time.time; _flyHoldUntil = Time.time + hold; _flyCrouch = Mathf.Max(0.05f, crouch); _flyUnfold = unfold;
            float[] tops = StrokeTops;
            if (tops != null && tops.Length > 0) _flyT = Mathf.Repeat(tops[0] - 0.03f, 1f); // up at the top: the first move is the push down
            if (_skel != null && _skel.FadeW >= 0f) _skel.Stretch(hold + BlendTakeOff, unfold); // (the change into the flight waits for the spring)
        }
        private bool FlyHeld { get { return Time.time < _flyHoldUntil; } }

        // crouched to spring off: the head already looks the way it will go (one jump there, then held), a touch up
        internal static float LookOffMaxYaw = 80f, LookOffPitch = -8f;
        private void LookOff()
        {
            if (_head == null || Motor == null) return;
            Vector3 way = Vector3.ProjectOnPlane(Motor.LaunchVel, Vector3.up);
            Vector3 fwd = Vector3.ProjectOnPlane((transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation))) * Vector3.forward, Vector3.up);
            if (way.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(fwd, way, Vector3.up), -LookOffMaxYaw, LookOffMaxYaw);
            Quaternion want = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(LookOffPitch, right);
            _head.rotation = _sac.Step(want, Time.deltaTime, Munin, Nerves, 1.3f) * _head.rotation;
        }

        // the fly-in (the game's clip on the animator): the body's top bones put where they are when it sits, so
        // the clip does not carry the bird far off the way it is flown
        private Transform[] _perchRoots;
        internal void HoldPerchRoots()
        {
            if (_anim == null || LandClip() == null || _landRoot == null) return;
            if (_perchRoots == null)
            {
                List<Transform> found = new List<Transform>();
                foreach (Transform t in _anim.GetComponentsInChildren<Transform>(true)) if (_landRoot.ContainsKey(t.name.ToLowerInvariant())) found.Add(t);
                _perchRoots = found.ToArray();
            }
            foreach (Transform t in _perchRoots) if (t != null) t.localPosition = _landRoot[t.name.ToLowerInvariant()];
        }

        // the landing's base for the skeleton: the clip at its moment, the body's top bones held where it sits
        private float _landU;
        private void LandBase()
        {
            if (_landClip == null) return;
            _landClip.SampleAnimation(_anim.gameObject, _landU);
            if (_rootBones != null) for (int i = 0; i < _rootBones.Length; i++) if (_rootBones[i] != null) _rootBones[i].localPosition = _rootPos[i];
        }

        // one frame of the landing: the clip's pose, and how far along the way it is (0..1); false once done
        internal bool StepLand(out float progress)
        {
            progress = 1f;
            if (!_landing || _landClip == null) return false;
            float len = Mathf.Max(0.05f, _landB - _landA);
            float clipNow = _landA + Mathf.Clamp01(_landT) * len;
            float rate = clipNow < _landTouch ? _landRate : 1f;
            if (!LandHold) _landT += Time.deltaTime * LandSpeed * rate * _landStyleRate / len;
            if (LandNudge != 0f) { _landT = Mathf.Clamp(_landT + LandNudge, 0f, 0.999f); LandNudge = 0f; }
            float u = Mathf.Clamp01(_landT);
            _landU = _landA + u * len;
            RavenSkeleton sk = Skel;
            if (sk != null && sk.BaseIs(LandD)) sk.Frame();
            else { Transform root = _anim.transform; Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale; LandBase(); root.localPosition = lp; root.localRotation = lr; root.localScale = ls; }
            LandWings(u, len);
            progress = _landWay0 < 0.999f ? Mathf.Clamp01((Way(u) - _landWay0) / (1f - _landWay0)) : 1f;
            if (u < 1f || LandHold) return true;
            _landing = false;
            SettleLater();
            if (sk != null) { sk.SetBase(null, 0.2f); _landState = 0; } // the clip's last frame eases into the sitting
            else _anim.enabled = true;
            _look = _lookMid = Quaternion.identity; _wrote = false; _spineWrote = false; // the head starts straight, turns from there
            _sac.Reset(Quaternion.identity);
            // down: it settles in (wings folded once more as a wave, a flick of the tail), after the landing's own hop, beat or step
            if (Random.value < SettleChance) Act(SitTemplates.SettleName, _land != null && _land.Settle > 0 ? SettleAfter : 0.15f);
            _perchW = 0f;                                                   // the bent leg bends in over a moment
            // the fly-in's own state would go on where it was stopped and play the landing a second time: to its end
            if (_landState != 0) { _anim.Play(_landState, 0, 1f); _landState = 0; }
            if (_rootBones != null) { for (int i = 0; i < _rootBones.Length; i++) if (_rootBones[i] != null) _rootBones[i].localPosition = _rootPos[i]; _rootBones = null; }
            return false;
        }

        // When the flight's clip hands over to the game's perch (landing) or back (take-off), the skeleton would
        // jump in one frame: wings spread → folded, the levelled body → its sitting tilt. Instead the pose it had is
        // kept and eased into the new one over a moment.
        internal static float BlendLand = 0.35f, BlendTakeOff = 0.25f;
        internal float BlendW { get { return _skel != null ? _skel.FadeW : -1f; } } // how far the skeleton's change of base has come (-1: none)
        internal static bool LogLandings = false; // what each landing does (log); the landing panel's recorder does it better

        // the raven's skeleton (RavenSkeleton): sitting, flying, landing; made once its clips are known
        private RavenSkeleton Skel { get { if (_skel == null && _anim != null && SitClips.Ready(_anim)) _skel = new RavenSkeleton(_anim, Munin); return _skel; } }
        private System.Action _flyD, _landD; // the flight's and the landing's bases (kept: the skeleton tells them apart)
        private System.Action FlyD { get { if (_flyD == null) _flyD = FlyBase; return _flyD; } }
        private System.Action LandD { get { if (_landD == null) _landD = LandBase; return _landD; } }
        // FlightBody drives these: wing beats × FlapRate, Flare 0..1 = braking to land (wings forward, legs out)
        internal float FlapRate = 1f;
        internal float Flare;

        // one line of how it flies now (a bird whose wings stood still in the air: which step did not run)
        internal string FlyState()
        {
            Player me = Player.m_localPlayer;
            return "vanillaFly " + _vanillaFly + ", still " + Still + ", soaring " + Soaring + ", flap " + FlapRate.ToString("0.00")
                + ", fly clip " + (FlyClip(_anim) != null) + ", animator " + (_anim != null ? (_anim.enabled ? "on" : "off") : "none")
                + ", skeleton " + (Skel != null) + ", takeoff " + _takingOff + ", held " + FlyHeld + ", far " + (Player != null && FarFlight())
                + ", stroke " + _flyT.ToString("0.00") + ", life of " + (Player != null ? (ReferenceEquals(Player, me) ? "me" : Player.GetPlayerName()) : "nobody");
        }
        internal static float FlareOut = 30f, FlareUp = 15f;
        private static int _loopBeats = 1;
        internal static float FlyClipSpeed = 1f;
        internal static string FlyClipName = "HuginFlyAway"; // the game's raven has no looping flight: its fly-away clip, the middle of it
        internal static float FlyFrom = 0.25f, FlyTo = 0.8f; // the part of that clip played round and round (shares of its length)
        private Transform[] _bones; private Quaternion[] _held; // for melting the end of the loop into its start
        private Transform[] _rootBones; private Vector3[] _rootPos; // the clip may carry the body up and away: held in place

        private static AnimationClip _flyClip;
        private static bool _flyLooked;

        // the flying clip of the game's raven, found once (all its clips go to the log, to choose another by name)
        private static AnimationClip FlyClip(Animator anim)
        {
            if (_flyLooked) return _flyClip;
            if (anim == null || anim.runtimeAnimatorController == null) return null;
            _flyLooked = true;
            AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Raven animation clips:");
            AnimationClip named = null, loopFly = null, anyFly = null;
            foreach (AnimationClip c in clips)
            {
                if (c == null) continue;
                sb.Append(' ').Append(c.name).Append(c.isLooping ? "(loop " : "(").Append(c.length.ToString("0.00")).Append("s)");
                string n = c.name.ToLowerInvariant();
                if (FlyClipName.Length > 0 && c.name == FlyClipName) named = c;
                bool fly = n.Contains("fly") || n.Contains("flap") || n.Contains("flight");
                bool landing = n.Contains("land") || n.Contains("flyin") || n.Contains("swoop");
                if (fly && c.isLooping && !landing && loopFly == null) loopFly = c;
                if (fly && !landing && anyFly == null) anyFly = c;
            }
            _flyClip = named != null ? named : loopFly != null ? loopFly : anyFly;
            sb.Append(". Flying with: ").Append(_flyClip != null ? _flyClip.name : "none (our own wing beats)");
            if (Plugin.Instance != null) Plugin.Instance.Log(sb.ToString());
            return _flyClip;
        }

        // The game's fly-away clip is not one flapping loop: it takes off, beats, glides, turns. It is read once: the
        // wing beat's length (the shift after which all wing bones come back to the same pose), then as many whole
        // beats as fit in the flying part, starting where both ends are most alike. That run plays round and round,
        // and its last moment melts into the one before its start.
        internal static bool FlyAuto = true;
        internal static float FlyLevel = 0.85f; // how much of the clip's nose-up is taken out (1 = flat, 0 = as the clip has it)
        internal static float BeatHz = 2.1f;    // the found loop's wing beats a second (the hover bob follows it)
        private static Vector3 _clipBody;       // where the head points from the hip in the loop, on average (the model's own axes)
        private static bool _haveBody;
        internal static bool HoverVanilla = true; // hanging in the air during an emote: the same clip as in flight
        private static float _loopA = -1f, _loopB = -1f;
        private static bool _loopLooked;

        private void FindLoop(AnimationClip clip)
        {
            _loopLooked = true;
            // every wing bone, both sides: the whole wing's pose is compared, not one bone's swing
            List<Transform> wings = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().StartsWith("wing")) wings.Add(t);
            if (wings.Count == 0 || clip.length <= 0.1f) { Log("Flight loop: no wing bones found, the set slice is played"); return; }
            const int N = 400;
            float dt = clip.length / (N - 1);
            Quaternion[,] q = new Quaternion[N, wings.Count];
            Quaternion[] keep = new Quaternion[wings.Count];
            for (int w = 0; w < wings.Count; w++) keep[w] = wings[w].localRotation;
            for (int i = 0; i < N; i++)
            {
                clip.SampleAnimation(_anim.gameObject, i * dt);
                for (int w = 0; w < wings.Count; w++) q[i, w] = wings[w].localRotation;
            }
            for (int w = 0; w < wings.Count; w++) wings[w].localRotation = keep[w];

            // the part where it flies (not the take-off at the start, not the vanishing at the end)
            int lo = Mathf.RoundToInt(N * 0.12f), hi = Mathf.RoundToInt(N * 0.92f);
            // the beat: the shift after which the wings come back to the same pose, on average over the flight
            int minLag = Mathf.Max(2, Mathf.RoundToInt(0.2f / dt)), maxLag = Mathf.Min((hi - lo) / 2, Mathf.RoundToInt(1.6f / dt));
            float bestAvg = float.MaxValue; int period = -1;
            float[] avg = new float[maxLag + 2];
            for (int L = minLag; L <= maxLag; L++)
            {
                float sum = 0f; int n = 0;
                for (int i = lo; i + L <= hi; i += 2) { sum += Diff(q, i, i + L, wings.Count); n++; }
                avg[L] = n > 0 ? sum / n : float.MaxValue;
            }
            // the first deep dip (a true beat), not a later multiple of it: the lowest within 15% of the very lowest
            float lowest = float.MaxValue;
            for (int L = minLag; L <= maxLag; L++) lowest = Mathf.Min(lowest, avg[L]);
            for (int L = minLag + 1; L < maxLag; L++)
                if (avg[L] <= avg[L - 1] && avg[L] <= avg[L + 1] && avg[L] <= lowest * 1.15f + 0.5f) { period = L; bestAvg = avg[L]; break; }
            if (period < 0) { Log("Flight loop: no wing beat found in " + clip.name + ", the set slice is played"); return; }

            // as many whole beats as fit, starting where the seam (the pose at both ends) is the closest
            int k = Mathf.Clamp((hi - lo) / period, 1, 6);
            float bestSeam = float.MaxValue; int start = -1;
            for (int i = lo; i + k * period <= hi; i++)
            {
                float seam = Diff(q, i, i + k * period, wings.Count);
                if (seam < bestSeam) { bestSeam = seam; start = i; }
            }
            if (start < 0) { Log("Flight loop: no room for whole beats in " + clip.name + ", the set slice is played"); return; }
            _loopA = start * dt;
            _loopB = (start + k * period) * dt;
            BeatHz = 1f / Mathf.Max(0.1f, period * dt);
            _loopBeats = k;
            MeasureBody(clip, _loopA, _loopB);
            FindStroke(clip, _loopA, _loopB);
            Log("Flight loop: " + clip.name + " " + _loopA.ToString("0.00") + "–" + _loopB.ToString("0.00") + " s, " + k + " beats of " + (period * dt).ToString("0.00") + " s, seam " + (bestSeam / wings.Count).ToString("0.0") + "° a bone (" + wings.Count + " wing bones)");
        }

        // The wingtip's height over the whole flying loop, in the body's own frame (hip → neck forward, across the
        // shoulders sideways: the clip holds the body steeply nose-up, so the model's up is not the bird's up). From it:
        // how many true strokes (top → bottom → top) the loop has, and where in one stroke the top and bottom are.
        internal static int Strokes = -1;
        internal static float[] StrokeTops, StrokeBottoms; // each stroke's top and bottom, as points of the loop (0..1)
        internal static int StrokeCount { get { return Strokes > 0 ? Strokes : Mathf.Max(1, _loopBeats); } }
        private void FindStroke(AnimationClip clip, float a, float b)
        {
            StrokeTops = null; StrokeBottoms = null; Strokes = -1;
            Transform tip = null, hip = null, neck = null, wl = null, wr = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "wing3feathers3.l_end" || (tip == null && n == "wing3.l")) tip = t;
                if (hip == null && (n == "hip" || n == "hips")) hip = t;
                if (neck == null && n == "neck") neck = t;
                if (n == "wing1.l") wl = t;
                if (n == "wing1.r") wr = t;
            }
            if (tip == null || hip == null || neck == null || wl == null || wr == null || b <= a) { Log("Wing stroke: bones missing, even halves"); return; }
            const int M = 240;
            float[] h = new float[M];
            for (int i = 0; i < M; i++) h[i] = TipUp(clip, a + (b - a) * i / M, tip, hip, neck, wl, wr);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < M; i += 2) sb.Append(Mathf.RoundToInt(h[i] * 100f)).Append(' ');
            Log("Wing stroke curve over the loop (" + (b - a).ToString("0.00") + " s, wingtip up in % of the wing, every " + ((b - a) * 2f / M * 1000f).ToString("0") + " ms): " + sb);
            // every stroke on its own: a top is the highest point within ±0.12 s (and the wing well up), the bottom
            // the lowest between two tops. The game's strokes are not all alike (slower first, quicker later).
            int W = Mathf.Max(2, Mathf.RoundToInt(0.12f / ((b - a) / M)));
            List<int> tops = new List<int>();
            for (int i = 0; i < M; i++)
            {
                if (h[i] < 0.2f) continue;
                bool peak = true;
                for (int d = 1; d <= W && peak; d++) if (h[(i - d + M) % M] >= h[i] || h[(i + d) % M] > h[i]) peak = false;
                if (peak) tops.Add(i);
            }
            if (tops.Count == 0) { StrokeTops = null; StrokeBottoms = null; Strokes = -1; Log("Wing stroke: no tops found, the beat as measured"); return; }
            StrokeTops = new float[tops.Count]; StrokeBottoms = new float[tops.Count];
            System.Text.StringBuilder sl = new System.Text.StringBuilder();
            for (int k = 0; k < tops.Count; k++)
            {
                int t0 = tops[k], t1 = k + 1 < tops.Count ? tops[k + 1] : tops[0] + M;
                int low = t0; float lv = float.MaxValue;
                for (int i = t0; i < t1; i++) if (h[i % M] < lv) { lv = h[i % M]; low = i; }
                StrokeTops[k] = t0 / (float)M; StrokeBottoms[k] = (low % M) / (float)M;
                sl.Append(" ").Append(k + 1).Append(": ").Append(((t1 - t0) * (b - a) / M).ToString("0.00")).Append(" s, down ").Append(Mathf.RoundToInt((low - t0) * 100f / (t1 - t0))).Append("%;");
            }
            Strokes = tops.Count;
            Log("Wing stroke: " + Strokes + " strokes in the loop," + sl);
        }

        // −1..1: how far up the wing points from its shoulder, in the body's frame, at this moment of the clip
        private float TipUp(AnimationClip clip, float time, Transform tip, Transform hip, Transform neck, Transform wl, Transform wr)
        {
            clip.SampleAnimation(_anim.gameObject, time);
            Vector3 fwd = (neck.position - hip.position).normalized;
            Vector3 right = Vector3.ProjectOnPlane(wr.position - wl.position, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right).normalized;
            if (Vector3.Dot(up, transform.up) < 0f) up = -up;
            Vector3 w = tip.position - wl.position;
            return Vector3.Dot(w, up) / Mathf.Max(0.0001f, w.magnitude);
        }

        // The fly-away clip carries the body nose-up (the raven climbs away). The head's direction from the hip is
        // read over the loop, and in flight the skeleton is turned back by most of that, so the raven flies level.
        private void MeasureBody(AnimationClip clip, float a, float b)
        {
            Transform hip = null, neck = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (hip == null && (n == "hip" || n == "hips" || n == "spine1")) hip = t;
                if (neck == null && n == "neck") neck = t;
            }
            if (hip == null || neck == null) { Log("Flight level: no hip or neck bone, flown as the clip has it"); return; }
            Vector3 sum = Vector3.zero;
            for (int i = 0; i <= 20; i++)
            {
                clip.SampleAnimation(_anim.gameObject, Mathf.Lerp(a, b, i / 20f));
                sum += transform.InverseTransformDirection(neck.position - hip.position).normalized;
            }
            _clipBody = sum.normalized;
            _haveBody = true;
            float up = Mathf.Asin(Mathf.Clamp(_clipBody.y, -1f, 1f)) * Mathf.Rad2Deg;
            Log("Flight level: the clip holds the body " + up.ToString("0") + "° up, " + Mathf.RoundToInt(FlyLevel * 100f) + "% of it taken out");
        }

        // after each sampled frame: the skeleton turned back towards level
        private void LevelBody()
        {
            if (!_haveBody || FlyLevel <= 0f || _rootBones == null || _rootBones.Length == 0) return;
            Vector3 flat = new Vector3(_clipBody.x, 0f, _clipBody.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            Quaternion fix = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(_clipBody, flat.normalized), Mathf.Clamp01(FlyLevel));
            Transform top = _rootBones[0];
            foreach (Transform t in _rootBones) if (t != null && t.name.ToLowerInvariant() == "armature") { top = t; break; }
            if (top == null) return;
            // the clip may not write this bone: then it still holds last frame's turn, and the turn must not add up
            if (_levelTop == top && Quaternion.Angle(top.localRotation, _levelWrote) < 0.01f) { top.localRotation = _levelBase; top.localPosition = _levelBasePos; }
            else { _levelTop = top; _levelBase = top.localRotation; _levelBasePos = top.localPosition; }
            Quaternion world = transform.rotation * fix * Quaternion.Inverse(transform.rotation);
            Vector3 pivot = transform.position;
            top.position = pivot + world * (top.position - pivot);
            top.rotation = world * top.rotation;
            _levelWrote = top.localRotation;
        }

        private Transform _levelTop;
        private Quaternion _levelBase, _levelWrote;
        private Vector3 _levelBasePos;

        // landing: the skeleton as it was, so no tilt stays on the shoulder
        private void Unlevel()
        {
            if (_levelTop != null) { _levelTop.localRotation = _levelBase; _levelTop.localPosition = _levelBasePos; }
            _levelTop = null;
        }

        // how far apart two moments of the clip are: the wing bones' turns added up
        private static float Diff(Quaternion[,] q, int i, int j, int bones)
        {
            float d = 0f;
            for (int w = 0; w < bones; w++) d += Quaternion.Angle(q[i, w], q[j, w]);
            return d;
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }

        // Hanging in the air (a dance, a strike's hover): the head as a bird's. It holds a look a moment, then turns
        // quickly to the next: at you (you are dancing), at an enemy close by, or just about. Munin is restless
        // (shorter holds, faster turns), Hugin calmer.
        private float _glanceNext;
        private int _glanceAt;            // 0 about, 1 at the owner, 2 at the enemy
        private float _gYaw, _gPitch;
        private Quaternion _gLook = Quaternion.identity;

        private void Nod()
        {
            if (_head == null) return;
            if (Time.time >= _nextSearch) { _nextSearch = Time.time + 0.4f; Search(); }
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 fwd = facing * Vector3.forward, up = facing * Vector3.up, right = facing * Vector3.right;
            if (Time.time >= _glanceNext)
            {
                _glanceNext = Time.time + Random.Range(1.5f, 3.5f) * (Munin ? 0.65f : 1f);
                float r = Random.value;
                _glanceAt = r < 0.4f ? 1 : (r < 0.65f && _foe != null && !_foe.IsDead()) ? 2 : 0;
                _gYaw = Random.Range(-70f, 70f); _gPitch = Random.Range(-10f, 30f);
            }
            float yaw = _gYaw, pitch = _gPitch;
            Vector3 at = Vector3.zero;
            bool has = false;
            if (_glanceAt == 1 && Player != null) { at = Player.GetHeadPoint(); has = true; }
            else if (_glanceAt == 2 && _foe != null && !_foe.IsDead()) { at = Body.Eye(_foe); has = true; }
            if (has)
            {
                Vector3 dir = at - _head.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Vector3 flat = Vector3.ProjectOnPlane(dir, up);
                    yaw = flat.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(fwd, flat, up) : 0f;
                    pitch = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir.normalized, up), -1f, 1f)) * Mathf.Rad2Deg; // positive: down
                }
            }
            yaw = Mathf.Clamp(yaw, -MaxYaw, MaxYaw);
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            Quaternion want = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right);
            _gLook = Quaternion.RotateTowards(_gLook, want, (Munin ? 520f : 380f) * Time.deltaTime); // a quick turn, then held
            // the clip normally sets the head each frame; if it did not, start again from where it was, or the turn adds up
            if (_nodSet && Quaternion.Angle(_head.localRotation, _nodWrote) < 0.01f) _head.localRotation = _nodBase;
            else _nodBase = _head.localRotation;
            _nodSet = true;
            _head.rotation = _gLook * _head.rotation;
            _nodWrote = _head.localRotation;
        }

        private bool _nodSet;
        private Quaternion _nodBase, _nodWrote;

        // the body's top bones as they sit now: the fly-away clip lifts them up and away, our flight does the moving
        private void HoldRoots()
        {
            List<Transform> bones = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "armature" || n == "hip" || n == "hips" || n == "root" || n == "body") bones.Add(t);
            }
            _rootBones = bones.ToArray();
            _rootPos = new Vector3[_rootBones.Length];
            for (int i = 0; i < _rootBones.Length; i++) _rootPos[i] = _rootBones[i].localPosition;
        }

        // one frame of the flying clip; the model's own place and turn stay as the flight set them
        private bool SampleFly()
        {
            AnimationClip clip = FlyClip(_anim);
            if (clip == null || _anim == null || clip.length <= 0f) return false;
            if (_anim.enabled) _anim.enabled = false;
            if (_rootBones == null) HoldRoots();
            if (!_loopLooked) { long t0 = Slow.Start(); FindLoop(clip); Slow.End(t0, "raven flight loop analysis"); }
            float from, to;
            if (_loopA >= 0f && FlyAuto) { from = _loopA / clip.length; to = _loopB / clip.length; }
            else
            {
                from = Mathf.Clamp01(Mathf.Min(FlyFrom, FlyTo)); to = Mathf.Clamp01(Mathf.Max(FlyFrom, FlyTo));
                if (to - from < 0.02f) { from = 0f; to = 1f; }
            }
            float span = (to - from) * clip.length;
            float adv = Dt * FlyClipSpeed / Mathf.Max(0.05f, span);
            _flyClock += Dt; // its own time in flight (a slowed picture slows the legs and the looks too)
            if (!FlyHeld) _flyT += adv * Mathf.Max(0.05f, FlapRate); // (the take-off's crouch: held at a stroke's top)
            _flyX = (from + (_flyT % 1f) * (to - from)) * clip.length;
            _flySpan = span; _flyA0 = from * clip.length;
            RavenSkeleton sk = Skel;
            if (sk != null)
            {
                if (!sk.BaseIs(FlyD)) { sk.SetBase(FlyD, BlendTakeOff); sk.Begin(); } // (flying from its first moment: a stand-in, the test picture)
                sk.Frame();
            }
            else { Transform root = _anim.transform; Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale; FlyBase(); root.localPosition = lp; root.localRotation = lr; root.localScale = ls; }
            return true;
        }

        // the flight's base for the skeleton: the fly clip's loop at its moment (its last moment melting into the one
        // just before its start: at the wrap both are the same), the body's top bones held in place, the body levelled,
        // the wing keys, the adaptive strokes and the flight template on it (a change of base eases into all of that)
        private float _flyX, _flySpan, _flyA0;
        private void FlyBase()
        {
            AnimationClip clip = _flyClip;
            if (clip == null) return;
            float x = _flyX, span = _flySpan, a0 = _flyA0, fade = Mathf.Min(0.18f, span * 0.25f);
            if (x > a0 + span - fade && a0 - fade >= 0f)
            {
                if (_bones == null) { _bones = _anim.GetComponentsInChildren<Transform>(true); _held = new Quaternion[_bones.Length]; }
                clip.SampleAnimation(_anim.gameObject, x - span);
                for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _held[i] = _bones[i].localRotation; // a transform gone since (not a bone) is skipped
                clip.SampleAnimation(_anim.gameObject, x);
                float f = (x - (a0 + span - fade)) / fade;
                f = f * f * (3f - 2f * f);
                for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _held[i], f);
            }
            else clip.SampleAnimation(_anim.gameObject, x);
            if (_rootBones != null) for (int i = 0; i < _rootBones.Length; i++) if (_rootBones[i] != null) _rootBones[i].localPosition = _rootPos[i];
            LevelBody();
            if (WingKeys.Active || WingMods.On || Mix.Active || Flight.Dive > 0.001f) KeyWings(); // dive shaping also applies without the optional beat layers
        }

        // ---------------------------------------------------------------- the wing beat corrected by hand (WingKeys)
        private Transform[] _kl, _kr;
        internal WingMods.State Flight;                       // what the flight does now (FlightBody sets it)
        private readonly WingMods.Runner _mods = new WingMods.Runner();
        private readonly Vector3[] _addL = new Vector3[WingKeys.Bones], _addR = new Vector3[WingKeys.Bones];

        // on top of the clip's wings, at the same point in the continuously advancing beat
        private void KeyWings()
        {
            if (_kl == null) WingKeys.Find(transform, out _kl, out _kr);
            float u = _flyT - Mathf.Floor(_flyT), x = WingKeys.KeyX(u);
            bool poseOnlyDive = Motor != null && Motor.DivePoseTest;
            if (poseOnlyDive)
            {
                for (int i = 0; i < WingKeys.Bones; i++) { _addL[i] = Vector3.zero; _addR[i] = Vector3.zero; }
            }
            else if (_loopA >= 0f && FlyAuto) _mods.Step(u, x, Flight, Munin, Dt, _addL, _addR);
            else
            {
                for (int i = 0; i < WingKeys.Bones; i++) { _addL[i] = Vector3.zero; _addR[i] = Vector3.zero; }
            }
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            // the flight template: a new pick comes in at a stroke's top (x wraps round), or after a second without one
            if (Mix.Waiting && Mix.W >= 1f && ((x < _mixX - WingKeys.Half) || Mix.Due)) Mix.Commit();
            _mixX = x;
            Mix.Tick(Dt);
            bool tpl = Mix.From != null || Mix.To != null;
            if (tpl) for (int i = 0; i < WingKeys.Bones; i++) { Vector3 p = Mix.PoseAt(x, i); _addL[i] += p; _addR[i] += p; }
            WingKeys.Apply(_kl, _kr, facing, x, _addL, _addR, poseOnlyDive ? 0f : Flight.Dive);
            // the body, head and tail: the base beat's turns (the wing panel) and the template's on top
            if (WingKeys.Active || tpl)
            {
                if (_bodyBones == null) _bodyBones = WingKeys.FindBody(transform);
                for (int i = 0; i < WingKeys.BodyBones; i++) _bodyAdd[i] = (WingKeys.Active ? WingKeys.BodyAt(x, i) : Vector3.zero) + (tpl ? Mix.PoseAt(x, 3 + i) : Vector3.zero);
                WingKeys.ApplyBody(_bodyBones, facing, _bodyAdd);
            }
        }

        private float _mixX;
        private Transform[] _bodyBones;
        private readonly Vector3[] _bodyAdd = new Vector3[WingKeys.BodyBones];

        // for the wing panel (raven wingui): the game's beat as measured here
        internal static bool BeatKnown { get { return _loopA >= 0f && _loopB > _loopA; } }
        internal static float BeatStart { get { return _loopA; } }
        internal static float LoopLength { get { return _loopB - _loopA; } }
        internal static AnimationClip FlyClipOf(Animator a) { return FlyClip(a); }
        internal static Quaternion LevelFix
        {
            get
            {
                if (!_haveBody || FlyLevel <= 0f) return Quaternion.identity;
                Vector3 flat = new Vector3(_clipBody.x, 0f, _clipBody.z);
                if (flat.sqrMagnitude < 0.0001f) return Quaternion.identity;
                return Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(_clipBody, flat.normalized), Mathf.Clamp01(FlyLevel));
            }
        }
        // measured on a sitting raven (the panel opened before any flight): its pose is put back after, or it would
        // show the clip's last sampled moment for a frame
        internal void EnsureLoop()
        {
            if (_anim == null || _loopLooked) return;
            AnimationClip c = FlyClip(_anim);
            if (c == null) return;
            Transform[] all = _anim.GetComponentsInChildren<Transform>(true);
            Vector3[] p = new Vector3[all.Length]; Quaternion[] r = new Quaternion[all.Length]; Vector3[] sc = new Vector3[all.Length];
            for (int i = 0; i < all.Length; i++) { p[i] = all[i].localPosition; r[i] = all[i].localRotation; sc[i] = all[i].localScale; }
            FindLoop(c);
            for (int i = 0; i < all.Length; i++) if (all[i] != null) { all[i].localPosition = p[i]; all[i].localRotation = r[i]; all[i].localScale = sc[i]; }
        }

        internal bool Still; // in flight (dome, strike): the game's own idle moves (preening, looking round) are held
        internal bool Soaring; // in flight, when the game's flying clip is not found: wings spread, now and then a beat
        internal bool Hovering; // in the air above the shoulder during an emote (ShoulderPin sets it): wings beating
        internal void PrepareTakeoff()
        {
            _takeoffStarted = Time.time;
            _takingOff = true;
            if (_takeoffRig != null) _takeoffRig.RecaptureBase();
            _awakeUntil = Time.time + 60f;
        }
        internal void FinishTakeoff() { _takingOff = false; _takeoffStarted = -1f; }
        internal void ApplyTakeoffPose(float phase, bool resetToBase, float weight = 1f, bool force = false)
        {
            if (!force && !TakeoffKeys.Active) return;
            if (_takeoffRig == null) _takeoffRig = new TakeoffKeys.Rig(transform);
            _takeoffRig.Apply(phase, weight, resetToBase);
        }
        internal bool HasTakeoffBone(int index)
        {
            if (_takeoffRig == null) _takeoffRig = new TakeoffKeys.Rig(transform);
            return _takeoffRig.HasBone(index);
        }

        internal bool Asleep { get { return Level <= 0 && Time.time >= _awakeUntil; } }
        internal Vector3 HeadPoint { get { return _head != null ? _head.position : transform.position; } } // where its head is (the other raven looks at it)
        internal bool HasClip(SitClip k) { return _skel != null && _skel.Has(k); }
        internal float ClipLength(int clip) { return _skel != null && clip >= 0 ? _skel.Length((SitClip)clip) : 0f; }

        // an act of the sitting director begins on this bird, 'age' s in (another player's raven: picked up where it is)
        internal void SitPlay(SitRun r, float age)
        {
            SitTemplate t = r.T;
            if (_skel != null && r.Clip >= 0) _skel.Play((SitClip)r.Clip, SitSystem.FadeIn, age);
            if (t.Shake) _shakeAt = Time.time - age;
            if (t.Step) { _stepFrom = _stepNow; _stepTo = (new System.Random(r.Seed).NextDouble() < 0.5 ? -1f : 1f) * StepSize * (0.5f + 0.5f * r.Amp); _stepAt = Time.time - age; }
            if (age > 0.25f) return; // the moment's own things (a knock, beats, a caw) only when it is now
            if (t.Flap > 0f) Flap(t.Flap * r.Amp);
            if (t.KickPitch != 0f || t.KickRoll != 0f || t.KickDip != 0f) _sitBody.Kick(t.KickPitch * r.Amp, t.KickRoll * r.Sign * r.Amp, t.KickDip);
            if (r.Caw) Caw(r.CawPitch, t.CawVolume);
        }

        // ---- a sitting template's pose over everything (SitTemplates): the wings (both mirrored, the inner one's extra,
        // a tremble), the body, the neck, the head and the tail on the flight templates' axes; the beak and the crouch
        // have their own places (Clack, PerchLegs)
        private readonly SitPoser _sitPoser = new SitPoser();
        internal float SitRow(int bone) { SitRun r = _sitDir.Run; float w = _sitDir.ActW; return r != null && w > 0f ? r.Pose(_sitDir.ActT, bone).x * w : 0f; }
        private void SitPose()
        {
            SitRun r = _sitDir.Run;
            float w = _sitDir.ActW;
            if (r == null || w <= 0.001f) return;
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            // the inner wing: the one by your head
            bool leftInner = true;
            if (_left != null && _right != null && _left.A.T != null && _right.A.T != null && Player != null) { Vector3 head = Player.GetHeadPoint(); leftInner = (_left.A.T.position - head).sqrMagnitude <= (_right.A.T.position - head).sqrMagnitude; }
            _sitPoser.Apply(transform, r, _sitDir.ActT, w, facing, leftInner, !Asleep && !Ducked);
        }

        internal static float FarFlightMetres = 80f;
        private bool FarFlight()
        {
            Player me = Player.m_localPlayer;
            return me != null && Player != null && !ReferenceEquals(Player, me) && (transform.position - me.transform.position).sqrMagnitude > FarFlightMetres * FarFlightMetres;
        }

        // starving (the owner's net flag, read once a second): head down, back hunched, slow, like the unwoken
        // raven but a little less; a word from it lifts the head for a moment
        internal static float GloomyDegrees = 30f, GloomyHunch = 12f;
        private bool _gloomy;
        private float _gloomyAt;
        private bool Gloomy
        {
            get
            {
                if (Time.time >= _gloomyAt) { _gloomyAt = Time.time + 1f; _gloomy = Player != null && (NetFlag.Get(Player, Munin ? Trust.StarveKeyM : Trust.StarveKeyH) || NetFlag.Get(Player, Munin ? Quirks.GloomKeyM : Quirks.GloomKeyH)); } // starving, or a quirk (sulking, cold, afraid)
                return _gloomy && Level > 0 && Time.time >= _awakeUntil;
            }
        }
        internal bool Droopy { get { return Asleep || Gloomy; } }
        // Munin's wings carry you: Hugin ducks — head down as low as it goes, wings folded at his sides, no beats
        internal static float DuckDegrees = 58f, DuckHunch = 12f;
        internal static float HunchEase = 30f; // ° a second the extra hunch (fury, duck) comes and goes
        private float _hunchExtra, _softUntil;
        private bool _wingsWas;
        private bool _wingsOn, _furyOn;
        private bool Ducked { get { return !Munin && !Asleep && _wingsOn; } }
        internal bool Sleepy { get { return Droopy; } } // asleep or starving: no outings
        internal RavenMotor Motor; // its motor in the air (RavenMotor.Of), kept with the bird
        internal float TimeScale = 1f; // its own pace of time in flight (the test window's picture slows it; the game: 1)
        private float Dt { get { return Time.deltaTime * TimeScale; } }
        internal readonly TemplateMix Mix = new TemplateMix(); // the flight template it flies now (the motor's director picks)
        // the animation director's book on this bird's busy idles
        internal float IdleBusyUntil, IdleLastAt = -1000f;
        internal string IdleLastKind = "";

        internal void Init(Player p, bool munin, int level)
        {
            Player = p; Munin = munin; Level = level;
            FinishTakeoff(); // a bird back from a perch flight is not leaving
            _anim = GetComponentInChildren<Animator>();
            if (_skel != null && !_skel.For(_anim)) _skel = null; // another model: its own clips
            // the game's raven has no head bone: the neck carries the head and the beak (Jaw)
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (_head == null && (t.name == "Neck" || t.name.ToLowerInvariant() == "head")) _head = t;
                if (_spine == null && t.name == "Spine1") _spine = t;
            }
            _nextGlance = Time.time + Random.Range(3f, 7f);
            _left = FindWing(".l");
            _right = FindWing(".r");
            LogBones();
            RavenAura.Attach(gameObject, munin, level); // a faint haze on a raven of trust 6 and 7
        }

        // it was spoken to: the head comes up for a moment, even on an unwoken raven
        internal void Wake(float seconds) { _awakeUntil = Mathf.Max(_awakeUntil, Time.time + seconds); }

        // from ShoulderPin, once the raven is in place
        internal void Apply()
        {
            if (Player == null) return;
            // the gifts' net flags, read once a frame (the poses ask them several times)
            _wingsOn = WingsFx.On(Player);
            _furyOn = Munin && Fury.Shown(Player);
            if (!_furyOn) { _furyNext = 0f; _furyFlareFrom = -1f; } // the next fury starts its rhythm afresh
            if (_lgBob > 0f && !Leap.Gliding(Player)) _lgBob = 0f;     // and the next leap its own
            if (_eyes != EyeGlow.Version) { _eyes = EyeGlow.Version; long t0 = Slow.Start(); EyeGlow.Apply(gameObject, Level); Slow.End(t0, "raven eye glow"); } // the eyes glow by trust
            bool takeoff = TakeoffActive;
            if (takeoff && TakeoffKeys.Active)
            {
                float phase = Mathf.Clamp01((Time.time - _takeoffStarted) / Mathf.Max(0.1f, TakeoffKeys.Duration)) * (TakeoffKeys.Keys - 1);
                float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _takeoffStarted) / Mathf.Max(0.01f, TakeoffEntrySeconds)));
                ApplyTakeoffPose(phase, true, entry);
                return;
            }
            // another player's raven flying far from you: too small to see its wings, the clip is not sampled
            if (_vanillaFly && FarFlight()) return;
            if (_vanillaFly && (!takeoff || !TakeoffKeys.Active) && SampleFly()) { Tuck(); FlareWings(); FlightSmall(); if (Hovering) Nod(); if (FlyHeld) LookOff(); return; } // the game's flying clip moves the wings (and, hanging over the shoulder, the head looks about)
            SitBase(); // the sitting itself: the skeleton's sitting (its clips, its actions)
            if (SitNew) { SitLean(); _sitDir.Tick(this); }
            if (!Hovering && !takeoff) PerchLegs();
            // another player's raven far away: too small to see, nothing to turn
            Player me = Player.m_localPlayer;
            if (!ReferenceEquals(Player, me) && me != null && (Player.transform.position - me.transform.position).sqrMagnitude > 2500f) return;
            if (_head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Quaternion want;
            if (Ducked) want = Quaternion.AngleAxis(DuckDegrees, right);
            else if (Droopy) want = Quaternion.AngleAxis(Asleep ? SleepyDegrees : GloomyDegrees, right);
            else
            {
                if (Time.time >= _nextSearch) { _nextSearch = Time.time + 0.4f; Search(); }
                want = SitNew ? SitLook(fwd, right) : Turn(fwd, right);
            }
            // two eased steps: the turn starts and stops softly (a constant-speed snap read as a twitch), Munin quick,
            // Hugin slow and smooth
            // awake: the head jumps from look to look and holds (Saccade); asleep, drooping, ducked, dozing: it eases
            if (!Droopy && !Ducked && _sitDir.Doze < 0.3f)
            {
                _look = _sac.Step(want, Time.deltaTime, Munin, Nerves);
                _lookMid = _look;
            }
            else
            {
                float tau = (Munin ? HeadEaseMunin : HeadEaseHugin) / Nerves;
                float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, tau));
                _lookMid = Quaternion.Slerp(_lookMid, want, a);
                _look = Quaternion.Slerp(_look, _lookMid, a);
                _sac.Reset(_look);
            }

            // the game's animation sets the head each frame; when it did not this frame, start from its last pose,
            // or the turn would add up frame after frame
            Quaternion local = _head.localRotation;
            if (_wrote && Quaternion.Angle(local, _written) < 0.01f) local = _base;
            _base = local;
            _head.localRotation = local;
            _head.rotation = Stabilize() * _look * _head.rotation; // a bird's head holds still a moment when the body turns
            _written = _head.localRotation;
            _wrote = true;

            // an unwoken raven sits hunched, its back bent forward
            if (_spine != null)
            {
                Quaternion sl = _spine.localRotation;
                if (_spineWrote && Quaternion.Angle(sl, _spineWritten) < 0.01f) sl = _spineBase;
                _spineBase = sl;
                _spine.localRotation = sl;
                if (Droopy) _spine.rotation = Quaternion.AngleAxis(Asleep ? HunchDegrees : GloomyHunch, right) * _spine.rotation;
                else
                {
                    // Hugin sits proud, Munin hunched; the fury's pounce and Hugin's duck come and go eased, not at once
                    float extra = Munin ? (_furyOn ? FuryHunch : 0f) : (Ducked ? DuckHunch + HuginUpright : 0f);
                    _hunchExtra = Mathf.MoveTowards(_hunchExtra, extra, Time.deltaTime * HunchEase);
                    float sitHunch = SitNew ? _sitDir.MoodHunch : 0f; // (the sitting system: its mood; an act's own body turn is its template's, SitPose)
                    _spine.rotation = Quaternion.AngleAxis((Munin ? MuninHunch : -HuginUpright) + _hunchExtra + sitHunch, right) * _spine.rotation;
                }
                _spineWritten = _spine.localRotation;
                // warm by a fire, or the cold: the feathers puffed up (WorldReact, looked at once a second; eased both ways)
                if (Time.time >= _fluffAt) { _fluffAt = Time.time + 1f; _fluffWant = WorldReact.Fluffed(Player) ? 1f : 0f; }
                _fluff = Mathf.MoveTowards(_fluff, _fluffWant, Time.deltaTime * 0.5f);
                if (_fluff > 0.01f) _spine.localScale = Vector3.one * (1f + _fluff * FluffScale);
                else if (_spine.localScale != Vector3.one) _spine.localScale = Vector3.one;
                _spineWrote = true;
            }
            Wings(fwd, right);
            SmallMotion(fwd, right);
            if (SitNew) SitPose(); // a sitting template's pose, on top of everything
        }

        private Wing FindWing(string side)
        {
            Wing w = new Wing();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.EndsWith(side)) continue;
                if (n == "wing1" + side) w.A = new BoneTurn(t);
                else if (n == "wing2" + side) w.B = new BoneTurn(t);
                else if (n == "wing3" + side) w.C = new BoneTurn(t);
            }
            return w.A != null ? w : null;
        }

        // the pose wanted now: a test pose, a glide in Hugin's high leap, taking off, Munin's run under his Wings, a flap, or folded (the game's own)
        private void Wings(Vector3 fwd, Vector3 right)
        {
            if (_left == null && _right == null) return;
            Vector3 pos = Player.transform.position;
            if (Time.deltaTime > 0f && _lastPos != Vector3.zero)
            {
                Vector3 step = Vector3.ProjectOnPlane(pos - _lastPos, Vector3.up);
                _speed = Mathf.Lerp(_speed, Mathf.Min(step.magnitude / Time.deltaTime, 30f), Mathf.Clamp01(Time.deltaTime * 6f));
            }
            _lastPos = pos;
            float o = 0f, u = 0f, e = 0f, io = 0f, iu = 0f, ie = 0f;
            float hz = 0f, amp = 0f; int mode = 0; // the stroke now: beats a second, size (°), which kind of beating
            bool local = ReferenceEquals(Player, Player.m_localPlayer);
            GiftPose gift = null;
            if (_moment != null && Time.time >= _momentUntil) _moment = null;
            if (_moment != null) Poses.TryGetValue(_moment, out gift);
            if (TestWings && local) { o = TestOut; u = TestUp; e = TestElbow; }
            // carried through the air with you (Hugin's leap, or on Odin's wings while it sits on you): a glide with its own
            // slow bob and now and then a few beats of its own, never a still spread
            else if (!Asleep && ((local && Leap.Gliding(Player)) || OdinWings.AirShown(Player))) { LeapWings(out o, out u, out e, out hz, out amp); mode = amp > 0f ? 1 : 0; }
            else if (_takingOff)
            {
                // leaving: wide, deep beats (the stroke folds the wing on the way up by itself)
                o = FlapOut + 10f; u = TakeOffUp; e = FlapOut * 0.7f; hz = TakeOffHz; amp = TakeOffBeat; mode = 2;
            }
            else if (!Asleep && Time.time < _jumpUntil)
            {
                float t = Mathf.Clamp01((Time.time - _jumpStarted) / Mathf.Max(0.05f, _jumpUntil - _jumpStarted));
                float envelope = Mathf.Sin(t * Mathf.PI);
                o = FlapOut * 0.82f; u = FlapUp; e = FlapOut * 0.52f;
                hz = _jumpCycles / Mathf.Max(0.05f, _jumpUntil - _jumpStarted); amp = envelope * FlapBeat * _jumpStrength; mode = 3;
            }
            else if (Soaring && Time.time >= _flapUntil) { o = GlideOut; u = GlideUp + Mathf.Sin(Time.time * 1.7f) * 5f; e = GlideElbow; }
            else if (Hovering) { o = FlapOut; u = FlapUp; e = FlapOut * 0.6f; hz = HoverHz; amp = FlapBeat; mode = 4; }
            else if (gift != null && !Asleep) Gift(gift, out o, out u, out e, out io, out iu, out ie);
            else if (Ducked) { } // Hugin under Munin's wings: folded, still (every wing value stays 0)
            else if (SitNew && !Asleep && _sitDir.Run != null && _sitDir.Run.Wings && _sitDir.ActW > 0.01f) { } // a sitting template's wings (SitPose): nothing else poses them meanwhile
            else if (!Asleep && SitNew && _sitBody.Balance > 0.02f && Time.time >= _flapUntil && !(Munin && _wingsOn) && !Leap.Gliding(Player) && !OdinWings.AirShown(Player))
            {
                // keeping its seat: wings a little open for balance, as much as its feet cannot hold (SitBody), a light quiver
                float run = _sitBody.Balance * (Munin ? 1.2f : 1f);
                float quiver = Mathf.Sin(Time.time * (Munin ? 3.1f : 2.3f) * Mathf.PI * 2f) * 3f * run;
                o = BalanceOut * run; u = BalanceUp * run + quiver; e = BalanceElbow * run;
            }
            else if (Munin && !Asleep && _speed > RunSpeed && _wingsOn)
            {
                o = RunOut; u = RunUp; e = RunElbow; hz = RunBeatHz; amp = RunBeat; mode = 5; // slow, small beats around the pose
            }
            else if (_furyOn && !Asleep) FuryPose(out o, out u, out e); // restless, with sharp threats, not a held pose
            else if (Time.time < _flapUntil)
            {
                // a few quick beats: Hugin spreads wide, Munin keeps his wings swept back (a narrow, sharp beat)
                hz = FlapHz * _flapRate; mode = 6;
                if (Munin) { o = FlapOut * 0.4f; u = FlapUp * 0.6f; e = FlapOut * 0.2f; amp = FlapBeat * 0.8f; }
                else { o = FlapOut; u = FlapUp; e = FlapOut * 0.6f; amp = FlapBeat; }
            }
            bool engine = (EngineBeats(mode) || (_engineOn && mode == 0 && _amp > 0.3f)) && EngineUsable(); // (else the hand-made stroke, with its own spread)
            if (engine && mode != 0) { o = 0f; u = 0f; e = 0f; } // the clip's own wings are the spread
            // each new opening its own: now and then held a little back (only the opening shrinks: towards the tail, never forward)
            bool open = o > 1f || o + io > 1f;
            if (open && !_poseOpen) _poseBack = Random.Range(0f, PoseBackMax);
            _poseOpen = open;
            if (open)
            {
                float oi = o + io;
                o -= _poseBack * Mathf.Max(0f, o); oi -= _poseBack * Mathf.Max(0f, oi); io = oi - o;
                e -= _poseBack * 0.5f * Mathf.Max(0f, e);
            }
            // into or out of Munin's wings (his run pose, Hugin's duck): a soft blend for a moment, not a snap
            bool wings = _wingsOn;
            if (wings != _wingsWas) { _wingsWas = wings; _softUntil = Time.time + 0.9f; }
            float k = Mathf.Clamp01(Time.deltaTime * (_takingOff ? 40f : Time.time < _softUntil ? 4.5f : 14f)); // quick, but not a jump from pose to pose (leaving: follows the deep beats closely)
            _out = Mathf.Lerp(_out, o, k); _up = Mathf.Lerp(_up, u, k); _elbow = Mathf.Lerp(_elbow, e, k);
            _inOut = Mathf.Lerp(_inOut, io, k); _inUp = Mathf.Lerp(_inUp, iu, k); _inElbow = Mathf.Lerp(_inElbow, ie, k);
            float kb = Mathf.Clamp01(k * ElbowFollow), kc = Mathf.Clamp01(k * HandFollow); // the elbow after the shoulder, the hand after the elbow
            _elbowB = Mathf.Lerp(_elbowB, _elbow, kb); _elbowC = Mathf.Lerp(_elbowC, _elbowB, kc); _outC = Mathf.Lerp(_outC, _out, kc);
            _inElbowB = Mathf.Lerp(_inElbowB, _inElbow, kb); _inElbowC = Mathf.Lerp(_inElbowC, _inElbowB, kc); _inOutC = Mathf.Lerp(_inOutC, _inOut, kc);
            Stroke(hz, amp, mode, k);
            // the inner wing: the one closer to the wearer's head
            bool leftInner = true;
            bool inner = Mathf.Abs(_inOut) + Mathf.Abs(_inUp) + Mathf.Abs(_inElbow) > 0.01f; // a gift pose with one wing: which one
            if (inner && _left != null && _right != null && _left.A.T != null && _right.A.T != null)
            {
                Vector3 head = Player.GetHeadPoint();
                leftInner = (_left.A.T.position - head).sqrMagnitude <= (_right.A.T.position - head).sqrMagnitude;
            }
            _engineOn = engine && _amp > 0.3f && EngineBeatSafe(fwd);
            if (_engineOn) return; // beaten by the flight's own engine (EngineBeat)
            PoseWing(_left, 1f, fwd, leftInner);
            PoseWing(_right, -1f, fwd, !leftInner);
        }

        // ---------------------------------------------------------------- the beat on the shoulder by the flight's engine
        // A jump, a flap, a beat in Hugin's leap: the same wing beat as in flight, not a hand-made one: the game's flying
        // clip sampled for the wings alone (the body, head and legs keep sitting), eased in by how hard it beats (a small
        // flutter half opens the wings, a full beat is the flight's), and the user's wing keys («Змах крил») on top. Munin
        // keeps his wings swept back. Only near you; without the clip the hand-made stroke stays (PoseWing).
        private bool _engineOn;
        private float _shT;                              // where in the clip's loop the beat is (its strokes, 0..1)
        private Transform[] _shBones; private string[] _shNames; private Quaternion[] _shRot; private Vector3[] _shPos, _shScl;
        private int[] _shWing, _shTier; private Quaternion[] _shWingRot;
        internal static float EngineLag = 0.05f, EngineOpen = 0.7f; // s the elbow follows the shoulder (the hand twice that); share of the flight's spread
        private readonly Vector3[] _shAddL = new Vector3[WingKeys.Bones], _shAddR = new Vector3[WingKeys.Bones];
        internal static Vector3 MuninSwept = new Vector3(-10f, 0f, 0f), MuninSweptElbow = new Vector3(-8f, 0f, 0f), MuninSweptHand = new Vector3(-5f, 0f, 0f);

        private static bool EngineBeats(int mode) { return mode == 1 || mode == 2 || mode == 3 || mode == 4 || mode == 6; } // leap, leaving, jump, hover, flap (not Munin's run pose)

        // the engine can beat this raven now: near you, the game's flying clip there and its beat measured (once, for all)
        private bool EngineUsable()
        {
            if (_anim == null || Player == null || FlyClip(_anim) == null) return false;
            Player me = Player.m_localPlayer;
            if (!ReferenceEquals(Player, me) && me != null && (transform.position - me.transform.position).sqrMagnitude > 2500f) return false;
            if (!BeatKnown) { EnsureLoop(); if (!BeatKnown) return false; }
            if (_shBones == null || Stale()) FindEngineBones(); // (a thing it wore taken off: its parts are gone from the list)
            return _shWing.Length > 0;
        }

        // a bone gone in the middle of a beat: the lists are found anew, this frame beats by hand, and what was gone
        // goes to the log once (which bones, and whether the stale check saw it)
        private static bool _engineToldDead;
        private bool EngineBeatSafe(Vector3 fwd)
        {
            try { return EngineBeat(fwd); }
            catch (System.NullReferenceException)
            {
                if (!_engineToldDead)
                {
                    _engineToldDead = true;
                    StringBuilder sb = new StringBuilder("Raven wing engine: a bone was gone mid-beat on " + name + " (frame " + Time.frameCount + ")");
                    sb.Append(_anim == null ? ", animator gone" : ", animator " + _anim.name);
                    sb.Append(", bones ").Append(_shBones != null ? _shBones.Length : -1).Append(", stale ").Append(_shBones != null && Stale());
                    if (_shBones != null) for (int i = 0; i < _shBones.Length; i++) if (_shBones[i] == null) sb.Append(" #").Append(i).Append(' ').Append(_shNames != null && i < _shNames.Length ? _shNames[i] : "?");
                    if (_kl != null) for (int i = 0; i < _kl.Length; i++) if (_kl[i] == null) sb.Append(" L").Append(i);
                    if (_kr != null) for (int i = 0; i < _kr.Length; i++) if (_kr[i] == null) sb.Append(" R").Append(i);
                    Log(sb.ToString());
                }
                _shBones = null; _kl = _kr = null; // (both found anew next time)
                return false;
            }
        }

        private bool EngineBeat(Vector3 fwd)
        {
            AnimationClip clip = FlyClip(_anim);
            float w = Mathf.Clamp01(_amp / Mathf.Max(1f, FlapBeat));
            w = w * w * (3f - 2f * w) * EngineOpen; // (on a shoulder never the flight's whole spread)
            _shT = Mathf.Repeat(_shT + Time.deltaTime * _strokeHz / Mathf.Max(1, Strokes), 1f);
            float x = _loopA + _shT * (_loopB - _loopA);
            // the sitting pose of every bone, the clip's wings at this moment, then everything back but the wings
            Transform root = _anim.transform;
            Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale;
            for (int i = 0; i < _shBones.Length; i++) { _shRot[i] = _shBones[i].localRotation; _shPos[i] = _shBones[i].localPosition; _shScl[i] = _shBones[i].localScale; }
            // a wave along the wing: the shoulder leads, the elbow a moment after it, the hand after the elbow
            float span = Mathf.Max(0.01f, _loopB - _loopA);
            try
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    clip.SampleAnimation(_anim.gameObject, tier == 0 ? x : _loopA + Mathf.Repeat(x - _loopA - tier * EngineLag, span));
                    for (int j = 0; j < _shWing.Length; j++) if (_shTier[j] == tier) _shWingRot[j] = _shBones[_shWing[j]].localRotation;
                }
            }
            finally
            {
                // always back, even when a bone was gone mid-beat: left in the clip's pose and size, the bird and
                // everything on it (its effects) would stay the game's big raven
                for (int i = 0; i < _shBones.Length; i++)
                    if (_shBones[i] != null) { _shBones[i].localRotation = _shRot[i]; _shBones[i].localPosition = _shPos[i]; _shBones[i].localScale = _shScl[i]; }
                if (root != null) { root.localPosition = lp; root.localRotation = lr; root.localScale = ls; }
            }
            for (int j = 0; j < _shWing.Length; j++) { int i = _shWing[j]; _shBones[i].localRotation = Quaternion.Slerp(_shRot[i], _shWingRot[j], w); }
            // the user's keys and Munin's swept wings, as much as it beats
            if (_kl == null) WingKeys.Find(transform, out _kl, out _kr);
            float kx = WingKeys.KeyX(_shT);
            for (int b = 0; b < WingKeys.Bones; b++)
            {
                Vector3 p = WingKeys.Active ? WingKeys.At(kx, b) : Vector3.zero;
                if (Munin) p += b == 0 ? MuninSwept : b == 1 ? MuninSweptElbow : MuninSweptHand;
                if (b < 2) p.x -= _strokeBack * (b == 0 ? 1f : 0.5f); // (back: towards the tail)
                _shAddL[b] = _shAddR[b] = p * w;
            }
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            WingKeys.ApplyAdds(_kl, _kr, facing, _shAddL, _shAddR);
            return true;
        }

        private bool Stale()
        {
            for (int i = 0; i < _shBones.Length; i++) if (_shBones[i] == null) return true;
            return false;
        }

        // every bone under the animator (to keep), and which of them are the wings (Wing1 and all under it)
        private void FindEngineBones()
        {
            _shBones = _anim.GetComponentsInChildren<Transform>(true);
            List<Transform> keep = new List<Transform>();
            // only its own bones: not what it wears or holds (taken off later, those would leave holes in the list)
            foreach (Transform t in _shBones) if (t != _anim.transform && t.GetComponentInParent<Outfits.Piece>() == null && t.GetComponentInParent<RavenFetch.Carried>() == null) keep.Add(t);
            _shBones = keep.ToArray();
            _shNames = new string[_shBones.Length];
            for (int i = 0; i < _shBones.Length; i++) _shNames[i] = _shBones[i].name + (_shBones[i].parent != null ? "<" + _shBones[i].parent.name : "");
            _shRot = new Quaternion[_shBones.Length]; _shPos = new Vector3[_shBones.Length]; _shScl = new Vector3[_shBones.Length];
            List<int> wing = new List<int>();
            for (int i = 0; i < _shBones.Length; i++)
                for (Transform t = _shBones[i]; t != null && t != _anim.transform; t = t.parent)
                    if (t.name.ToLowerInvariant().StartsWith("wing1.")) { wing.Add(i); break; }
            _shWing = wing.ToArray();
            _shWingRot = new Quaternion[_shWing.Length];
            // which part of the wing each is: 0 the shoulder's, 1 from the elbow (Wing2) out, 2 from the hand (Wing3) out
            _shTier = new int[_shWing.Length];
            for (int j = 0; j < _shWing.Length; j++)
                for (Transform t = _shBones[_shWing[j]]; t != null && t != _anim.transform; t = t.parent)
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.StartsWith("wing3")) { _shTier[j] = 2; break; }
                    if (n.StartsWith("wing2")) { _shTier[j] = Mathf.Max(_shTier[j], 1); }
                }
        }

        // a gift's pose now, with its tremble
        private static void Gift(GiftPose g, out float o, out float u, out float e, out float io, out float iu, out float ie)
        {
            float shake = g.Shake > 0f ? Mathf.Sin(Time.time * 7f * Mathf.PI * 2f) * g.Shake : 0f;
            o = g.Out; u = g.Up + shake; e = g.Elbow;
            io = g.InOut; iu = g.InUp; ie = g.InElbow;
        }

        // ---------------------------------------------------------------- legs in flight
        // The game's fly-away clip leaves the legs as they sit. In the air (the dome, a strike, hanging over the
        // shoulder) they are drawn back under the tail and the toes curled, turned by the raven's own sideways axis.
        // Tried live with: raven legs <hip> <knee> <ankle> <toes>
        internal static float LegHip = 82f, LegKnee = -50f, LegAnkle = 20f, LegToes = 60f;
        // landing (the new flight's flare): legs forward to take the shoulder, toes spread
        internal static float ReachHip = -35f, ReachKnee = 25f, ReachAnkle = -15f, ReachToes = -25f;
        private float _reach;
        internal static bool LegsOff;
        private sealed class LegBone { public BoneTurn B; public int Kind; public bool Left; } // 0 hip, 1 knee, 2 ankle, 3 front toe, 4 back talon
        private List<LegBone> _legs;
        private float _tuck;
        private float _legPhase, _legHz;

        private void FindLegs()
        {
            _legs = new List<LegBone>();
            _legPhase = Random.value * Mathf.PI * 2f;
            _legHz = Random.Range(0.45f, 0.7f);
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.EndsWith(".l") && !n.EndsWith(".r")) continue;
                string b = n.Substring(0, n.Length - 2);
                int kind = b == "leg1" ? 0 : b == "leg2" ? 1 : b == "leg3" ? 2 : (b == "index1" || b == "mid1" || b == "ring1" || b == "index2" || b == "mid2" || b == "ring2") ? 3 : (b == "backtalon1" || b == "backtalon2") ? 4 : -1;
                if (kind >= 0) _legs.Add(new LegBone { B = new BoneTurn(t), Kind = kind, Left = n.EndsWith(".l") });
            }
        }

        // after each sampled flight frame: up in a moment, as a bird pulls its feet in after the take-off
        private void Tuck()
        {
            if (_legs == null) FindLegs();
            // the take-off: legs bend into the crouch (as the perch's bend), straighten in the push, then come up
            bool held = FlyHeld;
            float crouchW = held ? Mathf.Clamp01((Time.time - _flyHoldFrom) / (_flyCrouch * 0.7f)) : 0f;
            crouchW = crouchW * crouchW * (3f - 2f * crouchW);
            float since = Time.time - _flyHoldUntil, push = !held && _flyHoldUntil > 0f && since < 0.15f ? 1f - since / 0.15f : 0f;
            _tuck = Mathf.MoveTowards(_tuck, held || push > 0f || LegsOff ? 0f : 1f, Dt * 2.5f);
            _reach = Mathf.MoveTowards(_reach, Flare > 0.25f ? 1f : 0f, Dt * 3f);
            if (_legs.Count == 0) return;
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 side = facing * Vector3.right;
            float k = _tuck * _tuck * (3f - 2f * _tuck) ;
            float flutter = (1f - _reach) * k;
            foreach (LegBone l in _legs) l.B.Begin(); // parents first (the list is in hierarchy order)
            foreach (LegBone l in _legs)
            {
                float a = l.Kind == 0 ? LegHip : l.Kind == 1 ? LegKnee : l.Kind == 2 ? LegAnkle : l.Kind == 3 ? LegToes : -LegToes;
                float r = l.Kind == 0 ? ReachHip : l.Kind == 1 ? ReachKnee : l.Kind == 2 ? ReachAnkle : l.Kind == 3 ? ReachToes : -ReachToes;
                float phase = _flyClock * _legHz + _legPhase + (l.Left ? 0f : Mathf.PI * 0.65f);
                float sway = Mathf.Sin(phase) * flutter * 0.65f;
                if (l.Kind == 0) a += sway * 3f + Flight.Dive * 4f;
                else if (l.Kind == 1) a -= sway * 2f + Flight.Dive * 2f;
                else if (l.Kind == 2) a += sway * 1.5f;
                else if (l.Kind == 3 || l.Kind == 4) a += Mathf.Sin(phase + 0.8f) * 3f * flutter;
                a = Mathf.Lerp(a, r, _reach * _reach * (3f - 2f * _reach));
                if (k > 0.001f && Mathf.Abs(a) > 0.01f) l.B.Turn(Quaternion.AngleAxis(a * k, side));
                if (l.Kind <= 2 && (crouchW > 0.001f || push > 0.001f))
                {
                    float bend = (l.Kind == 0 ? PerchHip : l.Kind == 1 ? PerchKnee : PerchAnkle) * (0.6f * crouchW - 0.4f * push);
                    if (Mathf.Abs(bend) > 0.01f) l.B.Turn(Quaternion.AngleAxis(bend, side));
                }
                l.B.End();
            }
        }

        // ---------------------------------------------------------------- sitting on a sloping shoulder
        // The shoulder falls away from the neck: the leg by the head (Hugin's left, Munin's right) is bent so the
        // bird sits down lower and the outer foot reaches the shoulder too. Tried live: raven perch <hip> <knee> <ankle> [drop cm]
        internal static float PerchHip = -25f, PerchKnee = 40f, PerchAnkle = -18f; // the user's fit (2026-10-09)
        internal static bool PerchSwap; // the other leg, if the model's sides turn out the other way
        private float _perchW = 1f;     // how far the leg is bent in (eased after a landing)

        private void PerchLegs()
        {
            if (_legs == null) FindLegs();
            if (_legs.Count == 0) return;
            if (Mathf.Abs(PerchHip) + Mathf.Abs(PerchKnee) + Mathf.Abs(PerchAnkle) < 0.1f) return;
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 side = facing * Vector3.right;
            bool innerLeft = !Munin ^ PerchSwap; // Hugin sits on the right shoulder: his left leg is by the head
            _perchW = Mathf.MoveTowards(_perchW, 1f, Time.deltaTime * 2.5f);
            float w = _perchW * _perchW * (3f - 2f * _perchW);
            float crouch = SitNew ? Mathf.Clamp(SitRow(11), -0.5f, 1f) : 0f; // a sitting template's crouch: both legs
            foreach (LegBone l in _legs) if (l.Kind <= 2) l.B.Begin();
            foreach (LegBone l in _legs)
            {
                if (l.Kind > 2) continue;
                float a = (l.Kind == 0 ? PerchHip : l.Kind == 1 ? PerchKnee : PerchAnkle) * ((l.Left == innerLeft ? w : 0f) + crouch * 0.8f);
                if (Mathf.Abs(a) > 0.01f) l.B.Turn(Quaternion.AngleAxis(a, side));
                l.B.End();
            }
        }

        // ---------------------------------------------------------------- layer 2: small motion over any animation
        // A bird's head keeps still when its body turns and catches up after (Hugin holds longer and calmer, Munin
        // jerks after it sooner); in flight it stays level while the body banks. The tail balances turns and changes
        // of pace, the chest breathes, and now and then it shakes out its feathers (Munin more often).
        internal static float StabHold = 0.75f, StabBack = 3f, StabMax = 60f;
        internal static float HeadLevel = 0.7f;               // in flight: how much of the bank the head takes out
        internal static float BreathDeg = 1.2f, BreathHz = 0.32f;
        internal static float TailTurn = 0.08f, TailMax = 22f;
        internal static float ShakeSeconds = 0.45f, ShakeHz = 5f, ShakeDeg = 7f;
        private Quaternion _stab = Quaternion.identity, _lastBody;
        private int _stabFrame = -10;
        private BoneTurn _tailB, _breathB, _neckRootB, _hipB;
        private bool _smallFound;
        private float _tailYaw, _tailPitch;
        private float _flightNeckPitch;
        private float _shakeAt = -10f;
        private readonly Saccade _sac = new Saccade();
        internal static float SettleChance = 0.85f; // after a landing on the shoulder: it settles in (SitTemplates.SettleName)
        internal static float SettleAfter = 0.75f;  // s after the landing, when the landing style's own hop / beat / step came first

        internal void ShakeNow() { _sitDir.ShakeWanted = true; } // a quirk (Quirks: «Мокрий»): shake the water off now, if nothing else is going on
        internal void Act(string template, float after = 0f) { if (!SitNew) return; _sitDir.Want(template, after, 4f); } // a quirk's moment (or the landing's last fold): this act soon

        private Quaternion Stabilize()
        {
            Quaternion body = transform.rotation;
            if (Time.frameCount - _stabFrame > 2) { _stab = Quaternion.identity; _lastBody = body; } // back from a flight: start afresh
            _stabFrame = Time.frameCount;
            Quaternion delta = body * Quaternion.Inverse(_lastBody);
            _lastBody = body;
            float hold = Mathf.Min(0.95f, (Munin ? 0.4f : 0.85f) * StabHold / 0.75f / Nerves); // Hugin holds his head long, Munin hardly (never more than the body turned)
            float back = Munin ? StabBack * 2f : StabBack * 0.65f;
            _stab = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(delta), hold) * _stab;
            _stab = Quaternion.Slerp(_stab, Quaternion.identity, Mathf.Clamp01(Time.deltaTime * back));
            float ang; Vector3 ax;
            _stab.ToAngleAxis(out ang, out ax);
            if (ang > 180f) ang -= 360f;
            if (Mathf.Abs(ang) > StabMax) _stab = Quaternion.AngleAxis(Mathf.Sign(ang) * StabMax, ax);
            return _stab;
        }

        private void FindSmall()
        {
            _smallFound = true;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (_tailB == null && t.name == "Tail") _tailB = new BoneTurn(t);
                else if (_breathB == null && t.name == "Spine") _breathB = new BoneTurn(t);
                else if (_neckRootB == null && t.name == "Spine1") _neckRootB = new BoneTurn(t);
                else if (_hipB == null && t.name == "Hip") _hipB = new BoneTurn(t);
            }
        }

        // sitting: breathing, the tail with your turns and pace, a feather shake now and then
        private void SmallMotion(Vector3 fwd, Vector3 right)
        {
            if (!_smallFound) FindSmall();
            float dt = Time.deltaTime;
            if (dt <= 0f || Player == null || !SitNew) return;
            // the tail balances the body: swung against its turn, tipped with its lean (SitBody)
            _tailYaw = Mathf.Lerp(_tailYaw, Mathf.Clamp(-_sitBody.YawVel * TailTurn, -TailMax, TailMax), Mathf.Clamp01(dt * 8f));
            _tailPitch = Mathf.Lerp(_tailPitch, Mathf.Clamp(_sitBody.Pitch * 0.9f + _sitBody.PitchVel * 0.03f, -TailMax, TailMax), Mathf.Clamp01(dt * 8f));

            if (_breathB != null)
            {
                _breathB.Begin();
                _breathB.Turn(Quaternion.AngleAxis(Mathf.Sin(Time.time * BreathHz * Mathf.PI * 2f + (Munin ? 1.7f : 0f)) * BreathDeg * (Droopy ? 0.6f : 1f), right));
                _breathB.End();
            }
            if (_tailB != null)
            {
                _tailB.Begin();
                _tailB.Turn(Quaternion.AngleAxis(_tailYaw, Vector3.up) * Quaternion.AngleAxis(_tailPitch, right));
                _tailB.End();
            }

            // a feather shake (a sitting template's), a quick shiver of the body dying away
            float st = Time.time - _shakeAt;
            if (_hipB != null)
            {
                _hipB.Begin();
                if (st < ShakeSeconds)
                {
                    float k = Mathf.Sin(st / ShakeSeconds * Mathf.PI); // swells and dies away, no jolt at either end
                    _hipB.Turn(Quaternion.AngleAxis(Mathf.Sin(st * ShakeHz * Mathf.PI * 2f) * ShakeDeg * k, fwd));
                }
                // Munin: small nervous twitches of the body now and then
                if (Munin && !Droopy && !AnimDirector.Busy(this))
                {
                    if (Time.time >= _twitchNext) { _twitchNext = Time.time + Random.Range(2f, 5f) / Nerves; _twitchAt = Time.time; _twitchDeg = Random.Range(3f, 5f) * (Random.value < 0.5f ? -1f : 1f); }
                    float tw = Time.time - _twitchAt;
                    if (tw < 0.25f) _hipB.Turn(Quaternion.AngleAxis(_twitchDeg * Mathf.Sin(tw / 0.25f * Mathf.PI), Vector3.up));
                }
                _hipB.End();
            }

            SettleStep(); // just landed: its little settle, when the director lets it
            float jolt = _sitBody.TakeJolt();
            if (jolt > 0f) JoltWings(jolt); // a knock its feet cannot hold: a few beats (the dip is the body's)
            if (_jumpFlapAt > 0f && Time.time >= _jumpFlapAt)
            {
                _jumpStarted = Time.time;
                _jumpUntil = Time.time + _jumpFlapLen;
                _jumpFlapAt = 0f;
            }
            float ht = Time.time - _hopAt, hop = 0f;
            if (ht < 0f) hop = 0f; // its own moment has not come yet
            else if (ht < 0.08f) hop = -HopDip * (ht / 0.08f);
            else if (ht < 0.4f) hop = HopUp * Mathf.Sin((ht - 0.08f) / 0.32f * Mathf.PI);

            // a step aside on the shoulder (a sitting template's), with a little hop
            float sp = Mathf.Clamp01((Time.time - _stepAt) / 0.3f);
            _stepNow = Mathf.Lerp(_stepFrom, _stepTo, sp * sp * (3f - 2f * sp));
            float stepHop = sp < 1f ? Mathf.Sin(sp * Mathf.PI) * 0.015f : 0f;
            PinNudge = Vector3.up * (hop + stepHop + _sitBody.Dip) + right * _stepNow;
            Clack(right);
        }

        // the animator's sitting idle state, once seen on a shoulder (Hugin, Munin): a copy brought straight into it
        // skips the game's appearing (teleportin: its smoke and sound) — ShoulderPin.ArriveFrom
        internal static readonly int[] KnownIdle = new int[2];

        // the sitting idle's state, learned once from a copy nobody sees (a raven that has not sat on a shoulder yet this
        // session, called back from a perch first thing): the copy goes through the game's appearing to its sitting, its
        // state is kept, the copy is gone in the same frame (nothing drawn, no sound, no smoke: its events off)
        internal static int LearnIdle(bool munin)
        {
            int k = munin ? 1 : 0;
            if (KnownIdle[k] != 0) return KnownIdle[k];
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) return 0;
            GameObject g = Object.Instantiate(src.m_visual);
            AppearMute.Quiet(g);
            try
            {
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (AudioSource au in g.GetComponentsInChildren<AudioSource>(true)) au.enabled = false;
                foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                g.SetActive(true);
                Animator a = g.GetComponentInChildren<Animator>(true);
                if (a == null) return 0;
                a.enabled = true; a.fireEvents = false; a.applyRootMotion = false;
                a.Rebind(); a.Update(0f);
                a.SetTrigger("teleportin");
                for (int i = 0; i < 16 && KnownIdle[k] == 0; i++)
                {
                    a.Update(0.5f);
                    if (a.IsInTransition(0)) continue;
                    AnimatorClipInfo[] ci = a.GetCurrentAnimatorClipInfo(0);
                    string n = ci != null && ci.Length > 0 && ci[0].clip != null ? ci[0].clip.name.ToLowerInvariant() : "";
                    if (n.Contains("idle") && !n.Contains("idle2") && !n.Contains("idle3") && !n.Contains("idle4")) KnownIdle[k] = a.GetCurrentAnimatorStateInfo(0).fullPathHash;
                }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven idle could not be learned: " + e.Message); }
            finally { Object.DestroyImmediate(g); }
            return KnownIdle[k];
        }

        // ---------------------------------------------------------------- the sitting system (SitSystem)
        // While it sits, the clips are laid down here first each frame; the rest of Apply goes on over them in its order.
        private RavenSkeleton _skel;
        private readonly SitDirector _sitDir = new SitDirector(); // what it does and how it feels (the owner's game decides, SitDirector)
        internal SitDirector SitDirectorNow { get { return _sitDir; } }

        // ---- what the sitting director asks of a raven on a shoulder
        bool ISitBird.BirdMunin { get { return Munin; } }
        int ISitBird.BirdLevel { get { return Level; } }
        float ISitBird.BirdNerves { get { return Nerves; } }
        bool ISitBird.BirdAsleep { get { return Asleep; } }
        bool ISitBird.BirdDroopy { get { return Droopy; } }
        float ISitBird.BirdSpeed { get { return _speed; } }
        bool ISitBird.BirdOnPerch { get { return false; } }
        Player ISitBird.BirdOwner { get { return Player; } }
        bool ISitBird.BirdDecides { get { return Player != null && ReferenceEquals(Player, Player.m_localPlayer); } }
        bool ISitBird.BirdHears { get { return Player != null && ReferenceEquals(Player, Player.m_localPlayer); } }
        string ISitBird.BirdQuirk { get { if (Player == null || !ReferenceEquals(Player, Player.m_localPlayer)) return ""; Norov.Quirk q = Norov.QuirkOf(RavenSeat.On(Player, Munin)); return q != null ? q.Id : ""; } }
        private ZNetView _ownerNv;
        ZDO ISitBird.BirdZdo(bool write)
        {
            if (Player == null) return null;
            if (_ownerNv == null) _ownerNv = Player.GetComponent<ZNetView>();
            return _ownerNv != null && _ownerNv.IsValid() && (!write || _ownerNv.IsOwner()) ? _ownerNv.GetZDO() : null;
        }
        int ISitBird.BirdKey { get { return Munin ? SitNet.KeyM : SitNet.KeyH; } }
        ISitBird ISitBird.BirdPartner { get { ShoulderLife p = Player != null ? Shoulder.LifeOf(Player, !Munin) : null; return p != null && p.SitNew && !p.Asleep && !p.Droopy ? p : null; } }
        SitDirector ISitBird.BirdDir { get { return _sitDir; } }
        bool ISitBird.BirdHasClip(SitClip k) { return HasClip(k); }
        float ISitBird.BirdClipLength(int clip) { return ClipLength(clip); }
        void ISitBird.BirdPlay(SitRun r, float age) { SitPlay(r, age); }
        bool ISitBird.BirdMayDo() { return AnimDirector.MayIdle(this, AnimDirector.Idle.Doing); }
        void ISitBird.BirdDid(float seconds, bool reaction) { AnimDirector.DidIdle(this, reaction ? AnimDirector.Idle.React : AnimDirector.Idle.Doing, seconds); }
        void ISitBird.BirdMood(out bool alert, out bool drowsy)
        {
            alert = Senses.Ready && (Senses.Fight || Senses.Foe != null || Senses.Boss != null);
            drowsy = Senses.Ready && (Senses.Sleeping || (Senses.Afk && (Senses.Night || Senses.Indoors)));
        }
        private int SitSlot { get { return (GetInstanceID() & 0x7fffffff) % 3; } } // a far raven's clips: every third frame, not all on the same one

        internal bool SitNew { get { return _skel != null && _skel.Active && _skel.Sitting && !_vanillaFly && !_landing; } } // it sits (the skeleton's sitting is on)
        internal string SitInfo { get { return SitNew ? Lang.F("сидить: {0}, {1}; тіло: нахил {2}°, вбік {3}°, просідає {4} см, крила {5}%, звик {6}%, змахів за хвилину {7}", _skel.Now, _sitDir.Describe(), _sitBody.Pitch.ToString("0"), _sitBody.Roll.ToString("0"), Mathf.RoundToInt(_sitBody.Dip * 100f), Mathf.RoundToInt(_sitBody.Balance * 100f), Mathf.RoundToInt(_sitBody.Used * 100f), _sitBody.JoltsPerMinute) : _vanillaFly ? Lang.T("у польоті") : Lang.T("аніматор гри"); } }

        private void SitBase()
        {
            if (Skel == null) { if (_anim != null) _anim.speed = Still ? 0f : Droopy ? 0.5f : 1f; return; } // (a model without the raven's clips: the game's animator as it is)
            if (!_skel.Active)
            {
                // the game's animator still bringing it in (its appearing, a fly-in clip): it finishes into its sitting first
                if (_anim.enabled && !AnimatorSits()) { _anim.speed = 1f; return; }
                if (!_skel.Sitting) _skel.SetBase(null, 0.25f); // (from the animator's pose, eased)
                else if (_anim.enabled) _skel.EaseFromNow(0.25f);
                _skel.Begin();
            }
            else if (!_skel.Sitting && !_landing)
            {
                _skel.SetBase(null, BlendLand); // sits without the landing's clip (its fallback): into the sitting, eased
            }
            float rate = (Still ? 0f : Droopy ? 0.5f : 1f) * _sitDir.RateMul; // (its mood: alert a touch quicker, drowsy slower)
            Player me = Player.m_localPlayer;
            bool far = me != null && !ReferenceEquals(Player, me) && (transform.position - me.transform.position).sqrMagnitude > SitSystem.FarMetres * SitSystem.FarMetres;
            _skel.Step(Time.deltaTime, rate, _skel.Visible && (!far || Time.frameCount % 3 == SitSlot));
        }

        // the animator has it sitting (its idle, no transition, not "away"): the clips can take over from here
        private bool AnimatorSits()
        {
            if (!_anim.isInitialized || _anim.IsInTransition(0)) return false;
            AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsTag("away")) return false;
            AnimatorClipInfo[] ci = _anim.GetCurrentAnimatorClipInfo(0);
            return ci != null && ci.Length > 0 && ci[0].clip != null && ci[0].clip.name.ToLowerInvariant().Contains("idle");
        }

        // it speaks: the beak and the body as the game's raven talks (over its sitting, or the animator's trigger)
        internal void Talk()
        {
            if (SitNew) { _skel.Play(SitClip.Talk, SitSystem.TalkFadeIn); return; }
            if (_anim != null) _anim.SetTrigger("talk");
        }

        // a raven speaks (anyone's; its model or its animator): its life if it has one (the sitting system plays it),
        // else the game's animator
        internal static void TalkOn(Component c)
        {
            if (c == null) return;
            ShoulderLife l = c.GetComponentInParent<ShoulderLife>();
            if (l != null) { l.Talk(); return; }
            Animator a = c as Animator ?? c.GetComponentInChildren<Animator>();
            if (a != null) a.SetTrigger("talk");
        }

        // ---------------------------------------------------------------- layer 4: characters
        // Hugin: calm and proud, slow smooth head, long looks. Munin: hunched and restless, snapping head, short looks,
        // and he clacks his beak at enemies. Low trust makes both more nervous (busier, jumpier), high trust calmer.
        internal static float HeadEaseHugin = 0.28f, HeadEaseMunin = 0.05f;  // s: how quickly the head eases into a turn
        internal static float HuginUpright = 4f, MuninHunch = 8f;            // ° the back leans back / forward
        internal static float ClackOpen = 22f, ClackEvery = 3.5f;            // ° the beak opens; s between bursts
        private BoneTurn _jawB;
        private bool _jawLooked;
        private float _clackNext, _clackAt = -10f;
        private int _clackN;

        // 1.35 at trust level 1 (jumpy) down to 0.8 at level 7 (settled)
        // × its character (Norov: a fidget busier, a calm one quieter), looked up once a second
        internal float Nerves
        {
            get
            {
                if (Time.time >= _norovNext) { _norovNext = Time.time + 1f; _norovNerves = Norov.NervesMul(Player, Munin, Level); }
                return Mathf.Lerp(1.35f, 0.8f, Mathf.Clamp01((Level - 1) / 6f)) * _norovNerves;
            }
        }
        private float _norovNerves = 1f, _norovNext;

        private void Clack(Vector3 right)
        {
            float beak = SitNew ? Mathf.Max(0f, SitRow(10)) : 0f; // a sitting template's beak (begging chatters, cheering wide)
            float beakHz = SitNew && _sitDir.Run != null ? _sitDir.Run.T.BeakHz : 0f;
            if (!Munin && beak <= 0.01f) return;
            if (!_jawLooked) { _jawLooked = true; foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "Jaw") { _jawB = new BoneTurn(t); break; } }
            if (_jawB == null) return;
            bool foe = Munin && _foe != null && !_foe.IsDead() && !_foe.IsPlayer() && !Droopy && !AnimDirector.Busy(this);
            if (foe && Time.time >= _clackNext)
            {
                _clackAt = Time.time; _clackN = Random.Range(2, 5);
                _clackNext = Time.time + Random.Range(0.7f, 1.4f) * ClackEvery / Nerves;
            }
            float t0 = Time.time - _clackAt, one = 0.11f;
            _jawB.Begin();
            if (t0 < _clackN * one)
            {
                float ph = (t0 % one) / one;                      // open and shut, quick
                _jawB.Turn(Quaternion.AngleAxis(Mathf.Sin(ph * Mathf.PI) * ClackOpen, right));
            }
            else if (beak > 0.01f) _jawB.Turn(Quaternion.AngleAxis(beak * (beakHz > 0f ? 0.55f + 0.45f * Mathf.Sin(Time.time * beakHz * Mathf.PI * 2f) : 1f), right));
            _jawB.End();
        }

        // ---------------------------------------------------------------- layer 3: life on the shoulder
        internal static float BalanceOut = 30f, BalanceUp = 8f, BalanceElbow = 18f; // wings a little open, at full run
        internal static float HopDip = 0.025f, HopUp = 0.04f;                      // m: landing after a jump
        internal static float StepSize = 0.025f;                                   // a step aside, m
        internal Vector3 PinNudge;                                                 // the pin adds this to the seat
        private float _hopAt = -10f, _stepAt = -10f, _stepFrom, _stepTo, _stepNow;
        private float _twitchNext, _twitchAt = -10f, _twitchDeg;
        private float _jumpFlapAt, _jumpFlapLen;
        private float _jumpStarted = -1f, _jumpUntil, _jumpCycles, _jumpStrength;

        // ---- the body on the seat (SitBody): the seat's motion leans it, presses it down, turns it after you
        private readonly SitBody _sitBody = new SitBody();
        // the way it faces on the seat (the pin asks): after you on its own spring while it sits, else at once
        internal float SitYaw(float now) { return SitNew && _sitBody.Live ? _sitBody.Yaw : now; }

        private void SitLean()
        {
            if (Player == null) return;
            Vector3 f = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
            if (f.sqrMagnitude < 0.0001f) return;
            f.Normalize();
            _sitBody.Step(Player.transform.position, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, Time.deltaTime, Munin, Level, Asleep);
            // round its feet (the model's own place): forward or back, to its left or right
            Quaternion face = Quaternion.Euler(0f, _sitBody.Yaw, 0f);
            transform.rotation = Quaternion.AngleAxis(_sitBody.Pitch, face * Vector3.right) * Quaternion.AngleAxis(_sitBody.Roll, face * Vector3.forward) * transform.rotation;
        }

        // a knock its feet could not hold (0..1): a few beats of its own, Munin more
        private void JoltWings(float strength)
        {
            _jumpFlapAt = Time.time + (Munin ? Random.Range(0.02f, 0.12f) : Random.Range(0f, 0.08f));
            _jumpFlapLen = Mathf.Lerp(0.25f, Munin ? 0.9f : 0.6f, strength) * Random.Range(0.85f, 1.15f);
            _jumpCycles = Mathf.Lerp(1.1f, Munin ? 2.6f : 2f, strength);
            _jumpStrength = Mathf.Lerp(0.5f, Munin ? 1.05f : 0.9f, strength);
        }

        // In flight the head looks about (Hugin slowly and long, Munin in quick glances): at a foe near you, now and then
        // at you, else a glance aside or down at the ground. Not upside down in a figure, not for a raven far from you.
        internal static float FlyLookYaw = 60f, FlyLookPitch = 35f;
        private float _flLookNext, _flYaw, _flPitch, _flWantYaw, _flWantPitch;
        private Character _flFoe;
        private bool _flAtOwner;
        private void FlightLook(Quaternion facing, float upright)
        {
            Player me = Player.m_localPlayer;
            if (Player == null || (!ReferenceEquals(Player, me) && me != null && (transform.position - me.transform.position).sqrMagnitude > 2500f)) return;
            if (_flyClock >= _flLookNext || _flLookNext - _flyClock > 10f)
            {
                _flLookNext = _flyClock + (Munin ? Random.Range(0.6f, 1.6f) : Random.Range(1.5f, 3.5f)) / Nerves;
                _flFoe = ReferenceEquals(Player, me) && Senses.Ready && Senses.Foe != null && Random.value < 0.7f ? Senses.Foe : null;
                _flAtOwner = _flFoe == null && Random.value < 0.35f;
                if (_flFoe == null && !_flAtOwner) { _flWantYaw = Random.Range(-45f, 45f); _flWantPitch = Random.Range(0f, 30f); }
            }
            Vector3 at = Vector3.zero;
            bool has = false;
            if (_flFoe != null && !_flFoe.IsDead()) { at = Body.Center(_flFoe); has = true; }
            else if (_flAtOwner) { at = Player.GetHeadPoint(); has = true; }
            if (has)
            {
                Vector3 dir = Quaternion.Inverse(facing) * (at - _head.position);
                if (dir.sqrMagnitude > 0.01f) { _flWantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; _flWantPitch = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg; }
            }
            float a = 1f - Mathf.Exp(-Dt / Mathf.Max(0.02f, (Munin ? 0.08f : 0.3f) / Nerves)); // eased (a constant-speed turn reads as a twitch)
            _flYaw = Mathf.Lerp(_flYaw, Mathf.Clamp(_flWantYaw, -FlyLookYaw, FlyLookYaw) * upright, a);
            _flPitch = Mathf.Lerp(_flPitch, Mathf.Clamp(_flWantPitch, -FlyLookPitch, FlyLookPitch) * upright, a);
            _head.rotation = Quaternion.AngleAxis(_flYaw, facing * Vector3.up) * Quaternion.AngleAxis(_flPitch, facing * Vector3.right) * _head.rotation;
        }

        // in flight: the head stays level while the body banks; the tail turns into the turn
        private void FlightSmall()
        {
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 fwd = facing * Vector3.forward, up = facing * Vector3.up;
            Vector3 a = Vector3.ProjectOnPlane(Vector3.up, fwd), b = Vector3.ProjectOnPlane(up, fwd);
            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f) return;
            float roll = Vector3.SignedAngle(a, b, fwd); // how far the body is banked
            // only an ordinary bank is taken out: upside down or nose straight up/down (a loop, a roll) the "bank" jumps
            // through ±180° or means nothing, and the head would spin round in a frame; there it goes with the body
            float upright = Mathf.Clamp01((up.y - 0.2f) / 0.5f) * Mathf.Clamp01((0.95f - Mathf.Abs(fwd.y)) / 0.25f);
            roll = Mathf.Clamp(roll, -60f, 60f) * upright;
            if (!_smallFound) FindSmall();
            float phase = WingKeys.KeyX(_flyT - Mathf.Floor(_flyT));
            float stroke = Mathf.Cos(Mathf.PI * Mathf.Repeat(phase, WingKeys.Keys) / WingKeys.Half);
            float climb = Mathf.Clamp(Flight.Climb, -1f, 1f);
            float thrust = Mathf.Clamp(Flight.Thrust, -1f, 1f);
            float effort = Mathf.Clamp01(Flight.Effort);
            float dive = Mathf.Clamp01(Flight.Dive);
            float torsoPitch = Mathf.Clamp(-climb * 2.2f + thrust * 1.5f + dive * 2.5f + stroke * (2.2f + effort * 2.4f), -9f, 9f);
            float dt = Dt;
            float neckWant = -torsoPitch * 0.42f + Mathf.Cos(Mathf.PI * Mathf.Repeat(phase - 1.4f, WingKeys.Keys) / WingKeys.Half) * (0.8f + effort * 0.8f);
            _flightNeckPitch = Mathf.Lerp(_flightNeckPitch, neckWant, 1f - Mathf.Exp(-dt * 4f));
            if (_breathB != null)
            {
                _breathB.Begin();
                _breathB.Turn(Quaternion.AngleAxis(torsoPitch, facing * Vector3.right)
                    * Quaternion.AngleAxis(Mathf.Clamp(roll * 0.08f, -3f, 3f), fwd));
                _breathB.End();
            }
            if (_neckRootB != null)
            {
                _neckRootB.Begin();
                _neckRootB.Turn(Quaternion.AngleAxis(_flightNeckPitch, facing * Vector3.right)
                    * Quaternion.AngleAxis(Mathf.Clamp(-roll * 0.18f, -5f, 5f), fwd));
                _neckRootB.End();
            }
            if (_head != null)
            {
                _head.rotation = Quaternion.AngleAxis(-roll * HeadLevel, fwd)
                    * Quaternion.AngleAxis(-torsoPitch * 0.42f, facing * Vector3.right) * _head.rotation;
                if (!Hovering) FlightLook(facing, upright); // hanging over the shoulder the head is Nod's (both at once turned it twice)
            }
            if (_tailB != null && _tailB.T != null)
            {
                _tailB.Begin();
                _tailB.Turn(Quaternion.AngleAxis(Mathf.Clamp(-roll * 0.35f, -TailMax, TailMax), up)
                    * Quaternion.AngleAxis(Mathf.Clamp(-torsoPitch * 0.65f - thrust * 2f, -TailMax, TailMax), facing * Vector3.right));
                _tailB.End();
            }
        }

        // braking to land: both wings swung forward and a little up, on top of the beat, by the body's own axes
        private void FlareWings()
        {
            if (Flare <= 0.01f) return;
            Quaternion facing = transform.rotation * Quaternion.Inverse(Quaternion.Euler(Shoulder.Rotation));
            Vector3 fwd = facing * Vector3.forward, up = facing * Vector3.up;
            float o = FlareOut * Flare, u = FlareUp * Flare;
            if (_left != null && _left.A.T != null) _left.A.T.rotation = Quaternion.AngleAxis(-u, fwd) * Quaternion.AngleAxis(o, up) * _left.A.T.rotation;
            if (_right != null && _right.A.T != null) _right.A.T.rotation = Quaternion.AngleAxis(u, fwd) * Quaternion.AngleAxis(-o, up) * _right.A.T.rotation;
        }

        // ---------------------------------------------------------------- the stroke (a wave along the wing)
        internal static float StrokeLagElbow = 0.7f, StrokeLagHand = 1.3f;   // rad the elbow / hand follow the shoulder
        internal static float StrokeFlexElbow = 0.35f, StrokeFlexHand = 0.45f; // their own lift, share of the shoulder's
        internal static float StrokeFoldElbow = 22f, StrokeFoldHand = 26f;   // ° it folds in on the way up
        internal static float StrokeSweep = 12f;                             // ° forward on the way down, back on the way up
        internal static float StrokeTwist = 14f, StrokeTwistSign = 1f;       // ° the hand turns its edge down on the way down
        private float _ph, _amp, _ampL = 1f, _ampR = 1f, _skew;
        private int _strokeMode;

        // the beat goes on round and round (its pace may change, it never jumps); a new series of beats (a jump, a
        // flap) from stillness starts at the top of a stroke, each wing a touch off the other, sizes eased
        private void Stroke(float hz, float amp, int mode, float k)
        {
            if (mode != _strokeMode)
            {
                if (mode != 0 && _amp < 3f)
                {
                    _ph = Mathf.PI * 0.5f; // from the top: the first move is down (the push)
                    _shT = StrokeTops != null && StrokeTops.Length > 0 ? StrokeTops[0] : 0f; // (the engine's beat: the clip's top)
                    _skew = Random.Range(-0.12f, 0.12f);
                    _ampL = Random.Range(0.94f, 1.06f); _ampR = Random.Range(0.94f, 1.06f);
                    _strokeBack = Random.Range(0f, StrokeBackMax); // (this series a little back, never forward)
                }
                _strokeMode = mode;
            }
            if (hz > 0f) _strokeHz = hz;
            _ph = Mathf.Repeat(_ph + Time.deltaTime * _strokeHz * Mathf.PI * 2f, Mathf.PI * 2f);
            _amp = Mathf.Lerp(_amp, amp, mode == 3 ? 1f : k); // (a jump's own swell shapes it already)
        }
        private float _strokeHz = 3f;

        // side: +1 the left wing, -1 the right one (mirrored); the inner one gets the extra of the gift's pose
        // a held pose (balance, a gift's, the fury's, a glide): the elbow and the hand come a moment after the shoulder
        // (a wave along the wing), and the more the wing opens, the more the hand bends back at the wrist (never a plank)
        internal static float ElbowFollow = 0.55f, HandFollow = 0.45f, WristBend = 0.4f;
        internal static float PoseBackMax = 0.35f, StrokeBackMax = 12f; // share of a pose's opening / ° of a series of beats held back
        private float _poseBack, _strokeBack; private bool _poseOpen;
        private float _elbowB, _elbowC, _outC, _inElbowB, _inElbowC, _inOutC;

        private void PoseWing(Wing w, float side, Vector3 fwd, bool inner)
        {
            if (w == null) return;
            float o = _out + (inner ? _inOut : 0f), u = _up + (inner ? _inUp : 0f);
            float eB = _elbowB + (inner ? _inElbowB : 0f), eC = _elbowC + (inner ? _inElbowC : 0f), oC = _outC + (inner ? _inOutC : 0f);
            // the stroke on this wing: its own small lead or lag and size (two wings never beat as one)
            float amp = _amp * (side > 0f ? _ampL : _ampR), ph = _ph + (side > 0f ? _skew : -_skew);
            bool beating = amp > 0.3f;
            bool rest = Mathf.Abs(o) < 0.2f && Mathf.Abs(u) < 0.2f && Mathf.Abs(eB) < 0.2f && Mathf.Abs(eC) < 0.2f && Mathf.Abs(oC) < 0.2f && !beating;
            w.A.Begin(); if (w.B != null) w.B.Begin(); if (w.C != null) w.C.Begin();
            if (!rest)
            {
                float sweep = 0f, lift = 0f, eLift = 0f, hLift = 0f, eFold = 0f, hFold = 0f, twist = 0f;
                if (beating)
                {
                    // A beat is a wave along the wing, not a plate: the shoulder leads, the elbow and the hand follow a
                    // moment later (the tip trails, the wing curves); on the way up it half folds at the wrist and
                    // comes back swept, on the way down it opens, reaches forward and the hand turns its edge down.
                    float n = Mathf.Clamp(amp / Mathf.Max(1f, FlapBeat), 0f, 1.6f);
                    lift = Mathf.Sin(ph) * amp;
                    sweep = -Mathf.Cos(ph) * StrokeSweep * n;                       // forward on the downstroke
                    eLift = Mathf.Sin(ph - StrokeLagElbow) * amp * StrokeFlexElbow;
                    hLift = Mathf.Sin(ph - StrokeLagHand) * amp * StrokeFlexHand;
                    float up = Mathf.Max(0f, Mathf.Cos(ph - 0.35f));                 // the upstroke (lift rising)
                    eFold = up * up * StrokeFoldElbow * n;
                    hFold = up * up * StrokeFoldHand * n;
                    twist = Mathf.Min(0f, Mathf.Cos(ph - 0.2f)) * StrokeTwist * n;   // the downstroke: edge down
                }
                Quaternion qa = Quaternion.AngleAxis(-side * (u + lift), fwd) * Quaternion.AngleAxis(side * (o + sweep), Vector3.up);
                w.A.Turn(qa);
                Vector3 chord = qa * fwd, normal = qa * Vector3.up; // the wing's own axes after the shoulder
                if (w.B != null)
                {
                    w.B.Turn(Quaternion.AngleAxis(side * eB, Vector3.up));
                    if (beating) w.B.Turn(Quaternion.AngleAxis(-side * eLift, chord) * Quaternion.AngleAxis(-side * eFold, normal));
                }
                if (w.C != null)
                {
                    w.C.Turn(Quaternion.AngleAxis(side * (eC * 0.6f - oC * WristBend), Vector3.up)); // (bent back at the wrist)
                    if (beating)
                    {
                        Vector3 span = w.B != null && w.B.T != null ? w.C.T.position - w.B.T.position : Vector3.zero;
                        Quaternion tw = span.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(side * twist * StrokeTwistSign, span.normalized) : Quaternion.identity;
                        w.C.Turn(tw * Quaternion.AngleAxis(-side * hLift, chord) * Quaternion.AngleAxis(-side * hFold, normal));
                    }
                }
            }
            w.A.End(); if (w.B != null) w.B.End(); if (w.C != null) w.C.End();
        }


        // the turn towards what it watches (limited, a raven cannot look behind itself), or a glance, or ahead
        private Quaternion Turn(Vector3 fwd, Vector3 right)
        {
            Vector3 at = Vector3.zero;
            bool has = false;
            if (_foe != null && !_foe.IsDead()) { at = Body.Eye(_foe); has = true; }
            else if (_thing != null) { at = _thing.position + Vector3.up * 0.3f; has = true; }
            float yaw, pitch;
            if (has)
            {
                Vector3 dir = at - _head.position;
                if (dir.sqrMagnitude < 0.01f) { _lookOn = false; return Quaternion.identity; }
                Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
                yaw = flat.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(fwd, flat, Vector3.up) : 0f;
                pitch = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg; // positive: down
            }
            else
            {
                if (Time.time >= _nextGlance)
                {
                    _glanceYaw = Random.Range(-MaxYaw, MaxYaw);
                    _glancePitch = Random.Range(-15f, 30f);
                    _glanceUntil = Time.time + (Munin ? Random.Range(0.25f, 0.8f) : Random.Range(2.5f, 5f));
                    _nextGlance = _glanceUntil + Random.Range(3f, 8f) * (Munin ? 0.35f : 2f) / Nerves * (SitNew ? _sitDir.GlanceMul : 1f);
                }
                if (Time.time >= _glanceUntil) { _lookOn = false; return Quaternion.identity; }
                yaw = _glanceYaw; pitch = _glancePitch;
            }
            yaw = Mathf.Clamp(yaw, -MaxYaw, MaxYaw);
            pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            _lookOn = true; _lookYaw = yaw; _lookPitch = pitch; // (the sitting system sends this to the other games)
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right);
        }
        private bool _lookOn;
        private float _lookYaw, _lookPitch;

        // at a point, as Turn would (limited)
        private Quaternion LookAt(Vector3 at, Vector3 fwd, Vector3 right)
        {
            Vector3 dir = at - _head.position;
            if (dir.sqrMagnitude < 0.0004f) return Quaternion.identity;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            float yaw = Mathf.Clamp(flat.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(fwd, flat, Vector3.up) : 0f, -MaxYaw, MaxYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -MaxPitch, 50f); // (down at your hands: a little lower than a glance)
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right);
        }

        // The sitting system's look: your game picks where it looks (what Turn sees) and sends it; another player's raven
        // looks where its owner's game says. An act may look at your face (begging) or your hands (watching your work,
        // what you picked up), on every game alike. Its mood lifts or drops the head, a curious one tilts it.
        private Quaternion SitLook(Vector3 fwd, Vector3 right)
        {
            Quaternion w;
            if (ReferenceEquals(Player, Player.m_localPlayer)) { w = Turn(fwd, right); _sitDir.SetLook(_lookOn, _lookYaw, _lookPitch); }
            else w = _sitDir.LookOn ? Quaternion.AngleAxis(_sitDir.LookYaw, Vector3.up) * Quaternion.AngleAxis(_sitDir.LookPitch, right) : Quaternion.identity;
            SitRun p = _sitDir.Run;
            float aw = p != null ? _sitDir.ActW : 0f;
            if (aw > 0.001f && p.T.Look == 1) w = Quaternion.Slerp(w, LookAt(Player.GetHeadPoint(), fwd, right), aw);
            else if (aw > 0.001f && p.T.Look == 2) w = Quaternion.Slerp(w, LookAt(Player.transform.position + fwd * 0.55f + Vector3.up * 0.95f, fwd, right), aw);
            else if (aw > 0.001f && p.T.Look >= 3)
            {
                // the other raven (on its own shoulder, on every game): at it, or away from it
                ShoulderLife o = Shoulder.LifeOf(Player, !Munin);
                if (o != null) { Vector3 at = o.HeadPoint; if (p.T.Look == 4) at = _head.position * 2f - at; w = Quaternion.Slerp(w, LookAt(at, fwd, right), aw); }
            }
            if (_sitDir.Doze > 0.001f) w = Quaternion.Slerp(w, Quaternion.identity, _sitDir.Doze); // (nodding off: it stops looking about)
            float pitch = _sitDir.MoodHeadPitch;
            float roll = _sitDir.LookOn ? -Mathf.Sign(_sitDir.LookYaw) * 10f * _sitDir.MoodW[(int)SitMood.Curious] : 0f;
            w = w * Quaternion.AngleAxis(pitch, right);
            if (Mathf.Abs(roll) > 0.01f) w = Quaternion.AngleAxis(roll, w * fwd) * w;
            return w;
        }

        // what to watch: a boss first, then the nearest enemy, then what you look at, then a player close by
        private void Search()
        {
            _foe = null; _thing = null;
            Vector3 me = Player.transform.position;
            Character boss = null, enemy = null;
            if (ReferenceEquals(Player, Player.m_localPlayer))
            {
                // yours: what is around is already known (Senses, once a second for both ravens and everything else)
                boss = Senses.Boss; enemy = Senses.Foe;
                if (boss != null && boss.IsDead()) boss = null;
                if (enemy != null && enemy.IsDead()) enemy = null;
            }
            else
            {
                SitSense.Around(Player, out boss, out enemy); // another player's: looked at once for both their ravens
            }
            if (boss != null) { _foe = boss; SawBoss(boss); return; }
            if (enemy != null) { _foe = enemy; return; }
            Transform talk = CrossBubble.LookFor(Player, Munin); // in a talk with another player's raven: at the one speaking (or spoken to)
            if (talk != null) { _thing = talk; return; }
            if (ReferenceEquals(Player, Player.m_localPlayer))
            {
                GameObject hover = Player.GetHoverObject();
                if (hover != null && Vector3.Distance(hover.transform.position, me) < 8f) { _thing = hover.transform; return; }
            }
            Player near = null;
            float best = PlayerRange;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == Player) continue;
                float d = Vector3.Distance(o.transform.position, me);
                if (d < best) { best = d; near = o; }
            }
            if (near != null) _foe = near;
        }

        // a boss appears: a loud caw (on your own raven only, once for each boss)
        private void SawBoss(Character boss)
        {
            if (!ReferenceEquals(Player, Player.m_localPlayer) || boss.GetZDOID() == _bossSeen) return;
            _bossSeen = boss.GetZDOID();
            SitEvents.Raise(SitEv.Boss); // it bristles (the sitting director)
            Caw(Munin ? 0.8f : 1.15f, 0.9f);
        }

        // You got hurt: Hugin cries out, Munin snarls. A real blow startles it; health trickling away (cold in a storm,
        // poison, fire) only now and then makes it shift its wings, without a word: it would flap and caw all the time.
        internal static float BlowShare = 0.03f;                       // of your most health: less is a trickle
        internal static float BlowFlapEvery = 3f, BlowCawEvery = 12f, TrickleFlapEvery = 15f;

        internal void Hurt(float lost, float maxHealth)
        {
            if (Level <= 0) return;
            bool blow = lost >= maxHealth * BlowShare;
            if (blow && SitNew) SitEvents.Raise(SitEv.Hurt); // the sitting director startles it (its kick, its beats: none added here)
            if (Time.time >= _nextStartle && !(blow && SitNew))
            {
                _nextStartle = Time.time + (blow ? BlowFlapEvery : TrickleFlapEvery);
                Flap(1f);
            }
            if (!blow || Time.time < _nextCaw) return;
            _nextCaw = Time.time + BlowCawEvery;
            Caw(Munin ? 0.85f : 1.35f, Munin ? 0.55f : 0.4f);
        }

        private void Caw(float pitch, float volume)
        {
            Talk();
            if (Player != null) ScreamFx.Caw(Player, Munin, pitch, volume);
        }

        // once: the raven's bones in the log (for the wing flaps that come next)
        private void LogBones()
        {
            if (_bonesLogged || Plugin.Instance == null) return;
            _bonesLogged = true;
            StringBuilder sb = new StringBuilder("Shoulder raven bones (head: " + (_head != null ? _head.name : "not found") + "):");
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) sb.Append(' ').Append(t.name);
            Plugin.Instance.Log(sb.ToString());
        }
    }
}
#endif

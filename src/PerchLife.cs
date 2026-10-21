#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Життя на жердині»: a raven sitting on a perch, over its pose (PerchPose calls ResetBones before the clips and
    // Apply after the legs).
    //  - what it does: the sitting system's (its director, the sitting templates: preening, a shake, a step along the
    //    bar, a wing stretched, a nudge at the other raven...), with a rest between any two (Hugin 14 s, Munin 6 s,
    //    ÷ nerves by trust); now and then it gets up on the bar for a while; asleep at night (head down, slow breath,
    //    nothing of its own; wakes when someone comes close);
    //  - looks: about now and then, at the other raven, at players who come near (its owner gets a caw; a stranger is
    //    watched, Munin puffs up at him), at an enemy (alert: wings half open, Munin caws at it).
    // Nothing of it runs with nobody within Near m: there it just sits.
    internal class PerchLife : MonoBehaviour, ISitBird
    {
        // ---------------------------------------------------------------- the sitting system on a perch
        // With the sitting system on, what it does here is the sitting director's (the same templates as on a shoulder:
        // its own doings, the pair's with the other raven here), decided by whoever holds the perch and written on the
        // perch (one number a seat), played alike on every game; the clips are its own (RavenSkeleton). Its body on the
        // perch stays the perch's: the legs round the bar, sitting or standing, turning round, the step along the bar, its
        // looks at who comes near, asleep at night.
        private readonly SitDirector _sitDir = new SitDirector();
        private readonly SitPoser _poser = new SitPoser();
        private RavenSkeleton _skel;
        private bool _skelLooked;
        private float _sitBusyUntil, _ownerAt = -1f;
        private Player _ownerNow;
        private static readonly int[] SeatKeys = { "RavenWatch_SitP0".GetStableHashCode(), "RavenWatch_SitP1".GetStableHashCode(), "RavenWatch_SitP2".GetStableHashCode(), "RavenWatch_SitP3".GetStableHashCode() };

        // the clips are the sitting system's here (not in the test pictures, not held still for the book)
        internal bool Composes
        {
            get
            {
                if (Stage || Pose == null || Pose.Neutral) return false;
                if (!_skelLooked)
                {
                    _skelLooked = true;
                    Animator a = GetComponentInChildren<Animator>(true);
                    if (a != null && SitClips.Ready(a)) { _skel = new RavenSkeleton(a, Munin); _skel.Begin(); }
                }
                return _skel != null;
            }
        }
        // one frame of its clips (PerchPose, where the game's idle was played)
        internal void ComposeStep() { _skel.Step(Time.deltaTime, Pose.Rate * _sitDir.RateMul, true); }
        internal SitDirector SitDirectorNow { get { return _sitDir; } }
        private float SitW { get { return Composes ? _sitDir.ActW : 0f; } }
        private float SitRow(int bone) { SitRun r = _sitDir.Run; float w = SitW; return r != null && w > 0f ? r.Pose(_sitDir.ActT, bone).x * w : 0f; }

        bool ISitBird.BirdMunin { get { return Munin; } }
        int ISitBird.BirdLevel { get { return Level; } }
        float ISitBird.BirdNerves { get { return Nerves; } }
        bool ISitBird.BirdAsleep { get { return _asleep; } }
        bool ISitBird.BirdDroopy { get { return _asleep || _foe != null || _turnAt >= 0f || _crouchWant > 0f; } } // (an enemy near, turning round, about to leave: nothing of its own)
        float ISitBird.BirdSpeed { get { return 0f; } }
        bool ISitBird.BirdOnPerch { get { return true; } }
        Player ISitBird.BirdOwner { get { if (Time.time >= _ownerAt) { _ownerAt = Time.time + 1f; _ownerNow = PerchView.PlayerOf(Owner); } return _ownerNow; } }
        bool ISitBird.BirdDecides { get { return View != null && View.Owner; } }
        bool ISitBird.BirdHears { get { return false; } } // (what you do is for the ravens on you)
        string ISitBird.BirdQuirk { get { return ""; } }   // (in its owner's bag, not known to the perch)
        ZDO ISitBird.BirdZdo(bool write) { return View == null || (write && !View.Owner) ? null : View.Zdo; }
        int ISitBird.BirdKey { get { return SeatKeys[Mathf.Clamp(Seat, 0, SeatKeys.Length - 1)]; } }
        ISitBird ISitBird.BirdPartner
        {
            get
            {
                Transform nb = View != null ? View.Neighbour(Seat) : null;
                PerchLife p = nb != null ? nb.GetComponent<PerchLife>() : null;
                return p != null && p.Composes && !p._asleep && p._foe == null ? p : null;
            }
        }
        SitDirector ISitBird.BirdDir { get { return _sitDir; } }
        bool ISitBird.BirdHasClip(SitClip k) { return _skel != null && _skel.Has(k); }
        float ISitBird.BirdClipLength(int clip) { return _skel != null && clip >= 0 ? _skel.Length((SitClip)clip) : 0f; }
        bool ISitBird.BirdMayDo() { return Time.time - _lastAny >= (Munin ? RestMunin : RestHugin) / Nerves && Time.time >= _sitBusyUntil; }
        void ISitBird.BirdDid(float seconds, bool reaction) { _lastAny = Time.time; _sitBusyUntil = Time.time + seconds; }
        void ISitBird.BirdMood(out bool alert, out bool drowsy) { alert = _foe != null; drowsy = _asleep; }

        // now and then it gets up on the bar for a while (its body's, not a template: the perch's own sitting and standing)
        private void StandUp()
        {
            if (_sitWant < 1f || _standUntil >= 0f || _asleep || _foe != null || _turnAt >= 0f || !May(5, StandRest) || Random.value > 0.08f) return;
            _last[5] = Time.time;
            _sitWant = 0f; _standUntil = Time.time + Random.Range(StandMin, StandMax);
        }

        // a template begins here, 'age' s in: its clip, its shake or its step along the bar, its caw
        void ISitBird.BirdPlay(SitRun r, float age)
        {
            if (_skel != null && r.Clip >= 0) _skel.Play((SitClip)r.Clip, SitSystem.FadeIn, age);
            if (r.T.Shake) { _act = Act.Shake; _actAt = Time.time - age; _actLen = 0.45f; }
            else if (r.T.Step)
            {
                _act = Act.Step; _actAt = Time.time - age;
                _stepFrom = _stepX;
                _stepWant = Mathf.Clamp(_stepX + (r.Seed % 2 == 0 ? -1f : 1f) * 0.03f * r.Amp, -0.05f, 0.05f);
                _walkSteps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(_stepWant - _stepFrom) / WalkStride), 2, 4);
                _actLen = _walkSteps * WalkStepSeconds;
            }
            if (r.Caw && age < 0.25f) Caw(r.CawPitch, r.T.CawVolume);
        }

        internal bool Munin;
        internal int Level = 1;
        internal long Owner;
        internal int VoiceCode = -1; // its own voice (Voice), from the seat's record
        internal PerchView View;
        internal int Seat;
        internal PerchPose Pose;

        internal static float Near = 30f, OwnerNear = 8f, StrangerNear = 6f, FoeNear = 15f;
        internal static float RestHugin = 14f, RestMunin = 6f;
        internal static float HeadEaseHugin = 0.28f, HeadEaseMunin = 0.05f;
        internal static float AlertWing = 25f, SleepHead = 35f, SleepHunch = 15f;

        private float Nerves { get { return Mathf.Lerp(1.35f, 0.8f, Mathf.Clamp01((Level - 1) / 6f)); } }

        private sealed class Bone { public Transform T; public Quaternion Rest; }
        private Bone _neck, _jaw, _spine, _wingL, _wingR, _handL, _handR;
        private bool _found;
        private Bone[] _all;
        internal bool Active { get { return _active; } }

        private enum Act { None, Shake, Step } // a sitting template's shake or step along the bar, as it plays
        private Act _act;
        private float _actAt, _actLen, _lastAny = -999f;
        private readonly float[] _last = { -999f, -999f, -999f, -999f, -999f, -999f };
        private float _stepX, _stepWant, _stepFrom;
        private int _walkSteps = 2;
        // along the bar: side-steps (the foot on the side it goes to first, the other drawn after), the body moving on
        // while a foot is up, its weight over the standing one
        internal static float WalkStride = 0.012f, WalkStepSeconds = 0.32f, WalkLift = 0.7f, WalkRoll = 4f;
        // asleep on the bar: deeper into its legs, fluffed up, the head low and turned a little to its back; now and
        // then it half wakes (the head up a moment) and sinks back
        internal static float SleepCrouch = 0.35f, SleepPuff = 0.7f, SleepYaw = 28f, PeekMin = 25f, PeekMax = 50f;
        private float _sleepW, _sleepSide = 1f, _peekAt = -1f, _peekUntil = -1f;

        private float _nextScan;
        private bool _active, _asleep, _ownerWasNear;
        private Transform _look;          // what it looks at now (or none: its own glances)
        // pecking at what it sits on (a berry bush: WorldVisit): dips this deep, this often, eased in and out
        internal static float PeckDip = 38f, PeckHz = 2.2f, PeckOpen = 14f;
        private float _peckFrom = -1f, _peckLen;
        internal void Peck(float seconds) { _peckFrom = Time.time; _peckLen = Mathf.Max(0.5f, seconds); }
        // 0..1 how far down the head is now in the pecking (0: not pecking)
        private float PeckNow()
        {
            if (_peckFrom < 0f) return 0f;
            float t = Time.time - _peckFrom;
            if (t >= _peckLen) { _peckFrom = -1f; return 0f; }
            float edge = Mathf.Clamp01(Mathf.Min(t, _peckLen - t) / 0.35f); // into it and out of it softly
            return edge * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * PeckHz * Mathf.PI)));
        }
        private Vector3 _glance;          // a point it glances at by itself, in the seat's frame
        private float _nextGlance, _lookUntil;
        private Character _foe;
        private Player _stranger;
        private float _yaw, _pitch, _yawMid, _pitchMid;
        private readonly Saccade _sac = new Saccade(); // the head's jumps from look to look
        private float _jawUntil, _jawLen = 0.3f, _nextFoeCaw;
        private float _puff, _lookUp;
        private Transform _lookWas;
        internal static float WakeMin = 15f, WakeMax = 30f; // s awake after somebody comes close at night
        private float _wakeUntil;
        private Player _strangerWas;
        internal bool Asleep { get { return _asleep; } }

        // the perch's hover line: what it is doing now
        internal string State()
        {
            if (!_active) return "";
            if (_foe != null) return Lang.T("насторожі");
            if (_asleep) return Lang.T("дрімає");
            if (Composes && _sitDir.Playing) return _sitDir.Run.T.Name.ToLowerInvariant();
            if (Pose != null && Pose.Sit < 0.5f) return Lang.T("стоїть");
            return Lang.T("сидить");
        }

        // standing or sitting: mostly it sits; after landing it stands a moment first; now and then it gets up for a
        // while; an enemy near gets it up; asleep it sits
        internal static float StandAfterLandMin = 1.5f, StandAfterLandMax = 3.5f, StandRest = 90f, StandMin = 8f, StandMax = 25f, SitSeconds = 0.8f;
        private float _sitWant = 1f, _standUntil = -1f;

        // ---- turning round on the bar (a little hop, wings flicked, 180°), and crouching to push off

        // the turn's keys and time: PerchTurn (tuned in the test window, tab «Розворот»)
        internal static float FaceCheckAfter = 1.2f, FaceEvery = 30f, FaceNear = 12f; // after landing, then this often while you are this near
        private float _turnAt = -1f, _turnSign = 1f, _crouchWant, _faceCheckAt = -1f, _turnWings, _turnHead;
        private bool _preview;

        // the perch turned it round (its facing flipped): it is hung under the new facing as it is now and turns into it
        internal void TurnTo(Transform face)
        {
            transform.SetParent(face, true);
            _stepX = -_stepX; _stepWant = -_stepWant; _glance.x = -_glance.x; _glance.z = -_glance.z; // the seat's frame turned round
            _turnSign = Random.value < 0.5f ? 1f : -1f; // left or right about
            _preview = false;
            _turnAt = Time.time;
            if (Pose != null) Pose.Yaw = _turnSign * 180f;
        }

        // the test window: the turn played here without turning the seat (it ends as it sits now)
        internal void StartPreview(bool left)
        {
            _turnSign = left ? -1f : 1f;
            _preview = true;
            _turnAt = Time.time;
        }

        internal static string Preview(bool left)
        {
            Player me = Player.m_localPlayer;
            if (me == null) return "";
            PerchLife best = null; float d = 30f;
            foreach (PerchLife l in Object.FindObjectsByType<PerchLife>(FindObjectsSortMode.None))
            {
                float x = Vector3.Distance(l.transform.position, me.transform.position);
                if (x < d) { d = x; best = l; }
            }
            if (best == null) return Lang.T("Нема ворона на жердині ближче 30 м.");
            best.StartPreview(left);
            return Lang.F("Розворот: {0}, {1} м.", (best.Munin ? Lang.T("Мунін") : Lang.T("Хугін")), Mathf.RoundToInt(d));
        }

        internal void Crouch() { _crouchWant = 1f; }

        // down on the bar: it settles in, as on a shoulder (the sitting system plays it once it may)
        private void Settle()
        {
            if (!Composes || Random.value >= ShoulderLife.SettleChance) return;
            _sitDir.Want(SitTemplates.SettleName, 0.3f, 6f);
        }

        // free to turn round now: awake, no enemy near, not turning already, not crouched to leave
        internal bool CanTurn { get { return !_asleep && _foe == null && _turnAt < 0f && _crouchWant <= 0f; } }

        private void TurnStep(float dt)
        {
            if (Pose == null) return;
            float crouch = Mathf.MoveTowards(Pose.Crouch, _crouchWant, dt / 0.25f);
            _turnWings = 0f; _turnHead = 0f;
            if (_turnAt < 0f) { Pose.Crouch = Mathf.Max(Mathf.Max(crouch, Mathf.Clamp01(SitRow(11))), _sleepW * SleepCrouch); Pose.LeanAdd = 0f; return; } // (a sitting template's crouch; asleep: deeper)
            bool held = _preview && Stage && PerchTurn.Hold >= 0f; // the hold is the picture's; a real perch only plays it through
            float u = held ? PerchTurn.Hold : Mathf.Clamp01((Time.time - _turnAt) * (_preview ? PerchTurn.Speed : 1f) / Mathf.Max(0.05f, PerchTurn.Seconds));
            // the body goes round in steps (its own time: on mostly while a foot is up), the rest at the turn's pace
            int foot; float up;
            float uy = PerchTurn.Steps(u, out foot, out up);
            float yaw = PerchTurn.Sample(uy).Yaw; // (Sample hands back one kept object: read before the next)
            PerchTurn.Key k = PerchTurn.Sample(u);
            Pose.Yaw = _turnSign * yaw;
            Pose.Lift = k.Lift / 100f;
            Pose.Crouch = Mathf.Max(crouch, k.Crouch / 100f + PerchTurn.StepDip * up);
            Pose.LeanAdd = k.Lean;
            // the foot on the side it turns to goes first
            bool leftUp = foot * _turnSign < 0f;
            Pose.StepL = foot != 0 && leftUp ? up * PerchTurn.StepLift : 0f;
            Pose.StepR = foot != 0 && !leftUp ? up * PerchTurn.StepLift : 0f;
            Pose.Roll = PerchTurn.StepRoll * up;
            _turnWings = k.Wings + PerchTurn.StepWings * up;
            _turnHead = _turnSign * -k.Head; // the head leads: towards where the body still turns
            if (u >= 1f && !held) { _turnAt = -1f; _preview = false; Pose.Yaw = 0f; Pose.Lift = 0f; Pose.LeanAdd = 0f; Pose.StepL = 0f; Pose.StepR = 0f; Pose.Roll = 0f; }
        }

        // Once, a moment after it landed: its owner's game looks where its owner is. Behind the raven (it sat facing
        // the way it hopped, away from you) and nothing solid on that side: it turns round to face you.
        private void FaceCheck()
        {
            if (_faceCheckAt < 0f || Time.time < _faceCheckAt) return;
            _faceCheckAt = -1f; // later looks: the perch's holder, for anyone near (PerchView.FaceNearest)
            Player me = Player.m_localPlayer;
            if (me == null || me.GetPlayerID() != Owner || View == null || _foe != null || _asleep || _turnAt >= 0f || _crouchWant > 0f) return;
            if (Vector3.Distance(me.transform.position, transform.position) > FaceNear) return; // you are not by the perch
            Transform face = transform.parent;
            if (face == null) return;
            Vector3 toMe = me.transform.position - face.position, fwd = face.forward;
            toMe.y = 0f; fwd.y = 0f;
            if (toMe.sqrMagnitude < 0.01f || Vector3.Dot(toMe.normalized, fwd.normalized) > -0.2f) return; // you are not behind it
            Perches.Occupant o = View.At(Seat);
            if (o == null || Perches.WallAhead(View, Seat, !o.Flip)) return; // facing you would put its beak in a wall
            Perches.RequestTurn(View, Seat);
        }

        internal void JustLanded()
        {
            _landSayAt = Time.time + Random.Range(2f, 3.5f); // a word about the place, once it has settled
            _ownerWasNear = true; // whoever sat it down is here: no greeting over the word about the place
            _faceCheckAt = Time.time + FaceCheckAfter;
            if (Pose != null) Pose.Sit = 0f;
            _sitWant = 0f;
            _standUntil = Time.time + Random.Range(StandAfterLandMin, StandAfterLandMax);
            Settle();
        }

        private void SitStep(float dt)
        {
            // asleep: the game's idle (its look-arounds) slows to a stop; only the breath stays
            if (Pose != null) Pose.Rate = Mathf.MoveTowards(Pose.Rate, _asleep ? 0f : 1f, dt * 0.7f);
            float sw = _sleepW;
            _sleepW = Mathf.MoveTowards(_sleepW, _asleep ? 1f : 0f, dt / (_asleep ? 3f : 0.8f));
            if (sw <= 0f && _sleepW > 0f) { _sleepSide = ((Owner + Seat + (EnvMan.instance != null ? EnvMan.instance.GetDay() : 0)) & 1) == 0 ? -1f : 1f; _peekAt = Time.time + Random.Range(PeekMin, PeekMax); }
            if (_asleep && _peekAt > 0f && Time.time >= _peekAt) { _peekUntil = Time.time + Random.Range(1.2f, 2.5f); _peekAt = Time.time + Random.Range(PeekMin, PeekMax); }
            float want = _sitWant;
            if (_standUntil >= 0f && Time.time >= _standUntil) { _standUntil = -1f; _sitWant = want = 1f; }
            if (_foe != null) want = 0f;
            if (_asleep) want = 1f;
            if (Pose != null) Pose.Sit = Mathf.MoveTowards(Pose.Sit, want, dt / Mathf.Max(0.05f, SitSeconds));
        }

        private void Find()
        {
            _found = true;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (_neck == null && (n == "Neck" || n.ToLowerInvariant() == "head")) _neck = Make(t);
                else if (_jaw == null && n == "Jaw") _jaw = Make(t);
                else if (_spine == null && n == "Spine1") _spine = Make(t);
                else if (_wingL == null && n.ToLowerInvariant() == "wing1.l") _wingL = Make(t);
                else if (_wingR == null && n.ToLowerInvariant() == "wing1.r") _wingR = Make(t);
                else if (_handL == null && n.ToLowerInvariant() == "wing3.l") _handL = Make(t);
                else if (_handR == null && n.ToLowerInvariant() == "wing3.r") _handR = Make(t);
            }
        }

        private static Bone Make(Transform t) { return new Bone { T = t, Rest = t.localRotation }; }

        // before the clip is sampled: back to the start, so a bone the clip does not move is not turned twice
        internal void ResetBones()
        {
            if (!_found) Find();
            if (_all == null) _all = new[] { _neck, _jaw, _spine, _wingL, _wingR, _handL, _handR };
            foreach (Bone b in _all) if (b != null) b.T.localRotation = b.Rest;
        }

        // ---------------------------------------------------------------- deciding (a few times a second)

        // the test window's picture (PerchTurnStage): always "near", no looking about the world, looking ahead
        internal bool Stage;
        internal bool Turning { get { return _turnAt >= 0f; } }

        // ---- speaking (PerchView.Say / a talk): the beak goes while the words show; the other one looks at the speaker
        private float _speakUntil = -1f, _landSayAt = -1f;
        internal bool Calm { get { return !_asleep && _foe == null && _turnAt < 0f && _crouchWant <= 0f; } }

        internal void Speak(float seconds)
        {
            _speakUntil = Time.time + Mathf.Clamp(seconds, 1f, 8f);
            _nextScan = 0f;
        }

        internal void Listen(Transform speaker, float seconds)
        {
            if (speaker == null || _asleep) return;
            _look = speaker;
            _lookUntil = Time.time + Mathf.Clamp(seconds, 1f, 8f);
        }

        private void LandSay()
        {
            if (_landSayAt < 0f || Time.time < _landSayAt || View == null) return;
            _landSayAt = -1f;
            bool roof; int comfort;
            View.Env(out roof, out comfort);
            float mul = Perches.Mul(roof, comfort);
            View.Say(Seat, mul < 1f ? "perch_cold" : comfort >= 6 ? "perch_cozy" : "perch_land", null, 0.75f);
        }

        private void Update()
        {
            if (Stage) { _active = true; _glance = new Vector3(0f, 0.2f, 3f); return; }
            LandSay();
            // a turn is over in its time even when nobody here sees the bird (the pose runs only when seen)
            if (_turnAt >= 0f && !_preview && Time.time - _turnAt > PerchTurn.Seconds + 0.5f && Pose != null) { _turnAt = -1f; Pose.Yaw = 0f; Pose.Lift = 0f; Pose.LeanAdd = 0f; Pose.StepL = 0f; Pose.StepR = 0f; Pose.Roll = 0f; }
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.5f;
            // anyone near (not only you): the perch's holder speaks for everyone there
            _active = false;
            foreach (Player pl in Player.GetAllPlayers()) if (pl != null && (pl.transform.position - transform.position).sqrMagnitude < Near * Near) { _active = true; break; }
            if (!_active) { _act = Act.None; return; }
            Scan();
            if (_act != Act.None && Time.time - _actAt >= _actLen) _act = Act.None;
            if (Composes) { _sitDir.Tick(this); StandUp(); } // the sitting system decides what it does
        }

        // A perch facing a wall: whoever is in front of the raven is behind that wall, unseen (it would stare into it).
        // One ray per seat, kept until the raven turns round (another facing) or 10 s pass (a wall built later);
        // every look after that is a dot product.
        private Transform _wallFace;
        private bool _wallFront;
        private float _wallCheckAt;
        private bool Unseen(Vector3 pos)
        {
            Transform face = transform.parent;
            if (face == null || View == null) return false;
            if (face != _wallFace || Time.time >= _wallCheckAt)
            {
                _wallFace = face; _wallCheckAt = Time.time + 10f;
                Perches.Occupant o = View.At(Seat);
                _wallFront = o != null && Perches.WallAhead(View, Seat, o.Flip);
            }
            if (!_wallFront) return false;
            Vector3 to = pos - face.position, fwd = face.forward;
            return to.x * fwd.x + to.z * fwd.z > 0f; // in front: beyond the wall
        }

        private void Scan()
        {
            Vector3 at = transform.position;
            // an enemy near the perch
            Character foeWas = _foe;
            _foe = null;
            float best = FoeNear;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null) continue;
                float d2 = (c.transform.position - at).sqrMagnitude;
                if (d2 >= best * best || c.IsPlayer() || !(c.GetBaseAI() is MonsterAI) || c.IsDead() || c.IsTamed() || Unseen(c.transform.position)) continue; // near first, then the dearer asks
                best = Mathf.Sqrt(d2); _foe = c;
            }
            // people: its owner and anybody else close
            Player owner = PerchView.PlayerOf(Owner);
            bool ownerNear = owner != null && Vector3.Distance(owner.transform.position, at) < OwnerNear && !Unseen(owner.transform.position);
            if (ownerNear && !_ownerWasNear && _foe == null) { Greet(owner); _wakeUntil = Time.time + Random.Range(WakeMin, WakeMax); }
            if (_foe != null && foeWas == null && View != null) View.Say(Seat, "perch_foe", null, 0.6f); // an enemy turned up
            _ownerWasNear = ownerNear;
            _stranger = null;
            float sb = StrangerNear;
            Player carrier = View != null ? View.Carrier : null; // (on someone's head: not a stranger to it)
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p == owner || p == carrier || p.GetPlayerID() == Owner) continue;
                float d = Vector3.Distance(p.transform.position, at);
                if (d < sb && !Unseen(p.transform.position)) { sb = d; _stranger = p; }
            }
            // a stranger coming close wakes it too; then, all calm, it dozes off again (you standing by included)
            if (_stranger != null && _stranger != _strangerWas)
            {
                _wakeUntil = Mathf.Max(_wakeUntil, Time.time + Random.Range(WakeMin, WakeMax));
                if (View != null) View.Say(Seat, "perch_stranger", new Dictionary<string, string> { { "stranger", _stranger.GetPlayerName() } }, 0.5f);
            }
            _strangerWas = _stranger;
            bool night = EnvMan.instance != null && EnvMan.IsNight();
            if (_foe != null) _wakeUntil = Time.time + WakeMin;
            bool was = _asleep;
            _asleep = night && _foe == null && Time.time >= _wakeUntil;
            if (was && !_asleep) _wakeUntil = Mathf.Max(_wakeUntil, Time.time + Random.Range(WakeMin, WakeMax));
            _puff = Mathf.MoveTowards(_puff, Munin && _stranger != null ? 1f : 0f, 0.5f);

            // what it looks at (asleep: nothing; the head stays tucked)
            if (_asleep) { _look = null; _lookUntil = 0f; }
            else if (_foe != null) _look = _foe.transform;
            else if (Time.time < _lookUntil && _look != null) { }
            else if (_stranger != null && Random.value < 0.6f) { _look = _stranger.transform; _lookUntil = Time.time + Random.Range(2f, 5f); }
            else if (ownerNear && Random.value < 0.35f) { _look = owner.transform; _lookUntil = Time.time + Random.Range(2f, 4f); }
            else
            {
                Transform nb = View != null ? View.Neighbour(Seat) : null;
                if (nb != null && Random.value < 0.15f) { _look = nb; _lookUntil = Time.time + Random.Range(1.5f, 3f); }
                else if (Time.time >= _lookUntil) _look = null;
            }
            if (_look == null && !_asleep && Time.time >= _nextGlance)
            {
                _nextGlance = Time.time + Random.Range(2f, 6f) * (Munin ? 0.6f : 1.2f);
                _glance = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.25f), Random.Range(0.2f, 1f)) * 3f;
            }
            if (_foe != null && Munin && Time.time >= _nextFoeCaw) { _nextFoeCaw = Time.time + Random.Range(4f, 8f); Caw(0.8f, 0.55f); }
        }

        private void Greet(Player owner)
        {
            _look = owner.transform;
            _lookUntil = Time.time + Random.Range(2.5f, 4f);
            Caw(Munin ? 0.85f : 1.15f, 0.5f);
            if (View != null) View.Say(Seat, _asleep ? "perch_wake" : "perch_greet", null, 0.6f); // woken by you, or glad (or not) to see you
        }

        private void Caw(float pitch, float volume)
        {
            _jawUntil = Time.time + _jawLen;
            Vector3 head = _neck != null ? _neck.T.position : transform.position + Vector3.up * 0.2f;
            ScreamFx.CawAt(transform, head, pitch, volume, true, VoiceCode);
        }

        private bool May(int kind, float sameRest)
        {
            float k = (Munin ? 0.6f : 1.3f) / Nerves;
            return Time.time - _last[kind] >= sameRest * k;
        }

        // ---------------------------------------------------------------- the moves (each frame, after the legs)

        private void Walk(Vector3 along)
        {
            float u = Mathf.Clamp01((Time.time - _actAt) / Mathf.Max(0.05f, _actLen));
            float x = u * _walkSteps;
            int i = Mathf.Min(_walkSteps - 1, (int)x);
            float s = Mathf.Clamp01(x - i), up = Mathf.Sin(s * Mathf.PI), e = s * s * (3f - 2f * s);
            _stepX = Mathf.Lerp(_stepFrom, _stepWant, (i + Mathf.Lerp(s, e, 0.75f)) / _walkSteps);
            if (u >= 1f) _stepX = _stepWant;
            if (Pose == null) return;
            // which foot is on the side it goes to: that one first, then the other, by turns
            Vector3 dir = along * Mathf.Sign(_stepWant - _stepFrom);
            bool leadLeft = Pose.LeftFootToward(dir);
            bool left = i % 2 == 0 ? leadLeft : !leadLeft;
            float lift = u >= 1f ? 0f : up * WalkLift;
            Pose.StepL = left ? lift : 0f;
            Pose.StepR = left ? 0f : lift;
            Pose.Roll = WalkRoll * (u >= 1f ? 0f : up);
        }

        private float Env(float len)
        {
            float u = Mathf.Clamp01((Time.time - _actAt) / Mathf.Max(0.05f, len));
            return Mathf.Sin(u * Mathf.PI);
        }

        internal void Apply(Vector3 side)
        {
            if (!_found) Find();
            Transform seat = transform.parent;
            Vector3 up = Vector3.up;
            Vector3 fwd = Vector3.Cross(side, up).normalized; // as it faces now (turning round included)
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            float dt = Time.deltaTime;
            SitStep(dt);
            TurnStep(dt);
            FaceCheck();

            // the step along the bar (kept between moves): side-steps, a foot up at each
            Vector3 along = seat != null ? seat.right : side;
            if (_act == Act.Step) Walk(along);
            else if (_turnAt < 0f && Pose != null && (Pose.StepL > 0f || Pose.StepR > 0f || Pose.Roll > 0f)) { Pose.StepL = 0f; Pose.StepR = 0f; Pose.Roll = 0f; }
            Vector3 move = along * _stepX;
            float reach = Composes && _sitDir.Run != null ? _sitDir.Run.T.Reach * SitW : 0f;
            if (reach > 0.0001f)
            {
                Transform nb = View != null ? View.Neighbour(Seat) : null;
                Vector3 to = nb != null ? Vector3.ProjectOnPlane(nb.position - transform.position, up) : Vector3.zero;
                if (to.sqrMagnitude > 0.0001f) move += to.normalized * reach;
            }
            transform.position += move;
            if (!_active) return;

            // the back: breath, Munin hunched / Hugin upright, asleep or alert hunched more; the shake rolls it
            if (_spine != null)
            {
                float breathHz = _asleep ? 0.18f : 0.32f, breathDeg = _asleep ? 2f : 1.2f;
                float hunch = (Munin ? 8f : -4f) + (_asleep ? SleepHunch : 0f) + (_foe != null ? 6f : 0f) + _puff * 4f;
                hunch += Mathf.Sin(Time.time * breathHz * Mathf.PI * 2f) * breathDeg;
                _spine.T.rotation = Quaternion.AngleAxis(hunch, side) * _spine.T.rotation;
                if (_act == Act.Shake) _spine.T.rotation = Quaternion.AngleAxis(Mathf.Sin((Time.time - _actAt) * 5f * Mathf.PI * 2f) * 7f * Env(_actLen), fwd) * _spine.T.rotation;
                float fluff = Mathf.Max(_puff, _sleepW * SleepPuff); // (asleep: fluffed up against the cold)
                if (fluff > 0.01f) _spine.T.localScale = Vector3.one * (1f + fluff * 0.06f);
                else if (_spine.T.localScale != Vector3.one) _spine.T.localScale = Vector3.one;
            }

            // the wings: half open when alert, and as the turn's keys say (a sitting template's go on after)
            float alert = (_foe != null ? AlertWing : 0f) + _turnWings;
            Wing(_wingL, _handL, alert, up, fwd, 1f);
            Wing(_wingR, _handR, alert, up, fwd, -1f);

            // the head: at what it watches, or its own glances; asleep tucked down; eased (Hugin slow, Munin quick)
            float wantYaw = 0f, wantPitch = 0f;
            Vector3 target;
            bool has = false;
            if (_look != null)
            {
                if (_look != _lookWas) { _lookWas = _look; _lookUp = _look.GetComponent<Character>() != null ? 1.4f : 0.1f; } // a creature: at its head
                target = _look.position + Vector3.up * _lookUp; has = true;
            }
            else target = seat != null ? seat.TransformPoint(_glance) : transform.position + fwd;
            SitRun sr = Composes ? _sitDir.Run : null;
            if (sr != null && SitW > 0.3f && sr.T.Look > 0 && _neck != null)
            {
                Transform nb = View != null ? View.Neighbour(Seat) : null;
                Player owner = ((ISitBird)this).BirdOwner;
                if (sr.T.Look >= 3 && nb != null) { Vector3 o = nb.position + up * 0.2f; target = sr.T.Look == 4 ? _neck.T.position * 2f - o : o; has = true; }
                else if (sr.T.Look == 1 && owner != null && Vector3.Distance(owner.transform.position, transform.position) < Near) { target = owner.GetHeadPoint(); has = true; }
                else if (sr.T.Look == 2 && owner != null && Vector3.Distance(owner.transform.position, transform.position) < Near) { target = owner.transform.position + owner.transform.forward * 0.55f + up * 0.95f; has = true; }
            }
            if (_asleep && !has)
            {
                bool peek = Time.time < _peekUntil; // half awake a moment: the head up, then down again
                wantYaw = peek ? 0f : _sleepSide * SleepYaw * _sleepW;
                wantPitch = peek ? 8f : Mathf.Lerp(0f, SleepHead, _sleepW);
            }
            else if (_neck != null)
            {
                Vector3 dir = target - _neck.T.position;
                Vector3 flat = Vector3.ProjectOnPlane(dir, up);
                if (flat.sqrMagnitude > 0.0001f)
                {
                    wantYaw = Mathf.Clamp(Vector3.SignedAngle(fwd, flat, up), -110f, 110f);
                    wantPitch = Mathf.Clamp(-Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg, -40f, 50f);
                }
            }
            // awake: the head jumps from look to look and holds (Saccade); asleep: it eases
            if (!_asleep)
            {
                Saccade.ToAngles(_sac.Step(Saccade.FromAngles(wantYaw, wantPitch), dt, Munin, Nerves), out _yaw, out _pitch);
                _yawMid = _yaw; _pitchMid = _pitch;
            }
            else
            {
                float tau = (Munin ? HeadEaseMunin : HeadEaseHugin) / Nerves;
                float a = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tau));
                _yawMid = Mathf.Lerp(_yawMid, wantYaw, a); _pitchMid = Mathf.Lerp(_pitchMid, wantPitch, a);
                _yaw = Mathf.Lerp(_yaw, _yawMid, a); _pitch = Mathf.Lerp(_pitch, _pitchMid, a);
                _sac.Reset(Saccade.FromAngles(_yaw, _pitch));
            }
            float peck = PeckNow();
            if (_neck != null)
            {
                float yaw = _yaw * (1f - peck) + _turnHead;
                Vector3 headSide = Quaternion.AngleAxis(yaw, up) * side;
                _neck.T.rotation = Quaternion.AngleAxis(Mathf.Lerp(_pitch, PeckDip, peck), headSide) * Quaternion.AngleAxis(yaw, up) * _neck.T.rotation;
            }
            if (_jaw != null && peck > 0.6f) _jaw.T.rotation = Quaternion.AngleAxis((peck - 0.6f) / 0.4f * PeckOpen, side) * _jaw.T.rotation; // the beak open at the bottom of a dip

            // the beak: open as it caws, clacking at the other raven
            if (_jaw != null && Time.time < _speakUntil && Time.time >= _jawUntil && Random.value < dt * 7f) _jawUntil = Time.time + _jawLen * Random.Range(0.5f, 1f); // words: a clack now and then
            if (_jaw != null && Time.time < _jawUntil)
            {
                float left = (_jawUntil - Time.time) / _jawLen;
                _jaw.T.rotation = Quaternion.AngleAxis(Mathf.Sin(Mathf.Clamp01(1f - left) * Mathf.PI) * ShoulderLife.ClackOpen, side) * _jaw.T.rotation;
            }
            else if (_jaw != null && sr != null)
            {
                float beak = Mathf.Max(0f, SitRow(10)), hz = sr.T.BeakHz;
                if (beak > 0.01f) _jaw.T.rotation = Quaternion.AngleAxis(beak * (hz > 0f ? 0.55f + 0.45f * Mathf.Sin(Time.time * hz * Mathf.PI * 2f) : 1f), side) * _jaw.T.rotation;
            }
            // a sitting template's pose over it all (wings, body, neck, head, tail; the inner wing: the side its seed picked)
            if (sr != null && SitW > 0.001f) _poser.Apply(transform, sr, _sitDir.ActT, SitW, Quaternion.LookRotation(fwd, up), sr.Sign > 0f, !_asleep);
        }

        // a wing out from the body (about the up axis, the tip away from the back) and a little up
        // (and the hand bent back at the wrist: a wing half open, not a plank)
        private static void Wing(Bone w, Bone hand, float deg, Vector3 up, Vector3 fwd, float sign)
        {
            if (w == null || deg < 0.1f) return;
            w.T.rotation = Quaternion.AngleAxis(deg * 0.35f * sign, fwd) * Quaternion.AngleAxis(deg * sign, up) * w.T.rotation;
            if (hand != null) hand.T.rotation = Quaternion.AngleAxis(-deg * ShoulderLife.WristBend * 1.2f * sign, up) * hand.T.rotation;
        }

        private void OnDestroy() { if (_spine != null && _spine.T != null) _spine.T.localScale = Vector3.one; }
    }
}
#endif

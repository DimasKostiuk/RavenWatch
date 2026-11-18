#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A falcon of Freya's sisters (story-lore.md «Глава 1»): a creature of its own (the user 2026-11-12: «не називай сокола вороном» —
    // big, standing three quarters of a person tall; wide, slow wingbeats as a real great raptor's; none of the ravens' ways).
    // Its body is its own skinned model (Model: assets/story/fbx/Falcon.fbx, rigged in Blender 2026-11-13 — the Meshy falcon made
    // in flight, laid straight and made even, its left half mirrored; weights by part; WsAssets reads it skinned). Its rest pose
    // is a glide: body level along +Z, back up, wings spread. Its bones (Root, Chest, Neck, Head, Tail; per side Arm, Forearm,
    // Hand, Tip, Thigh, Foot; their «_end» leaves) lie unturned at their joints, so each turn below is in its parent's frame,
    // which at rest is the model's (Ax: its way ahead, up, its right):
    //   its posture   laid along its flight, or upright on its feet (StandTilt: its body's line raised)
    //   its wings     a shape (raise: up/down at the shoulder; sweep: back along the body; elbow; hand: the outer wing bent
    //                 back), eased between its ways — gliding (as made), stooping (swept back close), braking (high, a little
    //                 forward), standing (drooped) — and a beat over it: a slow deep stroke, the hand bent back on the way up
    // It does not sit as a bird sits (a model made in flight folds into a lump, seen 2026-11-13): in a scene it lands and turns
    // into a person (the user 2026-11-13: «на землі перетворитись на людину»).
    // Its flight is its own, no raven motor: it soars in wide circles on still wings with a few slow beats now and then, glides
    // down a glide path to a place and flares (its breast up, wings up and forward, legs out), stoops at one from high behind it with
    // its wings swept back and comes out into that landing, brakes with its wings high and its feet forward, stands, takes off with
    // deep beats, leaves. It looks ahead for trees, walls and roofs and keeps over the crowns and the ground (never through it).
    // A picture with no network object: a scene's steps (StoryScenesFalcon) tell every game near, each flies its own; the author's
    // console (raven story falcon …: Cmd) only this game's. Every number below is a static field: the studio tunes it live
    // (set Falcon.<field> <value>); Hold keeps one wing shape to look at; «raven story falcon values» prints them all to bake.
    internal sealed class Falcon : MonoBehaviour
    {
        // its body: its own skinned model (built into the mod: assets/story/fbx/Falcon.fbx)
        internal const string Model = "asset/Птахи/Falcon";
        internal static readonly List<Falcon> Live = new List<Falcon>();
        internal static string Problem = "";

        // ---- size and pace
        internal static float Height = 1.35f;            // m standing, feet to the top of the head (3/4 of a person: the user 2026-11-12)
        private const float SizeTilt = 50f;              // ° its line raised as Height is measured (the size the user approved: wings 2.29 m)
        internal static float Cruise = 11f, Soar = 9f, Approach = 7f, StoopMax = 30f; // m/s
        internal static float MaxBank = 35f;             // ° banked in a turn
        internal static float ClimbMax = 2.5f, SinkMax = 1.2f; // m/s up beating, down gliding
        internal static float GroundClear = 5f;          // m over the ground in flight (not landing)
        internal static float CircleRadius = 16f, CircleUp = 18f; // m: its ring round someone, and how high over the ground there
        internal static float BrakeAt = 5f, BrakeDecel = 9f;      // m from the place it brakes; m/s² it slows braking
        internal static float TakeOffSeconds = 2.2f, PostureSeconds = 1.3f;
        // the landing, as a great raptor lands (2026-11-13: it came down nose first, legs tucked, then jumped upright in 0.9 s — «покращення приземлення»):
        // the body near level down the glide path (LandPitch: the share of the path's angle the body shows), then from FlareAt m it rises
        // breast first, wings up and forward, legs out (the flare, eased), and brakes from the height the flare reached
        // (2026-11-14, measured in the studio at 6 frames a second: braking began 5–6 m up and it sank upright for four seconds, «as on a parachute» — the user: «треба попрацювати над анімацією
        //  приземлення». Now: the glide path 14° (LandSlope, was 28), braking from 5 m (BrakeAt, was 9), the flare from 9 m (FlareAt, was 18: it flew half upright for five seconds) —
        //  it comes in at about 3 m, brakes from 2.8 m and stands in two seconds)
        internal static float FlareAt = 9f, FlareUp = 0.45f, FlareWings = 0.6f, LandPitch = 0.35f;
        // ---- the wings (°)
        internal static float BeatHz = 1.5f;             // strokes a second: a great raptor's slow, deep beat
        internal static float BeatUp = 45f, BeatDown = 35f; // the stroke's top over level, its bottom under
        internal static float BeatHand = 30f;            // the hand bent back on the way up
        internal static float BeatSweep = 8f;            // the whole wing a little back on the way up
        internal static float BeatEase = 0.35f;          // s to go from gliding to beating and back
        internal static float BeatVary = 0.14f;          // each falcon's own stroke rate, this share either way (and its own moment in the stroke)
        internal static float BeatBob = 0.09f;           // m its body rides its stroke: up on the way down of the wings, sinking on their way up (a heavy bird's weight)
        internal static float GlideUp = 0f, GlideSweep = 0f; // (its rest pose is a glide: as made)
        internal static float SoarEvery = 7f;            // s about between a few beats while soaring
        internal static int SoarBeats = 3;
        internal static float StoopUp = 12f, StoopSweep = 70f, StoopHand = 55f;
        internal static float BrakeHigh = 55f, BrakeForward = 15f, BrakeHand = 0f, BrakeLegs = 55f, BrakeBeatHz = 1.8f; // sitting up: wings over its head and a little forward, stroking to and fro
        internal static float StandDroop = -15f, StandBack = 55f, StandElbow = -40f, StandHand = 55f; // standing: wings lowered (raise below zero), back along the body, the forearm a little forward, the hand back
        internal static float ShapeRate = 140f;          // °/s a wing's shape changes from one way to the next
        // ---- head, tail, legs
        internal static float StandTilt = 30f;           // ° its body's line raised standing and braking (50 stood it like a post: «тіло надто до верху», the user 2026-11-13)
        internal static float StandNeck = 5f;            // ° its head bent down standing and braking (45 put its beak into the ground: «голова вниз», the user 2026-11-13; the model looks down already)
        internal static float StandLegs = 0f;            // ° its legs swung forward under it standing (its body raised by StandTilt sets them under it already; 70 pointed them ahead, 2026-11-13)
        internal static float StandTail = 0f;            // ° its tail lifted standing (20 lifted it over the braking body, 2026-11-13)
        internal static float FlyPitch = 0f;             // ° its body's line raised (+) or lowered in flight
        internal static float FlyLegs = 70f;             // ° its legs drawn back under its tail in flight (the model's hang down: tuned live 2026-11-13)
        internal static float FlyHead = 20f;             // ° its head raised in flight (the model looks down at the ground: from below its beak was not seen, the user 2026-11-13)
        internal static float TailSteer = 0.5f;          // its tail twisted with its bank (× the bank)
        internal static float LandSlope = 14f;           // ° its glide path down to a landing (the nearer, the lower: over the place it is low already)
        internal static float AvoidLook = 1.4f;          // s of flight it looks ahead for a tree, a wall, a roof
        internal static float AvoidPush = 1f;            // how hard it turns up and away from one
        internal static float CrownClear = 6f;           // m over the crowns and roofs under it in flight (not landing)
        internal static float AimBlend = 0.8f;           // s a new aim takes over from the old one
        internal static float DiveSeconds = 7f;          // s a stoop may last: past it, it lands there instead
        // the stoop, as a falcon stoops (2026-11-13: it dived from its circle at a place it could not turn to at 30 m/s and skimmed off
        // through the bushes 130 m away): first up StoopHigh m over the place and StoopBack m behind it on its line in, then straight down
        // that line to PullBack m short of it, PullUp m over its feet, wings swept close; there it comes out, shedding speed (PullOut m/s²),
        // into the landing's flare and braking
        internal static float StoopHigh = 30f, StoopBack = 30f, PullBack = 14f, PullUp = 5f, PullOut = 14f;
        internal static float StoopSetup = 25f;          // s its run-in may take: past it, it stoops from where it is
        internal static float LookAround = 25f;          // ° it turns its head about, standing
        // ---- its voice (assets/story/sounds/story.txt: a peregrine, NPS, a tone lower — the user 2026-11-13: «ворожими, не ніжними»)
        internal static float CallEvery = 14f;           // s about between its calls while it circles
        internal const string CallSound = "falcon_cack", StoopSound = "falcon_scream";
        // ---- the author's look: hold one wing shape (0 off, 1 glide, 2 beat, 3 stoop, 4 brake, 5 stand)
        internal static int Hold;
        internal static float Slow = 1f;               // its own clock's pace: 0 stopped, 0.25 a quarter (the author's look)
        private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>(); // its numbers as built (reset)
        static Falcon() { foreach (FieldInfo fi in Tunables()) Defaults[fi.Name] = fi.GetValue(null); }

        // the low run (the user 2026-11-14: «вони не сильно зупиняючись летять до землі і тоді різко перетворюються в людей … летять так з камерою 20 м»):
        // down a straight line to SkimUp m over the ground, then level along it the last «run» m, never braking; over its place it is gone behind
        // its feathers at that speed (Stride) and the person walks on. No stride told by then: it lands there as ever
        internal static float SkimSpeed = 6.5f;          // m/s along its line (slower than its cruise: «краще сповільнити трохи», the user 2026-11-14)
        internal static float SkimUp = 1.1f;             // m its middle over the ground on the level run («майже над землею»)
        internal static float SkimFlare = 4f, SkimFlareShare = 0.5f; // m short of its place its wings begin to rise and its breast with them · how far into the landing's flare it gets
        internal static float SkimGlide = 0.45f;         // the share of the level run it glides at its end, wings still (before that: slow deep beats)
        internal static float WakeSeconds = 1.1f, WakeSpeed = 1.4f, WakeRate = 16f; // the feathers after a change at speed: s more of them come · m/s their source goes on (a person's walk) · a second
        internal static float DriftShare = 0.85f;        // the share of its speed its feathers fly on with (slowing to nothing over their life)

        private const int Circling = 0, Diving = 1, Landing = 2, Braking = 3, Standing = 4, TakingOff = 5, Leaving = 6, Showing = 7, Skimming = 8;
        private static readonly string[] StateNames = { "кружляє", "кидається", "сідає", "гальмує", "стоїть", "злітає", "відлітає", "показується", "летить низько" };

        private GameObject _vis;
        private SkinnedMeshRenderer _skin;
        // its bones; per side [0, 1] by the side they are on (not by their names: Blender's left is mirrored into Unity)
        private Transform _root, _neck, _tail;
        private readonly Transform[] _arm = new Transform[2], _fore = new Transform[2], _hnd = new Transform[2], _tip = new Transform[2], _thigh = new Transform[2], _foot = new Transform[2];
        private readonly float[] _side = { 1f, -1f }; // +1: the wing on its right
        private Vector3 _ahead, _right;       // its way ahead and its right in the model's frame at rest (up is the model's up)
        private Quaternion _flyRest;          // the model's frame laid onto its own: its way ahead along +Z
        private float _standUp;               // m its body's middle stands over its feet (it is held by its middle, flying or standing)
        internal bool Scene;                  // a scene's falcon (StoryScenesFalcon): the author's console leaves it alone
        internal long Owner;                  // another's scene's copy: whose (gone, he left: it flies off — Unowned)
        private float _ownerAt;

        private int _state = Circling, _after = Circling;
        private Transform _around; private Vector3 _center, _target, _awayDir;
        private float _hdg, _speed, _vy, _bank, _pitch, _upright, _since;
        private float _phase, _beatW, _ownBeat = 1f; private int _burst; private float _nextBurst;
        private float _flare;                // 0 … 1: how far into the flare before a landing (Landing: by its distance)
        private float _nextCall;             // when it calls next, circling
        private Vector3 _stoopIn;            // its line in to the place it stoops at (level, toward it)
        private bool _stooping;              // Diving: false — getting up and behind; true — in the stoop
        private bool _outbound;              // Diving, before the stoop: still flying out along its line (too near the place to turn in)
        private float _raise = 6f, _sweep, _elbow, _hand, _legs; // the wings' shape now (eased), the legs forward
        private float _clock, _now;          // (its own time: Slow)
        private Vector3 _avoid, _avoidWant, _lastAim, _aimFrom; private float _probeAt, _crown = float.MinValue, _crownAt, _aimSince = -10f; // (the look ahead, the crowns under it, the aim's easing)
        private float _height, _span;        // m: as tall standing, as wide wing to wing (now)
        private Vector3 _showAt; private float _showUp; // the author's look: held there, this upright

        // ---------------------------------------------------------------- making

        internal static Falcon Make(Vector3 at, float yaw)
        {
            GameObject tpl = WsAssets.Template(Model);
            SkinnedMeshRenderer tskin = tpl != null ? tpl.GetComponentInChildren<SkinnedMeshRenderer>(true) : null; // heavy: once per falcon made
            if (tskin == null) { Problem = tpl == null ? "no model «" + Model + "» (log: «asset»)" : "the model «" + Model + "» has no skin"; Story.Log("falcon: " + Problem); return null; }
            GameObject root = new GameObject("RavenWatch_Falcon");
            root.transform.position = at;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject vis = Instantiate(tpl, root.transform, false);
            vis.name = "body"; vis.hideFlags = HideFlags.None;
            vis.SetActive(true);
            Falcon f = root.AddComponent<Falcon>();
            if (!f.Init(vis, vis.GetComponentInChildren<SkinnedMeshRenderer>(true), yaw)) { Story.Log("falcon: " + Problem); Destroy(root); return null; } // heavy: once per falcon made
            Live.Add(f);
            return f;
        }

        private bool Init(GameObject vis, SkinnedMeshRenderer skin, float yaw)
        {
            _vis = vis; _skin = skin;
            Dictionary<string, Transform> by = new Dictionary<string, Transform>();
            foreach (Transform b in skin.bones) if (b != null) by[b.name] = b;
            Transform headEnd, tailEnd;
            if (!by.TryGetValue("Root", out _root) || !by.TryGetValue("Neck", out _neck) || !by.TryGetValue("Tail", out _tail) || !by.TryGetValue("Head_end", out headEnd) || !by.TryGetValue("Tail_end", out tailEnd))
            { Problem = "the model's skeleton lacks Root, Neck, Tail, Head_end or Tail_end"; return false; }
            // its frame at rest, in the model's own (vis) frame: ahead = tail → beak, level; right = up × ahead
            Vector3 headAt = vis.transform.InverseTransformPoint(headEnd.position), tailAt = vis.transform.InverseTransformPoint(tailEnd.position);
            _ahead = Vector3.ProjectOnPlane(headAt - tailAt, Vector3.up).normalized;
            if (_ahead.sqrMagnitude < 0.5f) { Problem = "the model's beak and tail are one over the other"; return false; }
            _right = Vector3.Cross(Vector3.up, _ahead);
            _flyRest = Quaternion.Inverse(Quaternion.LookRotation(_ahead, Vector3.up));
            // each side by where its arm is: [0] its right, [1] its left
            Vector3 mid = vis.transform.InverseTransformPoint(_root.position);
            foreach (string n in new[] { "L", "R" })
            {
                Transform arm, fore, hand, tip, thigh, foot;
                if (!by.TryGetValue("Arm" + n, out arm) || !by.TryGetValue("Forearm" + n, out fore) || !by.TryGetValue("Hand" + n, out hand) || !by.TryGetValue("Tip" + n, out tip))
                { Problem = "the model's skeleton lacks a wing (Arm, Forearm, Hand, Tip + " + n + ")"; return false; }
                by.TryGetValue("Thigh" + n, out thigh); by.TryGetValue("Foot" + n, out foot);
                int s = Vector3.Dot(vis.transform.InverseTransformPoint(arm.position) - mid, _right) >= 0f ? 0 : 1;
                _arm[s] = arm; _fore[s] = fore; _hnd[s] = hand; _tip[s] = tip; _thigh[s] = thigh; _foot[s] = foot;
            }
            if (_arm[0] == null || _arm[1] == null) { Problem = "both wings of the model are on one side"; return false; }
            // its size: standing (its line raised by StandTilt), its feet to its head = Height
            Quaternion up = Quaternion.AngleAxis(-SizeTilt, _right); // (a fixed tilt, not StandTilt: lowering that to 30° grew it by a quarter unseen, 2026-11-13)
            float top = float.MinValue, low = float.MaxValue;
            foreach (Transform b in skin.bones)
            {
                if (b == null) continue;
                float y = (up * (vis.transform.InverseTransformPoint(b.position) - mid)).y;
                top = Mathf.Max(top, y); low = Mathf.Min(low, y);
            }
            float tall = (top - low) * vis.transform.lossyScale.x;
            if (tall > 1e-4f) vis.transform.localScale = vis.transform.localScale * (Height / tall);
            _height = Height;
            Transform tipA, tipB;
            _span = by.TryGetValue("TipL_end", out tipA) && by.TryGetValue("TipR_end", out tipB) ? (tipA.position - tipB.position).magnitude : 2f * Height;
            _standUp = Height * 0.4f; // (measured on its feet from the first frame it stands: LateUpdate)
            _hdg = yaw * Mathf.Deg2Rad;
            _clock = UnityEngine.Random.Range(0f, 100f);
            _phase = UnityEngine.Random.value; _ownBeat = UnityEngine.Random.Range(1f - BeatVary, 1f + BeatVary); // (its own stroke: three side by side never beat as one — the user 2026-11-14: «птахи летять синхронно»)
            _nextBurst = _now + SoarEvery * UnityEngine.Random.Range(0.3f, 1f);
            _baseScale = vis.transform.localScale;
            Story.Log("falcon: made, " + skin.bones.Length + " bones, tall " + Height.ToString("0.00", CultureInfo.InvariantCulture) + " m, wings " + _span.ToString("0.00", CultureInfo.InvariantCulture) + " m");
            return true;
        }

        private void OnDestroy() { Live.Remove(this); }

        // ---------------------------------------------------------------- orders

        internal void Circle(Transform around, Vector3 center)
        {
            _around = around; _center = center; _nextCall = 0f; // (its call: a while after it begins circling, every time)
            Go(Grounded ? TakingOff : Circling, Circling);
        }

        internal void Dive(Vector3 at)
        {
            _target = at; _stooping = false;
            Vector3 d = at - transform.position; d.y = 0f;
            _stoopIn = d.sqrMagnitude > 1f ? d.normalized : Facing; // (the way it comes from: no turn back at the place)
            _stoopIn = ClearLine(_stoopIn);
            _outbound = d.magnitude < StoopBack + 20f; // (circling near it: out first, then in — 2026-11-13 it circled its gate for a minute, the gate behind it)
            Go(Grounded ? TakingOff : Diving, Diving);
        }
        internal void Land(Vector3 at) { _target = at; Go(Grounded ? TakingOff : Landing, Landing); }

        // the low run to a place: from where it is, straight; the last «run» m level over the ground (made in the air for it: never from its feet)
        private Vector3 _skimFrom; private float _skimRun, _skimLeft = float.MaxValue;
        internal void Skim(Vector3 at, float run)
        {
            _target = at; _skimFrom = transform.position; _skimRun = Mathf.Max(1f, run); _skimLeft = float.MaxValue;
            Vector3 d = at - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) _hdg = Mathf.Atan2(d.x, d.z);
            _speed = SkimSpeed; _bank = 0f;
            Go(Skimming, Skimming);
        }
        internal void Leave(Vector3 away)
        {
            away.y = 0f;
            _awayDir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            if (Grounded) _hdg = Mathf.Atan2(_awayDir.x, _awayDir.z); // (it takes off that way: standing, it faced you — it climbed straight at you, the review 2026-11-13)
            Go(Grounded ? TakingOff : Leaving, Leaving);
        }

        private bool Grounded { get { return _state == Standing || _state == Braking || _state == Showing; } }

        private void Go(int now, int after)
        {
            if (now != _state) Story.Log("falcon: " + StateNames[_state] + " → " + StateNames[now] + (after != now ? " (then " + StateNames[after] + ")" : "") + " at " + Where(transform.position));
            _state = now; _after = after; _since = _now; _aimFrom = _lastAim; _aimSince = _lastAim != Vector3.zero ? _now : -10f; // (its very first aim: nothing to ease from)
            if (now == TakingOff) _speed = Mathf.Max(_speed, 2f);
        }

        // held where you look at it: in the air (upright 0, a wing shape from Hold) or standing (upright 1), turned as told
        internal void ShowAt(Vector3 at, float yaw, float upright)
        {
            _showAt = at; _showUp = upright; _hdg = yaw * Mathf.Deg2Rad;
            transform.position = at; transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _speed = 0f; _vy = 0f; _pitch = 0f; _bank = 0f; _burst = 0;
            Go(Showing, Showing);
        }

        internal void Turn(float deg) { _hdg += deg * Mathf.Deg2Rad; }

        // made beforehand for a scene, not to be seen yet (the user 2026-11-14: the picture froze as three were made in the frame their flight began):
        // held still high up with its picture off, till Unhide sets it where its flight begins
        internal bool Hidden { get; private set; }
        internal void Hide() { Hidden = true; if (_vis != null) _vis.SetActive(false); ShowAt(transform.position, _hdg * Mathf.Rad2Deg, 0f); }
        internal void Unhide(Vector3 at, float yaw)
        {
            Hidden = false;
            transform.position = at; transform.rotation = Quaternion.Euler(0f, yaw, 0f); _hdg = yaw * Mathf.Deg2Rad;
            if (_vis != null) _vis.SetActive(true);
            _state = Circling; _after = Circling; // (in the air: the order that follows says where)
        }

        // made in the air far off: at its cruising speed already (a scene's falcon flies in)
        internal void Launch() { _speed = Cruise; }

        // as tall as this now (m standing): the picture scaled, its standing height under its shoulders too
        internal void Resize(float h)
        {
            if (h <= 0.05f || _height <= 0f) return;
            float k = h / _height;
            _baseScale *= k; // (its own size: not the picture's now, half-way through a change)
            _vis.transform.localScale = _baseScale;
            _standUp *= k; _span *= k; _height = h;
            if (_state == Standing) transform.position = _target + Vector3.up * _standUp;
        }

        internal Vector3 Middle { get { return transform.position; } }
        internal float Span { get { return _span; } }
        internal float Tall { get { return _height; } }
        internal Vector3 Facing { get { return new Vector3(Mathf.Sin(_hdg), 0f, Mathf.Cos(_hdg)); } }
        internal bool OnFeet { get { return _state == Standing; } }
        // where it is going and how, for one who comes late (StoryScenesFalcon.FalconSync): its place, and stand|land|dive|circle|leave
        internal Vector3 Target { get { return _target; } }
        internal string Way { get { int s = _state == TakingOff ? _after : _state; return s == Circling ? "circle" : s == Diving ? "dive" : s == Standing || s == Braking || s == Showing ? "stand" : s == Leaving ? "leave" : "land"; } }
        // the ground under it: where it stands, else straight under it
        internal Vector3 Feet { get { return _state == Standing || _state == Braking ? _target : Footing(transform.position); } }

        // gone at once; with a burst (the sisters' change: a puff of the game's feathers, a warm haze and a light that fades) where it was
        // the change into a person: wings up and a beat for so long, then the burst at «at» and it shrinks away (gone by itself)
        private Vector3 _baseScale, _changeAt; private float _wrapFrom = -1f; private bool _unwrap, _stride;
        // what the game that directs the scene does the moment it is gone (the person made there): the other games' pictures have none
        internal Action Then; internal Action<Vector3, float> ThenStride;
        // «the cloak of wings»: on its feet it draws its wings round itself and rises to a person's height (WrapSeconds), then it is gone at once behind a
        // burst of its own feathers — and the person stands there whole (Then)
        internal void Wrap(Vector3 at) { _wrapFrom = _now; _unwrap = false; _changeAt = at; FeatherBurst(at, _height, 0.3f, false); }
        // made of a person: it stands as tall as the person was, wrapped, and opens its wings
        internal void Unwrap() { _wrapFrom = _now; _unwrap = true; }
        // «the step out of flight»: flaring down, it is gone behind its feathers the moment its feet are StrideUp over the ground (ThenStride: where, which way it flew)
        internal void Stride() { _stride = true; }
        // made from a person: out of nothing in the burst
        // (the burst of gold haze below is what is left of the first change: a scene stopped, «falcon … gone»)
        private bool Changing { get { return _wrapFrom >= 0f; } }

        internal void Vanish(bool burst)
        {
            if (burst) Burst(_state == Standing ? _target : transform.position - Vector3.up * _standUp, _height);
            Destroy(gameObject);
        }

        // the change between a falcon and a sister, on the ground at «at», as big as one «tall» m (a falcon's height, a person's)
        internal static float BurstLight = 3f, BurstSeconds = 1.6f;
        internal const float FalconPerson = 1.8f; // m: a person's height, for the feathers round one
        // the change, smoothed (2026-11-13: the falcon was just gone and the person took 2.5 s to come): on its feet it lifts its wings and
        // beats ChangeSeconds, then the burst and it shrinks away in ShrinkSeconds while the person comes in over ChangeIn s (StoryScenesFalcon);
        // made from a person, it grows out of the burst in GrowSeconds
        // The change made anew (the user 2026-11-14: «потрібно новий цікавий ефект», «не треба з прозорості сестер показувати — це має бути все наживо»):
        // nothing fades in or out. The leader's — the cloak of wings (Wrap): the falcon's own wings become her feather cape; the two others' — the step
        // out of flight (Stride): no standing at all. What hides the one frame of the swap is a burst of our own feathers in a falcon's colours
        // (FeatherBurst: assets/feathers.png, tinted), never a see-through body. NOT seen in the game yet: every number below is to tune live
        // («raven story falcon show wrap» holds the wrapped shape to look at).
        internal static float WrapSeconds = 1.1f, WrapTall = 1.3f, WrapTilt = 72f;                     // s · × its size at the end (a person's height) · ° its line raised at the end
        internal static float WrapRaise = -20f, WrapForward = 80f, WrapElbow = -110f, WrapHand = 95f;  // ° its wings drawn round it: at the shoulder, forward, the forearm in, the hand in (chosen 2026-11-14 from four shapes shot in the studio: the wings closed round the body, their tips behind — 35/75/−55/65 held them up over its head)
        internal static float StrideUp = 0.9f;                                                         // m its feet over the ground when the step out of flight comes
        internal static int FeatherCount = 54;                                                         // feathers of a whole burst
        internal static float FeatherLight = 1.6f, FeatherLightSeconds = 0.9f;
        internal static readonly Color[] FeatherColours = { new Color(0.72f, 0.6f, 0.42f), new Color(0.66f, 0.5f, 0.28f), new Color(0.42f, 0.3f, 0.2f), new Color(0.5f, 0.37f, 0.26f), new Color(0.34f, 0.24f, 0.17f) }; // sand, ochre, brown, coffee, dark brown (cream and pale read as white in the game's light)

        // a burst of feathers in a falcon's colours where it changes: round a body so tall, swirling out and sinking; «amount» of a whole burst; a soft warm light with a whole one
        // «drift»: it changed at speed (the low run) — the feathers fly on that way, slowing to nothing, and their source goes on at a walk for
        // WakeSeconds, shedding more: they trail after the one who walks out of them
        internal static void FeatherBurst(Vector3 at, float tall, float amount, bool light, Vector3 drift = default(Vector3))
        {
            GameObject go = new GameObject("RavenWatch_FalconFeathers");
            go.transform.position = at + Vector3.up * Mathf.Max(0.4f, tall * 0.55f);
            ParticleSystem ps = Feathers.Make(go.transform, "feathers", Vector3.zero);
            if (ps == null) { Destroy(go); return; }
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.24f); // (0.2…0.44 flew as big white sheets across the picture: the first run, 2026-11-14)
            main.gravityModifier = 0.22f;
            main.maxParticles = 300;
            Gradient g = new Gradient();
            GradientColorKey[] keys = new GradientColorKey[FeatherColours.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(FeatherColours[i], i / (float)(keys.Length - 1));
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            ParticleSystem.MinMaxGradient shade = new ParticleSystem.MinMaxGradient(g); shade.mode = ParticleSystemGradientMode.RandomColor;
            main.startColor = shade;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = Mathf.Max(0.25f, tall * 0.38f);
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true; vel.orbitalY = new ParticleSystem.MinMaxCurve(1.2f, 3.2f); vel.radial = new ParticleSystem.MinMaxCurve(0.2f, 0.9f); // (they swirl round where it stood)
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f); // (all three one kind of curve: «Particle Orbital Velocity curves must all be in the same mode» came 84 times in a run, 2026-11-14)
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime; // (a feather, not a body: it thins away at its end)
            Gradient thin = new Gradient();
            thin.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.enabled = true; fade.color = thin;
            if (drift.sqrMagnitude > 0.01f)
            {
                AnimationCurve slowing = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.3f), new Keyframe(1f, 0f));
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(drift.x * DriftShare, slowing); vel.y = new ParticleSystem.MinMaxCurve(Mathf.Abs(drift.y) > 0.01f ? drift.y * DriftShare : 0.4f, slowing); vel.z = new ParticleSystem.MinMaxCurve(drift.z * DriftShare, slowing); // (all three one kind of curve: Unity asks it)
                main.duration = Mathf.Max(0.1f, WakeSeconds);
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = WakeRate;
                go.AddComponent<Wake>().Begin(drift.normalized * WakeSpeed, WakeSeconds);
            }
            ps.Play();
            ps.Emit(Mathf.Max(4, Mathf.RoundToInt(FeatherCount * amount)));
            if (light)
            {
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(BurstColour.r, BurstColour.g, BurstColour.b); l.range = 6f; l.shadows = LightShadows.None;
                go.AddComponent<BeastLook.FadeLight>().Begin(l, FeatherLight, FeatherLightSeconds);
            }
            Destroy(go, 4f);
        }
        // the change at speed, all of it (the user 2026-11-14, after the first run: «ефекти треба доробити для перетворення в людей»): the falcon has
        // already risen and grown toward a person's height over its last RushRise m (LateUpdate) — here, the moment it is gone: its feathers thrown
        // on the way it flew and trailing after her, a second smaller burst that rises (what is left of the bird goes up, she goes on), Freya's warm
        // gold haze round her, a strong warm light that fades. Every game near makes its own
        internal static float RushRise = 2.6f, RushTall = 1.25f, RushUpright = 0.85f; // m short of its place it begins to rise · × its size at the end · how upright
        internal static float RushLight = 3f, RushLightSeconds = 1.1f, RushHaze = 7f; // the light's strength and s · puffs of gold haze (4.5 and 12 whited the whole picture out for a frame: the run on the glade, 2026-11-14)
        internal static float RushUpFeathers = 0.45f; // the rising burst's share of a whole one
        private static void Rush(Vector3 feet, Vector3 velocity)
        {
            FeatherBurst(feet, FalconPerson, 1.3f, false, velocity);
            FeatherBurst(feet + Vector3.up * 0.6f, FalconPerson * 0.6f, RushUpFeathers, false, Vector3.up * 3.2f + velocity * 0.25f);
            Vector3 mid = feet + Vector3.up * (FalconPerson * 0.6f);
            Material mat = Feathers.Smoke();
            if (mat != null && RushHaze >= 1f) BeastLook.Puffs(mat, mid, ParticleSystemShapeType.Sphere, FalconPerson * 0.35f, Mathf.RoundToInt(RushHaze), FalconPerson * 0.5f, FalconPerson * 1.0f, 1.6f, 1.4f, 1.2f, BurstColour);
            GameObject lg = new GameObject("RavenWatch_FalconRush");
            lg.transform.position = mid;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(BurstColour.r, BurstColour.g, BurstColour.b); l.range = 9f; l.shadows = LightShadows.None;
            lg.AddComponent<BeastLook.FadeLight>().Begin(l, RushLight, RushLightSeconds);
            lg.AddComponent<Wake>().Begin(velocity.normalized * WakeSpeed, RushLightSeconds); // (the light goes on with her)
            Destroy(lg, RushLightSeconds + 0.5f);
        }

        // the source of the feathers of a change at speed, going on at a walk for a moment (FeatherBurst «drift»)
        private sealed class Wake : MonoBehaviour
        {
            private Vector3 _v; private float _till;
            internal void Begin(Vector3 v, float seconds) { _v = v; _till = Time.time + seconds; }
            private void Update() { if (Time.time < _till) transform.position += _v * Time.deltaTime; else enabled = false; }
        }
        internal static readonly Color BurstColour = new Color(1f, 0.78f, 0.4f, 0.45f); // (Freya's: warm gold)
        internal static void Burst(Vector3 at, float tall)
        {
            Vector3 mid = at + Vector3.up * Mathf.Max(0.5f, tall * 0.6f);
            Raven tpl = Shoulder.Template(false);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, mid, Quaternion.identity, null, Mathf.Clamp(tall * 1.1f, 0.6f, 3f), -1); // (the game's feathers, a falcon's size)
            Material mat = Feathers.Smoke();
            if (mat != null) BeastLook.Puffs(mat, mid, ParticleSystemShapeType.Sphere, tall * 0.4f, 18, tall * 0.7f, tall * 1.3f, 2.2f, 1.8f, 1.2f, BurstColour);
            GameObject lg = new GameObject("RavenWatch_FalconChange");
            lg.transform.position = mid;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(BurstColour.r, BurstColour.g, BurstColour.b); l.range = 8f; l.shadows = LightShadows.None;
            lg.AddComponent<BeastLook.FadeLight>().Begin(l, BurstLight, BurstSeconds);
        }
        private static string Where(Vector3 v) { return Mathf.RoundToInt(v.x) + "," + Mathf.RoundToInt(v.y) + "," + Mathf.RoundToInt(v.z); }

        internal string Describe()
        {
            Player p = Player.m_localPlayer;
            return StateNames[_state] + " at " + Where(transform.position)
                + (p != null ? ", " + Mathf.RoundToInt(Vector3.Distance(p.transform.position, transform.position)) + " m from you" : "")
                + ", " + _speed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s, over the ground " + (transform.position.y - Ground(transform.position)).ToString("0.0", CultureInfo.InvariantCulture) + " m"
                + ", tall " + _height.ToString("0.00", CultureInfo.InvariantCulture) + " m, wings " + _span.ToString("0.00", CultureInfo.InvariantCulture) + " m, upright " + _upright.ToString("0.00", CultureInfo.InvariantCulture)
                + ", beating " + _beatW.ToString("0.00", CultureInfo.InvariantCulture) + (Hold > 0 ? ", held shape " + Hold : "") + (Slow != 1f ? ", clock ×" + Slow.ToString("0.##", CultureInfo.InvariantCulture) : "");
        }

        // ---------------------------------------------------------------- flight

        private static float Ground(Vector3 p)
        {
            if (ZoneSystem.instance == null) return p.y - 10f;
            return Mathf.Max(ZoneSystem.instance.GetGroundHeight(p), ZoneSystem.instance.m_waterLevel);
        }

        // where it stands at a place: a rock, a log or a floor at most 3 m over the ground there, else the ground itself — never
        // a tree's crown (a ray from high up stopped in the leaves: the first landing in a wood went onto a crown, 2026-11-12)
        internal static Vector3 Footing(Vector3 p)
        {
            float g = Ground(p);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, g + 3f, p.z), Vector3.down, out hit, 6f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return hit.point;
            return new Vector3(p.x, g, p.z);
        }

        // standing at once on a place (no flight): its feet on it, facing yaw
        internal void StandAt(Vector3 ground, float yaw)
        {
            _target = ground; _hdg = yaw * Mathf.Deg2Rad;
            transform.position = ground + Vector3.up * _standUp;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _upright = 1f; _speed = 0f; _vy = 0f; _pitch = 0f; _bank = 0f; _beatW = 0f; _burst = 0;
            _raise = StandBack; _sweep = StandDroop; _elbow = StandElbow; _hand = StandHand;
            Go(Standing, Standing);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * Slow; // (its own clock: slowed or stopped for the author's look)
            if (dt <= 0f) return;
            _clock += dt; _now += dt;
            Player me = Player.m_localPlayer;
            if (me == null || Vector3.Distance(me.transform.position, transform.position) > 400f) { Destroy(gameObject); return; }
            if (Owner != 0L && _now >= _ownerAt) // (another's scene's copy, its owner gone: no «leave» will come — off by itself; once a second)
            {
                _ownerAt = _now + 1f;
                if (PerchView.PlayerOf(Owner) == null) { Owner = 0L; if (_state != Leaving) Leave(transform.position - me.transform.position); }
            }
            Vector3 pos = transform.position;
            float want = Cruise; Vector3 aim = pos + transform.forward * 10f;
            bool beat = false;
            switch (_state)
            {
                case Circling:
                    {
                        Vector3 c = _around != null ? _around.position : _center;
                        Vector3 off = pos - c; off.y = 0f;
                        float ang = off.sqrMagnitude > 0.01f ? Mathf.Atan2(off.z, off.x) : 0f;
                        ang += 0.6f; // (a point ahead on the ring: it flies round, the left way)
                        aim = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * CircleRadius;
                        aim.y = Mathf.Max(Ground(c) + CircleUp, _crown + CrownClear);
                        want = Soar;
                        if (_burst <= 0 && _now > _nextBurst) { _burst = SoarBeats; _nextBurst = _now + SoarEvery * UnityEngine.Random.Range(0.6f, 1.4f); }
                        if (_now > _nextCall) { if (_nextCall > 0f) Call(CallSound); _nextCall = _now + CallEvery * UnityEngine.Random.Range(0.6f, 1.5f); } // (not the moment it starts circling: a while after)
                        break;
                    }
                case Diving:
                    if (!_stooping)
                    {
                        // its run-in, as a falcon sets up a stoop: too near the place, out along its line first (StoopBack + 30 m), then back in toward
                        // it, high; over the gate (StoopBack m short of it) and headed at it — the stoop. Never set up in StoopSetup s: the stoop from there
                        Vector3 flatTo = _target - pos; flatTo.y = 0f;
                        float back = flatTo.magnitude, high = pos.y - _target.y;
                        if (_outbound && back >= StoopBack + 20f) _outbound = false;
                        Vector3 gate = _target - _stoopIn * (_outbound ? StoopBack + 30f : StoopBack); gate.y = _target.y + StoopHigh;
                        aim = gate; want = Cruise;
                        bool set = !_outbound && back <= StoopBack + 3f && high > StoopHigh * 0.6f && Vector3.Angle(Facing, flatTo) < 45f;
                        if (set || _now - _since > StoopSetup)
                        {
                            if (back > 1f) _stoopIn = flatTo / back; // (the line from where it is: straight at the place)
                            Vector3 line = PullPoint() - pos;
                            RaycastHit block;
                            if (Physics.SphereCast(pos, 1.2f, line.normalized, out block, line.magnitude, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) // heavy: one cast as a stoop begins
                            { Story.Log("falcon: no clear stoop from " + Where(pos) + " (" + block.collider.name + "): down the glide path"); Go(Landing, Standing); break; }
                            _stooping = true; _since = _now;
                            Call(StoopSound);
                            Story.Log("falcon: stoops from " + Where(pos) + " at " + Where(_target) + (set ? "" : " (not set up in " + StoopSetup + " s: from where it was)"));
                        }
                    }
                    else if (_now - _since > DiveSeconds) { _stooping = false; Go(Landing, Standing); } // (past its time: down the glide path instead)
                    break;
                case Landing:
                    {
                        Vector3 flat = _target - pos; flat.y = 0f;
                        aim = _target + Vector3.up * (_standUp + Mathf.Min(flat.magnitude * Mathf.Tan(LandSlope * Mathf.Deg2Rad), 30f)); // (down its glide path)
                        want = Approach;
                        float near = Vector3.Distance(pos, _target + Vector3.up * _standUp);
                        float brakeFrom = Mathf.Max(BrakeAt, _speed * _speed / (2f * BrakeDecel)); // (fast out of a stoop: it brakes from farther — no snap to the braking pace)
                        _flare = Mathf.Max(_flare, Mathf.InverseLerp(Mathf.Max(FlareAt, brakeFrom + 9f), brakeFrom, near)); // (never back: it rises once)
                        if (near < brakeFrom) Go(Braking, Standing);
                        break;
                    }
                case Leaving:
                    aim = pos + _awayDir * 100f; aim.y = Ground(pos) + 45f; want = Cruise;
                    if (Vector3.Distance(me.transform.position, pos) > 170f) { Destroy(gameObject); return; }
                    break;
            }

            if (_state == Diving && _stooping) Stoop(dt);
            else if (_state == Braking) Brake(dt, ref beat);
            else if (_state == Standing) Stand();
            else if (_state == TakingOff) TakeOff(dt, ref beat);
            else if (_state == Showing) { transform.position = _showAt; transform.rotation = Quaternion.Euler(0f, _hdg * Mathf.Rad2Deg, 0f); }
            else if (_state == Skimming) SkimOn(dt, ref beat);
            else Fly(dt, aim, want, ref beat);

            // the beat: a few strokes, then a glide (never in a stoop)
            if (_state == Diving && _stooping) { beat = false; _burst = 0; }
            if (_burst > 0) beat = true;
            if (Hold == 2) beat = true; else if (Hold > 0) beat = false;
            _beatW = Mathf.MoveTowards(_beatW, beat ? 1f : 0f, dt / Mathf.Max(0.05f, BeatEase));
            if (_beatW > 0.001f)
            {
                float was = _phase;
                _phase += (_state == Braking ? BrakeBeatHz : BeatHz) * _ownBeat * dt;
                if (Mathf.Floor(_phase) > Mathf.Floor(was) && _burst > 0) _burst--;
            }
            if (_state != Landing && _state != Braking && _state != Skimming) _flare = 0f;
            if (_state != Diving) _stooping = false;
            float up = _state == Standing || _state == Braking ? 1f : _state == Showing ? _showUp : _state == Landing || _state == Skimming ? _flare * FlareUp : 0f;
            if (_state == Braking) up = Mathf.Lerp(FlareUp * _flare, 1f, Mathf.Clamp01((_now - _since) / PostureSeconds)); // (on from the flare's height: no jump)
            if (_state == TakingOff) up = 1f - Mathf.Clamp01((_now - _since) / PostureSeconds);
            _upright = Mathf.MoveTowards(_upright, up, dt / Mathf.Max(0.1f, PostureSeconds));
        }

        // in the air: toward a point, turning as a great bird turns (banked, no sharper than its bank lets), climbing on beats, sinking on glides
        private void Fly(float dt, Vector3 aim, float want, ref bool beat)
        {
            Vector3 pos = transform.position;
            // a new aim takes over from the last one (an order changed: no snap of its course)
            float ease = Mathf.Clamp01((_now - _aimSince) / Mathf.Max(0.05f, AimBlend)); ease = ease * ease * (3f - 2f * ease);
            if (ease < 1f) aim = Vector3.Lerp(_aimFrom, aim, ease);
            _lastAim = aim;
            bool final = (_state == Landing || _state == Diving) && Vector3.Distance(pos, _target) < 18f; // (near its place: the ground ahead is where it goes)
            if (_now >= _probeAt)
            {
                _probeAt = _now + 0.12f;
                Vector3 dir = new Vector3(Mathf.Sin(_hdg) * Mathf.Max(1f, _speed), _vy, Mathf.Cos(_hdg) * Mathf.Max(1f, _speed));
                float reach = 3f + Mathf.Max(1f, _speed) * AvoidLook;
                RaycastHit hit;
                _avoidWant = Vector3.zero;
                if (!final && Physics.SphereCast(pos, Mathf.Clamp(_span * 0.2f, 0.3f, 1.2f), dir.normalized, out hit, reach, RavenMotor.Mask, QueryTriggerInteraction.Ignore))
                    _avoidWant = (hit.normal + Vector3.up * 1.2f).normalized * (reach - hit.distance + 5f) * AvoidPush; // (up and away from what is ahead)
                if (_now >= _crownAt)
                {
                    _crownAt = _now + 0.3f;
                    _crown = Physics.Raycast(new Vector3(pos.x, pos.y + 40f, pos.z), Vector3.down, out hit, 120f, RavenMotor.Mask, QueryTriggerInteraction.Ignore) && hit.point.y < pos.y + 39f ? hit.point.y : Ground(pos);
                }
            }
            _avoid = Vector3.Lerp(_avoid, _avoidWant, Mathf.Clamp01(dt * 4f));
            aim += _avoid;
            Vector3 to = aim - pos, flat = to; flat.y = 0f;
            float wantHdg = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.x, flat.z) : _hdg;
            float d = Mathf.DeltaAngle(_hdg * Mathf.Rad2Deg, wantHdg * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float maxRate = 9.81f * Mathf.Tan(MaxBank * Mathf.Deg2Rad) / Mathf.Max(3f, _speed);
            float rate = Mathf.Clamp(d * 1.2f, -maxRate, maxRate);
            _hdg += rate * dt;
            float bankWant = -Mathf.Atan(rate * Mathf.Max(3f, _speed) / 9.81f) * Mathf.Rad2Deg;
            _bank = Mathf.MoveTowards(_bank, Mathf.Clamp(bankWant, -MaxBank, MaxBank), 40f * dt);

            // (a stoop flies by Stoop, never here: here the way up behind it, and the rest)
            float vyWant = Mathf.Clamp(to.y * 0.6f, -SinkMax * (_state == Landing ? 3.5f : 1f), ClimbMax);
            if (_state != Landing && (pos.y < Ground(pos) + GroundClear || pos.y < _crown + 1.5f)) vyWant = ClimbMax;
            _vy = Mathf.MoveTowards(_vy, vyWant, (_state == Landing && _vy < vyWant - 3f ? PullOut : 3f) * dt); // (out of a stoop: it levels out at once)
            if (pos.y + _vy * dt < Ground(pos) + 0.8f) _vy = Mathf.Max(_vy, 0f); // (never into the ground)

            beat = _vy > 0.6f || _speed < want - 1.5f;
            float acc = _state == Landing && _speed > want + 1f ? PullOut : beat ? 3f : 1.5f; // (out of a stoop: wings spread, it sheds its speed)
            _speed = Mathf.MoveTowards(_speed, want, acc * dt);
            float h = Mathf.Max(_speed * 0.3f, Mathf.Sqrt(Mathf.Max(0f, _speed * _speed - _vy * _vy)));
            Vector3 fwd = new Vector3(Mathf.Sin(_hdg), 0f, Mathf.Cos(_hdg));
            transform.position = pos + fwd * h * dt + Vector3.up * _vy * dt;
            float floor = Ground(transform.position) + 0.8f; // (never through the ground)
            if (transform.position.y < floor) { transform.position = new Vector3(transform.position.x, floor, transform.position.z); _vy = Mathf.Max(_vy, 0f); }
            float pitchWant = Mathf.Clamp(Mathf.Atan2(-_vy, h) * Mathf.Rad2Deg, -45f, 40f);
            if (_state == Landing) pitchWant *= LandPitch; // (down the glide path its body near level: a raptor sinks, it does not dive)
            _pitch = Mathf.MoveTowards(_pitch, pitchWant, 60f * dt);
            transform.rotation = Quaternion.Euler(_pitch, _hdg * Mathf.Rad2Deg, _bank);
        }

        // the low run: along its line at one pace, down from where it began to SkimUp over the ground (eased both ways), then level over
        // the ground as it lies; the last SkimFlare m its wings and breast begin to rise. Over its place: Stride takes it (LateUpdate) —
        // none told: down onto it as a landing
        private void SkimOn(float dt, ref bool beat)
        {
            Vector3 pos = transform.position, dir = Facing;
            Vector3 flat = _target - pos; flat.y = 0f;
            float total = Mathf.Max(1f, Utils.DistanceXZ(_skimFrom, _target));
            _speed = Mathf.MoveTowards(_speed, SkimSpeed, 4f * dt);
            float step = _speed * dt;
            _skimLeft = Vector3.Dot(flat, dir) - step; // (m still to go along its line: below zero — over its place, or past it)
            Vector3 next = pos + dir * step;
            float low = Ground(next) + SkimUp, y;
            if (_skimLeft > _skimRun)
            {
                float k = Mathf.InverseLerp(total, _skimRun, _skimLeft); k = k * k * (3f - 2f * k);
                y = Mathf.Lerp(_skimFrom.y, low, k);
            }
            else y = Mathf.Lerp(pos.y, low, 1f - Mathf.Exp(-6f * dt)); // (level: over the ground as it rises and falls, softly)
            _vy = dt > 1e-5f ? (y - pos.y) / dt : 0f;
            transform.position = new Vector3(next.x, Mathf.Max(y, Ground(next) + 0.5f), next.z);
            _bank = Mathf.MoveTowards(_bank, 0f, 60f * dt);
            _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(Mathf.Atan2(-_vy, Mathf.Max(1f, _speed)) * Mathf.Rad2Deg * 0.6f, -20f, 30f), 60f * dt);
            transform.rotation = Quaternion.Euler(_pitch, _hdg * Mathf.Rad2Deg, _bank);
            _flare = _skimLeft < SkimFlare ? Mathf.InverseLerp(SkimFlare, 0f, _skimLeft) * SkimFlareShare : 0f;
            beat = _skimLeft > _skimRun * SkimGlide;
            if (_skimLeft <= 0f && !_stride) { _skimLeft = float.MaxValue; Go(Landing, Standing); }
        }

        // a line in to stoop down that no tree, wall or roof is in: the way it comes from, else turned 30°, 60° … either way (a stoop through a
        // crown, 2026-11-13); none clear: the way it comes from
        private Vector3 ClearLine(Vector3 way)
        {
            for (int k = 0; k < 12; k++)
            {
                Vector3 w = Quaternion.AngleAxis(30f * ((k + 1) / 2) * (k % 2 == 1 ? 1f : -1f), Vector3.up) * way;
                Vector3 gate = _target - w * StoopBack; gate.y = _target.y + StoopHigh;
                Vector3 pull = _target - w * PullBack + Vector3.up * (_standUp + PullUp);
                RaycastHit hit;
                if (!Physics.SphereCast(gate, 1.2f, (pull - gate).normalized, out hit, (pull - gate).magnitude, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) // heavy: up to 12 casts once per stoop ordered
                {
                    if (k > 0) Story.Log("falcon: the stoop's line turned " + (30 * ((k + 1) / 2) * (k % 2 == 1 ? 1 : -1)) + "°: the straight one was blocked");
                    return w;
                }
            }
            return way;
        }

        // its cry, here, going with it (every game near flies its own picture: each hears its own)
        private void Call(string sound) { Audio.Sound(sound, transform.position, new SoundAsk { Follow = transform, Owner = "falcon" }); }

        // where it comes out of a stoop: short of the place on its line in, over its feet
        private Vector3 PullPoint() { Vector3 at = _target - _stoopIn * PullBack; at.y = Mathf.Max(_target.y, Ground(at)) + _standUp + PullUp; return at; } // (over the ground there too: on a slope it was inside the hill)

        // the stoop: straight down its line at the pull-out point, faster and faster (gravity, a little drag), wings swept close; there it
        // comes out into the landing (Fly sheds the speed: PullOut)
        private void Stoop(float dt)
        {
            Vector3 pos = transform.position, to = PullPoint() - pos;
            float d = to.magnitude;
            _speed = Mathf.Min(StoopMax, _speed + 9.81f * 0.8f * dt);
            Vector3 v = d > 0.01f ? to / d * _speed : Vector3.zero;
            transform.position = pos + v * Mathf.Min(dt, d / Mathf.Max(0.1f, _speed));
            _vy = v.y;
            Vector3 flat = v; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) _hdg = Mathf.Atan2(flat.x, flat.z);
            _bank = Mathf.MoveTowards(_bank, 0f, 60f * dt);
            _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(Mathf.Atan2(-v.y, flat.magnitude) * Mathf.Rad2Deg, -10f, 80f), 120f * dt);
            transform.rotation = Quaternion.Euler(_pitch, _hdg * Mathf.Rad2Deg, _bank);
            _lastAim = PullPoint(); // (the landing's aim eases in from here, not from the gate behind it)
            bool low = transform.position.y < Ground(transform.position) + _standUp + PullUp * 0.5f; // (the ground rises under it: out now)
            if (d < 2f || low) { _stooping = false; Story.Log("falcon: out of the stoop at " + Where(transform.position) + ", " + _speed.ToString("0", CultureInfo.InvariantCulture) + " m/s"); Go(Landing, Standing); }
        }

        // the last metres: slowing hard with its wings high, its feet forward, sitting up, onto the place
        private void Brake(float dt, ref bool beat)
        {
            beat = true;
            Vector3 pos = transform.position, to = _target + Vector3.up * _standUp - pos; // (its shoulders over the place: its feet on it)
            float left = to.magnitude;
            _speed = Mathf.Max(Mathf.MoveTowards(_speed, 0f, BrakeDecel * dt), Mathf.Min(left * 1.6f, 3f)); // (slower the nearer, never short of the place: 2026-11-12 it hung 5 m up)
            float step = Mathf.Min(left, Mathf.Min(_speed, Mathf.Max(left * 1.6f, 0.4f)) * dt);
            transform.position = pos + (to.sqrMagnitude > 1e-6f ? to.normalized * step : Vector3.zero);
            _bank = Mathf.MoveTowards(_bank, 0f, 60f * dt);
            _pitch = Mathf.MoveTowards(_pitch, 0f, 90f * dt);
            transform.rotation = Quaternion.Euler(_pitch, _hdg * Mathf.Rad2Deg, _bank);
            _vy = 0f;
            if (to.magnitude < 0.05f && _now - _since > PostureSeconds) { transform.position = _target + Vector3.up * _standUp; Go(Standing, Standing); _speed = 0f; }
        }

        // sitting where it landed: its shoulders as high over its feet as its numbers say now (tuned live: not floating, not sunk)
        private void Stand()
        {
            transform.rotation = Quaternion.Euler(0f, _hdg * Mathf.Rad2Deg, 0f);
            _pitch = 0f; _bank = 0f; _vy = 0f; _speed = 0f;
            transform.position = _target + Vector3.up * _standUp;
        }

        // off the ground: deep beats, sitting down into flight, climbing; then where it was told
        private void TakeOff(float dt, ref bool beat)
        {
            beat = true;
            _speed = Mathf.MoveTowards(_speed, Cruise, 4f * dt);
            _vy = Mathf.MoveTowards(_vy, ClimbMax * 1.4f, 4f * dt);
            Vector3 fwd = new Vector3(Mathf.Sin(_hdg), 0f, Mathf.Cos(_hdg));
            transform.position += fwd * _speed * dt + Vector3.up * _vy * dt;
            _pitch = Mathf.MoveTowards(_pitch, -15f, 30f * dt);
            transform.rotation = Quaternion.Euler(_pitch, _hdg * Mathf.Rad2Deg, 0f);
            if (_now - _since > TakeOffSeconds) Go(_after, _after);
        }

        // ---------------------------------------------------------------- the body: posture, wings, head, legs

        private void LateUpdate()
        {
            if (_skin == null || _root == null || Hidden) return;
            // the change: the burst once its wings have beaten, then smaller and gone; grown out of a burst
            // the cloak of wings: wrapped more and more and as tall as a person at its end — then gone at once behind its feathers (the person stands there
            // whole: Then); made of a person, the same the other way round
            float wrap = Hold == 6 ? 1f : 0f;
            if (_wrapFrom >= 0f)
            {
                float t = Mathf.Clamp01((_now - _wrapFrom) / Mathf.Max(0.05f, WrapSeconds));
                wrap = _unwrap ? 1f - t : t; wrap = wrap * wrap * (3f - 2f * wrap);
                if (t >= 1f)
                {
                    if (_unwrap) { _wrapFrom = -1f; wrap = 0f; }
                    else { Action then = Then; Then = null; FeatherBurst(_changeAt, _height * WrapTall, 1f, true); Destroy(gameObject); if (then != null) then(); return; }
                }
            }
            // the step out of flight: its feet low over the ground as it flares down (or it stands already) — gone behind its feathers, the person walks on from there
            // (the low run: over its place, at its speed — its feathers fly on with it, more of them come after the one who walks on, a warm light)
            bool rush = _state == Skimming && _skimLeft <= 0f;
            if (_stride && (rush || _state == Standing || ((_state == Braking || _state == Landing) && transform.position.y - _standUp - Ground(transform.position) < StrideUp)))
            {
                Vector3 feet = Footing(transform.position);
                Action<Vector3, float> then = ThenStride; ThenStride = null; _stride = false;
                if (rush) Rush(feet, Facing * _speed);
                else FeatherBurst(feet, FalconPerson, 0.75f, false);
                Destroy(gameObject);
                if (then != null) then(feet, _hdg * Mathf.Rad2Deg);
                return;
            }
            // (the low run's last metres with a change told: it rises breast first, wings flung up, and grows toward her height — the eye is led from a bird to a standing shape before the feathers take it)
            float rise = _state == Skimming && _stride && _skimLeft < RushRise ? 1f - Mathf.Clamp01(_skimLeft / Mathf.Max(0.1f, RushRise)) : 0f;
            rise = rise * rise * (3f - 2f * rise);
            if (rise > 0f) _upright = Mathf.Max(_upright, rise * RushUpright);
            float k = Mathf.Lerp(1f, RushTall, rise);

            _vis.transform.localScale = _baseScale * k * Mathf.Lerp(1f, WrapTall, wrap); // (its feet stay on the ground: its standing height is measured each frame, below)
            // the posture: the model laid along its flight, or upright on its feet (its line raised by StandTilt)
            Quaternion fly = Quaternion.AngleAxis(-FlyPitch, Vector3.right) * _flyRest;
            Quaternion stand = Quaternion.AngleAxis(-Mathf.Lerp(StandTilt, WrapTilt, wrap), Vector3.right) * _flyRest;
            _vis.transform.rotation = transform.rotation * Quaternion.Slerp(fly, stand, _upright);

            // the wings' shape for its way now (or the one held), eased; the beat over it
            float tr, ts, th, te = 0f;
            int way = Hold > 0 ? Hold : Changing ? 6 : _state == Diving && _stooping ? 3 : _state == Braking || rise > 0f ? 4 : _state == Standing ? 5 : 1;
            if (way == 3) { tr = StoopUp; ts = StoopSweep; th = StoopHand; }
            else if (way == 4) { tr = BrakeHigh; ts = -BrakeForward; th = BrakeHand; }
            else if (way == 5) { tr = StandDroop; ts = StandBack; te = StandElbow; th = StandHand; }
            else if (way == 6) { tr = WrapRaise; ts = -WrapForward; te = WrapElbow; th = WrapHand; } // (drawn round itself: the cloak)
            else { tr = GlideUp; ts = GlideSweep; th = 0f; }
            if (way == 1 && _flare > 0f) { tr = Mathf.Lerp(tr, BrakeHigh, _flare * FlareWings); ts = Mathf.Lerp(ts, -BrakeForward, _flare); } // (the flare: wings up and forward)
            float dt = Time.deltaTime * Slow, rate = ShapeRate * dt;
            _raise = Mathf.MoveTowards(_raise, tr, rate); _sweep = Mathf.MoveTowards(_sweep, ts, rate); _hand = Mathf.MoveTowards(_hand, th, rate * 1.5f); _elbow = Mathf.MoveTowards(_elbow, te, rate * 1.5f);
            _legs = Mathf.MoveTowards(_legs, _state == Braking ? BrakeLegs : _state == Landing || _state == Skimming ? Mathf.Lerp(-FlyLegs, BrakeLegs, _flare) : _upright < 0.5f ? -FlyLegs : StandLegs, 90f * dt); // (in flight tucked as made, braking forward, standing under it)
            float f = _phase - Mathf.Floor(_phase);
            float stroke = Mathf.Lerp(-BeatDown, BeatUp, 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * f)); // (top at 0, bottom at ½)
            float rising = f > 0.5f ? Mathf.Sin(2f * Mathf.PI * (f - 0.5f)) : 0f;                       // (the way up: 0 … 1 … 0)
            float raise = Mathf.Lerp(_raise, (way == 4 ? _raise * 0.5f : 0f) + stroke, _beatW);
            float sweep = _sweep + _beatW * rising * BeatSweep, hand = _hand + _beatW * rising * BeatHand;

            // each turn in its parent's frame (at rest the model's: its bones lie unturned). Per side, sg turns the right wing's
            // way into the left's: raise lifts the tip about its way ahead, sweep takes it back about its up
            for (int s = 0; s < 2; s++)
            {
                float sg = _side[s];
                _arm[s].localRotation = Quaternion.AngleAxis(sg * raise, _ahead) * Quaternion.AngleAxis(sg * sweep, Vector3.up);
                _fore[s].localRotation = Quaternion.AngleAxis(sg * _elbow, Vector3.up);
                _hnd[s].localRotation = Quaternion.AngleAxis(sg * hand, Vector3.up);
                _tip[s].localRotation = Quaternion.AngleAxis(sg * hand * 0.4f, Vector3.up);
                if (_thigh[s] != null) _thigh[s].localRotation = Quaternion.AngleAxis(-_legs, _right); // (forward: its feet out under it)
            }
            float bend = StandNeck * _upright - FlyHead * (1f - _upright); // (in flight its head raised, its beak seen; standing its beak forward)
            float look = _state == Standing ? LookAround * (0.7f * Mathf.Sin(_clock * 0.35f) + 0.3f * Mathf.Sin(_clock * 1.1f)) : 0f;
            _neck.localRotation = Quaternion.AngleAxis(look, Vector3.up) * Quaternion.AngleAxis(bend, _right);
            _tail.localRotation = Quaternion.AngleAxis(StandTail * _upright, _right) * Quaternion.AngleAxis(-_bank * TailSteer * (1f - _upright), _ahead); // (lifted off the ground standing; twisted with its bank in a turn)

            // where it is: held by its body's middle; on its feet that middle stands _standUp over its lowest foot (measured now:
            // its tilt, legs and size as they are — tuned live)
            _vis.transform.position += transform.position - _root.position;
            if (_upright < 0.5f && _beatW > 0.001f) _vis.transform.position += Vector3.up * (-Mathf.Cos(2f * Mathf.PI * f) * BeatBob * _beatW * (_height / Height)); // (highest as the stroke ends, lowest as it begins: the picture only — its flight's line stays)
            if (_foot[0] != null && _foot[1] != null && _upright > 0.5f)
            {
                float feet = Mathf.Min(Lowest(_foot[0]), Lowest(_foot[1]));
                _standUp = Mathf.Max(0.05f, _root.position.y - feet);
            }
        }

        // the lowest point of a foot: its toes' end (its leaf), else its joint
        private static float Lowest(Transform foot) { return foot.childCount > 0 ? Mathf.Min(foot.position.y, foot.GetChild(0).position.y) : foot.position.y; }

        // ---------------------------------------------------------------- the author's console
        // raven story falcon <word> … — the list in Help. Every answer goes into the game's log as well (Story: «falcon:»), so the
        // studio's «raven story falcon …» and the log show what it did; the studio photographs it too (falcon <name>: StudioFalcon.cs).

        internal const string Help = "raven story falcon — де він і що робить (нема — прилітає й кружляє над тобою)\n"
            + "  here [м=4] — одразу стоїть на землі перед тобою, лицем до тебе\n"
            + "  show <glide|beat|stoop|brake|stand> [side|front|back] [м=7] — завис перед тобою в цій формі крил (stand — на землі)\n"
            + "  turn <°> — обернути того, що стоїть чи показується\n"
            + "  circle [радіус] [висота] · dive · land · leave · gone · add (ще один)\n"
            + "  hold <glide|beat|stoop|brake|stand|off> — тримати форму крил у польоті\n"
            + "  slow <k> — його годинник: 1 звичайно, 0.25 учетверо повільніше, 0 стоп\n"
            + "  size <м> — зріст стоячи (усі, що є, і нові)\n"
            + "  set <поле> <число> · get <поле> · values (усі числа, вони й у лозі) · reset (як у збірці)";

        internal static string Cmd(Player p, string[] a)
        {
            string said;
            try { said = Run(p, a); }
            catch (Exception e) { said = "помилка: " + e.Message; Story.Log("falcon: " + e); }
            Story.Log("falcon: " + string.Join(" ", a) + " → " + said.Replace('\n', ' '));
            return said;
        }

        private static string Run(Player p, string[] a)
        {
            string w = a.Length > 2 ? a[2].ToLowerInvariant() : "";
            string w3 = a.Length > 3 ? a[3].ToLowerInvariant() : "";
            Vector3 fwd = (GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward); fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            float view = Quaternion.LookRotation(fwd).eulerAngles.y;
            int n = 0;
            switch (w)
            {
                case "":
                    if (Live.Count > 0) return Status();
                    return Come(p, fwd) ?? "Летить і кружлятиме над тобою.\n" + Help;
                case "add": return Come(p, fwd) ?? "Ще один летить. Їх " + Live.Count + ".";
                case "help": return Help;
                case "here":
                    {
                        Vector3 at = Footing(p.transform.position + fwd * Num(a, 3, 4f));
                        float yaw = Quaternion.LookRotation(Flat(p.transform.position - at)).eulerAngles.y;
                        Falcon f = First(at, yaw);
                        if (f == null) return "Не зроблено: " + Problem + " (лог: «falcon», «asset»)";
                        f.StandAt(at, yaw);
                        return "Стоїть перед тобою. " + f.Describe();
                    }
                case "show":
                    {
                        int hold = Shape(w3);
                        if (hold == 0) return "show <glide|beat|stoop|brake|stand|wrap> [side|front|back] [м]";
                        string side = a.Length > 4 ? a[4].ToLowerInvariant() : "side";
                        float dist = Num(a, side == "side" || side == "front" || side == "back" ? 5 : 4, 7f);
                        // which side of it you see: its right side (its way across your look), its face, its back
                        float yaw = side == "front" ? view + 180f : side == "back" ? view : view + 90f;
                        Vector3 ground = Footing(p.transform.position + fwd * dist);
                        Falcon f = First(ground, yaw);
                        if (f == null) return "Не зроблено: " + Problem;
                        if (hold == 5 || hold == 6) { Hold = hold == 6 ? 6 : 0; f.StandAt(ground, yaw); } // (wrap: on its feet, held wrapped and as tall as a person)
                        else { Hold = hold; f.ShowAt(ground + Vector3.up * (f.Tall * 1.3f), yaw, hold == 4 ? 0.5f : 0f); }
                        return "Показується: " + w3 + ", " + side + ", " + dist + " м. " + f.Describe();
                    }
                case "turn": foreach (Falcon f in Mine()) { f.Turn(Num(a, 3, 90f)); n++; } return "Обернуто: " + n;
                case "circle": if (a.Length > 3) CircleRadius = Num(a, 3, CircleRadius); if (a.Length > 4) CircleUp = Num(a, 4, CircleUp); foreach (Falcon f in Mine()) { f.Circle(p.transform, p.transform.position); n++; } return "Кружляють: " + n + " (радіус " + CircleRadius + " м, висота " + CircleUp + " м)";
                case "dive": { Vector3 at = Footing(p.transform.position + fwd * 9f); foreach (Falcon f in Mine()) { f.Dive(at); n++; } return "Кидаються на " + Where(at) + ": " + n; }
                case "land": { Vector3 at = Footing(p.transform.position + fwd * 9f); foreach (Falcon f in Mine()) { f.Land(at); n++; } return "Сідають на " + Where(at) + ": " + n; }
                case "leave": foreach (Falcon f in Mine()) { f.Leave(f.transform.position - p.transform.position); n++; } return "Відлітають: " + n;
                case "gone": foreach (Falcon f in Mine()) { Destroy(f.gameObject); n++; } Hold = 0; return "Прибрано: " + n;
                case "hold": Hold = Shape(w3); return Hold == 0 ? "Крила знову за польотом." : "Крила тримають «" + w3 + "».";
                case "slow": Slow = Mathf.Clamp(Num(a, 3, 1f), 0f, 4f); return "Годинник сокола ×" + Slow.ToString("0.##", CultureInfo.InvariantCulture);
                case "size":
                    {
                        float h = Num(a, 3, Height);
                        if (h < 0.1f || h > 20f) return "size <м>: від 0.1 до 20";
                        Height = h;
                        foreach (Falcon f in Live) f.Resize(h);
                        return "Зріст стоячи " + h.ToString("0.##", CultureInfo.InvariantCulture) + " м (тих, що є, і нових).";
                    }
                case "set": return a.Length > 4 ? Studio.Field("Falcon." + a[3], a[4]) : "set <поле> <число>";
                case "get": return a.Length > 3 ? Studio.Field("Falcon." + a[3], null) : "get <поле>";
                case "values": return Values();
                case "reset":
                    foreach (FieldInfo fi in Tunables()) { object v; if (Defaults.TryGetValue(fi.Name, out v)) fi.SetValue(null, v); }
                    foreach (Falcon f in Live) f.Resize(Height);
                    return "Числа як у збірці.";
            }
            return Help;
        }

        private static string Come(Player p, Vector3 fwd)
        {
            Vector3 from = p.transform.position - fwd * 90f; from.y = Ground(from) + 35f;
            Falcon f = Make(from, Quaternion.LookRotation(fwd).eulerAngles.y);
            if (f == null) return "Не зроблено: " + Problem + " (лог: «falcon», «asset»)";
            f.Launch();
            f.Circle(p.transform, p.transform.position);
            return null;
        }

        // the first one there is (any other stays as it is), else a new one at the place
        private static Falcon First(Vector3 at, float yaw) { List<Falcon> m = Mine(); return m.Count > 0 ? m[0] : Make(at, yaw); }
        // the author's own falcons: a scene's (here or a copy of another's) are its own (the review 2026-11-13)
        private static List<Falcon> Mine() { List<Falcon> l = new List<Falcon>(); foreach (Falcon f in Live) if (f != null && !f.Scene) l.Add(f); return l; }

        private static string Status()
        {
            StringBuilder sb = new StringBuilder("Соколи (" + Live.Count + "):");
            foreach (Falcon f in Live) sb.Append("\n  ").Append(f.Describe());
            return sb.ToString();
        }

        private static int Shape(string w) { return w == "glide" ? 1 : w == "beat" ? 2 : w == "stoop" ? 3 : w == "brake" ? 4 : w == "stand" ? 5 : w == "wrap" ? 6 : 0; }
        private static float Num(string[] a, int i, float def) { float v; return a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        private static List<FieldInfo> Tunables()
        {
            List<FieldInfo> l = new List<FieldInfo>();
            foreach (FieldInfo fi in typeof(Falcon).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (!fi.IsLiteral && !fi.IsInitOnly && (fi.FieldType == typeof(float) || fi.FieldType == typeof(int))) l.Add(fi);
            return l;
        }

        // its numbers now (into the log too, by Cmd): to bake into this file
        private static string Values()
        {
            StringBuilder sb = new StringBuilder();
            foreach (FieldInfo fi in Tunables())
            {
                object v = fi.GetValue(null), d;
                Defaults.TryGetValue(fi.Name, out d);
                bool changed = d != null && !d.Equals(v);
                sb.Append(fi.Name).Append(' ').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append(changed ? "   (у збірці " + Convert.ToString(d, CultureInfo.InvariantCulture) + ")" : "").Append('\n');
            }
            return "Числа сокола (set <поле> <число>):\n" + sb.ToString().TrimEnd('\n');
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
#endif

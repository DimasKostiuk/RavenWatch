#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person falling senseless (NpcVitals «down»), its own way instead of the game's «rest» emote — it lay down calmly,
    // as if to sleep, comic after a blow (the user, 2026-10-31). Put on its bones after the game's animation, on every
    // game (nothing sent: its ZDO already says it is down; the moment it changed is seen here):
    //   the fall     its own body gone limp (the user 2026-11-23: «зробити йому регдол, але щоб його не відшвирювало надто далеко. А коли
    //                він впаде, тоді плавно його в позу лежачу переводити»): NpcRagdoll puts a body, a collider and a joint on its bones
    //                for those seconds, its animator off (Limp: no layer of its pose writes a bone meanwhile — NpcPose). How it will lie
    //                is decided as it falls, the same on every game (its character and the fall's own draw, Roll), and the small push is
    //                given that way — backwards for one that lies on its back, to the side it lies on for the rest (Roll), most of it to its
    //                upper body (NpcRagdoll.Begin): so it topples into the lying it is drawn in. Landed (NpcRagdoll.Step), it is itself
    //                again (Land): where its hips came down and which way its head lies are taken as this game's place of its lying
    //                (Anchor, in the world — nothing moves its standing body meanwhile, on no game), and over BlendFor s every bone goes
    //                from where the fall left it to its lying pose (Mix). Each game lies it where its own copy fell (a few tens of cm
    //                apart); its standing body is moved there by its owner only as it gets up (Rise: NpcBody.Put), when no copy is limp.
    //                The hand-drawn fall this replaced (a stagger, onto its knees, down) is gone.
    //                One too far from the eye for it to matter (PhysicsNear), or with no ragdoll to copy: the blend alone, from standing.
    //   the lying    by its character (a steady draw from its id and its ways):
    //                  brave, a fighter   on its side holding its wound, breathing hard; now and then it tries to rise
    //                                     on its arm and sinks back
    //                  timid, old         on its back, arms flung out — senseless, only its breath moves
    //                  the rest           curled up on its side, holding its wound
    //   getting up   about a second and a half: onto its knees, then on its feet (mead given, or come to by itself)
    // The whole skeleton is tipped at its hips (every bone follows) and the hips set down to the ground; the limbs are
    // pointed in the tipped body, the way the player's own animations are written (BodyClips.Pointing: up −90 down …
    // 90 up, aside 0 ahead, 90 right). Its numbers are first guesses: tuned on studio shots.
    internal sealed class NpcFall : MonoBehaviour
    {
        internal enum Way { Side, Curl, Back }

        internal static float UpKneel = 0.8f, UpStand = 0.7f; // s: getting up — onto its knees, then on its feet
        internal static float BlendFor = 0.9f, PhysicsNear = 45f, Turned = 20f, Swung = 60f; // s from how it landed to its lying pose · m from the eye within which it falls limp · degrees its push is off its way · degrees its lying may be turned to fit how it landed
        internal static bool PhysicsOn = true;                // (off: every fall is the blend alone — the author's switch, raven npc tune NpcFall.PhysicsOn 0)
        internal static float KneelHips = 0.55f, SideHips = 0.2f, BackHips = 0.13f, Seen = 90f;
        internal static float RiseEvery = 9f, RiseFor = 2.6f;
        internal static float KneeUp = 55f; // (on its back: the bent leg's thigh and shin, degrees below the body line — 90 = straight)

        private NpcActor _a;
        private Animator _anim;
        private bool _looked, _seen, _down;
        private float _since = -100f, _upSince = -100f;
        private Transform _hips, _spine1, _spine2, _neck, _head, _lUp, _lLeg, _lFoot, _rUp, _rLeg, _rFoot, _lArm, _lFore, _lHand, _rArm, _rFore, _rHand;

        internal bool Busy; // (its bones are ours now: the gait keeps off them — NpcGait)
        internal bool Limp { get { return _rag != null; } } // (its bones are the physics' now: no layer of its pose writes one — NpcPose)

        // the fall: limp (NpcRagdoll), then the blend from how it landed (or stood) to its lying pose
        private NpcRagdoll _rag;
        private bool _blend;
        private float _blendAt;
        private Way _way;
        private Vector3 _push;
        private Character _c;
        private Transform[] _snap; private Quaternion[] _snapAt; // (every bone under its hips as the fall left it: its own turn)
        private Vector3 _snapHips; private Quaternion _snapHipsTurn; // (its hips in the world)
        private Vector3 _face; // (where it faced as it fell: its owner's game keeps its standing body so while it is limp)
        // where it lies on this game (Anchor): the ground under its hips as its copy came down, and its standing body's turn as the lying pose
        // there is drawn — in the world, so that its standing body moved under it (its owner's Rise, on another game) moves nothing of it
        private bool _anchored; private Vector3 _lieAt; private Quaternion _lieTurn;
        private Vector3 _at; private Quaternion _turn; // (this frame: where the pose is drawn from — its standing body, or its place of lying)

        private void Awake() { _a = GetComponent<NpcActor>(); _c = GetComponent<Character>(); }
        private void OnDisable() { if (_rag != null) Stiff(false); } // (taken away mid-fall — its zone asleep, a scene's end: its bones its own again; its parts go with the frame's end)

        // the body's own skeleton (the bones its mesh is skinned to): what it wears or holds may carry bones of the same
        // names, and a pose written into those moves nothing (the first studio check: «senseless», yet standing upright)
        private void Bones()
        {
            _looked = true;
            Transform v = _anim.transform;
            _hips = B(v, "Hips"); _spine1 = B(v, "Spine1"); _spine2 = B(v, "Spine2"); _neck = B(v, "Neck"); _head = B(v, "Head");
            _lUp = B(v, "LeftUpLeg"); _lLeg = B(v, "LeftLeg"); _lFoot = B(v, "LeftFoot");
            _rUp = B(v, "RightUpLeg"); _rLeg = B(v, "RightLeg"); _rFoot = B(v, "RightFoot");
            _lArm = B(v, "LeftArm"); _lFore = B(v, "LeftForeArm"); _lHand = B(v, "LeftHand");
            _rArm = B(v, "RightArm"); _rFore = B(v, "RightForeArm"); _rHand = B(v, "RightHand");
            _all = new[] { _hips, _spine1, _spine2, _neck, _head, _lUp, _lLeg, _lFoot, _rUp, _rLeg, _rFoot, _lArm, _lFore, _lHand, _rArm, _rFore, _rHand };
            _base = new Quaternion[_all.Length];
            _wrote = false;
        }

        // the game does not pose its bones every frame (they still hold ours then): ours laid on ours tipped it twice —
        // into the ground for a frame (a flicker). So: on a frame the game posed, its pose kept; on one it did not, put back
        private Transform[] _all;
        private Quaternion[] _base;
        private Vector3 _baseHips;
        private Quaternion _mine;
        private bool _wrote;

        private void FromGame()
        {
            if (_wrote && _hips.localRotation == _mine)
            {
                for (int i = 0; i < _all.Length; i++) if (_all[i] != null) _all[i].localRotation = _base[i];
                _hips.localPosition = _baseHips;
                return;
            }
            for (int i = 0; i < _all.Length; i++) if (_all[i] != null) _base[i] = _all[i].localRotation;
            _baseHips = _hips.localPosition;
        }
        private Transform B(Transform v, string name) { return BodyClips.SkeletonBone(this, v, name); }

        // ---------------------------------------------------------------- its way of lying, and the side it falls to
        internal static Way WayOf(NpcActor a)
        {
            if (a == null) return Way.Curl;
            NpcPersona w = NpcPersona.Of(a);
            string fight = NpcNature.Manner(a);
            if (w.Bravery >= 0.6f || fight == "defend" || fight == "hunt") return Way.Side;
            if (w.Bravery < 0.35f || w.Stoop > 0.3f) return Way.Back; // (timid, or old and bent)
            return Way.Curl;
        }

        // ---------------------------------------------------------------- this fall's own look (drawn anew every fall)
        // seeded by the person and the game time in 10 s steps: other games mostly draw the same, nothing is sent
        private float _side = 1f, _pace = 1f, _knee = 55f, _legOut = 14f, _roll, _curl, _turnHead = 30f;
        private float _armOut, _armOut2, _bend, _bend2;
        private int _chest; // (on its back: 0 both arms flung, 1 its right hand on its chest, -1 its left)

        private void Roll()
        {
            int seed = (_a.Zdo != null ? (int)NpcKit.Seed(_a.Zdo) : _a.Id.GetStableHashCode()) ^ (int)(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() / 10.0 : Time.time / 10f);
            System.Random r = new System.Random(seed);
            _side = r.NextDouble() < 0.5 ? 1f : -1f;
            _pace = R(r, 0.85f, 1.25f);
            _knee = r.NextDouble() < 0.2 ? 84f : R(r, KneeUp - 15f, KneeUp + 17f); // (now and then both legs straight)
            _legOut = R(r, 4f, 24f);
            _roll = R(r, -14f, 14f);
            _curl = R(r, -0.25f, 0.2f);
            _turnHead = R(r, 12f, 42f) * (r.NextDouble() < 0.8 ? _side : -_side);
            _armOut = R(r, -65f, 12f); _armOut2 = R(r, -65f, 12f); // (up −65: close along its body … 12: flung out past its shoulder)
            _bend = R(r, 10f, 70f); _bend2 = R(r, 10f, 70f);
            double c = r.NextDouble();
            _chest = c < 0.35 ? 0 : c < 0.675 ? 1 : -1;
            // how it will lie, and the push of its fall that way: on its back — pushed backwards; on a side (side 1: on its left) — to that
            // side and a little back; hardly up. Drawn here so that every game pushes its copy the same way and lies it the same way
            _way = WayOf(_a);
            Vector3 f = transform.forward, rt = transform.right;
            Vector3 dir = _way == Way.Back ? -f : (_side > 0f ? -rt : rt) - 0.3f * f;
            dir = Quaternion.Euler(0f, R(r, -Turned, Turned), 0f) * dir; dir.y = 0f;
            _push = dir.normalized * NpcRagdoll.MaxPush * R(r, 0.55f, 1f) + Vector3.up * NpcRagdoll.MaxUp * R(r, 0f, 1f);
            _pushSeed = (uint)seed;
        }
        private uint _pushSeed;
        private static float R(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        // ---------------------------------------------------------------- what is in its hands, put out of sight as it lies
        // (only seen so: the item stays in its hands, and is shown again as it gets up)
        private readonly List<GameObject> _hid = new List<GameObject>();
        private VisEquipment _ve;

        private void HideHands(bool hide)
        {
            if (!hide) { for (int i = 0; i < _hid.Count; i++) if (_hid[i] != null) _hid[i].SetActive(true); _hid.Clear(); return; }
            if (_ve == null) _ve = GetComponent<VisEquipment>();
            if (_ve == null) return;
            HideUnder(_ve.m_rightHand); HideUnder(_ve.m_leftHand);
        }
        private void HideUnder(Transform t)
        {
            if (t == null) return;
            for (int i = 0; i < t.childCount; i++) { GameObject g = t.GetChild(i).gameObject; if (g.activeSelf) { g.SetActive(false); _hid.Add(g); } }
        }

        // ---------------------------------------------------------------- every frame, after the game's pose
        // a layer of the pose (NpcPose: after the game's animation and the gait, before a meal)
        internal void Pose()
        {
            Posing();
            NpcCloth.Hold(gameObject, this, Busy); // (its cape follows our pose while it lies, not the game's standing one)
        }

        private void Posing()
        {
            Busy = false;
            if (_a == null || _a.Zdo == null) return;
            bool down = NpcVitals.IsDown(_a);
            if (!_seen) { _seen = true; _down = down; _anchored = false; if (down) Roll(); } // (lying when first seen: it lies already, no fall — where it stands)
            else if (down != _down)
            {
                _down = down;
                if (down) Fell();
                else
                {
                    if (_rag != null) Land(); else _blend = false; // (brought to mid-fall: landed at once, blended from there into getting up)
                    _upSince = Time.time;
                    if (_a.Owned) Rise();
                }
            }
            float t = (Time.time - _since) / _pace, u = Time.time - _upSince;
            if (!down && u > UpKneel + UpStand) { _wrote = false; _anchored = false; if (_hid.Count > 0) HideHands(false); return; } // (on its feet: the game's own)
            Camera cam = Camera.main;
            bool far = cam != null && (cam.transform.position - transform.position).sqrMagnitude > Seen * Seen;
            if (_rag != null) // limp: the physics has its bones — only watched here, till it has landed (out of sight: landed at once)
            {
                Busy = true;
                if (_a.Owned && _c != null) _c.SetLookDir(_face); // (the game turns a body on the ground to where it looks, and its limp bones with it)
                if (!far && !_rag.Step(Time.deltaTime)) return;
                Land();
            }
            if (far) { _wrote = false; _blend = false; return; }
            if (!Ready()) return;

            float kneel = 0f, lie = 1f;
            if (!down)
            {
                lie = 1f - Smooth(u / UpKneel);
                kneel = 1f - Smooth((u - UpKneel * 0.6f) / UpStand);
            }
            Busy = true;
            if (lie > 0.6f) HideHands(true); else if (_hid.Count > 0) HideHands(false);
            float w = down ? 1f : lie; // (getting up: from its place of lying to its standing body, as it rises)
            if (_anchored) { _at = Vector3.Lerp(transform.position, _lieAt, w); _turn = Quaternion.Slerp(transform.rotation, _lieTurn, w); }
            else { _at = transform.position; _turn = transform.rotation; }
            FromGame();
            TakeKnees();
            Pose(_way, kneel, lie, down ? t : 0f);
            if (_blend) Mix(Smooth((Time.time - _blendAt) / BlendFor));
            _mine = _hips.localRotation; _wrote = true;
        }

        // its animator and its body's own bones (false: no body here yet)
        private bool Ready()
        {
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return false;
            if (!_looked || NpcGait.Stale(_hips, _anim)) Bones(); // (a body rebuilt by new clothes: looked up again)
            return _hips != null;
        }

        // ---------------------------------------------------------------- the fall: limp, landed, blended into its lying
        // it fell this moment (every game: its ZDO said so)
        private void Fell()
        {
            _since = Time.time; _groundAt = -100f; _blend = false; _anchored = false;
            Roll();
            if (!Ready()) return; // (no body here: it lies when it is seen)
            Camera cam = Camera.main;
            if (cam == null) return; // (no eye at all — a server with nobody on it: nothing to show)
            Snapshot(); // (as it stands: what the blend begins from when it does not fall limp)
            _blend = true; _blendAt = Time.time;
            if (!PhysicsOn || (cam.transform.position - transform.position).sqrMagnitude > PhysicsNear * PhysicsNear) return;
            _face = transform.forward;
            if (_a.Owned) Stilled();
            _anim.enabled = false; // (before the parts are made: it would write the bones back under them)
            try { _rag = NpcRagdoll.Begin(_c, _push, _pushSeed); }
            catch (System.Exception e) { _rag = null; Npcs.Log("limp body: " + _a.Id + " — " + e.Message + ": it lies down without falling"); }
            if (_rag == null) { _anim.enabled = true; return; } // (no limp body: the blend alone, from standing)
            _blend = false;
        }

        // The blow's own push is taken off its standing body where it is run: the game slides it on for a moment after a hit, and its limp
        // bones are that body's children — they would be carried along («не відшвирювало надто далеко»)
        private static readonly System.Reflection.FieldInfo FPush = HarmonyLib.AccessTools.Field(typeof(Character), "m_pushForce");
        private void Stilled()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, Mathf.Min(0f, rb.linearVelocity.y), 0f);
            if (FPush != null && _c != null) FPush.SetValue(_c, Vector3.zero);
            if (_c != null) _c.SetLookDir(_face);
        }

        // its bones its own again: the parts off, its animator back on (and posed at once: the layers of its pose stand on the game's).
        // now: the parts gone this moment (landed: the ray under it must not find them); else with the frame's end (OnDisable: it may be going whole)
        private void Stiff(bool now = true)
        {
            if (_rag != null) { _rag.End(now); _rag = null; }
            if (_anim != null) { _anim.enabled = true; if (_anim.isActiveAndEnabled) _anim.Update(0f); }
        }

        // it has landed: where it lies taken off the limp body, and the blend begins from this very pose
        private void Land()
        {
            if (!Ready()) { Stiff(); return; }
            Snapshot();
            Anchor();
            Stiff();
            _blend = true; _blendAt = Time.time; _groundAt = -100f;
            _wrote = false; // (the bones hold the game's pose now, posed this moment: taken as its own, not put back from before the fall)
        }

        // Where it lies on this game, off the limp body: the ground under its hips, and its standing body's turn as the lying pose is drawn
        // so that its head lies where the limp one's does. The lying poses lay the body out from its hips one way each — on its back its
        // head behind it, on a side to the side it lies on (side 1, on its left: to its left) — so the turn is that way's head against the
        // limp head, at most Swung degrees; never farther from its standing body than the fall's leash allows
        private void Anchor()
        {
            _anchored = false;
            if (_neck == null) return;
            Vector3 hips = _hips.position, ground;
            if (!Hit(hips, out ground) || Mathf.Abs(ground.y - hips.y) > 1.2f) ground = new Vector3(hips.x, transform.position.y, hips.z);
            Vector3 off = ground - transform.position; off.y = 0f;
            float most = NpcRagdoll.Far + 1f;
            if (off.magnitude > most) { ground -= off; ground += off.normalized * most; }
            Vector3 head = _neck.position - hips; head.y = 0f;
            Vector3 drawn = _way == Way.Back ? -transform.forward : _side > 0f ? -transform.right : transform.right;
            float yaw = head.sqrMagnitude > 0.01f ? Mathf.Clamp(Vector3.SignedAngle(drawn, head, Vector3.up), -Swung, Swung) : 0f;
            _lieAt = ground; _lieTurn = Quaternion.Euler(0f, yaw, 0f) * transform.rotation; _anchored = true;
        }

        // getting up (its owner's game): its standing body moved to where it lies here and turned as it lies, so that it gets up there; the
        // other games get it as any move of its — none of them has a limp copy now (it lay a while, or each landed as it was brought to)
        private void Rise()
        {
            if (!_anchored || _a.Body == null) return;
            if (Utils.DistanceXZ(_lieAt, transform.position) < 0.05f && Quaternion.Angle(_lieTurn, transform.rotation) < 1f) return;
            _a.Body.Put(_lieAt, "встає там, де лежало", false);
            transform.rotation = _lieTurn;
            if (_c != null) _c.SetLookDir(_lieTurn * Vector3.forward);
        }

        // every bone under its hips as it is now, and its hips in the world
        private void Snapshot()
        {
            if (_snap == null || _snap.Length == 0 || _snap[0] != _hips) _snap = _hips.GetComponentsInChildren<Transform>(); // heavy: once a body (and again when its body is rebuilt), its bones and what hangs on them
            if (_snapAt == null || _snapAt.Length != _snap.Length) _snapAt = new Quaternion[_snap.Length];
            for (int i = 0; i < _snap.Length; i++) if (_snap[i] != null) _snapAt[i] = _snap[i].localRotation;
            _snapHips = _hips.position; _snapHipsTurn = _hips.rotation;
        }

        // from how the fall left it (0) to the pose just made (1)
        private void Mix(float w)
        {
            if (w >= 1f || _snap == null) { _blend = false; return; }
            Vector3 at = Vector3.Lerp(_snapHips, _hips.position, w);
            Quaternion turn = Quaternion.Slerp(_snapHipsTurn, _hips.rotation, w);
            for (int i = 1; i < _snap.Length; i++) if (_snap[i] != null) _snap[i].localRotation = Quaternion.Slerp(_snapAt[i], _snap[i].localRotation, w);
            _hips.SetPositionAndRotation(at, turn);
        }

        // ---------------------------------------------------------------- the pose: tipped, set down, limbs pointed
        private void Pose(Way way, float kneel, float lie, float t)
        {
            // lying along the ground under it (a slope), not flat in the air above one end and into the hill at the other
            if (lie > 0.001f) Ground();
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, _gn), Mathf.Clamp01(lie));
            _hips.rotation = tilt * _hips.rotation;
            _hips.rotation = tilt * (_turn * Quaternion.Inverse(transform.rotation)) * Quaternion.Inverse(tilt) * _hips.rotation; // (its hips turned as its lying is)
            Quaternion body = tilt * _turn;
            float side = _side;
            float fx = way == Way.Back ? -90f : _roll * 0.6f, fz = way == Way.Back ? _roll : side * (90f - Mathf.Abs(_roll) * 0.5f); // (its final tip: on its back, or on a side — a little askew, its own each fall)

            // trying to rise (a brave one, now and then): its tip eased back a little, its head up, a tremble
            float rise = 0f;
            if (way == Way.Side && lie >= 0.99f)
            {
                float cycle = RiseEvery + ((_a.Id.GetStableHashCode() & 7) * 0.4f);
                float k = Mathf.Repeat(t - 3f, cycle);
                if (t > 3f && k < RiseFor) rise = Mathf.Sin(k / RiseFor * Mathf.PI);
            }
            float tx = 15f * kneel * (1f - lie) + fx * lie;
            float tz = fz * lie * (1f - 0.35f * rise) + Mathf.Sin(t * 31f) * 1.5f * rise;
            float h0 = _hips.position.y - transform.position.y; // (its hips standing, as the game holds them now)
            float hLie = way == Way.Back ? BackHips : SideHips;
            float h = Mathf.Lerp(h0, KneelHips, kneel * (1f - lie));
            h = Mathf.Lerp(h, hLie + 0.12f * rise, lie);

            Turn(_hips, body, tx, 0f, tz);
            Vector3 hp = _hips.position + (_at - transform.position); hp.y = _at.y + h; // (where it lies on this game: Anchor)
            if (lie > 0.001f) hp.y = Mathf.Lerp(hp.y, GroundY(hp) + h, Mathf.Clamp01(lie)); // (set down on the slope where its hips are)
            _hips.position = hp;
            Quaternion frame = body * Quaternion.Euler(tx, 0f, tz); // (the body as it is tipped now)

            // its legs, each through its knee (Leg: thigh aimed, knee facing its own way, shin bent only in that plane — no
            // leg twisted about itself): kneeling (thighs down, shins back), then lying (curled, or one knee up and one out)
            float wk = kneel * (1f - lie);
            if (wk > 0.001f)
            {
                Leg(_lUp, _lLeg, _lFoot, _kUpL, _kLegL, frame, -1f, -82f, -6f, 88f, 6f, wk);
                Leg(_rUp, _rLeg, _rFoot, _kUpR, _kLegR, frame, 1f, -82f, 6f, 88f, 6f, wk);
            }
            if (lie > 0.001f)
            {
                if (way == Way.Back)
                {
                    // on its back «ahead» is the sky, 90 its right: one knee up (its shin back down to a foot on the
                    // ground, the knee fallen a little out), the other leg straight and out to its side; which one by its side
                    float bs = side > 0f ? 1f : -1f, ss = -bs;
                    LegOf(bs, frame, -_knee, bs * 15f, Mathf.Clamp(2f * (90f - _knee), 0f, 110f), 14f, lie);
                    LegOf(ss, frame, -90f + _legOut * 0.6f, ss * 90f, 6f, 10f, lie);
                }
                else
                {
                    float curl = Mathf.Clamp01((way == Way.Curl ? 1f : 0.6f) + _curl);
                    Leg(_lUp, _lLeg, _lFoot, _kUpL, _kLegL, frame, -1f, Mathf.Lerp(-80f, -15f, curl), -4f, Mathf.Lerp(15f, 95f, curl), 5f, lie);
                    Leg(_rUp, _rLeg, _rFoot, _kUpR, _kLegR, frame, 1f, Mathf.Lerp(-80f, -30f, curl), 4f, Mathf.Lerp(15f, 70f, curl), 5f, lie);
                }
            }

            // its back and head: curled forward lying on a side; its head to a side on its back;
            // its breath (hard for the hurt, slow for the senseless)
            float breath = Mathf.Sin(Time.time * 2f * Mathf.PI / (way == Way.Back ? 3.6f : 2.1f)) * (way == Way.Back ? 1.4f : 2.6f) * lie;
            if (way == Way.Back)
            {
                Turn(_spine2, frame, breath, 0f, 0f);
                Turn(_head, frame, -6f * lie, _turnHead * lie, 0f);
            }
            else
            {
                float curl = Mathf.Clamp01((way == Way.Curl ? 1f : 0.7f) + _curl);
                Turn(_spine1, frame, (16f * curl - 14f * rise) * lie, 0f, 0f);
                Turn(_spine2, frame, (12f * curl + breath) * lie - 6f * rise, 0f, 0f);
                Turn(_neck, frame, 8f * curl * lie - 10f * rise, 0f, 0f);
                Turn(_head, frame, 10f * curl * lie - 18f * rise, 0f, 0f);
            }

            // its arms: a hand pressed to its wound (a side's way; as it gets up, either way), the other in front; on its back flung out
            float hold = way == Way.Back ? kneel * (1f - lie) : Mathf.Max(kneel, lie);
            if (hold > 0.001f)
            {
                Aim(_rArm, _rFore, frame, -55f, 18f, hold); Aim(_rFore, _rHand, frame, -2f, -95f, hold); // (its hand on its belly)
                Aim(_lArm, _lFore, frame, -40f + 25f * rise, -8f, hold); Aim(_lFore, _lHand, frame, -35f + 30f * rise, 25f, hold); // (in front of it; pushing on it as it tries to rise)
            }
            // on its back: each arm flung on the ground (close along its body … out past its shoulder, its elbow bent towards
            // its head), or one hand on its chest (the arm by its side, the forearm across it)
            if (way == Way.Back && lie > 0.001f)
            {
                if (_chest == -1) Chest(_lArm, _lFore, _lHand, frame, -1f, lie); else Flung(_lArm, _lFore, _lHand, frame, -1f, _armOut, _bend, lie);
                if (_chest == 1) Chest(_rArm, _rFore, _rHand, frame, 1f, lie); else Flung(_rArm, _rFore, _rHand, frame, 1f, _armOut2, _bend2, lie);
            }
        }

        // (on its back the frame's «ahead» is the sky, 90 its right, 180 the ground; up −90 is towards its feet)
        private static void Flung(Transform arm, Transform fore, Transform hand, Quaternion frame, float s, float up, float bend, float share)
        {
            Aim(arm, fore, frame, up, s * 97f, share);
            Aim(fore, hand, frame, Mathf.Clamp(up + bend, -80f, 75f), s * 100f, share);
        }
        private static void Chest(Transform arm, Transform fore, Transform hand, Quaternion frame, float s, float share)
        {
            Aim(arm, fore, frame, -72f, s * 105f, share);
            Aim(fore, hand, frame, 18f, -s * 82f, share); // (across its belly, lying on it — not raised to the sky)
        }

        // ---------------------------------------------------------------- a leg through its knee
        // the knee's own «front» in each bone (taken from the game's standing pose: thigh down, knee ahead)
        private Vector3 _kUpL = Vector3.forward, _kLegL = Vector3.forward, _kUpR = Vector3.forward, _kLegR = Vector3.forward;

        private void TakeKnees()
        {
            KneeOf(_lUp, _lLeg, ref _kUpL); KneeOf(_lLeg, _lFoot, ref _kLegL);
            KneeOf(_rUp, _rLeg, ref _kUpR); KneeOf(_rLeg, _rFoot, ref _kLegR);
        }
        private void KneeOf(Transform bone, Transform next, ref Vector3 k)
        {
            if (bone == null || next == null) return;
            Vector3 along = next.position - bone.position;
            if (along.sqrMagnitude < 1e-6f || Vector3.Angle(along, -transform.up) > 40f) return; // (only from a standing leg)
            Vector3 front = Vector3.ProjectOnPlane(transform.forward, along);
            if (front.sqrMagnitude < 1e-6f) return;
            k = Quaternion.Inverse(bone.rotation) * front.normalized;
        }

        private void LegOf(float s, Quaternion frame, float up, float aside, float bend, float outward, float share)
        {
            if (s > 0f) Leg(_rUp, _rLeg, _rFoot, _kUpR, _kLegR, frame, 1f, up, aside, bend, outward, share);
            else Leg(_lUp, _lLeg, _lFoot, _kUpL, _kLegL, frame, -1f, up, aside, bend, outward, share);
        }

        // s: 1 its right leg, −1 its left; the thigh pointed (up, aside in the frame), the knee bent by «bend» degrees
        // (0 straight) with the shin going behind the knee, the knee turned out by «outward»
        private static void Leg(Transform up, Transform leg, Transform foot, Vector3 kUp, Vector3 kLeg, Quaternion frame, float s, float thUp, float thAside, float bend, float outward, float share)
        {
            if (up == null || leg == null || foot == null || share <= 0f) return;
            Vector3 th = (frame * BodyClips.Pointing(thUp, thAside)).normalized;
            Vector3 kf = Vector3.Cross(th, frame * Vector3.right); // (thigh down → knee ahead; thigh ahead → knee up)
            if (kf.sqrMagnitude < 1e-4f) kf = frame * Vector3.forward;
            kf = Quaternion.AngleAxis(-s * outward, th) * kf.normalized;
            float b = bend * Mathf.Deg2Rad;
            Vector3 sh = (th * Mathf.Cos(b) - kf * Mathf.Sin(b)).normalized;
            Vector3 kf2 = (kf * Mathf.Cos(b) + th * Mathf.Sin(b)).normalized;
            Set(up, leg, kUp, th, kf, share);
            Set(leg, foot, kLeg, sh, kf2, share);
        }
        private static void Set(Transform bone, Transform next, Vector3 kLocal, Vector3 dir, Vector3 front, float share)
        {
            Vector3 aL = next.localPosition;
            if (aL.sqrMagnitude < 1e-8f || next.parent != bone) return;
            aL.Normalize();
            Vector3 kL = Vector3.ProjectOnPlane(kLocal, aL);
            if (kL.sqrMagnitude < 1e-6f) return;
            Quaternion want = Quaternion.LookRotation(dir, front) * Quaternion.Inverse(Quaternion.LookRotation(aL, kL.normalized));
            bone.rotation = Quaternion.Slerp(bone.rotation, want, share);
        }

        // ---------------------------------------------------------------- the ground it lies on: a plane from 4 rays round it
        // (a cross of ±0.8 m), looked again every 2 s (it may be pushed); no steeper than SlopeMax; locally, nothing sent
        internal static float SlopeMax = 40f;
        private Vector3 _gp, _gn = Vector3.up;
        private float _groundAt = -100f;
        private static int _mask;

        private void Ground()
        {
            if (Time.time - _groundAt < 2f) return;
            _groundAt = Time.time;
            Vector3 c = _at;
            Vector3 f = Vector3.ProjectOnPlane(_turn * Vector3.forward, Vector3.up);
            f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 pc, pf, pb, pr, pl;
            _gn = Vector3.up;
            if (!Hit(c, out pc)) { _gp = c; return; }
            _gp = pc;
            if (!Hit(c + f * 0.8f, out pf) || !Hit(c - f * 0.8f, out pb) || !Hit(c + r * 0.8f, out pr) || !Hit(c - r * 0.8f, out pl)) return;
            Vector3 n = Vector3.Cross(pf - pb, pr - pl);
            if (n.sqrMagnitude < 1e-6f) return;
            n.Normalize();
            if (n.y < 0f) n = -n;
            if (Vector3.Angle(Vector3.up, n) > SlopeMax) n = Vector3.RotateTowards(Vector3.up, n, SlopeMax * Mathf.Deg2Rad, 0f);
            _gn = n;
        }

        private static bool Hit(Vector3 at, out Vector3 p)
        {
            RaycastHit h;
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            if (Physics.Raycast(at + Vector3.up * 1.2f, Vector3.down, out h, 3f, _mask)) { p = h.point; return true; }
            p = at; return false;
        }

        // the ground's height under a point, on that plane
        private float GroundY(Vector3 at)
        {
            if (_gn.y < 0.2f) return _gp.y;
            return _gp.y - (_gn.x * (at.x - _gp.x) + _gn.z * (at.z - _gp.z)) / _gn.y;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // a bone turned round the frame's axes (X forward/down, Y right, Z a roll), on top of the game's pose
        private static void Turn(Transform bone, Quaternion frame, float x, float y, float z)
        {
            if (bone == null || (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(z) < 0.01f)) return;
            bone.rotation = frame * Quaternion.Euler(x, y, z) * Quaternion.Inverse(frame) * bone.rotation;
        }

        // a bone pointed in the frame (up −90 down … 90 up; aside 0 ahead, 90 right, −90 left, 180 back), by a share
        private static void Aim(Transform bone, Transform next, Quaternion frame, float up, float aside, float share)
        {
            if (bone == null || next == null || share <= 0f) return;
            Vector3 now = next.position - bone.position;
            if (now.sqrMagnitude < 1e-6f) return;
            Vector3 want = frame * BodyClips.Pointing(up, aside);
            Vector3 down = frame * Vector3.down; // (two short turns through «hanging down», never one of nearly half a circle that flips: NpcGait.Aim)
            Quaternion to = Quaternion.FromToRotation(down, want) * Quaternion.FromToRotation(now, down) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, to, Mathf.Clamp01(share));
        }
    }
}
#endif

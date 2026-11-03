#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person falling senseless (NpcVitals «down»), its own way instead of the game's «rest» emote — it lay down calmly,
    // as if to sleep, comic after a blow (the user, 2026-10-31). Put on its bones after the game's animation, on every
    // game (nothing sent: its ZDO already says it is down; the moment it changed is seen here):
    //   the fall     about a second: it staggers back from the blow, its knees give, it drops onto them, then down
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

        internal static float Stagger = 0.35f, Knees = 0.55f, Collapse = 0.7f, UpKneel = 0.8f, UpStand = 0.7f; // (the fall ≈1.3 s, × its pace)
        internal static float KneelHips = 0.55f, SideHips = 0.2f, BackHips = 0.13f, Seen = 90f;
        internal static float RiseEvery = 9f, RiseFor = 2.6f;
        internal static float KneeUp = 55f; // (on its back: the bent leg's thigh and shin, degrees below the body line — 90 = straight)

        private NpcActor _a;
        private Animator _anim;
        private bool _looked, _seen, _down;
        private float _since = -100f, _upSince = -100f;
        private Transform _hips, _spine1, _spine2, _neck, _head, _lUp, _lLeg, _lFoot, _rUp, _rLeg, _rFoot, _lArm, _lFore, _lHand, _rArm, _rFore, _rHand;

        internal bool Busy; // (its bones are ours now: the gait keeps off them — NpcGait)

        private void Awake() { _a = GetComponent<NpcActor>(); }

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
            string fight = a.Def != null ? a.Def.Fight : "flee";
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
            int seed = (_a.Zdo != null ? _a.Zdo.m_uid.GetHashCode() : _a.Id.GetStableHashCode()) ^ (int)(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() / 10.0 : Time.time / 10f);
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
        }
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
            if (!_seen) { _seen = true; _down = down; if (down) Roll(); } // (lying when first seen: it lies already, no fall)
            else if (down != _down) { _down = down; if (down) { _since = Time.time; _groundAt = -100f; Roll(); } else _upSince = Time.time; }
            float t = (Time.time - _since) / _pace, u = Time.time - _upSince;
            if (!down && u > UpKneel + UpStand) { _wrote = false; if (_hid.Count > 0) HideHands(false); return; } // (on its feet: the game's own)
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > Seen * Seen) { _wrote = false; return; }
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            if (!_looked || NpcGait.Stale(_hips, _anim)) Bones(); // (a body rebuilt by new clothes: looked up again)
            if (_hips == null) return;

            float stag, kneel, lie, crumple = 0f;
            if (down)
            {
                stag = Mathf.Clamp01(t / (Stagger * 0.5f)) * (1f - Smooth((t - Stagger * 0.5f) / Stagger));
                kneel = Smooth((t - Stagger * 0.5f) / Knees);
                float c = Mathf.Clamp01((t - Stagger * 0.5f - Knees * 0.8f) / Collapse);
                lie = c * c * (3f - 2f * c);
                crumple = Mathf.Sin(Mathf.PI * c) * (1f - 0.5f * c); // (bent and reaching out as it goes down, less as it lands)
                lie += Bounce(t - Stagger * 0.5f - Knees * 0.8f - Collapse); // (a small rebound as it hits the ground)
            }
            else
            {
                stag = 0f;
                lie = 1f - Smooth(u / UpKneel);
                kneel = 1f - Smooth((u - UpKneel * 0.6f) / UpStand);
            }
            Busy = true;
            if (lie > 0.6f) HideHands(true); else if (_hid.Count > 0) HideHands(false);
            FromGame();
            TakeKnees();
            Pose(WayOf(_a), stag, kneel, lie, crumple, down ? t : 0f);
            _mine = _hips.localRotation; _wrote = true;
        }

        // ---------------------------------------------------------------- the pose: tipped, set down, limbs pointed
        private void Pose(Way way, float stag, float kneel, float lie, float crumple, float t)
        {
            // lying along the ground under it (a slope), not flat in the air above one end and into the hill at the other
            if (lie > 0.001f) Ground();
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, _gn), Mathf.Clamp01(lie));
            _hips.rotation = tilt * _hips.rotation;
            Quaternion body = tilt * transform.rotation;
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
            float tx = -12f * stag + 15f * kneel * (1f - lie) + fx * lie;
            float tz = fz * lie * (1f - 0.35f * rise) + Mathf.Sin(t * 31f) * 1.5f * rise;
            float h0 = _hips.position.y - transform.position.y; // (its hips standing, as the game holds them now)
            float hLie = way == Way.Back ? BackHips : SideHips;
            float h = Mathf.Lerp(h0, KneelHips, kneel * (1f - lie));
            h = Mathf.Lerp(h, hLie + 0.12f * rise, lie);

            Turn(_hips, body, tx, 0f, tz);
            Vector3 hp = _hips.position; hp.y = transform.position.y + h;
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

            // its back and head: a jolt back from the blow; curled forward lying on a side; its head to a side on its back;
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
            if (stag > 0.001f) { Turn(_spine2, frame, -10f * stag, 0f, 0f); Turn(_head, frame, -16f * stag, 0f, 0f); }
            // as it goes down: bent at the waist, its head lagging back (not one stiff plank tipped at the hips)
            if (crumple > 0.001f)
            {
                Turn(_spine1, frame, 16f * crumple, 0f, side * 6f * crumple);
                Turn(_spine2, frame, 12f * crumple, 0f, 0f);
                Turn(_head, frame, -14f * crumple, 0f, 0f);
            }

            // its arms: a hand pressed to its wound (from the fall on, a side's way), the other in front; on its back flung out
            float hold = way == Way.Back ? Mathf.Max(kneel * (1f - lie), stag) : Mathf.Max(kneel, lie);
            if (hold > 0.001f)
            {
                Aim(_rArm, _rFore, frame, -55f, 18f, hold); Aim(_rFore, _rHand, frame, -2f, -95f, hold); // (its hand on its belly)
                Aim(_lArm, _lFore, frame, -40f + 25f * rise, -8f, hold); Aim(_lFore, _lHand, frame, -35f + 30f * rise, 25f, hold); // (in front of it; pushing on it as it tries to rise)
            }
            // as it goes down its hands go out to catch the ground (the side it falls to the further)
            if (crumple > 0.001f)
            {
                Aim(_lArm, _lFore, frame, -30f, -40f - (side < 0f ? 25f : 0f), crumple); Aim(_lFore, _lHand, frame, -35f, -25f, crumple);
                Aim(_rArm, _rFore, frame, -30f, 40f + (side > 0f ? 25f : 0f), crumple); Aim(_rFore, _rHand, frame, -35f, 25f, crumple);
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
            if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            Vector3 c = transform.position;
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
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

        // after it hits the ground: tipped a hair back up and down again (a third of a second)
        private static float Bounce(float x) { return x <= 0f || x > 0.3f ? 0f : -0.06f * Mathf.Sin(x / 0.3f * Mathf.PI); }

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

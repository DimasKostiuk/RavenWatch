#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person eating at a table (NpcMeals, seated): the bite it took from the pantry in its right hand, raised to its
    // mouth now and then, slowly (the user, 2026-10-31: «трохи замедлити … розтянути»). Its own, put on its bones after
    // the game's sitting pose, on every game: its owner writes what it eats in its ZDO (KEat: the food's prefab), the
    // rest only looks. A bite: the hand up (its head bowing to it), a while chewing, the hand down, a rest in its lap —
    // each a little different (its own pace, drawn per bite). Numbers are first guesses: tuned on studio shots.
    internal sealed class NpcEat : MonoBehaviour
    {
        internal static readonly int KEat = "RavenWatch_npcEat".GetStableHashCode(); // string: the food's prefab while it eats seated, else ""

        internal static float Up = 1.4f, Chew = 2.2f, Down = 1.2f, RestMin = 3.5f, RestMax = 7f; // (a bite ≈ 8–12 s)
        internal static float HeadBow = 7f;
        internal static float MouthAhead = 0.22f, MouthDown = 0.03f, MouthSide = 0.05f; // (where its wrist stops: before its mouth, metres in its body — its fist and the bite reach on to the lips; at 0.11 the hand went into its face)
        internal static Vector3 Elbow = new Vector3(0.45f, -1f, 0.35f); // (where its elbow goes: out, down, a little forward — in its body)
        internal static float FoodSize = 0.18f, FoodAhead = 0.03f; // (its longest side in the hand, metres; how far past its grip toward its face)

        // its owner: what it eats now ("" when not) — the others see it so
        internal static void Set(NpcActor a, string food)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !z.IsOwner()) return;
            if (z.GetString(KEat, "") != (food ?? "")) z.Set(KEat, food ?? "");
        }

        private NpcActor _a;
        private Animator _anim;
        private VisEquipment _ve;
        private bool _looked;
        private Transform _hips, _head, _rArm, _rFore, _rHand;
        private string _food = "";
        private float _nextLook, _stale = -1f;

        private void Awake() { _a = GetComponent<NpcActor>(); }

        private void Bones()
        {
            _looked = true;
            Transform v = _anim.transform;
            _hips = B(v, "Hips"); _head = B(v, "Head");
            _rArm = B(v, "RightArm"); _rFore = B(v, "RightForeArm"); _rHand = B(v, "RightHand");
        }
        private Transform B(Transform v, string name) { return BodyClips.SkeletonBone(this, v, name); }

        // ---------------------------------------------------------------- its bites, one after another
        private float _cycle = -1f, _up, _chew, _down, _rest, _share;
        private int _bite;

        private void NextBite()
        {
            int seed = (_a.Zdo != null ? _a.Zdo.m_uid.GetHashCode() : _a.Id.GetStableHashCode()) ^ (_bite++ * 7919);
            System.Random r = new System.Random(seed);
            float pace = 0.85f + (float)r.NextDouble() * 0.35f; // (its own: one eats slower than another)
            _up = Up * pace; _chew = Chew * pace * (0.8f + (float)r.NextDouble() * 0.5f); _down = Down * pace;
            _rest = RestMin + (float)r.NextDouble() * (RestMax - RestMin);
            _cycle = 0f;
        }

        // how far its hand is up now (0 in its lap … 1 at its mouth), eased in and out
        private float Raise()
        {
            float t = _cycle;
            if (t < _up) return Smooth(t / _up);
            t -= _up; if (t < _chew) return 1f;
            t -= _chew; if (t < _down) return 1f - Smooth(t / _down);
            return 0f;
        }
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ---------------------------------------------------------------- every frame, after the game's pose
        // a layer of the pose (NpcPose: the last, after the gait and a fall)
        internal void Pose()
        {
            if (_a == null || _a.Zdo == null) return;
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + 0.5f;
                string food = _a.Zdo.GetString(KEat, "");
                if (food.Length > 0 && _a.Zdo.IsOwner() && (_a.Mind == null || !_a.Mind.AtTable)) { if (_stale < 0f) _stale = Time.time; else if (Time.time - _stale > 3f) { Set(_a, ""); food = ""; } } // (left from a meal cut off — its zone slept, it was run elsewhere: put away)
                else _stale = -1f;
                Look(food);
            }
            if (_food.Length == 0) return;
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            if (!_looked || NpcGait.Stale(_hips, _anim)) Bones();
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 50f * 50f) return; // (too far to see)
            float dt = Time.deltaTime;
            if (_cycle < 0f) { NextBite(); _cycle = _rest * 0.6f + _up + _chew + _down; } // (first: a moment before its first bite)
            _cycle += dt;
            if (_cycle > _up + _chew + _down + _rest) NextBite();
            _share = Mathf.MoveTowards(_share, 1f, dt * 2f); // (eased in as it starts: no jump onto the first frame)
            float k = Raise() * _share;
            FromGame();
            if (k < 0.001f) { _wrote = false; Hold(); return; } // (in its lap: the bite still before its fingers)
            Quaternion body = transform.rotation;
            Turn(_head, body, HeadBow * k, 0f, 0f); // (its head a little to the bite: smooth, nothing more — a chewing nod twitched)
            Reach(body, k); // (then its hand to its mouth, wherever its head is now)
            Hold(); // (the bite before its fingers, toward its mouth: seen, not hidden in its fist)
            if (_head != null && _rArm != null) { _mineHead = _head.localRotation; _wrote = true; }
        }

        // the game does not pose its bones every frame (they still hold ours then): ours not laid on ours — put back first
        private Quaternion _baseHead, _baseArm, _baseFore, _mineHead;
        private bool _wrote;
        private void FromGame()
        {
            if (_head == null || _rArm == null || _rFore == null) return;
            if (_wrote && _head.localRotation == _mineHead) { _head.localRotation = _baseHead; _rArm.localRotation = _baseArm; _rFore.localRotation = _baseFore; return; }
            _baseHead = _head.localRotation; _baseArm = _rArm.localRotation; _baseFore = _rFore.localRotation;
        }

        // the bite in its hand: shown as its ZDO says, put away when the meal is over (what it held, given back)
        private void Look(string food)
        {
            if (food == _food) return;
            _food = food;
            Drop();
            if (food.Length == 0) { _share = 0f; _cycle = -1f; _wrote = false; return; }
            if (_ve == null) _ve = GetComponent<VisEquipment>();
            if (_ve == null || _ve.m_rightHand == null) return;
            HideUnder(_ve.m_rightHand);
            _held = Model(food);
            if (_held == null) Npcs.Log(_a.Id + ": no look found for its bite " + food);
            if (_held == null) return;
            _held.transform.SetParent(_ve.m_rightHand, false);
            _held.transform.localPosition = new Vector3(0f, 0.02f, 0.03f);
            _held.transform.localRotation = Quaternion.identity;
        }

        private GameObject _held;
        private readonly List<GameObject> _hid = new List<GameObject>();

        private void Drop()
        {
            if (_held != null) Destroy(_held);
            _held = null;
            for (int i = 0; i < _hid.Count; i++) if (_hid[i] != null) _hid[i].SetActive(true);
            _hid.Clear();
        }
        private void HideUnder(Transform t)
        {
            for (int i = 0; i < t.childCount; i++) { GameObject g = t.GetChild(i).gameObject; if (g.activeSelf) { g.SetActive(false); _hid.Add(g); } }
        }
        private void OnDestroy() { if (_held != null) Destroy(_held); }

        // the food's look only, hand-sized (HandLook: its meshes copied off the game's item)
        private static GameObject Model(string food) { return HandLook.Copy(food, FoodSize); }

        // the bite in its fist (the game's grip in the palm, where a sword is held — not the wrist bone: from there it hung
        // on its forearm, 2026-11-01), a little toward its face (the hand's own axes differ body to body: put by its bones)
        private void Hold()
        {
            if (_held == null || _rHand == null || _head == null) return;
            Transform grip = _ve != null && _ve.m_rightHand != null ? _ve.m_rightHand : _rHand;
            Vector3 to = _head.position - grip.position;
            if (to.sqrMagnitude < 1e-4f) return;
            _held.transform.position = grip.position + to.normalized * FoodAhead;
        }

        // its hand brought to its mouth: the elbow put where upper arm and forearm just reach (two bones, their own
        // lengths — any body, any pose it sits in), out and down; each bone turned toward its point by a share
        private void Reach(Quaternion body, float k)
        {
            if (_rArm == null || _rFore == null || _rHand == null || _head == null) return;
            Vector3 sh = _rArm.position;
            float a = Vector3.Distance(sh, _rFore.position), b = Vector3.Distance(_rFore.position, _rHand.position);
            if (a < 0.05f || b < 0.05f) return;
            Vector3 mouth = _head.position + body * new Vector3(MouthSide, -MouthDown, MouthAhead);
            Vector3 to = mouth - sh;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.01f);
            Vector3 dir = to.normalized;
            Vector3 pole = body * Elbow.normalized;
            Vector3 side = Vector3.ProjectOnPlane(pole, dir);
            if (side.sqrMagnitude < 1e-6f) return;
            side.Normalize();
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 elbow = sh + a * (cos * dir + Mathf.Sqrt(1f - cos * cos) * side);
            AimAt(_rArm, _rFore, elbow, k);
            AimAt(_rFore, _rHand, mouth, k);
        }

        private static void AimAt(Transform bone, Transform next, Vector3 point, float share)
        {
            Vector3 now = next.position - bone.position, want = point - bone.position;
            if (now.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
            Quaternion to = Quaternion.FromToRotation(now, want) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, to, share);
        }

        // a bone turned round the body's axes, on top of the game's pose (NpcGait's own)
        private static void Turn(Transform bone, Quaternion body, float x, float y, float z)
        {
            if (bone == null || (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(z) < 0.01f)) return;
            bone.rotation = body * Quaternion.Euler(x, y, z) * Quaternion.Inverse(body) * bone.rotation;
        }

    }
}
#endif

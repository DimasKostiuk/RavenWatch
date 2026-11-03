#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's gait (npc-plan.md, «хода»): everyone walks the game's one walk — so on top of it each person carries
    // itself in its own way, put on its bones after the game's animation (every game, nothing sent, nothing kept):
    //   a kind of gait   plain · elder (bent, slow, hands behind its back) · soldier (straight, chin up, wide swing) ·
    //                    spry (quick, a spring in the step) · waddle (rolls from side to side) · limp (one leg spared) ·
    //                    meek (head down, small steps, arms close) — from its template («gait: elder»), its story (a
    //                    lame leg), its age, its way of fighting, its ways; else a steady draw from its id
    //   its own measure  a little more or less of each (its id): no two of a kind alike
    //   how it is now    tired — slower, more bent; hungry — its head down; low — it trudges, its arms hang; glad —
    //                    a lighter step (its toil, its hunger: in its ZDO, so every game sees the same)
    // The pace of its legs follows its speed (no feet sliding on the ground). Bones are turned the way the player's own
    // animations are written (BodyClips): round the body's axes (X forward/down, Z a roll), or pointed in the body
    // (an arm hanging back, a forearm across the back). Off on a seat, in a blow, lying, in a gesture.
    internal sealed class NpcGait : MonoBehaviour
    {
        internal enum Kind { Plain, Elder, Soldier, Spry, Waddle, Limp, Meek }

        internal sealed class Style
        {
            public Kind Kind;
            public float Speed = 1f;  // × its walk
            public float Stoop;       // 0 straight … 1 bent (≈ 24° of its back); below 0 the chest out
            public float Head;        // degrees: + looks down, − chin up
            public float Swing = 1f;  // × its arms' swing
            public float Sway;        // degrees its hips roll a step
            public float Bob;         // metres its step springs
            public float Limp;        // 0 … 1: one leg spared
            public bool ArmsBack;     // its hands behind its back as it strolls
        }

        // ---------------------------------------------------------------- its style (once per person, kept on it)
        internal static Style Of(NpcActor a)
        {
            if (a == null || a.Def == null || a.Zdo == null) return Plain;
            NpcPersona w = NpcPersona.Of(a);
            if (a.GaitKept != null && a.GaitFor == w) return a.GaitKept;
            a.GaitKept = Make(a, w); a.GaitFor = w;
            return a.GaitKept;
        }

        private static readonly Style Plain = new Style();

        private static Style Make(NpcActor a, NpcPersona w)
        {
            uint seed = (uint)(a.Zdo.m_uid.ID * 2246822519u) ^ (uint)a.Zdo.m_uid.UserID ^ 0x5bd1e995u;
            Kind k = Pick(a, w, ref seed);
            Style s = new Style { Kind = k };
            float j = Roll(ref seed), j2 = Roll(ref seed); // (its own measure of its kind)
            switch (k)
            {
                case Kind.Elder: s.Speed = 0.78f + j * 0.1f; s.Stoop = 0.55f + j2 * 0.35f; s.Head = 4f + j * 6f; s.Swing = 0.35f; s.Sway = 1.5f; s.ArmsBack = j2 > 0.3f; break;
                case Kind.Soldier: s.Speed = 1.04f + j * 0.08f; s.Stoop = -0.2f - j2 * 0.15f; s.Head = -4f - j * 4f; s.Swing = 1.3f + j2 * 0.2f; break;
                case Kind.Spry: s.Speed = 1.1f + j * 0.1f; s.Stoop = 0f; s.Head = -2f; s.Swing = 1.2f; s.Bob = 0.018f + j2 * 0.012f; break;
                case Kind.Waddle: s.Speed = 0.9f + j * 0.06f; s.Stoop = 0.15f; s.Head = 0f; s.Swing = 1.1f; s.Sway = 5f + j2 * 3f; break;
                case Kind.Limp: s.Speed = 0.82f + j * 0.08f; s.Stoop = 0.2f; s.Head = 3f; s.Swing = 0.9f; s.Limp = 0.45f + j2 * 0.35f; s.Sway = 2f; break;
                case Kind.Meek: s.Speed = 0.88f + j * 0.06f; s.Stoop = 0.3f + j2 * 0.15f; s.Head = 10f + j * 6f; s.Swing = 0.55f; break;
                default: s.Speed = 1f; s.Stoop = w.Stoop * 0.6f; s.Head = (j - 0.5f) * 6f; s.Swing = 0.85f + j2 * 0.35f; break;
            }
            return s;
        }

        // its kind: what is written of it first, then what it is (its story, its age, its fight), then its ways
        private static Kind Pick(NpcActor a, NpcPersona w, ref uint seed)
        {
            Kind k;
            if (Named(a.Def.Gait, out k)) return k;
            if (NpcBackstory.Fact(a, "кульгає")) return Kind.Limp;
            NpcSettlers.Look look = NpcSettlers.Of(a);
            float r = Roll(ref seed);
            if (look != null && look.Age == 2) return r < 0.7f ? Kind.Elder : r < 0.85f ? Kind.Meek : Kind.Plain;
            if (look != null && look.Age == 0 && r < 0.55f) return Kind.Spry;
            if (a.Def.Fight == "defend" || a.Def.Fight == "hunt") return r < 0.7f ? Kind.Soldier : Kind.Plain;
            if (w.Bravery < 0.35f && w.Chatty < 0.45f && r < 0.6f) return Kind.Meek;
            r = Roll(ref seed);
            return r < 0.4f ? Kind.Plain : r < 0.6f ? Kind.Waddle : r < 0.75f ? Kind.Soldier : r < 0.9f ? Kind.Meek : Kind.Spry;
        }

        // a test (console «raven npc gait <who> <kind>»): its kind on this game only, till its ways are read anew
        internal static string Force(NpcActor a, string word)
        {
            Kind k;
            if (!Named(word, out k)) return "raven npc gait <хто> plain|elder|soldier|spry|waddle|limp|meek — зараз: " + Of(a).Kind.ToString().ToLowerInvariant();
            NpcPersona w = NpcPersona.Of(a);
            NpcDef d = a.Def; string was = d.Gait;
            d.Gait = word.ToLowerInvariant(); a.GaitKept = null;
            try { Of(a); } finally { d.Gait = was; }
            return a.Name + ": " + a.GaitKept.Kind.ToString().ToLowerInvariant();
        }

        private static bool Named(string word, out Kind k)
        {
            k = Kind.Plain;
            switch ((word ?? "").Trim().ToLowerInvariant())
            {
                case "plain": k = Kind.Plain; return true;
                case "elder": k = Kind.Elder; return true;
                case "soldier": k = Kind.Soldier; return true;
                case "spry": k = Kind.Spry; return true;
                case "waddle": k = Kind.Waddle; return true;
                case "limp": k = Kind.Limp; return true;
                case "meek": k = Kind.Meek; return true;
            }
            return false;
        }

        private static float Roll(ref uint seed) { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed % 10000u) / 10000f; }

        // its gait in a word (its card)
        internal static string Word(NpcActor a)
        {
            switch (Of(a).Kind)
            {
                case Kind.Elder: return Lang.T("ходить по-старечому, руки за спиною");
                case Kind.Soldier: return Lang.T("ступає, як вояк");
                case Kind.Spry: return Lang.T("ходить пружно");
                case Kind.Waddle: return Lang.T("ходить перевальцем");
                case Kind.Limp: return Lang.T("накульгує");
                case Kind.Meek: return Lang.T("ходить тихо, похнюпившись");
            }
            return "";
        }

        // ---------------------------------------------------------------- how it is now (eased, so nothing jumps)
        private float _nowSpeed = 1f, _nowStoop, _nowHead, _nowSwing = 1f, _nowBob, _nextState;
        private float _wantSpeed = 1f, _wantStoop, _wantHead, _wantSwing = 1f, _wantBob;

        private void State(NpcActor a)
        {
            if (Time.time < _nextState) return;
            _nextState = Time.time + 1f;
            float tired = NpcToil.Tired(a) / 100f, mood = NpcResident.Is(a) ? NpcToil.Mood(a) / 100f : 0.6f, full = NpcResident.Is(a) ? NpcHunger.Full(a) : 100f;
            float worn = Mathf.Clamp01((tired - 0.5f) / 0.5f);
            _wantSpeed = 1f - 0.18f * worn - (full < 35f ? 0.08f : 0f) - (mood < 0.3f ? 0.08f : 0f) + (mood > 0.75f ? 0.05f : 0f);
            _wantStoop = 0.35f * Mathf.Clamp01((tired - 0.4f) / 0.6f) + (mood < 0.3f ? 0.1f : 0f);
            _wantHead = (full < 35f ? 6f : 0f) + (mood < 0.3f ? 8f : 0f) - (mood > 0.75f ? 3f : 0f);
            _wantSwing = mood < 0.3f ? 0.7f : mood > 0.75f ? 1.15f : 1f;
            _wantBob = mood > 0.75f ? 0.008f : 0f;
            if (a.Owned) a.Pace(Of(a).Speed * _wantSpeed); // (its walk: only where it is run — its speed goes to the others with it)
        }

        private void Ease(float dt)
        {
            float k = dt * 0.5f;
            _nowSpeed = Mathf.MoveTowards(_nowSpeed, _wantSpeed, k * 0.5f);
            _nowStoop = Mathf.MoveTowards(_nowStoop, _wantStoop, k * 0.5f);
            _nowHead = Mathf.MoveTowards(_nowHead, _wantHead, k * 10f);
            _nowSwing = Mathf.MoveTowards(_nowSwing, _wantSwing, k);
            _nowBob = Mathf.MoveTowards(_nowBob, _wantBob, k * 0.02f);
        }

        // ---------------------------------------------------------------- its body
        private NpcActor _a;
        private Character _c;
        private Humanoid _h;
        private NpcFall _fall;
        private NpcBody _body;
        private Vector3 _was; private float _speed; private bool _wasSet; // (its ground speed as seen here, eased)
        private Animator _anim;
        private bool _looked;
        private Transform _hips, _spine1, _spine2, _neck, _head, _lArm, _lFore, _lHand, _rArm, _rFore, _rHand;
        private Quaternion _lAvg = Quaternion.identity, _rAvg = Quaternion.identity;
        private bool _avg, _wrote;
        private Quaternion _mine; // (its back as we left it: the same next frame — the game did not pose it anew)
        private float _animOwn = 1f, _back, _strolling; // (the pace it gave the animation; hands behind it 0…1; strolling 0…1)

        private void Awake() { _a = GetComponent<NpcActor>(); _c = GetComponent<Character>(); _h = _c as Humanoid; _body = GetComponent<NpcBody>(); }

        // the body's own skeleton (the bones its mesh is skinned to), not a thing of the same name it wears or holds (NpcFall)
        private void Bones()
        {
            _looked = true;
            Transform v = _anim.transform;
            _hips = B(v, "Hips"); _spine1 = B(v, "Spine1"); _spine2 = B(v, "Spine2"); _neck = B(v, "Neck"); _head = B(v, "Head");
            _lArm = B(v, "LeftArm"); _lFore = B(v, "LeftForeArm"); _lHand = B(v, "LeftHand");
            _rArm = B(v, "RightArm"); _rFore = B(v, "RightForeArm"); _rHand = B(v, "RightHand");
            _wrote = false;
        }
        private Transform B(Transform v, string name) { return BodyClips.SkeletonBone(this, v, name); }

        // its bones lost: destroyed or no longer under its animator (a body rebuilt by new clothes)
        internal static bool Stale(Transform hips, Animator anim) { return (object)hips != null && (hips == null || !hips.IsChildOf(anim.transform)); }


        // a layer of the pose (NpcPose: after the game's animation, before a fall and a meal)
        internal void Pose()
        {
            if (_a == null || _c == null || _a.Def == null || _a.Zdo == null) return;
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            if (!_looked || Stale(_hips, _anim)) Bones();
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 70f * 70f) { Rest(); return; } // (too far to see: the game's own)
            Style s = Of(_a);
            State(_a);
            float dt = Time.deltaTime;
            Ease(dt);
            bool off = (_body != null && (_body.Attached || _body.ClipBusy)) || _c.InAttack() || NpcVitals.IsDown(_a) || ((_fall != null || (_fall = GetComponent<NpcFall>()) != null) && _fall.Busy) || _a.Zdo.GetString(ZDOVars.s_emote, "").Length > 0 || _c.IsDead();
            _on = Mathf.MoveTowards(_on, off ? 0f : 1f, dt * OnRate); // (off a seat, into a blow, a gesture: its carriage eased out and back, never snapped)
            if (_on <= 0f) { Rest(); return; }
            // the game did not pose its bones this frame (off screen, culled): they still hold ours — nothing more on top
            if (_spine1 != null && _wrote && _spine1.localRotation == _mine) return;
            float v = Speed(dt); // (from how it moves on this game: on another's its body is only placed, its velocity none)
            bool stroll = !off && v > 0.2f && v < 2.6f; // (a walk: a run, a jog keeps the game's own)
            _strolling = Mathf.MoveTowards(_strolling, stroll ? 1f : 0f, dt * 3f);
            Quaternion body = transform.rotation;
            float phase = Phase();
            float wave = Mathf.Sin(phase * 2f * Mathf.PI);

            // the pace of its legs: its speed (its own walk, its kind, how it is now), so the feet keep to the ground; a
            // limp: the bad leg's half of the step slower
            float pace = Mathf.Clamp(1f + (NpcPersona.Of(_a).Walk * s.Speed * _nowSpeed - 1f) * 0.8f, 0.7f, 1.3f);
            if (s.Limp > 0f) pace *= 1f - s.Limp * 0.4f * Mathf.Max(0f, wave) * _strolling;
            float target = Mathf.Lerp(1f, pace, _strolling);
            if ((Mathf.Abs(target - 1f) > 0.002f || Mathf.Abs(_animOwn - 1f) > 0.002f) && Mathf.Abs(_anim.speed - target) > 0.002f) _anim.speed = target; // (only a pace it gave or gives: the game's own left alone)
            _animOwn = target;

            // its back, its head (standing too)
            float stoop = (s.Stoop + _nowStoop) * _on;
            Turn(_spine1, body, stoop * 14f, 0f, 0f);
            Turn(_spine2, body, stoop * 10f, 0f, 0f);
            Turn(_neck, body, -stoop * 9f, 0f, 0f); // (it looks ahead, not at its feet)
            Turn(_head, body, (s.Head + _nowHead) * _on, 0f, 0f);

            // its step: hips rolling, a spring, a spared leg (only as it walks)
            if (_strolling > 0.01f)
            {
                float roll = (s.Sway * wave + s.Limp * 6f * Mathf.Max(0f, wave)) * _strolling;
                if (Mathf.Abs(roll) > 0.01f) { Turn(_hips, body, 0f, 0f, roll); Turn(_spine1, body, 0f, 0f, -roll * 0.6f); }
                float bob = (s.Bob + _nowBob) * (0.5f + 0.5f * Mathf.Cos(phase * 4f * Mathf.PI)) - s.Limp * 0.03f * Mathf.Max(0f, wave);
                if (_hips != null && Mathf.Abs(bob) > 0.0005f) _hips.position += Vector3.up * bob * _strolling;
            }

            // its arms: hands behind its back (an elder strolling, its hands free), else its swing wider or smaller
            bool free = _h == null || _h.m_unarmedWeapon == null || _h.GetCurrentWeapon() == _h.m_unarmedWeapon.m_itemData; // (nothing in its hands: no weapon, no tool)
            _back = Mathf.MoveTowards(_back, s.ArmsBack && free && _strolling > 0.5f ? 1f : 0f, dt * 1.5f);
            float back = _back * _on;
            if (back > 0.01f)
            {
                Aim(_lArm, _lFore, body, -62f, -150f, back); Aim(_lFore, _lHand, body, -12f, 105f, back);
                Aim(_rArm, _rFore, body, -62f, 150f, back); Aim(_rFore, _rHand, body, -12f, -105f, back);
                _avg = false;
            }
            else
            {
                float swing = s.Swing * _nowSwing;
                if (_strolling > 0.01f && Mathf.Abs(swing - 1f) > 0.02f) { SwingArm(_lArm, ref _lAvg, swing, dt); SwingArm(_rArm, ref _rAvg, swing, dt); _avg = true; }
                else _avg = false;
            }
            if (_spine1 != null) { _mine = _spine1.localRotation; _wrote = true; }
        }

        // its ground speed from its moves here (every game sees it walk the same; eased, a net step is not a sprint)
        private float Speed(float dt)
        {
            Vector3 now = transform.position;
            if (!_wasSet || dt <= 0f) { _was = now; _wasSet = true; return _speed; }
            float step = new Vector2(now.x - _was.x, now.z - _was.z).magnitude / dt;
            _was = now;
            if (step > 20f) return _speed; // (put somewhere: not a walk)
            _speed = Mathf.Lerp(_speed, step, Mathf.Clamp01(dt * 6f));
            return _speed;
        }

        // the game's own again (a seat, a blow, too far): its legs' pace back
        // the black box's look at it (NpcPoseWatch, on a jump of the pose)
        internal string Trace() { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "gait on {0:0.00} stroll {1:0.00} back {2:0.00} swing-avg {3} anim {4:0.00}", _on, _strolling, _back, _avg, _anim != null ? _anim.speed : 0f); }

        internal static float OnRate = 4f; // its carriage eased in or out in a quarter of a second
        private float _on;

        private void Rest()
        {
            _avg = false; _back = 0f; _strolling = 0f; _wrote = false; _on = 0f;
            if (_anim != null && Mathf.Abs(_animOwn - 1f) > 0.001f) { _anim.speed = 1f; _animOwn = 1f; }
        }

        // where it is in its step (0…1 a cycle of two steps): the game's walk's own time
        private float Phase()
        {
            AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
            float t = st.normalizedTime;
            return t - Mathf.Floor(t);
        }

        // a bone turned round the body's axes (X forward/down, Y right, Z a roll), on top of the game's pose
        private static void Turn(Transform bone, Quaternion body, float x, float y, float z)
        {
            if (bone == null || (Mathf.Abs(x) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(z) < 0.01f)) return;
            bone.rotation = body * Quaternion.Euler(x, y, z) * Quaternion.Inverse(body) * bone.rotation;
        }

        // a bone pointed in the body (up: −90 down … 90 up; aside: 0 ahead, 90 right, −90 left, 180 back), by a share
        private static void Aim(Transform bone, Transform next, Quaternion body, float up, float aside, float share)
        {
            if (bone == null || next == null) return;
            Vector3 now = next.position - bone.position;
            if (now.sqrMagnitude < 1e-6f) return;
            Vector3 want = body * BodyClips.Pointing(up, aside);
            // in two turns, each short and one way only: from where the game has it to hanging straight down, then to
            // where we want it — one turn of nearly half a circle (the arm swung forward, wanted behind the back) has no
            // one way to go and may flip from frame to frame
            Vector3 down = body * Vector3.down;
            Quaternion to = Quaternion.FromToRotation(down, want) * Quaternion.FromToRotation(now, down) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, to, share);
        }

        // its arm's swing wider or smaller: round where it swings about (its slow average), by a share
        private void SwingArm(Transform arm, ref Quaternion avg, float swing, float dt)
        {
            if (arm == null) return;
            Quaternion now = arm.localRotation;
            avg = _avg ? Quaternion.Slerp(avg, now, Mathf.Clamp01(dt * 1.6f)) : now;
            arm.localRotation = Quaternion.SlerpUnclamped(avg, now, Mathf.Lerp(1f, swing, _strolling));
        }
    }
}
#endif

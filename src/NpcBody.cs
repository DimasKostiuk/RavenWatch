#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // An NPC's body (the layer under its mind, NpcMind): everything physical about a person lives here and nowhere else.
    //   free      the game moves it (walking, falling, turning to where it looks)
    //   attached  held on a point as the game holds a seated or sleeping player: a chair, a bench, a bed, later a ship's
    //             seat or a scene's mark. The game counts it «attached» (no falling, no turning, no walking push), its
    //             body does not bump the thing it sits on, and it is put back on the point every physics step, right
    //             after the game moved it. One way for every seat and bed: nothing to write again for each.
    //   gestures  the game's emotes, as it plays them for players: the owner writes them into the ZDO, every game plays
    //             them (once: wave, bow… · held: sit, relax… until another)
    //   ours      other mods' creature changes kept off a person (Path of Valheiman's rarity and its bigger body,
    //             Elite Creatures' traits): asked of them through their own methods (ChampionOwn), every few seconds
    // The mind says what (Attach/Detach/Gesture); this says how. Runs on every game; what changes the world, only owned.
    internal sealed class NpcBody : MonoBehaviour
    {
        internal static readonly Dictionary<Character, NpcBody> Held = new Dictionary<Character, NpcBody>(); // the attached ones
        internal static bool HoldOn = true; // (studio: nattach off — to compare)

        private Character _c;
        private Rigidbody _rb;
        private ZNetView _nv;
        private ZSyncAnimation _zanim;
        private Animator _anim;
        private Collider _me;

        private Transform _point;
        private string _attachAnim = "";
        private Vector3 _detach;
        private Collider[] _ignored;

        private int _gestureId = -1;
        private string _gestureHeld = "";
        private float _nextOurs;

        internal bool Attached { get { return _on; } } // (not «its point is there»: a seat taken down leaves its point a destroyed one — it is let go all the same)
        private bool _on;
        internal Transform Point { get { return _point; } }
        private bool Owned { get { return _nv != null && _nv.IsValid() && _nv.IsOwner(); } }

        private void Awake()
        {
            _c = GetComponent<Character>();
            _rb = GetComponent<Rigidbody>();
            _nv = GetComponent<ZNetView>();
            _zanim = GetComponent<ZSyncAnimation>();
            _me = GetComponent<CapsuleCollider>();
        }

        private void OnDestroy() { if (_c != null) Held.Remove(_c); }

        // run by another game now: no seat held from here. Run here now: the sitting its last owner left in its ZDO (the game
        // syncs that animation and no one else would take it off) is taken off, unless it sits here already
        private static readonly int KAttach = "RavenWatch_npcAttach".GetStableHashCode(); // string: its attach animation on now
        internal void OwnerChanged(bool here)
        {
            if (!here)
            {
                if (!_on && !_leaving) return;
                Ignore(false);
                _ignored = null; _point = null; _attachAnim = ""; _on = false; _leaving = false;
                if (_c != null) Held.Remove(_c);
                if (_rb != null) _rb.useGravity = true;
                return;
            }
            ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
            string left = z != null ? z.GetString(KAttach, "") : "";
            if (left.Length == 0 || _on) return;
            if (_zanim != null) _zanim.SetBool(left, false);
            z.Set(KAttach, "");
        }

        private void NoteAttach(string anim)
        {
            ZDO z = Owned ? _nv.GetZDO() : null;
            if (z != null && z.GetString(KAttach, "") != anim) z.Set(KAttach, anim);
        }

        private void Update()
        {
            UpdateGesture();
            ClipTerm();
            if (Time.time >= _nextOurs) { _nextOurs = Time.time + 2f; KeepOurs(); }
        }

        // its own ways (NpcPersona): its size here; its gait — its legs' pace, its back, its arms — is NpcGait's
        private NpcActor _actor;
        private NpcPersona Ways { get { if (_actor == null) _actor = GetComponent<NpcActor>(); return _actor != null && _actor.Def != null && _actor.Zdo != null ? NpcPersona.Of(_actor) : null; } }

        // ---------------------------------------------------------------- attached (seat, bench, bed…)
        // point: where it sits/lies · anim: the game's attach animation (attach_chair, attach_bed…) · root: the thing
        // whose colliders it must not bump · detach: where it steps off, in the point's own space (the game's detach offset)
        internal void Attach(Transform point, string anim, GameObject root, Vector3 detach)
        {
            if (point == null || _c == null) return;
            if (Attached) Detach();
            Gesture("", false);
            // onto it in a moment, not at once (the game's own sitting down plays meanwhile); from far (a scene, a step on):
            // at once, and counted as a jump
            EndLeave();
            float far = Vector3.Distance(transform.position, point.position);
            _from = transform.position; _fromTurn = transform.rotation; _blendAt = Time.time;
            _blendFor = far > SnapFrom ? 0f : Mathf.Clamp(far / 1.2f, 0.25f, 0.6f);
            if (_blendFor <= 0f) NpcWalk.Jumped(this, transform.position, point.position, "на сидіння чи ліжко здалеку");
            _point = point; _attachAnim = anim ?? ""; _detach = detach; _on = true;
            _ignored = root != null ? root.GetComponentsInChildren<Collider>() : null;
            Ignore(true);
            if (_attachAnim.Length > 0 && _zanim != null) { _zanim.SetBool(_attachAnim, true); NoteAttach(_attachAnim); } // (synced: every game sees it sit)
            Held[_c] = this;
            Hold();
        }

        // up off it: in a moment (now: at once — it is put somewhere else, or drops senseless)
        internal void Detach(bool now = false)
        {
            if (!Attached) return;
            if (_attachAnim.Length > 0 && _zanim != null) { _zanim.SetBool(_attachAnim, false); NoteAttach(""); }
            Vector3 off = _point != null ? _point.TransformPoint(_detach) : transform.position;
            Ignore(false);
            _ignored = null; _point = null; _attachAnim = ""; _on = false;
            off = FreeAround(off);
            if (now || _c == null)
            {
                if (_c != null) Held.Remove(_c);
                transform.position = off;
                if (_rb != null) { _rb.useGravity = true; _rb.position = off; _rb.linearVelocity = Vector3.zero; }
                return;
            }
            // still held (no falling, no walking push) while it rises and steps off; then the game's again
            _leaving = true; _from = transform.position; _leavePut = _from; _leaveTo = off; _blendAt = Time.time;
            _blendFor = Mathf.Clamp(Vector3.Distance(_from, off) / 1.5f, 0.25f, 0.45f);
            Held[_c] = this;
        }

        internal static float SnapFrom = 2.5f; // m: onto a seat from farther than this — at once (a jump), not a glide
        private Vector3 _from, _leaveTo, _leavePut; private Quaternion _fromTurn;
        private float _blendAt, _blendFor;
        private bool _leaving;

        private float Blend() { if (_blendFor <= 0f) return 1f; float k = Mathf.Clamp01((Time.time - _blendAt) / _blendFor); return k * k * (3f - 2f * k); }

        private void EndLeave()
        {
            if (!_leaving) return;
            _leaving = false;
            if (_c != null && !_on) Held.Remove(_c);
            if (_rb != null) { _rb.useGravity = true; _rb.linearVelocity = Vector3.zero; }
        }

        // up off a seat: where the seat puts it, unless a table, a wall stands there — then the nearest free spot round it
        private static readonly int Solid = LayerMask.GetMask("piece", "Default", "static_solid");
        private static Vector3 FreeAround(Vector3 off)
        {
            if (!Blocked(off)) return off;
            foreach (float r in new[] { 0.6f, 1f, 1.5f, 2f }) // (up off a seat in a crowded hall: further round it, not into the nook between chair and wall)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = NpcKit.Floor(off + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * r);
                    if (Mathf.Abs(p.y - off.y) < 1.2f && !Blocked(p) && !Physics.Linecast(off + Vector3.up * 0.9f, p + Vector3.up * 0.9f, Solid, QueryTriggerInteraction.Ignore)) return p + Vector3.up * 0.05f; // (not through a wall)
                }
            Vector3 v; // (nowhere free round it: the nearest spot on the way finder's map — not left in a nook)
            if (Pathfinding.instance != null && NpcKit.Valid(out v, off, 2.5f, NpcKit.Walker) && Mathf.Abs(v.y - off.y) < 1.2f) return v + Vector3.up * 0.05f; // (not the floor above or below)
            return off;
        }
        private static bool Blocked(Vector3 p) { return Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.3f, Solid, QueryTriggerInteraction.Ignore); }

        private void Ignore(bool on)
        {
            if (_me == null || _ignored == null) return;
            foreach (Collider c in _ignored) if (c != null) Physics.IgnoreCollision(_me, c, on);
        }

        // on its point, still (every physics step, after the game's own motion: NpcHoldPatch)
        internal void Hold()
        {
            if (_leaving)
            {
                if ((transform.position - _leavePut).sqrMagnitude > 0.25f) { EndLeave(); return; } // (put somewhere else as it rose — a jump, a step on: that stands, no pull back)
                float u = Blend();
                Vector3 to = Vector3.Lerp(_from, _leaveTo, u);
                _leavePut = to;
                transform.position = to;
                if (_rb != null) { _rb.useGravity = false; _rb.position = to; _rb.linearVelocity = Vector3.zero; }
                if (u >= 1f) EndLeave();
                return;
            }
            if (_point == null) { if (Owned) Detach(true); return; } // (the seat was taken down under it)
            float k = Blend();
            Vector3 at = k < 1f ? Vector3.Lerp(_from, _point.position, k) : _point.position;
            Quaternion turn = k < 1f ? Quaternion.Slerp(_fromTurn, _point.rotation, k) : _point.rotation;
            transform.position = at;
            transform.rotation = turn;
            if (_c != null) _c.SetLookDir(_point.forward);
            if (_rb == null) return;
            _rb.useGravity = false;
            _rb.position = at;
            _rb.rotation = turn;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // ---------------------------------------------------------------- gestures
        // name "": back to standing · held: a pose until another gesture
        internal void Gesture(string name, bool held)
        {
            ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
            if (z == null || !Owned) return;
            if (string.IsNullOrEmpty(name) && z.GetString(ZDOVars.s_emote, "").Length == 0) return;
            z.Set(ZDOVars.s_emoteID, z.GetInt(ZDOVars.s_emoteID, 0) + 1);
            z.Set(ZDOVars.s_emote, name ?? "");
            z.Set(ZDOVars.s_emoteOneshot, !held);
        }


        // Our own clips (BodyClips: the same as a player's, the same skeleton), on its body's one layer for them —
        // a gesture, a laugh, a nod, a toast. Played where it is owned; its ZDO tells the others (NpcPose takes it there).
        // name "" or "none": eased off. A clip played once ends by itself; one that would not (it loops, it holds its last
        // moment, a value drives it) always has an end all the same: asked for with no seconds it gets LoopTerm (its asker lost, a reload: its ZDO brings it back, nobody left to say «none»).
        // seconds > 0: a term — eased off by itself after it, whatever became of the one who asked (a talk broken off)
        internal const int ClipSlot = 2;
        internal static float ClipFade = 0.3f;
        internal static float LoopTerm = 60f; // s (a scene that wants a loop longer writes its seconds)
        private static readonly int KClipUntil = "RavenWatch_npcClipUntil".GetStableHashCode(); // long: world milliseconds (its ZDO: it may come to be run on another game)
        private float _clipLook;
        internal bool Clip(string name, float seconds = 0f)
        {
            Humanoid h = _c as Humanoid;
            if (h == null || !Owned) return false;
            ZDO z = _nv.GetZDO();
            bool stop = string.IsNullOrEmpty(name) || name == "none";
            PAnimClip c = stop ? null : BodyClips.Find(name);
            if (!stop && c == null) return false; // (no such clip: its caller says so)
            if (seconds <= 0f && NoEnd(c)) { seconds = LoopTerm; Npcs.Log(_c.m_name + ": clip «" + name + "» loops with no term — eased off after " + LoopTerm + " s"); }
            long until = seconds > 0f ? (long)((WorldTime.Now + seconds) * 1000.0) : 0L;
            if (z.GetLong(KClipUntil, 0L) != until) z.Set(KClipUntil, until);
            if (stop) BodyClips.Stop(h, ClipSlot, ClipFade); else BodyClips.Play(h, ClipSlot, name, ClipFade, 1f);
            return true;
        }

        // its term over (twice a second, where it is run)
        private void ClipTerm()
        {
            if (Time.time < _clipLook || !Owned) return;
            _clipLook = Time.time + 0.5f;
            long until = _nv.GetZDO().GetLong(KClipUntil, 0L);
            if (until > 0L && WorldTime.Now * 1000.0 >= until) Clip("");
        }

        private static bool NoEnd(PAnimClip c) { return c != null && (c.Loop || c.Stays || c.Drive != null); } // (round and round, held at its last moment, driven by a value)

        // it walks off: a clip that does not end by itself let go (only such a one with a term is looked up, on its way every frame)
        internal void LoopOff()
        {
            if (!Owned || _nv.GetZDO().GetLong(KClipUntil, 0L) <= 0L) return;
            BodyAnimator a = BodyAnimator.Of(_c);
            if (a != null && NoEnd(BodyClips.Find(a.ClipIn(ClipSlot)))) Clip("");
        }

        // a clip of ours playing on it now (any game: from its pose's player of clips)
        internal bool ClipBusy { get { BodyAnimator a = BodyAnimator.Of(_c); return a != null && a.CodeBusy; } }

        // a pose it holds now (sit, a ritual…: a held one, not a one-off wave) — "" none
        internal string HeldGesture { get { ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; return z != null && !z.GetBool(ZDOVars.s_emoteOneshot, false) ? z.GetString(ZDOVars.s_emote, "") : ""; } }

        private void UpdateGesture()
        {
            ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
            if (z == null) return;
            int id = z.GetInt(ZDOVars.s_emoteID, 0);
            if (id == _gestureId) return;
            _gestureId = id;
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            if (_gestureHeld.Length > 0) _anim.SetBool("emote_" + _gestureHeld, false);
            _gestureHeld = "";
            _anim.SetTrigger("emote_stop");
            string e = z.GetString(ZDOVars.s_emote, "");
            if (e.Length == 0) return;
            _anim.ResetTrigger("emote_stop");
            if (z.GetBool(ZDOVars.s_emoteOneshot, true)) _anim.SetTrigger("emote_" + e);
            else { _gestureHeld = e; _anim.SetBool("emote_" + e, true); }
        }

        // ---------------------------------------------------------------- ours (other mods' creature changes kept off)
        private void KeepOurs()
        {
            if (_c == null || _c.IsDead()) return;
            if (Owned) ChampionOwn.Disown(_c);
            NpcPersona w = Ways;
            float h = w != null ? w.Height : 1f; // (on every game: the size is not sent)
            if (Mathf.Abs(transform.localScale.y - h) > 0.001f) transform.localScale = Vector3.one * h; // (a person is its own size: no other mod's)
        }
    }

    // an attached NPC is «attached» for the game too, as a seated player is
    [HarmonyPatch(typeof(Character), "IsAttached")]
    internal static class NpcAttachedPatch
    {
        private static void Postfix(Character __instance, ref bool __result)
        {
            try { if (!__result && NpcBody.HoldOn && NpcBody.Held.Count > 0 && NpcBody.Held.ContainsKey(__instance)) __result = true; }
            catch (System.Exception e) { PatchGuard.Fail("NpcAttachedPatch", e); }
        }
    }

    // held on its point every physics step, right after the game moved it (the game turns gravity back on in every
    // walking step: without this it sank between the mind's thoughts and was put back — 3 cm, ten times a second)
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    internal static class NpcHoldPatch
    {
        private static void Postfix(Character __instance)
        {
            if (NpcBody.Held.Count == 0 || !NpcBody.HoldOn) return;
            try { NpcBody b; if (NpcBody.Held.TryGetValue(__instance, out b) && b != null) b.Hold(); }
            catch (System.Exception e) { PatchGuard.Fail("NpcHoldPatch", e); }
        }
    }
}
#endif

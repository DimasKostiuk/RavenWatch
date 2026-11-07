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
    //             after the game moved it; its glide onto the seat and off it is drawn every frame too (Glide, from NpcPose). One way for every seat and bed: nothing to write again for each. Come to be run
    //             here while it sits: its sitting kept LeftFor s, so its plan sits it on on the same seat (no standing up)
    //   moved     put somewhere by us, not walked there (Put at once, Slide over seconds): the one way every safety and step moves a body
    //   gestures  the game's emotes, as it plays them for players: the owner writes them into the ZDO, every game plays
    //             them (once: wave, bow… — it stands still for one, GestureHold s · held: sit, relax… until another)
    //   size      its height (NpcPersona): eased when it changes on a living body; over 1 it only looks taller (its capsule
    //             a person's of size 1, Fit); its width (Half) for the seats; its build on its skin and cloth (BodyBuild)
    //   a word's gesture  «{~nod}» in its words (its talk, its lines: Spoken): the mark taken out, our clip of it played — seated its _sit
    //             kind, a hand of the two at random (Words: nod, laugh, proud, shy, shrug, point)
    //   ours      other mods' creature changes kept off a person (Path of Valheiman's rarity and its bigger body,
    //             Elite Creatures' traits): asked of them through their own methods (ChampionOwn), every few seconds
    // The mind says what (Attach/Detach/Gesture); this says how. Runs on every game; what changes the world, only owned.
    internal sealed class NpcBody : MonoBehaviour
    {
        // ---------------------------------------------------------------- a gesture in its words
        // a mark's clip: [standing, seated] — «_l|_r»: either hand
        internal static readonly Dictionary<string, string[]> Words = new Dictionary<string, string[]>
        {
            { "nod", new[] { "fire_nod_l|fire_nod_r", "fire_nod_sit_l|fire_nod_sit_r" } },
            { "laugh", new[] { "fire_laugh_l|fire_laugh_r", "fire_laugh_sit_l|fire_laugh_sit_r" } },
            { "proud", new[] { "fire_proud_l|fire_proud_r", "fire_proud_sit_l|fire_proud_sit_r" } },
            { "shy", new[] { "fire_sheepish_l|fire_sheepish_r", "fire_sheepish_sit_l|fire_sheepish_sit_r" } },
            { "shrug", new[] { "shrug_small", "" } },
            { "point", new[] { "point_at", "" } },
        };

        // its words with their marks taken out; the first mark's gesture sent to the game that runs it (once — whatever game says the words)
        internal static string Spoken(NpcActor a, string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("{~", System.StringComparison.Ordinal) < 0) return text;
            string first = null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf("{~", i, System.StringComparison.Ordinal), close = open >= 0 ? text.IndexOf('}', open) : -1;
                if (open < 0 || close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                if (first == null) first = text.Substring(open + 2, close - open - 2).Trim();
                i = close + 1;
            }
            string[] clips;
            if (a != null && first != null && Words.TryGetValue(first, out clips))
            {
                bool seated = a.Mind != null && a.Mind.SeatedNow;
                string[] pick = (seated && clips[1].Length > 0 ? clips[1] : seated ? "" : clips[0]).Split('|');
                string clip = pick[Random.Range(0, pick.Length)];
                if (clip.Length > 0) a.Order("clip " + clip);
            }
            else if (first != null) Npcs.Log((a != null ? a.Id : "?") + ": no gesture «" + first + "» (NpcBody.Words)");
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "  +", " ").Trim();
        }

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
        private VisEquipment _vis;

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
                _leftAnim = ""; // (its sitting, kept for a moment where it came to be run: not ours to decide now)
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
            _leftAnim = left; _leftUntil = Time.time + LeftFor; // (not taken off at once: its plan, read at its first thought, may sit it on the seat it is on — then it sits on, with no standing up between; else off after LeftFor)
        }

        // the sitting its last owner left on it, kept a moment (two players, 2026-11-03: both elders stood up and sat down
        // again at every handover — taken off here, then put on again by the plan a moment later)
        internal static float LeftFor = 4f; // s
        private string _leftAnim = "";
        private float _leftUntil;
        internal bool SittingLeft { get { return _leftAnim.Length > 0; } } // (NpcMind: its seat is the one it is on)
        private void DropLeft()
        {
            if (_leftAnim.Length == 0) return;
            if (!(_on && _attachAnim == _leftAnim))
            {
                if (_zanim != null) _zanim.SetBool(_leftAnim, false);
                ZDO z = Owned ? _nv.GetZDO() : null;
                if (z != null && z.GetString(KAttach, "") == _leftAnim) z.Set(KAttach, "");
            }
            _leftAnim = "";
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
            if (_sizing) Grow();
            if (_leftAnim.Length > 0 && Time.time >= _leftUntil) DropLeft(); // (no plan sat it on its seat: off)
        }

        // its own ways (NpcPersona): its size and build here; its gait — its legs' pace, its back, its arms — is NpcGait's
        private NpcActor _actor;
        private NpcPersona Ways { get { if (_actor == null) _actor = GetComponent<NpcActor>(); return _actor != null && _actor.Def != null && _actor.Zdo != null ? NpcPersona.Of(_actor) : null; } }

        // half its width at the shoulders (m), by its size and its build: a heavy or a tall one takes more of a bench
        // (NpcMind seats: the next seat is not its neighbour's when the two would not fit between them)
        internal const float NormalHalf = 0.28f;
        internal float Half { get { NpcPersona w = Ways; return w == null ? NormalHalf : NormalHalf * w.Height * (1f + 0.15f * w.Build); } } // (a lean one narrower)

        // its size: at once as it comes, eased over BodyBuild.EaseFor when it changes on a living body
        private float _sizeWant, _sizeFrom, _sizeAt;
        private bool _sizing;
        private void Grow()
        {
            float k = Mathf.Clamp01((Time.time - _sizeAt) / BodyBuild.EaseFor);
            Size(Mathf.Lerp(_sizeFrom, _sizeWant, k * k * (3f - 2f * k)));
            if (k >= 1f) _sizing = false;
        }
        private void Size(float h) { transform.localScale = Vector3.one * h; Fit(h); }

        // a tall one only looks tall: its capsule (what walks, fits a door, a seat, the way) no bigger than a person's of
        // size 1 — at 1.2 it was 2.4 m and would not pass under a door. A small one: smaller with it
        private float _capH0, _capR0, _capY0;
        private void Fit(float h)
        {
            CapsuleCollider cap = _me as CapsuleCollider;
            if (cap == null) return;
            if (_capH0 <= 0f) { _capH0 = cap.height; _capR0 = cap.radius; _capY0 = cap.center.y; }
            float k = h > 1f ? 1f / h : 1f;
            cap.height = _capH0 * k; cap.radius = _capR0 * k;
            cap.center = new Vector3(cap.center.x, _capY0 * k, cap.center.z);
        }

        // ---------------------------------------------------------------- attached (seat, bench, bed…)
        // point: where it sits/lies · anim: the game's attach animation (attach_chair, attach_bed…) · root: the thing
        // whose colliders it must not bump · detach: where it steps off, in the point's own space (the game's detach offset)
        internal void Attach(Transform point, string anim, GameObject root, Vector3 detach)
        {
            if (point == null || _c == null) return;
            if (_leftAnim.Length > 0) { if (_leftAnim == (anim ?? "")) _leftAnim = ""; else DropLeft(); } // (on again as it was: no standing up between)
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
                Place(off);
                if (_rb != null) _rb.useGravity = true;
                return;
            }
            // still held (no falling, no walking push) while it rises and steps off; then the game's again
            _leaving = true; _from = transform.position; _leavePut = _from; _leaveTo = off; _blendAt = Time.time;
            _blendFor = Mathf.Clamp(Vector3.Distance(_from, off) / 1.5f, 0.25f, 0.45f);
            Held[_c] = this;
        }

        // ---------------------------------------------------------------- moved by us, not by its own feet: the one way (2026-11-07)
        // Every safety and step that moves a body comes here: the mind's «put by its goal» and «home» (NpcMind.Unstick, HomeFromStuck), its feet's
        // step up and ease out of a nook (NpcWalkStuck), a body freed from a thing, a long way's step out of sight (NpcTravel). Before, each wrote
        // the transform and the rigidbody itself, and only the seat's glide had learnt to be drawn every frame.
        //   Put    there at once; seen (a player near where it was or where it comes) and farther than PuffFrom: a puff of smoke at both ends
        //   Slide  there over seconds, held as it is when it steps off a seat (the game counts it attached: no falling, no walking push), the
        //          physics step holding it (Hold) and every frame drawing it (Glide). Written only in the mind's steps — twenty a second, the game's
        //          own pace for every creature's mind — it moved in jumps, and its time ran 2.5 times slow (it was reckoned with a frame's time)
        // Both counted with the jumps (NpcWalk.Jumped: raven npc walk report).
        internal static float PuffFrom = 2.5f; // m: a shorter step is no sight worth a puff

        // true: it was seen go (in a puff)
        internal bool Put(Vector3 to, string why, bool puff = true)
        {
            Vector3 from = transform.position;
            NpcActor a = _actor != null ? _actor : (_actor = GetComponent<NpcActor>());
            bool seen = puff && a != null && Vector3.Distance(from, to) > PuffFrom && (NpcWalk.Watched(from) || NpcWalk.Watched(to));
            if (Attached) Detach(true);
            EndLeave();
            if (seen) a.Appear();
            NpcWalk.Jumped(this, from, to, why + (seen ? " (з димком)" : ""));
            Place(to);
            if (seen) a.Appear();
            return seen;
        }

        internal void Slide(Vector3 to, float seconds, string why)
        {
            if (Attached) Detach(true);
            NpcWalk.Jumped(this, transform.position, to, why);
            if (_c == null || !HoldOn) { Place(to); return; } // (no holding — the studio's «nattach off»: nothing would ever end the slide)
            _leaving = true; _from = transform.position; _leavePut = _from; _leaveTo = to; _blendAt = Time.time; _blendFor = Mathf.Max(0.1f, seconds);
            Held[_c] = this; // (held while it slides: Hold each physics step, Glide each frame; EndLeave lets it go)
        }

        // on its way somewhere by Slide, or up off a seat: its feet wait
        internal bool Sliding { get { if (_leaving && !HoldOn) EndLeave(); return _leaving; } } // (holding switched off — the studio's «nattach off» — Hold never runs to end it: ended here, not a person waiting for good)

        private void Place(Vector3 to)
        {
            transform.position = to;
            if (_rb != null) { _rb.position = to; _rb.linearVelocity = Vector3.zero; }
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
            Vector3 at; Quaternion turn;
            Onto(out at, out turn);
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

        // where its glide onto the seat is now: one sum of the time, for the physics step (Hold) and for the frame (LateUpdate)
        private void Onto(out Vector3 at, out Quaternion turn)
        {
            float k = Blend();
            at = k < 1f ? Vector3.Lerp(_from, _point.position, k) : _point.position;
            turn = k < 1f ? Quaternion.Slerp(_fromTurn, _point.rotation, k) : _point.rotation;
        }

        // A glide onto a seat or off it put the body only in the physics steps: written there, so the rigidbody's smoothing is gone, and about one
        // frame in six has no step at 58 frames a second (measured 2026-11-06, JoltWatch: sitting down moved in jumps). While it glides, every frame
        // shows where the same glide is now; the physics step still holds it and decides its end. The body only (no rigidbody), nothing while it does not glide.
        // Called by its pose's pass first of all (NpcPose), so every layer of its pose stands on where its body is shown.
        internal void Glide()
        {
            if (_blendFor <= 0f || !HoldOn || (!_leaving && !_on) || Time.time - _blendAt >= _blendFor) return;
            if (_leaving) { transform.position = Vector3.Lerp(_from, _leaveTo, Blend()); return; }
            if (_point == null) return;
            Vector3 at; Quaternion turn;
            Onto(out at, out turn);
            transform.SetPositionAndRotation(at, turn);
        }

        // ---------------------------------------------------------------- gestures
        // name "": back to standing · held: a pose until another gesture
        internal static float GestureHold = 2.5f; // s: a one-off gesture stands it still this long
        internal void Gesture(string name, bool held)
        {
            ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null;
            if (z == null || !Owned) return;
            if (string.IsNullOrEmpty(name) && z.GetString(ZDOVars.s_emote, "").Length == 0) return;
            z.Set(ZDOVars.s_emoteID, z.GetInt(ZDOVars.s_emoteID, 0) + 1);
            z.Set(ZDOVars.s_emote, name ?? "");
            z.Set(ZDOVars.s_emoteOneshot, !held);
            if (!string.IsNullOrEmpty(name) && Owned && InPose) Clip(""); // (a gesture over how it stood: its arms free for it)
            if (!held && !string.IsNullOrEmpty(name)) { NpcMind m = GetComponent<NpcMind>(); if (m != null) m.Still(GestureHold); } // (the game's gesture moves its legs too: on the way it slid along in it — the user 2026-11-03; it stands for it, then walks on)
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
            BodyAnimator on = stop ? null : BodyAnimator.Of(_c);
            if (on != null && NoEnd(c) && on.Holding(ClipSlot, name)) return true; // (the same one that does not end by itself, asked again: only its term is new — started over, its pose would jump)
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

        // It is to walk (NpcMind.Move, the one way every walk starts): a clip of ours a walk must not carry (PAnimClip.HoldsWalk: its legs, a
        // frozen base, or it touches something — a key, a door) is let go first, by the clip's own way out (BodyAnimator.Stop takes the clip's
        // «fade»), and its feet wait till that has faded: true meanwhile. One the walk may carry (a nod, a look back) plays on. The user
        // 2026-11-07, heid_opens: the scene's «go» came at the key's mark, she walked off sideways with her hands still turning the key.
        // The player's own walk asks the same rule (StoryWalk.LetFeetGo).
        private float _offUntil;
        internal bool ClipFirst()
        {
            if (!Owned) return false;
            BodyAnimator a = BodyAnimator.Of(_c);
            if (a == null) return false;
            if (Time.time < _offUntil) return true; // (its way out under way: asked again when it is over)
            PAnimClip c = a.HoldsWalk(); // (the one rule, the player's walk asks the same — StoryWalk: its legs, a frozen base, or it touches something; a nod or a look plays on over the walk)
            if (c != null && a.ClipIn(ClipSlot) == c.Name) Clip(""); // (through Clip: its term and what the other games are told go with it)
            float left = a.LetGoForWalk(); // (how long till none is left: one let go now, one already on its way out, one fading under the next in a crossfade)
            if (left <= 0.01f) return false;
            _offUntil = Time.time + left;
            Npcs.Log(_c.m_name + ": lets its clip " + (c != null ? "«" + c.Name + "» " : "") + "go before it walks (" + left.ToString("0.0") + " s)");
            return true;
        }

        // it walks off: a clip that does not end by itself let go (only such a one with a term is looked up, on its way every frame)
        internal void LoopOff()
        {
            if (!Owned || _nv.GetZDO().GetLong(KClipUntil, 0L) <= 0L) return;
            BodyAnimator a = BodyAnimator.Of(_c);
            if (a != null && NoEnd(BodyClips.Find(a.ClipIn(ClipSlot)))) Clip("");
        }

        // a clip of ours playing on it now (any game: from its pose's player of clips)
        internal bool ClipBusy { get { BodyAnimator a = BodyAnimator.Of(_c); return a != null && a.CodeBusy; } }

        // a clip that is only how it stands or sits, by the word of who took it (NpcStance; in its ZDO: every game reads it): anything else may
        // take its place, and the motor goes on over it. The same clip asked by a scene or a place is not one (the name alone would say so: Heid's arms_cross)
        internal static readonly int KPose = "RavenWatch_npcPose".GetStableHashCode(); // bool
        internal void PoseWord(bool on) { ZDO z = Owned ? _nv.GetZDO() : null; if (z != null && z.GetBool(KPose, false) != on) z.Set(KPose, on); }
        internal bool PoseSaid { get { ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; return z != null && z.GetBool(KPose, false); } }
        internal bool InPose { get { BodyAnimator a = BodyAnimator.Of(_c); return a != null && a.CodeBusy && PoseSaid; } }
        internal bool ClipFree { get { BodyAnimator a = BodyAnimator.Of(_c); return a == null || !a.CodeBusy || PoseSaid; } }

        // sitting now (any game: its ZDO): on a seat, or on the ground — not in a bed; AttachedSeen: held on any point
        internal string AttachedSeen { get { ZDO z = _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; return z != null ? z.GetString(KAttach, "") : ""; } }
        internal bool SeatsNow { get { string at = AttachedSeen; return (at.Length > 0 && at != "attach_bed") || HeldGesture == "sit"; } }

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
            if (w != null && _actor.Def.Generated && NpcSettlers.Of(_actor) == null) return; // (a settler not made yet: its body waits for its age — not one body, then another before your eyes)
            float h = w != null ? w.Height : 1f; // (on every game: the size is not sent)
            if (Mathf.Abs(_sizeWant - h) > 0.001f)
            {
                bool first = _sizeWant <= 0f;
                _sizeWant = h;
                if (first) Size(h); // (as it comes: at once)
                else { _sizeFrom = transform.localScale.y; _sizeAt = Time.time; _sizing = true; } // (changed on a living body — the window «Вигляд», the story: eased)
            }
            else if (!_sizing && Mathf.Abs(transform.localScale.y - h) > 0.001f) Size(h); // (a person is its own size: another mod's put back at once)
            if (_vis == null) _vis = GetComponent<VisEquipment>();
            BodyBuild.Set(_vis, w != null ? w.Build : 0f); // (nothing unless it changed)
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

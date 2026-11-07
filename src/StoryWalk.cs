#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A scene walks the player to a spot (scene step «player go»): he goes on his own feet, as if by his keys, so the game's
    // walking, slopes and sounds are the game's. Nothing is carried: a thing in the way stops him, and at the end of its
    // time the step gives up and the scene goes on from where he stands, and says how far off that is.
    // What it does to his walking is only for the walk: he goes as he went before it, whichever way it ends (the game keeps
    // the walk switch on until something turns it off).
    // He comes from the side he is on (never round or through the thing): keep the ground round a thing to be taken clear, at
    // least a body's width past where he would stand. «front» makes him go to the side its face looks to:
    // only for a thing whose side matters, and only where nothing stands in between.
    // A clip that follows needs him exactly so before the thing (its body frame is its map), so the step ends only when he
    // stands within Near of the spot and turned within Turned of the way it says: he slows into it, no overshoot, and
    // turns as he comes. The spot is never inside the thing's touch: measured from its edge on his side (a ray at his
    // shins, waist and chest), a body's width off at least (Clear). The spot also stands clear of every part of the thing's touch by
    // Clearance, measured from the part itself (a thing's look may lie off its touch: a signpost's board off its post).
    // Only a few cm off (within Shuffle m): no step to take — he is eased onto it and turned. From farther off he always walks, however
    // near: his feet step (the user 2026-11-07: eased 1.2 m to the table, «ніби підтягує до столу», his feet slid 0.9 m in the check). The
    // walk that once pressed him into the thing is gone: the spot stands clear of its touch (Clear, Clearance) and a walk held ends (BlockedFor). And he never presses on against anything: no way gained for BlockedFor s,
    // the walk ends where he stands and the scene goes on (the user 2026-11-04: «it must never be»). The spot is reckoned from what the eye sees, the thing's meshes' middle (StoryProps.Middle): the
    // same point its look is laid on, not its object's root.
    // (Walking up it is its touch's middle instead, TouchMiddle: he stands the same way off what he reaches for, from any side.)
    // «before»: square before the thing's middle, on its outer side (StoryWhere.Before), wherever he comes from: a gate, a door. Its m is
    // from the thing's face as a ray finds it (FaceEdge), not from its middle: a gate's leaves sit back in their frame, and a clip that
    // touches them is made «m from the surface» (the user 2026-11-04: the hands 0.4 m short of a gate set back in its frame).
    // Under the black (StoryScreen.Dark: a «fade out» before it) he is not walked at all: put on the spot at once (Put).
    // A «before» walk that is held (the side of a stair, the user 2026-11-05: he pushed the air 2.75 m short of the gate): the clip after it
    // touches the thing, so the screen goes black for a moment and he is put there (HeldBlack); a clip in the air is the worse of the two.
    // A walk starts as a body starts: he turns first, softly, on the spot (his feet step by themselves: BodyTurn), and goes only as he
    // comes to face his way, easing into it; never the whole pace at once, turning on the run. Not a walk he is already on («keep»).
    // «back <m>» (Back): he steps back that far, slowly, facing as he faces — the game's own walk backwards (its body turns to where it
    // goes unless it «always rotates to the camera», as when blocking: StoryWalkBackPatch says so while he steps back; without it he
    // turned round, the walk saw him still turning and held his pace at nothing: 0.17 m of 1.2 in 7 s); a clip of ours that holds his
    // feet is let go first and the walk starts as it has faded (feet held by a clip and walked at once slide).
    // A scene that takes up his walk where it is: «keep» — on at the pace he comes at (walking, trotting, running); Pace changes
    // it on the way (under a fade: to a slow walk), so the shot starts with him already going. Every walk slows over the last
    // SoftFrom m, so the game's walk fades out by itself. «soft»: no exact spot to reach (nothing is taken there), he stops
    // within SoftNear; what the scene may wait for on the way: Left.
    internal static class StoryWalk
    {
        internal static float Most = 7f, Near = 0.06f, Turned = 3f, Slow = 0.45f, TurnSpeed = 150f, Least = 0.25f, Body = 0.3f; // (Near, Slow, Least: as tuned in game on the shield; Body: his width)
        internal static float WalkAtLeast = 1f; // m/s: the slowest a walk is given time for
        private static readonly FieldInfo FLookYaw = AccessTools.Field(typeof(Character), "m_lookYaw");

        private static Vector3 _to, _way, _from;
        private static float _until, _shuffleAt, _shuffleFor;
        private static bool _shuffle, _softTurn;
        internal static float FaceSpeed = 110f, FaceEaseIn = 0.35f, FaceEaseEnd = 30f; // deg/s of a turn on the spot · seconds to get up to it · degrees left where it begins to slow
        internal static float Shuffle = 0.15f, ShuffleFor = 0.45f; // m: as near as this, no step to take — eased there · s it takes
        internal static float HeadOver = 0.95f; // m over his chest: the top of his head
        internal static float Clearance = 0.35f, BlockedFor = 0.35f, BlockedGain = 0.03f; // m from any part of the thing's touch · seconds, m: gained less than this in that time, he is held by something
        private static float _gainAt, _gainFar;
        private static float _settleAt = -1f;
        internal static float SettleMost = 1.8f, SettleLeast = 0.4f; // s the scene waits at most for his feet after a walk · s it looks before
        // arrived: the step ends when his feet have settled (BodyTurn), so what follows starts on them standing
        private static void Settle(Player p, string said) { p.SetMoveDir(Vector3.zero); _settleAt = Time.time; StoryGait.Settle(p); Story.Log(said); }
        internal static float WalkFrom = 35f, WalkAll = 15f, PaceIn = 0.5f; // degrees still to turn: he starts to go · he goes at his full pace · seconds to get into his stride
        private static bool _keep;
        private static float _goAt;
        private static bool _run, _slow, _walked, _soft, _back;
        internal static float SoftFrom = 3f, SoftNear = 0.35f, SoftLeast = 0.18f;

        // m still to go (0: no walk on)
        internal static float Until { get { return _until; } } // (when the walk on now gives up: the scene's step waits as long, not its general limit)
        internal static float Left(Player p) { if (!On || p == null) return 0f; Vector3 d = _to - p.transform.position; d.y = 0f; return d.magnitude; }

        internal static bool On { get; private set; }
        internal static bool Backing { get { return On && _back; } } // (he keeps his face: StoryWalkBackPatch)
        internal static float HeldBlack = 0.3f;
        private static bool _contact;
        private static float _heldPutAt = -1f;
        internal static int Helds; // (walks given up, ever: the studio's scene check counts them)
        internal static Vector3 Target { get { return _to; } } // (where the walk on now ends; its way there — the scene's set-up puts him there at once)
        internal static Vector3 Way { get { return _way; } }

        // a note when it cannot, else null. front: on the side the thing's face looks to, turned as it is turned, so what
        // lies before him lies as the clip has it; else from where he comes
        internal static string Go(Player p, string place, float within, bool front, bool run, bool keep = false, bool soft = false, bool before = false)
        {
            Vector3 at, outOf = Vector3.zero;
            ZNetView piece = StoryWhere.Piece(place);
            if (before && StoryWhere.Before(place, out at, out outOf)) { }
            else if (piece != null) at = TouchMiddle(piece);
            else if (!StoryWhere.Find(place, out at)) return "нема місця «" + place + "»";
            Vector3 from = at - p.transform.position; from.y = 0f;
            Vector3 dir = outOf.sqrMagnitude > 0.01f ? -outOf : front && piece != null ? piece.transform.forward : from.sqrMagnitude > 0.0001f ? from.normalized : p.transform.forward;
            dir.y = 0f;
            dir.Normalize();
            _way = dir;
            float face;
            if (before && outOf.sqrMagnitude > 0.01f && FaceEdge(place, at, dir, out face))
            {
                _to = at - dir * (face + Mathf.Max(0.1f, within));
                Story.Log("player go: before — its face " + face.ToString("0.00") + " m out of its middle, he stands " + within.ToString("0.00") + " m off the face");
            }
            else
            {
                if (piece != null) within = Mathf.Max(within, Clear(piece, at, dir, within));
                _to = at - dir * Mathf.Max(0.1f, within);
                if (piece != null) _to = OffTouch(piece, _to, dir);
            }
            Vector3 left = _to - p.transform.position; left.y = 0f;
            _softTurn = false; _haltAt = -1f; _back = false; _contact = before; _heldPutAt = -1f;
            if (!keep && StoryScreen.Dark && left.magnitude > Shuffle) { Put(p, _to, _way); Story.Log("player go: put on the spot under the black, " + left.magnitude.ToString("0.0") + " m off"); return null; } // (nobody sees a walk in the black)
            _shuffle = !keep && !soft && left.magnitude <= Shuffle; // (a few cm off: no step to take)
            float fade = keep || _shuffle ? 0f : LetFeetGo(p); // (a clip holding his feet goes first: walked at once, they slide)
            _gainAt = Time.time + fade + BlockedFor * 1.5f; _gainFar = left.magnitude;
            _keep = keep; _goAt = Time.time + fade;
            if (_shuffle) { _from = p.transform.position; _shuffleAt = Time.time; _shuffleFor = ShuffleFor; }
            _until = Time.time + fade + Mathf.Max(Most, Vector3.Distance(p.transform.position, _to) / WalkAtLeast) + SoftFrom + (keep ? 0f : 180f / FaceSpeed); // (a longer walk: time for it, at any pace he keeps; the slowing too; and for turning first)
            if (!On) _walked = p.GetWalk(); // (a walk after a walk: still the one from before them)
            _run = keep ? p.IsRunning() : run;
            _slow = keep ? p.GetWalk() : !run;
            _soft = soft;
            Gestures.Stand(p); // (not sneaking up to it: the clip after is made standing)
            On = true;
            return null;
        }

        // under the black, or a scene played from a later step (StoryScenes.Setup): put on the spot at once, on the floor under it (not
        // the land under a building), turned its way — no walk nobody sees, however far (a walk of 150 m once kept the picture black for
        // over a minute: the sign 2026-11-04)
        internal static void Put(Player p, Vector3 at, Vector3 way)
        {
            RaycastHit hit; // heavy: one ray, once a walk — down from just above the spot: the floor under it, not a roof over it
            Vector3 o = new Vector3(at.x, Mathf.Max(at.y, p.transform.position.y) + 1.5f, at.z);
            if (Physics.Raycast(o, Vector3.down, out hit, 8f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) at.y = hit.point.y;
            else if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetGroundHeight(at);
            Rigidbody body = p.GetComponent<Rigidbody>();
            p.transform.position = at;
            if (body != null) { body.position = at; body.velocity = Vector3.zero; }
            if (way.sqrMagnitude > 0.01f) { p.transform.rotation = Quaternion.LookRotation(way, Vector3.up); if (FLookYaw != null) { FLookYaw.SetValue(p, Quaternion.LookRotation(way)); p.FaceLookDirection(); } }
            p.SetMoveDir(Vector3.zero);
            Gestures.Stand(p);
        }

        // a clip of ours a walk must let go of (PAnimClip.HoldsWalk: his legs, the whole body, a frozen pose, a touch) goes first, and the walk starts
        // as it has faded — feet held by a clip and walked at once slide (Back from the start; Go too from 2026-11-07). One over his walk (the head,
        // the arms: look_back) plays on with it. The same rule a person's walk asks (NpcBody.ClipFirst)
        private static float LetFeetGo(Player p)
        {
            BodyAnimator a = BodyAnimator.Of(p);
            if (a == null) return 0f;
            PAnimClip c = a.HoldsWalk();
            if (c != null && a.ClipIn(2) == c.Name) Gestures.SceneEnd(p, 0f); // (a scene's pose: its own word that it is over. Only this clip: a look over the walk stays, while the one under it fades)
            return a.LetGoForWalk();
        }

        // he steps back m, facing as he does now
        internal static float BackSlowest = 0.35f; // m/s it is given time for
        internal static void Back(Player p, float m)
        {
            Vector3 f = p.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) return;
            f.Normalize();
            m = Mathf.Clamp(m, 0.2f, 5f);
            float fade = LetFeetGo(p);
            _way = f; _from = p.transform.position; _to = _from - f * m;
            _softTurn = _shuffle = _keep = _soft = _run = false; _haltAt = -1f; _back = true; _slow = true; _contact = false; _heldPutAt = -1f; // (nothing left of a «before» walk: held behind him, he would be put back there under a black)
            _goAt = Time.time + fade;
            _gainAt = _goAt + BlockedFor * 1.5f; _gainFar = m;
            _until = _goAt + m / BackSlowest + SoftFrom;
            if (!On) _walked = p.GetWalk();
            Gestures.Stand(p);
            On = true;
            Story.Log("player back: " + m.ToString("0.00") + " m" + (fade > 0f ? ", after its clip fades (" + fade.ToString("0.0") + " s)" : ""));
        }

        // he turns where he stands, softly, to that way (a scene's «player face»); the scene does not wait for it
        internal static void Face(Player p, Vector3 way, bool slow = false)
        {
            way.y = 0f;
            if (p == null || way.sqrMagnitude < 0.01f) return;
            _faceMul = slow ? SlowFace : 1f; _haltAt = -1f; _back = false;
            if (!On) _walked = p.GetWalk();
            _way = way.normalized;
            _from = _to = p.transform.position;
            _shuffle = _softTurn = true;
            _shuffleAt = Time.time; _shuffleFor = 0.1f;
            _until = Time.time + Most;
            _run = false; _slow = true; _soft = false;
            On = true;
        }

        internal static void Stop() { if (On) Done(Player.m_localPlayer); }

        // he stops where he is, softly, over seconds (a scene's «player stop»: something made him stop short)
        private static float _haltAt = -1f, _haltFor;
        internal static bool Halting { get { return On && _haltAt >= 0f; } }
        internal static void Halt(float seconds) { if (!On || _shuffle) return; _haltAt = Time.time; _haltFor = Mathf.Max(0.1f, seconds); }

        private static float _faceMul = 1f;
        internal static float SlowFace = 0.45f; // × FaceSpeed for «player face … slow»

        // the pace of the walk on now: walk (slow) · jog (the game's own, no key held) · run
        internal static void Pace(string how) { _run = how == "run"; _slow = how == "walk"; }

        private static void Done(Player p)
        {
            _settleAt = -1f; _heldPutAt = -1f;
            On = false; _shuffle = false; _back = false;
            if (p == null) return;
            p.SetMoveDir(Vector3.zero);
            p.SetWalk(_walked);
        }

        // how far from the thing's middle he must stand at least, coming from that way: its touch's edge on his side (rays at his
        // shins, waist and chest, from 3 m off towards it) and his body's width; said when it is more than the scene asks
        private static float Clear(ZNetView piece, Vector3 at, Vector3 dir, float within)
        {
            float edge = 0f;
            float ground = piece.transform.position.y;
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk starts
            {
                if (!c.enabled || c.isTrigger) continue;
                foreach (float up in new[] { 0.3f, 1f, 1.6f })
                {
                    Vector3 o = new Vector3(at.x, ground + up, at.z) - dir * 3f;
                    RaycastHit hit;
                    if (c.Raycast(new Ray(o, dir), out hit, 3f)) edge = Mathf.Max(edge, 3f - hit.distance);
                }
            }
            float need = edge + Body;
            if (need > within + 0.01f) Story.Log("player go: the thing's touch reaches " + edge.ToString("0.00") + " m his way: he stands at " + need.ToString("0.00") + " m, not " + within.ToString("0.00"));
            return need;
        }

        // how far out of its middle, towards him, the face of a thing is (all its pieces of that name: a gate's two halves): rays along his
        // way at the height of hands, a little to each side (the middle may be the gap between two halves); the nearest face
        internal static float FaceHigh = 1.3f, FaceAside = 0.35f;
        private static bool FaceEdge(string place, Vector3 at, Vector3 dir, out float edge)
        {
            edge = 0f; bool any = false;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            foreach (ZNetView nv in StoryWhere.Pieces(place))
                foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk «before» starts
                {
                    if (!c.enabled || c.isTrigger) continue;
                    foreach (float s in new[] { -FaceAside, 0f, FaceAside })
                        foreach (float up in new[] { FaceHigh - 0.3f, FaceHigh })
                        {
                            Vector3 o = new Vector3(at.x, at.y + up, at.z) + side * s - dir * 3f;
                            RaycastHit hit;
                            if (c.Raycast(new Ray(o, dir), out hit, 6f)) { float e = 3f - hit.distance; if (!any || e > edge) edge = e; any = true; }
                        }
                }
            return any;
        }

        // the middle of what can be touched of it, at its look's height (none: its look): a thing's look may lie off its touch
        private static Vector3 TouchMiddle(ZNetView piece)
        {
            Vector3 look = StoryProps.Middle(piece);
            Bounds b = new Bounds(); bool any = false;
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk starts
            {
                if (!c.enabled || c.isTrigger) continue;
                if (any) b.Encapsulate(c.bounds); else { b = c.bounds; any = true; }
            }
            return any ? new Vector3(b.center.x, look.y, b.center.z) : look;
        }

        // the spot moved back along his way till it stands Clearance off every part of the thing's touch, measured from the part
        private static Vector3 OffTouch(ZNetView piece, Vector3 spot, Vector3 dir)
        {
            float chest = piece.transform.position.y + 1f;
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk starts
            {
                if (!c.enabled || c.isTrigger) continue;
                if (c.bounds.min.y > chest + HeadOver) continue; // (over his head: a lintel he walks under)
                Vector3 q = new Vector3(spot.x, Mathf.Clamp(chest, c.bounds.min.y, c.bounds.max.y), spot.z);
                MeshCollider mesh = c as MeshCollider;
                Vector3 near = mesh != null && !mesh.convex ? c.bounds.ClosestPoint(q) : c.ClosestPoint(q); // (the game's rule: no closest point on a concave mesh)
                Vector3 off = near - q; off.y = 0f;
                float d = off.magnitude;
                if (d >= Clearance) continue;
                spot -= dir * (Clearance - d + 0.01f);
                Story.Log("player go: spot moved back " + (Clearance - d).ToString("0.00") + " m off the touch of " + c.name + " (its height " + (c.bounds.min.y - chest + 1f).ToString("0.00") + "–" + (c.bounds.max.y - chest + 1f).ToString("0.00") + " m)");
            }
            return spot;
        }

        // beside it already: eased onto the spot and turned (no steps: he shifts his feet), then the scene goes on
        private static void Shift(Player p)
        {
            p.SetMoveDir(Vector3.zero);
            float k = Mathf.Clamp01((Time.time - _shuffleAt) / _shuffleFor);
            Vector3 at = Vector3.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, k));
            at.y = p.transform.position.y; // (his height the game's: the ground under him)
            Rigidbody body = p.GetComponent<Rigidbody>();
            if (body != null) { body.MovePosition(at); body.velocity = new Vector3(0f, body.velocity.y, 0f); } else p.transform.position = at;
            Vector3 now = p.transform.forward; now.y = 0f;
            float speed = TurnSpeed * 1.5f;
            if (_softTurn) speed = FaceSpeed * _faceMul * Mathf.Clamp01((Time.time - _shuffleAt) / FaceEaseIn) * Mathf.Clamp(Vector3.Angle(now, _way) / FaceEaseEnd, 0.15f, 1f);
            Vector3 turned = Vector3.RotateTowards(now.normalized, _way, speed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            if (FLookYaw != null) { FLookYaw.SetValue(p, Quaternion.LookRotation(turned)); p.FaceLookDirection(); }
            float off = Vector3.Angle(now, _way);
            if ((k >= 1f && off <= Turned) || Time.time >= _until || p.IsDead())
            {
                _shuffle = false;
                Done(p);
                Vector3 d = _to - p.transform.position; d.y = 0f;
                Story.Log("player go: eased onto the spot (" + d.magnitude.ToString("0.00") + " m off, turned off by " + off.ToString("0") + " deg)");
            }
        }

        // right after the player's own keys were read (the patch below): this frame's keys are ours
        internal static void Steer(Player p)
        {
            if (!On || p == null || !ReferenceEquals(p, Player.m_localPlayer)) return;
            if (_shuffle) { Shift(p); return; }
            if (_heldPutAt >= 0f)
            {
                p.SetMoveDir(Vector3.zero);
                if (Time.time < _heldPutAt) return;
                _heldPutAt = -1f;
                Vector3 left = _to - p.transform.position; left.y = 0f;
                Put(p, _to, _way);
                Story.Log("player go: put on the spot under the black, " + left.magnitude.ToString("0.0") + " m off");
                StoryScreen.Fade(false, HeldBlack);
                Done(p);
                return;
            }
            if (_settleAt >= 0f)
            {
                p.SetMoveDir(Vector3.zero);
                if ((Time.time - _settleAt >= SettleLeast && !BodyTurn.LocalSettling) || Time.time - _settleAt >= SettleMost) { _settleAt = -1f; Done(p); }
                return;
            }
            if (Time.time < _goAt) { p.SetMoveDir(Vector3.zero); return; } // (its clip lets go of his feet first: LetFeetGo)
            Vector3 d = _to - p.transform.position; d.y = 0f;
            float far = d.magnitude;
            if (far > Near && Time.time >= _gainAt)
            {
                if (_gainFar - far < BlockedGain)
                {
                    Helds++;
                    Story.Log("player go: HELD, no way gained in " + BlockedFor.ToString("0.00") + " s, " + far.ToString("0.00") + " m short: " + (_contact ? "put there under a moment of black" : "stops here, the scene goes on"));
                    if (_contact) { StoryScreen.Fade(true, HeldBlack); _heldPutAt = Time.time + HeldBlack; return; }
                    Done(p);
                    return;
                }
                _gainAt = Time.time + BlockedFor; _gainFar = far;
            }
            Vector3 face = _back ? _way : far > Slow || (_soft && far > 0.01f) ? d / far : _way; // (back: facing as he was, the walk goes behind him) // (along the way, then turned as the spot wants; soft: no spot to turn to — on as he walks)
            Vector3 now = p.transform.forward; now.y = 0f;
            float toTurn = Vector3.Angle(now, face);
            float turnAt = _keep ? TurnSpeed : FaceSpeed * Mathf.Clamp01((Time.time - _goAt) / FaceEaseIn) * Mathf.Clamp(toTurn / FaceEaseEnd, 0.15f, 1f);
            Vector3 turned = Vector3.RotateTowards(now.normalized, face, turnAt * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            if (FLookYaw != null) { FLookYaw.SetValue(p, Quaternion.LookRotation(turned)); p.FaceLookDirection(); }
            float off = Vector3.Angle(now, _way);
            if ((_soft ? far <= SoftNear : far <= Near && off <= Turned) || Time.time >= _until || p.IsDead())
            {
                if (!p.IsDead()) { Settle(p, "player go: stands " + far.ToString("0.00") + " m from the spot, turned off by " + off.ToString("0") + " deg — his feet settle"); return; }
                Done(p);
                Story.Log("player go: stands " + far.ToString("0.00") + " m from the spot, turned off by " + off.ToString("0") + " deg" + (!_soft && (far > 0.1f || off > 10f) ? " — A CLIP THAT FOLLOWS WILL MISS" : ""));
                return;
            }
            float pace = Mathf.Lerp(SoftLeast, 1f, Mathf.SmoothStep(0f, 1f, far / SoftFrom));
            if (!_soft) pace = Mathf.Min(pace, Mathf.Clamp(far / Slow, Least, 1f)); // (an exact spot: right into it)
            if (!_keep && far > Slow)
            {
                pace *= Mathf.Clamp01((WalkFrom - toTurn) / (WalkFrom - WalkAll)) * Mathf.Clamp01((Time.time - _goAt) / PaceIn); // (he turns first, then goes)
                if (pace < 0.2f) { _gainAt = Time.time + BlockedFor; _gainFar = far; } // (not going yet: nothing to be held by)
            }
            if (_haltAt >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - _haltAt) / _haltFor);
                if (k >= 1f) { Done(p); Story.Log("player stop: stood still, " + far.ToString("0.00") + " m short of where he went"); return; }
                pace *= 1f - Mathf.SmoothStep(0f, 1f, k);
                _gainAt = Time.time + BlockedFor; _gainFar = far; // (slowing on purpose: not held)
            }
            p.SetMoveDir(far <= Near ? Vector3.zero : d / far * pace); // (slows into it: the game goes as far as this says)
            p.SetRun(_run && far > 2f);
            p.SetWalk(_slow || far <= 2f);
        }
    }

    // stepping back he keeps his face, as he does blocking (Player.AlwaysRotateCamera): the game plays its walk backwards
    [HarmonyPatch(typeof(Player), "AlwaysRotateCamera")]
    internal static class StoryWalkBackPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            try { if (StoryWalk.Backing && ReferenceEquals(__instance, Player.m_localPlayer)) __result = true; }
            catch (Exception e) { PatchGuard.Fail("StoryWalkBackPatch", e); }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    internal static class StoryWalkPatch
    {
        private static void Postfix(Player ___m_character)
        {
            try { if (StoryWalk.On) StoryWalk.Steer(___m_character); }
            catch (Exception e) { PatchGuard.Fail("StoryWalkPatch", e); }
        }
    }
}
#endif

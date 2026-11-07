#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), its bird: the steps, the orders here and for those near (RPC), a seat it is seen on.
    internal static partial class StoryScenes
    {
        // (played from a later step: the bird on its seat at once)
        private static Action<Player, Step> OnSeat(Action<Player, Step> begin) { return (p, st) => { _setup = true; try { begin(p, st); } finally { _setup = false; } }; }
        private static bool BirdSettling(Player p, Step st) { return _bird != null && !_bird.Settled; }

        private static StepKind BirdKind()
        {
            Action<Player, Step> come = BirdCome, land = BirdLand, look = BirdLook, leave = BirdLeave, gone = (p, st) => { Act("gone", ""); _bird = null; };
            return new StepKind(StepSeen.Shared) { Min = 1, VerbAt = 1, Usage = "bird come|land|look|say|circle|over|leave|gone|shoulder …", Verbs = new Dictionary<string, StepVerb>
            {
                { "come", new StepVerb(StepSeen.Shared) { PlaceAfter = "at", Begin = come, Setup = OnSeat(come), // (it is on its way with «nowait»: the next step may set it circling — gate_shut, the user 2026-11-04)
                    Busy = (p, st) => Array.IndexOf(st.W, "nowait") <= 0 && BirdSettling(p, st) } },
                { "land", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, Begin = land, Setup = OnSeat(land), Busy = BirdSettling } },
                { "shoulder", new StepVerb(StepSeen.Shared) { Begin = BirdShoulder, Busy = BirdSettling } },
                { "look", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, Begin = look, Setup = OnSeat(look) } },
                { "say", new StepVerb(StepSeen.Shared) { Min = 1,
                    Begin = (p, st) =>
                    {
                        string line = Grammar.Fix(Lang.T(Rest(st.Text, 2)), p);
                        _waitFor = Time.time + RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene);
                        Act("say", line);
                        Spoken("", line);
                    } } },
                { "circle", new StepVerb(StepSeen.Shared) { Min = 1,
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        float sec = F(w[2], 30f), r = w.Length > 3 ? F(w[3], 4.5f) : 4.5f, up = w.Length > 4 ? F(w[4], 3.5f) : 3.5f;
                        _waitFor = Time.time + sec;
                        Act("circle", F(r) + " " + F(up));
                    } } },
                { "over", new StepVerb(StepSeen.Shared) { // (till it is really over you, the step's own limit if it never comes)
                    Busy = (p, st) => _bird != null && Utils.DistanceXZ(_bird.transform.position, p.transform.position) > (st.W.Length > 2 ? F(st.W[2], 10f) : 10f) } },
                { "leave", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, PlaceNot = new[] { "up" }, Begin = leave, Setup = OnSeat(leave) } },
                { "gone", new StepVerb(StepSeen.Shared) { Begin = gone, Setup = OnSeat(gone) } },
            } };
        }

        // bird come [from=60] [up=18] land [metres=5] · … at <place> [side m] · … as hugin|munin
        private static void BirdCome(Player p, Step st)
        {
            string[] w = st.W;
            float from = w.Length > 2 ? F(w[2], 60f) : 60f, up = w.Length > 3 ? F(w[3], 18f) : 18f;
            int li = Array.IndexOf(w, "land");
            float before = li >= 0 && w.Length > li + 1 ? F(w[li + 1], 5f) : 5f;
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            Vector3 start = p.transform.position - fwd * from + Vector3.up * up;
            int ai = Array.IndexOf(w, "at");
            Vector3 seat;
            bool hover = false;
            if (ai >= 0 && w.Length > ai + 1) { int si = Array.IndexOf(w, "side"); if (!StoryWhere.Top(w[ai + 1], p.transform.position, si >= 0 && w.Length > si + 1 ? F(w[si + 1], 0f) : 0f, out seat)) { Note("bird come at: нема місця «" + w[ai + 1] + "» — крок пропущено"); return; } }
            else if (!Perch(p, fwd, before, out seat)) hover = true; // (nothing to sit on in sight — grass would hide it: in the air before you)
            if (hover) seat = p.transform.position + fwd * HoverAhead + Vector3.up * 2.2f;
            if (_setup) start = seat; // (played from a later step: on its seat already)
            float yaw = Quaternion.LookRotation(Flat(p.transform.position - seat), Vector3.up).eulerAngles.y;
            int asi = Array.IndexOf(w, "as"), own = asi >= 0 && w.Length > asi + 1 ? (w[asi + 1] == "munin" ? 1 : 0) : -1; // (as hugin|munin: your own raven's picture)
            int lki = Array.IndexOf(w, "look"); // (… look <id>: a look of assets/story/looks; none — the default one)
            Act("come", V(start) + " " + V(seat) + " " + F(yaw) + (hover ? " 1" : " 0") + " " + own + " " + (lki >= 0 && w.Length > lki + 1 ? w[lki + 1] : "-"));
            if (hover) Act("look", V(p.GetEyePoint()));
        }

        private static void BirdLand(Player p, Step st)
        {
            Vector3 at;
            if (!StoryWhere.Top(st.W[2], out at)) { Note("bird land: нема місця «" + st.W[2] + "» — крок пропущено"); return; }
            float yaw = Quaternion.LookRotation(Flat(p.transform.position - at), Vector3.up).eulerAngles.y;
            Act("land", V(at) + " " + F(yaw));
        }

        // onto your shoulder, its kind's (Shoulder.Bone): you stand still — the scene holds you
        private static void BirdShoulder(Player p, Step st)
        {
            string[] w = st.W;
            if (_bird == null) return;
            Transform bone = Shoulder.Bone(p, w.Length > 2 ? w[2] == "munin" : _bird.Munin);
            Vector3 at = bone != null ? bone.position : p.transform.position + Vector3.up * 1.5f;
            Act("land", V(at) + " " + F(p.transform.eulerAngles.y));
            if (_bird != null && bone != null) _bird.FollowSeat(bone, p.transform); // (he may move now: it stays on him)
        }

        // turns to you, or to a place (nothing there: where it looks now)
        private static void BirdLook(Player p, Step st)
        {
            if (_bird == null) return;
            Vector3 at;
            if (st.W[2] == "player") at = p.transform.position;
            else if (!StoryWhere.Find(st.W[2], out at)) at = _bird.transform.position + _bird.transform.forward;
            Act("look", V(at));
        }

        private static void BirdLeave(Player p, Step st)
        {
            Vector3 to;
            if (st.W[2] == "up" || !StoryWhere.Find(st.W[2], out to)) to = _bird != null ? _bird.transform.position + _bird.transform.forward * 10f : p.transform.position;
            Act("leave", V(to));
            _bird = null; // (on its own now: gone in the sky)
        }

        // the scene's bird gone with no puff, here and on those near (StoryOwn: it is the raven on your shoulder now)
        internal static void BirdVanish() { _birdGoWhen = null; if (_bird != null) { Act("vanish", ""); _bird = null; } }

        // the picture off when that holds (the real raven on the shoulder), at most HandOverMost s: no moment with neither
        internal static float HandOverMost = 3f;
        private static Func<bool> _birdGoWhen;
        private static float _birdGoBy;
        internal static void BirdVanishWhen(Func<bool> there) { _birdGoWhen = there; _birdGoBy = Time.time + HandOverMost; }
        private static void HandOver(bool now)
        {
            if (_birdGoWhen == null) return;
            if (now || Time.time >= _birdGoBy || _birdGoWhen()) BirdVanish();
        }

        // ---------------------------------------------------------------- the bird's orders: here and for those near
        private static void Act(string what, string args)
        {
            _bird = Apply(_bird, what, args, Player.m_localPlayer);
            if (what == "come") { _birdCome = args; _birdLast = ""; _birdLastArgs = ""; }
            else if (what == "land" || what == "circle") { _birdLast = what; _birdLastArgs = args; }
            SendAct(ZRoutedRpc.Everybody, what, args);
        }

        private static void SendAct(long to, string what, string args)
        {
            if (ZRoutedRpc.instance == null) return;
            Player p = Player.m_localPlayer;
            ZPackage pkg = new ZPackage();
            pkg.Write(p != null ? p.GetPlayerID() : 0L);
            pkg.Write(p != null ? p.transform.position : Vector3.zero);
            pkg.Write(what);
            pkg.Write(args);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, Rpc, pkg);
        }

        // one who came in the middle of the scene (StoryWatch): the bird from where it is now, to where it is going, doing what it does
        private static string _birdCome = "", _birdLast = "", _birdLastArgs = "";
        internal static void BirdSync(long to)
        {
            if (_bird == null || _birdCome.Length == 0) return;
            string[] a = _birdCome.Split(' ');
            if (a.Length < 7) return;
            SendAct(to, "come", V(_bird.transform.position) + " " + string.Join(" ", a, 3, a.Length - 3));
            if (_birdLast.Length > 0) SendAct(to, _birdLast, _birdLastArgs);
        }

        private static StoryBird Apply(StoryBird b, string what, string args, Player owner)
        {
            string[] a = args.Split(' ');
            switch (what)
            {
                case "come":
                    if (b != null) b.Gone();
                    return a.Length >= 7 ? StoryBird.Come(PV(a, 0), PV(a, 3), F(a[6], 0f), a.Length > 7 && a[7] == "1", a.Length > 8 ? (int)F(a[8], -1f) : -1, a.Length > 9 ? a[9] : "") : null;
                case "land": if (b != null && a.Length >= 4) b.Land(PV(a, 0), F(a[3], 0f)); return b;
                case "look": if (b != null && a.Length >= 3) b.Face(PV(a, 0)); return b;
                case "say": if (b != null) b.Say(args); return b;
                case "circle": if (b != null && a.Length >= 2) b.Circle(owner, F(a[0], 4.5f), F(a[1], 3.5f)); return b;
                case "leave": if (b != null && a.Length >= 3) b.Leave(PV(a, 0)); return null;
                case "gone": if (b != null) b.Gone(); return null;
                case "vanish": if (b != null) b.Vanish(); return null;
                case "free": if (b != null) b.Release(); return null;
            }
            return b;
        }

        internal static void RegisterRpcs() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnAct); }

        private static void OnAct(long sender, ZPackage pkg)
        {
            try
            {
                long who = pkg.ReadLong();
                Vector3 at = pkg.ReadVector3();
                string what = pkg.ReadString(), args = pkg.ReadString();
                Player me = Player.m_localPlayer;
                if (me == null || who == me.GetPlayerID()) return; // (our own: done already)
                StoryBird b;
                _mirrors.TryGetValue(who, out b);
                if (b == null && (what != "come" || Vector3.Distance(at, me.transform.position) > Seen)) return; // (too far to see it come)
                b = Apply(b, what, args, PerchView.PlayerOf(who));
                if (b != null) { b.Owner = who; _mirrors[who] = b; } else _mirrors.Remove(who);
            }
            catch (Exception e) { Story.Log("scene act: " + e.Message); }
        }

        // a seat it is seen on, before you (3–12 m, the way you look, ±25°): the top of something standing out of the
        // ground half a metre to three and a half (a rock, a stump, a log, a fence, a wall, a cart), level enough, sky
        // over it, in your sight; the nearest to «before» metres. On the bare ground the meadow's grass hides a raven.
        internal static float PerchMin = 0.45f, PerchMax = 3.5f, HoverAhead = 4.5f;
        private static bool Perch(Player p, Vector3 fwd, float before, out Vector3 seat)
        {
            seat = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            Vector3 eye = p.GetEyePoint(), right = Vector3.Cross(Vector3.up, fwd);
            float best = float.MaxValue;
            RaycastHit hit, block;
            for (float dist = 3f; dist <= 12f; dist += 1f)
                for (int s = -4; s <= 4; s++)
                {
                    Vector3 at = p.transform.position + fwd * dist + right * (s * dist * 0.11f);
                    if (!Physics.Raycast(at + Vector3.up * 8f, Vector3.down, out hit, 14f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) continue;
                    float raise = hit.point.y - ZoneSystem.instance.GetGroundHeight(hit.point);
                    if (raise < PerchMin || raise > PerchMax || Vector3.Angle(hit.normal, Vector3.up) > 35f) continue;
                    if (hit.collider.GetComponentInParent<Character>() != null) continue; // (not on a beast's back)
                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb != null && !rb.isKinematic) continue;                           // (nor on a thing that rolls)
                    if (Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 3f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) continue; // (a roof over it)
                    Vector3 top = hit.point + Vector3.up * 0.25f;
                    if (Physics.Linecast(eye, top, out block, RavenMotor.Mask, QueryTriggerInteraction.Ignore) && (block.point - top).magnitude > 0.5f) continue; // (out of sight)
                    float score = Mathf.Abs(dist - before) + Mathf.Abs(s) * 0.3f;
                    if (score < best) { best = score; seat = hit.point; }
                }
            return best < float.MaxValue;
        }
    }
}
#endif

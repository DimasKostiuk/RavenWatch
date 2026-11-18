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
        // what a bird step came to: where it is, how far from him, settled or not
        private static string BirdSaid(Player p, Step st) { return _bird == null ? "no bird" : StoryBird.StateName(_bird.State) + ", " + F(Utils.DistanceXZ(_bird.transform.position, p.transform.position)) + " m from him" + (_bird.Settled ? "" : " — NOT settled"); }

        private static StepKind BirdKind()
        {
            Action<Player, Step> come = BirdCome, land = BirdLand, look = BirdLook, leave = BirdLeave, gone = (p, st) => { Act("gone", ""); _bird = null; };
            return new StepKind(StepSeen.Shared) { Min = 1, VerbAt = 1, Usage = "bird come|land|look|say|circle|over|leave|gone|shoulder|wear …", Verbs = new Dictionary<string, StepVerb>
            {
                { "come", new StepVerb(StepSeen.Shared) { PlaceAfter = "at", Begin = come, Setup = OnSeat(come), // (it is on its way with «nowait»: the next step may set it circling — gate_shut, the user 2026-11-04)
                    Busy = (p, st) => Array.IndexOf(st.W, "nowait") <= 0 && BirdSettling(p, st), Done = BirdSaid } },
                { "land", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, Begin = land, Setup = OnSeat(land), Busy = BirdSettling, Done = BirdSaid } },
                { "shoulder", new StepVerb(StepSeen.Shared) { Begin = BirdShoulder, Busy = BirdSettling, Done = BirdSaid } },
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
                    Busy = (p, st) => _bird != null && Utils.DistanceXZ(_bird.transform.position, p.transform.position) > (st.W.Length > 2 ? F(st.W[2], 10f) : 10f), Done = BirdSaid } },
                { "leave", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, PlaceNot = new[] { "up" }, Begin = leave, Setup = OnSeat(leave) } },
                // bird home <place>: the place's own raven (LocRaven) back home — in through its roof where the place is drawn, else off its way
                { "home", new StepVerb(StepSeen.Shared) { Min = 1, PlaceAt = 2, Begin = BirdHome, Setup = OnSeat(BirdHome) } },
                { "gone", new StepVerb(StepSeen.Shared) { Begin = gone, Setup = OnSeat(gone) } },
                // bird wear <look> [seconds]: its colours go over into that look of assets/story/looks (played from a later step: at once)
                { "wear", new StepVerb(StepSeen.Shared) { Min = 1,
                    Begin = (p, st) => Act("wear", st.W[2] + " " + F(st.W.Length > 3 ? F(st.W[3], 3f) : 3f)),
                    Setup = (p, st) => Act("wear", st.W[2] + " 0") } },
            } };
        }

        // bird come [from=60] [up=18] land [metres=5] · … at <place> [side m] · … as hugin|munin · … hover (in the air before you, whatever
        // there is to sit on: the post's way) · … from <place> (the place's own raven, LocRaven: its very picture when you are within its
        // walls, else one of its look from the place's side while it goes out through its roof)
        private static void BirdCome(Player p, Step st)
        {
            string[] w = st.W;
            float from = w.Length > 2 ? F(w[2], 60f) : 60f, up = w.Length > 3 ? F(w[3], 18f) : 18f;
            int li = Array.IndexOf(w, "land");
            float before = li >= 0 && w.Length > li + 1 ? F(w[li + 1], 5f) : 5f;
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            Vector3 start = p.transform.position - fwd * from + Vector3.up * up;
            int fi = Array.IndexOf(w, "from");
            string home = fi >= 0 && w.Length > fi + 1 ? w[fi + 1] : "";
            if (home.Length > 0)
            {
                Vector3 there;
                if (!LocRaven.Has(home) || !StoryWhere.Find(home, out there)) { Note("bird come from: у «" + home + "» нема свого ворона (raven: у locations.list) — летить звідусіль"); home = ""; }
                else { Vector3 d = there - p.transform.position; d.y = 0f; start = d.magnitude > from ? p.transform.position + d.normalized * from + Vector3.up * up : there + Vector3.up * up; } // (from the place's side; nearer than that — over the place itself)
            }
            int ai = Array.IndexOf(w, "at");
            Vector3 seat;
            bool hover = Array.IndexOf(w, "hover") > 1;
            if (ai >= 0 && w.Length > ai + 1) { int si = Array.IndexOf(w, "side"); if (!StoryWhere.Top(w[ai + 1], p.transform.position, si >= 0 && w.Length > si + 1 ? F(w[si + 1], 0f) : 0f, out seat)) { Note("bird come at: нема місця «" + w[ai + 1] + "» — крок пропущено"); return; } }
            else if (hover) seat = Vector3.zero; // (told to hang in the air: its place just below)
            else if (!Perch(p, fwd, before, out seat)) hover = true; // (nothing to sit on in sight — grass would hide it: in the air before you)
            if (hover) seat = HoverBefore(p, fwd);
            if (_setup) start = seat; // (played from a later step: on its seat already)
            float yaw = Quaternion.LookRotation(Flat(p.transform.position - seat), Vector3.up).eulerAngles.y;
            int asi = Array.IndexOf(w, "as"), own = asi >= 0 && w.Length > asi + 1 ? (w[asi + 1] == "munin" ? 1 : 0) : -1; // (as hugin|munin: your own raven's picture)
            int lki = Array.IndexOf(w, "look"); // (… look <id>: a look of assets/story/looks; none — the default one)
            Act("come", V(start) + " " + V(seat) + " " + F(yaw) + (hover ? " 1" : " 0") + " " + own + " " + (lki >= 0 && w.Length > lki + 1 ? w[lki + 1] : home.Length > 0 && LocRaven.LookOf(home).Length > 0 ? LocRaven.LookOf(home) : "-") + " " + (home.Length > 0 ? home : "-"));
            _birdHome = home;
            if (LetterAhead(st)) Act("carry", RavenItem.LetterPrefab); // (the user 2026-11-18: the letter in its beak, as the post's)
            if (hover) Act("look", V(p.GetEyePoint()));
            if (hover && !_setup && Array.IndexOf(w, "nowait") < 0) _limit = Time.time + StoryBird.GiveUp + HoverPassRoom; // (its way, its give-up and the straight rest after it: StoryBird.Through)
        }

        // a «letter» step further on, before another bird comes or this one goes: this bird brings it — in its beak (StoryBird.Carry), off
        // as he takes it (the step «letter»: Act «drop»). Played from a step past the letter (_at, SetUp): given already, nothing in its beak
        private static bool LetterAhead(Step st)
        {
            int from = _now != null ? _now.Steps.IndexOf(st) : -1;
            if (from < 0) return false;
            for (int i = from + 1; i < _now.Steps.Count; i++)
            {
                string[] w = _now.Steps[i].W;
                if (w.Length == 0) continue;
                if (w[0] == "letter") return i > _at;
                if (w[0] == "bird" && w.Length > 1 && (w[1] == "come" || w[1] == "gone" || w[1] == "leave" || w[1] == "home")) return false;
            }
            return false;
        }
        internal static float HoverPassRoom = 20f;

        // the air before him where a bird hangs (bird come … hover; FightWait sends it back there): HoverAhead m ahead, over his head
        private static Vector3 HoverBefore(Player p, Vector3 fwd) { return p.transform.position + fwd * HoverAhead + Vector3.up * 2.2f; }

        // FightWait (StoryScenes): in a fight the scene's bird circles over him (its order to come back kept); calm again — back to it: the air
        // before him anew (where he stands now), or the seat or ring it was told last
        // A bird on its way through a roof or a straight pass (StoryBird.OnItsWay) is let get there first: a ring now would drop its way inside the
        // walls. The order it goes back to is kept apart (_birdBack): its last order is the ring, so one who comes in now sees it circle (BirdSync).
        // A seat it was to go back to that he has left over BackFar m behind: the air before him instead.
        internal static float BackFar = 10f;
        private static bool _birdRinged;
        private static string _birdBack = "", _birdBackArgs = "";
        private static void BirdWaits(bool fight, Player p)
        {
            if (fight)
            {
                if (_birdRinged || _bird == null || _bird.OnItsWay) return;
                _birdBack = _birdShoulder.Length > 0 ? "shoulder" : _birdLast; _birdBackArgs = _birdShoulder.Length > 0 ? _birdShoulder : _birdLastArgs; _birdRinged = true;
                Act("circle", F(FightRingR) + " " + F(FightRingUp));
                return;
            }
            if (!_birdRinged) return; // (it never left what it was told)
            _birdRinged = false;
            if (_bird == null) return;
            string[] c = _birdCome.Split(' ');
            string back = _birdBack, args = _birdBackArgs;
            if (back.Length == 0 && c.Length >= 7) { if (c.Length > 7 && c[7] == "1") back = "hover"; else { back = "land"; args = string.Join(" ", c, 3, 4); } } // (its seat and turn from «come»)
            if (back == "land" && Utils.DistanceXZ(PV(args.Split(' '), 0), p.transform.position) > BackFar) back = "hover"; // flat: a seat by him or left behind (scenes that roam are of the surface)
            if (back == "shoulder") ToShoulder(p, args == "munin");
            else if (back == "hover") { Act("hover", V(HoverBefore(p, Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward)))); Act("look", V(p.GetEyePoint())); }
            else if (back.Length > 0) Act(back, args);
        }

        // the place's raven home again (bird home <place>)
        private static void BirdHome(Player p, Step st)
        {
            Act("home", st.W[2]);
            _bird = null; _birdHome = ""; // (its place's now)
        }

        // the scene's bird is a place's raven (bird come … from <place>): the scene over or stopped before «bird home», it goes home all
        // the same — not a puff, not off into the sky (its place would stand empty till its «out» runs out)
        private static string _birdHome = "";
        private static bool BirdHomeAtEnd()
        {
            if (_bird == null || _birdHome.Length == 0) return false;
            string home = _birdHome;
            _birdHome = "";
            Act("home", home);
            return true;
        }

        // «come»'s args: …, its look (9), the place whose raven it is (10, «-» none)
        private static string HomeOf(string[] a) { return a.Length > 10 && a[10] != "-" ? a[10] : ""; }

        private static void BirdLand(Player p, Step st)
        {
            Vector3 at;
            if (!StoryWhere.Top(st.W[2], out at)) { Note("bird land: нема місця «" + st.W[2] + "» — крок пропущено"); return; }
            float yaw = Quaternion.LookRotation(Flat(p.transform.position - at), Vector3.up).eulerAngles.y;
            Act("land", V(at) + " " + F(yaw));
        }

        // onto your shoulder, its kind's: the very seat your own raven has there (Shoulder.SeatPoint — the bone alone is the collarbone's root, by the neck)
        private static void BirdShoulder(Player p, Step st)
        {
            string[] w = st.W;
            if (_bird == null) return;
            ToShoulder(p, w.Length > 2 ? w[2] == "munin" : _bird.Munin);
        }

        // onto his shoulder, following it: the step, and back from a fight's ring (the seat of then is in the air now)
        private static void ToShoulder(Player p, bool munin)
        {
            Transform bone = Shoulder.Bone(p, munin);
            Vector3 at = bone != null ? Shoulder.SeatPoint(p, munin, bone) : p.transform.position + Vector3.up * 1.5f;
            Act("land", V(at) + " " + F(p.transform.eulerAngles.y));
            _birdShoulder = munin ? "munin" : "hugin"; // (its «land» is at that point: back from a ring it is the shoulder again — BirdWaits; one who comes in sees the «land»)
            if (_bird != null && bone != null) _bird.FollowSeat(() => bone != null ? Shoulder.SeatPoint(p, munin, bone) : p.transform.position + Vector3.up * 1.5f, p.transform); // (he may move now: it stays on him; asked a frame while it sits there, a few seconds)
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
            if (what == "come") { _birdCome = args; _birdLast = ""; _birdLastArgs = ""; _birdRinged = false; _birdShoulder = ""; _birdCarry = ""; }
            else if (what == "carry") _birdCarry = args;
            else if (what == "drop" || what == "home" || what == "gone" || what == "vanish" || what == "leave") _birdCarry = "";
            else if (what == "land" || what == "circle" || what == "hover") { _birdLast = what; _birdLastArgs = args; _birdShoulder = ""; }
            else if (what == "wear") { string[] c = _birdCome.Split(' '); if (c.Length >= 10) { c[9] = args.Split(' ')[0]; _birdCome = string.Join(" ", c); } } // (one who comes in later sees it in its new colours)
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
        private static string _birdCome = "", _birdLast = "", _birdLastArgs = "", _birdShoulder = ""; // (_birdShoulder: «hugin»|«munin» — on his shoulder, following it: ToShoulder)
        private static string _birdCarry = ""; // (what is in its beak now: one who comes in sees it too)
        internal static void BirdSync(long to)
        {
            FalconSync(to); // (the falcons too: StoryScenesFalcon)
            if (_bird == null || _birdCome.Length == 0) return;
            string[] a = _birdCome.Split(' ');
            if (a.Length < 7) return;
            SendAct(to, "come", V(_bird.transform.position) + " " + string.Join(" ", a, 3, a.Length - 3));
            if (_birdCarry.Length > 0) SendAct(to, "carry", _birdCarry);
            if (_birdLast.Length > 0) SendAct(to, _birdLast, _birdLastArgs);
        }

        private static StoryBird Apply(StoryBird b, string what, string args, Player owner)
        {
            string[] a = args.Split(' ');
            switch (what)
            {
                case "come":
                    if (b != null) b.Gone();
                    if (a.Length < 7) return null;
                    {
                        string home = HomeOf(a);
                        StoryBird took = home.Length > 0 ? LocRaven.TakeOut(home, PV(a, 3)) : null; // (the place's raven itself where it is drawn here — through its roof first to one outside; else a bird of its look)
                        bool hover = a.Length > 7 && a[7] == "1";
                        if (took != null) { if (hover) took.HoverAt(PV(a, 3)); else took.Land(PV(a, 3), F(a[6], 0f)); return took; }
                    }
                    return StoryBird.Come(PV(a, 0), PV(a, 3), F(a[6], 0f), a.Length > 7 && a[7] == "1", a.Length > 8 ? (int)F(a[8], -1f) : -1, a.Length > 9 ? a[9] : "");
                case "carry": if (b != null) b.Carry(args); return b;
                case "drop": if (b != null) b.Drop(); return b;
                case "home": // (the place's raven back: its picture there takes this very bird, else it flies off towards the place)
                    if (b != null) b.Drop(); // (a letter not given — the scene cut off: not kept in the beak of the place's own raven)
                    if (b != null && !LocRaven.Back(a[0], b)) { Vector3 to; if (!StoryWhere.Find(a[0], out to)) to = b.transform.position + b.transform.forward * 10f; b.Leave(to); }
                    else if (b == null) LocRaven.Back(a[0], null);
                    return null;
                case "land": if (b != null && a.Length >= 4) b.Land(PV(a, 0), F(a[3], 0f)); return b;
                case "look": if (b != null && a.Length >= 3) b.Face(PV(a, 0)); return b;
                case "say": if (b != null) b.Say(args); return b;
                case "circle": if (b != null && a.Length >= 2) b.Circle(owner, F(a[0], 4.5f), F(a[1], 3.5f)); return b;
                case "hover": if (b != null && a.Length >= 3) b.HoverAt(PV(a, 0)); return b;
                case "leave": if (b != null && a.Length >= 3) b.Leave(PV(a, 0)); return null;
                case "gone": if (b != null) b.Gone(); return null;
                case "vanish": if (b != null) b.Vanish(); return null;
                case "free": if (b != null) b.Release(); return null;
                case "wear": if (b != null) { StoryLooks.Look l = StoryLooks.Get(a[0]); if (l == null) Story.Log("bird wear: no look «" + a[0] + "»"); else b.Wear(l, a.Length > 1 ? F(a[1], 0f) : 0f); } return b;
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
                if (what.StartsWith("n:")) { PersonMirror(what.Substring(2), args); return; } // (a person's act of a scene drawn here too: NpcSparks)
                if (what.StartsWith("f:")) { FalconMirror(who, at, what.Substring(2), args, me); return; } // (a sister's falcon: StoryScenesFalcon)
                StoryBird b;
                _mirrors.TryGetValue(who, out b);
                if (b == null && what == "come" && Vector3.Distance(at, me.transform.position) > Seen) { string home = HomeOf(args.Split(' ')); if (home.Length > 0) LocRaven.Away(home); return; } // (too far to see it come; its place's raven out through its roof, if drawn here)
                if (b == null && what == "home") { LocRaven.Back(args, null); return; } // (its place's raven: «in», where its mind is)
                if (b == null && what != "come") return;
                b = Apply(b, what, args, PerchView.PlayerOf(who));
                if (b != null) { b.Owner = who; _mirrors[who] = b; } else _mirrors.Remove(who);
            }
            catch (Exception e) { Story.Log("scene act: " + e.Message); }
        }

        // the author's own bird, with no scene (raven story bird <look>|circle|land|leave|gone): a bird of that look flies in from
        // behind you and sits before you as «bird come» does, then takes orders — to see a look in flight, sitting and landing.
        // On this game only; it stays till told to go.
        internal static string TryBird(Player p, string word)
        {
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            Vector3 seat;
            bool hover = !Perch(p, fwd, 5f, out seat);
            if (hover) seat = HoverBefore(p, fwd);
            float yaw = Quaternion.LookRotation(Flat(p.transform.position - seat), Vector3.up).eulerAngles.y;
            int n = 0;
            switch (word)
            {
                case "circle":
                    foreach (StoryBird b in StoryBird.Live.ToArray()) if (b != null) { b.Circle(p, 5f, 3.5f); n++; }
                    return "Кружляють над тобою: " + n;
                case "land":
                    if (hover) return "Перед тобою нема на що сісти (камінь, пень, колода, паркан): стань лицем до такого.";
                    foreach (StoryBird b in StoryBird.Live.ToArray()) if (b != null) { b.Land(seat, yaw); n++; }
                    return "Сідають перед тобою: " + n;
                case "leave":
                    foreach (StoryBird b in StoryBird.Live.ToArray()) if (b != null) { b.Leave(b.transform.position + (b.transform.position - p.transform.position)); n++; }
                    return "Відлітають: " + n;
                case "gone":
                    foreach (StoryBird b in StoryBird.Live.ToArray()) if (b != null) { b.Gone(); n++; }
                    return "Зникли: " + n;
            }
            if (StoryLooks.Get(word) == null) return "raven story bird <вигляд>|circle|land|leave|gone — нема вигляду «" + word + "» в assets/story/looks";
            StoryBird nb = StoryBird.Come(p.transform.position - fwd * 40f + Vector3.up * 15f, seat, yaw, hover, -1, word);
            if (nb == null) return "Птаха не зроблено (лог: «bird:»).";
            return "Летить «" + word + "»" + (hover ? " (сісти нема на що: зависне перед тобою)" : "") + ". Далі: raven story bird circle | land | leave | gone; стан — raven story bird";
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

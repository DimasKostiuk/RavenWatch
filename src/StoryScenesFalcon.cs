#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), the sisters' falcons (Falcon): several at once, each by its name; the orders here and for
    // those near (the bird's RPC, its acts marked «f:»: every game near flies a picture of its own, as with the bird).
    //   falcon <name> come [from=90] [up=35] land [<place>] [dive] [nowait]   from so many m behind you, so high, down to a place
    //                                       (none: FalconAhead m before you) — down a glide path, or «dive»: a stoop; waits till it stands
    //   falcon <name> come [from] [up] circle    … round over you instead (the scene goes on: it never lands)
    //   falcon <name> land [<place>] [dive]      down to a place from the air (waits till it stands)
    //   falcon <name> circle                     round over you (Falcon.CircleRadius, CircleUp)
    //   falcon <name> ready                      made now, unseen, high over you — under a black screen: its «skim» then makes nothing in that frame (no freeze)
    //   falcon <name> skim <from> <to> [run <m>=20] [up <m>]   the low run (Falcon.Skim): made «up» m over «from», straight to «to», down and then the last
    //                                            «run» m low over the ground at one pace, never braking. The scene goes on at once. Over «to» it does what
    //                                            the next word told it — «become <who> stride [go <place>]»: gone behind its feathers at that speed, the
    //                                            person walking on to the place without a stop; nothing told: it lands there
    //                                            «falcon.<name>» finds it till it is gone, and then the person it turned into (a shot goes on with her)
    //   falcon <name> become <who> [stride] [as <template>] [nowait]
    //                                            it turns into a person, nothing see-through (the user 2026-11-14). On its feet — «the cloak of wings» (Falcon.Wrap):
    //                                            it draws its wings round itself, rises to a person's height and is gone behind a burst of its own feathers; the
    //                                            person stands there whole. «stride» — «the step out of flight» (Falcon.Stride): still flaring down, it is gone
    //                                            behind its feathers and the person walks on (the next step: npc <who> go …). «as»: two people of one template
    //                                            in a scene, each by its own name (become sestra_3 as sestra_2). The step waits till the person is there.
    //                                            Before 2026-11-14: wings up and a beat, the burst
    //                                            (Falcon.Burst — the game's feathers, a warm haze, a light that fades), it shrinks away; the person
    //                                            (its template id) made there for the scene as «npc <who> spawn», coming in as the falcon goes
    //                                            (Made, Falcon.ChangeIn; gone with the scene)
    //   falcon <name> from <who>                 a person of the scene turns into a falcon where it stands: the person fades at once, the
    //                                            falcon stands there in the burst; then «falcon <name> leave»
    //   falcon <name> from <who> fly             … out of her run: gone behind her feathers, the falcon in the air at once and off the way she ran
    //                                            (it never stands: its standing shape is not one to show)
    //   falcon <name> leave [<place>|up]         takes off and goes that way (does not wait)
    //   falcon <name> gone                       a burst and gone
    //   falcon.<name>                            a place word: where it is now (camera on falcon.a wide)
    // At the scene's end the falcons still here fly off; a scene stopped: they are gone in a burst. It does not sit as a bird sits:
    // on the ground it turns into a person (the user 2026-11-13).
    internal static partial class StoryScenes
    {
        internal static float FalconFrom = 90f, FalconUp = 35f, FalconAhead = 9f, FalconTall = 1.8f;
        private static readonly Dictionary<string, Falcon> _falcons = new Dictionary<string, Falcon>();          // this scene's, by name
        private static readonly Dictionary<string, Falcon> _falconMirrors = new Dictionary<string, Falcon>();   // others' scenes': «<player id> <name>»

        private static readonly Dictionary<string, string> _became = new Dictionary<string, string>();          // this scene's: the falcon's name → the person it turned into

        private static Falcon FalconOf(string name) { Falcon f; return _falcons.TryGetValue(name, out f) && f != null ? f : null; }
        // where it is; turned into a person — where that one is, its middle (a shot that went with the falcon goes on with her: the change is never cut away from)
        internal static float FalconPersonUp = 1.1f;
        internal static bool FalconAt(string name, out Vector3 at)
        {
            Falcon f = FalconOf(name);
            if (f != null) { at = f.Middle; return true; }
            string who; NpcActor a;
            if (_became.TryGetValue(name, out who) && _cast.TryGetValue(who, out a) && a != null) { at = a.transform.position + Vector3.up * FalconPersonUp; return true; }
            at = Vector3.zero;
            return false;
        }
        private static string FalconSaid(Player p, Step st) { Falcon f = FalconOf(st.W[1]); return f == null ? "no falcon «" + st.W[1] + "»" : f.Describe() + (f.OnFeet ? "" : " — NOT on its feet"); }
        private static bool FalconAir(Player p, Step st) { Falcon f = FalconOf(st.W[1]); return f != null && !f.OnFeet; }
        private static bool FalconThere(string verb, string name) { if (FalconOf(name) != null) return true; Note("falcon " + verb + ": нема сокола «" + name + "» (спершу falcon " + name + " come) — крок пропущено"); return false; }

        private static StepKind FalconKind()
        {
            Action<Player, Step> come = FalconCome, land = FalconLand, circle = (p, st) => { if (FalconThere("circle", st.W[1])) FAct("circle", st.W[1]); }, leave = FalconLeave,
                gone = (p, st) => { if (FalconThere("gone", st.W[1])) FAct("gone", st.W[1]); _falcons.Remove(st.W[1]); },
                quiet = (p, st) => { if (FalconOf(st.W[1]) != null) FAct("quiet", st.W[1]); _falcons.Remove(st.W[1]); }; // (played from a later step: gone already, no burst out of nowhere)
            return new StepKind(StepSeen.Shared) { Min = 2, VerbAt = 2, Usage = "falcon <ім'я> come|land|circle|become|from|leave|gone …", Verbs = new Dictionary<string, StepVerb>
            {
                { "come", new StepVerb(StepSeen.Shared) { PlaceAfter = "land", PlaceNot = new[] { "dive", "nowait", "circle" }, Begin = come, Setup = OnSeat(come),
                    Busy = (p, st) => Array.IndexOf(st.W, "nowait") < 0 && Array.IndexOf(st.W, "circle") < 0 && FalconAir(p, st), Done = FalconSaid } },
                { "land", new StepVerb(StepSeen.Shared) { PlaceAt = 3, PlaceNot = new[] { "dive" }, Begin = land, Setup = OnSeat(land), Busy = FalconAir, Done = FalconSaid } },
                { "ready", new StepVerb(StepSeen.Shared) { Usage = "falcon <ім'я> ready   (зроблений заздалегідь і невидимий: його політ почнеться без завмирання кадру)",
                    Begin = (p, st) => { if (FalconOf(st.W[1]) == null) FAct("ready", st.W[1] + " " + V(p.transform.position + Vector3.up * FalconReadyUp)); } } },
                { "skim", new StepVerb(StepSeen.Shared) { Min = 2, PlaceAt = 3, Usage = "falcon <ім'я> skim <звідки> <куди> [run <м>] [up <м>]   (низький проліт: не чекає; над місцем — falcon <ім'я> become <хто> stride)",
                    Check = st => st.W.Length > 4 ? StoryWhere.Why(st.W[4]) : "falcon <ім'я> skim <звідки> <куди>: нема, куди",
                    Begin = FalconSkim, Setup = OnSeat(FalconSkim) } },
                { "circle", new StepVerb(StepSeen.Shared) { Begin = circle, Setup = OnSeat(circle) } },
                { "become", new StepVerb(StepSeen.Shared) { Min = 1, Begin = FalconBecome, Setup = OnSeat(FalconBecome),
                    Busy = (p, st) => st.W.Length > 3 && _changing.Contains(st.W[3]) && Array.IndexOf(st.W, "nowait") < 0 } }, // (till the person stands there: the next step speaks to it)
                { "from", new StepVerb(StepSeen.Shared) { Min = 1, Begin = FalconFromPerson, Setup = OnSeat(FalconFromPerson) } },
                { "leave", new StepVerb(StepSeen.Shared) { PlaceAt = 3, PlaceNot = new[] { "up" }, Begin = leave, Setup = quiet } },
                { "gone", new StepVerb(StepSeen.Shared) { Begin = gone, Setup = quiet } },
            } };
        }

        // where it comes down: a place (its footing: a rock, a floor, else the ground), none — before you
        private static bool FalconGround(Player p, Step st, int at, out Vector3 ground)
        {
            string[] w = st.W;
            string place = at > 0 && w.Length > at && w[at] != "dive" && w[at] != "nowait" && w[at] != "circle" ? w[at] : null;
            Vector3 spot = Vector3.zero;
            if (place != null && !StoryWhere.Find(place, out spot)) { ground = Vector3.zero; Note("falcon: нема місця «" + place + "» — крок пропущено"); return false; }
            if (place == null) spot = p.transform.position + Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward) * FalconAhead;
            ground = Apart(p, st.W[1], Falcon.Footing(spot));
            return true;
        }

        // the sisters land side by side, never one into another: a spot within FalconApart m of another falcon's landing (or of a person
        // the scene made, a sister already risen) moves FalconApart m aside — right, left, twice as far … — across your look at it
        internal static float FalconApart = 3f;
        private static Vector3 Apart(Player p, string name, Vector3 ground)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Flat(ground - p.transform.position)), at = ground;
            for (int k = 0; k < 9; k++)
            {
                if (k > 0) at = Falcon.Footing(ground + right * (FalconApart * ((k + 1) / 2) * (k % 2 == 1 ? 1f : -1f)));
                bool free = true;
                foreach (KeyValuePair<string, Falcon> kv in _falcons)
                {
                    if (kv.Key == name || kv.Value == null) continue;
                    string way = kv.Value.Way;
                    if ((way == "land" || way == "dive" || way == "stand") && Utils.DistanceXZ(kv.Value.Target, at) < FalconApart) { free = false; break; }
                }
                if (free) foreach (NpcActor a in _made) if (a != null && Utils.DistanceXZ(a.transform.position, at) < FalconApart) { free = false; break; }
                if (free) { if (k > 0) Track("falcon " + name + ": місце зайняте — сідає на " + F(FalconApart * ((k + 1) / 2)) + " м " + (k % 2 == 1 ? "праворуч" : "ліворуч")); return at; }
            }
            return ground; // (all taken: as told)
        }

        // falcon <name> come [from] [up] land [<place>] [dive] | circle
        private static void FalconCome(Player p, Step st)
        {
            string[] w = st.W;
            float from = w.Length > 3 ? F(w[3], FalconFrom) : FalconFrom, up = w.Length > 4 ? F(w[4], FalconUp) : FalconUp;
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            Vector3 start = p.transform.position - fwd * from;
            start.y = Falcon.Footing(start).y + up;
            bool circling = Array.IndexOf(w, "circle") > 0;
            Vector3 ground = p.transform.position;
            int li = Array.IndexOf(w, "land");
            if (!circling && !FalconGround(p, st, li > 0 ? li + 1 : 0, out ground)) return;
            string mode = circling ? "circle" : Array.IndexOf(w, "dive") > 0 ? "dive" : "land";
            if (_setup && !circling) start = ground; // (played from a later step: on its feet there already)
            FAct("come", w[1] + " " + V(start) + " " + V(ground) + " " + mode + " " + (_setup ? "1" : "0"));
            if (FalconOf(w[1]) == null) { Note("falcon come: «" + w[1] + "» не зроблено: " + Falcon.Problem + " — крок пропущено"); return; }
            _limit = Time.time + from / Falcon.Approach + 25f; // (its flight in, the circle round to the place, the braking: and some)
        }

        // falcon <name> land [<place>] [dive]
        private static void FalconLand(Player p, Step st)
        {
            Vector3 ground;
            if (!FalconThere("land", st.W[1])) return;
            if (!FalconGround(p, st, 3, out ground)) return;
            FAct(_setup ? "stand" : "land", st.W[1] + " " + V(ground) + " " + (Array.IndexOf(st.W, "dive") > 0 ? "dive" : "land"));
            _limit = Time.time + 40f;
        }

        // falcon <name> skim <from> <to> [run <m>] [up <m>]: made high over «from», it flies straight to «to» — down, then the last «run» m low over
        // the ground — and the scene goes on (it never waits: what it does over its place is the next word's, «become <who> stride»)
        internal static float SkimRun = 20f, FalconReadyUp = 120f; // m of the low run when none is said · m over the player a falcon made beforehand waits, unseen
        private static void FalconSkim(Player p, Step st)
        {
            string[] w = st.W;
            Vector3 from, to;
            if (!StoryWhere.Find(w[3], out from) || !StoryWhere.Find(w[4], out to)) { Note("falcon skim: нема місця «" + w[3] + "» чи «" + w[4] + "» — крок пропущено"); return; }
            int ri = Array.IndexOf(w, "run"), ui = Array.IndexOf(w, "up");
            float run = ri > 4 && w.Length > ri + 1 ? F(w[ri + 1], SkimRun) : SkimRun, up = ui > 4 && w.Length > ui + 1 ? F(w[ui + 1], FalconUp) : FalconUp;
            Vector3 ground = Falcon.Footing(to), start = from;
            start.y = Falcon.Footing(from).y + up;
            FAct("skim", w[1] + " " + V(start) + " " + V(ground) + " " + F(run) + " " + (_setup ? "1" : "0"));
            if (FalconOf(w[1]) == null) Note("falcon skim: «" + w[1] + "» не зроблено: " + Falcon.Problem + " — крок пропущено");
        }

        // falcon <name> leave [<place>|up]: away from you, or toward a place
        private static void FalconLeave(Player p, Step st)
        {
            Falcon f = FalconOf(st.W[1]);
            if (!FalconThere("leave", st.W[1])) return;
            Vector3 to;
            if (st.W.Length <= 3 || st.W[3] == "up" || !StoryWhere.Find(st.W[3], out to)) to = f.Middle + (f.Middle - p.transform.position);
            FAct("leave", st.W[1] + " " + V(to)); // (kept the scene's till its end: the camera follows it off — «нема falcon.a», 2026-11-13)
        }

        // falcon <name> become <who>: the falcon changes where it stands (Falcon.Change), the person made there (Made: networked), coming in as it goes
        private static void FalconBecome(Player p, Step st)
        {
            Falcon f = FalconOf(st.W[1]);
            if (f == null) { Note("falcon become: нема сокола «" + st.W[1] + "» — крок пропущено"); return; }
            string who = st.W[3], template = who;
            int asAt = Array.IndexOf(st.W, "as");
            if (asAt > 3 && st.W.Length > asAt + 1) template = st.W[asAt + 1];
            bool stride = Array.IndexOf(st.W, "stride") > 3;
            int goAt = Array.IndexOf(st.W, "go");
            string goTo = stride && goAt > 3 && st.W.Length > goAt + 1 ? st.W[goAt + 1] : null; // («… stride go <place>»: she walks on there out of the feathers, never standing first)
            Vector3 at = f.Feet;
            _became[st.W[1]] = who;
            if (_setup)
            {
                Vector3 there;
                if (goTo != null && StoryWhere.Find(goTo, out there)) at = Falcon.Footing(there); // (played from a later step: she stands where she walked to)
                FAct("quiet", st.W[1] + " " + V(at)); _falcons.Remove(st.W[1]); Made(p, who, at, -1f, template, true, float.NaN); return;
            }
            if (!stride && !f.OnFeet) Note("falcon become: «" + st.W[1] + "» ще не на землі (" + f.Describe() + ") — стає людиною там, де під ним земля; з польоту: слово stride");
            SceneDef scene = _now;
            _changing.Add(who);
            if (stride) f.ThenStride = (feet, yaw) => { _changing.Remove(who); if (_now == scene) { Made(p, who, feet, -1f, template, true, yaw); WalkOn(who, goTo, yaw); } };
            else f.Then = () => { _changing.Remove(who); if (_now == scene) Made(p, who, at, -1f, template, true, float.NaN); };
            FAct(stride ? "stride" : "become", st.W[1] + " " + V(at));
            if (!stride) _falcons.Remove(st.W[1]); // (out of flight it is still the scene's, and «falcon.<name>» still finds it, till the moment it is gone)
        }

        // out of the feathers she walks on at once (the user 2026-11-14: «без зупинки починають йти кроком»): the order, and her body already at a walk
        // the way she flew — a body made standing would take its first step from rest
        internal static float StrideWalk = 1.4f; // m/s her body has the moment she is made
        private static void WalkOn(string who, string goTo, float yaw)
        {
            NpcActor a; Vector3 to;
            if (goTo == null || !_cast.TryGetValue(who, out a) || a == null) return;
            if (!StoryWhere.Find(goTo, out to)) { Note("falcon become … go: нема місця «" + goTo + "» — вона стоїть, де стала"); return; }
            a.Order("go " + V(to) + " near 0.3", NpcMind.Layer.Scene);
            Rigidbody body = a.GetComponent<Rigidbody>();
            if (body != null) body.velocity = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * StrideWalk;
        }

        // falcon <name> from <who>: the person fades at once, the falcon stands there in a burst
        internal static float FromFlyUp = 1.3f, FromFlySpeed = 5f; // m over her feet the falcon is made, flying · m/s its feathers are thrown on with
        internal static float FalconFromFade = 0.05f; // (at once, behind the feathers: nothing see-through — the user 2026-11-14)
        private static readonly HashSet<string> _changing = new HashSet<string>(); // session: the people a falcon is turning into this moment (the step waits)
        private static void FalconFromPerson(Player p, Step st)
        {
            NpcActor a = Actor(st.W[3], p.transform.position);
            if (a == null) { Note("falcon from: нема людини «" + st.W[3] + "» — крок пропущено"); return; }
            Vector3 at = a.transform.position;
            NpcFade.Out(a, FalconFromFade, "-");
            _became.Remove(st.W[1]); // (a falcon again: «falcon.<name>» is the bird)
            bool fly = Array.IndexOf(st.W, "fly") > 3; // («from <who> fly»: out of her run, in the air at once and away — it never stands)
            FAct("from", st.W[1] + " " + V(at) + " " + F(a.transform.eulerAngles.y) + " " + (_setup ? "1" : "0") + (fly ? " fly" : ""));
        }

        // ---------------------------------------------------------------- the falcons' orders: here and for those near
        private static void FAct(string what, string args)
        {
            string name = args.Split(' ')[0];
            Falcon f = FalconApply(FalconOf(name), what, args, Player.m_localPlayer);
            if (f != null) _falcons[name] = f; else _falcons.Remove(name);
            SendAct(ZRoutedRpc.Everybody, "f:" + what, args);
        }

        // what it is after an order (null: gone, or on its own now)
        private static Falcon FalconApply(Falcon f, string what, string args, Player owner)
        {
            string[] a = args.Split(' ');
            switch (what)
            {
                case "come":
                    {
                        if (f != null) f.Vanish(false);
                        if (a.Length < 9) return null;
                        Vector3 start = PV(a, 1), ground = PV(a, 4);
                        bool now = a.Length > 8 && a[8] == "1";
                        Vector3 way = Flat(ground - start);
                        f = Falcon.Make(start, Quaternion.LookRotation(way).eulerAngles.y);
                        if (f == null) return null;
                        f.Scene = true;
                        if (a[7] == "circle") { f.Launch(); f.Circle(owner != null ? owner.transform : null, owner != null ? owner.transform.position : ground); }
                        else if (now) f.StandAt(ground, YawTo(ground, owner));
                        else { f.Launch(); if (a[7] == "dive") f.Dive(ground); else f.Land(ground); }
                        return f;
                    }
                case "ready":
                    {
                        if (f != null || a.Length < 4) return f;
                        f = Falcon.Make(PV(a, 1), 0f);
                        if (f == null) return null;
                        f.Scene = true; f.Hide();
                        return f;
                    }
                case "skim":
                    {
                        if (a.Length < 9) return null;
                        Vector3 start = PV(a, 1), ground = PV(a, 4);
                        float yaw = Quaternion.LookRotation(Flat(ground - start)).eulerAngles.y;
                        if (f != null && f.Hidden) f.Unhide(start, yaw); // (made beforehand: nothing is made in this frame)
                        else { if (f != null) f.Vanish(false); f = Falcon.Make(start, yaw); }
                        if (f == null) return null;
                        f.Scene = true;
                        if (a[8] == "1") f.StandAt(ground, YawTo(ground, owner)); // (played from a later step: there already)
                        else f.Skim(ground, F(a[7], SkimRun));
                        return f;
                    }
                case "land": if (f != null && a.Length >= 5) { if (a[4] == "dive") f.Dive(PV(a, 1)); else f.Land(PV(a, 1)); } return f;
                case "stand": if (f != null && a.Length >= 4) f.StandAt(PV(a, 1), YawTo(PV(a, 1), owner)); return f;
                case "circle": if (f != null) f.Circle(owner != null ? owner.transform : null, owner != null ? owner.transform.position : f.Middle); return f;
                case "leave": if (f != null && a.Length >= 4) f.Leave(PV(a, 1) - f.Middle); return f; // (on its way off: gone by itself far away)
                case "gone": if (f != null) f.Vanish(true); return null;
                case "quiet": if (f != null) f.Vanish(false); return null;
                case "stride": if (f != null) f.Stride(); return f; // (gone by itself the moment its feet come down, or over its place on the low run: the scene's till then)
                case "become": if (f != null && a.Length >= 4) f.Wrap(PV(a, 1)); else if (f != null) f.Vanish(true); return null; // (the cloak of wings; before: it beats its wings, bursts and shrinks away by itself)
                case "from":
                    {
                        if (f != null) f.Vanish(false);
                        if (a.Length < 5) return null;
                        Vector3 at = PV(a, 1);
                        bool fly = a.Length > 6 && a[6] == "fly";
                        f = Falcon.Make(at + Vector3.up * (fly ? FromFlyUp : 0.5f), F(a[4], 0f));
                        if (f == null) return null;
                        f.Scene = true;
                        if (fly)
                        {
                            // (the user 2026-11-14: «розвертаються і починають бігти і відразу перетворюються на соколів» — its standing pose is not fit to be seen:
                            //  out of her feathers it is in the air already, at her pace and more, climbing away the way she ran)
                            Vector3 way = Quaternion.Euler(0f, F(a[4], 0f), 0f) * Vector3.forward;
                            if (!(a.Length > 5 && a[5] == "1")) Falcon.FeatherBurst(at, FalconTall, 1f, true, way * FromFlySpeed);
                            f.Launch(); f.Leave(way);
                            return f;
                        }
                        f.StandAt(at, F(a[4], 0f));
                        if (!(a.Length > 5 && a[5] == "1")) { Falcon.FeatherBurst(at, FalconTall, 1f, true); f.Unwrap(); } // (out of the feathers: wrapped and as tall as she was, it opens its wings)
                        return f;
                    }
            }
            return f;
        }

        private static float YawTo(Vector3 from, Player owner) { return owner != null ? Quaternion.LookRotation(Flat(owner.transform.position - from)).eulerAngles.y : 0f; }

        // an order from another's scene: its falcon by its name (one too far to see it come is never made)
        private static void FalconMirror(long who, Vector3 at, string what, string args, Player me)
        {
            string name = args.Split(' ')[0], key = who + " " + name;
            Falcon f;
            _falconMirrors.TryGetValue(key, out f);
            if (f == null && what != "come" && what != "from" && what != "skim" && what != "ready") return; // (the orders that make one)
            if (f == null && Vector3.Distance(at, me.transform.position) > Seen) return;
            f = FalconApply(f, what, args, PerchView.PlayerOf(who));
            if (f != null) { f.Owner = who; _falconMirrors[key] = f; } else _falconMirrors.Remove(key);
        }

        // one who came in the middle of the scene (StoryWatch): each falcon from where it is now, to where it goes, as it goes
        // (Falcon.Way, Target — not its last order: a falcon gliding down lands where it was going, one taking off to circle circles)
        private static void FalconSync(long to)
        {
            foreach (KeyValuePair<string, Falcon> kv in _falcons)
            {
                Falcon f = kv.Value;
                if (f == null) continue;
                string way = f.Way;
                if (way == "leave" || f.Hidden) continue; // (on its own already: not the scene's; made beforehand and not flying yet: its «skim» will say)
                if (way == "stand") SendAct(to, "f:come", kv.Key + " " + V(f.Target) + " " + V(f.Target) + " land 1");
                else SendAct(to, "f:come", kv.Key + " " + V(f.Middle) + " " + V(way == "circle" ? f.Middle : f.Target) + " " + way + " 0");
            }
        }

        // the scene over: the falcons still here fly off (played to its end) or are gone in a burst (stopped)
        private static void FalconsEnd(bool stopped)
        {
            Player p = Player.m_localPlayer;
            foreach (string name in new List<string>(_falcons.Keys))
            {
                Falcon f = FalconOf(name);
                if (f == null || (!stopped && f.Way == "leave")) continue; // (flying off already)
                _changing.Clear();
                if (stopped || p == null) FAct("gone", name);
                else FAct("leave", name + " " + V(f.Middle + (f.Middle - p.transform.position)));
            }
            _falcons.Clear(); _became.Clear();
        }
    }
}
#endif

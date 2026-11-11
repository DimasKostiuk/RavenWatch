#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Where a person goes of its own accord, and why (the user 2026-11-04: «розумно давала точки місця інтересу»). The one
    // place that answers «where now?»: nobody else picks a spot of a base for its ease (the whims' rest and fire, the
    // evening's seat, the shelter from the rain ask here).
    //   A place   a kind (below: what it wants of a person, how long it stays, what it is said to be doing), where it stands
    //             and where it looks. From its base's things (NpcBase.Things), or the author's spots of a person (Authored)
    //   The choice, once in a while, one that is free at its ease (its day's idle, nothing on a higher layer):
    //     what the kind is worth to it now (cold, tired, lazy, diligent, chatty, curious, brave, its nature, the hour; its evening, Late: the fire, a seat, the table, its people — and it stays longer)
    //     + its own liking of that very place (from its seed: the same person likes the same bench after a restart)
    //     + how often it has come here (Favourite: grows with its visits, kept in its locker, a few places: its own bench by now;
    //       one of its people in it now: now and then a quarrel — Taken, NpcAttitude.Quarrel)
    //     + new here since it last looked (curious ones go and see)
    //     − how far, how crowded, how lately it was there and how lately at a place of the kind, + a little chance
    //   The visit  it stops and looks where it means to go, walks, does the thing facing it (sits, opens the chest, its clip),
    //             stays as long as the kind says, from the moment it is there. All through NpcMind.TakeWhim as a whim of its
    //             pleasure: a duty, an order, a fight, a scene take it off as they take off any whim
    //   raven npc why <who>   every place it could go to now, with the sum and its parts; each choice is in the log too.
    // Not a plan kept in its ZDO: a whim ends at a reload, and its next look picks again. Where it has been lately is a session's
    // (it may well go to the same bench again after a restart), so is what is new and where a fight was.
    // Runs where it is owned (NpcActor.Ticks): a look every LookEvery s a person, a choice every GapMin…GapMax s after the last visit, in
    // its turn (NpcClock). A choice in two parts, so that it costs what it may: all the places are weighed by what is already known (the
    // base's lists, Most a kind; where the people stand, once) with no physics and no way finder; only the best few are then looked at
    // closely (Ready: a player's house, a fire's spots, the floor), at most Tries. Whether there is a way: the walk's own (NpcWalk.CantReach:
    // the whim is let go, and that place left alone BadFor s), no path asked here as well.
    internal static class NpcPlaces
    {
        internal static float LookEvery = 0.5f, GapMin = 4f, GapMax = 14f, FirstMin = 2f, FirstMax = 12f, MullMin = 0.7f, MullMax = 1.5f;
        internal static float WayCost = 50f, SameFade = 300f, SameCost = 1.2f, KindFade = 150f, KindCost = 0.5f, Liking = 0.2f, Chance = 0.3f, FreshCost = 1f;
        internal static readonly List<Func<NpcActor, NpcActor, float>> PersonWorth = new List<Func<NpcActor, NpcActor, float>>(); // a module's worth of going up to that person (NpcModules)
        internal static float RefusedFor = 300f; // s a place that did not hold when looked at closely is left alone
        internal static float FavEach = 0.08f, FavMost = 0.6f, YardFire = 35f, WatchFor = 90f; // worth a visit, at most · m off its heart a fire in the open yard still counts · s a thing just built draws the curious
        private const int FavKeep = 8, FavSay = 5; // places it keeps count of · visits from which it says so
        internal static float FriendWorth = 0.6f; // what a dear neighbour adds to going up to it (a cold one takes as much away)
        internal static float NearThing = 1.2f, FreshWay = 0.4f, FreshFor = 1200f, ScarFor = 1200f, BadFor = 60f, CrowdLike = 0.3f;
        private const int Most = 24, Tries = 3;

        internal sealed class Need { public float Cold, Tired, Lazy, Diligent, Chatty, Curious, Brave; public bool Old, Night, Late; public NpcNature Nature; }

        internal sealed class Kind
        {
            public string Id, Going, There; // its name; what the person is said to be doing on its way and there
            public int Hash;                    // (of its name: part of a place's key)
            public float Min = 15f, Max = 30f;  // s it stays there
            public float LateStay = 1f;         // × that in its evening
            public bool NoFav;                  // not a place to grow fond of (a neighbour, a stroll: not where they stand)
            public float Reach = 2.5f;          // m round it: whoever is there is there too
            public int Cap = 3;                 // how many at once
            public Func<Need, float> Wants = n => 0.2f;
            public string Clip = "", From = "", Out = ""; // what it does there the whole stay (its way in, the loop, its way out): a place of its base
            public string Now = "", NowSit = "";         // …or now and then a short one, standing or seated («a|b»: one of them)
            public float NowMin = 8f, NowMax = 15f;       // s between those
        }

        // A kind: its worth to a person now. Words go through Lang.K: the table sees them, the status translates them.
        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>();
        private static void K(Kind k) { k.Hash = k.Id.GetStableHashCode(); Kinds[k.Id] = k; }
        static NpcPlaces()
        {
            K(new Kind { Id = "fire", Going = Lang.K("іде до вогню"), There = Lang.K("гріється біля вогню"), Min = 60f, Max = 150f, Cap = 4, LateStay = 2f, Wants = n => 0.35f + n.Cold * 1.2f + n.Lazy * 0.3f + (n.Late ? 0.9f : 0f), Now = "fire_warm|fire_warm|drink", NowSit = "sit_drink|fire_nod_sit_l|fire_nod_sit_r|fire_laugh_sit_l|fire_laugh_sit_r" });
            K(new Kind { Id = "seat", Going = Lang.K("іде сісти"), There = Lang.K("перепочиває"), Min = 45f, Max = 120f, Reach = 0.9f, Cap = 1, LateStay = 2f, Wants = n => 0.25f + n.Tired * 1.2f + n.Lazy * 0.5f + (n.Old ? 0.3f : 0f) + (n.Late ? 0.35f : 0f), NowSit = "fire_nod_sit_l|fire_nod_sit_r|fire_laugh_sit_l|fire_laugh_sit_r", NowMin = 15f, NowMax = 35f });
            K(new Kind { Id = "heart", Now = "fire_nod_l|fire_nod_r", Going = Lang.K("іде до Серця оселі"), There = Lang.K("стоїть біля Серця оселі"), Min = 15f, Max = 30f, Reach = 4f, Cap = 3, Wants = n => 0.3f + n.Diligent * 0.2f - (n.Late ? 0.35f : 0f) });
            K(new Kind { Id = "stores", Going = Lang.K("іде глянути запаси"), There = Lang.K("перевіряє запаси"), Min = 12f, Max = 25f, Reach = 1.6f, Cap = 1, Wants = n => 0.15f + n.Diligent * 0.6f - (n.Late ? 0.5f : 0f) });
            K(new Kind { Id = "craft", Going = Lang.K("іде глянути, що там"), There = Lang.K("оглядає господарство"), Min = 15f, Max = 35f, Reach = 2f, Cap = 2, Wants = n => 0.15f + n.Diligent * 0.5f + n.Curious * 0.2f - (n.Late ? 0.5f : 0f), Clip = "heid_search.loop", From = "heid_search.bends", Out = "heid_search.rises" });
            K(new Kind { Id = "trophy", Going = Lang.K("іде глянути на трофей"), There = Lang.K("розглядає трофей"), Min = 12f, Max = 25f, Reach = 2f, Cap = 2, Wants = n => 0.1f + n.Curious * 0.7f - (n.Late ? 0.2f : 0f), Clip = "look_examine.loop", From = "look_examine.lifts", Out = "look_examine.lowers" });
            K(new Kind { Id = "edge", Going = Lang.K("іде до краю бази"), There = Lang.K("дивиться за межі бази"), Min = 15f, Max = 35f, Reach = 4f, Cap = 2, Wants = n => 0.15f + n.Curious * 0.4f + n.Brave * 0.4f - (n.Night ? 0.4f : 0f), Clip = "arms_cross.loop", From = "arms_cross.folds", Out = "arms_cross.unfolds" });
            K(new Kind { Id = "person", Now = "talk_hands|talk_hands|arms_open|fire_nod_l|fire_nod_r|fire_laugh_l|fire_laugh_r|shrug_small|point_at", NowMin = 6f, NowMax = 12f, Going = Lang.K("іде до сусіда"), There = Lang.K("стоїть із сусідом"), Min = 20f, Max = 45f, Reach = 1f, Cap = 1, NoFav = true, Wants = n => 0.1f + n.Chatty * 0.9f + (n.Late ? 0.2f : 0f) });
            K(new Kind { Id = "scar", Going = Lang.K("іде глянути на місце бою"), There = Lang.K("оглядає місце бою"), Min = 15f, Max = 30f, Reach = 3f, Cap = 2, NoFav = true, Wants = n => 0.3f + n.Curious * 0.6f + n.Brave * 0.3f, Clip = "look_search.around" });
            K(new Kind { Id = "roam", Going = Lang.K("походжає"), There = Lang.K("походжає"), Min = 6f, Max = 14f, Reach = 2f, Cap = 2, NoFav = true, Wants = n => 0.35f - (n.Late ? 0.35f : 0f) });
            K(new Kind { Id = "table", NowSit = "sit_drink|sit_drink|sit_tap|fire_nod_sit_l|fire_nod_sit_r|fire_laugh_sit_l|fire_laugh_sit_r", Going = Lang.K("іде до столу"), There = Lang.K("сидить за столом"), Min = 60f, Max = 140f, LateStay = 2f, Reach = 1.5f, Cap = 8, Wants = n => (n.Late ? 0.9f : 0.05f) + n.Chatty * 0.5f }); // (the table the player picked for meals: its evening with the others)
            K(new Kind { Id = "ravens", Now = "fire_nod_l|fire_laugh_r|point_at", Going = Lang.K("іде глянути на воронів"), There = Lang.K("дивиться на воронів"), Min = 15f, Max = 35f, Reach = 2.5f, Cap = 2, Wants = n => 0.1f + n.Curious * 0.6f - (n.Late ? 0.2f : 0f) }); // (a perch with a raven on it)
            K(new Kind { Id = "watch", Now = "point_at|scratch_head|fire_nod_r", Going = Lang.K("іде глянути, що будують"), There = Lang.K("дивиться, що будують"), Min = 10f, Max = 25f, Reach = 3f, Cap = 3, NoFav = true, Wants = n => 0.15f + n.Curious * 1.1f }); // (a thing a player has just built)
            K(new Kind { Id = "think", Now = "scratch_head|shrug_small", NowMin = 12f, NowMax = 25f, Going = Lang.K("повільно йде"), There = Lang.K("задумався|задумалась"), Min = 30f, Max = 70f, Reach = 1.5f, Cap = 1, Wants = n => 0.4f }); // (the author's: a spot a person stands at in thought)
            K(new Kind { Id = "tend", Clip = "heid_search.loop", From = "heid_search.bends", Out = "heid_search.rises", Going = Lang.K("іде до справ"), There = Lang.K("порається"), Min = 30f, Max = 80f, Reach = 1.5f, Cap = 1, Wants = n => 0.3f + n.Diligent * 0.5f }); // (the author's: its own small work at a spot)
            K(new Kind { Id = "fetch", Going = Lang.K("іде по своє"), There = Lang.K("бере своє"), Min = 3f, Max = 4f, Reach = 1f, Cap = 1, NoFav = true, Wants = n => 0f }); // (the author's: a thing of its own set down somewhere — back to it (StoryLife, its Extra says how much)
        }

        internal sealed class Place
        {
            public Kind K;
            public Vector3 At, Look;
            public Component Thing;     // the chest it opens, the seat…
            public bool Sit, NoLook, Guest;
            public int Key, ThingKey; // (which place it is: by the thing, not the side it is walked up to; 0: no thing)
            public float Worth, Extra, Min, Max; // (Min/Max 0: the kind's)
            public string Clip = "", From = "", Out = ""; // the author's: its clip there, its way in and out
            public bool Exact;          // it stands on the very spot (a clip whose hands work on the thing)
            public Func<Vector3> Stand; // the author's: where it stands exactly, reckoned only if this place is the one (Ready)
            public Action<NpcActor> There; // the author's: done once as it arrives (a thing of its own taken up: StoryLife)
            public string Parts = "";   // (raven npc why)
        }

        // a visit with a clip of its own is about to begin it (the author's: what is in the person's hands goes down first — StoryProps);
        // returns the seconds to wait before the visit's own clip, 0 none
        internal static Func<NpcActor, float> ClipBegins;

        // the author's places of a person (StoryLife): null — none: its base's own
        internal static Func<NpcActor, List<Place>> Authored;

        private sealed class State
        {
            public float Look, Next, Stay, ClipAsked, NextNow;
            public NpcMind.Plan Plan; public Place Visit; public bool There; public int Stage;
            public string ClipOn = "", Last = "", Last2 = ""; // (Last2: the place last refused and why)
            public bool Hooked; public float Wait;                                            // (the visit's clip: ClipBegins asked once, and the wait it said)
            public Dictionary<int, int> Fav;                                                // its locker's count of its visits, read once
            public readonly Dictionary<int, float> Seen = new Dictionary<int, float>();    // session: a place → when it chose it
            public readonly Dictionary<string, float> KindSeen = new Dictionary<string, float>(); // session: a kind → when
            public readonly Dictionary<int, float> Bad = new Dictionary<int, float>();     // session: no way there, till then
        }
        private static readonly Dictionary<NpcActor, State> States = new Dictionary<NpcActor, State>(); // session: off when it goes (Forget)
        private static readonly int PerfChoice = NpcPerf.Id("places (вибір місця)");

        internal static void Forget(NpcActor a) { States.Remove(a); }

        // the last of its own places it came to (a favour: «show me»; any game: its locker, read when asked — never by its mind). False: none yet, or it lives elsewhere now
        internal static readonly int KFavAt = "RavenWatch_npcFavAt".GetStableHashCode(); // string «x,y,z» — in its locker
        internal static bool Favourite(NpcActor a, out Vector3 at)
        {
            at = Vector3.zero;
            string[] w = (NpcLocker.Get(a, KFavAt) ?? "").Split(',');
            float x, y, z;
            if (w.Length < 3 || !float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            at = new Vector3(x, y, z);
            return Utils.DistanceXZ(at, NpcRules.Home(a)) <= NpcBase.Reach * 2f;
        }

        // the kind of the place it is at or going to now ("" none): how it stands there is NpcStance's
        internal static string VisitKind(NpcActor a) { State s; return States.TryGetValue(a, out s) && s.Plan != null && s.Visit != null ? s.Visit.K.Id : ""; }

        // ---------------------------------------------------------------- its look
        internal static void Tick(NpcActor a)
        {
            State s;
            if (!States.TryGetValue(a, out s)) States[a] = s = new State { Next = Time.time + UnityEngine.Random.Range(FirstMin, FirstMax) };
            if (Time.time < s.Look) return;
            s.Look = Time.time + LookEvery;
            NpcMind m = a.Mind;
            if (m == null) return;
            if (s.Plan != null) { Visiting(a, m, s); return; }
            if (s.Next > Time.time + GapMax && NpcClock.Seen(a)) s.Next = Time.time + UnityEngine.Random.Range(0f, GapMax); // (it waited long with nobody near; you came: its day goes on)
            if (Time.time < s.Next || !AtEase(a, m)) return;
            if (NpcLife.RainHolds(a)) { s.Next = Time.time + GapMax; return; } // (in the rain its day is NpcLife's: no walk out first and back, 2026-11-04)
            if (!NpcClock.Take()) return; // (no room this frame: its next look)
            long perf = NpcPerf.Now();
            Choose(a, m, s);
            NpcPerf.Add(PerfChoice, perf);
        }

        // nothing over its own day and nothing to do in it now: not a seat of the evening, not its bed, not a whim, not a talk
        private static bool AtEase(NpcActor a, NpcMind m)
        {
            return NpcResident.Is(a) && m.NowLayer <= NpcMind.Layer.Routine && m.Now == NpcMind.Task.Idle && !m.SeatedNow && !m.Asleep && m.Of(NpcMind.Layer.Whim) == null;
        }

        // ---------------------------------------------------------------- the visit
        private static void Visiting(NpcActor a, NpcMind m, State s)
        {
            NpcMind.Plan p = m.Of(NpcMind.Layer.Whim);
            if (p != s.Plan) // (over, or something it must do took its place)
            {
                if (!s.There && p == null) s.Bad[s.Visit.Key] = Time.time + BadFor; // (it never got there: no way, NpcWalk: not that place again for a while)
                ClipOff(a, s);
                s.Plan = null; s.Visit = null; s.There = false; s.Stage = 0; s.Hooked = false;
                s.Next = Time.time + UnityEngine.Random.Range(GapMin, GapMax) * NpcClock.Slow(a);
                return;
            }
            if (m.NowLayer > NpcMind.Layer.Whim) { ClipOff(a, s); s.Stage = 0; return; } // (an order, a fight, a scene over it: its clip is theirs now; it starts anew when it is back)
            bool there = p.Kind == NpcMind.Task.Idle || p.Kind == NpcMind.Task.Pose || p.Seated;
            if (!there) return;
            if (!s.There)
            {
                s.There = true;
                p.Word = s.Visit.K.There;
                p.Until = Time.time + s.Stay; // (its stay counts from here, however long the way was)
                s.NextNow = Time.time + UnityEngine.Random.Range(2f, s.Visit.K.NowMin);
                Arrive(a, m, s);
                if (s.Visit.There != null) s.Visit.There(a);
            }
            Clips(a, p, s);
            Now(a, m, p, s);
        }

        private static void Arrive(NpcActor a, NpcMind m, State s)
        {
            if (Visited(a, s)) return; // (its own place by now: it says so)
            Container chest = s.Visit.Thing as Container;
            if (chest != null && UnityEngine.Random.value < 0.6f) m.Open(chest, UnityEngine.Random.Range(2.5f, 4.5f));
            if (UnityEngine.Random.value >= 0.15f + NpcPersona.Of(a).Chatty * 0.25f) return;
            string line = Line(s.Visit.K.Id);
            if (line != null) NpcPhrases.SayOr(a, "place_" + s.Visit.K.Id, a.Form(line), null, NpcActor.Voice.Unasked);
        }

        // how often it has come to each place: in its locker «key:n;…» (written as it arrives, the few it comes to most)
        internal static readonly int KFav = "RavenWatch_npcFavPlaces".GetStableHashCode();
        private static Dictionary<int, int> FavOf(NpcActor a, State s)
        {
            if (s.Fav != null) return s.Fav;
            string kept = NpcLocker.Get(a, KFav);
            if (kept == null) return null; // (its locker not here yet: asked again)
            s.Fav = new Dictionary<int, int>();
            foreach (string e in kept.Split(';')) { int c = e.IndexOf(':'), k, n; if (c > 0 && int.TryParse(e.Substring(0, c), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) s.Fav[k] = n; }
            return s.Fav;
        }

        // one more visit; true: it is its place by now, and it said so
        private static bool Visited(NpcActor a, State s)
        {
            if (s.Visit.K.NoFav) return false;
            Dictionary<int, int> f = FavOf(a, s);
            if (f == null) return false;
            int n; f.TryGetValue(s.Visit.Key, out n); f[s.Visit.Key] = ++n;
            while (f.Count > FavKeep) { int low = int.MaxValue, drop = 0; foreach (KeyValuePair<int, int> kv in f) if (kv.Key != s.Visit.Key && kv.Value < low) { low = kv.Value; drop = kv.Key; } f.Remove(drop); }
            StringBuilder t = new StringBuilder();
            foreach (KeyValuePair<int, int> kv in f) t.Append(kv.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            NpcLocker.Set(a, KFav, t.ToString());
            if (n >= FavSay) NpcLocker.Set(a, KFavAt, s.Visit.At.x.ToString("0.#", CultureInfo.InvariantCulture) + "," + s.Visit.At.y.ToString("0.#", CultureInfo.InvariantCulture) + "," + s.Visit.At.z.ToString("0.#", CultureInfo.InvariantCulture)); // (written with its count, on a visit)
            if (n < FavSay || UnityEngine.Random.value > 0.25f) return false;
            NpcPhrases.SayOr(a, "place_fav", a.Form(Lang.T("Люблю тут бувати.")), null, NpcActor.Voice.Unasked);
            return true;
        }

        private static string Line(string kind)
        {
            switch (kind)
            {
                case "stores": return Lang.T("Глянемо, що там лишилось.");
                case "trophy": return Lang.T("Оце так звір був…");
                case "scar": return Lang.T("Гаряче тут було.");
                case "edge": return Lang.T("Тихо поки що.");
                case "ravens": return Lang.T("Розумні птахи. Дивляться, ніби все знають.");
                case "watch": return Lang.T("Що це ви тут будуєте?");
                default: return null;
            }
        }

        // the author's clip there: its way in, the loop, its way out in time for the end of its stay
        private static void Clips(NpcActor a, NpcMind.Plan p, State s)
        {
            Place v = s.Visit;
            if (v.Clip.Length == 0 || a.Body == null || (s.Stage == 0 && !NpcClock.Seen(a))) return; // (not begun with nobody to see it; you come while it is there: then)
            PAnimClip way = v.Out.Length > 0 ? BodyClips.Find(v.Out) : null;
            float outFor = way != null ? way.Length : 0f;
            if (s.Stage < 3 && way != null && Time.time >= p.Until - outFor - LookEvery) { Ask(a, s, v.Out, 0f, 3); return; }
            if (s.Stage == 0)
            {
                if (!s.Hooked) { s.Hooked = true; s.Wait = Time.time + (ClipBegins != null ? ClipBegins(a) : 0f); } // (once a visit: what its hands hold goes down first)
                if (Time.time < s.Wait) return;
                PAnimClip from = v.From.Length > 0 ? BodyClips.Find(v.From) : null;
                if (from != null) { Ask(a, s, v.From, 0f, 1); return; }
                Ask(a, s, v.Clip, s.Stay + 5f, 2);
                return;
            }
            if (s.Stage == 1)
            {
                PAnimClip from = BodyClips.Find(v.From);
                if (from == null || Time.time - s.ClipAsked >= from.Length) Ask(a, s, v.Clip, s.Stay + 5f, 2);
            }
        }

        private static void Ask(NpcActor a, State s, string clip, float seconds, int stage)
        {
            a.Body.Clip(clip, seconds);
            s.ClipOn = clip; s.ClipAsked = Time.time; s.Stage = stage;
        }

        internal static float NatureMoves = 0.4f; // a share of the short moves that are its nature's own, not the place's
        // now and then a short one of its kind's or its nature's (a place with a clip of its own: that one only), not over another clip, not in its last seconds
        private static void Now(NpcActor a, NpcMind m, NpcMind.Plan p, State s)
        {
            Kind k = s.Visit.K;
            if (s.Visit.Clip.Length > 0 || a.Body == null || Time.time < s.NextNow) return;
            s.NextNow = Time.time + UnityEngine.Random.Range(k.NowMin, k.NowMax);
            if (!NpcClock.Seen(a)) return; // (nobody to see it)
            NpcNature nature = NpcNature.Of(a);
            string own = nature != null && !m.SeatedNow ? nature.Moves : ""; // (its nature's own, standing: NpcNature.Moves)
            string[] pick = (own.Length > 0 && UnityEngine.Random.value < NatureMoves ? own : m.SeatedNow ? k.NowSit : k.Now).Split('|');
            string clip = pick[UnityEngine.Random.Range(0, pick.Length)];
            if (clip.Length == 0 || !a.Body.ClipFree || p.Until - Time.time < 4f) return;
            if (a.Body.Clip(clip)) s.ClipOn = clip;
        }

        // off, if the clip on its body is still the one this visit asked for (a scene's own stays)
        private static void ClipOff(NpcActor a, State s)
        {
            if (s.ClipOn.Length == 0) return;
            string on = s.ClipOn; s.ClipOn = "";
            BodyAnimator b = BodyAnimator.Of(a.GetComponent<Character>());
            if (a.Body != null && b != null && b.ClipIn(NpcBody.ClipSlot) == on) a.Body.Clip("");
        }

        // ---------------------------------------------------------------- the choice
        private static void Choose(NpcActor a, NpcMind m, State s)
        {
            s.Next = Time.time + UnityEngine.Random.Range(GapMin, GapMax) * NpcClock.Slow(a); // (whatever comes of it: not asked again at once. Nobody near: fewer walks, each one a way asked)
            List<Place> all = Candidates(a);
            if (all == null) { s.Next = Time.time + 5f; return; } // (its base still being looked over)
            Need n = NeedOf(a);
            Others(a);
            foreach (Place p in all) Weigh(a, n, s, p, false);
            all.Sort(ByWorth);
            Place best = First(a, s, all);
            if (best != null) Go(a, m, s, best, n);
        }
        private static readonly Comparison<Place> ByWorth = (x, y) => y.Worth.CompareTo(x.Worth);

        // the best one that holds when looked at closely; those that do not: left alone a while
        private static Place First(NpcActor a, State s, List<Place> sorted)
        {
            int tried = 0;
            foreach (Place p in sorted)
            {
                if (p.Worth <= float.MinValue || tried++ >= Tries) return null;
                _refused = "";
                if (Ready(a, p)) return p;
                s.Bad[p.Key] = Time.time + RefusedFor; // (it holds no better soon: its tries left to the next ones — a wall torch took all three each time, and the real fire was never reached, 2026-11-05)
                s.Last2 = p.K.Id + " " + Mathf.RoundToInt(p.At.x) + "," + Mathf.RoundToInt(p.At.z) + " (" + (p.Thing != null ? Utils.GetPrefabName(p.Thing.gameObject) : "-") + "): " + _refused;
            }
            return null;
        }

        // the dear part, for the few it may go to: a player's house (physics), a fire's spots round it (physics, the way finder; kept a while
        // per fire), where the author's spot stands, the walkable floor
        private static string _refused = "";
        private static bool Ready(NpcActor a, Place p)
        {
            NpcResident r = a.Resident;
            NpcBase b = r != null ? r.Base : null;
            Vector3 thing = p.Thing != null ? p.Thing.transform.position : p.At;
            if (p.Stand == null && !p.Guest && b != null && b.Ready && NpcPrivate.Off(a, b, thing)) { _refused = "чужий дім"; return false; }
            Fireplace f = p.Thing as Fireplace;
            if (f != null)
            {
                if (b != null && b.Ready && !b.Holds(thing) && !NpcKit.UnderSky(thing)) { _refused = "поза базою й під чужим дахом"; return false; } // (past its base and under another's roof: not its fire)
                Vector3? side = NpcKit.FireSide(f, a.transform.position);
                if (side == null) { _refused = "нема де стати поруч"; return false; }
                p.At = side.Value;
            }
            if (p.Stand != null) p.At = p.Stand();
            if (!p.Sit && !p.Exact) p.At = Settled(p.At);
            return true;
        }

        private static bool Go(NpcActor a, NpcMind m, State s, Place p, Need n)
        {
            float min = p.Min > 0f ? p.Min : p.K.Min, max = p.Max > 0f ? p.Max : p.K.Max;
            float stay = UnityEngine.Random.Range(min, max) * (1f + (n.Lazy - 0.5f) * 0.6f) * (n.Late ? p.K.LateStay : 1f);
            float way = Utils.DistanceXZ(a.transform.position, p.At) / 1.2f;
            NpcMind.Plan plan = Plan(p);
            plan.SetOff = Time.time + UnityEngine.Random.Range(MullMin, MullMax) * (1f + n.Lazy * 0.5f);
            plan.AimAt = p.Look;
            if (!m.TakeWhim(plan, way + stay + 5f, NpcMind.Leisure)) return false;
            s.Plan = plan; s.There = false; s.Stage = 0; s.Stay = stay;
            s.Visit = Copy(p);
            Went(s, p);
            s.Last = p.K.Id + " " + p.Worth.ToString("0.00", CultureInfo.InvariantCulture);
            Npcs.Log(a.Who + ": goes to «" + p.K.Id + "» at " + Mathf.RoundToInt(p.At.x) + "," + Mathf.RoundToInt(p.At.z) + " (" + p.Worth.ToString("0.00", CultureInfo.InvariantCulture) + ", stays " + Mathf.RoundToInt(stay) + " s)");
            return true;
        }

        // it chose it: remembered; what it no longer weighs (long past, its pause over) let go, so these stay small
        private static readonly List<int> Old = new List<int>();
        private static void Went(State s, Place p)
        {
            s.Seen[p.Key] = Time.time; s.KindSeen[p.K.Id] = Time.time;
            if (s.Seen.Count + s.Bad.Count < 40) return;
            Old.Clear();
            foreach (KeyValuePair<int, float> kv in s.Seen) if (Time.time - kv.Value > SameFade * 3f) Old.Add(kv.Key);
            foreach (int k in Old) s.Seen.Remove(k);
            Old.Clear();
            foreach (KeyValuePair<int, float> kv in s.Bad) if (Time.time > kv.Value) Old.Add(kv.Key);
            foreach (int k in Old) s.Bad.Remove(k);
        }

        // a place's plan: its walk there and what it does (the mind's own: go, sit). To look at a thing (a chest, a station) it comes within
        // NearThing (+ the walk's slack): its way often ends a step short of its side (a table, a raised floor by it), and it pushed there till
        // it was put home in a puff before your eyes (2026-11-05)
        private static NpcMind.Plan Plan(Place p)
        {
            return new NpcMind.Plan { Kind = p.Sit ? NpcMind.Task.Sit : NpcMind.Task.Go, To = p.At, Near = p.Exact ? 0.3f : p.Thing != null ? NearThing : 0.6f, Exact = p.Exact, Looks = !p.NoLook, Look = p.Look, Word = p.K.Going, Guest = p.Guest };
        }

        // (the list's places are used again at the next choice: what it goes to is its own copy)
        private static Place Copy(Place p)
        {
            return new Place { K = p.K, At = p.At, Look = p.Look, Thing = p.Thing, Sit = p.Sit, NoLook = p.NoLook, Guest = p.Guest, Key = p.Key, ThingKey = p.ThingKey, Worth = p.Worth, Clip = p.Clip, From = p.From, Out = p.Out, Exact = p.Exact, There = p.There };
        }

        private static Need NeedOf(NpcActor a)
        {
            NpcPersona w = NpcPersona.Of(a);
            NpcSettlers.Look look = NpcSettlers.Of(a);
            Need n = new Need { Tired = NpcToil.Tired(a) / 100f, Lazy = w.Laziness, Diligent = w.Diligence, Chatty = w.Chatty, Curious = w.Curiosity, Brave = w.Bravery, Old = look != null && look.Age == 2, Nature = w.Nature };
            NpcResident r = a.Resident;
            n.Late = r != null && r.PartNow != NpcResident.Part.Day;
            if (EnvMan.instance == null) return n;
            n.Night = EnvMan.IsNight();
            n.Cold = Cold();
            return n;
        }

        // how cold it is out now, 0 … 1 (the weather and the hour: NpcGait reads it too — a cold one hunches)
        internal static float Cold()
        {
            if (EnvMan.instance == null) return 0f;
            return Mathf.Clamp01((EnvMan.IsFreezing() ? 1f : EnvMan.IsCold() ? 0.7f : 0f) + (EnvMan.IsNight() ? 0.3f : 0f) + (EnvMan.IsWet() ? 0.2f : 0f));
        }

        // what a place is worth to it now (float.MinValue: not for it). explain: its parts, for «raven npc why»
        private static void Weigh(NpcActor a, Need n, State s, Place p, bool explain)
        {
            Vector3 me = a.transform.position;
            float bad;
            int crowd = Crowd(p);
            if (crowd >= p.K.Cap && !explain) Taken(a, s, p); // (its own place, someone in it: it may hold that against one of its people)
            if (crowd >= p.K.Cap || (s.Bad.TryGetValue(p.Key, out bad) && Time.time < bad)) { p.Worth = float.MinValue; p.Parts = crowd >= p.K.Cap ? "зайнято" : "нема шляху"; return; }
            float want = p.K.Wants(n) + p.Extra + NpcNature.Place(n.Nature, p.K.Id); // (its nature: NpcNature)
            float like = (((NpcKit.Seed(a.Zdo) ^ (uint)p.Key) * 2654435761u >> 16 & 0xFFFF) / 65535f * 2f - 1f) * Liking; // (its own liking of this place: the same every run)
            float fresh = IsFresh(p.ThingKey) ? FreshCost * n.Curious : 0f;
            float way = -Utils.DistanceXZ(me, p.At) / WayCost * (1f + n.Lazy) * (fresh > 0f ? FreshWay : 1f); // (a new thing is worth the walk: the far side of the base too, 2026-11-04)
            float here = crowd > 0 ? (n.Chatty - 0.5f) * CrowdLike * crowd : 0f;
            float t;
            float same = s.Seen.TryGetValue(p.Key, out t) ? -SameCost * Mathf.Exp((t - Time.time) / SameFade) : 0f;
            float kind = s.KindSeen.TryGetValue(p.K.Id, out t) ? -KindCost * Mathf.Exp((t - Time.time) / KindFade) : 0f;
            float luck = UnityEngine.Random.Range(0f, Chance), fav = 0f;
            if (!p.K.NoFav) { Dictionary<int, int> f = FavOf(a, s); int visits; if (f != null && f.TryGetValue(p.Key, out visits)) fav = Mathf.Min(FavMost, visits * FavEach); }
            p.Worth = want + like + fresh + way + here + same + kind + luck + fav;
            if (!explain) return;
            CultureInfo ci = CultureInfo.InvariantCulture;
            p.Parts = "хоче " + want.ToString("0.00", ci) + ", любить " + like.ToString("0.00", ci) + (fav != 0f ? ", звичне " + fav.ToString("0.00", ci) : "") + (fresh != 0f ? ", нове " + fresh.ToString("0.00", ci) : "") + ", шлях " + way.ToString("0.00", ci) + (here != 0f ? ", люди " + here.ToString("0.00", ci) : "") + (same != 0f ? ", щойно тут " + same.ToString("0.00", ci) : "") + (kind != 0f ? ", щойно таке " + kind.ToString("0.00", ci) : "") + ", випадок " + luck.ToString("0.00", ci);
        }

        // where the others are and where they are walking to, read once a choice (those run here: another game's plans are its own). A plan's
        // «To» counts only on its way: one standing or sitting is where it is, not where its plan was once made
        private static Vector3[] _otherAt = new Vector3[16], _otherTo = new Vector3[16];
        private static bool[] _otherGoes = new bool[16];
        private static NpcActor[] _otherWho = new NpcActor[16];
        private static int _others;
        private static void Others(NpcActor a)
        {
            _others = 0;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a) continue;
                if (_others == _otherAt.Length) { System.Array.Resize(ref _otherAt, _others * 2); System.Array.Resize(ref _otherTo, _others * 2); System.Array.Resize(ref _otherGoes, _others * 2); System.Array.Resize(ref _otherWho, _others * 2); }
                NpcMind.Plan cur = o.Mind != null ? o.Mind.Current : null;
                _otherAt[_others] = o.transform.position;
                _otherGoes[_others] = cur != null && !cur.Seated && (cur.Kind == NpcMind.Task.Go || cur.Kind == NpcMind.Task.Sit);
                _otherTo[_others] = cur != null ? cur.To : Vector3.zero;
                _otherWho[_others] = o;
                _others++;
            }
        }

        // its own place (visited FavSay times and more) with one of its people in it now: now and then a quarrel (NpcAttitude.Quarrel «place»,
        // once a day a pair at most)
        internal static float TakenChance = 0.3f;
        private static void Taken(NpcActor a, State s, Place p)
        {
            if (p.K.NoFav || UnityEngine.Random.value > TakenChance) return;
            Dictionary<int, int> f = FavOf(a, s);
            int visits;
            if (f == null || !f.TryGetValue(p.Key, out visits) || visits < FavSay) return;
            ZDOID heart = NpcKit.GetRef(a.Zdo, NpcResident.KHeart);
            float r = p.K.Reach * p.K.Reach;
            for (int i = 0; i < _others; i++)
            {
                NpcActor o = _otherWho[i];
                if (o == null || (_otherAt[i] - p.At).sqrMagnitude >= r || NpcKit.GetRef(o.Zdo, NpcResident.KHeart) != heart || NpcAttitude.OfPerson(a, o) >= 25f) continue; // (a friend in it: shared, not a quarrel)
                NpcAttitude.Quarrel(a, o, "place");
                return;
            }
        }

        private static int Crowd(Place p)
        {
            int n = 0;
            float r = p.K.Reach * p.K.Reach;
            for (int i = 0; i < _others; i++)
                if ((_otherAt[i] - p.At).sqrMagnitude < r || (_otherGoes[i] && (_otherTo[i] - p.At).sqrMagnitude < r)) n++;
            return n;
        }

        // ---------------------------------------------------------------- the places
        private static readonly List<Place> Pool = new List<Place>();
        private static readonly List<Place> Out = new List<Place>();
        private static int _used;
        private static Place New(string kind, Vector3 at, Vector3 look, Component thing, Vector3? keyAt = null)
        {
            if (_used >= Pool.Count) Pool.Add(new Place());
            Place p = Pool[_used++];
            p.K = Kinds[kind]; p.At = at; p.Look = look; p.Thing = thing; p.There = null; p.Sit = false; p.NoLook = false; p.Guest = false; p.Worth = 0f; p.Extra = 0f; p.Min = 0f; p.Max = 0f; p.Clip = p.K.Clip; p.From = p.K.From; p.Out = p.K.Out; p.Exact = false; p.Stand = null; p.Parts = "";
            Vector3 stands = thing != null ? thing.transform.position : keyAt ?? at;
            p.Key = p.K.Hash ^ PosKey(stands); p.ThingKey = thing != null ? PosKey(thing.transform.position) : 0;
            Out.Add(p);
            return p;
        }
        private static int PosKey(Vector3 at) { return (Mathf.RoundToInt(at.x) * 73856093) ^ (Mathf.RoundToInt(at.y) * 19349663) ^ (Mathf.RoundToInt(at.z) * 83492791); }
        // an author's place (StoryLife): made through the same pool, in the list being gathered
        internal static Place Spot(string kind, Vector3 at, Vector3 look, Vector3 thing) { return New(kind, at, look, null, thing); }
        internal static List<Place> Begin() { Out.Clear(); _used = 0; return Out; }

        // all it could go to, by what is known already: nothing here asks physics or the way finder (Ready does, for the few)
        private static List<Place> Candidates(NpcActor a)
        {
            if (Authored != null) { List<Place> own = Authored(a); if (own != null && own.Count > 0) return own; }
            NpcResident r = a.Resident;
            NpcBase b = r != null ? r.Base : null;
            if (b == null || !b.Ready) return null;
            Begin();
            Vector3 me = a.transform.position;
            float sits = 0.5f + NpcPersona.Of(a).Laziness * 0.5f;
            List<Component> fires = b.Round(typeof(Fireplace)) ?? b.Things(typeof(Fireplace)); // (its base's, and those in the open yard by it: Ready looks whether under a roof of another's)
            if (fires != null)
            {
                int fireStart = _from++ & 0xFFFF;
                for (int i = 0; i < fires.Count && i < Most; i++)
                {
                    Fireplace f = fires[(fireStart + i) % fires.Count] as Fireplace;
                    if (f == null || !f.IsBurning() || !NpcKit.Warms(f)) continue;
                    Vector3 at = f.transform.position;
                    if (!b.Inside(at) && Utils.DistanceXZ(at, b.Centre) > YardFire) continue;
                    New("fire", Side(at, me, 2f), at + Vector3.up * 0.5f, f).Sit = UnityEngine.Random.value < sits;
                }
            }
            Transform dining = NpcHeart.DiningOf(b);
            if (dining != null) { Place table = New("table", dining.position, dining.position + Vector3.up, null, dining.position); table.Sit = true; table.Guest = true; }
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || (v.At(0) == null && v.At(1) == null)) continue;
                Vector3 at = v.transform.position;
                if (b.Inside(at)) New("ravens", Side(at, me, 2f), at + Vector3.up * 1.3f, v);
            }
            for (int i = RecentBuilt.Count - 1; i >= 0; i--)
            {
                if (Time.time - RecentBuilt[i].When > WatchFor) { RecentBuilt.RemoveAt(i); continue; }
                Vector3 at = RecentBuilt[i].At;
                if (b.Inside(at)) New("watch", Side(at, me, 4f), at + Vector3.up, null, at);
            }
            Each<Chair>(b, c => New("seat", c.transform.position, c.transform.position + c.transform.forward * 3f + Vector3.up, c).Sit = true);
            Each<Container>(b, c => By(me, "stores", c));
            Each<CraftingStation>(b, c => By(me, "craft", c));
            Each<Smelter>(b, c => By(me, "craft", c));
            Each<CookingStation>(b, c => By(me, "craft", c));
            Each<Beehive>(b, c => By(me, "craft", c));
            Each<Fermenter>(b, c => By(me, "craft", c));
            Each<ItemStand>(b, c => { if (c.HaveAttachment()) By(me, "trophy", c); });
            Heart(a);
            Neighbours(a, b);
            Scars(a, b);
            Vector3 edge = b.EdgeSpot(), outward = edge - b.Centre; outward.y = 0f;
            New("edge", edge, edge + outward.normalized * 20f + Vector3.up * 2f, null);
            Vector3 roam = b.Spot();
            New("roam", roam, roam + Vector3.up, null).NoLook = true;
            return Out;
        }

        // a kind of its base's things: at most Most, from a place of its list that moves on, so a base of many chests has them all in turn
        private static int _from;
        private static void Each<T>(NpcBase b, Action<T> add) where T : Component
        {
            List<Component> l = b.Things(typeof(T));
            if (l == null || l.Count == 0) return;
            int start = _from++ & 0xFFFF;
            for (int i = 0; i < l.Count && i < Most; i++)
            {
                T c = l[(start + i) % l.Count] as T;
                if (c != null) add(c);
            }
        }

        // a step from the thing, on its side, facing it
        private static void By(Vector3 me, string kind, Component c)
        {
            Vector3 at = c.transform.position;
            New(kind, Side(at, me, 1.1f), at + Vector3.up * 0.8f, c);
        }

        // on its side of a thing (nothing asked of the way finder: the many places of a choice)
        internal static Vector3 Side(Vector3 thing, Vector3 from, float by)
        {
            Vector3 side = from - thing; side.y = 0f;
            return thing + (side.sqrMagnitude > 0.01f ? side.normalized : Vector3.forward) * by;
        }
        // …and on the walkable floor there
        internal static Vector3 Settled(Vector3 spot) { Vector3 v; return NpcKit.Valid(out v, spot, 1.5f, NpcKit.Walker) ? v : spot; }

        private static void Heart(NpcActor a)
        {
            ZDOID id = NpcKit.GetRef(a.Zdo, NpcResident.KHeart);
            ZDO heart = !id.IsNone() && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            if (heart == null) return;
            Vector3 c = heart.GetPosition();
            New("heart", Side(c, a.transform.position, 2.5f), c + Vector3.up, null, c);
        }

        // someone at its ease near: it walks up to them (and may talk: NpcChatter). Only those run here: another game's plans it cannot read
        private static void Neighbours(NpcActor a, NpcBase b)
        {
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || o.Mind == null || !o.Owned || !NpcResident.Is(o) || o.Mind.Asleep || !o.Mind.AtLeisure) continue;
                Vector3 at = o.transform.position;
                Character c = o.GetComponent<Character>();
                if (!b.Inside(at) || (c != null && c.GetVelocity().magnitude > 0.5f)) continue;
                Place pl = New("person", Side(at, a.transform.position, 1.5f), at + Vector3.up * 1.6f, null, at);
                pl.Extra += Mathf.Clamp(NpcAttitude.OfPerson(a, o), -100f, 100f) / 100f * FriendWorth; // (one it likes: rather to them; one it cannot stand: not)
                foreach (Func<NpcActor, NpcActor, float> w in PersonWorth) pl.Extra += w(a, o); // (a module's own: the neighbour it wishes for a friend — NpcGoals)
            }
        }

        // ---------------------------------------------------------------- what happened (stage 3 of the plan): where a fight was, what is new
        private sealed class Scar { public Vector3 At; public float When; }
        private static readonly List<Scar> AllScars = new List<Scar>(); // session: a fight's traces are looked at the same day or not at all
        private static void Scars(NpcActor a, NpcBase b)
        {
            for (int i = AllScars.Count - 1; i >= 0; i--)
            {
                if (Time.time - AllScars[i].When > ScarFor) { AllScars.RemoveAt(i); continue; }
                Vector3 at = AllScars[i].At;
                if (!b.Inside(at)) continue;
                New("scar", Side(at, a.transform.position, 1.5f), at + Vector3.up * 0.3f, null, at);
            }
        }

        // a person's fight ended: there is something to look at (the bus says where)
        private static void Heard(StoryEvent e)
        {
            if (e.Name == "piece_placed" && e.HasAt) { Built(e.At); return; }
            if (e.Name != "npc_fight_over" || !e.HasAt) return;
            foreach (Scar s in AllScars) if ((s.At - e.At).sqrMagnitude < 25f) { s.When = Time.time; return; }
            if (AllScars.Count >= 8) AllScars.RemoveAt(0);
            AllScars.Add(new Scar { At = e.At, When = Time.time });
        }

        // a thing a player has just built (Diary.Placed → the bus «piece_placed», where it stands): new for FreshFor s. Not what merely loads
        // as you come near (a snapshot's difference took every piece loaded later for new, 2026-11-04). Heard on the builder's game: a person
        // run on another game does not hear it
        private static readonly Dictionary<int, float> NewThings = new Dictionary<int, float>(); // session
        private static readonly List<Scar> RecentBuilt = new List<Scar>(); // session: where a player has just built, to go and watch
        private static object _world;
        private static void Built(Vector3 at)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { NewThings.Clear(); AllScars.Clear(); RecentBuilt.Clear(); _world = ZNet.instance; }
            if (NewThings.Count > 64) NewThings.Clear();
            NewThings[PosKey(at)] = Time.time;
            if (RecentBuilt.Count >= 8) RecentBuilt.RemoveAt(0);
            RecentBuilt.Add(new Scar { At = at, When = Time.time });
        }
        private static bool IsFresh(int key) { float t; return NewThings.TryGetValue(key, out t) && Time.time - t < FreshFor; }

        // ---------------------------------------------------------------- for others: the same places, one kind
        // the best of a kind for it now as a plan (a whim's rest, its fire), null: none
        internal static NpcMind.Plan Best(NpcActor a, string kind)
        {
            State s;
            if (!States.TryGetValue(a, out s)) States[a] = s = new State { Next = Time.time + UnityEngine.Random.Range(FirstMin, FirstMax) };
            List<Place> all = Candidates(a);
            if (all == null) return null;
            Need n = NeedOf(a);
            Others(a);
            foreach (Place p in all) { if (p.K.Id == kind) Weigh(a, n, s, p, false); else p.Worth = float.MinValue; }
            all.Sort(ByWorth);
            Place best = First(a, s, all);
            if (best == null) return null;
            Went(s, best);
            NpcMind.Plan plan = Plan(best);
            plan.Word = best.K.There;
            return plan;
        }

        // to sit where it is, if that is its to sit on; else a spot of its own in its base (no seat for its rest: the ground)
        internal static NpcMind.Plan RestHere(NpcActor a)
        {
            NpcResident r = a.Resident;
            NpcBase b = r != null ? r.Base : null;
            Vector3 me = a.transform.position;
            return new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = b == null || !b.Ready || (b.Inside(me) && !NpcPrivate.Off(a, b, me)) ? me : NpcPrivate.Spot(a, b) };
        }

        // a fire burns in its base that is its to sit by (its mood, a whim's «can»): its list, the first that holds
        internal static bool AnyFire(NpcActor a)
        {
            NpcResident r = a.Resident;
            NpcBase b = r != null ? r.Base : null;
            List<Component> l = b != null && b.Ready ? b.Things(typeof(Fireplace)) : null;
            if (l == null) return false;
            foreach (Component c in l) { Fireplace f = c as Fireplace; if (f != null && f.IsBurning() && NpcKit.Warms(f) && !NpcPrivate.Off(a, b, f.transform.position)) return true; }
            return false;
        }

        // the nearest thing of its base under a roof (a fire, a chest, a bench, a bed…), a step off it on its side (a seat:
        // the seat itself, seat true); none: no. Its rain and its raid ask (NpcLife)
        private static readonly Type[] Shelters = { typeof(Fireplace), typeof(Container), typeof(Chair), typeof(Bed), typeof(CookingStation) };
        private static readonly Dictionary<string, KeyValuePair<float, List<Component>>> Dry = new Dictionary<string, KeyValuePair<float, List<Component>>>(); // base → {when, its things under a roof}
        internal static bool Roofed(NpcActor a, NpcBase b, out Vector3 at, out bool seat) { string why; return Roofed(a, b, out at, out seat, out why); }
        internal static bool Roofed(NpcActor a, NpcBase b, out Vector3 at, out bool seat, out string why)
        {
            at = a.transform.position; seat = false; why = "";
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<Component>> had;
            List<Component> dry;
            if (Dry.TryGetValue(key, out had) && Time.time - had.Key < 60f) dry = had.Value;
            else
            {
                dry = new List<Component>();
                foreach (Type t in Shelters)
                {
                    List<Component> things = b.Things(t);
                    if (things == null) continue;
                    foreach (Component c in things)
                        if (c != null && dry.Count < 40 && NpcToil.Roof(c.transform.position + Vector3.up * 1.6f)) dry.Add(c); // heavy: the base's things under a roof, once a minute a base and only in rain or a raid (at most 40 found)
                }
                if (Dry.Count > 50) Dry.Clear();
                Dry[key] = new KeyValuePair<float, List<Component>>(Time.time, dry);
            }
            float best = float.MaxValue; Component near = null; int off = 0;
            foreach (Component c in dry)
            {
                if (c == null) continue;
                if (NpcPrivate.Off(a, b, c.transform.position)) { off++; continue; } // (a player's own house: not for it)
                float d = Utils.DistanceXZ(a.transform.position, c.transform.position);
                if (d < best) { best = d; near = c; }
            }
            if (near == null) { why = "no roof: " + dry.Count + " things of its base under a roof, " + off + " of them in a player's house"; return false; }
            seat = near is Chair; // (a seat: «sit» finds it from the spot by it — within 4 m)
            at = Settled(Side(near.transform.position, a.transform.position, 1.1f));
            return true;
        }

        // ---------------------------------------------------------------- raven npc why <who>
        internal static string Why(NpcActor a)
        {
            string busy = NpcKit.Busy(a);
            if (busy != null) return busy;
            if (!NpcResident.Is(a)) return a.Name + " ніде не живе: місця лише в мешканців.";
            State s;
            if (!States.TryGetValue(a, out s)) States[a] = s = new State();
            List<Place> all = Candidates(a);
            if (all == null) return a.Name + ": база ще не оглянута.";
            Need n = NeedOf(a);
            Others(a);
            foreach (Place p in all) Weigh(a, n, s, p, true);
            all.Sort(ByWorth);
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder t = new StringBuilder();
            t.Append(a.Name).Append(": ").Append(a.StatusNow).Append(s.Last.Length > 0 ? "; востаннє " + s.Last : "");
            t.Append("\nхолод ").Append(n.Cold.ToString("0.0", ci)).Append(", втома ").Append(n.Tired.ToString("0.0", ci)).Append(", лінь ").Append(n.Lazy.ToString("0.0", ci)).Append(", старанність ").Append(n.Diligent.ToString("0.0", ci)).Append(", балакучість ").Append(n.Chatty.ToString("0.0", ci)).Append(", цікавість ").Append(n.Curious.ToString("0.0", ci)).Append(", сміливість ").Append(n.Brave.ToString("0.0", ci));
            int shown = 0;
            foreach (Place p in all)
            {
                if (shown++ >= 10) break;
                t.Append("\n").Append(p.K.Id).Append(" ").Append(Mathf.RoundToInt(p.At.x)).Append(",").Append(Mathf.RoundToInt(p.At.z)).Append(": ");
                t.Append(p.Worth <= float.MinValue ? p.Parts : p.Worth.ToString("0.00", ci) + " (" + p.Parts + ")");
            }
            t.Append("\nусього місць: ").Append(all.Count).Append(s.Last2.Length > 0 ? "; востаннє відкинуто: " + s.Last2 : "");
            Npcs.Log(t.ToString());
            return t.ToString();
        }

        internal static void Wire()
        {
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            StoryBus.Listen("npcplaces", Heard);
            Npcs.Commands["why"] = (p, who, a, aim) => { return Why(who); };
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A settler's long wish (not a story person's: the story says what they want) (the user 2026-11-05, «живі NPC», stage 4): one at a time, for days — not a request («bring ten logs in three
    // days»: NpcRequests), but something of its own it keeps wanting. It shows: in its words (its themes goal_<kind> now and then,
    // «Про що мрієш?» in its talk, its card «Вдача»), in its day (where it goes of its own accord: NpcPlaces.PersonWorth — to the
    // neighbour it wants for a friend), in its mood (NpcToil.MoodFrom: proud a few days once it came true, a little down after long
    // wanting). Come true: glad (NpcFeel joy), a line, its memory «goal_done» (and a player who helped: warmer to him — NpcMemory
    // Kinds Toward); some days later a new one; not come true in GiveUpDays, it lets it go. The kinds (Kinds: a new kind is a new entry):
    //   bed      its own bed (a resident with none)                        true when it is given one (NpcResident.GiveBed: the one who gave it, warmer)
    //   master   to master its work (a worker below ×1 of its craft)     true when its craft is full (NpcToil.Craft): a pay a step, so it wants this longer than the rest (GiveUp, Long)
    //   comfort  a thing at its base: a beehive, a fermenter, a trophy,  true when the base has it (one more seat: more than at
    //            a cooking station, one more seat (only what is not there)  the start) — NpcBase.Things, no physics; the one who built or hung that very thing, warmer (Built)
    //   friend   a neighbour it gets on with (NpcAttitude.Fit ≥ FriendFit) true when it is close to it (≥ FriendAt)
    //   thing    a thing from the life before (lands reached: NpcRequests.Tier) true when a player gives it (its talk: ?goalitem
    //                                                                       !goalgive) — paid by what it is worth
    //   room     a place of its own (it has a bed)                        true when its bed is under a roof, a door by it, nobody else's bed near; the one whose bed or building finished it, warmer
    //   learn    to try another work: one it would take, what its hands know first   true when it has that work (NpcWorker.SetJob: the one who gave it, warmer); it starts a step ahead (NpcToil.Learned)
    //   return   to stand again in a place it dreams of (WorldSpot, lands the world has reached, over land from home)   true when it stands there, led by «за мною» (the nearest player within 60 m, warmer); shown on the map from its talk (!goalshow)
    // Kept as one short string in its locker (NpcLocker: «kind|arg|since|base»), its next wish's time and its last come-true there too:
    // a reload, a new owner go on with it. Chosen and checked where it is run (NpcActor.Ticks): a look every CheckEvery s a person, a
    // few lists counted; a new wish only after GapMin…GapMax game days. In a talk (NpcTalk): ?hasgoal, ?goalitem, {goal}, {goal_thanks},
    // !goalgive. Console: raven npc goal <who> [kind|done|clear].
    internal static class NpcGoals
    {
        internal static readonly int KGoal = "RavenWatch_npcGoal".GetStableHashCode();         // string «kind|arg|since|base» ("" none) — in its locker
        internal static readonly int KGoalNext = "RavenWatch_npcGoalNext".GetStableHashCode(); // string: world seconds of its next wish — in its locker
        internal static readonly int KGoalDone = "RavenWatch_npcGoalDone".GetStableHashCode(); // string: world seconds its last came true — in its locker
        internal static float SpotLook = 60f, SpotEvery = 5f, CheckEvery = 60f, FirstDays = 1f, GapMin = 2f, GapMax = 4f, ProudDays = 3f, LongDays = 6f, GiveUpDays = 15f;
        internal static float FriendFit = 65f, FriendAt = 60f, FriendWorth = 0.5f, Proud = 8f, Pining = -3f;
        private const float MasterDays = 25f; // (a newcomer's four pays, NpcToil.PayDays apart, and one pay's room: what takes that long is no reason to give up or pine)

        internal sealed class Goal { public string Kind = "", Arg = ""; public long Since; public int Base; }

        internal sealed class Kind
        {
            public string Id = "";
            public Func<NpcActor, Goal> Pick;                 // a wish of this kind it could have now (null: none)
            public Func<NpcActor, Goal, bool> Done;           // has it come true by itself (null: only by a gift)
            public Func<NpcActor, Goal, string> What;         // {goal_what}: what it wants, in words (null: none fits now)
            public Func<NpcActor, Goal, string> Card;         // its card «Вдача»
            public Func<NpcActor, Goal, Piece, bool, bool> Deed; // a player built this (last word true: hung a trophy on it): that very thing it wished for (Built)
            public float GiveUp, Long;                        // game days till it lets go, till it pines: its own (0: GiveUpDays, LongDays)
            public bool Yours;                                // a player can bring it about (a heart's board, a promise: ForYou)
            public Func<NpcActor, Goal, long> By;             // who brought it about, when it came true by itself (0: nobody)
            public Func<NpcActor, Goal, Vector3?> Spot;       // a place it wants: shown from its talk, looked at often while a player is near
            public Action<NpcActor, Goal> Then;               // what follows when it comes true, where it is run
            public string DoneTheme = "goal_done";            // its words then
        }

        // ---------------------------------------------------------------- the kinds
        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>();
        private static void K(Kind k) { Kinds[k.Id] = k; }

        // a thing at its base: its type, how it is counted, its word
        private sealed class Comfort { public string Id; public Type T; public bool Trophy; public string Word; public bool More; }
        private static readonly Comfort[] Comforts =
        {
            new Comfort { Id = "beehive", T = typeof(Beehive), Word = Lang.K("вулик") },
            new Comfort { Id = "fermenter", T = typeof(Fermenter), Word = Lang.K("бродильний чан") },
            new Comfort { Id = "cooking", T = typeof(CookingStation), Word = Lang.K("місце, де готувати") },
            new Comfort { Id = "trophy", T = typeof(ItemStand), Trophy = true, Word = Lang.K("трофей на стіні") },
            new Comfort { Id = "seat", T = typeof(Chair), Word = Lang.K("ще одна лава"), More = true },
        };
        private static Comfort ComfortOf(string id) { foreach (Comfort c in Comforts) if (c.Id == id) return c; return null; }
        private static int Count(NpcActor a, Comfort c)
        {
            NpcBase b = NpcBase.For(NpcRules.Home(a));
            if (b == null || !b.Ready) return -1;
            List<Component> l = b.Things(c.T);
            if (l == null) return 0;
            if (!c.Trophy) return l.Count;
            int n = 0; foreach (Component x in l) { ItemStand s = x as ItemStand; if (s != null && s.HaveAttachment()) n++; }
            return n;
        }

        // a thing from the life before: [item, the lands it needs (NpcRequests.Tier)] — a keepsake, not a resource; the cheaper first
        private static readonly string[][] Keepsakes = { new[] { "TrophyDeer", "0" }, new[] { "TrophyBoar", "0" }, new[] { "TrophyGreydwarf", "1" }, new[] { "Amber", "1" }, new[] { "AmberPearl", "2" }, new[] { "SilverNecklace", "3" }, new[] { "Ruby", "5" } };
        internal static float ThingPay = 3f, ThingLeast = 15f; // its keepsake: × what it is worth, in coins · never less

        static NpcGoals()
        {
            K(new Kind
            {
                Id = "bed",
                Pick = a => NpcLocker.HasRef(a.Zdo, NpcResident.KBed) ? null : new Goal(),
                Done = (a, g) => NpcLocker.HasRef(a.Zdo, NpcResident.KBed),
                What = (a, g) => "",
                Card = (a, g) => Lang.T("мріє про своє ліжко"),
                Yours = true,
            });
            K(new Kind
            {
                Id = "master",
                Pick = a => a.Worker != null && a.Worker.Works && NpcToil.Craft(a, a.Worker.JobId) < 0.95f ? new Goal { Arg = a.Worker.JobId } : null,
                Done = (a, g) => NpcToil.Craft(a, g.Arg) >= 0.99f,
                What = (a, g) => Jobs.Word(g.Arg),
                Card = (a, g) => Lang.F("хоче стати майстром: {0}", Jobs.Word(g.Arg)),
                GiveUp = MasterDays, Long = MasterDays,
            });
            K(new Kind
            {
                Id = "comfort",
                Pick = a =>
                {
                    List<Comfort> can = new List<Comfort>();
                    foreach (Comfort c in Comforts) { int n = Count(a, c); if (n < 0) return null; if (c.More || n == 0) can.Add(c); }
                    if (can.Count == 0) return null;
                    Comfort pick = can[UnityEngine.Random.Range(0, can.Count)];
                    return new Goal { Arg = pick.Id, Base = pick.More ? Count(a, pick) : 0 };
                },
                Done = (a, g) => { Comfort c = ComfortOf(g.Arg); int n = c != null ? Count(a, c) : -1; return c == null || n > g.Base; }, // (a kind no longer known: over)
                What = (a, g) => { Comfort c = ComfortOf(g.Arg); return c != null ? Lang.T(c.Word) : null; },
                Card = (a, g) => { Comfort c = ComfortOf(g.Arg); return Lang.F("хоче на базі: {0}", c != null ? Lang.T(c.Word) : "?"); },
                Deed = (a, g, made, hung) => { Comfort c = ComfortOf(g.Arg); return c != null && c.Trophy == hung && made.GetComponentInChildren(c.T) != null; }, // heavy: once a thing built near one who wishes for a comfort
                Yours = true,
            });
            K(new Kind
            {
                Id = "friend",
                Pick = a =>
                {
                    NpcActor best = null; float most = FriendFit;
                    foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
                    {
                        if (o == null || o == a) continue;
                        float fit = NpcAttitude.Fit(a, o);
                        if (fit >= most && NpcAttitude.OfPerson(a, o) < FriendAt) { most = fit; best = o; }
                    }
                    return best != null ? new Goal { Arg = NpcAttitude.PersonKey(best).ToString(CultureInfo.InvariantCulture) } : null;
                },
                Done = (a, g) => { long k; return long.TryParse(g.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && NpcAttitude.Of(a, k) >= FriendAt; },
                What = (a, g) => { NpcActor o = Person(g); return o != null ? o.Name : null; },
                Card = (a, g) => { NpcActor o = Person(g); return Lang.F("хоче потоваришувати: {0}", o != null ? o.Name : Lang.T("сусід")); },
            });
            K(new Kind
            {
                Id = "thing",
                Pick = a =>
                {
                    int tier = NpcRequests.Tier();
                    for (int i = (int)(NpcKit.Seed(a.Zdo) % (uint)Keepsakes.Length); i >= 0; i--) // (its own keepsake by its seed, the same one every time; of lands not reached yet: the next cheaper one for now)
                        if (int.Parse(Keepsakes[i][1], CultureInfo.InvariantCulture) <= tier && ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(Keepsakes[i][0]) != null) return new Goal { Arg = Keepsakes[i][0] };
                    return null;
                },
                Done = null,
                What = (a, g) => NpcRequests.Words(g.Arg, 1),
                Card = (a, g) => Lang.F("шукає річ із минулого: {0}", NpcRequests.Words(g.Arg, 1)),
                Yours = true,
            });
            K(new Kind
            {
                Id = "room",
                Pick = a => BedOf(a) != null && !HasRoom(a, null) ? new Goal() : null,
                Done = (a, g) => HasRoom(a, null),
                What = (a, g) => "",
                Card = (a, g) => Lang.T("мріє про власну кімнату"),
                Deed = (a, g, made, hung) => !hung && Finished(a, made), // heavy: once a thing built by its bed, while it wishes for a room (its roof, its door: whoever finished it)
                Yours = true, DoneTheme = "goal_room_done",
            });
            K(new Kind
            {
                Id = "learn",
                Pick = a =>
                {
                    string now = a.Worker != null ? a.Worker.JobId : "";
                    List<string> can = new List<string>(), known = new List<string>();
                    foreach (Job j in Jobs.All)
                    {
                        if (j.Id == now || NpcWorker.WhyNot(a, j) != null) continue;
                        can.Add(j.Id);
                        if (NpcBackstory.Knack(a, j.Id) > 0f) known.Add(j.Id); // (what its hands remember first)
                    }
                    List<string> from = known.Count > 0 ? known : can;
                    return from.Count > 0 ? new Goal { Arg = from[(int)(NpcKit.Seed(a.Zdo) % (uint)from.Count)] } : null;
                },
                Done = (a, g) => a.Worker != null && a.Worker.JobId == g.Arg,
                What = (a, g) => Jobs.Word(g.Arg),
                Card = (a, g) => Lang.F("хоче навчитись іншого: {0}", Jobs.Word(g.Arg)),
                Then = (a, g) => NpcToil.Learned(a, g.Arg),
                Yours = true, DoneTheme = "goal_learn_done",
            });
            K(new Kind
            {
                Id = "return",
                Pick = a =>
                {
                    Player p = Player.GetClosestPlayer(a.transform.position, 200f); // (WorldSpot asks what lands a player knows)
                    Vector3 at, home = NpcRules.Home(a);
                    for (int i = 0; p != null && i < ReturnTries; i++) // heavy: WorldSpot.Pick (bases, the world's places) at most ReturnTries times, only on the turn this kind is chosen (Choose), a wish every few game days
                        if (WorldSpot.Pick(p, home, ReturnMin, ReturnMax, false, null, out at) && LandTier(at) <= NpcRequests.Tier() && OverLand(home, at))
                            return new Goal { Arg = at.x.ToString("0", CultureInfo.InvariantCulture) + "_" + at.y.ToString("0", CultureInfo.InvariantCulture) + "_" + at.z.ToString("0", CultureInfo.InvariantCulture) };
                    return null;
                },
                Spot = (a, g) => SpotOf(g),
                Done = (a, g) => { Vector3? at = SpotOf(g); return at != null && Utils.DistanceXZ(a.transform.position, at.Value) <= ReturnNear; },
                By = (a, g) => { Player p = Player.GetClosestPlayer(a.transform.position, ReturnNear * 2f); return p != null ? p.GetPlayerID() : 0L; },
                What = (a, g) => { Vector3? at = SpotOf(g); return at != null ? LandWord(at.Value) : ""; },
                Card = (a, g) => { Vector3? at = SpotOf(g); return Lang.F("хоче ще раз побачити місце: {0}", at != null ? LandWord(at.Value) : "?"); },
                Yours = true, DoneTheme = "goal_return_done",
            });
        }

        // ---- «room»: under a roof, a door by it, nobody else's bed near (doorAlso: a door just built, not in its base's look yet). Heavy: a look under its roof, CheckEvery s a person only while it wishes this
        internal static float RoomDoor = 7f, RoomApart = 5f, ReturnMin = 150f, ReturnMax = 500f, ReturnNear = 30f;
        private const int ReturnTries = 3;
        internal static float LandStep = 15f;

        // a walk there on dry land: the world's height along the line every LandStep m (no lake, no sea between: people do not swim)
        private static bool OverLand(Vector3 from, Vector3 to)
        {
            if (WorldGenerator.instance == null) return false;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            int n = Mathf.CeilToInt(Utils.DistanceXZ(from, to) / LandStep);
            for (int i = 1; i < n; i++) { Vector3 q = Vector3.Lerp(from, to, i / (float)n); if (WorldGenerator.instance.GetHeight(q.x, q.z) < water + 0.5f) return false; }
            return true;
        }
        private static GameObject BedOf(NpcActor a) { return NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed)); }
        private static bool HasRoom(NpcActor a, Piece doorAlso)
        {
            GameObject bed = BedOf(a);
            NpcBase b = bed != null ? NpcBase.For(NpcRules.Home(a)) : null;
            List<Component> doors = b != null ? b.Round(typeof(Door)) : null;
            if (doors == null) return false;
            Vector3 at = bed.transform.position;
            bool door = doorAlso != null && Utils.DistanceXZ(doorAlso.transform.position, at) <= RoomDoor;
            foreach (Component d in doors) if (!door && d != null && Utils.DistanceXZ(d.transform.position, at) <= RoomDoor) door = true;
            if (!door || !NpcToil.Roof(at + Vector3.up * 0.5f)) return false;
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
            {
                if (o == null || o == a) continue;
                GameObject other = BedOf(o);
                if (other != null && Utils.DistanceXZ(other.transform.position, at) <= RoomApart) return false;
            }
            return true;
        }

        // did this very thing finish its room (not whatever is built by its bed while a room it already has is not noticed yet: CheckEvery s)?
        //   a door by its bed: there was no room without it (a door just built is not in its base's look yet), there is with it
        //   a roof over its bed: the room is there now and straight over its bed there is this thing and no other (any other: a roof was there before)
        // Heavy: one ray up from its bed, once a thing built by it, only while it wishes for a room
        internal static float RoofUp = 12f;
        private static bool Finished(NpcActor a, Piece made)
        {
            GameObject bed = BedOf(a);
            if (bed == null || made == null || Utils.DistanceXZ(bed.transform.position, made.transform.position) > RoomDoor) return false;
            if (made.GetComponent<Door>() != null) return !HasRoom(a, null) && HasRoom(a, made);
            if (!HasRoom(a, null)) return false;
            bool hit = false;
            foreach (RaycastHit h in Physics.RaycastAll(bed.transform.position + Vector3.up * 0.5f, Vector3.up, RoofUp, LayerMask.GetMask("piece"))) // heavy: one ray, once a thing built within RoomDoor of its bed, only while it wishes for a room
            {
                Piece p = h.collider != null ? h.collider.GetComponentInParent<Piece>() : null;
                if (p == null || p.gameObject == bed) continue; // (its own bed's frame)
                if (p != made) return false;
                hit = true;
            }
            return hit;
        }

        // ---- «return»: the place, its land in a word, how far a world must have gone for one to walk there
        private static Vector3? SpotOf(Goal g)
        {
            string[] w = g.Arg.Split('_');
            float x, y, z;
            if (w.Length < 3 || !float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return null;
            return new Vector3(x, y, z);
        }
        private static int LandTier(Vector3 at)
        {
            switch (WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(at) : Heightmap.Biome.None)
            {
                case Heightmap.Biome.Meadows: return 0; // (as NpcRequests.Tier counts: the lords beaten)
                case Heightmap.Biome.BlackForest: return 1;
                case Heightmap.Biome.Swamp: return 2;
                case Heightmap.Biome.Mountain: return 3;
                case Heightmap.Biome.Plains: return 4;
                case Heightmap.Biome.Mistlands: return 5;
                default: return 99;
            }
        }
        private static string LandWord(Vector3 at)
        {
            switch (WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(at) : Heightmap.Biome.None)
            {
                case Heightmap.Biome.Meadows: return Lang.T("луки");
                case Heightmap.Biome.BlackForest: return Lang.T("чорний ліс");
                case Heightmap.Biome.Swamp: return Lang.T("болото");
                case Heightmap.Biome.Mountain: return Lang.T("гори");
                case Heightmap.Biome.Plains: return Lang.T("рівнини");
                case Heightmap.Biome.Mistlands: return Lang.T("туманні землі");
                default: return Lang.T("далекий край");
            }
        }

        // the neighbour of a «friend» wish, if it is loaded here
        private static NpcActor Person(Goal g)
        {
            long k; if (!long.TryParse(g.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out k)) return null;
            foreach (NpcActor o in NpcActor.Live) if (o != null && NpcAttitude.PersonKey(o) == k) return o;
            return null;
        }

        // ---------------------------------------------------------------- kept (its locker)
        internal static Goal Of(NpcActor a)
        {
            string s = a != null ? NpcLocker.Get(a, KGoal) : null;
            if (string.IsNullOrEmpty(s)) return null;
            string[] w = s.Split('|');
            if (w.Length < 4 || !Kinds.ContainsKey(w[0])) return null;
            Goal g = new Goal { Kind = w[0], Arg = w[1] };
            long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out g.Since);
            int.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out g.Base);
            return g;
        }
        private static void Write(NpcActor a, Goal g) { NpcLocker.Set(a, KGoal, g == null ? "" : g.Kind + "|" + g.Arg + "|" + g.Since.ToString(CultureInfo.InvariantCulture) + "|" + g.Base.ToString(CultureInfo.InvariantCulture)); }
        private static long Long(NpcActor a, int key) { long v; string s = NpcLocker.Get(a, key); return s != null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0L; }
        private static float Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }

        // ---------------------------------------------------------------- its life (where it is run)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        internal static void Tick(NpcActor a)
        {
            float next;
            if (!Next.TryGetValue(a, out next)) { Next[a] = Time.time + UnityEngine.Random.Range(5f, CheckEvery); return; }
            if (Time.time < next) return;
            Next[a] = Time.time + CheckEvery;
            if (a.Def == null || !a.Def.Settler || !NpcResident.Is(a) || NpcLocker.Get(a, KGoal) == null) return; // (its locker not here yet)
            long now = (long)NpcRescue.Now;
            Goal g = Of(a);
            if (g == null)
            {
                long when = Long(a, KGoalNext);
                if (when == 0L) { NpcLocker.Set(a, KGoalNext, (now + (long)(FirstDays * Day)).ToString(CultureInfo.InvariantCulture)); return; }
                if (now >= when) Choose(a, null);
                return;
            }
            Kind k = Kinds[g.Kind];
            if (k.Spot != null && Player.GetClosestPlayer(a.transform.position, SpotLook) != null) Next[a] = Time.time + SpotEvery; // (it may be led past: looked at often while someone is by it)
            if (k.Done != null && k.Done(a, g)) { long by = k.By != null ? k.By(a, g) : 0L; Came(a, g, by != 0L ? by : MakerOf(a)); Makers.Remove(a); return; }
            if (now - g.Since <= (k.GiveUp > 0f ? k.GiveUp : GiveUpDays) * Day) return;
            Write(a, null);
            NpcLocker.Set(a, KGoalNext, (now + (long)(GapMin * Day)).ToString(CultureInfo.InvariantCulture));
            Npcs.Log(a.Id + ": its wish «" + g.Kind + "» never came: let go");
        }

        internal static void Forget(NpcActor a) { Next.Remove(a); Makers.Remove(a); }

        // a new wish: one of the kinds it could have now, at random (kind: that one — the console)
        // the kinds in a random order, the first that has a wish now (as fair as choosing among all that could, and a dear Pick — «return» — runs only when it is its turn)
        private static string Choose(NpcActor a, string kind)
        {
            List<Kind> order = new List<Kind>(Kinds.Values);
            for (int i = order.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); Kind t = order[i]; order[i] = order[j]; order[j] = t; }
            List<Goal> can = new List<Goal>();
            foreach (Kind k in order)
            {
                if (kind != null && k.Id != kind) continue;
                Goal g = k.Pick(a);
                if (g != null) { g.Kind = k.Id; can.Add(g); break; }
            }
            long now = (long)NpcRescue.Now;
            if (can.Count == 0) { NpcLocker.Set(a, KGoalNext, (now + (long)(GapMin * Day)).ToString(CultureInfo.InvariantCulture)); return null; } // (nothing to wish for now: asked again later)
            Goal pick = can[UnityEngine.Random.Range(0, can.Count)];
            pick.Since = now;
            Write(a, pick);
            Npcs.Log(a.Id + ": wishes «" + pick.Kind + "» " + pick.Arg);
            Player p = Player.GetClosestPlayer(a.transform.position, 15f);
            if (p != null && NpcVoice.Free(a)) { string s = NpcPhrases.Compose(a, "goal_" + pick.Kind, p, Extra(a, pick)); if (!string.IsNullOrEmpty(s)) a.Say(s, null, NpcActor.Voice.Unasked); } // (said to whoever is near: else it waits for its talk)
            return pick.Kind;
        }

        // come true: glad, a line, remembered (giver: the player who gave it — warmer to him; 0: nobody's doing), a new wish later
        private static void Came(NpcActor a, Goal g, long giver)
        {
            Write(a, null);
            long now = (long)NpcRescue.Now;
            NpcLocker.Set(a, KGoalDone, now.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KGoalNext, (now + (long)(UnityEngine.Random.Range(GapMin, GapMax) * Day)).ToString(CultureInfo.InvariantCulture));
            string what = Kinds[g.Kind].What(a, g) ?? "";
            NpcMemory.Remember(a, "goal_done", giver, what);
            NpcFeel.Feel(a, NpcFeel.Joy, 120f, a.transform.position, false);
            Kind k = Kinds[g.Kind];
            if (k.Then != null) k.Then(a, g);
            NpcPhrases.SayOr(a, k.DoneTheme, a.Form(Lang.T("Збулося! Я й не вірив|вірила.")), Extra(a, g));
            Npcs.Log(a.Id + ": its wish «" + g.Kind + "» came true" + (giver != 0L ? " (" + giver + ")" : ""));
            StoryBus.Raise("npc_goal_done", a.Id, 0, a.transform.position);
        }

        private static Dictionary<string, string> Extra(NpcActor a, Goal g)
        {
            string w = Kinds[g.Kind].What(a, g);
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (w != null) d["goal_what"] = w;
            return d;
        }

        // ---------------------------------------------------------------- what it adds elsewhere
        // its mood (NpcToil.MoodFrom): proud a few days after, a little down after long wanting
        internal static float Mood(NpcActor a)
        {
            long now = (long)NpcRescue.Now, done = Long(a, KGoalDone);
            if (done > 0L && now - done < ProudDays * Day) return Proud;
            Goal g = Of(a);
            if (g == null) return 0f;
            Kind k = Kinds[g.Kind];
            return now - g.Since > (k.Long > 0f ? k.Long : LongDays) * Day ? Pining : 0f;
        }

        // you built this, or hung a trophy on it (Diary.Placed, Diary's trophy patch; on your game): the people round it who wished for that very thing note you as its maker.
        // Not come true by that alone: whether it did is its own Done, looked at once its base has looked again (BuiltLook) — a seat taken down and built again is no seat more.
        // A look at each one's short string, once a thing built
        internal static float BuiltLook = 35f, BuiltFor = 600f;
        internal static void Built(Player p, Piece made, bool hung)
        {
            if (p == null || made == null) return;
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || Utils.DistanceXZ(NpcRules.Home(a), made.transform.position) > NpcBase.Reach) continue;
                Goal g = Of(a);
                if (g != null && Kinds[g.Kind].Deed != null && Kinds[g.Kind].Deed(a, g, made, hung)) a.Act("goalbuilt " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture));
            }
        }

        // «goalbuilt <player>» where it is run: that player made what it wished for, if it comes true within BuiltFor s (only from that player's game)
        private sealed class Maker { public long Player; public float At; }
        private static readonly Dictionary<NpcActor, Maker> Makers = new Dictionary<NpcActor, Maker>(); // session: who built it lately (a reload in those few seconds: comes true with nobody's name)
        private static void MadeBy(NpcActor a, string[] w)
        {
            long id;
            if (w.Length < 2 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || !NpcActor.SentBy(id)) return;
            if (Makers.Count > 64) Makers.Clear();
            Makers[a] = new Maker { Player = id, At = Time.time };
            Next[a] = Time.time + BuiltLook; // (looked at again soon: its base's look of things is made again within NpcBase's Recheck)
        }
        private static long MakerOf(NpcActor a) { Maker m; return Makers.TryGetValue(a, out m) && Time.time - m.At <= BuiltFor ? m.Player : 0L; }

        // its wish of a bed, come true by you (NpcResident.GiveBed)
        internal static void BedGiven(NpcActor a, Player p) { Goal g = p != null ? Of(a) : null; if (g != null && (g.Kind == "bed" || (g.Kind == "room" && HasRoom(a, null)))) GivenBy(a, p); }

        // the work it wished to learn, from you (NpcWorker.SetJob: the one who pressed it)
        internal static void JobGiven(NpcActor a, Player p, string job) { Goal g = p != null ? Of(a) : null; if (g != null && g.Kind == "learn" && g.Arg == job) GivenBy(a, p); }

        private static void GivenBy(NpcActor a, Player p) { a.Act("goalgiven " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture)); }

        // to the neighbour it wants for a friend: rather than to another (NpcPlaces.PersonWorth)
        internal static float Toward(NpcActor a, NpcActor o)
        {
            Goal g = Of(a);
            return g != null && g.Kind == "friend" && g.Arg == NpcAttitude.PersonKey(o).ToString(CultureInfo.InvariantCulture) ? FriendWorth : 0f;
        }

        // its card «Вдача» (NpcProfile.More)
        internal static string Card(NpcActor a) { Goal g = Of(a); return g != null ? Kinds[g.Kind].Card(a, g) : null; }

        // ...how long it has wished for it ("" not a day yet: no words)
        internal static string CardSince(NpcActor a)
        {
            Goal g = Of(a);
            int days = g != null ? Mathf.FloorToInt((float)(NpcRescue.Now - g.Since) / Day) : 0;
            return days >= 1 ? Lang.F("уже {0}", Lang.N(days, "день|дні|днів")) : "";
        }

        // a wish a player can bring about (a heart's board «Справи», a promise): its row says so (Yours); not its craft or a friend
        internal static bool ForYou(NpcActor a) { Goal g = Of(a); return g != null && Kinds[g.Kind].Yours; }

        // {goal_what} in its words
        internal static void Values(NpcActor a, Player to, Dictionary<string, string> v) { Goal g = Of(a); string w = g != null ? Kinds[g.Kind].What(a, g) : null; if (w != null) v["goal_what"] = w; }

        // now and then a word of it (NpcVoice)
        private static readonly Dictionary<NpcActor, float> NextWord = new Dictionary<NpcActor, float>(); // session
        internal static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            float at;
            if (NextWord.TryGetValue(a, out at) && Time.time < at) return;
            Goal g = Of(a);
            if (g == null || !NpcPhrases.Has(a, "goal_" + g.Kind)) return;
            if (NextWord.Count > 128) NextWord.Clear();
            NextWord[a] = Time.time + UnityEngine.Random.Range(400f, 900f);
            themes.Add(new KeyValuePair<string, float>("goal_" + g.Kind, 25f));
        }

        // ---------------------------------------------------------------- in a talk (the game of the one who talks)
        private static string _thanks = "";
        private static readonly HashSet<string> Gave = new HashSet<string>(); // session: «seed@since» given here, its owner not yet heard from: never paid twice
        private static string GaveKey(NpcActor a, Goal g) { return NpcKit.Seed(a.Zdo).ToString(CultureInfo.InvariantCulture) + "@" + g.Since.ToString(CultureInfo.InvariantCulture); }
        private static bool CanGive(Player p, NpcActor a)
        {
            Goal g = Of(a);
            if (p == null || g == null || g.Kind != "thing" || Gave.Contains(GaveKey(a, g))) return false;
            GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g.Arg) : null;
            ItemDrop d = pf != null ? pf.GetComponent<ItemDrop>() : null;
            return d != null && p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) > 0;
        }

        internal static void Register()
        {
            StoryCond.Register("hasgoal", new StoryCond.Kind { Usage = "hasgoal   (людина, з якою говориш, має свою мету)", Test = (p, w) => Of(NpcTalk.Him) != null });
            StoryCond.Register("goalitem", new StoryCond.Kind { Usage = "goalitem   (у тебе є річ, якої вона шукає)", Test = (p, w) => CanGive(p, NpcTalk.Him) });
            NpcTalk.Words["{goal}"] = (a, p) =>
            {
                Goal g = Of(a);
                if (g == null) return a.Form(Lang.T("Та ні про що особливо."));
                return NpcPhrases.Compose(a, "goal_" + g.Kind, p, Extra(a, g)) ?? Kinds[g.Kind].Card(a, g);
            };
            NpcTalk.Words["{goal_thanks}"] = (a, p) => _thanks;
            NpcTalk.Deeds["goalshow"] = (a, p, w) => { Goal g = Of(a); Vector3? at = g != null && Kinds[g.Kind].Spot != null ? Kinds[g.Kind].Spot(a, g) : null; if (at != null) NpcMenu.ShowPlace(at.Value, null, a.Form(Kinds[g.Kind].Card(a, g))); }; // (the place it wants: on the screen and the map)
            NpcTalk.Deeds["goalgive"] = (a, p, w) =>
            {
                _thanks = a.Form(Lang.T("Ой… а де ж воно? Нема."));
                if (!CanGive(p, a)) return;
                Goal g = Of(a);
                ItemDrop d = ObjectDB.instance.GetItemPrefab(g.Arg).GetComponent<ItemDrop>();
                if (Gave.Count > 200) Gave.Clear();
                Gave.Add(GaveKey(a, g));
                p.GetInventory().RemoveItem(d.m_itemData.m_shared.m_name, 1);
                int coins = Mathf.Max(Mathf.RoundToInt(ThingLeast), Mathf.RoundToInt(NpcRequests.WorthOf(g.Arg) * ThingPay)); // (by what it is worth, over a request's pay: it is its own heart's thing)
                Errands.Give(p, ObjectDB.instance.GetItemPrefab("Coins"), coins);
                _thanks = (NpcPhrases.Compose(a, "goal_done", p, Extra(a, g)) ?? a.Form(Lang.T("Збулося! Я й не вірив|вірила."))) + " " + Lang.F("Тримай: {0}.", NpcRequests.Words("Coins", coins));
                GivenBy(a, p); // (its owner: come true, with you as the giver)
            };
        }

        // «goalgiven <player>» where it is run: only a wish a player can bring about, only from that player's game
        private static void Given(NpcActor a, string[] w)
        {
            long id;
            if (!ForYou(a) || w.Length < 2 || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || !NpcActor.SentBy(id)) return;
            Came(a, Of(a), id);
        }

        // the console: raven npc goal <who> [kind|done|clear]
        internal static string Cmd(NpcActor a, string word)
        {
            if (!a.Owned) return NpcKit.Busy(a) ?? "Його веде інша гра.";
            Goal g = Of(a);
            if (word == "clear") { Write(a, null); NpcLocker.Set(a, KGoalNext, "1"); return a.Name + ": мету знято (нова — при наступному погляді)."; }
            if (word == "done") { if (g == null) return a.Name + ": мети нема."; Came(a, g, 0L); return a.Name + ": мета збулася."; }
            if (word.Length > 0) { if (!Kinds.ContainsKey(word)) return "raven npc goal <хто> " + string.Join("|", new List<string>(Kinds.Keys).ToArray()) + "|done|clear"; Write(a, null); string got = Choose(a, word); return got != null ? a.Name + ": " + Card(a) : a.Name + ": «" + word + "» зараз не підходить."; }
            if (g == null) return a.Name + ": мети нема; наступна — через " + Mathf.Max(0f, (Long(a, KGoalNext) - (float)NpcRescue.Now) / 60f).ToString("0") + " хв гри.";
            Kind k = Kinds[g.Kind];
            return a.Name + ": " + k.Card(a, g) + " · від " + NpcMemory.Ago(g.Since) + (k.Done != null ? (k.Done(a, g) ? " · уже збулося" : " · ще ні") : " · чекає на подарунок") + " · настрій " + Mood(a).ToString("+0;-0;0");
        }

        internal static void Wire()
        {
            Register();
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcActor.Acts["goalgiven"] = Given;
            NpcActor.Acts["goalbuilt"] = MadeBy;
            NpcToil.MoodFrom.Add(Mood);
            NpcPlaces.PersonWorth.Add(Toward);
            NpcProfile.More.Add(Card);
            NpcPhrases.ValueSources.Add(Values);
            NpcVoice.Sources.Add(Themes);
            Npcs.Commands["goal"] = (p, who, a, aim) => Cmd(who, a.Length > 3 ? a[3] : "");
        }
    }
}
#endif

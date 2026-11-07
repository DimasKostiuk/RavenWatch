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
    //   bed      its own bed (a resident with none)                        true when it is given one
    //   master   to master its work (a worker below ×1 of its craft)     true when its craft is full (NpcToil.Craft)
    //   comfort  a thing at its base: a beehive, a fermenter, a trophy,  true when the base has it (one more seat: more than at
    //            a cooking station, one more seat (only what is not there)  the start) — NpcBase.Things, no physics
    //   friend   a neighbour it gets on with (NpcAttitude.Fit ≥ FriendFit) true when it is close to it (≥ FriendAt)
    //   thing    a thing from the life before (lands reached: NpcRequests.Tier) true when a player gives it (its talk: ?goalitem
    //                                                                       !goalgive) — paid better than a request
    // Kept as one short string in its locker (NpcLocker: «kind|arg|since|base»), its next wish's time and its last come-true there too:
    // a reload, a new owner go on with it. Chosen and checked where it is run (NpcActor.Ticks): a look every CheckEvery s a person, a
    // few lists counted; a new wish only after GapMin…GapMax game days. In a talk (NpcTalk): ?hasgoal, ?goalitem, {goal}, {goal_thanks},
    // !goalgive. Console: raven npc goal <who> [kind|done|clear].
    internal static class NpcGoals
    {
        internal static readonly int KGoal = "RavenWatch_npcGoal".GetStableHashCode();         // string «kind|arg|since|base» ("" none) — in its locker
        internal static readonly int KGoalNext = "RavenWatch_npcGoalNext".GetStableHashCode(); // string: world seconds of its next wish — in its locker
        internal static readonly int KGoalDone = "RavenWatch_npcGoalDone".GetStableHashCode(); // string: world seconds its last came true — in its locker
        internal static float CheckEvery = 60f, FirstDays = 1f, GapMin = 2f, GapMax = 4f, ProudDays = 3f, LongDays = 6f, GiveUpDays = 15f;
        internal static float FriendFit = 65f, FriendAt = 60f, FriendWorth = 0.5f, Proud = 8f, Pining = -3f;

        internal sealed class Goal { public string Kind = "", Arg = ""; public long Since; public int Base; }

        internal sealed class Kind
        {
            public string Id = "";
            public Func<NpcActor, Goal> Pick;                 // a wish of this kind it could have now (null: none)
            public Func<NpcActor, Goal, bool> Done;           // has it come true by itself (null: only by a gift)
            public Func<NpcActor, Goal, string> What;         // {goal_what}: what it wants, in words (null: none fits now)
            public Func<NpcActor, Goal, string> Card;         // its card «Вдача»
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

        // a thing from the life before: [item, the lands it needs (NpcRequests.Tier)] — a keepsake, not a resource
        private static readonly string[][] Keepsakes = { new[] { "Honey", "0" }, new[] { "TrophyDeer", "0" }, new[] { "Thistle", "1" }, new[] { "Amber", "1" }, new[] { "AmberPearl", "2" }, new[] { "SilverNecklace", "3" }, new[] { "Ruby", "5" } };

        static NpcGoals()
        {
            K(new Kind
            {
                Id = "bed",
                Pick = a => NpcLocker.HasRef(a.Zdo, NpcResident.KBed) ? null : new Goal(),
                Done = (a, g) => NpcLocker.HasRef(a.Zdo, NpcResident.KBed),
                What = (a, g) => "",
                Card = (a, g) => Lang.T("мріє про своє ліжко"),
            });
            K(new Kind
            {
                Id = "master",
                Pick = a => a.Worker != null && a.Worker.Works && NpcToil.Craft(a, a.Worker.JobId) < 0.95f ? new Goal { Arg = a.Worker.JobId } : null,
                Done = (a, g) => NpcToil.Craft(a, g.Arg) >= 0.99f,
                What = (a, g) => Jobs.Word(g.Arg),
                Card = (a, g) => Lang.F("хоче стати майстром: {0}", Jobs.Word(g.Arg)),
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
                    List<string> can = new List<string>();
                    foreach (string[] k in Keepsakes) if (int.Parse(k[1], CultureInfo.InvariantCulture) <= tier && ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(k[0]) != null) can.Add(k[0]);
                    return can.Count > 0 ? new Goal { Arg = can[(int)(NpcKit.Seed(a.Zdo) % (uint)can.Count)] } : null; // (its own keepsake: the same one every time it wishes for one)
                },
                Done = null,
                What = (a, g) => NpcRequests.Words(g.Arg, 1),
                Card = (a, g) => Lang.F("шукає річ із минулого: {0}", NpcRequests.Words(g.Arg, 1)),
            });
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
            if (a.Def == null || !a.Def.Generated || !NpcResident.Is(a) || NpcLocker.Get(a, KGoal) == null) return; // (its locker not here yet)
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
            if (k.Done != null && k.Done(a, g)) { Came(a, g, null); return; }
            if (now - g.Since <= GiveUpDays * Day) return;
            Write(a, null);
            NpcLocker.Set(a, KGoalNext, (now + (long)(GapMin * Day)).ToString(CultureInfo.InvariantCulture));
            Npcs.Log(a.Id + ": its wish «" + g.Kind + "» never came: let go");
        }

        internal static void Forget(NpcActor a) { Next.Remove(a); }

        // a new wish: one of the kinds it could have now, at random (kind: that one — the console)
        private static string Choose(NpcActor a, string kind)
        {
            List<Goal> can = new List<Goal>();
            foreach (Kind k in Kinds.Values)
            {
                if (kind != null && k.Id != kind) continue;
                Goal g = k.Pick(a);
                if (g != null) { g.Kind = k.Id; can.Add(g); }
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

        // come true: glad, a line, remembered (giver: the player who gave it — warmer to him), a new wish later
        private static void Came(NpcActor a, Goal g, Player giver)
        {
            Write(a, null);
            long now = (long)NpcRescue.Now;
            NpcLocker.Set(a, KGoalDone, now.ToString(CultureInfo.InvariantCulture));
            NpcLocker.Set(a, KGoalNext, (now + (long)(UnityEngine.Random.Range(GapMin, GapMax) * Day)).ToString(CultureInfo.InvariantCulture));
            string what = Kinds[g.Kind].What(a, g) ?? "";
            NpcMemory.Remember(a, "goal_done", giver != null ? giver.GetPlayerID() : 0L, what);
            NpcFeel.Feel(a, NpcFeel.Joy, 120f, a.transform.position, false);
            NpcPhrases.SayOr(a, "goal_done", a.Form(Lang.T("Збулося! Я й не вірив|вірила.")), Extra(a, g));
            Npcs.Log(a.Id + ": its wish «" + g.Kind + "» came true" + (giver != null ? " (" + giver.GetPlayerName() + ")" : ""));
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
            return g != null && now - g.Since > LongDays * Day ? Pining : 0f;
        }

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

        // a wish a player can bring about (a heart's board «Справи»): a bed, a thing built, a thing brought; not its craft or a friend
        internal static bool ForYou(NpcActor a) { Goal g = Of(a); return g != null && (g.Kind == "bed" || g.Kind == "comfort" || g.Kind == "thing"); }

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
        private static bool CanGive(Player p, NpcActor a)
        {
            Goal g = Of(a);
            if (p == null || g == null || g.Kind != "thing") return false;
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
            NpcTalk.Deeds["goalgive"] = (a, p, w) =>
            {
                _thanks = a.Form(Lang.T("Ой… а де ж воно? Нема."));
                if (!CanGive(p, a)) return;
                Goal g = Of(a);
                ItemDrop d = ObjectDB.instance.GetItemPrefab(g.Arg).GetComponent<ItemDrop>();
                p.GetInventory().RemoveItem(d.m_itemData.m_shared.m_name, 1);
                int coins = 25 * (NpcRequests.Tier() + 1); // (more than a request pays: it is its own heart's thing)
                Errands.Give(p, ObjectDB.instance.GetItemPrefab("Coins"), coins);
                _thanks = (NpcPhrases.Compose(a, "goal_done", p, Extra(a, g)) ?? a.Form(Lang.T("Збулося! Я й не вірив|вірила."))) + " " + Lang.F("Тримай: {0}.", NpcRequests.Words("Coins", coins));
                a.Act("goalgiven " + p.GetPlayerID().ToString(CultureInfo.InvariantCulture)); // (its owner: come true, with you as the giver)
            };
        }

        // «goalgiven <player>» where it is run
        private static void Given(NpcActor a, string[] w)
        {
            Goal g = Of(a);
            if (g == null || g.Kind != "thing") return;
            long id = 0L; if (w.Length > 1) long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
            Player giver = null; foreach (Player p in Player.GetAllPlayers()) if (p.GetPlayerID() == id) giver = p;
            Came(a, g, giver);
        }

        // the console: raven npc goal <who> [kind|done|clear]
        internal static string Cmd(NpcActor a, string word)
        {
            if (!a.Owned) return NpcKit.Busy(a) ?? "Його веде інша гра.";
            Goal g = Of(a);
            if (word == "clear") { Write(a, null); NpcLocker.Set(a, KGoalNext, "1"); return a.Name + ": мету знято (нова — при наступному погляді)."; }
            if (word == "done") { if (g == null) return a.Name + ": мети нема."; Came(a, g, null); return a.Name + ": мета збулася."; }
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

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // What you promised a person in its talk (the user 2026-11-05: answers have consequences): «Принесу», «Я подумаю, як допомогти», «Буде їжа», «Заплачу» are words it keeps.
    // Food and pay (NoBlame, the user 2026-11-07): who filled a chest cannot be told, so these alone are kept by what passes (it ate, it was paid), may be promised only while there is truly nothing (May), and past the term are dropped without blame.
    // Kept, they are only glad of it in words (promise_eased_<kind>): nothing remembered of you, no warmth, no thread — whose hand it was is not known, and food taken out and put back was a kept word every day.
    // A promise has a kind (Kinds: how to tell it is kept, whether there is anything to promise, when it no longer means anything) and a term (Days, or its own Due:
    // a request's end).
    // From half the term it reminds you: a worry of its own, themes promise_nag_<kind>, answers in Talk (you may take it back there: a small loss, no broken
    // word). Kept: warmer to you, and its people say so to you (memories promise_kept, heard_kept). Past the term: colder, and they say that too (promise_broken,
    // heard_broken). What its people heard is told only to you: it is about you, and «they say X keeps its word» is an odd thing to hear of yourself.
    // One a kind and a player, in its locker: a reload, a night away, a new owner go on with it.
    // Nothing keeps itself (the user 2026-11-05): a word is kept only by what that player did (Did: what it built — Built, Diary.Placed, the kind's Deed; its word to the other one — NpcArcs;
    // what it brought). What is promised and passes by itself, or by another's hand, is dropped without a word (Void): no thanks, no blame. Nor is anything promised where you could not do it (May).
    //   in a talk   !promise <kind>  ·  !unpromise <kind>  ·  ?promised <kind>
    //   in code     NpcPromises.Kinds (a new kind: a new entry)  ·  console: raven npc promise <who> [kind | clear]
    // A look every LookEvery s where it is run, its locker's one string read; nothing more while there is no promise.
    internal static class NpcPromises
    {
        internal static readonly int KPromise = "RavenWatch_npcPromise".GetStableHashCode(); // string "player|kind|arg|since;…" — in its locker
        internal static float LookEvery = 20f, TakenBack = -2f, NagAsked = 58f, NagSaid = 30f, MoodFine = 45f;

        internal sealed class Promise { public long Player, Since; public string Sort = "", Arg = ""; public bool Did; }
        internal sealed class Kind
        {
            public float Days = 3f;                     // its term, game days
            public Func<NpcActor, Promise, bool> Kept;  // done
            public Func<NpcActor, Promise, bool> Void;  // nothing to keep any more, no one's fault: dropped without a word
            public Func<NpcActor, bool> May;            // is there anything to promise now (none: the words stay words)
            public Func<NpcActor, string> ArgNow;       // what it was at when you promised
            public Func<NpcActor, Promise, double> Due; // its own end (world s), when the thing promised has one; else Since + Days
            public Func<NpcActor, Promise, Piece, bool> Deed; // this thing you just built is what you promised (Built): Did
            public bool NoBlame;                        // past its term: dropped without a word, not a broken word
        }

        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "request", new Kind { ArgNow = a => NpcRequests.UntilOf(a).ToString(CultureInfo.InvariantCulture), Due = (a, x) => { long u; return long.TryParse(x.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out u) && u > x.Since ? u : x.Since + 3.0 * Day; }, Kept = (a, x) => Since(NpcMemory.Find(a, "request_done", x.Player), x), Void = (a, x) => { long u; return NpcRequests.UntilOf(a).ToString(CultureInfo.InvariantCulture) != x.Arg && long.TryParse(x.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out u) && NpcRescue.Now < u; } } }, // what it asked for: brought by you before its request runs out; gone before its end (another brought it, it was let go): nothing to keep
            { "goal",    new Kind { Days = 8f, May = NpcGoals.ForYou, ArgNow = GoalKind, // its long wish (NpcGoals: long by nature), of the kinds a player can bring about: come true from your hand (NpcGoals names the giver: given, built, hung), or a bed you built and it got; come true otherwise, or let go: nothing to keep
                Deed = (a, x, made) => x.Arg == "bed" && made.GetComponent<Bed>() != null,
                Kept = (a, x) => Since(NpcMemory.Find(a, "goal_done", x.Player), x) || (x.Did && Since(NpcMemory.Find(a, "goal_done", 0L), x)),
                Void = (a, x) => GoalKind(a) != x.Arg || Since(NpcMemory.Find(a, "goal_done", 0L), x) } },
            { "mood",    new Kind { May = a => NpcToil.Mood(a) < MoodFine && Yours(a), ArgNow = a => WhyNow(a).ToString(CultureInfo.InvariantCulture), // what weighs on it, if it is what you can build (Yours): you built it, and that no longer weighs most; gone without you: nothing to keep
                Deed = MoodDeed,
                Kept = (a, x) => x.Did && CauseGone(a, x),
                Void = (a, x) => !x.Did && CauseGone(a, x) } },
            { "arc",     new Kind { ArgNow = NpcArcs.StageOf, Kept = (a, x) => x.Did, Void = (a, x) => NpcArcs.StageOf(a) != x.Arg } }, // a small line's word (NpcArcs): you spoke to the other one (Did); the line moved on without you, or is over: nothing to keep
            { "food",    new Kind { Days = 1.5f, NoBlame = true, May = a => NpcHunger.IsHungry(a) && NpcMeals.NothingToEat(a), Kept = (a, x) => NpcResident.Is(a) && !NpcHunger.IsHungry(a), Void = (a, x) => !NpcResident.Is(a) } }, // hungry with nothing in the base to eat: it has eaten; it no longer lives there: nothing to keep
            { "pay",     new Kind { Days = NpcToil.PayDays + 1f, NoBlame = true, May = a => NpcToil.Unpaid(a) >= 1 && BoxShort(a), Kept = (a, x) => NpcResident.Is(a) && NpcToil.Unpaid(a) == 0, Void = (a, x) => !NpcResident.Is(a) } }, // unpaid, the coin box short of its wage: paid at last; it no longer lives there: nothing to keep
        };

        private static bool BoxShort(NpcActor a)
        {
            GameObject go = NpcKit.Find(NpcKit.GetRef(a.Zdo, NpcResident.KHeart));
            NpcHeart h = go != null ? go.GetComponent<NpcHeart>() : null;
            return h != null && h.Coins < NpcToil.Wage(a);
        }

        private static int WhyNow(NpcActor a) { return a.Zdo != null ? NpcLocker.Int(a.Zdo, NpcToil.KWhy, 0) : 0; }

        private static string GoalKind(NpcActor a) { NpcGoals.Goal g = NpcGoals.Of(a); return g != null ? g.Kind : ""; }

        // what you can be seen to mend: a bed, a roof over it, a fire — built. Hunger, tiredness, the rain pass by themselves, and who filled the coin box or the pantry cannot be told: a word kept by nobody is no word
        private static bool Yours(NpcActor a)
        {
            switch ((NpcToil.Why)WhyNow(a))
            {
                case NpcToil.Why.NoBed: case NpcToil.Why.BedOpen: case NpcToil.Why.NoFire: return true;
                default: return false;
            }
        }

        private static NpcToil.Why WhyOf(Promise x) { int why; return int.TryParse(x.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out why) ? (NpcToil.Why)why : NpcToil.Why.None; }

        // what you built is for what weighs on it: a bed, a fire in its base, something by its bed for the roof it lacks
        internal static float RoofNear = 12f;
        private static bool MoodDeed(NpcActor a, Promise x, Piece made)
        {
            switch (WhyOf(x))
            {
                case NpcToil.Why.NoBed: return made.GetComponent<Bed>() != null;
                case NpcToil.Why.NoFire: return made.GetComponent<Fireplace>() != null;
                case NpcToil.Why.BedOpen:
                    GameObject bed = NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed));
                    return bed != null && made.GetComponent<WearNTear>() != null && Utils.DistanceXZ(bed.transform.position, made.transform.position) <= RoofNear;
                default: return false;
            }
        }

        // that very thing no longer lacks — asked of the thing itself, as NpcToil.Weigh does: what weighs most may change for another reason (it got hungry) with the bed still not there
        private static bool CauseGone(NpcActor a, Promise x)
        {
            switch (WhyOf(x))
            {
                case NpcToil.Why.NoBed: return NpcLocker.HasRef(a.Zdo, NpcResident.KBed);
                case NpcToil.Why.NoFire: return NpcPlaces.AnyFire(a); // (its base's list: every LookEvery s, only while such a promise stands)
                case NpcToil.Why.BedOpen:
                    GameObject bed = NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed));
                    return bed == null ? !NpcLocker.HasRef(a.Zdo, NpcResident.KBed) : NpcToil.Roof(bed.transform.position + Vector3.up * 0.5f); // heavy: every LookEvery s, only while such a promise stands; its bed gone: nothing to cover
                default: return true; // (nothing it can be told by)
            }
        }

        // ---------------------------------------------------------------- what you did
        // you built this (Diary.Placed, on your game): the people round it whom you promised just that. A look at each one's short string, once a thing built
        internal static void Built(Player p, Piece made)
        {
            if (p == null || made == null) return;
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || Utils.DistanceXZ(NpcRules.Home(a), made.transform.position) > NpcBase.Reach) continue;
                Given(a, p, made);
            }
        }

        // this very thing you gave it (NpcResident.GiveBed) or built by it: what you promised it, if it is
        internal static void Given(NpcActor a, Player p, Piece made)
        {
            if (a == null || p == null || made == null) return;
            string s = NpcLocker.Get(a, KPromise);
            if (string.IsNullOrEmpty(s)) return;
            long me = p.GetPlayerID();
            foreach (Promise x in Parse(s))
            {
                Kind k;
                if (x.Player == me && !x.Did && Kinds.TryGetValue(x.Sort, out k) && k.Deed != null && k.Deed(a, x, made)) Did(a, p, x.Sort);
            }
        }

        // you did what you promised (NpcArcs: you spoke to the other one; Built): none promised, nothing
        internal static void Did(NpcActor a, Player p, string kind)
        {
            if (a != null && p != null) a.Act("promisedid " + Id(p.GetPlayerID()) + " " + kind);
        }

        // what you promised it and how long is left (a heart's board «Справи»): words, seconds left; a word you already did what it took for is not listed
        internal static List<KeyValuePair<string, long>> Yours(NpcActor a, Player p)
        {
            List<KeyValuePair<string, long>> l = new List<KeyValuePair<string, long>>();
            string s = a != null && p != null ? NpcLocker.Get(a, KPromise) : null;
            if (string.IsNullOrEmpty(s)) return l;
            foreach (Promise x in Parse(s))
            {
                Kind k;
                if (x.Player != p.GetPlayerID() || x.Did || !Kinds.TryGetValue(x.Sort, out k)) continue;
                NpcRequests.Request r = x.Sort == "request" ? NpcRequests.Of(a) : null;
                string what = x.Sort == "request" ? (r != null ? Lang.F("принести: {0}", NpcRequests.Words(r.Item, r.Count)) : Lang.T("принести те, що просив|просила"))
                    : x.Sort == "goal" ? Lang.T("допомогти з мрією") : x.Sort == "mood" ? Lang.T("зробити щось, щоб полегшало") : x.Sort == "food" ? Lang.T("щоб у коморі була їжа") : x.Sort == "pay" ? Lang.T("поповнити скарбничку") : Lang.T("сказати за нього|неї слово");
                l.Add(new KeyValuePair<string, long>(a.Form(what), (long)(End(k, a, x) - NpcRescue.Now)));
            }
            return l;
        }

        private static bool Since(NpcMemory.Entry e, Promise x) { return e != null && e.At >= x.Since; }
        private static double End(Kind k, NpcActor a, Promise x) { return k.Due != null ? k.Due(a, x) : x.Since + k.Days * Day; }
        private static float Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }
        private static string Id(long v) { return v.ToString(CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- kept in its locker
        private static List<Promise> Parse(string s)
        {
            List<Promise> l = new List<Promise>();
            foreach (string e in s.Split(';'))
            {
                string[] w = e.Split('|');
                Promise x = new Promise();
                if (w.Length < 4 || !long.TryParse(w[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Player) || !long.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Since)) continue;
                x.Sort = w[1]; x.Arg = w[2]; x.Did = w.Length > 4 && w[4] == "did";
                l.Add(x);
            }
            return l;
        }

        private static void Write(NpcActor a, List<Promise> l)
        {
            List<string> parts = new List<string>();
            foreach (Promise x in l) parts.Add(Id(x.Player) + "|" + x.Sort + "|" + x.Arg + "|" + Id(x.Since) + (x.Did ? "|did" : ""));
            NpcLocker.Set(a, KPromise, string.Join(";", parts.ToArray()));
        }

        internal static bool Has(NpcActor a, long player, string kind)
        {
            string s = NpcLocker.Get(a, KPromise);
            return !string.IsNullOrEmpty(s) && Parse(s).Exists(x => x.Player == player && x.Sort == kind);
        }

        // «promise <player> <kind>» · «unpromise <player> <kind>» · «promisedid <player> <kind>» where it is run
        private static void Act(NpcActor a, string[] w)
        {
            long who;
            if (w.Length < 3 || !a.Owned || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out who) || !NpcActor.SentBy(who)) return;
            string s = NpcLocker.Get(a, KPromise);
            if (s == null) return; // (its locker not here yet)
            List<Promise> l = Parse(s);
            if (w[0] == "promisedid")
            {
                Promise done = l.Find(x => x.Player == who && x.Sort == w[2]);
                if (done != null && !done.Did) { done.Did = true; Write(a, l); Npcs.Log(a.Id + ": you did what you promised (" + w[2] + ", " + who + ")"); }
                return;
            }
            if (w[0] == "unpromise")
            {
                if (l.RemoveAll(x => x.Player == who && x.Sort == w[2]) == 0) return;
                Write(a, l);
                NpcAttitude.Add(a, who, TakenBack);
                return;
            }
            Kind k;
            if (!Kinds.TryGetValue(w[2], out k) || (k.May != null && !k.May(a)) || l.Exists(x => x.Player == who && x.Sort == w[2])) return; // (promised already: the same word, its term stands)
            l.Add(new Promise { Player = who, Sort = w[2], Arg = k.ArgNow != null ? k.ArgNow(a) : "", Since = (long)NpcRescue.Now });
            Write(a, l);
            Npcs.Log(a.Id + ": you promised (" + w[2] + ", " + who + ")");
        }

        // ---------------------------------------------------------------- kept, or not (where it is run, NpcActor.Ticks)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: when it looks next
        internal static void Tick(NpcActor a)
        {
            float at;
            if (Next.TryGetValue(a, out at) && Time.time < at) return;
            Next[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            string s = NpcLocker.Get(a, KPromise);
            if (string.IsNullOrEmpty(s)) return;
            List<Promise> l = Parse(s);
            bool changed = false;
            foreach (Promise x in l.ToArray())
            {
                Kind k;
                if (!Kinds.TryGetValue(x.Sort, out k)) { l.Remove(x); changed = true; continue; }
                bool kept = k.Kept(a, x);
                if (!kept && k.Void != null && k.Void(a, x)) { l.Remove(x); changed = true; continue; } // (it passed without you: no one's word)
                if (!kept && NpcRescue.Now <= End(k, a, x)) continue;
                l.Remove(x); changed = true;
                if (!kept && (x.Did || k.NoBlame)) continue; // (you did it, and it still weighs for another reason; or who filled a chest cannot be told: not a broken word)
                if (k.NoBlame) // (kept, but by whom cannot be told: glad of it in words, nothing remembered of you — else food taken out and put back is a kept word, warmth and a thread, every day)
                {
                    Player by = Player.GetClosestPlayer(a.transform.position, 20f);
                    if (by != null && by.GetPlayerID() == x.Player) NpcPhrases.SayOr(a, "promise_eased_" + x.Sort, Lang.T("Ну от, полегшало."));
                    Npcs.Log(a.Id + ": your promise (" + x.Sort + ") came about (by whom: unknown — nothing remembered)");
                    continue;
                }
                NpcMemory.Remember(a, kept ? "promise_kept" : "promise_broken", x.Player, "");
                NpcMemory.TellHome(a, kept ? "heard_kept" : "heard_broken", x.Player, ""); // (its people hear what your word is worth)
                Player near = kept ? Player.GetClosestPlayer(a.transform.position, 20f) : null;
                if (near != null && near.GetPlayerID() == x.Player) NpcPhrases.SayOr(a, "promise_kept_now", Lang.T("Слова дотримано. Я не забуду."));
                Npcs.Log(a.Id + ": your promise (" + x.Sort + ") " + (kept ? "kept" : "broken"));
            }
            if (changed) Write(a, l);
        }

        internal static void Forget(NpcActor a) { Next.Remove(a); }

        // it reminds you (NpcVoice: a worry of its own, the talk's first words too)
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            string s = NpcLocker.Get(a, KPromise);
            if (string.IsNullOrEmpty(s)) return;
            foreach (Promise x in Parse(s))
            {
                Kind k;
                if (x.Player == p.GetPlayerID() && !x.Did && Kinds.TryGetValue(x.Sort, out k) && NpcRescue.Now > (x.Since + End(k, a, x)) / 2.0) themes.Add(new KeyValuePair<string, float>("promise_nag_" + x.Sort, NpcVoice.Asked ? NagAsked : NagSaid));
            }
        }

        // what you promised it, in its menu (NpcMenu.Tasks): a request's promise is its request's own line, a small line's is said by NpcArcs (it knows with whom)
        private static void Tasks(NpcActor a, Player p, List<string> l)
        {
            string s = NpcLocker.Get(a, KPromise);
            if (string.IsNullOrEmpty(s)) return;
            foreach (Promise x in Parse(s))
            {
                if (x.Player != p.GetPlayerID() || x.Did) continue;
                if (x.Sort == "goal") l.Add(Grammar.Fix(Lang.T("ти обіця{в|ла}: допомогти з мрією"), p));
                else if (x.Sort == "mood") l.Add(Grammar.Fix(Lang.T("ти обіця{в|ла}: щось зробити, щоб полегшало"), p));
                else if (x.Sort == "food") l.Add(Grammar.Fix(Lang.T("ти обіця{в|ла}: щоб у коморі була їжа"), p));
                else if (x.Sort == "pay") l.Add(Grammar.Fix(Lang.T("ти обіця{в|ла}: поповнити скарбничку"), p));
            }
        }

        // ---------------------------------------------------------------- its talk: what you answer to a reminder
        private const string Busy = "> Мені зараз не до того. => end !tone cold\n";
        private static readonly string[] TalkWords = Lang.Keys("Пам'ятаю. Заплачу.", "Мені зараз не до того."); // (answers are plain text in Talk: marked here for tools/l10n.js)
        private const string Talk =
            "[talk common +]\n" +
            "@c_promise_nag_request\n{concern}\n> Пам'ятаю. Принесу. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise request !patience -2\n" + Busy +
            "@c_promise_nag_goal\n{concern}\n> Пам'ятаю. Ще думаю. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise goal !patience -2\n" + Busy +
            "@c_promise_nag_mood\n{concern}\n> Пам'ятаю. Зроблю. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise mood !patience -2\n" + Busy +
            "@c_promise_nag_arc\n{concern}\n> Пам'ятаю. Ще не було нагоди. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise arc !patience -2\n" + Busy +
            "@c_promise_nag_food\n{concern}\n> Пам'ятаю. Принесу. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise food !patience -2\n" + Busy +
            "@c_promise_nag_pay\n{concern}\n> Пам'ятаю. Заплачу. => c_heard !patience 2\n> Не вийшло. Вибач. => c_released !unpromise pay !patience -2\n" + Busy + // (an answer's words for the English table: TalkWords)
            "@c_released\n{@promise_released}\n> {@say_ok} => start\n";

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            NpcActor.Acts["promise"] = Act;
            NpcActor.Acts["unpromise"] = Act;
            NpcActor.Acts["promisedid"] = Act;
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcVoice.Sources.Add(Themes);
            NpcTalk.More.Add(Talk);
            NpcMenu.Tasks.Add(Tasks);
            NpcTalk.Deeds["promise"] = (a, p, w) => { if (w.Length < 2) return; a.Act("promise " + Id(p.GetPlayerID()) + " " + w[1]); if (NpcThreads.Has(a, p.GetPlayerID(), "word")) { NpcPatience.Spend(a, p, -NpcThreads.WordEases); RavenBook.TalkNote(a.Form(Lang.F("{0}: вірить тобі на слово", a.Name)), true); } }; // (the thread «слово»: NpcThreads)
            NpcTalk.Deeds["unpromise"] = (a, p, w) => { if (w.Length > 1) a.Act("unpromise " + Id(p.GetPlayerID()) + " " + w[1]); };
            Npcs.Commands["promise"] = (p, who, a, aim) =>
            {
                if (a.Length > 3 && a[3] == "clear") { NpcLocker.Set(who, KPromise, ""); return "Обіцянки знято."; }
                if (a.Length > 3) { who.Act("promise " + Id(p.GetPlayerID()) + " " + a[3]); return "Сказано."; }
                return who.Name + ": " + (NpcLocker.Get(who, KPromise) ?? "шафка ще не тут");
            };
        }

        internal static void Register()
        {
            StoryCond.Register("promised", new StoryCond.Kind { Usage = "promised <вид>   (ти йому це пообіцяв: request, goal, mood, arc, food, pay)", Min = 1, Test = (p, w) => NpcTalk.Him != null && p != null && Has(NpcTalk.Him, p.GetPlayerID(), w[1]) });
        }
    }
}
#endif

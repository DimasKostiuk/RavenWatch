#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Threads between a person and a player (the user 2026-11-07: not an abstract number, something you can see and that does something): deeds of a kind, kept apart from
    // NpcAttitude (the number still leads its words and how warm it is). A thread does not fade as a memory does; a betrayal cuts it, and it stays written as cut.
    //   Kinds   one row each: which memories tie it (From, Need: how many times), which cut it (Cuts), which tie it again only once it was cut (Mends: no thread out of a mere apology). Nothing else knows them: NpcMemory says what it took in (Taken), this
    //           looks it up, so a deed of any module ties a thread by being remembered. A new thread: a new row
    //   Gifts   what a thread opens, one row each: its thread, how often, Try (where the person is run, a player near: true if it did it). A new gift: a new row
    //   Seen    its card «Думки» (NpcProfile.Rows), the talk scroll's rope (Knots: RavenBookTalkRel), «?thread <kind>» in a talk, NpcThreads.Has / State in code
    // Settlers only: they are the ones who remember you (NpcMemory is written for anyone, Taken looks only at settlers). No limit on how many people are close (the user 2026-11-07: for now).
    // Kept in its locker (one short string, written only when a thread changes); read any game, written where it is run. Cheap: a look every LookEvery s where it is run, its one
    // string read, nothing more with no threads. Console: raven npc thread <who> [tie <kind> | cut <kind>].
    internal static class NpcThreads
    {
        internal static readonly int KThreads = "RavenWatch_npcThreads".GetStableHashCode(); // string «player,kind,source,at,c;…» (c: cut) — in its locker
        internal static readonly int KGiven = "RavenWatch_npcThreadGifts".GetStableHashCode(); // string «player,gift,at;…»: when a gift was last given — in its locker
        internal static float LookEvery = 60f, GiftNear = 15f;
        private const int Most = 30; // (the cut and oldest first go)

        internal sealed class Kind { public string Id = "", Word = ""; public string[] From = new string[0], Cuts = new string[0], Mends = new string[0]; public int Need = 1; }
        internal sealed class Strand { public long Player, At; public string Sort = "", Source = ""; public bool Cut; }
        internal sealed class Gift { public string Id = "", Thread = ""; public float Days = 3f; public Func<NpcActor, Player, bool> Try; }

        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "life",  new Kind { Id = "life",  Word = Lang.K("життя"), From = new[] { "rescued", "revived" }, Cuts = new[] { "fired" } } },
            { "word",  new Kind { Id = "word",  Word = Lang.K("слово"), From = new[] { "promise_kept" }, Cuts = new[] { "promise_broken", "fired" } } },
            { "dream", new Kind { Id = "dream", Word = Lang.K("мрія"),  From = new[] { "goal_done" }, Cuts = new[] { "fired" } } },
            { "hand",  new Kind { Id = "hand",  Word = Lang.K("рука"),  From = new[] { "request_done" }, Need = 3, Cuts = new[] { "clothes_taken", "fired" } } },
            { "time",  new Kind { Id = "time",  Word = Lang.K("час"),   From = new[] { "secret_told" }, Cuts = new[] { "insulted", "fired" } } },
        };

        // a module's own memory that ties a thread, or only mends a cut one (NpcTasks: its row says which)
        internal static void TiedBy(string thread, string memory, bool onlyMends)
        {
            Kind k = Kinds[thread];
            List<string> l = new List<string>(onlyMends ? k.Mends : k.From);
            if (!l.Contains(memory)) l.Add(memory);
            if (onlyMends) k.Mends = l.ToArray(); else k.From = l.ToArray();
        }

        internal static readonly List<Gift> Gifts = new List<Gift>
        {
            new Gift { Id = "vouch", Thread = "word", Days = 3f, Try = Vouch },
        };

        // ---------------------------------------------------------------- kept
        private static List<Strand> Parse(string s)
        {
            List<Strand> l = new List<Strand>();
            if (string.IsNullOrEmpty(s)) return l;
            foreach (string e in s.Split(';'))
            {
                string[] w = e.Split(',');
                Strand x = new Strand();
                if (w.Length < 4 || !long.TryParse(w[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Player) || !long.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.At)) continue;
                x.Sort = w[1]; x.Source = w[2]; x.Cut = w.Length > 4 && w[4] == "c";
                l.Add(x);
            }
            return l;
        }

        private static void Write(NpcActor a, List<Strand> l)
        {
            while (l.Count > Most)
            {
                Strand drop = null;
                foreach (Strand x in l) if (drop == null || (x.Cut && !drop.Cut) || (x.Cut == drop.Cut && x.At < drop.At)) drop = x;
                l.Remove(drop);
            }
            List<string> parts = new List<string>();
            foreach (Strand x in l) parts.Add(x.Player.ToString(CultureInfo.InvariantCulture) + "," + x.Sort + "," + x.Source + "," + x.At.ToString(CultureInfo.InvariantCulture) + (x.Cut ? ",c" : ""));
            NpcLocker.Set(a, KThreads, string.Join(";", parts.ToArray()));
        }

        // a thread of this kind holds between it and that player
        internal static bool Has(NpcActor a, long player, string kind) { return State(a, player, kind) > 0; }

        // 1 tied, −1 cut, 0 never was
        internal static int State(NpcActor a, long player, string kind)
        {
            foreach (Strand x in Parse(a != null ? NpcLocker.Get(a, KThreads) : null)) if (x.Player == player && x.Sort == kind) return x.Cut ? -1 : 1;
            return 0;
        }

        // ---------------------------------------------------------------- what a thread changes in your talks (the user 2026-11-08): read by the talk (NpcTalkDoor), the favours
        // (NpcPatience.AskFrom), the promises (NpcPromises); said on its knot's card on the talk's rope (Knots, Does)
        //   life   tied: its door is never shut to you · cut (you sent it off): it shuts sooner (CutDoor)
        //   word   tied: your promise eases it (WordEases) · cut: it does not believe your promises — they go grey
        //   dream  tied: «ще на думці» even when it says little
        //   hand   tied: a favour may be asked sooner (HandEases)
        //   time   tied: it does not hold its worry before yours · cut (you were rude): it insists sooner (CutInsist)
        internal static float CutDoor = 1.3f, WordEases = 4f, HandEases = 15f, CutInsist = 1.3f;

        // what each thread changes, in words (tied · cut; null: nothing)
        private static readonly Dictionary<string, string[]> Does = new Dictionary<string, string[]>
        {
            { "life", new[] { Lang.K("Тобі не відмовить у розмові ніколи."), Lang.K("Зачиняється перед тобою легше.") } },
            { "word", new[] { Lang.K("Твоя обіцянка його|її заспокоює."), Lang.K("Не вірить твоїм обіцянкам.") } },
            { "dream", new[] { Lang.K("Розповідає більше навіть не в настрої."), null } },
            { "hand", new[] { Lang.K("Попросити про щось можна раніше."), null } },
            { "time", new[] { Lang.K("Не вимагає спершу вислухати своє."), Lang.K("Вимагає вислухати своє раніше.") } },
        };

        // the threads between it and this player as knots on the talk's rope (RavenBookTalkRel), in the order of Kinds: its word, what tied or cut it, what it changes
        internal static List<RavenBook.TalkKnot> Knots(NpcActor a, Player p)
        {
            List<RavenBook.TalkKnot> l = new List<RavenBook.TalkKnot>();
            if (a == null || p == null || a.Def == null || !a.Def.Settler) return l;
            List<Strand> mine = Parse(NpcLocker.Get(a, KThreads)).FindAll(x => x.Player == p.GetPlayerID());
            foreach (Kind k in Kinds.Values)
            {
                Strand x = mine.Find(e => e.Sort == k.Id);
                if (x == null) continue;
                string[] does; Does.TryGetValue(k.Id, out does);
                string did = NpcMemory.LineOf(a, p, x.Source) ?? "";
                if (did.Length > 0) did = char.ToUpper(did[0]) + did.Substring(1);
                string what = does != null ? does[x.Cut ? 1 : 0] : null;
                l.Add(new RavenBook.TalkKnot { Word = Lang.T(k.Word), Cut = x.Cut, Deed = did, Does = what != null ? a.Form(Lang.T(what)) : "" });
            }
            return l;
        }

        // ---------------------------------------------------------------- what it remembers ties or cuts (NpcMemory.Taken, where it is run)
        private static void Taken(NpcActor a, string memory, long player, int count)
        {
            if (a.Def == null || !a.Def.Settler) return;
            foreach (Kind k in Kinds.Values)
            {
                if (Array.IndexOf(k.From, memory) >= 0 && count >= k.Need) Set(a, player, k.Id, memory, false);
                else if (Array.IndexOf(k.Cuts, memory) >= 0) Set(a, player, k.Id, memory, true);
                else if (Array.IndexOf(k.Mends, memory) >= 0 && Cut(a, player, k.Id)) Set(a, player, k.Id, memory, false);
            }
        }

        private static bool Cut(NpcActor a, long player, string kind) { foreach (Strand x in Parse(NpcLocker.Get(a, KThreads))) if (x.Player == player && x.Sort == kind) return x.Cut; return false; }

        // tied or cut (a cut one tied again by a new deed; cutting what never was: nothing)
        private static void Set(NpcActor a, long player, string kind, string source, bool cut)
        {
            string s = NpcLocker.Get(a, KThreads);
            if (s == null) return; // (its locker not here yet)
            List<Strand> l = Parse(s);
            Strand x = l.Find(e => e.Player == player && e.Sort == kind);
            if (x == null ? cut : x.Cut == cut) return;
            if (x == null) l.Add(x = new Strand { Player = player, Sort = kind });
            x.Source = source; x.Cut = cut; x.At = (long)NpcRescue.Now;
            Write(a, l);
            Npcs.Log(a.Id + ": thread «" + kind + "» " + (cut ? "cut" : "tied") + " with " + player + " (" + source + ")");
            StoryBus.Raise(cut ? "npc_thread_cut" : "npc_thread_tied", a.Id, 0, a.transform.position);
        }

        // ---------------------------------------------------------------- what a thread opens (where it is run)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        private static void Forget(NpcActor a) { Next.Remove(a); }

        private static void Tick(NpcActor a)
        {
            float at;
            if (Next.TryGetValue(a, out at) && Time.time < at) return;
            Next[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            string s = NpcLocker.Get(a, KThreads);
            if (string.IsNullOrEmpty(s) || !NpcResident.Is(a) || a.Mind == null || a.Mind.NowLayer >= NpcMind.Layer.Fight || !NpcVoice.Free(a)) return;
            Player p = Player.GetClosestPlayer(a.transform.position, GiftNear);
            if (p == null) return;
            long me = p.GetPlayerID();
            foreach (Gift g in Gifts)
            {
                if (!Has(a, me, g.Thread) || (long)NpcRescue.Now - GivenAt(a, me, g.Id) < g.Days * Day || !g.Try(a, p)) continue;
                Stamp(a, me, g.Id);
                Npcs.Log(a.Id + ": gift of its thread: " + g.Id + " (" + me + ")");
                return; // (one a look)
            }
        }

        private static float Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }

        private static long GivenAt(NpcActor a, long player, string gift)
        {
            string s = NpcLocker.Get(a, KGiven);
            if (string.IsNullOrEmpty(s)) return 0L;
            string head = player.ToString(CultureInfo.InvariantCulture) + "," + gift + ",";
            foreach (string e in s.Split(';')) { long v; if (e.StartsWith(head, StringComparison.Ordinal) && long.TryParse(e.Substring(head.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v; }
            return 0L;
        }

        private static void Stamp(NpcActor a, long player, string gift)
        {
            string head = player.ToString(CultureInfo.InvariantCulture) + "," + gift + ",";
            StringBuilder sb = new StringBuilder();
            foreach (string e in (NpcLocker.Get(a, KGiven) ?? "").Split(';')) if (e.Length > 0 && !e.StartsWith(head, StringComparison.Ordinal)) sb.Append(e).Append(';');
            NpcLocker.Set(a, KGiven, sb.Append(head).Append(((long)NpcRescue.Now).ToString(CultureInfo.InvariantCulture)).ToString());
        }

        // «Слово за тебе»: one who has your word stands up for you to a neighbour near it who is cold to you: that one warms (its memory «vouched»)
        internal static float VouchBelow = 0f, VouchNear = 12f;
        private static bool Vouch(NpcActor a, Player p)
        {
            long me = p.GetPlayerID();
            foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart)))
            {
                if (o == null || o == a || o.Def == null || !o.Def.Settler || Utils.DistanceXZ(o.transform.position, a.transform.position) > VouchNear || NpcAttitude.Of(o, me) >= VouchBelow) continue;
                NpcPhrases.SayOr(a, "thread_vouch", a.Form(Lang.F("{0}, годі. За цю людину я ручаюся.", o.Name)), new Dictionary<string, string> { { "other", o.Name } });
                NpcMemory.Tell(o, "vouched", me, a.Key);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- its card
        private static List<RavenBook.SheetRow> Rows(NpcActor a, Player me)
        {
            List<RavenBook.SheetRow> l = new List<RavenBook.SheetRow>();
            if (me == null) return l;
            List<Strand> mine = Parse(NpcLocker.Get(a, KThreads)).FindAll(x => x.Player == me.GetPlayerID());
            if (mine.Count == 0) return l;
            mine.Sort((x, y) => x.Cut != y.Cut ? (x.Cut ? 1 : -1) : y.At.CompareTo(x.At)); // (what holds first)
            l.Add(RavenBook.SheetRow.Label(Lang.T("що вас пов'язує")));
            foreach (Strand x in mine)
            {
                Kind k;
                if (!Kinds.TryGetValue(x.Sort, out k)) continue;
                string deed = NpcMemory.LineOf(a, me, x.Source) ?? "";
                l.Add(RavenBook.SheetRow.Line(x.Cut ? Lang.F("обірвано · {0}: {1}", Lang.T(k.Word), deed) : Lang.F("{0}: {1}", Lang.T(k.Word), deed), !x.Cut, NpcMemory.Ago(x.At)));
            }
            return l;
        }

        // ---------------------------------------------------------------- the console: raven npc thread <who> [tie <kind> | cut <kind>]
        private static string Cmd(NpcActor a, string[] w)
        {
            if (w.Length > 4 && (w[3] == "tie" || w[3] == "cut"))
            {
                if (!a.Owned) return NpcKit.Busy(a) ?? "Його веде інша гра.";
                if (!Kinds.ContainsKey(w[4])) return "raven npc thread <хто> tie|cut " + string.Join("|", new List<string>(Kinds.Keys).ToArray());
                Player me = Player.m_localPlayer;
                if (me == null) return "Нема гравця.";
                Set(a, me.GetPlayerID(), w[4], "console", w[3] == "cut");
                return "Сказано.";
            }
            return a.Name + ": " + (NpcLocker.Get(a, KThreads) ?? "шафка ще не тут");
        }

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            NpcMemory.Taken.Add(Taken);
            TiedBy("hand", "gift_liked", false); // (a gift it liked is a hand given, as what it asked for: three tie it — NpcTalkGift)
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcProfile.Rows.Add(Rows);
            Npcs.Commands["thread"] = (p, who, a, aim) => Cmd(who, a);
        }

        internal static void Register()
        {
            StoryCond.Register("thread", new StoryCond.Kind { Usage = "thread <вид>   (між вами є ця нитка: life, word, dream, hand, time)", Min = 1, Test = (p, w) => NpcTalk.Him != null && p != null && Has(NpcTalk.Him, p.GetPlayerID(), w[1]) });
        }
    }
}
#endif

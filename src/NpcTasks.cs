#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A settler's small task for one player (the user 2026-11-07): not «bring ten logs in three days» (NpcRequests), not a long wish (NpcGoals), but a word in passing with a true reason,
    // for the one standing near, and over within a little while. Nothing is accepted and nothing is owed: left undone it passes without blame.
    //   Kinds    one row each, nothing else names them. Why: is there a true reason now (what happened, what is so), Source asked once (a while: the last SeenMost). Trust: how warm it must be to ask. Days: how long it stands.
    //            Toward, Thread, Reties, Done: what it means when done (NpcMemory.AddKind makes its memory «task_<id>», NpcThreads.TiedBy its thread: a small deed ties one only where it truly means something; Reties: only mends one that was cut).
    //            Word/Line/About: your task in words. Its words: task_<id>_ask, task_<id>_done (at least six each, npcwordcheck); its answers are made from the row (BuildTalk)
    //   The deed, one of these, all asked on the game of the one it is for:
    //            When+Test   a thing done, as the Diary hears it (Ev.Fuel, Ev.Kill…: OnEvent, one line in Diary.Add; a kind none waits for costs a set's look)
    //            Built       a thing built (Diary.Placed)
    //            Hung        a trophy hung, on any stand (Diary's trophy patch)
    //            HoldFor+Holds  so many s of something true: sitting by it, standing by it (Poll, once a second, only over your own tasks; time that was true is kept, so stepping away loses nothing).
    //                        You near, it stays for that: taskstay, a whim of its own with a term (SitsToo: it sits; HoldTheme: it talks meanwhile), let go when done or dropped
    //            Spot        reaching a place (shown on the screen and the map once)
    //            Tell        a word to the one it is about: an answer in that one's talk (?taskabout), its answer theme ToldTheme or Deliver
    //            Here        an answer in its own talk
    //   Who      × NpcNature.Task: how like this nature it is to ask (a column of each row), on top of Chance a look
    //   Limits   one task a person, PerHeart at a heart, GapDays between two of one person; never in a fight or a scene, nor while it is angry. A reason asked once is not asked again (KTaskSeen)
    //   Kept     «kind|player|arg|until|source» in its locker: a reload, a new owner go on with it. Written where it is run; the deed comes from your game (taskdone)
    //   Seen     its words as it asks are said aloud; after that only to the one it is for (NpcVoice.Sources), the talk's first worry; «!» on its name; the menu's lines (NpcMenu.Tasks), the board's «Твої справи»
    //   Talk     ?taskabout <kind> · !taskdo <kind> · !taskhere · !taskdrop · {task_reply} · values {task_other} (the other one, on either side), {task_thing} (Kind.What)
    // Cheap: where it is run a look every LookEvery s, its one string read, a reason looked for only with a player within AskNear and its gap over. On your game: your own tasks read over the
    // people loaded at most once in MineEvery s; a thing done that no kind waits for stops at a set's look. Console: raven npc task <who> [<kind> | clear]; raven npc quarrel <who> <at whom> for a real quarrel.
    internal static class NpcTasks
    {
        internal static readonly int KTask = "RavenWatch_npcTask".GetStableHashCode();         // string «kind|player|arg|until|source» ("" none) — in its locker
        internal static readonly int KTaskSeen = "RavenWatch_npcTaskSeen".GetStableHashCode(); // string «source;…»: reasons already asked over — in its locker
        internal static readonly int KTaskNext = "RavenWatch_npcTaskNext".GetStableHashCode(); // string: world seconds it may ask again — in its locker
        internal static float LookEvery = 30f, HeartEvery = 5f, MineEvery = 3f, AskNear = 12f, Chance = 0.25f, GapDays = 0.5f, ThemeAsked = 55f, ThemeSaid = 25f, StayNear = 8f, StayEvery = 12f, StayFor = 25f;
        private const int PerHeart = 2, SeenMost = 32;
        private const string Whose = "task"; // (its whim's owner: it waits for you, with its term)

        internal sealed class Reason { public string Source = "", Arg = ""; }
        internal sealed class Task { public string Sort = "", Arg = "", Source = ""; public long Player, Until; }
        internal sealed class Kind
        {
            public string Id = "", Word = "", Done = "", Thread = "";
            public float Trust, Days = 0.25f, Toward = 3f;
            public Func<NpcActor, Player, Reason> Why;
            public Func<NpcActor, Task, string> Line, About, What;     // your task in words where it asked (none: Word) · at the one it is about · {task_thing}
            public Ev? When; public Func<NpcActor, Task, string, bool> Test;  // a thing done (Diary): is it this
            public Func<NpcActor, Task, Piece, bool> Built;            // a thing built
            public bool Hung, Reties;                                  // a trophy hung · its Thread only mended, never tied new
            public float HoldFor; public Func<NpcActor, Task, Player, bool> Holds; // so many s of this
            public bool SitsToo; public string HoldTheme = "";         // ...it sits meanwhile · what it says meanwhile
            public Func<NpcActor, Task, Vector3?> Spot;                // reach this place
            public string Tell = "", ToldTheme = "";                   // an answer in the talk of the one it is about («> слова. => … ?умови !дії» without the head), its reply
            public Func<NpcActor, NpcActor, Player, Task, string> Deliver; // …or a reply of its own, with what follows from it
            public string Here = "";                                   // an answer in its own talk, its reply task_<id>_done
            public string Yes = "> Добре. || Зроблю. || Гаразд.", No = "> Не зараз. || Іншим разом.", YesTheme = "task_ok", NoTheme = "task_no"; // what you answer it, what it says to that
        }

        private static float Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }
        private static long Today { get { return (long)(NpcRescue.Now / Day); } }
        private static string Id(long v) { return v.ToString(CultureInfo.InvariantCulture); }
        private static ZDOID Heart(NpcActor a) { return a != null && a.Zdo != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None; }
        private static float From(Vector3 at, Vector3 to) { return Utils.DistanceXZ(at, to); }
        private static bool AtBase(NpcActor a, Vector3 at) { return From(NpcRules.Home(a), at) <= NpcBase.Reach; }
        private static bool Sitting(Player p) { return p.IsSitting() || p.IsAttached(); }
        private static bool By(NpcActor a, Player p, float m) { return From(p.transform.position, a.transform.position) <= m; }
        private static Func<NpcActor, Task, Player, bool> SitsBy(float m) { return (a, t, p) => Sitting(p) && By(a, p, m); }
        private static bool YouAtHome(NpcActor a, Task t, string arg) { return AtBase(a, Player.m_localPlayer.transform.position); } // (a thing done: OnEvent asks only with you here)
        private static readonly int PerfAsk = NpcPerf.Id("resident: small tasks (дрібні завдання)");

        // the fires round its home: its base's look (NpcBase), never a search of the world (null: not made yet)
        private static List<Component> Fires(NpcActor a) { NpcBase b = NpcBase.For(NpcRules.Home(a)); return b != null ? b.Round(typeof(Fireplace)) : null; }
        private static string Seed(NpcActor o) { return NpcKit.Seed(o.Zdo).ToString(CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- the kinds
        private const string SitYes = "> Гаразд, посиджу. || Посиджу.";
        private static readonly string[] TalkWords = Lang.Keys("Передам.", "Гаразд, скажу.", "Самі розбирайтеся.", "Це не моя справа.", "Тобі дещо просили передати.", "Є слово для тебе.", "Тримай, поїж. Це за тебе просили.", "На, з'їж. Сусід за тебе хвилюється.", "Ласкаво просимо до оселі.", "Вітаю тебе тут. Освоюйся.", "Гаразд, посиджу.", "Вибач. Сідаю.", "Посиджу.", "Постою.", "Гаразд, я тут.", "Гляну.", "Гаразд, постою.", "Слухаю.", "Дивись.", "Підходь, дивись.", "Сідай, розкажу.", "Далеко бу{в|ла}. Слухай.", "Добре.", "Зроблю.", "Гаразд.", "Не зараз.", "Іншим разом.", "Мені зараз не до того."); // (answers are plain text in the rows: marked here for tools/l10n.js)
        internal static float SitNear = 4f, StandNear = 6f, FreshDays = 2f, LightNear = 8f, FireLow = 1.5f, BeastNear = 35f, WordYes = 0.75f, WordYesGrumpy = 0.35f;
        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>();
        private static void K(Kind k) { Kinds[k.Id] = k; }

        static NpcTasks()
        {
            // ---- from what happened between people
            K(new Kind { Id = "word", Word = Lang.K("передати слово"), Done = Lang.K("ти переда{в|ла} його|її слово"), Thread = "word", Trust = 25f, Days = 0.5f, Toward = 6f, Why = WordWhy, Deliver = WordDeliver,
                Line = (a, t) => Lang.F("передати слово: {0}", OtherName(a, t)), About = (o, t) => Lang.F("є слово від: {0}", MainName(o, t)),
                Tell = "> Тобі дещо просили передати. || Є слово для тебе. => c_task_told !taskdo word",
                Yes = "> Передам. || Гаразд, скажу.", No = "> Самі розбирайтеся. || Це не моя справа.", YesTheme = "task_word_yes", NoTheme = "task_word_drop" });
            K(new Kind { Id = "feed", Word = Lang.K("нагодувати сусіда"), Done = Lang.K("ти нагоду{вав|вала} сусіда, коли він|вона попросив|попросила"), Trust = 10f, Days = 0.25f, Toward = 4f, Why = FeedWhy, ToldTheme = "task_feed_told",
                Line = (a, t) => Lang.F("нагодувати: {0}", OtherName(a, t)), About = (o, t) => Lang.T("принести поїсти: просить сусід"),
                Tell = "> Тримай, поїж. Це за тебе просили. || На, з'їж. Сусід за тебе хвилюється. => c_task_told ?hasfood !givefood !taskdo feed" });
            K(new Kind { Id = "greet", Word = Lang.K("привітати новенького"), Done = Lang.K("ти приві{тав|тала} новенького, коли він|вона попросив|попросила"), Why = GreetWhy, ToldTheme = "task_greet_told", Days = 0.5f,
                Line = (a, t) => Lang.F("привітати: {0}", OtherName(a, t)), About = (o, t) => Lang.T("привітай: новенький тут"),
                Tell = "> Ласкаво просимо до оселі. || Вітаю тебе тут. Освоюйся. => c_task_told !taskdo greet" });
            K(new Kind { Id = "mend", Word = Lang.K("посидіти, щоб помиритись"), Done = Lang.K("ти посиді{в|ла} поруч, коли не дотрима{в|ла} слова"), Thread = "word", Reties = true, Trust = -60f, Toward = 5f, Why = MendWhy, SitsToo = true,
                HoldFor = 20f, Holds = SitsBy(SitNear), Yes = "> Гаразд, посиджу. || Вибач. Сідаю.", YesTheme = "task_mend_yes", NoTheme = "task_mend_no" });
            // ---- from what it feels
            K(new Kind { Id = "sit", Word = Lang.K("посидіти поруч"), Done = Lang.K("ти посиді{в|ла} поруч, коли йому|їй було важко"), Thread = "time", Trust = 15f, Toward = 6f, Why = SitWhy, SitsToo = true, Yes = SitYes,
                HoldFor = 20f, Holds = SitsBy(SitNear) });
            K(new Kind { Id = "calm", Word = Lang.K("постояти поруч"), Done = Lang.K("ти постоя{в|ла} поруч, коли йому|їй було страшно"), Thread = "time", Toward = 6f, Why = CalmWhy, Yes = "> Постою. || Гаразд, я тут.",
                HoldFor = 25f, Holds = (a, t, p) => By(a, p, StandNear) });
            K(new Kind { Id = "look", Word = Lang.K("глянути, що там"), Done = Lang.K("ти схо{див|дила} глянути, чого він|вона боявся|боялася"), Why = LookWhy, Toward = 4f, Spot = (a, t) => SpotOf(t) });
            // ---- from what is so at home
            K(new Kind { Id = "fire", Word = Lang.K("підкинути у вогонь"), Done = Lang.K("ти підкину{в|ла} у вогонь, коли він|вона попросив|попросила"), Why = FireWhy, Toward = 2f, When = Ev.Fuel, Test = YouAtHome });
            K(new Kind { Id = "hearth", Word = Lang.K("розпалити вогонь в оселі"), Done = Lang.K("ти да{в|ла} оселі вогонь"), Why = HearthWhy, Toward = 5f, Days = 0.5f, When = Ev.Fuel, Test = YouAtHome, Built = (a, t, made) => made.GetComponent<Fireplace>() != null && AtBase(a, made.transform.position) });
            K(new Kind { Id = "light", Word = Lang.K("поставити світло біля ліжка"), Done = Lang.K("ти постави{в|ла} світло біля його|її ліжка"), Why = LightWhy, Toward = 4f, Days = 0.5f, Built = (a, t, made) => { GameObject bed = BedOf(a); return bed != null && made.GetComponent<Fireplace>() != null && From(bed.transform.position, made.transform.position) <= LightNear; } });
            K(new Kind { Id = "beast", Word = Lang.K("розібратися зі звіром"), Done = Lang.K("ти розібра{вся|лась} зі звіром біля оселі"), Why = BeastWhy, Toward = 4f, When = Ev.Kill, Test = (a, t, arg) => arg == t.Arg.TrimStart('$') && From(NpcRules.Home(a), Player.m_localPlayer.transform.position) <= NpcBase.Reach + BeastNear,
                What = (a, t) => Localization.instance != null ? Localization.instance.Localize(t.Arg).ToLowerInvariant() : t.Arg });
            K(new Kind { Id = "trophy", Word = Lang.K("повісити трофей"), Done = Lang.K("ти пові{сив|сила} трофей, коли він|вона попросив|попросила"), Why = TrophyWhy, Days = 0.5f, Hung = true });
            // ---- from who it is
            K(new Kind { Id = "fireside", Word = Lang.K("посидіти біля вогню"), Done = Lang.K("ти посиді{в|ла} з ним|нею біля вогню"), Trust = 10f, Why = FiresideWhy, SitsToo = true, Yes = SitYes, HoldFor = 20f, Holds = SitsBy(StandNear) });
            K(new Kind { Id = "stars", Word = Lang.K("подивитись на зорі"), Done = Lang.K("ти диви{вся|лась} з ним|нею на зорі"), Thread = "time", Trust = 10f, Toward = 4f, Why = StarsWhy, Yes = "> Гляну. || Гаразд, постою.", HoldFor = 15f, Holds = (a, t, p) => By(a, p, StandNear * 1.5f) && !NpcToil.Roof(p.transform.position + Vector3.up) }); // (heavy: once a second, only while this one task of yours stands)
            K(new Kind { Id = "listen", Word = Lang.K("послухати історію"), Done = Lang.K("ти слуха{в|ла} його|її історію"), Thread = "time", Trust = 10f, Toward = 4f, Why = ListenWhy, SitsToo = true, HoldTheme = "tale", Yes = "> Слухаю. || Гаразд, посиджу.", HoldFor = 30f, Holds = SitsBy(StandNear) });
            K(new Kind { Id = "raven", Word = Lang.K("показати ворона"), Done = Lang.K("ти показа{в|ла} йому|їй свого ворона"), Why = RavenWhy, Yes = "> Дивись. || Підходь, дивись.", HoldFor = 4f, Holds = (a, t, p) => By(a, p, StandNear * 0.6f) && HasRaven(p) });
            K(new Kind { Id = "news", Word = Lang.K("розповісти, де ти бу{в|ла}"), Done = Lang.K("ти розпові{в|ла}, де бу{в|ла}"), Why = NewsWhy, Here = "> Сідай, розкажу. || Далеко бу{в|ла}. Слухай. => c_task_here !taskhere" });
        }

        // ---------------------------------------------------------------- why now
        private static NpcMemory.Entry Newest(NpcActor a, long player, Func<string, bool> kinds)
        {
            NpcMemory.Entry last = null;
            foreach (NpcMemory.Entry e in NpcMemory.About(a, player)) if (kinds(e.Kind) && (last == null || e.At > last.At)) last = e;
            return last;
        }
        private static bool Fresh(NpcMemory.Entry e, float days) { return e != null && NpcRescue.Now - e.At <= days * Day; }

        // one of its own people a task may be about: a settler (a person of the story living by the same heart has no answer for it in its talk)
        private static bool Folk(NpcActor a, NpcActor o) { return o != null && o != a && o.Zdo != null && o.Def != null && o.Def.Settler; }

        private static NpcActor NamedAtHome(NpcActor a, string name)
        {
            foreach (NpcActor o in NpcHeart.Residents(Heart(a))) if (Folk(a, o) && o.Key == name) return o;
            return null;
        }

        // a quarrel of its own, not long ago, the other one here
        private static Reason WordWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry e = Newest(a, 0L, k => k.StartsWith("quarrel_", StringComparison.Ordinal));
            NpcActor o = Fresh(e, FreshDays) ? NamedAtHome(a, e.What) : null;
            return o != null ? new Reason { Source = e.Kind + "@" + Id(e.At), Arg = Seed(o) } : null;
        }

        // one of its people is hungry with nothing cooked to be had at home, or starving; it is not (a hungry one with a full pantry just goes and eats)
        private static Reason FeedWhy(NpcActor a, Player p)
        {
            if (NpcHunger.IsHungry(a)) return null;
            foreach (NpcActor o in NpcHeart.Residents(Heart(a)))
                if (Folk(a, o) && NpcHunger.IsHungry(o) && (NpcHunger.IsStarving(o) || NpcMeals.NothingToEat(o))) return new Reason { Source = "feed" + Seed(o) + "@" + Id(Today), Arg = Seed(o) };
            return null;
        }

        // one came to live here not long ago, not itself
        private static Reason GreetWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry e = Newest(a, 0L, k => k == "news_newcomer");
            NpcActor o = Fresh(e, FreshDays) ? NamedAtHome(a, e.What) : null;
            return o != null ? new Reason { Source = "greet" + Id(e.At), Arg = Seed(o) } : null;
        }

        // you broke your word to it not long ago: a little thing first
        private static Reason MendWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry e = NpcMemory.Find(a, "promise_broken", p.GetPlayerID());
            return Fresh(e, 5f) ? new Reason { Source = "mend" + Id(e.At) } : null;
        }

        // it grieves, or one of its people left not long ago
        private static Reason SitWhy(NpcActor a, Player p)
        {
            string feel; float strength;
            if (NpcFeel.Of(a, out feel, out strength) && feel == NpcFeel.Grief) return new Reason { Source = "sit" + Id(Today) };
            NpcMemory.Entry e = NpcMemory.Find(a, "neighbour_left", 0L);
            return Fresh(e, FreshDays) ? new Reason { Source = "sit" + Id(e.At) } : null;
        }

        private static bool Frightened(NpcActor a) { string feel; float strength; return NpcFeel.Of(a, out feel, out strength) && feel == NpcFeel.Fright; }

        // it is frightened now, the danger over
        private static Reason CalmWhy(NpcActor a, Player p) { return Frightened(a) && !NpcNews.RaidNear(a) ? new Reason { Source = "calm" + Id(Today) } : null; }

        // frightened, or a fight at home not long ago, or a timid one at night: the door of the base farthest from it, is anyone there
        private static Reason LookWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry fight = Newest(a, 0L, k => k == "fight_lost" || k == "fight_won");
            if (!Frightened(a) && !Fresh(fight, 1f) && !(NpcPhrases.Tones(a).Contains("боязк") && EnvMan.IsNight())) return null;
            NpcBase b = NpcBase.For(NpcRules.Home(a));
            List<Component> doors = b != null ? b.Round(typeof(Door)) : null;
            if (doors == null) return null;
            Component far = null; float most = 8f;
            foreach (Component d in doors) { if (d == null || From(d.transform.position, p.transform.position) <= StandNear * 2f) continue; float m = From(d.transform.position, a.transform.position); if (m > most) { most = m; far = d; } } // (not one you already stand by)
            if (far == null) return null;
            Vector3 at = far.transform.position;
            return new Reason { Source = "look" + Id(Today), Arg = at.x.ToString("0", CultureInfo.InvariantCulture) + "_" + at.y.ToString("0", CultureInfo.InvariantCulture) + "_" + at.z.ToString("0", CultureInfo.InvariantCulture) };
        }

        private static Vector3? SpotOf(Task t)
        {
            string[] w = t.Arg.Split('_');
            float x, y, z;
            if (w.Length < 3 || !float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return null;
            return new Vector3(x, y, z);
        }

        // a fire of the base about to go out, nobody at home whose work that is
        private static Reason FireWhy(NpcActor a, Player p)
        {
            foreach (NpcActor o in NpcHeart.Residents(Heart(a))) if (o != null && o.Worker != null && o.Worker.JobId == "stoker") return null;
            List<Component> fires = Fires(a);
            if (fires == null) return null;
            foreach (Component c in fires)
            {
                Fireplace f = c as Fireplace;
                ZNetView nv = f != null ? f.GetComponent<ZNetView>() : null;
                if (nv == null || !nv.IsValid() || f.m_infiniteFuel || !f.m_canRefill || f.m_fuelItem == null || (f.m_canTurnOff && nv.GetZDO().GetInt(ZDOVars.s_state, 1) == 0)) continue;
                float fuel = nv.GetZDO().GetFloat(ZDOVars.s_fuel);
                if (fuel > 0f && fuel <= FireLow) return new Reason { Source = "fire" + Id(Today) }; // (burning low: one out already is not «about to go out»)
            }
            return null;
        }

        // no warm fire burning at its home, the yard by its heart too (its base not looked over yet: not known, so no reason)
        private static Reason HearthWhy(NpcActor a, Player p)
        {
            List<Component> fires = Fires(a);
            if (fires == null) return null;
            foreach (Component c in fires) { Fireplace f = c as Fireplace; if (f != null && f.IsBurning() && NpcKit.Warms(f)) return null; }
            return new Reason { Source = "hearth" + Id(Today) };
        }

        private static GameObject BedOf(NpcActor a) { return NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed)); }

        // nothing that burns near its bed; asked once a bed
        private static Reason LightWhy(NpcActor a, Player p)
        {
            GameObject bed = BedOf(a);
            List<Component> fires = bed != null ? Fires(a) : null;
            if (fires == null) return null;
            foreach (Component c in fires) if (c != null && From(c.transform.position, bed.transform.position) <= LightNear) return null;
            return new Reason { Source = "light" + NpcLocker.Text(a.Zdo, NpcResident.KBed).Replace(";", "").Replace("|", "") };
        }

        // a wild thing that means harm near its home
        private static readonly List<Character> Around = new List<Character>();
        private static Reason BeastWhy(NpcActor a, Player p)
        {
            Vector3 home = NpcRules.Home(a);
            Around.Clear();
            NpcFoes.Near(home, BeastNear, Around);
            foreach (Character c in Around)
            {
                if (c == null || c.IsBoss() || c.IsTamed() || c.m_faction == Character.Faction.AnimalsVeg || c.m_faction == Character.Faction.Players || c.GetComponent<NpcActor>() != null || From(c.transform.position, home) > BeastNear) continue;
                return new Reason { Source = "beast" + c.m_name.Replace("|", "") + Id(Today), Arg = c.m_name };
            }
            return null;
        }

        // the news of a lord of these lands fallen, not long ago
        private static Reason TrophyWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry e = Newest(a, 0L, k => k.StartsWith("news_boss_", StringComparison.Ordinal));
            return Fresh(e, 3f) ? new Reason { Source = "trophy" + Id(e.At) } : null;
        }

        // the evening, a fire burning by it
        private static Reason FiresideWhy(NpcActor a, Player p)
        {
            if (NpcVoice.DayPart() != "evening") return null;
            List<Component> fires = Fires(a);
            if (fires == null) return null;
            foreach (Component c in fires) { Fireplace f = c as Fireplace; if (f != null && f.IsBurning() && NpcKit.Warms(f) && From(f.transform.position, a.transform.position) <= 10f) return new Reason { Source = "fireside" + Id(Today) }; }
            return null;
        }

        // its story: it counts the stars; a clear night
        private static Reason StarsWhy(NpcActor a, Player p) { return NpcBackstory.Fact(a, "зорі") && EnvMan.IsNight() && NpcVoice.Weather().Length == 0 ? new Reason { Source = "stars" + Id(Today) } : null; }

        // its story: one who tells stories; the evening
        private static Reason ListenWhy(NpcActor a, Player p) { return NpcBackstory.Fact(a, "оповідач") && NpcVoice.DayPart() == "evening" ? new Reason { Source = "listen" + Id(Today) } : null; }

        private static bool HasRaven(Player p) { return p == Player.m_localPlayer && (EmoteTalk.Free(p, true) || EmoteTalk.Free(p, false)); }

        // a curious one, your raven on your shoulder (known only on your own game: asked only where it is run by that game); not again while it remembers seeing it
        private static Reason RavenWhy(NpcActor a, Player p) { return HasRaven(p) && NpcPersona.Of(a).Curiosity > 0.4f && NpcMemory.Find(a, "task_raven", p.GetPlayerID()) == null ? new Reason { Source = "raven" + Id(p.GetPlayerID()) } : null; }

        // you were away long, and it said so
        private static Reason NewsWhy(NpcActor a, Player p)
        {
            NpcMemory.Entry e = NpcMemory.Find(a, "missed", p.GetPlayerID());
            return Fresh(e, 1f) ? new Reason { Source = "news" + Id(e.At) } : null;
        }

        // ---------------------------------------------------------------- kept
        // its task now (past its time, or a kind no longer known: none, wiped at its next look: Tick); the one word for everyone who asks
        internal static Task Of(NpcActor a)
        {
            string s = a != null ? NpcLocker.Get(a, KTask) : null;
            if (string.IsNullOrEmpty(s)) return null;
            string[] w = s.Split('|');
            Task t = new Task();
            if (w.Length < 5 || !Kinds.ContainsKey(w[0]) || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.Player) || !long.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.Until) || NpcRescue.Now > t.Until) return null;
            t.Sort = w[0]; t.Arg = w[2]; t.Source = w[4];
            return t;
        }

        private static void Write(NpcActor a, Task t)
        {
            _hearts.Clear();
            _mineAt = 0f;
            NpcLocker.Set(a, KTask, t == null ? "" : t.Sort + "|" + Id(t.Player) + "|" + t.Arg + "|" + Id(t.Until) + "|" + t.Source);
        }

        // who has a task at this heart: looked for at most once in HeartEvery s (its talk and each line it says ask)
        private sealed class AtHeart { public float Until; public readonly List<KeyValuePair<NpcActor, Task>> Who = new List<KeyValuePair<NpcActor, Task>>(); }
        private static readonly Dictionary<ZDOID, AtHeart> _hearts = new Dictionary<ZDOID, AtHeart>(); // session: the heart's id now, never kept
        private static AtHeart Look(ZDOID heart)
        {
            AtHeart h;
            if (_hearts.TryGetValue(heart, out h) && Time.time < h.Until) return h;
            h = new AtHeart { Until = Time.time + HeartEvery };
            foreach (NpcActor o in NpcHeart.Residents(heart)) { Task t = Of(o); if (t != null) h.Who.Add(new KeyValuePair<NpcActor, Task>(o, t)); }
            return _hearts[heart] = h;
        }

        // the one at its heart who gave that player a task about this one (null: none)
        private static NpcActor MainOf(NpcActor other, long player, string kind, out Task task)
        {
            task = null;
            ZDOID heart = Heart(other);
            if (heart.IsNone() || other.Zdo == null) return null;
            string seed = Seed(other);
            foreach (KeyValuePair<NpcActor, Task> e in Look(heart).Who)
                if (e.Key != null && e.Key != other && e.Value.Arg == seed && (player == 0L || e.Value.Player == player) && (kind == null || e.Value.Sort == kind)) { task = e.Value; return e.Key; }
            return null;
        }

        private static NpcActor OtherOf(NpcActor a, Task t)
        {
            uint seed;
            if (t == null || !uint.TryParse(t.Arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) return null;
            foreach (NpcActor o in NpcHeart.Residents(Heart(a))) if (o != null && o.Zdo != null && NpcKit.Seed(o.Zdo) == seed) return o;
            return null;
        }

        private static string OtherName(NpcActor a, Task t) { NpcActor o = OtherOf(a, t); return o != null ? o.Name : "…"; }
        private static string OtherKey(NpcActor a, Task t) { NpcActor o = OtherOf(a, t); return o != null ? o.Key : "…"; } // (kept: what its memory holds)
        private static string MainName(NpcActor o, Task t) { Task x; NpcActor m = MainOf(o, t.Player, t.Sort, out x); return m != null ? m.Name : "…"; }

        // ---------------------------------------------------------------- asking (where it is run)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        private static void Forget(NpcActor a) { Next.Remove(a); }

        private static void Tick(NpcActor a)
        {
            float at;
            if (!Next.TryGetValue(a, out at)) { Next[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.2f, 1f); return; } // (not all at once as a base loads, nor before it is looked over)
            if (Time.time < at) return;
            Next[a] = Time.time + LookEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            string kept = NpcLocker.Get(a, KTask);
            if (kept == null) return; // (its locker not here yet)
            if (kept.Length > 0)
            {
                if (Of(a) == null) { Write(a, null); Npcs.Log(a.Id + ": its small task passed"); }
                return;
            }
            if (!NpcResident.Settled(a)) return;
            long now = (long)NpcRescue.Now;
            Player p = Player.GetClosestPlayer(a.transform.position, AskNear);
            if (p == null || a.Mind == null || a.Mind.NowLayer >= NpcMind.Layer.Fight || !NpcVoice.Free(a) || NpcPatience.Angry(a)) return;
            long next; string n = NpcLocker.Get(a, KTaskNext);
            if (n != null && long.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out next) && now < next) return;
            if (Look(Heart(a)).Who.Count >= PerHeart) return;
            long perf = NpcPerf.Now();
            Ask(a, p, null);
            NpcPerf.Add(PerfAsk, perf);
        }

        // a task now: of the kinds with a true reason it has not asked over, by its nature; only: that kind, at once (the console). Its name, or null
        private static string Ask(NpcActor a, Player p, string only)
        {
            long me = p.GetPlayerID();
            float warm = NpcAttitude.Of(a, me);
            NpcNature nature = NpcNature.Of(a);
            string seen = NpcLocker.Get(a, KTaskSeen) ?? "";
            string[] asked = seen.Split(';');
            List<KeyValuePair<Kind, Reason>> can = new List<KeyValuePair<Kind, Reason>>();
            foreach (Kind k in Kinds.Values)
            {
                if (only != null ? k.Id != only : warm < k.Trust || UnityEngine.Random.value > Chance * NpcNature.Task(nature, k.Id)) continue;
                Reason r = k.Why(a, p);
                if (r != null && Array.IndexOf(asked, r.Source) < 0) can.Add(new KeyValuePair<Kind, Reason>(k, r));
            }
            if (can.Count == 0) return null;
            KeyValuePair<Kind, Reason> pick = can[UnityEngine.Random.Range(0, can.Count)];
            long now = (long)NpcRescue.Now;
            Write(a, new Task { Sort = pick.Key.Id, Player = me, Arg = pick.Value.Arg, Source = pick.Value.Source, Until = now + (long)(pick.Key.Days * Day) });
            List<string> kept = new List<string>(seen.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            kept.Add(pick.Value.Source);
            if (kept.Count > SeenMost) kept.RemoveRange(0, kept.Count - SeenMost);
            NpcLocker.Set(a, KTaskSeen, string.Join(";", kept.ToArray()));
            NpcLocker.Set(a, KTaskNext, Id(now + (long)(GapDays * Day)));
            Npcs.Log(a.Id + ": asks a small task «" + pick.Key.Id + "» of " + me + " (" + pick.Value.Source + ")");
            NpcPhrases.SayOr(a, "task_" + pick.Key.Id + "_ask", "…");
            return pick.Key.Id;
        }

        // {task_other} in what the other one answers is the one who asked, whatever task it may have of its own
        private static Dictionary<string, string> ToldBy(NpcActor main) { return new Dictionary<string, string> { { "task_other", main.Name } }; }

        // you said it to the other one: it takes it, or not yet (its patience to you, a grumbler hardly); either way you did what was asked
        private static string WordDeliver(NpcActor main, NpcActor other, Player p, Task t)
        {
            bool yes = NpcPatience.Of(other, p) >= NpcPatience.Dry && UnityEngine.Random.value < (NpcPhrases.Tones(other).Contains("бурч") ? WordYesGrumpy : WordYes);
            string said = NpcPhrases.Compose(other, yes ? "task_word_told_yes" : "task_word_told_no", p, ToldBy(main)) ?? "…";
            int at = t.Source.IndexOf('@');
            if (yes && at > 0) NpcAttitude.MakeUp(main, other, t.Source.Substring(0, at));
            return said;
        }

        // ---------------------------------------------------------------- the deed (on your game)
        private static readonly List<KeyValuePair<NpcActor, Task>> _mine = new List<KeyValuePair<NpcActor, Task>>(); // session: your tasks, read at most once in MineEvery s
        private static float _mineAt;
        private static List<KeyValuePair<NpcActor, Task>> Mine()
        {
            if (Time.time < _mineAt) return _mine;
            _mineAt = Time.time + MineEvery;
            _mine.Clear();
            Player me = Player.m_localPlayer;
            if (me == null) return _mine;
            long id = me.GetPlayerID();
            foreach (NpcActor a in NpcActor.Live) { Task t = a != null ? Of(a) : null; if (t != null && t.Player == id) _mine.Add(new KeyValuePair<NpcActor, Task>(a, t)); }
            return _mine;
        }

        private static void Complete(NpcActor main, Task t)
        {
            main.Act("taskdone " + Id(t.Player) + " " + Id(t.Until));
            _held.Remove(HeldKey(main, t));
            _mineAt = 0f; _hearts.Clear();
        }

        // a trophy hung, on any stand (Diary's trophy patch)
        internal static void Hung(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return;
            foreach (KeyValuePair<NpcActor, Task> e in Mine()) if (e.Key != null && Kinds[e.Value.Sort].Hung && AtBase(e.Key, p.transform.position)) Complete(e.Key, e.Value);
        }

        private static readonly HashSet<Ev> Watched = new HashSet<Ev>(); // (the things done some kind waits for: all others stop at this)

        // a thing you did (Diary.Add, on your game)
        internal static void OnEvent(Ev type, string arg)
        {
            if (!Watched.Contains(type) || Player.m_localPlayer == null) return;
            foreach (KeyValuePair<NpcActor, Task> e in Mine()) // (Complete does not touch the list: only the next read makes it anew)
            {
                Kind k = Kinds[e.Value.Sort];
                if (e.Key != null && k.When == type && k.Test != null && k.Test(e.Key, e.Value, arg)) Complete(e.Key, e.Value);
            }
        }

        // a thing you built (Diary.Placed)
        internal static void Built(Player p, Piece made)
        {
            if (p == null || made == null || p != Player.m_localPlayer) return;
            foreach (KeyValuePair<NpcActor, Task> e in Mine())
            {
                Kind k = Kinds[e.Value.Sort];
                if (e.Key != null && k.Built != null && k.Built(e.Key, e.Value, made)) Complete(e.Key, e.Value);
            }
        }

        // what holds for a while, a place to reach: once a second, only your own tasks
        private static float _pollAt;
        private static readonly Dictionary<string, float> _stayAt = new Dictionary<string, float>(); // session: when each one is told again to wait
        private static readonly Dictionary<string, float> _held = new Dictionary<string, float>(); // session: how long it has held now
        private static readonly HashSet<string> _shown = new HashSet<string>();                    // session: a place already shown
        private static string HeldKey(NpcActor a, Task t) { return Seed(a) + "@" + Id(t.Until); }
        private static void Poll()
        {
            if (Time.time < _pollAt) return;
            _pollAt = Time.time + 1f;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            foreach (KeyValuePair<NpcActor, Task> e in Mine())
            {
                NpcActor a = e.Key; Task t = e.Value; Kind k = Kinds[t.Sort];
                if (a == null || a.Zdo == null) continue;
                if (k.Spot != null)
                {
                    Vector3? at = k.Spot(a, t);
                    if (at == null) continue;
                    if (_shown.Count > 64) _shown.Clear();
                    if (_shown.Add(HeldKey(a, t))) NpcMenu.ShowPlace(at.Value, null, a.Form(Grammar.Fix(Lang.T(k.Word), p)));
                    if (From(p.transform.position, at.Value) <= StandNear) Complete(a, t);
                }
                else if (k.Holds != null)
                {
                    string key = HeldKey(a, t);
                    float stay; _stayAt.TryGetValue(key, out stay);
                    if (By(a, p, StayNear) && Time.time >= stay) { if (_stayAt.Count > 64) _stayAt.Clear(); _stayAt[key] = Time.time + StayEvery; a.Act("taskstay " + Id(t.Player) + " " + Id(t.Until)); } // (you came: it waits for you, each its own)
                    float held; _held.TryGetValue(key, out held);
                    if (!k.Holds(a, t, p)) continue; // (what was true stays counted: its work may take it a step away)
                    held += 1f;
                    if (held >= k.HoldFor) Complete(a, t); else { if (_held.Count > 64) _held.Clear(); _held[key] = held; }
                }
            }
        }

        // ---------------------------------------------------------------- done, dropped (where it is run)
        // it waits for you, with its term: its own whim (a duty of its own, a fight, a scene go over it); said again while you are near
        private static void Stay(NpcActor a, Kind k)
        {
            if (a.Mind == null) return;
            NpcMind.Plan w = a.Mind.Of(NpcMind.Layer.Whim);
            if (w != null && w.Whose == Whose) w.Until = Time.time + StayFor;
            else if (!a.Mind.TakeWhim(new NpcMind.Plan { Kind = k.SitsToo ? NpcMind.Task.Sit : NpcMind.Task.Idle, To = a.transform.position }, StayFor, Whose)) return;
            if (k.HoldTheme.Length > 0) NpcPhrases.SayOr(a, k.HoldTheme, "…", null, NpcActor.Voice.Unasked);
        }

        // «taskdone <player> <until>» · «taskdrop <player> <until>» · «taskstay <player> <until>»: only that very task, only from that player's game
        private static void Act(NpcActor a, string[] w)
        {
            long who, until;
            if (w.Length < 3 || !a.Owned || !long.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out who) || !long.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out until) || !NpcActor.SentBy(who)) return;
            Task t = Of(a);
            if (t == null || t.Player != who || t.Until != until) return;
            Kind k = Kinds[t.Sort];
            if (w[0] == "taskstay") { Stay(a, k); return; }
            string other = OtherKey(a, t);
            Write(a, null);
            if (a.Mind != null) a.Mind.EndWhim(Whose);
            if (w[0] == "taskdrop") { Npcs.Log(a.Id + ": its small task «" + t.Sort + "» let go"); return; }
            NpcMemory.Remember(a, "task_" + t.Sort, who, other == "…" ? "" : other); // (its warmth and its thread: NpcMemory, NpcThreads)
            NpcFeel.Feel(a, NpcFeel.Joy, 60f, a.transform.position, false);
            Player near = Player.GetClosestPlayer(a.transform.position, 30f);
            if (k.Here.Length == 0 && near != null && near.GetPlayerID() == who) NpcPhrases.SayOr(a, "task_" + t.Sort + "_done", "…"); // (Here: said in the talk)
            Npcs.Log(a.Id + ": its small task «" + t.Sort + "» done by " + who);
            StoryBus.Raise("npc_task_done", a.Id, 0, a.transform.position);
        }

        // ---------------------------------------------------------------- what you see of it
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            Task t = Of(a);
            if (t != null && t.Player == p.GetPlayerID()) themes.Add(new KeyValuePair<string, float>("task_" + t.Sort + "_ask", NpcVoice.Asked ? ThemeAsked : ThemeSaid));
        }

        private static void Values(NpcActor a, Player to, Dictionary<string, string> v)
        {
            Task t = Of(a);
            if (t != null && Kinds[t.Sort].What != null) v["task_thing"] = Kinds[t.Sort].What(a, t);
            NpcActor o = OtherOf(a, t); // (it asked about the other one; or it is the other one of another's task, whether it has one of its own or not)
            Task about;
            if (o == null) o = MainOf(a, to != null ? to.GetPlayerID() : 0L, null, out about);
            if (o != null) v["task_other"] = o.Name;
        }

        // your tasks with this person: words, seconds left (the menu, the board)
        internal static List<KeyValuePair<string, long>> Yours(NpcActor a, Player p)
        {
            List<KeyValuePair<string, long>> l = new List<KeyValuePair<string, long>>();
            if (a == null || p == null) return l;
            long me = p.GetPlayerID(), now = (long)NpcRescue.Now;
            Task t = Of(a);
            if (t != null && t.Player == me) { Kind k = Kinds[t.Sort]; l.Add(new KeyValuePair<string, long>(k.Line != null ? k.Line(a, t) : a.Form(Grammar.Fix(Lang.T(k.Word), p)), t.Until - now)); }
            return l;
        }

        // the menu: yours with it, and what you were asked to do for it by another (one task: on the board once, at the one who asked)
        private static void MenuTasks(NpcActor a, Player p, List<string> l)
        {
            foreach (KeyValuePair<string, long> e in Yours(a, p)) l.Add(e.Key);
            Task about;
            if (MainOf(a, p.GetPlayerID(), null, out about) != null && Kinds[about.Sort].About != null) l.Add(Kinds[about.Sort].About(a, about));
        }

        // ---------------------------------------------------------------- its talk
        private static string _reply = "";
        private static void DoDeed(NpcActor other, Player p, string[] w)
        {
            _reply = "…";
            if (w.Length < 2 || p == null) return;
            Task t;
            NpcActor main = MainOf(other, p.GetPlayerID(), w[1], out t);
            if (main == null) return;
            Kind k = Kinds[t.Sort];
            _reply = k.Deliver != null ? k.Deliver(main, other, p, t) : NpcPhrases.Compose(other, k.ToldTheme, p, ToldBy(main)) ?? "…";
            Complete(main, t);
        }

        private static void HereDeed(NpcActor a, Player p, string[] w)
        {
            _reply = "…";
            Task t = Of(a);
            if (t == null || p == null || t.Player != p.GetPlayerID()) return;
            _reply = NpcPhrases.Compose(a, "task_" + t.Sort + "_done", p) ?? "…";
            Complete(a, t);
        }

        private static void DropDeed(NpcActor a, Player p, string[] w)
        {
            Task t = Of(a);
            if (t != null && p != null && t.Player == p.GetPlayerID()) a.Act("taskdrop " + Id(t.Player) + " " + Id(t.Until));
            _hearts.Clear(); _mineAt = 0f;
        }

        // a kind's places, from its row: what it says first and your answers; the one it is about: your word to it
        private static string BuildTalk()
        {
            StringBuilder s = new StringBuilder("[talk common +]\n@start +\n");
            foreach (Kind k in Kinds.Values) if (k.Tell.Length > 0) s.Append("?? taskabout ").Append(k.Id).Append('\n').Append(k.Tell).Append('\n');
            s.Append("@c_task_told\n{task_reply}\n> {@say_ok} => start\n@c_task_here\n{task_reply}\n> {@say_ok} => start\n");
            foreach (Kind k in Kinds.Values)
            {
                s.Append("@c_task_").Append(k.Id).Append("_ask\n{concern}\n");
                if (k.Here.Length > 0) s.Append(k.Here).Append('\n');
                else s.Append(k.Yes).Append(" => c_task_").Append(k.Id).Append("_yes\n");
                s.Append(k.No).Append(" => c_task_").Append(k.Id).Append("_no !taskdrop\n> Мені зараз не до того. => end !tone cold\n");
                if (k.Here.Length == 0) s.Append("@c_task_").Append(k.Id).Append("_yes\n{@").Append(k.YesTheme).Append("}\n> {@say_ok} => start\n");
                s.Append("@c_task_").Append(k.Id).Append("_no\n{@").Append(k.NoTheme).Append("}\n> {@say_ok} => start\n");
            }
            return s.ToString();
        }

        // ---------------------------------------------------------------- the console: raven npc task <who> [<kind> | clear]
        private static string Cmd(Player p, NpcActor a, string[] w)
        {
            if (w.Length < 4) { Task t = Of(a); return a.Name + ": " + (t != null ? t.Sort + " · " + t.Source + " · ще " + NpcRescue.Left(t.Until - (long)NpcRescue.Now) : "завдання нема"); }
            if (!a.Owned) return NpcKit.Busy(a) ?? "Його веде інша гра.";
            if (w[3] == "clear") { Write(a, null); NpcLocker.Set(a, KTaskSeen, ""); NpcLocker.Set(a, KTaskNext, ""); return a.Name + ": завдання й пам'ять про причини знято."; }
            if (!Kinds.ContainsKey(w[3])) return "raven npc task <хто> " + string.Join("|", new List<string>(Kinds.Keys).ToArray()) + "|clear";
            if (Of(a) != null) Write(a, null);
            return Ask(a, p, w[3]) != null ? a.Name + ": просить." : a.Name + ": причини нема (або вже просив про неї).";
        }

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            foreach (Kind k in Kinds.Values)
            {
                string memory = "task_" + k.Id;
                NpcMemory.AddKind(memory, new NpcMemory.Kind { Weight = 2f, FadeDays = 15f, Yours = true, Toward = k.Toward }, k.Done);
                if (k.Thread.Length > 0) NpcThreads.TiedBy(k.Thread, memory, k.Reties);
                if (k.When != null) Watched.Add(k.When.Value);
            }
            NpcActor.Acts["taskdone"] = Act;
            NpcActor.Acts["taskdrop"] = Act;
            NpcActor.Acts["taskstay"] = Act;
            NpcActor.Ticks.Add(Tick);
            NpcActor.Everywhere.Add(a => Poll());
            NpcActor.Unloaded.Add(Forget);
            NpcVoice.Sources.Add(Themes);
            NpcPhrases.ValueSources.Add(Values);
            NpcMenu.Tasks.Add(MenuTasks);
            NpcActor.HoverMarks.Add(a => { Player me = Player.m_localPlayer; Task t = me != null && NpcRequests.Of(a) == null ? Of(a) : null; return t != null && t.Player == me.GetPlayerID() ? " <color=#ffd15c><b>!</b></color>" : ""; }); // (its request's mark says the same: one «!»)
            NpcTalk.More.Add(BuildTalk());
            NpcTalk.Words["{task_reply}"] = (a, p) => _reply;
            NpcTalk.Deeds["taskdo"] = DoDeed;
            NpcTalk.Deeds["taskhere"] = HereDeed;
            NpcTalk.Deeds["taskdrop"] = DropDeed;
            Npcs.Commands["task"] = (p, who, a, aim) => Cmd(p, who, a);
        }

        internal static void Register()
        {
            StoryCond.Register("taskabout", new StoryCond.Kind { Usage = "taskabout <вид>   (хтось просив тебе зробити щось для цієї людини)", Min = 1, Test = (p, w) => { Task t; return NpcTalk.Him != null && p != null && MainOf(NpcTalk.Him, p.GetPlayerID(), w[1], out t) != null; } });
        }
    }
}
#endif

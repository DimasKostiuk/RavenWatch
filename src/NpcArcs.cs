#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Small lines of a settler's life (the user 2026-11-05): not a written quest, but a few steps that begin from what really happened at a base and go by what
    // you say. Settlers are made, not written, so a line casts by what they did («the one who hid and the one who fought»). The story's spine is not touched.
    //   A line     Stages: what the one it is about (the main one) and the other one have on their minds (Theme, OtherTheme: worries, answers in the talk's c_<theme>),
    //              how long it lasts (Days, then Then; no Then: over), what it does (Enter once; Step at each look). Begun by NpcArcs.Begin
    //   Kept       on the main one: its locker, one string «arc|stage|other's seed|since» — a reload, a night away, a new owner go on. One line a person, one a
    //              heart at a time, each line once in a person's life (memory arc_<id>_done)
    //   A way out  the main one no longer living there, the other one gone from its heart for a game day: the line just ends. Nothing waits for one who left
    //   In a talk  ?arc <stage> (the one you talk to is the main one, at that stage) · ?arcabout <stage> (…the other one of such a line) · !arc <stage> <from> (only from the stage
    //              that answer is for: two players at one answer, the second moves nothing) · {arc_other} in its words
    //   Words      NpcMoreArcPatterns; places in Talk below (NpcTalk.More)
    //   Console    raven npc arc <who> [<stage> | clear | start <other's name>]
    // Written only where the main one is run; a person's line, not a player's: a second player finds it where it is and whoever answers first moves it.
    // Cheap by rule: who has which line is looked for at a heart at most once in LookEvery s (LineAt); whether it stands, and how late it runs, read every half a second by its
    // mind, are two session sets (_standing, _brave) put right at each look, so its mind never reads its locker or its memory.
    //
    // «Той, хто сховався» (hidden): a fighter holds it against one who hid in a raid (NpcLife.Blamed). Stand by it: at the next raid it does not hide and runs later (Stands,
    // FleeScales: it has no fight of its own). What it then did decides, not chance (RaidStep: its mind's last fight, kept in the line): it stayed, the fighter respects it; it ran, ashamed again, or,
    // if you told it to fight or go, it goes. Speak to the fighter: made up, or «let it show itself». Nobody answers: it fades in a few days.
    internal static class NpcArcs
    {
        internal static readonly int KArc = "RavenWatch_npcArc".GetStableHashCode();
        internal static float LookEvery = 5f, ThemeAsked = 60f, ThemeSaid = 28f, GoneDays = 1f;
        internal static float PeaceBy = 14f, PeaceBack = 8f, WinBy = 22f, WinBack = 10f, FleeLate = 0.3f, FleeBrave = 0.7f, Yes = 0.75f, YesGrumpy = 0.35f, RefusedCosts = 4f;

        internal sealed class Inst { public string Arc = "", Stage = ""; public uint Other; public long Since; public bool Ran; }
        internal sealed class Stage
        {
            public string Theme = "", OtherTheme = "", Then = "";
            public float Days = 3f;
            public bool Stands;
            public Action<NpcActor, NpcActor, Inst> Enter, Step;
        }
        internal static readonly Dictionary<string, Stage> Stages = new Dictionary<string, Stage>();

        private static float Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f; } }

        // ---------------------------------------------------------------- kept
        internal static Inst Of(NpcActor a)
        {
            string s = a != null ? NpcLocker.Get(a, KArc) : null;
            if (string.IsNullOrEmpty(s)) return null;
            string[] w = s.Split('|');
            Inst i = new Inst();
            if (w.Length < 4 || !uint.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out i.Other) || !long.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out i.Since)) return null;
            i.Arc = w[0]; i.Stage = w[1]; i.Ran = w.Length > 4 && w[4] == "ran";
            return i;
        }

        internal static string StageOf(NpcActor a) { Inst i = Of(a); return i != null ? i.Stage : ""; }

        private static void Write(NpcActor a, Inst i)
        {
            _lines.Clear();
            Mark(a, i);
            NpcLocker.Set(a, KArc, i == null ? "" : i.Arc + "|" + i.Stage + "|" + i.Other.ToString(CultureInfo.InvariantCulture) + "|" + i.Since.ToString(CultureInfo.InvariantCulture) + (i.Ran ? "|ran" : ""));
        }

        private static ZDOID Heart(NpcActor a) { return a != null && a.Zdo != null ? NpcKit.GetRef(a.Zdo, NpcResident.KHeart) : ZDOID.None; }

        private static NpcActor BySeed(NpcActor near, uint seed)
        {
            foreach (NpcActor o in NpcHeart.Residents(Heart(near))) if (o.Zdo != null && NpcKit.Seed(o.Zdo) == seed) return o;
            return null;
        }

        // the line at this heart: looked for at most once in LookEvery s (one at a heart at a time)
        private sealed class Line { public NpcActor Main; public Inst I; public float Until; }
        private static readonly Dictionary<ZDOID, Line> _lines = new Dictionary<ZDOID, Line>(); // session: the heart's id now, never kept
        private static Line LineAt(ZDOID heart)
        {
            Line l;
            if (_lines.TryGetValue(heart, out l) && Time.time < l.Until && (l.I == null || l.Main)) return l;
            l = new Line { Until = Time.time + LookEvery };
            foreach (NpcActor o in NpcHeart.Residents(heart))
            {
                Inst i = Of(o);
                if (i != null) { l.Main = o; l.I = i; break; }
            }
            return _lines[heart] = l;
        }

        // the main one of a line in which this one is the other one
        internal static NpcActor MainOf(NpcActor other, out Inst inst)
        {
            inst = null;
            ZDOID heart = Heart(other);
            if (heart.IsNone()) return null;
            Line l = LineAt(heart);
            if (l.I == null || l.Main == other || l.I.Other != NpcKit.Seed(other.Zdo)) return null;
            inst = l.I;
            return l.Main;
        }

        // the other person of a line this one is in, on either side (null: none)
        private static NpcActor Partner(NpcActor a)
        {
            Inst i = Of(a);
            if (i != null) return BySeed(a, i.Other);
            return MainOf(a, out i);
        }

        // ---------------------------------------------------------------- moving on (where the main one is run)
        // from any game: a line begins for «main», with «other» as the other one
        internal static void Begin(NpcActor main, NpcActor other, string arc, string stage)
        {
            if (main == null || other == null || other.Zdo == null) return;
            main.Act("arcstart " + arc + " " + stage + " " + NpcKit.Ref(other.Zdo.m_uid)); // session: the other one by its id now, read at once there
        }

        // why a line cannot begin for it now (null: it can) — the one rule, for the line's start and for the console's word
        private static string WhyNot(NpcActor a, string arc)
        {
            if (!NpcResident.Is(a)) return "не мешканець";
            if (NpcLocker.Get(a, KArc) == null) return "шафка ще не тут";
            if (Of(a) != null) return "у нього вже є лінія";
            if (NpcMemory.Find(a, "arc_" + arc + "_done", 0L) != null) return "цю лінію він уже прожив"; // (a line once in a life)
            foreach (NpcActor o in NpcHeart.Residents(Heart(a))) if (Of(o) != null) return "на оселі вже йде лінія: " + o.Name; // (one at a heart at a time: a base is not a soap opera)
            return null;
        }

        private static void StartAct(NpcActor a, string[] w)
        {
            if (w.Length < 4 || !a.Owned || WhyNot(a, w[1]) != null) return;
            GameObject go = NpcKit.Find(NpcKit.Parse(w[3]));
            NpcActor other = go != null ? go.GetComponent<NpcActor>() : null;
            if (other == null || other == a || other.Zdo == null) return;
            Npcs.Log(a.Id + ": a small line begins: " + w[1] + " (with " + other.Id + ")");
            Go(a, new Inst { Arc = w[1], Other = NpcKit.Seed(other.Zdo) }, w[2], other);
        }

        // «arc <stage> [from]» (from: the stage the one who said it saw; moved on since: nothing)
        private static void StageAct(NpcActor a, string[] w)
        {
            Inst i = w.Length > 1 && a.Owned ? Of(a) : null;
            if (i == null || !Stages.ContainsKey(w[1])) return;
            if (w.Length > 2 && w[2] != i.Stage) return;
            Go(a, i, w[1], BySeed(a, i.Other));
        }

        private static void Go(NpcActor a, Inst i, string stage, NpcActor other)
        {
            Stage s;
            if (string.IsNullOrEmpty(stage) || !Stages.TryGetValue(stage, out s)) { End(a, i); return; }
            i.Stage = stage; i.Since = (long)NpcRescue.Now; i.Ran = false;
            Write(a, i);
            Npcs.Log(a.Id + ": line " + i.Arc + " at " + stage);
            if (s.Enter != null) s.Enter(a, other, i);
        }

        private static void End(NpcActor a, Inst i)
        {
            Write(a, null);
            NpcMemory.Remember(a, "arc_" + i.Arc + "_done", 0L, ""); // (no kind of its own: kept for good)
            Npcs.Log(a.Id + ": line " + i.Arc + " is over");
        }

        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: when it looks next
        private static readonly Dictionary<NpcActor, float> Missing = new Dictionary<NpcActor, float>(); // session: since when the other one is not here
        internal static void Tick(NpcActor a)
        {
            float at;
            if (Next.TryGetValue(a, out at) && Time.time < at) return;
            Next[a] = Time.time + LookEvery;
            if (NpcLocker.Get(a, KArc) == null) return; // (its locker not here yet)
            if (_read.Add(a) && NpcMemory.Find(a, Brave, 0L) != null) _brave.Add(a); // (once a session: its mind reads the set)
            Inst i = Of(a);
            Mark(a, i);
            if (i == null) return;
            Stage s;
            if (!NpcResident.Is(a) || !Stages.TryGetValue(i.Stage, out s)) { LetGo(a, i); return; }
            NpcActor other = BySeed(a, i.Other);
            if (other == null)
            {
                float since; // (out of range is no leaving: on an errand, or a far corner; only a long while is)
                if (!Missing.TryGetValue(a, out since)) Missing[a] = since = Time.time;
                if (Time.time - since > GoneDays * Day) LetGo(a, i);
                return;
            }
            Missing.Remove(a);
            string was = i.Stage;
            if (s.Step != null) { s.Step(a, other, i); if (i.Stage != was || Of(a) == null) return; }
            if (NpcRescue.Now - i.Since > s.Days * Day) Go(a, i, s.Then, other);
        }

        private static void LetGo(NpcActor a, Inst i)
        {
            Write(a, null);
            Missing.Remove(a);
            Npcs.Log(a.Id + ": line " + i.Arc + " let go (someone left)");
        }

        internal static void Forget(NpcActor a) { Next.Remove(a); Missing.Remove(a); _standing.Remove(a); _brave.Remove(a); _read.Remove(a); _raidFrom.Remove(a); }

        // does it stand its ground now (NpcLife does not send it under a roof; it runs later)
        private const string Brave = "arc_hidden_brave";
        private static readonly HashSet<NpcActor> _standing = new HashSet<NpcActor>(), _brave = new HashSet<NpcActor>(), _read = new HashSet<NpcActor>(); // session: read from its locker and its memory at a look
        internal static bool Stands(NpcActor a) { return _standing.Contains(a); }

        private static void Mark(NpcActor a, Inst i)
        {
            Stage s;
            if (i != null && Stages.TryGetValue(i.Stage, out s) && s.Stands) _standing.Add(a); else _standing.Remove(a);
        }

        // a raid it means to stand through: while it lasts, whether it ran (its mind's last fight, a session's) is written into the line — a reload in the middle does not make a brave one of it; over: where that leads
        private static readonly Dictionary<NpcActor, float> _raidFrom = new Dictionary<NpcActor, float>(); // session: from when this game watches its raid (the mind's clock)
        private static void RaidStep(NpcActor a, NpcActor o, Inst i, string ranTo)
        {
            float from;
            if (!_raidFrom.TryGetValue(a, out from)) _raidFrom[a] = from = Time.time;
            bool ran, won;
            if (!i.Ran && NpcMind.FoughtOf(a, out ran, out won) <= Time.time - from && ran) { i.Ran = true; Write(a, i); }
            if (NpcNews.RaidNear(a)) { StandBy(a, o); return; }
            _raidFrom.Remove(a);
            Release(a);
            Go(a, i, i.Ran ? ranTo : "hidden_after_win", o);
        }

        // ---------------------------------------------------------------- «Той, хто сховався»
        private static void Hidden()
        {
            Action<NpcActor, NpcActor, Inst> stand = (a, o, i) => { _raidFrom[a] = Time.time; NpcPhrases.SayOr(a, "arc_hidden_stands", Lang.T("Ні. Цього разу я не ховаюсь.")); StandBy(a, o); };
            Stages["hidden_ashamed"] = new Stage { Theme = "arc_hidden_ashamed", OtherTheme = "arc_hidden_blame", Days = 5f };
            Stages["hidden_ask"] = new Stage { Theme = "arc_hidden_waits", OtherTheme = "arc_hidden_blame", Days = 4f, Then = "hidden_brave" };
            Stages["hidden_after_peace"] = new Stage { Theme = "arc_hidden_peace", OtherTheme = "arc_hidden_peace_other", Days = 1.5f, Enter = Peace };
            Stages["hidden_brave"] = new Stage { Theme = "arc_hidden_ready", Days = 6f, Stands = true, Step = (a, o, i) => { if (NpcNews.RaidNear(a)) Go(a, i, "hidden_brave_raid", o); } };
            Stages["hidden_push"] = new Stage { Theme = "arc_hidden_pushed", Days = 6f, Stands = true, Step = (a, o, i) => { if (NpcNews.RaidNear(a)) Go(a, i, "hidden_push_raid", o); } };
            Stages["hidden_brave_raid"] = new Stage { Days = 1f, Then = "hidden_after_win", Stands = true, Enter = stand, Step = (a, o, i) => RaidStep(a, o, i, "hidden_ran") };
            Stages["hidden_push_raid"] = new Stage { Days = 1f, Then = "hidden_after_win", Stands = true, Enter = stand, Step = (a, o, i) => RaidStep(a, o, i, "hidden_leaves") };
            Stages["hidden_after_win"] = new Stage { Theme = "arc_hidden_wins", OtherTheme = "arc_hidden_respect", Days = 1.5f, Enter = Won };
            Stages["hidden_ran"] = new Stage { Theme = "arc_hidden_ran", OtherTheme = "arc_hidden_blame", Days = 3f };
            Stages["hidden_leaves"] = new Stage { Enter = (a, o, i) => { NpcPhrases.SayOr(a, "arc_hidden_leaves", Lang.T("Я не з тих, хто б'ється. Піду.")); End(a, i); a.Act("unhire"); } };
            NpcMind.FleeScales.Add(a => _standing.Contains(a) ? FleeLate : _brave.Contains(a) ? FleeBrave : 1f); // (it stands longer: for this fight, and a little for good)
        }

        // at the raid it stands by the fighter («станемо разом»: the user's test 2026-11-05 — left to its day it sat on a bench through it), and hits back
        // when they come (fight defend, set by the line on its whim layer: let go with it)
        private const string Whose = "arc";
        internal static float StandFor = 30f;
        private static void StandBy(NpcActor a, NpcActor o)
        {
            if (a.Mind == null) return;
            NpcMind.Plan w = a.Mind.Of(NpcMind.Layer.Whim);
            if (w != null && w.Whose == Whose) { w.Until = Time.time + StandFor; return; } // (on its way already: only longer)
            NpcMind.Plan p = o != null && o.Zdo != null ? new NpcMind.Plan { Kind = NpcMind.Task.Follow, Who = o.Zdo.m_uid, Keep = 3f, Run = true } : new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = a.transform.position };
            if (!a.Mind.TakeWhim(p, StandFor, Whose, true)) return;
            a.Order("fight defend", NpcMind.Layer.Whim, Whose);
            Npcs.Log(a.Id + ": stands by " + (o != null ? o.Id : "itself") + " at the raid");
        }

        private static void Release(NpcActor a) { if (a.Mind != null) a.Order("free", NpcMind.Layer.Whim, Whose); }

        // the other agreed: made up, the quarrel let go
        private static void Peace(NpcActor a, NpcActor o, Inst i)
        {
            NpcFeel.Tell(a, NpcFeel.Joy, 240f);
            if (o == null) return;
            NpcAttitude.TellPerson(o, a, PeaceBy); NpcAttitude.TellPerson(a, o, PeaceBack);
            o.Act("forget quarrel_raid");
        }

        // it stood at the raid: respected, and it keeps that for good
        private static void Won(NpcActor a, NpcActor o, Inst i)
        {
            NpcMemory.Remember(a, Brave, 0L, "");
            _brave.Add(a);
            NpcFeel.Tell(a, NpcFeel.Joy, 420f);
            NpcPhrases.SayOr(a, "arc_hidden_wins", a.Form(Lang.T("Я стояв|стояла! Я не втік|втекла!")));
            if (o == null) return;
            NpcAttitude.TellPerson(o, a, WinBy); NpcAttitude.TellPerson(a, o, WinBack);
            o.Act("forget quarrel_raid");
        }

        // ---------------------------------------------------------------- its words, its talk
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            float w = NpcVoice.Asked ? ThemeAsked : ThemeSaid;
            Stage s;
            Inst i = Of(a);
            if (i != null) { if (Stages.TryGetValue(i.Stage, out s) && s.Theme.Length > 0) themes.Add(new KeyValuePair<string, float>(s.Theme, w)); return; }
            if (MainOf(a, out i) != null && Stages.TryGetValue(i.Stage, out s) && s.OtherTheme.Length > 0) themes.Add(new KeyValuePair<string, float>(s.OtherTheme, w));
        }

        // what you have to do in its line, in the menu of either of them (NpcMenu.Tasks): you promised to speak to the other one
        private static void Tasks(NpcActor a, Player p, List<string> l)
        {
            Inst i = Of(a);
            NpcActor main = i != null ? a : MainOf(a, out i), other = i != null && main == a ? BySeed(a, i.Other) : null;
            if (main == null || i.Stage != "hidden_ask" || !NpcPromises.Has(main, p.GetPlayerID(), "arc")) return;
            l.Add(main == a ? Grammar.Fix(Lang.T("ти обіця{в|ла} поговорити з: ") + (other != null ? other.Name : "…"), p) : Grammar.Fix(Lang.T("ти обіця{в|ла} заступитися за: ") + main.Name, p));
        }

        private static string _reply = "";

        // you speak for the main one to the other: it agrees, or wants to see it in a fight (as NpcAttitude.MakePeace: its patience to you, a grumbler hardly)
        private static void TryAsk(NpcActor other, Player p, string[] w)
        {
            Inst i;
            NpcActor main = MainOf(other, out i);
            if (main == null) { _reply = "…"; return; }
            bool yes = NpcPatience.Of(other, p) >= NpcPatience.Dry && UnityEngine.Random.value < (NpcPhrases.Tones(other).Contains("бурч") ? YesGrumpy : Yes);
            _reply = NpcPhrases.Compose(other, yes ? "arc_hidden_agrees" : "arc_hidden_refuses", p) ?? "…";
            if (!yes) NpcPatience.Spend(other, p, RefusedCosts);
            NpcPromises.Did(main, p, "arc"); // (you spoke for it, as you promised: whatever the other one answers)
            main.Act("arc " + (yes ? "hidden_after_peace" : "hidden_brave") + " " + i.Stage);
            _lines.Clear();
        }

        private const string Ask = "> Не суди так суворо. Людина злякалась, а не зрадила. => c_arc_reply !arctry\n";
        private const string Talk =
            "[talk common +]\n" +
            "@start +\n?? arcabout hidden_ashamed\n" + Ask + "?? arcabout hidden_ask\n" + Ask +
            "@c_arc_hidden_blame\n{concern}\n" + Ask + "> Розумію. => c_heard !patience 3\n> Мені зараз не до того. => end !tone cold\n" +
            "@c_arc_hidden_ashamed\n{concern}\n" +
            "> Усі колись бояться. Наступного разу станемо разом. => c_arc_back !arc hidden_brave hidden_ashamed !attitude 3 !patience 4\n" +
            "> Наступного разу бийся або йди звідси. => c_arc_push !arc hidden_push hidden_ashamed !attitude -5 !patience -5\n" +
            "> Я поговорю, щоб не тримали на тебе зла. => c_arc_ask !arc hidden_ask hidden_ashamed !promise arc !patience 3\n" +
            "> Мені зараз не до того. => end !tone cold\n" +
            "@c_arc_back\n{@arc_hidden_back}\n> Добре. => start\n" +
            "@c_arc_push\n{@arc_hidden_push}\n> Добре. => start\n" +
            "@c_arc_ask\n{@arc_hidden_ask}\n> Добре. => start\n" +
            "@c_arc_reply\n{arc_reply}\n> Добре. => start\n";

        // ---------------------------------------------------------------- wired once (NpcModules)
        internal static void Wire()
        {
            Hidden();
            NpcActor.Acts["arcstart"] = StartAct;
            NpcActor.Acts["arc"] = StageAct;
            NpcActor.Ticks.Add(Tick);
            NpcActor.Unloaded.Add(Forget);
            NpcVoice.Sources.Add(Themes);
            NpcPhrases.ValueSources.Add((a, to, v) => { NpcActor o = Partner(a); if (o != null) v["arc_other"] = o.Name; });
            NpcTalk.More.Add(Talk);
            NpcMenu.Tasks.Add(Tasks);
            NpcTalk.Words["{arc_reply}"] = (a, p) => _reply;
            NpcTalk.Deeds["arctry"] = TryAsk;
            NpcTalk.Deeds["arc"] = (a, p, w) => { if (w.Length > 1) a.Act("arc " + w[1] + " " + (w.Length > 2 ? w[2] : StageOf(a))); _lines.Clear(); };
            Npcs.Commands["arc"] = Command;
        }

        private static string Command(Player p, NpcActor who, string[] a, Vector3 aim)
        {
            if (a.Length < 4) return who.Name + ": " + (NpcLocker.Get(who, KArc) ?? "шафка ще не тут");
            if (a[3] == "clear") { NpcLocker.Set(who, KArc, ""); _lines.Clear(); return "Лінію знято."; }
            if (a[3] == "start" && a.Length > 4)
            {
                NpcActor other = null;
                foreach (NpcActor o in NpcHeart.Residents(Heart(who))) if (o != who && o.Name == a[4]) other = o;
                if (other == null) return "Нема такого серед людей цієї оселі: " + a[4];
                string no = WhyNot(who, "hidden"); // (said, not «begun» when it will not: the user's test 2026-11-05)
                if (no != null) return "Лінію не почато: " + no + ".";
                Begin(who, other, "hidden", "hidden_ashamed");
                return "Лінію почато.";
            }
            if (!Stages.ContainsKey(a[3])) return "raven npc arc <хто> [<етап> | clear | start <ім'я другого>]";
            who.Act("arc " + a[3]);
            return "Сказано.";
        }

        internal static void Register()
        {
            StoryCond.Register("arc", new StoryCond.Kind { Usage = "arc <етап>   (мала лінія цієї людини на цьому етапі)", Min = 1, Test = (p, w) => StageOf(NpcTalk.Him) == w[1] });
            StoryCond.Register("arcabout", new StoryCond.Kind { Usage = "arcabout <етап>   (ця людина — другий у малій лінії на цьому етапі)", Min = 1, Test = (p, w) => { Inst i; return MainOf(NpcTalk.Him, out i) != null && i.Stage == w[1]; } });
        }
    }
}
#endif

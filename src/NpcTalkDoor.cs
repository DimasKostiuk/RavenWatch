#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // You are not the centre of it (the user 2026-11-08): a person may not want to talk, and what weighs on it comes before what you want. Each by its nature
    // (NpcNature.TalkBy: the column Talk of its row — door, insist, grudge; a plain one by its ways). And it is said why (the user: «зараз взагалі незрозуміло
    // що відбувається»): a line under its words (RavenBook.TalkNote), the grey row's tip, its patience bar with its reasons — the window's own parts, no new look.
    //   the door     its talk's first screen is shut (Shut): its patience to you below Out × door, it is angry with you (NpcPatience: you caused it), it is hostile to
    //                you, or it ended a talk with you itself a while ago (row o in your character: WalkedFor game hours × door). Then: its short word door_<why>,
    //                what it must still do with you (Essential: hire, let go, its request, its rescue, the story), «Вибач» (once a talk: a chance by its nature and how
    //                it feels about you — yes, the door opens and it is a little kinder; no, its word door_sorry_no), «Бувай». A note says why and, for a walk-off, when
    //   its own first a worry pressing for it (Insist: InsistAt ÷ insist, lower when its mood is low) — «твоє» goes grey («Спершу вислухай»), only the essential stays.
    //                A lesser one you may pass by for your own topic: that is ignoring it (Brushes: its «потім про це» words)
    //   ignored      its opening worry left unanswered when the talk closes (MarkIgnored): a little of its patience (× grudge), a note, row i in your character —
    //                next time it weighs more, not less (IgnoredWeighs, NpcVoice.Concerns), and it opens with a reproach (concern_ignored); a second time running, it
    //                remembers it (brushed_off). Answered — the row goes
    //   the margins  what the talk scroll shows of it (Relation → RavenBookTalkRel): its state now with its word, its patience to you with its feeling's word, the threads
    internal static partial class NpcTalk
    {
        internal static float InsistAt = 50f, WalkedFor = 1f, IgnoredFor = 12f, IgnoredMore = 0.4f, IgnoredCost = 2f, SorryChance = 0.5f, SorryWarm = 5f, SorryCost = 2f;
        private const string DoorPlace = "\u0001door";
        private static bool _doorOpened, _sorryTried, _doorNoted; // (this talk: an apology took · was tried · why it is shut said)
        private static string _doorWhy;

        private static float Way(NpcActor a, string key) { return NpcNature.TalkBy(a, key); }

        // a thread between it and you (NpcThreads.State: 1 tied, −1 cut, 0 none) — what it changes here is said in NpcThreads
        private static int Bond(Player p, string kind) { return With != null && p != null ? NpcThreads.State(With, p.GetPlayerID(), kind) : 0; }
        private static bool Promises(Answer a) { return a.Acts.Exists(x => x.StartsWith("promise ", StringComparison.Ordinal)); }
        private static float DoorWay(NpcActor a, Player p) { return Way(a, "door") * (Bond(p, "life") < 0 ? NpcThreads.CutDoor : 1f); }

        // why its door is shut to you now (null: open)
        private static string Shut(NpcActor a, Player p, float toYou)
        {
            if (_doorOpened || a == null || p == null || !Minded || NpcRescueTalk.State(a) != null) return null;
            if (Bond(p, "life") > 0) { if (!_doorNoted && Shut0(a, p, toYou) != null) { RavenBook.TalkNote(a.Form(Grammar.Fix(Lang.F("{0} не в настрої, але тобі не відмовить: ти врятува{{в|ла}} йому|їй життя.", a.Name), p)), true); _doorNoted = true; } return null; } // (the thread «життя»)
            return Shut0(a, p, toYou);
        }

        private static string Shut0(NpcActor a, Player p, float toYou)
        {
            Row o = FindRow(p, "o", a, "");
            if (o != null && !Elsewhere(o) && NowSec - o.At < WalkedFor * DoorWay(a, p) * GameHour) return "walked";
            if (NpcAttitude.Level(a, p) == "hostile") return "hostile";
            long by;
            if (NpcPatience.Irk(a, out by) >= NpcPatience.Gate && by == p.GetPlayerID()) return "angry";
            if (toYou >= 0f && toYou < NpcPatience.Out * DoorWay(a, p)) return "patience"; // (only how it is with you, the showing's own reckoning: NpcTalk.Show)
            return null;
        }

        // what a person must do with you whatever its mood (NpcPatience: never shut) — and the way out
        private static bool Essential(Answer a)
        {
            if (Duties.Contains(a.To) || a.To == "end" || a.To == Homes) return true;
            return IsStory(a) || LeadsToStory(a.To) || a.Acts.Exists(x => x.StartsWith("giverequest", StringComparison.Ordinal) || x.StartsWith("goalgive", StringComparison.Ordinal) || x.StartsWith("hire", StringComparison.Ordinal));
        }

        private static void ShowDoor(Player p, string why, RavenBook.TalkRel rel)
        {
            _doorWhy = why;
            Node start = _talk.Nodes["start"];
            string says = (_prefix != null ? _prefix + " " : "") + (NpcPhrases.Compose(With, "door_" + why, p) ?? With.Form(Lang.T("Не зараз.")));
            _prefix = null; _youSaid = null;
            if (!_doorNoted) { RavenBook.TalkNote(DoorNote(why, p), false); _doorNoted = true; } // (why, once: the window's line)
            List<RavenBook.TalkRow> rows = new List<RavenBook.TalkRow>();
            List<Answer> ans = new List<Answer>();
            foreach (Answer a in start.Answers) if (Essential(a) && a.To != "end") AddRow(rows, ans, a, p, 2);
            if (!_sorryTried && why != "walked")
            {
                Answer sorry = new Answer { Text = Lang.T("Вибач. Я не хоті{в|ла} тебе образити."), To = "start" };
                sorry.Acts.Add("doorsorry");
                ans.Add(sorry); rows.Add(new RavenBook.TalkRow { Text = Grammar.Fix(sorry.Text, p), Ring = 2 });
            }
            Answer bye = new Answer { Text = Lang.T("Бувай."), To = "end" };
            ans.Add(bye); rows.Add(new RavenBook.TalkRow { Text = bye.Text, Ring = 2 });
            List<Answer> keep = ans; List<RavenBook.TalkRow> keepRows = rows;
            RavenBook.ShowTalk(With.Name, RavenBook.Says(NpcBody.Spoken(With, says)), rows, i => Pick(keep, keepRows, i, DoorPlace), Close, rel, null, false, null);
        }

        // the note: why it will not talk, and for a walk-off when it will
        private static string DoorNote(string why, Player p)
        {
            string s;
            switch (why)
            {
                case "walked": s = Lang.F("{0} не хоче говорити: щойно сам|сама закінчив|закінчила розмову з тобою. Спробуй за ~{1} год.", With.Name, Mathf.Max(1, Mathf.CeilToInt(WalkedFor * DoorWay(With, p)))); break;
                case "hostile": s = Lang.F("{0} не хоче говорити: ставиться до тебе вороже.", With.Name); break;
                case "angry": s = Lang.F("{0} не хоче говорити: розізлився|розізлилася саме на тебе. Злість минає з часом.", With.Name); break;
                default: s = Lang.F("{0} не хоче говорити: терпіння саме до тебе вичерпане (чому — наведи на лінію «до тебе»).", With.Name); break;
            }
            return With.Form(Grammar.Fix(s, p));
        }

        // «!doorsorry»: once a talk — a chance by its ways and how it feels about you
        private static void DoorSorry(NpcActor a, Player p, string[] w)
        {
            _sorryTried = true;
            float chance = SorryChance / DoorWay(a, p) * (_doorWhy == "hostile" ? 0.3f : _doorWhy == "angry" ? 0.6f : 1f) * Mathf.Lerp(0.7f, 1.3f, Mathf.InverseLerp(-50f, 50f, NpcAttitude.Of(a, p.GetPlayerID())));
            if (UnityEngine.Random.value < Mathf.Clamp(chance, 0.05f, 0.9f))
            {
                _doorOpened = true; _doorWhy = null;
                NpcPatience.Spend(a, p, -SorryWarm);
                _prefix = NpcPhrases.Compose(a, "door_sorry_yes", p);
                RavenBook.TalkNote(a.Form(Lang.F("{0}: погодився|погодилася вислухати", a.Name)), true);
                return;
            }
            NpcPatience.Spend(a, p, SorryCost);
            _prefix = NpcPhrases.Compose(a, "door_sorry_no", p);
        }

        // ---------------------------------------------------------------- its own first
        // how heavy a worry must be for it to come before anything of yours: by its nature, sooner when it feels low
        private static float InsistOf(NpcActor a, Player p) { return InsistAt / Mathf.Max(0.3f, Way(a, "insist") * (Bond(p, "time") < 0 ? NpcThreads.CutInsist : 1f)) * (NpcResident.Is(a) && NpcToil.Mood(a) < 30f ? 0.85f : 1f); }
        private static bool Insists(Player p) { return _top != null && !_topDone && _greeted && Bond(p, "time") <= 0 && _top.Weight >= InsistOf(With, p); } // (the thread «час»: it trusts you to come back to it)

        // ---------------------------------------------------------------- ignored (row i in your character)
        internal static bool IgnoredLately(NpcActor a, Player p, string theme) { Row r = FindRow(p, "i", a, theme); return r != null && !Elsewhere(r) && NowSec - r.At < IgnoredFor * GameHour; }
        internal static float IgnoredWeighs(NpcActor a) { return 1f + IgnoredMore * Way(a, "grudge"); }

        // the talk closes with its opening worry unanswered
        private static void MarkIgnored(Player p)
        {
            if (p == null || With == null || _top == null || _topDone || !_greeted || !Minded || _doorWhy != null || _alerted || Senses.HurtByFoeAt > _openedAt || ScenePlaying) return; // (cut off by a foe or a scene: not yours; stood there silent: NpcTalkIdle has its own price)
            bool again = IgnoredLately(With, p, _top.Theme);
            Row r = PutRow(p, "i", With, _top.Theme);
            r.Count = again ? r.Count + 1 : 1; r.At = NowSec;
            SaveRows(p);
            NpcPatience.Spend(With, p, IgnoredCost * Way(With, "grudge"));
            if (again) NpcMemory.Tell(With, "brushed_off", p.GetPlayerID(), ""); // (a second time running: it remembers it)
            RavenBook.TalkNote(With.Form(Grammar.Fix(Lang.F("{0}: ти не вислуха{{в|ла}} про своє — це запам'ятається", With.Name), p)), false);
            Npcs.Log(With.Id + ": its worry «" + _top.Theme + "» left unanswered" + (again ? " again" : ""));
        }

        // answered now: no longer held against you
        private static void Answered(Player p)
        {
            Row r = _top != null ? FindRow(p, "i", With, _top.Theme) : null;
            if (r == null) return;
            Rows(p).Remove(r); SaveRows(p);
        }

        // the walk-off kept (ShowOut): its door shut to you a while
        private static void WalkedOff(Player p)
        {
            Row r = PutRow(p, "o", With, "");
            r.At = NowSec; SaveRows(p);
            _doorWhy = "walked"; // (it ended it: not also «you did not listen»)
            RavenBook.TalkNote(DoorNote("walked", p), false);
        }

        // how it is with you, for the scroll's margins (RavenBookTalkRel): its state now, its patience to you with its feeling's word, the threads between you
        private static RavenBook.TalkRel Relation(Player p, string tip, float now, float toYou)
        {
            RavenBook.TalkRel r = new RavenBook.TalkRel { Now = now, ToYou = toYou, Tip = tip, NowWord = NpcPatience.NowWord(now), YouWord = NpcAttitude.Adjective(With, NpcAttitude.Level(With, p)) };
            r.Knots.AddRange(NpcThreads.Knots(With, p));
            return r;
        }

        private static void WireDoor()
        {
            Deeds["doorsorry"] = DoorSorry;
        }
    }
}
#endif

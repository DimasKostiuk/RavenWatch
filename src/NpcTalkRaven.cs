#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Your raven on your shoulder butts into your talk with a person (the user 2026-11-05: people hear it, «такий собі діалог зненацька»; docs/plans/raven-in-talk.md). The window's part:
    // a scene's own raven lines are its step «say hugin|munin <words>», the same idea with no window.
    // Your ravens are your own thought and memory (story-lore.md): Hugin says what you think now, Munin what you forgot, neither knows anyone's secret. No branch, stage or condition may
    // hang on such a line: the raven may be away, and then there simply is none. A written line asks what a scene's «say» asks (Shoulder.Present: the prologue's raven is still asleep, the story speaks through it all the
    // same); a made one a raven awake and yours, the book yours too (RavenHere: one asleep has no mind to butt in with).
    //   two ways     aside  only you hear it: the person goes on as if nothing was said (a doubt, a memory: the story)
    //                aloud  it hears: its reaction under the raven's words (raven_said), your answers to it above the others, and it keeps it in mind (NpcMemory «raven_mocked»)
    //   written      a line in a place of a talk, said once for each character, at most one a place:  ~hugin aside: Занадто легко вона про мерців.   (~hugin · ~munin · ~any; conditions:  ~munin aside ?step q s: …)
    //   made         only with a settler, aloud, by the ravens' own engine (their trust, their mouths; book NpcAsidePatterns, tags ppl_<group of what it speaks of>): one roll a talk (CutChance),
    //                not again soon from this person (CutEvery), never over its own (Personal: what it told you as one it trusts), one in trouble, a repeat, or another raven's words
    //   your answers by the tone core (NpcTalkTone): Замовкни — you stood up for it (warm; the raven has a word about it) · Він має рацію — you mock it with the
    //                raven (rude, Munin's harder: AgreeHugin, AgreeMunin; the raven is pleased: Trust.Talked) · Не зважай — nothing · none of them, another answer
    //                picked — it takes your silence for a yes, a little (Unanswered: angry, no feeling moved). Your raven minds it too: shut up, it loses
    //                trust (ShutTrust, even a full bar: Trust.Offend), brushed off a little (SkipTrust), agreed with, it gains (Trust.Talked); each said in the window
    // The window: its lines (RavenBook.TalkSay: SayRaven, SayAside), after the person's first one. Its voice: RavenVoice; the ravens' own chatter waits for it (Director.Spoke).
    internal static partial class NpcTalk
    {
        internal sealed class RavenLine { public string Who = "any", Text = ""; public bool Aloud; public List<Cond> When = new List<Cond>(); }

        internal static float CutChance = 0.25f, CutEvery = 300f, CutHurt = 4f, AgreeHugin = 0.7f, AgreeMunin = 1f, ShutTrust = 20f, SkipTrust = 10f; // (the trust your raven loses when you shut it up, or brush it off)
        private static bool _cutRolled;              // this talk has had its chance of a made line
        private static RavenBook.TalkSay _cutNext;   // the raven's word after your answer to it: on the next showing
        private static readonly Dictionary<uint, float> CutAfter = new Dictionary<uint, float>(); // session: a person's seed → when its next one may come
        private static readonly HashSet<string> CutPlaces = new HashSet<string> { "how", "home", "folk", "ravens", "wish", "talk" }; // (small talk: a person's worry and opening count too, what you do does not)

        // «~hugin aside: words» · «~munin aloud ?cond, cond: words»
        private static void ParseRaven(Node n, string line, List<string> errors)
        {
            int colon = line.IndexOf(':');
            string[] head = colon > 0 ? line.Substring(1, colon - 1).Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries) : new string[0];
            if (head.Length < 2 || (head[0] != "hugin" && head[0] != "munin" && head[0] != "any") || (head[1] != "aside" && head[1] != "aloud")) { errors.Add("«" + line + "»: ~hugin|munin|any aside|aloud: words"); return; }
            RavenLine r = new RavenLine { Who = head[0], Aloud = head[1] == "aloud", Text = line.Substring(colon + 1).Trim() };
            if (head.Length > 2) r.When.AddRange(StoryCond.Parse(head[2].TrimStart('?').Trim(), errors));
            n.Ravens.Add(r);
        }

        // which raven may speak now: on your shoulder, awake, yours, and you can hear it at all (RavenBook.Given)
        private static bool RavenHere(Player p, string who, out bool munin)
        {
            bool h = who != "munin" && EmoteTalk.Free(p, false), m = who != "hugin" && EmoteTalk.Free(p, true);
            munin = h && m ? UnityEngine.Random.value < 0.5f : m;
            return (h || m) && RavenBook.Given(p);
        }

        // a written line's raven: on your shoulder, as a scene's «say» asks
        private static bool Present(Player p, string who, out bool munin)
        {
            bool h = who != "munin" && Shoulder.Present(p, false), m = who != "hugin" && Shoulder.Present(p, true);
            munin = h && m ? UnityEngine.Random.value < 0.5f : m;
            return h || m;
        }

        // a showing's raven: a written line of this place not yet said to you, else perhaps a made one
        private static List<RavenBook.TalkSay> Cut(Node n, string node, string worry, bool opening, List<RavenBook.TalkRow> rows, List<Answer> ans, Player p)
        {
            bool munin;
            for (int i = 0; i < n.Ravens.Count; i++)
            {
                RavenLine r = n.Ravens[i];
                string key = ReadKey(_talk.Id, "raven_" + n.Id + "_" + i);
                if (Story.Flag(p, key) || !StoryCond.All(p, r.When) || !Present(p, r.Who, out munin)) continue;
                Story.SetFlag(p, key, true);
                return Speak(p, munin, Grammar.Fix(Lang.T(r.Text), p), r.Aloud, rows, ans, node);
            }
            return Made(node, worry, opening, rows, ans, p);
        }

        private static List<RavenBook.TalkSay> Made(string node, string worry, bool opening, List<RavenBook.TalkRow> rows, List<Answer> ans, Player p)
        {
            bool munin;
            if (_cutRolled || !Minded || Plugin.Instance == null || !(opening || worry != null || CutPlaces.Contains(node)) || NpcRescueTalk.State(With) != null) return null;
            if (Cozy.Quieted) { CutWhy("another raven is speaking"); return null; }
            string theme = worry ?? (opening && _top != null ? _top.Theme : null);
            NpcVoice.Group g = theme != null ? NpcVoice.GroupOf(theme) : null;
            if (g != null && g.Personal) { CutWhy("its own (" + g.Id + ")"); return null; } // (what it told you as one it trusts: not to be mocked)
            float after;
            uint seed = SeedOf(With);
            if (!RavenHere(p, "any", out munin)) { CutWhy(WhyNoRaven(p)); return null; }
            if (CutAfter.TryGetValue(seed, out after) && Time.time < after) { CutWhy("not again so soon"); return null; }
            _cutRolled = true;
            if (UnityEngine.Random.value >= CutChance) { CutWhy("the roll"); return null; }
            string tag = CutTag(theme, g, munin);
            string line = Plugin.Instance.ComposeTagged(p, munin, tag);
            if (string.IsNullOrEmpty(line)) { CutWhy("no words for " + tag); return null; }
            if (CutAfter.Count > 128) CutAfter.Clear();
            CutAfter[seed] = Time.time + CutEvery;
            NpcMemory.Tell(With, RavenMocked, p.GetPlayerID(), "");
            return Speak(p, munin, line, true, rows, ans, node);
        }

        internal const string RavenMocked = "raven_mocked";

        // its words for what the person speaks of: the theme's own (ppl_hurt, ppl_hungry…; low_hungry as hungry), else its group's, else any
        private static string CutTag(string theme, NpcVoice.Group g, bool munin)
        {
            if (theme != null)
            {
                if (Patterns.Has(Plugin.Instance.PhraseDir, munin, "ppl_" + theme)) return "ppl_" + theme;
                if (theme.StartsWith("low_", StringComparison.Ordinal) && Patterns.Has(Plugin.Instance.PhraseDir, munin, "ppl_" + theme.Substring(4))) return "ppl_" + theme.Substring(4);
            }
            return g != null && Patterns.Has(Plugin.Instance.PhraseDir, munin, "ppl_" + g.Id) ? "ppl_" + g.Id : "ppl_any";
        }

        // why it kept out, in the log (a line a showing at most, only in a talk: the answer to «why was it silent?»)
        private static void CutWhy(string why) { Npcs.Log(With.Id + ": no raven in this talk: " + why); }

        private static string WhyNoRaven(Player p)
        {
            if (!RavenBook.Given(p)) return "no book yet";
            ItemDrop.ItemData h = RavenSeat.On(p, false), m = RavenSeat.On(p, true);
            if (h == null && m == null) return "none on the shoulder";
            ItemDrop.ItemData r = h ?? m;
            return !Trust.Active(r) ? "asleep" : Trust.Stranger(r, p) ? "not yours" : Trust.Away(r) ? "away" : "?";
        }

        private static List<RavenBook.TalkSay> Speak(Player p, bool munin, string line, bool aloud, List<RavenBook.TalkRow> rows, List<Answer> ans, string node)
        {
            Voice(p, munin, line);
            List<RavenBook.TalkSay> cut = new List<RavenBook.TalkSay> { new RavenBook.TalkSay { Kind = aloud ? RavenBook.SayRaven : RavenBook.SayAside, Name = Plugin.RavenName(munin), Text = line, Munin = munin } };
            string reaction = aloud ? NpcPhrases.Compose(With, "raven_said", p) : null;
            if (string.IsNullOrEmpty(reaction)) return cut; // (it says nothing to it, as the story's own people: nothing to answer either)
            cut.Add(new RavenBook.TalkSay { Kind = RavenBook.SayPerson, Text = NpcBody.Spoken(With, reaction) });
            Reply(rows, ans, 0, Lang.T(munin ? "Замовкни, Муніне." : "Замовкни, Хугіне."), "shut", munin, node);
            Reply(rows, ans, 1, Lang.T("Він має рацію."), "agree", munin, node);
            Reply(rows, ans, 2, Lang.T("Не зважай на нього."), "skip", munin, node);
            return cut;
        }

        // your answers to it: first of all, and back to where you were (its words about it in place of the place's own)
        private static void Reply(List<RavenBook.TalkRow> rows, List<Answer> ans, int at, string text, string kind, bool munin, string node)
        {
            Answer a = new Answer { Text = text, To = node };
            a.Acts.Add("ravenreply " + kind + " " + (munin ? "1" : "0"));
            ans.Insert(at, a);
            rows.Insert(at, new RavenBook.TalkRow { Text = text, Ring = 5 });
        }

        private static void Voice(Player p, bool munin, string line)
        {
            RavenVoice.Say(p.gameObject, munin, RavenVoice.VoiceOf(p, munin, p.gameObject), line, s => With != null); // (no card over it: its words are in the window)
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line)); // (the other chatter waits for it)
        }

        // the raven's words had answers and you picked none of them: it takes your silence for a yes, a little
        private static void Unanswered(List<Answer> ans, Answer picked, Player p)
        {
            if (With == null || picked.Acts.Exists(IsReply) || !ans.Exists(x => x.Acts.Exists(IsReply))) return;
            NpcPatience.Irritate(With, p, CutHurt * Hurt(With, true));
            Npcs.Log(With.Id + ": your raven's words went unanswered: taken as a yes");
        }

        // your raven hurt by your word: its trust down, said in the window
        private static void RavenHurt(Player p, bool munin, float points)
        {
            bool top;
            float lost = Trust.Offend(RavenSeat.On(p, munin), points, out top);
            if (lost > 0f) RavenBook.TalkNote(Lang.F("{0}: довіра до тебе падає", Plugin.RavenName(munin)), false);
            else if (top) RavenBook.TalkNote(Lang.F("{0} образився: дні на найвищій довірі почнуться заново", Plugin.RavenName(munin)), false);
        }

        private static bool IsReply(string act) { return act.StartsWith("ravenreply ", StringComparison.Ordinal); }

        // «ravenreply <shut|agree|skip> <1 munin|0 hugin>»
        private static void RavenReply(NpcActor a, Player p, string[] w)
        {
            if (w.Length < 3) return;
            bool munin = w[2] == "1";
            string tag = null;
            switch (w[1])
            {
                case "shut": _instead = NpcPhrases.Compose(a, "raven_shut", p); Tone(a, p, new[] { "tone", "warm" }); RavenHurt(p, munin, ShutTrust); tag = "ppl_shut"; break;
                case "agree": _instead = NpcPhrases.Compose(a, "raven_agree", p); Tone(a, p, new[] { "tone", "rude", (munin ? AgreeMunin : AgreeHugin).ToString(System.Globalization.CultureInfo.InvariantCulture) }); if (Trust.Talked(munin) > 0f) RavenBook.TalkNote(Lang.F("{0}: довіра до тебе росте", Plugin.RavenName(munin)), true); tag = "ppl_agree"; break;
                default: _instead = NpcPhrases.Compose(a, "raven_skip", p); RavenHurt(p, munin, SkipTrust); tag = "ppl_skip"; break;
            }
            if (tag == null || Plugin.Instance == null) return;
            string line = Plugin.Instance.ComposeTagged(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return;
            Voice(p, munin, line);
            _cutNext = new RavenBook.TalkSay { Kind = RavenBook.SayAside, Name = Plugin.RavenName(munin), Text = line, Munin = munin };
        }
    }
}
#endif

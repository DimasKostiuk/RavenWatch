#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The author's jump (the window «Сюжет»: «→» by a stage, «Пройти етап зараз», «Почати з початку», «Стерти»; the console: raven story jump|start|forget):
    // the story put AS IF IT HAD BEEN WALKED to that stage (the user 2026-11-14: «щоб я в будь-який момент міг натиснути кнопку певного етапу і всі
    // залежності на сервері перерахувались згідно цього»). Till then a jump wrote the stage's word alone and left the world as it was, so the stage was
    // left at once by the world's own answers: a quest's fork «world heart_first -> lantern» in a world with a heart, «gate hall open -> inside» with the
    // gate open, «hearth hall 1 -> fire_seen» with the fire burning; the server's census set «heart_first» again half a minute after it was taken off;
    // «Пройти етап зараз» ran no «done:» of the stage it passed; a server's quest whose «who:» did not hold for the author stood still with no word why.
    //
    // How: nothing is replayed. What the story leaves behind is already written beside each action and condition — its line «Dry» (StoryActions.Kind,
    // StoryCond.Kind: «set world world:{1}», «set world gate:{1}={2}», «order {1} hold», «world hearth:{1} : …»; tools/quest_dry.js walks a quest by the
    // same lines with no game). So the jump reckons the facts as they would stand, and writes each fact once:
    //   1. every fact the quest's data names (its stages' actions, its scenes' «do» lines, what its waits and forks ask of the world) — off;
    //      the same for the quests that come after it (those whose «starts:» / «who:» name it), which are forgotten — and for those that begin at
    //      a stage of it («starts: reached <it> <stage>») when the jump goes back before that stage (StageStarted)
    //   2. the quests it comes after (named by its «starts:» / «who:»: «quest prologue done») walked whole — their facts on, themselves done, their block's own keys («scratch:») off as at their real end (Scratched)
    //   3. the stages on the way to the target (its «next:» line; a fork only when no other way leads there; of a stage's ways the one chosen, else
    //      the first that leads there): their «start:», their scene's «do» lines, what their «wait:» asked of the world, their «done:» — on;
    //      and the target's own «start:», which then runs for real as on any entering
    //   4. each fact made or unmade by its kind (Makers below: a new kind of fact is a new entry; a module's own answer to «the author took it
    //      back» is Taken — the first heart's census). A fact this game cannot make (a bed given, a boss killed) or take back (the book in the
    //      bag) is SAID in the track's log, never passed over in silence
    //   5. the quest's own word: a personal one — its saga's entries of the stages passed, its scenes unbegun, then Enter as ever; the server's —
    //      its saga's entries of the stages passed in the author's own character (he is the one «there»: StoryQuestsServer.cs, Witnessed), its
    //      scenes unbegun in the world, then the forced ask the server grants to an admin (StoryQuestsServer.Asked)
    // One write a fact: a game that is not the server reads its own writes back only when the server has sent them round, so «off, then on» of one key
    // in a row would end as «off». That is why the facts are reckoned first and written after.
    // What it does not turn back, and says so where it matters: things in the bag and in the world (the book, a raven, a heart built, a thing dropped),
    // what a character remembers of its talks (a branch asked «once»), the tales people tell (StoryTales). A place's own state (the Hall's «гейд»,
    // «розпалена») is set to its first and its «becomes:» rules bring it on by the facts, behind the scenes as ever.
    // Cost: the author's click only — a pass over one quest's data, a few world keys written.
    internal static partial class StoryQuests
    {
        internal sealed class Wish // (internal: a module may put its own maker of a fact into Makers)
        {
            public bool On;
            public string Act;  // the action that makes it, as written (a fact with words of its own: a hold's place, a spot's rings); null — made by its kind
            public int Level;   // a hearth's fire asked for · a mark's age (minutes, days) asked by a wait passed
        }

        // a fact made or unmade: (player, quest, the fact's name, the wish) → null, or what could not be done (said in the log)
        internal static readonly Dictionary<string, Func<Player, QuestDef, string, Wish, string>> Makers = new Dictionary<string, Func<Player, QuestDef, string, Wish, string>>();
        // a module's own answer when the author sets a world flag it keeps by itself (StoryHeart: its census must not set the first heart again)
        internal static readonly Dictionary<string, Action<bool>> Taken = new Dictionary<string, Action<bool>>();
        private static bool _makersWired;

        private static string Did(Player p, QuestDef q, Wish w) { if (w.Act != null) StoryActions.Do(p, q, w.Act); return null; }

        private static void WireMakers()
        {
            if (_makersWired) return;
            _makersWired = true;
            Add("world world", (p, q, n, w) => { StoryWorld.SetFlag(n, w.On); Action<bool> t; if (Taken.TryGetValue(n, out t)) t(w.On); return null; });
            Add("world wvar", (p, q, n, w) => { if (!w.On) StoryWorld.SetVar(n, 0); else if (w.Act != null) StoryActions.Do(p, q, w.Act); else if (StoryWorld.Var(n) == 0) StoryWorld.SetVar(n, 1); return null; });
            Add("world gate", (p, q, n, w) => { Loc l = Locs.Get(n); if (l == null) return "брама: нема місця «" + n + "»"; LocState.SetGate(l, w.On); return null; });
            Add("world hearth", (p, q, n, w) =>
            {
                Loc l = Locs.Get(n);
                if (l == null) return "вогнище: нема місця «" + n + "»";
                if (w.On && w.Act != null) { StoryActions.Do(p, q, w.Act); return null; } // (an action set it — «hearth <place> 2»: as it said)
                int want = w.On ? Mathf.Max(1, w.Level) : LocHearth.Cold;
                if (!w.On || LocHearth.LevelOf(l) < want) LocHearth.SetLevel(l, want);
                return null;
            });
            Add("world state", (p, q, n, w) =>
            {
                Loc l = Locs.Get(n);
                if (l == null || w.On || ZoneSystem.instance == null) return null; // (on: its own «becomes:» rules bring it there by the facts)
                if (LocState.StateOf(l) == LocState.Abandoned && LocState.SubOf(l) == 0 && !LocState.ShutOf(l)) return null;
                LocState.Put(l, LocState.Abandoned, 0);
                ZoneSystem.instance.RemoveGlobalKey(l.KeyNext);
                return "місце «" + l.Id + "»: стан повернуто до першого — далі його доведуть власні правила місця (becomes:), щойно поруч нікого не буде";
            });
            Add("world wstamp", (p, q, n, w) => { StoryTrackConds.DayAgo(n, w.On ? w.Level : -1); return null; });
            Add("world wclock", (p, q, n, w) => { StoryTrackConds.ClockAgo(n, w.On ? w.Level : -1); return null; }); // («wsince x 5»: put five minutes back)
            Add("world found", (p, q, n, w) =>
            {
                if (!w.On) { StoryWorld.Forget(n, true); return null; } // (this one place and its chest: the whole name — StoryWorld.Forget)
                return StoryWorld.Text(StoryFound.Found + n).Length > 0 ? null : Did(p, q, w); // (a place already picked stays the one picked)
            });
            Add("world stash", (p, q, n, w) => // (stash, lay, plant: off — every «once» of them at that place undone, so the stage that comes again does it again; what was put stays)
            {
                if (!w.On) { StoryFound.Unstash(n); return null; }
                return Did(p, q, w);
            });
            Add("world npcbed", (p, q, n, w) => w.On ? "ліжко для «" + n + "» дай сам (коло Q людини): цього стрибок не робить" : null);
            Add("world placed", (p, q, n, w) => null);  // (the world places it by itself)
            Add("world boss", (p, q, n, w) => w.On ? "бос «" + n + "»: ключ гри стрибок не ставить" : null);
            Add("world key", (p, q, n, w) => w.On ? "ключ гри «" + n + "» стрибок не ставить" : null);
            Add("me flag", (p, q, n, w) => { Story.SetFlag(p, n, w.On); return null; });
            Add("me var", (p, q, n, w) => { if (!w.On) Story.SetVar(p, n, 0); else if (w.Act != null) StoryActions.Do(p, q, w.Act); else if (Story.Var(p, n) == 0) Story.SetVar(p, n, 1); return null; });
            Add("me stamp", (p, q, n, w) => { StoryTrackConds.StampAgo(p, n, w.On ? w.Level : -1); return null; });
            Add("me spot", (p, q, n, w) => { Vector3 at; return w.On && !StoryWhere.Find("me." + n, out at) ? Did(p, q, w) : null; }); // (a spot of his own already given stays where it is)
            Add("me chose", (p, q, n, w) =>
            {
                if (w.On) return Did(p, q, w);
                string pre = ChoseFlag + n + ".";
                foreach (string f in Story.Of(p).Flags.ToArray()) if (f.StartsWith(pre, StringComparison.Ordinal)) Story.SetFlag(p, f, false);
                return null;
            });
            Add("me book", (p, q, n, w) =>
            {
                if (w.On) return RavenBook.Given(p) ? null : Did(p, q, w);
                return RavenBook.Given(p) ? "книга воронів лишається в тебе: речей із торби стрибок не забирає" : null;
            });
            Add("me own", (p, q, n, w) => null);   // (a raven of his own: a thing, as the book)
            Add("me been", (p, q, n, w) => null);  // (what he has seen, read, asked, been told stays his)
            Add("me read", (p, q, n, w) => null);
            Add("me asked", (p, q, n, w) => null);
            Add("me knows", (p, q, n, w) => null);
            Add("order", (p, q, who, w) =>
            {
                string[] aw = null;
                if (w.Act != null) StoryActions.KindOf(w.Act, out aw);
                string verb = w.On && aw != null && aw.Length > 2 ? aw[2] : "";
                if (verb == "hold") { StoryActions.LeadClear(who); StoryActions.Do(p, q, w.Act); return null; }
                StoryHold.Clear(who);
                if (verb == "follow") { StoryActions.Do(p, q, w.Act); return null; }
                StoryActions.LeadClear(who); // (free, stay, or no word at all: nobody's, at its own day)
                NpcActor a = StoryScenes.Actor(who, p.transform.position);
                if (a != null && !StoryScenes.InCast(a)) { a.Order("look none", NpcMind.Layer.Scene); a.Order("free", NpcMind.Layer.Scene); }
                return null;
            });
        }
        private static void Add(string kind, Func<Player, QuestDef, string, Wish, string> make) { if (!Makers.ContainsKey(kind)) Makers[kind] = make; } // (a module's own, put there before, stays)

        // ---------------------------------------------------------------- a line «Dry» read as a fact
        private static string Fill(string s, string[] w) { for (int i = 1; i < w.Length && i < 10; i++) s = s.Replace("{" + i + "}", w[i]); return s; }

        // «gate:hall=open» → «gate:zala», on (a place by its own id, whatever word of it was written); «…=shut» turns it round
        private static string Named(string scope, string f, ref bool on)
        {
            int eq = f.IndexOf('=');
            if (eq > 0) { string val = f.Substring(eq + 1); f = f.Substring(0, eq); if (val == "shut") on = !on; }
            int c = f.IndexOf(':');
            string kind = c > 0 ? f.Substring(0, c) : f, name = c > 0 ? f.Substring(c + 1) : "";
            if (kind == "gate" || kind == "hearth" || kind == "state") { Loc l = Locs.Get(name); if (l != null) name = l.Id; }
            return scope + " " + kind + (name.Length > 0 ? ":" + name : "");
        }

        // what an action leaves behind (false: nothing a jump keeps — it moves the quest, raises an event, plays a scene, shows a line)
        private static bool FactOf(string act, out string fact, out bool on)
        {
            fact = null; on = true;
            string[] w;
            StoryActions.Kind k = StoryActions.KindOf(act, out w);
            if (k == null || string.IsNullOrEmpty(k.Dry)) return false;
            string[] d = k.Dry.Split(' ');
            if (d[0] == "order" && d.Length >= 3) { fact = "order " + Fill(d[1], w); return true; }
            if ((d[0] != "set" && d[0] != "clear") || d.Length < 3) return false;
            on = d[0] == "set";
            fact = Named(d[1], Fill(d[2], w), ref on);
            return true;
        }

        // what a condition asks of the world or of the character (false: the player or time brings it about, or it asks the quests themselves)
        private static bool FactOf(Cond c, out string fact, out bool on)
        {
            fact = null; on = !c.Not;
            string dry = c.Kind != null ? c.Kind.Dry : null;
            if (string.IsNullOrEmpty(dry)) return false;
            int how = dry.IndexOf(" : ", StringComparison.Ordinal);
            if (how > 0) dry = dry.Substring(0, how);
            string[] d = dry.Split(' ');
            if (d.Length < 2) return false;
            if (d[0] == "after") { fact = Named("me", Fill(d[1], c.A), ref on); return true; } // («since <mark> N»: the mark, so old)
            if (d[0] != "me" && d[0] != "world") return false;
            fact = Named(d[0], Fill(d[1], c.A), ref on);
            return true;
        }

        // the number a condition compares with («hearth hall 1», «days ch1_heart >= 2»): false — it asks for less than a number («< 2»), nothing to make
        private static bool Number(Cond c, out int n)
        {
            n = 0;
            if (c.A.Length < 3) return false;
            string s = string.Join("", c.A, 2, c.A.Length - 2).Trim();
            if (s.StartsWith("<", StringComparison.Ordinal)) return false;
            bool over = s.StartsWith(">", StringComparison.Ordinal) && !s.StartsWith(">=", StringComparison.Ordinal);
            s = s.TrimStart('>', '=');
            int dot = s.IndexOf('.');
            if (dot > 0) s = s.Substring(0, dot);
            if (!int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)) return false;
            if (over) n++;
            return true;
        }

        // A quest walked to its end forgets its block's own world keys, as its real end does (StoryQuestsServer: «scratch:» → StoryWorld.Forget):
        // its facts named with one of its prefixes are off, not on (2026-11-17: a jump to chapter 2 walked chapter 1 whole and brought back ch1_home,
        // ch1_torv_home… that its end had taken away). Reckoned here, not forgotten after the writes: a game that is not the server sees its own
        // writes only when the server sends them round (the head of this file). The name as the key bears it after its kind's word: «world
        // world:ch1_home», «world wstamp:ch1_heart» (t_ch1_heart), «world found:ch1_glade», «world stash:found.ch1_thistle:…»
        private static void Scratched(QuestDef b, Dictionary<string, Wish> want)
        {
            if (b.Scratch.Length == 0) return;
            string[] pre = b.Scratch.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (KeyValuePair<string, Wish> kv in want)
            {
                int colon = kv.Key.IndexOf(':');
                if (!kv.Value.On || colon <= 0 || !kv.Key.StartsWith("world ", StringComparison.Ordinal)) continue;
                string name = kv.Key.Substring(colon + 1).ToLowerInvariant();
                if (name.StartsWith("found.", StringComparison.Ordinal)) name = name.Substring(6);
                foreach (string x in pre) if (name.StartsWith(x, StringComparison.Ordinal)) { kv.Value.On = false; break; }
            }
        }

        // ---------------------------------------------------------------- the facts of a quest
        private static void Wished(Dictionary<string, Wish> want, string fact, Wish w)
        {
            want[fact] = w;
            if (fact.StartsWith("world gate:", StringComparison.Ordinal) || fact.StartsWith("world hearth:", StringComparison.Ordinal))
            {
                string state = "world state:" + fact.Substring(fact.IndexOf(':') + 1);
                if (!want.ContainsKey(state)) want[state] = new Wish(); // (a place whose gate or fire the story moves: its own state follows them — set back, its rules bring it on)
            }
        }

        private static void Acts(Dictionary<string, Wish> want, List<string> acts, bool walked)
        {
            foreach (string a in acts)
            {
                string fact; bool on;
                if (!FactOf(a, out fact, out on)) continue;
                Wished(want, fact, walked ? new Wish { On = on, Act = a } : new Wish());
            }
        }

        private static void Asks(Dictionary<string, Wish> want, List<Cond> conds, bool walked)
        {
            if (conds == null) return;
            foreach (Cond c in conds)
            {
                string fact; bool on;
                if (!FactOf(c, out fact, out on)) continue;
                if (!walked) { if (!want.ContainsKey(fact)) Wished(want, fact, new Wish()); continue; }
                int n; bool num = Number(c, out n);
                if (on && !num && c.A.Length >= 3 && (fact.Contains(" hearth:") || fact.Contains(" wstamp:") || fact.Contains(" wclock:") || fact.Contains(" stamp:"))) continue; // («hearth zala < 2», «days x < 3»: nothing to make)
                Wish had;
                if (on && want.TryGetValue(fact, out had) && had.On) { had.Level = Mathf.Max(had.Level, num ? n : 0); continue; } // (made by an action before: only as high, as old as this wait asked)
                Wished(want, fact, new Wish { On = on, Level = num ? n : 0 });
            }
        }

        // a scene's «do» lines (walked: as its stage is passed; else every fact they name); its other lasting steps are not a jump's to make
        private static void SceneActs(Player p, Dictionary<string, Wish> want, string scene, bool walked, List<string> notes)
        {
            StoryScenes.SceneDef sc = scene.Length > 0 ? StoryScenes.Find(scene) : null;
            if (sc == null) return;
            List<string> acts = new List<string>(), other = new List<string>();
            foreach (StoryScenes.Step st in sc.Steps)
            {
                if (st.W.Length == 0) continue;
                if (st.W[0] == "do") { if (!walked || st.If == null || StoryCond.All(p, st.If)) acts.Add(st.Text.Substring(2).Trim()); }
                else if (walked && st.Kind != null && st.Kind.Lasting && !other.Contains(st.W[0])) other.Add(st.W[0]);
            }
            Acts(want, acts, walked);
            if (!walked) return;
            foreach (string who in sc.Cast) // (a scene that takes a person ends the «follow» it cut across: StoryActions.LeadOff)
            {
                Wish w; string[] aw;
                if (want.TryGetValue("order " + who, out w) && w.On && w.Act != null && StoryActions.KindOf(w.Act, out aw) != null && aw.Length > 2 && aw[2] == "follow") want["order " + who] = new Wish();
            }
            if (notes != null && other.Count > 0) notes.Add("сцена «" + scene + "»: кроки «" + string.Join("», «", other.ToArray()) + "» (річ, двері, свій ворон) стрибок не відтворює");
        }

        // every fact a quest's data names: off (what the walk passes turns on again)
        private static void AllOff(Player p, QuestDef q, Dictionary<string, Wish> want)
        {
            foreach (Fork f in q.Forks) Asks(want, f.When, false);
            foreach (StageDef s in q.Stages)
            {
                Acts(want, s.Start, false); Acts(want, s.Done, false);
                Asks(want, s.Wait, false);
                foreach (Fork f in s.Forks) Asks(want, f.When, false);
                SceneActs(p, want, s.Scene, false, null);
                foreach (StageDef w in s.Ways) { Asks(want, w.Wait, false); Acts(want, w.Done, false); }
            }
        }

        // ---------------------------------------------------------------- the way to a stage
        private sealed class Leg { public StageDef S, Way; public Fork Via; } // (a stage passed: by its way, or left by a fork — then its scene, wait and «done:» were never passed)
        private sealed class Edge { public string To; public StageDef Way; public Fork Via; }

        // the stages passed on the way from the quest's first to the target («end»: all of them), in order; null — no way there in its data
        private static List<Leg> PathTo(Player p, QuestDef q, string target, bool forks)
        {
            Dictionary<string, Leg> came = new Dictionary<string, Leg>(); // stage → the leg that leads into it
            Dictionary<string, string> from = new Dictionary<string, string>();
            Queue<string> open = new Queue<string>();
            open.Enqueue(q.First); from[q.First] = "";
            while (open.Count > 0)
            {
                string id = open.Dequeue();
                if (id == target) break;
                StageDef s = q.Stage(id);
                if (s == null) continue;
                List<Edge> next = new List<Edge>();
                if (s.Ways.Count > 0)
                {
                    string chose = Chosen(p, ChoiceOf(q, s));
                    foreach (StageDef w in s.Ways) if (w.Id == chose) next.Add(new Edge { To = w.Next, Way = w }); // (the way he went first)
                    foreach (StageDef w in s.Ways) if (w.Id != chose) next.Add(new Edge { To = w.Next, Way = w });
                }
                else next.Add(new Edge { To = s.Next });
                if (forks)
                {
                    foreach (Fork f in s.Forks) next.Add(new Edge { To = f.To, Via = f });
                    foreach (Fork f in q.Forks) next.Add(new Edge { To = f.To, Via = f });
                }
                foreach (Edge n in next)
                {
                    if (n.To.Length == 0 || from.ContainsKey(n.To)) continue;
                    from[n.To] = id; came[n.To] = new Leg { S = s, Way = n.Way, Via = n.Via };
                    open.Enqueue(n.To);
                }
            }
            if (!from.ContainsKey(target)) return forks ? null : PathTo(p, q, target, true); // (a stage only a fork leads to: «inside», for a world whose gate stands open)
            List<Leg> path = new List<Leg>();
            for (string at = target; at != q.First && came.ContainsKey(at); at = from[at]) path.Insert(0, came[at]);
            return path;
        }

        // the facts as they stand once the quest is walked to the target (the target's own «start:» among them: it runs for real after, and finds them so)
        private static List<Leg> Walk(Player p, QuestDef q, string target, Dictionary<string, Wish> want, List<string> notes)
        {
            List<Leg> path = PathTo(p, q, target, false);
            if (path == null) { notes.Add(q.Id + ": до «" + target + "» у файлі нема дороги від «" + q.First + "» — світ лишився без пройдених етапів"); return null; }
            foreach (Leg l in path)
            {
                Acts(want, l.S.Start, true);
                if (l.Via != null) { Asks(want, l.Via.When, true); continue; } // (left by a fork — «inside», for a world whose gate stands open: what the fork asked holds)
                SceneActs(p, want, l.S.Scene, true, notes);
                Asks(want, l.S.Wait, true);
                if (l.Way != null) { Asks(want, l.Way.Wait, true); Acts(want, l.Way.Done, true); }
                Acts(want, l.S.Done, true);
            }
            StageDef t = q.Stage(target);
            if (t != null) Acts(want, t.Start, true);
            return path;
        }

        // ---------------------------------------------------------------- the quests before and after
        private static void QuestsNamed(List<Cond> l, List<string> into)
        {
            if (l != null) foreach (Cond c in l) if (!c.Not && c.A.Length > 1 && c.A[0] == "quest" && !into.Contains(c.A[1])) into.Add(c.A[1]);
        }

        // the quests it comes after (its «starts:», its «who:»), theirs first
        private static void Before(QuestDef q, List<QuestDef> into, int depth)
        {
            if (depth > 16) return;
            List<string> ids = new List<string>();
            QuestsNamed(q.Starts, ids); QuestsNamed(q.Who, ids);
            foreach (string id in ids)
            {
                QuestDef b = Find(id);
                if (b == null || b == q || into.Contains(b)) continue;
                Before(b, into, depth + 1);
                if (!into.Contains(b)) into.Add(b);
            }
        }

        // the quests that come after it — of a server's quest only: the world's story does not hang on one character's own quest (an admin trying his own prologue
        // on a shared server must not wipe the world's), and a personal quest has none of its kind after it yet
        private static List<QuestDef> After(QuestDef q)
        {
            List<QuestDef> l = new List<QuestDef>();
            if (!q.Server) return l;
            foreach (QuestDef d in Quests)
            {
                if (d == q) continue;
                List<QuestDef> b = new List<QuestDef>();
                Before(d, b, 0);
                if (b.Contains(q)) l.Add(d);
            }
            return l;
        }

        // the quests that begin at a stage of it («starts: reached ch2 lit_more»: Eyvind's beside Lit's, chapter 2) — and theirs after them: forgotten
        // too when the jump goes back before that stage (2026-11-18: a jump to ch2 lit_day left ch2_eyvind «done», so his mushrooms never came again).
        // A jump to that stage or past it leaves them as they stand: one begun runs beside it, as in the game. Its order is the file's, as «reached» reckons
        private static void StageStarted(QuestDef q, string target, List<QuestDef> into)
        {
            if (!q.Server) return;
            int at = target.Length == 0 ? -1 : target == "end" ? int.MaxValue : q.Stages.IndexOf(q.Stage(target));
            foreach (QuestDef d in Quests)
            {
                if (d == q || into.Contains(d) || d.Starts == null) continue;
                foreach (Cond c in d.Starts)
                {
                    if (c.Not || c.A.Length < 3 || (c.A[0] != "reached" && c.A[0] != "step") || c.A[1] != q.Id) continue;
                    StageDef s = q.Stage(c.A[2]);
                    if (s != null && q.Stages.IndexOf(s) <= at) continue;
                    into.Add(d);
                    foreach (QuestDef e in After(d)) if (!into.Contains(e) && e != q) into.Add(e);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- the jump
        private static void Unbegun(Player p, QuestDef q)
        {
            foreach (StageDef s in q.Stages)
            {
                if (s.Scene.Length == 0) continue;
                if (q.Server) { if (StoryWorld.Var("scene." + s.Scene) != 0) StoryWorld.Remove("scene." + s.Scene); }
                else Story.SetVar(p, "scene." + s.Scene, 0, true);
            }
        }

        private static void Blank(Player p, QuestDef q)
        {
            Unbegun(p, q);
            Story.ForgetSaga(p, q.Id); // (a quest of the server's too: what the author's character was «there» for)
            if (q.Server) { StoryWorld.SetText(KeyOf(q), ""); return; }
            Story.SetQuest(p, q.Id, ""); Story.SetStage(p, q.Id, "");
        }

        private static void Sagas(Player p, QuestDef q, List<Leg> path)
        {
            if (path == null) return;
            foreach (Leg l in path)
            {
                if (l.Via != null) continue; // (a fork past it writes no entry, as in the game)
                if (l.Way != null) Choose(p, ChoiceOf(q, l.S), l.Way.Id);
                Lived(p, q, l.S, null);
                if (l.Way != null) Lived(p, q, l.S, l.Way);
            }
        }

        // the story as if walked to that stage of that quest («end»: the quest done); «» — as if it had never begun
        internal static void Rebuild(Player p, QuestDef q, string target)
        {
            Load();
            if (p == null || q == null) return;
            if (target.Length > 0 && target != "end" && q.Stage(target) == null) { Note(q.Id + ": нема етапу «" + target + "» — нічого не змінено"); return; }
            if (ZNet.instance != null && !ZNet.instance.LocalPlayerIsAdminOrHost())
            {
                bool world = q.Server;
                List<QuestDef> near = new List<QuestDef>(); Before(q, near, 0); near.AddRange(After(q)); StageStarted(q, target, near);
                foreach (QuestDef d in near) world |= d.Server;
                if (world) { Note(q.Id + ": стрибок зачіпає сюжет сервера, а тебе нема в його adminlist.txt — сервер його не прийме; нічого не змінено"); return; }
            }
            WireMakers();
            try
            {
                if (StoryScenes.Playing.Length > 0) StoryScenes.Stop("автор перебудовує сюжет");
                StoryScenes.AuthorFresh();
                List<string> notes = new List<string>();
                Dictionary<string, Wish> want = new Dictionary<string, Wish>();
                List<QuestDef> before = new List<QuestDef>(), after = After(q);
                StageStarted(q, target, after);
                if (target.Length > 0) Before(q, before, 0); // (forgotten: «as if it had never begun» — the quests before it stand as they are)
                foreach (QuestDef d in after) AllOff(p, d, want);
                AllOff(p, q, want);
                foreach (QuestDef b in before) AllOff(p, b, want);
                Dictionary<QuestDef, List<Leg>> walked = new Dictionary<QuestDef, List<Leg>>();
                foreach (QuestDef b in before) { walked[b] = Walk(p, b, "end", want, notes); Scratched(b, want); }
                List<Leg> path = target.Length > 0 ? Walk(p, q, target, want, notes) : null;

                int made = 0, unmade = 0;
                foreach (KeyValuePair<string, Wish> kv in want)
                {
                    string fact = kv.Key;
                    int sp = fact.IndexOf(' '), colon = fact.IndexOf(':');
                    string kind = fact.StartsWith("order ", StringComparison.Ordinal) ? fact.Substring(0, sp) : colon > 0 ? fact.Substring(0, colon) : fact;
                    string name = kind == "order" ? fact.Substring(sp + 1) : colon > 0 ? fact.Substring(colon + 1) : "";
                    Func<Player, QuestDef, string, Wish, string> make;
                    if (!Makers.TryGetValue(kind, out make)) { if (kv.Value.On) notes.Add("факт «" + fact + "» стрибок робити не вміє (нового виду: рядок у StoryQuests.Makers)"); continue; }
                    try { string why = make(p, q, name, kv.Value); if (why != null) notes.Add(why); if (kv.Value.On) made++; else unmade++; }
                    catch (Exception e) { notes.Add("факт «" + fact + "»: " + e.Message); Failed("rebuild " + fact, e); }
                }

                foreach (QuestDef d in after) Blank(p, d);
                foreach (QuestDef b in before)
                {
                    if (State(p, b) == Story.Done) continue;
                    Unbegun(p, b);
                    Story.ForgetSaga(p, b.Id); Sagas(p, b, walked[b]);
                    if (b.Server) Ask(b, At(p, b), "end", false, true);
                    else { Story.SetQuest(p, b.Id, Story.Done); Story.SetStage(p, b.Id, ""); }
                    notes.Add("квест «" + b.Id + "» перед цим вважається пройденим");
                }
                Note(q.Id + ": автор → " + (target.Length > 0 ? target : "(не почато)") + " · світ і персонаж як після пройденого: фактів увімкнено " + made + ", вимкнено " + unmade
                    + (after.Count > 0 ? ", квестів після нього стерто " + after.Count : ""));
                foreach (string n in notes) Note("  · " + n);

                if (target.Length == 0) { Blank(p, q); return; }
                Unbegun(p, q);
                Story.ForgetSaga(p, q.Id);
                Sagas(p, q, path);
                if (q.Server) { Ask(q, At(p, q), target, false, true); return; }
                if (Story.Quest(p, q.Id) != Story.Active) Story.SetQuest(p, q.Id, Story.Active);
                Enter(p, q, target);
            }
            catch (Exception e) { Failed("rebuild " + q.Id, e); Note(q.Id + ": перебудова зламалась — " + e.Message + " (подробиці в лозі)"); }
        }

        // «Пройти етап зараз»: on to its next, the stage it stands at counted as passed (its «done:», what its wait asked of the world)
        internal static void PassNow(Player p, QuestDef q)
        {
            StageDef cur = q.Stage(At(p, q));
            if (cur == null) { Note(q.Id + ": не стоїть на жодному етапі — нема що проходити"); return; }
            Rebuild(p, q, cur.Next);
        }
    }

    internal static partial class StoryScenes
    {
        // the author rebuilt the story: no scene is «just stopped» nor «told to wait» any more (a scene stopped is not tried again for Retry s — the stage he jumped to would stand that long)
        internal static void AuthorFresh() { _stopped.Clear(); _toldNeeds = null; _toldMissing = null; }
    }
}
#endif

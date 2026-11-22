#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The story's track (story-plan.md, part 10): quests from text files, each a row of stages, each stage waits for
    // something and then goes on. Runs beside Odin's errands, never in their way. A character's own: where it stands is
    // kept in Story (personal); what the world already did is StoryWorld — and every stage may fork on it.
    //
    // Files: assets/story/quests/*.txt — read where StoryFiles says (built in; on the author's machine the mod's own folder,
    // edited in F9 «Сюжет»).
    //
    //   [quest prologue]
    //   name: Пролог
    //   chapter: prologue                the saga's chapter its entries go into (StorySaga; needed once a stage has «saga:»)
    //   starts: hall                     conditions (StoryCond) — the quest starts by itself once they hold
    //   first: settle
    //   scratch: ch1_                   a quest of the server: the world keys of its own block (marks, finds, chests: their names begin so, or have it
    //                                    after a word of their kind — t_ch1_heart, found_ch1_house) are taken away when it is over: ended by its own «next: end» (it may be told
    //                                    on: Resume) — once a quest begun after it begins (CloseBefore); ended any other way — at once; what later blocks,
    //                                    the people and the book ask is never named so (storycheck). docs/jobs/story-data.md «Блоки сюжету»
    //   nudge: raven_nudge 12            a stage with a mark standing this many minutes of real play: this scene plays (nothing is
    //                                    skipped, the scene only shows the way: place «goal.mark»), again after twice as long, then twice
    //                                    that, at most NudgeMost times a stage (the user 2026-11-07: it called on and on), then quiet till the next stage
    //
    //   [stage settle]
    //   goal: Освойся                    the line shown (the panel now, the tracker later)
    //   more: …                          a longer word on it, only in the book («Сага» → «Зараз», under the goal): why and how, where the goal is short
    //   fork: gate hall open -> inside   checked on entering and while waiting: holds → straight to that stage
    //   mark: hall 30 Зала Варти         the golden «!» on the map while this stage lasts (place, ring metres, label). Several lines — several
    //                                    marks at once, places to choose from (chapter 2, the user 2026-11-18: Eyvind gone, two places to look; the
    //                                    first one come to is empty): «wait: near <a> 25» and «fork: near <b> 25 -> <stage>» say where each leads.
    //                                    A label of its own, or the goal; «goal.mark» is the nearest of them. Its ring is gold; a stage that starts
    //                                    a fight («waves») or waits for its end has the game's red raid ring (StageDef.Fight; the user 2026-11-18)
    //   start: msg …                     actions on entering, split by «;»
    //   wait: played 10, calm            passes once all hold …
    //   on: use eirik_carving            … or once this event comes (with wait: both)
    //   scene: raven_call                … or plays the scene and passes when it ends
    //   done: gate hall open; flag x     actions on passing
    //   next: call                       the next stage, or «end» (the quest done); none = the next one in the file
    //   call: heid                       the person to talk to now: a thread of gold over its head (StoryCallMark), for this player alone
    //   seek: found.ch1_stash lost      a search: the raven on the shoulder is the finder of that place while this stage lasts — caws, says warmer or
    //                                    colder in the words of that search (StorySeek: tags seek_<words>_warmer|_colder); the stage's ring on the map
    //                                    stands off that place (SeekOff), so it says where to look, not where it is
    //   stock: found.ch2e_pechera 50 Troll 1 Pickable_Mushroom_yellow 1-4 what the stage sends you to find there is there when you come (StoryStock):
    //                                    a creature made at the game's own spawner, things to pick grown again or new round them
    //   road: torvald 12 torvald_home   a person who talks to you on the way (StoryRoad): while the stage stands and you are within m of it, the talks
    //                                    of «[road <name>]» (assets/story/meets) one after another, each once a character; who does not walk with it hears nothing
    //   lost: <item> found.ch1_stash    a thing of the story this stage waits for, on the ground there: gone (a server's cleaning, another mod) while you are
    //                                    near and do not carry it — laid there again (StoryLost); storycheck asks it of a stage that waits for a thing laid on the ground
    //   saga: Хтось тебе кликав.         the book's «Сага» entry once the stage is passed (not when a fork skips it): kept in
    //                                    the character in the order lived (Story.AddSaga), so what came before the book is there too.
    //                                    A quest of the server: to those who were there — StoryQuestsServer.cs, Witnessed
    //
    // Ways — the player's own choice (the user 2026-11-02): a stage may hold several [way <id>] blocks instead of its own
    // wait/on/scene. All of them stand at once (each its goal line and map mark); the first one done wins (file order
    // when two pass together), the rest are gone. The choice is kept in the character (condition «chose <choice> <way>»,
    // event «way <choice>.<way>»), so any later quest, scene or line may ask which way he went.
    //
    //   [stage start]
    //   choice: prologue                 the choice's name (none = the quest's id; two choosing stages in one quest name theirs)
    //   done: …                          the stage's own done runs after the way's, whichever way it was
    //   [way munin]
    //   goal: Лети за вороном            a way knows goal, mark, wait, on, done, next (none = the stage's next), saga
    //   mark: hall 30 Зала Варти
    //   wait: near hall 25
    //   next: to_hall
    internal static partial class StoryQuests
    {
        internal sealed class Fork { public List<Cond> When; public string To = "", Text = ""; }

        internal sealed class StageDef
        {
            public string Music = ""; // «music: off | <track> [vol V]»: while the stage stands, the player hears that (StoryStageMusic)
            public string Road = ""; // «road: <who> <m> <talks>»: that person talks to you on the way while you are near it (StoryRoad)
            public string Id = "", Goal = "", More = "", Scene = "", Next = "", OnName = "", OnArgs = "", WaitText = "", MarkAt = "", MarkLabel = "", Choice = "", Saga = "", Call = "", Seek = "";
            public float MarkRadius;
            public bool Fight; // (it starts a fight — StoryActions.Kind.Fight — or waits for one's end: its ring on the map is the game's red, not gold; read once: MarkFights, StoryQuestsRead.cs)
            public List<Cond> Wait;
            public readonly List<Fork> Forks = new List<Fork>();
            public readonly List<string> Start = new List<string>(), Done = new List<string>();
            public readonly List<string> Lost = new List<string>(); // «lost: <item> <place>»: the thing laid there again while this stage stands, if gone (StoryLost)
            public readonly List<string> Stock = new List<string>(); // «stock: <place> <m> <Prefab> <N> …»: at least that many there when you come (StoryStock: a troll cave's troll, its mushrooms)
            public readonly List<StageDef> Ways = new List<StageDef>(); // (a way is a small stage of its own: goal, mark, wait, on, done, next)
            public readonly List<StageDef> Marks = new List<StageDef>(); // (its second «mark:» line and on: places to choose from, each its own mark — its wait and forks say where each leads; a quest of the server has no ways)
        }

        internal sealed class QuestDef
        {
            public string Id = "", Name = "", First = "", StartsText = "", From = "", Chapter = "", NudgeScene = "", Scratch = ""; // (Scratch: «scratch:» — the prefixes of its own world keys, gone when it is over: StoryQuests.CloseBefore)
            public int NudgeMinutes;
            public int Block; // (the story's block of its file, read once: StoryFiles.Block — ChapterNow)
            public List<Cond> Starts;
            public bool Server;   // («owner: server»: one stage for the whole world, StoryQuestsServer)
            public List<Cond> Who; // («who:»: the players it leads)
            public readonly List<Fork> Forks = new List<Fork>(); // (a server quest's own forks: on every stage)
            public string Raw, AtNow = "", OwedFrom = "", Latch = "", EndedAt = ""; // session: the world's word on it as last read · the stage this game has seen passed
            public readonly List<KeyValuePair<string, float>> Left = new List<KeyValuePair<string, float>>(); public string Mine = "", NearStage = "", WorldOf; public float NearAt; // session, of one world (WorldOf): stages with «saga:» the world has just passed, and when this game saw it · the last one the server granted this game itself (OnGo) · the last one this player stood by the place of, and when (Witnessed)
            public bool Owed, OwedRan; // (actions granted and not run yet: the world's word)
            public float AskUntil, ClaimUntil, ClaimAskedAt, ActAskAt; // session: an answer awaited from the server · this game's claim on its scene
            public readonly List<StageDef> Stages = new List<StageDef>();
            internal StageDef Stage(string id) { foreach (StageDef s in Stages) if (s.Id == id) return s; return null; }
        }

        internal static readonly List<QuestDef> Quests = new List<QuestDef>();
        internal static readonly List<string> Errors = new List<string>();
        internal static readonly List<string> Recent = new List<string>(); // what the track did (the panel)
        internal static int Version;

        private static bool _loaded;
        private static float _next, _startNext;
        private static readonly List<StoryEvent> _heard = new List<StoryEvent>(), _now = new List<StoryEvent>(); // (heard since the last pass · this pass's)
        internal static string Folder { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "story"); } }

        // a quest file's text as it is used now (the author's copy, else the built-in one); saved as his copy and read again
        internal static string FileText(string file) { return StoryFiles.Text(StoryFiles.Quests, Folder, Blueprint.On, file); }
        internal static string SaveFile(string file, string text) { string why = StoryFiles.Save(StoryFiles.Quests, Folder, file, text); if (why == null) Reload(); return why; }

        internal static QuestDef Find(string id) { Load(); foreach (QuestDef q in Quests) if (q.Id == id) return q; return null; }

        // ---------------------------------------------------------------- running
        // the modules' own looks (StoryModules fills them; the core names none): Frame — every frame, after the scene; Each — twice a
        // second on every game, after the stages (StoryTales: a moment of the world; StoryHush: the quiet rounds; StoryCallMark: whom to talk to)
        // A module that throws rests alone (Parts): it never stops the stages
        internal static readonly Parts Frame = new Parts("Story frame");
        internal static readonly Parts Each = new Parts("Story pass");

        // every frame (RavenSkills): the played minutes, the scene, the stages twice a second
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || ZNet.instance == null) { StoryScenes.Tick(); return; } // (left the world in a scene: stopped now, its people and camera given back — not played on after the next login)
            Load();
            StoryTrackConds.Count(p);
            StoryScenes.Tick();
            Frame.Run(p);
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            _now.Clear(); _now.AddRange(_heard); _heard.Clear(); // (what is raised during this pass waits for the next one)
            RunOwed();
            if (Time.time >= _startNext) { _startNext = Time.time + 2f; StartWaiting(p); }
            foreach (QuestDef q in Quests)
            {
                try
                {
                    if (q.Server) ServerStep(p, q);
                    else if (Story.Quest(p, q.Id) == Story.Active) Step(p, q);
                    else if (Story.Quest(p, q.Id) == Story.Done) Resume(p, q); // (the story told on since it ended here)
                    Mark(p, q);
                }
                catch (Exception e) { Failed(q.Id + " pass", e); } // (one quest's fault: the others and the modules go on)
            }
            ShowGoal(p);
            Each.Run(p);
        }

        // said once per place and fault, not twice a second
        private static readonly HashSet<string> _failed = new HashSet<string>();
        private static void Failed(string where, Exception e) { if (_failed.Add(where + ": " + e.Message)) Story.Log("track: " + where + " failed: " + e); }

        // the stage this player stands at in an active quest (null: not active, or a stage gone from the file)
        internal static StageDef StageNow(Player p, QuestDef q) { return State(p, q) == Story.Active && Leads(p, q) ? q.Stage(At(p, q)) : null; }

        // a stage of this player's (or a way of it) waits for this very event now and would pass on it — its «wait:» holds too (Passed:
        // «on:» and «wait:» both). A touch it waits for is its, not the place's «elsedo:»; E it would not pass on is not taken from
        // the thing's own use (a heart's board, a person's talk) for nothing
        internal static bool Awaits(Player p, string name, string arg) { return Awaiting(p, name, arg) != null; }

        // that stage (or way) itself: its goal is the words of the E it takes (StoryHeart: «[E] <goal>»); null — none
        internal static StageDef Awaiting(Player p, string name, string arg)
        {
            StoryEvent e = new StoryEvent { Name = name, Arg = arg ?? "" };
            foreach (QuestDef q in Quests)
            {
                StageDef s = StageNow(p, q);
                if (s == null || (q.Server && q.Owed)) continue; // (a quest of the server whose stage's actions are not run yet hears nothing: ServerStep — a press taken then was lost, the thing's own use too)
                if (s.OnName.Length > 0 && StoryBus.Matches(e, s.OnName, s.OnArgs) && StoryCond.All(p, s.Wait)) return s;
                foreach (StageDef w in s.Ways) if (w.OnName.Length > 0 && StoryBus.Matches(e, w.OnName, w.OnArgs) && StoryCond.All(p, w.Wait)) return w;
            }
            return null;
        }

        // the scene the stages after this one come to (a few steps on: way → touch the shield → the scene); null — none
        internal static StoryScenes.SceneDef Leads(QuestDef q, string next)
        {
            for (int i = 0; i < 3; i++)
            {
                StageDef n = q.Stage(next);
                if (n == null) return null;
                if (n.Scene.Length > 0) return StoryScenes.Find(n.Scene);
                next = n.Next;
            }
            return null;
        }

        // what the player is to do now: the first active quest and its stage (false: nothing) — the screen's goal line, the book's «Сага»
        internal static bool Now(Player p, out QuestDef q, out StageDef s)
        {
            Load();
            foreach (QuestDef d in Quests)
            {
                s = StageNow(p, d);
                if (s == null) continue;
                q = d;
                return true;
            }
            q = null; s = null;
            return false;
        }

        // every quest the player stands in now, in the files' order — the chapter's own first, a quest beside it after (the user
        // 2026-11-17: Lit's chest and Eyvind's mushrooms at once, both goals in sight). One list, filled anew at each ask: read it at once
        // (the screen twice a second, the book when it draws). Now — its first
        internal struct Standing { internal QuestDef Quest; internal StageDef Stage; }
        private static readonly List<Standing> _active = new List<Standing>();
        internal static List<Standing> NowAll(Player p)
        {
            Load();
            _active.Clear();
            if (p == null) return _active;
            foreach (QuestDef d in Quests) { StageDef s = StageNow(p, d); if (s != null) _active.Add(new Standing { Quest = d, Stage = s }); }
            return _active;
        }

        // a stage the player stands at now (any of his quests) plays a scene as soon as it may
        internal static bool WantsScene(Player p)
        {
            foreach (Standing a in NowAll(p))
            {
                if (a.Stage.Scene.Length == 0) continue;
                if (!a.Quest.Server) return true;
                StoryScenes.SceneDef sc = StoryScenes.Find(a.Stage.Scene); // (a stage of the world may stand long with its scene for someone else: not about to start here unless it could)
                if (sc != null && StoryScenes.Near(p, sc) && StoryCond.All(p, sc.Needs)) return true;
            }
            return false;
        }

        // a stage's goal in words, its ways side by side (translated)
        internal static string GoalText(StageDef s)
        {
            if (s.Ways.Count == 0) return Lang.T(s.Goal);
            List<string> l = new List<string>();
            foreach (StageDef w in s.Ways) if (w.Goal.Length > 0) l.Add("· " + Lang.T(w.Goal));
            return (s.Goal.Length > 0 ? Lang.T(s.Goal) + "\n" : "") + string.Join("\n", l.ToArray());
        }

        // where the quest's mark stands on the map now — of several (its ways', its places to choose from), the one nearest the player: false — no mark
        internal static bool MarkAt(QuestDef q, StageDef s, out Vector3 at)
        {
            Player p = Player.m_localPlayer;
            return MarkNear(q, s, p != null ? p.transform.position : Vector3.zero, out at);
        }
        // … the one nearest that point (a heart's board: the place by this settlement — NpcBoard)
        internal static bool MarkNear(QuestDef q, StageDef s, Vector3 from, out Vector3 at)
        {
            at = Vector3.zero;
            List<StageDef> more = MoreMarks(s);
            if (more == null || more.Count == 0) return StoryMap.Where("story." + q.Id, out at);
            bool any = false;
            float best = float.MaxValue;
            for (int i = s.Ways.Count > 0 ? 0 : -1; i < more.Count; i++) // (-1: the stage's own mark, beside its second ones; ways have none of their own)
            {
                Vector3 m;
                if (!StoryMap.Where("story." + q.Id + (i < 0 ? "" : "." + i), out m)) continue;
                float d = Utils.DistanceXZ(m, from); // flat: marks on the map
                if (d < best) { best = d; at = m; any = true; }
            }
            return any;
        }

        // the stage's other marks (its ways', else its second «mark:» lines and on): drawn as «story.<quest>.<n>»
        private static List<StageDef> MoreMarks(StageDef s) { return s == null ? null : s.Ways.Count > 0 ? s.Ways : s.Marks; }

        // where a place to choose from leads: the fork that names it (one word of its conditions), else the stage's own next (its «wait:»)
        internal static string Whither(StageDef s, string place)
        {
            foreach (Fork f in s.Forks) if (Names(f.Text.Substring(0, Math.Max(0, f.Text.IndexOf("->", StringComparison.Ordinal))), place)) return f.To;
            return s.Next;
        }
        internal static bool Names(string conds, string place) { return Array.IndexOf(conds.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries), place) >= 0; }

        // the goal lines on the screen (StoryScreen.Goal): the first active quest's stage — its ways side by side — and, under it and
        // paler, the one beside it (a third and more: only the book shows them, the screen keeps two)
        private static void ShowGoal(Player p)
        {
            List<Standing> a = NowAll(p);
            StoryScreen.Goal(a.Count > 0 ? Lang.T(a[0].Quest.Name) : "", a.Count > 0 ? GoalText(a[0].Stage) : "",
                a.Count > 1 ? Lang.T(a[1].Quest.Name) : "", a.Count > 1 ? GoalText(a[1].Stage) : "");
        }

        // the stage's mark on the map (StoryMap: set again each pass costs nothing when unchanged; gone with the stage)
        private static readonly Dictionary<string, int> _wayMarks = new Dictionary<string, int>(); // (quest → how many way marks it set last pass)

        private static void Mark(Player p, QuestDef q)
        {
            string key = "story." + q.Id;
            StageDef s = StageNow(p, q);
            MarkOne(key, s);
            List<StageDef> more = MoreMarks(s);
            int had, n = more != null ? more.Count : 0;
            _wayMarks.TryGetValue(q.Id, out had);
            for (int i = 0; i < Mathf.Max(had, n); i++) MarkOne(key + "." + i, i < n ? more[i] : null, s);
            if (had != n) _wayMarks[q.Id] = n;
        }

        internal static float SceneMarkRadius = 10f;
        // a search's ring on the map (the stage's «seek:»): its middle up to SeekOff of its radius away from the spot, the way from the spot's own
        // whole metres (every game draws the same ring; none knows the spot from it)
        internal static float SeekOff = 0.6f, SeekOffLeast = 0.35f; // (the spot at least this share of the radius off the middle: the user 2026-11-15 — the barrow came out in the middle of its ring, «дивно»)
        private static Vector3 SeekRing(Vector3 at, float radius)
        {
            System.Random r = new System.Random(Mathf.RoundToInt(at.x) * 73856093 ^ Mathf.RoundToInt(at.z) * 19349663);
            double a = r.NextDouble() * Math.PI * 2d, d = (SeekOffLeast + r.NextDouble() * (SeekOff - SeekOffLeast)) * radius;
            return new Vector3(at.x + (float)(Math.Cos(a) * d), at.y, at.z + (float)(Math.Sin(a) * d));
        }
        private static void MarkOne(string key, StageDef s, StageDef of = null) // (of: the stage whose way or place to choose from this is — its goal is the label where this has none)
        {
            Vector3 at;
            string where = s != null ? s.MarkAt : "";
            float radius = s != null ? s.MarkRadius : 0f;
            if (s != null && where.Length == 0 && s.Scene.Length > 0) // (no mark of its own: where its scene plays, so nobody is left with no way back to it while it waits)
            {
                StoryScenes.SceneDef sc = StoryScenes.Find(s.Scene);
                if (sc != null && sc.Place.Length > 0 && sc.Place != StoryShots.PlayerFrame ) { where = sc.Place; radius = SceneMarkRadius; }
            }
            if (where.Length == 0 || !StoryWhere.MapAt(where, out at)) { StoryMap.Clear(key); return; } // (a point inside a dungeon of ours: drawn at its way in)
            bool bare = false;
            if (s.Seek.Length > 0 && StorySeek.Words(s.Seek).Length > 0 && StorySeek.Words(s.Seek)[0] == where)
            {
                radius *= StorySeek.RingOf(where); at = SeekRing(at, radius);
                Player me = Player.m_localPlayer; // (in the search's ring: its ring alone, the «!» back once he leaves it)
                bare = me != null && Utils.DistanceXZ(me.transform.position, at) <= radius;
            } // (a search: the ring off the spot, the same on every game; with no raven, narrowed as he comes — StorySeek)
            StoryMap.Set(key, at, Lang.T(s.MarkLabel.Length > 0 ? s.MarkLabel : s.Goal.Length > 0 || of == null ? s.Goal : of.Goal), radius, bare, s.Fight || (of != null && of.Fight));
        }

        // only what some stage waits for (an «on:», a scene's end) is kept for the next pass: a fight's hundred kills cost nothing
        // and nothing a stage needs is ever dropped (the bound: a runaway loop of raises)
        private static readonly HashSet<string> _wanted = new HashSet<string>();
        private static void Hear(StoryEvent e) { if (_wanted.Contains(e.Name) && _heard.Count < 1024) _heard.Add(e); }

        // quests whose «starts:» now hold start by themselves
        private static void StartWaiting(Player p)
        {
            foreach (QuestDef q in Quests)
                if (State(p, q).Length == 0 && q.Starts != null && q.Starts.Count > 0 && Leads(p, q) && StoryCond.All(p, q.Starts) && Time.time >= q.AskUntil) { if (q.Server) StartOnServer(q); else Start(p, q); }
        }

        internal static void Start(Player p, QuestDef q)
        {
            if (q.Server) { Ask(q, "", q.First, false, true); return; } // (the author's: from its first stage, whatever stands)
            Story.SetQuest(p, q.Id, Story.Active);
            Note(q.Id + ": почався");
            Enter(p, q, q.First);
        }

        // to a stage: its start actions, then its forks at once
        private static int _depth; // (stages entered one from another in this pass: a fork or goto going round in a ring stops)
        private static string _ringTold; // (said once in the log, not twice a second)

        internal static void Enter(Player p, QuestDef q, string id)
        {
            if (_depth >= 16) { if (_ringTold != q.Id) { _ringTold = q.Id; Note(q.Id + ": розвилки й goto ходять по колу (на «" + id + "») — квест стоїть, перевір файл"); } return; }
            _depth++;
            try { Enter0(p, q, id); } finally { _depth--; }
        }

        private static void Enter0(Player p, QuestDef q, string id)
        {
            if (id == "end") { Finish(p, q, Story.Done); return; }
            if (id.Length == 0) return; // (the action «end» in a row has finished the quest: nowhere to go)
            StageDef s = q.Stage(id);
            if (s == null) { Note(q.Id + ": нема етапу «" + id + "» — квест стоїть"); return; }
            Story.SetStage(p, q.Id, s.Id);
            if (q.NudgeScene.Length > 0) { Story.SetVar(p, NudgeVar(q), Mathf.Max(1, StoryTrackConds.Played(p)), true); Story.SetVar(p, NudgeVar(q) + "_n", 0, true); } // (a new stage: its clock and its count anew)
            Note(q.Id + " → " + s.Id);
            StoryBus.Raise("stage", q.Id + "." + s.Id, 0);
            string jump = Run(p, q, s.Start);
            if (jump != null) { Enter(p, q, jump); return; }
            if (Forked(p, q, s)) return;
            if (s.Scene.Length > 0) StoryScenes.TryPlay(s.Scene);
        }

        private static bool Forked(Player p, QuestDef q, StageDef s)
        {
            foreach (Fork f in s.Forks)
                if (StoryCond.All(p, f.When)) { Note(q.Id + "." + s.Id + ": розвилка «" + f.Text + "»"); Enter(p, q, f.To); return true; }
            return false;
        }

        private static void Step(Player p, QuestDef q)
        {
            StageDef s = q.Stage(Story.StageOf(p, q.Id));
            if (s == null) { Enter(p, q, q.First); return; } // (a stage gone from the file: from the top, said in the log)
            if ((s.Scene.Length == 0 || StoryScenes.Playing != s.Scene) && Forked(p, q, s)) return; // (its own scene plays: the forks wait for its end, not walk away under it)
            Nudge(p, q, s);
            if (s.Ways.Count > 0)
            {
                foreach (StageDef w in s.Ways) if (Passed(p, w)) { Choose(p, q, s, w); return; }
                return;
            }
            if (s.Scene.Length > 0 && StoryScenes.Playing != s.Scene && !HeardEvent("scene_done", s.Scene)) StoryScenes.TryPlay(s.Scene); // (not begun yet, stopped, or back after a relog: when it may)
            if (!Passed(p, s)) return;
            Lived(p, q, s, null);
            string jump = Run(p, q, s.Done);
            Enter(p, q, jump ?? s.Next);
        }

        // a stage with a place to go to, standing long: the quest's «nudge» scene shows the way. A stage that only waits for time has no
        // mark and no nudge. The clock is the character's (a relog goes on), minutes of real play; it starts anew only when the scene began
        // (needs not met — asked again next pass, TryPlay says so in the log once): twice a second one read of a var, at worst
        private static string NudgeVar(QuestDef q) { return "nudge_" + q.Id; }

        private static void Nudge(Player p, QuestDef q, StageDef s)
        {
            if (q.NudgeScene.Length == 0) return;
            int now = StoryTrackConds.Played(p), from = Story.Var(p, NudgeVar(q)), shown = Story.Var(p, NudgeVar(q) + "_n");
            if (from <= 0) { Story.SetVar(p, NudgeVar(q), Mathf.Max(1, now), true); return; } // (stood on this stage since before this word)
            if (shown >= NudgeMost || now - from < (q.NudgeMinutes * 60) << Mathf.Min(shown, 4) || StoryScenes.Playing.Length > 0) return; // (each time twice as long; enough times: quiet)
            Vector3 mark;
            if (!MarkAt(q, s, out mark)) return;
            StoryScenes.TryPlay(q.NudgeScene);
            if (StoryScenes.Playing != q.NudgeScene) return;
            Story.SetVar(p, NudgeVar(q), Mathf.Max(1, now), true);
            Story.SetVar(p, NudgeVar(q) + "_n", shown + 1, true);
            Note(q.Id + "." + s.Id + ": стоїть " + (now - from) / 60 + " хв — ворон показує дорогу (" + (shown + 1) + " з " + NudgeMost + ")");
        }
        internal static int NudgeMost = 3;

        // the place «goal.mark»: the mark of the stage the player stands at now; of its ways, the nearest
        private const string GoalPrefix = "goal.";
        internal static void Places() { StoryWhere.Prefixed[GoalPrefix] = GoalPlace; }
        internal static int ReadCount; // (how many times the files were read: what is made from them once — the «Тест» tab)
        private static bool GoalPlace(string rest, out Vector3 at)
        {
            at = Vector3.zero;
            Player p = Player.m_localPlayer;
            QuestDef q; StageDef s;
            if (p == null || !Now(p, out q, out s)) return false;
            return MarkNear(q, s, p.transform.position, out at);
        }

        // a way done: the choice kept (one way per choice — an older one of the same name goes), its done, the stage's done, on
        internal static void Choose(Player p, QuestDef q, StageDef s, StageDef w)
        {
            string c = ChoiceOf(q, s);
            Choose(p, c, w.Id);
            Note(q.Id + "." + s.Id + ": вибрано шлях «" + w.Id + "»");
            StoryBus.Raise("way", c + "." + w.Id, 0);
            Lived(p, q, s, null);
            Lived(p, q, s, w);
            string jump = Run(p, q, w.Done); // (the stage's own done runs after it, whichever way, as the head says; only an «end» stops it: the quest is over)
            string own = jump == null || jump.Length > 0 ? Run(p, q, s.Done) : null;
            Enter(p, q, jump ?? own ?? w.Next);
        }

        internal static void Choose(Player p, string choice, string way)
        {
            string pre = ChoseFlag + choice + ".";
            foreach (string f in Story.Of(p).Flags.ToArray()) if (f.StartsWith(pre, StringComparison.Ordinal) && f != pre + way) Story.SetFlag(p, f, false);
            Story.SetFlag(p, pre + way, true);
        }

        // the way chosen under that name ("" none yet)
        internal static string Chosen(Player p, string choice)
        {
            string pre = ChoseFlag + choice + ".";
            foreach (string f in Story.Of(p).Flags) if (f.StartsWith(pre, StringComparison.Ordinal)) return f.Substring(pre.Length);
            return "";
        }

        // a stage (or its way) passed: its saga entry into the character (StorySaga reads them; the book may come later)
        private static void Lived(Player p, QuestDef q, StageDef s, StageDef w)
        {
            if ((w ?? s).Saga.Length > 0) Story.AddSaga(p, q.Id + "." + s.Id + (w != null ? "." + w.Id : ""));
        }

        // the stage (or way) a saga entry «quest.stage[.way]» names (null: gone from the files)
        internal static StageDef Entry(string entry, out QuestDef q)
        {
            q = null;
            string[] w = entry.Split('.');
            if (w.Length < 2 || w.Length > 3) return null;
            q = Find(w[0]);
            StageDef s = q != null ? q.Stage(w[1]) : null;
            if (s == null || w.Length == 2) return s;
            foreach (StageDef way in s.Ways) if (way.Id == w[2]) return way;
            return null;
        }

        private static bool Passed(Player p, StageDef s)
        {
            if (s.Scene.Length > 0) return HeardEvent("scene_done", s.Scene);
            if (s.OnName.Length > 0) return HeardEvent(s.OnName, s.OnArgs) && StoryCond.All(p, s.Wait);
            return StoryCond.All(p, s.Wait);
        }

        private static bool HeardEvent(string name, string args)
        {
            foreach (StoryEvent e in _now) if (StoryBus.Matches(e, name, args)) return true;
            return false;
        }

        // the actions in a row; a «goto» stops the row and says where
        private static string Run(Player p, QuestDef q, List<string> acts)
        {
            foreach (string a in acts)
            {
                string jump = null;
                try { jump = StoryActions.Do(p, q, a); }
                catch (Exception e) { Failed(q.Id + " action «" + a + "»", e); } // (the row goes on: a stage half entered, its stage word already written, would never run the rest)
                if (jump != null) return jump;
            }
            return null;
        }

        internal static void Finish(Player p, QuestDef q, string state)
        {
            if (q.Server) { Ask(q, At(p, q), "end", false, true); return; } // (the author's panel: storycheck keeps the action «end» off a server's quest)
            StageDef last = q.Stage(Story.StageOf(p, q.Id));
            Story.SetQuest(p, q.Id, state);
            Story.SetStage(p, q.Id, "");
            Story.SetStage(p, EndedKey(q), state == Story.Done && last != null && last.Next == "end" ? last.Id : ""); // (the stage it ended at by its own «next: end»: Resume)
            Note(q.Id + ": " + (state == Story.Done ? "пройдено" : "провалено"));
            StoryBus.Raise("quest_" + state, q.Id, 0);
        }

        // A story told on in a later version of the mod (the user 2026-11-15: «ті, хто до того моменту пройде його частину, не застрягнуть — квест піде
        // далі просто після оновлення»): a quest done by its stage's own «next: end» keeps that stage (the character's «endat_<id>», the world's «-done
        // <stage>»); once that stage leads on in the files read now, the quest is active again on the new stage, its «start:» run as on any step.
        // Ended any other way (a fork, the action «end», failed) — nothing kept: it stays as it ended. Asked a pass where the quest is done
        private static string EndedKey(QuestDef q) { return "endat_" + q.Id; }
        private static void Resume(Player p, QuestDef q)
        {
            string at = Story.StageOf(p, EndedKey(q));
            if (at.Length == 0) return;
            StageDef s = q.Stage(at);
            if (s == null || s.Next.Length == 0 || s.Next == "end" || q.Stage(s.Next) == null) return; // (still the end, or that stage gone from the files)
            Story.SetStage(p, EndedKey(q), "");
            Story.SetQuest(p, q.Id, Story.Active);
            Note(q.Id + ": далі в новій версії — після «" + s.Id + "» тепер «" + s.Next + "»");
            Enter(p, q, s.Next);
        }


        // ---------------------------------------------------------------- the author's (the panel, the console)
        // the author's jump, start over («» first) and forget («»): the world and the character put as if the story had been walked there — StoryRebuild.cs
        internal static void Jump(Player p, QuestDef q, string stage) { Rebuild(p, q, stage); }

        // a personal quest's stage word alone, nothing else touched (the studio's scene check: its world is put by the scene's own «check:» / «test:» lines
        // and given back after the run — StudioScene.OnItsStage)
        internal static void SetOn(Player p, QuestDef q, string stage)
        {
            if (q.Server) return;
            if (Story.Quest(p, q.Id) != Story.Active) Story.SetQuest(p, q.Id, Story.Active);
            Note(q.Id + ": студія → " + stage);
            Enter(p, q, stage);
        }

        internal static void Note(string s)
        {
            Recent.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (Recent.Count > 60) Recent.RemoveAt(0);
            Story.Log("track: " + s);
            Version++;
        }

        // what the stage waits for, in words (the panel)
        internal static string Waits(Player p, StageDef s)
        {
            if (s.Scene.Length > 0) return "сцена «" + s.Scene + "»" + (StoryScenes.Playing == s.Scene ? " — іде" : "");
            StringBuilder sb = new StringBuilder();
            if (s.Ways.Count > 0)
            {
                sb.Append("один зі шляхів:");
                foreach (StageDef w in s.Ways) sb.Append("\n      ").Append(w.Id).Append(" — ").Append(w.Goal).Append(": ").Append(Waits(p, w)).Append(" → ").Append(w.Next);
                return sb.ToString();
            }
            if (s.OnName.Length > 0) sb.Append("подія «").Append(s.OnName).Append(s.OnArgs.Length > 0 ? " " + s.OnArgs : "").Append("»");
            if (s.Wait != null && s.Wait.Count > 0)
            {
                if (sb.Length > 0) sb.Append(" і ");
                for (int i = 0; i < s.Wait.Count; i++) sb.Append(i > 0 ? ", " : "").Append(s.Wait[i].Test(p) ? "✓ " : "✗ ").Append(s.Wait[i].Text);
            }
            return sb.Length > 0 ? sb.ToString() : "нічого — одразу далі";
        }
    }
}
#endif

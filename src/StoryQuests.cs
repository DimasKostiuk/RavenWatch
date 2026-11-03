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
    //   starts: hall                     conditions (StoryCond) — the quest starts by itself once they hold
    //   first: settle
    //
    //   [stage settle]
    //   goal: Освойся                    the line shown (the panel now, the tracker later)
    //   fork: hallgate open -> inside    checked on entering and while waiting: holds → straight to that stage
    //   mark: hall 30 Зала Варти         the golden «!» on the map while this stage lasts (place, ring metres, label)
    //   start: msg …                     actions on entering, split by «;»
    //   wait: played 10, calm            passes once all hold …
    //   on: use eirik_carving            … or once this event comes (with wait: both)
    //   scene: raven_call                … or plays the scene and passes when it ends
    //   done: hallgate open; flag x      actions on passing
    //   next: call                       the next stage, or «end» (the quest done); none = the next one in the file
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
    //   goal: Лети за вороном            a way knows goal, mark, wait, on, done, next (none = the stage's next)
    //   mark: hall 30 Зала Варти
    //   wait: near hall 25
    //   next: to_hall
    internal static class StoryQuests
    {
        internal sealed class Fork { public List<Cond> When; public string To = "", Text = ""; }

        internal sealed class StageDef
        {
            public string Id = "", Goal = "", Scene = "", Next = "", OnName = "", OnArgs = "", WaitText = "", MarkAt = "", MarkLabel = "", Choice = "";
            public float MarkRadius;
            public List<Cond> Wait;
            public readonly List<Fork> Forks = new List<Fork>();
            public readonly List<string> Start = new List<string>(), Done = new List<string>();
            public readonly List<StageDef> Ways = new List<StageDef>(); // (a way is a small stage of its own: goal, mark, wait, on, done, next)
        }

        internal sealed class QuestDef
        {
            public string Id = "", Name = "", First = "", StartsText = "", From = "";
            public List<Cond> Starts;
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

        // ---------------------------------------------------------------- reading
        internal static void Load() { if (!_loaded) Reload(); }

        internal static void Reload()
        {
            _loaded = true;
            StoryTrackConds.Register();
            Quests.Clear(); Errors.Clear();
            foreach (KeyValuePair<string, string> kv in StoryFiles.All(StoryFiles.Quests, Folder, Blueprint.On)) Parse(kv.Key, kv.Value); // (built in; the mod's own folder on the author's machine — StoryFiles)
            foreach (QuestDef q in Quests) Validate(q);
            StoryScenes.Load(); // (the scenes the stages name: checked against them)
            foreach (QuestDef q in Quests) ValidateRefs(q);
            foreach (string e in Errors) Story.Log("quests: " + e);
            Story.Log("quests: " + Quests.Count + " loaded" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
            StoryBus.Listen("story", Hear);
            Version++;
        }

        private static void Parse(string file, string text)
        {
            QuestDef q = null; StageDef s = null, w = null; // (w: the way being read, inside s)
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value, at = file + ":" + ln.Key + ": ";
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string[] h = line.Substring(1, line.Length - 2).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (h.Length == 2 && h[0] == "quest" && Story.GoodName(h[1]))
                    {
                        if (Find0(h[1]) != null) { Errors.Add(at + "квест «" + h[1] + "» уже є — цей пропущено"); q = null; s = null; continue; }
                        q = new QuestDef { Id = h[1], From = file }; Quests.Add(q); s = null; w = null;
                    }
                    else if (h.Length == 2 && h[0] == "stage" && q != null && Story.GoodName(h[1]))
                    {
                        if (q.Stage(h[1]) != null) Errors.Add(at + "етап «" + h[1] + "» двічі");
                        s = new StageDef { Id = h[1] }; q.Stages.Add(s); w = null;
                    }
                    else if (h.Length == 2 && h[0] == "way" && s != null && Story.GoodName(h[1]))
                    {
                        foreach (StageDef o in s.Ways) if (o.Id == h[1]) Errors.Add(at + "шлях «" + h[1] + "» двічі");
                        w = new StageDef { Id = h[1] }; s.Ways.Add(w);
                    }
                    else Errors.Add(at + "не розумію «" + line + "»");
                    continue;
                }
                int c = line.IndexOf(':');
                if (c <= 0 || q == null) { Errors.Add(at + "рядок поза квестом або без «:»"); continue; }
                string key = line.Substring(0, c).Trim().ToLowerInvariant(), val = line.Substring(c + 1).Trim();
                if (s == null)
                {
                    switch (key)
                    {
                        case "name": q.Name = val; break;
                        case "starts": q.StartsText = val; q.Starts = Conds(val, at); break;
                        case "first": q.First = val; break;
                        default: Errors.Add(at + "невідоме поле квесту «" + key + "»"); break;
                    }
                    continue;
                }
                if (w != null && key != "goal" && key != "mark" && key != "wait" && key != "on" && key != "done" && key != "next")
                {
                    Errors.Add(at + "у шляху лише goal, mark, wait, on, done, next — «" + key + "» тут ні до чого"); continue;
                }
                StageDef d = w ?? s; // (what the line belongs to: the way, else the stage)
                switch (key)
                {
                    case "goal": d.Goal = val; break;
                    case "choice": if (Story.GoodName(val) && val.IndexOf('.') < 0) s.Choice = val; else Errors.Add(at + "choice: назва без крапок і пробілів"); break;
                    case "wait": d.WaitText = val; d.Wait = Conds(val, at); break;
                    case "on":
                        {
                            int sp = val.IndexOf(' ');
                            d.OnName = (sp < 0 ? val : val.Substring(0, sp)).ToLowerInvariant();
                            d.OnArgs = sp < 0 ? "" : val.Substring(sp + 1).Trim();
                            break;
                        }
                    case "scene": d.Scene = val; break;
                    case "mark":
                        {
                            string[] m = val.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                            if (m.Length == 0) { Errors.Add(at + "mark: <місце> [радіус] <напис>"); break; }
                            d.MarkAt = m[0];
                            int r = m.Length > 1 ? Errands.I(m[1], -1) : -1;
                            d.MarkRadius = r > 0 ? r : 0f;
                            d.MarkLabel = r > 0 ? (m.Length > 2 ? m[2] : "") : (m.Length > 1 ? val.Substring(val.IndexOf(m[1], m[0].Length)) : "");
                            break;
                        }
                    case "next": d.Next = val; break;
                    case "start": Actions(val, d.Start, at); break;
                    case "done": Actions(val, d.Done, at); break;
                    case "fork":
                        {
                            int arrow = val.LastIndexOf("->");
                            if (arrow < 0) { Errors.Add(at + "fork: <умови> -> <етап>"); break; }
                            d.Forks.Add(new Fork { Text = val, When = Conds(val.Substring(0, arrow), at), To = val.Substring(arrow + 2).Trim() });
                            break;
                        }
                    default: Errors.Add(at + "невідоме поле етапу «" + key + "»"); break;
                }
            }
        }

        private static QuestDef Find0(string id) { foreach (QuestDef q in Quests) if (q.Id == id) return q; return null; }

        // conditions, their problems with the line's place in front
        private static List<Cond> Conds(string val, string at)
        {
            List<string> errs = new List<string>();
            List<Cond> l = StoryCond.Parse(val, errs);
            foreach (string e in errs) Errors.Add(at + e);
            return l;
        }

        private static void Actions(string val, List<string> into, string at)
        {
            foreach (string a in val.Split(';'))
            {
                string t = a.Trim();
                if (t.Length == 0) continue;
                string why = StoryActions.Check(t);
                if (why != null) Errors.Add(at + why);
                into.Add(t);
            }
        }

        private static void Validate(QuestDef q)
        {
            string at = q.From + " [" + q.Id + "]: ";
            if (q.Stages.Count == 0) { Errors.Add(at + "жодного етапу"); return; }
            if (q.First.Length == 0) q.First = q.Stages[0].Id;
            if (q.Stage(q.First) == null) Errors.Add(at + "first: нема етапу «" + q.First + "»");
            for (int i = 0; i < q.Stages.Count; i++)
            {
                StageDef s = q.Stages[i];
                if (s.Next.Length == 0) s.Next = i + 1 < q.Stages.Count ? q.Stages[i + 1].Id : "end";
                if (s.Next != "end" && q.Stage(s.Next) == null) Errors.Add(at + s.Id + ": next — нема етапу «" + s.Next + "»");
                foreach (Fork f in s.Forks) if (f.To != "end" && q.Stage(f.To) == null) Errors.Add(at + s.Id + ": fork — нема етапу «" + f.To + "»");
                foreach (string a in s.Start) StoryActions.CheckGoto(q, a, at + s.Id + ": ", Errors);
                foreach (string a in s.Done) StoryActions.CheckGoto(q, a, at + s.Id + ": ", Errors);
                if (s.Ways.Count > 0) { ValidateWays(q, s, at); continue; }
                if (s.Goal.Length == 0) Errors.Add(at + s.Id + ": без goal (гравець не знатиме, що робити)");
            }
            int unnamed = 0;
            foreach (StageDef s in q.Stages) if (s.Ways.Count > 0 && s.Choice.Length == 0) unnamed++;
            if (unnamed > 1) Errors.Add(at + "кілька етапів зі шляхами без choice: — назви кожен вибір");
        }

        // what a stage names elsewhere: its scene, its marks' places (a typo there — a scene «played» at once, a mark nowhere)
        private static void ValidateRefs(QuestDef q)
        {
            string at = q.From + " [" + q.Id + "]: ";
            foreach (StageDef s in q.Stages)
            {
                if (s.Scene.Length > 0 && StoryScenes.Find(s.Scene) == null) Errors.Add(at + s.Id + ": нема сцени «" + s.Scene + "» (зіграється одразу, нічого не показавши)");
                foreach (StageDef d in s.Ways.Count > 0 ? s.Ways : new List<StageDef> { s })
                {
                    string why = d.MarkAt.Length > 0 ? StoryWhere.Why(d.MarkAt) : null;
                    if (why != null) Errors.Add(at + d.Id + ": mark — " + why);
                }
            }
        }

        private static void ValidateWays(QuestDef q, StageDef s, string at)
        {
            at += s.Id + ": ";
            if (s.Scene.Length > 0 || s.OnName.Length > 0 || (s.Wait != null && s.Wait.Count > 0)) Errors.Add(at + "етап зі шляхами чекає лише на шляхи — wait/on/scene перенеси в шлях");
            if (s.Ways.Count > MaxWays) Errors.Add(at + "шляхів більше " + MaxWays);
            foreach (StageDef w in s.Ways)
            {
                if (w.Next.Length == 0) w.Next = s.Next;
                if (w.Next != "end" && q.Stage(w.Next) == null) Errors.Add(at + "шлях " + w.Id + ": next — нема етапу «" + w.Next + "»");
                if (w.Goal.Length == 0) Errors.Add(at + "шлях " + w.Id + ": без goal");
                if (w.OnName.Length == 0 && (w.Wait == null || w.Wait.Count == 0)) Errors.Add(at + "шлях " + w.Id + ": без wait/on — його виберуть одразу");
                foreach (string a in w.Done) StoryActions.CheckGoto(q, a, at + "шлях " + w.Id + ": ", Errors);
            }
        }

        internal const int MaxWays = 6;
        internal const string ChoseFlag = "chose.";

        // the name a stage's choice is kept under (its own, else the quest's)
        internal static string ChoiceOf(QuestDef q, StageDef s) { return s.Choice.Length > 0 ? s.Choice : q.Id; }

        // ---------------------------------------------------------------- running
        // every frame (RavenSkills): the played minutes, the events heard, the stages twice a second
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || ZNet.instance == null) return;
            Load();
            StoryTrackConds.Count(p);
            StoryOwn.Tick(p);
            StoryScenes.Tick();
            StoryHush.Tick(p);
            StoryProps.Look();
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            _now.Clear(); _now.AddRange(_heard); _heard.Clear(); // (what is raised during this pass waits for the next one)
            if (Time.time >= _startNext) { _startNext = Time.time + 2f; StartWaiting(p); }
            foreach (QuestDef q in Quests)
            {
                if (Story.Quest(p, q.Id) == Story.Active) Step(p, q);
                Mark(p, q);
            }
            ShowGoal(p);
            Hush(p);
        }

        // the quiet rounds this player's story wants (StoryHush): the scene playing (round its place, else round him);
        // a stage that plays a scene with a place; a stage whose mark leads to a scene (its next stage's, or its ways')
        private static void Hush(Player p)
        {
            StoryHush.Pass();
            StoryScenes.SceneDef now = StoryScenes.Current;
            if (now != null) HushAt("scene", now, now.Place.Length > 0 ? now.Place : null, p, true);
            foreach (QuestDef q in Quests)
            {
                if (Story.Quest(p, q.Id) != Story.Active) continue;
                StageDef s = q.Stage(Story.StageOf(p, q.Id));
                if (s == null) continue;
                if (s.Scene.Length > 0) { StoryScenes.SceneDef sc = StoryScenes.Find(s.Scene); if (sc != null && sc.Place.Length > 0 && sc != now) HushAt(q.Id + "." + s.Id, sc, sc.Place, p, false); }
                if (s.Ways.Count == 0) { if (s.MarkAt.Length > 0) HushAt(q.Id + "." + s.Id + ".m", Leads(q, s.Next), s.MarkAt, p, false); }
                else foreach (StageDef w in s.Ways) if (w.MarkAt.Length > 0) HushAt(q.Id + "." + s.Id + "." + w.Id, Leads(q, w.Next), w.MarkAt, p, false);
            }
        }

        // the scene the stages after this one come to (a few steps on: way → touch the shield → the scene); null — none:
        // a mark that leads to no scene wants no quiet
        private static StoryScenes.SceneDef Leads(QuestDef q, string next)
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

        private static void HushAt(string key, StoryScenes.SceneDef sc, string place, Player p, bool full)
        {
            if (sc == null) return;
            Vector3 at;
            if (place == null) at = p.transform.position; // (a scene with no place: round the player)
            else if (!StoryWhere.Find(place, out at)) return;
            StoryHush.Want(key, at, StoryHush.MaxFor(sc, place), full);
        }

        // the goal line on the screen (StoryScreen.Goal): the first active quest's stage — its ways side by side
        private static void ShowGoal(Player p)
        {
            foreach (QuestDef q in Quests)
            {
                if (Story.Quest(p, q.Id) != Story.Active) continue;
                StageDef s = q.Stage(Story.StageOf(p, q.Id));
                if (s == null) continue;
                string goal;
                if (s.Ways.Count > 0)
                {
                    List<string> l = new List<string>();
                    foreach (StageDef w in s.Ways) if (w.Goal.Length > 0) l.Add("· " + Lang.T(w.Goal));
                    goal = (s.Goal.Length > 0 ? Lang.T(s.Goal) + "\n" : "") + string.Join("\n", l.ToArray());
                }
                else goal = Lang.T(s.Goal);
                StoryScreen.Goal(Lang.T(q.Name), goal);
                return;
            }
            StoryScreen.Goal("", "");
        }

        // the stage's mark on the map (StoryMap: set again each pass costs nothing when unchanged; gone with the stage)
        private static readonly Dictionary<string, int> _wayMarks = new Dictionary<string, int>(); // (quest → how many way marks it set last pass)

        private static void Mark(Player p, QuestDef q)
        {
            string key = "story." + q.Id;
            StageDef s = Story.Quest(p, q.Id) == Story.Active ? q.Stage(Story.StageOf(p, q.Id)) : null;
            MarkOne(key, s);
            int had, n = s != null ? s.Ways.Count : 0;
            _wayMarks.TryGetValue(q.Id, out had);
            for (int i = 0; i < Mathf.Max(had, n); i++) MarkOne(key + "." + i, i < n ? s.Ways[i] : null);
            if (had != n) _wayMarks[q.Id] = n;
        }

        private static void MarkOne(string key, StageDef s)
        {
            Vector3 at;
            if (s == null || s.MarkAt.Length == 0 || s.MarkAt == "hall" || !StoryWhere.Find(s.MarkAt, out at)) { StoryMap.Clear(key); return; } // (the Hall wears its own mark always: Loc.MapMark)
            StoryMap.Set(key, at, Lang.T(s.MarkLabel.Length > 0 ? s.MarkLabel : s.Goal), s.MarkRadius);
        }

        // (a busy half second — a fight's kills — may fill the list; what moves a stage is never dropped)
        private static void Hear(StoryEvent e) { if (_heard.Count < 256 || e.Name == "scene_done" || e.Name == "heart_built" || e.Name == "use") _heard.Add(e); }

        // quests whose «starts:» now hold start by themselves
        private static void StartWaiting(Player p)
        {
            foreach (QuestDef q in Quests)
                if (Story.Quest(p, q.Id).Length == 0 && q.Starts != null && q.Starts.Count > 0 && StoryCond.All(p, q.Starts)) Start(p, q);
        }

        internal static void Start(Player p, QuestDef q)
        {
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
            StageDef s = q.Stage(id);
            if (s == null) { Note(q.Id + ": нема етапу «" + id + "» — квест стоїть"); return; }
            Story.SetStage(p, q.Id, s.Id);
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
            if (Forked(p, q, s)) return;
            if (s.Ways.Count > 0)
            {
                foreach (StageDef w in s.Ways) if (Passed(p, w)) { Choose(p, q, s, w); return; }
                return;
            }
            if (s.Scene.Length > 0 && StoryScenes.Playing != s.Scene && !HeardEvent("scene_done", s.Scene)) StoryScenes.TryPlay(s.Scene); // (not begun yet, stopped, or back after a relog: when it may)
            if (!Passed(p, s)) return;
            string jump = Run(p, q, s.Done);
            Enter(p, q, jump ?? s.Next);
        }

        // a way done: the choice kept (one way per choice — an older one of the same name goes), its done, the stage's done, on
        internal static void Choose(Player p, QuestDef q, StageDef s, StageDef w)
        {
            string c = ChoiceOf(q, s);
            Choose(p, c, w.Id);
            Note(q.Id + "." + s.Id + ": вибрано шлях «" + w.Id + "»");
            StoryBus.Raise("way", c + "." + w.Id, 0);
            string jump = Run(p, q, w.Done) ?? Run(p, q, s.Done);
            Enter(p, q, jump ?? w.Next);
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
                string jump = StoryActions.Do(p, q, a);
                if (jump != null) return jump;
            }
            return null;
        }

        internal static void Finish(Player p, QuestDef q, string state)
        {
            Story.SetQuest(p, q.Id, state);
            Story.SetStage(p, q.Id, "");
            Note(q.Id + ": " + (state == Story.Done ? "пройдено" : "провалено"));
            StoryBus.Raise("quest_" + state, q.Id, 0);
        }


        // ---------------------------------------------------------------- the author's (the panel, the console)
        internal static void Jump(Player p, QuestDef q, string stage)
        {
            if (Story.Quest(p, q.Id) != Story.Active) Story.SetQuest(p, q.Id, Story.Active);
            Note(q.Id + ": автор → " + stage);
            Enter(p, q, stage);
        }

        internal static void Forget(Player p, QuestDef q)
        {
            Story.SetQuest(p, q.Id, "");
            Story.SetStage(p, q.Id, "");
            Note(q.Id + ": стерто для персонажа");
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

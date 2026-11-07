#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story window's tab «Тест» (the user 2026-11-04: what a test needs, under the hand, no console): every button is one
    // line of the console, run as it would be typed, and what it answers is shown beside it. No logic of its own: a new
    // test command is a new row here. A line starting «con » goes to the game's own console. A row marked sure asks for a
    // second click. What names the story (its quests and their stages, its places, scenes, the player's own spots) comes from what is
    // loaded; the story's own switches (a flag of the ghost's house, Heid called) from the author's file <source>/_test.txt
    // («<група> | <напис> | <рядок консолі> [| sure]»; never in the mod) — the rows made once, again when the story is read again.
    internal partial class StoryPanel
    {
        private const int TestTab = 8;
        private const float TestButton = 138f;
        private const int TestInRow = 5;
        private Vector2 _testScroll, _testOut;
        private string _testSaid = "Натисни кнопку: тут буде відповідь.", _testSure = "";

        private sealed class TestRow { public string Word, Line; public bool Sure; public TestRow(string word, string line, bool sure = false) { Word = word; Line = line; Sure = sure; } }

        private static readonly TestRow[] TestStart =
        {
            new TestRow("Стерти персонажа", "wipe yes", true),
            new TestRow("Стерти сюжет світу", "story world clear", true),
            new TestRow("Лише сюжет персонажа", "story reset", true),
        };
        private static readonly TestRow[] TestRaven =
        {
            new TestRow("Дати Муніна", "story own munin"),
            new TestRow("Дати Хугіна", "story own hugin"),
            new TestRow("Забрати воронів", "story unown both", true),
            new TestRow("Дати книгу", "story do book"),
            new TestRow("+10 хв гри", "story played 10"),
        };
        private static readonly TestRow[] TestState =
        {
            new TestRow("Де я в сюжеті", "story quests"),
            new TestRow("Чи спокійно", "story check calm"),
            new TestRow("Стоп сцена", "story scene stop"),
            new TestRow("Перечитати сцени", "story scene reload"),
            new TestRow("Люди сюжету", "story people"),
            new TestRow("Голова: цифри", "story look"),
            new TestRow("Бог", "con god"),
            new TestRow("Привид", "con ghost"),
        };
        private static readonly TestRow[] TestStage =
        {
            new TestRow("Манекен тут", "stage up"),
            new TestRow("Прибрати", "stage down"),
            new TestRow("Щит перед ним", "stage prop ShieldBanded"),
            new TestRow("Зупинити кліп", "stage stop"),
            new TestRow("Заміряти", "stage check"),
            new TestRow("Ходити", "stage walk 8"),
            new TestRow("Не ходити", "stage walk off"),
            new TestRow("Усі ходи", "stage gait all"),
            new TestRow("Дублі кліпу", "stage takes"),
            new TestRow("Дубль назад", "stage back", true),
        };

        private void TestTabDraw()
        {
            GUILayout.BeginHorizontal();
            _testScroll = GUILayout.BeginScrollView(_testScroll, GUILayout.Width(740f));
            TestGroup("Почати заново (стерти персонажа — потім вийди зі світу й зайди знову)", TestStart);
            TestGroup("Ворони й час", TestRaven);
            TestGroup("Стан", TestState);
            if (_testVersion != StoryQuests.ReadCount) TestRowsMade();
            foreach (KeyValuePair<string, List<TestRow>> g in _testMade) TestGroup(g.Key, g.Value);
            TestGroup("Манекен", TestStage);
            if (_testClips == null) TestClipsMade();
            TestGroup("Манекен грає кліп", _testClips);
            GUILayout.EndScrollView();

            GUILayout.BeginVertical();
            GUILayout.Label("Відповідь", _head);
            _testOut = GUILayout.BeginScrollView(_testOut);
            GUILayout.Label(_testSaid, _hint);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // the rows the story's data makes (once; again when the quests are read again: StoryQuests.ReadCount)
        private int _testVersion = -1;
        private readonly List<KeyValuePair<string, List<TestRow>>> _testMade = new List<KeyValuePair<string, List<TestRow>>>();
        private List<TestRow> _testClips;

        private void TestRowsMade()
        {
            _testVersion = StoryQuests.ReadCount;
            _testMade.Clear();
            List<TestRow> forget = new List<TestRow>(), go = new List<TestRow>(), scenes = new List<TestRow>();
            HashSet<string> spots = new HashSet<string>();
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                forget.Add(new TestRow("Забути: " + q.Id, "story forget " + q.Id, true));
                foreach (StoryQuests.StageDef s in q.Stages)
                {
                    List<string> acts = new List<string>(s.Start);
                    acts.AddRange(s.Done);
                    foreach (string a in acts) if (a.StartsWith("spot ")) { string[] w = a.Split(' '); if (w.Length > 1) spots.Add(w[1]); } // (the player's own spots the quests make: «me.<name>»)
                }
            }
            foreach (Loc l in Locs.All) go.Add(new TestRow(l.Title, "story tp " + l.Id));
            foreach (string s in spots) go.Add(new TestRow("Своя: " + s, "story tp me." + s));
            StoryScenes.Load();
            foreach (StoryScenes.SceneDef s in StoryScenes.Scenes) scenes.Add(new TestRow(s.Id, "story scene " + s.Id));
            _testMade.Add(new KeyValuePair<string, List<TestRow>>("Забути квест", forget));
            _testMade.Add(new KeyValuePair<string, List<TestRow>>("Перенестись", go));
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                List<TestRow> rows = new List<TestRow>();
                foreach (StoryQuests.StageDef s in q.Stages) rows.Add(new TestRow(s.Id, "story jump " + q.Id + " " + s.Id));
                _testMade.Add(new KeyValuePair<string, List<TestRow>>((q.Name.Length > 0 ? q.Name : q.Id) + ": стати на етап", rows));
            }
            _testMade.Add(new KeyValuePair<string, List<TestRow>>("Зіграти сцену", scenes));
            AuthorRows();
        }

        // the author's own switches of the story (<source>/_test.txt: his machine only)
        private void AuthorRows()
        {
            string f = StoryFiles.Source != null ? System.IO.Path.Combine(StoryFiles.Source, "_test.txt") : null;
            if (f == null || !System.IO.File.Exists(f)) return;
            string text;
            try { text = System.IO.File.ReadAllText(f, System.Text.Encoding.UTF8); } // heavy: once when the tab's rows are made
            catch (System.Exception e) { Story.Log("test: " + f + ": " + e.Message); return; }
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string[] w = ln.Value.Split('|');
                if (w.Length < 3) { Story.Log("test: _test.txt:" + ln.Key + ": «група | напис | рядок консолі [| sure]»"); continue; }
                string group = w[0].Trim();
                List<TestRow> rows = null;
                foreach (KeyValuePair<string, List<TestRow>> g in _testMade) if (g.Key == group) rows = g.Value;
                if (rows == null) { rows = new List<TestRow>(); _testMade.Add(new KeyValuePair<string, List<TestRow>>(group, rows)); }
                rows.Add(new TestRow(w[1].Trim(), w[2].Trim(), w.Length > 3 && w[3].Trim() == "sure"));
            }
        }

        private void TestClipsMade()
        {
            _testClips = new List<TestRow>();
            foreach (KeyValuePair<string, PAnimClip> c in BodyClips.Clips) if (c.Value.Eased) _testClips.Add(new TestRow(c.Key, "stage play " + c.Key)); // (the ones made on the new curve: the story's)
            _testClips.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
        }

        private void TestGroup(string title, IList<TestRow> rows)
        {
            GUILayout.Label(title, _head);
            TestButtons(rows);
        }

        private void TestButtons(IList<TestRow> rows)
        {
            for (int i = 0; i < rows.Count; i += TestInRow)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < i + TestInRow && k < rows.Count; k++)
                {
                    TestRow r = rows[k];
                    bool asked = r.Sure && _testSure == r.Line;
                    if (GUILayout.Button(asked ? "Точно?" : r.Word, asked ? _sel : GUI.skin.button, GUILayout.Width(TestButton))) TestPress(r);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void TestPress(TestRow r)
        {
            if (r.Sure && _testSure != r.Line) { _testSure = r.Line; return; }
            _testSure = "";
            string said;
            if (r.Line.StartsWith("con "))
            {
                if (global::Console.instance == null) said = "Консолі гри нема.";
                else { global::Console.instance.TryRunCommand(r.Line.Substring(4)); said = "Гра: " + r.Line.Substring(4); }
            }
            else said = Commands.Run(r.Line);
            _testSaid = "raven " + r.Line + "\n" + (said ?? "");
            _testOut = Vector2.zero;
        }
    }
}
#endif

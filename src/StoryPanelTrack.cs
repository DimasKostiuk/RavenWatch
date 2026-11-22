#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Сюжет»: the quests' track (StoryQuests) — where this character stands, jumps, the world's keys, events.
    // Grouped as the story is (the user 2026-11-18: «там де квести то мають бути конкретно акти»): on the left the acts (and the prologue, a chapter
    // with no act: saga/chapters.txt), on the right the picked act's chapters in a row, under it that chapter's quests — its main one (named as
    // the chapter) and the parallel ones of it (ch2_eyvind) — then the picked quest's stages. A quest whose «chapter:» names no chapter of the saga
    // stands in a group of its own («Без глави»), never lost. The groups are made again only when the quests are (StoryQuests.Version).
    internal partial class StoryPanel
    {
        private readonly Dictionary<StoryQuests.StageDef, string> _waits = new Dictionary<StoryQuests.StageDef, string>();
        private float _waitsAt;

        // ---------------------------------------------------------------- «Сюжет»
        private string _trackPick = "", _trackAct = "", _trackChapter = "", _worldNew = "", _raise = "";
        private Vector2 _trackStages, _trackLog, _trackWorld;

        private sealed class TrackChapter { public string Id = "", Name = ""; public readonly List<StoryQuests.QuestDef> Quests = new List<StoryQuests.QuestDef>(); } // (Quests[0]: the main one)
        private sealed class TrackAct { public string Key = "", Label = ""; public readonly List<TrackChapter> Chapters = new List<TrackChapter>(); }
        private readonly List<TrackAct> _acts = new List<TrackAct>();
        private int _actsOf = -1;
        private const string NoChapter = "—";

        // the acts, their chapters and the chapters' quests, in the saga's order (made again when the quests are)
        private void TrackGroups()
        {
            if (_actsOf == StoryQuests.Version) return;
            _actsOf = StoryQuests.Version;
            _acts.Clear();
            Dictionary<string, TrackChapter> byId = new Dictionary<string, TrackChapter>();
            foreach (StorySaga.Chapter c in StorySaga.Chapters)
            {
                string key = c.Act != null ? "act " + c.Act.Id : "chapter " + c.Id;
                TrackAct a = _acts.Find(x => x.Key == key);
                if (a == null) { a = new TrackAct { Key = key, Label = c.Act != null ? (c.Act.Label.Length > 0 ? c.Act.Label : "Акт " + c.Act.Id) + (c.Act.Name.Length > 0 ? " — " + c.Act.Name : "") : c.Name }; _acts.Add(a); }
                TrackChapter ch = new TrackChapter { Id = c.Id, Name = c.Name };
                a.Chapters.Add(ch);
                byId[c.Id] = ch;
            }
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                TrackChapter ch;
                if (!byId.TryGetValue(q.Chapter, out ch))
                {
                    TrackAct none = _acts.Find(x => x.Key == NoChapter);
                    if (none == null) { none = new TrackAct { Key = NoChapter, Label = "Без глави" }; none.Chapters.Add(new TrackChapter { Id = NoChapter, Name = "квести без «chapter:»" }); _acts.Add(none); }
                    ch = none.Chapters[0];
                }
                if (q.Id == ch.Id) ch.Quests.Insert(0, q); else ch.Quests.Add(q); // (the main one: named as its chapter)
            }
        }

        // a group's word: something of it goes on, all of it passed, something begun, nothing begun (a quest's state is a key looked up: StoryQuests.State)
        private const string Going = "іде";
        [System.Flags] private enum Seen { None = 0, Untouched = 1, Begun = 2, Passed = 4, On = 8 } // (Begun: neither going nor passed — failed)
        private static Seen StateOf(Player p, List<StoryQuests.QuestDef> qs, Seen had)
        {
            foreach (StoryQuests.QuestDef q in qs)
            {
                string st = StoryQuests.State(p, q);
                had |= st.Length == 0 ? Seen.Untouched : st == Story.Active ? Seen.On : st == Story.Done ? Seen.Passed : Seen.Begun;
            }
            return had;
        }
        private static string Word(Seen s) { return (s & Seen.On) != 0 ? Going : s == Seen.None ? "порожньо" : s == Seen.Passed ? "пройдено" : s == Seen.Untouched ? "не почато" : "почато"; }
        private static string ChapterState(Player p, TrackChapter c) { return Word(StateOf(p, c.Quests, Seen.None)); }
        private static string ActState(Player p, TrackAct a) { Seen s = Seen.None; foreach (TrackChapter c in a.Chapters) s = StateOf(p, c.Quests, s); return Word(s); }

        // what is picked, made whole: an act (first: the last one going on), its chapter (likewise), the chapter's quest (its main one)
        private TrackChapter TrackPicked(Player p, out TrackAct act)
        {
            act = null; // (plain loops: the window draws several times a frame — no lambdas made each time)
            foreach (TrackAct a in _acts) if (a.Key == _trackAct) { act = a; break; }
            if (act == null && _acts.Count > 0)
            {
                act = _acts[0];
                foreach (TrackAct a in _acts) if (ActState(p, a) == Going) act = a;
                _trackAct = act.Key; _trackChapter = "";
            }
            if (act == null) return null;
            TrackChapter ch = null;
            foreach (TrackChapter c in act.Chapters) if (c.Id == _trackChapter) { ch = c; break; }
            if (ch == null)
            {
                ch = act.Chapters[0];
                foreach (TrackChapter c in act.Chapters) if (ChapterState(p, c) == Going) ch = c;
                _trackChapter = ch.Id; _trackPick = "";
            }
            bool picked = false;
            foreach (StoryQuests.QuestDef q in ch.Quests) if (q.Id == _trackPick) { picked = true; break; }
            if (!picked) _trackPick = ch.Quests.Count > 0 ? ch.Quests[0].Id : "";
            return ch;
        }

        // the words before the rows (Глава:, Головний:, Паралельні:): one width, the widest of them, measured once
        private float _trackLabelW;
        private float TrackLabelW()
        {
            if (_trackLabelW <= 0f) foreach (string w in new[] { "Глава:", "Головний:", "Паралельні:", "Квести:" }) _trackLabelW = Mathf.Max(_trackLabelW, Mathf.Ceil(_hint.CalcSize(new GUIContent(w)).x) + 4f);
            return _trackLabelW;
        }

        private void Track()
        {
            Player p = Player.m_localPlayer;
            StoryQuests.Load();
            TrackGroups();
            TrackAct act;
            TrackChapter chapter = TrackPicked(p, out act);
            StoryQuests.QuestDef q = StoryQuests.Find(_trackPick);
            GUILayout.BeginHorizontal();

            // left: the acts, the files, the world, the character
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Акти", _head);
            if (StoryQuests.Quests.Count == 0) GUILayout.Label("Жодного квесту. Файли: assets/story/quests (вбудовані) або " + StoryQuests.Folder, _hint);
            foreach (TrackAct a in _acts)
                if (GUILayout.Button(a.Label + "  [" + ActState(p, a) + "]", a == act ? _sel : GUI.skin.button)) { _trackAct = a.Key; _trackChapter = ""; }
            GUILayout.Space(4);
            if (GUILayout.Button("Перечитати файли")) { StoryFiles.ReloadAll(); Note = "Квестів: " + StoryQuests.Quests.Count + ", проблем: " + StoryQuests.Errors.Count; }
            foreach (string e in StoryQuests.Errors) GUILayout.Label("! " + e, _warn);

            GUILayout.Space(8);
            GUILayout.Label("Світ (спільне для всіх)", _head);
            List<string> keys = StoryWorld.All();
            _trackWorld = GUILayout.BeginScrollView(_trackWorld, GUILayout.Height(Mathf.Min(26f * keys.Count + 8f, Fill(254f + 25f * _acts.Count, 110f)))); // (the world's keys grow with the story: their own scroll, the character's lines below stay in sight; above them the acts, a button each)
            foreach (string k in keys)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(k);
                int sp = k.IndexOf(' ');
                if (GUILayout.Button("прибрати", GUILayout.Width(80f))) { if (sp < 0) StoryWorld.SetFlag(k, false); else StoryWorld.SetText(k.Substring(0, sp), ""); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            _worldNew = GUILayout.TextField(_worldNew, 40, GUILayout.Width(180f));
            if (GUILayout.Button("додати прапорець") && Story.GoodName(_worldNew)) { StoryWorld.SetFlag(_worldNew, true); _worldNew = ""; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(_confirmDelete == "world" ? "Точно стерти весь світовий сюжет?" : "Стерти світовий сюжет")) { if (_confirmDelete == "world") { Note = "Стерто ключів: " + StoryWorld.Clear(); _confirmDelete = ""; } else _confirmDelete = "world"; }

            GUILayout.Space(8);
            GUILayout.Label("Персонаж", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Живої гри: " + StoryTrackConds.Played(p) / 60 + " хв" + (StoryTrackConds.Calm(p) ? " · зараз спокійно" : " · НЕ спокійно: " + StoryTrackConds.WhyNotCalm(p)));
            if (GUILayout.Button("+10 хв", GUILayout.Width(70f))) StoryTrackConds.AddMinutes(p, 10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _raise = GUILayout.TextField(_raise, 60, GUILayout.Width(180f));
            if (GUILayout.Button("підняти подію") && _raise.Trim().Length > 0) { string[] w = _raise.Trim().Split(' '); StoryBus.Raise(w[0], w.Length > 1 ? w[1] : "", 0); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Напр.: use eirik_carving · heart_lit · scene_done raven_call", _hint);
            GUILayout.EndVertical();

            GUILayout.Space(12);
            // right: the act's chapters, the chapter's quests, the picked quest
            GUILayout.BeginVertical();
            if (act == null) { GUILayout.Label("Нема жодної глави (assets/story/saga/chapters.txt) і жодного квесту.", _hint); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
            AuthorRow chs = AuthorRow.Begin(false);
            chs.Label("Глава:", _hint, TrackLabelW());
            foreach (TrackChapter c in act.Chapters)
                if (chs.Button(c.Id == NoChapter ? c.Name : c.Id + (c.Name.Length > 0 ? " · " + c.Name : ""), c == chapter ? _tabOn : null)) { _trackChapter = c.Id; _trackPick = ""; }
            chs.End();
            AuthorRow qs = AuthorRow.Begin(false);
            for (int i = 0; i < chapter.Quests.Count; i++)
            {
                StoryQuests.QuestDef d = chapter.Quests[i];
                if (i == 0) qs.Label(d.Id == chapter.Id ? "Головний:" : "Квести:", _hint, TrackLabelW());
                else if (i == 1 && chapter.Quests[0].Id == chapter.Id) qs.Label("Паралельні:", _hint, TrackLabelW());
                string st = StoryQuests.State(p, d);
                if (qs.Button(d.Id + (d.Name.Length > 0 ? " — " + d.Name : "") + "  [" + (st.Length > 0 ? st : "не почато") + "]", d.Id == _trackPick ? _tabOn : null)) _trackPick = d.Id;
            }
            if (chapter.Quests.Count == 0) qs.Label("У цій главі ще нема квестів.", _hint, 240f);
            qs.End();
            float tabsH = chs.Tall + qs.Tall + 6f; // (the two rows over the quest: the stages' list below takes the rest)
            GUILayout.Space(6);
            if (q == null) { GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
            string state = StoryQuests.State(p, q), at = StoryQuests.At(p, q);
            GUILayout.Label(q.Id + (q.Name.Length > 0 ? " — " + q.Name : "") + " · " + q.From + (q.Server ? " · сервера" + (StoryQuests.Leads(p, q) ? "" : " · тебе ще не веде") : ""), _head);
            GUILayout.Label("Стан: " + (state.Length > 0 ? state : "не почато") + (at.Length > 0 ? " · етап: " + at : "")
                + (q.StartsText.Length > 0 ? " · почнеться сам, коли: " + q.StartsText + (q.Starts != null && StoryCond.All(p, q.Starts) ? " ✓" : " ✗") : " · сам не почнеться"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Почати з початку")) StoryQuests.Rebuild(p, q, q.First);
            StoryQuests.StageDef cur = q.Stage(at);
            GUI.enabled = cur != null && state == Story.Active;
            if (GUILayout.Button("Пройти етап зараз") && cur != null) StoryQuests.PassNow(p, q);
            if (GUILayout.Button("Завершити сцену") && cur != null && cur.Scene.Length > 0) { if (StoryScenes.Playing == cur.Scene) StoryScenes.Stop("автор завершив"); StoryBus.Raise("scene_done", cur.Scene, 0); }
            GUI.enabled = true;
            if (GUILayout.Button(q.Server ? "Стерти для світу" : "Стерти для персонажа")) StoryQuests.Rebuild(p, q, "");
            if (GUILayout.Button(_editKind == EditQuest ? "До етапів" : "Редагувати файл", GUILayout.Width(150f))) Edit(EditQuest, q.From);
            GUILayout.EndHorizontal();

            if (_editKind == EditQuest) { FileEditor(); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
            _trackStages = GUILayout.BeginScrollView(_trackStages, GUILayout.Height(Fill(270f + tabsH))); // (the chapters' and quests' rows and the quest's head and buttons above, «Що робила доріжка» below)
            foreach (StoryQuests.StageDef s in q.Stages)
            {
                bool here = s.Id == at && state == Story.Active;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("→", GUILayout.Width(30f))) StoryQuests.Jump(p, q, s.Id);
                GUILayout.Label((here ? "▶ " : "") + s.Id + " — " + s.Goal, here ? _head : GUI.skin.label);
                GUILayout.EndHorizontal();
                // (conditions may cost rays: asked twice a second for the stages shown, not each pass of the window)
                if (Time.time >= _waitsAt) { _waits.Clear(); _waitsAt = Time.time + 0.5f; }
                string more;
                if (!_waits.TryGetValue(s, out more))
                {
                    more = "   чекає: " + StoryQuests.Waits(p, s) + " · далі: " + s.Next;
                    foreach (StoryQuests.Fork f in s.Forks) more += "\n   розвилка: " + f.Text + (StoryCond.All(p, f.When) ? "  ✓" : "");
                    _waits[s] = more;
                }
                if (s.MarkAt.Length > 0) more += "\n   на мапі: " + s.MarkAt;
                string chose = s.Ways.Count > 0 ? StoryQuests.Chosen(p, StoryQuests.ChoiceOf(q, s)) : "";
                if (s.Ways.Count > 0) more += "\n   вибір «" + StoryQuests.ChoiceOf(q, s) + "»: " + (chose.Length > 0 ? chose : "ще ні");
                GUILayout.Label(more, _hint);
                if (here && s.Ways.Count > 0)
                {
                    AuthorRow ways = AuthorRow.Begin(false);
                    ways.Label("   пройти шлях:", _hint, 110f);
                    foreach (StoryQuests.StageDef w in s.Ways) if (ways.Button(w.Id)) StoryQuests.Choose(p, q, s, w);
                    ways.End();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label("Що робила доріжка", _head);
            _trackLog = GUILayout.BeginScrollView(_trackLog, GUILayout.Height(130f));
            for (int i = StoryQuests.Recent.Count - 1; i >= 0; i--) GUILayout.Label(StoryQuests.Recent[i], _hint);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // the console's short answer (raven story quests|start|jump|forget)
        internal static string TrackText(Player p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Квести: " + StoryQuests.Quests.Count + (StoryQuests.Errors.Count > 0 ? ", проблем " + StoryQuests.Errors.Count : ""));
            foreach (string e in StoryQuests.Errors) sb.Append("\n! ").Append(e);
            foreach (StoryQuests.QuestDef d in StoryQuests.Quests)
            {
                string st = StoryQuests.State(p, d), at = StoryQuests.At(p, d);
                sb.Append("\n  ").Append(d.Id).Append(": ").Append(st.Length > 0 ? st : "не почато");
                StoryQuests.StageDef s = d.Stage(at);
                if (s != null) sb.Append(" · ").Append(s.Id).Append(" — ").Append(s.Goal).Append(" · чекає ").Append(StoryQuests.Waits(p, s));
            }
            sb.Append("\nСвіт: ").Append(string.Join(", ", StoryWorld.All().ToArray()));
            return sb.ToString();
        }
    }
}
#endif

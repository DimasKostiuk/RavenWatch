#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Сюжет»: the quests' track (StoryQuests) — where this character stands, jumps, the world's keys, events.
    internal partial class StoryPanel
    {
        private readonly Dictionary<StoryQuests.StageDef, string> _waits = new Dictionary<StoryQuests.StageDef, string>();
        private float _waitsAt;

        // ---------------------------------------------------------------- «Сюжет»
        private string _trackPick = "", _worldNew = "", _raise = "";
        private Vector2 _trackList, _trackStages, _trackLog, _trackWorld;

        private void Track()
        {
            Player p = Player.m_localPlayer;
            StoryQuests.Load();
            if (_trackPick.Length == 0 && StoryQuests.Quests.Count > 0) _trackPick = StoryQuests.Quests[0].Id;
            StoryQuests.QuestDef q = StoryQuests.Find(_trackPick);
            GUILayout.BeginHorizontal();

            // left: the quests, the files, the world, the character
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Квести", _head);
            _trackList = GUILayout.BeginScrollView(_trackList, GUILayout.Height(150f));
            if (StoryQuests.Quests.Count == 0) GUILayout.Label("Жодного. Файли: assets/story/quests (вбудовані) або " + StoryQuests.Folder, _hint);
            foreach (StoryQuests.QuestDef d in StoryQuests.Quests)
            {
                string st = StoryQuests.State(p, d);
                if (GUILayout.Button(d.Id + (d.Name.Length > 0 ? " — " + d.Name : "") + "  [" + (st.Length > 0 ? st : "не почато") + "]", d.Id == _trackPick ? _sel : GUI.skin.button)) _trackPick = d.Id;
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Перечитати файли")) { StoryFiles.ReloadAll(); Note = "Квестів: " + StoryQuests.Quests.Count + ", проблем: " + StoryQuests.Errors.Count; }
            foreach (string e in StoryQuests.Errors) GUILayout.Label("! " + e, _warn);

            GUILayout.Space(8);
            GUILayout.Label("Світ (спільне для всіх)", _head);
            List<string> keys = StoryWorld.All();
            _trackWorld = GUILayout.BeginScrollView(_trackWorld, GUILayout.Height(Mathf.Min(26f * keys.Count + 8f, Fill(400f, 110f)))); // (the world's keys grow with the story: their own scroll, the character's lines below stay in sight)
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
            // right: the picked quest
            GUILayout.BeginVertical();
            if (q == null) { GUILayout.Label("Обери квест зліва.", _hint); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
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
            _trackStages = GUILayout.BeginScrollView(_trackStages, GUILayout.Height(Fill(270f))); // (the quest's head and buttons above, «Що робила доріжка» below)
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

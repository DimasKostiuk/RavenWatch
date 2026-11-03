#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Сцени»: the scenes (StoryScenes) — play, from a step, step by step, the director's log.
    internal partial class StoryPanel
    {
        // ---------------------------------------------------------------- «Сцени»
        private string _scenePick = "";
        private Vector2 _sceneList, _sceneSteps, _sceneLog;

        private void ScenesTabDraw()
        {
            Player p = Player.m_localPlayer;
            StoryScenes.Load();
            if (_scenePick.Length == 0 && StoryScenes.Scenes.Count > 0) _scenePick = StoryScenes.Scenes[0].Id;
            StoryScenes.SceneDef s = StoryScenes.Find(_scenePick);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Сцени", _head);
            _sceneList = GUILayout.BeginScrollView(_sceneList, GUILayout.Height(260f));
            if (StoryScenes.Scenes.Count == 0) GUILayout.Label("Жодної. Файли: assets/story/scenes (вбудовані) або " + StoryScenes.Folder, _hint);
            foreach (StoryScenes.SceneDef d in StoryScenes.Scenes)
                if (GUILayout.Button(d.Id + (StoryScenes.Playing == d.Id ? "  [іде]" : ""), d.Id == _scenePick ? _sel : GUI.skin.button)) _scenePick = d.Id;
            GUILayout.EndScrollView();
            if (GUILayout.Button("Перечитати файли")) { StoryFiles.Again(); StoryScenes.Reload(); Note = "Сцен: " + StoryScenes.Scenes.Count + ", проблем: " + StoryScenes.Errors.Count; }
            foreach (string e in StoryScenes.Errors) GUILayout.Label("! " + e, _warn);
            GUILayout.Space(8);
            GUILayout.Label("Сцена перерветься: смерть, удар, ворог ближче 12 м, відійти далі " + Mathf.RoundToInt(StoryScenes.FarStop) + " м. Повтор — не раніше ніж за " + Mathf.RoundToInt(StoryScenes.Retry) + " с. Один крок — не довше " + Mathf.RoundToInt(StoryScenes.StepMax) + " с.", _hint);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            GUILayout.BeginVertical();
            if (s == null) { GUILayout.Label("Обери сцену зліва.", _hint); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return; }
            GUILayout.Label(s.Id + " · " + s.From + (s.NeedsText.Length > 0 ? " · потребує: " + s.NeedsText + (s.Needs != null && StoryCond.All(p, s.Needs) ? " ✓" : " ✗") : ""), _head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Грати (без умов)")) StoryScenes.Play(s);
            GUI.enabled = StoryScenes.Playing.Length > 0;
            if (GUILayout.Button("Стоп")) StoryScenes.Stop("автор");
            GUI.enabled = StoryScenes.Held;
            if (GUILayout.Button("Далі >", GUILayout.Width(90f))) StoryScenes.Go();
            GUI.enabled = true;
            StoryScenes.StepMode = GUILayout.Toggle(StoryScenes.StepMode, " покроково");
            if (GUILayout.Button(_editKind == EditScene ? "До кроків" : "Редагувати файл", GUILayout.Width(150f))) Edit(EditScene, s.From);
            GUILayout.EndHorizontal();
            if (_editKind == EditScene) FileEditor();
            else
            {
                _sceneSteps = GUILayout.BeginScrollView(_sceneSteps, GUILayout.Height(330f));
                for (int i = 0; i < s.Steps.Count; i++)
                {
                    bool here = StoryScenes.Playing == s.Id && StoryScenes.At == i;
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(">", GUILayout.Width(28f))) StoryScenes.Play(s, i); // (from this step)
                    GUILayout.Label((here ? (StoryScenes.Held ? "|| " : "> ") : "   ") + (i + 1) + ". " + s.Steps[i].Line, here ? _head : GUI.skin.label);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.Label("«>» біля кроку — грати з нього. «Покроково» — кожен крок чекає твого «Далі».", _hint);
            }
            GUILayout.Label("Що робив режисер", _head);
            _sceneLog = GUILayout.BeginScrollView(_sceneLog, GUILayout.Height(150f));
            for (int i = StoryScenes.Recent.Count - 1; i >= 0; i--) GUILayout.Label(StoryScenes.Recent[i], _hint);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
#endif

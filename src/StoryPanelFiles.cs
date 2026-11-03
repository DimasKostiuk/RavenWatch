#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), the one editor of the story's text files (a quest's in «Сюжет», a scene's in «Сцени»: StoryFiles).
    internal partial class StoryPanel
    {
        // A story file as text, edited here (a quest's in «Сюжет», a scene's in «Сцени» — one editor): saved as the author's
        // copy (config/RavenWatch/story[/scenes] — it wins over the built-in one; the last kept as .bak), read again at
        // once; its problems shown on the left. Before a release the author's copies go into assets/story.
        private const int EditNone = 0, EditQuest = 1, EditScene = 2;
        private int _editKind;
        private string _editFile = "", _editText = "";
        private Vector2 _editScroll;

        private void Edit(int kind, string file)
        {
            if (_editKind == kind) { _editKind = EditNone; return; } // (the same button again: back to the list)
            _editKind = kind; _editFile = file;
            _editText = kind == EditQuest ? StoryQuests.FileText(file) : StoryScenes.FileText(file);
        }

        private void FileEditor()
        {
            bool quest = _editKind == EditQuest;
            string target = StoryFiles.Target(quest ? StoryFiles.Quests : StoryFiles.Scenes, quest ? StoryQuests.Folder : StoryScenes.Folder);
            GUILayout.Label("Файл «" + _editFile + "» → " + (StoryFiles.IsSource ? "у сам мод (одне джерело): " : "твоя копія (перекриває вбудований; немає source.txt): ") + target, StoryFiles.IsSource ? _hint : _warn);
            _editScroll = GUILayout.BeginScrollView(_editScroll, GUILayout.Height(380f));
            _editText = GUILayout.TextArea(_editText, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти й перечитати", GUILayout.Height(28f)))
            {
                string why = quest ? StoryQuests.SaveFile(_editFile, _editText) : StoryScenes.SaveFile(_editFile, _editText);
                int errors = quest ? StoryQuests.Errors.Count : StoryScenes.Errors.Count;
                Note = why != null ? "Не збережено: " + why : "Збережено «" + _editFile + "»: " + (quest ? "квестів " + StoryQuests.Quests.Count : "сцен " + StoryScenes.Scenes.Count) + ", проблем " + errors + (errors > 0 ? " (див. зліва)" : "") + ". Попередня копія — .bak.";
            }
            if (GUILayout.Button("Скасувати зміни", GUILayout.Height(28f))) _editText = quest ? StoryQuests.FileText(_editFile) : StoryScenes.FileText(_editFile);
            GUILayout.EndHorizontal();
            GUILayout.Label(quest ? "Поля квесту й етапів — у шапці StoryQuests.cs; умови (wait, fork) — StoryCond і StoryActions; після збереження → біля етапу перескакує на нього."
                                  : "Кроки — у шапці StoryScenes.cs; рядки камери й атмосфери — у вкладках «Камера» і «Атмосфера» (Копіювати → встав тут Ctrl+V).", _hint);
        }
    }
}
#endif

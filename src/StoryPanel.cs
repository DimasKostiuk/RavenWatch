#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story's tools window (console: raven story with nothing after it, or F9 — while the test window is closed).
    // Asleep like the blueprints: without the folder config/RavenWatch/blueprints neither F9 nor the word opens it.
    // Shift+F9 gives the mouse to the game and back (walk, look, then come back to the window).
    // Tabs, a partial file each: «Сюжет» (StoryPanelTrack), «Креслення» (StoryPanelBuild), «Актори» (StoryPanelActors),
    // «Вигляд» (StoryPanelLook), «Камера» and «Атмосфера» (StoryPanelShots), «Сцени» (StoryPanelScenes), «Режисер»
    // (StoryPanelDirector: the shots, the moves, into a scene), «Тест» (StoryPanelTest: the test's console lines as buttons); the one editor
    // of the story's text files (StoryPanelFiles). This file: the window, its tabs, its fields.
    internal partial class StoryPanel : MonoBehaviour
    {
        private static readonly string[] Tabs = { "Сюжет", "Креслення", "Актори", "Вигляд", "Камера", "Сцени", "Атмосфера", "Режисер", "Тест" };
        private const int TrackTab = 0, BuildTab = 1, ActorsTab = 2, LookTab = 3, ScenesTab = 5;

        private static StoryPanel _inst;
        internal static string Note = "";
        internal static bool MouseHere = true;
        internal static bool IsOpen { get { return _inst != null && _inst.enabled; } }
        internal static bool Wants { get { return IsOpen && MouseHere; } }

        private int _tab = TrackTab;
        private Rect _rect = new Rect(40f, 60f, 1120f, 800f);
        private GUIStyle _win, _tabOn, _head, _hint, _sel, _warn;
        private Texture2D _bg, _dot, _dotGold;
        private EventSystem _uiOff;
        private Vector2 _listScroll, _placeScroll, _rightScroll;

        // «Креслення»
        private string _saveName = "", _placeName = "", _confirmSave = "";
        private float _radius = 25f;
        private bool _ring = true, _showPlaces = true;
        private string _picked = "";
        private Blueprint.Info _info;
        private float _step = 1f;
        private float _countAt;
        private int _countPieces, _countKinds;
        private List<string> _countOdd = new List<string>();
        private string _confirmDelete = "", _confirmPlace = "";
        private readonly List<Vector3> _ringPts = new List<Vector3>();
        private Vector3 _ringFor = new Vector3(float.NaN, 0f, 0f);
        private float _ringRadius = -1f;

        // «Актори»
        private string _npcDef = "", _npcTag = "", _npcSay = "Здоров був.", _npcPlace = "";
        private NpcActor _npc;
        private Vector2 _defScroll, _liveScroll, _npcPlaceScroll;
        private static readonly string[] Emotes = { "wave", "point", "bow", "cheer", "thumbsup", "nonono", "relax", "sit", "kneel", "cry", "laugh", "headbang" };

        // «Вигляд»
        private Vector2 _lookScroll;
        private string _saveId = "", _saveNameNpc = "";
        private float _redressAt = -1f;
        private static List<string> _hairs, _beards;
        private static Dictionary<int, List<string>> _bySlot;
        private static readonly string[] SlotNames = { "Шолом", "Груди", "Ноги", "Плащ", "Пояс", "Права рука", "Ліва рука" };
        private static readonly Vector3[] SkinPresets = { new Vector3(1f, 0.86f, 0.74f), new Vector3(0.85f, 0.72f, 0.6f), new Vector3(0.7f, 0.55f, 0.42f), new Vector3(0.5f, 0.37f, 0.27f), new Vector3(0.33f, 0.24f, 0.18f), new Vector3(0.78f, 0.8f, 0.82f) };
        private static readonly string[] SkinNames = { "світла", "звичайна", "засмагла", "смаглява", "темна", "бліда (відлуння)" };
        private static readonly Vector3[] HairPresets = { new Vector3(0.08f, 0.07f, 0.06f), new Vector3(0.35f, 0.24f, 0.15f), new Vector3(0.62f, 0.28f, 0.12f), new Vector3(0.85f, 0.7f, 0.42f), new Vector3(0.6f, 0.6f, 0.58f), new Vector3(0.92f, 0.92f, 0.9f) };
        private static readonly string[] HairNames = { "чорне", "каштанове", "руде", "русяве", "сиве", "біле" };

        internal static void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_StoryPanel");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<StoryPanel>();
            }
            _inst.enabled = true;
            MouseHere = true;
            _inst.Refresh();
        }

        internal static string Said()
        {
            return IsOpen ? "Вікно сюжету відкрито. F9 — закрити, Shift+F9 — мишка між вікном і грою." : "Вікно сюжету закрито.";
        }

        private static void Close()
        {
            if (_inst == null) return;
            _inst.enabled = false;
            _inst.UiBack();
        }

        // every frame (RavenSkills): F9 opens and closes it (F8 is PlantEasily's on/off key — not ours to take)
        internal static void Tick()
        {
            if (!Blueprint.On || Player.m_localPlayer == null || DevPanel.IsOpen || !Plugin.IsLocalAdmin()) return; // (admins only: it places buildings and people)
            if (!Input.GetKeyDown(KeyCode.F9)) return;
            if (global::Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return; // (typing)
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && IsOpen) { MouseHere = !MouseHere; return; }
            if (!shift) Toggle();
        }

        private void UiBack() { if (_uiOff != null) { _uiOff.enabled = true; _uiOff = null; } }

        private void Update()
        {
            if (Player.m_localPlayer == null || !Plugin.IsLocalAdmin()) { Close(); return; }
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool over = MouseHere && _rect.Contains(m);
            EventSystem es = EventSystem.current;
            if (over && es != null && es.enabled) { es.enabled = false; _uiOff = es; }
            else if (!over) UiBack();
            if (_tab == BuildTab && Time.unscaledTime >= _countAt) { _countAt = Time.unscaledTime + 1f; Count(); } // (the live count: only on its tab)
        }

        private void LateUpdate()
        {
            if (!Wants) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // the folder read once here (the window draws several times a frame: never the disk there)
        private List<string> _names = new List<string>();

        private void Refresh()
        {
            _names = Blueprint.Names();
            List<string> names = _names;
            if (_picked.Length == 0 || !names.Contains(_picked)) _picked = names.Count > 0 ? names[0] : "";
            _info = _picked.Length > 0 ? Blueprint.Describe(_picked) : null;
        }

        // the pieces inside the circle now (twice a second)
        private void Count()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            HashSet<string> kinds = new HashSet<string>();
            _countOdd.Clear();
            List<Piece> list = Blueprint.Around(p.transform.position, _radius);
            foreach (Piece pc in list)
            {
                string n = Utils.GetPrefabName(pc.gameObject);
                if (kinds.Add(n) && !Blueprint.LooksVanilla(n)) _countOdd.Add(n);
            }
            _countPieces = list.Count;
            _countKinds = kinds.Count;
        }

        // ---------------------------------------------------------------- the window
        private void OnGUI()
        {
            GUI.depth = -2000; // (over a scene's bars and its fade to black, StoryScreen: the author never loses his window)
            if (_win == null || _bg == null)
            {
                _bg = Plugin.Solid(new Color(0.07f, 0.07f, 0.09f, 0.95f));
                _dot = Plugin.Solid(new Color(0.55f, 0.85f, 1f, 0.9f));
                _dotGold = Plugin.Solid(new Color(1f, 0.82f, 0.35f, 0.95f));
                _win = new GUIStyle(GUI.skin.window);
                _win.normal.background = _bg; _win.onNormal.background = _bg; _win.focused.background = _bg; _win.onFocused.background = _bg;
                _win.normal.textColor = new Color(0.75f, 0.88f, 1f); _win.onNormal.textColor = _win.normal.textColor;
                _win.fontSize = 15;
                _win.padding = new RectOffset(12, 12, 28, 12);
                _tabOn = new GUIStyle(GUI.skin.button); _tabOn.fontStyle = FontStyle.Bold; _tabOn.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _head = new GUIStyle(GUI.skin.label); _head.fontSize = 15; _head.fontStyle = FontStyle.Bold; _head.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _hint = new GUIStyle(GUI.skin.label); _hint.wordWrap = true; _hint.normal.textColor = new Color(0.7f, 0.7f, 0.72f);
                _sel = new GUIStyle(GUI.skin.button); _sel.fontStyle = FontStyle.Bold; _sel.normal.textColor = new Color(1f, 0.83f, 0.3f); _sel.alignment = TextAnchor.MiddleLeft;
                _warn = new GUIStyle(GUI.skin.label); _warn.wordWrap = true; _warn.normal.textColor = new Color(1f, 0.55f, 0.45f);
            }
            WorldMarks();
            GUI.color = Color.white; GUI.backgroundColor = Color.white;
            _rect.width = Mathf.Min(DirCompact ? 460f : _tab == DirectorTab && _dirMore ? 1260f : 1120f, Screen.width - 10f); // (the director's three columns; the free camera: out of the picture)
            _rect.height = Mathf.Min(DirCompact ? 150f : 800f, Screen.height - 40f);
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - 60f));
            _rect = GUI.Window(0x52575354, _rect, Draw, "Ворони: сюжет", _win);
        }

        private void Draw(int id)
        {
            if (DirCompact) { DirFlying(); GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f)); return; }
            GUILayout.Label(MouseHere ? "Мишка: ВІКНО. Shift+F9 — віддати мишку грі (ходити, дивитись). F9 — закрити вікно."
                                      : "Мишка: ГРА. Shift+F9 — повернути мишку на вікно.", _hint);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (GUILayout.Button(Tabs[i], i == _tab ? _tabOn : GUI.skin.button)) _tab = i;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            if (_tab == TrackTab) Track();
            else if (_tab == BuildTab) Build();
            else if (_tab == ActorsTab) Actors();
            else if (_tab == LookTab) Look();
            else if (_tab == ScenesTab) ScenesTabDraw();
            else if (_tab == CameraTab) CameraTabDraw();
            else if (_tab == AirTab) AirTabDraw();
            else if (_tab == DirectorTab) DirectorTabDraw();
            else if (_tab == TestTab) TestTabDraw();

            GUILayout.FlexibleSpace();
            if (Note.Length > 0) GUILayout.Label(Note, _hint);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрити (F9)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
        }
    }
}
#endif

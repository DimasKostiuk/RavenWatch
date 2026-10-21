#if RAVEN_ITEM
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The test window (console: raven test; raven land / flight / wingui / workshop / sounds open it on their tab):
    // every test and tuning tool in one window, tabs:
    //   Посадка   — the landing tuning and the outings (LandPanel)
    //   Політ     — the ravens' motor: how they fly, each its own, keeping clear, with a picture (FlightPanel)
    //   Шаблони   — flight templates: where each may fly, its motion and pose, the director's rhythm (TemplatePanel)
    //   Змах крил — the wing beat's ten points and the adaptive strokes (WingPanel)
    //   Зліт      — five editable poses for leaving the shoulder (TakeoffPanel)
    //   Майстерня — the raven model builder (Workshop); the window widens for it
    //   Фото      — the icons of the Workshop's models, taken by hand (PhotoPanel)
    //   Фарби     — raven looks: colours by zone, eyes, glow, sheen, sparks (LooksPanel; console raven looks)
    //   Звуки     — the game's sound library (SoundPanel)
    //   Пасивки   — every passive gift fired by a click, without its conditions, strength or wait
    //   Персонаж  — our own animations for the player: clips, keys, bones (PlayerAnimPanel; console raven panim)
    //   Руни      — every rune effect by layer with a code to name it by, worn on a stage raven, and the mix (RunePanel; raven wear ui)
    //   Доручення — give yourself any errand, see what is written, go there, the nest, the runes (ErrandPanel; raven errand ui)
    // F8 switches the mouse between the window (cursor shown, the game's input paused) and the game.
    internal class DevPanel : MonoBehaviour
    {
        internal const int AnimTab = 0, SoundsTab = 1, PassivesTab = 2, FlightTab = 3, WingsTab = 4, WorkshopTab = 5, TemplatesTab = 6, TurnTab = 7, TakeoffTab = 8, PhotoTab = 9, SitTab = 10, LooksTab = 11, PlayerTab = 12, ErrandsTab = 13, RunesTab = 14;
        private static readonly string[] Tabs = { "Посадка", "Звуки", "Пасивки", "Політ", "Змах крил", "Майстерня", "Шаблони", "Розворот", "Зліт", "Фото", "Сидіння", "Фарби", "Персонаж", "Доручення", "Руни" };
        private static readonly int[] Order = { RunesTab, ErrandsTab, AnimTab, FlightTab, TemplatesTab, SitTab, WingsTab, TakeoffTab, WorkshopTab, PhotoTab, LooksTab, PlayerTab, TurnTab, SoundsTab, PassivesTab };

        private static DevPanel _inst;
        private LandPanel _land;
        private SoundPanel _sounds;
        private WingPanel _wings;
        private TakeoffPanel _takeoff;
        private Workshop _workshop;
        private PhotoPanel _photo;
        private LooksPanel _looks;
        private RunePanel _runes;
        private readonly FlightPanel _flight = new FlightPanel();
        private readonly TemplatePanel _templates = new TemplatePanel();
        private readonly PerchTurnPanel _turn = new PerchTurnPanel(); // the raven turning round on its perch
        private readonly SitPanel _sit = new SitPanel(); // the sitting system on your ravens (templates, body, reactions)
        private readonly PlayerAnimPanel _player = new PlayerAnimPanel(); // our own animations for the player
        private readonly ErrandPanel _errands = new ErrandPanel(); // Odin's errands, the nest, the runes
        private int _tab = AnimTab;
        private Rect _rect = new Rect(20f, 60f, 560f, 760f);
        private GUIStyle _win, _tabOn;
        private Texture2D _bg;
        private EventSystem _uiOff;
        private string _note = "";
        private Vector2 _scroll;

        internal static bool IsOpen { get { return _inst != null && _inst.enabled; } }
        internal static bool MouseHere = true;
        internal static bool Wants { get { return IsOpen && MouseHere; } }

        // tab -1: as it was left. The same tab again closes it; another tab switches to it.
        internal static void Toggle(int tab)
        {
            if (IsOpen && (tab < 0 || tab == _inst._tab)) { Close(); return; }
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_DevPanel");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<DevPanel>();
                _inst._land = go.AddComponent<LandPanel>();
                _inst._sounds = go.AddComponent<SoundPanel>();
                _inst._wings = go.AddComponent<WingPanel>(); _inst._wings.enabled = false;
                _inst._takeoff = go.AddComponent<TakeoffPanel>(); _inst._takeoff.enabled = false;
                _inst._workshop = go.AddComponent<Workshop>(); _inst._workshop.enabled = false;
                _inst._photo = go.AddComponent<PhotoPanel>(); _inst._photo.enabled = false;
                _inst._looks = go.AddComponent<LooksPanel>(); _inst._looks.enabled = false;
                _inst._runes = go.AddComponent<RunePanel>(); _inst._runes.enabled = false;
            }
            bool was = IsOpen;
            int from = _inst._tab;
            if (tab >= 0) _inst._tab = tab;
            _inst.enabled = true;
            MouseHere = true;
            if (was && from != _inst._tab) _inst.Leave(from);
            if (!was || from != _inst._tab) _inst.Enter(_inst._tab);
        }

        // a tab shown / left: the tools that keep a picture or a stage start and stop with it
        private void Enter(int tab)
        {
            if (tab == WingsTab) _wings.enabled = true;
            else if (tab == TakeoffTab) { FlightStage.TakeoffPreview = true; _takeoff.enabled = true; }
            else if (tab == WorkshopTab) _workshop.Opened(); // it shows its own note
            else if (tab == PhotoTab) _photo.Opened();
            else if (tab == LooksTab) _looks.Opened();
            else if (tab == RunesTab) _runes.Opened();
            else if (tab == PlayerTab) _player.Opened();
        }

        private void Leave(int tab)
        {
            if (tab == WingsTab) _wings.Closed();
            else if (tab == TakeoffTab) _takeoff.Closed();
            else if (tab == WorkshopTab) _workshop.Closed(false);
            else if (tab == PhotoTab) _photo.Closed();
            else if (tab == LooksTab) _looks.Closed();
            else if (tab == RunesTab) _runes.Closed();
            else if (tab == FlightTab) FlightStage.Close();
            else if (tab == TemplatesTab) { _templates.Closed(); FlightStage.Close(); }
            else if (tab == TurnTab) { PerchTurn.Hold = -1f; PerchTurnStage.Close(); } // a held preview lets the raven go
            else if (tab == SitTab) _sit.Closed(); // a held pose, a forced mood: let go
            else if (tab == PlayerTab) _player.Closed(); // the preview stops
        }

        internal static string Said()
        {
            return IsOpen ? "Тестове вікно: " + Tabs[_inst._tab] + ". F8 — мишка між вікном і грою." : "Тестове вікно закрито.";
        }

        private static void Close()
        {
            if (_inst == null) return;
            _inst._land.Closed();
            _inst._sounds.Closed();
            _inst._wings.Closed();
            _inst._takeoff.Closed();
            _inst._workshop.Closed(true);
            _inst._photo.Closed();
            _inst._looks.Closed();
            _inst._runes.Closed();
            _inst._templates.Closed();
            _inst._sit.Closed();
            _inst._player.Closed();
            PerchTurn.Hold = -1f;
            PerchTurnStage.Close();
            FlightStage.Close(); // the «Політ» / «Шаблони» picture
            _inst.enabled = false;
            _inst.UiBack();
        }

        private void UiBack() { if (_uiOff != null) { _uiOff.enabled = true; _uiOff = null; } }

        private void Update()
        {
            if (Player.m_localPlayer == null) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.F8)) MouseHere = !MouseHere;
            // clicks on the window must not reach the game's or other mods' UI underneath
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool over = MouseHere && _rect.Contains(m);
            EventSystem es = EventSystem.current;
            if (over && es != null && es.enabled) { es.enabled = false; _uiOff = es; }
            else if (!over) UiBack();
        }

        private void LateUpdate()
        {
            if (!Wants) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (_win == null || _bg == null)
            {
                _bg = Plugin.Solid(new Color(0.08f, 0.07f, 0.06f, 0.94f));
                _win = new GUIStyle(GUI.skin.window);
                _win.normal.background = _bg; _win.onNormal.background = _bg; _win.focused.background = _bg; _win.onFocused.background = _bg;
                _win.normal.textColor = new Color(0.95f, 0.82f, 0.55f);
                _win.onNormal.textColor = _win.normal.textColor;
                _win.fontSize = 15;
                _win.padding = new RectOffset(12, 12, 28, 12);
                _tabOn = new GUIStyle(GUI.skin.button); _tabOn.fontStyle = FontStyle.Bold; _tabOn.normal.textColor = new Color(1f, 0.83f, 0.3f);
            }
            GUI.color = Color.white; GUI.backgroundColor = Color.white;
            Vector2 size = _tab == TakeoffTab ? new Vector2(1280f, 900f) : _tab == WorkshopTab || _tab == PhotoTab || _tab == LooksTab || _tab == RunesTab ? new Vector2(1280f, 820f) : _tab == WingsTab ? new Vector2(920f, 720f) : _tab == TemplatesTab || _tab == FlightTab || _tab == TurnTab || _tab == SitTab || _tab == PlayerTab || _tab == ErrandsTab ? new Vector2(1100f, 820f) : new Vector2(560f, 780f);
            _rect.width = Mathf.Min(size.x, Screen.width - 10f);
            _rect.height = Mathf.Min(size.y, Screen.height - 40f);
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - 60f));
            _rect = GUI.Window(0x52574456, _rect, Draw, "Ворони: тест", _win);
        }

        private void Draw(int id)
        {
            GUILayout.Label(MouseHere ? "Мишка: ВІКНО. F8 — віддати мишку грі (камера, персонаж)." : "Мишка: ГРА. F8 — повернути мишку на вікно.");
            GUILayout.BeginHorizontal();
            foreach (int i in Order)
                if (GUILayout.Button(Tabs[i], i == _tab ? _tabOn : GUI.skin.button) && i != _tab) Toggle(i);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            switch (_tab)
            {
                case AnimTab: _land.Body(); break;
                case SoundsTab: _sounds.Body(_rect.height - 40f); break;
                case FlightTab: _flight.Body(_rect.height - 150f); break;
                case WingsTab: _wings.Body(); break;
                case TakeoffTab: _takeoff.Body(); break;
                case TemplatesTab: _templates.Body(_rect.height - 130f); break;
                case WorkshopTab: _workshop.Body(_rect.height - 112f); break;
                case PhotoTab: _photo.Body(_rect.height - 112f); break;
                case LooksTab: _looks.Body(_rect.height - 112f); break;
                case RunesTab: _runes.Body(_rect.height - 112f); break;
                case TurnTab: _turn.Body(_rect.height - 150f); break;
                case SitTab: _sit.Body(_rect.height - 150f); break;
                case PlayerTab: _player.Body(_rect.height - 170f); break;
                case ErrandsTab: _errands.Body(_rect.height - 112f); break;
                default: Passives(); break;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрити", GUILayout.Width(100f))) Close();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
        }

        // ---------------------------------------------------------------- Пасивки
        private void Passives()
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Клік запускає пасивний дар так, як він спрацьовує в грі: ефект, світіння, фраза, повідомлення. Без умов (удару, бігу, малого здоров'я, смерті), без витрати сили й без перезарядки — справжня перезарядка дару не змінюється.");
            GUILayout.Space(6);

            GUILayout.Label("<b><color=#8EC9FF>Хугін</color></b>" + (hugin == null ? "  <color=#FF7A7A>не на плечі</color>" : ""));
            Row(p, "lightstep", hugin != null, () => WeakPassive.TestStep(p));
            Row(p, "lastchance", hugin != null, () => LastChance.Test(p));
            Row(p, "hloyal", hugin != null, () => _note = Loyal.Test(p, true, false));

            GUILayout.Space(8);
            GUILayout.Label("<b><color=#FF8A7A>Мунін</color></b>" + (munin == null ? "  <color=#FF7A7A>не на плечі</color>" : ""));
            Row(p, "anger", munin != null, () => WeakPassive.TestAnger(p));
            Row(p, "bloodfury", munin != null, () => BloodFury.Test(p));
            Row(p, "mloyal", munin != null, () => _note = Loyal.Test(p, false, true));

            GUILayout.Space(8);
            GUI.enabled = hugin != null && munin != null;
            if (GUILayout.Button("Вірність і Впертість разом (обидва прилітають)")) _note = Loyal.Test(p, true, true);
            GUI.enabled = true;
            if (GUILayout.Button("Скинути перезарядки всіх дарів")) { RavenSkills.ResetCooldowns(); _note = "Перезарядки й образи скинуто."; }
            GUILayout.EndScrollView();
            if (_note.Length > 0) GUILayout.Label(_note);
        }

        private void Row(Player p, string id, bool here, System.Action fire)
        {
            RavenAbility a = RavenAbilities.Find(id);
            if (a == null) return;
            GUILayout.BeginHorizontal();
            GUI.enabled = here && p != null && !p.IsDead();
            if (GUILayout.Button("▶", GUILayout.Width(34f), GUILayout.Height(24f)))
            {
                _note = a.Plain + ": запущено.";
                fire();
            }
            GUI.enabled = true;
            GUILayout.Label(a.Plain, GUILayout.Width(150f));
            GUILayout.Label(here ? RavenAbilities.PassiveReady(a) : "");
            GUILayout.EndHorizontal();
        }
    }
}
#endif

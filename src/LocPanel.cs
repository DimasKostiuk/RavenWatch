#if RAVEN_ITEM && RAVEN_NPC
// (the author's window for the Hall's states: from 2.9 — the user 2026-11-01)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The author's window for the story's places — the Hall of the Watch and every other one (Loc, Locs): a place picked
    // at the top, or a new one made: F10 (console: raven loc / raven hall; the story window has a button too).
    // Asleep like the blueprints: without config/RavenWatch/blueprints it does not open.
    // Shift+F10 gives the mouse to the game and back: walk, aim at a piece, come back and give it a layer.
    //   · the frame: where the Hall's blueprint is fixed (BlueprintFrame) — saves never shift it;
    //   · a look at each state on the Hall he builds by hand (LocPreview: drawn only for him, nothing kept);
    //   · the layers: the piece under the crosshair put into «ruin», «repaired», «lived» or none (LocParts);
    //   · the save of the Hall (always in its frame);
    //   · the Hall a blueprint put into this world: its real state, now or behind the scenes.
    internal class LocPanel : MonoBehaviour
    {
        private static LocPanel _inst;
        internal static bool MouseHere = true;
        internal static bool IsOpen { get { return _inst != null && _inst.enabled; } }
        internal static bool Wants { get { return IsOpen && MouseHere; } }

        private Rect _rect = new Rect(60f, 70f, 620f, 760f);
        private GUIStyle _win, _head, _hint, _warn, _on, _step, _rich;
        private Texture2D _bg;
        private EventSystem _uiOff;
        private Vector2 _scroll;
        private string _note = "";
        private float _radius = 25f;
        private bool _confirmHere;
        private ZNetView _target;
        private string _targetName = "", _targetLayer = "";
        private Loc _loc;                         // the place worked on
        private string _newId = "", _newName = ""; // a new place's blueprint name and title
        private bool _adding, _confirmRebuild;
        private string _newSub = ""; private bool _addingSub; // a new sub-state's name, for the state looked at
        private bool _behind;                                 // the world's state changed behind the scenes (else at once)
        private string _nextText = ""; private float _nextAt; private Loc _nextFor; // («Що далі»: looked at once a second)

        internal static string Toggle()
        {
            if (!Blueprint.On) return "Вікно локацій спить: немає теки " + Blueprint.Root;
            if (IsOpen) { Close(); return "Вікно «Локації сюжету» закрито."; }
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_LocPanel");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<LocPanel>();
            }
            _inst.enabled = true;
            MouseHere = true;
#if RAVEN_STORY
            if (StoryPanel.IsOpen) StoryPanel.Toggle(); // (one window at a time: they lay over each other)
#endif
            return "Вікно «Локації сюжету» відкрито (F10). Shift+F10 — мишка між вікном і грою.";
        }

        private static void Close()
        {
            if (_inst == null) return;
            _inst.enabled = false;
            _inst.UiBack();
            LocPreview.Set(0); // (closed: the Hall as it is — no one left wondering why it looks ruined)
        }

        // every frame (RavenSkills): F10 opens and closes it, Shift+F10 moves the mouse (F9 is the story window's)
        internal static void Tick()
        {
            if (!Blueprint.On || Player.m_localPlayer == null || DevPanel.IsOpen || !Plugin.IsLocalAdmin()) return;
            if (!Input.GetKeyDown(KeyCode.F10) || global::Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && IsOpen) { MouseHere = !MouseHere; return; }
            if (!shift) Toggle();
        }

        private void UiBack() { if (_uiOff != null) { _uiOff.enabled = true; _uiOff = null; } }

        private void Update()
        {
            Player p = Player.m_localPlayer;
            if (p == null || !Plugin.IsLocalAdmin()) { Close(); return; }
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool over = MouseHere && _rect.Contains(m);
            EventSystem es = EventSystem.current;
            if (over && es != null && es.enabled) { es.enabled = false; _uiOff = es; }
            else if (!over) UiBack();
            // the piece under the crosshair, kept while the mouse is back on the window
            if (!MouseHere)
            {
                ZNetView nv = LocParts.Aimed(p);
                if (nv != null && nv != _target) { _target = nv; Look(); }
                Keys(p);
            }
            if (_target == null && _targetName.Length > 0) { _targetName = ""; _targetLayer = ""; }
        }

        // the mouse in the game: aim, a key on the number pad gives the piece its layer (no going back to the window).
        // Two modes, by what is looked at (the user 2026-11-01): a state («Як є» / 1 / 2 …) — the keys give the piece its
        // states' part and never touch its sub-states' marks; a sub-state (1.1 …) — the keys only mark the piece for that
        // sub-state and never touch its states' part
        private static readonly KeyCode[] LayerKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad8, KeyCode.Keypad0 };
        private static readonly string[] KeyLayers = { "ruin", "repaired", "lived", "fixed", "-" };

        // a sub-state looked at: its state and number (0 — a state's mode)
        private int SubMode(out int state)
        {
            state = LocPreview.Of == _loc && _loc != null ? LocPreview.State : 0;
            return state > 0 ? LocPreview.Sub : 0;
        }

        // another place (its own states): Num1/Num2/Num3 — only in state 1/2/3, Num8 — from the state looked at on, Num0 — none
        private string KeyLayer(int i)
        {
            if (_loc == null || _loc.Last >= 3) return KeyLayers[i]; // (three states and more: the named layers — ruin, repaired, lived)
            switch (LayerKeys[i])
            {
                case KeyCode.Keypad1: return "only1";
                case KeyCode.Keypad2: return _loc.Last >= 2 ? "only2" : null;
                case KeyCode.Keypad3: return _loc.Last >= 3 ? "only3" : null;
                case KeyCode.Keypad8: return _loc.Last >= 2 ? "from" + Mathf.Max(2, LocPreview.Of == _loc ? LocPreview.State : 2) : null;
                case KeyCode.Keypad0: return "-";
            }
            return null;
        }

        // what the keys do now, short
        private string KeysText()
        {
            int s, k = SubMode(out s);
            if (k > 0) return "Num1 — є ЛИШЕ в «" + _loc.SubWord(s, k) + "» · Num3 — з'являється з «" + _loc.SubWord(s, k) + "» і далі · Num2 — прибрати в «" + _loc.SubWord(s, k) + "» · Num0 — у «" + _loc.SubWord(s, k) + "» як у початку";
            if (_loc == null || _loc.Last >= 3) return "Num1 — лише закинута · Num2 — з відремонтованої · Num3 — з обжитої · Num8 — лише відремонтована · Num0 — завжди";
            string t = "Num1 — лише стан 1";
            if (_loc.Last >= 2) t += " · Num2 — лише 2";
            if (_loc.Last >= 3) t += " · Num3 — лише 3";
            return t + (_loc.Last >= 2 ? " · Num8 — з того, що дивишся, і далі" : "") + " · Num0 — завжди";
        }

        // what is edited now, in words
        private string ModeText()
        {
            int s, k = SubMode(out s);
            return k > 0 ? "Правиш субстан «" + _loc.SubWord(s, k) + "» стану «" + _loc.WordOf(s) + "» — стани й інші субстани не міняються"
                         : "Правиш стани — позначки субстанів не міняються";
        }

        // where the piece is: every state and its steps (0 — its start), present green, absent red with ×
        private string Where(string layer)
        {
            List<string> parts = new List<string>();
            for (int s = 1; s <= _loc.Last; s++)
            {
                int n = _loc.SubsOf(s), there = 0;
                for (int k = 0; k <= n; k++) if (LocState.Shows(layer, s, k)) there++;
                if (there == 0 || there == n + 1) { parts.Add(Paint(_loc.WordOf(s), there > 0)); continue; }
                List<string> steps = new List<string>();
                for (int k = 0; k <= n; k++) steps.Add(Paint(k == 0 ? "початок" : _loc.SubWord(s, k), LocState.Shows(layer, s, k)));
                parts.Add(_loc.WordOf(s) + ": " + string.Join(" · ", steps.ToArray()));
            }
            return string.Join("   |   ", parts.ToArray());
        }
        private static string Paint(string word, bool there) { return there ? "<color=#7CFC7C>" + word + "</color>" : "<color=#ff6b6b>" + word + "×</color>"; }

        // the piece's states' part set (its sub-states' marks kept); a piece only of sub-states is not a state's
        private string SetMain(ZNetView nv, string main)
        {
            string of; Vector3 at;
            if (nv == null || !nv.IsValid()) return LocParts.Layer(nv, main); // (its own words why not)
            string now = LocParts.Place(nv, out of, out at) && of == _loc.Id ? LocParts.LayerAt(of, Utils.GetPrefabName(nv.gameObject), at) : null;
            if (now == null) return LocParts.Layer(nv, main); // (its own words why not)
            if (LocState.SubOnly(now)) return "«" + Utils.GetPrefabName(nv.gameObject) + "» — деталь субстану (" + LocState.LayerWhen(now, _loc) + "). Її правиш у режимі субстану.";
            return Change(nv, now, LocState.WithMain(now, main));
        }

        // every change of a piece's layer remembered (the piece and its layer before): Num* — back one step, whatever
        // the mode (a key pressed on the wrong piece — the user 2026-11-01)
        private struct Was { internal ZNetView Nv; internal string Layer; }
        private readonly List<Was> _undo = new List<Was>();
        private const int UndoMax = 50;

        private string Change(ZNetView nv, string before, string after)
        {
            string done = LocParts.Layer(nv, after);
            if (after == before) return done;
            _undo.Add(new Was { Nv = nv, Layer = before });
            if (_undo.Count > UndoMax) _undo.RemoveAt(0);
            return done;
        }

        private string Undo()
        {
            while (_undo.Count > 0)
            {
                Was w = _undo[_undo.Count - 1];
                _undo.RemoveAt(_undo.Count - 1);
                if (w.Nv == null || !w.Nv.IsValid()) continue; // (the piece gone since: the step before it)
                _target = w.Nv;
                return "Повернуто: " + LocParts.Layer(w.Nv, w.Layer);
            }
            return "Нема чого повертати.";
        }

        // Num.: the piece wholly cleared — no state's layer, no sub-state's mark — in any mode (a piece hidden in the
        // sub-state looked at cannot be aimed at: cleared from «Як є» or another look where it stands)
        private string Reset(ZNetView nv)
        {
            string of; Vector3 at;
            if (nv == null || !nv.IsValid()) return LocParts.Layer(nv, "-"); // (its own words why not)
            string now = LocParts.Place(nv, out of, out at) && of == _loc.Id ? LocParts.LayerAt(of, Utils.GetPrefabName(nv.gameObject), at) : null;
            if (now == null) return LocParts.Layer(nv, "-");
            if (now.Length == 0) return "«" + Utils.GetPrefabName(nv.gameObject) + "» і так без шару: є в усіх станах.";
            return Change(nv, now, "");
        }

        // the piece marked for the sub-state looked at: true — there only in it, false — gone in it, null — no mark
        private string SetMark(ZNetView nv, int s, int k, bool? there)
        {
            string of; Vector3 at;
            if (nv == null || !nv.IsValid()) return LocParts.Layer(nv, "-"); // (its own words why not)
            string name = Utils.GetPrefabName(nv.gameObject);
            string now = LocParts.Place(nv, out of, out at) && of == _loc.Id ? LocParts.LayerAt(of, name, at) : null;
            if (now == null) return LocParts.Layer(nv, "-"); // (its own words why not)
            string main = LocState.MainOf(now), sub = "«" + _loc.WordOf(s, k) + "»";
            bool? had = LocState.MarkFor(now, s, k);
            if (there == true && main.Length > 0)
            {
                // a piece of this state only (as «ruin» in the abandoned): made this step's only — no other state changes
                for (int o = 1; o <= _loc.Last; o++)
                    if (o != s && LocState.Shows(main, o, 0))
                        return "«" + name + "» є й в інших станах (" + LocState.MainWhen(main) + "): у режимі субстану їх не міняю. Num. — скинути деталь, тоді Num1.";
                return Change(nv, now, LocState.OnlyIn(now, s, k));
            }
            if (there == false && LocState.SubOnly(now))
                return "«" + name + "» і так є лише в субстанах (" + LocState.LayerWhen(now, _loc) + ")." + (had == true ? " Прибрати з " + sub + " — Num0." : "");
            if (there == false && !LocState.Shows(LocState.MainOf(now), s, 0))
                return "«" + name + "» і так нема в стані «" + _loc.WordOf(s) + "» (" + LocState.MainWhen(main) + ").";
            if (there == null && had == null && LocState.FromOf(now, s) == k) return Change(nv, now, LocState.WithFrom(now, s, k, false)); // («з цього кроку» taken off)
            if (there == null && had == null)
                return "«" + name + "» не має позначки " + sub + (LocState.HasAnyMark(now) ? ": її позначки — інших субстанів (" + LocState.LayerWhen(now, _loc) + "). Num. — скинути деталь повністю (без шару й позначок), Num* — повернути останню зміну." : ".");
            string layer = LocState.WithMark(now, s, k, there);
            string done = Change(nv, now, layer);
            if (there == null && had == true && !LocState.SubOnly(layer) && LocState.MainOf(layer).Length == 0) done += " Увага: тепер вона без шару — є в усіх станах.";
            return done;
        }

        // Num3: the piece there from the step looked at on — not at its state's start nor in the steps before (the
        // state's part and the later states as they are); again — off
        private string SetFrom(ZNetView nv, int s, int k)
        {
            string of; Vector3 at;
            if (nv == null || !nv.IsValid()) return LocParts.Layer(nv, "-"); // (its own words why not)
            string name = Utils.GetPrefabName(nv.gameObject);
            string now = LocParts.Place(nv, out of, out at) && of == _loc.Id ? LocParts.LayerAt(of, name, at) : null;
            if (now == null) return LocParts.Layer(nv, "-");
            if (LocState.SubOnly(now)) return "«" + name + "» і так є лише в субстанах (" + LocState.LayerWhen(now, _loc) + "). Num. — скинути, тоді Num3.";
            if (!LocState.Shows(LocState.MainOf(now), s, 0)) return "«" + name + "» нема в стані «" + _loc.WordOf(s) + "» (" + LocState.MainWhen(LocState.MainOf(now)) + "): спершу дай їй шар стану в режимі станів.";
            bool already = LocState.FromOf(now, s) == k;
            return Change(nv, now, LocState.WithFrom(now, s, k, !already));
        }
        // and the turn the hammer cannot: Num4/Num6 tip left/right, Num7/Num9 tip away/towards you (round the camera's
        // own axes, level), Num+/Num− lift/lower, Num5 level again, Num/ the step big or small (Shift with the number
        // pad turns its keys into Home/End…: not used)
        private bool _fine;
        private void Keys(Player p)
        {
            if (global::Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return;
            if (Input.GetKeyDown(KeyCode.KeypadDivide)) { _fine = !_fine; p.Message(MessageHud.MessageType.Center, _fine ? "Крок: дрібний (5°, 1 см)" : "Крок: великий (15°, 5 см)"); return; }
            float deg = _fine ? 5f : 15f, lift = _fine ? 0.01f : 0.05f;
            Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : p.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Quaternion turn = Quaternion.identity; Vector3 shift = Vector3.zero; bool level = false, act = true;
            if (Input.GetKeyDown(KeyCode.Keypad4)) turn = Quaternion.AngleAxis(deg, fwd);
            else if (Input.GetKeyDown(KeyCode.Keypad6)) turn = Quaternion.AngleAxis(-deg, fwd);
            else if (Input.GetKeyDown(KeyCode.Keypad7)) turn = Quaternion.AngleAxis(deg, right);
            else if (Input.GetKeyDown(KeyCode.Keypad9)) turn = Quaternion.AngleAxis(-deg, right);
            else if (Input.GetKeyDown(KeyCode.KeypadPlus)) shift = Vector3.up * lift;
            else if (Input.GetKeyDown(KeyCode.KeypadMinus)) shift = Vector3.down * lift;
            else if (Input.GetKeyDown(KeyCode.Keypad5)) level = true;
            else act = false;
            if (act)
            {
                ZNetView nv = LocParts.Aimed(p);
                string why = LocParts.Nudge(nv, turn, shift, level);
                if (why != null) p.Message(MessageHud.MessageType.Center, why);
                return;
            }
            if (Input.GetKeyDown(KeyCode.KeypadMultiply)) { _note = Undo(); if (_target != null) Look(); LocPreview.Again(); p.Message(MessageHud.MessageType.Center, _note); return; }
            if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                ZNetView aimed = LocParts.Aimed(p);
                if (aimed == null) { p.Message(MessageHud.MessageType.Center, "Приціл не на деталі."); return; }
                _target = aimed;
                _note = Reset(aimed);
                Look();
                LocPreview.Again();
                p.Message(MessageHud.MessageType.Center, _note);
                return;
            }
            int s, k = SubMode(out s);
            for (int i = 0; i < LayerKeys.Length; i++)
            {
                if (!Input.GetKeyDown(LayerKeys[i])) continue;
                bool? mark = null;
                bool from = false;
                string layer = null;
                if (k > 0)
                {
                    if (LayerKeys[i] == KeyCode.Keypad1) mark = true;
                    else if (LayerKeys[i] == KeyCode.Keypad2) mark = false;
                    else if (LayerKeys[i] == KeyCode.Keypad3) from = true;
                    else if (LayerKeys[i] != KeyCode.Keypad0) return; // (a state's key: nothing in a sub-state's mode)
                }
                else if ((layer = KeyLayer(i)) == null) return;
                ZNetView nv = LocParts.Aimed(p);
                if (nv == null) { p.Message(MessageHud.MessageType.Center, "Приціл не на деталі."); return; }
                _target = nv;
                _note = k > 0 ? (from ? SetFrom(nv, s, k) : SetMark(nv, s, k, mark)) : SetMain(nv, layer);
                Look();
                LocPreview.Again();
                p.Message(MessageHud.MessageType.Center, _note);
                return;
            }
        }

        private void Look()
        {
            string of; Vector3 at;
            _targetName = Utils.GetPrefabName(_target.gameObject);
            _targetLayer = LocParts.Place(_target, out of, out at) && _loc != null && of == _loc.Id ? LocParts.LayerAt(of, _targetName, at) : null;
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
                _bg = Plugin.Solid(new Color(0.07f, 0.07f, 0.09f, 0.95f));
                _win = new GUIStyle(GUI.skin.window);
                _win.normal.background = _bg; _win.onNormal.background = _bg; _win.focused.background = _bg; _win.onFocused.background = _bg;
                _win.normal.textColor = new Color(1f, 0.83f, 0.3f); _win.onNormal.textColor = _win.normal.textColor;
                _win.fontSize = 15;
                _win.padding = new RectOffset(12, 12, 28, 12);
                _head = new GUIStyle(GUI.skin.label); _head.fontSize = 15; _head.fontStyle = FontStyle.Bold; _head.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _hint = new GUIStyle(GUI.skin.label); _hint.wordWrap = true; _hint.normal.textColor = new Color(0.7f, 0.7f, 0.72f);
                _warn = new GUIStyle(GUI.skin.label); _warn.wordWrap = true; _warn.normal.textColor = new Color(1f, 0.55f, 0.45f);
                _on = new GUIStyle(GUI.skin.button); _on.fontStyle = FontStyle.Bold; _on.normal.textColor = new Color(1f, 0.83f, 0.3f);
                _step = new GUIStyle(GUI.skin.box); _step.wordWrap = true; _step.alignment = TextAnchor.UpperLeft; _step.fontSize = 14; _step.padding = new RectOffset(8, 8, 6, 6);
                _step.normal.textColor = new Color(0.55f, 1f, 0.6f);
            }
            GUI.color = Color.white; GUI.backgroundColor = Color.white;
            if (!MouseHere) Crosshair();
            _rect.height = Mathf.Min(760f, Screen.height - 40f);
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - 60f));
            _rect = GUI.Window(0x52574841, _rect, Draw, "Локації сюжету: " + (_loc != null ? _loc.Title : ""), _win);
        }

        // under the crosshair while the mouse is in the game: the piece aimed at, its layer, and the keys
        private GUIStyle _aim;
        private void Crosshair()
        {
            if (_aim == null)
            {
                _aim = new GUIStyle(GUI.skin.label); _aim.fontSize = 16; _aim.alignment = TextAnchor.UpperCenter; _aim.richText = true;
                _aim.normal.textColor = Color.white;
            }
            string what = _targetName.Length == 0 ? "Наведи приціл на деталь"
                        : _targetLayer == null ? _targetName + " — не частина «" + (_loc != null ? _loc.Title : "") + "»"
                        : "<b>" + _targetName + "</b> є тут:   " + Where(_targetLayer);
            string keys = "<color=#ffd34d>" + ModeText() + "</color>\n"
                        + KeysText() + "\n"
                        + "Num. — скинути деталь (є завжди) · Num* — повернути останнє · Shift+F10 — вікно\n"
                        + "<size=13>Num4/6 — нахил ліво/право · Num7/9 — від себе/до себе · Num+/− — вище/нижче · Num5 — рівно · Num/ — крок " + (_fine ? "дрібний" : "великий") + "</size>";
            Rect r = new Rect(Screen.width / 2f - 550f, Screen.height / 2f + 30f, 1100f, 140f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), System.Text.RegularExpressions.Regex.Replace(what + "\n" + keys, "</?color[^>]*>", ""), _aim); // (the shadow: no colour of its own)
            GUI.color = Color.white;
            GUI.Label(r, what + "\n" + keys, _aim);
        }

        // the next step for the place worked on, in plain words
        private string _countKey, _countText = "", _worldOf = "", _worldOfFor;
        private float _worldOfAt;

        private string Next(bool framed)
        {
            if (!framed) return BlueprintFrame.WorldOf(_loc.Id).Length > 0 || System.IO.File.Exists(Blueprint.FileOf(_loc.Id))
                ? "стань посеред уже збудованої «" + _loc.Title + "» і натисни «Знайти рамку в будівлі» (розділ 1)."
                : "стань посередині місця, де будуватимеш «" + _loc.Title + "», і натисни «Нова рамка тут» (розділ 1, двічі).";
            if (!System.IO.File.Exists(Blueprint.FileOf(_loc.Id))) return "будуй «" + _loc.Title + "» молотом. Що має бути лише в одному стані — познач (розділ 3). Тоді «Зберегти» (розділ 4).";
            if (!_loc.Shape()) return "креслення збережене. Скажи Claude вкласти його в мод — тоді «" + _loc.Title + "» з'являтиметься у світах. Змінюєш далі — просто зберігай знову.";
            return "«" + _loc.Title + "» уже в моді. Змінив будівлю — збережи знову (розділ 4) і скажи Claude оновити мод. Перевірити у світі — розділ 5.";
        }

        private void Draw(int id)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (_loc == null || !Locs.All.Contains(_loc)) _loc = (_loc != null ? Locs.Get(_loc.Id) : null) ?? (Locs.All.Count > 0 ? Locs.All[0] : null); // (read again elsewhere: the same place by its id)
            string zala = _loc.Id, title = _loc.Title;
            GUILayout.Label(MouseHere ? "Мишка: ВІКНО. Shift+F10 — віддати мишку грі (ходити, цілитись у деталь)." : "Мишка: ГРА. Наведи приціл на деталь. Shift+F10 — назад до вікна.", _hint);

            // the place
            GUILayout.Label("Локація, з якою працюєш (усе нижче — про неї):", _head);
            GUILayout.BeginHorizontal();
            foreach (Loc l in Locs.All)
                if (GUILayout.Button(l.Title, l == _loc ? _on : GUI.skin.button, GUILayout.Height(26f)) && l != _loc)
                { _loc = l; LocPreview.Set(l, 0); _confirmHere = false; _note = ""; if (_target != null) Look(); }
            if (GUILayout.Button(_adding ? "× Скасувати" : "+ Нова локація", GUILayout.Height(26f), GUILayout.Width(130f))) _adding = !_adding;
            GUILayout.EndHorizontal();
            if (_adding)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Креслення (латиницею):", GUILayout.Width(160f));
                _newId = GUILayout.TextField(_newId, 32, GUILayout.Width(140f));
                GUILayout.Label("Назва:", GUILayout.Width(50f));
                _newName = GUILayout.TextField(_newName, 48);
                GUILayout.EndHorizontal();
                GUILayout.Label("Нова локація стане поруч Зали (60–250 м, Луки), з одним станом. Правила — у " + Locs.AuthorFile + ".", _hint);
                if (GUILayout.Button("Додати", GUILayout.Height(26f)))
                {
                    _note = Locs.Add(_newId.Trim(), _newName);
                    Loc made = Locs.Get(_newId.Trim());
                    if (made != null) { _loc = made; _adding = false; _newId = _newName = ""; LocPreview.Set(made, 0); return; }
                }
            }
            // what to do next with this place, one step at a time
            Vector3 c; Quaternion rot;
            bool framed = BlueprintFrame.Get(zala, out c, out rot);
            if (Time.unscaledTime > _nextAt || _nextFor != _loc) { _nextText = Next(framed); _nextAt = Time.unscaledTime + 1f; _nextFor = _loc; }
            GUILayout.Label("Що далі: " + _nextText, _step);

            _scroll = GUILayout.BeginScrollView(_scroll);

            // the frame
            GUILayout.Label("1 · Рамка", _head);
            if (framed) GUILayout.Label("Закріплена в цьому світі: " + Mathf.RoundToInt(Vector3.Distance(p.transform.position, c)) + " м від тебе. Кожне збереження йде в ній, хоч би де ти стояв — деталі, шари й копії в світах гравців не зсуваються.", _hint);
            else
            {
                if (Time.time >= _worldOfAt || _worldOfFor != zala) { _worldOfAt = Time.time + 1f; _worldOfFor = zala; _worldOf = BlueprintFrame.WorldOf(zala); } // (a file read: once a second, not every pass)
                string w = _worldOf;
                GUILayout.Label(w.Length > 0 ? "Закріплена в іншому світі («" + w + "»). Тут — лише знайти її в будівлі." : "Ще не закріплена. Уже зберігав — стань посеред будівлі й натисни «Знайти»: мод сам знайде, як її було збережено. Нова будівля — стань посередині й «Нова рамка тут».", _warn);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Знайти рамку в будівлі", GUILayout.Height(28f))) { _note = Blueprint.Frame(p, zala, "find"); _confirmHere = false; if (_target != null) Look(); }
                if (GUILayout.Button(_confirmHere ? "Точно нова? Натисни ще раз" : "Нова рамка тут (лише нова будівля!)", GUILayout.Height(28f)))
                {
                    if (!_confirmHere) _confirmHere = true;
                    else { _confirmHere = false; _note = Blueprint.Frame(p, zala, "here"); if (_target != null) Look(); }
                }
                GUILayout.EndHorizontal();
            }

            // the look
            GUILayout.Space(10);
            GUILayout.Label("2 · Подивитись стан (лише в тебе)", _head);
            GUILayout.Label("Твоя будівля малюється так, як виглядатиме в цьому стані. У світі нічого не змінюється; «Як є» (чи закрите вікно) повертає все.", _hint);
            GUI.enabled = framed;
            GUILayout.BeginHorizontal();
            bool mine = LocPreview.Of == _loc;
            if (GUILayout.Button("Як є", !mine || LocPreview.State == 0 ? _on : GUI.skin.button, GUILayout.Height(28f))) _note = LocPreview.Set(_loc, 0);
            for (int i = 1; i <= _loc.Last; i++)
                if (GUILayout.Button(i + " · " + _loc.WordOf(i), mine && LocPreview.State == i ? _on : GUI.skin.button, GUILayout.Height(28f))) _note = LocPreview.Set(_loc, i, 0);
            GUILayout.EndHorizontal();
            // the sub-states of the state looked at
            int ls, lk = SubMode(out ls);
            if (ls > 0)
            {
                GUILayout.Label("Стан «" + _loc.WordOf(ls) + "» іде кроками: «початок» — поки нічого не сталося, далі субстани по черзі. Вибери крок — і нижче правиш лише його.", _hint);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(ls + ".0 · початок", lk == 0 ? _on : GUI.skin.button, GUILayout.Height(26f))) _note = LocPreview.Set(_loc, ls, 0);
                for (int k = 1; k <= _loc.SubsOf(ls); k++)
                    if (GUILayout.Button(ls + "." + k + " · " + _loc.SubWord(ls, k), lk == k ? _on : GUI.skin.button, GUILayout.Height(26f))) _note = LocPreview.Set(_loc, ls, k);
                if (GUILayout.Button(_addingSub ? "× Скасувати" : "+ Субстан", GUILayout.Height(26f), GUILayout.Width(110f))) _addingSub = !_addingSub;
                GUILayout.EndHorizontal();
                if (_addingSub)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Назва " + ls + "." + (_loc.SubsOf(ls) + 1) + ":", GUILayout.Width(90f));
                    _newSub = GUILayout.TextField(_newSub, 40);
                    if (GUILayout.Button("Додати", GUILayout.Width(90f)))
                    {
                        int had = _loc.SubsOf(ls);
                        _note = Locs.AddSub(_loc, ls, _newSub);
                        Loc again = Locs.Get(zala); // (the list read anew: the same place, its sub-states updated)
                        if (again != null && again.SubsOf(ls) > had)
                        {
                            _loc = again; _addingSub = false; _newSub = "";
                            _note += "\n" + LocPreview.Set(_loc, ls, _loc.SubsOf(ls));
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUI.enabled = true;

            // the layers
            GUILayout.Space(10);
            GUILayout.Label("3 · Деталь", _head);
            GUILayout.Label(ModeText() + ".", _step);
            GUILayout.Label("Shift+F10 (мишка в гру) → приціл на деталь → клавіша: " + KeysText() + ". Num. — скинути деталь (є завжди). Num* — повернути останнє. Або кнопками нижче.", _hint);
            if (_rich == null) { _rich = new GUIStyle(_hint); _rich.richText = true; _rich.normal.textColor = Color.white; }
            if (_targetName.Length == 0) GUILayout.Label("Деталь під прицілом: ще жодної.", _hint);
            else if (_targetLayer == null) GUILayout.Label("«" + _targetName + "» — не частина «" + title + "» (поза її рамкою, чи ближче до рамки іншої локації, чи далі " + Mathf.RoundToInt(LocParts.AuthorReach) + " м).", _warn);
            else GUILayout.Label("«" + _targetName + "» є тут:  " + Where(_targetLayer), _rich);
            GUI.enabled = _target != null && _targetLayer != null;
            GUILayout.BeginHorizontal();
            // how many pieces in each layer: counted over the whole blueprint's table, so only when the place, the step or the files change
            string countKey = _loc.Id + " " + ls + "." + lk + " " + LocParts.Version;
            bool countNow = countKey != _countKey;
            List<string> counts = new List<string>();
            if (lk > 0)
            {
                bool? had = _targetLayer != null ? LocState.MarkFor(_targetLayer, ls, lk) : null;
                if (GUILayout.Button("є лише тут", had == true ? _on : GUI.skin.button, GUILayout.Height(28f))) { _note = SetMark(_target, ls, lk, true); Look(); LocPreview.Again(); }
                bool fromHere = _targetLayer != null && LocState.FromOf(_targetLayer, ls) == lk;
                if (GUILayout.Button("з'являється звідси", fromHere ? _on : GUI.skin.button, GUILayout.Height(28f))) { _note = SetFrom(_target, ls, lk); Look(); LocPreview.Again(); }
                if (GUILayout.Button("прибрати тут", had == false ? _on : GUI.skin.button, GUILayout.Height(28f))) { _note = SetMark(_target, ls, lk, false); Look(); LocPreview.Again(); }
                if (GUILayout.Button("як у початку", GUILayout.Height(28f))) { _note = SetMark(_target, ls, lk, null); Look(); LocPreview.Again(); }
                string plus = LocState.Mark(true, ls, lk), minus = LocState.Mark(false, ls, lk);
                if (countNow) counts.Add("з'являються тут — " + LocParts.CountIf(zala, n => LocState.HasMark(n, plus)));
                if (countNow) counts.Add("зникають тут — " + LocParts.CountIf(zala, n => LocState.HasMark(n, minus)));
                string fromMark = LocState.From(ls, lk);
                if (countNow) counts.Add("з'являються звідси — " + LocParts.CountIf(zala, n => LocState.HasMark(n, fromMark)));
            }
            else
            {
                List<string> layers = new List<string>(), words = new List<string>();
                if (_loc.Last >= 3) { layers.AddRange(new[] { "ruin", "repaired", "lived", "fixed", "-" }); words.AddRange(new[] { "закинута", "з відремонт.", "з обжитої", "лише відремонт.", "без шару" }); }
                else
                {
                    for (int i = 1; i <= _loc.Last; i++) { layers.Add("only" + i); words.Add("лише " + i + " · " + _loc.WordOf(i)); }
                    for (int i = 2; i <= _loc.Last; i++) { if (i == _loc.Last) continue; layers.Add("from" + i); words.Add("з " + i + " і далі"); } // (from the last: only the last)
                    layers.Add("-"); words.Add("без шару");
                }
                string main = _targetLayer != null ? LocState.MainOf(_targetLayer) : null;
                for (int i = 0; i < layers.Count; i++)
                    if (GUILayout.Button(words[i], main == (layers[i] == "-" ? "" : layers[i]) ? _on : GUI.skin.button, GUILayout.Height(28f))) { _note = SetMain(_target, layers[i]); Look(); LocPreview.Again(); }
                for (int i = 0; i < layers.Count; i++)
                {
                    if (layers[i] == "-") continue;
                    string want = layers[i];
                    if (countNow) counts.Add(words[i] + " — " + LocParts.CountIf(zala, n => LocState.MainOf(n) == want));
                }
                if (countNow) counts.Add("лише в субстанах — " + LocParts.CountIf(zala, LocState.SubOnly));
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = _target != null && !string.IsNullOrEmpty(_targetLayer);
            if (GUILayout.Button("Скинути деталь повністю (Num.)", GUILayout.Height(26f))) { _note = Reset(_target); Look(); LocPreview.Again(); }
            GUI.enabled = _undo.Count > 0;
            if (GUILayout.Button("↶ Повернути (Num*) · кроків: " + _undo.Count, GUILayout.Height(26f))) { _note = Undo(); if (_target != null) Look(); LocPreview.Again(); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (countNow) { _countKey = countKey; _countText = "У шарах: " + string.Join(", ", counts.ToArray()) + "."; }
            GUILayout.Label(_countText, _hint);

            // the save
            GUILayout.Space(10);
            GUILayout.Label("4 · Зберегти «" + title + "»", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Радіус: " + Mathf.RoundToInt(_radius) + " м", GUILayout.Width(110f));
            _radius = Mathf.Round(GUILayout.HorizontalSlider(_radius, 10f, 60f, GUILayout.Width(280f)));
            GUILayout.EndHorizontal();
            GUILayout.Label(framed ? "Коло — від середини рамки. Стара версія лишиться в " + zala + ".txt.bak." : "Рамки ще нема: збереження спершу знайде її в будівлі (або закріпить, якщо креслення нове).", _hint);
            if (GUILayout.Button("Зберегти «" + zala + "»", GUILayout.Height(30f))) _note = Blueprint.Save(p, zala, _radius);

            // the real one
            GUILayout.Space(10);
            GUILayout.Label("5 · «" + title + "», яку мод сам поставив у цьому світі", _head);
            if (!_loc.Built) GUILayout.Label(_loc.Shape() ? "У цьому світі її ще нема (з'явиться, коли хтось підійде; raven blueprint hall — де)." : "У цій збірці ще нема її креслення: збережи, і я вкладу його в мод.", _hint);
            else
            {
                int next, nextSub; bool outside, pending = LocState.Pending(_loc, out next, out nextSub, out outside);
                int now = LocState.StateOf(_loc), nowSub = LocState.SubOf(_loc);
                GUILayout.Label("Зараз: " + _loc.WordOf(now, nowSub) + (pending ? " · чекає за кадром: " + _loc.WordOf(next, nextSub) + (outside ? " (ззовні)" : " (всередині)") + ", двері зачинені" : ""), _hint);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Міняти:", GUILayout.Width(60f));
                if (GUILayout.Button("одразу", !_behind ? _on : GUI.skin.button, GUILayout.Width(90f))) _behind = false;
                if (GUILayout.Button("за кадром", _behind ? _on : GUI.skin.button, GUILayout.Width(110f))) _behind = true;
                GUILayout.EndHorizontal();
                for (int s = LocState.Abandoned; s <= _loc.Last; s++)
                {
                    GUILayout.BeginHorizontal();
                    for (int k = 0; k <= _loc.SubsOf(s); k++)
                    {
                        string label = k == 0 ? s + " · " + _loc.WordOf(s) : s + "." + k + " · " + _loc.SubWord(s, k);
                        if (!GUILayout.Button(label, now == s && nowSub == k ? _on : GUI.skin.button, GUILayout.Height(24f))) continue;
                        string arg = k > 0 ? s + "." + k : s.ToString();
                        _note = _behind ? LocState.Cmd(_loc, new[] { "blueprint", "state", arg, _loc.Last >= 3 && s >= LocState.Repaired && now < LocState.Repaired ? "out" : "in" })
                                        : LocState.Cmd(_loc, new[] { "blueprint", "state", arg });
                    }
                    GUILayout.EndHorizontal();
                }
                GUI.enabled = pending;
                if (GUILayout.Button("Скасувати зміну, що чекає")) _note = LocState.Cmd(_loc, new[] { "blueprint", "state", "cancel" });
                GUI.enabled = true;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Вогнище: " + LocHearth.WordOf(LocHearth.LevelOf(_loc)) + " (далі)")) _note = LocState.Cmd(_loc, new[] { "blueprint", "state", "fire" });
                if (GUILayout.Button(LocState.GateOpenOf(_loc) ? "Великі двері: відімкнені (замкнути)" : "Великі двері: замкнені (відімкнути)")) _note = LocState.Cmd(_loc, new[] { "blueprint", "state", "gate" });
                GUILayout.EndHorizontal();
                // built again from the blueprint in this build (a world keeps the one it was built from)
                if (GUILayout.Button(_confirmRebuild ? "Точно? Її деталі зникнуть і постануть з нового креслення — натисни ще раз" : "Перебудувати з креслення цієї збірки", GUILayout.Height(26f)))
                {
                    if (!_confirmRebuild) _confirmRebuild = true;
                    else { _confirmRebuild = false; _loc.AskRebuild(); _note = _loc.Title + ": прошу сервер перебудувати…"; }
                }
            }
            GUILayout.EndScrollView();

            if (_note.Length > 0) GUILayout.Label(_note, _hint);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрити", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
        }
    }
}
#endif

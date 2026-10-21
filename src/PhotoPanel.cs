#if RAVEN_ITEM
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // The test window's tab «Фото»: a picture for each model made in the Workshop, taken by hand.
    // Light on purpose: the list is only the files' names (their heads read, as the Workshop does), and only the model
    // picked is put together, in its own small studio (IconPhotos / PhotoStudio), gone when the tab is left.
    // The big picture follows every move; beside it the icon itself (the clean shot, as the game will show it) at the
    // inventory's sizes. «Зняти» keeps it in config/RavenWatch/photos/<id>.png and puts it in the game at once.
    internal class PhotoPanel : MonoBehaviour
    {
        private GUIStyle _on, _small, _head, _rowL;
        private Texture2D _slot, _light, _frame;

        private List<string> _files = new List<string>();
        private readonly Dictionary<string, string> _keys = new Dictionary<string, string>(); // file → id
        private int _tab;
        private Vector2 _listScroll, _setScroll;
        private string _file;
        private WsModel _m;
        private string _key = "";
        private PhotoSet _set = new PhotoSet(), _saved = new PhotoSet();

        private PhotoStudio _studio;
        private WsView _view;
        private int _measureIn;          // frames until the model is measured (its parts made this frame)
        private Texture2D _result;       // the icon as it will be
        private int _solid;
        private bool _changed;
        private float _changedAt;
        private int _bg;
        private string _note = "";
        private Vector2 _downAt;

        private static readonly string[] Tabs = { "Усі", "Для ворона", "Будується", "Предмети", "Інше" };
        private static readonly string[] BgNames = { "темне", "світле", "зелене" };
        private static readonly Color[] Bgs = { new Color(0.17f, 0.15f, 0.13f, 1f), new Color(0.78f, 0.76f, 0.72f, 1f), new Color(0.2f, 0.45f, 0.25f, 1f) };

        // ---------------------------------------------------------------- the tab shown / left

        internal void Opened()
        {
            enabled = true;
            ReadList();
            if (_file != null && File.Exists(_file)) Pick(_file);
            else _note = "Вибери модель зліва. Фото лягають у " + IconPhotos.Dir;
        }

        internal void Closed()
        {
            enabled = false;
            Drop();
        }

        private void OnDestroy() { Drop(); }

        // the studio and the model go: nothing is kept while the tab is not shown
        private void Drop()
        {
            if (_view != null) _view.Clear();
            _view = null;
            if (_studio != null) _studio.Dispose();
            _studio = null;
            if (_result != null) Destroy(_result);
            _result = null;
        }

        private void ReadList()
        {
            _files = WsFiles.List();
            _keys.Clear();
        }

        private string KeyOfFile(string f)
        {
            string k;
            if (_keys.TryGetValue(f, out k)) return k;
            WsModel m = WsFiles.Load(f);
            k = m != null ? IconPhotos.KeyOf(m) : "";
            _keys[f] = k;
            return k;
        }

        private static bool InTab(int tab, string use)
        {
            switch (tab)
            {
                case 1: return use == RavenGear.Use;
                case 2: return use == Perches.Use || use == Perches.TableUse;
                case 3: return use == WsItems.Use;
                case 4: return use != RavenGear.Use && use != Perches.Use && use != Perches.TableUse && use != WsItems.Use;
                default: return true;
            }
        }

        // ---------------------------------------------------------------- the model picked

        private void Pick(string file)
        {
            WsModel m = WsFiles.Load(file);
            if (m == null) { _note = "Не вдалося прочитати " + Path.GetFileName(file); return; }
            if (_studio == null)
            {
                int layer = RavenFx.FreeLayer();
                if (layer < 0) { _note = "Немає вільного шару для фото."; return; }
                _studio = new PhotoStudio("RavenWatch_PhotoStudio", new Vector3(0f, 9800f, 0f), layer);
                _view = new WsView(_studio.Subject, layer, false); // no raven seats: only the thing itself
            }
            _view.Clear();
            _file = file;
            _m = m;
            _key = IconPhotos.KeyOf(m);
            _keys[file] = _key;
            _view.Sync(m);
            _studio.Prepare();
            _measureIn = 2;
            _saved = IconPhotos.SettingsOf(_key);
            _set = _saved.Clone();
            Changed();
            _note = m.Name + " → photos/" + _key + ".png" + (IconPhotos.HasOwn(_key) ? " (є фото, знято цими налаштуваннями)" : " (фото ще нема)");
        }

        private void Changed() { _changed = true; _changedAt = Time.unscaledTime; }

        private void LateUpdate()
        {
            if (_studio == null || _m == null) return;
            if (_measureIn > 0 && --_measureIn == 0)
            {
                _studio.Prepare();
                if (!_studio.Measure()) _note = _m.Name + ": нічого не видно (деталей нема в грі?)";
                Changed();
            }
            // the clean icon a few times a second while something changes (two shots and a read: not every frame)
            if (_changed && _measureIn == 0 && Time.unscaledTime - _changedAt > 0.15f)
            {
                _changed = false;
                Color32[] px = _studio.Clean(_set, out _solid);
                if (_result != null) Destroy(_result);
                _result = IconPhotos.Finish(px, _set.Trim);
            }
            _studio.Aim(_set);
            _studio.Shoot(_set, Bgs[_bg]);
        }

        // ---------------------------------------------------------------- drawing

        private void Styles()
        {
            if (_on != null && _slot != null) return;
            _on = new GUIStyle(GUI.skin.button); _on.fontStyle = FontStyle.Bold; _on.normal.textColor = new Color(1f, 0.83f, 0.3f);
            _small = new GUIStyle(GUI.skin.label); _small.fontSize = 11; _small.wordWrap = true; _small.richText = true;
            _head = new GUIStyle(GUI.skin.label); _head.fontStyle = FontStyle.Bold; _head.normal.textColor = new Color(0.95f, 0.82f, 0.55f); _head.richText = true;
            _rowL = new GUIStyle(GUI.skin.button); _rowL.alignment = TextAnchor.MiddleLeft; _rowL.richText = true; _rowL.fontSize = 12; _rowL.wordWrap = false; _rowL.clipping = TextClipping.Clip;
            _slot = Plugin.Solid(new Color(0.2f, 0.17f, 0.14f, 1f));      // an inventory cell
            _light = Plugin.Solid(new Color(0.85f, 0.83f, 0.78f, 1f));
            _frame = Plugin.Solid(new Color(0.75f, 0.6f, 0.3f, 1f));
        }

        internal void Body(float h)
        {
            Styles();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Фото моделей</b>", _head, GUILayout.Width(150f));
            GUILayout.Label("<color=#C8C8C8>" + _note + "</color>", _small, GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            h -= 34f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(280f), GUILayout.Height(h)); List(h); GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(500f), GUILayout.Height(h)); Picture(Mathf.Clamp(h - 110f, 240f, 490f)); GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Height(h));
            _setScroll = GUILayout.BeginScrollView(_setScroll);
            Settings();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // ---- left: my models

        private void List(float h)
        {
            GUILayout.Label("Мої моделі (Майстерня)", _head);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (GUILayout.Button(Tabs[i], i == _tab ? _on : GUI.skin.button, GUILayout.ExpandWidth(false))) _tab = i;
            GUILayout.EndHorizontal();
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(Mathf.Max(100f, h - 90f)));
            foreach (string f in _files)
            {
                string use = WsFiles.UseOf(f);
                if (!InTab(_tab, use)) continue;
                bool has = IconPhotos.HasOwn(KeyOfFile(f));
                string label = (has ? "<color=#7CD67C>● </color>" : "<color=#777777>○ </color>") + Path.GetFileNameWithoutExtension(f)
                    + (use.Length > 0 ? "  <color=#9A9A9A>" + use + "</color>" : "");
                if (GUILayout.Button(label, f == _file ? _on : _rowL, GUILayout.Height(24f)) && f != _file) Pick(f);
            }
            GUILayout.EndScrollView();
            GUILayout.Label("<color=#9A9A9A>● є своє фото, ○ знімок робиться сам.</color>", _small);
            if (GUILayout.Button("Оновити список")) ReadList();
        }

        // ---- middle: the live picture

        private void Picture(float size)
        {
            Rect pic = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (_studio != null && _m != null) GUI.DrawTexture(pic, _studio.Rt, ScaleMode.ScaleToFit, false);
            else GUI.Box(pic, "Вибери модель зліва");
            Edge(pic, 1f);
            Event e = Event.current;
            if (_m != null)
            {
                if (e.type == EventType.MouseDown && pic.Contains(e.mousePosition)) { _downAt = e.mousePosition; e.Use(); }
                if (e.type == EventType.MouseDrag && pic.Contains(_downAt))
                {
                    if (e.button == 1 || e.shift)
                    {
                        float k = 2f / pic.width;
                        _set.PanX += e.delta.x * k; _set.PanY -= e.delta.y * k;
                    }
                    else
                    {
                        _set.Yaw = Mathf.Repeat(_set.Yaw + e.delta.x * 0.6f, 360f);
                        _set.Pitch = Mathf.Clamp(_set.Pitch + e.delta.y * 0.4f, -89f, 89f);
                    }
                    Changed(); e.Use();
                }
                if (e.type == EventType.MouseUp) _downAt = new Vector2(-1f, -1f);
                if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { _set.Zoom = Mathf.Clamp(_set.Zoom * (1f - e.delta.y * 0.04f), 0.2f, 6f); Changed(); e.Use(); }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3/4 зліва")) View(200f, 18f);
            if (GUILayout.Button("3/4 справа")) View(160f, 18f);
            if (GUILayout.Button("Спереду")) View(180f, 5f);
            if (GUILayout.Button("Збоку")) View(270f, 5f);
            if (GUILayout.Button("Згори")) View(_set.Yaw, 80f);
            if (GUILayout.Button("Вписати")) { _set.Zoom = 1f; _set.PanX = _set.PanY = 0f; Changed(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тло (лише тут, не на фото):", GUILayout.ExpandWidth(false));
            for (int i = 0; i < BgNames.Length; i++) if (GUILayout.Button(BgNames[i], i == _bg ? _on : GUI.skin.button)) _bg = i;
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Тягни — крутити, правою/Shift — зсунути, коліщатко — ближче.</color>", _small);
        }

        private void View(float yaw, float pitch) { _set.Yaw = yaw; _set.Pitch = pitch; _set.Roll = 0f; Changed(); }

        private void Edge(Rect r, float w)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), _frame); GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), _frame);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), _frame); GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), _frame);
        }

        // ---- right: the camera, the light, the icon

        private void Settings()
        {
            if (_m == null) { GUILayout.Label("Тут будуть налаштування камери і саме фото.", _small); return; }
            GUILayout.Label("Камера", _head);
            _set.Yaw = Slider("Поворот", _set.Yaw, 0f, 360f, "0");
            _set.Pitch = Slider("Нахил", _set.Pitch, -89f, 89f, "0");
            _set.Roll = Slider("Крен", _set.Roll, -180f, 180f, "0");
            _set.Zoom = Slider("Ближче", _set.Zoom, 0.2f, 6f, "0.00");
            _set.PanX = Slider("Зсув ↔", _set.PanX, -1.5f, 1.5f, "0.00");
            _set.PanY = Slider("Зсув ↕", _set.PanY, -1.5f, 1.5f, "0.00");
            _set.Fov = Slider("Перспект.", _set.Fov, 0f, 60f, "0");
            GUILayout.Label("<color=#9A9A9A>Перспектива 0 — рівна, без перспективи (зручно для довгих жердин).</color>", _small);
            GUILayout.Label("Світло", _head);
            _set.Light = Slider("Сила", _set.Light, 0.2f, 2.5f, "0.00");
            _set.LightTurn = Slider("Звідки", _set.LightTurn, -180f, 180f, "0");
            _set.Ambient = Slider("Тіні м'якші", _set.Ambient, 0f, 1f, "0.00");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_set.Trim ? "Поля: підрізати" : "Поля: як у кадрі", _set.Trim ? _on : GUI.skin.button)) { _set.Trim = !_set.Trim; Changed(); }
            GUILayout.Label("<color=#9A9A9A>" + (_set.Trim ? "модель заповнює іконку сама" : "іконка = рамка картинки") + "</color>", _small);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Так буде в грі", _head);
            GUILayout.BeginHorizontal();
            Icon(_result, 128f, _slot); Icon(_result, 64f, _slot); Icon(_result, 40f, _slot); Icon(_result, 64f, _light);
            GUILayout.EndHorizontal();
            if (_result != null && _solid < PhotoStudio.Size * PhotoStudio.Size / 2000) GUILayout.Label("<color=#FF9A6A>На фото майже нічого не видно.</color>", _small);
            Sprite now = IconPhotos.Get(_key);
            if (now != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Зараз у грі:", GUILayout.Width(90f));
                Icon(now.texture, 64f, _slot);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Зняти й зберегти", _on, GUILayout.Height(34f))) Take();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Як було")) { _set = _saved.Clone(); Changed(); }
            if (GUILayout.Button("Стандарт")) { _set = new PhotoSet(); Changed(); }
            GUI.enabled = IconPhotos.HasOwn(_key);
            if (GUILayout.Button("Прибрати фото")) { IconPhotos.Remove(_key); _saved = new PhotoSet(); _note = _m.Name + ": своє фото прибрано, знімок знову робиться сам."; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void Take()
        {
            if (_studio == null || _m == null) return;
            int solid;
            Color32[] px = _studio.Clean(_set, out solid);
            if (solid < PhotoStudio.Size * PhotoStudio.Size / 2000) { _note = _m.Name + ": на фото нічого не видно — не збережено."; return; }
            Texture2D icon = IconPhotos.Finish(px, _set.Trim);
            bool ok = IconPhotos.Save(_key, icon, _set, _m.Name);
            Destroy(icon);
            if (ok) { _saved = _set.Clone(); _note = _m.Name + ": знято → photos/" + _key + ".png (уже в грі)"; }
            else _note = _m.Name + ": не вдалося зберегти (дивись лог).";
        }

        private void Icon(Texture t, float size, Texture2D back)
        {
            Rect r = GUILayoutUtility.GetRect(size + 8f, size + 8f, GUILayout.Width(size + 8f), GUILayout.Height(size + 8f));
            GUI.DrawTexture(r, back);
            if (t != null) GUI.DrawTexture(new Rect(r.x + 4f, r.y + 4f, size, size), t, ScaleMode.ScaleToFit, true);
        }

        private float Slider(string name, float v, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(86f));
            float n = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(n.ToString(fmt), GUILayout.Width(42f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(n - v) > 0.0001f) Changed();
            return n;
        }
    }
}
#endif

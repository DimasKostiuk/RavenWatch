#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Креслення»: blueprints and places — and what is drawn over the world for them.
    internal partial class StoryPanel
    {
        // ---------------------------------------------------------------- «Креслення»
        private void Build()
        {
            Player p = Player.m_localPlayer;
            GUILayout.BeginHorizontal();

            // left: the blueprints in the folder
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("Мої креслення", _head);
            List<string> names = _names;
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(300f));
            if (names.Count == 0) GUILayout.Label("Ще жодного. Збережи будівлю праворуч (крок 1).", _hint);
            foreach (string n in names)
                if (GUILayout.Button((n == Blueprint.GhostName ? "[привид] " : "") + n, n == _picked ? _sel : GUI.skin.button)) { _picked = n; _info = Blueprint.Describe(n); _confirmDelete = ""; _confirmPlace = ""; }
            GUILayout.EndScrollView();
            if (GUILayout.Button("+ Нове креслення")) { _saveName = ""; _confirmSave = ""; Note = "Нове креслення: впиши назву в кроці 1 (латиницею), стань у центр будівлі, подивись на вхід і натисни «Зберегти»."; }
            if (GUILayout.Button("Оновити список")) Refresh();
            if (GUILayout.Button("Локації сюжету: стани, шари, нова локація… (F10)")) Note = LocPanel.Toggle();
            GUILayout.Space(10);
            GUILayout.Label("Як це працює", _head);
            GUILayout.Label("1. Будуєш у грі (devcommands, nocost).\n2. Зберігаєш: усе в колі стає кресленням — лише форма, без вмісту скринь.\n3. Обираєш креслення зліва → привид → зсуваєш → ставиш.\n4. Місця (мітки) в колі зберігаються разом із будівлею й переїжджають з нею.", _hint);
            GUILayout.EndVertical();

            GUILayout.Space(12);
            _rightScroll = GUILayout.BeginScrollView(_rightScroll);
            GUILayout.BeginVertical();

            // step 1: save
            GUILayout.Label("Крок 1 · Зберегти будівлю в креслення", _head);
            GUILayout.Label("Стань у центр будівлі на підлогу й подивись на вхід — це буде центр і «перед» креслення (на землі видно кільце й золоту стрілку).", _hint);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва:", GUILayout.Width(70f));
            _saveName = GUILayout.TextField(_saveName, 40, GUILayout.Width(200f));
            GUILayout.Label(Blueprint.GoodName(_saveName) ? (names.Contains(_saveName) ? "є таке — старе піде в .bak" : "") : "лише латиниця, цифри, _ -", Blueprint.GoodName(_saveName) ? _hint : _warn);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Радіус: " + Mathf.RoundToInt(_radius) + " м", GUILayout.Width(110f));
            _radius = Mathf.Round(GUILayout.HorizontalSlider(_radius, 5f, 80f, GUILayout.Width(300f)));
            _ring = GUILayout.Toggle(_ring, " кільце на землі");
            GUILayout.EndHorizontal();
            GUILayout.Label("У колі зараз: " + _countPieces + " елементів, " + _countKinds + " видів" + (_countPieces > 1500 ? " — багато, перевіримо, чи не просідає гра" : ""));
            if (_countOdd.Count > 0) GUILayout.Label("Схоже не на ванільні (з інших модів?): " + string.Join(", ", _countOdd.ToArray()), _warn);
            GUI.enabled = Blueprint.GoodName(_saveName) && _countPieces > 0;
            bool overwrite = names.Contains(_saveName);
            if (_confirmSave.Length > 0 && _confirmSave != _saveName) _confirmSave = "";
            string saveText = !overwrite ? "Зберегти нове «" + _saveName + "»" : _confirmSave == _saveName ? "Точно перезаписати «" + _saveName + "»? (старе піде в .bak)" : "Перезаписати «" + _saveName + "»";
            if (GUILayout.Button(saveText, GUILayout.Height(30f)))
            {
                if (overwrite && _confirmSave != _saveName) _confirmSave = _saveName; // (an existing one: a second press)
                else { Note = Blueprint.Save(p, _saveName, _radius); _picked = _saveName; _confirmSave = ""; Refresh(); }
            }
            GUI.enabled = true;

            GUILayout.Space(14);
            // step 2: the picked blueprint
            GUILayout.Label("Крок 2 · Обране креслення" + (_picked.Length > 0 ? ": " + _picked : ""), _head);
            if (_info == null) GUILayout.Label("Обери креслення зліва.", _hint);
            else
            {
                GUILayout.Label(_info.Pieces + " елементів, " + _info.Kinds + " видів · розмір ≈ " + Mathf.RoundToInt(_info.Size.x) + " × " + Mathf.RoundToInt(_info.Size.z) + " м, висота " + Mathf.RoundToInt(_info.Size.y) + " м · міток: " + _info.Marks.Count);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Blueprint.GhostName == _picked ? "Привид — до мене" : "Показати привида тут", GUILayout.Height(28f))) Note = Blueprint.Preview(_picked, p.transform.position, Blueprint.Facing(p));
                GUI.enabled = Blueprint.Ghost != null;
                if (GUILayout.Button("Сховати привида", GUILayout.Height(28f))) Note = Blueprint.GhostOff();
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (Blueprint.Ghost != null && Blueprint.GhostName == _picked)
                {
                    Transform g = Blueprint.Ghost.transform;
                    GUILayout.Label("Зсунути привида (крок " + (_step >= 1f ? "1 м" : "0.1 м") + ", поворот " + (_step >= 1f ? "15°" : "1°") + "):", _hint);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(_step >= 1f ? "Крок: 1 м" : "Крок: 0.1 м", GUILayout.Width(110f))) _step = _step >= 1f ? 0.1f : 1f;
                    Vector3 fwd = g.forward, right = g.right;
                    if (GUILayout.Button("◀ ліворуч")) g.position -= right * _step;
                    if (GUILayout.Button("праворуч ▶")) g.position += right * _step;
                    if (GUILayout.Button("▲ вперед")) g.position += fwd * _step;
                    if (GUILayout.Button("▼ назад")) g.position -= fwd * _step;
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("⤒ вище")) g.position += Vector3.up * _step;
                    if (GUILayout.Button("⤓ нижче")) g.position -= Vector3.up * _step;
                    if (GUILayout.Button("⟲ повернути")) g.Rotate(0f, -(_step >= 1f ? 15f : 1f), 0f, Space.World);
                    if (GUILayout.Button("повернути ⟳")) g.Rotate(0f, _step >= 1f ? 15f : 1f, 0f, Space.World);
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
                bool atGhost = Blueprint.Ghost != null && Blueprint.GhostName == _picked;
                GUI.enabled = !Blueprint.Placing;
                string placeLabel = _confirmPlace == _picked ? "Точно поставити? Натисни ще раз" : atGhost ? "Поставити по-справжньому там, де привид" : "Поставити по-справжньому тут (де стоїш, дивлячись уперед)";
                if (GUILayout.Button(placeLabel, GUILayout.Height(30f)))
                {
                    if (_confirmPlace != _picked) _confirmPlace = _picked;
                    else
                    {
                        _confirmPlace = "";
                        Note = atGhost ? Blueprint.Place(_picked, Blueprint.Ghost.transform.position, Blueprint.Ghost.transform.rotation)
                                       : Blueprint.Place(_picked, p.transform.position, Blueprint.Facing(p));
                    }
                }
                GUI.enabled = Blueprint.LastCount > 0 && !Blueprint.Placing;
                if (GUILayout.Button("Прибрати останнє поставлене" + (Blueprint.LastCount > 0 ? " («" + Blueprint.LastName + "», " + Blueprint.LastCount + ")" : " (до виходу з гри)"))) Note = Blueprint.Undo();
                GUI.enabled = true;
                if (GUILayout.Button(_confirmDelete == _picked ? "Точно видалити файл «" + _picked + "»? Натисни ще раз" : "Видалити креслення"))
                {
                    if (_confirmDelete != _picked) _confirmDelete = _picked;
                    else { _confirmDelete = ""; Note = Blueprint.Delete(_picked); Refresh(); }
                }
                GUILayout.Label("Поставлена так будівля не зношується й не падає без опори. Землю під нею мод поки не рівняє — бери рівне місце або глибокий фундамент.", _hint);
            }

            GUILayout.Space(14);
            // step 3: the places
            GUILayout.Label("Крок 3 · Місця (мітки сюжету)", _head);
            GUILayout.Label("Мітка — іменована точка у світі: де вогнище, де сяде актор, звідки дивиться камера. Ставиться там, де стоїш, і дивиться туди, куди дивишся. Мітки в колі креслення зберігаються з ним.", _hint);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва:", GUILayout.Width(70f));
            _placeName = GUILayout.TextField(_placeName, 48, GUILayout.Width(200f));
            GUI.enabled = StoryPlaces.GoodName(_placeName);
            if (GUILayout.Button(StoryPlaces.Find(_placeName) != null ? "Перенести сюди" : "Поставити мітку тут", GUILayout.Width(180f))) Note = StoryPlaces.Set(_placeName, p.transform.position, Blueprint.Facing(p).eulerAngles.y);
            GUI.enabled = true;
            _showPlaces = GUILayout.Toggle(_showPlaces, " видно у світі");
            GUILayout.EndHorizontal();
            if (_placeName.Length > 0 && !StoryPlaces.GoodName(_placeName)) GUILayout.Label("лише латиниця, цифри, _ - .", _warn);
            List<StoryPlaces.Place> places = StoryPlaces.All;
            if (places.Count == 0) GUILayout.Label("Міток ще нема.", _hint);
            _placeScroll = GUILayout.BeginScrollView(_placeScroll, GUILayout.Height(Mathf.Min(200f, 30f + places.Count * 28f)));
            StoryPlaces.Place gone = null;
            foreach (StoryPlaces.Place pl in places)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(pl.Name, GUILayout.Width(220f));
                GUILayout.Label(Mathf.RoundToInt(Vector3.Distance(p.transform.position, pl.Pos)) + " м", GUILayout.Width(70f));
                if (GUILayout.Button("Туди", GUILayout.Width(70f))) ErrandMap.Teleport(p, pl.Pos);
                if (GUILayout.Button("Назва → поле", GUILayout.Width(110f))) _placeName = pl.Name;
                if (GUILayout.Button("Видалити", GUILayout.Width(90f))) gone = pl;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (gone != null) Note = StoryPlaces.Remove(gone.Name);

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- drawn over the world: the circle, the front, the places
        private void WorldMarks()
        {
            Player p = Player.m_localPlayer;
            Camera cam = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
            if (p == null || cam == null || Event.current.type != EventType.Repaint) return;
            if (_tab == BuildTab && _ring)
            {
                Vector3 c = p.transform.position;
                if (float.IsNaN(_ringFor.x) || (c - _ringFor).sqrMagnitude > 0.25f || _ringRadius != _radius) RingPoints(c);
                foreach (Vector3 w in _ringPts) Dot(cam, w, _dot, 5f);
                Vector3 f = Blueprint.Facing(p) * Vector3.forward;
                for (float d = 0.5f; d <= 4f; d += 0.5f) Dot(cam, c + f * d + Vector3.up * 0.1f, _dotGold, d >= 3.5f ? 10f : 6f);
            }
            if (_showPlaces)
                foreach (StoryPlaces.Place pl in StoryPlaces.All)
                {
                    if (Vector3.Distance(cam.transform.position, pl.Pos) > 150f) continue;
                    Vector3 sp = cam.WorldToScreenPoint(pl.Pos + Vector3.up * 1.6f);
                    if (sp.z <= 0f) continue;
                    Rect r = new Rect(sp.x - 90f, Screen.height - sp.y - 12f, 180f, 24f);
                    GUI.color = new Color(1f, 0.83f, 0.3f);
                    GUI.Label(r, "◆ " + pl.Name, _head);
                    GUI.color = Color.white;
                    Dot(cam, pl.Pos + Vector3.up * 0.1f, _dotGold, 9f);
                    Vector3 look = Quaternion.Euler(0f, pl.Yaw, 0f) * Vector3.forward;
                    Dot(cam, pl.Pos + look * 1f + Vector3.up * 0.1f, _dotGold, 5f);
                }
        }

        private void RingPoints(Vector3 c)
        {
            _ringFor = c; _ringRadius = _radius;
            _ringPts.Clear();
            int n = Mathf.Clamp(Mathf.RoundToInt(_radius * 3f), 48, 240);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 w = new Vector3(c.x + Mathf.Cos(a) * _radius, c.y, c.z + Mathf.Sin(a) * _radius);
                float h;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(new Vector3(w.x, c.y + 60f, w.z), out h)) w.y = h + 0.15f;
                _ringPts.Add(w);
            }
        }

        private static void Dot(Camera cam, Vector3 w, Texture2D t, float size)
        {
            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z <= 0f) return;
            GUI.DrawTexture(new Rect(sp.x - size * 0.5f, Screen.height - sp.y - size * 0.5f, size, size), t);
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The «Персонаж» tab of the test window (DevPanel; console raven panim): our own animations for the player, made
    // and tried live on your own character. Two pages:
    //   Кліпи — left: the clips (new, copy, rename, delete), the chosen clip's length, loop, pivot, base (the game's
    //           live animation or its pose frozen), what drives it (the clock or a motion value), its sounds, playing
    //           it on you (or holding one moment) and its keys; a picture of you from the front, side, back or above.
    //           Right: the bones by part, the chosen bone's three turns at the chosen key, mirrored if you like.
    //   Стани — the rules that pick the clips by themselves from your motion, with your motion values live.
    // Every change shows at once; «Зберегти» writes config/RavenWatch/player_anims. For the author only: nothing of it
    // is in the README or the changelog.
    internal sealed class PlayerAnimPanel
    {
        private Vector2 _left, _right, _rules;
        private string _sel, _bone = "LeftArm", _note = "", _newName = "", _rename = "", _sound = "sfx_raven_flap";
        private int _key = -1, _group = 3, _page;
        private bool _play, _dirty, _mirror = false, _askDelete, _askRevert, _rulesDirty;
        private float _speed = 1f, _scrub, _volume = 1f;
        private Vector3 _copied;
        private bool _hasCopy;
        // how a turn maps to the other side (the game's skeleton mirrors left and right): x stays, y and z flip
        private bool _flipX, _flipY = true, _flipZ = true;

        // the picture of you
        private bool _camOn = true;
        private int _view;
        private float _camDist = 3.4f;
        private Camera _cam;
        private RenderTexture _rt;
        private static readonly string[] Views = { "Спереду", "Збоку", "Ззаду", "Зліва", "Згори" };

        private PAnimClip Sel { get { return PlayerAnims.Find(_sel); } }
        private static Player Me { get { return Player.m_localPlayer; } }

        internal void Opened() { if (PlayerAnims.Clips.Count == 0) PlayerAnims.Load(); }

        // leaving the tab: the preview stops, the picture goes
        internal void Closed()
        {
            _play = false;
            if (Me != null) PlayerAnims.Stop(Me, PlayerAnims.PreviewSlot, 0.3f);
            FreeCam();
        }

        internal void Body(float h)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_page == 0, " Кліпи", "Button", GUILayout.Width(120f))) _page = 0;
            if (GUILayout.Toggle(_page == 1, " Стани (правила)", "Button", GUILayout.Width(160f))) _page = 1;
            GUILayout.EndHorizontal();
            if (_page == 1) { Rules(h - 30f); if (_note.Length > 0) GUILayout.Label(_note); return; }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(500f));
            _left = GUILayout.BeginScrollView(_left, GUILayout.Height(h - 30f));
            Clips();
            PAnimClip c = Sel;
            if (c != null) { Clip(c); Preview(c); Keys(c); Events(c); }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            _right = GUILayout.BeginScrollView(_right, GUILayout.Height(h - 30f));
            Picture();
            if (c != null) Bones(c);
            else GUILayout.Label("Вибери або створи кліп ліворуч.");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_note.Length > 0) GUILayout.Label(_note);
        }

        // ---------------------------------------------------------------- the clips

        private void Clips()
        {
            GUILayout.Label("<b>Кліпи</b>  <color=#9a9a9a>(анімації персонажа поверх анімації гри)</color>");
            List<string> names = new List<string>(PlayerAnims.Clips.Keys);
            names.Sort();
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (string n in names)
            {
                PAnimClip c = PlayerAnims.Find(n);
                string label = n + (c.FromNet ? " ⇣" : c.FromFile ? "" : " ·");
                if (GUILayout.Toggle(_sel == n, label, "Button", GUILayout.Width(155f)) && _sel != n) Select(n);
                if (++col % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9a9a9a>· — вбудований, ще не у файлі · ⇣ — прийшов від іншого гравця</color>");
            GUILayout.BeginHorizontal();
            _newName = GUILayout.TextField(_newName, GUILayout.Width(170f));
            GUI.enabled = Valid(_newName) && PlayerAnims.Find(_newName) == null;
            if (GUILayout.Button("Новий", GUILayout.Width(70f)))
            {
                PAnimClip c = new PAnimClip { Name = _newName, Length = 1f, Loop = true };
                c.AddKey(0f);
                PlayerAnims.Clips[c.Name] = c;
                Select(c.Name);
                _dirty = true;
                _note = "Створено «" + c.Name + "». Додай кістки праворуч і ключі.";
            }
            if (GUILayout.Button("Копія вибраного", GUILayout.Width(130f)) && Sel != null)
            {
                PAnimClip c = Sel.Copy(_newName);
                PlayerAnims.Clips[c.Name] = c;
                Select(c.Name);
                _dirty = true;
            }
            GUI.enabled = true;
            if (GUILayout.Button("Перечитати файли", GUILayout.Width(130f))) { PlayerAnims.Load(); if (Sel == null) _sel = null; _note = "Кліпи й стани перечитано."; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(PAnimLog.On ? "■ Зупинити запис і зберегти" : "● Записувати (лог анімацій)", GUILayout.Width(230f)))
            {
                if (PAnimLog.On) { string f = PAnimLog.End("вручну"); _note = f != null ? "Запис збережено: " + f : "Не вдалося зберегти запис."; }
                else { PAnimLog.Begin("вручну"); _note = "Записую: останні " + PAnimLog.RingSeconds.ToString("0") + " с кадрів і всі зміни. Політ на Крилах Одіна пише сам."; }
            }
            if (PAnimLog.LastFile.Length > 0) GUILayout.Label("<color=#9a9a9a>останній: " + System.IO.Path.GetFileName(PAnimLog.LastFile) + "</color>");
            GUILayout.EndHorizontal();
        }

        private static bool Valid(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length > 40 || n == "rules") return false; // (rules.txt holds the states)
            foreach (char ch in n) if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')) return false;
            return true;
        }

        private void Select(string n)
        {
            _sel = n; _key = 0; _scrub = 0f; _rename = n; _askDelete = false;
            PAnimClip c = Sel;
            if (c != null && c.Bones.Count > 0 && !c.Bones.Contains(_bone)) _bone = c.Bones[0];
            Show();
        }

        private void Clip(PAnimClip c)
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>" + c.Name + "</b>" + (_dirty ? "  <color=#FFB04A>є незбережені зміни</color>" : ""));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти", GUILayout.Width(90f))) { _note = "Збережено: " + PlayerAnims.Save(c); _dirty = false; }
            if (GUILayout.Button("Скопіювати для мода", GUILayout.Width(160f))) { GUIUtility.systemCopyBuffer = PlayerAnims.AsCode(c); _note = "Кліп скопійовано рядком C# (для вбудованих)."; }
            if (GUILayout.Button(_askDelete ? "Точно видалити?" : "Видалити", GUILayout.Width(120f)))
            {
                if (!_askDelete) _askDelete = true;
                else { PlayerAnims.Delete(c); _sel = null; _askDelete = false; Closed(); _note = "Видалено (і файл)."; return; }
            }
            GUILayout.EndHorizontal();
            // back to the mod's own clip: the saved file goes (a copy kept in player_anims_backup), the built-in comes back
            if (PlayerAnims.HasBuiltin(c.Name) && (c.FromFile || _dirty))
            {
                if (GUILayout.Button(_askRevert ? "Точно? Твої правки цього кліпу зникнуть (копія — у player_anims_backup)" : "↺ Повернути як було в моді", GUILayout.Width(_askRevert ? 470f : 220f)))
                {
                    if (!_askRevert) _askRevert = true;
                    else { _askRevert = false; _dirty = false; _note = PlayerAnims.Revert(c.Name); Restart(); }
                }
            }
            else _askRevert = false;
            GUILayout.BeginHorizontal();
            _rename = GUILayout.TextField(_rename, GUILayout.Width(170f));
            GUI.enabled = Valid(_rename) && _rename != c.Name && PlayerAnims.Find(_rename) == null;
            if (GUILayout.Button("Перейменувати", GUILayout.Width(120f)))
            {
                PlayerAnims.Delete(c);
                c.Name = _rename;
                PlayerAnims.Clips[c.Name] = c;
                PlayerAnims.Save(c);
                _sel = c.Name;
                _note = "Перейменовано й збережено. Правила з цим кліпом поправ у «Станах».";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            float len = Num("Довжина, с", c.Length, 0.1f, 10f, 0.05f);
            if (Mathf.Abs(len - c.Length) > 1e-4f)
            {
                float k = len / c.Length; // the keys and sounds stretch with it
                foreach (PAnimKey key in c.Keys) key.T = Mathf.Round(key.T * k * 1000f) / 1000f;
                foreach (PAnimEvent e in c.Events) e.T = Mathf.Round(e.T * k * 1000f) / 1000f;
                c.Length = len; _dirty = true;
            }
            GUILayout.BeginHorizontal();
            bool loop = GUILayout.Toggle(c.Loop, c.Loop ? " По колу" : " Один раз (потім згасає)", "Button", GUILayout.Width(200f));
            if (loop != c.Loop) { c.Loop = loop; _dirty = true; Show(); }
            bool frozen = GUILayout.Toggle(c.Frozen, c.Frozen ? " Основа: застигла поза гри" : " Основа: жива анімація гри", "Button", GUILayout.Width(250f));
            if (frozen != c.Frozen) { c.Frozen = frozen; _dirty = true; Restart(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9a9a9a>" + (c.Frozen ? "Застигла: кістки кліпу беруть позу гри на мить, коли кліп почався, і далі не слідують за грою (ноги не бовтаються)." : "Жива: повороти кліпу додаються до того, що робить гра цієї миті.") + "</color>");
            float pv = Num("Центр повороту тіла, м", c.Pivot, 0f, 2f, 0.05f);
            if (Mathf.Abs(pv - c.Pivot) > 1e-4f) { c.Pivot = pv; _dirty = true; }

            // what moves it along: the clock, or one of the motion values
            GUILayout.BeginHorizontal();
            GUILayout.Label("Рух кліпу:", GUILayout.Width(90f));
            if (GUILayout.Button(c.Drive == null ? "за часом" : "від «" + c.Drive + "»", GUILayout.Width(150f)))
            {
                int i = c.Drive == null ? -1 : System.Array.IndexOf(PlayerAnims.Values, c.Drive);
                i++;
                c.Drive = i >= PlayerAnims.Values.Length ? null : PlayerAnims.Values[i];
                if (c.Drive != null && Mathf.Abs(c.DriveMax - c.DriveMin) < 1e-4f) { c.DriveMin = 0f; c.DriveMax = 10f; }
                _dirty = true; Show();
            }
            GUILayout.Label("<color=#9a9a9a>клік — наступне</color>");
            GUILayout.EndHorizontal();
            if (c.Drive != null)
            {
                int vi = System.Array.IndexOf(PlayerAnims.Values, c.Drive);
                GUILayout.Label("<color=#9a9a9a>Початок кліпу — коли «" + PlayerAnims.ValueNames[vi] + "» = від, кінець — коли = до. Між ними — плавно.</color>");
                float a = Num("від", c.DriveMin, -720f, 720f, 0.5f), b = Num("до", c.DriveMax, -720f, 720f, 0.5f);
                if (a != c.DriveMin || b != c.DriveMax) { c.DriveMin = a; c.DriveMax = b; _dirty = true; }
                PlayerAnimator an = Me != null ? PlayerAnimator.For(Me) : null;
                if (an != null) GUILayout.Label("Зараз «" + c.Drive + "» = " + an.ValueOf(c.Drive).ToString("0.0"));
            }
        }

        // ---------------------------------------------------------------- playing it on you

        private void Preview(PAnimClip c)
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>На тобі</b>  <color=#9a9a9a>(шар " + PlayerAnims.PreviewSlot + " — над усіма станами)</color>");
            GUILayout.BeginHorizontal();
            bool play = GUILayout.Toggle(_play, _play ? " ⏸ Пауза" : " ▶ Грати", "Button", GUILayout.Width(110f));
            if (play != _play) { _play = play; Show(); }
            if (GUILayout.Button("■ Зняти з себе", GUILayout.Width(120f))) { _play = false; if (Me != null) PlayerAnims.Stop(Me, PlayerAnims.PreviewSlot, 0.3f); }
            if (GUILayout.Button("Підкинути вгору", GUILayout.Width(130f))) Toss();
            GUILayout.EndHorizontal();
            float sp = Num("Швидкість", _speed, 0.1f, 3f, 0.05f);
            if (Mathf.Abs(sp - _speed) > 1e-4f) { _speed = sp; Show(); }
            PlayerAnimator a = Me != null ? PlayerAnimator.Of(Me) : null;
            if (_play && a != null) _scrub = a.TimeIn(PlayerAnims.PreviewSlot);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Мить", GUILayout.Width(60f));
            float s = GUILayout.HorizontalSlider(_scrub, 0f, c.Length, GUILayout.Width(300f));
            GUILayout.Label(_scrub.ToString("0.00") + " с" + (c.Drive != null ? " = " + Mathf.Lerp(c.DriveMin, c.DriveMax, _scrub / Mathf.Max(0.001f, c.Length)).ToString("0.0") : ""), GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(s - _scrub) > 1e-4f) { _scrub = s; _play = false; _key = -1; Show(); }
            GUILayout.Label("<color=#9a9a9a>«Підкинути вгору» — щоб глянути позу на анімації падіння гри (основа для польоту)." + (c.Drive != null ? " Кліп від значення: «Грати» показує його так, як грав би зараз за твоїм рухом." : "") + "</color>");
        }

        // the clip on you: playing, or held at the chosen key / the scrubbed moment
        private void Show()
        {
            PAnimClip c = Sel;
            Player p = Me;
            if (c == null || p == null) return;
            PlayerAnimator a = PlayerAnimator.For(p);
            if (a.ClipIn(PlayerAnims.PreviewSlot) != c.Name || !a.Playing(PlayerAnims.PreviewSlot)) PlayerAnims.Play(p, PlayerAnims.PreviewSlot, c.Name, 0.2f, _speed);
            else if (_play) PlayerAnims.Play(p, PlayerAnims.PreviewSlot, c.Name, 0.01f, _speed); // again from its start, at the new speed
            a.Hold(PlayerAnims.PreviewSlot, _play ? -1f : (_key >= 0 && _key < c.Keys.Count ? c.Keys[_key].T : _scrub));
        }

        // a new base (the frozen pose taken again): the clip comes in anew
        private void Restart()
        {
            if (Me == null || Sel == null) return;
            PlayerAnimator a = PlayerAnimator.Of(Me);
            if (a != null) a.Refreeze(PlayerAnims.PreviewSlot);
            Show();
        }

        private static void Toss()
        {
            Player p = Me;
            if (p == null) return;
            Rigidbody rb = p.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = rb.linearVelocity + Vector3.up * 16f;
        }

        // ---------------------------------------------------------------- the keys

        private void Keys(PAnimClip c)
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>Ключі</b>  <color=#9a9a9a>(клік — вибрати й тримати цю мить на тобі)</color>");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < c.Keys.Count; i++)
            {
                if (i > 0 && i % 6 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Toggle(_key == i, c.Keys[i].T.ToString("0.00"), "Button", GUILayout.Width(70f)) && _key != i) { _key = i; _play = false; Show(); }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Ключ на миті " + _scrub.ToString("0.00"), GUILayout.Width(170f)))
            {
                PAnimKey k = c.AddKey(_scrub);
                _key = c.Keys.IndexOf(k); _play = false; _dirty = true; Show();
            }
            GUI.enabled = _key >= 0 && _key < c.Keys.Count && c.Keys.Count > 1;
            if (GUILayout.Button("Видалити ключ", GUILayout.Width(120f))) { c.Keys.RemoveAt(_key); _key = Mathf.Min(_key, c.Keys.Count - 1); _dirty = true; Show(); }
            GUI.enabled = _key >= 0 && _key < c.Keys.Count;
            if (GUILayout.Button("Ключ як перший", GUILayout.Width(130f)) && c.Keys.Count > 0) { foreach (string b in c.Bones) c.Keys[_key].V[b] = c.Keys[0].V[b]; _dirty = true; Show(); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_key >= 0 && _key < c.Keys.Count)
            {
                PAnimKey k = c.Keys[_key];
                float t = Num("Час ключа, с", k.T, 0f, c.Length, 0.01f);
                if (Mathf.Abs(t - k.T) > 1e-4f) { k.T = t; c.Sort(); _key = c.Keys.IndexOf(k); _dirty = true; Show(); }
                if (c.Loop && c.Drive == null && c.Keys.Count > 1) GUILayout.Label("<color=#9a9a9a>По колу: після останнього ключа поза плавно вертається до першого.</color>");
            }
        }

        // ---------------------------------------------------------------- the sounds

        private void Events(PAnimClip c)
        {
            GUILayout.Space(6);
            GUILayout.Label("<b>Звуки</b>  <color=#9a9a9a>(у потрібну мить кліпу; назва ефекту гри, як у вкладці «Звуки»)</color>");
            for (int i = 0; i < c.Events.Count; i++)
            {
                PAnimEvent e = c.Events[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(e.T.ToString("0.00") + " с — " + e.Sound + " ×" + e.Volume.ToString("0.0"), GUILayout.Width(330f));
                if (GUILayout.Button("▶", GUILayout.Width(30f)) && Me != null) PlayerAnims.Sound(e.Sound, Me.transform.position + Vector3.up * 1.2f, e.Volume);
                if (GUILayout.Button("×", GUILayout.Width(30f))) { c.Events.RemoveAt(i); _dirty = true; GUILayout.EndHorizontal(); break; }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            _sound = GUILayout.TextField(_sound, GUILayout.Width(200f));
            _volume = GUILayout.HorizontalSlider(_volume, 0.1f, 2f, GUILayout.Width(80f));
            GUILayout.Label("×" + _volume.ToString("0.0"), GUILayout.Width(36f));
            GUI.enabled = _sound.Trim().Length > 0;
            if (GUILayout.Button("+ на миті " + _scrub.ToString("0.00"), GUILayout.Width(120f)))
            {
                c.Events.Add(new PAnimEvent { T = _scrub, Sound = _sound.Trim(), Volume = _volume });
                c.Sort(); _dirty = true;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- the picture of you

        private void Picture()
        {
            GUILayout.BeginHorizontal();
            _camOn = GUILayout.Toggle(_camOn, _camOn ? " Картинка: є" : " Картинка: нема", "Button", GUILayout.Width(120f));
            for (int i = 0; i < Views.Length; i++)
                if (GUILayout.Toggle(_view == i, Views[i], "Button", GUILayout.Width(70f)) && (_view != i || !_viewSet)) { _view = i; _viewSet = true; _yaw = ViewYaw[i]; _pitch = ViewPitch[i]; _lift = 0f; }
            GUILayout.EndHorizontal();
            if (!_camOn || Me == null) { FreeCam(); return; }
            GUILayout.BeginHorizontal();
            _camDist = GUILayout.HorizontalSlider(_camDist, MinDist, MaxDist, GUILayout.Width(200f));
            GUILayout.Label("<color=#9a9a9a>ліва миша — крутити, колесо — ближче/далі, права — вгору/вниз</color>");
            GUILayout.EndHorizontal();
            Player p = Me;
            if (_cam == null)
            {
                GameObject go = new GameObject("RavenWatch_PlayerAnimCam");
                Object.DontDestroyOnLoad(go);
                _cam = go.AddComponent<Camera>();
                _rt = new RenderTexture(320, 400, 16);
                _cam.targetTexture = _rt;
                _cam.fieldOfView = 40f;
                _cam.nearClipPlane = 0.05f;
                _cam.farClipPlane = 60f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
            }
            if (!_viewSet) { _viewSet = true; _yaw = ViewYaw[_view]; _pitch = ViewPitch[_view]; }
            float big = p.transform.localScale.x; // (a grown body: «Звір Муніна»)
            Vector3 center = p.transform.position + Vector3.up * (1.05f * big + _lift);
            // round you in your own frame: yaw 0 in front of you, 90 at your right; pitch up from level
            Vector3 dir = p.transform.rotation * (Quaternion.Euler(-_pitch, _yaw, 0f) * Vector3.forward);
            _cam.transform.position = center + dir * _camDist * big;
            _cam.transform.LookAt(center);
            Marker(p);
            GUILayout.Box(_rt, GUILayout.Width(320f), GUILayout.Height(400f));
            Steer(GUILayoutUtility.GetLastRect());
        }

        // the picture steered with the mouse: left drag turns round you, the wheel comes closer or goes off, right drag
        // moves the look up or down your body
        private static readonly float[] ViewYaw = { 0f, 90f, 180f, -90f, 0f }, ViewPitch = { 0f, 0f, 0f, 0f, 70f };
        private const float MinDist = 0.6f, MaxDist = 12f;
        private float _yaw, _pitch, _lift;
        private bool _viewSet;
        private int _dragButton = -1;

        private void Steer(Rect pic)
        {
            Event e = Event.current;
            if (e == null) return;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!pic.Contains(e.mousePosition) || (e.button != 0 && e.button != 1)) return;
                    _dragButton = e.button;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (_dragButton < 0) return;
                    _dragButton = -1;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (_dragButton < 0) return;
                    if (_dragButton == 0)
                    {
                        _yaw = Mathf.Repeat(_yaw + e.delta.x * 0.5f + 180f, 360f) - 180f;
                        _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.4f, -80f, 85f);
                    }
                    else _lift = Mathf.Clamp(_lift + e.delta.y * 0.006f * _camDist, -1.2f, 1.6f);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    if (!pic.Contains(e.mousePosition)) return;
                    _camDist = Mathf.Clamp(_camDist * (1f + e.delta.y * 0.06f), MinDist, MaxDist);
                    e.Use();
                    break;
            }
        }

        private void FreeCam()
        {
            FreeMarker();
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            _cam = null; _rt = null;
        }

        // ---------------------------------------------------------------- the bones

        private void Bones(PAnimClip c)
        {
            GUILayout.Label("<b>Кістки</b>  <color=#9a9a9a>(повороти у власних осях кістки)</color>");
            GUILayout.BeginHorizontal();
            for (int g = 0; g < PlayerAnims.Groups.Length; g++)
            {
                if (g == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (GUILayout.Toggle(_group == g, PlayerAnims.GroupNames[g], "Button", GUILayout.Width(130f)) && _group != g)
                {
                    // another part of the body: its first bone the clip turns (or its first at all) is the one being edited
                    _group = g;
                    string pick = null;
                    foreach (string b in PlayerAnims.Groups[g]) if (c.Bones.Contains(b)) { pick = b; break; }
                    _bone = pick ?? PlayerAnims.Groups[g][0];
                }
            }
            GUILayout.EndHorizontal();
            PlayerAnimator a = Me != null ? PlayerAnimator.For(Me) : null;
            GUILayout.BeginHorizontal();
            foreach (string b in PlayerAnims.Groups[_group])
            {
                bool used = c.Bones.Contains(b), missing = a != null && a.Missing(b);
                string label = (used ? "● " : "") + JointName(b) + (missing ? " (нема)" : "");
                if (GUILayout.Toggle(_bone == b, label, "Button", GUILayout.Width(140f))) _bone = b;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("<b>" + PlayerAnims.GroupNames[_group] + " · " + JointName(_bone) + "</b>  <color=#9a9a9a>(" + _bone + ") — " + JointHint(_bone) + "</color>");
            GUILayout.Label(_key >= 0 && _key < c.Keys.Count ? "ключ " + c.Keys[_key].T.ToString("0.00") + " с  <color=#9a9a9a>· на картинці жовта точка — цей суглоб, лінія — те, що він рухає</color>" : "<color=#FFB04A>вибери ключ ліворуч, щоб правити</color>");
            if (!c.Bones.Contains(_bone))
            {
                if (GUILayout.Button("Додати цю кістку в кліп", GUILayout.Width(220f))) { c.AddBone(_bone); c.SetMode(_bone, PlayerAnims.DefaultMode(_bone)); _dirty = true; }
                return;
            }
            if (_key < 0 || _key >= c.Keys.Count) return;
            PAnimKey k = c.Keys[_key];
            Vector3 v = k.V.ContainsKey(_bone) ? k.V[_bone] : Vector3.zero;
            Vector3 was = v;
            bool meters = _bone == PlayerAnims.Move, whole = _bone == PlayerAnims.Body || meters;
            int mode = c.ModeOf(_bone);
            if (!whole)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Як рахувати:", GUILayout.Width(100f));
                for (int m = 0; m < PlayerAnims.ModeNames.Length; m++)
                    if (GUILayout.Toggle(mode == m, PlayerAnims.ModeNames[m], "Button", GUILayout.Width(125f)) && mode != m)
                    {
                        c.SetMode(_bone, m); mode = m; _dirty = true;
                        string other = PlayerAnims.Mirror(_bone);
                        if (_mirror && other != null && c.Bones.Contains(other)) c.SetMode(other, m);
                        Show();
                    }
                GUILayout.EndHorizontal();
                GUILayout.Label("<color=#9a9a9a>" + (mode == PlayerAnims.Aim ? "Куди дивиться: ставиш напрямок частини (вниз, уперед, у бік, назад) — і вона туди дивиться, хоч що робить гра під нею." : mode == PlayerAnims.Frame ? "Повернути: частина повертається від того, як її тримає гра (у ходьбі нога й далі ходить, лише зігнута)." : "Свої осі кістки: у кожної кістки вони свої — для тонкої правки, коли інше не підходить.") + "</color>");
                GUILayout.Label("<color=#FFB04A>Увага: спосіб діє на всі ключі кліпу. Перемкнеш у готовому кліпі — старі числа в інших ключах почнуть означати інше, і поза зламається.</color>");
            }
            // both sides at once only when asked (off by default: a leg moves alone)
            if (PlayerAnims.Mirror(_bone) != null)
            {
                string otherSide = PlayerAnims.Mirror(_bone).StartsWith("Left") ? "ліву" : "праву";
                _mirror = GUILayout.Toggle(_mirror, _mirror ? " ⇄ Дзеркало: правка йде й на " + otherSide + " сторону (натисни — вимкнути)" : " Лише ця сторона  (натисни — правити обидві разом, дзеркально)", "Button");
            }
            // the three numbers in plain words, the same way round for both sides (shown = stored × sign)
            string[] axis;
            Vector3 sign = Vector3.one;
            float side = _bone.StartsWith("Left") ? -1f : 1f;
            bool limb = IsLimb(_bone);
            if (meters) axis = new[] { "Вбік (+ вправо), м", "Вгору (+), м", "Вперед (+), м" };
            else if (_bone == PlayerAnims.Body) { axis = new[] { "Нахил: + уперед, − назад", "Поворот: + вправо", "Нахил убік: + вправо" }; sign = new Vector3(1f, 1f, -1f); }
            else if (mode == PlayerAnims.Aim) { axis = new[] { "Вниз/вгору: −90 донизу, 0 рівно, 90 вгору", "Куди: 0 уперед, 90 від тіла, 180 назад", "Скрутка: + назовні" }; sign = new Vector3(1f, side, side); }
            else if (mode == PlayerAnims.Frame && limb) { axis = new[] { "Уперед (+) / назад (−)", "Скрутка: + назовні", "Від тіла (+) / до тіла (−)" }; sign = new Vector3(-1f, side, side); }
            else if (mode == PlayerAnims.Frame) { axis = new[] { "Нахил: + уперед, − назад", "Поворот: + вправо", "Нахил убік: + вправо" }; sign = new Vector3(1f, 1f, -1f); }
            else axis = new[] { "X (свої осі)", "Y (свої осі)", "Z (свої осі)" };
            if (meters) { v.x = Num(axis[0], v.x, -2f, 2f, 0.01f); v.y = Num(axis[1], v.y, -2f, 3f, 0.01f); v.z = Num(axis[2], v.z, -2f, 2f, 0.01f); }
            else
            {
                v.x = Deg(axis[0], v.x * sign.x) * sign.x;
                v.y = Deg(axis[1], v.y * sign.y) * sign.y;
                v.z = Deg(axis[2], v.z * sign.z) * sign.z;
            }
            if (v != was)
            {
                k.V[_bone] = v;
                _dirty = true;
                if (_mirror) MirrorTo(c, k, _bone);
                Show();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Обнулити", GUILayout.Width(90f))) { k.V[_bone] = Vector3.zero; if (_mirror) MirrorTo(c, k, _bone); _dirty = true; Show(); }
            if (GUILayout.Button("Копіювати", GUILayout.Width(90f))) { _copied = k.V[_bone]; _hasCopy = true; }
            GUI.enabled = _hasCopy;
            if (GUILayout.Button("Вставити", GUILayout.Width(90f))) { k.V[_bone] = _copied; if (_mirror) MirrorTo(c, k, _bone); _dirty = true; Show(); }
            GUI.enabled = true;
            if (GUILayout.Button("Прибрати з кліпу", GUILayout.Width(130f))) { c.RemoveBone(_bone); _dirty = true; Show(); return; }
            GUILayout.EndHorizontal();
            if (PlayerAnims.Mirror(_bone) != null)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Знаки дзеркала:", GUILayout.Width(120f));
                _flipX = GUILayout.Toggle(_flipX, _flipX ? " X −" : " X +", "Button", GUILayout.Width(60f));
                _flipY = GUILayout.Toggle(_flipY, _flipY ? " Y −" : " Y +", "Button", GUILayout.Width(60f));
                _flipZ = GUILayout.Toggle(_flipZ, _flipZ ? " Z −" : " Z +", "Button", GUILayout.Width(60f));
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Віддзеркалити весь кліп: " + (_bone.StartsWith("Left") ? "ліве → праве" : "праве → ліве"), GUILayout.Width(300f)))
                {
                    bool fromLeft = _bone.StartsWith("Left");
                    foreach (string b in new List<string>(c.Bones))
                        if (b.StartsWith(fromLeft ? "Left" : "Right")) foreach (PAnimKey key in c.Keys) MirrorTo(c, key, b);
                    _dirty = true; Show();
                }
                GUILayout.Label("<color=#9a9a9a>Якщо друга рука крутиться не туди — перемкни знак тієї осі.</color>");
            }
        }

        // ---------------------------------------------------------------- the joints in plain words

        private static string Part(string b) { return b.StartsWith("Left") ? b.Substring(4) : b.StartsWith("Right") ? b.Substring(5) : b; }

        private static bool IsLimb(string b) { string n = Part(b); return n.Contains("Arm") || n.Contains("Leg") || n == "Foot" || n == "Hand" || n == "ToeBase" || n == "Shoulder"; }

        private static string JointName(string b)
        {
            if (b == PlayerAnims.Body) return "Поворот тіла";
            if (b == PlayerAnims.Move) return "Зсув тіла";
            switch (Part(b))
            {
                case "Hips": return "Таз";
                case "Spine": return "Поперек";
                case "Spine1": return "Груди";
                case "Spine2": return "Верх грудей";
                case "Neck": return "Шия";
                case "Head": return "Голова";
                case "Shoulder": return "Ключиця";
                case "Arm": return "Плече";
                case "ForeArm": return "Лікоть";
                case "Hand": return "Кисть";
                case "UpLeg": return "Стегно";
                case "Leg": return "Коліно";
                case "Foot": return "Кісточка";
                case "ToeBase": return "Пальці";
            }
            return b;
        }

        // what turning this joint moves
        private static string JointHint(string b)
        {
            if (b == PlayerAnims.Body) return "нахиляє й повертає всього персонажа довкола центру";
            if (b == PlayerAnims.Move) return "зсуває всього персонажа (у метрах)";
            switch (Part(b))
            {
                case "Hips": return "таз: тягне за собою все — і корпус, і ноги";
                case "Spine": return "усе вище пояса";
                case "Spine1": return "груди, руки й голова";
                case "Spine2": return "плечі, руки й голова";
                case "Neck": return "шия разом із головою";
                case "Head": return "лише голова";
                case "Shoulder": return "уся рука разом із плечем (знизати, опустити плече)";
                case "Arm": return "уся рука від плеча";
                case "ForeArm": return "передпліччя з кистю (згин у лікті)";
                case "Hand": return "кисть";
                case "UpLeg": return "уся нога від стегна";
                case "Leg": return "гомілка зі стопою (згин у коліні)";
                case "Foot": return "стопа";
                case "ToeBase": return "пальці ноги";
            }
            return "";
        }

        // ---------------------------------------------------------------- the marker on the picture

        // a yellow point on the chosen joint and a line down what it moves, drawn over the body and only in the picture
        private GameObject _marker;
        private LineRenderer _line;
        private Material _markerMat;
        private bool _hooked;
        private Animator _markerAnim;
        private readonly Dictionary<string, Transform> _markerBones = new Dictionary<string, Transform>();

        private Transform MarkerBone(Player p, string name)
        {
            if (name == null) return null;
            Animator an = p.GetComponentInChildren<Animator>();
            if (an != _markerAnim) { _markerAnim = an; _markerBones.Clear(); }
            if (an == null) return null;
            Transform t;
            if (_markerBones.TryGetValue(name, out t))
            {
                if (ReferenceEquals(t, null)) return null; // (known to be missing: not looked for every frame)
                if (t != null) return t;
            }
            t = Utils.FindChild(an.transform, name, Utils.IterativeSearchType.DepthFirst);
            _markerBones[name] = t;
            return t;
        }

        private void Marker(Player p)
        {
            Transform joint = MarkerBone(p, _bone == PlayerAnims.Body || _bone == PlayerAnims.Move ? "Hips" : _bone);
            if (joint == null) { if (_marker != null) _marker.SetActive(false); return; }
            if (_marker == null)
            {
                Shader sh = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
                _markerMat = new Material(sh);
                _markerMat.hideFlags = HideFlags.HideAndDontSave;
                _markerMat.color = new Color(1f, 0.85f, 0.2f, 1f);
                _markerMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always); // (over the body: the joint is inside it)
                _markerMat.renderQueue = 4000;
                _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _marker.name = "RavenWatch_PAnimMarker";
                Object.Destroy(_marker.GetComponent<Collider>());
                Object.DontDestroyOnLoad(_marker);
                Renderer mr = _marker.GetComponent<Renderer>();
                mr.sharedMaterial = _markerMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObject lg = new GameObject("line");
                lg.transform.SetParent(_marker.transform, false);
                _line = lg.AddComponent<LineRenderer>();
                _line.sharedMaterial = _markerMat;
                _line.useWorldSpace = true;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!_hooked) { Camera.onPreCull += OnCameraCull; _hooked = true; }
            _marker.SetActive(true);
            float big = p.transform.localScale.x;
            _marker.transform.position = joint.position;
            _marker.transform.localScale = Vector3.one * 0.07f * big;
            // the line: down the chain the joint moves (two bones on), or none for the whole body
            List<Vector3> pts = new List<Vector3> { joint.position };
            string n = _bone == PlayerAnims.Body || _bone == PlayerAnims.Move ? null : PlayerAnims.NextOf(_bone);
            for (int i = 0; i < 2 && n != null; i++)
            {
                Transform t = MarkerBone(p, n);
                if (t == null) break;
                pts.Add(t.position);
                n = PlayerAnims.NextOf(n);
            }
            _line.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _line.SetPosition(i, pts[i]);
            _line.startWidth = _line.endWidth = 0.02f * big;
        }

        // the marker only in the picture's camera, never in the game's own view
        private void OnCameraCull(Camera cam)
        {
            if (_marker == null) return;
            bool mine = cam != null && cam == _cam;
            Renderer r = _marker.GetComponent<Renderer>();
            if (r != null) r.enabled = mine;
            if (_line != null) _line.enabled = mine;
        }

        private void FreeMarker()
        {
            if (_hooked) { Camera.onPreCull -= OnCameraCull; _hooked = false; }
            if (_marker != null) Object.Destroy(_marker);
            if (_markerMat != null) Object.Destroy(_markerMat);
            _marker = null; _line = null; _markerMat = null;
        }

        private void MirrorTo(PAnimClip c, PAnimKey k, string bone)
        {
            string other = PlayerAnims.Mirror(bone);
            if (other == null) return;
            if (!c.Bones.Contains(other)) { c.AddBone(other); c.SetMode(other, c.ModeOf(bone)); }
            Vector3 v = k.V[bone];
            k.V[other] = new Vector3(_flipX ? -v.x : v.x, _flipY ? -v.y : v.y, _flipZ ? -v.z : v.z);
        }

        // ---------------------------------------------------------------- the rules

        private void Rules(float h)
        {
            Player p = Me;
            PlayerAnimator a = p != null ? PlayerAnimator.For(p) : null;
            _rules = GUILayout.BeginScrollView(_rules, GUILayout.Height(h));
            GUILayout.Label("<b>Стани</b>  <color=#9a9a9a>Кожна гра сама вибирає кожному гравцю кліпи за його рухом. У шарі грає перше правило згори, умова якого справджується; жодне — шар згасає. Що грає код (дар), має перевагу над правилами свого шару.</color>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти стани", GUILayout.Width(140f))) { _note = "Стани збережено: " + PlayerAnims.SaveRules(); _rulesDirty = false; }
            if (GUILayout.Button("+ Правило", GUILayout.Width(110f))) { PlayerAnims.Rules.Add(new PAnimRule { Name = "rule" + (PlayerAnims.Rules.Count + 1), Clip = _sel ?? "", When = "air>0.3" }); _rulesDirty = true; }
            if (_rulesDirty) GUILayout.Label("<color=#FFB04A>є незбережені зміни</color>");
            GUILayout.EndHorizontal();

            for (int i = 0; i < PlayerAnims.Rules.Count; i++)
            {
                PAnimRule r = PlayerAnims.Rules[i];
                bool live = a != null && a.RuleIn(r.Slot) == r.Name;
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                bool on = GUILayout.Toggle(r.On, r.On ? " так" : " ні", "Button", GUILayout.Width(44f));
                if (on != r.On) { r.On = on; _rulesDirty = true; }
                string nm = GUILayout.TextField(r.Name, GUILayout.Width(110f));
                if (nm != r.Name && Valid(nm)) { r.Name = nm; _rulesDirty = true; }
                GUILayout.Label("шар", GUILayout.Width(30f));
                for (int s = 0; s < PlayerAnims.Slots - 1; s++) if (GUILayout.Toggle(r.Slot == s, s.ToString(), "Button", GUILayout.Width(26f)) && r.Slot != s) { r.Slot = s; _rulesDirty = true; }
                if (GUILayout.Button(string.IsNullOrEmpty(r.Clip) ? "(кліп)" : r.Clip, GUILayout.Width(150f))) { r.Clip = NextClip(r.Clip); _rulesDirty = true; }
                GUILayout.Label(live ? "<color=#7CFC7C>грає</color>" : PlayerAnims.Find(r.Clip) == null ? "<color=#FF7A7A>нема кліпу</color>" : "", GUILayout.Width(80f));
                if (GUILayout.Button("↑", GUILayout.Width(26f)) && i > 0) { PlayerAnims.Rules.RemoveAt(i); PlayerAnims.Rules.Insert(i - 1, r); _rulesDirty = true; GUILayout.EndHorizontal(); break; }
                if (GUILayout.Button("↓", GUILayout.Width(26f)) && i < PlayerAnims.Rules.Count - 1) { PlayerAnims.Rules.RemoveAt(i); PlayerAnims.Rules.Insert(i + 1, r); _rulesDirty = true; GUILayout.EndHorizontal(); break; }
                if (GUILayout.Button("×", GUILayout.Width(26f))) { PlayerAnims.Rules.RemoveAt(i); _rulesDirty = true; GUILayout.EndHorizontal(); break; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("коли", GUILayout.Width(40f));
                string w = GUILayout.TextField(r.When, GUILayout.Width(380f));
                if (w != r.When) { r.When = w; _rulesDirty = true; }
                GUILayout.Label(r.Readable() ? (a != null && r.Holds(a) ? "<color=#7CFC7C>так зараз</color>" : "<color=#9a9a9a>не зараз</color>") : "<color=#FF7A7A>не читається</color>", GUILayout.Width(100f));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                float fi = SmallNum("вхід, с", r.FadeIn, 0.01f, 2f, 0.05f), fo = SmallNum("вихід, с", r.FadeOut, 0.01f, 2f, 0.05f), sp = SmallNum("швидкість", r.Speed, 0.1f, 3f, 0.05f);
                if (fi != r.FadeIn || fo != r.FadeOut || sp != r.Speed) { r.FadeIn = fi; r.FadeOut = fo; r.Speed = sp; _rulesDirty = true; }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Твій рух зараз</b>  <color=#9a9a9a>(для умов: назва, знак, число; між частинами &; ще tag:назва, !tag:назва)</color>");
            if (a != null)
                for (int i = 0; i < PlayerAnims.Values.Length; i++)
                    GUILayout.Label(PlayerAnims.Values[i] + " = " + a.ValueOf(PlayerAnims.Values[i]).ToString("0.0") + "   <color=#9a9a9a>" + PlayerAnims.ValueNames[i] + "</color>");
            if (a != null)
            {
                string slots = "";
                for (int s = 0; s < PlayerAnims.Slots; s++) slots += "  шар " + s + ": " + (a.ClipIn(s) ?? "—") + (a.ClipIn(s) != null ? " (" + Mathf.RoundToInt(a.WeightIn(s) * 100f) + "%)" : "");
                GUILayout.Label("<b>Шари</b>" + slots);
            }
            GUILayout.Label("<color=#9a9a9a>Приклад: «air>0.4 & speed>3 & tag:odinwings» — у повітрі довше 0,4 с, швидше 3 м/с і на крилах Одіна.</color>");
            GUILayout.EndScrollView();
        }

        private static string NextClip(string cur)
        {
            List<string> names = new List<string>(PlayerAnims.Clips.Keys);
            names.Sort();
            if (names.Count == 0) return "";
            int i = names.IndexOf(cur);
            return names[(i + 1) % names.Count];
        }

        // ---------------------------------------------------------------- the rows

        private float Num(string name, float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(180f));
            if (GUILayout.Button("−", GUILayout.Width(24f))) v -= step;
            float sl = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(170f));
            if (Mathf.Abs(sl - v) > 1e-6f) v = Mathf.Round(sl / step) * step;
            if (GUILayout.Button("+", GUILayout.Width(24f))) v += step;
            v = Mathf.Clamp(v, min, max);
            GUILayout.Label(step < 0.1f ? v.ToString("0.00") : v.ToString("0.0"), GUILayout.Width(50f));
            GUILayout.EndHorizontal();
            return v;
        }

        private float SmallNum(string name, float v, float min, float max, float step)
        {
            GUILayout.Label(name, GUILayout.Width(70f));
            if (GUILayout.Button("−", GUILayout.Width(22f))) v -= step;
            if (GUILayout.Button("+", GUILayout.Width(22f))) v += step;
            v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
            GUILayout.Label(v.ToString("0.00"), GUILayout.Width(40f));
            return v;
        }

        private float Deg(string name, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(225f));
            if (GUILayout.Button("−10", GUILayout.Width(36f))) v -= 10f;
            if (GUILayout.Button("−", GUILayout.Width(24f))) v -= 1f;
            float sl = GUILayout.HorizontalSlider(v, -180f, 180f, GUILayout.Width(150f));
            if (Mathf.Abs(sl - v) > 1e-4f) v = Mathf.Round(sl);
            if (GUILayout.Button("+", GUILayout.Width(24f))) v += 1f;
            if (GUILayout.Button("+10", GUILayout.Width(36f))) v += 10f;
            v = Mathf.Clamp(v, -180f, 180f);
            GUILayout.Label(v.ToString("0") + "°", GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
#endif

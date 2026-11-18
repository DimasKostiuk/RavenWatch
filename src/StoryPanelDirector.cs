#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Режисер» — a simple sequencer for a scene's camera (the user 2026-11-02). Scene
    // first: the scene picked on top (played, stopped, step by step), then
    //   1 Кроки   its steps (◆ the camera's); one picked — a line goes in after it or instead of it (straight into the
    //             mod's own file: StoryScenes.SaveFile), played from it to see it in its place
    //   2 Кадри   the shots of the scene's place («place:» in the scene): fly the free camera (StoryShots.Fly) to an angle
    //             and keep it (the place's own frame: the same in every world it stands in); shown, taken again, off; A / B
    //   3 Рух     what the camera does: a shot · a move A→B in so many seconds (eased) · an orbit round something · over
    //             the shoulder · from above · at his face · going with him · one in the picture, over one's shoulder at
    //             another, two from the side (the shots told in words: their words read by StoryScenes.ReadFraming, the same
    //             as the scene reads them) · back to the player; a cut or eased in; what it
    //             looks at; alive or still (live) — its line, shown. A camera step clicked in 1 comes here, its numbers on
    //             the sliders, and is shown; «show at once» shows each change, so a shot is tuned in the game and put back
    //             with «Замінити». The window «Сцени» opens this one on a scene (its button «Камера»)
    // The scene is staged first by the ordinary director (its camera in plain words); the author films it here after.
    internal partial class StoryPanel
    {
        private const int DirectorTab = 7;
        private static readonly string[] DirKinds = { "shot", "move", "orbit", "behind", "over", "face", "follow", "on", "shoulder", "two", "off" };
        private static readonly string[] DirKindWords = { "Кадр", "Переїзд A→B", "Оберт", "Через плече", "Згори", "На обличчя", "За плечем", "Один у кадрі", "Плече A → B", "Двоє збоку", "Камеру гравцю" };
        private static readonly string[] OnSizeWords = { "Крупно", "Середньо", "На зріст", "Загальний" }, OnAngleWords = { "Спереду", "Справа", "Ззаду", "Зліва" }; // (StoryScenes.OnSizes, OnAngles, in their order)
        // on, shoulder, two: who (A), at whom (B), and the shot's own words — as StoryScenes.Framing holds them
        private string _dirWho = "player", _dirWith = "", _dirSize = "medium", _dirAngle = "", _dirTilt = ""; // (_dirAngle: «» the first clear · front|left|right|back · deg — _dirOnDeg)
        private float _dirOnDeg, _dirPushM, _dirPushS = 3f;
        private int _dirLineSide; // (1 left of the way A looks at B, -1 right, 0 — as the scene has it)
        private bool _dirTrack;
        private Vector2 _dirWhos;
        private static readonly string[] Eases = { "both", "none", "in", "out" };
        private static readonly string[] EaseWords = { "Плавно", "Рівно", "Розгін", "Гальмо" };
        private string _dirLoc = "", _shotName = "", _shotA = "", _shotB = "", _dirLook = "", _dirScene = "", _dirConfirm = "";
        private int _dirKind, _dirEase, _dirStep = -1;
        private bool _dirCut, _dirWait, _dirAtOnce = true;
        private float _dirFaceDeg = 28f, _dirFaceFar = 1.15f, _dirSide = 0.32f, _dirUp = 0.08f, _dirBack = 0.95f, _dirLive;
        private string _dirShown = ""; // (the line «show at once» showed last)
        private float _dirBlend = 1.5f, _dirSecs = 4f, _dirRadius = 4f, _dirHeight = 2f, _dirDeg = 90f, _dirFov;
        private Vector2 _dirShots, _dirLooks, _dirSteps;

        // the window «Сцени»: this scene's camera
        private void DirOpen(StoryScenes.SceneDef d)
        {
            _dirScene = d.Id; _dirStep = -1;
            if (d.Place.Length > 0) { Loc l = Locs.Get(d.Place); _dirLoc = l != null ? l.Id : d.Place; }
            _tab = DirectorTab;
        }

        // a camera step clicked: its words on the sliders, shown in the game
        private void DirLoad(StoryScenes.Step st)
        {
            string[] w = st.W;
            if (w.Length < 2) return;
            int kind = System.Array.IndexOf(DirKinds, w[1]);
            if (kind < 0) { Note = "Цей крок — кадр із місця: міняється лише текстом."; return; }
            _dirKind = kind;
            _dirCut = System.Array.IndexOf(w, "cut") > 0;
            _dirWait = System.Array.IndexOf(w, "wait") > 0;
            _dirBlend = DirWord(w, "blend", 1.5f); _dirFov = DirWord(w, "fov", 0f); _dirLive = DirWord(w, "live", 0f);
            int ei = System.Array.IndexOf(w, "ease");
            _dirEase = ei > 0 && ei + 1 < w.Length ? Mathf.Max(0, System.Array.IndexOf(Eases, w[ei + 1])) : 0;
            int li = System.Array.IndexOf(w, "look");
            _dirLook = li > 0 && li + 1 < w.Length ? w[li + 1] : "";
            switch (w[1])
            {
                case "shot": if (w.Length > 2) _shotA = w[2]; break;
                case "move": if (w.Length > 4) { _shotA = w[2]; _shotB = w[3]; _dirSecs = DirNum(w, 4, _dirSecs); } break;
                case "orbit": if (w.Length > 6) { _dirLook = w[2]; _dirRadius = DirNum(w, 3, _dirRadius); _dirHeight = DirNum(w, 4, _dirHeight); _dirDeg = DirNum(w, 5, _dirDeg); _dirSecs = DirNum(w, 6, _dirSecs); } break;
                case "face": _dirFaceDeg = DirOwn(w, 2, 28f); _dirFaceFar = DirOwn(w, 3, 1.15f); break;
                case "follow": _dirSide = DirOwn(w, 2, 0.32f); _dirUp = DirOwn(w, 3, 0.08f); _dirBack = DirOwn(w, 4, 0.95f); break;
                case "on": case "shoulder": case "two":
                {
                    StoryScenes.Framing f = StoryScenes.ReadFraming(w); // (its words read where the scene reads them)
                    int named = System.Array.IndexOf(StoryScenes.OnAngleDeg, f.Angle);
                    _dirWho = f.A; _dirWith = f.B; _dirSize = f.Size; _dirTilt = f.Tilt; _dirTrack = f.Track; _dirLineSide = f.Side; _dirPushM = f.PushM; _dirPushS = f.PushS;
                    _dirAngle = float.IsNaN(f.Angle) ? "" : named >= 0 ? StoryScenes.OnAngles[named] : "deg";
                    if (_dirAngle == "deg") _dirOnDeg = f.Angle;
                    break;
                }
            }
            _dirShown = "";
            if (Player.m_localPlayer != null && w[1] != "off") { StoryScenes.PreviewCamera(DirLine() + " cut"); _dirShown = DirLine(); }
        }

        private static float DirNum(string[] w, int i, float def) { float v; return i < w.Length && float.TryParse(w[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def; }
        // a shot's own number: only if every word from its name up to it is a number (the 1 of «live 1» further on is not one)
        private static float DirOwn(string[] w, int i, float def) { for (int k = 2; k <= i; k++) { float v; if (k >= w.Length || !float.TryParse(w[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return def; } return DirNum(w, i, def); }
        private static float DirWord(string[] w, string word, float def) { int i = System.Array.IndexOf(w, word); return i > 0 ? DirNum(w, i + 1, def) : def; }

        private void DirectorTabDraw()
        {
            Player p = Player.m_localPlayer;
            StoryScenes.Load();
            // the scene first: all of them to pick from; one picked — only it, and a way back to the list
            GUILayout.BeginHorizontal();
            GUILayout.Label("Сцена:", _head, GUILayout.Width(60f));
            if (StoryScenes.Find(_dirScene) != null)
            {
                if (GUILayout.Button("← Усі сцени", GUILayout.Width(110f))) _dirScene = "";
                GUILayout.Label(_dirScene + (StoryScenes.Playing == _dirScene ? " ▶" : ""), _head);
            }
            else
            {
                AuthorRow scenes = AuthorRow.Begin(); // (as many a row as fit, beside «Сцена:»)
                foreach (StoryScenes.SceneDef d in StoryScenes.Scenes)
                    if (scenes.Button(d.Id + (StoryScenes.Playing == d.Id ? " ▶" : ""))) DirOpen(d);
                scenes.End();
            }
            GUILayout.EndHorizontal();
            StoryScenes.SceneDef s = StoryScenes.Find(_dirScene);
            if (s == null) { GUILayout.Label("Обери сцену.", _hint); return; }
            if (!_dirMore) { DirSimple(p, s); return; } // (the plain face: StoryPanelSceneEdit)
            if (GUILayout.Button("← Простий вигляд", GUILayout.Width(150f))) _dirMore = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label(s.Id + " · місце: " + (s.Place.Length > 0 ? s.Place : "— (додай у сцену рядок place: <місце>, щоб кадри були її)") + " · " + Path.GetFileName(s.From), _hint);
            if (GUILayout.Button("Грати", GUILayout.Width(70f))) EdPlay(p, s, 0);
            GUI.enabled = StoryScenes.Playing.Length > 0;
            if (GUILayout.Button("Стоп", GUILayout.Width(60f))) StoryScenes.Stop("автор");
            GUI.enabled = StoryScenes.Held;
            if (GUILayout.Button("Далі >", GUILayout.Width(70f))) StoryScenes.Go();
            GUI.enabled = true;
            StoryScenes.StepMode = GUILayout.Toggle(StoryScenes.StepMode, " покроково", GUILayout.Width(100f));
            GUILayout.EndHorizontal();
            if (_dirLoc.Length == 0 && p != null) { Loc near = StoryShots.Near(p.transform.position, 400f); if (near != null) _dirLoc = near.Id; }
            GUILayout.BeginHorizontal();
            DirSteps(p, s);
            GUILayout.Space(10);
            DirShots(p);
            GUILayout.Space(10);
            DirMove(p);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- 1 · the shots of a place
        private void DirShots(Player p)
        {
            GUILayout.BeginVertical(GUILayout.Width(330f));
            GUILayout.Label("2 · Кадри місця", _head);
            AuthorRow locs = AuthorRow.Begin();
            if (locs.Button("гравець", _dirLoc == StoryShots.PlayerFrame ? _sel : null)) _dirLoc = StoryShots.PlayerFrame; // (a scene that plays anywhere: the shot round the player himself)
            foreach (Loc l in Locs.All) if (l.Stands && locs.Button(l.Id, l.Id == _dirLoc ? _sel : null)) _dirLoc = l.Id;
            locs.End();
            bool flying = StoryShots.Flying;
            if (GUILayout.Button(flying ? "Вільна камера: ВИМКНУТИ" : "Вільна камера: увімкнути", flying ? _sel : GUI.skin.button, GUILayout.Height(28f))) StoryShots.Fly(!flying);
            GUILayout.Label("ПКМ затиснута — дивитись і коліщатко (кут); WASD, Q/E чи Ctrl/Пробіл — вниз/вгору, Shift — швидко, Alt — повільно.", _hint);
            GUILayout.BeginHorizontal();
            _shotName = GUILayout.TextField(_shotName, 32, GUILayout.Width(150f));
            GUI.enabled = _dirLoc.Length > 0;
            if (GUILayout.Button("Зняти кадр тут"))
            {
                string name = _shotName.Length > 0 ? _shotName : "shot" + (StoryShots.Of(_dirLoc).Count + 1);
                Note = StoryShots.Take(_dirLoc, name, StoryCamera.Now());
                if (_shotA.Length == 0) _shotA = _dirLoc + "." + name; else if (_shotB.Length == 0) _shotB = _dirLoc + "." + name;
                _shotName = "";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            _dirShots = GUILayout.BeginScrollView(_dirShots, GUILayout.Height(300f));
            if (_dirLoc.Length > 0)
            {
                if (StoryShots.Of(_dirLoc).Count == 0) GUILayout.Label("Ще жодного кадру в «" + _dirLoc + "».", _hint);
                foreach (StoryShots.Shot s in new List<StoryShots.Shot>(StoryShots.Of(_dirLoc)))
                {
                    string full = _dirLoc + "." + s.Name;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(s.Name, GUILayout.Width(96f));
                    if (GUILayout.Button("A", full == _shotA ? _sel : GUI.skin.button, GUILayout.Width(28f))) _shotA = full;
                    if (GUILayout.Button("B", full == _shotB ? _sel : GUI.skin.button, GUILayout.Width(28f))) _shotB = full;
                    if (GUILayout.Button("Показати", GUILayout.Width(72f))) { StoryCamera.Pose at; if (StoryShots.Find(full, out at)) { StoryCamera.Shot(StoryShots.Still(at, null), 0.6f); } }
                    if (GUILayout.Button("Оновити", GUILayout.Width(64f))) Note = StoryShots.Take(_dirLoc, s.Name, StoryCamera.Now());
                    if (GUILayout.Button(_dirConfirm == full ? "Так?" : "×", GUILayout.Width(36f)))
                    {
                        if (_dirConfirm == full) { Note = StoryShots.Remove(_dirLoc, s.Name); _dirConfirm = ""; } else _dirConfirm = full;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("«Оновити» — кадр стає там, де камера зараз. Кадри пишуться в мод: blueprints/" + (_dirLoc.Length > 0 ? _dirLoc : "<місце>") + ".shots.txt.", _hint);
            if (GUILayout.Button("Повернути камеру гравцю")) { StoryCamera.Off(0.8f); StoryScreen.Bars(false); }
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- 2 · what the camera does
        private void DirMove(Player p)
        {
            GUILayout.BeginVertical(GUILayout.Width(440f));
            GUILayout.Label("3 · Рух камери", _head);
            AuthorRow kinds = AuthorRow.Begin();
            for (int i = 0; i < DirKinds.Length; i++) if (kinds.Button(DirKindWords[i], i == _dirKind ? _sel : null)) _dirKind = i;
            kinds.End();
            string k = DirKinds[_dirKind];
            if (k == "shot" || k == "move") GUILayout.Label("A: " + (_shotA.Length > 0 ? _shotA : "—") + (k == "move" ? "    B: " + (_shotB.Length > 0 ? _shotB : "—") : ""), _hint);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Різко (cut)", _dirCut ? _sel : GUI.skin.button)) _dirCut = true;
            if (GUILayout.Button("Плавно", !_dirCut ? _sel : GUI.skin.button)) _dirCut = false;
            GUILayout.EndHorizontal();
            if (!_dirCut) Slider("Перехід, с", ref _dirBlend, 0.2f, 5f, 0.1f);
            if (k == "move" || k == "orbit")
            {
                Slider("Рух, с", ref _dirSecs, 0.5f, 30f, 0.5f);
                AuthorRow eases = AuthorRow.Begin();
                for (int i = 0; i < Eases.Length; i++) if (eases.Button(EaseWords[i], i == _dirEase ? _sel : null)) _dirEase = i;
                eases.End();
                _dirWait = GUILayout.Toggle(_dirWait, " сцена чекає кінця руху (wait)");
            }
            if (k == "orbit")
            {
                Slider("Радіус, м", ref _dirRadius, 1f, 20f, 0.5f);
                Slider("Висота, м", ref _dirHeight, -2f, 10f, 0.5f);
                Slider("Градусів", ref _dirDeg, -360f, 360f, 15f);
            }
            if (k == "face")
            {
                Slider("Збоку, °", ref _dirFaceDeg, -180f, 180f, 5f);
                Slider("Відстань, м", ref _dirFaceFar, 0.5f, 20f, 0.5f);
            }
            if (k == "follow")
            {
                Slider("Вбік, м", ref _dirSide, -2f, 2f, 0.02f);
                Slider("Вгору, м", ref _dirUp, -1f, 3f, 0.02f);
                Slider("Назад, м", ref _dirBack, 0.3f, 8f, 0.05f);
            }
            bool pair = k == "shoulder" || k == "two";
            if (k == "on" || pair)
            {
                GUILayout.Label(k == "on" ? "Кого знімати:" : k == "shoulder" ? "A — через чиє плече, B — на кого:" : "A і B — хто у кадрі:", _hint);
                _dirWhos = GUILayout.BeginScrollView(_dirWhos, GUILayout.Height(96f));
                foreach (string n in LookNames())
                {
                    if (n == "sky") continue; // (nobody: only something to look at)
                    if (!pair) { if (GUILayout.Button(LookWord(n), n == _dirWho ? _sel : GUI.skin.button)) _dirWho = n; continue; }
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(LookWord(n));
                    if (GUILayout.Button("A", n == _dirWho ? _sel : GUI.skin.button, GUILayout.Width(28f))) _dirWho = n;
                    if (GUILayout.Button("B", n == _dirWith ? _sel : GUI.skin.button, GUILayout.Width(28f))) _dirWith = n;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if (!pair)
                {
                    AuthorRow sizes = AuthorRow.Begin();
                    for (int i = 0; i < StoryScenes.OnSizes.Length; i++) if (sizes.Button(OnSizeWords[i], StoryScenes.OnSizes[i] == _dirSize ? _sel : null)) _dirSize = StoryScenes.OnSizes[i];
                    sizes.End();
                    AuthorRow angles = AuthorRow.Begin();
                    if (angles.Button("Сама", _dirAngle.Length == 0 ? _sel : null)) _dirAngle = ""; // (no angle asked: the first clear one)
                    for (int i = 0; i < StoryScenes.OnAngles.Length; i++) if (angles.Button(OnAngleWords[i], StoryScenes.OnAngles[i] == _dirAngle ? _sel : null)) _dirAngle = StoryScenes.OnAngles[i];
                    if (angles.Button("Кут", _dirAngle == "deg" ? _sel : null)) _dirAngle = "deg";
                    angles.End();
                    if (_dirAngle == "deg") Slider("Збоку, °", ref _dirOnDeg, -180f, 180f, 5f);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Знизу", _dirTilt == "low" ? _sel : GUI.skin.button)) _dirTilt = "low";
                    if (GUILayout.Button("На рівні голови", _dirTilt.Length == 0 ? _sel : GUI.skin.button)) _dirTilt = "";
                    if (GUILayout.Button("Згори", _dirTilt == "high" ? _sel : GUI.skin.button)) _dirTilt = "high";
                    GUILayout.EndHorizontal();
                    _dirTrack = GUILayout.Toggle(_dirTrack, " іде за ним, коли він іде (track)");
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Бік: як у сцені", _dirLineSide == 0 ? _sel : GUI.skin.button)) _dirLineSide = 0; // (the scene's line; none yet: the first clear side)
                    if (GUILayout.Button("Зліва від A", _dirLineSide > 0 ? _sel : GUI.skin.button)) _dirLineSide = 1;
                    if (GUILayout.Button("Справа від A", _dirLineSide < 0 ? _sel : GUI.skin.button)) _dirLineSide = -1;
                    GUILayout.EndHorizontal();
                }
                Slider("Наїзд, м", ref _dirPushM, -5f, 5f, 0.1f);
                if (Mathf.Abs(_dirPushM) > 0.01f)
                {
                    Slider("Наїзд, с", ref _dirPushS, 0.5f, 30f, 0.1f);
                    _dirWait = GUILayout.Toggle(_dirWait, " сцена чекає кінця наїзду (wait)");
                }
            }
            if (k != "off")
            {
                Slider("Жива, сила", ref _dirLive, 0f, 3f, 0.1f);
                Slider(_dirFov > 1f ? "Кут, °" : "Кут: як у кадрі", ref _dirFov, 0f, 100f, 1f);
                if (_dirFov > 0f && _dirFov < 15f) _dirFov = 15f;
                if (!pair) // (those two look at the ones named)
                {
                    GUILayout.Label(k == "orbit" ? "Навколо чого:" : "Дивитись на:", _hint);
                    _dirLooks = GUILayout.BeginScrollView(_dirLooks, GUILayout.Height(120f));
                    if (k == "shot" || k == "move") if (GUILayout.Button("як у кадрі", _dirLook.Length == 0 ? _sel : GUI.skin.button)) _dirLook = "";
                    if (k == "on") if (GUILayout.Button("на нього самого", _dirLook.Length == 0 ? _sel : GUI.skin.button)) _dirLook = "";
                    foreach (string n in LookNames()) if (GUILayout.Button(LookWord(n), n == _dirLook ? _sel : GUI.skin.button)) _dirLook = n;
                    GUILayout.EndScrollView();
                }
            }
            GUILayout.Space(4);
            string line = DirLine();
            GUILayout.TextField(line);
            _dirAtOnce = GUILayout.Toggle(_dirAtOnce, " показувати одразу, як змінюю");
            if (_dirAtOnce && p != null && k != "off" && line.IndexOf('<') < 0 && _dirShown.Length > 0 && line != _dirShown) { StoryScenes.PreviewCamera(line + " cut"); _dirShown = line; }
            GUILayout.BeginHorizontal();
            GUI.enabled = p != null && line.IndexOf('<') < 0;
            if (GUILayout.Button("Показати", GUILayout.Height(26f))) { StoryScenes.PreviewCamera(line); _dirShown = line; }
            GUI.enabled = true;
            if (GUILayout.Button("Копіювати", GUILayout.Height(26f))) { GUIUtility.systemCopyBuffer = line; Note = "Скопійовано: " + line; }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void Slider(string label, ref float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + F1(v), GUILayout.Width(130f));
            v = Mathf.Round(GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(180f)) / step) * step;
            GUILayout.EndHorizontal();
        }

        private string DirLine()
        {
            string k = DirKinds[_dirKind];
            string how = _dirCut ? " cut" : " blend " + F1(_dirBlend);
            string fov = _dirFov > 1f ? " fov " + Mathf.RoundToInt(_dirFov) : "";
            string ease = Eases[_dirEase] != "both" ? " ease " + Eases[_dirEase] : "";
            string wait = _dirWait ? " wait" : "";
            string look = _dirLook.Length > 0 ? " look " + _dirLook : "";
            if (_dirLive > 0.01f) fov += " live " + F1(_dirLive); // (a living camera: on any of them)
            string push = Mathf.Abs(_dirPushM) > 0.01f ? " push " + F2(_dirPushM) + " " + F2(_dirPushS) + wait : ""; // (on, shoulder, two: only a push has an end to wait for)
            switch (k)
            {
                case "on": return "camera on " + OrWho(_dirWho) + (_dirSize != "medium" ? " " + _dirSize : "") + (_dirAngle == "deg" ? " " + F1(_dirOnDeg) : _dirAngle.Length > 0 ? " " + _dirAngle : "") + (_dirTilt.Length > 0 ? " " + _dirTilt : "") + (_dirTrack ? " track" : "") + push + look + how + fov;
                case "shoulder": case "two": return "camera " + k + " " + OrWho(_dirWho) + " " + OrWho(_dirWith == _dirWho ? "" : _dirWith) + (_dirLineSide > 0 ? " left" : _dirLineSide < 0 ? " right" : "") + push + how + fov;
                case "shot": return "camera shot " + Or(_shotA) + look + how + fov;
                case "move": return "camera move " + Or(_shotA) + " " + Or(_shotB) + " " + F1(_dirSecs) + ease + look + how + fov + wait;
                case "orbit": return "camera orbit " + (_dirLook.Length > 0 ? _dirLook : "player") + " " + F1(_dirRadius) + " " + F1(_dirHeight) + " " + F1(_dirDeg) + " " + F1(_dirSecs) + ease + how + fov + wait;
                case "behind": return "camera behind look " + (_dirLook.Length > 0 ? _dirLook : "player") + how + fov;
                case "over": return "camera over look " + (_dirLook.Length > 0 ? _dirLook : "player") + how + fov;
                case "face": return "camera face " + F1(_dirFaceDeg) + " " + F1(_dirFaceFar) + look + how + fov;
                case "follow": return "camera follow " + F2(_dirSide) + " " + F2(_dirUp) + " " + F2(_dirBack) + how + fov;
                default: return "camera off" + (_dirCut ? " cut" : " blend " + F1(_dirBlend));
            }
        }
        private static string Or(string shot) { return shot.Length > 0 ? shot : "<кадр>"; }
        private static string OrWho(string who) { return who.Length > 0 ? who : "<хто>"; }
        private static string F2(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- 1 · the scene's steps, a line into it
        private void DirSteps(Player p, StoryScenes.SceneDef s)
        {
            GUILayout.BeginVertical(GUILayout.Width(380f));
            GUILayout.Label("1 · Кроки сцени", _head);
            _dirSteps = GUILayout.BeginScrollView(_dirSteps, GUILayout.Height(Fill(90f)));
            for (int i = 0; i < s.Steps.Count; i++)
            {
                bool cam = s.Steps[i].W.Length > 0 && s.Steps[i].W[0] == "camera";
                bool here = StoryScenes.Playing == s.Id && StoryScenes.At == i;
                if (GUILayout.Button((here ? "▶ " : cam ? "◆ " : "   ") + (i + 1) + ". " + s.Steps[i].Line, i == _dirStep ? _sel : GUI.skin.label)) { _dirStep = i; if (cam) DirLoad(s.Steps[i]); }
            }
            GUILayout.EndScrollView();
            string line = DirLine();
            bool picked = _dirStep >= 0 && _dirStep < s.Steps.Count;
            GUILayout.BeginHorizontal();
            GUI.enabled = picked && line.IndexOf('<') < 0;
            if (GUILayout.Button("Вставити після")) Note = PutLine(s, _dirStep, line, false);
            if (GUILayout.Button("Замінити")) Note = PutLine(s, _dirStep, line, true);
            GUI.enabled = picked;
            if (GUILayout.Button("Грати з нього")) EdPlay(p, s, _dirStep);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Обери крок; рядок з «Руху камери» стане після нього або замість нього — одразу у файл сцени. ◆ — камера, ▶ — де сцена зараз.", _hint);
            GUILayout.EndVertical();
        }

        // the line into the scene's file after a step (or instead of it), read again
        private string PutLine(StoryScenes.SceneDef s, int step, string line, bool replace)
        {
            List<string> lines = new List<string>((StoryScenes.FileText(s.From) ?? "").Replace("\r", "").Split('\n'));
            int num = StoryScenes.FileLine(s.Steps[step], lines) + 1;
            if (num < 1) return StoryScenes.FileMoved;
            if (replace) { string was = lines[num - 1].TrimStart(); int c = was.StartsWith("if ") ? was.IndexOf(':') : -1; lines[num - 1] = (c > 0 ? was.Substring(0, c + 1) + " " : "") + line; } // (its «if …:» kept)
            else lines.Insert(num, line);
            string why = StoryScenes.SaveFile(s.From, string.Join("\n", lines.ToArray()));
            if (why != null) return why;
            if (!replace) _dirStep = step + 1;
            return (replace ? "Замінено: " : "Вставлено: ") + line + "  (" + Path.GetFileName(s.From) + ")";
        }
    }
}
#endif

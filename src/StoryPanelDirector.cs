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
    //             the shoulder · from above · back to the player; a cut or eased in; what it looks at — its line, shown
    // The scene is staged first by the ordinary director (its camera in plain words); the author films it here after.
    internal partial class StoryPanel
    {
        private const int DirectorTab = 7;
        private static readonly string[] DirKinds = { "shot", "move", "orbit", "behind", "over", "off" };
        private static readonly string[] DirKindWords = { "Кадр", "Переїзд A→B", "Оберт", "Через плече", "Згори", "Камеру гравцю" };
        private static readonly string[] Eases = { "both", "none", "in", "out" };
        private static readonly string[] EaseWords = { "Плавно", "Рівно", "Розгін", "Гальмо" };
        private string _dirLoc = "", _shotName = "", _shotA = "", _shotB = "", _dirLook = "", _dirScene = "", _dirConfirm = "";
        private int _dirKind, _dirEase, _dirStep = -1;
        private bool _dirCut, _dirWait;
        private float _dirBlend = 1.5f, _dirSecs = 4f, _dirRadius = 4f, _dirHeight = 2f, _dirDeg = 90f, _dirFov;
        private Vector2 _dirShots, _dirLooks, _dirSteps;

        private void DirectorTabDraw()
        {
            Player p = Player.m_localPlayer;
            StoryScenes.Load();
            // the scene first
            GUILayout.BeginHorizontal();
            GUILayout.Label("Сцена:", _head, GUILayout.Width(60f));
            int col = 0;
            foreach (StoryScenes.SceneDef d in StoryScenes.Scenes)
            {
                if (col > 0 && col % 7 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); GUILayout.Space(64f); } // (seven a row)
                col++;
                if (GUILayout.Button(d.Id + (StoryScenes.Playing == d.Id ? " ▶" : ""), d.Id == _dirScene ? _sel : GUI.skin.button))
                {
                    _dirScene = d.Id; _dirStep = -1;
                    if (d.Place.Length > 0) _dirLoc = d.Place == "hall" ? HallSpawn.Name : d.Place;
                }
            }
            GUILayout.EndHorizontal();
            StoryScenes.SceneDef s = StoryScenes.Find(_dirScene);
            if (s == null) { GUILayout.Label("Обери сцену — тоді її кроки й кадри її місця.", _hint); return; }
            GUILayout.BeginHorizontal();
            GUILayout.Label(s.Id + " · місце: " + (s.Place.Length > 0 ? s.Place : "— (додай у сцену рядок place: <місце>, щоб кадри були її)") + " · " + Path.GetFileName(s.From), _hint);
            if (GUILayout.Button("Грати", GUILayout.Width(70f))) { StoryScenes.Play(s); }
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
            GUILayout.BeginHorizontal();
            foreach (Loc l in Locs.All) if (l.Stands && GUILayout.Button(l.Id, l.Id == _dirLoc ? _sel : GUI.skin.button)) _dirLoc = l.Id;
            GUILayout.EndHorizontal();
            bool flying = StoryShots.Flying;
            if (GUILayout.Button(flying ? "Вільна камера: ВИМКНУТИ" : "Вільна камера: увімкнути", flying ? _sel : GUI.skin.button, GUILayout.Height(28f))) StoryShots.Fly(!flying);
            GUILayout.Label("ПКМ затиснута — дивитись і коліщатко (кут); WASD, Q/E — вниз/вгору, Shift — швидко, Ctrl — повільно.", _hint);
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
            GUILayout.BeginVertical();
            GUILayout.Label("3 · Рух камери", _head);
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * 3; i < row * 3 + 3; i++) if (GUILayout.Button(DirKindWords[i], i == _dirKind ? _sel : GUI.skin.button)) _dirKind = i;
                GUILayout.EndHorizontal();
            }
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
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Eases.Length; i++) if (GUILayout.Button(EaseWords[i], i == _dirEase ? _sel : GUI.skin.button)) _dirEase = i;
                GUILayout.EndHorizontal();
                _dirWait = GUILayout.Toggle(_dirWait, " сцена чекає кінця руху (wait)");
            }
            if (k == "orbit")
            {
                Slider("Радіус, м", ref _dirRadius, 1f, 20f, 0.5f);
                Slider("Висота, м", ref _dirHeight, -2f, 10f, 0.5f);
                Slider("Градусів", ref _dirDeg, -360f, 360f, 15f);
            }
            if (k != "off")
            {
                Slider(_dirFov > 1f ? "Кут, °" : "Кут: як у кадрі", ref _dirFov, 0f, 100f, 1f);
                if (_dirFov > 0f && _dirFov < 15f) _dirFov = 15f;
                GUILayout.Label(k == "orbit" ? "Навколо чого:" : "Дивитись на:", _hint);
                _dirLooks = GUILayout.BeginScrollView(_dirLooks, GUILayout.Height(120f));
                if (k == "shot" || k == "move") if (GUILayout.Button("як у кадрі", _dirLook.Length == 0 ? _sel : GUI.skin.button)) _dirLook = "";
                foreach (string n in LookNames()) if (GUILayout.Button(LookWord(n), n == _dirLook ? _sel : GUI.skin.button)) _dirLook = n;
                GUILayout.EndScrollView();
            }
            GUILayout.Space(4);
            string line = DirLine();
            GUILayout.TextField(line);
            GUILayout.BeginHorizontal();
            GUI.enabled = p != null && line.IndexOf('<') < 0;
            if (GUILayout.Button("Показати", GUILayout.Height(26f))) { StoryScenes.PreviewCamera(line); }
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
            switch (k)
            {
                case "shot": return "camera shot " + Or(_shotA) + look + how + fov;
                case "move": return "camera move " + Or(_shotA) + " " + Or(_shotB) + " " + F1(_dirSecs) + ease + look + how + fov + wait;
                case "orbit": return "camera orbit " + (_dirLook.Length > 0 ? _dirLook : "player") + " " + F1(_dirRadius) + " " + F1(_dirHeight) + " " + F1(_dirDeg) + " " + F1(_dirSecs) + ease + how + fov + wait;
                case "behind": return "camera behind look " + (_dirLook.Length > 0 ? _dirLook : "player") + how + fov;
                case "over": return "camera over look " + (_dirLook.Length > 0 ? _dirLook : "player") + how + fov;
                default: return "camera off" + (_dirCut ? " cut" : " blend " + F1(_dirBlend));
            }
        }
        private static string Or(string shot) { return shot.Length > 0 ? shot : "<кадр>"; }

        // ---------------------------------------------------------------- 1 · the scene's steps, a line into it
        private void DirSteps(Player p, StoryScenes.SceneDef s)
        {
            GUILayout.BeginVertical(GUILayout.Width(380f));
            GUILayout.Label("1 · Кроки сцени", _head);
            _dirSteps = GUILayout.BeginScrollView(_dirSteps, GUILayout.Height(400f));
            for (int i = 0; i < s.Steps.Count; i++)
            {
                bool cam = s.Steps[i].W.Length > 0 && s.Steps[i].W[0] == "camera";
                bool here = StoryScenes.Playing == s.Id && StoryScenes.At == i;
                if (GUILayout.Button((here ? "▶ " : cam ? "◆ " : "   ") + (i + 1) + ". " + s.Steps[i].Line, i == _dirStep ? _sel : GUI.skin.label)) _dirStep = i;
            }
            GUILayout.EndScrollView();
            string line = DirLine();
            bool picked = _dirStep >= 0 && _dirStep < s.Steps.Count;
            GUILayout.BeginHorizontal();
            GUI.enabled = picked && line.IndexOf('<') < 0;
            if (GUILayout.Button("Вставити після")) Note = PutLine(s, _dirStep, line, false);
            if (GUILayout.Button("Замінити")) Note = PutLine(s, _dirStep, line, true);
            GUI.enabled = picked;
            if (GUILayout.Button("Грати з нього")) { StoryScenes.Play(s, _dirStep); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Обери крок; рядок з «Руху камери» стане після нього або замість нього — одразу у файл сцени. ◆ — камера, ▶ — де сцена зараз.", _hint);
            GUILayout.EndVertical();
        }

        // the line into the scene's file after a step (or instead of it), read again
        private string PutLine(StoryScenes.SceneDef s, int step, string line, bool replace)
        {
            int num = s.Steps[step].Num;
            List<string> lines = new List<string>((StoryScenes.FileText(s.From) ?? "").Replace("\r", "").Split('\n'));
            if (num < 1 || num > lines.Count) return "Рядок " + num + " не знайдено у файлі — перечитай сцени.";
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

#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Режисер», the plain face of it (the user 2026-11-03: «full control of the scene, and I
    // must see what to click»): one scene as one strip of time, in plain words, each line with the second it comes at. A line
    // clicked — on the right only what can be changed in that line, «Зберегти» puts it into the scene's file at once:
    //   a camera     shown in the game as it is clicked; «Поставити камеру руками» — the free camera (the window shrinks to two
    //                buttons), «Зберегти» keeps where it stands; a cut or eased in, alive or still, how wide
    //   a pause      its seconds · a line of words   its text · the player's walk   its pace, whether the scene waits for it
    //   anything else   its line as written
    // Any line: up, down, away; after it a new shot, pause, line or caw. «Більше» — the director's three columns.
    // Played two ways: as the player sees it, or from outside — on his own body, his free camera left to him (StoryScenes.Watching).
    // Everything goes through the same file as ever (StoryScenes.SaveFile): the scene is read again at once.
    internal partial class StoryPanel
    {
        private bool _dirMore, _dirAdding;
        private int _edFor = -1, _edGone = -1, _edArriveFor = -1; // (_edGone: the step whose «Прибрати» waits for its second click; _edArriveFor: the arrive step whose seconds were read)
        private string _edText = "", _edShotName = "", _edShotFor = "";
        private float _edSecs = 1f;
        private Vector2 _edStrip;

        // the free camera on: the window out of the picture, only what ends it
        private bool DirCompact { get { return _tab == DirectorTab && !_dirMore && StoryShots.Flying; } }

        private void DirSimple(Player p, StoryScenes.SceneDef s)
        {
            GUILayout.BeginHorizontal();
            if (StoryScenes.Playing == s.Id) { if (GUILayout.Button("■ Зупинити сцену", _sel, GUILayout.Width(170f), GUILayout.Height(30f))) StoryScenes.Stop("автор"); }
            else if (GUILayout.Button("▶ Програти всю сцену", GUILayout.Width(170f), GUILayout.Height(30f))) EdPlay(p, s, 0);
            if (StoryScenes.Playing != s.Id && GUILayout.Button("Подивитись збоку", GUILayout.Width(150f), GUILayout.Height(30f))) { StoryScenes.Stop("автор"); StoryCamera.Off(0f); StoryScreen.Reset(); StoryScenes.Watching = true; _dirAdding = false; StoryShots.Fly(true); EdPlay(p, s, 0); StoryScenes.Watching = StoryScenes.Playing == s.Id; }
            GUI.enabled = _dirStep >= 0 && _dirStep < s.Steps.Count;
            if (GUILayout.Button("▶ Програти з цього рядка", GUILayout.Width(190f), GUILayout.Height(30f))) EdPlay(p, s, _dirStep);
            GUI.enabled = true;
            if (GUILayout.Button("Повернути камеру собі", GUILayout.Width(170f), GUILayout.Height(30f))) { StoryCamera.Off(0.8f); StoryScreen.Bars(false); _dirShown = ""; }
            GUI.enabled = StoryScenes.Held;
            if (GUILayout.Button("Далі >", GUILayout.Width(70f), GUILayout.Height(30f))) StoryScenes.Go();
            GUI.enabled = true;
            StoryScenes.StepMode = GUILayout.Toggle(StoryScenes.StepMode, " покроково", GUILayout.Width(100f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Більше…", GUILayout.Width(80f), GUILayout.Height(30f))) _dirMore = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Клікни рядок сцени → праворуч зміни його → «Зберегти». Сцена одразу така. Часу в рядках — приблизно.", _hint);
            foreach (string e in StoryScenes.Errors) GUILayout.Label("Рядок не прочитано, його в сцені НЕМАЄ: " + e, _warn); // (a line the game threw out: said where it is edited)
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            EdStrip(s);
            GUILayout.Space(12);
            EdLine(p, s);
            GUILayout.EndHorizontal();
        }

        // played as a player would see it. A walk the scene picks up as he goes (go … nowait) needs him not to be there yet: if he
        // already stands by its place, he is put StartBack m off, facing it; a short step up to a thing leaves him where he is.
        // A row inside such a walk (before its arrive) starts from the walk; any other row from that row, what comes before it
        // put at once (StoryScenes: Setup). A new shot at the very end gets a pause after it (else it would not be seen)
        internal static float StartBack = 11f; // (as the quest waits for him: near me.call 11)
        private void EdPlay(Player p, StoryScenes.SceneDef s, int row)
        {
            int go = -1, arrive = -1;
            for (int i = 0; i < s.Steps.Count; i++)
            {
                string[] w = s.Steps[i].W;
                if (w.Length < 2 || w[0] != "player") continue;
                if (w[1] == "go" && go < 0 && w.Length > 3 && System.Array.IndexOf(w, "nowait") > 0) go = i;
                else if (w[1] == "arrive" && go >= 0 && arrive < 0) arrive = i;
            }
            int from = go >= 0 && go < row && arrive > go && row <= arrive ? go : row;
            Vector3 to;
            if (p != null && go >= from && StoryWhere.Find(s.Steps[go].W[2], out to) && WorldSpot.Flat(p.transform.position, to) < StartBack * 0.75f)
            {
                Vector3 away = p.transform.position - to; away.y = 0f;
                away = away.sqrMagnitude > 0.25f ? away.normalized : -Vector3.forward;
                Vector3 at = to + away * StartBack;
                float h;
                at.y = ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h) ? h : p.transform.position.y;
                p.transform.position = at;
                p.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
                Rigidbody body = p.GetComponent<Rigidbody>();
                if (body != null) { body.position = at; body.velocity = Vector3.zero; }
                Note = "Поставив тебе на початок ходи: " + Mathf.RoundToInt(StartBack) + " м від місця.";
            }
            StoryScenes.Play(s, from);
        }

        // ---------------------------------------------------------------- the strip of time
        private void EdStrip(StoryScenes.SceneDef s)
        {
            GUILayout.BeginVertical(GUILayout.Width(560f));
            _edStrip = GUILayout.BeginScrollView(_edStrip, GUILayout.Height(Fill(90f)));
            float at = 0f;
            for (int i = 0; i < s.Steps.Count; i++)
            {
                StoryScenes.Step st = s.Steps[i];
                bool here = StoryScenes.Playing == s.Id && StoryScenes.At == i;
                string text = at.ToString("0", CultureInfo.InvariantCulture) + " с   " + (here ? "▶ " : "") + EdWords(st);
                if (GUILayout.Button(text, i == _dirStep ? _sel : GUI.skin.label)) EdPick(s, i);
                at += EdSecs(st);
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Разом близько " + at.ToString("0", CultureInfo.InvariantCulture) + " с", _hint);
            GUILayout.EndVertical();
        }

        private void EdPick(StoryScenes.SceneDef s, int i)
        {
            _dirStep = i; _edFor = -1; _edGone = -1; _edArriveFor = -1;
            if (s.Steps[i].W.Length > 0 && s.Steps[i].W[0] == "camera") DirLoad(s.Steps[i]);
        }

        // a step in plain words
        private static string EdWords(StoryScenes.Step st)
        {
            string[] w = st.W;
            string a = w.Length > 0 ? w[0] : "", b = w.Length > 1 ? w[1] : "", when = st.If != null ? "(за умови) " : "";
            string rest = st.Text.IndexOf(' ') > 0 ? st.Text.Substring(st.Text.IndexOf(' ') + 1).Trim() : "";
            switch (a)
            {
                case "camera": return when + "◆ КАДР: " + EdCamera(b);
                case "wait": return when + "пауза " + b + " с";
                case "sub": return when + "текст унизу: " + rest;
                case "msg": return when + "напис посередині: " + rest;
                case "say": return when + (b == "munin" ? "Мунін" : "Хугін") + " каже: " + rest.Substring(rest.IndexOf(' ') + 1);
                case "sound": return when + (b == "stop" ? "звук стихає" : "звук: " + (b == "sfx_raven_kaw" ? "каркання" : b));
                case "music": return when + (b == "off" ? "музика: тиша" : b == "game" ? "музика: знову гри" : "музика: " + b);
                case "bed": return when + (b == "off" ? "фон стихає" : "фон: " + b);
                case "mix": return when + (b == "reset" ? "гучність світу як була" : "гучність: " + b + " " + (w.Length > 2 ? w[2] : ""));
                case "mute": return when + "не звучить: " + b;
                case "unmute": return when + "знову звучить: " + b;
                case "ear": return when + (b == "camera" ? "чути з кадру" : "чути від гравця");
                case "bars": return when + (b == "on" ? "чорні смуги з'являються" : "чорні смуги зникають");
                case "fade": return when + (b == "out" ? "екран темніє" : "екран світлішає");
                case "own": return when + (w.Length > 2 && w[2] == "here" ? "ворон на плечі тепер твій (" + b + ")" : "свій ворон прилітає (" + b + ")");
                case "until": return when + "чекає: " + rest;
                case "bird":
                    switch (b)
                    {
                        case "come": return when + "ворон прилітає";
                        case "circle": return when + "ворон кружляє";
                        case "say": return when + "ворон каже: " + rest.Substring(rest.IndexOf(' ') + 1);
                        case "look": return when + "ворон дивиться на гравця";
                        case "land": return when + "ворон сідає: " + (w.Length > 2 ? w[2] : "");
                        case "leave": return when + "ворон відлітає";
                        case "gone": return when + "ворон зникає";
                        case "shoulder": return when + "ворон сідає тобі на плече";
                    }
                    break;
                case "player":
                    switch (b)
                    {
                        case "go": return when + "гравець іде: " + (w.Length > 2 ? w[2] : "") + (System.Array.IndexOf(w, "keep") > 0 ? ", як ішов" : System.Array.IndexOf(w, "run") > 0 ? ", бігом" : ", пішки") + (System.Array.IndexOf(w, "nowait") > 0 ? ", сцена йде далі" : ", сцена чекає");
                        case "pace": return when + "гравець переходить: " + (w.Length > 2 ? (w[2] == "walk" ? "на крок" : w[2] == "run" ? "на біг" : "на звичайний темп") : "");
                        case "gait": return when + "хода гравця: " + EdGaitWord(w.Length > 2 ? w[2] : "");
                        case "arrive": return when + (w.Length > 3 ? "чекає, поки гравцю лишиться " + w[3] + " м" : "чекає, поки гравець дійде");
                        case "clip": return when + "гравець рухається: " + (w.Length > 2 ? w[2] : "");
                        case "face": return when + "гравець дивиться на: " + (w.Length > 2 ? w[2] : "");
                        case "emote": return when + "гравець: " + (w.Length > 2 ? w[2] : "");
                    }
                    break;
                case "npc":
                    if (w.Length > 2 && w[2] == "say") return when + b + " каже: " + rest.Substring(rest.IndexOf("say ") + 4);
                    break;
            }
            return when + st.Text;
        }

        private static string EdCamera(string kind)
        {
            switch (kind)
            {
                case "follow": return "за плечем, іде з гравцем";
                case "face": return "спереду, на гравця";
                case "over": return "згори";
                case "behind": return "через плече";
                case "on": return "один у кадрі";
                case "shoulder": return "через плече одного на іншого";
                case "two": return "двоє збоку";
                case "orbit": return "оберт навколо";
                case "shot": return "твій кадр";
                case "move": return "переїзд між кадрами";
                case "off": return "камера повертається гравцю";
            }
            return "з місця " + kind;
        }

        // about how long a step holds the scene (a walk and a clip that waits: a rough guess)
        private static float EdSecs(StoryScenes.Step st)
        {
            string[] w = st.W;
            if (w.Length < 2) return 0f;
            float v;
            string rest = st.Text.IndexOf(' ') > 0 ? st.Text.Substring(st.Text.IndexOf(' ') + 1) : "";
            switch (w[0])
            {
                case "wait": return float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
                case "fade": return w.Length > 2 && float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 1.5f;
                case "sub": return RavenSpeech.Seconds(rest, RavenSpeech.Pace.Scene);
                case "say": return RavenSpeech.Seconds(rest, RavenSpeech.Pace.Scene);
                case "bird": return w[1] == "say" ? RavenSpeech.Seconds(rest, RavenSpeech.Pace.Scene) : w[1] == "circle" && w.Length > 2 && float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
                case "player": return w[1] == "go" ? (System.Array.IndexOf(w, "nowait") > 0 ? 0f : 8f) : w[1] == "arrive" ? 6f : w[1] == "clip" && System.Array.IndexOf(w, "nowait") < 0 ? 3f : 0f;
            }
            return 0f;
        }

        // ---------------------------------------------------------------- the line picked
        private void EdLine(Player p, StoryScenes.SceneDef s)
        {
            GUILayout.BeginVertical();
            if (_dirStep < 0 || _dirStep >= s.Steps.Count)
            {
                GUILayout.Label("Рядок не вибрано", _head);
                GUILayout.Label("Клікни будь-який рядок ліворуч. Рядки ◆ — це кадри камери: кліком вони одразу показуються в грі.", _hint);
                GUILayout.Space(8);
                if (GUILayout.Button("+ Додати перший кадр у кінець сцени", GUILayout.Height(30f))) { _dirStep = s.Steps.Count - 1; DirAddShot(); }
                GUILayout.EndVertical();
                return;
            }
            StoryScenes.Step st = s.Steps[_dirStep];
            string a = st.W.Length > 0 ? st.W[0] : "", b = st.W.Length > 1 ? st.W[1] : "";
            if (_edFor != _dirStep)
            {
                _edFor = _dirStep;
                _edText = a == "sub" || a == "msg" ? Tail(st.Text, 1) : a == "say" || (a == "bird" && b == "say") ? Tail(st.Text, 2) : st.Text;
                if (!float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out _edSecs)) _edSecs = 1f;
            }
            GUILayout.Label(EdWords(st), _head);
            GUILayout.Space(4);
            if (a == "camera" && System.Array.IndexOf(DirKinds, b) >= 0) EdCam(p, s, st, b);
            else if (a == "player" && b == "go" && st.W.Length > 3) EdGo(s, st);
            else if (a == "player" && b == "gait")
            {
                GUILayout.Label("Як він іде до кінця сцени чи наступного такого рядка:", _hint);
                string now = st.W.Length > 2 ? st.W[2] : "";
                AuthorRow gaits = AuthorRow.Begin();
                foreach (string g in EdGaits())
                    if (gaits.Button(EdGaitWord(g), now == g ? _sel : null)) Note = PutLine(s, _dirStep, "player gait " + g, true);
                gaits.End();
            }
            else if (a == "player" && b == "pace")
            {
                GUILayout.Label("На який темп перейти:", _hint);
                GUILayout.BeginHorizontal();
                string now = st.W.Length > 2 ? st.W[2] : "";
                if (GUILayout.Button("Крок", now == "walk" ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, "player pace walk", true);
                if (GUILayout.Button("Звичайний", now == "jog" ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, "player pace jog", true);
                if (GUILayout.Button("Біг", now == "run" ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, "player pace run", true);
                GUILayout.EndHorizontal();
            }
            else if (a == "player" && b == "arrive")
            {
                GUILayout.Label("Сцена чекає тут, поки гравцю до місця лишиться стільки метрів (0 — поки дійде зовсім):", _hint);
                if (_edFor == _dirStep && st.W.Length > 3 && _edArriveFor != _dirStep) { float left; if (float.TryParse(st.W[3], NumberStyles.Float, CultureInfo.InvariantCulture, out left)) _edSecs = left; _edArriveFor = _dirStep; }
                Slider("Лишається, м", ref _edSecs, 0f, 12f, 0.5f);
                if (GUILayout.Button("Зберегти", GUILayout.Height(30f))) { Note = PutLine(s, _dirStep, "player arrive " + (st.W.Length > 2 ? st.W[2] : "") + (_edSecs > 0.01f ? " " + F1(_edSecs) : ""), true); _edArriveFor = -1; }
            }
            else if (a == "wait")
            {
                Slider("Секунд", ref _edSecs, 0.1f, 20f, 0.1f);
                if (GUILayout.Button("Зберегти", GUILayout.Height(30f))) Note = PutLine(s, _dirStep, "wait " + F1(_edSecs), true);
            }
            else if (a == "sub" || a == "msg" || a == "say" || (a == "bird" && b == "say"))
            {
                GUILayout.Label("Текст:", _hint);
                _edText = GUILayout.TextArea(_edText, GUILayout.Height(60f));
                string head = a == "say" ? "say " + b + " " : a == "bird" ? "bird say " : a + " ";
                if (GUILayout.Button("Зберегти", GUILayout.Height(30f))) Note = PutLine(s, _dirStep, head + _edText.Replace("\n", " ").Trim(), true);
            }
            else
            {
                GUILayout.Label("Рядок сцени як написаний:", _hint);
                _edText = GUILayout.TextField(_edText);
                if (GUILayout.Button("Зберегти", GUILayout.Height(30f))) Note = PutLine(s, _dirStep, _edText.Trim(), true);
            }
            GUILayout.Space(14);
            GUILayout.Label("Сам рядок:", _hint);
            GUILayout.BeginHorizontal();
            GUI.enabled = _dirStep > 0;
            if (GUILayout.Button("↑ Раніше")) EdSwap(s, _dirStep, _dirStep - 1);
            GUI.enabled = _dirStep < s.Steps.Count - 1;
            if (GUILayout.Button("↓ Пізніше")) EdSwap(s, _dirStep, _dirStep + 1);
            GUI.enabled = true;
            if (GUILayout.Button(_edGone == _dirStep ? "Точно прибрати?" : "Прибрати", _edGone == _dirStep ? _sel : GUI.skin.button))
            {
                if (_edGone == _dirStep) { EdRemove(s, _dirStep); _edGone = -1; }
                else _edGone = _dirStep;
            }
            GUILayout.EndHorizontal();
            if (a == "camera" && (b == "shot") && st.W.Length > 2)
            {
                string sure = "shot:" + st.W[2];
                if (GUILayout.Button(_dirConfirm == sure ? "Точно видалити кадр «" + st.W[2] + "»?" : "Видалити цей кадр зовсім (і рядок, і сам кадр)", _dirConfirm == sure ? _sel : GUI.skin.button))
                {
                    if (_dirConfirm == sure) { _dirConfirm = ""; EdRemoveShot(s, _dirStep, st.W[2]); }
                    else _dirConfirm = sure;
                }
            }
            GUILayout.Space(14);
            GUILayout.Label("Додати після цього рядка:", _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Кадр камери", GUILayout.Height(28f))) DirAddShot();
            if (GUILayout.Button("Паузу", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "wait 2", false);
            if (GUILayout.Button("Текст", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "sub …", false);
            if (GUILayout.Button("Каркання", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "sound sfx_raven_kaw player", false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Музику", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "music meadows 3", false);
            if (GUILayout.Button("Тишу", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "music off 3", false);
            if (GUILayout.Button("Стишити світ", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "mix world 0.2 2", false);
            if (GUILayout.Button("Чути з кадру", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "ear camera", false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Хода гравця", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "player gait careful", false);
            if (GUILayout.Button("Гравець на крок", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "player pace walk", false);
            if (GUILayout.Button("Дочекатись, поки дійде", GUILayout.Height(28f))) Note = PutLine(s, _dirStep, "player arrive -", false);
            GUILayout.EndHorizontal();
            if (Note.Length > 0) { GUILayout.Space(8); GUILayout.Label(Note, _hint); }
            GUILayout.EndVertical();
        }

        private static string Tail(string text, int words)
        {
            string t = text;
            for (int i = 0; i < words; i++) { int sp = t.IndexOf(' '); if (sp < 0) return ""; t = t.Substring(sp + 1).TrimStart(); }
            return t;
        }

        private static string[] EdGaits() { List<string> l = new List<string> { "none" }; foreach (string g in BodyGait.Names) if (g != BodyGait.Plain) l.Add(g); return l.ToArray(); } // (the table's: a kind added is a button)
        private static string EdGaitWord(string g)
        {
            switch (g)
            {
                case "none": case "plain": return "звичайна";
                case "careful": return "обережна";
                case "tired": return "втомлена";
                case "meek": return "несміла";
                case "soldier": return "як вояк";
                case "spry": return "пружна";
                case "elder": return "по-старечому";
                case "waddle": return "перевальцем";
                case "limp": return "кульгає";
            }
            return g;
        }

        // the player's walk: at what pace, does the scene wait for it
        private void EdGo(StoryScenes.SceneDef s, StoryScenes.Step st)
        {
            string[] w = st.W;
            bool keep = System.Array.IndexOf(w, "keep") > 0, run = System.Array.IndexOf(w, "run") > 0, nowait = System.Array.IndexOf(w, "nowait") > 0, front = System.Array.IndexOf(w, "front") > 0;
            string stem = "player go " + w[2] + " " + w[3] + (front ? " front" : "") + (System.Array.IndexOf(w, "soft") > 0 ? " soft" : "");
            GUILayout.Label("Темп:", _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Як гравець ішов", keep ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, stem + " keep" + (nowait ? " nowait" : ""), true);
            if (GUILayout.Button("Пішки", !keep && !run ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, stem + (nowait ? " nowait" : ""), true);
            if (GUILayout.Button("Бігом", run ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, stem + " run" + (nowait ? " nowait" : ""), true);
            GUILayout.EndHorizontal();
            string pace = keep ? " keep" : run ? " run" : "";
            GUILayout.Label("Сцена:", _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Чекає, поки дійде", !nowait ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, stem + pace, true);
            if (GUILayout.Button("Йде далі, він іде", nowait ? _sel : GUI.skin.button)) Note = PutLine(s, _dirStep, stem + pace + " nowait", true);
            GUILayout.EndHorizontal();
            GUILayout.Label("Куди: " + w[2] + ", зупиниться за " + w[3] + " м. Місце можна змінити в «Більше…».", _hint);
        }

        // a shot: what can be changed in it
        private void EdCam(Player p, StoryScenes.SceneDef s, StoryScenes.Step st, string kind)
        {
            if (kind == "shot" && st.W.Length > 2 && st.W[2].IndexOf('.') > 0) EdShotName(st.W[2]);
            if (kind != "off")
            {
                GUILayout.Label("Як зараз виглядає — у грі, за цим вікном. Щоб бачити: Shift+F9 або перетягни вікно.", _hint);
                if (GUILayout.Button("Поставити камеру руками", GUILayout.Height(36f))) { StoryShots.Fly(true); _dirAdding = false; }
                GUILayout.Space(8);
            }
            GUILayout.Label("Як з'являється:", _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Різко", _dirCut ? _sel : GUI.skin.button)) _dirCut = true;
            if (GUILayout.Button("Плавно", !_dirCut ? _sel : GUI.skin.button)) _dirCut = false;
            GUILayout.EndHorizontal();
            if (!_dirCut) Slider("Перехід, с", ref _dirBlend, 0.2f, 5f, 0.1f);
            if (kind != "off")
            {
                Slider("Хитання", ref _dirLive, 0f, 3f, 0.1f);
                Slider(_dirFov > 1f ? "Ширина кадру" : "Ширина: звичайна", ref _dirFov, 0f, 100f, 1f);
                if (_dirFov > 0f && _dirFov < 15f) _dirFov = 15f;
            }
            string line = DirLine();
            if (kind != "off" && p != null && line.IndexOf('<') < 0 && _dirShown.Length > 0 && line != _dirShown) { StoryScenes.PreviewCamera(line + " cut"); _dirShown = line; }
            GUILayout.Space(6);
            GUI.enabled = line.IndexOf('<') < 0;
            if (GUILayout.Button("Зберегти", GUILayout.Height(30f))) Note = PutLine(s, _dirStep, line, true);
            GUI.enabled = true;
        }

        // the shot's name, changed in place (its files and every scene's line that names it: StoryShots.Rename)
        private void EdShotName(string full)
        {
            int dot = full.IndexOf('.');
            string of = full.Substring(0, dot), name = full.Substring(dot + 1);
            if (_edShotFor != full) { _edShotFor = full; _edShotName = name; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва кадру:", _hint, GUILayout.Width(95f));
            _edShotName = GUILayout.TextField(_edShotName, GUILayout.Width(170f));
            GUI.enabled = _edShotName.Trim().Length > 0 && _edShotName.Trim() != name;
            if (GUILayout.Button("Перейменувати", GUILayout.Width(120f))) { string why = StoryShots.Rename(of, name, _edShotName.Trim()); Note = why ?? Note; if (why != null && why.StartsWith("Кадр «" + full)) _edShotFor = of + "." + _edShotName.Trim(); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ---------------------------------------------------------------- the free camera
        private void DirAddShot()
        {
            _dirAdding = true; _dirCut = true; _dirLive = 0f; _dirFov = 0f; _dirLook = "";
            StoryShots.Fly(true);
        }

        // the window while it flies
        private void DirFlying()
        {
            if (StoryScenes.Watching)
            {
                GUILayout.Label("Дивишся збоку: сцена йде на твоєму персонажі", _head);
                GUILayout.Label("Камера твоя. Затисни праву кнопку миші — дивитись. WASD — летіти, Q/E чи Ctrl/Пробіл — вниз і вгору, Shift — швидко.", _hint);
                if (GUILayout.Button("■ Зупинити сцену", GUILayout.Height(32f))) { StoryScenes.Stop("автор"); StoryShots.Fly(false); StoryCamera.Off(0.5f); }
                return;
            }
            GUILayout.Label(_dirAdding ? "Новий кадр" : "Кадр: став камеру", _head);
            GUILayout.Label("Затисни праву кнопку миші — дивитись. WASD — летіти, Q/E чи Ctrl/Пробіл — вниз і вгору, Shift — швидко, Alt — повільно, коліщатко — кут.", _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти цей кадр", GUILayout.Height(32f))) DirKeep();
            if (GUILayout.Button("Скасувати", GUILayout.Height(32f), GUILayout.Width(110f))) { StoryShots.Fly(false); _dirAdding = false; _edFor = -1; _dirShown = ""; StoryCamera.Off(0.5f); }
            GUILayout.EndHorizontal();
        }

        private void DirKeep()
        {
            StoryScenes.SceneDef s = StoryScenes.Find(_dirScene);
            Player p = Player.m_localPlayer;
            if (s == null || p == null) { StoryShots.Fly(false); return; }
            StoryCamera.Pose now = StoryCamera.Now();
            string of = DirFrame(s), why = null;
            bool follow = !_dirAdding && _dirKind == System.Array.IndexOf(DirKinds, "follow");
            if (follow) // (it goes on going with him: only where it rides, measured from his head)
            {
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, p.transform.eulerAngles.y, 0f)) * (now.At - Body.Head(p));
                _dirSide = local.x; _dirUp = local.y; _dirBack = -local.z;
            }
            else
            {
                string name = DirShotName(s, of);
                why = StoryShots.Take(of, name, now);
                _dirKind = System.Array.IndexOf(DirKinds, "shot"); _shotA = of + "." + name; _dirLook = ""; _dirFov = 0f;
            }
            StoryShots.Fly(false);
            string line = DirLine();
            Note = (why != null && why.IndexOf("записано") < 0 ? why + " " : "") + (_dirAdding ? PutLine(s, _dirStep, line + (_dirStep >= s.Steps.Count - 1 ? "\nwait 3" : ""), false) : PutLine(s, _dirStep, line, true));
            _dirAdding = false; _edFor = -1;
            StoryScenes.PreviewCamera(line + " cut"); _dirShown = line;
        }

        // where a shot of this scene is kept: its place if it stands here, else round the player
        private static string DirFrame(StoryScenes.SceneDef s)
        {
            Loc ofLoc = Locs.Get(s.Place);
            string of = ofLoc != null ? ofLoc.Id : s.Place;
            Loc l = of.Length > 0 ? Locs.Get(of) : null;
            return l != null && l.Stands ? of : StoryShots.PlayerFrame;
        }

        // a shot already this scene's, taken again, keeps its name; else the first free one
        private string DirShotName(StoryScenes.SceneDef s, string of)
        {
            string stem = s.Id + "_";
            if (!_dirAdding && _dirKind == System.Array.IndexOf(DirKinds, "shot") && _shotA.StartsWith(of + "." + stem)) return _shotA.Substring(of.Length + 1);
            List<StoryShots.Shot> have = StoryShots.Of(of);
            for (int n = 1; ; n++) { string name = stem + n; if (!have.Exists(x => x.Name == name)) return name; }
        }

        // ---------------------------------------------------------------- moving lines in the file
        private void EdSwap(StoryScenes.SceneDef s, int a, int b)
        {
            List<string> lines = new List<string>((StoryScenes.FileText(s.From) ?? "").Replace("\r", "").Split('\n'));
            int na = StoryScenes.FileLine(s.Steps[a], lines), nb = na < 0 ? -1 : StoryScenes.FileLine(s.Steps[b], lines);
            if (na < 0 || nb < 0) { Note = StoryScenes.FileMoved; return; }
            string keep = lines[na]; lines[na] = lines[nb]; lines[nb] = keep;
            string why = StoryScenes.SaveFile(s.From, string.Join("\n", lines.ToArray()));
            if (why != null) { Note = why; return; }
            _dirStep = b; _edFor = -1;
        }

        private void EdRemove(StoryScenes.SceneDef s, int i)
        {
            List<string> lines = new List<string>((StoryScenes.FileText(s.From) ?? "").Replace("\r", "").Split('\n'));
            int n = StoryScenes.FileLine(s.Steps[i], lines);
            if (n < 0) { Note = StoryScenes.FileMoved; return; }
            string gone = lines[n].Trim();
            lines.RemoveAt(n);
            string why = StoryScenes.SaveFile(s.From, string.Join("\n", lines.ToArray()));
            if (why != null) { Note = why; return; }
            _dirStep = -1; _edFor = -1;
            Note = "Рядок прибрано: " + gone;
        }

        // the line, and the shot itself unless another line still uses it (its file of shots)
        private void EdRemoveShot(StoryScenes.SceneDef s, int i, string full)
        {
            EdRemove(s, i);
            if (_dirStep >= 0) return; // (the line stayed: the note says why)
            foreach (StoryScenes.SceneDef o in StoryScenes.Scenes)
                foreach (StoryScenes.Step st in o.Steps)
                    if (System.Array.IndexOf(st.W, full) >= 0) { Note = "Рядок прибрано, а кадр лишився: його ще вживає сцена «" + o.Id + "»."; return; }
            int dot = full.IndexOf('.');
            Note = dot > 0 ? StoryShots.Remove(full.Substring(0, dot), full.Substring(dot + 1)) : "Нема такого кадру: " + full;
        }
    }
}
#endif

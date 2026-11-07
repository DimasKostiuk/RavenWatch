#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story window's tabs for a scene's look (the user 2026-11-02: tools first — he sets the shots himself):
    //   «Камера»      a shot put together by buttons (from where, at what, how soft, how wide), shown at once on your own
    //                 screen, its line for the scene file to copy; the camera where it is now kept as a place to film from
    //   «Атмосфера»   weather, bolts, Thor, a shake — the game's own lists — and what is heard (music ours and the game's, how
    //                 loud the rest of the world is, the sounds of the catalogue), shown at once (here, or for everyone), each with
    //                 its scene line
    // Nothing here is kept but the places: a scene file is where a shot lives (StoryScenes).
    internal partial class StoryPanel
    {
        private const int CameraTab = 4, AirTab = 6;

        // ---------------------------------------------------------------- «Камера»
        private static readonly string[] ShotFrom = { "behind", "over", "place" };
        private static readonly string[] ShotFromWords = { "Через плече", "Згори", "З мітки" };
        private int _shotFrom;
        private string _shotPlace = "", _shotLook = "player", _shotSub = "", _camName = "";
        private float _shotBlend = 1.5f, _shotFov;
        private bool _shotBars = true;
        private Vector2 _shotPlaces, _shotLooks;

        private string ShotLine()
        {
            string from = _shotFrom == 2 ? (_shotPlace.Length > 0 ? _shotPlace : "<мітка>") : ShotFrom[_shotFrom];
            string s = "camera " + from + " look " + _shotLook + " blend " + F1(_shotBlend);
            if (_shotFov > 1f) s += " fov " + F1(_shotFov);
            return s;
        }

        private void CameraTabDraw()
        {
            Player p = Player.m_localPlayer;
            GUILayout.BeginHorizontal();

            // left: from where
            GUILayout.BeginVertical(GUILayout.Width(330f));
            GUILayout.Label("1 · Звідки знімає", _head);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ShotFrom.Length; i++) if (GUILayout.Button(ShotFromWords[i], i == _shotFrom ? _sel : GUI.skin.button)) _shotFrom = i;
            GUILayout.EndHorizontal();
            GUILayout.Label(_shotFrom == 0 ? "За твоїм плечем, повернута на ціль; сама шукає ракурс, де нічого не заважає, і йде за рухом."
                          : _shotFrom == 1 ? "Високо над тобою (буря, Тор, загальний план)." : "З мітки (на висоті очей над нею). Обери мітку нижче або запам'ятай свою камеру.", _hint);
            if (_shotFrom == 2)
            {
                _shotPlaces = GUILayout.BeginScrollView(_shotPlaces, GUILayout.Height(170f));
                foreach (string n in PlaceNames()) if (GUILayout.Button(n, n == _shotPlace ? _sel : GUI.skin.button)) _shotPlace = n;
                GUILayout.EndScrollView();
            }
            GUILayout.Space(6);
            GUILayout.Label("Запам'ятати ракурс тут", _head);
            GUILayout.Label("Стань камерою так, як треба кадр (Shift+F9 — мишку грі, покрути), і збережи: вийде мітка, з якої знімати.", _hint);
            GUILayout.BeginHorizontal();
            _camName = GUILayout.TextField(_camName, 40, GUILayout.Width(170f));
            GUI.enabled = StoryPlaces.GoodName(_camName) && GameCamera.instance != null;
            if (GUILayout.Button("Зберегти ракурс"))
            {
                Transform c = GameCamera.instance.transform;
                Note = StoryPlaces.Set(_camName, c.position - Vector3.up * 1.6f, c.eulerAngles.y); // (a shot from a place rises 1.6 m over it: so it is where the camera is now)
                _shotFrom = 2; _shotPlace = _camName;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // middle: at what
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("2 · На що дивиться", _head);
            _shotLooks = GUILayout.BeginScrollView(_shotLooks, GUILayout.Height(330f));
            foreach (string n in LookNames()) if (GUILayout.Button(LookWord(n), n == _shotLook ? _sel : GUI.skin.button)) _shotLook = n;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // right: how, and try it
            GUILayout.BeginVertical();
            GUILayout.Label("3 · Як", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Плавно, с: " + F1(_shotBlend), GUILayout.Width(110f));
            _shotBlend = Mathf.Round(GUILayout.HorizontalSlider(_shotBlend, 0f, 4f, GUILayout.Width(170f)) * 10f) / 10f;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(_shotFov > 1f ? "Кут, °: " + Mathf.RoundToInt(_shotFov) : "Кут: як у гри", GUILayout.Width(110f));
            _shotFov = Mathf.Round(GUILayout.HorizontalSlider(_shotFov, 0f, 90f, GUILayout.Width(170f)));
            if (_shotFov > 0f && _shotFov < 20f) _shotFov = 20f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Кут як у гри", GUILayout.Width(150f))) _shotFov = 0f;
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = p != null && (_shotFrom != 2 || _shotPlace.Length > 0);
            if (GUILayout.Button("Показати", GUILayout.Height(30f))) { StoryScreen.Bars(_shotBars); StoryScenes.PreviewCamera(ShotLine()); Note = "Кадр: " + ShotLine() + "  · гра не слухає кнопок, поки кадр; «Повернути камеру» — назад."; }
            GUI.enabled = StoryCamera.Active;
            if (GUILayout.Button("Повернути камеру", GUILayout.Height(30f))) { StoryCamera.Off(_shotBlend); StoryScreen.Bars(false); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            _shotBars = GUILayout.Toggle(_shotBars, " чорні смуги (bars on)");
            GUILayout.BeginHorizontal();
            _shotSub = GUILayout.TextField(_shotSub, 160);
            if (GUILayout.Button("Субтитр", GUILayout.Width(90f)) && _shotSub.Length > 0) StoryScreen.Sub(_shotSub, 4f);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            Line("Рядок для сцени", ShotLine());
            if (_shotBars) Line("", "bars on");
            if (_shotSub.Length > 0) Line("", "sub " + _shotSub);
            Line("", "camera off blend " + F1(_shotBlend));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // the places a shot is taken from (the author's marks and the story's places) or looked at (and who is about)
        private static List<string> PlaceNames()
        {
            List<string> l = new List<string>();
            foreach (StoryPlaces.Place pl in StoryPlaces.All) l.Add(pl.Name);
            foreach (Loc loc in Locs.All) l.Add(loc.Id);
            return l;
        }

        private static List<string> LookNames()
        {
            List<string> l = new List<string> { "player", "sky", "bird" };
            HashSet<string> seen = new HashSet<string>();
            foreach (NpcActor n in NpcActor.Live) if (n != null && n.Def != null && seen.Add(n.Def.Id)) l.Add("npc." + n.Def.Id);
            l.AddRange(PlaceNames());
            return l;
        }

        private static string LookWord(string n)
        {
            if (n == "player") return "Гравець";
            if (n == "sky") return "Небо перед тобою";
            if (n == "bird") return "Ворон сцени";
            if (n.StartsWith("npc.")) { NpcDef d = NpcDefs.Find(n.Substring(4)); return (d != null ? Lang.T(d.Name) : n) + "  (" + n + ")"; }
            return n;
        }

        // ---------------------------------------------------------------- «Атмосфера»
        private string _envPick = "ThunderStorm", _musicPick = "";
        private float _envSecs = 60f, _bolts = 3f, _shake = 0.6f;
        private bool _airAll, _boltAhead = true;
        private Vector2 _envScroll, _musicScroll, _soundScroll;
        private float _mixWorld = 1f, _mixAmbient = 1f, _mixMusic = 1f;

        // one channel of the mix: moved, heard at once
        private void MixRow(string title, string word, ref float v, string all)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(120f));
            float was = v;
            v = Mathf.Round(GUILayout.HorizontalSlider(v, 0f, 1f) * 20f) / 20f;
            if (v != was) StoryAudio.Panel("mix " + word + " " + F1N(v) + " 0.3", _airAll);
            GUILayout.TextField("mix " + word + " " + F1N(v) + " 2" + all, GUILayout.Width(170f));
            GUILayout.EndHorizontal();
        }

        private static string F1N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        private void AirTabDraw()
        {
            string all = _airAll ? " all" : "";
            GUILayout.BeginHorizontal();
            _airAll = GUILayout.Toggle(_airAll, " для всіх у світі (all) — інакше лише в тебе");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            // weather
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Погода", _head);
            _envScroll = GUILayout.BeginScrollView(_envScroll, GUILayout.Height(330f));
            if (EnvMan.instance != null) foreach (EnvSetup e in EnvMan.instance.m_environments) if (e != null && GUILayout.Button(e.m_name, e.m_name == _envPick ? _sel : GUI.skin.button)) _envPick = e.m_name;
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Секунд: " + Mathf.RoundToInt(_envSecs), GUILayout.Width(100f));
            _envSecs = Mathf.Round(GUILayout.HorizontalSlider(_envSecs, 5f, 600f, GUILayout.Width(170f)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Показати")) StoryAir.Play("weather " + _envPick + " " + Mathf.RoundToInt(_envSecs), _airAll);
            if (GUILayout.Button("Прибрати")) StoryAir.Play("weather off", _airAll);
            GUILayout.EndHorizontal();
            Line("", "weather " + _envPick + " " + Mathf.RoundToInt(_envSecs) + all);
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // bolts, Thor, a shake
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Блискавки", _head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Перед очима", _boltAhead ? _sel : GUI.skin.button)) _boltAhead = true;
            if (GUILayout.Button("Довкола", !_boltAhead ? _sel : GUI.skin.button)) _boltAhead = false;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Скільки: " + Mathf.RoundToInt(_bolts), GUILayout.Width(100f));
            _bolts = Mathf.Round(GUILayout.HorizontalSlider(_bolts, 1f, 12f, GUILayout.Width(170f)));
            GUILayout.EndHorizontal();
            string bolt = "bolt " + (_boltAhead ? "ahead" : "near") + " " + Mathf.RoundToInt(_bolts);
            if (GUILayout.Button("Показати")) StoryAir.Play(bolt, _airAll);
            Line("", bolt + all);
            GUILayout.Space(8);
            GUILayout.Label("Тор у небі", _head);
            if (GUILayout.Button("Показати")) StoryAir.Play("thor", _airAll);
            Line("", "thor" + all);
            GUILayout.Space(8);
            GUILayout.Label("Тряска", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Сила: " + F1(_shake), GUILayout.Width(100f));
            _shake = Mathf.Round(GUILayout.HorizontalSlider(_shake, 0.1f, 3f, GUILayout.Width(170f)) * 10f) / 10f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Показати")) StoryAir.Play("shake " + F1(_shake), _airAll);
            Line("", "shake " + F1(_shake) + all);
            GUILayout.EndVertical();
            GUILayout.Space(12);

            // what is heard (the audio core; held by this window until «Скинути»)
            GUILayout.BeginVertical();
            GUILayout.Label("Музика: наша і гри", _head);
            _musicScroll = GUILayout.BeginScrollView(_musicScroll, GUILayout.Height(120f));
            foreach (string m in AudioBank.Names()) if (AudioBank.IsTrack(m) && GUILayout.Button(m, m == _musicPick ? _sel : GUI.skin.button)) _musicPick = m;
            foreach (string m in Audio.GameTracks()) if (GUILayout.Button(m, m == _musicPick ? _sel : GUI.skin.button)) _musicPick = m;
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUI.enabled = _musicPick.Length > 0;
            if (GUILayout.Button("Показати")) StoryAudio.Panel("music " + _musicPick + " 2", _airAll);
            GUI.enabled = true;
            if (GUILayout.Button("Тиша")) StoryAudio.Panel("music off 2", _airAll);
            if (GUILayout.Button("Музика гри")) StoryAudio.Panel("music game 2", _airAll);
            GUILayout.EndHorizontal();
            if (_musicPick.Length > 0) Line("", "music " + _musicPick + " 3" + all);
            GUILayout.Label("Решта звуків", _head);
            MixRow("Світ", "world", ref _mixWorld, all);
            MixRow("Вітер, море, фон", "ambient", ref _mixAmbient, all);
            MixRow("Музика", "music", ref _mixMusic, all);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути все")) { StoryAudio.PanelEnd(_airAll); _mixWorld = _mixAmbient = _mixMusic = 1f; }
            if (GUILayout.Button("Що звучить")) Note = Audio.Report();
            GUILayout.EndHorizontal();
            GUILayout.Label("Звуки каталогу", _head);
            _soundScroll = GUILayout.BeginScrollView(_soundScroll, GUILayout.Height(90f));
            foreach (string m in AudioBank.Names()) if (GUILayout.Button("▶ " + m)) { StoryAudio.PanelSound(m); Note = "sound " + m + " player"; }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // a scene line, to copy into the file
        private void Line(string title, string line)
        {
            if (title.Length > 0) GUILayout.Label(title, _head);
            GUILayout.BeginHorizontal();
            GUILayout.TextField(line);
            if (GUILayout.Button("Копіювати", GUILayout.Width(100f))) { GUIUtility.systemCopyBuffer = line; Note = "Скопійовано: " + line; }
            GUILayout.EndHorizontal();
        }

        private static string F1(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
#endif

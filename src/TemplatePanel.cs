#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The «Шаблони» tab of the test window (DevPanel): flight templates (FlightTemplates). Left, the picture: the
    // raven flying the chosen template (or as the director would pick); right, the list and the template: where it
    // may be used (ticks), for whom, how often, its motion and its pose at the ten points of a stroke (wings, body,
    // head, tail). Changes act at once (in the picture and on your ravens); «Зберегти» writes config/RavenWatch/templates.
    internal sealed class TemplatePanel
    {
        private Vector2 _list, _scroll;
        private int _sel = -1, _key, _bone;
        private bool _munin, _director;
        private string _name = "", _note = "";
        private bool _dirty;
        private static readonly string[] BoneNames = { "Плече", "Лікоть", "Кисть", "Тулуб", "Шия", "Голова", "Хвіст" }; // as shown
        private static readonly int[] BoneOf = { 0, 1, 2, 3, 6, 4, 5 };                                            // shown → pose index
        private static readonly string[] OnlyNames = { "Обом", "Лише Хугіну", "Лише Муніну" };

        private FlightTemplate Sel { get { return _sel >= 0 && _sel < FlightTemplates.All.Count ? FlightTemplates.All[_sel] : null; } }

        internal void Closed() { FlightTemplates.PreviewForce = null; }

        internal void Body(float h)
        {
            FlightTemplate t = Sel;
            FlightTemplates.PreviewForce = _director ? null : t;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(FlightStage.Size + 20f));
            Picture(t);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h));
            List();
            if (t != null) { DiveTest(t); Edit(t); }
            Director();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_note.Length > 0) GUILayout.Label(_note);
        }

        private void DiveTest(FlightTemplate t)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Тест ниряння</b> — редагуй нижче «Позу», обираючи 1–10 кадрів змаху.");
            GUILayout.Label("Кнопка запускає звичайний наказ «Пікірування» з вибраним шаблоном. Шаблон примусово діє лише на цей наказ, не на інші польоти.");
            bool active = FlightTemplates.DiveTestName == t.Name;
            if (GUILayout.Button("▶ Пікірувати з «" + t.Name + "»"))
            {
                FlightTemplates.DiveTestName = t.Name;
                FlightTemplates.PreviewForce = t;
                FlightTemplates.PreviewSit = FlightSit.Dive;
                _director = false;
                if (!t.UsePose) { t.UsePose = true; _dirty = true; }
                Player me = Player.m_localPlayer;
                string why = Orders.Why(me, _munin, Orders.Dive);
                if (why != null) _note = "Шаблон тесту увімкнено. Наказ не запущено: " + why + ".";
                else if (Orders.Give(me, _munin, Orders.Dive)) _note = "Тест ниряння: «" + t.Name + "». Зміни пози застосовуються наживо.";
                else _note = "Не вдалося запустити наказ «Пікірування».";
            }
            if (active)
            {
                bool poseOnly = GUILayout.Toggle(FlightTemplates.DiveTestPoseOnly,
                    " Ізолювати позу: вимкнути автоматичну складку крил мотора (траєкторія лишається)", "Button");
                if (poseOnly != FlightTemplates.DiveTestPoseOnly) FlightTemplates.DiveTestPoseOnly = poseOnly;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Тестовий шаблон: " + t.Name + " · тільки ниряння");
                if (GUILayout.Button("Вимкнути", GUILayout.Width(90f))) FlightTemplates.DiveTestName = "";
                GUILayout.EndHorizontal();
            }
            else if (FlightTemplates.DiveTestName.Length > 0)
                GUILayout.Label("Активний тестовий шаблон: " + FlightTemplates.DiveTestName + " (лише ниряння).");
            GUILayout.Label("Для тесту потрібен ворон на плечі, достатньо сили й готовий наказ. Наказ витрачає силу та має звичайну перезарядку.");
        }

        // ---- left: the picture
        private void Picture(FlightTemplate t)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_munin, " Хугін", "Button")) _munin = false;
            if (GUILayout.Toggle(_munin, " Мунін", "Button")) _munin = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_director, "Цей шаблон", "Button")) _director = false;
            if (GUILayout.Toggle(_director, "Як вирішить режисер", "Button")) _director = true;
            GUILayout.EndHorizontal();
            if (_director)
            {
                GUILayout.Label("Ситуація для перегляду:");
                FlightTemplates.PreviewSit = (FlightSit)GUILayout.SelectionGrid((int)FlightTemplates.PreviewSit, FlightTemplates.SitNames, 3);
            }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FlightStage.RouteNames.Length; i++)
                if (GUILayout.Toggle((int)FlightStage.Path == i, FlightStage.RouteNames[i], "Button")) FlightStage.Path = (FlightStage.Route)i;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FlightStage.ViewNames.Length; i++)
                if (GUILayout.Toggle((int)FlightStage.Camera_ == i, FlightStage.ViewNames[i], "Button")) FlightStage.Camera_ = (FlightStage.View)i;
            GUILayout.EndHorizontal();
            FlightStage.Replay = null; // the black box plays in «Політ»; here the templates fly
            Rect pic = GUILayoutUtility.GetRect(FlightStage.Size, FlightStage.Size, GUILayout.Width(FlightStage.Size), GUILayout.Height(FlightStage.Size));
            if (FlightStage.Show(_munin) && FlightStage.Texture != null) GUI.DrawTexture(pic, FlightStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, FlightStage.Problem.Length > 0 ? FlightStage.Problem : "…");
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition)) { FlightStage.Yaw += e.delta.x * 0.6f; FlightStage.Pitch = Mathf.Clamp(FlightStage.Pitch + e.delta.y * 0.4f, -80f, 85f); e.Use(); }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { FlightStage.Zoom = Mathf.Clamp(FlightStage.Zoom - e.delta.y * 0.05f, 0.3f, 5f); e.Use(); }
            FlightStage.SpeedRow();
            GUILayout.Label(FlightStage.Status());
            ShoulderLife life = FlightStage.Life;
            if (life != null) GUILayout.Label("Шаблон зараз: " + life.Mix.Describe());
            GUILayout.Label("Тягни — повернути вид, колесо — ближче/далі.");
        }

        // ---- right: the list
        private void List()
        {
            GUILayout.Label("<b>Шаблони польоту</b>" + (_dirty ? " *" : ""));
            _list = GUILayout.BeginScrollView(_list, GUILayout.Height(120f));
            for (int i = 0; i < FlightTemplates.All.Count; i++)
            {
                FlightTemplate x = FlightTemplates.All[i];
                string label = (x.Builtin ? "★ " : "") + x.Name + "  <color=#9A9A9A>" + Where(x) + "</color>" + (FlightTemplates.ForceName == x.Name ? "  <color=#FFD24A>▶ у грі</color>" : FlightTemplates.DiveTestName == x.Name ? "  <color=#FFD24A>▶ тест ниряння</color>" : "");
                if (GUILayout.Toggle(_sel == i, label, "Button")) { if (_sel != i) { _sel = i; _name = x.Name; _key = 0; } }
            }
            if (FlightTemplates.All.Count == 0) GUILayout.Label("Ще жодного. Як почати:\n1) «Новий» — з'явиться шаблон;\n2) «Де дозволено» — познач, у яких польотах його можна вмикати;\n3) крути «Рух» і «Позу» — ворон зліва показує одразу;\n4) «Зберегти шаблон».");
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Новий")) Add(new FlightTemplate { Name = Free("Новий шаблон") });
            GUI.enabled = Sel != null;
            if (GUILayout.Button("Копія")) Add(Sel.Copy(Free(Sel.Name + " копія")));
            if (GUILayout.Button("Видалити")) { FlightTemplate d = Sel; if (FlightTemplates.ForceName == d.Name) FlightTemplates.ForceName = ""; FlightTemplates.Delete(d); _sel = -1; _note = "Видалено: " + d.Name + (d.Builtin ? " (вбудований повернувся як був)" : "") + "."; }
            GUI.enabled = true;
            if (GUILayout.Button("Перечитати файли")) { FlightTemplates.Load(); _sel = -1; _dirty = false; _note = "Шаблони перечитано з файлів."; }
            GUILayout.EndHorizontal();
        }

        private static string Where(FlightTemplate x)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < FlightTemplates.SitCount; i++) if (x.Allowed[i]) sb.Append(sb.Length > 0 ? ", " : "").Append(FlightTemplates.SitNames[i].ToLowerInvariant());
            return (sb.Length > 0 ? sb.ToString() : "ніде") + (x.Only == 1 ? " · Хугін" : x.Only == 2 ? " · Мунін" : "");
        }

        private static string Free(string name)
        {
            string n = name; int i = 2;
            while (FlightTemplates.Find(n) != null) n = name + " " + i++;
            return n;
        }

        private void Add(FlightTemplate t) { FlightTemplates.All.Add(t); _sel = FlightTemplates.All.Count - 1; _name = t.Name; _key = 0; _dirty = true; _note = "Створено: " + t.Name + ". Збережи, коли сподобається."; }

        // ---- right: the template
        private void Edit(FlightTemplate t)
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва", GUILayout.Width(50f));
            _name = GUILayout.TextField(_name ?? "");
            if (GUILayout.Button("Перейменувати", GUILayout.Width(120f)))
            {
                string n = (_name ?? "").Trim();
                if (n.Length == 0 || (n != t.Name && FlightTemplates.Find(n) != null)) _note = "Назва порожня або вже є.";
                else { bool forced = FlightTemplates.ForceName == t.Name; FlightTemplates.Rename(t, n); if (forced) FlightTemplates.ForceName = n; FlightTemplates.Save(t); _note = "Перейменовано й збережено."; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти шаблон")) { _note = FlightTemplates.Save(t) ? "Збережено: " + t.Name + "." : "Не вдалося зберегти (див. лог)."; _dirty = false; }
            bool forcedNow = FlightTemplates.ForceName == t.Name;
            bool force = GUILayout.Toggle(forcedNow, forcedNow ? " ⏹ Грає на твоїх воронах (натисни — вимкнути)" : " ▶ Грати на твоїх воронах у грі", "Button");
            if (force != forcedNow) FlightTemplates.ForceName = force ? t.Name : "";
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Де дозволено</b> (режисер вмикає його лише тут):");
            for (int r = 0; r < 4; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 3; c++)
                {
                    int i = r * 3 + c;
                    if (i >= FlightTemplates.SitCount) { GUILayout.Space(170f); continue; }
                    bool v = GUILayout.Toggle(t.Allowed[i], " " + FlightTemplates.SitNames[i], GUILayout.Width(170f));
                    if (v != t.Allowed[i]) { t.Allowed[i] = v; _dirty = true; }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Кому:", GUILayout.Width(50f));
            int only = GUILayout.Toolbar(t.Only, OnlyNames);
            if (only != t.Only) { t.Only = only; _dirty = true; }
            GUILayout.EndHorizontal();
            // each raven its own: how often it picks this one (Hugin glides, Munin dashes)
            t.WeightH = Slide("Як часто: Хугін (вага)", t.WeightFor(false), 0f, 5f, 0.1f);
            t.WeightM = Slide("Як часто: Мунін (вага)", t.WeightFor(true), 0f, 5f, 0.1f);

            GUILayout.Space(6);
            bool um = GUILayout.Toggle(t.UseMotion, t.UseMotion ? " <b>Рух</b>: увімкнено" : " <b>Рух</b>: вимкнено (як летить сам)", "Button");
            if (um != t.UseMotion) { t.UseMotion = um; _dirty = true; }
            if (t.UseMotion)
            {
                t.Speed = Slide("Швидкість ×", t.Speed, 0.5f, 1.6f, 0.05f);
                t.Turn = Slide("Різкість поворотів ×", t.Turn, 0.4f, 2f, 0.05f);
                t.Bank = Slide("Крен ×", t.Bank, 0f, 2f, 0.05f);
                t.Glide = Slide("Планерування ×", t.Glide, 0f, 2.5f, 0.05f);
                t.Beat = Slide("Темп змахів ×", t.Beat, 0.4f, 2f, 0.05f);
                t.Wave = Slide("Хвилі вгору-вниз, м", t.Wave, 0f, 3f, 0.1f);
                t.WaveHz = Slide("Хвилі: за секунду", t.WaveHz, 0.1f, 1.5f, 0.05f);
            }

            GUILayout.Space(6);
            bool up = GUILayout.Toggle(t.UsePose, t.UsePose ? " <b>Поза</b>: увімкнено" : " <b>Поза</b>: вимкнено", "Button");
            if (up != t.UsePose) { t.UsePose = up; _dirty = true; }
            if (t.UsePose)
            {
                GUILayout.Label("Кадр змаху (1–5 опускання з верху донизу, 6–10 підйом):");
                GUILayout.BeginHorizontal();
                for (int k = 0; k < WingKeys.Keys; k++) if (GUILayout.Toggle(_key == k, k == 0 ? "1 верх" : k == WingKeys.Half ? (k + 1) + " низ" : (k + 1).ToString(), "Button")) _key = k;
                GUILayout.EndHorizontal();
                _bone = GUILayout.Toolbar(Mathf.Clamp(_bone, 0, BoneNames.Length - 1), BoneNames);
                int bi = BoneOf[_bone];
                Vector3 v = t.Pose[_key, bi];
                bool wing = bi < 3;
                v.x = Deg(wing ? "вперед-назад" : "нахил (+ вниз)", v.x);
                v.y = Deg(wing ? "вгору-вниз" : "поворот (+ вправо)", v.y);
                v.z = Deg(wing ? "поворот уздовж" : "крен (+ вправо)", v.z);
                if (v != t.Pose[_key, bi]) { t.Pose[_key, bi] = v; _dirty = true; }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Копія з попереднього кадру")) { int p = (_key + WingKeys.Keys - 1) % WingKeys.Keys; for (int b = 0; b < FlightTemplate.PoseBones; b++) t.Pose[_key, b] = t.Pose[p, b]; _dirty = true; }
                if (GUILayout.Button("Цю кістку на всі кадри")) { for (int k = 0; k < WingKeys.Keys; k++) t.Pose[k, BoneOf[_bone]] = t.Pose[_key, BoneOf[_bone]]; _dirty = true; }
                if (GUILayout.Button("Скинути кадр")) { for (int b = 0; b < FlightTemplate.PoseBones; b++) t.Pose[_key, b] = Vector3.zero; _dirty = true; }
                GUILayout.EndHorizontal();
                GUILayout.Label("Поза — поправка поверх твого змаху крил (0 = без змін). Крила дзеркальні. Тулуб, голова й хвіст — повороти відносно птаха.");
            }
        }

        // ---- the director's rhythm (saved with the flight, flight.txt)
        private void Director()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Режисер польоту</b> (для всіх шаблонів; зберігається кнопкою «Зберегти» у вкладці «Політ»):");
            FlightTemplates.On = GUILayout.Toggle(FlightTemplates.On, FlightTemplates.On ? " Шаблони в польоті: увімкнено" : " Шаблони в польоті: вимкнено", "Button");
            FlightTemplates.SwitchMin = Slide("Думає раз на, с: від", FlightTemplates.SwitchMin, 1f, 20f, 0.5f);
            FlightTemplates.SwitchMax = Mathf.Max(FlightTemplates.SwitchMin, Slide("…до", FlightTemplates.SwitchMax, 1f, 30f, 0.5f));
            FlightTemplates.KeepChance = Slide("Лишає той самий, %", FlightTemplates.KeepChance * 100f, 0f, 100f, 5f) / 100f;
            FlightTemplates.NoneWeight = Slide("Вага «без шаблону»", FlightTemplates.NoneWeight, 0f, 5f, 0.1f);
            FlightTemplates.BlendSeconds = Slide("Перехід між шаблонами, с", FlightTemplates.BlendSeconds, 0.2f, 3f, 0.05f);
        }

        private float Slide(string name, float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(180f));
            float o = v;
            if (GUILayout.Button("−", GUILayout.Width(26f))) v = Mathf.Clamp(v - step, min, max);
            float sl = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(150f));
            if (Mathf.Abs(sl - v) > 1e-6f) v = Mathf.Clamp(Mathf.Round(sl / step) * step, min, max);
            if (GUILayout.Button("+", GUILayout.Width(26f))) v = Mathf.Clamp(v + step, min, max);
            GUILayout.Label(step < 1f ? v.ToString("0.00") : v.ToString("0"), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(o - v) > 1e-6f) _dirty = true;
            return v;
        }

        private float Deg(string name, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(130f));
            if (GUILayout.Button("−5", GUILayout.Width(30f))) v -= 5f;
            if (GUILayout.Button("−", GUILayout.Width(24f))) v -= 1f;
            float sl = GUILayout.HorizontalSlider(v, -90f, 90f, GUILayout.Width(150f));
            if (Mathf.Abs(sl - v) > 1e-4f) v = Mathf.Round(sl);
            if (GUILayout.Button("+", GUILayout.Width(24f))) v += 1f;
            if (GUILayout.Button("+5", GUILayout.Width(30f))) v += 5f;
            v = Mathf.Clamp(v, -90f, 90f);
            GUILayout.Label(v.ToString("0") + "°", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
#endif

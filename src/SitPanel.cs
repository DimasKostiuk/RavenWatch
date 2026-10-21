#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The «Сидіння» tab of the test window (DevPanel): the sitting system on your own ravens, live. Left: which raven,
    // play the chosen template on it (or hold one moment of it to shape the pose), «as if» events (a blow, a boss, your
    // meal...), its mood, its body on the seat and how readily it reacts (sit.txt). Right: the sitting templates and the
    // chosen one: when, how long, the moment's things, the pose at five keys. Changes act at once; «Зберегти» writes
    // config/RavenWatch/sit_templates (a template) or sit.txt (the body and the chances).
    internal sealed class SitPanel
    {
        private Vector2 _list, _scroll, _left;
        private int _sel = -1, _key, _bone;
        private bool _munin, _hold, _dirty;
        private float _holdU;
        private string _name = "", _note = "";
        private static readonly string[] OnlyNames = { "Обом", "Лише Хугіну", "Лише Муніну" };
        private static readonly string[] MoodPick = { "Сам", "Спокійний", "Насторожений", "Цікавий", "Дрімотний" };
        private static readonly string[] EventNames = { "Удар", "Бос", "Вбивство", "Робота", "Їжа", "Знахідка", "Погладити", "Падіння" };

        private SitTemplate Sel { get { return _sel >= 0 && _sel < SitTemplates.All.Count ? SitTemplates.All[_sel] : null; } }
        private ShoulderLife Life { get { return Shoulder.LocalLife(_munin); } }

        // leaving the tab: the ravens are let go (no held pose, their own mood)
        internal void Closed()
        {
            _hold = false;
            foreach (bool m in new[] { false, true }) { ShoulderLife l = Shoulder.LocalLife(m); if (l != null) { l.SitDirectorNow.HoldU = -1f; l.SitDirectorNow.ForceMood = -1; } }
        }

        internal void Body(float h)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(470f));
            _left = GUILayout.BeginScrollView(_left, GUILayout.Height(h));
            Live();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h));
            List();
            if (Sel != null) Edit(Sel);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_note.Length > 0) GUILayout.Label(_note);
        }

        // ---------------------------------------------------------------- left: your raven, live
        private void Live()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_munin, " Хугін", "Button") && _munin) { Closed(); _munin = false; }
            if (GUILayout.Toggle(_munin, " Мунін", "Button") && !_munin) { Closed(); _munin = true; }
            GUILayout.EndHorizontal();
            ShoulderLife life = Life;
            GUILayout.Label(life == null ? "<color=#FF7A7A>Цього ворона немає на плечі.</color>" : life.SitInfo);
            if (life != null)
            {
                Norov.Character ch = Norov.OnPlayer(life.Player, _munin);
                Norov.Quirk q = Norov.QuirkOf(RavenSeat.On(life.Player, _munin));
                GUILayout.Label("Характер: " + (ch != null ? ch.Name + " (розкрито " + Mathf.RoundToInt(Norov.Shows(life.Level) * 100f) + "%)" : "—") + ", примха: " + (q != null ? q.Name : "—"));
            }
            if (life == null || !life.SitNew) { GUILayout.Label("Ворон має сидіти на плечі (не в польоті)."); Tuning(); return; }
            SitDirector d = life.SitDirectorNow;

            GUILayout.Space(6);
            GUILayout.Label("<b>Шаблон на вороні</b>");
            SitTemplate t = Sel;
            GUI.enabled = t != null;
            if (GUILayout.Button(t != null ? "▶ Зіграти «" + t.Name + "»" : "▶ Зіграти (вибери шаблон праворуч)")) { d.HoldU = -1f; _hold = false; d.PlayNow(life, t); _note = "Грає: " + t.Name + "."; }
            bool hold = GUILayout.Toggle(_hold, _hold ? " ⏸ Тримає кадр (натисни — відпустити)" : " Тримати кадр (щоб правити позу)", "Button");
            GUI.enabled = true;
            if (hold != _hold)
            {
                _hold = hold;
                if (_hold && t != null) { d.HoldU = -1f; d.PlayNow(life, t); _holdU = _key / (float)(SitTemplate.Keys - 1); }
                if (!_hold) d.HoldU = -1f;
            }
            if (_hold)
            {
                if (t != null && (d.Run == null || d.Run.T != t)) { d.HoldU = -1f; d.PlayNow(life, t); } // another template chosen: it holds that one
                _holdU = Slide("Момент шаблону, %", _holdU * 100f, 0f, 100f, 1f, false) / 100f;
                d.HoldU = _holdU;
                GUILayout.Label("Кадр праворуч ставить момент на свій ключ. Ворон тримає позу повністю (без входу й виходу).");
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>Як ніби</b> (обидва ворони реагують, без шансу й паузи):");
            for (int r = 0; r < 2; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 4; c++) { int i = r * 4 + c; if (GUILayout.Button(EventNames[i])) { Closed(); SitEvents.Raise((SitEv)i, true); _note = "Подія: " + EventNames[i] + "."; } }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>Настрій</b>:");
            int mood = GUILayout.Toolbar(d.ForceMood + 1, MoodPick);
            if (mood != d.ForceMood + 1) d.ForceMood = mood - 1;
            Tuning();
        }

        // the body on the seat and the readiness to react (sit.txt)
        private void Tuning()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Тіло на плечі</b> — " + (_munin ? "Мунін" : "Хугін") + " (довіра 7; молодий тримається слабше):");
            SitBodyStyle s = SitBody.StyleOf(_munin);
            s.Hz = Slide("Пружність нахилу, Гц", s.Hz, 0.8f, 8f, 0.1f, false);
            s.Damp = Slide("Гасіння (1 — без розгойдування)", s.Damp, 0.2f, 1.5f, 0.05f, false);
            s.Gain = Slide("Нахил від ривка, °/(м/с²)", s.Gain, 0f, 4f, 0.1f, false);
            s.MaxTilt = Slide("Найбільший нахил, °", s.MaxTilt, 5f, 45f, 1f, false);
            s.DipGain = Slide("Просідання, мм/(м/с²)", s.DipGain * 1000f, 0f, 5f, 0.1f, false) / 1000f;
            s.DipMax = Slide("Найбільше просідання, см", s.DipMax * 100f, 0f, 10f, 0.5f, false) / 100f;
            s.YawHz = Slide("Поворот за тобою, Гц", s.YawHz, 0.5f, 8f, 0.1f, false);
            s.YawDamp = Slide("Поворот: гасіння", s.YawDamp, 0.3f, 1.5f, 0.05f, false);
            s.Grip = Slide("Хватка лапами, °", s.Grip, 1f, 25f, 0.5f, false);
            s.JoltShare = Slide("Змах, коли кинуло на, %", s.JoltShare * 100f, 30f, 100f, 1f, false) / 100f;
            s.WindLean = Slide("Нахил проти вітру на бігу, °", s.WindLean, 0f, 25f, 1f, false);
            SitBody.HabitIn = Slide("Звикає до бігу за, с", SitBody.HabitIn, 0.5f, 10f, 0.5f, false);
            SitBody.HabitGrip = Slide("Звикнувши: хватка +, ×", SitBody.HabitGrip, 0f, 2f, 0.05f, false);
            SitBody.WindWings = Slide("Крила від вітру на бігу", SitBody.WindWings, 0f, 1f, 0.05f, false);

            GUILayout.Space(6);
            GUILayout.Label("<b>Як часто</b> — " + (_munin ? "Мунін" : "Хугін") + ":");
            if (_munin) SitDirector.DoingEveryMunin = Slide("Власна справа раз на, с", SitDirector.DoingEveryMunin, 2f, 120f, 1f, false);
            else SitDirector.DoingEveryHugin = Slide("Власна справа раз на, с", SitDirector.DoingEveryHugin, 2f, 120f, 1f, false);
            float[] ch = _munin ? SitDirector.MuninChance : SitDirector.HuginChance;
            for (int i = 1; i < SitTemplates.Pair; i++) ch[i] = Slide("Реагує: " + SitTemplates.SitNames[i].ToLowerInvariant() + ", %", ch[i] * 100f, 0f, 100f, 5f, false) / 100f;
            GUILayout.Label("Пауза між реакціями на те саме (обом):");
            for (int i = 1; i < SitTemplates.Pair; i++) SitDirector.Cooldown[i] = Slide(SitTemplates.SitNames[i] + ", с", SitDirector.Cooldown[i], 0f, 60f, 0.5f, false);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти (sit.txt)")) { SitSystem.Save(); _note = "Тіло й шанси збережено в sit.txt."; }
            if (GUILayout.Button("Як вбудовано")) { SitSystem.ToBuilt(); _note = "Повернуто вбудовані значення (ще не збережено)."; }
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- right: the templates
        private void List()
        {
            GUILayout.Label("<b>Шаблони сидіння</b>" + (_dirty ? " *" : ""));
            _list = GUILayout.BeginScrollView(_list, GUILayout.Height(150f));
            for (int i = 0; i < SitTemplates.All.Count; i++)
            {
                SitTemplate x = SitTemplates.All[i];
                if (GUILayout.Toggle(_sel == i, (x.Builtin ? "★ " : "") + x.Name + "  <color=#9A9A9A>" + Where(x) + "</color>", "Button") && _sel != i) { _sel = i; _name = x.Name; _key = 0; }
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Новий")) Add(new SitTemplate { Name = Free("Новий шаблон") });
            GUI.enabled = Sel != null;
            if (GUILayout.Button("Копія")) Add(Sel.Copy(Free(Sel.Name + " копія")));
            if (GUILayout.Button("Видалити")) { SitTemplate d = Sel; SitTemplates.Delete(d); _sel = -1; _note = "Видалено: " + d.Name + (d.Builtin ? " (вбудований повернувся як був)" : "") + "."; }
            GUI.enabled = true;
            if (GUILayout.Button("Перечитати файли")) { SitTemplates.Load(); _sel = -1; _dirty = false; _note = "Шаблони сидіння перечитано."; }
            GUILayout.EndHorizontal();
        }

        private static string Where(SitTemplate x)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < SitTemplates.SitCount; i++) if (x.Sits[i]) sb.Append(sb.Length > 0 ? ", " : "").Append(SitTemplates.SitNames[i].ToLowerInvariant());
            return (sb.Length > 0 ? sb.ToString() : "ніде") + (x.Only == 1 ? " · Хугін" : x.Only == 2 ? " · Мунін" : "");
        }

        private static string Free(string name) { string n = name; int i = 2; while (SitTemplates.Find(n) != null) n = name + " " + i++; return n; }

        private void Add(SitTemplate t) { SitTemplates.All.Add(t); _sel = SitTemplates.All.Count - 1; _name = t.Name; _key = 0; _dirty = true; _note = "Створено: " + t.Name + ". Збережи, коли сподобається."; }

        private void Edit(SitTemplate t)
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва", GUILayout.Width(50f));
            _name = GUILayout.TextField(_name ?? "");
            if (GUILayout.Button("Перейменувати", GUILayout.Width(120f)))
            {
                string n = (_name ?? "").Trim();
                if (n.Length == 0 || (n != t.Name && SitTemplates.Find(n) != null)) _note = "Назва порожня або вже є.";
                else { SitTemplates.Rename(t, n); SitTemplates.Save(t); _note = "Перейменовано й збережено (інші гравці впізнають його за назвою)."; }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Зберегти шаблон")) { _note = SitTemplates.Save(t) ? "Збережено: " + t.Name + "." : "Не вдалося зберегти (див. лог)."; _dirty = false; }

            GUILayout.Label("<b>Коли</b> (ситуації):");
            for (int r = 0; r * 3 < SitTemplates.SitCount; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 3; c++) { int i = r * 3 + c; if (i >= SitTemplates.SitCount) break; bool v = GUILayout.Toggle(t.Sits[i], " " + SitTemplates.SitNames[i], GUILayout.Width(170f)); if (v != t.Sits[i]) { t.Sits[i] = v; _dirty = true; } }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("«Пара» — щось разом з іншим вороном (режисер іноді обирає замість власної справи). Шаблон без жодної ситуації грає лише як відповідь.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Настрій:", GUILayout.Width(60f));
            for (int i = 0; i < 4; i++) { bool v = GUILayout.Toggle(t.Moods[i], " " + SitTemplates.MoodNames[i]); if (v != t.Moods[i]) { t.Moods[i] = v; _dirty = true; } }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Кому:", GUILayout.Width(50f));
            int only = GUILayout.Toolbar(t.Only, OnlyNames);
            if (only != t.Only) { t.Only = only; _dirty = true; }
            GUILayout.EndHorizontal();
            t.WeightH = Slide("Як часто: Хугін (вага)", t.WeightH, 0f, 5f, 0.05f, true);
            t.WeightM = Slide("Як часто: Мунін (вага)", t.WeightM, 0f, 5f, 0.05f, true);
            t.Prio = Mathf.RoundToInt(Slide("Пріоритет (вищий перебиває)", t.Prio, 1f, 9f, 1f, true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пасує:", GUILayout.Width(60f));
            string suits = GUILayout.TextField(string.Join(",", t.Suits));
            if (suits != string.Join(",", t.Suits)) { System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>(); foreach (string id in suits.Split(',')) if (id.Trim().Length > 0) ids.Add(id.Trim().ToLowerInvariant()); t.Suits = ids.ToArray(); _dirty = true; }
            Flag(" лише їм", ref t.Need);
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Характери (вага ×2): " + Ids(true) + "\nПримхи (вага ×3): " + Ids(false) + "</color>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Відповідь іншого ворона:", GUILayout.Width(170f));
            string ans = GUILayout.TextField(t.Answer ?? "");
            if (ans != t.Answer) { t.Answer = ans; _dirty = true; }
            GUILayout.EndHorizontal();
            if (t.Answer.Length > 0)
            {
                if (SitTemplates.Find(t.Answer) == null) GUILayout.Label("<color=#FF7A7A>Немає шаблону з такою назвою.</color>");
                t.AnswerChance = Slide("Відповідає, шанс", t.AnswerChance, 0f, 1f, 0.05f, true);
                t.AnswerMin = Slide("Відповідає за, с: від", t.AnswerMin, 0f, 3f, 0.05f, true);
                t.AnswerMax = Slide("…до", Mathf.Max(t.AnswerMin, t.AnswerMax), 0f, 3f, 0.05f, true);
            }

            GUILayout.Space(4);
            GUILayout.Label("<b>Скільки</b>:");
            t.SecMin = Slide("Триває від, с (0 — як кліп)", t.SecMin, 0f, 10f, 0.05f, true);
            t.SecMax = Slide("…до, с", Mathf.Max(t.SecMin, t.SecMax), 0f, 10f, 0.05f, true);
            t.In = Slide("Вхід, с", t.In, 0.02f, 2f, 0.01f, true);
            t.Out = Slide("Вихід, с", t.Out, 0.02f, 2f, 0.01f, true);
            t.Rest = Slide("Не повторює раніше, ніж за, с", t.Rest, 0f, 120f, 1f, true);
            t.Vary = Slide("Щоразу інакше, ± частка", t.Vary, 0f, 0.5f, 0.01f, true);

            GUILayout.Space(4);
            GUILayout.Label("<b>Що ще</b>:");
            GUILayout.Label("Кліп гри під ним:");
            int clip = GUILayout.SelectionGrid(t.Clip + 1, SitTemplates.ClipNames, 3) - 1;
            if (clip != t.Clip) { t.Clip = clip; _dirty = true; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Дивиться:", GUILayout.Width(70f));
            int look = GUILayout.Toolbar(t.Look, SitTemplates.LookNames);
            if (look != t.Look) { t.Look = look; _dirty = true; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Flag(" Струшується", ref t.Shake); Flag(" Крок убік", ref t.Step); Flag(" Нахил у будь-який бік", ref t.Mirror);
            GUILayout.EndHorizontal();
            t.Tremble = Slide("Тремтіння крил, °", t.Tremble, 0f, 10f, 0.1f, true);
            t.TrembleHz = Slide("Тремтіння, Гц", t.TrembleHz, 1f, 15f, 0.5f, true);
            t.Flap = Slide("Змахи на початку, с", t.Flap, 0f, 2f, 0.05f, true);
            t.KickPitch = Slide("Поштовх: назад(−)/вперед, °/с", t.KickPitch, -150f, 150f, 5f, true);
            t.KickRoll = Slide("Поштовх: вбік, °/с", t.KickRoll, -150f, 150f, 5f, true);
            t.KickDip = Slide("Поштовх: вниз(−), м/с", t.KickDip, -1f, 1f, 0.05f, true);
            t.BeakHz = Slide("Дзьоб клацає, Гц (0 — тримає)", t.BeakHz, 0f, 10f, 0.5f, true);
            t.CawH = Slide("Каркає: Хугін, шанс", t.CawH, 0f, 1f, 0.05f, true);
            t.CawM = Slide("Каркає: Мунін, шанс", t.CawM, 0f, 1f, 0.05f, true);
            t.CawPitchH = Slide("Голос Хугіна, висота", t.CawPitchH, 0.5f, 1.6f, 0.01f, true);
            t.CawPitchM = Slide("Голос Муніна, висота", t.CawPitchM, 0.5f, 1.6f, 0.01f, true);
            t.CawVolume = Slide("Гучність", t.CawVolume, 0f, 1f, 0.05f, true);

            GUILayout.Space(6);
            GUILayout.Label("<b>Поза</b> — у п'ять моментів (поверх усього; крила дзеркальні, «Внутрішнє» — додаток крилу біля голови):");
            GUILayout.BeginHorizontal();
            string[] keys = { "Початок", "¼", "½", "¾", "Кінець" };
            for (int k = 0; k < SitTemplate.Keys; k++) if (GUILayout.Toggle(_key == k, keys[k], "Button") && _key != k) { _key = k; if (_hold) _holdU = k / (float)(SitTemplate.Keys - 1); }
            GUILayout.EndHorizontal();
            _bone = GUILayout.SelectionGrid(Mathf.Clamp(_bone, 0, SitTemplate.Bones - 1), SitTemplate.BoneNames, 4);
            Vector3 pv = t.Pose[_key, _bone];
            bool wing = _bone < 6;
            if (_bone == 10) pv.x = Deg("відкритий, °", pv.x, 0f, 40f);
            else if (_bone == 11) pv.x = Deg("присід, % ×100", pv.x * 100f, -50f, 100f) / 100f;
            else
            {
                pv.x = Deg(wing ? "вперед-назад" : "нахил (+ вниз)", pv.x, -90f, 90f);
                pv.y = Deg(wing ? "вгору-вниз" : "поворот (+ вправо)", pv.y, -90f, 90f);
                pv.z = Deg(wing ? "поворот уздовж" : "крен (+ вправо)", pv.z, -90f, 90f);
            }
            if (pv != t.Pose[_key, _bone]) { t.Pose[_key, _bone] = pv; _dirty = true; }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Як попередній момент") && _key > 0) { for (int b = 0; b < SitTemplate.Bones; b++) t.Pose[_key, b] = t.Pose[_key - 1, b]; _dirty = true; }
            if (GUILayout.Button("Цю кістку на всі моменти")) { for (int k = 0; k < SitTemplate.Keys; k++) t.Pose[k, _bone] = t.Pose[_key, _bone]; _dirty = true; }
            if (GUILayout.Button("Скинути момент")) { for (int b = 0; b < SitTemplate.Bones; b++) t.Pose[_key, b] = Vector3.zero; _dirty = true; }
            GUILayout.EndHorizontal();
        }

        // the Norov ids a template may suit (its «Пасує»): characters or quirks
        private static string Ids(bool characters)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (characters) foreach (Norov.Character c in Norov.Characters) sb.Append(sb.Length > 0 ? ", " : "").Append(c.Id).Append(" — ").Append(c.Name);
            else foreach (Norov.Quirk q in Norov.QuirkList) sb.Append(sb.Length > 0 ? ", " : "").Append(q.Id).Append(" — ").Append(q.Name);
            return sb.ToString();
        }

        private void Flag(string name, ref bool v) { bool n = GUILayout.Toggle(v, name); if (n != v) { v = n; _dirty = true; } }

        private float Slide(string name, float v, float min, float max, float step, bool dirty)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(210f));
            float o = v;
            if (GUILayout.Button("−", GUILayout.Width(24f))) v = Mathf.Clamp(v - step, min, max);
            float sl = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(130f));
            if (Mathf.Abs(sl - v) > 1e-6f) v = Mathf.Clamp(Mathf.Round(sl / step) * step, min, max);
            if (GUILayout.Button("+", GUILayout.Width(24f))) v = Mathf.Clamp(v + step, min, max);
            GUILayout.Label(step < 0.1f ? v.ToString("0.00") : step < 1f ? v.ToString("0.0") : v.ToString("0"), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            if (dirty && Mathf.Abs(o - v) > 1e-6f) _dirty = true;
            return v;
        }

        private float Deg(string name, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(130f));
            if (GUILayout.Button("−5", GUILayout.Width(30f))) v -= 5f;
            if (GUILayout.Button("−", GUILayout.Width(24f))) v -= 1f;
            float sl = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(150f));
            if (Mathf.Abs(sl - v) > 1e-4f) v = Mathf.Round(sl);
            if (GUILayout.Button("+", GUILayout.Width(24f))) v += 1f;
            if (GUILayout.Button("+5", GUILayout.Width(30f))) v += 5f;
            v = Mathf.Clamp(v, min, max);
            GUILayout.Label(v.ToString("0"), GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
#endif

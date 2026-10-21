#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    internal sealed class TakeoffPanel : MonoBehaviour
    {
        private static readonly string[] Groups = { "Ліве крило", "Праве крило", "Тулуб", "Ліва лапа", "Права лапа" };
        private static readonly int[] GroupStart = { 0, 23, 3, 7, 15 };
        private static readonly int[] GroupCount = { 3, 3, 4, 8, 8 };
        private int _group, _bone, _key;
        private bool _play;
        private float _phase, _speed = 0.8f;
        private string _note = "";
        private Vector2 _scroll;
        private bool _munin;

        internal void Closed()
        {
            enabled = false;
            _play = false;
            FlightStage.TakeoffPreview = false;
            FlightStage.Close();
        }

        private void Update()
        {
            if (_play)
            {
                _phase = Mathf.Repeat(_phase + Time.unscaledDeltaTime * _speed * TakeoffKeys.Keys / Mathf.Max(0.3f, TakeoffKeys.Duration), TakeoffKeys.Keys);
                _key = Mathf.Clamp(Mathf.RoundToInt(_phase) % TakeoffKeys.Keys, 0, TakeoffKeys.Keys - 1);
            }
        }

        private void LateUpdate()
        {
            FlightStage.TakeoffPreview = true;
            FlightStage.TakeoffPreviewPhase = _play ? _phase : _key;
            FlightStage.Show(_munin);
        }

        internal void Body()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(430f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_munin, "Хугін", "Button")) _munin = false;
            if (GUILayout.Toggle(_munin, "Мунін", "Button")) _munin = true;
            GUILayout.EndHorizontal();

            Rect pic = GUILayoutUtility.GetRect(420f, 420f, GUILayout.Width(420f), GUILayout.Height(420f));
            if (FlightStage.Texture != null) GUI.DrawTexture(pic, FlightStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, FlightStage.Problem.Length > 0 ? FlightStage.Problem : "…");
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition))
            {
                FlightStage.Yaw += e.delta.x * 0.6f;
                FlightStage.Pitch = Mathf.Clamp(FlightStage.Pitch + e.delta.y * 0.4f, -80f, 80f);
                e.Use();
            }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition))
            {
                FlightStage.Zoom = Mathf.Clamp(FlightStage.Zoom - e.delta.y * 0.05f, 0.5f, 4f);
                e.Use();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { FlightStage.Yaw = 0f; FlightStage.Pitch = 5f; }
            if (GUILayout.Button("Збоку")) { FlightStage.Yaw = 90f; FlightStage.Pitch = 5f; }
            if (GUILayout.Button("Ззаду")) { FlightStage.Yaw = 180f; FlightStage.Pitch = 5f; }
            if (GUILayout.Button("Згори")) FlightStage.Pitch = 80f;
            GUILayout.EndHorizontal();
            GUILayout.Label("Тягни картинку — крутити, коліщатко — ближче/далі.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_play ? "⏸ Пауза" : "▶ Грати по колу", GUILayout.Width(180f)))
            {
                _play = !_play;
                if (_play) _phase = _key;
            }
            _speed = GUILayout.HorizontalSlider(_speed, 0.2f, 1.5f, GUILayout.Width(150f));
            GUILayout.Label("×" + _speed.ToString("0.0"), GUILayout.Width(45f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тривалість зльоту", GUILayout.Width(125f));
            float duration = GUILayout.HorizontalSlider(TakeoffKeys.Duration, 0.3f, 2f, GUILayout.Width(170f));
            if (Mathf.Abs(duration - TakeoffKeys.Duration) > 0.01f) { TakeoffKeys.Duration = Mathf.Round(duration * 20f) / 20f; _play = false; }
            GUILayout.Label(TakeoffKeys.Duration.ToString("0.00") + " с");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            TakeoffKeys.Active = GUILayout.Toggle(TakeoffKeys.Active, TakeoffKeys.Active
                ? "У старті з плеча: МОЇ 5 КАДРІВ (натисни — вимкнути)"
                : "У старті з плеча: вбудована анімація (натисни — мої кадри)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти"))
            {
                try { TakeoffKeys.Save(); _note = "Збережено в config/RavenWatch/takeoffkeys.txt."; }
                catch (System.IO.IOException ex) { SaveFailed(ex); }
                catch (System.UnauthorizedAccessException ex) { SaveFailed(ex); }
                catch (System.Security.SecurityException ex) { SaveFailed(ex); }
                catch (System.ArgumentException ex) { SaveFailed(ex); }
            }
            if (GUILayout.Button("Скинути все")) { TakeoffKeys.Reset(); _play = false; _note = "Кадри обнулено й вимкнено. Натисни «Зберегти», щоб записати це."; }
            GUILayout.EndHorizontal();
            GUILayout.Label(_note.Length > 0 ? _note : "У грі кадри проходяться один раз під час зльоту; у прев'ю — по колу.");

            GUILayout.Label("Кадр зльоту:");
            GUILayout.BeginHorizontal();
            for (int k = 0; k < TakeoffKeys.Keys; k++)
            {
                if (GUILayout.Toggle(_key == k && !_play, (k + 1).ToString(), "Button"))
                {
                    if (_key != k || _play) { _key = k; _phase = k; _play = false; }
                }
            }
            if (GUILayout.Button("Тест X кістки", GUILayout.Width(120f)))
            {
                for (int k = 0; k < TakeoffKeys.Keys; k++)
                {
                    Vector3 pose = TakeoffKeys.Pose[k, _bone];
                    pose.x = Mathf.Sin(k * Mathf.PI * 2f / TakeoffKeys.Keys) * 22f;
                    TakeoffKeys.Pose[k, _bone] = pose;
                }
                _play = false;
                _phase = _key;
                _note = "Тестові кути X записано для вибраної кістки в усі 5 кадрів.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Кістки:");
            GUILayout.BeginHorizontal();
            for (int g = 0; g < Groups.Length; g++)
            {
                if (GUILayout.Toggle(_group == g, Groups[g], "Button") && _group != g)
                {
                    _group = g;
                    _bone = GroupStart[g];
                    _play = false;
                }
            }
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < GroupCount[_group]; i++)
            {
                int index = GroupStart[_group] + i;
                if (GUILayout.Toggle(_bone == index, TakeoffKeys.BoneLabels[index], "Button") && _bone != index)
                {
                    _bone = index;
                    _play = false;
                }
            }
            ShoulderLife previewLife = FlightStage.Life;
            if (previewLife != null)
                GUILayout.Label(previewLife.HasTakeoffBone(_bone) ? "Кістку знайдено у моделі прев'ю." : "У моделі прев'ю такої кістки немає; її значення не застосовуються.");
            bool boneHasPose = false;
            for (int k = 0; k < TakeoffKeys.Keys; k++)
                if (TakeoffKeys.Pose[k, _bone].sqrMagnitude > 0.01f) { boneHasPose = true; break; }
            if (!boneHasPose) GUILayout.Label("У цієї кістки всі кадри нульові: вибір 1–5 нічого не змінить. Задай кути або натисни «Тест X кістки».");
            GUILayout.Space(6f);
            Vector3 value = TakeoffKeys.Pose[_key, _bone];
            value.x = Row("нахил уперед-назад", value.x);
            value.y = Row("поворот вгору-вниз", value.y);
            value.z = Row("поворот уздовж кістки", value.z);
            if (TakeoffKeys.Pose[_key, _bone] != value) _play = false;
            TakeoffKeys.Pose[_key, _bone] = value;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Копія з попереднього кадру"))
            {
                int previous = (_key + TakeoffKeys.Keys - 1) % TakeoffKeys.Keys;
                for (int b = 0; b < TakeoffKeys.BoneNames.Length; b++) TakeoffKeys.Pose[_key, b] = TakeoffKeys.Pose[previous, b];
                _play = false;
            }
            if (GUILayout.Button("Скинути вибрану кістку"))
            {
                for (int k = 0; k < TakeoffKeys.Keys; k++) TakeoffKeys.Pose[k, _bone] = Vector3.zero;
                _play = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static float Row(string name, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(155f));
            if (GUILayout.Button("−5", GUILayout.Width(30f))) value -= 5f;
            if (GUILayout.Button("−", GUILayout.Width(24f))) value -= 1f;
            value = GUILayout.HorizontalSlider(value, -90f, 90f, GUILayout.Width(130f));
            if (GUILayout.Button("+", GUILayout.Width(24f))) value += 1f;
            if (GUILayout.Button("+5", GUILayout.Width(30f))) value += 5f;
            value = Mathf.Clamp(Mathf.Round(value), -90f, 90f);
            GUILayout.Label(value.ToString("0") + "°", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return value;
        }

        private void SaveFailed(System.Exception ex)
        {
            _note = "Помилка збереження: " + ex.Message;
            if (Plugin.Instance != null) Plugin.Instance.Log(_note);
            else Debug.LogError(_note);
        }
    }
}
#endif

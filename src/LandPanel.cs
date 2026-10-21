#if RAVEN_ITEM
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The «Посадка» tab of the test window (DevPanel): the landing tuning. It sends a shoulder raven home from a few
    // metres off (behind, beside, ahead, above), slows the game down, holds the landing still and steps it by hand,
    // and nudges the bird at the moment shown. «Зберегти» writes it all to the config line "Landing tuning" (and the log).
    internal class LandPanel : MonoBehaviour
    {
        private Vector2 _scroll;
        private string _note = "";
        private bool _munin;
        private float _dist = 5f;
        private float _slow = 1f;
        private float _stepUntil;

        internal static BepInEx.Configuration.ConfigEntry<string> Saved;

        // the test window closed: the game at its own speed again, no landing held
        internal void Closed()
        {
            SetSlow(1f);
            ShoulderLife.LandHold = false;
        }

        private static readonly System.Reflection.FieldInfo GameTimeScale = AccessTools.Field(typeof(Game), "m_timeScale");
        private void SetSlow(float v)
        {
            _slow = v;
            try { if (GameTimeScale != null && GameTimeScale.IsStatic) GameTimeScale.SetValue(null, v); } catch { }
            Time.timeScale = v;
        }

        private void Update()
        {
            if (!DevPanel.IsOpen) return;
            // the game may set it back; a step runs at full speed for a moment, then stops again
            float want = Time.realtimeSinceStartup < _stepUntil ? 1f : _slow;
            if (want < 0.999f || _stepUntil > 0f) { Time.timeScale = want; if (Time.realtimeSinceStartup >= _stepUntil) _stepUntil = 0f; }
        }

        private ShoulderLife Life() { return Shoulder.LifeOf(Player.m_localPlayer, _munin); }

        internal void Body()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_munin, " Хугін", "Button")) _munin = false;
            if (GUILayout.Toggle(_munin, " Мунін", "Button")) _munin = true;
            GUILayout.EndHorizontal();

            Player meP = Player.m_localPlayer;
            ShoulderLife lf = meP != null ? Shoulder.LifeOf(meP, _munin) : null;
            GUILayout.Label("Остання посадка: " + (lf != null ? lf.LandInfo : "—"));
            GUILayout.Label("Останній зліт: " + (lf != null && lf.Motor != null && lf.Motor.TakeoffNow != null ? lf.Motor.TakeoffNow.Describe() : "—"));
            _dist = Row("Звідки, м", _dist, 2f, 15f, 0.5f);
            GUILayout.Label("Пустити на посадку:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ззаду")) Send(new Vector3(0f, 1.2f, -1f));
            if (GUILayout.Button("Збоку")) Send(new Vector3(_munin ? -1f : 1f, 1.2f, 0f));
            if (GUILayout.Button("Спереду")) Send(new Vector3(0f, 1.2f, 1f));
            if (GUILayout.Button("Згори")) Send(new Vector3(0.2f * (_munin ? -1f : 1f), 2.5f, -0.3f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Записати кадр за кадром (звичайна швидкість гри):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ззаду")) Record("back", new Vector3(0f, 1.2f, -1f));
            if (GUILayout.Button("Збоку")) Record("side", new Vector3(_munin ? -1f : 1f, 1.2f, 0f));
            if (GUILayout.Button("Спереду")) Record("front", new Vector3(0f, 1.2f, 1f));
            if (GUILayout.Button("Згори")) Record("above", new Vector3(0.2f * (_munin ? -1f : 1f), 2.5f, -0.3f));
            if (GUILayout.Button("Приліт")) RecordFlyIn();
            GUILayout.EndHorizontal();
            GUILayout.Label("Прогулянки й стан воронів у повітрі — у вкладці «Політ».");
            if (GUILayout.Button("Записати: сидять на плечах, обидва (20 с)"))
            {
                Player p = Player.m_localPlayer;
                int n = 0;
                if (p != null && Shoulder.ModelOf(p, false) != null) { FlightRec.Start(p, false, "idle", false, 20f); n++; }
                if (p != null && Shoulder.ModelOf(p, true) != null) { FlightRec.Start(p, true, "idle", false, 20f); n++; }
                _note = n > 0 ? "Пишу 20 с (" + n + "): стій, ходи, крутись — як зазвичай." : "На плечах нікого.";
            }
            GUILayout.Label(FlightRec.Busy ? "Іде запис…" : FlightRec.Last.Length > 0 ? "Останній запис: " + FlightRec.Last : "");
            float slow = Row("Швидкість гри (0 = стоп)", _slow, 0f, 1f, 0.05f);
            if (Mathf.Abs(slow - _slow) > 0.001f) SetSlow(slow);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Стоп")) SetSlow(0f);
            if (GUILayout.Button("▶ Пуск")) SetSlow(1f);
            if (GUILayout.Button("▶ крок 0.1 с")) _stepUntil = Time.realtimeSinceStartup + 0.1f;
            if (GUILayout.Button("Повільно 0.25")) SetSlow(0.25f);
            if (GUILayout.Button("Звичайно")) SetSlow(1f);
            GUILayout.EndHorizontal();
            int mi = _munin ? 1 : 0;
            float nd = RavenFlight.NowDist[mi];
            GUILayout.Label(nd >= 0f ? (RavenFlight.NowFinal[mi] ? "Фінальний захід" : "Летить додому") + ": до плеча " + nd.ToString("0.00") + " м, швидкість " + RavenFlight.NowSpeed[mi].ToString("0.0") + " м/с" : "");

            ShoulderLife life = Life();
            bool landing = life != null && life.Landing;
            GUILayout.Label(landing ? "Посадка: кадр " + Mathf.RoundToInt(life.LandU * 100f) + "%" + (ShoulderLife.LandHold ? " (пауза)" : "") : "Ворон не сідає зараз" + (ShoulderLife.LandHold ? " — пауза ввімкнеться на наступній посадці" : ""));
            GUILayout.BeginHorizontal();
            ShoulderLife.LandHold = GUILayout.Toggle(ShoulderLife.LandHold, ShoulderLife.LandHold ? " Пауза ✓" : " Пауза", "Button");
            if (GUILayout.Button("◀ кадр")) ShoulderLife.LandNudge = -0.04f;
            if (GUILayout.Button("кадр ▶")) ShoulderLife.LandNudge = 0.04f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            // the nudge for this moment: on the way in by distance (4/2/1/0.5 m), or in the landing itself by its %
            int k = landing ? Mathf.Clamp(Mathf.RoundToInt(life.LandU * 4f), 0, 4) : -1;
            int f = -1;
            if (!landing && nd >= 0f && nd < 6f && RavenFlight.NowFinal[mi])
            {
                float best = float.MaxValue;
                for (int i = 0; i < RavenFlight.FixAt.Length; i++) { float g = Mathf.Abs(RavenFlight.FixAt[i] - nd); if (g < best) { best = g; f = i; } }
            }
            if (k >= 0) GUILayout.Label("Поправка: посадка, точка " + (k * 25) + "% (см):");
            else if (f >= 0) GUILayout.Label("Поправка: захід, точка " + RavenFlight.FixAt[f] + " м від плеча (см):");
            else GUILayout.Label("Поправка: з'явиться на фінальному заході (ближче 6 м) і на посадці. Найзручніше — на стоп-кадрі.");
            if (k >= 0 || f >= 0)
            {
                Vector3 v = k >= 0 ? ShoulderPin.LandFixes[k] : RavenFlight.FlyFixes[f];
                v.x = Row("Вправо", v.x * 100f, -100f, 100f, 1f) / 100f;
                v.y = Row("Вгору", v.y * 100f, -100f, 100f, 1f) / 100f;
                v.z = Row("Вперед", v.z * 100f, -100f, 100f, 1f) / 100f;
                if (k >= 0) ShoulderPin.LandFixes[k] = v; else RavenFlight.FlyFixes[f] = v;
            }
            if (GUILayout.Button("Скинути поправки")) { for (int i = 0; i < 5; i++) ShoulderPin.LandFixes[i] = Vector3.zero; for (int i = 0; i < 4; i++) RavenFlight.FlyFixes[i] = Vector3.zero; }

            GUILayout.Space(6);
            GUILayout.Label("Перехід на ванільну посадку:");
            FlightBody.LandReach = Row("З відстані, м", FlightBody.LandReach, 0.2f, 3f, 0.05f);
            FlightBody.LandAbove = Row("Над плечем, м", FlightBody.LandAbove, 0f, 1.5f, 0.05f);
            FlightBody.LandBehind = Row("Позаду плеча, м", FlightBody.LandBehind, -1f, 1.5f, 0.05f);
            FlightBody.LandOut = Row("Назовні від шиї, м", FlightBody.LandOut, -0.5f, 1f, 0.05f);
            FlightBody.BrakeGain = Row("М'якість гальмування", FlightBody.BrakeGain, 0.5f, 6f, 0.1f);
            FlightBody.HoverBeat = Row("Змахи при зависанні ×", FlightBody.HoverBeat, 0.5f, 2.5f, 0.05f);
            FlightBody.HoverStill = Row("Сідає, коли швидкість <", FlightBody.HoverStill, 0.2f, 4f, 0.1f);
            FlightBody.HoverTurned = Row("…і розвернутий до, °", FlightBody.HoverTurned, 3f, 60f, 1f);
            float lead = Row("Кліп до торкання (наст.)", ShoulderLife.LandLead, 0.2f, 1.2f, 0.05f);
            float after = Row("Кліп після (наст.)", ShoulderLife.LandAfter, 0.1f, 3f, 0.05f);
            if (Mathf.Abs(lead - ShoulderLife.LandLead) > 0.001f || Mathf.Abs(after - ShoulderLife.LandAfter) > 0.001f) { ShoulderLife.LandLead = lead; ShoulderLife.LandAfter = after; ShoulderLife.ReLand(); }
            ShoulderLife.LandSpeed = Row("Темп кліпу", ShoulderLife.LandSpeed, 0.3f, 2f, 0.05f);
            ShoulderPin.LandTurnRate = Row("Доворот (× шляху)", ShoulderPin.LandTurnRate, 0.5f, 4f, 0.1f);
            ShoulderLife.BlendLand = Row("Злиття поз, с", ShoulderLife.BlendLand, 0.05f, 1f, 0.05f);

            GUILayout.Space(6);
            GUILayout.Label("Захід перед посадкою:");
            RavenFlight.TurnFrom = Row("Розвертатись з, м", RavenFlight.TurnFrom, 1f, 15f, 0.5f);
            RavenFlight.FlareDistance = Row("Гальмувати з, м", RavenFlight.FlareDistance, 0.5f, 6f, 0.25f);
            FlightBody.ApproachSide = Row("Бокова точка: вбік", FlightBody.ApproachSide, 0f, 4f, 0.1f);
            FlightBody.ApproachBack = Row("Бокова точка: назад", FlightBody.ApproachBack, -2f, 4f, 0.1f);
            FlightBody.ApproachUp = Row("Бокова точка: вгору", FlightBody.ApproachUp, 0f, 3f, 0.1f);

            GUILayout.EndScrollView();
            if (_note.Length > 0) GUILayout.Label(_note);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути все")) { Unpack(Defaults); ShoulderLife.ReLand(); _note = "Повернуто вбудоване."; }
            if (GUILayout.Button("Зберегти"))
            {
                string line = Pack();
                if (Saved != null) Saved.Value = line;
                if (Plugin.Instance != null) Plugin.Instance.Log("Landing tuning saved: " + line);
                _note = "Збережено в конфіг (і в лог).";
            }
            GUILayout.EndHorizontal();
        }

        // dir: in the owner's frame (x right, y up, z forward), scaled to the distance
        private void Send(Vector3 dir)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            Quaternion facing = Quaternion.LookRotation(fwd, Vector3.up);
            Vector3 from = p.transform.position + Vector3.up * 1.6f + facing * (new Vector3(dir.x, 0f, dir.z).normalized * _dist) + Vector3.up * dir.y;
            if (dir.x == 0f && dir.z == 0f) from = p.transform.position + Vector3.up * (1.6f + _dist);
            _note = StrikeFlight.TestHome(p, _munin, from) ? "Летить з " + _dist + " м." : (_munin ? "Мунін" : "Хугін") + " не на плечі (або вже в польоті).";
        }

        private void Record(string label, Vector3 dir)
        {
            SetSlow(1f);
            Player p = Player.m_localPlayer;
            if (p == null || Shoulder.ModelOf(p, _munin) == null) { _note = (_munin ? "Мунін" : "Хугін") + " не на плечі."; return; }
            FlightRec.Start(p, _munin, label, false);
            Send(dir);
        }

        private void RecordFlyIn()
        {
            SetSlow(1f);
            Player p = Player.m_localPlayer;
            if (p == null || Shoulder.ModelOf(p, _munin) == null) { _note = (_munin ? "Мунін" : "Хугін") + " не на плечі."; return; }
            FlightRec.Start(p, _munin, "flyin", true);
            _note = Shoulder.Respawn(p, _munin) ? "Прилітає знову, як при вході." : "Не вдалося.";
        }

        private static float Row(string name, float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(150f));
            if (GUILayout.Button("−", GUILayout.Width(26f))) v -= step;
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(120f));
            if (GUILayout.Button("+", GUILayout.Width(26f))) v += step;
            v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
            GUILayout.Label(step < 1f ? v.ToString("0.00") : v.ToString("0"), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            return v;
        }

        // reach;lead;after;speed;turn;blend;turnFrom;flare;side;back;up; then 5 nudges x,y,z
        internal static readonly string Defaults = "0.45;0.5;3;1;1.4;0.35;6;3;1.6;0.3;0.7;0;0;0;0;0;0;0;0;0;0;0;0;0;0;0";

        internal static string Pack()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float[] v = { FlightBody.LandReach, ShoulderLife.LandLead, ShoulderLife.LandAfter, ShoulderLife.LandSpeed, ShoulderPin.LandTurnRate, ShoulderLife.BlendLand,
                RavenFlight.TurnFrom, RavenFlight.FlareDistance, FlightBody.ApproachSide, FlightBody.ApproachBack, FlightBody.ApproachUp };
            foreach (float f in v) sb.Append(f.ToString("0.###", c)).Append(';');
            for (int i = 0; i < 5; i++) { Vector3 x = ShoulderPin.LandFixes[i]; sb.Append(x.x.ToString("0.###", c)).Append(';').Append(x.y.ToString("0.###", c)).Append(';').Append(x.z.ToString("0.###", c)).Append(';'); }
            for (int i = 0; i < 4; i++) { Vector3 x = RavenFlight.FlyFixes[i]; sb.Append(x.x.ToString("0.###", c)).Append(';').Append(x.y.ToString("0.###", c)).Append(';').Append(x.z.ToString("0.###", c)).Append(i < 3 ? ";" : ""); }
            return sb.ToString();
        }

        internal static void Unpack(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            string[] p = line.Split(';');
            if (p.Length < 26) return;
            float[] v = new float[26];
            for (int i = 0; i < 26; i++) if (!float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return;
            FlightBody.LandReach = v[0]; ShoulderLife.LandLead = v[1]; ShoulderLife.LandAfter = v[2]; ShoulderLife.LandSpeed = v[3];
            ShoulderPin.LandTurnRate = v[4]; ShoulderLife.BlendLand = v[5]; RavenFlight.TurnFrom = v[6]; RavenFlight.FlareDistance = v[7];
            FlightBody.ApproachSide = v[8]; FlightBody.ApproachBack = v[9]; FlightBody.ApproachUp = v[10];
            for (int i = 0; i < 5; i++) ShoulderPin.LandFixes[i] = new Vector3(v[11 + i * 3], v[12 + i * 3], v[13 + i * 3]);
            for (int i = 0; i < 4; i++) RavenFlight.FlyFixes[i] = Vector3.zero;
            if (p.Length >= 38)
                for (int i = 0; i < 4; i++)
                {
                    float a, b, cc;
                    if (float.TryParse(p[26 + i * 3], NumberStyles.Float, CultureInfo.InvariantCulture, out a) && float.TryParse(p[27 + i * 3], NumberStyles.Float, CultureInfo.InvariantCulture, out b) && float.TryParse(p[28 + i * 3], NumberStyles.Float, CultureInfo.InvariantCulture, out cc))
                        RavenFlight.FlyFixes[i] = new Vector3(a, b, cc);
                }
        }
    }
}
#endif

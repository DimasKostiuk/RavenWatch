#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Розворот на жердині»: the raven turning round on its perch (PerchLife.TurnTo), as five key poses over its time.
    // Key 1 is how it sits now (its back to you, 180° still to turn), key 5 facing you (0°); keys 2-4 between, set by
    // hand in the test window (tab «Розворот») and saved to config/RavenWatch/perch_turn.txt (it overrides Builtin).
    // Between keys a smooth curve (Catmull-Rom), so no key shows as a jolt.
    internal static class PerchTurn
    {
        internal sealed class Key
        {
            internal float T;      // when, a share of the whole turn (0..1)
            internal float Yaw;    // ° still to turn (180 at key 1, 0 at key 5)
            internal float Lift;   // cm off the bar
            internal float Crouch; // % crouched (legs bent as to push off)
            internal float Wings;  // ° the wings open
            internal float Lean;   // ° the body leans forward (−: back)
            internal float Head;   // ° the head is turned ahead of the body (towards where it turns)
            internal Key Clone() { return new Key { T = T, Yaw = Yaw, Lift = Lift, Crouch = Crouch, Wings = Wings, Lean = Lean, Head = Head }; }
        }

        internal const int Count = 5;
        internal static float Seconds = BuiltinSeconds;
        internal static Key[] Keys = Builtin();
        private static readonly Key _sample = new Key();

        // the built-in keys: it walks round on the bar, step by step (not a hop, not a spin on the spot):
        //   1-2 the head goes first: it looks over its shoulder, settles a little into its legs;
        //   2-4 the steps (Steps): one foot up, a part of the turn on the other, down; then the other foot; the head
        //       still ahead of the body, the wings a touch loose for balance;
        //   4-5 the last step down, the head comes in line, it straightens up
        internal static Key[] Builtin()
        {
            return new[]
            {
                new Key { T = 0f,    Yaw = 180f, Lift = 0f, Crouch = 0f,  Wings = 0f, Lean = 0f, Head = 0f },
                new Key { T = 0.16f, Yaw = 176f, Lift = 0f, Crouch = 14f, Wings = 3f, Lean = 4f, Head = 50f },
                new Key { T = 0.5f,  Yaw = 96f,  Lift = 0f, Crouch = 16f, Wings = 6f, Lean = 3f, Head = 38f },
                new Key { T = 0.84f, Yaw = 14f,  Lift = 0f, Crouch = 12f, Wings = 3f, Lean = 2f, Head = 10f },
                new Key { T = 1f,    Yaw = 0f,   Lift = 0f, Crouch = 0f,  Wings = 0f, Lean = 0f, Head = 0f },
            };
        }
        internal const float BuiltinSeconds = 2.4f;

        // ---- the steps: the body goes round in steps, a foot up at each while it turns on the other, the weight over
        //      the standing leg; the head and the rest follow the keys at their own smooth pace
        internal static int StepCount = 6;                       // steps from StepFrom to StepTo of the turn
        internal static float StepFrom = 0.12f, StepTo = 0.9f;
        internal static float StepLift = 1f;                     // × StepFold: how high the foot comes up
        internal static float StepRoll = 6f;                     // ° the body leans over the standing leg
        internal static float StepDip = 0.1f;                    // a little deeper into the legs as the foot comes up
        internal static float StepWings = 5f;                    // ° the wings open a touch more while a foot is up
        internal static float StepYaw = 0.75f;                   // how much of each step's turn waits for the foot to be up (0: even)
        internal static readonly float[] StepFold = { -30f, 62f, -36f, 38f }; // the lifted leg: hip, knee, ankle, toes (folded: the foot comes up, the toes curl)
        internal static void ResetSteps() { StepCount = 6; StepFrom = 0.12f; StepTo = 0.9f; StepLift = 1f; StepRoll = 6f; StepDip = 0.1f; StepWings = 5f; StepYaw = 0.75f; }

        // at u: which foot is up (+1 the first, -1 the other, 0 none), how far (0..1, up and down within its step), and
        // the turn's own time for the body (it turns on mostly while a foot is up, never stops dead)
        internal static float Steps(float u, out int foot, out float up)
        {
            foot = 0; up = 0f;
            if (StepCount <= 0 || u <= StepFrom || u >= StepTo) return u;
            float len = (StepTo - StepFrom) / StepCount;
            float x = (u - StepFrom) / len;
            int i = Mathf.Min(StepCount - 1, (int)x);
            float s = Mathf.Clamp01(x - i);
            foot = i % 2 == 0 ? 1 : -1;
            up = Mathf.Sin(s * Mathf.PI);
            float e = s * s * (3f - 2f * s);
            return StepFrom + (i + Mathf.Lerp(s, e, StepYaw)) * len;
        }

        // the pose at u (0..1 of the turn)
        internal static Key Sample(float u)
        {
            Key[] k = Keys;
            u = Mathf.Clamp01(u);
            int i = 0;
            while (i < Count - 2 && u > k[i + 1].T) i++;
            Key a = k[i], b = k[i + 1];
            Key a0 = k[Mathf.Max(0, i - 1)], b1 = k[Mathf.Min(Count - 1, i + 2)];
            float span = Mathf.Max(0.0001f, b.T - a.T);
            float t = Mathf.Clamp01((u - a.T) / span);
            Key r = _sample; // one kept object: asked every frame while a raven turns
            r.T = u;
            r.Yaw = Cr(a0.Yaw, a.Yaw, b.Yaw, b1.Yaw, t);
            r.Lift = Cr(a0.Lift, a.Lift, b.Lift, b1.Lift, t);
            r.Crouch = Mathf.Max(0f, Cr(a0.Crouch, a.Crouch, b.Crouch, b1.Crouch, t));
            r.Wings = Mathf.Max(0f, Cr(a0.Wings, a.Wings, b.Wings, b1.Wings, t));
            r.Lean = Cr(a0.Lean, a.Lean, b.Lean, b1.Lean, t);
            r.Head = Cr(a0.Head, a.Head, b.Head, b1.Head, t);
            return r;
        }

        private static float Cr(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ---- the file

        internal static string FilePath { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "perch_turn.txt"); } }
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        internal static string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# RavenWatch — розворот ворона на жердині (вкладка «Розворот» тестового вікна). key: частка часу, поворот °, підйом см, присід %, крила °, нахил °, голова °\n");
            sb.Append("seconds ").Append(Seconds.ToString("0.###", C)).Append('\n');
            sb.Append("# steps: кроків, підйом лапи ×, вага на лапу °, присід на крок, крила на крок °, від, до, поворот чекає лапу\n");
            sb.Append("steps ").Append(StepCount).Append(' ').Append(StepLift.ToString("0.##", C)).Append(' ').Append(StepRoll.ToString("0.#", C)).Append(' ').Append(StepDip.ToString("0.##", C)).Append(' ')
              .Append(StepWings.ToString("0.#", C)).Append(' ').Append(StepFrom.ToString("0.##", C)).Append(' ').Append(StepTo.ToString("0.##", C)).Append(' ').Append(StepYaw.ToString("0.##", C)).Append('\n');
            foreach (Key k in Keys)
                sb.Append("key ").Append(k.T.ToString("0.###", C)).Append(' ').Append(k.Yaw.ToString("0.#", C)).Append(' ').Append(k.Lift.ToString("0.##", C)).Append(' ')
                  .Append(k.Crouch.ToString("0.#", C)).Append(' ').Append(k.Wings.ToString("0.#", C)).Append(' ').Append(k.Lean.ToString("0.#", C)).Append(' ').Append(k.Head.ToString("0.#", C)).Append('\n');
            return sb.ToString();
        }

        internal static bool Save()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); File.WriteAllText(FilePath, ToText(), new UTF8Encoding(false)); return true; }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perch turn save: " + e.Message); return false; }
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                List<Key> keys = new List<Key>();
                float secs = Seconds;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("seconds ")) { float sv; if (float.TryParse(line.Substring(8), NumberStyles.Float, C, out sv) && sv > 0f) secs = sv; continue; }
                    if (line.StartsWith("steps "))
                    {
                        string[] sw = line.Substring(6).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        float[] sv = new float[8];
                        for (int i = 0; i < 8 && i < sw.Length; i++) float.TryParse(sw[i], NumberStyles.Float, C, out sv[i]);
                        if (sw.Length >= 8)
                        {
                            StepCount = Mathf.Clamp(Mathf.RoundToInt(sv[0]), 0, 12); StepLift = sv[1]; StepRoll = sv[2]; StepDip = sv[3]; StepWings = sv[4];
                            StepFrom = Mathf.Clamp01(sv[5]); StepTo = Mathf.Clamp(sv[6], StepFrom + 0.05f, 1f); StepYaw = Mathf.Clamp01(sv[7]);
                        }
                        continue;
                    }
                    if (!line.StartsWith("key ")) continue;
                    string[] w = line.Substring(4).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length < 7) continue;
                    float[] v = new float[7];
                    for (int i = 0; i < 7; i++) float.TryParse(w[i], NumberStyles.Float, C, out v[i]);
                    keys.Add(new Key { T = v[0], Yaw = v[1], Lift = v[2], Crouch = v[3], Wings = v[4], Lean = v[5], Head = v[6] });
                }
                if (keys.Count != Count) return;
                Keys = keys.ToArray();
                Seconds = Mathf.Clamp(secs, 0.1f, 5f);
                Fix();
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perch turn load: " + e.Message); }
        }

        // key 1 at the start with its back to you, key 5 at the end facing you, the times in order
        internal static void Fix()
        {
            Keys[0].T = 0f; Keys[0].Yaw = 180f;
            Keys[Count - 1].T = 1f; Keys[Count - 1].Yaw = 0f;
            for (int i = 1; i < Count - 1; i++) Keys[i].T = Mathf.Clamp(Keys[i].T, Keys[i - 1].T + 0.01f, 0.99f - 0.01f * (Count - 2 - i));
        }

        internal static void Reset()
        {
            Keys = Builtin();
            Seconds = BuiltinSeconds;
            ResetSteps();
        }

        // ---- the test window: the tab's own picture (PerchTurnStage) and, on «Програти», the nearest real perched raven too;
        //      held at a moment, slowed down
        internal static float Hold = -1f;   // ≥ 0: the preview stands at this share of the turn
        internal static float Speed = 1f;   // the preview's pace
    }

    // the tab «Розворот» of the test window
    internal class PerchTurnPanel
    {
        private int _key = 1;
        private bool _left;
        private string _note = "";
        private Vector2 _scroll;
        private static readonly string[] Names = { "Коли (частка часу)", "Ще повернути (°)", "Підйом (см)", "Присід (%)", "Крила (°)", "Нахил тіла (°)", "Голова наперед (°)" };
        private static readonly float[] Steps = { 0.05f, 5f, 0.5f, 5f, 5f, 2f, 5f };

        internal void Body(float height)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(500f));
            Controls(height);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.BeginVertical();
            Picture();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // ---- the picture: the turn played round and round on a bar (PerchTurnStage)
        private Vector2 _down;
        private void Picture()
        {
            PerchTurnStage.Left = _left;
            if (Event.current.type == EventType.Repaint) PerchTurnStage.Render(); // one picture a frame
            float size = 480f;
            Rect pic = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (PerchTurnStage.Texture != null) GUI.DrawTexture(pic, PerchTurnStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, PerchTurnStage.Problem);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && pic.Contains(e.mousePosition)) _down = e.mousePosition;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition))
            {
                PerchTurnStage.Yaw += e.delta.x * 0.6f;
                PerchTurnStage.Pitch = Mathf.Clamp(PerchTurnStage.Pitch + e.delta.y * 0.4f, -60f, 85f);
                e.Use();
            }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { PerchTurnStage.Zoom = Mathf.Clamp(PerchTurnStage.Zoom - e.delta.y * 0.05f, 0.4f, 4f); e.Use(); }
            GUILayout.Label("<color=#9A9A9A>Тягни — крутити, коліщатко — ближче. Ворон розвертається по колу з паузою; темп, плече й «Тримати» — ліворуч.</color>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { PerchTurnStage.Yaw = 180f; PerchTurnStage.Pitch = 8f; }
            if (GUILayout.Button("Збоку")) { PerchTurnStage.Yaw = 90f; PerchTurnStage.Pitch = 8f; }
            if (GUILayout.Button("Згори")) { PerchTurnStage.Pitch = 80f; }
            if (GUILayout.Button(PerchTurnStage.MuninShown ? "Мунін" : "Хугін")) { PerchTurnStage.MuninShown = !PerchTurnStage.MuninShown; PerchTurnStage.Again(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            string poseName = PerchTurnStage.PoseFrom >= 0 && PerchTurnStage.PoseFrom < Perches.Defs.Count ? Perches.Defs[PerchTurnStage.PoseFrom].Name : "без пози (як у грі)";
            if (GUILayout.Button("Поза лап: " + poseName))
            {
                PerchTurnStage.PoseFrom++;
                if (PerchTurnStage.PoseFrom >= Perches.Defs.Count) PerchTurnStage.PoseFrom = -1;
                PerchTurnStage.Again();
            }
            GUILayout.EndHorizontal();
        }

        private void Controls(float height)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height));
            GUILayout.Label("Розворот ворона на жердині: 5 ключових поз. 1 — як сидить зараз (спиною до тебе, 180°), 5 — лицем до тебе (0°). 2–4 між ними: підбери сам. Між ключами — плавна крива.");
            GUILayout.Label("Праворуч — картинка. ▶ — розворот по колу; клік по ключу або зміна числа — пауза на цій позі (як на «Змаху крил»). На паузі повзунок проводить між позами.");
            GUILayout.Space(4);

            // play / pause, as the wing beat's tab
            if (!PerchTurnStage.Playing && PerchTurn.Hold < 0f) PerchTurn.Hold = PerchTurn.Keys[_key].T; // paused: a pose is shown
            GUILayout.BeginHorizontal();
            bool play = GUILayout.Toggle(PerchTurnStage.Playing, PerchTurnStage.Playing ? " ⏸ Пауза" : " ▶ Програти", "Button", GUILayout.Width(120f));
            if (play != PerchTurnStage.Playing)
            {
                PerchTurnStage.Playing = play;
                PerchTurn.Hold = play ? -1f : PerchTurn.Keys[_key].T;
                if (play) PerchTurnStage.Restart();
            }
            if (GUILayout.Button(_left ? "Через ліве плече" : "Через праве плече", GUILayout.Width(150f))) { _left = !_left; PerchTurnStage.Left = _left; PerchTurnStage.Restart(); }
            GUILayout.Label("Темп", GUILayout.Width(40f));
            PerchTurn.Speed = GUILayout.HorizontalSlider(PerchTurn.Speed, 0.1f, 1.5f, GUILayout.Width(110f));
            GUILayout.Label("×" + PerchTurn.Speed.ToString("0.0"), GUILayout.Width(40f));
            GUILayout.EndHorizontal();

            if (!PerchTurnStage.Playing)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Позиція", GUILayout.Width(60f));
                float h = GUILayout.HorizontalSlider(PerchTurn.Hold, 0f, 1f, GUILayout.Width(330f));
                if (Mathf.Abs(h - PerchTurn.Hold) > 0.0001f) PerchTurn.Hold = h;
                GUILayout.Label(Mathf.RoundToInt(PerchTurn.Hold * 100f) + "%");
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Програти ще й на справжній жердині поруч")) { _note = PerchLife.Preview(_left); }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Тривалість (с)", GUILayout.Width(150f));
            if (GUILayout.Button("−", GUILayout.Width(30f))) PerchTurn.Seconds = Mathf.Max(0.1f, PerchTurn.Seconds - 0.05f);
            GUILayout.Label(PerchTurn.Seconds.ToString("0.00"), GUILayout.Width(50f));
            if (GUILayout.Button("+", GUILayout.Width(30f))) PerchTurn.Seconds = Mathf.Min(5f, PerchTurn.Seconds + 0.05f);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ключ", GUILayout.Width(50f));
            for (int i = 0; i < PerchTurn.Count; i++)
                if (GUILayout.Toggle(i == _key && !PerchTurnStage.Playing, (i + 1).ToString(), "Button", GUILayout.Width(44f)) && (i != _key || PerchTurnStage.Playing))
                {
                    _key = i; // a key clicked: paused on it
                    PerchTurnStage.Playing = false;
                    PerchTurn.Hold = PerchTurn.Keys[i].T;
                }
            GUILayout.EndHorizontal();

            PerchTurn.Key k = PerchTurn.Keys[_key];
            bool ends = _key == 0 || _key == PerchTurn.Count - 1;
            for (int f = 0; f < Names.Length; f++)
            {
                if (ends && f <= 1) { GUILayout.Label(Names[f] + ": " + (f == 0 ? k.T.ToString("0.##") : k.Yaw.ToString("0")) + "  <color=#9A9A9A>(для першого й останнього ключа не змінюється)</color>"); continue; }
                GUILayout.BeginHorizontal();
                GUILayout.Label(Names[f], GUILayout.Width(150f));
                float v = Get(k, f);
                if (GUILayout.Button("−−", GUILayout.Width(34f))) v -= Steps[f] * 4f;
                if (GUILayout.Button("−", GUILayout.Width(30f))) v -= Steps[f];
                GUILayout.Label(v.ToString(f == 0 ? "0.00" : "0.#"), GUILayout.Width(50f));
                if (GUILayout.Button("+", GUILayout.Width(30f))) v += Steps[f];
                if (GUILayout.Button("++", GUILayout.Width(34f))) v += Steps[f] * 4f;
                bool changed = Mathf.Abs(v - Get(k, f)) > 0.0001f;
                Set(k, f, v);
                if (changed) { PerchTurnStage.Playing = false; PerchTurn.Hold = k.T; } // a change: paused on this key, to see it
                GUILayout.EndHorizontal();
            }
            PerchTurn.Fix();

            // the steps (all keys alike): how the feet go round
            GUILayout.Space(6);
            GUILayout.Label("<b>Кроки лапами</b> (лапа вгору, поворот на другій, вниз; по черзі)");
            float sc = PerchTurn.StepCount;
            Row("Кроків", ref sc, 1f, "0"); PerchTurn.StepCount = Mathf.Clamp(Mathf.RoundToInt(sc), 0, 12);
            Row("Лапу підняти (×)", ref PerchTurn.StepLift, 0.1f, "0.0");
            Row("Вага на лапу (°)", ref PerchTurn.StepRoll, 1f, "0");
            Row("Присід на крок", ref PerchTurn.StepDip, 0.02f, "0.00");
            Row("Крила на крок (°)", ref PerchTurn.StepWings, 1f, "0");
            Row("Поворот чекає лапу", ref PerchTurn.StepYaw, 0.05f, "0.00"); PerchTurn.StepYaw = Mathf.Clamp01(PerchTurn.StepYaw);
            Row("Кроки від (частка)", ref PerchTurn.StepFrom, 0.02f, "0.00"); PerchTurn.StepFrom = Mathf.Clamp(PerchTurn.StepFrom, 0f, 0.9f);
            Row("Кроки до (частка)", ref PerchTurn.StepTo, 0.02f, "0.00"); PerchTurn.StepTo = Mathf.Clamp(PerchTurn.StepTo, PerchTurn.StepFrom + 0.05f, 1f);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти", GUILayout.Width(110f))) _note = PerchTurn.Save() ? "Збережено: " + PerchTurn.FilePath : "Не вдалося зберегти.";
            if (GUILayout.Button("Відкрити збережене", GUILayout.Width(160f))) { PerchTurn.Load(); _note = "Прочитано з файлу."; }
            if (GUILayout.Button("Скинути до вбудованого", GUILayout.Width(180f))) { PerchTurn.Reset(); _note = "Вбудований розворот (ще не збережено)."; }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            if (_note.Length > 0) GUILayout.Label(_note);
        }

        private static void Row(string name, ref float v, float step, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(150f));
            if (GUILayout.Button("−", GUILayout.Width(30f))) v -= step;
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(50f));
            if (GUILayout.Button("+", GUILayout.Width(30f))) v += step;
            GUILayout.EndHorizontal();
        }

        private static float Get(PerchTurn.Key k, int f)
        {
            switch (f) { case 0: return k.T; case 1: return k.Yaw; case 2: return k.Lift; case 3: return k.Crouch; case 4: return k.Wings; case 5: return k.Lean; default: return k.Head; }
        }

        private static void Set(PerchTurn.Key k, int f, float v)
        {
            switch (f)
            {
                case 0: k.T = Mathf.Clamp01(v); break;
                case 1: k.Yaw = Mathf.Clamp(v, -90f, 270f); break;
                case 2: k.Lift = Mathf.Clamp(v, -5f, 30f); break;
                case 3: k.Crouch = Mathf.Clamp(v, 0f, 100f); break;
                case 4: k.Wings = Mathf.Clamp(v, 0f, 120f); break;
                case 5: k.Lean = Mathf.Clamp(v, -45f, 45f); break;
                default: k.Head = Mathf.Clamp(v, -120f, 120f); break;
            }
        }

        private static GUIStyle _bold;
        private static GUIStyle Bold
        {
            get
            {
                if (_bold == null) { _bold = new GUIStyle(GUI.skin.button); _bold.fontStyle = FontStyle.Bold; _bold.normal.textColor = new Color(1f, 0.83f, 0.3f); }
                return _bold;
            }
        }
    }
}
#endif

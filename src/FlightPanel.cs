#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The «Політ» tab of the test window (DevPanel): the ravens' motor (RavenMotor). What each raven is doing in
    // the air right now, how each kind flies (Hugin, Munin), each raven's own touch and trust, the easing between
    // aims, keeping clear of the ground and trees; buttons to send them up. Changes act at once, also in the air;
    // «Зберегти» writes config/RavenWatch/flight.txt.
    internal sealed class FlightPanel
    {
        private Vector2 _scroll;
        private string _note = "";
        private bool _munin;

        // the picture on the left (always in sight), everything to change on the right, scrolling on its own
        internal void Body(float h)
        {
            Player me = Player.m_localPlayer;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(FlightStage.Size + 20f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_munin, " Хугін", "Button")) _munin = false;
            if (GUILayout.Toggle(_munin, " Мунін", "Button")) _munin = true;
            GUILayout.EndHorizontal();
            Picture();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(h));
            Box(me);

            GUILayout.Label("<b>Зараз у повітрі</b>");
            Live(me, false);
            Live(me, true);

            GUILayout.Space(6);
            GUILayout.Label("<b>Тест у грі</b> (обраний ворон з твого плеча):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Слідом")) _note = Outing.Start(me, _munin, Outing.Follow) ? "Летить слідом." : Cannot();
            if (GUILayout.Button("Коло")) _note = Outing.Start(me, _munin, Random.value < 0.5f ? Outing.CircleRight : Outing.CircleLeft) ? "Кружляє." : Cannot();
            if (GUILayout.Button("Широке коло")) _note = Outing.Start(me, _munin, Random.value < 0.5f ? Outing.WideRight : Outing.WideLeft) ? "Кружляє широко." : Cannot();
            if (GUILayout.Button("Розвідка")) _note = Outing.Start(me, _munin, Outing.Scout) ? "Полетів уперед." : Cannot();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Обидва кружляють")) _note = Outing.StartBoth(me) ? "Обидва кружляють у різні боки." : "Потрібні обидва на плечах, не під дахом.";
            if (GUILayout.Button("Перехопити → додому")) Takeover(me);
            if (GUILayout.Button("Повернути")) { Outing.Recall(me); _note = "Повертаються."; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("На жердину")) _note = Perches.TestSit(me, _munin);
            if (GUILayout.Button("З жердини")) _note = Perches.TestBack(me, _munin);
            GUILayout.EndHorizontal();
            GUILayout.Label("«Перехопити» — як удар під час прогулянки: ворона в повітрі забирає інший політ і веде додому. Має летіти далі на своїй швидкості, без ривка й нового зльоту.");

            GUILayout.Space(6);
            FlightStyle s = RavenFlight.For(_munin);
            GUILayout.Label("<b>Як літає " + (_munin ? "Мунін" : "Хугін") + "</b> (основа для всіх " + (_munin ? "Мунінів" : "Хугінів") + "):");
            s.Cruise = Changed(Row("Крейсерська, м/с", s.Cruise, 3f, 20f, 0.5f), s.Cruise);
            s.Top = Changed(Row("Найбільша, м/с", s.Top, 5f, 30f, 0.5f), s.Top);
            s.Accel = Changed(Row("Розгін, м/с²", s.Accel, 4f, 40f, 0.5f), s.Accel);
            s.Turn = Changed(Row("Поворот, °/с", s.Turn, 60f, 400f, 5f), s.Turn);
            s.Bank = Changed(Row("Крен у віражі, °", s.Bank, 10f, 80f, 1f), s.Bank);
            s.Glide = Changed(Row("Планерує (частка)", s.Glide, 0f, 1f, 0.05f), s.Glide);
            s.Beat = Changed(Row("Темп змахів ×", s.Beat, 0.3f, 2f, 0.05f), s.Beat);

            GUILayout.Space(6);
            FlightCharacter ch = RavenFlight.CharOf(_munin);
            GUILayout.Label("<b>Характер " + (_munin ? "Муніна" : "Хугіна") + "</b> (на довірі 7; з довірою 1 — лише " + Mathf.RoundToInt(RavenFlight.TrustShows * 100f) + "% характеру, незграбний і смиканий):");
            ch.TurnCap = CharRow("Найрізкіший поворот, °/с", ch.TurnCap, 60f, 400f, 5f);
            ch.SharpCap = CharRow("…у кидку на здобич, °/с", ch.SharpCap, 60f, 500f, 5f);
            ch.TurnAccel = CharRow("Розгін повороту, °/с²", ch.TurnAccel, 50f, 1000f, 10f);
            ch.CurveMin = CharRow("Дуга розвороту: від, м", ch.CurveMin, 1f, 12f, 0.25f);
            ch.CurveMax = Mathf.Max(ch.CurveMin, CharRow("…до, м", ch.CurveMax, 1f, 15f, 0.25f));
            ch.BankEase = CharRow("Крен доганяє, 1/с", ch.BankEase, 0.5f, 15f, 0.25f);
            ch.PitchEase = CharRow("Ніс доганяє, 1/с", ch.PitchEase, 0.5f, 15f, 0.25f);
            ch.Bend = CharRow("Вигин: частка повороту", ch.Bend, 0.3f, 1f, 0.05f);
            ch.SwitchMul = CharRow("Шаблони: пауза ×", ch.SwitchMul, 0.2f, 5f, 0.05f);
            ch.KeepAdd = CharRow("Шаблони: лишає той самий, ±%", ch.KeepAdd * 100f, -50f, 60f, 5f) / 100f;
            ch.TplSpeed = CharRow("Шаблон не більше: швидкість ×", ch.TplSpeed, 1f, 2f, 0.05f);
            ch.TplTurn = CharRow("…поворот ×", ch.TplTurn, 1f, 2.5f, 0.05f);
            ch.TplBank = CharRow("…крен ×", ch.TplBank, 1f, 2.5f, 0.05f);
            ch.TplBeat = CharRow("…змахи ×", ch.TplBeat, 1f, 2.5f, 0.05f);
            ch.TrickTempo = CharRow("Трюки: тривалість ×", ch.TrickTempo, 0.6f, 1.6f, 0.05f);
            ch.TrickSize = CharRow("Трюки: розмір ×", ch.TrickSize, 0.6f, 1.6f, 0.05f);
            ch.TrickVary = CharRow("Трюки: розкид щоразу, ±%", ch.TrickVary * 100f, 0f, 30f, 1f) / 100f;
            GUILayout.Label("Удар:");
            ch.StoopUp = CharRow("Заходить згори, м над ціллю", ch.StoopUp, 0f, 8f, 0.25f);
            ch.ArcSide = CharRow("Дуга вбік на підльоті, м", ch.ArcSide, 0f, 6f, 0.25f);
            ch.ReUp = CharRow("Після удару: вгору, м", ch.ReUp, 0f, 6f, 0.1f);
            ch.ReBack = CharRow("…назад, м", ch.ReBack, 0f, 6f, 0.1f);
            ch.BounceBack = CharRow("Відскок назад (частка швидкості)", ch.BounceBack, 0f, 1f, 0.05f);
            ch.BounceUp = CharRow("Відскок угору, м/с", ch.BounceUp, 0f, 10f, 0.25f);
            ch.StrikeSharp = CharRow("Різкість кидка", ch.StrikeSharp, 0.5f, 6f, 0.1f);
            GUILayout.Label("Посадка (пози — шаблони з галочкою «Посадка»):");
            ch.LandBackMin = CharRow("Захід: назад від, м", ch.LandBackMin, -1f, 3f, 0.1f); ch.LandBackMax = CharRow("…до, м", ch.LandBackMax, -1f, 3f, 0.1f);
            ch.LandUpMin = CharRow("Захід: вгору від, м", ch.LandUpMin, -1f, 3f, 0.1f); ch.LandUpMax = CharRow("…до, м", ch.LandUpMax, -1f, 3f, 0.1f);
            ch.LandSideMin = CharRow("Захід: вбік від ×", ch.LandSideMin, 0.2f, 2f, 0.05f); ch.LandSideMax = CharRow("…до ×", ch.LandSideMax, 0.2f, 2f, 0.05f);
            ch.FlareMin = CharRow("Гальмує з, м: від", ch.FlareMin, 0.5f, 6f, 0.1f); ch.FlareMax = CharRow("…до", ch.FlareMax, 0.5f, 6f, 0.1f);
            ch.ClipMin = CharRow("Темп посадки: від ×", ch.ClipMin, 0.5f, 1.6f, 0.02f); ch.ClipMax = CharRow("…до ×", ch.ClipMax, 0.5f, 1.6f, 0.02f);
            ch.LandTilt = CharRow("Крен крил при посадці, °", ch.LandTilt, 0f, 35f, 1f);
            ch.SettleNone = CharRow("Сівши: нічого (вага)", ch.SettleNone, 0f, 5f, 0.1f); ch.SettleHop = CharRow("…підскок", ch.SettleHop, 0f, 5f, 0.1f);
            ch.SettleFlap = CharRow("…змах", ch.SettleFlap, 0f, 5f, 0.1f); ch.SettleStep = CharRow("…крок убік", ch.SettleStep, 0f, 5f, 0.1f); ch.SettleHopFlap = CharRow("…підскок і змах", ch.SettleHopFlap, 0f, 5f, 0.1f);
            GUILayout.Label("Зліт:");
            ch.CrouchMin = CharRow("Присідає, с: від", ch.CrouchMin, 0.02f, 0.8f, 0.01f); ch.CrouchMax = CharRow("…до", ch.CrouchMax, 0.02f, 0.8f, 0.01f);
            ch.CrouchDepth = CharRow("Присідає на, см", ch.CrouchDepth * 100f, 0f, 8f, 0.5f) / 100f;
            ch.PushMul = CharRow("Поштовх ×", ch.PushMul, 0.4f, 2f, 0.05f);
            ch.VeerMax = CharRow("Відштовхується вбік до, °", ch.VeerMax, 0f, 60f, 1f);
            ch.Unfold = CharRow("Розкриття крила: лікоть за плечем, с", ch.Unfold, 0f, 0.3f, 0.01f);
            GUILayout.Label("Довіра 1 (зникає до 7):");
            ch.Wobble = CharRow("Хитається з лінії, м", ch.Wobble, 0f, 2f, 0.05f);
            ch.Twitch = CharRow("Смикається, °", ch.Twitch, 0f, 25f, 0.5f);
            RavenFlight.TrustShows = CharRow("Характеру на довірі 1, %", RavenFlight.TrustShows * 100f, 0f, 100f, 5f) / 100f;
            GUILayout.Label("<b>Пара</b> (обидва ворони одного гравця):");
            Landings.MaxHold = Row("Чекає своєї черги до, с", Landings.MaxHold, 0f, 3f, 0.05f);
            Landings.TogetherChance = Row("Майже разом, %", Landings.TogetherChance * 100f, 0f, 100f, 5f) / 100f;
            Landings.ApartMin = Row("Розрив посадок: від, с", Landings.ApartMin, 0f, 2f, 0.05f);
            Landings.ApartMax = Mathf.Max(Landings.ApartMin, Row("…до, с", Landings.ApartMax, 0f, 3f, 0.05f));
            Landings.LeaveApartMin = Row("Розрив зльотів: від, с", Landings.LeaveApartMin, 0f, 1.5f, 0.02f);
            Landings.LeaveApartMax = Mathf.Max(Landings.LeaveApartMin, Row("…до, с", Landings.LeaveApartMax, 0f, 2f, 0.02f));
            Landings.LandBeatHz = Row("Помахи в шаблонах посадки, Гц", Landings.LandBeatHz, 1f, 10f, 0.25f);
            RavenMotor.CrouchLean = Row("Нахил при присіданні, °", RavenMotor.CrouchLean, 0f, 25f, 1f);

            GUILayout.Space(6);
            GUILayout.Label("<b>Кожен ворон свій</b>:");
            RavenMotor.Spread = Changed(Row("Розкид характеру, %", RavenMotor.Spread * 100f, 0f, 30f, 1f) / 100f, RavenMotor.Spread);
            RavenMotor.NervesWeight = Changed(Row("Вплив довіри", RavenMotor.NervesWeight, 0f, 1f, 0.05f), RavenMotor.NervesWeight);
            GameObject model = me != null ? Shoulder.ModelOf(me, _munin) : null;
            RavenMotor mine = model != null ? RavenMotor.Of(model, _munin, me) : null;
            if (mine != null)
            {
                FlightStyle p = mine.StyleNow;
                GUILayout.Label("Твій " + (_munin ? "Мунін" : "Хугін") + " зараз: " + p.Cruise.ToString("0.0") + " / " + p.Top.ToString("0.0") + " м/с, розгін " + p.Accel.ToString("0") + ", поворот " + p.Turn.ToString("0") + "°/с, крен " + p.Bank.ToString("0") + "°, планерує " + p.Glide.ToString("0.00") + ", змахи ×" + p.Beat.ToString("0.00"));
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>Плавність</b>:");
            RavenMotor.BlendSeconds = Row("Перехід до нової цілі, с", RavenMotor.BlendSeconds, 0f, 2f, 0.05f);
            FlightBody.LaunchSeconds = Row("М'який зліт, с", FlightBody.LaunchSeconds, 0.2f, 3f, 0.1f);
            FlightBody.Coast = Row("Гальмує в польоті (частка розгону)", FlightBody.Coast, 0.1f, 1f, 0.05f);
            FlightBody.BendLimit = GUILayout.Toggle(FlightBody.BendLimit, FlightBody.BendLimit ? " Гальмує перед вигином ✓" : " Гальмує перед вигином", "Button");
            GUILayout.Label("Трюки й переліт на жердину (ведення «вручну»):");
            RavenMotor.DriveFollow = Row("Тримається лінії, 1/с", RavenMotor.DriveFollow, 2f, 20f, 0.5f);
            RavenMotor.DriveAccel = Row("Найсильніший ривок, м/с²", RavenMotor.DriveAccel, 20f, 200f, 5f);
            RavenMotor.DriveTurnIn = Row("Поворот на вході, с", RavenMotor.DriveTurnIn, 0.05f, 1.5f, 0.05f);

            GUILayout.Space(6);
            GUILayout.Label("<b>Приліт</b> (після входу у світ): з'являється ззаду, мить ванільного прильоту, далі мотор — пірнання, плавне гальмування, зависання над плечем, ванільна посадка.");
            ShoulderPin.FlyInUp = Row("Звідки: вгору, м", ShoulderPin.FlyInUp, 1f, 12f, 0.5f);
            ShoulderPin.FlyInBack = Row("Звідки: назад, м", ShoulderPin.FlyInBack, -4f, 12f, 0.5f);
            ShoulderPin.FlyInSide = Row("Звідки: вбік, м", ShoulderPin.FlyInSide, 0f, 10f, 0.5f);
            ShoulderPin.FlyInSpeed = Row("Швидкість до плеча, м/с", ShoulderPin.FlyInSpeed, 3f, 15f, 0.5f);
            ShoulderPin.ArriveWake = Row("Ванільний початок, с", ShoulderPin.ArriveWake, 0.1f, 1f, 0.05f);
            ShoulderPin.FlyInSeconds = Row("Темп ванільної дуги, с", ShoulderPin.FlyInSeconds, 0.6f, 3f, 0.1f);
            GUILayout.Label("Як гальмує над плечем і як сідає — у вкладці «Посадка» (захід, зависання, кліп посадки).");
            if (GUILayout.Button("▶ Прилетіти знову (" + (_munin ? "Мунін" : "Хугін") + ")"))
                _note = Shoulder.Respawn(me, _munin) ? "Прилітає, як при вході у світ." : "Не вийшло (не на плечі).";

            GUILayout.Space(6);
            GUILayout.Label("<b>Перешкоди</b> (земля, вода, стіни, дерева; не ближче " + RavenMotor.OwnerQuiet.ToString("0") + " м до тебе і не на посадці):");
            RavenMotor.AvoidOn = GUILayout.Toggle(RavenMotor.AvoidOn, RavenMotor.AvoidOn ? " Обходить ✓" : " Обходить", "Button");
            RavenMotor.LookAhead = Row("Дивиться наперед, с", RavenMotor.LookAhead, 0.2f, 2f, 0.05f);
            RavenMotor.PushStrength = Row("Відхиляється до, м", RavenMotor.PushStrength, 0.5f, 10f, 0.25f);
            RavenMotor.GroundClear = Row("Над землею й водою, м", RavenMotor.GroundClear, 0.5f, 5f, 0.1f);
            if (_dirty) { _dirty = false; RavenMotor.Version++; }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (_note.Length > 0) GUILayout.Label(_note);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути все")) { RavenMotor.ToBuilt(); _note = "Повернуто вбудоване (ще не збережено)."; }
            if (GUILayout.Button("Зберегти"))
            {
                try { RavenMotor.Save(); _note = "Збережено в flight.txt (і в лог)."; }
                catch (System.Exception e) { _note = "Не вдалося зберегти: " + e.Message; }
            }
            GUILayout.EndHorizontal();
        }

        // the raven flying the test route by the same motor and wings as in the game (FlightStage): every slider
        // shows here at once; drag to turn the view, wheel to zoom
        private void Picture()
        {
            GUILayout.Label("<b>Перегляд</b> — " + (_munin ? "твій Мунін" : "твій Хугін") + " летить тим самим мотором і крилами, що в грі:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FlightStage.RouteNames.Length; i++)
                if (GUILayout.Toggle((int)FlightStage.Path == i, FlightStage.RouteNames[i], "Button")) FlightStage.Path = (FlightStage.Route)i;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FlightStage.ViewNames.Length; i++)
                if (GUILayout.Toggle((int)FlightStage.Camera_ == i, FlightStage.ViewNames[i], "Button")) { if ((int)FlightStage.Camera_ != i) { FlightStage.Yaw = i == (int)FlightStage.View.Behind ? 0f : 90f; } FlightStage.Camera_ = (FlightStage.View)i; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect pic = GUILayoutUtility.GetRect(FlightStage.Size, FlightStage.Size, GUILayout.Width(FlightStage.Size), GUILayout.Height(FlightStage.Size));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (FlightStage.Show(_munin) && FlightStage.Texture != null) GUI.DrawTexture(pic, FlightStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, FlightStage.Problem.Length > 0 ? FlightStage.Problem : "…");
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition)) { FlightStage.Yaw += e.delta.x * 0.6f; FlightStage.Pitch = Mathf.Clamp(FlightStage.Pitch + e.delta.y * 0.4f, -80f, 85f); e.Use(); }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { FlightStage.Zoom = Mathf.Clamp(FlightStage.Zoom - e.delta.y * 0.05f, 0.3f, 5f); e.Use(); }
            FlightStage.SpeedRow();
            GUILayout.Label(FlightStage.Status());
            FlightStage.RouteSize = Row("Розмір маршруту, м", FlightStage.RouteSize, 3f, 20f, 0.5f);
            GUILayout.Label("Тягни мишкою — повернути вид, колесо — ближче/далі. Змінюй повзунки нижче й дивись одразу.");
            GUILayout.Space(6);
        }

        // ---- the black box: the last seconds saved (Num0 in the game or here), played back on the picture
        private string _bbNote = "";
        private void Box(Player me)
        {
            GUILayout.Label("<b>Чорна скринька</b> — " + BlackBox.Key + " у грі (або кнопка) зберігає останні " + BlackBox.Seconds.ToString("0") + " с польоту обох воронів: файл для аналізу й відтворення тут.");
            BlackBox.Capture cap = BlackBox.Last;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Зберегти останні " + BlackBox.Seconds.ToString("0") + " с")) { _note = BlackBox.Save(me); cap = BlackBox.Last; }
            GUILayout.Label(cap != null ? "Запис " + cap.When + (cap.File.Length > 0 ? " · " + System.IO.Path.GetFileName(cap.File) : "") : "Ще нічого не записано.");
            GUILayout.EndHorizontal();
            BlackBox.Track tr = cap != null ? cap.Tracks[_munin ? 1 : 0] : null;
            if (tr == null) { if (FlightStage.Replay != null) FlightStage.Replay = null; GUILayout.Space(6); return; }
            bool on = FlightStage.Replay != null;
            bool want = GUILayout.Toggle(on, on ? " ⏹ Відтворення на картинці (натисни — назад до польоту)" : " ▶ Відтворити на картинці (" + (_munin ? "Мунін" : "Хугін") + ", " + tr.Length.ToString("0.0") + " с)", "Button");
            if (want != on) { FlightStage.Replay = want ? tr : null; FlightStage.ReplayT = 0f; FlightStage.ReplayPlay = true; if (want && FlightStage.Camera_ == FlightStage.View.Wide) FlightStage.Camera_ = FlightStage.View.Orbit; }
            if (FlightStage.Replay != null && FlightStage.Replay != tr) FlightStage.Replay = tr; // the other raven picked
            if (FlightStage.Replay == null) { GUILayout.Space(6); return; }
            GUILayout.BeginHorizontal();
            GUILayout.Label(FlightStage.ReplayT.ToString("0.00") + " с", GUILayout.Width(52f));
            float t = GUILayout.HorizontalSlider(FlightStage.ReplayT, 0f, tr.Length);
            if (Mathf.Abs(t - FlightStage.ReplayT) > 1e-4f) { FlightStage.ReplayT = t; FlightStage.ReplayPlay = false; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            int i = tr.IndexAt(FlightStage.ReplayT);
            if (GUILayout.Button("◀ кадр")) { FlightStage.ReplayPlay = false; if (i > 0) FlightStage.ReplayT = tr.Frames[i - 1].T - tr.Frames[0].T; }
            if (GUILayout.Button(FlightStage.ReplayPlay ? "⏸ Пауза" : "▶ Грати")) FlightStage.ReplayPlay = !FlightStage.ReplayPlay;
            if (GUILayout.Button("кадр ▶")) { FlightStage.ReplayPlay = false; if (i >= 0 && i + 1 < tr.Frames.Count) FlightStage.ReplayT = tr.Frames[i + 1].T - tr.Frames[0].T + 1e-4f; }
            if (GUILayout.Toggle(FlightStage.ReplaySpeed < 0.5f, "×0.25", "Button")) FlightStage.ReplaySpeed = 0.25f;
            if (GUILayout.Toggle(FlightStage.ReplaySpeed >= 0.5f, "×1", "Button")) FlightStage.ReplaySpeed = 1f;
            GUILayout.EndHorizontal();
            BlackBox.Frame f = FlightStage.ReplayFrame;
            if (f != null)
                GUILayout.Label("Кадр " + (i + 1) + " з " + tr.Frames.Count + ": " + f.Phase + " · від плеча " + Mathf.RoundToInt(f.Pos.x * 100f) + "/" + Mathf.RoundToInt(f.Pos.y * 100f) + "/" + Mathf.RoundToInt(f.Pos.z * 100f) + " см (вправо/вгору/вперед) · " + f.Speed.ToString("0.0") + " м/с · змахи ×" + f.Flap.ToString("0.00") + " · гальмо " + f.Flare.ToString("0.00"));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Що не так:", GUILayout.Width(80f));
            _bbNote = GUILayout.TextField(_bbNote ?? "");
            if (GUILayout.Button("Дописати", GUILayout.Width(80f))) { string at = FlightStage.ReplayT.ToString("0.00") + " с (" + (_munin ? "Мунін" : "Хугін") + "): "; _note = BlackBox.Note(at + _bbNote) ? "Дописано в файл запису." : "Нема файлу запису."; if (_note.StartsWith("Дописано")) _bbNote = ""; }
            GUILayout.EndHorizontal();
            GUILayout.Label("Знайди кривий момент повзунком і кадрами, напиши, що не так, і «Дописати» — я прочитаю файл разом з цією приміткою.");
            GUILayout.Space(6);
        }

        private bool _dirty;
        // a style value moved: the personal styles are made again (bumped once per frame at the end)
        private float Changed(float now, float was) { if (Mathf.Abs(now - was) > 1e-5f) _dirty = true; return now; }
        // a character row: a change is taken by the ravens in the air at once (their character is made again)
        private static float CharRow(string name, float v, float min, float max, float step) { float n = Row(name, v, min, max, step); if (Mathf.Abs(n - v) > 1e-5f) RavenFlight.CharVersion++; return n; }

        private static string Cannot() { return "Не вийшло (не на плечі або під дахом)."; }

        private void Live(Player me, bool munin)
        {
            string name = munin ? "Мунін" : "Хугін";
            GameObject m = me != null ? Shoulder.ModelOf(me, munin) : null;
            if (m == null) m = me != null ? StrikeFlight.FlyerOf(me, munin) : null;
            if (m == null) { GUILayout.Label(name + ": не на плечі"); return; }
            RavenMotor mot = RavenMotor.Of(m, munin, me);
            string act = AnimDirector.Of(m).ToString();
            if (mot == null || !mot.Airborne) { GUILayout.Label(name + ": " + act + ", не в повітрі"); return; }
            FlightBody b = mot.Body;
            GUILayout.Label(name + ": " + act + " · «" + mot.Plan + "» · " + b.Speed.ToString("0.0") + " м/с · до цілі " + Vector3.Distance(m.transform.position, mot.Aim).ToString("0.0") + " м"
                + (b.Final ? " · фінальний захід" : "") + (mot.Avoiding > 0.05f ? " · обходить " + mot.Avoiding.ToString("0.0") + " м" : ""));
        }

        private void Takeover(Player me)
        {
            if (me == null) return;
            GameObject m = Shoulder.ModelOf(me, _munin);
            RavenMotor mot = m != null ? RavenMotor.Of(m, _munin, me) : null;
            if (mot == null || !mot.Airborne) { _note = "Спершу підніми його в повітря (Слідом / Коло / Розвідка)."; return; }
            float before = mot.Body.Speed;
            _note = StrikeFlight.TestHome(me, _munin, m.transform.position)
                ? "Перехоплено на " + before.ToString("0.0") + " м/с — далі " + mot.Body.Speed.ToString("0.0") + " м/с, летить додому."
                : "Не вийшло (вже в ударі чи куполі).";
        }

        private static float Row(string name, float v, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(170f));
            // only a touch rounds to the step (a value just shown stays exactly as it is, e.g. Hugin's beat 0.57)
            if (GUILayout.Button("−", GUILayout.Width(26f))) v = Mathf.Clamp(v - step, min, max);
            float sl = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(120f));
            if (Mathf.Abs(sl - v) > 1e-6f) v = Mathf.Clamp(Mathf.Round(sl / step) * step, min, max);
            if (GUILayout.Button("+", GUILayout.Width(26f))) v = Mathf.Clamp(v + step, min, max);
            GUILayout.Label(step < 1f ? v.ToString("0.00") : v.ToString("0"), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The test window's tab «Доручення» (raven errand ui): give yourself any errand by a click, see exactly what was
    // written into your character (raw), go there, pass a step, see the champions near and the errands' log. Also the
    // nest's waits, the runes' notches and gold, so the whole way errand → notches → rune → gold can be walked through.
    internal sealed class ErrandPanel
    {
        private Vector2 _scroll, _logScroll;
        private string _note = "";
        private bool _raw, _showAll = true;

        internal void Body(float height)
        {
            Player p = Player.m_localPlayer;
            if (p == null) { GUILayout.Label("Нема гравця."); return; }
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(200f, height - 190f)));
            Now(p);
            GUILayout.Space(8);
            Give(p);
            GUILayout.Space(8);
            NestAndRunes(p);
            GUILayout.Space(8);
            ChampionsNear(p);
            GUILayout.EndScrollView();
            if (_note.Length > 0) GUILayout.Label("<color=#FFD27A>" + _note + "</color>");
            GUILayout.Label("<b>Журнал доручень</b> (BepInEx лог: «Errands:»)");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(120f));
            for (int i = Errands.Recent.Count - 1; i >= 0; i--) GUILayout.Label("<size=11>" + Errands.Recent[i] + "</size>");
            GUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- what you have now
        private void Now(Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            GUILayout.Label("<b>" + p.GetPlayerName() + "</b> · id " + p.GetPlayerID() + " · етап " + Stage.Of(p) + " (босів) · руни " + d.Opened + "/" + RuneBook.All.Length
                + " · різа " + d.Notches + "/" + RuneBook.NotchesPerRune + " · золоті " + d.Gold + " (усього " + d.GoldEver + ") · шанс золотого " + Mathf.RoundToInt(RuneBook.GoldChance(d.Opened) * 100f) + "%");
            ErrandRun r = Errands.Of(p);
            if (r == null) { GUILayout.Label("<color=#9A9A9A>Доручення нема.</color>"); return; }
            GUILayout.Label("<b><color=#FFD27A>Зараз:</color></b> " + r.Id + (r.Def != null && r.Def.Golden ? " <color=#FFC94A>(золоте)</color>" : "") + " · від " + Plugin.RavenName(r.Munin)
                + " · ціна " + r.Price + " зарубок (прихована) · крок " + (r.Step + 1) + "/" + r.Steps.Length + " · видано " + Mathf.RoundToInt((float)(WorldTime.Now - r.Given)) + " с тому");
            GUILayout.Label(Errands.Describe(p, r));
            GUILayout.Label("Зона кроку: " + (Errands.InZone ? "<color=#9FD49F>ти в ній</color>" : "ти поза нею"));
            foreach (KeyValuePair<string, string> kv in r.Slot) GUILayout.Label("<size=11>слот " + kv.Key + " = " + kv.Value + "</size>");
            Vector3 at; float radius;
            bool where = r.Current != null && r.Current.Where(r, r.Step, out at, out radius);
            if (!where) GUILayout.Label("<color=#9A9A9A>У цього кроку нема місця на мапі (вбити, принести, зробити): телепортувати нікуди.</color>");
            if (r.Mods.Count > 0) GUILayout.Label("Модифікатори: <color=#FF8A7A>" + string.Join(", ", r.Mods.ToArray()) + "</color> (бонус +" + ErrandMods.Bonus(r) + ")");
            RiddleStep rs = r.Current as RiddleStep;
            Riddles.Riddle rd = rs != null ? rs.Of(r) : null;
            if (rd != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Відгадка (тест): <b>" + rd.Prefab + "</b> ×" + rd.Count + " — " + ErrandSteps.Item(rd.Prefab));
                if (GUILayout.Button("Дати відгадку в сумку", GUILayout.Width(170f))) _note = AddToBag(p, rd.Prefab, rd.Count);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пройти крок")) { _note = Errands.Cmd(p, new[] { "errand", "step" }); }
            if (GUILayout.Button("Виконати все")) { _note = Errands.Cmd(p, new[] { "errand", "done" }); }
            if (GUILayout.Button("Віддати ворону")) { _note = Errands.Cmd(p, new[] { "errand", "hand" }); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = where;
            if (GUILayout.Button("Показати на мапі")) { Vector3 a; float rr; r.Current.Where(r, r.Step, out a, out rr); if (Minimap.instance != null) Minimap.instance.ShowPointOnMap(a); }
            if (GUILayout.Button("Телепорт у зону")) { Vector3 a; float rr; r.Current.Where(r, r.Step, out a, out rr); ErrandMap.Teleport(p, a); }
            GUI.enabled = true;
            if (GUILayout.Button("Відкласти (без паузи)")) { Errands.Drop(p); _note = "Відкладено без денної паузи."; }
            if (GUILayout.Button("Відмовитись (як у гнізді)")) { Nest.Refuse(p); _note = "Відмовився: нове — наступного дня."; }
            GUILayout.EndHorizontal();
            _raw = GUILayout.Toggle(_raw, " показати сирий запис у персонажі");
            if (_raw)
            {
                string raw;
                GUILayout.Label("<size=11>" + Errands.Key + " = " + (p.m_customData.TryGetValue(Errands.Key, out raw) ? raw : "(нема)") + "</size>");
                GUILayout.Label("<size=11>" + RuneBook.Key + " = " + (p.m_customData.TryGetValue(RuneBook.Key, out raw) ? raw : "(нема)") + "</size>");
                GUILayout.Label("<size=11>" + Errands.LogKey + " = " + (p.m_customData.TryGetValue(Errands.LogKey, out raw) ? raw : "(нема)") + "</size>");
            }
        }

        private static string AddToBag(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null) return "Нема предмета " + prefab + ".";
            return p.GetInventory().AddItem(go, n) ? "У сумці: " + prefab + " ×" + n + "." : "Сумка повна.";
        }

        // the time of day for a test (the world's own clock, forward only: on the host or alone)
        private static string SetTime(float visual)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || EnvMan.instance == null) return "Час міняє лише хост або гра наодинці.";
            float raw = visual < 0.25f ? visual / 0.25f * 0.15f : visual <= 0.75f ? 0.15f + (visual - 0.25f) / 0.5f * 0.7f : 0.85f + (visual - 0.75f) / 0.25f * 0.15f; // (EnvMan.RescaleDayFraction, back)
            double len = EnvMan.instance.m_dayLengthSec, now = ZNet.instance.GetTimeSeconds();
            double t = System.Math.Floor(now / len) * len + raw * len;
            if (t < now) t += len;
            ZNet.instance.SetNetTime(t);
            int min = Mathf.RoundToInt(visual * 24f * 60f);
            return "Час: " + (min / 60).ToString("00") + ":" + (min % 60).ToString("00") + " (вперед, ніколи назад).";
        }

        private void Times()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Час (тест):", GUILayout.Width(80f));
            if (GUILayout.Button("Ніч 22:00")) _note = SetTime(22f / 24f);
            if (GUILayout.Button("Перед світанком 5:40")) _note = SetTime(5f / 24f + 40f / 1440f);
            if (GUILayout.Button("Ранок 6:30")) _note = SetTime(6.5f / 24f);
            if (GUILayout.Button("Полудень 12:00")) _note = SetTime(0.5f);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- give one
        private void Give(Player p)
        {
            Times();
            GUILayout.BeginHorizontal();
            Errands.ForceNoDeath = GUILayout.Toggle(Errands.ForceNoDeath, " видавати з модифікатором «Без смерті» (тест)");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Видати доручення</b> (поточне відкладеться)");
            GUILayout.FlexibleSpace();
            _showAll = GUILayout.Toggle(_showAll, " усі, навіть не на цьому етапі");
            if (GUILayout.Button("Перечитати шаблони", GUILayout.Width(150f))) { Errands.Reload(); _note = "Шаблонів: " + Errands.All().Count + "."; }
            GUILayout.EndHorizontal();
            List<string> recent = Errands.History(p);
            foreach (ErrandDef d in Errands.All())
            {
                bool stage = Stage.HasAll(p, d.Needs);
                if (!stage && !_showAll) continue;
                GUILayout.BeginHorizontal();
                string tags = (d.Golden ? " <color=#FFC94A>золоте</color>" : "") + (Errands.NeedsServer(d) ? " <color=#8EC9FF>місце світу</color>" : "")
                    + (!stage ? " <color=#FF7A7A>не на етапі (" + string.Join(" ", d.Needs.ToArray()) + ")</color>" : "") + (recent.Contains(d.Id) ? " <color=#9A9A9A>недавнє</color>" : "");
                if (Stage.Of(p) > d.Upto) tags += " <color=#9A9A9A>уже не на етапі (до " + d.Upto + " босів)</color>";
                if (d.Mods.Count > 0) tags += " <color=#FF8A7A>" + string.Join("+", d.Mods.ToArray()) + "</color>";
                foreach (KeyValuePair<string, float> mm in d.MayMods) tags += " <color=#FF8A7A>" + mm.Key + " " + Mathf.RoundToInt(mm.Value) + "%</color>";
                if (d.Weight != 1f) tags += " <color=#9A9A9A>вага " + d.Weight.ToString("0.##") + "</color>";
                GUILayout.Label("<b>" + d.Id + "</b> — " + d.Name + " · " + d.Who + " · ціна " + d.PriceMin + (d.PriceMax != d.PriceMin ? "–" + d.PriceMax : "") + tags);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Хугін", GUILayout.Width(64f))) GiveIt(p, d, false);
                if (GUILayout.Button("Мунін", GUILayout.Width(64f))) GiveIt(p, d, true);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Як з гнізда: випадкове (Хугін)")) PickIt(p, false);
            if (GUILayout.Button("Як з гнізда: випадкове (Мунін)")) PickIt(p, true);
            if (GUILayout.Button("Скинути історію")) { p.m_customData.Remove(Errands.LogKey); _note = "Історію доручень скинуто: недавні знову можуть прийти."; }
            GUILayout.EndHorizontal();
        }

        private void GiveIt(Player p, ErrandDef d, bool munin)
        {
            if (Errands.Of(p) != null) Errands.Drop(p);
            string why = Errands.Give(p, d, munin, w => _note = "Не вийшло: " + w);
            _note = why != null ? "Не вийшло: " + why : Errands.Of(p) != null ? "Видано: " + d.Id + " від " + Plugin.RavenName(munin) + "." : "Питаю в сервера, де це місце… (до " + Mathf.RoundToInt(ErrandNet.Wait) + " с)";
        }

        private void PickIt(Player p, bool munin)
        {
            if (Errands.Of(p) != null) Errands.Drop(p);
            float chance = RuneBook.GoldChance(RuneBook.Of(p).Opened);
            ErrandDef d = chance > 0f && Random.value < chance ? Errands.Pick(p, munin, true) : null;
            if (d == null) d = Errands.Pick(p, munin, false);
            if (d == null) { _note = "Нічого не підходить (етап, хто приносить, недавні)."; return; }
            GiveIt(p, d, munin);
        }

        // ---------------------------------------------------------------- the nest and the runes
        private void NestAndRunes(Player p)
        {
            GUILayout.Label("<b>Гніздо</b>: " + Nest.Status(p) + (Nest.Blocked(p) != null ? " · <color=#9A9A9A>" + Nest.Blocked(p) + "</color>" : "") + " · " + Nest.Cmd(p, new[] { "nest" }).Split('\n')[1]);
            GUILayout.Label("<b>Милість</b>: " + Favour.Cmd(p, new[] { "favour" }).Split('\n')[0]);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Одін прийде (після 10 с у гнізді)")) { _note = Nest.Cmd(p, new[] { "nest", "come" }); }
            if (GUILayout.Button("Милість: усе по 50")) { foreach (string g in Favour.Ids) Favour.Cmd(p, new[] { "favour", g, "50" }); _note = "Милість — по 50 у кожній групі."; }
            if (GUILayout.Button("Повернувся зараз")) { _note = Nest.Cmd(p, new[] { "nest", "back" }); }
            if (GUILayout.Button("Зняти денну паузу")) { _note = Nest.Cmd(p, new[] { "nest", "ready" }); }
            GUILayout.EndHorizontal();
            GUILayout.Label("<b>Вороняче дерево</b> (сервер ставить 3: зелене в Луках і крижане в Горах — від 2 км від центру, горіле в Попелястих землях) · в'язь: " + (RuneBook.Of(p).Bind.Length > 0 ? RuneBook.Of(p).Bind : "нема") + " (raven bind)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Поставити дерево поруч (тест)"))
            {
                GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(RavenTree.PrefabHere(p.transform.position)) : null;
                if (pf == null) _note = "Нема префаба дерева.";
                else { Vector3 at = p.transform.position + p.transform.forward * 6f; at.y = ZoneSystem.instance.GetGroundHeight(at); Object.Instantiate(pf, at, Quaternion.identity); _note = "Дерево стоїть перед тобою (лишиться у світі)."; }
            }
            if (GUILayout.Button("Телепорт до найближчого (через сервер)"))
            {
                _note = "Питаю в сервера…";
                List<ErrandNet.Ask> asks = new List<ErrandNet.Ask> { new ErrandNet.Ask { Slot = "t", Names = "RavenTree", X = p.transform.position.x, Z = p.transform.position.z, Min = 0f, Max = 6000f } };
                ErrandNet.Find(asks, found =>
                {
                    string v; Vector3 at;
                    if (found == null || !found.TryGetValue("t", out v) || !ErrandSteps.Point(v, out at)) { _note = "Сервер не знає дерев (або не відповів)."; return; }
                    ErrandMap.Teleport(Player.m_localPlayer, at + new Vector3(4f, 0f, 4f));
                    _note = "Телепорт до дерева " + v + ".";
                });
            }
            if (GUILayout.Button("Розплести в'язь")) { RuneBook.Unweave(p); _note = "В'язь розплетено."; }
            GUILayout.EndHorizontal();
            GUILayout.Label("<b>Руни</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 зарубка")) { _note = RuneBook.Cmd(p, new[] { "runes", "notch", "1" }); }
            if (GUILayout.Button("Відкрити руну")) { _note = RuneBook.Cmd(p, new[] { "runes", "open" }); }
            if (GUILayout.Button("+1 золота")) { _note = RuneBook.Cmd(p, new[] { "runes", "gold", "1" }); }
            if (GUILayout.Button("Скинути руни")) { _note = RuneBook.Cmd(p, new[] { "runes", "reset" }); }
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- champions near
        private void ChampionsNear(Player p)
        {
            GUILayout.Label("<b>Ватажки поруч</b> (до 300 м)");
            int n = 0;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead()) continue;
                Champions.Mods m = Champions.Fast(c);
                if (m == null) continue;
                float dist = Vector3.Distance(p.transform.position, c.transform.position);
                if (dist > 300f) continue;
                n++;
                bool mine = m.Owner == p.GetPlayerID();
                GUILayout.BeginHorizontal();
                GUILayout.Label(Champions.Name(m) + " · " + Champions.Words(m) + " · " + Mathf.RoundToInt(c.GetHealth()) + "/" + Mathf.RoundToInt(c.GetMaxHealth()) + " · ×" + m.Hp.ToString("0.#") + " здоров'я, ×" + m.Dmg.ToString("0.#") + " шкода · "
                    + Mathf.RoundToInt(dist) + " м · " + (mine ? "<color=#9FD49F>твій</color>" : "чужий (" + m.Owner + ")") + (mine && !Errands.Wants(p, m.Id) ? " <color=#FF7A7A>доручення вже нема</color>" : ""));
                GUILayout.EndHorizontal();
            }
            if (n == 0) GUILayout.Label("<color=#9A9A9A>Нема. Ватажок з'являється, коли зайдеш у зону доручення (коло на мапі).</color>");
        }

    }
}
#endif

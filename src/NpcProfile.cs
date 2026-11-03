#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Вдача»: a person's card (the round menu's button): its ways in words, not numbers — how it walks, how it works,
    // its day, how it is with people, how it is in a fight — and its life (work, home, bed, chest, health). Words only
    // (to tune a template: its file). Shown in the book's card style (RavenBook.ShowCard).
    internal static class NpcProfile
    {
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 5, Title = "Вдача", Icon = "n-persona", AnyTime = true,
                Desc = a => Lang.T("Яка це людина: хода, вдача, ритм дня, з людьми, у бою — і чим живе."),
                Do = (a, p) => { Show(a); return null; } };
        }

        internal static void Show(NpcActor a)
        {
            if (a == null || a.Def == null) return;
            NpcPersona w = NpcPersona.Of(a);
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            _a = a;
            NpcSettlers.Look own = NpcSettlers.Of(a);
            rows.Add(Row("Хода", Join(own != null ? NpcSettlers.AgeWord(own.Age) : "", NpcGait.Word(a), Band(w.Walk, 0.92f, 1.08f, "повільний крок", "звичайний крок", "швидкий крок"),
                Band(w.Stoop, 0.2f, 0.5f, "рівна постава", "трохи згорблений|згорблена", "сутулий|сутула"),
                Band(w.Height, 0.98f, 1.02f, "невисокий|невисока", "середнього зросту", "високий|висока"))));
            rows.Add(Row("Вдача", Join(Band(w.Diligence, 0.35f, 0.65f, "неквапливий|некваплива у роботі", "робить як слід", "старанний|старанна"),
                Band(w.Laziness, 0.25f, 0.6f, "невтомний|невтомна", "", "лінькуватий|лінькувата"),
                Band(w.Curiosity, 0.35f, 0.65f, "сам|сама у собі", "", "цікавий|цікава — усе помічає"))));
            rows.Add(Row("Хист", NpcKnack.Describe(a)));
            rows.Add(Row("Ритм дня", Day(w.Chrono)));
            rows.Add(Row("З людьми", Band(w.Chatty, 0.35f, 0.65f, "мовчазний|мовчазна", "говорить, коли є що сказати", "балакучий|балакуча")));
            rows.Add(Row("У бою", Join(Fight(a), Band(w.Bravery, 0.35f, 0.65f, "боязкий|боязка", "", "сміливий|смілива"))));
            rows.Add(Row("Життя", Life(a)));
            string story = NpcBackstory.Text(a, true);
            if (story.Length > 0) rows.Add(new KeyValuePair<string, string>(Lang.T("Історія"), "<i>" + story + "</i>")); // (its own words: not in the card's form, already its own)
            RavenBook.ShowCard(a.Name, rows);
        }

        private static KeyValuePair<string, string> Row(string label, string text)
        {
            if (_a != null) text = _a.Form(text);
            return new KeyValuePair<string, string>(Lang.T(label), Capital(text));
        }

        private static NpcActor _a; // (the card being made: its words in its form)

        private static string Band(float v, float lo, float hi, string low, string mid, string high) { return Lang.T(v < lo ? low : v > hi ? high : mid); }

        private static string Join(params string[] parts)
        {
            List<string> l = new List<string>();
            foreach (string s in parts) if (!string.IsNullOrEmpty(s)) l.Add(s);
            return string.Join(", ", l.ToArray());
        }

        private static string Capital(string s) { return string.IsNullOrEmpty(s) ? s : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1); }
        private static string Clock(float h) { h = Mathf.Repeat(h, 24f); int m = Mathf.RoundToInt(h * 60f) % 1440; return (m / 60) + ":" + (m % 60).ToString("00"); }

        // its day in words: when it gets up, when it goes to bed (the schedule 6 / 22, shifted by its chrono)
        private static string Day(float chrono)
        {
            string when = Lang.F("встає о {0}, лягає о {1}", Clock(6f + chrono), Clock(22f + chrono));
            if (chrono < -0.7f) return Lang.T("жайворонок: ") + when;
            if (chrono > 0.7f) return Lang.T("сова: ") + when;
            return when;
        }

        private static string Fight(NpcActor a)
        {
            switch (a.Mind != null ? a.Mind.Style : NpcMind.Fight.Flee)
            {
                case NpcMind.Fight.Flee: return Lang.T("мирний|мирна — тікає");
                case NpcMind.Fight.Wary: return Lang.T("обережний|обережна — тікає, а загнаний|загнана відбивається");
                case NpcMind.Fight.Defend: return Lang.T("боєць|бійчиня — захищає себе й тих, кого супроводжує");
                case NpcMind.Fight.Hunt: return Lang.T("боєць|бійчиня — сам|сама шукає ворогів");
                default: return Lang.T("тримається осторонь бою");
            }
        }

        private static string Life(NpcActor a)
        {
            List<string> l = new List<string>();
            NpcWorker wk = a.Worker;
            l.Add(wk != null && wk.Works ? Lang.T("робота: ") + Jobs.Word(wk.JobId) : Lang.T("без роботи"));
            l.Add(NpcResident.Is(a) ? Lang.T("живе на базі") : NpcTalk.Hired(a) ? Lang.T("найнятий|найнята, ще без дому") : Lang.T("ніде не живе"));
            if (NpcResident.Is(a)) l.Add(!NpcKit.HasRef(a.Zdo, NpcResident.KBed) ? Lang.T("без ліжка") : Lang.T("має ліжко"));
            if (wk != null && !wk.ChestId.IsNone()) l.Add(wk.JobId == "cook" ? Lang.T("тримає комору") : Lang.T("має ящик запасів"));
            if (NpcResident.Is(a)) l.Add(NpcHunger.Word(a));
            if (NpcTalk.Hired(a)) l.Add(NpcAttitude.Word(a, Player.m_localPlayer));
            Character c = a.GetComponent<Character>();
            if (c != null && NpcTalk.Hired(a)) l.Add(Lang.T("здоров'я ") + Mathf.RoundToInt(c.GetHealthPercentage() * 100f) + "%");
            return string.Join(", ", l.ToArray());
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Talking a person round, its own way (the user 2026-11-07, «переконати по-своєму»): when it said no, you may try once more — by its pride, by what it gains, by fear,
    // by friendship, by duty. Each nature takes each way its own way (NpcNature.SwayBy: the column Sway of its row; a plain one by its traits), the best one a chip on its
    // card («Вдача»: a hint, not the table). Where:
    //   favour   it refused a favour you asked (NpcFavours.Ask → SwayOn «favour <id>»; not one only for one dear): talked round, it does it (NpcFavours.Grant — the same yes)
    //   peace    it would not make it up with the one it quarrelled with (NpcAttitude.MakePeace → SwayOn «peace»): talked round, they make it up (MadePeace)
    //   leave    it means to leave the base (NpcToil.Leaving: its worry «leaving», place c_leaving): talked round, it stays (act «stay»: its days at the bottom start
    //            again) and remembers it (stayed); a try an hour at most (SwayAgain)
    // A refusal of work for its nature is not one of these (NpcNature.WontTake: what it is, not how it stands with you).
    // Whether it works: SwayBase × its way × how it feels about you × its patience now; no — a little of its patience, twice as much for a way that is wrong for it
    // (below SwayWrong: its words sway_wrong), a threat that does not work angers it. One try a no. Words: sway_yes, sway_no, sway_wrong, sway_stay; your words are the
    // answers below (SwayText). ?swayable <favour|peace|leave> · !sway <way> <favour|peace|leave> · {sway}
    internal static partial class NpcTalk
    {
        private static string _sway; // what it just said no to that a word may change: «favour <id>» · «peace» (this talk only)
        internal static float SwayBase = 0.45f, SwayMost = 0.9f, SwayCost = 6f, SwayWrong = 0.7f, SwayThreatIrk = 6f, SwayAgain = 1f;
        internal static string SwaySaid = "";

        internal static void SwayOn(string what) { _sway = what; }

        // your words for each way, by what you talk it round from («||»: one of them a showing)
        private static readonly Dictionary<string, string[]> SwayText = new Dictionary<string, string[]>
        {
            { "favour", new[] {
                "pride:Ти ж не з тих, хто відступає. || Я дума{в|ла}, ти сміливіший|сміливіша.",
                "gain:Тобі ж із цього буде користь. || Не прогадаєш, повір. || Подумай, що з того матимеш.",
                "fear:Подумай, що буде, якщо ні. || Краще не зли мене. || Пошкодуєш, як відмовишся.",
                "friend:Зроби це для мене. || Ми ж свої, правда? || Прошу тебе як друга.",
                "duty:Так треба, для всіх. || Хтось мусить. Чому не ти? || Ти ж обіцяв|обіцяла допомагати." } },
            { "peace", new[] {
                "pride:Перший крок робить сильніший. || Покажи, хто з вас мудріший.",
                "gain:Мир вигідний обом. || Помиритесь — і житиметься легше.",
                "fear:Не помиритесь — обом буде гірше. || Я довго цього терпіти не буду.",
                "friend:Помирись. Заради мене. || Зроби це для мене, прошу.",
                "duty:Ви ж одна оселя. || Нам тут разом жити." } },
            { "leave", new[] {
                "pride:Ти ж не з тих, хто тікає. || Підеш — і всі скажуть, що не витримав|витримала.",
                "gain:Тут тобі буде краще, ніж деінде. Я подбаю. || Лишайся. Не прогадаєш.",
                "fear:Сам|Сама в дорозі пропадеш. || Там, за стінами, гірше, ніж тут.",
                "friend:Лишайся. Заради мене. || Не йди. Ти мені тут потрібен|потрібна.",
                "duty:Без тебе оселя не впорається. || Тебе тут чекають. Хто ж буде замість тебе?" } },
        };

        // the answers of a place: your ways, each «!sway <way> <what>», then the place's own
        private static string SwayAnswers(string what)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string e in SwayText[what]) { int c = e.IndexOf(':'); sb.Append("> ").Append(e.Substring(c + 1)).Append(" => swayed ?swayable ").Append(what).Append(" !sway ").Append(e.Substring(0, c)).Append(' ').Append(what).Append('\n'); }
            return sb.ToString();
        }

        private static bool Swayable(NpcActor a, Player p, string what)
        {
            if (a == null || p == null) return false;
            if (what == "leave") { Row r = FindRow(p, "s", a, "leave"); return NpcToil.Leaving(a) && !(r != null && !Elsewhere(r) && NowSec - r.At < SwayAgain * GameHour); }
            return _sway != null && With == a && (what == "peace" ? _sway == "peace" : _sway.StartsWith("favour ", System.StringComparison.Ordinal));
        }

        // «!sway <way> <what>»
        private static void Sway(NpcActor a, Player p, string[] w)
        {
            SwaySaid = "…";
            if (a == null || p == null || w.Length < 3 || !Swayable(a, p, w[2])) return;
            string way = w[1], what = w[2], favour = what == "favour" ? _sway.Substring(7) : null;
            _sway = null; // (one try a no)
            if (what == "leave") { Row r = PutRow(p, "s", a, "leave"); r.At = NowSec; SaveRows(p); }
            float by = NpcNature.SwayBy(a, way);
            float chance = Mathf.Clamp(SwayBase * by * (1f + NpcAttitude.Of(a, p.GetPlayerID()) / 100f) * Mathf.Lerp(0.6f, 1.2f, NpcPatience.Of(a, p) / 100f), 0.05f, SwayMost);
            bool yes = UnityEngine.Random.value < chance;
            Npcs.Log(a.Id + ": talked round by " + way + " (" + what + ", " + Mathf.RoundToInt(chance * 100f) + "%): " + (yes ? "yes" : "no"));
            if (!yes)
            {
                bool wrong = by < SwayWrong;
                NpcPatience.Spend(a, p, SwayCost * (wrong ? 2f : 1f));
                if (wrong && way == "fear") NpcPatience.Irritate(a, p, SwayThreatIrk); // (a threat that does not frighten it: it angers it)
                SwaySaid = NpcPhrases.Compose(a, wrong ? "sway_wrong" : "sway_no", p) ?? a.Form(Lang.T("Ні. Не переконаєш."));
                return;
            }
            string lead = NpcPhrases.Compose(a, what == "leave" ? "sway_stay" : "sway_yes", p) ?? a.Form(Lang.T("Ну… гаразд."));
            switch (what)
            {
                case "leave":
                    a.Act("stay");
                    NpcMemory.Tell(a, "stayed", p.GetPlayerID(), "");
                    SwaySaid = lead;
                    break;
                case "peace":
                {
                    string kind;
                    NpcActor o = NpcAttitude.Grudge(a, out kind);
                    if (o == null) { SwaySaid = lead; return; }
                    NpcAttitude.MadePeace(a, o, kind, p);
                    SwaySaid = lead + " " + NpcAttitude.PeaceSaid;
                    break;
                }
                default:
                    NpcFavours.Grant(a, p, favour);
                    SwaySaid = lead + " " + NpcFavours.Said;
                    break;
            }
        }

        private static void WireSway()
        {
            Deeds["sway"] = Sway;
            Words["{sway}"] = (a, p) => SwaySaid;
            StoryCond.Register("swayable", new StoryCond.Kind { Usage = "swayable favour|peace|leave   (його можна спробувати переконати: щойно відмовив у проханні, не хоче миритися, зібрався піти)", Min = 1, Test = (p, w) => Swayable(Him, p, w[1]) });
            More.Add("[talk common +]\n" +
                "@favour +\n" + SwayAnswers("favour") +
                "@c_peace +\n" + SwayAnswers("peace") +
                "@c_leaving\n{concern}\n" + SwayAnswers("leave") +
                "> Що тебе гнітить? => how\n> Іди, якщо хочеш. || Тримати не буду. => end !tone cold\n" +
                "@swayed\n{sway}\n> То що ж тобі вдягти? => dress ?dressok\n> {@say_ok} => start\n> {@say_bye} => end\n");
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text;

namespace HuginWhispers
{
    // Munin's street mouth: his lines said the way a lout from the block talks — the plain spoken words («шо», «якшо»,
    // «нашо», «тіко», «зара», «ше»; Ukrainian ones, never Russian borrowings), a curse or a clumsy filler wedged in
    // mid-sentence («ти, бля, знов…», «ти, той-во, знов…»), now and then a grunt in front («Чуєш,», «Та бля,») or a
    // jab on the end («Лошара.»). As dirty as he is wild: most at the low trust, a little gentler at the top, all of it
    // with the beast awake. Ukrainian lines only; the forms {his|hers} and the placeholders are left untouched.
    internal static class Gutter
    {
        internal static bool On = true;

        // plain spoken forms (whole words; the first letter's case kept)
        private static readonly Dictionary<string, string> Spoken = new Dictionary<string, string>
        {
            { "що", "шо" }, { "щоб", "шоб" }, { "якщо", "якшо" }, { "щось", "шось" }, { "навіщо", "нашо" }, { "нащо", "нашо" },
            { "нічого", "нічо" }, { "тільки", "тіко" }, { "зараз", "зара" }, { "дивись", "зирь" }, { "подивись", "позирь" },
            { "ще", "ше" }, { "щоразу", "шоразу" }, { "щодня", "шодня" }, { "щоправда", "шоправда" }, { "ніщо", "нішо" },
            { "чогось", "шось" }, { "сьогодні", "сьодні" }, { "дивися", "зирь" }, { "подивися", "позирь" },
            { "звичайно", "ясне діло" }, { "отже", "короче" }, { "трохи", "трошки" }, { "хлопче", "малий" },
            { "мабуть", "мо'" }, { "втім", "та й похуй" }, { "проте", "але" }, { "адже", "бо" }, { "однак", "але" },
        };

        // a curse or a clumsy filler wedged in mid-sentence, between commas: only the words that work as a pause there
        // («ти, бля, знов…»); the ones that mean «to hell» («в пизду», «нахуй», «до сраки») live on the end instead
        private static readonly string[] Wedge =
        {
            "бля", "курва", "сука", "бляха", "курва мать", "бляха-муха", "сука-бля",
            "той-во", "як його", "ну, той", "короче", "ото", "їй-бо", "холєра", "бля, як його", "йо", "трясця",
            "йо-майо", "хай йому грець", "шоб я так жив", "твою дивізію", "мать твою", "йошкін кіт", "йоб твою мать",
        };
        // softer ones for the top trust
        private static readonly string[] WedgeWarm = { "бля", "курва", "бляха", "йой", "той-во", "короче", "ну", "холєра", "їй-бо", "йо-майо", "хай йому грець", "шоб я так жив", "йошкін кіт", "братан" };

        // a grunt in front of the line
        private static readonly string[] Grunt = { "Чуєш,", "Йо,", "Та бля,", "Ну шо,", "Ото", "Короче,", "Нє, ну", "Тю,", "Йой,", "Бля,", "Е,", "Ге,",
            "Ох йо,", "Мать моя,", "Ну піздець,", "Йошкін кіт,", "Йо-майо,", "Хай йому грець,", "Твою дивізію,", "Ну хрін з ним,",
        };

        // a jab on the end (at you)
        private static readonly string[] Curse =
        {
            "Лошара.", "От чмо.", "Тьху на тебе.", "Насеру тобі в шолом, побачиш.", "Ото мудило.", "Ну шо за пиздець.",
            "Дзьобом би тебе в тім'я.", "Тупе, як чобіт.", "Пиздець, а не воїн.", "Шоб тебе. Шоб тебе!", "Їбанько, їй-бо.",
            "Мені з тебе ржачно.", "Ну ти й довбойоб, курва.", "Сракою чую — буде біда.", "Тьху. Ссикло.", "Позорище, бля.",
            "Мудозвон.", "Пень з вухами.", "Ну його в пизду.", "Та ну нахуй.", "До сраки таке.", "Їбать-копать.", "Хай тебе грець.", "Йо-майо.", "Шоб тебе підняло та гепнуло.", "Ну й лажа.", "Дурне, як сто кабанів.", "Тьху, нездара.", "Хрін тобі, а не слава.", "Чорт лисий.", "Ото халепа.", "Їй-бо, без мізків.", "Сто чортів тобі.", "Твою ж дивізію.", "Мать твою за ногу.", "Охрініти.", "Ну ти й жук.", "Позорище ходяче.", "Тьху, гнида.", "Валенок, їй-бо.", "Лох педальний.", "Дубина стоєросова.", "Ну ти й баклан.", "Обрубок, курва.", "Шоб тобі пусто було.",
        };
        private static readonly string[] CurseWarm = { "Ех ти, дурне моє.", "Ну ти й кабанчик, бля.", "Йой, братан.", "Та ну тебе, холєра.", "Свій, хоч і з прибабахом.", "Ото, курва. Ото.",
            "Дурне, але моє.", "Хуйло ти моє рідне.", "Свій, хоч і довбень.", "Ех ти, халепа ходяча.", "Горе моє, курва.", "Ну ти й жук, братан.", "Тримайся, бляха.", "Йой, малий.", "Мій ти мудозвон.", "Шо б я без тебе робив.", "Ну й нехай, живемо.", "Біда моя, курва, кривонога.", "Ото ми з тобою, бля.", "Пацан, шо тут скажеш.", "Люблю, але мовчу, сука.",
        };

        // the grunts with no swear in them («Чуєш,», «Тю,», «Мать моя,»), for a line that already has two
        private static string[] _gruntClean;
        private static string[] GruntClean
        {
            get
            {
                if (_gruntClean == null) { var l = new List<string>(); foreach (string g in Grunt) if (Count(g) == 0) l.Add(g); _gruntClean = l.ToArray(); }
                return _gruntClean;
            }
        }

        // the line as Munin says it; tier 1..3 (Tone), rabid: the beast awake
        internal static string Dirty(string s, int tier, bool rabid)
        {
            if (!On || string.IsNullOrEmpty(s) || !Lang.IsSource) return s;
            float k = rabid ? 1f : tier >= 3 ? 0.6f : tier == 2 ? 0.8f : 1f; // how much of it
            s = Swap(s, rabid || tier < 3 ? 1f : 0.85f); // «шо», not «що»: nearly always
            bool warm = tier >= 3 && !rabid;
            int swears = Count(s);
            // a curse in mid-sentence
            if (swears < (rabid ? 2 : 1) && UnityEngine.Random.value < (rabid ? 0.7f : 0.38f * k)) { string w = Wedged(s, warm ? WedgeWarm : Wedge); if (w != null) { s = w; swears++; } }
            // a grunt in front (a swearing one only while the line has room for it: two swears a line at most)
            if (UnityEngine.Random.value < 0.16f * k) { s = Grunted(s, swears < 2 ? Grunt : GruntClean); swears = Count(s); }
            // an old curse on the end
            if (swears < 2 && UnityEngine.Random.value < (rabid ? 0.25f : 0.13f * k) && Ends(s) && s.Length < 120) s = s + " " + Pick(warm ? CurseWarm : Curse);
            return s;
        }

        // ---------------------------------------------------------------- Hugin: the smartass, not the scholar
        // Hugin speaks plainly too, only sharper: the bookish linking words turn into spoken ones («утім» → «але»), now and
        // then a filler wedged in («ти, ну, знов…», «ти, значить, знов…») or a word in front («Слухай,», «Ну,»), and once in a
        // while a jab on the end («Незграба.», «Генію.»); at the top trust the jabs are fond ones. Ukrainian lines only.
        private static readonly Dictionary<string, string> Plainer = new Dictionary<string, string>
        {
            { "утім", "але" }, { "втім", "але" }, { "проте", "але" }, { "однак", "але" }, { "адже", "бо" }, { "отже", "так от" },
            { "вельми", "дуже" }, { "воістину", "справді" }, { "неабияк", "дуже" }, { "щонайменше", "хоч" }, { "доволі", "досить" },
        };
        private static readonly string[] HuginWedge = { "ну", "от", "значить", "власне", "як його", "слухай", "бач", "ага" };
        private static readonly string[] HuginGrunt = { "Ну,", "Слухай,", "Ага,", "Хм,", "Так,", "Ну от,", "Знаєш,", "Ну-ну,", "От" };
        private static readonly string[] HuginJab =
        {
            "Незграба.", "Роззява.", "Недотепа.", "Генію.", "Ну ти й {бовдур|бовдурка}.", "Ледащо.", "Ну-ну.", "Браво.", "Я записав.",
            "Ну ти й {йолоп|йолопка}.", "Хто б сумнівався.", "Логічно.", "Сирота.", "Ну ти й ґава.", "Ну й вайло.", "Мисливець року.",
        };
        private static readonly string[] HuginJabWarm = { "Ех ти.", "Горе моє.", "Ну-ну, генію.", "Недотепа {мій|моя}.", "Що б ти без мене робив{|ла}.", "Чудо ти моє." };

        internal static string Plain(string s, int tier)
        {
            if (!On || string.IsNullOrEmpty(s) || !Lang.IsSource) return s;
            s = Swap(s, 1f, Plainer);
            if (UnityEngine.Random.value < 0.12f) { string w = Wedged(s, HuginWedge); if (w != null) return w; } // one habit a line, not a pile of them
            if (UnityEngine.Random.value < 0.1f) return Grunted(s, HuginGrunt);
            // a jab on the end only after a real sentence: not after a short tail already there («…Трохи.», his «Я записав.»)
            if (UnityEngine.Random.value < (tier >= 3 ? 0.05f : 0.09f) && Ends(s) && !s.EndsWith("?") && s.Length < 110 && LastWords(s) > 2) return s + " " + Pick(tier >= 3 ? HuginJabWarm : HuginJab);
            return s;
        }

        // the words in the line's last sentence (outside {…}; of the forms {his|hers} the braces count as one word)
        private static int LastWords(string s)
        {
            int end = s.Length - 1;
            while (end > 0 && (s[end] == '.' || s[end] == '!' || s[end] == '?' || s[end] == '…')) end--;
            int start = s.LastIndexOfAny(new[] { '.', '!', '?', '…' }, end) + 1;
            int n = 0, depth = 0;
            bool inWord = false;
            for (int i = start; i <= end; i++)
            {
                char c = s[i];
                if (c == '{') { depth++; if (!inWord) n++; inWord = true; continue; }
                if (c == '}') { if (depth > 0) depth--; continue; }
                if (depth > 0) continue;
                bool l = char.IsLetter(c) || c == '\'';
                if (l && !inWord) n++;
                inWord = l;
            }
            return n;
        }

        // the spoken forms: word by word outside {…}
        private static string Swap(string s, float chance) { return Swap(s, chance, Spoken); }
        private static string Swap(string s, float chance, Dictionary<string, string> words)
        {
            StringBuilder sb = new StringBuilder(s.Length + 8), word = new StringBuilder();
            int depth = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                char c = i < s.Length ? s[i] : ' ';
                if (depth == 0 && (char.IsLetter(c) || c == '\'' || c == '’')) { word.Append(c); continue; }
                if (word.Length > 0) { sb.Append(Spoke(word.ToString(), chance, words)); word.Length = 0; }
                if (i == s.Length) break;
                if (c == '{') depth++;
                else if (c == '}' && depth > 0) depth--;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // the word starting at `from` is the last one of its sentence (right after it: . ! ? … or the line's end)
        private static bool LastWordAfter(string s, int from)
        {
            int i = from;
            while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '\'' || s[i] == '’' || s[i] == '-')) i++;
            return i >= s.Length || ".!?…".IndexOf(s[i]) >= 0;
        }

        private static string Spoke(string w, float chance, Dictionary<string, string> words)
        {
            string low = w.ToLowerInvariant(), to;
            if (!words.TryGetValue(low, out to) || UnityEngine.Random.value > chance) return w;
            if (w == w.ToUpperInvariant() && w.Length > 1) return to.ToUpperInvariant(); // (shouted)
            if (char.IsUpper(w[0])) return char.ToUpperInvariant(to[0]) + to.Substring(1);
            return to;
        }

        // «ти знов поліз у болото» → «ти, курва, знов поліз у болото»: after a word in the line's first half, not next to
        // punctuation, not inside {…}, not in a sentence of fewer than five words, and not before a sentence's last word
        // («Ледащо, курва, сонне.» splits a pair: such a pair keeps together)
        private static string Wedged(string s, string[] pool)
        {
            List<int> spots = new List<int>();
            int depth = 0, words = 0;
            bool inWord = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{') { depth++; inWord = false; continue; }
                if (c == '}') { if (depth > 0) depth--; continue; }
                if (depth > 0) continue;
                bool l = char.IsLetter(c);
                if (l && !inWord) words++;
                inWord = l;
                // a space right after a word, with a word (no punctuation, no brace) after it
                if (c == ' ' && i > 0 && char.IsLetter(s[i - 1]) && i + 1 < s.Length && char.IsLetter(s[i + 1]) && words >= 1 && !LastWordAfter(s, i + 1)
                    && !NoWedgeAfter.Contains(WordBefore(s, i).ToLowerInvariant()) && !NoWedgeBefore.Contains(WordAt(s, i + 1).ToLowerInvariant())) spots.Add(i);
            }
            if (words < 5 || spots.Count < 3) return null;
            int at = spots[UnityEngine.Random.Range(0, System.Math.Max(1, spots.Count / 2))];
            string w = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (IsShouted(s)) w = w.ToUpperInvariant();
            return s.Substring(0, at) + ", " + w + "," + s.Substring(at);
        }

        // a wedge never splits these from the next word («в, бля, повітрі», «не, курва, вміє», «і, бля, всі»)
        private static readonly HashSet<string> NoWedgeAfter = new HashSet<string>
        {
            "в", "у", "на", "з", "із", "зі", "зо", "до", "від", "од", "по", "за", "під", "над", "без", "для", "про", "через", "біля", "коло", "при", "між", "перед", "після", "крізь", "серед", "попри",
            "не", "ні", "ані", "і", "й", "та", "чи", "або", "що", "шо", "щоб", "шоб", "якщо", "якшо", "бо", "аби", "хоч", "хоча", "який", "яка", "яке", "які", "свій", "своя", "своє", "свої", "мій", "моя", "твій", "твоя", "твоє", "твої", "цей", "ця", "це", "ці", "той", "те", "ті", "дуже", "так", "такий", "така", "таке", "такі",
        };
        // …nor from a clinging word after it («ти, бля, ж»)
        private static readonly HashSet<string> NoWedgeBefore = new HashSet<string> { "ж", "же", "б", "би", "то", "от", "ото", "ся", "собі" };

        private static string WordBefore(string s, int i) { int a = i; while (a > 0 && (char.IsLetter(s[a - 1]) || s[a - 1] == '\'' || s[a - 1] == '’')) a--; return s.Substring(a, i - a); }
        private static string WordAt(string s, int i) { int b = i; while (b < s.Length && (char.IsLetter(s[b]) || s[b] == '\'' || s[b] == '’')) b++; return s.Substring(i, b - i); }

        private static string Grunted(string s, string[] pool)
        {
            if (s.Length < 2 || !char.IsUpper(s[0]) || IsShouted(s)) return s; // (a line starting with {name} or a brace, or shouted: left)
            string g = Pick(pool);
            if (s.StartsWith(g.TrimEnd(','), System.StringComparison.Ordinal)) return s;
            string first = s.Substring(0, System.Math.Min(s.Length, 6));
            if (first.StartsWith("Ох") || first.StartsWith("Ну") || first.StartsWith("Ха") || first.StartsWith("Е") || first.StartsWith("Курва") || first.StartsWith("Тьху") || first.StartsWith("Бля") || first.StartsWith("Чуєш") || first.StartsWith("Та ") || first.StartsWith("Йо") || first.StartsWith("Шо") || first.StartsWith("Слух") || first.StartsWith("Хм") || first.StartsWith("Ага") || first.StartsWith("От") || first.StartsWith("Так")) return s; // a grunt there already
            int sp = 0;
            while (sp < s.Length && char.IsLetter(s[sp])) sp++;
            string w = s.Substring(0, sp);
            if (Proper.Contains(w)) return g + " " + s; // a name keeps its capital
            return g + " " + char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        private static readonly HashSet<string> Proper = new HashSet<string> { "Одін", "Одіне", "Хугін", "Мунін", "Тор", "Хель", "Мідґард", "Вальгалла", "Локі", "Фрейя", "Тролі", "Троль", "Модер", "Еіктір", "Яґлут", "Бонемас", "Генерал" };

        private static bool Ends(string s) { char c = s[s.Length - 1]; return c == '.' || c == '!' || c == '?' || c == '…'; }

        private static bool IsShouted(string s)
        {
            int up = 0, low = 0;
            foreach (char c in s) { if (char.IsUpper(c)) up++; else if (char.IsLower(c)) low++; }
            return up > 6 && up > low * 2;
        }

        internal static int Count(string s)
        {
            int n = 0;
            string l = Patterns.Plain(s); // («рахуй», «люблять», «дупло» are no swears)
            foreach (string root in Roots) for (int i = l.IndexOf(root, System.StringComparison.Ordinal); i >= 0; i = l.IndexOf(root, i + root.Length, System.StringComparison.Ordinal)) n++;
            return n;
        }
        private static readonly string[] Roots = { "курв", "сук", "хуй", "хує", "пізд", "пизд", "йоб", "їб", "гівн", "срак", "дуп", "трясц", "холєр", "пся", "бля", "мудак", "мудил", "чмо", "ссик" };

        private static string Pick(string[] a) { return a[UnityEngine.Random.Range(0, a.Length)]; }
    }
}
#endif

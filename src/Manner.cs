#if RAVEN_ITEM
using System.Collections.Generic;

namespace HuginWhispers
{
    // Each raven's manner of speech: the same words put together, but said by this raven, not by any Hugin or Munin.
    //   - how it calls you: by name, or its own word for you («друже», «господарю», «смертний», «мішку з кістками»…);
    //   - its little habits: a word it starts with now and then («Хм.», «Ну.»), and one it ends with («Отак.», «Тьху.»);
    //   - how much it says: a terse raven leaves out what can be left out, a chatty one adds it;
    //   - Munin's own swearing: about half of the swear words are his, the rest he never uses.
    // All of it from the raven's own number (Voice: one number is the raven's voice and manner, the same in every game).
    // Set before a line is put together (Tone.Use for your own, ComposeCross / ComposePerch for others' and a perch's);
    // the forms of the words for you stay {his|hers}, resolved later like every other line.
    internal static class Manner
    {
        internal static bool Active;
        internal static double Wordy = 0.5;   // the chance an optional part comes (0.5: as before)
        private static int _call, _open, _end;
        private static uint _seed;
        private static bool _munin, _address;

        private static readonly double[] Wordiness = { 0.3, 0.5, 0.72 };
        // how it calls you, in the forms {for him|for her}; index 0: by name, as before
        private static string[] HuginCalls { get { return Lang.Habits(HuginCallsSrc); } } // (translated on every read)
        private static readonly string[] HuginCallsSrc = Lang.Keys(null, "{друже|подруго}", "генію", "{розумнику|розумнице}", "недотепо", "{героє|героїне}", "бідолахо", "{мисливцю|мисливице}", "роззяво", "стратегу", "ледащо", "ґаво", "незграбо", "{учню|ученице}", "{вікінгу|войовнице}");
        private static string[] MuninCalls { get { return Lang.Habits(MuninCallsSrc); } } // (translated on every read)
        private static readonly string[] MuninCallsSrc = Lang.Keys(null, "{малий|мала}", "лошаро", "{дурню|дурепо}", "чмо", "{гівнюче|гівнючко}", "{засранцю|засранко}", "{придурку|придурко}", "мудило", "{пиздюче|пиздючко}", "{шмаркачу|шмаркачко}", "терпило", "кабанчику", "ссикло", "{братан|сеструхо}", "{мужик|тітко}", "їбанько", "{довбойобе|довбойобко}", "{сучий сину|суча доню}", "харчу ходячий", "{телепню|телепко}");
        private static string[] HuginOpen { get { return Lang.Habits(HuginOpenSrc); } } // (translated on every read)
        private static readonly string[] HuginOpenSrc = Lang.Keys(null, "Хм.", "Ну.", "Ну-ну.", "Слухай.", "Ага.", "Так.", "От.");
        private static string[] MuninOpen { get { return Lang.Habits(MuninOpenSrc); } } // (translated on every read)
        private static readonly string[] MuninOpenSrc = Lang.Keys(null, "Чуєш.", "Йо.", "Ну.", "Е.", "Бля.", "Тьху.", "Слухай сюди.", "Короче.", "Та бля.");
        private static string[] HuginEnd { get { return Lang.Habits(HuginEndSrc); } } // (translated on every read)
        private static readonly string[] HuginEndSrc = Lang.Keys(null, "Я записав.", "Кра.", "Ну-ну.", "Отак от.", "Логічно.", "Хто б сумнівався.", "Браво.", "Ага.");
        private static string[] MuninEnd { get { return Lang.Habits(MuninEndSrc); } } // (translated on every read)
        private static readonly string[] MuninEndSrc = Lang.Keys(null, "Ото.", "Тьху.", "Я сказав.", "Кар!", "І все, нахуй.", "Бля.", "Вкурив?", "Шо, нє?", "Ото так, курва.");

        // this raven speaks next (its number, -1: none known: the plain manner); address: the line may call you by its word
        internal static void Use(int code, bool munin, bool address)
        {
            Active = code >= 0;
            if (!Active) { Wordy = 0.5; return; }
            uint h = (uint)code * 2654435761u; h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; // its own stir: the manner apart from the voice's traits
            Wordy = Wordiness[h % 3];
            uint k = h / 3;
            int calls = (munin ? MuninCalls : HuginCalls).Length;
            _call = k % 4 == 0 ? 0 : 1 + (int)((k / 4) % (uint)(calls - 1)); k /= 4 * (uint)calls; // a quarter call you by name
            _open = (int)(k % (uint)(munin ? MuninOpen : HuginOpen).Length); k /= 16;
            _end = (int)(k % (uint)(munin ? MuninEnd : HuginEnd).Length);
            _seed = h >> 12;
            _munin = munin;
            _address = address;
        }

        internal static void Clear() { Active = false; Wordy = 0.5; }

        // Munin's swearing: his half of the list (when enough is left of it to stay fresh)
        internal static List<string> Own(List<string> fit)
        {
            if (!Active || !_munin || fit.Count < 4) return fit;
            List<string> mine = new List<string>(fit.Count);
            foreach (string l in fit) if (((Hash(l) ^ _seed) & 1u) == 0u) mine.Add(l);
            return mine.Count >= 2 ? mine : fit;
        }

        // the habits on a whole line, before its words are filled in ({name} still a placeholder)
        internal static string Dress(string s)
        {
            if (!Active || string.IsNullOrEmpty(s)) return s;
            if (_address && _call > 0 && UnityEngine.Random.value < 0.5f)
            {
                int at = Called(s);
                if (at >= 0)
                {
                    string call = (_munin ? MuninCalls : HuginCalls)[_call];
                    if (call != null) // (null: no such word in this language, your name stays)
                    {
                        if (at >= 2 && ".!?".IndexOf(s[at - 2]) >= 0) call = Cap(call); // a new sentence: «Здається. Друже, …»
                        s = s.Substring(0, at) + call + s.Substring(at + 6);
                    }
                }
            }
            float r = UnityEngine.Random.value;
            string open = _open > 0 ? (_munin ? MuninOpen : HuginOpen)[_open] : null, end = _end > 0 ? (_munin ? MuninEnd : HuginEnd)[_end] : null;
            // not two exclamations in a row («Бляха. Курва. …», «…, бля. Тьху.»), and a swear habit only where the line has room for it
            if (open != null && r < 0.1f && !s.StartsWith(open.TrimEnd('.'), System.StringComparison.Ordinal) && !ShortFirst(s) && Fits(open, s)) return open + " " + Up(s);
            if (end != null && r >= 0.1f && r < 0.27f && !s.EndsWith(end, System.StringComparison.Ordinal) && !ShortLast(s) && Fits(end, s)) return s + " " + end;
            return s;
        }

        // a swearing habit only on a line with no swear of its own («…, курва. Бля.» is one too many); a plain one anywhere
        private static bool Fits(string habit, string line) { return !Patterns.SwearsIn(habit) || !Patterns.SwearsIn(line); }

        // where the line calls you by name: «{name}, …» at its start or after «, » «. » «! » «? » («Гей, {name},»), not
        // where your name is only spoken of («якщо ти з {name}, …», «Це {name}, а я …»); -1: nowhere
        private static int Called(string s)
        {
            for (int at = s.IndexOf("{name},", System.StringComparison.Ordinal); at >= 0; at = s.IndexOf("{name},", at + 1, System.StringComparison.Ordinal))
            {
                if (at != 0 && !(at >= 2 && s[at - 1] == ' ' && ",.!?".IndexOf(s[at - 2]) >= 0)) continue;
                if (Epithet(s, at + 7)) continue; // «{name}, скарбе мій, …»: the line calls you twice already, not a third time
                return at;
            }
            return -1;
        }

        // right after the name, a short word for you of the line's own ending in a comma («скарбе мій,», «ну ти й відрижка троля,»)
        private static bool Epithet(string s, int from)
        {
            int comma = s.IndexOf(',', from), stop = s.IndexOfAny(Stops, from);
            return comma > from && (stop < 0 || comma < stop) && Words(s, from, comma) <= 5;
        }

        // the first / the last sentence is a mere exclamation (two words at most: «Курва.», «Ох ти ж.», «…, бля.» is not one, «Тьху на тебе.» is)
        private static bool ShortFirst(string s)
        {
            int end = s.IndexOfAny(Stops);
            return end > 0 && end < s.Length - 1 && Words(s, 0, end) <= 2;
        }
        private static bool ShortLast(string s)
        {
            int last = s.Length - 1;
            while (last > 0 && System.Array.IndexOf(Stops, s[last]) >= 0) last--;
            int start = s.LastIndexOfAny(Stops, last);
            return start > 0 && Words(s, start + 1, last + 1) <= 2;
        }
        private static readonly char[] Stops = { '.', '!', '?', '…' };
        // words in a piece of the line; of the forms {his|hers} only the first is counted
        private static int Words(string s, int from, int to)
        {
            int n = 0; bool inWord = false, other = false;
            for (int i = from; i < to; i++)
            {
                char c = s[i];
                if (c == '|') { other = true; inWord = false; continue; }
                if (c == '}') { other = false; inWord = false; continue; }
                if (other) continue;
                bool l = char.IsLetter(c) || c == '\'';
                if (l && !inWord) n++;
                inWord = l;
            }
            return n;
        }

        // the first letter of a call, in both its forms: «{друже|подруго}» → «{Друже|Подруго}»
        private static string Cap(string call)
        {
            char[] c = call.ToCharArray();
            bool next = true;
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == '{' || c[i] == '|') { next = true; continue; }
                if (next && char.IsLetter(c[i])) { c[i] = char.ToUpperInvariant(c[i]); next = false; }
            }
            return new string(c);
        }

        private static string Up(string s) { return s.Length > 0 && char.IsLower(s[0]) ? Lang.UpperFirst(s) : s; }

        private static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h ^ (h >> 16);
        }

        // the console: what this raven's manner is
        internal static string Describe(int code, bool munin)
        {
            if (code < 0) return Lang.T("звичайна манера");
            bool wasActive = Active; double wasWordy = Wordy; int c = _call, o = _open, e = _end; uint sd = _seed; bool m = _munin, a = _address;
            Use(code, munin, true);
            string[] wordy = { Lang.T("небагатослівний"), Lang.T("звичайно говорить"), Lang.T("балакучий") };
            string call = _call > 0 && (munin ? MuninCalls : HuginCalls)[_call] != null ? Lang.F("кличе тебе «{0}»", Grammar.Fix((munin ? MuninCalls : HuginCalls)[_call], Player.m_localPlayer)) : Lang.T("кличе на ім'я");
            string open = _open > 0 && (munin ? MuninOpen : HuginOpen)[_open] != null ? Lang.F("починає з «{0}»", (munin ? MuninOpen : HuginOpen)[_open]) : Lang.T("без примовки на початку");
            string end = _end > 0 && (munin ? MuninEnd : HuginEnd)[_end] != null ? Lang.F("закінчує «{0}»", (munin ? MuninEnd : HuginEnd)[_end]) : Lang.T("без примовки в кінці");
            string res = wordy[System.Array.IndexOf(Wordiness, Wordy)] + ", " + call + ", " + open + ", " + end + (munin ? Lang.T(", лається своїм набором") : "");
            Active = wasActive; Wordy = wasWordy; _call = c; _open = o; _end = e; _seed = sd; _munin = m; _address = a;
            return res;
        }
    }
}
#endif

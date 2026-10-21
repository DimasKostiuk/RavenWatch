using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The mod's languages. The code is written in Ukrainian (the source text is the key, as in gettext): Lang.T("…")
    // gives that text in the language the game is set to. A language is a folder of tables:
    //   built in   - assets/lang/<Language>/ui.txt, embedded in the dll (RavenWatch.lang.<Language>.ui.txt);
    //   by players - config/RavenWatch/lang/<Language>/ui.txt (overrides the built-in lines, or adds a whole language).
    // <Language> is the game's own name for it (English, Polish, German…). Ukrainian needs no table: it is the source.
    // A language with no line for a text falls back to English, and English with none to the source.
    // ui.txt: one entry per line «source<TAB>translation», \n and \t written as escapes, # starts a comment.
    // Plural forms are one entry, the forms joined by '|': «день|дні|днів<TAB>day|days» (Lang.P picks by the count).
    internal static class Lang
    {
        internal const string Source = "Ukrainian", Fallback = "English";
        internal static string Forced = ""; // the config's choice ("" or "Auto": the game's language)

        private static string _lang;
        private static Dictionary<string, string> _table = new Dictionary<string, string>(), _fallback = new Dictionary<string, string>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        internal static event Action Changed;
        internal static Action<string> Logger; // set by the plugin (the core stays free of it: tools/LangTest.cs runs it outside the game)
        private static void Say(string text) { if (Logger != null) Logger(text); }

        // the language the mod speaks now
        internal static string Current { get { if (_lang == null) Reload(); return _lang; } }
        internal static bool IsSource { get { return Current == Source; } }

        internal static string GameLanguage()
        {
            try { return Localization.instance != null ? Localization.instance.GetSelectedLanguage() : Fallback; }
            catch { return Fallback; }
        }

        // reads the tables again (start, the game's language changed, the config changed)
        internal static void Reload()
        {
            string want = Forced != null && Forced.Trim().Length > 0 && !Forced.Trim().Equals("Auto", StringComparison.OrdinalIgnoreCase) ? Forced.Trim() : GameLanguage();
            if (string.IsNullOrEmpty(want)) want = Fallback;
            bool changed = _lang != null && _lang != want;
            _lang = want;
            _phraseLang = null; // (asked again: PhraseLanguage)
            _english = null; Made.Clear(); LinesCache.Clear(); // (tables read again: made anew)
            _table =want == Source ? new Dictionary<string, string>() : Table(want);
            _fallback = want == Source || want == Fallback ? new Dictionary<string, string>() : Table(Fallback);
            Missing.Clear();
            Say("Language: " + want + " (" + _table.Count + " lines" + (_fallback.Count > 0 ? ", English under it " + _fallback.Count : "") + ")");
            ApplyWords();
            if (changed && Changed != null)
                foreach (Delegate d in Changed.GetInvocationList())
                {
                    try { ((Action)d)(); }
                    catch (Exception e) { Say("Language change: " + e.Message); }
                }
        }

        // ---------------------------------------------------------------- the texts

        // While a raven's line is put together (Composer, Patterns…), every text asked for goes into the phrases'
        // language, so a value put into a line (a land, a day, a boss) is in the line's own language. Everything below
        // speaks the Target language: the menus' one, or the phrases' one inside a line.
        private static int _phraseDepth;
        internal sealed class PhraseScope : IDisposable { public void Dispose() { if (_phraseDepth > 0) _phraseDepth--; } }
        private static readonly PhraseScope Scope = new PhraseScope();
        internal static IDisposable Phrase() { _phraseDepth++; return Scope; }

        private static string Target
        {
            get
            {
                if (_lang == null) Reload();
                return _phraseDepth > 0 ? PhraseLanguage : _lang;
            }
        }

        // the text in the current language
        internal static string T(string source) { return In(source, Target); }

        // a text in the language of the ravens' phrases (it may differ from the menus': a language with menus of its own
        // but no phrases speaks English ones). For words looked for in the phrases, and words glued onto them.
        internal static string TP(string source) { return In(source, PhraseLanguage); }

        // the text in English whatever the language (the config file is English for everyone)
        internal static string En(string source) { return In(source, Fallback); }

        // a text in a language: the source itself, the current table (with English under it), or the English one
        private static Dictionary<string, string> _english;
        private static string In(string source, string language)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (_lang == null) Reload();
            if (language == Source) return source;
            string s;
            if (language == _lang)
            {
                if (_table.TryGetValue(source, out s) || _fallback.TryGetValue(source, out s)) return s;
                if (Missing.Count < 200 && Missing.Add(source)) Say("Language " + _lang + ": no line for «" + Short(source) + "»"); // (capped: a text made on the fly would grow it forever)
                return source;
            }
            // (another language is only ever English: the phrases' fallback, or the config's)
            if (_english == null) _english = _lang == Fallback ? _table : Table(Fallback);
            return _english.TryGetValue(source, out s) ? s : source;
        }

        // the text with its blanks filled ({0}, {1}… as string.Format; numbers as the language writes them)
        internal static string F(string source, params object[] args)
        {
            string t = T(source);
            try { return string.Format(Numbers, t, args); }
            catch (FormatException)
            {
                Say("Language " + _lang + ": bad blanks in «" + Short(t) + "»");
                try { return string.Format(Numbers, source, args); } catch { return source; }
            }
        }

        // how a value in a blank is written: the language's decimal mark (1,5 / 1.5), nothing else changed
        private static NumberFormatInfo _comma;
        private static IFormatProvider Numbers
        {
            get
            {
                if (!DecimalComma) return CultureInfo.InvariantCulture;
                if (_comma == null) { _comma = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone(); _comma.NumberDecimalSeparator = ","; }
                return _comma;
            }
        }

        // the form for a count: forms "one|few|many" (Ukrainian source); the translation may have fewer forms
        // ("one|other" in English), the rule is picked by how many there are
        internal static string P(int n, string forms)
        {
            string[] f = T(forms).Split('|');
            return f[PluralIndex(n, f.Length)];
        }

        // the count and its word: "3 дні" / "3 days"
        internal static string N(int n, string forms) { return n.ToString(CultureInfo.InvariantCulture) + " " + P(n, forms); }

        // a list of texts kept in a field (read once): marked for the tools, translated where it is read (Arr)
        internal static string[] Keys(params string[] source) { return source; }
        internal static string K(string source) { return source; }

        // a field's list or table of texts in the current language, made once per language (and per target: a list
        // read inside a raven's line is the phrases' one)
        private static readonly Dictionary<string, Dictionary<object, object>> Made = new Dictionary<string, Dictionary<object, object>>();
        private static Dictionary<object, object> MadeFor(string language)
        {
            Dictionary<object, object> d;
            if (!Made.TryGetValue(language, out d)) { d = new Dictionary<object, object>(); Made[language] = d; }
            return d;
        }

        internal static string[] Arr(string[] source) { return ArrIn(source, Target); }
        internal static string[] ArrP(string[] source) { return ArrIn(source, PhraseLanguage); }
        private static string[] ArrIn(string[] source, string language)
        {
            if (source == null || language == Source) return source;
            Dictionary<object, object> made = MadeFor(language);
            object o;
            if (made.TryGetValue(source, out o)) return (string[])o;
            string[] t = new string[source.Length];
            for (int i = 0; i < source.Length; i++) t[i] = source[i] == null ? null : In(source[i], language);
            made[source] = t;
            return t;
        }

        // A language need not have a word for each Ukrainian one: «-» as the translation means «none in this language».
        // Habits(): a list where a place may stay empty (a raven keeps its number in it: the habit then just is not there);
        // Pool(): words picked at random, the missing ones left out. Both in the phrases' language (they are speech).
        internal const string None = "-";
        internal static string[] Habits(string[] source)
        {
            string[] t = ArrIn(source, PhraseLanguage);
            if (t == null || Array.IndexOf(t, None) < 0) return t;
            string[] h = (string[])t.Clone();
            for (int i = 0; i < h.Length; i++) if (h[i] == None) h[i] = null;
            return h;
        }
        internal static string[] Pool(string[] source)
        {
            string[] t = ArrIn(source, PhraseLanguage);
            if (t == null) return null;
            List<string> l = new List<string>(t.Length);
            foreach (string s in t) if (!string.IsNullOrEmpty(s) && s != None) l.Add(s);
            return l.Count > 0 ? (l.Count == t.Length ? t : l.ToArray()) : t; // (all «-»: a slip, the words kept)
        }

        internal static Dictionary<string, string> Map(Dictionary<string, string> source)
        {
            string language = Target;
            if (source == null || language == Source) return source;
            Dictionary<object, object> made = MadeFor(language);
            object o;
            if (made.TryGetValue(source, out o)) return (Dictionary<string, string>)o;
            Dictionary<string, string> t = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in source) t[kv.Key] = In(kv.Value, language);
            made[source] = t;
            return t;
        }
        internal static Dictionary<string, string[]> Map(Dictionary<string, string[]> source)
        {
            string language = Target;
            if (source == null || language == Source) return source;
            Dictionary<object, object> made = MadeFor(language);
            object o;
            if (made.TryGetValue(source, out o)) return (Dictionary<string, string[]>)o;
            Dictionary<string, string[]> t = new Dictionary<string, string[]>();
            foreach (KeyValuePair<string, string[]> kv in source) t[kv.Key] = ArrIn(kv.Value, language);
            made[source] = t;
            return t;
        }

        // numbers as the language writes them: 1,5 or 1.5; 1 514 or 1,514
        private static readonly HashSet<string> CommaLanguages = new HashSet<string> { "Ukrainian", "Russian", "German", "French", "Spanish", "Italian", "Portuguese_European", "Portuguese_Brazilian", "Polish", "Czech", "Slovak", "Dutch", "Swedish", "Norwegian", "Danish", "Finnish", "Turkish", "Hungarian", "Romanian", "Bulgarian", "Serbian", "Croatian", "Lithuanian", "Latvian", "Estonian", "Greek", "Georgian" };
        internal static bool DecimalComma { get { return CommaLanguages.Contains(Target); } }

        // languages whose nouns change by case (the ravens' lines then ask for {x_gen}, {x_ins}…); in the others a name stays
        // the same, so a creature the names file does not know still fills those blanks with its plain name
        private static readonly HashSet<string> CaseLanguages = new HashSet<string> { "Ukrainian", "Russian", "Polish", "Czech", "Slovak", "Serbian", "Croatian", "Slovenian", "Lithuanian", "Latvian", "Estonian", "Finnish", "Hungarian", "German", "Greek", "Turkish", "Georgian", "Armenian" };
        internal static bool Cases { get { return CaseLanguages.Contains(Target); } }
        // letter case, the same on every computer (never the system's culture: Turkish i), and nouns left as they are
        // where the language writes them with a capital (German)
        private static readonly HashSet<string> NounCapitals = new HashSet<string> { "German", "Luxembourgish" };
        internal static string Lower(string s) { return string.IsNullOrEmpty(s) || NounCapitals.Contains(Target) ? s : s.ToLowerInvariant(); }
        internal static string LowerFirst(string s) { return string.IsNullOrEmpty(s) || NounCapitals.Contains(Target) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1); }
        internal static string UpperFirst(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        internal static string Num(float v, string format)
        {
            string s = v.ToString(format, CultureInfo.InvariantCulture);
            return DecimalComma ? s.Replace('.', ',') : s;
        }
        internal static string Int(long n)
        {
            string s = n.ToString("#,0", CultureInfo.InvariantCulture);
            return DecimalComma ? s.Replace(',', ' ') : s;
        }

        internal static int PluralIndex(int n, int forms)
        {
            if (forms <= 1) return 0;
            n = Math.Abs(n);
            if (forms == 2) return n == 1 ? 0 : 1; // English, German, Spanish…
            int d = n % 10, h = n % 100; // the Slavic three: 1, 2–4, the rest (11–14 too)
            if (d == 1 && h != 11) return 0;
            if (d >= 2 && d <= 4 && (h < 12 || h > 14)) return 1;
            return 2;
        }

        // ---------------------------------------------------------------- names the game shows itself

        // A name the game puts on screen by itself (an item, a status effect): "$rw_<key>", registered in the game's
        // own dictionary in the current language (and again whenever the language changes)
        private static readonly Dictionary<string, Func<string>> Words = new Dictionary<string, Func<string>>();
        private static MethodInfo _addWord;

        internal static string Word(string key, string source) { WordSources["$" + key] = source; return WordF(key, () => T(source)); }

        // Before the words, these names were the Ukrainian text itself, and a character's save remembers its recipes,
        // materials and stations by name: a loaded character learns each word's name too (nothing is taken away, so an
        // older version still finds the old names)
        private static readonly Dictionary<string, string> WordSources = new Dictionary<string, string>();
        internal static void MigrateKnown(Player p)
        {
            if (p == null || WordSources.Count == 0) return;
            try
            {
                HashSet<string> recipes = AccessTools.Field(typeof(Player), "m_knownRecipes").GetValue(p) as HashSet<string>;
                HashSet<string> materials = AccessTools.Field(typeof(Player), "m_knownMaterial").GetValue(p) as HashSet<string>;
                Dictionary<string, int> stations = AccessTools.Field(typeof(Player), "m_knownStations").GetValue(p) as Dictionary<string, int>;
                int n = 0;
                foreach (KeyValuePair<string, string> kv in WordSources)
                {
                    if (recipes != null && recipes.Contains(kv.Value) && recipes.Add(kv.Key)) n++;
                    if (materials != null && materials.Contains(kv.Value) && materials.Add(kv.Key)) n++;
                    int level;
                    if (stations != null && stations.TryGetValue(kv.Value, out level) && !stations.ContainsKey(kv.Key)) { stations[kv.Key] = level; n++; }
                }
                if (n > 0) Say("Known names carried over to the game words: " + n);
            }
            catch (Exception e) { Say("Known names: " + e.Message); }
        }

        // the same for a text made at the moment (numbers from the settings in it): made again for a new language
        internal static string WordF(string key, Func<string> make)
        {
            Words[key] = make;
            string text;
            try { text = make(); } catch { text = ""; }
            AddWord(key, text);
            return "$" + key;
        }

        // a name with a key of its own made from the text ("$rw_w1a2b3c4d")
        internal static string W(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            return Word("rw_w" + ((uint)source.GetStableHashCode()).ToString("x8"), source);
        }

        private static void ApplyWords()
        {
            foreach (KeyValuePair<string, Func<string>> kv in Words)
            {
                string text;
                try { text = kv.Value(); } catch { text = ""; }
                AddWord(kv.Key, text);
            }
        }

        private static void AddWord(string key, string text)
        {
            try
            {
                if (_addWord == null) _addWord = AccessTools.Method(typeof(Localization), "AddWord", new[] { typeof(string), typeof(string) });
                if (_addWord != null && Localization.instance != null) _addWord.Invoke(Localization.instance, new object[] { key, text });
            }
            catch (Exception e) { Say("Language word " + key + ": " + e.Message); }
        }

        // the game cleared its dictionary for a new language: our words back in, our tables read again
        internal static void OnGameLanguage() { Reload(); }

        // ---------------------------------------------------------------- the game's own words ($item_…, $enemy_…)

        // A game name ("$enemy_troll", an item's m_name) in the language spoken now: the game's own dictionary when
        // the mod speaks the game's language; else the game's word lists read for that language (once), so an English
        // line set by the mod's config in a Ukrainian game says «Troll», not «Троль». Words the lists don't have
        // (another mod's) come from the game's dictionary as they are.
        private static readonly Dictionary<string, Dictionary<string, string>> GameTables = new Dictionary<string, Dictionary<string, string>>();
        private static readonly char[] EndChars = " (){}[]+-!?/\\\\&%,.:-=<>\n".ToCharArray(); // (the game's own)

        internal static string Game(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Localization loc;
            try { loc = Localization.instance; } catch { return text; }
            if (loc == null) return text;
            string want = Target;
            if (text.IndexOf('$') < 0 || want == GameLanguage()) return loc.Localize(text);
            Dictionary<string, string> d = GameTable(want);
            if (d == null) return loc.Localize(text);
            StringBuilder sb = new StringBuilder(text.Length + 16);
            int from = 0;
            while (from < text.Length)
            {
                int at = text.IndexOf('$', from);
                if (at < 0) break;
                sb.Append(text, from, at - from);
                int end = text.IndexOfAny(EndChars, at);
                if (end < 0) end = text.Length;
                string word = text.Substring(at + 1, end - at - 1);
                string w;
                if (!word.StartsWith("KEY_", StringComparison.Ordinal) && d.TryGetValue(word, out w)) sb.Append(w);
                else sb.Append(loc.Localize("$" + word));
                from = end;
            }
            if (from < text.Length) sb.Append(text, from, text.Length - from);
            return sb.ToString();
        }

        private static Dictionary<string, string> GameTable(string language)
        {
            Dictionary<string, string> d;
            if (GameTables.TryGetValue(language, out d)) return d;
            d = null;
            try
            {
                FieldInfo sf = AccessTools.Field(typeof(Localization), "m_localizationSettings");
                MethodInfo split = AccessTools.Method(typeof(Localization), "DoQuoteLineSplit");
                LocalizationSettings settings = sf != null ? sf.GetValue(null) as LocalizationSettings : null;
                if (settings != null && split != null)
                    foreach (TextAsset file in settings.Localizations)
                    {
                        if (file == null) continue;
                        StringReader reader = new StringReader(file.text);
                        string[] head = reader.ReadLine().Split(',');
                        int col = -1;
                        for (int i = 0; i < head.Length; i++) if (head[i].Trim().Trim('"') == language) { col = i; break; }
                        if (col < 0) continue;
                        if (d == null) d = new Dictionary<string, string>();
                        List<List<string>> rows = (List<List<string>>)split.Invoke(Localization.instance, new object[] { reader });
                        foreach (List<string> row in rows)
                        {
                            if (row.Count == 0 || row[0].Length == 0 || row[0].StartsWith("//")) continue;
                            string v = row.Count > col ? row[col].Trim() : "";
                            if (v.Length == 0 || v[0] == '\r') v = row.Count > 1 ? row[1] : "";
                            d[row[0]] = v;
                        }
                    }
                Say("Game words in " + language + ": " + (d != null ? d.Count + " read" : "the game has no such language, its own dictionary used"));
            }
            catch (Exception e) { Say("Game words in " + language + ": " + e.Message); d = null; }
            GameTables[language] = d;
            return d;
        }

        // ---------------------------------------------------------------- the ravens' phrases

        // The phrase files (hugin_patterns.txt and the rest) are written per language: Ukrainian (the source) in
        // config/RavenWatch as always, any other in config/RavenWatch/lang/<Language>/, made from that language's
        // built-in files (assets/lang/<Language>/<file>, embedded). A language with no phrases of its own speaks
        // English ones; while English has none either, the Ukrainian files are used (never a mix in one file).
        internal static string PhraseLanguage
        {
            get
            {
                if (_lang == null) Reload();
                if (_phraseLang == null) _phraseLang = HasPhrases(_lang) ? _lang : HasPhrases(Fallback) ? Fallback : Source;
                return _phraseLang;
            }
        }
        private static string _phraseLang, _phraseDirFor, _phraseDir;

        internal static bool HasPhrases(string language)
        {
            if (language == Source) return true;
            if (typeof(Lang).Assembly.GetManifestResourceInfo("RavenWatch.lang." + language + ".hugin_patterns.txt") != null) return true;
            try { return File.Exists(Path.Combine(LangDir(language), "hugin_patterns.txt")); } catch { return false; }
        }

        // where the phrase files of the current language live (made when missing)
        internal static string PhraseDir(string root)
        {
            if (PhraseLanguage == Source) return root;
            if (_phraseDirFor == PhraseLanguage) return _phraseDir; // (made once: asked for every line)
            string dir = LangDir(PhraseLanguage);
            try { Directory.CreateDirectory(dir); } catch (Exception e) { Say("Language folder " + dir + ": " + e.Message); return root; }
            _phraseDirFor = PhraseLanguage; _phraseDir = dir;
            return dir;
        }

        // the built-in lines of a phrase file in the current phrase language. neutral: the file holds no words (scenes
        // of tags), the same in every language. A language without that file gets nothing rather than Ukrainian.
        internal static string[] Lines(string file, string[] source, bool neutral = false)
        {
            string pl = PhraseLanguage;
            if (pl == Source) return source;
            string[] got;
            string key = pl + "/" + file;
            if (LinesCache.TryGetValue(key, out got)) return got ?? source;
            got = MakeLines(pl, file, neutral);
            LinesCache[key] = got;
            return got ?? source;
        }
        private static readonly Dictionary<string, string[]> LinesCache = new Dictionary<string, string[]>();

        private static string[] MakeLines(string pl, string file, bool neutral)
        {
            string text = Embedded(pl, file);
            if (text == null && pl != Fallback) text = Embedded(Fallback, file);
            if (text == null)
            {
                if (neutral) return null; // (null: the source lines)
                Say("Language " + pl + ": no built-in " + file);
                return new string[0];
            }
            List<string> l = new List<string>();
            foreach (string raw in text.Split('\n')) l.Add(raw.TrimEnd('\r'));
            while (l.Count > 0 && l[l.Count - 1].Length == 0) l.RemoveAt(l.Count - 1);
            return l.ToArray();
        }

        // ---------------------------------------------------------------- the tables

        internal static string LangDir(string language) { return Path.Combine(Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "lang"), language); }

        private static Dictionary<string, string> Table(string language)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            Read(d, Embedded(language, "ui.txt"), "built-in " + language);
            string file = Path.Combine(LangDir(language), "ui.txt");
            try { if (File.Exists(file)) Read(d, File.ReadAllText(file, Encoding.UTF8), file); }
            catch (Exception e) { Say("Language file " + file + ": " + e.Message); }
            return d;
        }

        // a text file built into the dll for a language (null: none)
        internal static string Embedded(string language, string file)
        {
            try
            {
                using (Stream st = typeof(Lang).Assembly.GetManifestResourceStream("RavenWatch.lang." + language + "." + file))
                {
                    if (st == null) return null;
                    using (StreamReader r = new StreamReader(st, Encoding.UTF8)) return r.ReadToEnd();
                }
            }
            catch { return null; }
        }

        private static void Read(Dictionary<string, string> d, string text, string from)
        {
            if (string.IsNullOrEmpty(text)) return;
            int bad = 0;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab == line.Length - 1) continue; // not translated yet: the source shows
                if (tab <= 0) { bad++; continue; }
                d[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
            }
            if (bad > 0) Say("Language " + from + ": " + bad + " line(s) without a tab or a translation skipped");
        }

        internal static string Escape(string s) { return s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r"); }

        internal static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Short(string s) { s = s.Replace("\n", " "); return s.Length > 80 ? s.Substring(0, 80) + "…" : s; }
    }

    // the game's language changed in its settings
    [HarmonyPatch(typeof(Localization), "SetLanguage")]
    internal static class LangGamePatch
    {
        private static void Postfix() { Lang.OnGameLanguage(); }
    }

    // a character loaded: the names it knew from before the game words
    [HarmonyPatch(typeof(Player), "Load")]
    internal static class LangKnownPatch
    {
        private static void Postfix(Player __instance) { Lang.MigrateKnown(__instance); }
    }

    // the main menu: the game's settings are surely read by now (the mod starts before them): the language looked at again
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class LangMenuPatch
    {
        private static void Postfix() { Lang.OnGameLanguage(); }
    }
}

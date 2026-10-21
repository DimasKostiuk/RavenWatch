// RavenWatch: checks the language core (Lang) outside the game, on the built dll, with a made-up table.
//   tools/langtest.ps1
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal static class LangTest
{
    private static readonly List<string> Dirs = new List<string>();
    private static Type _lang;
    private static int _fail, _pass;

    private static int Main(string[] args)
    {
        for (int i = 1; i < args.Length; i++) Dirs.Add(args[i]);
        Dirs.Add(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string d in Dirs) { string p = Path.Combine(d, name); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        Assembly a = Assembly.LoadFrom(args[0]);
        _lang = a.GetType("HuginWhispers.Lang", true);

        // a config folder of our own, with an English table and a Polish one (Polish: menus only, no phrases)
        string cfg = Path.Combine(Path.GetTempPath(), "rw_langtest");
        if (Directory.Exists(cfg)) Directory.Delete(cfg, true);
        Directory.CreateDirectory(Path.Combine(cfg, "RavenWatch", "lang", "English"));
        Directory.CreateDirectory(Path.Combine(cfg, "RavenWatch", "lang", "Polish"));
        File.WriteAllText(Path.Combine(cfg, "RavenWatch", "lang", "English", "ui.txt"),
            "# test\n" +
            "Хугін\tHugin\n" +
            "ще {0} с\t{0} s left\n" +
            "день|дні|днів\tday|days\n" +
            "Левади\tMeadows\n" +
            "рядок\\nдругий\tline\\nsecond\n" +
            "\\#хеш\t#hash\n" +
            "Сила {0}\tStrength of {0}\n" +
            "{0} гравця {1}\t{1}'s {0}\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(cfg, "RavenWatch", "lang", "Polish", "ui.txt"), "Хугін\tHugin-pl\nЛевади\tŁąki\n", new UTF8Encoding(false));
        Type paths = a.GetReferencedAssemblies().Length > 0 ? Type.GetType("BepInEx.Paths, BepInEx", true) : null;
        PropertyInfo cp = paths.GetProperty("ConfigPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        cp.SetValue(null, cfg, null);

        // Ukrainian: the source as it is
        Force("Ukrainian");
        Eq("uk T", Call("T", "Хугін"), "Хугін");
        Eq("uk F", Call("F", "ще {0} с", new object[] { 5 }), "ще 5 с");
        Eq("uk F float", Call("F", "ще {0} с", new object[] { 1.5f }), "ще 1,5 с");
        Eq("uk N 21", Call("N", 21, "день|дні|днів"), "21 день");
        Eq("uk N 3", Call("N", 3, "день|дні|днів"), "3 дні");
        Eq("uk N 12", Call("N", 12, "день|дні|днів"), "12 днів");
        Eq("uk Num", Call("Num", 2.5f, "0.#"), "2,5");
        Eq("uk Int", Call("Int", 15140L), "15 140");

        // English
        Force("English");
        Eq("en T", Call("T", "Хугін"), "Hugin");
        Eq("en T missing", Call("T", "нема такого"), "нема такого");
        Eq("en F", Call("F", "ще {0} с", new object[] { 5 }), "5 s left");
        Eq("en F float", Call("F", "ще {0} с", new object[] { 1.5f }), "1.5 s left");
        Eq("en F order", Call("F", "{0} гравця {1}", new object[] { "Кусака", "Sigurd" }), "Sigurd's Кусака");
        Eq("en N 1", Call("N", 1, "день|дні|днів"), "1 day");
        Eq("en N 21", Call("N", 21, "день|дні|днів"), "21 days");
        Eq("en escapes", Call("T", "рядок\nдругий"), "line\nsecond");
        Eq("en hash", Call("T", "#хеш"), "#hash");
        Eq("en Num", Call("Num", 2.5f, "0.#"), "2.5");
        Eq("en Int", Call("Int", 15140L), "15,140");
        string[] src = (string[])Call("Keys", (object)new string[] { "Хугін", null, "Левади" });
        string[] arr = (string[])Call("Arr", (object)src);
        Eq("en Arr", arr[0] + "|" + (arr[1] ?? "null") + "|" + arr[2], "Hugin|null|Meadows");
        Eq("en Arr cached", ReferenceEquals(arr, Call("Arr", (object)src)) ? "same" : "new", "same");
        Eq("en En", Call("En", "Левади"), "Meadows");
        Eq("en phrases", (string)Prop("PhraseLanguage"), "English"); // (English phrases are built in)

        // Polish: its menus; no Polish phrases, so the phrases are English
        Force("Polish");
        Eq("pl T", Call("T", "Хугін"), "Hugin-pl");
        Eq("pl T from English", Call("T", "ще {0} с"), "{0} s left");
        Eq("pl F float comma", Call("F", "ще {0} с", new object[] { 1.5f }), "1,5 s left");
        Eq("pl En", Call("En", "Левади"), "Meadows");
        Eq("pl TP", Call("TP", "Левади"), "Meadows"); // phrases in English (Polish has none)
        string[] arrPl = (string[])Call("Arr", (object)src);
        using ((IDisposable)Call("Phrase"))
        {
            Eq("pl in phrase T", Call("T", "Левади"), "Meadows");
            string[] arrIn = (string[])Call("Arr", (object)src);
            Eq("pl in phrase Arr", arrIn[2], "Meadows");
        }
        Eq("pl Arr not spoiled", ((string[])Call("Arr", (object)src))[2], "Łąki");
        Eq("pl Arr same", ReferenceEquals(arrPl, Call("Arr", (object)src)) ? "same" : "new", "same");

        // a broken translation: the source's blanks used instead, nothing thrown
        File.AppendAllText(Path.Combine(cfg, "RavenWatch", "lang", "Polish", "ui.txt"), "Сила {0}\tSiła {1}\n");
        Force("English"); Force("Polish");
        Eq("pl bad blanks", Call("F", "Сила {0}", new object[] { "X" }), "Сила X");

        // English phrases appear (a hugin_patterns.txt of its own): the phrases switch to English; «-» = no such word
        File.WriteAllText(Path.Combine(cfg, "RavenWatch", "lang", "English", "hugin_patterns.txt"), "[x] y\n", new UTF8Encoding(false));
        File.AppendAllText(Path.Combine(cfg, "RavenWatch", "lang", "English", "ui.txt"), "Хм.\t-\nНу.\tWell.\nТьху.\t-\n");
        Force("English");
        Eq("en phrases now", (string)Prop("PhraseLanguage"), "English");
        string[] habits = (string[])Call("Habits", (object)new string[] { null, "Хм.", "Ну." });
        Eq("en Habits", (habits[0] ?? "null") + "|" + (habits[1] ?? "null") + "|" + habits[2], "null|null|Well.");
        string[] pool = (string[])Call("Pool", (object)new string[] { "Хм.", "Ну.", "Тьху." });
        Eq("en Pool", string.Join("|", pool), "Well.");
        string[] allGone = (string[])Call("Pool", (object)new string[] { "Хм.", "Тьху." });
        Eq("en Pool all gone keeps", allGone.Length.ToString(), "2");
        Force("Polish");
        Eq("pl phrases English", (string)Prop("PhraseLanguage"), "English");
        Eq("pl TP English", Call("TP", "Левади"), "Meadows");
        using ((IDisposable)Call("Phrase")) { Eq("pl in phrase English", Call("T", "Левади"), "Meadows"); }
        Eq("pl out of phrase Polish", Call("T", "Левади"), "Łąki");

        Console.WriteLine(_pass + " passed, " + _fail + " failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Force(string language)
    {
        _lang.GetField("Forced", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).SetValue(null, language);
        _lang.GetMethod("Reload", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Invoke(null, null);
    }

    private static object Prop(string name) { return _lang.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null, null); }

    private static object Call(string name, params object[] args)
    {
        foreach (MethodInfo m in _lang.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (m.Name != name) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != args.Length) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++) if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) ok = false;
            if (!ok) continue;
            return m.Invoke(null, args);
        }
        throw new Exception("no Lang." + name + " for these arguments");
    }

    private static void Eq(string what, object got, string want)
    {
        string g = got as string ?? (got == null ? "null" : got.ToString());
        if (g == want) { _pass++; return; }
        _fail++;
        Console.WriteLine("FAIL " + what + ": got «" + g.Replace("\n", "\\n") + "», want «" + want.Replace("\n", "\\n") + "»");
    }
}

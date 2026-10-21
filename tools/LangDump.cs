// RavenWatch: the built-in phrase files of the source language (Ukrainian), straight from the built dll, one file each
// as the mod writes them (without its header lines) — the text to translate into assets/lang/<Language>/<file>.
//   tools/langdump.ps1  (compiles this and runs it on build/RavenWatch-<version>.dll; output: build/l10n_source/)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal static class LangDump
{
    private static readonly List<string> Dirs = new List<string>();

    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("LangDump <RavenWatch.dll> <out dir> [reference dirs...]"); return 1; }
        for (int i = 2; i < args.Length; i++) Dirs.Add(args[i]);
        Dirs.Add(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string d in Dirs) { string p = Path.Combine(d, name); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        Assembly a = Assembly.LoadFrom(args[0]);
        string outDir = args[1];
        Directory.CreateDirectory(outDir);
        Dump(outDir, "hugin_patterns.txt", Call(a, "HuginWhispers.Patterns", "All", false));
        Dump(outDir, "munin_patterns.txt", Call(a, "HuginWhispers.Patterns", "All", true));
        Dump(outDir, "hugin_titles.txt", Field(a, "HuginWhispers.Defaults", "HuginTitles"));
        Dump(outDir, "hugin_texts.txt", Field(a, "HuginWhispers.Defaults", "HuginTexts"));
        Dump(outDir, "munin_titles.txt", Field(a, "HuginWhispers.Defaults", "MuninTitles"));
        Dump(outDir, "munin_texts.txt", Field(a, "HuginWhispers.Defaults", "MuninTexts"));
        Dump(outDir, "hugin_event_titles.txt", Field(a, "HuginWhispers.DefaultsEvents", "Titles"));
        Dump(outDir, "hugin_event_texts.txt", Field(a, "HuginWhispers.DefaultsEvents", "Texts"));
        Dump(outDir, "hugin_tails.txt", Field(a, "HuginWhispers.DefaultsEvents", "Tails"));
        Dump(outDir, "creature_names.txt", Field(a, "HuginWhispers.DefaultsEvents", "CreatureNames"));
        Dump(outDir, "hugin_quips.txt", Field(a, "HuginWhispers.Quips", "Hugin"));
        Dump(outDir, "munin_quips.txt", Field(a, "HuginWhispers.Quips", "Munin"));
        Dump(outDir, "dialogues.txt", Field(a, "HuginWhispers.RavenTalk", "Defaults"));
        return 0;
    }

    private static string[] Field(Assembly a, string type, string name)
    {
        Type t = a.GetType(type, true);
        FieldInfo f = t.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) throw new Exception(type + "." + name + " not found");
        return (string[])f.GetValue(null);
    }

    private static string[] Call(Assembly a, string type, string name, bool arg)
    {
        Type t = a.GetType(type, true);
        MethodInfo m = t.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (m == null) throw new Exception(type + "." + name + " not found");
        return (string[])m.Invoke(null, new object[] { arg });
    }

    private static void Dump(string dir, string file, string[] lines)
    {
        File.WriteAllText(Path.Combine(dir, file), string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        int words = 0;
        foreach (string l in lines) if (!l.StartsWith("#")) words += l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        Console.WriteLine(file + ": " + lines.Length + " lines, ~" + words + " words");
    }
}

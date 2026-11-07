#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HuginWhispers
{
    // The story's text files, one place for all of them (the user 2026-11-02: one source, nothing to carry over before a
    // release). Kinds: quests, scenes, npc, talk — each a folder of *.txt in the mod's assets/story/<kind> (built in as
    // RavenWatch.<kind>.<file>). Which text is used, in this order, the later winning:
    //   1. built in                                   — every player
    //   2. the mod's own assets/story/<kind>          — the author's machine: config/RavenWatch/story/source.txt holds the
    //                                                   path of that assets/story folder; read there, saved there
    //   3. else the old author folder (config/…)     — only with no source: as before (an admin's tuning copies)
    // The editors (F9 «Сюжет», «Сцени», «Вигляд») write where the text is read: the source when there is one.
    // An npc file whose name starts with «_» is the author's own (the animation stage's mannequin, test people): read from the source on
    // his machine, never built in (rw_build leaves it out of assets/story/npc), so players never get it; storycheck stops a quest or scene naming it.
    internal static class StoryFiles
    {
        internal const string Quests = "quests", Scenes = "scenes", Npc = "npc", Talk = "talk";
        private static string _source;
        private static bool _read;

        private static string SourceFile { get { return Path.Combine(Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "story"), "source.txt"); } }

        // the mod's own assets/story folder (null — not the author's machine, or the path is not there)
        internal static string Source
        {
            get
            {
                if (_read) return _source;
                _read = true; _source = null;
                try
                {
                    if (File.Exists(SourceFile))
                    {
                        string d = File.ReadAllText(SourceFile, Encoding.UTF8).Trim().TrimEnd('/', '\\'); // (a trailing slash: its parent would be itself — gaits, photos)
                        if (d.Length > 0 && Directory.Exists(d)) _source = d;
                        else Story.Log("files: source.txt names «" + d + "» — no such folder: the built-in files are used");
                    }
                }
                catch (Exception e) { Story.Log("files: source.txt: " + e.Message); }
                return _source;
            }
        }

        internal static void Again() { _read = false; } // (read source.txt anew)

#if RAVEN_STORY
        // every file of the story read anew (the windows' «Перечитати», the console): one list, so none is forgotten
        internal static void ReloadAll()
        {
            Again();
            StoryLooks.Reload();
            StoryPeople.Reload();
            Locs.Load();
            LocParts.Reread();
            LocState.Again();
            StoryCond.ForgetWrong();
            StoryTrackConds.Register();
            StoryScenes.Reload(); // (its shots, its sounds)
            StoryQuests.Reload(); // (its saga; checked against the scenes just read)
            StoryTales.Reload();
        }
#endif

        private static string Dir(string kind) { string s = Source; return s != null ? Path.Combine(s, kind) : null; }

        // every file of a kind: name → text (the order above)
        internal static Dictionary<string, string> All(string kind, string legacy, bool legacyOn)
        {
            Dictionary<string, string> files = new Dictionary<string, string>();
            string pre = "RavenWatch." + kind + ".";
            System.Reflection.Assembly asm = typeof(StoryFiles).Assembly;
            foreach (string r in asm.GetManifestResourceNames())
            {
                if (!r.StartsWith(pre) || !r.EndsWith(".txt")) continue;
                try { using (Stream st = asm.GetManifestResourceStream(r)) using (StreamReader rd = new StreamReader(st, Encoding.UTF8)) files[r.Substring(pre.Length)] = rd.ReadToEnd(); }
                catch (Exception e) { Story.Log("files: " + r + ": " + e.Message); }
            }
            string over = Dir(kind) ?? (legacyOn ? legacy : null);
            if (over != null && Directory.Exists(over))
                foreach (string f in Directory.GetFiles(over, "*.txt"))
                {
                    try { files[Path.GetFileName(f)] = File.ReadAllText(f, Encoding.UTF8); }
                    catch (Exception e) { Story.Log("files: " + f + ": " + e.Message); }
                }
            return files;
        }

        // a story file's lines that say something: «# …» and « #…» after a line are notes, blank ones nothing; each with
        // its number in the file (the errors name it)
        internal static string Bare(string raw) { int note = raw.IndexOf(" #"); return (note >= 0 ? raw.Substring(0, note) : raw).Trim(); }

        internal static IEnumerable<KeyValuePair<int, string>> Lines(string text)
        {
            int n = 0;
            foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                n++;
                string line = Bare(raw);
                if (line.Length == 0 || line.StartsWith("#")) continue;
                yield return new KeyValuePair<int, string>(n, line);
            }
        }

        // one file's text as it is used now ("" — none)
        internal static string Text(string kind, string legacy, bool legacyOn, string file)
        {
            string t;
            return All(kind, legacy, legacyOn).TryGetValue(file, out t) ? t : "";
        }

        // where the editors write (the source, else the old author folder)
        internal static string Target(string kind, string legacy) { return Dir(kind) ?? legacy; }

        internal static bool IsSource { get { return Source != null; } }

        // written where it is read; what went wrong (null — written). The old author folder keeps the last one as .bak;
        // the source does not (its history is the project's own)
        internal static string Save(string kind, string legacy, string file, string text)
        {
            if (!Story.GoodFile(file)) return "погана назва файлу «" + file + "»";
            string dir = Target(kind, legacy);
            try
            {
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, file);
                if (!IsSource && File.Exists(f)) File.Copy(f, f + ".bak", true);
                File.WriteAllText(f, text.Replace("\r", ""), new UTF8Encoding(false));
                return null;
            }
            catch (Exception e) { return "не вдалося записати: " + e.Message; }
        }
    }
}
#endif

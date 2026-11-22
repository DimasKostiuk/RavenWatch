#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;

namespace HuginWhispers
{
    // The story's world half (story-lore.md «Мережа: два шари»): what happened in this world for everyone — the Hall's
    // gate opened, the first heart lit, the storm's bolt fell. The character's own half stays in Story (customData).
    // Kept as the game's world keys (the server keeps and shares them, like the Hall's): «rws_<name>» for a flag,
    // «rws_<name> <N>» for a number. Names lower case (the game lowers its keys anyway).
    // Any client may set one: the game hands the key to the server, everyone gets it back.
    internal static class StoryWorld
    {
        internal const string Prefix = "rws_";

        // What a write costs (finding 2026-11-07, read in the game's ZoneSystem): every change of any key makes the server send the whole list to
        // every game, and each game reads all of it again; each new text of a key is also kept for good in the character's stats (PlayerStats
        // m_knownWorldKeys, three of them). Not measured yet: so each write says in the log how many keys the world has and how many the
        // story wrote this session — the prologue walked once gives the numbers before anything is changed
        private static int _writes; // session: the story's writes of world keys in this game
        private static string Cost()
        {
            _writes++;
            return " (keys " + (ZoneSystem.instance != null ? ZoneSystem.instance.GetGlobalKeys().Count : 0) + ", writes " + _writes + ")"; // heavy: a copy of the key list, only on a write of the story (a few a stage)
        }

        private static string K(string name) { return Prefix + (name ?? "").ToLowerInvariant(); }

        internal static bool Flag(string name)
        {
            return ZoneSystem.instance != null && Story.GoodName(name) && ZoneSystem.instance.GetGlobalKey(K(name));
        }

        internal static void SetFlag(string name, bool on)
        {
            if (ZoneSystem.instance == null || !Story.GoodName(name) || Flag(name) == on) return;
            if (on) ZoneSystem.instance.SetGlobalKey(K(name)); else ZoneSystem.instance.RemoveGlobalKey(K(name));
            Story.Log("world " + (on ? "flag " : "unflag ") + name + Cost());
        }

        internal static int Var(string name)
        {
            string v; int n;
            return ZoneSystem.instance != null && Story.GoodName(name) && ZoneSystem.instance.GetGlobalKey(K(name), out v)
                && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        internal static void SetVar(string name, int v)
        {
            if (ZoneSystem.instance == null || !Story.GoodName(name) || Var(name) == v) return;
            if (v == 0) ZoneSystem.instance.RemoveGlobalKey(K(name)); // (0 is the same as never set)
            else ZoneSystem.instance.SetGlobalKey(K(name) + " " + v.ToString(CultureInfo.InvariantCulture));
            Story.Log("world var " + name + " = " + v + Cost());
        }

        // off, whatever this game reads now (a key just written may not have come back from the server yet)
        internal static void Remove(string name) { if (ZoneSystem.instance != null && Story.GoodName(name)) { ZoneSystem.instance.RemoveGlobalKey(K(name)); Cost(); } }

        // a text kept by the world, for everyone («» none): StoryHold's words
        internal static string Text(string name)
        {
            string v;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(K(name), out v) ? v : "";
        }

        internal static void SetText(string name, string text)
        {
            if (ZoneSystem.instance == null || !Story.GoodName(name) || Text(name) == (text ?? "").ToLowerInvariant()) return;
            if (string.IsNullOrEmpty(text)) ZoneSystem.instance.RemoveGlobalKey(K(name)); else ZoneSystem.instance.SetGlobalKey(K(name) + " " + text.ToLowerInvariant());
            Story.Log("world text " + name + " = " + text + Cost());
        }

        // the world's texts whose names start so: name → text (the saga's witnesses); a pass over the world's keys
        internal static List<KeyValuePair<string, string>> Texts(string prefix)
        {
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            if (ZoneSystem.instance == null) return l;
            string pre = Prefix + prefix.ToLowerInvariant();
            foreach (string k in ZoneSystem.instance.GetGlobalKeys())
            {
                int sp = k.IndexOf(' ');
                if (sp > 0 && k.StartsWith(pre, System.StringComparison.Ordinal)) l.Add(new KeyValuePair<string, string>(k.Substring(Prefix.Length, sp - Prefix.Length), k.Substring(sp + 1)));
            }
            return l;
        }

        // every story key of the world, as "name" or "name N" (the panel)
        internal static List<string> All()
        {
            List<string> l = new List<string>();
            if (ZoneSystem.instance == null) return l;
            foreach (string k in ZoneSystem.instance.GetGlobalKeys())
                if (k.StartsWith(Prefix)) l.Add(k.Substring(Prefix.Length));
            l.Sort();
            return l;
        }

        // a place the world keeps as a text «x y z» (the first heart, a location found): every game and the server read the same; false — none yet
        internal static bool Pos(string name, out UnityEngine.Vector3 at)
        {
            at = UnityEngine.Vector3.zero;
            string[] w = Text(name).Split(' ');
            float x, y, z;
            if (w.Length < 3 || !float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                || !float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            at = new UnityEngine.Vector3(x, y, z);
            return true;
        }
        internal static void SetPos(string name, UnityEngine.Vector3 at)
        {
            SetText(name, at.x.ToString("0.#", CultureInfo.InvariantCulture) + " " + at.y.ToString("0.#", CultureInfo.InvariantCulture) + " " + at.z.ToString("0.#", CultureInfo.InvariantCulture));
        }

        // A block of the story over (a quest's «scratch:», StoryQuests.CloseBefore, docs/jobs/story-data.md «Блоки сюжету»): its own keys taken away — a flag, number or text named
        // with one of the prefixes, or so named after the words of the kinds that keep such keys (ScratchKinds: a module adds its own — «t_» a day's mark,
        // «found_», «stash_»…): «ch1_x», «t_ch1_heart», «stashed_found_ch1_house». Nothing else is touched (a quest's state «q_…», a tale, a word to a
        // person): a quest named so keeps its state. Each key gone is one send of the list to every game: once a block. How many
        internal static readonly List<string> ScratchKinds = new List<string>();
        internal static int Forget(string prefixes) { return Forget(prefixes, false); }
        // whole: the names exactly (one place found, turned off: «ch1_house» — not «ch1_house2» beside it), and a thing's own key at that place
        // («ch1_thistle__ravenwatch_item_budyak_heid»: ThingSep — StoryFound's lay, plant)
        internal const string ThingSep = "__";
        internal static int Forget(string prefixes, bool whole)
        {
            int n = 0;
            string[] pre = prefixes.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string k in All())
            {
                int sp = k.IndexOf(' ');
                string name = sp < 0 ? k : k.Substring(0, sp), bare = name;
                for (bool cut = true; cut; ) { cut = false; foreach (string kind in ScratchKinds) if (bare.StartsWith(kind, System.StringComparison.Ordinal)) { bare = bare.Substring(kind.Length); cut = true; break; } } // («stashed_found_ch1_house» → «ch1_house»)
                foreach (string p in pre)
                    if (whole ? bare == p || bare.StartsWith(p + ThingSep, System.StringComparison.Ordinal) : bare.StartsWith(p, System.StringComparison.Ordinal)) { ZoneSystem.instance.RemoveGlobalKey(Prefix + name); n++; break; }
            }
            if (n > 0) Story.Log("world: " + n + " key(s) of «" + prefixes + "» forgotten" + Cost());
            return n;
        }

        // the author's «as if this world never had a story» (the panel, the console)
        internal static int Clear()
        {
            List<string> l = All();
            foreach (string k in l)
            {
                int sp = k.IndexOf(' ');
                ZoneSystem.instance.RemoveGlobalKey(Prefix + (sp < 0 ? k : k.Substring(0, sp)));
            }
            if (l.Count > 0) Story.Log("world story cleared (" + l.Count + " keys)");
            return l.Count;
        }
    }
}
#endif

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

        private static string K(string name) { return Prefix + (name ?? "").ToLowerInvariant(); }

        internal static bool Flag(string name)
        {
            return ZoneSystem.instance != null && Story.GoodName(name) && ZoneSystem.instance.GetGlobalKey(K(name));
        }

        internal static void SetFlag(string name, bool on)
        {
            if (ZoneSystem.instance == null || !Story.GoodName(name) || Flag(name) == on) return;
            if (on) ZoneSystem.instance.SetGlobalKey(K(name)); else ZoneSystem.instance.RemoveGlobalKey(K(name));
            Story.Log("world " + (on ? "flag " : "unflag ") + name);
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
            Story.Log("world var " + name + " = " + v);
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

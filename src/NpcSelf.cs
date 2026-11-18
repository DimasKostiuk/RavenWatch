#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HuginWhispers
{
    // A check of who each person is (`raven npc self`): what it is made of — its lasting number, template, name, look,
    // ways (NpcPersona, its build too), gait and story — written per world to npc_self_<world>.txt, and set against what
    // the last run wrote there: the same person after a restart, another game, an owner change (NpcKit.Seed), or not —
    // and which part of it changed. Those not near now keep their last record. A tool for the author, run by hand.
    internal static class NpcSelf
    {
        private static string FileOf(string world) { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "npc_self_" + world + ".txt"); }

        internal static string Check()
        {
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "";
            if (world.Length == 0) return "Нема світу.";
            foreach (char ch in Path.GetInvalidFileNameChars()) world = world.Replace(ch, '_');
            string file = FileOf(world);
            Dictionary<string, string[]> was = new Dictionary<string, string[]>();
            if (File.Exists(file))
                foreach (string line in File.ReadAllLines(file)) // heavy: a file, once a run by hand
                {
                    string[] f = line.Split('\t');
                    if (f.Length >= 2) was[f[0]] = f;
                }
            StringBuilder said = new StringBuilder();
            Dictionary<string, string> keep = new Dictionary<string, string>(); // (the record: the ones not near now stay as they were written)
            foreach (KeyValuePair<string, string[]> e in was) keep[e.Key] = string.Join("\t", e.Value);
            int same = 0, changed = 0, fresh = 0, unnumbered = 0;
            foreach (NpcActor a in NpcActor.Live)
            {
                ZDO z = a != null ? a.Zdo : null;
                if (z == null || a.Def == null) continue;
                long sid = z.GetLong(NpcKit.KSid, 0L);
                if (sid == 0L) { unnumbered++; said.Append("\n  ").Append(a.Name).Append(": НЕМА постійного номера").Append(a.Owned ? "" : " (веде інша гра: її господар дасть)"); continue; }
                string[] parts = Parts(a);
                string key = sid.ToString(CultureInfo.InvariantCulture);
                keep[key] = key + "\t" + string.Join("\t", parts);
                string[] old;
                if (!was.TryGetValue(key, out old)) { fresh++; continue; }
                List<string> diff = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    string o = i + 1 < old.Length ? old[i + 1] : "";
                    if (o != parts[i]) diff.Add(Names[i] + ": «" + o + "» → «" + parts[i] + "»");
                }
                if (diff.Count == 0) same++;
                else { changed++; said.Append("\n  ").Append(a.Name).Append(" ЗМІНИВСЯ — ").Append(string.Join("; ", diff.ToArray())); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, string.Join("\n", new List<string>(keep.Values).ToArray()) + "\n"); // heavy: a file, once a run by hand
            string r = "Хто є хто (" + world + "): той самий " + same + ", змінився " + changed + ", новий у записі " + fresh + (unnumbered > 0 ? ", без номера " + unnumbered : "")
                + (was.Count == 0 ? " — перший запис: запусти ще раз після перезапуску світу." : ".") + said;
            NpcDefs.All(); // (the templates read, with their check)
            r += NpcDefs.Problems.Count == 0 ? "\nШаблони: без помилок (" + NpcDefs.All().Count + ")." : "\nШаблони, помилки (" + NpcDefs.Problems.Count + "):\n  " + string.Join("\n  ", NpcDefs.Problems.ToArray());
            Npcs.Log("[npc self]\n" + r);
            return r;
        }

        // what a person is made of, part by part (Names says which is which). Its ways as they are now, and the story's
        // changes (NpcLevers «trait») apart as well: a change of its ways with them is the story's, without them a fault
        private static readonly string[] Names = { "шаблон", "зерно", "ім'я", "вигляд", "вдача", "зміни сюжету", "хода", "передісторія" };
        private static string[] Parts(NpcActor a)
        {
            NpcPersona p = NpcPersona.Of(a);
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder w = new StringBuilder();
            if (p.Nature != null) w.Append("nature=").Append(p.Nature.Id).Append(' ');
            foreach (string t in NpcLevers.TraitNames) w.Append(t).Append('=').Append(p.Get(t).ToString("0.###", ci)).Append(' ');
            string story = NpcBackstory.Has(a) ? NpcBackstory.Text(a, true) : "";
            return new[]
            {
                a.Id, NpcKit.Seed(a.Zdo).ToString(ci), a.Key, a.Zdo.GetString(NpcSettlers.KLook, ""), w.ToString().Trim(), NpcLevers.Traits(a),
                NpcGait.Of(a).Kind.ToString(), story.Length > 0 ? story.GetStableHashCode().ToString(ci) : ""
            };
        }
    }
}
#endif

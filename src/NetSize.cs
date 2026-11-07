#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // "raven netsize": how much the player's ZDO weighs when the game sends it. The game sends a changed ZDO whole,
    // up to 20 times a second to every peer, and a moving player's ZDO changes all the time. Split into the game's
    // own keys, ours and the rest, with the biggest entries and what the bonds string grows to with more players.
    internal static class NetSize
    {
        private const int SendHeader = 42; // id, both revisions, owner, position, the package length
        private const float SendsPerSecond = 20f;

        private sealed class Entry { internal int Hash, Bytes; internal string Type, Value; }

        private static HashSet<int> _vanilla, _ours;

        internal static string Cmd(Player p)
        {
            ZDO z = NetFlag.Zdo(p, false);
            if (z == null) return "netsize: no player ZDO.";
            Known();

            ZPackage whole = new ZPackage();
            z.Serialize(whole);
            int total = whole.Size() + SendHeader;

            List<KeyValuePair<int, float>> floats; List<KeyValuePair<int, Vector3>> vecs; List<KeyValuePair<int, Quaternion>> quats;
            List<KeyValuePair<int, int>> ints; List<KeyValuePair<int, long>> longs; List<KeyValuePair<int, string>> strings;
            List<KeyValuePair<int, byte[]>> bytes; ZDOConnection conn;
            ZDOExtraData.GetData(z.m_uid, out floats, out vecs, out quats, out ints, out longs, out strings, out bytes, out conn);

            List<Entry> all = new List<Entry>();
            foreach (KeyValuePair<int, float> e in floats) all.Add(new Entry { Hash = e.Key, Type = "float", Bytes = 8, Value = e.Value.ToString("0.##") });
            foreach (KeyValuePair<int, Vector3> e in vecs) all.Add(new Entry { Hash = e.Key, Type = "vec3", Bytes = 16, Value = e.Value.ToString() });
            foreach (KeyValuePair<int, Quaternion> e in quats) all.Add(new Entry { Hash = e.Key, Type = "quat", Bytes = 20, Value = "" });
            foreach (KeyValuePair<int, int> e in ints) all.Add(new Entry { Hash = e.Key, Type = "int", Bytes = 8, Value = e.Value.ToString() });
            foreach (KeyValuePair<int, long> e in longs) all.Add(new Entry { Hash = e.Key, Type = "long", Bytes = 12, Value = e.Value.ToString() });
            foreach (KeyValuePair<int, string> e in strings)
            {
                ZPackage one = new ZPackage();
                one.Write(e.Value ?? "");
                all.Add(new Entry { Hash = e.Key, Type = "string", Bytes = 4 + one.Size(), Value = e.Value ?? "" });
            }
            foreach (KeyValuePair<int, byte[]> e in bytes) all.Add(new Entry { Hash = e.Key, Type = "bytes", Bytes = 8 + (e.Value != null ? e.Value.Length : 0), Value = "" });

            int vanB = 0, ourB = 0, othB = 0, vanN = 0, ourN = 0, othN = 0;
            foreach (Entry e in all)
            {
                if (_ours.Contains(e.Hash)) { ourB += e.Bytes; ourN++; }
                else if (_vanilla.Contains(e.Hash)) { vanB += e.Bytes; vanN++; }
                else { othB += e.Bytes; othN++; }
            }

            StringBuilder s = new StringBuilder();
            s.Append("ZDO гравця: ").Append(total).Append(" байт за одне надсилання (").Append(all.Count).Append(" ключів)\n");
            s.Append("  гра: ").Append(vanB).Append(" Б, ").Append(vanN).Append(" ключів\n");
            s.Append("  RavenWatch: ").Append(ourB).Append(" Б, ").Append(ourN).Append(" ключів\n");
            s.Append("  інше (інші моди / не розпізнано): ").Append(othB).Append(" Б, ").Append(othN).Append(" ключів\n");
            s.Append("  решта (заголовок, позиція, поворот): ").Append(total - vanB - ourB - othB).Append(" Б\n");
            s.Append("Поки ти рухаєшся, кожному іншому гравцю до ").Append(Kb(total * SendsPerSecond)).Append(" КБ/с, з них RavenWatch ").Append(Kb(ourB * SendsPerSecond)).Append(" КБ/с\n");

            all.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            s.Append("Найбільші записи:\n");
            for (int i = 0; i < all.Count && i < 15; i++)
            {
                Entry e = all[i];
                string who = _ours.Contains(e.Hash) ? "RW" : _vanilla.Contains(e.Hash) ? "гра" : "інше";
                string v = e.Value.Length > 50 ? e.Value.Substring(0, 50) + "…" : e.Value;
                s.Append("  ").Append(e.Bytes).Append(" Б  ").Append(who).Append("  ").Append(e.Type).Append("  ").Append(v.Replace("\n", " ")).Append('\n');
            }
            s.Append('\n');

            string text = s.ToString();
            Plugin.Instance.Log("[netsize]\n" + text);
            return text;
        }

        // any ZDO's weight on one send (a person's: «raven npc net»), and its biggest strings (where the weight is)
        internal static int Weigh(ZDO z, out string biggest)
        {
            biggest = "";
            if (z == null) return 0;
            ZPackage whole = new ZPackage();
            z.Serialize(whole);
            List<KeyValuePair<int, float>> floats; List<KeyValuePair<int, Vector3>> vecs; List<KeyValuePair<int, Quaternion>> quats;
            List<KeyValuePair<int, int>> ints; List<KeyValuePair<int, long>> longs; List<KeyValuePair<int, string>> strings;
            List<KeyValuePair<int, byte[]>> bytes; ZDOConnection conn;
            ZDOExtraData.GetData(z.m_uid, out floats, out vecs, out quats, out ints, out longs, out strings, out bytes, out conn);
            List<KeyValuePair<int, int>> sizes = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<int, string> e in strings) sizes.Add(new KeyValuePair<int, int>(e.Key, (e.Value ?? "").Length));
            sizes.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<string> top = new List<string>();
            for (int i = 0; i < sizes.Count && i < 3; i++) if (sizes[i].Value > 40) top.Add(Name(sizes[i].Key) + " " + sizes[i].Value);
            biggest = string.Join(", ", top.ToArray());
            return whole.Size() + SendHeader;
        }

        // every key of one ZDO by its name and bytes, heaviest first (a person's: «raven npc net <хто>» — what to move off it)
        internal static string Dump(ZDO z, string title)
        {
            if (z == null) return title + ": —";
            ZPackage whole = new ZPackage();
            z.Serialize(whole);
            List<KeyValuePair<int, float>> floats; List<KeyValuePair<int, Vector3>> vecs; List<KeyValuePair<int, Quaternion>> quats;
            List<KeyValuePair<int, int>> ints; List<KeyValuePair<int, long>> longs; List<KeyValuePair<int, string>> strings;
            List<KeyValuePair<int, byte[]>> bytes; ZDOConnection conn;
            ZDOExtraData.GetData(z.m_uid, out floats, out vecs, out quats, out ints, out longs, out strings, out bytes, out conn);
            List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<int, float> e in floats) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " f", 8));
            foreach (KeyValuePair<int, Vector3> e in vecs) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " v", 16));
            foreach (KeyValuePair<int, Quaternion> e in quats) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " q", 20));
            foreach (KeyValuePair<int, int> e in ints) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " i", 8));
            foreach (KeyValuePair<int, long> e in longs) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " l", 12));
            foreach (KeyValuePair<int, string> e in strings) { ZPackage one = new ZPackage(); one.Write(e.Value ?? ""); l.Add(new KeyValuePair<string, int>(Named(e.Key) + " s", 4 + one.Size())); }
            foreach (KeyValuePair<int, byte[]> e in bytes) l.Add(new KeyValuePair<string, int>(Named(e.Key) + " b", 8 + (e.Value != null ? e.Value.Length : 0)));
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            StringBuilder s = new StringBuilder(title).Append(": ").Append(whole.Size() + SendHeader).Append(" Б, ").Append(l.Count).Append(" ключів —");
            foreach (KeyValuePair<string, int> e in l) s.Append(' ').Append(e.Key).Append('=').Append(e.Value);
            return s.ToString();
        }

        // this assembly's types (the ones it could load: a type of a mod not here is left out)
        private static Type[] Types()
        {
            try { return Assembly.GetExecutingAssembly().GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types; }
        }

        private static Dictionary<int, string> _names;
        private static string Named(int hash)
        {
            if (_names == null)
            {
                _names = new Dictionary<int, string>();
                foreach (FieldInfo f in typeof(ZDOVars).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    if (f.FieldType == typeof(int)) _names[(int)f.GetValue(null)] = f.Name.Replace("s_", "");
                foreach (Type t in Types())
                {
                    if (t == null) continue;
                    if (t.ContainsGenericParameters) continue; // (no static values to read in an open generic)
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (f.FieldType == typeof(int) && f.IsInitOnly && f.Name.StartsWith("K", StringComparison.Ordinal)) _names[(int)f.GetValue(null)] = t.Name + "." + f.Name;
                }
            }
            string n;
            return _names.TryGetValue(hash, out n) ? n : hash.ToString();
        }

        private static string Name(int hash)
        {
            if (hash == ZDOVars.s_items) return "items(спорядження)";
#if RAVEN_NPC
            if (hash == NpcWorker.KCarry) return "carry(ноша)";
            if (hash == NpcMind.KPlans) return "plans";
            if (hash == NpcLocker.KLocker) return "locker";
#endif
            return hash.ToString();
        }

        internal static float PerSecond(int bytes) { return bytes * SendsPerSecond / 1024f; }

        private static string Kb(float b) { return (b / 1024f).ToString("0.0"); }

        // the game's keys: ZDOVars; ours: every "RavenWatch_"/"HuginWhispers_" string in this assembly's code, with the
        // endings the code adds (H/M per raven, _u/_i of a ZDOID)
        private static void Known()
        {
            if (_vanilla != null) return;
            _vanilla = new HashSet<int>();
            foreach (FieldInfo f in typeof(ZDOVars).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (f.FieldType == typeof(int)) _vanilla.Add((int)f.GetValue(null));

            _ours = new HashSet<int>();
            Type[] types = Types();
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Type t in types)
            {
                if (t == null) continue;
                List<MethodBase> ms = new List<MethodBase>();
                try { ms.AddRange(t.GetMethods(all)); ms.AddRange(t.GetConstructors(all)); } catch { continue; }
                foreach (MethodBase m in ms)
                {
                    byte[] il;
                    try { MethodBody b = m.GetMethodBody(); il = b != null ? b.GetILAsByteArray() : null; } catch { il = null; }
                    if (il == null) continue;
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != 0x72 || il[i + 4] != 0x70) continue; // ldstr + a user-string token
                        string str;
                        try { str = m.Module.ResolveString(BitConverter.ToInt32(il, i + 1)); } catch { continue; }
                        if (str.StartsWith("RavenWatch_", StringComparison.Ordinal) || str.StartsWith("HuginWhispers_", StringComparison.Ordinal)) AddOurs(str);
                    }
                }
            }
        }

        private static void AddOurs(string key)
        {
            foreach (string end in new[] { "", "H", "M" })
            {
                string k = key + end;
                _ours.Add(k.GetStableHashCode());
                _ours.Add((k + "_u").GetStableHashCode());
                _ours.Add((k + "_i").GetStableHashCode());
            }
        }
    }
}
#endif

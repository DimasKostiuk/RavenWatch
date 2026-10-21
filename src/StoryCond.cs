#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Conditions for the story (a quest's «starts:», a branch's «when:») and for Odin's errands («when:»): a list split
    // by commas, all of them must hold. Each is a word and its own words; «!» before one turns it round.
    //   boss eikthyr, !flag ch1_done, trust >= 2, var odin_favour 3, night
    // The kinds are found by name in a registry (Kinds below): a new kind is a new entry, nothing else changes.
    // Numbers compare as «N» (at least N), «>= N», «> N», «<= N», «< N», «= N», the sign may stick to the number (<3).
    internal sealed class Cond
    {
        public string Text = "";   // as written (for the log, the panel)
        public bool Not;
        public string[] A = new string[0]; // A[0] the kind, then its own words
        public StoryCond.Kind Kind;
        internal bool Test(Player p)
        {
            bool v;
            try { v = Kind.Test(p, A); }
            catch (Exception e) { Story.Log("condition «" + Text + "»: " + e.Message); v = false; }
            return Not ? !v : v;
        }
    }

    internal static class StoryCond
    {
        internal sealed class Kind
        {
            public string Usage;                       // how it is written (the help, the errors)
            public int Min;                            // words it needs after the kind
            public Func<string[], string> Check;       // null: fine; else what is wrong (load time)
            public Func<Player, string[], bool> Test;  // holds now?
        }

        private static readonly string[] BossNames = { "eikthyr", "elder", "bonemass", "moder", "yagluth", "queen", "fader" };

        // the kinds, by the word
        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "boss", new Kind { Usage = "boss eikthyr|elder|bonemass|moder|yagluth|queen|fader", Min = 1,
                Check = a => BossKey(a[1]) == null ? "нема боса «" + a[1] + "»" : null,
                Test = (p, a) => Stage.Has(p, BossKey(a[1])) } },
            { "key", new Kind { Usage = "key <глобальний ключ>", Min = 1,
                Test = (p, a) => Stage.Has(p, a[1]) } },
            { "stage", new Kind { Usage = "stage [>=|<|…] N   (скільки босів переможено, 0–7)", Min = 1,
                Check = a => CmpCheck(a, 1), Test = (p, a) => Compare(Stage.Of(p), a, 1) } },
            { "flag", new Kind { Usage = "flag <назва>", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва прапорця «" + a[1] + "»",
                Test = (p, a) => Story.Flag(p, a[1]) } },
            { "var", new Kind { Usage = "var <назва> [>=|<|…] N", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва числа «" + a[1] + "»" : CmpCheck(a, 2),
                Test = (p, a) => Compare(Story.Var(p, a[1]), a, 2) } },
            { "quest", new Kind { Usage = "quest <id> [done|active|failed|started]   (без стану: done)", Min = 1,
                Check = a => a.Length > 2 && Array.IndexOf(new[] { Story.Done, Story.Active, Story.Failed, "started" }, a[2]) < 0 ? "стан квесту: done | active | failed | started" : null,
                Test = (p, a) =>
                {
                    string want = a.Length > 2 ? a[2] : Story.Done, has = Story.Quest(p, a[1]);
                    return want == "started" ? has.Length > 0 : has == want;
                } } },
            { "trust", new Kind { Usage = "trust [>=|<|…] N   (найвища довіра ворона, що з тобою)", Min = 1,
                Check = a => CmpCheck(a, 1), Test = (p, a) => Compare(TrustNow(p), a, 1) } },
            { "raven", new Kind { Usage = "raven hugin|munin|both|any   (сидить на плечі)", Min = 1,
                Check = a => Array.IndexOf(new[] { "hugin", "munin", "both", "any" }, a[1]) < 0 ? "raven: hugin | munin | both | any" : null,
                Test = (p, a) =>
                {
                    bool h = Shoulder.Present(p, false), m = Shoulder.Present(p, true);
                    return a[1] == "hugin" ? h : a[1] == "munin" ? m : a[1] == "both" ? h && m : h || m;
                } } },
            { "biome", new Kind { Usage = "biome Meadows|BlackForest|Swamp|Mountain|Plains|Mistlands|AshLands|DeepNorth|Ocean", Min = 1,
                Check = a => Enum.IsDefined(typeof(Heightmap.Biome), a[1]) ? null : "нема біому «" + a[1] + "»",
                Test = (p, a) => string.Equals(p.GetCurrentBiome().ToString(), a[1], StringComparison.OrdinalIgnoreCase) } },
            { "night", new Kind { Usage = "night", Test = (p, a) => EnvMan.instance != null && EnvMan.IsNight() } },
            { "day", new Kind { Usage = "day", Test = (p, a) => EnvMan.instance != null && !EnvMan.IsNight() } },
            { "item", new Kind { Usage = "item <Prefab> [N]   (стільки в торбі, без N: хоч один)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "item: кількість — число від 1" : null,
                Test = (p, a) =>
                {
                    GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(a[1]) : null;
                    ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                    return d != null && p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) >= (a.Length > 2 ? Errands.I(a[2], 1) : 1);
                } } },
            { "errand", new Kind { Usage = "errand   (є доручення з гнізда)", Test = (p, a) => Errands.Of(p) != null } },
        };

        internal static bool Known(string kind) { return Kinds.ContainsKey(kind); }

        // "eikthyr" → defeated_eikthyr; a whole key (defeated_…) as it is
        internal static string BossKey(string w)
        {
            if (string.IsNullOrEmpty(w)) return null;
            string l = w.ToLowerInvariant();
            if (Array.IndexOf(Stage.BossKeys, l) >= 0) return l;
            int i = Array.IndexOf(BossNames, l);
            return i >= 0 ? Stage.BossKeys[i] : null;
        }

        // the highest settled trust of a raven you have with you (0: none)
        private static int TrustNow(Player p)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            return Mathf.Max(h != null ? Trust.SettledLevel(h) : 0, m != null ? Trust.SettledLevel(m) : 0);
        }

        // ---------------------------------------------------------------- numbers
        private static readonly string[] Ops = { ">=", "<=", ">", "<", "=" };

        // the sign and the number from a[at] (and a[at+1] when the sign stands alone)
        private static bool ReadCmp(string[] a, int at, out string op, out int n)
        {
            op = ">="; n = 0;
            if (a.Length <= at) return false;
            string w = a[at];
            foreach (string o in Ops) if (w.StartsWith(o)) { op = o; w = w.Substring(o.Length); break; }
            if (w.Length == 0) { if (a.Length <= at + 1) return false; w = a[at + 1]; }
            int v;
            if (!int.TryParse(w, out v)) return false;
            n = v;
            return true;
        }

        private static string CmpCheck(string[] a, int at) { string op; int n; return ReadCmp(a, at, out op, out n) ? null : "потрібне число (N, >= N, < N…)"; }

        private static bool Compare(int have, string[] a, int at)
        {
            string op; int n;
            if (!ReadCmp(a, at, out op, out n)) return false;
            switch (op)
            {
                case ">": return have > n;
                case "<": return have < n;
                case "<=": return have <= n;
                case "=": return have == n;
                default: return have >= n;
            }
        }

        // ---------------------------------------------------------------- lists
        // "boss eikthyr, !flag x, trust 2" → the conditions; what cannot be read goes to errors (and is left out, so a
        // bad word never makes a list hold by accident: the list then never holds, see Broken)
        internal static List<Cond> Parse(string text, List<string> errors)
        {
            List<Cond> l = new List<Cond>();
            if (string.IsNullOrEmpty(text)) return l;
            foreach (string raw in text.Split(','))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                Cond c = new Cond { Text = t };
                if (t.StartsWith("!")) { c.Not = true; t = t.Substring(1).Trim(); }
                c.A = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (c.A.Length == 0) continue;
                c.A[0] = c.A[0].ToLowerInvariant();
                Kind k;
                string why = null;
                if (!Kinds.TryGetValue(c.A[0], out k)) why = "невідома умова «" + c.A[0] + "»";
                else if (c.A.Length - 1 < k.Min) why = "мало слів: " + k.Usage;
                else if (k.Check != null) why = k.Check(c.A);
                if (why != null) { if (errors != null) errors.Add(c.Text + ": " + why); l.Add(new Cond { Text = c.Text, A = c.A, Kind = Never }); continue; }
                c.Kind = k;
                l.Add(c);
            }
            return l;
        }

        // a condition that could not be read: never holds (even under «!»: Not is left false)
        private static readonly Kind Never = new Kind { Usage = "(помилка)", Test = (p, a) => false };

        internal static bool All(Player p, List<Cond> l)
        {
            if (l == null) return true;
            foreach (Cond c in l) if (!c.Test(p)) return false;
            return true;
        }

        // the console and the panel: each condition with ✓ / ✗, and what could not be read
        internal static string Explain(Player p, string text)
        {
            List<string> errors = new List<string>();
            List<Cond> l = Parse(text, errors);
            StringBuilder sb = new StringBuilder();
            foreach (Cond c in l) sb.Append(c.Test(p) ? "✓ " : "✗ ").Append(c.Text).Append('\n');
            foreach (string e in errors) sb.Append("! ").Append(e).Append('\n');
            sb.Append(All(p, l) ? "Разом: так." : "Разом: ні.");
            return sb.ToString();
        }

        internal static string Help()
        {
            StringBuilder sb = new StringBuilder("Умови (через кому, ! — навпаки):");
            foreach (KeyValuePair<string, Kind> kv in Kinds) sb.Append("\n  ").Append(kv.Value.Usage);
            return sb.ToString();
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A piece's layer: which states and sub-states it is there in (read once per text). The whole story: the head of LocState.cs.
    internal static partial class LocState
    {
        // ---------------------------------------------------------------- layers: a state's part and sub-states' marks
        // «ruin,-1.1» → «ruin» and [«-1.1»]; «+1.2» → «» and [«+1.2»]. Read once per
        // text (a few dozen kinds in a whole place; looked at for every piece every second while the author looks)
        private sealed class Parsed
        {
            internal string Main = "";
            internal string[] Marks = new string[0];
            internal bool Only; // any «+»: only in sub-states, its state's part means nothing then
        }
        private static readonly Dictionary<string, Parsed> _parsed = new Dictionary<string, Parsed>();

        private static Parsed Parse(string layer)
        {
            layer = layer ?? "";
            Parsed p;
            if (_parsed.TryGetValue(layer, out p)) return p;
            p = new Parsed();
            List<string> marks = new List<string>();
            foreach (string one in layer.Split(','))
                {
                    if (one.Length == 0) continue;
                    if (one[0] == '+' || one[0] == '-' || one[0] == '>') { if (!marks.Contains(one)) marks.Add(one); }
                    else if (p.Main.Length == 0) p.Main = one;
                }
            p.Marks = marks.ToArray();
            foreach (string m in p.Marks) if (m[0] == '+') p.Only = true;
            if (_parsed.Count > 1000) _parsed.Clear(); // (the author's tries: never many)
            _parsed[layer] = p;
            return p;
        }

        internal static string MainOf(string layer) { return Parse(layer).Main; }
        internal static bool HasMark(string layer, string mark) { return System.Array.IndexOf(Parse(layer).Marks, mark) >= 0; }
        internal static bool SubOnly(string layer) { return Parse(layer).Only; }
        internal static bool HasAnyMark(string layer) { return Parse(layer).Marks.Length > 0; }

        internal static string Mark(bool there, int state, int sub) { return (there ? "+" : "-") + state + "." + sub; }

        private static string Compose(string main, IEnumerable<string> marks)
        {
            string t = main == "-" ? "" : main ?? "";
            foreach (string m in marks) t += (t.Length > 0 ? "," : "") + m;
            return t;
        }

        // the layer with its state's part changed (its sub-states' marks kept)
        internal static string WithMain(string layer, string main) { return Compose(main, Parse(layer).Marks); }

        // the layer with its mark for this sub-state changed: «+» there only, «-» gone, null — no mark for it
        internal static string WithMark(string layer, int state, int sub, bool? there)
        {
            Parsed p = Parse(layer);
            List<string> marks = new List<string>(p.Marks);
            marks.Remove(Mark(true, state, sub));
            marks.Remove(Mark(false, state, sub));
            if (there.HasValue) marks.Add(Mark(there.Value, state, sub));
            return Compose(p.Main, marks);
        }

        // a piece of one state made one of its steps' only: its state's part and that state's «-» and «>» marks gone, its «+»
        // marks kept (there in those steps too), this step's «+» added
        internal static string OnlyIn(string layer, int state, int sub)
        {
            string mine = "-" + state + ".", plus = Mark(true, state, sub);
            List<string> marks = new List<string>();
            foreach (string m in Parse(layer).Marks) if (!m.StartsWith(mine) && !m.StartsWith(">" + state + ".") && m != plus) marks.Add(m);
            marks.Add(plus);
            return Compose("", marks);
        }

        // its mark for this sub-state: true «+», false «-», null none
        internal static bool? MarkFor(string layer, int state, int sub)
        {
            if (HasMark(layer, Mark(true, state, sub))) return true;
            if (HasMark(layer, Mark(false, state, sub))) return false;
            return null;
        }

        internal static bool Shows(string layer, int state, int sub)
        {
            Parsed p = Parse(layer);
            if (p.Marks.Length == 0) return MainShows(p.Main, state);
            if (p.Only) return sub > 0 && HasMark(layer, Mark(true, state, sub));
            if (!MainShows(p.Main, state) || (sub > 0 && HasMark(layer, Mark(false, state, sub)))) return false;
            int from = FromOf(p, state);
            return sub >= from; // («>2.2»: not in that state's earlier steps)
        }

        // «>2.2» — there from that step of its state on (not at its start nor in the steps before); 0 — none
        internal static string From(int state, int sub) { return ">" + state + "." + sub; }
        private static int FromOf(Parsed p, int state)
        {
            string mine = ">" + state + ".";
            foreach (string m in p.Marks) { int s, k; if (m.StartsWith(mine) && SubNumber(m.Substring(1), out s, out k)) return k; }
            return 0;
        }
        internal static int FromOf(string layer, int state) { return FromOf(Parse(layer), state); }

        // the layer «from this step on» in its state (on), or without it (off)
        internal static string WithFrom(string layer, int state, int sub, bool on)
        {
            Parsed p = Parse(layer);
            string mine = ">" + state + ".";
            List<string> marks = new List<string>();
            foreach (string m in p.Marks) if (!m.StartsWith(mine)) marks.Add(m);
            if (on) marks.Add(From(state, sub));
            return Compose(p.Main, marks);
        }

        private static bool MainShows(string main, int state)
        {
            switch (main)
            {
                case "ruin": return state < Repaired;
                case "repaired": return state >= Repaired;
                case "lived": return state >= Lived;
                case "fixed": return state == Repaired; // (only while repaired: scaffolds, planks left over)
                case Gone: return false;
            }
            int n;
            if (Numbered(main, "only", out n)) return state == n;
            if (Numbered(main, "from", out n)) return state >= n;
            return true;
        }

        // the other places' layers, by their states' numbers: «only2» — in state 2 only, «from2» — from state 2 on
        internal static bool Numbered(string layer, string kind, out int n)
        {
            n = 0;
            return layer != null && layer.StartsWith(kind) && int.TryParse(layer.Substring(kind.Length), out n) && n >= 1;
        }

        // «gone»: a piece the author took out of the blueprint (the user 2026-11-17: the Hall's two benches by the fire, one bench in their place).
        // A world built before still has it — nothing is taken away from a built place (FillOnce) — so there it is hidden in every state, as
        // any piece its state has not: its looks and touch off, a chest's things kept in it. Its line stays in the layers file for those worlds
        internal const string Gone = "gone";

        internal static bool GoodLayer(string layer)
        {
            int n, s, k;
            Parsed p = Parse(layer);
            if (p.Main.Length > 0 && System.Array.IndexOf(Layers, p.Main) < 0 && p.Main != Gone && !Numbered(p.Main, "only", out n) && !Numbered(p.Main, "from", out n)) return false;
            foreach (string m in p.Marks) if (!SubNumber(m.Substring(1), out s, out k) || s < 1 || k < 1) return false;
            return p.Main.Length > 0 || p.Marks.Length > 0;
        }

        // «1.1» → 1, 1; «2» → 2, 0
        internal static bool SubNumber(string v, out int state, out int sub)
        {
            state = sub = 0;
            if (string.IsNullOrEmpty(v)) return false;
            int dot = v.IndexOf('.');
            if (dot < 0) return int.TryParse(v, out state);
            return int.TryParse(v.Substring(0, dot), out state) && int.TryParse(v.Substring(dot + 1), out sub) && sub >= 0;
        }

        internal static string MainWhen(string main)
        {
            switch (main)
            {
                case "": return "завжди";
                case "ruin": return "лише в закинутій";
                case "repaired": return "з відремонтованої";
                case "lived": return "з обжитої";
                case "fixed": return "лише у відремонтованій";
                case Gone: return "прибрана з креслення: у вже збудованих світах схована";
            }
            int n;
            if (Numbered(main, "only", out n)) return "лише в стані " + n;
            if (Numbered(main, "from", out n)) return "зі стану " + n;
            return main;
        }

        // in words, with the place's sub-states' names when it is known
        internal static string LayerWhen(string layer, Loc l = null)
        {
            Parsed p = Parse(layer);
            List<string> there = new List<string>(), gone = new List<string>(), from = new List<string>();
            foreach (string m in p.Marks)
            {
                int s, k;
                if (!SubNumber(m.Substring(1), out s, out k)) continue;
                (m[0] == '+' ? there : m[0] == '>' ? from : gone).Add(l != null ? "«" + l.WordOf(s, k) + "»" : s + "." + k);
            }
            if (there.Count > 0) return "лише в субстані " + string.Join(", ", there.ToArray());
            return MainWhen(p.Main) + (from.Count > 0 ? ", у своєму стані — з " + string.Join(", ", from.ToArray()) : "") + (gone.Count > 0 ? ", крім " + string.Join(", ", gone.ToArray()) : "");
        }

    }
}
#endif

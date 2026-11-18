#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The story's levers on a person (npc-plan.md, «сюжетні важелі»): what a talk, a scene or the console may change in
    // it, and ask of it — one set of words for all of them, kept in its ZDO (small strings), every game sees the same.
    //   deeds (where it is owned: NpcActor.DoAct «lever …»; from a talk: !<deed>)
    //     npcflag <name> · npcunflag <name>       a mark of its own (the story's «он уже бачив вогонь»)
    //     npcvar <name> <value|+N|-N>             a number or a word of its own
    //     knack <job|melee|bow> <×|+x|-x>       its knack (NpcKnack: ×0.5 … ×2) changed
    //     trait nature <id>                       its nature (NpcNature) from now on
    //     trait <trait> <value|+x|-x>             its persona changed (bravery, chatty… — NpcPersona's words) over its
    //                                             template's and its own (kept until set again)
    //     become <template>                       another template from now on (the faded Torvald → the living one):
    //                                             its name, look, talk, fighting, pace; its ways and life stay
    //     protect · unprotect                     the story holds it: nobody sends it off, unsettles it, hunger does not
    //                                             take it away
    //     nojob <job> · okjob <job>               a job it will not take (or will again)
    //     limp <game hours>                       it limps that long (the world's clock: every game the same) — its gait's wish «hurt»
    //                                             (gaits.txt mood_hurt; Limping, NpcModules: the user 2026-11-15 — Torvald out of his hiding place)
    //   conditions (StoryCond: in talks, in the story's own files)
    //     npcflag <name> [npc]   npcvar <name> <op> <number> [npc]   npcis <template> [npc]
    //     (npc: its template id or scene tag; none — the one you talk to)
    internal static class NpcLevers
    {
        internal static readonly int KVars = "RavenWatch_npcVars".GetStableHashCode();     // string "name=value;name=value"
        internal static readonly int KTraits = "RavenWatch_npcTraits".GetStableHashCode(); // string "bravery=0.8 chatty=0.2"
        internal const string Protected = "protected";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- reading (any game)
        private static Dictionary<string, string> Parse(string s, char sep)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string part in (s ?? "").Split(sep))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) d[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
            }
            return d;
        }

        private static string Join(Dictionary<string, string> d, char sep)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> e in d) { if (sb.Length > 0) sb.Append(sep); sb.Append(e.Key).Append('=').Append(e.Value); }
            return sb.ToString();
        }

        internal static string Var(NpcActor a, string name)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null) return null;
            string v;
            return Parse(NpcLocker.Text(z, KVars), ';').TryGetValue(name, out v) ? v : null;
        }

        internal static bool Flag(NpcActor a, string name) { string v = Var(a, name); return v != null && v != "0" && v.Length > 0; }
        internal static float Num(NpcActor a, string name) { float f; return float.TryParse(Var(a, name) ?? "", NumberStyles.Float, Inv, out f) ? f : 0f; }
        internal static bool IsProtected(NpcActor a) { return Flag(a, Protected); }
        internal const string LimpVar = "limp_until"; // (the world's second it stops limping)
        internal static bool Limping(NpcActor a) { return ZNet.instance != null && Num(a, LimpVar) > ZNet.instance.GetTimeSeconds(); }
        internal static float GameHour() { return (EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f) / 24f; }
        internal static string NoJob(NpcActor a, string job) { return !string.IsNullOrEmpty(job) && Flag(a, "nojob_" + job) ? Lang.T("Цієї роботи не візьме.") : null; }
        internal static string Traits(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Text(z, KTraits) : ""; }

        // ---------------------------------------------------------------- the deeds (where it is owned)
        internal static string Do(NpcActor a, string[] w)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || w.Length == 0) return "нема кого";
            if (!a.Owned) return NpcKit.Busy(a);
            string what = w[0].ToLowerInvariant();
            switch (what)
            {
                case "npcflag": if (w.Length < 2) return Usage; SetVar(z, w[1], "1"); return "прапорець " + w[1];
                case "npcunflag": if (w.Length < 2) return Usage; SetVar(z, w[1], null); return "без прапорця " + w[1];
                case "limp":
                {
                    float h;
                    if (w.Length < 2 || !float.TryParse(w[1], NumberStyles.Float, Inv, out h) || h < 0f || ZNet.instance == null) return Usage;
                    SetVar(z, LimpVar, h > 0f ? (ZNet.instance.GetTimeSeconds() + h * GameHour()).ToString("0", Inv) : null);
                    return h > 0f ? "кульгає " + h.ToString("0.#", Inv) + " год гри" : "не кульгає";
                }
                case "npcvar":
                {
                    if (w.Length < 3) return Usage;
                    string v = w[2];
                    float add;
                    if ((v[0] == '+' || v[0] == '-') && float.TryParse(v, NumberStyles.Float, Inv, out add)) v = (Num(a, w[1]) + add).ToString("0.##", Inv);
                    SetVar(z, w[1], v);
                    return w[1] + " = " + v;
                }
                case "trait":
                {
                    if (w.Length < 3) return Usage;
                    string name = w[1].ToLowerInvariant();
                    if (name == "nature")
                    {
                        if (NpcNature.Find(w[2]) == null) return "нема такої вдачі: " + w[2];
                        Dictionary<string, string> tn = Parse(NpcLocker.Text(z, KTraits), ' ');
                        tn[name] = w[2];
                        NpcLocker.Put(z, KTraits, Join(tn, ' '));
                        a.Rebind();
                        NpcNature.SyncFight(a); // (its way of fighting is its nature's)
                        return "вдача: " + w[2];
                    }
                    float now;
                    if (!TraitOf(NpcPersona.Of(a), name, out now)) return "нема такої риси: " + name + " (" + string.Join(", ", TraitNames) + ")";
                    float v;
                    if (!float.TryParse(w[2], NumberStyles.Float, Inv, out v)) return Usage;
                    if (w[2][0] == '+' || w[2][0] == '-') v = now + v;
                    Dictionary<string, string> t = Parse(NpcLocker.Text(z, KTraits), ' ');
                    t[name] = v.ToString("0.###", Inv);
                    NpcLocker.Put(z, KTraits, Join(t, ' '));
                    a.Rebind(); // (its pace and the rest read anew)
                    NpcNature.SyncFight(a); // (a plain one fights by its bravery)
                    return name + " " + now.ToString("0.##", Inv) + " → " + NpcPersona.Of(a).Get(name).ToString("0.##", Inv);
                }
                case "become":
                {
                    if (w.Length < 2) return Usage;
                    if (NpcDefs.Find(w[1]) == null) return "нема шаблону " + w[1];
                    z.Set(Npcs.KId, w[1]);
                    a.Rebind();
                    return "тепер це " + w[1];
                }
                case "knack": if (w.Length < 3) return Usage; return NpcKnack.Set(a, w[1].ToLowerInvariant(), w[2]);
                case "protect": SetVar(z, Protected, "1"); return "під захистом сюжету";
                case "unprotect": SetVar(z, Protected, null); return "без захисту сюжету";
                case "nojob":
                    if (w.Length < 2) return Usage;
                    SetVar(z, "nojob_" + w[1], "1");
                    if (a.Worker != null && a.Worker.JobId == w[1]) NpcWorker.SetJob(a, ""); // (it drops it now)
                    return "не візьме роботу " + w[1];
                case "okjob": if (w.Length < 2) return Usage; SetVar(z, "nojob_" + w[1], null); return NpcNature.WontTake(a, w[1]) == null ? "знову візьме роботу " + w[1] : "сюжет не забороняє, але вдача не дає: " + w[1];
            }
            return Usage;
        }

        internal const string Usage = "npcflag|npcunflag <назва> · limp <годин гри> · npcvar <назва> <значення|+N|-N> · trait <риса> <значення|+x|-x> · knack <робота|melee|bow> <×|+x|-x> · become <шаблон> · protect · unprotect · nojob|okjob <робота>";
        internal static readonly string[] Deeds = { "npcflag", "npcunflag", "npcvar", "trait", "knack", "become", "protect", "unprotect", "nojob", "okjob", "limp" };
        // a template's persona words without its nature (a word, not a number), which is checked here
        internal static string[] NoNature(NpcDef d, string from, string v)
        {
            List<string> rest = new List<string>();
            foreach (string pair in v.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!pair.StartsWith("nature=", System.StringComparison.Ordinal)) { rest.Add(pair); continue; }
                if (NpcNature.Find(pair.Substring(7)) == null) NpcDefs.Problem(d, from, "persona: вдачі «" + pair.Substring(7) + "» нема");
            }
            return rest.ToArray();
        }

        // NpcPersona's words, every one it reads (Put) and gives (Get): the templates' «persona:» check and the levers ask this list
        // («lively» came into NpcPersona 2026-11-17 and not here: every template with «lively=» was said to be wrong in the log)
        internal static readonly string[] TraitNames = { "walk", "stoop", "height", "build", "diligence", "laziness", "curiosity", "chrono", "chatty", "bravery", "lively" };

        private static void SetVar(ZDO z, string name, string value)
        {
            Dictionary<string, string> d = Parse(NpcLocker.Text(z, KVars), ';');
            if (value == null) d.Remove(name); else d[name] = value.Replace(";", ",").Replace("=", "-");
            NpcLocker.Put(z, KVars, Join(d, ';'));
        }

        private static bool TraitOf(NpcPersona p, string name, out float v)
        {
            v = 0f;
            if (System.Array.IndexOf(TraitNames, name) < 0) return false;
            v = p.Get(name);
            return true;
        }

        // ---------------------------------------------------------------- the conditions (StoryCond)
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            StoryCond.Register("npcflag", new StoryCond.Kind { Usage = "npcflag <назва> [npc]   (у нього цей прапорець)", Min = 1, Test = (p, a) => { NpcActor n = Who(a, 2); return n != null && Flag(n, a[1]); } });
            StoryCond.Register("npcvar", new StoryCond.Kind { Usage = "npcvar <назва> <= | != | > | < | >= | <=> <число> [npc]", Min = 3, Test = (p, a) => { NpcActor n = Who(a, 4); return n != null && Compare(Num(n, a[1]), a[2], a[3]); } });
            StoryCond.Register("npcis", new StoryCond.Kind { Usage = "npcis <шаблон> [npc]   (він тепер цей шаблон)", Min = 1, Test = (p, a) => { NpcActor n = Who(a, 2); return n != null && n.Id == a[1]; } });
        }

        // the person a condition asks of: named (its template id or scene tag), else the one you talk to
        internal static NpcActor Who(string[] a, int at)
        {
            if (a.Length <= at) return NpcTalk.Him; // (no name: the one speaking, else the one talked to)
            foreach (NpcActor n in NpcActor.Live) if (n != null && (n.Id == a[at] || n.Tag == a[at])) return n;
            return null;
        }

        private static bool Compare(float have, string op, string num)
        {
            float n;
            if (!float.TryParse(num, NumberStyles.Float, Inv, out n)) return false;
            switch (op)
            {
                case "=": case "==": return Mathf.Approximately(have, n);
                case "!=": return !Mathf.Approximately(have, n);
                case ">": return have > n;
                case "<": return have < n;
                case ">=": return have >= n;
                case "<=": return have <= n;
            }
            return false;
        }

        // for the console and the card: all it has
        internal static string Describe(NpcActor a)
        {
            string v = a.Zdo != null ? NpcLocker.Text(a.Zdo, KVars) : "", t = Traits(a);
            return "шаблон " + a.Id + (v.Length > 0 ? "; змінні: " + v : "; змінних нема") + (t.Length > 0 ? "; риси від сюжету: " + t : "");
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HuginWhispers
{
    // The story's memory (story-plan.md): what this character's own story has already seen and chosen. Each player has
    // their own, kept in the character (player customData RavenWatch_story, with its format number; unknown fields
    // kept), never in the world. Flags (a thing happened), numbers (how much of something), and each quest's state.
    //   v=1;f=ch1_asked+met_odin;n.odin_favour=2;q.ch1_shepit=done
    internal sealed class StoryState
    {
        internal const int Format = 1;
        public readonly List<string> Flags = new List<string>();
        public readonly Dictionary<string, int> Vars = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Quests = new Dictionary<string, string>(); // id → active | done | failed
        private readonly List<KeyValuePair<string, string>> _other = new List<KeyValuePair<string, string>>();

        internal string Code()
        {
            StringBuilder sb = new StringBuilder("v=").Append(Format);
            if (Flags.Count > 0) sb.Append(";f=").Append(string.Join("+", Flags.ToArray()));
            foreach (KeyValuePair<string, int> kv in Vars) sb.Append(";n.").Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, string> kv in Quests) sb.Append(";q.").Append(kv.Key).Append('=').Append(kv.Value);
            foreach (KeyValuePair<string, string> kv in _other) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }

        internal static StoryState Parse(string raw)
        {
            StoryState s = new StoryState();
            if (string.IsNullOrEmpty(raw)) return s;
            foreach (string part in raw.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                if (k == "v") continue;
                if (k == "f") { foreach (string f in v.Split('+')) if (f.Length > 0 && !s.Flags.Contains(f)) s.Flags.Add(f); }
                else if (k.StartsWith("n.") && k.Length > 2) s.Vars[k.Substring(2)] = Errands.I(v, 0);
                else if (k.StartsWith("q.") && k.Length > 2) s.Quests[k.Substring(2)] = v;
                else s._other.Add(new KeyValuePair<string, string>(k, v));
            }
            return s;
        }
    }

    internal static class Story
    {
        internal const string Key = "RavenWatch_story";
        internal static readonly char[] Bad = { ';', '=', ',', '+', '|', ' ', '\n', '\t' };
        internal static int Version; // goes up on every change (the book, the panel, quests waiting to start)

        private static string _raw;
        private static StoryState _state;

        internal static bool GoodName(string n) { return !string.IsNullOrEmpty(n) && n.IndexOfAny(Bad) < 0; }

        // the character's story (an empty one when none yet; never null for a player)
        internal static StoryState Of(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(Key, out raw)) raw = "";
            if (_state != null && raw == _raw) return _state;
            _raw = raw; _state = StoryState.Parse(raw);
            return _state;
        }

        private static void Save(Player p, StoryState s)
        {
            if (p == null) return;
            p.m_customData[Key] = s.Code();
            _raw = p.m_customData[Key]; _state = s;
            Version++;
        }

        internal static void Reset() { _raw = null; _state = null; }

        // ---------------------------------------------------------------- flags
        internal static bool Flag(Player p, string f) { return p != null && Of(p).Flags.Contains(f); }

        internal static void SetFlag(Player p, string f, bool on)
        {
            if (p == null || !GoodName(f)) return;
            StoryState s = Of(p);
            if (on == s.Flags.Contains(f)) return;
            if (on) s.Flags.Add(f); else s.Flags.Remove(f);
            Save(p, s);
            Log((on ? "flag " : "unflag ") + f);
        }

        // ---------------------------------------------------------------- numbers
        internal static int Var(Player p, string n) { int v; return p != null && Of(p).Vars.TryGetValue(n, out v) ? v : 0; }

        internal static void SetVar(Player p, string n, int v)
        {
            if (p == null || !GoodName(n)) return;
            StoryState s = Of(p);
            int old;
            if (s.Vars.TryGetValue(n, out old) && old == v) return;
            if (v == 0) s.Vars.Remove(n); else s.Vars[n] = v; // (0 is the same as never set)
            Save(p, s);
            Log("var " + n + " = " + v);
        }

        internal static void AddVar(Player p, string n, int d) { SetVar(p, n, Var(p, n) + d); }

        // ---------------------------------------------------------------- quests
        internal const string Active = "active", Done = "done", Failed = "failed";

        // "" never started
        internal static string Quest(Player p, string id) { string v; return p != null && Of(p).Quests.TryGetValue(id, out v) ? v : ""; }

        internal static void SetQuest(Player p, string id, string state)
        {
            if (p == null || !GoodName(id)) return;
            StoryState s = Of(p);
            if (string.IsNullOrEmpty(state)) { if (!s.Quests.Remove(id)) return; }
            else { string old; if (s.Quests.TryGetValue(id, out old) && old == state) return; s.Quests[id] = state; }
            Save(p, s);
            Log("quest " + id + " → " + (string.IsNullOrEmpty(state) ? "(forgotten)" : state));
        }

        // ---------------------------------------------------------------- small things
        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Story: " + s); }

        internal static string Describe(Player p)
        {
            StoryState s = Of(p);
            StringBuilder sb = new StringBuilder("Сюжет " + p.GetPlayerName() + ":");
            sb.Append("\n  прапорці: ").Append(s.Flags.Count > 0 ? string.Join(", ", s.Flags.ToArray()) : "—");
            sb.Append("\n  числа: ");
            if (s.Vars.Count == 0) sb.Append("—");
            else { bool first = true; foreach (KeyValuePair<string, int> kv in s.Vars) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value); } }
            sb.Append("\n  квести: ");
            if (s.Quests.Count == 0) sb.Append("—");
            else { bool first = true; foreach (KeyValuePair<string, string> kv in s.Quests) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); } }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven story · flag x · unflag x · var x N · quest x active|done|failed|- · check <conditions> · reset
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            string name = a.Length > 2 ? a[2] : "";
            switch (what)
            {
                case "flag":
                case "unflag":
                    if (!GoodName(name)) return "raven story " + what + " <назва без ; = , + | пробілів>";
                    SetFlag(p, name, what == "flag");
                    return Describe(p);
                case "var":
                    if (!GoodName(name) || a.Length < 4) return "raven story var <назва> <N | =N | +N | -N>";
                    string v = a[3];
                    if (v.StartsWith("+") || v.StartsWith("-")) AddVar(p, name, Errands.I(v.TrimStart('+'), 0)); // +N / -N: added
                    else SetVar(p, name, Errands.I(v.TrimStart('='), 0));                                       // N / =N (=-3): set
                    return Describe(p);
                case "quest":
                    if (!GoodName(name) || a.Length < 4) return "raven story quest <id> active|done|failed|-";
                    string st = a[3].ToLowerInvariant();
                    if (st != Active && st != Done && st != Failed && st != "-") return "Стан: active | done | failed | -";
                    SetQuest(p, name, st == "-" ? "" : st);
                    return Describe(p);
                case "check":
                    if (a.Length < 3) return "raven story check <умови через кому>, напр.: boss eikthyr, !flag ch1_done, trust 2";
                    return StoryCond.Explain(p, string.Join(" ", a, 2, a.Length - 2));
                case "conds": return StoryCond.Help();
                case "events": return StoryBus.Describe();
                case "trace": StoryBus.Trace = !StoryBus.Trace; return "Події в лог: " + (StoryBus.Trace ? "так" : "ні");
                case "raise":
                    if (a.Length < 3) return "raven story raise <подія> [arg] [число]";
                    StoryBus.Raise(a[2], a.Length > 3 ? a[3] : "", a.Length > 4 ? Errands.I(a[4], 0) : 0);
                    return StoryBus.Describe();
                case "reset":
                    p.m_customData.Remove(Key);
                    Reset(); Version++;
                    Log("reset for " + p.GetPlayerName());
                    return "Сюжет персонажа стерто.";
            }
            return Describe(p) + "\nraven story flag|unflag x · var x N|+N|-N · quest x active|done|failed|- · check <умови> · conds · events · trace · raise <подія> [arg] [N] · reset";
        }
    }
}
#endif

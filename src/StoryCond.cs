#if RAVEN_ITEM && RAVEN_NPC
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
            catch (Exception e) { if (!_told) { _told = true; Story.Log("condition «" + Text + "»: " + e); } v = false; } // (once: it may be asked twice a second)
            return Not ? !v : v;
        }
        private bool _told;
    }

    internal static class StoryCond
    {
        internal sealed class Kind
        {
            public string Usage;                       // how it is written (the help, the errors)
            public int Min;                            // words it needs after the kind
            public Func<string[], string> Check;       // null: fine; else what is wrong (load time)
            public Func<Player, string[], bool> Test;  // holds now?
            public Func<Player, string[], string> Why; // may be null: what keeps it from holding now (a scene's wait says it)
            public bool World;                         // asks only the world, never a player: may be asked where no player is (the server's rules of a place)
            public Func<string[], bool> WorldIf;       // or only with these words: «reached ch1 x» of a quest of the server is the world's (StoryTrackConds)
            // What stands behind it, for a quest walked with no game (tools/quest_dry.js reads these lines; in the game only the author's jump does — StoryQuests.Rebuild). A kind with
            // none is «not judged» there, said aloud — so a new kind a quest may wait on gets its line here. {1}, {2}: the condition's own words.
            //   free                    the player, or time, brings it about whenever
            //   moment                  holds only for a moment after something done
            //   at {1}                  free, at a place named by that word (a place of the player's own, «me.x», must have been given: «spot x»)
            //   after <fact>            comes by itself once that fact of the character stands («since» after «stamp»)
            //   me <fact> [: <how>]     a lasting fact of the character; world <fact> [: <how>] — of the world. With «: how» something outside the
            //                           story's data makes it (the player, the game), in words; with none only an action of the data does (StoryActions' Dry)
            //   step · quest · lived · reached    asked of the quests themselves
            public string Dry;
        }

        private static readonly string[] BossNames = { "eikthyr", "elder", "bonemass", "moder", "yagluth", "queen", "fader" };

        // the kinds, by the word
        private static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            { "boss", new Kind { Dry = "world boss:{1} : the players kill it", Usage = "boss eikthyr|elder|bonemass|moder|yagluth|queen|fader", Min = 1,
                Check = a => BossKey(a[1]) == null ? "нема боса «" + a[1] + "»" : null,
                Test = (p, a) => Stage.Has(p, BossKey(a[1])) } },
            { "key", new Kind { Dry = "world key:{1} : the game's own key", Usage = "key <глобальний ключ>", Min = 1,
                Test = (p, a) => Stage.Has(p, a[1]) } },
            { "stage", new Kind { Dry = "free", Usage = "stage [>=|<|…] N   (скільки босів переможено, 0–7)", Min = 1,
                Check = a => CmpCheck(a, 1), Test = (p, a) => Compare(Stage.Of(p), a, 1) } },
            { "flag", new Kind { Dry = "me flag:{1}", Usage = "flag <назва>", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва прапорця «" + a[1] + "»",
                Test = (p, a) => Story.Flag(p, a[1]) } },
            { "var", new Kind { Dry = "me var:{1}", Usage = "var <назва> [>=|<|…] N", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва числа «" + a[1] + "»" : CmpCheck(a, 2),
                Test = (p, a) => Compare(Story.Var(p, a[1]), a, 2) } },
            { "trust", new Kind { Dry = "free", Usage = "trust [>=|<|…] N   (найвища довіра ворона, що з тобою)", Min = 1,
                Check = a => CmpCheck(a, 1), Test = (p, a) => Compare(TrustNow(p), a, 1) } },
            { "raven", new Kind { Dry = "free", Usage = "raven hugin|munin|both|any [awake]   (сидить на плечі; awake — і розбуджений, не спить: Trust.Active)", Min = 1,
                Check = a => Array.IndexOf(new[] { "hugin", "munin", "both", "any" }, a[1]) < 0 ? "raven: hugin | munin | both | any" : a.Length > 2 && a[2] != "awake" ? "raven <який> [awake]" : null,
                Test = (p, a) =>
                {
                    bool aw = a.Length > 2; // («awake»: the Watch's runes are read by a raven awake — a sleeping one on the shoulder reads nothing: the user 2026-11-15)
                    bool h = Shoulder.Present(p, false) && (!aw || Trust.Active(Trust.Worn(false))), m = Shoulder.Present(p, true) && (!aw || Trust.Active(Trust.Worn(true)));
                    return a[1] == "hugin" ? h : a[1] == "munin" ? m : a[1] == "both" ? h && m : h || m;
                } } },
            { "biome", new Kind { Dry = "free", Usage = "biome Meadows|BlackForest|Swamp|Mountain|Plains|Mistlands|AshLands|DeepNorth|Ocean", Min = 1,
                Check = a => Enum.IsDefined(typeof(Heightmap.Biome), a[1]) ? null : "нема біому «" + a[1] + "»",
                Test = (p, a) => string.Equals(p.GetCurrentBiome().ToString(), a[1], StringComparison.OrdinalIgnoreCase) } },
            { "night", new Kind { Dry = "free", Usage = "night", Test = (p, a) => EnvMan.instance != null && EnvMan.IsNight() } },
            { "day", new Kind { Dry = "free", Usage = "day", Test = (p, a) => EnvMan.instance != null && !EnvMan.IsNight() } },
            { "item", new Kind { Dry = "free", Usage = "item <Prefab> [N]   (стільки в торбі, без N: хоч один)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "item: кількість — число від 1" : null,
                Test = (p, a) =>
                {
                    GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(a[1]) : null;
                    ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                    return d != null && p.GetInventory().CountItems(d.m_itemData.m_shared.m_name) >= (a.Length > 2 ? Errands.I(a[2], 1) : 1);
                } } },
            { "errand", new Kind { Dry = "free", Usage = "errand   (є доручення з гнізда)", Test = (p, a) => Errands.Of(p) != null } },
        };

        // another part adds its own kinds (NpcTalk: a person's hired / mine / resident / job)
        internal static void Register(string name, Kind k) { Kinds[name] = k; }

        // A text of conditions kept in data and asked again and again (a place's rules: lockdoors, statewhile, becomes, cold, a touch …):
        // read once, the same list for everyone who asks it; a wrong text is said once (log) and holds never (null). world: only kinds that ask
        // the world (Kind.World) — the server asks these with no player. The story's own kinds are registered first, so a text read before
        // the first quest pass (a place, the server) is read right.
        private static readonly Dictionary<string, List<Cond>> _cached = new Dictionary<string, List<Cond>>();
        internal static List<Cond> Cached(string text, Action<string> log, bool world = false)
        {
            text = text ?? "";
            List<Cond> c;
            if (_cached.TryGetValue(text, out c)) return c;
#if RAVEN_STORY
            StoryTrackConds.Register();
#endif
            List<string> errs = new List<string>();
            c = Parse(text, errs);
            if (world) foreach (Cond one in c) if (one.Kind != null && !one.Kind.World && !(one.Kind.WorldIf != null && one.Kind.WorldIf(one.A))) errs.Add("«" + one.Text + "» питає гравця, а тут лише світ (world, wvar, gate, hearth, state, placed; step, reached, quest — квесту сервера)");
            if (errs.Count > 0) { if (log != null) foreach (string e in errs) log(e); c = null; }
            _cached[text] = c;
            return c;
        }

        // the wrong texts asked anew (the files read again: StoryFiles.ReloadAll), a place added since may have mended them
        internal static void ForgetWrong()
        {
            List<string> wrong = new List<string>();
            foreach (KeyValuePair<string, List<Cond>> c in _cached) if (c.Value == null) wrong.Add(c.Key);
            foreach (string t in wrong) _cached.Remove(t);
        }

        // the same, asked: a wrong text never holds
        internal static bool Holds(Player p, string text, Action<string> log, bool world = false)
        {
            List<Cond> c = Cached(text, log, world);
            return c != null && All(p, c);
        }

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

        internal static string CmpCheck(string[] a, int at) { string op; int n; return ReadCmp(a, at, out op, out n) ? null : "потрібне число (N, >= N, < N…)"; }

        internal static bool Compare(int have, string[] a, int at)
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

        // the first one that does not hold (as written), else null; why: what keeps it (its kind's Why) or null — a scene says why it waits
        internal static string Failing(Player p, List<Cond> l, out string why)
        {
            why = null;
            if (l == null) return null;
            foreach (Cond c in l)
            {
                if (c.Test(p)) continue;
                why = WhyNot(p, c);
                return c.Text;
            }
            return null;
        }

        private static string WhyNot(Player p, Cond c)
        {
            if (c.Not || c.Kind == null || c.Kind.Why == null) return null;
            try { return c.Kind.Why(p, c.A); }
            catch (Exception e) { Story.Log("condition «" + c.Text + "» why: " + e.Message); return null; }
        }

        // the console and the panel: each condition with ✓ / ✗, and what could not be read
        internal static string Explain(Player p, string text)
        {
            List<string> errors = new List<string>();
            List<Cond> l = Parse(text, errors);
            StringBuilder sb = new StringBuilder();
            foreach (Cond c in l)
            {
                bool ok = c.Test(p);
                string why = ok ? null : WhyNot(p, c);
                sb.Append(ok ? "✓ " : "✗ ").Append(c.Text).Append(why != null ? " — " + why : "").Append('\n');
            }
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

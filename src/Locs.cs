#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The list of the story's places: assets/story/blueprints/locations.list.txt — built in; on the author's machine the mod's own folder
    // (StoryFiles.Source: read and written there, «Нова локація» too; Blueprint.Data). A block a place: «loc <id>», then its lines. Every word
    // is one entry of Lines below and is explained in the head of the list's file; a word it does not know is said in the log (and stops the build: storycheck).
    internal static class Locs
    {
        internal const string File = "locations.list";
        private static List<Loc> _all;

        internal static bool Holding(Vector3 point) { foreach (Loc l in All) if (l.Built && l.Holds(point)) return true; return false; } // (on the ground of some standing place of the story)
        internal static bool InAuthorCopy(Vector3 point) { if (!Blueprint.On) return false; foreach (Loc l in All) if (l.InAuthorCopy(point)) return true; return false; }
        internal static List<Loc> All { get { if (_all == null) Load(); return _all; } }
        internal static Loc Refuge { get { foreach (Loc l in All) if (l.Refuge) return l; return null; } } // (the ravens' home: «refuge: yes»)

        // a place by its id or one of its aliases («hall» — the Hall's word in the story's data): the one resolver for every caller
        internal static Loc Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (Loc l in All) if (l.Id == id) return l;
            foreach (Loc l in All) if (l.Is(id)) return l;
            return null;
        }

        // ---------------------------------------------------------------- what is done for every place (the game's own hooks call these)
        // the author's look (`raven blueprint hall`): every place of the story, where it is
        internal static string Tell(Player p)
        {
            List<string> lines = new List<string>();
            foreach (Loc l in All) lines.Add(l.Tell(p));
            return string.Join("\n", lines.ToArray()) + "\nЗолоті знаки — на мапі.";
        }

        // inside any standing place's no-build ring (the author's folder asked last: he builds anywhere)
        internal static bool InNoBuild(Vector3 point)
        {
            foreach (Loc l in All) if (l.InNoBuild(point)) return !Perches.StoryAuthor;
            return false;
        }

        internal static void RegisterRpcs() { foreach (Loc l in All) l.RegisterRpcs(); }
        internal static void ServerTick() { foreach (Loc l in All) l.ServerTick(); } // (the server: each place's spot, once a world, and its building)
        internal static void Tick() { foreach (Loc l in All) l.Tick(); }             // (everyone: its mark on the map)

        // the place a piece is of: stamped by a blueprint, or — in the author's world — the one whose frame it stands in
        internal static Loc Of(ZNetView nv)
        {
            string of; Vector3 at;
            return nv != null && nv.IsValid() && LocParts.Place(nv, out of, out at) ? Get(of) : null;
        }

        internal static string AuthorFile { get { return Path.Combine(Blueprint.Data, File + ".txt"); } }

        internal static void Load()
        {
            List<Loc> list = new List<Loc>();
            string text = null;
            try
            {
                if (Blueprint.On && System.IO.File.Exists(AuthorFile)) text = System.IO.File.ReadAllText(AuthorFile, Encoding.UTF8);
                else
                    using (Stream st = typeof(Locs).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + File + ".txt"))
                        if (st != null) using (StreamReader r = new StreamReader(st, Encoding.UTF8)) text = r.ReadToEnd();
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Locs: " + e.Message); }
            if (text == null && _all != null) return; // (the file not to be read just now: the places stay as they are)
            if (text != null) Parse(text, list);
            if (_all == null) { _all = list; return; }
            // read again (a place added): the places already known stay the same objects, the new ones join
            List<Loc> merged = new List<Loc>();
            foreach (Loc d in list)
            {
                Loc known = _all.Find(l => l.Id == d.Id);
                if (known != null) { known.Take(d); merged.Add(known); }
                else { merged.Add(d); d.RegisterRpcs(); }
            }
            _all = merged;
        }

        private static bool Yes(string v) { return v == "yes" || v == "так" || v == "1"; }
        private static string After(string v, string[] w) { return v.Substring(v.IndexOf(w[0], StringComparison.Ordinal) + w[0].Length).Trim(); } // (all but its first word)
        private delegate void LineFn(LocDef d, string v, string[] w);

        // the list's words: a new one is a line here
        private static readonly Dictionary<string, LineFn> Lines = new Dictionary<string, LineFn>
        {
            { "name", (d, v, w) => d.Title = v },
            { "alias", (d, v, w) => d.Aliases = w },
            { "refuge", (d, v, w) => d.Refuge = Yes(v) },
            { "biome", (d, v, w) => { try { d.Biome = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), v, true); } catch (Exception e) { Plugin.Instance.Log("Locs: " + d.Id + ": biome: " + e.Message); } } },
            { "from", (d, v, w) =>
                {
                    float f;
                    if (w.Length == 0) return;
                    d.Near = w[0] == "start" ? "" : w[0];
                    if (w.Length > 1 && TryF(w[1], out f)) d.Min = f;
                    if (w.Length > 2 && TryF(w[2], out f)) d.Max = f;
                    d.Far = w.Length > 3 && TryF(w[3], out f) ? f : d.Max * 2f;
                    d.Edge = Array.IndexOf(w, "edge") > 0;
                    d.Nearest = Array.IndexOf(w, "nearest") > 0;
                } },
            { "clear", (d, v, w) => { float f; if (TryF(v, out f)) d.Clear = f; } },
            { "nobuild", (d, v, w) => { float f; if (TryF(v, out f)) d.NoBuild = f; } },
            { "room", (d, v, w) => { float f; if (w.Length > 0 && TryF(w[0], out f)) { d.RoomAlong = Mathf.Max(0f, f); d.RoomAcross = w.Length > 1 && TryF(w[1], out f) ? Mathf.Max(0f, f) : d.RoomAlong; } } },
            { "sink", (d, v, w) => { float f; if (TryF(v, out f)) d.Sink = f; } },
            { "apart", (d, v, w) => { float f; if (w.Length >= 2 && TryF(w[1], out f)) d.Apart[w[0]] = f; } },
            { "keys", (d, v, w) => { if (v.Length > 0) d.KeyBase = v.ToLowerInvariant(); } },
            { "grass", (d, v, w) => { float f; d.Grass = Yes(v); if (!d.Grass && TryF(v, out f) && f > 1f) d.BareRound = f; } },
            { "tint", (d, v, w) => { Color c; if (w.Length >= 2 && ColorUtility.TryParseHtmlString("#" + w[1].TrimStart('#'), out c)) d.Tints[w[0]] = c; } },
            { "states", (d, v, w) => States(v, d) },
            { "ruin", (d, v, w) => { int n; if (int.TryParse(v, out n)) d.RuinTo = n; } },
            { "becomes", (d, v, w) => { LocDef.Rule st = RuleOf(w, v); if (st != null) d.Becomes.Add(st); } },
            { "follows", (d, v, w) => d.Follow = RuleOf(w, v) },
            { "statewhile", (d, v, w) => { if (w.Length >= 2) d.StateWhile.Add(new KeyValuePair<string, string>(w[0], After(v, w))); } },
            { "fog", (d, v, w) => { d.Fog = v != "no" && v != "ні" && v != "0"; d.FogWhile = d.Fog && !Yes(v) ? v : ""; } }, // (yes, or the conditions it hangs while)
            { "cold", (d, v, w) => d.ColdWhile = v },
            { "mapmark", (d, v, w) => d.MapMark = v },
            { "mappin", (d, v, w) => d.MapPin = v },
            { "ruinlit", (d, v, w) => d.RuinLit = w },
            { "litfrom", (d, v, w) =>
                {
                    int s, k;
                    if (w.Length < 2 || !LocState.SubNumber(w[0], out s, out k)) return;
                    string[] p = new string[w.Length - 1];
                    Array.Copy(w, 1, p, 0, p.Length);
                    d.LitFrom.Add(new LocDef.LitStep { State = s, Sub = k, Prefabs = p });
                } },
            { "worn", (d, v, w) =>
                {
                    int on = Array.IndexOf(w, "on");
                    if (on < 2 || on % 2 != 0 || on + 1 >= w.Length) { Plugin.Instance.Log("Locs: " + d.Id + ": worn: «" + v + "» — <крок> <частка> … on <слова префабів>"); return; }
                    for (int i = 0; i < on; i += 2)
                    {
                        int s, k; float f;
                        if (!LocState.SubNumber(w[i], out s, out k) || !TryF(w[i + 1], out f)) { Plugin.Instance.Log("Locs: " + d.Id + ": worn: «" + w[i] + " " + w[i + 1] + "» is no step and part"); continue; }
                        d.Worn.Add(new LocDef.WornStep { State = s, Sub = k, Share = Mathf.Clamp01(f) });
                    }
                    d.WornOn = new string[w.Length - on - 1];
                    Array.Copy(w, on + 1, d.WornOn, 0, d.WornOn.Length);
                } },
            { "litwhile", (d, v, w) => { if (w.Length >= 2) d.LitWhile.Add(new KeyValuePair<string, string>(w[0], After(v, w))); } },
            { "hidewhile", (d, v, w) => { if (w.Length >= 2) d.HideWhile.Add(new KeyValuePair<string, string>(w[0], After(v, w))); } },
            { "gate", (d, v, w) => d.Gate = v },
            { "hearthshut", (d, v, w) => d.HearthShut = v },
            { "hearthgives", (d, v, w) => d.HearthGives = v.Trim() },
            { "hearthgivewait", (d, v, w) => { int sep = v.IndexOf("::", StringComparison.Ordinal); d.HearthGiveWait = (sep < 0 ? v : v.Substring(0, sep)).Trim(); d.HearthGiveWaitWords = sep < 0 ? "" : v.Substring(sep + 2).Trim(); } },
            { "hearthwait", (d, v, w) => { int sep = v.IndexOf("::", StringComparison.Ordinal); d.HearthWait = (sep < 0 ? v : v.Substring(0, sep)).Trim(); d.HearthWaitWords = sep < 0 ? "" : v.Substring(sep + 2).Trim(); } },
            { "lockdoors", (d, v, w) => d.LockDoors = v },
            { "shutdoors", (d, v, w) => d.ShutDoors = v },
            { "opendoors", (d, v, w) => d.OpenDoors = w },
            { "lockchests", (d, v, w) => d.LockChests = Yes(v) },
            { "lockstations", (d, v, w) => d.LockStations = Yes(v) },
            { "lockbeds", (d, v, w) => d.LockBeds = Yes(v) },
            { "touch", (d, v, w) =>
                {
                    if (w.Length < 1) return;
                    string tc = After(v, w);
                    for (bool more = true; more; )
                    {
                        more = false;
                        if ((tc + " ").StartsWith("anim ")) { d.TouchAnim.Add(w[0]); tc = tc.Substring(4).Trim(); more = true; }
                        if ((tc + " ").StartsWith("quiet ")) { d.TouchQuiet.Add(w[0]); tc = tc.Substring(5).Trim(); more = true; }
                    }
                    d.Touches.Add(new KeyValuePair<string, string>(w[0], tc == "-" ? "" : tc)); // («-», or nothing: always)
                } },
            { "read", (d, v, w) =>
                {
                    if (w.Length < 2) return;
                    string rest = After(v, w);
                    int sep = rest.IndexOf("::", StringComparison.Ordinal);
                    List<KeyValuePair<string, string>> reads;
                    if (!d.Reads.TryGetValue(w[0], out reads)) d.Reads[w[0]] = reads = new List<KeyValuePair<string, string>>();
                    reads.Add(new KeyValuePair<string, string>(sep < 0 ? "" : rest.Substring(0, sep).Trim(), (sep < 0 ? rest : rest.Substring(sep + 2)).Trim().Replace("\\n", "\n")));
                } },
            { "pool", (d, v, w) => { if (w.Length >= 2) d.Pools[w[0]] = After(v, w); } },
            { "elsedo", (d, v, w) => { if (w.Length >= 2) d.ElseDo[w[0]] = After(v, w); } },
            { "shell", (d, v, w) =>
                {
                    if (w.Length == 0) return;
                    d.Shell = w[0];
                    float x = 0f, y = 0f, z = 0f, t = 0f;
                    if (w.Length >= 4) { TryF(w[1], out x); TryF(w[2], out y); TryF(w[3], out z); }
                    if (w.Length >= 5) TryF(w[4], out t);
                    d.ShellShift = new Vector3(x, y, z); d.ShellTurn = t;
                } },
            { "insidemusic", (d, v, w) => { if (w.Length == 0) return; d.InsideMusic = w[0]; int at = Array.IndexOf(w, "vol"); float f; d.InsideMusicVol = at > 0 && at + 1 < w.Length && TryF(w[at + 1], out f) ? Mathf.Clamp01(f) : 1f; } },
            { "raven", (d, v, w) => { if (w.Length < 2) return; d.RavenLook = w[0]; d.RavenHome = w[1]; d.RavenWhen = After(v, w).Substring(w[1].Length).Trim(); } }, // (LocRaven)
            { "ravenroof", (d, v, w) => { float x, y, z; if (w.Length >= 3 && TryF(w[0], out x) && TryF(w[1], out y) && TryF(w[2], out z)) { d.RavenRoof = new Vector3(x, y, z); d.HasRavenRoof = true; } } },
            { "ravensits", (d, v, w) => d.RavenSits = w },
            { "sitsay", (d, v, w) => { if (w.Length >= 2) d.SitSays.Add(new LocDef.SitSay { Place = w[0], Tag = w[1], When = After(v, w).Substring(w[1].Length).Trim() }); } },
            { "insideshut", (d, v, w) => { int sep = v.IndexOf("::", StringComparison.Ordinal); d.InsideShut = (sep < 0 ? v : v.Substring(0, sep)).Trim(); d.InsideShutWords = sep < 0 ? "" : v.Substring(sep + 2).Trim(); } },
            { "inside", (d, v, w) => { if (w.Length == 0) return; d.Inside = w[0]; int at = Array.IndexOf(w, "door"); d.InsideDoor = at > 0 && at + 1 < w.Length ? w[at + 1] : ""; } },
        };

        private static void Parse(string text, List<Loc> into)
        {
            Loc cur = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string l = StoryFiles.Bare(raw); // (« # …» at a line's end: a note)
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("loc "))
                {
                    string id = l.Substring(4).Trim();
                    if (!Blueprint.GoodName(id)) { cur = null; continue; }
                    cur = new Loc { Id = id, Title = id, KeyBase = "ravenwatch_loc_" + id.ToLowerInvariant() };
                    into.Add(cur);
                    continue;
                }
                int colon = l.IndexOf(':');
                if (cur == null || colon < 0) continue;
                string k = l.Substring(0, colon).Trim().ToLowerInvariant(), v = l.Substring(colon + 1).Trim();
                LineFn fn;
                if (Lines.TryGetValue(k, out fn)) fn(cur, v, v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                else if (Plugin.Instance != null) Plugin.Instance.Log("Locs: " + cur.Id + ": невідомий рядок «" + k + "»");
            }
        }

        // «1.2 hearth zala 2, world heart_first»: a step of its states and the world conditions that bring it
        private static LocDef.Rule RuleOf(string[] w, string v)
        {
            int s, k;
            if (w.Length < 2 || !LocState.SubNumber(w[0], out s, out k) || s < 1) return null;
            return new LocDef.Rule { State = s, Sub = k, When = v.Substring(w[0].Length).Trim() };
        }


        // «закинута (розпалена / відчинена), відремонтована, обжита»: the states, each with its sub-states in brackets
        private static void States(string v, LocDef into)
        {
            List<string> words = new List<string>();
            List<string[]> subs = new List<string[]>();
            int depth = 0, from = 0;
            for (int i = 0; i <= v.Length; i++)
            {
                char c = i < v.Length ? v[i] : ',';
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                if (c != ',' || depth > 0) continue;
                string one = v.Substring(from, i - from).Trim();
                from = i + 1;
                List<string> mine = new List<string>();
                int open = one.IndexOf('(');
                if (open >= 0)
                {
                    int close = one.LastIndexOf(')');
                    string inner = close > open ? one.Substring(open + 1, close - open - 1) : one.Substring(open + 1);
                    foreach (string sub in inner.Split('/')) if (sub.Trim().Length > 0) mine.Add(sub.Trim());
                    one = one.Substring(0, open).Trim();
                }
                if (one.Length == 0) continue;
                words.Add(one);
                subs.Add(mine.ToArray());
            }
            if (words.Count == 0) return;
            into.Words = words.ToArray();
            into.Subs = subs.ToArray();
        }

        internal static string StatesText(Loc l)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < l.Words.Length; i++)
            {
                if (i > 0) b.Append(", ");
                b.Append(l.Words[i]);
                if (l.SubsOf(i + 1) > 0) b.Append(" (").Append(string.Join(" / ", l.Subs[i])).Append(')');
            }
            return b.ToString();
        }

        // a new sub-state of a state, from the author's window: its place's «states:» line in his own list (the built-in
        // one copied first); the rest of his list as he wrote it
        internal static string AddSub(Loc l, int state, string name)
        {
            if (!Blueprint.On) return "Немає теки " + Blueprint.Root + ".";
            name = name.Replace(",", " ").Replace("/", " ").Replace("(", " ").Replace(")", " ").Trim();
            if (l == null || state < 1 || state > l.Last || state > l.Subs.Length) return "Нема такого стану.";
            if (name.Length == 0) return "Дай субстану назву.";
            if (Array.IndexOf(l.Subs[state - 1], name) >= 0) return "Субстан «" + name + "» уже є.";
            FirstCopy();
            List<string> subs = new List<string>(l.Subs[state - 1]) { name };
            string[][] all = (string[][])l.Subs.Clone();
            all[state - 1] = subs.ToArray();
            Loc shape = new Loc { Words = l.Words, Subs = all };
            List<string> lines = new List<string>(System.IO.File.ReadAllLines(AuthorFile, Encoding.UTF8));
            int at = -1, statesAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("loc ")) { if (at >= 0) break; if (t.Substring(4).Trim() == l.Id) at = i; continue; }
                if (at >= 0 && t.StartsWith("states:")) { statesAt = i; break; }
            }
            if (at < 0) return "У " + AuthorFile + " нема «loc " + l.Id + "».";
            string line = "states: " + StatesText(shape);
            if (statesAt >= 0) lines[statesAt] = line; else lines.Insert(at + 1, line);
            System.IO.File.WriteAllLines(AuthorFile, lines.ToArray(), new UTF8Encoding(false));
            Load();
            return "«" + l.Title + "»: новий субстан " + state + "." + subs.Count + " «" + name + "» (у стані «" + l.WordOf(state) + "»; записано в " + AuthorFile + ").";
        }

        // a new place from the author's window: a block in his own list (the built-in one copied first), round the Hall
        internal static string Add(string id, string title)
        {
            if (!Blueprint.On) return "Немає теки " + Blueprint.Root + ".";
            if (!Blueprint.GoodName(id)) return "Назва креслення: латиниця, цифри, _ чи - (наприклад torvald).";
            if (Get(id) != null) return "Локація «" + id + "» уже є.";
            FirstCopy();
            string by = All.Count > 0 ? All[0].Id : "start"; // (round the first place of the list: the Hall)
            string block = "\nloc " + id + "\nname: " + (title.Trim().Length > 0 ? title.Trim() : id) + "\nbiome: Meadows\nfrom: " + by + " 60 250\nclear: 20\nnobuild: 15\nstates: закинута\nruin: 1\nfog: no\n";
            System.IO.File.AppendAllText(AuthorFile, block, new UTF8Encoding(false));
            Load();
            return "Локацію «" + id + "» додано (поруч Зали, 60–250 м; правила — у " + AuthorFile + "). Тепер: рамка тут, будуй, зберігай.";
        }

        // no list in his folder yet: the built-in one, word for word (its notes too)
        private static void FirstCopy()
        {
            if (System.IO.File.Exists(AuthorFile)) return;
            Directory.CreateDirectory(Blueprint.Data);
            using (Stream st = typeof(Locs).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + File + ".txt"))
                if (st != null) using (StreamReader r = new StreamReader(st, Encoding.UTF8)) System.IO.File.WriteAllText(AuthorFile, r.ReadToEnd(), new UTF8Encoding(false));
        }

        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }
}
#endif

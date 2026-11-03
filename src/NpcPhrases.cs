#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // People's own words, put together (npc-plan.md, «трансформер»): the ravens' way (Patterns) — a sentence from parts,
    // nearly never the same twice — in a core of its own for people (the ravens' one carries their trust, their swearing
    // and their moods: left as it is). The same file shape:
    //   [idle] {вигук?} {думка}            a theme's template; {part?} only sometimes; plain text as it is
    //   [torvald:idle] Ноги вже не ті.      a story person's own (its template id): mixed with the common ones — written
    //                                       lines and put-together ones side by side (the user)
    //   [torvald:idle ?quest q1 done, !boss moder] Тепер я знаю.   only while these hold (StoryCond, as in its talk:
    //                                       flag, var, quest, boss… and the person's — hired, job, npcflag — about the one
    //                                       speaking); none of its own holds: the common ones
    //   @думка                              a list of parts, one a line, taken at random, the last few not again soon
    //   ~бурч Знову робота.                 a tone mark: only for a person of that way (NpcPersona, its age) —
    //                                       ~весел cheerful · ~бурч grumpy · ~стар old · ~дор grown · ~мол young ·
    //                                       ~смілив brave · ~боязк timid; unmarked: anyone
    // Values: {name} the one it speaks to · {npc} itself · {job} its work · {base} its heart's name · {other} another
    // person near · {neighbour} one who lives at its heart ({alone}: none — an empty value) · {weather} · {daypart} · {food}
    // what it ate · and a moment's own (SayOr): {wage} {item} {why}. All go in as they are (a name: never after «з», «у»).
    // A template or a part whose value is missing now is passed over.
    // Forms: its own «ходив|ходила» (NpcActor.Form), yours {ходив|ходила} (Grammar).
    // Anywhere words are written for people (a talk's node, an answer, a line of two people's chat) «{@theme}» is a
    // put-together line of that theme, said by whoever says the line: written and made lines take turns (the user).
    // Built in below; the author's own files config/RavenWatch/talk/npc_patterns*.txt (while tuning) add to them.
    // NpcVoice (below) is who speaks unasked and of what.
    internal static class NpcPhrases
    {
        private sealed class Book { public readonly Dictionary<string, List<string>> Templates = new Dictionary<string, List<string>>(); public readonly Dictionary<string, List<string>> Parts = new Dictionary<string, List<string>>(); }
        private static Book _book;
        private static readonly System.Random Rng = new System.Random();
        private static readonly Dictionary<List<string>, List<string>> Recent = new Dictionary<List<string>, List<string>>();

        internal static void Reload() { _book = null; Recent.Clear(); Conds.Clear(); }

        // every theme this person can speak, a few lines each as it would say them now (raven npc words <who> [n], for the
        // author): a theme that gives nothing, or a line with a {…} left in it, is told
        internal static string Sample(NpcActor a, Player p, int n)
        {
            if (a == null || p == null) return "Ні.";
            List<string> themes = new List<string>();
            string own = a.Def != null ? a.Def.Id + ":" : "\u0001";
            foreach (string k in B.Templates.Keys)
            {
                string th = k.StartsWith(own, StringComparison.Ordinal) ? k.Substring(own.Length) : k;
                if (th.IndexOf(':') < 0 && !themes.Contains(th)) themes.Add(th);
            }
            themes.Sort(StringComparer.Ordinal);
            StringBuilder sb = new StringBuilder("[npc words] " + a.Name + " (" + a.Id + "), themes " + themes.Count + "\n");
            int none = 0, left = 0;
            foreach (string th in themes)
            {
                sb.Append(th).Append(':');
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    string s;
                    try { s = Compose(a, th, p); } catch (Exception e) { s = "FAILED: " + e.Message; } // quiet: written into the sample, which goes to the log
                    if (string.IsNullOrEmpty(s)) continue;
                    any = true;
                    if (s.Contains("{") || s.StartsWith("FAILED", StringComparison.Ordinal)) { left++; sb.Append(" !!"); }
                    sb.Append(" | ").Append(s.Replace('\n', ' '));
                }
                if (!any) { none++; sb.Append(" (nothing now)"); }
                sb.Append('\n');
            }
            Npcs.Log(sb.ToString());
            return a.Name + ": themes " + themes.Count + ", nothing now " + none + ", broken lines " + left + " (all of them: the log, «[npc words]»)";
        }

        // values a module or the story adds to every line ({m_rescued} NpcMemory…): wired once, like NpcModules
        internal static readonly List<Action<NpcActor, Player, Dictionary<string, string>>> ValueSources = new List<Action<NpcActor, Player, Dictionary<string, string>>>();
        // another theme to try first for this person and player (null: none) — NpcAttitude, the story's own moods
        internal static readonly List<Func<NpcActor, Player, string, string>> Variants = new List<Func<NpcActor, Player, string, string>>();

        // a steady draw (its story: the same every time and on every game — seeded by the person, nothing sent)
        [ThreadStatic] private static System.Random _steady;
        [ThreadStatic] private static HashSet<string> _told; // (a story being told: the ^marks of what it has said already)

        internal static void StoryBegin() { _told = new HashSet<string>(); }
        internal static HashSet<string> StoryEnd() { HashSet<string> t = _told; _told = null; return t ?? new HashSet<string>(); }

        // a part's ^marks (one thing told once in a story; a fact that may change what it does: ^боїться_води)
        private static List<string> MarksOf(string line)
        {
            List<string> l = new List<string>();
            int i = line.IndexOf(" ^", StringComparison.Ordinal);
            while (i >= 0)
            {
                int end = line.IndexOf(' ', i + 2);
                l.Add(end < 0 ? line.Substring(i + 2) : line.Substring(i + 2, end - i - 2));
                i = end < 0 ? -1 : line.IndexOf(" ^", end, StringComparison.Ordinal);
            }
            return l;
        }

        private static string Unmark(string line) { int i = line.IndexOf(" ^", StringComparison.Ordinal); return i < 0 ? line : line.Substring(0, i); }
        private static double Next() { return _steady != null ? _steady.NextDouble() : Rng.NextDouble(); }
        private static int Next(int n) { return _steady != null ? _steady.Next(n) : Rng.Next(n); }

        internal static string ComposeSteady(NpcActor a, string theme, int seed)
        {
            _steady = new System.Random(seed);
            try { return Compose(a, theme, null); }
            finally { _steady = null; }
        }

        private static Book B
        {
            get
            {
                if (_book != null) return _book;
                _book = new Book();
                Parse(_book, Builtin);
                if (RavenLooks.FromFiles && Directory.Exists(NpcTalk.Dir))
                    foreach (string f in Directory.GetFiles(NpcTalk.Dir, "npc_patterns*.txt"))
                    {
                        try { Parse(_book, File.ReadAllText(f, Encoding.UTF8)); }
                        catch (Exception e) { Npcs.Log("npc patterns " + f + ": " + e.Message); }
                    }
                return _book;
            }
        }

        private static void Parse(Book b, string text)
        {
            List<string> part = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue; // (a note: «#» in a file, «//» in the built-in words — a «#» line would be read by the compiler)
                if (line.StartsWith("@")) { string n = line.Substring(1).Trim(); if (!b.Parts.TryGetValue(n, out part)) b.Parts[n] = part = new List<string>(); continue; }
                if (line.StartsWith("["))
                {
                    int close = line.IndexOf(']');
                    if (close < 0) continue;
                    string head = line.Substring(1, close - 1).Trim();
                    int q = head.IndexOf(" ?", StringComparison.Ordinal); // ([torvald:idle ?quest q1 done, !boss moder]: only while these hold)
                    string theme = q < 0 ? head : head.Substring(0, q).Trim(), body = line.Substring(close + 1).Trim();
                    if (q >= 0) body = Mark + head.Substring(q + 2).Trim() + Mark + body;
                    List<string> t;
                    if (!b.Templates.TryGetValue(theme, out t)) b.Templates[theme] = t = new List<string>();
                    t.Add(body);
                    part = null;
                    continue;
                }
                if (part != null) part.Add(line);
            }
        }

        // ---------------------------------------------------------------- composing
        internal static bool Has(NpcActor a, string theme) { return a != null && (B.Templates.ContainsKey(theme) || (a.Def != null && B.Templates.ContainsKey(a.Def.Id + ":" + theme))); }

        // a line of this theme by this person to that player (null: nothing fits now)
        internal static string Compose(NpcActor a, string theme, Player to) { return Compose(a, theme, to, null); }

        // the same with values of the moment ({wage}, {item}, {why}…: a template wanting one not given is passed over)
        internal static string Compose(NpcActor a, string theme, Player to, Dictionary<string, string> extra)
        {
            if (a == null) return null;
            foreach (Func<NpcActor, Player, string, string> v in Variants) // (another theme for this person and player now — its feeling to you: see_player_cold… — tried first)
            {
                string alt = v(a, to, theme);
                if (string.IsNullOrEmpty(alt) || alt == theme) continue;
                string got = One(a, alt, to, extra);
                if (got != null) return got;
            }
            return One(a, theme, to, extra);
        }

        private static string One(NpcActor a, string theme, Player to, Dictionary<string, string> extra)
        {
            List<string> own = null, common;
            if (a.Def != null) B.Templates.TryGetValue(a.Def.Id + ":" + theme, out own);
            B.Templates.TryGetValue(theme, out common);
            if ((own == null || own.Count == 0) && (common == null || common.Count == 0)) return null;
            HashSet<string> tones = Tones(a);
            List<string> ownFit = Fitting(own, a, tones, to), commonFit = Fitting(common, a, tones, to);
            bool mine = ownFit.Count > 0 && (commonFit.Count == 0 || Next() < 0.6); // (a story person: its own words more often, the common ones too)
            if (ownFit.Count == 0 && commonFit.Count == 0) return null;
            Dictionary<string, string> values = Values(a, to);
            if (extra != null) foreach (KeyValuePair<string, string> e in extra) values[e.Key] = e.Value;
            return Pick(a, mine ? own : common, mine ? ownFit : commonFit, tones, values, to)
                ?? Pick(a, mine ? common : own, mine ? commonFit : ownFit, tones, values, to); // (none of those fits now: the others)
        }

        private static string Pick(NpcActor a, List<string> templates, List<string> fit, HashSet<string> tones, Dictionary<string, string> values, Player to)
        {
            for (int attempt = 0; attempt < 40 && fit.Count > 0; attempt++)
            {
                string pick = Fresh(templates, fit);
                string s = Expand(Unmark(pick), tones, values);
                if (s != null) s = Fill(s, values);
                if (s == null) { fit.Remove(pick); continue; } // (it wants what is not there now: the next one)
                s = Grammar.Fix(s, to); // (your forms first: {ходив|ходила})
                return Tidy(a.Form(s));  // (then its own: ходив|ходила)
            }
            return null;
        }

        // the templates that fit this person now: its conditions hold (the story's, NpcTalk's, the levers' — «he» is the one
        // speaking, the player the one spoken to, else you) and its tone is its
        private const char Mark = '\u0001'; // (a template's conditions, kept in front of it: Mark conditions Mark template)
        private static readonly Dictionary<string, List<Cond>> Conds = new Dictionary<string, List<Cond>>();

        private static List<string> Fitting(List<string> templates, NpcActor a, HashSet<string> tones, Player to)
        {
            List<string> fit = new List<string>();
            if (templates == null) return fit;
            foreach (string t in templates) { string x = t; if (Holds(ref x, a, to) && Tone(ref x, tones)) fit.Add(x); }
            return fit;
        }

        private static bool Holds(ref string line, NpcActor a, Player to)
        {
            if (line.Length == 0 || line[0] != Mark) return true;
            int end = line.IndexOf(Mark, 1);
            if (end < 0) return true;
            string text = line.Substring(1, end - 1);
            line = line.Substring(end + 1);
            List<Cond> l;
            if (!Conds.TryGetValue(text, out l))
            {
                NpcTalk.Register(); // (a person's conditions known)
                List<string> errors = new List<string>();
                Conds[text] = l = StoryCond.Parse(text, errors);
                foreach (string e in errors) Npcs.Log("npc words «" + text + "»: " + e);
            }
            if (_steady != null) return false; // (a steady draw — a backstory — is the same on every game: no one's story state in it)
            Player p = to;
            if (p == null || p != Player.m_localPlayer) return false; // (a player's story lives on its own game only: words for another player here never ask it)
            NpcActor was = NpcTalk.Speaker;
            NpcTalk.Speaker = a;
            try { return StoryCond.All(p, l); }
            finally { NpcTalk.Speaker = was; }
        }

        // a made line of this theme said now; none fits: the written one (already in its form)
        internal static void SayOr(NpcActor a, string theme, string written, Dictionary<string, string> extra = null)
        {
            if (a == null || !NpcVoice.Free(a)) return; // (a scene, the story's hush: quiet)
            string s = Compose(a, theme, Player.GetClosestPlayer(a.transform.position, 20f), extra);
            a.Say(!string.IsNullOrEmpty(s) ? s : written);
        }

        // «{@theme}» in written words: a made line in its place (none fits: the words without it)
        internal static string Inline(NpcActor a, string text, Player to)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("{@", StringComparison.Ordinal) < 0) return text;
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf("{@", i, StringComparison.Ordinal);
                if (open < 0) { sb.Append(text.Substring(i)); break; }
                int close = text.IndexOf('}', open);
                if (close < 0) { sb.Append(text.Substring(i)); break; }
                sb.Append(text.Substring(i, open - i));
                sb.Append(Compose(a, text.Substring(open + 2, close - open - 2).Trim(), to) ?? "");
                i = close + 1;
            }
            return Tidy(sb.ToString());
        }

        // its tones: its ways and its age (a line marked for another tone is not its)
        private static HashSet<string> Tones(NpcActor a)
        {
            HashSet<string> t = new HashSet<string>();
            NpcPersona p = NpcPersona.Of(a);
            NpcSettlers.Look l = NpcSettlers.Of(a);
            int age = l != null ? l.Age : (p.Stoop > 0.45f ? 2 : 1);
            if (age == 2) t.Add("стар");
            if (age == 1) t.Add("дор");
            if (p.Bravery > 0.6f) t.Add("смілив");
            if (p.Bravery < 0.4f) t.Add("боязк");
            if (age == 0) t.Add("мол");
            if ((p.Laziness + (1f - p.Chatty)) * 0.5f > 0.55f || (age == 2 && p.Chatty < 0.5f)) t.Add("бурч");
            if (p.Chatty > 0.6f && p.Laziness < 0.5f) t.Add("весел");
            return t;
        }

        private static bool Tone(ref string line, HashSet<string> tones)
        {
            if (!line.StartsWith("~")) return true;
            int sp = line.IndexOf(' ');
            string mark = sp > 0 ? line.Substring(1, sp - 1) : line.Substring(1);
            line = sp > 0 ? line.Substring(sp + 1) : "";
            return tones.Contains(mark);
        }

        private static Dictionary<string, string> Values(NpcActor a, Player to)
        {
            Dictionary<string, string> v = new Dictionary<string, string>();
            if (to != null) v["name"] = to.GetPlayerName();
            v["npc"] = a.Name;
            if (a.Worker != null && a.Worker.Works) v["job"] = Jobs.Word(a.Worker.JobId);
            ZDOID heart = NpcKit.GetRef(a.Zdo, NpcResident.KHeart);
            foreach (NpcHeart h in NpcHeart.Loaded) if (h != null && h.Id == heart && h.Name.Length > 0) v["base"] = h.Name;
            NpcActor other = null; float best = 10f * 10f;
            foreach (NpcActor o in NpcActor.Live) { if (o == null || o == a || o.Mind == null) continue; float d = (o.transform.position - a.transform.position).sqrMagnitude; if (d < best) { best = d; other = o; } }
            if (other != null) v["other"] = other.Name;
            List<string> folk = new List<string>(); // (who else lives at its heart, near or not: {neighbour}; nobody: {alone}, an empty value)
            foreach (NpcActor o in NpcHeart.Residents(heart)) if (o != a) folk.Add(o.Name);
            if (folk.Count > 0) v["neighbour"] = folk[Rng.Next(folk.Count)]; else v["alone"] = ""; // (never the steady draw: a story stays the same however many live there)
            if (a.Hunger != null && a.Hunger.LastFood.Length > 0 && Time.time - a.Hunger.LastMeal < 600f) v["food"] = a.Hunger.LastFood;
            foreach (Action<NpcActor, Player, Dictionary<string, string>> s in ValueSources) { try { s(a, to, v); } catch (Exception e) { Npcs.Log("npc words values: " + e.Message); } } // (a module's or the story's own)
            string w = NpcVoice.Weather();
            if (w.Length > 0) v["weather"] = Lang.T(w == "storm" ? "буря" : w == "snow" ? "сніг" : w == "fog" ? "туман" : "дощ");
            v["daypart"] = Lang.T(NpcVoice.DayPart() == "night" ? "ніч" : NpcVoice.DayPart() == "morning" ? "ранок" : NpcVoice.DayPart() == "evening" ? "вечір" : "день");
            return v;
        }

        // the parts in, at random (the last few of a list not again soon); null: a needed part has nothing for it now
        private static string Expand(string template, HashSet<string> tones, Dictionary<string, string> values)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < template.Length)
            {
                int open = template.IndexOf('{', i);
                if (open < 0) { sb.Append(template.Substring(i)); break; }
                int close = template.IndexOf('}', open);
                if (close < 0) { sb.Append(template.Substring(i)); break; }
                sb.Append(template.Substring(i, open - i));
                string name = template.Substring(open + 1, close - open - 1);
                bool optional = name.EndsWith("?");
                string bare = optional ? name.Substring(0, name.Length - 1) : name;
                List<string> part;
                if (B.Parts.TryGetValue(bare, out part))
                {
                    if (!(optional && Next() < 0.5))
                    {
                        List<string> fit = new List<string>();
                        foreach (string p in part) { string x = p; if (Tone(ref x, tones) && Fill(x, values) != null) fit.Add(x); }
                        if (fit.Count > 0) sb.Append(Unmark(Fresh(part, fit)));
                        else if (!optional) return null;
                    }
                }
                else sb.Append(template.Substring(open, close - open + 1)); // (a value, or a form pair: later)
                i = close + 1;
            }
            return sb.ToString();
        }

        // the values in; null: one is wanted that is not there now ({x|y} form pairs are no values)
        private static string Fill(string s, Dictionary<string, string> values)
        {
            int i = 0;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                int open = s.IndexOf('{', i);
                if (open < 0) { sb.Append(s.Substring(i)); break; }
                int close = s.IndexOf('}', open);
                if (close < 0) { sb.Append(s.Substring(i)); break; }
                sb.Append(s.Substring(i, open - i));
                string key = s.Substring(open + 1, close - open - 1);
                string v;
                if (key.IndexOf('|') >= 0 || key.StartsWith("@")) sb.Append(s.Substring(open, close - open + 1));
                else if (values.TryGetValue(key, out v)) sb.Append(v);
                else return null;
                i = close + 1;
            }
            return sb.ToString();
        }

        private static string Fresh(List<string> owner, List<string> fit)
        {
            if (_steady != null)
            {
                List<string> left = fit;
                if (_told != null) { left = new List<string>(); foreach (string x in fit) { bool again = false; foreach (string m in MarksOf(x)) if (_told.Contains(m)) again = true; if (!again) left.Add(x); } if (left.Count == 0) left = fit; } // (said already in this story: not again)
                string got = left[Next(left.Count)];
                if (_told != null) foreach (string m in MarksOf(got)) _told.Add(m);
                return got; // (a steady draw: nothing «said lately»)
            }
            List<string> recent;
            if (!Recent.TryGetValue(owner, out recent)) { recent = new List<string>(); Recent[owner] = recent; }
            List<string> pool = new List<string>();
            foreach (string x in fit) if (!recent.Contains(x)) pool.Add(x);
            if (pool.Count == 0) pool = fit;
            string pick = pool[Rng.Next(pool.Count)];
            recent.Add(pick);
            int keep = Mathf.Clamp(fit.Count - 1, 0, 6);
            while (recent.Count > keep) recent.RemoveAt(0);
            return pick;
        }

        private static string Tidy(string s)
        {
            StringBuilder sb = new StringBuilder();
            bool space = false;
            foreach (char c in s.Trim())
            {
                if (c == ' ') { space = true; continue; }
                if (space && sb.Length > 0 && ",.!?:;…".IndexOf(c) < 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            for (int i = 0; i < sb.Length; i++) // (a capital at the start and after a sentence's end: parts are written small)
                if (char.IsLower(sb[i]) && (i == 0 || (i >= 2 && sb[i - 1] == ' ' && ".!?…".IndexOf(sb[i - 2]) >= 0))) sb[i] = char.ToUpper(sb[i]);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the first words (the author's files add to them)
        private const string Builtin = @"
// Слова людей: [тема] шаблон, @список частин. ~весел ~бурч ~стар ~мол — тон. {name} ти, {npc} він сам, {job} робота,
// {base} назва серця, {other} хтось поруч, {neighbour} хтось із його оселі ({alone} — нікого), {weather}, {daypart}, {food} що їв,
// {wage} {item} {why} — дає той, хто кличе. ходив|ходила — його форма (тільки про себе!), {ходив|ходила} — твоя.

[idle] {вигук?} {думка}
[idle] {думка} {думка_кінець?}
[idle] {вигук} {думка} {думка_кінець?}
@вигук
Ех.
Ну що ж.
Хм.
Так-так.
Отакої.
Ну-ну.
~стар Ох-ох.
~стар Ох, літа мої.
~бурч Тьху.
~бурч Ет.
~весел Ха!
~весел Еге!
~мол Ого.
@думка
колись тут був ліс, а тепер — ми.
цікаво, що там за пагорбом.
ворони сьогодні щось розкаркалися.
треба б дров на зиму.
у нас тут непогано, як подумати.
от якби всі дні були такі тихі.
дах не тече — і на тому дякую.
хтось бачив мою другу рукавицю?
вогонь у печі — то половина щастя.
тролі, кажуть, теж колись були малими. Не віриться.
мені снилося, що я літав|літала з воронами.
треба навчитися рибалити. Колись.
скільки ж тут каміння — на десять хат вистачить.
в оселі «{base}» навіть повітря інше.
кажуть, Одін бачить усе. Навіть як я ліную.
десь там, за морем, теж хтось рубає дрова. Мабуть.
от би хтось вигадав сокиру, що рубає сама.
~стар за моїх часів ми й не таке будували.
~стар коліна ниють — буде погода.
~стар раніше трави були зеленіші, а тролі — менші.
~стар скільки зим я бачив|бачила — а все одно мерзну.
~мол от би на кораблі в море.
~мол колись і я піду на тролів.
~мол а якщо за туманом — інший світ?
~мол хочу шолом із рогами. Знаю, знаю — непрактично.
~бурч хтось знову лишив сокиру де попало.
~бурч усе на мені тримається, як завжди.
~бурч і нащо я сюди прийшов|прийшла.
~бурч знову все не так, як треба.
~весел гарний день, щоб нічого не робити. Шкода, що треба.
~весел аж співати хочеться. Не бійтеся, не буду.
~весел життя — штука смачна, якщо добре посолити.
{other} знову щось бурмоче собі під ніс.
@думка_кінець
Еге ж.
Отак.
Таке.
Ну, хай.
~бурч І нікому нема діла.
~бурч Як завжди.
~весел Добре ж!
~весел Хе-хе.
~стар Ох-хо-хо.

[see_player] {привіт} {про_тебе?}
[see_player] {привіт}
@привіт
{name}!
О, {name}.
Здоров, {name}.
А, це ти.
Привіт, {name}.
{name}, гарно, що зайш{ов|ла}.
~весел {name}! Радий|Рада тебе бачити!
~весел О, {name}! Саме вчасно!
~бурч Знову ти.
~бурч {name}. Ну, здоров.
~стар А, {name}… ходи-но сюди.
~стар {name}, дитино, як ти?
~мол {name}! Візьмеш мене колись із собою?
@про_тебе
Як воно там, у світі?
Щось ти {змарнів|змарніла}.
Ноги ще носять?
Будеш їсти — кажи.
Ти хоч спиш інколи?
Що чути?
Знову кудись {зібрався|зібралася}?
~бурч Тільки не проси нічого.
~бурч Я зайнятий|зайнята, якщо що.
~весел Розкажи щось, бо тут нудьга.
~весел Ти сьогодні аж сяєш.
~стар Бережи себе. Хоч трохи.

[back] {привіт_назад} {питання?}
@привіт_назад
З поверненням, {name}!
{name}! А ми вже думали — все.
Ну нарешті! Без тебе тут нудно.
О, хто прийшов!
{name} вдома. Можна видихнути.
~бурч {Повернувся|Повернулася}. Ну-ну.
~бурч А, {вернувся|вернулася}. Ми тут, між іншим, працювали.
~весел {name} вдома! Ура!
~весел Нарешті! Я вже почав|почала скучати.
~стар Слава богам, {живий|жива}.
~стар Ох, {name}… я вже за тебе богам молився|молилася.
~мол {name}! Розкажеш, де {був|була}?
@питання
Що там, у світі?
Що {привіз|привезла}?
{Цілий|Ціла}? Ну й добре.
Довго ж тебе не було.
Тролів хоч {бачив|бачила}?
{Голодний|Голодна}?
~весел Сідай, розповідай!

[stare] {дивиться}
@дивиться
Ти щось {хотів|хотіла}?
Що, щось не так із моїм обличчям?
Я тут, якщо що. Просто стою.
Ти мовчиш — я мовчу. Добре мовчимо.
~бурч Дивишся, ніби я тобі гроші винен|винна.
~бурч Іди вже, не стовбич.
~весел Хочеш, я тобі щось розповім? Ні? Ну гаразд.
~стар Дивишся, як мій покійний дід. Він теж мовчав, а потім — бац, і мудрість.
~мол Ти так стоїш, ніби зараз дасиш мені завдання. Даси?

[hurt] {рани} {рани_кінець?}
@рани
{name}, ти ж {весь|вся} у крові!
Хто тебе так?
Ох, та ти ледве на ногах.
На тобі живого місця нема!
Ти виглядаєш, як після троля.
~стар Сядь, перев'яжу. Ну, спробую.
~бурч Знову {влип|влипла} кудись?
~мол Ого! Покажеш, хто це зробив?
@рани_кінець
До вогню, швидко.
Поїж і полеж.
Меду б тобі.
~весел Нічого, до весілля загоїться.
~бурч І хто потім лікуватиме? Знову я?

[weather] {погода} {погода_кінець?}
@погода
Знову {weather}.
Ну й {weather} сьогодні.
{weather}. Мені подобається. Через раз.
~бурч {weather}, звісно. Як завжди.
~стар {weather}… кістки ще зранку казали.
~весел {weather}! Принаймні не нудно.
~мол У таку погоду тільки історії біля вогню розповідати.
@погода_кінець
Хоч із дому не виходь.
Під дах би.
Зачиніть хтось двері!
~бурч Робота стоїть, а я тут мерзну.
~весел Зате буде про що згадати.

[night] {ніч}
@ніч
Темно вже. Час до ліжка.
Чуєш? То не вітер.
Вночі тут усе інше.
Скільки зірок… як іскор від вогнища.
Хтось чув вовків? Чи то мені здалося?
Вогонь горить — і на душі тепліше.
~стар Раніше я ночами не спав|спала. Тепер — тільки ночами й сплю.
~мол Зірок сьогодні — як іскор!
~мол Кажуть, уночі драугри ходять. Брешуть, правда?
~бурч Темно, холодно, і хтось знову хропе.
~весел Ніч — то час для казок!

[morning] {ранок}
@ранок
Ранок. Ну, хоч не дощ.
Знову день — знову робота.
Росиця на траві — гарно.
Півні б ще… та нема в нас півнів.
~весел Добрий ранок, {name}!
~весел Сонце встало — і я встав|встала!
~бурч Хто це вигадав — вставати?
~бурч Ще б годинку…
~стар Ще один ранок. Дякую, боги.

[evening] {вечір}
@вечір
Вечоріє. До вогню б.
Ще трохи — і відпочинок.
День минув — і добре.
Сонце сідає, як утомлений воїн.
~весел Вечеря скоро!
~бурч Нарешті цей день скінчиться.
~стар Вечір — найкращий час. Ніхто нічого не хоче.

[work] {робота} {робота_кінець?}
@робота
Я тут {job}. А це не жарти.
Роблю, роблю, а кінця не видно.
Робота сама себе не зробить.
Ще трохи — і перепочину.
~весел Люблю свою роботу. Майже.
~бурч Хтось колись мені подякує? Ні.
~стар У мої роки — і ще працювати…
~мол Колись я стану найкращим|найкращою у цьому!
@робота_кінець
Ну, далі.
Не відволікай.
Вибач, руки зайняті.
~весел Ще трохи!
~бурч Ох.

[work_cook] {кухня}
@кухня
М'ясо саме себе не засмажить.
Хто знову з'їв усе готове?
Сьогодні буде смачно. Мабуть.
Не підходь — вогонь кусається.
Солі б ще… завжди солі мало.
~бурч Варю, смажу — а дякують воронам.
~весел Хто голодний — підходь! Ну, скоро.
~стар Моя бабця так смажила — пальчики оближеш.
[work_stoker] {вогонь}
@вогонь
Вогонь — він як дитина: годуй і стеж.
Дрова кінчаються швидше, ніж росте ліс.
Тепло — то моя робота.
Без вогню тут усі б уже змерзли. Кажу, щоб знали.
~бурч Знову хтось лишив смолоскип без живиці.
~весел Люблю, як потріскує!
[work_smelter] {піч}
@піч
Руда, вугілля, руда, вугілля…
Піч гуде — отже, усе добре.
Гаряча робота. У прямому сенсі.
~бурч Від цієї печі в мене брови обгоріли.
~весел Метал — то майже чарівництво!
[work_gatherer] {збір}
@збір
Ягоди самі себе не зберуть.
Тут гриб, там гриб — а кошик повний.
Головне — не з'їсти все дорогою.
~весел Ліс сьогодні щедрий!
~бурч Колючки. Всюди колючки.
[work_cleaner] {прибирання}
@прибирання
Хто кидає речі на підлогу? Хто?!
Прибрав|Прибрала — і знову насмітили.
Порядок — він як вогонь: відвернешся — згасне.
~бурч Я вам не служка. Ну, майже.
~весел Чисто — і на душі світло!
[work_keeper] {бджоли}
@бджоли
Бджоли сьогодні сердиті.
Мед добрий буде. Якщо ніхто не з'їсть.
Медовуха — справа повільна.
~весел Медовуха зріє — життя гарне!
~бурч Мене знову вкусили. Тричі.
[work_guard] {варта}
@варта
Тихо. Надто тихо.
Я стежу. Не хвилюйся.
Ніхто не пройде. Ну, без мого відома.
~бурч Стій тут, дивись туди. Ціле життя.
~мол Хай тільки хтось сунеться!
~стар Очі вже не ті, але вуха — ого.

[night_watch] {нічна_варта}
@нічна_варта
Вночі варта — найважча.
Я не сплю. Просто довго кліпаю.
Кожен шурхіт — ворог. Або їжак.
~бурч Усі сплять, а я — дивись.
~мол Вночі вони й приходять. Я готовий|готова.

[raven] {ворон}
@ворон
Знову твій ворон на мене дивиться, як на сир.
Розумна птаха. Розумніша за декого.
Твій ворон знову щось бурмоче. Чи то мені здається?
Кажуть, ворони пам'ятають обличчя. Моє хай забуде.
~бурч Скажи своєму ворону, щоб не крав мої ложки.
~весел Птаха! Привіт, птахо!
~стар Ворони Одіна… за мого діда їх тільки в казках бачили.
~мол Можна я його погладжу? Ні? Він кусається?

[other] {плітка}
@плітка
{other} сьогодні мовчить більше, ніж зазвичай.
{Бачив|Бачила}, як {other} дрова носить? Сміхота.
{other} знову щось бурмоче уві сні.
~весел {other} — добра душа, хоч і бурчить.
~бурч А моя порція знову кудись поділась. Хтось знає куди?

[chat_open] {розмова}
@розмова
Як гадаєш, буде сьогодні дощ?
Чули, що за пагорбом бачили троля?
Що на вечерю?
Як спалося?
Скільки ще тих дров рубати?
Ти віриш у Вальгаллу?
Знаєш, чого ворони кружляють над нами?
Якби був корабель — куди б попливти?
~бурч Тобі теж здається, що тут усе на мені тримається?
~весел Хочеш, загадку загадаю?
~стар Пам'ятаєш, якою була зима торік?
~мол Драконів колись бачити доводилось?
@відповідь
Та хтозна.
Боги знають.
Не питай.
Може, і так.
Добре питання. Немає відповіді.
Як скажеш.
Ну ти й питаєш.
~бурч А мені яке діло?
~бурч Відчепись, а?
~весел Ха! Добре сказано.
~весел Оце вигадка!
~стар Колись і я про таке думав|думала.
~стар Ох, дитино… довга то історія.
~мол Та ну! Серйозно?
[chat_reply] {відповідь}

[hungry] {голод}
@голод
Шлунок бурчить, як троль у печері.
Поїсти б…
Коли ж та вечеря?
Хоч би сухарик.
~бурч Тут хтось взагалі годує людей?
~стар На порожній шлунок і ложка важка.
~мол Я б зараз цілого кабана з'їв|з'їла.
[fed] {ситий}
@ситий
Ох, наївся|наїлася. Тепер би подрімати.
{food} — от що робить людину людиною.
Смачно було. Хто готував — молодець.
~весел Ситий — отже, щасливий!
~бурч Могло бути й солоніше.
~стар Добре поїв|поїла — добре спатиму.
[meal] {їжа}
@їжа
О, оце діло.
Смачного всім.
Ну нарешті.
~весел Хто куховарив — тому шана!
~бурч Ну, їсти можна.
[mend] {лагодити}
@лагодити
Залатаємо — буде як нове.
Хто ж так стіни б'є…
Ще цвях — і готово.
~бурч І знову я.
~весел Тук-тук, і стіна — як нова!

[after_fight] {переміг}
@переміг
Отак їм!
Ще прийдуть — ще отримають.
Фух. Живі.
~весел {Бачив|Бачила}, як я того махнув|махнула?
~бурч Знову все в крові. Хто пратиме?
~мол Це був мій перший! Ну, другий.
~стар Старий кінь борозни не псує.
[after_fight_lost] {відступив}
@відступив
Важко було.
Ледве відбилися.
~бурч Наступного разу — без мене.
~стар Ох, старий|стара я вже для такого.
[after_flight] {втік}
@втік
Я не втікав|втікала. Я… відходив|відходила стратегічно.
Ух… серце досі калатає.
Ще трохи — і мене б з'їли.
~бурч Хтось колись зробить тут нормальну огорожу?
~весел Бігаю я швидше, ніж думав|думала!
~мол Наступного разу я битимусь! Мабуть.

[new_home] {новенький}
@новенький
Тут гарно. Дякую, що {прийняв|прийняла}.
Ще звикаю до нового місця.
{base} — гарна назва.
Де тут у вас криниця? А, нема криниці…
~весел Новий дім! Новий початок!
~бурч Ліжко тверде, але бувало й гірше.
~стар На старості — новий дім. Хто б подумав.

[building] {будуєш}
@будуєш
Що будуєш?
О, нова стіна! Чи то підлога?
Тільки не заважай мені ходити.
Гарно виходить. Мабуть.
~бурч Знову будуєш, а прибирати — мені.
~весел Будуй, будуй! Хочу вежу!
~стар Кривенько. Але то нічого — стоятиме.
~мол Навчиш мене колись так будувати?

[heavy] {тяжко}
@тяжко
Ти ж ледве несеш! Що там?
Каміння, чи що, {назбирав|назбирала}?
Обережно, спину зірвеш.
~бурч Сподіваюсь, то не мені розбирати.
~весел Ого, скільки добра!

[fishing] {риба}
@риба
Риба тут є. Кажуть.
Клює?
Тільки не лови мою улюблену.
~весел Принеси велику — засмажимо!
~бурч Від рибалки — сама луска й нежить.

[land_meadows] {луки}
@луки
Луки — гарне місце. Тихе.
Тут навіть кабани дружні. Майже.
Трава шепоче — хтось іде.
[land_forest] {ліс}
@ліс
Цей ліс аж дихає.
Карлики знову шастають десь неподалік.
Тут легко заблукати. Я вже двічі.
[land_swamp] {болото}
@болото
Болото… усе тут пахне сирістю.
П'явки — моя улюблена компанія. Ні.
~бурч Хто ж будується на болоті?
[land_mountain] {гори}
@гори
Холодно. Але як гарно видно!
Тут навіть вітер сердитий.
~бурч Вовки знову виють. Чи то мій шлунок.
[land_plains] {рівнини}
@рівнини
Рівнини — простору багато, а тіні мало.
Фулінги знову гуркотять десь у таборі.
~весел Видно аж до краю світу!
[land_mist] {туман}
@туман
Туман такий, що власних рук не бачу.
У тумані щось шепоче. Мені страшно. Ну, трохи.
[land_ash] {попіл}
@попіл
Тут усе горить. Навіть повітря.
Попіл на зубах. Знову.
[land_sea] {море}
@море
Море шумить — гарно спиться.
Солоне все, навіть хліб.
// ---------------------------------------------------------------- передісторія (NpcBackstory): уламки, бо обрані приходять без пам'яті.
// Збирається сталим жеребом людини: та сама історія щоразу. Тон ~мол ~дор ~стар (вік) ~смілив ~боязк (сміливість) ~бурч ~весел.

[story_came] {валькірія} {давно} {перше?}
[story_came] {давно} {валькірія} {перше?}
@валькірія
Мене принесла валькірія.
Валькірія кинула мене на камінь посеред лук.
Прокинувся|Прокинулася на холодному камені, а наді мною — крила.
Пам'ятаю крила — і падіння.
Я не прийшов|прийшла сюди. Мене принесли.
Хтось поклав мене на камені, як мішок із зерном.
~бурч Валькірія скинула мене, наче непотріб. Дякую, що хоч не в болото.
~весел Валькірія принесла мене, як подарунок. Не знаю, кому.
@давно
~мол Тут я недавно — може, рік.
~мол Небагато зим тому це було.
~мол Ще й трава не встигла вирости над моїми першими слідами.
~дор Кілька зим тому це було.
~дор Рахую дні зарубками на палиці — вже кілька сотень.
~дор Я тут уже кілька років, наскільки тут рахують роки.
~стар Це було так давно, що тоді на Луках ще не було жодної хати.
~стар Стільки зим минуло, що я перестав|перестала їх лічити.
~стар Тоді тут ще жили ті, кого вже нема. ^згаслі
@перше
Небо було чуже, а руки — мої.
Перше, що я побачив|побачила, — ворона на гілці.
Перша ніч була найдовша.
Я довго сидів|сиділа і не знав|знала, куди йти.
Першого дня мене ледь не з'їли карлики. ^карлики
~весел Першого дня я знайшов|знайшла малину — і вирішив|вирішила, що тут жити можна.
~бурч Перше, що я зробив|зробила, — промочив|промочила ноги.

[story_memory] {нічого} {уламок} {уламок_кінець?}
@нічого
Колишнього життя не пам'ятаю.
Ким я був|була раніше — не знаю.
Пам'ять — як туман над болотом.
Минуле в мене забрали. Майже все.
З того, що було до, лишилось небагато.
@уламок
Лишився тільки запах — дим і смола.
Лишився тільки запах моря, солоний і холодний.
Іноді сниться жінка, що співає над колискою.
Знаю одну пісню, а звідки — не знаю.
Руки самі тягнуться до сокири.
Руки пам'ятають, як в'язати сіті.
Пам'ятаю сніг на віях і чийсь голос: «Не бійся».
Пам'ятаю червоне вітрило над сірою водою.
Пам'ятаю стукіт молота в кузні — рівний, як серце.
Пам'ятаю смак меду з чиїхось рук.
~мол Пам'ятаю сміх — дзвінкий, дитячий. Можливо, мій.
~мол Пам'ятаю, як біг|бігла босоніж по росі.
~дор Мені сниться битва, в якій я впав|впала. Прокидаюсь — і плечі болять.
~дор Пам'ятаю вогнище й людей довкола. Облич не пам'ятаю — тільки тепло.
~стар У мене були діти. Я певен|певна. Але не пам'ятаю облич — і це болить найбільше.
~стар Колись я пам'ятав|пам'ятала імена. Тепер — лише кольори.
~смілив Пам'ятаю останній бій. Я не відступив|відступила.
~боязк Пам'ятаю, що тікав|тікала. Від чого — не знаю.
@уламок_кінець
Не знаю, звідки це.
Іноді думаю, що то просто сон.
Тримаюсь за це, як за мотузку.
Мабуть, колись згадаю більше.
~стар Боюсь, що й це забуду.
~смілив Мабуть, тому я тут.
~боязк Може, тому й досі здригаюсь від шерехів.

[story_life] {як_жив} {втрата?}
[story_life] {як_жив} {як_жив_ще?}
@як_жив
Будував|Будувала хижу тричі. Тричі приходили карлики. ^карлики
Ходив|Ходила від хижі до хижі, де ніхто не жив, і грівся|грілася біля чужих згаслих вогнищ.
Ловив|Ловила рибу руками, поки не зробив|зробила вудку. ^вода
Ночував|Ночувала на деревах — там безпечніше.
Збирав|Збирала гриби й ягоди, і так перебивався|перебивалася.
~мол Перші місяці ховався|ховалася по покинутих хижах і боявся|боялася ночі.
~мол Вчився|Вчилася всього сам|сама: розпалювати вогонь, смажити м'ясо, не плакати.
~дор Ходив|Ходила з ватагою. Потім ватага… згасла. Один за одним.
~дор Жив|Жила сам|сама на краю болота. Самотність тут гірша за драугрів.
~дор Торгував|Торгувала з Гальдором, поки мав|мала що продати.
~дор Кілька зим доглядав|доглядала чужу кузню, поки господар не згас.
~стар Колись мене кликали на кожне свято. Тепер нема кому кликати.
~стар Бачив|Бачила, як тут виростали хати й як вони порожніли. ^згаслі
~стар Учив|Учила молодших, де копати мідь і як не злити троля.
~смілив Ходив|Ходила на тролів сам|сама. Двічі. ^knack_melee_10
~смілив Стояв|Стояла на варті в чужому таборі, поки табір не розбігся.
~боязк Ховався|Ховалася, коли інші билися. Мені не соромно. Ну, трохи.
~боязк Обходив|Обходила ліс десятою дорогою.
@як_жив_ще
А потім знову все спочатку.
Так і жив|жила, день за днем.
~весел І знаєш — було не так уже й погано.
~бурч Ніхто не допоміг. Ніхто.
@втрата
~мол Ще нікого тут не втратив|втратила. Кажуть, це ненадовго.
~дор Порожніх ліжок я бачив|бачила більше, ніж хотілося б.
~дор Друг мій одного ранку просто не прокинувся. Ліжко лишилось тепле. ^згаслі
~стар Пережив|Пережила трьох товаришів. Вони не загинули — просто одного ранку їх не стало. ^згаслі
~стар Усіх, кого я знав|знала першими, вже нема. ^згаслі

[story_mark] {прикмета} {прикмета_кінець?}
@прикмета
Боюся глибокої води. Не знаю чому. ^вода ^боїться_води
Вночі рахую зорі. Збиваюсь на двохсотій. ^зорі
Різьблю з дерева воронів. Жодного ще не подарував|подарувала.
Шукаю когось. Не пам'ятаю кого. Але впізнаю. ^шукає
Не можу заснути без вогню поруч. ^без_вогню
Збираю гарні камінці. Навіщо — не питай.
Розмовляю з деревами. Вони не відповідають, але слухають.
~дор Інколи вночі чую голос — обіцяє спокій, якщо я все забуду.
~стар Інколи вночі хтось шепоче мені, що досить, що можна відпочити.
~мол Знаю всі ягідні місця на три дні ходу.
~боязк Сплю з ножем під подушкою.
~смілив Не люблю, коли хтось воює за мене.
~бурч Терпіти не можу, коли співають уранці.
~весел Сміюся уві сні — так кажуть.
@прикмета_кінець
Отакий|Отака я.
Не смійся.
~стар Поки не погоджуюсь.
~весел Ну, в кожного свої дивацтва.


[story_trouble_surrounded] А потім мене обступили — і якби не ти…
[story_trouble_captive] А потім мене схопили й тягли до табору. Далі ти знаєш.
[story_trouble_hungry] А потім скінчилась їжа. Три дні я їв|їла кору.
[story_trouble_wounded] А потім звірі підрали. Лежав|Лежала і думав|думала — ось і все.
[story_trouble_lost] А потім я загубив|загубила свою річ — а з нею й надію.
[story_trouble_freezing] А потім я заблукав|заблукала серед гір і майже замерз|замерзла.
[story_trouble_chased] А потім за мною погналися — і я біг|бігла, поки не побачив|побачила тебе.
[story_trouble_giant] А потім мене загнав у кут велетень. Сидів|Сиділа і не дихав|дихала.
[story_trouble_astray] А потім я заблукав|заблукала — і кричав|кричала, поки не охрип|охрипла.
[story_trouble_ambush] А потім мене змусили кликати на допомогу. Пробач мені.
// ---------------------------------------------------------------- передісторія: нові шматки (не в кожного — скільки, вирішує балакучість)
[story_dream] {сон_вступ} {сон} {сон_кінець?}
@сон_вступ
Мені часто сниться один і той самий сон.
Буває, сниться таке, що прокидаюсь мокрий|мокра.
Сни тут дивні.
~стар Сни в мене тепер довші за дні.
~мол Мені сняться сни — яскраві, як вогонь.
@сон
Велике дерево, і я на ньому — як листок.
Хтось кличе мене на ім'я, але я не чую, яке.
Довга зала, повна столів, і всі місця зайняті, крім одного.
Вода, яка піднімається, а я не можу ворухнутись.
Чорні птахи, що несуть у дзьобах вогники.
Міст із веселки — і я не наважуюсь ступити.
Двері в землі, а за ними — тиша.
~смілив Я стою на березі, а з туману виходять кораблі. Я не тікаю.
~боязк За мною хтось іде. Я озираюсь — нікого.
@сон_кінець
Що воно значить — не знаю.
Прокидаюсь — і серце калатає.
Може, то пам'ять. А може, ні.
~весел Гарний сон. Шкода, що короткий.
~бурч Краще б мені снилася вечеря.

[story_friend] {друг} {друг_кінець?}
@друг
Мене тут навчив розпалювати вогонь один старий. Потім він згас. ^згаслі
Була в мене товаришка — сміялася так, що й вовки замовкали. Одного ранку її ліжко стояло порожнє. ^згаслі
Я довго ходив|ходила з одним мовчуном. Він ні разу не назвав свого імені.
Колись я подружився|подружилася з Гальдором. Ну, як подружився|подружилася — він мене не обдурив.
Мав|Мала я пса. Тутешнього, сірого. Він пішов за вовками й не повернувся.
~мол Моя перша подруга тут навчила мене ставити пастки на зайців.
~стар Друзів я пережив|пережила. Тепер дружу з вогнем. ^згаслі
~смілив Ми з товаришем стояли спина до спини проти карликів. Він тоді вперше засміявся. ^карлики
~боязк Мене колись витягли з болота за комір. Хто — так і не дізнався|дізналася.
@друг_кінець
Згадую часто.
Це було давно. Або вчора — тут важко сказати.
~весел Добрі були часи.
~бурч Тепер нема з ким і посваритись.

[story_thing] Ношу з собою {річ}. {річ_чому}
@річ
дерев'яну ложку з вирізаним вороном
камінчик із діркою посередині
шкіряний мішечок, а в ньому — пасмо чийогось волосся
ніж без руків'я
мідний ґудзик, що не пасує до жодного одягу
мотузку з дев'ятьма вузлами
пір'їну — чорну, як ніч
~стар старий ключ, а від яких дверей — не пам'ятаю
~мол свистульку, яку зробив|зробила сам|сама
@річ_чому
Не знаю, звідки це в мене.
Без цього не засинаю.
Здається, колись мені це подарували.
Тримаю в руці, коли страшно.
~бурч І нікому не віддам.
~весел На щастя. Ну, поки що працює.

[story_skill] {вміння_вступ} {вміння}.
@вміння_вступ
Вмію
Я трохи вмію
~бурч Без мене тут би ніхто не вмів
~весел Хвалитися не люблю, але вмію
@вміння
лагодити сіті так, що й нова позаздрить ^knack_servant_15 ^knack_cleaner_10
розпалити вогонь навіть під дощем ^knack_stoker_25 ^knack_servant_15
знаходити воду там, де її нема ^knack_gatherer_20
співати так, що вовки замовкають ^оповідач
ходити так тихо, що й олень не почує ^knack_guard_15
вгадувати погоду за хмарами
куховарити з того, що росте під ногами ^knack_cook_25
плести кошики з будь-якої лози ^knack_gatherer_15 ^knack_keeper_10
~стар розповідати історії так, що всі засинають ^оповідач
~смілив дивитись тролю в очі й не кліпати ^knack_melee_15
~боязк ховатися так, що мене не знаходять ^ховається

[story_scar] {шрам}
@шрам
На руці в мене шрам від зуба карлика. Він потім довго мене боявся. ^карлики
Шрам на щоці — від гілки, а всім кажу, що від троля.
Двох пальців на лівій руці не згинаю — відморозив|відморозила серед гір.
Спина в мене — карта старих ран. Кожна зі своєю історією.
~дор Права нога з тої зими так і не загоїлась — шкутильгаю, але не відстаю. ^кульгає
~стар Коліно вже не те: накульгую, та дійду куди треба. ^кульгає
~смілив Стріла в стегно — пам'ятка з одного бою. Відтоді трохи кульгаю. ^кульгає
~мол Шрамів у мене поки нема. Хоча коліно — то окрема розмова.
~стар Шрамів стільки, що вже й не пам'ятаю, котрий від чого.

[story_ravens] {ворони}
@ворони
Воронів я спершу боявся|боялася. Тепер, бачачи їх, думаю: хтось за нами дивиться.
Кажуть, ворони пам'ятають усе. Хотів|Хотіла би я так.
Ворони — то очі Одіна. А я не знаю, чи хочу, щоб він мене бачив.
~стар Колись за мною теж літав ворон. Чи то мені наснилося.
~мол Мрію, щоб колись і в мене був свій ворон.

[story_wish] {мрія}
@мрія
Хочу хоч раз побачити Вальгаллу. Хоч одним оком.
Хочу дім, де мене чекають.
Хочу згадати своє справжнє ім'я.
Хочу знайти тих, кого забув|забула. Якщо вони ще є. ^шукає
Хочу, щоб мене поховали під деревом. Ну, коли прийде час.
~весел Хочу бенкет на сто людей. І щоб усі співали.
~бурч Хочу, щоб мене лишили в спокої. Ну, хоч на день.
~мол Хочу навчитися битися так, щоб мене боялися тролі.
~мол Мрію збудувати корабель і поплисти за туман.
~стар Хочу дожити до весни. Щороку хочу.
~стар Хочу тільки, щоб мене пам'ятали. Хоч хтось.

[story_trouble_surrounded] Вони вийшли з-за дерев, і я зрозумів|зрозуміла: втекти не вийде.
[story_trouble_captive] Мене тримали зв'язаним|зв'язаною, і я вже почав|почала забувати, як світить сонце.
[story_trouble_hungry] Їжа скінчилась раніше, ніж дорога.
[story_trouble_wounded] Звір підкрався, поки я спав|спала.
[story_trouble_lost] Без своєї речі я наче без руки — а вони сиділи просто біля неї.
[story_trouble_freezing] Вогонь згас, а нового не було з чого розпалити.
[story_trouble_chased] Я біг|бігла, не озираючись, бо знав|знала, що там побачу.
[story_trouble_giant] Воно сиділо біля мене, наче стерегло обід.
[story_trouble_astray] Я ходив|ходила колами, і кожне дерево було тим самим деревом.
[story_trouble_ambush] Мені сказали: клич — або тобі кінець. І я кликав|кликала.

// ---------------------------------------------------------------- 2026-11-01: роботи, яких бракувало
[work_lumber] {лісоруб} {робота_кінець?}
[work_lumber] {лісоруб}
@лісоруб
Дерево падає — бережись!
Тюк, тюк… а воно стоїть, наче насміхається.
Береза рубається легко. Не те що дуб.
Сокира тупиться швидше, ніж я втомлююсь. Майже.
Колода за колодою — от і день минув.
Ліс великий, а я один|одна. Нічого, потроху.
Головне — щоб дерево впало туди, куди я хочу, а не на мене.
Тріски летять — отже, робота йде.
Пеньків тут уже більше, ніж дерев.
Кожне дерево — то чиясь майбутня стіна.
~бурч Рубай, носи, складай. І знову рубай.
~бурч Хто це вигадав — будувати з дерева? Хай би з каменю.
~весел Чуєш, як гарно гупає? То моя пісня.
~весел Ще одне — і я герой лісу!
~стар Колись я валив|валила таке дерево з трьох ударів. Колись.
~мол Колись зрубаю найбільше дерево в лісі. Побачиш!
~смілив Хай тільки карлик сунеться — сокира в мене гостра.
~боязк У лісі хтось шарудить… Ні, то вітер. Мабуть.
[work_miner] {рудокоп} {робота_кінець?}
[work_miner] {рудокоп}
@рудокоп
Камінь твердий, а я — твердіший.
Дзень, дзень — і ще шматок.
Жила тут добра. Ще трохи покопаю.
Кайло — найкращий співрозмовник: не сперечається.
Пил у носі, пил у вухах, пил у каші.
Земля віддає руду неохоче. Як скупий торговець.
Кожен камінь — то чиясь майбутня піч.
Ще кілька ударів — і понесу.
~бурч Спина не гнеться, а кайло не легшає.
~бурч Хто мене вмовив копати? А, точно — ти.
~весел Глянь, як блищить! Майже як золото. Майже.
~стар Руки вже не ті, та камінь про це не знає.
~мол Може, я знайду тут щось дивовижне? Скарб?
~смілив Кажуть, у глибині живе щось старе. Хай живе — я не боюсь.
~боязк Лиш би не завалило… лиш би не завалило…
[work_servant] {слуга} {робота_кінець?}
[work_servant] {слуга}
@слуга
Двері — зачинити, вогонь — підкинути, підлогу — підмести.
У кожної речі є своє місце. Навіть якщо вона про це не знає.
Дім — він як живий: не доглянеш — занедужає.
Хтось знову лишив двері навстіж. Протяг же!
Тепло в хаті — то моя справа.
Підлатаю тут, підкину там — і все гаразд.
Порядок у домі — порядок у голові.
Скриню розібрав|розібрала. Тепер усе на своїх місцях.
~бурч Я вам не нянька. Хоча… так, нянька.
~бурч Ніхто не помічає, коли все гаразд. Тільки коли щось не так.
~весел Чистий дім, теплий вогонь — що ще треба?
~стар Скільки я тих хат доглядав|доглядала на своєму віку…
~мол Колись у мене буде свій дім. А поки — твій.

// ---------------------------------------------------------------- доповнення коротких списків
@робота
Без діла сидіти — гірше за будь-яку роботу.
Ще трохи — і можна буде похвалитись.
Роботи тут на сто людей, а нас — жменька.
~бурч Скільки не роби — все одно мало.
~весел Руки працюють, голова відпочиває.
~стар Робота старого не боїться. А от старий роботи — трохи.
@робота_кінець
Потім поговоримо.
Ага.
~стар Ох-ох.
@кухня
Хто їстиме — той хай і миє.
Казан кипить — душа співає.
Цибулі б. Без цибулі все не те.
Сьогодні — юшка. Завтра — теж юшка.
~бурч Пальці знову попік|попекла.
~мол Колись я зварю таке, що сам Одін прийде на вечерю.
@вогонь
Сирі дрова — то не дрова, а дим.
Поки вогонь горить — оселя жива.
Іскра туди, іскра сюди — головне, щоб не на дах.
~стар Вогонь пам'ятає більше за людей.
~мол Подивись, як гарно грає полум'я!
@піч
Вугілля знову кінчається.
Піч — як дракон: їсть багато, а дає мало.
Метал не любить поспіху.
~стар Колись я в кузні так і жив|жила — при вогні.
~мол Одного дня я викую собі меч. Справжній!
@збір
Ягода до ягоди — от і вечеря.
Тут кабан пасся. Усе потоптав.
Гриби люблять тінь. Як я в спеку.
~стар Знаю тут кожен кущ. Кожен!
~мол Ой, жук! Великий!
@прибирання
Пил, пил, пил — звідки його стільки?
Хтось знову розкидав пір'я. Ворони, звісно.
Усе на місце — і на душі легше.
~стар Мітла й терпіння — от і вся наука.
~мол Знайшов|Знайшла під лавою монетку! Ну, свою ж.
@бджоли
Бджоли не люблять крику. І я не люблю.
Тихо, малі, тихо — я свій|своя.
Вулик гуде, як ціле село.
~стар Бджола — найчесніший робітник. Не те що деякі.
~мол А бджоли сплять узимку? Цікаво.
@варта
Щось ворухнулось у кущах. Ні, заєць.
Поки я тут — сплять спокійно.
Око — на ліс, вухо — на дорогу.
Хто йде? А, свої.
~смілив Хай приходять. Я чекаю.
~боязк Тільки б нікого. Тільки б нікого.
@нічна_варта
Ніч довга, а смолоскип короткий.
Зорі — моя єдина компанія.
Сова кричить — отже, усе спокійно. Мабуть.
~стар Старі очі вночі бачать краще. Так кажуть.
~смілив Вночі я бачу все. Ну, майже все.
~боязк Тінь ворухнулась… Ні. Просто тінь.
@луки
Кабани знову рили біля паркану.
Тут навіть дощ теплий.
Найкраще місце, щоб почати все спочатку.
~стар На Луках я б і віку доживав|доживала.
~мол Тут нудно. Добре, але нудно.
@ліс
Під цими деревами вдень сутінки.
Карлики знову кидали каміння.
Ліс шепоче — не слухай, бо заведе.
~боязк Тролі, кажуть, ходять тут стежками. Великими стежками.
~смілив Ліс — то для тих, хто не боїться.
@болото
Драугри не сплять. Ніколи.
Тут чоботи не висихають ніколи.
Комарі тут такі, що з ними можна торгуватись.
~боязк Щось булькає в багні. Не хочу знати що.
~стар Болото пам'ятає кожного, хто в ньому лишився.
@гори
Сніг навіть у кашу потрапляє.
Тут дихати важко, а думати легко.
Дракони, кажуть, гніздяться вище. Я не перевіряв|перевіряла.
~стар Холод у кістках — тепер мій постійний гість.
~мол Звідси видно, мабуть, увесь світ!
@рівнини
Мошка тут злюща — кусає, наче мстить.
Сонце пече, а тіні нема де взяти.
Ячмінь тут росте гарно. Якщо фулінги не витопчуть.
~боязк Ці фулінги… вони ж дивляться на нас, правда?
~смілив Тут треба тримати сокиру напоготові. Я тримаю.
@туман
Тут навіть звуки губляться.
Не відходь від вогню — заблукаєш за три кроки.
Тут щось велике ходить. Я чув|чула.
Туман липне до шкіри, як павутина.
~смілив Що б там не ховалось — хай ховається далі.
~стар Колись я думав|думала, що туман — то край світу. Виявилось — ні.
@попіл
Тут земля тріщить під ногами.
Нащо ми сюди прийшли? Ну, я знаю нащо. Але все одно.
Пече навіть крізь чоботи.
Тут ніщо не росте. Навіть надія.
~бурч Хто в здоровому глузді будує тут дім?
~смілив Гаряче, але жити можна. Якщо обережно.
@море
Хвилі сьогодні неспокійні.
Чайки знову вкрали рибу.
Кажуть, у глибині живе змій. Великий.
Дерево тут сохне й солоніє.
~мол Колись попливу на тому кораблі — далеко-далеко.
~стар Море не прощає дурних. А розумних — інколи.
@плітка
{other} знову куняє біля вогню. Ну, не я ж.
{other} каже, що в лісі ходить троль. Троль, мабуть, теж про нас щось розповідає.
{other} знову заглядає в мою миску. Я все бачу.
{other} знову наспівує ту саму пісню. Третій день.
{other} кудись ховає сухарі. Я знаю куди.
~весел {other} — смішна душа. Тільки не передавай, що я так сказав|сказала.
~бурч {other} знову хропе так, що стіни трусяться.
~стар {other} ще всьому вчиться. Нічого, навчиться.
@ворон
Твій ворон знову сидів на даху й рахував нас.
Ворон дивиться на мене так, ніби знає мої таємниці.
Учора ворон приніс мені камінчик. Це подарунок чи погроза?
~бурч Ворон знову напаскудив на мою лаву.
~весел Розумна птаха! Майже як я.
~боязк Коли ворон довго дивиться — мені аж мурашки по шкірі.
@риба
Тихіше, бо всю рибу розлякаєш.
Риба любить терплячих.
~стар Колись я руками рибу ловив|ловила. Правда!
~мол А можна мені теж вудку?
~весел Як упіймаєш — я засмажу!
@тяжко
Ти що, всю гору з собою {приніс|принесла}?
Поклади, бо спина не залізна.
~стар У мої роки я б уже лежав|лежала під таким вантажем.
~мол Дай я допоможу! Ну, понесу щось маленьке.
~весел Отакої! Ти що, Тор?
@їжа
Пахне — аж слинка тече.
Нарешті хоч щось тепле.
Їжте, поки гаряче.
~стар Хліб на столі — то вже свято.
~мол Можна мені ще? Ну будь ласка.
@лагодити
Тріщину бачу — тріщину латаю.
Тут треба б ще дошку.
~стар Скільки я тих стін залатав|залатала…
~весел От і ціла! Як нова. Ну, майже.
@відступив
Ледве ноги виніс|винесла.
Наступного разу будемо готові.
~смілив Я ще повернусь. І тоді — подивимось.
~боязк Я ж казав|казала — не треба було туди йти.
~весел Ну, зате живі. Вже щось.
@втік
Я… просто швидко пішов|пішла. Дуже швидко.
Ноги самі понесли. Я тут ні до чого.
~стар Колись я не тікав|тікала. Але колись я був|була молодший|молодша.
@переміг
Ну що, хто наступний?
Ось так ми й живемо.
~смілив Ще трохи — і я почну їм співчувати.
~стар Старий|Стара, але ще кусається.
@ситий
Тепер можна й до роботи. Ну, трохи згодом.
Шлунок задоволений — і я задоволений|задоволена.
~весел Оце життя!
~мол Я б іще з'їв|з'їла. Але вже не влазить.
@голод
У животі хтось свариться.
Я б зараз і кору погриз|погризла.
Хто там на кухні? Ау!
~бурч Працюю, працюю, а їсти — дулю.
~весел Голодний|Голодна, як вовк. Тільки не кусаюсь. Поки.
@новенький
Ще не знаю, де тут що, але вже подобається.
Дякую за дах. Справді, дякую.
Тут пахне домом. Чи то мені здається.
~боязк Тут же безпечно, правда? Правда?
~смілив Хто тут головний над вартою? А, ніхто? То буду я.
@будуєш
Стіну рівніше став, бо впаде на голову.
Будуєш — то будуй, тільки двері не забудь.
~бурч Будує, будує… а де мені спати?
~весел О, якщо це для мене — то я не проти!
~мол Можна я подаватиму цвяхи?
@погода
Ну й {weather}… навіть ворони сховались.
{weather} — то знак. Не знаю чого, але знак.
~мол {weather}! Можна бігати?
~весел {weather}? То й що! Нам не звикати.
~боязк {weather}… а якщо дах не витримає?
@погода_кінець
Добре, що є дах.
До вогню, люди, до вогню.
~стар Кістки ниють — не обдурять.
~мол Завтра, мабуть, сонце буде.
@вечір
Вечір — час для балачок.
Ще трохи роботи — і до вогню.
Сонце сіло — тепер ми господарі. Поки нічні гості не прийшли.
~мол Розкажеш щось, як сядемо біля вогню?
~бурч І знову нічого не встиг|встигла.
@ранок
Роса холодна, а день буде теплий.
Ворони прокинулись раніше за всіх.
Ранок мудріший за вечір. Хоч і сонніший.
~стар Знову прокинувся|прокинулася. Вже добре.
~мол Ранок! Новий день! Що будемо робити?
@ніч
Тихо… навіть вогонь шепоче.
Вночі все страшніше, ніж є насправді. Майже все.
~стар Ночі тепер довші. Чи то я старший|старша.
~смілив Хай темно. Сокира бачить і в темряві.
@дивиться
Я щось не те сказав|сказала?
Хочеш, щоб я теж так подивився|подивилася?
~бурч Якщо ти чекаєш, що я заспіваю — не дочекаєшся.
~стар Дивись-дивись. Таких, як я, вже мало лишилось.
@рани
Сядь, бо впадеш.
Ох, ну й подряпини…
~весел Нічого, шрами прикрашають. Деяких.
@рани_кінець
Їж і відпочивай, чуєш?
Не лізь більше {сам|сама}. Тобто… не лізь.
~стар Бережи себе, дитино.
@думка
сьогодні вітер пахне снігом.
цікаво, чи ворони бачать сни.
треба б полатати чоботи. Знову.
якщо скласти всі камінці, що я збираю, вийде гора. Маленька.
і хто ж вигадав мед? Добра людина була.
кажуть, за морем є земля, де завжди літо. Брешуть, мабуть.
знову ця пісня в голові. Звідки вона?
хтось бачив мій ніж? Той, що з різьбленням?
дивно: прокидаюсь — і не пам'ятаю, що снилося. А тут кажуть — то на щастя.
в оселі «{base}» хоч є з ким поговорити.
сьогодні добрий день, щоб нічого не зламати.
мабуть, ворони сміються з нас. Тихенько.
~стар скільки разів я все починав|починала спочатку…
~стар молоді тепер не ті. Хоча й ми були не ті.
~мол колись я переможу троля. Ну, хоча б маленького.
~мол а якщо попросити ворона навчити мене літати?
~бурч і чого я знову все роблю сам|сама?
~бурч хтось украв мою ложку. Знову.
~весел добре жити, коли є що їсти й з ким посміятися.
~весел а що, як влаштувати свято? Просто так!
~смілив ще трохи — і піду на той пагорб сам|сама. Подивлюсь, що там.
~боязк а якщо вночі прийдуть? Хто тоді стоятиме на варті?
{neighbour} знову щось вигадує. Цікаво що.
@привіт
Ну здрастуй, {name}.
{name}! А я якраз про тебе думав|думала.
О, хто прийшов.
~смілив {name}! Як там вороги — ще є?
~боязк {name}… ти {сам|сама}? Усе гаразд?
@про_тебе
Що доброго принесла дорога?
Ти хоч їси вчасно?
~стар Ти знову без шолома ходиш? Ех.
~мол Розкажеш, де {був|була}?
~весел У тебе сьогодні гарний настрій. Чи то в мене?
@привіт_назад
Нарешті! А то вже хотіли шукати.
~смілив {Повернувся|Повернулася}! Значить, вороги не такі вже й страшні.
~боязк Ох, {name}, я вже почав|почала хвилюватися.
@питання
Їсти будеш?
~мол Принесеш мені щось наступного разу?
~стар Відпочинь хоч день, га?

// ---------------------------------------------------------------- стан людини: втома, платня, настрій
[rest] {відпочинок}
@відпочинок
Ух… втомився|втомилась. Посиджу трохи.
Ноги гудуть. Хвилинку перепочину.
Руки вже не слухаються. Сяду.
Хто б подумав — і я можу втомитись.
Трохи посиджу, а тоді знову.
Ще удар — і я б упав|упала. Краще сяду.
~бурч Усе. Без мене поки обійдетесь.
~весел Перерва! Найкраща частина роботи.
~стар Старі кості просять спокою. Послухаю їх.
~мол Я не втомився|втомилась! Просто… трохи сяду.

[tired] {втома} {втома_кінець?}
@втома
Очі злипаються.
Сьогодні я працював|працювала за двох.
Ще трохи — і засну стоячи.
Руки як чужі.
Спина болить так, що й не скажу.
~бурч Втомився|Втомилась. І нікому до того діла.
~весел Втома — то добре. Значить, щось зроблено.
~стар Раніше я втомлювався|втомлювалась під вечір. Тепер — під обід.
~мол Я ще можу! Ну… майже.
@втома_кінець
Посидіти б біля вогню.
Ліжко, де ти?
~бурч Хоч би хтось сказав «дякую».

[paid] {платня}
@платня
Платня — {wage}. Дякую.
{wage}. Чесно зароблено.
Дякую. {wage} — то не жарти.
О, {wage}! Тепер можна й жити.
{wage} — і на душі тепліше.
Добре, що в нас тут слово тримають. {wage}, як домовлялись.
~бурч {wage}. Могло бути й більше. Але дякую.
~весел {wage}! Оце я розумію — робота!
~стар {wage}… За моїх часів за таке давали менше.
~мол Мої перші {wage}! Ну, не перші. Але все одно!

[learned] {навчився}
@навчився
Руки вже звикають до роботи.
Тепер у мене виходить краще. Я відчуваю.
Ще трохи — і буду майстром|майстринею.
Робота вже не така чужа.
~стар Старого вчити — що камінь гріти. А ти глянь — гріється.
~мол Я вчуся! Скоро буду найкращим|найкращою!

[unpaid] {без_платні}
@без_платні
Скарбничка порожня… Знову без платні.
Прийшов|Прийшла за платнею — а там вітер свище.
Порожньо. Ну, нічого. Почекаю. Трохи.
Працюю за так? Ну, за дах. Але все ж.
~бурч Ще раз без платні — і подумаю, чи варто лишатись.
~весел Порожньо? Ну, хоч скриня гарна.
~стар Пусту скриню відкриваю — як пусту долоню.
~мол А платня буде? Ну, колись?

[mood_low] {сумно}
@сумно
Щось мені тут недобре.
Не знаю, скільки ще так витримаю.
Важко мені, {name}. Справді важко.
Останнім часом усе з рук валиться.
Я стараюсь. Але щось не так.
Думаю, чи не піти мені звідси.
~бурч Так жити — не жити.
~весел Навіть мені не до сміху. А це вже щось означає.
~стар Не для того я дожив|дожила до сивини.
~мол Я думав|думала, тут буде краще.
~боязк Не сердься, але мені тут погано.
~смілив Я не скаржусь. Але скажу прямо: так не годиться.

[low_nobed] {без_ліжка}
@без_ліжка
Свого ліжка в мене нема — сплю, де впаду.
Ліжко б мені. Хоч яке, аби своє.
Знову спав|спала на підлозі. Спина досі пам'ятає.
Усі мають де спати, а я — де прийдеться.
~стар У мої роки на голій землі спати — то кара.
[low_bedopen] {ліжко_надворі}
@ліжко_надворі
Моє ліжко під відкритим небом. Дощ — то моя ковдра.
Сплю, а на мене зірки дивляться. І дощ капає.
Дах би над ліжком. Хоч маленький.
~бурч Хто ставить ліжко під небом? Ти, звісно.
[low_hungry] {голодно}
@голодно
Голодно тут у нас.
Їжі мало, а роботи багато.
На порожній шлунок і працюється погано.
Їсти б хоч раз удосталь.
[low_starving] {дуже_голодно}
@дуже_голодно
Я вже не пам'ятаю, коли їв|їла востаннє.
Ще трохи без їжі — і я впаду.
Голод такий, що й думати не можу.
Без їжі довго не протягну. Кажу чесно.
[low_samefood] {та_сама_їжа}
@та_сама_їжа
Щодня одне й те саме. Скоро мені це снитиметься.
Знову те саме на вечерю. І вчора. І позавчора.
Хоч би щось інше покуштувати.
~бурч Ще раз оте саме — і я піду їсти траву.
~весел Мені б хоч раз чогось нового. Хоч ягідку.
[low_tired] {заморився}
@заморився
Я заморився|заморилась так, що й сон не бере.
Роботи забагато. Мені б перепочити.
Ні сил, ні часу на відпочинок.
[low_wet] {мокро}
@мокро
Мокну, як собака під дощем.
Мені б під дах. Хоч на хвилину.
Сухої нитки на мені нема.
~стар Дощ мені в кістки заліз — тепер не вижене.
[low_unpaid] {не_платять}
@не_платять
Працюю, а платні не бачу.
Скарбничка порожня вже вкотре.
Обіцяли платити — а де?
~бурч За дякую в мене тільки ворони працюють.
[low_nofire] {без_вогню}
@без_вогню
Вогню в нас нема — холодно й темно.
Без вогню хата — не хата.
Хоч би хтось розпалив вогнище.
~стар Старим кісткам без вогню ніяк.

[mood_good] {радісно}
@радісно
Добре в нас тут. Справді добре.
Давно мені так спокійно не було.
Дах є, їжа є, робота є. Чого ще треба?
Я радий|рада, що тоді погодився|погодилась прийти.
Іноді думаю: от воно — щастя. Маленьке, але моє.
Дякую тобі, {name}. За все це.
~весел Аж співати хочеться! Ну, трохи.
~бурч Ну… не так уже й погано. Тільки нікому не кажи.
~стар На старості — і такий спокій. Не чекав|чекала.
~мол Тут найкраще місце на світі! Ну, з тих, що я бачив|бачила.
~боязк Тут я вперше сплю без ножа під подушкою.

[leave_unhappy] {іду}
@іду
Досить з мене. Піду до Дому воронів.
Не можу я так більше. Прощавай.
Я казав|казала, що так не годиться. Ти не {слухав|слухала}. Бувай.
Шукатиму кращої долі. Тут її нема.
~бурч Бувайте. І не кажіть, що я не попереджав|попереджала.
~стар Піду. Хай хоч під іншим дахом спокійно помру.
~весел Навіть мені тут стало сумно. Піду, поки ще можу посміхатись.

// ---------------------------------------------------------------- тіло й дорога
[revived] {піднявся}
@піднявся
Ох… дякую. Думав|Думала, вже не встану.
Живий|Жива… досі живий|жива.
Ти мене {підняв|підняла}? Тепер я твій|твоя боржник|боржниця.
Голова гуде, як вулик. Але дякую.
Ще не час. Чую — ще не час.
~смілив Ще повоюємо. Дякую.
~боязк Я бачив|бачила темряву… а потім тебе.
~стар Боги ще не хочуть мене. Ну й добре.
~весел Ух! Отак полежав|полежала. Дякую!

[limp_home] {додому}
@додому
Піду додому… потихеньку.
Доповзу якось. Не чекайте мене до вечері.
Додому. Лиш би дійти.
~бурч Ну й прогулянка вийшла.
~стар Старі ноги, а дорогу пам'ятають.

[arrived] {прийшов}
@прийшов
Ось я й тут. Тут і житиму.
Дійшов|Дійшла! Ну, показуйте, де тут що.
Тож оце й є «{base}». Гарно.
Дорога була довга. Але я тут.
Привіт, нова оселе. Будемо дружити.
~весел Я тут! Ставте казан — новенький|новенька прийшов|прийшла!
~бурч Ноги збив|збила, поки дійшов|дійшла. Сподіваюсь, воно того варте.
~стар Ще одна хата на моєму віку. Може, остання. Добре б.
~боязк Тут же не кусаються, правда?

[no_room] {нема_місця}
@нема_місця
Тут нема для мене місця. Піду до Дому воронів.
Прийшов|Прийшла — а тут повно. Нічого, піду назад.
Мене тут не чекали. Повертаюсь до воронів.

// ---------------------------------------------------------------- робота стоїть (лісоруб, рудокоп)
[no_area] {нема_ділянки}
@нема_ділянки
Де мені працювати? Покажи ділянку.
Я готовий|готова, тільки скажи — де.
Стою, як пень. Ділянки нема.
~бурч Найняли, а де робити — не сказали.
~мол Покажеш, де копати чи рубати? Я швидко!
[no_axe] {нема_сокири}
@нема_сокири
Без сокири я тут як без рук.
Сокира б мені. Або хоч стара.
Сокира зламалась. Полагодь або дай іншу.
~бурч Чим рубати? Зубами?
~стар Ох, дерево руками не звалиш. Навіть я.
[no_pick] {нема_кайла}
@нема_кайла
Без кайла камінь не візьмеш.
Кайло б мені — і піду в роботу.
Кайло зламалось. Без нього — ніяк.
~бурч Чим копати? Нігтями?
~весел Дай кайло — і побачиш, як я вмію!

// ---------------------------------------------------------------- сусіди
[neighbour] {сусід}
@сусід
{neighbour} сьогодні щось мовчить. Не знаєш чому?
{neighbour} ділиться зі мною медом. Добра душа.
{neighbour} знову щось вирізьблює з дерева. Гарно ж виходить.
{neighbour} знову про щось сперечається з власною тінню.
{neighbour} десь біля вогню, якщо що.
{neighbour} хропе. Але по-доброму.
{neighbour} каже, що вночі в лісі горять вогники.
Коли поруч {neighbour}, добре помовчати. Не з кожним так.
{neighbour} знову співає за роботою. Тихенько, але я чую.
{neighbour} питає, коли ти знову підеш у похід. Чекає новин.
~весел {neighbour} — золота душа! Не сусідство, а свято.
~бурч {neighbour} знову бере мою миску. Я все пам'ятаю.
~стар {neighbour} нагадує мені когось. Не пам'ятаю кого.
~мол {neighbour} обіцяє навчити мене стріляти з лука!

// ---------------------------------------------------------------- прохання (NpcRequests): {item} що, {why} навіщо
[request_nag] {нагадую} {item} — {why}.
[request_nag] {нагадую} {item}.
[request_new] {прошу} {item} — {why}.
[request_new] {прошу} {item}.
@нагадую
не забудь, мені б
я все ще чекаю на
пам'ятаєш? Мені б
слухай, а що там із проханням? Мені б
~бурч ну то що? Мені ж потрібно
~весел я не тисну, але дуже б хотілося
~стар не підганяю, але мені б
~мол а ти принесеш? Мені б
@прошу
слухай… мені б
можна попросити? Мені б
маю прохання. Мені б
якщо буде нагода — мені б
~бурч ну от, знову щось треба. Мені б
~весел у мене маленьке прохання! Мені б
~стар не відмов старому|старій. Мені б
~мол а можна… мені б

// ---------------------------------------------------------------- «Як справи?» (NpcTalk {doing})
[how_low] {справи_погано}
@справи_погано
Чесно? Погано.
Не дуже, якщо по правді.
Бувало й краще. Значно краще.
Тримаюсь. Ледве.
~бурч А ти як думаєш?
~стар Ох, не питай.
~весел Навіть я не можу сказати «добре». Отак.
[how_hungry] {справи_голод}
@справи_голод
Їсти хочеться. А так — нічого.
Справи? У шлунку пусто, в голові — теж.
Було б краще, якби хтось нагодував.
~мол Я б поїв|поїла. Дуже.
[how_tired] {справи_втома}
@справи_втома
Втомився|Втомилась. Але живий|жива.
Ще трохи — і впаду. А так добре.
Робота висмоктала все. Посидіти б.
~стар Старі кості просять спочинку.
[how_good] {справи_добре}
@справи_добре
Добре! Справді добре.
Краще не буває. Ну, майже.
Живу, працюю, радію. Чого ще треба?
~весел Чудово! Аж хочеться обійняти когось. Не бійся, не тебе.
~бурч Та… нормально. Навіть добре. Не звикай.
~стар Давно мені так добре не було. Дякую.
~мол Найкраще! Тут найкраще!
[how_work] {справи_робота}
@справи_робота
Я — {job}. Роблю своє.
Працюю. {job} — то не жарти.
Як завжди: робота, їжа, сон. І знову робота.
Тримаю своє діло. Ти ж знаєш — {job} без діла не сидить.
Усе йде як треба. Мабуть.
~бурч Працюю. Як віл.
~весел Роблю те, що люблю. Ну, що вмію.
~стар Роблю, поки руки дозволяють.
~мол Учуся ремесла: {job} — то тобі не абищо!
[how_idle] {справи_без_діла}
@справи_без_діла
Живу, ходжу, дивлюсь. Дай мені роботу — не відмовлюсь.
Без діла нудно. Знайди мені заняття.
Тиняюсь без роботи. Руки сверблять.
~бурч Сиджу без діла. І не те щоб мені подобалось.
~стар Відпочиваю. Хоча краще б при ділі.
~мол Дай мені роботу! Будь-яку!
[how_free] {справи_вільний}
@справи_вільний
Та нічого. Йду, куди веде дорога.
Живу, як умію. Без дому, зате вільно.
Шукаю свого місця під цим небом.

// ---------------------------------------------------------------- розмова з найнятим: «Поговорімо»
[talk_invite] {запрошення}
@запрошення
Поговорімо. Про що?
Давай. Я слухаю.
О, поговорити — то я люблю. Питай.
Для тебе — завжди. Що цікавить?
Сідай. Про що балачка?
~бурч Ну, тільки недовго. Що?
~весел Нарешті хтось хоче поговорити! Питай що завгодно!
~стар Сідай, дитино. Розпитуй старого|стару.
~мол Ура! Про що говоритимемо?
~боязк Поговорити? Добре… тільки не про щось страшне.

[about_home] {про_дім} {про_дім_кінець?}
@про_дім
«{base}» — добре місце. Тут я вперше за довгий час сплю спокійно.
Тут добре. Тепло, є що їсти, є з ким побалакати.
Мені подобається, як тут пахне вечорами — дим і хліб.
Огорожу б міцнішу. А так — добре.
Тут затишно. Тільки протяги.
Мені тут подобається вогнище. Біля нього думається.
Якби ще лазню — було б як у казці.
Хата стоїть міцно. А людям тут добре.
~бурч Жити можна. Хоча я б дещо переробив|переробила.
~весел Найкраще місце, яке я бачив|бачила! Ну, з тих, що пам'ятаю.
~стар Тут добре старіти. Тихо.
~мол Тут класно! Тільки б іще вежу високу.
~боязк Тут безпечно. Здебільшого. Мені подобається «здебільшого».
~смілив Добре місце, але оборону б посилити. Я подумаю над цим.
@про_дім_кінець
Дякую, що питаєш.
Головне — що разом.
~весел Не жени мене звідси, гаразд?
~бурч Але скаржитись я не перестану. Звичка.

[about_folk] {про_сусідів}
[about_folk] {alone}{самотньо}
@про_сусідів
{neighbour} — душа надійна. Спиною до спини стати можна.
Є {neighbour}. Цікава людина — завжди щось вигадує.
{neighbour} живе тут. Мовчазна душа, але добра.
Найкраще мені балакається, коли поруч {neighbour}. Решта — то так, привіт-бувай.
{neighbour}? Хороша людина. Хропе, щоправда.
{neighbour} знає багато історій про море. Слухати можна до ранку.
~бурч {neighbour}. Більше нічого не скажу. {Сам|Сама} побачиш.
~весел {neighbour}! Ми вже як рідні.
~стар {neighbour} ще вчиться жити. Хай вчиться, поки я поруч.
~мол {neighbour} знає стільки всього! Хочу знати так само.
@самотньо
Поки що я тут сам|сама. Ну, ще ти й ворони.
Нікого. Тиша. Інколи це добре.
Ніхто, крім мене. Знайди мені компанію, га?
~весел Тільки я! Тому тут так весело.
~бурч Сам|Сама. І ніхто не забирає мою ложку.

[tale] {казка}
@казка
Кажуть, колись один чоловік спіймав рибу таку велику, що вона його й з'їла. Мораль? Не лови великої риби.
Знаєш, чому тролі такі сердиті? Бо їм ніхто ніколи не каже «добрий ранок».
Колись карлик украв у мене чобіт. Тепер десь у лісі ходить карлик у моєму чоботі. Сподіваюсь, тисне.
Кажуть, якщо вночі довго дивитися у вогонь, побачиш тих, хто згас. Я дивився|дивилася. Бачив|Бачила лише дим.
Була в одному селі жінка, що розмовляла з вороном. Ворон відповідав. Ніхто не знає, що саме — вона нікому не казала.
Одного разу я заблукав|заблукала у тумані й вийшов|вийшла до того ж вогнища, від якого пішов|пішла. Тричі.
Кажуть, Гальдор колись був звичайним торговцем. А потім продав комусь щось таке, що йому заплатили вічністю.
Є прикмета: якщо ворон сяде тобі на плече — до добра. Якщо на голову — до прання.
Жив собі воїн, що ніколи не програвав. Бо ніколи не бився. Мудрий був.
Казала мені одна стара: світ стоїть на дереві, а дерево — на терпінні. Не знаю, що вона мала на увазі. Може, нічого.
Одного разу ми смажили кабана, а він виявився не зовсім смаженим. Довго потім бігали.
Знаєш, чому драугри такі злі? Бо їх ніхто не поховав як слід. Я б теж злився|злилася.
Кажуть, у горах живе дракониця, що збирає яйця. Не свої. Чужі. Навіщо — не питай.
Я чув|чула, що Одін віддав око за мудрість. Мені б і пальця жаль. Значить, я не мудрий|мудра.
Колись ми з товаришем посперечались, хто довше простоїть під дощем. Обидва застудились. Нічия.
Хочеш загадку? Що має зуби, а не кусає? Гребінь. Що? Ну, я ж казав|казала — загадка.
Є така пісня, про корабель, що плив по небу. Слів не пам'ятаю, а мелодію — от послухай… ні, краще не буду.
Кажуть, кожна валькірія колись була людиною. Ця думка мені не дає спати.
~стар Коли я був|була молодим|молодою, тролі були меншими. Чи я був|була більшим|більшою. Тепер уже не розібрати.
~стар Знаєш, що я зрозумів|зрозуміла за всі ці роки? Що каша смачніша, коли її варить хтось інший.
~мол Я вчора бачив|бачила оленя з такими рогами — як дерево! Ну, майже як дерево.
~весел Слухай анекдот. Приходить троль до карлика… ні, забув|забула кінець. Але смішно було!
~бурч Нема в мене казок. Є правда: робота, їжа, сон. Кінець.
~смілив Колись я стояв|стояла сам|сама проти трьох карликів. Їх було двоє, але звучить краще, коли три.
~боязк Не люблю страшних історій. Розкажу смішну: я колись утік|утекла від зайця. Ну, я думав|думала, то вовк.

[about_ravens] {про_воронів}
@про_воронів
Розумні птахи. Інколи мені здається — розумніші за нас.
Вони все бачать. Мені від цього і спокійно, і страшно.
Хугін і Мунін — думка й пам'ять. Хотів|Хотіла би я мати хоч одне з двох напевно.
Ворон учора сидів на моєму вікні й дивився, як я їм. Довго дивився.
Кажуть, ворони переносять вісті до Одіна. Сподіваюсь, про мене — тільки добре.
Твій ворон — дивна птаха. Але мені подобається.
~бурч Крадуть усе, що блищить. Мою ложку теж.
~весел Я б і собі ворона хотів|хотіла! Навчив|Навчила би його співати.
~стар Ворони були тут задовго до нас. І будуть після.
~мол Вони вміють говорити? Справді? А мене навчать?
~боязк Вони так дивляться… ніби знають, коли я помру.
~смілив Добрі птахи. У бою, кажуть, теж не пасуть задніх.

[about_wish] {хочу}
@хочу
Тепле ліжко й тишу. Більше нічого.
Мені б хоч раз скупатися в гарячій воді.
Хочу навчитися чогось нового. Може, кувати?
Свій куточок біля вогню. Щоб ніхто не займав.
Медовухи доброї. Одну чарку. Ну, дві.
Хочу, щоб тут жило більше людей. Веселіше було б.
Щоб на столі було щось, крім одного й того самого.
Хочу побачити море. Не з берега — з корабля.
~бурч Щоб мене лишили в спокої. Хоч на день.
~весел Свята! Великого, з піснями й танцями!
~стар Хочу дожити до весни. І ще до однієї.
~мол Хочу свою сокиру! Справжню! І шолом!
~боязк Хочу, щоб огорожа була вища. Набагато вища.
~смілив Хочу піти з тобою в похід. Хоч раз.

[about_you] {про_тебе_думка}
@про_тебе_думка
Ти добра людина, {name}. Тут таких мало.
Ти мене {прихистив|прихистила}, коли нікому не було діла. Я пам'ятаю.
Ти ходиш туди, куди ніхто не наважується. Я це поважаю.
Ти дивн{ий|а}, {name}. Але дивн{ий|а} по-доброму.
Ти завжди кудись поспішаєш. Зупинись колись, подихай.
Ти — наче ворон: прилетиш, подивишся — і знову в дорогу.
~бурч Ти… нормальн{ий|а}. Не задирай носа.
~весел Ти найкращ{ий|а}! Ну, після мене.
~стар Ти нагадуєш мені когось із молодості. Когось доброго.
~мол Я хочу бути як ти, {name}! Отак само нічого не боятись!
~боязк З тобою я почуваюсь безпечніше. Чесно.
~смілив Ти б'єшся добре. Але я б'юся краще. Колись покажу.

// ---------------------------------------------------------------- розмови двох (NpcChatter: «- {@q_…}» / «- {@a_…}»)
[q_sleep] {п_сон}
@п_сон
Як спалося?
Чули вночі щось?
Чого це ти позіхаєш?
[a_sleep] {в_сон}
@в_сон
Погано. Хтось хропів, як троль.
Як убитий|убита. Нічого не снилося.
Снилося, що я літаю. Прокинувся|Прокинулася — а я на підлозі.
Вовки вили всю ніч. Чи то вітер.
~бурч Спалося б краще, якби дехто не хропів.
~стар Старі не сплять — старі лежать і думають.
[q_food] {п_їжа}
@п_їжа
Що сьогодні їстимемо?
Уже поїли?
Хто сьогодні куховарить?
[a_food] {в_їжа}
@в_їжа
Те, що вчора. І позавчора.
Ще не їв|їла. Чекаю на диво.
Не знаю, але пахне непогано.
Краще не питай — розчаруєшся.
~весел Щось смачне! Я певен|певна!
~бурч Знову щось недосолене.
[q_work] {п_робота}
@п_робота
Як робота?
Багато ще на сьогодні?
Важко сьогодні?
[a_work] {в_робота}
@в_робота
Робиться. Потихеньку.
Ще стільки, що й до ночі не впораюсь.
Руки болять, а так — нормально.
Сьогодні добре пішло. Аж дивно.
~бурч Робота як робота. Кінця не видно.
~весел Чудово! Ну, могло бути й гірше.
[q_weather] {п_погода}
@п_погода
Як думаєш, дощ буде?
Холодно сьогодні, правда?
Бачиш, які хмари над пагорбом?
[a_weather] {в_погода}
@в_погода
Буде. Коліно ниє.
Хто його знає. Небо тут примхливе.
Хай буде що буде — дах у нас є.
Головне, щоб не буря.
~стар Кістки кажуть — буде.
~мол А я люблю дощ!
[q_ravens] {п_ворони}
@п_ворони
Воронів сьогодні бачили?
Як гадаєш, ворони розуміють, що ми кажемо?
Чого це ворони так розкаркались?
[a_ravens] {в_ворони}
@в_ворони
Бачив|Бачила. Сиділи на даху й дивились на мене.
Розуміють, точно. Тому я при них мовчу.
Мабуть, вісті якісь Одіну несуть.
Може, їсти хочуть. Як і ми.
~боязк Не нагадуй. Мені від них моторошно.
~весел Вони просто балакучі. Як я!
[q_home] {п_дім}
@п_дім
Тобі тут подобається?
Як думаєш, довго ми тут проживемо?
Ти сумуєш за чимось?
[a_home] {в_дім}
@в_дім
Подобається. Краще, ніж будь-де до цього.
Сподіваюсь, довго. Я втомився|втомилась від доріг.
Сумую. Тільки не пам'ятаю за чим.
Тут добре. Аби тільки нічого не сталося.
~стар Мені вже нікуди йти. Тож — так, подобається.
~мол Тут класно! Але я хочу колись побачити світ.

// ---------------------------------------------------------------- пам'ять (NpcMemory): {m_x} що, {ago_x} коли («сьогодні», «учора», «кілька днів тому», «давно»)
// Тема mem_<подія> кажеться, лише коли він це пам'ятає (і про тебе — лише тобі). you_remember — у «Що думаєш про мене?»:
// кожен шаблон просить свою подію, тож підходить лише те, що було.
[mem_rescued] {пам_порятунок}
@пам_порятунок
{ago_rescued} ти мене з халепи {витягнув|витягнула}. Я пам'ятаю.
Якби не ти тоді — мене б тут не було.
Мені досі іноді сниться той день, коли ти {прийшов|прийшла} по мене.
Ти мене {врятував|врятувала}, {name}. Такого не забувають.
Я тоді вже й не чекав|чекала нікого. А {прийшов|прийшла} ти.
~весел Я щодня дякую богам за той день. І тобі. Тобі — частіше.
~бурч Дякувати я не вмію. Але той день пам'ятаю.
~стар Старі забувають багато. Але не того, хто їх урятував.
~боязк Мені досі страшно згадувати, як воно було. Добре, що ти тоді {прийшов|прийшла}.
~смілив Я б і сам|сама відбився|відбилася. Мабуть. Але дякую, що {прийшов|прийшла}.

[mem_revived] {пам_підняв}
@пам_підняв
{ago_revived} я лежав|лежала без тями, а ти мене {підняв|підняла}. Дякую.
Пам'ятаю, як ти мене медом {відпоїв|відпоїла}. Гіркий був, а солодший за все.
Ти мене тоді з того світу {витягнув|витягнула}. Чесно.
~бурч Того меду я досі не забув|забула. Гидкий. Але дякую.
~стар Вальгалла вже кликала, а ти не {пустив|пустила}.
~мол Я тоді думав|думала — усе! А тут ти з медом.

[mem_hired] {пам_найняв}
@пам_найняв
Пам'ятаю день, коли ти мене {взяв|взяла} до себе. Добрий був день.
Ти {дав|дала} мені дах. Я це ціную.
Відколи ти мене {взяв|взяла}, усе якось налагодилось.
~бурч Ти мене {найняв|найняла} — то й терпи тепер.
~весел Найкраще, що я зробив|зробила, — погодився|погодилась, коли ти {покликав|покликала}!
~стар На старості — і раптом потрібен|потрібна комусь. Дякую.

[mem_settled] {пам_оселився}
@пам_оселився
Я вже звикаю до цього місця.
Пам'ятаю, як уперше сюди прийшов|прийшла. Усе було чуже.
{ago_settled} я тут оселився|оселилася. А наче завжди тут жив|жила.
~мол Перший день тут був такий дивний! Тепер — як удома.
~стар Нова хата старим кісткам — як нові чоботи: спершу тисне, а тоді звикаєш.

[mem_request_done] {пам_прохання}
@пам_прохання
Дякую ще раз за {m_request_done}. Знадобилось.
Ти мені {приніс|принесла} {m_request_done} — я це пам'ятаю.
{ago_request_done} ти мене {виручив|виручила}. Тепер моя черга, якщо що.
~весел Ти мене тоді так {виручив|виручила}! Я аж танцювати хотів|хотіла.
~бурч Ти мене тоді {виручив|виручила}. Не думай, що я забув|забула.
~стар Хто допоміг у скруті — того пам'ятай. Я пам'ятаю.

[mem_neighbour_left] {пам_пішов}
@пам_пішов
{m_neighbour_left} — добра була людина. Шкода, що пішла.
Згадую інколи: {m_neighbour_left}. Де та людина тепер?
{ago_neighbour_left} нас стало менше. Сумно.
Людей тут поменшало. {m_neighbour_left} — пам'ятаєш?
~бурч Пішла людина — і нікому діла. {m_neighbour_left}, кажу, пішла! Ех.
~стар Люди приходять і йдуть. {m_neighbour_left} — теж пішла душа.
~весел Може, {m_neighbour_left} ще повернеться? Двері ж не зачинені.
~мол А куди пішла та людина — {m_neighbour_left}? Ти не знаєш?

[mem_fight_won] {пам_відбились}
@пам_відбились
Напад, що був {ago_fight_won}, ми відбили. Пам'ятаєш?
Після того бою я сплю спокійніше. Ми ж їх прогнали.
~смілив Хай ще приходять. Ми їм показали, хто тут господар.
~боязк Після того нападу я досі здригаюсь від кожного шереху.
~весел Пам'ятаєш, як ми їх гнали? Оце було свято!
~стар Відбилися — і слава богам. На старість мені такого вже не треба.

[mem_fight_lost] {пам_напад}
@пам_напад
Пам'ятаєш напад, що був {ago_fight_lost}? Нам тоді добряче дісталось.
Той напад… я тоді ледве утік|утекла.
Огорожу б вищу. Після того нападу я тільки про це й думаю.
~смілив Наступного разу я не відступлю. Слово даю.
~боязк Я досі бачу їх уві сні. Вони ж повернуться, правда?
~бурч Хтось колись подумає про оборону? Після того, що було?

[mem_starved] {пам_голод}
@пам_голод
Голод, що був {ago_starved}, я досі пам'ятаю. Думав|Думала — не витримаю.
Пам'ятаю ті дні без їжі. Не хочу знову.
Коли комора порожня, мені тепер трохи страшно.
~бурч Голодом мене тут уже морили. Пам'ятаю.
~стар Голод старого не жаліє. Я це добре запам'ятав|запам'ятала.
~весел Тепер я ціную кожну скибку. Голод — добрий учитель.

[mem_unpaid] {пам_платня}
@пам_платня
Порожню скарбничку, що була {ago_unpaid}, я не забув|забула.
Платню мені тоді так і не дали.
Працюю, а про платню тепер думаю з острахом.
~бурч Хто раз не заплатив — той і вдруге може.
~весел Нічого, гроші — не головне. Але й не останнє!
~стар У мої роки без платні — як без хліба.

[mem_missed] {пам_чекав}
@пам_чекав
Тебе так довго не було. Я вже хвилювався|хвилювалася.
{ago_missed} ти нарешті {повернувся|повернулася}. Ми тут без тебе сумували.
Не зникай так надовго, гаразд?
~бурч Пропадаєш, а ми тут сиди й гадай, чи ти {живий|жива}.
~весел Без тебе тут було тихо. Аж нудно!
~стар Старі чекають довго. Але не безкінечно, чуєш?

[you_remember] {ago_rescued} ти мене {врятував|врятувала}. Такого не забувають.
[you_remember] {ago_revived} ти мене медом на ноги {поставив|поставила}. Я це пам'ятаю.
[you_remember] Ти мене {взяв|взяла} до себе, коли більше нікому не було до мене діла.{m_hired}
[you_remember] Ти мені {приніс|принесла} {m_request_done}, коли я просив|просила. Не забуду.
[you_remember] Ти буваєш довго в дорозі, але завжди повертаєшся.{m_missed}
[you_remember] {m_unpaid}Скажу чесно: ту порожню скарбничку я пам'ятаю.
[you_remember] {m_starved}Пам'ятаю, як у нас не було що їсти. Хоч ти, мабуть, і не {винен|винна}.

// ---------------------------------------------------------------- ставлення до тебе (NpcAttitude): <тема>_hostile · _cold · _warm · _close
// Пробується першою (не завжди), нічого не підійшло — звичайна тема.
[see_player_hostile] {вітання_вороже}
@вітання_вороже
Чого тобі.
А, це ти. Шкода.
Йди своєю дорогою, {name}.
Не підходь.
~бурч Знову ти. Як же ти мені {набрид|набридла}.
~стар Я тебе бачу, {name}. І все пам'ятаю.
~боязк Ох… це ти. Я нічого не роблю, чесно.
~смілив Дивись мені, {name}.
[see_player_cold] {вітання_холодне}
@вітання_холодне
{name}.
А, ти.
Здоров. Якщо треба.
Ну, привіт.
~бурч Чого {прийшов|прийшла}?
~весел О, {name}. Ну… привіт.
~стар Ходиш тут…
~мол А… привіт.
[see_player_warm] {вітання_тепле}
@вітання_тепле
{name}! Добре, що ти тут.
О, {name}! Як ти?
Привіт, {name}. Сідай, якщо маєш час.
~бурч А, {name}. Ну, тобі я радий|рада.
~весел {name}! А я якраз про тебе думав|думала!
~стар {name}, дитино, ходи сюди.
~мол {name}! Привіт-привіт!
[see_player_close] {вітання_рідне}
@вітання_рідне
{name}! Нарешті!
Ось і ти, {name}. Тепер усе гаразд.
{name}! Без тебе тут не те.
~бурч {name}… Ну, іди вже сюди. Скучив|Скучила, чого там.
~весел {name}! Ходи, обійму!
~стар {name}, рідна душе. Як я радий|рада.
~мол {name}! Я тебе чекав|чекала!
~боязк З тобою мені спокійніше, {name}.

[back_hostile] {назад_вороже}
@назад_вороже
{Повернувся|Повернулася}. Шкода.
А я думав|думала, ти вже не прийдеш.
Знову тут.
~бурч І чого тобі там не сиділося.
[back_cold] {назад_холодне}
@назад_холодне
{Повернувся|Повернулася}. Ну.
А, ти знову тут.
Давно тебе не було. Не скажу, що сумував|сумувала.
~стар Прийшов хтось… а, це ти.
[back_warm] {назад_тепле}
@назад_тепле
З поверненням, {name}! Ми чекали.
{name}! Нарешті ти вдома.
Добре, що ти {цілий|ціла}. Розповідай!
~весел Ура, {name} {вернувся|вернулася}!
~стар Слава богам, {name}, ти вдома.
[back_close] {назад_рідне}
@назад_рідне
{name}! Я вже місця собі не знаходив|знаходила!
Ти вдома! Тепер я спокійний|спокійна.
~стар Слава богам, {name}, ти {живий|жива}. Ходи сюди.
~бурч Де ти пропадаєш?! …Добре, що {повернувся|повернулася}.
~весел {name}! Сідай, я тобі все розповім! Ні, ти мені!

[about_you_hostile] {думка_вороже}
@думка_вороже
Що думаю? Краще тобі не знати.
Я тобі не довіряю, {name}.
Від тебе я нічого доброго не бачив|бачила.
~смілив Скажу прямо: ти мені не до вподоби.
~боязк Я… я тебе боюся, якщо чесно.
~бурч Нічого хорошого. Можеш іти.
[about_you_cold] {думка_холодна}
@думка_холодна
Ти… нормальн{ий|а}. Мабуть.
Я тебе ще не знаю, {name}.
Ти тут {господар|господиня}. Цього досить.
~бурч Нічого не думаю. Працюю.
~стар Час покаже, яка ти людина.
[about_you_warm] {думка_тепла}
@думка_тепла
Ти добра людина, {name}. Я це бачу.
З тобою добре. Спокійно.
Я радий|рада, що саме ти тут {господар|господиня}.
~весел Ти мені подобаєшся! Ну, як людина.
~бурч Ти… нічого. Навіть добре.
~мол Ти справжній герой! Ну, для мене.
[about_you_close] {думка_рідна}
@думка_рідна
Ти для мене — рідна людина, {name}.
За тобою я піду куди завгодно.
Ти — найкраще, що зі мною тут сталося.
~бурч Не змушуй мене казати це вголос. Ти мені {дорогий|дорога}. Ось.
~стар Я на світі пожив|пожила, але таких, як ти, бачив|бачила небагато.
~мол Я хочу бути таким|такою, як ти! Справді!
~боязк Поки ти поруч, мені не страшно.

[talk_invite_hostile] {розмова_ворожа}
@розмова_ворожа
Нема нам про що говорити.
Говори швидше.
Ну. Чого?
~бурч Якщо коротко — то говори.
[talk_invite_cold] {розмова_холодна}
@розмова_холодна
Говори.
Слухаю. Коротко.
Ну, питай.
~весел Добре, давай. Тільки недовго.
[talk_invite_warm] {розмова_тепла}
@розмова_тепла
Залюбки! Про що?
Для тебе — завжди. Питай.
~бурч Ну, з тобою можна й поговорити.
~стар Сідай, сідай. Поговоримо.
[talk_invite_close] {розмова_рідна}
@розмова_рідна
З тобою — хоч до ранку! Питай.
Ох, як я люблю з тобою говорити. Що цікавить?
~бурч Для тебе я завжди маю час. Тільки нікому не кажи.
~мол Ура, говоримо! Про що?

[how_hostile] {справи_вороже}
@справи_вороже
Тобі яке діло?
Живу. Без твоєї допомоги.
Краще, ніж тобі б хотілося.
~бурч Не твоя справа.
[how_cold] {справи_холодно}
@справи_холодно
Нормально.
Як завжди.
Та нічого. Працюю.
~бурч Жити можна.
~стар Як у старого — помаленьку.

// ---------------------------------------------------------------- новини світу (NpcNews): боси, твої нові краї, новенький, набіг
// Імена босів — як у грі українською: Ейктюр, Прадавній, Кістянка, Мати, Яґлут, Королева, Батько.
[mem_news_boss_eikthyr] {новина_ейктюр}
@новина_ейктюр
Кажуть, Ейктюра повалили! Того оленя з блискавками в рогах.
Ейктюра більше нема. Може, тепер і грози на Луках тихіші будуть.
{Чув|Чула} новину? Ейктюр упав!
~весел Ейктюра звалили! Треба б відсвяткувати!
~стар Пам'ятаю часи, коли того оленя всі боялись. А тепер — нема.
~боязк Ейктюр упав… А хто тепер прийде замість нього?
~смілив Ейктюра повалили! Шкода, що без мене.
[mem_news_boss_elder] {новина_прадавній}
@новина_прадавній
Кажуть, Прадавнього в Чорному лісі звалили. Ліс тепер дихатиме легше.
Прадавній упав! Те дерево, що ходило, — уявляєш?
{Чув|Чула}? Прадавнього більше нема.
~стар Прадавній стояв там, ще як мене й на світі не було. А тепер — нема.
~боязк Тепер у Чорний ліс, може, й ходити можна. Ну, трохи.
~весел Прадавнього звалили! Дров із нього вистачило б на сто зим!
[mem_news_boss_bonemass] {новина_кістянка}
@новина_кістянка
Кістянку на Болоті прибили! Кажуть, смерділо — страх.
Кістянки більше нема. Може, й болото трохи висохне.
~бурч Кістянку звалили. Тепер, може, той сморід вітром рознесе.
~стар Кістянка… Скільки ж там добрих людей полягло. Нарешті.
~боязк Кістянки нема? А драугри куди ж тепер подінуться?
~смілив Кістянку звалили! Хотів|Хотіла би я бачити, як вона розсипалась.
[mem_news_boss_moder] {новина_мати}
@новина_мати
Кажуть, Матір у горах звалили! Дракона, уявляєш?
Мати впала. У горах тепер, кажуть, і вітер тихіший.
~весел Дракона звалили! Ото буде що онукам розповідати!
~стар За мого життя ніхто й близько до Матері не підходив. А тепер — ось.
~боязк Мати впала… А її діти? Вони ж не прилетять мститися?
~смілив Матір звалили! Ех, мені б туди, на ту гору.
[mem_news_boss_yagluth] {новина_яґлут}
@новина_яґлут
Яґлута на Рівнинах звалили! Фулінги тепер, мабуть, без ватажка.
Кажуть, Яґлута більше нема. Того, що з самих кісток і злості.
~весел Яґлут упав! Хай тепер фулінги самі собі ради дають!
~бурч Яґлута звалили. Добре. Тепер би ще мошку на Рівнинах хтось звалив.
~боязк Яґлута нема… а його вогонь? Він же не перекинеться сюди?
~стар Яґлут був старший за всіх нас разом. І він упав. Отже, усе можливо.
[mem_news_boss_queen] {новина_королева}
@новина_королева
Кажуть, Королеву в туманах убили! Ту, що під землею.
Королеви більше нема. Туман, може, тепер розійдеться.
~боязк Королеву вбили? А її шукачі… вони ж іще там?
~весел Королеву звалили! Тепер і мені корона не завадить!
~стар Королева… Я чув|чула про неї ще від старих. Думав|Думала — вигадка.
~смілив Королеву звалили! Отакі в нас тепер новини!
[mem_news_boss_fader] {новина_батько}
@новина_батько
Кажуть, Батька в попелі звалили! Того, що все спалює.
Батька більше нема. Попіл, кажуть, тепер холоне.
~стар Батько… Таке ім'я й вимовляти страшно. А тепер — нема.
~боязк Батька звалили? Я… я не вірю. Таких же не звалюють.
~весел Батька повалили! Хто тепер нам страшний? Хіба що кухар.
~смілив Батька звалили! Після такого й Вальгалла не страшна.

[mem_you_land_blackforest] {новина_ліс}
@новина_ліс
Ти вже {ходив|ходила} у Чорний ліс? Ну і як там?
Кажуть, ти вже в Чорному лісі {був|була}. Тролів {бачив|бачила}?
~боязк Чорний ліс… Ти туди {ходив|ходила}? Сміливо.
~стар У Чорному лісі будь {пильний|пильна}. Там дерева дивляться.
[mem_you_land_swamp] {новина_болото}
@новина_болото
Ти що, на Болоті {був|була}? Ну й запах від тебе!
Болото {бачив|бачила}? Кажуть, там мертві не лежать спокійно.
~бурч З Болота {повернувся|повернулася}? Чоботи хоч витри.
~мол Розкажи про Болото! Там справді драугри?
[mem_you_land_mountain] {новина_гори}
@новина_гори
Ти в горах {був|була}? Холодно там?
Кажуть, ти вже в гори {ходив|ходила}. Вовків {бачив|бачила}?
~стар У горах без теплого одягу не ходи. Я колись ледь не замерз|замерзла.
~весел Гори! Звідти, кажуть, видно край світу!
[mem_you_land_plains] {новина_рівнини}
@новина_рівнини
Ти вже на Рівнинах {був|була}? Фулінгів {бачив|бачила}?
Рівнини — то не жарти. Добре, що ти {цілий|ціла}.
~боязк Рівнини… Там же мошка, що кусає до смерті.
~смілив Рівнини! Колись і я там побуваю.
[mem_you_land_mistlands] {новина_туман}
@новина_туман
Ти в тумані {був|була}? І як там — що видно?
Туманні землі… Ти справді туди {ходив|ходила}?
~боязк У тумані щось ходить. Ти ж {бачив|бачила}, правда?
~мол Розкажи про туман! Там є чарівники?
[mem_you_land_ashlands] {новина_попіл}
@новина_попіл
Ти що, в Попелястих землях {був|була}?! І {живий|жива}?
Попіл на тобі… Ти звідти, де все горить?
~стар Попелясті землі… Я думав|думала, туди ніхто живим не ходить.
~весел Ти в Попелі {був|була}! Ну ти й відчайдух!
[mem_you_land_deepnorth] {новина_північ}
@новина_північ
Далека Північ! Ти й там {побував|побувала}?
~стар Далі на північ уже нічого нема. А ти там {був|була}.
[mem_you_land_ocean] {новина_море}
@новина_море
Ти вже в море {виходив|виходила}? Змія {бачив|бачила}?
Кажуть, ти вже в море {плавав|плавала}. Страшно було?
~мол Візьмеш мене колись у море?

[mem_news_newcomer] {новина_новенький}
@новина_новенький
У нас нова людина — {m_news_newcomer}. Сподіваюсь, приживеться.
{m_news_newcomer} тепер живе з нами. Ще одна ложка за столом.
Нова душа в оселі: {m_news_newcomer}. Треба буде познайомитись.
~бурч Ще одна людина — {m_news_newcomer}. Тепер черга до вогню ще довша.
~весел {m_news_newcomer} з нами! Веселіше буде!
~стар Новенькі — то добре. Хай {m_news_newcomer} почувається як удома.
~боязк Нова людина… {m_news_newcomer}. Вона ж добра, правда?

[raid_now] {набіг}
@набіг
Чуєш? Вони йдуть!
До зброї! Нападають!
Тримаймося разом!
Зачиняйте двері — вони близько!
~смілив Нарешті! Хай підходять!
~боязк Ой, ні-ні-ні… вони вже тут!
~стар Знову… Я це вже бачив|бачила.
~бурч Отакої. І саме тоді, коли я сів|сіла їсти.
";
    }

    // Who speaks unasked, and of what (npc-plan.md): every resident and hired person, now and then (by its chattiness: a
    // talkative one every two minutes or so, a quiet one rarely), when a player is near and it is free (not talking, not
    // fighting, not in a scene, awake): the most pressing thing it has to say — you are back after long, you are hurt, the
    // weather, the hour, its work, your raven, someone near — else a thought of its own; not the same thing twice in a row.
    // Where it is owned; a line is a Say (one message).
    internal static class NpcVoice
    {
        internal static readonly int KSeen = "RavenWatch_npcSeen".GetStableHashCode(); // string "player=worldsec;…": when it last saw whom

        internal static string Weather()
        {
            EnvSetup e = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            string n = e != null && e.m_name != null ? e.m_name.ToLowerInvariant() : "";
            if (n.Contains("storm") || n.Contains("thunder")) return "storm";
            if (n.Contains("snow")) return "snow";
            if (n.Contains("rain")) return "rain";
            if (n.Contains("mist") || n.Contains("fog")) return "fog";
            return "";
        }

        internal static string DayPart()
        {
            if (EnvMan.instance == null) return "day";
            if (EnvMan.IsNight()) return "night";
            float f = EnvMan.instance.GetDayFraction();
            return f < 0.33f ? "morning" : f > 0.68f ? "evening" : "day";
        }

        // ---------------------------------------------------------------- what the story (or a module) adds — wired once, like NpcModules
        // a source of themes: adds (theme, weight) for this person near this player now (≥ 50: said first, below: by chance)
        internal delegate void Source(NpcActor a, Player p, List<KeyValuePair<string, float>> themes);
        internal static readonly List<Source> Sources = new List<Source>();
        // a mute: true keeps that theme unsaid for this person now (a mourning: no jokes); the story's levers can do it too:
        // «npcflag mute_<theme>» on a person
        internal static readonly List<Func<NpcActor, string, bool>> Mutes = new List<Func<NpcActor, string, bool>>();

        private static bool Muted(NpcActor a, string theme)
        {
            if (Has(a, "mute_") && NpcLevers.Flag(a, "mute_" + theme)) return true;
            foreach (Func<NpcActor, string, bool> m in Mutes) if (m(a, theme)) return true;
            return false;
        }

        // may it speak unasked now (a remark, a mutter, a talk of two): not asleep, not talked to by you, not held quiet — by a
        // scene (a scene keeps it quiet unless its plan Speaks: a rescue's calls) or by the story's lever «npcflag hush»
        internal static bool Free(NpcActor a)
        {
            if (a == null || a.Mind == null || a.Mind.Asleep || NpcTalk.With == a) return false;
            NpcMind.Plan scene = a.Mind.Of(NpcMind.Layer.Scene);
            if (scene != null && !scene.Speaks) return false;
            return !(Has(a, "hush") && NpcLevers.Flag(a, "hush"));
        }

        // its story vars name this at all (a look into the string before it is taken apart: most people have none)
        private static bool Has(NpcActor a, string part) { ZDO z = a.Zdo; return z != null && z.GetString(NpcLevers.KVars, "").IndexOf(part, StringComparison.Ordinal) >= 0; }

        internal static void Tick(NpcActor a)
        {
            if (a == null || !a.Owned || a.Mind == null || a.Def == null || Time.time < a.NextLook) return;
            a.NextLook = Time.time + 10f; // (a look round every ten seconds: you back after long is said at once)
            Player p = Player.GetClosestPlayer(a.transform.position, 12f);
            if (p == null) return;
            if (!NpcResident.Is(a) && !NpcTalk.Hired(a)) return; // (a stranger keeps to itself: its greeting is its own habit)
            bool back = Saw(a, p);
            NpcAttitude.SawYou(a, p); // (a day it sees you: a little warmer — every look round, not only when it speaks)
            if (back) NpcMemory.Remember(a, "missed", p.GetPlayerID(), ""); // (you were away long)
            a.Stared = Utils.DistanceXZ(p.transform.position, a.transform.position) < 4f && p.GetVelocity().magnitude < 0.5f ? a.Stared + 1 : 0; // (you stand by it, still)
            bool stare = a.Stared >= 3; // (half a minute)
            if (stare) a.Stared = 0;
            if (!back && !stare && Time.time < a.NextRemark) return;
            a.NextRemark = Time.time + UnityEngine.Random.Range(120f, 300f) * (1.6f - NpcPersona.Of(a).Chatty); // (a chatty one sooner)
            if (a.Mind.NowLayer == NpcMind.Layer.Fight || !Free(a) || NpcChatter.IsTalking(a)) return;
            List<KeyValuePair<string, float>> themes = new List<KeyValuePair<string, float>>();
            if (back) themes.Add(new KeyValuePair<string, float>("back", 100f));
            if (stare) themes.Add(new KeyValuePair<string, float>("stare", 90f));
            if (p.GetHealthPercentage() < 0.4f) themes.Add(new KeyValuePair<string, float>("hurt", 80f));
            if (Time.time - a.Mind.FoughtAt < 90f) themes.Add(new KeyValuePair<string, float>(a.Mind.FoughtRan ? "after_flight" : a.Mind.FoughtWon ? "after_fight" : "after_fight_lost", 70f));
            if (NpcHunger.IsHungry(a)) themes.Add(new KeyValuePair<string, float>("hungry", 45f));
            else if (a.Hunger != null && Time.time - a.Hunger.LastMeal < 300f) themes.Add(new KeyValuePair<string, float>("fed", 30f));
            if (NpcResident.Is(a))
            {
                float mood = NpcToil.Mood(a);
                if (mood < 30f) { themes.Add(new KeyValuePair<string, float>(LowTheme(a), 60f)); themes.Add(new KeyValuePair<string, float>("mood_low", 35f)); } // (what weighs on it, said before it goes — NpcToil; the plain complaint only now and then)
                else if (mood > 75f) themes.Add(new KeyValuePair<string, float>("mood_good", 18f));
                if (NpcToil.Tired(a) >= 70f) themes.Add(new KeyValuePair<string, float>("tired", 40f));
                themes.Add(new KeyValuePair<string, float>("neighbour", 10f));
            }
            if (a.Worker != null && (a.Worker.JobId == "lumber" || a.Worker.JobId == "miner"))
            {
                string lacks = NpcHew.Lacks(a, a.Worker.JobId == "lumber");
                Vector3 plot;
                if (lacks != null) themes.Add(new KeyValuePair<string, float>(NpcHew.Area(a, out plot) ? (a.Worker.JobId == "lumber" ? "no_axe" : "no_pick") : "no_area", 50f));
            }
            long settled = a.Zdo != null ? a.Zdo.GetLong(NpcResident.KSettledAt, 0L) : 0L;
            if (settled > 0 && NpcRescue.Now - settled < (EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f)) themes.Add(new KeyValuePair<string, float>("new_home", 35f));
            if (p.InPlaceMode()) themes.Add(new KeyValuePair<string, float>("building", 40f));
            if (p.IsEncumbered()) themes.Add(new KeyValuePair<string, float>("heavy", 35f));
            ItemDrop.ItemData held = p.GetCurrentWeapon();
            if (held != null && held.m_dropPrefab != null && held.m_dropPrefab.name.StartsWith("FishingRod", StringComparison.Ordinal)) themes.Add(new KeyValuePair<string, float>("fishing", 30f));
            if (a.Worker != null && a.Worker.JobId == "guard" && EnvMan.IsNight()) themes.Add(new KeyValuePair<string, float>("night_watch", 25f));
            string land = Land(a);
            if (land.Length > 0) themes.Add(new KeyValuePair<string, float>("land_" + land, 12f));
            if (Weather().Length > 0) themes.Add(new KeyValuePair<string, float>("weather", 30f));
            string part = DayPart();
            if (part != "day") themes.Add(new KeyValuePair<string, float>(part, 15f));
            if (a.Worker != null && a.Worker.Works && a.Mind.NowLayer == NpcMind.Layer.Job) { themes.Add(new KeyValuePair<string, float>("work_" + a.Worker.JobId, 25f)); themes.Add(new KeyValuePair<string, float>("work", 10f)); }
            if (p == Player.m_localPlayer && Trial.HasRaven(p)) themes.Add(new KeyValuePair<string, float>("raven", 12f));
            themes.Add(new KeyValuePair<string, float>("other", 12f));
            themes.Add(new KeyValuePair<string, float>("see_player", 15f));
            themes.Add(new KeyValuePair<string, float>("idle", 20f));
            foreach (Source s in Sources) { try { s(a, p, themes); } catch (Exception e) { Npcs.Log("npc remark source: " + e.Message); } } // (a module's or the story's own themes)
            themes.RemoveAll(t => t.Key == a.LastRemark || Muted(a, t.Key) || !NpcPhrases.Has(a, t.Key));
            themes.Sort((x, y) => y.Value.CompareTo(x.Value));
            foreach (KeyValuePair<string, float> t in themes)
            {
                if (t.Value < 50f && UnityEngine.Random.value > 0.55f) continue; // (the pressing ones first; the rest by a little chance, in order)
                string line = NpcPhrases.Compose(a, t.Key, p);
                if (string.IsNullOrEmpty(line)) continue;
                a.LastRemark = t.Key;
                a.Say(line, p); // (to the one it noticed)
                return;
            }
        }

        // what weighs on it most, as a theme (low_hungry, low_nobed…: NpcToil.Why)
        internal static string LowTheme(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            switch ((NpcToil.Why)(z != null ? z.GetInt(NpcToil.KWhy, 0) : 0))
            {
                case NpcToil.Why.NoBed: return "low_nobed";
                case NpcToil.Why.BedOpen: return "low_bedopen";
                case NpcToil.Why.Hungry: return "low_hungry";
                case NpcToil.Why.Starving: return "low_starving";
                case NpcToil.Why.SameFood: return "low_samefood";
                case NpcToil.Why.Tired: return "low_tired";
                case NpcToil.Why.Wet: return "low_wet";
                case NpcToil.Why.Unpaid: return "low_unpaid";
                case NpcToil.Why.NoFire: return "low_nofire";
                default: return "mood_low";
            }
        }

        // its base's land (its words of the place: land_meadows, land_mountain…)
        private static string Land(NpcActor a)
        {
            if (WorldGenerator.instance == null || !NpcResident.Is(a)) return "";
            switch (WorldGenerator.instance.GetBiome(NpcRules.Home(a)))
            {
                case Heightmap.Biome.Meadows: return "meadows";
                case Heightmap.Biome.BlackForest: return "forest";
                case Heightmap.Biome.Swamp: return "swamp";
                case Heightmap.Biome.Mountain: return "mountain";
                case Heightmap.Biome.Plains: return "plains";
                case Heightmap.Biome.Mistlands: return "mist";
                case Heightmap.Biome.AshLands: return "ash";
                case Heightmap.Biome.Ocean: return "sea";
            }
            return "";
        }

        // it sees this player: true when it had not for most of a game day (kept in its ZDO, so a reload does not count)
        private static bool Saw(NpcActor a, Player p)
        {
            ZDO z = a.Zdo;
            if (z == null) return false;
            long now = (long)NpcRescue.Now, day = (long)(EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f);
            string id = p.GetPlayerID().ToString(CultureInfo.InvariantCulture);
            Dictionary<string, long> seen = new Dictionary<string, long>();
            foreach (string e in z.GetString(KSeen, "").Split(';')) { int eq = e.IndexOf('='); long t; if (eq > 0 && long.TryParse(e.Substring(eq + 1), out t)) seen[e.Substring(0, eq)] = t; }
            long last;
            bool had = seen.TryGetValue(id, out last);
            if (had && now - last < 600) return false; // (seen lately: nothing to write)
            seen[id] = now;
            if (seen.Count > 6) { string oldest = null; long o = long.MaxValue; foreach (KeyValuePair<string, long> e in seen) if (e.Value < o) { o = e.Value; oldest = e.Key; } seen.Remove(oldest); }
            List<string> l = new List<string>();
            foreach (KeyValuePair<string, long> e in seen) l.Add(e.Key + "=" + e.Value);
            z.Set(KSeen, string.Join(";", l.ToArray()));
            return had && now - last > day * 3 / 4;
        }
    }
}
#endif

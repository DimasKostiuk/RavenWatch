#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Слова ворона» (the user, 2026-10-26): ravens can mimic speech, so yours learns words and short phrases from you
    // and drops them into its talk. From trust FromLevel on; SlotsAt by trust (3 at level 3 … 8 at level 7).
    //   - taught: its own wish «Навчи мене» (Requests "teach"): the wish's card has a field to write the word in;
    //   - overheard: now and then it catches a word a living player wrote in the chat near you (never a raven's line:
    //     those are put into the chat locally, not sent; never a command). It croaks it at once, and the word waits in
    //     the book (seal ᚨ beside its name on «Ворони») for you to keep it or throw it out.
    // Each word has its moment («коли захоче», at a meeting, in a fight, when you die, at food, after a win, by the fire),
    // chosen in the book's card: the first choice is free, every later change and every word forgotten costs one of
    // the raven's sparklets (Requests: a wish done gives them) — so the words cannot be swept out or reshuffled at once. Said rarely (one word of a raven every MinGap at most, the same word every WordGap), and
    // never over the director. At first it mangles the word; the more it says it, the cleaner (Uses). Munin keeps a little
    // of it on purpose. Words past the slots of a fallen trust sleep (kept, not said) until the trust is back.
    // 8 words all said clean: the nickname «Тріпло» (Hugin) / «Пиздабол» (Munin) — Nicks d_words.
    // All of it lives in the raven (customData): a raven given away takes its words along.
    internal static class Words
    {
        internal const int FromLevel = 3, MaxChars = 24, MaxWords = 3;
        internal const int CleanAt = 8, LightAt = 3;           // uses: from here clean, from here lightly mangled
        internal static float MinGap = 480f, WordGap = 1080f;   // s: any taught word of a raven / the same word again
        internal static float AnyEvery = 840f;                  // s: about this often it says a «коли захоче» word by itself
        internal static float SeasonChance = 0.05f;             // a «коли захоче» word slipped into one of its ordinary lines
        internal static float MomentChance = 0.4f;
        internal static float HearChance = 0.06f, HearRange = 40f; // a chat message near you (a shout from anywhere)
        internal static int HearEveryDays = 2;                  // game days between two words caught from the chat (both ravens)
        internal static int HeardKeepDays = 3;                  // game days a caught word waits in the book

        internal enum Moment { Any = 0, Greet, Fight, Death, Food, Win, Fire }
        internal const int MomentCount = 7;

        private const string Key = "RavenWatch_words", HeardKey = "RavenWatch_wordHeard", NextHearKey = "RavenWatch_wordNextHear";

        internal sealed class Word
        {
            public string Text = "";
            public Moment When = Moment.Any;
            public int Uses;
            public bool Fixed;            // its moment chosen once: a change now costs a sparklet
            public float SaidAt = -9999f; // (this game only)
        }

        // ---------------------------------------------------------------- the store

        private static readonly Dictionary<string, List<Word>> _cache = new Dictionary<string, List<Word>>();

        // its words, the oldest first ("moment\tuses\tfixed\ttext" a line; the text never holds a tab: Clean)
        internal static List<Word> Of(ItemDrop.ItemData item)
        {
            List<Word> l = new List<Word>();
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return l;
            List<Word> was;
            _cache.TryGetValue(raw, out was);
            foreach (string line in raw.Split('\n'))
            {
                string[] a = line.Split('\t');
                if (a.Length == 3) a = new[] { a[0], a[1], "1", a[2] }; // (the first test build: no mark yet)
                if (a.Length < 4 || a[3].Length == 0) continue;
                Word w = new Word { When = (Moment)Mathf.Clamp(Errands.I(a[0], 0), 0, MomentCount - 1), Uses = Errands.I(a[1], 0), Fixed = a[2] == "1", Text = a[3] };
                if (was != null) { Word old = was.Find(x => x.Text == w.Text); if (old != null) w.SaidAt = old.SaidAt; } // (when it was said: kept across a re-read)
                l.Add(w);
            }
            _cache[raw] = l;
            return l;
        }

        private static void Save(ItemDrop.ItemData item, List<Word> l)
        {
            if (item == null) return;
            if (l.Count == 0) { item.m_customData.Remove(Key); return; }
            StringBuilder sb = new StringBuilder();
            foreach (Word w in l)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(((int)w.When).ToString(CultureInfo.InvariantCulture)).Append('\t').Append(w.Uses.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(w.Fixed ? '1' : '0').Append('\t').Append(w.Text);
            }
            string raw = sb.ToString();
            item.m_customData[Key] = raw;
            if (_cache.Count > 64) _cache.Clear(); // (every word said writes a new record: the old ones need not stay)
            _cache[raw] = l;
        }

        // how many words it can keep at its trust (0: not yet)
        internal static int Slots(ItemDrop.ItemData item)
        {
            int lv = Trust.Level(item);
            if (lv < FromLevel) return 0;
            return lv >= 7 ? 8 : lv; // 3, 4, 5, 6 … 8
        }

        internal static bool Open(ItemDrop.ItemData item) { return Slots(item) > 0; }
        internal static bool HasRoom(ItemDrop.ItemData item) { return Of(item).Count < Slots(item); }

        // 1 heavily mangled, 2 lightly, 3 clean
        internal static int Stage(Word w) { return w.Uses >= CleanAt ? 3 : w.Uses >= LightAt ? 2 : 1; }

        // all eight said clean (the nickname)
        internal static bool AllClean(ItemDrop.ItemData item)
        {
            List<Word> l = Of(item);
            if (l.Count < 8) return false;
            for (int i = 0; i < 8; i++) if (Stage(l[i]) < 3) return false;
            return true;
        }

        internal static string MomentName(Moment m)
        {
            switch (m)
            {
                case Moment.Greet: return Lang.T("При зустрічі");
                case Moment.Fight: return Lang.T("У бою");
                case Moment.Death: return Lang.T("Коли гинеш");
                case Moment.Food: return Lang.T("За їжею");
                case Moment.Win: return Lang.T("Після перемоги");
                case Moment.Fire: return Lang.T("Біля вогню");
            }
            return Lang.T("Коли захоче");
        }

        internal static string MomentTip(Moment m)
        {
            switch (m)
            {
                case Moment.Greet: return Lang.T("коли ти заходиш у світ або ворон повертається до тебе");
                case Moment.Fight: return Lang.T("коли починається бій");
                case Moment.Death: return Lang.T("коли ти воскресаєш після смерті");
                case Moment.Food: return Lang.T("коли ти їси або годуєш його");
                case Moment.Win: return Lang.T("коли падає бос чи звір із зірками");
                case Moment.Fire: return Lang.T("коли ви грієтесь біля вогню");
            }
            return Lang.T("будь-коли сам, а часом вставить у звичайну фразу");
        }

        // ---------------------------------------------------------------- what may be a word

        // what is typed or heard, made a word it can say: letters, digits, a little punctuation; no tags, braces, links
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c0 in s)
            {
                char c = c0;
                if (char.IsSurrogate(c) || char.IsControl(c)) { if (c == '\t' || c == '\n') c = ' '; else continue; }
                if ("<>{}[]|@^~$\\/#`*_=%&".IndexOf(c) >= 0) continue; // (tags, the phrase files' marks, the game's $tokens)
                if (c == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' ')) continue;
                sb.Append(c);
            }
            string t = sb.ToString().Trim();
            string low = t.ToLowerInvariant();
            if (low.Contains("http") || low.Contains("www") || low.Contains(".com") || low.Contains(".net") || low.Contains(".gg")) return "";
            string[] parts = t.Split(' ');
            if (parts.Length > MaxWords) t = string.Join(" ", parts, 0, MaxWords);
            if (t.Length > MaxChars) t = t.Substring(0, MaxChars).TrimEnd();
            t = t.Trim(' ', ',', '.', '-', '!', '?', ':', ';');
            int letters = 0;
            foreach (char c in t) if (char.IsLetter(c)) letters++;
            return letters >= 2 ? t : "";
        }

        // ---------------------------------------------------------------- learning

        // a word taught by you (the wish's card). null: learnt, else why not
        internal static string Teach(ItemDrop.ItemData item, string typed)
        {
            string w = Clean(typed);
            if (w.Length == 0) return Lang.T("Це не слово. Напиши щось людське — літерами, без посилань.");
            List<Word> l = Of(item);
            if (l.Count >= Slots(item)) return Lang.T("Йому вже нема куди вчити: спершу прибери одне слово в книзі (ᚨ біля імені).");
            if (l.Exists(x => Same(x.Text, w))) return Lang.T("Це слово він уже знає.");
            l.Add(new Word { Text = w });
            Save(item, l);
            return null;
        }

        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        internal const int Price = 1; // sparklets for a word forgotten, or its moment changed (not the first choice)

        // the price of setting this word's moment now (0: its first choice)
        internal static int MomentPrice(Word w) { return w.Fixed ? Price : 0; }

        // null: forgotten, else why not
        internal static string Forget(ItemDrop.ItemData item, int index)
        {
            List<Word> l = Of(item);
            if (index < 0 || index >= l.Count) return Lang.T("Такого слова вже нема.");
            if (!Requests.SpendSparks(item, Price)) return NoSparks();
            l.RemoveAt(index);
            Save(item, l);
            return null;
        }

        // null: set, else why not (the same moment: nothing, nothing paid)
        internal static string SetMoment(ItemDrop.ItemData item, int index, Moment m)
        {
            List<Word> l = Of(item);
            if (index < 0 || index >= l.Count) return Lang.T("Такого слова вже нема.");
            Word w = l[index];
            if (w.When == m) return null;
            if (!Requests.SpendSparks(item, MomentPrice(w))) return NoSparks();
            w.When = m;
            w.Fixed = true;
            Save(item, l);
            return null;
        }

        internal static string NoSparks() { return Lang.F("Потрібна {0} — їх дають виконані прохання ворона.", Lang.N(Price, "іскринка|іскринки|іскринок")); }

        // the word caught from the chat, waiting in the book ("" none); kept HeardKeepDays
        internal static string Heard(ItemDrop.ItemData item)
        {
            string raw;
            if (item == null || !item.m_customData.TryGetValue(HeardKey, out raw) || string.IsNullOrEmpty(raw)) return "";
            int tab = raw.IndexOf('\t');
            if (tab < 0) return "";
            if (Day - Errands.I(raw.Substring(0, tab), 0) > HeardKeepDays) { item.m_customData.Remove(HeardKey); return ""; }
            return raw.Substring(tab + 1);
        }

        // keep: into its words (when there is room); else thrown out. null: done, else why not
        internal static string KeepHeard(ItemDrop.ItemData item, bool keep)
        {
            string w = Heard(item);
            if (w.Length == 0) return null;
            if (keep)
            {
                string why = Teach(item, w);
                if (why != null) return why;
            }
            item.m_customData.Remove(HeardKey);
            return null;
        }

        // ---------------------------------------------------------------- the chat

        // a message of a living player came (Chat.OnNewChatMessage: what players send; the ravens' own lines are only
        // put into the chat on each game, they never come here)
        internal static void OnChat(long sender, Vector3 pos, Talker.Type type, string text)
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || EnvMan.instance == null || string.IsNullOrEmpty(text)) return;
            if (type == Talker.Type.Ping || text.StartsWith("$", StringComparison.Ordinal) || text.StartsWith("/", StringComparison.Ordinal)) return; // (pings, commands)
            if (Arrival(text)) return; // (the game's own «I have arrived!» shout on every spawn)
            if (type != Talker.Type.Shout && Vector3.Distance(p.transform.position, pos) > HearRange) return;
            string raw;
            int next = p.m_customData.TryGetValue(NextHearKey, out raw) ? Errands.I(raw, 0) : 0;
            if (Day < next || UnityEngine.Random.value >= HearChance) return;
            List<bool> who = new List<bool>();
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData it = Mine(p, m);
                if (it != null && Open(it) && Shoulder.Present(p, m) && Heard(it).Length == 0) who.Add(m);
            }
            if (who.Count == 0) return;
            string word = Catch(text);
            if (word.Length == 0) return;
            bool munin = who[UnityEngine.Random.Range(0, who.Count)];
            ItemDrop.ItemData item = Mine(p, munin);
            if (Of(item).Exists(x => Same(x.Text, word))) return;
            item.m_customData[HeardKey] = Day.ToString(CultureInfo.InvariantCulture) + "\t" + word;
            p.m_customData[NextHearKey] = (Day + HearEveryDays).ToString(CultureInfo.InvariantCulture);
            Say(p, munin, Fill(Pick(munin ? MuninHeard : HuginHeard), Mangle(word, 1, munin)), false);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} підхопив слово «{1}». Лишити чи викинути — у книзі воронів, ᚨ біля імені.", Plugin.RavenName(munin), word));
            Log(Plugin.RavenName(munin) + " caught a word from the chat: " + word);
            RavenBook.Refresh();
        }

        private static bool Arrival(string text)
        {
            string t = text.Trim();
            if (string.Equals(t, "I have arrived!", StringComparison.OrdinalIgnoreCase)) return true;
            string here = Localization.instance != null ? Localization.instance.Localize("$text_player_arrived") : null;
            return !string.IsNullOrEmpty(here) && string.Equals(t, here.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // a word or two of a message: a longer word more likely; now and then two words together
        private static string Catch(string text)
        {
            string[] parts = Clean(text).Split(' ');
            List<int> good = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                int letters = 0;
                foreach (char c in parts[i]) if (char.IsLetter(c)) letters++;
                if (letters >= 3) { good.Add(i); if (parts[i].Length >= 5) good.Add(i); } // (a longer word: twice the chance)
            }
            if (good.Count == 0) return "";
            int at = good[UnityEngine.Random.Range(0, good.Count)];
            string w = parts[at];
            if (UnityEngine.Random.value < 0.3f && at + 1 < parts.Length && (w + " " + parts[at + 1]).Length <= MaxChars) w += " " + parts[at + 1];
            return Clean(w);
        }

        // ---------------------------------------------------------------- saying them

        // your own raven of that side, awake (a stranger's raven says its owner's words, not yours to change)
        private static ItemDrop.ItemData Mine(Player p, bool munin) { ItemDrop.ItemData it = Trust.Worn(munin); return it != null && p != null && Trust.Active(it) && !Trust.Stranger(it, p) ? it : null; }

        private static int Day { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }

        private static readonly float[] _lastWord = { -9999f, -9999f }, _nextAny = { -1f, -1f };
        private static bool _saying;

        // a word for this moment, if one is due (it is then counted as said)
        private static Word Due(ItemDrop.ItemData item, int mi, Moment m)
        {
            if (Time.time - _lastWord[mi] < MinGap) return null;
            List<Word> l = Of(item);
            int slots = Slots(item);
            List<Word> can = new List<Word>();
            for (int i = 0; i < l.Count && i < slots; i++) // (past the slots: asleep)
                if (l[i].When == m && Time.time - l[i].SaidAt >= WordGap) can.Add(l[i]);
            return can.Count == 0 ? null : can[UnityEngine.Random.Range(0, can.Count)];
        }

        private static void Said(ItemDrop.ItemData item, int mi, Word w)
        {
            w.SaidAt = _lastWord[mi] = Time.time;
            List<Word> l = Of(item);
            Word same = l.Find(x => x.Text == w.Text);
            if (same == null) return;
            same.Uses++;
            same.SaidAt = w.SaidAt;
            Save(item, l);
            if (same.Uses == CleanAt) Log(same.Text + ": said clean now");
        }

        // the moment came: maybe a word of it
        internal static void At(Player p, bool munin, Moment m)
        {
            if (p == null || p.IsDead()) return;
            ItemDrop.ItemData item = Mine(p, munin);
            if (item == null || !Open(item) || Trust.Away(item) || !Shoulder.Present(p, munin)) return;
            if (UnityEngine.Random.value >= MomentChance || !Director.May(Director.Voice.Event) || RavenSkills.Hushed(p)) return;
            int mi = munin ? 1 : 0;
            Word w = Due(item, mi, m);
            if (w == null) return;
            string line = Fill(Pick(Lines(munin, m)), Mangle(w.Text, Stage(w), munin));
            Said(item, mi, w);
            Say(p, munin, line, true);
        }

        // both ravens: the moment for whoever has a word for it (one of them)
        private static void AtBoth(Player p, Moment m)
        {
            bool first = UnityEngine.Random.value < 0.5f;
            float was = _lastWord[0] + _lastWord[1];
            At(p, first, m);
            if (_lastWord[0] + _lastWord[1] == was) At(p, !first, m);
        }

        // RavenSkills.Speak: one of its own lines, now and then with a «коли захоче» word in it
        internal static string Season(Player p, bool munin, string line)
        {
            if (_saying || p == null || string.IsNullOrEmpty(line) || UnityEngine.Random.value >= SeasonChance) return line;
            ItemDrop.ItemData item = Mine(p, munin);
            if (item == null || !Open(item)) return line;
            int mi = munin ? 1 : 0;
            Word w = Due(item, mi, Moment.Any);
            if (w == null) return line;
            string said = Mangle(w.Text, Stage(w), munin);
            Said(item, mi, w);
            return UnityEngine.Random.value < 0.5f ? Up(said) + "! " + line : line.TrimEnd() + " " + Up(said) + (munin ? "!" : ".");
        }

        private static void Say(Player p, bool munin, string line, bool director)
        {
            _saying = true;
            try { RavenSkills.SayFromShoulder(p, munin, line); }
            finally { _saying = false; }
            if (director) Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }

        // after a word is taught: it tries it at once (mangled), and has its say
        internal static void Tried(Player p, bool munin, string word)
        {
            if (p == null || !Shoulder.Present(p, munin)) return;
            Say(p, munin, Fill(Pick(munin ? MuninTried : HuginTried), Mangle(word, 1, munin), word), false);
        }

        internal static void Forgot(Player p, bool munin)
        {
            if (p != null && Shoulder.Present(p, munin)) Say(p, munin, Pick(munin ? MuninForget : HuginForget), false);
        }

        // ---------------------------------------------------------------- the clock (Errands.Tick, twice a second)

        private static readonly bool[] _was = { false, false };
        private static readonly float[] _goneAt = { -9999f, -9999f };
        private static readonly float[] _greetAt = { -1f, -1f };
        private static bool _fight, _dead, _deathOwed;
        private static float _slow;
        private static Player _player;

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || EnvMan.instance == null) return;
            if (p.IsDead()) { _dead = true; return; }
            if (p != _player) // another character, a new login, or risen after death: the ravens met anew
            {
                _player = p;
                Reset();
                if (_dead) { _goneAt[0] = _goneAt[1] = Time.time; _fight = false; } // (risen: the death's word, not a greeting)
            }
            if (_dead) { _dead = false; _deathOwed = true; _deathAt = Time.time + UnityEngine.Random.Range(6f, 12f); }
            if (Time.time < _slow) return;
            _slow = Time.time + 1f;
            for (int mi = 0; mi < 2; mi++)
            {
                bool munin = mi == 1;
                ItemDrop.ItemData item = Mine(p, munin);
                bool here = item != null && !Trust.Away(item) && Shoulder.Arrived(p, munin);
                if (here && !_was[mi] && Time.time - _goneAt[mi] >= 60f) _greetAt[mi] = Time.time + UnityEngine.Random.Range(3f, 6f); // (back after a while, or the first time this game)
                if (!here && _was[mi]) _goneAt[mi] = Time.time;
                _was[mi] = here;
                if (!here || item == null || !Open(item)) continue;
                if (_greetAt[mi] > 0f && Time.time >= _greetAt[mi]) { _greetAt[mi] = -1f; At(p, munin, Moment.Greet); }
                // «коли захоче»: by itself, about every AnyEvery, in a calm moment
                if (_nextAny[mi] < 0f) _nextAny[mi] = Time.time + AnyEvery * UnityEngine.Random.Range(0.5f, 1.2f);
                if (Time.time >= _nextAny[mi] && !Senses.Fight && !p.InInterior() && !p.IsSleeping() && Director.May(Director.Voice.Shoulder) && !RavenSkills.Hushed(p))
                {
                    _nextAny[mi] = Time.time + AnyEvery * UnityEngine.Random.Range(0.8f, 1.3f);
                    Word w = Due(item, mi, Moment.Any);
                    if (w != null)
                    {
                        string line = Fill(Pick(Lines(munin, Moment.Any)), Mangle(w.Text, Stage(w), munin));
                        Said(item, mi, w);
                        _saying = true;
                        try { RavenSkills.SayFromShoulder(p, munin, line); } finally { _saying = false; }
                        Director.Spoke(Director.Voice.Shoulder, RavenSpeech.ReadSeconds(line));
                    }
                }
            }
            bool fight = Senses.Fight;
            if (fight && !_fight) AtBoth(p, Moment.Fight);
            _fight = fight;
            if (_deathOwed && Time.time >= _deathAt)
            {
                if (Shoulder.Arrived(p, false) || Shoulder.Arrived(p, true)) { _deathOwed = false; AtBoth(p, Moment.Death); }
                else if (Time.time - _deathAt > 120f) _deathOwed = false; // (no raven on you: it lies in the grave)
            }
        }
        private static float _deathAt;

        // the diary: you ate, sat warm by a fire
        internal static void OnEvent(Ev type)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (type == Ev.Eat) AtBoth(p, Moment.Food);
            else if (type == Ev.Cozy) AtBoth(p, Moment.Fire);
        }

        internal static void OnFed(Player p, bool munin) { At(p, munin, Moment.Food); }

        internal static void OnKill(int stars, bool boss)
        {
            if (boss || stars >= 1) AtBoth(Player.m_localPlayer, Moment.Win);
        }

        internal static void Reset() { _was[0] = _was[1] = false; _greetAt[0] = _greetAt[1] = -1f; _nextAny[0] = _nextAny[1] = -1f; _goneAt[0] = _goneAt[1] = -9999f; }

        // ---------------------------------------------------------------- the mangling

        // the word as it says it now: 1 heavily mangled, 2 lightly, 3 clean (Munin: now and then lightly on purpose)
        internal static string Mangle(string word, int stage, bool munin)
        {
            if (string.IsNullOrEmpty(word)) return word;
            if (stage >= 3 && !(munin && UnityEngine.Random.value < 0.2f)) return word;
            if (stage >= 3) stage = 2;
            // the longest word of a phrase takes it
            string[] parts = word.Split(' ');
            int at = 0;
            for (int i = 1; i < parts.Length; i++) if (parts[i].Length > parts[at].Length) at = i;
            string w = parts[at];
            if (w.Length < 3) return word;
            float r = UnityEngine.Random.value;
            int mid = w.Length / 2;
            if (stage == 1)
            {
                if (r < 0.35f) w = w.Substring(0, mid) + "-" + Lang.T("кррр") + "-" + w.Substring(mid);
                else if (r < 0.65f) w = w.Substring(0, Mathf.Max(2, Mathf.CeilToInt(w.Length * 0.6f))) + "…";
                else { string s = w.Substring(0, 2); w = s + "-" + s.ToLowerInvariant() + "-" + char.ToLowerInvariant(w[0]) + w.Substring(1); }
            }
            else
            {
                int v = Vowel(w, mid);
                if (r < 0.5f && v >= 0) w = w.Substring(0, v + 1) + new string(w[v], 2) + w.Substring(v + 1);
                else if (r < 0.75f) { string s = w.Substring(0, 1); w = s + "-" + w; }
                else w = w + "… " + Lang.T("кра");
            }
            parts[at] = w;
            return string.Join(" ", parts);
        }

        private static int Vowel(string w, int near)
        {
            const string vowels = "аеєиіїоуюяaeiouy";
            for (int d = 0; d < w.Length; d++)
            {
                int a = near + d, b = near - d;
                if (a < w.Length && vowels.IndexOf(char.ToLowerInvariant(w[a])) >= 0) return a;
                if (b >= 0 && vowels.IndexOf(char.ToLowerInvariant(w[b])) >= 0) return b;
            }
            return -1;
        }

        private static string Up(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1); }

        // ---------------------------------------------------------------- the lines ({0}: the word)

        private static string Pick(string[] l) { return l[UnityEngine.Random.Range(0, l.Length)]; }

        // the word into a line ({0}, {1}): not string.Format — the lines carry the forms by gender in braces ({в|ла}),
        // which Format takes for blanks and throws on (2026-10-29 log: Words.Tick FormatException)
        private static string Fill(string line, params string[] a)
        {
            for (int i = 0; i < a.Length; i++) line = line.Replace("{" + i + "}", a[i] ?? "");
            return line;
        }

        private static string[] Lines(bool munin, Moment m)
        {
            switch (m)
            {
                case Moment.Greet: return munin ? MuninGreet : HuginGreet;
                case Moment.Fight: return munin ? MuninFight : HuginFight;
                case Moment.Death: return munin ? MuninDeath : HuginDeath;
                case Moment.Food: return munin ? MuninFood : HuginFood;
                case Moment.Win: return munin ? MuninWin : HuginWin;
                case Moment.Fire: return munin ? MuninFire : HuginFire;
            }
            return munin ? MuninAny : HuginAny;
        }

        private static string[] HuginAny { get { return new[] { Lang.T("Кра… {0}."), Lang.T("{0}. Так, я це пам'ятаю."), Lang.T("До речі: {0}."), Lang.T("Хм. {0}."), Lang.T("Ти колись каза{в|ла}: «{0}». Я записав."), Lang.T("{0}… Гарне слово.") }; } }
        private static string[] MuninAny { get { return new[] { Lang.T("{0}! Кар!"), Lang.T("КАР! {0}!"), Lang.T("Слухай: {0}. Ось."), Lang.T("{0}, бля."), Lang.T("Ги. {0}."), Lang.T("{0}! Ха!") }; } }
        private static string[] HuginGreet { get { return new[] { Lang.T("О, ти тут. {0}!"), Lang.T("{0}! Тобто, привіт."), Lang.T("{0}. Я скучив, якщо що.") }; } }
        private static string[] MuninGreet { get { return new[] { Lang.T("{0}! Явил{ся|ася}."), Lang.T("О, знову ти. {0}."), Lang.T("{0}! Ну, здоров.") }; } }
        private static string[] HuginFight { get { return new[] { Lang.T("{0}! До бою!"), Lang.T("Увага! {0}!"), Lang.T("{0}! Тримайся!") }; } }
        private static string[] MuninFight { get { return new[] { Lang.T("{0}! Мочи!"), Lang.T("КАР! {0}!"), Lang.T("{0}! Рви його!") }; } }
        private static string[] HuginDeath { get { return new[] { Lang.T("{0}… Ну, буває."), Lang.T("Знову? {0}."), Lang.T("{0}. Я нічого не бачив.") }; } }
        private static string[] MuninDeath { get { return new[] { Lang.T("{0}! Ха-ха!"), Lang.T("{Здох|Здохла}. {0}."), Lang.T("{0}! Красиво лі{г|гла}.") }; } }
        private static string[] HuginFood { get { return new[] { Lang.T("{0}. Смачно."), Lang.T("Ням. {0}."), Lang.T("{0}… і крихту мені.") }; } }
        private static string[] MuninFood { get { return new[] { Lang.T("Ням! {0}!"), Lang.T("{0}! Жери швидше."), Lang.T("{0}. Ще давай.") }; } }
        private static string[] HuginWin { get { return new[] { Lang.T("{0}! Перемога!"), Lang.T("{0}! Я так і знав."), Lang.T("Готово. {0}!") }; } }
        private static string[] MuninWin { get { return new[] { Lang.T("{0}! Так йому!"), Lang.T("КАР! {0}! Лежить!"), Lang.T("{0}! Наступного!") }; } }
        private static string[] HuginFire { get { return new[] { Lang.T("Тепло… {0}."), Lang.T("{0}. Добре сидимо."), Lang.T("Під вогонь пасує: {0}.") }; } }
        private static string[] MuninFire { get { return new[] { Lang.T("{0}. І пива б."), Lang.T("Гріємось. {0}."), Lang.T("{0}… Кайф.") }; } }

        // caught from the chat ({0}: as it says it now)
        private static string[] HuginHeard { get { return new[] { Lang.T("{0}?"), Lang.T("Що-що? {0}? Цікаво."), Lang.T("{0}… Треба запам'ятати.") }; } }
        private static string[] MuninHeard { get { return new[] { Lang.T("Ги! {0}!"), Lang.T("{0}! Кар! Моє тепер."), Lang.T("Хто сказав «{0}»? Беру.") }; } }
        // taught ({0}: mangled, {1}: the word)
        private static string[] HuginTried { get { return new[] { Lang.T("«{1}»… {0}. Здається, вийшло."), Lang.T("{0}. Ні, стривай. {0}! Навчусь."), Lang.T("Записав: «{1}». Ну, майже: {0}.") }; } }
        private static string[] MuninTried { get { return new[] { Lang.T("{0}! Тьху, язик зламаєш. Вивчу."), Lang.T("«{1}»? Нормально. {0}!"), Lang.T("{0}… Бля, ще раз. {0}!") }; } }
        private static string[] HuginForget { get { return new[] { Lang.T("Добре, забуду. Що там було…"), Lang.T("Викреслив. Не дуже й хотілось."), Lang.T("Як скажеш. Вже не пам'ятаю.") }; } }
        private static string[] MuninForget { get { return new[] { Lang.T("Та й хер з ним."), Lang.T("Забув. А шо було?"), Lang.T("Ну й ладно. Тупе було слово.") }; } }

        // ---------------------------------------------------------------- the console: raven words [h|m] [teach <word>|clear|clean|hear <text>]

        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            int at = a.Length > 1 && (a[1] == "m" || a[1] == "h" || a[1] == "munin" || a[1] == "hugin") ? 2 : 1;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return "raven words: no " + Plugin.RavenName(munin) + " on the shoulder";
            string what = a.Length > at ? a[at] : "";
            string rest = a.Length > at + 1 ? string.Join(" ", a, at + 1, a.Length - at - 1) : "";
            if (what == "teach") { string why = Teach(item, rest); if (why == null) Tried(p, munin, Clean(rest)); RavenBook.Refresh(); return why ?? "taught: " + Clean(rest); }
            if (what == "clear") { item.m_customData.Remove(Key); item.m_customData.Remove(HeardKey); RavenBook.Refresh(); return "words cleared"; }
            if (what == "sparks") { int n = Errands.I(rest, 5); Requests.SpendSparks(item, -n); RavenBook.Refresh(); return "sparklets +" + n; }
            if (what == "clean") { List<Word> l = Of(item); foreach (Word w in l) w.Uses = Mathf.Max(w.Uses, CleanAt); Save(item, l); RavenBook.Refresh(); return "all said clean"; }
            if (what == "hear") { p.m_customData.Remove(NextHearKey); float was = HearChance; HearChance = 1f; try { OnChat(0L, p.transform.position, Talker.Type.Normal, rest); } finally { HearChance = was; } return "heard: " + Heard(item); }
            if (what == "say") { _lastWord[munin ? 1 : 0] = -9999f; foreach (Word w in Of(item)) w.SaidAt = -9999f; _nextAny[munin ? 1 : 0] = 0f; return "next «any» word soon"; }
            StringBuilder sb = new StringBuilder(Plugin.RavenName(munin) + ": " + Of(item).Count + "/" + Slots(item));
            foreach (Word w in Of(item)) sb.Append("\n  " + w.Text + " · " + MomentName(w.When) + " · uses " + w.Uses);
            string h = Heard(item);
            if (h.Length > 0) sb.Append("\n  heard: " + h);
            return sb.ToString();
        }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Words: " + s); }
    }

    // what living players write in the chat (their own included); the ravens' lines never pass here
    [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
    internal static class WordsChatPatch
    {
        private static void Postfix(long senderID, Vector3 pos, Talker.Type type, string text)
        {
            try { Words.OnChat(senderID, pos, type, text); } catch (Exception e) { Words.Log("chat: " + e.Message); }
        }
    }
}
#endif

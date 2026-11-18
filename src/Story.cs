#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The story's memory (story-plan.md): what this character's own story has already seen and chosen. Each player has
    // their own, kept in the character (player customData RavenWatch_story, with its format number; unknown fields
    // kept), never in the world. Flags (a thing happened), numbers (how much of something), each quest's state, and the
    // saga's own entries in the order they were lived (StorySaga: «quest.stage» or «quest.stage.way», only those with a «saga:»),
    // and short texts by a name (Text: what of the book's «Сага» was read; no «;» in one).
    //   v=1;f=ch1_asked+met_odin;n.odin_favour=2;q.ch1_shepit=done;g=prologue.call+prologue.roads.munin;t.saga_read=heid:1f3a2b
    internal sealed class StoryState
    {
        internal const int Format = 1;
        public readonly List<string> Flags = new List<string>(); // (in the order set: kept so; changed only through Has/Set below)
        private readonly HashSet<string> _flagSet = new HashSet<string>(); // (the same, to ask: every condition asks twice a second — 85 µs against 6 for 78 «asked» at 500 flags, .NET 8, 2026-11-16)
        internal bool Has(string f) { return _flagSet.Contains(f); }
        internal bool Set(string f, bool on) { if (on ? !_flagSet.Add(f) : !_flagSet.Remove(f)) return false; if (on) Flags.Add(f); else Flags.Remove(f); return true; }
        public readonly Dictionary<string, int> Vars = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Quests = new Dictionary<string, string>(); // id → active | done | failed
        public readonly Dictionary<string, string> Stages = new Dictionary<string, string>(); // id → the stage it stands at (StoryQuests)
        public readonly List<string> Saga = new List<string>(); // the saga's entries lived, in order (StorySaga)
        public readonly Dictionary<string, string> Texts = new Dictionary<string, string>(); // a short text by a name (Story.Text)
        private readonly List<KeyValuePair<string, string>> _other = new List<KeyValuePair<string, string>>();

        internal string Code()
        {
            StringBuilder sb = new StringBuilder("v=").Append(Format);
            if (Flags.Count > 0) sb.Append(";f=").Append(string.Join("+", Flags.ToArray()));
            foreach (KeyValuePair<string, int> kv in Vars) sb.Append(";n.").Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, string> kv in Quests) sb.Append(";q.").Append(kv.Key).Append('=').Append(kv.Value);
            foreach (KeyValuePair<string, string> kv in Stages) sb.Append(";s.").Append(kv.Key).Append('=').Append(kv.Value);
            if (Saga.Count > 0) sb.Append(";g=").Append(string.Join("+", Saga.ToArray()));
            foreach (KeyValuePair<string, string> kv in Texts) sb.Append(";t.").Append(kv.Key).Append('=').Append(kv.Value);
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
                if (k == "f") { foreach (string f in v.Split('+')) if (f.Length > 0) s.Set(f, true); }
                else if (k == "g") { foreach (string g in v.Split('+')) if (g.Length > 0 && !s.Saga.Contains(g)) s.Saga.Add(g); }
                else if (k.StartsWith("n.") && k.Length > 2) s.Vars[k.Substring(2)] = Errands.I(v, 0);
                else if (k.StartsWith("q.") && k.Length > 2) s.Quests[k.Substring(2)] = v;
                else if (k.StartsWith("s.") && k.Length > 2) s.Stages[k.Substring(2)] = v;
                else if (k.StartsWith("t.") && k.Length > 2) s.Texts[k.Substring(2)] = v;
                else s._other.Add(new KeyValuePair<string, string>(k, v));
            }
            return s;
        }
    }

    internal static class Story
    {
        internal const string Key = "RavenWatch_story";
        internal static readonly char[] Bad = { ';', '=', ',', '+', '|', ' ', '\n', '\t' };

        private static string _raw;
        private static StoryState _state;

        internal static bool GoodName(string n) { return !string.IsNullOrEmpty(n) && n.IndexOfAny(Bad) < 0; }

        // this world's number as the end of a character's key of this world only («heard.…», «mapkeep_…», «been.…»): made once a world, not on every ask
        private static string _worldId = "0";
        private static long _worldUid;
        internal static string WorldId
        {
            get
            {
                long uid = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L;
                if (uid != _worldUid) { _worldUid = uid; _worldId = uid.ToString(CultureInfo.InvariantCulture); }
                return _worldId;
            }
        }

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
        }

        internal static void Reset() { _raw = null; _state = null; }

        // ---------------------------------------------------------------- flags
        internal static bool Flag(Player p, string f) { return p != null && Of(p).Has(f); }

        internal static void SetFlag(Player p, string f, bool on)
        {
            if (p == null || !GoodName(f)) return;
            StoryState s = Of(p);
            if (!s.Set(f, on)) return;
            Save(p, s);
            Log((on ? "flag " : "unflag ") + f);
        }

        // ---------------------------------------------------------------- numbers
        internal static int Var(Player p, string n) { int v; return p != null && Of(p).Vars.TryGetValue(n, out v) ? v : 0; }

        internal static void SetVar(Player p, string n, int v, bool quiet = false)
        {
            if (p == null || !GoodName(n)) return;
            StoryState s = Of(p);
            int old;
            if (s.Vars.TryGetValue(n, out old) && old == v) return;
            if (v == 0) s.Vars.Remove(n); else s.Vars[n] = v; // (0 is the same as never set)
            Save(p, s);
            if (!quiet) Log("var " + n + " = " + v);
        }

        internal static void AddVar(Player p, string n, int d) { SetVar(p, n, Var(p, n) + d); }

        // ---------------------------------------------------------------- short texts
        internal static string Text(Player p, string n) { string v; return p != null && Of(p).Texts.TryGetValue(n, out v) ? v : ""; }

        // «» is the same as never set; a text with «;» would cut the character's story in two: refused, said in the log
        internal static void SetText(Player p, string n, string v)
        {
            if (p == null || !GoodName(n)) return;
            v = v ?? "";
            if (v.IndexOf(';') >= 0) { Log("text " + n + ": «;» in it — not kept"); return; }
            StoryState s = Of(p);
            string old;
            if ((s.Texts.TryGetValue(n, out old) ? old : "") == v) return;
            if (v.Length == 0) s.Texts.Remove(n); else s.Texts[n] = v;
            Save(p, s);
        }

        // ---------------------------------------------------------------- quests
        internal const string Active = "active", Done = "done", Failed = "failed";

        // "" never started
        internal static string Quest(Player p, string id) { string v; return p != null && Of(p).Quests.TryGetValue(id, out v) ? v : ""; }

        internal static void SetQuest(Player p, string id, string state)
        {
            if (p == null || !GoodName(id)) return;
#if RAVEN_STORY
            { StoryQuests.QuestDef sq = StoryQuests.Find(id); if (sq != null && sq.Server && !string.IsNullOrEmpty(state)) { Log("quest " + id + " is the server's: not kept in a character (a talk's «!quest», the console) — nothing done"); return; } } // (one place for every writer: the world keeps it)
#endif
            StoryState s = Of(p);
            if (string.IsNullOrEmpty(state)) { if (!s.Quests.Remove(id)) return; }
            else { string old; if (s.Quests.TryGetValue(id, out old) && old == state) return; s.Quests[id] = state; }
            Save(p, s);
            Log("quest " + id + " → " + (string.IsNullOrEmpty(state) ? "(forgotten)" : state));
        }

        // the stage a quest stands at ("" none)
        internal static string StageOf(Player p, string id) { string v; return p != null && Of(p).Stages.TryGetValue(id, out v) ? v : ""; }

        internal static void SetStage(Player p, string id, string stage)
        {
            if (p == null || !GoodName(id)) return;
            StoryState s = Of(p);
            if (string.IsNullOrEmpty(stage)) { if (!s.Stages.Remove(id)) return; }
            else { string old; if (s.Stages.TryGetValue(id, out old) && old == stage) return; s.Stages[id] = stage; }
            Save(p, s);
            Log("stage " + id + " → " + (string.IsNullOrEmpty(stage) ? "(none)" : stage));
        }

        // ---------------------------------------------------------------- the saga's entries (StorySaga)
        // lived now: kept once, at the end (a stage lived again after a jump keeps its first place)
        internal static void AddSaga(Player p, string entry)
        {
            if (p == null || !GoodName(entry)) return;
            StoryState s = Of(p);
            if (s.Saga.Contains(entry)) return;
            s.Saga.Add(entry);
            Save(p, s);
            Log("saga + " + entry);
        }

        // a quest forgotten (the author): its entries go with it
        internal static void ForgetSaga(Player p, string quest)
        {
            if (p == null) return;
            StoryState s = Of(p);
            if (s.Saga.RemoveAll(e => e.StartsWith(quest + ".", StringComparison.Ordinal)) > 0) Save(p, s);
        }

        // ---------------------------------------------------------------- small things
        // a file name the author's tools may write (letters, digits, _ - . and .txt at the end; no folders)
        internal static bool GoodFile(string f)
        {
            if (string.IsNullOrEmpty(f) || !f.EndsWith(".txt") || f.Length > 60) return false;
            foreach (char c in f) if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return false;
            return f.IndexOf("..") < 0;
        }

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
            sb.Append("\n  сага: ").Append(s.Saga.Count > 0 ? string.Join(", ", s.Saga.ToArray()) : "—");
            return sb.ToString();
        }

        // the author's test: there at once, on the ground (or its floor, if higher) — a place's own spot, never one of its pieces
        private const float TpBefore = 9f; // m before the door of a place with an inside: clear of the mound's own passage
        internal static bool Tp(Player p, Vector3 at) { return Tp(p, at, p.transform.rotation); }
        // false: the game refused it (a portal still going, its pause after one): said in the log, never silent
        internal static bool Tp(Player p, Vector3 at, Quaternion turn)
        {
            float y = ZoneSystem.instance != null ? Mathf.Max(at.y, ZoneSystem.instance.GetGroundHeight(at)) : at.y;
            bool went = p.TeleportTo(new Vector3(at.x, y + 1f, at.z), turn, true);
            if (!went) Log("teleport refused by the game (a portal still going?): " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z));
            return went;
        }

#if RAVEN_STORY
        // the author's test: a person of the story put at a place at once (beside it, not in it: a person put into the hearth burns) — the studio's
        // «npc <who> at <place>» (StudioScene) and the console «raven story put <who> <place>». Only a person loaded here (its body near the player):
        // its owner moves it, and the place may be far (Ketil from the heart to his glade, 2026-11-14). Null: done, else why not
        internal static string PutPerson(Player p, string who, string place, out NpcActor a, out Vector3 at)
        {
            a = Npcs.Find(who); at = Vector3.zero;
            if (a == null) return "нема «" + who + "» поруч (його тіло мусить бути завантажене: стань біля нього)";
            if (!StoryWhere.Approach(place, p.transform.position, out at)) return "нема місця «" + place + "»";
            a.Order("tp " + Npcs.V(at), NpcMind.Layer.Scene); a.Order("free", NpcMind.Layer.Scene);
            return null;
        }
#endif

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven story · flag x · unflag x · var x N · quest x active|done|failed|- · check <conditions> · reset · put <who> <place>
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
#if RAVEN_STORY
            if ((what == "ui") && a.Length > 2 && a[2].ToLowerInvariant() == "look") { if (!Blueprint.On) return "Вікно сюжету спить: немає теки " + Blueprint.Root; int sl; return StoryPanel.OpenLook(a.Length > 3 ? a[3] : "", a.Length > 4 && int.TryParse(a[4], out sl) ? sl : -1); } // (raven story ui look <id> [slot]: the look tab, for the studio)
            if (what.Length == 0 || what == "ui") { if (!Blueprint.On) return "Вікно сюжету спить: немає теки " + Blueprint.Root; StoryPanel.Toggle(); return StoryPanel.Said(); }
#else
            if (what.Length == 0 || what == "ui") return Describe(p); // (the story's window: from 3.0)
#endif
#if RAVEN_STORY
            if (what == "saga") return StorySaga.Describe(p); // (the book's «Сага» as words: what it would show now)
            if (what == "people") return StoryPeople.Describe(p, a.Length > 2 ? a[2] : ""); // (where the story's people are, and why)
            if (what == "falcon") return Falcon.Cmd(p, a); // (the author's look at a falcon of the sisters: Falcon)
            if (what == "put")
            {
                if (a.Length < 4) return "raven story put <хто> <місце>   (людину сюжету — туди одразу; вона мусить бути поруч)";
                NpcActor who; Vector3 to;
                string why = PutPerson(p, a[2], a[3], out who, out to);
                return why ?? "«" + a[2] + "» тепер біля «" + a[3] + "» (" + Mathf.RoundToInt(to.x) + " " + Mathf.RoundToInt(to.z) + ")";
            }
            if (what == "rooms") return LocRooms.Cmd(p, a); // (the author's look at a dungeon of the game: its rooms and the things in them, into a file)
            if (StoryPeople.All.Exists(x => x.Id == what)) return StoryPeople.Describe(p, what); // (`raven story <person>`: one of them)
#endif
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
#if RAVEN_STORY
                    { StoryQuests.QuestDef sq = StoryQuests.Find(name); if (sq != null && sq.Server) return "«" + name + "» — квест сервера, у персонажі його нема: raven story jump|forget " + name; }
#endif
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
#if RAVEN_STORY
                case "quests": StoryQuests.Reload(); return StoryPanel.TrackText(p);
                case "start":
                case "jump":
                case "forget":
                    {
                        StoryQuests.QuestDef q = StoryQuests.Find(name);
                        if (q == null) return "raven story " + what + " <квест>" + (what == "jump" ? " <етап>" : "") + " — нема квесту «" + name + "»";
                        if (what == "start") StoryQuests.Rebuild(p, q, q.First);
                        else if (what == "forget") StoryQuests.Rebuild(p, q, "");
                        else { if (a.Length < 4) return "raven story jump <квест> <етап>"; StoryQuests.Jump(p, q, a[3]); }
                        return StoryPanel.TrackText(p);
                    }
                case "bird":
                    {
                        if (name.Length > 0) return StoryScenes.TryBird(p, name); // (the author's: a bird of a look called here, its orders — no scene)
                        if (StoryBird.Live.Count == 0) return "Сюжетних воронів нема.";
                        StringBuilder sb = new StringBuilder("Сюжетні ворони (" + StoryBird.Live.Count + "):");
                        foreach (StoryBird b in StoryBird.Live) if (b != null) sb.Append("\n  ").Append(b.Describe());
                        return sb.ToString();
                    }
                case "scene":
                    {
                        if (name == "stop") { StoryScenes.Stop("автор"); return "Сцену зупинено."; }
                        if (name == "reload") { StoryFiles.ReloadAll(); return "Перечитано: сцен " + StoryScenes.Scenes.Count + ", проблем " + StoryScenes.Errors.Count + "; квестів " + StoryQuests.Quests.Count + ", проблем " + StoryQuests.Errors.Count + "."; } // (the author: files changed outside the game)
                        StoryScenes.SceneDef sc = StoryScenes.Find(name);
                        if (sc == null) return "raven story scene <id>|stop|reload — нема сцени «" + name + "»";
                        StoryScenes.Play(sc);
                        return "Сцена «" + name + "» пішла.";
                    }
                case "world":
                    if (name == "clear") return "Світових ключів сюжету стерто: " + StoryWorld.Clear();
                    if (name.Length > 0) StoryWorld.SetFlag(name.TrimStart('!'), !name.StartsWith("!"));
                    return "Світ: " + string.Join(", ", StoryWorld.All().ToArray());
                case "title": return StorySaga.ShowTitle(name);
                case "played":
                    StoryTrackConds.AddMinutes(p, Errands.I(name, 10));
                    return "Зіграно хвилин: " + StoryTrackConds.Played(p) / 60;
                case "where":
                case "tp":
                    {
                        Vector3 at;
                        if (!StoryWhere.Find(name, out at)) return "raven story " + what + " <місце> — нема місця «" + name + "» (hall, hall.<назва>, torvald, prologue_house, prologue_munin, мітка автора…)";
                        string xyz = Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.y) + " " + Mathf.RoundToInt(at.z);
                        if (what == "where") return name + ": " + xyz + ", " + Mathf.RoundToInt(Utils.DistanceXZ(at, p.transform.position)) + " м від тебе";
                        // (a place whose way in is its own middle — a barrow over an inside of ours: the game's mound has its door at its heart, and the
                        //  author carried there stood inside the mound, 2026-11-15: before its front instead, looking at it)
                        Loc inPlace = what == "tp" ? Locs.Get(name) : null; Vector3 mid; Quaternion front;
                        if (inPlace != null && inPlace.Inside.Length > 0 && inPlace.Anchor(out mid, out front)) { Tp(p, mid + front * new Vector3(0f, 0f, TpBefore), front * Quaternion.Euler(0f, 180f, 0f)); return "До «" + name + "» (перед вхід): " + xyz; }
                        Tp(p, at);
                        return "До «" + name + "»: " + xyz;
                    }
                case "mark":
                    if (!GoodName(name)) return "raven story mark <назва>   (мітка автора тут, де стоїш: як F9 → «Поставити мітку тут»)";
                    return StoryPlaces.Set(name, p.transform.position, p.transform.eulerAngles.y);
                case "do":
                    if (a.Length < 3) return "raven story do <дія квесту>, напр.: book, flag x, gate hall open";
                    {
                        string act = string.Join(" ", a, 2, a.Length - 2), why = StoryActions.Check(act, true);
                        if (why != null) return why;
                        StoryActions.Do(p, null, act);
                        return "Дія: " + act;
                    }
                case "look": return StoryLook.Describe(p);
                case "touch":
                    if (a.Length < 3 || a[2].IndexOf('.') < 0) return "raven story touch <місце.назва>   (як E на сюжетній речі)";
                    {
                        Vector3 at;
                        if (!StoryWhere.Find(a[2], out at)) at = p.transform.position;
                        LocTouch.Touch(a[2], at);
                    }
                    return "Торкнувся «" + a[2] + "».";
                case "npcravens": return NpcRaven.Describe() + LocRaven.Describe();
                case "placeraven": LocRaven.Trace = !LocRaven.Trace; return "Ворон місця (Зали): його слова й польоти в лог — " + (LocRaven.Trace ? "так" : "ні");
                case "raventrace": NpcRaven.Trace = !NpcRaven.Trace; return "Польоти воронів людей у лог: " + (NpcRaven.Trace ? "так" : "ні");
                case "own":
                case "unown":
                    {
                        if (name != "hugin" && name != "munin" && !(what == "unown" && name == "both")) return "raven story own hugin|munin · unown hugin|munin|both";
                        if (what == "own") { StoryOwn.Return(p, name == "munin"); return "Свій " + (name == "munin" ? "Мунін" : "Хугін") + " летить (є вже такий — зіллється з ним)."; }
                        if (name == "both") return StoryOwn.Unown(p, false) + "\n" + StoryOwn.Unown(p, true);
                        return StoryOwn.Unown(p, name == "munin");
                    }
#endif
                case "reset":
                    p.m_customData.Remove(Key);
                    Reset();
                    Log("reset for " + p.GetPlayerName());
                    return "Сюжет персонажа стерто.";
            }
            return Describe(p) + "\nraven story flag|unflag x · var x N|+N|-N · quest x active|done|failed|- · check <умови> · conds · events · trace · raise <подія> [arg] [N] · reset · quests · start|forget <квест> · jump <квест> <етап> · scene <id>|stop|reload · world [x|!x|clear] · played [+хв] · saga · title <глава> · own hugin|munin · unown hugin|munin|both · npcravens · raventrace · placeraven · where|tp <місце> · mark <назва> · rooms [kinds [theme]]";
        }
    }
}
#endif

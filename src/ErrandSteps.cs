#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // One step of an errand. Its count lives in the run (ErrandRun.Count[i]); the step itself only reads its words
    // from the template and says what moves the count. Return true from an event when the count changed.
    internal abstract class ErrandStep
    {
        protected string[] A = new string[0]; // the template's words: A[0] the kind, then its own
        internal void Init(string[] a) { A = a; Read(); }
        protected virtual void Read() { }

        internal abstract int Need { get; }
        internal bool Done(ErrandRun r, int i) { return r.Count[i] >= Need; }
        internal void Force(ErrandRun r, int i) { r.Count[i] = Need; }

        internal virtual void Start(ErrandRun r, int i, Player p) { }
        internal virtual bool OnEvent(ErrandRun r, int i, Ev type, string arg, int value) { return false; }
        internal virtual bool OnKill(ErrandRun r, int i, string prefab, string token, int stars, bool boss, bool raven) { return false; }
        internal virtual bool Tick(ErrandRun r, int i, Player p) { return false; }
        internal virtual string HandIn(ErrandRun r, int i, Player p) { return Lang.T("Зараз нема чого віддавати."); }
        internal virtual bool Where(ErrandRun r, int i, out Vector3 at, out float radius) { at = Vector3.zero; radius = 0f; return false; }

        // The step's zone (Where): the core watches you come in and go out, and calls these. Here a step does its work in
        // the world (a champion appears; later: a cache, a defence…). Inside: every couple of seconds while you are in.
        // Return true when the count changed.
        internal virtual float Margin { get { return 30f; } } // m round the ring that already count as in (the zone loads ahead of you)
        internal virtual bool Enter(ErrandRun r, int i, Player p) { return false; }
        internal virtual bool Inside(ErrandRun r, int i, Player p) { return false; }
        internal virtual void Leave(ErrandRun r, int i, Player p) { }
        internal abstract string Describe(ErrandRun r, int i, Player p);

        protected string Arg(int n, string def) { return A.Length > n ? A[n] : def; }
        protected int ArgI(int n, int def)
        {
            if (A.Length <= n) return def;
            int lo, hi;
            if (ErrandSteps.Range(A[n], out lo, out hi)) return Mathf.Max(1, lo); // (a range not rolled: an errand from before it had one)
            return Mathf.Max(1, Errands.I(A[n], def));
        }
    }

    internal static class ErrandSteps
    {
        // the kinds of steps, by the template's word
        private static readonly Dictionary<string, Func<ErrandStep>> Kinds = new Dictionary<string, Func<ErrandStep>>
        {
            { "kill", () => new KillStep() },
            { "bring", () => new BringStep() },
            { "go", () => new GoStep() },
            { "do", () => new DoStep() },
            { "kill_champion", () => new ChampionStep() },
            { "clear", () => new ClearStep() },
            { "read", () => new ReadStep() },
            { "loot", () => new LootStep() },
            { "destroy", () => new DestroyStep() },
            { "rest", () => new RestStep() },
            { "riddle", () => new RiddleStep() },
        };

        // a count as a range ("20-40"): rolled once when the errand is given (ErrandRun.Need), in steps of 10 or 5 when
        // both ends are such
        internal static bool Range(string w, out int lo, out int hi)
        {
            lo = hi = 0;
            if (string.IsNullOrEmpty(w)) return false;
            int dash = w.IndexOf('-');
            if (dash <= 0 || dash == w.Length - 1) return false;
            if (!int.TryParse(w.Substring(0, dash), NumberStyles.Integer, CultureInfo.InvariantCulture, out lo)) return false;
            if (!int.TryParse(w.Substring(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out hi)) return false;
            return lo >= 1 && hi >= lo;
        }

        // the first word after the kind that is a range (-1: none)
        internal static int RangeArg(string[] words)
        {
            int lo, hi;
            for (int j = 1; j < words.Length; j++) if (Range(words[j], out lo, out hi)) return j;
            return -1;
        }

        internal static int Roll(int lo, int hi)
        {
            int step = lo % 10 == 0 && hi % 10 == 0 ? 10 : lo % 5 == 0 && hi % 5 == 0 ? 5 : 1;
            return lo + step * UnityEngine.Random.Range(0, (hi - lo) / step + 1);
        }

        internal static bool Known(string kind) { return Kinds.ContainsKey(kind); }

        internal static ErrandStep Make(string[] words)
        {
            Func<ErrandStep> make;
            ErrandStep s = words.Length > 0 && Kinds.TryGetValue(words[0], out make) ? make() : new UnknownStep();
            s.Init(words);
            return s;
        }

        // ---------------------------------------------------------------- slots: filled once, when the errand is given
        // words: name, kind, args… → the value kept in the run; null when it cannot be filled (why says so)
        internal static string Fill(Player p, string[] slot, Dictionary<string, string> filled, float within, out string why)
        {
            why = null;
            string kind = slot.Length > 1 ? slot[1] : "";
            switch (kind)
            {
                case "around": return Around(p, slot.Length > 2 ? Errands.F(slot[2], 200f) : 200f, slot.Length > 3 ? Errands.F(slot[3], 600f) : 600f, within, out why);
                case "riddle":
                    Riddles.Riddle rd = Riddles.Pick(p);
                    if (rd == null) { why = "no riddle for this stage"; return null; }
                    return rd.Id;
                case "champion":
                    // champion around <min> <max>: a place, and a champion of that land at your stage;
                    // champion near <slot> <min> <max>: beside another slot's place (two of them in one ring)
                    string pt, other;
                    if (slot.Length > 3 && slot[2] == "near")
                    {
                        if (filled == null || !filled.TryGetValue(slot[3], out other)) { why = "slot " + slot[0] + ": no slot «" + slot[3] + "» before it"; return null; }
                        pt = Near(other, slot.Length > 4 ? Errands.F(slot[4], 20f) : 20f, slot.Length > 5 ? Errands.F(slot[5], 40f) : 40f);
                    }
                    else pt = Around(p, slot.Length > 3 ? Errands.F(slot[3], 300f) : 300f, slot.Length > 4 ? Errands.F(slot[4], 900f) : 900f, within, out why);
                    Vector3 at;
                    if (pt == null || !Point(pt, out at)) return null;
                    // options after the numbers: lord (a guardian: a boss's bar, size, music), who:<Prefab>, title:<Its_name>
                    string force = "", title = "";
                    bool lord = false;
                    for (int k = 3; k < slot.Length; k++)
                    {
                        if (slot[k] == "lord") lord = true;
                        else if (slot[k].StartsWith("who:")) force = slot[k].Substring(4);
                        else if (slot[k].StartsWith("title:")) title = slot[k].Substring(6).Replace('_', ' ');
                    }
                    Champions.Mods m = Champions.Roll(p, at, force, lord);
                    if (m == null) { why = Lang.T("Одін не знайшов там гідного ворога."); return null; }
                    return pt + "|" + m.Who + "|" + m.Level + "|" + m.Hp.ToString("0.##", CultureInfo.InvariantCulture) + "|" + m.Dmg.ToString("0.##", CultureInfo.InvariantCulture) + "|" + string.Join("+", m.Aff.ToArray()) + "|" + title;
            }
            why = "unknown slot «" + kind + "»";
            return null;
        }

        // a place on land this far from another (a slot's value): the same land if it can be
        private static string Near(string v, float min, float max)
        {
            Vector3 c;
            if (!ParsePoint(v, out c)) return v;
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            Heightmap.Biome land = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(c.x, c.z) : Heightmap.Biome.None;
            for (int tries = 0; tries < 40 && WorldGenerator.instance != null; tries++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2f, dist = UnityEngine.Random.Range(min, Mathf.Max(min, max));
                float x = c.x + Mathf.Cos(ang) * dist, z = c.z + Mathf.Sin(ang) * dist;
                if (WorldGenerator.instance.GetHeight(x, z) < sea + 1f) continue;
                if (tries < 25 && WorldGenerator.instance.GetBiome(x, z) != land) continue;
                return Pt(x) + "/" + Pt(z);
            }
            return Pt(c.x) + "/" + Pt(c.z);
        }

        // a place on land this far from you, in a land you have already walked (the Meadows always); within: this near
        // the world's middle at most (0: anywhere)
        private static string Around(Player p, float min, float max, float within, out string why)
        {
            why = null;
            if (p == null || WorldGenerator.instance == null) { why = Lang.T("Світ ще не готовий."); return null; }
            Vector3 from = p.transform.position;
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            string lands;
            if (!p.m_customData.TryGetValue(Diary.LandsKey, out lands)) lands = "|";
            for (int tries = 0; tries < 60; tries++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2f, dist = UnityEngine.Random.Range(min, Mathf.Max(min, max));
                float x = from.x + Mathf.Cos(ang) * dist, z = from.z + Mathf.Sin(ang) * dist;
                if (x * x + z * z > 9800f * 9800f) continue; // the world's edge
                if (within > 0f && x * x + z * z > within * within) continue;
                Heightmap.Biome b = WorldGenerator.instance.GetBiome(x, z);
                if (b == Heightmap.Biome.Ocean || b == Heightmap.Biome.None) continue;
                if (b != Heightmap.Biome.Meadows && !lands.Contains("|" + b + "|")) continue; // not sent where you have never been
                if (WorldGenerator.instance.GetHeight(x, z) < sea + 1f) continue; // under water
                return Pt(x) + "/" + Pt(z);
            }
            why = Lang.T("Одін не знайшов поблизу місця, куди тебе послати.");
            return null;
        }

        internal static string Pt(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }

        // a slot's place: the value's first part (a champion's slot has more after "|"). Asked several times a second
        // (the step, its zone, the book): parsed and its height found once per value
        private static readonly Dictionary<string, Vector3> Points = new Dictionary<string, Vector3>();
        private static WorldGenerator _pointsWorld;
        internal static bool Point(string v, out Vector3 at)
        {
            at = Vector3.zero;
            if (string.IsNullOrEmpty(v)) return false;
            if (_pointsWorld != WorldGenerator.instance) { Points.Clear(); _pointsWorld = WorldGenerator.instance; } // (another world: other heights)
            if (Points.TryGetValue(v, out at)) return true;
            if (!ParsePoint(v, out at)) return false;
            if (WorldGenerator.instance == null) return true; // (no height yet: not kept)
            if (Points.Count > 32) Points.Clear();
            Points[v] = at;
            return true;
        }

        private static bool ParsePoint(string v, out Vector3 at)
        {
            at = Vector3.zero;
            int bar = v.IndexOf('|');
            if (bar >= 0) v = v.Substring(0, bar);
            string[] xz = v.Split('/');
            if (xz.Length != 2) return false;
            at = new Vector3(Errands.F(xz[0], 0f), 0f, Errands.F(xz[1], 0f));
            if (WorldGenerator.instance != null) at.y = Mathf.Max(WorldGenerator.instance.GetHeight(at.x, at.z), ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f);
            return true;
        }

        // ---------------------------------------------------------------- names for the words
        internal static string Creature(string prefab)
        {
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            Character c = go != null ? go.GetComponent<Character>() : null;
            return c != null ? Loc(c.m_name) : prefab;
        }

        // a thing of the world (a nest, a rock): our own word for the ones errands use, else its hover or piece name
        private static readonly Dictionary<string, string> Pieces = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Spawner_GreydwarfNest", Lang.K("гніздо грейдворфів") },
        };
        internal static string Piece(string prefab)
        {
            string own;
            if (Pieces.TryGetValue(prefab, out own)) return Lang.T(own);
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (go == null) return prefab;
            HoverText h = go.GetComponent<HoverText>();
            if (h != null && !string.IsNullOrEmpty(h.m_text)) return Loc(h.m_text);
            Piece pc = go.GetComponent<Piece>();
            return pc != null && !string.IsNullOrEmpty(pc.m_name) ? Loc(pc.m_name) : prefab;
        }

        // picked from a plant, a gift of the woods: something to eat (berries, mushrooms…) or a flower — not stones,
        // flint or branches
        private static readonly string[] Flowers = { "Dandelion", "Thistle", "Fiddleheadfern" };
        internal static bool Forage(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return false;
            if (Array.IndexOf(Flowers, prefab) >= 0) return true;
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            if (d == null || d.m_itemData == null) return false;
            ItemDrop.ItemData.SharedData s = d.m_itemData.m_shared;
            return s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f;
        }

        internal static string Item(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            return d != null ? Loc(d.m_itemData.m_shared.m_name) : prefab;
        }

        private static string Loc(string s) { return Localization.instance != null ? Localization.instance.Localize(s) : s; }
    }

    // kill <Prefab,Prefab…|*> <count> [near <slot> <radius>] [raven]: any of them (by prefab or the game's name token;
    // * anyone but a boss); near: only while you are that close to the slot's place; raven: only when the last blow was
    // a raven's (Strike's mark)
    internal sealed class KillStep : ErrandStep
    {
        private string[] _who = new string[0];
        private int _n = 1;
        private string _near = "";
        private float _radius = 150f;
        private bool _any, _raven, _night;
        protected override void Read()
        {
            _who = Arg(1, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _any = _who.Length == 1 && _who[0] == "*";
            _n = ArgI(2, 1);
            for (int k = 3; k < A.Length; k++)
            {
                if (A[k] == "near" && k + 1 < A.Length) { _near = A[k + 1]; _radius = Mathf.Max(10f, Errands.F(Arg(k + 2, "150"), 150f)); k += 2; }
                else if (A[k] == "raven") _raven = true;
                else if (A[k] == "night") _night = true;
            }
        }
        internal override int Need { get { return _n; } }

        private bool Close(ErrandRun r, Player p)
        {
            if (_near.Length == 0) return true;
            string v;
            Vector3 at;
            if (p == null || !r.Slot.TryGetValue(_near, out v) || !ErrandSteps.Point(v, out at)) return false;
            Vector3 d = p.transform.position - at; d.y = 0f;
            return d.sqrMagnitude <= _radius * _radius;
        }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = _radius; at = Vector3.zero;
            string v;
            return _near.Length > 0 && r.Slot.TryGetValue(_near, out v) && ErrandSteps.Point(v, out at);
        }

        internal override bool OnKill(ErrandRun r, int i, string prefab, string token, int stars, bool boss, bool raven)
        {
            if (_raven && !raven || _night && !EnvMan.IsNight() || !Close(r, Player.m_localPlayer)) return false;
            if (_any) { if (boss) return false; r.Count[i] = Mathf.Min(_n, r.Count[i] + 1); return true; }
            foreach (string w in _who)
                if (string.Equals(w, prefab, StringComparison.OrdinalIgnoreCase) || string.Equals("enemy_" + w, token, StringComparison.OrdinalIgnoreCase) || string.Equals(w, token, StringComparison.OrdinalIgnoreCase))
                { r.Count[i] = Mathf.Min(_n, r.Count[i] + 1); return true; }
            return false;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            string line;
            if (_any) line = Lang.F("Убий будь-кого — {0}/{1}", r.Count[i], _n);
            else
            {
                List<string> names = new List<string>();
                foreach (string w in _who) names.Add(ErrandSteps.Creature(w));
                line = Lang.F("Убий: {0} — {1}/{2}", string.Join(Lang.T(" чи "), names.ToArray()), r.Count[i], _n);
            }
            if (_raven) line += Lang.T(" (останній удар — воронячий)");
            if (_night) line += Lang.T(" (лише вночі)");
            return _near.Length > 0 ? line + Lang.T(" (біля позначеного місця)") : line;
        }
    }

    // bring <Prefab> <count>: handed to the raven (the nest's «Віддати»); taken from the bag then
    internal sealed class BringStep : ErrandStep
    {
        private string _what = "";
        private int _n = 1;
        protected override void Read() { _what = Arg(1, ""); _n = ArgI(2, 1); }
        internal override int Need { get { return _n; } }

        internal override string HandIn(ErrandRun r, int i, Player p)
        {
            if (p == null) return Lang.T("Нема гравця.");
            int have = Trust.Have(p, _what);
            if (have < _n) return Lang.F("Ще бракує: {0} — {1}/{2}.", ErrandSteps.Item(_what), have, _n);
            ErrandMark.Take(p, _what, _n); // (the ones got for this errand first)
            r.Count[i] = _n;
            return null;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            int have = p != null ? Mathf.Min(_n, Trust.Have(p, _what)) : 0;
            if (r.Def != null && r.Def.Trial) // (Odin's trial: no raven yet, it goes into the nest's bowl)
                return r.Count[i] >= _n ? Lang.F("Покладено в чашу: {0}", ErrandSteps.Item(_what))
                    : Lang.F("Поклади в чашу гнізда: {0} — у сумці {1}/{2}", ErrandSteps.Item(_what), have, _n);
            return r.Count[i] >= _n ? Lang.F("Віддано ворону: {0}", ErrandSteps.Item(_what))
                : Lang.F("Віддай ворону: {0} — у сумці {1}/{2}", ErrandSteps.Item(_what), have, _n);
        }
    }

    // go <slot> <radius>: come to that place
    internal sealed class GoStep : ErrandStep
    {
        private string _slot = "";
        private float _radius = 40f;
        protected override void Read() { _slot = Arg(1, ""); _radius = Mathf.Max(5f, Errands.F(Arg(2, "40"), 40f)); }
        internal override int Need { get { return 1; } }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            Vector3 at;
            if (p == null || r.Count[i] >= 1 || !ErrandSteps.Point(Slot(r), out at)) return false;
            Vector3 d = p.transform.position - at; d.y = 0f;
            if (d.sqrMagnitude > _radius * _radius) return false;
            r.Count[i] = 1;
            return true;
        }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = _radius;
            return ErrandSteps.Point(Slot(r), out at);
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            Vector3 at;
            if (r.Count[i] >= 1) return Lang.T("Дійшов до позначеного місця");
            if (p == null || !ErrandSteps.Point(Slot(r), out at)) return Lang.T("Дійди до позначеного на мапі місця");
            Vector3 d = at - p.transform.position; d.y = 0f;
            return Lang.F("Дійди до позначеного на мапі місця — ще {0} м", Mathf.RoundToInt(d.magnitude));
        }

        private string Slot(ErrandRun r) { string v; return r.Slot.TryGetValue(_slot, out v) ? v : ""; }
    }

    // do <Event> <count> [arg]: a deed the diary sees (Fish, Chop, Mine, Cook, Craft, Plant, Smelt, Trophy, Parry…)
    internal sealed class DoStep : ErrandStep
    {
        private Ev _ev;
        private bool _ok;
        private int _n = 1;
        private string _arg = "";
        protected override void Read()
        {
            try { _ev = (Ev)Enum.Parse(typeof(Ev), Arg(1, ""), true); _ok = true; } catch { _ok = false; Errands.Log("step do: unknown deed «" + Arg(1, "") + "»"); }
            _n = ArgI(2, 1);
            _arg = Arg(3, "");
        }
        internal override int Need { get { return _n; } }
        private const string ForageArg = "forage"; // do Pick <n> forage: only what the woods give — berries, mushrooms, flowers

        internal override bool OnEvent(ErrandRun r, int i, Ev type, string arg, int value)
        {
            if (!_ok || type != _ev) return false;
            if (_arg == ForageArg) { if (!ErrandSteps.Forage(arg)) return false; }
            else if (_arg.Length > 0 && !string.Equals(_arg, arg, StringComparison.OrdinalIgnoreCase)) return false;
            r.Count[i] = Mathf.Min(_n, r.Count[i] + 1);
            return true;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            string what;
            switch (_ev)
            {
                case Ev.Fish: what = Lang.T("Злови рибу"); break;
                case Ev.Chop: what = Lang.T("Зрубай дерево"); break;
                case Ev.Mine: what = Lang.T("Видобудь руду чи камінь"); break;
                case Ev.Cook: what = Lang.T("Приготуй страву"); break;
                case Ev.Craft: what = Lang.T("Зроби річ"); break;
                case Ev.Plant: what = Lang.T("Посади щось"); break;
                case Ev.Smelt: what = Lang.T("Переплав руду"); break;
                case Ev.Trophy: what = Lang.T("Здобудь трофей"); break;
                case Ev.Parry: what = Lang.T("Відбий удар вчасно"); break;
                case Ev.Build: what = Lang.T("Збудуй щось"); break;
                case Ev.Potion: what = Lang.T("Випий зілля"); break;
                case Ev.Pick: what = _arg == ForageArg ? Lang.T("Збери ягоди, гриби чи квіти") : Lang.T("Збери щось"); break;
                case Ev.Ferment: what = Lang.T("Постав основу меду бродити"); break;
                case Ev.TrophyHang: what = Lang.T("Повісь трофей на підставку"); break;
                case Ev.Fuel: what = _arg.Length > 0 ? Lang.F("Підкинь у вогонь: {0}", ErrandSteps.Item(_arg)) : Lang.T("Підкинь паливо у вогонь"); break;
                default: what = _ok ? _ev.ToString() : "?"; break;
            }
            return Lang.F("{0} — {1}/{2}", what, r.Count[i], _n);
        }
    }

    // kill_champion <slot>[,<slot>…]: the champions of those slots (Champions) — they appear when you come near their
    // place; the map shows a ring round them, not the very spot. Several: all of them in one ring (their slots
    // «champion near» the first), each counted once: the count is a mask, a bit for each
    internal sealed class ChampionStep : ErrandStep
    {
        private string[] _slots = new string[0];
        private string _guard = ""; // guard <slot>: it waits for you to come out of that place (a barrow) — then rises
        private float _rise;         // when the ritual ends and it stands (0: not begun)
        protected override void Read()
        {
            _slots = Arg(1, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (Arg(2, "") == "guard") _guard = Arg(3, "");
        }
        internal override int Need { get { return (1 << Mathf.Clamp(_slots.Length, 1, 8)) - 1; } }

        internal static string Stamp(ErrandRun r, int i) { return r.Id + "@" + r.Given.ToString("0", CultureInfo.InvariantCulture) + "." + i; }
        // (a step object belongs to one run at one place: its stamps made once; the first one as before several were)
        private string[] _stamps;
        internal string StampOf(ErrandRun r, int i, int k)
        {
            if (_stamps == null)
            {
                _stamps = new string[Mathf.Max(1, _slots.Length)];
                string s0 = Stamp(r, i);
                for (int n = 0; n < _stamps.Length; n++) _stamps[n] = n == 0 ? s0 : s0 + "#" + n;
            }
            return k >= 0 && k < _stamps.Length ? _stamps[k] : "";
        }

        private static bool Got(ErrandRun r, int i, int k) { return (r.Count[i] & (1 << k)) != 0; }

        // still to be killed in this step (the core asks for each step not yet behind: Errands.Wants)
        internal bool Wants(ErrandRun r, int i, string stamp)
        {
            for (int k = 0; k < _slots.Length; k++) if (!Got(r, i, k) && StampOf(r, i, k) == stamp) return true;
            return false;
        }

        private Champions.Mods Mods(ErrandRun r, int k)
        {
            string v;
            if (k >= _slots.Length || !r.Slot.TryGetValue(_slots[k], out v)) return null;
            string[] w = v.Split('|');
            if (w.Length < 5) return null;
            Champions.Mods m = new Champions.Mods { Who = w[1], Level = Mathf.Clamp(Errands.I(w[2], 2), 1, 10), Hp = Errands.F(w[3], 1f), Dmg = Errands.F(w[4], 1f) };
            if (w.Length > 5) foreach (string a in w[5].Split('+')) if (a.Length > 0) m.Aff.Add(a);
            if (w.Length > 6) m.Title = w[6];
            return m;
        }

        // in its zone: each champion not yet killed is there (found, or made once its place is loaded). A guard: only
        // when you stand outside by its place (out of the barrow, not inside it) — the ground shakes, then it rises
        internal override bool Inside(ErrandRun r, int i, Player p)
        {
            if (p == null) return false;
            if (_guard.Length > 0) { Guard(r, i, p); return false; }
            for (int k = 0; k < _slots.Length; k++)
            {
                if (Got(r, i, k)) continue;
                Vector3 at;
                string v;
                if (!r.Slot.TryGetValue(_slots[k], out v) || !ErrandSteps.Point(v, out at)) continue;
                Champions.Mods m = Mods(r, k);
                if (m != null) Champions.Ensure(p, StampOf(r, i, k), at, m);
            }
            return false;
        }

        private void Guard(ErrandRun r, int i, Player p)
        {
            if (Got(r, i, 0)) return;
            string v, gv;
            Vector3 at, door;
            if (!r.Slot.TryGetValue(_slots[0], out v) || !ErrandSteps.Point(v, out at)) return;
            Champions.Mods m = Mods(r, 0);
            if (m == null) return;
            Character have = Champions.Find(StampOf(r, i, 0), p.GetPlayerID());
            if (have != null)
            {
                if (Vector3.Distance(have.transform.position, p.transform.position) < 70f) FightMusic.Keep(); else FightMusic.Stop();
                return;
            }
            // it rose once already (gone from sight, or killed without you): it stands there again, no second ritual
            if (_rise <= 0f && r.Mark("risen" + i).Length > 0) { Champions.Ensure(p, StampOf(r, i, 0), at, m); return; }
            if (_rise > 0f)
            {
                if (Time.time < _rise) return;
                Character c = Champions.Ensure(p, StampOf(r, i, 0), at, m);
                if (c == null) return; // (its ground not loaded yet: next time)
                _rise = 0f;
                r.SetMark("risen" + i, "1");
                Errands.Keep(p, r);
                RavenFx.Play("vfx_offering", c.transform.position, Quaternion.identity);
                if (GameCamera.instance != null) GameCamera.instance.AddShake(c.transform.position, 40f, 3f, false);
                RavenMsg.Center(p, Lang.F("{0} постав з-під землі!", Champions.Name(m)));
                FightMusic.Keep();
                return;
            }
            // you, out of it, at its door
            if (p.InInterior() || !r.Slot.TryGetValue(_guard, out gv) || !ErrandSteps.Point(gv, out door)) return;
            Vector3 d = p.transform.position - door; d.y = 0f;
            if (d.sqrMagnitude > 45f * 45f) return;
            _rise = Time.time + 3f;
            RavenFx.Play("vfx_offering", at, Quaternion.identity);
            RavenFx.Play("sfx_offering", at, Quaternion.identity);
            if (GameCamera.instance != null) GameCamera.instance.AddShake(at, 60f, 2f, false);
            RavenMsg.Center(p, Lang.T("Земля над курганом здригнулася…"));
            Errands.Log("guardian of " + r.Id + " rises at " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z));
        }

        internal override void Leave(ErrandRun r, int i, Player p) { if (_guard.Length > 0) FightMusic.Stop(); }

        internal bool Killed(ErrandRun r, int i, string stamp)
        {
            for (int k = 0; k < _slots.Length; k++)
                if (!Got(r, i, k) && StampOf(r, i, k) == stamp) { r.Count[i] |= 1 << k; if (_guard.Length > 0) FightMusic.Stop(); return true; }
            return false;
        }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = Champions.SpawnNear;
            at = Vector3.zero;
            int n = 0;
            float far = 0f;
            Vector3 first = Vector3.zero;
            foreach (string sl in _slots)
            {
                string v;
                Vector3 pt;
                if (!r.Slot.TryGetValue(sl, out v) || !ErrandSteps.Point(v, out pt)) continue;
                if (n == 0) first = pt;
                else far = Mathf.Max(far, new Vector2(pt.x - first.x, pt.z - first.z).magnitude);
                at += pt; n++;
            }
            if (n == 0) return false;
            at /= n;
            radius += far * 0.5f; // (all of them inside the one ring)
            // the ring's middle off the very spot (the same every time): "somewhere here"
            int h = StampOf(r, i, 0).GetStableHashCode();
            float ang = (h & 0xFFFF) / 65535f * Mathf.PI * 2f, dist = ((h >> 16) & 0xFF) / 255f * 35f;
            at += new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
            return true;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (_slots.Length > 1)
            {
                List<string> left = new List<string>();
                int got = 0;
                for (int k = 0; k < _slots.Length; k++) { if (Got(r, i, k)) { got++; continue; } Champions.Mods mk = Mods(r, k); if (mk != null) left.Add(Champions.Name(mk)); }
                if (left.Count == 0) return Lang.F("Переміг ватажків: {0}", _slots.Length);
                return Lang.F("Знайди й убий ватажків у колі на мапі — {0}/{1}: {2}", got, _slots.Length, string.Join(", ", left.ToArray()));
            }
            Champions.Mods m = Mods(r, 0);
            if (m == null) return Lang.T("Убий ватажка");
            if (r.Count[i] >= 1) return Lang.F("Переміг: {0}", Champions.Name(m));
            if (_guard.Length > 0) return r.Mark("risen" + i).Length > 0 ? Lang.F("Здолай: {0} — він біля входу в курган", Champions.Name(m))
                : Lang.F("Вийди з кургану: біля входу на тебе чекає {0}", Champions.Name(m));
            string w = Champions.Words(m);
            return Lang.F("Знайди й убий: {0}{1} — він десь у колі на мапі", Champions.Name(m), w.Length > 0 ? " (" + w + ")" : "");
        }
    }

    // clear <slot> [radius]: come to that place (a crypt, a cave…) and kill everything its spawners hold (PlaceClear);
    // what never woke there because its group already has one, or the world's progress forbids it, is not asked
    internal sealed class ClearStep : ErrandStep
    {
        private string _slot = "";
        private float _radius = 40f;
        private int _left = -1; // (last look: enemies still there; -1 not looked yet)
        protected override void Read() { _slot = Arg(1, ""); _radius = Mathf.Max(10f, Errands.F(Arg(2, "40"), 40f)); }
        internal override int Need { get { return 1; } }
        internal override float Margin { get { return PlaceClear.Reach; } } // (a dungeon below fills its zone: far from its door)

        private string Slot(ErrandRun r) { string v; return r.Slot.TryGetValue(_slot, out v) ? v : ""; }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = _radius;
            return ErrandSteps.Point(Slot(r), out at);
        }

        internal override bool Inside(ErrandRun r, int i, Player p)
        {
            Vector3 at;
            if (p == null || r.Count[i] >= 1 || !ErrandSteps.Point(Slot(r), out at)) return false;
            int seen;
            _left = PlaceClear.Left(at, out seen);
            if (_left != 0) return false; // (-1: not loaded yet)
            Errands.Log("place " + Slot(r) + " cleared (" + seen + " spawners there)");
            r.Count[i] = 1;
            return true;
        }

        internal override void Leave(ErrandRun r, int i, Player p) { _left = -1; }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= 1) return Lang.T("Місце зачищене");
            if (_left > 0 && Errands.InZone) return Lang.F("Зачисть це місце — ще ворогів: {0}", _left);
            Vector3 at;
            if (p == null || !ErrandSteps.Point(Slot(r), out at)) return Lang.T("Дійди до позначеного на мапі місця й зачисть його");
            Vector3 d = at - p.transform.position; d.y = 0f;
            return Lang.F("Дійди до позначеного на мапі місця й зачисть його — ще {0} м", Mathf.RoundToInt(d.magnitude));
        }
    }

    // read <slot> [radius]: read the runestone there (its words open on E: RuneReadPatch)
    internal sealed class ReadStep : ErrandStep
    {
        private string _slot = "";
        private float _radius = 30f;
        protected override void Read() { _slot = Arg(1, ""); _radius = Mathf.Max(5f, Errands.F(Arg(2, "30"), 30f)); }
        internal override int Need { get { return 1; } }

        private string Slot(ErrandRun r) { string v; return r.Slot.TryGetValue(_slot, out v) ? v : ""; }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = _radius;
            return ErrandSteps.Point(Slot(r), out at);
        }

        internal bool OnRead(ErrandRun r, int i, Vector3 stone)
        {
            Vector3 at;
            if (r.Count[i] >= 1 || !ErrandSteps.Point(Slot(r), out at)) return false;
            Vector3 d = stone - at; d.y = 0f;
            if (d.sqrMagnitude > _radius * _radius) return false;
            r.Count[i] = 1;
            return true;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= 1) return Lang.T("Прочитав рунний камінь");
            Vector3 at;
            if (p == null || !ErrandSteps.Point(Slot(r), out at)) return Lang.T("Дійди до позначеного на мапі каменя й прочитай його");
            Vector3 d = at - p.transform.position; d.y = 0f;
            return Lang.F("Дійди до позначеного на мапі каменя й прочитай його — ще {0} м", Mathf.RoundToInt(d.magnitude));
        }
    }

    // loot <Prefab> <count>: picked up from the world by your own hand — dropped by a creature, a rock, a plant — never
    // from what a player threw down (LootMark); what is in the bag already does not count
    internal sealed class LootStep : ErrandStep
    {
        private string _what = "";
        private int _n = 1;
        protected override void Read() { _what = Arg(1, ""); _n = ArgI(2, 1); }
        internal override int Need { get { return _n; } }

        internal bool Wants(string prefab) { return string.Equals(prefab, _what, StringComparison.OrdinalIgnoreCase); }

        internal bool OnGain(ErrandRun r, int i, string prefab, int amount)
        {
            if (amount <= 0 || !string.Equals(prefab, _what, StringComparison.OrdinalIgnoreCase)) return false;
            r.Count[i] = Mathf.Min(_n, r.Count[i] + amount);
            return true;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            return Lang.F("Добудь сам: {0} — {1}/{2}", ErrandSteps.Item(_what), r.Count[i], _n);
        }
    }

    // destroy <Prefab> <count>: things of the world broken (a greydwarf nest…): you hit it, and it is gone within a few
    // seconds (DestroyWatch; whoever gave the last blow — you were in it)
    internal sealed class DestroyStep : ErrandStep
    {
        private string _what = "";
        private int _n = 1;
        protected override void Read() { _what = Arg(1, ""); _n = ArgI(2, 1); }
        internal override int Need { get { return _n; } }
        internal bool Wants(string prefab) { return string.Equals(prefab, _what, StringComparison.OrdinalIgnoreCase); }

        internal bool OnGone(ErrandRun r, int i, string prefab)
        {
            if (!Wants(prefab)) return false;
            r.Count[i] = Mathf.Min(_n, r.Count[i] + 1);
            return true;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            return Lang.F("Зруйнуй: {0} — {1}/{2}", ErrandSteps.Piece(_what), r.Count[i], _n);
        }
    }

    // rest <seconds>: sit by a fire at night with every raven you wear on your shoulder (all of it together; the time
    // adds up, counted in tens of seconds). rest dawn: be sitting so when the morning comes (6:00), after at least
    // DawnSat seconds of that night by the fire (sleeping through it is not sitting)
    internal sealed class RestStep : ErrandStep
    {
        private const int Unit = 10;
        private const float DawnSat = 120f;
        private int _n = 120;
        private bool _dawn;
        private float _sat, _last, _okAt, _prev = -1f;
        private string _why = "";
        private static readonly int CampFire = "CampFire".GetStableHashCode();
        protected override void Read()
        {
            _dawn = Arg(1, "") == "dawn";
            _n = _dawn ? 1 : Mathf.Max(Unit, ArgI(1, 120) / Unit * Unit);
        }
        internal override int Need { get { return _n; } }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            float now = Time.time, dt = _last > 0f ? Mathf.Min(2f, now - _last) : 0f;
            _last = now;
            if (p == null || r.Count[i] >= _n) return false;
            if (_dawn) return Dawn(r, i, p, now, dt);
            _why = Why(p);
            if (_why.Length > 0) return false;
            _sat += dt;
            if (_sat < Unit) return false;
            _sat -= Unit;
            r.Count[i] = Mathf.Min(_n, r.Count[i] + Unit);
            return true;
        }

        private bool Dawn(ErrandRun r, int i, Player p, float now, float dt)
        {
            if (EnvMan.instance == null) return false;
            float f = EnvMan.instance.GetDayFraction();
            string by = Why(p, false); // (the night itself is not asked: the morning is what we wait for)
            bool ok = by.Length == 0;
            if (ok && EnvMan.IsNight()) { _sat += dt; _okAt = now; }
            bool dawned = _prev >= 0f && _prev < 0.25f && f >= 0.25f && f < 0.4f;
            _prev = f;
            _why = !ok ? by : _sat >= DawnSat ? "" : EnvMan.IsNight() ? Lang.T("посидь довше") : Lang.T("чекай ночі");
            if (!dawned) return false;
            if (ok && now - _okAt < 5f && _sat >= DawnSat) { r.Count[i] = 1; return true; }
            _sat = 0f; // (the morning found you elsewhere: the next night anew)
            return false;
        }

        // what is missing ("" nothing)
        private static string Why(Player p) { return Why(p, true); }

        private static string Why(Player p, bool night)
        {
            if (night && !EnvMan.IsNight()) return Lang.T("чекай ночі");
            if (!p.GetSEMan().HaveStatusEffect(CampFire)) return Lang.T("ближче до вогню");
            if (!p.IsSitting() && !(p.IsAttached() && !p.IsAttachedToShip())) return Lang.T("сядь");
            bool any = false;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData worn = Trust.Worn(m);
                if (worn == null || Trust.Stranger(worn, p)) continue;
                if (!Trust.Active(worn) || Shoulder.NotOnShoulder(p, m) != null) return Lang.F("{0} не на плечі", Plugin.RavenName(m));
                any = true;
            }
            return any ? "" : Lang.T("без ворона на плечі");
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            string line;
            if (_dawn)
            {
                if (r.Count[i] >= 1) return Lang.T("Досидів біля вогню до світанку");
                float f = EnvMan.instance != null ? EnvMan.instance.GetDayFraction() : 0f;
                int min = Mathf.FloorToInt(f * 24f * 60f);
                line = Lang.F("Досиди біля вогню до світанку з воронами на плечах — зараз {0:00}:{1:00}", min / 60, min % 60);
            }
            else line = Lang.F("Посидь уночі біля вогню з воронами на плечах — {0}/{1} с", r.Count[i], _n);
            return r.Count[i] < _n && _why.Length > 0 ? line + " (" + _why + ")" : line;
        }
    }

    // riddle <slot>: Odin's riddle (Riddles); the answer is laid in the nest's bowl and given to Odin (TryRiddle) — the
    // book never names it
    internal sealed class RiddleStep : ErrandStep
    {
        private string _slot = "";
        protected override void Read() { _slot = Arg(1, ""); }
        internal override int Need { get { return 1; } }

        internal Riddles.Riddle Of(ErrandRun r) { string v; return r.Slot.TryGetValue(_slot, out v) ? Riddles.Find(v) : null; }

        internal int Answer(ErrandRun r, int i, Player p, string prefab, int n, out string why)
        {
            why = null;
            Riddles.Riddle rd = Of(r);
            if (rd == null || r.Count[i] >= 1 || !string.Equals(prefab, rd.Prefab, StringComparison.OrdinalIgnoreCase)) return 0;
            if (n < rd.Count) return 0; // (too few: as any wrong answer, or one piece at a time would tell the answer)
            if (Trust.Have(p, rd.Prefab) < rd.Count) { why = Lang.T("У сумці цього вже нема."); return 0; }
            p.GetInventory().RemoveItem(Trust.FindName(p, rd.Prefab), rd.Count, -1, false);
            r.Count[i] = 1;
            return rd.Count;
        }

        internal override string HandIn(ErrandRun r, int i, Player p) { return Lang.T("Відгадку кладуть у чашу гнізда й віддають Одіну."); }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            Riddles.Riddle rd = Of(r);
            if (rd == null) return Lang.T("Загадка Одіна");
            if (r.Count[i] >= 1) return Lang.F("Відгадано: {0}", ErrandSteps.Item(rd.Prefab));
            return Lang.F("Загадка: «{0}» Поклади відгадку в чашу гнізда ({1} шт.) і віддай Одіну.", Lang.T(rd.Text), rd.Count);
        }
    }

    // a step of a kind this mod does not know (a newer template): it waits; the console can pass it
    internal sealed class UnknownStep : ErrandStep
    {
        internal override int Need { get { return 1; } }
        internal override string Describe(ErrandRun r, int i, Player p) { return Lang.F("(крок «{0}», якого ця версія мода не знає)", Arg(0, "?")); }
    }
}
#endif

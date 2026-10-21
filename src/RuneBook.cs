#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Руни»: the 24 runes of the Elder Futhark, always the same set, by ætt = by layer of what a raven wears:
    // Фрея (the first eight) the feathers' colour, Бальдр (the second) a steady effect, Геймдаль (the third) a trail.
    // A rune is no item: it is a record of the character (player customData RavenWatch_runes). Notches from Odin's
    // errands fill a «різа»; five open one grey rune at random, and at that moment the rune takes a random effect of
    // its layer from the catalog (WearCatalog) for good — so the same rune gives different players different things.
    // An open rune hangs on one raven at a time (its effect goes into that raven's wear, RavenWear); the raven item
    // only wears what its owner's runes give (Publish strips the rest: a raven given away leaves the runes behind).
    // Golden runes (gift upgrades, phase 6) are counted here too, hidden until the first one.
    internal sealed class RuneDef
    {
        public string Id, Glyph, Name;
        public WearKind Kind;
        internal RuneDef(string id, string glyph, string name, WearKind kind) { Id = id; Glyph = glyph; Name = name; Kind = kind; }
    }

    internal static class RuneBook
    {
        internal const string Key = "RavenWatch_runes";
        internal const int Format = 1;
        internal static int NotchesPerRune = 5;

        internal static readonly RuneDef[] All =
        {
            new RuneDef("fehu", "ᚠ", Lang.K("Феху"), WearKind.Freya), new RuneDef("uruz", "ᚢ", Lang.K("Уруз"), WearKind.Freya),
            new RuneDef("thurisaz", "ᚦ", Lang.K("Турисаз"), WearKind.Freya), new RuneDef("ansuz", "ᚨ", Lang.K("Ансуз"), WearKind.Freya),
            new RuneDef("raido", "ᚱ", Lang.K("Райдо"), WearKind.Freya), new RuneDef("kenaz", "ᚲ", Lang.K("Кеназ"), WearKind.Freya),
            new RuneDef("gebo", "ᚷ", Lang.K("Гебо"), WearKind.Freya), new RuneDef("wunjo", "ᚹ", Lang.K("Вуньо"), WearKind.Freya),

            new RuneDef("hagalaz", "ᚺ", Lang.K("Хагалаз"), WearKind.Baldr), new RuneDef("naudiz", "ᚾ", Lang.K("Наутіз"), WearKind.Baldr),
            new RuneDef("isa", "ᛁ", Lang.K("Іса"), WearKind.Baldr), new RuneDef("jera", "ᛃ", Lang.K("Єра"), WearKind.Baldr),
            new RuneDef("eihwaz", "ᛇ", Lang.K("Ейваз"), WearKind.Baldr), new RuneDef("perthro", "ᛈ", Lang.K("Перто"), WearKind.Baldr),
            new RuneDef("algiz", "ᛉ", Lang.K("Альгіз"), WearKind.Baldr), new RuneDef("sowilo", "ᛊ", Lang.K("Совіло"), WearKind.Baldr),

            new RuneDef("tiwaz", "ᛏ", Lang.K("Тейваз"), WearKind.Heimdall), new RuneDef("berkano", "ᛒ", Lang.K("Беркана"), WearKind.Heimdall),
            new RuneDef("ehwaz", "ᛖ", Lang.K("Еваз"), WearKind.Heimdall), new RuneDef("mannaz", "ᛗ", Lang.K("Манназ"), WearKind.Heimdall),
            new RuneDef("laguz", "ᛚ", Lang.K("Лагуз"), WearKind.Heimdall), new RuneDef("ingwaz", "ᛜ", Lang.K("Інгваз"), WearKind.Heimdall),
            new RuneDef("dagaz", "ᛞ", Lang.K("Дагаз"), WearKind.Heimdall), new RuneDef("othala", "ᛟ", Lang.K("Отала"), WearKind.Heimdall),
        };

        internal static readonly string[] KindTitles = Lang.Keys("Руни Фреї", "Руни Бальдра", "Руни Геймдаля");
        internal static readonly string[] KindOne = Lang.Keys("руна Фреї", "руна Бальдра", "руна Геймдаля");
        internal static readonly string[] KindWhat = Lang.Keys("колір пір'я, очі й полиск", "сталий ефект навколо ворона", "слід у польоті");

        internal static RuneDef Def(string id) { foreach (RuneDef d in All) if (d.Id == id) return d; return null; }

        // ---------------------------------------------------------------- the character's record
        // v=1;n=<notches>;r=<rune>:<wear id>,...;new=<rune>,...;x=<the в'язь>;xr=<its runes>,...;g=<golden in hand>;gt=<golden ever>;s=<rune>:<stars>,...;<unknown kept>
        internal sealed class Data
        {
            public int Notches, Gold, GoldEver;
            public readonly List<string> Order = new List<string>();                         // opened runes, in the order they opened
            public readonly Dictionary<string, string> WearOf = new Dictionary<string, string>(); // rune → the effect it took ("" = waits for the catalog)
            public readonly List<string> New = new List<string>();                           // opened and not yet seen in the book
            public string Bind = "";                                                          // the в'язь (Bind.cs id; "" none) — one at most
            public readonly List<string> BindRunes = new List<string>();                      // the runes in it: taken, none worn alone
            public readonly Dictionary<string, int> Stars = new Dictionary<string, int>();                 // rune → its stars from Odin's sparks (RuneStars), 1–3
            public readonly List<KeyValuePair<string, string>> Other = new List<KeyValuePair<string, string>>();

            internal int Opened { get { return Order.Count; } }
            internal string WearFor(string rune) { string w; return rune != null && WearOf.TryGetValue(rune, out w) ? w ?? "" : ""; }
            internal bool Has(string rune) { return WearOf.ContainsKey(rune); }
            internal bool InBind(string rune) { return Bind.Length > 0 && BindRunes.Contains(rune); }
            internal int StarsOf(string rune) { int n; return rune != null && Stars.TryGetValue(rune, out n) ? n : 0; }

            internal static Data Parse(string raw)
            {
                Data d = new Data();
                if (string.IsNullOrEmpty(raw)) return d;
                foreach (string part in raw.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                    switch (k)
                    {
                        case "v": break;
                        case "n": d.Notches = Int(v); break;
                        case "g": d.Gold = Int(v); break;
                        case "gt": d.GoldEver = Int(v); break;
                        case "new": foreach (string s in v.Split(',')) if (s.Length > 0 && !d.New.Contains(s)) d.New.Add(s); break;
                        case "b": break; // (the fixed в'язі of 2.2: gone)
                        case "x": d.Bind = v; break;
                        case "s":
                            foreach (string st in v.Split(','))
                            {
                                int c = st.IndexOf(':');
                                if (c > 0) d.Stars[st.Substring(0, c)] = Mathf.Clamp(Int(st.Substring(c + 1)), 0, RuneStars.Max);
                            }
                            break;
                        case "xr": foreach (string s in v.Split(',')) if (s.Length > 0 && !d.BindRunes.Contains(s)) d.BindRunes.Add(s); break;
                        case "r":
                            foreach (string s in v.Split(','))
                            {
                                int c = s.IndexOf(':');
                                string rune = c >= 0 ? s.Substring(0, c) : s, wear = c >= 0 ? s.Substring(c + 1) : "";
                                if (rune.Length == 0 || d.WearOf.ContainsKey(rune)) continue;
                                d.Order.Add(rune); d.WearOf[rune] = wear;
                            }
                            break;
                        default: d.Other.Add(new KeyValuePair<string, string>(k, v)); break;
                    }
                }
                return d;
            }

            internal string Code()
            {
                StringBuilder sb = new StringBuilder("v=").Append(Format).Append(";n=").Append(Notches);
                if (Order.Count > 0)
                {
                    sb.Append(";r=");
                    for (int i = 0; i < Order.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Order[i]).Append(':').Append(WearFor(Order[i])); }
                }
                if (New.Count > 0) sb.Append(";new=").Append(string.Join(",", New.ToArray()));
                if (Bind.Length > 0) sb.Append(";x=").Append(Bind).Append(";xr=").Append(string.Join(",", BindRunes.ToArray()));
                if (Gold > 0) sb.Append(";g=").Append(Gold);
                if (GoldEver > 0) sb.Append(";gt=").Append(GoldEver);
                bool first = true;
                foreach (string r in Order) { int n; if (!Stars.TryGetValue(r, out n) || n <= 0) continue; sb.Append(first ? ";s=" : ",").Append(r).Append(':').Append(n); first = false; }
                foreach (KeyValuePair<string, string> kv in Other) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
                return sb.ToString();
            }

            private static int Int(string s) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? Mathf.Max(0, v) : 0; }
        }

        private static string _raw;
        private static Data _cache;

        internal static Data Of(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(Key, out raw)) raw = "";
            if (_cache != null && raw == _raw) return _cache;
            _raw = raw; _cache = Data.Parse(raw);
            return _cache;
        }

        internal static void Save(Player p, Data d)
        {
            if (p == null) return;
            string c = d.Code();
            p.m_customData[Key] = c;
            _raw = c; _cache = d;
            Version++;
        }

        internal static int Version; // goes up on every change (the book draws again)

        // ---------------------------------------------------------------- the golden chance
        // 0–2 opened: none; 3–6: 15%; then +2% for every rune past six
        internal static float GoldBase = 15f, GoldStep = 2f;
        internal static int GoldFrom = 3, GoldGrowFrom = 6;
        internal static float GoldChance(int opened)
        {
            if (opened < GoldFrom) return 0f;
            float pct = GoldBase + Mathf.Max(0, opened - GoldGrowFrom) * GoldStep;
            return Mathf.Clamp01(pct / 100f);
        }

        // ---------------------------------------------------------------- notches → runes
        // notches from a done errand; returns the runes that opened (each told in the middle of the screen)
        internal static List<string> AddNotches(Player p, int n)
        {
#if RAVEN_STORY
            if (p != null && n > 0) StoryBus.Raise("notches", "", n);
#endif
            return AddNotches(p, n, false);
        }

        // settle: no new notches, only what is already cut (five or more) opens
        private static List<string> AddNotches(Player p, int n, bool settle)
        {
            List<string> opened = new List<string>();
            if (p == null || (n <= 0 && !settle)) return opened;
            Data d = Of(p);
            d.Notches += n;
            while (d.Notches >= NotchesPerRune)
            {
                string r = OpenOne(d);
                if (r == null) { d.Notches = NotchesPerRune - 1; break; } // all 24 open: the stick waits full (what then is decided later)
                d.Notches -= NotchesPerRune;
                opened.Add(r);
            }
            Save(p, d);
            foreach (string r in opened) TellOpened(p, d, r);
            return opened;
        }

        // one grey rune at random opens and takes its effect for good; null when none is left
        private static string OpenOne(Data d)
        {
            List<RuneDef> grey = new List<RuneDef>();
            foreach (RuneDef r in All) if (!d.Has(r.Id)) grey.Add(r);
            if (grey.Count == 0) return null;
            RuneDef pick = grey[Random.Range(0, grey.Count)];
            string wear = PickWear(d, pick.Kind);
            d.Order.Add(pick.Id);
            d.WearOf[pick.Id] = wear;
            if (!d.New.Contains(pick.Id)) d.New.Add(pick.Id);
            Log("rune " + pick.Id + " opened with «" + d.WearFor(pick.Id) + "» (" + d.Opened + " of " + All.Length + ")");
            return pick.Id;
        }

        // an effect of that layer no other rune of yours took; "" when the catalog has none left (filled in later)
        private static string PickWear(Data d, WearKind kind)
        {
            HashSet<string> taken = new HashSet<string>();
            foreach (string rune in d.Order) { RuneDef r = Def(rune); if (r != null && r.Kind == kind && d.WearFor(rune).Length > 0) taken.Add(d.WearFor(rune)); }
            List<WearDef> free = new List<WearDef>();
            foreach (WearDef w in WearCatalog.Of(kind)) if (!taken.Contains(w.Id)) free.Add(w);
            if (free.Count == 0) { Log("no free " + WearCatalog.KindNames[(int)kind] + " effect in the catalog: the rune waits for one"); return ""; }
            return free[Random.Range(0, free.Count)].Id;
        }

        // runes opened while their layer's catalog had nothing free: they take one now (the book calls it on opening)
        internal static void FillWaiting(Player p)
        {
            Data d = Of(p);
            if (d.Notches >= NotchesPerRune) { AddNotches(p, 0, true); d = Of(p); }
            bool changed = false;
            foreach (string rune in d.Order)
            {
                RuneDef r = Def(rune);
                if (r == null) continue;
                string had = d.WearFor(rune);
                if (had.Length > 0 && WearCatalog.Find(r.Kind, had) != null) continue;
                string w = PickWear(d, r.Kind);
                if (w.Length == 0) continue;
                if (had.Length > 0) Log("rune " + rune + ": its effect «" + had + "» is no longer in the catalog, takes «" + w + "»");
                d.WearOf[rune] = w; changed = true;
            }
            if (changed) Save(p, d);
        }

        private static void TellOpened(Player p, Data d, string rune)
        {
            RuneDef r = Def(rune);
            WearDef w = WearCatalog.Find(r.Kind, d.WearFor(rune));
            RavenMsg.Center(p, Lang.F("Руна загорілась: {0} — {1}", Lang.T(r.Name), w != null ? w.Name : Lang.T("її сила ще проявиться"))); // (no rune glyph: the game's font has none)
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
        }

        // a golden rune from a golden errand
        internal static void AddGold(Player p, int n)
        {
            if (p == null || n <= 0) return;
            Data d = Of(p);
            d.Gold += n; d.GoldEver += n;
            Save(p, d);
#if RAVEN_STORY
            StoryBus.Raise("gold", "", n);
#endif
            RavenMsg.Center(p, Lang.T("Одін дав тобі золоту руну. Вклади її в дар у книзі воронів, вкладка «Руни»."));
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
        }

        // ---------------------------------------------------------------- the в'язь
        // woven (the arena won): the runes go into it — off the ravens, taken while it lasts
        internal static void SetBind(Player p, Bind b, List<string> runes)
        {
            if (p == null || b == null) return;
            foreach (string r in runes) TakeOff(p, r, true);
            Data d = Of(p);
            d.Bind = b.Id();
            d.BindRunes.Clear();
            d.BindRunes.AddRange(runes);
            Save(p, d);
            Log("woven " + d.Bind + " from " + string.Join(",", runes.ToArray()));
            Changed(p);
        }

        // unwoven: off the raven, the runes free again
        internal static void Unweave(Player p)
        {
            Data d = Of(p);
            if (d.Bind.Length == 0) return;
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData it = Trust.Worn(m); if (it != null && RavenWear.Of(it).Bind == d.Bind) RavenWear.SetBind(it, ""); }
            Log("unwoven " + d.Bind);
            d.Bind = ""; d.BindRunes.Clear();
            Save(p, d);
            Changed(p);
        }

        // the в'язь on this raven (off the other one); null when done
        internal static string PutBind(Player p, bool munin)
        {
            Data d = Of(p);
            if (d.Bind.Length == 0) return Lang.T("У тебе ще нема в'язі.");
            string off = Shoulder.NotOnShoulder(p, munin);
            if (off != null) return off;
            ItemDrop.ItemData item = Trust.Worn(munin), other = Trust.Worn(!munin);
            if (other != null && RavenWear.Of(other).Bind == d.Bind)
            {
                if (Shoulder.NotOnShoulder(p, !munin) != null) return Lang.F("В'язь зараз на {0}, а він не на плечі.", munin ? Lang.T("Хугіні") : Lang.T("Муніні"));
                RavenWear.SetBind(other, "");
            }
            RavenWear.SetBind(item, d.Bind);
            Changed(p);
            return null;
        }

        internal static string TakeOffBind(Player p)
        {
            Data d = Of(p);
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item == null || d.Bind.Length == 0 || RavenWear.Of(item).Bind != d.Bind) continue;
                string off = Shoulder.NotOnShoulder(p, m);
                if (off != null) return off;
                RavenWear.SetBind(item, "");
            }
            Changed(p);
            return null;
        }

        // which raven wears your в'язь: 0 none, 1 Hugin, 2 Munin
        internal static int BindOn(Player p)
        {
            Data d = Of(p);
            if (d.Bind.Length == 0) return 0;
            if (RavenWear.Of(Trust.Worn(false)).Bind == d.Bind) return 1;
            if (RavenWear.Of(Trust.Worn(true)).Bind == d.Bind) return 2;
            return 0;
        }

        internal static void SpendGold(Player p)
        {
            Data d = Of(p);
            if (d.Gold <= 0) return;
            d.Gold--;
            Save(p, d);
        }

        internal static void Seen(Player p)
        {
            Data d = Of(p);
            if (d.New.Count == 0) return;
            d.New.Clear();
            Save(p, d);
        }

        // ---------------------------------------------------------------- on a raven
        internal static WearDef EffectOf(Data d, string rune)
        {
            RuneDef r = Def(rune);
            string w;
            return r != null && d.WearOf.TryGetValue(rune, out w) ? WearCatalog.Find(r.Kind, w) : null;
        }

        // which of your ravens wears this rune now: 0 none, 1 Hugin, 2 Munin
        internal static int WornBy(Player p, string rune)
        {
            Data d = Of(p);
            RuneDef r = Def(rune);
            string w;
            if (r == null || !d.WearOf.TryGetValue(rune, out w) || w.Length == 0) return 0;
            if (RavenWear.Of(Trust.Worn(false))[r.Kind] == w) return 1;
            if (RavenWear.Of(Trust.Worn(true))[r.Kind] == w) return 2;
            return 0;
        }

        // the rune hangs on this raven (off the other one, and in place of another rune of the same layer); null when done
        internal static string PutOn(Player p, string rune, bool munin)
        {
            Data d = Of(p);
            RuneDef r = Def(rune);
            if (r == null || !d.Has(rune)) return Lang.T("Ця руна ще не відкрита.");
            if (d.InBind(rune)) return Lang.T("Ця руна зараз у в'язі. Розплети в'язь, щоб вдягти її окремо.");
            string w = d.WearFor(rune);
            if (w.Length == 0 || WearCatalog.Find(r.Kind, w) == null) return Lang.T("Сила цієї руни ще не проявилась.");
            ItemDrop.ItemData item = Trust.Worn(munin), other = Trust.Worn(!munin);
            string off = Shoulder.NotOnShoulder(p, munin);
            if (off != null) return off;
            bool fromOther = other != null && RavenWear.Of(other)[r.Kind] == w;
            if (fromOther && Shoulder.NotOnShoulder(p, !munin) != null)
                return Lang.F("Ця руна зараз на {0}, а він не на плечі. Перевісиш, коли сяде.", munin ? Lang.T("Хугіні") : Lang.T("Муніні"));
            if (fromOther) RavenWear.Set(other, r.Kind, "");
            RavenWear.Set(item, r.Kind, w);
            Changed(p);
            return null;
        }

        // null when done (or nothing to take off), else why not
        internal static string TakeOff(Player p, string rune) { return TakeOff(p, rune, false); }

        private static string TakeOff(Player p, string rune, bool anywhere)
        {
            Data d = Of(p);
            RuneDef r = Def(rune);
            string w;
            if (r == null || !d.WearOf.TryGetValue(rune, out w) || w.Length == 0) return null;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item == null || RavenWear.Of(item)[r.Kind] != w) continue;
                string off = anywhere ? null : Shoulder.NotOnShoulder(p, m);
                if (off != null) return off;
                RavenWear.Set(item, r.Kind, "");
            }
            Changed(p);
            return null;
        }

        private static void Changed(Player p)
        {
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            RavenWear.Publish(p, hugin, munin);
            RavenWear.Dress(Shoulder.LocalModel(false), false, RavenWear.Code(hugin));
            RavenWear.Dress(Shoulder.LocalModel(true), true, RavenWear.Code(munin));
            Version++;
        }

        // a raven of yours wears only what your runes give (one given to you, or tried on from the console, loses the rest);
        // while tuning (admins) anything may be worn for trying
        // (asked twice a second by Publish: a code once found right is not looked at again until it or the runes change)
        private static readonly Dictionary<ItemDrop.ItemData, string> _kept = new Dictionary<ItemDrop.ItemData, string>();
        private static Data _keptFor;
        private static int _keptAt = -1;

        internal static void KeepOwn(Player p, ItemDrop.ItemData item)
        {
            if (p == null || item == null || Outfits.Tuning) return;
            string code = RavenWear.Code(item);
            if (code.Length == 0) return;
            Data d = Of(p);
            if (_keptFor != d || _keptAt != Version) { _kept.Clear(); _keptFor = d; _keptAt = Version; }
            string ok;
            if (_kept.TryGetValue(item, out ok) && ok == code) return;
            Wear w = RavenWear.Of(item);
            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                string id = w.Ids[k];
                if (id.Length == 0) continue;
                bool mine = false;
                foreach (string rune in d.Order) { RuneDef r = Def(rune); if (r != null && (int)r.Kind == k && d.WearFor(rune) == id && !d.InBind(rune)) { mine = true; break; } }
                if (!mine) { RavenWear.Set(item, (WearKind)k, ""); bool mun; RavenItem.IsRaven(item, out mun); Log(Plugin.RavenName(mun) + " took off «" + id + "»: no rune of yours gives it"); }
            }
            if (w.Bind.Length > 0 && w.Bind != d.Bind) { RavenWear.SetBind(item, ""); Log("a raven took off the в'язь «" + w.Bind + "»: not yours"); }
            if (_kept.Count > 8) _kept.Clear();
            _kept[item] = RavenWear.Code(item);
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Runes: " + s); }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven runes · raven runes notch [n] · raven runes open · raven runes gold [n] · raven runes star <rune> [0-3] · raven runes reset
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            int n = 1;
            if (a.Length > 2) int.TryParse(a[2], out n);
            Data d = Of(p);
            switch (what)
            {
                case "notch":
                    List<string> got = AddNotches(p, Mathf.Max(1, n));
                    return "Зарубок +" + Mathf.Max(1, n) + (got.Count > 0 ? ", відкрито: " + string.Join(", ", got.ToArray()) : "") + ". Різа " + Of(p).Notches + "/" + NotchesPerRune + ".";
                case "open":
                    List<string> one = AddNotches(p, NotchesPerRune - d.Notches);
                    return one.Count > 0 ? "Відкрито: " + one[0] + "." : "Усі руни вже відкриті.";
                case "star":
                    {
                        if (a.Length < 3 || !d.Has(a[2].ToLowerInvariant())) return "raven runes star <rune> [0-3] (руна має горіти)";
                        string rs = a[2].ToLowerInvariant();
                        int to; if (a.Length < 4 || !int.TryParse(a[3], out to)) to = d.StarsOf(rs) + 1;
                        d.Stars[rs] = Mathf.Clamp(to, 0, RuneStars.Max); Save(p, d);
                        return rs + ": " + RuneStars.StarText(d.StarsOf(rs)) + " — " + RuneStars.Plain(RuneStars.Describe(d, rs));
                    }
                case "gold":
                    d.Gold += Mathf.Max(1, n); d.GoldEver += Mathf.Max(1, n); Save(p, d);
                    return "Золотих рун: " + d.Gold + " (усього " + d.GoldEver + ").";
                case "reset":
                    foreach (string rune in new List<string>(d.Order)) TakeOff(p, rune, true);
                    Unweave(p);
                    p.m_customData.Remove(Key); _cache = null; _raw = null; Version++;
                    return "Руни скинуто.";
            }
            FillWaiting(p);
            d = Of(p);
            StringBuilder sb = new StringBuilder();
            sb.Append("Різа ").Append(d.Notches).Append('/').Append(NotchesPerRune).Append(", відкрито ").Append(d.Opened).Append('/').Append(All.Length)
              .Append(", шанс золотого ").Append(Mathf.RoundToInt(GoldChance(d.Opened) * 100f)).Append("%, золотих ").Append(d.Gold).Append(" (усього ").Append(d.GoldEver).Append(")");
            foreach (string rune in d.Order)
            {
                RuneDef r = Def(rune);
                int by = WornBy(p, rune);
                sb.Append("\n  ").Append(r != null ? r.Glyph + " " + r.Id : rune).Append(" → ").Append(d.WearFor(rune).Length > 0 ? d.WearFor(rune) : "(чекає)")
                  .Append(by == 1 ? " · на Хугіні" : by == 2 ? " · на Муніні" : "").Append(d.StarsOf(rune) > 0 ? " " + RuneStars.StarText(d.StarsOf(rune)) : "");
            }
            sb.Append("\nraven runes notch [n] | open | gold [n] | star <rune> [0-3] | reset");
            return sb.ToString();
        }
    }
}
#endif

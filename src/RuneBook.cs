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
            public readonly Dictionary<string, string> On = new Dictionary<string, string>();   // rune → the raven (Backup id) it hangs on; "-" none; missing = not yet known (before 2.7.0: the first raven seen wearing it)
            public readonly List<KeyValuePair<string, string>> Other = new List<KeyValuePair<string, string>>();

            internal int Opened { get { return Order.Count; } }
            internal string WearFor(string rune) { string w; return rune != null && WearOf.TryGetValue(rune, out w) ? w ?? "" : ""; }
            internal bool Has(string rune) { return WearOf.ContainsKey(rune); }
            internal bool InBind(string rune) { return Bind.Length > 0 && BindRunes.Contains(rune); }
            internal int StarsOf(string rune) { int n; return rune != null && Stars.TryGetValue(rune, out n) ? n : 0; }
            internal string HolderOf(string rune) { string h; return rune != null && On.TryGetValue(rune, out h) ? h : null; }

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
                        case "on":
                            foreach (string st in v.Split(','))
                            {
                                int c = st.IndexOf(':');
                                if (c > 0 && c < st.Length - 1) d.On[st.Substring(0, c)] = st.Substring(c + 1);
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
                first = true;
                foreach (string r in Order) { string h; if (!On.TryGetValue(r, out h) || string.IsNullOrEmpty(h)) continue; sb.Append(first ? ";on=" : ",").Append(r).Append(':').Append(h); first = false; }
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
            d.On[pick.Id] = "-";
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

        // runes opened while their layer's catalog had nothing free: they take one now (the book calls it on opening).
        // A rune whose effect is only missing from the catalog for now keeps it (a catalog without the tuning files:
        // not an admin, or not known yet — rerolling then would reroll a starred rune again and again); one whose effect
        // is gone for good (neither built in nor its file left) takes a new one, its sparks stay (they are the rune's).
        // A rune woven into a в'язь waits until it is unwoven: the в'язь is made of its effect
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
                if (had.Length > 0 && (!Gone(r.Kind, had) || d.InBind(rune))) continue;
                string w = PickWear(d, r.Kind);
                if (w.Length == 0) continue;
                if (had.Length > 0) Log("rune " + rune + ": its effect «" + had + "» is gone (no built-in, no file), takes «" + w + "»");
                d.WearOf[rune] = w; d.On[rune] = "-"; changed = true; // (the raven that wore the old effect does not wear the new one)
            }
            if (changed) Save(p, d);
        }

        private static bool Gone(WearKind k, string id)
        {
            if (k == WearKind.Freya) return !RavenLooks.IsBuiltin(id) && !RavenLooks.HasFile(id);
            return !FxLib.IsBuiltin(id) && !FxLib.HasFile(id) && (FxLib.Live == null || FxLib.Live.Id != id);
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
            RavenMsg.Center(p, Lang.T("Одін дав тобі золоту руну. Вклади її в дар у книзі воронів, вкладка «Образ»."));
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

        // unwoven (only at a tree): the runes free again
        internal static void Unweave(Player p)
        {
            Data d = Of(p);
            if (d.Bind.Length == 0) return;
            Log("unwoven " + d.Bind);
            d.Bind = ""; d.BindRunes.Clear();
            Save(p, d);
            Changed(p);
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

        // ---- one rune, one raven: the character keeps which raven ("h."/"m." + its Backup id) each rune hangs on. The
        // runes stay on that raven while it is yours in deed: on your shoulder, resting on a perch, at home or on guard,
        // even put away in a chest. They come off it only when
        //   · another raven of the same kind becomes your active one (put on that shoulder: Track),
        //   · it is lost (brought back from its copy at the stones, or gone to Odin from a broken perch: Lost),
        //   · you hang the rune on another raven or take it off in the book.
        // A raven that still carries an effect it no longer holds wears it no more: no sparks, no look, and it is taken
        // off it as soon as one of your games has it at hand.

        private const string ActiveH = "RavenWatch_runesActiveH", ActiveM = "RavenWatch_runesActiveM";

        // the raven as a rune holder: "h.<id>" / "m.<id>"; null before it has its id (its first moments)
        internal static string Tag(ItemDrop.ItemData item)
        {
            string id = Backup.Id(item);
            bool m;
            if (string.IsNullOrEmpty(id) || !RavenItem.IsRaven(item, out m)) return null;
            return (m ? "m." : "h.") + id;
        }
        private static string IdOf(string holder) { return holder != null && holder.Length > 2 && holder[1] == '.' ? holder.Substring(2) : holder; }
        private static bool MuninHolder(string holder) { return holder != null && holder.StartsWith("m.", System.StringComparison.Ordinal); }

        // the rune of yours that gives this effect of this layer (woven or not); null when none
        internal static string RuneOfEffect(Data d, WearKind k, string effect)
        {
            if (d == null || string.IsNullOrEmpty(effect)) return null;
            foreach (string rune in d.Order) { RuneDef r = Def(rune); if (r != null && r.Kind == k && d.WearFor(rune) == effect) return rune; }
            return null;
        }

        // this raven is the one the rune hangs on (asked without changing anything)
        internal static bool Held(Data d, string rune, ItemDrop.ItemData item)
        {
            if (d == null || rune == null || item == null || d.InBind(rune)) return false;
            string tag = Tag(item), h = d.HolderOf(rune);
            return tag == null || h == null || h == tag || h == IdOf(tag); // (no id yet — its first moments — or a rune hung before 2.7.0: as it was)
        }

        // the same, for your own game: a rune hung before 2.7.0 (whose raven is not yet known) goes to the first of your
        // ravens seen wearing it — the shoulders are looked at twice a second, so they come first
        internal static bool Holds(Player p, Data d, string rune, ItemDrop.ItemData item)
        {
            if (!Held(d, rune, item)) return false;
            string tag = Tag(item);
            if (p != null && ReferenceEquals(p, Player.m_localPlayer) && d.HolderOf(rune) == null && tag != null && Trust.IsOwner(item, p))
            {
                d.On[rune] = tag; Save(p, d);
                Log(rune + " is " + tag + "'s from now on");
            }
            return true;
        }

        // twice a second, with what your shoulders wear: a raven of that kind other than the last active one is put on —
        // it is your active one now, and the runes of every other raven of its kind come off them (free in the book)
        internal static void Track(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            if (p == null || !ReferenceEquals(p, Player.m_localPlayer)) return;
            Track(p, hugin, false);
            Track(p, munin, true);
        }

        private static void Track(Player p, ItemDrop.ItemData item, bool munin)
        {
            if (item == null || !Trust.IsOwner(item, p)) return; // (none on that shoulder, or someone else's raven: nothing changes)
            string tag = Tag(item);
            if (tag == null) return;
            string key = munin ? ActiveM : ActiveH, was;
            p.m_customData.TryGetValue(key, out was);
            if (was == tag) return;
            p.m_customData[key] = tag;
            if (string.IsNullOrEmpty(was)) return; // (the first look since 2.7.0: nothing known to take off)
            Data d = Of(p);
            List<string> off = new List<string>();
            foreach (string rune in d.Order)
            {
                string h = d.HolderOf(rune);
                if (h == null) // hung before 2.7.0: stays only if this raven wears it
                {
                    RuneDef r = Def(rune);
                    if (r != null && RavenWear.Of(item)[r.Kind] == d.WearFor(rune) && d.WearFor(rune).Length > 0) d.On[rune] = tag;
                    else if (MuninHolder(was) == munin) { d.On[rune] = "-"; off.Add(rune); }
                    continue;
                }
                if (h == "-" || h == tag || MuninHolder(h) != munin) continue;
                d.On[rune] = "-"; off.Add(rune);
            }
            Save(p, d);
            Log(Plugin.RavenName(munin) + " " + tag + " is the active one now" + (off.Count > 0 ? "; off the old one: " + string.Join(",", off.ToArray()) : ""));
            if (off.Count > 0) RavenMsg.Center(p, Lang.F("Тепер з тобою інший {0} — руни зійшли з попереднього.", Plugin.RavenName(munin)));
        }

        // a raven of yours is lost (brought back from its copy, or gone to Odin from a broken perch): its runes come off it
        internal static void Lost(Player p, string ravenId)
        {
            if (p == null || string.IsNullOrEmpty(ravenId)) return;
            Data d = Of(p);
            List<string> off = new List<string>();
            foreach (string rune in d.Order) { string h = d.HolderOf(rune); if (h != null && h != "-" && IdOf(h) == ravenId) { d.On[rune] = "-"; off.Add(rune); } }
            if (off.Count == 0) return;
            Save(p, d);
            Log("raven " + ravenId + " is lost: runes off it: " + string.Join(",", off.ToArray()));
        }

        // this raven's wear without what it no longer holds (to draw a raven that is not on your shoulder: at home, in a
        // grave); someone else's raven as it is (its owner's game sorts it out)
        internal static string ShownCode(ItemDrop.ItemData item, long owner)
        {
            string code = RavenWear.Code(item);
            Player p = Player.m_localPlayer;
            if (code.Length == 0 || p == null || owner != p.GetPlayerID() || Outfits.Tuning) return code;
            Data d = Of(p);
            Wear w = RavenWear.Of(item);
            bool cut = false;
            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                string id = w.Ids[k];
                if (id.Length == 0) continue;
                string rune = RuneOfEffect(d, (WearKind)k, id);
                if (rune == null || !Held(d, rune, item)) { w.Ids[k] = ""; cut = true; }
            }
            return cut ? w.Code() : code;
        }

        // which of your ravens wears this rune now: 0 none, 1 Hugin, 2 Munin
        internal static int WornBy(Player p, string rune)
        {
            Data d = Of(p);
            RuneDef r = Def(rune);
            string w;
            if (r == null || !d.WearOf.TryGetValue(rune, out w) || w.Length == 0) return 0;
            ItemDrop.ItemData h = Trust.Worn(false), m = Trust.Worn(true);
            if (h != null && RavenWear.Of(h)[r.Kind] == w && Held(d, rune, h)) return 1;
            if (m != null && RavenWear.Of(m)[r.Kind] == w && Held(d, rune, m)) return 2;
            return 0;
        }

        // where the rune hangs when not on a raven with you ("на Муніні вдома, «Жердина»"); null when on you or on none
        internal static string Elsewhere(Player p, string rune)
        {
            Data d = Of(p);
            string h = d.HolderOf(rune);
            if (string.IsNullOrEmpty(h) || h == "-" || d.InBind(rune) || WornBy(p, rune) != 0) return null;
            string id = IdOf(h);
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData it = Trust.Worn(m); if (it != null && Backup.Id(it) == id) return null; } // (on you, its look not yet in step)
            foreach (Home.Note n in Home.Notes(p))
                if (n.Rid == id) return Lang.F("на {0} {1}, «{2}»", n.Munin ? Lang.T("Муніні") : Lang.T("Хугіні"), Home.ModeWord(n.Mode), n.Perch);
            return Lang.T("на іншому твоєму вороні — не з тобою");
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
            string rid = Tag(item);
            if (string.IsNullOrEmpty(rid)) return Lang.F("{0} ще не освоївся — зачекай хвилину.", Plugin.RavenName(munin));
            bool fromOther = other != null && RavenWear.Of(other)[r.Kind] == w;
            if (fromOther && Shoulder.NotOnShoulder(p, !munin) != null)
                return Lang.F("Ця руна зараз на {0}, а він не на плечі. Перевісиш, коли сяде.", munin ? Lang.T("Хугіні") : Lang.T("Муніні"));
            if (fromOther) RavenWear.Set(other, r.Kind, "");
            string before = RuneOfEffect(d, r.Kind, RavenWear.Of(item)[r.Kind]); // the rune of this layer it wore till now: free again
            if (before != null && before != rune && (d.HolderOf(before) == rid || d.HolderOf(before) == IdOf(rid))) d.On[before] = "-";
            RavenWear.Set(item, r.Kind, w);
            d.On[rune] = rid; // (off whichever raven had it: at home, in a chest — that one wears it no more)
            Save(p, d);
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
            if (d.HolderOf(rune) != "-") { d.On[rune] = "-"; Save(p, d); } // (also off a raven not with you: it wears it no more)
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
                string rune = RuneOfEffect(d, (WearKind)k, id);
                if (rune == null || !Holds(p, d, rune, item))
                {
                    RavenWear.Set(item, (WearKind)k, "");
                    bool mun; RavenItem.IsRaven(item, out mun);
                    Log(Plugin.RavenName(mun) + " took off «" + id + "»: " + (rune == null ? "no rune of yours gives it" : d.InBind(rune) ? "its rune is in the в'язь" : "its rune hangs on another raven"));
                }
            }
            Wear now = RavenWear.Of(item);
            if (now.Code() != RavenWear.Code(item)) { RavenWear.Rewrite(item, now); Log("a raven's wear rewritten: a в'язь on it dropped (ravens wear none, it is the player's)"); }
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
                        return rs + ": " + RuneStars.SparkWord(d.StarsOf(rs)) + " — " + RuneStars.Plain(RuneStars.Describe(d, rs));
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
                  .Append(by == 1 ? " · на Хугіні" : by == 2 ? " · на Муніні" : "").Append(d.StarsOf(rune) > 0 ? " " + RuneStars.SparkWord(d.StarsOf(rune)) : "");
            }
            sb.Append("\nraven runes notch [n] | open | gold [n] | star <rune> [0-3] | reset");
            return sb.ToString();
        }
    }
}
#endif

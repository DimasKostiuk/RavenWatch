#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Іскра Одіна»: a modifier of an errand done may bring a spark in place of its notches; the spark falls on a random
    // rune that burns (and is not ★★★ yet) and gives it a star. A starred rune's effect, hung on a raven, works:
    //   Фрея     — on the raven itself (its strike, hunger, flight, trust, gifts), wherever it is;
    //   Бальдр   — on you in a fight, Геймдаль — on your legs: both only while that raven sits on your shoulder
    //              (one status «Руни» in the bar, made again only when what it gives changes).
    // ★ opens the effect, ★★ twice it, ★★★ three times, and some add a second thing. The power goes with the EFFECT a
    // rune took (the same rune gives different players different things). In a в'язь a rune gives nothing alone, but
    // every star of its runes makes the в'язь 10% stronger. Stars are never taken back.
    // One stat from two ravens (armour) adds up to SameStatCap at most (or the bigger one alone, if that is more).
    internal sealed class StarPassive
    {
        public string Id = "", Stat = "", Extra = "", Words = "", ExtraWords = "";
        public WearKind Kind;
        public float[] V;  // ★ ★★ ★★★
        public bool Flat;  // a number, not a share (carry weight)
    }

    internal static class RuneStars
    {
        internal const int Max = 3;
        internal static float SparkChance = 30f, SparkGolden = 40f; // % for each modifier of an errand done (a golden errand: more)
        internal static float SameStatCap = 0.25f, BindPerStar = 0.10f;
        internal static float IgniteShare = 0.2f, StealShare = 0.1f, HomeExtra = 0.2f, FedHealPct = 0.01f, FedHealEvery = 10f, SneakMul = 1.1f;

        internal static readonly Dictionary<string, StarPassive> All = new Dictionary<string, StarPassive>();

        private static void P(string id, WearKind k, string stat, float a, float b, float c, string words, string extra, string extraWords)
        {
            All[id] = new StarPassive { Id = id, Kind = k, Stat = stat, V = new[] { a, b, c }, Words = words, Extra = extra ?? "", ExtraWords = extraWords ?? "", Flat = stat == "carry" };
        }

        static RuneStars()
        {
            WearKind F = WearKind.Freya, B = WearKind.Baldr, H = WearKind.Heimdall;
            // Фрея: the raven
            P("f_sontse", F, "strike", 0.08f, 0.16f, 0.25f, Lang.K("удар ворона +{0}%"), "ignite", Lang.K("п'ята частина удару — вогнем, ціль горить"));
            P("f_inii", F, "hunger", 0.10f, 0.20f, 0.30f, Lang.K("ворон голодніє на {0}% повільніше"), "starvefree", Lang.K("голодуючи, б'є на повну силу"));
            P("f_burya", F, "fly", 0.10f, 0.20f, 0.30f, Lang.K("ворон літає на {0}% швидше"), "homefast", Lang.K("на плече повертається ще на 20% швидше"));
            P("f_moh", F, "trust", 0.10f, 0.20f, 0.30f, Lang.K("довіра росте на {0}% швидше"), "fedheal", Lang.K("ситий ворон на плечі лікує тебе на 1% здоров'я кожні 10 с"));
            P("f_popil", F, "cool", 0.05f, 0.10f, 0.15f, Lang.K("дари ворона перезаряджаються на {0}% швидше"), null, null);
            P("f_nich", F, "night", 0.10f, 0.20f, 0.30f, Lang.K("уночі дари ворона на {0}% сильніші"), "cave", Lang.K("і в печерах та підземеллях теж"));
            P("f_krov", F, "blood", 0.15f, 0.30f, 0.45f, Lang.K("удар ворона +{0}%, але голодніє на 20% швидше"), "steal", Lang.K("удар лікує тебе на 10% завданої шкоди"));
            P("f_zoloto", F, "range", 0.15f, 0.30f, 0.45f, Lang.K("дальність удару й розвідки ворона +{0}%"), null, null);
            // Бальдр: you in a fight
            P("b_zharyny", B, "dmg", 0.04f, 0.08f, 0.12f, Lang.K("шкода твоєї зброї +{0}%"), "fireres", Lang.K("опір вогню"));
            P("b_inii", B, "armor", 0.03f, 0.06f, 0.09f, Lang.K("броня +{0}%"), "frostres", Lang.K("опір морозу"));
            P("b_iskry", B, "atkstam", 0.05f, 0.10f, 0.15f, Lang.K("атака дешевша на {0}% (посох — ейтир, лук — і натяг)"), null, null);
            P("b_svitliachky", B, "regen", 0.10f, 0.20f, 0.30f, Lang.K("здоров'я відновлюється на {0}% швидше"), null, null);
            P("b_spory", B, "poison", 0.25f, 0.50f, 0.75f, Lang.K("отрута шкодить на {0}% менше"), null, null);
            P("b_dym", B, "noise", 0.15f, 0.30f, 0.45f, Lang.K("ти шумиш на {0}% тихіше"), "sneak", Lang.K("удар зі скритності +10%"));
            P("b_tuman", B, "leech", 0.01f, 0.02f, 0.03f, Lang.K("твої удари лікують на {0}% завданої шкоди"), null, null);
            P("b_runy", B, "guard", 0.04f, 0.08f, 0.12f, Lang.K("броня +{0}% (зі щитом — блок дешевший, з посохом — ейтир швидше)"), null, null);
            // Геймдаль: your legs
            P("h_vohon", H, "run", 0.05f, 0.10f, 0.15f, Lang.K("біг забирає на {0}% менше витривалості"), null, null);
            P("h_snih", H, "dodge", 0.10f, 0.20f, 0.30f, Lang.K("перекат дешевший на {0}%"), null, null);
            P("h_blyskavka", H, "speed", 0.03f, 0.06f, 0.09f, Lang.K("ти рухаєшся на {0}% швидше"), null, null);
            P("h_svitliachky", H, "stamregen", 0.10f, 0.20f, 0.30f, Lang.K("витривалість відновлюється на {0}% швидше"), null, null);
            P("h_dym", H, "crouch", 0.10f, 0.20f, 0.30f, Lang.K("у присяді ти швидший на {0}%"), null, null);
            P("h_bile_pirya", H, "fall", 0.25f, 0.50f, 0.75f, Lang.K("падіння шкодить на {0}% менше"), null, null);
            P("h_runy", H, "carry", 15f, 30f, 50f, Lang.K("вантаж +{0}"), null, null);
            P("h_pirya", H, "jump", 0.10f, 0.20f, 0.30f, Lang.K("стрибок вищий на {0}%"), null, null);
        }

        // ---------------------------------------------------------------- the numbers
        internal static float At(StarPassive s, int stars) { return s == null || stars <= 0 ? 0f : s.V[Mathf.Clamp(stars, 1, Max) - 1]; }
        // a rune's sparks in words: «1 іскра», «2 іскри» (the book draws them as little carved sparks, RavenBook.SparkRow)
        internal static string SparkWord(int n) { return n <= 0 ? "" : Lang.N(Mathf.Clamp(n, 1, Max), "іскра|іскри|іскор"); }
        private static string Num(StarPassive s, int stars) { float v = At(s, stars); return s.Flat ? Mathf.RoundToInt(v).ToString() : Mathf.RoundToInt(v * 100f).ToString(); }
        internal static string Words(StarPassive s, int stars) { return Lang.F(s.Words, Num(s, stars)); }
        internal static string Plain(string s) { return System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", ""); }

        // the stars of the rune of yours that gives this effect (0: none gives it, or no star)
        internal static int ForEffect(RuneBook.Data d, WearKind k, string effect)
        {
            if (d == null || string.IsNullOrEmpty(effect)) return 0;
            foreach (string rune in d.Order)
            {
                RuneDef r = RuneBook.Def(rune);
                if (r != null && r.Kind == k && d.WearFor(rune) == effect) return d.StarsOf(rune);
            }
            return 0;
        }

        // the same on one raven: only the raven the rune hangs on (a raven left at home with it, then the rune hung on
        // another, gets nothing from it), never while the rune is woven into the в'язь
        internal static int ForRaven(Player p, RuneBook.Data d, WearKind k, string effect, ItemDrop.ItemData item)
        {
            string rune = RuneBook.RuneOfEffect(d, k, effect);
            return rune != null && RuneBook.Holds(p, d, rune, item) ? d.StarsOf(rune) : 0;
        }

        internal static StarPassive OfRune(RuneBook.Data d, string rune)
        {
            RuneDef r = RuneBook.Def(rune);
            StarPassive s;
            return r != null && All.TryGetValue(d.WearFor(rune), out s) && s.Kind == r.Kind ? s : null;
        }

        // a rune's stars in words (the book's tip): what it gives now, the ladder, where it works
        internal static string Describe(RuneBook.Data d, string rune)
        {
            StarPassive s = OfRune(d, rune);
            if (s == null) return Lang.T("Сили від іскор ця руна не має: лише вигляд.");
            int n = d.StarsOf(rune);
            StringBuilder sb = new StringBuilder();
            if (n <= 0) sb.Append(Lang.T("Без іскор — лише вигляд. Іскри Одіна з доручень дадуть їй силу:"));
            else sb.Append("<color=#C9A227>").Append(SparkWord(n)).Append(":</color> ").Append(Words(s, n));
            sb.Append("\n<size=85%>");
            for (int i = 1; i <= Max; i++)
            {
                if (i > 1) sb.Append("  ·  ");
                bool now = i == n;
                sb.Append(now ? "<b>" : "").Append(SparkWord(i)).Append(' ').Append(s.Flat ? "+" : "").Append(Num(s, i)).Append(s.Flat ? "" : "%").Append(now ? "</b>" : "");
            }
            if (s.Extra.Length > 0) sb.Append('\n').Append(Lang.F("З {0} іскрами ще й: {1}", Max, Lang.T(s.ExtraWords)));
            sb.Append("</size>\n").Append(s.Kind == WearKind.Freya ? Lang.T("Діє, коли руна висить на вороні: на нього самого.") : Lang.T("Діє, коли руна висить на вороні, а він сидить у тебе на плечі."));
            if (n > 0) sb.Append('\n').Append(Lang.F("У в'язі сама не діє, зате кожна її іскра дає в'язі +{0}% сили.", Mathf.RoundToInt(BindPerStar * 100f)));
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the spark
        // a random burning rune with an effect, not yet ★★★, gets a star; null when there is none such
        // the runes a spark could land on now: open, not full, and a star there gives something (a power, or a stronger в'язь)
        private static List<string> Sparkable(RuneBook.Data d)
        {
            List<string> can = new List<string>();
            foreach (string rune in d.Order) if (d.StarsOf(rune) < Max && (OfRune(d, rune) != null || (d.InBind(rune) && d.WearFor(rune).Length > 0))) can.Add(rune);
            return can;
        }

        internal static bool CanSpark(Player p) { return p != null && Sparkable(RuneBook.Of(p)).Count > 0; }
        internal static bool CanSparkRune(Player p, string rune) { return p != null && Sparkable(RuneBook.Of(p)).Contains(rune); }

        internal static string Spark(Player p)
        {
            if (p == null) return null;
            RuneBook.Data d = RuneBook.Of(p);
            List<string> can = Sparkable(d);
            if (can.Count == 0) return null;
            return Light(p, d, can[UnityEngine.Random.Range(0, can.Count)], null);
        }

        // a spark on the rune you chose (by: whose, e.g. «Іскринки Хугіна» — Requests.Light); null when it cannot take one
        internal static string SparkOn(Player p, string rune, string by)
        {
            if (p == null) return null;
            RuneBook.Data d = RuneBook.Of(p);
            return Sparkable(d).Contains(rune) ? Light(p, d, rune, by) : null;
        }

        // by: who lit it (null: Odin's spark from an errand)
        private static string Light(Player p, RuneBook.Data d, string pick, string by)
        {
            int n = d.StarsOf(pick) + 1;
            d.Stars[pick] = n;
            RuneBook.Save(p, d);
            RuneDef r = RuneBook.Def(pick);
            StarPassive s = OfRune(d, pick);
            if (Plugin.Instance != null) Plugin.Instance.Log("Runes: a spark of Odin on " + pick + " → " + n + " star(s)");
            string rn = r != null ? Lang.T(r.Name) : pick, more = s != null ? ": " + Words(s, n) : "";
            string said = by == null ? Lang.F("Іскра Одіна впала на руну {0} — тепер у неї {1}{2}", rn, SparkWord(n), more)
                : Lang.F("{0} запалили руну {1} — тепер у неї {2}{3}", by, rn, SparkWord(n), more);
            RavenMsg.Center(p, said);
            p.Message(MessageHud.MessageType.TopLeft, said); // (kept in the corner too: a rune lit by the same errand takes the middle of the screen)
            RavenFx.Play("vfx_offering", p.transform.position + Vector3.up, Quaternion.identity);
            return pick;
        }

        // the stars of the runes woven into this в'язь (the в'язь grows stronger by them)
        internal static int BindStars(Bind b)
        {
            Player p = Player.m_localPlayer;
            if (b == null || p == null) return 0;
            RuneBook.Data d = RuneBook.Of(p);
            int n = 0;
            foreach (BindPart part in b.Parts) n += ForEffect(d, part.Kind, part.Wear);
            return n;
        }

        // ---------------------------------------------------------------- Фрея: on the raven itself
        private sealed class FreyaOf { public string Code; public RuneBook.Data D; public int Ver; public StarPassive S; public int Stars; }
        private static readonly Dictionary<ItemDrop.ItemData, FreyaOf> _freya = new Dictionary<ItemDrop.ItemData, FreyaOf>();

        // what this raven of yours wears in Фрея's layer, with its stars (asked often: kept until its wear or the runes change)
        private static StarPassive Freya(ItemDrop.ItemData item, out int stars)
        {
            stars = 0;
            Player p = Player.m_localPlayer;
            if (item == null || p == null) return null;
            string code = RavenWear.Code(item);
            if (code.Length == 0) return null;
            RuneBook.Data d = RuneBook.Of(p);
            FreyaOf c;
            if (!_freya.TryGetValue(item, out c) || c.Code != code || !ReferenceEquals(c.D, d) || c.Ver != RuneBook.Version)
            {
                if (_freya.Count > 8) _freya.Clear();
                c = new FreyaOf { Code = code, D = d, Ver = RuneBook.Version };
                if (!Trust.Stranger(item, p))
                {
                    string id = RavenWear.Of(item)[WearKind.Freya];
                    StarPassive s;
                    if (id.Length > 0 && All.TryGetValue(id, out s) && s.Kind == WearKind.Freya) { c.S = s; c.Stars = ForRaven(p, d, WearKind.Freya, id, item); }
                }
                _freya[item] = c;
            }
            stars = c.Stars;
            return c.Stars > 0 ? c.S : null;
        }

        private static float Raven(ItemDrop.ItemData item, string stat) { int n; StarPassive s = Freya(item, out n); return s != null && s.Stat == stat ? At(s, n) : 0f; }
        internal static bool Has(ItemDrop.ItemData item, string extra) { int n; StarPassive s = Freya(item, out n); return s != null && n >= Max && s.Extra == extra; }

        internal static float StrikePct(ItemDrop.ItemData item) { return (Raven(item, "strike") + Raven(item, "blood")) * 100f; }
        internal static float HungerMul(ItemDrop.ItemData item) { return (1f - Raven(item, "hunger")) * (Raven(item, "blood") > 0f ? 1.2f : 1f); }
        internal static float TrustMul(ItemDrop.ItemData item) { return 1f + Raven(item, "trust"); }
        internal static float CooldownMul(ItemDrop.ItemData item) { return 1f - Raven(item, "cool"); }
        internal static float RangeMul(ItemDrop.ItemData item) { return 1f + Raven(item, "range"); }
        internal static float FireShare(ItemDrop.ItemData item) { return Has(item, "ignite") ? IgniteShare : 0f; }
        internal static float Steal(ItemDrop.ItemData item) { return Has(item, "steal") ? StealShare : 0f; }
        internal static bool StarveFree(ItemDrop.ItemData item) { return Has(item, "starvefree"); }

        internal static float NightMul(ItemDrop.ItemData item)
        {
            float v = Raven(item, "night");
            if (v <= 0f) return 1f;
            Player p = Player.m_localPlayer;
            bool dark = (EnvMan.instance != null && EnvMan.IsNight()) || (Has(item, "cave") && p != null && p.InInterior());
            return dark ? 1f + v : 1f;
        }

        // a gift's pause, by the raven that has the gift
        internal static float CooldownMulOf(string gift)
        {
            RavenAbility a = RavenAbilities.Find(gift);
            return a != null ? CooldownMul(Trust.Worn(a.Munin)) : 1f;
        }

        // how fast it flies; home: «Буря» ★★★ more. Someone else's raven: as their game sent it (Wing's network key)
        internal static float FlyMul(Player owner, bool munin, bool home)
        {
            if (owner == null) return 1f;
            // (yours read from the same byte as the others read it: the very same speed in every game)
            int b = ReferenceEquals(owner, Player.m_localPlayer) ? StormByte(Trust.Worn(munin)) : Wing.StormOf(owner, munin);
            return b == 0 ? 1f : 1f + (b & 127) / 100f + (home && (b & 128) != 0 ? HomeExtra : 0f);
        }

        // «Буря» as a byte for the owner's ZDO (Wing.Publish): its speed-up in hundredths, the top bit its faster way home
        internal static int StormByte(ItemDrop.ItemData item)
        {
            float v = item != null ? Raven(item, "fly") : 0f;
            if (v <= 0f) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(v * 100f), 1, 127) | (Has(item, "homefast") ? 128 : 0);
        }

        // the strike's explanation (Strike.Explain): its line, when there is one
        internal static string WhyLine(ItemDrop.ItemData item)
        {
            int n; StarPassive s = Freya(item, out n);
            if (s == null || (s.Stat != "strike" && s.Stat != "blood")) return "";
            WearDef w = WearCatalog.Find(WearKind.Freya, s.Id);
            return Lang.F("+ руна «{0}» ({1}): +{2}%\n", w != null ? w.Name : s.Id, SparkWord(n), Mathf.RoundToInt(At(s, n) * 100f));
        }

        // ---------------------------------------------------------------- Бальдр, Геймдаль: on you (the status «Руни»)
        private static float _next, _healAt;
        private static string _key = "";
        private static Player _for;
        private static readonly StringBuilder _sb = new StringBuilder();
        private static readonly Dictionary<string, float> _sum = new Dictionary<string, float>(), _top = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> _n = new Dictionary<string, int>();
        private static readonly List<string> _extras = new List<string>(), _lines = new List<string>();
        internal static float Leech;   // read by the hit patch
        internal static float EitrCut, DrawCut; // «Іскри» with a staff / a bow in hand: read by the patches below

        // what you fight with now: 0 a weapon (or bare hands), 1 a staff, 2 a bow or crossbow; and a shield on the arm
        internal static int Style(Player p, out bool shield)
        {
            ItemDrop.ItemData left = p != null && LeftF != null ? LeftF.GetValue(p) as ItemDrop.ItemData : null, w = p != null ? p.GetCurrentWeapon() : null;
            shield = left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
            Skills.SkillType k = w != null ? w.m_shared.m_skillType : Skills.SkillType.None;
            return k == Skills.SkillType.ElementalMagic || k == Skills.SkillType.BloodMagic ? 1 : k == Skills.SkillType.Bows || k == Skills.SkillType.Crossbows ? 2 : 0;
        }
        private static readonly System.Reflection.FieldInfo LeftF = AccessTools.Field(typeof(Humanoid), "m_leftItem");
        private static int _style;
        private static bool _shield;
        internal static bool Sneak;
        internal static readonly int SeHash = SE_RavenRunes.Id.GetStableHashCode();

        // twice a second (yours): what the ravens on your shoulder give; the status made again only on a change
        internal static void Tick(Player p)
        {
            if (p == null || Time.time < _next) return;
            _next = Time.time + 0.5f;
            RuneBook.Data d = RuneBook.Of(p);
            _sum.Clear(); _top.Clear(); _n.Clear(); _extras.Clear(); _lines.Clear();
            _sb.Length = 0;
            bool heal = false;
            for (int i = 0; i < 2; i++)
            {
                bool m = i == 1;
                ItemDrop.ItemData it = Trust.Worn(m);
                if (it == null || Trust.Stranger(it, p) || Shoulder.NotOnShoulder(p, m) != null) continue;
                Wear w = RavenWear.Of(it);
                for (int k = (int)WearKind.Baldr; k <= (int)WearKind.Heimdall; k++)
                {
                    string id = w.Ids[k];
                    StarPassive s;
                    if (id.Length == 0 || !All.TryGetValue(id, out s) || (int)s.Kind != k) continue;
                    int st = ForRaven(p, d, (WearKind)k, id, it);
                    if (st <= 0) continue;
                    Add(s.Stat, At(s, st));
                    _lines.Add(SparkWord(st) + ": " + Words(s, st) + (st >= Max && s.Extra.Length > 0 ? "; " + Lang.T(s.ExtraWords) : ""));
                    if (st >= Max && s.Extra.Length > 0) _extras.Add(s.Extra);
                    _sb.Append(id).Append(st).Append(';');
                }
                if (Has(it, "fedheal")) { int z = Satiety.Zone(it); heal |= z == Satiety.Fed || z == Satiety.Stuffed; }
            }
            if (heal && !p.IsDead() && Time.time >= _healAt) { _healAt = Time.time + FedHealEvery; p.Heal(p.GetMaxHealth() * FedHealPct, true); } // «Мох» ★★★
            Leech = Get("leech");
            Sneak = _extras.Contains("sneak");
            _style = Style(p, out _shield);
            if (_sb.Length > 0) _sb.Append('|').Append(_style).Append(_shield ? 's' : '-'); // (another weapon in hand: the status made again)
            string key = _sb.ToString();
            if (key.Length == 0) EitrCut = DrawCut = 0f;
            SEMan se = p.GetSEMan();
            bool has = se.HaveStatusEffect(SeHash);
            if (key == _key && ReferenceEquals(p, _for) && has == (key.Length > 0)) return;
            _key = key; _for = p;
            if (has) se.RemoveStatusEffect(SeHash, true);
            if (key.Length == 0) return;
            se.AddStatusEffect(Make(), true, 0, 0f);
        }

        private static void Add(string stat, float v)
        {
            float had; int n;
            _sum[stat] = (_sum.TryGetValue(stat, out had) ? had : 0f) + v;
            _top[stat] = Mathf.Max(_top.TryGetValue(stat, out had) ? had : 0f, v);
            _n[stat] = (_n.TryGetValue(stat, out n) ? n : 0) + 1;
        }

        // a stat as it works: one from two ravens adds up to the cap at most (or the bigger one, if that is more)
        private static float Get(string stat)
        {
            float v; int n;
            if (!_sum.TryGetValue(stat, out v)) return 0f;
            if (_n.TryGetValue(stat, out n) && n >= 2) v = Mathf.Min(v, Mathf.Max(SameStatCap, _top[stat]));
            return v;
        }

        private static HitData.DamageModifier Resist(float v) { return v >= 0.75f ? HitData.DamageModifier.VeryResistant : v >= 0.5f ? HitData.DamageModifier.Resistant : HitData.DamageModifier.SlightlyResistant; }

        // one kept template, every field set anew (the game adds a shallow copy of it: a new object each time would pile up)
        private static SE_RavenRunes _tpl;
        private static SE_RavenRunes Make()
        {
            if (_tpl == null) { _tpl = ScriptableObject.CreateInstance<SE_RavenRunes>(); _tpl.name = SE_RavenRunes.Id; _tpl.hideFlags = HideFlags.HideAndDontSave; }
            SE_RavenRunes s = _tpl;
            s.m_name = Lang.W("Руни");
            s.m_tooltip = Lang.T("Що дають руни з іскрами на воронах у тебе на плечі:") + "\n" + string.Join("\n", _lines.ToArray());
            s.m_icon = AbilityIcons.Get("stone");
            s.m_ttl = 0f;
            float dmg = Get("dmg");
            s.m_modifyAttackSkill = dmg > 0f ? Skills.SkillType.All : Skills.SkillType.None; s.m_damageModifier = 1f + dmg;
            // «Руни» of Бальдр: armour; with a shield, cheaper blocks; with a staff, eitr back sooner
            float guard = Get("guard");
            s.m_armorMultiplier = Get("armor") + (!_shield && _style != 1 ? guard : 0f);
            s.m_blockStaminaUseModifier = _shield ? -guard : 0f;
            s.m_eitrRegenMultiplier = !_shield && _style == 1 ? 1f + guard : 1f;
            // «Іскри»: a cheaper attack — stamina; with a staff its eitr; with a bow the draw too
            float atk = Get("atkstam");
            s.m_attackStaminaUseModifier = _style == 1 ? 0f : -atk;
            EitrCut = _style == 1 ? atk : 0f;
            DrawCut = _style == 2 ? atk : 0f;
            s.m_healthRegenMultiplier = 1f + Get("regen");
            s.m_noiseModifier = -Get("noise");
            s.m_runStaminaDrainModifier = -Get("run");
            s.m_dodgeStaminaUseModifier = -Get("dodge");
            s.m_speedModifier = Get("speed");
            s.m_staminaRegenMultiplier = 1f + Get("stamregen");
            s.Crouch = Get("crouch");
            s.m_fallDamageModifier = -Mathf.Min(1f, Get("fall"));
            s.m_addMaxCarryWeight = Get("carry");
            float jump = Get("jump");
            s.m_jumpModifier = new Vector3(0f, jump, 0f);
            s.m_mods = new List<HitData.DamageModPair>();
            float poison = Get("poison");
            if (poison > 0f) s.m_mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Poison, m_modifier = Resist(poison) });
            if (_extras.Contains("fireres")) s.m_mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Fire, m_modifier = HitData.DamageModifier.Resistant });
            if (_extras.Contains("frostres")) s.m_mods.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant });
            return s;
        }
    }

    // «Іскри» with a staff in hand: its spells take less eitr
    [HarmonyPatch(typeof(Attack), "GetAttackEitr")]
    internal static class RuneEitrPatch
    {
        private static readonly System.Reflection.FieldInfo Who = AccessTools.Field(typeof(Attack), "m_character"); // (whose attack: looked up once; gone in a game update — the rune just does not cut, and the log says so)
        private static bool _told;
        private static void Postfix(Attack __instance, ref float __result)
        {
            if (RuneStars.EitrCut <= 0f) return;
            if (Who == null) { if (!_told && Plugin.Instance != null) { _told = true; Plugin.Instance.Log("Runes: no Attack.m_character in this game: «Іскри» cannot cut a staff's eitr"); } return; }
            if (ReferenceEquals(Who.GetValue(__instance), Player.m_localPlayer)) __result *= 1f - RuneStars.EitrCut;
        }
    }

    // «Іскри» with a bow in hand: drawing it takes less stamina
    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetDrawStaminaDrain")]
    internal static class RuneDrawPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            Player me = Player.m_localPlayer;
            if (RuneStars.DrawCut > 0f && me != null && ReferenceEquals(me.GetCurrentWeapon(), __instance)) __result *= 1f - RuneStars.DrawCut;
        }
    }

    // the runes' status in the bar: its own words only (not the game's list of numbers), and the speed in a crouch
    internal sealed class SE_RavenRunes : SE_Stats
    {
        internal const string Id = "RavenWatch_Runes";
        public float Crouch;

        public override string GetTooltipString() { return m_tooltip; }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            base.ModifySpeed(baseSpeed, ref speed, character, dir);
            if (Crouch > 0f && m_character != null && m_character.IsCrouching()) speed += baseSpeed * Crouch;
        }
    }

    // your hits: «Кривавий туман» heals you by a share of them, «Дим» ★★★ makes a hit from stealth stronger
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class RuneStarsHitPatch
    {
        private static int _leechFrame = -1;

        private static void Prefix(Character __instance, HitData hit)
        { try {
            if ((RuneStars.Leech <= 0f && !RuneStars.Sneak) || hit == null || BindRun.Ours) return; // (every hit of this game passes here)
            Player me = Player.m_localPlayer;
            if (me == null || ReferenceEquals(__instance, me) || hit.m_attacker != me.GetZDOID() || hit.m_skillRaiseAmount <= Strike.Marker) return; // (yours, not a raven's blow)
            if (__instance.IsPlayer() || __instance.IsTamed()) return;
            if (RuneStars.Sneak && hit.m_backstabBonus > 1f) { BaseAI ai = __instance.GetBaseAI(); if (ai != null && !ai.IsAlerted()) hit.m_damage.Modify(RuneStars.SneakMul); }
            if (RuneStars.Leech > 0f && !me.IsDead() && Time.frameCount != _leechFrame) // (once a swing, not once for every one it hits: a swing's hits land in one frame)
            {
                HitData h = hit.Clone(); // what gets through its resistances (immune: nothing), no chopping or mining
                h.m_damage.m_chop = 0f; h.m_damage.m_pickaxe = 0f;
                HitData.DamageModifier mod;
                h.ApplyResistance(__instance.GetDamageModifiers(), out mod);
                float dealt = h.m_damage.GetTotalDamage();
                if (dealt > 0f) { me.Heal(dealt * RuneStars.Leech, true); _leechFrame = Time.frameCount; }
            }
        } catch (Exception e) { PatchGuard.Fail("RuneStarsHitPatch", e); } }
    }
}
#endif

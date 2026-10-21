#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «В'язь» (binds-plan.md): two to four burning runes woven at a raven tree into one thing — a look on the raven and
    // a power. The power is a sentence: runes of Бальдр say WHAT happens, runes of Геймдаль WHEN, runes of Фрея HOW.
    // A player has one в'язь at most; the runes in it are taken (none of them is worn alone while it lasts).
    // Its id is its recipe — x~<tree>~<f|b|h>.<effect id>~… (the roots' places in order) — so every game rebuilds the
    // same look and power from the id alone; the player's record keeps which runes went into it.
    internal sealed class BindPart
    {
        public WearKind Kind;
        public string Wear = "";
        internal Element El { get { return Elements.Of(Kind, Wear); } }
    }

    internal sealed class Bind
    {
        internal const string Prefix = "x~";
        public int Tree; // RavenTree kind: 0 icy, 1 green, 2 charred
        public readonly List<BindPart> Parts = new List<BindPart>();

        // what each tree holds: green 2 runes, icy 3, charred 4; the trust one raven of yours needs; the charges
        internal static int Slots(int tree) { return tree == 1 ? 2 : tree == 0 ? 3 : 4; }
        internal static int TrustNeed(int tree) { return tree == 1 ? 2 : tree == 0 ? 4 : 6; }
        internal static int ChargesOf(int tree) { return Slots(tree); }

        // the names of the roots' places: where each rune lies on the raven (the tree's way of weaving)
        internal static readonly string[][] PlaceNames =
        {
            Lang.Keys("Серце", "Середина", "Оболонка"),
            Lang.Keys("Перша", "Друга"),
            Lang.Keys("Хвіст", "Ліве крило", "Праве крило", "Спина"),
        };

        internal string Id()
        {
            StringBuilder sb = new StringBuilder(Prefix).Append(Tree);
            foreach (BindPart p in Parts) sb.Append('~').Append(WearCatalog.KindIds[(int)p.Kind]).Append('.').Append(p.Wear);
            return sb.ToString();
        }

        internal static Bind Parse(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(Prefix, System.StringComparison.Ordinal)) return null;
            string[] a = id.Split('~');
            int tree;
            if (a.Length < 3 || !int.TryParse(a[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out tree) || tree < 0 || tree > 2) return null;
            Bind b = new Bind { Tree = tree };
            for (int i = 2; i < a.Length; i++)
            {
                int dot = a[i].IndexOf('.');
                WearKind k;
                if (dot <= 0 || !WearCatalog.Kind(a[i].Substring(0, dot), out k)) return null;
                b.Parts.Add(new BindPart { Kind = k, Wear = a[i].Substring(dot + 1) });
            }
            return b.Parts.Count > 0 ? b : null;
        }

        // the runes laid on the roots (in their places) at a tree of this kind
        internal static Bind From(RuneBook.Data d, List<string> runes, int tree)
        {
            Bind b = new Bind { Tree = tree };
            foreach (string r in runes)
            {
                RuneDef rd = RuneBook.Def(r);
                if (rd == null) continue;
                b.Parts.Add(new BindPart { Kind = rd.Kind, Wear = d.WearFor(r) });
            }
            return b;
        }

        internal int Count(WearKind k) { int n = 0; foreach (BindPart p in Parts) if (p.Kind == k) n++; return n; }

        // null when it can be woven; else why not
        internal string Problem()
        {
            if (Parts.Count < 2) return Lang.T("Поклади на коріння щонайменше дві руни.");
            if (Parts.Count > Slots(Tree)) return Lang.F("На цьому дереві вміщається {0} рун(и).", Slots(Tree));
            foreach (BindPart p in Parts) if (p.Wear.Length == 0 || WearCatalog.Find(p.Kind, p.Wear) == null) return Lang.T("Одна з рун ще не знайшла свого ефекту.");
            for (int i = 0; i < Parts.Count; i++) for (int j = i + 1; j < Parts.Count; j++) if (Parts[i].Kind == Parts[j].Kind && Parts[i].Wear == Parts[j].Wear) return Lang.T("Одна руна — лише на одне місце.");
            if (Count(WearKind.Baldr) + Count(WearKind.Heimdall) == 0) return Lang.T("Фрея лише підсилює. Додай руну Бальдра (що станеться) або Геймдаля (коли).");
            return null;
        }
    }

    // ---------------------------------------------------------------- the power: the sentence
    internal enum BindMode { Auto, Button, Motion }

    internal sealed class BindPower
    {
        // what a rune of each layer does (by its catalog id; a new effect: by its element)
        internal static readonly string[] WhatByEl = { "flash", "frost", "chain", "heal", "vanish", "leech", "shield", "gust" };
        internal static readonly string[] WhenByEl = { "sprint", "dodge", "heavy", "lowhp", "sneak", "kill", "parry", "land" };
        internal static readonly string[] HowByEl = { "radius", "duration", "double", "allyheal", "recharge", "blood", "power", "recharge" };
        private static readonly Dictionary<string, string> Own = new Dictionary<string, string> { { "b_spory", "poison" }, { "f_nich", "night" } };
        // a в'язь of Бальдр alone is a button: its FIRST rune gives a great power of its own (the others add their small
        // effects); by the rune's effect, a new effect by its element
        internal static readonly string[] ActiveByEl = { "sun", "crown", "hammer", "light", "shadow", "sacrifice", "runeshield", "sun" };
        private static readonly Dictionary<string, string> OwnActive = new Dictionary<string, string>
        {
            { "b_zharyny", "sun" }, { "b_inii", "crown" }, { "b_iskry", "hammer" }, { "b_svitliachky", "light" },
            { "b_spory", "hel" }, { "b_dym", "shadow" }, { "b_tuman", "sacrifice" }, { "b_runy", "runeshield" },
        };

        internal static string ActiveOf(BindPart p)
        {
            string a;
            if (OwnActive.TryGetValue(p.Wear, out a)) return a;
            Element e = p.El;
            return ActiveByEl[(int)(e == Element.None ? Element.Fire : e)];
        }

        public string Active = "";       // the button's great power (Mode Button), "" none
        public string ActiveFx = "";     // its rune's effect id: the power is shown with that rune's own effect

        public BindMode Mode;
        public readonly List<string> What = new List<string>(), When = new List<string>(), How = new List<string>();
        public Element BonusEl = Element.None;
        public int BonusSteps;            // runes of one element past the first (at most 3)
        public int Charges;
        public float Recharge;            // s for one charge
        public float Pow = 1f, Radius = 1f, Dur = 1f, Instant = 1f, Personal = 1f, Double;
        public bool AllyHeal, Night, BloodCost;
        public int Targets = 3;
        public int Stars;                 // the stars of its runes (RuneStars): each makes it stronger

        internal static string ActionOf(BindPart p)
        {
            string a;
            if (Own.TryGetValue(p.Wear, out a)) return a;
            Element e = p.El;
            if (e == Element.None) e = Element.Fire;
            return (p.Kind == WearKind.Baldr ? WhatByEl : p.Kind == WearKind.Heimdall ? WhenByEl : HowByEl)[(int)e];
        }

        internal static bool Offensive(string what) { return what == "flash" || what == "frost" || what == "chain" || what == "poison" || what == "gust"; }

        internal static BindPower Of(Bind b)
        {
            BindPower w = new BindPower();
            int[] els = new int[Elements.Count];
            foreach (BindPart p in b.Parts)
            {
                string a = ActionOf(p);
                if (p.Kind == WearKind.Baldr) w.What.Add(a); else if (p.Kind == WearKind.Heimdall) { if (!w.When.Contains(a)) w.When.Add(a); } else w.How.Add(a);
                Element e = p.El;
                if (e != Element.None) els[(int)e]++;
            }
            w.Mode = w.What.Count > 0 && w.When.Count > 0 ? BindMode.Auto : w.What.Count > 0 ? BindMode.Button : BindMode.Motion;
            if (w.Mode == BindMode.Button)
                foreach (BindPart p in b.Parts)
                    if (p.Kind == WearKind.Baldr) { w.Active = ActiveOf(p); w.ActiveFx = p.Wear; w.What.RemoveAt(0); break; } // (the first one: its small effect gives way to the great power)
            for (int i = 0; i < els.Length; i++) if (els[i] >= 2 && (w.BonusEl == Element.None || els[i] > els[(int)w.BonusEl])) w.BonusEl = (Element)i;
            w.BonusSteps = w.BonusEl == Element.None ? 0 : Mathf.Min(3, els[(int)w.BonusEl] - 1);
            w.Charges = Bind.ChargesOf(b.Tree);

            float rech = w.Mode == BindMode.Motion ? 20f : w.Mode == BindMode.Button ? 90f + 15f * w.What.Count : 40f + 15f * Mathf.Max(0, w.What.Count - 1);
            w.Pow = 1f + 0.3f * w.BonusSteps;
            foreach (string h in w.How)
                switch (h)
                {
                    case "radius": w.Radius *= 1.5f; w.Personal *= 1.25f; break;
                    case "duration": w.Dur *= 1.5f; w.Instant *= 1.25f; break;
                    case "double": w.Double = Mathf.Min(0.75f, w.Double + 0.25f); break;
                    case "allyheal": w.AllyHeal = true; break;
                    case "recharge": rech *= 0.7f; break;
                    case "night": w.Night = true; break;
                    case "blood": w.BloodCost = true; w.Pow *= 2f; break;
                    case "power": w.Pow *= 1.3f; break;
                }
            w.Stars = RuneStars.BindStars(b);
            w.Pow *= 1f + RuneStars.BindPerStar * w.Stars; // (every star of its runes: +10%)
            w.Targets = Mathf.RoundToInt(3f * w.Radius);
            rech *= 1f - 0.15f * Mathf.Max(0, w.When.Count - 1);
            rech *= 1f - 0.2f * w.BonusSteps;
            w.Recharge = Mathf.Max(8f, rech);
            return w;
        }

        // ---------------------------------------------------------------- in words (the tree, the book)
        internal static readonly Dictionary<string, string> WhenWords = new Dictionary<string, string>
        {
            { "sprint", Lang.K("біжиш 3 с без зупинки") }, { "dodge", Lang.K("ухиляєшся перекатом") }, { "heavy", Lang.K("влучаєш сильною атакою або стрілою") },
            { "lowhp", Lang.K("здоров'я падає нижче 30%") }, { "sneak", Lang.K("б'єш зі скритності") }, { "kill", Lang.K("вбиваєш ворога") },
            { "parry", Lang.K("відбиваєш удар щитом") }, { "land", Lang.K("приземляєшся з висоти") },
        };

        internal string WhatWords(string a, float night)
        {
            float p = Pow * night;
            switch (a)
            {
                case "flash": return Lang.F("спалах вогню довкола ({0} м): {1}% шкоди зброї + підпал", N(4f * Radius), Pct(40f * p * Instant));
                case "frost": return Lang.F("морозна хвиля ({0} м): {1}% шкоди зброї морозом, вороги повільніші {2} с", N(5f * Radius), Pct(15f * p), N(4f * Dur));
                case "chain": return Lang.F("розряд у {0} найближчих ворогів: кожному {1}% шкоди зброї", Targets, Pct(50f * p * Instant));
                case "heal": return Lang.F("лікує {0}% здоров'я одразу й ще {1}% за {2} с", Pct(15f * p * Personal), Pct(10f * p * Personal), N(5f * Dur));
                case "poison": return Lang.F("хмара отрути ({0} м): {1}% шкоди зброї отрутою", N(4f * Radius), Pct(60f * p));
                case "vanish": return Lang.F("вороги довкола губляють тебе на {0} с", N(3f * Dur));
                case "leech": return Lang.F("{0} наступні удари (за {1} с) лікують на {2}% шкоди", 3, N(8f * Dur), Pct(15f * p * Personal));
                case "shield": return Lang.F("щит на {0}% твого здоров'я, {1} с", Pct(20f * p * Personal), N(6f * Dur));
                case "gust": return Lang.F("вихор відкидає ворогів ({0} м), {1}% шкоди зброї", N(4f * Radius), Pct(10f * p * Instant));
            }
            return a;
        }

        // the great power of a button, in words
        internal static readonly Dictionary<string, string> ActiveNames = new Dictionary<string, string>
        {
            { "sun", Lang.K("Сонце Бальдра") }, { "crown", Lang.K("Крижаний вінець") }, { "hammer", Lang.K("Молот Тора") }, { "light", Lang.K("Світло Бальдра") },
            { "hel", Lang.K("Подих Хель") }, { "shadow", Lang.K("Тінь Одіна") }, { "sacrifice", Lang.K("Кривава жертва") }, { "runeshield", Lang.K("Рунний щит") },
        };

        internal string ActiveWords(string a, float night)
        {
            float p = Pow * night;
            switch (a)
            {
                case "sun": return Lang.F("вибух вогню ({0} м): {1}% шкоди зброї, вороги збиті з ніг", N(6f * Radius), Pct(120f * p * Instant));
                case "crown": return Lang.F("кільце льоду ({0} м): {1}% шкоди зброї морозом, вороги збиті з ніг і сповільнені", N(8f * Radius), Pct(30f * p));
                case "hammer": return Lang.F("блискавка в ціль під прицілом і ще в {0} ворогів поруч, кожному {1}% шкоди зброї", Targets, Pct(100f * p * Instant));
                case "light": return Lang.F("сяйво на {0} с ({1} м): ти й союзники відновлюєте {2}% здоров'я щосекунди, вороги б'ють тебе на 20% слабше", N(8f * Dur), N(8f * Radius), Pct(3f * p * Personal));
                case "hel": return Lang.F("поле отрути на {0} с ({1} м): разом {2}% шкоди зброї кожному ворогу в ньому", N(6f * Dur), N(6f * Radius), Pct(120f * p));
                case "shadow": return Lang.F("{0} с вороги тебе не бачать, перший удар з тіні — удвічі сильніший", N(5f * Dur));
                case "sacrifice": return Lang.F("віддаєш 20% здоров'я; {0} с усі удари лікують на {1}% шкоди", N(10f * Dur), Pct(25f * p * Personal));
                case "runeshield": return Lang.F("щит на {0}% здоров'я тобі й союзникам поруч, {1} с", Pct(30f * p * Personal), N(8f * Dur));
            }
            return a;
        }

        internal string ActiveTitle() { string n; return ActiveNames.TryGetValue(Active, out n) ? Lang.T(n) : Active; }

        internal static readonly Dictionary<string, string> HowWordsMap = new Dictionary<string, string>
        {
            { "radius", Lang.K("радіус і кількість цілей +50%, особистим ефектам +25%") }, { "duration", Lang.K("діє на 50% довше, миттєвим ефектам +25%") },
            { "double", Lang.K("25% шанс спрацювати двічі") }, { "allyheal", Lang.K("кожне спрацювання лікує тебе й союзників поруч на 5%") },
            { "recharge", Lang.K("заряди відновлюються на 30% швидше") }, { "night", Lang.K("уночі й у печерах на 60% сильніше") },
            { "blood", Lang.K("сила ×2, але кожне спрацювання забирає 10% здоров'я (не нижче 30%)") }, { "power", Lang.K("сила +30%") },
        };

        // the whole sentence, rich text (one line per part)
        internal string Sentence()
        {
            StringBuilder sb = new StringBuilder();
            List<string> what = new List<string>();
            foreach (string a in What) what.Add(WhatWords(a, 1f));
            List<string> when = new List<string>();
            foreach (string t in When) { string s; when.Add(WhenWords.TryGetValue(t, out s) ? Lang.T(s) : t); }
            if (Mode == BindMode.Auto) sb.Append(Lang.F("<b>Коли</b> {0} — {1}.", string.Join(Lang.T(" або "), when.ToArray()), string.Join("; ", what.ToArray())));
            else if (Mode == BindMode.Button)
            {
                sb.Append(Lang.F("<b>Кнопка в колі дарів (Q) — {0}:</b> {1}.", ActiveTitle(), ActiveWords(Active, 1f)));
                if (what.Count > 0) sb.Append('\n').Append(Lang.F("Разом із нею: {0}.", string.Join("; ", what.ToArray())));
            }
            else sb.Append(Lang.F("<b>Коли</b> {0} — {1}.", string.Join(Lang.T(" або "), when.ToArray()), Lang.T("+25% до швидкості й удвічі менше витривалості на біг 4 с")));
            foreach (string h in How) { string s; sb.Append('\n').Append(Lang.T("Фрея:")).Append(' ').Append(HowWordsMap.TryGetValue(h, out s) ? Lang.T(s) : h).Append('.'); }
            if (BonusSteps > 0) sb.Append('\n').Append(Lang.F("Бонус стихії: {0} ×{1} — сила +{2}%, заряди на {3}% швидше.", Elements.Colored(BonusEl), BonusSteps + 1, BonusSteps * 30, BonusSteps * 20));
            if (Stars > 0) sb.Append('\n').Append(Lang.F("Зірки рун: {0} — сила +{1}%.", Stars, Mathf.RoundToInt(Stars * RuneStars.BindPerStar * 100f)));
            sb.Append('\n').Append(Lang.F("Зарядів: {0}, кожен відновлюється за {1} с.", Charges, Mathf.RoundToInt(Recharge)));
            return sb.ToString();
        }

        private static string N(float v) { return v.ToString(Mathf.Abs(v - Mathf.Round(v)) < 0.05f ? "0" : "0.#", CultureInfo.InvariantCulture); }
        private static string Pct(float v) { return Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture); }
    }

    // ---------------------------------------------------------------- the look: what a в'язь puts on a raven, by layer
    internal static class BindLook
    {
        internal sealed class Visual
        {
            public Look Look;                                         // Фрея (null: the layer is not the в'язь's)
            public readonly List<RavenFxDef>[] Fx = new List<RavenFxDef>[WearCatalog.Kinds];
            internal bool Has(WearKind k) { return k == WearKind.Freya ? Look != null : Fx[(int)k] != null && Fx[(int)k].Count > 0; }
        }

        private static readonly Dictionary<string, Visual> Cache = new Dictionary<string, Visual>();
        private static int _stamp = -1;

        internal static Visual Of(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int stamp = WearCatalog.Stamp;
            if (stamp != _stamp) { Cache.Clear(); _stamp = stamp; }
            Visual v;
            if (Cache.TryGetValue(id, out v)) return v;
            v = null;
            try { v = Make(Bind.Parse(id), id); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Raven bind look " + id + ": " + e.Message); }
            Cache[id] = v;
            return v;
        }

        // the runes of one layer woven the tree's way (FxMix: green together, icy core and shell, charred over the body)
        private static Visual Make(Bind b, string id)
        {
            if (b == null) return null;
            Visual v = new Visual();
            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                List<string> ids = new List<string>();
                foreach (BindPart p in b.Parts) if ((int)p.Kind == k && p.Wear.Length > 0) ids.Add(p.Wear);
                if (ids.Count == 0) continue;
                MixDef m = new MixDef { Id = id + "_" + WearCatalog.KindIds[k], Name = "", Kind = (WearKind)k, Mode = FxMix.ModeForTree(b.Tree), Harmony = k != (int)WearKind.Freya };
                foreach (string s in ids) { m.Of.Add(s); m.Weight.Add(1f); }
                if (k == (int)WearKind.Freya) v.Look = FxMix.LookOf(m);
                else v.Fx[k] = FxMix.Parts(m);
            }
            return v;
        }
    }
}
#endif

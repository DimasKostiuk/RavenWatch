#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Шлях Одіна»: the left page — the errand (RavenBookNest); the right page — every rune you have, like a shelf of
    // deeds: the stick of notches, the 24 runes in three rows (Фрея, Бальдр, Геймдаль), grey until they open; the golden
    // runes (their own row once the first one is got, the ones put into gifts too) and the в'язі of the raven trees.
    // A click on an open rune: on which raven to hang it (or take it off). What a raven wears is also in «Образ»: the
    // rune places round its window (RuneSlot here). The mixing is at the tree itself (RavenTreeWindow).
    internal static partial class RavenBook
    {
        private static readonly List<string> _freshRunes = new List<string>(); // opened since last seen: marked while the tab stays open
        private static int _runesDrawnFor = -1;

        private static void PageRunes()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            ErrandSide(p);
            RuneBook.FillWaiting(p);
            RuneBook.Data d = RuneBook.Of(p);
            foreach (string r in d.New) if (!_freshRunes.Contains(r)) _freshRunes.Add(r);
            RuneBook.Seen(p);

            // ---- right: the runes, drawn tighter step by step until the page fits with no scroll (more golden runes, a в'язь)
            for (_runeTight = 0; ; _runeTight++)
            {
                RectTransform col = RunesRight(p, d);
                if (_runeTight >= RuneTightMax || RunesFit(col)) break;
                UnityEngine.Object.DestroyImmediate(col.parent.gameObject);
            }
            _runeTight = 0;
        }

        // how tight the right page is drawn: 0 roomy … RuneTightMax; the sizes for each step
        private static int _runeTight;
        private const int RuneTightMax = 4;
        private static readonly float[] TightRuneH = { 58f, 58f, 54f, 52f, 50f };     // a rune cell's height (its stone gets smaller)
        private static readonly float[] TightRuneGap = { 4f, 0f, 0f, 0f, 0f };        // between rune rows
        private static readonly int[] TightHeadPad = { 4, 0, 0, 0, 0 };               // over each kind's title
        private static readonly float[] TightColGap = { 4f, 2f, 2f, 2f, 2f };         // between the column's pieces
        private static readonly int[] TightGoldCols = { 5, 5, 6, 7, 8 };              // golden runes a row (the cells narrower to fit 500)
        private static readonly float[] TightGoldW = { 94f, 94f, 78f, 66f, 57f };
        private static readonly float[] TightGoldH = { 80f, 80f, 72f, 66f, 60f };
        private static readonly float[] TightGoldStone = { 48f, 48f, 44f, 40f, 36f };
        private static readonly float[] TightBindH = { 100f, 80f, 72f, 64f, 60f };     // the в'язь card (its emblem with it)

        private static bool RunesFit(RectTransform col)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(col);
            return LayoutUtility.GetPreferredHeight(col) <= ((RectTransform)col.parent).rect.height + 0.5f;
        }

        private static RectTransform RunesRight(Player p, RuneBook.Data d)
        {
            int t = _runeTight;
            RectTransform col = Column("Right", 644f, 40f, 500f, TightColGap[t]);
            RectTransform head = Rect("Head", col);
            HBox(head, 8f, TextAnchor.LowerLeft);
            T(head, Lang.T("Руни"), 26f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(head);
            TMP_Text count = T(head, Lang.F("відкрито {0} із {1}", d.Opened, RuneBook.All.Length), 15f, Brown, F.Body, TextAlignmentOptions.BottomRight);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            Stick(col, d);

            for (int k = 0; k < WearCatalog.Kinds; k++)
            {
                RectTransform row = Rect("Kind", col);
                HBox(row, 6f, TextAnchor.LowerLeft, 0, 0, TightHeadPad[t], 0);
                T(row, Lang.T(RuneBook.KindTitles[k]), 18f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(row);
                TMP_Text what = T(row, Lang.T(RuneBook.KindWhat[k]), 13f, Brown, F.Italic, TextAlignmentOptions.BottomRight);
                what.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform grid = Rect("Grid", col);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(56f, TightRuneH[t]); g.spacing = new Vector2(6f, TightRuneGap[t]);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 8;
                foreach (RuneDef r in RuneBook.All) if ((int)r.Kind == k) RuneCell(grid, p, d, r);
            }

            if (d.GoldEver == 0)
                T(col, Lang.T("Кажуть, в Одіна є й золоті руни — для тих, хто служить йому найвірніше."), 15f, InkSoft, F.Italic);
            else
            {
                RectTransform row = Rect("Gold", col);
                HBox(row, 6f, TextAnchor.LowerLeft, 0, 0, TightHeadPad[t], 0);
                T(row, Lang.T("Золоті руни"), 18f, GoldInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(row);
                TMP_Text n = T(row, Lang.F("у тебе {0} · усього {1}", d.Gold, d.GoldEver), 14f, Brown, F.Body, TextAlignmentOptions.BottomRight);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform grid = Rect("GoldGrid", col);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                // five a row, more and smaller when tight (free ones first, then the ones in gifts), each with its words under it: «вільна», or the gift and the raven
                g.cellSize = new Vector2(TightGoldW[t], TightGoldH[t]); g.spacing = new Vector2(5f, 4f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = TightGoldCols[t];
                for (int i = 0; i < d.Gold; i++) GoldCell(grid);
                // the ones put into gifts stay in sight: in their raven's colour, the gift on hover
                int spent = 0;
                ItemDrop.ItemData wh, wm;
                RavenItem.Worn(p, out wh, out wm);
                foreach (bool mu in new bool[] { false, true })
                {
                    ItemDrop.ItemData it = mu ? wm : wh;
                    if (it == null || Trust.Stranger(it, p)) continue;
                    foreach (System.Collections.Generic.KeyValuePair<string, int> kv in GiftUp.Of(it))
                    {
                        RavenAbility ga = RavenAbilities.Find(kv.Key);
                        int from = mu && ga != null && ga.Pair && !Trust.Stranger(wh, p) ? GiftUp.LevelIn(wh, kv.Key) : 0; // (a paired gift's levels are both ravens' runes: Munin's count on from Hugin's)
                        for (int lv = 1; lv <= kv.Value; lv++) { SpentGoldCell(grid, mu, kv.Key, from + lv); spent++; }
                    }
                }
                if (d.Gold == 0 && spent == 0) T(col, Lang.T("Усі золоті руни вже вкладені в дари."), 14f, InkSoft, F.Italic);
            }

            if (d.Bind.Length > 0)
            {
                RectTransform brow = Rect("Binds", col);
                HBox(brow, 6f, TextAnchor.LowerLeft, 0, 0, TightHeadPad[t], 0);
                T(brow, Lang.T("В'язь"), 18f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(brow);
                bool on = BindRun.On;
                TMP_Text state = T(brow, on ? Lang.T("діє") : Lang.T("вимкнено"), 13f, Brown, F.Italic, TextAlignmentOptions.BottomRight);
                state.textWrappingMode = TextWrappingModes.NoWrap;
                TMP_Text sw = T(brow, on ? Lang.T("Вимкнути") : Lang.T("Увімкнути"), 13f, GoldInk, F.Head, TextAlignmentOptions.BottomRight);
                sw.textWrappingMode = TextWrappingModes.NoWrap;
                Tip(sw.gameObject, () => Lang.T("Вимкнена в'язь не спрацьовує й не відновлює заряди. Те саме — перемикач у колі дарів (Q)."));
                Clickable(sw.gameObject, () => { BindRun.SetOn(p, !BindRun.On); Show(_tab, true); });
                BindCell(col, p, d);
            }
            else if (d.Opened >= 2) T(col, Lang.T("Десь у світі ростуть Воронячі дерева. Біля їхнього коріння руни сплітаються у в'язі."), 14f, InkSoft, F.Italic);
            return col;
        }

        // the stick of notches: five cuts, the done ones gold
        private static void Stick(RectTransform col, RuneBook.Data d)
        {
            RectTransform row = Rect("Stick", col);
            HBox(row, 5f, TextAnchor.MiddleLeft, 0, 0, 2, 4);
            T(row, Lang.T("Різа"), 15f, GoldInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            for (int i = 0; i < RuneBook.NotchesPerRune; i++)
            {
                RectTransform cut = Rect("Cut", row);
                Fixed(cut.gameObject, 8f, 22f);
                Box(cut, 2f, i < d.Notches ? BookArt.Initial : A(Ink, 0.08f), A(Ink, 0.45f), 1f);
            }
            TMP_Text n = T(row, Lang.F("{0} / {1}", d.Notches, RuneBook.NotchesPerRune), 14f, InkSoft, F.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            Spring(row);
            Tip(row.gameObject, () => Lang.F("<b>Різа</b>\n\nЗа кожне виконане доручення Одіна на різі з'являються зарубки. Коли їх {0}, загоряється одна руна, навмання з тих, що ще не відкриті, і разом з нею — її сила.", RuneBook.NotchesPerRune));
        }

        private static void RuneCell(Transform grid, Player p, RuneBook.Data d, RuneDef r)
        {
            bool open = d.Has(r.Id), taken = open && d.InBind(r.Id);
            WearDef eff = open ? RuneBook.EffectOf(d, r.Id) : null;
            int by = open && !taken ? RuneBook.WornBy(p, r.Id) : 0;
            int stars = open ? d.StarsOf(r.Id) : 0;
            RectTransform cell = Rect("Rune_" + r.Id, grid);
            if (stars > 0 && !taken) // the sparks of Odin: a gold halo well past the stone, slowly breathing (the user: seen at once)
            {
                Image glow = Img(cell, "SparkGlow", BookArt.Glow, A(Gold, 0.6f));
                Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-24f, -12f), new Vector2(24f, 24f));
                glow.raycastTarget = false;
                SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
                b.Low = 0.45f + 0.1f * stars; b.High = Mathf.Min(1f, 0.75f + 0.1f * stars);
            }
            if (!RuneStone(cell, (int)r.Kind, r.Glyph, !open ? 0 : by != 0 || (stars > 0 && !taken) ? 2 : 1, 13f, 22f))
            {
                Torn(cell, open ? (by != 0 ? new Color(0.66f, 0.52f, 0.23f, 0.25f) : BookArt.Card) : A(Ink, 0.05f),
                    open ? C("#a8843a") : A(Ink, 0.22f), 1.5f);
                TMP_Text g = T(cell, r.Glyph, 30f, open ? (eff != null ? GoldInk : Muted) : A(Ink, 0.22f), F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 10f), new Vector2(0f, 0f));
            }
            TMP_Text name = T(cell, Lang.T(r.Name), 11f, open ? Brown : A(Ink, 0.3f), F.Body, TextAlignmentOptions.Bottom);
            Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 3f), new Vector2(-1f, 0f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            if (by != 0)
            {
                Image dot = Img(cell, "On", BookArt.Circle, by == 2 ? BookArt.Munin : BookArt.Hugin);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(0f, 1f);
                dot.rectTransform.sizeDelta = new Vector2(10f, 10f); dot.rectTransform.anchoredPosition = new Vector2(5f, -5f);
            }
            if (taken) // woven into the в'язь: dimmed and clamped like a golden rune given away; no click
            {
                Image dull = Img(cell, "Dull", BookArt.SoftDisc, new Color(0.22f, 0.16f, 0.08f, 0.6f));
                Stretch(dull.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 15f), new Vector2(-2f, -2f));
                dull.raycastTarget = false;
                Image clamp = Img(cell, "Clamp", BookArt.Picture("tree_clamp"), Color.white);
                Stretch(clamp.rectTransform, Vector2.zero, Vector2.one, new Vector2(-6f, 6f), new Vector2(6f, 6f));
                clamp.preserveAspect = true; clamp.raycastTarget = false;
            }
            if (stars > 0) SparkRow(cell, stars, true);
            if (_freshRunes.Contains(r.Id))
            {
                Image nd = Img(cell, "New", BookArt.Circle, Gold);
                nd.rectTransform.anchorMin = nd.rectTransform.anchorMax = nd.rectTransform.pivot = new Vector2(1f, 1f);
                nd.rectTransform.sizeDelta = new Vector2(10f, 10f); nd.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
            }
            string kind = Lang.T(RuneBook.KindTitles[(int)r.Kind]);
            Tip(cell.gameObject, () =>
            {
                string t = "<b>" + Glyph(r) + Lang.T(r.Name) + "</b> · " + kind;
                if (!open) return t + "\n\n" + Lang.F("Ще не відкрита. Руни загоряються за доручення Одіна: {0} зарубок на різі — одна руна, навмання.", RuneBook.NotchesPerRune);
                if (eff == null) return t + "\n\n" + Lang.T("Руна горить, але її сила ще не проявилась.");
                if (taken) return t + "\n\n<color=" + GoldHex + ">" + eff.Name + "</color>\n\n" + Lang.T("Ця руна зараз у в'язі. Розплети в'язь, щоб вдягти її окремо.");
                string away = by == 0 ? RuneBook.Elsewhere(p, r.Id) : null;
                string on = by == 1 ? Lang.T("Зараз на Хугіні.") : by == 2 ? Lang.T("Зараз на Муніні.") : away != null ? Lang.F("Зараз {0}.", away) : Lang.T("Зараз ні на кому.");
                return t + "\n\n<color=" + GoldHex + ">" + eff.Name + "</color>" + (eff.Desc.Length > 0 ? "\n" + eff.Desc : "") + "\n\n" + RuneStars.Describe(d, r.Id) + "\n\n" + on + "\n" + Lang.T("Клік — на якого ворона повісити.");
            });
            if (!LightCell(cell, p, r.Id) && open && eff != null && !taken) Clickable(cell.gameObject, () => RuneAsk(cell, r, eff)); // (lighting a rune with a raven's sparklets: RavenBookRequest)
        }

        // your в'язь: its emblem (the stones tied by its tree's knot) and its sentence, short; hover — the whole sentence.
        // It is yours, not a raven's: nothing to hang anywhere, no click (on/off over it, unweaving only at a tree)
        private static void BindCell(Transform col, Player p, RuneBook.Data d)
        {
            bool on = BindRun.On;
            Bind b = Bind.Parse(d.Bind);
            RectTransform cell = Rect("Bind", col);
            float h = TightBindH[_runeTight], e = h - 4f;
            Fixed(cell.gameObject, -1f, h);
            Torn(cell, on ? new Color(0.66f, 0.52f, 0.23f, 0.25f) : BookArt.Card, C("#7a5ab0"), 1.5f);
            RectTransform holder = Rect("Holder", cell);
            At(holder, 4f, 2f, e, e);
            BindEmblem(holder, b, d.BindRunes, e);
            BindPower w = BindRun.PowerOf(d.Bind);
            TMP_Text n = T(cell, w != null ? Short(d, d.BindRunes, w) : BindRunes(d), 14f, on ? Ink : InkSoft, F.Body, TextAlignmentOptions.Left);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(e + 12f, 6f), new Vector2(-10f, -6f));
            n.richText = true; n.raycastTarget = false;
            n.enableAutoSizing = true; n.fontSizeMin = 10f; n.fontSizeMax = 14f;
            Tip(cell.gameObject, () => "<b>" + Lang.T("В'язь") + "</b>\n\n" + (w != null ? w.Sentence() : "") + "\n\n" + (BindRun.On ? Lang.T("Діє на тобі.") : Lang.T("Вимкнена: не спрацьовує й не відновлює заряди.")) + "\n" + Lang.T("Розплести її можна лише біля Воронячого дерева."));
        }

        private static string BindRunes(RuneBook.Data d)
        {
            List<string> l = new List<string>();
            foreach (string r in d.BindRunes) { RuneDef rd = RuneBook.Def(r); if (rd != null) l.Add(Glyph(rd) + Lang.T(rd.Name)); }
            return string.Join(" + ", l.ToArray());
        }

        // a golden rune in hand (what it goes into comes with the gifts' levels)
        private static void GoldCell(Transform grid)
        {
            RectTransform cell = Rect("Gold", grid);
            RectTransform stone = GoldStone(cell);
            if (!RuneStone(stone, GoldKind, "ᚨ", 2, 0f, 24f))
            {
                Box(stone, 6f, new Color(0.9f, 0.75f, 0.35f, 0.3f), C("#a8843a"), 2f);
                TMP_Text g = T(stone, "ᚨ", 28f, GoldInk, F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            GoldWords(cell, Lang.T("вільна"), null, Brown);
            Tip(cell.gameObject, () => Lang.F("<b>Золота руна</b>\n\nВкладається в один дар одного ворона — назавжди: дар стає сильнішим. У ворона до {0} золотих рун, у дару до {1} рівнів.\nКлік — у який дар вкласти.", GiftUp.MaxPerRaven, GiftUp.MaxLevel));
            Clickable(cell.gameObject, () => GoldAsk(cell, null, null));
        }

        // a golden rune already in a gift: no glow, no click, the raven's colour
        private static void SpentGoldCell(Transform grid, bool munin, string gift, int level)
        {
            RectTransform cell = Rect("GoldIn", grid);
            Color ink = BookArt.RavenInk(munin);
            RectTransform stone = GoldStone(cell);
            if (!RuneStone(stone, GoldKind, "ᚨ", 1, 0f, 22f))
            {
                Box(stone, 6f, new Color(ink.r, ink.g, ink.b, 0.12f), new Color(ink.r, ink.g, ink.b, 0.6f), 2f);
                TMP_Text g = T(stone, "ᚨ", 28f, ink, F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            Image dull = Img(stone, "Dull", BookArt.SoftDisc, new Color(0.22f, 0.16f, 0.08f, 0.55f)); // (its glow given to the gift)
            Stretch(dull.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image clamp = Img(stone, "Clamp", BookArt.Picture("tree_clamp"), Color.white);
            Stretch(clamp.rectTransform, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            clamp.preserveAspect = true;
            RavenAbility a = RavenAbilities.Find(gift);
            string name = a != null ? a.Plain : gift;
            GoldWords(cell, "<b>" + name + " " + GiftUp.Roman(level) + "</b>", Plugin.RavenName(munin), Ink);
            Tip(cell.gameObject, () => Lang.F("<b>Золота руна · вкладена</b>\n\n{0}, дар «{1}» — рівень {2}.", Plugin.RavenName(munin), name, GiftUp.Roman(level)));
        }

        // a rune's sparks of Odin as little carved sparks (assets/book/tree_spark.png) in a row across its cell, at its
        // top or bottom edge
        private static void SparkRow(RectTransform cell, int n, bool top)
        {
            if (n <= 0) return;
            const float s = 15f, gap = 1f;
            RectTransform row = Rect("Sparks", cell);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, top ? 1f : 0f);
            row.sizeDelta = new Vector2(n * s + (n - 1) * gap, s);
            row.anchoredPosition = new Vector2(0f, top ? 1f : -4f);
            for (int i = 0; i < n; i++)
            {
                Image im = Img(row, "Spark", BookArt.Picture("tree_spark"), Color.white);
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = im.rectTransform.pivot = new Vector2(0f, 0.5f);
                im.rectTransform.sizeDelta = new Vector2(s, s);
                im.rectTransform.anchoredPosition = new Vector2(i * (s + gap), 0f);
            }
        }

        // a golden cell's stone: the upper part of the cell (the words go under it)
        private static RectTransform GoldStone(RectTransform cell)
        {
            RectTransform s = Rect("Stone", cell);
            s.anchorMin = s.anchorMax = s.pivot = new Vector2(0.5f, 1f);
            float z = TightGoldStone[_runeTight];
            s.sizeDelta = new Vector2(z, z); s.anchoredPosition = new Vector2(0f, -2f);
            return s;
        }

        // the words under a golden cell: one or two lines
        private static void GoldWords(RectTransform cell, string first, string second, Color ink)
        {
            TMP_Text t = T(cell, first + (second != null ? "\n<size=90%><color=#" + ColorUtility.ToHtmlStringRGB(Brown) + ">" + second + "</color></size>" : ""), 11f, ink, F.Body, TextAlignmentOptions.Bottom);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -TightGoldStone[_runeTight] - 2f));
            t.richText = true; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = 8f; t.fontSizeMax = 11f;
        }

        // ---- a golden rune into a gift: which raven (or «Парні») → which gift → «так, назавжди» (one card, step by step)
        private static void GoldAsk(RectTransform at, bool? munin, string gift, bool pair = false)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (_root == null || p == null) return;
            Card card = OpenCard("GoldAsk", Lang.T("Золота руна"), 460f, true);
            RectTransform panel = card.Paper;
            panel.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            RectTransform row = card.Foot;

            if (pair && gift == null)
            {
                T(panel, Lang.T("Який парний дар покращити?"), 21f, PInk, F.Head);
                T(panel, Lang.T("Руна ляже в того ворона, у кого ще є місце."), 15f, PSoft, F.Body);
                foreach (RavenAbility a in RavenAbilities.All)
                {
                    if (!a.Pair || !GiftUp.Upgradable.Contains(a.Id)) continue;
                    RavenAbility ab = a;
                    int lv = GiftUp.Level(a.Id);
                    string why;
                    PairRaven(p, a.Id, out why);
                    RectTransform r = GoldGiftRow(panel, a, lv, why == null);
                    Tip(r.gameObject, () => "<b>" + ab.Plain + "</b>\n\n" + GiftUp.LevelsText(ab.Id) + (why != null ? "\n\n<color=#FF6B6B>" + why + "</color>" : ""));
                    if (why == null) Clickable(r.gameObject, () => GoldAsk(at, null, ab.Id, true));
                }
                AskButton(row, Lang.T("Назад"), false, () => GoldAsk(at, null, null));
            }
            else if (pair)
            {
                RavenAbility a = RavenAbilities.Find(gift);
                string none;
                bool m = PairRaven(p, gift, out none);
                int lv = GiftUp.Level(gift) + 1;
                T(panel, Lang.F("«{0}» — рівень {1}?", a != null ? a.Plain : gift, GiftUp.Roman(lv)), 21f, PInk, F.Head);
                T(panel, GiftUp.LevelsText(gift), 15f, PInk, F.Body);
                T(panel, Lang.F("<color=#FF6B6B>Назавжди.</color> Руна ляже в {0}: з дару її вже не витягти, вона лишиться в цьому вороні, навіть якщо віддаси його іншому.", m ? Lang.T("Муніна") : Lang.T("Хугіна")).Replace("#FF6B6B", BadHex), 15f, PInk, F.Body);
                Action yes = () => { ClosePopup(); string why = GiftUp.Invest(Player.m_localPlayer, m, gift); Tell(why ?? Lang.F("Золота руна в дарі «{0}»: рівень {1}.", a != null ? a.Plain : gift, GiftUp.Roman(lv))); Show(_tab, true); };
                AskButton(row, Lang.T("Назад"), false, () => GoldAsk(at, null, null, true));
                CardButton(row, Lang.T("Так, назавжди"), true, yes, "ᚨ");
                _nickYes = yes;
            }
            else if (!munin.HasValue)
            {
                T(panel, Lang.T("Золота руна — у дар якого ворона?"), 21f, PInk, F.Head);
                foreach (bool m in new bool[] { true, false })
                {
                    bool w = m;
                    ItemDrop.ItemData item = Trust.Worn(w);
                    if (item == null) continue;
                    CardButton(row, Lang.F("{0} ({1}/{2})", Plugin.RavenName(w), GiftUp.Used(item), GiftUp.MaxPerRaven), true, () => GoldAsk(at, w, null), "ᚨ");
                }
                if (AnyPairOpen()) CardButton(row, Lang.T("Парні"), true, () => GoldAsk(at, null, null, true), "ᚨ");
                if (Trust.Worn(false) == null && Trust.Worn(true) == null) T(panel, Lang.T("Жодного ворона нема на плечі."), 15f, PSoft, F.Italic);
            }
            else if (gift == null)
            {
                bool m = munin.Value;
                ItemDrop.ItemData item = Trust.Worn(m);
                T(panel, Lang.F("Який дар {0} покращити?", m ? Lang.T("Муніна") : Lang.T("Хугіна")), 21f, PInk, F.Head);
                T(panel, Lang.F("Золотих рун у ньому: {0} з {1}.", GiftUp.Used(item), GiftUp.MaxPerRaven), 15f, PSoft, F.Body);
                foreach (RavenAbility a in RavenAbilities.All)
                {
                    if (a.Pair || a.Munin != m || !GiftUp.Upgradable.Contains(a.Id)) continue; // (paired gifts have their own «Парні» list)
                    RavenAbility ab = a;
                    int lv = GiftUp.LevelIn(item, a.Id);
                    string why = GiftUp.CanInvest(p, m, a.Id);
                    RectTransform r = GoldGiftRow(panel, a, lv, why == null);
                    Tip(r.gameObject, () => "<b>" + ab.Plain + "</b>\n\n" + GiftUp.LevelsText(ab.Id) + (why != null ? "\n\n<color=#FF6B6B>" + why + "</color>" : ""));
                    if (why == null) Clickable(r.gameObject, () => GoldAsk(at, m, ab.Id));
                }
                AskButton(row, Lang.T("Назад"), false, () => GoldAsk(at, null, null));
            }
            else
            {
                bool m = munin.Value;
                RavenAbility a = RavenAbilities.Find(gift);
                int lv = GiftUp.LevelIn(Trust.Worn(m), gift) + 1;
                T(panel, Lang.F("«{0}» {1} — рівень {2}?", a != null ? a.Plain : gift, Plugin.RavenName(m), GiftUp.Roman(lv)), 21f, PInk, F.Head);
                T(panel, GiftUp.LevelsText(gift), 15f, PInk, F.Body);
                T(panel, Lang.T("<color=#FF6B6B>Назавжди.</color> Золоту руну з дару вже не витягти: вона лишиться в цьому вороні, навіть якщо віддаси його іншому.").Replace("#FF6B6B", BadHex), 15f, PInk, F.Body); // (the dark tooltips' red, darker on parchment)
                Action yes = () => { ClosePopup(); string why = GiftUp.Invest(Player.m_localPlayer, m, gift); Tell(why ?? Lang.F("Золота руна в дарі «{0}»: рівень {1}.", a != null ? a.Plain : gift, GiftUp.Roman(lv))); Show(_tab, true); };
                AskButton(row, Lang.T("Назад"), false, () => GoldAsk(at, m, null));
                CardButton(row, Lang.T("Так, назавжди"), true, yes, "ᚨ");
                _nickYes = yes;
            }
            PlaceCard(card, at);
        }

        // a gift's line in the golden rune card: its icon, then the light block with its name and level (returned: the
        // block, for the tip and the click)
        private static RectTransform GoldGiftRow(Transform panel, RavenAbility a, int lv, bool can)
        {
            RectTransform line = Rect("GiftLine", panel);
            HBox(line, 8f, TextAnchor.MiddleLeft);
            RectTransform pic = Rect("Pic", line);
            Fixed(pic.gameObject, 32f, 32f);
            Image ic = Img(pic, "Icon", AbilityIcons.For(a.Id, a.Munin), can ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            ic.preserveAspect = true;
            Stretch(ic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (lv > 0) GoldBadge(pic, 32f, a.Pair ? 0 : a.Munin ? -1 : 1, lv);
            RectTransform r = PaperChoice(line, false, can);
            r.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            HBox(r, 8f, TextAnchor.MiddleLeft, 10, 10, 5, 5);
            Shrink(T(r, a.Plain, 17f, can ? PInk : PSoft, F.Head), 12f); // (one line: a longer name gets smaller, then «…»)
            T(r, lv > 0 ? GiftUp.Roman(lv) : "—", 16f, lv > 0 ? BookArt.GoldInk : PSoft, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            return r;
        }

        // «Парні» only with both ravens on you and at least one paired gift open
        private static bool AnyPairOpen()
        {
            if (!BothWorn) return false;
            foreach (RavenAbility a in RavenAbilities.All) if (a.Pair && GiftUp.Upgradable.Contains(a.Id) && RavenAbilities.Unlocked(a)) return true;
            return false;
        }

        // which raven a paired gift's rune goes into: the one that can take it, the emptier one when both can
        private static bool PairRaven(Player p, string id, out string why)
        {
            string h = GiftUp.CanInvest(p, false, id), m = GiftUp.CanInvest(p, true, id);
            why = h != null && m != null ? h : null;
            if (h == null && m == null) return GiftUp.Used(Trust.Worn(true)) < GiftUp.Used(Trust.Worn(false));
            return h != null && m == null;
        }

        // "on which raven?" by the clicked rune: the two ravens on you, the one wearing it marked; «Зняти» when worn
        private static void RuneAsk(RectTransform cell, RuneDef r, WearDef eff)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (_root == null || p == null) return;
            int by = RuneBook.WornBy(p, r.Id);

            Card card = OpenCard("RuneAsk", Lang.T("Руна"), 440f, true);
            RectTransform panel = card.Paper;

            TMP_FontAsset rf = FontOf(F.Rune);
            T(panel, (rf != null ? "<font=\"" + rf.name + "\">" + r.Glyph + "</font> " : "") + Lang.T(r.Name) + " — <color=" + GoldInkHex + ">" + eff.Name + "</color>", 21f, PInk, F.Head);
            T(panel, Lang.F("Одна руна — на одному вороні. Якщо на ньому вже висить інша {0}, ця стане замість неї.", Lang.T(RuneBook.KindOne[(int)r.Kind])), 15f, PInk, F.Body);
            string away = by == 0 ? RuneBook.Elsewhere(p, r.Id) : null;
            if (away != null) T(panel, Lang.F("Зараз вона {0}. Повісиш тут — з нього вона зійде.", away), 15f, PSoft, F.Italic);

            RectTransform row = card.Foot;
            if (by != 0 || away != null) AskButton(row, Lang.T("Зняти"), false, () => { ClosePopup(); string why = RuneBook.TakeOff(p, r.Id); if (why != null) Tell(why); Show(_tab, true); });
            foreach (bool m in new bool[] { true, false })
            {
                bool w = m;
                if (Trust.Worn(w) == null) continue;
                bool on = by == (w ? 2 : 1);
                AskButton(row, (on ? "✓ " : "") + Plugin.RavenName(w), !on, () =>
                {
                    ClosePopup();
                    if (!on) { string why = RuneBook.PutOn(p, r.Id, w); if (why != null) Tell(why); else _dressMunin = w; }
                    Show(_tab, true);
                });
            }

            PlaceCard(card, cell);
        }

        // ---- rune stones: the player's own pictures with the rune drawn on them, so one picture serves a whole kind.
        // assets/book/rune_freya.png, rune_baldr.png, rune_heimdall.png, rune_gold.png (each kind its own), rune_stone.png
        // (any kind without its own); a "_closed" one (rune_freya_closed / rune_stone_closed) for a rune not yet open, else
        // the open picture darkened. No picture at all: the cells stay drawn as before.
        private const int GoldKind = 3; // (StoneNames/StoneGlow index past the three wear kinds)
        private static readonly string[] StoneNames = { "rune_freya", "rune_baldr", "rune_heimdall", "rune_gold" };
        private static readonly Color[] StoneGlow = { C("#ffb3c4"), C("#ffe7a8"), C("#a8ecff"), C("#ffd76a") };
        // where the gem sits in each picture (a share of the cell off its middle, + right / + up): the rune goes there
        private static readonly Vector2[] StoneGem = { new Vector2(0.03f, 0.01f), new Vector2(0.02f, 0f), new Vector2(0f, 0.03f), new Vector2(0.07f, 0.03f) };
        private static readonly Dictionary<int, Material> _glowMats = new Dictionary<int, Material>();

        private static Sprite StoneArt(int kind, bool open, out bool ownClosed)
        {
            ownClosed = false;
            if (!open)
            {
                Sprite c = BookArt.Picture(StoneNames[kind] + "_closed") ?? BookArt.Picture("rune_stone_closed");
                if (c != null) { ownClosed = true; return c; }
            }
            return BookArt.Picture(StoneNames[kind]) ?? BookArt.Picture("rune_stone");
        }

        // the rune font's material with a soft glow of the kind's colour under the sign: one per font, kind and brightness;
        // the atlas is set again on every use (a dynamic font may have grown a new one since)
        private static Material GlowMat(TMP_FontAsset f, int kind, bool lit)
        {
            if (f == null || f.material == null) return null;
            int key = f.GetInstanceID() * 8 + kind * 2 + (lit ? 1 : 0);
            Material m;
            if (_glowMats.TryGetValue(key, out m) && m != null)
            {
                m.SetTexture(ShaderUtilities.ID_MainTex, f.material.GetTexture(ShaderUtilities.ID_MainTex));
                return m;
            }
            m = new Material(f.material);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            Color c = StoneGlow[kind]; c.a = lit ? 0.95f : 0.6f;
            m.SetColor(ShaderUtilities.ID_UnderlayColor, c);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, lit ? 0.6f : 0.3f);
            _glowMats[key] = m;
            return m;
        }

        // the stone and its rune into a cell: state 0 not open (cut dark into the stone), 1 open, 2 open and lit (worn,
        // laid on the roots, gold in hand); bottom: the room kept free under it (for the name). False: no picture.
        private static bool RuneStone(RectTransform cell, int kind, string glyph, int state, float bottom, float glyphSize)
        {
            bool ownClosed;
            Sprite s = StoneArt(kind, state > 0, out ownClosed);
            if (s == null) return false;
            Image im = Img(cell, "Stone", s, state == 0 && !ownClosed ? new Color(0.45f, 0.45f, 0.45f, 0.7f) : Color.white);
            float pad = 7f; // (the pictures have room round the stone: drawn a little past the cell, the stone fills it)
            Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(-pad, bottom - pad), new Vector2(pad, pad));
            im.preserveAspect = true; im.raycastTarget = false;
            Color glow = StoneGlow[kind];
            TMP_Text g = T(cell, glyph, glyphSize, state == 0 ? new Color(0.05f, 0.04f, 0.03f, 0.75f) : Color.Lerp(glow, Color.white, state == 2 ? 0.7f : 0.4f), F.Rune, TextAlignmentOptions.Center);
            Vector2 gem = StoneGem[kind];
            Stretch(g.rectTransform, gem, Vector2.one + gem, new Vector2(0f, bottom), Vector2.zero);
            g.raycastTarget = false; g.textWrappingMode = TextWrappingModes.NoWrap;
            if (state > 0)
            {
                Material m = GlowMat(g.font, kind, state == 2);
                if (m != null) { g.fontSharedMaterial = m; g.extraPadding = true; }
            }
            return true;
        }

        // ---- «Образ»: the rune places round the raven's window (a diamond per kind) and its golden seal

        // which open rune gives the raven what it wears in this layer (null: nothing, or a в'язь)
        private static string RuneOn(RuneBook.Data d, WearKind k, string id)
        {
            if (id.Length == 0) return null;
            foreach (string r in d.Order) { RuneDef rd = RuneBook.Def(r); if (rd != null && rd.Kind == k && d.WearFor(r) == id) return r; }
            return null;
        }

        // how many runes of this kind could hang there
        private static int RunesOfKind(RuneBook.Data d, WearKind k)
        {
            int n = 0;
            foreach (string r in d.Order) { RuneDef rd = RuneBook.Def(r); if (rd != null && rd.Kind == k && RuneBook.EffectOf(d, r) != null && !d.InBind(r)) n++; }
            return n;
        }

        private static void RuneSlot(RectTransform left, Player p, bool m, ItemDrop.ItemData item, WearKind k, float x, float y, bool nameAbove)
        {
            RuneBook.Data d = RuneBook.Of(p);
            string id = RavenWear.Of(item)[k];
            string rune = RuneOn(d, k, id);
            WearDef eff = id.Length > 0 ? WearCatalog.Find(k, id) : null;
            bool on = eff != null && rune != null;
            int have = RunesOfKind(d, k);

            RectTransform box = Rect("Rune" + (int)k, left);
            At(box, x, y, 88f, 96f);
            Vector2 mid = new Vector2(0f, nameAbove ? 38f : -38f);
            Vector2 anchor = new Vector2(0.5f, nameAbove ? 0f : 1f);
            Image shade = Img(box, "Shade", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.35f));
            shade.rectTransform.anchorMin = shade.rectTransform.anchorMax = anchor;
            shade.rectTransform.sizeDelta = new Vector2(78f, 78f); shade.rectTransform.anchoredPosition = mid + new Vector2(0f, -4f);
            shade.raycastTarget = false;
            string sign = rune != null ? RuneBook.Def(rune).Glyph : have > 0 ? "+" : "";
            RectTransform dia = Rect("Diamond", box);
            dia.anchorMin = dia.anchorMax = anchor;
            dia.anchoredPosition = mid;
            dia.sizeDelta = new Vector2(70f, 70f);
            if (!on || !RuneStone(dia, (int)k, sign, 2, 0f, 26f)) // a rune on it: its stone (when there is a picture); else the diamond
            {
                dia.sizeDelta = new Vector2(52f, 52f);
                dia.localEulerAngles = new Vector3(0f, 0f, 45f);
                Box(dia, 6f, on ? C("#3a2c1c") : have > 0 ? C("#efe3c4") : C("#e2d4b2"), on ? C("#a8843a") : A(Ink, have > 0 ? 0.5f : 0.22f), on ? 2f : 1.5f);
                TMP_Text g = T(box, sign, on ? 28f : 24f, on ? Gold : A(Ink, 0.4f), on ? F.Rune : F.Head, TextAlignmentOptions.Center);
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = anchor;
                g.rectTransform.sizeDelta = new Vector2(52f, 52f); g.rectTransform.anchoredPosition = mid;
                g.raycastTarget = false;
            }
            TMP_Text n = T(box, Lang.T(WearCatalog.KindNames[(int)k]), 15f, Ink, F.Head, nameAbove ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            n.raycastTarget = false;

            string kind = Lang.T(RuneBook.KindTitles[(int)k]);
            Tip(dia.gameObject, () =>
            {
                string t = "<b>" + kind + "</b> · " + Lang.T(RuneBook.KindWhat[(int)k]);
                if (on) t += "\n\n" + Glyph(RuneBook.Def(rune)) + Lang.T(RuneBook.Def(rune).Name) + " — <color=" + GoldHex + ">" + eff.Name + "</color>" + (eff.Desc.Length > 0 ? "\n" + eff.Desc : "") + "\n\n" + RuneStars.Describe(d, rune);
                else if (have == 0) return t + "\n\n" + Lang.T("Жодна така руна ще не горить. Руни дає Одін за доручення — «Шлях Одіна».");
                return t + "\n\n" + Lang.T("Клік — яку руну повісити.");
            });
            if (have > 0 || on) Clickable(dia.gameObject, () => RuneSlotAsk(dia, m, k));
        }

        // a click on a rune place: the open runes of its kind; «Зняти» when one hangs there
        private static void RuneSlotAsk(RectTransform at, bool m, WearKind k)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(m);
            if (_root == null || p == null || item == null) return;
            RuneBook.Data d = RuneBook.Of(p);
            string id = RavenWear.Of(item)[k];
            Card card = OpenCard("RuneSlotAsk", Lang.T(RuneBook.KindTitles[(int)k]), 460f, true);
            RectTransform panel = card.Paper;
            panel.GetComponent<VerticalLayoutGroup>().spacing = 6f;
            T(panel, Lang.F("{0} — {1}", Lang.T(RuneBook.KindTitles[(int)k]), Plugin.RavenName(m)), 21f, PInk, F.Head);
            string away = Shoulder.NotOnShoulder(p, m);
            if (away != null) T(panel, away, 15f, BookArt.Bad, F.Italic);
            foreach (string r in d.Order)
            {
                RuneDef rd = RuneBook.Def(r);
                WearDef w = rd != null && rd.Kind == k ? RuneBook.EffectOf(d, r) : null;
                if (w == null || d.InBind(r)) continue;
                string rid = r;
                int by = RuneBook.WornBy(p, r);
                SlotChoice(panel, Glyph(rd) + Lang.T(rd.Name), w, w.Id == id, by, by == 0 ? RuneBook.Elsewhere(p, r) : null, () => RuneBook.PutOn(p, rid, m));
            }
            RectTransform row = card.Foot;
            string on = RuneOn(d, k, id);
            if (on != null)
                AskButton(row, Lang.T("Зняти"), false, () => { ClosePopup(); string why = RuneBook.TakeOff(p, on); if (why != null) Tell(why); Show(_tab, true); });
            AskButton(row, Lang.T("Назад"), false, ClosePopup);
            PlaceCard(card, at);
        }

        // one choice in that list: the rune, what it gives, who wears it now (the one there now: dark, the gold line)
        private static void SlotChoice(RectTransform panel, string rune, WearDef w, bool here, int by, string away, Func<string> put)
        {
            RectTransform r = PaperChoice(panel, here, true);
            HBox(r, 8f, TextAnchor.MiddleLeft, 10, 10, 5, 5);
            TMP_Text n = T(r, rune + " — <color=" + (here ? GoldHex : GoldInkHex) + ">" + w.Name + "</color>", 17f, here ? PCream : PInk, F.Head);
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            n.raycastTarget = false;
            string who = here ? "✓" : by == 1 ? Lang.T("на Хугіні") : by == 2 ? Lang.T("на Муніні") : away ?? "";
            if (who.Length > 0) { TMP_Text t = T(r, who, 14f, here ? Gold : PSoft, F.Italic); t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; }
            if (here) return;
            Clickable(r.gameObject, () => { ClosePopup(); string why = put(); if (why != null) Tell(why); Show(_tab, true); });
        }

        // the raven's golden runes: a gold seal with how many of five; hover — in which gifts; a click — put one in
        private static void GoldSeal(RectTransform left, Player p, bool m, ItemDrop.ItemData item, float x, float y)
        {
            RuneBook.Data d = RuneBook.Of(p);
            int used = GiftUp.Used(item);
            if (used == 0 && d.GoldEver == 0) return; // (no gold seen yet: the place stays a surprise)
            RectTransform box = Rect("Gold", left);
            At(box, x, y, 88f, 96f);
            RectTransform c = Rect("Seal", box);
            c.anchorMin = c.anchorMax = new Vector2(0.5f, 1f);
            c.sizeDelta = new Vector2(62f, 62f); c.anchoredPosition = new Vector2(0f, -38f);
            Image ring = Img(c, "Ring", BookArt.Circle, C("#a8843a"));
            Stretch(ring.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = Img(c, "Fill", BookArt.Circle, used > 0 ? new Color(0.55f, 0.42f, 0.16f, 1f) : C("#efe3c4"));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.raycastTarget = false;
            TMP_Text g = T(c, "ᚨ", 28f, used > 0 ? C("#ffe3a0") : A(GoldInk, 0.6f), F.Rune, TextAlignmentOptions.Center);
            Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            g.raycastTarget = false;
            TMP_Text n = T(box, Lang.F("Золото {0}/{1}", used, GiftUp.MaxPerRaven), 14f, GoldInk, F.Head, TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            n.raycastTarget = false;
            Tip(ring.gameObject, () =>
            {
                List<string> gold = new List<string>();
                foreach (KeyValuePair<string, int> kv in GiftUp.Of(item)) { RavenAbility a = RavenAbilities.Find(kv.Key); gold.Add((a != null ? a.Plain : kv.Key) + " " + GiftUp.Roman(kv.Value)); }
                return Lang.F("<b>Золоті руни {0}</b> · {1} із {2}", m ? Lang.T("Муніна") : Lang.T("Хугіна"), used, GiftUp.MaxPerRaven)
                    + "\n\n" + (gold.Count > 0 ? string.Join(", ", gold.ToArray()) : Lang.T("Ще жодної в дарах."))
                    + (d.Gold > 0 ? "\n\n" + Lang.F("У тебе невкладених: {0}. Клік — у який дар.", d.Gold) : "");
            });
            if (d.Gold > 0) Clickable(ring.gameObject, () => GoldAsk(c, m, null)); // (a button lights up by itself: no BookGlow on it too)
        }

        // a rune's sign in words: in the runic font (the others have no runes: they would show «□»)
        private static string Glyph(RuneDef r)
        {
            TMP_FontAsset rf = FontOf(F.Rune);
            return rf != null ? "<font=\"" + rf.name + "\">" + r.Glyph + "</font> " : "";
        }

        private static string RunesHelp(string h)
        {
            return Lang.F("{0}Руни дає Одін за свої доручення. За кожне виконане доручення на різі з'являються зарубки, а коли їх {1}, загоряється одна руна — навмання з тих, що ще не відкриті. Що саме вона дасть, вирішується тієї миті й лишається за нею назавжди: у різних гравців та сама руна дає різне.\n\n<b>Руни Фреї</b> фарбують пір'я, очі й полиск. <b>Руни Бальдра</b> дають ефект, що завжди з вороном. <b>Руни Геймдаля</b> лишають слід, коли ворон летить.\n\nНа вороні висить по одній руні кожного роду, а кожна руна — лише на одному вороні. Перевісити можна будь-коли: клік по руні. Руни — твої, а не ворона: віддаси ворона, і він їх не понесе.", h, RuneBook.NotchesPerRune);
        }
    }

    // a rune's halo of Odin's sparks breathing slowly between two brightnesses (the book may stand over a paused game)
    internal sealed class SparkBreath : MonoBehaviour
    {
        internal float Low = 0.5f, High = 0.9f;
        private Image _im;
        private float _phase;
        // its own small canvas: its colour changing every frame redraws only itself, not the whole book (as SealPulse);
        // it takes no clicks (a glow is never a raycast target), so nothing is lost under it
        private void Awake()
        {
            _im = GetComponent<Image>();
            _phase = UnityEngine.Random.value * 6.28f;
            if (_im != null) _im.raycastTarget = false;
            if (GetComponent<Canvas>() == null) gameObject.AddComponent<Canvas>();
        }
        private void Update()
        {
            if (_im == null) return;
            Color c = _im.color;
            c.a = Mathf.Lerp(Low, High, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f + _phase));
            _im.color = c;
        }
    }
}
#endif

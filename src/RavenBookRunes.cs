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

            // ---- right: the runes
            RectTransform col = Column("Right", 644f, 40f, 500f, 8f);
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
                HBox(row, 6f, TextAnchor.LowerLeft, 0, 0, 6, 0);
                T(row, Lang.T(RuneBook.KindTitles[k]), 20f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(row);
                TMP_Text what = T(row, Lang.T(RuneBook.KindWhat[k]), 14f, Brown, F.Italic, TextAlignmentOptions.BottomRight);
                what.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform grid = Rect("Grid", col);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(56f, 62f); g.spacing = new Vector2(6f, 6f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 8;
                foreach (RuneDef r in RuneBook.All) if ((int)r.Kind == k) RuneCell(grid, p, d, r);
            }

            if (d.GoldEver == 0)
                T(col, Lang.T("Кажуть, в Одіна є й золоті руни — для тих, хто служить йому найвірніше."), 15f, InkSoft, F.Italic);
            else
            {
                RectTransform row = Rect("Gold", col);
                HBox(row, 6f, TextAnchor.LowerLeft, 0, 0, 6, 0);
                T(row, Lang.T("Золоті руни"), 20f, GoldInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(row);
                TMP_Text n = T(row, Lang.F("у тебе {0} · усього {1}", d.Gold, d.GoldEver), 14f, Brown, F.Body, TextAlignmentOptions.BottomRight);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform grid = Rect("GoldGrid", col);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(56f, 62f); g.spacing = new Vector2(6f, 6f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 8;
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
                        for (int lv = 1; lv <= kv.Value; lv++) { SpentGoldCell(grid, mu, kv.Key, lv); spent++; }
                }
                if (d.Gold == 0 && spent == 0) T(col, Lang.T("Усі золоті руни вже вкладені в дари."), 14f, InkSoft, F.Italic);
            }

            if (d.Bind.Length > 0)
            {
                RectTransform brow = Rect("Binds", col);
                HBox(brow, 6f, TextAnchor.LowerLeft, 0, 0, 6, 0);
                T(brow, Lang.T("В'язь"), 20f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(brow);
                RectTransform bgrid = Rect("BindGrid", col);
                GridLayoutGroup bg = bgrid.gameObject.AddComponent<GridLayoutGroup>();
                bg.cellSize = new Vector2(250f, 40f); bg.spacing = new Vector2(6f, 6f);
                bg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; bg.constraintCount = 2;
                BindCell(bgrid, p, d);
            }
            else if (d.Opened >= 2) T(col, Lang.T("Десь у світі ростуть Воронячі дерева. Біля їхнього коріння руни сплітаються у в'язі."), 14f, InkSoft, F.Italic);
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
            bool open = d.Has(r.Id);
            WearDef eff = open ? RuneBook.EffectOf(d, r.Id) : null;
            int by = open ? RuneBook.WornBy(p, r.Id) : 0;
            RectTransform cell = Rect("Rune_" + r.Id, grid);
            if (!RuneStone(cell, (int)r.Kind, r.Glyph, !open ? 0 : by != 0 ? 2 : 1, 13f, 22f))
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
            int stars = open ? d.StarsOf(r.Id) : 0;
            if (stars > 0) // the sparks of Odin on it
            {
                TMP_Text st = T(cell, RuneStars.StarText(stars), 12f, Gold, F.Head, TextAlignmentOptions.Top);
                Stretch(st.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -1f));
                st.textWrappingMode = TextWrappingModes.NoWrap; st.raycastTarget = false;
            }
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
                string on = by == 1 ? Lang.T("Зараз на Хугіні.") : by == 2 ? Lang.T("Зараз на Муніні.") : Lang.T("Зараз ні на кому.");
                return t + "\n\n<color=" + GoldHex + ">" + eff.Name + "</color>" + (eff.Desc.Length > 0 ? "\n" + eff.Desc : "") + "\n\n" + RuneStars.Describe(d, r.Id) + "\n\n" + on + "\n" + Lang.T("Клік — на якого ворона повісити.");
            });
            if (open && eff != null) Clickable(cell.gameObject, () => RuneAsk(cell, r, eff));
        }

        // your в'язь: its runes; hover — its sentence; a click — on which raven, or unweave
        private static void BindCell(Transform grid, Player p, RuneBook.Data d)
        {
            int on = RuneBook.BindOn(p);
            RectTransform cell = Rect("Bind", grid);
            Torn(cell, on != 0 ? new Color(0.66f, 0.52f, 0.23f, 0.25f) : BookArt.Card, C("#7a5ab0"), 1.5f);
            TMP_Text n = T(cell, BindRunes(d), 15f, Ink, F.Head, TextAlignmentOptions.Center);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            n.enableAutoSizing = true; n.fontSizeMin = 9f; n.fontSizeMax = 15f;
            if (on != 0)
            {
                Image dot = Img(cell, "On", BookArt.Circle, on == 2 ? BookArt.Munin : BookArt.Hugin);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(0f, 1f);
                dot.rectTransform.sizeDelta = new Vector2(9f, 9f); dot.rectTransform.anchoredPosition = new Vector2(4f, -4f);
            }
            BindPower w = BindRun.PowerOf(d.Bind);
            Tip(cell.gameObject, () => "<b>" + Lang.T("В'язь") + "</b>\n\n" + (w != null ? w.Sentence() : "") + "\n\n" + (on == 1 ? Lang.T("Зараз на Хугіні.") : on == 2 ? Lang.T("Зараз на Муніні.") : Lang.T("Зараз ні на кому — не діє.")) + "\n" + Lang.T("Клік — на якого ворона, або розплести."));
            Clickable(cell.gameObject, () => BindAsk(cell));
        }

        private static string BindRunes(RuneBook.Data d)
        {
            List<string> l = new List<string>();
            foreach (string r in d.BindRunes) { RuneDef rd = RuneBook.Def(r); if (rd != null) l.Add(Glyph(rd) + Lang.T(rd.Name)); }
            return string.Join(" + ", l.ToArray());
        }

        private static void BindAsk(RectTransform cell)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (_root == null || p == null) return;
            RuneBook.Data d = RuneBook.Of(p);
            int by = RuneBook.BindOn(p);
            Card card = OpenCard("BindAsk", Lang.T("В'язь"), 520f, true);
            RectTransform panel = card.Paper;
            T(panel, BindRunes(d), 19f, PInk, F.Head);
            BindPower w = BindRun.PowerOf(d.Bind);
            if (w != null) T(panel, w.Sentence(), 15f, PInk, F.Body);
            T(panel, Lang.T("В'язь носить один ворон; вона діє, поки він на плечі. Руни в ній окремо не вдягаються."), 14f, PSoft, F.Italic);
            RectTransform row = card.Foot;
            if (by != 0) AskButton(row, Lang.T("Зняти"), false, () => { ClosePopup(); string why = RuneBook.TakeOffBind(p); if (why != null) Tell(why); Show(_tab, true); });
            foreach (bool m in new bool[] { true, false })
            {
                bool wm = m;
                if (Trust.Worn(wm) == null) continue;
                bool on = by == (wm ? 2 : 1);
                AskButton(row, (on ? "✓ " : "") + Plugin.RavenName(wm), !on, () => { ClosePopup(); if (!on) { string why = RuneBook.PutBind(p, wm); if (why != null) Tell(why); else _dressMunin = wm; } Show(_tab, true); });
            }
            AskButton(row, Lang.T("Розплести"), false, () => { ClosePopup(); RuneBook.Unweave(p); Tell(Lang.T("В'язь розплетено. Руни вільні.")); Show(_tab, true); });
            PlaceCard(card, cell);
        }

        // a golden rune in hand (what it goes into comes with the gifts' levels)
        private static void GoldCell(Transform grid)
        {
            RectTransform cell = Rect("Gold", grid);
            if (!RuneStone(cell, GoldKind, "ᚨ", 2, 4f, 26f))
            {
                Box(cell, 6f, new Color(0.9f, 0.75f, 0.35f, 0.3f), C("#a8843a"), 2f);
                TMP_Text g = T(cell, "ᚨ", 30f, GoldInk, F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            }
            Tip(cell.gameObject, () => Lang.F("<b>Золота руна</b>\n\nВкладається в один дар одного ворона — назавжди: дар стає сильнішим. У ворона до {0} золотих рун, у дару до {1} рівнів.\nКлік — у який дар вкласти.", GiftUp.MaxPerRaven, GiftUp.MaxLevel));
            Clickable(cell.gameObject, () => GoldAsk(cell, null, null));
        }

        // a golden rune already in a gift: no glow, no click, the raven's colour
        private static void SpentGoldCell(Transform grid, bool munin, string gift, int level)
        {
            RectTransform cell = Rect("GoldIn", grid);
            Color ink = BookArt.RavenInk(munin);
            if (!RuneStone(cell, GoldKind, "ᚨ", 1, 13f, 22f))
            {
                Box(cell, 6f, new Color(ink.r, ink.g, ink.b, 0.12f), new Color(ink.r, ink.g, ink.b, 0.6f), 2f);
                TMP_Text g = T(cell, "ᚨ", 30f, ink, F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);
            }
            TMP_Text lv = T(cell, GiftUp.Roman(level), 11f, ink, F.Body, TextAlignmentOptions.Bottom);
            Stretch(lv.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            RavenAbility a = RavenAbilities.Find(gift);
            string name = a != null ? a.Plain : gift;
            Tip(cell.gameObject, () => Lang.F("<b>Золота руна · вкладена</b>\n\n{0}, дар «{1}» — рівень {2}.", Plugin.RavenName(munin), name, GiftUp.Roman(level)));
        }

        // ---- a golden rune into a gift: which raven → which gift → «так, назавжди» (one card, step by step)
        private static void GoldAsk(RectTransform at, bool? munin, string gift)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (_root == null || p == null) return;
            Card card = OpenCard("GoldAsk", Lang.T("Золота руна"), 460f, true);
            RectTransform panel = card.Paper;
            panel.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            RectTransform row = card.Foot;

            if (!munin.HasValue)
            {
                T(panel, Lang.T("Золота руна — у дар якого ворона?"), 21f, PInk, F.Head);
                foreach (bool m in new bool[] { true, false })
                {
                    bool w = m;
                    ItemDrop.ItemData item = Trust.Worn(w);
                    if (item == null) continue;
                    CardButton(row, Lang.F("{0} ({1}/{2})", Plugin.RavenName(w), GiftUp.Used(item), GiftUp.MaxPerRaven), true, () => GoldAsk(at, w, null), "ᚨ");
                }
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
                    if (a.Munin != m || !GiftUp.Upgradable.Contains(a.Id)) continue;
                    RavenAbility ab = a;
                    int lv = GiftUp.LevelIn(item, a.Id);
                    string why = GiftUp.CanInvest(p, m, a.Id);
                    RectTransform r = PaperChoice(panel, false, why == null);
                    HBox(r, 8f, TextAnchor.MiddleLeft, 10, 10, 5, 5);
                    TMP_Text n = T(r, a.Plain, 17f, why == null ? PInk : PSoft, F.Head);
                    n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    T(r, lv > 0 ? GiftUp.Roman(lv) : "—", 16f, lv > 0 ? BookArt.GoldInk : PSoft, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
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

            RectTransform row = card.Foot;
            if (by != 0) AskButton(row, Lang.T("Зняти"), false, () => { ClosePopup(); string why = RuneBook.TakeOff(p, r.Id); if (why != null) Tell(why); Show(_tab, true); });
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

        // how many runes and в'язі of this kind could hang there
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
            BindLook.Visual bv = BindLook.Of(RavenWear.Of(item).Bind);
            bool bind = bv != null && bv.Has(k);
            bool on = bind || (eff != null && rune != null);
            int have = RunesOfKind(d, k);

            RectTransform box = Rect("Rune" + (int)k, left);
            At(box, x, y, 88f, 96f);
            Vector2 mid = new Vector2(0f, nameAbove ? 38f : -38f);
            Vector2 anchor = new Vector2(0.5f, nameAbove ? 0f : 1f);
            Image shade = Img(box, "Shade", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.35f));
            shade.rectTransform.anchorMin = shade.rectTransform.anchorMax = anchor;
            shade.rectTransform.sizeDelta = new Vector2(78f, 78f); shade.rectTransform.anchoredPosition = mid + new Vector2(0f, -4f);
            shade.raycastTarget = false;
            string sign = rune != null ? RuneBook.Def(rune).Glyph : bind ? "ᛝ" : have > 0 ? "+" : "";
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
                if (bind) t += "\n\n" + Lang.T("Цей шар зараз малює твоя в'язь.");
                else if (on) t += "\n\n" + (rune != null ? Glyph(RuneBook.Def(rune)) + Lang.T(RuneBook.Def(rune).Name) : Lang.T("В'язь")) + " — <color=" + GoldHex + ">" + eff.Name + "</color>" + (eff.Desc.Length > 0 ? "\n" + eff.Desc : "") + (rune != null ? "\n\n" + RuneStars.Describe(d, rune) : "");
                else if (have == 0) return t + "\n\n" + Lang.T("Жодна така руна ще не горить. Руни дає Одін за доручення — «Шлях Одіна».");
                return t + "\n\n" + Lang.T("Клік — яку руну повісити.");
            });
            if (have > 0 || on) Clickable(dia.gameObject, () => RuneSlotAsk(dia, m, k));
        }

        // a click on a rune place: the open runes (and в'язі) of its kind; «Зняти» when one hangs there
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
                SlotChoice(panel, Glyph(rd) + Lang.T(rd.Name), w, w.Id == id, RuneBook.WornBy(p, r), () => RuneBook.PutOn(p, rid, m));
            }
            RectTransform row = card.Foot;
            string on = RuneOn(d, k, id);
            if (on != null)
                AskButton(row, Lang.T("Зняти"), false, () => { ClosePopup(); string why = RuneBook.TakeOff(p, on); if (why != null) Tell(why); Show(_tab, true); });
            AskButton(row, Lang.T("Назад"), false, ClosePopup);
            PlaceCard(card, at);
        }

        // one choice in that list: the rune, what it gives, who wears it now (the one there now: dark, the gold line)
        private static void SlotChoice(RectTransform panel, string rune, WearDef w, bool here, int by, Func<string> put)
        {
            RectTransform r = PaperChoice(panel, here, true);
            HBox(r, 8f, TextAnchor.MiddleLeft, 10, 10, 5, 5);
            TMP_Text n = T(r, rune + " — <color=" + (here ? GoldHex : GoldInkHex) + ">" + w.Name + "</color>", 17f, here ? PCream : PInk, F.Head);
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            n.raycastTarget = false;
            string who = here ? "✓" : by == 1 ? Lang.T("на Хугіні") : by == 2 ? Lang.T("на Муніні") : "";
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
}
#endif

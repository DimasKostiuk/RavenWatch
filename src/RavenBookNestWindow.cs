#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The nest's own window (the bowl's E, NestBowl), after the user's design (Downloads «Вороняче гніздо@2x.png»,
    // 2026-10-18): the post card's dark wooden frame with its plaque, twigs over the plaque, a birch twig by the round ✕,
    // a raven feather on the left edge. Left: the ravens in the nest (portrait, satiety stroke, «Нагодувати» → their food).
    // Middle: Odin's bowl (assets/book/nest_bowl.png) with three places for gifts, the four favours as arcs round it,
    // «Віддати Одіну» under it. Right, on parchment: Odin — the call, his glow and «Прийняти волю Одіна», or the errand you
    // have. Under all: the gifts from the bag. Long words live in the window's own tooltip (the book's is not up here).
    // It closes far from the nest, when no raven of yours sits in it any more, by Esc or ✕. Drawn again only when what it
    // shows changed (a stamp, checked a few times a second).
    internal static partial class RavenBook
    {
        private static GameObject _nwRoot;
        private static RectTransform _nwPanel;
        private static NestView _nwAt;
        private static string _nwStamp = "";
        private static float _nwNext;
        private static int _nwFeed = -1;          // whose food list is open: -1 none, 0 Hugin, 1 Munin
        private static bool _nwRefuseAsk;          // «відмовитись»/«відхилити» pressed once: the second press does it
        private const float NwW = 1240f;
        private const float NwMidX = 318f, NwMidW = 566f, NwColTop = 40f, NwColH = 600f, NwOdinX = 900f, NwOdinW = 312f;
        private const float NwBowlX = 283f, NwBowlY = 268f, NwArcR = 216f, NwArcW = 9f;   // in the middle panel
        private const int NwCols = 15;                                                    // gift cells in a row

        // on the dark wood: words, softer words, the section marks
        private static readonly Color NwInk = C("#ecd9b0"), NwSoft = C("#b49a6a"), NwMark = C("#a08660"), NwDark = C("#1b130c");
        // the four favours on the dark middle (Favour.Colors are for paper): Кров, Ремесло, Шлях, Золото
        private static readonly Color[] NwGroup = { C("#d9574c"), C("#c9a77c"), C("#7fb27a"), C("#e2b54e") };
        private static Color NwRaven(bool m) { return m ? C("#e07a66") : C("#8fb8e8"); }
        private static Color NwRavenLine(bool m) { return m ? C("#a8402f") : C("#5a8cd9"); }

        internal static bool NestWindowOpen { get { return _nwRoot != null && _nwRoot.activeSelf; } }

        internal static void OpenNestWindow(NestView v)
        {
            if (IsOpen) Close();
            CloseTreeWindow();
            OrdersMenu.CloseNow();
            if (_gameFont == null && InventoryGui.instance != null)
            {
                TMP_Text sample = InventoryGui.instance.m_recipeName != null ? InventoryGui.instance.m_recipeName : InventoryGui.instance.m_playerName;
                _gameFont = sample != null ? sample.font : null;
                BookArt.Fonts(_gameFont);
            }
            if (_nwRoot == null)
            {
                _nwRoot = new GameObject("RavenWatch_NestWindow", typeof(RectTransform));
                Canvas c = _nwRoot.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 30002;
                CanvasScaler sc = _nwRoot.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920f, 1080f);
                sc.matchWidthOrHeight = 1f;
                _nwRoot.AddComponent<GraphicRaycaster>();
                UnityEngine.Object.DontDestroyOnLoad(_nwRoot);
                Image dim = Rect("Dim", _nwRoot.transform).gameObject.AddComponent<Image>();
                Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                dim.color = new Color(0f, 0f, 0f, 0.55f);
            }
            _nwAt = v; _nwFeed = -1; _nwRefuseAsk = false; _nwStamp = "";
            ClearOffer();
            _nwRoot.SetActive(true);
            NestWindowDraw();
        }

        internal static void CloseNestWindow()
        {
            if (_nwRoot != null) _nwRoot.SetActive(false);
            _nwAt = null;
            ClearOffer();
            if (_nwGhost != null) UnityEngine.Object.Destroy(_nwGhost.gameObject);
            _nwGhost = null; _nwDragging = null; _nwWait = null; _nwTipFn = null;
        }

        // from the plugin's Update (RavenBook.Update)
        private static void NestWindowTick()
        {
            if (!NestWindowOpen) return;
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || InventoryGui.IsVisible() || Menu.IsVisible() || RavenKeys.Down(KeyCode.Escape)
                || _nwAt == null || !_nwAt.Near(p) || (Nest.MineIn(p, _nwAt).Count == 0 && !Nest.Flying(out _nwB, out _nwF)))
            { CloseNestWindow(); return; }
            NwTipTick();
            if (Time.time < _nwNext) return;
            _nwNext = Time.time + 0.3f;
            if (_nwWait != null) _nwWait.text = WaitDots();
            if (_nwDragging == null && NestStamp(p) != _nwStamp) NestWindowDraw(); // (not under a drag: it would lose what is dragged)
        }
        private static TMP_Text _nwWait;
        private static string WaitDots() { return Lang.T("Ворони кличуть Одіна") + new string('.', 1 + Mathf.FloorToInt(Nest.Waited) % 3); }
        private static bool _nwB; private static float _nwF;

        // what the window shows, in one line: drawn again when it changes
        private static string NestStamp(Player p)
        {
            bool m; float left;
            List<bool> mine = Nest.MineIn(p, _nwAt);
            string s = Errands.Version + ":" + Favour.Version + ":" + RuneBook.Version + ":" + mine.Count + ":" + (Nest.OdinOver(_nwAt) ? 1 : 0)
                + ":" + Mathf.CeilToInt(Nest.CooldownLeft(p) / 10f) + ":" + (Nest.Flying(out m, out left) ? Mathf.CeilToInt(left / 5f) : -1)
                + ":" + _nwFeed + ":" + _nwRefuseAsk + ":" + (Nest.Waited > 0f ? 1 : 0) + ":" + OfferCode(); // (the waiting dots change on their own: WaitDots)
            foreach (bool r in mine) { ItemDrop.ItemData it = Perches.MineOn(p, _nwAt.Perch, r); s += ":" + (it != null ? Mathf.RoundToInt(Satiety.Value(it)) : -1); }
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) s += it.m_stack; // (the bag: gifts and food counts)
            return s;
        }

        private static void NestWindowDraw()
        {
            Player p = Player.m_localPlayer;
            if (_nwRoot == null || p == null || _nwAt == null) return;
            _nwStamp = NestStamp(p);
            _nwTipFn = null;
            if (_nwPanel != null) { _nwPanel.gameObject.SetActive(false); UnityEngine.Object.Destroy(_nwPanel.gameObject); } // (hidden now: the new one is not drawn over it for a frame)

            bool riddle = Errands.RiddleOpen(p); // (Odin's riddle waits: anything may be laid, the answer among it)
            List<ItemDrop.ItemData> gifts = riddle ? AnyThings(p) : Favour.Giftable(p);
            int rows = Mathf.Clamp((gifts.Count + NwCols - 1) / NwCols, 1, 2);
            float stripH = 44f + rows * 78f + 28f, h = 656f + stripH + 22f;

            _nwPanel = Rect("Panel", _nwRoot.transform);
            _nwPanel.anchorMin = _nwPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _nwPanel.pivot = new Vector2(0.5f, 0.5f);
            _nwPanel.anchoredPosition = new Vector2(0f, -24f); // (the plaque and the twigs stand out over the top)
            _nwPanel.sizeDelta = new Vector2(NwW, h);
            CardPiece(_nwPanel.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            _nwPanel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // (clicks on the window stay on it)

            // behind everything on it: the twigs over the plaque (the plaque itself comes last, on top)
            NwPicture(_nwPanel, "nest_twigs", NwW / 2f - 310f, -86f, 620f, 145f, 0f, 0.95f);

            RectTransform left = Rect("Ravens", _nwPanel);
            At(left, 28f, 46f, 272f, NwColH - 6f);
            NestRavens(left, p);

            RectTransform mid = Rect("Bowl", _nwPanel);
            At(mid, NwMidX, NwColTop, NwMidW, NwColH);
            Box(mid, 10f, NwDark, A(C("#6e5a38"), 0.45f), 1f);
            NestBowl(mid, p, riddle);

            RectTransform odin = Rect("Odin", _nwPanel);
            At(odin, NwOdinX, NwColTop, NwOdinW, NwColH);
            CardPiece(odin.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(odin, 8f, TextAnchor.UpperLeft, 26, 24, 24, 22);
            NestOdin(odin, p);

            RectTransform strip = Rect("Gifts", _nwPanel);
            At(strip, 28f, 656f, NwW - 56f, stripH);
            Box(strip, 8f, new Color(0f, 0f, 0f, 0.22f), A(C("#6e5a38"), 0.55f), 1f);
            NestGifts(strip, p, gifts, riddle, rows);

            // the feather on the left edge, held by a strap; the birch twig on the right; the plaque; the ✕
            NwPicture(_nwPanel, "nest_feather", -46f, 268f, 124f, 292f, -14f, 1f);
            RectTransform strap = Rect("Strap", _nwPanel);
            At(strap, -30f, 452f, 64f, 16f);
            strap.localEulerAngles = new Vector3(0f, 0f, -14f);
            Box(strap, 3f, C("#5a3a20"), C("#2a1a0e"), 1f);
            Image stud = Img(strap, "Stud", BookArt.Circle, C("#d8b46a"));
            stud.rectTransform.anchorMin = stud.rectTransform.anchorMax = new Vector2(0.62f, 0.5f);
            stud.rectTransform.sizeDelta = new Vector2(9f, 9f);
            NwPicture(_nwPanel, "nest_birch", NwW - 248f, -64f, 300f, 206f, 0f, 1f);
            Plaque(_nwPanel, _nwAt.Name, 50f, true);
            NwClose(_nwPanel, 46f);
        }

        // a picture of the design at this place (its top-left), turned by deg; none when the file is missing
        private static void NwPicture(RectTransform parent, string name, float x, float top, float w, float hh, float deg, float alpha)
        {
            Sprite s = BookArt.Picture(name);
            if (s == null) return;
            RectTransform r = Rect(name, parent);
            At(r, x, top, w, hh);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition += new Vector2(w / 2f, -hh / 2f);
            if (deg != 0f) r.localEulerAngles = new Vector3(0f, 0f, deg);
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = s; im.preserveAspect = true; im.raycastTarget = false; im.color = new Color(1f, 1f, 1f, alpha);
        }

        private static void NwClose(RectTransform frame, float size)
        {
            RectTransform x = Rect("Close", frame);
            x.anchorMin = x.anchorMax = new Vector2(1f, 1f);
            x.pivot = new Vector2(0.5f, 0.5f);
            x.anchoredPosition = new Vector2(-size * 0.12f, -size * 0.12f);
            x.sizeDelta = new Vector2(size, size);
            Image ring = x.gameObject.AddComponent<Image>();
            ring.sprite = BookArt.Picture("card_close");
            if (ring.sprite == null) { ring.sprite = BookArt.Circle; ring.color = C("#2a1b10"); }
            Image cross = Img(x, "Cross", BookArt.Cross, PlaqueInk);
            float pad = size * 0.31f;
            Stretch(cross.rectTransform, Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad));
            Clickable(x.gameObject, CloseNestWindow);
        }

        // «I · ВОРОНИ В ГНІЗДІ ———»
        private static void NwSection(Transform parent, string mark, string text, float x, float top, float w)
        {
            RectTransform row = Rect("Section", parent);
            At(row, x, top, w, 20f);
            HBox(row, 10f, TextAnchor.MiddleLeft);
            TMP_Text t = T(row, mark + " · " + text.ToUpperInvariant(), 13f, NwMark, F.Head);
            t.characterSpacing = 6f; t.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform line = Rect("Rule", row);
            LayoutElement le = line.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.preferredHeight = 1f; le.minHeight = 1f;
            line.gameObject.AddComponent<Image>().color = A(NwMark, 0.45f);
        }

        // ---- left: the ravens in the nest, their food
        private static void NestRavens(RectTransform col, Player p)
        {
            NwSection(col, "I", Lang.T("Ворони в гнізді"), 0f, 0f, col.sizeDelta.x);
            RectTransform list = Rect("List", col);
            At(list, 0f, 32f, col.sizeDelta.x, col.sizeDelta.y - 70f);
            VBox(list, 12f, TextAnchor.UpperLeft);
            List<bool> mine = Nest.MineIn(p, _nwAt);
            bool fm; float fl;
            bool flying = Nest.Flying(out fm, out fl);
            int shown = 0;
            for (int k = 0; k < 2; k++)
            {
                bool m = k == 1;
                ItemDrop.ItemData item = mine.Contains(m) ? Perches.MineOn(p, _nwAt.Perch, m) : null;
                if (item != null) { RavenCard(list, p, m, item); shown++; }
                else if (flying && fm == m) { AwayCard(list, m, fl); shown++; }
            }
            if (shown == 0) T(list, Lang.T("Твоїх воронів у гнізді нема."), 15f, NwSoft, F.Italic);

            TMP_Text hint = T(col, Lang.T("Забрати воронів — <b><color=#e3c27c>E</color></b> на гнізді вгорі."), 14f, NwSoft, F.Italic);
            At(hint.rectTransform, 0f, col.sizeDelta.y - 30f, col.sizeDelta.x, 26f);
        }

        // the round portrait with its thin ring
        private static void NwPortrait(Transform row, bool m, float size, float alpha)
        {
            RectTransform r = Rect("Portrait", row);
            Fixed(r.gameObject, size, size);
            Image bg = Img(r, "Bg", BookArt.Circle, A(C("#0e0a06"), 0.9f * alpha));
            Sprite face = BookArt.Portrait(m);
            if (face != null) { Image f = Img(r, "Face", face, new Color(1f, 1f, 1f, alpha)); Stretch(f.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)); }
            Image ring = Img(r, "Ring", BookArt.Ring(0.03f), A(C("#8a7450"), alpha));
            bg.raycastTarget = false; ring.raycastTarget = false;
        }

        // the bookmark ribbon on a card's top right corner, in the raven's colour
        private static void NwRibbon(RectTransform card, Color color)
        {
            RectTransform r = Rect("Ribbon", card);
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-18f, 6f);
            r.sizeDelta = new Vector2(14f, 40f);
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = BookArt.Picture("card_ribbon"); im.color = color; im.raycastTarget = false;
        }

        private static void RavenCard(RectTransform list, Player p, bool m, ItemDrop.ItemData item)
        {
            RectTransform card = Rect("Raven", list);
            Box(card, 8f, A(C("#3a2a1a"), 0.55f), NwRavenLine(m), 1.5f);
            VBox(card, 8f, TextAnchor.UpperLeft, 14, 14, 12, 14);
            NwRibbon(card, NwRavenLine(m));

            RectTransform head = Rect("Head", card);
            HBox(head, 12f, TextAnchor.MiddleLeft);
            NwPortrait(head, m, 66f, 1f);
            RectTransform names = Rect("Names", head);
            VBox(names, 0f, TextAnchor.MiddleLeft);
            names.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(names, Plugin.RavenName(m), 26f, NwRaven(m), F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text sub = T(names, "<color=" + Satiety.ZoneHexOf(item) + ">" + Satiety.ZoneWord(item) + "</color> · " + Lang.T("сидить у гнізді"), 14f, NwSoft, F.Italic);
            sub.textWrappingMode = TextWrappingModes.NoWrap; sub.overflowMode = TextOverflowModes.Ellipsis;

            float v = Satiety.Value(item);
            RectTransform lab = Rect("Satiety", card);
            HBox(lab, 4f, TextAnchor.MiddleLeft);
            T(lab, "<b>" + Lang.T("Ситість") + "</b>", 14f, C("#d9c49a"), F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(lab);
            T(lab, Mathf.RoundToInt(v) + " / 100", 14f, NwSoft, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform bar = Rect("Bar", card);
            Fixed(bar.gameObject, -1f, 12f);
            Image track = Img(bar, "Track", null, A(NwSoft, 0.3f));
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f); track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 1f);
            if (v > 0.5f)
            {
                Image fill = Img(bar, "Fill", BookArt.Brush(m ? "brush-munin" : "brush-huginn"), Color.white);
                fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 28f / 12f;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(v / 100f), 1f);
                if (fill.sprite == null) fill.color = NwRavenLine(m);
            }

            bool open = _nwFeed == (m ? 1 : 0);
            RectTransform row = Rect("Buttons", card);
            HBox(row, 0f, TextAnchor.MiddleLeft, 0, 0, 2, 0);
            List<Trust.Food> foods = NwFoods(p, m, item);
            NwDarkButton(row, open ? Lang.T("Сховати") : Lang.T("Нагодувати"), foods.Count > 0 ? ItemIcon(foods[0].Prefab) : null, () => { _nwFeed = open ? -1 : (m ? 1 : 0); NestWindowDraw(); });
            Spring(row);
            if (open) NestFoods(card, p, m, item, foods);
        }

        // a raven up in the clouds with Odin: its card dimmed, when it comes back
        private static void AwayCard(RectTransform list, bool m, float left)
        {
            RectTransform card = Rect("Away", list);
            Box(card, 8f, A(C("#3a2a1a"), 0.3f), A(NwSoft, 0.35f), 1.2f);
            HBox(card, 12f, TextAnchor.MiddleLeft, 14, 14, 12, 12);
            NwRibbon(card, A(NwRavenLine(m), 0.8f));
            NwPortrait(card, m, 66f, 0.45f);
            RectTransform names = Rect("Names", card);
            VBox(names, 0f, TextAnchor.MiddleLeft);
            names.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(names, Plugin.RavenName(m), 26f, A(NwInk, 0.55f), F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            T(names, Lang.F("у хмарах, в Одіна · ще {0}", RavenAbilities.Span(left)), 14f, NwSoft, F.Italic);
            NwTip(card.gameObject, () => Lang.F("{0} полетів до Одіна й повернеться на плече вже з дорученням.", Plugin.RavenName(m)));
        }

        // a dark button with a gold line (and a small picture before its words)
        private static RectTransform NwDarkButton(Transform parent, string caption, Sprite icon, Action click)
        {
            RectTransform r = Rect("Button", parent);
            HBox(r, 8f, TextAnchor.MiddleCenter, 12, 16, 0, 0);
            Fixed(r.gameObject, -1f, 38f);
            Box(r, 5f, C("#2a1c10"), C("#8a6a38"), 1.5f);
            if (icon != null) { RectTransform ir = Rect("Icon", r); Fixed(ir.gameObject, 24f, 24f); Image im = Img(ir, "I", icon, Color.white); im.preserveAspect = true; }
            T(r, caption, 18f, NwInk, F.Body, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.onClick.AddListener(() => click());
            return r;
        }

        // the foods it eats that are in the bag
        private static List<Trust.Food> NwFoods(Player p, bool m, ItemDrop.ItemData raven)
        {
            List<Trust.Food> l = new List<Trust.Food>();
            foreach (Trust.Food f in Trust.EdibleFoods(m, raven)) if (Trust.Have(p, f.Prefab) > 0) l.Add(f);
            return l;
        }

        // the food grid under the card's button: a click — one piece
        private static void NestFoods(RectTransform card, Player p, bool m, ItemDrop.ItemData raven, List<Trust.Food> foods)
        {
            if (foods.Count == 0) { T(card, Lang.F("У сумці нема їжі, яку {0} зараз їсть.", Plugin.RavenName(m)), 14f, NwSoft, F.Italic); return; }
            RectTransform grid = Rect("Foods", card);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(118f, 32f); g.spacing = new Vector2(6f, 6f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
            int n = 0;
            foreach (Trust.Food f in foods)
            {
                if (++n > 8) break; // (four rows: the card must leave room for the other raven)
                Trust.Food food = f;
                int have = Trust.Have(p, f.Prefab);
                RectTransform c = Rect("Food", grid);
                Box(c, 5f, C("#21170e"), A(C("#8a6a38"), 0.6f), 1f);
                HBox(c, 5f, TextAnchor.MiddleLeft, 5, 5, 2, 2);
                Sprite icon = ItemIcon(f.Prefab);
                if (icon != null) { RectTransform ir = Rect("Icon", c); Fixed(ir.gameObject, 24f, 24f); Image im = Img(ir, "I", icon, Color.white); im.preserveAspect = true; }
                TMP_Text t = T(c, (Satiety.KnownFav(raven, f.Prefab) ? "<color=#e3c27c>★</color> " : "") + "×" + have, 14f, NwInk, F.Body);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Clickable(c.gameObject, () => { Tell(Trust.FeedInNest(p, m, food)); NestWindowDraw(); });
                bool fav = Satiety.KnownFav(raven, f.Prefab);
                string name = f.Name;
                NwTip(c.gameObject, () => "<b>" + name + "</b>" + (fav ? "\n" + Lang.T("улюблена страва") : "") + "\n" + Lang.T("Клік — згодувати один шматок."));
            }
        }

        // ---- middle: the bowl, the favours round it, «Віддати Одіну»
        private static readonly float[] ArcFrom = { 10f, 170f, 190f, 350f };   // clockwise from 9 o'clock: Кров, Ремесло, Шлях, Золото
        private static readonly bool[] ArcCw = { true, false, true, false };
        private const float ArcSpan = 70f;

        private static void NestBowl(RectTransform mid, Player p, bool riddle)
        {
            float[] v = Favour.Of(p);
            float[] gain = OfferGains(p);
            for (int i = 0; i < 4; i++)
            {
                FavourArc(mid, i, 1f, A(C("#3a2c1e"), 1f));
                float now = Mathf.Clamp01(v[i] / Favour.Max), more = Mathf.Clamp01((v[i] + gain[i]) / Favour.Max);
                if (more > now + 0.002f) FavourArc(mid, i, more, A(NwGroup[i], 0.4f));
                if (now > 0.002f) FavourArc(mid, i, now, NwGroup[i]);
                FavourLabel(mid, p, i, v[i], gain[i]);
            }

            RectTransform glow = Rect("Glow", mid);
            At(glow, NwBowlX - 230f, NwBowlY - 230f, 460f, 460f);
            Image gi = glow.gameObject.AddComponent<Image>();
            gi.sprite = BookArt.Glow; gi.raycastTarget = false;
            gi.color = Nest.OdinOver(_nwAt) ? new Color(1f, 0.82f, 0.4f, 0.55f) : new Color(1f, 0.8f, 0.45f, 0.12f);

            RectTransform bowl = Rect("Bowl", mid);
            At(bowl, NwBowlX - 170f, NwBowlY - 170f, 340f, 340f);
            Image bi = bowl.gameObject.AddComponent<Image>();
            bi.sprite = BookArt.Picture("nest_bowl"); bi.preserveAspect = true; bi.raycastTarget = false;
            if (bi.sprite == null) { bi.sprite = BookArt.Circle; bi.color = C("#6a4a24"); }

            Vector2[] at = { new Vector2(0f, 56f), new Vector2(-48f, -28f), new Vector2(48f, -28f) }; // all three as far from the middle (the lower two sat on the bowl's edge)
            for (int i = 0; i < _nwOffer.Length; i++) OfferSlot(mid, p, i, at[i]);

            bool laid = false;
            foreach (Offer o in _nwOffer) if (o != null) laid = true;
            string gainText = OfferGain(p);
            RectTransform row = Rect("Give", mid);
            At(row, 0f, 506f, NwMidW, 52f);
            HBox(row, 0f, TextAnchor.MiddleCenter);
            RectTransform give = CardButton(row, Lang.T("Віддати Одіну"), true, () => { GiveOffer(p); NestWindowDraw(); }, "ᚠ", gainText.Length > 0 || riddle && laid);
            NwTip(give.gameObject, () => Lang.T("Що лежить у чаші, піде Одіну й наповнить його милість. Віддане назад не забрати."));

            int lean = Favour.Leaning(p);
            string line = gainText.Length > 0 ? Lang.F("Дасть милості: {0}", gainText)
                : riddle ? Lang.T("Загадка Одіна чекає на відгадку: поклади її в чашу й віддай Одіну.")
                : lean >= 0 ? Lang.F("Одін схиляється до: {0}.", Lang.T(Favour.Leads[lean]))
                : Lang.T("Дари в чашу схиляють, яке доручення дасть Одін.");
            TMP_Text t = T(mid, line, 15f, gainText.Length > 0 || riddle ? PlaqueInk : NwSoft, F.Italic, TextAlignmentOptions.Top);
            At(t.rectTransform, 20f, 566f, NwMidW - 40f, 26f);
        }

        // one favour's arc (share of its span from its start) round the bowl
        private static void FavourArc(RectTransform mid, int i, float share, Color color)
        {
            float d = NwArcR * 2f + NwArcW;
            RectTransform r = Rect("Arc", mid);
            At(r, NwBowlX - d / 2f, NwBowlY - d / 2f, d, d);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition += new Vector2(d / 2f, -d / 2f);
            r.localEulerAngles = new Vector3(0f, 0f, -ArcFrom[i]);
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = BookArt.Ring(NwArcW / d); im.color = color; im.raycastTarget = false;
            im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Radial360;
            im.fillOrigin = (int)Image.Origin360.Left; im.fillClockwise = ArcCw[i];
            im.fillAmount = ArcSpan * share / 360f;
            // round ends: one at the start, one where it stops
            float end = ArcFrom[i] + (ArcCw[i] ? 1f : -1f) * ArcSpan * share;
            ArcCap(mid, ArcFrom[i], color);
            ArcCap(mid, end, color);
        }

        private static void ArcCap(RectTransform mid, float deg, Color color)
        {
            float a = deg * Mathf.Deg2Rad;
            float x = NwBowlX - NwArcR * Mathf.Cos(a), y = NwBowlY - NwArcR * Mathf.Sin(a);
            RectTransform r = Rect("Cap", mid);
            At(r, x - NwArcW / 2f, y - NwArcW / 2f, NwArcW, NwArcW);
            Image im = r.gameObject.AddComponent<Image>();
            im.sprite = BookArt.Circle; im.color = color; im.raycastTarget = false;
        }

        // the favour's name and «12 / 100» in its corner
        private static void FavourLabel(RectTransform mid, Player p, int i, float v, float gain)
        {
            bool right = i == 1 || i == 2, low = i == 2 || i == 3;
            RectTransform r = Rect("Label", mid);
            At(r, NwBowlX + (right ? 197f : -197f) - 75f, NwBowlY + (low ? 178f : -196f), 150f, 50f);
            VBox(r, 0f, TextAnchor.UpperCenter);
            T(r, Lang.T(Favour.Names[i]), 24f, NwGroup[i], F.Head, TextAlignmentOptions.Top).textWrappingMode = TextWrappingModes.NoWrap;
            string n = Mathf.RoundToInt(v) + " / " + Mathf.RoundToInt(Favour.Max);
            if (gain > 0.05f) n += " <color=#e3c27c>+" + Mathf.RoundToInt(gain) + "</color>";
            T(r, n, 14f, NwSoft, F.Body, TextAlignmentOptions.Top).textWrappingMode = TextWrappingModes.NoWrap;
            int k = i;
            NwTip(r.gameObject, () => Lang.F("<b>{0}</b> — {1}.\n{2}", Lang.T(Favour.Names[k]), Lang.T(Favour.Leads[k]), Lang.T("Чим повніша милість, тим частіше Одін дає такі доручення.")));
        }

        // ---- right: Odin
        private static void NwTitle(RectTransform col)
        {
            RectTransform row = Rect("Title", col);
            HBox(row, 10f, TextAnchor.LowerLeft, 0, 0, 0, 4);
            T(row, Lang.T("Одін"), 34f, PInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            T(row, "ᚠ", 18f, PLabel, F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(row);
        }

        private static void PaperLabel(RectTransform col, string text)
        {
            RectTransform gap = Rect("Gap", col); Fixed(gap.gameObject, -1f, 4f);
            TMP_Text t = T(col, text.ToUpperInvariant(), 13f, PLabel, F.Head);
            t.characterSpacing = 6f; t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // a link on the parchment: underlined, its ink turns gold under the pointer
        private static RectTransform PaperLink(Transform parent, string caption, Color ink, Action click)
        {
            TMP_Text t = T(parent, "<u>" + caption + "</u>", 15f, ink, F.Body, TextAlignmentOptions.MidlineRight);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = true;
            Clickable(t.gameObject, click);
            Hover(t.gameObject, new Graphic[] { t }, ink, null);
            return t.rectTransform;
        }

        private static void NestOdin(RectTransform col, Player p)
        {
            NwTitle(col);
            ErrandRun r = Errands.Of(p);
            if (r != null) { NestErrand(col, p, r); Spring(col); return; }
            PaperLabel(col, Lang.T("Воля Одіна"));
            string blocked = Nest.Blocked(p);
            if (blocked != null) { T(col, blocked, 16f, PSoft, F.Italic); Spring(col); return; }
            if (Nest.OdinOver(_nwAt))
            {
                T(col, Lang.T("Над гніздом золоте сяйво: Одін змилувався."), 19f, GoldInk, F.Head);
                RectTransform row = Rect("Accept", col);
                HBox(row, 0f, TextAnchor.MiddleLeft, 0, 0, 6, 2);
                RectTransform yes = CardButton(row, Lang.T("Прийняти волю Одіна"), true, () => { _nwRefuseAsk = false; string why = Nest.Accept(p, _nwAt); if (why != null) Tell(why); NestWindowDraw(); });
                NwTip(yes.gameObject, () => Lang.T("Чого він хоче, Одін не скаже. Один із воронів злетить до нього в хмари й повернеться на плече вже з дорученням."));
                Spring(row);
                RectTransform no = Rect("Decline", col);
                HBox(no, 0f, TextAnchor.MiddleLeft);
                PaperLink(no, _nwRefuseAsk ? Lang.T("так, відхилити") : Lang.T("відхилити"), Bad, () =>
                {
                    if (!_nwRefuseAsk) { _nwRefuseAsk = true; NestWindowDraw(); return; }
                    _nwRefuseAsk = false; string why = Nest.Decline(p); if (why != null) Tell(why); NestWindowDraw();
                });
                Spring(no);
                if (_nwRefuseAsk) T(col, Lang.T("Одін піде без кари, дари не пропадуть, але покликати його знову можна лише наступного дня. Натисни ще раз, щоб відхилити."), 14f, Bad, F.Italic);
                Spring(col);
                return;
            }
            if (Nest.Called(p))
            {
                _nwWait = T(col, WaitDots(), 19f, PInk, F.Head);
                TMP_Text when = T(col, Lang.T("Коли над гніздом засяє золото — він тут."), 15f, PSoft, F.Italic);
                NwTip(when.gameObject, () => Lang.T("Він приходить, коли захоче: інколи за кілька секунд, інколи за хвилину. А буває, що й зовсім не озветься."));
                OmenBlock(col, p);
                Spring(col);
                return;
            }
            // not called yet: the gifts first (the bowl), then the call
            OmenBlock(col, p);
            RectTransform call = Rect("Call", col);
            HBox(call, 0f, TextAnchor.MiddleLeft, 0, 0, 8, 2);
            RectTransform go = CardButton(call, Lang.T("Призвати милість Одіна"), true, () => { string why = Nest.Summon(p, _nwAt); if (why != null) Tell(why); NestWindowDraw(); });
            NwTip(go.gameObject, () => Lang.T("Ворони закаркають із гнізда й покличуть Одіна. Він не завжди відгукується, а щедрі дари роблять його прихильнішим. Кликати можна раз на день — тож спершу дари."));
            Spring(call);
            T(col, Lang.T("Спершу дари в чашу — кликати можна раз на день."), 14f, PSoft, F.Italic);
            Spring(col);
        }

        // how Odin looks at your nest now: gold marks and an omen, never the chance itself
        private static void OmenBlock(RectTransform col, Player p)
        {
            int lvl = Nest.OmenLevel(p);
            TMP_Text marks = T(col, "<color=#a8843a>" + new string('◆', lvl) + "</color><color=#c9b48a>" + new string('◇', 5 - lvl) + "</color>", 22f, PInk, F.Body);
            marks.characterSpacing = 8f;
            NwTip(marks.gameObject, () => Lang.T("Прикмета: наскільки Одін зараз прихильний до твого гнізда. Свіжі дари роблять її кращою."));
            T(col, Nest.Omen(p), 15f, lvl >= 3 ? GoldInk : PSoft, F.Italic);
        }

        // the errand you have: its card (name, whose, the step now and its count, the reward, «відмовитись»), the steps
        private static void NestErrand(RectTransform col, Player p, ErrandRun r)
        {
            PaperLabel(col, Lang.T("Доручення"));
            bool golden = r.Def != null && r.Def.Golden;
            RectTransform card = Rect("Errand", col);
            VBox(card, 6f, TextAnchor.UpperLeft, 14, 14, 12, 12);
            Box(card, 6f, golden ? new Color(0.95f, 0.8f, 0.4f, 0.35f) : new Color(1f, 0.99f, 0.95f, 0.55f), golden ? PGoldLine : PEdge, golden ? 2f : 1.2f);
            if (r.Def == null)
            {
                T(card, Lang.F("Доручення «{0}» з іншої версії мода: його можна лише відкласти.", r.Id), 15f, PSoft, F.Italic);
            }
            else
            {
                T(card, Lang.T(r.Def.Name) + "  <size=68%><i><color=" + (r.Munin ? "#7a2414" : "#2a5a86") + ">" + Trial.From(r) + "</color></i></size>", 22f, golden ? GoldInk : PInk, F.Head);
                if (r.Step < r.Steps.Length)
                {
                    ErrandStep st = r.Steps[r.Step];
                    T(card, st.Describe(r, r.Step, p), 15f, PInk, F.Body);
                    if (st.Need > 1)
                    {
                        RectTransform bar = Rect("Bar", card);
                        Fixed(bar.gameObject, -1f, 8f);
                        Image track = Img(bar, "Track", null, A(PInk, 0.18f));
                        track.rectTransform.anchorMin = new Vector2(0f, 0.5f); track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                        track.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
                        float k = Mathf.Clamp01((float)r.Count[r.Step] / st.Need);
                        RectTransform fill = Rect("Fill", bar);
                        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Max(0.03f, k), 1f);
                        fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                        Box(fill, 3f, r.Munin ? BookArt.Munin : BookArt.Hugin, new Color(0f, 0f, 0f, 0f), 0f);
                    }
                }
            }
            RectTransform rule = Rect("Rule", card); Fixed(rule.gameObject, -1f, 1f);
            rule.gameObject.AddComponent<Image>().color = A(PRule, 0.8f);
            RectTransform foot = Rect("Foot", card);
            HBox(foot, 6f, TextAnchor.MiddleLeft);
            if (r.Def != null && !r.Def.Trial)
            {
                int bonus = ErrandMods.Bonus(r) * (golden ? 2 : 1);
                string reward = golden ? Lang.T("золота руна") + (bonus > 0 ? " + " + Lang.N(bonus, "зарубка|зарубки|зарубок") : "")
                    : Lang.N(r.Price + bonus, "зарубка|зарубки|зарубок");
                if (bonus > 0) reward += " " + Lang.F("(або Іскра Одіна, {0}%)", Mathf.RoundToInt(golden ? RuneStars.SparkGolden : RuneStars.SparkChance));
                TMP_Text rt = T(foot, Lang.T("Нагорода:") + " <b><color=" + (golden ? "#7a5514" : "#8E1E1E") + ">" + reward + "</color></b>", 14f, PSoft, F.Body);
                rt.textWrappingMode = TextWrappingModes.NoWrap;
                NwTip(rt.gameObject, () => Lang.F("За кожне виконане доручення на різі з'являються зарубки. Коли їх {0}, загоряється одна руна.", RuneBook.NotchesPerRune));
            }
            Spring(foot);
            PaperLink(foot, _nwRefuseAsk ? Lang.T("так, відмовитись") : Lang.T("відмовитись"), Bad, () =>
            {
                if (!_nwRefuseAsk) { _nwRefuseAsk = true; NestWindowDraw(); return; }
                _nwRefuseAsk = false; Nest.Refuse(p); NestWindowDraw();
            });
            if (_nwRefuseAsk) T(col, Lang.T("Доручення пропаде без кари, але нове Одін дасть лише наступного дня. Натисни ще раз, щоб відмовитись."), 14f, Bad, F.Italic);

            if (Nest.AsksToHandIn(p))
            {
                RectTransform row = Rect("HandIn", col);
                HBox(row, 0f, TextAnchor.MiddleLeft, 0, 0, 4, 0);
                InkButton(row, Lang.T("Віддати в чашу"), () => { Tell(Nest.HandIn(p) ?? Lang.T("Віддано.")); NestWindowDraw(); });
                Spring(row);
            }

            if (r.Def != null && r.Steps.Length > 1)
            {
                PaperLabel(col, Lang.T("Кроки"));
                for (int i = 0; i < r.Steps.Length; i++)
                {
                    bool done = i < r.Step, now = i == r.Step;
                    T(col, (done ? "✓  " : now ? "•  " : "·  ") + r.Steps[i].Describe(r, i, p), 14f, done ? Good : now ? PInk : A(PInk, 0.45f), now ? F.Head : F.Body);
                }
            }
        }


        // ---- bottom: the gifts from the bag
        private static void NestGifts(RectTransform strip, Player p, List<ItemDrop.ItemData> gifts, bool riddle, int rows)
        {
            float w = strip.sizeDelta.x;
            RectTransform head = Rect("Head", strip);
            At(head, 18f, 12f, w - 36f, 22f);
            HBox(head, 8f, TextAnchor.MiddleLeft);
            TMP_Text ht = T(head, "II · " + Lang.T("Дари з сумки").ToUpperInvariant(), 13f, NwMark, F.Head);
            ht.characterSpacing = 6f; ht.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform gap = Rect("Gap", head); Fixed(gap.gameObject, 10f, 1f);
            for (int i = 0; i < 4; i++)
            {
                RectTransform sq = Rect("Key", head); Fixed(sq.gameObject, 11f, 11f);
                Box(sq, 2f, NwGroup[i], new Color(0f, 0f, 0f, 0f), 0f);
                T(head, Lang.T(Favour.Names[i]), 15f, NwInk, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform g2 = Rect("Gap", head); Fixed(g2.gameObject, 6f, 1f);
            }
            Spring(head);
            T(head, Lang.T("Віддане Одіну назад не забрати"), 14f, NwSoft, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;

            if (gifts.Count == 0)
            {
                TMP_Text none = T(strip, Lang.T("У сумці нема нічого, що прийняв би Одін: трофеї, м'ясо, руда, метал, дерево, шкури, мед, їжа, монети, самоцвіти."), 15f, NwSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
                At(none.rectTransform, 18f, 44f, w - 36f, 70f);
                return;
            }
            RectTransform grid = Rect("Grid", strip);
            At(grid, 18f, 42f, w - 36f, rows * 78f);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(70f, 70f); g.spacing = new Vector2(8.5f, 8f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = NwCols;
            int shown = 0;
            foreach (ItemDrop.ItemData it in gifts)
            {
                if (++shown > rows * NwCols) break;
                GiftCell(grid, p, it);
            }
            TMP_Text hint = T(strip, Lang.T("Клік — покласти одну в чашу · <b><color=#e3c27c>Shift+клік</color></b> — скільки треба до повної милості · клік по ямці в чаші — забрати"), 13f, NwSoft, F.Italic);
            At(hint.rectTransform, 18f, 46f + rows * 78f, w - 36f, 22f);
            hint.textWrappingMode = TextWrappingModes.NoWrap; hint.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void GiftCell(RectTransform grid, Player p, ItemDrop.ItemData it)
        {
            FavourGroup grp; float worth;
            bool known = Favour.Worth(it, out grp, out worth);
            Color line = known ? NwGroup[(int)grp] : PlaqueInk;
            string prefab = it.m_dropPrefab.name;
            int free = Trust.Have(p, prefab) - Laid(prefab);
            RectTransform c = Rect("Gift", grid);
            Box(c, 6f, C("#2a1d12"), A(C("#6e5a38"), 0.7f), 1f);
            RectTransform under = Rect("Under", c);
            under.anchorMin = new Vector2(0f, 0f); under.anchorMax = new Vector2(1f, 0f);
            under.offsetMin = new Vector2(5f, 2f); under.offsetMax = new Vector2(-5f, 4f);
            under.gameObject.AddComponent<Image>().color = A(line, 0.85f);
            under.GetComponent<Image>().raycastTarget = false;
            Sprite icon = it.GetIcon();
            if (icon != null) { Image im = Img(c, "Icon", icon, free > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f)); im.preserveAspect = true; Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(9f, 9f), new Vector2(-9f, -9f)); }
            if (known)
            {
                TMP_Text plus = T(c, "+" + Lang.Num(worth, "0.#"), 12f, line, F.Body, TextAlignmentOptions.TopLeft);
                Stretch(plus.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 2f), new Vector2(-2f, -3f));
            }
            TMP_Text cnt = T(c, "<b>" + free + "</b>", 14f, NwInk, F.Body, TextAlignmentOptions.BottomRight);
            Stretch(cnt.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 5f), new Vector2(-6f, -2f));
            Clickable(c.gameObject, () =>
            {
                bool all = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                string why = AddOffer(p, prefab, all ? int.MaxValue : 1, -1);
                if (why != null) Tell(why);
                NestWindowDraw();
            });
            string name = Lang.Game(it.m_shared.m_name);
            FavourGroup gg = grp; float ww = worth; bool kk = known;
            NwTip(c.gameObject, () => "<b>" + name + "</b> ×" + free + (kk ? "\n" + Lang.F("{0}: +{1} милості за штуку", Lang.T(Favour.Names[(int)gg]), Lang.Num(ww, "0.#")) : "\n" + Lang.T("Не дар, але може бути відгадкою.")));
            NwDrag drag = c.gameObject.AddComponent<NwDrag>();
            drag.Prefab = prefab; drag.Icon = icon;
        }

        // ---- the bowl's places
        private sealed class Offer { public string Prefab; public int Count; }
        private static readonly Offer[] _nwOffer = new Offer[3];

        private static int Laid(string prefab) { int n = 0; foreach (Offer o in _nwOffer) if (o != null && o.Prefab == prefab) n += o.Count; return n; }

        private static void ClearOffer() { for (int i = 0; i < _nwOffer.Length; i++) _nwOffer[i] = null; }

        private static string OfferCode() { string s = ""; foreach (Offer o in _nwOffer) s += o != null ? o.Prefab + "×" + o.Count + ";" : "-;"; return s; }

        // the bag's things, one of each kind (a riddle's bowl takes anything): not worn, not the ravens
        private static List<ItemDrop.ItemData> AnyThings(Player p)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            HashSet<string> seen = new HashSet<string>();
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                bool mr; if (it.m_equipped || it.m_dropPrefab == null || RavenItem.IsRaven(it, out mr) || !seen.Add(it.m_dropPrefab.name)) continue;
                l.Add(it);
            }
            return l;
        }

        // n more of this kind into the bowl: into its own place if it has one, else the place given (or the first free one)
        private static string AddOffer(Player p, string prefab, int n, int slot)
        {
            if (Favour.Find(p, prefab) == null) return Lang.T("У сумці цього вже нема.");
            int at = -1;
            for (int i = 0; i < _nwOffer.Length; i++) if (_nwOffer[i] != null && _nwOffer[i].Prefab == prefab) at = i;
            if (at < 0 && slot >= 0) at = slot;
            if (at < 0) for (int i = 0; i < _nwOffer.Length; i++) if (_nwOffer[i] == null) { at = i; break; }
            if (at < 0) return Lang.T("У чаші лише три місця. Клік по місцю — звільнити його.");
            if (_nwOffer[at] != null && _nwOffer[at].Prefab != prefab) _nwOffer[at] = null; // (dropped on another kind's place: it takes the place)
            int already = _nwOffer[at] != null ? _nwOffer[at].Count : 0;
            int room = Trust.Have(p, prefab) - Laid(prefab);
            int fill = (Errands.RiddleOpen(p) ? Mathf.Max(Favour.ToFill(p, prefab), 9999) : Favour.ToFill(p, prefab)) - already;
            int add = Mathf.Min(n, room, fill);
            if (add <= 0) return fill <= 0 ? Lang.T("Цього вже досить, щоб наповнити милість.") : Lang.T("Усе, що є в сумці, вже в чаші.");
            if (_nwOffer[at] == null) _nwOffer[at] = new Offer { Prefab = prefab };
            _nwOffer[at].Count += add;
            return null;
        }

        // what the bowl would give, by group (capped at what each favour still has room for)
        private static float[] OfferGains(Player p)
        {
            float[] gain = new float[4];
            foreach (Offer o in _nwOffer)
            {
                if (o == null) continue;
                FavourGroup g;
                float add = Favour.Would(p, o.Prefab, o.Count, out g);
                gain[(int)g] += add;
            }
            float[] now = Favour.Of(p);
            for (int i = 0; i < 4; i++) gain[i] = Mathf.Min(gain[i], Favour.Max - now[i]);
            return gain;
        }

        // the same in words: «Кров +12, Ремесло +3» ("" when nothing)
        private static string OfferGain(Player p)
        {
            float[] gain = OfferGains(p);
            List<string> parts = new List<string>();
            for (int i = 0; i < 4; i++) if (gain[i] > 0.05f) parts.Add(Lang.T(Favour.Names[i]) + " +" + Mathf.RoundToInt(gain[i]));
            return string.Join(", ", parts.ToArray());
        }

        private static void GiveOffer(Player p)
        {
            bool riddleWas = Errands.RiddleOpen(p), answered = false, wrong = false;
            for (int i = 0; i < _nwOffer.Length; i++)
            {
                Offer o = _nwOffer[i];
                if (o == null) continue;
                _nwOffer[i] = null;
                string why;
                int used = Errands.TryRiddle(p, o.Prefab, o.Count, out why);
                if (why != null) { Tell(why); continue; } // (the answer, but too few: it all stays in the bag, not given as a gift)
                if (used > 0) { Tell(Lang.T("Одін усміхнувся: відгадано.")); answered = true; }
                int rest = o.Count - used;
                if (rest <= 0) continue;
                ItemDrop.ItemData it = Favour.Find(p, o.Prefab);
                FavourGroup g; float w;
                if (it == null || !Favour.Worth(it, out g, out w)) { if (riddleWas && used == 0) wrong = true; continue; } // (not a gift: stays in the bag)
                why = Favour.Give(p, o.Prefab, rest);
                if (why != null) Tell(why);
            }
            if (riddleWas && !answered) Tell(Lang.T("Одін глянув на твої дари й промовчав: загадку не відгадано.") + (wrong ? " " + Lang.T("Те, що не годиться в дар, лишилося в сумці.") : ""));
            RavenFx.Play("vfx_offering", _nwAt != null ? _nwAt.Middle : p.transform.position, Quaternion.identity);
            RavenFx.Play("sfx_offering", _nwAt != null ? _nwAt.Middle : p.transform.position, Quaternion.identity);
        }

        // a hollow in the bowl (pos: from the bowl's middle, y up): empty «+», or what is laid with its count
        private static void OfferSlot(RectTransform mid, Player p, int i, Vector2 pos)
        {
            const float s = 74f;
            Offer o = _nwOffer[i];
            RectTransform c = Rect("Place", mid);
            At(c, NwBowlX + pos.x - s / 2f, NwBowlY - pos.y - s / 2f, s, s);
            ItemDrop.ItemData it = o != null ? Favour.Find(p, o.Prefab) : null;
            FavourGroup grp = FavourGroup.Blood; float w;
            bool known = it != null && Favour.Worth(it, out grp, out w);
            Image hollow = c.gameObject.AddComponent<Image>();
            hollow.sprite = BookArt.Circle; hollow.color = new Color(0.05f, 0.03f, 0.01f, it != null ? 0.45f : 0.28f); hollow.raycastTarget = true;
            Image ring = Img(c, "Ring", BookArt.Ring(0.035f), it != null ? (known ? NwGroup[(int)grp] : PlaqueInk) : new Color(0.1f, 0.06f, 0.02f, 0.45f));
            ring.raycastTarget = false;
            if (it != null)
            {
                Sprite icon = it.GetIcon();
                if (icon != null) { Image im = Img(c, "Icon", icon, Color.white); im.preserveAspect = true; Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f)); }
                TMP_Text cnt = T(c, "<b>×" + o.Count + "</b>", 14f, NwInk, F.Body, TextAlignmentOptions.Bottom);
                Stretch(cnt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -4f), new Vector2(0f, 0f));
                cnt.outlineWidth = 0.2f; cnt.outlineColor = new Color32(0, 0, 0, 200);
                Clickable(c.gameObject, () => { _nwOffer[i] = null; NestWindowDraw(); });
                string name = Lang.Game(it.m_shared.m_name);
                NwTip(c.gameObject, () => "<b>" + name + "</b> ×" + o.Count + "\n" + Lang.T("Клік — забрати з чаші."));
            }
            else
            {
                TMP_Text plus = T(c, "+", 28f, new Color(0.17f, 0.11f, 0.05f, 0.6f), F.Head, TextAlignmentOptions.Center);
                Stretch(plus.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                NwTip(c.gameObject, () => Lang.T("Поклади дари в чашу: клік по речі внизу або перетягни її сюди."));
            }
            c.gameObject.AddComponent<NwDrop>().Index = i;
        }

        // ---- the window's own tooltip (the book's lives on the book's canvas, closed while this is up)
        private static Func<string> _nwTipFn;
        private static RectTransform _nwTip;
        private static TMP_Text _nwTipText;

        private static void NwTip(GameObject g, Func<string> text)
        {
            Graphic gr = g.GetComponent<Graphic>();
            if (gr != null) gr.raycastTarget = true;
            else g.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            NwHover h = g.GetComponent<NwHover>();
            if (h == null) h = g.AddComponent<NwHover>();
            h.Text = text;
        }

        private static void NwTipTick()
        {
            if (_nwTip == null)
            {
                _nwTip = Rect("Tip", _nwRoot.transform);
                _nwTip.anchorMin = _nwTip.anchorMax = new Vector2(0.5f, 0.5f);
                _nwTip.pivot = new Vector2(0f, 1f);
                Box(_nwTip, 6f, BookArt.TipBg, Gold, 1f);
                foreach (Image i in _nwTip.GetComponentsInChildren<Image>(true)) i.raycastTarget = false;
                _nwTipText = T(_nwTip, "", 15f, BookArt.TipInk, F.Body);
                Stretch(_nwTipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 12f), new Vector2(-14f, -12f));
            }
            string text = null;
            if (_nwTipFn != null && _nwDragging == null) { try { text = _nwTipFn(); } catch { text = null; } }
            if (string.IsNullOrEmpty(text)) { if (_nwTip.gameObject.activeSelf) _nwTip.gameObject.SetActive(false); return; }
            if (_nwTipText.text != text)
            {
                _nwTipText.text = text;
                Vector2 size = _nwTipText.GetPreferredValues(text, 380f, 0f);
                float w = Mathf.Min(380f, size.x) + 28f;
                _nwTip.sizeDelta = new Vector2(w, _nwTipText.GetPreferredValues(text, w - 28f, 0f).y + 24f);
            }
            _nwTip.gameObject.SetActive(true);
            _nwTip.SetAsLastSibling();
            RectTransform root = (RectTransform)_nwRoot.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out local);
            Vector2 half = root.rect.size / 2f, sz = _nwTip.sizeDelta;
            float x = local.x + 18f, y = local.y - 18f;
            if (x + sz.x > half.x - 8f) x = local.x - 12f - sz.x;
            if (y - sz.y < -half.y + 8f) y = local.y + 12f + sz.y;
            _nwTip.anchoredPosition = new Vector2(x, y);
        }

        internal sealed class NwHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal Func<string> Text;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { _nwTipFn = Text; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (_nwTipFn == Text) _nwTipFn = null; }
            private void OnDisable() { if (_nwTipFn == Text) _nwTipFn = null; }
        }

        // ---- dragging a thing from the bag's list onto a place in the bowl
        private static string _nwDragging;
        private static RectTransform _nwGhost;

        internal sealed class NwDrag : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler
        {
            internal string Prefab;
            internal Sprite Icon;

            public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                _nwDragging = Prefab;
                if (_nwRoot == null || Icon == null) return;
                if (_nwGhost != null) Destroy(_nwGhost.gameObject);
                _nwGhost = Rect("Ghost", _nwRoot.transform);
                _nwGhost.sizeDelta = new Vector2(52f, 52f);
                Image im = _nwGhost.gameObject.AddComponent<Image>();
                im.sprite = Icon; im.preserveAspect = true; im.raycastTarget = false; im.color = new Color(1f, 1f, 1f, 0.85f);
                OnDrag(e);
            }

            public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (_nwGhost == null) return;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_nwRoot.transform, e.position, null, out local);
                _nwGhost.anchorMin = _nwGhost.anchorMax = new Vector2(0.5f, 0.5f);
                _nwGhost.anchoredPosition = local;
            }

            public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (_nwGhost != null) Destroy(_nwGhost.gameObject);
                _nwGhost = null;
                _nwDragging = null;
            }
        }

        internal sealed class NwDrop : MonoBehaviour, UnityEngine.EventSystems.IDropHandler
        {
            internal int Index;

            public void OnDrop(UnityEngine.EventSystems.PointerEventData e)
            {
                Player p = Player.m_localPlayer;
                if (p == null || string.IsNullOrEmpty(_nwDragging)) return;
                bool all = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                string why = AddOffer(p, _nwDragging, all ? int.MaxValue : 1, Index);
                if (why != null) Tell(why);
                _nwDragging = null;
                NestWindowDraw();
            }
        }

    }
}
#endif

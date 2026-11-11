#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // A heart's board on the screen (the user's design 2026-11-06, docs/plans/mockups/heart-board.html): planks hanging on two chains, its head with the name's plate that grows with the name, a
    // plaque with how they live, on the left its notes (Треба зробити, Комора і скарбничка), on the right a note with tabs (its page made only when opened), under it the wooden buttons, always there.
    // Who shows it only says what is on it (Board): nothing here knows a heart (NpcBoard). It shares with a sheet (RavenBookSheet) its place, its keys and its box for the why: one such window at a time.
    internal static partial class RavenBook
    {
        internal const int BoardNoDot = -1, BoardGreyDot = -2;

        internal sealed class BoardRow { public string Label = "", Text = "", Sub = "", Far = "", Link = "", Tip, TipHead; public int Dot = BoardNoDot, Ink; public bool Arrow; public Action Click; }
        internal sealed class BoardGroup { public string Head = "", Text = ""; public int Level = BoardNoDot; public List<BoardRow> Rows = new List<BoardRow>(); public bool Diamond; }
        internal sealed class BoardNote { public string Title = ""; public List<BoardGroup> Groups = new List<BoardGroup>(); }
        internal sealed class BoardPerson { public string Name = "", Job = "", You = "", Tip, TipHead; public int Food, Rest, Mood, YouRank = 2; public bool JobBad, Far; public Action Click; }
        internal sealed class BoardPage { public List<BoardPerson> People; public List<BoardGroup> Groups = new List<BoardGroup>(); }
        internal sealed class BoardTab { public string Title = ""; public Func<BoardPage> Page; }

        internal sealed class Board
        {
            public string Title = "", Mood = "", MoodTip;
            public Func<string> CountNow; // (how many: asked again when the server's list comes)
            public int MoodTone;
            public List<BoardNote> Notes = new List<BoardNote>();
            public List<BoardTab> Tabs = new List<BoardTab>();
            public int Open;
            public Func<bool> Stay;
            public int Dining; // 0 none · 1 picked · 2 its table is gone
            public Action Choose, Remove, ShowTable, Edges, Rename;
        }

        private const float BoardDown = 16f, BoardHeadH = 72f, BoardW = 980f, BoardBodyW = 944f, BoardLeftW = 400f, BoardRightW = 476f;
        private static readonly Color BPaper = C("#efe2c0"), BTitle = C("#8a3416"), BLabel = C("#6b5636"), BSub = C("#7a3a1c"), BTabOn = C("#5a230f"), BTabOff = C("#5a3e26"), BTabOnInk = C("#f0d590"), BTabOffInk = C("#e2cfa6"),
            BRule = C("#4b1c0d"), BDotGood = C("#8fbf5a"), BDotWarn = C("#e0b04a"), BDotBad = C("#e0603e"), BDotGrey = C("#a89c86"), BFar = C("#7a6a52"), BGold = C("#d9b877"), BName = C("#f0d590");
        private static readonly Color[] BYou = { C("#9a2216"), C("#8a3416"), C("#6b5636"), C("#7f4e04"), C("#4f6a2a") };
        private static readonly Color BRowLit = new Color(1f, 0.745f, 0.31f, 0.38f);

        private static Board _board;
        private static RectTransform _boardTabs, _boardBody;
        private static LayoutElement _boardH; private static RectMask2D _boardMask; private static CanvasGroup _boardFade; private static BoardEase _boardEase; // (the open page's window: its height eased from one tab to the next, the user 2026-11-08)
        private const float BoardEaseFor = 0.24f;
        private static TMP_Text _boardCount;

        internal static void ShowBoard(Board b)
        {
            EnsureGameFont();
            HideSheet();
            _board = b;
            _sheetRoot = OverlayCanvas("RavenWatch_Board", 30004, true);
            _sheetRoot.GetComponent<Canvas>().pixelPerfect = true; // (it stands still: its thin lines on whole pixels, all as thick as each other)
            SheetKeys keys = _sheetRoot.AddComponent<SheetKeys>();
            keys.Stay = b.Stay;
            _boardEase = _sheetRoot.AddComponent<BoardEase>(); _boardEase.enabled = false;
            RectTransform all = Rect("Board", _sheetRoot.transform);
            _sheetScroll = all;
            _sheetEdge = all;
            all.anchorMin = all.anchorMax = all.pivot = new Vector2(0.5f, 0.5f);
            all.anchoredPosition = new Vector2(0f, -BoardDown); // (a little under the middle: its chains above it)
            VBox(all, 0f, TextAnchor.UpperCenter, 0, 0, 0, 0).childForceExpandWidth = false;
            all.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fixed(all.gameObject, BoardW, -1f);
            all.sizeDelta = new Vector2(BoardW, all.sizeDelta.y);

            foreach (float x in new[] { 190f, 780f })
            {
                RectTransform chain = Rect("Chain", all); chain.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                chain.anchorMin = chain.anchorMax = new Vector2(0f, 1f); chain.pivot = new Vector2(0.5f, 0f); chain.sizeDelta = new Vector2(18f, 80f); chain.anchoredPosition = new Vector2(x, 0f);
                Image ci = chain.gameObject.AddComponent<Image>(); ci.sprite = BookArt.BoardChain; ci.raycastTarget = false;
            }
            RectTransform room = Rect("HeadRoom", all); Fixed(room.gameObject, BoardW, BoardHeadH - 6f); // (the planks start a little under the head)
            BoardBody(all, b);
            BoardHead(all, b);
            BoardDraw();
            SheetBox();
            if (!TalkInstant) { CanvasGroup g = all.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; keys.Fade = g; }
        }

        // ---------------------------------------------------------------- its head, last of all: its plate hangs over the planks
        private static void BoardHead(RectTransform all, Board b)
        {
            RectTransform head = Rect("Head", all);
            head.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f); head.pivot = new Vector2(0.5f, 1f); head.sizeDelta = new Vector2(0f, BoardHeadH); head.anchoredPosition = Vector2.zero;
            HBox(head, 16f, TextAnchor.MiddleCenter, 60, 60, 0, 0);
            RectTransform wood = Rect("Plank", head); wood.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; Stretch(wood, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
            Image wi = wood.gameObject.AddComponent<Image>(); wi.sprite = BookArt.BoardPlank; wi.raycastTarget = false;
            foreach (bool left in new[] { true, false })
            {
                RectTransform iron = Rect("Iron", head); iron.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                iron.anchorMin = iron.anchorMax = new Vector2(left ? 0f : 1f, 0.5f); iron.sizeDelta = new Vector2(48f, 72f); iron.anchoredPosition = new Vector2(left ? 24f : -24f, 0f);
                if (!left) iron.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Image ii = iron.gameObject.AddComponent<Image>(); ii.sprite = BookArt.BoardIron(left); ii.raycastTarget = false;
            }
            BoardRunes(head, b.Title, TextAlignmentOptions.MidlineRight);
            RectTransform plate = Rect("Plate", head);
            Image pi = plate.gameObject.AddComponent<Image>(); pi.sprite = BookArt.BoardPlaque; pi.type = Image.Type.Sliced; pi.raycastTarget = false;
            VBox(plate, 2f, TextAnchor.MiddleCenter, 50, 50, 14, 14).childForceExpandWidth = false;
            RectTransform over = Rect("Over", plate); HBox(over, 8f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            BoardDiamond(over, 5f, A(BTabOnInk, 0.7f));
            TMP_Text word = T(over, Lang.T("СЕРЦЕ ОСЕЛІ"), 11f, A(C("#e6c77d"), 0.7f), F.Head, TextAlignmentOptions.Midline); word.characterSpacing = 50f; word.textWrappingMode = TextWrappingModes.NoWrap;
            BoardDiamond(over, 5f, A(BTabOnInk, 0.7f));
            TMP_Text name = T(plate, b.Title.ToUpperInvariant(), 30f, BName, F.Head, TextAlignmentOptions.Midline); name.characterSpacing = 30f; name.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Max(name.GetPreferredValues(name.text).x, 150f) + 100f; // (it grows with the name: the head's runes give way)
            LayoutElement ple = plate.gameObject.AddComponent<LayoutElement>(); ple.preferredWidth = w; ple.minWidth = w; ple.preferredHeight = 94f; ple.minHeight = 94f;
            BoardRunes(head, b.Title, TextAlignmentOptions.MidlineLeft);
            RectTransform x = CloseButton(all, HideSheet); x.anchorMin = x.anchorMax = x.pivot = new Vector2(1f, 1f); x.anchoredPosition = new Vector2(14f, -14f);
        }

        // what is carved into the head beside the plate: its own name in runes, again and again, as far as there is room
        private static void BoardRunes(RectTransform head, string name, TextAlignmentOptions align)
        {
            RectTransform box = Rect("Runes", head);
            LayoutElement le = box.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1f; le.minWidth = 0f; le.preferredWidth = 0f; le.preferredHeight = 24f;
            box.gameObject.AddComponent<RectMask2D>();
            string runes = BookArt.RuneWords(name);
            TMP_Text t = T(box, runes + " · " + runes + " · " + runes, 16f, A(C("#e6c77d"), 0.42f), F.Rune, align);
            t.characterSpacing = 45f; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void BoardDiamond(Transform to, float size, Color c, bool edge = false)
        {
            float wide = size * 1.5f;
            RectTransform place = Rect("Diamond", to); Fixed(place.gameObject, wide, wide);
            if (edge) { Image rim = place.gameObject.AddComponent<Image>(); rim.sprite = BookArt.Diamond; rim.color = A(PInk, 0.5f); rim.raycastTarget = false; }
            RectTransform d = Rect("Fill", place); d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f); d.sizeDelta = edge ? new Vector2(wide - 3f, wide - 3f) : new Vector2(wide, wide);
            Image i = d.gameObject.AddComponent<Image>(); i.sprite = BookArt.Diamond; i.color = c; i.raycastTarget = false;
        }

        // ---------------------------------------------------------------- the planks and what is on them
        private static void BoardBody(RectTransform all, Board b)
        {
            RectTransform body = Rect("Planks", all);
            Fixed(body.gameObject, BoardBodyW, -1f);
            Image wi = body.gameObject.AddComponent<Image>(); wi.sprite = BookArt.BoardWood; wi.raycastTarget = true; // (a click on the wood is no click on the world)
            Outline(body, BookArt.FrameFine(2f, 2f), C("#24170c"));
            VBox(body, 16f, TextAnchor.UpperLeft, 24, 24, 30, 30);
            RectTransform rail = Rect("Rail", body); rail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rail.anchorMin = new Vector2(0f, 0f); rail.anchorMax = new Vector2(1f, 0f); rail.pivot = new Vector2(0.5f, 0f); rail.sizeDelta = new Vector2(0f, 10f); rail.anchoredPosition = new Vector2(0f, 14f);
            Image ri = rail.gameObject.AddComponent<Image>(); ri.sprite = BookArt.BoardRail; ri.raycastTarget = false;
            BoardPlate(body, b);
            RectTransform grid = Rect("Grid", body); HBox(grid, 20f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            RectTransform left = Rect("Left", grid); VBox(left, 14f, TextAnchor.UpperLeft, 0, 0, 0, 0); Fixed(left.gameObject, BoardLeftW, -1f);
            int n = 0;
            foreach (BoardNote note in b.Notes)
            {
                RectTransform box = BoardPaper(left, 7 + n, false);
                TMP_Text title = T(box, note.Title, 16f, BTitle, F.Head, TextAlignmentOptions.TopLeft); title.characterSpacing = 22f;
                foreach (BoardGroup g in note.Groups) BoardGroupRows(box, g);
                n++;
            }
            RectTransform right = Rect("Right", grid); VBox(right, 22f, TextAnchor.UpperLeft, 0, 0, 0, 0); Fixed(right.gameObject, BoardRightW, -1f);
            RectTransform main = BoardPaper(right, 3, true);
            RectTransform band = Rect("TabBand", main); VBox(band, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0); // (the tabs stand on their rule: the page's own gap not between them — the user 2026-11-08, as the card's TabBand)
            _boardTabs = Rect("Tabs", band); HBox(_boardTabs, 4f, TextAnchor.LowerLeft, 0, 0, 4, 0);
            RectTransform rule = Rect("Rule", band); Fixed(rule.gameObject, -1f, 3f);
            Image rli = rule.gameObject.AddComponent<Image>(); rli.color = BRule; rli.raycastTarget = false;
            RectTransform clip = Rect("PageWindow", main); // (the page hangs in it at its full height: only the window's height moves, no row is ever squeezed)
            _boardH = clip.gameObject.AddComponent<LayoutElement>(); _boardH.minHeight = 0f; _boardH.preferredHeight = -1f; _boardH.flexibleHeight = 0f;
            _boardMask = clip.gameObject.AddComponent<RectMask2D>(); _boardMask.enabled = false;
            _boardFade = clip.gameObject.AddComponent<CanvasGroup>();
            _boardBody = Rect("Page", clip); Stretch(_boardBody, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); _boardBody.pivot = new Vector2(0.5f, 1f);
            VBox(_boardBody, 4f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            _boardBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BoardActs(right, b);
        }

        private static void BoardPlate(RectTransform body, Board b)
        {
            RectTransform line = Rect("Top", body); HBox(line, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            RectTransform plate = Rect("Count", line);
            Image pi = plate.gameObject.AddComponent<Image>(); pi.sprite = BookArt.RoundFine(2f); pi.type = Image.Type.Sliced; pi.color = C("#ecdfbc"); pi.raycastTarget = false;
            UnityEngine.UI.Shadow sh = plate.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(0f, -4f);
            HBox(plate, 16f, TextAnchor.MiddleLeft, 18, 18, 8, 9);
            _boardCount = T(plate, b.CountNow(), 19f, PInk, F.Body, TextAlignmentOptions.MidlineLeft); _boardCount.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform bar = Rect("Bar", plate); Fixed(bar.gameObject, 1f, 24f);
            Image bi = bar.gameObject.AddComponent<Image>(); bi.color = A(C("#5a3c1e"), 0.3f); bi.raycastTarget = false;
            RectTransform mood = Rect("Mood", plate); HBox(mood, 9f, TextAnchor.MiddleLeft, 0, 0, 0, 0);
            Image fill = mood.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 1f, 1f, 0f);
            BoardDot(mood, b.MoodTone, 11f);
            TMP_Text m = T(mood, b.Mood.ToUpperInvariant(), 20f, PInk, F.Head, TextAlignmentOptions.MidlineLeft); m.characterSpacing = 10f; m.textWrappingMode = TextWrappingModes.NoWrap;
            SheetHook(mood.gameObject, Lang.T("ЧОМУ"), b.MoodTip, null, new Graphic[] { m }, PInk, null, fill, BRowLit);
        }

        // a note on the planks, torn all round, on its pin (two: the one with tabs). It stands straight: a little askew, its rules and words broke into steps (the user 2026-11-06)
        private static RectTransform BoardPaper(RectTransform to, int look, bool twoPins)
        {
            RectTransform p = Rect("Note", to);
            Image i = p.gameObject.AddComponent<Image>(); i.sprite = BookArt.TornFill(look); i.type = Image.Type.Tiled; i.color = BPaper; i.raycastTarget = false;
            UnityEngine.UI.Shadow sh = p.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.5f); sh.effectDistance = new Vector2(0f, -6f);
            VBox(p, 7f, TextAnchor.UpperLeft, 18, 18, 20, 14);
            foreach (float x in twoPins ? new[] { 0f, 1f } : new[] { 0.5f })
            {
                RectTransform pin = Rect("Pin", p); pin.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                pin.anchorMin = pin.anchorMax = new Vector2(x, 1f); pin.sizeDelta = new Vector2(10f, 10f); pin.anchoredPosition = new Vector2(x == 0.5f ? 0f : x == 0f ? 21f : -21f, -12f);
                Image pi = pin.gameObject.AddComponent<Image>(); pi.sprite = BookArt.Circle; pi.color = C("#5a564e"); pi.raycastTarget = false;
            }
            return p;
        }

        private static void BoardGroupRows(Transform to, BoardGroup g)
        {
            RectTransform box = Rect("Group", to); VBox(box, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            if (g.Head.Length > 0)
            {
                RectTransform head = Rect("Head", box); HBox(head, 7f, TextAnchor.MiddleLeft, 0, 0, 4, 2);
                Color ink = g.Level == ToneBad ? C("#9a2216") : g.Level == ToneWarn ? C("#7f4e04") : g.Diamond ? BLabel : BTitle;
                if (g.Diamond) BoardDiamond(head, 8f, g.Level == ToneBad ? BDotBad : g.Level == ToneWarn ? BDotWarn : BDotGrey, true);
                T(head, g.Head, 14f, ink, F.Italic, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            }
            foreach (BoardRow r in g.Rows) BoardRowDraw(box, r);
            if (g.Text.Length > 0) { TMP_Text t = T(box, g.Text, 17f, BLabel, F.Italic, TextAlignmentOptions.TopLeft); t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = 6f; }
        }

        private static void BoardRowDraw(Transform to, BoardRow r)
        {
            RectTransform row = Rect("Row", to);
            HBox(row, 10f, TextAnchor.UpperLeft, 0, 0, 4, 4);
            Image fill = row.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 1f, 1f, 0f);
            if (r.Label.Length > 0) { Room(T(row, r.Label, 14f, BLabel, F.Italic, TextAlignmentOptions.TopLeft), 92f, 10f); }
            if (r.Dot != BoardNoDot) { RectTransform at = Rect("At", row); Fixed(at.gameObject, 13f, 22f); HBox(at, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0); BoardDot(at, r.Dot, 10f); }
            RectTransform words = Rect("Words", row); VBox(words, 1f, TextAnchor.UpperLeft, 0, 0, 0, 0); LayoutElement wl = words.gameObject.AddComponent<LayoutElement>(); wl.flexibleWidth = 1f; wl.minWidth = 0f; wl.preferredWidth = 0f;
            Color ink = r.Ink == ToneBad ? BTitle : PInk;
            TMP_Text t = T(words, r.Text, 18f, ink, F.Body, TextAlignmentOptions.TopLeft); t.textWrappingMode = TextWrappingModes.Normal;
            if (r.Sub.Length > 0) { TMP_Text s = T(words, r.Sub, 14f, BSub, F.Italic, TextAlignmentOptions.TopLeft); s.textWrappingMode = TextWrappingModes.Normal; }
            if (r.Far.Length > 0) Keep(T(row, r.Far, 14f, BLabel, F.Italic, TextAlignmentOptions.TopRight));
            if (r.Link.Length > 0) Keep(T(row, r.Link, 14f, BTitle, F.Italic, TextAlignmentOptions.TopRight));
            if (r.Arrow || (r.Click != null && r.Link.Length == 0)) { RectTransform at = Rect("At", row); Fixed(at.gameObject, 18f, 22f); HBox(at, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0); SheetArrowImage(at, BTitle, true, 18f); }
            if (r.Tip != null) SheetHook(row.gameObject, r.TipHead, r.Tip, r.Click, new Graphic[] { t }, ink, null, fill, BRowLit);
            else SheetHook(row.gameObject, null, null, r.Click, new Graphic[] { t }, ink, fill);
            RectTransform rule = Rect("Rule", to); Fixed(rule.gameObject, -1f, 1f);
            Image ri = rule.gameObject.AddComponent<Image>(); ri.color = A(C("#6e5028"), 0.14f); ri.raycastTarget = false;
        }

        private static void BoardDot(Transform to, int tone, float size)
        {
            RectTransform d = Rect("Dot", to); Fixed(d.gameObject, size + 3f, size + 3f);
            Image rim = d.gameObject.AddComponent<Image>(); rim.sprite = BookArt.Circle; rim.color = A(PInk, 0.55f); rim.raycastTarget = false;
            RectTransform inner = Rect("In", d); Stretch(inner, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
            Image i = inner.gameObject.AddComponent<Image>(); i.sprite = BookArt.Circle; i.raycastTarget = false; i.color = tone == ToneBad ? BDotBad : tone == ToneWarn ? BDotWarn : tone == ToneGood ? BDotGood : BDotGrey;
        }

        // ---------------------------------------------------------------- its tabs and the open page (a click on a tab: only this)
        private static void BoardDraw(bool fade = false)
        {
            Board b = _board;
            if (b == null || _boardTabs == null) return;
            HideSheetTip();
            for (int i = _boardTabs.childCount - 1; i >= 0; i--) { GameObject g = _boardTabs.GetChild(i).gameObject; g.transform.SetParent(null); UnityEngine.Object.Destroy(g); }
            b.Open = Mathf.Clamp(b.Open, 0, Mathf.Max(0, b.Tabs.Count - 1));
            for (int i = 0; i < b.Tabs.Count; i++) BoardTabButton(b, i);
            FitTabs(_boardTabs, BoardRightW - 36f);
            for (int i = _boardBody.childCount - 1; i >= 0; i--) { GameObject g = _boardBody.GetChild(i).gameObject; g.transform.SetParent(null); UnityEngine.Object.Destroy(g); }
            BoardPage page = null;
            if (b.Tabs.Count > 0 && b.Tabs[b.Open].Page != null)
            {
                try { page = b.Tabs[b.Open].Page(); }
                catch (Exception e) { Npcs.Log("board «" + b.Title + "», tab «" + b.Tabs[b.Open].Title + "»: " + e); page = new BoardPage(); page.Groups.Add(new BoardGroup { Text = Lang.T("Не вдалося це прочитати.") }); }
            }
            if (page != null)
            {
                if (page.People != null) BoardPeople(page.People);
                foreach (BoardGroup g in page.Groups) BoardGroupRows(_boardBody, g);
            }
            BoardSettle(fade);
        }

        // the page drawn: the window goes to its height, the board to its scale, from where they are (a click in the middle of an easing goes on from there)
        private static void BoardSettle(bool fade)
        {
            if (_sheetScroll == null || _boardH == null) return;
            float was = _boardH.preferredHeight, scaleWas = _sheetScroll.localScale.x;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_sheetScroll); // (the page's width: its words wrap by it)
            float to = LayoutUtility.GetPreferredHeight(_boardBody);
            _boardH.preferredHeight = to;
            SheetFit(); // (the scale it will end at)
            float scaleTo = _sheetScroll.localScale.x;
            if (was < 0f || TalkInstant || _boardEase == null || (!fade && !_boardEase.enabled && Mathf.Abs(to - was) < 0.5f)) return;
            _boardEase.Go(was, to, scaleWas, scaleTo, fade);
        }

        internal sealed class BoardEase : MonoBehaviour
        {
            private float _from, _to, _s0, _s1, _at; private bool _fade;
            internal void Go(float from, float to, float s0, float s1, bool fade) { _from = from; _to = to; _s0 = s0; _s1 = s1; _fade = fade; _at = Time.unscaledTime; _boardMask.enabled = true; enabled = true; Update(); }
            private void Update()
            {
                if (_boardH == null || _sheetScroll == null) { enabled = false; return; }
                float k = Mathf.Clamp01((Time.unscaledTime - _at) / BoardEaseFor), e = k * k * (3f - 2f * k);
                _boardH.preferredHeight = Mathf.Lerp(_from, _to, e);
                _sheetScroll.localScale = Vector3.one * Mathf.Lerp(_s0, _s1, e);
                if (_fade && _boardFade != null) _boardFade.alpha = e;
                LayoutRebuilder.MarkLayoutForRebuild(_sheetScroll);
                if (k < 1f) return;
                if (_boardMask != null) _boardMask.enabled = false;
                if (_boardFade != null) _boardFade.alpha = 1f;
                enabled = false;
            }
        }

        internal static bool BoardOpen(Board b) { return b != null && _board == b && SheetOpen; }

        // new people known while it is open (the server's list came): how many, and this page anew (people where it had none: NpcBoard builds it anew)
        internal static void BoardRefresh(Board b)
        {
            if (_board != b || !SheetOpen) return;
            if (_boardCount != null) _boardCount.text = b.CountNow();
            BoardDraw();
        }

        private static void BoardTabButton(Board b, int i)
        {
            bool on = i == b.Open;
            RectTransform tab = Rect("Tab", _boardTabs);
            Image bg = tab.gameObject.AddComponent<Image>(); bg.sprite = BookArt.RoundFine(5f, true); bg.type = Image.Type.Sliced; bg.color = on ? BTabOn : BTabOff;
            HBox(tab, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            Color ink = on ? BTabOnInk : BTabOffInk;
            TMP_Text t = T(tab, b.Tabs[i].Title, 14f, ink, F.Head, TextAlignmentOptions.Center); t.characterSpacing = 14f; t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(tab.gameObject, t.GetPreferredValues(t.text).x + 24f, on ? 33f : 29f);
            if (on) return;
            int at = i;
            Clickable(tab.gameObject, () => { if (_board != null) { _board.Open = at; BoardDraw(true); } });
            Hover(tab.gameObject, new Graphic[] { t }, ink, null);
        }

        // the table: name · work · food, rest, mood · how it is with you
        private const float CName = 122f, CJob = 100f, CDot = 34f, CMood = 46f, CYou = 70f, CGap = 6f;

        private static void BoardPeople(List<BoardPerson> people)
        {
            RectTransform head = Rect("Head", _boardBody); HBox(head, CGap, TextAnchor.MiddleLeft, 0, 0, 2, 3);
            foreach (KeyValuePair<string, float> c in new[] { new KeyValuePair<string, float>("", CName + CGap + CJob), new KeyValuePair<string, float>(Lang.T("їжа"), CDot), new KeyValuePair<string, float>(Lang.T("втома"), CDot + 4f), new KeyValuePair<string, float>(Lang.T("настрій"), CMood), new KeyValuePair<string, float>(Lang.T("до тебе"), CYou) })
            {
                Room(T(head, c.Key, 14f, BLabel, F.Italic, TextAlignmentOptions.Midline), c.Value, 9f);
            }
            RectTransform rule = Rect("Rule", _boardBody); Fixed(rule.gameObject, -1f, 1f);
            Image ri = rule.gameObject.AddComponent<Image>(); ri.color = A(C("#5a3c1e"), 0.3f); ri.raycastTarget = false;
            foreach (BoardPerson p in people)
            {
                RectTransform row = Rect("Person", _boardBody); HBox(row, CGap, TextAnchor.MiddleLeft, 0, 0, 3, 3);
                Image fill = row.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 1f, 1f, 0f);
                Color ink = p.Far ? BFar : PInk;
                TMP_Text n = T(row, p.Name, 18f, ink, F.Body, TextAlignmentOptions.MidlineLeft); Room(n, CName, 13f);
                TMP_Text j = T(row, p.Job, 15f, p.JobBad ? C("#9a2216") : p.Far ? C("#8a7f6c") : BLabel, F.Italic, TextAlignmentOptions.MidlineLeft); Room(j, CJob, 11f);
                foreach (KeyValuePair<int, float> d in new[] { new KeyValuePair<int, float>(p.Food, CDot), new KeyValuePair<int, float>(p.Rest, CDot + 4f), new KeyValuePair<int, float>(p.Mood, CMood) })
                {
                    RectTransform at = Rect("At", row); Fixed(at.gameObject, d.Value, 16f); HBox(at, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
                    BoardDot(at, p.Far ? BoardGreyDot : d.Key, 11f);
                }
                TMP_Text y = T(row, p.You.ToUpperInvariant(), 14f, BYou[Mathf.Clamp(p.YouRank, 0, BYou.Length - 1)], F.Head, TextAlignmentOptions.MidlineLeft); y.characterSpacing = 8f; Room(y, CYou, 10f);
                if (p.Tip != null) SheetHook(row.gameObject, p.TipHead, p.Tip, p.Click, new Graphic[] { n }, ink, null, fill, BRowLit);
                else SheetHook(row.gameObject, null, null, p.Click, new Graphic[] { n }, ink, fill);
                RectTransform line = Rect("Rule", _boardBody); Fixed(line.gameObject, -1f, 1f);
                Image li = line.gameObject.AddComponent<Image>(); li.color = A(C("#6e5028"), 0.14f); li.raycastTarget = false;
            }
        }

        // ---------------------------------------------------------------- the wooden buttons: what is done to the whole of it, always there
        private const float ActH = 46f, ActMain = 318f, ActSmall = 154f;

        private static void BoardActs(RectTransform right, Board b)
        {
            RectTransform box = Rect("Acts", right);
            Image bg = box.gameObject.AddComponent<Image>(); bg.sprite = BookArt.RoundFine(4f); bg.type = Image.Type.Sliced; bg.color = new Color(0.04f, 0.02f, 0.01f, 0.32f); bg.raycastTarget = false;
            VBox(box, 12f, TextAnchor.UpperCenter, 0, 0, 14, 16).childForceExpandWidth = true;
            RectTransform state = Rect("State", box); HBox(state, 10f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            TMP_Text word = T(state, Lang.T("ЇДАЛЬНЯ:"), 14f, BGold, F.Head, TextAlignmentOptions.Midline); word.characterSpacing = 22f; word.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform st = Rect("Now", state); HBox(st, 8f, TextAnchor.MiddleLeft, 0, 0, 0, 0);
            Image stFill = st.gameObject.AddComponent<Image>(); stFill.sprite = BookArt.RoundFine(3f); stFill.type = Image.Type.Sliced; stFill.color = new Color(1f, 1f, 1f, 0f);
            RectTransform dot = Rect("Dot", st); Fixed(dot.gameObject, 9f, 9f); Image di = dot.gameObject.AddComponent<Image>(); di.sprite = BookArt.Circle; di.raycastTarget = false; di.color = b.Dining == 1 ? BDotGood : b.Dining == 2 ? BDotBad : BDotGrey;
            Color stInk = b.Dining == 2 ? C("#f08a6a") : TPaper;
            TMP_Text now = T(st, b.Dining == 1 ? Lang.T("обрано") : b.Dining == 2 ? Lang.T("стіл зник, обери інший") : Lang.T("не обрано"), 18f, stInk, F.Body, TextAlignmentOptions.MidlineLeft); now.textWrappingMode = TextWrappingModes.NoWrap;
            if (b.Dining == 1 && b.ShowTable != null)
            {
                TMP_Text show = T(st, Lang.T("показати"), 14f, BGold, F.Italic, TextAlignmentOptions.MidlineLeft); show.textWrappingMode = TextWrappingModes.NoWrap;
                SheetArrowImage(st, BGold, true, 16f);
                Clickable(st.gameObject, b.ShowTable);
                Hover(st.gameObject, new Graphic[] { now, show }, stInk, stFill);
            }
            RectTransform dine = Rect("Dining", box); HBox(dine, 10f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            if (b.Dining == 1)
            {
                BoardAct(dine, Lang.T("Інший стіл"), ActSmall, false, Lang.T("ІНШИЙ СТІЛ"), Lang.T("Перенести їдальню: покажи інший стіл."), b.Choose);
                BoardAct(dine, Lang.T("Зняти"), ActSmall, false, Lang.T("ЗНЯТИ"), Lang.T("Без їдальні мешканці їдять де випаде, а вечорами сидять біля вогню."), b.Remove);
            }
            else BoardAct(dine, Lang.T("Обрати стіл"), ActMain, true, Lang.T("ОБРАТИ СТІЛ"), Lang.T("Покажи стіл: тут мешканці обідатимуть і збиратимуться вечорами. Їжу братимуть зі скринь довкола, сідатимуть на лави й стільці поруч."), b.Choose);
            RectTransform rail = Rect("Rail", box); Fixed(rail.gameObject, -1f, 4f);
            Image ri = rail.gameObject.AddComponent<Image>(); ri.sprite = BookArt.BoardRail; ri.raycastTarget = false;
            RectTransform land = Rect("Land", box); HBox(land, 10f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            BoardAct(land, Lang.T("Показати межі"), ActSmall, false, Lang.T("ПОКАЗАТИ МЕЖІ"), Lang.T("На 30 секунд: золоті вогники по межі оселі й позначки на мапі. Усе всередині мешканці вважають своїм."), b.Edges);
            BoardAct(land, Lang.T("Змінити назву"), ActSmall, false, Lang.T("ЗМІНИТИ НАЗВУ"), Lang.T("Під цією назвою оселю бачать у списку, куди послати найманих."), b.Rename);
        }

        private static void BoardAct(RectTransform row, string word, float w, bool main, string tipHead, string tip, Action click)
        {
            RectTransform b = Rect("Act", row);
            Image bg = b.gameObject.AddComponent<Image>(); bg.sprite = BookArt.BoardAct(0, main); bg.type = Image.Type.Sliced;
            HBox(b, 0f, TextAnchor.MiddleCenter, 12, 12, 4, 7);
            Fixed(b.gameObject, w, ActH);
            Color ink = main ? BTabOnInk : BTabOffInk;
            TMP_Text t = T(b, word, 18f, ink, F.Head, TextAlignmentOptions.Center); t.characterSpacing = 5f; Shrink(t, 13f);
            BoardActLook look = b.gameObject.AddComponent<BoardActLook>();
            look.Bg = bg; look.Word = t; look.Main = main; look.Normal = ink;
            SheetHook(b.gameObject, tipHead, tip, click, new Graphic[0], ink, null);
        }

        // its three looks: under the pointer brighter, pressed sunk in
        internal sealed class BoardActLook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            internal Image Bg; internal TMP_Text Word; internal bool Main; internal Color Normal;
            private bool _over, _down;
            private void Show()
            {
                int s = _down ? 2 : _over ? 1 : 0;
                Bg.sprite = BookArt.BoardAct(s, Main);
                Word.color = s == 2 ? (Main ? C("#e0b860") : C("#c9a876")) : s == 1 ? C("#ffd98a") : Normal;
            }
            public void OnPointerEnter(PointerEventData e) { _over = true; Show(); }
            public void OnPointerExit(PointerEventData e) { _over = false; _down = false; Show(); }
            public void OnPointerDown(PointerEventData e) { _down = true; Show(); }
            public void OnPointerUp(PointerEventData e) { _down = false; Show(); }
        }
    }
}
#endif

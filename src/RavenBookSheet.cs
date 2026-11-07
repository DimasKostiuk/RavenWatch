#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // A sheet: what you read of a person or a place, on a scroll on the left of the screen (the user's picture 2026-11-06, docs/plans/mockups/npc-card-2026-11-06.png). The one window for it:
    // a person's card («Вдача», NpcProfile), a short list to pick from (NpcResident: where to move to); a heart's board has its own planks (RavenBookBoard) and shares its place, keys and box for the why. Who makes one only says what is on it (Sheet, SheetTab, SheetRow), nothing here knows a person or a heart.
    //   on top      the parchment's roll with its end, the shield, the iron ✕; «← back» when it came from another sheet; its name on the paper between two rules; who it is in a line or two;
    //               the dark block (ЗАРАЗ, what goes on now, its pills); three cards (a word and a dot; why under the pointer, the card lit); how it is with you on a line painted by the brush
    //   tabs        folders over a dark rule, one open at a time; each one's rows are made only when it is opened (Rows reads lockers and chests: never for what nobody looks at), anew each time
    //   rows        Label · Text · Plus/Minus · Mind · Pair · Chips · Fold: words, no numbers but what is really counted
    //   the why     Tip (TipHead over it): one box beside the scroll, its arrow at the row, while the pointer is on it
    //   it closes   Esc, the ✕, what it was about gone (Sheet.Stay), a blow from a foe (as a talk does: NpcTalk.Tick). While it is open the game is held as for the book
    // Only one at a time: a new one takes the old one's place («←» goes back: Sheet.Back).
    internal static partial class RavenBook
    {
        internal const int ToneNone = 0, ToneGood = 1, ToneWarn = 2, ToneBad = 3;

        // a pill in the dark block, a card, a chip, a person's dot: what it says, why, what a click does
        internal sealed class SheetMark { public string Text = "", Label = "", Tip, TipHead; public int Tone; public Action Click; public bool Hot; }

        internal enum SheetKind { Label, Text, Attitude, Plus, Minus, Mind, Chips, Pair, Fold }

        internal sealed class SheetRow
        {
            public SheetKind Kind;
            public string Text = "", Right = "", Far = "", Tip, TipHead, More;  // its words, its second column, what stands at its far right; why; Fold: what opens
            public int Tone;                                                    // Pair, Person's sign, Trouble's stripe; Attitude: which of the five words
            public float Share;                                                 // Attitude: 0 … 1 along the line
            public List<SheetMark> Marks;                                       // Chips, Person's dots
            public Action Click;

            internal static SheetRow Label(string t) { return new SheetRow { Kind = SheetKind.Label, Text = t }; }
            internal static SheetRow Plain(string t) { return new SheetRow { Kind = SheetKind.Text, Text = t }; }
            internal static SheetRow Attitude(string word, int rank, float share, string tipHead, string tip) { return new SheetRow { Kind = SheetKind.Attitude, Text = word, Tone = rank, Share = share, TipHead = tipHead, Tip = tip }; }
            internal static SheetRow Line(string t, bool up, string far) { return new SheetRow { Kind = up ? SheetKind.Plus : SheetKind.Minus, Text = t, Far = far }; }
            internal static SheetRow Mind(string t) { return new SheetRow { Kind = SheetKind.Mind, Text = t }; }
            internal static SheetRow Chips(List<SheetMark> l) { return new SheetRow { Kind = SheetKind.Chips, Marks = l }; }
            internal static SheetRow Pair(string label, string value, int tone = ToneNone, string tip = null, Action click = null, string far = "") { return new SheetRow { Kind = SheetKind.Pair, Text = label, Right = value, Tone = tone, Tip = tip, Click = click, Far = far }; }
            internal static SheetRow Fold(string t, string open, string more) { return new SheetRow { Kind = SheetKind.Fold, Text = t, Right = open, More = more }; }
        }

        internal sealed class SheetTab { public string Title = ""; public Func<List<SheetRow>> Rows; public int Tone; }

        internal sealed class Sheet
        {
            public string Title = "", Who = "", Now = "", BackWord = "";
            public List<SheetMark> Marks = new List<SheetMark>(), Cards = new List<SheetMark>();
            public List<SheetRow> Above = new List<SheetRow>();
            public Action Back;
            public Func<bool> Stay;
            public List<SheetTab> Tabs = new List<SheetTab>();
            public int Open;
        }

        private static readonly Color SLabel = C("#7a6446"), SGood = C("#3f5a1f"), SBad = C("#8a2216"), SWarn = C("#8a5a10"), SDotGood = C("#8fbf5a"), SDotWarn = C("#e0b04a"), SDotBad = C("#e0603e"),
            SPaperDotGood = C("#7f9f4a"), SPaperDotWarn = C("#c9a040"), SPaperDotBad = C("#d05a3c"), SRust = C("#8a3416"), SSaid = C("#7a3418"),
            SName = C("#3a2410"), SNowWord = C("#c9a464"), SPill = C("#2e0f06"), SCardLit = new Color(0.98f, 0.82f, 0.5f, 0.75f), SCard = new Color(1f, 0.98f, 0.92f, 0.32f),
            STabOn = C("#7a2a12"), STabOff = C("#4f3720"), STabOnInk = C("#f2dca8"), STabOffInk = C("#d9c08e"), STabRule = C("#5a1f0e"), STipFill = C("#f4e3bb");
        private static readonly Color[] SAttitudeWord = { C("#9a2216"), C("#a8492a"), C("#6b5636"), C("#5f7a2c"), C("#3f5a1f") };
        private const float SheetRoom = TalkPaperW - 28f - 30f, SheetFade = 0.14f, RollH = 28f, ShieldD = 84f;

        private static GameObject _sheetRoot;
        private static Sheet _sheet;
        private static RectTransform _sheetScroll, _sheetBody, _sheetTabs, _sheetTip;
        private static TMP_Text _sheetTipText, _sheetTipHead;

        internal static bool SheetOpen { get { return _sheetRoot != null && _sheetRoot.activeSelf; } }

        internal static void HideSheet() { if (_sheetRoot != null) UnityEngine.Object.Destroy(_sheetRoot); _sheetRoot = null; _sheet = null; _board = null; }

        internal static void ShowSheet(Sheet s)
        {
            EnsureGameFont();
            HideSheet();
            _sheet = s;
            _sheetRoot = OverlayCanvas("RavenWatch_Sheet", 30004, true);
            _sheetRoot.GetComponent<Canvas>().pixelPerfect = true; // (it stands still: its thin lines on whole pixels, all as thick as each other)
            SheetKeys keys = _sheetRoot.AddComponent<SheetKeys>();
            keys.Stay = s.Stay;
            RectTransform paper = SheetShell(s);
            if (s.Back != null) SheetBack(paper, s);
            SheetName(paper, s.Title);
            if (s.Who.Length > 0) { TMP_Text w = T(paper, s.Who, 15.5f, PSoft, F.Italic, TextAlignmentOptions.Top); w.textWrappingMode = TextWrappingModes.Normal; }
            if (s.Now.Length > 0 || s.Marks.Count > 0) SheetNowBlock(paper, s);
            if (s.Cards.Count > 0) SheetCards(paper, s.Cards);
            foreach (SheetRow r in s.Above) SheetAdd(paper, r);
            if (s.Tabs.Count > 1)
            {
                RectTransform band = Rect("TabBand", paper); VBox(band, 0f, TextAnchor.UpperLeft, 0, 0, 2, 0);
                _sheetTabs = Rect("Tabs", band); HBox(_sheetTabs, 3f, TextAnchor.LowerLeft, 0, 0, 0, 0);
                RectTransform rule = Rect("Rule", band); Fixed(rule.gameObject, -1f, 3f);
                Image ri = rule.gameObject.AddComponent<Image>(); ri.color = STabRule; ri.raycastTarget = false;
            }
            else _sheetTabs = null;
            _sheetBody = Rect("Body", paper); VBox(_sheetBody, 7f, TextAnchor.UpperLeft, 0, 0, 2, 0);
            SheetBox();
            s.Open = s.Tabs.Count > 0 ? Mathf.Clamp(s.Open, 0, s.Tabs.Count - 1) : 0;
            SheetDraw();
            if (!TalkInstant) { CanvasGroup g = _sheetScroll.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; keys.Fade = g; }
        }

        // the scroll: the roll, then the paper; the shade, the shield and the ✕ over them. Returns the paper
        private static RectTransform SheetShell(Sheet s)
        {
            RectTransform scroll = Rect("Scroll", _sheetRoot.transform);
            _sheetScroll = scroll;
            scroll.anchorMin = scroll.anchorMax = scroll.pivot = new Vector2(0f, 1f);
            scroll.anchoredPosition = new Vector2(TalkLeft, -TalkTop);
            VBox(scroll, 0f, TextAnchor.UpperCenter, 0, 0, 0, 0).childForceExpandWidth = false;
            scroll.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fixed(scroll.gameObject, TalkW, -1f);
            scroll.sizeDelta = new Vector2(TalkW, scroll.sizeDelta.y);

            RectTransform roll = Rect("Roll", scroll);
            Fixed(roll.gameObject, TalkW, RollH);
            RectTransform body = Rect("Body", roll); Stretch(body, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-CloseSize * 0.8f, 0f));
            Image bi = body.gameObject.AddComponent<Image>(); bi.sprite = BookArt.SheetRoll; bi.raycastTarget = false;
            RectTransform end = Rect("End", roll); end.anchorMin = end.anchorMax = new Vector2(0f, 0.5f); end.sizeDelta = new Vector2(30f, 30f); end.anchoredPosition = new Vector2(15f, 0f);
            Image ei = end.gameObject.AddComponent<Image>(); ei.sprite = BookArt.SheetRollEnd; ei.raycastTarget = false;

            RectTransform paper = Rect("Paper", scroll);
            Image pi = paper.gameObject.AddComponent<Image>(); pi.sprite = BookArt.TalkPaper(s.Title.GetStableHashCode()); pi.type = Image.Type.Sliced;
            VBox(paper, 9f, TextAnchor.UpperLeft, 28, 30, 18, 56);
            Fixed(paper.gameObject, TalkPaperW, -1f);
            paper.GetComponent<LayoutElement>().minHeight = TalkLeast;

            RectTransform shade = Rect("Shade", scroll); shade.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            shade.anchorMin = new Vector2(0.5f, 1f); shade.anchorMax = new Vector2(0.5f, 1f); shade.pivot = new Vector2(0.5f, 1f); shade.sizeDelta = new Vector2(TalkPaperW, 14f); shade.anchoredPosition = new Vector2(0f, -RollH);
            Image si = shade.gameObject.AddComponent<Image>(); si.sprite = BookArt.SheetRollShade; si.raycastTarget = false;

            RectTransform shield = Rect("Shield", scroll); shield.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            shield.anchorMin = shield.anchorMax = new Vector2(0.5f, 1f); shield.sizeDelta = new Vector2(ShieldD, ShieldD); shield.anchoredPosition = new Vector2(-6f, -RollH * 0.5f);
            Image shi = shield.gameObject.AddComponent<Image>(); shi.sprite = BookArt.SheetShield; shi.raycastTarget = false;
            UnityEngine.UI.Shadow ss = shield.gameObject.AddComponent<UnityEngine.UI.Shadow>(); ss.effectColor = new Color(0f, 0f, 0f, 0.45f); ss.effectDistance = new Vector2(0f, -4f);
            TMP_Text rune = T(shield, "ᛋ", 15f, C("#2a1a0c"), F.Rune, TextAlignmentOptions.Center); Stretch(rune.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform x = CloseButton(scroll, HideSheet); x.anchorMin = x.anchorMax = x.pivot = new Vector2(1f, 1f); x.anchoredPosition = new Vector2(-5f, 8f);
            return paper;
        }

        // the open tab, its rows made now (a click on a tab: only this)
        private static void SheetDraw()
        {
            Sheet s = _sheet;
            HideSheetTip();
            if (_sheetTabs != null)
            {
                for (int i = _sheetTabs.childCount - 1; i >= 0; i--) { GameObject g = _sheetTabs.GetChild(i).gameObject; g.transform.SetParent(null); UnityEngine.Object.Destroy(g); }
                for (int i = 0; i < s.Tabs.Count; i++) SheetTabButton(_sheetTabs, s, i);
                FitTabs(_sheetTabs, SheetRoom);
            }
            for (int i = _sheetBody.childCount - 1; i >= 0; i--) { GameObject g = _sheetBody.GetChild(i).gameObject; g.transform.SetParent(null); UnityEngine.Object.Destroy(g); }
            List<SheetRow> rows = null;
            if (s.Tabs.Count > 0 && s.Tabs[s.Open].Rows != null)
            {
                try { rows = s.Tabs[s.Open].Rows(); }
                catch (Exception e) { Npcs.Log("sheet «" + s.Title + "», tab «" + s.Tabs[s.Open].Title + "»: " + e); rows = new List<SheetRow> { SheetRow.Plain(Lang.T("Не вдалося це прочитати.")) }; }
            }
            if (rows != null) foreach (SheetRow r in rows) SheetAdd(_sheetBody, r);
            SheetFit();
        }

        private static void SheetFit()
        {
            _sheetScroll.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_sheetScroll);
            float h = LayoutUtility.GetPreferredHeight(_sheetScroll), room = 1080f - TalkTop - 20f;
            _sheetScroll.localScale = Vector3.one * (h > room ? room / h : 1f); // (a long one a little smaller, never off the screen)
        }

        // ---------------------------------------------------------------- its top
        private static Image SheetArrowImage(Transform to, Color c, bool right, float w)
        {
            RectTransform a = Rect("Arrow", to); Fixed(a.gameObject, w, w * 0.5f);
            Image i = a.gameObject.AddComponent<Image>(); i.sprite = BookArt.SheetArrow; i.color = c; i.raycastTarget = false;
            if (right) a.localRotation = Quaternion.Euler(0f, 0f, 180f);
            return i;
        }

        private static void SheetBack(RectTransform paper, Sheet s)
        {
            RectTransform row = Rect("Back", paper); HBox(row, 4f, TextAnchor.MiddleLeft, 0, 0, 0, 2);
            row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            Image arrow = SheetArrowImage(row, SRust, false, 18f);
            TMP_Text t = T(row, s.BackWord, 15f, SRust, F.Italic, TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
            Flex(t.gameObject);
            Clickable(row.gameObject, s.Back);
            Hover(row.gameObject, new Graphic[] { t, arrow }, SRust, null);
        }

        private static void SheetName(RectTransform paper, string title)
        {
            RectTransform row = Rect("Name", paper); HBox(row, 12f, TextAnchor.MiddleCenter, 0, 0, 10, 0);
            foreach (bool first in new[] { true, false })
            {
                if (!first) { TMP_Text t = T(row, title.ToUpperInvariant(), 30f, SName, F.Head, TextAlignmentOptions.Center); t.characterSpacing = 22f; Room(t, Mathf.Min(t.GetPreferredValues(t.text, float.PositiveInfinity, float.PositiveInfinity).x + 2f, SheetRoom - 40f), 16f); }
                RectTransform rule = Rect("Rule", row); Fixed(rule.gameObject, -1f, 1f); Flex(rule.gameObject);
                Image ri = rule.gameObject.AddComponent<Image>(); ri.color = A(PRule, 0.75f); ri.raycastTarget = false;
            }
        }

        private static void SheetNowBlock(RectTransform paper, Sheet s)
        {
            RectTransform block = Rect("Now", paper);
            Image bi = block.gameObject.AddComponent<Image>(); bi.sprite = BookArt.RoundFine(4f); bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = BookArt.Fine; bi.color = TBlock; bi.raycastTarget = false;
            VBox(block, 6f, TextAnchor.UpperLeft, 15, 15, 11, 11);
            if (s.Now.Length > 0)
            {
                RectTransform top = Rect("Top", block); HBox(top, 12f, TextAnchor.UpperLeft, 0, 0, 0, 0);
                TMP_Text word = T(top, Lang.T("ЗАРАЗ"), 13f, SNowWord, F.Head, TextAlignmentOptions.TopLeft); word.characterSpacing = 16f; Keep(word);
                word.GetComponent<LayoutElement>().minHeight = 24f;
                TMP_Text now = T(top, s.Now, 19f, TBlockInk, F.Body, TextAlignmentOptions.TopLeft); now.textWrappingMode = TextWrappingModes.Normal; Flex(now.gameObject);
            }
            if (s.Marks.Count == 0) return;
            float room = SheetRoom - 30f, used = room + 1f;
            RectTransform line = null;
            RectTransform box = Rect("Marks", block); VBox(box, 4f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            foreach (SheetMark m in s.Marks)
            {
                RectTransform pill = Rect("Pill", box);
                Image fill = pill.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(11f); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = BookArt.Fine; fill.color = A(SPill, 0.75f);
                Outline(pill, BookArt.FrameFine(11f, 1f), A(SNowWord, 0.3f));
                HBox(pill, 6f, TextAnchor.MiddleLeft, 10, 11, 3, 4);
                RectTransform dot = Rect("Dot", pill); Fixed(dot.gameObject, 8f, 8f);
                Image di = dot.gameObject.AddComponent<Image>(); di.sprite = BookArt.Circle; di.raycastTarget = false; di.color = m.Tone == ToneBad ? SDotBad : m.Tone == ToneWarn ? SDotWarn : SDotGood;
                TMP_Text t = T(pill, m.Text, 14.5f, C("#f0dfbb"), F.Italic, TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
                float w = Mathf.Min(room, t.GetPreferredValues(m.Text).x + 37f);
                if (line == null || used + 6f + w > room) { line = Rect("Line", box); HBox(line, 6f, TextAnchor.UpperLeft, 0, 0, 0, 0); used = -6f; }
                pill.SetParent(line, false);
                Fixed(pill.gameObject, w, -1f);
                used += 6f + w;
                SheetHook(pill.gameObject, m.TipHead, m.Tip, m.Click, new Graphic[] { t }, t.color, null);
            }
        }

        // three cards in a row: a small label, a dot and a word that stays on its one line (smaller, not broken in two); under the pointer the card lights and says why
        private static void SheetCards(RectTransform paper, List<SheetMark> cards)
        {
            RectTransform row = Rect("Cards", paper); HBox(row, 5f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            foreach (SheetMark m in cards)
            {
                RectTransform card = Rect("Card", row);
                LayoutElement le = card.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1f; le.preferredWidth = 0f; le.minWidth = 0f; le.minHeight = 54f;
                Image fill = card.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = BookArt.Fine; fill.color = SCard;
                Outline(card, BookArt.FrameFine(3f, 1f), A(PRule, 0.8f));
                VBox(card, 1f, TextAnchor.UpperLeft, 10, 6, 6, 7);
                Shrink(T(card, m.Label, 13.5f, SLabel, F.Italic, TextAlignmentOptions.TopLeft), 10f);
                RectTransform state = Rect("State", card); HBox(state, 6f, TextAnchor.MiddleLeft, 0, 0, 0, 0);
                RectTransform dot = Rect("Dot", state); Fixed(dot.gameObject, 13f, 13f);
                Image di = dot.gameObject.AddComponent<Image>(); di.sprite = BookArt.Circle; di.raycastTarget = false; di.color = m.Tone == ToneBad ? SPaperDotBad : m.Tone == ToneWarn ? SPaperDotWarn : SPaperDotGood;
                TMP_Text t = T(state, m.Text, 18f, PInk, F.Body, TextAlignmentOptions.MidlineLeft);
                Shrink(t, 12f);
                SheetHook(card.gameObject, m.TipHead, m.Tip, m.Click, new Graphic[0], PInk, null, fill, SCardLit);
            }
        }

        // how it is with you: its word, then a line painted by the brush (the book's stroke: BookArt.SheetScale, the user 2026-11-06) with a dark mark on it
        private static void SheetAttitude(Transform to, SheetRow r)
        {
            RectTransform row = Rect("Attitude", to);
            HBox(row, 8f, TextAnchor.MiddleLeft, 0, 0, 3, 3);
            row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            Keep(T(row, Lang.T("до тебе"), 14f, SLabel, F.Italic, TextAlignmentOptions.MidlineLeft));
            int at = Mathf.Clamp(r.Tone, 0, SAttitudeWord.Length - 1);
            TMP_Text word = T(row, r.Text, 18f, SAttitudeWord[at], F.Body, TextAlignmentOptions.MidlineLeft); Keep(word);
            RectTransform bar = Rect("Bar", row); Fixed(bar.gameObject, -1f, 14f); Flex(bar.gameObject);
            Image bi = bar.gameObject.AddComponent<Image>(); bi.sprite = BookArt.SheetScale; bi.raycastTarget = false;
            RectTransform mark = Rect("At", bar); mark.anchorMin = mark.anchorMax = new Vector2(Mathf.Clamp01(r.Share), 0.5f); mark.sizeDelta = new Vector2(5f, 20f);
            Image mi = mark.gameObject.AddComponent<Image>(); mi.sprite = BookArt.RoundFine(1.5f); mi.type = Image.Type.Sliced; mi.pixelsPerUnitMultiplier = BookArt.Fine; mi.color = PInk; mi.raycastTarget = false;
            UnityEngine.UI.Outline ol = mark.gameObject.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = TPaper; ol.effectDistance = new Vector2(1f, 1f);
            SheetHook(row.gameObject, r.TipHead, r.Tip, null, new Graphic[0], SAttitudeWord[at], null);
        }

        private static void SheetTabButton(RectTransform row, Sheet s, int i)
        {
            bool on = i == s.Open;
            RectTransform tab = Rect("Tab", row);
            Image bg = tab.gameObject.AddComponent<Image>(); bg.sprite = BookArt.RoundFine(4f, true); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = BookArt.Fine; bg.color = on ? STabOn : STabOff;
            HBox(tab, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            Color ink = on ? STabOnInk : STabOffInk;
            string alert = s.Tabs[i].Tone == ToneBad ? " <color=#e0603e>•</color>" : s.Tabs[i].Tone == ToneWarn ? " <color=#e0b04a>•</color>" : "";
            TMP_Text t = T(tab, s.Tabs[i].Title + alert, 13f, ink, F.Head, TextAlignmentOptions.Center); t.characterSpacing = 10f; t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(tab.gameObject, t.GetPreferredValues(t.text).x + 26f, on ? 31f : 27f);
            if (on) return;
            int at = i;
            Clickable(tab.gameObject, () => { if (_sheet != null) { _sheet.Open = at; SheetDraw(); } });
            Hover(tab.gameObject, new Graphic[] { t }, ink, null);
        }

        // ---------------------------------------------------------------- a row
        private static void SheetAdd(Transform to, SheetRow r)
        {
            switch (r.Kind)
            {
                case SheetKind.Label: T(to, r.Text, 14.5f, SRust, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.NoWrap; break;
                case SheetKind.Text: SheetText(to, r); break;
                case SheetKind.Attitude: SheetAttitude(to, r); break;
                case SheetKind.Plus: case SheetKind.Minus: SheetLine(to, r); break;
                case SheetKind.Mind: SheetMindRow(to, r); break;
                case SheetKind.Chips: SheetChips(to, r); break;
                case SheetKind.Pair: SheetPair(to, r); break;
                case SheetKind.Fold: SheetFold(to, r); break;
            }
        }

        private static void SheetRule(Transform to)
        {
            RectTransform line = Rect("Rule", to); Fixed(line.gameObject, -1f, 1f);
            Image li = line.gameObject.AddComponent<Image>(); li.color = A(PRule, 0.45f); li.raycastTarget = false;
        }

        private static Color ToneInk(int tone) { return tone == ToneBad ? SBad : tone == ToneWarn ? SWarn : tone == ToneGood ? SGood : PInk; }

        private static void SheetText(Transform to, SheetRow r)
        {
            Color ink = ToneInk(r.Tone);
            TMP_Text t = T(to, r.Text, 16f, ink, F.Body, TextAlignmentOptions.TopLeft); t.textWrappingMode = TextWrappingModes.Normal;
            SheetHook(t.gameObject, r.TipHead, r.Tip, r.Click, new Graphic[] { t }, ink, null);
        }

        // what it remembers: «—» or «+», the words in plain ink, when at the far right, a thin rule under
        private static void SheetLine(Transform to, SheetRow r)
        {
            bool up = r.Kind == SheetKind.Plus;
            RectTransform row = Rect("Line", to);
            HBox(row, 8f, TextAnchor.UpperLeft, 0, 0, 1, 0);
            TMP_Text sign = T(row, up ? "+" : "—", 15f, up ? PSoft : SBad, F.Body, TextAlignmentOptions.TopLeft); Fixed(sign.gameObject, 14f, -1f);
            TMP_Text t = T(row, r.Text, 17f, PInk, F.Body, TextAlignmentOptions.TopLeft); t.textWrappingMode = TextWrappingModes.Normal; Flex(t.gameObject);
            if (r.Far.Length > 0) Keep(T(row, r.Far, 14f, SLabel, F.Italic, TextAlignmentOptions.TopRight));
            SheetRule(to);
        }

        // its own words, rust and slanted, a thin rule under
        private static void SheetMindRow(Transform to, SheetRow r)
        {
            T(to, r.Text, 17f, SSaid, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
            SheetRule(to);
        }

        private static void SheetChips(Transform to, SheetRow r)
        {
            RectTransform box = Rect("Chips", to);
            VBox(box, 6f, TextAnchor.UpperLeft, 0, 0, 2, 2);
            float used = SheetRoom + 1f;
            RectTransform line = null;
            foreach (SheetMark m in r.Marks)
            {
                Color ink = m.Hot ? TStoryInk : PInk;
                Image fill;
                TMP_Text t = Chip(box, m.Text, 16f, ink, m.Hot ? 0.95f : 0.8f, 0.3f, SheetRoom, ref line, ref used, out fill);
                if (m.Hot) fill.color = new Color(1f, 0.84f, 0.55f, 0.5f);
                SheetHook(fill.gameObject, m.TipHead, m.Tip, m.Click, new Graphic[] { t }, ink, m.Click != null ? fill : null);
            }
        }

        // a small slanted label, its value, what stands at the far right («мрія … уже 3 дні»), a thin rule under
        private static void SheetPair(Transform to, SheetRow r)
        {
            RectTransform row = Rect("Pair", to);
            HBox(row, 10f, TextAnchor.UpperLeft, 0, 0, 1, 0);
            Room(T(row, r.Text, 14f, SLabel, F.Italic, TextAlignmentOptions.TopLeft), 92f, 10f);
            Color ink = ToneInk(r.Tone);
            TMP_Text v = T(row, r.Right, 17f, ink, F.Body, TextAlignmentOptions.TopLeft); v.textWrappingMode = TextWrappingModes.Normal; Flex(v.gameObject);
            if (r.Far.Length > 0) Keep(T(row, r.Far, 14f, SLabel, F.Italic, TextAlignmentOptions.TopRight));
            if (r.Tip != null || r.Click != null) row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            SheetHook(row.gameObject, r.TipHead, r.Tip, r.Click, new Graphic[] { v }, ink, null);
            SheetRule(to);
        }

        // words with more behind them: a framed «Розкажи більше →» opens the rest on the spot
        private static void SheetFold(Transform to, SheetRow r)
        {
            RectTransform box = Rect("Fold", to);
            VBox(box, 10f, TextAnchor.UpperLeft, 0, 0, 2, 2);
            T(box, r.Text, 18f, PInk, F.Body, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
            if (string.IsNullOrEmpty(r.More)) return;
            RectTransform line = Rect("Line", box); HBox(line, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            RectTransform open = Rect("Open", line);
            Image fill = open.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = BookArt.Fine; fill.color = new Color(1f, 0.98f, 0.92f, 0.35f);
            Outline(open, BookArt.FrameFine(3f, 1f), A(TStoryEdge, 0.8f));
            HBox(open, 8f, TextAnchor.MiddleLeft, 14, 14, 6, 7);
            TMP_Text word = T(open, r.Right, 17f, SSaid, F.Body, TextAlignmentOptions.MidlineLeft); word.textWrappingMode = TextWrappingModes.NoWrap;
            Image arrow = SheetArrowImage(open, SSaid, true, 22f);
            Fixed(open.gameObject, word.GetPreferredValues(r.Right).x + 58f, -1f);
            string more = r.More;
            Clickable(open.gameObject, () =>
            {
                line.SetParent(null); UnityEngine.Object.Destroy(line.gameObject);
                T(box, more, 18f, PInk, F.Body, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
                SheetFit();
            });
            Hover(open.gameObject, new Graphic[] { word, arrow }, SSaid, fill);
        }

        // ---------------------------------------------------------------- the pointer on a row
        private static void SheetHook(GameObject g, string tipHead, string tip, Action click, Graphic[] targets, Color normal, Image fill, Image lit = null, Color litColor = default(Color))
        {
            if (string.IsNullOrEmpty(tip) && click == null) return;
            if (g.GetComponent<Graphic>() == null) g.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            if (!string.IsNullOrEmpty(tip))
            {
                SheetTipLater t = g.AddComponent<SheetTipLater>();
                t.Head = tipHead; t.Text = Grammar.Fix(tip); t.Row = (RectTransform)g.transform;
                t.Lit = lit; t.LitOn = litColor; if (lit != null) t.LitOff = lit.color;
            }
            if (click != null) Clickable(g, click);
            if (targets.Length > 0) Hover(g, targets, normal, fill);
        }

        // the one box for the why, beside the scroll: its arrow at the row, its head, its lines («−» and «+» in their colours)
        private static void SheetBox()
        {
            _sheetTip = Rect("Why", _sheetRoot.transform);
            _sheetTip.anchorMin = _sheetTip.anchorMax = _sheetTip.pivot = new Vector2(0f, 1f);
            _sheetTip.sizeDelta = new Vector2(280f, 10f);
            Image bg = _sheetTip.gameObject.AddComponent<Image>(); bg.sprite = BookArt.RoundFine(3f); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = BookArt.Fine; bg.color = STipFill; bg.raycastTarget = false;
            UnityEngine.UI.Shadow sh = _sheetTip.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(2f, -4f);
            Outline(_sheetTip, BookArt.FrameFine(3f, 1f), TStoryEdge);
            VBox(_sheetTip, 5f, TextAnchor.UpperLeft, 15, 14, 11, 12);
            _sheetTip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform arrow = Rect("Arrow", _sheetTip); arrow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            arrow.anchorMin = arrow.anchorMax = new Vector2(0f, 1f); arrow.sizeDelta = new Vector2(18f, 18f); arrow.anchoredPosition = new Vector2(0f, -24f);
            Image ai = arrow.gameObject.AddComponent<Image>(); ai.sprite = BookArt.Diamond; ai.color = STipFill; ai.raycastTarget = false;
            _sheetTipHead = T(_sheetTip, "", 13f, TMind, F.Head, TextAlignmentOptions.TopLeft); _sheetTipHead.characterSpacing = 12f; _sheetTipHead.raycastTarget = false;
            _sheetTipText = T(_sheetTip, "", 16f, PInk, F.Body, TextAlignmentOptions.TopLeft);
            _sheetTipText.textWrappingMode = TextWrappingModes.Normal; _sheetTipText.raycastTarget = false; _sheetTipText.lineSpacing = 12f;
            _sheetTip.gameObject.SetActive(false);
        }

        private static void HideSheetTip() { if (_sheetTip != null) _sheetTip.gameObject.SetActive(false); }

        private static string SheetTipLines(string text)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("− ", StringComparison.Ordinal)) lines[i] = "<color=#8a2216>—</color>   " + lines[i].Substring(2);
                else if (lines[i].StartsWith("+ ", StringComparison.Ordinal)) lines[i] = "<color=#6b5636>+</color>   " + lines[i].Substring(2);
            }
            return string.Join("\n", lines);
        }

        internal sealed class SheetTipLater : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal string Head, Text; internal RectTransform Row; internal Image Lit; internal Color LitOn, LitOff;

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            {
                if (_sheetTip == null || _sheetScroll == null || Row == null) return;
                if (Lit != null) Lit.color = LitOn;
                _sheetTipHead.text = Head ?? "";
                _sheetTipHead.gameObject.SetActive(!string.IsNullOrEmpty(Head));
                _sheetTipText.text = SheetTipLines(Text);
                _sheetTip.gameObject.SetActive(true);
                _sheetTip.SetAsLastSibling();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_sheetTip);
                Vector3[] rowAt = new Vector3[4], scrollAt = new Vector3[4], tipAt = new Vector3[4];
                Row.GetWorldCorners(rowAt); _sheetScroll.GetWorldCorners(scrollAt);
                float unit = _sheetTip.lossyScale.x;
                _sheetTip.position = new Vector3(scrollAt[2].x + 14f * unit, rowAt[1].y + 12f * unit, 0f);
                _sheetTip.GetWorldCorners(tipAt);
                if (tipAt[0].y < 8f) _sheetTip.position += new Vector3(0f, 8f - tipAt[0].y, 0f); // (never off the bottom of the screen)
                if (tipAt[2].x > Screen.width - 8f) _sheetTip.position += new Vector3(Screen.width - 8f - tipAt[2].x, 0f, 0f); // (a narrow screen: over the window's edge rather than off the side)
            }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            {
                if (Lit != null) Lit.color = LitOff;
                if (_sheetTip != null) _sheetTip.gameObject.SetActive(false);
            }
        }

        internal sealed class SheetKeys : MonoBehaviour
        {
            internal CanvasGroup Fade;
            internal Func<bool> Stay;
            private float _opened, _nextStay;
            private void Start() { _opened = Time.time; }
            private void Update()
            {
                if (Fade != null) { Fade.alpha = Mathf.MoveTowards(Fade.alpha, 1f, Time.unscaledDeltaTime / SheetFade); if (Fade.alpha >= 1f) { Destroy(Fade); Fade = null; } }
                if (Input.GetKeyDown(KeyCode.Escape)) { HideSheet(); return; }
                Player p = Player.m_localPlayer;
                if (p == null || p.IsDead() || Senses.HurtByFoeAt > _opened) { HideSheet(); return; } // (as a talk: a blow from a foe and you are back in the game)
                if (Time.time < _nextStay) return;
                _nextStay = Time.time + 0.5f;
                if (Stay != null && !Stay()) HideSheet();
            }
        }
    }
}
#endif

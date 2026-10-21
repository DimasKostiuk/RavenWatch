#if RAVEN_ITEM
using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The post in the book (Post): a small envelope on the book's upper left corner, beside "?" — grey, with a word why,
    // when there is nobody on the server to write to. A click opens the post's card (the user's design,
    // Downloads\mail-1a.png; the card itself: RavenBookCard.cs): «Що» — a letter or a parcel; «Несе» — the ravens on
    // you, their strength; «Кому» — one name, «змінити ▾» drops the list of players (those without the mod or not taking
    // ravens greyed, the reason on hover); the letter on lined paper or the parcel's cell; under it the price and who
    // carries it, «Скасувати» and «Відправити». (The letter that comes to you: RavenBookLetter.cs.)
    internal static partial class RavenBook
    {
        // ---------------------------------------------------------------- the envelope on the corner

        private static RectTransform _mailCorner;
        private static Image _mailRing, _mailIcon;

        private static void BuildMailCorner()
        {
            _mailCorner = CornerButton("Mail", 48f, AbilityIcons.Get("mail"));
            Transform ring = _mailCorner.GetChild(1);
            _mailRing = ring.GetComponent<Image>();
            Transform icon = ring.Find("Icon");
            _mailIcon = icon != null ? icon.GetComponent<Image>() : null;
            if (_mailIcon != null) _mailIcon.rectTransform.offsetMin = new Vector2(11f, 12f); // (the envelope is wider than tall)
            if (_mailIcon != null) _mailIcon.rectTransform.offsetMax = new Vector2(-11f, -12f);
            Clickable(ring.gameObject, () =>
            {
                string why = MailWhyNot();
                if (why != null) { Tell(why); return; }
                MailPopup();
            });
            Tip(ring.gameObject, () =>
            {
                string why = MailWhyNot();
                return "<b>" + Lang.T("Пошта") + "</b>\n" + (why != null ? "<color=" + GreyHex + ">" + why + "</color>"
                    : Lang.F("Лист ({0} сили й пергамент) чи посилка ({1} сили) іншому гравцю. Несе ворон.", Mathf.RoundToInt(Post.LetterCost), Mathf.RoundToInt(Post.ParcelCost)));
            });
            _mailCorner.SetAsLastSibling();
        }

        // why the envelope is grey: nobody on the server, or no raven of yours on the letter's trust level yet
        private static string MailWhyNot() { return Post.NobodyWhy() ?? Post.LockedWhy(Player.m_localPlayer); }

        // grey while it cannot be used (on opening the book, then once a second)
        private static void MailCorner()
        {
            if (_mailCorner == null) return;
            bool can = MailWhyNot() == null;
            if (_mailRing != null) _mailRing.color = can ? C("#a8843a") : A(C("#a8843a"), 0.35f);
            if (_mailIcon != null) _mailIcon.color = can ? Gold : A(Muted, 0.6f);
        }

        // ---------------------------------------------------------------- the card

        private static bool _mailUp;
        private static int _mKind = Post.Letter;
        private static int _mRaven = -1;      // -1: not chosen yet (the first that can), 0 Hugin, 1 Munin
        private static long _mTo;
        private static string _mText = "";
        private static RectTransform _mToRow, _mDrop;
        private static int _mListVer = -1;
        private static bool _mListAsking;
        private static TMP_InputField _mField;
        private static TMP_Text _mCount, _mErr;

        private const int MailRuleH = 28;       // the lined paper: one line of the letter per rule
        private const float MailTextSize = 19f;

        internal static bool MailTyping { get { return _mField != null && _mField.isFocused; } }

        // the parcel's window shut: the book again, on the post
        internal static void OpenPost()
        {
            if (!IsOpen) Open();
            if (IsOpen) MailPopup();
        }

        private static void MailForget() { _mailUp = false; _mToRow = null; _mDrop = null; _mField = null; _mCount = null; _mErr = null; _mKinds = null; _mWho = null; _mBody = null; _mBodyLe = null; _mPrice = null; _mCarrier = null; }

        private static void MailPopup()
        {
            Player p = Player.m_localPlayer;
            ClosePopup();
            HideTip();
            if (_root == null || p == null) return;
            if (Time.unscaledTime - Post.AskedAt > 5f) Post.Ask();
            MailPickRaven(p, _mRaven < 0 || Trust.Worn(_mRaven == 1) == null);

            Card card = OpenCard("Post", Lang.T("Пошта"), 660f, true, true);
            _mailUp = true;
            RectTransform paper = card.Paper;
            paper.GetComponent<VerticalLayoutGroup>().spacing = 14f;

            // what (the two cards redrawn in place on a switch: the window does not jump)
            RectTransform what = MailRow(paper, Lang.T("Що"), 54f);
            _mKinds = Rect("Kinds", what);
            _mKinds.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Fixed(_mKinds.gameObject, -1f, 54f);
            HBox(_mKinds, 14f, TextAnchor.MiddleLeft).childForceExpandHeight = true;
            MailKinds();

            // who carries it (redrawn in place: on a switch of kind or raven)
            RectTransform who = MailRow(paper, Lang.T("Несе"), 74f);
            _mWho = Rect("Ravens", who);
            _mWho.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Fixed(_mWho.gameObject, -1f, 74f);
            HBox(_mWho, 14f, TextAnchor.MiddleLeft).childForceExpandHeight = true;
            MailRavens();

            // to whom
            RectTransform to = MailRow(paper, Lang.T("Кому"), 40f);
            _mToRow = Rect("To", to);
            _mToRow.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Fixed(_mToRow.gameObject, -1f, 40f);
            HBox(_mToRow, 10f, TextAnchor.MiddleLeft, 8, 4, 0, 4);
            MailTo();

            // the letter's words or the parcel: a page in a body as tall as it, its height eased on a switch
            _mBody = Rect("Body", paper);
            _mBody.gameObject.AddComponent<RectMask2D>();
            _mBodyLe = _mBody.gameObject.AddComponent<LayoutElement>();
            RectTransform page = MailPage(p);

            // two lines' room kept, so the card does not grow when it speaks (one 18 px line was lower than the font's
            // line: TMP's ellipsis then drew nothing at all, and «Відправити» looked dead)
            _mErr = T(paper, "", 15f, BookArt.Bad, F.Italic);
            _mErr.textWrappingMode = TextWrappingModes.Normal; _mErr.overflowMode = TextOverflowModes.Ellipsis;
            Fixed(_mErr.gameObject, -1f, 42f);
            _mErrUntil = 0f; _mStandWas = null; _mStandAt = 0f;
            MailStanding();

            // the price and who carries it; «Скасувати», «Відправити»
            _mPrice = T(card.FootWords, "", 18f, FootInk, F.Body);
            _mPrice.textWrappingMode = TextWrappingModes.NoWrap;
            _mCarrier = T(card.FootWords, "", 14f, FootSoft, F.Italic);
            MailPrice();
            CardButton(card.Foot, Lang.T("Скасувати"), false, ClosePopup);
            CardButton(card.Foot, Lang.T("Відправити"), true, MailSend);

            Vector2 at = PlaceCard(card, null);
            // from here the card follows what is on it (a switch eases its height), growing both ways from its middle
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            _mBodyLe.minHeight = _mBodyLe.preferredHeight = LayoutUtility.GetPreferredHeight(page);
            RectTransform f = card.Frame;
            LayoutRebuilder.ForceRebuildLayoutImmediate(f);
            float h = LayoutUtility.GetPreferredHeight(f);
            f.pivot = new Vector2(0.5f, 0.5f);
            f.anchoredPosition = new Vector2(at.x, BookH / 2f - 13f); // (the plaque's 26 over the top edge: the whole in the book's middle)
            f.sizeDelta = new Vector2(card.Width, h);
            f.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (_mField != null) { _mField.ActivateInputField(); _mField.MoveTextEnd(false); }
        }

        private static RectTransform _mKinds, _mWho, _mBody;
        private static LayoutElement _mBodyLe;
        private static TMP_Text _mPrice, _mCarrier;

        // the raven that carries it: kept while it can carry this kind; else the other one if it can (always: any worn)
        private static void MailPickRaven(Player p, bool always)
        {
            int was = _mRaven;
            if (!always && was >= 0 && Post.RavenWhyNot(p, was == 1, _mKind) == null) return;
            _mRaven = Post.RavenWhyNot(p, false, _mKind) == null ? 0 : Post.RavenWhyNot(p, true, _mKind) == null ? 1
                : was >= 0 && Trust.Worn(was == 1) != null ? was : Trust.Worn(false) != null ? 0 : 1;
        }

        private static void MailRavens()
        {
            if (_mWho == null) return;
            Player p = Player.m_localPlayer;
            for (int i = _mWho.childCount - 1; i >= 0; i--) { Transform old = _mWho.GetChild(i); old.SetParent(null, false); UnityEngine.Object.Destroy(old.gameObject); }
            if (p == null) return;
            if (Trust.Worn(false) == null && Trust.Worn(true) == null) T(_mWho, Lang.T("На плечах нема воронів."), 16f, PSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            else { RavenCard(_mWho, p, false); RavenCard(_mWho, p, true); }
        }

        private static void MailKinds()
        {
            if (_mKinds == null) return;
            for (int i = _mKinds.childCount - 1; i >= 0; i--) { Transform old = _mKinds.GetChild(i); old.SetParent(null, false); UnityEngine.Object.Destroy(old.gameObject); }
            KindCard(_mKinds, Post.Letter, "ᛚ", Lang.T("Лист"), Lang.T("1 пергамент"),
                () => Lang.F("Кілька слів на пергаменті. {0} сили й один пергамент (Хальдор, {1} монет).", Mathf.RoundToInt(Post.LetterCost), RavenItem.LetterPrice));
            KindCard(_mKinds, Post.Parcel, "ᛒ", Lang.T("Посилка"), Lang.T("предмет із сумки"),
                () => Lang.F("Одна дрібна річ до {0} кг (монети — скільки влізе в цю вагу). {1} сили.", Satiety.Num(Post.MaxWeight), Mathf.RoundToInt(Post.ParcelCost)));
        }

        private static void MailPrice()
        {
            if (_mPrice == null) return;
            float cost = Power.Price(_mRaven == 1, Post.Cost(_mKind)); // (a hungry raven asks more, as for its gifts)
            _mPrice.text = _mKind == Post.Letter ? Lang.F("−{0} сили · 1 пергамент", Mathf.RoundToInt(cost)) : Lang.F("−{0} сили", Mathf.RoundToInt(cost));
            if (_mCarrier != null) _mCarrier.text = _mRaven >= 0 && Post.RavenWhyNot(Player.m_localPlayer, _mRaven == 1, _mKind) == null ? Lang.F("несе {0}", Plugin.RavenName(_mRaven == 1)) : ""; // (none that can: no name under the price)
        }

        // the letter's or the parcel's page, at the top of the body, as wide as it
        private static RectTransform MailPage(Player p)
        {
            RectTransform page = Rect("Page", _mBody);
            page.anchorMin = new Vector2(0f, 1f); page.anchorMax = new Vector2(1f, 1f); page.pivot = new Vector2(0.5f, 1f);
            page.offsetMin = page.offsetMax = Vector2.zero;
            VBox(page, 14f, TextAnchor.UpperLeft);
            page.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            page.gameObject.AddComponent<CanvasGroup>();
            _mField = null; _mCount = null;
            if (_mKind == Post.Letter) MailText(page, p);
            else MailParcel(page, p);
            return page;
        }

        // Лист ↔ Посилка: the cards and the price at once; the page under them fades into the other while the body
        // eases to its height (the card with it, from its middle)
        private static void SetMailKind(int kind)
        {
            Player p = Player.m_localPlayer;
            if (kind == _mKind || p == null || _mBody == null) return;
            _mKind = kind;
            HideTip();
            MailPickRaven(p, false); // (the chosen one too young for a parcel: the other one, when it can)
            MailKinds();
            MailRavens();
            MailPrice();
            _mErrUntil = 0f; MailStanding();
            Transform old = _mBody.childCount > 0 ? _mBody.GetChild(_mBody.childCount - 1) : null;
            float from = _mBodyLe.preferredHeight;
            RectTransform page = MailPage(p);
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            MailMorph m = _mBody.gameObject.GetComponent<MailMorph>();
            if (m == null) m = _mBody.gameObject.AddComponent<MailMorph>();
            m.Begin(_mBodyLe, old as RectTransform, page, from, LayoutUtility.GetPreferredHeight(page));
            if (_mField != null) { _mField.ActivateInputField(); _mField.MoveTextEnd(false); }
        }

        // an equal share of its row, whatever is written in it (preferred 0: only the flexible part counts)
        private static void HalfWidth(RectTransform r)
        {
            LayoutElement le = r.GetComponent<LayoutElement>();
            if (le == null) le = r.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f; le.preferredWidth = 0f; le.flexibleWidth = 1f;
        }

        // a row of the card: its name on the left (the design's muted gold), its things after
        private static RectTransform MailRow(Transform paper, string name, float height)
        {
            RectTransform r = Rect("Row", paper);
            HBox(r, 14f, TextAnchor.MiddleLeft);
            Fixed(r.gameObject, -1f, height);
            TMP_Text t = T(r, name, 21f, PLabel, F.Body, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(t.gameObject, 72f, -1f);
            return r;
        }

        // «Лист» / «Посилка»: the rune in its ring, the name, a word under it; chosen — dark with the gold line
        private static void KindCard(RectTransform row, int kind, string glyph, string title, string sub, Func<string> tip)
        {
            bool on = _mKind == kind;
            RectTransform r = Rect("Kind", row);
            Fixed(r.gameObject, -1f, 54f);
            HalfWidth(r);
            Box(r, 6f, on ? PDark : new Color(1f, 1f, 1f, 0f), on ? PGoldLine : PEdge, on ? 2f : 1.5f);
            HBox(r, 12f, TextAnchor.MiddleLeft, 10, 10, 0, 0);
            RectTransform ring = Rect("Ring", r);
            Fixed(ring.gameObject, 36f, 36f);
            Box(ring, 18f, new Color(0f, 0f, 0f, 0f), on ? Gold : PInk, 1.6f);
            TMP_Text g = T(ring, glyph, 19f, on ? Gold : PInk, F.Rune, TextAlignmentOptions.Center);
            Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform words = Rect("Words", r);
            VBox(words, -2f, TextAnchor.MiddleLeft);
            HalfWidth(words);
            T(words, title, 21f, on ? PCream : PInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            // no raven on you trusts you enough for it yet: the word under it says from which level
            Player p = Player.m_localPlayer;
            bool young = (Trust.Worn(false) != null || Trust.Worn(true) != null) && !Post.LevelOk(p, false, kind) && !Post.LevelOk(p, true, kind); // (nobody on you: the row under says that)
            if (young) sub = Lang.F("з {0}-го рівня довіри", Post.LevelFor(kind));
            TMP_Text st = T(words, sub, 13f, young ? (on ? C("#e08a7a") : BookArt.Bad) : on ? C("#d9b56a") : PSoft, F.Italic);
            st.textWrappingMode = TextWrappingModes.NoWrap; st.overflowMode = TextOverflowModes.Ellipsis;
            Tip(r.gameObject, () => tip() + "\n" + Lang.F("Несе ворон із {0}-го рівня довіри.", Post.LevelFor(kind)));
            if (!on) Clickable(r.gameObject, () => SetMailKind(kind));
        }

        // a raven on you: its portrait, name in its colour, its strength and stroke, a bookmark ribbon; the chosen one
        // light and outlined in its colour, the other paler; one that cannot fly now — faded, the reason under its name
        private static void RavenCard(RectTransform row, Player p, bool m)
        {
            ItemDrop.ItemData raven = Trust.Worn(m);
            RectTransform r = Rect("Raven", row);
            Fixed(r.gameObject, -1f, 74f);
            HalfWidth(r);
            Color ink = BookArt.RavenInk(m);
            if (raven == null)
            {
                Box(r, 6f, new Color(1f, 1f, 1f, 0f), A(PEdge, 0.5f), 1.2f);
                TMP_Text none = T(r, Lang.F("{0} не на плечі", Plugin.RavenName(m)), 15f, PSoft, F.Italic, TextAlignmentOptions.Center);
                Stretch(none.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                return;
            }
            bool on = _mRaven == (m ? 1 : 0);
            string why = Post.PlaceWhyNot(p) ?? Post.RavenWhyNot(p, m, _mKind);          // (the full sentence: in the tooltip)
            string shortWhy = Post.PlaceWhyShort(p) ?? Post.RavenWhyShort(p, m, _mKind); // (a word or two: on the card)
            if (why != null && shortWhy == null) shortWhy = why;
            Box(r, 6f, on ? PCream : new Color(1f, 1f, 1f, 0f), on ? ink : PEdge, on ? 2f : 1.5f);
            if (!on || why != null) r.gameObject.AddComponent<CanvasGroup>().alpha = why != null ? 0.45f : 0.62f;
            HBox(r, 12f, TextAnchor.MiddleLeft, 8, 26, 0, 0);
            Portrait(r, m, 48f);
            RectTransform col = Rect("Col", r);
            VBox(col, 2f, TextAnchor.MiddleLeft);
            HalfWidth(col); // (its words never widen the card: both cards stay half and half)
            TMP_Text nm = T(col, Plugin.RavenName(m), 24f, ink, F.Head);
            nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
            if (why != null)
            {
                TMP_Text w = T(col, shortWhy, 14f, BookArt.Bad, F.Italic);
                w.textWrappingMode = TextWrappingModes.NoWrap; w.overflowMode = TextOverflowModes.Ellipsis;
            }
            else
            {
                RectTransform line = Rect("Strength", col);
                HBox(line, 8f, TextAnchor.MiddleLeft);
                T(line, "<b>" + Lang.T("Сила") + "</b>", 14f, PLabel, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(line);
                T(line, Mathf.FloorToInt(Power.Get(raven)) + " / " + Mathf.RoundToInt(Trust.Strength(raven)), 14f, PInk, F.Body, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
                BrushBar(col, () => Trust.Strength(raven) > 0f ? Mathf.Clamp01(Power.Get(raven) / Trust.Strength(raven)) : 0f, m ? "brush-munin" : "brush-huginn", 6f, 10f, null);
            }
            Image rib = Img(r, "Ribbon", BookArt.Picture("card_ribbon"), ink);
            rib.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rr = rib.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = on ? new Vector2(13f, 44f) : new Vector2(13f, 22f);
            rr.anchoredPosition = new Vector2(-20f, on ? 6f : 0f); // the chosen one's ribbon from above the card
            Tip(r.gameObject, () => why ?? (m ? Lang.T("Мунін донесе й на прощання обматюкає того, кому приніс.") : Lang.T("Хугін донесе й на прощання гарненько підколе.")));
            if (why == null && !on) Clickable(r.gameObject, () => { _mRaven = m ? 1 : 0; HideTip(); MailRavens(); MailPrice(); _mErrUntil = 0f; MailStanding(); });
        }

        // «Кому»: the chosen name (the first who can take a raven, when none is chosen), «змінити ▾»; a line under it
        private static void MailTo()
        {
            if (_mToRow == null) return;
            _mListVer = Post.Version; _mListAsking = Post.Asking;
            for (int i = _mToRow.childCount - 1; i >= 0; i--) { Transform old = _mToRow.GetChild(i); old.SetParent(null, false); UnityEngine.Object.Destroy(old.gameObject); }
            Post.Peer cur = Post.Peers.Find(x => x.Uid == _mTo);
            if (cur == null || !cur.Mod || !cur.Open)
            {
                Post.Peer first = Post.Peers.Find(x => x.Mod && x.Open);
                if (first != null) { _mTo = first.Uid; cur = first; }
            }
            bool can = cur != null && cur.Mod && cur.Open;
            if (cur != null)
            {
                string name = cur.Name, note = "";
                const string test = " (тест)";
                if (name.EndsWith(test, StringComparison.Ordinal)) { name = name.Substring(0, name.Length - test.Length); note = Lang.T("(тест)"); }
                else if (can && cur.Near) note = Lang.T("(поруч)");
                TMP_Text n = T(_mToRow, name, 24f, can ? PInk : PSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
                n.richText = false; n.fontStyle = FontStyles.Bold;
                n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
                if (!can) note = cur.Mod ? cur.Why : Post.Asking ? Lang.T("питаю…") : Lang.T("без RavenWatch");
                if (note.Length > 0) T(_mToRow, note, 15f, PSoft, F.Body, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            }
            else T(_mToRow, Post.Asking ? Lang.T("питаю, хто тут…") : Lang.T("Нікого нема."), 18f, PSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            Spring(_mToRow);
            if (Post.Peers.Count > 0)
            {
                RectTransform change = Rect("Change", _mToRow);
                HBox(change, 6f, TextAnchor.MiddleRight);
                TMP_Text ct = T(change, Lang.T("змінити"), 15f, PSoft, F.Body, TextAlignmentOptions.MidlineRight);
                ct.textWrappingMode = TextWrappingModes.NoWrap;
                Image[] ch = Chevron(change, PSoft); // (▾ is not in the fonts: drawn)
                Clickable(change.gameObject, () => { if (_mDrop != null) MailDropClose(); else MailDrop(); });
                Hover(change.gameObject, new Graphic[] { ct, ch[0], ch[1] }, PSoft, null); // gold under the pointer
            }
            Image rule = Img(_mToRow, "Rule", null, PRule);
            rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rule.rectTransform.anchorMin = Vector2.zero; rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1.5f); rule.rectTransform.anchoredPosition = Vector2.zero;
        }

        // a small "v": two strokes meeting at the bottom
        private static Image[] Chevron(Transform row, Color ink)
        {
            RectTransform box = Rect("Chevron", row);
            Fixed(box.gameObject, 10f, 10f);
            Image[] strokes = new Image[2];
            foreach (float side in new float[] { -1f, 1f })
            {
                Image s = Img(box, "Stroke", null, ink);
                strokes[side < 0f ? 0 : 1] = s;
                RectTransform r = s.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(6.5f, 1.6f);
                r.anchoredPosition = new Vector2(side * 2.1f, -0.5f);
                r.localRotation = Quaternion.Euler(0f, 0f, side * 45f);
            }
            return strokes;
        }

        // the players dropped under «Кому» (over the rest of the card; a click beside it shuts only the list)
        private static void MailDrop()
        {
            MailDropClose();
            if (_mToRow == null || _popup == null) return;
            RectTransform layer = (RectTransform)_popup.transform;
            RectTransform drop = Rect("Drop", layer);
            Stretch(drop, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            drop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            drop.gameObject.AddComponent<Button>().onClick.AddListener(MailDropClose);
            _mDrop = drop;

            Vector3[] cs = new Vector3[4];
            _mToRow.GetWorldCorners(cs);
            Vector3 bl = drop.InverseTransformPoint(cs[0]), br = drop.InverseTransformPoint(cs[3]);
            RectTransform list = Rect("List", drop);
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(0f, 1f);
            Box(list, 6f, C("#f3e6c6"), PEdge, 1.5f);
            Button keep = list.gameObject.AddComponent<Button>();
            keep.transition = Selectable.Transition.None;
            VBox(list, 2f, TextAnchor.UpperLeft, 6, 6, 6, 6);
            foreach (Post.Peer peer in Post.Peers)
            {
                Post.Peer pr = peer;
                bool can = pr.Mod && pr.Open;
                string why = !pr.Mod ? (Post.Asking ? Lang.T("питаю…") : Lang.T("без RavenWatch — ворон не знайде дороги")) : !pr.Open ? pr.Why : pr.Near ? Lang.T("поруч") : "";
                RectTransform r = Rect("Peer", list);
                HBox(r, 10f, TextAnchor.MiddleLeft, 10, 10, 0, 0);
                Fixed(r.gameObject, -1f, 32f);
                Box(r, 4f, pr.Uid == _mTo && can ? new Color(0.62f, 0.5f, 0.27f, 0.22f) : new Color(1f, 1f, 1f, 0f), new Color(0f, 0f, 0f, 0f), 0f);
                TMP_Text n = T(r, pr.Name, 18f, can ? PInk : PSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
                n.richText = false; n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
                n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                if (why.Length > 0)
                {
                    TMP_Text w = T(r, why, 13f, PSoft, F.Italic, TextAlignmentOptions.MidlineRight);
                    w.textWrappingMode = TextWrappingModes.NoWrap; w.overflowMode = TextOverflowModes.Ellipsis;
                    Fixed(w.gameObject, 220f, -1f);
                }
                if (!can) { r.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f; continue; }
                Clickable(r.gameObject, () => { _mTo = pr.Uid; MailDropClose(); MailTo(); });
            }
            float width = br.x - bl.x;
            list.sizeDelta = new Vector2(width, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            list.sizeDelta = new Vector2(width, LayoutUtility.GetPreferredHeight(list));
            list.anchoredPosition = new Vector2(bl.x, bl.y - 4f);
        }

        private static void MailDropClose()
        {
            if (_mDrop != null) { _mDrop.gameObject.SetActive(false); UnityEngine.Object.Destroy(_mDrop.gameObject); }
            _mDrop = null;
        }

        // every frame while the card is open (RavenBook.Update): the players' answers come in
        private static void MailTick()
        {
            if (!_mailUp || _popup == null) return;
            if (_mToRow != null && (Post.Version != _mListVer || Post.Asking != _mListAsking))
            {
                MailTo();
                if (_mDrop != null) MailDrop();
            }
            if (Time.unscaledTime - _mStandAt >= 1f) { _mStandAt = Time.unscaledTime; MailStanding(); } // (the return countdown ticks)
        }

        // the letter: lined paper with its margin line, a line of words on every rule
        private static void MailText(RectTransform paper, Player p)
        {
            int rows = Post.MaxLines;
            RectTransform box = Rect("Field", paper);
            Fixed(box.gameObject, -1f, rows * MailRuleH + 10f);
            box.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            Image margin = Img(box, "Margin", null, C("#c89069"));
            margin.rectTransform.anchorMin = Vector2.zero; margin.rectTransform.anchorMax = new Vector2(0f, 1f);
            margin.rectTransform.pivot = new Vector2(0f, 0.5f);
            margin.rectTransform.sizeDelta = new Vector2(1.5f, 0f); margin.rectTransform.anchoredPosition = Vector2.zero;

            RectTransform area = Rect("Area", box);
            Stretch(area, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-2f, -2f));
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text t = T(area, "", MailTextSize, PInk, F.Italic, TextAlignmentOptions.TopLeft);
            t.richText = false; // what is typed stays words: no tags of size or colour
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text ph = T(area, Lang.T("Напиши кілька слів…"), MailTextSize, A(PSoft, 0.75f), F.Italic, TextAlignmentOptions.TopLeft);
            Stretch(ph.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // each line as tall as the rules are apart; the rules just under where the words stand
            float ascent = MailTextSize * 0.8f;
            if (t.font != null && t.font.faceInfo.pointSize > 0f)
            {
                float scale = MailTextSize / t.font.faceInfo.pointSize;
                float natural = t.font.faceInfo.lineHeight * scale;
                ascent = t.font.faceInfo.ascentLine * scale;
                t.lineSpacing = ph.lineSpacing = (MailRuleH - natural) / (MailTextSize * 0.01f);
            }
            for (int i = 0; i < rows; i++)
            {
                Image rule = Img(box, "Rule", null, PRule);
                RectTransform rr = rule.rectTransform;
                rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 0.5f);
                rr.offsetMin = new Vector2(30f, 0f); rr.offsetMax = Vector2.zero;
                rr.sizeDelta = new Vector2(rr.sizeDelta.x, 1.2f);
                rr.anchoredPosition = new Vector2(rr.anchoredPosition.x, -(2f + ascent + 6f + i * MailRuleH));
            }

            TMP_InputField f = box.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area; f.textComponent = t; f.placeholder = ph;
            f.targetGraphic = box.GetComponent<Image>();
            f.transition = Selectable.Transition.None;
            f.lineType = TMP_InputField.LineType.MultiLineNewline;
            f.characterLimit = Post.MaxChars;
            f.richText = false;
            f.customCaretColor = true; f.caretColor = PInk; f.caretWidth = 2;
            f.selectionColor = new Color(0.66f, 0.52f, 0.23f, 0.35f);
            Navigation nav = f.navigation; nav.mode = Navigation.Mode.None; f.navigation = nav;
            f.text = _mText;
            f.onValueChanged.AddListener(s =>
            {
                string clean = Post.Clean(s, t.font);
                if (clean != s) { f.SetTextWithoutNotify(clean); f.MoveTextEnd(false); }
                _mText = clean;
                if (_mCount != null) _mCount.text = clean.Length + " / " + Post.MaxChars;
            });
            _mField = f;

            RectTransform under = Rect("Under", paper);
            HBox(under, 8f, TextAnchor.MiddleLeft);
            int have = Post.Letters(p);
            T(under, have > 0 ? Lang.F("Пергаменту в сумці: <b><color=#2a1c10>{0}</color></b>", have) : Lang.F("<color=#8E1E1E>Нема пергаменту</color> — Хальдор продає по {0} монет.", RavenItem.LetterPrice), 15f, PSoft, F.Body);
            Spring(under);
            _mCount = T(under, _mText.Length + " / " + Post.MaxChars, 15f, PSoft, F.Body, TextAlignmentOptions.MidlineRight);
            _mCount.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // the parcel: its cell, what lies there, «Покласти річ» / «Змінити»
        private static void MailParcel(RectTransform paper, Player p)
        {
            ItemDrop.ItemData item = Post.ParcelOf(p);
            RectTransform row = Rect("Parcel", paper);
            HBox(row, 14f, TextAnchor.MiddleLeft, 0, 0, 6, 6);
            RectTransform cell = Rect("Cell", row);
            Fixed(cell.gameObject, 68f, 68f);
            Box(cell, 6f, new Color(1f, 1f, 1f, 0.25f), item != null ? PGoldLine : PEdge, item != null ? 2f : 1.5f);
            Sprite icon = null;
            try { icon = item != null ? BookArt.Smooth(item.GetIcon()) : null; } catch { icon = null; }
            if (icon != null) { Image im = Img(cell, "Icon", icon, Color.white); im.preserveAspect = true; Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f)); }
            else { TMP_Text g = T(cell, "ᛒ", 26f, A(PSoft, 0.5f), F.Rune, TextAlignmentOptions.Center); Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); }
            RectTransform words = Rect("Words", row);
            VBox(words, 2f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(words, item != null ? Post.NameOf(item) : Lang.T("Порожньо"), 21f, item != null ? PInk : PSoft, F.Head);
            T(words, Lang.F("Одна дрібна річ до {0} кг (монети — до {1}). Без зброї, обладунків і руди.", Satiety.Num(Post.MaxWeight), Mathf.FloorToInt(Post.MaxWeight / 0.01f + 0.001f)), 14f, PSoft, F.Italic);
            InkButton(row, item != null ? Lang.T("Змінити") : Lang.T("Покласти річ"), () =>
            {
                string why = PostParcel.Open(Player.m_localPlayer);
                if (why != null) MailError(why);
            });
        }

        private static void MailError(string why)
        {
            if (string.IsNullOrEmpty(why)) return;
            if (_mErr != null) { _mErr.text = why.Replace("\n", " "); _mErrUntil = Time.unscaledTime + 4f; }
            else Tell(why);
        }

        private static float _mErrUntil, _mStandAt;
        private static string _mStandWas;

        // why nothing can be sent right now, before any click: the place, or the chosen raven (away — and when it is
        // back, sleeping, hungry…); null: it can go
        private static string MailStandingWhy()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            if (Trust.Worn(false) == null && Trust.Worn(true) == null) return null; // (the ravens' row says that itself)
            return Post.PlaceWhyNot(p) ?? (_mRaven >= 0 ? Post.RavenWhyNot(p, _mRaven == 1, _mKind) : null);
        }

        // the line under the page says it (a click's own answer stays a few seconds); when it changes — a raven came
        // back, flew off — the ravens' cards and the price are drawn again
        private static void MailStanding()
        {
            if (_mErr == null) return;
            string why = MailStandingWhy();
            if (why != _mStandWas)
            {
                bool redraw = _mStandWas != null || why != null;
                _mStandWas = why;
                if (redraw && _mWho != null)
                {
                    Player p = Player.m_localPlayer;
                    if (p != null) MailPickRaven(p, false);
                    MailRavens();
                    MailPrice();
                    why = _mStandWas = MailStandingWhy(); // (the other raven may have been picked)
                }
            }
            if (Time.unscaledTime >= _mErrUntil) _mErr.text = why != null ? why.Replace("\n", " ") : "";
        }

        private static void MailSend()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (_mRaven < 0) { MailError(Lang.T("Нема ворона, щоб нести.")); return; }
            Post.Peer to = Post.Peers.Find(x => x.Uid == _mTo);
            string text = _mField != null ? _mField.text : _mText;
            string why = Post.Send(p, _mRaven == 1, _mKind, to, text);
            if (why == null)
            {
                if (_mKind == Post.Letter) _mText = "";
                ClosePopup();
                Close(); // to see it fly off
                return;
            }
            MailError(why);
        }
    }

    // Лист ↔ Посилка: the body eases from the old page's height to the new one's (the card follows), the old page
    // fading out first, the new one coming up over its last part; the old one is gone at the end
    internal sealed class MailMorph : MonoBehaviour
    {
        private const float Time_ = 0.26f;
        private LayoutElement _body;
        private RectTransform _old;
        private CanvasGroup _oldG, _newG;
        private float _from, _to, _start, _oldFrom = 1f;

        internal void Begin(LayoutElement body, RectTransform old, RectTransform page, float from, float to)
        {
            if (_old != null) Destroy(_old.gameObject); // (a switch during a switch: the one before is gone at once)
            _body = body; _old = old; _from = from; _to = to; _start = Time.unscaledTime;
            _oldG = old != null ? old.GetComponent<CanvasGroup>() : null;
            _oldFrom = _oldG != null ? _oldG.alpha : 1f; // (switched mid-way: it fades on from where it was, no flash)
            if (_oldG != null) _oldG.blocksRaycasts = false;
            _newG = page.GetComponent<CanvasGroup>();
            if (_newG != null) _newG.alpha = 0f;
            enabled = true;
            Update();
        }

        private void Update()
        {
            if (_body == null) { enabled = false; return; }
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / Time_);
            float e = t * t * (3f - 2f * t);
            _body.minHeight = _body.preferredHeight = Mathf.Lerp(_from, _to, e);
            if (_oldG != null) _oldG.alpha = _oldFrom * (1f - Mathf.Clamp01(t / 0.45f));
            if (_newG != null) _newG.alpha = Mathf.Clamp01((t - 0.35f) / 0.65f);
            if (t < 1f) return;
            if (_old != null) Destroy(_old.gameObject);
            _old = null; _oldG = null;
            enabled = false;
        }
    }

    // typing a letter: the game's keys keep still (the map, the inventory, walking), as in its own text boxes
    [HarmonyPatch(typeof(TextInput), "IsVisible")]
    internal static class PostTypingPatch
    {
        private static void Postfix(ref bool __result) { if (!__result && RavenBook.MailTyping) __result = true; }
    }
}
#endif

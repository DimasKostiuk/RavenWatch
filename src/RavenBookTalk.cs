#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // A person's talk (NpcTalk): a card in the book's style low on the screen — the dark wooden frame, its name on the
    // plaque, what it says on the parchment, your answers under it (a click, or the keys 1–9). Esc, the ✕ or walking
    // off closes it. While it is open the game is held as for the book (the cursor shows, the hero stands).
    internal static partial class RavenBook
    {
        private static GameObject _talkRoot;
        private static Action<int> _talkPick;
        private static Action _talkClose;
        private static int _talkCount;

        internal static bool TalkOpen { get { return _talkRoot != null && _talkRoot.activeSelf; } }

        internal static void ShowTalk(string who, string says, List<string> answers, Action<int> pick, Action close)
        {
            EnsureGameFont();
            if (_talkRoot != null) UnityEngine.Object.Destroy(_talkRoot);
            _talkRoot = OverlayCanvas("RavenWatch_Talk", 30004, true);
            _talkRoot.AddComponent<TalkKeys>();
            _talkPick = pick; _talkClose = close; _talkCount = answers.Count;

            RectTransform frame = Rect("Frame", _talkRoot.transform);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0f);
            frame.pivot = new Vector2(0.5f, 0f);
            frame.anchoredPosition = new Vector2(0f, 60f);
            CardPiece(frame.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, 34, 16);
            ContentSizeFitter fit = frame.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fixed(frame.gameObject, 860f, -1f);
            frame.sizeDelta = new Vector2(860f, frame.sizeDelta.y);

            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 8f, TextAnchor.UpperLeft, 30, 30, 22, 20);

            TMP_Text line = T(paper, says, 22f, PInk, F.Body, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rule = Rect("Rule", paper);
            Fixed(rule.gameObject, -1f, 1.5f);
            rule.gameObject.AddComponent<Image>().color = A(PRule, 0.8f);

            for (int i = 0; i < answers.Count; i++)
            {
                int at = i;
                RectTransform row = Rect("Answer", paper);
                HBox(row, 10f, TextAnchor.MiddleLeft, 8, 8, 4, 4);
                TMP_Text num = T(row, (i + 1) + ".", 18f, PLabel, F.Head, TextAlignmentOptions.MidlineLeft);
                num.textWrappingMode = TextWrappingModes.NoWrap;
                Fixed(num.gameObject, 26f, -1f);
                TMP_Text a = T(row, answers[i], 19f, PInk, F.Body, TextAlignmentOptions.MidlineLeft);
                a.textWrappingMode = TextWrappingModes.Normal;
                a.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Image fill = row.gameObject.AddComponent<Image>();
                fill.color = new Color(1f, 1f, 1f, 0f);
                Clickable(row.gameObject, () => TalkChoose(at));
                Hover(row.gameObject, new Graphic[] { a, num }, PInk, fill);
            }

            Plaque(frame, who, 38f, false);
            RectTransform x = Rect("Close", frame);
            x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            x.anchorMin = x.anchorMax = new Vector2(1f, 1f);
            x.pivot = new Vector2(0.5f, 0.5f);
            x.anchoredPosition = new Vector2(-4f, -4f);
            x.sizeDelta = new Vector2(36f, 36f);
            Image ring = x.gameObject.AddComponent<Image>();
            ring.sprite = BookArt.Picture("card_close");
            if (ring.sprite == null) { ring.sprite = BookArt.Circle; ring.color = C("#2a1b10"); }
            Image cross = Img(x, "Cross", BookArt.Cross, PlaqueInk);
            Stretch(cross.rectTransform, Vector2.zero, Vector2.one, new Vector2(11f, 11f), new Vector2(-11f, -11f));
            Clickable(x.gameObject, TalkClose);
            LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
        }

        // ---------------------------------------------------------------- a person's card («Вдача», NpcProfile): its name on
        // the plaque, rows on the parchment (a gold label, the words beside it), the ✕ or Esc closes it
        private static GameObject _cardRoot;
        internal static bool CardOpen { get { return _cardRoot != null && _cardRoot.activeSelf; } }

        internal static void ShowCard(string title, List<KeyValuePair<string, string>> rows) { ShowCard(title, rows, null); }

        // ...and buttons under the parchment (a heart's «Їдальня»): pressed, the card closes and the button's own act runs
        internal static void ShowCard(string title, List<KeyValuePair<string, string>> rows, List<KeyValuePair<string, Action>> buttons) { ShowCard(title, rows, buttons, null); }

        // ...each button with a few words of what it does (tips, by the same order): shown under the buttons while the
        // pointer is on one
        internal static void ShowCard(string title, List<KeyValuePair<string, string>> rows, List<KeyValuePair<string, Action>> buttons, List<string> tips)
        {
            EnsureGameFont();
            HideCard();
            _cardRoot = OverlayCanvas("RavenWatch_PersonCard", 30004, true);
            _cardRoot.AddComponent<CardKeys>();
            RectTransform frame = Rect("Frame", _cardRoot.transform);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            CardPiece(frame.gameObject.AddComponent<Image>(), "card_frame", 32f, 12f, C("#2a1b10"));
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, 34, 16);
            ContentSizeFitter fit = frame.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fixed(frame.gameObject, 640f, -1f);
            frame.sizeDelta = new Vector2(640f, frame.sizeDelta.y);
            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 10f, TextAnchor.UpperLeft, 28, 28, 22, 22);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) { RectTransform rule = Rect("Rule", paper); Fixed(rule.gameObject, -1f, 1f); rule.gameObject.AddComponent<Image>().color = A(PRule, 0.6f); }
                RectTransform row = Rect("Row", paper);
                HBox(row, 14f, TextAnchor.UpperLeft, 0, 0, 0, 0);
                TMP_Text l = T(row, rows[i].Key, 15f, PLabel, F.Head, TextAlignmentOptions.TopLeft);
                l.textWrappingMode = TextWrappingModes.NoWrap;
                Fixed(l.gameObject, 120f, -1f);
                TMP_Text v = T(row, rows[i].Value, 18f, PInk, F.Body, TextAlignmentOptions.TopLeft);
                v.textWrappingMode = TextWrappingModes.Normal;
                v.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
            if (buttons != null && buttons.Count > 0)
            {
                RectTransform foot = Rect("Foot", frame);
                HBox(foot, 12f, TextAnchor.MiddleCenter, 8, 8, 10, 2);
                TMP_Text hint = null;
                if (tips != null && tips.Count > 0)
                {
                    hint = T(frame, "", 14f, PLabel, F.Body, TextAlignmentOptions.Top);
                    hint.fontStyle = FontStyles.Italic;
                    hint.textWrappingMode = TextWrappingModes.Normal;
                    LayoutElement he = hint.gameObject.AddComponent<LayoutElement>(); he.minHeight = 40f; he.preferredWidth = 560f; // (room for two lines kept: the card does not jump)
                }
                for (int i = 0; i < buttons.Count; i++)
                {
                    Action act = buttons[i].Value;
                    RectTransform btn = CardButton(foot, buttons[i].Key, false, () => { HideCard(); act(); });
                    string tip = tips != null && i < tips.Count ? tips[i] : null;
                    if (hint != null && btn != null && !string.IsNullOrEmpty(tip)) { CardTip h = btn.gameObject.AddComponent<CardTip>(); h.Line = hint; h.Text = Grammar.Fix(tip); }
                }
            }
            Plaque(frame, title, 38f, false);
            RectTransform x = Rect("Close", frame);
            x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            x.anchorMin = x.anchorMax = new Vector2(1f, 1f);
            x.pivot = new Vector2(0.5f, 0.5f);
            x.anchoredPosition = new Vector2(-4f, -4f);
            x.sizeDelta = new Vector2(36f, 36f);
            Image ring = x.gameObject.AddComponent<Image>();
            ring.sprite = BookArt.Picture("card_close");
            if (ring.sprite == null) { ring.sprite = BookArt.Circle; ring.color = C("#2a1b10"); }
            Image cross = Img(x, "Cross", BookArt.Cross, PlaqueInk);
            Stretch(cross.rectTransform, Vector2.zero, Vector2.one, new Vector2(11f, 11f), new Vector2(-11f, -11f));
            Clickable(x.gameObject, HideCard);
            LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
        }

        internal static void HideCard() { if (_cardRoot != null) UnityEngine.Object.Destroy(_cardRoot); _cardRoot = null; }

        // a card button's few words, in the line under the buttons while the pointer is on it
        internal sealed class CardTip : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal TMP_Text Line; internal string Text;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Line != null) Line.text = Text; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Line != null && Line.text == Text) Line.text = ""; }
        }

        internal sealed class CardKeys : MonoBehaviour
        {
            private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) HideCard(); }
        }

        internal static void HideTalk()
        {
            if (_talkRoot != null) UnityEngine.Object.Destroy(_talkRoot);
            _talkRoot = null; _talkPick = null; _talkClose = null;
        }

        private static void TalkChoose(int i) { Action<int> p = _talkPick; if (p != null) p(i); }

        private static void TalkClose() { Action c = _talkClose; if (c != null) c(); else HideTalk(); }

        // the keys while it is open: 1–9 an answer, Esc closes; and it closes when you walk off (NpcTalk.Tick)
        internal sealed class TalkKeys : MonoBehaviour
        {
            private void Update()
            {
                NpcTalk.Tick();
                if (_talkRoot == null) return;
                if (Input.GetKeyDown(KeyCode.Escape)) { TalkClose(); return; }
                for (int i = 0; i < Mathf.Min(9, _talkCount); i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { TalkChoose(i); return; }
            }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Поради Провідника»: a round button with the Guide's mark on the book's upper corner, beside the post's envelope —
    // only once the Guide has told something. A click opens a card with the list of topics he has told or brought (the
    // ones let fly off count too), in his order: each a row lit gold under the pointer; a click closes the book and he
    // flies in to tell it again (Guide.Replay).
    internal static partial class RavenBook
    {
        private static RectTransform _guideCorner;
        private const float GuideCardW = 560f, GuideViewMax = 440f;

        private static void BuildGuideCorner()
        {
            _guideCorner = CornerButton("Guide", 100f, AbilityIcons.Get("guide"));
            Clickable(_guideCorner.GetChild(1).gameObject, GuidePopup);
            Tip(_guideCorner.GetChild(1).gameObject, () => "<b>" + Lang.T("Поради Провідника") + "</b>\n" + Lang.T("Теми, про які Провідник уже розповідав: обери — і він прилетить розповісти знову."));
            _guideCorner.SetAsLastSibling();
        }

        // shown once there is something to read again (on opening the book)
        private static void GuideCorner()
        {
            if (_guideCorner != null) _guideCorner.gameObject.SetActive(Guide.Told(Player.m_localPlayer).Count > 0);
        }

        private static void GuidePopup()
        {
            Player p = Player.m_localPlayer;
            ClosePopup();
            HideTip();
            if (_root == null || p == null) return;
            List<KeyValuePair<int, string>> told = Guide.Told(p);
            if (told.Count == 0) return;

            Card card = OpenCard("GuideTips", Lang.T("Поради Провідника"), GuideCardW, false); // a click beside the card closes it
            TMP_Text note = T(card.Paper, Lang.T("Натисни на тему — Провідник прилетить і розповість її знову."), 15f, PSoft, F.Italic);
            RectTransform view = Rect("View", card.Paper);
            Fixed(view.gameObject, -1f, GuideViewMax);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RectTransform list = Rect("List", view);
            list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            VBox(list, 6f, TextAnchor.UpperLeft, 0, 10);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = list; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 120f;
            ScrollArrow(view, sr, 26f);
            foreach (KeyValuePair<int, string> t in told) GuideRow(list, p, t.Key, t.Value, note);

            // the card first takes its width, then the list its height (the words wrap only once the width is known)
            PlaceCard(card, null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Fixed(view.gameObject, -1f, Mathf.Min(GuideViewMax, LayoutUtility.GetPreferredHeight(list)));
            PlaceCard(card, null);
        }

        // one topic: its title in gold ink, «послухати» on the right; under the pointer the words go brighter gold on a
        // faint gold fill. A click: the book closes and the Guide comes; can he not come now, the card says why.
        private static void GuideRow(RectTransform list, Player p, int id, string title, TMP_Text note)
        {
            RectTransform row = Rect("Topic", list);
            HBox(row, 10f, TextAnchor.MiddleLeft, 14, 14, 0, 0);
            Fixed(row.gameObject, -1f, 40f);
            Box(row, 5f, new Color(1f, 1f, 1f, 0.18f), A(PRule, 0.9f), 1.2f);
            TMP_Text mark = T(row, "◆", 12f, PLabel, F.Body, TextAlignmentOptions.Midline);
            mark.raycastTarget = false;
            TMP_Text name = T(row, title, 18f, GoldInk, F.Head, TextAlignmentOptions.MidlineLeft);
            name.raycastTarget = false;
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text hint = T(row, Lang.T("послухати"), 14f, PLabel, F.Italic, TextAlignmentOptions.MidlineRight);
            hint.raycastTarget = false; hint.textWrappingMode = TextWrappingModes.NoWrap;
            Clickable(row.gameObject, () =>
            {
                string why = Guide.Replay(p, id);
                if (why == null) { Close(); p.Message(MessageHud.MessageType.Center, Lang.T("Провідник летить до тебе.")); return; }
                note.text = why; note.color = Bad;
            });
            Hover(row.gameObject, new Graphic[] { name }, GoldInk, row.GetComponent<Image>());
            Hover(hint.gameObject, new Graphic[] { hint, mark }, PLabel, null, row.gameObject);
            Transform line = row.Find("Line");
            if (line != null) Hover(line.gameObject, new Graphic[] { line.GetComponent<Image>() }, A(PRule, 0.9f), null, row.gameObject);
        }
    }
}
#endif

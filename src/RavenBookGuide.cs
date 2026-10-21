#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Поради Провідника»: a round button with the Guide's mark on the book's upper corner, beside the post's envelope —
    // only once the Guide has told something. A click opens a card with every topic he has told, in his order, to read
    // again (the talk itself comes once).
    internal static partial class RavenBook
    {
        private static RectTransform _guideCorner;
        private const float GuideCardW = 560f, GuideViewMax = 440f;

        private static void BuildGuideCorner()
        {
            _guideCorner = CornerButton("Guide", 100f, AbilityIcons.Get("guide"));
            Clickable(_guideCorner.GetChild(1).gameObject, GuidePopup);
            Tip(_guideCorner.GetChild(1).gameObject, () => "<b>" + Lang.T("Поради Провідника") + "</b>\n" + Lang.T("Усе, що Провідник уже розповів: перечитати."));
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
            List<KeyValuePair<string, string>> told = Guide.Told(p);
            if (told.Count == 0) return;

            Card card = OpenCard("GuideTips", Lang.T("Поради Провідника"), GuideCardW, false); // a click beside the card closes it
            RectTransform view = Rect("View", card.Paper);
            Fixed(view.gameObject, -1f, GuideViewMax);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RectTransform list = Rect("List", view);
            list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            VBox(list, 14f, TextAnchor.UpperLeft, 0, 10);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = list; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 120f;
            foreach (KeyValuePair<string, string> t in told)
            {
                RectTransform one = Rect("Topic", list);
                VBox(one, 4f, TextAnchor.UpperLeft);
                T(one, t.Key, 19f, PInk, F.Head);
                T(one, t.Value, 15f, PSoft, F.Body);
            }

            // the card first takes its width, then the list its height (the words wrap only once the width is known)
            PlaceCard(card, null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Fixed(view.gameObject, -1f, Mathf.Min(GuideViewMax, LayoutUtility.GetPreferredHeight(list)));
            PlaceCard(card, null);
        }
    }
}
#endif

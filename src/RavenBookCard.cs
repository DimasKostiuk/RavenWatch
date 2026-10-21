#if RAVEN_ITEM
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The book's small windows («картки»), after the user's design of the post (Downloads\mail-1a.png): a dark wooden
    // frame, its title on a plaque across the frame's top edge (ᚱ either side), a round gold close button on its corner,
    // the parchment inside with its double line (what the window says, in ink), and — when it asks for a deed — a dark
    // strip under the parchment: a few words on the left, the buttons on the right («Скасувати» outlined, the deed a gold
    // pill with a wax seal). The pieces are cut by tools/card_art.html into assets/book/card_*.png (drawn at 2×).
    internal static partial class RavenBook
    {
        private sealed class Card
        {
            public RectTransform Layer, Frame, Paper, Foot, FootWords;
            public float Width; // (no shadow box under it: the user, 2026-10-18 — the frame has its own dark edge)
        }

        // the parchment's inks (the design's): words, softer words, the rows' names, the rules, its card edges
        private static readonly Color PInk = C("#2a1c10"), PSoft = C("#6b5636"), PLabel = C("#a07f45"), PRule = C("#bba67e"),
            PEdge = C("#ac9672"), PDark = C("#2a1c10"), PGoldLine = C("#9f7f45"), PCream = C("#f5e8c8"),
            FootInk = C("#ecd9b0"), FootSoft = C("#b49a6a"), PlaqueInk = C("#e3c27c");
        // the satiety zones' colours on parchment (Satiety.ZoneHex is for the dark tooltips)
        private static readonly string[] ZonePaperHex = { "#8E1E1E", "#9a4a16", "#7a5c08", "#2f5a2a", "#6A3F9E" };
        private const string PGoodHex = "#2f5a2a";

        // a sliced piece of the card (or, without its picture, a plain rounded fill)
        private static void CardPiece(Image im, string pic, float border, float fallbackRadius, Color fallback)
        {
            Sprite s = BookArt.Sliced(pic, border);
            if (s != null) { im.sprite = s; im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 2f; im.color = Color.white; }
            else { im.sprite = BookArt.RoundFine(fallbackRadius); im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = BookArt.Fine; im.color = fallback; }
        }

        // A card over the book (the popup: a click beside it closes it). title null: no plaque; foot: the dark strip for
        // buttons; big: the post's size of plaque and close button. Fill c.Paper (a column) and c.Foot, then PlaceCard.
        private static Card OpenCard(string name, string title, float width, bool foot, bool big = false)
        {
            RectTransform layer = Rect(name, _over);
            Stretch(layer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            layer.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            layer.gameObject.AddComponent<Button>().onClick.AddListener(ClosePopup);
            _popup = layer.gameObject;
            Card c = new Card { Layer = layer, Width = width };

            RectTransform frame = Rect("Frame", layer);
            c.Frame = frame;
            Image fi = frame.gameObject.AddComponent<Image>();
            CardPiece(fi, "card_frame", 32f, 12f, C("#2a1b10"));
            Button keep = frame.gameObject.AddComponent<Button>(); // a click on the card itself does not close it
            keep.transition = Selectable.Transition.None;
            float plaqueH = big ? 50f : 38f;
            VBox(frame, 0f, TextAnchor.UpperCenter, 16, 16, Mathf.RoundToInt(title != null ? plaqueH * 0.5f + (big ? 16f : 12f) : 16f), foot ? 0 : 16);

            RectTransform paper = Rect("Paper", frame);
            CardPiece(paper.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(paper, 10f, TextAnchor.UpperLeft, big ? 30 : 24, big ? 30 : 24, big ? 26 : 20, big ? 22 : 18);
            c.Paper = paper;

            if (foot)
            {
                RectTransform f = Rect("Foot", frame);
                HBox(f, 12f, TextAnchor.MiddleRight, 6, 0, big ? 16 : 12, big ? 18 : 14);
                RectTransform words = Rect("Words", f);
                VBox(words, 0f, TextAnchor.MiddleLeft);
                words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                c.Foot = f; c.FootWords = words;
            }

            if (title != null) Plaque(frame, title, plaqueH, big);
            CloseRound(frame, big ? 46f : 36f);
            return c;
        }

        // the title's plaque, its middle on the frame's top edge
        private static void Plaque(RectTransform frame, string title, float h, bool big)
        {
            RectTransform pl = Rect("Plaque", frame);
            pl.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pl.anchorMin = pl.anchorMax = new Vector2(0.5f, 1f);
            pl.pivot = new Vector2(0.5f, 0.5f);
            pl.anchoredPosition = new Vector2(0f, -2f);
            Image im = pl.gameObject.AddComponent<Image>();
            CardPiece(im, "card_plaque", 18f, 4f, C("#281a0f")); // (18: two borders still fit the small plaque's 38)
            im.raycastTarget = false;
            HBox(pl, big ? 16f : 10f, TextAnchor.MiddleCenter, big ? 30 : 20, big ? 30 : 20, 0, 4);
            float size = big ? 30f : 18f;
            T(pl, "ᚱ", size * 0.5f, A(PlaqueInk, 0.7f), F.Rune, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text t = T(pl, title.ToUpperInvariant(), size, PlaqueInk, F.Head, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.characterSpacing = big ? 14f : 8f;
            T(pl, "ᚱ", size * 0.5f, A(PlaqueInk, 0.7f), F.Rune, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            LayoutRebuilder.ForceRebuildLayoutImmediate(pl);
            pl.sizeDelta = new Vector2(Mathf.Max(big ? 260f : 140f, LayoutUtility.GetPreferredWidth(pl)), h);
        }

        // the round gold close button on the frame's top right corner
        private static void CloseRound(RectTransform frame, float size)
        {
            RectTransform x = Rect("Close", frame);
            x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
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
            Clickable(x.gameObject, ClosePopup);
        }

        // a button on the card's dark strip: main — a gold pill with a wax seal (its rune), else outlined
        private static RectTransform CardButton(Transform row, string caption, bool main, Action click, string rune = "ᚱ", bool enabled = true)
        {
            RectTransform r = Rect("Button", row);
            if (main)
            {
                HBox(r, 10f, TextAnchor.MiddleLeft, 4, 22, 0, 0);
                Fixed(r.gameObject, -1f, 46f);
                CardPiece(r.gameObject.AddComponent<Image>(), "card_gold", 23f, 20f, Gold);
                RectTransform seal = Rect("Seal", r);
                Fixed(seal.gameObject, 38f, 38f);
                Image si = seal.gameObject.AddComponent<Image>();
                si.sprite = BookArt.Picture("card_seal");
                if (si.sprite == null) { si.sprite = BookArt.Circle; si.color = C("#a8302a"); }
                si.raycastTarget = false;
                TMP_Text g = T(seal, rune, 17f, C("#f6d9b8"), F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                T(r, caption, 19f, PInk, F.Body, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            }
            else
            {
                HBox(r, 0f, TextAnchor.MiddleCenter, 20, 20, 0, 0);
                Fixed(r.gameObject, -1f, 42f);
                Box(r, 6f, new Color(0f, 0f, 0f, 0f), C("#6e5a38"), 1.5f);
                T(r, caption, 18f, FootInk, F.Body, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            }
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.interactable = enabled;
            if (!enabled) r.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
            b.onClick.AddListener(() => click());
            return r;
        }

        // a small outlined button on the parchment itself (in ink)
        private static RectTransform InkButton(Transform row, string caption, Action click)
        {
            RectTransform r = Rect("Button", row);
            HBox(r, 0f, TextAnchor.MiddleCenter, 16, 16, 0, 0);
            Fixed(r.gameObject, -1f, 34f);
            Box(r, 5f, new Color(0f, 0f, 0f, 0f), C("#8a7350"), 1.5f);
            TMP_Text t = T(r, caption, 16f, PInk, F.Body, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.onClick.AddListener(() => click());
            Transform line = r.Find("Line");
            Hover(r.gameObject, new Graphic[] { t }, PInk, r.GetComponent<Image>()); // gold words, a faint gold fill
            if (line != null) Hover(line.gameObject, new Graphic[] { line.GetComponent<Image>() }, C("#8a7350"), null, r.gameObject);
            return r;
        }

        // the gold the parchment's links and ink buttons take under the pointer
        private static readonly Color PHover = C("#a87d2c");

        // under the pointer these turn gold (and the fill, if any, a faint gold); from: whose pointer counts (default: g)
        private static void Hover(GameObject g, Graphic[] targets, Color normal, Image fill, GameObject from = null)
        {
            InkHover h = g.AddComponent<InkHover>();
            h.Targets = targets; h.Normal = normal; h.HoverColor = PHover;
            h.Fill = fill;
            if (fill != null) { h.FillNormal = fill.color; h.FillHover = A(PHover, 0.12f); }
            if (from != null && from != g) h.Source = from.GetComponent<InkHover>();
        }

        // a choice on the parchment (a gift, a rune): a light card, outlined; here — dark with the gold line
        private static RectTransform PaperChoice(Transform parent, bool here, bool can)
        {
            RectTransform r = Rect("Choice", parent);
            Box(r, 5f, here ? PDark : new Color(1f, 1f, 1f, can ? 0.22f : 0.08f), here ? PGoldLine : A(PEdge, can ? 1f : 0.5f), here ? 1.5f : 1.2f);
            return r;
        }

        // the card's size from what is on it, then its place: by null — the middle of the book; else over the clicked
        // thing (no room above: under it), or (left) to its left; keep — where it stood (drawn again). Returns that place.
        private static Vector2 PlaceCard(Card c, RectTransform by, bool left = false, Vector2? keep = null)
        {
            RectTransform f = c.Frame;
            f.anchorMin = f.anchorMax = new Vector2(0.5f, 0f); // the layer's units: (0, 0) the book's lower middle
            f.pivot = new Vector2(0.5f, 0f);
            f.sizeDelta = new Vector2(c.Width, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(f);
            float w = c.Width, h = LayoutUtility.GetPreferredHeight(f);
            f.sizeDelta = new Vector2(w, h);
            const float over = 26f; // the plaque and the close button stand out over the top edge
            Vector2 at;
            if (keep.HasValue) at = keep.Value;
            else if (by == null) at = new Vector2(0f, BookH / 2f - (h + over) / 2f); // (the book from 0 up to BookH)
            else
            {
                Vector3[] cs = new Vector3[4];
                by.GetWorldCorners(cs);
                Vector3 bl = _over.InverseTransformPoint(cs[0]), tr = _over.InverseTransformPoint(cs[2]);
                if (left) at = new Vector2(bl.x - 14f - w / 2f, tr.y - h);
                else
                {
                    at = new Vector2((bl.x + tr.x) / 2f, tr.y + 12f);
                    if (at.y + h + over > BookTop + BookH - 8f) at.y = bl.y - 12f - h - over;
                }
            }
            at.x = Mathf.Clamp(at.x, -BookW / 2f + 8f + w / 2f, BookW / 2f - 8f - w / 2f);
            at.y = Mathf.Max(8f, Mathf.Min(at.y, BookTop + BookH - 8f - h - over));
            f.anchoredPosition = at;
            c.Layer.SetAsLastSibling();
            return at;
        }
    }

    // a link or an ink button on the parchment: its ink eases to gold under the pointer (Source: follow another's
    // pointer, e.g. the button's frame line follows the button)
    internal sealed class InkHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        internal Graphic[] Targets;
        internal Color Normal, HoverColor, FillNormal, FillHover;
        internal Image Fill;
        internal InkHover Source;
        internal bool Over;
        private float _k = -1f;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { Over = true; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { Over = false; }
        private void OnDisable() { Over = false; _k = 0f; Apply(); }

        private void Update()
        {
            bool over = Source != null ? Source.Over : Over;
            float to = over ? 1f : 0f;
            if (_k == to) return;
            _k = _k < 0f ? to : Mathf.MoveTowards(_k, to, Time.unscaledDeltaTime / 0.12f);
            Apply();
        }

        private void Apply()
        {
            Color c = Color.Lerp(Normal, HoverColor, _k);
            if (Targets != null) foreach (Graphic g in Targets) if (g != null) g.color = c;
            if (Fill != null) Fill.color = Color.Lerp(FillNormal, FillHover, _k);
        }
    }
}
#endif

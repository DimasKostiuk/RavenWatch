#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // How it is with you, on the talk scroll's margins (the user's design 2026-11-08, Downloads\suviy-stosunky.html — variant 5a «Тонкі лінії на полях»: «без кольорових плям і
    // медальйонів: лише те саме коричневе чорнило, що й текст»). In place of the old patience strip:
    //   left margin    a hair line «зараз · <word>»: how it feels in itself (NpcPatience: its state, the same for everyone) — the ink's own brown
    //   right margin   a hair line «до тебе · <word>»: its height its patience to you (what opens its door: NpcPatience.ToYou), its word how it feels about you
    //                  (NpcAttitude level), four ticks for its five steps — the only colour on the page, and muted
    //   the rope       down the left edge (talk_cord), a knot on it for each thread between you (NpcThreads: talk_knot1/2, turned in turn), its word beside it, a card on
    //                  the pointer: what tied it and what it changes in your talks; its end above the paper's torn edge — tied neatly (talk_end), or frayed when
    //                  a thread was cut (talk_frayed), their words beside it
    //   why            both lines show the reasons box beside the scroll while pointed at (the same box the strip had: NpcPatience.Tip)
    // The pictures: tools/talk_knot_art.html (that page's own CSS, at 2×). A talk with no person's feeling (a story person's own) has none of it.
    internal static partial class RavenBook
    {
        internal sealed class TalkKnot { public string Word = "", Deed = "", Does = ""; public bool Cut; }
        internal sealed class TalkRel { public float Now = 50f, ToYou = 50f; public string NowWord = "", YouWord = "", Tip; public readonly List<TalkKnot> Knots = new List<TalkKnot>(); }

        private static readonly Color RInk = C("#3a2414"), RLine = new Color(58f / 255f, 36f / 255f, 20f / 255f, 0.18f), RNow = C("#5a4a38"), RYou = C("#8a3a1e"),
            RTick = new Color(58f / 255f, 36f / 255f, 20f / 255f, 0.5f), RWord = C("#6b5436"), RWordCut = C("#9a8666"), RCard = C("#f4e9cc");
        private const float RTop = 60f, RLabel = 130f, KnotStep = 27f, KnotLow = 74f, EndAt = 58f; // (EndAt: the rope's end — the paper's torn edge bites up to ~35 by the rope, its tassel must not hang in the air: the user 2026-11-08, «нитка не має кінця»)
        private const string RelName = "Rel"; // (the margins' own: cleared between turns, TalkFlow.Clear)
        private const float WhyW = 360f; // (the reasons box's width)
        private const int WhyPad = 26;  // (its words' distance from its edge: clear of the card's double line, ~9 in — the user 2026-11-08, «підказки не мають відступів»)

        // the paper's margins with nothing of a person's feeling on them (the scroll's own: ScrollShell), and with it
        private static RectOffset PaperPlain() { return new RectOffset(28, 60, 66, 76); }
        private static RectOffset PaperRel(float bottom) { return new RectOffset(58, 52, 66, Mathf.RoundToInt(bottom)); }

        // the paper's margins for it (before it is measured): room for the rope and the lines, the knots' height at the bottom
        private static void RelPaper(TalkRel r)
        {
            if (_talkPaper == null) return;
            VerticalLayoutGroup v = _talkPaper.GetComponent<VerticalLayoutGroup>();
            LayoutElement le = _talkPaper.GetComponent<LayoutElement>();
            if (r == null) { if (v != null) v.padding = PaperPlain(); if (le != null) le.minHeight = TalkPaperMin; return; }
            float bottom = RelBottom(r);
            if (v != null) v.padding = PaperRel(bottom);
            if (le != null) le.minHeight = RelHeight(r);
        }

        // how tall the margins are: the paper's least, or more for many knots (a line to see between the plaque and its word, whatever the knots). Never the paper's own height:
        // more words make the paper taller, the lines and the rope stay this long, from its top (the user 2026-11-08)
        private static float RelHeight(TalkRel r) { return Mathf.Max(TalkPaperMin, RelBottom(r) + RLabel + RTop + 90f); }

        private static bool AnyCut(TalkRel r) { foreach (TalkKnot k in r.Knots) if (k.Cut) return true; return false; }
        private static int Tied(TalkRel r) { int n = 0; foreach (TalkKnot k in r.Knots) if (!k.Cut) n++; return n; }

        // the paper's own bottom margin: the knots' column, at least the plain one
        private static float RelBottom(TalkRel r)
        {
            int n = Tied(r);
            float top = n > 0 ? KnotLow + KnotStep * (n - 1) + 19f : EndAt;
            return Mathf.Max(PaperPlain().bottom, top + 16f);
        }

        // drawn when the turn is laid out
        private static void RelMargins(RectTransform paper, TalkRel r, float boxLeft)
        {
            RectTransform m = Rect(RelName, paper);
            m.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(m, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -RelHeight(r)), Vector2.zero); // (hung from the paper's top, its own height: a taller paper goes on under it)
            float bottom = RelBottom(r), lineBottom = bottom + RLabel;
            GameObject why = WhyBox(r.Tip, boxLeft);

            // the rope down the left edge, its end above the torn edge: tied neatly, frayed when a thread was cut
            bool cut = AnyCut(r);
            Sprite cord = BookArt.Picture("talk_cord");
            RectTransform rope = Rect("Cord", m); rope.anchorMin = new Vector2(0f, 0f); rope.anchorMax = new Vector2(0f, 1f); rope.pivot = new Vector2(0f, 0.5f);
            rope.offsetMin = new Vector2(15f, EndAt - 1f); rope.offsetMax = new Vector2(26f, 0f);
            Image ri = rope.gameObject.AddComponent<Image>(); ri.raycastTarget = false;
            if (cord != null) { ri.sprite = cord; ri.type = Image.Type.Tiled; ri.pixelsPerUnitMultiplier = 2f; } else ri.color = C("#a8864c");
            Art(m, cut ? "talk_frayed" : "talk_end", new Vector2(6f, EndAt - 30f), new Vector2(30f, 30f)); // (its band on the rope's last pixel)

            // the two lines and their words
            float px = 1f / Mathf.Max(0.01f, m.lossyScale.y); // (one screen pixel in the paper's units: a 720p screen and a long talk's shrink make a unit less than a pixel)
            Line(m, false, 40f, RTop, lineBottom, r.Now / 100f, RNow, false, why, px);
            Line(m, true, 24f, RTop, lineBottom, r.ToYou / 100f, RYou, true, why, px);
            Word(m, false, 33f + 14f, lineBottom - RLabel + 20f, Lang.F("зараз · {0}", r.NowWord));
            Word(m, true, 17f, lineBottom - RLabel + 20f, Lang.T("до тебе") + " · <color=#8a3a1e>" + r.YouWord + "</color>");

            // the knots: the first one highest, the last one lowest, just above the frayed end
            int tied = Tied(r), i = 0;
            foreach (TalkKnot k in r.Knots)
            {
                if (k.Cut) continue;
                float at = KnotLow + KnotStep * (tied - 1 - i);
                RectTransform knot = Art(m, (i % 2 == 0) ? "talk_knot1" : "talk_knot2", new Vector2(6f, at - 4f), new Vector2(30f, 26f));
                TMP_Text w = Label(m, k.Word, new Vector2(34f, at + 3f), RWord);
                KnotCard(m, knot, w, Said(k), k.Does, new Vector2(36f, at - 1f));
                i++;
            }
            if (cut)
            {
                List<string> words = new List<string>(); System.Text.StringBuilder deeds = new System.Text.StringBuilder();
                foreach (TalkKnot k in r.Knots) { if (!k.Cut) continue; words.Add(k.Word); if (deeds.Length > 0) deeds.Append('\n'); deeds.Append(Said(k)); if (k.Does.Length > 0) deeds.Append(". ").Append(k.Does); }
                TMP_Text w = Label(m, string.Join(" · ", words.ToArray()), new Vector2(34f, EndAt - 12f), RWordCut);
                KnotCard(m, null, w, Lang.T("обірвано"), deeds.ToString(), new Vector2(36f, EndAt - 16f));
            }
        }

        // a knot's own words: its word and what tied or cut it (tied from the console: no deed — the word alone)
        private static string Said(TalkKnot k) { return k.Deed.Length > 0 ? k.Word + " — " + k.Deed : k.Word; }

        // a hair line on a margin, filled from the bottom (right: its distance from the right edge), the reasons box while pointed at
        private static void Line(RectTransform m, bool right, float edge, float top, float bottom, float k, Color fill, bool steps, GameObject why, float px)
        {
            k = Mathf.Clamp01(k);
            RectTransform hit = Rect(right ? "You" : "Now", m);
            hit.anchorMin = new Vector2(right ? 1f : 0f, 0f); hit.anchorMax = new Vector2(right ? 1f : 0f, 1f); hit.pivot = new Vector2(0.5f, 0.5f);
            float x = right ? -edge - 1f : edge + 1f;
            hit.offsetMin = new Vector2(x - 8f, bottom); hit.offsetMax = new Vector2(x + 8f, -top);
            hit.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // (a finger's width takes the pointer, the line is a hair)
            RectTransform back = Rect("Back", hit); float half = Mathf.Max(1f, px * 0.5f); Stretch(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-half, 0f), new Vector2(half, 0f)); // (a hair, never thinner than a pixel)
            Image bi = back.gameObject.AddComponent<Image>(); bi.color = RLine; bi.raycastTarget = false;
            RectTransform f = Rect("Fill", back); Stretch(f, Vector2.zero, new Vector2(1f, k), Vector2.zero, Vector2.zero);
            Image fi = f.gameObject.AddComponent<Image>(); fi.color = fill; fi.raycastTarget = false;
            if (steps) for (int s = 1; s <= 4; s++) Tick(back, s * 0.2f, 6f, Mathf.Max(1f, px), RTick);
            Tick(back, k, 8f, Mathf.Max(1.5f, px), RInk);
            if (why != null) hit.gameObject.AddComponent<ShowWhilePointed>().Box = why;
        }

        private static void Tick(RectTransform line, float k, float w, float h, Color c)
        {
            RectTransform t = Rect("Tick", line); t.anchorMin = t.anchorMax = new Vector2(0.5f, k); t.pivot = new Vector2(0.5f, 0.5f); t.sizeDelta = new Vector2(w, h);
            Image i = t.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        }

        // a margin's word, written up the line (as the page's writing-mode: read from the bottom)
        private static void Word(RectTransform m, bool right, float x, float y, string text)
        {
            RectTransform w = Rect("Word", m);
            w.anchorMin = w.anchorMax = new Vector2(right ? 1f : 0f, 0f); w.pivot = new Vector2(0f, 0f);
            w.sizeDelta = new Vector2(RLabel - 10f, 16f); w.anchoredPosition = new Vector2(right ? -x : x, y); w.localRotation = Quaternion.Euler(0f, 0f, 90f);
            TMP_Text t = T(w, text, 14f, RWord, F.Italic, TextAlignmentOptions.MidlineLeft); Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static RectTransform Art(RectTransform m, string pic, Vector2 at, Vector2 size)
        {
            RectTransform a = Rect("Art", m); a.anchorMin = a.anchorMax = a.pivot = Vector2.zero; a.sizeDelta = size; a.anchoredPosition = at;
            Image i = a.gameObject.AddComponent<Image>(); i.sprite = BookArt.Picture(pic); i.preserveAspect = true; i.raycastTarget = false; // (a knot with a card takes the pointer: KnotCard)
            if (i.sprite == null) { i.sprite = BookArt.Circle; i.color = C("#9a7842"); }
            return a;
        }

        private static TMP_Text Label(RectTransform m, string text, Vector2 at, Color c)
        {
            RectTransform l = Rect("Label", m); l.anchorMin = l.anchorMax = l.pivot = Vector2.zero; l.sizeDelta = new Vector2(150f, 16f); l.anchoredPosition = at;
            TMP_Text t = T(l, text, 13f, c, F.Italic, TextAlignmentOptions.MidlineLeft); Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // the knot's card while pointed at: its deed in italic, what it changes under it — the page's own small card with its arrow
        private static void KnotCard(RectTransform m, RectTransform knot, TMP_Text label, string head, string does, Vector2 at)
        {
            RectTransform c = Rect("Card", m); c.anchorMin = c.anchorMax = c.pivot = Vector2.zero; c.anchoredPosition = at;
            Image ci = c.gameObject.AddComponent<Image>(); ci.sprite = BookArt.RoundFine(2f); ci.type = Image.Type.Sliced; ci.color = RCard; ci.raycastTarget = false;
            Outline o = c.gameObject.AddComponent<Outline>(); o.effectColor = new Color(110f / 255f, 70f / 255f, 30f / 255f, 0.35f); o.effectDistance = new Vector2(1f, -1f);
            UnityEngine.UI.Shadow sh = c.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(70f / 255f, 40f / 255f, 15f / 255f, 0.22f); sh.effectDistance = new Vector2(0f, -2f);
            VBox(c, 2f, TextAnchor.UpperLeft, 13, 13, 8, 9); // (its words clear of its edge, as the reasons box)
            ContentSizeFitter fit = c.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text h = T(c, head, 13f, RInk, F.Italic, TextAlignmentOptions.TopLeft); h.textWrappingMode = TextWrappingModes.NoWrap; h.raycastTarget = false;
            if (!string.IsNullOrEmpty(does)) { TMP_Text d = T(c, does, 12f, RWord, F.Body, TextAlignmentOptions.TopLeft); d.textWrappingMode = TextWrappingModes.NoWrap; d.raycastTarget = false; }
            RectTransform arrow = Rect("Arrow", c); arrow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            arrow.anchorMin = arrow.anchorMax = new Vector2(0f, 1f); arrow.sizeDelta = new Vector2(8f, 8f); arrow.anchoredPosition = new Vector2(-1f, -15f); arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image ai = arrow.gameObject.AddComponent<Image>(); ai.color = RCard; ai.raycastTarget = false;
            arrow.SetAsFirstSibling(); // (under the card's own words)
            c.gameObject.SetActive(false);
            foreach (Graphic g in new Graphic[] { knot != null ? knot.GetComponent<Image>() : null, label })
            {
                if (g == null) continue;
                g.raycastTarget = true;
                g.gameObject.AddComponent<ShowWhilePointed>().Box = c.gameObject;
            }
        }

        // the reasons box beside the scroll, shown while a line is pointed at (the old strip's own box)
        private static GameObject WhyBox(string tip, float boxLeft)
        {
            if (string.IsNullOrEmpty(tip) || _talkRoot == null) return null;
            RectTransform box = Rect("Why", _talkRoot.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
            float wide = ((RectTransform)_talkRoot.transform).rect.width; // (the screen in its units: a narrow window has less than the scroll and the box)
            box.anchoredPosition = new Vector2(wide > 1f ? Mathf.Min(boxLeft, wide - WhyW - 10f) : boxLeft, -TalkHang - 120f);
            box.sizeDelta = new Vector2(WhyW, 10f);
            CardPiece(box.gameObject.AddComponent<Image>(), "card_paper", 24f, 10f, Paper);
            VBox(box, 2f, TextAnchor.UpperLeft, WhyPad, WhyPad, WhyPad - 4, WhyPad - 2);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text t = T(box, tip, 15f, PInk, F.Body, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            box.gameObject.SetActive(false);
            return box.gameObject;
        }
    }
}
#endif

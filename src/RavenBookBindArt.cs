#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The в'язь drawn as a thing (the user's mockup 5a–5i): its two to four rune stones in a tight heap, tied over by the
    // knot of the tree that wove it — a living vine (green), a frosted thread with a crystal (icy), a burnt cord
    // smouldering (charred); the knots are tools/tree_art.js's assets/book/tree_knot_*.png. Shown in the book (the
    // в'язь's cell and its card), in the tree's window when you have one, and big on the screen when it is woven.
    internal static partial class RavenBook
    {
        private static readonly string[] KnotArt = { "tree_knot_icy", "tree_knot_green", "tree_knot_charred" }; // by the tree's kind
        // where each stone lies in the heap (× the emblem's size) and its turn, for 2 / 3 / 4 stones
        private static readonly Vector3[][] HeapAt =
        {
            new[] { new Vector3(-0.11f, -0.05f, -12f), new Vector3(0.12f, 0.05f, 10f) },
            new[] { new Vector3(-0.12f, -0.09f, -10f), new Vector3(0.13f, -0.09f, 12f), new Vector3(0f, 0.11f, -4f) },
            new[] { new Vector3(-0.11f, 0.1f, -8f), new Vector3(0.12f, 0.1f, 9f), new Vector3(-0.11f, -0.12f, 6f), new Vector3(0.12f, -0.12f, -10f) },
        };

        // the emblem, centred in its parent (a square of this size); null when the в'язь cannot be read
        private static RectTransform BindEmblem(Transform parent, Bind b, List<string> runes, float size)
        {
            if (b == null || runes == null || runes.Count == 0) return null;
            RectTransform root = Rect("Emblem", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            Image shade = Img(root, "Shade", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.45f));
            Stretch(shade.rectTransform, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);
            Vector3[] at = HeapAt[Mathf.Clamp(runes.Count, 2, 4) - 2];
            for (int i = 0; i < runes.Count && i < at.Length; i++)
            {
                RuneDef r = RuneBook.Def(runes[i]);
                if (r == null) continue;
                RectTransform c = Rect("Stone", root);
                c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0.5f);
                c.sizeDelta = new Vector2(size * 0.42f, size * 0.42f);
                c.anchoredPosition = new Vector2(at[i].x * size, at[i].y * size);
                c.localEulerAngles = new Vector3(0f, 0f, at[i].z);
                if (!RuneStone(c, (int)r.Kind, r.Glyph, 2, 0f, size * 0.15f))
                {
                    TMP_Text g = T(c, r.Glyph, size * 0.17f, Gold, F.Rune, TextAlignmentOptions.Center);
                    Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    g.raycastTarget = false;
                }
            }
            Image knot = Img(root, "Knot", BookArt.Picture(KnotArt[Mathf.Clamp(b.Tree, 0, 2)]), Color.white);
            Stretch(knot.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
            knot.preserveAspect = true;
            return root;
        }

        // ---------------------------------------------------------------- woven: the result on the screen
        private static GameObject _brRoot;
        private static float _brUntil;

        internal static void ShowBindResult(Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            Bind b = Bind.Parse(d.Bind);
            if (b == null) return;
            if (_brRoot != null) Object.Destroy(_brRoot);
            EnsureGameFont();
            _brRoot = OverlayCanvas("RavenWatch_BindResult", 30003, true);
            Image dim = Img(_brRoot.transform, "Dim", null, new Color(0f, 0f, 0f, 0.5f));
            ClickArea(dim.gameObject, CloseBindResult); // (a click anywhere closes it)

            Color light = TreeLight[Mathf.Clamp(b.Tree, 0, 2)];
            RectTransform panel = Rect("Panel", _brRoot.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(680f, 680f);
            Image glow = Img(panel, "Glow", BookArt.SoftDisc, A(light, 0.25f));
            Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-80f, -80f), new Vector2(80f, 80f));
            RectTransform sheet = Rect("Sheet", panel);
            Stretch(sheet, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Box(sheet, 14f, TreeSheet[Mathf.Clamp(b.Tree, 0, 2)], A(light, 0.6f), 2f);
            ClickArea(sheet.gameObject, CloseBindResult);

            TMP_Text title = T(panel, Lang.T("В'язь сплелась").ToUpperInvariant(), 34f, light, F.Head, TextAlignmentOptions.Top);
            At(title.rectTransform, 0f, 30f, 680f, 50f);
            title.characterSpacing = 8f; title.raycastTarget = false;
            RectTransform holder = Rect("Holder", panel);
            At(holder, 190f, 90f, 300f, 300f);
            BindEmblem(holder, b, d.BindRunes, 300f);
            BindPower w = BindPower.Of(b);
            TMP_Text line = T(panel, Short(d, d.BindRunes, w), 22f, C("#ecdcb4"), F.Body, TextAlignmentOptions.Top);
            At(line.rectTransform, 50f, 410f, 580f, 110f);
            line.richText = true; line.raycastTarget = false;
            TMP_Text hint = T(panel, Lang.T("Вона вже діє на тобі. Повне речення — у книзі, «Шлях Одіна»."), 14f, A(light, 0.7f), F.Italic, TextAlignmentOptions.Bottom);
            At(hint.rectTransform, 50f, 528f, 580f, 46f);
            hint.raycastTarget = false;
            RectTransform row = Rect("Close", panel); // (the cursor shows while it is up: BindResultOpen)
            At(row, 240f, 600f, 200f, 46f);
            HBox(row, 0f, TextAnchor.MiddleCenter, 0, 0, 0, 0);
            CardButton(row, Lang.T("Закрити"), true, CloseBindResult);
            _brUntil = Time.unscaledTime + 25f;
        }

        internal static bool BindResultOpen { get { return _brRoot != null; } }

        private static void CloseBindResult()
        {
            if (_brRoot != null) Object.Destroy(_brRoot);
            _brRoot = null;
        }

        // from the book's Update: Esc, or its time out
        private static void BindResultTick()
        {
            if (_brRoot == null) return;
            if (Time.unscaledTime > _brUntil || RavenKeys.Down(KeyCode.Escape) || Player.m_localPlayer == null || Player.m_localPlayer.IsDead()) CloseBindResult();
        }
    }
}
#endif

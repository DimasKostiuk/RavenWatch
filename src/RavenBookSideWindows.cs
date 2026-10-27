#if RAVEN_ITEM
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    internal static partial class RavenBook
    {
        // the game's own font for the windows beside the book (taken from the inventory's texts once it is there)
        internal static void EnsureGameFont()
        {
            if (_gameFont != null || InventoryGui.instance == null) return;
            TMP_Text sample = InventoryGui.instance.m_recipeName != null ? InventoryGui.instance.m_recipeName : InventoryGui.instance.m_playerName;
            _gameFont = sample != null ? sample.font : null;
            BookArt.Fonts(_gameFont);
        }

        // a screen-wide canvas of its own for a window beside the book: over the game at this order, the game's 1080p
        // scale; clicks: it takes the pointer (a bar only shows)
        internal static GameObject OverlayCanvas(string name, int order, bool clicks)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            Canvas c = root.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            CanvasScaler sc = root.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 1f;
            if (clicks) root.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root;
        }

        // ---------------------------------------------------------------- the tip plates' look
        // Every tip plate (the book's tooltip and help, the side windows' tips, the circles' info bar, hints and chips): a dark warm
        // rounded plate with a soft shadow under it, a thin gold frame and a faint second one just inside it
        internal static readonly Color TipFill = new Color(0.085f, 0.066f, 0.045f, 0.96f), TipTitle = new Color(0.91f, 0.78f, 0.5f);

        internal static void TipPlate(RectTransform r, float radius)
        {
            Box(r, radius, TipFill, A(Gold, 0.8f), 1f);
            Image fill = r.GetComponent<Image>();
            if (fill != null && fill.GetComponent<UnityEngine.UI.Shadow>() == null)
            {
                UnityEngine.UI.Shadow sh = fill.gameObject.AddComponent<UnityEngine.UI.Shadow>(); // (the same rounded plate, dark and lower: a shadow under it)
                sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(2f, -4f);
            }
            RectTransform inner = Rect("Inner", r);
            Stretch(inner, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image l = inner.gameObject.AddComponent<Image>();
            l.sprite = BookArt.FrameFine(Mathf.Max(2f, radius - 3f), 1f); l.type = Image.Type.Sliced; l.pixelsPerUnitMultiplier = BookArt.Fine;
            l.color = A(Gold, 0.22f); l.raycastTarget = false;
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // ---------------------------------------------------------------- a plate on the screen for the IMGUI circles
        // (Q/J): their info bar, hints and chips in the book's fonts and look. Placed in screen pixels from OnGUI (top-left, y
        // down); shown while it is placed every frame, gone by itself a frame after the circle stops placing it.
        internal sealed class ScreenPlate
        {
            private readonly string _name;
            private readonly int _order;
            private GameObject _root;
            private RectTransform _box, _rule;
            private TMP_Text _title, _desc;
            private string _t, _d, _line;
            private float _w, _s, _h;
            internal int Shown = -10;

            internal ScreenPlate(string name, int order) { _name = name; _order = order; }

            private void Make()
            {
                EnsureGameFont();
                _root = new GameObject(_name, typeof(RectTransform));
                Canvas c = _root.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = _order; // (no scaler: pixels, as the circle's own)
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _root.AddComponent<ScreenPlateLife>().Plate = this;
                _box = Rect("Plate", _root.transform);
                _box.anchorMin = _box.anchorMax = _box.pivot = new Vector2(0f, 1f);
                TipPlate(_box, 10f);
                _title = T(_box, "", 17f, TipTitle, F.Head, TextAlignmentOptions.TopLeft);
                _rule = Rect("Rule", _box);
                Image rule = _rule.gameObject.AddComponent<Image>(); rule.color = A(Gold, 0.3f); rule.raycastTarget = false;
                _desc = T(_box, "", 14f, BookArt.TipInk, F.Body, TextAlignmentOptions.TopLeft);
                foreach (RectTransform t in new[] { _title.rectTransform, _rule, _desc.rectTransform }) { t.anchorMin = t.anchorMax = t.pivot = new Vector2(0f, 1f); }
                foreach (Image i in _root.GetComponentsInChildren<Image>(true)) i.raycastTarget = false;
            }

            // its words, width and scale (screen px): the plate's height; laid out only when something changed
            internal float Set(string title, string desc, float w, float s)
            {
                if (_root == null) Make();
                if (_line == null && title == _t && desc == _d && w == _w && s == _s) return _h;
                _t = title; _d = desc; _w = w; _s = s; _line = null;
                title = Grammar.Fix(title); desc = Grammar.Fix(desc); // ({в|ла}: your forms)
                bool hasTitle = !string.IsNullOrEmpty(title), hasDesc = !string.IsNullOrEmpty(desc);
                float pad = 14f * s, inner = w - 2f * pad, y = 10f * s;
                _title.gameObject.SetActive(hasTitle); _rule.gameObject.SetActive(hasTitle && hasDesc); _desc.gameObject.SetActive(hasDesc);
                if (hasTitle)
                {
                    _title.fontSize = 17f * s; _title.text = title;
                    float th = _title.GetPreferredValues(title, inner, 0f).y;
                    _title.rectTransform.anchoredPosition = new Vector2(pad, -y); _title.rectTransform.sizeDelta = new Vector2(inner, th);
                    y += th;
                    if (hasDesc) { y += 5f * s; _rule.anchoredPosition = new Vector2(pad, -y); _rule.sizeDelta = new Vector2(Mathf.Min(inner, 140f * s), Mathf.Max(1f, s)); y += 6f * s; }
                }
                if (hasDesc)
                {
                    _desc.fontSize = 14f * s; _desc.text = desc;
                    float dh = _desc.GetPreferredValues(desc, inner, 0f).y;
                    _desc.rectTransform.anchoredPosition = new Vector2(pad, -y); _desc.rectTransform.sizeDelta = new Vector2(inner, dh);
                    y += dh;
                }
                _h = y + 11f * s;
                _box.sizeDelta = new Vector2(w, _h);
                return _h;
            }

            // one short line (the circles' chips over the wings): the plate just as wide as its words; its size
            internal Vector2 SetLine(string text, float s)
            {
                if (_root == null) Make();
                if (text == _line && s == _s) return new Vector2(_w, _h);
                _line = text; _s = s; _t = _d = null;
                _title.gameObject.SetActive(false); _rule.gameObject.SetActive(false); _desc.gameObject.SetActive(true);
                float padX = 14f * s, padY = 7f * s;
                _desc.fontSize = 14f * s; _desc.text = text;
                Vector2 p = _desc.GetPreferredValues(text);
                _desc.rectTransform.anchoredPosition = new Vector2(padX, -padY); _desc.rectTransform.sizeDelta = new Vector2(p.x + 2f, p.y);
                _w = Mathf.Ceil(p.x + 2f + 2f * padX); _h = Mathf.Ceil(p.y + 2f * padY);
                _box.sizeDelta = new Vector2(_w, _h);
                return new Vector2(_w, _h);
            }

            // its top-left corner in GUI pixels (y down), this frame
            internal void Place(float x, float y)
            {
                if (_root == null) return;
                Vector2 at = new Vector2(x, -y);
                if (_box.anchoredPosition != at) _box.anchoredPosition = at; // (a plate standing still: its canvas left alone)
                Shown = Time.frameCount;
                if (!_root.activeSelf) _root.SetActive(true);
            }

            internal void Hide() { if (_root != null && _root.activeSelf) _root.SetActive(false); }
        }

        internal sealed class ScreenPlateLife : MonoBehaviour
        {
            internal ScreenPlate Plate;
            private void LateUpdate() { if (Plate != null && Time.frameCount - Plate.Shown > 1) Plate.Hide(); } // (the circle closed or shows none)
        }

        // the tooltip of a window beside the book (the nest's, the raven tree's: the book's own lives only inside the
        // open book): what is under the pointer, beside it. Its text is asked for when the pointer comes and twice a
        // second after (a charge filling shows), never every frame.
        internal sealed class WinTip
        {
            private readonly float _width;
            private RectTransform _box;
            private TMP_Text _text;
            private Func<string> _fn, _asked;
            private string _said;
            private float _askAt;

            internal WinTip(float width) { _width = width; }

            internal Func<string> Fn { get { return _fn; } set { _fn = value; } }

            internal void Attach(GameObject g, Func<string> text)
            {
                Graphic gr = g.GetComponent<Graphic>();
                if (gr != null) gr.raycastTarget = true;
                else g.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                WinTipHover h = g.GetComponent<WinTipHover>();
                if (h == null) h = g.AddComponent<WinTipHover>();
                h.Tip = this; h.Text = text;
            }

            internal void Hide()
            {
                _fn = null; _asked = null;
                if (_box != null) _box.gameObject.SetActive(false);
            }

            // every frame while the window is up; quiet: shown nothing (a drag going on)
            internal void Tick(Transform root, bool quiet)
            {
                if (_box == null)
                {
                    _box = Rect("Tip", root);
                    _box.anchorMin = _box.anchorMax = new Vector2(0.5f, 0.5f);
                    _box.pivot = new Vector2(0f, 1f);
                    TipPlate(_box, 8f);
                    foreach (Image i in _box.GetComponentsInChildren<Image>(true)) i.raycastTarget = false;
                    _text = T(_box, "", 15f, BookArt.TipInk, F.Body);
                    Stretch(_text.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 12f), new Vector2(-14f, -12f));
                    _text.richText = true; _text.raycastTarget = false;
                }
                if (_fn == null || quiet) { _asked = null; if (_box.gameObject.activeSelf) _box.gameObject.SetActive(false); return; }
                if (_fn != _asked || Time.unscaledTime >= _askAt)
                {
                    _asked = _fn; _askAt = Time.unscaledTime + 0.5f;
                    try { _said = Grammar.Fix(_fn()); } catch { _said = null; }
                }
                if (string.IsNullOrEmpty(_said)) { if (_box.gameObject.activeSelf) _box.gameObject.SetActive(false); return; }
                if (_text.text != _said)
                {
                    _text.text = _said;
                    Vector2 size = _text.GetPreferredValues(_said, _width, 0f);
                    float w = Mathf.Min(_width, size.x) + 28f;
                    _box.sizeDelta = new Vector2(w, _text.GetPreferredValues(_said, w - 28f, 0f).y + 24f);
                }
                _box.gameObject.SetActive(true);
                _box.SetAsLastSibling();
                RectTransform r = (RectTransform)root;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(r, Input.mousePosition, null, out local);
                Vector2 half = r.rect.size / 2f, sz = _box.sizeDelta;
                float x = local.x + 18f, y = local.y - 18f;
                if (x + sz.x > half.x - 8f) x = local.x - 12f - sz.x;
                if (y - sz.y < -half.y + 8f) y = local.y + 12f + sz.y;
                _box.anchoredPosition = new Vector2(x, y);
            }
        }

        internal sealed class WinTipHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal WinTip Tip;
            internal Func<string> Text;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Tip != null) Tip.Fn = Text; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Tip != null && Tip.Fn == Text) Tip.Fn = null; }
            private void OnDisable() { if (Tip != null && Tip.Fn == Text) Tip.Fn = null; }
        }
    }
}
#endif

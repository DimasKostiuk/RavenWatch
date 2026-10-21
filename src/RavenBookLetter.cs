#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuginWhispers
{
    // A letter that came (Post), after the user's design (Desktop\export\letter-2c; its pictures cut into layers without
    // words by tools/letter_art.html → assets/book/letter_*.png). The raven drops it closed: the envelope with its wax
    // seal and the sender's name comes up out of nothing, from half its size. A click on the seal (or LetterAutoOpen
    // seconds) breaks it: the envelope shudders, the seal's halves fall away, the envelope gives way to the sheet still
    // folded, which opens along its fold, and the words come up on it. «Скласти» folds it away; then the next one.
    internal static partial class RavenBook
    {
        private sealed class Got { public string From, Text, Line; public bool Munin; }
        private static readonly Queue<Got> Letters = new Queue<Got>();
        private static GameObject _lwRoot;
        internal static float LetterAutoOpen = 15f;

        // the design's pixels to the screen's (1080 high): the envelope is 600×840, the sheet 840×1120 with its fold at 446
        private const float EnvK = 0.72f, PaperK = 0.74f, PaperW = 840f, PaperH = 1120f, FoldY = 446f;
        private const float AppearTime = 0.35f, OpenTime = 1.35f, FoldTime = 0.28f;
        private static readonly Color LetterBack = new Color(0.78f, 0.66f, 0.5f, 1f); // the sheet's other side, while it lies folded over

        private enum LState { Appear, Closed, Opening, Open, Folding }
        private static LState _lState;
        private static float _lAt, _lShown;
        private static Image _lDim;
        private static RectTransform _lClosed, _lSeal, _lHalfL, _lHalfR, _lOpen, _lTop;
        private static CanvasGroup _lClosedG, _lHalvesG, _lOpenG, _lWordsG;
        private static RawImage _lTopImg;
        private static LetterSealHover _lHover;

        internal static bool LetterOpen { get { return _lwRoot != null && _lwRoot.activeSelf; } }

        internal static void ShowLetter(string from, bool munin, string text, string line)
        {
            Letters.Enqueue(new Got { From = from, Text = text, Line = line, Munin = munin });
            if (!LetterOpen) NextLetter();
        }

        private static void NextLetter()
        {
            if (Letters.Count == 0) { if (_lwRoot != null) _lwRoot.SetActive(false); return; }
            Got g = Letters.Dequeue();
            if (_gameFont == null && InventoryGui.instance != null)
            {
                TMP_Text sample = InventoryGui.instance.m_recipeName != null ? InventoryGui.instance.m_recipeName : InventoryGui.instance.m_playerName;
                _gameFont = sample != null ? sample.font : null;
                BookArt.Fonts(_gameFont);
            }
            if (_lwRoot != null) Object.Destroy(_lwRoot);
            _lwRoot = new GameObject("RavenWatch_Letter", typeof(RectTransform));
            Canvas c = _lwRoot.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 30003;
            CanvasScaler sc = _lwRoot.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 1f;
            _lwRoot.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(_lwRoot);
            Transform root = _lwRoot.transform;

            _lDim = Rect("Dim", root).gameObject.AddComponent<Image>();
            Stretch(_lDim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lDim.color = new Color(0f, 0f, 0f, 0f);

            BuildOpenSheet(root, g);   // under the envelope: it comes up as the envelope fades
            BuildEnvelope(root, g);

            _lState = LState.Appear;
            _lAt = _lShown = Time.unscaledTime;
            LetterAnimate(0f);
            Player me = Player.m_localPlayer;
            if (me != null) ScreamFx.CawAt(me.transform, me.transform.position + Vector3.up * 1.6f, g.Munin ? 0.95f : 1.4f, 0.45f, false);
        }

        // ---------------------------------------------------------------- the closed letter

        private static void BuildEnvelope(Transform root, Got g)
        {
            _lClosed = Centered(Rect("Closed", root), 600f * EnvK, 840f * EnvK);
            _lClosedG = _lClosed.gameObject.AddComponent<CanvasGroup>();
            Image env = Img(_lClosed, "Envelope", BookArt.Picture("letter_envelope"), Color.white);
            if (env.sprite == null) env.color = Paper;
            env.raycastTarget = true; // (a click on the paper is not a click through it)

            TMP_Text name = T(_lClosed, g.From, 44f * EnvK, C("#4a2e14"), F.Italic, TextAlignmentOptions.Center);
            name.richText = false;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform nr = name.rectTransform;
            nr.anchorMin = new Vector2(0.08f, 1f - 745f / 840f); nr.anchorMax = new Vector2(0.92f, 1f - 665f / 840f);
            nr.offsetMin = nr.offsetMax = Vector2.zero;

            TMP_Text hint = T(_lClosed, Lang.T("Зламай печатку"), 17f, A(Paper, 0.75f), F.Italic, TextAlignmentOptions.Center);
            RectTransform hr = hint.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, 28f); hr.anchoredPosition = new Vector2(0f, -10f);

            _lSeal = SealPiece(_lClosed, "letter_seal");
            Image si = _lSeal.GetComponent<Image>();
            si.raycastTarget = true;
            Button b = _lSeal.gameObject.AddComponent<Button>();
            b.targetGraphic = si;
            b.transition = Selectable.Transition.None; // (it grows under the pointer instead)
            b.onClick.AddListener(OpenLetterNow);
            _lHover = _lSeal.gameObject.AddComponent<LetterSealHover>();

            // its halves once broken: beside the envelope, not in it (they fall on their own while it fades)
            RectTransform halves = Centered(Rect("Halves", root), 600f * EnvK, 840f * EnvK);
            _lHalvesG = halves.gameObject.AddComponent<CanvasGroup>();
            _lHalvesG.alpha = 0f; _lHalvesG.blocksRaycasts = false;
            _lHalfL = SealPiece(halves, "letter_seal_l");
            _lHalfR = SealPiece(halves, "letter_seal_r");
        }

        // a piece of the seal where the design has it: its middle at (300, 428) of the envelope
        private static RectTransform SealPiece(Transform parent, string pic)
        {
            Image i = Img(parent, pic, BookArt.Picture(pic), Color.white);
            if (i.sprite == null) { i.sprite = BookArt.Circle; i.color = C("#a42e29"); }
            RectTransform r = i.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f - 428f / 840f);
            r.pivot = new Vector2(0.5f, 1f - 104f / 240f);
            r.sizeDelta = new Vector2(220f * EnvK, 240f * EnvK);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        private static RectTransform Centered(RectTransform r, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        // ---------------------------------------------------------------- the open sheet

        private static void BuildOpenSheet(Transform root, Got g)
        {
            _lOpen = Centered(Rect("Open", root), PaperW * PaperK, PaperH * PaperK);
            _lOpenG = _lOpen.gameObject.AddComponent<CanvasGroup>();
            _lOpenG.alpha = 0f; _lOpenG.blocksRaycasts = false;
            Texture2D tex = BookArt.Texture("letter_paper");
            float top = FoldY / PaperH; // the part above the fold (the one folded over)

            RawImage bottom = Rect("Lower", _lOpen).gameObject.AddComponent<RawImage>();
            bottom.texture = tex; bottom.color = tex != null ? Color.white : Paper;
            bottom.uvRect = new Rect(0f, 0f, 1f, 1f - top); // (a texture's v runs up from its bottom)
            RectTransform br = bottom.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(1f, 1f - top); br.offsetMin = br.offsetMax = Vector2.zero;

            _lTopImg = Rect("Upper", _lOpen).gameObject.AddComponent<RawImage>();
            _lTopImg.texture = tex; _lTopImg.color = tex != null ? Color.white : Paper;
            _lTopImg.uvRect = new Rect(0f, 1f - top, 1f, top);
            _lTop = _lTopImg.rectTransform;
            _lTop.anchorMin = new Vector2(0f, 1f - top); _lTop.anchorMax = Vector2.one; _lTop.offsetMin = _lTop.offsetMax = Vector2.zero;
            _lTop.pivot = new Vector2(0.5f, 0f); // it turns on the fold

            RectTransform words = Rect("Words", _lOpen);
            Stretch(words, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _lWordsG = words.gameObject.AddComponent<CanvasGroup>();
            _lWordsG.alpha = 0f; _lWordsG.blocksRaycasts = false;

            TMP_Text head = T(words, (Lang.T("Лист") + " · " + g.From).ToUpperInvariant(), 22f * PaperK, C("#6b4a26"), F.Head);
            head.richText = false;
            head.characterSpacing = 18f;
            head.textWrappingMode = TextWrappingModes.NoWrap;
            head.overflowMode = TextOverflowModes.Ellipsis;
            Px(head.rectTransform, 92f, 92f, 656f, 40f);

            // the words, as large as the design has them while they fit above the raven's (smaller when there are many)
            string text = Post.Clean(g.Text, BookArt.Italic ?? BookArt.Body);
            TMP_Text body = T(words, text, 40f * PaperK, C("#2a1a0c"), F.Italic);
            body.richText = false; // the sender's words, as they are
            body.lineSpacing = 22f;
            float w = 656f * PaperK, room = (890f - 160f) * PaperK, size = 40f * PaperK, h = 0f;
            for (; ; size -= 1f)
            {
                body.fontSize = size;
                h = body.GetPreferredValues(text, w, 0f).y;
                if (h + size * 2.2f <= room || size <= 14f) break;
            }
            Px(body.rectTransform, 92f, 160f, 656f, h / PaperK + 6f);

            TMP_Text sig = T(words, "— " + Initial(g.From) + ".", size, C("#2a1a0c"), F.Italic, TextAlignmentOptions.TopRight);
            sig.richText = false;
            Px(sig.rectTransform, 92f, 160f + h / PaperK + 14f, 656f, size * 1.6f / PaperK);

            // the raven's word, on a slip laid a little askew
            RectTransform slip = Rect("Slip", words);
            slip.anchorMin = slip.anchorMax = new Vector2(0f, 1f);
            slip.pivot = new Vector2(0.5f, 0.5f);
            slip.sizeDelta = new Vector2(456f * PaperK, 132f * PaperK);
            slip.anchoredPosition = new Vector2((92f + 228f) * PaperK, -(914f + 66f) * PaperK);
            slip.localRotation = Quaternion.Euler(0f, 0f, 2f);
            Box(slip, 4f, new Color(0f, 0f, 0f, 0f), C("#6b5f52"), 1.5f);
            TMP_Text said = T(slip, Plugin.RavenName(g.Munin) + ": «" + g.Line + "»", 24f * PaperK, g.Munin ? C("#8E1E1E") : C("#26304a"), F.Italic, TextAlignmentOptions.MidlineLeft);
            said.richText = false;
            Stretch(said.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));
            said.enableAutoSizing = true; said.fontSizeMin = 11f; said.fontSizeMax = 24f * PaperK;

            RectTransform fold = DarkButton(words, Lang.T("Скласти"), FoldLetter);
            Px(fold, 572f, 968f, 176f, 72f);
        }

        // a place on the sheet in the design's pixels (from its upper left corner)
        private static void Px(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x * PaperK, -y * PaperK);
            r.sizeDelta = new Vector2(w * PaperK, h * PaperK);
        }

        private static string Initial(string name)
        {
            string n = (name ?? "").Trim();
            return n.Length > 0 ? char.ToUpperInvariant(n[0]).ToString() : "?";
        }

        // ---------------------------------------------------------------- what it does

        private static void OpenLetterNow()
        {
            if (_lwRoot == null || (_lState != LState.Appear && _lState != LState.Closed)) return;
            if (_lState == LState.Appear) LetterAnimate(AppearTime); // (clicked while it still came up: whole first)
            _lState = LState.Opening;
            _lAt = Time.unscaledTime;
            _lSeal.gameObject.SetActive(false);
            _lHalvesG.alpha = 1f;
            _lOpenG.alpha = 0f;
            SetFold(-1f);
        }

        private static void FoldLetter()
        {
            if (_lwRoot == null || _lState != LState.Open) return;
            _lState = LState.Folding;
            _lAt = Time.unscaledTime;
            _lWordsG.blocksRaycasts = false;
        }

        private static void CloseLetter()
        {
            if (_lwRoot != null) { Object.Destroy(_lwRoot); _lwRoot = null; }
            if (Letters.Count > 0) NextLetter();
        }

        // the folded part: -1 lies over the lower part (its back seen), 1 open
        private static void SetFold(float s)
        {
            _lTop.localScale = new Vector3(1f, Mathf.Abs(s) < 0.02f ? 0.02f * Mathf.Sign(s + 0.0001f) : s, 1f);
            _lTopImg.color = _lTopImg.texture == null ? Paper : s < 0f ? LetterBack : Color.white;
        }

        private static float EaseOut(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        private static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Seg(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }

        private static void LetterAnimate(float t)
        {
            switch (_lState)
            {
                case LState.Appear:
                {
                    float k = EaseOut(t / AppearTime); // out of nothing, from half its size
                    _lClosedG.alpha = k;
                    _lClosed.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, k);
                    _lDim.color = new Color(0f, 0f, 0f, 0.35f * k);
                    if (t >= AppearTime) { _lState = LState.Closed; _lAt = Time.unscaledTime; }
                    break;
                }
                case LState.Closed:
                {
                    // the seal breathes a little; under the pointer it comes up to meet it
                    float want = _lHover != null && _lHover.Over ? 1.08f : 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 3.2f);
                    _lSeal.localScale = Vector3.Lerp(_lSeal.localScale, Vector3.one * want, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                    if (Time.unscaledTime - _lShown >= LetterAutoOpen) OpenLetterNow();
                    break;
                }
                case LState.Opening:
                {
                    // the envelope shudders as the wax gives
                    float shake = t < 0.16f ? Mathf.Sin(t * 95f) * 2.4f * (1f - t / 0.16f) : 0f;
                    _lClosed.localRotation = Quaternion.Euler(0f, 0f, shake);
                    // the seal's halves fall apart and away
                    float f = Seg(t, 0f, 0.55f);
                    _lHalvesG.alpha = 1f - Seg(t, 0.2f, 0.55f);
                    _lHalfL.anchoredPosition = new Vector2(-80f * f, 50f * f - 340f * f * f);
                    _lHalfL.localRotation = Quaternion.Euler(0f, 0f, 38f * f);
                    _lHalfR.anchoredPosition = new Vector2(70f * f, 60f * f - 380f * f * f);
                    _lHalfR.localRotation = Quaternion.Euler(0f, 0f, -46f * f);
                    // the envelope gives way to the folded sheet
                    float e = Seg(t, 0.2f, 0.45f);
                    _lClosedG.alpha = 1f - e;
                    _lClosed.localScale = Vector3.one * (1f + 0.06f * e);
                    float p = EaseOut(Seg(t, 0.25f, 0.5f));
                    _lOpenG.alpha = p;
                    _lOpen.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, p);
                    // which opens along its fold, and its words come up
                    SetFold(Mathf.Lerp(-1f, 1f, EaseInOut(Seg(t, 0.45f, 1.0f))));
                    _lWordsG.alpha = Seg(t, 0.95f, OpenTime);
                    if (t >= OpenTime)
                    {
                        _lState = LState.Open; _lAt = Time.unscaledTime;
                        _lClosed.gameObject.SetActive(false);
                        _lOpenG.blocksRaycasts = true; _lWordsG.blocksRaycasts = true;
                    }
                    break;
                }
                case LState.Folding:
                {
                    float k = EaseOut(t / FoldTime);
                    _lOpenG.alpha = 1f - k;
                    _lOpen.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, k);
                    _lDim.color = new Color(0f, 0f, 0f, 0.35f * (1f - k));
                    if (t >= FoldTime) CloseLetter();
                    break;
                }
            }
        }

        // every frame (RavenBook.Update)
        private static void LetterTick()
        {
            if (!LetterOpen) return;
            if (Player.m_localPlayer == null) { Letters.Clear(); if (_lwRoot != null) { Object.Destroy(_lwRoot); _lwRoot = null; } return; }
            if (RavenKeys.Down(KeyCode.Escape))
            {
                if (_lState == LState.Open) FoldLetter();
                else if (_lState == LState.Appear || _lState == LState.Closed) OpenLetterNow(); // (Escape does not throw it away unread)
            }
            if (_lwRoot != null) LetterAnimate(Time.unscaledTime - _lAt);
        }
    }

    // the seal knows when the pointer is over it (it grows to meet it)
    internal sealed class LetterSealHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal bool Over;
        public void OnPointerEnter(PointerEventData e) { Over = true; }
        public void OnPointerExit(PointerEventData e) { Over = false; }
        private void OnDisable() { Over = false; } // (hidden under the pointer: no exit comes, it would stay grown)
    }
}
#endif

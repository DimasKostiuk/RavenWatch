#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The Book of ravens, after the design «Книга воронів» (Desktop\book-export, «Хід 9»): an open book over the dimmed game,
    // Munin's page on the left and Hugin's on the right, four tabs over it — Ravens, Gifts, Growth (trust, ritual,
    // training), Gear. The book with everything on its pages is drawn by its own camera, tilted back 8° in perspective as
    // in the design, into a picture shown over the game; clicks go to the tilted pieces themselves. The empty book, the
    // brush strokes and the portraits are pictures made from the design, the fonts its own (BookArt). Few words on the
    // page: numbers, bars, names, one seal to press; the rest is under the cursor (gold words) or behind "?".
    internal static partial class RavenBook
    {
        private static string[] TabNames { get { return Lang.Arr(TabNamesSrc); } } // (translated on every read)
        private static readonly string[] TabNamesSrc = Lang.Keys("Ворони", "Дари", "Розвиток", "Образ", "Шлях Одіна", "Підгонка");
        private const int RavensTab = 0, GiftsTab = 1, GrowTab = 2, OutfitTab = 3, RunesTab = 4, FitTab = 5; // RunesTab «Шлях Одіна»: the errand + every rune (RavenBookRunes) // FitTab: admins fitting pieces (Outfits.TuningAllowed); goes away later
        private static bool Dressing(int tab) { return tab == OutfitTab || tab == FitTab; }

        // ---------------------------------------------------------------- the design's measures (canvas units = its pixels)

        internal const string DiscordUrl = "https://discord.gg/XM5kuwpAUU";       // the RavenWatch server (bugs, ideas), never expires
        private const float W = 1240f, H = 880f;                                   // the window on the desk
        private const float BookX = 20f, BookTop = 134f, BookW = 1200f, BookH = 680f; // the book in it
        private const float TiltDeg = 8f, Perspective = 2200f;                      // rotateX(8deg), perspective 2200px
        private const float PageBottom = 640f;                                      // the pages' frames end here (book coordinates)

        private static Color C(string hex) { return BookArt.C(hex); }
        private static readonly Color Ink = BookArt.Ink, InkSoft = BookArt.InkSoft, Brown = BookArt.Brown, Caption = BookArt.Caption,
            GoldInk = BookArt.GoldInk, Good = BookArt.Good, Bad = BookArt.Bad, Gold = BookArt.Gold, Paper = BookArt.Paper, Muted = BookArt.Muted,
            SealInk = C("#f6e6c8");
        private const string GoldHex = BookArt.GoldHex, GoldInkHex = BookArt.GoldInkHex, GoodHex = BookArt.GoodHex, BadHex = BookArt.BadHex, MutedHex = BookArt.MutedHex;
        private const string GreyHex = "#8C93A1", DimHex = "#6B7280", OkHex = "#9FD49F", QuirkHex = "#B48CE6"; // in the dark tooltips
        private static string RavenHex(bool m) { return m ? "#E0283F" : "#FFC94A"; } // a raven's name in the dark tooltips

        // the satiety's zones on paper: colours, the brush of each
        private static readonly string[] ZoneInk = { "#8E1E1E", "#8E1E1E", "#9a6a20", "#5a7a3a", "#7a5a8a" };
        private static readonly string[] ZoneBrush = { "brush-sat-hungry", "brush-sat-hungry", "brush-sat-warn", "brush-sat-ok", "brush-sat-ok" };

        private static GameObject _root;          // the overlay: the dim, the book's picture, the tooltip, the food list
        private static GameObject _stage;         // the book itself: its canvas and its camera
        private static Camera _cam;
        private static RenderTexture _rt;
        private static RawImage _view;
        private static Canvas _stageCanvas;
        private static CanvasScaler _scaler, _stageScaler;
        private static RectTransform _window, _book, _pages, _help, _tip, _tabsBar, _footer;
        private static RectTransform _over; // over everything in the window, tilted as the book is: the popups (tooltip, food list, "?")
        private const float OverH = 900f;    // its height up from the book's lower edge (to above the window's top)
        private static Image _chrome;
        private static TMP_Text _helpText, _tipText;
        private static RectTransform[] _tabs;
        private static Image[] _tabDots;
        private static TMP_FontAsset _gameFont;
        private static int _tab;
        private static string _capturing;
        private static float _nextRefresh;
        private static string _shown; // what the open page was drawn from: drawn again when it changes
        private static readonly List<Action> Live = new List<Action>();
        private static KeyCode[] _captureKeys;

        internal static bool IsOpen { get { return _root != null && _root.activeInHierarchy; } }
        internal static bool Capturing { get { return IsOpen && _capturing != null; } }

        internal static void Toggle() { if (IsOpen) Close(); else Open(); }

        // The book is Odin's: it comes with the first raven or with his trial (Trial), and stays for good after that
        // (a lost raven is brought back from it). Kept in the character.
        internal const string GivenKey = "RavenWatch_bookGiven";

        internal static bool Given(Player p)
        {
            if (p == null) return false;
            if (p.m_customData.ContainsKey(GivenKey)) return true;
            bool had = Trial.HasRaven(p) || Trial.Running(p) || Trial.Over(p);
            if (!had) foreach (string k in p.m_customData.Keys) if (k.StartsWith("RavenWatch_copy_")) { had = true; break; } // (a raven of yours, now lost)
            if (had) p.m_customData[GivenKey] = "1";
            return had;
        }

        // Odin hands it over with the trial: opens on «Шлях Одіна» next time
        internal static void GiveFromOdin(Player p)
        {
            if (p == null) return;
            p.m_customData[GivenKey] = "1";
            _tab = RunesTab;
        }

        // straight on «Шлях Одіна» (the nest's bowl during Odin's trial)
        internal static void OpenPath() { _tab = RunesTab; if (IsOpen) Show(_tab); else Open(); }

        internal static void Open()
        {
            if (InventoryGui.instance == null || Player.m_localPlayer == null) return;
            if (!Given(Player.m_localPlayer)) { RavenMsg.Center(Player.m_localPlayer, Lang.T("Книги воронів у тебе ще нема. Її дає Одін — разом із вороном чи з випробуванням у Воронячому гнізді.")); return; }
            if (_root == null) Build();
            if (_root == null) return;
            CloseNestWindow(); CloseTreeWindow(); // (one window at a time)
            _capturing = null;
            _root.SetActive(true);
            _stage.SetActive(true);
            Rescale();
            StageOn();
            Show(_tab);
            MailCorner();
            GuideCorner();
        }

        // ---------------------------------------------------------------- size
        // The book follows the game's own interface scale (taken again at every opening: resolution and GUI scale may
        // have changed), times its own size from the config; and it never gets bigger than the screen allows.

        internal static float SizePct = 90f; // config "Book of ravens size percent"

        private static void Rescale()
        {
            if (_scaler == null || InventoryGui.instance == null) return;
            CanvasScaler game = InventoryGui.instance.GetComponentInParent<CanvasScaler>();
            float k = Mathf.Clamp(SizePct, 40f, 200f) / 100f;
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            if (game != null && game.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.screenMatchMode = game.screenMatchMode;
                _scaler.matchWidthOrHeight = game.matchWidthOrHeight;
                _scaler.referencePixelsPerUnit = game.referencePixelsPerUnit;
                Vector2 rf = game.referenceResolution;
                float s = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(w / rf.x, 2f), Mathf.Log(h / rf.y, 2f), game.matchWidthOrHeight)) * k;
                k *= Fit(w / s, h / s);
                _scaler.referenceResolution = rf / k;
            }
            else
            {
                float base_ = game != null ? game.scaleFactor : h / 1080f;
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.referencePixelsPerUnit = game != null ? game.referencePixelsPerUnit : 100f;
                float s = base_ * k;
                _scaler.scaleFactor = s * Fit(w / s, h / s);
            }
            // the book's own canvas: the very same scale (its units are the overlay's)
            _stageScaler.uiScaleMode = _scaler.uiScaleMode;
            _stageScaler.screenMatchMode = _scaler.screenMatchMode;
            _stageScaler.matchWidthOrHeight = _scaler.matchWidthOrHeight;
            _stageScaler.referencePixelsPerUnit = _scaler.referencePixelsPerUnit;
            _stageScaler.referenceResolution = _scaler.referenceResolution;
            _stageScaler.scaleFactor = _scaler.scaleFactor;
        }

        // a further shrink when the window would not fit the screen (with a small margin)
        private static float Fit(float unitsWide, float unitsHigh)
        {
            return Mathf.Min(1f, unitsWide * 0.98f / W, unitsHigh * 0.97f / H);
        }

        internal static void Close()
        {
            _capturing = null;
            ClosePopup();
            LeaveOutfits();
            OutfitStage.Close();
            Outfits.PlayerView = false; // the world dresses the ravens as tuning says again
            HideTip();
            StageOff();
            if (_stage != null) _stage.SetActive(false);
            if (_root != null) _root.SetActive(false);
        }

        // from the plugin's Update
        internal static void Update()
        {
            NestWindowTick(); // (the nest's own window: not the book)
            LetterTick();     // (a letter that came: not the book either)
            PostParcel.Guard(); // (the parcel's window shut: the post again)
            TreeWindowTick(); // (the raven tree's too)
            if (!IsOpen) return;
            if (Player.m_localPlayer == null || InventoryGui.IsVisible() || Menu.IsVisible()) { Close(); return; }
            StageTick();
            Animate();
            if (_capturing != null) { Capture(); MoveTip(); return; }
            if (_popup != null)
            {
                MailTick();
                if (RavenKeys.Down(KeyCode.Escape)) ClosePopup();
                else if (_nickYes != null && (RavenKeys.Down(KeyCode.Return) || RavenKeys.Down(KeyCode.KeypadEnter))) _nickYes();
                return;
            }
            if (RavenKeys.Down(KeyCode.Escape)) { if (_help != null && _help.gameObject.activeSelf) _help.gameObject.SetActive(false); else Close(); return; }
            if (Dressing(_tab)) TickOutfit();
            MoveTip();
            if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 1f; Renew(); MailCorner(); }
        }

        // ---------------------------------------------------------------- the stage: the book's own camera
        // The game's overlay windows are drawn after every camera, so the tilted book cannot be a camera canvas on the
        // screen: its camera draws it into a picture of the screen's size, and the overlay shows that picture on top of
        // everything. The book's raycaster answers before the game's overlays (its priority is the book's), so the clicks
        // reach the tilted pieces wherever they are.

        private static void StageOn()
        {
            if (_cam == null) return;
            int w = Mathf.Max(16, Screen.width), h = Mathf.Max(16, Screen.height);
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                FreeTarget();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                _rt.antiAliasing = 4;
                _rt.hideFlags = HideFlags.HideAndDontSave;
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            _view.texture = _rt;
            _cam.enabled = true;
            StageTick();
        }

        private static void StageOff()
        {
            if (_cam != null) { _cam.enabled = false; _cam.targetTexture = null; }
            if (_view != null) _view.texture = null;
            FreeTarget();
        }

        private static void FreeTarget()
        {
            if (_rt == null) return;
            _rt.Release();
            UnityEngine.Object.Destroy(_rt);
            _rt = null;
        }

        // every frame while open: the perspective of the design (2200 of its pixels from the eye) for the canvas's size now
        private static void StageTick()
        {
            if (_cam == null || _stageCanvas == null) return;
            if (_rt != null && (_rt.width != Screen.width || _rt.height != Screen.height)) { Rescale(); StageOn(); return; }
            float scale = Mathf.Max(0.01f, _stageCanvas.scaleFactor);
            float unitsHigh = Screen.height / scale;
            _cam.fieldOfView = 2f * Mathf.Atan(unitsHigh * 0.5f / Perspective) * Mathf.Rad2Deg;
        }

        // ---------------------------------------------------------------- the window

        private static void Build()
        {
            try
            {
                InventoryGui inv = InventoryGui.instance;
                TMP_Text sample = inv.m_recipeName != null ? inv.m_recipeName : inv.m_playerName;
                _gameFont = sample != null ? sample.font : null;
                BookArt.Fonts(_gameFont);

                // the overlay: over everything, the game's scale
                _root = new GameObject("RavenWatch_Book", typeof(RectTransform));
                Canvas canvas = _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30001;
                _scaler = _root.AddComponent<CanvasScaler>();
                _root.AddComponent<GraphicRaycaster>();
                UnityEngine.Object.DontDestroyOnLoad(_root);
                Image dim = Rect("Dim", _root.transform).gameObject.AddComponent<Image>();
                Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                dim.color = new Color(0f, 0f, 0f, 0.45f); dim.raycastTarget = false;
                _view = Rect("Book picture", _root.transform).gameObject.AddComponent<RawImage>();
                Stretch(_view.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                _view.raycastTarget = false;

                // the stage: far from the world, seen only by its own camera
                _stage = new GameObject("RavenWatch_BookStage");
                _stage.transform.position = new Vector3(0f, 20000f, 0f);
                UnityEngine.Object.DontDestroyOnLoad(_stage);
                GameObject camGo = new GameObject("Camera");
                camGo.transform.SetParent(_stage.transform, false);
                _cam = camGo.AddComponent<Camera>();
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _cam.cullingMask = 1 << 5;
                _cam.nearClipPlane = 1f; _cam.farClipPlane = 400f;
                _cam.depth = 100f;
                _cam.allowHDR = false; _cam.allowMSAA = true; _cam.useOcclusionCulling = false;
                _cam.enabled = false;
                GameObject cv = new GameObject("Canvas", typeof(RectTransform));
                cv.layer = 5;
                cv.transform.SetParent(_stage.transform, false);
                _stageCanvas = cv.AddComponent<Canvas>();
                _stageCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _stageCanvas.worldCamera = _cam;
                _stageCanvas.planeDistance = 100f;
                _stageScaler = cv.AddComponent<CanvasScaler>();
                cv.AddComponent<BookRaycaster>();
                Image catcher = Rect("Catch", cv.transform).gameObject.AddComponent<Image>(); // clicks off the book stay with the book
                Stretch(catcher.rectTransform, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
                catcher.color = new Color(0f, 0f, 0f, 0f);

                _window = Rect("Window", cv.transform);
                _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 0.5f);
                _window.sizeDelta = new Vector2(W, H);
                // no desk under the book (the dimmed game shows around it). The title on a small dark plaque hung on
                // Yggdrasil: the crown spreads over it, the trunk goes down behind the plaque and the tabs into the book.
                Sprite tree = BookArt.Picture("yggdrasil-tree"); // (the book's pictures load mipmapped: smooth at a third of its size)
                if (tree != null)
                {
                    Image ygg = Img(_window, "Yggdrasil", tree, Color.white);
                    // the picture's trunk stands 50 px right of its middle (710 of 1320): moved back so it rises straight
                    // up from the plaque and the tabs; set low enough that its foot goes under the tilted book (no seam above it)
                    At(ygg.rectTransform, W / 2f - 230f - 50f * 460f / 1320f, 12f, 460f, 460f * 450f / 1320f);
                }
                RectTransform plaque = Rect("Plaque", _window);
                At(plaque, W / 2f - 180f, 56f, 360f, 50f);
                Image psh = Img(plaque, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.5f));
                Stretch(psh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-14f, -14f), new Vector2(14f, 6f));
                RectTransform face = Rect("Face", plaque);
                Stretch(face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Box(face, 4f, C("#2a1f12"), C("#a8843a"), 1.5f);
                RectTransform inner = Rect("Inner", face); // the double frame of the design
                Stretch(inner, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                Box(inner, 2f, new Color(0f, 0f, 0f, 0f), A(C("#a8843a"), 0.45f), 1f);
                TMP_Text title = T(plaque, Lang.T("КНИГА ВОРОНІВ"), 28f, Gold, F.Head, TextAlignmentOptions.Center);
                title.characterSpacing = 14f; title.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(34f, 1f), new Vector2(-34f, -1f));
                foreach (bool left in new bool[] { true, false }) // ᚺ and ᛗ: Hugin's and Munin's runes at the ends
                {
                    TMP_Text rune = T(plaque, left ? "ᚺ" : "ᛗ", 15f, A(Gold, 0.75f), F.Rune, TextAlignmentOptions.Center);
                    rune.rectTransform.anchorMin = rune.rectTransform.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
                    rune.rectTransform.sizeDelta = new Vector2(24f, 30f);
                    rune.rectTransform.anchoredPosition = new Vector2(left ? 18f : -18f, 0f);
                }

                // the book, tilted back from its lower edge
                _book = Rect("Book", _window);
                _book.anchorMin = _book.anchorMax = new Vector2(0f, 1f);
                _book.pivot = new Vector2(0.5f, 0f);
                _book.sizeDelta = new Vector2(BookW, BookH);
                _book.anchoredPosition = new Vector2(BookX + BookW / 2f, -(BookTop + BookH));
                _book.localRotation = Quaternion.Euler(TiltDeg, 0f, 0f);
                _chrome = Img(_book, "Chrome", BookArt.Picture("chrome"), Color.white);
                At(_chrome.rectTransform, -50f, -50f, 1300f, 780f);
                _pages = Rect("Pages", _book);
                Stretch(_pages, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                // "?" and the close box: round, sitting on the book's two upper corners (on the book: tilted with it)
                RectTransform ask = CornerButton("Ask", -4f, null);
                TMP_Text q = T(ask, "?", 24f, Gold, F.Head, TextAlignmentOptions.Center);
                q.fontStyle = FontStyles.Bold;
                Stretch(q.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -1f));
                Clickable(ask.GetChild(1).gameObject, ToggleHelp);
                RectTransform close = CornerButton("Close", BookW + 4f, BookArt.Cross);
                Clickable(close.GetChild(1).gameObject, Close);
                BuildMailCorner(); // the post: the envelope beside "?"
                BuildGuideCorner(); // the Guide's tips, beside the envelope

                // a small loose leaf tucked between the pages at the book's lower left corner: drawn under the book, its
                // upper part hidden by the pages, the rest sticking out. On it the Discord server for bugs and ideas
                RectTransform leaf = Rect("Leaf", _window);
                leaf.SetSiblingIndex(_book.GetSiblingIndex());
                At(leaf, 6f, BookTop + BookH - 40f, 236f, 106f);
                leaf.localRotation = Quaternion.Euler(0f, 0f, 3f);
                Image ls = Img(leaf, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.4f));
                Stretch(ls.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, -12f), new Vector2(10f, -30f));
                RectTransform sheet = Rect("Sheet", leaf);
                Stretch(sheet, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Box(sheet, 2f, C("#e9dab4"), A(C("#8a6a3a"), 0.45f), 1f);
                for (int i = 0; i < 3; i++) // the pages' shade where the leaf comes out from under them
                {
                    Image shade = Img(sheet, "Shade", null, new Color(0.23f, 0.14f, 0.08f, 0.16f - i * 0.05f));
                    Stretch(shade.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(1f, -40f - (i + 1) * 5f), new Vector2(-1f, -40f - i * 5f));
                }
                TMP_Text lt = T(sheet, Lang.T("ПРОПОЗИЦІЇ ТА ПОМИЛКИ"), 13f, A(Brown, 0.85f), F.Head, TextAlignmentOptions.Center);
                lt.characterSpacing = 3f; lt.textWrappingMode = TextWrappingModes.NoWrap;
                Shrink(lt, 10f);
                Stretch(lt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 34f), new Vector2(-10f, 54f));
                TMP_Text ll = T(sheet, "<u>" + Lang.T("Пиши нам у Discord") + "</u>", 17f, GoldInk, F.Body, TextAlignmentOptions.Center);
                ll.textWrappingMode = TextWrappingModes.NoWrap;
                float lw = ll.GetPreferredValues().x;
                if (lw > 212f) { ll.fontSize = Mathf.Max(12f, 17f * 212f / lw); lw = ll.GetPreferredValues().x; } // (a longer language: smaller)
                ll.rectTransform.anchorMin = ll.rectTransform.anchorMax = ll.rectTransform.pivot = new Vector2(0.5f, 0f);
                ll.rectTransform.sizeDelta = new Vector2(Mathf.Min(lw, 212f) + 6f, 26f); // only the words catch the cursor
                ll.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                sheet.GetComponent<Image>().raycastTarget = false; // (the leaf itself is no button: its hidden top under the book neither)
                ll.raycastTarget = true;
                BookLink link = ll.gameObject.AddComponent<BookLink>();
                link.Rest = GoldInk; link.Click = () => { try { Application.OpenURL(DiscordUrl); } catch { } };
                Tip(ll.gameObject, () => Lang.T("Відкриє наш сервер RavenWatch у Discord: пиши туди про баги, ідеї й помилки в словах воронів."));
                _leaf = sheet;

                // the tabs, over the book's upper edge
                _tabsBar = Rect("Tabs", _window);
                At(_tabsBar, 0f, 118f, W, 44f);
                HorizontalLayoutGroup hb = HBox(_tabsBar, 8f, TextAnchor.LowerCenter);
                hb.childControlHeight = false; hb.childControlWidth = false;
                _tabs = new RectTransform[TabNames.Length];
                _tabH = new float[TabNames.Length];
                _tabDots = new Image[TabNames.Length];
                for (int i = 0; i < TabNames.Length; i++)
                {
                    int tab = i;
                    RectTransform t = Rect("Tab", _tabsBar);
                    t.sizeDelta = new Vector2(150f, 36f);
                    Image bg = t.gameObject.AddComponent<Image>();
                    bg.sprite = BookArt.RoundFine(10f, true); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = BookArt.Fine;
                    TMP_Text name = T(t, TabNames[i], 18f, C("#d8c497"), F.Head, TextAlignmentOptions.Top);
                    Shrink(name, 12f); // a longer word (another language) gets smaller, never out of the tab
                    Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, -7f));
                    Image dot = Img(t, "Dot", BookArt.Circle, Gold);
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(1f, 1f);
                    dot.rectTransform.sizeDelta = new Vector2(11f, 11f);
                    dot.rectTransform.anchoredPosition = new Vector2(4f, 4f);
                    dot.gameObject.SetActive(false);
                    Button b = t.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    Tint(b);
                    b.onClick.AddListener(() => { bool was = _capturing != null; _capturing = null; if (tab != _tab) Show(tab); else if (was) Show(_tab, true); }); // the open tab again: nothing to turn (a key wait just ends)
                    _tabs[i] = t; _tabDots[i] = dot; _tabH[i] = 36f;
                    Tip(t.gameObject, () => TabWhy(tab));
                }

                // the line under the book: hints or the keys, and «Закрити»
                _footer = Rect("Footer", _window);
                _footer.anchorMin = new Vector2(0f, 0f); _footer.anchorMax = new Vector2(1f, 0f); _footer.pivot = new Vector2(0.5f, 0f);
                _footer.offsetMin = new Vector2(262f, 8f); _footer.offsetMax = new Vector2(-28f, 8f + 46f); // (left of it: the leaf)
                HBox(_footer, 14f, TextAnchor.MiddleLeft);

                ask.SetAsLastSibling(); close.SetAsLastSibling(); // over the book's chrome and pages

                // the popups' layer: the book's plane (its pivot and tilt), over the tabs and the footer; in its own units
                // (0, 0) is the middle of the book's lower edge, y up
                _over = Rect("Over book", _window);
                _over.anchorMin = _over.anchorMax = new Vector2(0f, 1f);
                _over.pivot = new Vector2(0.5f, 0f);
                _over.sizeDelta = new Vector2(W, OverH);
                _over.anchoredPosition = _book.anchoredPosition;
                _over.localRotation = _book.localRotation;

                // "?": what this tab is about, over the book
                _help = Rect("Help", _over);
                _help.anchorMin = _help.anchorMax = new Vector2(0.5f, 0f);
                _help.pivot = new Vector2(0.5f, 0.5f);
                _help.sizeDelta = new Vector2(W - 240f, 600f);
                _help.anchoredPosition = new Vector2(0f, BookTop + BookH - 150f - 300f);
                Box(_help, 8f, A(BookArt.TipBg, 0.97f), Gold, 1f);
                _helpText = T(_help, "", 17f, BookArt.TipInk, F.Body, TextAlignmentOptions.TopLeft);
                Stretch(_helpText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 24f), new Vector2(-28f, -24f));
                ClickArea(_help.gameObject, () => _help.gameObject.SetActive(false));
                // a small × in its upper right corner (a click anywhere on it closes it too)
                RectTransform helpX = Rect("Close", _help);
                helpX.anchorMin = helpX.anchorMax = helpX.pivot = new Vector2(1f, 1f);
                helpX.sizeDelta = new Vector2(26f, 26f);
                helpX.anchoredPosition = new Vector2(-12f, -12f);
                Image helpXi = Img(helpX, "Cross", BookArt.Cross, Muted);
                Stretch(helpXi.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                Clickable(helpX.gameObject, () => _help.gameObject.SetActive(false));
                Tip(helpX.gameObject, () => Lang.T("Закрити"));
                _help.gameObject.SetActive(false);

                // the tooltip, over everything, on the book's plane
                _tip = Rect("Tip", _over);
                _tip.pivot = new Vector2(0f, 1f);
                _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 0f);
                Box(_tip, 6f, BookArt.TipBg, Gold, 1f);
                foreach (Image i in _tip.GetComponentsInChildren<Image>(true)) i.raycastTarget = false;
                _tipText = T(_tip, "", 15f, BookArt.TipInk, F.Body, TextAlignmentOptions.TopLeft);
                Stretch(_tipText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 12f), new Vector2(-14f, -12f));
                _tip.gameObject.SetActive(false);

                _stage.SetActive(false);
                _root.SetActive(false);
            }
            catch (Exception e)
            {
                if (Plugin.Instance != null) Plugin.Instance.Log("Book of ravens could not be built: " + e);
                if (_root != null) UnityEngine.Object.Destroy(_root);
                if (_stage != null) UnityEngine.Object.Destroy(_stage);
                _root = null; _stage = null; _cam = null;
            }
        }

        // a round button on an upper corner of the book (its middle at x of the book, just over its upper edge): a soft
        // shadow, a gold ring, dark inside; child 1 is the ring, the part that takes the click; an icon in it if given
        private static RectTransform CornerButton(string name, float x, Sprite icon)
        {
            const float size = 44f;
            RectTransform r = Rect(name, _book);
            At(r, x - size / 2f, -2f - size / 2f, size, size);
            Image sh = Img(r, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.5f));
            Stretch(sh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-7f, -10f), new Vector2(7f, 3f));
            Image ring = Img(r, "Ring", BookArt.Circle, C("#a8843a"));
            ring.raycastTarget = true;
            Image face = Img(ring.transform, "Face", BookArt.Circle, C("#241a10"));
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f));
            if (icon != null)
            {
                Image ic = Img(ring.transform, "Icon", icon, Gold);
                Stretch(ic.rectTransform, Vector2.zero, Vector2.one, new Vector2(13f, 13f), new Vector2(-13f, -13f));
            }
            return r;
        }

        // ---------------------------------------------------------------- the turn of a tab
        // Only the tabs move: the chosen one grows and the old one sinks, smoothly. The page itself just appears (no
        // fade, no slide), and its bars stand at their values at once — they paint in only when the book is opened.

        private static RectTransform _leaf;
        private static float[] _tabH;
        private static bool _barsAtOnce; // the page being drawn came from another tab: its bars skip the paint-in

        private static void Animate()
        {
            if (_tabs != null && _tabH != null)
                for (int i = 0; i < _tabs.Length; i++)
                {
                    Vector2 sd = _tabs[i].sizeDelta;
                    if (Mathf.Abs(sd.y - _tabH[i]) < 0.05f) continue;
                    _tabs[i].sizeDelta = new Vector2(sd.x, Mathf.MoveTowards(sd.y, _tabH[i], Time.unscaledDeltaTime * 60f));
                }
        }

        private static void ToggleHelp()
        {
            if (_help == null) return;
            bool on = !_help.gameObject.activeSelf;
            _help.gameObject.SetActive(on);
            if (on) { _helpText.text = HelpFor(_tab); FitHelp(); _help.SetAsLastSibling(); }
        }

        // the card as tall as its words, up to the book's height; still more words: a smaller letter (never past the card)
        private static void FitHelp()
        {
            const float padX = 56f, padY = 48f, most = BookH - 20f;
            float width = _help.sizeDelta.x - padX;
            _helpText.enableAutoSizing = false;
            _helpText.fontSize = 17f;
            float need = _helpText.GetPreferredValues(_helpText.text, width, 0f).y + padY;
            if (need > most)
            {
                _helpText.enableAutoSizing = true;
                _helpText.fontSizeMin = 12f;
                _helpText.fontSizeMax = 17f;
            }
            _help.sizeDelta = new Vector2(_help.sizeDelta.x, Mathf.Clamp(need, 200f, most));
            _help.anchoredPosition = new Vector2(0f, BookTop + BookH * 0.5f); // in the middle of the book
        }

        // ---------------------------------------------------------------- pages

        private static void Show(int tab) { Show(tab, false); }
        // something changed from outside (a thing put on after the hero's move): the open page again
        internal static void Again() { if (IsOpen) Show(_tab, true); }

        private static void Show(int tab, bool refresh)
        {
            if (_pages == null) return;
            if (Dressing(_tab) && !Dressing(tab)) LeaveOutfits();
            if (tab == FitTab && !Outfits.TuningAllowed) tab = OutfitTab;
            _barsAtOnce = !refresh && tab != _tab; // another tab (not the book opening): its bars stand at once
            Dictionary<string, float> scroll = refresh && tab == _tab ? KeepScroll() : null; // drawn anew: the columns stay where they were scrolled
            _tab = tab;
            Outfits.PlayerView = tab == OutfitTab;
            if (_tabs.Length > FitTab) _tabs[FitTab].gameObject.SetActive(Outfits.TuningAllowed);
            HideTip();
            if (!refresh && _help != null) _help.gameObject.SetActive(false);
            bool? feeding = refresh ? _feedFor : null; // the food list stays open over a page drawn anew (a meal changes it)
            Vector2 feedAt = _feedAt;
            float feedScroll = feeding.HasValue ? PopupScroll() : 0f;
            ClosePopup();
            Live.Clear();
            if (!refresh) { BookBar.Last.Clear(); BookBar.LastWidth.Clear(); _nickKind[0] = _nickKind[1] = -1; } // opened: the names on the chosen one's page // opened: the strokes paint in from nothing; redrawn: they go on
            if (!Dressing(tab)) OutfitStage.Close();
            if (tab != _runesDrawnFor) { _freshRunes.Clear(); _runesDrawnFor = tab; }
            Clear(_pages);
            Clear(_footer);
            for (int i = 0; i < _tabs.Length; i++) MarkTab(i, i == tab);
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(Player.m_localPlayer, out hugin, out munin);
            try
            {
                if (tab == RavensTab) PageRavens(hugin, munin);
                else if (tab == GiftsTab) PageGifts(hugin, munin);
                else if (tab == GrowTab) PageGrow(hugin, munin);
                else if (tab == RunesTab) PageRunes();
                else PageOutfits(hugin, munin);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Book of ravens page failed: " + e); }
            try { Footer(tab); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Book of ravens footer failed: " + e); }
            if (scroll != null) RestoreScroll(scroll);
            _shown = Snapshot();
            TabDots();
            if (feeding.HasValue && tab == RavensTab && Trust.Worn(feeding.Value) != null) FeedPicker(null, feeding.Value, feedAt, feedScroll);
        }

        private static void Clear(RectTransform r)
        {
            for (int i = r.childCount - 1; i >= 0; i--)
            {
                GameObject old = r.GetChild(i).gameObject;
                old.SetActive(false); // out of any layout now: Destroy waits for the end of the frame
                UnityEngine.Object.Destroy(old);
            }
        }

        // how far each scrolling column of the page is scrolled, by its place in the page (before the page is cleared)
        private static Dictionary<string, float> KeepScroll()
        {
            Dictionary<string, float> d = new Dictionary<string, float>();
            if (_pages == null) return d;
            foreach (ScrollRect sr in _pages.GetComponentsInChildren<ScrollRect>())
                if (sr.content != null && sr.content.anchoredPosition.y > 0f) d[ScrollKey(sr)] = sr.content.anchoredPosition.y;
            return d;
        }

        // ... and back on the page drawn anew (its layout made now, so it is known how far it can go)
        private static void RestoreScroll(Dictionary<string, float> d)
        {
            if (d.Count == 0 || _pages == null) return;
            foreach (ScrollRect sr in _pages.GetComponentsInChildren<ScrollRect>()) // (the old page is inactive: only the new one)
            {
                float y;
                if (sr.content == null || sr.viewport == null || !d.TryGetValue(ScrollKey(sr), out y)) continue;
                LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
                float most = sr.content.rect.height - sr.viewport.rect.height;
                if (most > 1f) sr.content.anchoredPosition = new Vector2(sr.content.anchoredPosition.x, Mathf.Min(y, most));
            }
        }

        private static string ScrollKey(ScrollRect sr)
        {
            string k = "";
            for (Transform t = sr.transform; t != null && t != _pages; t = t.parent) k = t.name + "/" + k;
            return k;
        }

        // how far the open popup's list is scrolled (the food list drawn anew after a meal stays there)
        private static float PopupScroll()
        {
            if (_popup == null) return 0f;
            ScrollRect sr = _popup.GetComponentInChildren<ScrollRect>();
            return sr != null && sr.content != null ? sr.content.anchoredPosition.y : 0f;
        }

        // the active tab stands taller, gold; the others dark
        private static void MarkTab(int i, bool on)
        {
            RectTransform t = _tabs[i];
            if (_tabH != null) _tabH[i] = on ? 44f : 36f; else t.sizeDelta = new Vector2(150f, on ? 44f : 36f); // grows there smoothly (Animate)
            t.GetComponent<Image>().color = on ? Gold : C("#3a2c1c");
            TMP_Text name = t.GetComponentInChildren<TMP_Text>(true);
            if (name != null) name.color = on ? C("#2a1a0c") : C("#d8c497");
        }

        private static void Renew()
        {
            foreach (Action a in Live.ToArray()) { try { a(); } catch { } }
            TabDots();
            if (Snapshot() != _shown) Show(_tab, true);
        }

        // what the open page shows: when any of it changes, the page is drawn again (a meal, a ritual, a raven taken off)
        private static string Snapshot()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "";
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            string s = _tab + "|" + _capturing + "|" + Outfits.Tuning + "|" + (_tab == GrowTab ? _growMunin.ToString() : Dressing(_tab) ? _dressMunin.ToString() : "") + "|";
            Dictionary<string, int> bag = null;
            foreach (ItemDrop.ItemData item in new ItemDrop.ItemData[] { hugin, munin })
            {
                if (item == null) { s += "-;"; continue; }
                bool m = item == munin;
                s += Trust.Level(item) + ":" + Mathf.FloorToInt(Trust.Points(item)) + ":" + Trust.FedToday(item) + ":" + Satiety.Zone(item) + ":" + Satiety.FavKnown(item) + ":" + Trust.Away(item) + ":" + Trust.ShownLevel(m, item) + ":" + Trust.SettledLevel(item)
                    + ":" + Strike.Level(item) + ":" + Strike.Flights(item) + ":" + Strike.Tier(item) + ":" + Norov.Code(item) + ":" + Orders.Of(p, m) + ":" + Perches.On(item) + ":" + Nicks.Snapshot(item);
                string off;
                s += ":" + (RavenSkills.OffShoulder(p, m, out off) ? "out" : "in") + (Shoulder.Present(p, m) ? "+" : "-");
                if (Dressing(_tab)) s += ":" + Outfits.Code(item) + ":" + RavenPouch.Describe(item);
                if (_tab == RavensTab || _tab == GrowTab)
                {
                    Trust.Step step = Trust.NextStep(item);
                    if (bag == null) bag = Bag(p);
                    if (step != null) s += ":" + Count(bag, step.Prefab) + ":" + Trust.AtStones(p) + ":" + Trust.TrophyHung(p, step.Prefab);
                    foreach (Trust.Food f in Trust.Foods(m)) s += "," + Count(bag, f.Prefab);
                }
                if (_tab == GiftsTab) foreach (RavenAbility a in RavenAbilities.All) if (a.Munin == m) s += RavenAbilities.Unlocked(a) ? "u" : "l";
                s += ";";
            }
            if (Dressing(_tab)) s += Bag(p).Count;
            if (_tab == RunesTab)
            {
                bool fm; float fl;
                s += "|n" + Errands.Version + ":" + RuneBook.Version + ":" + WearCatalog.Stamp + ":" + Mathf.CeilToInt(Nest.CooldownLeft(p) / 10f) + ":" + (Nest.Flying(out fm, out fl) ? Mathf.CeilToInt(fl / 5f) : -1);
                ErrandRun er = Errands.Of(p);
                if (er != null && er.Current != null) s += ":" + er.Current.Describe(er, er.Step, p);
                s += ":" + Shoulder.Present(p, false) + Shoulder.Present(p, true) + "|";
            }
            if (Dressing(_tab)) s += "|w" + RuneBook.Version + ":" + WearCatalog.Stamp + "|"; // (the rune places round the raven)
            if (_tab != GiftsTab)
            {
                bool any = false;
                foreach (Backup.Lost l in Backup.Ravens(p))
                {
                    s += l.Id + (l.WithYou ? "+" : "-") + l.DaysLeft + (l.Price != null ? ":" + Trust.Have(p, l.Price.Prefab) : "") + ";";
                    any |= !l.WithYou;
                }
                if (any) s += Trust.AtStones(p); // the return button wakes up / greys out as you come to the stones or pick up the heads
            }
            return s;
        }

        // what the bag holds, by prefab: one pass for all the counts the snapshot needs
        private static Dictionary<string, int> Bag(Player p)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                if (it.m_dropPrefab == null) continue;
                int n;
                d.TryGetValue(it.m_dropPrefab.name, out n);
                d[it.m_dropPrefab.name] = n + it.m_stack;
            }
            return d;
        }

        private static int Count(Dictionary<string, int> bag, string prefab) { int n; return bag.TryGetValue(prefab, out n) ? n : 0; }

        // the dots on the tabs: a red one where a raven is hungry (Ravens: the feeding is there), gold where a ritual can be
        // done right now (Growth) and where new pieces wait (Gear)
        private static void TabDots()
        {
            Player p = Player.m_localPlayer;
            if (p == null || _tabDots == null) return;
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            bool hungry = HungryNow(p, false) || HungryNow(p, true);
            _tabDots[RavensTab].color = BookArt.Munin;
            _tabDots[RavensTab].gameObject.SetActive(hungry);
            _tabDots[GrowTab].gameObject.SetActive(RiteNow(p, false) || RiteNow(p, true));
            Outfits.SeenStart(hugin); Outfits.SeenStart(munin); // once per raven: its old pieces are not "new"
            if (_tabDots.Length > RunesTab) _tabDots[RunesTab].gameObject.SetActive(RuneBook.Of(p).New.Count > 0);
            _tabDots[OutfitTab].gameObject.SetActive(OutfitNews(p, false) || OutfitNews(p, true));
        }

        // something new on «Образ» for this raven: a piece the page shows, or a nickname
        private static bool OutfitNews(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return item != null && (Outfits.AnyNew(p, item) || Nicks.HasNew(item));
        }

        // the gold "new" mark: a bright drop with a dark rim (plain gold is lost on the parchment), top right of its owner;
        // over: how far it sticks out past the corner (negative: inside)
        private static void NewDot(RectTransform owner, float size, float over)
        {
            Image rim = Img(owner, "New", BookArt.Circle, C("#5a3c14"));
            rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rim.raycastTarget = false;
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = rim.rectTransform.pivot = new Vector2(1f, 1f);
            rim.rectTransform.sizeDelta = new Vector2(size, size); rim.rectTransform.anchoredPosition = new Vector2(over, over);
            Image core = Img(rim.rectTransform, "Core", BookArt.Circle, C("#FFD98A"));
            core.raycastTarget = false;
            Stretch(core.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        }

        // the same mark in a row of words, after a heading
        private static void NewDotInline(RectTransform row)
        {
            RectTransform r = Rect("NewMark", row);
            Fixed(r.gameObject, 12f, 22f);
            NewDot(r, 11f, 0f);
        }

        private static string Few(List<string> names)
        {
            if (names.Count <= 4) return string.Join(", ", names.ToArray());
            return Lang.F("{0} і ще {1}", string.Join(", ", names.GetRange(0, 3).ToArray()), names.Count - 3);
        }

        private static bool HungryNow(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return item != null && !Trust.Away(item) && !Trust.Stranger(item, p) && Trust.Hungry(item, p);
        }

        private static bool RiteNow(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null || Trust.Away(item) || Trust.Stranger(item, p)) return false;
            Rite r = RiteFor(p, item);
            return r != null && r.All;
        }

        private static string TabWhy(int tab)
        {
            Player p = Player.m_localPlayer;
            if (p == null || _tabDots == null || !_tabDots[tab].gameObject.activeSelf) return null;
            List<string> l = new List<string>();
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item == null) continue;
                if (tab == RavensTab && HungryNow(p, m))
                    l.Add(Lang.F("<b><color=#FF6B6B>●</color> {0} {1}</b> · ситість {2}/100{3}", Plugin.RavenName(m), Satiety.ZoneWord(item), Mathf.RoundToInt(Satiety.Value(item)), (Trust.Need(item) > 0f && !Trust.Full(item) ? Lang.T(" (зараз їжа дає довіру)") : "")));
                if (tab == GrowTab && RiteNow(p, m))
                    l.Add(Lang.F("<b><color={0}>●</color> {1}</b>: можна провести ритуал — ти біля каменів і все готово", GoldHex, Plugin.RavenName(m)));
                if (tab == OutfitTab)
                {
                    List<string> things = new List<string>(), names = new List<string>();
                    foreach (Outfits.Entry e in Outfits.NewOnPage(p, item)) things.Add(e.Name);
                    foreach (Nicks.Nick n in Nicks.NewOnes(item)) names.Add(n.Name);
                    if (things.Count + names.Count > 0)
                        l.Add(Lang.F("<b><color={0}>●</color> {1}</b>", GoldHex, Plugin.RavenName(m))
                            + (things.Count > 0 ? Lang.F("\nнові речі: {0}", Few(things)) : "")
                            + (names.Count > 0 ? Lang.F("\nнові прізвиська: {0}", Few(names)) : ""));
                }
            }
            if (tab == RunesTab) l.Add(Lang.F("<b><color={0}>●</color> Нові руни</b> · загорілися руни, яких ти ще не бачив.", GoldHex));
            if (tab == OutfitTab && l.Count > 0) l.Add(Lang.T("<i>Золота крапка позначає нове: на вороні, на речі, на прізвиську.</i>"));
            return l.Count > 0 ? string.Join("\n", l.ToArray()) : null;
        }

        // a button: say what came of it; the page is drawn again if something changed
        private static void Act(Func<string> action)
        {
            string message = action();
            Player p = Player.m_localPlayer;
            if (p != null && !string.IsNullOrEmpty(message)) RavenMsg.Center(p, message);
            Show(_tab, true);
        }

        private static void Tell(string message) { Player p = Player.m_localPlayer; if (p != null && !string.IsNullOrEmpty(message)) RavenMsg.Center(p, message); }

        // ---------------------------------------------------------------- the spread
        // Two equal pages, always: Munin's column on the left, Hugin's on the right (a raven not on you: his page says so).

        private static float ColumnX(bool munin) { return munin ? 56f : 644f; }
        private const float ColumnW = 500f;

        // a column of a page, from top down to the frame's end; it scrolls when its content is taller (the wheel)
        private static RectTransform Column(string name, float x, float top, float width, float spacing)
        {
            RectTransform view = Rect(name, _pages);
            At(view, x, top, width, PageBottom - top);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // catches the wheel between the pieces
            RectTransform content = Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VBox(content, spacing, TextAnchor.UpperLeft, 0, 0, 0, 6);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = content; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 160f; sr.inertia = false; // a notch of the wheel: a good step down a long page (50 crawled)
            view.gameObject.AddComponent<BookScrollGate>().Scroll = sr;
            return content;
        }

        // ---------------------------------------------------------------- «Ворони»

        private static void PageRavens(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            foreach (bool m in new bool[] { true, false })
            {
                ItemDrop.ItemData item = m ? munin : hugin;
                RectTransform col = Column(m ? "Munin" : "Hugin", ColumnX(m), 46f, ColumnW, 11f);
                if (item != null) RavenPage(col, m, item);
                else EmptyPage(col, m, true);
            }
        }

        private static void RavenPage(RectTransform col, bool m, ItemDrop.ItemData item)
        {
            Player p = Player.m_localPlayer;
            int level = Trust.Level(item);
            bool stranger = Trust.Stranger(item, p);
            bool away = Trust.Away(item) && !Perches.On(item);

            // the portrait, the name with its initial, the level and what it does now
            RectTransform top = Rect("Top", col);
            HBox(top, 16f, TextAnchor.MiddleLeft);
            Image portrait = Portrait(top, m, 96f);
            if (level == 0) portrait.color = new Color(0.62f, 0.58f, 0.52f, 1f);  // asleep: grey, as a stranger to you
            else if (away) portrait.color = new Color(1f, 1f, 1f, 0.5f);           // flown off: half there
            RectTransform info = Rect("Info", top);
            VBox(info, 2f, TextAnchor.MiddleLeft);
            info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Name(info, m, 34f, stranger ? null : Nicks.Current(item));
            TMP_Text lv = T(info, Lang.F("Рівень {0}/{1} · <color={2}><b>{3}</b></color> · {4}", level, Trust.MaxLevel, GoldInkHex, Trust.Rank(m, level), (m ? Lang.T("ліве плече") : Lang.T("праве плече"))), 15f, Ink, F.Italic);
            lv.textWrappingMode = TextWrappingModes.NoWrap;
            bool badState;
            string state = StateLine(p, m, item, out badState);
            TMP_Text st = T(info, state, 14f, badState ? Bad : BookArt.RavenInk(m), F.Body);
            st.textWrappingMode = TextWrappingModes.NoWrap;

            if (stranger)
                T(col, Lang.F("Чужий ворон: його власник — {0}. Довіри, годування й ритуалів з тобою не буде.", (Trust.OwnerName(item) ?? Lang.T("хтось інший"))), 15f, Bad, F.Italic);

            // the bars: trust, strength, satiety
            RectTransform bars = Rect("Bars", col);
            VBox(bars, 11f, TextAnchor.UpperLeft);
            ItemDrop.ItemData it = item;
            if (level == 0) Meter(bars, Lang.T("Довіра"), () => TrustTip(m, it), () => Lang.T("спить"), () => 0f, "brush-gold", BarKey(m, Lang.T("Довіра")));
            else Meter(bars, Lang.T("Довіра"), () => TrustTip(m, it), () => Trust.Need(it) > 0f ? Mathf.FloorToInt(Trust.Points(it)) + " / " + Mathf.RoundToInt(Trust.Need(it)) : Lang.T("найвища"), () => Trust.Need(it) > 0f ? Trust.Points(it) / Trust.Need(it) : 1f, "brush-gold", BarKey(m, Lang.T("Довіра")));
            Meter(bars, Lang.T("Сила"), () => Lang.F("<b>Сила {0}</b>\n\n{1}", Plugin.RavenNameGen(m), Power.Describe(it, m, true)),
                () => Mathf.FloorToInt(Power.Get(it)) + " / " + Mathf.RoundToInt(Trust.Strength(it)), () => Power.Get(it) / Mathf.Max(1f, Trust.Strength(it)), m ? "brush-munin" : "brush-huginn", BarKey(m, Lang.T("Сила")));
            if (level > 0 && !stranger) SatietyGauge(bars, m, item);

            // «Благо» and «Тягар»
            string bonus, minus;
            RectTransform perks = Rect("Perks", col);
            HorizontalLayoutGroup ph = HBox(perks, 16f, TextAnchor.MiddleLeft);
            ph.childForceExpandWidth = false;
            Fixed(perks.gameObject, -1f, RowPerks);
            if (Perks.Lines(item, out bonus, out minus))
            {
                string bl = Lang.F("<color={0}><b>Благо</b></color> <color={1}>{2}</color>", GoldInkHex, GoodHex, bonus);
                string tl = Lang.F("<color={0}><b>Тягар</b></color> <color={1}>{2}</color>", GoldInkHex, BadHex, minus);
                TMP_Text b = Shrink(T(perks, bl, 15f, Ink, F.Body), 11f);
                b.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Tip(b.gameObject, () => bl + "\n" + tl + "\n\n" + PerksTip());
                TMP_Text t = Shrink(T(perks, tl, 15f, Ink, F.Body), 11f);
                t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Tip(t.gameObject, () => bl + "\n" + tl + "\n\n" + PerksTip());
            }
            else Shrink(T(perks, Lang.T("Благо й тягар відкриються після покупки."), 15f, InkSoft, F.Italic), 11f);

            NorovRow(col, m, item);
            GiftsRow(col, m);
            RuneDivider(col);
            NextCard(col, p, m, item);
        }

        // the raven page's rows: the same height on both pages, whatever they say (a long text shrinks, Shrink)
        private const float RowPerks = 26f, RowNorov = 46f, RowGifts = 32f, RowFoot = 20f, RowNext = 96f;

        private static string PerksTip()
        {
            return Lang.F("<b><color={0}>Благо й тягар</color></b>\n\nКожен ворон при покупці отримує одне випадкове благо (тобі на користь) і один тягар (тобі на шкоду). Не подобається, що випало? <color={1}>Камінь долі</color> (у Хальдора) перекине їх наново — тоді зміниться й тягар.", GoldHex, GoldHex);
        }

        // the name: its first letter as an illuminated initial in a gold frame, the rest in the raven's colour
        private static void Name(Transform parent, bool m, float size, Nicks.Nick nick = null)
        {
            string name = Plugin.RavenName(m);
            RectTransform row = Rect("Name", parent);
            if (name.Length < 2)
            {
                HBox(row, 0f, TextAnchor.LowerLeft);
                TMP_Text whole = T(row, name, size, BookArt.RavenInk(m), F.Head);
                whole.textWrappingMode = TextWrappingModes.NoWrap;
                return;
            }
            // the initial's frame at the row's lower left; the rest set so its baseline is the initial's baseline (by the
            // font's own lines, so a letter with a tail — Д, Щ — or any other language's sits right too)
            RectTransform cap = Rect("Initial", row);
            cap.anchorMin = cap.anchorMax = cap.pivot = Vector2.zero;
            Box(cap, 4f, new Color(0.66f, 0.52f, 0.23f, 0.12f), new Color(0.66f, 0.52f, 0.23f, 0.6f), 1.5f);
            TMP_Text first = T(cap, name.Substring(0, 1), size * 1.7f, BookArt.Initial, F.Head, TextAlignmentOptions.Center);
            first.textWrappingMode = TextWrappingModes.NoWrap;
            float baseline = FitAround(cap, first, 6f, 4f, 1.5f); // little room under: the frame ends just below the name's line
            TMP_Text rest = T(row, name.Substring(1), size, BookArt.RavenInk(m), F.Head, TextAlignmentOptions.BottomLeft);
            rest.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform rr = rest.rectTransform;
            rr.anchorMin = rr.anchorMax = rr.pivot = Vector2.zero;
            rr.sizeDelta = new Vector2(Mathf.Ceil(rest.GetPreferredValues().x) + 4f, size * 1.6f);
            rest.ForceMeshUpdate();
            float own = rest.textInfo != null && rest.textInfo.lineCount > 0 ? rest.textInfo.lineInfo[0].baseline : size * 0.3f; // its baseline over its own bottom
            rr.anchoredPosition = new Vector2(cap.sizeDelta.x + 2f, baseline - own);
            float width = cap.sizeDelta.x + 2f + rr.sizeDelta.x;
            if (nick != null) // the nickname to the right, small and quiet, on the same line
            {
                TMP_Text nk = T(row, nick.Name, Mathf.Round(size * 0.5f), A(C(Nicks.PaperHex(nick.K)), 0.75f), F.Italic, TextAlignmentOptions.BottomLeft);
                nk.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform nr = nk.rectTransform;
                nr.anchorMin = nr.anchorMax = nr.pivot = Vector2.zero;
                nr.sizeDelta = new Vector2(Mathf.Ceil(nk.GetPreferredValues().x) + 4f, size);
                nk.ForceMeshUpdate();
                float nb = nk.textInfo != null && nk.textInfo.lineCount > 0 ? nk.textInfo.lineInfo[0].baseline : size * 0.2f;
                nr.anchoredPosition = new Vector2(width + 10f, baseline - nb);
                width += 10f + nr.sizeDelta.x;
                Nicks.Nick n = nick;
                Tip(nk.gameObject, () => NickTip(n, true, false, false, null)); // (the chosen one: no price to tell)
            }
            Fixed(row.gameObject, width, cap.sizeDelta.y);
        }

        // A frame round one big letter (any letter of any language): sized by what the glyph really covers when drawn
        // (its ink, not the font's line), the letter put in its middle, this much room at the sides and over and under it.
        // (returns how high the letter's baseline is over the frame's lower edge)
        private static float FitAround(RectTransform frame, TMP_Text letter, float side, float over) { return FitAround(frame, letter, side, over, over); }

        // ... with its own room over the letter and under it (the name's initial: little under, so its frame ends just
        // below the line the name stands on)
        private static float FitAround(RectTransform frame, TMP_Text letter, float side, float over, float under)
        {
            RectTransform lr = letter.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(letter.fontSize * 2f, letter.fontSize * 2f); // room for any glyph to be laid out
            letter.ForceMeshUpdate();
            Bounds b = letter.textBounds;
            Vector2 ink = b.size.x > 0.5f && b.size.y > 0.5f ? new Vector2(b.size.x, b.size.y) : letter.GetPreferredValues(); // (nothing drawn: the line's size)
            Vector2 mid = b.size.x > 0.5f && b.size.y > 0.5f ? new Vector2(b.center.x, b.center.y) : Vector2.zero;
            float shift = (under - over) / 2f; // the ink's middle this far over the frame's middle (more room over it: lower)
            lr.anchoredPosition = -mid + new Vector2(0f, shift);
            Vector2 size = new Vector2(Mathf.Ceil(ink.x + side * 2f), Mathf.Ceil(ink.y + over + under));
            frame.sizeDelta = size;
            Fixed(frame.gameObject, size.x, size.y);
            float baseline = letter.textInfo != null && letter.textInfo.lineCount > 0 ? letter.textInfo.lineInfo[0].baseline : b.min.y;
            return baseline - mid.y + shift + size.y / 2f;
        }

        // what the raven is doing, under his name; bad: said in red (asleep, hungry, flown off, a stranger)
        private static string StateLine(Player p, bool m, ItemDrop.ItemData item, out bool bad)
        {
            bad = false;
            if (Trust.Stranger(item, p)) { bad = true; return Lang.F("● чужий — власник {0}", (Trust.OwnerName(item) ?? Lang.T("інший"))); }
            if (!Trust.Active(item)) { bad = true; return Lang.T("● спить · довіра 0"); }
            if (Perches.OnSpot(item)) return "● " + (Grammar.Female(p) ? Lang.T("сидить, де ти показала") : Lang.T("сидить, де ти показав"));
            if (Perches.On(item)) return Lang.F("● сидить на жердині · {0}", Perches.DayText(item));
            if (Trust.Away(item)) { bad = true; return Trust.AwayWhy(item) == Post.Why ? "● " + Trust.AwayWord(m, item) : Lang.F("● {0} — повернеться за ~{1}", Trust.AwayWord(m, item), RavenAbilities.Span(Trust.AwayLeft(item))); } // (the post: no clock, it goes as the post goes)
            int zone = Satiety.Zone(item);
            if (zone <= Satiety.Hungry) { bad = true; return Lang.F("● {0} — накази не виконує", Satiety.ZoneWords[zone]); }
            if (p != null && Strike.Busy(p, m)) return Lang.T("● в атаці");
            int o = Orders.Of(p, m);
            if (o != Orders.None && o > 0 && o < Orders.Names.Length) return Lang.F("● виконує «{0}»", Orders.Names[o]);
            string off;
            if (RavenSkills.OffShoulder(p, m, out off)) return "● " + off;
            return Lang.T("● на плечі");
        }

        // a raven's portrait in its rings (#3a2414, the paper band, #3a2414), as round as a seal
        private static Image Portrait(Transform parent, bool m, float size)
        {
            RectTransform r = Rect("Portrait", parent);
            Fixed(r.gameObject, size + 12f, size + 12f);
            Img(r, "Ring", BookArt.Circle, Ink);
            Image band = Img(r, "Band", BookArt.Circle, C("#d8c497"));
            Stretch(band.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            Image inner = Img(r, "Inner", BookArt.Circle, Ink);
            Stretch(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image pic = Img(r, "Raven", BookArt.Portrait(m) ?? RavenItem.Icon(m), Color.white);
            Stretch(pic.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            return pic;
        }

        // «Норов»: the raven's traits — a framed letter (or the trait's picture) and its name, each told in full on hover
        private static void NorovRow(Transform col, bool m, ItemDrop.ItemData item)
        {
            if (item == null || Trust.Stranger(item, Player.m_localPlayer)) return;
            RectTransform row = Rect("Norov", col);
            HorizontalLayoutGroup h = HBox(row, 8f, TextAnchor.MiddleLeft);
            h.childForceExpandHeight = true;
            Fixed(row.gameObject, -1f, RowNorov);
            TMP_Text l = T(row, Lang.T("Норов"), 14f, Brown, F.Body, TextAlignmentOptions.MidlineLeft);
            Fixed(l.gameObject, 58f, -1f);
            Norov.Character c = Norov.CharOf(item);
            Norov.Knack k = Norov.KnackOf(item);
            Norov.Quirk q = Norov.QuirkOf(item);
            if (c == null || k == null || q == null) { T(row, Lang.T("відкриється після покупки"), 14f, InkSoft, F.Italic, TextAlignmentOptions.MidlineLeft); Spring(row); return; }
            ItemDrop.ItemData it = item;
            // the voice comes with the character: a quiet «· голос» on it, only while the ravens really speak aloud
            bool voice = RavenVoice.Available;
            Trait(row, 0, c.Id, c.Name + (voice ? " <size=78%><color=#8a6420>· " + Lang.T("голос") + "</color></size>" : ""),
                () => Norov.CharTip(it) + (RavenVoice.Available ? "\n\n" + Lang.T("Говорить уголос — голосом свого характеру.") : ""));
            Trait(row, 1, k.Id, k.Name(m), () => Norov.KnackTip(it, m));
            Trait(row, 2, q.Id, q.Name + (Quirks.SulkLeft(item) > 0f ? Lang.F(" <color={0}>(ображений)</color>", BadHex) : ""), () => Norov.QuirkTip(it, m));
        }

        // the three kinds of trait: caption, colour, mark
        private static string[] TraitCaption { get { return Lang.Arr(TraitCaptionSrc); } } // (translated on every read)
        private static readonly string[] TraitCaptionSrc = Lang.Keys("Характер", "Хист", "Примха");
        private static readonly Color[] TraitInk = { C("#8a6420"), C("#2f5a2a"), C("#8E1E1E") };

        // one trait's card: a stripe in its colour down the left, a pale tint of it, its caption, a round mark and the name
        private static void Trait(RectTransform row, int kind, string id, string name, Func<string> tip)
        {
            Color ink = TraitInk[kind];
            RectTransform card = Rect("Trait", row);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Box(card, 5f, A(ink, 0.07f), A(ink, 0.35f), 1f);
            Image stripe = Img(card, "Stripe", BookArt.Stripe, ink); // soft-edged (the tilt made a hard thin box ragged)
            stripe.type = Image.Type.Sliced;
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f); stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.offsetMin = new Vector2(-1f, 1f); stripe.rectTransform.offsetMax = new Vector2(5f, -1f); // ~4 of ink, its clear sides outside
            VBox(card, 1f, TextAnchor.MiddleLeft, 10, 6, 3, 4);
            Shrink(T(card, TraitCaption[kind], 13f, ink, F.Body), 10f);
            RectTransform line = Rect("Line", card);
            HBox(line, 6f, TextAnchor.MiddleLeft);
            RectTransform mark = Rect("Mark", line);
            Fixed(mark.gameObject, 18f, 18f);
            Sprite sp = Norov.Icon(id);
            if (sp != null) Img(mark, "Icon", sp, Color.white).preserveAspect = true; // its own picture when one is drawn
            else TraitMark(mark, kind, ink);
            TMP_Text n = Shrink(T(line, "<b>" + name + "</b>", 14f, ink, F.Body), 10f);
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Tip(card.gameObject, tip);
        }

        // the round mark: a ring with ◆ (character), + (knack) or − (quirk) in it, drawn (no font has to have them)
        private static void TraitMark(RectTransform mark, int kind, Color ink)
        {
            Img(mark, "Ring", BookArt.Circle, ink);
            Image paper = Img(mark, "Paper", BookArt.Circle, C("#efe2c2"));
            Stretch(paper.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
            if (kind == 0)
            {
                Image d = Img(mark, "Diamond", null, ink);
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(6.5f, 6.5f); d.rectTransform.anchoredPosition = Vector2.zero;
                d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                return;
            }
            Image a = Img(mark, "Across", null, ink);
            a.rectTransform.anchorMin = a.rectTransform.anchorMax = a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            a.rectTransform.sizeDelta = new Vector2(8f, 1.6f); a.rectTransform.anchoredPosition = Vector2.zero;
            if (kind != 1) return;
            Image u = Img(mark, "Up", null, ink);
            u.rectTransform.anchorMin = u.rectTransform.anchorMax = u.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            u.rectTransform.sizeDelta = new Vector2(1.6f, 8f); u.rectTransform.anchoredPosition = Vector2.zero;
        }

        // the open gifts, small; a click goes to the Gifts tab
        private static void GiftsRow(Transform col, bool m)
        {
            RectTransform row = Rect("Gifts", col);
            HBox(row, 6f, TextAnchor.MiddleLeft);
            Fixed(row.gameObject, -1f, RowGifts);
            TMP_Text l = T(row, Lang.T("Дари"), 14f, Brown, F.Body);
            Fixed(l.gameObject, 58f, -1f);
            List<RavenAbility> open = new List<RavenAbility>();
            foreach (RavenAbility a in BookOrder()) if (a.Munin == m && !RavenAbilities.Stub(a) && RavenAbilities.Unlocked(a) && !a.Passive) open.Add(a);
            const float size = 30f;
            foreach (RavenAbility a in open)
            {
                RavenAbility ab = a;
                RectTransform r = Rect("Gift", row);
                Fixed(r.gameObject, size, size);
                Img(r, "Ring", BookArt.Circle, Ink);
                Image ic = Img(r, "Icon", AbilityIcons.For(a.Id, a.Munin), Color.white);
                Stretch(ic.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
                ic.preserveAspect = true;
                Tip(r.gameObject, () => GiftTip(ab));
                Clickable(r.gameObject, () => Show(GiftsTab));
                Action dim = () => { if (ic != null) ic.color = new Color(1f, 1f, 1f, ab.Passive || RavenSkills.Dot(ab) == RavenSkills.DotReady ? 1f : 0.45f); };
                dim(); // now, not on the next second's renew: a page drawn anew must not blink
                Live.Add(dim);
            }
            if (open.Count == 0) T(row, Lang.T("ще жодного"), 14f, InkSoft, F.Italic);
            Spring(row);
            TMP_Text ready = T(row, "", 14f, Good, F.Body, TextAlignmentOptions.MidlineRight);
            ready.textWrappingMode = TextWrappingModes.NoWrap;
            Action count = () =>
            {
                if (ready == null) return;
                int n = 0; foreach (RavenAbility a in open) if (RavenSkills.Dot(a) == RavenSkills.DotReady) n++;
                ready.text = open.Count == 0 ? "" : Lang.N(n, "готовий|готові|готові");
            };
            count();
            Live.Add(count);
        }

        // ── ᚱᚨᚢᛖᚾ ── between the raven and what comes next
        private static void RuneDivider(Transform col)
        {
            RectTransform row = Rect("Runes", col);
            HBox(row, 10f, TextAnchor.MiddleCenter, 0, 0, 4, 0);
            Image a = Img(row, "Line", BookArt.Fade(true), new Color(0.227f, 0.141f, 0.078f, 0.5f));
            Fixed(a.gameObject, -1f, 1f); a.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text t = T(row, "ᚱᚨᚢᛖᚾ", 15f, new Color(0.478f, 0.353f, 0.196f, 0.7f), F.Rune, TextAlignmentOptions.Center);
            t.characterSpacing = 40f; t.textWrappingMode = TextWrappingModes.NoWrap;
            Image b = Img(row, "Line", BookArt.Fade(false), new Color(0.227f, 0.141f, 0.078f, 0.5f));
            Fixed(b.gameObject, -1f, 1f); b.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void DashedLine(Transform col)
        {
            Image d = Img(col, "Dashes", BookArt.Dash(true), new Color(0.227f, 0.141f, 0.078f, 0.45f));
            d.type = Image.Type.Tiled;
            Fixed(d.gameObject, -1f, 4f);
        }

        // what trust is, short (the full story is behind the window's "?" on the Growth tab)
        private static string TrustTip(bool m, ItemDrop.ItemData item)
        {
            int level = Trust.Level(item);
            return Lang.F("<b><color={0}>Довіра {1}</color></b> · рівень {2} із {3}\n\nРосте, коли годуєш голодного ворона (улюблена їжа — удвічі), коли питаєш його думку (ви ж розмовляєте, до 10 на день) і коли ви разом живете — у мирі й у бою. Коли шкала повна, проведи ритуал біля Жертовних каменів — ворон перейде на новий рівень: більше сили, сильніші дари, нове вбрання.\n\nТане, коли ти гинеш, знімаєш ворона з плеча чи він голодує (ситість 0).", GoldHex, Plugin.RavenNameGen(m), level, Trust.MaxLevel);
        }

        // ---------------------------------------------------------------- the brush bars

        // a gold label (its story under the cursor), the numbers on the right, the stroke under them
        private static void Meter(Transform parent, string label, Func<string> tip, Func<string> numbers, Func<float> value, string brush, string key = null)
        {
            RectTransform box = Rect("Meter", parent);
            VBox(box, 3f, TextAnchor.UpperLeft);
            RectTransform head = Rect("Head", box);
            HBox(head, 6f, TextAnchor.MiddleLeft);
            TMP_Text l = T(head, "<b>" + label + "</b>", 15f, GoldInk, F.Body);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            if (tip != null) Tip(l.gameObject, tip);
            Spring(head);
            TMP_Text n = T(head, numbers(), 15f, Ink, F.Body, TextAlignmentOptions.MidlineRight);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            Live.Add(() => { if (n != null) n.text = numbers(); });
            BrushBar(box, value, brush, 7f, 11f, null, key);
        }

        // a faint line along the bottom of the room and the stroke over it (left −2, bottom −3, as the design lays it)
        private static BookBar BrushBar(Transform parent, Func<float> value, string brush, float height, float stroke, Func<Sprite> strokeNow, string key = null)
        {
            RectTransform zone = Rect("Bar", parent);
            Fixed(zone.gameObject, -1f, height);
            Image line = Img(zone, "Line", BookArt.Picture("track-line"), Color.white);
            line.rectTransform.anchorMin = new Vector2(0f, 0f); line.rectTransform.anchorMax = new Vector2(1f, 0f); line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f); line.rectTransform.anchoredPosition = new Vector2(0f, -1f);
            return Stroke(zone, value, brush, stroke, new Vector2(-2f, -3f), strokeNow, key);
        }

        private static BookBar Stroke(RectTransform zone, Func<float> value, string brush, float stroke, Vector2 at, Func<Sprite> strokeNow, string key = null)
        {
            RectTransform fill = Rect("Stroke", zone);
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 0f);
            fill.anchoredPosition = at;
            fill.sizeDelta = new Vector2(0f, stroke);
            Image fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = BookArt.Brush(brush); fi.type = Image.Type.Sliced; fi.pixelsPerUnitMultiplier = 28f / stroke; fi.raycastTarget = false;
            BookBar b = zone.gameObject.AddComponent<BookBar>();
            b.Value = value; b.Fill = fill; b.FillImage = fi; b.Stroke = strokeNow; b.Key = key; b.AtOnce = _barsAtOnce;
            b.Resume();
            return b;
        }

        // a bar's name across page redraws (the same raven, tab and bar)
        private static string BarKey(bool munin, string what) { return _tab + ":" + (munin ? "m" : "h") + ":" + what; }

        // ---------------------------------------------------------------- satiety
        // The zones faint under the stroke (hungry, asking, fed, stuffed), the stroke in the colour of the zone it is in,
        // the mark at 15 (below it no orders), then the favourite food and what to feed.

        private static readonly float[] ZoneEdges = { 0f, Satiety.HungryAt, Satiety.AskAt, Satiety.StuffedAt, 100f };
        private static readonly string[] ZoneTrack = { "#8E1E1E", "#c08a3a", "#7a9a4a", "#7a5a8a" };

        private static void SatietyGauge(Transform parent, bool m, ItemDrop.ItemData item)
        {
            RectTransform box = Rect("Satiety", parent);
            VBox(box, 3f, TextAnchor.UpperLeft);
            RectTransform head = Rect("Head", box);
            HBox(head, 6f, TextAnchor.MiddleLeft);
            TMP_Text l = T(head, Lang.T("<b>Ситість</b>"), 15f, GoldInk, F.Body);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            Tip(l.gameObject, () => HungerTip(m, item));
            Spring(head);
            TMP_Text num = T(head, "", 15f, Ink, F.Body, TextAlignmentOptions.MidlineRight);
            num.textWrappingMode = TextWrappingModes.NoWrap;

            ItemDrop.ItemData sat = item;
            Func<float> value = () => Satiety.Value(sat) / 100f;
            Func<Sprite> stroke = () => BookArt.Brush(ZoneBrush[Satiety.Zone(sat)]);
            {
                RectTransform bar = Rect("Bar", box);
                Fixed(bar.gameObject, -1f, 30f);
                RectTransform track = Rect("Track", bar);
                track.anchorMin = new Vector2(0f, 1f); track.anchorMax = new Vector2(1f, 1f); track.pivot = new Vector2(0.5f, 1f);
                track.sizeDelta = new Vector2(0f, 12f); track.anchoredPosition = new Vector2(0f, -1f);
                // the zones, 3 apart, their widths their share of 0–100
                for (int i = 0; i < 4; i++)
                {
                    Image seg = Img(track, "Zone", BookArt.Round(2f), A(C(ZoneTrack[i]), 0.55f));
                    seg.type = Image.Type.Sliced;
                    seg.rectTransform.anchorMin = new Vector2(ZoneEdges[i] / 100f, 0.5f); seg.rectTransform.anchorMax = new Vector2(ZoneEdges[i + 1] / 100f, 0.5f);
                    seg.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 1.5f, -3f); seg.rectTransform.offsetMax = new Vector2(i == 3 ? 0f : -1.5f, 3f);
                    seg.raycastTarget = true;
                    int z = i == 0 ? Satiety.Hungry : i + 1;
                    Tip(seg.gameObject, () => "<b><color=" + Satiety.ZoneHex[z] + ">" + Satiety.ZoneWords[z] + "</color></b>\n" + Satiety.ZoneText(z));
                }
                Stroke(track, value, ZoneBrush[Satiety.Zone(item)], 10f, new Vector2(-2f, 1f), stroke, BarKey(m, Lang.T("Ситість")));
                // the mark at 15: below it no orders
                // an inked diamond just over the bar and a quill's stroke down through it (BookArt.Marker)
                Image tick = Img(bar, "Mark", BookArt.Marker, A(Ink, 0.92f));
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(Satiety.HungryAt / 100f, 1f);
                tick.rectTransform.pivot = new Vector2(0.5f, 1f);
                tick.rectTransform.sizeDelta = new Vector2(8f, 24f); tick.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                TMP_Text fifteen = T(bar, RavenAbilities.R(Satiety.HungryAt), 13f, Brown, F.Body, TextAlignmentOptions.Top);
                fifteen.rectTransform.anchorMin = fifteen.rectTransform.anchorMax = new Vector2(Satiety.HungryAt / 100f, 1f);
                fifteen.rectTransform.pivot = new Vector2(0.5f, 1f);
                fifteen.rectTransform.sizeDelta = new Vector2(40f, 16f); fifteen.rectTransform.anchoredPosition = new Vector2(0f, -17f);
                Tip(tick.gameObject, () => Lang.F("<b>{0}</b> — нижче ворон голодний і не виконує наказів.", RavenAbilities.R(Satiety.HungryAt)));
            }

            // under it: the favourite food so far (or how long a fed raven strikes harder) and «Чим нагодувати»
            RectTransform foot = Rect("Foot", box);
            HBox(foot, 10f, TextAnchor.MiddleLeft);
            Fixed(foot.gameObject, -1f, RowFoot);
            TMP_Text fav = Shrink(T(foot, "", 14f, InkSoft, F.Italic), 11f);
            fav.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Tip(fav.gameObject, () => Satiety.FavLine(item, m));
            TMP_Text feed = T(foot, Lang.T("<b>Чим нагодувати</b>"), 14f, GoldInk, F.Body, TextAlignmentOptions.MidlineRight);
            feed.textWrappingMode = TextWrappingModes.NoWrap;
            ItemDrop.ItemData it = item;
            TipIcons(feed.gameObject, () => FoodMenuTip(m, it) + "\n\n" + (FeedWhyNot(m, it) ?? Lang.F("<color={0}>Клік — вибрати їжу з сумки.</color>", GoldHex)), () => FoodMenuIcons(m, it));
            RectTransform[] anchor = { feed.rectTransform };
            Clickable(feed.gameObject, () => { string why = FeedWhyNot(m, it); if (why == null) FeedPicker(anchor[0], m); else Tell(why); });

            Action live = () =>
            {
                if (num == null) return;
                float v = Satiety.Value(sat);
                int z = Satiety.Zone(v);
                num.text = "<b><color=" + ZoneInk[z] + ">" + Lang.UpperFirst(Satiety.ZoneWords[z]) + " · " + Mathf.RoundToInt(v) + " / 100</color></b>";
                float left = Strike.FedLeft(sat);
                string favLine = System.Text.RegularExpressions.Regex.Replace(Satiety.FavLine(sat, m) ?? "", "<.*?>", "");
                fav.text = left > 0f ? Lang.F("<color={0}>удар +{1}% · ще {2}</color>", GoodHex, RavenAbilities.R(Strike.FedPct), RavenAbilities.Clock(left)) : favLine;
            };
            live();
            Live.Add(live);
        }

        // what hunger does to a raven: the satiety zones, from the Satiety rules
        private static string HungerTip(bool m, ItemDrop.ItemData item)
        {
            string name = Plugin.RavenName(m);
            string s = Lang.F("<b><color={0}>Ситість</color></b>\n\n{1} має ситість від 0 до 100{2}. Вона тане сама (≈{3} за ігровий день{4}) і від кожного дару ({5}% його ціни в силі). Годувати можна скільки завгодно, аж до 100.\n", GoldHex, name, (item != null ? Lang.F(", зараз <color={0}>{1} — {2}</color>", Satiety.ZoneHexOf(item), Mathf.RoundToInt(Satiety.Value(item)), Satiety.ZoneWord(item)) : ""), RavenAbilities.R(Satiety.PerDay * Satiety.HungerMul(item)), (Norov.SatietyMul(item) < 1f ? Lang.T(", з хистом «Невибагливий»") : ""), RavenAbilities.R(Satiety.GiftShare * 100f));
            for (int z = Satiety.Starving; z <= Satiety.Stuffed; z++)
                s += "\n• <color=" + Satiety.ZoneHex[z] + ">" + Satiety.ZoneText(z) + "</color>";
            s += Lang.F("\n\n<b>Їжа.</b> Що далі від дому здобута, то ситніша: від +15 (Луки) до +35 (Попелясті землі). Раціон залежить від довіри: {0}. Клас — біом походження: від 1 (Луки) до 5 (Попелясті землі). Нижчий або вищий клас ворон відмовиться їсти; їжа не витратиться. Довіру дає лише та частина їжі, що вгамовує справжній голод (нижче {1}); ситому — нуль. За день з їжі — не більше {2} довіри{3}. Нагодований ворон ще й {4} хв б'є на {5}% сильніше.", (item != null ? Trust.FoodRuleText(Trust.Level(item)) : Lang.T("1–2-й рівні — класи 1–3, 3–5-й — 2–4, 6–7-й — 2–5")), RavenAbilities.R(Satiety.AskAt), RavenAbilities.R(Trust.FoodDailyCap), (item != null && Trust.Need(item) > 0f ? Lang.F(" (сьогодні лишилось {0})", RavenAbilities.R(Trust.FoodLeftToday(item))) : ""), RavenAbilities.R(Strike.FedMinutes), RavenAbilities.R(Strike.FedPct));
            s += Lang.F("\n\n<b>Улюблена їжа</b> — своя в кожного ворона і спершу прихована. Він натякає на неї, коли просить їсти; а нагодуєш нею — і дізнаєшся напевно. Ситить у {0} раза більше, довіри вдвічі більше, бонус до удару вдвічі довший.", Satiety.Num(Satiety.FavFillMul));
            return s;
        }

        // «Чим годувати»: what it eats at this trust, as icons (bright: in your bag), and how many a picky one refuses
        private static string FoodMenuTip(bool m, ItemDrop.ItemData item)
        {
            List<Trust.Food> foods = Trust.EdibleFoods(m, item);
            int refused = 0;
            foreach (Trust.Food f in Trust.Foods(m)) if (Trust.CanEat(item, f) && Quirks.Dislikes(item, f.Prefab)) refused++;
            string s = Lang.F("<b><color={0}>Чим годувати {1}</color></b> · {2}{3}", GoldHex, Plugin.RavenNameGen(m), Trust.FoodRuleText(Trust.Level(item)), (refused > 0 ? Lang.F("\n<color={0}>Перебірливий: −{1}</color>", QuirkHex, Lang.N(refused, "страва|страви|страв")) : ""));
            if (foods.Count == 0) return Lang.F("{0}\n\nЗараз нічого не їсть.", s);
            List<string> l = new List<string>();
            foreach (Trust.Food f in foods)
                l.Add((Satiety.KnownFav(item, f.Prefab) ? "<color=" + GoldHex + ">★</color> " : "") + f.Name + " <color=" + GoldHex + ">+" + Mathf.RoundToInt(f.Fill) + "</color>");
            string fav = Satiety.FavLine(item, m);
            return Lang.F("{0}\n\n{1}{2}\n\n<color={3}>Іконки: яскраві — є в сумці. Число — скільки ситості дає. Клас їжі — біом (1 Луки … 5 Попелясті землі); інший клас ворон не візьме. Довіру їжа дає лише голодному (ситість нижче {4}).</color>", s, string.Join("  ·  ", l.ToArray()), (fav.Length > 0 ? "\n\n" + fav : ""), GreyHex, RavenAbilities.R(Satiety.AskAt));
        }

        private static List<RavenBookHover.Icon> FoodMenuIcons(bool m, ItemDrop.ItemData item)
        {
            List<RavenBookHover.Icon> list = new List<RavenBookHover.Icon>();
            Player p = Player.m_localPlayer;
            List<Trust.Food> foods = Trust.EdibleFoods(m, item);
            foods.Sort((a, b) => a.Class != b.Class ? a.Class.CompareTo(b.Class) : a.Points.CompareTo(b.Points));
            foreach (Trust.Food f in foods)
            {
                Sprite sp = ItemIcon(f.Prefab);
                if (sp == null) continue;
                RavenBookHover.Icon ic = new RavenBookHover.Icon();
                ic.Sprite = sp;
                ic.Lit = p != null && Trust.Have(p, f.Prefab) > 0;
                ic.Star = Satiety.KnownFav(item, f.Prefab);
                list.Add(ic);
            }
            return list;
        }

        // why it cannot be fed from the book now (null: it can)
        private static string FeedWhyNot(bool m, ItemDrop.ItemData item)
        {
            Player p = Player.m_localPlayer;
            if (p == null || item == null) return Lang.T("Нема кого годувати.");
            string sulk = Quirks.Sulk(p, m);
            if (sulk != null) return Lang.F("{0}: навіть їсти не хоче.", sulk);
            string off;
            if (Trust.Away(item)) return Lang.F("{0} {1} — нагодуєш, коли повернеться.", Plugin.RavenName(m), Trust.AwayWord(m, item));
            if (RavenSkills.OffShoulder(p, m, out off)) return Lang.F("{0} зараз {1} — нагодуєш, коли повернеться.", Plugin.RavenName(m), off);
            if (Satiety.Value(item) >= 99.5f) return Lang.T("Ситий по самий дзьоб — більше не влізе.");
            if (BestFood(p, m) == null) return Lang.T("У сумці нема нічого, що він їсть.");
            return null;
        }

        private static Trust.Food BestFood(Player p, bool m) { return Satiety.Best(p, m, Trust.Worn(m)); }

        // ---------------------------------------------------------------- «Що далі»: the one thing worth doing now, as a seal

        private static void NextCard(Transform col, Player p, bool m, ItemDrop.ItemData item)
        {
            string tag, title, sub, button = null;
            bool ritual = false; // (the seal leads to the rite: its own tooltip)
            Action click = null;
            bool enabled = true;
            string why = null; // under the cursor on the seal: why it cannot be pressed now, or what it does
            string seal = m ? "munin" : "hugin";
            string name = Plugin.RavenName(m);
            int level = Trust.Level(item);
            Rite rite = RiteFor(p, item);
            bool loves = false;
            if (Trust.Stranger(item, p))
            {
                tag = Lang.T("Що далі · чужий"); title = Lang.F("{0} не твій", name); sub = Lang.T("Ти бачиш лише його стан; обряди — у його власника.");
                button = Lang.T("Лише перегляд"); enabled = false; seal = "plain";
                why = Lang.F("Це ворон гравця {0}: годувати його, проводити ритуали й навчати може тільки власник.", (Trust.OwnerName(item) ?? Lang.T("іншого гравця")));
            }
            else if (Trust.Away(item))
            {
                bool perch = Perches.On(item);
                tag = perch ? Lang.T("Що далі · жердина") : Lang.T("Що далі · чекати"); title = Trust.AwayText(m, item).Split('\n')[0].TrimEnd('.');
                sub = perch ? Lang.F("набирає силу, {0}", Perches.DayText(item)) : Lang.F("повернеться за ~{0}", RavenAbilities.Span(Trust.AwayLeft(item)));
                button = Lang.T("Чекати"); enabled = false;
                why = Trust.AwayText(m, item).Replace("\n", " ") + (perch ? Lang.T(" Покликати його з жердини — у колі наказів.") : Lang.T(" Поки його нема, не нагодуєш і ритуал не проведеш."));
            }
            else if (level == 0)
            {
                tag = Lang.T("Що далі · пробудження"); title = Lang.F("{0} спить · {1}", name, Trust.Rank(m, 0));
                sub = rite != null ? Lang.F("Розбуди: {0} ×{1} (є {2}) біля Жертовних каменів", rite.Step.Name, rite.Step.Count, rite.Have) : Lang.T("Розбуди трофеями біля каменів");
                if (rite != null && rite.All) { button = Lang.T("Розбудити"); ritual = true; click = () => Act(() => Trust.Ritual(p, m)); }
                else { button = Lang.T("До розвитку"); click = () => { _growMunin = m; Show(GrowTab); }; }
            }
            else if (rite != null && rite.All)
            {
                tag = Lang.T("Що далі · ритуал"); title = Lang.T("Можна провести ритуал"); sub = Lang.F("{0} {1}/{2} · ти біля каменів", rite.Step.Name, rite.Have, rite.Step.Count);
                button = Lang.T("Провести ритуал"); ritual = true; click = () => Act(() => Trust.Ritual(p, m));
            }
            else if (rite != null && Trust.Full(item))
            {
                tag = Lang.T("Що далі · ритуал"); title = Lang.T("Шкала довіри повна"); sub = Lang.F("Умов виконано {0} з {1} · бракує: {2}", rite.Met, rite.Count, rite.Missing);
                button = Lang.T("До розвитку"); click = () => { _growMunin = m; Show(GrowTab); };
            }
            else if (Trust.Hungry(item, p))
            {
                tag = Lang.T("Що далі · годування"); title = name + " " + Satiety.ZoneWord(item) + " · " + Mathf.RoundToInt(Satiety.Value(item)) + "/100";
                button = Lang.T("Нагодувати"); loves = true;
                Trust.Food food = BestFood(p, m);
                string off;
                if (Quirks.Sulk(p, m) != null)
                {
                    sub = Lang.F("{0} — навіть їсти не хоче", Quirks.Sulk(p, m)); enabled = false;
                    why = Lang.F("{0}: поки ображений, з твоїх рук не їсть{1}. Із торби на спині поїсть сам.", Quirks.Sulk(p, m), (Quirks.SulkLeft(item) > 0f ? Lang.F(" — мине за ~{0}", RavenAbilities.Span(Quirks.SulkLeft(item))) : ""));
                }
                else if (RavenSkills.OffShoulder(p, m, out off))
                {
                    sub = Lang.F("{0} зараз {1} — нагодуєш, коли повернеться", name, off); enabled = false; button = Lang.T("Чекати");
                    why = Lang.F("{0} зараз {1}. Нагодуєш, коли повернеться на плече.", name, off);
                }
                else if (food != null)
                {
                    sub = Lang.F("Найкраще — {0}{1} (є {2}): одна штука — {3}", (Satiety.KnownFav(item, food.Prefab) ? "★ " : ""), food.Name, Trust.Have(p, food.Prefab), Satiety.Preview(item, food));
                    why = Lang.T("Вибрати їжу з сумки: кожен клік — одна штука.");
                }
                else
                {
                    sub = Lang.T("У сумці нема нічого, що він їсть"); enabled = false;
                    why = Lang.F("У сумці нема їжі, яку {0} зараз їсть. {1}.\nУся його їжа — під ситістю, у «Чим нагодувати».", name, Trust.FoodRuleText(level));
                }
            }
            else if (rite != null && Trust.Need(item) > 0f && Trust.Points(item) / Trust.Need(item) >= 0.8f)
            {
                tag = Lang.T("Що далі · ритуал"); title = Lang.T("Шкала майже повна"); sub = Lang.F("Умов виконано {0} з {1}", rite.Met, rite.Count);
                button = Lang.T("До розвитку"); click = () => { _growMunin = m; Show(GrowTab); };
            }
            else
            {
                tag = Satiety.IsStuffed(item) ? Lang.T("Що далі · об'ївся") : Lang.T("Що далі · вишкіл");
                title = Satiety.IsStuffed(item) ? Lang.F("{0} об'ївся · {1}/100", name, Mathf.RoundToInt(Satiety.Value(item))) : level >= Trust.MaxLevel ? Lang.F("{0} довіряє тобі цілком", name) : Lang.F("{0} ситий і довіряє", name);
                int tier = Strike.Tier(item);
                sub = tier < Strike.Bosses.Length ? Lang.F("наступне — перемогти {0} з ним на плечі", Strike.BossNames[tier]) : Lang.F("вишкіл {0} / {1}", Strike.Level(item), Strike.MaxLevel);
                if (Satiety.IsStuffed(item)) sub = Lang.F("лінується: не відлітає погуляти, удар −{0}% · мине за ~{1} хв", RavenAbilities.R(Satiety.StuffedStrikePct), Mathf.CeilToInt(Satiety.StuffedMinutes(item)));
                button = Lang.T("Вишкіл"); click = () => { _growMunin = m; Show(GrowTab); };
            }

            RectTransform card = Rect("Next", col);
            HBox(card, 14f, TextAnchor.MiddleLeft, 0, 0, 4, 0);
            Fixed(card.gameObject, -1f, RowNext);
            RectTransform words = Rect("Words", card);
            VBox(words, 2f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text t = T(words, tag.ToUpperInvariant(), 14f, Caption, F.Body);
            t.characterSpacing = 12f;
            TMP_Text head = Shrink(T(words, title, 21f, Ink, F.Head), 14f);
            if (loves) Tip(head.gameObject, () => HungerTip(m, item));
            TMP_Text subT = Shrink(T(words, sub, 14f, InkSoft, F.Italic), 11f);
            Tip(subT.gameObject, () => title + "\n" + sub);
            if (button == null) return;
            RectTransform[] anchor = new RectTransform[1];
            if (loves && enabled) click = () => FeedPicker(anchor[0], m);
            anchor[0] = SealButton(card, button, seal, 86f, 15f, click, enabled && click != null, enabled ? 1f : 0.6f);
            if (why == null && click != null) // what the rest do
                why = ritual ? Lang.T("Провести обряд зараз: ворон з'їсть трофеї й полетить до Одіна.") : Lang.T("Відкрити вкладку «Розвиток».");
            string w = why, b = button;
            if (w != null) SealTip(anchor[0], () => "<b>" + b + "</b>" + (enabled && click != null ? "" : Lang.T(" <color=#8C93A1>· зараз не можна</color>")) + "\n" + w);
        }

        // a seal's tooltip: on its face, so it shows on a seal that cannot be pressed too
        private static void SealTip(RectTransform seal, Func<string> text)
        {
            Transform face = seal != null ? seal.Find("Face") : null;
            if (face != null) Tip(face.gameObject, text);
        }

        // ---------------------------------------------------------------- a raven not on you (his page stays as wide as the other)

        // the raven's return (a copy of a lost one) only here on the Ravens tab, as the design has it
        private static void EmptyPage(RectTransform col, bool m, bool returns)
        {
            col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            col.GetComponent<VerticalLayoutGroup>().spacing = 12f;
            RectTransform holder = Rect("Holder", col);
            HBox(holder, 0f, TextAnchor.MiddleCenter);
            RectTransform pic = Rect("Portrait", holder);
            Fixed(pic.gameObject, 150f, 150f);
            Img(pic, "Ring", BookArt.Circle, new Color(0.227f, 0.141f, 0.078f, 0.4f));
            Image back = Img(pic, "Paper", BookArt.Circle, C("#e2d0a4"));
            Stretch(back.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            Image raven = Img(pic, "Raven", BookArt.Portrait(m) ?? RavenItem.Icon(m), new Color(0.72f, 0.6f, 0.45f, 0.35f));
            raven.preserveAspect = true;
            Stretch(raven.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            TMP_Text n = T(col, Plugin.RavenName(m), 28f, A(BookArt.RavenInk(m), 0.6f), F.Head, TextAlignmentOptions.Center);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            T(col, Lang.F("{0} нема на плечі", (m ? Lang.T("Муніна") : Lang.T("Хугіна"))), 15f, Ink, F.Italic, TextAlignmentOptions.Center);
            TMP_Text how = T(col, Lang.T("<b>Як посадити</b>"), 15f, GoldInk, F.Body, TextAlignmentOptions.Center);
            Tip(how.gameObject, () => Lang.F("<b><color={0}>Як посадити ворона</color></b>\n\nХугіна й Муніна продає Хальдор. Правий клік на вороні в сумці садить його на плече: Хугіна — на праве, Муніна — на ліве. Ще раз — знімає (довіра від цього трохи тане).", GoldHex));
            if (!returns) return;
            Player p = Player.m_localPlayer;
            foreach (Backup.Lost l in Backup.Ravens(p)) if (l.Munin == m && !l.WithYou) ReturnRow(col, p, l);
        }

        // a lost raven comes back: only for one that is not with you
        private static void ReturnRow(Transform parent, Player p, Backup.Lost l)
        {
            bool atStones = Trust.AtStones(p);
            DashedLine(parent);
            RectTransform box = Rect("Return", parent);
            VBox(box, 6f, TextAnchor.UpperCenter, 0, 0, 6, 0).childForceExpandWidth = false;
            string price = l.Price != null ? Lang.F("{0} ×{1} (є {2})", l.Price.Name, l.Price.Count, Trust.Have(p, l.Price.Prefab)) : Lang.T("трофеї боса");
            string why = l.DaysLeft > 0 ? Lang.F("лишилось {0} дн. до наступного повернення", l.DaysLeft) : !atStones ? Lang.T("підійди до Жертовних каменів") : price;
            TMP_Text t = T(box, Lang.F("Повернути {0}", (l.Munin ? Lang.T("Муніна") : Lang.T("Хугіна"))), 19f, Ink, F.Head, TextAlignmentOptions.Center);
            Tip(t.gameObject, () => Lang.F("<b><color={0}>Повернення ворона</color></b>\n\nВорон загинув у морі, зник чи лишився деінде? Його можна повернути обрядом біля Жертовних каменів.\n\nЦіна: {1}.\n\nВорон повернеться з тим самим рівнем довіри, вишколом і переможеними босами. Лише шкала до наступного рівня почнеться з нуля, а частину спогадів він забуде. Старий ворон, якщо десь лишився, розсиплеться пір'ям. Не частіше ніж раз на {2} днів.", GoldHex, price, Backup.RestoreDays));
            // which raven it is: its nickname, its norov, how far you went together
            Nicks.Nick nick = Nicks.Current(l.Copy);
            if (nick != null) T(box, "«" + nick.Name + "»", 17f, C(Nicks.PaperHex(nick.K)), F.Head, TextAlignmentOptions.Center);
            Norov.Character ch = Norov.CharOf(l.Copy);
            Norov.Knack kn = Norov.KnackOf(l.Copy);
            Norov.Quirk qu = Norov.QuirkOf(l.Copy);
            List<string> nature = new List<string>();
            if (ch != null) nature.Add(ch.Name);
            if (kn != null) nature.Add(kn.Name(l.Munin));
            if (qu != null) nature.Add(qu.Name);
            if (nature.Count > 0) T(box, string.Join(" · ", nature.ToArray()), 14f, InkSoft, F.Italic, TextAlignmentOptions.Center);
            int days = Nicks.Count(l.Copy, "days");
            T(box, Lang.F("довіра {0} · вишкіл {1}{2}", l.Level, Strike.Level(l.Copy), (days > 0 ? Lang.F(" · {0} разом", Lang.N(days, "день|дні|днів")) : "")), 14f, InkSoft, F.Body, TextAlignmentOptions.Center);
            string id = l.Id;
            bool can = atStones && l.DaysLeft == 0 && l.Price != null && Trust.Have(p, l.Price.Prefab) >= l.Price.Count;
            T(box, why, 14f, can ? Good : Bad, F.Body, TextAlignmentOptions.Center); // what it takes: green when all is there
            OutlineButton(box, Lang.T("Повернути"), () => Act(() => Backup.Restore(p, id)), can);
        }

        // ---------------------------------------------------------------- the food list by the seal
        // The design's small dark card: what he eats now, three across — the name, fill · trust, how many you have, ★ the
        // favourite; those you do not have, faded. A click feeds; a click beside it (or Esc) closes only the list.

        private static GameObject _popup;
        private const float PopupW = 440f; // the food card (its frame and parchment take 80 of it)
        private static bool? _feedFor;   // whose food list is open (it stays open while you feed, piece by piece)
        private static Vector2 _feedAt;  // where it stands (the overlay's units): drawn again in the same place

        private static void ClosePopup()
        {
            if (_popup != null) { _popup.SetActive(false); UnityEngine.Object.Destroy(_popup); } // (hidden now: Destroy waits for the frame's end, a new card may open meanwhile)
            _popup = null;
            _feedFor = null;
            _nickYes = null;
            MailForget();
        }

        private static void FeedPicker(RectTransform button, bool m) { FeedPicker(button, m, null); }

        // one click, one piece: the list stays open with the new counts and satiety, so you feed as much as you like
        private static void FeedPicker(RectTransform button, bool m, Vector2? keep, float scroll = 0f)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            if (p == null || _root == null) return;
            ItemDrop.ItemData raven = Trust.Worn(m);
            List<Trust.Food> foods = new List<Trust.Food>(Trust.EdibleFoods(m, raven));
            Trust.Food top = Satiety.Best(p, m, raven);
            foods.Sort((a, b) =>
            {
                if (a == b) return 0;
                int ha = Trust.Have(p, a.Prefab) > 0 ? 0 : 1, hb = Trust.Have(p, b.Prefab) > 0 ? 0 : 1;
                if (ha != hb) return ha.CompareTo(hb);
                if (a == top) return -1;
                if (b == top) return 1;
                return Satiety.TrustOf(raven, b, Satiety.KnownFav(raven, b.Prefab)).CompareTo(Satiety.TrustOf(raven, a, Satiety.KnownFav(raven, a.Prefab)));
            });
            if (foods.Count == 0) return;

            Card card = OpenCard("FoodList", Lang.T("Годування"), PopupW, false); // a click beside the card closes it
            RectTransform panel = card.Paper;
            T(panel, Lang.F("Чим нагодувати {0}", (m ? Lang.T("Муніна") : Lang.T("Хугіна"))), 21f, PInk, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            // how full he is now (it changes with every piece)
            T(panel, Lang.F("Ситість <color={0}><b>{1} / 100 · {2}</b></color> · клік — одна штука", ZonePaperHex[Mathf.Clamp(Satiety.Zone(raven), 0, ZonePaperHex.Length - 1)], Mathf.RoundToInt(Satiety.Value(raven)), Satiety.ZoneWord(raven)), 14f, PSoft, F.Body);

            // two across: the food's picture on the left; its name, fill · trust and how many you have by it
            const float cellH = 62f, gap = 6f, inner = PopupW - 32f - 48f;
            int rows = (foods.Count + 1) / 2;
            RectTransform view = Rect("View", panel);
            Fixed(view.gameObject, -1f, Mathf.Min(rows * cellH + (rows - 1) * gap, 4.5f * (cellH + gap)));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RectTransform grid = Rect("Grid", view);
            grid.anchorMin = new Vector2(0f, 1f); grid.anchorMax = new Vector2(1f, 1f); grid.pivot = new Vector2(0.5f, 1f);
            grid.sizeDelta = Vector2.zero;
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(Mathf.Floor((inner - gap) / 2f), cellH); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 2;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = grid; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 120f; // a notch of the wheel: about two rows
            foreach (Trust.Food f in foods)
            {
                Trust.Food food = f;
                int have = Trust.Have(p, f.Prefab);
                bool fav = Satiety.KnownFav(raven, f.Prefab), favReal = Satiety.IsFav(raven, f.Prefab);
                RectTransform cell = Rect("Food", grid);
                Box(cell, 5f, new Color(1f, 1f, 1f, 0.22f), fav ? PGoldLine : PEdge, fav ? 2f : 1.2f);
                if (have <= 0) cell.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
                HBox(cell, 8f, TextAnchor.MiddleLeft, 6, 8, 5, 5);
                RectTransform pic = Rect("Picture", cell);
                Fixed(pic.gameObject, 44f, 44f);
                Box(pic, 4f, A(PDark, 0.85f), new Color(0f, 0f, 0f, 0f), 0f);
                Sprite icon = ItemIcon(f.Prefab);
                if (icon != null) { Image im = Img(pic, "Icon", icon, Color.white); im.preserveAspect = true; Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)); }
                RectTransform words = Rect("Words", cell);
                VBox(words, 0f, TextAnchor.MiddleLeft);
                words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                TMP_Text n = T(words, f.Name, 14f, PInk, F.Body);
                n.enableAutoSizing = true; n.fontSizeMin = 11f; n.fontSizeMax = 14f; n.textWrappingMode = TextWrappingModes.NoWrap;
                n.overflowMode = TextOverflowModes.Ellipsis;
                RectTransform line = Rect("Line", words);
                HBox(line, 6f, TextAnchor.MiddleLeft);
                T(line, "+" + Mathf.RoundToInt(Satiety.FillOf(f, favReal)) + " · +" + Satiety.Num(Mathf.Round(Satiety.TrustOf(raven, f, favReal) * 10f) / 10f), 14f, BookArt.Good, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(line);
                T(line, "×" + have, 14f, PSoft, F.Body, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
                if (fav)
                {
                    TMP_Text star = T(cell, "★", 14f, BookArt.GoldInk, F.Body, TextAlignmentOptions.TopRight);
                    star.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    Stretch(star.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-5f, -3f));
                }
                Tip(cell.gameObject, () => "<b>" + food.Name + (fav ? " <color=" + GoldHex + ">★</color>" : "") + "</b>\n" + Satiety.Preview(raven, food) + (have <= 0 ? Lang.F("\n<color={0}>нема в сумці</color>", GreyHex) : ""));
                if (have > 0)
                {
                    Button b = cell.gameObject.AddComponent<Button>();
                    b.targetGraphic = cell.GetComponent<Image>();
                    Tint(b);
                    b.onClick.AddListener(() => { Tell(Trust.Feed(p, m, food)); if (Trust.Worn(m) != null) FeedPicker(null, m, _feedAt, PopupScroll()); });
                }
            }
            T(panel, Lang.F("★ улюблена · ситість · довіра — довіру дає лише голодному, до {0} за день", RavenAbilities.R(Trust.FoodDailyCap)), 14f, PSoft, F.Italic);

            // by the seal, to its left (both on the book's plane); fed piece by piece, it stays where it stood
            Vector2 at = PlaceCard(card, button, true, keep.HasValue ? keep : (button == null ? (Vector2?)new Vector2(0f, BookH * 0.25f) : null));
            _feedFor = m; _feedAt = at;
            if (scroll > 0f) // fed piece by piece: the list stays where it was scrolled
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
                float most = grid.rect.height - view.rect.height;
                if (most > 1f) grid.anchoredPosition = new Vector2(grid.anchoredPosition.x, Mathf.Min(scroll, most));
            }
        }

        // ---------------------------------------------------------------- the ritual's conditions

        private sealed class Rite
        {
            public Trust.Step Step;
            public bool Waking, Bar, Heads, Stones, Hung, All;
            public bool? HungRaw;
            public bool HungLive; // seen at the stones now (else: as last seen there)
            public int Have, Met, Count;
            public float Distance = -1f; // to the nearest stones the game has loaded; -1: none near
            public string Missing;
            public string Off; // the raven is off the shoulder for a moment, and why
        }

        private static Rite RiteFor(Player p, ItemDrop.ItemData item)
        {
            if (p == null || item == null) return null;
            Trust.Step step = Trust.NextStep(item);
            if (step == null) return null;
            Rite r = new Rite();
            r.Step = step;
            r.Waking = Trust.Level(item) == 0;
            r.Bar = Trust.Full(item);
            r.Have = Trust.Have(p, step.Prefab);
            r.Heads = r.Have >= step.Count;
            r.Stones = Trust.AtStones(p);
            r.HungRaw = Trust.TrophyHungKnown(p, step.Prefab, out r.HungLive);
            r.Hung = r.HungRaw == true;
            foreach (BossStone s in Trust.Stones())
            {
                if (s == null) continue;
                float d = Vector3.Distance(s.transform.position, p.transform.position);
                if (r.Distance < 0f || d < r.Distance) r.Distance = d;
            }
            List<string> miss = new List<string>();
            if (!r.Waking && !r.Bar) miss.Add(Lang.T("шкала"));
            if (!r.Heads) miss.Add(Lang.T("трофеї"));
            if (!r.Stones) miss.Add(Lang.T("камені"));
            if (!r.Hung) miss.Add(Lang.T("трофей на камені"));
            r.Count = r.Waking ? 3 : 4;
            r.Met = r.Count - miss.Count;
            string off;
            bool munin; RavenItem.IsRaven(item, out munin);
            r.Off = RavenSkills.OffShoulder(p, munin, out off) ? off : null;
            r.All = miss.Count == 0 && r.Off == null && !Trust.Away(item) && !Trust.Stranger(item, p);
            r.Missing = string.Join(", ", miss.ToArray());
            return r;
        }

        // ---------------------------------------------------------------- «Дари»

        private static List<RavenAbility> BookOrder()
        {
            // the strikes first (the ones used most), then the rest as they come
            List<RavenAbility> list = new List<RavenAbility>();
            foreach (RavenAbility a in RavenAbilities.All) if (a.Id == "hstrike" || a.Id == "mstrike") list.Add(a);
            foreach (RavenAbility a in RavenAbilities.All) if (a.Id != "hstrike" && a.Id != "mstrike") list.Add(a);
            return list;
        }

        private static string Clean(string name) { return name.Replace(Lang.T(" (пасивний ефект)"), ""); }

        private static void PageGifts(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            foreach (bool m in new bool[] { true, false })
            {
                ItemDrop.ItemData item = m ? munin : hugin;
                RectTransform col = Column(m ? "Munin" : "Hugin", ColumnX(m), 46f, ColumnW, 12f);
                if (item != null) GiftsPage(col, m, item);
                else EmptyPage(col, m, false);
            }
        }

        // The gifts' page of one raven (the user's design «Дари · 3 — коло навколо ворона», 2026-10-19): his name over it,
        // his gifts on torn cards round his strength in the middle (a dial filling clockwise), a dashed ring through them;
        // under it the passive ones and the shut ones as small chips. A card: click — give it a key (the key then hangs
        // under the card as a dark tab).
        private const float RingH = 420f;
        private static void GiftsPage(RectTransform col, bool m, ItemDrop.ItemData item)
        {
            RectTransform head = Rect("Head", col);
            HBox(head, 12f, TextAnchor.LowerCenter, 0, 0, 2, 0);
            T(head, Plugin.RavenName(m), 34f, BookArt.RavenInk(m), F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            T(head, m ? Lang.T("дари ворона пам'яті") : Lang.T("дари ворона думки"), 16f, Brown, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;

            int level = Trust.Level(item);
            List<RavenAbility> cards = new List<RavenAbility>(), passive = new List<RavenAbility>(), shut = new List<RavenAbility>();
            foreach (RavenAbility a in BookOrder())
            {
                if (a.Munin != m) continue;
                bool open = !RavenAbilities.Stub(a) && RavenAbilities.UnlockLevel(a) <= Mathf.Max(1, level);
                if (!open) shut.Add(a);
                else if (a.Passive) passive.Add(a);
                else cards.Add(a);
            }

            RectTransform ring = Rect("Ring", col);
            Fixed(ring.gameObject, -1f, RingH);
            float w, h;
            List<Vector2> at = RingPlaces(cards.Count, ColumnW, RingH, out w, out h);
            DashedRing(ring, (ColumnW - w) * 0.5f, (RingH - h) * 0.5f);
            Dial(ring, m, item);
            for (int i = 0; i < cards.Count; i++) GiftCard(ring, cards[i], level, at[i], w, h);

            if (passive.Count + shut.Count == 0) return;
            if (passive.Count + shut.Count <= 3) // a few: both groups on one line
            {
                RectTransform row = Rect("Rest", col);
                HBox(row, 16f, TextAnchor.UpperLeft);
                if (passive.Count > 0) ChipGroup(row, Lang.T("Пасивні"), passive, false, passive.Count);
                if (shut.Count > 0) ChipGroup(row, Lang.T("Закриті"), shut, true, shut.Count);
                return;
            }
            if (passive.Count > 0) ChipGroup(col, Lang.T("Пасивні"), passive, false, 3);
            if (shut.Count > 0) ChipGroup(col, Lang.T("Закриті"), shut, true, 4);
        }

        // n cards of one size round an ellipse in w × h, an equal way apart along it, the first at the top (or half a step
        // round, whichever lets them be bigger); never on the dial in the middle. Their size: as big as fits, ≤ 140 wide.
        private static List<Vector2> RingPlaces(int n, float W, float H, out float cw, out float ch)
        {
            cw = 140f; ch = 118f;
            List<Vector2> best = null;
            float bestW = 0f;
            foreach (float off in new float[] { 0f, 0.5f })
                for (float w = 140f; w >= 88f; w -= 2f)
                {
                    if (w <= bestW) break;
                    float h = Mathf.Round(w * 0.84f);
                    List<Vector2> p = Ellipse(n, (W - w) * 0.5f, (H - h) * 0.5f, off);
                    if (!RingFits(p, w, h)) continue;
                    best = p; bestW = w; cw = w; ch = h;
                    break;
                }
            if (best == null) { cw = 88f; ch = 74f; best = Ellipse(n, (W - cw) * 0.5f, (H - ch) * 0.5f, 0f); } // (never: at most 9)
            return best;
        }

        private static List<Vector2> Ellipse(int n, float rx, float ry, float off)
        {
            const int M = 720;
            float[] acc = new float[M + 1];
            Vector2 prev = new Vector2(0f, ry);
            for (int i = 1; i <= M; i++)
            {
                float t = i * Mathf.PI * 2f / M;
                Vector2 q = new Vector2(Mathf.Sin(t) * rx, Mathf.Cos(t) * ry);
                acc[i] = acc[i - 1] + (q - prev).magnitude; prev = q;
            }
            List<Vector2> list = new List<Vector2>();
            for (int k = 0, i = 0; k < n; k++)
            {
                float s = acc[M] * (k + off) / Mathf.Max(1, n);
                while (i < M && acc[i] < s) i++;
                float t = i * Mathf.PI * 2f / M;
                list.Add(new Vector2(Mathf.Sin(t) * rx, Mathf.Cos(t) * ry));
            }
            return list;
        }

        private static bool RingFits(List<Vector2> p, float w, float h)
        {
            const float gap = 6f;
            for (int i = 0; i < p.Count; i++)
            {
                if (Mathf.Abs(p[i].x) < w * 0.5f + 95f && Mathf.Abs(p[i].y) < h * 0.5f + 70f) return false; // the dial and its words
                for (int j = i + 1; j < p.Count; j++)
                    if (Mathf.Abs(p[i].x - p[j].x) < w + gap && Mathf.Abs(p[i].y - p[j].y) < h + gap) return false;
            }
            return true;
        }

        // a faint dashed ellipse through the cards' middles
        private static void DashedRing(RectTransform ring, float rx, float ry)
        {
            const int n = 64;
            Color c = A(Brown, 0.32f);
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                Image d = Img(ring, "Dash", null, c);
                RectTransform r = d.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(5f, 1.6f);
                r.anchoredPosition = new Vector2(Mathf.Sin(t) * rx, Mathf.Cos(t) * ry);
                Vector2 tan = new Vector2(Mathf.Cos(t) * rx, -Mathf.Sin(t) * ry);
                r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg);
            }
        }

        // his strength in the middle: a pale oval, a ring filling clockwise from the top in his colour round a dark disc
        // with his portrait, and «85 / 200 сили» under it
        private static void Dial(RectTransform ring, bool m, ItemDrop.ItemData item)
        {
            ItemDrop.ItemData it = item;
            Image oval = Img(ring, "Oval", BookArt.Circle, new Color(1f, 0.98f, 0.92f, 0.28f));
            Centre(oval.rectTransform, Vector2.zero, new Vector2(196f, 136f));
            Image ovalLine = Img(ring, "OvalLine", BookArt.Ring(0.006f), A(Brown, 0.22f));
            Centre(ovalLine.rectTransform, Vector2.zero, new Vector2(196f, 136f));
            Vector2 mid = new Vector2(0f, 10f);
            Image track = Img(ring, "Track", BookArt.Ring(0.09f), A(Brown, 0.16f));
            Centre(track.rectTransform, mid, new Vector2(104f, 104f));
            Image fill = Img(ring, "Fill", BookArt.Ring(0.09f), BookArt.RavenInk(m));
            Centre(fill.rectTransform, mid, new Vector2(104f, 104f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360; fill.fillOrigin = (int)Image.Origin360.Top; fill.fillClockwise = true;
            Image rim = Img(ring, "Rim", BookArt.Circle, C("#5a3a20"));
            Centre(rim.rectTransform, mid, new Vector2(82f, 82f));
            Image disc = Img(ring, "Disc", BookArt.Circle, m ? BookArt.MuninDark : BookArt.HuginDark);
            Centre(disc.rectTransform, mid, new Vector2(76f, 76f));
            Image pic = Img(ring, "Raven", BookArt.Portrait(m) ?? RavenItem.Icon(m), Color.white);
            Centre(pic.rectTransform, mid, new Vector2(70f, 70f));
            pic.preserveAspect = true;
            TMP_Text words = T(ring, "", 17f, Brown, F.Body, TextAlignmentOptions.Center);
            words.textWrappingMode = TextWrappingModes.NoWrap;
            Centre(words.rectTransform, new Vector2(0f, -50f), new Vector2(190f, 24f));
            string hex = "#" + ColorUtility.ToHtmlStringRGB(BookArt.RavenInk(m));
            Action paint = () =>
            {
                if (fill == null || words == null) return;
                float now = Power.Get(it), top = Mathf.Max(1f, Trust.Strength(it));
                fill.fillAmount = Mathf.Clamp01(now / top);
                words.text = Lang.F("<b><color={0}>{1}</color></b> / {2} сили", hex, Mathf.FloorToInt(now), Mathf.RoundToInt(top));
            };
            paint();
            Live.Add(paint);
            Tip(words.gameObject, () => Lang.F("<b>Сила {0}</b>\n\n{1}", Plugin.RavenNameGen(m), Power.Describe(it, m, true)));
            Tip(disc.gameObject, () => Lang.F("<b>Сила {0}</b>\n\n{1}", Plugin.RavenNameGen(m), Power.Describe(it, m, true)));
        }

        // a child of the ring at a place from its middle, this size
        private static void Centre(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        // the passive or the shut gifts: a small heading and their chips (shut ones grey, a lock and the level they open at)
        private static void ChipGroup(Transform parent, string title, List<RavenAbility> list, bool shut, int cols)
        {
            RectTransform g = Rect(shut ? "Shut" : "Passive", parent);
            VBox(g, 4f, TextAnchor.UpperLeft);
            if (parent.GetComponent<HorizontalLayoutGroup>() != null) g.gameObject.AddComponent<LayoutElement>().flexibleWidth = list.Count;
            T(g, title, 17f, Brown, F.Head);
            RectTransform grid = Rect("Chips", g);
            GridLayoutGroup gl = null;
            if (list.Count <= cols) HBox(grid, 8f, TextAnchor.MiddleLeft); // one line: the chips share it
            else // more than a line: a grid
            {
                gl = grid.gameObject.AddComponent<GridLayoutGroup>();
                gl.cellSize = new Vector2(Mathf.Floor((ColumnW - (cols - 1) * 8f) / cols), 44f); gl.spacing = new Vector2(8f, 8f);
                gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = cols;
            }
            foreach (RavenAbility a in list)
            {
                RavenAbility ab = a;
                RectTransform chip = Rect("Chip_" + a.Id, grid);
                HBox(chip, 6f, TextAnchor.MiddleLeft, 8, 8, 5, 5);
                Torn(chip, shut ? new Color(0.88f, 0.84f, 0.76f, 0.8f) : BookArt.Card, BookArt.CardLine, 1f);
                if (gl == null) { LayoutElement le = chip.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 44f; le.flexibleWidth = 1f; }
                RectTransform ic = Rect("Icon", chip);
                Fixed(ic.gameObject, 30f, 30f);
                Img(ic, "Ring", BookArt.Circle, shut ? C("#8a7a5c") : Ink);
                if (shut)
                {
                    Image back = Img(ic, "Back", BookArt.Circle, C("#6e6252"));
                    Stretch(back.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
                    Image lk = Img(ic, "Lock", BookArt.Lock, C("#d8ccb0"));
                    Stretch(lk.rectTransform, Vector2.zero, Vector2.one, new Vector2(7f, 6f), new Vector2(-7f, -7f));
                }
                else
                {
                    Image im = Img(ic, "Icon", AbilityIcons.For(a.Id, a.Munin), Color.white);
                    Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
                    im.preserveAspect = true;
                }
                RectTransform words = Rect("Words", chip);
                VBox(words, -2f, TextAnchor.MiddleLeft);
                words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Shrink(T(words, a.Plain, 15f, shut ? Brown : Ink, F.Head, TextAlignmentOptions.MidlineLeft), 11f);
                if (shut) Shrink(T(words, RavenAbilities.Stub(a) ? Lang.T("в роботі") : Lang.F("рів. {0}", RavenAbilities.UnlockLevel(a)), 13f, Muted, F.Italic, TextAlignmentOptions.MidlineLeft), 10f);
                Tip(chip.gameObject, () => GiftTip(ab));
            }
        }

        private static readonly Color DotReady = C("#5a9a4a"), DotWait = C("#d8a83a"), DotWeak = C("#b8322a");

        // a gift's card on the ring: its icon, name and price; click — pick its key (shown as a dark tab under the card)
        private static void GiftCard(RectTransform ring, RavenAbility a, int level, Vector2 pos, float w, float h)
        {
            float k = w / 140f;
            RectTransform card = Rect("Gift_" + a.Id, ring);
            Centre(card, pos, new Vector2(w, h));
            bool waiting = _capturing == a.Id;
            Torn(card, BookArt.Card, waiting ? GoldInk : BookArt.CardLine, waiting ? 2f : 1f);
            if (level == 0) card.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            VBox(card, 2f * k, TextAnchor.UpperCenter, 6, 6, Mathf.RoundToInt(12f * k), 6).childForceExpandWidth = false;
            RectTransform ic = Rect("Icon", card);
            float isz = Mathf.Max(32f, 46f * k);
            Fixed(ic.gameObject, isz, isz);
            Img(ic, "Ring", BookArt.Circle, Ink);
            Image icon = Img(ic, "Icon", AbilityIcons.For(a.Id, a.Munin), Color.white);
            Stretch(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            icon.preserveAspect = true;
            TMP_Text name = T(card, a.Plain, Mathf.Max(13f, 17f * k), Ink, F.Head, TextAlignmentOptions.Center);
            name.enableAutoSizing = true; name.fontSizeMin = 11f; name.fontSizeMax = Mathf.Max(13f, 17f * k); name.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(name.gameObject, w - 14f, Mathf.Max(16f, 21f * k));
            TMP_Text price = T(card, RavenAbilities.R(a.Cost()) + " · " + RavenAbilities.Span(a.Cooldown()), Mathf.Max(11f, 13f * k), Brown, F.Body, TextAlignmentOptions.Center);
            price.textWrappingMode = TextWrappingModes.NoWrap;

            // its key: a dark tab hanging over the card's lower edge (only when it has one, or while one is being picked)
            KeyboardShortcut key = RavenKeys.Get(a.Id);
            if (waiting || key.MainKey != KeyCode.None)
            {
                List<string> clashes = waiting ? new List<string>() : RavenKeys.Clashes(a.Id);
                RectTransform tab = Rect("Key", card);
                tab.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                tab.anchorMin = tab.anchorMax = tab.pivot = new Vector2(0.5f, 0f);
                tab.sizeDelta = new Vector2(waiting ? w - 10f : Mathf.Min(w - 24f, 70f), 22f); tab.anchoredPosition = new Vector2(0f, -9f);
                Box(tab, 4f, waiting ? C("#3a2c1c") : C("#2a1e12"), waiting ? Gold : C("#5a4a30"), 1f);
                tab.GetComponent<Image>().raycastTarget = false; // a click on it is the card's
                TMP_Text t = T(tab, (waiting ? Lang.T("Натисни клавішу…") : RavenKeys.Label(key)) + (clashes.Count > 0 ? " <color=#FF6B6B><b>!</b></color>" : ""), 14f, waiting ? Gold : Paper, F.Head, TextAlignmentOptions.Center);
                t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = 14f; t.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            }
            Clickable(card.gameObject, () => { _capturing = a.Id; Show(_tab, true); });
            Tip(card.gameObject, () => GiftTip(a) + "\n\n<color=" + GreyHex + ">" + (waiting
                ? Lang.T("Натисни клавішу (можна з Shift, Ctrl чи Alt) · Esc — скасувати · Backspace — прибрати")
                : key.MainKey == KeyCode.None ? Lang.T("Клік — призначити клавішу.") : Lang.F("Клік — змінити клавішу ({0}).", RavenKeys.Label(key))) + "</color>"
                + (!waiting && key.MainKey != KeyCode.None && RavenKeys.Clashes(a.Id).Count > 0 ? Lang.F("\n\n<color=#FF6B6B>!</color> Також на цій клавіші: {0}. Спрацює і те, і дар — клавіші воронів нічого не забирають у гри й інших модів.", string.Join(", ", RavenKeys.Clashes(a.Id).ToArray())) : ""));

            Image dot = Img(card, "Dot", BookArt.Circle, DotReady);
            dot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = dot.rectTransform.pivot = new Vector2(1f, 1f);
            dot.rectTransform.sizeDelta = new Vector2(10f, 10f); dot.rectTransform.anchoredPosition = new Vector2(-10f, -10f);
            Image dotRing = Img(dot.rectTransform, "Ring", BookArt.Circle, new Color(0.227f, 0.141f, 0.078f, 0.5f));
            Stretch(dotRing.rectTransform, Vector2.zero, Vector2.one, new Vector2(-1.5f, -1.5f), new Vector2(1.5f, 1.5f));
            dotRing.transform.SetAsFirstSibling();
            Image dotFill = Img(dot.rectTransform, "Fill", BookArt.Circle, DotReady);
            Stretch(dotFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dot.color = new Color(0f, 0f, 0f, 0f);
            Action paint = () =>
            {
                if (dot == null) return;
                int d = RavenSkills.Dot(a);
                dot.gameObject.SetActive(d != RavenSkills.DotNone);
                dotFill.color = d == RavenSkills.DotReady ? DotReady : d == RavenSkills.DotWait ? DotWait : DotWeak;
            };
            paint();
            Live.Add(paint);
            int gold = GiftUp.Level(a.Id); // golden runes in it: its level, gold in the corner
            if (gold > 0)
            {
                TMP_Text g = T(card, GiftUp.Roman(gold), 15f, GoldInk, F.Head, TextAlignmentOptions.TopLeft);
                g.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));
            }
        }

        // a key field in the footer's keys strip, a raised keycap: the key, "—", or waiting for a key
        private static RectTransform KeyBox(Transform parent, string id)
        {
            bool waiting = _capturing == id;
            KeyboardShortcut k = RavenKeys.Get(id);
            bool none = k.MainKey == KeyCode.None;
            RectTransform box = Rect("Key_" + id, parent);
            List<string> clashes = waiting ? new List<string>() : RavenKeys.Clashes(id);
            string text = waiting ? Lang.T("Натисни клавішу…") : none ? "—" : RavenKeys.Label(k);
            {
                HBox(box, 0f, TextAnchor.MiddleCenter, 7, 7, 2, 4);
                Box(box, 5f, waiting ? C("#3a2c1c") : new Color(0f, 0f, 0f, 0f), waiting ? Gold : C("#5a4a30"), 1.5f);
                Image under = Img(box, "Under", BookArt.Round(5f), C("#5a4a30"));
                under.type = Image.Type.Sliced;
                under.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                under.rectTransform.anchorMin = new Vector2(0f, 0f); under.rectTransform.anchorMax = new Vector2(1f, 0f); under.rectTransform.pivot = new Vector2(0.5f, 0f);
                under.rectTransform.sizeDelta = new Vector2(0f, 3f); under.rectTransform.anchoredPosition = Vector2.zero;
                TMP_Text t = T(box, text, 14f, waiting ? Gold : Paper, F.Body, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Fixed(box.gameObject, -1f, 26f);
                box.gameObject.GetComponent<LayoutElement>().minWidth = 28f;
            }
            Clickable(box.gameObject, () => { _capturing = id; Show(_tab, true); });
            Tip(box.gameObject, () => waiting
                ? Lang.F("<b>Натисни клавішу</b>\nМожна з Shift, Ctrl чи Alt.\n<color={0}>Esc — скасувати · Backspace — прибрати · клік деінде — скасувати</color>", GreyHex)
                : (none ? Lang.T("<b>Клавіша не задана</b>\nКлікни й натисни клавішу.") : Lang.F("<b>{0}</b>\nКлікни, щоб змінити (Backspace у режимі вибору — прибрати).", RavenKeys.Label(k)))
                + (clashes.Count > 0 ? Lang.F("\n\n<color=#FF6B6B>!</color> Також на цій клавіші: {0}. Спрацює і те, і дар — клавіші воронів нічого не забирають у гри й інших модів.", string.Join(", ", clashes.ToArray())) : ""));
            return box;
        }

        private static string GiftTip(RavenAbility a)
        {
            string s = "<size=20><color=" + RavenHex(a.Munin) + ">" + a.Plain + "</color></size>";
            if (a.Passive) s += Lang.F("\n<color={0}>пасивний — діє сам</color>", DimHex);
            else if (!RavenAbilities.Stub(a))
            {
                s += Lang.F("\n<color={0}>−{1} сили{2} · перезарядка {3}{4}</color>", GoldHex, RavenAbilities.R(a.Cost()), Satiety.CostNote(a.Munin), RavenAbilities.Span(a.Cooldown()), (a.Lasts() > 0f ? Lang.F(" · діє {0}", RavenAbilities.Span(a.Lasts())) : ""));
            }
            s += "\n\n" + a.Long();
            if (!RavenAbilities.Unlocked(a)) s += "\n\n<color=" + GreyHex + ">" + (RavenAbilities.Stub(a) ? Lang.T("ще в роботі") : Lang.F("відкриється на {0}-му рівні довіри", RavenAbilities.UnlockLevel(a))) + "</color>";
            else if (Trust.Worn(a.Munin) != null) s += "\n\n" + RavenSkills.Status(a);
            if (GiftUp.Upgradable.Contains(a.Id) && RuneBook.Of(Player.m_localPlayer).GoldEver > 0) // golden runes: once the first one is got
            {
                int lv = GiftUp.Level(a.Id);
                s += Lang.F("\n\n<color={0}>Золоті руни: {1}</color>\n", GoldHex, lv > 0 ? Lang.F("рівень {0}", GiftUp.Roman(lv)) : Lang.T("ще нема")) + GiftUp.LevelsText(a.Id);
            }
            return s;
        }

        // ---------------------------------------------------------------- the line under the book

        private static void Footer(int tab)
        {
            if (tab == GiftsTab)
            {
                RectTransform strip = Rect("Keys", _footer);
                HBox(strip, 8f, TextAnchor.MiddleLeft, 12, 12, 8, 8);
                Box(strip, 8f, C("#17130e"), C("#3a2e1c"), 1f);
                strip.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                T(strip, Lang.T("Клавіші"), 16f, Gold, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                KeyItem(strip, Lang.T("Книга"), RavenKeys.BookId);
                KeyItem(strip, Lang.T("Дари"), RavenKeys.GiftsId);
                KeyItem(strip, Lang.T("Накази"), RavenKeys.OrdersId);
                KeyItem(strip, Lang.T("Жердина Мунін"), RavenKeys.PerchMId);
                KeyItem(strip, Lang.T("Хугін"), RavenKeys.PerchHId);
                KeyItem(strip, Lang.T("Обидва"), RavenKeys.PerchBothId);
                Spring(strip);
                TMP_Text clear = T(strip, Lang.T("<u>Прибрати всі клавіші</u>"), 14f, BookArt.PaperDim, F.Body, TextAlignmentOptions.MidlineRight);
                clear.textWrappingMode = TextWrappingModes.NoWrap;
                Clickable(clear.gameObject, () => { RavenKeys.ClearRaven(); Show(_tab, true); });
                Tip(clear.gameObject, () => Lang.F("<b>Прибрати всі клавіші</b>\nУсі клавіші дарів, наказів, трюків і жердин. Клавіші книги ({0}), дарів ({1}) і наказів ({2}) лишаються.", RavenKeys.Label(RavenKeys.Get(RavenKeys.BookId)), RavenKeys.Label(RavenKeys.Get(RavenKeys.GiftsId)), RavenKeys.Label(RavenKeys.Get(RavenKeys.OrdersId))));
            }
            else
            {
                TMP_Text hint = T(_footer, FooterFor(tab), 14f, Muted, F.Body, TextAlignmentOptions.MidlineLeft);
                hint.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
            // (closing: the round × at the book's upper right corner)
        }

        private static void KeyItem(Transform strip, string label, string id)
        {
            RectTransform item = Rect("Key", strip);
            HBox(item, 5f, TextAnchor.MiddleLeft);
            T(item, label, 14f, BookArt.PaperDim, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            KeyBox(item, id);
            if (RavenKeys.Get(id).MainKey != KeyCode.None && RavenKeys.Clashes(id).Count > 0)
                T(item, "<b>!</b>", 14f, C("#FF6B6B"), F.Body).textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static string FooterFor(int tab)
        {
            switch (tab)
            {
                case RavensTab: return Lang.T("Наведи на золотий текст — пояснення · ● на вкладці — є що зробити");
                case GrowTab: return Lang.T("Стрічки «Мунін / Хугін» перемикають ворона · ● — ритуал можна провести");
                case RunesTab: return Lang.T("Наведи на руну — що дає · клік — на якого ворона · ● нова руна");
                default: return Lang.T("Клік по речі — вдягнути · клік по слоту — зняти · у кутках — руни · тягни ворона мишею · ● нова річ");
            }
        }

        // ---------------------------------------------------------------- picking a key

        private static int _releaseFrame = -1;
        private static int _boundFrame = -10;
        internal static bool JustBound { get { return Time.frameCount <= _boundFrame + 1; } }
        private static bool _clicked;
        private static string _clickedOn;

        private static void Capture()
        {
            if (RavenKeys.Down(KeyCode.Escape)) { _capturing = null; Show(_tab, true); return; }
            if (RavenKeys.Down(KeyCode.Backspace) || RavenKeys.Down(KeyCode.Delete))
            {
                RavenKeys.Set(_capturing, KeyboardShortcut.Empty);
                _capturing = null; Show(_tab, true); return;
            }
            // a click anywhere else lets it go (a click on another key field picks that one instead)
            if (RavenKeys.Down(KeyCode.Mouse0)) { _clicked = true; _clickedOn = _capturing; _releaseFrame = -1; }
            if (_clicked && _releaseFrame < 0 && !RavenKeys.Held(KeyCode.Mouse0)) _releaseFrame = Time.frameCount;
            if (_clicked && _releaseFrame >= 0 && Time.frameCount > _releaseFrame)
            {
                _clicked = false;
                if (_capturing == _clickedOn) { _capturing = null; Show(_tab, true); }
                return;
            }
            if (_captureKeys == null) _captureKeys = CaptureKeys();
            foreach (KeyCode k in _captureKeys)
            {
                if (!RavenKeys.Down(k)) continue;
                RavenKeys.Set(_capturing, new KeyboardShortcut(k, RavenKeys.HeldModifiers(k)));
                _boundFrame = Time.frameCount; // this very press must not also work the key just bound (it closed the book)
                _capturing = null; Show(_tab, true); return;
            }
        }

        // every key a person can bind: no left/right click (they work the window), no modifiers alone, no gamepad
        internal static KeyCode[] CaptureKeys()
        {
            List<KeyCode> keys = new List<KeyCode>();
            foreach (KeyCode k in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None || k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k == KeyCode.Escape || k == KeyCode.Backspace || k == KeyCode.Delete) continue;
                if (k >= KeyCode.JoystickButton0 || RavenKeys.IsModifier(k) || !RavenKeys.Known(k)) continue;
                if (!keys.Contains(k)) keys.Add(k);
            }
            return keys.ToArray();
        }

        // ---------------------------------------------------------------- «Розвиток»: trust, the ritual, training

        private static bool _growMunin;

        // which raven a one-raven page shows: the chosen one if it is on you, else the other
        private static bool Choose(ref bool munin, ItemDrop.ItemData hugin, ItemDrop.ItemData mu)
        {
            if (munin && mu == null && hugin != null) munin = false;
            if (!munin && hugin == null && mu != null) munin = true;
            return (munin ? mu : hugin) != null;
        }

        // the ribbons «Мунін / Хугін» over the left page: the shown one in its colour; a gold dot when its ritual can be done
        private static void Switcher(Transform parent, bool current, Action<bool> pick, Func<bool, bool> dot)
        {
            Player p = Player.m_localPlayer;
            foreach (bool which in new bool[] { true, false })
            {
                bool w = which, on = w == current, worn = Trust.Worn(w) != null;
                // a bookmark tab: the chosen one filled in the raven's colour with a darker edge, the other only outlined;
                // drawn fine (mipmapped) so its edges stay smooth on the tilted page; the raven's small portrait before the name
                RectTransform b = Rect("Pick", parent);
                HBox(b, 8f, TextAnchor.MiddleCenter, 10, 16, 5, 5);
                Color raven = w ? BookArt.Munin : BookArt.Hugin, deep = w ? BookArt.MuninDark : BookArt.HuginDark;
                Image bg = b.gameObject.AddComponent<Image>();
                bg.sprite = BookArt.RoundFine(7f, true); bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = BookArt.Fine;
                bg.color = on ? raven : A(raven, 0.1f);
                Image edge = Rect("Edge", b).gameObject.AddComponent<Image>();
                edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Stretch(edge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                edge.sprite = BookArt.FrameFine(7f, on ? 2.5f : 2f, true); edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = BookArt.Fine;
                edge.color = on ? A(deep, 0.85f) : A(raven, 0.55f); edge.raycastTarget = false;
                RectTransform face = Rect("Face", b);
                Fixed(face.gameObject, 24f, 24f);
                Img(face, "Ring", BookArt.Circle, on ? A(deep, 0.9f) : A(raven, 0.6f));
                Image fill = Img(face, "Fill", BookArt.Circle, C("#efe3c4"));
                Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
                Image pic = Img(face, "Raven", BookArt.Portrait(w) ?? RavenItem.Icon(w), Color.white);
                Stretch(pic.rectTransform, Vector2.zero, Vector2.one, new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f));
                pic.preserveAspect = true;
                TMP_Text t = T(b, Plugin.RavenName(w), 17f, on ? (w ? SealInk : C("#0e2030")) : (w ? C("#7a3020") : BookArt.HuginInk), F.Head, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (!worn) b.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
                if (dot != null && dot(w)) NewDot(b, 12f, 4f);
                if (!on && worn)
                {
                    Button bt = b.gameObject.AddComponent<Button>();
                    bt.targetGraphic = bg;
                    Tint(bt);
                    bt.onClick.AddListener(() => pick(w));
                }
            }
        }

        private static void PageGrow(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            Player p = Player.m_localPlayer;
            bool m = _growMunin;
            bool any = Choose(ref m, hugin, munin);
            _growMunin = m;
            ItemDrop.ItemData item = m ? munin : hugin;
            RectTransform left = Column("Left", 56f, 40f, 500f, 9f);
            RectTransform right = Column("Right", 644f, 40f, 500f, 10f);

            RectTransform top = Rect("Top", left);
            HBox(top, 6f, TextAnchor.LowerLeft);
            Switcher(top, m, w => { _growMunin = w; Show(GrowTab, true); }, w => RiteNow(p, w));
            Spring(top);
            if (!any)
            {
                T(left, Lang.T("Жодного ворона нема на плечі. Посади ворона (правий клік у сумці), щоб бачити його довіру й вишкіл. Повернути загубленого — на вкладці «Ворони»."), 16f, InkSoft, F.Italic);
                return;
            }
            int level = Trust.Level(item);
            T(top, Lang.F("Рівень {0}/{1} · <color={2}><b>{3}</b></color>", level, Trust.MaxLevel, GoldInkHex, Trust.Rank(m, level)), 15f, Ink, F.Italic, TextAlignmentOptions.BottomRight).textWrappingMode = TextWrappingModes.NoWrap;

            if (Trust.Stranger(item, p))
            {
                T(left, Lang.F("Це чужий ворон (власник — {0}): довіра, годування й ритуали — лише з ним.", (Trust.OwnerName(item) ?? Lang.T("хтось інший"))), 16f, Bad, F.Italic);
                Training(right, m, item);
                return;
            }

            TMP_Text cap = T(left, Lang.T("ДРАБИНА ДОВІРИ"), 14f, Caption, F.Body);
            cap.characterSpacing = 12f;
            Ladder(left, m, level);
            float need = Trust.Need(item);
            ItemDrop.ItemData it = item;
            if (level == 0) Meter(left, Lang.T("Спить"), () => TrustTip(m, it), () => Lang.T("розбуди ритуалом"), () => 0f, "brush-gold", BarKey(m, Lang.T("Рівень")));
            else if (need > 0f) Meter(left, Lang.F("До рівня {0}", (level + 1)), () => TrustTip(m, it), () => Mathf.FloorToInt(Trust.Points(it)) + " / " + Mathf.RoundToInt(Trust.Need(it)) + (Trust.Full(it) ? Lang.T(" · повна") : ""), () => Trust.Points(it) / Mathf.Max(1f, Trust.Need(it)), "brush-gold", BarKey(m, Lang.T("Рівень")));
            else Meter(left, Lang.T("Найвищий рівень"), () => TrustTip(m, it), () => Lang.T("довіряє цілком"), () => 1f, "brush-gold", BarKey(m, Lang.T("Рівень")));
            if (level > 0 && need > 0f)
            {
                TMP_Text food = T(left, Lang.F("<color={0}><b>Їжа дає довіру</b></color>: лише голодному (ситість нижче {1}), до {2} за день — сьогодні лишилось {3}", GoldInkHex, RavenAbilities.R(Satiety.AskAt), RavenAbilities.R(Trust.FoodDailyCap), RavenAbilities.R(Trust.FoodLeftToday(item))), 14f, InkSoft, F.Body);
                Tip(food.gameObject, () => HungerTip(m, it));
            }

            if (Trust.Away(item))
                T(right, Lang.F("{0} Поки його нема, не нагодуєш і ритуал не проведеш.", Trust.AwayText(m, item).Replace("\n", " ")), 15f, Bad, F.Italic);
            else
            {
                Rite rite = RiteFor(p, item);
                if (rite != null) RitualBlock(right, p, m, item, rite);
            }
            Divider(right);
            Training(right, m, item);
        }

        private static void Divider(Transform parent)
        {
            Image d = Img(parent, "Divider", BookArt.Fade2, new Color(0.227f, 0.141f, 0.078f, 0.5f));
            Fixed(d.gameObject, -1f, 1f);
        }

        // seven rungs: the number in a circle (reached: gold, the current one: pale, ahead: empty), the gifts it opens as
        // icons, their names and what the level adds; the current rung tinted in the raven's colour
        private static void Ladder(Transform parent, bool m, int level)
        {
            int max = Trust.MaxLevel;
            for (int lv = 1; lv <= max; lv++)
            {
                int l = lv;
                bool done = lv < level, now = lv == level;
                RectTransform row = Rect("Rung" + lv, parent);
                HBox(row, 10f, TextAnchor.MiddleLeft, 8, 8, 3, 3);
                // the current rung in the raven's own colour: a tinted band with its frame, the number on a seal of his colour
                Color raven = m ? BookArt.Munin : BookArt.Hugin;
                if (now) Box(row, 6f, A(raven, 0.3f), m ? BookArt.Munin : BookArt.HuginInk, 1.5f);
                RectTransform num = Rect("Number", row);
                Fixed(num.gameObject, 28f, 28f);
                Img(num, "Ring", BookArt.Circle, now ? (m ? BookArt.MuninDark : BookArt.HuginInk) : Ink);
                Image fill = Img(num, "Fill", BookArt.Circle, done ? C("#a8843a") : now ? raven : C("#e6d3a8"));
                Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
                if (!done && !now) fill.color = new Color(0.93f, 0.86f, 0.7f, 0f);
                TMP_Text n = T(num, lv.ToString(), 16f, done || now ? SealInk : Ink, F.Head, TextAlignmentOptions.Center);
                Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Tip(num.gameObject, () => LevelTip(m, l));

                List<RavenAbility> gifts = new List<RavenAbility>();
                foreach (RavenAbility g in RavenAbilities.All) if (g.Munin == m && RavenAbilities.UnlockLevel(g) == lv) gifts.Add(g);
                RectTransform icons = Rect("Icons", row);
                HBox(icons, 3f, TextAnchor.MiddleLeft);
                Fixed(icons.gameObject, 88f, 26f);
                int shown = 0;
                foreach (RavenAbility g in gifts)
                {
                    if (shown++ >= 3) break;
                    RavenAbility gg = g;
                    RectTransform ic = Rect("Gift", icons);
                    Fixed(ic.gameObject, 26f, 26f);
                    Image im = Img(ic, "Icon", AbilityIcons.For(g.Id, g.Munin), new Color(1f, 1f, 1f, lv <= Mathf.Max(level, 1) ? 1f : 0.35f));
                    im.preserveAspect = true;
                    Tip(ic.gameObject, () => GiftTip(gg));
                }
                if (gifts.Count == 0 && lv < max)
                {
                    RectTransform ph = Rect("None", icons);
                    Fixed(ph.gameObject, 26f, 26f);
                    Img(ph, "Dot", BookArt.Circle, new Color(0.15f, 0.16f, 0.19f, lv <= level ? 0.9f : 0.35f));
                }
                Spring(icons);

                List<string> names = new List<string>();
                foreach (RavenAbility g in gifts) names.Add(g.Bare);
                if (lv == Outfits.WardrobeLevel) names.Add(Lang.T("спорядження"));
                if (lv == max) names.Add(Lang.F("найвища довіра · {0}", Trust.Rank(m, max))); // (Odin gives no wardrobe at the top: his gifts come one per ritual)
                if (names.Count == 0) names.Add(Lang.T("більше сили"));
                RectTransform words = Rect("Words", row);
                VBox(words, 0f, TextAnchor.MiddleLeft);
                words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                Color tc = lv > Mathf.Max(level, 1) ? new Color(0.227f, 0.141f, 0.078f, 0.55f) : Ink;
                Shrink(T(words, now ? "<b>" + string.Join(", ", names.ToArray()) + "</b>" : string.Join(", ", names.ToArray()), 15f, now ? (m ? BookArt.MuninDark : BookArt.HuginInk) : tc, F.Body), 11f);
                // what the level gives, told in full on hover (the whole words block): gold ink, as all hoverable text on the paper
                Shrink(T(words, Lang.F("Запас сили {0}{1}", RavenAbilities.R(StrengthAt(lv)), (lv == 1 ? Lang.T(" · основа") : Lang.T(" · дари сильніші"))), 14f, lv > Mathf.Max(level, 1) ? A(GoldInk, 0.6f) : GoldInk, F.Body), 11f);
                Tip(words.gameObject, () => LevelTip(m, l));
            }
        }

        private static float StrengthAt(int lv)
        {
            int steps = Mathf.Max(1, Trust.MaxLevel - 1);
            return Mathf.Round(Trust.MinStrength + (Trust.MaxStrength - Trust.MinStrength) * (lv - 1) / steps);
        }

        // Everything a trust level gives, with the real numbers: the gifts it opens, and every open gift's price,
        // pause and length on this level (what changed against the level before in grey). The numbers come from the
        // gifts themselves, worked out as if the worn raven were on that level (Trust.LevelAs), so they are what the
        // game will do: the gear and the raven's own knack count in them as now.
        private static string LevelTip(bool m, int lv)
        {
            string s = Lang.F("<size=20><color={0}>Рівень {1} — {2}</color></size>", RavenHex(m), lv, Trust.Rank(m, lv));
            float str = StrengthAt(lv);
            s += Lang.F("\n\nЗапас сили: до <b>{0}</b>{1}", RavenAbilities.R(str), (lv > 1 ? Lang.F(" <color={0}>(було {1})</color>", DimHex, RavenAbilities.R(StrengthAt(lv - 1))) : ""));

            List<RavenAbility> opens = new List<RavenAbility>();
            foreach (RavenAbility g in RavenAbilities.All) if (g.Munin == m && RavenAbilities.UnlockLevel(g) == lv) opens.Add(g);
            List<string> more = new List<string>();
            if (lv == Outfits.WardrobeLevel) more.Add(Lang.T("спорядження: шапки, намиста й інші речі для ворона"));
            if (lv >= Outfits.WardrobeLevel && lv < Outfits.TopLevel) more.Add(Lang.T("після ритуалу на цей рівень ворон може принести від Одіна річ"));
            if (lv == Trust.MaxLevel) more.Add(Lang.T("найвища довіра: усе спорядження відкрите"));
            if (m && lv == Ward.TwoBlowsAt) more.Add(Lang.T("«Заслона» витримує два удари"));
            if (m && lv == Ward.ThreeBlowsAt) more.Add(Lang.T("«Заслона» витримує три удари"));
            if (opens.Count > 0 || more.Count > 0)
            {
                s += Lang.F("\n\n<color={0}><b>Відкриває</b></color>", GoldHex);
                foreach (RavenAbility g in opens) s += "\n• <b>" + g.Plain + "</b> — " + Lower(g.Short);
                foreach (string x in more) s += "\n• " + x;
            }
            if (lv <= Trust.WanderChance.Length) s += Lang.F("\n\n<color={0}>Ще дичиться: часом сам відлітає на кілька хвилин{1}.</color>", DimHex, (lv > 1 ? Lang.T(", уже рідше") : ""));

            // the open gifts on this level, against the level before
            List<string> rows = new List<string>();
            foreach (RavenAbility g in RavenAbilities.All)
            {
                if (g.Munin != m || RavenAbilities.Stub(g) || RavenAbilities.UnlockLevel(g) > lv || Loyal.Is(g)) continue;
                float cost, wait, lasts, wasCost = -1f, wasWait = -1f, wasLasts = -1f;
                GiftAt(g, lv, out cost, out wait, out lasts);
                if (lv > RavenAbilities.UnlockLevel(g)) GiftAt(g, lv - 1, out wasCost, out wasWait, out wasLasts);
                string r = "• <b>" + g.Plain + "</b>: ";
                List<string> parts = new List<string>();
                if (cost > 0.5f) parts.Add(Lang.F("{0} сили{1}", RavenAbilities.R(cost), Was(wasCost >= 0f && Mathf.Abs(wasCost - cost) >= 0.5f, RavenAbilities.R(wasCost))));
                if (wait > 1f) parts.Add((g.Passive ? Lang.T("раз на ") : Lang.T("пауза ")) + RavenAbilities.Span(wait) + Was(wasWait >= 0f && Mathf.Abs(wasWait - wait) >= 1f, RavenAbilities.Span(wasWait)));
                if (lasts > 0.5f) parts.Add(Lang.F("діє {0}{1}", RavenAbilities.Span(lasts), Was(wasLasts >= 0f && Mathf.Abs(wasLasts - lasts) >= 0.5f, RavenAbilities.Span(wasLasts))));
                if (parts.Count == 0) continue;
                rows.Add(r + string.Join(", ", parts.ToArray()));
            }
            if (rows.Count > 0) s += Lang.F("\n\n<color={0}><b>Дари на цьому рівні</b></color> <color={1}>(у дужках — як було рівнем нижче)</color>\n<size=15>{2}</size>", GoldHex, DimHex, string.Join("\n", rows.ToArray()));
            if (lv > 1) s += Lang.F("\n\n<color={0}>Удари воронів («Удар в очі», «Кігті») з кожним рівнем ще й б'ють сильніше — дивись «Вишкіл».</color>", DimHex);
            return s;
        }

        // a gift's price, pause and length as they would be on that level
        private static void GiftAt(RavenAbility g, int lv, out float cost, out float wait, out float lasts)
        {
            int was = Trust.LevelAs;
            Trust.LevelAs = Mathf.Clamp(lv, 1, Trust.MaxLevel);
            try { cost = g.Cost(); wait = g.Cooldown(); lasts = g.Lasts(); }
            catch { cost = 0f; wait = 0f; lasts = 0f; }
            finally { Trust.LevelAs = was; }
        }

        private static string Was(bool changed, string before) { return changed ? " <color=" + DimHex + ">(" + before + ")</color>" : ""; }

        private static string Lower(string text) { return Lang.LowerFirst(text); }

        private static readonly Color MetLine = new Color(0.184f, 0.353f, 0.165f, 0.5f), MissLine = new Color(0.557f, 0.118f, 0.118f, 0.5f);

        private static void RitualBlock(Transform col, Player p, bool m, ItemDrop.ItemData item, Rite r)
        {
            RectTransform head = Rect("Head", col);
            HBox(head, 10f, TextAnchor.LowerLeft);
            TMP_Text t = T(head, r.Waking ? Lang.T("ПРОБУДЖЕННЯ") : Lang.F("РИТУАЛ · РІВЕНЬ {0}", (Trust.Level(item) + 1)), 14f, Caption, F.Body);
            t.characterSpacing = 12f; t.textWrappingMode = TextWrappingModes.NoWrap;
            T(head, Lang.F("умов виконано {0} з {1}", r.Met, r.Count), 15f, Ink, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(head);

            RectTransform grid = Rect("Conditions", col);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            int cols = r.Count == 4 ? 2 : 3;
            g.cellSize = new Vector2(Mathf.Floor((500f - (cols - 1) * 6f) / cols), r.Count == 4 ? 64f : 78f);
            g.spacing = new Vector2(6f, 6f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
            if (!r.Waking)
            {
                float need = Trust.Need(item), have = Trust.Points(item);
                Condition(grid, r.Bar, Lang.T("Шкала довіри повна"), Mathf.FloorToInt(have) + " / " + Mathf.RoundToInt(need) + (r.Bar ? "" : Lang.F(" · ще {0}", Mathf.CeilToInt(need - have))),
                    Lang.F("<b>Шкала довіри повна</b>\n\nДовіра {0}-го рівня має бути набрана до кінця: {1} очок, зараз {2}.\n\nДовіра росте, коли годуєш голодного ворона (тим, що він любить) і коли ви разом живете — у мирі й у бою. Повна шкала замерзає: до ритуалу довіра вже не тане.", (Trust.Level(item) + 1), Mathf.RoundToInt(need), Mathf.FloorToInt(have)));
            }
            string boss = BossOf(r.Step.Prefab);
            Condition(grid, r.Heads, Lang.T("Трофеї в сумці"), Lang.F("{0} · є {1} з {2}", r.Step.Name, r.Have, r.Step.Count),
                Lang.F("<b>Трофеї в сумці</b>\n\nУ твоїй сумці мають бути {0} ×{1} — трофеї боса <color={2}>{3}</color>. Зараз є {4}.\n\nТрофей падає з боса, коли ти його вбиваєш, тож боса доведеться перемогти не раз. Під час обряду ворон з'їсть ці трофеї. Кожен наступний рівень просить трофеї наступного боса.", r.Step.Name, r.Step.Count, GoldHex, boss, r.Have));
            Condition(grid, r.Stones, Lang.T("Біля Жертовних каменів"), r.Stones ? Lang.T("ти біля каменів") : r.Distance > 0f ? Lang.F("далеко · ~{0} м", Mathf.RoundToInt(r.Distance)) : Lang.T("далеко"),
                Lang.F("<b>Біля Жертовних каменів</b>\n\nЖертовні камені — коло великих каменів там, де ти вперше з'явився у світі (місце, куди тебе приніс ворон на початку гри). На кожному з них вішають трофей переможеного боса.\n\nОбряд проводиться лише поруч із ними — ближче ніж {0} м.", RavenAbilities.R(Trust.RitualRange)));
            Condition(grid, r.Hung, Lang.T("Трофей на камені"), r.HungRaw == true ? (r.HungLive ? Lang.T("висить на камені") : Lang.T("висить (востаннє бачено)")) : r.HungRaw == false ? (r.HungLive ? Lang.T("не висить — повісь його") : Lang.T("не висів, коли ти був біля каменів")) : Lang.T("ще не бачено — підійди до каменів"),
                Lang.F("<b>Трофей на камені боса</b>\n\nНа Жертовному камені боса <color={0}>{1}</color> має висіти його трофей ({2}) — як у грі: підійди до каменя з трофеєм у сумці й повісь його. Цей трофей лишається на камені, ворон його не їсть.\n\nВорон приймає голови лише боса, якого ти справді переміг і вшанував. {3}", GoldHex, boss, r.Step.Name, (r.HungRaw == null ? Lang.T("Чи висить трофей, видно, коли ти біля каменів: тоді ворони це запам'ятають.") : r.HungRaw == true ? (r.HungLive ? Lang.T("Трофей уже висить.") : Lang.T("Трофей висів, коли ти востаннє був біля каменів.")) : (r.HungLive ? Lang.T("Зараз камінь порожній.") : Lang.T("Коли ти востаннє був біля каменів, камінь був порожній.")))));

            RectTransform act = Rect("Act", col);
            HBox(act, 12f, TextAnchor.MiddleLeft);
            string say = r.All ? Lang.F("Усе готово — можна {0}", (r.Waking ? Lang.T("будити.") : Lang.T("проводити ритуал.")))
                : r.Off != null && r.Met == r.Count ? Lang.F("{0} зараз {1} — ритуал, коли повернеться", Plugin.RavenName(m), r.Off)
                : Lang.F("Бракує: {0}", r.Missing);
            TMP_Text miss = T(act, say, 14f, r.All ? Good : Bad, F.Body);
            miss.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform ritualSeal = SealButton(act, r.Waking ? Lang.T("Розбудити") : Lang.T("Провести"), r.All ? (m ? "munin" : "hugin") : "stone", 70f, 14f, () => Act(() => Trust.Ritual(p, m)), r.All, r.All ? 1f : 0.6f);
            // under the cursor: every condition still missing, in words (the same ones the ritual itself checks)
            ItemDrop.ItemData it = item;
            SealTip(ritualSeal, () =>
            {
                List<string> lines = new List<string>();
                if (!r.Waking && !r.Bar) lines.Add(Lang.F("• шкала довіри ще не повна: {0} / {1}", Mathf.FloorToInt(Trust.Points(it)), Mathf.RoundToInt(Trust.Need(it))));
                if (!r.Heads) lines.Add(Lang.F("• у сумці {0} з {1}: {2}", r.Have, r.Step.Count, r.Step.Name));
                if (!r.Stones) lines.Add(Lang.F("• ти не біля Жертовних каменів{0}", (r.Distance > 0f ? Lang.F(" (~{0} м)", Mathf.RoundToInt(r.Distance)) : "")));
                if (!r.Hung) lines.Add(Lang.T("• на камені боса не висить його трофей"));
                if (r.Off != null) lines.Add(Lang.F("• {0} зараз {1}", Plugin.RavenName(m), r.Off));
                string sulk = Quirks.Sulk(p, m);
                if (sulk != null) lines.Add(Lang.F("• {0} — обряду не прийме, поки не відійде", sulk));
                string title = "<b>" + (r.Waking ? Lang.T("Розбудити") : Lang.T("Провести ритуал")) + "</b>";
                return lines.Count == 0
                    ? Lang.F("{0}\nУсе готово: ворон з'їсть трофеї й полетить до Одіна, а повернеться на новому рівні.", title)
                    : Lang.F("{0} <color=#8C93A1>· зараз не можна</color>\n{1}", title, string.Join("\n", lines.ToArray()));
            });
        }

        // the boss a trophy comes from, by its name in the strike table; the trophy's own name if it is not there
        private static string BossOf(string trophy)
        {
            for (int i = 0; i < BossTrophies.Length && i < Strike.BossNames.Length; i++) if (BossTrophies[i] == trophy) return Strike.BossNames[i];
            return trophy;
        }

        private static void Condition(Transform grid, bool met, string title, string sub, string tip)
        {
            RectTransform c = Rect("Condition", grid);
            Torn(c, BookArt.Card, met ? MetLine : MissLine, 1f);
            VBox(c, 2f, TextAnchor.UpperLeft, 8, 8, 6, 6);
            RectTransform head = Rect("Head", c);
            HBox(head, 6f, TextAnchor.MiddleLeft);
            RectTransform mark = Rect("Mark", head);
            Fixed(mark.gameObject, 16f, 16f);
            Img(mark, "Glyph", met ? BookArt.Check : BookArt.Cross, met ? Good : Bad);
            TMP_Text t = T(head, title, 14f, Ink, F.Body);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text s = T(c, sub, 14f, Brown, F.Italic);
            s.enableAutoSizing = true; s.fontSizeMin = 11f; s.fontSizeMax = 14f;
            Tip(c.gameObject, () => tip);
        }

        // ---------------------------------------------------------------- the training of the strike

        private static readonly string[] BossTrophies = { "TrophyEikthyr", "TrophyTheElder", "TrophyBonemass", "TrophyDragonQueen", "TrophyGoblinKing", "TrophySeekerQueen", "TrophyFader" };
        private static string[] BossShort { get { return Lang.Arr(BossShortSrc); } } // (translated on every read)
        private static readonly string[] BossShortSrc = Lang.Keys("Ей", "Пр", "Кс", "Мд", "Яг", "Кр", "Фд");

        private static void Training(Transform col, bool m, ItemDrop.ItemData item)
        {
            int have, need;
            int level = Strike.Level(item, out have, out need);
            RectTransform head = Rect("Head", col);
            HBox(head, 10f, TextAnchor.LowerLeft);
            TMP_Text cap = T(head, Lang.T("ВИШКІЛ"), 14f, Caption, F.Body);
            cap.characterSpacing = 12f; cap.textWrappingMode = TextWrappingModes.NoWrap;
            T(head, m ? Lang.T("Кігті") : Lang.T("Удар в очі"), 19f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(head);
            T(head, Lang.F("Вильотів разом: {0}", Strike.Flights(item)), 14f, InkSoft, F.Body, TextAlignmentOptions.BottomRight).textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform row = Rect("Level", col);
            HBox(row, 16f, TextAnchor.MiddleLeft);
            RectTransform seal = SealButton(row, "<size=28>" + level + "</size><size=14>/" + Strike.MaxLevel + "</size>", m ? "munin" : "hugin", 76f, 28f, null, true, 1f); // no action: a seal to look at (it lights up under the cursor)
            Tip(seal.gameObject, () => Lang.F("<b>Вишкіл {0} із {1}</b>\n\nКожен виліт, що когось вразив, додає досвіду; політ у порожнечу — ні. Усього вильотів: {2}.", level, Strike.MaxLevel, Strike.Flights(item)));
            RectTransform prog = Rect("Progress", row);
            VBox(prog, 3f, TextAnchor.MiddleLeft);
            prog.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform ph = Rect("Head", prog);
            HBox(ph, 6f, TextAnchor.MiddleLeft);
            T(ph, need > 0 ? Lang.F("До {0}-го", (level + 1)) : Lang.T("Найвищий"), 14f, Ink, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(ph);
            if (need > 0) T(ph, Lang.F("{0} / {1} вильотів", have, need), 14f, Ink, F.Body, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
            int h2 = have, n2 = need;
            BrushBar(prog, () => n2 > 0 ? h2 / (float)n2 : 1f, m ? "brush-munin" : "brush-huginn", 7f, 11f, null, BarKey(m, Lang.T("Вишкіл")));

            float blow = Strike.Blow(m, item);
            bool starving = Strike.StarveMul(item) < 1f;
            string cut = !starving ? ""
                : Trust.Starving(item) ? Lang.F("<color=#FF6B6B><b>Голод: удар −{0}%</b> — ситість 0. Нагодуй, і удар повернеться.</color>\n\n", RavenAbilities.R(Trust.StarveStrikePct))
                : Lang.F("<color=#B48CE6><b>Об'ївся: удар −{0}%</b> — лінується, поки ситість понад {1}.</color>\n\n", RavenAbilities.R(Satiety.StuffedStrikePct), RavenAbilities.R(Satiety.StuffedAt));
            RectTransform stats = Rect("Stats", col);
            HBox(stats, 28f, TextAnchor.UpperLeft);
            Stat(stats, Lang.T("Урон"), RavenAbilities.R(blow), starving, () => Lang.F("<b>Удар · {0}</b> <color={1}>({2} шкода)</color>\n\n{3}{4}", RavenAbilities.R(blow), GreyHex, (m ? Lang.T("рубляча") : Lang.T("колюча")), cut, Strike.Why(m, item)));
            Stat(stats, Lang.T("Крит"), RavenAbilities.R(blow * 2f), starving, () => Lang.F("<b>Крит · {0}</b>\n\n{1}Удар ×2.\n{2}\nПодвійного удару: {3}%{4}\n\nХто не помітив ворона, отримує перший удар критом (крім як у Тіні).\n{5}\nБоса один виліт ранить не більше ніж на {6}% здоров'я.", RavenAbilities.R(blow * 2f), cut, Strike.CritLine(m, item), RavenAbilities.R(Strike.DoubleChance(m, item)), (m ? Lang.F("\nПотрійного: {0}%", RavenAbilities.R(Strike.TripleChance(m, item))) : ""), (m ? Lang.T("Рве до 3 ворогів поруч; серія — лише по першому.") : Lang.T("Б'є одну ціль — ту, на яку ти дивишся.")), RavenAbilities.R((m ? Strike.BossCapMunin : Strike.BossCapHugin) * 100f)));
            Stat(stats, Lang.T("Дальність"), Lang.F("{0} м", RavenAbilities.R(Strike.RangeOf(item))), false, () => Lang.F("<b>Дальність польоту · {0} м</b>\n\n{1}", RavenAbilities.R(Strike.RangeOf(item)), Strike.RangeWhy(m, item)));
            Spring(stats);

            T(col, Lang.T("<b>Ступені за босів</b>"), 14f, GoldInk, F.Body);
            RectTransform icons = Rect("Bosses", col);
            HBox(icons, 5f, TextAnchor.MiddleLeft);
            int tier = Strike.Tier(item);
            List<string> seen = Strike.BossesSeen(item);
            for (int i = 0; i < Strike.Bosses.Length; i++)
            {
                int idx = i;
                bool beaten = seen.Contains(Strike.Bosses[i]);
                bool next = i == tier && !beaten;
                RectTransform cell = Rect("Boss", icons);
                Fixed(cell.gameObject, 40f, 40f);
                // a dark cell under every head (as the game's item slots), the state by its frame and the head's light:
                // beaten — gold frame, the head bright; next — the raven's frame, nearly bright; ahead — dim frame, the head
                // muted but plain to see
                Color line = beaten ? C("#c9a24a") : next ? (m ? BookArt.Munin : BookArt.Hugin) : new Color(0.227f, 0.141f, 0.078f, 0.45f);
                Color fill = beaten ? C("#2e2416") : next ? C("#2a2218") : C("#3a3024");
                Box(cell, 5f, fill, line, beaten || next ? 3f : 2.5f); // (a thicker frame: a thin one stepped on the tilted page)
                // the boss's head (its trophy); its two letters only when the game has no picture of it
                Sprite trophy = i < BossTrophies.Length ? ItemIcon(BossTrophies[i]) : null;
                if (trophy != null)
                {
                    // not beaten yet (the next one too): out of focus — you know a boss is there, not its face
                    Image ic = Img(cell, "Head", beaten ? trophy : BookArt.Blurred(trophy), beaten ? Color.white : next ? new Color(0.92f, 0.9f, 0.86f, 0.9f) : new Color(0.72f, 0.68f, 0.62f, 0.7f));
                    Stretch(ic.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                    ic.preserveAspect = true;
                }
                else
                {
                    string abbr = i < BossShort.Length ? BossShort[i] : (Strike.BossNames[i].Length > 1 ? Strike.BossNames[i].Substring(0, 2) : Strike.BossNames[i]);
                    TMP_Text t = T(cell, abbr, 14f, beaten ? SealInk : next ? BookArt.RavenInk(m) : new Color(0.227f, 0.141f, 0.078f, 0.4f), F.Head, TextAlignmentOptions.Center);
                    Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
                Tip(cell.gameObject, () => "<b>" + Strike.BossNames[idx] + "</b>\n" + (beaten ? Lang.T("переможений з ним на плечі") : next ? Lang.F("наступний: удар {0} → {1}", RavenAbilities.R(blow), RavenAbilities.R(Strike.BlowWithTier(m, item, idx + 1))) : Lang.T("ще попереду")));
            }
            Spring(icons);
            T(col, tier < Strike.Bosses.Length ? Lang.F("далі: {0} · удар {1} → {2}", Strike.BossNames[tier], RavenAbilities.R(blow), RavenAbilities.R(Strike.BlowWithTier(m, item, tier + 1))) : Lang.T("усі боси переможені з ним"), 14f, InkSoft, F.Italic);
        }

        // a number with its gold name over it; cut: weakened by hunger (red, with how much)
        private static void Stat(Transform parent, string label, string value, bool cut, Func<string> tip)
        {
            RectTransform s = Rect("Stat", parent);
            VBox(s, 0f, TextAnchor.UpperLeft).childForceExpandWidth = false;
            T(s, "<b>" + label + "</b>", 14f, GoldInk, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            string shown = cut ? value + " <size=55%>−" + RavenAbilities.R(Trust.StarveStrikePct) + "%</size>" : value;
            T(s, shown, 30f, cut ? Bad : Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            Tip(s.gameObject, tip);
        }

        // ---------------------------------------------------------------- «Спорядження»

        private static bool _dressMunin;
        private static string _editing;           // the piece the fitting buttons move (tuning only)
        private static ItemDrop.ItemData _dressed; // the raven the page shows: its new pieces count as seen when you leave

        private static void LeaveOutfits()
        {
            if (_dressed != null) { Outfits.MarkSeen(_dressed); Nicks.MarkSeen(_dressed); }
            _dressed = null;
        }

        private static void PageOutfits(ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            Player p = Player.m_localPlayer;
            bool m = _dressMunin;
            if (!Choose(ref m, hugin, munin))
            {
                OutfitStage.Close();
                RectTransform empty = Column("Left", 56f, 40f, 500f, 10f);
                T(empty, Lang.T("Жодного ворона нема на плечі. Посади ворона (правий клік у сумці), щоб його вбрати."), 16f, InkSoft, F.Italic);
                return;
            }
            _dressMunin = m;
            ItemDrop.ItemData item = m ? munin : hugin;
            if (_dressed != item) LeaveOutfits();
            _dressed = item;
            bool open = Outfits.Tuning || Trust.SettledLevel(item) >= Outfits.WardrobeLevel;

            // left: the ribbons, the raven turning in its round window, the four places round it
            RectTransform left = Rect("Left", _pages);
            At(left, 56f, 40f, 500f, 600f);
            RectTransform top = Rect("Top", left);
            At(top, 0f, 0f, 500f, 34f);
            HBox(top, 6f, TextAnchor.LowerLeft);
            Switcher(top, m, w => { LeaveOutfits(); _dressMunin = w; Show(_tab, true); }, w => w != m && OutfitNews(p, w)); // the one shown counts as seen
            Spring(top);

            RectTransform hole = Rect("Window", left);
            At(hole, 85f - 11f, 110f - 11f, 352f, 352f);
            Img(hole, "Rim", BookArt.Porthole, Color.white);
            bool shown = false;
            if (!open)
            {
                OutfitStage.Close();
                TMP_Text say = T(hole, Lang.F("{0}\n\n<size=70%>Спорядження відкриється на {1}-му рівні довіри.</size>", (m
                    ? Lang.T("«Тільки спробуй щось на мене натягнути. Без пальців лишишся.»")
                    : Lang.T("«Ще рано мене вбирати. Спершу доведи, що з тобою не соромно показатися на людях.»")), Outfits.WardrobeLevel), 19f, C("#c8ae7a"), F.Italic, TextAlignmentOptions.Center);
                Stretch(say.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 50f), new Vector2(-50f, -50f));
            }
            else if (OutfitStage.Show(m, Trust.ShownLevel(m, item)))
            {
                shown = true;
                RectTransform mask = Rect("Mask", hole);
                Stretch(mask, Vector2.zero, Vector2.one, new Vector2(31f, 31f), new Vector2(-31f, -31f));
                Image mi = mask.gameObject.AddComponent<Image>();
                mi.sprite = BookArt.Circle;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                RawImage img = Rect("Picture", mask).gameObject.AddComponent<RawImage>();
                Stretch(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                img.texture = OutfitStage.Texture;
                img.gameObject.AddComponent<RavenBookDrag>();
                TMP_Text hint = T(hole, Lang.T("↔ тягни мишею"), 14f, C("#9ab0c4"), F.Body, TextAlignmentOptions.Bottom);
                Stretch(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 33f), new Vector2(0f, 55f));
            }
            SlotCircle(left, m, item, Outfits.Head, 206f, 12f, true);
            SlotCircle(left, m, item, Outfits.Neck, 10f, 236f, false);
            SlotCircle(left, m, item, Outfits.Back, 402f, 236f, false);
            SlotCircle(left, m, item, Outfits.Legs, 206f, 440f, false);
            if (_tab != FitTab) // the corners: what its runes give (the runes themselves are on «Шлях Одіна»), its golden seal
            {
                RuneSlot(left, p, m, item, WearKind.Freya, 0f, 40f, true);
                RuneSlot(left, p, m, item, WearKind.Baldr, 412f, 40f, true);
                RuneSlot(left, p, m, item, WearKind.Heimdall, 0f, 470f, false);
                GoldSeal(left, p, m, item, 412f, 470f);
            }
            if (shown && _tab == FitTab) PreviewButtons(left);

            // right: what it may wear, by place; the pouch's own row
            RectTransform right = Column("Right", 644f, 40f, 500f, 10f);
            string away = open ? Shoulder.NotOnShoulder(p, m) : null;
            if (away != null) T(right, away, 15f, Bad, F.Italic); // things change only on the shoulder
            if (_tab != FitTab) NickBlock(right, m, item);
            if (_tab == FitTab && Outfits.Tuning) FitPanel(right, m, item);
            // the wardrobe still shut (asleep, or below its level): no pieces at all, not even the ones its trust opens
            // later — only when it opens (the hats by level are the same for both ravens; Odin's gifts are each its own)
            if (!open)
            {
                T(right, Outfits.SlotTitles[Outfits.Head], 20f, Ink, F.Head);
                T(right, Lang.F("{0}Спорядження відкриється на {1}-му рівні довіри: тоді тут з'являться шапки, які він може носити.", (Trust.Level(item) == 0 ? Lang.F("{0} ще спить. ", Plugin.RavenName(m)) : ""), Outfits.WardrobeLevel), 15f, InkSoft, F.Italic);
                return;
            }
            List<Outfits.Entry> all = Outfits.Catalog();
            for (int slot = Outfits.Head; slot < Outfits.Slots; slot++)
            {
                int s = slot;
                List<Outfits.Entry> every = all.FindAll(e => e.Slot == s && Outfits.Listed(e));
                if (every.Count == 0) continue;
                bool things = s != Outfits.Head;
                // the head: only what this raven may wear now (what trust or Odin opens later is a riddle: never shown);
                // the rest: things made at the raven table that it wears or that are in your bag
                List<Outfits.Entry> mine = every.FindAll(e => open && (things && e.Gear != null && !Outfits.OwnHat(e)
                    ? Outfits.Tuning || Outfits.Worn(item, e.Slot) == e.Prefab || Outfits.CountInBag(p, e) > 0
                    : Outfits.IsOpen(item, e)));
                if (things && mine.Count == 0) continue;
                RectTransform head = Rect("Head", right);
                HBox(head, 6f, TextAnchor.LowerLeft);
                T(head, Outfits.SlotTitles[s], 20f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
                if (mine.Exists(e => Outfits.IsNew(item, e))) NewDotInline(head);
                Spring(head);
                TMP_Text cnt = T(head, things ? Lang.F("{0} із {1}", mine.Count, every.Count) : mine.Count.ToString(), 14f, Brown, F.Body, TextAlignmentOptions.BottomRight);
                cnt.textWrappingMode = TextWrappingModes.NoWrap;
                if (!things) Tip(cnt.gameObject, OpensTip);
                if (mine.Count == 0) { T(right, Lang.T("Поки нічого."), 14f, InkSoft, F.Italic); continue; }
                RectTransform grid = Rect("Grid", right);
                GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(78f, 78f); g.spacing = new Vector2(6f, 6f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 6;
                foreach (Outfits.Entry e in mine) OutfitCell(grid, e, item, m);
            }
            Outfits.Entry back = Outfits.Find(Outfits.WornOpen(item, Outfits.Back));
            if (back != null && RavenGear.IsPouch(back.Prefab)) PouchRow(right, m, item, back);
        }

        // one of the four places: a round cell with what is worn there (a click takes it off), its name by it
        private static void SlotCircle(RectTransform left, bool m, ItemDrop.ItemData item, int slot, float x, float y, bool nameAbove)
        {
            Outfits.Entry on = Outfits.Find(Outfits.WornOpen(item, slot));
            RectTransform box = Rect("Slot", left);
            At(box, x, y, 88f, 96f);
            RectTransform circle = Rect("Circle", box);
            circle.anchorMin = circle.anchorMax = new Vector2(0.5f, nameAbove ? 0f : 1f);
            circle.pivot = new Vector2(0.5f, nameAbove ? 0f : 1f);
            circle.sizeDelta = new Vector2(70f, 70f); circle.anchoredPosition = Vector2.zero;
            Image shade = Img(circle, "Shade", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.4f));
            Stretch(shade.rectTransform, Vector2.zero, Vector2.one, new Vector2(-6f, -10f), new Vector2(6f, 2f));
            Img(circle, "Ring", BookArt.Circle, on != null ? C("#a8843a") : new Color(0.227f, 0.141f, 0.078f, 0.5f));
            Image fill = Img(circle, "Fill", BookArt.Circle, on != null ? C("#3a2c1c") : C("#efe3c4"));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            Sprite icon = Outfits.IconOf(on);
            if (on != null && icon != null)
            {
                Image ic = Img(circle, "Icon", icon, Color.white);
                Stretch(ic.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
                ic.preserveAspect = true;
            }
            else
            {
                TMP_Text t = T(circle, on != null ? on.Name : "—", 14f, on != null ? Paper : Caption, F.Body, TextAlignmentOptions.Center);
                Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = 14f;
            }
            TMP_Text n = T(box, Lang.UpperFirst(Outfits.SlotWhere[slot]), 15f, Ink, F.Head, nameAbove ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (on == null) return;
            int s = slot;
            bool pouch = RavenGear.IsPouch(on.Prefab);
            Tip(circle.gameObject, () => Lang.F("<b>{0}</b>{1}\nклік — зняти", on.Name, (pouch ? "\n" + (RavenPouch.Describe(item).Length > 0 ? RavenPouch.Describe(item) : Lang.T("порожня")) : "")));
            Clickable(circle.gameObject, () => { string why = Outfits.TakeOff(Player.m_localPlayer, m, s); if (why == null) Outfits.Choose(m, s, null); else Tell(why); Show(_tab, true); });
        }

        // ---------------------------------------------------------------- the nickname (Nicks)

        // The names it has opened, to choose from: plain, by its norov, and the ones it earned (those only once earned:
        // they are a surprise). The chosen one is how the others see it: in their talks, the chat, over your head.
        // A card: the kinds as four tabs (a gem and its rarity in dots; a kind with nothing open yet is greyed, its hover
        // teases), under them a page of six names with arrows at the sides, and how far through the pages you are.
        private const int NickRows = 2, NickCols = 3, NickPerPage = NickRows * NickCols;
        private static readonly int[] _nickKind = { -1, -1 }, _nickPage = { 0, 0 }; // per raven (Hugin 0, Munin 1); -1: the chosen name's kind
        private static readonly string[] NickGem = { "#8a9097", "#8a5ad0", "#d9a640", "#cf5330" };
        private static string[] NickKindTitle { get { return Lang.Arr(NickKindTitleSrc); } } // (translated on every read)
        private static readonly string[] NickKindTitleSrc = Lang.Keys("Прості", "За норовом", "Віхи", "Легендарні");
        private static string[] NickCaption { get { return Lang.Arr(NickCaptionSrc); } } // (translated on every read)
        private static readonly string[] NickCaptionSrc = Lang.Keys("прості й буденні", "за норовом", "за справи разом", "за подвиги");
        private static readonly Color NickDark = C("#2e2014"), NickRing = C("#a8843a"), NickLight = C("#f0dcae");
        private static readonly Color NickPaper = new Color(0.54f, 0.42f, 0.24f, 0.07f); // a cell on the page: a touch darker than the paper, never white

        private static void NickBlock(Transform col, bool m, ItemDrop.ItemData item)
        {
            List<Nicks.Nick> open = Nicks.Open(item);
            if (open.Count == 0) return;
            Nicks.Nick cur = Nicks.Current(item);
            bool stranger = Trust.Stranger(item, Player.m_localPlayer);
            int mi = m ? 1 : 0;
            List<Nicks.Nick>[] byKind = new List<Nicks.Nick>[4];
            for (int k = 0; k < 4; k++) byKind[k] = new List<Nicks.Nick>();
            foreach (Nicks.Nick n in open) byKind[(int)n.K].Add(n); // as they came, within a kind
            // which kind: the one picked; first time (or it emptied): the chosen name's, on its page
            int kind = _nickKind[mi];
            if (kind < 0 || byKind[kind].Count == 0)
            {
                kind = cur != null ? (int)cur.K : 0;
                if (byKind[kind].Count == 0) for (int k = 0; k < 4; k++) if (byKind[k].Count > 0) { kind = k; break; }
                _nickKind[mi] = kind;
                int at = cur != null ? byKind[kind].IndexOf(cur) : -1;
                _nickPage[mi] = at >= 0 ? at / NickPerPage : 0;
            }
            List<Nicks.Nick> list = byKind[kind];
            int pages = Mathf.Max(1, (list.Count + NickPerPage - 1) / NickPerPage);
            int page = _nickPage[mi] = Mathf.Clamp(_nickPage[mi], 0, pages - 1);

            RectTransform card = Rect("NickCard", col);
            VBox(card, 12f, TextAnchor.UpperLeft, 16, 16, 12, 14);
            Box(card, 8f, new Color(0f, 0f, 0f, 0f), BookArt.CardLine, 1f); // on the page itself, only its frame

            RectTransform head = Rect("Head", card);
            HBox(head, 6f, TextAnchor.LowerLeft);
            T(head, Lang.T("Прізвисько"), 20f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            if (Nicks.HasNew(item)) NewDotInline(head);
            Spring(head);
            TMP_Text cnt = T(head, Lang.F("стор. {0} / {1} · {2}", (page + 1), pages, Lang.N(list.Count, "прізвисько|прізвиська|прізвиськ")), 13f, Brown, F.Body, TextAlignmentOptions.BottomRight);
            cnt.textWrappingMode = TextWrappingModes.NoWrap;
            Tip(cnt.gameObject, () => Lang.F("<b><color={0}>Прізвиська</color></b>\n\nВсього відкрито: {1}.\n\nПрості й за норовом ворон має одразу. Інші він заслужить сам — справами, які ви зробите разом. Які саме — побачиш, коли заслужить.\n\nОбране прізвисько бачать інші гравці: над головою, у чаті й у розмовах воронів.", GoldHex, open.Count));

            // the kinds
            RectTransform tabs = Rect("Kinds", card);
            HBox(tabs, 8f, TextAnchor.MiddleLeft);
            for (int k = 0; k < 4; k++)
            {
                bool fresh = false;
                foreach (Nicks.Nick n in byKind[k]) if (Nicks.IsNew(item, n)) { fresh = true; break; }
                NickKindTab(tabs, m, k, k == kind, byKind[k].Count, fresh);
            }
            Image fade = Img(tabs, "Line", BookArt.Fade(false), A(Brown, 0.45f));
            Fixed(fade.gameObject, -1f, 2f); fade.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // the page of names between the arrows
            RectTransform body = Rect("Body", card);
            HBox(body, 10f, TextAnchor.MiddleCenter);
            const float arrow = 34f, gap = 7f, cellH = 42f;
            float gridW = ColumnW - 32f - 2f * arrow - 20f;
            int firstNew = list.FindIndex(x => Nicks.IsNew(item, x));
            bool newBefore = firstNew >= 0 && firstNew < page * NickPerPage; // a gold mark on the arrow that leads to it
            NickArrow(body, "‹", page > 0, () => { _nickPage[mi] = page - 1; Show(_tab, true); }, newBefore);
            RectTransform grid = Rect("Names", body);
            Fixed(grid.gameObject, gridW, NickRows * cellH + (NickRows - 1) * gap);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((gridW - (NickCols - 1) * gap) / NickCols, cellH); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = NickCols;
            for (int i = page * NickPerPage; i < Mathf.Min(list.Count, (page + 1) * NickPerPage); i++)
                NickCell(grid, list[i], list[i] == cur, Nicks.IsNew(item, list[i]), m, item, stranger); // (seen when you leave the page: LeaveOutfits)
            bool newAfter = list.FindLastIndex(x => Nicks.IsNew(item, x)) >= (page + 1) * NickPerPage;
            NickArrow(body, "›", page < pages - 1, () => { _nickPage[mi] = page + 1; Show(_tab, true); }, newAfter);

            // how far through the pages, and what this kind is for
            RectTransform foot = Rect("Foot", card);
            HBox(foot, 14f, TextAnchor.MiddleLeft, (int)(arrow + 10f), 0, 0, 0);
            RectTransform track = Rect("Track", foot);
            Fixed(track.gameObject, -1f, 4f); track.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Image tr = Img(track, "Line", BookArt.RoundFine(2f), A(Brown, 0.2f)); tr.type = Image.Type.Sliced; tr.pixelsPerUnitMultiplier = BookArt.Fine;
            Image fill = Img(track, "Fill", BookArt.RoundFine(2f), A(Brown, 0.7f)); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = BookArt.Fine;
            fill.rectTransform.anchorMax = new Vector2((page + 1f) / pages, 1f);
            TMP_Text cap = T(foot, NickCaption[kind], 13f, Brown, F.Italic, TextAlignmentOptions.MidlineRight);
            cap.textWrappingMode = TextWrappingModes.NoWrap;

            // what a new name costs now: free, how long to wait, or the trust it takes (the rules under the cursor)
            if (!stranger)
            {
                TMP_Text price = T(card, RenameLine(item, m), 14f, Ink, F.Body, TextAlignmentOptions.Center);
                price.textWrappingMode = TextWrappingModes.NoWrap;
                Tip(price.gameObject, RenameRules);
                ItemDrop.ItemData it = item;
                Live.Add(() => { if (price != null) price.text = RenameLine(it, m); });
            }

            RectTransform space = Rect("Gap", col);
            Fixed(space.gameObject, -1f, 8f);
        }

        // a kind's tab: its gem, its rarity in dots under it; nothing open of it yet: greyed, and the hover teases
        private static void NickKindTab(Transform row, bool m, int k, bool on, int count, bool fresh)
        {
            RectTransform t = Rect("Kind" + k, row);
            Fixed(t.gameObject, 58f, 44f);
            bool empty = count == 0;
            Box(t, 6f, on ? NickDark : NickPaper, on ? NickRing : A(Brown, 0.5f), on ? 2f : 1f);
            if (empty) t.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
            Color gem = empty ? C("#a49c90") : C(NickGem[k]);
            RectTransform g = Rect("Gem", t);
            g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
            g.sizeDelta = new Vector2(15f, 15f); g.anchoredPosition = new Vector2(0f, 5f);
            g.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image edge = Img(g, "Edge", BookArt.RoundFine(2f), Color.Lerp(gem, Color.black, 0.45f)); edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = BookArt.Fine;
            Image face = Img(g, "Face", BookArt.RoundFine(2f), gem); face.type = Image.Type.Sliced; face.pixelsPerUnitMultiplier = BookArt.Fine;
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
            Image shine = Img(g, "Shine", BookArt.RoundFine(1f), new Color(1f, 1f, 1f, 0.35f)); shine.type = Image.Type.Sliced; shine.pixelsPerUnitMultiplier = BookArt.Fine;
            Stretch(shine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 1f), new Vector2(2.5f, 0f), new Vector2(0f, -2.5f));
            for (int i = 0; i <= k; i++)
            {
                Image d = Img(t, "Dot", BookArt.Circle, on ? NickRing : A(Brown, 0.75f));
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                d.rectTransform.sizeDelta = new Vector2(4f, 4f);
                d.rectTransform.anchoredPosition = new Vector2((i - k / 2f) * 7f, 8f);
            }
            if (fresh && !empty) NewDot(t, 11f, 4f);
            Tip(t.gameObject, () => NickKindTip(m, k, count));
            if (empty || on) return;
            Clickable(t.gameObject, () => { _nickKind[m ? 1 : 0] = k; _nickPage[m ? 1 : 0] = 0; Show(_tab, true); });
        }

        private static string NickKindTip(bool m, int k, int count)
        {
            string title = "<b><color=" + Nicks.Hex((Nicks.Kind)k) + ">" + NickKindTitle[k] + "</color></b> · ";
            if (count > 0)
            {
                string[] about =
                {
                    Lang.T("Імена, з якими ворон до тебе прийшов, і ті, що він заробив буденною працею поруч: дрова, ягоди, двері, каміння."),
                    Lang.T("Імена за його вдачею: характер, хист і примха. Яким він є, таким і зветься."),
                    Lang.T("Віхи довгої спільної дороги: боси, дні разом, сотні влучних ударів. За що саме — у підказці кожного імені."),
                    Lang.T("Подвиг або сотні годин разом. Такі імена скальди пам'ятають довше, ніж самих героїв."),
                };
                return title + Lang.N(count, "відкрите|відкриті|відкритих") + "\n\n" + about[k];
            }
            string[] hugin =
            {
                Lang.T("«Без імені? Дивно. Зазвичай навіть я з якимось приходжу.»"),
                Lang.T("«Мою вдачу ще треба заслужити побачити. Я поки придивляюся до твоєї.»"),
                Lang.T("«Віха? Спершу зроби щось, гідне запису. Я почекаю. Я терплячий.»"),
                Lang.T("«Легенди пишуть про героїв. Я поки пишу про те, як ти падаєш з даху.»"),
            };
            string[] munin =
            {
                Lang.T("«Імені нема. І не треба. Звик, що мене кличуть \"ей ти\".»"),
                Lang.T("«Норов? Покажу, коли розізлиш як слід. Тобто скоро.»"),
                Lang.T("«Віху тобі? Зроби хоч щось, крім того, щоб гепатися на рівному місці.»"),
                Lang.T("«Легенда? З тобою? Не смішіть моє пір'я.»"),
            };
            string[] hint =
            {
                Lang.T("Тут з'являться прості імена."),
                Lang.T("Тут з'являться імена за його характер, хист і примху."),
                Lang.T("Тут з'являться імена за довгі спільні справи. Які саме — секрет, доки він їх не заслужить."),
                Lang.T("Тут з'являться найрідкісніші імена. Які саме — секрет, і він довгий."),
            };
            return Lang.F("{0}ще жодного\n\n{1}\n\n<color={2}>{3}:</color> <i>{4}</i>", title, hint[k], RavenHex(m), Plugin.RavenName(m), (m ? munin[k] : hugin[k]));
        }

        // a round arrow at the side of the names: dark with a gold ring when it leads somewhere, a faded one when not
        private static void NickArrow(Transform row, string glyph, bool can, Action click, bool fresh)
        {
            RectTransform r = Rect("Arrow", row);
            Fixed(r.gameObject, 34f, 34f);
            if (can)
            {
                Image sh = Img(r, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.3f));
                Stretch(sh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -7f), new Vector2(4f, 1f));
            }
            Image ring = Img(r, "Ring", BookArt.Circle, can ? NickRing : A(C("#b8a07c"), 0.7f));
            ring.raycastTarget = true;
            Image face = Img(r, "Face", BookArt.Circle, can ? NickDark : A(C("#b8a07c"), 0.7f));
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            TMP_Text t = T(r, glyph, 20f, can ? NickLight : A(C("#efe3c4"), 0.9f), F.Head, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero);
            if (!can) return;
            if (fresh) NewDot(r, 11f, 2f); // a new name waits on that side
            Button b = ring.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            Tint(b);
            b.onClick.AddListener(() => click());
        }

        private static void NickCell(Transform grid, Nicks.Nick n, bool chosen, bool fresh, bool m, ItemDrop.ItemData item, bool stranger)
        {
            RectTransform cell = Rect("Nick_" + n.Id, grid);
            Color ink = C(Nicks.PaperHex(n.K));
            Box(cell, 6f, chosen ? NickDark : NickPaper, chosen ? NickRing : A(Brown, 0.5f), chosen ? 2f : 1f);
            TMP_Text t = T(cell, n.Name, 16f, chosen ? NickLight : ink, n.K >= Nicks.Kind.Deed || chosen ? F.Head : F.Body, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = 11f; t.fontSizeMax = 16f;
            if (fresh) NewDot(cell, 11f, -3f);
            NickHover hov = cell.gameObject.AddComponent<NickHover>(); // under the cursor: a touch bigger, a darker paper, a gold rim
            hov.Fill = cell.GetComponent<Image>();
            Transform line = cell.Find("Line");
            hov.Line = line != null ? line.GetComponent<Image>() : null;
            hov.FillOn = chosen ? NickDark : new Color(NickPaper.r, NickPaper.g, NickPaper.b, 0.18f);
            hov.LineOn = NickRing;
            Tip(cell.gameObject, () => NickTip(n, chosen, stranger, m, item));
            if (chosen || stranger) return;
            ClickArea(cell.gameObject, () => // (its own hover: NickHover; a button's tint too made these cells darker than the rest)
            {
                string block = Nicks.RenameBlock(Player.m_localPlayer, item, m);
                if (block != null) { Tell(block); return; }
                NickAsk(cell, n, m, item); // every renaming asks first: what it costs and what follows
            });
        }

        // ---------------------------------------------------------------- "name him so?" by the clicked name
        // A small dark card over the name: the new name, who sees it, what it costs now (free / trust / nothing), the wait
        // after it, that the old name stays open. «Так» (or Enter) renames; «Ні», a click beside it or Esc leave it be.

        private static Action _nickYes; // the open ask's «Так» (Enter presses it)

        private static void NickAsk(RectTransform cell, Nicks.Nick n, bool m, ItemDrop.ItemData item)
        {
            ClosePopup();
            HideTip();
            if (_root == null) return;
            Nicks.Nick cur = Nicks.Current(item);
            bool free = Nicks.FreeRename(item);
            int cost = free ? 0 : Nicks.RenameCost(item);
            string whom = m ? Lang.T("Муніна") : Lang.T("Хугіна");

            Card card = OpenCard("NickAsk", Lang.T("Ім'я"), 440f, true); // a click beside the card: «Ні»
            RectTransform panel = card.Paper;
            TMP_Text title = T(panel, Lang.F("Назвати {0} «<color={1}>{2}</color>»?", whom, Nicks.PaperHex(n.K), n.Name), 21f, PInk, F.Head);
            title.textWrappingMode = TextWrappingModes.Normal;

            // what follows, line by line
            string price;
            if (free)
                price = Lang.F("<color={0}>★ Безкоштовно</color> — без втрати довіри й без очікування.\nПісля неї наступне нове ім'я коштуватиме довіри (ще одну безкоштовну дасть лише заслужене золоте чи легендарне прізвисько).", PGoodHex);
            else if (cost > 0)
                price = Lang.F("<color={0}>Довіра −{1}</color> ({2}% шкали до наступного рівня). Рівень не впаде.", BadHex, cost, Mathf.RoundToInt(Nicks.RenameLoss * 100f));
            else
                price = Lang.F("<color={0}>Довіра не зменшиться</color> — {1}", PGoodHex, (Trust.Full(item) ? Lang.T("шкала повна й чекає ритуалу.") : Trust.Need(item) <= 0f ? Lang.T("рівень уже найвищий.") : Lang.T("на шкалі довіри зараз нічого забрати.")));
            T(panel, price, 15f, PInk, F.Body);
            T(panel, free ? Lang.T("Очікування після неї не почнеться: якщо передумаєш, знову зміниш одразу, але вже з утратою довіри.")
                : Lang.F("Наступне нове ім'я — лише через {0} ігрові доби.", Nicks.RenameDays), 15f, PInk, F.Body);
            T(panel, Lang.F("Нове ім'я бачитимуть інші гравці: над головою ворона, у чаті й у розмовах воронів.{0}", (cur != null ? Lang.F(" «{0}» лишиться у списку — до нього можна повернутись за тими ж правилами.", cur.Name) : "")), 14f, PSoft, F.Italic);

            // «Так» on the right, as the deed; «Ні» before it
            Action yes = () =>
            {
                ClosePopup();
                string why = Nicks.Choose(Player.m_localPlayer, item, m, n.Id); // checks again: things may have changed meanwhile
                if (why != null) Tell(why);
                Show(_tab, true);
            };
            AskButton(card.Foot, Lang.T("Ні"), false, ClosePopup);
            AskButton(card.Foot, cost > 0 ? Lang.F("Так, довіра −{0}", cost) : Lang.T("Так"), true, yes);
            _nickYes = yes;

            PlaceCard(card, cell); // over the clicked name; no room above: under it; always inside the book
        }

        // the ask's buttons on the card's dark strip (RavenBookCard): the deed a gold pill with a seal, the rest outlined
        private static void AskButton(Transform row, string caption, bool main, Action click) { CardButton(row, caption, main, click); }

        private static string NickTip(Nicks.Nick n, bool chosen, bool stranger, bool m, ItemDrop.ItemData item)
        {
            string why = n.Why != null ? (n.Earned != null ? Lang.T("За що: ") : "") + n.Why : Lang.T("Просте ім'я, з яким ворон до тебе прийшов."); // an earned one (grey ones too): what for
            string act;
            if (chosen) act = Lang.T("Зараз його так звуть.");
            else if (stranger) act = Lang.T("Чужий ворон: ім'я йому дав власник.");
            else
            {
                string block = Nicks.RenameBlock(Player.m_localPlayer, item, m);
                int cost = Nicks.RenameCost(item);
                act = block != null ? "<color=#FF6B6B>" + block + "</color>"
                    : Nicks.FreeRename(item) ? Lang.F("Клік — назвати так. <color={0}>Безкоштовно.</color> Перед зміною спитає.", OkHex)
                    : cost > 0 ? Lang.F("Клік — назвати так: <color=#FF6B6B>довіра −{0}</color>, далі нове ім'я лише через {1} доби. Перед зміною спитає.", cost, Nicks.RenameDays)
                    : Lang.F("Клік — назвати так. Довіра не зменшиться (шкала повна або найвищий рівень), але далі нове ім'я лише через {0} доби. Перед зміною спитає.", Nicks.RenameDays);
            }
            return "<b><color=" + Nicks.Hex(n.K) + ">" + n.Name + "</color></b> · " + Nicks.KindName(n.K) + "\n\n" + why + "\n\n" + act;
        }

        // the line under the names: what a new name costs right now
        private static string RenameLine(ItemDrop.ItemData item, bool m)
        {
            if (Nicks.FreeRename(item)) return Lang.F("<color={0}>★ Нове ім'я зараз безкоштовне</color>", GoodHex);
            if (Trust.Away(item)) return Lang.F("<color={0}>Нове ім'я — коли ворон повернеться</color>", MutedHex);
            float wait = Nicks.RenameWait(item);
            if (wait > 0f) return Lang.F("<color={0}>Нове ім'я — через {1}</color>", MutedHex, Nicks.WaitText(wait));
            int cost = Nicks.RenameCost(item);
            return cost > 0 ? Lang.F("Нове ім'я: <color={0}>довіра −{1}</color> · далі раз на {2} доби", BadHex, cost, Nicks.RenameDays)
                : Lang.F("Нове ім'я: без втрати довіри · далі раз на {0} доби", Nicks.RenameDays);
        }

        private static string RenameRules()
        {
            return Lang.F("<b><color={0}>Зміна імені</color></b>\n\nВорон звикає до свого імені. Нове ім'я забирає <color=#FF6B6B>{1}% шкали довіри</color> до наступного рівня (рівень не падає), а наступне можна дати лише через {2} ігрові доби.\n\n<color={3}>Безкоштовно</color> — без втрати довіри і без очікування:\n• перший вибір імені;\n• один раз після того, як ворон заслужить <color={4}>золоте</color> чи <color={5}>легендарне</color> прізвисько. Кілька заслужених поспіль дають лише одну безкоштовну зміну.\n\nКоли шкала довіри повна (чекає ритуалу) чи рівень найвищий, довіра не зменшується — лишається тільки очікування.", GoldHex, Mathf.RoundToInt(Nicks.RenameLoss * 100f), Nicks.RenameDays, OkHex, Nicks.Hex(Nicks.Kind.Deed), Nicks.Hex(Nicks.Kind.Legend));
        }

        // the pouch on its back: what is in it and the button that opens it by the bag
        private static void PouchRow(Transform col, bool m, ItemDrop.ItemData item, Outfits.Entry pouch)
        {
            RectTransform row = Rect("Pouch", col);
            HBox(row, 12f, TextAnchor.MiddleLeft, 10, 10, 8, 8);
            Torn(row, BookArt.Card, BookArt.CardLine, 1f);
            RectTransform ic = Rect("Icon", row);
            Fixed(ic.gameObject, 44f, 44f);
            Box(ic, 6f, new Color(0f, 0f, 0f, 0f), C("#a8843a"), 1.5f);
            Sprite sp = Outfits.IconOf(pouch);
            if (sp != null) { Image im = Img(ic, "Pic", sp, Color.white); im.preserveAspect = true; Stretch(im.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f)); }
            RectTransform words = Rect("Words", row);
            VBox(words, 0f, TextAnchor.MiddleLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(words, Lang.T("Торба на спині"), 18f, Ink, F.Head);
            string food = RavenPouch.Describe(item);
            T(words, Lang.F("1 клітинка · до {0} шматків їжі · {1}", RavenPouch.Max, (food.Length > 0 ? Lang.F("зараз {0}", food) : Lang.T("порожня"))), 14f, Brown, F.Body);
            RectTransform open = DarkButton(row, Lang.T("Торба"), () => { string why = RavenPouch.Open(Player.m_localPlayer, m); if (why != null) Tell(why); });
            Tip(open.gameObject, () => Lang.F("Відкрити торбу поруч із сумкою: поклади їжу (до {0} шматків). Голодний ворон поїсть із неї сам.", RavenPouch.Max));
        }

        // the counter's tip: that more opens, never what or when (a riddle)
        private static string OpensTip()
        {
            return Lang.T("<b>Що відкриється</b>\n\nЩо ще він згодиться вдягти — загадка. Довіра й Одін відкривають нове: у кожного ворона свій набір.");
        }

        private static void OutfitCell(Transform grid, Outfits.Entry e, ItemDrop.ItemData item, bool munin)
        {
            bool worn = Outfits.WornOpen(item, e.Slot) == e.Prefab;
            RectTransform cell = Rect("Outfit_" + e.Prefab, grid);
            Torn(cell, worn ? new Color(0.66f, 0.52f, 0.23f, 0.25f) : BookArt.Card, worn ? C("#a8843a") : new Color(0.227f, 0.141f, 0.078f, 0.35f), 1.5f);
            Sprite icon = Outfits.IconOf(e);
            if (icon != null)
            {
                Image ic = Img(cell, "Icon", icon, Color.white);
                Stretch(ic.rectTransform, Vector2.zero, Vector2.one, new Vector2(9f, 9f), new Vector2(-9f, -9f));
                ic.preserveAspect = true;
            }
            else
            {
                TMP_Text n = T(cell, e.Name, 14f, Ink, F.Body, TextAlignmentOptions.Center); // no picture of it: its name
                Stretch(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                n.enableAutoSizing = true; n.fontSizeMin = 9f; n.fontSizeMax = 14f;
            }
            if (Outfits.IsNew(item, e)) NewDot(cell, 13f, -4f);
            if (Outfits.Tuning && Outfits.Ready(e, munin))
            {
                TMP_Text done = T(cell, "<b>ok</b>", 12f, Good, F.Body, TextAlignmentOptions.TopLeft);
                Stretch(done.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-2f, -2f));
            }
            bool thing = e.Gear != null && !Outfits.OwnHat(e) && !Outfits.Tuning;
            int have = thing ? Outfits.CountInBag(Player.m_localPlayer, e) : 0;
            if (have > 1 || (have == 1 && worn))
            {
                TMP_Text count = T(cell, "×" + have, 13f, Ink, F.Body, TextAlignmentOptions.BottomRight);
                Stretch(count.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-5f, -2f));
            }
            // its words, then what it gives (as in the item's own tooltip)
            string gives = e.Gear == null ? null : RavenGear.IsPouch(e.Gear.Key) ? Lang.F("Поклади туди їжу — до {0} шматків. Коли ворон зголодніє, поїсть із неї сам.", RavenPouch.Max) : GearFx.Text(e.Gear.Id);
            Tip(cell.gameObject, () => "<b>" + e.Name + "</b>" + (e.Gear != null && !string.IsNullOrEmpty(e.Gear.Note) ? "\n" + e.Gear.Note : "") + (gives != null ? "\n\n" + gives : "") + "\n" + (worn ? Lang.F("надіте · клік — зняти{0}", (thing && Outfits.Paid(item, e.Slot) ? Lang.T(" (повернеться в сумку)") : "")) : thing && have > 0 ? Lang.T("у сумці · клік — надіти") : Lang.T("клік — надіти")));
            Clickable(cell.gameObject, () =>
            {
                string why = null;
                if (thing && worn) why = Outfits.TakeOff(Player.m_localPlayer, munin, e.Slot);
                else if (thing && Outfits.CountInBag(Player.m_localPlayer, e) > 0) why = GearDress.Start(Player.m_localPlayer, munin, Outfits.InBag(Player.m_localPlayer, e)); // the hero's move, then on (the book redraws then)
                else Outfits.Choose(munin, e.Slot, worn ? null : e.Prefab); // a hat, or an admin trying a thing on
                if (thing && worn && why == null) Outfits.Choose(munin, e.Slot, null); // an admin's try-on: off as well
                if (why != null) Tell(why);
                _editing = worn ? null : e.Prefab;
                Show(_tab, true);
            });
        }

        // under the picture (admins fitting): where the camera looks from, how near, turning by itself, sitting or flying
        private static void PreviewButtons(RectTransform left)
        {
            RectTransform a = Rect("Views", left);
            At(a, 0f, 536f, 500f, 28f);
            HorizontalLayoutGroup ha = HBox(a, 4f, TextAnchor.MiddleLeft);
            ha.childForceExpandWidth = true;
            SmallButton(a, Lang.T("Спереду"), false, () => OutfitStage.View(180f, 0.15f));
            SmallButton(a, Lang.T("Збоку"), false, () => OutfitStage.View(_dressMunin ? 90f : 270f, 0.15f));
            SmallButton(a, Lang.T("Ззаду"), false, () => OutfitStage.View(0f, 0.15f));
            SmallButton(a, Lang.T("Згори"), false, () => OutfitStage.View(180f, 6f));
            SmallButton(a, "−", false, () => OutfitStage.ZoomBy(1f / 1.25f));
            SmallButton(a, "+", false, () => OutfitStage.ZoomBy(1.25f));
            RectTransform c = Rect("Poses", left);
            At(c, 0f, 568f, 500f, 28f);
            HorizontalLayoutGroup hc = HBox(c, 4f, TextAnchor.MiddleLeft);
            hc.childForceExpandWidth = true;
            SmallButton(c, Lang.T("Обертати"), OutfitStage.Spin, () => { OutfitStage.Go(); Show(_tab, true); });
            SmallButton(c, Lang.T("Стоп"), OutfitStage.Still, () => { OutfitStage.Stop(); Show(_tab, true); });
            for (int i = 0; i < OutfitStage.ModeNames.Length; i++)
            {
                int k = i;
                SmallButton(c, OutfitStage.ModeNames[i], OutfitStage.Mode == i, () => { OutfitStage.Mode = k; if (k != OutfitStage.Sitting) OutfitStage.Still = false; Show(_tab, true); });
            }
        }

        // ---------------------------------------------------------------- admins tuning new pieces
        // The last piece clicked (and worn) moves with these: a step size, minus / its number (typed) / plus for each
        // axis, a fit of its own for this raven or the shared one, the same on the other raven, back to the start, done.
        private static int _step = 1;                       // 0 coarse, 1 normal, 2 fine
        private static TMP_Text _fitLabel;
        private static readonly List<KeyValuePair<TMP_InputField, Func<Outfits.Fit, float>>> _fitFields = new List<KeyValuePair<TMP_InputField, Func<Outfits.Fit, float>>>();

        private sealed class Axis
        {
            public string Name;
            public Func<Outfits.Fit, float> Get;  // as shown (cm for a thing's place)
            public Action<Outfits.Fit, float> Set;
            public Func<int, float> Step;          // by the step size: added (or, for a size, multiplied by 1 + it)
            public bool Mul;
        }

        private static List<Axis> AxesFor(Outfits.Entry ed)
        {
            bool gear = ed.Gear != null;
            float[] pos = gear ? new[] { 2f, 0.5f, 0.1f } : new[] { 0.1f, 0.05f, 0.01f }; // cm for a thing, head lengths for a hat
            float[] rot = { 15f, 5f, 1f };
            float[] size = { 0.1f, 0.03f, 0.01f };
            float u = gear ? 100f : 1f;
            List<Axis> l = new List<Axis>
            {
                new Axis { Name = Lang.T("Розмір"), Get = f => f.S, Set = (f, v) => f.S = Mathf.Max(0.01f, v), Step = s => size[s], Mul = true },
                new Axis { Name = gear ? Lang.T("Вбік, см") : Lang.T("Вбік"), Get = f => f.X * u, Set = (f, v) => f.X = v / u, Step = s => pos[s] },
                new Axis { Name = gear ? Lang.T("Вгору, см") : Lang.T("Вгору"), Get = f => f.Y * u, Set = (f, v) => f.Y = v / u, Step = s => pos[s] },
                new Axis { Name = gear ? Lang.T("Вперед, см") : Lang.T("Вперед"), Get = f => f.Z * u, Set = (f, v) => f.Z = v / u, Step = s => pos[s] },
                new Axis { Name = Lang.T("Нахил, °"), Get = f => f.RX, Set = (f, v) => f.RX = v, Step = s => rot[s] },
                new Axis { Name = Lang.T("Поворот, °"), Get = f => f.RY, Set = (f, v) => f.RY = v, Step = s => rot[s] },
                new Axis { Name = Lang.T("Крен, °"), Get = f => f.RZ, Set = (f, v) => f.RZ = v, Step = s => rot[s] },
            };
            if (gear)
            {
                l.Add(new Axis { Name = Lang.T("Ширина"), Get = f => f.SX, Set = (f, v) => f.SX = Mathf.Max(0.01f, v), Step = s => size[s], Mul = true });
                l.Add(new Axis { Name = Lang.T("Висота"), Get = f => f.SY, Set = (f, v) => f.SY = Mathf.Max(0.01f, v), Step = s => size[s], Mul = true });
                l.Add(new Axis { Name = Lang.T("Довжина"), Get = f => f.SZ, Set = (f, v) => f.SZ = Mathf.Max(0.01f, v), Step = s => size[s], Mul = true });
            }
            return l;
        }

        private static void FitPanel(Transform parent, bool m, ItemDrop.ItemData item)
        {
            RectTransform panel = Rect("Fitting", parent);
            Torn(panel, BookArt.Card, BookArt.CardLine, 1f);
            VBox(panel, 5f, TextAnchor.UpperLeft, 10, 10, 8, 8);
            TMP_Text cap = T(panel, Lang.T("АДМІН: ПІДГОНКА"), 13f, Caption, F.Body);
            cap.characterSpacing = 12f;
            Outfits.Entry ed = Outfits.Find(_editing);
            _fitFields.Clear();
            if (ed == null || Outfits.WornOpen(item, ed.Slot) != ed.Prefab)
            {
                T(panel, Lang.T("Клікни річ у сітці нижче, щоб надіти її, — тут з'являться кнопки підгонки. Речі з Майстерні («Річ для ворона») з'являються, щойно їх збережено."), 14f, InkSoft, F.Italic);
                return;
            }
            _fitLabel = T(panel, FitText(ed, m), 15f, GoldInk, F.Body);

            RectTransform steps = Rect("Steps", panel);
            HBox(steps, 4f, TextAnchor.MiddleLeft);
            T(steps, Lang.T("Крок"), 14f, Brown, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
            string[] names = { Lang.T("Грубо"), Lang.T("Звичайно"), Lang.T("Точно") };
            for (int i = 0; i < names.Length; i++) { int k = i; SmallButton(steps, names[i], _step == i, () => { _step = k; Show(_tab, true); }); }

            RectTransform grid = Rect("Axes", panel);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(232f, 28f);
            g.spacing = new Vector2(8f, 3f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            foreach (Axis a in AxesFor(ed)) AxisRow(grid, ed, m, a);

            RectTransform acts = Rect("Acts", panel);
            HorizontalLayoutGroup ha = HBox(acts, 4f, TextAnchor.MiddleLeft);
            ha.childForceExpandWidth = true;
            bool own = Outfits.Separate(ed, m);
            string me = m ? Lang.T("Муніна") : Lang.T("Хугіна"), other = m ? Lang.T("Хугіна") : Lang.T("Муніна");
            SmallButton(acts, own ? Lang.F("Спільна з {0}", other) : Lang.F("Окремо для {0}", me), own, () => { Outfits.SetSeparate(ed, m, !own); Show(_tab, true); });
            SmallButton(acts, Lang.F("Так само на {0}", other), false, () => { Outfits.CopyToOther(ed, m); Show(_tab, true); });
            SmallButton(acts, Lang.T("Скинути"), false, () => { Outfits.ResetFit(ed, m); Show(_tab, true); });
            SmallButton(acts, Lang.T("Готово"), true, () => { Outfits.Approve(ed, m); Show(_tab, true); });
            T(panel, Lang.F("{0}Число можна вписати + Enter. «Готово» — позначиться «ok» і піде в мод.", (ed.Gear != null ? Lang.T("Зсув — від місця з Майстерні, у см; права лапа — дзеркально. ") : "")), 13f, InkSoft, F.Italic);
        }

        private static void AxisRow(Transform grid, Outfits.Entry ed, bool m, Axis a)
        {
            RectTransform r = Rect("Axis", grid);
            HBox(r, 4f, TextAnchor.MiddleLeft);
            TMP_Text n = T(r, a.Name, 14f, Ink, F.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Fixed(SmallButton(r, "−", false, () => Change(ed, m, f => a.Set(f, a.Mul ? a.Get(f) / (1f + a.Step(_step)) : a.Get(f) - a.Step(_step)))).gameObject, 30f, 26f);
            TMP_InputField field = NumberField(r, 66f, Num(a.Get(Outfits.FitFor(ed, m))), v => Change(ed, m, f => a.Set(f, v)));
            _fitFields.Add(new KeyValuePair<TMP_InputField, Func<Outfits.Fit, float>>(field, a.Get));
            Fixed(SmallButton(r, "+", false, () => Change(ed, m, f => a.Set(f, a.Mul ? a.Get(f) * (1f + a.Step(_step)) : a.Get(f) + a.Step(_step)))).gameObject, 30f, 26f);
        }

        // only the numbers change: the page stays as it is (the raven is dressed again by itself)
        private static void Change(Outfits.Entry ed, bool m, Action<Outfits.Fit> change)
        {
            Outfits.Nudge(ed, change, m);
            Outfits.Fit f = Outfits.FitFor(ed, m);
            if (_fitLabel != null) _fitLabel.text = FitText(ed, m);
            foreach (KeyValuePair<TMP_InputField, Func<Outfits.Fit, float>> kv in _fitFields)
                if (kv.Key != null && !kv.Key.isFocused) kv.Key.text = Num(kv.Value(f));
        }

        private static string Num(float v) { return (Mathf.Abs(v) < 0.0005f ? 0f : v).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

        // a box to type a number in (Enter or a click away takes it)
        private static TMP_InputField NumberField(Transform parent, float width, string value, Action<float> set)
        {
            RectTransform box = Rect("Field", parent);
            Torn(box, new Color(1f, 0.97f, 0.89f, 0.6f), BookArt.CardLine, 1f);
            Fixed(box.gameObject, width, 26f);
            RectTransform area = Rect("Area", box);
            Stretch(area, Vector2.zero, Vector2.one, new Vector2(5f, 2f), new Vector2(-5f, -2f));
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text t = T(area, value, 14f, Ink, F.Body, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_InputField f = box.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area;
            f.textComponent = t;
            f.targetGraphic = box.GetComponent<Image>();
            f.contentType = TMP_InputField.ContentType.DecimalNumber;
            f.customCaretColor = true; f.caretColor = Ink;
            f.selectionColor = new Color(0.66f, 0.52f, 0.23f, 0.35f);
            Navigation nav = f.navigation; nav.mode = Navigation.Mode.None; f.navigation = nav;
            f.text = value;
            f.onEndEdit.AddListener(s =>
            {
                float v;
                if (float.TryParse((s ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) set(v);
            });
            return f;
        }

        private static string FitText(Outfits.Entry ed, bool m)
        {
            return Lang.F("Підгонка «{0}» на {1} ({2})", ed.Name, (m ? Lang.T("Муніні") : Lang.T("Хугіні")), Outfits.Status(ed, m));
        }

        // every frame while the page is open: the picture of the raven as it is dressed now
        private static void TickOutfit()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            OutfitStage.Tick(Outfits.Code(_dressMunin ? munin : hugin), RavenWear.Code(_dressMunin ? munin : hugin));
        }

        // ---------------------------------------------------------------- words behind "?"

        private static string HelpFor(int tab)
        {
            string h = "<font=\"" + HeadName() + "\"><size=26><color=" + GoldHex + ">" + TabNames[tab] + "</color></size></font>\n\n";
            switch (tab)
            {
                case RavensTab:
                    return Lang.F("{0}Ліва сторінка — Мунін, ворон пам'яті, бойовий: сидить на лівому плечі. Права — Хугін, ворон думки, мандрівний: на правому. Обох продає Хальдор; правий клік на вороні в сумці садить його на плече чи знімає.\n\n<b>Довіра, Сила, Ситість.</b> Сила витрачається на дари: Хугін заряджається в мирі, Мунін — у бою. Ситість нижче {1} — ворон не слухає наказів. Наведи на золоті слова, щоб побачити все.\n\n<b>Благо й тягар</b> кожен ворон отримує випадково при покупці; Камінь долі в Хальдора перекидає їх. <b>Норов</b> — його характер, хист і примха.\n\n<b>Що далі</b> — найважливіша справа для ворона саме зараз: годування, ритуал чи наступна ціль. Печатка праворуч її робить.", h, RavenAbilities.R(Satiety.HungryAt));
                case GiftsTab:
                    return Lang.F("{0}Дари викликаються з кола дарів (<color={1}>{2}</color>), з G → Ворони або своїми клавішами. Кожен коштує сили й має перезарядку; наведи на дар — побачиш усе про нього.\n\n<b>Клавіші</b> — за бажанням. Клікни поле на картці дару й натисни клавішу (можна з Shift, Ctrl чи Alt); Esc — скасувати, Backspace — прибрати. Або ПКМ по дару в колі. Клавіші воронів <b>нічого не забирають</b> у гри й інших модів: якщо клавіша вже зайнята, спрацює і те, і дар. <color=#FF6B6B>!</color> показує, хто ще на ній.\n\nТочка на картці: <color=#5a9a4a>●</color> готовий · <color=#d8a83a>●</color> чекає · <color=#b8322a>●</color> бракує сили. <b>Пасивні</b> дари діють самі. Закриті відкриваються з довірою.", h, GoldHex, RavenKeys.Label(RavenKeys.Get(RavenKeys.GiftsId)));
                case RunesTab:
                    return NestHelp(h) + "\n\n" + RunesHelp("");
                case GrowTab:
                    return Lang.F("{0}<b>Довіра.</b> Ворон звикає до того, з ким мандрує. Довіра росте, коли годуєш його, голодного (улюблена їжа — удвічі), і коли ви разом живете — і в мирі, і в бою. Коли шкала повна, вона замерзає й чекає ритуалу: принеси трофеї наступного боса до Жертовних каменів, де висить трофей цього боса. Ворон з'їсть їх, полетить до Одіна й повернеться на новому рівні: більше сили, сильніші дари, нове вбрання.\nДовіра тане, коли ти гинеш, знімаєш ворона з плеча чи він голодує (ситість 0). На 1–2 рівнях ворон ще дичиться й іноді відлітає на кілька хвилин.\n\n<b>Вишкіл</b> — сила удару ворона («Удар в очі», «Кігті»). Кожен виліт, що когось вразив, додає досвіду. Сила не залежить від твоєї зброї: вона росте з вишколом, довірою та за кожного боса, якого ти переміг із ним на плечі.\n\n<b>Повернення.</b> Загиблого чи зниклого ворона можна повернути обрядом біля каменів (на сторінці ворона), не частіше ніж раз на {1} днів.", h, Backup.RestoreDays);
                default:
                    return Lang.F("{0}Спорядження відкривається на {1}-му рівні довіри. У ворона чотири місця: голова, шия, спина, лапи.\n\n<b>Голова.</b> Шапки й шоломи відкриваються з рівнями, а інші ворон приносить від Одіна після ритуалу — по одній, і в кожного ворона свої.\n\n<b>Шия, спина, лапи.</b> Тут речі для ворона, які лежать у твоїй сумці. Клік надіває річ (вона переходить із сумки на ворона), клік по круглому слоту — знімає назад у сумку. Речі з воронячого столу дають ворону свою силу; речі з просунутого столу він дасть надіти лише з {2}-го рівня довіри.\n\n<b>Торба</b> (на спині). Кнопка «Торба» відкриває її поруч із сумкою: поклади їжу для цього ворона, до {3} шматків. Коли він зголодніє, поїсть із неї сам.\n\nВорона у вікні можна крутити мишею. Точка — річ, якої ти ще не бачив.", h, Outfits.WardrobeLevel, GearFx.HighLevel, RavenPouch.Max);
            }
        }

        // ---------------------------------------------------------------- the tooltip

        private static RavenBookHover _hover;
        private static Vector2 _pointer;

        internal static void HoverOn(RavenBookHover h, Vector2 screen) { _hover = h; _pointer = screen; ShowTip(); }
        internal static void HoverMove(RavenBookHover h, Vector2 screen) { if (_hover == h) _pointer = screen; }
        internal static void HoverOff(RavenBookHover h)
        {
            if (_hover != h) return;
            RavenBookHover up = h != null && h.isActiveAndEnabled && h.transform.parent != null ? h.transform.parent.GetComponentInParent<RavenBookHover>() : null;
            if (up != null && up.isActiveAndEnabled) { _hover = up; ShowTip(); }
            else HideTip();
        }

        private static float _tipRenew;

        private static void ShowTip()
        {
            if (_tip == null || _hover == null || _hover.Text == null) { HideTip(); return; }
            string text;
            try { text = _hover.Text(); } catch { text = null; }
            if (string.IsNullOrEmpty(text)) { HideTip(); return; }
            _tipText.text = text;
            List<RavenBookHover.Icon> icons = null;
            try { if (_hover.Icons != null) icons = _hover.Icons(); } catch { icons = null; }
            int n = icons != null ? icons.Count : 0, cols = Mathf.Min(TipIconCols, n), rows = cols > 0 ? (n + cols - 1) / cols : 0;
            Vector2 size = _tipText.GetPreferredValues(text, 420f, 0f);
            float w = Mathf.Max(Mathf.Min(420f, size.x), cols * TipIconCell) + 28f;
            Vector2 real = _tipText.GetPreferredValues(text, w - 28f, 0f);
            float iconsH = rows > 0 ? rows * TipIconCell + 8f : 0f;
            _tip.sizeDelta = new Vector2(w, real.y + 24f + iconsH);
            _tipText.rectTransform.offsetMin = new Vector2(14f, 12f + iconsH);
            TipIcons(icons, cols);
            _tip.gameObject.SetActive(true);
            _tip.SetAsLastSibling();
            _tipRenew = Time.unscaledTime + 1f;
            MoveTip();
        }

        private static void HideTip()
        {
            _hover = null;
            if (_tip != null) _tip.gameObject.SetActive(false);
        }

        private static void MoveTip()
        {
            if (_tip == null || !_tip.gameObject.activeSelf) return;
            if (_hover == null || !_hover.isActiveAndEnabled) { HideTip(); return; }
            if (Time.unscaledTime >= _tipRenew) { ShowTip(); return; } // the live state in it (a cooldown) goes on
            // the pointer on the book's plane (through the book's camera), in the layer's units: x across from the
            // book's middle, y up from its lower edge; kept inside the window
            Vector2 p;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_over, _pointer, _cam, out p)) return;
            Vector2 s = _tip.sizeDelta;
            float left = -W / 2f + 8f, right = W / 2f - 8f, bottom = 8f - (H - BookTop - BookH), top = BookTop + BookH - 8f;
            float x = p.x + 18f, y = p.y - 18f;
            if (x + s.x > right) x = p.x - 18f - s.x;
            if (y - s.y < bottom) y = bottom + s.y;
            x = Mathf.Max(left, x);
            y = Mathf.Min(top, y);
            _tip.anchoredPosition = new Vector2(x, y);
        }

        // the tooltip's pictures, under its text: a grid of cells, dim when Lit is false, a gold star in the corner
        internal static int TipIconCols = 8;
        internal static float TipIconCell = 40f;
        private static RectTransform _tipIcons;
        private static void TipIcons(List<RavenBookHover.Icon> icons, int cols)
        {
            if (_tipIcons == null)
            {
                _tipIcons = Rect("Icons", _tip);
                _tipIcons.anchorMin = new Vector2(0f, 0f); _tipIcons.anchorMax = new Vector2(1f, 0f); _tipIcons.pivot = new Vector2(0f, 0f);
                _tipIcons.offsetMin = new Vector2(14f, 12f); _tipIcons.offsetMax = new Vector2(-14f, 12f);
            }
            for (int i = _tipIcons.childCount - 1; i >= 0; i--) { GameObject old = _tipIcons.GetChild(i).gameObject; old.SetActive(false); UnityEngine.Object.Destroy(old); }
            int n = icons != null ? icons.Count : 0;
            int rows = cols > 0 ? (n + cols - 1) / cols : 0;
            _tipIcons.sizeDelta = new Vector2(0f, rows * TipIconCell);
            for (int i = 0; i < n; i++)
            {
                RectTransform cell = Rect("Icon", _tipIcons);
                cell.anchorMin = cell.anchorMax = new Vector2(0f, 1f); cell.pivot = new Vector2(0f, 1f);
                cell.sizeDelta = new Vector2(TipIconCell - 4f, TipIconCell - 4f);
                cell.anchoredPosition = new Vector2((i % cols) * TipIconCell, -(i / cols) * TipIconCell);
                Image img = cell.gameObject.AddComponent<Image>();
                img.sprite = icons[i].Sprite; img.preserveAspect = true; img.raycastTarget = false;
                img.color = icons[i].Lit ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                if (icons[i].Star)
                {
                    TMP_Text star = T(cell, "★", 14f, Gold, F.Body, TextAlignmentOptions.TopRight);
                    Stretch(star.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
            }
        }

        // a tooltip with a row of pictures under its text (the food menu)
        private static void TipIcons(GameObject g, Func<string> text, Func<List<RavenBookHover.Icon>> icons)
        {
            Tip(g, text);
            g.GetComponent<RavenBookHover>().Icons = icons;
        }

        private static void Tip(GameObject g, Func<string> text)
        {
            Graphic gr = g.GetComponent<Graphic>();
            if (gr != null) gr.raycastTarget = true;
            else g.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            RavenBookHover h = g.GetComponent<RavenBookHover>();
            if (h == null) h = g.AddComponent<RavenBookHover>();
            h.Text = text;
        }

        // ---------------------------------------------------------------- small pieces

        private enum F { Head, Body, Italic, Rune }

        private static TMP_FontAsset FontOf(F f)
        {
            switch (f)
            {
                case F.Head: return BookArt.Heading ?? BookArt.Body ?? _gameFont;
                case F.Italic: return BookArt.Italic ?? BookArt.Body ?? _gameFont;
                case F.Rune: return BookArt.Runic ?? BookArt.Body ?? _gameFont;
                default: return BookArt.Body ?? _gameFont;
            }
        }
        private static string HeadName() { TMP_FontAsset f = FontOf(F.Head); return f != null ? f.name : ""; }

        private static TMP_Text T(Transform parent, string text, float size, Color color, F font = F.Body, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            RectTransform r = Rect("Text", parent);
            TMP_FontAsset f = FontOf(font);
            TextMeshProUGUI t = SpeechCard.NewText(r.gameObject, f);
            if (font == F.Italic && BookArt.Italic == null) t.fontStyle = FontStyles.Italic; // no real italic: the slanted one
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        // one line that must stay in its room (a tab, a rung of the ladder): smaller down to min, then cut with «…» —
        // so a longer translation fits where Ukrainian did
        // Not TMP's auto-size: inside a layout it rocks (the size changes the width, the width the size) and the rows under
        // it twitched. Its room comes from the layout alone (no width of its own), BookFit sets the size for that room.
        private static TMP_Text Shrink(TMP_Text t, float min)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement le = t.GetComponent<LayoutElement>();
            if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f; le.preferredWidth = 0f; if (le.flexibleWidth < 1f) le.flexibleWidth = 1f;
            BookFit fit = t.gameObject.AddComponent<BookFit>();
            fit.Text = t; fit.Max = t.fontSize; fit.Min = Mathf.Min(min, t.fontSize);
            return t;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.layer = parent.gameObject.layer;
            g.transform.SetParent(parent, false);
            return (RectTransform)g.transform;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            Image i = Rect(name, parent).gameObject.AddComponent<Image>();
            Stretch(i.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            i.sprite = sprite; i.color = color; i.raycastTarget = false;
            return i;
        }

        // a rounded fill with a thin rounded frame over it (width 0: no frame)
        private static void Box(RectTransform r, float radius, Color fill, Color line, float width)
        {
            Image f = r.gameObject.GetComponent<Image>();
            if (f == null) f = r.gameObject.AddComponent<Image>();
            f.sprite = BookArt.RoundFine(radius); f.type = Image.Type.Sliced; f.pixelsPerUnitMultiplier = BookArt.Fine; f.color = fill;
            if (width <= 0f || line.a <= 0f) return;
            Image l = Rect("Line", r).gameObject.AddComponent<Image>();
            Stretch(l.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            l.sprite = BookArt.FrameFine(radius, width); l.type = Image.Type.Sliced; l.pixelsPerUnitMultiplier = BookArt.Fine; l.color = line; l.raycastTarget = false;
            l.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // a card on paper with gnawed edges (the light cards of the book: gifts, ritual steps, gear, runes): its fill and a
        // frame along the same torn edge (width 0: no frame)
        private static void Torn(RectTransform r, Color fill, Color line, float width)
        {
            Image f = r.gameObject.GetComponent<Image>();
            if (f == null) f = r.gameObject.AddComponent<Image>();
            // its own way of torn: by its name and place among its neighbours (the same card looks the same each time)
            int v = r.GetSiblingIndex();
            foreach (char ch in r.name) v = v * 31 + ch;
            v &= 0x7fffffff;
            f.sprite = BookArt.TornFill(v); f.type = Image.Type.Tiled; f.pixelsPerUnitMultiplier = BookArt.Fine; f.color = fill;
            if (width <= 0f || line.a <= 0f) return;
            Image l = Rect("Line", r).gameObject.AddComponent<Image>();
            Stretch(l.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (line == BookArt.CardLine) line = A(line, 0.32f); // the plain card: its torn edge only a shade darker, no inked frame (states keep their colour)
            l.sprite = BookArt.TornLine(width, v); l.type = Image.Type.Tiled; l.pixelsPerUnitMultiplier = BookArt.Fine; l.color = line; l.raycastTarget = false;
            l.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // a sheet pinned into the book by four photo corners, a little askew (Odin's errand); no shadow round it (the user:
        // it read as a grey frame). Called before the card's words go in (the sheet lies under them). seed: its own tilt, kept the same
        private static void Pinned(RectTransform r, Color fill, Color corners, string seed)
        {
            int h = 0;
            foreach (char ch in seed ?? "") h = h * 31 + ch;
            r.localRotation = Quaternion.Euler(0f, 0f, ((h & 0x7fffffff) % 5 - 2) * 0.6f); // −1.2…1.2°: the layout keeps its place, only turns it
            Image sheet = Img(r, "Sheet", BookArt.SheetFill, fill);
            sheet.type = Image.Type.Tiled; sheet.pixelsPerUnitMultiplier = BookArt.Fine;
            sheet.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Vector2[] at = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) };
            for (int c = 0; c < 4; c++)
            {
                Image k = Img(r, "Corner" + c, BookArt.PhotoCorner(c), corners);
                RectTransform kr = k.rectTransform;
                kr.anchorMin = kr.anchorMax = kr.pivot = at[c];
                kr.sizeDelta = new Vector2(BookArt.CornerSize, BookArt.CornerSize);
                kr.anchoredPosition = new Vector2(at[c].x > 0.5f ? 4f : -4f, at[c].y > 0.5f ? 4f : -4f); // a little over the sheet's edge
                k.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
        }

        // a wax seal with its words: the raven's ("munin", "hugin"), a grey stone one ("stone") or a plain dark one ("plain")
        private static RectTransform SealButton(Transform parent, string caption, string kind, float size, float font, Action click, bool enabled, float alpha)
        {
            RectTransform r = Rect("Seal", parent);
            Fixed(r.gameObject, size, size);
            if (alpha < 1f) r.gameObject.AddComponent<CanvasGroup>().alpha = alpha;
            Image sh = Img(r, "Shadow", BookArt.SoftDisc, new Color(0f, 0f, 0f, 0.35f));
            Stretch(sh.rectTransform, Vector2.zero, Vector2.one, new Vector2(-8f, -12f), new Vector2(8f, 4f));
            Sprite face = kind == "munin" ? BookArt.Seal(BookArt.Munin, BookArt.MuninDark)
                : kind == "hugin" ? BookArt.Seal(BookArt.Hugin, BookArt.HuginDark)
                : kind == "stone" ? BookArt.Seal(C("#8a7a5a"), C("#5a4a30"))
                : BookArt.Seal(C("#6a5a40"), C("#5a4a30"));
            Image f = Img(r, "Face", face, Color.white);
            f.raycastTarget = true;
            TMP_Text t = T(f.transform, caption, font, SealInk, F.Head, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(size * 0.12f, size * 0.1f), new Vector2(-size * 0.12f, -size * 0.1f));
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Min(11f, font); t.fontSizeMax = font;
            t.lineSpacing = -18f;
            if (click == null && enabled) { f.gameObject.AddComponent<BookGlow>(); return r; } // only shown: it lights up, nothing to press
            Button b = f.gameObject.AddComponent<Button>();
            b.targetGraphic = f;
            Tint(b);
            b.interactable = enabled;
            if (click != null) b.onClick.AddListener(() => click());
            return r;
        }

        // an outlined button on paper (the raven's return): can be pressed — filled with the initials' gold;
        // can't — empty, its ink a little greyer
        private static readonly Color InkGrey = Color.Lerp(BookArt.Ink, C("#8a8580"), 0.45f);
        private static RectTransform OutlineButton(Transform parent, string caption, Action click, bool enabled)
        {
            RectTransform r = Rect("Button", parent);
            HBox(r, 0f, TextAnchor.MiddleCenter, 16, 16, 5, 6);
            Color ink = enabled ? Ink : InkGrey;
            Box(r, 6f, enabled ? BookArt.Initial : new Color(0f, 0f, 0f, 0f), ink, 1.5f);
            T(r, caption, 16f, ink, F.Head, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.interactable = enabled;
            b.onClick.AddListener(() => click());
            return r;
        }

        // a dark button on paper (the pouch)
        private static RectTransform DarkButton(Transform parent, string caption, Action click)
        {
            RectTransform r = Rect("Button", parent);
            HBox(r, 0f, TextAnchor.MiddleCenter, 14, 14, 5, 6);
            Box(r, 6f, Ink, new Color(0f, 0f, 0f, 0f), 0f);
            T(r, caption, 16f, Paper, F.Head, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.onClick.AddListener(() => click());
            return r;
        }

        // a small button of a row (fitting, views): dark, gold when on
        private static RectTransform SmallButton(Transform parent, string caption, bool on, Action click)
        {
            RectTransform r = Rect("Button", parent);
            HBox(r, 0f, TextAnchor.MiddleCenter, 6, 6, 3, 3);
            Box(r, 5f, on ? C("#a8843a") : Ink, new Color(0f, 0f, 0f, 0f), 0f);
            TMP_Text t = T(r, caption, 13f, on ? SealInk : Paper, F.Body, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(r.gameObject, -1f, 26f);
            Button b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            Tint(b);
            b.onClick.AddListener(() => click());
            return r;
        }

        // anything clicked (its graphic catches the cursor)
        private static void Clickable(GameObject g, Action click)
        {
            Graphic gr = g.GetComponent<Graphic>();
            if (gr == null) { gr = g.AddComponent<Image>(); gr.color = new Color(0f, 0f, 0f, 0f); }
            gr.raycastTarget = true;
            Button b = g.GetComponent<Button>();
            if (b == null) b = g.AddComponent<Button>();
            b.targetGraphic = gr;
            Tint(b);
            b.onClick.AddListener(() => click());
        }

        private static void Tint(Button b)
        {
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.highlightedColor = Color.white;
            cb.selectedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            // setting colors fades the graphic from white (a fresh one's colour) to rest: on every redraw (a click) all the
            // page's cards lit up at once. Stand at rest now; the one under the cursor lights up by itself
            if (b.targetGraphic != null) b.targetGraphic.CrossFadeColor(cb.normalColor, 0f, true, true);
            Navigation n = b.navigation; n.mode = Navigation.Mode.None; b.navigation = n;
            b.onClick.AddListener(() => Unselect(b.gameObject)); // a clicked button stays selected: Enter or Space would press it again
        }

        private static void Unselect(GameObject g)
        {
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == g) es.SetSelectedGameObject(null);
        }

        // a big place that only closes or switches something (the "?" card): clickable, but it does not light up whole
        private static void ClickArea(GameObject g, Action click)
        {
            Clickable(g, click);
            Button b = g.GetComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (b.targetGraphic != null) b.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        }

        private static Color A(Color c, float a) { c.a = a; return c; }

        private static Sprite ItemIcon(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            if (d == null) return null;
            try { return BookArt.Smooth(d.m_itemData.GetIcon()); } catch { return null; } // mipmapped: no ladders on the tilted page
        }

        // at x, top (from the parent's top-left), this size
        private static void At(RectTransform r, float x, float top, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -top);
            r.sizeDelta = new Vector2(w, h);
        }

        private static HorizontalLayoutGroup HBox(RectTransform r, float spacing, TextAnchor align, int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            HorizontalLayoutGroup h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align; h.padding = new RectOffset(left, right, top, bottom);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        private static VerticalLayoutGroup VBox(RectTransform r, float spacing, TextAnchor align, int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            VerticalLayoutGroup v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align; v.padding = new RectOffset(left, right, top, bottom);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        // empty room that takes up the rest: across in a row, down in a column (never both, or a row grows tall)
        private static void Spring(Transform parent)
        {
            LayoutElement le = Rect("Spring", parent).gameObject.AddComponent<LayoutElement>();
            if (parent.GetComponent<HorizontalLayoutGroup>() != null) le.flexibleWidth = 1f; else le.flexibleHeight = 1f;
        }

        // a fixed size in a layout (-1: left to the layout)
        private static void Fixed(GameObject g, float w, float h)
        {
            LayoutElement le = g.GetComponent<LayoutElement>();
            if (le == null) le = g.AddComponent<LayoutElement>();
            if (w >= 0f) { le.minWidth = le.preferredWidth = w; le.flexibleWidth = 0f; }
            if (h >= 0f) { le.minHeight = le.preferredHeight = h; le.flexibleHeight = 0f; }
        }

        private static void Stretch(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
        }
    }

    // The book's raycaster: its canvas is drawn by a camera, and a camera canvas would answer after every overlay of the
    // game; this one answers with the book's own place (just under the book's overlay: the tooltip and the food list).
    internal sealed class BookRaycaster : GraphicRaycaster
    {
        public override int sortOrderPriority { get { return 30000; } }
        public override int renderOrderPriority { get { return 30000; } }
    }

    // under the cursor: the book's own tooltip
    internal sealed class RavenBookHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Func<string> Text;
        public Func<List<Icon>> Icons; // a row of pictures under the text (the food menu), or none
        internal sealed class Icon { public Sprite Sprite; public bool Lit = true, Star; }
        public void OnPointerEnter(PointerEventData e) { RavenBook.HoverOn(this, e.position); }
        public void OnPointerMove(PointerEventData e) { RavenBook.HoverMove(this, e.position); }
        public void OnPointerExit(PointerEventData e) { RavenBook.HoverOff(this); }
        private void OnDisable() { RavenBook.HoverOff(this); }
    }

    // words that are a link (the Discord leaf): Discord's own blue under the cursor, a click opens it
    internal sealed class BookLink : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        internal static readonly Color Discord = new Color(0x58 / 255f, 0x65 / 255f, 0xF2 / 255f, 1f); // #5865F2
        public Color Rest;
        public Action Click;
        private void Set(Color c) { Graphic g = GetComponent<Graphic>(); if (g != null) g.color = c; }
        public void OnPointerEnter(PointerEventData e) { Set(Discord); }
        public void OnPointerExit(PointerEventData e) { Set(Rest); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && Click != null) Click(); }
        private void OnDisable() { Set(Rest); }
    }

    // a nickname's cell under the cursor: it grows a little, its paper darkens, its rim turns gold (eased, unscaled time)
    internal sealed class NickHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Fill, Line;
        public Color FillOn, LineOn;
        private Color _fillRest, _lineRest;
        private bool _on, _rested;
        private float _k; // 0 at rest … 1 under the cursor
        private void Rest() { if (_rested) return; _rested = true; if (Fill != null) _fillRest = Fill.color; if (Line != null) _lineRest = Line.color; }
        public void OnPointerEnter(PointerEventData e) { Rest(); _on = true; }
        public void OnPointerExit(PointerEventData e) { _on = false; }
        private void OnDisable() { _on = false; _k = 0f; Apply(); }
        private void Update()
        {
            if (!_rested) return;
            float to = _on ? 1f : 0f;
            if (Mathf.Approximately(_k, to)) return;
            _k = Mathf.MoveTowards(_k, to, Time.unscaledDeltaTime / 0.1f);
            Apply();
        }
        private void Apply()
        {
            if (!_rested) return;
            float s = Mathf.SmoothStep(0f, 1f, _k);
            transform.localScale = Vector3.one * (1f + 0.04f * s);
            if (Fill != null) Fill.color = Color.Lerp(_fillRest, FillOn, s);
            if (Line != null) Line.color = Color.Lerp(_lineRest, LineOn, s);
        }
    }

    // a seal that is only shown (the training level): it lights up under the cursor like a button, without being one
    internal sealed class BookGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color Rest = new Color(0.92f, 0.92f, 0.92f, 1f); // as the buttons rest (Tint)
        private void Start() { Graphic g = GetComponent<Graphic>(); if (g != null) g.CrossFadeColor(Rest, 0f, true, true); }
        public void OnPointerEnter(PointerEventData e) { Graphic g = GetComponent<Graphic>(); if (g != null) g.CrossFadeColor(Color.white, 0.08f, true, true); }
        public void OnPointerExit(PointerEventData e) { Graphic g = GetComponent<Graphic>(); if (g != null) g.CrossFadeColor(Rest, 0.08f, true, true); }
        private void OnDisable() { Graphic g = GetComponent<Graphic>(); if (g != null) g.CrossFadeColor(Rest, 0f, true, true); }
    }

    // the raven's picture turns as the mouse drags it
    internal sealed class RavenBookDrag : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e) { OutfitStage.Turn(-e.delta.x * 0.5f); }
    }

    // While the book is open the game treats it like the trader's window: the cursor shows, the hero and the
    // camera stand still, the hotbar and Esc (the main menu) leave it alone.
    [HarmonyPatch(typeof(StoreGui), "IsVisible")]
    internal static class RavenBookAsWindowPatch
    {
        private static void Postfix(ref bool __result) { if (RavenBook.IsOpen || RavenBook.NestWindowOpen || RavenBook.TreeWindowOpen || RavenBook.LetterOpen) __result = true; }
    }
}
#endif

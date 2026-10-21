using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The ravens' words in a card of their own instead of the game's small speech bubble, which is easy to run past.
    // A card has a coloured frame (Hugin blue, Munin red), the raven's round picture and name, and the line.
    // While the raven (or the player a shoulder raven sits on) is in view and near, the card hangs over it.
    // Otherwise a card of your own raven moves under the hotbar with a small arrow towards the raven, and when it
    // first lands there its frame flashes, so a raven that came to you is never missed. Lines that follow each other
    // (a talk between the ravens) stack under the hotbar, the older ones dimmed. Other players' ravens only hang over
    // them. Nothing is shown while the inventory, the map or a menu is open; the time keeps running.
    internal static class RavenSpeech
    {
        internal static bool Enabled = true;
        internal static readonly Color HuginFrame = new Color(0.30f, 0.62f, 0.95f, 1f);  // the glow and the picture's ring
        internal static readonly Color MuninFrame = new Color(0.90f, 0.22f, 0.20f, 1f);
        internal static readonly Color Edge = new Color(1f, 1f, 1f, 0.06f);            // the capsule's outline: barely there (design 5a)
        internal const string HuginName = "#6EC1FF";
        internal const string MuninName = "#FF6A55";
        // the Guide (a raven of the game that teaches): amber, so it is never taken for Hugin or Munin
        internal static readonly Color GuideFrame = new Color(0.95f, 0.66f, 0.18f, 1f);
        internal const string GuideName = "#FFB84D", GuideTitle = "Провідник";
        internal const float MaxWidth = 470f;       // the words, px at 1080p
        internal const float FadeFrom = 25f;        // a card over a raven is clear up to here...
        internal const float DockAlpha = 0.82f;     // ...your own raven's fades only to this, and goes under the hotbar just as bright
        internal const float FarFrom = 20f, FarAlpha = 0.5f; // your raven left this far behind: the card under the hotbar pales, to half at 40 m
        internal const float FadeTo = 70f;          // ...and melts away by here (just a number on its alpha: costs nothing to draw)

        private static SpeechHost _host;

        internal static SpeechHost Host
        {
            get
            {
                if (_host != null) return _host;
                GameObject go = new GameObject("RavenWatch_Speech");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<SpeechHost>();
                return _host;
            }
        }

        // a raven speaks: over the talker, or under the hotbar if this is your own raven and it is not in view
        // with the voice on, the card comes at once with three dots while its sound is made; then the words open one by
        // one along the voice (RavenVoice, WordReveal)
        internal static void Show(GameObject talker, Vector3 offset, float range, float seconds, bool munin, string name, string text, bool dockable)
        {
            if (talker == null || string.IsNullOrEmpty(text)) return;
            int voice = RavenVoice.VoiceOf(dockable ? Player.m_localPlayer : null, munin, talker);
            SpeechCard card = null;
            bool voiced = RavenVoice.Say(talker, munin, voice, text, s => Voiced(card, seconds, s));
            card = ShowNow(talker, offset, range, seconds, munin, name, text, dockable);
            if (voiced && card != null) card.Typing();
        }

        private static SpeechCard ShowNow(GameObject talker, Vector3 offset, float range, float seconds, bool munin, string name, string text, bool dockable)
        {
            if (talker == null) return null;
            return Host.Show(talker, offset, Mathf.Min(range > 0f ? range : FadeTo, FadeTo), Mathf.Max(Mathf.Max(1.5f, seconds), ReadSeconds(text)), munin, name, text, dockable);
        }

        // the voice is ready (or gave up: 0 s): the words open along it, and the card stays a little past its end;
        // false = the card is already gone (the raven flew off, the card was pushed out): no voice without its words
        private static bool Voiced(SpeechCard card, float seconds, float sound)
        {
            if (card == null || card.Ending) return false;
            card.Reveal(sound);
            card.Extend(Mathf.Max(Mathf.Max(seconds, ReadSeconds(card.Text)) * 0.6f, sound + 1.5f));
            return true;
        }

        // time to read a line calmly while playing: about 11 letters a second plus a moment to notice it, 7 to 35 s
        internal static float ReadSeconds(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int letters = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "").Length;
            return Mathf.Clamp(3f + letters / 11f, 7f, 35f);
        }

        // another player's raven (or one on a perch): it hangs over it in the world, not under your hotbar, and you
        // are not the one it speaks to, so it is noticed later; it is kept a little longer
        internal const float WorldMul = 1.25f;
        // a card that cannot be read (a menu or the map open, or one in the world out of view or too far) keeps its
        // time for later, up to this much in all
        internal const float HoldMax = 15f;

        // a shoulder raven speaks: the card hangs over the raven itself wherever it is (over the dome, on its way to a
        // strike, hovering, flying off), and over the player when the raven cannot be seen
        internal static void ShowShoulder(Player who, bool munin, float seconds, string text)
        {
            if (who == null || string.IsNullOrEmpty(text)) return;
            SpeechCard card = null;
            bool voiced = RavenVoice.Say(who.gameObject, munin, RavenVoice.VoiceOf(who, munin, who.gameObject), text, s => Voiced(card, seconds, s));
            card = ShowShoulderNow(who, munin, seconds, text);
            if (voiced && card != null) card.Typing();
        }

        private static SpeechCard ShowShoulderNow(Player who, bool munin, float seconds, string text)
        {
            if (who == null) return null;
            SpeechCard card = Host.Show(who.gameObject, Vector3.up * 2.3f, RavenSpeech.FadeTo, Mathf.Max(seconds, ReadSeconds(text)), munin, Nicks.Title(who, munin), text, who == Player.m_localPlayer); // another player's: «Кривдзьоб · Валя»
            if (card != null) card.Follow = () => Shoulder.SpeakerOf(who, munin);
            return card;
        }

        // the players' names in a raven's words, in a quiet warm tone: whom it speaks to stands out a little; a raven's
        // nickname (as said, or as called: «Кривдзьоб», «Кривдзьобе») in its kind's colour, so a raven named in a talk
        // stands out too
        internal const string NameTone = "#D9C7A0";
        private const string SimpleNickTone = "#C8D0D8"; // (the plain kind's own grey is darker than the words: lifted)
        private static readonly List<KeyValuePair<string, string>> Names = new List<KeyValuePair<string, string>>();
        private static void AddName(string n, string tone)
        {
            if (string.IsNullOrEmpty(n) || n.Trim().Length < 2) return;
            foreach (KeyValuePair<string, string> kv in Names) if (kv.Key == n) return;
            Names.Add(new KeyValuePair<string, string>(n, tone));
        }

        internal static string MarkNames(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Names.Clear();
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null) continue;
                foreach (bool m in new[] { false, true })
                {
                    string name, call; Nicks.Kind kind;
                    if (!Nicks.Of(pl, m, out name, out call, out kind)) continue;
                    string tone = kind == Nicks.Kind.Simple ? SimpleNickTone : Nicks.Hex(kind);
                    AddName(name, tone); AddName(call, tone);
                }
                AddName(pl.GetPlayerName(), NameTone);
            }
            if (Game.instance != null && Game.instance.GetPlayerProfile() != null) AddName(Game.instance.GetPlayerProfile().GetName(), NameTone);
            if (Names.Count == 0) return text;
            Names.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length)); // a longer name first ("Valentina" before "Val")
            try
            {
                foreach (KeyValuePair<string, string> n in Names)
                    text = System.Text.RegularExpressions.Regex.Replace(text,
                        "(?<![\\p{L}\\p{N}>=#])" + System.Text.RegularExpressions.Regex.Escape(n.Key) + "(?![\\p{L}\\p{N}<])",
                        "<color=" + n.Value + ">$0</color>");
            }
            catch { }
            return text;
        }

        internal static bool Has(GameObject talker) { return _host != null && _host.Has(talker); }
        internal static void Clear(GameObject talker) { if (_host != null) _host.Clear(talker); }

        // the capsule: a filled rounded shape and its thin outline (1 px), stretched without bending the round ends
        internal const float Radius = 24f;
        private static Sprite _fill, _ring;
        internal static Sprite Fill() { if (_fill == null) _fill = Rounded(false); return _fill; }
        internal static Sprite Ring() { if (_ring == null) _ring = Rounded(true); return _ring; }

        // drawn four times finer than it is shown (and shrunk with mipmaps), so the round ends and the 1 px line
        // stay clean at any screen size instead of showing their pixels
        private const int Fine = 4;
        private static Sprite Rounded(bool outline)
        {
            const int n = 64 * Fine; const float r = Radius * Fine; const float line = 1f * Fine;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (outline) a -= Mathf.Clamp01(r - line - d + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            float b = r + 2f * Fine;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f * Fine, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // the dark behind the words: thickest in the middle, fading out to nothing at the card's edges
        private static Sprite _glow;
        internal static Sprite Glow()
        {
            if (_glow != null) return _glow;
            const int n = 128;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    // a soft rounded rectangle: flat over the middle, falling off towards the edges
                    float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), 4f) + Mathf.Pow(Mathf.Abs(dy), 4f), 0.25f);
                    float a = 1f - Mathf.SmoothStep(0.35f, 1f, d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _glow = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _glow;
        }

        // a small triangle for the arrow, pointing right
        private static Sprite _arrow;
        internal static Sprite Arrow()
        {
            if (_arrow != null) return _arrow;
            const int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n, fy = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f; // 0 at the middle line
                    float edge = 1f - fx;                                                 // the slanted sides
                    float a = Mathf.Clamp01((edge - fy) * n * 0.5f + 0.5f) * Mathf.Clamp01(fx * n);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _arrow = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _arrow;
        }

        // the game's own font of the centre messages; with SeneaL UI its own fonts (SeneaLUI.Theme.Fonts: Regular for the
        // words, Bold for the name), so the cards read like the rest of its interface
        private static readonly FieldInfo CenterText = AccessTools.Field(typeof(MessageHud), "m_messageCenterText");
        private static bool _seneaLFontsLooked;
        private static FieldInfo _seneaLRegular, _seneaLBold;

        private static TMP_FontAsset SeneaLFont(bool bold)
        {
            if (!_seneaLFontsLooked)
            {
                _seneaLFontsLooked = true;
                try
                {
                    Type fonts = AccessTools.TypeByName("SeneaLUI.Theme.Fonts");
                    if (fonts != null)
                    {
                        _seneaLRegular = AccessTools.Field(fonts, "Regular");
                        _seneaLBold = AccessTools.Field(fonts, "Bold");
                    }
                }
                catch { _seneaLRegular = _seneaLBold = null; }
            }
            FieldInfo f = bold ? (_seneaLBold ?? _seneaLRegular) : _seneaLRegular;
            if (f == null || f.FieldType != typeof(TMP_FontAsset)) return null;
            try { return f.GetValue(null) as TMP_FontAsset; } catch { return null; }
        }

        internal static TMP_FontAsset Font(bool bold)
        {
            TMP_FontAsset own = SeneaLFont(bold);
            if (own != null) return own;
            return Font();
        }

        internal static TMP_FontAsset Font()
        {
            TMP_Text t = MessageHud.instance != null && CenterText != null ? CenterText.GetValue(MessageHud.instance) as TMP_Text : null;
            return t != null ? t.font : null;
        }

        // ---------------------------------------------------------------- where the hotbar is

        private static bool _hotLooked;
        private static FieldInfo _seneaLHotbar;   // SeneaLUI.Hud.HudRoot._elHotbar
        private static FieldInfo _seneaLLayer;    // its Layer
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly List<Graphic> Parts = new List<Graphic>(64);
        private static HotkeyBar _bar;
        private static float _barLooked = -999f;

        // the bottom left corner of the hotbar on the screen, in pixels; false when it cannot be found
        internal static bool HotbarBottomLeft(out Vector2 at)
        {
            at = Vector2.zero;
            RectTransform root = HotbarRoot();
            if (root == null || !root.gameObject.activeInHierarchy) return false;
            float minX = float.MaxValue, minY = float.MaxValue;
            bool any = false;
            root.GetComponentsInChildren<Graphic>(false, Parts);
            foreach (Graphic g in Parts)
            {
                if (g == null || !g.enabled || g.color.a <= 0.01f) continue;
                g.rectTransform.GetWorldCorners(Corners);
                minX = Mathf.Min(minX, Corners[0].x);
                minY = Mathf.Min(minY, Corners[0].y);
                any = true;
            }
            if (!any) return false;
            at = new Vector2(minX, minY);
            return true;
        }

        private static RectTransform HotbarRoot()
        {
            if (!_hotLooked)
            {
                _hotLooked = true;
                try
                {
                    Type hud = AccessTools.TypeByName("SeneaLUI.Hud.HudRoot");
                    _seneaLHotbar = hud != null ? AccessTools.Field(hud, "_elHotbar") : null;
                    _seneaLLayer = _seneaLHotbar != null ? AccessTools.Field(_seneaLHotbar.FieldType, "Layer") : null;
                }
                catch { _seneaLHotbar = null; _seneaLLayer = null; }
            }
            if (_seneaLHotbar != null && _seneaLLayer != null)
            {
                try
                {
                    object el = _seneaLHotbar.GetValue(null);
                    RectTransform layer = el != null ? _seneaLLayer.GetValue(el) as RectTransform : null;
                    if (layer != null && layer.gameObject.activeInHierarchy) return layer;
                }
                catch { }
            }
            // the game's own hotbar: looked for in the scene only now and then, not every check
            if (_bar == null && Time.unscaledTime - _barLooked > 10f) { _barLooked = Time.unscaledTime; _bar = UnityEngine.Object.FindFirstObjectByType<HotkeyBar>(); }
            return _bar != null && _bar.isActiveAndEnabled ? _bar.transform as RectTransform : null;
        }
    }

    // the canvas with the cards, and their life
    internal sealed class SpeechHost : MonoBehaviour
    {
        private Canvas _canvas;
        private readonly List<SpeechCard> _cards = new List<SpeechCard>();
        private float _hotCheck;
        private Vector2 _hotAt;
        private bool _hotFound;

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5; // over the HUD, under the game's menus (which hide the cards anyway)
            CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 1f;
        }

        internal float Scale { get { return _canvas != null ? _canvas.scaleFactor : 1f; } }

        internal bool Has(GameObject talker)
        {
            foreach (SpeechCard c in _cards) if (c.Talker == talker && !c.Ending) return true;
            return false;
        }

        internal void Clear(GameObject talker)
        {
            foreach (SpeechCard c in _cards) if (c.Talker == talker) c.End();
        }

        internal SpeechCard Show(GameObject talker, Vector3 offset, float range, float seconds, bool munin, string name, string text, bool dockable)
        {
            if (!dockable) seconds *= RavenSpeech.WorldMul;
            // the same words again (the game repeats them while the raven sits): only the time is kept going
            foreach (SpeechCard c in _cards)
                if (c.Talker == talker && c.IsMunin == munin && c.Text == text && !c.Ending) { c.Extend(seconds); return c; }
            foreach (SpeechCard c in _cards) if (c.Talker == talker && c.IsMunin == munin) c.End(); // a new line from the same raven replaces the old one
            SpeechCard card = SpeechCard.Make(transform, munin, name, text);
            card.Talker = talker; card.Offset = offset; card.Range = range; card.Dockable = dockable;
            card.Extend(seconds);
            _cards.Add(card);
            while (CountDocked() > 3) OldestDocked().End(); // a long talk: the oldest leaves
            while (CountForeign() > MaxForeign) OldestForeign().End(); // many players' ravens at once: the screen is not papered over
            return card;
        }

        // cards hanging over ravens that stand close together would lie on each other: each one that touches a card
        // below it goes up above it (the lowest keep their places, so the nearest talker stays where it is)
        private readonly List<SpeechCard> _over = new List<SpeechCard>();
        private void Spread(float s)
        {
            _over.Clear();
            foreach (SpeechCard c in _cards) if (!c.Dead && c.Over && !c.Docked && !c.Ending) _over.Add(c);
            if (_over.Count < 2) return;
            _over.Sort((a, b) => (a.Target.y - a.H).CompareTo(b.Target.y - b.H));
            float gap = 6f * s;
            for (int i = 1; i < _over.Count; i++)
            {
                SpeechCard c = _over[i];
                for (int pass = 0; pass < _over.Count; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        SpeechCard o = _over[j];
                        bool side = c.Target.x < o.Target.x + o.W && o.Target.x < c.Target.x + c.W;
                        bool up = c.Target.y - c.H < o.Target.y + gap && o.Target.y - o.H < c.Target.y;
                        if (!side || !up) continue;
                        c.Target = new Vector2(c.Target.x, o.Target.y + gap + c.H);
                        moved = true;
                    }
                    if (!moved) break;
                }
            }
        }

        internal const int MaxForeign = 3; // other players' ravens talking at the same time
        private int CountForeign() { int n = 0; foreach (SpeechCard c in _cards) if (!c.Dockable && !c.Ending) n++; return n; }
        private SpeechCard OldestForeign() { foreach (SpeechCard c in _cards) if (!c.Dockable && !c.Ending) return c; return null; }

        private int CountDocked() { int n = 0; foreach (SpeechCard c in _cards) if (c.Docked && !c.Ending) n++; return n; }
        private SpeechCard OldestDocked() { foreach (SpeechCard c in _cards) if (c.Docked && !c.Ending) return c; return null; }

        private static bool Hidden()
        {
            return InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible() || Game.IsPaused();
        }

        private void Update()
        {
            if (_cards.Count == 0) return;
            bool hidden = Hidden();
            if (Time.unscaledTime >= _hotCheck)
            {
                _hotCheck = Time.unscaledTime + 0.5f;
                _hotFound = RavenSpeech.HotbarBottomLeft(out _hotAt);
            }
            // under the hotbar (or where it would be): the top left corner of the first card
            float s = Scale;
            Vector2 dockTop = _hotFound ? new Vector2(_hotAt.x, _hotAt.y - 10f * s) : new Vector2(32f * s, Screen.height - 150f * s);
            Camera cam = Utils.GetMainCamera();
            float y = dockTop.y;
            for (int i = 0; i < _cards.Count; i++)
            {
                SpeechCard c = _cards[i];
                if (c.Dead) { Destroy(c.gameObject); _cards.RemoveAt(i); i--; continue; }
                c.Place(cam, hidden, new Vector2(dockTop.x, y), s);
                if (c.Dead) continue;
                if (c.Docked && !c.Ending) y -= (c.Height + 8f) * s;
            }
            Spread(s);
            foreach (SpeechCard c in _cards) if (!c.Dead) c.Move(hidden, s);
            // the newest docked line is bright, the older ones step back
            SpeechCard newest = null;
            foreach (SpeechCard c in _cards) if (c.Docked && !c.Ending) newest = c;
            foreach (SpeechCard c in _cards) c.Older = c.Docked && newest != null && c != newest;
        }
    }

    // one card
    internal sealed class SpeechCard : MonoBehaviour
    {
        internal GameObject Talker;
        internal Vector2 Target;               // where its top left corner should be this frame
        internal bool Over;                    // hanging over its raven (not under the hotbar)
        internal float W, H;                   // its size on the screen this frame
        private Vector3 _sp;
        private bool _wasDocked;
        internal bool IsMunin { get { return _munin; } }
        internal Vector3 Offset;
        internal Func<Transform> Follow;       // a raven that moves on its own (null: hang on the talker)
        internal const float OverRaven = 0.45f; // over a small raven's head
        internal float Range;
        private float _far = 1f;               // the fading with distance (1 near, 0 at its range)
        internal bool Dockable, Docked, Older, Ending, Dead;
        internal string Text;
        internal float Height;
        private float _width;
        private bool _munin;
        private Color _own; // its colour: Hugin blue, Munin red, the Guide amber
        private float _until, _flashAt = -1f, _held;
        private RectTransform _rt, _arrowRt;
        private CanvasGroup _cg;
        private Image _frame, _arrow, _side;
        private Vector2 _pos, _goal;
        private bool _placed;

        private const float SideGlow = 0.22f; // the raven's colour in from the left end (design 5a: faint)

        internal static SpeechCard Make(Transform parent, bool munin, string name, string text)
        {
            GameObject go = new GameObject(munin ? "Munin" : "Hugin", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SpeechCard c = go.AddComponent<SpeechCard>();
            c._munin = munin;
            c.Text = text;
            c._rt = (RectTransform)go.transform;
            c._rt.anchorMin = c._rt.anchorMax = Vector2.zero;
            c._rt.pivot = new Vector2(0f, 1f); // placed by its top left corner
            c._cg = go.AddComponent<CanvasGroup>();
            c._cg.blocksRaycasts = false; c._cg.interactable = false;
            c._cg.alpha = 0f;
            bool guide = StripTags(name) == Lang.T(RavenSpeech.GuideTitle);
            Color frameColor = guide ? RavenSpeech.GuideFrame : munin ? RavenSpeech.MuninFrame : RavenSpeech.HuginFrame;
            c._own = frameColor;

            // a soft shadow under the capsule
            RectTransform shadow = Child(go.transform, "shadow");
            shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one; shadow.offsetMin = new Vector2(-14f, -18f); shadow.offsetMax = new Vector2(14f, 8f);
            Image sh = shadow.gameObject.AddComponent<Image>();
            sh.sprite = RavenSpeech.Glow(); sh.color = new Color(0f, 0f, 0f, 0.35f); sh.raycastTarget = false;

            // the capsule: smoky, see-through (design 5a), the raven's colour faintly in from the left end (cut to its shape)
            RectTransform bg = Child(go.transform, "bg");
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            Image bi = bg.gameObject.AddComponent<Image>();
            bi.sprite = RavenSpeech.Fill(); bi.type = Image.Type.Sliced; bi.color = new Color(0.04f, 0.045f, 0.05f, 0.64f); bi.raycastTarget = false;
            RectTransform side = Child(bg, "side"); // the colour: the capsule's own shape, faded out from the left (BandFill)
            side.anchorMin = Vector2.zero; side.anchorMax = Vector2.one; side.offsetMin = side.offsetMax = Vector2.zero;
            c._side = side.gameObject.AddComponent<Image>();
            c._side.sprite = RavenSpeech.Fill(); c._side.type = Image.Type.Sliced; c._side.color = new Color(frameColor.r, frameColor.g, frameColor.b, SideGlow); c._side.raycastTarget = false;
            BandFill sideFill = side.gameObject.AddComponent<BandFill>();

            RectTransform frame = Child(go.transform, "frame"); // the thin outline
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
            c._frame = frame.gameObject.AddComponent<Image>();
            c._frame.sprite = RavenSpeech.Ring(); c._frame.type = Image.Type.Sliced; c._frame.color = RavenSpeech.Edge; c._frame.raycastTarget = false;

            // the round picture of the raven, in the middle of the left end
            const float pic = 30f, padL = 7f, gap = 9f, padR = 18f, padV = 7f; // a thin bar (design 5a)
            RectTransform pr = Child(go.transform, "pic");
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f); pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(padL, 0f); pr.sizeDelta = new Vector2(pic, pic);
            Image ring = pr.gameObject.AddComponent<Image>();
            ring.sprite = SeneaLShoulders.Disc(); ring.color = frameColor; ring.raycastTarget = false;
            RectTransform cutRt = Child(pr, "cut");
            cutRt.anchorMin = Vector2.zero; cutRt.anchorMax = Vector2.one; cutRt.offsetMin = new Vector2(1.5f, 1.5f); cutRt.offsetMax = new Vector2(-1.5f, -1.5f);
            Image ci = cutRt.gameObject.AddComponent<Image>(); ci.sprite = SeneaLShoulders.Disc(); ci.color = new Color(0.03f, 0.035f, 0.045f, 1f); ci.raycastTarget = false;
            cutRt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform face = Child(cutRt, "face");
            face.anchorMin = Vector2.zero; face.anchorMax = Vector2.one; face.offsetMin = new Vector2(2f, 2f); face.offsetMax = new Vector2(-2f, -2f);
            face.localScale = new Vector3(munin && !guide ? -1f : 1f, 1f, 1f);
            Image fi = face.gameObject.AddComponent<Image>(); fi.sprite = AbilityIcons.For(guide ? "guide" : munin ? "munin" : "hugin", !guide && munin); fi.preserveAspect = true; fi.raycastTarget = false;

            // "Хугін: words" in one run of text, the name bold in the raven's colour
            string shownName = StripTags(name);
            if (shownName.Length == 0) shownName = Plugin.RavenName(munin); // the game gives its own ravens no title
            string full = "<b><color=" + (guide ? RavenSpeech.GuideName : munin ? RavenSpeech.MuninName : RavenSpeech.HuginName) + ">" + shownName + ":</color></b> " + RavenSpeech.MarkNames(text);
            TextMeshProUGUI body = Label(go.transform, "text", RavenSpeech.Font(false), 15.5f, FontStyles.Normal);
            body.text = full;
            body.color = new Color(0.90f, 0.91f, 0.93f, 1f);
            c._body = body; c._full = full; c._tone = guide ? RavenSpeech.GuideName : munin ? RavenSpeech.MuninName : RavenSpeech.HuginName;
            c._head = "<b><color=" + c._tone + ">" + shownName + ":</color></b> ";
            body.lineSpacing = 2f;
            Vector2 pref = body.GetPreferredValues(full, RavenSpeech.MaxWidth, 0f);
            float tw = Mathf.Clamp(pref.x + 2f, 120f, RavenSpeech.MaxWidth);
            float th = body.GetPreferredValues(full, tw, 0f).y;
            c.Height = Mathf.Max(pic + 2f * 6f, th + 2f * padV);
            c._width = padL + pic + gap + tw + padR;
            RectTransform br = body.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f); br.pivot = new Vector2(0f, 0.5f);
            br.anchoredPosition = new Vector2(padL + pic + gap, 0f); br.sizeDelta = new Vector2(tw, th);
            body.alignment = TextAlignmentOptions.MidlineLeft;
            c._rt.sizeDelta = new Vector2(c._width, c.Height);
            sideFill.Set(0f, c.Height + 10f, Mathf.Min(c._width * 0.35f, 170f));

            // the arrow towards the raven, shown only under the hotbar
            c._arrowRt = Child(go.transform, "arrow");
            c._arrowRt.anchorMin = c._arrowRt.anchorMax = new Vector2(1f, 0.5f);
            c._arrowRt.sizeDelta = new Vector2(14f, 9f); // a slim arrow (pointing along its length)
            c._arrowRt.anchoredPosition = new Vector2(14f, 0f);
            c._arrow = c._arrowRt.gameObject.AddComponent<Image>();
            c._arrow.sprite = RavenSpeech.Arrow(); c._arrow.color = frameColor; c._arrow.raycastTarget = false;
            c._arrowRt.gameObject.SetActive(false);
            return c;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // A text with its font given before it wakes: woken without one, TMP looks for its default LiberationSans
        // (absent in Valheim) and writes a warning to the log for every text made.
        internal static TextMeshProUGUI NewText(GameObject go, TMP_FontAsset font)
        {
            bool wake = go.activeSelf && font != null;
            if (wake) go.SetActive(false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (wake) go.SetActive(true);
            return t;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style)
        {
            RectTransform rt = Child(parent, name);
            TextMeshProUGUI t = NewText(rt.gameObject, font);
            t.fontSize = size;
            t.fontStyle = style;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.outlineWidth = 0f; // clean letters on the dark capsule
            return t;
        }

        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
        }

        // the raven itself is no longer there (flown off or vanished): its words go with it
        private Raven _raven;
        private bool _ravenLooked;
        private bool Gone()
        {
            if (!_ravenLooked) { _ravenLooked = true; _raven = Talker != null ? Talker.GetComponent<Raven>() : null; }
            if (_raven == null) return false;
            return _raven.IsAway() || (_raven.m_visual != null && !_raven.m_visual.activeInHierarchy);
        }

        internal void Extend(float seconds) { _until = Mathf.Max(_until, Time.time + seconds); }

        // ---- the voice: three dots while the sound is made, then the words one by one along it
        private TextMeshProUGUI _body;
        private string _full, _head, _tone;
        private bool _typing, _revealed;
        private float _dotAt; private int _dots;

        internal void Typing()
        {
            if (_body == null || _revealed || _typing) return;
            _typing = true; _dots = 0; _dotAt = 0f;
            Extend(RavenVoice.MaxWait + RavenVoice.TurnWait + 3f); // the card never runs out while it waits for its voice
            Dots();
        }

        internal void Reveal(float seconds)
        {
            _typing = false; _revealed = true;
            if (_body == null) return;
            _body.text = _full;
            if (seconds > 0f) WordReveal.Begin(_body, seconds);
        }

        private void Dots()
        {
            _dotAt = Time.time + 0.35f; _dots = _dots % 3 + 1;
            string d = "";
            for (int i = 0; i < 3; i++) d += i < _dots ? "•" : "<alpha=#40>•<alpha=#FF>";
            _body.text = _head + "<color=" + _tone + ">" + d + "</color>";
        }

        private void Update() { if (_typing && _body != null && Time.time >= _dotAt) Dots(); }

        internal void End() { Ending = true; }

        // where it stands this frame: over the talker in view, or under the hotbar
        internal void Place(Camera cam, bool hidden, Vector2 dock, float s)
        {
            if (!Ending && _held < RavenSpeech.HoldMax && (hidden || (!Dockable && !Over))) // not readable now: its time waits
            {
                float dt = Mathf.Min(Time.deltaTime, RavenSpeech.HoldMax - _held);
                _until += dt; _held += dt;
            }
            if (!Ending && (Talker == null || Time.time >= _until || Gone())) End();
            Vector2 target = Vector2.zero;
            bool over = false;
            Vector3 sp = Vector3.zero;
            if (Talker != null && cam != null)
            {
                Transform bird = null;
                if (Follow != null) { try { bird = Follow(); } catch { bird = null; } }
                Vector3 at = bird != null ? bird.position + Vector3.up * OverRaven : Talker.transform.position + Offset;
                sp = cam.WorldToScreenPoint(at);
                Player me = Player.m_localPlayer; // the distance from you (not from the camera, which may be zoomed far out)
                float dist = Vector3.Distance(me != null ? me.transform.position + Vector3.up * 1.6f : cam.transform.position, at);
                float m = 40f * s;
                over = sp.z > 0f && sp.x > m && sp.x < Screen.width - m && sp.y > m && sp.y < Screen.height - m && dist <= Range;
                // further off it fades: clear up to FadeFrom (or a little before its own range); another player's raven is
                // gone at its range, your own only dims to DockAlpha, the brightness it keeps under the hotbar: no jump
                float from = Mathf.Min(RavenSpeech.FadeFrom, Range * 0.7f);
                float far = Mathf.InverseLerp(from, Range, dist);
                _far = Dockable ? Mathf.Lerp(1f, RavenSpeech.DockAlpha, far) : 1f - far;
                // under the hotbar: as bright as it was over the raven, and paler still as the raven is left far behind
                if (!over && Dockable) _far = Mathf.Lerp(RavenSpeech.DockAlpha, RavenSpeech.FarAlpha, Mathf.InverseLerp(RavenSpeech.FarFrom, RavenSpeech.FarFrom + 20f, dist));
                if (over) target = new Vector2(sp.x - _width * 0.5f * s, sp.y + (Height + 6f) * s);
            }
            _wasDocked = Docked;
            _sp = sp;
            Over = over;
            Docked = !over && Dockable && !Ending;
            if (Docked) target = dock;
            else if (!over && _placed) target = _pos; // fading out, or waiting out of view: it stays where it was
            bool visible = !hidden && (over || Docked || Ending);

            // fade in, fade out; the older lines of a talk step back
            float want = Ending ? 0f : !visible ? 0f : (Older ? 0.6f : 1f) * _far;
            float k = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            _cg.alpha = Mathf.Lerp(_cg.alpha, want, Ending ? 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime) : k);
            if (Ending && _cg.alpha < 0.02f) { Dead = true; return; }

            // keep it on the screen
            float size = Dockable ? 1f : 0.85f; // other players' ravens a little smaller: yours come first
            float w = _width * s * size, h = Height * s * size;
            W = w; H = h;
            target.x = Mathf.Clamp(target.x, 4f * s, Screen.width - w - 24f * s);
            target.y = Mathf.Clamp(target.y, h + 4f * s, Screen.height - 4f * s);
            Target = target;
        }

        // to where Place aimed it (pushed apart from the others by the host), with the flash and the arrow
        internal void Move(bool hidden, float s)
        {
            Vector2 target = new Vector2(Target.x, Mathf.Min(Target.y, Screen.height - 4f * s));
            float w = W, h = H;
            Vector3 sp = _sp;
            bool wasDocked = _wasDocked;
            float size = Dockable ? 1f : 0.85f;
            // first shown, hidden, or coming back from nothing: straight there, no glide across the screen
            // small wobbles of the target are let be (it only moves on when the target has really gone somewhere),
            // and it stands on whole pixels: steady, no shiver
            if (!_placed || hidden || (_cg.alpha < 0.05f && !Ending)) { _pos = target; _goal = target; _placed = true; }
            else
            {
                if ((target - _goal).sqrMagnitude > 36f * s * s) _goal = target;
                _pos = Vector2.Lerp(_pos, _goal, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            }
            _rt.position = new Vector3(Mathf.Round(_pos.x), Mathf.Round(_pos.y), 0f);

            // landing under the hotbar for the first time: the frame flashes twice, so it is seen
            if (Docked && !wasDocked && _flashAt < 0f && !hidden) _flashAt = Time.unscaledTime;
            Color own = _own;
            Color fc = RavenSpeech.Edge;
            float glow = SideGlow, scale = 1f;
            if (_flashAt >= 0f)
            {
                float t = Time.unscaledTime - _flashAt;
                if (t < 1.2f)
                {
                    float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI / 0.6f));
                    fc = Color.Lerp(fc, own, pulse);
                    glow = Mathf.Lerp(SideGlow, 0.55f, pulse);
                    scale = 1f + pulse * 0.03f;
                }
            }
            _frame.color = fc;
            _side.color = new Color(own.r, own.g, own.b, glow);
            _rt.localScale = new Vector3(scale * size, scale * size, 1f);

            // the arrow towards the raven
            bool arrow = Docked && Talker != null && _sp != Vector3.zero && Talker != (Player.m_localPlayer != null ? Player.m_localPlayer.gameObject : null);
            if (_arrowRt.gameObject.activeSelf != arrow) _arrowRt.gameObject.SetActive(arrow);
            if (arrow)
            {
                Vector2 from = new Vector2(_pos.x + w, _pos.y - h * 0.5f);
                Vector2 to = new Vector2(sp.x, sp.y);
                if (sp.z < 0f) to = new Vector2(Screen.width - sp.x, Screen.height - sp.y); // behind: the other way
                Vector2 dir = to - from;
                float ang = dir.sqrMagnitude > 1f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
                _arrowRt.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
        }
    }

    // A raven of the game stays while its words are shown (it asks Chat.IsDialogVisible before it leaves). Our card
    // stands in for those words, so it counts as shown: the raven waits until it is read and then flies off its own
    // way, instead of vanishing in a puff of feathers the moment you step away and leaving the card behind.
    [HarmonyPatch(typeof(Chat), "IsDialogVisible")]
    internal static class RavenSpeechVisiblePatch
    {
        private static void Postfix(GameObject talker, ref bool __result)
        {
            if (!__result && RavenSpeech.Has(talker)) __result = true;
        }
    }

    // the game forgets a raven's words of its own: ours go with the card
    [HarmonyPatch(typeof(Chat), "ClearNpcText", new System.Type[] { typeof(GameObject) })] // two overloads: the one by the talker
    internal static class RavenSpeechClearPatch
    {
        private static void Prefix(GameObject talker) { RavenSpeech.Clear(talker); }
    }
}

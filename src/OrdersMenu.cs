#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Накази» on their key (J) and «Дари» on theirs (Q): the ravens' own round menu with two faces, drawn by the mod
    // (not the game's radial), both on one template (the user's design «10c» and its rune-ring buttons, 2026-10-20):
    // a dark disc in the middle, each raven a wing in his colour a little apart from it (Munin left, Hugin right, every
    // piece of one mirrored on the other), round buttons in two rows on the wings. One raven: his wing a fan over the
    // middle, the other's buttons not there at all.
    // The gifts: each raven's open gifts (a golden rune in a gift: a golden ring, glowing more with each); his strength
    // a half-ring filling like a bowl round «Накази», the round button in the very middle; under it the gift under the
    // pointer.
    // The orders: Облетіти, Поруч, Кружляй, Жердина, Туди and the pouch on each wing, in gold rune-ring frames; in the
    // middle «Усіх назад» (lit while a raven is away), each raven's strength round it and his satiety outside that,
    // its stroke starting at his food button (a small popup with the food he eats, from your bag); under the middle
    // «Трюки», which drops the tricks below the circle (and «Догонялки» for both). A chip over each wing says where the
    // raven is; an order going on lights up with a ring of the time it has left, a raven on a perch fills its button.
    // Over the circle: a line about the piece under the pointer. Right click a gift, an order, the perch or a trick:
    // give it its own key (the right button read straight from the input: IMGUI gives it no plain mouse-up on Windows).
    // Held key: point and let go to pick; a short tap leaves it open for a click; right click, Esc or a click outside
    // closes. Everything is plain 2D: the frames and icons are pictures (tools/menu_icons.html), the plates' shapes
    // are made here once per screen size.
    internal class OrdersMenu : MonoBehaviour
    {
        private static OrdersMenu _inst;
        private const float HoldSeconds = 0.3f; // held longer than this: letting go picks the piece under the pointer
        internal static bool IsOpen { get { return _inst != null && _inst._open; } }
        internal static bool Capturing { get { return IsOpen && _inst._bindId != null; } }
        private static int _boundFrame = -10;
        internal static bool JustBound { get { return Time.frameCount <= _boundFrame + 1; } }

        private bool _open;
        private KeyCode _key = KeyCode.None;
        private float _heldFor, _built = -1f;
        private int _hover = -1;           // the piece under the pointer (index in _pieces), -1 nothing
        private bool _downIn;              // a mouse button went down while the menu was open (its click is ours)
        private string _bindId, _bindName; // right-clicked trick waiting for its key
        private int _feedFor = -1;         // the feeding popup: 0 Hugin, 1 Munin, -1 shut
        private float _feedScroll;
        private int _feedHover = -1;

        // ---------------------------------------------------------------- the pieces

        private enum Kind { Order, Pouch, Trick, Chase, Recall, Feed, Gift, Power, ToOrders, Here, Toggle, Bind, BindFire, Arm }

        private sealed class Piece
        {
            internal Kind Kind;
            internal bool Munin;
            internal int Order;
            internal float A0, A1, R0, R1;   // degrees clockwise from the top, radii in reference pixels
            internal Shape Shape;             // its baked masks (ShapeFor), for the screen's scale ShapeS
            internal float ShapeS = -1f;
            internal string Title = "", Sub = "", Desc = "";
            internal bool Usable = true, Now, Glow, Stays;
            internal bool Hidden;             // not there now (the arm's row below its trust): not drawn, not hit
            internal bool Sits;               // the raven sits where this button sent it (a perch, a spot): filled with his colour
            internal bool Moves;              // ...and a click moves him to where you look (not back to you)
            internal bool Play;               // the perch's button turned «Гратися» (your tamed beast in sight)
            internal bool Wash;               // ...or «Помити» (you stand in the water)
            internal string Icon;             // its picture when it depends on the place (null: by its kind)
            internal int Seq;                 // a trick: its turn flying out of «Трюки»
            internal float CoolLeft, CoolAll;
            internal string KeyId, KeyLabel; // a trick's own key and its label ("—" none), read four times a second, not every frame
            internal string Info, InfoDesc, Tip; // the line under the circle and the tooltip, made when first shown
            internal string MidName, MidSub;     // a gift's words under the middle, made when first shown
            internal string Label, CoolText;     // what is written on it, and its wait ("2:40"), made in Build
            internal float Strength;             // a strength half-ring: the bowl's fill, 0..1
            internal RavenAbility Gift;          // a gift's piece: which one
            internal int Gold;                   // a gift's golden runes (0–3): its edge gold, glowing more with each
            internal float CR;                   // a round button (the gifts): its radius; its centre at angle A0, distance R0
            internal Action Act;
        }
        private readonly List<Piece> _pieces = new List<Piece>();
        private int _mode = -1;  // 0 both, 1 Hugin alone, 2 Munin alone (the layout); -2 none on you
        private bool _gifts;     // which face: the gifts (Q) or the orders (J)
        private string _layoutSig; // set once laid out (null: lay out anew); the face, the layout and the open gifts it was made for:
        private int _sigMode = -99; private bool _sigGifts; private string _sigIds;
        private float _sigAt = -1f;
        private string _giftIds = "";
        private readonly List<RavenAbility> _gH = new List<RavenAbility>(), _gM = new List<RavenAbility>();
        private bool _fromRadial; // opened from the game's radial (G): waits until that one has gone
        private float _radialAt;
        private int _rightFrame = -10;

        // what the ravens are doing, worked out with the pieces (four times a second), drawn every frame: [0] Hugin, [1] Munin
        private readonly string[] _state = new string[2], _chip = new string[2];
        private readonly int[] _stateIcon = new int[2], _zone = new int[2];
        private readonly float[] _sat = new float[2], _satShown = { -1f, -1f }; // the satiety, and the bar's level moving after it
        private readonly bool[] _far = new bool[2];             // off the shoulder, and we know where: a pointer to it on the disc's edge
        private readonly Vector3[] _farAt = new Vector3[2];

        // ---------------------------------------------------------------- opening

        // its key again closes it; the other face's key turns it to that face
        internal static void OpenByKey(KeyCode key, bool gifts)
        {
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_OrdersMenu");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<OrdersMenu>();
            }
            Player me = Player.m_localPlayer;
            if (!_inst._open && me != null && RavenSeat.On(me, false) == null && RavenSeat.On(me, true) == null)
            {
                // no raven on you: no circle and no cursor, only the line at the top for a few seconds
                _inst._noticeUntil = Time.unscaledTime + NoticeSeconds; _inst._noticeGifts = gifts;
                _inst.enabled = true;
                return;
            }
            if (_inst._open)
            {
                if (_inst._gifts == gifts) { _inst.Close(); return; }
                _inst.Face(gifts); _inst._key = key; _inst._heldFor = 0f;
                return;
            }
            _inst.enabled = true;
            _inst._open = true; _inst._key = key; _inst._heldFor = 0f; _inst._downIn = false; _inst._fromRadial = false;
            _inst.Face(gifts);
        }

        // the book's key with the circle open: the circle gives way to the book
        internal static void CloseNow() { if (IsOpen) _inst.Close(); }

        // from G → Ворони: open (as after a short tap) once the game's radial has closed
        internal static void OpenFromRadial(bool gifts)
        {
            if (IsOpen) _inst.Close();
            OpenByKey(KeyCode.None, gifts);
            _inst._fromRadial = true; _inst._radialAt = Time.unscaledTime;
        }

        private void Face(bool gifts)
        {
            _gifts = gifts; _layoutSig = null; _sigAt = -1f;
            _built = -1f; _hover = -1; _bindId = null; _feedFor = -1; _tricksOpen = false;
            _satShown[0] = _satShown[1] = -1f;
        }

        // closed: the game still sees a window for a frame (as its own windows do) and while the click that closed it
        // is held, so neither Esc nor that click reaches the game (the main menu, a swing)
        internal static int ClosedFrame = -10;
        internal static bool ClickHeld;
        // a clear veil over the whole screen while the circle is up: the game's and other mods' own windows under it
        // (Path of Valheiman's counters with their long hover texts) get no pointer. The circle is drawn by IMGUI, which
        // stops nothing on its own; the circle's plates lie above the veil and take no pointer either
        private static GameObject _veil;
        private static void Veil(bool on)
        {
            if (_veil == null)
            {
                if (!on) return;
                _veil = new GameObject("RavenWatch_CircleVeil", typeof(RectTransform));
                DontDestroyOnLoad(_veil);
                Canvas c = _veil.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 30000;
                _veil.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                UnityEngine.UI.Image img = _veil.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;
            }
            if (_veil.activeSelf != on) _veil.SetActive(on);
        }

        private void Close() { Veil(false); _open = false; _key = KeyCode.None; _bindId = null; _feedFor = -1; _pick = false; _pickTarget = null; ClosedFrame = Time.frameCount; ClickHeld = MouseHeld(); enabled = false; } // closed: no OnGUI every frame

        private void Awake() { useGUILayout = false; } // drawn by hand: no layout pass
        private void OnDisable() { Veil(false); } // (shut by any way at all: the veil never stays over the screen)

        // a mouse button held, asked both ways (the game's own input and Unity's old one: either may be the live one)
        private static bool MouseHeld()
        {
            if (RavenKeys.Held(KeyCode.Mouse0) || RavenKeys.Held(KeyCode.Mouse1)) return true;
            try { return Input.GetMouseButton(0) || Input.GetMouseButton(1); } catch { return false; } // the old input may be switched off
        }
        internal static bool Shields
        {
            get
            {
                if (IsOpen || Time.frameCount <= ClosedFrame + 1) return true;
                if (ClickHeld && !MouseHeld()) ClickHeld = false;
                return ClickHeld;
            }
        }

        private static KeyCode[] _captureKeys;

        internal static float NoticeSeconds = 5f;
        private float _noticeUntil;
        private bool _noticeGifts;

        // «no raven on you», over where the circle would be, while it lasts (the plate hides itself once not placed)
        private void Notice()
        {
            if (Time.unscaledTime >= _noticeUntil || Player.m_localPlayer == null || InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || RavenBook.IsOpen) { _noticeUntil = 0f; enabled = false; return; }
            float s = ScaleNow;
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f - 20f * s);
            InfoBar(c, s, Lang.T("<b>Жодного ворона на плечі</b>"), Lang.F("Посади ворона на плече (правий клік по ньому в сумці), щоб {0}", (_noticeGifts ? Lang.T("кликати його дари.") : Lang.T("давати накази."))));
        }

        private void Update()
        {
            if (!_open) { Notice(); return; }
            _noticeUntil = 0f;
            Veil(!_fromRadial);
            Player p = Player.m_localPlayer;
            if (_fromRadial) { if (Hud.InRadial() && Time.unscaledTime - _radialAt < 2f) return; _fromRadial = false; }
            if (p == null || p.IsDead() || RavenBook.IsOpen || Hud.InRadial() || InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || global::Console.IsVisible()) { Close(); return; }
            if (_bindId != null) { Capture(); return; }
            if (RavenKeys.Down(KeyCode.Escape) || OldEsc()) { if (_pick) { _pick = false; _pickTarget = null; } else if (_feedFor >= 0) _feedFor = -1; else if (_tricksOpen) { _tricksOpen = false; _built = -1f; } else Close(); return; }
            if (RightDown() && Time.frameCount > _rightFrame + 1) { _rightFrame = Time.frameCount; RightClick(); if (!_open || _bindId != null) return; }
            // how long the key is held, counted frame by frame with each frame at most 0.05 s: a slow frame (the first
            // opening, the game saving) must not turn a short tap into "held and let go over nothing" (that closed it)
            if (_key != KeyCode.None && RavenKeys.Held(_key)) _heldFor += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_key != KeyCode.None && !RavenKeys.Held(_key))
            {
                bool held = _heldFor >= HoldSeconds;
                _key = KeyCode.None;
                if (held) { if (_hover >= 0) Pick(_hover); else Close(); } // let go over a piece: that one
            }
        }

        // a trick's own key: the next key pressed (with its modifiers); Esc leaves it, Backspace clears it
        private void Capture()
        {
            if (RavenKeys.Down(KeyCode.Escape)) { _bindId = null; return; }
            if (RavenKeys.Down(KeyCode.Backspace) || RavenKeys.Down(KeyCode.Delete)) { RavenKeys.Set(_bindId, BepInEx.Configuration.KeyboardShortcut.Empty); _bindId = null; _boundFrame = Time.frameCount; return; }
            if (_captureKeys == null) _captureKeys = RavenBook.CaptureKeys();
            foreach (KeyCode k in _captureKeys)
            {
                if (!RavenKeys.Down(k)) continue;
                if (k == _key || k == RavenKeys.Get(RavenKeys.OrdersId).MainKey || k == RavenKeys.Get(RavenKeys.GiftsId).MainKey) continue; // the menu's own keys (still held, or pressed to close)
                RavenKeys.Set(_bindId, new BepInEx.Configuration.KeyboardShortcut(k, RavenKeys.HeldModifiers(k)));
                _boundFrame = Time.frameCount; // this very press must not also give the trick
                _bindId = null;
                return;
            }
        }

        private static bool OldEsc() { try { return Input.GetKeyDown(KeyCode.Escape); } catch { return false; } }

        // the right button went down, asked both ways (the game's input and Unity's old one)
        private static bool RightDown()
        {
            if (RavenKeys.Down(KeyCode.Mouse1)) return true;
            try { return Input.GetMouseButtonDown(1); } catch { return false; }
        }

        // a right click: on a piece that can have a key, wait for that key; on another piece nothing; beside the pieces
        // it closes (the food popup first)
        private void RightClick()
        {
            if (_feedFor >= 0) { _feedFor = -1; return; }
            int hit = _hover;
            if (hit >= 0 && hit < _pieces.Count)
            {
                Piece pc = _pieces[hit];
                if (pc.KeyId == null) return;
                _bindId = pc.KeyId;
                _bindName = (pc.Kind == Kind.Trick || pc.Kind == Kind.Order || pc.Kind == Kind.Here || pc.Kind == Kind.Gift ? Plugin.RavenName(pc.Munin) + ": " : "") + (pc.Kind == Kind.Here ? Lang.T("Туди") : pc.Title); // («Туди» changes its title with what you look at)
                _built = -1f;
                return;
            }
            Close();
        }

        private void LateUpdate()
        {
            if (!_open) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------------------------------------------------------------- the layout (reference pixels, 1080 p)

        // Both faces are one template (the user's design «10c», 2026-10-20), so J and Q are the same size: a dark disc in
        // the middle, each raven a wing in his colour a little apart from it (the game shows between), round buttons in
        // two rows on the wings. One raven: his wing a fan over the middle, the other's buttons not there at all.
        // The gifts: by the middle the everyday ones (the strike first, at the top, a little smaller: it is mostly used by
        // its key), out from them the big ones; «Накази» in the very middle, each raven's strength a half-ring round it.
        // The orders: the orders on the wings; «Усіх назад» in the middle (lit while a raven is away); each raven's
        // strength round it and his satiety outside that, its stroke starting at his food button (Munin meat, Hugin
        // berries); under the middle «Трюки», which drops the tricks under the circle (two rows of five each, and
        // «Догонялки» for both), flying out of it one by one. A chip over each wing says where the raven is; the order
        // going on lights up (a ring of the time it has left), a raven on a perch fills its button with his colour.
        // Everything is GS times the design's pixels; every button keeps 6–8 px from the edges of the plates round it.
        private const float GS = 1.1f;
        private const float DiscR = 114f * GS, Centre = DiscR;                                 // the middle disc
        private const float WingIn = 136f * GS, WingOut = 314f * GS, Wing0 = 35f, Wing1 = 140f; // a wing: radii, angles from the top (Hugin's; Munin's mirrored)
        private const float FanA = 82f, FanIn = 60f, FanOuter = 70f;                            // one raven: the fan's half-width, each gift row's half-span
        private const float GRim1 = 320f * GS;                                                  // beyond this a click closes
        private const float PowerIn = 54f * GS, PowerOut = 68f * GS, PowerA0 = 20f, PowerA1 = 155f, OrdersBtn = 40f * GS; // the strength half-rings and the middle button
        private static readonly float[] RowR = { 178.5f * GS, 263.5f * GS };                  // the two rows' radii (far enough apart that no two buttons touch)
        private static readonly float[] OrdD = { 68f, 84f };                                    // the orders' buttons in each row (before GS): the gifts' sizes
        private const float SatIn = 84.5f * GS, SatOut = 91.5f * GS, FoodR = 88f * GS, FoodD = 38f; // the satiety stroke, the food button on its end
        private const float ChipR = 330f * GS;                                                 // the chips with where the ravens are
        // where each gift sits: its row (0 by the middle, 1 out), angle from the top (Hugin's side; Munin's mirrored) and
        // button size (reference px before GS). Fixed places, so a gift opening later never moves the others; each row
        // spans the wing with ~6 px between buttons and from the wing's edges.
        private static readonly Dictionary<string, Vector3> GiftPlace = new Dictionary<string, Vector3>
        {
            { "hstrike", new Vector3(0, 46.5f, 54f) }, { "thought", new Vector3(0, 69f, 68f) }, { "scout", new Vector3(0, 94.5f, 68f) }, { "light", new Vector3(0, 120f, 68f) },
            { "leap", new Vector3(1, 46.5f, 84f) }, { "shadow", new Vector3(1, 67f, 84f) }, { "dome", new Vector3(1, 87.5f, 84f) }, { "hdecoy", new Vector3(1, 108f, 84f) }, { "hflight", new Vector3(1, 128.5f, 84f) },
            { "mstrike", new Vector3(0, 46.5f, 54f) }, { "mthought", new Vector3(0, 69f, 68f) }, { "scream", new Vector3(0, 94.5f, 68f) }, { "fury", new Vector3(0, 120f, 68f) },
            { "wings", new Vector3(1, 46.5f, 84f) }, { "heal", new Vector3(1, 67f, 84f) }, { "ward", new Vector3(1, 87.5f, 84f) }, { "mtame", new Vector3(1, 108f, 84f) }, { "mberserk", new Vector3(1, 128.5f, 84f) },
            // the paired ones (both ravens): the outer row at the very top, between the wings (the user, 2026-10-24)
            // (their angle here is their order only: the open ones stand centred at the top, no gap left for one not open yet — Paired, the user 2026-11-11)
            { "pload", new Vector3(1, -18f, 76f) }, { "pclear", new Vector3(1, 0f, 76f) }, { "pwhirl", new Vector3(1, 18f, 76f) },
        };
        private static bool _g; // the face drawn now is the gifts' one
        private float RimNow { get { return !_g && _tricksOpen ? 450f * GS : !_g && (_armShown[0] || _armShown[1]) ? (_armMore ? ArmQ1 : ArmR1) + 4f : GRim1; } } // (the tricks dropped: their band is ours too; so are the arm's rows)

        // the orders on a wing: Облетіти, Поруч, Кружляй, Жердина, Туди, Торба — each its angle (Hugin's side; Munin's the
        // mirror) and row (0 by the middle, 1 out), the rows staggered so the wing is filled evenly
        private static readonly float[] TwoAt = { 56f, 86f, 116f, 46.5f, 88f, 128f }, OneAt = { 0f, -54f, 0f, 54f, 62f, -62f };
        private static readonly int[] TwoRow = { 0, 0, 0, 1, 1, 1 }, OneRow = { 1, 0, 0, 0, 1, 1 };
        // the tricks' band under the circle: two ravens — each his part of it, one — the band in the middle
        private const float TwoBand0 = 322f * GS, TwoBand1 = 436f * GS, TwoBandA0 = 130f, TwoBandA1 = 174f, TrickA0 = 136f, TrickStep = 8.2f, TrickR0 = 352f, TrickR1 = 404f, OneBand0 = 190f * GS, OneBand1 = 322f * GS;
        private const float ToggleTwo = 232f * GS, ToggleOne = 150f * GS, ToggleD = 44f, ChaseTwo = 378f * GS, ChaseOne = 375f * GS, ChaseD = 50f;
        private bool _tricksOpen;
        private float _tricksAt;
        // «На руку» (ArmPerch): each raven's row of small buttons on a thin band just outside his wing (one raven: on his
        // side, under the fan), shown from trust 5: onto the arm or back, a stroke, food, eye to eye, a toss
        private const float ArmR0 = 321f * GS, ArmR1 = 363f * GS, ArmR = 342f * GS, ArmD = 32f, ArmStep = 9f, ArmTwoA0 = 68f, ArmOneA0 = 100f, ArmPad = 6f;
        private const float ArmQ0 = 367f * GS, ArmQ1 = 409f * GS, ArmQ = 388f * GS, ArmQStep = 8f; // its own tricks' row, out beyond the arm's (ArmTricks)
        private readonly bool[] _armShown = new bool[2];
        private static bool _armMore;                         // its own tricks' row dropped («Звички»)
        private static readonly int[] _armOpen = new int[2];  // how many tricks each raven has open (its row's length)
        private static readonly int[] _armRowN = { 1, 1 };    // the arm row's buttons shown (1: «На руку» alone, it is not on the arm)
        private static readonly int[] ArmRowOrders = { ArmPerch.ActToggle, ArmPerch.ActFeed, ArmPerch.ActToss, ArmPerch.ActMore };

        private void Layout(Player p)
        {
            bool h = RavenSeat.On(p, false) != null, m = RavenSeat.On(p, true) != null;
            int mode = h && m ? 0 : h ? 1 : m ? 2 : -2;
            if (_gifts && Time.unscaledTime - _sigAt >= 0.25f) // which gifts are open: a few times a second, not every frame
            {
                _sigAt = Time.unscaledTime;
                GiftsOf(p, false, _gH); GiftsOf(p, true, _gM);
                string ids = Ids(_gH) + "/" + Ids(_gM) + "/" + (BindRun.Power == null ? "" : BindRun.Power.Mode == BindMode.Button ? "bb" : "b");
                if (ids != _giftIds) _giftIds = ids; // (a new string only when they changed: the layout looks at the reference)
            }
            if (_layoutSig != null && mode == _sigMode && _gifts == _sigGifts && (!_gifts || ReferenceEquals(_giftIds, _sigIds))) return;
            _sigMode = mode; _sigGifts = _gifts; _sigIds = _giftIds;
            _layoutSig = "set";
            _mode = mode;
            _hover = -1;
            if (_gifts) GiftPieces(mode, _gH, _gM, _pieces); else Pieces(mode, _pieces);
            _built = -1f; // the new pieces say what they are at once
        }

        // a raven's gifts for the circle: the open ones with a button (each has its own place: GiftPlace)
        private static void GiftsOf(Player p, bool munin, List<RavenAbility> into)
        {
            into.Clear();
            if (p == null || RavenSeat.On(p, munin) == null) return;
            bool both = RavenSeat.On(p, !munin) != null; // (a paired gift: only with both on the shoulders)
            foreach (RavenAbility a in RavenAbilities.All)
                if (a.Munin == munin && (!a.Pair || both) && !a.Passive && !RavenAbilities.Stub(a) && RavenAbilities.Shown(a)) into.Add(a);
        }

        private static string Ids(List<RavenAbility> list)
        {
            string s = "";
            foreach (RavenAbility a in list) s += a.Id + ",";
            return s;
        }

        // the gifts' face: each raven's wing of gifts and his strength half-ring, «Накази» in the middle
        private static void GiftPieces(int mode, List<RavenAbility> h, List<RavenAbility> m, List<Piece> into)
        {
            into.Clear();
            if (mode == -2) return;
            if (mode == 0)
            {
                Wing(into, false, h); Add(into, Kind.Power, false, 0, PowerA0, PowerA1, PowerIn, PowerOut, 1f);
                Wing(into, true, m); Add(into, Kind.Power, true, 0, PowerA0, PowerA1, PowerIn, PowerOut, -1f);
                Paired(into, h, m);
            }
            else
            {
                // one raven (the user's reference): his wing a half-circle over the middle, the gifts in the same two
                // rows (each spread even over its arc), his strength a whole ring round the button
                bool mm = mode == 2;
                Fan(into, mm, mm ? m : h);
                Add(into, Kind.Power, mm, 0, -178f, 178f, PowerIn, PowerOut, 1f);
            }
            Add(into, Kind.ToOrders, false, 0, -180f, 180f, 0f, OrdersBtn, 1f);
            // the в'язь under the middle: its switch with the charges; a button-в'язь (only Бальдр) its own press as a
            // medallion in the free lower half under the switch (the top between the wings holds the paired gifts; inside
            // the rim: past it a click closes, and the info bar lies there)
            if (BindRun.Power != null)
            {
                Round(into, Kind.Bind, false, 0, 180f, BindR, BindD);
                if (BindRun.Power.Mode == BindMode.Button) Round(into, Kind.BindFire, false, 0, 180f, RowR[0], BindFireD);
            }
        }

        private const float BindR = 96f, BindD = 34f, BindFireD = 84f;

        private static void Fan(List<Piece> into, bool munin, List<RavenAbility> gifts)
        {
            List<RavenAbility> list = new List<RavenAbility>();
            foreach (RavenAbility a in gifts) if (GiftPlace.ContainsKey(a.Id)) list.Add(a);
            list.Sort((x, y) => { Vector3 px = GiftPlace[x.Id], py = GiftPlace[y.Id]; return px.x != py.x ? px.x.CompareTo(py.x) : px.y.CompareTo(py.y); });
            int inner = 0;
            foreach (RavenAbility a in list) if (GiftPlace[a.Id].x == 0f) inner++;
            int outer = list.Count - inner;
            for (int i = 0; i < list.Count; i++)
            {
                bool first = i < inner;
                int n = first ? inner : outer, j = first ? i : i - inner;
                float span = first ? FanIn : FanOuter;
                float ang = n == 1 ? 0f : -span + 2f * span * j / (n - 1);
                float r = RowR[first ? 0 : 1];
                Piece pc = Add(into, Kind.Gift, munin, 0, ang, ang, r, r, 1f);
                pc.CR = GiftPlace[list[i].Id].z * 0.5f * GS;
                pc.Gift = list[i];
            }
        }

        // the paired gifts (both ravens) at the top between the wings: the open ones centred, PairStep apart — one in the very
        // middle, two either side of it — in the order of their GiftPlace angles
        private const float PairStep = 18f;
        private static void Paired(List<Piece> into, List<RavenAbility> h, List<RavenAbility> m)
        {
            List<RavenAbility> list = new List<RavenAbility>();
            foreach (List<RavenAbility> l in new[] { h, m }) foreach (RavenAbility a in l) if (a.Pair && GiftPlace.ContainsKey(a.Id)) list.Add(a);
            list.Sort((x, y) => GiftPlace[x.Id].y.CompareTo(GiftPlace[y.Id].y));
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 at = GiftPlace[list[i].Id];
                float ang = (i - (list.Count - 1) * 0.5f) * PairStep;
                Piece pc = Add(into, Kind.Gift, list[i].Munin, 0, ang, ang, RowR[(int)at.x], RowR[(int)at.x], 1f);
                pc.CR = at.z * 0.5f * GS;
                pc.Gift = list[i];
            }
        }

        // a raven's own gifts as round buttons in his wing, each at its fixed place (GiftPlace; the paired ones: Paired)
        private static void Wing(List<Piece> into, bool munin, List<RavenAbility> gifts)
        {
            float side = munin ? -1f : 1f;
            foreach (RavenAbility a in gifts)
            {
                Vector3 at;
                if (a.Pair || !GiftPlace.TryGetValue(a.Id, out at)) continue; // (a gift with no place yet: not on the circle)
                int row = (int)at.x;
                Piece pc = Add(into, Kind.Gift, munin, 0, at.y, at.y, RowR[row], RowR[row], side); // (Add mirrors Munin's)
                pc.CR = at.z * 0.5f * GS;
                pc.Gift = a;
            }
        }

        // the orders' pieces of a layout (also for making their shapes ahead, Warm)
        private static void Pieces(int mode, List<Piece> into)
        {
            into.Clear();
            if (mode == -2) return;
            bool one = mode != 0;
            if (!one) { Half(into, false, 1f); Half(into, true, -1f); ArmRow(into, false, false); ArmRow(into, true, false); }
            else { Half(into, mode == 2, 0f); ArmRow(into, mode == 2, true); }
            Round(into, Kind.Chase, false, Orders.Chase, 180f, one ? ChaseOne : ChaseTwo, ChaseD); // two ravens: in the gap between their bands; one: just under its band (with another player's raven)
            Round(into, Kind.Toggle, false, 0, 180f, one ? ToggleOne : ToggleTwo, ToggleD);
            Round(into, Kind.Recall, false, 0, 0f, 0f, OrdersBtn * 2f / GS);
        }

        // the arm's five small buttons (Munin's on the left, as his arm)
        private static void ArmRow(List<Piece> into, bool munin, bool one)
        {
            for (int i = 0; i < ArmRowOrders.Length; i++) Round(into, Kind.Arm, munin, ArmRowOrders[i], (one ? ArmOneA0 : ArmTwoA0) + ArmStep * i, ArmR, ArmD, munin ? -1f : 1f);
            for (int i = 0; i < ArmTricks.Slots; i++) Round(into, Kind.Arm, munin, ArmPerch.ActTrick + i, (one ? ArmOneA0 : ArmTwoA0) + 0.5f + ArmQStep * i, ArmQ, ArmD, munin ? -1f : 1f); // (its own tricks, out beyond)
        }

        // a round button: its centre at an angle (Hugin's side) and a radius, its size before GS
        private static Piece Round(List<Piece> into, Kind k, bool munin, int order, float ang, float r, float d, float side = 1f)
        {
            Piece pc = Add(into, k, munin, order, ang, ang, r, r, side);
            pc.CR = d * 0.5f * GS;
            return pc;
        }

        // one raven's orders, tricks, strength and food: side +1 Hugin's half, −1 Munin's, 0 the one raven (the whole circle)
        private static void Half(List<Piece> into, bool munin, float side)
        {
            bool one = side == 0f;
            float sd = one ? 1f : side;
            float[] at = one ? OneAt : TwoAt;
            int[] row = one ? OneRow : TwoRow;
            for (int i = 0; i < 6; i++)
            {
                Kind k = i == 4 ? Kind.Here : i == 5 ? Kind.Pouch : Kind.Order;
                int order = i == 0 ? Orders.Scout : i == 1 ? Orders.Follow : i == 2 ? Orders.Circle : i == 3 ? -1 : 0; // (-1: the perch)
                Round(into, k, munin, order, at[i], RowR[row[i]], OrdD[row[i]], sd);
            }
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 5; i++)
                {
                    float a = one ? 156f + 12f * i : TrickA0 + TrickStep * i;
                    float rad = (one ? (r == 0 ? 224f : 290f) : (r == 0 ? TrickR0 : TrickR1)) * GS;
                    float d = one ? (r == 0 ? 44f : 52f) : (r == 0 ? 46f : 50f);
                    Round(into, Kind.Trick, munin, Orders.Tricks[r * 5 + i], a, rad, d, sd).Seq = r * 5 + i;
                }
            if (one) Add(into, Kind.Power, munin, 0, -158f, 158f, PowerIn, PowerOut, 1f); // (open at the bottom: the food button)
            else Add(into, Kind.Power, munin, 0, PowerA0, PowerA1, PowerIn, PowerOut, sd);
            Round(into, Kind.Feed, munin, 0, one ? 180f : 165f, FoodR, FoodD, sd);
        }

        private static Piece Add(List<Piece> into, Kind k, bool munin, int order, float a0, float a1, float r0, float r1, float side)
        {
            Piece pc = new Piece { Kind = k, Munin = munin, Order = order, R0 = r0, R1 = r1 };
            if (side > 0f) { pc.A0 = a0; pc.A1 = a1; } else { pc.A0 = -a1; pc.A1 = -a0; } // the mirror
            into.Add(pc);
            return pc;
        }

        // ---------------------------------------------------------------- what each piece says (four times a second)

        private void Build(Player p)
        {
            if (Time.unscaledTime - _built < 0.25f) return;
            _built = Time.unscaledTime;
            _armShown[0] = _armShown[1] = false;
            foreach (Piece pc in _pieces)
            {
                pc.Usable = true; pc.Now = pc.Glow = pc.Stays = pc.Sits = pc.Moves = pc.Hidden = false; pc.CoolLeft = 0f; pc.Info = pc.MidName = null; // (the action stays: made once)
                bool mm = pc.Munin;
                ItemDrop.ItemData item = RavenSeat.On(p, mm);
                string name = Plugin.RavenName(mm);
                switch (pc.Kind)
                {
                    case Kind.Order:
                        if (pc.Order < 0) PerchPiece(p, pc, item);
                        else OrderPiece(p, pc, pc.Order);
                        break;
                    case Kind.Here:
                        HerePiece(p, pc, item);
                        break;
                    case Kind.Trick:
                        OrderPiece(p, pc, pc.Order);
                        pc.KeyId = RavenKeys.TrickId(mm, pc.Order);
                        pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId);
                        break;
                    case Kind.Chase:
                        ChasePiece(p, pc);
                        break;
                    case Kind.Arm:
                        ArmPiece(p, pc, item);
                        if (!pc.Hidden) _armShown[mm ? 1 : 0] = true;
                        break;
                    case Kind.Gift:
                    {
                        RavenAbility a = pc.Gift;
                        pc.Title = GiftTitle(a); pc.Sub = RavenSkills.Status(a); pc.Desc = a.Short;
                        pc.Usable = RavenSkills.UsableNow(a);
                        pc.Now = RavenSkills.Ends(a); // going on: pressed again, it ends
                        pc.CoolLeft = RavenSkills.LeftOf(a.Id); pc.CoolAll = Mathf.Max(1f, RavenSkills.CooldownSeconds(a.Id));
                        pc.KeyId = a.Id;
                        pc.KeyLabel = RavenKeys.LabelOf(a.Id);
                        pc.Gold = Mathf.Clamp(GiftUp.LevelIn(item, a.Id), 0, GiftUp.MaxLevel);
                        if (pc.Act == null) pc.Act = () => RavenSkills.Use(a);
                        break;
                    }
                    case Kind.Power:
                        pc.Title = Lang.F("Сила {0}", (mm ? Lang.T("Муніна") : Lang.T("Хугіна")));
                        pc.Sub = item == null ? Lang.T("нема на плечі") : Lang.F("{0} з {1}", Power.Current(mm), Mathf.RoundToInt(Power.MaxWorn(mm)));
                        pc.Desc = Lang.T("Дари беруть його силу: кільце наповнене стільки, скільки її є зараз.");
                        break;
                    case Kind.ToOrders:
                        pc.Title = Lang.T("Накази"); pc.Sub = Lang.T("поруч · кружляй · облетіти · туди · жердина · трюки");
                        pc.Desc = Lang.F("Повернути коло на накази: куди ворон летить і що робить. Їхня власна клавіша — {0}.", RavenKeys.Label(RavenKeys.Get(RavenKeys.OrdersId)));
                        pc.Stays = true;
                        if (pc.Act == null) pc.Act = () => { if (_inst != null) _inst.Face(false); };
                        break;
                    case Kind.Pouch:
                    {
                        bool has = RavenPouch.Worn(item);
                        string food = has ? RavenPouch.Describe(item) : "";
                        pc.Title = Lang.T("Торба"); pc.Sub = !has ? Lang.F("на {0} нема торби", (mm ? Lang.T("Муніні") : Lang.T("Хугіні"))) : food.Length > 0 ? food : Lang.T("порожня");
                        pc.Desc = Lang.F("Відкрити торбу поруч із сумкою: поклади їжу (до {0} шматків). Голодний ворон поїсть із неї сам.", RavenPouch.Cap(item));
                        pc.Usable = has;
                        if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; string why = RavenPouch.Open(me, mm); if (why != null && me != null) RavenMsg.Center(me, why); };
                        break;
                    }
                    case Kind.Recall:
                    {
                        ItemDrop.ItemData ih = RavenSeat.On(p, false), im = RavenSeat.On(p, true);
                        bool away = Recallable(p, false, ih) || Recallable(p, true, im); // (only what AllHome ends: not a flight to Odin, not scouting)
                        bool one = _mode != 0;
                        pc.Title = one ? Lang.T("Назад") : Lang.T("Усіх назад");
                        pc.Sub = away ? (one ? Lang.T("на плече — звідусіль") : Lang.T("усіх на плечі — звідусіль")) : (one ? Lang.T("уже на плечі") : Lang.T("усі вже на плечах"));
                        pc.Desc = Lang.T("Кожен ворон кидає, що робить, злітає з жердини й летить тобі на плече. Безкоштовно.");
                        pc.Usable = away;
                        if (pc.Act == null) pc.Act = () => Perches.AllHome(Player.m_localPlayer);
                        break;
                    }
                    case Kind.Toggle:
                        pc.Title = _tricksOpen ? Lang.T("Згорнути трюки") : Lang.T("Трюки");
                        pc.Sub = _tricksOpen ? Lang.T("сховати") : Lang.T("розгорнути донизу");
                        pc.Desc = Lang.T("Фігури в повітрі, по десять у кожного ворона, а для двох — «Догонялки».");
                        pc.Now = _tricksOpen; pc.Stays = true;
                        if (pc.Act == null) pc.Act = () => { if (_inst == null) return; _inst._tricksOpen = !_inst._tricksOpen; _inst._tricksAt = Time.unscaledTime; _inst._built = -1f; };
                        break;
                    case Kind.Bind:
                    {
                        BindPower w = BindRun.Power;
                        pc.Title = Lang.T("В'язь");
                        pc.Sub = w == null ? "" : !BindRun.On ? Lang.T("вимкнена · клік — увімкнути") : Lang.F("зарядів {0} з {1} · клік — вимкнути", BindRun.Charges, w.Charges);
                        pc.Desc = BindRun.Plain + "\n" + Lang.T("Вимкнена в'язь не спрацьовує й не відновлює заряди.");
                        pc.Now = BindRun.On; pc.Stays = true;
                        pc.CoolLeft = BindRun.On ? BindRun.NextIn : 0f; pc.CoolAll = w != null ? w.Recharge : 1f;
                        if (pc.Act == null) pc.Act = () => BindRun.SetOn(Player.m_localPlayer, !BindRun.On);
                        break;
                    }
                    case Kind.BindFire:
                    {
                        BindPower w = BindRun.Power;
                        pc.Title = w != null && w.Active.Length > 0 ? w.ActiveTitle() : Lang.T("Сила в'язі");
                        pc.Sub = Lang.F("зарядів {0} з {1}", BindRun.Charges, w != null ? w.Charges : 0);
                        pc.Desc = BindRun.Plain;
                        pc.Usable = BindRun.On && BindRun.Active && BindRun.Charges > 0;
                        pc.CoolLeft = BindRun.Charges > 0 ? 0f : BindRun.NextIn; pc.CoolAll = w != null ? w.Recharge : 1f;
                        if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; string why = BindRun.Press(me); if (why != null && me != null) RavenMsg.Center(me, why); };
                        break;
                    }
                    case Kind.Feed:
                    {
                        bool stuffed = item != null && Satiety.Value(item) > Satiety.StuffedAt;
                        pc.Title = Lang.F("Погодувати {0}", (mm ? Lang.T("Муніна") : Lang.T("Хугіна")));
                        pc.Sub = item == null ? Lang.T("нема на плечі") : Satiety.ZoneWord(item) + " · " + Mathf.RoundToInt(Satiety.Value(item));
                        pc.Desc = stuffed ? Lang.F("{0} об'ївся: більше не влізе.", name) : Lang.T("Вибрати їжу з сумки.");
                        pc.Glow = stuffed; // drawn dim with a tick
                        pc.Usable = item != null;
                        pc.Stays = true;
                        int k = mm ? 1 : 0;
                        if (pc.Act == null) pc.Act = () => { _feedFor = _feedFor == k ? -1 : k; _feedScroll = 0f; _built = -1f; };
                        break;
                    }
                }
            }
            foreach (Piece pc in _pieces)
            {
                pc.Label = pc.Title;
                pc.CoolText = pc.CoolLeft > 0.5f ? (pc.Kind == Kind.Trick ? Lang.F("{0}с", Mathf.CeilToInt(pc.CoolLeft)) : Clock(pc.CoolLeft)) : null;
                if (pc.Kind == Kind.Bind) pc.Label = !BindRun.On ? Lang.T("вимк.") : BindRun.Charges + "/" + (BindRun.Power != null ? BindRun.Power.Charges : 0);
                else if (pc.Kind == Kind.BindFire) pc.Label = BindRun.Charges + "/" + (BindRun.Power != null ? BindRun.Power.Charges : 0); // (under the medallion)
                if (pc.Kind == Kind.Power) { pc.Strength = Mathf.Clamp01(Power.Current(pc.Munin) / Mathf.Max(1f, Power.MaxWorn(pc.Munin))); pc.Label = Power.Current(pc.Munin).ToString(); }
            }
            for (int i = 0; i < 2; i++)
            {
                bool m = i == 1;
                ItemDrop.ItemData it = RavenSeat.On(p, m);
                _state[i] = it != null ? StateLine(p, m, it) : null;
                _stateIcon[i] = StateOf(p, m, it);
                _zone[i] = it != null ? Satiety.Zone(it) : -1;
                _sat[i] = it != null ? Satiety.Value(it) : 0f;
                _far[i] = it != null && FarAt(p, m, it, out _farAt[i]);
                string dist = _far[i] ? " · " + Lang.F("{0} м", Mathf.RoundToInt(Vector3.Distance(p.transform.position, _farAt[i]))) : "";
                _chip[i] = it != null ? "<color=" + (m ? ChipMunin : ChipHugin) + "><b>" + Plugin.RavenName(m) + "</b></color><color=" + ChipDot + ">  ·  </color>"
                    + (_stateIcon[i] != 0 ? "<color=" + ChipAway + ">" + _state[i] + dist + "</color>" : _state[i]) : null;
            }
            if (_feedFor >= 0) FoodsNow(p);
        }

        // an order's name on its piece: a few long ones shortened (from the source names, then translated)
        private static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            { "Кружляй наді мною", Lang.K("Кружляй") }, { "Лети поруч", Lang.K("Поруч") }, { "Падаючий лист", Lang.K("Листок") }, { "Бриючий проліт", Lang.K("Над землею") },
        };
        private static string Short(int order)
        {
            string src = order >= 0 && order < Orders.NamesSrc.Length ? Orders.NamesSrc[order] : "", sh;
            return Lang.T(ShortNames.TryGetValue(src, out sh) ? sh : src);
        }

        private static void OrderPiece(Player p, Piece pc, int order)
        {
            bool mm = pc.Munin;
            string why = Orders.Why(p, mm, order);
            pc.Now = p != null && Orders.Of(p, mm) == order;
            pc.Title = Short(order);
            pc.Sub = why ?? (pc.Now ? Lang.T("● зараз") : Orders.PriceText(mm, order));
            pc.Desc = Orders.Desc[order] + (pc.Now ? Lang.T("\nЩе клік — скасувати, і він повернеться на плече.") : "");
            pc.Usable = why == null || pc.Now; // (going on: a click brings it back)
            pc.CoolLeft = pc.Now ? 0f : Orders.LeftOf(mm, order); pc.CoolAll = Orders.WaitOf(order);
            if (Array.IndexOf(RavenKeys.KeyedOrders, order) >= 0) { pc.KeyId = RavenKeys.OrderId(mm, order); pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId); }
            if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me == null) return; if (Orders.Of(me, mm) == order) Orders.Return(me, mm); else Orders.Give(me, mm, order); };
        }

        // a gift's name on its piece: without the raven's own name ("Світло Хугіна" → "Світло")
        private static string GiftTitle(RavenAbility a) { return a.Bare; }

        // «На руку» (ArmPerch): a button of the arm's row; from trust 5, your own raven only
        private static void ArmPiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin;
            pc.Hidden = !RavenSkills.Animations || item == null || !Trust.Active(item) || Trust.Stranger(item, p) || Trust.Level(item) < ArmPerch.MinTrust;
            if (pc.Hidden) return;
            // only «На руку» until it sits on your arm; then the rest of the row (the user, 2026-10-29: the arm first)
            bool on = ArmPerch.Holding(p, mm), flying = ArmPerch.Away(p, mm), seated = on && !flying;
            if (pc.Order == ArmPerch.ActToggle) _armRowN[mm ? 1 : 0] = seated ? ArmRowOrders.Length : 1;
            else if (!seated) { pc.Hidden = true; if (pc.Order == ArmPerch.ActMore) _armOpen[mm ? 1 : 0] = 0; return; }
            if (pc.Order == ArmPerch.ActMore) { MorePiece(p, pc, item); return; }
            if (pc.Order >= ArmPerch.ActTrick) { TrickPiece(p, pc, item); return; }
            if (pc.Act == null) { int what = pc.Order; pc.Act = () => { Player me = Player.m_localPlayer; if (me != null) ArmPerch.Act(me, mm, what); }; }
            if (pc.Order == ArmPerch.ActToggle)
            {
                // onto the arm (or called back onto it from the air), or back to the shoulder
                string why = on ? null : ArmPerch.WhyNot(p, mm, true);
                pc.Icon = seated ? "o-armback" : "o-arm"; pc.Now = on; pc.Stays = true; pc.Usable = why == null;
                pc.Title = seated ? Lang.T("На плече") : Lang.T("На руку");
                pc.Desc = on && flying ? Lang.T("Ворон летить до тебе на руку. Ще клік — сяде на плече.")
                    : on ? Lang.T("Ворон перестрибує з руки назад на плече, рука опускається.")
                    : flying ? Lang.T("Кличе ворона з польоту: він повертається й сідає тобі на руку.")
                    : Lang.T("Виставляєш руку, і ворон перестрибує на неї з плеча. Зброю чи знаряддя в руках ховаєш (R — дістати). Візьмеш щось у руки, вдариш, побіжиш чи стрибнеш — він сам повернеться на плече.");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : on && flying ? Lang.T("● летить на руку") : on ? Lang.T("● на руці")
                    : flying ? Lang.T("у польоті · покликати на руку") : Lang.T("з плеча на руку");
                return;
            }
            if (pc.Order == ArmPerch.ActFeed)
            {
                pc.Icon = "o-armfeed"; pc.Title = Lang.T("Нагодувати з руки");
                pc.Desc = Lang.T("Даєш з долоні його їжу з сумки: улюблену, якщо знаєш її, інакше ту, якої найбільше.");
                pc.Sub = Lang.T("● на руці");
                return;
            }
            // the toss: hungry, too weak, or an outing would be called off at once — grey with why; else its price, as «Кружляй»
            string no = ArmPerch.TossWhy(p, mm);
            pc.Icon = "o-toss"; pc.Stays = true; pc.Usable = no == null; // (the circle stays: «На руку» calls it back)
            pc.Title = Lang.T("Підкинути");
            pc.Desc = Lang.T("Змах рукою, і ворон злітає. Дивишся вдалину — облетить місцину попереду, інакше покружляє над тобою й повернеться.");
            pc.Sub = no != null ? no : Orders.PriceText(mm, Orders.Circle);
        }

        // «Звички»: drops (or folds) the row of its own tricks, so the circle is not cluttered with them
        private static readonly string TrickOwnSrc = Lang.K("У кожного ворона свої звички на руці: на довірі 3 він знає одну, з кожною новою довірою — ще одну. А часом на руці сам робить будь-яку.");
        private static void MorePiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            int open = ArmTricks.Open(item);
            _armOpen[pc.Munin ? 1 : 0] = open;
            pc.Icon = "o-more"; pc.Stays = true; pc.Now = _armMore;
            pc.Usable = open > 0;
            pc.Title = Lang.T("Звички");
            pc.Sub = open > 0 ? Lang.F("{0} з {1} · {2}", open, ArmTricks.Slots, _armMore ? Lang.T("сховати") : Lang.T("показати")) : Lang.F("довіра {0}+", ArmTricks.FirstTrust);
            pc.Desc = Lang.T(TrickOwnSrc);
            if (pc.Act == null) pc.Act = () => { _armMore = !_armMore; if (_inst != null) _inst._built = -1f; }; // (shown or hidden at once)
        }

        // a slot of its own tricks' row: only an open one, only while the row is dropped
        private static void TrickPiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin; // (shown only while it sits on your arm: ArmPiece)
            int slot = pc.Order - ArmPerch.ActTrick, trick = ArmTricks.At(item, mm, slot);
            pc.Hidden = !_armMore || trick < 0;
            if (!pc.Hidden)
            {
                pc.Icon = ArmTricks.Icons[trick];
                pc.Now = trick == ArmTricks.Other && ArmPerch.OnOther; // (on the other arm now: lit)
                pc.Title = ArmTricks.Names[trick];
                pc.Sub = Lang.T("● на руці");
                pc.Desc = ArmTricks.Desc[trick] + "\n" + Lang.T(TrickOwnSrc);
            }
            if (pc.Act == null) { int what = pc.Order; pc.Act = () => { Player me = Player.m_localPlayer; if (me != null) ArmPerch.Act(me, mm, what); }; }
        }

        private static readonly string TwoInOneSrc = Lang.K("Кнопка потрійна: зазвичай — «Жердина»; коли дивишся на свого прирученого звіра — «Гратися»; коли стоїш у воді — «Помити».");
        private static void PerchPiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin, on = item != null && Perches.On(item), spot = on && Perches.OnSpot(item);
            pc.Icon = null;
            pc.KeyId = mm ? RavenKeys.PerchMId : RavenKeys.PerchHId;
            pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId);
            if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me != null) BeastPlay.PerchOrPlay(me, mm); }; // (looked at again on the click)
            Character beast = !on && p != null ? BeastPlay.Aimed(p, mm) : null;
            pc.Play = beast != null;
            pc.Wash = false;
            if (beast != null)
            {
                string why = BeastPlay.WhyNot(p, beast) ?? Orders.Why(p, mm, Orders.Beast);
                pc.Title = Lang.T("Гратися");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1}", SitOn.NameOf(beast), Orders.PriceText(mm, Orders.Beast));
                pc.Desc = Lang.T("Грається з твоїм прирученим звіром у квача: смикає за хвіст, тікає, тоді сам ганяється за звіром. Хто переможе — як пощастить. Ворону — трохи довіри, звірові — наче його нагодували.") + "\n" + Lang.T(TwoInOneSrc);
                pc.Now = pc.Sits = false;
                pc.Usable = why == null;
                pc.CoolLeft = Orders.LeftOf(mm, Orders.Beast); pc.CoolAll = Orders.WaitOf(Orders.Beast);
                return;
            }
            if (!on && Bath.Wading(p))
            {
                string why = Bath.HandWhyNot(p, mm);
                pc.Wash = true;
                pc.Title = Lang.T("Помити");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.T("ти у воді · помити ворона");
                pc.Desc = Lang.T("Ворон сідає у воду біля тебе, а ти його миєш: бризки, тремтіння, і він знову чистий. Довіри за це більше, ніж коли купається сам.") + "\n" + Lang.T(TwoInOneSrc);
                pc.Now = pc.Sits = false;
                pc.Usable = why == null;
                pc.CoolLeft = 0f;
                return;
            }
            string seat = on && !spot ? Perches.SeatKind(item) : "perch";
            pc.Icon = seat == "nest" ? "o-nest" : seat == "tree" ? "o-tree" : null;
            pc.Title = seat == "nest" ? Lang.T("Гніздо") : seat == "tree" ? Lang.T("Дерево") : Lang.T("Жердина");
            pc.Sub = spot ? (Grammar.Female(p) ? Lang.T("сидить, де ти показала · покликати") : Lang.T("сидить, де ти показав · покликати")) : on ? Lang.F("● зараз · {0}", PerchDay(item)) : Perches.OrderSub(p, mm);
            pc.Desc = spot ? Lang.T("Кличе його звідти, де він сидить: злетить і прилетить до тебе.")
                    : on ? Lang.T("Кличе його з жердини: він злетить і прилетить до тебе, хоч би й здалека.")
                         : Lang.F("Шукає найближчу жердину з вільним місцем до {0} м, яку видно (не за стіною), і сідає на неї відпочити й набратися сили.", Mathf.RoundToInt(Perches.FindRadius));
            pc.Desc += "\n" + Lang.T(TwoInOneSrc);
            pc.Now = pc.Sits = on && !spot; // (on a spot: «Туди» shows it)
            pc.CoolLeft = 0f;
            ItemDrop.ItemData it;
            pc.Usable = on || Perches.WhyNot(p, mm, out it) == null; // on it: call back; else only when he may go (asleep, flying in, busy: not)
        }

        // «Догонялки»: your two together; or, aimed at another player, one of yours with one of theirs (JointPlay: a raven
        // picked here, an invitation to them); a game going on: its score, a click calls yours back
        private static readonly string PairDescSrc = Lang.K("Удвох з іншим гравцем: наведи приціл на нього, клікни й обери свого ворона — той гравець отримає запрошення й обере свого. Ворони граються над вами, рахунок видно в повітрі. Вороги грати не захочуть.");
        private void ChasePiece(Player p, Piece pc)
        {
            pc.CoolLeft = Mathf.Max(Orders.LeftOf(false, Orders.Chase), Orders.LeftOf(true, Orders.Chase)); pc.CoolAll = Orders.WaitOf(Orders.Chase);
            if (pc.Act == null) pc.Act = ChaseClick; // (looked at again on the click: what you aim at may change)
            pc.Stays = true; // (the click itself shuts the menu, or opens the raven picker over it)
            JointPlay.Game jg = JointPlay.Mine();
            if (jg != null)
            {
                int mine = jg.SlotOf(p.GetPlayerID());
                pc.Title = Lang.T("Догонялки удвох"); pc.Sub = Lang.F("{0} — {1} · з: {2}", jg.Score[mine], jg.Score[1 - mine], jg.Name[1 - mine]);
                pc.Now = true; pc.Usable = true;
                pc.Desc = Lang.T("Іде гра. Ще клік — покликати свого ворона назад (гру буде перервано).");
                return;
            }
            Player t = AboutTalk.Target(p);
            bool two = RavenSeat.On(p, false) != null && RavenSeat.On(p, true) != null;
            if (t != null && JointPlay.CanPlayWith(t))
            {
                pc.Title = Lang.T("Догонялки удвох");
                pc.Sub = Lang.F("з: {0}", t.GetPlayerName()) + (JointPlay.Waiting ? Lang.T(" · чекаємо відповіді") : "");
                pc.Desc = Lang.T(PairDescSrc);
                pc.Usable = !JointPlay.Waiting && (JointPlay.WhyNot(p, false) == null || JointPlay.WhyNot(p, true) == null);
                return;
            }
            if (two)
            {
                string why = Orders.WhyBoth(p, Orders.Chase);
                pc.Title = Lang.T("Догонялки"); pc.Sub = why ?? Lang.F("обидва, кожному: {0}", Orders.PriceText(false, Orders.Chase));
                pc.Now = Orders.Of(p, false) == Orders.Chase && Orders.Of(p, true) == Orders.Chase;
                pc.Desc = Orders.Desc[Orders.Chase] + (pc.Now ? Lang.T("\nЩе клік — скасувати, і обидва повернуться на плечі.") : "") + (JointPlay.Others ? "\n" + Lang.T(PairDescSrc) : "");
                pc.Usable = why == null || pc.Now;
                return;
            }
            pc.Title = Lang.T("Догонялки удвох");
            pc.Sub = JointPlay.Others ? Lang.T("наведи приціл на гравця") : Lang.T("потрібні обидва ворони");
            pc.Desc = JointPlay.Others ? Lang.T(PairDescSrc) : Orders.Desc[Orders.Chase];
            pc.Usable = false;
        }

        private void ChaseClick()
        {
            Player me = Player.m_localPlayer;
            if (me == null) return;
            JointPlay.Game jg = JointPlay.Mine();
            Player t = jg == null ? AboutTalk.Target(me) : null;
            if (t != null && JointPlay.CanPlayWith(t)) { _pick = true; _pickTarget = t; _pickWhyAt = 0f; _built = -1f; return; }
            Close();
            if (jg != null) { int mine = jg.SlotOf(me.GetPlayerID()); Orders.Return(me, jg.Munin[mine]); return; }
            if (Orders.Of(me, false) == Orders.Chase && Orders.Of(me, true) == Orders.Chase) Orders.ReturnBoth(me); else Orders.GiveBoth(me, Orders.Chase);
        }

        // ---- the raven picker (the pair chase): a small popup with your ravens, a click on one invites
        private bool _pick;
        private Player _pickTarget;
        private int _pickHover = -1;
        private readonly string[] _pickWhy = new string[2];
        private float _pickWhyAt;

        private static Rect PickRect(Vector2 c, float s)
        {
            float w = 2f * 150f * s + 3f * 12f * s, h = 30f * s + 120f * s + 14f * s;
            float y = Mathf.Min(c.y + Centre * s + 8f * s, Screen.height - h - 4f);
            return new Rect(c.x - w * 0.5f, y, w, h);
        }

        private void PickPopup(Rect r, float s, Vector2 mouse)
        {
            Player p = Player.m_localPlayer;
            if (p == null || _pickTarget == null) { _pick = false; return; }
            float rad = 10f * s, pad = 12f * s;
            RoundFill(new Rect(r.x + 2f * s, r.y + 4f * s, r.width, r.height), rad, new Color(0f, 0f, 0f, 0.45f));
            RoundFill(r, rad, RavenBook.TipFill);
            RoundLine(r, rad, A(Gold, 0.7f), Mathf.Max(1f, 1.5f * s));
            _title.alignment = TextAnchor.MiddleCenter;
            Label(new Rect(r.x, r.y + 4f * s, r.width, 24f * s), Lang.F("Догонялки з: {0}", _pickTarget.GetPlayerName()), _title, Bright, 15f * s);
            _pickHover = -1;
            if (Time.unscaledTime >= _pickWhyAt) { _pickWhyAt = Time.unscaledTime + 0.25f; _pickWhy[0] = JointPlay.WhyNot(p, false); _pickWhy[1] = JointPlay.WhyNot(p, true); }
            for (int k = 0; k < 2; k++)
            {
                bool m = k == 1;
                ItemDrop.ItemData item = RavenSeat.On(p, m);
                Rect cr = new Rect(r.x + pad + k * (150f * s + pad), r.y + 32f * s, 150f * s, 120f * s);
                if (item == null) { RoundLine(cr, 8f * s, A(Dim, 0.35f), Mathf.Max(1f, s)); Label(cr, Lang.T("не на плечі"), _small, Dim, 12f * s); continue; }
                string why = _pickWhy[k];
                bool hot = why == null && cr.Contains(mouse);
                if (hot) _pickHover = m ? 1 : 0;
                RoundFill(cr, 8f * s, hot ? new Color(0.22f, 0.18f, 0.12f, 1f) : new Color(0.13f, 0.11f, 0.08f, 1f));
                RoundLine(cr, 8f * s, hot ? Bright : A(m ? MLine : HLine, 0.8f), Mathf.Max(1f, 1.5f * s));
                GUI.color = why == null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                DrawSprite(new Rect(cr.center.x - 30f * s, cr.y + 8f * s, 60f * s, 60f * s), AbilityIcons.For(m ? "munin" : "hugin", m));
                GUI.color = Color.white;
                Label(new Rect(cr.x, cr.y + 70f * s, cr.width, 22f * s), Nicks.Name(p, m), _title, m ? MLine : HLine, 14f * s);
                Label(new Rect(cr.x + 4f * s, cr.y + 92f * s, cr.width - 8f * s, 22f * s), why ?? Lang.T("клік — покликати грати"), _small, why == null ? Ink : Dim, 11f * s);
            }
        }

        private void PickClick(int which)
        {
            Player me = Player.m_localPlayer;
            Player t = _pickTarget;
            _pick = false; _pickTarget = null;
            if (me == null || t == null || which < 0) return;
            string said = JointPlay.Invite(t, which == 1);
            if (!string.IsNullOrEmpty(said)) RavenMsg.Center(me, said);
            Close();
        }

        // «Туди», the context order (SitHere): says what it will do where you look, before you press it
        // the errand's deed goes on the «Туди» of the raven who brought it (of the other one when that one is not on you)
        private static bool ErrandHere(Player p, bool munin)
        {
            if (p == null || !Errands.CanAct(p)) return false;
            ErrandRun r = Errands.Of(p);
            return r != null && (r.Munin == munin || RavenSeat.On(p, r.Munin) == null);
        }

        private static void HerePiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin, on = item != null && Perches.On(item), spot = on && Perches.OnSpot(item);
            pc.Icon = null;
            pc.KeyId = mm ? RavenKeys.HereMId : RavenKeys.HereHId;
            pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId);
            if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me == null) return; if (ErrandHere(me, mm)) Errands.Act(me); else Perches.OrderHere(me, mm); };
            if (ErrandHere(p, mm)) // an errand's deed that can be done right here (Odin's feather…): it, not «Туди», while it can
            {
                string why;
                pc.Title = Errands.ActLabel(p, out why);
                pc.Sub = Lang.T("доручення Одіна");
                pc.Icon = "o-odin";
                pc.Desc = Lang.T("Дія доручення Одіна — те саме, що E на місці чи кнопка в книзі.");
                return;
            }
            string moveBlock = null;
            bool move = on && p != null && Perches.MovesTo(p, mm, item, SitHere.Look(p, mm), out moveBlock); // looking at another place: it moves there
            if (on && !move)
            {
                pc.Title = spot ? Lang.T("Назад") : Lang.T("Туди"); // (a click calls him back)
                pc.Icon = "o-recall";
                pc.Sub = spot ? Lang.T("● сидить · покликати") : Lang.F("сидить {0} · покликати", Perches.OnWord(item));
                pc.Desc = Lang.T("Кличе його звідти, де він сидить: злетить і прилетить до тебе.");
                pc.Now = pc.Sits = spot;
                return;
            }
            if (p != null && Orders.Of(p, mm) == Orders.Fetch)
            {
                pc.Title = Lang.T("Принести"); pc.Sub = Lang.T("● летить по здобич"); pc.Icon = "o-fetch"; pc.Desc = Orders.Desc[Orders.Fetch]; pc.Now = true; pc.Usable = false;
                return;
            }
            SitHere.Target t = p != null ? SitHere.Look(p, mm) : null;
            ItemDrop.ItemData it;
            string busy = move ? moveBlock : p != null ? Perches.WhyNot(p, mm, out it) : null;
            bool freePerch = false; // (a perch with room: free to sit on, «пересяде на неї» when he moves)
            if (t != null && t.Player != null)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("На гравця"); pc.Icon = "o-player";
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), SitHere.PriceText(mm));
                pc.Desc = Lang.T("Сідає на голову або на вільне плече гравця, на якого ти дивишся (куди саме — навмання з вільних місць), і сидить там, доки не повернеш. Гравець може струсити його перекатом.");
                pc.Usable = why == null;
            }
            else if (t != null && t.Animal != null)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("На тварину"); pc.Icon = "o-animal";
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), SitHere.PriceText(mm));
                pc.Desc = Lang.T("Сідає на ручну тварину, на яку ти дивишся: на спину (якщо ніхто не їде верхи) або на голову — навмання з вільних місць, і їде на ній, доки не повернеш. На диких не сідає.");
                pc.Usable = why == null;
            }
            else if (t != null && t.Item != null)
            {
                string why = t.Why ?? (RavenFetch.Carrying(mm) ? Lang.T("ще несе попереднє") : null) ?? Orders.Why(p, mm, Orders.Fetch);
                pc.Title = Lang.T("Принести"); pc.Icon = "o-fetch";
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), Orders.PriceText(mm, Orders.Fetch));
                pc.Desc = Lang.F("Летить до дрібниці, на яку ти дивишся, хапає одну штуку й приносить тобі в сумку. Лише легке — до {0} кг за штуку.", Satiety.Num(RavenFetch.MaxWeight));
                pc.Usable = why == null;
                pc.CoolLeft = Orders.LeftOf(mm, Orders.Fetch); pc.CoolAll = Orders.WaitOf(Orders.Fetch);
            }
            else if (t != null && t.Perch != null)
            {
                pc.Title = t.Perch.IsNest ? Lang.T("У це гніздо") : Lang.T("На цю жердину"); pc.Icon = t.Perch.IsNest ? "o-nest" : "o-perch";
                bool room = t.Perch.FreeSeat() >= 0;
                freePerch = busy == null && room;
                pc.Sub = busy != null ? busy.TrimEnd('.').Replace("\n", " ") : !room ? Lang.T("на ній нема місця") : Lang.T("сісти на неї · безкоштовно");
                pc.Desc = Lang.T("Сідає саме на ту жердину, на яку ти дивишся, — відпочити й набратися сили.");
                pc.Usable = busy == null && room;
            }
            else if (t != null && t.Building)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("Сісти тут"); pc.Icon = t.Follow != null ? "o-ship" : "o-roof";
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), SitHere.PriceText(mm));
                pc.Desc = Lang.F("Сідає зверху на будову, на яку ти дивишся (табличка, паркан, дах, портал...), і сидить там, доки не повернеш. Сили там не набирається. Відійдеш далі {0} м — полетить за тобою.", Mathf.RoundToInt(SitHere.Far));
                pc.Usable = why == null;
            }
            else
            {
                pc.Title = Lang.T("Туди");
                pc.Sub = Lang.T("наведи приціл");
                pc.Desc = Lang.T("Робить те, що пасує місцю, куди ти дивишся: гравець чи ручна тварина — сяде на них; дрібниця на землі — принесе; будова — сяде на неї зверху; жердина — сяде саме на цю. Спершу наведи приціл, тоді відкривай накази.");
                pc.Usable = false;
            }
            if (move) // from where it sits now, straight there
            {
                pc.Now = pc.Sits = spot; // he still shows as sitting on his spot; the click moves him
                pc.Moves = true; // (blocked: a click says why — from a spot or a perch alike)
                if (pc.Usable) pc.Sub = freePerch ? Lang.T("пересяде на неї · безкоштовно") : Lang.F("пересяде · {0}", pc.Sub);
                pc.Desc += Lang.T("\nВін сидить в іншому місці: злетить звідти й полетить сюди одразу, не сідаючи на тебе. Подивись на те місце, де він сидить (або в порожнечу), — і він повернеться до тебе.");
            }
        }

        private static string PerchDay(ItemDrop.ItemData item) { string d = Perches.DayText(item); int sp = d.IndexOf(' '); return sp > 0 ? d.Substring(0, sp) : d; }

        // what the raven is doing, under his name
        private static string StateLine(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return Lang.T("нема на плечі");
            if (!Trust.Active(item)) return Lang.T("спить · довіра 0");
            if (Trust.Stranger(item, p)) return Lang.T("чужий");
            if (Perches.OnSpot(item)) return Perches.SpotWord(p, munin);
            if (Perches.On(item)) return Lang.F("{0} · {1}", Perches.OnWord(item), PerchDay(item));
            int o = Orders.Of(p, munin);
            if (o == Orders.Follow) return Lang.T("в польоті · поруч");
            if (o == Orders.Circle) return Lang.T("в польоті · кружляє");
            if (o == Orders.Scout) return Lang.T("в польоті · облітає");
            if (o == Orders.Chase) return Lang.T("в польоті · догонялки");
            if (o != Orders.None) return Lang.F("в польоті · {0}", Lang.Lower(Short(o)));
            if (Trust.Away(item)) return Trust.AwayWord(munin, item);
            return Perks.FlightWord(p, munin); // (a gift, an outing, scouting: in the air though no order)
        }

        // where the raven is when it is off your shoulder: its seat (a perch, a nest, a «sit here» spot), else the bird in
        // the air (an order, an outing, a gift); false when it is on the shoulder, close by, or out of sight (at Odin's)
        private static bool FarAt(Player p, bool munin, ItemDrop.ItemData item, out Vector3 at)
        {
            at = Vector3.zero;
            if (Perches.On(item)) { if (!Perches.SpotPos(item, out at)) return false; }
            else
            {
                if (StateOf(p, munin, item) == 0) return false;
                Transform bird = Shoulder.SpeakerOf(p, munin);
                if (bird == null) return false;
                at = bird.position;
            }
            Vector3 d = at - p.transform.position; d.y = 0f;
            return d.sqrMagnitude > FarMin * FarMin;
        }
        private const float FarMin = 4f; // closer than this the pointer would only spin about

        // an order or a perch: what «Усіх назад» calls the raven back from
        private static bool Recallable(Player p, bool munin, ItemDrop.ItemData item) { return item != null && (Perches.On(item) || Orders.Of(p, munin) != Orders.None); }

        // 0 on the shoulder, 1 on a perch, 2 flying
        private static int StateOf(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return 0;
            if (Perches.On(item)) return 1;
            if (Orders.Of(p, munin) != Orders.None || Trust.Away(item) || (!munin && Shoulder.HuginAway) || Perks.OffWord(p, munin) != null) return 2;
            return 0;
        }

        private void Pick(int i)
        {
            if (i < 0 || i >= _pieces.Count) return;
            Piece pc = _pieces[i];
            if (pc.Act == null) return;
            // not now: the menu stays; a gift still says why in the middle of the screen (RavenSkills.Use names every
            // refusal): a click that did nothing at all looked broken
            if (!pc.Usable) { if (pc.Kind == Kind.Gift && pc.Gift != null) RavenSkills.Use(pc.Gift); else if (pc.Kind == Kind.BindFire || pc.Kind == Kind.Arm || (pc.Kind == Kind.Here && pc.Moves)) pc.Act(); return; } // (the в'язь's press and a blocked move say why not too)
            Action act = pc.Act;
            if (!pc.Stays) Close();
            try { act(); } // an order that cannot go now says why itself
            catch (Exception e)
            {
                Player me = Player.m_localPlayer;
                if (me != null) RavenMsg.Center(me, Lang.F("{0}: щось пішло не так.", (pc.Title ?? "")));
                if (Plugin.Instance != null) Plugin.Instance.LogError("Circle piece " + pc.Title + " failed: " + e);
            }
        }

        // ---------------------------------------------------------------- drawing

        private static readonly Color Gold = Hex("#E6C77C"), Bright = Hex("#FFD98A"), HoverLine = Hex("#FFF6DC"), GoldGlow = Hex("#FFC23A");
        private static readonly Color HLine = Hex("#6FA8D6"), HDeep = Hex("#3A6E99"), MLine = Hex("#C2503A"), MDeep = Hex("#8E1E1E");
        private static readonly Color TileHot = new Color(0.16f, 0.12f, 0.07f, 0.92f);
        private static readonly Color Ink = new Color(0.93f, 0.9f, 0.82f), Dim = new Color(0.6f, 0.57f, 0.5f);

        private static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }
        private static Color A(Color c, float a) { c.a *= a; return c; }

        private static GUIStyle _title, _small, _info;

        // IMGUI calls this for every event (layout, repaint, each mouse move and click): everything is drawn on the
        // repaint only, the other events just find what is under the pointer and take the clicks
        private void OnGUI()
        {
            if (!_open || _fromRadial) return; // (from G: not before the game's radial has gone)
            Event e = Event.current;
            if (e.type == EventType.Layout) return; // drawn by hand: nothing to lay out
            Player p = Player.m_localPlayer;
            if (p == null) return;
            float s = ScaleNow;
            _g = _gifts;
            Styles();
            Layout(p);
            Plain(s);
            Build(p);
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f - 20f * s); // (room over it for the line, under it for the tricks)
            Vector2 mouse = e.mousePosition;
            Rect popup = _feedFor >= 0 ? PopupRect(c, s) : _pick ? PickRect(c, s) : new Rect(0, 0, 0, 0);
            bool inPopup = (_feedFor >= 0 || _pick) && popup.Contains(mouse);
            if (e.type == EventType.Repaint || e.type == EventType.MouseMove) _hover = inPopup ? -1 : HitTest(mouse - c, s);
            if (_hover >= _pieces.Count) _hover = -1; // the layout changed under it
            if (e.type != EventType.Repaint) { Clicks(e, c, s, inPopup); return; }

            if (_mode == -2)
            {
                InfoBar(c, s, Lang.T("<b>Жодного ворона на плечі</b>"), Lang.F("Посади ворона на плече (правий клік по ньому в сумці), щоб {0}", (_gifts ? Lang.T("кликати його дари.") : Lang.T("давати накази."))));
                return;
            }

            // the plates: each raven's wing, the tricks' band (dropped), the middle disc and the small ones under «Трюки»
            // and «Догонялки»
            Wings(c, s);
            if (!_g) ArmBands(c, s);
            if (!_g && _tricksOpen) Band(c, s);
            Plate(c, DiscR, s, 1f);
            if (!_g)
            {
                foreach (Piece pc in _pieces)
                    if (pc.Kind == Kind.Toggle || (pc.Kind == Kind.Chase && _tricksOpen)) Plate(c + Dir(pc.A0) * pc.R0 * s, pc.CR + 7f * GS, s, pc.Kind == Kind.Chase ? BandAlpha() : 1f);
            }

            for (int i = 0; i < _pieces.Count; i++) DrawPiece(_pieces[i], i, c, s);
            if (_g) { PowerTicks(c, s); CentreLabel(c, s); }
            else SatietyArcs(c, s);
            Chips(c, s);
            if (!_g) Pointers(p, c, s);

            // the line over the circle: the piece under the pointer, or the trick waiting for its key
            if (_bindId != null) InfoBar(c, s, Lang.F("<b>Клавіша для «{0}»</b>", _bindName), Lang.T("Натисни клавішу (можна з Shift, Ctrl, Alt) · Esc — скасувати · Backspace — прибрати"));
            else if (_feedFor >= 0 && inPopup) InfoBar(c, s, Lang.F("<b>Погодувати {0}</b>", (_feedFor == 1 ? Lang.T("Муніна") : Lang.T("Хугіна"))), Lang.T("Клік по їжі — нагодувати. Колесо — гортати."));
            else if (_pick) InfoBar(c, s, Lang.T("<b>Догонялки удвох</b>"), Lang.T("Обери, котрого ворона кликати грати. Той гравець отримає запрошення й обере свого. Esc — назад."));
            else if (_hover >= 0)
            {
                Piece pc = _pieces[_hover];
                if (pc.Info == null) // made once per change of the piece (Build clears it)
                {
                    bool own = pc.Kind == Kind.Trick || pc.Kind == Kind.Order || pc.Kind == Kind.Here || pc.Kind == Kind.Pouch || pc.Kind == Kind.Gift || pc.Kind == Kind.Arm;
                    bool bound = pc.KeyLabel != null && pc.KeyLabel != "—";
                    int gold = pc.Kind == Kind.Gift && pc.Gift != null && GiftUp.Upgradable.Contains(pc.Gift.Id) ? GiftUp.Level(pc.Gift.Id) : 0; // golden runes in it
                    pc.Info = "<b>" + (own ? Plugin.RavenName(pc.Munin) + ": " : "") + pc.Title + "</b>  <color=#BFB59A>" + pc.Sub + "</color>"
                        + (gold > 0 ? Lang.F("  <color=#E3C27C>· золотий дар {0}</color>", GiftUp.Roman(gold)) : "");
                    pc.InfoDesc = pc.Desc + (gold > 0 ? "  <color=#E3C27C>" + GiftUp.ShortText(pc.Gift.Id, gold) + ".</color>" : "") + (pc.KeyId != null ? Lang.F("  ПКМ — {0}.", (bound ? Lang.F("змінити клавішу ({0})", pc.KeyLabel) : Lang.T("призначити клавішу"))) : "");
                    pc.Tip = pc.Kind == Kind.Trick ? pc.Title + (bound ? "  [" + pc.KeyLabel + "]" : "") : null;
                }
                InfoBar(c, s, pc.Info, pc.InfoDesc);
                if (pc.Tip != null) Tip(new Vector2(mouse.x - 85f * s, c.y + RimNow * s + 8f * s), s, pc.Tip); // under the tricks' band, clear of it (the circle is drawn over the plates)
            }
            else if (_gifts) InfoBar(c, s, Lang.T("<b>Дари</b>"), Lang.T("Наведи на дар — тут буде, що він робить. У центрі — накази."));
            else InfoBar(c, s, Lang.T("<b>Накази</b>"), Lang.T("Наведи на кнопку — тут буде, що вона робить."));

            if (_feedFor >= 0) FeedPopup(popup, s, mouse);
            else if (_pick) PickPopup(popup, s, mouse);
        }

        private void Clicks(Event e, Vector2 c, float s, bool inPopup)
        {
            // a click acts when the button comes up, not down: while it is held the menu is still open and the game
            // does not take it (a swing); by then the button is up and nothing is left for the game to read
            if (e.type == EventType.ScrollWheel && inPopup) { _feedScroll += e.delta.y * 18f * s; e.Use(); return; }
            // the right button is read in Update (IMGUI sends it no plain mouse-up on Windows): here only kept from the game
            if (e.type == EventType.ContextClick || ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && e.button == 1)) { e.Use(); return; }
            if (e.type == EventType.MouseDown && e.button == 0) { _downIn = true; e.Use(); return; }
            if (e.type != EventType.MouseUp || !_downIn) return;
            _downIn = false;
            e.Use();
            if (_bindId != null) return; // waiting for a key: clicks do nothing
            if (_pick)
            {
                if (inPopup) { if (e.button == 0 && _pickHover >= 0) PickClick(_pickHover); return; }
                _pick = false; _pickTarget = null; return; // a click beside it shuts it
            }
            if (_feedFor >= 0)
            {
                if (inPopup) { if (e.button == 0 && _feedHover >= 0) FeedClick(_feedHover); return; }
                int under = HitTest(e.mousePosition - c, s);
                if (under < 0 || _pieces[under].Kind != Kind.Feed) { _feedFor = -1; return; } // a click beside the popup shuts it
            }
            int hit = HitTest(e.mousePosition - c, s);
            if (hit >= 0) Pick(hit);
            else if (hit == -1) Close();
        }

        private void DrawPiece(Piece pc, int index, Vector2 c, float s)
        {
            bool hot = index == _hover;
            if (pc.Hidden) return;
            if (pc.Kind == Kind.ToOrders) { OrdersButton(c, s, hot); return; }
            if (pc.Kind == Kind.Gift) { RoundGift(pc, c, s, hot); return; }
            if (pc.Kind == Kind.Power) { PowerRing(pc, c, s); return; }
            if (pc.Kind == Kind.BindFire && AbilityIcons.Get("bind") != null) { BindMedallion(pc, c + Dir(pc.A0) * pc.R0 * s, pc.CR * 2f * s, hot, s); return; }
            if (pc.Kind == Kind.Bind || pc.Kind == Kind.BindFire) { BindButton(pc, c + Dir(pc.A0) * pc.R0 * s, BindD * GS * s, hot, s); return; } // (no medallion picture: the press drawn the switch's size)
            float scale = 1f, alpha = 1f;
            Vector2 at = c + Dir(pc.A0) * pc.R0 * s;
            if (pc.Kind == Kind.Trick || pc.Kind == Kind.Chase)
            {
                if (!_tricksOpen) return;
                at = FlyOut(pc, c, s, out scale, out alpha);
                if (alpha <= 0.01f) return;
            }
            OrderButton(pc, at, pc.CR * 2f * s * scale, hot, alpha, s);
        }

        private static readonly Color SatRed = Hex("#e05a4a");
        private static readonly Color IconInk = Hex("#e6c57d"), HoverGlow = new Color(0.94f, 0.78f, 0.43f, 1f), OffInk = new Color(0.45f, 0.47f, 0.5f, 1f);

        // A round button of the orders' face, after the user's reference (2026-10-20): a gold frame — with the rune ring
        // for the orders and «Усіх назад», plain for the food, «Трюки» and the tricks — round a disc in the raven's colour
        // (Облетіти green, the shared ones dark), a gold line icon. Its states: under the pointer a soft glow, a touch
        // bigger; going on (a glow, round it the time it has left); the raven sits where it sent him (the disc lit with
        // his colour, «СИДИТЬ ТУТ» under it: a click calls him back; «СИДИТЬ · ПЕРЕСАДИТИ» when «Туди» moves him); not now (grey, half seen); waiting (a dark share).
        private void OrderButton(Piece pc, Vector2 at, float d, bool hot, float alpha, float s)
        {
            bool gold = pc.Kind == Kind.Recall || pc.Kind == Kind.Toggle || pc.Kind == Kind.Chase;
            bool scout = pc.Kind == Kind.Order && pc.Order == Orders.Scout;
            bool runes = pc.Kind == Kind.Order || pc.Kind == Kind.Here || pc.Kind == Kind.Pouch || pc.Kind == Kind.Recall;
            bool off = !pc.Usable && !pc.Now;
            string colour = off ? "grey" : gold ? "dark" : scout ? "green" : pc.Munin ? "red" : "blue";
            float a = alpha * (off ? 0.5f : 1f);
            if (hot && !off) d *= 1.04f;

            bool calls = pc.Kind == Kind.Recall && pc.Usable; // a raven is away: «Усіх назад» calls
            if ((hot && !off) || pc.Now || calls) // the glow round it
            {
                float g = d * 1.5f;
                GUI.color = A(pc.Now || calls ? GoldGlow : HoverGlow, (hot ? 0.55f : 0.42f) * a);
                GUI.DrawTexture(new Rect(at.x - g * 0.5f, at.y - g * 0.5f, g, g), _soft);
            }
            Rect r = new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(r, Asset(FrameName(runes, colour)));
            float inner = d * (runes ? 0.78f : 0.84f); // the frame's disc
            Rect ir = new Rect(at.x - inner * 0.5f, at.y - inner * 0.5f, inner, inner);
            if (pc.Sits) { GUI.color = A(pc.Munin ? MLine : HLine, 0.55f * a); GUI.DrawTexture(ir, _disc); }
            if (pc.CoolLeft > 0.5f && pc.CoolAll > 0f) Overlay(ir, _disc, new Color(0f, 0f, 0f, 0.55f * a), Mathf.Clamp01(pc.CoolLeft / pc.CoolAll));

            // what is on it: an icon (the tricks: their rune); «Усіх назад» its words under the icon
            Color ink = off ? OffInk : pc.Sits ? new Color(1f, 0.95f, 0.84f) : IconInk;
            GUI.color = A(ink, a);
            if (pc.Kind == Kind.Trick)
            {
                float rs = d * 0.46f;
                GUI.DrawTexture(new Rect(at.x - rs * 0.5f, at.y - rs * 0.5f, rs, rs), Rune(pc.Order));
            }
            else
            {
                bool words = pc.Kind == Kind.Recall;
                float isz = d * (words ? 0.26f : pc.Kind == Kind.Arm ? 0.8f : 0.42f); // (the arm's buttons are small: their pictures twice as big in them)
                Vector2 ic = words ? at - new Vector2(0f, d * 0.12f) : at;
                GUI.DrawTexture(new Rect(ic.x - isz * 0.5f, ic.y - isz * 0.5f, isz, isz), Asset(IconOf(pc)));
                if (words) Text(at + new Vector2(0f, d * 0.18f), 0f, "<b>" + pc.Title.ToUpperInvariant() + "</b>", _small, A(new Color(0.85f, 0.78f, 0.64f), a), Mathf.Max(9f * s, d * 0.1f), d * 1.1f);
                if (pc.Kind == Kind.Feed && pc.Glow) Text(at + new Vector2(d * 0.38f, -d * 0.36f), 0f, "✓", _title, A(Bright, a), 13f * GS * s, 20f * s); // stuffed
            }
            GUI.color = Color.white;


            // going on: the ring of the time it has left (no clock under it: the user); a raven sitting there: words under it
            if (pc.Now && !pc.Sits && pc.Kind == Kind.Order && pc.Order > 0)
            {
                float share = Orders.TimeShare(pc.Munin), tr = d + 14f * GS * s;
                Rect rr = new Rect(at.x - tr * 0.5f, at.y - tr * 0.5f, tr, tr);
                GUI.color = A(Bright, 0.22f * a); GUI.DrawTexture(rr, TimeRing(40));
                GUI.color = A(Bright, a); GUI.DrawTexture(rr, TimeRing(Mathf.RoundToInt(share * 40f)));
                GUI.color = Color.white;
            }
            else if (pc.Sits) Badge(at + new Vector2(0f, d * 0.5f + 5f * GS * s), pc.Moves && pc.Usable ? Lang.T("СИДИТЬ · ПЕРЕСАДИТИ") : Lang.T("СИДИТЬ ТУТ"), s, a);
            else if (pc.Kind == Kind.Trick && pc.KeyLabel != null && pc.KeyLabel != "—" && alpha >= 0.99f) Text(at + new Vector2(0f, d * 0.5f + 8f * s), 0f, pc.KeyLabel, _small, A(Bright, a), 9f * GS * s, 60f * s);
            if (pc.CoolText != null && pc.Kind != Kind.Trick) Text(at + new Vector2(0f, d * 0.5f + 8f * GS * s), 0f, pc.CoolText, _small, A(Bright, a), 11f * GS * s, d);
            else if (pc.CoolText != null) Text(at - new Vector2(0f, d * 0.5f + 7f * s), 0f, pc.CoolText, _small, A(Bright, a), 9f * GS * s, 60f * s);
        }

        // the в'язь's switch (its charges written on it; off: grey, «вимк.») and its press (a button-в'язь)
        private void BindButton(Piece pc, Vector2 at, float d, bool hot, float s)
        {
            bool off = pc.Kind == Kind.Bind ? !BindRun.On : !pc.Usable;
            float a = off ? 0.55f : 1f;
            if (hot) d *= 1.04f;
            if (hot || (pc.Kind == Kind.Bind && pc.Now))
            {
                float g = d * 1.5f;
                GUI.color = A(pc.Now ? GoldGlow : HoverGlow, (hot ? 0.55f : 0.35f) * a);
                GUI.DrawTexture(new Rect(at.x - g * 0.5f, at.y - g * 0.5f, g, g), _soft);
            }
            Rect r = new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(r, Asset("f-plain-" + (off ? "grey" : "dark")));
            float inner = d * 0.84f;
            Rect ir = new Rect(at.x - inner * 0.5f, at.y - inner * 0.5f, inner, inner);
            if (pc.CoolLeft > 0.5f && pc.CoolAll > 0f) Overlay(ir, _disc, new Color(0f, 0f, 0f, 0.55f * a), Mathf.Clamp01(pc.CoolLeft / pc.CoolAll));
            Text(at, 0f, pc.Label ?? "", _title, A(off ? OffInk : IconInk, a), Mathf.Max(9f * s, d * 0.3f), d);
            GUI.color = Color.white;
        }

        // the в'язь's press drawn as a gift is: its medallion (assets/icons/bind.png), its charges under it, its wait over it
        private void BindMedallion(Piece pc, Vector2 at, float d, bool hot, float s)
        {
            if (hot)
            {
                float rd = d + 6f * GS * s;
                GUI.color = HoverLine;
                GUI.DrawTexture(new Rect(at.x - rd * 0.5f, at.y - rd * 0.5f, rd, rd), _disc);
            }
            Rect r = new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d);
            GUI.color = pc.Usable ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
            DrawSprite(r, AbilityIcons.Get("bind"));
            if (pc.CoolLeft > 0.5f && pc.CoolAll > 0f) Overlay(r, _disc, new Color(0f, 0f, 0f, 0.6f), Mathf.Clamp01(pc.CoolLeft / pc.CoolAll));
            if (!pc.Usable) { GUI.color = new Color(0f, 0f, 0f, 0.3f); GUI.DrawTexture(r, _disc); }
            GUI.color = Color.white;
            Text(at + new Vector2(0f, d * 0.5f + 8f * GS * s), 0f, pc.Label, _small, Bright, 11f * GS * s, d);
        }

        // which picture a button of the orders' face shows
        private static string IconOf(Piece pc)
        {
            if (pc.Icon != null) return pc.Icon;
            switch (pc.Kind)
            {
                case Kind.Here: return "o-here";
                case Kind.Pouch: return "o-bag";
                case Kind.Feed: return pc.Munin ? "o-meat" : "o-berries";
                case Kind.Recall: return "o-recall";
                case Kind.Toggle: return _inst != null && _inst._tricksOpen ? "o-up" : "o-down";
                case Kind.Chase: return "o-chase";
                default: return pc.Order < 0 ? (pc.Play ? "o-chase" : pc.Wash ? "o-wash" : "o-perch") : pc.Order == Orders.Scout ? "o-scout" : pc.Order == Orders.Follow ? "o-near" : "o-circle";
            }
        }

        // a few words in a small dark pill with a gold edge (under a button that is on)
        private static void Badge(Vector2 at, string text, float s, float alpha)
        {
            float fs = 10f * GS * s - 1f;
            string words = "<b>" + text + "</b>";
            _small.fontSize = Mathf.Max(8, Mathf.RoundToInt(fs));
            float h = fs + 8f * s, w = _small.CalcSize(new GUIContent(words)).x + h; // (the round ends take half a height each)
            Rect r = new Rect(at.x - w * 0.5f, at.y - h * 0.5f, w, h);
            float e = Mathf.Max(1f, Mathf.Round(s)); // the gold edge
            Pill(new Rect(r.x - e, r.y - e, r.width + e * 2f, r.height + e * 2f), A(Bright, alpha));
            Pill(r, new Color(0.16f, 0.11f, 0.03f, 0.97f * alpha));
            GUI.color = Color.white;
            Label(r, words, _small, A(Bright, alpha), fs);
        }

        // a rounded box: a strip with a half-disc at each end, in one colour
        private static void Pill(Rect r, Color col)
        {
            float h = r.height;
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x + h * 0.5f, r.y, Mathf.Max(0f, r.width - h), h), Texture2D.whiteTexture);
            GUI.BeginGroup(new Rect(r.x, r.y, h * 0.5f, h)); GUI.DrawTexture(new Rect(0f, 0f, h, h), _disc); GUI.EndGroup();
            GUI.BeginGroup(new Rect(r.xMax - h * 0.5f, r.y, h * 0.5f, h)); GUI.DrawTexture(new Rect(-h * 0.5f, 0f, h, h), _disc); GUI.EndGroup();
        }

        // a dark round plate with a faint gold edge (the middle; under «Трюки» and «Догонялки»)
        private static void Plate(Vector2 at, float radius, float s, float alpha)
        {
            float r = radius * s, rl = r + 1.5f * s;
            GUI.color = new Color(0.66f, 0.52f, 0.23f, 0.45f * alpha);
            GUI.DrawTexture(new Rect(at.x - rl, at.y - rl, rl * 2f, rl * 2f), _disc);
            GUI.color = new Color(0.07f, 0.05f, 0.035f, 0.95f * alpha);
            GUI.DrawTexture(new Rect(at.x - r, at.y - r, r * 2f, r * 2f), _disc);
            GUI.color = Color.white;
        }

        // the tricks fly out of «Трюки» one after another (each a little later), easing past their place and back
        private const float FlyTime = 0.32f, FlyStep = 0.022f;
        private Vector2 FlyOut(Piece pc, Vector2 c, float s, out float scale, out float alpha)
        {
            Vector2 to = c + Dir(pc.A0) * pc.R0 * s, from = c + Dir(180f) * (_mode == 0 ? ToggleTwo : ToggleOne) * s;
            int seq = pc.Kind == Kind.Chase ? 10 : pc.Seq;
            float t = Mathf.Clamp01((Time.unscaledTime - _tricksAt - seq * FlyStep) / FlyTime);
            float back = 1.6f, e = 1f + (back + 1f) * Mathf.Pow(t - 1f, 3f) + back * Mathf.Pow(t - 1f, 2f); // ease out with a small overshoot
            scale = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(e));
            alpha = Mathf.Clamp01(t * 1.8f);
            return Vector2.LerpUnclamped(from, to, e);
        }

        private float BandAlpha() { return Mathf.Clamp01((Time.unscaledTime - _tricksAt) / 0.25f); }

        // the tricks' band: each raven's part in his colour (one raven: one band in the middle)
        private void Band(Vector2 c, float s)
        {
            float a = BandAlpha();
            for (int k = 0; k < 2; k++)
            {
                bool munin = k == 1;
                if (_mode == 1 && munin || _mode == 2 && !munin) continue;
                Shape sh = _mode != 0 ? ShapeFor(150f, 210f, OneBand0, OneBand1, 14f * GS, true, s)
                    : munin ? ShapeFor(-TwoBandA1, -TwoBandA0, TwoBand0, TwoBand1, 14f * GS, true, s) : ShapeFor(TwoBandA0, TwoBandA1, TwoBand0, TwoBand1, 14f * GS, true, s);
                Rect r = Place(sh, c, s);
                GUI.color = munin ? new Color(0.24f, 0.09f, 0.06f, 0.92f * a) : new Color(0.06f, 0.13f, 0.22f, 0.92f * a);
                GUI.DrawTexture(r, sh.Fill);
                GUI.color = A(munin ? MLine : HLine, 0.6f * a);
                GUI.DrawTexture(r, sh.Line);
            }
            GUI.color = Color.white;
        }

        // the arm's thin band outside each wing, in the raven's colour (only while its row is shown)
        private void ArmBands(Vector2 c, float s)
        {
            for (int k = 0; k < 2; k++)
            {
                if (!_armShown[k]) continue;
                bool munin = k == 1;
                float start = _mode != 0 ? ArmOneA0 : ArmTwoA0;
                for (int row = 0; row < 2; row++)
                {
                    if (row == 1 && (!_armMore || _armOpen[k] <= 0)) break; // (its tricks: only dropped, as long as it has)
                    float a0 = start - ArmPad, a1 = row == 0 ? start + ArmStep * (_armRowN[k] - 1) + ArmPad : start + 0.5f + ArmQStep * (_armOpen[k] - 1) + ArmPad;
                    float r0 = row == 0 ? ArmR0 : ArmQ0, r1 = row == 0 ? ArmR1 : ArmQ1;
                    Shape sh = munin ? ShapeFor(-a1, -a0, r0, r1, 14f * GS, true, s) : ShapeFor(a0, a1, r0, r1, 14f * GS, true, s);
                    Rect r = Place(sh, c, s);
                    GUI.color = munin ? new Color(0.16f, 0.07f, 0.05f, row == 0 ? 0.88f : 0.8f) : new Color(0.05f, 0.08f, 0.13f, row == 0 ? 0.88f : 0.8f);
                    GUI.DrawTexture(r, sh.Fill);
                    GUI.color = A(munin ? MLine : HLine, row == 0 ? 0.55f : 0.4f);
                    GUI.DrawTexture(r, sh.Line);
                }
            }
            GUI.color = Color.white;
        }

        // over each wing a chip: the raven and where he is, what he does (lit gold while he is away from the shoulder);
        // the book's tip plate, as the info bar and the hints
        private void Chips(Vector2 c, float s)
        {
            for (int k = 0; k < 2; k++)
            {
                if (_chip[k] == null) continue;
                bool munin = k == 1;
                Vector2 at = c + (_mode == 0 ? new Vector2((munin ? -1f : 1f) * 150f * GS, -ChipR) : new Vector2(0f, -ChipR)) * s;
                Vector2 size = ChipPlates[k].SetLine(_chip[k], 0.9f * GS * s);
                ChipPlates[k].Place(at.x - size.x * 0.5f, at.y - size.y * 0.5f);
            }
        }

        // a raven off the shoulder: a small arrow in its colour on the middle disc's edge, turned the way it is from you,
        // as the camera looks (up = ahead); its distance is in its chip. Worked out every frame: you may turn meanwhile.
        private void Pointers(Player p, Vector2 c, float s)
        {
            Camera cam = Utils.GetMainCamera();
            Vector3 ahead = cam != null ? cam.transform.forward : p.transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f) return;
            for (int k = 0; k < 2; k++)
            {
                if (!_far[k]) continue;
                Vector3 d = _farAt[k] - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude < 0.0001f) continue;
                float ang = Vector3.SignedAngle(ahead, d, Vector3.up); // (clockwise from ahead, as the screen's)
                Vector2 at = c + Dir(ang) * (DiscR + PointerR) * s;
                float w = 18f * GS * s, h = 13f * GS * s;
                Matrix4x4 keep = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, at);
                Color was = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f); // (a dark rim under it: seen on the disc and on the world)
                GUI.DrawTexture(new Rect(at.x - w * 0.5f - 2f * s, at.y - h * 0.5f - 2f * s, w + 4f * s, h + 4f * s), _pointer);
                GUI.color = k == 1 ? MLine : HLine;
                GUI.DrawTexture(new Rect(at.x - w * 0.5f, at.y - h * 0.5f, w, h), _pointer);
                GUI.color = was;
                GUI.matrix = keep;
            }
        }
        private const float PointerR = 11f * GS; // past the disc's edge, short of the wings

        // a strength half-ring: it fills like a bowl, from the bottom
        private static void PowerRing(Piece pc, Vector2 c, float s)
        {
            Shape sh = PieceShape(pc, s);
            Rect r = Place(sh, c, s);
            GUI.color = A(pc.Munin ? MDeep : HDeep, 0.35f);
            GUI.DrawTexture(r, sh.Fill);
            Bowl(pc, r, sh, 1f);
            GUI.color = Color.white;
        }

        // the satiety: a stroke along the middle disc from the raven's food button up (Munin left, Hugin right; one raven
        // all round), four parts (0–10, 10–30, 30–90, 90–100) faint, lit up to the satiety, three marks of their own sizes
        private static readonly Color[] SatInk = { Hex("#b8322a"), Hex("#d8a83a"), Hex("#8fc45a"), Hex("#9a7ac0") };
        private static readonly float[] SatEdge = { 0f, Satiety.HungryAt, Satiety.AskAt, Satiety.StuffedAt, 100f };
        private static float SatAngle(int mode, bool munin, float v)
        {
            if (mode != 0) return 197f + 326f * v / 100f;
            return munin ? 212f + 146f * v / 100f : 148f - 146f * v / 100f;
        }
        private static Shape SatShape(int mode, bool munin, float v0, float v1, float s)
        {
            float a = SatAngle(mode, munin, v0), b = SatAngle(mode, munin, v1);
            return ShapeFor(Mathf.Min(a, b), Mathf.Max(a, b), SatIn, SatOut, 1.5f, false, s);
        }

        // the faint parts and the marks: the same few shapes every frame, kept by index (not looked up by a new text key
        // each repaint); again when the scale changes (ShapeFor frees its shapes then)
        private static readonly Shape[] _satAll = new Shape[16], _satMark = new Shape[12];
        private static float _satS = -1f;

        private void SatietyArcs(Vector2 c, float s)
        {
            if (_satS != s) { System.Array.Clear(_satAll, 0, _satAll.Length); System.Array.Clear(_satMark, 0, _satMark.Length); _satS = s; }
            int mi = _mode != 0 ? 1 : 0;
            for (int k = 0; k < 2; k++)
            {
                bool munin = k == 1;
                if (_zone[k] < 0) continue; // not on you
                if (_satShown[k] < 0f) _satShown[k] = _sat[k];
                _satShown[k] = Mathf.MoveTowards(_satShown[k], _sat[k], 60f * Time.unscaledDeltaTime);
                float v = Mathf.Round(Mathf.Clamp(_satShown[k], 0f, 100f)); // (whole points: the lit part is a shape of its own)
                float gap = _mode != 0 ? 0.9f : 1.2f;
                for (int z = 0; z < 4; z++)
                {
                    float z0 = SatEdge[z] + gap, z1 = SatEdge[z + 1] - gap; // (in points: a small gap between the parts)
                    int ai = (mi * 2 + k) * 4 + z;
                    Shape all = _satAll[ai] ?? (_satAll[ai] = SatShape(_mode, munin, z0, z1, s));
                    GUI.color = A(SatInk[z], 0.22f);
                    GUI.DrawTexture(Place(all, c, s), all.Fill);
                    if (v <= z0) continue;
                    Shape lit = v >= z1 ? all : LitShape(k, z, munin, z0, v, s);
                    GUI.color = SatInk[z];
                    GUI.DrawTexture(Place(lit, c, s), lit.Fill);
                }
                for (int m = 1; m <= 3; m++) // the marks at 15 (the biggest, red: below it no orders), 40 and 90
                {
                    int ti = (mi * 2 + k) * 3 + m - 1;
                    Shape t = _satMark[ti];
                    if (t == null) { float an = SatAngle(_mode, munin, SatEdge[m]), grow = m == 1 ? 8f : 3f; t = _satMark[ti] = ShapeFor(an - 0.9f, an + 0.9f, SatIn - 1.5f * GS, SatOut + grow * GS, 0.5f, false, s); }
                    GUI.color = m == 1 ? SatRed : new Color(0.79f, 0.71f, 0.54f, 1f);
                    GUI.DrawTexture(Place(t, c, s), t.Fill);
                }
            }
            GUI.color = Color.white;
        }

        // the lit share of a part, kept one per raven (not in the shape cache: every level would stay there for good)
        private static readonly Shape[] _lit = new Shape[2];
        private static readonly Vector4[] _litKey = new Vector4[2]; // (mode, zone, level, scale): compared, not made into words each frame
        private Shape LitShape(int k, int zone, bool munin, float v0, float v1, float s)
        {
            Vector4 key = new Vector4(_mode, zone, v1, s);
            if (_lit[k] != null && _litKey[k] == key && Mathf.Abs(_cacheS - s) < 0.001f) return _lit[k];
            FreeLit(k);
            float a = SatAngle(_mode, munin, v0), b = SatAngle(_mode, munin, v1);
            _lit[k] = Bake(Mathf.Min(a, b), Mathf.Max(a, b), SatIn, SatOut, s, 1.5f, false);
            _litKey[k] = key;
            return _lit[k];
        }
        private static void FreeLit(int k) { if (_lit[k] != null && _lit[k].Fill != null) Destroy(_lit[k].Fill); _lit[k] = null; _litKey[k] = Vector4.zero; }

        // the ring of the time an order has left: a share of a thin ring from the top, clockwise (made once per step of 40)
        private static readonly Texture2D[] _timeRings = new Texture2D[41];
        private static Texture2D TimeRing(int step)
        {
            step = Mathf.Clamp(step, 0, 40);
            if (_timeRings[step] != null) return _timeRings[step];
            const int n = 128;
            float share = step / 40f, r1 = n * 0.5f - 1f, r0 = r1 - 5f;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // (rows go up: dy up is the top)
                    if (ang < 0f) ang += 360f;
                    float inR = Mathf.Min(r - r0, r1 - r);
                    float f = ang <= share * 360f ? Mathf.Clamp01(inR + 0.5f) : 0f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(f * 255f));
                }
            Texture2D t = NewTex(n, n);
            t.filterMode = FilterMode.Bilinear;
            t.SetPixels32(px); t.Apply(false, true);
            _timeRings[step] = t;
            return t;
        }


        // «Накази» in the gifts' centre: a round button that turns the plate to the orders
        private static void OrdersButton(Vector2 c, float s, bool hot)
        {
            float r = OrdersBtn * s, rl = r + 2f * GS * s;
            GUI.color = hot ? HoverLine : A(Gold, 0.85f);
            GUI.DrawTexture(new Rect(c.x - rl, c.y - rl, rl * 2f, rl * 2f), _disc);
            GUI.color = hot ? TileHot : new Color(0.07f, 0.05f, 0.03f, 0.96f);
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), _disc);
            GUI.color = Color.white;
            float sz = 34f * GS * s; // just the icon: what it does is said in the line under the circle
            GUI.DrawTexture(new Rect(c.x - sz * 0.5f, c.y - sz * 0.5f, sz, sz), Asset("act-circle"));
        }

        // the gifts' wings: a fan for each raven on his side, faint lines between the rows
        private void Wings(Vector2 c, float s)
        {
            for (int k = 0; k < 2; k++)
            {
                bool munin = k == 1, one = _mode != 0;
                if (_mode == 1 && munin || _mode == 2 && !munin) continue; // one raven: his wing only, over the middle
                List<Shape> sh = WingShapes(one, munin, s);
                Rect r = Place(sh[0], c, s);
                GUI.color = munin ? new Color(0.16f, 0.07f, 0.05f, 0.88f) : new Color(0.05f, 0.08f, 0.13f, 0.88f);
                GUI.DrawTexture(r, sh[0].Fill);
                GUI.color = A(munin ? MLine : HLine, 0.55f);
                GUI.DrawTexture(r, sh[0].Line);
                GUI.color = A(munin ? MLine : HLine, 0.18f);
                for (int i = 1; i < sh.Count; i++) GUI.DrawTexture(Place(sh[i], c, s), sh[i].Fill); // faint lines between the rows
            }
            GUI.color = Color.white;
        }

        // a wing (one: the half-circle of one raven) and the lines between its rows; made once per scale (the shape cache)
        // (kept here too, so drawing does not look them up by name every frame; forgotten with the shapes)
        private static readonly List<Shape>[] _wings = new List<Shape>[4];
        private static float _wingsS = -1f;
        private static List<Shape> WingShapes(bool one, bool munin, float s)
        {
            if (Mathf.Abs(_wingsS - s) > 0.001f || Mathf.Abs(_cacheS - s) > 0.001f) { for (int i = 0; i < 4; i++) _wings[i] = null; _wingsS = s; } // (FreeShapes also forgets them)
            int key = (one ? 2 : 0) + (munin ? 1 : 0);
            if (_wings[key] != null) return _wings[key];
            List<Shape> list = new List<Shape>();
            float a0 = one ? -FanA : munin ? -Wing1 : Wing0, a1 = one ? FanA : munin ? -Wing0 : Wing1;
            list.Add(ShapeFor(a0, a1, WingIn, WingOut, 14f * GS, true, s));
            _wings[key] = list;
            return list;
        }

        // a gift: a round button with its rune medallion. Hover: a light ring; golden runes: a small medallion with the level
        // (I–III) on the outer side (Hugin's right, Munin's left, the paired ones below), the third also breathes a glow;
        // its wait drains over it from the top
        private static void RoundGift(Piece pc, Vector2 c, float s, bool hot)
        {
            Vector2 at = c + Dir(pc.A0) * pc.R0 * s;
            float d = pc.CR * 2f * s;
            if (pc.Gold >= GiftUp.MaxLevel) // all three: it breathes a gold glow
            {
                float g = d * 1.95f; // (the soft disc fades fast: this wide, it still shows a rim of light round the icon)
                GUI.color = A(GoldGlow, 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.4f));
                GUI.DrawTexture(new Rect(at.x - g * 0.5f, at.y - g * 0.5f, g, g), _soft);
            }
            if (hot || pc.Now)
            {
                float rw = 3f * GS * s, rd = d + rw * 2f;
                GUI.color = hot ? HoverLine : Bright;
                GUI.DrawTexture(new Rect(at.x - rd * 0.5f, at.y - rd * 0.5f, rd, rd), _disc);
            }
            Rect r = new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d);
            GUI.color = pc.Usable || pc.Now ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
            DrawSprite(r, AbilityIcons.For(pc.Gift.Id, pc.Munin));
            if (!pc.Now && pc.CoolLeft > 0.5f && pc.CoolAll > 0f) Overlay(r, _disc, new Color(0f, 0f, 0f, 0.6f), Mathf.Clamp01(pc.CoolLeft / pc.CoolAll));
            if (!pc.Usable && !pc.Now) { GUI.color = new Color(0f, 0f, 0f, 0.3f); GUI.DrawTexture(r, _disc); }
            if (pc.Gold > 0) GoldBadge(at, d, pc.Gift != null && pc.Gift.Pair ? 0 : pc.Munin ? -1 : 1, pc.Gold, s);
            GUI.color = Color.white;
        }

        // the golden runes' level: a dark medallion with a gold rim and the numeral, on the gift's outer side
        // (side 1 right, -1 left, 0 below). Drawn from a texture made at its own pixel size, on whole pixels: the big
        // shared disc squeezed this small showed stairs on its edge.
        private static void GoldBadge(Vector2 at, float d, int side, int level, float s)
        {
            int bd = Mathf.RoundToInt(Mathf.Max(15f * GS * s, d * 0.4f));
            Vector2 dir = side == 0 ? new Vector2(0f, 1f) : new Vector2(side * 0.7071f, 0.7071f); // (screen y grows down)
            Vector2 bc = at + dir * (d * 0.5f * 0.86f);
            Rect r = new Rect(Mathf.Round(bc.x - bd * 0.5f), Mathf.Round(bc.y - bd * 0.5f), bd, bd);
            if (level >= GiftUp.MaxLevel) // the third: a small glow breathing behind the medallion too
            {
                float g = bd * 2.4f;
                GUI.color = A(GoldGlow, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.4f + 1.2f));
                GUI.DrawTexture(new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), _soft);
            }
            GUI.color = Color.white;
            GUI.DrawTexture(r, BadgeTex(bd, Mathf.Max(1.5f, 1.6f * GS * s)));
            Text(r.center, 0f, "<b>" + GiftUp.Roman(level) + "</b>", _title, Bright, bd * 0.52f, bd * 1.6f); // (GUI.color white: it tints the label too)
        }

        // marks on the strength half-rings at a quarter, half and three quarters (the fill rises straight up)
        private void PowerTicks(Vector2 c, float s)
        {
            // both ravens: each his own side; one raven: his whole ring, marked on both sides
            Shape[] ticks = TickShapes(_mode != 0, s);
            GUI.color = A(Gold, 0.7f);
            for (int side = 0; side < 2; side++) // 0 left (Munin), 1 right (Hugin)
            {
                if (_mode == 0 && _state[side == 0 ? 1 : 0] == null) continue; // that raven is not on you
                for (int q = 0; q < 3; q++) GUI.DrawTexture(Place(ticks[side * 3 + q], c, s), ticks[side * 3 + q].Fill);
            }
            GUI.color = Color.white;
        }

        // the marks: left three, then right three (one: on the whole ring)
        private static readonly Shape[] _ticks = new Shape[6];
        private static float _ticksS = -1f; private static bool _ticksOne;
        private static Shape[] TickShapes(bool one, float s)
        {
            if (Mathf.Abs(_ticksS - s) < 0.001f && Mathf.Abs(_cacheS - s) < 0.001f && _ticksOne == one) return _ticks;
            _ticksS = s; _ticksOne = one;
            float top = one ? 1f : Mathf.Cos(PowerA0 * Mathf.Deg2Rad);
            for (int side = 0; side < 2; side++)
                for (int q = 1; q <= 3; q++)
                {
                    float a = Mathf.Acos(-top * (1f - 2f * q / 4f)) * Mathf.Rad2Deg; // where the level of q quarters crosses the ring
                    if (side == 0) a = -a;
                    _ticks[side * 3 + q - 1] = ShapeFor(a - 0.8f, a + 0.8f, PowerOut - 5f * GS, PowerOut + 3f * GS, 0f, false, s);
                }
            return _ticks;
        }

        // under the middle (the user's reference): «Накази», or the gift under the pointer — its name (golden runes as gold
        // dots), and under it whose it is with its key or its wait
        private void CentreLabel(Vector2 c, float s)
        {
            Piece pc = _hover >= 0 && _hover < _pieces.Count && _pieces[_hover].Kind == Kind.Gift ? _pieces[_hover] : null;
            Vector2 at = c + new Vector2(0f, (DiscR + 16f * GS) * s); // (under the middle disc, between the wings)
            if (pc == null)
            {
                bool hot = _hover >= 0 && _hover < _pieces.Count && _pieces[_hover].Kind == Kind.ToOrders;
                Text(at, 0f, Lang.T("Накази"), _title, hot ? Bright : A(Gold, 0.9f), 17f * GS * s, 200f * GS * s);
                return;
            }
            if (pc.MidName == null) // made once per change of the piece (Build clears it), not every frame
            {
                pc.MidName = pc.Title + (pc.Gold > 0 ? " <color=#FFC23A>" + GiftUp.Roman(pc.Gold) + "</color>" : "");
                string extra = pc.CoolText ?? (pc.Now ? Lang.T("● зараз") : pc.KeyLabel != null && pc.KeyLabel != "—" ? "[" + pc.KeyLabel + "]" : null);
                pc.MidSub = "<color=#" + ColorUtility.ToHtmlStringRGB(pc.Munin ? MLine : HLine) + ">" + Plugin.RavenName(pc.Munin) + "</color>" + (extra != null ? "  " + extra : "");
            }
            Text(at, 0f, pc.MidName, _title, pc.Usable || pc.Now ? Ink : Dim, 17f * GS * s, 190f * GS * s);
            Text(at + new Vector2(0f, 19f * GS * s), 0f, pc.MidSub, _small, A(Gold, 0.85f), 11f * GS * s, 190f * GS * s);
        }

        // «Повернути» fills like a bowl: his strength, from the bottom
        private static void Bowl(Piece pc, Rect r, Shape sh, float alpha)
        {
            float frac = pc.Strength;
            if (frac <= 0f) return;
            Color liquid = pc.Munin ? new Color(0.55f, 0.12f, 0.1f, 0.5f) : new Color(0.16f, 0.36f, 0.58f, 0.5f);
            if (_g) liquid = pc.Munin ? new Color(0.78f, 0.25f, 0.18f, 0.95f) : new Color(0.3f, 0.58f, 0.86f, 0.95f); // the gifts' half-rings: bright, as the reference
            // the bowl's depth: the half's own height
            Overlay(r, sh.Fill, A(liquid, alpha), frac, true);
            float y = r.yMax - r.height * frac;
            GUI.BeginGroup(new Rect(r.x, y - 1f, r.width, 2.5f));
            GUI.color = A(pc.Munin ? MLine : HLine, 0.9f * alpha);
            GUI.DrawTexture(new Rect(0f, -(y - 1f - r.y), r.width, r.height), sh.Fill);
            GUI.EndGroup();
            GUI.color = Color.white;
        }

        // a share of a shape, from the bottom (fromBottom) or the top: the cooldown drains down, the bowl fills up
        private static void Overlay(Rect r, Texture2D tex, Color col, float frac, bool fromBottom = false)
        {
            float h = r.height * frac;
            float y = fromBottom ? r.yMax - h : r.y;
            GUI.BeginGroup(new Rect(r.x, y, r.width, h));
            GUI.color = col;
            GUI.DrawTexture(new Rect(0f, r.y - y, r.width, r.height), tex);
            GUI.EndGroup();
            GUI.color = Color.white;
        }

        private static Rect Place(Shape sh, Vector2 c, float s) { return new Rect(c.x + sh.Box.x * s, c.y + sh.Box.y * s, sh.Box.width * s, sh.Box.height * s); }

        // the info bar, the pointer's hint and the chips over the wings: the book's tip plates (its fonts, rounded,
        // framed), not IMGUI boxes; they lie under the circle's own drawing, so each sits clear of it
        private static readonly RavenBook.ScreenPlate BarPlate = new RavenBook.ScreenPlate("RavenWatch_CircleInfo", 30010);
        private static readonly RavenBook.ScreenPlate HintPlate = new RavenBook.ScreenPlate("RavenWatch_CircleHint", 30011);
        private static readonly RavenBook.ScreenPlate[] ChipPlates = { new RavenBook.ScreenPlate("RavenWatch_CircleChipH", 30009), new RavenBook.ScreenPlate("RavenWatch_CircleChipM", 30009) };
        // the chips' words: the name in a lighter tint of the raven's colour (for the dark plate), the state lit gold while he is away
        private const string ChipHugin = "#8EC0E8", ChipMunin = "#E0705A", ChipDot = "#8A7650", ChipAway = "#FFD98A";

        private static void InfoBar(Vector2 c, float s, string title, string desc)
        {
            // as tall as its words: a long title or description wraps onto more lines instead of being cut off
            float s0 = s;
            s *= GS; // (all of it GS larger, as the circle)
            float w = 600f * s;
            float h = BarPlate.Set(title, desc, w, s); // (laid out once per text, not on every GUI event)
            BarPlate.Place(c.x - w / 2f, Mathf.Max(4f, c.y - (ChipR + 18f * GS) * s0 - h)); // over the circle, above the chips (the tricks drop under it)
        }

        private static void Tip(Vector2 at, float s, string text)
        {
            float h = HintPlate.Set(null, text, 170f * s, s);
            HintPlate.Place(Mathf.Clamp(at.x, 4f, Screen.width - 174f * s), Mathf.Min(at.y, Screen.height - h - 4f));
        }

        private static string Clock(float sec) { int t = Mathf.CeilToInt(sec); return (t / 60) + ":" + (t % 60).ToString("00"); }

        // ---------------------------------------------------------------- feeding: a small popup over the centre

        private static Rect PopupRect(Vector2 c, float s)
        {
            // under the feed button (the centre disc's lower edge), never off the screen's bottom
            float w = 4f * 56f * s + 28f * s, h = 30f * s + 2f * 58f * s + 50f * s; // (the bottom: two lines, the food and what it gives)
            float y = Mathf.Min(c.y + Centre * s + 8f * s, Screen.height - h - 4f);
            return new Rect(c.x - w * 0.5f, y, w, h);
        }

        // the popup's food: worked out with the pieces (four times a second, and at once after feeding), not every frame;
        // what he eats now, those you have first
        private readonly List<Trust.Food> _foods = new List<Trust.Food>();
        private readonly List<int> _have = new List<int>();
        private readonly List<bool> _favKnown = new List<bool>();
        private readonly List<string> _foodLine = new List<string>();
        private readonly List<string> _haveText = new List<string>();
        private string _feedHead;

        private void FoodsNow(Player p)
        {
            bool munin = _feedFor == 1;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            _foods.Clear(); _have.Clear(); _favKnown.Clear(); _foodLine.Clear(); _haveText.Clear();
            if (item == null) { _feedFor = -1; return; }
            _feedHead = "<color=" + Satiety.ZoneHexOf(item) + ">" + Satiety.ZoneWord(item) + "</color> · " + Mathf.RoundToInt(Satiety.Value(item));
            List<Trust.Food> all = Trust.EdibleFoods(munin, item);
            for (int pass = 0; pass < 2; pass++) // those in the bag, then the rest (in the list's own order)
                foreach (Trust.Food fd in all)
                {
                    int n = Trust.Have(p, fd.Prefab);
                    if ((n > 0) != (pass == 0)) continue;
                    bool fav = Satiety.IsFav(item, fd.Prefab), known = Satiety.KnownFav(item, fd.Prefab);
                    _foods.Add(fd); _have.Add(n); _favKnown.Add(known); _haveText.Add("×" + n);
                    _foodLine.Add(Lang.F("<b>{0}{1}</b>\n<color=#BFB59A>+{2} ситості · +{3} довіри{4}</color>", fd.Name, (known ? " ★" : ""), Mathf.RoundToInt(Satiety.FillOf(fd, fav, item)), Satiety.Num(Mathf.Round(Satiety.TrustOf(item, fd, fav) * 10f) / 10f), (n <= 0 ? Lang.T(" · нема в сумці") : "")));
                }
        }

        private void FeedPopup(Rect r, float s, Vector2 mouse)
        {
            bool munin = _feedFor == 1;
            if (_feedHead == null) return;
            // the book's tip plate: rounded, a soft shadow under it, the raven's frame and a faint gold one inside
            float rad = 10f * s;
            RoundFill(new Rect(r.x + 2f * s, r.y + 4f * s, r.width, r.height), rad, new Color(0f, 0f, 0f, 0.45f));
            RoundFill(r, rad, RavenBook.TipFill);
            RoundLine(r, rad, A(munin ? MLine : HLine, 0.9f), Mathf.Max(1f, 1.5f * s));
            RoundLine(new Rect(r.x + 4f * s, r.y + 4f * s, r.width - 8f * s, r.height - 8f * s), rad - 3f * s, A(Gold, 0.22f), Mathf.Max(1f, s));
            float pad = 14f * s;
            GUI.color = A(Gold, 0.3f);
            GUI.DrawTexture(new Rect(r.x + pad, r.y + 27f * s, r.width - pad * 2f, Mathf.Max(1f, s)), Texture2D.whiteTexture); // (a rule under the head)
            GUI.color = Color.white;
            _title.alignment = TextAnchor.MiddleLeft;
            Label(new Rect(r.x + pad, r.y + 4f * s, r.width, 24f * s), Plugin.RavenName(munin), _title, munin ? MLine : HLine, 15f * s);
            _title.alignment = TextAnchor.MiddleCenter;
            _small.alignment = TextAnchor.MiddleRight;
            Label(new Rect(r.x, r.y + 4f * s, r.width - pad, 24f * s), _feedHead, _small, Dim, 12f * s);
            _small.alignment = TextAnchor.MiddleCenter;

            float cell = 52f * s, gap = 4f * s;
            Rect view = new Rect(r.x + pad, r.y + 30f * s, r.width - pad * 2f, 2f * (cell + gap) + 6f * s);
            int rows = Mathf.CeilToInt(_foods.Count / 4f);
            float full = rows * (cell + gap), maxScroll = Mathf.Max(0f, full - view.height + 6f * s);
            _feedScroll = Mathf.Clamp(_feedScroll, 0f, maxScroll);
            _feedHover = -1;
            GUI.BeginGroup(view);
            for (int i = 0; i < _foods.Count; i++)
            {
                Trust.Food f = _foods[i];
                Rect cr = new Rect((i % 4) * (cell + gap), (i / 4) * (cell + gap) - _feedScroll, cell, cell);
                if (cr.yMax < 0f || cr.y > view.height) continue;
                int have = _have[i];
                bool hot = view.Contains(mouse) && new Rect(view.x + cr.x, view.y + cr.y, cr.width, cr.height).Contains(mouse);
                if (hot) _feedHover = i;
                RoundFill(cr, 6f * s, hot ? new Color(0.22f, 0.18f, 0.12f, 1f) : new Color(0.13f, 0.11f, 0.08f, 1f));
                RoundLine(cr, 6f * s, _favKnown[i] ? Bright : hot ? HoverLine : new Color(0.35f, 0.3f, 0.22f), Mathf.Max(1f, s));
                GUI.color = have > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.55f);
                DrawSprite(new Rect(cr.x + 7f * s, cr.y + 5f * s, cell - 14f * s, cell - 14f * s), Icon(f.Prefab));
                GUI.color = Color.white;
                if (_favKnown[i]) Label(new Rect(cr.x + 2f * s, cr.y + 1f * s, 14f * s, 14f * s), "★", _small, Bright, 11f * s);
                _small.alignment = TextAnchor.LowerRight;
                Label(new Rect(cr.x, cr.y, cr.width - 3f * s, cr.height - 1f * s), _haveText[i], _small, have > 0 ? Ink : Dim, 10f * s);
                _small.alignment = TextAnchor.MiddleCenter;
            }
            GUI.EndGroup();
            if (maxScroll > 0f) // a thin bar: there is more below
            {
                float barH = view.height * view.height / (full + 6f * s);
                Rect bar = new Rect(view.xMax + 4f * s, view.y + (view.height - barH) * (_feedScroll / maxScroll), 3f * s, barH);
                RoundFill(bar, bar.width * 0.5f, A(Gold, 0.6f));
            }
            // the line at the bottom: the food under the pointer
            string line;
            if (_feedHover >= 0) line = _foodLine[_feedHover];
            else line = _foods.Count == 0 ? Lang.T("Зараз він нічого не їсть.") : Lang.T("<color=#BFB59A>Наведи на їжу.</color>");
            _info.alignment = TextAnchor.MiddleLeft;
            Label(new Rect(r.x + pad, r.yMax - 46f * s, r.width - pad * 2f, 42f * s), line, _info, Ink, 12f * s);
            // the pointer: a little notch under the popup, at the centre
            GUI.color = RavenBook.TipFill;
            GUI.DrawTexture(new Rect(r.center.x - 7f * s, r.yMax - 1f, 14f * s, 7f * s), _notch);
            GUI.color = Color.white;
        }

        private void FeedClick(int i)
        {
            Player p = Player.m_localPlayer;
            if (p == null || i < 0 || i >= _foods.Count) return;
            Trust.Food f = _foods[i];
            if (Trust.Have(p, f.Prefab) <= 0) return;
            string msg = Trust.Feed(p, _feedFor == 1, f);
            if (!string.IsNullOrEmpty(msg)) RavenMsg.Center(p, msg);
            _built = -1f; // the counts and the satiety anew, at once
            Build(p);
        }

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private static Sprite Icon(string prefab)
        {
            Sprite sp;
            if (Icons.TryGetValue(prefab, out sp)) return sp; // (a prefab with no icon: its null kept, not looked up every frame)
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
            sp = d != null && d.m_itemData.m_shared.m_icons != null && d.m_itemData.m_shared.m_icons.Length > 0 ? d.m_itemData.m_shared.m_icons[0] : null;
            Icons[prefab] = sp;
            return sp;
        }

        private static void DrawSprite(Rect r, Sprite sp)
        {
            if (sp == null || sp.texture == null) return;
            Rect t = sp.textureRect;
            Rect uv = new Rect(t.x / sp.texture.width, t.y / sp.texture.height, t.width / sp.texture.width, t.height / sp.texture.height);
            GUI.DrawTextureWithTexCoords(r, sp.texture, uv);
        }

        // ---------------------------------------------------------------- helpers

        // a direction on the screen: degrees clockwise from the top (y down)
        private static Vector2 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); }

        private static void Text(Vector2 at, float angle, string text, GUIStyle st, Color col, float size, float width)
        {
            Matrix4x4 keep = GUI.matrix;
            if (Mathf.Abs(angle) > 0.5f) GUIUtility.RotateAroundPivot(angle, at);
            Label(new Rect(at.x - width * 0.5f, at.y - size * 1.6f, width, size * 3.2f), text, st, col, size);
            GUI.matrix = keep;
        }

        private static void Label(Rect r, string text, GUIStyle st, Color col, float size)
        {
            st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            st.normal.textColor = col;
            GUI.Label(r, text, st);
        }

        private static void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, richText = true, clipping = TextClipping.Overflow };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true, clipping = TextClipping.Overflow };
            _info = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true };
        }

        // what is under the pointer (relative to the centre, screen pixels: y down): a piece, -2 the plate (nothing), -1 outside (closes)
        private int HitTest(Vector2 d, float s)
        {
            float r = d.magnitude / s;
            if (r > RimNow) return -1;
            float ang = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece pc = _pieces[i];
                if ((pc.Kind == Kind.Trick || pc.Kind == Kind.Chase) && !_tricksOpen) continue; // (not dropped: not there)
                if (pc.Hidden) continue;
                if (pc.CR > 0f) { if ((d / s - Dir(pc.A0) * pc.R0).magnitude <= pc.CR + 3f) return i; continue; } // a round button
                if (pc.Kind == Kind.Power) continue;
                if (r < pc.R0 || r > pc.R1) continue;
                float mid = (pc.A0 + pc.A1) * 0.5f, half = (pc.A1 - pc.A0) * 0.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(mid, ang)) <= half) return i;
            }
            return -2;
        }

        // ---------------------------------------------------------------- the pictures

        private static readonly Dictionary<string, Texture2D> Assets = new Dictionary<string, Texture2D>();
        private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });

        // a button frame's picture name, made once (asked for every button every frame)
        private static readonly Dictionary<string, string> FrameRing = new Dictionary<string, string>(), FramePlain = new Dictionary<string, string>();
        private static string FrameName(bool runes, string colour)
        {
            Dictionary<string, string> d = runes ? FrameRing : FramePlain;
            string n;
            if (!d.TryGetValue(colour, out n)) d[colour] = n = (runes ? "f-ring-" : "f-plain-") + colour;
            return n;
        }

        // the circles' pictures (frames f-*, icons o-*) for other round menus of ours (the NPC menu): one look
        internal static Texture2D Pic(string name) { return Asset(name); }
        // the same for a sprite or a RawImage: null when the build has no such picture (Pic gives IMGUI the black 4×4 then, which a check by size took for a picture)
        internal static Texture2D PicOrNone(string name) { Texture2D t = Asset(name); return t == Texture2D.blackTexture ? null : t; }

        private static Texture2D Asset(string name)
        {
            Texture2D t;
            if (Assets.TryGetValue(name, out t)) return t ?? Texture2D.blackTexture;
            try
            {
                using (Stream st = typeof(OrdersMenu).Assembly.GetManifestResourceStream("RavenWatch.menu." + name + ".png"))
                {
                    if (st != null && LoadImage != null)
                    {
                        byte[] data = new byte[st.Length];
                        st.Read(data, 0, data.Length);
                        t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear;
                        if (!(bool)LoadImage.Invoke(null, new object[] { t, data })) { Destroy(t); t = null; }
                        else t.Apply(true, true); // on the graphics card only: no second copy kept in memory
                    }
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Orders menu picture " + name + ": " + e.Message); t = null; }
            Assets[name] = t;
            return t ?? Texture2D.blackTexture;
        }

        // the shapes: a fill, its edge (thin and thick) and a soft glow, each only as big as the piece
        private sealed class Shape { internal Rect Box; internal Texture2D Fill, Line; }
        private static Texture2D _disc, _notch, _soft, _pointer;

        // the boxes (the feeding popup and its cells): the GUI's own rounded drawing, no picture, so always crisp
        private static void RoundFill(Rect r, float radius, Color col)
        {
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, Mathf.Max(0f, radius));
        }
        private static void RoundLine(Rect r, float radius, Color col, float width)
        {
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, width, Mathf.Max(0f, radius));
        }

        // Every shape is made once for the screen's scale and kept by its geometry: the three layouts share most of them,
        // a raven getting on or off the shoulder makes nothing new, and a new scale (another resolution) frees them all.
        private static readonly Dictionary<string, Shape> Cache = new Dictionary<string, Shape>();
        private static float _cacheS = -1f;

        private static Shape ShapeFor(float a0, float a1, float r0, float r1, float round, bool outlined, float s)
        {
            if (Mathf.Abs(_cacheS - s) > 0.001f) { FreeShapes(); _cacheS = s; }
            string key = a0.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + a1.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + r0.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + r1.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + round + (outlined ? "|o" : "");
            Shape sh;
            if (!Cache.TryGetValue(key, out sh)) { sh = Bake(a0, a1, r0, r1, s, round, outlined); Cache[key] = sh; }
            return sh;
        }

        // how round a piece's corners are (reference px): the tricks and the strength half-rings less
        private static float Corner(Kind k) { return k == Kind.Power ? 2f : 10f; }

        private static Shape PieceShape(Piece pc, float s)
        {
            if (pc.Shape == null || Mathf.Abs(pc.ShapeS - s) > 0.001f || Mathf.Abs(_cacheS - s) > 0.001f)
            {
                pc.Shape = ShapeFor(pc.A0, pc.A1, pc.R0, pc.R1, Corner(pc.Kind), true, s);
                pc.ShapeS = s;
            }
            return pc.Shape;
        }

        // the round discs and the popup's notch: also once per scale
        private static void Plain(float s)
        {
            if (Mathf.Abs(_cacheS - s) > 0.001f) { FreeShapes(); _cacheS = s; }
            if (_disc == null) _disc = Disc(Mathf.CeilToInt(Centre * 2f * s));
            if (_notch == null) _notch = Notch(16, 8);
            if (_pointer == null) _pointer = Pointer(36, 26);
            if (_soft == null) _soft = SoftDisc(128);
        }

        private static void FreeShapes()
        {
            foreach (Shape sh in Cache.Values) foreach (Texture2D t in new[] { sh.Fill, sh.Line }) if (t != null) Destroy(t);
            Cache.Clear();
            foreach (Texture2D t in new[] { _disc, _notch, _soft, _pointer }) if (t != null) Destroy(t);
            _disc = _notch = _soft = _pointer = null;
            foreach (Texture2D t in Badges.Values) if (t != null) Destroy(t);
            Badges.Clear();
            FreeLit(0); FreeLit(1);
            _wingsS = _ticksS = _satS = -1f;
        }

        // the golden runes' medallion at one pixel size: a gold rim round a dark middle, both edges smoothed over a pixel
        private static readonly Dictionary<int, Texture2D> Badges = new Dictionary<int, Texture2D>();
        private static Texture2D BadgeTex(int size, float rim)
        {
            Texture2D t;
            if (Badges.TryGetValue(size, out t) && t != null) return t;
            t = NewTex(size, size);
            t.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            Color dark = new Color(0.13f, 0.09f, 0.05f, 0.97f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(half - r);         // 1 inside, 0 outside, a pixel of blend
                    float inner = Mathf.Clamp01(half - rim - r);   // the dark middle
                    Color c = Color.Lerp(Gold, dark, inner);
                    c.a = outer * Mathf.Lerp(1f, dark.a, inner);
                    px[y * size + x] = c;
                }
            t.SetPixels32(px); t.Apply(false, true);
            Badges[size] = t;
            return t;
        }

        internal static float ScaleNow { get { return Mathf.Round(Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f) * 20f) / 20f; } } // in 5% steps: a window being resized does not rebuild the pictures every frame

        // Made ahead, a few a frame, once the hero is in the world: the first J then opens at once (making them all at
        // the moment of opening took a long frame). All three layouts, the hunger arcs, the rim, the runes, the pictures.
        internal static void Warm()
        {
            if (_inst == null)
            {
                GameObject go = new GameObject("RavenWatch_OrdersMenu");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<OrdersMenu>();
                _inst.enabled = false;
            }
            if (!_inst._warming) _inst.StartCoroutine(_inst.WarmUp());
        }

        private bool _warming;

        private System.Collections.IEnumerator WarmUp()
        {
            _warming = true;
            try
            {
                yield return null;
                float s = ScaleNow;
                Plain(s);
                yield return null;
                List<Piece> list = new List<Piece>();
                int made = 0;
                List<RavenAbility> gh = new List<RavenAbility>(), gm = new List<RavenAbility>();
                GiftsOf(Player.m_localPlayer, false, gh); GiftsOf(Player.m_localPlayer, true, gm); // the gifts open now (more later: made then)
                for (int k = 0; k <= 5; k++)
                {
                    int mode = k % 3;
                    if (k < 3) Pieces(mode, list); else GiftPieces(mode, gh, gm, list);
                    foreach (Piece pc in list)
                    {
                        if (pc.Kind == Kind.ToOrders || pc.CR > 0f) continue; // a plain disc, a round gift
                        int before = Cache.Count;
                        ShapeFor(pc.A0, pc.A1, pc.R0, pc.R1, Corner(pc.Kind), true, s);
                        if (Cache.Count != before && ++made % 3 == 0) yield return null; // a few a frame
                    }
                }
                foreach (bool one in new[] { false, true }) // the gifts' wings, their row lines and the strength marks
                {
                    for (int k = 0; k < (one ? 1 : 2); k++) { WingShapes(one, k == 1, s); yield return null; }
                    TickShapes(one, s);
                }
                foreach (int mode in new[] { 0, 1 }) // the satiety's faint parts (the lit share is made as it goes)
                {
                    foreach (bool mu in new[] { false, true })
                        for (int z = 0; z < 4; z++) SatShape(mode, mu, SatEdge[z] + 1f, SatEdge[z + 1] - 1f, s);
                    yield return null;
                }
                ShapeFor(150f, 210f, OneBand0, OneBand1, 14f * GS, true, s); ShapeFor(-TwoBandA1, -TwoBandA0, TwoBand0, TwoBand1, 14f * GS, true, s); ShapeFor(TwoBandA0, TwoBandA1, TwoBand0, TwoBand1, 14f * GS, true, s);
                yield return null;
                foreach (int o in Orders.Tricks) Rune(o);
                yield return null;
                foreach (string a in new[] { "scout", "near", "circle", "perch", "here", "bag", "meat", "berries", "recall", "chase", "down", "up" }) Asset("o-" + a);
                foreach (string f in new[] { "ring", "plain" }) foreach (string col in new[] { "red", "blue", "green", "grey", "dark" }) Asset("f-" + f + "-" + col);
                Asset("act-circle");
                yield return null;
                TimeRing(40);
            }
            finally { _warming = false; }
        }

        private static Texture2D NewTex(int w, int h)
        {
            Texture2D t = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // a ring sector with rounded corners, white (tinted when drawn), only its own box
        private static Shape Bake(float a0, float a1, float r0, float r1, float s, float round, bool outlined)
        {
            const float gap = 2f; // between neighbours, reference pixels
            // its box: the corners and points along both arcs
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (float a = a0; a <= a1 + 0.01f; a += Mathf.Max(0.5f, (a1 - a0) / 24f))
                foreach (float rr in new[] { r0, r1 })
                {
                    Vector2 v = Dir(a) * rr;
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
            minX -= 3f; minY -= 3f; maxX += 3f; maxY += 3f;
            Shape sh = new Shape { Box = new Rect(minX, minY, maxX - minX, maxY - minY) };
            int w = Mathf.CeilToInt(sh.Box.width * s), h = Mathf.CeilToInt(sh.Box.height * s);
            Color32[] fill = new Color32[w * h], line = outlined ? new Color32[w * h] : null;
            float mid = (a0 + a1) * 0.5f, half = (a1 - a0) * 0.5f;
            float rad = round * s, R0 = r0 * s, R1 = r1 * s, g = gap * s * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // the pixel in reference units round the centre (texture rows go up: y flipped to the screen)
                    float px = minX * s + x + 0.5f, py = minY * s + (h - 1 - y) + 0.5f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float ang = Mathf.Atan2(px, -py) * Mathf.Rad2Deg;
                    float inR = Mathf.Min(r - R0, R1 - r) - g;
                    float inA = (half - Mathf.Abs(Mathf.DeltaAngle(mid, ang))) * Mathf.Deg2Rad * r - g;
                    float d;
                    if (inR < rad && inA < rad && rad > 0f) d = rad - Mathf.Sqrt((rad - inR) * (rad - inR) + (rad - inA) * (rad - inA));
                    else d = Mathf.Min(inR, inA);
                    int i = y * w + x;
                    float af = Mathf.Clamp01(d + 0.5f);
                    fill[i] = new Color32(255, 255, 255, (byte)(af * 255f));
                    if (!outlined) continue;
                    float al = af * Mathf.Clamp01(1.3f * s - d + 0.5f);
                    line[i] = new Color32(255, 255, 255, (byte)(al * 255f));
                }
            sh.Fill = NewTex(w, h); sh.Fill.SetPixels32(fill); sh.Fill.Apply(false, true);
            if (outlined) { sh.Line = NewTex(w, h); sh.Line.SetPixels32(line); sh.Line.Apply(false, true); }
            return sh;
        }

        private static Texture2D Disc(int size)
        {
            Texture2D t = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = half - 1f - Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // a soft round glow: full in the middle, fading to nothing at the edge (the golden runes' halo)
        private static Texture2D SoftDisc(int size)
        {
            Texture2D t = NewTex(size, size);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / half);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(f * f * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // an arrowhead pointing up, its back cut in (a chevron that still reads as one shape), soft-edged
        private static Texture2D Pointer(int w, int h)
        {
            Texture2D t = NewTex(w, h);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fromTop = h - 1 - y + 0.5f, side = Mathf.Abs(x + 0.5f - w * 0.5f);
                    float edge = (w * 0.5f) * (fromTop / h);                 // the sides: wider going down
                    float back = h - (w * 0.5f - side) * (h * 0.45f) / (w * 0.5f); // the back: cut in towards the middle
                    float a = Mathf.Clamp01(edge - side + 0.5f) * Mathf.Clamp01(back - fromTop + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        private static Texture2D Notch(int w, int h)
        {
            Texture2D t = NewTex(w, h);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fromTop = h - 1 - y + 0.5f, halfW = (w * 0.5f) * (1f - fromTop / h);
                    float a = Mathf.Clamp01(halfW - Mathf.Abs(x + 0.5f - w * 0.5f) + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // ---------------------------------------------------------------- the runes, drawn here as strokes

        // each trick's rune: line strokes in a unit square (x right, y down)
        private static readonly Dictionary<int, float[][]> RuneLines = new Dictionary<int, float[][]>
        {
            { Orders.Roll,      new[] { new[] { .32f, .1f, .32f, .9f }, new[] { .32f, .1f, .7f, .3f, .32f, .5f, .7f, .7f, .32f, .9f } } },  // ᛒ
            { Orders.Loop,      new[] { new[] { .5f, .08f, .76f, .36f, .5f, .62f, .24f, .36f, .5f, .08f }, new[] { .36f, .48f, .74f, .92f }, new[] { .64f, .48f, .26f, .92f } } }, // ᛟ
            { Orders.Dive,      new[] { new[] { .5f, .1f, .5f, .9f }, new[] { .22f, .6f, .5f, .9f, .78f, .6f } } },                      // ᛏ upside down
            { Orders.Eight,     new[] { new[] { .2f, .15f, .2f, .85f }, new[] { .8f, .15f, .8f, .85f }, new[] { .2f, .15f, .8f, .85f }, new[] { .8f, .15f, .2f, .85f } } }, // ᛞ
            { Orders.Candle,    new[] { new[] { .5f, .1f, .5f, .9f }, new[] { .22f, .4f, .5f, .1f, .78f, .4f } } },                      // ᛏ
            { Orders.Leaf,      new[] { new[] { .36f, .1f, .36f, .9f }, new[] { .36f, .1f, .72f, .36f } } },                             // ᛚ
            { Orders.Immelmann, new[] { new[] { .3f, .1f, .3f, .9f }, new[] { .3f, .1f, .7f, .3f, .3f, .5f, .7f, .9f } } },             // ᚱ
            { Orders.Hammer,    new[] { new[] { .32f, .1f, .32f, .9f }, new[] { .32f, .28f, .72f, .5f, .32f, .72f } } },                 // ᚦ
            { Orders.Skim,      new[] { new[] { .2f, .1f, .2f, .9f }, new[] { .8f, .1f, .8f, .9f }, new[] { .2f, .1f, .5f, .42f, .8f, .1f } } }, // ᛖ
            { Orders.Waves,     new[] { new[] { .66f, .1f, .3f, .4f, .7f, .6f, .34f, .9f } } },                                          // ᛋ
        };
        private static readonly Dictionary<int, Texture2D> Runes = new Dictionary<int, Texture2D>();

        private static Texture2D Rune(int order)
        {
            Texture2D t;
            if (Runes.TryGetValue(order, out t) && t != null) return t;
            const int n = 64;
            float w = 4.2f; // the stroke, pixels of 64
            Color32[] px = new Color32[n * n];
            float[][] lines;
            RuneLines.TryGetValue(order, out lines);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float qx = (x + 0.5f) / n, qy = 1f - (y + 0.5f) / n, best = 9f;
                    if (lines != null)
                        foreach (float[] l in lines)
                            for (int k = 0; k + 3 < l.Length; k += 2) best = Mathf.Min(best, Seg(qx, qy, l[k], l[k + 1], l[k + 2], l[k + 3]));
                    float a = Mathf.Clamp01((w * 0.5f / n - best) * n + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t = NewTex(n, n);
            t.filterMode = FilterMode.Bilinear;
            t.SetPixels32(px); t.Apply(false, true);
            Runes[order] = t;
            return t;
        }

        private static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
            float k = Mathf.Clamp01((wx * vx + wy * vy) / Mathf.Max(1e-6f, vx * vx + vy * vy));
            float dx = wx - vx * k, dy = wy - vy * k;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }

    // the menu's shapes made ahead, once the hero is in the world (OrdersMenu.Warm)
    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class OrdersMenuWarmPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            try { OrdersMenu.Warm(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Orders menu warm-up: " + e.Message); }
        }
    }

    // While the orders menu is open (and the frame it closes, and while the click that closed it is held) the game
    // treats it like the trader's window: the cursor shows, the hero and the camera stand still, Esc is ours
    [HarmonyPatch(typeof(StoreGui), "IsVisible")]
    internal static class OrdersMenuAsWindowPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (OrdersMenu.Shields) __result = true;
#if RAVEN_NPC
            if (NpcMenu.Shields) __result = true; // (the NPC's round menu: the same shield)
#endif
        }
    }

    // The trader's-window trick does not stop a mouse swing: the game hands the hero its attack, second attack and block
    // in Player.SetControls (read from the buttons before). While the menu shields the game they are taken away there.
    [HarmonyPatch(typeof(Player), "SetControls")]
    internal static class OrdersMenuNoSwingPatch
    {
        private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            bool shield = OrdersMenu.Shields;
#if RAVEN_NPC
            shield |= NpcMenu.Shields;
            if (!shield && __instance == Player.m_localPlayer && NpcMenu.HoldsBlock) block = blockHold = false; // (the right click that drops a place being awaited: no block, all else goes on)
#endif
            if (!shield || __instance != Player.m_localPlayer) return;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }
    }
}
#endif

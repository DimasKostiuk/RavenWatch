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

        private enum Kind { Order, Pouch, Trick, Chase, Recall, Feed, Gift, Power, ToOrders, Here, Toggle, Bind, BindFire }

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
            internal bool Sits;               // the raven sits where this button sent it (a perch, a spot): filled with his colour
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
        private void Close() { _open = false; _key = KeyCode.None; _bindId = null; _feedFor = -1; ClosedFrame = Time.frameCount; ClickHeld = MouseHeld(); enabled = false; } // closed: no OnGUI every frame

        private void Awake() { useGUILayout = false; } // drawn by hand: no layout pass

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

        private void Update()
        {
            if (!_open) return;
            Player p = Player.m_localPlayer;
            if (_fromRadial) { if (Hud.InRadial() && Time.unscaledTime - _radialAt < 2f) return; _fromRadial = false; }
            if (p == null || p.IsDead() || RavenBook.IsOpen || Hud.InRadial() || InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || global::Console.IsVisible()) { Close(); return; }
            if (_bindId != null) { Capture(); return; }
            if (RavenKeys.Down(KeyCode.Escape) || OldEsc()) { if (_feedFor >= 0) _feedFor = -1; else if (_tricksOpen) { _tricksOpen = false; _built = -1f; } else Close(); return; }
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
                _bindName = (pc.Kind == Kind.Trick || pc.Kind == Kind.Order || pc.Kind == Kind.Here || pc.Kind == Kind.Gift ? Plugin.RavenName(pc.Munin) + ": " : "") + pc.Title;
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
            { "wings", new Vector3(1, 46.5f, 84f) }, { "heal", new Vector3(1, 73.8f, 84f) }, { "ward", new Vector3(1, 101.2f, 84f) }, { "mberserk", new Vector3(1, 128.5f, 84f) },
        };
        private static bool _g; // the face drawn now is the gifts' one
        private float RimNow { get { return !_g && _tricksOpen ? 500f * GS : GRim1; } } // (the tricks dropped: their band is ours too)

        // the orders on a wing: Облетіти, Поруч, Кружляй, Жердина, Туди, Торба — each its angle (Hugin's side; Munin's the
        // mirror) and row (0 by the middle, 1 out), the rows staggered so the wing is filled evenly
        private static readonly float[] TwoAt = { 56f, 86f, 116f, 46.5f, 88f, 128f }, OneAt = { 0f, -54f, 0f, 54f, 62f, -62f };
        private static readonly int[] TwoRow = { 0, 0, 0, 1, 1, 1 }, OneRow = { 1, 0, 0, 0, 1, 1 };
        // the tricks' band under the circle: two ravens — each his part of it, one — the band in the middle
        private const float TwoBand0 = 322f * GS, TwoBand1 = 446f * GS, OneBand0 = 190f * GS, OneBand1 = 322f * GS;
        private const float ToggleTwo = 232f * GS, ToggleOne = 150f * GS, ToggleD = 44f, ChaseR = 470f * GS, ChaseD = 50f;
        private bool _tricksOpen;
        private float _tricksAt;

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
            foreach (RavenAbility a in RavenAbilities.All)
                if (a.Munin == munin && !a.Passive && !RavenAbilities.Stub(a) && RavenAbilities.Shown(a)) into.Add(a);
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
            // the в'язь under the middle: its switch with the charges; a button-в'язь (only Бальдр) its own press beside it
            if (BindRun.Power != null)
            {
                bool button = BindRun.Power.Mode == BindMode.Button;
                Round(into, Kind.Bind, false, 0, button ? 196f : 180f, BindR, BindD);
                if (button) Round(into, Kind.BindFire, false, 0, 164f, BindR, BindD);
            }
        }

        private const float BindR = 96f, BindD = 34f;

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

        // a raven's gifts as round buttons in his wing, each at its fixed place (GiftPlace)
        private static void Wing(List<Piece> into, bool munin, List<RavenAbility> gifts)
        {
            float side = munin ? -1f : 1f;
            foreach (RavenAbility a in gifts)
            {
                Vector3 at;
                if (!GiftPlace.TryGetValue(a.Id, out at)) continue; // (a gift with no place yet: not on the circle)
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
            if (!one) { Half(into, false, 1f); Half(into, true, -1f); }
            else Half(into, mode == 2, 0f);
            if (!one) Round(into, Kind.Chase, false, Orders.Chase, 180f, ChaseR, ChaseD); // in the tricks' band, for both
            Round(into, Kind.Toggle, false, 0, 180f, one ? ToggleOne : ToggleTwo, ToggleD);
            Round(into, Kind.Recall, false, 0, 0f, 0f, OrdersBtn * 2f / GS);
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
                    float a = one ? 156f + 12f * i : 138f + 9f * i;
                    float rad = (one ? (r == 0 ? 224f : 290f) : (r == 0 ? 352f : 414f)) * GS;
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
            foreach (Piece pc in _pieces)
            {
                pc.Usable = true; pc.Now = pc.Glow = pc.Stays = pc.Sits = false; pc.CoolLeft = 0f; pc.Info = pc.MidName = null; // (the action stays: made once)
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
                    {
                        string why = Orders.WhyBoth(p, Orders.Chase);
                        pc.Title = Lang.T("Догонялки"); pc.Sub = why ?? Lang.F("обидва, кожному: {0}", Orders.PriceText(false, Orders.Chase));
                        pc.Now = Orders.Of(p, false) == Orders.Chase;
                        pc.Desc = Orders.Desc[Orders.Chase] + (pc.Now ? Lang.T("\nЩе клік — скасувати, і обидва повернуться на плечі.") : "");
                        pc.Usable = why == null || pc.Now;
                        pc.CoolLeft = Mathf.Max(Orders.LeftOf(false, Orders.Chase), Orders.LeftOf(true, Orders.Chase)); pc.CoolAll = Orders.WaitOf(Orders.Chase);
                        if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me == null) return; if (Orders.Of(me, false) == Orders.Chase) Orders.ReturnBoth(me); else Orders.GiveBoth(me, Orders.Chase); };
                        break;
                    }
                    case Kind.Gift:
                    {
                        RavenAbility a = pc.Gift;
                        pc.Title = GiftTitle(a); pc.Sub = RavenSkills.Status(a); pc.Desc = a.Short;
                        pc.Usable = RavenSkills.UsableNow(a);
                        pc.Now = (a.Id == "light" && RavenLight.On(p)) || (a.Id == "dome" && Dome.Mine()); // pressed again: it ends
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
                        pc.Desc = Lang.F("Відкрити торбу поруч із сумкою: поклади їжу (до {0} шматків). Голодний ворон поїсть із неї сам.", RavenPouch.Max);
                        pc.Usable = has;
                        if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; string why = RavenPouch.Open(me, mm); if (why != null && me != null) RavenMsg.Center(me, why); };
                        break;
                    }
                    case Kind.Recall:
                    {
                        ItemDrop.ItemData ih = RavenSeat.On(p, false), im = RavenSeat.On(p, true);
                        bool away = (ih != null && StateOf(p, false, ih) != 0) || (im != null && StateOf(p, true, im) != 0);
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
                        pc.Desc = BindRun.Plain + "\n" + Lang.T("Вимкнена в'язь не спрацьовує й не відновлює заряди.") + (BindRun.Active ? "" : "\n" + Lang.T("Ворон із в'яззю не на плечі — вона не діє."));
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
                else if (pc.Kind == Kind.BindFire) pc.Label = Lang.T("сила");
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
                _chip[i] = it != null ? "<b>" + Plugin.RavenName(m) + "</b>  ·  " + _state[i] : null;
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

        private static void PerchPiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin, on = item != null && Perches.On(item), spot = on && Perches.OnSpot(item);
            pc.Title = Lang.T("Жердина");
            pc.Sub = spot ? (Grammar.Female(p) ? Lang.T("сидить, де ти показала · покликати") : Lang.T("сидить, де ти показав · покликати")) : on ? Lang.F("● зараз · {0}", PerchDay(item)) : Perches.OrderSub(p, mm);
            pc.Desc = spot ? Lang.T("Кличе його звідти, де він сидить: злетить і прилетить до тебе.")
                    : on ? Lang.T("Кличе його з жердини: він злетить і прилетить до тебе, хоч би й здалека.")
                         : Lang.F("Шукає найближчу жердину з вільним місцем до {0} м, яку видно (не за стіною), і сідає на неї відпочити й набратися сили.", Mathf.RoundToInt(Perches.FindRadius));
            pc.Now = pc.Sits = on && !spot; // (on a spot: «Туди» shows it)
            pc.KeyId = mm ? RavenKeys.PerchMId : RavenKeys.PerchHId;
            pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId);
            ItemDrop.ItemData it;
            pc.Usable = on || Perches.WhyNot(p, mm, out it) == null; // on it: call back; else only when he may go (asleep, flying in, busy: not)
            if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me != null) Perches.Order(me, mm); };
        }

        // «Туди», the context order (SitHere): says what it will do where you look, before you press it
        private static void HerePiece(Player p, Piece pc, ItemDrop.ItemData item)
        {
            bool mm = pc.Munin, on = item != null && Perches.On(item), spot = on && Perches.OnSpot(item);
            pc.KeyId = mm ? RavenKeys.HereMId : RavenKeys.HereHId;
            pc.KeyLabel = RavenKeys.LabelOf(pc.KeyId);
            if (pc.Act == null) pc.Act = () => { Player me = Player.m_localPlayer; if (me != null) Perches.OrderHere(me, mm); };
            string moveBlock = null;
            bool move = on && p != null && Perches.MovesTo(p, mm, item, SitHere.Look(p, mm), out moveBlock); // looking at another place: it moves there
            if (on && !move)
            {
                pc.Title = spot ? Lang.T("Сісти тут") : Lang.T("Туди");
                pc.Sub = spot ? Lang.T("● сидить · покликати") : Perches.InNest(item) ? Lang.T("сидить у гнізді · покликати") : Lang.T("сидить на жердині · покликати");
                pc.Desc = Lang.T("Кличе його звідти, де він сидить: злетить і прилетить до тебе.");
                pc.Now = pc.Sits = spot;
                return;
            }
            if (p != null && Orders.Of(p, mm) == Orders.Fetch)
            {
                pc.Title = Lang.T("Принести"); pc.Sub = Lang.T("● летить по здобич"); pc.Desc = Orders.Desc[Orders.Fetch]; pc.Now = true; pc.Usable = false;
                return;
            }
            SitHere.Target t = p != null ? SitHere.Look(p, mm) : null;
            ItemDrop.ItemData it;
            string busy = move ? moveBlock : p != null ? Perches.WhyNot(p, mm, out it) : null;
            if (t != null && t.Player != null)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("На гравця");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), SitHere.PriceText(mm));
                pc.Desc = Lang.T("Сідає на голову або на вільне плече гравця, на якого ти дивишся (куди саме — навмання з вільних місць), і сидить там, доки не повернеш. Гравець може струсити його перекатом.");
                pc.Usable = why == null;
            }
            else if (t != null && t.Animal != null)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("На тварину");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), SitHere.PriceText(mm));
                pc.Desc = Lang.T("Сідає на ручну тварину, на яку ти дивишся: на спину (якщо ніхто не їде верхи) або на голову — навмання з вільних місць, і їде на ній, доки не повернеш. На диких не сідає.");
                pc.Usable = why == null;
            }
            else if (t != null && t.Item != null)
            {
                string why = t.Why ?? (RavenFetch.Carrying(mm) ? Lang.T("ще несе попереднє") : null) ?? Orders.Why(p, mm, Orders.Fetch);
                pc.Title = Lang.T("Принести");
                pc.Sub = why != null ? why.TrimEnd('.').Replace("\n", " ") : Lang.F("{0} · {1} м · {2}", t.Name, Mathf.RoundToInt(t.Dist), Orders.PriceText(mm, Orders.Fetch));
                pc.Desc = Lang.F("Летить до дрібниці, на яку ти дивишся, хапає одну штуку й приносить тобі в сумку. Лише легке — до {0} кг за штуку.", Satiety.Num(RavenFetch.MaxWeight));
                pc.Usable = why == null;
                pc.CoolLeft = Orders.LeftOf(mm, Orders.Fetch); pc.CoolAll = Orders.WaitOf(Orders.Fetch);
            }
            else if (t != null && t.Perch != null)
            {
                pc.Title = Lang.T("На цю жердину");
                bool room = t.Perch.FreeSeat() >= 0;
                pc.Sub = busy != null ? busy.TrimEnd('.').Replace("\n", " ") : !room ? Lang.T("на ній нема місця") : Lang.T("сісти на неї · безкоштовно");
                pc.Desc = Lang.T("Сідає саме на ту жердину, на яку ти дивишся, — відпочити й набратися сили.");
                pc.Usable = busy == null && room;
            }
            else if (t != null && t.Building)
            {
                string why = busy ?? t.Why ?? SitHere.WhyNotNow(p, mm);
                pc.Title = Lang.T("Сісти тут");
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
                if (pc.Usable) pc.Sub = Lang.F("пересяде · {0}", pc.Sub);
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
            if (Perches.OnSpot(item)) return (Grammar.Female(p) ? Lang.T("сидить, де ти показала") : Lang.T("сидить, де ти показав"));
            if (Perches.On(item)) return Perches.InNest(item) ? Lang.F("у гнізді · {0}", PerchDay(item)) : Lang.F("на жердині · {0}", PerchDay(item));
            int o = Orders.Of(p, munin);
            if (o == Orders.Follow) return Lang.T("в польоті · поруч");
            if (o == Orders.Circle) return Lang.T("в польоті · кружляє");
            if (o == Orders.Scout) return Lang.T("в польоті · облітає");
            if (o == Orders.Chase) return Lang.T("в польоті · догонялки");
            if (o != Orders.None) return Lang.F("в польоті · {0}", Lang.Lower(Short(o)));
            if (Trust.Away(item)) return Trust.AwayWord(munin, item);
            return Perks.FlightWord(p, munin); // (a gift, an outing, scouting: in the air though no order)
        }

        // 0 on the shoulder, 1 on a perch, 2 flying
        private static int StateOf(Player p, bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return 0;
            if (Perches.On(item)) return 1;
            if (Orders.Of(p, munin) != Orders.None || Trust.Away(item) || (!munin && Shoulder.HuginAway)) return 2;
            return 0;
        }

        private void Pick(int i)
        {
            if (i < 0 || i >= _pieces.Count) return;
            Piece pc = _pieces[i];
            if (pc.Act == null) return;
            // not now: the menu stays; a gift still says why in the middle of the screen (RavenSkills.Use names every
            // refusal): a click that did nothing at all looked broken
            if (!pc.Usable) { if (pc.Kind == Kind.Gift && pc.Gift != null) RavenSkills.Use(pc.Gift); else if (pc.Kind == Kind.BindFire) pc.Act(); return; } // (the в'язь's press says why not too)
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
        private static float _s = -1f;

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
            _s = s;
            _g = _gifts;
            Styles();
            Layout(p);
            Plain(s);
            Build(p);
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f - 20f * s); // (room over it for the line, under it for the tricks)
            Vector2 mouse = e.mousePosition;
            Rect popup = _feedFor >= 0 ? PopupRect(c, s) : new Rect(0, 0, 0, 0);
            bool inPopup = _feedFor >= 0 && popup.Contains(mouse);
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

            // the line over the circle: the piece under the pointer, or the trick waiting for its key
            if (_bindId != null) InfoBar(c, s, Lang.F("<b>Клавіша для «{0}»</b>", _bindName), Lang.T("Натисни клавішу (можна з Shift, Ctrl, Alt) · Esc — скасувати · Backspace — прибрати"));
            else if (_feedFor >= 0 && inPopup) InfoBar(c, s, Lang.F("<b>Погодувати {0}</b>", (_feedFor == 1 ? Lang.T("Муніна") : Lang.T("Хугіна"))), Lang.T("Клік по їжі — нагодувати. Колесо — гортати."));
            else if (_hover >= 0)
            {
                Piece pc = _pieces[_hover];
                if (pc.Info == null) // made once per change of the piece (Build clears it)
                {
                    bool own = pc.Kind == Kind.Trick || pc.Kind == Kind.Order || pc.Kind == Kind.Here || pc.Kind == Kind.Pouch || pc.Kind == Kind.Gift;
                    bool bound = pc.KeyLabel != null && pc.KeyLabel != "—";
                    pc.Info = "<b>" + (own ? Plugin.RavenName(pc.Munin) + ": " : "") + pc.Title + "</b>  <color=#BFB59A>" + pc.Sub + "</color>";
                    pc.InfoDesc = pc.Desc + (pc.KeyId != null ? Lang.F("  ПКМ — {0}.", (bound ? Lang.F("змінити клавішу ({0})", pc.KeyLabel) : Lang.T("призначити клавішу"))) : "");
                    pc.Tip = pc.Kind == Kind.Trick ? pc.Title + (bound ? "  [" + pc.KeyLabel + "]" : "") : null;
                }
                InfoBar(c, s, pc.Info, pc.InfoDesc);
                if (pc.Tip != null) Tip(mouse, s, pc.Tip);
            }
            else if (_gifts) InfoBar(c, s, Lang.T("<b>Дари</b>"), Lang.T("Наведи на дар — тут буде, що він робить. У центрі — накази."));
            else InfoBar(c, s, Lang.T("<b>Накази</b>"), Lang.T("Наведи на кнопку — тут буде, що вона робить."));

            if (_feedFor >= 0) FeedPopup(popup, s, mouse);
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
            if (pc.Kind == Kind.ToOrders) { OrdersButton(c, s, hot); return; }
            if (pc.Kind == Kind.Gift) { RoundGift(pc, c, s, hot); return; }
            if (pc.Kind == Kind.Power) { PowerRing(pc, c, s); return; }
            if (pc.Kind == Kind.Bind || pc.Kind == Kind.BindFire) { BindButton(pc, c + Dir(pc.A0) * pc.R0 * s, pc.CR * 2f * s, hot, s); return; }
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

        private static readonly Color HFill = Hex("#14263d"), MFill = Hex("#3a1610"); // the frames' discs (the chips of a raven away)
        private static readonly Color IconInk = Hex("#e6c57d"), HoverGlow = new Color(0.94f, 0.78f, 0.43f, 1f), OffInk = new Color(0.45f, 0.47f, 0.5f, 1f);

        // A round button of the orders' face, after the user's reference (2026-10-20): a gold frame — with the rune ring
        // for the orders and «Усіх назад», plain for the food, «Трюки» and the tricks — round a disc in the raven's colour
        // (Облетіти green, the shared ones dark), a gold line icon. Its states: under the pointer a soft glow, a touch
        // bigger; going on (a glow, round it the time it has left); the raven sits where it sent him (the disc lit with
        // his colour, «СИДИТЬ ТУТ» under it: a click calls him back); not now (grey, half seen); waiting (a dark share).
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
            GUI.DrawTexture(r, Asset((runes ? "f-ring-" : "f-plain-") + colour));
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
                float isz = d * (words ? 0.26f : 0.42f);
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
            else if (pc.Sits) Badge(at + new Vector2(0f, d * 0.5f + 5f * GS * s), Lang.T("СИДИТЬ ТУТ"), s, a);
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

        // which picture a button of the orders' face shows
        private static string IconOf(Piece pc)
        {
            switch (pc.Kind)
            {
                case Kind.Here: return "o-here";
                case Kind.Pouch: return "o-bag";
                case Kind.Feed: return pc.Munin ? "o-meat" : "o-berries";
                case Kind.Recall: return "o-recall";
                case Kind.Toggle: return _inst != null && _inst._tricksOpen ? "o-up" : "o-down";
                case Kind.Chase: return "o-chase";
                default: return pc.Order < 0 ? "o-perch" : pc.Order == Orders.Scout ? "o-scout" : pc.Order == Orders.Follow ? "o-near" : "o-circle";
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
                    : munin ? ShapeFor(-228f, -182f, TwoBand0, TwoBand1, 14f * GS, true, s) : ShapeFor(132f, 178f, TwoBand0, TwoBand1, 14f * GS, true, s);
                Rect r = Place(sh, c, s);
                GUI.color = munin ? new Color(0.24f, 0.09f, 0.06f, 0.92f * a) : new Color(0.06f, 0.13f, 0.22f, 0.92f * a);
                GUI.DrawTexture(r, sh.Fill);
                GUI.color = A(munin ? MLine : HLine, 0.6f * a);
                GUI.DrawTexture(r, sh.Line);
            }
            GUI.color = Color.white;
        }

        // over each wing a chip: the raven and where he is, what he does; lit gold while he is away from the shoulder
        private void Chips(Vector2 c, float s)
        {
            for (int k = 0; k < 2; k++)
            {
                if (_chip[k] == null) continue;
                bool munin = k == 1, away = _stateIcon[k] != 0;
                Vector2 at = c + (_mode == 0 ? new Vector2((munin ? -1f : 1f) * 150f * GS, -ChipR) : new Vector2(0f, -ChipR)) * s;
                float fs = 13f * GS * s;
                _small.fontSize = Mathf.Max(8, Mathf.RoundToInt(fs));
                float w = Mathf.Min(_small.CalcSize(new GUIContent(_chip[k])).x + 24f * s, 300f * GS * s), h = fs + 12f * s;
                Rect r = new Rect(at.x - w * 0.5f, at.y - h * 0.5f, w, h);
                GUI.color = away ? A(munin ? MFill : HFill, 0.97f) : new Color(0.1f, 0.075f, 0.05f, 0.95f); Fill(r);
                GUI.color = away ? Bright : new Color(0.35f, 0.29f, 0.19f, 1f); Border(r);
                GUI.color = Color.white;
                Label(r, _chip[k], _small, away ? new Color(1f, 0.89f, 0.66f) : munin ? MLine : HLine, fs);
            }
        }

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
        // all round), four parts (0–15, 15–40, 40–90, 90–100) faint, lit up to the satiety, three marks of their own sizes
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

        private void SatietyArcs(Vector2 c, float s)
        {
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
                    Shape all = SatShape(_mode, munin, z0, z1, s);
                    GUI.color = A(SatInk[z], 0.22f);
                    GUI.DrawTexture(Place(all, c, s), all.Fill);
                    if (v <= z0) continue;
                    Shape lit = v >= z1 ? all : LitShape(k, z, munin, z0, v, s);
                    GUI.color = SatInk[z];
                    GUI.DrawTexture(Place(lit, c, s), lit.Fill);
                }
                for (int m = 1; m <= 3; m++) // the marks at 15 (the biggest, red: below it no orders), 40 and 90
                {
                    float an = SatAngle(_mode, munin, SatEdge[m]), grow = m == 1 ? 8f : 3f;
                    Shape t = ShapeFor(an - 0.9f, an + 0.9f, SatIn - 1.5f * GS, SatOut + grow * GS, 0.5f, false, s);
                    GUI.color = m == 1 ? Hex("#e05a4a") : new Color(0.79f, 0.71f, 0.54f, 1f);
                    GUI.DrawTexture(Place(t, c, s), t.Fill);
                }
            }
            GUI.color = Color.white;
        }

        // the lit share of a part, kept one per raven (not in the shape cache: every level would stay there for good)
        private static readonly Shape[] _lit = new Shape[2];
        private static readonly string[] _litKey = new string[2];
        private Shape LitShape(int k, int zone, bool munin, float v0, float v1, float s)
        {
            string key = _mode + "|" + zone + "|" + v1 + "|" + s;
            if (_lit[k] != null && _litKey[k] == key && Mathf.Abs(_cacheS - s) < 0.001f) return _lit[k];
            FreeLit(k);
            float a = SatAngle(_mode, munin, v0), b = SatAngle(_mode, munin, v1);
            _lit[k] = Bake(Mathf.Min(a, b), Mathf.Max(a, b), SatIn, SatOut, s, 1.5f, false);
            _litKey[k] = key;
            return _lit[k];
        }
        private static void FreeLit(int k) { if (_lit[k] != null && _lit[k].Fill != null) Destroy(_lit[k].Fill); _lit[k] = null; _litKey[k] = null; }

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

        // a gift: a round button with its rune medallion. Hover: a light ring; a golden rune: a golden ring and a glow
        // round it, a little stronger with each (the third breathes); its wait drains over it from the top
        private static void RoundGift(Piece pc, Vector2 c, float s, bool hot)
        {
            Vector2 at = c + Dir(pc.A0) * pc.R0 * s;
            float d = pc.CR * 2f * s;
            if (pc.Gold > 0)
            {
                float glow = pc.Gold == 1 ? 0.3f : pc.Gold == 2 ? 0.5f : 0.6f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.4f);
                float g = d * (1.3f + 0.12f * pc.Gold);
                GUI.color = A(GoldGlow, glow);
                GUI.DrawTexture(new Rect(at.x - g * 0.5f, at.y - g * 0.5f, g, g), _soft);
            }
            if (hot || pc.Gold > 0 || pc.Now)
            {
                float rw = (hot || pc.Now ? 3f : 1.5f + pc.Gold * 0.75f) * GS * s, rd = d + rw * 2f;
                GUI.color = hot ? HoverLine : pc.Gold > 0 ? GoldGlow : Bright;
                GUI.DrawTexture(new Rect(at.x - rd * 0.5f, at.y - rd * 0.5f, rd, rd), _disc);
            }
            Rect r = new Rect(at.x - d * 0.5f, at.y - d * 0.5f, d, d);
            GUI.color = pc.Usable || pc.Now ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
            DrawSprite(r, AbilityIcons.For(pc.Gift.Id, pc.Munin));
            if (pc.CoolLeft > 0.5f && pc.CoolAll > 0f) Overlay(r, _disc, new Color(0f, 0f, 0f, 0.6f), Mathf.Clamp01(pc.CoolLeft / pc.CoolAll));
            if (!pc.Usable && !pc.Now) { GUI.color = new Color(0f, 0f, 0f, 0.3f); GUI.DrawTexture(r, _disc); }
            GUI.color = Color.white;
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
                pc.MidName = pc.Title + (pc.Gold > 0 ? " <color=#FFC23A>" + new string('•', pc.Gold) + "</color>" : "");
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

        private static string _barTitle, _barDesc;
        private static float _barScale, _barTh, _barDh;

        private static void InfoBar(Vector2 c, float s, string title, string desc)
        {
            // as tall as its words: a long title or description wraps onto more lines instead of being cut off
            float s0 = s;
            s *= GS; // (all of it GS larger, as the circle)
            float w = 600f * s, inner = w - 24f * s;
            if (title != _barTitle || desc != _barDesc || s != _barScale) // measured once per text, not on every GUI event
            {
                _barTitle = title; _barDesc = desc; _barScale = s;
                _info.fontSize = Mathf.Max(8, Mathf.RoundToInt(14f * s));
                _barTh = Mathf.Max(20f * s, _info.CalcHeight(new GUIContent(title), inner));
                _info.fontSize = Mathf.Max(8, Mathf.RoundToInt(12f * s));
                _barDh = string.IsNullOrEmpty(desc) ? 0f : _info.CalcHeight(new GUIContent(desc), inner);
            }
            float th = _barTh, dh = _barDh;
            float h = 5f * s + th + (dh > 0f ? 2f * s + dh : 0f) + 7f * s;
            Rect r = new Rect(c.x - w / 2f, Mathf.Max(4f, c.y - (ChipR + 18f * GS) * s0 - h), w, h); // over the circle, above the chips (the tricks drop under it)
            GUI.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
            Fill(r);
            GUI.color = A(Gold, 0.7f);
            Border(r);
            GUI.color = Color.white;
            Label(new Rect(r.x + 12f * s, r.y + 5f * s, inner, th), title, _info, Ink, 14f * s);
            if (dh > 0f) Label(new Rect(r.x + 12f * s, r.y + 7f * s + th, inner, dh), desc, _info, new Color(0.78f, 0.74f, 0.64f), 12f * s);
        }

        private static void Tip(Vector2 at, float s, string text)
        {
            Rect r = new Rect(at.x + 16f * s, at.y + 10f * s, 150f * s, 26f * s);
            GUI.color = new Color(0.08f, 0.06f, 0.04f, 0.96f);
            Fill(r);
            GUI.color = A(Gold, 0.8f);
            Border(r);
            GUI.color = Color.white;
            Label(r, text, _small, Bright, 12f * s);
        }

        private static string Clock(float sec) { int t = Mathf.CeilToInt(sec); return (t / 60) + ":" + (t % 60).ToString("00"); }

        // ---------------------------------------------------------------- feeding: a small popup over the centre

        private static Rect PopupRect(Vector2 c, float s)
        {
            // under the feed button (the centre disc's lower edge), never off the screen's bottom
            float w = 4f * 56f * s + 28f * s, h = 30f * s + 2f * 58f * s + 34f * s;
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
                    _foodLine.Add(Lang.F("<b>{0}{1}</b>  <color=#BFB59A>+{2} ситості · +{3} довіри{4}</color>", fd.Name, (known ? " ★" : ""), Mathf.RoundToInt(Satiety.FillOf(fd, fav)), Satiety.Num(Mathf.Round(Satiety.TrustOf(item, fd, fav) * 10f) / 10f), (n <= 0 ? Lang.T(" · нема в сумці") : "")));
                }
        }

        private void FeedPopup(Rect r, float s, Vector2 mouse)
        {
            bool munin = _feedFor == 1;
            if (_feedHead == null) return;
            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.97f);
            Fill(r);
            GUI.color = A(munin ? MLine : HLine, 0.9f);
            Border(r);
            GUI.color = Color.white;
            float pad = 14f * s;
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
                GUI.color = hot ? new Color(0.22f, 0.18f, 0.12f, 1f) : new Color(0.13f, 0.11f, 0.08f, 1f);
                Fill(cr);
                GUI.color = _favKnown[i] ? Bright : hot ? HoverLine : new Color(0.35f, 0.3f, 0.22f);
                Border(cr);
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
                GUI.color = A(Gold, 0.6f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            // the line at the bottom: the food under the pointer
            string line;
            if (_feedHover >= 0) line = _foodLine[_feedHover];
            else line = _foods.Count == 0 ? Lang.T("Зараз він нічого не їсть.") : Lang.T("<color=#BFB59A>Наведи на їжу.</color>");
            _info.alignment = TextAnchor.MiddleLeft;
            Label(new Rect(r.x + pad, r.yMax - 30f * s, r.width - pad * 2f, 26f * s), line, _info, Ink, 12f * s);
            // the pointer: a little notch under the popup, at the centre
            GUI.color = new Color(0.07f, 0.06f, 0.05f, 0.97f);
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
            if (Icons.TryGetValue(prefab, out sp) && sp != null) return sp;
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
        private sealed class Shape { internal Rect Box; internal Texture2D Fill, Line, Thick, Glow; }
        private static Texture2D _disc, _notch, _soft;

        // the boxes (the line under the circle, the feeding popup and its cells): a plain fill and a one-pixel border
        // of four thin strips, in the colour set before (GUI.color); no picture, so always crisp
        private static void Fill(Rect r) { GUI.DrawTexture(r, Texture2D.whiteTexture); }
        private static void Border(Rect r)
        {
            float w = Mathf.Max(1f, Mathf.Round(_s));
            Texture2D px = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), px);
            GUI.DrawTexture(new Rect(r.x, r.y + w, w, r.height - w * 2f), px);
            GUI.DrawTexture(new Rect(r.xMax - w, r.y + w, w, r.height - w * 2f), px);
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
            if (_soft == null) _soft = SoftDisc(128);
        }

        private static void FreeShapes()
        {
            foreach (Shape sh in Cache.Values) foreach (Texture2D t in new[] { sh.Fill, sh.Line, sh.Thick, sh.Glow }) if (t != null) Destroy(t);
            Cache.Clear();
            foreach (Texture2D t in new[] { _disc, _notch, _soft }) if (t != null) Destroy(t);
            _disc = _notch = _soft = null;
            FreeLit(0); FreeLit(1);
            _wingsS = _ticksS = -1f;
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
                ShapeFor(150f, 210f, OneBand0, OneBand1, 14f * GS, true, s); ShapeFor(-228f, -182f, TwoBand0, TwoBand1, 14f * GS, true, s); ShapeFor(132f, 178f, TwoBand0, TwoBand1, 14f * GS, true, s);
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
            Color32[] fill = new Color32[w * h], line = outlined ? new Color32[w * h] : null, thick = outlined ? new Color32[w * h] : null, glow = outlined ? new Color32[w * h] : null;
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
                    float at = af * Mathf.Clamp01(2.4f * s - d + 0.5f);
                    float ag = Mathf.Clamp01(1f - Mathf.Abs(d) / (6f * s)) * (d < 0f ? 0.8f : 0.4f);
                    line[i] = new Color32(255, 255, 255, (byte)(al * 255f));
                    thick[i] = new Color32(255, 255, 255, (byte)(at * 255f));
                    glow[i] = new Color32(255, 255, 255, (byte)(ag * 255f));
                }
            sh.Fill = NewTex(w, h); sh.Fill.SetPixels32(fill); sh.Fill.Apply(false, true);
            if (outlined)
            {
                sh.Line = NewTex(w, h); sh.Line.SetPixels32(line); sh.Line.Apply(false, true);
                sh.Thick = NewTex(w, h); sh.Thick.SetPixels32(thick); sh.Thick.Apply(false, true);
                sh.Glow = NewTex(w, h); sh.Glow.SetPixels32(glow); sh.Glow.Apply(false, true);
            }
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
        private static void Postfix(ref bool __result) { if (OrdersMenu.Shields) __result = true; }
    }

    // The trader's-window trick does not stop a mouse swing: the game hands the hero its attack, second attack and block
    // in Player.SetControls (read from the buttons before). While the menu shields the game they are taken away there.
    [HarmonyPatch(typeof(Player), "SetControls")]
    internal static class OrdersMenuNoSwingPatch
    {
        private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (!OrdersMenu.Shields || __instance != Player.m_localPlayer) return;
            attack = attackHold = secondaryAttack = secondaryAttackHold = block = blockHold = false;
        }
    }
}
#endif

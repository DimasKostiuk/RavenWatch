#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The feeder's own window (the user's design «Годівниця», v2 light; art in assets/book/feeder_*.png cut from it by
    // Chrome): it stands in for the game's chest panel when a feeder is opened, the bag beside it as ever. The food and
    // the finds are the game's own item grids (cloned from the chest's, bound to the feeder's two stores), so moving
    // things works as with any chest; the finds shelf only gives (Feeder.Finds). Right: a card for each
    // raven at home that eats here (up to four), its satiety, guard store and strength; under them the word of the
    // moment. Laid out in the design's own units (2200×1400) scaled to the window.
    internal static class FeederUi
    {
        internal static float Width = 850f; // 1080p units; its height by the design (×1400/2200); narrower when the screen is
        private const float DW = 2200f, DH = 1400f;

        private static readonly FieldInfo DragItemF = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo CurrentF = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");
        private static readonly FieldInfo ContainerGridF = AccessTools.Field(typeof(InventoryGui), "m_containerGrid");
        private static readonly FieldInfo ElementsF = AccessTools.Field(typeof(InventoryGrid), "m_elements");
        private static readonly FieldInfo GridRootF = AccessTools.Field(typeof(InventoryGrid), "m_gridRoot");
        private static readonly MethodInfo OnSelectedM = AccessTools.Method(typeof(InventoryGui), "OnSelectedItem");
        private static readonly MethodInfo OnRightM = AccessTools.Method(typeof(InventoryGui), "OnRightClickItem");

        private static GameObject _root;
        private static RectTransform _win;
        private static float _k = 0.5f;
        private static FeederView _feeder;
        private static InventoryGrid _food, _finds;
        private static TMP_Text _title, _badge, _hint, _ribbon, _button, _empty;
        private static CanvasGroup _hidden; // the game's chest panel, hidden while ours shows
        private static float _hiddenAlpha = 1f; private static bool _hiddenRays = true; // how it was (a UI mod may keep it hidden itself)
        private static GameObject _otherChest; // the chest window a UI mod draws instead of the game's, put away while ours shows
        private static float _textAt;
        private static Canvas _canvas;

        // The window that holds one of the game's two grids, whoever draws it: climbed from the grid for as long as the
        // other grid stays outside. A UI mod that draws its own bag or chest carries the game's grid inside it, so no mod
        // has to be known by name; on the plain game this is the game's own panel. A few steps up, cheap every frame
        private static RectTransform WindowOf(InventoryGrid grid, InventoryGrid other)
        {
            if (grid == null || other == null) return null;
            Transform t = grid.transform;
            while (t.parent != null && !other.transform.IsChildOf(t.parent)) t = t.parent;
            return t as RectTransform;
        }

        // this feeder's window is up here (its finds are yours to change)
        internal static bool Showing(FeederView f) { return f != null && _feeder == f; }

        private sealed class Card
        {
            internal RectTransform Rt, Ghost;
            internal Image Portrait;
            internal TMP_Text Name;
            internal Image[] Fill = new Image[3];
            internal RectTransform[] Bar = new RectTransform[3];
            internal TMP_Text[] Num = new TMP_Text[3];
            internal string[] Tip = new string[3];
        }
        private static readonly Card[] _cards = new Card[4];

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Feeder window: " + s); }

        // ---------------------------------------------------------------- open / close (from the game's inventory)

        internal static void Opened(InventoryGui gui, Container c)
        {
            FeederView f = c != null ? c.GetComponent<FeederView>() : null;
            if (f == null) { Close(); return; }
            try
            {
                float k = KFor(gui);
                if (_root != null && Mathf.Abs(k - _k) > 0.001f) { UnityEngine.Object.Destroy(_root); _root = null; _laid.Clear(); } // (another screen size: made again at its own)
                if (_root == null) { _k = k; Build(gui); }
                if (_root == null) return;
                _feeder = f;
                _told.Clear();
                f.Sync();
                _root.SetActive(true);
                _root.transform.SetAsLastSibling(); // (over a UI mod's bag, a sibling in the same root)
                HideOtherChest(gui);
                Place(gui);
                // unseen until the bag beside it stands still (a UI mod may lay it out and slide it in over the first
                // frames: following it showed as jerks), then in at once
                _group.alpha = 0f; _group.blocksRaycasts = false;
                _still = 0; _openAt = Time.unscaledTime;
                if (gui.m_container != null && _hidden == null)
                {
                    _hidden = gui.m_container.GetComponent<CanvasGroup>() ?? gui.m_container.gameObject.AddComponent<CanvasGroup>();
                    _hiddenAlpha = _hidden.alpha; _hiddenRays = _hidden.blocksRaycasts;
                    _hidden.alpha = 0f; _hidden.blocksRaycasts = false; _hidden.interactable = false;
                }
                _textAt = 0f;
            }
            catch (Exception e) { Log(e.ToString()); Close(); }
        }

        internal static void Close()
        {
            _feeder = null;
            if (_root != null) _root.SetActive(false);
            if (_hidden != null) { _hidden.alpha = _hiddenAlpha; _hidden.blocksRaycasts = _hiddenRays; _hidden.interactable = _hiddenRays; _hidden = null; }
            if (_otherChest != null)
            {
                Container cur = InventoryGui.instance != null && CurrentF != null ? CurrentF.GetValue(InventoryGui.instance) as Container : null;
                if (cur != null) _otherChest.SetActive(true); // (another chest opened meanwhile: its window back)
                _otherChest = null;
            }
        }

        private static CanvasGroup _group;
        private static Vector3 _placed; // where Place put it last: x, top, scale
        private static int _still;
        private static float _openAt;
        private const int StillFrames = 3;
        private const float StillWait = 0.5f, FadeIn = 0.1f;

        // a UI mod shows the feeder as a chest too (it is one, for the game): ours instead. The game's own panel is hidden by
        // its CanvasGroup (Opened), so only a window that does not hold it is put away here
        private static void HideOtherChest(InventoryGui gui)
        {
            RectTransform w = WindowOf(ContainerGridF != null ? ContainerGridF.GetValue(gui) as InventoryGrid : null, gui.m_playerGrid);
            if (w != null && !gui.m_container.IsChildOf(w) && w.gameObject.activeSelf) { w.gameObject.SetActive(false); _otherChest = w.gameObject; }
        }

        // every frame while the inventory is up (InventoryGui.Update)
        internal static void Tick(InventoryGui gui)
        {
            if (ReferenceEquals(_feeder, null)) return;
            if (_feeder == null) { Close(); return; } // (broken while open)
            Container cur = CurrentF != null ? CurrentF.GetValue(gui) as Container : null;
            if (!InventoryGui.IsVisible() || cur != _feeder.Box) { Close(); return; }
            HideOtherChest(gui);
            Vector3 was = _placed;
            Place(gui); // (a UI mod may lay its bag out over the first frames and slide it: ours follows)
            if (_group.alpha < 1f)
            {
                _still = (_placed - was).sqrMagnitude < 0.25f ? _still + 1 : 0;
                if (_still >= StillFrames || Time.unscaledTime - _openAt > StillWait)
                {
                    _group.alpha = Mathf.Min(1f, _group.alpha + Time.unscaledDeltaTime / FadeIn);
                    _group.blocksRaycasts = true;
                }
            }
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData drag = DragItemF != null ? DragItemF.GetValue(gui) as ItemDrop.ItemData : null;
            // each part on its own: another mod's add-on to the game's grid drawing failing on our cloned grids (it
            // came with the first thing in the box) must not take the whole window away
            try { _feeder.Sync(); } catch (Exception e) { Fail("sync", e); }
            // (no player, as the game's chest grid: with one the top row shows the hotbar's numbers)
            try { _food.UpdateInventory(_feeder.Box.GetInventory(), null, drag); Lay(_food, FoodX, FoodY, FoodSlot); } catch (Exception e) { Fail("food", e); }
            try { _finds.UpdateInventory(_feeder.Finds, null, drag); Lay(_finds, FindX, FindY, FindSlot); } catch (Exception e) { Fail("finds", e); }
            if (Time.time >= _textAt) { _textAt = Time.time + 0.25f; try { Texts(p); } catch (Exception e) { Fail("texts", e); } }
            try { Hover(); } catch (Exception e) { Fail("hover", e); }
        }

        private static readonly HashSet<string> _told = new HashSet<string>(); // (each failing part logged once a window, with where)
        private static void Fail(string what, Exception e) { if (_told.Add(what)) Log(what + ": " + e); }

        // ---------------------------------------------------------------- the design's places (its 2200×1400 units)

        // (the food shelf drawn at half the design's size, the finds 150 higher: assets/book/feeder_bg.png)
        private static readonly float[] FoodX = { 132f, 271f, 410f, 549f }, FoodY = { 322f, 461f };
        private const float FoodSlot = 125f;
        private static readonly float[] FindX = { 201f, 341f, 481f, 621f }, FindY = { 890f };
        private const float FindSlot = 118f;
        private const float CardX = 1344f, CardY = 236f, CardStep = 243f, CardW = 740f, CardH = 225f;

        private static RectTransform Put(Transform parent, string name, float x, float y, float w, float h)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x * _k, -y * _k);
            rt.sizeDelta = new Vector2(w * _k, h * _k);
            return rt;
        }

        private static Image Pic(Transform parent, string name, Sprite s, float x, float y, float w, float h)
        {
            Image i = Put(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            i.sprite = s; i.raycastTarget = false; i.preserveAspect = false;
            return i;
        }

        private static TMP_Text Text(Transform parent, string name, float x, float y, float w, float h, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Put(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Max(10f, size - 4f); // (a long word shrinks a little, never below readable)
            return t;
        }

        private static readonly Color Ink = new Color(0.23f, 0.14f, 0.07f, 1f), Light = new Color(0.95f, 0.86f, 0.66f, 1f);

        // ---------------------------------------------------------------- building (once)

        private static void Build(InventoryGui gui)
        {
            RavenBook.EnsureGameFont();
            _laid.Clear(); // (the old grids went with the old window: a new game's inventory, another screen size)
            Transform parent = gui.m_container != null ? gui.m_container.parent : gui.transform;
            _root = new GameObject("RavenWatch_Feeder", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            _win = (RectTransform)_root.transform;
            _win.anchorMin = _win.anchorMax = new Vector2(0f, 1f); _win.pivot = new Vector2(0f, 1f);
            _win.sizeDelta = new Vector2(DW * _k, DH * _k);
            Image bg = _root.AddComponent<Image>();
            bg.sprite = BookArt.Picture("feeder_bg"); bg.raycastTarget = true; // (a click on the window is not a click through it)
            _group = _root.AddComponent<CanvasGroup>();

            _title = Text(_win, "Title", 190f, 100f, 1630f, 92f, 22f, Light, BookArt.Heading, TextAlignmentOptions.Center);
            RectTransform badge = Put(_win, "Badge", 1902f, 84f, 124f, 124f);
            _badge = Text(badge, "Value", 0f, 0f, 124f, 124f, 17f, Light, BookArt.Heading, TextAlignmentOptions.Center);
            Text(_win, "FoodLabel", 218f, 224f, 370f, 50f, 16f, Light, BookArt.Heading, TextAlignmentOptions.Center).text = Lang.T("Корм");
            Text(_win, "FindsLabel", 166f, 830f, 290f, 40f, 15f, Light, BookArt.Heading, TextAlignmentOptions.Center).text = Lang.T("Знахідки");
            _hint = Text(_win, "Hint", 770f, 330f, 520f, 460f, 14f, Ink, BookArt.Italic, TextAlignmentOptions.TopLeft);
            _ribbon = Text(_win, "Ribbon", 175f, 1226f, 1700f, 66f, 15f, Ink, BookArt.Body, TextAlignmentOptions.MidlineLeft);
            _ribbon.textWrappingMode = TextWrappingModes.NoWrap;

            // «Забрати все»: every find into your bag
            RectTransform b = Put(_win, "TakeAll", 1914f, 1228f, 148f, 64f);
            Image bi = b.gameObject.AddComponent<Image>(); bi.color = new Color(1f, 1f, 1f, 0f); // (the art draws the button; this only takes the click)
            Button btn = b.gameObject.AddComponent<Button>(); btn.targetGraphic = bi;
            btn.onClick.AddListener(TakeAll);
            _button = Text(b, "Label", 8f, 0f, 132f, 64f, 13f, Light, BookArt.Heading, TextAlignmentOptions.Center);
            _button.text = Lang.T("Забрати все");

            for (int i = 0; i < 4; i++) _cards[i] = MakeCard(i);
            _empty = Text(_win, "NoRavens", CardX + 30f, CardY + 40f, CardW - 60f, CardH * 2f, 15f, Ink, BookArt.Italic, TextAlignmentOptions.Center);
            _empty.text = Lang.F("Поруч нема воронів, що живуть удома.\nЛиши ворона на жердині ({0}) до {1} м звідси.", RavenKeys.LabelOf(RavenKeys.HomeId), Mathf.RoundToInt(Home.FeederReach));

            _food = Grid(gui, "Food");
            _finds = Grid(gui, "Finds");
            _canvas = _win.GetComponentInParent<Canvas>();
            _root.SetActive(false);
        }

        private static Card MakeCard(int i)
        {
            float y = CardY + CardStep * i;
            Card c = new Card();
            c.Ghost = Pic(_win, "Ghost" + i, BookArt.Picture("feeder_ghost"), CardX - 5f, y - 5f, 760f, 245f).rectTransform;
            c.Rt = Pic(_win, "Card" + i, BookArt.Picture("feeder_card"), CardX - 5f, y - 5f, 760f, 245f).rectTransform;
            Transform t = c.Rt; // (its own units: the card's 760×245 picture, the card itself at 5,5)
            Image ring = Pic(t, "Portrait", null, 5f + 50f, 5f + 50.5f, 124f, 124f);
            ring.preserveAspect = true;
            c.Portrait = ring;
            c.Name = Text(t, "Name", 5f + 240f, 5f + 30f, 324f, 30f, 14f, Ink, BookArt.Heading, TextAlignmentOptions.MidlineLeft);
            c.Name.textWrappingMode = TextWrappingModes.NoWrap;
            float[] by = { 82f, 124f, 166f }, ny = { 79f, 121f, 163f };
            for (int k = 0; k < 3; k++)
            {
                c.Bar[k] = Put(t, "Bar" + k, 5f + 232f, 5f + by[k], 380f, 24f);
                Image f = Pic(c.Bar[k], "Fill", null, 4f, 4f, 372f, 16f);
                f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = 0;
                c.Fill[k] = f;
                c.Num[k] = Text(t, "Num" + k, 5f + 628f, 5f + ny[k], 84f, 30f, 13f, Ink, BookArt.Heading, TextAlignmentOptions.Center);
                c.Num[k].textWrappingMode = TextWrappingModes.NoWrap;
            }
            return c;
        }

        // one of the game's item grids (the chest's), made ours: its old cells gone, its clicks to the game's own
        private static InventoryGrid Grid(InventoryGui gui, string name)
        {
            InventoryGrid src = ContainerGridF != null ? ContainerGridF.GetValue(gui) as InventoryGrid : null;
            if (src == null) throw new InvalidOperationException("no chest grid in the game");
            GameObject g = UnityEngine.Object.Instantiate(src.gameObject, _win);
            g.name = "RavenWatch_" + name;
            InventoryGrid grid = g.GetComponent<InventoryGrid>();
            RectTransform root = GridRootF != null ? GridRootF.GetValue(grid) as RectTransform : null;
            if (root != null) for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
            foreach (ScrollRect sr in g.GetComponentsInChildren<ScrollRect>(true)) sr.enabled = false; // (it shows all its cells: no scrolling)
            RectTransform rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = _win.sizeDelta;
            if (root != null) { root.anchorMin = root.anchorMax = new Vector2(0f, 1f); root.pivot = new Vector2(0f, 1f); root.anchoredPosition = Vector2.zero; }
            if (OnSelectedM != null) grid.m_onSelected = (Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>)Delegate.CreateDelegate(typeof(Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>), gui, OnSelectedM);
            grid.CanDropDragOntoItem = src.CanDropDragOntoItem; // (set by the game in code, not kept in a copy: without it the first thing in the box broke the drawing)
            if (OnRightM != null) grid.m_onRightClick = (Action<InventoryGrid, ItemDrop.ItemData, Vector2i>)Delegate.CreateDelegate(typeof(Action<InventoryGrid, ItemDrop.ItemData, Vector2i>), gui, OnRightM);
            return grid;
        }

        // each cell where the design has its slot (the game lays its cells its own way, once, when it makes them: put
        // right after it does — and only then, not every frame)
        private static readonly Dictionary<InventoryGrid, InventoryElement> _laid = new Dictionary<InventoryGrid, InventoryElement>();
        private static void Lay(InventoryGrid grid, float[] xs, float[] ys, float slot)
        {
            List<InventoryElement> els = ElementsF != null ? ElementsF.GetValue(grid) as List<InventoryElement> : null;
            if (els == null || els.Count == 0) return;
            InventoryElement first;
            if (_laid.TryGetValue(grid, out first) && ReferenceEquals(first, els[0])) return; // (the same cells, laid already)
            _laid[grid] = els[0];
            foreach (InventoryElement e in els)
            {
                if (e == null) continue;
                int x = e.Position.x, y = e.Position.y;
                RectTransform rt = e.transform as RectTransform;
                if (rt == null || x >= xs.Length || y >= ys.Length) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((xs[x] + slot * 0.5f) * _k, -(ys[y] + slot * 0.5f) * _k);
                float want = slot * _k * 0.86f, have = Mathf.Max(1f, rt.rect.width);
                rt.localScale = Vector3.one * (want / have);
            }
        }

        // its scale: the usual width, or what the screen has room for (Place shrinks it further if the bag leaves less)
        private static float KFor(InventoryGui gui)
        {
            RectTransform parent = gui.m_container != null ? gui.m_container.parent as RectTransform : null;
            float room = parent != null ? ScreenIn(parent).width - 40f : Width;
            return Mathf.Min(Width, Mathf.Max(600f, room)) / DW;
        }

        // the whole screen in the parent's units (a UI mod may size the inventory root to its own windows, not the screen)
        private static Rect ScreenIn(RectTransform parent)
        {
            Canvas cv = parent.GetComponentInParent<Canvas>();
            RectTransform screen = cv != null ? cv.rootCanvas.transform as RectTransform : null;
            if (screen == null) return parent.rect;
            Vector3[] c = _corners;
            screen.GetWorldCorners(c);
            Vector3 a = parent.InverseTransformPoint(c[0]), b = parent.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // where your bag is on the screen: the window around the game's bag grid, or the game's panel
        private static RectTransform BagWindow(InventoryGui gui)
        {
            RectTransform bag = WindowOf(gui.m_playerGrid, ContainerGridF != null ? ContainerGridF.GetValue(gui) as InventoryGrid : null);
            return bag != null && bag.gameObject.activeInHierarchy ? bag : gui.m_player;
        }

        // right of your bag, never over it: smaller if the screen's right side is narrow, beside the bag on the left only
        // when the right has no room at all
        private const float Gap = 16f, MinFit = 0.55f;
        private static readonly Vector3[] _corners = new Vector3[4]; // (every frame: one buffer, read at once each time)
        private static void Place(InventoryGui gui)
        {
            RectTransform parent = _win.parent as RectTransform;
            if (parent == null) return;
            Rect pr = parent.rect, sr = ScreenIn(parent);
            float w = DW * _k, h = DH * _k, fit = 1f;
            float x = sr.xMin + 40f, top = sr.yMax - 60f;
            RectTransform bag = BagWindow(gui);
            if (bag != null)
            {
                Vector3[] c = _corners;
                bag.GetWorldCorners(c); // 0 bottom-left, 2 top-right
                Vector3 left = parent.InverseTransformPoint(c[0]), right = parent.InverseTransformPoint(c[2]);
                float roomR = sr.xMax - right.x - Gap * 2f, roomL = left.x - sr.xMin - Gap * 2f;
                top = right.y;
                if (roomR >= w * MinFit || roomR >= roomL) { fit = Mathf.Clamp(roomR / w, MinFit, 1f); x = right.x + Gap; }
                else { fit = Mathf.Clamp(roomL / w, MinFit, 1f); x = left.x - Gap - w * fit; }
            }
            fit = Mathf.Min(fit, (sr.height - 20f) / h);
            _win.localScale = new Vector3(fit, fit, 1f);
            x = Mathf.Clamp(x, sr.xMin, Mathf.Max(sr.xMin, sr.xMax - w * fit));
            top = Mathf.Clamp(top, sr.yMin + h * fit, sr.yMax);
            _win.anchoredPosition = new Vector2(x - pr.xMin, top - pr.yMax);
            _placed = new Vector3(x, top, fit * 100f);
        }

        // ---------------------------------------------------------------- what it says

        private static void Texts(Player p)
        {
            Piece piece = _feeder.GetComponent<Piece>();
            _title.text = piece != null ? Lang.Game(piece.m_name) : Lang.T("Годівниця");
            bool thrifty = _feeder.Feed > 1.001f;
            _badge.text = "×" + Lang.Num(_feeder.Feed, "0.##");
            _hint.text = Lang.T("Муніну — м'ясо, Хугіну — риба, ягоди, гриби, овочі й страви з них. Що поживніша їжа, то довше ворон ситий; вилітає на ворогів лише ситий.")
                + (thrifty ? Lang.F("\nТа сама їжа звідси насичує в {0} раза більше.", Lang.Num(_feeder.Feed, "0.##")) : "");
            List<Home.Eater> eaters = Home.EatersAt(_feeder.transform.position);
            string hungry = null;
            for (int i = 0; i < 4; i++)
            {
                Card c = _cards[i];
                bool has = i < eaters.Count;
                c.Rt.gameObject.SetActive(has);
                c.Ghost.gameObject.SetActive(!has && eaters.Count > 0);
                if (!has) continue;
                Home.Eater e = eaters[i];
                c.Portrait.sprite = BookArt.Portrait(e.Munin);
                c.Name.text = e.Name + " <size=80%><color=#7A5630>· " + e.State + "</color></size>";
                Set(c, 0, e.Sat / 100f, Mathf.RoundToInt(e.Sat).ToString(), ZoneBrush(e.Zone), Lang.F("Ситість {0} — {1}{2}", Mathf.RoundToInt(e.Sat), e.ZoneWord, e.Fed ? "" : Lang.T(": голодний — не вилітає")));
                Set(c, 1, e.StoreMax > 0f ? e.Store / e.StoreMax : 0f, Mathf.FloorToInt(e.Store).ToString(), "brush-gold", Lang.F("Запас варти {0}/{1}: ним він б'є ворогів", Mathf.FloorToInt(e.Store), Mathf.RoundToInt(e.StoreMax)));
                Set(c, 2, e.Max > 0f ? e.Strength / e.Max : 0f, Mathf.FloorToInt(e.Strength).ToString(), e.Munin ? "brush-munin" : "brush-huginn", Lang.F("Сила {0}/{1}", Mathf.FloorToInt(e.Strength), Mathf.RoundToInt(e.Max)) + (e.Fav.Length > 0 ? Lang.F(" · улюблене: {0}", e.Fav) : ""));
                if (!e.Fed && hungry == null) hungry = Lang.F("{0} голодний — додай {1}.", e.Name, e.Munin ? Lang.T("м'яса") : Lang.T("риби, ягід чи грибів"));
            }
            _empty.gameObject.SetActive(eaters.Count == 0);
            string last = _feeder.LastFind;
            _ribbonIdle = hungry ?? (last.Length > 0 ? Lang.F("Остання знахідка: {0}", Home.ItemName(last)) : eaters.Count > 0 ? Lang.T("Усі ситі.") : "");
            _button.color = _feeder.Finds.NrOfItems() > 0 ? Light : new Color(Light.r, Light.g, Light.b, 0.45f);
        }
        private static string _ribbonIdle = "";

        private static string ZoneBrush(int zone) { return zone <= Satiety.Hungry ? "brush-sat-hungry" : zone == Satiety.Empty ? "brush-sat-warn" : "brush-sat-ok"; }

        private static void Set(Card c, int k, float fill, string num, string brush, string tip)
        {
            c.Fill[k].sprite = BookArt.Brush(brush);
            c.Fill[k].fillAmount = Mathf.Clamp01(fill);
            c.Num[k].text = num;
            c.Tip[k] = tip;
        }

        // over a bar: the ribbon says what it is (the numbers on the card are short)
        private static void Hover()
        {
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 mouse = Input.mousePosition;
            foreach (Card c in _cards)
            {
                if (c == null || !c.Rt.gameObject.activeSelf) continue;
                for (int k = 0; k < 3; k++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(c.Bar[k], mouse, cam) || RectTransformUtility.RectangleContainsScreenPoint((RectTransform)c.Num[k].transform, mouse, cam))
                    { _ribbon.text = c.Tip[k]; return; }
            }
            _ribbon.text = _ribbonIdle;
        }

        private static void TakeAll()
        {
            Player p = Player.m_localPlayer;
            if (p == null || _feeder == null || _feeder.Finds.NrOfItems() == 0) return;
            _feeder.Sync(true);
            if (!_feeder.ClaimFinds()) return;
            p.GetInventory().MoveAll(_feeder.Finds);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Show")]
    internal static class FeederUiShowPatch
    {
        private static void Postfix(InventoryGui __instance, Container __0) { try { FeederUi.Opened(__instance, __0); } catch { } } // (by place, not by name: the game may call it otherwise)
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class FeederUiTickPatch
    {
        private static void Postfix(InventoryGui __instance) { FeederUi.Tick(__instance); }
    }

    [HarmonyPatch(typeof(InventoryGui), "Hide")]
    internal static class FeederUiHidePatch
    {
        private static void Postfix() { FeederUi.Close(); }
    }

    // Shift+click on a find: into your bag. (The game moves a Shift-clicked thing of any grid but the chest's own into the
    // chest, and takes it out of your bag, not out of the shelf: the find would be in the food box and on the shelf at once.)
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class FeederFindsMovePatch
    {
        private static readonly FieldInfo DragGoF = AccessTools.Field(typeof(InventoryGui), "m_dragGo");
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (mod != InventoryGrid.Modifier.Move || item == null || grid == null || !FeederView.IsFinds(grid.GetInventory())) return true;
            if (DragGoF != null && DragGoF.GetValue(__instance) as GameObject != null) return true; // (dragging: the game's own drop)
            Player p = Player.m_localPlayer;
            if (p == null || p.IsTeleporting()) return false;
            p.GetInventory().MoveItemToThis(grid.GetInventory(), item);
            return false;
        }
    }

    // the finds shelf only gives: nothing is put on it by hand (the ravens put their finds there) — nor by a swap: a find
    // dropped on a taken cell of another grid would send that cell's thing back onto the shelf in its place
    [HarmonyPatch(typeof(InventoryGrid), "DropItem")]
    internal static class FeederFindsDropPatch
    {
        private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
        {
            Inventory to = __instance.GetInventory();
            if (to == null || fromInventory == null || fromInventory == to) return true;
            if (FeederView.IsFinds(to)) return Refuse(ref __result, Lang.T("Сюди ворони кладуть свої знахідки — класти сюди не можна."));
            if (item == null || !FeederView.IsFinds(fromInventory)) return true;
            ItemDrop.ItemData there = to.GetItemAt(pos.x, pos.y);
            bool swap = there != null && there != item && item.m_stack == amount
                && (there.m_shared.m_name != item.m_shared.m_name || (item.m_shared.m_maxQuality > 1 && there.m_quality != item.m_quality) || there.m_shared.m_maxStackSize == 1); // (the game's own swap rule)
            return swap ? Refuse(ref __result, Lang.T("Поклади знахідку на вільну клітинку — на полицю нічого не кладуть.")) : true;
        }

        private static bool Refuse(ref bool result, string why)
        {
            result = false;
            Player p = Player.m_localPlayer;
            if (p != null) p.Message(MessageHud.MessageType.Center, why);
            return false;
        }
    }
}
#endif

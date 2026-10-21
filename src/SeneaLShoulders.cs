#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuginWhispers
{
    // SeneaL UI draws worn gear on a doll of its own, made of the game's inventory cells. The ravens get two small round
    // cells there, on the lower corners of the head cell (Munin on the left, Hugin on the right), out of the helmet's way.
    // While they are there a raven on the shoulder is not in the bag at all (RavenSeat keeps it).
    // Nothing of SeneaL UI is changed: the cells lie beside its head cell and follow it (its cells hide the pictures other
    // mods put inside them), and only what it keeps public is read: Plugin.Active, Slots.Bypass and Slots.SlotCell.
    // If any of it is missing, or its inventory is switched off, no cells are drawn and the ravens go back into the bag,
    // still on the shoulders, as in the plain game. Should SeneaL UI one day draw the ravens itself, this steps aside.
    internal static class SeneaLShoulders
    {
        internal static bool Wanted = true;
        internal static float Size = 30f;       // of SeneaL UI's 64 px cell
        internal static float OutX = -10f;      // the centre past the head cell's side edge (outwards), px
        internal static float DownY = 2f;       // and past its bottom edge (downwards): they stick out, the helmet keeps its room
        internal static float HoverScale = 1.25f;
        internal static float HoverSpeed = 12f;

        private static bool _looked, _found;
        private static FieldInfo _active;       // SeneaLUI.Plugin.Active
        private static PropertyInfo _bypass;    // SeneaLUI.Inv.Slots.Bypass: its inventory is not drawn
        private static MethodInfo _slotCell;    // SeneaLUI.Inv.Slots.SlotCell(Inventory, slot): where a doll slot lies
        // the same, made into plain calls once: they are asked every frame while the inventory is open
        private static Func<bool> _activeGet, _bypassGet;
        private static Func<Inventory, int, Vector2i> _cellOf;
        private static readonly FieldInfo DragItemField = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo DragInvField = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly MethodInfo SetupDrag = AccessTools.Method(typeof(InventoryGui), "SetupDragItem");
        private static readonly FieldInfo Elements = AccessTools.Field(typeof(InventoryGrid), "m_elements");

        private static int _holdsFrame = -1;
        private static bool _holds;
        private static float _offSince = -1f;

        private static ShoulderCell _hugin, _munin;
        private static InventoryElement _head;
        private static CanvasGroup _headFade;   // SeneaL UI fades its doll with it (found once per head cell)
        private static bool _headFadeLooked;
        private static Sprite _disc;

        // SeneaL UI's own tooltip (Tooltip.Show / Exit, Tips.ForItem): the cells' words look like the rest of its inventory;
        // without it the game's own tooltip is used
        private static bool _tipLooked;
        private static Type _tipType;
        private static MethodInfo _tipShow, _tipExit, _forItem, _tipReset;
        private static FieldInfo _tipSub, _tipBody;

        internal static bool TipFind()
        {
            if (_tipLooked) return _tipType != null;
            _tipLooked = true;
            try
            {
                Type tooltip = AccessTools.TypeByName("SeneaLUI.Components.Tooltip");
                Type tips = AccessTools.TypeByName("SeneaLUI.Inv.Tips");
                Type tip = AccessTools.TypeByName("SeneaLUI.Components.Tip");
                if (tooltip == null || tips == null || tip == null) return false;
                Type provider = typeof(Func<>).MakeGenericType(tip);
                _tipShow = AccessTools.Method(tooltip, "Show", new Type[] { typeof(RectTransform), provider, typeof(bool) });
                _tipExit = AccessTools.Method(tooltip, "Exit", new Type[] { typeof(RectTransform) });
                _forItem = AccessTools.Method(tips, "ForItem", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(int) });
                _tipReset = AccessTools.Method(tip, "Reset", new Type[] { typeof(string), typeof(int) });
                _tipSub = AccessTools.Field(tip, "Sub");
                _tipBody = AccessTools.Field(tip, "Body");
                if (_tipShow != null && _tipExit != null && _forItem != null && _forItem.ReturnType == tip && _tipReset != null && _tipSub != null && _tipBody != null) _tipType = tip;
            }
            catch (Exception e) { Log("SeneaL UI tooltip not used: " + e.Message); }
            return _tipType != null;
        }

        internal static void TipShow(RectTransform anchor, ShoulderCell cell)
        {
            if (!TipFind()) return;
            // () => (Tip)cell.TipObject(): the provider SeneaL UI asks while the pointer stays; made once per cell
            if (cell.TipProvider == null)
            {
                Expression call = Expression.Convert(Expression.Call(Expression.Constant(cell), typeof(ShoulderCell).GetMethod("TipObject", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)), _tipType);
                cell.TipProvider = Expression.Lambda(typeof(Func<>).MakeGenericType(_tipType), call).Compile();
            }
            _tipShow.Invoke(null, new object[] { anchor, cell.TipProvider, false });
        }

        internal static void TipExit(RectTransform anchor)
        {
            if (TipFind()) try { _tipExit.Invoke(null, new object[] { anchor }); } catch { }
        }

        // the worn raven's own card (as for any worn item), with the shoulder under its name
        internal static object ItemTip(ItemDrop.ItemData item, string sub)
        {
            object tip = _forItem.Invoke(null, new object[] { item, 1, false, 0 });
            if (tip != null) _tipSub.SetValue(tip, sub);
            return tip;
        }

        // an empty shoulder: what goes there and how
        internal static object EmptyTip(ref object own, string title, string body, int version)
        {
            if (own == null) own = Activator.CreateInstance(_tipType);
            _tipReset.Invoke(own, new object[] { title, version });
            _tipBody.SetValue(own, body);
            return own;
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }

        private static bool Find()
        {
            if (_looked) return _found;
            _looked = true;
            try
            {
                Type plugin = AccessTools.TypeByName("SeneaLUI.Plugin");
                Type slots = AccessTools.TypeByName("SeneaLUI.Inv.Slots");
                if (plugin == null || slots == null) return false;
                if (AccessTools.TypeByName("SeneaLUI.Mods.RavenWatch") != null) { Log("SeneaL UI draws the ravens itself: RavenWatch's shoulder cells step aside."); return false; }
                _active = AccessTools.Field(plugin, "Active");
                _bypass = AccessTools.Property(slots, "Bypass");
                _slotCell = AccessTools.Method(slots, "SlotCell", new Type[] { typeof(Inventory), typeof(int) });
                _found = _active != null && _active.FieldType == typeof(bool) && _bypass != null && _bypass.PropertyType == typeof(bool)
                    && _slotCell != null && _slotCell.ReturnType == typeof(Vector2i)
                    && DragItemField != null && DragInvField != null && SetupDrag != null && Elements != null;
                if (_found)
                {
                    _activeGet = Expression.Lambda<Func<bool>>(Expression.Field(null, _active)).Compile();
                    _bypassGet = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), _bypass.GetGetMethod(true));
                    _cellOf = (Func<Inventory, int, Vector2i>)Delegate.CreateDelegate(typeof(Func<Inventory, int, Vector2i>), _slotCell);
                }
                Log(_found ? "SeneaL UI: the ravens sit in their own shoulder cells on its doll." : "SeneaL UI is not as RavenWatch knows it: the ravens on the shoulders stay in the bag.");
            }
            catch (Exception e) { _found = false; Log("SeneaL UI shoulder cells off: " + e.Message); }
            return _found;
        }

        // SeneaL UI draws the inventory now: the ravens on the shoulders are kept off the bag
        internal static bool Holds
        {
            get
            {
                if (Time.frameCount == _holdsFrame) return _holds;
                _holdsFrame = Time.frameCount;
                _holds = false;
                if (Wanted && Find())
                {
                    try { _holds = _activeGet() && !_bypassGet(); }
                    catch { _holds = false; }
                }
                if (_holds) _offSince = -1f;
                return _holds;
            }
        }

        // the cells are gone for good (not a moment of SeneaL UI starting up): the ravens go back into the bag
        internal static bool Released
        {
            get
            {
                if (Holds) return false;
                if (!Wanted || !Find()) return true;
                if (_offSince < 0f) _offSince = Time.time;
                return Time.time - _offSince >= 3f;
            }
        }

        // ---------------------------------------------------------------- the cells, every frame the inventory is open

        internal static void Update(InventoryGui gui)
        {
            Player p = Player.m_localPlayer;
            RectTransform head = null;
            if (gui != null && p != null && InventoryGui.IsVisible() && Holds) head = HeadCell(gui.m_playerGrid, p.GetInventory());
            if (head == null || !head.gameObject.activeInHierarchy) { Show(false); return; }
            if (_hugin == null || _hugin.Rt == null) Build(gui, head);
            if (_hugin == null) return;
            Show(true);
            if (!_headFadeLooked) { _headFadeLooked = true; _headFade = head.GetComponent<CanvasGroup>(); }
            float alpha = _headFade != null ? _headFade.alpha : 1f; // SeneaL UI fades its doll in and out
            _munin.Follow(head, false, alpha);
            _hugin.Follow(head, true, alpha);
            _munin.Refresh(p);
            _hugin.Refresh(p);
        }

        private static void Show(bool on)
        {
            if (_hugin != null && _hugin.gameObject.activeSelf != on) _hugin.gameObject.SetActive(on);
            if (_munin != null && _munin.gameObject.activeSelf != on) _munin.gameObject.SetActive(on);
        }

        // SeneaL UI's head slot is slot 0; the game's cell that stands there
        private static RectTransform HeadCell(InventoryGrid grid, Inventory inv)
        {
            if (grid == null || inv == null) return null;
            Vector2i pos;
            try { pos = _cellOf(inv, 0); }
            catch { return null; }
            if (_head != null && _head.Position.x == pos.x && _head.Position.y == pos.y) return _head.transform as RectTransform;
            _head = null;
            _headFadeLooked = false;
            List<InventoryElement> els = Elements.GetValue(grid) as List<InventoryElement>;
            if (els == null) return null;
            foreach (InventoryElement e in els)
                if (e != null && e.Position.x == pos.x && e.Position.y == pos.y) { _head = e; break; }
            return _head != null ? _head.transform as RectTransform : null;
        }

        private static void Build(InventoryGui gui, RectTransform head)
        {
            Kill();
            Transform parent = head.parent;
            if (parent == null) return;
            GameObject tipPrefab = _head != null && _head.m_tooltip != null ? _head.m_tooltip.m_tooltipPrefab : null;
            _munin = ShoulderCell.Make(parent, true, Disc(), tipPrefab);
            _hugin = ShoulderCell.Make(parent, false, Disc(), tipPrefab);
        }

        internal static void Kill()
        {
            if (_hugin != null) UnityEngine.Object.Destroy(_hugin.gameObject);
            if (_munin != null) UnityEngine.Object.Destroy(_munin.gameObject);
            _hugin = null; _munin = null;
        }

        // a soft round disc, drawn once
        internal static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int n = 256; // drawn finer than shown, shrunk with mipmaps: a clean round edge
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            Color32[] px = new Color32[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            t.hideFlags = HideFlags.HideAndDontSave;
            _disc = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 400f);
            _disc.hideFlags = HideFlags.HideAndDontSave;
            return _disc;
        }

        // ---------------------------------------------------------------- what a click or a drop does

        internal static ItemDrop.ItemData Dragged(InventoryGui gui)
        {
            return gui != null && DragItemField != null ? DragItemField.GetValue(gui) as ItemDrop.ItemData : null;
        }

        private static void EndDrag(InventoryGui gui)
        {
            try { SetupDrag.Invoke(gui, new object[] { null, null, 1 }); } catch { }
        }

        private static void Moved(InventoryGui gui)
        {
            if (gui != null) RavenFx.Own(gui.m_moveItemEffects, gui.transform.position, Quaternion.identity);
        }

        // a raven dragged onto a shoulder cell or onto the head: it sits on its own shoulder, whichever side it was dropped on
        internal static bool DropRaven(InventoryGui gui)
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Dragged(gui);
            bool munin;
            if (p == null || !RavenItem.IsRaven(item, out munin)) return false;
            Inventory from = DragInvField.GetValue(gui) as Inventory;
            if (from != null && from != p.GetInventory() && GraveRavens.OwnGrave(from) && from.ContainsItem(item))
            {
                // out of your own grave straight onto the shoulder, a full bag or not
                EndDrag(gui);
                if (RavenSeat.FromGrave(p, from, item, true)) Moved(gui);
                else if (RavenSeat.On(p, munin) != null) RavenMsg.Center(p, munin ? Lang.T("На лівому плечі вже сидить Мунін.") : Lang.T("На правому плечі вже сидить Хугін."));
                return true;
            }
            if (from != p.GetInventory() || !from.ContainsItem(item))
            {
                RavenMsg.Center(p, Lang.T("Спершу поклади ворона в сумку."));
                return true;
            }
            EndDrag(gui);
            if (RavenSeat.Sit(p, item, false)) Moved(gui);
            return true;
        }

        internal static void Clicked(bool munin, bool right)
        {
            InventoryGui gui = InventoryGui.instance;
            Player p = Player.m_localPlayer;
            if (gui == null || p == null) return;
            if (!right && Dragged(gui) != null) { DropRaven(gui); return; }
            ItemDrop.ItemData on = RavenSeat.On(p, munin);
            if (on != null) { RavenSeat.Toggle(p, on); return; } // the equipping move first, as with gear (the cell follows when it is done)
            if (!right) RavenSeat.ToggleFromBag(p, munin);
        }

        // a raven dropped on the head cell of the doll
        internal static void SelectedOn(InventoryGui gui, InventoryGrid grid, Vector2i pos)
        {
            if (gui == null || grid != gui.m_playerGrid || !Holds) return;
            ItemDrop.ItemData item = Dragged(gui);
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return;
            Vector2i head;
            try { head = _cellOf(grid.GetInventory(), 0); }
            catch { return; }
            if (head.x == pos.x && head.y == pos.y) DropRaven(gui);
        }
    }

    // one round shoulder cell
    internal sealed class ShoulderCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        internal bool Munin;
        internal RectTransform Rt;
        internal Delegate TipProvider;          // SeneaL UI's tooltip asks it for the words (made once)
        private Image _rim, _icon;
        private CanvasGroup _cg;
        private UITooltip _tip;
        private bool _over;
        private float _scale = 1f;
        private ItemDrop.ItemData _shown;
        private int _state = -1;

        private static readonly Color Gold = new Color(0.85f, 0.72f, 0.43f, 1f);
        private static readonly Color Dim = new Color(0.35f, 0.31f, 0.25f, 1f);
        private static readonly Color Back = new Color(0.08f, 0.07f, 0.06f, 0.92f);

        internal static ShoulderCell Make(Transform parent, bool munin, Sprite disc, GameObject tipPrefab)
        {
            GameObject go = new GameObject(munin ? "RavenWatch_ShoulderMunin" : "RavenWatch_ShoulderHugin", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ShoulderCell c = go.AddComponent<ShoulderCell>();
            c.Munin = munin;
            c.Rt = (RectTransform)go.transform;
            c.Rt.anchorMin = c.Rt.anchorMax = c.Rt.pivot = new Vector2(0.5f, 0.5f);
            c._cg = go.AddComponent<CanvasGroup>();
            c._rim = go.AddComponent<Image>(); // the gold rim, and what takes the clicks
            c._rim.sprite = disc;
            c._rim.raycastTarget = true;

            GameObject back = new GameObject("back", typeof(RectTransform));
            back.transform.SetParent(go.transform, false);
            RectTransform br = (RectTransform)back.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(2f, 2f); br.offsetMax = new Vector2(-2f, -2f);
            Image bi = back.AddComponent<Image>();
            bi.sprite = disc; bi.color = Back; bi.raycastTarget = false;
            Mask mask = back.AddComponent<Mask>(); // the square picture is cut round
            mask.showMaskGraphic = true;

            GameObject icon = new GameObject("icon", typeof(RectTransform));
            icon.transform.SetParent(back.transform, false);
            RectTransform ir = (RectTransform)icon.transform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = ir.offsetMax = Vector2.zero;
            ir.localScale = new Vector3(munin ? -1f : 1f, 1f, 1f); // both look away from the head, out over the shoulder
            c._icon = icon.AddComponent<Image>();
            c._icon.sprite = AbilityIcons.For(munin ? "munin" : "hugin", munin);
            c._icon.preserveAspect = true;
            c._icon.raycastTarget = false;

            if (tipPrefab != null && !SeneaLShoulders.TipFind()) // the game's own tooltip only without SeneaL UI's
            {
                c._tip = go.AddComponent<UITooltip>();
                c._tip.m_tooltipPrefab = tipPrefab;
            }
            go.transform.SetAsLastSibling();
            return c;
        }

        // on the head cell's lower corner, as big as it is now (it swells a little under the pointer)
        internal void Follow(RectTransform head, bool right, float alpha)
        {
            if (Rt.parent != head.parent) Rt.SetParent(head.parent, false);
            if (Rt.GetSiblingIndex() < head.GetSiblingIndex()) Rt.SetAsLastSibling();
            Rect r = head.rect;
            Vector3 local = new Vector3(right ? r.xMax + SeneaLShoulders.OutX : r.xMin - SeneaLShoulders.OutX, r.yMin - SeneaLShoulders.DownY, 0f);
            Rt.position = head.TransformPoint(local);
            Rt.sizeDelta = new Vector2(SeneaLShoulders.Size, SeneaLShoulders.Size);
            float want = _over ? SeneaLShoulders.HoverScale : 1f;
            _scale = Mathf.Lerp(_scale, want, 1f - Mathf.Exp(-SeneaLShoulders.HoverSpeed * Time.unscaledDeltaTime));
            Vector3 hs = head.localScale;
            Rt.localScale = new Vector3(hs.x * _scale, hs.y * _scale, 1f);
            if (_over && _scale > 1.01f && Rt.GetSiblingIndex() != Rt.parent.childCount - 1) Rt.SetAsLastSibling(); // over its neighbours while it swells
            if (!Mathf.Approximately(_cg.alpha, alpha)) _cg.alpha = alpha;
        }

        // who sits there: the colours, and the words under the pointer
        private float _nextRefresh;
        internal void Refresh(Player p)
        {
            if (_state >= 0 && Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            ItemDrop.ItemData on = RavenSeat.On(p, Munin);
            int state = on == null ? 0 : Trust.Away(on) ? 2 : 1;
            if (on == _shown && state == _state) return;
            _shown = on; _state = state;
            _rim.color = state == 0 ? Dim : Gold;
            _icon.color = state == 0 ? new Color(0.45f, 0.45f, 0.45f, 0.45f) : state == 2 ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            if (_tip == null) return;
            string side = Munin ? Lang.T("Ліве плече") : Lang.T("Праве плече");
            if (on == null)
                _tip.Set(side, Lang.F("{0}\nПеретягни ворона сюди або на шолом,\nабо клікни — сяде ворон із сумки.", (Munin ? Lang.T("Тут сидить Мунін.") : Lang.T("Тут сидить Хугін."))));
            else
                _tip.Set(on.m_shared.m_name, Lang.F("{0}. Клік — зняти в сумку.\n\n{1}", (side + (state == 2 ? " (" + Trust.AwayWord(Munin, on) + ")" : "")), ItemDrop.ItemData.GetTooltip(on, on.m_quality, false, on.m_worldLevel, -1, false)));
        }

        private object _emptyTip, _tipObj;
        private ItemDrop.ItemData _tipItem;
        private bool _tipAway;
        private float _tipUntil;

        // asked by SeneaL UI's tooltip every frame while the pointer stays
        internal object TipObject()
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData on = p != null ? RavenSeat.On(p, Munin) : null;
            string side = Munin ? Lang.T("Ліве плече") : Lang.T("Праве плече");
            if (on != null)
            {
                // the item's whole tooltip is put together anew only twice a second (or when another raven sits there)
                bool away = Trust.Away(on);
                if (_tipObj == null || on != _tipItem || away != _tipAway || Time.unscaledTime >= _tipUntil)
                {
                    _tipObj = SeneaLShoulders.ItemTip(on, side + (away ? " · " + Trust.AwayWord(Munin, on) : ""));
                    _tipItem = on; _tipAway = away; _tipUntil = Time.unscaledTime + 0.5f;
                }
                return _tipObj;
            }
            return SeneaLShoulders.EmptyTip(ref _emptyTip, side, Lang.F("{0}\nПеретягни ворона сюди або на шолом,\nабо клікни — сяде ворон із сумки.", (Munin ? Lang.T("Тут сидить Мунін.") : Lang.T("Тут сидить Хугін."))), Munin ? 2 : 1);
        }

        public void OnPointerEnter(PointerEventData e) { _over = true; _state = -1; SeneaLShoulders.TipShow(Rt, this); }
        public void OnPointerExit(PointerEventData e) { _over = false; SeneaLShoulders.TipExit(Rt); }
        private void OnDisable() { if (_over) { _over = false; SeneaLShoulders.TipExit(Rt); } }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) SeneaLShoulders.Clicked(Munin, false);
            else if (e.button == PointerEventData.InputButton.Right) SeneaLShoulders.Clicked(Munin, true);
            _state = -1;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class SeneaLShouldersUpdatePatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            try { SeneaLShoulders.Update(__instance); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder cells: " + e.Message); SeneaLShoulders.Kill(); SeneaLShoulders.Wanted = false; }
        }
    }

    // a raven dropped on the head cell: SeneaL UI refuses it there (not a helmet) and leaves it in the hand
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class SeneaLShouldersDropPatch
    {
        private static void Postfix(InventoryGui __instance, InventoryGrid grid, Vector2i pos)
        {
            try { SeneaLShoulders.SelectedOn(__instance, grid, pos); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder drop: " + e.Message); }
        }
    }

    // SeneaL UI's own «take all» (its container toolbar) never calls the game's MoveAll (GraveRavensTakeAllPatch): the
    // graves' ravens are seen to here too. Your own go to their shoulders past a full bag; from someone else's grave all
    // but the ravens are taken
    [HarmonyPatch]
    internal static class SeneaLTakeAllPatch
    {
        private static MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("SeneaLUI.Inv.Transfer");
            return t != null ? AccessTools.Method(t, "TakeAll", new Type[] { typeof(Inventory), typeof(Inventory), typeof(Container) }) : null;
        }
        private static bool Prepare() { return TargetMethod() != null; }
        private static bool Prefix(Inventory p, Inventory c, Container remote, ref int __result)
        {
            try
            {
                if (p == null || c == null || Player.m_localPlayer == null || p != Player.m_localPlayer.GetInventory()) return true;
                if (GraveRavens.ForeignGraveOf(c) == null) { GraveRavens.RavensOut(c); return true; }
                if (remote != null) // (another mod's shared chest: its own way, which this cannot sort) nothing taken while a raven lies there
                {
                    foreach (ItemDrop.ItemData item in c.GetAllItems()) { bool munin; if (RavenItem.IsRaven(item, out munin)) { GraveRavens.NoTaking(munin); __result = 0; return false; } }
                    return true;
                }
                if (GraveRavens.TakeAllBut(p, c)) return true;
                __result = 1;
                return false;
            }
            catch { return true; }
        }
    }
}
#endif

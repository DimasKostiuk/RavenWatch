#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The feeding pouch (торба): a thing for a raven's back (RavenGear.IsPouch) with one cell for food, up to Max pieces
    // of one kind. Hungry (satiety at the asking level or below), the raven eats from it by itself, a piece at a time:
    // it eats only what you put there for it, and while there is some it has no need to steal from your bag.
    //
    // The pouch opens in the game's own window, as a chest does: your bag on the left, the pouch beside it, moved
    // between with the mouse, the gamepad, «Взяти все», as you are used to. Behind it is a Container that never wakes
    // (its object stays inactive, on you), whose inventory is ours and whose "network view" is your own: the game asks
    // only whether you own it (you do). Nothing of it is sent anywhere or saved by the game: the food is kept in the
    // raven item, as "Prefab:count", while the pouch is worn; taken off in any way, the food goes to your bag (Spill).
    //
    // Only food this raven eats now goes in, and never more than Max: a stack of 20 dragged in leaves 15 where it was.
    internal static class RavenPouch
    {
        internal const int Max = 5;
        internal static float SayChance = 0.5f;       // it says something about it, now and then (always told in the corner)
        internal static float CheckSeconds = 3f;      // how often a hungry raven looks into its pouch
        internal static float AfterMealSeconds = 20f; // and after a piece, before the next
        internal const string Key = "RavenWatch_pouch";

        private static Container _box;
        private static Inventory _inv;
        private static bool _munin;
        private static ItemDrop.ItemData _raven;
        internal static bool Loading;                 // our own filling: no checks
        private static readonly float[] Next = new float[2];
        private static float _toldAt = -10f;

        private static readonly FieldInfo InvF = AccessTools.Field(typeof(Container), "m_inventory");
        private static readonly FieldInfo ViewF = AccessTools.Field(typeof(Container), "m_nview");
        private static readonly FieldInfo CurrentF = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");
        private static readonly MethodInfo CloseM = AccessTools.Method(typeof(InventoryGui), "CloseContainer");
        internal static readonly MethodInfo AddAtM = AccessTools.Method(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });

        internal static Container Box { get { return _box; } }
        internal static bool Is(Inventory inv) { return inv != null && inv == _inv; }

        // ---------------------------------------------------------------- what is in it ("Prefab:count")

        private static string Read(ItemDrop.ItemData item)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(Key, out v) ? v : "";
        }

        private static void Write(ItemDrop.ItemData item, string v)
        {
            if (item == null) return;
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(v)) item.m_customData.Remove(Key); else item.m_customData[Key] = v;
        }

        // a pouch item that still holds food (made by an older version of the mod): that food to the bag (p null: just emptied)
        internal static void SpillItem(Player p, ItemDrop.ItemData pouch)
        {
            string v = Read(pouch);
            Write(pouch, null);
            GiveBack(p, v);
        }
        internal static void SetInRaven(ItemDrop.ItemData raven, string v) { Write(raven, v); }

        private static bool Parse(string v, out string prefab, out int n)
        {
            prefab = null; n = 0;
            if (string.IsNullOrEmpty(v)) return false;
            int c = v.LastIndexOf(':');
            if (c <= 0 || !int.TryParse(v.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) return false;
            prefab = v.Substring(0, c);
            n = Mathf.Min(n, Max);
            return true;
        }

        private static string Code(string prefab, int n) { return n > 0 && !string.IsNullOrEmpty(prefab) ? prefab + ":" + n.ToString(CultureInfo.InvariantCulture) : ""; }

        private static ItemDrop.ItemData Make(string prefab, int n)
        {
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = g != null ? g.GetComponent<ItemDrop>() : null;
            if (drop == null) return null;
            ItemDrop.ItemData d = drop.m_itemData.Clone();
            d.m_dropPrefab = g;
            d.m_stack = n;
            d.m_worldLevel = (byte)Game.m_worldLevel;
            return d;
        }

        // "Малина ×3" for the book and tooltips; "" when empty
        internal static string Describe(ItemDrop.ItemData raven)
        {
            string prefab; int n;
            if (_inv != null && raven == _raven) { ItemDrop.ItemData at = _inv.GetItemAt(0, 0); return at != null ? Name(at.m_shared.m_name) + " ×" + at.m_stack : ""; }
            if (!Parse(Read(raven), out prefab, out n)) return "";
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null; // its name only: no copy of the item
            ItemDrop drop = g != null ? g.GetComponent<ItemDrop>() : null;
            return (drop != null ? Name(drop.m_itemData.m_shared.m_name) : prefab) + " ×" + n;
        }

        private static string Name(string key) { return Localization.instance != null ? Lang.Game(key) : key; }

        // ---------------------------------------------------------------- the window

        internal static bool Worn(ItemDrop.ItemData raven) { return raven != null && RavenGear.IsPouch(Outfits.Worn(raven, Outfits.Back)); }

        // opens this raven's pouch beside your bag; null when it opened, else why not
        internal static string Open(Player p, bool munin)
        {
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (p == null || InventoryGui.instance == null) return null;
            if (raven == null) return Lang.F("{0} зараз не на тобі.", Plugin.RavenName(munin));
            if (!Worn(raven)) return Lang.F("На {0} нема торби.", (munin ? Lang.T("Муніні") : Lang.T("Хугіні")));
            if (Trust.Stranger(raven, p)) return Lang.F("{0} не твій: у його торбу не лізь.", Plugin.RavenName(munin));
            ZNetView view = p.GetComponent<ZNetView>();
            if (view == null || InvF == null || ViewF == null) return Lang.T("Торбу зараз не відкрити.");
            if (_box == null || _box.transform.parent != p.transform)
            {
                if (_box != null) UnityEngine.Object.Destroy(_box.gameObject);
                GameObject go = new GameObject("RavenWatch_Pouch");
                go.SetActive(false); // never wakes: no network, no saving of its own
                go.transform.SetParent(p.transform, false);
                _box = go.AddComponent<Container>();
            }
            _box.m_name = munin ? Lang.W("Торба Муніна") : Lang.W("Торба Хугіна");
            _box.m_width = 1; _box.m_height = 1;
            _inv = new Inventory(_box.m_name, null, 1, 1);
            _munin = munin; _raven = raven;
            string prefab; int n;
            if (Parse(Read(raven), out prefab, out n))
            {
                ItemDrop.ItemData d = Make(prefab, n);
                Loading = true;
                try { if (d != null) _inv.AddItem(d, new Vector2i(0, 0)); }
                finally { Loading = false; }
            }
            _inv.m_onChanged = (Action)Delegate.Combine(_inv.m_onChanged, new Action(Save));
            InvF.SetValue(_box, _inv);
            ViewF.SetValue(_box, view);
            if (RavenBook.IsOpen) RavenBook.Close();
            InventoryGui.instance.Show(_box);
            return null;
        }

        internal static bool IsOpen
        {
            get
            {
                InventoryGui gui = InventoryGui.instance;
                return _box != null && gui != null && CurrentF != null && InventoryGui.IsVisible() && (Container)CurrentF.GetValue(gui) == _box;
            }
        }

        // written into the raven on every change (the window is the live copy while open)
        private static void Save()
        {
            if (Loading || _inv == null || _raven == null) return;
            ItemDrop.ItemData at = _inv.GetItemAt(0, 0);
            Write(_raven, at != null && at.m_dropPrefab != null ? Code(at.m_dropPrefab.name, at.m_stack) : "");
        }

        private static void Shut()
        {
            InventoryGui gui = InventoryGui.instance;
            if (IsOpen && CloseM != null) CloseM.Invoke(gui, null);
        }

        internal static void CloseFor(bool munin) { if (_inv != null && _munin == munin) { Save(); Shut(); _inv = null; _raven = null; } }

        // twice a second: the window shuts when its raven is off you or the pouch off the raven
        internal static void Guard()
        {
            if (_inv == null) return;
            if (!IsOpen) { _inv = null; _raven = null; return; } // closed by the player: all was saved on every change
            if (Trust.Worn(_munin) != _raven || !Worn(_raven)) { Save(); Shut(); _inv = null; _raven = null; }
        }

        // A pouch tried on without being given (admins fitting) comes off: its food goes back into your bag (or at your feet).
        internal static void Spill(Player p, ItemDrop.ItemData raven, int slot)
        {
            if (raven == null || slot != Outfits.Back || !RavenGear.IsPouch(Outfits.Worn(raven, slot))) return;
            bool munin;
            if (RavenItem.IsRaven(raven, out munin)) CloseFor(munin);
            string v = Read(raven);
            Write(raven, null);
            GiveBack(p, v);
        }

        // food back to the bag; what does not fit, at your feet
        private static void GiveBack(Player p, string v)
        {
            string prefab; int n;
            if (!Parse(v, out prefab, out n) || p == null) return;
            ItemDrop.ItemData d = Make(prefab, n);
            if (d == null) return;
            if (p.GetInventory().CanAddItem(d)) p.GetInventory().AddItem(d);
            else ItemDrop.DropItem(d, n, p.transform.position + p.transform.forward + Vector3.up, Quaternion.identity);
        }

        // ---------------------------------------------------------------- what goes in

        // food this raven eats now; anything else is refused with a word (once a second at most)
        private static Trust.Food FoodOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null || _raven == null) return null;
            string prefab = item.m_dropPrefab.name;
            foreach (Trust.Food f in Trust.EdibleFoods(_munin, _raven)) if (f.Prefab == prefab) return f;
            return null;
        }

        internal static bool Accepts(ItemDrop.ItemData item)
        {
            if (FoodOf(item) != null) return true;
            if (Time.unscaledTime - _toldAt > 1f && Player.m_localPlayer != null)
            {
                _toldAt = Time.unscaledTime;
                bool food = item != null && item.m_dropPrefab != null && Trust.Foods(_munin).Exists(f => f.Prefab == item.m_dropPrefab.name);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, food
                    ? Lang.F("{0} поки такого не їсть ({1}).", Plugin.RavenName(_munin), Trust.FoodRuleText(Trust.Level(_raven)))
                    : Lang.F("У торбу — лише їжа для {0}.", (_munin ? Lang.T("Муніна") : Lang.T("Хугіна"))));
            }
            return false;
        }

        // how many of this item still fit (0: none, or another food is in there)
        internal static int Room(ItemDrop.ItemData item)
        {
            if (_inv == null || !Accepts(item)) return 0;
            ItemDrop.ItemData at = _inv.GetItemAt(0, 0);
            if (at == null) return Max;
            if (at.m_shared.m_name != item.m_shared.m_name) return 0;
            return Mathf.Max(0, Max - at.m_stack);
        }

        internal static void Redraw(Inventory inv) { InvRedraw.Changed(inv); }

        // ---------------------------------------------------------------- eating by itself

        // for each worn raven, twice a second (Trust.Tick)
        internal static void Tick(Player p, bool munin, ItemDrop.ItemData raven)
        {
            int i = munin ? 1 : 0;
            if (Time.time < Next[i]) return;
            Next[i] = Time.time + CheckSeconds;
            if (!Worn(raven) || Satiety.Value(raven) > Satiety.AskAt) return;
            bool live = _inv != null && _raven == raven; // its window is open: that is the pouch now
            string prefab; int n;
            if (live)
            {
                ItemDrop.ItemData at = _inv.GetItemAt(0, 0);
                if (at == null || at.m_dropPrefab == null) return;
                prefab = at.m_dropPrefab.name; n = at.m_stack;
            }
            else if (!Parse(Read(raven), out prefab, out n)) return;
            Trust.Food food = Trust.Foods(munin).Find(f => f.Prefab == prefab);
            if (food == null) return;
            bool ate;
            string said = Trust.FeedFromPouch(p, munin, food, out ate);
            if (!ate) return;
            Next[i] = Time.time + AfterMealSeconds;
            if (live) _inv.RemoveOneItem(_inv.GetItemAt(0, 0)); // saved by the window's own change
            else Write(raven, Code(prefab, n - 1));
            if (!string.IsNullOrEmpty(said)) p.Message(MessageHud.MessageType.TopLeft, said, 0, RavenItem.Icon(munin));
        }

        internal static void Reset() { _inv = null; _raven = null; Next[0] = Next[1] = 0f; }
    }

    // ---------------------------------------------------------------- the game's inventory, for the pouch only

    // the window's open/closed look and sounds are a chest's: the pouch has none (and must not write to your network data)
    [HarmonyPatch(typeof(Container), "SetInUse")]
    internal static class PouchInUsePatch
    {
        private static bool Prefix(Container __instance) { return __instance == null || (__instance != RavenPouch.Box && __instance != PostParcel.Box); }
    }

    // dragged onto its cell (and every other way that names the cell): no more than fits
    [HarmonyPatch]
    internal static class PouchAddAtPatch
    {
        private static MethodBase TargetMethod() { return RavenPouch.AddAtM; }
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref int amount, ref bool __result)
        {
            if (!Boxes.Is(__instance) || Boxes.Loading(__instance)) return true;
            int room = Boxes.Room(__instance, item);
            if (room <= 0) { __result = false; return false; }
            amount = Mathf.Min(amount, room);
            return true;
        }

        // the whole stack went in: a drag still on the cursor would show a stack that is gone (seen with SeneaL UI)
        private static void Postfix(Inventory __instance, ItemDrop.ItemData item, bool __result)
        {
            if (__result && Boxes.Is(__instance) && !Boxes.Loading(__instance) && item != null && item.m_stack <= 0) PouchDrag.EndIfGoneSoon();
        }
    }

    // A drag left on the cursor after its whole stack went into the pouch: the game (or a UI mod) did not end it. Ended
    // here when its item is used up or no longer where it was dragged from; said once in the log (to see which way it came).
    internal static class PouchDrag
    {
        private static readonly FieldInfo DragItem = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo DragInv = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly MethodInfo Setup = AccessTools.Method(typeof(InventoryGui), "SetupDragItem");
        private static bool _told, _waiting;

        // not at once: right after the move the game (and SeneaL UI) still read the dragged item and end the drag
        // themselves; only one left over a frame later is ended here
        internal static void EndIfGoneSoon()
        {
            if (_waiting || Plugin.Instance == null) return;
            _waiting = true;
            Plugin.Instance.StartCoroutine(Later());
        }

        private static System.Collections.IEnumerator Later()
        {
            yield return null;
            _waiting = false;
            try { EndIfGone(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Pouch drag: " + e.Message); }
        }

        private static void EndIfGone()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || DragItem == null || Setup == null) return;
            ItemDrop.ItemData dragged = DragItem.GetValue(gui) as ItemDrop.ItemData;
            if (dragged == null) return;
            Inventory from = DragInv != null ? DragInv.GetValue(gui) as Inventory : null;
            if (dragged.m_stack > 0 && (from == null || from.ContainsItem(dragged))) return; // a real drag: something is left of it
            Setup.Invoke(gui, new object[] { null, null, 1 });
            if (!_told && Plugin.Instance != null) { _told = true; Plugin.Instance.Log("Pouch: a drag was left on the cursor after its stack went into the pouch; ended it"); }
        }
    }

    // put in without a cell (a click with the move key, «stack all», other mods): only what fits; the rest stays where it was
    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData) })]
    internal static class PouchAddPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { return PouchAdd.Check(__instance, item, ref __result); }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
    internal static class PouchAddPosPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { return PouchAdd.Check(__instance, item, ref __result); }
    }

    internal static class PouchAdd
    {
        internal static bool Check(Inventory inv, ItemDrop.ItemData item, ref bool result)
        {
            if (!Boxes.Is(inv) || Boxes.Loading(inv) || item == null) return true;
            int room = Boxes.Room(inv, item);
            if (room <= 0) { result = false; return false; }
            if (item.m_stack <= room) return true;
            // a part of it: put in through the cell (that takes it off the stack), and "not all of it went in" so the
            // caller keeps the rest where it was
            RavenPouch.AddAtM.Invoke(inv, new object[] { item, room, 0, 0, false });
            result = false;
            return false;
        }
    }

    // A drop onto a cell. Into the pouch: another food there goes back to where the new one came from (not lost, not
    // more than fits). Out of the pouch onto another thing in your bag: no swapping into the pouch, the food just
    // goes into your bag wherever it fits.
    [HarmonyPatch(typeof(InventoryGrid), "DropItem")]
    internal static class PouchDropPatch
    {
        private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
        {
            Inventory to = __instance != null ? __instance.GetInventory() : null;
            if (item == null || to == null || fromInventory == null) return true;
            if (Boxes.Is(to))
            {
                if (fromInventory == to) return true; // within the pouch: one cell, nothing to do
                if (!Boxes.Accepts(to, item)) { __result = false; return false; }
                ItemDrop.ItemData at = to.GetItemAt(pos.x, pos.y);
                if (at == item) { __result = true; return false; }
                if (at != null && at.m_shared.m_name != item.m_shared.m_name)
                {
                    if (!fromInventory.CanAddItem(at)) { __result = false; return false; }
                    to.RemoveItem(at);
                    fromInventory.AddItem(at);
                }
                int before = item.m_stack;
                to.MoveItemToThis(fromInventory, item, amount, pos.x, pos.y);
                RavenPouch.Redraw(fromInventory);
                __result = item.m_stack < before || !fromInventory.ContainsItem(item); // something went in: the drag ends
                return false;
            }
            if (Boxes.Is(fromInventory))
            {
                ItemDrop.ItemData at = to.GetItemAt(pos.x, pos.y);
                if (at == null || at == item || at.m_shared.m_name == item.m_shared.m_name) return true; // the game's own move
                ItemDrop.ItemData part = item.Clone();
                part.m_stack = Mathf.Min(amount, item.m_stack);
                if (!to.CanAddItem(part)) { __result = false; return false; }
                to.AddItem(part);
                item.m_stack -= part.m_stack;
                if (item.m_stack <= 0) fromInventory.RemoveItem(item); else RavenPouch.Redraw(fromInventory);
                __result = true;
                return false;
            }
            return true;
        }
    }
}
#endif

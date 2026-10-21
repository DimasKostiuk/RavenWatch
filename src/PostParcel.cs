#if RAVEN_ITEM
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The parcel being packed (Post): one cell beside your bag in the game's own window, as the feeding pouch opens
    // (RavenPouch: a Container that never wakes, its inventory ours). One small thing goes in (Post.ParcelWhyNot), one
    // piece of it — coins as many as fit in the parcel's weight. What lies there is kept in the character (Post.ParcelKey)
    // until it is sent or taken out; the window shut, the book opens again on the post.
    internal static class PostParcel
    {
        private static Container _box;
        private static Inventory _inv;
        internal static bool Loading;
        private static bool _back;          // the book's post opens again once the window is shut
        private static int _backWait;       // frames waited for the game's window to be really gone
        private static float _toldAt = -10f, _openedAt = -10f;

        private static readonly FieldInfo InvF = AccessTools.Field(typeof(Container), "m_inventory");
        private static readonly FieldInfo ViewF = AccessTools.Field(typeof(Container), "m_nview");
        private static readonly FieldInfo CurrentF = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        internal static Container Box { get { return _box; } }
        internal static bool Is(Inventory inv) { return inv != null && inv == _inv; }

        internal static string Open(Player p)
        {
            if (p == null || InventoryGui.instance == null) return null;
            ZNetView view = p.GetComponent<ZNetView>();
            if (view == null || InvF == null || ViewF == null) return Lang.T("Посилку зараз не відкрити.");
            if (_box == null || _box.transform.parent != p.transform)
            {
                if (_box != null) UnityEngine.Object.Destroy(_box.gameObject);
                GameObject go = new GameObject("RavenWatch_Parcel");
                go.SetActive(false); // never wakes: no network, no saving of its own
                go.transform.SetParent(p.transform, false);
                _box = go.AddComponent<Container>();
            }
            _box.m_name = Lang.W("Посилка");
            _box.m_width = 1; _box.m_height = 1;
            _inv = new Inventory(_box.m_name, null, 1, 1);
            ItemDrop.ItemData d = Post.ParcelOf(p);
            Loading = true;
            try { if (d != null) _inv.AddItem(d, new Vector2i(0, 0)); }
            finally { Loading = false; }
            _inv.m_onChanged = (Action)Delegate.Combine(_inv.m_onChanged, new Action(Save));
            InvF.SetValue(_box, _inv);
            ViewF.SetValue(_box, view);
            _back = true;
            _openedAt = Time.unscaledTime;
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

        private static void Save()
        {
            if (Loading || _inv == null) return;
            Post.SetParcel(Player.m_localPlayer, _inv.GetItemAt(0, 0));
        }

        // every frame (RavenBook.Update): the window shut, the post opens again in the book
        internal static void Guard()
        {
            if (_inv == null && !_back) return;
            if (IsOpen || Time.unscaledTime - _openedAt < 0.5f) return; // (the game shows its window a frame or two later)
            if (_inv != null) { Save(); _inv = null; }
            if (!_back) return;
            // the game still counts its window as shown a frame or two after it shuts, and the Esc or Tab that shut
            // it is still down: a book opened then was shut again by its own Update at once (the post never came back)
            if (InventoryGui.IsVisible() || RavenKeys.Down(KeyCode.Escape) || RavenKeys.Down(KeyCode.Tab))
            {
                if (++_backWait < 30) return;
                _back = false; _backWait = 0; // (another window of the game stayed open: no book over it)
                return;
            }
            _back = false; _backWait = 0;
            if (Player.m_localPlayer != null && !Player.m_localPlayer.IsDead() && !Menu.IsVisible()) RavenBook.OpenPost();
        }

        internal static bool Accepts(ItemDrop.ItemData item)
        {
            string why = Post.ParcelWhyNot(item);
            if (why == null) return true;
            if (Time.unscaledTime - _toldAt > 1f && Player.m_localPlayer != null)
            {
                _toldAt = Time.unscaledTime;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, why);
            }
            return false;
        }

        // how many of this still fit: one thing (coins: up to the parcel's weight); 0 when another thing is in there
        internal static int Room(ItemDrop.ItemData item)
        {
            if (_inv == null || !Accepts(item)) return 0;
            ItemDrop.ItemData at = _inv.GetItemAt(0, 0);
            int max = Post.MaxOf(item);
            if (at == null) return max;
            if (at.m_shared.m_name != item.m_shared.m_name) return 0;
            return Mathf.Max(0, max - at.m_stack);
        }

        internal static void Reset() { _inv = null; _back = false; _backWait = 0; }

        // the parcel sent: the live cell lets go of it (its Save would put the thing back into the character)
        internal static void Drop() { if (_inv == null) return; Loading = true; try { _inv.RemoveAll(); } finally { Loading = false; } _inv = null; }
    }

    // the pouch's and the parcel's cells answer the same patches (RavenPouch.cs)
    internal static class Boxes
    {
        internal static bool Is(Container c) { return c != null && (c == RavenPouch.Box || c == PostParcel.Box); }
        internal static bool Is(Inventory inv) { return RavenPouch.Is(inv) || PostParcel.Is(inv); }
        internal static bool Loading(Inventory inv) { return PostParcel.Is(inv) ? PostParcel.Loading : RavenPouch.Loading; }
        internal static int Room(Inventory inv, ItemDrop.ItemData item) { return PostParcel.Is(inv) ? PostParcel.Room(item) : RavenPouch.Room(item); }
        internal static bool Accepts(Inventory inv, ItemDrop.ItemData item) { return PostParcel.Is(inv) ? PostParcel.Accepts(item) : RavenPouch.Accepts(item); }
    }

    // Our cells are Containers with no Piece (they live on the player, borrowing its ZNetView): Path of Valheiman took
    // them for world chests and filled them with chest loot on their first opening (a PoV jewel turned up in the
    // parcel, 2026-10-18) — and its Save wrote the cell into the player's ZDO. Not a world chest, for every PoV hook
    // (first loot, more/rarer loot, refills all ask WorldChests.IsWorld). Without PoV this patch is skipped.
    [HarmonyPatch]
    internal static class BoxesNotWorldChests
    {
        private static MethodBase _target;
        private static bool Prepare()
        {
            Type t = AccessTools.TypeByName("PassiveTree.WorldChests");
            _target = t != null ? AccessTools.Method(t, "IsWorld", new Type[] { typeof(Container) }) : null;
            return _target != null;
        }
        private static MethodBase TargetMethod() { return _target; }
        private static void Postfix(Container __0, ref bool __result) { if (__result && Boxes.Is(__0)) __result = false; }
    }

    // and no mod's Save of our cells: they keep what is in them themselves (the character), never in the player's ZDO
    [HarmonyPatch(typeof(Container), "Save")]
    internal static class BoxesNoSave
    {
        private static bool Prefix(Container __instance) { return !Boxes.Is(__instance); }
    }
}
#endif

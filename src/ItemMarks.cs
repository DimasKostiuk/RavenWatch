#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // What an item of yours says of itself where you see it — one place for every kind of mark (the user 2026-11-14: «треба сюжетні предмети якось
    // помічати»; the errands' raven was the first, the story's sign the second):
    //   Kinds              a kind of mark: Any — is there one of its kind at all now (most frames: none, nothing done), Icon — the picture in the slot's top
    //                      left corner for this item (null: not this kind's), Line — a line under its tooltip (null: none). The first kind with an icon wins the corner
    //   the slot's corner  InventoryGrid.UpdateGui (the bag, a chest, any grid): a small picture, hidden on every other slot
    //   the tooltip        ItemData.GetTooltip: each kind's line, in its own colour
    // A new kind of mark is a line in Kinds from its own module (ErrandMark, WsItems' story items), never a patch of its own.
    // What an item of yours keeps (Keeps: the story's items — the user 2026-11-14: «не губиться при смерті, не викидається»):
    //   death              it stays in the bag: taken out before the game makes the grave (Player.CreateTombStone), put back after
    //   a drop             it is not thrown on the ground (Humanoid.DropItem): why, in the middle of the screen
    //   gone some other way it comes back (Tick, from the story's pass — StoryModules: the story's things are the only kept ones; the user 2026-11-14 — another mod's bin deletes a thing by taking it out of the bag, just as a move
    //                      into a chest does, so no one call tells a deletion; no mod is named): twice a second the bag's kept things are counted, and one gone that
    //                      is in no chest near, on no ground near and was not let go (Let: the story took it — a talk's «!take») is put back in the bag
    //   a trader           the mod's items have no worth to a trader (WsItems: m_value 0): none buys them
    //   a stand            it is not hung on an item stand (ItemStand.CanAttach): a stand is no chest, and the watch below would put a second one in the bag
    //   not covered        a storage that is no chest of the game (another mod's own bag of rows): what goes there is taken for lost and put back — one more
    internal static class ItemMarks
    {
        internal sealed class Kind
        {
            public string Name = "";
            public Func<bool> Any;                                  // (cheap: asked every frame a grid is up)
            public Func<ItemDrop.ItemData, Sprite> Icon;
            public Func<ItemDrop.ItemData, string> Line;
        }
        internal static readonly List<Kind> Kinds = new List<Kind>();

        // the kinds, each from its own module (a new kind of mark: one line here); once, at the first look
        private static bool _wired;
        internal static void Wire()
        {
            if (_wired) return;
            _wired = true;
            ErrandMarkKind.Marks();   // the running errand's things: the raven in the corner, «Добуто для доручення…»
            WsItems.StoryMarks();     // the story's things (an item's «story:» line): the gold sign, «Сюжетний предмет», kept on death and never dropped
        }

        // an item that stays with you whatever happens: why (shown when a drop is refused), null — an item as any
        internal static readonly List<Func<ItemDrop.ItemData, string>> Keeps = new List<Func<ItemDrop.ItemData, string>>();
        internal static string KeptWhy(ItemDrop.ItemData it)
        {
            if (it == null) return null;
            Wire();
            for (int i = 0; i < Keeps.Count; i++) { string why = Keeps[i](it); if (why != null) return why; }
            return null;
        }

        // ---------------------------------------------------------------- kept things watched
        // Cost: the bag's items counted twice a second while any kind keeps something (a few dozen items, a lookup each); the look round — the
        // chests and the things lying within LookR m — only at the moment a kept thing is gone (a sphere of colliders, once)
        internal static float LookR = 20f;
        private static Dictionary<string, int> _had = new Dictionary<string, int>(); // prefab → how many of the kept things were in the bag at the last look
        private static readonly Dictionary<string, int> _let = new Dictionary<string, int>(); // prefab → how many the story may take away (Let)
        private static Player _whose;
        private static float _nextLook;

        // the story takes it (a talk's «!take»: it goes to the person): not a loss to undo
        internal static void Let(string prefab, int n) { int had; _let.TryGetValue(prefab, out had); _let[prefab] = had + Mathf.Max(0, n); }

        // a thing out of the player's bag on purpose — the story takes it, he gives or pays it, it goes into a heart's lantern: no loss to undo.
        // Every such taking goes through here: a kept thing taken any other way is put back by Tick, a second one (2026-11-15: the ember
        // put in a heart came back to the bag — one ember would have lit every heart)
        internal static bool Take(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null && p != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            if (d == null) return false;
            Inventory inv = p.GetInventory();
            string name = d.m_itemData.m_shared.m_name;
            int had = inv.CountItems(name);
            inv.RemoveItem(name, n);
            int gone = had - inv.CountItems(name);
            if (gone > 0) Let(prefab, gone); // (only what really left: a word for more would let a later real loss pass unreturned)
            return gone > 0;
        }
        internal static void Take(Player p, ItemDrop.ItemData it, int n)
        {
            if (p == null || it == null) return;
            if (it.m_dropPrefab != null && KeptWhy(it) != null) Let(it.m_dropPrefab.name, n); // (only a kept one is watched: no word kept for the rest)
            p.GetInventory().RemoveItem(it, n);
        }

        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Time.time < _nextLook) return;
            _nextLook = Time.time + 0.5f;
            Wire();
            if (Keeps.Count == 0) return;
            Dictionary<string, int> now = KeptIn(p.GetInventory());
            if (p != _whose) { _whose = p; _had = now; _let.Clear(); return; } // (another character, another world: nothing to compare with)
            foreach (KeyValuePair<string, int> h in _had)
            {
                int n, let;
                now.TryGetValue(h.Key, out n);
                int gone = h.Value - n;
                if (gone > 0) gone = h.Value - Mathf.Max(n, InBag(p.GetInventory(), h.Key)); // (still in the bag, no longer kept — its «until» came: not gone, never put back a second time)
                if (gone <= 0) continue;
                _let.TryGetValue(h.Key, out let);
                int ok = Mathf.Min(let, gone); _let[h.Key] = let - ok; gone -= ok;
                if (gone <= 0) continue;
                gone -= Mathf.Min(gone, Around(p, h.Key)); // (moved into a chest near, or lying near: still in the world)
                if (gone > 0) n += Back(p, h.Key, gone);
                now[h.Key] = n;
            }
            _had = now;
        }

        // how many of it are in the bag, kept or not (only when one kept seems gone; StoryLost: a stage's thing carried)
        internal static int InBag(Inventory inv, string prefab)
        {
            int n = 0;
            if (inv != null) foreach (ItemDrop.ItemData it in inv.GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) n += it.m_stack;
            return n;
        }

        private static Dictionary<string, int> KeptIn(Inventory inv)
        {
            Dictionary<string, int> l = new Dictionary<string, int>();
            if (inv == null) return l;
            foreach (ItemDrop.ItemData it in inv.GetAllItems())
            {
                if (it.m_dropPrefab == null || KeptWhy(it) == null) continue;
                int had; l.TryGetValue(it.m_dropPrefab.name, out had); l[it.m_dropPrefab.name] = had + it.m_stack;
            }
            return l;
        }

        // how many of it are in the chests within LookR m and lying there. Every loaded chest, not those a sphere touches: a person's gear cells are a chest
        // on a part of its body with no collider of its own (Npcs: «Gear»), and the review of 2026-11-14 doubled the necklace through them.
        // Heavy (all the loaded chests, a sphere for what lies about): only the moment a kept thing is gone from the bag, once; and StoryLost's look, once in a while
        private static int Around(Player p, string prefab) { return Around(p.transform.position, LookR, prefab); }
        internal static int Around(Vector3 at, float r, string prefab)
        {
            int n = 0;
            HashSet<ItemDrop> lying = new HashSet<ItemDrop>();
            foreach (Collider c in Physics.OverlapSphere(at, r)) // heavy: once, the moment a kept thing is gone (StoryLost: once its Every s)
            {
                ItemDrop d = c != null ? c.GetComponentInParent<ItemDrop>() : null;
                if (d != null && lying.Add(d) && d.m_itemData != null && Utils.GetPrefabName(d.gameObject) == prefab) n += d.m_itemData.m_stack;
            }
            foreach (Container k in UnityEngine.Object.FindObjectsOfType<Container>()) // heavy: the same once
            {
                #if RAVEN_STORY
                if (k == null || k.GetInventory() == null || StoryWhere.Apart(at, k.transform.position) > r) continue; // (a chest inside, over its door, is not «near» one on the ground: the review 2026-11-18)
#else
                if (k == null || k.GetInventory() == null || Utils.DistanceXZ(k.transform.position, at) > r) continue; // flat: no insides without the story
#endif
                foreach (ItemDrop.ItemData it in k.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) n += it.m_stack;
            }
            return n;
        }

        // the lost ones back in the bag (no room: at the feet); how many
        private static int Back(Player p, string prefab, int n)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) return 0;
            ItemDrop.ItemData d = drop.m_itemData.Clone();
            d.m_dropPrefab = go; d.m_stack = n;
            if (!p.GetInventory().AddItem(d)) ItemDrop.DropItem(d, n, p.transform.position + p.transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.identity);
            p.Message(MessageHud.MessageType.Center, KeptWhy(d) ?? "");
            Debug.Log("[RavenWatch] a kept thing came back to the bag: " + prefab + " × " + n + " (gone, and in no chest nor on the ground near)");
            return n;
        }

        internal static bool AnyNow() { Wire(); for (int i = 0; i < Kinds.Count; i++) if (Kinds[i].Any == null || Kinds[i].Any()) return true; return false; }

        internal static Sprite IconOf(ItemDrop.ItemData it)
        {
            for (int i = 0; i < Kinds.Count; i++)
            {
                Kind k = Kinds[i];
                if (k.Icon == null || (k.Any != null && !k.Any())) continue;
                Sprite s = k.Icon(it);
                if (s != null) return s;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class ItemMarksTooltip
    {
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            try
            {
                ItemMarks.Wire();
                foreach (ItemMarks.Kind k in ItemMarks.Kinds)
                {
                    if (k.Line == null || (k.Any != null && !k.Any())) continue;
                    string line = k.Line(item);
                    if (!string.IsNullOrEmpty(line)) __result += "\n\n" + line;
                }
            }
            catch (Exception e) { PatchGuard.Fail("ItemMarksTooltip", e); }
        }
    }

    // a small picture in the slot's corner (each kind's own); hidden on every other slot
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class ItemMarksSlots
    {
        private const string Name = "RavenWatch_ItemMark";
        private static readonly Dictionary<InventoryGrid, bool[]> Shown = new Dictionary<InventoryGrid, bool[]>(); // a grid's slots with a mark up (a grid not here yet: each slot looked at once)
        private static readonly List<Sprite> Want = new List<Sprite>();

        private static void Postfix(InventoryGrid __instance, Inventory ___m_inventory, List<InventoryElement> ___m_elements)
        {
            try
            {
                bool any = ItemMarks.AnyNow();
                bool[] was;
                Shown.TryGetValue(__instance, out was);
                if (!any && was == null) return; // (nothing up, nothing to show)
                if (___m_inventory == null || ___m_elements == null) return;
                int width = ___m_inventory.GetWidth();
                Want.Clear();
                for (int i = 0; i < ___m_elements.Count; i++) Want.Add(null);
                if (any)
                    foreach (ItemDrop.ItemData it in ___m_inventory.GetAllItems())
                    {
                        int idx = it.m_gridPos.y * width + it.m_gridPos.x;
                        if (idx >= 0 && idx < Want.Count) Want[idx] = ItemMarks.IconOf(it);
                    }
                int up = 0;
                bool[] now = was != null && was.Length == ___m_elements.Count ? was : new bool[___m_elements.Count];
                bool full = was == null || was.Length != ___m_elements.Count; // (a grid not seen yet: every slot once — a mark left up must come down)
                for (int i = 0; i < ___m_elements.Count; i++)
                {
                    InventoryElement el = ___m_elements[i];
                    if (el == null || (Want[i] == null && !now[i] && !full)) continue; // (no mark, none up: nothing to touch — most slots, most frames)
                    Transform t = el.transform.Find(Name);
                    now[i] = Want[i] != null;
                    if (Want[i] == null) { if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                    if (t == null) t = Make(el);
                    Image img = t.GetComponent<Image>();
                    if (img.sprite != Want[i]) img.sprite = Want[i];
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    up++;
                }
                if (up > 0) Shown[__instance] = now; else Shown.Remove(__instance);
                if (Shown.Count > 16) { List<InventoryGrid> gone = new List<InventoryGrid>(); foreach (InventoryGrid g in Shown.Keys) if (g == null) gone.Add(g); foreach (InventoryGrid g in gone) Shown.Remove(g); } // (grids destroyed with their windows)
            }
            catch (Exception e) { PatchGuard.Fail("ItemMarksSlots", e); }
        }

        private static Transform Make(InventoryElement el)
        {
            GameObject g = new GameObject(Name, typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(el.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // the top left corner
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(2f, -2f);
            rt.sizeDelta = new Vector2(22f, 22f);
            Image img = g.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = Color.white;
            return rt;
        }
    }

    // death: what stays with you is out of the bag while the game makes the grave, and back in it after (only the local player's own death)
    [HarmonyPatch(typeof(Player), "CreateTombStone")]
    internal static class ItemMarksKeepOnDeath
    {
        private static readonly List<ItemDrop.ItemData> Held = new List<ItemDrop.ItemData>();
        private static void Prefix(Player __instance)
        {
            Held.Clear();
            try
            {
                ItemMarks.Wire();
                if (__instance != Player.m_localPlayer || ItemMarks.Keeps.Count == 0) return;
                Inventory inv = __instance.GetInventory();
                foreach (ItemDrop.ItemData it in inv.GetAllItems()) if (ItemMarks.KeptWhy(it) != null) Held.Add(it);
                foreach (ItemDrop.ItemData it in Held) inv.RemoveItem(it);
            }
            catch (Exception e) { PatchGuard.Fail("ItemMarksKeepOnDeath", e); }
        }
        private static void Finalizer(Player __instance)
        {
            try
            {
                if (Held.Count == 0) return;
                Inventory inv = __instance.GetInventory();
                foreach (ItemDrop.ItemData it in Held) if (!inv.AddItem(it)) Debug.LogWarning("[RavenWatch] a kept item had no room after death: " + it.m_shared.m_name);
                Held.Clear();
            }
            catch (Exception e) { PatchGuard.Fail("ItemMarksKeepOnDeath", e); }
        }
    }

    // not hung on an item stand (a stand is no chest: ItemMarks.Around would not find it there)
    [HarmonyPatch(typeof(ItemStand), "CanAttach")]
    internal static class ItemMarksKeepOffStands
    {
        private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
        {
            try { if (ItemMarks.KeptWhy(item) == null) return true; __result = false; return false; }
            catch (Exception e) { PatchGuard.Fail("ItemMarksKeepOffStands", e); return true; }
        }
    }

    // a drop refused: it stays in the bag, why in the middle of the screen
    [HarmonyPatch(typeof(Humanoid), "DropItem")]
    internal static class ItemMarksKeepOnDrop
    {
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            try
            {
                if (__instance != Player.m_localPlayer) return true;
                string why = ItemMarks.KeptWhy(item);
                if (why == null) return true;
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, why);
                __result = false;
                return false;
            }
            catch (Exception e) { PatchGuard.Fail("ItemMarksKeepOnDrop", e); return true; }
        }
    }
}
#endif

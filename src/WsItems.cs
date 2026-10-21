#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Предмет»: a Workshop model marked "use: item" becomes an item of the game. Two kinds:
    //   a new item ("replaces:" empty): its own prefab RavenWatch_item_<id>, a thing you carry (a material for now),
    //     with its name, words (the model's note), stack, weight, and a recipe (cost) at the station named (a workbench,
    //     a forge… or a raven table);
    //   a new look for an item there is ("replaces: RavenWatch_FateStone"): that item keeps what it does, only its look
    //     and its icon are the model's.
    // In the mod: the models built into it (assets/items/*.txt); for testing also the Workshop's files (config
    // "Perches from workshop files", as the perches). The item's own shape (its mesh) is hidden and the model put in
    // its place, centred where the item lies, with a box to roll on round it.
    // A thing for a raven (a model marked "use: gear", RavenGear) is an item too: RavenWatch_gear_<id>, one to a stack,
    // made at a raven table (the one its model names, else the first), given to a raven in the book (Outfits.PutOn).
    internal static class WsItems
    {
        internal const string Use = "item";
        internal const string PrefabPrefix = "RavenWatch_item_";
        private const string LookName = "RavenWatch_Look";

        // the items there are whose look may be made anew (prefab, name in the Workshop)
        internal static readonly string[] Replaceable = { RavenItem.RerollPrefab, RavenItem.LetterPrefab };
        internal static string[] ReplaceableNames { get { return Lang.Arr(ReplaceableNamesSrc); } } // (translated on every read)
        private static readonly string[] ReplaceableNamesSrc = Lang.Keys("Камінь долі ворона", "Пергамент");

        private sealed class Def
        {
            internal WsModel Model;
            internal string Prefab;
            internal bool Replace;      // a new look for an item there is
            internal GameObject Built;  // a new item's prefab
            internal GameObject Dressed; // the prefab its look was put on
            internal bool Gear;         // a thing for a raven (use: gear)
            internal bool IconDone;
        }

        private static readonly List<Def> Defs = new List<Def>();
        private static bool _loaded;
        private static GameObject _holder;

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + s); }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Defs.Clear();
            List<WsModel> models = new List<WsModel>();
            try
            {
                Assembly asm = typeof(WsItems).Assembly;
                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith("RavenWatch.items.", StringComparison.Ordinal) && !res.StartsWith("RavenWatch.gear.", StringComparison.Ordinal)) continue;
                    using (Stream st = asm.GetManifestResourceStream(res))
                    using (StreamReader rd = new StreamReader(st, Encoding.UTF8)) { WsModel m = WsFiles.FromText(rd.ReadToEnd()); m.File = res; models.Add(m); }
                }
            }
            catch (Exception e) { Log("built-in: " + e.Message); }
            if (Perches.FromFiles) foreach (string f in WsFiles.List()) { WsModel m = WsFiles.Load(f); if (m != null) models.Add(m); }
            Dictionary<string, Def> by = new Dictionary<string, Def>();
            foreach (WsModel m in models)
            {
                bool gear = m.Use == RavenGear.Use;
                if ((m.Use != Use && !gear) || m.Parts.Count == 0) continue;
                if (gear && RavenGear.SlotOf(m) == RavenGear.Head) continue; // our own hats are not made: opened like the game's (Outfits)
                Def d = new Def { Model = m, Gear = gear, Replace = !gear && !string.IsNullOrEmpty(m.Replaces) };
                string id = string.IsNullOrEmpty(m.Id) || m.Id == Perches.IdFor("") ? Perches.IdFor(m.Name) : m.Id;
                d.Prefab = d.Replace ? m.Replaces : gear ? RavenGear.PrefabOf(RavenGear.KeyPrefix + id) : PrefabPrefix + id;
                Def old;
                if (by.TryGetValue(d.Prefab, out old)) Defs.Remove(old); // a file wins over the built-in one
                by[d.Prefab] = d;
                Defs.Add(d);
            }
            if (Defs.Count > 0) Log(Defs.Count + " item model(s)" + (Perches.FromFiles ? " (with the Workshop's files)" : ""));
        }

        // ---------------------------------------------------------------- the new items' prefabs

        private static GameObject Make(Def d, Func<string, GameObject> find)
        {
            if (d.Built != null) return d.Built;
            GameObject source = find("Thunderstone") ?? find("Resin") ?? find("Feathers");
            if (source == null) return null;
            if (_holder == null)
            {
                _holder = new GameObject("RavenWatch_ItemPrefabs");
                _holder.SetActive(false); // the templates stay out of the world
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            GameObject go = UnityEngine.Object.Instantiate(source, _holder.transform, false);
            go.name = d.Prefab;
            ItemDrop drop = go.GetComponent<ItemDrop>();
            if (drop == null) { UnityEngine.Object.Destroy(go); return null; }
            drop.m_itemData.m_dropPrefab = go;
            ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared = Copy(drop.m_itemData.m_shared);
            WsModel m = d.Model;
            sh.m_name = Lang.W(m.Name);
            sh.m_description = Lang.WordF("rw_ws_" + d.Prefab + "_desc", () => d.Gear ? RavenGear.Description(m) : Lang.T(m.Note ?? ""));
            sh.m_itemType = d.Gear ? ItemDrop.ItemData.ItemType.Misc : ItemDrop.ItemData.ItemType.Material;
            sh.m_maxStackSize = d.Gear ? 1 : m.Stack > 0 ? m.Stack : 20;
            sh.m_weight = m.Weight > 0f ? m.Weight : 0.5f;
            sh.m_value = 0;
            sh.m_teleportable = true;
            sh.m_questItem = false;
            sh.m_consumeStatusEffect = null;
            // the source's own glow and sparks (the thunderstone's) are not this item's
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps.gameObject == go ? (UnityEngine.Object)ps : ps.gameObject);
            foreach (Light l in go.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l.gameObject == go ? (UnityEngine.Object)l : l.gameObject);
            d.Built = go;
            return go;
        }

        // its own copy of the shared data (the source item's stays as it was)
        private static ItemDrop.ItemData.SharedData Copy(ItemDrop.ItemData.SharedData s)
        {
            MethodInfo clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return (ItemDrop.ItemData.SharedData)clone.Invoke(s, null);
        }

        // ObjectDB (the menu's too): the new items are known before a character is loaded (or they would vanish from it)
        internal static void RegisterItems(ObjectDB db)
        {
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;
            Load();
            bool added = false;
            foreach (Def d in Defs)
            {
                if (d.Replace) continue;
                GameObject g = Make(d, n => db.GetItemPrefab(n));
                if (g != null && !db.m_items.Contains(g)) { db.m_items.Add(g); added = true; }
            }
            MethodInfo update = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
            if (added && update != null) update.Invoke(db, null);
            DressAll();
            Recipes(db);
        }

        // the world's prefab list: dropped ones exist for everyone; the looks need the world's pieces, so they are put on here
        internal static void RegisterNet(ZNetScene scene)
        {
            if (scene == null || scene.m_prefabs == null) return;
            Load();
            foreach (Def d in Defs)
            {
                if (d.Replace) continue;
                GameObject g = Make(d, n => scene.m_prefabs.Find(x => x != null && x.name == n));
                if (g != null && !scene.m_prefabs.Contains(g)) scene.m_prefabs.Add(g);
            }
        }

        // ---------------------------------------------------------------- the look

        private static GameObject Target(Def d)
        {
            if (!d.Replace) return d.Built;
            if (d.Model.Replaces == RavenItem.RerollPrefab && RavenItem.Reroll != null) return RavenItem.Reroll;
            if (d.Model.Replaces == RavenItem.LetterPrefab && RavenItem.Letter != null) return RavenItem.Letter;
            ObjectDB db = ObjectDB.instance;
            return db != null ? db.GetItemPrefab(d.Model.Replaces) : null;
        }

        internal static void DressAll()
        {
            if (ZNetScene.instance == null) return; // the pieces the models are made of are the world's
            foreach (Def d in Defs)
            {
                GameObject g = Target(d);
                if (g == null || g == d.Dressed) continue;
                try { Dress(g, d.Model); d.Dressed = g; d.IconDone = false; }
                catch (Exception e) { Log(d.Model.Name + ": " + e.Message); }
            }
        }

        // the item's own shape hidden, the model in its place: centred where the item lies, a box round it to roll on
        private static void Dress(GameObject g, WsModel m)
        {
            Transform old = g.transform.Find(LookName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.enabled = false;
            foreach (LODGroup lg in g.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false;
            GameObject look = new GameObject(LookName);
            look.transform.SetParent(g.transform, false);
            foreach (WsPart p in m.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(look.transform, p, false);
            foreach (Transform t in look.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = g.layer;
            // where the model is, in the item's own frame (from the meshes: the prefab is not in the world, its bounds are empty)
            Bounds b = new Bounds();
            bool any = false;
            Matrix4x4 toItem = g.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 k = toItem * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = k.MultiplyPoint3x4(c);
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            if (!any) return;
            look.transform.localPosition -= b.center; // its middle where the item's middle is
            foreach (Collider c in g.GetComponents<Collider>()) if (!c.isTrigger) UnityEngine.Object.DestroyImmediate(c);
            BoxCollider box = g.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(0.05f, b.size.x), Mathf.Max(0.05f, b.size.y), Mathf.Max(0.05f, b.size.z));
        }

        // ---------------------------------------------------------------- icons and recipes (once the world is up)

        internal static void Spawned()
        {
            DressAll();
            Icons();
            Recipes(ObjectDB.instance);
        }

        // each item's own photo (the tab «Фото»), or photographed by itself
        private static void Icons()
        {
            foreach (Def d in Defs)
            {
                if (d.IconDone || d.Dressed == null) continue;
                d.IconDone = true;
                try
                {
                    Sprite s = IconPhotos.Get(IconPhotos.KeyOf(d.Model));
                    Transform look = s == null ? d.Dressed.transform.Find(LookName) : null;
                    if (look != null) s = Perches.Photo(look);
                    // on the prefab and on the ones already in the bag (they may hold their own copy of the look: the
                    // source's icon — a Thunderstone's — stayed on a pouch in the bag while the book showed the photo)
                    if (s != null && d.Dressed.GetComponent<ItemDrop>() != null) RavenItem.UseIcon(d.Dressed, s);
                }
                catch (Exception e) { Log("icon " + d.Model.Name + ": " + e.Message); }
            }
        }

        // a new photo taken (or the own one thrown away): the item's picture again
        internal static void IconAgain(string id)
        {
            bool any = false;
            foreach (Def d in Defs) if (IconPhotos.KeyOf(d.Model) == id) { d.IconDone = false; any = true; }
            if (any) Icons();
        }

        // a new item is made at its station (a workbench when none is named; a raven table by its prefab)
        private static void Recipes(ObjectDB db)
        {
            if (db == null || db.m_recipes == null || ZNetScene.instance == null) return;
            foreach (Def d in Defs)
            {
                if (d.Replace || d.Built == null) continue;
                string name = "Recipe_" + d.Prefab;
                if (db.m_recipes.Exists(x => x != null && x.name == name)) continue;
                string st = d.Gear ? RavenGear.TableOf(d.Model) : string.IsNullOrEmpty(d.Model.Station) ? "piece_workbench" : d.Model.Station;
                if (st == null) { Log(d.Model.Name + ": no raven table to make it at (yet)"); continue; }
                GameObject sp = ZNetScene.instance.GetPrefab(st);
                CraftingStation station = sp != null ? sp.GetComponent<CraftingStation>() : null;
                if (station == null) { Log(d.Model.Name + ": no crafting station " + st + " (yet)"); continue; }
                Recipe r = ScriptableObject.CreateInstance<Recipe>();
                r.name = name;
                r.m_item = d.Built.GetComponent<ItemDrop>();
                r.m_amount = 1;
                r.m_enabled = true;
                r.m_craftingStation = station;
                r.m_minStationLevel = 1;
                r.m_resources = Perches.Costs(db, d.Model.Cost);
                db.m_recipes.Add(r);
            }
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class WsItemsDbPatch
    {
        [HarmonyPriority(Priority.Low)] // after the raven items (a new look may be for one of them)
        private static void Postfix(ObjectDB __instance) { try { WsItems.RegisterItems(__instance); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + e); } }
    }

    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    internal static class WsItemsMenuDbPatch
    {
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(ObjectDB __instance) { try { WsItems.RegisterItems(__instance); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + e); } }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class WsItemsNetPatch
    {
        private static void Prefix(ZNetScene __instance) { try { WsItems.RegisterNet(__instance); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + e); } }
        private static void Postfix() { try { WsItems.DressAll(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + e); } }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class WsItemsSpawnPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            try { WsItems.Spawned(); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop items: " + e); }
        }
    }
}
#endif

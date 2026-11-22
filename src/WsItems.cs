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
    // An item the story puts in someone's hand may say in its file how it hangs, lies and falls (hang:, lies:, falls: — kept on its model as
    // written; StoryProps.OwnOf reads them through ModelOf).
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
            internal GameObject Grow, GrowDressed; // «grows:»: the plant picked by hand (MakeGrow), and the one its look was put on
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
            _story = null; _one = null; // (the story's things and the ones of a kind read anew with them)
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
                if (by.TryGetValue(d.Prefab, out old))
                {
                    Defs.Remove(old); // a file wins over the built-in one — for its look: the mod's words and the story's rules stay the mod's (the user
                    // 2026-11-15: a Workshop copy of the Watch's feather from 10-31 came with no «story:», an old note and Del in its menu)
                    WsModel b = old.Model;
                    if (b.File.StartsWith("RavenWatch.", StringComparison.Ordinal) && !m.File.StartsWith("RavenWatch.", StringComparison.Ordinal))
                    {
                        m.Name = b.Name; m.Note = b.Note; m.Story = b.Story; m.Pickup = b.Pickup; m.One = b.One; m.Wear = b.Wear; m.Dreams = b.Dreams;
                        Log("«" + m.File + "»: its look over the built-in «" + id + "»; the name, note and story rules are the mod's");
                    }
                }
                by[d.Prefab] = d;
                Defs.Add(d);
            }
            if (Defs.Count > 0) Log(Defs.Count + " item model(s)" + (Perches.FromFiles ? " (with the Workshop's files)" : ""));
        }

        // ---------------------------------------------------------------- the story's things (a model's «story:» line: the user 2026-11-14)
        // its words under «Сюжетний предмет» (for whom: «для Кетіля»; «yes» — none), null: not one of the story's. By its prefab, once a load.
        // «story: для Кетіля until world ketil_south» (the user 2026-11-14: the necklace he left on his bed is a thing as any, to lay down or throw
        // away): the story's only till those conditions of the world hold — then an item as any (no mark, dropped, hung on a stand, lost on death).
        // Asked of the story (StoryOver: StoryModules) at most once an OverEvery s an item, its slot drawn each frame
        private static Dictionary<string, string> _story, _until;
        internal static Func<string, bool> StoryOver; // (conditions of the world → they hold; a build with no story: none, never over)
        internal static float OverEvery = 1f;
        private static readonly Dictionary<string, bool> _over = new Dictionary<string, bool>();
        private static float _overAt;
        internal static string StoryOf(ItemDrop.ItemData it)
        {
            if (it == null || it.m_dropPrefab == null) return null;
            if (_story == null)
            {
                Load();
                _story = new Dictionary<string, string>();
                _until = new Dictionary<string, string>();
                foreach (Def d in Defs)
                {
                    if (d.Replace || string.IsNullOrEmpty(d.Model.Story)) continue;
                    string s0 = " " + d.Model.Story.Trim();
                    int u = s0.IndexOf(" until ", StringComparison.Ordinal);
                    if (u >= 0) { _until[d.Prefab] = s0.Substring(u + 7).Trim(); s0 = s0.Substring(0, u); }
                    s0 = s0.Trim();
                    _story[d.Prefab] = s0 == "yes" ? "" : s0;
                }
            }
            string s, until;
            string prefab = it.m_dropPrefab.name;
            if (!_story.TryGetValue(prefab, out s)) return null;
            return _until.TryGetValue(prefab, out until) && Over(prefab, until) ? null : s;
        }
        private static bool Over(string prefab, string until)
        {
            if (Time.time >= _overAt) { _over.Clear(); _overAt = Time.time + OverEvery; }
            bool over;
            if (!_over.TryGetValue(prefab, out over)) _over[prefab] = over = StoryOver != null && StoryOver(until);
            return over;
        }

        // its mark (ItemMarks): a gold sign in the slot's corner, a gold line in the tooltip; it stays with you (death, a drop)
        internal static void StoryMarks()
        {
            ItemMarks.Kinds.Add(new ItemMarks.Kind
            {
                Name = "story",
                Icon = it => StoryOf(it) != null ? StorySign() : null,
                Line = it => { string s = StoryOf(it); return s == null ? null : "<color=#E8C04A>" + Lang.T("Сюжетний предмет") + (s.Length > 0 ? " · " + Lang.T(s) : "") + "</color>"; }
            });
            ItemMarks.Keeps.Add(it => StoryOf(it) != null ? Lang.T("Це сюжетна річ: її не викинути.") : null);
        }

        // the sign: a small gold lozenge with a dark edge, drawn once (no picture of the game's or another's)
        private static Sprite _sign;
        private static Sprite StorySign()
        {
            if (_sign != null) return _sign;
            const int n = 32;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color gold = new Color(0.91f, 0.75f, 0.29f, 1f), edge = new Color(0.16f, 0.11f, 0.04f, 1f), none = new Color(0f, 0f, 0f, 0f);
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Abs(x - c) + Mathf.Abs(y - c) * 1.25f; // (a lozenge, a little taller than wide)
                    tex.SetPixel(x, y, d <= c - 4f ? gold : d <= c - 1f ? edge : none);
                }
            tex.Apply();
            _sign = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f));
            return _sign;
        }

        // the model an item of ours is made from, by its prefab (StoryProps reads a held thing's own lines from it: hang:, lies:, falls:); null: not ours
        internal static WsModel ModelOf(string prefab)
        {
            Load();
            foreach (Def d in Defs) if (d.Prefab == prefab) return d.Model;
            return null;
        }

        // ---------------------------------------------------------------- one in the world (a model's «one: yes»: what keeps it one — StoryOne)
        // its prefabs, once a load (asked of every item StoryOne looks at: a lookup); its prefab gets StoryOneDrop as it is made (Make)
        private static HashSet<string> _one;
        internal static bool IsOne(string prefab)
        {
            if (prefab == null) return false;
            if (_one == null) { Load(); _one = new HashSet<string>(); foreach (Def d in Defs) if (!d.Replace && !d.Gear && d.Model.One) _one.Add(d.Prefab); }
            return _one.Contains(prefab);
        }
        internal static IEnumerable<string> Ones() { IsOne(""); return _one; }
        internal static bool AnyOne() { IsOne(""); return _one.Count > 0; }

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
            sh.m_description = Lang.WordF("rw_ws_" + d.Prefab + "_desc", () => d.Gear ? RavenGear.Description(m) : SoldTop(m) + Lang.T(m.Note ?? ""));
            sh.m_itemType = d.Gear ? ItemDrop.ItemData.ItemType.Misc : m.Wear == "trinket" ? ItemDrop.ItemData.ItemType.Trinket : ItemDrop.ItemData.ItemType.Material;
            // («wear: trinket»: the game's trinket slot — no adrenaline of its own (none to fill, no power when full), nothing hung on the body: the
            // game shows only an item's «attach_» children, ours has none — the user 2026-11-21: Sverre's ring worn as a trinket)
            if (m.Wear == "trinket") { sh.m_maxAdrenaline = 0f; sh.m_fullAdrenalineSE = null; }
            sh.m_maxStackSize = d.Gear || m.One ? 1 : m.Stack > 0 ? m.Stack : 20; // (one of a kind: never two in a stack — the game stacks with no look at an item's data)
            sh.m_weight = m.Weight > 0f ? m.Weight : 0.5f;
            sh.m_value = 0;
            sh.m_teleportable = true;
            drop.m_autoPickup = m.Pickup != "hand"; // («pickup: hand»: taken only by E — the prefab's own, so every game's copy: Player.AutoPickup asks it)
            // a thing of the story («story:») never times out on the ground: the game's ItemDrop.TimedDestruction takes a dropped thing an hour
            // after it fell, outside a base — a scroll half left by Torvald's stash or the crypt, never picked up, was gone and its stage waited
            // for ever. The prefab's own, so every copy, and one loaded after a long time away too
            if (!string.IsNullOrEmpty(m.Story) || m.One) drop.m_autoDestroy = false; // (and one of a kind in the world: never gone by itself)
#if RAVEN_STORY
            if (m.One) go.AddComponent<StoryOneDrop>(); // (lying on the ground: an older or foreign copy crumbles, the live one is seen — StoryOne)
#endif
            sh.m_questItem = false;
            sh.m_consumeStatusEffect = null;
            // the source's own glow and sparks (the thunderstone's) are not this item's
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.DestroyImmediate(ps.gameObject == go ? (UnityEngine.Object)ps : ps.gameObject);
            foreach (Light l in go.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l.gameObject == go ? (UnityEngine.Object)l : l.gameObject);
            d.Built = go;
            return go;
        }

        // the names of the goods Haldor sells (the game's item names)
        internal static void SoldNames(List<string> into)
        {
            foreach (Def d in Defs)
                if (!d.Replace && d.Model.Sell > 0 && d.Built != null) into.Add(d.Built.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
        }

        // a sold item's first words, in gold: who sells it and from which boss on (the boss by the game's own name)
        private static readonly string[] BossNames = { "$enemy_eikthyr", "$enemy_gdking", "$enemy_bonemass", "$enemy_dragon", "$enemy_goblinking", "$enemy_seekerqueen", "$enemy_fader" }; // (as Stage.BossKeys)
        internal const string SoldInk = "#FFD54F";
        private static string SoldText(WsModel m)
        {
            if (m.Sell <= 0) return "";
            int i = Array.IndexOf(Stage.BossKeys, m.SellAfter);
            return (i >= 0 && i < BossNames.Length ? Lang.F("\n\nПродає Хальдор, щойно переможено: {0}.", Lang.Game(BossNames[i])) : Lang.T("\n\nПродає Хальдор.")).Trim();
        }

        private static string SoldTop(WsModel m)
        {
            string s = SoldText(m);
            return s.Length > 0 ? "<color=" + SoldInk + ">" + s + "</color>\n\n" : "";
        }

        // for a recipe's material by its game name: who sells it (plain text), or null when it is not a sold good.
        // Asked every frame by the open crafting panel: kept per language, made again once the goods are built
        private static readonly Dictionary<string, string> Sold = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> SoldWords = new Dictionary<string, string>(); // its own words, without the gold line
        private static string _soldLang;
        private static int _soldBuilt = -1;
        internal static string SoldNote(string name)
        {
            string words;
            return SoldNote(name, out words);
        }

        internal static string SoldNote(string name, out string words)
        {
            words = null;
            if (string.IsNullOrEmpty(name)) return null;
            string lang = Lang.Current + "/" + (Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "");
            int built = 0;
            foreach (Def d in Defs) if (!d.Replace && d.Model.Sell > 0 && d.Built != null) built++;
            if (lang != _soldLang || built != _soldBuilt)
            {
                _soldLang = lang;
                _soldBuilt = built;
                Sold.Clear();
                SoldWords.Clear();
                foreach (Def d in Defs)
                {
                    if (d.Replace || d.Model.Sell <= 0 || d.Built == null) continue;
                    string n = d.Built.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
                    Sold[n] = SoldText(d.Model);
                    SoldWords[n] = Lang.T(d.Model.Note ?? "");
                }
            }
            string s;
            if (!Sold.TryGetValue(name, out s)) return null;
            SoldWords.TryGetValue(name, out words);
            return s;
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
                GameObject grow = g != null && d.Model.Grows > 0f ? MakeGrow(d, scene) : null;
                if (grow != null && !scene.m_prefabs.Contains(grow)) scene.m_prefabs.Add(grow);
            }
        }

        // ---------------------------------------------------------------- a plant picked by hand («grows: <m>», the user 2026-11-14: the flower Heid asks for,
        // ten of them kept at the black forest's edge — a stage's «stock:», StoryStock). The game's own thistle (GrowFrom: its picking, its sound, its colliders, the
        // picked state everyone sees) with its look hidden and the model in its place, standing on its foot (the model's lowest point at the prefab's
        // root, as tall as «grows:» says); it gives this item, one, and never grows again (a story's plant: so many in the world, no more). Nothing
        // hidden when picked and no growing back: the game takes a picked one out of the world (Pickable.SetPicked) — no hidden bush left behind for
        // ever, and StoryStock counts only what stands
        internal const string GrowPrefix = "RavenWatch_grow_";
        internal static string GrowFrom = "Pickable_Thistle";
        internal static string GrowOf(string itemPrefab) { return itemPrefab.StartsWith(PrefabPrefix, StringComparison.Ordinal) ? GrowPrefix + itemPrefab.Substring(PrefabPrefix.Length) : null; }
        private static GameObject MakeGrow(Def d, ZNetScene scene)
        {
            if (d.Grow != null) return d.Grow;
            GameObject source = scene.m_prefabs.Find(x => x != null && x.name == GrowFrom);
            if (source == null || source.GetComponent<Pickable>() == null) { Log(d.Model.Name + ": no «" + GrowFrom + "» to grow from"); return null; }
            GameObject go = UnityEngine.Object.Instantiate(source, _holder.transform, false);
            go.name = GrowOf(d.Prefab);
            Pickable pk = go.GetComponent<Pickable>();
            pk.m_itemPrefab = d.Built;
            pk.m_amount = 1;
            pk.m_respawnTimeMinutes = 0f;
            pk.m_hideWhenPicked = null; // (picked: gone from the world, not a hidden bush kept for ever — its look sits on the root: DressGrow)
            pk.m_extraDrops = new DropTable();
            pk.m_overrideName = d.Built.GetComponent<ItemDrop>().m_itemData.m_shared.m_name; // (what the hover says: this item's name, never the thistle's own)
            d.Grow = go;
            return go;
        }

        // its look: the game's thistle hidden, the model on the root, its foot there
        private static void DressGrow(GameObject g, WsModel m)
        {
            Transform under = g.transform;
            Transform old = under.Find(LookName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.enabled = false; // heavy: once a plant's prefab, as the world loads (DressAll)
            foreach (LODGroup lg in g.GetComponentsInChildren<LODGroup>(true)) lg.enabled = false; // heavy: the same once
            GameObject look = new GameObject(LookName);
            look.transform.SetParent(under, false);
            foreach (WsPart p in m.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(look.transform, p, false);
            foreach (Transform t in look.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = under.gameObject.layer; // heavy: the same once
            Bounds b;
            if (!BoundsIn(look, g.transform, out b) || b.size.y <= 0.001f) return;
            float k = m.Grows / b.size.y;
            look.transform.localScale = Vector3.one * k;
            if (!BoundsIn(look, g.transform, out b)) return;
            look.transform.position += g.transform.TransformVector(new Vector3(-b.center.x, -b.min.y, -b.center.z)); // its foot on the root: the plant stands on the ground it is set on
            if (_told.Add(m.Name + " grows")) Log(m.Name + ": grows " + b.size.y.ToString("0.00") + " m tall, " + b.size.x.ToString("0.00") + " × " + b.size.z.ToString("0.00") + " m across");
        }

        // where a look's meshes are, in the frame of «frame» (a prefab is not in the world: its renderers' bounds are empty)
        private static bool BoundsIn(GameObject look, Transform frame, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            Matrix4x4 to = frame.worldToLocalMatrix;
            foreach (MeshFilter mf in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 k = to * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = k.MultiplyPoint3x4(c);
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            return any;
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
                // the photo at once, not on the player's spawn: an item made in the world before that (lying near
                // the spawn, loaded with its zone) copies the prefab's look and kept the thunderstone's icon
                try { Sprite s = d.Dressed == g ? IconPhotos.Get(IconPhotos.KeyOf(d.Model)) : null; if (s != null && g.GetComponent<ItemDrop>() != null) RavenItem.UseIcon(g, s); }
                catch (Exception e) { Log("icon " + d.Model.Name + ": " + e.Message); }
            }
            foreach (Def d in Defs)
            {
                if (d.Grow == null || d.Grow == d.GrowDressed) continue;
                try { DressGrow(d.Grow, d.Model); d.GrowDressed = d.Grow; }
                catch (Exception e) { Log(d.Model.Name + " (grows): " + e.Message); }
            }
        }

        // an item holding its own copy of the look (made in the world, or copied by another mod) shows the prefab's icon
        internal static void FixIcon(ItemDrop.ItemData item)
        {
            GameObject pf = item.m_dropPrefab;
            if (pf == null || !pf.name.StartsWith(RavenGear.PrefabStart, StringComparison.Ordinal)) return;
            ItemDrop drop = pf.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData.m_shared == item.m_shared) return;
            Sprite[] icons = drop.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0 && item.m_shared.m_icons != icons) item.m_shared.m_icons = icons;
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
            if (!string.IsNullOrEmpty(m.Story) && g.transform.Find(GlintName) == null) StoryGlint(g); // (a thing of the story lying about: the story pieces' stronger glint — the user 2026-11-14: the feather «лежить та підсвічується»)
            else Glint(g);
            foreach (WsPart p in m.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(look.transform, p, false);
            foreach (Transform t in look.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = g.layer;
            // where the model is, in the item's own frame (from the meshes: the prefab is not in the world, its bounds are empty)
            Bounds b;
            if (!BoundsIn(look, g.transform, out b)) return;
            look.transform.localPosition -= b.center; // its middle where the item's middle is
            foreach (Collider c in g.GetComponents<Collider>()) if (!c.isTrigger) UnityEngine.Object.DestroyImmediate(c);
            BoxCollider box = g.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(MinBox, b.size.x), Mathf.Max(MinBox, b.size.y), Mathf.Max(MinBox, b.size.z));
            // a thin thing thrown from the hand went through the floor in one step of the physics (the user 2026-11-14: Ketil's necklace lay under the
            // stones): never thinner than MinBox, and its fall checked all the way, not step by step (a few items lying about: cheap)
            Rigidbody rb = g.GetComponent<Rigidbody>();
            if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // (the one mode a kinematic body takes too: a copy another game owns)
            if (_told.Add(m.Name)) Log(m.Name + ": a box " + box.size.x.ToString("0.00") + " × " + box.size.y.ToString("0.00") + " × " + box.size.z.ToString("0.00") + " m (the model " + b.size.x.ToString("0.00") + " × " + b.size.y.ToString("0.00") + " × " + b.size.z.ToString("0.00") + ")");
        }
        internal static float MinBox = 0.12f; // m: the thinnest side of a lying thing's box (the model's own may be a few mm: a necklace, a page)
        private static readonly HashSet<string> _told = new HashSet<string>(); // (each model's box said once a session)

        // Lying on the ground, ours sparkle a little — gold specks rising, a faint warm light close by — or a thing in
        // the grass is hard to find (the thunderstone's own sparks are taken off them in Make). Only an item in the
        // world shows it: the bag and the icons never build the item's object.
        private const string GlintName = "RavenWatch_Glint";
        internal static bool IsGlint(Component c) { return c != null && c.gameObject.name == GlintName; } // (a sign to the player, no lamp of the world: LocState.Lights leaves it on)
        private static void Glint(GameObject g)
        {
            if (g.transform.Find(GlintName) != null) return;
            foreach (ParticleSystem own in g.GetComponentsInChildren<ParticleSystem>(true)) return; // (one of its own already: the item kept its look)
            Glint(g, 1f);
        }

        // a story thing that must be found (LocTouch: the Hall's old letter, Munin's shield; an item of the story lying on the ground — Dress): the one look for all of them. StoryMore: × how many specks, StoryRise: × how high they go,
        // StoryBig: × each speck's size (the user 2026-11-06: Munin's shield lost in the grass — the specks must rise over it, ~1.5 m, and be seen)
        internal static float StorySize = 3f, StoryMore = 1.6f, StoryRise = 2.2f, StoryBig = 2f;
        internal static GameObject StoryGlint(GameObject g) { return Glint(g, StorySize, StoryMore, StoryRise, StoryBig); }

        // the gold of Glint as a small thread rising over a person (StoryCallMark): a few specks, straight up, about half a metre, no light
        internal static float CallRate = 11f, CallHigh = 0.8f, CallWide = 0.04f, CallSize = 1.7f; // (the user 2026-11-07: bigger — more specks, higher, each ×CallSize)
        internal static GameObject CallLine()
        {
            GameObject o = new GameObject(GlintName);
            ParticleSystem ps = o.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f * CallSize, 0.05f * CallSize);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.4f, 1f), new Color(1f, 0.97f, 0.75f, 1f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = CallRate;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = CallWide;
            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime; // (straight up, as high as asked in its life: CallHigh)
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            v.y = new ParticleSystem.MinMaxCurve(CallHigh / 1.4f, CallHigh / 1f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystemRenderer pr = o.GetComponent<ParticleSystemRenderer>();
            pr.material = FxLib.Glow(true);
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            return o;
        }

        // the same sparkle, stronger (s × specks and light; more × specks alone; rise × how high they go: their speed and their lift together)
        internal static GameObject Glint(GameObject g, float s, float more = 1f, float rise = 1f, float big = 1f)
        {
            GameObject o = new GameObject(GlintName);
            o.layer = g.layer;
            o.transform.SetParent(g.transform, false);
            ParticleSystem ps = o.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f * rise, 0.12f * rise);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f * big, 0.07f * big);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.4f, 1f), new Color(1f, 0.97f, 0.75f, 1f));
            main.gravityModifier = -0.04f * rise;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.RoundToInt(16 * s * more);
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 5f * s * more;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f * Mathf.Sqrt(s);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystemRenderer pr = o.GetComponent<ParticleSystemRenderer>();
            pr.material = FxLib.Glow(true);
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            Light l = o.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.8f, 0.45f);
            l.range = 1.3f * s;
            l.intensity = 0.6f * Mathf.Sqrt(s);
            l.shadows = LightShadows.None;
            LightLod lod = o.AddComponent<LightLod>();
            lod.m_lightDistance = 20f; lod.m_shadowLod = false;
            return o;
        }

        // ---------------------------------------------------------------- icons and recipes (once the world is up)

        // what Haldor sells of them now: (the item, its price) for those whose key this player has (Stage.Has: the world's or his own)
        internal static List<KeyValuePair<GameObject, int>> ForSale(Player p)
        {
            List<KeyValuePair<GameObject, int>> l = new List<KeyValuePair<GameObject, int>>();
            Load();
            foreach (Def d in Defs)
                if (!d.Replace && d.Built != null && d.Model.Sell > 0 && (d.Model.SellAfter.Length == 0 || Stage.Has(p, d.Model.SellAfter)))
                    l.Add(new KeyValuePair<GameObject, int>(d.Built, d.Model.Sell));
            return l;
        }

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
                if (d.Replace || d.Built == null || d.Model.Sell > 0) continue; // (Haldor sells it: made nowhere)
                if (string.Equals(d.Model.Cost, "none", StringComparison.OrdinalIgnoreCase)) continue; // (cost: none — never made: the story gives it, or the console)
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

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetIcon")]
    internal static class WsItemsIconPatch
    {
        private static void Prefix(ItemDrop.ItemData __instance) { try { WsItems.FixIcon(__instance); } catch { } }
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

    // Haldor's goods do not hide what needs them: while the game looks which recipes and pieces you know, they count as
    // known — so a perch or a raven's thing shows up (grey, the good's icon in its cost) as soon as you have held the
    // rest of its materials and know its station, before you ever bought the good. Only for that look: the good itself
    // stays unknown until you hold it (its «нова знахідка» still comes).
    // a recipe's material that Haldor sells says so on its own hover, in gold (the game's crafting and building panels)
    [HarmonyPatch(typeof(InventoryGui), "SetupRequirement")]
    internal static class WsSoldReqPatch
    {
        private static void Postfix(Transform elementRoot, Piece.Requirement req)
        {
            if (elementRoot == null || req == null || req.m_resItem == null) return;
            UITooltip tip = elementRoot.GetComponent<UITooltip>();
            string sold = WsItems.SoldNote(req.m_resItem.m_itemData.m_shared.m_name);
            if (tip == null || sold == null || string.IsNullOrEmpty(tip.m_text) || tip.m_text.Contains(sold)) return;
            tip.m_text += "\n<color=" + WsItems.SoldInk + ">" + sold + "</color>";
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
    internal static class WsSoldKnownPatch
    {
        private static readonly FieldInfo Known = AccessTools.Field(typeof(Player), "m_knownMaterial");
        private static readonly List<string> Sold = new List<string>();
        private static void Prefix(Player __instance, out List<string> __state)
        {
            __state = null;
            HashSet<string> known = Known != null ? Known.GetValue(__instance) as HashSet<string> : null;
            if (known == null) return;
            Sold.Clear();
            WsItems.SoldNames(Sold);
            foreach (string n in Sold)
                if (known.Add(n)) { if (__state == null) __state = new List<string>(); __state.Add(n); }
        }
        private static void Finalizer(Player __instance, List<string> __state) // (a finalizer: taken back even if the look threw)
        {
            if (__state == null) return;
            HashSet<string> known = Known.GetValue(__instance) as HashSet<string>;
            if (known != null) foreach (string n in __state) known.Remove(n);
        }
    }
}
#endif

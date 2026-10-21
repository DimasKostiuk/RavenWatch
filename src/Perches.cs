#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Жердини»: perches for the ravens, built with the hammer. Each is a model from the Workshop (Майстерня) marked
    // "use: perch" (with an id, a cost and one or two raven seats, @seat), turned into a piece of the game when the
    // world loads: the meshes copied from the game's own pieces, colliders round them, a ZNetView, a Piece and a
    // WearNTear. The released ones are inside the DLL (assets/perches/*.txt), the same for everyone; the Workshop's
    // own files count only with the config switch (testing: other players do not have them).
    //
    // E on a perch sends Hugin from your shoulder to sit there (Shift+E: Munin); E again calls him back. A raven on a
    // perch is away from you (Trust.Away): no gifts, nothing on the shoulder; it rests and gathers strength by itself,
    // up to the perch's day norm, faster in comfort (kept in the raven: RavenWatch_perch*). It flies there and back by
    // its motor (PerchFlight, ShoulderPin.ArriveFrom), however high or far the perch stands.
    // The perch keeps who sits on it in its ZDO (RavenWatch_seat<i>: owner id|munin|level|token|name|session|flip;
    // "o": outfit); everyone draws the sitting ravens from that. The raven keeps where it sits
    // (zdoid|seat|x|y|z|token|world|norm); the game renews object ids each run, so a stay ends with the session.
    internal static class Perches
    {
        internal const string Use = "perch";
        internal const string DefaultCost = "Wood:10";
        internal static bool FromFiles;                 // the Workshop's marked files too (testing; not shared)
        internal static float DailyCap = 30f;           // a perch's day norm when its model names none ("perday:")
        // The norm comes in over a whole game day under a bare roof; comfort speeds it up (+10% a level above 1, at
        // most x3); out in the open and not cosy (comfort under 3) only a fifth of the pace.
        internal static float ComfortStep = 0.1f, MaxMul = 3f, ColdMul = 0.2f;
        internal static int ColdComfort = 3;
        internal const string MulKey = "RavenWatch_perchMul", EnvKey = "RavenWatch_env";
        internal static float Reach = 80f;              // m: a raven flies to a perch this near

        internal const string PerchKey = "RavenWatch_perch", AtKey = "RavenWatch_perchAt", DayKey = "RavenWatch_perchDay", GotKey = "RavenWatch_perchGot";
        internal const string RpcSeat = "RavenWatch_PerchSeat", RpcGone = "RavenWatch_PerchGone", RpcSay = "RavenWatch_PerchSay", RpcTalkHere = "RavenWatch_PerchTalk";
        internal static string SeatKey(int i) { return "RavenWatch_seat" + i; }

        internal sealed class Def
        {
            internal string Id, Name, Note, Cost, Prefab;
            internal float PerDay;
            internal string Station;      // the crafting station's prefab (piece_workbench when none named)
            internal bool Wall;           // hung on a wall: placed only on walls, turned away from them
            internal WsModel Model;
            internal float[] Pose, Stand; // sitting (the usual) and standing (after landing, now and then)
            internal GameObject Built;
            internal bool IconDone;
            internal bool Table;          // «Стіл ворона» (use: station): a crafting station, no seats
            internal bool Nest;           // «Вороняче гніздо» (use: nest): the raven flies to Odin from it (Nest.cs)
            internal bool Tree;           // «Вороняче дерево» (use: tree): put in the world by the server, not built, never broken (RavenTree)
        }

        // «Стіл ворона»: a Workshop model marked "use: station" becomes a crafting station of its own (E opens the
        // crafting window, as at the workbench). Things for the ravens are made at it (RavenGear, later).
        internal const string TableUse = "station";
        internal const string NestUse = "nest", TreeUse = "tree";

        internal static readonly List<Def> Defs = new List<Def>();    // the perches
        internal static readonly List<Def> Tables = new List<Def>();  // the raven tables
        internal static readonly List<Def> Nests = new List<Def>();   // the ravens' nests (Odin's errands)
        internal static readonly List<Def> Trees = new List<Def>();   // the raven trees in the world (mixing runes)
        private static readonly List<Def> All = new List<Def>();      // both: built, put in the hammer, photographed alike
        private static readonly Dictionary<string, Def> ByPrefab = new Dictionary<string, Def>();
        private static GameObject _holder;
        private static bool _loaded;

        internal static Def DefOf(string prefab) { Def d; return prefab != null && ByPrefab.TryGetValue(prefab, out d) ? d : null; }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Perches: " + s); }

        // ---------------------------------------------------------------- the models

        // a prefab id from a name: Latin letters only, so the piece's name in the save never changes with the font
        internal static string IdFor(string name)
        {
            string[] from = { "а", "б", "в", "г", "ґ", "д", "е", "є", "ж", "з", "и", "і", "ї", "й", "к", "л", "м", "н", "о", "п", "р", "с", "т", "у", "ф", "х", "ц", "ч", "ш", "щ", "ь", "ю", "я", "'", "’" };
            string[] to = { "a", "b", "v", "h", "g", "d", "e", "ie", "zh", "z", "y", "i", "i", "i", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "f", "kh", "ts", "ch", "sh", "shch", "", "iu", "ia", "", "" };
            StringBuilder sb = new StringBuilder();
            foreach (char ch in (name ?? "").ToLowerInvariant())
            {
                string c = ch.ToString();
                int i = Array.IndexOf(from, c);
                if (i >= 0) { sb.Append(to[i]); continue; }
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            string id = sb.ToString().Trim('_');
            return id.Length > 0 ? id : "perch";
        }

        private static void LoadDefs()
        {
            if (_loaded) return;
            _loaded = true;
            Defs.Clear(); Tables.Clear(); Nests.Clear(); Trees.Clear(); All.Clear(); ByPrefab.Clear();
            List<WsModel> models = new List<WsModel>();
            try
            {
                System.Reflection.Assembly asm = typeof(Perches).Assembly;
                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith("RavenWatch.perches.", StringComparison.Ordinal)) continue;
                    using (Stream st = asm.GetManifestResourceStream(res))
                    using (StreamReader rd = new StreamReader(st, Encoding.UTF8)) { WsModel m = WsFiles.FromText(rd.ReadToEnd()); m.File = res; models.Add(m); }
                }
            }
            catch (Exception e) { Log("built-in models: " + e.Message); }
            if (FromFiles)
                foreach (string f in WsFiles.List()) { WsModel m = WsFiles.Load(f); if (m != null) models.Add(m); }
            foreach (WsModel m in models)
            {
                bool table = m.Use == TableUse, nest = m.Use == NestUse, tree = m.Use == TreeUse;
                if (m.Use != Use && !table && !nest && !tree) continue;
                if (!table && m.Seats == 0) { Log(m.Name + ": no raven seat, left out"); continue; }
                Def d = new Def();
                d.Table = table; d.Nest = nest; d.Tree = tree;
                d.Id = string.IsNullOrEmpty(m.Id) ? IdFor(m.Name) : m.Id;
                d.Prefab = (table ? "RavenWatch_table_" : nest ? "RavenWatch_nest_" : tree ? "RavenWatch_tree_" : "RavenWatch_perch_") + d.Id;
                d.Name = m.Name; d.Note = m.Note; d.Cost = string.IsNullOrEmpty(m.Cost) ? DefaultCost : m.Cost;
                d.Model = m; d.Pose = m.Pose; d.Stand = m.Stand; d.PerDay = m.PerDay > 0f ? m.PerDay : DailyCap; d.Wall = m.Wall; d.Station = string.IsNullOrEmpty(m.Station) ? "piece_workbench" : m.Station;
                Def old;
                if (ByPrefab.TryGetValue(d.Prefab, out old)) { Defs.Remove(old); Tables.Remove(old); Nests.Remove(old); Trees.Remove(old); All.Remove(old); } // a file of the same id wins over the built-in one
                ByPrefab[d.Prefab] = d;
                (table ? Tables : nest ? Nests : tree ? Trees : Defs).Add(d);
                All.Add(d);
            }
            Log(Defs.Count + " perch model(s), " + Tables.Count + " raven table(s), " + Nests.Count + " nest(s)" + (FromFiles ? " (with the Workshop's files)" : ""));
        }

        // ---------------------------------------------------------------- the pieces

        private static int PieceLayer { get { int l = LayerMask.NameToLayer("piece"); return l >= 0 ? l : 0; } }

        // the world loads (ZNetScene.Awake): the prefabs are made once and put in the game's list
        internal static void Register(ZNetScene scene)
        {
            // the Workshop's files may have changed since the last world: made again from them
            if (FromFiles && _loaded)
            {
                foreach (Def d in All) if (d.Built != null) UnityEngine.Object.Destroy(d.Built);
                _loaded = false;
            }
            LoadDefs();
            if (scene == null || All.Count == 0) return;
            if (_holder == null)
            {
                _holder = new GameObject("RavenWatch_Perches");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            foreach (Def d in All)
            {
                try
                {
                    if (d.Built == null) d.Built = Build(d, scene);
                    if (d.Built == null) continue;
                    int hash = d.Prefab.GetStableHashCode();
                    if (!scene.m_prefabs.Contains(d.Built)) scene.m_prefabs.Add(d.Built);
                    Dictionary<int, GameObject> named = Traverse.Create(scene).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
                    if (named != null) named[hash] = d.Built;
                }
                catch (Exception e) { Log(d.Name + ": " + e); }
            }
            try { BuildSpot(scene); } catch (Exception e) { Log("spot: " + e); }
            Hammer();
        }

        // «Сісти тут» (SitHere): an unseen perch of one seat where you point. No look, no colliders, not in the hammer,
        // not saved with the world (gone with whoever made it); no strength there (its day norm 0).
        private static GameObject _spot;
        private static void BuildSpot(ZNetScene scene)
        {
            if (_spot == null)
            {
                Def d = new Def { Id = SitHere.DefId, Name = Lang.T("Місце"), Prefab = SitHere.Prefab, PerDay = 0f };
                GameObject root = new GameObject(d.Prefab);
                root.transform.SetParent(_holder.transform, false);
                ZNetView nv = root.AddComponent<ZNetView>();
                nv.m_persistent = false;
                GameObject s = new GameObject("seat_0");
                s.transform.SetParent(root.transform, false);
                ByPrefab[d.Prefab] = d; // (before the view: its Awake looks its def up)
                root.AddComponent<PerchView>();
                root.AddComponent<SpotFollow>(); // (on another player: SitOn)
                d.Built = root;
                _spot = root;
            }
            if (!scene.m_prefabs.Contains(_spot)) scene.m_prefabs.Add(_spot);
            Dictionary<int, GameObject> named = Traverse.Create(scene).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
            if (named != null) named[SitHere.Prefab.GetStableHashCode()] = _spot;
        }

        // sitting on such a spot (not on a real perch)
        internal static bool OnSpot(ItemDrop.ItemData item) { Spot s = Where(item); return s != null && s.PerDay <= 0f; }

        // the hammer's list and the costs (ObjectDB): whichever comes later fills them in
        internal static void Hammer()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null) return;
            GameObject hammer = db.GetItemPrefab("Hammer");
            ItemDrop drop = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            PieceTable table = drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
            if (table != null) table.m_pieces.RemoveAll(g => g == null || ((g.name.StartsWith("RavenWatch_perch_", StringComparison.Ordinal) || g.name.StartsWith("RavenWatch_table_", StringComparison.Ordinal) || g.name.StartsWith("RavenWatch_nest_", StringComparison.Ordinal)) && DefOf(g.name) == null) || (DefOf(g.name) != null && DefOf(g.name).Built != g));
            foreach (Def d in All)
            {
                if (d.Built == null || d.Tree) continue; // (the trees are no pieces you build)
                Piece piece = d.Built.GetComponent<Piece>();
                if (piece.m_resources == null || piece.m_resources.Length == 0) piece.m_resources = Costs(db, d.Cost);
                if (table != null && !table.m_pieces.Contains(d.Built)) table.m_pieces.Add(d.Built);
            }
        }

        internal static Piece.Requirement[] Costs(ObjectDB db, string cost)
        {
            List<Piece.Requirement> list = new List<Piece.Requirement>();
            foreach (string w in (cost ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = w.Split(':');
                int n = 1;
                if (kv.Length > 1) int.TryParse(kv[1], out n);
                GameObject item = db.GetItemPrefab(kv[0]);
                ItemDrop id = item != null ? item.GetComponent<ItemDrop>() : null;
                if (id == null) { Log("cost: no item " + kv[0]); continue; }
                Piece.Requirement r = new Piece.Requirement();
                r.m_resItem = id; r.m_amount = Mathf.Max(1, n); r.m_recover = true;
                list.Add(r);
            }
            if (list.Count == 0 && db.GetItemPrefab("Wood") != null)
            {
                Piece.Requirement r = new Piece.Requirement();
                r.m_resItem = db.GetItemPrefab("Wood").GetComponent<ItemDrop>(); r.m_amount = 10; r.m_recover = true;
                list.Add(r);
            }
            return list.ToArray();
        }

        private static GameObject Build(Def d, ZNetScene scene)
        {
            GameObject root = new GameObject(d.Prefab);
            root.transform.SetParent(_holder.transform, false);
            root.layer = PieceLayer;
            ZNetView nv = root.AddComponent<ZNetView>();
            nv.m_persistent = true;
            Piece piece = root.AddComponent<Piece>();
            piece.m_name = Lang.W(d.Name);
            // the model's own words, then what it gives (from its norm, so the numbers never go stale), then the keys
            if (d.Nest)
                piece.m_description = Lang.WordF("rw_piece_" + d.Id + "_desc", () => Lang.F("{0}\nПосади в гніздо воронів і чекай: коли над ним засяє золото, Одін змилувався — зазирни в чашу внизу. Раз на день він дає доручення, а за виконані — руни.{1}", (string.IsNullOrEmpty(d.Note) ? Lang.T("Гніздо для воронів.") : Lang.T(d.Note).TrimEnd('.') + "."), Lang.T("\nСтавиться лише просто неба, не під дахом.")));
            else if (d.Table)
                piece.m_description = Lang.WordF("rw_piece_" + d.Id + "_desc", () => Lang.F("{0}\nБіля нього майструють, як за верстаком.", ((string.IsNullOrEmpty(d.Note) ? Lang.T("Стіл, за яким роблять речі для воронів.") : Lang.T(d.Note).TrimEnd('.') + ".")
                    + (d.Wall ? Lang.T("\nСтавиться лише на стіну.") : ""))));
            else
                piece.m_description = Lang.WordF("rw_piece_" + d.Id + "_desc", () => Lang.F("{0}\nНа ній ворон відпочиває й набирається сили: до {1} на день, під дахом і в затишку швидше.{2}\nВзаємодія — посадити Хугіна, із Shift — Муніна.", (string.IsNullOrEmpty(d.Note) ? Lang.T("Жердина для ворона.") : Lang.T(d.Note).TrimEnd('.') + "."), Mathf.RoundToInt(d.PerDay), (d.Wall ? Lang.T("\nСтавиться лише на стіну.") : "")));
            piece.m_icon = IconPhotos.Get(d.Id) ?? AbilityIcons.For("hugin", false); // its own photo when there is one (no camera needed); else photographed later (Icons)
            piece.m_category = d.Table ? Piece.PieceCategory.Crafting : Piece.PieceCategory.Furniture;
            piece.m_usage = BuildTab.Flag; // the hammer's own tab «RavenWatch» (BuildTab)
            piece.m_notOnFloor = d.Wall;
            GameObject bench = scene.GetPrefab(d.Station) ?? scene.GetPrefab("piece_workbench");
            if (bench != null && bench.GetComponent<CraftingStation>() == null) { Log(d.Name + ": " + d.Station + " is no crafting station, the workbench instead"); bench = scene.GetPrefab("piece_workbench"); }
            if (bench != null) piece.m_craftingStation = bench.GetComponent<CraftingStation>();

            GameObject look = new GameObject("new");
            look.transform.SetParent(root.transform, false);
            look.layer = PieceLayer;
            int parts = 0, seats = 0;
            bool ownBoxes = d.Model.Parts.Exists(x => WsFiles.IsBox(x.Src)); // (its own colliders: its parts get none)
            foreach (WsPart p in d.Model.Parts)
            {
                if (p.Src == WsFiles.Light) { MakeLight(look.transform, p); continue; }
                if (WsFiles.IsBox(p.Src))
                {
                    GameObject b = new GameObject(p.Src.Substring(1)); // "box", or "box:top" — a zone (NestView)
                    b.layer = PieceLayer;
                    b.transform.SetParent(look.transform, false);
                    b.transform.localPosition = p.Pos; b.transform.localRotation = Quaternion.Euler(p.Rot);
                    b.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.02f, Mathf.Abs(p.Scale.x)), Mathf.Max(0.02f, Mathf.Abs(p.Scale.y)), Mathf.Max(0.02f, Mathf.Abs(p.Scale.z)));
                    continue;
                }
                if (p.Src == WsFiles.Seat)
                {
                    if (d.Table) continue; // a table seats no raven
                    GameObject s = new GameObject("seat_" + seats++);
                    s.transform.SetParent(root.transform, false);
                    s.transform.localPosition = p.Pos; s.transform.localRotation = Quaternion.Euler(p.Rot);
                    continue;
                }
                if (Part(look.transform, p, !ownBoxes)) parts++;
            }
            if (parts == 0) { Log(d.Name + ": none of its parts are in the game"); UnityEngine.Object.Destroy(root); return null; }

            if (d.Tree)
            {
                piece.m_canBeRemoved = false; // (no hammer takes it down; no WearNTear: nothing breaks it)
                nv.m_distant = true;          // a landmark: loaded (and seen) from far away, as the game's big trees
                root.AddComponent<PerchView>(); // its seats: the ravens sit on it first (SeatZone), then its roots open the mixing
                root.AddComponent<TreeView>();
                Log(d.Name + " built as " + d.Prefab + ": " + parts + " parts (a raven tree)");
                return root;
            }
            WearNTear wnt = root.AddComponent<WearNTear>();
            wnt.m_new = look; wnt.m_worn = look; wnt.m_broken = look;
            wnt.m_health = 400f;
            wnt.m_materialType = WearNTear.MaterialType.Wood;
            GameObject like = scene.GetPrefab("piece_chair") ?? scene.GetPrefab("woodiron_pole");
            if (like != null)
            {
                Piece lp = like.GetComponent<Piece>();
                WearNTear lw = like.GetComponent<WearNTear>();
                if (lp != null) piece.m_placeEffect = lp.m_placeEffect;
                if (lw != null) { wnt.m_destroyedEffect = lw.m_destroyedEffect; wnt.m_hitEffect = lw.m_hitEffect; wnt.m_switchEffect = lw.m_switchEffect; }
            }
            if (d.Table) Station(d, root);
            else if (d.Nest) { root.AddComponent<PerchView>(); root.AddComponent<NestView>(); } // a perch of two seats, and Odin's (Nest)
            else root.AddComponent<PerchView>();
            Log(d.Name + " built as " + d.Prefab + ": " + parts + " parts, " + seats + " seat(s), cost " + d.Cost);
            return root;
        }

        // the raven table: a crafting station of the game (E opens the crafting window). No roof or fire needed; the
        // sounds of making things are the workbench's. Its name is the one recipes for the ravens ask for.
        private static void Station(Def d, GameObject root)
        {
            CraftingStation cs = root.AddComponent<CraftingStation>();
            cs.m_name = Lang.W(d.Name); // (the recipes ask for the station by this same word)
            cs.m_icon = root.GetComponent<Piece>().m_icon; // (the tables already standing in the world are made from the prefab as it is now)
            cs.m_craftRequireRoof = false;
            cs.m_roofCheckPoint = root.transform; // (other mods may read it)
            cs.m_craftRequireFire = false;
            cs.m_showBasicRecipies = false;
            cs.m_canRepair = false;
            cs.m_discoverRange = 4f;
            cs.m_rangeBuild = 10f;
            cs.m_useDistance = 2.5f;
            GameObject wb = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("piece_workbench") : null;
            CraftingStation like = wb != null ? wb.GetComponent<CraftingStation>() : null;
            if (like != null)
            {
                cs.m_craftItemEffects = like.m_craftItemEffects;
                cs.m_craftItemDoneEffects = like.m_craftItemDoneEffects;
                cs.m_craftItemDoneFailEffects = like.m_craftItemDoneFailEffects;
                cs.m_useAnimation = like.m_useAnimation;
            }
        }

        // the raven table's station (for the recipes of things for the ravens): the first table there is
        internal static CraftingStation TableStation { get { foreach (Def d in Tables) if (d.Built != null) return d.Built.GetComponent<CraftingStation>(); return null; } }

        private static readonly Dictionary<string, Material> Baked = new Dictionary<string, Material>();

        // «@light»: a point light of our own (a lantern, a crystal) — tint its colour (warm by default), scale X its reach in
        // metres, Y its brightness, Z how much it flickers; switched off far away (the game's LightLod), never a shadow
        internal static void MakeLight(Transform parent, WsPart p)
        {
            GameObject g = new GameObject("light");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = p.Pos;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = p.Tinted ? p.Tint : new Color(1f, 0.72f, 0.38f);
            l.range = Mathf.Clamp(Mathf.Abs(p.Scale.x), 0.5f, 30f);
            l.intensity = Mathf.Clamp(Mathf.Abs(p.Scale.y), 0.05f, 8f);
            l.shadows = LightShadows.None;
            LightLod lod = g.AddComponent<LightLod>();
            lod.m_lightDistance = 40f; lod.m_shadowLod = false;
            if (Mathf.Abs(p.Scale.z) > 0.01f)
            {
                LightFlicker f = g.AddComponent<LightFlicker>();
                f.m_flickerIntensity = Mathf.Clamp(Mathf.Abs(p.Scale.z), 0f, 1f);
                f.m_flickerSpeed = 10f; f.m_movement = 0.02f;
            }
        }

        // one part: the game piece's meshes copied, its look (another piece's material, glow, tint) baked into kept
        // material copies (a tint by property block would not survive the game's own blocks on pieces), a box each
        internal static bool Part(Transform parent, WsPart p, bool boxes = true)
        {
            GameObject t = WsCatalog.Template(p.Src);
            if (t == null) { Log("no game piece " + p.Src); return false; }
            GameObject holder = new GameObject(p.Src);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = p.Pos; holder.transform.localRotation = Quaternion.Euler(p.Rot); holder.transform.localScale = p.Scale;
            holder.layer = PieceLayer;
            Material swap = WsCatalog.MaterialOf(p.Mat);
            foreach (MeshRenderer src in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mf = src.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject g = new GameObject(src.name);
                g.layer = PieceLayer;
                g.transform.SetParent(holder.transform, false);
                g.transform.localPosition = src.transform.localPosition; g.transform.localRotation = src.transform.localRotation; g.transform.localScale = src.transform.localScale;
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                Material[] mats = (Material[])src.sharedMaterials.Clone();
                for (int k = 0; k < mats.Length; k++) mats[k] = Bake(swap != null ? swap : mats[k], p);
                mr.sharedMaterials = mats;
                if (!boxes) continue;
                BoxCollider bc = g.AddComponent<BoxCollider>();
                Bounds b = mf.sharedMesh.bounds;
                bc.center = b.center;
                bc.size = new Vector3(Mathf.Max(0.02f, b.size.x), Mathf.Max(0.02f, b.size.y), Mathf.Max(0.02f, b.size.z));
            }
            if (p.Fx || WsCatalog.FxOnly(p.Src)) WsCatalog.AddFx(holder.transform, p.Src, p); // its flame, sparks and light, as the game shows them
            return true;
        }

        private static Material Bake(Material from, WsPart p)
        {
            if (from == null || (!p.Tinted && !p.Glow)) return from;
            string key = from.GetInstanceID() + "|" + p.LookKey();
            Material m;
            if (Baked.TryGetValue(key, out m) && m != null) return m;
            m = new Material(from);
            m.name = from.name + " (perch)";
            if (p.Tinted && m.HasProperty("_Color")) m.SetColor("_Color", p.Tint);
            if (p.Glow)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                foreach (string prop in new[] { "_EmissionColor", "_EmissiveColor" }) if (m.HasProperty(prop)) m.SetColor(prop, p.GlowColor * p.GlowPower);
                if (m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") == null) m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            }
            Baked[key] = m;
            return m;
        }

        // the hammer's pictures: each perch's own photo (taken in the tab «Фото», PhotoPanel), or photographed by
        // itself once you are in the world (the camera needs a running game)
        internal static void Icons()
        {
            foreach (Def d in All)
            {
                if (d.IconDone || d.Built == null) continue;
                d.IconDone = true;
                try
                {
                    Sprite s = IconPhotos.Get(d.Id);
                    if (s == null) s = Photo(d.Built.transform.Find("new"));
                    if (s != null)
                    {
                        d.Built.GetComponent<Piece>().m_icon = s;
                        CraftingStation cs = d.Built.GetComponent<CraftingStation>();
                        if (cs != null) { cs.m_icon = s; SameIcon(d.Prefab, s); }
                    }
                }
                catch (Exception e) { Log("icon " + d.Name + ": " + e.Message); }
            }
        }

        // the tables standing in the world were copied from the prefab before its picture came: theirs too (the crafting window shows theirs)
        private static void SameIcon(string prefab, Sprite s)
        {
            foreach (CraftingStation st in UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
                if (st != null && Utils.GetPrefabName(st.gameObject.name) == prefab) { st.m_icon = s; Piece p = st.GetComponent<Piece>(); if (p != null) p.m_icon = s; }
        }

        // a new photo taken (or the own one thrown away): this perch's picture again
        internal static void IconAgain(string id)
        {
            bool any = false;
            foreach (Def d in All) if (d.Id == id) { d.IconDone = false; any = true; }
            if (any) Icons();
        }

        internal static Sprite Photo(Transform look) { return IconPhotos.Auto(look); }

        // ---------------------------------------------------------------- the sitting raven's look (perch and Workshop)

        // a still copy of the game's raven sitting at 'parent' (facing its +Z), its legs posed by PerchPose
        // (only for a raven seen here for the first time: one that flies in is seated itself, Seat)
        internal static GameObject Sitter(Transform parent, bool munin, float size, int layer, float[] pose, float[] stand)
        {
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) return null;
            GameObject r = UnityEngine.Object.Instantiate(src.m_visual, parent, false);
            r.SetActive(true);
            AppearMute.Quiet(r);
            Shared.RemoveExclamation(src, r);
            return Seat(r, parent, munin, size, layer, pose, stand);
        }

        // One raven, one bird: the very bird that flew here (the shoulder's model) sits down on the perch, and the
        // same bird later flies back to the shoulder (Shoulder.Make takes it over). Its flying parts go (the shoulder's
        // life with its motor, the pin), the sitting ones come (PerchPose; PerchLife is added by the perch).
        internal static GameObject Seat(GameObject r, Transform parent, bool munin, float size, int layer, float[] pose, float[] stand)
        {
            Raven src = Shoulder.Template(munin);
            if (r == null || src == null || src.m_visual == null) return null;
            foreach (ShoulderPin p in r.GetComponents<ShoulderPin>()) UnityEngine.Object.DestroyImmediate(p);
            foreach (ShoulderLife l in r.GetComponents<ShoulderLife>()) UnityEngine.Object.DestroyImmediate(l);
            ToTemplate(r, src.m_visual); // the flight's marks off its bones (the body's tilt, tucked legs, the landing's lift): as a new copy
            r.name = munin ? "Munin" : "Hugin";
            r.transform.SetParent(parent, false);
            r.transform.localPosition = Vector3.zero;
            r.transform.localRotation = Quaternion.Euler(Shoulder.Rotation);
            Vector3 want = src.m_visual.transform.lossyScale * size, ps = parent.lossyScale;
            r.transform.localScale = new Vector3(want.x / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), want.y / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), want.z / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));
            foreach (Collider c in r.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (Animator a in r.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            if (layer >= 0) foreach (Transform t in r.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            Reveal(r, false); // its own bounds: culled when not seen (PerchPose skips it then)
            foreach (ParticleSystemRenderer pr in r.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (AppearMute.OwnSmoke(pr.GetComponent<ParticleSystem>())) pr.enabled = false; // the model's own smoke: never (its aura stays)
            PerchPose pp = r.AddComponent<PerchPose>();
            pp.Init(pose);
            pp.Stand = stand;
            return r;
        }

        // A bird going from one life to the other (shoulder ↔ perch) takes the model's own pose first, every part as
        // in a new copy: what the last one did to its bones (and the clip it plays next does not write) does not stay.
        // Parts added to it (its aura, its outfit) are left as they are.
        internal static void ToTemplate(GameObject r, GameObject tpl)
        {
            if (r != null && tpl != null) CopyPose(tpl.transform, r.transform);
        }

        private static void CopyPose(Transform from, Transform to)
        {
            for (int i = 0; i < from.childCount; i++)
            {
                Transform f = from.GetChild(i);
                Transform t = to.Find(f.name);
                if (t == null) continue;
                t.localPosition = f.localPosition; t.localRotation = f.localRotation; t.localScale = f.localScale;
                CopyPose(f, t);
            }
        }

        // The game's raven model starts hidden (its "away" pose: meshes off, parts shrunk to nothing); the game's own
        // appearing clips show it. A copy shown without them is shown here: meshes on, parts at their size; particles
        // (its smoke) left off.
        internal static void Reveal(GameObject r, bool offscreen)
        {
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t != r.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
            foreach (Renderer x in r.GetComponentsInChildren<Renderer>(true))
            {
                if (x is ParticleSystemRenderer || Doomed(x.transform, r.transform)) continue;
                x.enabled = true;
                for (Transform t = x.transform; t != null && t != r.transform; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                SkinnedMeshRenderer sk = x as SkinnedMeshRenderer;
                if (sk != null && offscreen) sk.updateWhenOffscreen = true;
            }
        }

        // the game's "!" over a raven (Shared.RemoveExclamation takes it away): not shown again
        private static bool Doomed(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                string n = t.name;
                if (n.IndexOf("exclam", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("exclem", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the raven's side: where it sits

        internal static bool On(ItemDrop.ItemData item)
        {
            return item != null && item.m_customData != null && item.m_customData.ContainsKey(PerchKey);
        }

        private sealed class Spot { internal ZDOID Zdo; internal int Seat; internal Vector3 Pos; internal string Token, World = ""; internal float PerDay; }

        // the world it sits in (a character taken to another world finds its raven back on the shoulder there)
        private static string World { get { return ZNet.instance != null ? ZNet.instance.GetWorldUID().ToString() : ""; } }

        private static Spot Where(ItemDrop.ItemData item)
        {
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(PerchKey, out raw)) return null;
            string[] w = raw.Split('|');
            if (w.Length < 6) return null;
            string[] z = w[0].Split(':');
            long user; uint id; int seat; float x, y, zz;
            if (z.Length < 2 || !long.TryParse(z[0], out user) || !uint.TryParse(z[1], out id) || !int.TryParse(w[1], out seat)) return null;
            float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
            float.TryParse(w[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
            float.TryParse(w[4], NumberStyles.Float, CultureInfo.InvariantCulture, out zz);
            Spot s = new Spot();
            s.Zdo = new ZDOID(user, id); s.Seat = seat; s.Pos = new Vector3(x, y, zz); s.Token = w[5]; s.World = w.Length > 6 ? w[6] : "";
            if (w.Length > 7) float.TryParse(w[7], NumberStyles.Float, CultureInfo.InvariantCulture, out s.PerDay);
            if (w.Length <= 7 || s.PerDay < 0f) s.PerDay = DailyCap; // (0: a «sit here» spot, nothing gained there)
            return s;
        }

        // "Хугін сидить на жердині" for the book, the ring and the gifts
        internal static string Text(bool munin) { return Lang.F("{0} сидить на жердині", Plugin.RavenName(munin)); }
        internal static string Text(bool munin, ItemDrop.ItemData item) { return OnSpot(item) && WorldVisit.Active(munin) ? Lang.F("{0} полетів роздивитися щось поруч", Plugin.RavenName(munin)) : OnSpot(item) ? Grammar.Fix(Lang.F("{0} сидить там, куди ти його посла{{в|ла}}", Plugin.RavenName(munin)), Player.m_localPlayer) : InNest(item) ? Lang.F("{0} сидить у гнізді", Plugin.RavenName(munin)) : Text(munin); }
        // how to call it back from where it sits (a «sit here» spot has no E: the orders circle)
        internal static string CallHint(ItemDrop.ItemData item) { return OnSpot(item) ? Lang.T("Поклич його назад: J → «Повернути».") : InNest(item) ? Lang.T("Поклич його звідти (E на гнізді).") : Lang.T("Поклич його звідти (E біля жердини)."); }
        // sitting in a ravens' nest (Nest), not on a perch: its words say «у гнізді» (older records: by the piece, when it is loaded)
        internal static bool InNest(ItemDrop.ItemData item)
        {
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(PerchKey, out raw)) return false;
            string[] w = raw.Split('|');
            if (w.Length > 8) return w[8] == "n";
            return FromKind(Where(item)) == FromNest;
        }
        // "на жердині" or "у гнізді"
        internal static string OnWord(ItemDrop.ItemData item) { return InNest(item) ? Lang.T("у гнізді") : Lang.T("на жердині"); }

        // this player's ravens: in the bag, or on the shoulder cells
        private static readonly List<ItemDrop.ItemData> _mine = new List<ItemDrop.ItemData>(4);
        private static List<ItemDrop.ItemData> Mine(Player p)
        {
            _mine.Clear();
            if (p == null) return _mine;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) { bool m; if (RavenItem.IsRaven(item, out m)) _mine.Add(item); }
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (!_mine.Contains(item)) _mine.Add(item);
            return _mine;
        }

        // my raven of that kind sitting on this perch (any of my ravens of that kind)
        internal static ItemDrop.ItemData MineOn(Player p, PerchView v, bool munin)
        {
            if (p == null || v == null || !v.Valid) return null;
            ZDOID id = v.Id;
            foreach (ItemDrop.ItemData item in Mine(p))
            {
                bool m;
                if (!RavenItem.IsRaven(item, out m) || m != munin) continue;
                Spot s = Where(item);
                if (s != null && s.Zdo == id) return item;
            }
            return null;
        }

        internal static bool HasToken(Player p, string token)
        {
            foreach (Pending pd in _pending) if (pd.Token == token) return true; // sent, not yet confirmed
            foreach (ItemDrop.ItemData item in Mine(p)) { Spot s = Where(item); if (s != null && s.Token == token) return true; }
            return false;
        }

        // ---------------------------------------------------------------- strength on the perch

        private static float DayGained(ItemDrop.ItemData item, int day)
        {
            string raw;
            if (!item.m_customData.TryGetValue(DayKey, out raw)) return 0f;
            string[] w = raw.Split('|');
            int d; float g;
            if (w.Length < 2 || !int.TryParse(w[0], out d) || d != day || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out g)) return 0f;
            return g;
        }

        private static float ReadF(ItemDrop.ItemData item, string key)
        {
            string raw; float v;
            return item.m_customData.TryGetValue(key, out raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }

        // the time since the last look turned into strength (at most a game day's share); asleep: none
        private static void Charge(ItemDrop.ItemData item)
        {
            double now = WorldTime.Now, at;
            string raw;
            if (!item.m_customData.TryGetValue(AtKey, out raw) || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out at) || at > now) at = now;
            item.m_customData[AtKey] = now.ToString("R", CultureInfo.InvariantCulture);
            Spot here = Where(item);
            if (here != null && here.World.Length > 0 && World.Length > 0 && here.World != World) return; // another world's clock: nothing from it
            if (!Trust.Active(item)) return;
            float minutes = (float)Math.Min(now - at, 86400.0) / 60f;
            int day = Today;
            float gained = DayGained(item, day);
            Spot sp = Where(item);
            float norm = sp != null ? sp.PerDay : DailyCap;
            float mul = item.m_customData.ContainsKey(MulKey) ? ReadF(item, MulKey) : 1f;
            int zone = Satiety.Zone(item);
            float fed = zone <= Satiety.Hungry ? 0f : zone == Satiety.Fed ? Satiety.FedGainMul : 1f; // as Satiety.GainMul
            float perMinute = norm / Mathf.Max(1f, Power.GameDaySeconds() / 60f) * mul * fed;
            float add = Mathf.Min(minutes * perMinute, norm - gained);
            float before = Power.Get(item), max = Trust.Strength(item);
            add = Mathf.Min(add, max - before);
            if (add <= 0f) return;
            Power.SetValue(item, before + add);
            item.m_customData[DayKey] = day + "|" + (gained + add).ToString("0.###", CultureInfo.InvariantCulture);
            item.m_customData[GotKey] = (ReadF(item, GotKey) + add).ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ---- the place: a roof and comfort, as the game counts them for resting (measured at the perch)

        internal static void Measure(Vector3 pos, out bool roof, out int comfort)
        {
            float cover; bool underRoof;
            Cover.GetCoverForPoint(pos, out cover, out underRoof);
            roof = underRoof && cover >= 0.8f; // the game's "in shelter"
            comfort = RawComfort.At(roof, pos); // the game's own count: not the holder's Path of Valheiman tree
        }

        internal static float Mul(bool roof, int comfort)
        {
            if (!roof && comfort < ColdComfort) return ColdMul;
            return Mathf.Min(MaxMul, 1f + ComfortStep * Mathf.Max(0, comfort - 1));
        }

        internal static bool ParseEnv(string raw, out bool roof, out int comfort)
        {
            roof = false; comfort = 1;
            if (string.IsNullOrEmpty(raw)) return false;
            string[] w = raw.Split('|');
            if (w.Length < 2 || !int.TryParse(w[1], out comfort)) return false;
            roof = w[0] == "1";
            return true;
        }

        // "затишок 6 · сила ×1,5" / "відкрите небо, затишок 2 · сила −80%"
        internal static string EnvText(bool roof, int comfort)
        {
            float m = Mul(roof, comfort);
            if (m < 1f) return Lang.F("відкрите небо, затишок {0} · сила −{1}%", comfort, Mathf.RoundToInt((1f - m) * 100f));
            return Lang.F("затишок {0}{1} · сила ×{2}", comfort, (roof ? "" : Lang.T(", без даху")), Lang.Num(m, "0.#"));
        }

        internal static string DayText(ItemDrop.ItemData item)
        {
            Spot sp = Where(item);
            return Lang.F("{0}/{1} сили сьогодні", Mathf.FloorToInt(DayGained(item, Today)), Mathf.RoundToInt(sp != null ? sp.PerDay : DailyCap));
        }

        // off the perch (called back, the perch gone): the stay is counted and forgotten
        private static void Leave(Player p, ItemDrop.ItemData item, bool munin, string why) { Leave(p, item, munin, why, true); }

        private static void Leave(Player p, ItemDrop.ItemData item, bool munin, string why, bool charge)
        {
            if (charge) Charge(item);
            Spot was = Where(item);
            int from = FromKind(was);
            why = FromWords(why, from);
            if (was != null) Misses.Remove(was.Token);
            int got = Mathf.FloorToInt(ReadF(item, GotKey));
            item.m_customData.Remove(PerchKey); item.m_customData.Remove(AtKey); item.m_customData.Remove(GotKey); item.m_customData.Remove(MulKey);
            RavenItem.ForgetWorn();
            RavenSkills.SyncSoon(); // its model on the shoulder takes over from the perch's bird at once
            if (p == null) return;
            if (_together) { _togetherMunin.Add(munin); _togetherGot.Add(got); _togetherFrom.Add(from); return; } // both called at once: one message after
            RavenMsg.Center(p, why + GotText(got, from));
        }

        // where it sat: 0 a perch, 1 a «sit here» spot (the ground, a stone), 2 the nest
        private const int FromPerch = 0, FromSpot = 1, FromNest = 2;
        private static int FromKind(Spot s)
        {
            if (s == null) return FromPerch;
            if (s.PerDay <= 0f) return FromSpot;
            ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(s.Zdo) : null;
            GameObject pf = z != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(z.GetPrefab()) : null;
            Def d = pf != null ? DefOf(pf.name) : null;
            return d != null && d.Nest ? FromNest : FromPerch;
        }

        // the perch's words, for a spot or the nest (the translations keep these pieces whole)
        private static string FromWords(string why, int from)
        {
            if (why == null || from == FromPerch) return why;
            if (from == FromSpot)
                return why.Replace(Lang.T(" летить до тебе з жердини."), Lang.T(" летить до тебе.")).Replace(Lang.T("зігнали з жердини"), Lang.T("зігнали з місця")).Replace(Lang.T("Жердини, де сидів"), Lang.T("Місця, де сидів"));
            return why.Replace(Lang.T(" летить до тебе з жердини."), Lang.T(" летить до тебе з гнізда.")).Replace(Lang.T("зігнали з жердини"), Lang.T("зігнали з гнізда"));
        }

        private static string GotText(int got, int from)
        {
            if (got <= 0) return "";
            return from == FromNest ? Lang.F("\nНабрався в гнізді: +{0} сили.", got) : Lang.F("\nНабрався на жердині: +{0} сили.", got);
        }

        // Both ravens called back at once: one message for the two («Хугін і Мунін летять до тебе»), each one's gain
        private static bool _together;
        private static readonly List<bool> _togetherMunin = new List<bool>(2);
        private static readonly List<int> _togetherGot = new List<int>(2);
        private static readonly List<int> _togetherFrom = new List<int>(2);

        private static void Together(bool on) { _together = on; if (on) { _togetherMunin.Clear(); _togetherGot.Clear(); _togetherFrom.Clear(); } }

        private static void SayTogether(Player p)
        {
            Together(false);
            if (p == null || _togetherMunin.Count == 0) return;
            if (_togetherMunin.Count == 1)
            {
                bool m = _togetherMunin[0];
                RavenMsg.Center(p, FromWords(Lang.F("{0} летить до тебе з жердини.", Plugin.RavenName(m)), _togetherFrom[0]) + GotText(_togetherGot[0], _togetherFrom[0]));
                return;
            }
            string gains = "";
            for (int i = 0; i < _togetherMunin.Count; i++)
                if (_togetherGot[i] > 0) gains += (gains.Length > 0 ? ", " : "") + Plugin.RavenName(_togetherMunin[i]) + " +" + _togetherGot[i];
            RavenMsg.Center(p, Lang.F("Хугін і Мунін летять до тебе.{0}", (gains.Length > 0 ? Lang.F("\nНабралися на жердинах: {0} сили.", gains) : "")));
        }

        // ---------------------------------------------------------------- sitting down and calling back

        private sealed class Pending { internal PerchView View; internal ZDOID Zdo; internal int Seat; internal string Token; internal ItemDrop.ItemData Item; internal bool Munin; internal float Until; }

        // sit-downs dropped before they were confirmed (death, a new life): their seats let go, wherever the perch is
        private static void DropPending()
        {
            foreach (Pending pd in _pending) FreeRemote(pd.Zdo, pd.Seat, pd.Token);
            _pending.Clear();
        }
        private static readonly List<Pending> _pending = new List<Pending>(2);
        private static bool Waiting(bool munin) { foreach (Pending pd in _pending) if (pd.Munin == munin) return true; return false; }
        private static bool Reserved(PerchView v, int seat) { foreach (Pending pd in _pending) if (pd.View == v && pd.Seat == seat) return true; return false; }

        // why this raven cannot go to the perch now, or null
        internal static string WhyNot(Player p, bool munin, out ItemDrop.ItemData item)
        {
            item = RavenSeat.On(p, munin);
            string name = Plugin.RavenName(munin);
            if (item == null) return Lang.F("{0} нема на плечі.", Plugin.RavenNameGen(munin));
            if (Trust.Stranger(item, p)) return Lang.F("{0} не твій — він не сяде, куди ти скажеш.", name);
            if (On(item)) return Text(munin, item) + ".";
            if (Trust.Away(item)) return Lang.F("{0} {1}.", name, Trust.AwayWord(munin, item));
            if (!Trust.Active(item)) return Lang.F("{0} ще спить.", name);
            if (!Moving && !Shoulder.Arrived(p, munin)) return Lang.F("{0} ще летить до тебе.", name); // (after loading in: not yet on the shoulder; a move goes from the air)
            string sulk = Quirks.Sulk(p, munin);
            if (sulk != null) return sulk + ".";
            if (!Moving && !Shoulder.Free(p, munin)) return Lang.F("{0} зараз зайнятий у польоті.", name); // on an order or an outing: it goes from the air (FlyTo)
            if (Waiting(munin)) return Lang.F("Зачекай: {0} уже летить до жердини.", name);
            return null;
        }

        internal static void Sit(Player p, PerchView v, bool munin) { Sit(p, v, munin, -1); }

        internal static void Sit(Player p, PerchView v, bool munin, int seat)
        {
            ItemDrop.ItemData item;
            string why = WhyNot(p, munin, out item);
            if (why != null) { RavenMsg.Center(p, why); return; }
            if (seat < 0) seat = FreeSeatOf(v);
            if (seat < 0) { RavenMsg.Center(p, Lang.T("На жердині нема вільного місця.")); return; }
            string token = UnityEngine.Random.Range(0x100000, 0x7FFFFFFF).ToString("x");
            // it will sit facing the way it flies there (either way along the bar): no flight round the perch
            Transform st = v.SeatT(seat);
            Vector3 way = st.position - p.transform.position, front = st.forward;
            way.y = 0f; front.y = 0f;
            bool flip = Vector3.Dot(way, front) < 0f;
            if (v.IsSpot && SitOn.Follows(v)) flip = false; // on a player: facing their way, as on a shoulder
            else if (WallAhead(v, seat, flip) && !WallAhead(v, seat, !flip)) flip = !flip; // its beak would be in the wall: the other way
            ZDO pz = NetFlag.Zdo(p, false);
            string outfit = pz != null ? pz.GetString(munin ? Outfits.KeyM : Outfits.KeyH, "") : "";
            string wear = RavenWear.Code(item); // what its runes give it (line 3)
            string data = p.GetPlayerID() + "|" + (munin ? 1 : 0) + "|" + Trust.ShownLevel(munin, item) + "|" + token + "|" + p.GetPlayerName().Replace("|", "/") + "|" + p.GetZDOID().UserID + "|" + (flip ? "1" : "0") + "|" + (Grammar.Female(p) ? "1" : "0") + "|" + Voice.Code(item) + "\n" + outfit + "\n" + wear; // field 8: its own voice, for when you are not around
            v.Send(seat, data);
            _pending.Add(new Pending { View = v, Zdo = v.Id, Seat = seat, Token = token, Item = item, Munin = munin, Until = Time.time + 4f });
        }

        // the perch's owner took it: the raven is told where it sits now (and flies off the shoulder at the next look)
        private static void Confirm(Player p)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending pd = _pending[i];
                if (Confirm(p, pd)) _pending.Remove(pd);
            }
        }

        // true when this sit-down is settled (one way or another)
        private static bool Confirm(Player p, Pending pd)
        {
            Occupant mine = pd.View != null && pd.View.Valid ? pd.View.At(pd.Seat) : null;
            if ((mine == null || mine.Token != pd.Token) && (pd.View == null || !pd.View.Valid || Time.time > pd.Until))
            {
                FreeRemote(pd.Zdo, pd.Seat, pd.Token); // if it came late, it is let go
                RavenMsg.Center(p, Lang.F("Жердина не відповіла — {0} лишився на плечі.\nСпробуй ще раз.", Plugin.RavenName(pd.Munin)));
                return true;
            }
            Occupant o = pd.View.At(pd.Seat);
            if (o != null && o.Token != pd.Token)
            {
                RavenMsg.Center(p, Lang.F("Це місце на жердині щойно зайняли.\n{0} лишився на плечі.", Plugin.RavenName(pd.Munin)));
                return true;
            }
            if (o == null) return false; // not yet
            if (RavenSeat.On(p, pd.Munin) != pd.Item || Trust.Away(pd.Item)) // taken off or gone meanwhile: the seat let go
            {
                pd.View.Send(pd.Seat, "-" + pd.Token);
                return true;
            }
            Vector3 at = pd.View.SeatT(pd.Seat).position;
            ZDOID id = pd.View.Id;
            pd.Item.m_customData[PerchKey] = id.UserID + ":" + id.ID + "|" + pd.Seat + "|" + at.x.ToString("0.##", CultureInfo.InvariantCulture) + "|" + at.y.ToString("0.##", CultureInfo.InvariantCulture) + "|" + at.z.ToString("0.##", CultureInfo.InvariantCulture) + "|" + pd.Token + "|" + World + "|" + pd.View.PerDay.ToString("0.##", CultureInfo.InvariantCulture) + (pd.View.GetComponent<NestView>() != null ? "|n" : "");
            pd.Item.m_customData[MulKey] = pd.View.Mul().ToString("0.###", CultureInfo.InvariantCulture);
            pd.Item.m_customData[AtKey] = WorldTime.Now.ToString("R", CultureInfo.InvariantCulture);
            pd.Item.m_customData.Remove(GotKey);
            RavenItem.ForgetWorn();
            RavenSkills.SyncSoon(); // the shoulder and the book see it at once
            string onWhom = pd.View.IsSpot ? SitOn.Text(pd.View) : null, visit = pd.View.IsSpot ? WorldVisit.Note(pd.Munin) : null;
            if (visit != null) p.Message(MessageHud.MessageType.TopLeft, visit, 0, RavenItem.Icon(pd.Munin));
            else if (onWhom != null) p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} «Повернути» — і він прилетить назад.", (Plugin.RavenName(pd.Munin) + onWhom)));
            else if (pd.View.IsSpot) p.Message(MessageHud.MessageType.TopLeft, Plugin.RavenName(pd.Munin) + Grammar.Fix(Lang.T(" полетів сісти туди, куди ти показа{в|ла}. «Повернути» — і він прилетить назад."), p));
            else if (pd.View.GetComponent<NestView>() != null) p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} полетів у гніздо. Там він відпочине й набереться сили (до {1} на день; {2}).", Plugin.RavenName(pd.Munin), Mathf.RoundToInt(pd.View.PerDay), pd.View.EnvText()));
            else p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} полетів на жердину. Там він відпочине й набереться сили (до {1} на день; {2}).", Plugin.RavenName(pd.Munin), Mathf.RoundToInt(pd.View.PerDay), pd.View.EnvText()));
            return true;
        }

        // a free seat here not already promised to the other raven
        private static int FreeSeatOf(PerchView v)
        {
            int seat = v.FreeSeat();
            if (seat >= 0 && !Reserved(v, seat)) return seat;
            for (int i = 0; i < v.Count; i++) if (v.At(i) == null && !Reserved(v, i)) return i;
            return -1;
        }

        // ---------------------------------------------------------------- the orders: to the nearest perch and back

        internal static float FindRadius = 20f;  // m: the orders look for a perch this near
        internal static float StartFar = 90f;    // m: a perch farther (not loaded): its flight starts this far out on that line

        // the nearest perch with a free seat, seen from your eyes (not through a wall), within FindRadius
        internal static bool FindNear(Player p, out PerchView best, out int seat, out bool full)
        {
            best = null; seat = -1; full = false;
            if (p == null) return false;
            Vector3 eye = p.GetEyePoint();
            float bestD = FindRadius;
            foreach (PerchView v in PerchView.All)
            {
                if (!v.Valid || v.IsSpot) continue; // (a «sit here» spot is nobody's perch to be sent to)
                float d = Vector3.Distance(v.transform.position, p.transform.position);
                if (d > bestD) continue;
                int s = -1;
                bool anyFree = false;
                for (int i = 0; i < v.Count && s < 0; i++)
                {
                    if (v.At(i) != null || Reserved(v, i)) continue;
                    anyFree = true;
                    if (Seen(eye, v, i)) s = i;
                }
                if (s < 0) { if (!anyFree && Seen(eye, v, 0)) full = true; continue; }
                best = v; seat = s; bestD = d;
            }
            return best != null;
        }

        private static readonly RaycastHit[] _seenHits = new RaycastHit[16];
        private static bool Seen(Vector3 eye, PerchView v, int seat)
        {
            if (_wallMask == 0) _wallMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            Vector3 to = v.SeatT(seat).position + Vector3.up * 0.1f;
            Vector3 dir = to - eye;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(eye, dir / len, _seenHits, len, _wallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _seenHits[i].collider;
                if (c == null || c.transform.IsChildOf(v.transform)) continue;
                return false; // a wall, a door, a post between you
            }
            return true;
        }

        // the order for one raven: on the perch → back to you (from anywhere); on the shoulder → to the nearest perch
        internal static void Order(Player p, bool munin)
        {
            string why = Go(p, munin);
            if (why != null) RavenMsg.Center(p, why);
        }

        // the order done, or why not (said by the caller: both ravens' reasons go in one message)
        private static readonly float[] _orderAt = { -9f, -9f };
        internal static float OrderGap = 1.5f; // s between two perch orders for one raven

        // the context order (the orders circle's «Туди» piece above the perch, its keys: SitHere): where you look — another
        // player or a tamed animal → onto them (SitOn); a small thing → fetched (RavenFetch); a perch → onto that one; a
        // building → «сісти тут» on its top. Sitting anywhere already → called back.
        internal static void OrderHere(Player p, bool munin)
        {
            string why = GoHere(p, munin);
            if (why != null) RavenMsg.Center(p, why);
        }

        private static string GoHere(Player p, bool munin)
        {
            int k = munin ? 1 : 0;
            if (Time.time - _orderAt[k] < OrderGap) return null;
            _orderAt[k] = Time.time;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.F("{0} нема на плечі.", Plugin.RavenNameGen(munin));
            if (On(item))
            {
                // sitting somewhere and you look at another place: it goes there straight (the move), else it comes back
                SitHere.Target nt = SitHere.Look(p, munin);
                if (!Elsewhere(p, munin, item, nt)) { CallFar(p, item, munin); return null; }
                string block = MoveBlock(p, munin, item, nt);
                if (block != null) return block;
                CallFar(p, item, munin, Lang.F("{0} перелітає.", Plugin.RavenName(munin)));
                Moves[k] = new Move { T = nt, Until = Time.time + MoveWait };
                return null;
            }
            ItemDrop.ItemData it;
            string why = WhyNot(p, munin, out it);
            if (why != null) return why;
            return ToTarget(p, munin, SitHere.Look(p, munin));
        }

        // where you look, from the shoulder (or, in a move, from the air on its way off its old place)
        private static string ToTarget(Player p, bool munin, SitHere.Target t)
        {
            if (t.Player != null) return t.Why ?? SitOn.Go(p, munin, t.Player);           // another player: onto them
            if (t.Animal != null) return t.Why ?? SitOn.Go(p, munin, t.Animal);           // a tamed animal: onto it
            if (t.Item != null) return t.Why ?? RavenFetch.Go(p, munin, t.Item, t.Dist); // a thing: «Принести»
            if (t.Perch != null)
            {
                int ps = FreeSeatOf(t.Perch);
                if (ps < 0) return Lang.T("На цій жердині нема вільного місця.");
                Sit(p, t.Perch, munin, ps);
                return null;
            }
            if (t.Building) return t.Why ?? SitHere.Go(p, munin, t);
            return SitHere.Nothing;
        }

        // ---------------------------------------------------------------- the move: from where it sits to another place

        // It is called off its seat as when called back, and the moment its bird is in the air (its shoulder model
        // made from it) it is sent on to the new place: the flight there turns it in the air (Perches.FlyTo), it does
        // not land on you between. Moving: the checks "on the shoulder, landed, not busy" let it go from the air.
        private sealed class Move { internal SitHere.Target T; internal float Until; }
        private static readonly Move[] Moves = new Move[2];
        private const float MoveWait = 6f; // its bird not in the air by then (a far perch, a lag): the move is dropped, it comes to you
        private static bool Moving;

        // a place to move to: one it can sit on (a fetch goes from the shoulder: not a move), not the one it sits on now
        private static bool Elsewhere(Player p, bool munin, ItemDrop.ItemData item, SitHere.Target t)
        {
            if (t == null || t.Item != null) return false;
            if (t.Player == null && t.Animal == null && t.Perch == null && !t.Building) return false; // nothing there: come back
            Spot s = Where(item);
            if (s == null) return true;
            if (t.Perch != null) return t.Perch.Id != s.Zdo;
            if (t.Building) return Vector3.Distance(t.At, s.Pos) > SameSpot;
            Character c = t.Player != null ? (Character)t.Player : t.Animal; // on them already (any slot): that one again is a call back
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(s.Zdo) : null;
            PerchView v = g != null ? g.GetComponent<PerchView>() : null;
            if (v == null || !v.Valid || c == null) return true;
            ZDOID id = c.GetZDOID();
            return !v.Zdo.GetString(SitOn.FollowKey, "").StartsWith(id.UserID + ":" + id.ID + "|", StringComparison.Ordinal);
        }
        private const float SameSpot = 1.2f; // m: looking at where it sits (its own spot) is not a new place

        // why it cannot move there now (said, and it stays where it sits), or null
        private static string MoveBlock(Player p, bool munin, ItemDrop.ItemData item, SitHere.Target t)
        {
            string name = Plugin.RavenName(munin);
            if (Trust.Stranger(item, p)) return Lang.F("{0} не твій — він не сяде, куди ти скажеш.", name);
            if (Trust.AwayLeft(item) > 0f) return Lang.F("{0} {1}.", name, Trust.AwayWord(munin, item));
            if (Waiting(munin) || Moves[munin ? 1 : 0] != null) return Lang.F("Зачекай: {0} уже перелітає.", name);
            string sulk = Quirks.Sulk(p, munin);
            if (sulk != null) return sulk + ".";
            if (t.Why != null) return t.Why;
            if (t.Perch != null) return FreeSeatOf(t.Perch) < 0 ? Lang.T("На цій жердині нема вільного місця.") : null; // (a perch: free, as from the shoulder)
            return SitHere.WhyNotNow(p, munin); // hungry, too little strength
        }

        // for the orders circle: what «Туди» does while it sits (true: moves there; false: calls it back) and why not now
        internal static bool MovesTo(Player p, bool munin, ItemDrop.ItemData item, SitHere.Target t, out string block)
        {
            block = null;
            if (p == null || item == null || !On(item) || !Elsewhere(p, munin, item, t)) return false;
            block = MoveBlock(p, munin, item, t);
            return true;
        }

        // twice a second (Tick): a move's bird in the air → on to the new place
        private static void MoveOn(Player p)
        {
            for (int k = 0; k < 2; k++)
            {
                Move m = Moves[k];
                if (m == null) continue;
                bool munin = k == 1;
                ItemDrop.ItemData item = RavenSeat.On(p, munin);
                if (item == null || Time.time > m.Until || Trust.AwayLeft(item) > 0f) { Moves[k] = null; if (item != null && Time.time > m.Until) Log(Plugin.RavenName(munin) + " move dropped: its bird was not in the air in time"); continue; }
                if (On(item) || Shoulder.ModelOf(p, munin) == null) continue; // not off its seat yet, or its bird not taken over yet
                Moves[k] = null;
                Moving = true;
                string why;
                try { why = ToTarget(p, munin, m.T); }
                finally { Moving = false; }
                if (why != null) RavenMsg.Center(p, Lang.F("{0}\n{1} летить до тебе.", why, Plugin.RavenName(munin))); // (the place went meanwhile: it just comes back)
            }
        }

        private static string Go(Player p, bool munin)
        {
            int k = munin ? 1 : 0;
            if (Time.time - _orderAt[k] < OrderGap) return null; // pressed again at once: once is enough
            _orderAt[k] = Time.time;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.F("{0} нема на плечі.", Plugin.RavenNameGen(munin));
            if (On(item)) { CallFar(p, item, munin); return null; }
            PerchView v; int seat; bool full;
            ItemDrop.ItemData it;
            string why = WhyNot(p, munin, out it);
            if (why != null) return why;
            if (!FindNear(p, out v, out seat, out full))
                return full ? Lang.T("Усі жердини поруч зайняті.") : Lang.F("{0} не бачить жердини поруч (до {1} м, не за стіною).", Plugin.RavenName(munin), Mathf.RoundToInt(FindRadius));
            Sit(p, v, munin, seat);
            return null;
        }

        // back to you from where it sits, saying nothing (the end of its own visit: WorldVisit)
        internal static void CallQuiet(Player p, ItemDrop.ItemData item, bool munin)
        {
            if (p == null || item == null || !On(item)) return;
            Together(true);
            try { CallFar(p, item, munin); } finally { Together(false); }
        }

        // «Обидва — до мене» (the orders menu's centre): every order dropped and every raven on a perch called back
        internal static void AllHome(Player p)
        {
            if (p == null) return;
            Orders.ReturnBoth(p);
            Together(true);
            try
            {
                foreach (bool munin in new[] { false, true })
                {
                    ItemDrop.ItemData item = RavenSeat.On(p, munin);
                    if (item != null && On(item)) CallFar(p, item, munin);
                }
            }
            finally { SayTogether(p); }
        }

        // both at once: any of them on a perch → those come back; else both go (each to its own seat)
        internal static void OrderBoth(Player p)
        {
            ItemDrop.ItemData h = RavenSeat.On(p, false), m = RavenSeat.On(p, true);
            bool any = (h != null && On(h)) || (m != null && On(m));
            if (any)
            {
                Together(true);
                try
                {
                    if (h != null && On(h)) CallFar(p, h, false);
                    if (m != null && On(m)) CallFar(p, m, true);
                }
                finally { SayTogether(p); }
                return;
            }
            string a = h != null ? Go(p, false) : null, b = m != null ? Go(p, true) : null;
            if (a != null && b != null && a != b) RavenMsg.Center(p, a + "\n" + b);
            else if (a != null || b != null) RavenMsg.Center(p, a ?? b);
        }

        // The raven flies off to Odin from the perch it sits on (a shy one, or after a ritual: Trust.Wander): the seat is
        // freed with that mark (everyone sees it fly away; a perch not loaded here is just freed), what it gained there
        // counts, and why it goes is said with it. It comes back to the shoulder, as from any flight to Odin.
        internal static void ToOdin(Player p, ItemDrop.ItemData item, bool munin, string why)
        {
            Spot s = Where(item);
            if (s != null)
            {
                GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(s.Zdo) : null;
                PerchView v = g != null ? g.GetComponent<PerchView>() : null;
                if (v != null && v.Valid) v.Depart(s.Seat, s.Token);
                else if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGone, s.Zdo, s.Seat, s.Token);
            }
            Leave(p, item, munin, why);
        }

        // the bird on a perch (or on its way there) flies off and away: the shoulder's fly-off on the same bird
        internal static void FlyAway(GameObject b, bool munin, int level, Player owner, Transform face)
        {
            if (b == null) return;
            foreach (PerchFlight f in b.GetComponents<PerchFlight>()) UnityEngine.Object.DestroyImmediate(f);
            foreach (PerchLife l in b.GetComponents<PerchLife>()) UnityEngine.Object.DestroyImmediate(l);
            foreach (PerchPose pp in b.GetComponents<PerchPose>()) UnityEngine.Object.DestroyImmediate(pp);
            foreach (ShoulderPin p in b.GetComponents<ShoulderPin>()) UnityEngine.Object.DestroyImmediate(p); // not yet off the shoulder: it lets go
            ShoulderLife life = b.GetComponent<ShoulderLife>(); // on its way here it has one (it flies on); a sitting one not
            if (life == null)
            {
                Raven src = Shoulder.Template(munin);
                if (src != null && src.m_visual != null) { ToTemplate(b, src.m_visual); Reveal(b, false); } // the perch's leg pose off its bones, every part shown
                life = b.AddComponent<ShoulderLife>();
                life.Init(owner != null ? owner : Player.m_localPlayer, munin, Mathf.Max(1, level));
            }
            b.transform.SetParent(null, true);
            ShoulderFlyOff fly = b.AddComponent<ShoulderFlyOff>(); // (it crouches and springs off by the motor's take-off)
            fly.Life = life; fly.Munin = munin; fly.Owner = null; // (no owner: it does not turn to a perch)
            Vector3 fwd = face != null ? face.forward : b.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            fly.Fwd = fwd;
            fly.Side = Vector3.Cross(Vector3.up, fwd) * (munin ? -1f : 1f);
            fly.Motor = RavenMotor.Of(b, munin, owner);
            fly.Wait = fly.Motor != null && fly.Motor.Airborne ? 0f : 0.25f; // the wings open first, unless already flying
            AnimDirector.Take(b, RavenAct.FlyOff, fly, "flies off a perch to Odin");
        }

        // called back from wherever it sits: the perch here → its own bird flies to you, however far; not loaded → it
        // starts on the line from that perch to you, at most StartFar out, and flies by its motor (never a puff by you)
        internal static void CallFar(Player p, ItemDrop.ItemData item, bool munin) { CallFar(p, item, munin, null); }

        // why: what is said (null: "летить до тебе")
        private static void CallFar(Player p, ItemDrop.ItemData item, bool munin, string why)
        {
            Spot s = Where(item);
            GameObject g = s != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(s.Zdo) : null;
            PerchView v = g != null ? g.GetComponent<PerchView>() : null;
            if (v != null && v.Valid) { CallBack(p, v, munin, item, why); return; }
            if (FindLoadedSeat(p, munin, out v)) { Log(Plugin.RavenName(munin) + " called back: its perch by the spot not found here, another seat of it used"); CallBack(p, v, munin, item, why); return; }
            Log(Plugin.RavenName(munin) + " called back: its perch is not here" + (s != null ? " (" + Mathf.RoundToInt(Vector3.Distance(p.transform.position, s.Pos)) + " m off)" : " (no spot kept)") + ": it flies in from that way");
            if (s != null)
            {
                FreeRemote(s.Zdo, s.Seat, s.Token);
                Vector3 toYou = p.transform.position - s.Pos; toYou.y = 0f;
                float far = Vector3.Distance(p.transform.position, s.Pos);
                if (toYou.sqrMagnitude > 0.01f)
                {
                    // the perch is out of the loaded world: it shows up on that line where the ground is still loaded
                    Vector3 from = far > StartFar ? p.transform.position - toYou.normalized * StartFar + Vector3.up * 12f : s.Pos + Vector3.up * 0.3f;
                    NoteLeft(p.GetPlayerID(), munin, from, Quaternion.LookRotation(toYou.normalized, Vector3.up) * Quaternion.Euler(Shoulder.Rotation), null);
                }
            }
            Leave(p, item, munin, why ?? Lang.F("{0} летить до тебе з жердини.", Plugin.RavenName(munin)));
        }

        // what the order's piece says: where to, or how it sits
        internal static string OrderSub(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.T("нема на плечі");
            if (On(item)) return Lang.F("сидить · {0} · покликати", DayText(item));
            ItemDrop.ItemData it;
            string why = WhyNot(p, munin, out it);
            if (why != null) return why.TrimEnd('.').Replace("\n", " ");
            PerchView v; int seat; bool full;
            if (!FindNear(p, out v, out seat, out full)) return full ? Lang.T("усі жердини поруч зайняті") : Lang.T("жердини поруч не видно");
            return Lang.F("до жердини {0} м · безкоштовно", Mathf.RoundToInt(Vector3.Distance(v.transform.position, p.transform.position)));
        }

        internal static void CallBack(Player p, PerchView v, bool munin, ItemDrop.ItemData item) { CallBack(p, v, munin, item, null); }

        private static void CallBack(Player p, PerchView v, bool munin, ItemDrop.ItemData item, string why)
        {
            Spot s = Where(item);
            int seat = s != null ? s.Seat : -1;
            Occupant o = seat >= 0 ? v.At(seat) : null;
            if (o == null || o.Pid != p.GetPlayerID() || o.Munin != munin || (s != null && o.Token != s.Token))
                seat = v.SeatOf(p.GetPlayerID(), munin);
            o = seat >= 0 ? v.At(seat) : null;
            if (o != null && o.Pid == p.GetPlayerID() && o.Munin == munin) v.Release(seat, o.Token);
            Leave(p, item, munin, why ?? Lang.F("{0} летить до тебе з жердини.", Plugin.RavenName(munin)));
        }

        private static bool FindLoadedSeat(Player p, bool munin, out PerchView view)
        {
            view = null;
            if (p == null) return false;
            long pid = p.GetPlayerID();
            foreach (PerchView candidate in PerchView.All)
            {
                if (candidate == null || !candidate.Valid) continue;
                int seat = candidate.SeatOf(pid, munin);
                if (seat < 0) continue;
                Occupant o = candidate.At(seat);
                if (o == null || o.Pid != pid || o.Munin != munin) continue;
                view = candidate;
                return true;
            }
            return false;
        }

        // the test window (tab «Політ»): the nearest perch within 30 m, again and again from any side
        private static PerchView Nearest(Player p)
        {
            PerchView best = null; float d = 30f;
            foreach (PerchView v in PerchView.All) { if (v.IsSpot) continue; float x = Vector3.Distance(v.transform.position, p.transform.position); if (x < d) { d = x; best = v; } }
            return best;
        }

        internal static string TestSit(Player p, bool munin)
        {
            PerchView v = p != null ? Nearest(p) : null;
            if (v == null) return Lang.T("Нема жердини ближче 30 м.");
            Sit(p, v, munin);
            return Lang.F("{0} — на жердину ({1} м).", Plugin.RavenName(munin), Mathf.RoundToInt(Vector3.Distance(v.transform.position, p.transform.position)));
        }

        internal static string TestBack(Player p, bool munin)
        {
            PerchView v = p != null ? Nearest(p) : null;
            ItemDrop.ItemData item = v != null ? MineOn(p, v, munin) : null;
            if (item == null) return Lang.F("{0} не сидить на найближчій жердині.", Plugin.RavenName(munin));
            CallBack(p, v, munin, item);
            return Lang.F("{0} — з жердини до тебе.", Plugin.RavenName(munin));
        }

        // A cheap look ahead of the raven's beak on the perch: something solid (a wall, a post) this near in front
        // of where it would face. Its own perch does not count.
        internal static float WallReach = 0.6f;
        private static readonly RaycastHit[] _wallHits = new RaycastHit[8];
        private static int _wallMask;
        internal static bool WallAhead(PerchView v, int seat, bool flip)
        {
            if (v == null) return false;
            if (_wallMask == 0) _wallMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            Transform f = v.Facing(seat, flip);
            Vector3 dir = f.forward; dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return false;
            dir.Normalize();
            Vector3 from = f.position + Vector3.up * 0.12f; // about the raven's head
            int n = Physics.RaycastNonAlloc(from, dir, _wallHits, WallReach, _wallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (_wallHits[i].collider != null && !_wallHits[i].collider.transform.IsChildOf(v.transform)) return true;
            return false;
        }

        // the raven on this seat turns round on it (its owner's game asks; everyone sees it turn: PerchView)
        internal static void RequestTurn(PerchView v, int seat)
        {
            Occupant o = v != null ? v.At(seat) : null;
            if (o == null) return;
            string[] w = o.Raw.Split('|');
            if (w.Length < 8) { string[] x = new string[8]; for (int i = 0; i < 8; i++) x[i] = i < w.Length ? w[i] : "0"; w = x; }
            w[6] = o.Flip ? "0" : "1";
            v.Send(seat, string.Join("|", w) + "\n" + v.Outfit(seat));
        }

        // console "raven roost": every raven of yours called back from wherever it sits (a perch out of reach, lost)
        internal static string CallAll(Player p)
        {
            int n = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(Mine(p)))
            {
                bool munin;
                if (!On(item) || !RavenItem.IsRaven(item, out munin)) continue;
                Spot s = Where(item);
                if (s != null) FreeRemote(s.Zdo, s.Seat, s.Token);
                Leave(p, item, munin, Lang.F("{0} покинув жердину й летить до тебе.", Plugin.RavenName(munin)));
                n++;
            }
            return n > 0 ? Lang.F("Покликано з жердин: {0}.", n) : Lang.T("Жоден твій ворон не сидить на жердині.");
        }

        // the seat freed where this game has it, or by whoever owns that perch (or by the server for a perch nobody has loaded)
        private static void FreeRemote(ZDOID zdo, int seat, string token)
        {
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
            PerchView v = g != null ? g.GetComponent<PerchView>() : null;
            Player me = Player.m_localPlayer;
            if (v != null && v.Valid && me != null) { v.Release(seat, token); return; }
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGone, zdo, seat, token);
        }

        // ---------------------------------------------------------------- the routed messages

        internal static void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZDOID, int, string>(RpcGone, OnGone);
            ZRoutedRpc.instance.Register<ZPackage>(RpcSay, OnSay);
            ZRoutedRpc.instance.Register<ZPackage>(RpcTalkHere, OnTalkHere);
        }

        // "free this seat if that raven still sits there": seat -1 = the perch itself is gone (every raven on it leaves)
        private static void OnGone(long sender, ZDOID zdo, int seat, string token)
        {
            if (seat < 0)
            {
                Player p = Player.m_localPlayer;
                if (p == null) return;
                if (ZDOMan.instance != null && ZDOMan.instance.GetZDO(zdo) != null) return; // still here: not broken (a false word)
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(Mine(p)))
                {
                    bool munin;
                    Spot s = Where(item);
                    if (s != null && s.Zdo == zdo && RavenItem.IsRaven(item, out munin)) Leave(p, item, munin, Lang.F("Жердину зламали — {0} летить до тебе.", Plugin.RavenName(munin)));
                }
                return;
            }
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
            PerchView v = g != null ? g.GetComponent<PerchView>() : null;
            if (v != null && v.Valid) { if (v.Owner && (MayChange(sender, v.At(seat)) || SitOn.TargetIs(v, sender))) v.ClearIf(seat, token); return; } // (a raven on a player: they may shake it off)
            // nobody near it: the server keeps every ZDO and changes it itself
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null) return;
            ZDO z = ZDOMan.instance.GetZDO(zdo);
            if (z == null) return;
            Occupant cur = Parse(z.GetString(SeatKey(seat), ""));
            if (cur != null && cur.Token == token && MayChange(sender, cur)) { z.Set(SeatKey(seat), ""); z.Set(SeatKey(seat) + "o", ""); z.Set(SeatKey(seat) + "w", ""); }
        }

        // A seat is changed only by the game of the raven's owner (the session written in it) or by the perch's holder
        // itself (turning the raven round, letting go of an owner who left). A doctored client cannot free or rewrite
        // anyone else's seat. Old seats without a session (before it was kept) are let go by anyone.
        internal static bool MayChange(long sender, Occupant cur)
        {
            if (cur == null || cur.Uid == 0) return true;
            return sender == cur.Uid || sender == ZDOMan.GetSessionID();
        }

        // ---- what a perch's raven says, sent once by the perch's holder to everyone (shown by those near)
        internal static void SendSay(ZDOID perch, int seat, string text)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage(); pkg.Write(perch); pkg.Write(seat); pkg.Write(text);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcSay, pkg);
        }

        internal static void SendTalk(ZDOID perch, List<TalkBubble.Line> lines)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage(); pkg.Write(perch); pkg.Write(lines.Count);
            foreach (TalkBubble.Line l in lines) { pkg.Write(l.Munin); pkg.Write(l.Text); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcTalkHere, pkg);
        }

        private static PerchView Near(ZDOID id)
        {
            Player me = Player.m_localPlayer;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            PerchView v = g != null ? g.GetComponent<PerchView>() : null;
            if (v == null || me == null || Vector3.Distance(me.transform.position, v.transform.position) > 60f) return null;
            return v;
        }

        private static void OnSay(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return; // shown here already
            ZDOID id = pkg.ReadZDOID(); int seat = pkg.ReadInt(); string text = pkg.ReadString();
            PerchView v = Near(id);
            if (v != null && !v.HeldBy(sender)) return; // only the perch's holder speaks for it
            if (v != null && !string.IsNullOrEmpty(text)) v.ShowSay(seat, text.Length > 400 ? text.Substring(0, 400) : text);
        }

        private static void OnTalkHere(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return;
            ZDOID id = pkg.ReadZDOID();
            int n = Mathf.Min(pkg.ReadInt(), RavenTalk.MaxLines);
            List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
            for (int i = 0; i < n; i++) lines.Add(new TalkBubble.Line { Munin = pkg.ReadBool(), Text = pkg.ReadString() });
            PerchView v = Near(id);
            if (v == null || !v.HeldBy(sender) || lines.Count == 0) return;
            Player me = Player.m_localPlayer;
            TalkBubble.PlayAt(v, lines, me != null && v.SeatOf(me.GetPlayerID(), false) >= 0); // your own ravens' talk: shown as yours
        }

        // the perch broke (WearNTear): its ravens' owners are told, wherever they are
        internal static void Broke(ZDOID zdo)
        {
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcGone, zdo, -1, "");
        }

        // ---------------------------------------------------------------- twice a second (RavenSkills)

        private static float _nextCharge, _spawnedAt = -1f;
        private static readonly Dictionary<string, int> Misses = new Dictionary<string, int>();

        internal static void Tick(Player p)
        {
            if (p == null) return;
            BackOnLoad(p);
            if (_pending.Count > 0) Confirm(p);
            if (Moves[0] != null || Moves[1] != null) MoveOn(p);
            SitHere.Tick(); // the «sit here» spots nobody uses any more: taken away
            WorldVisit.Tick(p); // the ravens' own visits: what they do there, and back in time
            if (Time.time < _nextCharge) return;
            _nextCharge = Time.time + 5f;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(Mine(p)))
            {
                if (!On(item)) continue;
                Charge(item);
                // near the perch and it is not there (broken, gone) or someone else sits in that seat: back to you
                Spot s = Where(item);
                bool munin;
                if (s == null || !RavenItem.IsRaven(item, out munin)) continue;
                // «sit here»: you went far from where it sits → it comes after you (a spot is no place to stay)
                GameObject spotGo = s.PerDay <= 0f && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(s.Zdo) : null;
                PerchView spotV = spotGo != null ? spotGo.GetComponent<PerchView>() : null;
                string gone = spotV != null ? SitOn.Gone(spotV) : null; // the player it sat on is gone
                if (s.PerDay <= 0f && (gone != null || Vector3.Distance(p.transform.position, spotV != null ? spotV.transform.position : s.Pos) > SitHere.Far))
                {
                    if (!WorldVisit.Active(munin)) RavenMsg.Center(p, gone != null ? Lang.F("{0} — {1} летить до тебе.", gone, Plugin.RavenName(munin)) : Grammar.Fix(Lang.F("Ти відійш{{ов|ла}} далеко — {0} летить за тобою.", Plugin.RavenName(munin)), p)); // (its own visit: quietly)
                    Together(true); // (its own «летить до тебе» not said twice)
                    try { CallFar(p, item, munin); } finally { Together(false); }
                    continue;
                }
                if (!Ready) continue; // the world around still loading
                if (s.World.Length > 0 && World.Length > 0 && s.World != World) { Leave(p, item, munin, Lang.F("{0} лишився на жердині в іншому світі — тут він знову з тобою.", Plugin.RavenName(munin))); continue; }
                // the perch's own data, wherever this game has it: its seat taken by another, or freed
                ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(s.Zdo) : null;
                if (z != null)
                {
                    Occupant o = Parse(z.GetString(SeatKey(s.Seat), ""));
                    Misses.Remove(s.Token);
                    bool roof; int comfort;
                    if (ParseEnv(z.GetString(EnvKey, ""), out roof, out comfort)) item.m_customData[MulKey] = Mul(roof, comfort).ToString("0.###", CultureInfo.InvariantCulture);
                    if (o == null || o.Token != s.Token) Leave(p, item, munin, Lang.F("{0} зігнали з жердини.\nВін летить до тебе.", Plugin.RavenNameGen(munin)));
                    continue;
                }
                // no such perch where it should stand, the ground there loaded: broken
                if (Vector3.Distance(p.transform.position, s.Pos) > 40f || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(s.Pos)) { Misses.Remove(s.Token); continue; }
                int miss;
                Misses.TryGetValue(s.Token, out miss);
                Misses[s.Token] = ++miss;
                if (miss < 4) continue; // a big base streams in slowly: missing for 15-20 s before it counts as gone
                Misses.Remove(s.Token);
                Leave(p, item, munin, Lang.F("Жердини, де сидів {0}, більше нема.\nВін летить до тебе.", Plugin.RavenName(munin)));
            }
        }

        // you died: your ravens (in the bag that goes to the grave) leave their perches; nobody keeps a seat for a
        // raven lying in a grave, and it would sit there for ever if you came back another way
        internal static void Died(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return;
            DropPending();
            Moves[0] = Moves[1] = null; // (a move under way: it just comes back)
            if (Loyal.WorldKeeps()) return; // nothing goes to the grave: they stay where they sit
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(Mine(p)))
            {
                if (!On(item)) continue;
                Spot s = Where(item);
                if (s != null) FreeRemote(s.Zdo, s.Seat, s.Token);
                bool munin;
                if (RavenItem.IsRaven(item, out munin)) { PuffLeft(p.GetPlayerID(), munin, "its owner died"); Leave(null, item, munin, ""); }
            }
        }

        // the character has been in the world a while (its ravens and the perches around loaded)
        internal static bool Ready { get { return _spawnedAt >= 0f && Time.time - _spawnedAt > 15f; } }

        internal static void Spawned()
        {
            _spawnedAt = Time.time; DropPending();
            foreach (Left l in LeftFrom.Values) if (l.Bird != null) UnityEngine.Object.Destroy(l.Bird);
            LeftFrom.Clear();
            Misses.Clear();
        }

        // a bird held for a hand-over that will not come (its owner died): off in a puff now
        private static void PuffLeft(long pid, bool munin, string why)
        {
            Left l;
            if (!LeftFrom.TryGetValue(Who(pid, munin), out l)) return;
            LeftFrom.Remove(Who(pid, munin));
            Puff(l.Bird, munin, why);
        }

        // A clean slate each time you enter the world: a raven left on a perch when you left (logged out, lost the
        // connection, crashed) is simply on your shoulder again, no flight from the old perch (the game gives its
        // objects new ids each run, so that perch could not be found again anyway); the old seat just empties.
        // Once a game session (not after a respawn: a world that keeps your gear keeps them where they sit).
        internal static bool FreshDone { get { return ZNet.instance != null && _backFor == ZNet.instance; } }
        private static ZNet _backFor;
        private static void BackOnLoad(Player p)
        {
            if (ZNet.instance == null || _backFor == ZNet.instance) return;
            _backFor = ZNet.instance;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(Mine(p)))
            {
                bool munin;
                if (!On(item) || !RavenItem.IsRaven(item, out munin)) continue;
                Leave(null, item, munin, "", false); // quietly, and nothing for the time you were away
            }
        }

        // who is in the game now (their network sessions), looked up at most once a second
        private static readonly HashSet<long> _online = new HashSet<long>();
        private static float _onlineAt = -1f;
        internal static bool Online(long uid)
        {
            if (Time.time - _onlineAt > 1f)
            {
                _onlineAt = Time.time;
                _online.Clear();
                if (ZNet.instance != null) foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) _online.Add(info.m_characterID.UserID);
            }
            Player me = Player.m_localPlayer;
            if (me != null && uid == me.GetZDOID().UserID) return true; // you are here, whatever the list says
            if (_online.Count == 0 || uid == 0) return true;             // not known (an old seat, the list not come yet)
            return _online.Contains(uid);
        }

        // a raven gone from a perch without its owner near (the owner left, the seat was freed elsewhere): off in a puff
        internal static void Puff(GameObject bird, bool munin, string why)
        {
            if (bird == null) return;
            if (!string.IsNullOrEmpty(why)) Log(Plugin.RavenName(munin) + " gone in a puff: " + why);
            Raven tpl = Shoulder.Template(munin);
            if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, bird.transform.position, bird.transform.rotation, null, 0.6f, -1);
            UnityEngine.Object.Destroy(bird);
        }

        // ---------------------------------------------------------------- the flights there and back

        internal sealed class Occupant { internal long Pid; internal bool Munin; internal int Level; internal string Token = "", Name = "", Raw = ""; internal long Uid; internal bool Flip, Female; internal int Voice = -1; }

        internal static Occupant Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string[] w = raw.Split('|');
            if (w.Length < 4) return null;
            Occupant o = new Occupant();
            long.TryParse(w[0], out o.Pid);
            o.Munin = w[1] == "1";
            int.TryParse(w[2], out o.Level);
            o.Token = w[3];
            o.Name = w.Length > 4 ? w[4] : "";
            if (w.Length > 5) long.TryParse(w[5], out o.Uid);
            o.Flip = w.Length > 6 && w[6] == "1"; // it sits facing the other way along the bar
            o.Female = w.Length > 7 && w[7] == "1"; // its owner's forms in its words
            if (w.Length > 8 && !int.TryParse(w[8], out o.Voice)) o.Voice = -1; // its own voice (Voice), for when its owner is not around
            o.Raw = raw;
            return o;
        }

        // where a raven just left a perch: its way back to the shoulder starts there
        // The perch's bird stays seen until the shoulder's new model takes its place in the same frame (TakeFrom),
        // or a few seconds pass (that player's raven is not drawn here): no gap, no jump.
        private sealed class Left { internal Vector3 Pos; internal Quaternion Rot; internal float At; internal GameObject Bird; }
        private static readonly Dictionary<long, Left> LeftFrom = new Dictionary<long, Left>();
        private static long Who(long pid, bool munin) { return pid * 2 + (munin ? 1 : 0); }
        internal static float HandOver = 8f;

        internal static void NoteLeft(long pid, bool munin, Vector3 pos, Quaternion rot, GameObject bird)
        {
            Left old;
            if (LeftFrom.TryGetValue(Who(pid, munin), out old) && old.Bird != null && old.Bird != bird) UnityEngine.Object.Destroy(old.Bird);
            LeftFrom[Who(pid, munin)] = new Left { Pos = pos, Rot = rot, At = Time.time, Bird = bird };
        }

        private static float _sweepAt;
        internal static void Sweep()
        {
            if (Time.time < _sweepAt || LeftFrom.Count == 0) return;
            _sweepAt = Time.time + 1f;
            List<long> old = null;
            foreach (KeyValuePair<long, Left> kv in LeftFrom) if (Time.time - kv.Value.At > HandOver) { if (old == null) old = new List<long>(); old.Add(kv.Key); }
            if (old == null) return;
            foreach (long k in old) { Left l = LeftFrom[k]; Puff(l.Bird, (k & 1) == 1, "nobody took it over in " + HandOver + " s"); LeftFrom.Remove(k); } // nobody took it over: off in a puff
        }

        // a raven comes back to the shoulder: from the perch it sat on, if it just left one (or still sits there in
        // this game: the perch's news comes later than the player's)
        // bird: the very bird (from the perch, or turned back on its way there) the shoulder takes over; null when
        // there is none in this game (far, not drawn here): then the shoulder's new model starts where it was
        internal static string NoPerchWhy(Player pl, bool munin)
        {
            if (pl == null) return "no player";
            int seats = 0; float near = float.MaxValue;
            foreach (PerchView v in PerchView.All) { if (v == null) continue; if (v.SeatOf(pl.GetPlayerID(), munin) >= 0) { seats++; near = Mathf.Min(near, Vector3.Distance(v.transform.position, pl.transform.position)); } }
            return seats == 0 ? "no left-from note, no seat of it on a loaded perch" : "no left-from note; its seat " + Mathf.RoundToInt(near) + " m off (reach " + Reach + ")";
        }

        internal static bool TakeFrom(Player pl, bool munin, out Vector3 pos, out Quaternion rot, out Vector3 vel, out GameObject bird)
        {
            pos = Vector3.zero; rot = Quaternion.identity; vel = Vector3.zero; bird = null;
            if (pl == null) return false;
            long pid = pl.GetPlayerID();
            Left l;
            if (LeftFrom.TryGetValue(Who(pid, munin), out l))
            {
                LeftFrom.Remove(Who(pid, munin));
                bool fresh = Time.time - l.At < HandOver;
                if (l.Bird != null)
                {
                    pos = l.Bird.transform.position; rot = l.Bird.transform.rotation;
                    PerchFlight f = l.Bird.GetComponent<PerchFlight>();
                    if (f != null) vel = f.AirVel; // turned back in the air: it flies on at its speed
                    if (fresh) { bird = l.Bird; return true; } // the same bird, on to the shoulder (from however far: it flies)
                    UnityEngine.Object.Destroy(l.Bird);
                    vel = Vector3.zero;
                }
                else { pos = l.Pos; rot = l.Rot; }
                if (fresh) return true; // back by its motor from there
            }
            foreach (PerchView v in PerchView.All)
            {
                int seat = v.SeatOf(pid, munin);
                if (seat < 0 || Vector3.Distance(v.transform.position, pl.transform.position) > Reach) continue;
                if (pl == Player.m_localPlayer && !HasToken(pl, v.At(seat).Token)) continue; // your old seat from before: not flown from
                pos = v.Give(seat, out rot, out bird);
                PerchFlight pf = bird != null ? bird.GetComponent<PerchFlight>() : null;
                if (pf != null) vel = pf.AirVel; // it was on its way here: it flies on
                return true;
            }
            return false;
        }

        // this player's raven sitting on a perch, as drawn here (the nearest one within 'reach' m), or null
        internal static GameObject SittingBird(Player pl, bool munin, float reach)
        {
            if (pl == null) return null;
            long pid = pl.GetPlayerID();
            GameObject found = null;
            float best = reach * reach;
            foreach (PerchView v in PerchView.All)
            {
                if (v == null) continue;
                int s = v.SeatOf(pid, munin);
                GameObject b = s >= 0 && !v.IsHidden(s) ? v.BirdAt(s) : null;
                if (b == null) continue;
                float d = (b.transform.position - pl.transform.position).sqrMagnitude;
                if (d <= best) { best = d; found = b; }
            }
            return found;
        }

        // the seat this raven is going to sit on, near enough to fly to
        internal static bool Target(Player pl, bool munin, out PerchView view, out int seat)
        {
            view = null; seat = -1;
            if (pl == null) return false;
            long pid = pl.GetPlayerID();
            float best = Reach;
            foreach (PerchView v in PerchView.All)
            {
                int s = v.SeatOf(pid, munin);
                if (s < 0 || v.IsHidden(s)) continue; // (called back from there already: not where it goes)
                if (pl == Player.m_localPlayer && !HasToken(pl, v.At(s).Token)) continue; // an old seat of yours: not where it goes
                float d = Vector3.Distance(v.transform.position, pl.transform.position);
                if (d <= best) { best = d; view = v; seat = s; }
            }
            return view != null;
        }

        // Shoulder.FlyOff: off the shoulder to a perch, if it is going to one
        internal static bool FlyTo(GameObject g, ShoulderPin pin, ShoulderLife life, bool munin)
        {
            Player pl = pin != null ? pin.Player : null;
            PerchView v; int seat;
            if (!Target(pl, munin, out v, out seat)) return false;
            PerchFlight f = g.AddComponent<PerchFlight>();
            f.View = v; f.Seat = seat; f.Munin = munin; f.Owner = pl; f.Pin = pin; f.Life = life;
            f.Motor = RavenMotor.Of(g, munin, pl);
            bool inAir = f.Motor != null && f.Motor.Airborne;
            f.Wait = inAir ? 0f : ShoulderFlyOff.Delay; // (two leaving at once: the dispatcher sets them apart, Landings.PickTakeoff)
            AnimDirector.Take(g, RavenAct.FlyOff, f, "flies to a perch");
            if (inAir && pin != null) pin.enabled = false;
            v.Incoming(seat, f);
            return true;
        }

        // already flying off (the perch's news came late here): it turns to the perch instead
        internal static bool Redirect(ShoulderFlyOff fly, Player owner, bool munin, ShoulderLife life, RavenMotor motor)
        {
            PerchView v; int seat;
            if (!Target(owner, munin, out v, out seat)) return false;
            AnimDirector.Release(fly.gameObject, fly); // the fly-off lets go of it: the perch's flight takes it
            PerchFlight f = fly.gameObject.AddComponent<PerchFlight>();
            f.View = v; f.Seat = seat; f.Munin = munin; f.Owner = owner; f.Life = life; f.Motor = motor; f.Wait = 0f;
            f.Launched();
            AnimDirector.Take(fly.gameObject, RavenAct.FlyOff, f, "turns to a perch");
            v.Incoming(seat, f);
            return true;
        }
    }

    // ---------------------------------------------------------------- hooks

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class PerchScenePatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            try { Perches.Register(__instance); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perches: " + e); }
        }
    }

    // A wall perch's ghost in the hammer: on the wall where you aim, its back to the wall (its +Z out into the room);
    // on a floor or a ceiling it cannot go.
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class PerchWallGhostPatch
    {
        private static readonly System.Reflection.FieldInfo GhostF = AccessTools.Field(typeof(Player), "m_placementGhost");
        private static readonly System.Reflection.FieldInfo StatusF = AccessTools.Field(typeof(Player), "m_placementStatus");
        private static readonly System.Reflection.MethodInfo RayM = AccessTools.Method(typeof(Player), "PieceRayTest");
        private static readonly System.Reflection.MethodInfo ValidM = AccessTools.Method(typeof(Player), "SetPlacementGhostValid");
        private static GameObject _ghost;
        private static Perches.Def _def;

        private static void Postfix(Player __instance)
        {
            try
            {
                if (GhostF == null || RayM == null) return;
                GameObject ghost = GhostF.GetValue(__instance) as GameObject;
                if (ghost == null || !ghost.activeSelf) return;
                if (ghost != _ghost) { _ghost = ghost; _def = Perches.DefOf(Utils.GetPrefabName(ghost)); } // the name once per ghost, not every frame
                Perches.Def d = _def;
                if (d != null && d.Nest) { NestSky(__instance, ghost); return; }
                if (d == null || !d.Wall) return;
                if (_args == null)
                {
                    System.Reflection.ParameterInfo[] ps = RayM.GetParameters();
                    _args = new object[ps.Length];
                    _bools = new bool[ps.Length];
                    for (int i = 0; i < ps.Length; i++) _bools[i] = ps[i].ParameterType == typeof(bool);
                }
                object[] args = _args;
                for (int i = 0; i < args.Length; i++) args[i] = _bools[i] ? (object)false : null;
                bool hit = (bool)RayM.Invoke(__instance, args);
                Vector3 point = args.Length > 0 && args[0] is Vector3 ? (Vector3)args[0] : Vector3.zero;
                Vector3 normal = args.Length > 1 && args[1] is Vector3 ? (Vector3)args[1] : Vector3.up;
                if (!hit) return;
                Vector3 flat = new Vector3(normal.x, 0f, normal.z);
                if (Mathf.Abs(normal.y) > 0.35f || flat.sqrMagnitude < 0.01f) { Invalid(__instance); return; } // not a wall
                ghost.transform.position = point;
                ghost.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            catch (Exception e) { if (!_told && Plugin.Instance != null) { _told = true; Plugin.Instance.Log("Perches: wall ghost: " + e.Message); } }
        }
        private static bool _told;
        private static object[] _args;
        private static bool[] _bools;

        // the nest stands under the open sky only: Odin must see it (a roof, a tree, a cliff above — red ghost)
        private static float _skyTold;
        private static void NestSky(Player p, GameObject ghost)
        {
            if (StatusF == null || StatusF.GetValue(p).ToString() != "Valid") return;
            float cover; bool roof;
            Cover.GetCoverForPoint(ghost.transform.position + Vector3.up * 3.8f, out cover, out roof); // (over its top: its own model never counts)
            if (!roof) return;
            Invalid(p);
            if (Time.time - _skyTold > 3f) { _skyTold = Time.time; p.Message(MessageHud.MessageType.Center, Lang.T("Гніздо ставлять лише просто неба: над ним не має бути даху.")); }
        }

        private static void Invalid(Player p)
        {
            if (StatusF != null) StatusF.SetValue(p, Enum.Parse(StatusF.FieldType, "Invalid"));
            if (ValidM != null) ValidM.Invoke(p, new object[] { false });
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class PerchDbPatch
    {
        private static void Postfix()
        {
            try { Perches.Hammer(); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perches: " + e); }
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class PerchSpawnPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            Perches.Spawned();
            try { Perches.Hammer(); Perches.Icons(); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Perches: " + e); }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Вороняче дерево»: three old trees in the world where ravens gather (Workshop models "use: tree", built by Perches,
    // not in the hammer, never broken), each of its own kind and land: the green one in the Meadows, the icy one in the
    // Mountains (both well away from the world's middle), the charred one in the Ashlands. The server puts them in
    // itself (ZDOs, so it works in zones nobody has seen yet), and takes away any tree standing out of its own land (the
    // five of the first version).
    // By a tree your runes weave into a «в'язь» (Bind.cs): the tree's window (RavenBookTreeWindow) lays them on the roots
    // — green 2, icy 3, charred 4, any kinds — and «Сплести» begins the arena (BindArena); won, the в'язь is yours.
    internal static class RavenTree
    {
        internal const string KnownKey = "RavenWatch_trees";     // player customData: the trees you found ("v2|x/z|…")
        internal static float FindRange = 400f, UseRange = 8f;
        internal static float Footprint = 3f, FlatMax = 0.7f, AwayFromPlaces = 60f; // m: the roots' reach, how uneven under them, how far from the world's own buildings

        // ---------------------------------------------------------------- which tree, where
        // Kind: 0 icy, 1 green, 2 charred (the mixing counts by it: KindNames)
        private sealed class Sort { public string Id; public Heightmap.Biome Land; public float Min, Max; public int Kind; }
        private static readonly Sort[] Sorts =
        {
            new Sort { Id = "voroniache_derevo_zelene", Land = Heightmap.Biome.Meadows, Min = 2000f, Max = 6000f, Kind = 1 },
            new Sort { Id = "voroniache_derevo", Land = Heightmap.Biome.Mountain, Min = 2000f, Max = 8500f, Kind = 0 },
            new Sort { Id = "voroniache_derevo_horile", Land = Heightmap.Biome.AshLands, Min = 4000f, Max = 10200f, Kind = 2 },
        };
        private const string PrefabStart = "RavenWatch_tree_"; // (Perches: the prefab of a tree model)

        internal static IEnumerable<string> Prefabs { get { foreach (Sort t in Sorts) yield return PrefabStart + t.Id; } }

        // the tree that would stand on this land (the test panel's «a tree here»); the icy one elsewhere
        internal static string PrefabHere(Vector3 at)
        {
            Heightmap.Biome b = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(at.x, at.z) : Heightmap.Biome.Mountain;
            foreach (Sort t in Sorts) if (t.Land == b) return PrefabStart + t.Id;
            return PrefabStart + Sorts[1].Id;
        }

        // every tree of this world (where the world is: alone, hosting, the server)
        internal static List<ZDO> AllZdos()
        {
            List<ZDO> all = new List<ZDO>();
            if (ZDOMan.instance == null) return all;
            foreach (string pf in Prefabs)
            {
                List<ZDO> some = new List<ZDO>();
                int idx = 0;
                while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(pf, some, ref idx)) { }
                all.AddRange(some);
            }
            return all;
        }

        private static Sort SortOf(int prefabHash) { foreach (Sort t in Sorts) if ((PrefabStart + t.Id).GetStableHashCode() == prefabHash) return t; return null; }

        // ---------------------------------------------------------------- the server puts them in
        private static bool _placed;
        private static ZNet _world;

        internal static void ServerTick()
        {
            if (ZNet.instance != _world) { _world = ZNet.instance; _placed = false; }
            if (_placed || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || WorldGenerator.instance == null || ZoneSystem.instance == null) return;
            // (a dedicated server needs no look of them: the ZDOs go in by the prefab's name; a game with a screen waits for the prefabs)
            if (ZNetScene.instance == null) return;
            if (!ZNet.instance.IsDedicated()) foreach (string pf in Prefabs) if (ZNetScene.instance.GetPrefab(pf) == null) return;
            _placed = true;
            Dictionary<Sort, ZDO> kept = new Dictionary<Sort, ZDO>();
            Dictionary<Sort, ZDO> askew = new Dictionary<Sort, ZDO>(); // its own land, but on a slope or by a building: moved if a better place is found
            int gone = 0;
            foreach (ZDO z in AllZdos())
            {
                Sort t = SortOf(z.GetPrefab());
                Vector3 p = z.GetPosition();
                if (t == null || WorldGenerator.instance.GetBiome(p.x, p.z) != t.Land || kept.ContainsKey(t) || askew.ContainsKey(t))
                {
                    // out of its own land (the first version's five), or a second of its kind: taken away
                    Remove(z);
                    gone++;
                    Log("raven tree at " + IdOf(p) + " (" + WorldGenerator.instance.GetBiome(p.x, p.z) + ") taken away: not its land");
                    continue;
                }
                if (Fits(p, FlatMax)) kept[t] = z; else askew[t] = z;
            }
            System.Random rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ 0x52617665);
            int made = 0;
            foreach (Sort t in Sorts)
            {
                if (kept.ContainsKey(t)) continue;
                Vector3 at;
                ZDO old;
                askew.TryGetValue(t, out old);
                // one standing askew moves only to a really flat place (else every start would move it again)
                if (!Spot(rnd, t, old != null, out at)) { Log("no place for the " + t.Id + " tree in " + t.Land + (old != null ? ": the old one stays" : "")); continue; }
                if (old != null) { Log("raven tree at " + IdOf(old.GetPosition()) + " moved: on a slope or by a building"); Remove(old); gone++; }
                int hash = (PrefabStart + t.Id).GetStableHashCode();
                ZDO zdo = ZDOMan.instance.CreateNewZDO(at, hash);
                zdo.Persistent = true;
                zdo.SetPrefab(hash);
                zdo.SetRotation(Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
                made++;
                Log("raven tree " + t.Id + " put in " + t.Land + " at " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + " (" + Mathf.RoundToInt(new Vector2(at.x, at.z).magnitude) + " m from the middle)");
            }
            Log(made + " raven tree(s) put in, " + gone + " taken away, " + (kept.Count + made) + " in the world");
        }

        private static void Remove(ZDO z)
        {
            z.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(z);
        }

        // dry land of that land, in its ring round the middle, flat under the roots (the height of the world as generated);
        // the flatness asked loosens when such land is rare (the mountains), never for a tree only moved (strict).
        // Land nobody has been to yet first: there no player has built, and the game grows its woods only later (TreeView clears them)
        private static bool Spot(System.Random rnd, Sort t, bool strict, out Vector3 at)
        {
            _land = _sea = _uneven = _fresh = 0;
            foreach (float flat in strict ? new[] { FlatMax } : new[] { FlatMax, FlatMax * 2f, FlatMax * 4f })
            {
                if (Spot(rnd, t, flat, true, out at)) return true;
                if (Spot(rnd, t, flat, false, out at)) return true;
            }
            Log("  (tries: not " + t.Land + " " + _land + ", by the sea " + _sea + ", uneven/water/lava/a place near " + _uneven + ", land seen before " + _fresh + ")");
            at = Vector3.zero;
            return false;
        }

        private static readonly System.Reflection.FieldInfo GeneratedZones = HarmonyLib.AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");
        private static bool Fresh(Vector3 c)
        {
            HashSet<Vector2s> made = GeneratedZones != null ? GeneratedZones.GetValue(ZoneSystem.instance) as HashSet<Vector2s> : null;
            if (made == null) return true; // (cannot tell: any land will do)
            Vector2s zone = ZoneSystem.GetZone(c);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (made.Contains(new Vector2s((short)(zone.x + dx), (short)(zone.y + dz)))) return false;
            return true;
        }

        private static int _land, _sea, _uneven, _fresh; // why the tries failed (the log, when no place is found)

        private static bool Spot(System.Random rnd, Sort t, float flat, bool fresh, out Vector3 at)
        {
            at = Vector3.zero;
            float sea = ZoneSystem.instance.m_waterLevel;
            for (int i = 0; i < 6000; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = t.Min + (float)rnd.NextDouble() * (t.Max - t.Min);
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                if (WorldGenerator.instance.GetBiome(x, z) != t.Land) { _land++; continue; }
                float h = WorldGenerator.instance.GetHeight(x, z);
                if (h < sea + 2f) { _sea++; continue; }
                Vector3 c = new Vector3(x, h, z);
                if (!Fits(c, flat)) { _uneven++; continue; }
                if (fresh && !Fresh(c)) { _fresh++; continue; }
                at = new Vector3(x, Lowest(c), z); // (the roots sit on the lowest ground under them: no root in the air)
                return true;
            }
            return false;
        }

        internal static void Reset() { _placed = false; }

        // flat enough under the whole spread of the roots, no root in the water or the lava (nor lava a few steps off),
        // and no building of the world (crypt, ruin, stone) near
        private static bool Fits(Vector3 c, float flatMax)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i <= 8; i++)
            {
                float r = i == 0 ? 0f : Footprint, ang = i * Mathf.PI / 4f;
                float h = WorldGenerator.instance.GetHeight(c.x + Mathf.Cos(ang) * r, c.z + Mathf.Sin(ang) * r);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
            if (hi - lo > flatMax || lo < ZoneSystem.instance.m_waterLevel + 1f) return false;
            for (int i = 0; i <= 16; i++)
            {
                float r = i == 0 ? 0f : i <= 8 ? Footprint : Footprint + 5f, ang = i * Mathf.PI / 4f;
                if (ZoneSystem.IsLavaPreHeightmap(new Vector3(c.x + Mathf.Cos(ang) * r, 0f, c.z + Mathf.Sin(ang) * r), 0.2f)) return false;
            }
            Vector2s zone = ZoneSystem.GetZone(c);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    ZoneSystem.LocationInstance li;
                    if (!ZoneSystem.instance.m_locationInstances.TryGetValue(new Vector2s((short)(zone.x + dx), (short)(zone.y + dz)), out li)) continue; // (zones are Vector2s in this game)
                    Vector3 d = li.m_position - c; d.y = 0f;
                    if (d.magnitude < AwayFromPlaces) return false;
                }
            return true;
        }

        private static float Lowest(Vector3 c)
        {
            float lo = float.MaxValue;
            for (int i = 0; i <= 8; i++) { float r = i == 0 ? 0f : Footprint * 0.6f, ang = i * Mathf.PI / 4f; lo = Mathf.Min(lo, WorldGenerator.instance.GetHeight(c.x + Mathf.Cos(ang) * r, c.z + Mathf.Sin(ang) * r)); }
            return lo;
        }

        // ---------------------------------------------------------------- one tree
        internal static string IdOf(Vector3 at) { return Mathf.RoundToInt(at.x) + "/" + Mathf.RoundToInt(at.z); }

        // its kind, from its land: 0 icy (the Mountains), 1 green (the Meadows), 2 charred (the Ashlands)
        internal static int Kind(Vector3 at)
        {
            Heightmap.Biome b = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(at.x, at.z) : Heightmap.Biome.Meadows;
            foreach (Sort t in Sorts) if (t.Land == b) return t.Kind;
            return 1;
        }
        internal static readonly string[] KindNames = Lang.Keys("крижане", "зелене", "горіле");

        // found: remembered in the character (its mark on the map comes from that, MapTick), Hugin says a word
        internal static void Found(Player p, TreeView v)
        {
            if (p == null || v == null) return;
            string id = IdOf(v.transform.position), raw;
            if (!p.m_customData.TryGetValue(KnownKey, out raw)) raw = "v2|";
            if (raw.Contains("|" + id + "|")) return;
            p.m_customData[KnownKey] = raw + id + "|";
            RavenMsg.Center(p, Lang.T("Ти знайш{ов|ла} Вороняче дерево. Біля нього руни можна змішувати."));
            Log("found the raven tree at " + id);
        }

        // ---------------------------------------------------------------- its mark on the map
        // Our own picture (a tree with a raven), in pale gold. A map mark of the game's player kinds (Icon0–4) gets the
        // picture of its kind from the game or a UI mod (SeneaL UI draws them all its own way), and the game cannot save
        // a mark with a picture of its own: so it is a mark of kind None, not saved, drawn again from the found trees
        // whenever there is a new map (entering the world) or a new tree. The old saved marks (a plain dot) are taken off.
        internal static readonly Color PinColor = new Color(0.93f, 0.8f, 0.5f, 1f);
        private static readonly List<Minimap.PinData> _pins = new List<Minimap.PinData>();
        private static Minimap _pinMap;
        private static string _pinFor;
        private static readonly System.Reflection.FieldInfo MapPins = HarmonyLib.AccessTools.Field(typeof(Minimap), "m_pins");

        // twice a second (Errands.Tick): cheap unless the map or the found trees changed
        // a found tree that is not there any more (moved off a slope by the server): its mark goes once you are near its old place
        private static float _nextGone;
        private static string DropGone(Player p, string raw)
        {
            string keep = "v2|";
            bool changed = false;
            foreach (string id in raw.Split('|'))
            {
                string[] xz = id.Split('/');
                if (xz.Length != 2) continue;
                Vector3 at = new Vector3(Errands.F(xz[0], 0f), p.transform.position.y, Errands.F(xz[1], 0f));
                bool gone = false;
                if (Vector3.Distance(at, p.transform.position) < 150f && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(at))
                {
                    gone = true;
                    foreach (TreeView v in TreeView.All) { if (v == null) continue; Vector3 d = v.transform.position - at; d.y = 0f; if (d.magnitude < 15f) { gone = false; break; } }
                }
                if (gone) { changed = true; Log("the mark of a tree no longer at " + id + " taken off"); } else keep += id + "|";
            }
            if (!changed) return raw;
            p.m_customData[KnownKey] = keep;
            return keep;
        }

        internal static void MapTick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Minimap.instance == null) return;
            string raw;
            if (!p.m_customData.TryGetValue(KnownKey, out raw)) raw = "";
            if (raw.Length > 0 && !raw.StartsWith("v2|") && WorldGenerator.instance != null)
            {
                // the first version's five: only the one in the Mountains stands still (the others were taken away)
                string keep = "v2|";
                foreach (string id in raw.Split('|'))
                {
                    string[] xz = id.Split('/');
                    if (xz.Length != 2) continue;
                    if (WorldGenerator.instance.GetBiome(Errands.F(xz[0], 0f), Errands.F(xz[1], 0f)) == Heightmap.Biome.Mountain) keep += id + "|";
                }
                p.m_customData[KnownKey] = raw = keep;
            }
            if (raw.Length > 3 && Time.time >= _nextGone) { _nextGone = Time.time + 5f; raw = DropGone(p, raw); }
            if (_pinMap == Minimap.instance && _pinFor == raw) return;
            if (_pinMap == Minimap.instance) foreach (Minimap.PinData pin in _pins) if (pin != null) Minimap.instance.RemovePin(pin);
            _pins.Clear();
            _pinMap = Minimap.instance; _pinFor = raw;
            Sprite icon = AbilityIcons.Get("map_tree");
            string name = Lang.T("Вороняче дерево");
            foreach (string id in raw.Split('|'))
            {
                string[] xz = id.Split('/');
                if (xz.Length != 2) continue;
                Vector3 at = new Vector3(Errands.F(xz[0], 0f), 0f, Errands.F(xz[1], 0f));
                OldMarks(at);
                Minimap.PinData pin = Minimap.instance.AddPin(at, Minimap.PinType.None, name, false, false, 0L, default(Splatform.PlatformUserID));
                if (icon != null) pin.m_icon = icon;
                _pins.Add(pin);
            }
            ErrandMap.Trees.Clear(); ErrandMap.Trees.AddRange(_pins);
        }

        // the plain dots this mod saved on the map before (a mark of the player's kind, named so, right on the tree)
        private static void OldMarks(Vector3 at)
        {
            List<Minimap.PinData> all = MapPins != null ? MapPins.GetValue(Minimap.instance) as List<Minimap.PinData> : null;
            if (all == null) return;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Minimap.PinData pin = all[i];
                if (pin == null || pin.m_type != Minimap.PinType.Icon3 || !pin.m_save) continue;
                if (pin.m_name != "Вороняче дерево" && pin.m_name != "Raven Tree") continue;
                Vector3 d = pin.m_pos - at; d.y = 0f;
                if (d.sqrMagnitude > 25f) continue;
                Minimap.instance.RemovePin(pin);
                Log("the old map mark of the tree at " + IdOf(at) + " taken off (now its own picture)");
            }
        }

        // ---------------------------------------------------------------- weaving (Bind.cs, the arena: BindArena)
        // how a tree of this kind weaves (the tree window says it)
        internal static readonly string[] HowWeaves = Lang.Keys("кладе руни серцем, серединою й оболонкою (кольори: голова й тіло, крила, хвіст)", "зливає руни в одне (кольори — навпіл)", "розносить руни по тілу ворона (кольори — від голови до хвоста)");

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven tree: " + s); }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven tree                     the trees of this world (known to the server; alone or hosting: here)
        // raven tree go [kind]           to the nearest one, or that one (green / icy / charred; the Ukrainian words work too)
        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return "Нема гравця.";
            string what = a.Length > 1 ? a[1].ToLowerInvariant() : "";
            Heightmap.Biome want = a.Length > 2 ? LandOf(a[2]) : Heightmap.Biome.None;
            if (a.Length > 2 && want == Heightmap.Biome.None) return "Не знаю такого дерева: " + a[2] + " (green, icy, charred).";
            List<Vector3> all = Here();
            if (what != "go")
            {
                if (all == null) return "Дерева знає лише сервер; raven tree go — до найближчого (сервер підкаже).";
                if (all.Count == 0) return "У цьому світі ще нема воронячих дерев.";
                System.Text.StringBuilder sb = new System.Text.StringBuilder("Воронячі дерева (" + all.Count + "):");
                foreach (Vector3 t in all)
                    sb.Append("\n  ").Append(WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(t.x, t.z).ToString() : "?").Append(' ')
                      .Append(Mathf.RoundToInt(t.x)).Append(' ').Append(Mathf.RoundToInt(t.z)).Append(" — ").Append(Mathf.RoundToInt(Vector3.Distance(p.transform.position, t))).Append(" м");
                return sb.Append("\nraven tree go [green|icy|charred] · raven back — назад").ToString();
            }
            if (all != null)
            {
                Vector3 best = Vector3.zero; float bestD = float.MaxValue;
                foreach (Vector3 t in all)
                {
                    if (want != Heightmap.Biome.None && (WorldGenerator.instance == null || WorldGenerator.instance.GetBiome(t.x, t.z) != want)) continue;
                    float d = Vector3.Distance(p.transform.position, t);
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (bestD == float.MaxValue) return want != Heightmap.Biome.None ? "Дерева в цій землі нема." : "У цьому світі ще нема воронячих дерев.";
                ErrandMap.Teleport(p, best + new Vector3(4f, 0f, 4f));
                return "До дерева: " + Mathf.RoundToInt(best.x) + " " + Mathf.RoundToInt(best.z) + ". raven back — назад.";
            }
            // on a client: the server knows them (one within 6 km)
            string lands = want != Heightmap.Biome.None ? "|" + want + "|" : "|Meadows|Mountain|AshLands|";
            List<ErrandNet.Ask> asks = new List<ErrandNet.Ask> { new ErrandNet.Ask { Slot = "t", Names = "RavenTree", Lands = lands, X = p.transform.position.x, Z = p.transform.position.z, Min = 0f, Max = 6000f } };
            ErrandNet.Find(asks, found =>
            {
                Player me = Player.m_localPlayer;
                string v; Vector3 at;
                if (me == null) return;
                if (found == null || !found.TryGetValue("t", out v) || !ErrandSteps.Point(v, out at)) { RavenMsg.Center(me, "Сервер не знає дерева в межах 6 км (або не відповів)."); return; }
                ErrandMap.Teleport(me, at + new Vector3(4f, 0f, 4f));
                RavenMsg.Center(me, "До дерева: " + v + ". raven back — назад.");
            });
            return "Питаю в сервера, де дерево…";
        }

        // every tree of this world, where the world is (alone or hosting); null on a client
        private static List<Vector3> Here()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null) return null;
            List<Vector3> l = new List<Vector3>();
            foreach (ZDO z in AllZdos()) l.Add(z.GetPosition());
            return l;
        }

        private static Heightmap.Biome LandOf(string s)
        {
            s = s.ToLowerInvariant();
            if (s.StartsWith("лук") || s.StartsWith("mead") || s.StartsWith("зел") || s.StartsWith("green")) return Heightmap.Biome.Meadows;
            if (s.StartsWith("горі") || s.StartsWith("поп") || s.StartsWith("ash") || s.StartsWith("char")) return Heightmap.Biome.AshLands; // (before «гори»: «горіле»)
            if (s.StartsWith("гор") || s.StartsWith("mount") || s.StartsWith("кри") || s.StartsWith("ic")) return Heightmap.Biome.Mountain;
            return Heightmap.Biome.None;
        }
    }

    // the tree in the world: found when you come near (from afar: it is a distant object), its zones (the crown: your
    // ravens sit on it, SeatZone; the roots: the mixing, TreeRoot); the first time it is in the world, the game's own
    // trees, bushes and stones that grew into it are cleared away (once, by the game that holds it, only after the game
    // has grown the land round it: a distant tree is in the world long before its zone is), and it is set on the real ground
    internal sealed class TreeView : MonoBehaviour
    {
        internal static readonly List<TreeView> All = new List<TreeView>();
        internal const string ClearedKey = "RavenWatch_cleared2"; // (2: the first one was often set before the woods grew)
        internal const string GroundKey = "RavenWatch_grounded";
        internal static float ClearRadius = 6f, ClearHeight = 9f;
        private ZNetView _nv;
        private PerchView _perch;
        private float _next, _readySince = -1f;
        private int _clears;

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
            _perch = GetComponent<PerchView>();
            All.Add(this);
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "box:top") { SeatZone z = t.gameObject.AddComponent<SeatZone>(); z.Perch = _perch; z.Title = () => Name; z.Line = Line; }
                else if (t.name == "box:bowl") t.gameObject.AddComponent<TreeRoot>().View = this;
            }
        }

        private void OnDestroy() { All.Remove(this); }

        internal bool Valid { get { return this != null && _nv != null && _nv.IsValid(); } }
        internal PerchView Perch { get { return _perch; } }
        internal bool Near(Player p) { return Valid && p != null && Vector3.Distance(p.transform.position, transform.position) <= RavenTree.UseRange; }
        internal string Name { get { return Lang.T("Вороняче дерево") + " (" + Lang.T(RavenTree.KindNames[RavenTree.Kind(transform.position)]) + ")"; } }

        private string Line(Player p)
        {
            return Lang.T("Посади воронів на дерево — і сплітай руни біля коріння");
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 2f;
            Player p = Player.m_localPlayer;
            if (p != null && Vector3.Distance(p.transform.position, transform.position) <= RavenTree.FindRange) RavenTree.Found(p, this);
            if (_clears >= 2 || !Valid || !_nv.IsOwner() || _nv.GetZDO().GetBool(ClearedKey, false)) return;
            // the land round it grown by the game (every zone its roots and crown reach) and loaded here
            if (!Grown()) { _readySince = -1f; return; }
            if (_readySince < 0f) { _readySince = Time.time; Ground(); }
            // twice (3 s and 10 s after that: what grew into it may still be coming into the world), then never again
            if (Time.time - _readySince > (_clears == 0 ? 3f : 10f))
            {
                _clears++;
                Clear();
                if (_clears == 2) _nv.GetZDO().Set(ClearedKey, true);
            }
        }

        // you are by it (within 30 m every zone its roots reach is one of the zones round you, which the game grows in
        // full as they load), and they are loaded
        private bool Grown()
        {
            Player me = Player.m_localPlayer;
            if (me == null || ZoneSystem.instance == null || ZNetScene.instance == null || !ZNetScene.instance.IsAreaReady(transform.position)) return false;
            Vector3 c = transform.position, d = me.transform.position - c; d.y = 0f;
            if (d.magnitude > 30f) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (!ZoneSystem.instance.IsZoneLoaded(c + new Vector3(dx * ClearRadius, 0f, dz * ClearRadius))) return false;
            return true;
        }

        // on the real ground (the server put it by the world as generated; a mod changing the land, or the game's own
        // smoothing, may leave it in the air or deep in): the roots on the lowest ground under them, once
        private void Ground()
        {
            ZDO zdo = _nv.GetZDO();
            if (zdo.GetBool(GroundKey, false)) return;
            Vector3 c = transform.position;
            float lo = float.MaxValue;
            for (int i = 0; i <= 8; i++)
            {
                float r = i == 0 ? 0f : RavenTree.Footprint * 0.6f, ang = i * Mathf.PI / 4f, h;
                if (ZoneSystem.instance.GetGroundHeight(new Vector3(c.x + Mathf.Cos(ang) * r, c.y + 50f, c.z + Mathf.Sin(ang) * r), out h)) lo = Mathf.Min(lo, h);
            }
            zdo.Set(GroundKey, true);
            if (lo == float.MaxValue || Mathf.Abs(lo - c.y) < 0.25f) return;
            Vector3 to = new Vector3(c.x, lo, c.z);
            transform.position = to;
            zdo.SetPosition(to);
            RavenTree.Log("the tree at " + RavenTree.IdOf(c) + " set on the ground: " + (lo - c.y).ToString("+0.0;-0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");
        }

        // the game's own trees, logs, bushes, stones and pickables in its roots and crown: gone (never a player's building, a
        // creature, an item)
        private void Clear()
        {
            int n = 0;
            HashSet<GameObject> done = new HashSet<GameObject>();
            foreach (Collider c in Physics.OverlapCapsule(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * ClearHeight, ClearRadius))
            {
                ZNetView v = c != null ? c.GetComponentInParent<ZNetView>() : null;
                if (v == null || v == _nv || !v.IsValid() || !done.Add(v.gameObject)) continue;
                GameObject g = v.gameObject;
                if (g.GetComponent<Character>() != null || g.GetComponent<ItemDrop>() != null || g.GetComponent<TreeView>() != null) continue;
                Piece pc = g.GetComponent<Piece>();
                if (pc != null && pc.GetCreator() != 0L) continue; // someone built it
                bool growth = g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null || g.GetComponent<MineRock>() != null
                    || g.GetComponent<MineRock5>() != null || g.GetComponent<Destructible>() != null || g.GetComponent<Pickable>() != null || g.GetComponent<StaticPhysics>() != null;
                if (!growth) continue;
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(g);
                n++;
            }
            if (n > 0) RavenTree.Log("cleared " + n + " thing(s) grown into the tree at " + RavenTree.IdOf(transform.position));
        }

        internal void Burst()
        {
            RavenFx.Play("vfx_offering", transform.position + Vector3.up * 1.2f, Quaternion.identity);
            RavenFx.Play("sfx_raven_kaw", transform.position + Vector3.up * 4f, Quaternion.identity);
        }
    }

    // the roots: the mixing (the book's «Руни» page), once your ravens sit on the tree
    internal sealed class TreeRoot : MonoBehaviour, Hoverable, Interactable
    {
        internal TreeView View;

        public string GetHoverName() { return View != null ? View.Name : Lang.T("Вороняче дерево"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            Player p = Player.m_localPlayer;
            if (View == null || p == null) return "";
            bool mine = SeatZone.Mine(p, View.Perch).Count > 0;
            string s = GetHoverName() + (mine
                ? "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Сплести руни")
                : "\n<color=#9A9A9A>" + Lang.T("Спершу посади воронів на дерево (вище, на стовбур).") + "</color>");
            return Localization.instance != null ? Localization.instance.Localize(s) : s;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer || View == null || !View.Valid) return false;
            if (SeatZone.Mine(p, View.Perch).Count == 0) { RavenMsg.Center(p, Lang.T("Коріння мовчить, поки на дереві нема твоїх воронів. Посади їх (вище, на стовбур).")); return true; }
            RavenBook.OpenTree(View);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }

    // The upper zone of a nest or a tree (an unseen box "box:top" in its model): E sends both your ravens up into its
    // seats, E again calls them both back; nobody else's ravens may be there at the same time.
    internal sealed class SeatZone : MonoBehaviour, Hoverable, Interactable
    {
        internal PerchView Perch;
        internal NestView Nest; // a nest's top: one with no raven at all is spoken to by Odin's trial instead (Trial)
        internal System.Func<string> Title;
        internal System.Func<Player, string> Line;

        // your ravens sitting on this perch (Hugin false, Munin true)
        internal static List<bool> Mine(Player p, PerchView v)
        {
            List<bool> l = new List<bool>(2);
            if (p == null || v == null || !v.Valid) return l;
            foreach (bool m in new bool[] { false, true }) if (Perches.MineOn(p, v, m) != null) l.Add(m);
            return l;
        }

        private string Other(Player p)
        {
            if (Perch == null) return null;
            for (int i = 0; i < Perch.Count; i++) { Perches.Occupant o = Perch.At(i); if (o != null && o.Pid != p.GetPlayerID()) return o.Name; }
            return null;
        }

        public string GetHoverName() { return Title != null ? Title() : ""; }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            Player p = Player.m_localPlayer;
            if (Perch == null || p == null) return "";
            bool mine = Mine(p, Perch).Count > 0;
            string other = mine ? null : Other(p);
            string line = Line != null ? Line(p) : "";
            if (Nest != null && !mine && !Trial.HasRaven(p))
            {
                string t = GetHoverName() + (line.Length > 0 ? "\n<size=80%>" + line + "</size>" : "") + "\n" + Trial.NoRavenHover(p, Nest);
                return Localization.instance != null ? Localization.instance.Localize(t) : t;
            }
            string s = GetHoverName() + (line.Length > 0 ? "\n<size=80%>" + line + "</size>" : "") + "\n" + (other != null
                ? "<color=#9A9A9A>" + Lang.F("Тут ворони гравця {0}.", other) + "</color>"
                : "[<color=yellow><b>$KEY_Use</b></color>] " + (mine ? Lang.T("Забрати воронів") : Lang.T("Посадити воронів сюди")));
            return Localization.instance != null ? Localization.instance.Localize(s) : s;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || p != Player.m_localPlayer || Perch == null || !Perch.Valid) return false;
            List<bool> mine = Mine(p, Perch);
            if (Nest != null && mine.Count == 0 && !Trial.HasRaven(p)) { Trial.NoRavenUse(p, Nest); return true; }
            if (mine.Count > 0)
            {
                foreach (bool m in mine) { ItemDrop.ItemData item = Perches.MineOn(p, Perch, m); if (item != null) Perches.CallBack(p, Perch, m, item); }
                return true;
            }
            string other = Other(p);
            if (other != null) { RavenMsg.Center(p, Lang.F("Тут ворони гравця {0}.", other)); return true; }
            // both at once or none: every raven you wear must really sit on your shoulder (not still flying back, not on
            // a perch, not away) — else one would go up and the other be left behind
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData worn = Trust.Worn(m);
                if (worn == null || Trust.Stranger(worn, p) || !Trust.Active(worn)) continue; // (none, not yours, still asleep: it never goes anyway)
                if (Shoulder.NotOnShoulder(p, m) == null) continue;
                string name = Plugin.RavenName(m);
                RavenMsg.Center(p, Perches.OnSpot(worn) ? Lang.F("{0} сидить не на тобі. Поклич його спершу на плече.", name) // (a «sit here» spot: the ground, a stone)
                    : Perches.On(worn) ? Lang.F("{0} сидить на іншій жердині. Поклич його спершу на плече.", name)
                    : Trust.Away(worn) ? Lang.F("{0} зараз далеко. Посадиш, коли повернеться.", name)
                    : Lang.F("Зачекай, поки {0} сяде на плече: сюди злітають лише ті, що сидять на тобі.", name));
                return true;
            }
            string firstWhy = null;
            int sent = 0;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item;
                string why = Perches.WhyNot(p, m, out item);
                if (item == null) continue;
                if (why != null) { if (firstWhy == null) firstWhy = why; continue; }
                Perches.Sit(p, Perch, m);
                sent++;
            }
            if (sent == 0) RavenMsg.Center(p, firstWhy ?? Lang.T("Посади ворона на плече: сідають ті, хто з тобою."));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif

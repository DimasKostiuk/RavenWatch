#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A quiet spot in the wild, the one finder for everything that lays something out there for you (a raven's cache:
    // Requests; the story's spot of your own: «spot», StoryWhere «me.<name>»). A spot min..max m from «from»:
    //   dry land, not steep, not a land you never were in (the far north, the ash lands)
    //   away from a base (Clear m): your bed, your own map marks, a fire's or workbench's base area, anything built
    //   away from the world's places (villages, altars, dungeons) when the game knows them
    //   loaded (its zone is here): the ground itself — no rock or roof over it, nobody's ward
    //   shore > 0: no water within that many m (the height looked at on two rings round it)
    //   anyLand: only the land's own tests (dry, not steep, the shore): bases and places not asked — the last resort
    // Why: what the last pick turned down, counted (the log says it, so a place where nothing fits explains itself).
    // Pick: the first that fits. Best: the open and flat ones of many (Forest: known on land not loaded too; Uneven), then of those the one with fewest trees,
    // a tree close by counted twice (the user 2026-11-07: a scene's shots there, trees must not cover it all).
    // At most Tries tries, each cheap but the loaded ones (a ray down, a sphere for the trees): once per pick, never a frame.
    internal static class WorldSpot
    {
        internal static float Clear = 70f; // m from anything built, a bed, a mark of yours
        internal static int Tries = 40;
        internal const float TreeWeighs = 6f; // (one tree in the way: as bad as so many metres of slope)
        internal static int Samples = 160, Finalists = 16;
        internal static float UnknownTrees = 0.5f; // (trees reckoned on land not loaded yet)
        internal static float CloseTrees = 10f; // m: a tree this near counts twice: it is what stands in the shot
        internal static float OpenFrom = 1.2f, ForestWeighs = 8f, OtherLand = 3f; // the game's forest factor: the less, the more forest it plants (from about this: open; NOT measured, the log says each pick's) · as bad as so many metres of slope, in the thick of it · a land not the Meadows

        // how wooded the land is, from the game's own factor for planting trees: the same whether its zone is loaded or not; in metres of slope, 0 open
        internal static float Forest(Vector3 q)
        {
            float wood = Mathf.Clamp01((OpenFrom - WorldGenerator.GetForestFactor(q)) / OpenFrom) * ForestWeighs;
            return WorldGenerator.instance.GetBiome(q) == Heightmap.Biome.Meadows ? wood : wood + OtherLand; // (the black forest is wooded whatever the factor)
        }

        internal static string Why = "";

        // water within r m of it (two rings of the world's height)
        private static bool WaterNear(Vector3 q, float r, float water)
        {
            for (int ring = 1; ring <= 2; ring++)
                for (int a = 0; a < 8; a++)
                {
                    float deg = a * 45f * Mathf.Deg2Rad, d = r * ring / 2f;
                    if (WorldGenerator.instance.GetHeight(q.x + Mathf.Cos(deg) * d, q.z + Mathf.Sin(deg) * d) < water + 0.5f) return true;
                }
            return false;
        }

        // the world's own tests of a spot (q.y: the world's height there); what it fails is counted (Why)
        private static int _wet, _steep, _based, _placed, _ground;
        private static void Start() { _wet = _steep = _based = _placed = _ground = 0; Why = ""; }
        private static string Told() { return "вода " + _wet + ", круто " + _steep + ", база " + _based + ", місця світу " + _placed + ", земля " + _ground; }
        private static bool Fits(Player p, ref Vector3 q, string lands, float water, float shore, string ignore, bool anyLand)
        {
            if (!Dry(ref q, lands, water, shore)) return false;
            if (anyLand) return true;
            if (NearBase(p, q, ignore)) { _based++; return false; }
            if (NearPlace(q)) { _placed++; return false; }
            return true;
        }

        // the land's own tests, cheap: a land he knows, no water by it, not steep underfoot (q.y: the world's height there)
        private static bool Dry(ref Vector3 q, string lands, float water, float shore)
        {
            Heightmap.Biome b = WorldGenerator.instance.GetBiome(q);
            if (b == Heightmap.Biome.Ocean || b == Heightmap.Biome.None) { _wet++; return false; }
            if ((b == Heightmap.Biome.AshLands || b == Heightmap.Biome.DeepNorth) && (lands == null || !lands.Contains("|" + b + "|"))) return false;
            float h = WorldGenerator.instance.GetHeight(q.x, q.z);
            if (h < water + 1.5f || (shore > 0f && WaterNear(q, shore, water))) { _wet++; return false; }
            float steep = 0f;
            foreach (Vector2 o in new Vector2[] { new Vector2(2f, 0f), new Vector2(-2f, 0f), new Vector2(0f, 2f), new Vector2(0f, -2f) })
                steep = Mathf.Max(steep, Mathf.Abs(WorldGenerator.instance.GetHeight(q.x + o.x, q.z + o.y) - h));
            if (steep > 1.2f) { _steep++; return false; }
            q.y = h;
            return true;
        }

        private static Vector3 Round(Vector3 from, float min, float max)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            return from + new Vector3(dir.x, 0f, dir.y) * Random.Range(min, max);
        }

        // the first spot that fits
        internal static bool Pick(Player p, Vector3 from, float min, float max, bool loaded, string ignore, out Vector3 at, float shore = 0f, bool anyLand = false)
        {
            at = Vector3.zero;
            Start();
            if (p == null || WorldGenerator.instance == null) return false;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            string lands;
            p.m_customData.TryGetValue(Diary.LandsKey, out lands);
            for (int i = 0; i < Tries; i++)
            {
                Vector3 q = Round(from, min, max);
                if (!Fits(p, ref q, lands, water, shore, ignore, anyLand)) continue;
                if (loaded && !GroundClear(q, out q)) { _ground++; continue; }
                at = q;
                return true;
            }
            Why = Told();
            return false;
        }

        // the best spot: of Samples points round it the Finalists most open and flattest by the world's own numbers (cheap, so many), and of those,
        // asked the dear tests (bases, places, the ground, trees within r m), the one with fewest trees and flattest. A point
        // whose zone is not loaded: its trees unknown (counted as UnknownTrees). Trees and uneven: what it has. Once per pick.
        internal static bool Best(Player p, Vector3 from, float min, float max, float r, float shore, out Vector3 at, out int trees, out float uneven)
        {
            at = Vector3.zero; trees = 0; uneven = 0f;
            Start();
            if (p == null || WorldGenerator.instance == null || ZoneSystem.instance == null) return false;
            float water = ZoneSystem.instance.m_waterLevel, best = float.MaxValue;
            string lands;
            p.m_customData.TryGetValue(Diary.LandsKey, out lands);
            List<KeyValuePair<float, Vector3>> flat = new List<KeyValuePair<float, Vector3>>();
            for (int i = 0; i < Samples; i++)
            {
                Vector3 q = Round(from, min, max);
                if (Dry(ref q, lands, water, shore)) flat.Add(new KeyValuePair<float, Vector3>(Uneven(q, r) + Forest(q), q));
            }
            flat.Sort((x, y) => x.Key.CompareTo(y.Key));
            int asked = 0;
            foreach (KeyValuePair<float, Vector3> c in flat)
            {
                if (asked >= Finalists) break;
                if (c.Key >= best) break; // (flatter than this none is left, and trees only add)
                Vector3 q = c.Value;
                if (NearBase(p, q, null)) { _based++; continue; }
                if (NearPlace(q)) { _placed++; continue; }
                asked++;
                bool here = Here(q);
                if (here && !GroundClear(q, out q)) { _ground++; continue; }
                int t = here ? TreesNear(q, r) : 0, close = here ? TreesNear(q, Mathf.Min(r, CloseTrees)) : 0;
                float score = (here ? t + close : UnknownTrees) * TreeWeighs + c.Key;
                if (score >= best) continue;
                best = score; at = q; trees = t; uneven = Uneven(q, r);
            }
            Why = Told();
            return best < float.MaxValue;
        }

        // how far the ground rises and falls within r m (the world's height on three rings)
        internal static float Uneven(Vector3 q, float r)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int ring = 0; ring <= 3; ring++)
                for (int a = 0; a < (ring == 0 ? 1 : 8); a++)
                {
                    float deg = a * 45f * Mathf.Deg2Rad, d = r * ring / 3f, h = WorldGenerator.instance.GetHeight(q.x + Mathf.Cos(deg) * d, q.z + Mathf.Sin(deg) * d);
                    if (h < lo) lo = h;
                    if (h > hi) hi = h;
                }
            return hi - lo;
        }

        // your bed, your own marks on the map, a base area, anything built that is loaded near it (but «ignore»: the
        // finder's own piece)
        internal static bool NearBase(Player p, Vector3 q, string ignore)
        {
            try
            {
                PlayerProfile prof = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (prof != null && prof.HaveCustomSpawnPoint() && Flat(prof.GetCustomSpawnPoint(), q) < Clear * 1.5f) return true;
                if (Minimap.instance != null)
                    foreach (Minimap.PinData pin in Traverse.Create(Minimap.instance).Field("m_pins").GetValue<List<Minimap.PinData>>())
                        if (pin != null && pin.m_save && Flat(pin.m_pos, q) < Clear) return true;
            }
            catch (System.Exception e) { Log("map pins unreadable: " + e.Message); }
            if (EffectArea.GetBaseValue(q, Clear) > 0) return true;
            _pieces.Clear();
            Piece.GetAllPiecesInRadius(q, Clear, _pieces);
            foreach (Piece pc in _pieces) if (pc != null && pc.IsPlacedByPlayer() && Utils.GetPrefabName(pc.gameObject) != ignore) return true;
            return false;
        }
        private static readonly List<Piece> _pieces = new List<Piece>();

        internal static bool NearPlace(Vector3 q)
        {
            if (Location.IsInsideLocation(q, 40f) || Location.IsInsideNoBuildLocation(q)) return true;
            try
            {
                if (ZoneSystem.instance != null)
                    foreach (ZoneSystem.LocationInstance li in ZoneSystem.instance.GetLocationList())
                        if (Flat(li.m_position, q) < 60f) return true;
            }
            catch (System.Exception e) { Log("location list unreadable: " + e.Message); }
            return false;
        }

        // the ground itself, once its zone is loaded: no rock or piece over it, not under another's ward; the spot on
        // the ground's height
        internal static bool GroundClear(Vector3 q, out Vector3 spot)
        {
            spot = q;
            if (ZoneSystem.instance == null) return false;
            float ground = ZoneSystem.instance.GetGroundHeight(q);
            spot.y = ground;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(q.x, ground + 40f, q.z), Vector3.down, out hit, 45f, GroundMask) && hit.point.y > ground + 0.4f) return false; // a rock, a roof (a tree's crown may stand over it)
            if (!PrivateArea.CheckAccess(spot, 0f, false, false)) return false;
            if (Location.IsInsideLocation(spot, 30f)) return false;
            return true;
        }

        // trees standing within r m (a felled log or a stump does not count)
        private static readonly Collider[] _hits = new Collider[256]; // (a wood: bushes and stones on the same layers)
        private static int TreesNear(Vector3 q, float r)
        {
            int n = Physics.OverlapSphereNonAlloc(q, r, _hits, TreeMask); // heavy: a sphere, once per try of a loaded pick
            HashSet<TreeBase> seen = new HashSet<TreeBase>();
            for (int i = 0; i < n; i++)
            {
                TreeBase t = _hits[i] != null ? _hits[i].GetComponentInParent<TreeBase>() : null;
                if (t != null) seen.Add(t);
            }
            return seen.Count;
        }

        private static int _groundMask, _treeMask;
        private static int GroundMask { get { if (_groundMask == 0) _groundMask = LayerMask.GetMask("static_solid", "piece", "terrain"); return _groundMask; } }
        private static int TreeMask { get { if (_treeMask == 0) _treeMask = LayerMask.GetMask("Default", "static_solid", "Default_small"); return _treeMask; } }

        // its land here and its objects made (a zone loaded is not yet its trees: ZNetScene makes them a moment later)
        internal static bool Here(Vector3 q) { return ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(q) && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(q); }

        internal static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("WorldSpot: " + s); }
    }
}
#endif

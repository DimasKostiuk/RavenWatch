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
    // Open: the same land's tests where no player is asked (the server — a place of the world: the story's glade), by the world's numbers only.
    // Someones / Settled: «is this someone's base» as the SERVER knows it (it sees no beds nor map marks of a player, but it holds every object of
    // the world, loaded or not): a piece a player built. One answer for a place of the story (Loc.NearBuilding), a location of the game the story
    // picks and a glade (StoryFound) — the user 2026-11-21: a base with no heart and no ward was no base to the last two.
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

        // land all the way from a to b by the world's own height (every 8 m): water no longer than wetMost m at a stretch (SwimMost: a river one swims
        // across; 0 — none at all). One for everyone who asks «can one walk there»: a person made out of sight walking in (NpcTravel.ComeFrom), a place of
        // the story the player walks to (Open «onFoot», StoryFound pickloc — the user 2026-11-15: Heid's thistle edge was put 1753 m off across the sea).
        // A straight line: a bay between them reads as water though one may walk round it — so it is a «first», never a «must» for those who ask
        internal const float SwimMost = 40f;
        internal static bool DryWay(Vector3 a, Vector3 b, float wetMost)
        {
            if (WorldGenerator.instance == null || ZoneSystem.instance == null) return true;
            float water = ZoneSystem.instance.m_waterLevel + 0.3f, d = Utils.DistanceXZ(a, b), wet = 0f;
            for (float t = 8f; t < d; t += 8f)
            {
                Vector3 q = Vector3.Lerp(a, b, t / d);
                if (WorldGenerator.instance.GetHeight(q.x, q.z) >= water) { wet = 0f; continue; }
                wet += 8f;
                if (wet > wetMost) return false;
            }
            return true;
        }

        // the world's own tests of a spot (q.y: the world's height there); what it fails is counted (Why)
        private static int _wet, _steep, _based, _placed, _ground, _sea;
        internal static int SeaAll; // (every spot ever turned down for the sea: a caller asks whether «over water» could find anything more)
        private static void Start() { _wet = _steep = _based = _placed = _ground = _sea = 0; Why = ""; }
        private static string Told() { return "вода " + _wet + ", круто " + _steep + ", база " + _based + ", місця світу " + _placed + ", земля " + _ground + (_sea > 0 ? ", через море " + _sea : ""); }
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

        // The most open land for a place of the world, picked where no player is asked (the server: the story's glade, StoryFound «pickglade»):
        // of Samples points min..max m from «from» (within arc° either side of «toward», when it gives one), dry, all of it one land
        // (its middle and eight points on its edge r m out; None: any), no water within r + GladeShore m, away from the world's places
        // (NearPlace) and from «taken» by «clear» m: the flattest and least wooded by the world's own numbers (Uneven + Forest: the same
        // whether its zone was ever made or not — its trees are cleared away after). Once per pick: some 60 asks of the world's height a point.
        // edge: a land that must lie within edgeM m of it (the black forest's edge — Meadows within so many m: the user 2026-11-14, Heid's flowers);
        // None — no edge asked. onFoot: only a spot one walks to from «from» (DryWay, a river swum at most); asked first by whoever lays a place out
        // for the player to walk to (the last of the best checked only: the line costs up to max/8 asks of the height). fits: one more test of the
        // caller's own (a glade kept away from other places too — pickglade «apart», the user 2026-11-18: Eyvind's meadow 300 m from the Hall AND the middle)
        internal static float GladeShore = 6f;
        internal static bool Open(Vector3 from, float min, float max, float r, Heightmap.Biome land, Vector3 toward, float arc, List<Vector3> taken, float clear, out Vector3 at, out float uneven,
            Heightmap.Biome edge = Heightmap.Biome.None, float edgeM = 0f, bool onFoot = false, System.Func<Vector3, bool> fits = null)
        {
            at = Vector3.zero; uneven = 0f;
            Start();
            if (WorldGenerator.instance == null || ZoneSystem.instance == null) return false;
            float water = ZoneSystem.instance.m_waterLevel, best = float.MaxValue;
            toward.y = 0f;
            bool aimed = toward.sqrMagnitude > 0.01f && arc < 180f;
            if (aimed) toward.Normalize();
            List<Vector3> rays = edge != Heightmap.Biome.None ? EdgeSpots(from, min, max, r, land, edge, aimed ? toward : Vector3.forward, aimed ? arc : 180f) : null;
            int n = (rays != null ? rays.Count : 0) + Samples;
            for (int i = 0; i < n; i++)
            {
                bool onRay = rays != null && i < rays.Count;
                Vector3 q = onRay ? rays[i] : aimed ? from + Quaternion.Euler(0f, Random.Range(-arc, arc), 0f) * toward * Random.Range(min, max) : Round(from, min, max);
                if (!Dry(ref q, null, water, r + GladeShore)) continue;
                if (!OneLand(q, r, land) || !Borders(q, edgeM, edge)) { _ground++; continue; }
                float score = Uneven(q, r) + Forest(q) + (edge != Heightmap.Biome.None ? Mathf.Max(0f, Utils.DistanceXZ(from, q) - min) / EdgeFarPer : 0f); // (an edge: the nearer the better — the forest's start on the side of «from»)
                if (score >= best) continue;
                if (taken != null && taken.Exists(b => Flat(b, q) < clear)) { _based++; continue; }
                if (fits != null && !fits(q)) { _based++; continue; } // (the caller's own test: counted with the bases — «too near»)
                if (NearPlace(q)) { _placed++; continue; }
                if (onFoot && !DryWay(from, q, SwimMost)) { _sea++; SeaAll++; continue; }
                best = score; at = q; uneven = Uneven(q, r);
            }
            Why = Told();
            return best < float.MaxValue;
        }

        // an edge's spots, found rather than hit by chance (the user 2026-11-15: of 160 random points none fell on the black forest's edge by Heid's camp —
        // a strip some 25 m wide — and the pick went 1753 m off): from «from» out along EdgeRayCount rays (within arc° of «toward»), every EdgeStep m min..max,
        // where the edge's land turns into «land» — the forest's start on the side of «from» — the spot r + EdgeIn m inside it; at most two a ray.
        // Some EdgeRayCount·max/EdgeStep asks of the world's land (48·75 for 600 m): once per pick
        internal static int EdgeRayCount = 48;
        internal static float EdgeStep = 8f, EdgeIn = 4f, EdgeFarPer = 50f; // (EdgeFarPer: so many m farther weigh as one m of slope)
        private static List<Vector3> EdgeSpots(Vector3 from, float min, float max, float r, Heightmap.Biome land, Heightmap.Biome edge, Vector3 toward, float arc)
        {
            List<Vector3> l = new List<Vector3>();
            if (land == Heightmap.Biome.None) return l; // (no land asked: no edge to cross into)
            for (int i = 0; i < EdgeRayCount; i++)
            {
                float deg = arc >= 180f ? i * 360f / EdgeRayCount : -arc + 2f * arc * i / Mathf.Max(1, EdgeRayCount - 1);
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * toward;
                Heightmap.Biome was = WorldGenerator.instance.GetBiome(from + dir * min);
                int got = 0;
                for (float d = min + EdgeStep; d <= max && got < 2; d += EdgeStep)
                {
                    Vector3 p = from + dir * d;
                    Heightmap.Biome b = WorldGenerator.instance.GetBiome(p);
                    if (b == land && was == edge) { l.Add(p + dir * (r + EdgeIn)); got++; }
                    was = b;
                }
            }
            return l;
        }

        private static bool OneLand(Vector3 q, float r, Heightmap.Biome land)
        {
            if (land == Heightmap.Biome.None) return true;
            if (WorldGenerator.instance.GetBiome(q) != land) return false;
            for (int a = 0; a < 8; a++)
            {
                float deg = a * 45f * Mathf.Deg2Rad;
                if (WorldGenerator.instance.GetBiome(new Vector3(q.x + Mathf.Cos(deg) * r, 0f, q.z + Mathf.Sin(deg) * r)) != land) return false;
            }
            return true;
        }

        // that land within m of it: one of eight points on each of two rings (m/2, m) is of it
        private static bool Borders(Vector3 q, float m, Heightmap.Biome land)
        {
            if (land == Heightmap.Biome.None) return true;
            for (int ring = 1; ring <= 2; ring++)
                for (int a = 0; a < 8; a++)
                {
                    float deg = a * 45f * Mathf.Deg2Rad, d = m * ring / 2f;
                    if (WorldGenerator.instance.GetBiome(new Vector3(q.x + Mathf.Cos(deg) * d, 0f, q.z + Mathf.Sin(deg) * d)) == land) return true;
                }
            return false;
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

#if RAVEN_NPC
        // someone's: a piece a player built (it has its creator; a blueprint's pieces — the story's own — are not his). A prefab unknown to this
        // game counts: better a wrong «no» than a hall on a house
        private static readonly Dictionary<int, bool> _isPiece = new Dictionary<int, bool>();
        internal static bool Someones(ZDO zdo)
        {
            if (zdo == null || zdo.GetLong(ZDOVars.s_creator, 0L) == 0L || zdo.GetInt(Blueprint.Mark) == 1) return false;
            int hash = zdo.GetPrefab();
            bool p;
            if (_isPiece.TryGetValue(hash, out p)) return p;
            GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            p = pf == null || pf.GetComponent<Piece>() != null;
            _isPiece[hash] = p;
            return p;
        }

        // where someone's pieces stand in a zone, by the server's own list of the world's objects (a client holds only what is near it: the server
        // asks). Kept for the frame: a pick asks many spots of the same few zones (a hall's tries and the camp's by it, a glade's candidates), and
        // each zone is gone through once — its objects looked at one by one — however many ask. Nothing kept past the frame: a piece built next
        // second is seen by the next pick
        private static readonly Dictionary<Vector2s, List<Vector3>> _builtIn = new Dictionary<Vector2s, List<Vector3>>();
        private static readonly List<ZDO> _around = new List<ZDO>();
        private static int _builtFrame = -1;
        internal static int BuiltLooks; // (zones gone through since the game began: a pick's log says how many it cost)
        internal static List<Vector3> BuiltIn(Vector2s zone)
        {
            if (_builtFrame != Time.frameCount) { _builtFrame = Time.frameCount; _builtIn.Clear(); }
            List<Vector3> l;
            if (_builtIn.TryGetValue(zone, out l)) return l;
            _builtIn[zone] = l = new List<Vector3>();
            _around.Clear();
            BlueprintGround.ObjectsOf(zone, _around);
            BuiltLooks++;
            foreach (ZDO z in _around) if (Someones(z)) l.Add(z.GetPosition());
            return l;
        }

        // anything of someone's within m of a spot. Asked after the cheap tests of a spot, never of every try
        internal static bool Settled(Vector3 at, float m)
        {
            foreach (Vector2s zone in BlueprintGround.ZonesOf(at, m))
                foreach (Vector3 p in BuiltIn(zone)) if (Flat(p, at) < m) return true;
            return false;
        }
#endif

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

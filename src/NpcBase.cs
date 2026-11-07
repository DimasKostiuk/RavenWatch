#if RAVEN_ITEM && RAVEN_NPC
using StringComparison = System.StringComparison;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Where a resident lives (npc-plan.md, stage 1): its base, found from the place it was settled at.
    //   A map of 1 m cells round that place (up to Reach). Walls, palisades, fences, doors and gates the players built are
    //   the edges, and so is the land where nobody walks: a steep bank (a moat's sides, a cliff: more than Bank metres up
    //   or down to the next cell) and water — unless something built lies over it (a bridge across the moat, a floor over
    //   a slope): that is a way through, as a gate is. A moat, a fence, or both: one edge. A «paint» is poured in from the rim of the map: what it cannot reach is ENCLOSED (a yard behind a fence
    //   and the houses in it — the bed in the far corner, the chests by the gate: all its own).
    //     settled in an enclosed place of at least Yard m²  → FENCED: the base is every enclosed cell
    //     settled in the open, or only inside one house      → OPEN: a circle of Open m plus a strip round the buildings,
    //                                                         and the enclosed rooms in it (it may go out to the fire);
    //                                                         never the water (the sea, a lake) round it
    // Cheap: made once, spread over a few frames, and made again only when the buildings round it change (their count,
    // looked at every 30 s) or after a few minutes; in between it is a lookup in a ready map. One map per place, shared
    // by everyone living there.
    internal sealed class NpcBase
    {
        internal const float Reach = 50f, Open = 20f, Strip = 4f, Cell = 1f;
        private const int Yard = 200; // m²: smaller than this, an enclosed place is a house, not a fenced yard
        private const float Bank = 1.2f; // m of height from one cell to the next (≈50°): steeper is a bank nobody walks
        private const float Recheck = 30f, MaxAge = 300f, Stuck = 30f, Least = 60f, Forgotten = 600f;
        // (Least: never made again sooner than this — a player building changes the count all the time; Forgotten: a base
        // nobody asked about for this long is let go)

        private static readonly Dictionary<long, NpcBase> All = new Dictionary<long, NpcBase>();
        private static readonly Collider[] Hits = new Collider[32];

        internal readonly Vector3 Centre;
        internal bool Ready, Fenced, Making;
        internal int Pieces, Blockers, Banks;
        private float[] _h = new float[0]; // the land's height of each cell (for showing the edges)
        private readonly int _w;
        private bool[] _in = new bool[0];
        private readonly List<int> _cells = new List<int>();
        private readonly List<int> _edge = new List<int>(); // its cells on its OUTER edge (MakeEdge)
        private float _edgeFar, _edgeNear; // (m from the centre: the average and the nearest of them, for its description)
        private float _madeAt = -1000f, _checkAt, _startedAt, _askedAt;

        // ---------------------------------------------------------------- what stands in it, by kind
        // Made with its map and again (alone, cheaper) when its buildings change in between: one look over its pieces for
        // everyone who lives there — NpcWork's fires, smelters, chests…, NpcMeals' tables, NpcChores' wear, NpcWhims' tub —
        // instead of each of them looking over all of them every few seconds. A thing gone since is a null in a list.
        internal static readonly System.Type[] Kinds = { typeof(Fireplace), typeof(Smelter), typeof(CookingStation), typeof(Beehive), typeof(Fermenter), typeof(Container), typeof(Bed), typeof(Chair), typeof(CraftingStation), typeof(ItemStand), typeof(Door) };
        // …and these all round it (within Reach, in it or not: a fire in the yard by the heart, a seat on the porch, a door in its wall — a
        // door stands on the edge, never inside): the people's seat, fire and door searches read them instead of going over every piece of
        // the world near (each took 6–7 ms on a big base, 2026-11-05: the load test). A chair: every seat of its piece (a bench has two)
        internal static readonly System.Type[] RoundKinds = { typeof(Fireplace), typeof(Chair), typeof(Door) };
        private Dictionary<System.Type, List<Component>> _round;
        internal List<Component> Round(System.Type t) { List<Component> l; return _round != null && _round.TryGetValue(t, out l) ? l : null; }

        // the base whose look round it covers this spot (its round lists hold what is near it), null: none ready here
        internal static NpcBase Covering(Vector3 at)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) return null;
            foreach (NpcBase b in All.Values) if (b._round != null && Utils.DistanceXZ(at, b.Centre) <= Reach) return b;
            return null;
        }
        private Dictionary<System.Type, List<Component>> _things;
        private List<Transform> _tables;
        private List<WearNTear> _wears;
        private List<Piece> _tubs;
        private bool _thingsMaking;
        private static readonly Dictionary<string, bool> WetKinds = new Dictionary<string, bool>(); // a kind of piece → has water of its own (asked once a kind)
        private static readonly int PerfThings = NpcPerf.Id("base things (знімок бази)");

        // its things of a kind (one of Kinds), null — not made yet or not a kind kept
        internal List<Component> Things(System.Type t) { List<Component> l; return _things != null && _things.TryGetValue(t, out l) ? l : null; }
        internal List<Transform> Tables { get { return _tables; } }   // the tables to eat at (not a workbench, a map table)
        internal List<WearNTear> Wears { get { return _wears; } }     // every piece that wears (round it, as NpcChores always counted)
        internal List<Piece> Tubs { get { return _tubs; } }           // pieces with water of their own (a hot tub, a mod's pool)
        private static object _world;   // the world these maps are of (another world: all forgotten)
        private static float _nextPrune;

        private NpcBase(Vector3 centre)
        {
            Centre = centre;
            _w = Mathf.RoundToInt(Reach * 2f / Cell) + 1;
        }

        // the base round this place (made when first asked; kept fresh while asked)
        internal static NpcBase For(Vector3 home)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { All.Clear(); _world = ZNet.instance; } // (a world left and another loaded)
            if (Time.time >= _nextPrune) Prune();
            long key = ((long)Mathf.RoundToInt(home.x) << 32) ^ (uint)Mathf.RoundToInt(home.z);
            NpcBase b;
            if (!All.TryGetValue(key, out b)) { b = new NpcBase(home); All[key] = b; }
            b._askedAt = Time.time;
            b.Fresh();
            return b;
        }

        // the maps nobody asked about for a while go (each is some 90 KB)
        private static void Prune()
        {
            _nextPrune = Time.time + 60f;
            List<long> old = null;
            foreach (KeyValuePair<long, NpcBase> kv in All)
                if (!kv.Value.Making && Time.time - kv.Value._askedAt > Forgotten) { if (old == null) old = new List<long>(); old.Add(kv.Key); }
            if (old != null) foreach (long k in old) { All[k].Hide(); All.Remove(k); }
        }

        private void Fresh()
        {
            if (Making && Time.time - _startedAt < Stuck) return;
            if (Time.time < _checkAt) return;
            _checkAt = Time.time + Recheck;
            bool young = Ready && Time.time - _madeAt < Least;
            bool stale = !Ready || Time.time - _madeAt > MaxAge;
            if (!stale)
            {
                List<Piece> now = new List<Piece>(); Piece.GetAllPiecesInRadius(Centre, Reach, now);
                bool changed = now.Count != _thingsCount || _lookVersion != LocState.LookVersion; // (a place's state changed: what it has now is another list)
                if (changed && Ready && !_thingsMaking && Plugin.Instance != null) { _thingsMaking = true; Plugin.Instance.StartCoroutine(MakeThings(now, _in)); } // (a chest put down: seen within Recheck s, its map later)
                if (young) return;
                stale = now.Count != Pieces;
            }
            if (stale && Plugin.Instance != null) { Making = true; _startedAt = Time.time; Plugin.Instance.StartCoroutine(Make()); } // (a making that broke off is started again after Stuck s)
        }

        // ---------------------------------------------------------------- inside / a spot in it
        internal bool Inside(Vector3 p)
        {
            if (!Ready) return Utils.DistanceXZ(p, Centre) <= Open;
            int i = Index(p);
            return i >= 0 && _in[i];
        }

        // a random spot of its own on the land (round the centre while it has no map yet)
        internal Vector3 Spot()
        {
            if (!Ready || _cells.Count == 0)
            {
                Vector2 r = Random.insideUnitCircle * Open;
                Vector3 p = Ground(Centre + new Vector3(r.x, 0f, r.y));
                return Wet(p) ? Centre : p;
            }
            return Ground(CellCentre(_cells[Random.Range(0, _cells.Count)]));
        }

        // a spot on its outer edge, on the land (the guard's round); the centre while it has no map
        internal Vector3 EdgeSpot()
        {
            if (!Ready || _edge.Count == 0) return Spot();
            return Ground(CellCentre(_edge[Random.Range(0, _edge.Count)]));
        }

        // Its OUTER edge: its cells next to what lies outside it, reached from the map's rim through cells not its own. Any cell by a cell not
        // its own counted before: by a wall, a fence, a slope in the middle of the village, so a guard «on the edge» walked about its centre
        // (the user 2026-11-04). Once a map, over its cells (about ten thousand), with the map.
        private void MakeEdge(bool[] inside)
        {
            bool[] outside = new bool[inside.Length];
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < _w; i++)
            {
                Step(i, 0, inside, outside, q); Step(i, _w - 1, inside, outside, q);
                Step(0, i, inside, outside, q); Step(_w - 1, i, inside, outside, q);
            }
            Flood(q, inside, outside);
            _edge.Clear();
            float sum = 0f; _edgeNear = float.MaxValue;
            foreach (int c in _cells)
            {
                int x = c % _w, y = c / _w;
                if (!Out(x + 1, y, outside) && !Out(x - 1, y, outside) && !Out(x, y + 1, outside) && !Out(x, y - 1, outside)) continue;
                _edge.Add(c);
                float d = Utils.DistanceXZ(CellCentre(c), Centre);
                sum += d; if (d < _edgeNear) _edgeNear = d;
            }
            _edgeFar = _edge.Count > 0 ? sum / _edge.Count : 0f;
        }
        private bool Out(int x, int y, bool[] outside) { return x < 0 || y < 0 || x >= _w || y >= _w || outside[x + y * _w]; }

        // a spot of its own land by the water (to wash at): a cell with water a couple of steps off; false: none
        internal bool ShoreSpot(out Vector3 at, out Vector3 water)
        {
            at = water = Centre;
            if (!Ready || _cells.Count == 0) return false;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (int tries = 0; tries < 60; tries++)
            {
                Vector3 p = CellCentre(_cells[Random.Range(0, _cells.Count)]);
                foreach (Vector3 d in dirs) if (Wet(p + d * 2f)) { at = Ground(p); water = p + d * 2f; return true; }
            }
            return false;
        }

        internal string Describe()
        {
            if (!Ready) return Making ? "база рахується…" : "база ще не порахована";
            return (Fenced ? "огороджена" : "відкрита (коло " + Mathf.RoundToInt(Open) + " м + смуга біля будівель)") + ", " + _cells.Count + " м², межа " + _edge.Count + " м (від центру в середньому " + Mathf.RoundToInt(_edgeFar) + " м, найближче " + Mathf.RoundToInt(_edgeNear) + " м), будівель " + Pieces + ", з них стін і дверей " + Blockers + ", схилів і води " + Banks;
        }

        // ---------------------------------------------------------------- making the map
        private IEnumerator Make()
        {
            int n = _w * _w;
            // the land's height of every cell, once (the game finds it with a ray from the sky: a thousand a frame)
            float[] h = new float[n];
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : -1000f;
            for (int c = 0; c < n; c++)
            {
                h[c] = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(CellCentre(c)) : Centre.y;
                if ((c + 1) % 1000 == 0) yield return null;
            }
            bool[] wall = new bool[n], near = new bool[n];
            List<Piece> pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(Centre, Reach, pieces);
            int blockers = 0, done = 0;
            foreach (Piece pc in pieces)
            {
                if (pc == null) continue;
                Stamp(near, pc.transform.position, Strip);
                if (Edge(pc, h)) { blockers++; Rasterise(wall, pc); }
                if (++done % 20 == 0) yield return null; // (a few pieces a frame)
            }
            // the land itself: steep banks and water are edges too, unless something built lies over them (a bridge, a floor)
            int banks = 0;
            for (int c = 0; c < n; c++)
            {
                if (wall[c]) continue;
                int x = c % _w, y = c / _w;
                float top;
                bool edge = h[c] < water - 0.3f;
                top = Mathf.Max(h[c], Higher(x + 1, y, h), Higher(x - 1, y, h), Higher(x, y + 1, h), Higher(x, y - 1, h));
                if (!edge) edge = top - h[c] > Bank || h[c] - Lower(x, y, h) > Bank;
                if (!edge || Bridged(c, h[c], Mathf.Max(top, water))) continue;
                wall[c] = true; banks++;
                if (banks % 300 == 0) yield return null;
            }
            // the paint from the rim inwards: what it cannot reach is enclosed
            bool[] outside = new bool[n];
            Queue<int> q = new Queue<int>();
            float rim = Reach - Cell * 1.5f;
            for (int c = 0; c < n; c++)
                if (!wall[c] && Utils.DistanceXZ(CellCentre(c), Centre) >= rim) { outside[c] = true; q.Enqueue(c); }
            Flood(q, wall, outside);
            yield return null;
            int home = Index(Centre);
            bool homeEnclosed = home >= 0 && !outside[home];
            int yard = 0;
            if (homeEnclosed) { bool[] mine = new bool[n]; Queue<int> hq = new Queue<int>(); mine[home] = true; hq.Enqueue(home); yard = Flood(hq, OrOf(wall, outside), mine); }
            bool fenced = homeEnclosed && yard >= Yard;
            bool[] inside = new bool[n];
            _cells.Clear();
            for (int c = 0; c < n; c++)
            {
                if (wall[c]) continue;
                float d = Utils.DistanceXZ(CellCentre(c), Centre);
                if (d > Reach) continue;
                bool enclosed = !outside[c];
                bool own = fenced ? enclosed : (enclosed || d <= Open || near[c]);
                if (own && !enclosed && h[c] < water - 0.3f) own = false; // (the sea or a lake by the base: not a place to stroll; a house's floor over it counts as enclosed)
                inside[c] = own;
                if (own) _cells.Add(c);
            }
            _thingsMaking = true;
            yield return MakeThings(pieces, inside);
            _in = inside; _h = h; Fenced = fenced; Pieces = pieces.Count; Blockers = blockers; Banks = banks;
            MakeEdge(inside);
            Ready = true; Making = false; _madeAt = Time.time;
            Npcs.Log("base at " + Mathf.RoundToInt(Centre.x) + "," + Mathf.RoundToInt(Centre.z) + ": " + Describe());
        }

        private int _thingsCount = -1, _lookVersion = -1;
        private IEnumerator MakeThings(List<Piece> pieces, bool[] inside)
        {
            int lookVersion = LocState.LookVersion;
            Dictionary<System.Type, List<Component>> things = new Dictionary<System.Type, List<Component>>();
            foreach (System.Type t in Kinds) things[t] = new List<Component>();
            Dictionary<System.Type, List<Component>> round = new Dictionary<System.Type, List<Component>>();
            foreach (System.Type t in RoundKinds) round[t] = new List<Component>();
            List<Transform> tables = new List<Transform>();
            List<WearNTear> wears = new List<WearNTear>();
            List<Piece> tubs = new List<Piece>();
            int done = 0;
            long perf = NpcPerf.Now();
            foreach (Piece pc in pieces)
            {
                if (pc == null) continue;
                // A place of the story (its pieces carry its stamp) is not a home's things: it belongs to the base that stands in it (its own people's, a
                // story person's: a settler's heart may stand just outside its no-build ring, and it is no one's bench, chest or thing to mend). What its
                // state has not is nobody's. The story's own spots go past this (people: StoryLife)
                ZNetView pnv = pc.GetComponent<ZNetView>();
                Loc place = Blueprint.Copy(pnv) ? Locs.Get(pnv.GetZDO().GetString(LocParts.KOf, "")) : null;
                if (place != null && (!place.Holds(Centre) || LocState.Absent(pnv))) { if (++done % 25 == 0) { NpcPerf.Add(PerfThings, perf); yield return null; perf = NpcPerf.Now(); } continue; }
                WearNTear w = pc.GetComponent<WearNTear>();
                if (w != null && place == null) wears.Add(w); // (mended whole by the story, never by a hammer: LocRepairPatch)
                foreach (System.Type t in Kinds)
                {
                    if (place != null && place.Keeps(t)) continue;
                    Component c = pc.GetComponentInChildren(t);
                    if (c == null) continue;
                    bool rounds = System.Array.IndexOf(RoundKinds, t) >= 0;
                    if (t == typeof(Chair)) { foreach (Chair ch in pc.GetComponentsInChildren<Chair>()) { if (ch.m_inShip) continue; round[t].Add(ch); if (In(inside, ch.transform.position)) things[t].Add(ch); } continue; } // heavy: once a snapshot, a piece with a seat only
                    if (rounds) round[t].Add(c);
                    if (In(inside, c.transform.position)) things[t].Add(c);
                }
                if (In(inside, pc.transform.position))
                {
                    if (Utils.GetPrefabName(pc.gameObject).ToLowerInvariant().Contains("table") && pc.GetComponentInChildren<CraftingStation>() == null && pc.GetComponentInChildren<MapTable>() == null) tables.Add(pc.transform); // (piece_table, _round, _oak — and other mods' tables; not a workbench, a prep table, a map table)
                    bool wet; string kind = pc.m_name ?? "";
                    if (!WetKinds.TryGetValue(kind, out wet)) { wet = pc.GetComponentInChildren<WaterVolume>() != null; WetKinds[kind] = wet; }
                    if (wet) tubs.Add(pc);
                }
                if (++done % 25 == 0) { NpcPerf.Add(PerfThings, perf); yield return null; perf = NpcPerf.Now(); } // (a few pieces a frame)
            }
            NpcPerf.Add(PerfThings, perf);
            _things = things; _round = round; _tables = tables; _wears = wears; _tubs = tubs; _thingsCount = pieces.Count; _thingsMaking = false; _lookVersion = lookVersion;
        }

        // inside by a map being made (the cells), or — none yet — round the centre
        private bool In(bool[] inside, Vector3 p)
        {
            if (inside == null || inside.Length == 0) return Utils.DistanceXZ(p, Centre) <= Open;
            int i = Index(p);
            return i >= 0 && i < inside.Length && inside[i];
        }

        // fills from what is queued through every cell not blocked; how many it filled
        private int Flood(Queue<int> q, bool[] blocked, bool[] filled)
        {
            int count = q.Count;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int x = c % _w, y = c / _w;
                count += Step(x + 1, y, blocked, filled, q) + Step(x - 1, y, blocked, filled, q) + Step(x, y + 1, blocked, filled, q) + Step(x, y - 1, blocked, filled, q);
            }
            return count;
        }

        private int Step(int x, int y, bool[] blocked, bool[] filled, Queue<int> q)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _w) return 0;
            int c = x + y * _w;
            if (filled[c] || blocked[c]) return 0;
            filled[c] = true;
            q.Enqueue(c);
            return 1;
        }

        // a neighbour's height (none: very low / very high, so it adds nothing)
        private float Higher(int x, int y, float[] h) { return x < 0 || y < 0 || x >= _w || y >= _w ? float.MinValue : h[x + y * _w]; }
        private float Lower(int x, int y, float[] h)
        {
            float low = h[x + y * _w];
            if (x + 1 < _w) low = Mathf.Min(low, h[x + 1 + y * _w]);
            if (x > 0) low = Mathf.Min(low, h[x - 1 + y * _w]);
            if (y + 1 < _w) low = Mathf.Min(low, h[x + (y + 1) * _w]);
            if (y > 0) low = Mathf.Min(low, h[x + (y - 1) * _w]);
            return low;
        }

        // something built over this cell at about the bank's top (a bridge, a dock, a floor): looked for from just above
        // the bank's top down to the land, so a roof high over it does not count
        private static readonly int Decks = LayerMask.GetMask("piece");
        private bool Bridged(int c, float ground, float top)
        {
            Vector3 mid = CellCentre(c);
            float from = top + 1.2f;
            RaycastHit hit;
            return Physics.Raycast(new Vector3(mid.x, from, mid.z), Vector3.down, out hit, from - ground + 0.2f, Decks, QueryTriggerInteraction.Ignore) && hit.point.y > ground + 0.4f;
        }

        // ---------------------------------------------------------------- showing it (the menu's «Межі бази»)
        // gold sparks on the ground along its edge, and marks on the map, for a while
        private GameObject _shown;
        private static Material _spark; // (one for every showing)
        private readonly List<Minimap.PinData> _shownPins = new List<Minimap.PinData>();

        internal void Show(float seconds)
        {
            if (!Ready || Plugin.Instance == null) return;
            Hide();
            _shown = new GameObject("RavenWatch_BaseEdge");
            if (_spark == null) { Shader sh = Shader.Find("Sprites/Default"); if (sh != null) { _spark = new Material(sh); _spark.color = new Color(1f, 0.8f, 0.3f, 0.9f); } }
            Material mat = _spark;
            int dots = 0, pins = 0;
            foreach (int c in _edge)
            {
                if (dots >= 900) break;
                int x = c % _w, y = c / _w;
                if ((x + y) % 2 != 0) continue; // (every other one is enough to see the line)
                Vector3 at = CellCentre(c); at.y = (c < _h.Length ? _h[c] : at.y) + 0.35f;
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(dot.GetComponent<Collider>());
                dot.transform.SetParent(_shown.transform, false);
                dot.transform.position = at; dot.transform.localScale = Vector3.one * 0.28f;
                Renderer r = dot.GetComponent<Renderer>();
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dots++;
                if (dots % 4 == 0 && pins < 160 && Minimap.instance != null) { _shownPins.Add(Minimap.instance.AddPin(at, Minimap.PinType.Icon3, "", false, false, 0L, default(Splatform.PlatformUserID))); pins++; }
            }
            Plugin.Instance.StartCoroutine(HideLater(seconds));
        }

        private bool Own(int x, int y) { return x >= 0 && y >= 0 && x < _w && y < _w && _in[x + y * _w]; }

        private IEnumerator HideLater(float seconds) { GameObject was = _shown; yield return new WaitForSeconds(seconds); if (_shown == was) Hide(); }

        private void Hide()
        {
            if (_shown != null) UnityEngine.Object.Destroy(_shown);
            _shown = null;
            if (Minimap.instance != null) foreach (Minimap.PinData p in _shownPins) if (p != null) Minimap.instance.RemovePin(p);
            _shownPins.Clear();
        }

        private static bool[] OrOf(bool[] a, bool[] b) { bool[] r = new bool[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = a[i] || b[i]; return r; }

        // an edge of the base: a wall, a palisade, a fence, a door, a gate — something a player built standing on the
        // ground and taller than a step (not a floor, a roof, a table)
        private bool Edge(Piece pc, float[] h)
        {
            if (pc.GetComponent<WearNTear>() == null) return false;
            if (pc.GetComponent<Door>() != null) return true;
            if (pc.m_name != null && pc.m_name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0) return false; // (stairs are walked on, not a wall)
            foreach (Collider c in pc.GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger) continue;
                Bounds b = c.bounds;
                if (b.size.y < 1f) continue;
                int at = Index(b.center);
                float ground = at >= 0 ? h[at] : b.min.y; // (the heights already found: no ray of its own)
                if (b.min.y <= ground + 1f) return true;
            }
            return false;
        }

        // the cells it stands on: a cell-sized box at the cell, as tall as the piece, touching this collider (works for
        // every collider kind, a slanted wall marks only its own cells)
        private void Rasterise(bool[] wall, Piece pc)
        {
            foreach (Collider c in pc.GetComponentsInChildren<Collider>())
            {
                if (c == null || c.isTrigger) continue;
                Bounds b = c.bounds;
                if (b.size.y < 0.5f) continue;
                int mask = 1 << c.gameObject.layer;
                Vector3 half = new Vector3(Cell * 0.45f, Mathf.Max(0.25f, b.extents.y), Cell * 0.45f);
                int x0 = Mathf.FloorToInt((b.min.x - Centre.x + Reach) / Cell), x1 = Mathf.CeilToInt((b.max.x - Centre.x + Reach) / Cell);
                int y0 = Mathf.FloorToInt((b.min.z - Centre.z + Reach) / Cell), y1 = Mathf.CeilToInt((b.max.z - Centre.z + Reach) / Cell);
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_w - 1, y1); y++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_w - 1, x1); x++)
                    {
                        int i = x + y * _w;
                        if (wall[i]) continue;
                        Vector3 mid = CellCentre(i); mid.y = b.center.y;
                        int got = Physics.OverlapBoxNonAlloc(mid, half, Hits, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                        for (int k = 0; k < got; k++) if (Hits[k] == c) { wall[i] = true; break; }
                    }
            }
        }

        private void Stamp(bool[] near, Vector3 at, float r)
        {
            int cx = Mathf.RoundToInt((at.x - Centre.x + Reach) / Cell), cy = Mathf.RoundToInt((at.z - Centre.z + Reach) / Cell), n = Mathf.CeilToInt(r / Cell);
            for (int y = Mathf.Max(0, cy - n); y <= Mathf.Min(_w - 1, cy + n); y++)
                for (int x = Mathf.Max(0, cx - n); x <= Mathf.Min(_w - 1, cx + n); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= n * n) near[x + y * _w] = true;
        }

        private int Index(Vector3 p)
        {
            int x = Mathf.RoundToInt((p.x - Centre.x + Reach) / Cell), y = Mathf.RoundToInt((p.z - Centre.z + Reach) / Cell);
            return x < 0 || y < 0 || x >= _w || y >= _w ? -1 : x + y * _w;
        }

        private Vector3 CellCentre(int i) { return new Vector3(Centre.x - Reach + (i % _w) * Cell, Centre.y, Centre.z - Reach + (i / _w) * Cell); }

        // under water: the land lower than the sea (a little margin for the shore)
        private static bool Wet(Vector3 p)
        {
            if (ZoneSystem.instance == null) return false;
            return ZoneSystem.instance.GetGroundHeight(p) < ZoneSystem.instance.m_waterLevel - 0.3f;
        }

        // on the land (not on a roof: the path finder takes it to the floor over it, if there is one)
        private static Vector3 Ground(Vector3 p)
        {
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }
    }
}
#endif

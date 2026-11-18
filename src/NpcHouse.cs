#if RAVEN_ITEM && RAVEN_NPC
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A house (npc-plan.md, «слуга»): the rooms under one roof round a spot, on every floor of it — what a servant keeps
    // (one, or several sharing it: they never take the same fire or thing, Reserve). Found from the spot pointed at, a
    // cell (1 m) at a time:
    //   the floor pointed at first, as a closed place: its walls mapped (a wall, a door, a cliff it leans on — at 1.6 m,
    //   above the furniture), the gaps up to 2 m shut for the look (a doorway with no door, a window, one wall left out),
    //   the open air poured in from round it and from every cell under the open sky (a house in a fenced yard: the yard
    //   is outside too) — what the air does not reach is closed in; the house is the closed part
    //   where it was pointed at, spread through its real ways (inner doors and arches join rooms into one house), under a
    //   roof. A porch, the eaves, a covered way to the next house, a pavilion: not in it.
    //   a house not closed (a gap wider than 2 m: a barn's gate, a wall left open): by its roof as before, said so
    //   then its other floors: over and under that floor's cells (and 2 m round them), the floors built at other heights
    //   (an upper storey, a loft, a cellar) — each spread by its roof and its walls. The stairs between them the servant walks (the game's paths).
    // So rooms behind inner doors are one house, the yard is not (no roof over it), every storey of it is the same house.
    // Its things (fires, torches on its walls, its doors, what lies on its floors) are those on a floor of it or its edge.
    // Made a little at a time over frames (physics only, on the game that asks: nothing sent), once, and again at most
    // every few minutes when asked; one for every spot inside it (a second servant pointed at another room gets it too).
    internal sealed class NpcHouse
    {
        private const float Cell = 1f, Reach = 22f, Again = 300f, Low = 1.2f, Above = 14f, Storey = 1.8f, Up = 13f, Down = 8f;
        private const int Most = 700;   // cells of a floor: bigger than this is no house (an open roof over a whole yard)
        private const int Levels = 5;   // floors at most (a cellar, the ground floor, two storeys, a loft)
        private static readonly List<NpcHouse> All = new List<NpcHouse>();
        private static object _world;
        private static int _floorMask, _roofMask, _wallMask;

        private sealed class Level { public float Floor; public bool[] In; public int Count; }

        internal readonly Vector3 At;
        internal float Floor; // (the floor pointed at)
        internal bool Ready, Making, TooBig, Open; // (Open: not closed in — found by its roof only)
        internal int Count;
        private readonly int _w;
        private readonly List<Level> _levels = new List<Level>();
        private float _madeAt = -1000f;
        private bool _showWhenReady;

        private NpcHouse(Vector3 at) { At = at; _w = Mathf.RoundToInt(Reach * 2f / Cell) + 1; }

        // the house round a spot: one already known that holds it, else a new look from it
        internal static NpcHouse For(Vector3 at) { return For(at, Again); }

        // ...looked over again only when older than «again» s (a player's house, NpcPrivate: seldom)
        internal static NpcHouse For(Vector3 at, float again)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { All.Clear(); _world = ZNet.instance; }
            NpcHouse h = null;
            foreach (NpcHouse o in All) if ((o.At - at).sqrMagnitude < 0.25f || (o.Ready && o.Inside(at))) { h = o; break; }
            if (h == null) { if (All.Count > 40) All.RemoveAt(0); h = new NpcHouse(at); All.Add(h); }
            if (!h.Making && Time.time - h._madeAt > again && Plugin.Instance != null) { h.Making = true; Plugin.Instance.StartCoroutine(h.Make()); }
            return h;
        }

        private int Index(Vector3 p)
        {
            int x = Mathf.RoundToInt((p.x - (At.x - Reach)) / Cell), y = Mathf.RoundToInt((p.z - (At.z - Reach)) / Cell);
            return x >= 0 && y >= 0 && x < _w && y < _w ? x + y * _w : -1;
        }

        private Vector3 CellAt(int i, float y) { return new Vector3(At.x - Reach + (i % _w) * Cell, y, At.z - Reach + (i / _w) * Cell); }

        // the floor it stands on (the one under p, up to a storey's height above it)
        private Level LevelOf(Vector3 p)
        {
            Level best = null;
            foreach (Level l in _levels) if (p.y > l.Floor - 1f && p.y < l.Floor + Storey + 2.5f && (best == null || l.Floor > best.Floor)) best = l;
            return best;
        }

        // in it: on one of its floors (or up its walls: a torch hangs higher than the floor)
        internal bool Inside(Vector3 p)
        {
            if (!Ready) return false;
            Level l = LevelOf(p);
            int i = Index(p);
            return l != null && i >= 0 && l.In[i];
        }

        // in it or on its edge (a sconce on its wall, its door)
        internal bool Near(Vector3 p)
        {
            if (Inside(p)) return true;
            Level l = Ready ? LevelOf(p) : null;
            if (l == null) return false;
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { int i = Index(p + new Vector3(dx * Cell, 0f, dz * Cell)); if (i >= 0 && l.In[i]) return true; }
            return false;
        }

        // part of the house itself (what a servant mends): over any of its floors, two steps round it too (its walls, the
        // eaves, a porch roof), from under its lowest floor up to the ridge
        internal bool Covers(Vector3 p)
        {
            if (!Ready || _levels.Count == 0) return false;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Level l in _levels) { lo = Mathf.Min(lo, l.Floor); hi = Mathf.Max(hi, l.Floor); }
            if (p.y < lo - 1.5f || p.y > hi + Storey + 8f) return false;
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++)
            {
                int i = Index(p + new Vector3(dx * Cell, 0f, dz * Cell));
                if (i < 0) continue;
                foreach (Level l in _levels) if (l.In[i]) return true;
            }
            return false;
        }

        // a spot on one of its floors (a servant's idle round)
        internal Vector3 Spot()
        {
            if (!Ready || Count == 0) return At;
            List<Vector3> cells = new List<Vector3>();
            foreach (Level l in _levels) for (int i = 0; i < l.In.Length; i++) if (l.In[i]) cells.Add(CellAt(i, l.Floor));
            return cells[Random.Range(0, cells.Count)];
        }

        // what a player should know of it, in a word (the menu): nothing when all is well
        internal string Trouble()
        {
            if (!Ready) return "";
            if (TooBig || Count < 4) return " " + Lang.T("Тут дому нема: потрібна підлога під дахом — обери інший.");
            return Open ? " " + Lang.T("Дім не замкнений — він доглядає лише те, що під дахом.") : "";
        }

        internal string Describe()
        {
            if (!Ready) return Making ? Lang.K("дім роздивляється…") : Lang.K("дім ще не роздивлений");
            if (TooBig) return Lang.K("це не дім — завеликий дах (подвір'я?)");
            if (Count < 4) return Lang.K("тут нема даху — це не дім");
            return "дім: " + Count + " м²" + (_levels.Count > 1 ? ", поверхів " + _levels.Count : "") + (Open ? " — не замкнений (прохід ширший за 2 м): межі за дахом, можуть бути неточні" : "");
        }

        internal bool Good { get { return Ready && !TooBig && Count >= 4; } }

        // ---------------------------------------------------------------- making it (over frames)
        private IEnumerator Make()
        {
            if (_floorMask == 0) { _floorMask = LayerMask.GetMask("piece", "terrain", "Default", "static_solid"); _roofMask = LayerMask.GetMask("piece"); _wallMask = LayerMask.GetMask("piece"); }
            RaycastHit hit;
            Floor = Physics.Raycast(At + Vector3.up * 1.5f, Vector3.down, out hit, 4f, _floorMask) ? hit.point.y : At.y;
            List<Level> levels = new List<Level>();
            int start = Index(At);
            bool big = false;
            // the floor pointed at: its walls, the open air round it, the closed part where it was pointed
            Level first = new Level { Floor = Floor };
            sbyte[] kinds = new sbyte[_w * _w];
            bool[] sky = new bool[_w * _w];
            IEnumerator walls = Walls(Floor, kinds, sky);
            while (walls.MoveNext()) yield return null;
            bool[] air = new bool[_w * _w];
            IEnumerator pour = Air(kinds, sky, air);
            while (pour.MoveNext()) yield return null;
            if (start >= 0 && kinds[start] == 1) start = Free(start, kinds); // (pointed at a wall: the floor beside it)
            bool open = start < 0 || air[start];
            if (start >= 0)
            {
                List<int> seed = new List<int> { start };
                IEnumerator spread = open ? Spread(first, seed) : Spread(first, seed, kinds, air, null);
                while (spread.MoveNext()) yield return null;
            }
            if (first.Count > Most) big = true;
            if (first.Count > 0) levels.Add(first);
            // its other floors: over and under the first floor's cells (the floors built at other heights)
            if (!big && first.Count > 0)
            {
                Dictionary<int, List<int>> heights = new Dictionary<int, List<int>>(); // (a floor's height in half metres → its cells)
                int done = 0;
                for (int c = 0; c < first.In.Length; c++)
                {
                    if (!first.In[c]) continue;
                    Vector3 p = CellAt(c, Floor);
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, Floor + Up, p.z), Vector3.down, Up + Down, _roofMask))
                    {
                        if (h.normal.y < 0.7f || Mathf.Abs(h.point.y - Floor) < Storey) continue; // (a floor: lying flat, a storey away)
                        int key = Mathf.RoundToInt(h.point.y * 2f);
                        List<int> cells;
                        if (!heights.TryGetValue(key, out cells)) heights[key] = cells = new List<int>();
                        cells.Add(c);
                    }
                    if (++done % 60 == 0) yield return null;
                }
                List<int> keys = new List<int>(heights.Keys);
                keys.Sort((x, y) => heights[y].Count.CompareTo(heights[x].Count)); // (the biggest floors first)
                foreach (int key in keys)
                {
                    if (levels.Count >= Levels) break;
                    if (heights[key].Count < 4) continue; // (a shelf, a table top: no floor)
                    float y = key / 2f;
                    bool near = false;
                    foreach (Level l in levels) if (Mathf.Abs(l.Floor - y) < Storey) near = true; // (the same storey, a step up or down)
                    if (near) continue;
                    Level l2 = new Level { Floor = y };
                    IEnumerator spread = Spread(l2, heights[key], null, null, Round(first.In, 2));
                    while (spread.MoveNext()) yield return null;
                    if (l2.Count > Most) { big = true; break; }
                    if (l2.Count >= 4) levels.Add(l2);
                }
            }
            _levels.Clear(); _levels.AddRange(levels); Open = open;
            int count = 0; foreach (Level l in levels) count += l.Count;
            Count = count; TooBig = big; Ready = true; Making = false; _madeAt = Time.time;
            Npcs.Log("house at " + Mathf.RoundToInt(At.x) + "," + Mathf.RoundToInt(At.z) + ": " + Describe());
            if (_showWhenReady) { _showWhenReady = false; Show(20f); }
        }

        // a floor spread out from its first cells: a floor under it near its height, a roof over it, no wall between
        private IEnumerator Spread(Level l, List<int> seeds) { return Spread(l, seeds, null, null, null); }

        // kinds/air: the closed place's map (a wall stops, a door is a way, the open air is not the house's);
        // within: only these cells (an upper floor: over the first one and round it)
        private IEnumerator Spread(Level l, List<int> seeds, sbyte[] kinds, bool[] air, bool[] within)
        {
            l.In = new bool[_w * _w];
            bool[] seen = new bool[_w * _w];
            Queue<int> q = new Queue<int>();
            foreach (int s in seeds) if (!seen[s]) { seen[s] = true; q.Enqueue(s); }
            RaycastHit hit;
            int done = 0;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                Vector3 p = CellAt(c, l.Floor);
                bool floor = Physics.Raycast(new Vector3(p.x, l.Floor + 1.5f, p.z), Vector3.down, out hit, 1.5f + Low, _floorMask) && Mathf.Abs(hit.point.y - l.Floor) < Low;
                bool roof = floor && Physics.Raycast(new Vector3(p.x, l.Floor + 0.6f, p.z), Vector3.up, Above, _roofMask);
                if (floor && roof && (within == null || within[c]))
                {
                    l.In[c] = true;
                    if (++l.Count > Most) yield break;
                    int x = c % _w, y = c / _w;
                    Try(l, c, x + 1, y, seen, q, kinds, air); Try(l, c, x - 1, y, seen, q, kinds, air); Try(l, c, x, y + 1, seen, q, kinds, air); Try(l, c, x, y - 1, seen, q, kinds, air);
                }
                if (++done % 120 == 0) yield return null; // (a little each frame)
            }
        }

        // the next cell, unless a wall stands between (a door is a way within the house)
        private void Try(Level l, int from, int x, int y, bool[] seen, Queue<int> q, sbyte[] kinds, bool[] air)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _w) return;
            int to = x + y * _w;
            if (seen[to]) return;
            if (kinds != null) // (the closed place's map: a wall stops, the open air is out, a door or a free cell is a way)
            {
                if (kinds[to] == 1 || air[to]) return;
                seen[to] = true; q.Enqueue(to);
                return;
            }
            Vector3 a = CellAt(from, l.Floor) + Vector3.up * 1.6f, b = CellAt(to, l.Floor) + Vector3.up * 1.6f; // (above the furniture: only walls part the rooms)
            RaycastHit hit;
            if (Physics.Linecast(a, b, out hit, _wallMask) && hit.collider.GetComponentInParent<Door>() == null) return; // (a wall: not this way — another room may still reach it)
            seen[to] = true;
            q.Enqueue(to);
        }

        // the walls of a floor, cell by cell at 1.6 m (above the furniture): 1 a wall (or a cliff it leans on), 2 a door
        private static readonly Collider[] Hits = new Collider[16];
        private static int _terrainMask;
        private IEnumerator Walls(float floor, sbyte[] kinds, bool[] sky)
        {
            if (_terrainMask == 0) _terrainMask = LayerMask.GetMask("terrain");
            Vector3 half = new Vector3(Cell * 0.45f, 0.35f, Cell * 0.45f);
            RaycastHit hit;
            for (int c = 0; c < kinds.Length; c++)
            {
                Vector3 p = CellAt(c, floor) + Vector3.up * 1.6f;
                int n = Physics.OverlapBoxNonAlloc(p, half, Hits, Quaternion.identity, _wallMask, QueryTriggerInteraction.Ignore);
                sbyte k = 0;
                for (int i = 0; i < n; i++) { if (Hits[i] == null) continue; if (Hits[i].GetComponentInParent<Door>() != null) { k = 2; break; } k = 1; }
                if (k == 0 && Physics.Raycast(new Vector3(p.x, floor + 4f, p.z), Vector3.down, out hit, 4f, _terrainMask) && hit.point.y > floor + 1.4f) k = 1; // (the ground higher than a man: a cliff, a dug bank)
                kinds[c] = k;
                sky[c] = k == 0 && !Physics.Raycast(new Vector3(p.x, floor + 0.6f, p.z), Vector3.up, Above, _roofMask); // (the open sky over it: outside)
                if (c % 150 == 149) yield return null; // (a little each frame)
            }
        }

        // the open air: poured in from the edge of the square round it, through no wall and no gap under 2 m (every wall and
        // door grown by a cell, for this look only) — what it reaches is outside
        private IEnumerator Air(sbyte[] kinds, bool[] sky, bool[] air)
        {
            bool[] shut = new bool[kinds.Length];
            for (int c = 0; c < kinds.Length; c++)
            {
                if (kinds[c] == 0) continue;
                int x = c % _w, y = c / _w;
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) { int nx = x + dx, ny = y + dy; if (nx >= 0 && ny >= 0 && nx < _w && ny < _w) shut[nx + ny * _w] = true; }
            }
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < _w; i++) foreach (int c in new[] { i, i + (_w - 1) * _w, i * _w, i * _w + _w - 1 }) if (!shut[c] && !air[c]) { air[c] = true; q.Enqueue(c); }
            for (int c = 0; c < sky.Length; c++) if (sky[c] && !shut[c] && !air[c] && OpenAround(sky, c)) { air[c] = true; q.Enqueue(c); } // (under the open sky: a yard, fenced or not — not a smoke hole in the roof)
            int done = 0;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int x = c % _w, y = c / _w;
                foreach (int n in new[] { x + 1 < _w ? c + 1 : -1, x > 0 ? c - 1 : -1, y + 1 < _w ? c + _w : -1, y > 0 ? c - _w : -1 })
                    if (n >= 0 && !shut[n] && !air[n]) { air[n] = true; q.Enqueue(n); }
                if (++done % 400 == 0) yield return null;
            }
        }

        // a real open place, not a hole in the roof: most of the cells round it under the sky too (a smoke hole up to 2×2 m is not)
        private bool OpenAround(bool[] sky, int c)
        {
            int x = c % _w, y = c / _w, n = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) { int nx = x + dx, ny = y + dy; if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < _w && ny < _w && sky[nx + ny * _w]) n++; }
            return n >= 5;
        }

        // a free cell beside a wall (pointed at the wall itself)
        private int Free(int c, sbyte[] kinds)
        {
            int x = c % _w, y = c / _w;
            for (int r = 1; r <= 2; r++)
                for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < _w && ny < _w && kinds[nx + ny * _w] != 1) return nx + ny * _w;
                }
            return c;
        }

        // these cells and r round them (an upper floor's room to be in: over the first, a little round it)
        private bool[] Round(bool[] cells, int r)
        {
            bool[] o = new bool[cells.Length];
            for (int c = 0; c < cells.Length; c++)
            {
                if (!cells[c]) continue;
                int x = c % _w, y = c / _w;
                for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) { int nx = x + dx, ny = y + dy; if (nx >= 0 && ny >= 0 && nx < _w && ny < _w) o[nx + ny * _w] = true; }
            }
            return o;
        }

        // ---------------------------------------------------------------- shown (the menu: which house it is) — teal sparks round its floors
        private GameObject _shown;
        private static Material _spark;

        internal void ShowSoon() { if (Ready && !Making) Show(20f); else _showWhenReady = true; }

        internal void Show(float seconds)
        {
            if (!Ready || Plugin.Instance == null) return;
            if (_shown != null) Object.Destroy(_shown);
            _shown = new GameObject("RavenWatch_HouseEdge");
            if (_spark == null) { Shader sh = Shader.Find("Sprites/Default"); if (sh != null) { _spark = new Material(sh); _spark.color = new Color(0.55f, 0.95f, 0.85f, 0.9f); } }
            int dots = 0;
            foreach (Level l in _levels)
                for (int c = 0; c < l.In.Length && dots < 400; c++)
                {
                    if (!l.In[c]) continue;
                    int x = c % _w, y = c / _w;
                    if (Own(l, x + 1, y) && Own(l, x - 1, y) && Own(l, x, y + 1) && Own(l, x, y - 1)) continue; // (its edge only)
                    GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(dot.GetComponent<Collider>());
                    dot.transform.SetParent(_shown.transform, false);
                    dot.transform.position = CellAt(c, l.Floor) + Vector3.up * 0.3f; dot.transform.localScale = Vector3.one * 0.22f;
                    Renderer rr = dot.GetComponent<Renderer>();
                    if (_spark != null) rr.sharedMaterial = _spark;
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    dots++;
                }
            Plugin.Instance.StartCoroutine(HideLater(_shown, seconds));
        }

        private bool Own(Level l, int x, int y) { return x >= 0 && y >= 0 && x < _w && y < _w && l.In[x + y * _w]; }
        private IEnumerator HideLater(GameObject g, float seconds) { yield return new WaitForSeconds(seconds); if (g != null) Object.Destroy(g); }
    }
}
#endif

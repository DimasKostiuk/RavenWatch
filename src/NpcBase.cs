#if RAVEN_ITEM && RAVEN_NPC
using StringComparison = System.StringComparison;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Where a resident lives (npc-plan.md, stage 1): its base, round its heart (For: a heart within a few metres of its home — a settler's home is its heart; else that place).
    //   A map of 1 m cells round that place (up to Reach). Walls, palisades, fences, doors and gates the players built are
    //   the edges — standing on the land or on the ground storey's floor (a house on stilts), never a thing to use (a bed, a chest, a
    //   workbench, a fire) nor the heart — and so is the land where nobody walks: a steep bank (a moat's sides, a cliff: more than
    //   Bank metres up or down to the next cell) and water — unless something flat is built over it up to a storey high (a bridge
    //   across the moat, a floor over a slope): that is a way through, as a gate is. A moat, a fence, or both: one edge. A «paint»
    //   is poured in from the rim of the map: what it cannot reach is ENCLOSED (a yard behind a fence and the houses in it — the bed
    //   in the far corner, the chests by the gate: all its own).
    //     settled in an enclosed place of at least Yard m²  → FENCED: the base is every enclosed cell
    //     settled in the open, or only inside one house      → OPEN: a circle of Open m and its own village — a strip of Strip m
    //                                                         round each piece, joined from its home (a gap of two strips: another's
    //                                                         house, not ours); never the water (the sea, a lake) round it
    // Cheap (the user 2026-11-08: «база готова — не треба перераховувати; збудував — один дешевий процес раз на хвилину»):
    //   made once, when first asked on this game, within a frame budget (Budget ms a frame); then never again by itself.
    //   A piece coming or going within it (Piece.Awake / OnDestroy: built, taken down, loaded — on every game) or the land changed
    //   (Heightmap.Poke: a hoe) only marks it changed, with the box round it (Touched). Asked again once nothing has changed for
    //   Settle s and at least Least s after the last one: an update — the land's heights read again from the heightmap (no ray),
    //   walls and banks only in the changed box, the fill and its things whole (those are cheap). In between it is a lookup in
    //   a ready map. What each step cost: NpcPerf «base map: …», and a line in the log each time. One map per place, shared by everyone
    //   living there.
    internal sealed class NpcBase
    {
        internal const float Reach = 50f, Open = 20f, Strip = 4f, Cell = 1f;
        private const int Yard = 200; // m²: smaller than this, an enclosed place is a house, not a fenced yard
        private const float Bank = 1.2f; // m of height from one cell to the next (≈50°): steeper is a bank nobody walks
        private const float Stuck = 30f, Least = 60f, Settle = 3f, FirstWait = 15f, Forgotten = 600f, Budget = 1.5f;
        internal const float TouchPad = 6f;
        // (Least: an update never sooner than this after the last — a player building changes it all the time; Settle: no change for this
        // long first (a zone loading in is a few thousand pieces in a few seconds); FirstWait: its first making waits no longer than this
        // for that; Forgotten: a base nobody asked about for this long is let go; Budget: ms of a frame; TouchPad: m round a piece that
        // came or went (a wall's half-length and a cell))

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
        private float _madeAt = -1000f, _startedAt, _askedAt, _bornAt = -1f;
        // kept between makings, so an update redoes only its changed box: the cells a wall stands on, the land's banks; the box changed
        // since (cells, inclusive; none: x1 < x0) and the one being made now (Refuse waits on both)
        private bool[] _pieceWall = new bool[0], _bank = new bool[0];
        private bool[] _floored = new bool[0]; // (land lower than the sea with something flat built over it: kept with the banks, an update rays only its box)
        private bool Floored(int c) { return c >= 0 && c < _floored.Length && _floored[c]; }
        private bool[] _near = new bool[0], _village = new bool[0]; // (kept for Explain only: what the last making saw)
        private bool _dirty;
        private int _dx0, _dy0, _dx1 = -1, _dy1 = -1;
        private float _touchedAt = -1000f, _dirtySince;
        private int _mx0, _my0, _mx1 = -1, _my1 = -1;

        // ---------------------------------------------------------------- what stands in it, by kind
        // Made with its map, every making: one look over its pieces for everyone who lives there — NpcWork's fires, smelters, chests…, NpcMeals' tables, NpcChores' wear, NpcWhims' tub —
        // instead of each of them looking over all of them every few seconds. A thing gone since (taken down, its zone let go — you went through a portal and came
        // back: the map stays, its things were all made anew) is taken out of the lists before anyone reads them (Live), so a list holds only what stands: the heart's
        // board read a fire that was gone and E on the heart did nothing till the map's next update (the user 2026-11-14). The new ones come with that update.
        // Wears is left as it is (thousands of pieces: its one reader, NpcChores, skips what is gone).
        internal static readonly System.Type[] Kinds = { typeof(Fireplace), typeof(Smelter), typeof(CookingStation), typeof(Beehive), typeof(Fermenter), typeof(Container), typeof(Bed), typeof(Chair), typeof(CraftingStation), typeof(ItemStand), typeof(Door) };
        // …and these all round it (within Reach, in it or not: a fire in the yard by the heart, a seat on the porch, a door in its wall — a
        // door stands on the edge, never inside): the people's seat, fire and door searches read them instead of going over every piece of
        // the world near (each took 6–7 ms on a big base, 2026-11-05: the load test). A chair: every seat of its piece (a bench has two)
        internal static readonly System.Type[] RoundKinds = { typeof(Fireplace), typeof(Chair), typeof(Door), typeof(Container) }; // (a chest: by the dining table, on its far side of the base's edge too)
        private Dictionary<System.Type, List<Component>> _round;
        internal List<Component> Round(System.Type t) { Live(); List<Component> l; return _round != null && _round.TryGetValue(t, out l) ? l : null; }

        // a piece of it went (Went: Piece.OnDestroy) or its lists were just made (a piece may have gone while they were): what is gone leaves them, once, at the
        // first reading in a later frame (a thing taken down is gone only when its frame ends). A few hundred things looked at, only after something went
        private bool _gone;
        private int _goneFrame;
        private static readonly System.Predicate<Component> IsGone = c => c == null;
        private static readonly int PerfLive = NpcPerf.Id("base things: gone ones out (зниклі зі списків)");
        private void Live()
        {
            if (!_gone || Time.frameCount <= _goneFrame) return;
            _gone = false;
            long perf = NpcPerf.Now();
            int had = 0, left = 0;
            if (_things != null) foreach (List<Component> l in _things.Values) { had += l.Count; l.RemoveAll(IsGone); left += l.Count; }
            if (_round != null) foreach (List<Component> l in _round.Values) { had += l.Count; l.RemoveAll(IsGone); left += l.Count; }
            if (_tables != null) _tables.RemoveAll(IsGone);
            if (_tubs != null) _tubs.RemoveAll(IsGone);
            NpcPerf.Add(PerfLive, perf);
            if (had > 0 && left == 0 && Ready) LetGo();
        }
        private void Went() { _gone = true; _goneFrame = Time.frameCount; }
        internal float MadeAt { get { return _madeAt; } } // (when its lists were last made: what a reader keeps of them from before that is old — NpcPlaces.Roofed)

        // everything it knew went at once: its zone was let go (you left by a portal and came back before the map was forgotten). What stands there now is not
        // known — and «not known» is the word every reader has already: a base not made yet (Ready false: «ще рахуються», no «нема що їсти», no «вогнища: нема»,
        // which an empty list of a ready base said for the seconds till its update — the review 2026-11-14). Made whole again as a base first asked: once its
        // zone has come in (Fresh: settled, FirstWait at most, counted from now). A making under way is of what is gone: it writes nothing (_gen)
        private void LetGo()
        {
            Ready = false; Making = false; _gen++; _bornAt = Time.time;
            Npcs.Log("base at " + Mathf.RoundToInt(Centre.x) + "," + Mathf.RoundToInt(Centre.z) + ": all its things went at once (its zone let go) — made whole again when its zone is in");
        }

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
        private static readonly int PerfThings = NpcPerf.Id("base things (знімок бази)"), PerfHeights = NpcPerf.Id("base map: heights (висоти)"),
            PerfWalls = NpcPerf.Id("base map: walls (стіни)"), PerfBanks = NpcPerf.Id("base map: banks (схили)"), PerfFill = NpcPerf.Id("base map: fill (заливка)");

        // ---------------------------------------------------------------- what a kind of piece is, once a kind (by its prefab)
        // which of Kinds it has, a table to eat at, its own water, it wears: the same for every piece of that prefab, so asked of the first
        // only (a base of four thousand pieces asked eleven component searches of each, every making)
        private sealed class PieceKind { internal int Mask; internal bool Table, Wet, Wears, Heart; }
        private static readonly Dictionary<int, PieceKind> PieceKinds = new Dictionary<int, PieceKind>();
        private static PieceKind KindOf(Piece pc)
        {
            ZNetView nv = pc.GetComponent<ZNetView>();
            int key = nv != null && nv.IsValid() ? nv.GetZDO().GetPrefab() : Utils.GetPrefabName(pc.gameObject).GetStableHashCode();
            PieceKind k;
            if (PieceKinds.TryGetValue(key, out k)) return k;
            k = new PieceKind();
            for (int i = 0; i < Kinds.Length; i++) if (pc.GetComponentInChildren(Kinds[i]) != null) k.Mask |= 1 << i;
            k.Table = Utils.GetPrefabName(pc.gameObject).ToLowerInvariant().Contains("table") && pc.GetComponentInChildren<CraftingStation>() == null && pc.GetComponentInChildren<MapTable>() == null; // (piece_table, _round, _oak — and other mods' tables; not a workbench, a prep table, a map table)
            k.Wet = pc.GetComponentInChildren<WaterVolume>() != null;
            k.Wears = pc.GetComponent<WearNTear>() != null;
            k.Heart = pc.GetComponentInChildren<NpcHeart>() != null;
            PieceKinds[key] = k;
            return k;
        }
        private static readonly int DoorBit = 1 << System.Array.IndexOf(Kinds, typeof(Door));

        // its things of a kind (one of Kinds), null — not made yet or not a kind kept
        internal List<Component> Things(System.Type t) { Live(); List<Component> l; return _things != null && _things.TryGetValue(t, out l) ? l : null; }
        internal List<Transform> Tables { get { Live(); return _tables; } }   // the tables to eat at (not a workbench, a map table)
        internal List<WearNTear> Wears { get { return _wears; } }     // every piece that wears (round it, as NpcChores always counted); what is gone stays in it as a null
        internal List<Piece> Tubs { get { Live(); return _tubs; } }           // pieces with water of their own (a hot tub, a mod's pool)
        private static object _world;   // the world these maps are of (another world: all forgotten)
        private static float _nextPrune;

        private NpcBase(Vector3 centre)
        {
            Centre = centre;
            _w = Mathf.RoundToInt(Reach * 2f / Cell) + 1;
        }

        // the base round this place (made when first asked; kept fresh while asked). A place within HeartSnap of a heart is that heart's base, centred on it:
        // one map for all who live there and the heart's board (2026-11-14: a person put by the heart a metre off — the story's Кетіль — made a map of its own,
        // the bed checked on one, the edges shown of the other). A settler's home IS its heart (NpcResident.Settle, Rehome); only a few metres, so the home of
        // a story person farther off (its own place by a player's heart) stays its own place's base. The heart not loaded on this game: the place itself
        private const float HeartSnap = 3f;
        internal static NpcBase For(Vector3 home)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { All.Clear(); _world = ZNet.instance; } // (a world left and another loaded)
            if (Time.time >= _nextPrune) Prune();
            NpcHeart heart = NpcHeart.Near(home, HeartSnap); // (the loaded hearts: a few)
            if (heart != null) home = heart.transform.position;
            long key = ((long)Mathf.RoundToInt(home.x) << 32) ^ (uint)Mathf.RoundToInt(home.z);
            NpcBase b;
            if (!All.TryGetValue(key, out b)) { b = new NpcBase(home); All[key] = b; }
            b._askedAt = Time.time;
            b.Fresh();
            return b;
        }

        // the maps nobody asked about for a while go (each some 160 KB: its arrays of 101² cells, counted by their sizes)
        private static void Prune()
        {
            _nextPrune = Time.time + 60f;
            List<long> old = null;
            foreach (KeyValuePair<long, NpcBase> kv in All)
                if (!kv.Value.Making && Time.time - kv.Value._askedAt > Forgotten) { if (old == null) old = new List<long>(); old.Add(kv.Key); }
            if (old != null) foreach (long k in old) { All[k].Hide(); All.Remove(k); }
        }

        // asked: made the first time, updated when it changed — never by a clock alone. A few comparisons: it is asked often
        private void Fresh()
        {
            Live(); // (what went is out before Ready is read: a base whose all went says «not made» to whoever asks next, not after its first list is read)
            if (_bornAt < 0f) _bornAt = Time.time;
            if (Plugin.Instance == null) return;
            if (Making)
            {
                if (Time.time - _startedAt < Stuck) return;
                Npcs.Log("base at " + Mathf.RoundToInt(Centre.x) + "," + Mathf.RoundToInt(Centre.z) + ": its making broke off, made whole again");
                Begin(true); // (a making that broke off: whole again — the old one, if it still runs, writes nothing: _gen)
                return;
            }
            bool settled = Time.time - _touchedAt >= Settle;
            if (!Ready)
            {
                if (!settled && Time.time - _bornAt < FirstWait) return; // (its zone still loading in: the whole of it first)
                Begin(true);
                return;
            }
            if (_lookVersion != LocState.LookVersion && !_dirty) { _dirty = true; _dirtySince = Time.time; } // (a place's state changed: what it has now is another list — its things only, no box)
            if (!_dirty || Time.time - _madeAt < Least || (!settled && Time.time - _dirtySince < Least)) return; // (building on and on: once a minute all the same)
            Begin(false);
        }

        private int _gen;
        private void Begin(bool full)
        {
            Making = true; _startedAt = Time.time; _gen++;
            int x0 = _dx0, y0 = _dy0, x1 = _dx1, y1 = _dy1;
            bool box = _dirty && _dx1 >= _dx0;
            bool whole = full || !Ready || _pieceWall.Length == 0;
            if (whole || !box) { _mx0 = _my0 = 0; _mx1 = _my1 = _w - 1; } else { _mx0 = x0; _my0 = y0; _mx1 = x1; _my1 = y1; }
            _dirty = false; _dx0 = _dy0 = 0; _dx1 = _dy1 = -1; // (what comes while it is made marks it again)
            Plugin.Instance.StartCoroutine(Make(whole, box ? new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : new RectInt(0, 0, 0, 0)));
        }

        // a piece came or went, or the land changed, here (every game: Piece.Awake / OnDestroy, Heightmap.Poke): each base it touches is marked
        // changed with that box. Called in bursts (a zone loading in): a look at the few bases there are, nothing else
        internal static void Touched(Vector3 at, float r, bool went = false)
        {
            if (All.Count == 0 || !ReferenceEquals(_world, ZNet.instance)) return;
            foreach (NpcBase b in All.Values)
            {
                float dx = at.x - b.Centre.x, dz = at.z - b.Centre.z, far = Reach + r;
                if (dx * dx + dz * dz > far * far) continue;
                b.Mark(at, r);
                if (went) b.Went(); // (its lists may hold it: Live)
            }
        }

        // the land changed here (a hoe): marked changed, no box — an update finds the cells whose height moved by itself
        internal static void LandTouched(Vector3 at, float r)
        {
            if (All.Count == 0 || !ReferenceEquals(_world, ZNet.instance)) return;
            foreach (NpcBase b in All.Values)
            {
                float dx = at.x - b.Centre.x, dz = at.z - b.Centre.z, far = Reach + r;
                if (dx * dx + dz * dz > far * far) continue;
                if (!b._dirty) { b._dirty = true; b._dirtySince = Time.time; }
                b._touchedAt = Time.time;
            }
        }

        private void Mark(Vector3 at, float r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - r - Centre.x + Reach) / Cell)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt((at.x + r - Centre.x + Reach) / Cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((at.z - r - Centre.z + Reach) / Cell)), y1 = Mathf.Min(_w - 1, Mathf.CeilToInt((at.z + r - Centre.z + Reach) / Cell));
            _touchedAt = Time.time;
            if (x0 > x1 || y0 > y1) return;
            if (!_dirty) _dirtySince = Time.time;
            if (!_dirty || _dx1 < _dx0) { _dx0 = x0; _dy0 = y0; _dx1 = x1; _dy1 = y1; }
            else { _dx0 = Mathf.Min(_dx0, x0); _dy0 = Mathf.Min(_dy0, y0); _dx1 = Mathf.Max(_dx1, x1); _dy1 = Mathf.Max(_dy1, y1); }
            _dirty = true;
        }

        // ---------------------------------------------------------------- one spot, in full (the console's «raven npc base <who> at»: why a bed there is in or out)
        // its cell's every word on the last map, the nearest cell of its own village and how far, and the pieces whose middle stands within Strip + 1 m of it
        // (a village is joined only through those). Asked by hand: one search round the spot, one pass over the map
        internal string Explain(Vector3 p)
        {
            if (!Ready) return Making ? "база рахується…" : "база ще не порахована";
            int i = Index(p);
            float d = Utils.DistanceXZ(p, Centre);
            if (i < 0) return "поза мапою бази (" + Mathf.RoundToInt(d) + " м від центру, мапа ±" + Mathf.RoundToInt(Reach) + " м)";
            int home = Index(Centre);
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            s.Append("центр ").Append(Mathf.RoundToInt(Centre.x)).Append(',').Append(Mathf.RoundToInt(Centre.z)).Append("; точка ").Append(d.ToString("0.0")).Append(" м від нього");
            s.Append("; клітинка: ").Append(_in[i] ? "у базі" : "не в базі").Append(", дає річ: ").Append(_holds[i] ? "так" : "ні");
            s.Append(", біля будівлі (").Append(Mathf.RoundToInt(Strip)).Append(" м): ").Append(_near.Length > i && _near[i] ? "так" : "ні");
            s.Append(", своє селище: ").Append(_village.Length > i && _village[i] ? "так" : "ні");
            s.Append(", стіна: ").Append(_pieceWall[i] ? "так" : "ні").Append(", схил чи вода: ").Append(_bank[i] ? "так" : "ні");
            s.Append(", земля нижче моря: ").Append(_h[i] < WaterLevel - 0.3f ? "так" : "ні").Append(", підлога над нею: ").Append(_floored.Length > i && _floored[i] ? "так" : "ні");
            s.Append(", чому ні: ").Append(new[] { "—", "стіна", "схил", "вода", "відкрита база", "огорожа", "далеко" }[Mathf.Clamp(_why.Length > i ? _why[i] : 0, 0, 6)]);
            int vil = 0, best = -1; float bd = float.MaxValue;
            for (int c = 0; c < _village.Length; c++)
            {
                if (!_village[c]) continue;
                vil++;
                float e = Utils.DistanceXZ(CellCentre(c), p);
                if (e < bd) { bd = e; best = c; }
            }
            s.Append("\nселище від центру: ").Append(home >= 0 && _near.Length > home && _near[home] ? "почалось" : "НЕ почалось (у центрі нема будівлі ближче " + Mathf.RoundToInt(Strip) + " м)").Append(", ").Append(vil).Append(" м²");
            if (best >= 0 && !(_village[i])) { Vector3 b = CellCentre(best); s.Append("; найближча його клітинка за ").Append(bd.ToString("0.0")).Append(" м (").Append(Mathf.RoundToInt(b.x)).Append(',').Append(Mathf.RoundToInt(b.z)).Append(')'); }
            List<Piece> round = new List<Piece>();
            Piece.GetAllPiecesInRadius(p, Strip + 1f, round); // heavy: by hand, from the console only
            round.Sort((x, y) => Utils.DistanceXZ(x.transform.position, p).CompareTo(Utils.DistanceXZ(y.transform.position, p)));
            s.Append("\nбудівлі, чия середина ближче ").Append(Mathf.RoundToInt(Strip + 1f)).Append(" м (3D, як рахує гра): ").Append(round.Count);
            for (int k = 0; k < round.Count && k < 8; k++) { Transform t = round[k].transform; s.Append(k == 0 ? ": " : ", ").Append(Utils.GetPrefabName(round[k].gameObject)).Append(' ').Append(Utils.DistanceXZ(t.position, p).ToString("0.0")).Append(" м, висота ").Append((t.position.y - Centre.y).ToString("+0.0;-0.0")); }
            return s.ToString();
        }

        // ---------------------------------------------------------------- inside / a spot in it
        // a THING is of the base: on its own cell, on one beside it (a bed against a wall stands on the wall's cells), or, a base with no fence, on a wall of its own village
        // (a narrow room or a loft is all walls on this map of 1 m cells: «there is a wall or a door» of a bed in the owner's own house, 2026-11-09). Who walks where asks Inside
        private bool[] _holds = new bool[0];
        internal bool Holds(Vector3 p)
        {
            if (!Ready) return Utils.DistanceXZ(p, Centre) <= Open;
            int i = Index(p);
            return i >= 0 && i < _holds.Length && _holds[i];
        }

        internal bool Inside(Vector3 p)
        {
            if (!Ready) return Utils.DistanceXZ(p, Centre) <= Open;
            int i = Index(p);
            return i >= 0 && _in[i];
        }

        // A player gives it a thing of its base (a bed, a storeroom, a post: NpcResident.GiveBed, NpcWork): null — it is in it; else why not, in
        // words. The one answer for all of them. Its map not made on this game yet (the person run by a friend's game, or let go after
        // Forgotten s): «wait», never the circle's guess — a bed 25 m off in a house was «out of the base» while the map was being made
        // (the user 2026-11-08). The thing's own cell or one beside it: a bed against a wall stands half on the wall's cells
        private byte[] _why = new byte[0];
        private const byte WhyWall = 1, WhyBank = 2, WhyWater = 3, WhyOpen = 4, WhyFence = 5, WhyFar = 6;
        internal string Refuse(Vector3 p)
        {
            if (!Ready) return Lang.T("Межі ще рахуються — спробуй за мить.");
            int i = Index(p);
            if (i >= 0 && Making && In(i % _w, i / _w, _mx0, _my0, _mx1, _my1)) return Lang.T("Межі ще рахуються — спробуй за мить."); // (being made there now)
            if (_dirty && i >= 0 && (_dx1 < _dx0 || In(i % _w, i / _w, _dx0, _dy0, _dx1, _dy1))) { Hurry(); return Lang.T("Межі ще рахуються — спробуй за мить."); } // (just built there — a house and its bed: not the old map's word, its update now)
            if (Holds(p)) return null;
            byte w = i >= 0 && i < _why.Length ? _why[i] : WhyFar;
            if (w == WhyWall && !Fenced) w = WhyOpen; // (in an open base a wall is no boundary: it stood outside its village, and the wall on its cell was not why)
            string why = w == WhyWall ? Lang.T("там стіна чи двері")
                : w == WhyBank ? Lang.T("крутий схил, а над ним нема підлоги")
                : w == WhyWater ? Lang.T("там вода")
                : w == WhyFence ? Lang.T("поза огорожею двору")
                : w == WhyOpen ? Lang.F("база без огорожі: її дім — коло {0} м і {1} м довкола будівель її селища", Mathf.RoundToInt(Open), Mathf.RoundToInt(Strip))
                : Lang.F("далі {0} м від її дому", Mathf.RoundToInt(Reach));
            return Lang.F("Поза базою: {0} ({1} м від Серця оселі; межі — на дошці Серця, «Показати межі»).", why, Mathf.RoundToInt(Utils.DistanceXZ(p, Centre)));
        }

        // in a box of cells, or beside it (a thing against its edge)
        private static bool In(int x, int y, int x0, int y0, int x1, int y1) { return x >= x0 - 1 && x <= x1 + 1 && y >= y0 - 1 && y <= y1 + 1; }

        // a player waits for it: its update now, not after Least and Settle
        private void Hurry()
        {
            if (Making || Plugin.Instance == null) return;
            Begin(false);
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
            return StandOn(_cells);
        }

        // a spot on its outer edge, on the land (the guard's round); the centre while it has no map
        internal Vector3 EdgeSpot()
        {
            if (!Ready || _edge.Count == 0) return Spot();
            return StandOn(_edge);
        }

        // a cell of these to stand on: never one with a floor over the low shore (Ground is the land under it — the sea bed under the floor); none in a few
        // tries: the centre (its heart, on its own floor)
        private Vector3 StandOn(List<int> cells)
        {
            for (int i = 0; i < 8; i++) { int c = cells[Random.Range(0, cells.Count)]; if (!Floored(c)) return Ground(CellCentre(c)); }
            return Centre;
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
                int c = _cells[Random.Range(0, _cells.Count)];
                if (Floored(c)) continue; // (a floor over the low shore: not where one kneels to wash — its ground is the sea bed under it)
                Vector3 p = CellCentre(c);
                foreach (Vector3 d in dirs) { int w = Index(p + d * 2f); if (w >= 0 && _h[w] < WaterLevel - 0.3f && !Floored(w)) { at = Ground(p); water = p + d * 2f; return true; } } // (its own heights: a base far from any water asked 240 rays a try; water with a floor over it is no water to wash in)
            }
            return false;
        }

        internal string Describe()
        {
            if (!Ready) return Making ? "база рахується…" : "база ще не порахована";
            return (Fenced ? "огороджена" : "відкрита (коло " + Mathf.RoundToInt(Open) + " м + своє селище)") + ", " + _cells.Count + " м², межа " + _edge.Count + " м (від центру в середньому " + Mathf.RoundToInt(_edgeFar) + " м, найближче " + Mathf.RoundToInt(_edgeNear) + " м), будівель " + Pieces + ", клітинок стін і дверей " + Blockers + ", схилів і води " + Banks;
        }

        // ---------------------------------------------------------------- making the map
        // full: every cell (its first making, or one that broke off); else an update: the land's heights again (cheap), walls and banks only in
        // the changed box (and banks where the land moved), the fill and its things whole. Into new arrays: the old map answers till it is done
        private IEnumerator Make(bool full, RectInt box)
        {
            int n = _w * _w, gen = _gen;
            Pace pace = new Pace(Budget);
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : -1000f;
            // the land's height of every cell, straight from the heightmap (a cell is one of its vertices: 1 m); a ray only where none is loaded
            pace.Phase(0, PerfHeights);
            float[] old = _h, h = new float[n];
            Heightmap hm = null;
            for (int c = 0; c < n; c++)
            {
                Vector3 at = CellCentre(c);
                float y;
                if (hm == null || !hm.GetWorldHeight(at, out y)) { hm = Heightmap.FindHeightmap(at); if (hm == null || !hm.GetWorldHeight(at, out y)) y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : Centre.y; } // (the last one found first: a row of cells is mostly one heightmap)
                h[c] = y;
                if ((c & 255) == 255 && pace.Due) { pace.Yield(); yield return null; pace.Resumed(); }
            }
            bool again = !full && old.Length == n && _pieceWall.Length == n && _bank.Length == n && _floored.Length == n;
            if (!again) full = true;
            int cx0 = full ? 0 : box.xMin, cy0 = full ? 0 : box.yMin, cx1 = full ? _w - 1 : box.xMax - 1, cy1 = full ? _w - 1 : box.yMax - 1;
            // the walls the players built: every piece's strip; the cells walls stand on, made again in the box only
            pace.Phase(1, PerfWalls);
            List<Piece> pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(Centre, Reach, pieces);
            bool[] near = new bool[n];
            bool[] pw = full ? new bool[n] : (bool[])_pieceWall.Clone();
            if (!full) for (int y = cy0; y <= cy1; y++) for (int x = cx0; x <= cx1; x++) pw[x + y * _w] = false;
            int done = 0;
            foreach (Piece pc in pieces)
            {
                if (pc == null) continue;
                Stamp(near, pc.transform.position, Strip);
                if (cx1 >= cx0 && Reaches(pc.transform.position, cx0, cy0, cx1, cy1) && Edge(pc, h)) Rasterise(pw, pc, cx0, cy0, cx1, cy1);
                if ((++done & 15) == 0 && pace.Due) { pace.Yield(); yield return null; pace.Resumed(); }
            }
            // the land itself: steep banks and water are edges too, unless something built lies over them (a bridge, a floor). A ray only for a
            // bank in the box, where the land moved, or never looked at; the rest keep what they were
            pace.Phase(2, PerfBanks);
            bool[] bank = new bool[n], floored = new bool[n];
            for (int c = 0; c < n; c++)
            {
                int x = c % _w, y = c / _w;
                bool wet = h[c] < water - 0.3f;
                float top = Mathf.Max(h[c], Higher(x + 1, y, h), Higher(x - 1, y, h), Higher(x, y + 1, h), Higher(x, y - 1, h));
                if (!wet && top - h[c] <= Bank && h[c] - Lower(x, y, h) <= Bank) continue;
                if (pw[c] && !wet) continue; // (a wall on a slope: no ray — the cell is an edge either way)
                bool redo = full || (x >= cx0 && x <= cx1 && y >= cy0 && y <= cy1) || Mathf.Abs(old[c] - h[c]) > 0.05f;
                if (!redo) { bank[c] = !pw[c] && _bank[c]; floored[c] = wet && _floored[c]; continue; }
                bool built = Bridged(c, h[c], Mathf.Max(top, water));
                if (!pw[c]) bank[c] = !built;
                floored[c] = wet && built; // (land lower than the sea with something flat built over it within a storey: land — a wall's cell too, so a bed against the wall of a house over
                // the low shore is the base's, 2026-11-14. On a wall's cell the flat thing the ray meets may be the wall's own top: a sea wall of its village holds a thing too — harmless, said so)
                if (pace.Due) { pace.Yield(); yield return null; pace.Resumed(); }
            }
            // the paint from the rim inwards: what it cannot reach is enclosed
            pace.Phase(3, PerfFill);
            bool[] wall = new bool[n];
            byte[] why = new byte[n]; // (each cell not its own: why — a refusal says it: Refuse)
            int walls = 0, banks = 0;
            for (int c = 0; c < n; c++)
            {
                if (pw[c]) { wall[c] = true; why[c] = WhyWall; walls++; }
                else if (bank[c]) { wall[c] = true; why[c] = h[c] < water - 0.3f ? WhyWater : WhyBank; banks++; }
            }
            bool[] outside = new bool[n];
            Queue<int> q = new Queue<int>();
            float rim = Reach - Cell * 1.5f;
            for (int c = 0; c < n; c++)
                if (!wall[c] && Utils.DistanceXZ(CellCentre(c), Centre) >= rim) { outside[c] = true; q.Enqueue(c); }
            Flood(q, wall, outside);
            if (pace.Due) { pace.Yield(); yield return null; pace.Resumed(); }
            int home = Index(Centre);
            bool homeEnclosed = home >= 0 && !outside[home];
            int yard = 0;
            if (homeEnclosed) { bool[] mine = new bool[n]; Queue<int> hq = new Queue<int>(); mine[home] = true; hq.Enqueue(home); yard = Flood(hq, OrOf(wall, outside), mine); }
            bool fenced = homeEnclosed && yard >= Yard;
            // its own village: the strips round the pieces, joined from its home (a gap wider than two strips: another's house, not ours)
            bool[] village = new bool[n];
            if (home >= 0 && near[home])
            {
                Queue<int> vq = new Queue<int>(); village[home] = true; vq.Enqueue(home);
                while (vq.Count > 0)
                {
                    int c = vq.Dequeue(), x = c % _w, y = c / _w;
                    if (x + 1 < _w) Join(c + 1, near, village, vq);
                    if (x > 0) Join(c - 1, near, village, vq);
                    if (y + 1 < _w) Join(c + _w, near, village, vq);
                    if (y > 0) Join(c - _w, near, village, vq);
                }
            }
            bool[] inside = new bool[n];
            List<int> cells = new List<int>(); // (the old map stays whole till the new one takes its place, below)
            for (int c = 0; c < n; c++)
            {
                if (wall[c]) continue;
                float d = Utils.DistanceXZ(CellCentre(c), Centre);
                if (d > Reach) { why[c] = WhyFar; continue; }
                bool enclosed = !outside[c];
                bool own = fenced ? enclosed : (d <= Open || village[c]); // (a room of its own village is in its strips: its floor is a piece. Water nobody built over is
                // a bank before this — an edge, never here; what is left lower than the sea has a floor over it: land, a street or a house over the low shore — 2026-11-14: it was
                // left out of an open base by the land's height under it, and a bed in such a house was «out of the base»)
                inside[c] = own;
                if (own) cells.Add(c);
                else why[c] = fenced && !enclosed ? WhyFence : WhyOpen;
            }
            bool[] holds = new bool[n];
            for (int c = 0; c < n; c++)
            {
                int x = c % _w, y = c / _w;
                bool has = inside[c];
                for (int dy = -1; dy <= 1 && !has; dy++) for (int dx = -1; dx <= 1 && !has; dx++) { int nx = x + dx, ny = y + dy; has = nx >= 0 && ny >= 0 && nx < _w && ny < _w && inside[nx + ny * _w]; }
                holds[c] = has || (!fenced && village[c] && (h[c] >= water - 0.3f || floored[c]) && Utils.DistanceXZ(CellCentre(c), Centre) <= Reach); // (a wall's cell of its village: not in the sea — on land, or with a floor over it)
            }
            yield return MakeThings(pieces, holds, pace, gen);
            pace.Stop();
            if (gen != _gen) yield break; // (a newer making took its place)
            _in = inside; _holds = holds; _why = why; _cells.Clear(); _cells.AddRange(cells); _h = h; _pieceWall = pw; _bank = bank; _floored = floored; _near = near; _village = village;
            Fenced = fenced; Pieces = pieces.Count; Blockers = walls; Banks = banks;
            MakeEdge(inside);
            Ready = true; Making = false; _madeAt = Time.time;
            Npcs.Log("base at " + Mathf.RoundToInt(Centre.x) + "," + Mathf.RoundToInt(Centre.z) + ": " + (full ? "made whole" : "updated, box " + (cx1 - cx0 + 1) + "×" + (cy1 - cy0 + 1) + " m")
                + " in " + pace.Ms() + " ms over " + pace.Frames + " frames (heights " + pace.Ms(0) + ", walls " + pace.Ms(1) + ", banks " + pace.Ms(2) + ", fill " + pace.Ms(3) + ", things " + pace.Ms(4) + "): " + Describe());
        }

        private static void Join(int c, bool[] near, bool[] village, Queue<int> q) { if (near[c] && !village[c]) { village[c] = true; q.Enqueue(c); } }

        // a piece near enough to the box to stand on a cell of it (its middle within TouchPad of it)
        private bool Reaches(Vector3 p, int x0, int y0, int x1, int y1)
        {
            float x = (p.x - Centre.x + Reach) / Cell, y = (p.z - Centre.z + Reach) / Cell, pad = TouchPad / Cell;
            return x >= x0 - pad && x <= x1 + pad && y >= y0 - pad && y <= y1 + pad;
        }

        // a making's time: Budget ms of a frame at most, each step's own sum (NpcPerf and the log line)
        private sealed class Pace
        {
            private readonly long _budget;
            private long _frame, _from;
            private int _id = -1, _phase, _keepId = -1, _keepPhase;
            private readonly long[] _took = new long[5];
            internal int Frames = 1;
            internal Pace(float ms) { _budget = (long)(System.Diagnostics.Stopwatch.Frequency * ms / 1000.0); _frame = NpcPerf.Now(); }
            internal void Phase(int phase, int id) { Stop(); _phase = phase; _id = id; _from = NpcPerf.Now(); }
            internal void Stop() { if (_id < 0) return; _took[_phase] += NpcPerf.Now() - _from; NpcPerf.Add(_id, _from); _id = -1; }
            internal bool Due { get { return NpcPerf.Now() - _frame > _budget; } }
            internal void Yield() { _keepId = _id; _keepPhase = _phase; Stop(); Frames++; }
            internal void Resumed() { _frame = NpcPerf.Now(); if (_keepId >= 0) Phase(_keepPhase, _keepId); _keepId = -1; }
            internal string Ms(int phase) { return (_took[phase] * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
            internal string Ms() { long s = 0; foreach (long t in _took) s += t; return (s * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
        }

        private int _lookVersion = -1;
        private IEnumerator MakeThings(List<Piece> pieces, bool[] inside, Pace pace, int gen)
        {
            pace.Phase(4, PerfThings);
            int lookVersion = LocState.LookVersion;
            Dictionary<System.Type, List<Component>> things = new Dictionary<System.Type, List<Component>>();
            foreach (System.Type t in Kinds) things[t] = new List<Component>();
            Dictionary<System.Type, List<Component>> round = new Dictionary<System.Type, List<Component>>();
            foreach (System.Type t in RoundKinds) round[t] = new List<Component>();
            List<Transform> tables = new List<Transform>();
            List<WearNTear> wears = new List<WearNTear>();
            List<Piece> tubs = new List<Piece>();
            int done = 0;
            foreach (Piece pc in pieces)
            {
                if ((++done & 15) == 0 && pace.Due) { pace.Yield(); yield return null; pace.Resumed(); }
                if (pc == null) continue;
                // A place of the story (its pieces carry its stamp) is not a home's things: it belongs to the base that stands in it (its own people's, a
                // story person's: a settler's heart may stand just outside its no-build ring, and it is no one's bench, chest or thing to mend). What its
                // state has not is nobody's. The story's own spots go past this (people: StoryLife)
                ZNetView pnv = pc.GetComponent<ZNetView>();
                Loc place = Blueprint.Copy(pnv) ? Locs.Get(pnv.GetZDO().GetString(LocParts.KOf, "")) : null; // blueprint only: the place by its stamp, its state (an inside hangs far over any base)
                if (place != null && (!place.Holds(Centre) || LocState.Absent(pnv))) continue;
                PieceKind kind = KindOf(pc);
                if (kind.Wears && place == null) wears.Add(pc.GetComponent<WearNTear>()); // (mended whole by the story, never by a hammer: LocRepairPatch)
                for (int i = 0; kind.Mask != 0 && i < Kinds.Length; i++)
                {
                    if ((kind.Mask & (1 << i)) == 0) continue;
                    System.Type t = Kinds[i];
                    if (place != null && place.Keeps(t)) continue;
                    Component c = pc.GetComponentInChildren(t);
                    if (c == null) continue;
                    bool rounds = System.Array.IndexOf(RoundKinds, t) >= 0;
                    if (t == typeof(Chair)) { foreach (Chair ch in pc.GetComponentsInChildren<Chair>()) { if (!NpcMind.Seatable(ch)) continue; /* (a ship's, or a toppled bench: no seat) */ round[t].Add(ch); if (In(inside, ch.transform.position)) things[t].Add(ch); } continue; } // heavy: once a making, a piece with a seat only
                    if (rounds) round[t].Add(c);
                    if (In(inside, c.transform.position)) things[t].Add(c);
                }
                if ((kind.Table || kind.Wet) && In(inside, pc.transform.position))
                {
                    if (kind.Table) tables.Add(pc.transform);
                    if (kind.Wet) tubs.Add(pc);
                }
            }
            pace.Stop();
            if (gen != _gen) yield break; // (a newer making took its place, or its zone was let go meanwhile: these lists are of what is gone)
            _things = things; _round = round; _tables = tables; _wears = wears; _tubs = tubs; _lookVersion = lookVersion;
            Went(); // (made over several frames: a piece gone meanwhile is in the new lists)
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

        // something built over this cell (a bridge, a dock, a floor on stilts): a flat piece's top, up to a storey over the bank's top. A roof is
        // not flat, so one over it does not count (the ray only from just over the bank's top missed a floor higher than that: a house on a
        // slope was a bank under it, 2026-11-08)
        private static readonly int Decks = LayerMask.GetMask("piece");
        private const float Storey = 3.5f, FlatFloor = 0.9f;
        private static readonly RaycastHit[] Rays = new RaycastHit[8];
        private bool Bridged(int c, float ground, float top)
        {
            Vector3 mid = CellCentre(c);
            float from = top + Storey;
            int got = Physics.RaycastNonAlloc(new Vector3(mid.x, from, mid.z), Vector3.down, Rays, from - ground + 0.2f, Decks, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < got; i++) if (Rays[i].normal.y >= FlatFloor && Rays[i].point.y > ground + 0.4f) return true;
            return false;
        }

        // the floor a piece stands on: a flat piece's top just under it, no higher than a storey over the land (a house on stilts); else the land
        private float Floor(Bounds b, float ground)
        {
            if (b.min.y - ground > Storey + 0.5f) return ground; // (an upper storey: not the base's ground)
            float from = b.min.y + 0.3f;
            int got = Physics.RaycastNonAlloc(new Vector3(b.center.x, from, b.center.z), Vector3.down, Rays, from - ground + 0.2f, Decks, QueryTriggerInteraction.Ignore);
            float best = ground;
            for (int i = 0; i < got; i++) if (Rays[i].normal.y >= FlatFloor && Rays[i].point.y > best && Rays[i].point.y <= b.min.y + 0.3f) best = Rays[i].point.y;
            return best;
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
            PieceKind k = KindOf(pc);
            if (!k.Wears) return false;
            pc.GetComponentsInChildren(Cols); // heavy: once a making, a piece in the box only (Rasterise reads the same list after: a door too)
            if ((k.Mask & DoorBit) != 0) return true;
            if (k.Mask != 0 || k.Heart) return false; // (a thing to use — a bed, a chest, a workbench, a fire — is no wall: a bed with a tall head was one, its own cell out of the base, the user 2026-11-08; nor the heart: its home's own cell was on the edge)
            if (pc.m_name != null && pc.m_name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0) return false; // (stairs are walked on, not a wall)
            foreach (Collider c in Cols)
            {
                if (c == null || c.isTrigger) continue;
                Bounds b = c.bounds;
                if (b.size.y < 1f) continue;
                int at = Index(b.center);
                float ground = at >= 0 ? h[at] : b.min.y; // (the heights already found: no ray of its own)
                if (b.min.y <= ground + 1f || b.min.y <= Floor(b, ground) + 1f) return true; // (on the land, or on the ground storey's floor: a house on stilts)
            }
            return false;
        }

        // the cells it stands on: a cell-sized box at the cell, as tall as the piece, touching this collider (works for
        // every collider kind, a slanted wall marks only its own cells); only the cells of the box x0..x1, y0..y1 (an update: the changed box)
        private static readonly List<Collider> Cols = new List<Collider>(); // (the piece Edge just looked at: Rasterise is asked right after it)
        private void Rasterise(bool[] wall, Piece pc, int bx0, int by0, int bx1, int by1)
        {
            foreach (Collider c in Cols)
            {
                if (c == null || c.isTrigger) continue;
                Bounds b = c.bounds;
                if (b.size.y < 0.5f) continue;
                int mask = 1 << c.gameObject.layer;
                Vector3 half = new Vector3(Cell * 0.45f, Mathf.Max(0.25f, b.extents.y), Cell * 0.45f);
                int x0 = Mathf.FloorToInt((b.min.x - Centre.x + Reach) / Cell), x1 = Mathf.CeilToInt((b.max.x - Centre.x + Reach) / Cell);
                int y0 = Mathf.FloorToInt((b.min.z - Centre.z + Reach) / Cell), y1 = Mathf.CeilToInt((b.max.z - Centre.z + Reach) / Cell);
                for (int y = Mathf.Max(by0, y0); y <= Mathf.Min(by1, y1); y++)
                    for (int x = Mathf.Max(bx0, x0); x <= Mathf.Min(bx1, x1); x++)
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

        private static float WaterLevel { get { return ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : -1000f; } }

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

    // what marks a base changed (NpcBase.Touched, LandTouched): a piece coming in or going (built, taken down, loaded, let go — on every game),
    // the land's heights made again (a hoe). Only a look at the few bases there are: a zone loading in calls these a few thousand times
    [HarmonyLib.HarmonyPatch(typeof(Piece), "Awake")]
    internal static class NpcBasePieceIn
    {
        private static void Postfix(Piece __instance)
        {
            try
            {
                if (ZNetView.m_forceDisableInit) return; // (the hammer's ghost is made so: not a piece)
                NpcBase.Touched(__instance.transform.position, NpcBase.TouchPad); LocParts.PieceChanged(__instance.transform.position);
#if RAVEN_STORY
                LocInside.PieceIn(__instance); // (a station of the author's inside a dungeon of ours: dead as it comes in)
#endif
            }
            catch (System.Exception e) { PatchGuard.Fail("NpcBasePieceIn", e); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Piece), "OnDestroy")]
    internal static class NpcBasePieceOut
    {
        private static readonly int Ghost = LayerMask.NameToLayer("ghost");
        private static void Prefix(Piece __instance)
        {
            try { if (__instance.gameObject.layer != Ghost) { NpcBase.Touched(__instance.transform.position, NpcBase.TouchPad, true); LocParts.PieceChanged(__instance.transform.position); } } // (the hammer's ghost, let go)
            catch (System.Exception e) { PatchGuard.Fail("NpcBasePieceOut", e); }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Heightmap), "Poke")]
    internal static class NpcBaseLand
    {
        private static void Postfix(Heightmap __instance, bool paintOnly)
        {
            try { if (!paintOnly) NpcBase.LandTouched(__instance.transform.position, __instance.m_width * __instance.m_scale * 0.5f); }
            catch (System.Exception e) { PatchGuard.Fail("NpcBaseLand", e); }
        }
    }
}
#endif
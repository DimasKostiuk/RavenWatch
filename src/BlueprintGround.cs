#if RAVEN_ITEM && RAVEN_NPC
// (the ground under a blueprint: from 2.9, with Blueprint)
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HuginWhispers
{
    // The ground under a building put somewhere else (the user 2026-10-31: the Hall, ~30 m long, must stand right in any
    // world). Done the game's own way — straight into the terrain's height deltas, as the hoe's «level» does (TerrainComp,
    // the same ±8 m limit) — never by stacking a hundred hoe strokes:
    //   - save: how high the ground stood under the floor where it was built (the median under its footprint) → «ground y»;
    //   - check: the median ground under the footprint here is where its ground goes; more than MaxDev off anywhere
    //     inside, or that ground near the water, and the place is refused (force: the author's word);
    //   - level: the footprint flat at that height, its edge eased back into the land over Margin metres, the inside
    //     painted bare (no grass through the floor), trees/rocks/bushes on it taken away;
    //   - undo: the terrain as it was (the trees and rocks taken stay gone);
    //   - here: the author's square round him flat at his feet (raven blueprint ground <half m> | undo), the same way.
    internal static class BlueprintGround
    {
        internal const float NoGround = -0.15f; // (a blueprint saved before «ground»: just under the floor)
        internal const float MaxDev = 6f;       // (the game moves its ground at most 8 m from what the world made: room to spare)
        private const float Pad = 1f, Margin = 6f;

        private static readonly FieldInfo FLevel = typeof(TerrainComp).GetField("m_levelDelta", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FSmooth = typeof(TerrainComp).GetField("m_smoothDelta", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FModH = typeof(TerrainComp).GetField("m_modifiedHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FPaint = typeof(TerrainComp).GetField("m_paintMask", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FModP = typeof(TerrainComp).GetField("m_modifiedPaint", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FInit = typeof(TerrainComp).GetField("m_initialized", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo MSave = typeof(TerrainComp).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo FindObjects = HarmonyLib.AccessTools.Method(typeof(ZDOMan), "FindObjects");
        private static readonly MethodInfo MakeObject = HarmonyLib.AccessTools.Method(typeof(ZNetScene), "CreateObject");
        private static readonly int CompilerHash = "_TerrainCompiler".GetStableHashCode();

        // the last levelling, to put back on undo: per terrain piece its arrays as they were
        private sealed class Snap { internal TerrainComp Tc; internal float[] Level, Smooth; internal bool[] ModH, ModP; internal Color[] Paint; }
        private static readonly List<Snap> _undo = new List<Snap>();

        internal struct Fit
        {
            internal bool Ok, Seen; // (Seen: the ground here was read at all)
            internal float Ground, Dev; // (where the ground goes; how far the land strays from it inside the footprint)
            internal string Why;
        }

        // the footprint: the pieces' rectangle in the blueprint's own frame (x across, z along)
        internal struct Rect { internal float X0, X1, Z0, Z1; }

        internal static Rect Footprint(List<Blueprint.Part> parts)
        {
            Rect r = new Rect { X0 = float.MaxValue, X1 = float.MinValue, Z0 = float.MaxValue, Z1 = float.MinValue };
            foreach (Blueprint.Part pt in parts)
            {
                r.X0 = Mathf.Min(r.X0, pt.Pos.x); r.X1 = Mathf.Max(r.X1, pt.Pos.x);
                r.Z0 = Mathf.Min(r.Z0, pt.Pos.z); r.Z1 = Mathf.Max(r.Z1, pt.Pos.z);
            }
            r.X0 -= Pad; r.X1 += Pad; r.Z0 -= Pad; r.Z1 += Pad;
            return r;
        }

        // how far a point (blueprint frame) lies outside the rectangle; 0 inside
        internal static float Outside(Rect r, Vector3 lp)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.X0 - lp.x, lp.x - r.X1));
            float dz = Mathf.Max(0f, Mathf.Max(r.Z0 - lp.z, lp.z - r.Z1));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Reach(Rect r)
        {
            float w = Mathf.Max(Mathf.Abs(r.X0), Mathf.Abs(r.X1)), l = Mathf.Max(Mathf.Abs(r.Z0), Mathf.Abs(r.Z1));
            return Mathf.Sqrt(w * w + l * l) + Margin + 2f;
        }

        // every terrain vertex within reach: its heightmap, its index, where it is, and how far outside the footprint
        private delegate void Visit(Heightmap h, int x, int y, Vector3 world, float outside);

        private static int Walk(Rect r, Vector3 at, Quaternion rot, float reach, Visit v)
        {
            List<Heightmap> maps = new List<Heightmap>();
            Heightmap.FindHeightmap(at, reach, maps);
            Quaternion inv = Quaternion.Inverse(rot);
            int seen = 0;
            foreach (Heightmap h in maps)
            {
                if (h == null || h.IsDistantLod) continue;
                int half = h.m_width / 2;
                Vector3 o = h.transform.position;
                for (int y = 0; y <= h.m_width; y++)
                    for (int x = 0; x <= h.m_width; x++)
                    {
                        Vector3 wp = new Vector3(o.x + (x - half) * h.m_scale, o.y + h.GetHeight(x, y), o.z + (y - half) * h.m_scale);
                        Vector3 d = wp - at; d.y = 0f;
                        if (d.magnitude > reach) continue;
                        v(h, x, y, wp, Outside(r, inv * d));
                        seen++;
                    }
            }
            return seen;
        }

        // ---------------------------------------------------------------- save: the ground under the floor where it stood
        internal static float Measure(List<Blueprint.Part> parts, Vector3 c, Quaternion rot)
        {
            List<float> under = new List<float>();
            Walk(Footprint(parts), c, rot, Reach(Footprint(parts)), (h, x, y, wp, outside) => { if (outside <= 0f) under.Add(wp.y - c.y); });
            if (under.Count == 0) return NoGround;
            under.Sort();
            return under[under.Count / 2];
        }

        // ---------------------------------------------------------------- check: may it stand here, and how high
        internal static Fit Check(List<Blueprint.Part> parts, Vector3 at, Quaternion rot) { return Check(Footprint(parts), at, rot); }

        // the same for a ground given whole (a place of the story: its walls and the room round them — Loc.Room)
        internal static Fit Check(Rect r, Vector3 at, Quaternion rot)
        {
            Fit f = new Fit();
            BringIn(at, Reach(r));
            List<float> under = new List<float>();
            Walk(r, at, rot, Reach(r), (h, x, y, wp, outside) => { if (outside <= 0f) under.Add(wp.y); });
            if (under.Count < 4) { f.Why = "Землю тут ще не видно (підійди ближче до місця)."; return f; }
            under.Sort();
            f.Seen = true;
            f.Ground = under[under.Count / 2];
            f.Dev = Mathf.Max(f.Ground - under[0], under[under.Count - 1] - f.Ground);
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (f.Ground < water + 1f) { f.Why = "Тут надто низько — вода поруч (земля " + (f.Ground - water).ToString("0.0") + " м над водою)."; return f; }
            if (f.Dev > MaxDev) { f.Why = "Тут надто криво: земля під залою відходить на " + f.Dev.ToString("0.0") + " м (можна до " + MaxDev.ToString("0") + "). Пошукай рівніше."; return f; }
            f.Ok = true;
            return f;
        }

        // ---------------------------------------------------------------- level: flat under it, eased at the edge
        // bare: earth painted under it (no grass through the floor); bareUnder (its floors' places in it): only under
        // those, the grass round them kept (the user 2026-11-01: grass round the Hall)
        // bareRound > 0: earth only within that many m of its middle, grass round it (a camp's trodden ground)
        internal static string Level(List<Blueprint.Part> parts, Vector3 at, Quaternion rot, float ground, bool bare = true, List<Vector3> bareUnder = null, float bareRound = 0f)
        {
            return Level(Footprint(parts), at, rot, ground, bare, bareUnder, bareRound);
        }

        // the author's own: a square of half m each way round him, turned as he faces, flat at his feet (the grass kept)
        internal static string LevelHere(Player p, float half)
        {
            if (p == null) return "Нема гравця.";
            half = Mathf.Clamp(half, 1f, 30f);
            Rect r = new Rect { X0 = -half, X1 = half, Z0 = -half, Z1 = half };
            Vector3 at = p.transform.position;
            return Level(r, at, Quaternion.Euler(0f, p.transform.eulerAngles.y, 0f), at.y, false, null, 0f) + ". Назад: raven blueprint ground undo";
        }

        internal static string Level(Rect r, Vector3 at, Quaternion rot, float ground, bool bare, List<Vector3> bareUnder, float bareRound)
        {
            Quaternion inv = Quaternion.Inverse(rot);
            if (FLevel == null || FSmooth == null || FModH == null || FPaint == null || FModP == null || MSave == null)
                return "земля: гра змінилась, вирівняти не вдалось";
            _undo.Clear();
            float reach = Reach(r);
            Dictionary<Heightmap, TerrainComp> comps = new Dictionary<Heightmap, TerrainComp>();
            int moved = 0, capped = 0;
            Walk(r, at, rot, reach, (h, x, y, wp, outside) =>
            {
                if (outside >= Margin) return;
                TerrainComp tc;
                if (!comps.TryGetValue(h, out tc))
                {
                    tc = Compiler(h);
                    if (tc == null || (FInit != null && !(bool)FInit.GetValue(tc))) { comps[h] = null; return; }
                    ZNetView nv = tc.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid() && !nv.IsOwner()) nv.ClaimOwnership();
                    _undo.Add(new Snap
                    {
                        Tc = tc,
                        Level = (float[])((float[])FLevel.GetValue(tc)).Clone(),
                        Smooth = (float[])((float[])FSmooth.GetValue(tc)).Clone(),
                        ModH = (bool[])((bool[])FModH.GetValue(tc)).Clone(),
                        Paint = (Color[])((Color[])FPaint.GetValue(tc)).Clone(),
                        ModP = (bool[])((bool[])FModP.GetValue(tc)).Clone()
                    });
                    comps[h] = tc;
                }
                if (tc == null) return;
                float[] level = (float[])FLevel.GetValue(tc), smooth = (float[])FSmooth.GetValue(tc);
                bool[] modH = (bool[])FModH.GetValue(tc);
                int i = y * (h.m_width + 1) + x;
                if (i < 0 || i >= level.Length) return;
                // inside: the ground's height; on the edge: eased back into the land (smoothstep over Margin)
                float s = Mathf.SmoothStep(0f, 1f, outside / Margin);
                float want = Mathf.Lerp(ground, wp.y, s);
                float d = want - wp.y + smooth[i];
                smooth[i] = 0f;
                float l = level[i] + d;
                if (l > 8f || l < -8f) capped++;
                level[i] = Mathf.Clamp(l, -8f, 8f); // (the game's own limit, as the hoe)
                modH[i] = true;
                if (outside <= 0.5f)
                {
                    Color[] paint = (Color[])FPaint.GetValue(tc);
                    bool[] modP = (bool[])FModP.GetValue(tc);
                    // bare under the floor (no grass through it); or, where the place keeps its grass (Loc.Grass), the land's
                    // own look back (earth painted there before — a place built again — is wiped)
                    Vector3 flat = wp - at; flat.y = 0f;
                    bool dirt = bareRound > 0f ? flat.sqrMagnitude <= bareRound * bareRound : bare && (bareUnder == null || Under(bareUnder, inv * (wp - at)));
                    if (i < paint.Length) { paint[i] = dirt ? Heightmap.m_paintMaskDirt : Heightmap.m_paintMaskNothing; modP[i] = dirt; }
                }
                moved++;
            });
            foreach (KeyValuePair<Heightmap, TerrainComp> kv in comps)
            {
                if (kv.Value == null) continue;
                MSave.Invoke(kv.Value, new object[] { false });
                kv.Key.Poke(0, false);
            }
            if (ClutterSystem.instance != null) ClutterSystem.instance.ResetGrass(at, reach);
            int cleared = Clear(r, at, rot);
            return "земля вирівняна (" + moved + " точок" + (capped > 0 ? ", " + capped + " вперлись у межу гри 8 м" : "") + ")" + (cleared > 0 ? ", прибрано дерев і каміння: " + cleared : "");
        }

        // the terrain's changes of each piece of land within reach, far from every player too: one may stand in the world with no body (a
        // hoe, the place built here before), and a second one made beside it would fight it. Brought in, the game loads it and redraws the
        // land with it at once (TerrainComp.CheckLoad → Heightmap.Poke), so what is read after is the ground as it is. Its body goes with the
        // game's next look round (nobody near): what was written stays in its ZDO
        private static void BringIn(Vector3 at, float reach)
        {
            List<Heightmap> maps = new List<Heightmap>();
            Heightmap.FindHeightmap(at, reach, maps);
            foreach (Heightmap h in maps) if (h != null && !h.IsDistantLod && TerrainComp.FindTerrainCompiler(h.transform.position) == null) Made(h);
        }

        private static TerrainComp Compiler(Heightmap h)
        {
            TerrainComp tc = TerrainComp.FindTerrainCompiler(h.transform.position);
            return tc ?? Made(h) ?? h.GetAndCreateTerrainCompiler();
        }

        // the one this land has in the world's list, made (null: it has none yet)
        private static TerrainComp Made(Heightmap h)
        {
            if (FindObjects == null || MakeObject == null || ZDOMan.instance == null || ZNetScene.instance == null) return null;
            List<ZDO> all = new List<ZDO>();
            FindObjects.Invoke(ZDOMan.instance, new object[] { ZoneSystem.GetZone(h.transform.position), all, new HashSet<ZoneSystem.SectorIndex>() }); // heavy: once a piece of land, when a place is checked or levelled
            foreach (ZDO z in all)
            {
                if (z == null || z.GetPrefab() != CompilerHash || ZNetScene.instance.HaveInstance(z)) continue;
                MakeObject.Invoke(ZNetScene.instance, new object[] { z });
                TerrainComp tc = TerrainComp.FindTerrainCompiler(h.transform.position);
                if (tc != null) return tc;
            }
            return null;
        }

        // under one of its floors (a 2×2 floor's half-diagonal and a little)
        private const float UnderFloor = 1.6f;
        private static bool Under(List<Vector3> floors, Vector3 local)
        {
            foreach (Vector3 f in floors)
            {
                float dx = f.x - local.x, dz = f.z - local.z;
                if (dx * dx + dz * dz <= UnderFloor * UnderFloor) return true;
            }
            return false;
        }

        // trees, logs, rocks, bushes, pickables on the footprint and its eased edge: away (as the game clears round its own places)
        private static int Clear(Rect r, Vector3 at, Quaternion rot)
        {
            if (FindObjects == null || ZDOMan.instance == null) return 0;
            Vector3 mid = new Vector3((r.X0 + r.X1) * 0.5f, 0f, (r.Z0 + r.Z1) * 0.5f);
            Vector3 centre = at + rot * mid;
            float hx = (r.X1 - r.X0) * 0.5f + Margin + WildPad, hz = (r.Z1 - r.Z0) * 0.5f + Margin + WildPad;
            float reach = Mathf.Sqrt(hx * hx + hz * hz);
            Quaternion inv = Quaternion.Inverse(rot);
            Vector2s lo = ZoneSystem.GetZone(centre - new Vector3(reach, 0f, reach)), hi = ZoneSystem.GetZone(centre + new Vector3(reach, 0f, reach));
            List<ZDO> all = new List<ZDO>();
            int gone = 0;
            for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                {
                    all.Clear();
                    FindObjects.Invoke(ZDOMan.instance, new object[] { new Vector2s((short)x, (short)y), all, new HashSet<ZoneSystem.SectorIndex>() }); // heavy: once a zone, when a place is levelled
                    foreach (ZDO z in all)
                    {
                        if (z == null || !Wild(z.GetPrefab())) continue;
                        Vector3 d = inv * (z.GetPosition() - centre);
                        if (Mathf.Abs(d.x) > hx || Mathf.Abs(d.z) > hz) continue;
                        z.SetOwner(ZDOMan.GetSessionID());
                        ZDOMan.instance.DestroyZDO(z); // (a body there goes with it: the game's own OnZDODestroyed)
                        gone++;
                    }
                }
            return gone;
        }

        // by the world's list, not by bodies: far from every player they have none (a big rock reaches past its middle: WildPad)
        private const float WildPad = 2f;
        private static readonly Dictionary<int, bool> _wild = new Dictionary<int, bool>();
        private static bool Wild(int hash)
        {
            bool w;
            if (_wild.TryGetValue(hash, out w)) return w;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            w = g != null && g.GetComponent<Piece>() == null && g.GetComponent<Character>() == null && g.GetComponent<ItemDrop>() == null
                && (g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null || g.GetComponent<Destructible>() != null
                    || g.GetComponent<MineRock>() != null || g.GetComponent<MineRock5>() != null || g.GetComponent<Pickable>() != null);
            _wild[hash] = w;
            return w;
        }

        // the world's own building (LocBuild): its ground is kept, never undone by the author's «undo»
        internal static void Forget() { _undo.Clear(); }

        // ---------------------------------------------------------------- undo: the terrain as it was before the last levelling
        internal static string Undo()
        {
            if (_undo.Count == 0) return "";
            int back = 0;
            foreach (Snap s in _undo)
            {
                if (s.Tc == null) continue;
                ZNetView nv = s.Tc.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                if (!nv.IsOwner()) nv.ClaimOwnership();
                Array.Copy(s.Level, (float[])FLevel.GetValue(s.Tc), s.Level.Length);
                Array.Copy(s.Smooth, (float[])FSmooth.GetValue(s.Tc), s.Smooth.Length);
                Array.Copy(s.ModH, (bool[])FModH.GetValue(s.Tc), s.ModH.Length);
                Array.Copy(s.Paint, (Color[])FPaint.GetValue(s.Tc), s.Paint.Length);
                Array.Copy(s.ModP, (bool[])FModP.GetValue(s.Tc), s.ModP.Length);
                MSave.Invoke(s.Tc, new object[] { false });
                Heightmap h = s.Tc.GetComponent<Heightmap>() ?? Heightmap.FindHeightmap(s.Tc.transform.position);
                if (h != null) h.Poke(0, false);
                back++;
            }
            _undo.Clear();
            return back > 0 ? " Землю повернуто як була (дерева й каміння — ні)." : "";
        }
    }
}
#endif

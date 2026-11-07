#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet: the one door to the game's way finder for everything but a walk's own way (NpcWalk.Ask) — is there a way (Have), the
    // whole way (Way: how far on foot, a pantry by a table, a fire's seats, a door that leads there). Before 2026-11-06 these went straight to the
    // way finder from NpcKit, NpcMeals, NpcChores, NpcDoors, NpcMind: a pantry looked over every chest of a base in one frame, none of it in the
    // measures. Now an answer is kept Keep s for the same two spots (to Cell m), so a base asking the same thing again costs nothing; a real ask
    // counts with the walks' own in the frame (AsksAFrame: one asked past it is still answered — a caller needs its answer now — and counted as
    // Over, raven npc perf «path over»), and in the measures as «path (пошук шляху)». The rule path-ask (arch_check) keeps every other ask out.
    internal sealed partial class NpcWalk
    {
        internal static float Keep = 6f, Cell = 1f;
        private const int Most = 512;
        private sealed class Kept { public float At; public bool Ok, Full; public readonly List<Vector3> Points = new List<Vector3>(); }
        // the two spots to Cell m: a key of its own (a hash of six numbers would have let two different asks share an answer)
        private struct Pair : System.IEquatable<Pair>
        {
            public Vector3Int A, B;
            public bool Equals(Pair o) { return A == o.A && B == o.B; }
            public override bool Equals(object o) { return o is Pair && Equals((Pair)o); }
            public override int GetHashCode() { unchecked { return A.GetHashCode() * 486187739 ^ B.GetHashCode(); } }
        }
        private static readonly Dictionary<Pair, Kept> _kept = new Dictionary<Pair, Kept>();
        private static readonly List<Vector3> _wayTmp = new List<Vector3>();
        private static readonly int PerfOver = NpcPerf.Id("path over (понад ліміт кадру)");

        private static Vector3Int CellOf(Vector3 p) { return new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell)); }
        private static Pair Key(Vector3 a, Vector3 b) { return new Pair { A = CellOf(a), B = CellOf(b) }; }

        private static Kept Asked(Vector3 from, Vector3 to)
        {
            Pathfinding pf = Pathfinding.instance;
            if (pf == null) return null;
            Pair k = Key(from, to);
            Kept w;
            if (_kept.TryGetValue(k, out w) && Time.time - w.At < Keep) return w;
            if (_kept.Count >= Most) { _kept.Clear(); w = null; } // (a world's worth of old answers: let go at once — a moment's asking again)
            if (w == null) { w = new Kept(); _kept[k] = w; }
            if (!MayAsk()) NpcPerf.Add(PerfOver, NpcPerf.Now());
            _asksNow++;
            long perf = NpcPerf.Now();
            _wayTmp.Clear();
            w.Ok = pf.GetPath(from, to, _wayTmp, NpcKit.Walker, false, true, false) && _wayTmp.Count > 0;
            NpcPerf.Add(PerfPath, perf);
            w.Points.Clear(); w.Points.AddRange(_wayTmp);
            w.Full = w.Ok && Utils.DistanceXZ(_wayTmp[_wayTmp.Count - 1], to) < 1.5f && Mathf.Abs(_wayTmp[_wayTmp.Count - 1].y - to.y) < 2f;
            w.At = Time.time;
            return w;
        }

        // a whole way there (not one that ends short)
        internal static bool Have(Vector3 from, Vector3 to) { Kept w = Asked(from, to); return w != null && w.Full; }

        // the way there as the way finder gives it (it may end short: its caller judges), into a list of its own
        internal static bool Way(Vector3 from, Vector3 to, List<Vector3> into)
        {
            into.Clear();
            Kept w = Asked(from, to);
            if (w == null || !w.Ok) return false;
            into.AddRange(w.Points);
            return true;
        }
    }
}
#endif

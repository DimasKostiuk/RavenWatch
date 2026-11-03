#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Who is about, for the people's look for a foe (NpcMind.LookForFoe): the game's creatures (no players, nobody dead)
    // laid once every Every s into cells of Cell m on this game — each person then looks only at the cells within its
    // sight, not at every creature loaded (the user 2026-11-01: small, predictable checks).
    internal static class NpcFoes
    {
        internal static float Every = 0.25f, Cell = 16f;
        private static readonly Dictionary<long, List<Character>> Grid = new Dictionary<long, List<Character>>();
        private static readonly Stack<List<Character>> Spare = new Stack<List<Character>>();
        private static float _at = -100f;
        private static int _frame = -1;
        private static readonly int PerfLay = NpcPerf.Id("foes grid (огляд істот)");

        private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        private static void Fresh()
        {
            if (Time.frameCount == _frame || Time.time - _at < Every) return;
            _frame = Time.frameCount; _at = Time.time;
            long perf = NpcPerf.Now();
            foreach (List<Character> l in Grid.Values) { l.Clear(); Spare.Push(l); }
            Grid.Clear();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead()) continue;
                Vector3 p = c.transform.position;
                long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                List<Character> l;
                if (!Grid.TryGetValue(k, out l)) { l = Spare.Count > 0 ? Spare.Pop() : new List<Character>(); Grid[k] = l; }
                l.Add(c);
            }
            NpcPerf.Add(PerfLay, perf);
        }

        // the creatures in the cells within r of a spot (some a little farther: the caller measures)
        internal static void Near(Vector3 at, float r, List<Character> into)
        {
            Fresh();
            int x0 = Mathf.FloorToInt((at.x - r) / Cell), x1 = Mathf.FloorToInt((at.x + r) / Cell);
            int z0 = Mathf.FloorToInt((at.z - r) / Cell), z1 = Mathf.FloorToInt((at.z + r) / Cell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<Character> l;
                    if (Grid.TryGetValue(Key(x, z), out l)) into.AddRange(l);
                }
        }
    }
}
#endif

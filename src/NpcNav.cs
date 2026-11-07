#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The game's way finder maps the land in tiles, only where some creature asked for a way lately, a little at a time,
    // and lets a tile go after a while nobody asks there. Its first answer about an unmapped place is «no way» — and a
    // base's people asked about seats, tables, fires they had not walked to yet, and walked straight into walls when it
    // had no way for them (2026-10-31). So: the land round each base with people in it kept mapped — its middle, a ring
    // round it, every person of it — asked every few seconds where it is run, each Cell m square once (it all falls in one frame). And a test command: what the way finder
    // sees round someone (raven npc nav <who>).
    internal static class NpcNav
    {
        private const float Every = 4f, Ring = 24f, Cell = 8f;
        private static float _next;
        private static readonly HashSet<long> Asked = new HashSet<long>();
        private static readonly int PerfNav = NpcPerf.Id("nav (карта шляхів)");

        internal static void Tick()
        {
            if (Time.time < _next || Pathfinding.instance == null) return;
            _next = Time.time + Every;
            long perf = NpcPerf.Now();
            Asked.Clear();
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || !a.Owned) continue;
                Poke(a.transform.position);
                NpcResident r = a.Resident;
                if (r == null || !NpcResident.Is(a)) continue;
                Vector3 home = NpcRules.Home(a);
                Poke(home);
                for (int i = 0; i < 6; i++) Poke(home + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * Ring);
            }
            NpcPerf.Add(PerfNav, perf);
        }

        private static void Poke(Vector3 p)
        {
            if (!Asked.Add(((long)Mathf.FloorToInt(p.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(p.z / Cell))) return;
            Vector3 v;
            Pathfinding.instance.FindValidPoint(out v, p, 1f, NpcKit.Walker); // (asking is what keeps a tile: the answer itself not needed)
        }

        // test: the walkable floor the way finder knows round someone, by radius and kind of walker, and its way home
        internal static string Describe(NpcActor a)
        {
            if (Pathfinding.instance == null) return "Нема шукача шляхів.";
            Vector3 me = a.transform.position;
            StringBuilder sb = new StringBuilder();
            sb.Append(a.Id).Append(" at ").Append(Npcs.V(me)).Append("");
            foreach (Pathfinding.AgentType t in new[] { NpcKit.Walker })
            {
                sb.Append(" · ").Append(t).Append(":");
                foreach (float r in new[] { 0.5f, 2.5f, 6f })
                {
                    Vector3 v;
                    Vector3 g; bool game = Pathfinding.instance.FindValidPoint(out g, me, r, t);
                    bool ok = NpcKit.Valid(out v, me, r, t);
                    sb.Append(" ").Append(r).Append("m game ").Append(game ? "yes" : "no").Append(" ours");
                    sb.Append(" ").Append(ok ? Mathf.Round(Vector3.Distance(v, me) * 10f) / 10f + "" : "no");
                }
            }
            // the same way to you as the game's humanoid finds it and as theirs does (2026-11-06: a raised floor an island for the game's one)
            Player you = Player.m_localPlayer;
            if (you != null)
                foreach (Pathfinding.AgentType t in new[] { Pathfinding.AgentType.HumanoidNoSwim, NpcKit.Walker })
                {
                    List<Vector3> way = new List<Vector3>();
                    bool got = Pathfinding.instance.GetPath(me, you.transform.position, way, t, false, true, false);
                    float len = 0f; for (int i = 1; i < way.Count; i++) len += Vector3.Distance(way[i - 1], way[i]);
                    float shortBy = way.Count > 0 ? Utils.DistanceXZ(way[way.Count - 1], you.transform.position) : -1f;
                    sb.Append(" · to you as ").Append(t == Pathfinding.AgentType.HumanoidNoSwim ? "game" : "ours").Append(": ").Append(got ? way.Count + " points, " + Mathf.RoundToInt(len) + " m, ends " + shortBy.ToString("0.0") + " m short at " + Npcs.V(way[way.Count - 1]) + " (you at " + Npcs.V(you.transform.position) + ")" : "none");
                }
            Vector3 home = NpcRules.Home(a);
            sb.Append(" · home ").Append(Mathf.RoundToInt(Utils.DistanceXZ(me, home))).Append(" m: path ").Append(Pathfinding.instance.HavePath(me, home, NpcKit.Walker) ? "yes" : "no");
            float f = NpcKit.OnFoot(me, home, 200f);
            sb.Append(", on foot ").Append(f < float.MaxValue ? Mathf.RoundToInt(f) + " m" : "none (" + NpcKit.FootWhy + ")");
            return sb.ToString();
        }
    }
}
#endif

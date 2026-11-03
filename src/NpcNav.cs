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
    // round it, every person of it — asked every few seconds where it is run. And a test command: what the way finder
    // sees round someone (raven npc nav <who>).
    internal static class NpcNav
    {
        private const float Every = 4f, Ring = 24f;
        private static float _next;
        private static readonly HashSet<Vector3> Bases = new HashSet<Vector3>();

        internal static void Tick()
        {
            if (Time.time < _next || Pathfinding.instance == null) return;
            _next = Time.time + Every;
            Bases.Clear();
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || !a.Owned) continue;
                Poke(a.transform.position);
                NpcResident r = a.Resident;
                if (r == null || !NpcResident.Is(a)) continue;
                Vector3 home = NpcRules.Home(a);
                if (!Bases.Add(new Vector3(Mathf.Round(home.x), 0f, Mathf.Round(home.z)))) continue;
                Poke(home);
                for (int i = 0; i < 6; i++) Poke(home + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * Ring);
            }
        }

        private static void Poke(Vector3 p)
        {
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
            Vector3 home = NpcRules.Home(a);
            sb.Append(" · home ").Append(Mathf.RoundToInt(Utils.DistanceXZ(me, home))).Append(" m: path ").Append(Pathfinding.instance.HavePath(me, home, NpcKit.Walker) ? "yes" : "no");
            float f = NpcKit.OnFoot(me, home, 200f);
            sb.Append(", on foot ").Append(f < float.MaxValue ? Mathf.RoundToInt(f) + " m" : "none (" + NpcKit.FootWhy + ")");
            return sb.ToString();
        }
    }
}
#endif

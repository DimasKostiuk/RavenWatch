#if RAVEN_ITEM && RAVEN_NPC
// (the Hall of the Watch in every world: from 2.9 — the user 2026-10-31; since 2026-11-01 one of the story's places, Loc)
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The Hall of the Watch: the first of the story's places (Loc, Locs) — the Meadows, 300–1500 m from the start, its
    // blueprint assets/story/blueprints/zala.txt. What asks for the Hall asks here; what is done for every place (the
    // server's seeking, the builder's look, the no-build ring) goes round all of them from here.
    internal static class HallSpawn
    {
        internal const string Name = "zala";
        internal const float NoBuild = 30f;

        private static Loc H { get { return Locs.Hall; } }

        internal static bool Where(out Vector3 at, out Quaternion rot) { return H.Where(out at, out rot); }
        internal static List<Vector3> Corners() { return H.Corners(); }
        internal static List<Vector3> Floors() { return H.Floors(); }
        internal static bool Built { get { return H.Built; } }

        // the author's look (`raven blueprint hall`): every place of the story, where it is
        internal static string Tell(Player p)
        {
            List<string> lines = new List<string>();
            foreach (Loc l in Locs.All) lines.Add(l.Tell(p));
            return string.Join("\n", lines.ToArray()) + "\nЗолоті знаки — на мапі.";
        }

        // inside any standing place's no-build ring (the author's folder asked last: he builds anywhere)
        internal static bool InNoBuild(Vector3 point)
        {
            foreach (Loc l in Locs.All) if (l.InNoBuild(point)) return !Perches.StoryAuthor;
            return false;
        }

        internal static void RegisterRpcs() { foreach (Loc l in Locs.All) l.RegisterRpcs(); }
        internal static void ServerTick() { foreach (Loc l in Locs.All) l.ServerTick(); }
        internal static void Tick() { foreach (Loc l in Locs.All) l.Tick(); }
    }
}
#endif

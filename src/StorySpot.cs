#if RAVEN_ITEM && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // A spot of the player's own (the prologue: where Eirik's raven circles over you — the user 2026-11-03): picked once
    // in the wild by WorldSpot (open ground, few trees, never by a base or a place of the world), kept in the character
    // (vars spot_<name>_x / _z, whole metres), so a relog finds the same one. The story's words for it:
    //   spot <name> <min> <max>     the action: picks it min..max m from the player (again: a new one)
    //   me.<name>                   the place (StoryWhere): a quest's «mark:», «near», a scene's steps
    // Picked once, ring by ring from near to far, the story never waiting on it: the most open, flattest, with fewest trees (WorldSpot.Best);
    // on to the next ring only while what is found is no good (more trees than FewTrees, more uneven than Flat, more wooded than Wooded). On land not
    // loaded yet its trees are unknown: looked over when it loads (Find). Nothing at all: any dry land, at last where he
    // stands. Never by water (Shore). The log says which ring and what it has.
    internal static class StorySpot
    {
        internal const string Prefix = "me.";
        internal static float Open = 18f; // m round it that count: he walks 16 m in (no tree in his way) and stands there
        internal static float Flat = 2.5f; // m the ground may rise and fall over Open
        internal static int FewTrees = 1;
        internal static float Wooded = 2f; // WorldSpot.Forest: more than this, the next ring is looked at
        internal static float LoadedFar = 110f, Closer = 50f; // m: as far as the ground is loaded round him; how far a spot moves when looked over
        internal static float Shore = 15f; // m round it with no water

        internal static void Actions()
        {
            StoryWhere.Prefixed[Prefix] = (string rest, out Vector3 at) => Find(Player.m_localPlayer, rest, out at);
            StoryActions.Register("spot", new StoryActions.Kind { Dry = "set me spot:{1}", Min = 3, Usage = "spot <назва> <від м> <до м>   (своє місце гравця на відкритому, далеко від баз: місце me.<назва>)",
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : Errands.I(a[2], -1) < 1 || Errands.I(a[3], -1) < Errands.I(a[2], -1) ? "spot: від і до — метри, від < до" : null,
                Do = (p, q, w, rest) => { Pick(p, w[1], Errands.I(w[2], 40), Errands.I(w[3], 80)); return null; } });
        }

        internal static void Pick(Player p, string name, float min, float max)
        {
            if (p == null) return;
            Vector3 at = Vector3.zero, from = p.transform.position, q;
            int trees = 0, t2; float uneven = 0f, u2;
            string how = "", first = "";
            bool got = false;
            // nearest first, farther only while what is found is no good: a flat place matters more than a near one
            float[][] rings = { new[] { min, max }, new[] { max, LoadedFar }, new[] { LoadedFar, LoadedFar * 2f }, new[] { LoadedFar * 2f, LoadedFar * 4f } };
            foreach (float[] ring in rings)
            {
                if (Good(got, trees, uneven) && WorldSpot.Forest(at) <= Wooded) break;
                bool found = WorldSpot.Best(p, from, ring[0], ring[1], Open, Shore, out q, out t2, out u2);
                if (first.Length == 0) first = WorldSpot.Why;
                if (!found || (got && t2 * WorldSpot.TreeWeighs + u2 + WorldSpot.Forest(q) >= trees * WorldSpot.TreeWeighs + uneven + WorldSpot.Forest(at))) continue;
                got = true; at = q; trees = t2; uneven = u2; how = ring[0] + "-" + ring[1] + " m";
            }
            if (!got) { how = "land only"; got = WorldSpot.Pick(p, from, min, LoadedFar * 4f, false, null, out at, Shore, true); }
            if (!got) { how = "where he stands"; at = from; } // (nothing dry round him at all: never into the water)
            bool later = got && !WorldSpot.Here(at); // (its trees unknown: looked over when he comes: Find)
            Keep(p, name, at);
            Story.SetFlag(p, Key(name, "later"), later);
            Story.Log("spot " + name + ": " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + ", " + Mathf.RoundToInt(WorldSpot.Flat(at, from)) + " m off, " + how + ", trees " + (later ? "?" : trees.ToString()) + ", uneven " + uneven.ToString("0.0") + " m, forest factor " + WorldGenerator.GetForestFactor(at).ToString("0.00") + " (first ring turned down: " + first + ")");
        }

        private static bool Good(bool got, int trees, float uneven) { return got && trees <= FewTrees && uneven <= Flat; }

        private static void Keep(Player p, string name, Vector3 at)
        {
            Story.SetVar(p, Key(name, "x"), Mathf.RoundToInt(at.x));
            Story.SetVar(p, Key(name, "z"), Mathf.RoundToInt(at.z));
            Story.SetFlag(p, Key(name, "set"), true);
            Story.SetFlag(p, AnyKey(name), true);
        }

        // where it is: on the ground when its zone is here, else the world's height there. One picked on land not loaded then
        // is looked over once it is: the best spot close by (its trees could not be seen from afar)
        internal static bool Find(Player p, string name, out Vector3 at)
        {
            at = Vector3.zero;
            if (p == null || !Story.Flag(p, AnyKey(name))) return false;
            if (!Story.Flag(p, Key(name, "set"))) Pick(p, name, AgainMin, AgainMax); // (he has one, in another world: the prologue goes with him, so a new one here. One pick a world)
            at = new Vector3(Story.Var(p, Key(name, "x")), 0f, Story.Var(p, Key(name, "z")));
            if (Story.Flag(p, Key(name, "later")) && WorldSpot.Here(at))
            {
                Story.SetFlag(p, Key(name, "later"), false);
                Vector3 better; int trees; float uneven;
                if (WorldSpot.Best(p, at, 0f, Closer, Open, Shore, out better, out trees, out uneven))
                {
                    Keep(p, name, better);
                    at = new Vector3(Story.Var(p, Key(name, "x")), 0f, Story.Var(p, Key(name, "z")));
                    Story.Log("spot " + name + ": looked over from near: " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + ", trees " + trees + ", uneven " + uneven.ToString("0.0") + " m, forest factor " + WorldGenerator.GetForestFactor(at).ToString("0.00"));
                }
            }
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out h)) at.y = h;
            else if (WorldGenerator.instance != null) at.y = WorldGenerator.instance.GetHeight(at.x, at.z);
            return true;
        }

        private static string Key(string name, string part) { return "spot_" + name + "_" + part + "_" + (ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L); } // (a spot of the ground: of this world only)
        private static string AnyKey(string name) { return "spot_" + name + "_any"; }
        internal static float AgainMin = 40f, AgainMax = 80f; // m: a new one in another world
    }
}
#endif

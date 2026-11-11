#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What is the world's own growth and stone (a tree, a log, a bush, a stone, a pickable) and nobody's: the one answer for
    // everything that clears the wild away (the ground under a place of the story: BlueprintGround; the roots of a raven
    // tree: RavenTree).
    //   Kind: by its prefab, once a kind — a tree, a log, a rock, something that breaks, something to pick, or the game's
    //         plain bushes, which have none of these (Bush01, shrub_2: only StaticPhysics); never a creature, an item, a
    //         building piece that wears (WearNTear), a raven tree.
    //   Is:   this one in the world — its kind, and nobody put it there (no creator). A Piece on it says nothing: another mod
    //         may make the game's own bushes pieces (PlantEverything puts a Piece on Bush01, Bush02_en, shrub_2, the berry
    //         bushes, small firs, the pickables, so they can be planted); one a player planted has its creator.
    // Bushes stood inside the Hall and the ghost's house (2026-11-08): the ground's clearing let pass every prefab with a Piece.
    internal static class Wild
    {
        private static readonly Dictionary<int, bool> _kind = new Dictionary<int, bool>();

        internal static bool Kind(int hash)
        {
            bool w;
            if (_kind.TryGetValue(hash, out w)) return w;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            if (g == null) return false; // (not cached: the scene may not know it yet)
            _kind[hash] = w = Kind(g);
            return w;
        }

        private static bool Kind(GameObject g)
        {
            if (g.GetComponent<Character>() != null || g.GetComponent<ItemDrop>() != null || g.GetComponent<WearNTear>() != null || g.GetComponent<TreeView>() != null) return false;
            return g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null || g.GetComponent<MineRock>() != null || g.GetComponent<MineRock5>() != null
                || g.GetComponent<Destructible>() != null || g.GetComponent<Pickable>() != null || g.GetComponent<StaticPhysics>() != null;
        }

        internal static bool Is(ZDO z)
        {
            return z != null && z.GetLong(ZDOVars.s_creator, 0L) == 0L && Kind(z.GetPrefab());
        }

        internal static bool Is(ZNetView nv)
        {
            return nv != null && nv.IsValid() && Is(nv.GetZDO());
        }
    }
}
#endif

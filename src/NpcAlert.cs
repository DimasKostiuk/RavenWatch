#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // Whether a person is on its guard now (the user 2026-11-08: «поки вони сполохані — поза бойова», else its own calm one): the one answer for its body and its
    // hands — the pose it may take (NpcStance), whether a guard's weapon is out of turn (NpcGear.InHand) — on every game, from what the core already knows:
    //   a fight         its mind's fight layer (NpcMind.LayerNow: its own where it is run, its owner's word elsewhere)
    //   a fright        its feeling now (NpcFeel: in its ZDO)
    //   just fought     a while after its last fight's end (NpcMind.FoughtOf), so it does not drop its guard the instant the foe falls
    // No look of its own for a foe: its mind looks (NpcMind.LookForFoe) and goes to its fight layer or a fright. Cheap: a layer, a feeling, a number. Rule alert-core: who
    // decides a pose or a weapon by «a fight» asks here, not the fight layer or a fright itself.
    internal static class NpcAlert
    {
        internal static float AfterFight = 8f; // s on its guard after its fight is over

        internal static bool On(NpcActor a)
        {
            if (a == null) return false;
            if (NpcMind.LayerNow(a) == NpcMind.Layer.Fight) return true;
            string feel; float strength;
            if (NpcFeel.Of(a, out feel, out strength) && feel == NpcFeel.Fright) return true;
            bool ran, won;
            return NpcMind.FoughtOf(a, out ran, out won) < AfterFight;
        }
    }
}
#endif

#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Терпимість» at the moments that matter (the user, 2026-10-23): someone your raven can't stand is nearly dead or
    // dies (glee), someone it respects dies (grief; nearly dead is People's alarm already), and either kind back in sight
    // after days apart. The lines: BondMomentPatterns (people_*).
    internal static partial class People
    {
        internal static float BackDays = 2f, BackChance = 0.7f;

        // a player near brought low (Bonds.Watch, when no raven of theirs matters to mine): false when nothing was said
        internal static bool OnPlight(Player me, Player o, bool dead)
        {
            if (!Enabled) return false;
            foreach (bool m in new[] { true, false })
            {
                if (!Free(me, m)) continue;
                State st = StateTo(me, m, o);
                string tag = st <= State.Dislike ? (dead ? "people_hate_died" : "people_hate_hurt") : st >= State.Respect && dead ? "people_respect_died" : null;
                if (tag != null && Say(me, m, o, tag, Director.Voice.Event)) return true;
            }
            return false;
        }

        // in sight again this session (Tick, the first look at them): one it has strong feelings about, not met for days
        private static void Back(Player me, Player o)
        {
            if (UnityEngine.Random.value > BackChance) return;
            foreach (bool m in new[] { true, false })
            {
                ItemDrop.ItemData item = RavenSeat.On(me, m);
                Rec r = item != null ? Find(item, o.GetPlayerID()) : null;
                if (r == null || !r.Shown || r.Last < 0 || Today - r.Last < BackDays || !Free(me, m)) continue;
                State st = StateOf(r.Score);
                string tag = st <= State.Dislike ? "people_hate_back" : st >= State.Respect ? "people_respect_back" : null;
                if (tag != null && Say(me, m, o, tag, Director.Voice.Shoulder)) return;
            }
        }
    }
}
#endif

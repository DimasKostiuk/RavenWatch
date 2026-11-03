#if RAVEN_ITEM
using System.Collections.Generic;

namespace HuginWhispers
{
    // What a line is about, so the bits a raven adds on its end fit it: «Логічно.» after you fell off a cliff, never after
    // the rain; a jab only when you messed up; nothing tacked onto a memory or a friend's death.
    //   Fail    you messed up or got hurt (deaths, falls, a near death, a miss, too heavy, poisoned…)
    //   Win     you did something (kills, bosses, crafting, building, ore, fish, loot, a good grade…)
    //   Talk    the ravens' talks and words about other players (only a sound on the end, never a verdict at you)
    //   Sincere memories, a grave, a friend lost, a stroke of the hand: nothing added at all
    //   Neutral the rest (weather, the land, idling, the world, the ravens' own business)
    // Set by the tag before a line is put together (Patterns.ComposeIn, Composer's whole lines).
    internal static class LineMood
    {
        internal const int Neutral = 0, Fail = 1, Win = 2, Talk = 3, Sincere = 4;
        internal static int Now;

        internal static void For(string tag) { Now = Of(tag); }

        private static readonly Dictionary<string, int> Known = new Dictionary<string, int>();
        internal static int Of(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return Neutral;
            int m;
            if (Known.TryGetValue(tag, out m)) return m;
            m = Find(tag);
            Known[tag] = m;
            return m;
        }

        private static int Find(string t)
        {
            if (Starts(t, "mem_", "grave_", "fate_", "guest_", "nick_", "breed_reveal_") || Is(t, "loyal", "petted", "awake", "heal",
                "bond_friend_died", "bond_friend_hurt", "people_respect_died", "people_respect_back", "stranger_death", "errand_failed", "habit_lost")) return Sincere;
            if (Starts(t, "dlg_", "x_", "about_", "bond_", "pair_", "perch_", "chase_", "joint_", "victim_", "people_") || Is(t, "cut_in", "stranger_talk", "again_lead")) return Talk;
            if (Starts(t, "death", "fall", "judge_tip_") || Is(t, "near_death", "sh_near_death", "sh_fall", "encumbered", "escaped", "flee", "broke", "freezing", "poisoned", "burning",
                "portal_ore", "hstrike_miss", "mstrike_miss", "stranger_peck", "judge_1", "judge_2", "lastchance", "hungry", "hungry_bad", "hungry_ask_again", "starve_peck",
                "home_forgotten", "home_broken", "revenge_late", "revenge_gone")) return Fail;
            if (Starts(t, "kill", "sh_kill", "build", "craft", "cook", "smelt", "ferment", "repair", "chop", "mine", "pick", "plant", "loot", "trade", "fav_", "fed", "trophy",
                "hstrike", "mstrike") || Is(t, "boss", "sh_boss", "strike_boss", "strike_stronger", "sh_build", "sh_smelt", "sh_plant", "sh_trade", "sh_trophy_hang", "fish", "fish_again",
                "tame", "mtame_done", "level", "levelup", "judge_4", "judge_5", "errand_done", "revenge_paid", "parry", "dodge", "beast_kill")) return Win;
            return Neutral;
        }

        private static bool Starts(string t, params string[] heads) { foreach (string h in heads) if (t.StartsWith(h, System.StringComparison.Ordinal)) return true; return false; }
        private static bool Is(string t, params string[] tags) { foreach (string x in tags) if (t == x) return true; return false; }
    }
}
#endif

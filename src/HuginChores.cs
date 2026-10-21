#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // More of Hugin's peaceful strength, from ordinary life at home (Munin stays the fighting one):
    //   - a night's sleep in a bed: a share of his strength in the morning, once a night, and a little trust
    //     (a night spent together);
    //   - craft and cooking: a point for each thing made (not building), at most CraftPerDay a game day;
    //   - the farm: a point for each harvest (berries, crops, mushrooms, honey, sap) or planting, at most FarmPerDay;
    //   - sailing: a point for every SailMeters on a ship (steering or riding along), no daily limit;
    //   - walking: a point for every WalkMeters on foot (walked or run), at most WalkPerDay;
    //   - work: a point for each tree felled, log split or rock broken, two for a fish, at most WorkPerDay.
    // And Munin's side of it: a point for every few blows landed and for every wound taken from a creature, at most
    // FightPerDay (on top of his kills, scrapes and deaths).
    // Craft, farm, work and fight give strength only: everyday chores must not fill the day's trust.
    // The game's own player statistics tell what was done (one hook for all of it), planting is seen when placed.
    internal static class HuginChores
    {
        internal static float SleepShare = 0.15f;              // of Hugin's most strength
        internal static float SleepTrust = 10f;                // a deed of this size: Hugin about 5 trust, Munin 3.5
        internal static float CraftPerDay = 50f, FarmPerDay = 50f, WorkPerDay = 40f, FightPerDay = 50f;
        internal static int HitsPerPoint = 6;      // Munin: blows landed on creatures for a point
        internal static float HurtPerPoint = 30f;  // Munin: damage taken from creatures for a point
        internal static float SailMeters = 200f; // about 3 a minute at full sail (by night ×1.5)
        internal static float WalkMeters = 500f, WalkPerDay = 30f; // on foot: about 1 a minute running
        private const string SleptKey = "RavenWatch_sleptDay";
        private const string CraftKey = "RavenWatch_craft", FarmKey = "RavenWatch_farm", WorkKey = "RavenWatch_work", FightKey = "RavenWatch_fight";
        private const string MeadKey = "RavenWatch_mead", TrophyKey = "RavenWatch_trophy", WalkKey = "RavenWatch_walk";
        internal static float MeadPerDay = 10f, TrophyPerDay = 10f;
        private static int _hits;
        private static float _hurt;
        private static float _walked; // meters on foot not yet counted
        private static float _sailed; // meters sailed not yet counted (the chores' daily counts: "day:points" in the character)

        private static readonly HashSet<PlayerStatType> Craft = new HashSet<PlayerStatType>
        {
            PlayerStatType.CraftsOrUpgrades, // the workbench, forge, cauldron... (one for each craft)
            PlayerStatType.CraftGrill,       // a cooked piece taken off the fire or out of the oven
        };
        private static readonly HashSet<PlayerStatType> Work = new HashSet<PlayerStatType>
        {
            PlayerStatType.Tree, // a tree felled
            PlayerStatType.Logs, // a log split
            PlayerStatType.Mines, // a rock or a deposit broken
        };
        private static readonly HashSet<PlayerStatType> Farm = new HashSet<PlayerStatType>
        {
            PlayerStatType.HarvestCrop, PlayerStatType.HarvestBerry, PlayerStatType.HarvestMushroom, PlayerStatType.HarvestVine,
            PlayerStatType.BeesHarvested, PlayerStatType.SapHarvested,
        };

        private static bool _wasSleeping;
        private static int _sleptFrom = -1;

        private static int Day() { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; }

        // a stat went up (only ever the local player's own)
        internal static void OnStat(PlayerStatType stat, float amount, bool cheated)
        {
            if (cheated) return;
            if (stat == PlayerStatType.EnemyHits) Beast.OnHit(); // the beast's Munin roars at the blows
            if (stat == PlayerStatType.DistanceSail)
            {
                _sailed += Mathf.Max(0f, amount);
                while (SailMeters > 0f && _sailed >= SailMeters) { _sailed -= SailMeters; Power.Earn(false, NightBonus(), Lang.T("плавання"), false); }
                return;
            }
            if (stat == PlayerStatType.DistanceWalk || stat == PlayerStatType.DistanceRun)
            {
                _walked += Mathf.Max(0f, amount);
                while (WalkMeters > 0f && _walked >= WalkMeters) { _walked -= WalkMeters; Chore(WalkKey, WalkPerDay); } // by night ×1.5 there too
                return;
            }
            if (Craft.Contains(stat)) Chore(CraftKey, CraftPerDay);
            else if (Farm.Contains(stat)) Chore(FarmKey, FarmPerDay);
            else if (Work.Contains(stat)) Chore(WorkKey, WorkPerDay);
            else if (stat == PlayerStatType.FishCaught) { Chore(WorkKey, WorkPerDay); Chore(WorkKey, WorkPerDay); }
            else if (stat == PlayerStatType.EnemyHits && HitsPerPoint > 0 && ++_hits >= HitsPerPoint) { _hits = 0; Chore(FightKey, FightPerDay, true); }
        }

        // Hugin's chores give more at night (the fire does not); Munin's night bonus is in Power itself
        private static float NightBonus() { return EnvMan.instance != null && EnvMan.IsNight() ? Power.NightMul : 1f; }

        // a perfect block: two of the fight's points
        internal static void OnParry() { Chore(FightKey, FightPerDay, true); Chore(FightKey, FightPerDay, true); }

        // a mead drunk: Munin wets his beak too (two points, within its own limit)
        internal static void OnMead() { Chore(MeadKey, MeadPerDay, true); Chore(MeadKey, MeadPerDay, true); }

        // a trophy picked up: a head for Munin (its own small limit: dropping and picking it up again pays little)
        internal static void OnTrophy() { Chore(TrophyKey, TrophyPerDay, true); }

        // a wound from a creature (falls, fire and the like do not count): Munin remembers the pain
        internal static void OnHurt(float damage)
        {
            if (damage <= 0f || HurtPerPoint <= 0f) return;
            _hurt += damage;
            while (_hurt >= HurtPerPoint) { _hurt -= HurtPerPoint; Chore(FightKey, FightPerDay, true); }
        }

        internal static void OnPlanted() { Chore(FarmKey, FarmPerDay); }

        // the short reason shown in the corner
        private static string Why(string key)
        {
            if (key == CraftKey) return Lang.T("ремесло");
            if (key == FarmKey) return Lang.T("господарство");
            if (key == WorkKey) return Lang.T("робота");
            if (key == FightKey) return Lang.T("бій");
            if (key == MeadKey) return Lang.T("медовуха");
            if (key == TrophyKey) return Lang.T("трофей");
            if (key == WalkKey) return Lang.T("подорож");
            return null;
        }

        // a point, within the day's limit (kept in the character: the limit holds over a relog)
        private static void Chore(string key, float perDay) { Chore(key, perDay, false); }

        private static void Chore(string key, float perDay, bool munin)
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.m_customData == null || perDay <= 0f) return;
            int day = Day(), d = -1;
            float done = 0f;
            string raw;
            if (p.m_customData.TryGetValue(key, out raw))
            {
                string[] parts = raw.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out d)) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out done);
            }
            if (d < day) done = 0f; // a new day (a later day from another world counts as today)
            if (done >= perDay) return;
            p.m_customData[key] = Mathf.Max(d, day) + ":" + (done + 1f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Power.Earn(munin, munin ? 1f : NightBonus(), Why(key), false);
        }

        // twice a second (Power.Tick, while Hugin is worn): a night slept through, told once in the morning
        internal static void Tick(Player p)
        {
            bool sleeping = p.IsSleeping();
            if (sleeping && !_wasSleeping) _sleptFrom = Day();
            if (!sleeping && _wasSleeping && _sleptFrom >= 0 && Day() > _sleptFrom) Slept(p);
            _wasSleeping = sleeping;
        }

        private static void Slept(Player p)
        {
            int day = Day(), last;
            string raw;
            if (p.m_customData.TryGetValue(SleptKey, out raw) && int.TryParse(raw, out last) && last >= day) return; // once a night
            p.m_customData[SleptKey] = day.ToString();
            Power.Earn(false, Mathf.Round(Power.MaxWorn(false) * SleepShare), Lang.T("сон"), false);
            Trust.Deed(false, SleepTrust, Lang.T("сон")); // a night spent together
        }

        // the fire charges faster in a cosy place: twice as fast at comfort 15
        internal static float RestSeconds(Player p, float baseSeconds)
        {
            int comfort = RawComfort.Of(p); // the game's own count (mods' comfort bonuses would speed it up without end)
            return baseSeconds / (1f + Mathf.Max(0, comfort) / 15f);
        }

        internal static void Reset() { _wasSleeping = false; _sleptFrom = -1; _sailed = 0f; _walked = 0f; _hits = 0; _hurt = 0f; }
    }

    // every stat of the local player passes here (the game counts the sailed distance straight into the profile)
    [HarmonyPatch(typeof(PlayerProfile), "IncrementStat")]
    internal static class ChoreStatPatch
    {
        private static void Postfix(PlayerStatType stat, float amount, bool cheated) { HuginChores.OnStat(stat, amount, cheated); Nicks.OnStat(stat, amount, cheated); }
    }

    [HarmonyPatch(typeof(Player), "PlacePiece")]
    internal static class ChorePlantPatch
    {
        private static void Postfix(Player __instance, Piece piece, bool cheated)
        {
            if (cheated || piece == null || __instance != Player.m_localPlayer) return;
            if (piece.GetComponent<Plant>() != null) HuginChores.OnPlanted(); // a seed in the ground; other building does not count
        }
    }
}
#endif

using System.Collections.Generic;
using System.Globalization;

namespace HuginWhispers
{
    // The character's lifetime numbers that vanilla keeps in the profile (deaths, kills per creature,
    // distance, trees, fish...), turned into placeholders for the quick ravens' lines.
    // A number that is still zero gives no placeholder, so a line needing it is never picked.
    internal static class Stats
    {
        internal static void Fill(Dictionary<string, string> v, Diary.Summary s) { using (Lang.Phrase()) FillIn(v, s); } // (the line's values in its own language)
        private static void FillIn(Dictionary<string, string> v, Diary.Summary s)
        {
            PlayerProfile prof = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (prof == null) return;

            int deaths = Get(prof, PlayerStatType.Deaths);
            if (deaths > 0) v["deaths_all"] = Count(deaths, Lang.T("смерть|смерті|смертей"));

            int cause = CauseDeaths(prof, s.LastDeathCause);
            if (cause > 0) v["cause_times"] = Times(cause);

            Put(v, "tombs_times", Get(prof, PlayerStatType.TombstonesOpenedOwn), true);
            Put(v, "bosses_all", Get(prof, PlayerStatType.BossKills), false);
            Put(v, "kills_all", Get(prof, PlayerStatType.EnemyKills), false);
            Put(v, "trees_all", Get(prof, PlayerStatType.Tree), false);
            Put(v, "mines_all", Get(prof, PlayerStatType.Mines), false);
            Put(v, "fish_all", Get(prof, PlayerStatType.FishCaught), false);
            Put(v, "fish_lost", Get(prof, PlayerStatType.FishLost), false);
            Put(v, "built_all", Get(prof, PlayerStatType.BuiltPieces), false);
            Put(v, "portals_times", Get(prof, PlayerStatType.PortalsUsed), true);
            Put(v, "sleep_all", Get(prof, PlayerStatType.Sleep), false);
            Put(v, "food_all", Get(prof, PlayerStatType.FoodEaten), false);
            Put(v, "berries_all", Get(prof, PlayerStatType.HarvestBerry), false);
            Put(v, "jumps_all", Get(prof, PlayerStatType.Jumps), false);

            int streak = Get(prof, PlayerStatType.ConsecutiveDaysSurvivedMax);
            if (streak > 0) v["streak_max"] = Count(streak, Lang.T("день|дні|днів"));
            int baseHours = Get(prof, PlayerStatType.TimeInBase) / 3600;
            if (baseHours > 0) v["base_hours"] = Lang.F("{0} год", Num(baseHours));

            Km(v, "km_all", Total(prof, PlayerStatType.DistanceTraveled));
            Km(v, "air_km", Total(prof, PlayerStatType.DistanceAir));
            Km(v, "sail_km", Total(prof, PlayerStatType.DistanceSail));

            Dictionary<string, float> enemies = Enemies(prof);
            Enemy(v, enemies, "killer_kills", s.LastKiller);
            Enemy(v, enemies, "top_kills", s.TopKill);
        }

        internal static int Get(PlayerProfile prof, PlayerStatType t)
        {
            return (int)Total(prof, t);
        }

        // The character's whole life: the profile's first set. The game's GetStat gives the set of the world's achievement
        // difficulty instead, which leaves out what happened in other worlds and settings (73 deaths showed as 67).
        private static float Total(PlayerProfile prof, PlayerStatType t)
        {
            try
            {
                if (prof.m_playerStats != null && prof.m_playerStats.Length > 0 && prof.m_playerStats[0] != null) return prof.m_playerStats[0][t];
                return prof.GetStat(t);
            }
            catch { return 0f; } // a stat this game version does not know
        }

        // the profile's lifetime kills per creature, keyed by the creature's $name
        private static Dictionary<string, float> Enemies(PlayerProfile prof)
        {
            if (prof.m_playerStats == null || prof.m_playerStats.Length == 0 || prof.m_playerStats[0] == null) return null;
            Dictionary<string, float>[] all = prof.m_playerStats[0].m_enemyStats;
            return all != null && all.Length > 0 ? all[0] : null;
        }

        // how many of this creature the character has ever killed (0: never, or the profile is not ready)
        internal static int Kills(string token)
        {
            PlayerProfile prof = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            Dictionary<string, float> enemies = prof != null ? Enemies(prof) : null;
            float n;
            return enemies != null && !string.IsNullOrEmpty(token) && enemies.TryGetValue("$" + token, out n) ? (int)n : 0;
        }

        private static void Enemy(Dictionary<string, string> v, Dictionary<string, float> enemies, string key, string token)
        {
            if (enemies == null || string.IsNullOrEmpty(token)) return;
            float n;
            if (enemies.TryGetValue("$" + token, out n) && n >= 1f) v[key] = Num((int)n);
        }

        // how often this character already died the way it just did
        private static int CauseDeaths(PlayerProfile prof, string cause)
        {
            switch (cause)
            {
                case "creature": return Get(prof, PlayerStatType.DeathByEnemyHit);
                case "Fall": return Get(prof, PlayerStatType.DeathByFall);
                case "Drowning": case "Water": return Get(prof, PlayerStatType.DeathByDrowning) + Get(prof, PlayerStatType.DeathByWater);
                case "Burning": case "CinderFire": case "AshlandsLava": case "AshlandsOcean":
                    return Get(prof, PlayerStatType.DeathByBurning) + Get(prof, PlayerStatType.DeathByCinderFire)
                        + Get(prof, PlayerStatType.DeathByAshlandsLava) + Get(prof, PlayerStatType.DeathByAshlandsOcean);
                case "Poisoned": return Get(prof, PlayerStatType.DeathByPoisoned);
                case "Freezing": return Get(prof, PlayerStatType.DeathByFreezing);
                case "Tree": return Get(prof, PlayerStatType.DeathByTree);
                default: return 0;
            }
        }

        private static void Put(Dictionary<string, string> v, string key, int n, bool times)
        {
            if (n > 0) v[key] = times ? Times(n) : Num(n);
        }

        private static void Km(Dictionary<string, string> v, string key, float meters)
        {
            if (meters >= 100f) v[key] = Lang.F("{0} км", Lang.Num(meters / 1000f, "0.0"));
        }

        // 1 514 rather than 1514
        private static string Num(int n)
        {
            return n.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");
        }

        private static string Count(int n, string forms) { return Num(n) + " " + Lang.P(n, forms); } // forms "one|few|many"

        private static string Times(int n) { return Count(n, Lang.T("раз|рази|разів")); }
    }
}

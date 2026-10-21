namespace HuginWhispers
{
    // The raven's tone follows its trust. A phrase line (a whole line, a template or a part) may start with ~ and the
    // tiers it suits: ~1 levels 0–3 (still a stranger: Hugin sharp, Munin wild), ~2 levels 4–5, ~3 levels 6–7
    // (softer: Hugin still teases, Munin as rude and funny but fond); ~12 / ~23 for two tiers. A line without the
    // mark suits every tier, so a file can mix them freely. A raven that is not worn speaks as a stranger (tier 1).
    internal static class Tone
    {
        internal static int Tier = 1; // set before each line is put together
        internal static bool Own;    // the line is your own raven's (Tone.Use), not a perch's or a talk with another player's

        internal static int TierOf(int level) { return level >= 6 ? 3 : level >= 4 ? 2 : 1; }

        // the tier of the raven of this kind on your shoulder
        internal static void Use(bool munin)
        {
            Own = true;
#if RAVEN_ITEM
            Tier = Trust.Hostile(Trust.Worn(munin)) ? 1 : TierOf(Trust.LevelWorn(munin)); // a stranger hears only the harshest
            Manner.Use(Voice.Code(Trust.Worn(munin)), munin, !Trust.Hostile(Trust.Worn(munin))); // and this raven's own manner (a stranger's raven has no kind word for you)
#else
            Tier = 1;
#endif
        }

        // false when the line is marked for other tiers; otherwise the mark is taken off
        internal static bool Take(ref string line)
        {
            if (line == null) return false;
            string t = line.TrimStart();
            if (t.Length < 2 || t[0] != '~') return true;
            int i = 1;
            bool fits = false;
            while (i < t.Length && char.IsDigit(t[i])) { if (t[i] - '0' == Tier) fits = true; i++; }
            if (i == 1) return true; // a ~ not followed by a tier: just text
            if (!fits) return false;
            line = t.Substring(i).TrimStart();
            return true;
        }
    }
}

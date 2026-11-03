#if RAVEN_ITEM && RAVEN_NPC
using UnityEngine;

namespace HuginWhispers
{
    // A settler's story (npc-plan.md, «передісторія»), within the story's world (story-lore.md): the chosen come to
    // Valheim with no memory of their life before — so it is not a life told, but what is left of one, and what this world
    // did to it since. Put together from parts like its words (NpcPhrases, themes story_*), in five pieces:
    //   story_came     how long ago a valkyrie left it here, and its first day (by its age)
    //   story_memory   a scrap of the life before: a smell, a song, what the hands remember
    //   story_life     how it lived here: alone, with a band that faded one by one, the empty beds
    //   story_mark     a fear, a habit — now and then the whisper that offers peace for forgetting (Hel's, unsaid)
    //   + some of: story_dream (a dream that comes back) · story_friend (someone it knew here) · story_thing (what it
    //     carries) · story_skill (what it can do) · story_scar · story_ravens (what it thinks of ravens) · story_wish —
    //     one for a quiet person, three for a talkative one
    //   story_trouble_<kind>   how it came to need you (its rescue's kind)
    // Every part is marked for whom it fits (~мол ~дор ~стар its age, ~смілив ~боязк its bravery, ~бурч ~весел its ways), so
    // an old one is never «new here» and a timid one never went for trolls alone. Drawn steadily: seeded by the person and
    // the piece — the same story every time and on every game, nothing sent; only its trouble's kind is kept (KStory).
    // A story person's own story is its template's «story:» line, never made up.
    internal static class NpcBackstory
    {
        internal static readonly int KStory = "RavenWatch_npcStory".GetStableHashCode(); // string: «made» and its trouble's kind

        // once (where it is owned): the kind of trouble it was found in (its look first: its age)
        internal static void Ensure(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned || a.Def == null || !a.Def.Generated || z.GetString(KStory, "").Length > 0 || NpcSettlers.Of(a) == null) return;
            z.Set(KStory, "made;" + z.GetString(NpcRescue.KKind, ""));
        }

        // what its story says of it that may change what it does (^боїться_води: it washes in a tub, never at the shore)
        internal static bool Fact(NpcActor a, string fact)
        {
            if (a == null || a.Zdo == null) return false;
            string kept = a.Zdo.GetString(KStory, "");
            if (kept.Length == 0) return false;
            if (a.StoryFacts == null || a.StoryFactsOf != kept) Text(a, true); // (told once to know it: kept on the person)
            return a.StoryFacts != null && a.StoryFacts.Contains(fact);
        }

        // a knack its story gives (^knack_<key>_<percent> on what it told): a share, 0.25 for +25%
        internal static float Knack(NpcActor a, string key)
        {
            if (a == null || a.Def == null || !a.Def.Generated || a.Zdo == null) return 0f;
            string kept = a.Zdo.GetString(KStory, "");
            if (kept.Length == 0) return 0f;
            if (a.StoryFacts == null || a.StoryFactsOf != kept) Text(a, true);
            if (a.StoryFacts == null) return 0f;
            float sum = 0f;
            string start = "knack_" + key + "_";
            foreach (string f in a.StoryFacts) { int n; if (f.StartsWith(start, System.StringComparison.Ordinal) && int.TryParse(f.Substring(start.Length), out n)) sum += n / 100f; }
            return sum;
        }

        internal static bool Has(NpcActor a) { return a != null && a.Def != null && (!string.IsNullOrEmpty(a.Def.Story) || (a.Zdo != null && a.Zdo.GetString(KStory, "").Length > 0)); }

        // short: how it came and a scrap of the life before; full: all of it
        internal static string Text(NpcActor a, bool full) { return Text(a, full, false); }

        // what it tells after the short one («Розкажи більше»): the rest only, never the start again
        internal static string Rest(NpcActor a) { return Text(a, true, true); }

        private static string Text(NpcActor a, bool full, bool restOnly)
        {
            if (a == null || a.Def == null) return "";
            if (!string.IsNullOrEmpty(a.Def.Story)) return restOnly ? "" : a.Form(Lang.T(a.Def.Story)); // (a story person's own, written: told whole at once)
            ZDO z = a.Zdo;
            string kept = z != null ? z.GetString(KStory, "") : "";
            if (kept.Length == 0) return "";
            int seed = (int)(z.m_uid.ID * 2246822519u) ^ (int)z.m_uid.UserID;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            NpcPhrases.StoryBegin(); // (one thing told once: ^marks — NpcPhrases)
            try {
            Piece(sb, a, "story_came", seed);
            Piece(sb, a, "story_memory", seed);
            if (restOnly) sb.Length = 0; // (told already: drawn only so its ^marks count the same)
            if (full)
            {
                // a few of the pieces not everyone has: as many as its chattiness tells (the quiet one, one; the talkative, three)
                float chatty = NpcPersona.Of(a).Chatty;
                int extra = chatty < 0.35f ? 1 : chatty > 0.65f ? 3 : 2;
                System.Collections.Generic.List<string> some = new System.Collections.Generic.List<string>(Extras);
                System.Random r = new System.Random(seed);
                while (some.Count > extra) some.RemoveAt(r.Next(some.Count));
                if (some.Contains("story_dream")) Piece(sb, a, "story_dream", seed);
                Piece(sb, a, "story_life", seed);
                foreach (string t in new[] { "story_friend", "story_thing", "story_skill", "story_scar" }) if (some.Contains(t)) Piece(sb, a, t, seed);
                Piece(sb, a, "story_mark", seed);
                foreach (string t in new[] { "story_ravens", "story_wish" }) if (some.Contains(t)) Piece(sb, a, t, seed);
                int semi = kept.IndexOf(';');
                string kind = semi >= 0 ? kept.Substring(semi + 1) : "";
                if (kind.Length > 0) Piece(sb, a, "story_trouble_" + kind, seed);
            }
            }
            finally { System.Collections.Generic.HashSet<string> told = NpcPhrases.StoryEnd(); if (full) { a.StoryFacts = told; a.StoryFactsOf = kept; } }
            return sb.ToString().Trim();
        }

        // the pieces not everyone tells (in the order they come in the story)
        private static readonly string[] Extras = { "story_dream", "story_friend", "story_thing", "story_skill", "story_scar", "story_ravens", "story_wish" };

        private static void Piece(System.Text.StringBuilder sb, NpcActor a, string theme, int seed)
        {
            string s = NpcPhrases.ComposeSteady(a, theme, seed ^ theme.GetStableHashCode());
            if (!string.IsNullOrEmpty(s)) sb.Append(s).Append(' ');
        }
    }
}
#endif

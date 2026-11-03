#if RAVEN_ITEM && RAVEN_NPC
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own ways (npc-plan.md: individuality): a few traits every other part reads, so no two people are alike.
    // Each trait comes from its template when written there, else from its id — the same every time, on every game (no
    // message needed): two people from one template still differ.
    //   walk      its stroll (×, the template's walk: and speed: on top)        0.8–1.2
    //   tempo     its legs' rhythm while walking (the animation's pace ×)     0.85–1.15
    //   stoop     how bent it walks and stands (0 straight … 1 hunched)        0–0.35
    //   height    its size (×)                                               0.95–1.05
    //   diligence how steadily it works (its steps quicker, breaths shorter)  0.2–0.9
    //   laziness  how often a whim takes it off its work                     0.1–0.8
    //   curiosity how far round it notices people and looks at them           0.2–0.9
    //   chrono    its day shifted (hours: − an early bird, + a night owl)    −1.5…+1.5
    //   chatty    how readily it greets and talks unasked                     0.2–0.9
    //   bravery   how late it runs from a foe / how near it lets one come     0.2–0.9
    // In a template: «persona: stoop=0.8 tempo=0.8 chatty=0.9» (any of them; the rest from its id).
    internal sealed class NpcPersona
    {
        public float Walk, Tempo, Stoop, Height, Diligence, Laziness, Curiosity, Chrono, Chatty, Bravery;

        // the ways of this person (made once per body and template: kept on the person itself, gone with it)
        internal static NpcPersona Of(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || a.Def == null) return Average;
            string story = (NpcSettlers.Ways(a) + " " + NpcLevers.Traits(a)).Trim(); // (a settler's age, then the story's changes, over its template's: NpcSettlers, NpcLevers «trait»)
            if (a.PersonaKept != null && a.PersonaFor == a.Def && a.PersonaStory == story) return a.PersonaKept;
            a.PersonaKept = Make(z.m_uid, story.Length > 0 ? a.Def.Persona + " " + story : a.Def.Persona); a.PersonaFor = a.Def; a.PersonaStory = story;
            return a.PersonaKept;
        }

        internal static readonly NpcPersona Average = new NpcPersona { Walk = 1f, Tempo = 1f, Stoop = 0f, Height = 1f, Diligence = 0.55f, Laziness = 0.4f, Curiosity = 0.5f, Chrono = 0f, Chatty = 0.5f, Bravery = 0.5f };

        private static NpcPersona Make(ZDOID id, string written)
        {
            uint seed = (uint)(id.ID * 2654435761u) ^ (uint)id.UserID;
            NpcPersona p = new NpcPersona
            {
                Walk = Roll(ref seed, 0.8f, 1.2f), Tempo = Roll(ref seed, 0.85f, 1.15f), Stoop = Roll(ref seed, 0f, 0.35f), Height = Roll(ref seed, 0.95f, 1.05f),
                Diligence = Roll(ref seed, 0.2f, 0.9f), Laziness = Roll(ref seed, 0.1f, 0.8f), Curiosity = Roll(ref seed, 0.2f, 0.9f),
                Chrono = Roll(ref seed, -1.5f, 1.5f), Chatty = Roll(ref seed, 0.2f, 0.9f), Bravery = Roll(ref seed, 0.2f, 0.9f)
            };
            if (!string.IsNullOrEmpty(written))
                foreach (string pair in written.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = pair.IndexOf('=');
                    float v;
                    if (eq <= 0 || !float.TryParse(pair.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    switch (pair.Substring(0, eq).ToLowerInvariant())
                    {
                        case "walk": p.Walk = Mathf.Clamp(v, 0.4f, 2f); break;
                        case "tempo": p.Tempo = Mathf.Clamp(v, 0.5f, 1.6f); break;
                        case "stoop": p.Stoop = Mathf.Clamp01(v); break;
                        case "height": p.Height = Mathf.Clamp(v, 0.85f, 1.15f); break;
                        case "diligence": p.Diligence = Mathf.Clamp01(v); break;
                        case "laziness": p.Laziness = Mathf.Clamp01(v); break;
                        case "curiosity": p.Curiosity = Mathf.Clamp01(v); break;
                        case "chrono": p.Chrono = Mathf.Clamp(v, -4f, 4f); break;
                        case "chatty": p.Chatty = Mathf.Clamp01(v); break;
                        case "bravery": p.Bravery = Mathf.Clamp01(v); break;
                    }
                }
            return p;
        }

        // the next of its own numbers (the same row every time for one id)
        private static float Roll(ref uint seed, float lo, float hi)
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return lo + (hi - lo) * ((seed % 10000u) / 10000f);
        }

        // a trait by its word (the story's levers)
        internal float Get(string name)
        {
            switch (name)
            {
                case "walk": return Walk; case "tempo": return Tempo; case "stoop": return Stoop; case "height": return Height;
                case "diligence": return Diligence; case "laziness": return Laziness; case "curiosity": return Curiosity;
                case "chrono": return Chrono; case "chatty": return Chatty; case "bravery": return Bravery;
            }
            return 0f;
        }

        // ---------------------------------------------------------------- what the other parts read
        internal float WorkStep { get { return Mathf.Lerp(1.3f, 0.6f, Diligence); } }    // × its step at work (the worker)
        internal float WorkBreath { get { return Mathf.Lerp(1.8f, 0.5f, Diligence); } }  // × its breath between errands
        internal float WhimChance { get { return Mathf.Lerp(0.15f, 0.6f, Laziness); } }   // a whim on a try (NpcWhims)
        internal float WhimEvery { get { return Mathf.Lerp(1.6f, 0.6f, Laziness); } }     // × the time between tries
        internal float Notice { get { return Mathf.Lerp(3f, 10f, Curiosity); } }          // m: a player this near, it looks at them
        internal float Greets { get { return Mathf.Lerp(0.25f, 1f, Chatty); } }           // its «near» habit (a greeting): how likely
        internal float FleeAt { get { return Mathf.Lerp(14f, 6f, Bravery); } }            // m: a foe this near, a peaceful one runs
        internal float CornerAt { get { return Mathf.Lerp(1.8f, 3.5f, Bravery); } }       // m: a wary one turns on a foe this near
        internal float RetreatAt { get { return Mathf.Lerp(0.4f, 0.12f, Bravery); } }     // a fighter's health share it falls back at
    }
}
#endif

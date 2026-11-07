#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own ways (npc-plan.md: individuality): a few traits every other part reads, so no two people are alike.
    // Each trait comes from its template when written there, else from its id — the same every time, on every game (no
    // message needed): two people from one template still differ.
    //   walk      its stroll (×, the template's walk: and speed: on top)        0.8–1.2
    //   stoop     how bent it walks and stands (0 straight … 1 hunched)        0–0.35
    //   height    its size (×)                                               0.9–1.1 (written 0.8–1.25;
    //             over 1 it only looks taller: its capsule stays a person's, NpcBody.Fit)
    //   build     its build: thin −1 … heavy +1, body and cloth (BodyBuild)   −0.25–0.25
    //   diligence how steadily it works (its steps quicker, breaths shorter)  0.2–0.9
    //   laziness  how often a whim takes it off its work                     0.1–0.8
    //   curiosity how far round it notices people and looks at them           0.2–0.9
    //   chrono    its day shifted (hours: − an early bird, + a night owl)    −1.5…+1.5
    //   chatty    how readily it greets and talks unasked                     0.2–0.9
    //   bravery   how late it runs from a foe / how near it lets one come     0.2–0.9
    // Over these a settler's nature (NpcNature: from its seed, its ranges over the rolled ones; «persona: nature=<id>» says which): written traits still win.
    // A settler's body (Kind): heavy one in ten (build 0.5–0.9; the young half as often), tall and lean one in ten
    // (height 1.1–1.18, build −0.65…−0.35), short and stocky one in twenty (height 0.88–0.94, build 0.3–0.55), the rest
    // as above; by its age: the young a little leaner, the old a little lower. Its body in a word: BodyWord (its card).
    // People written by hand: as above, their height and build set in «Вигляд».
    // In a template: «persona: stoop=0.8 chatty=0.9» (any of them; the rest from its id).
    internal sealed class NpcPersona
    {
        public float Walk, Stoop, Height, Build, Diligence, Laziness, Curiosity, Chrono, Chatty, Bravery;
        public NpcNature Nature; // (a settler's kind of person: NpcNature; one written by hand: none, unless written)
        internal Dictionary<string, string> Refusals; internal int RefusalsAt; // (NpcNature.WontTake: worked out once for these ways and this language)

        // the ways of this person (made once per body and template: kept on the person itself, gone with it)
        internal static NpcPersona Of(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || a.Def == null) return Average;
            uint own = NpcKit.Seed(z); // (its lasting number: the same person after a restart)
            string lookRaw = a.Def.Generated ? z.GetString(NpcSettlers.KLook, "") : "", traitsRaw = NpcLevers.Traits(a); // (what its ways are made of, as kept: compared, not built — this is asked many times a second)
            if (a.PersonaKept != null && a.PersonaFor == a.Def && a.PersonaSeed == own && (object)a.PersonaWritten == a.Def.Persona && a.PersonaLookRaw == lookRaw && a.PersonaTraitsRaw == traitsRaw) return a.PersonaKept; // (its template's line changed in the window «Вигляд»: made again)
            string story = (NpcSettlers.Ways(a) + " " + traitsRaw).Trim(); // (a settler's age, then the story's changes, over its template's: NpcSettlers, NpcLevers «trait»)
            a.PersonaLookRaw = lookRaw; a.PersonaTraitsRaw = traitsRaw;
            NpcSettlers.Look look = NpcSettlers.Of(a); // (a settler's age: its body by it — the look comes in its ZDO, and with it its Ways: made again then)
            a.PersonaKept = Make(own, story.Length > 0 ? a.Def.Persona + " " + story : a.Def.Persona, a.Def.Generated, look != null ? look.Age : -1); a.PersonaFor = a.Def; a.PersonaStory = story; a.PersonaSeed = own; a.PersonaWritten = a.Def.Persona;
            return a.PersonaKept;
        }

        // a template's person with no body (the look window's picture): seeded as one written by hand is in the world
        // (NpcKit.Born) — the same height and build as there; a settler's template: one of them
        internal static NpcPersona OfTemplate(NpcDef d)
        {
            if (d == null) return Average;
            uint seed = (uint)d.Id.GetStableHashCode();
            return Make(seed != 0u ? seed : 1u, d.Persona, d.Generated, -1);
        }

        internal static readonly NpcPersona Average = new NpcPersona { Walk = 1f, Stoop = 0f, Height = 1f, Build = 0f, Diligence = 0.55f, Laziness = 0.4f, Curiosity = 0.5f, Chrono = 0f, Chatty = 0.5f, Bravery = 0.5f };

        private static NpcPersona Make(uint own, string written, bool kinds, int age)
        {
            uint seed = own * 2654435761u ^ 0x9e3779b9u; if (seed == 0u) seed = 1u; // (xorshift: never 0)
            NpcPersona p = new NpcPersona
            {
                Walk = Roll(ref seed, 0.8f, 1.2f), Stoop = Roll(ref seed, 0f, 0.35f), Height = Roll(ref seed, 0.9f, 1.1f),
                Diligence = Roll(ref seed, 0.2f, 0.9f), Laziness = Roll(ref seed, 0.1f, 0.8f), Curiosity = Roll(ref seed, 0.2f, 0.9f),
                Chrono = Roll(ref seed, -1.5f, 1.5f), Chatty = Roll(ref seed, 0.2f, 0.9f), Bravery = Roll(ref seed, 0.2f, 0.9f),
                Build = Roll(ref seed, -0.25f, 0.25f) // (the last of the row: the traits before it keep their numbers)
            };
            if (kinds) Kind(ref seed, p, age);
            p.Nature = kinds ? NpcNature.Born(own) : null; // (apart from the row above: its body stays what it rolled)
            if (!string.IsNullOrEmpty(written)) foreach (string pair in written.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries)) if (pair.StartsWith("nature=", System.StringComparison.Ordinal)) { NpcNature n = NpcNature.Find(pair.Substring(7)); if (n != null) p.Nature = n; }
            if (p.Nature != null) p.Nature.Shape(own, p);
            if (!string.IsNullOrEmpty(written))
                foreach (string pair in written.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = pair.IndexOf('=');
                    float v;
                    if (eq <= 0 || !float.TryParse(pair.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    p.Put(pair.Substring(0, eq).ToLowerInvariant(), v);
                }
            return p;
        }

        // one trait by its word, within its limits (written, or a nature's)
        internal void Put(string name, float v)
        {
            switch (name)
            {
                case "walk": Walk = Mathf.Clamp(v, 0.4f, 2f); break;
                case "stoop": Stoop = Mathf.Clamp01(v); break;
                case "height": Height = Mathf.Clamp(v, 0.8f, 1.25f); break;
                case "build": Build = Mathf.Clamp(v, -1f, 1f); break;
                case "diligence": Diligence = Mathf.Clamp01(v); break;
                case "laziness": Laziness = Mathf.Clamp01(v); break;
                case "curiosity": Curiosity = Mathf.Clamp01(v); break;
                case "chrono": Chrono = Mathf.Clamp(v, -4f, 4f); break;
                case "chatty": Chatty = Mathf.Clamp01(v); break;
                case "bravery": Bravery = Mathf.Clamp01(v); break;
            }
        }

        // a settler's body now and then not an ordinary one (the user 2026-11-03: «повними не більше ніж 10%», the same for
        // tall and lean): its kind from its own number, its measures within the kind. A written height or build still wins
        internal static float HeavyShare = 0.1f, LeanShare = 0.1f, StockyShare = 0.05f;
        private static void Kind(ref uint seed, NpcPersona p, int age) // (age: 0 young · 1 grown · 2 old · −1 not known)
        {
            float heavy = age == 0 ? HeavyShare * 0.5f : HeavyShare, t = Roll(ref seed, 0f, 1f);
            if (t < heavy) { p.Build = Roll(ref seed, 0.5f, 0.9f); p.Height = Roll(ref seed, 0.93f, 1.07f); }
            else if (t < heavy + LeanShare) { p.Height = Roll(ref seed, 1.1f, 1.18f); p.Build = Roll(ref seed, -0.65f, -0.35f); }
            else if (t < heavy + LeanShare + StockyShare) { p.Height = Roll(ref seed, 0.88f, 0.94f); p.Build = Roll(ref seed, 0.3f, 0.55f); }
            if (age == 0) p.Build = Mathf.Max(-1f, p.Build - 0.1f); // (the young a little leaner)
            if (age == 2) p.Height *= 0.97f;                         // (the old a little lower)
        }

        // its body in a word or two (its card, NpcProfile; «a|b»: his and her forms, NpcActor.Form). A kind first, else
        // its height and its build each when not middling
        internal static string BodyWord(NpcPersona p)
        {
            if (p.Build >= 0.45f) return Lang.T("огрядний|огрядна") + (p.Height >= 1.06f ? Lang.T(", високий|висока") : "");
            if (p.Height >= 1.09f && p.Build <= -0.25f) return Lang.T("високий|висока і худорлявий|худорлява");
            if (p.Height <= 0.95f && p.Build >= 0.25f) return Lang.T("низький|низька і кремезний|кремезна");
            string h = p.Height <= 0.94f ? Lang.T("невисокий|невисока") : p.Height >= 1.06f ? Lang.T("високий|висока") : Lang.T("середнього зросту");
            string b = p.Build <= -0.35f ? Lang.T("худорлявий|худорлява") : p.Build >= 0.3f ? Lang.T("щільний|щільна") : "";
            return b.Length > 0 ? h + ", " + b : h;
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
                case "walk": return Walk; case "stoop": return Stoop; case "height": return Height; case "build": return Build;
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

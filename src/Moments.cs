#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Your own small moves by the moment (the user 2026-11-18: «нові анімації контекстні… гравець грає не все, а лише те, що ми йому дамо…
    // глобально правила всім не змінюй»). A clip says itself that the player makes it of himself and when: «hero idle», «hero rain cold» in its
    // file — a new one is a file with that line, no code. A clip with no such line he never makes of himself; people's moves are untouched by it
    // (their gate is NpcMoves: «tone», «shuns:», their places and whims).
    //   the words  idle — standing calm, whatever the weather · rain — it rains on you (not under a roof, not under the ground) ·
    //              cold — you are cold or freezing (the game's own state) · night — night, under the open sky
    //   when       standing calm (Pet's own look: on your feet, nothing in the air, no window, no scene) StillAfter s, then one move every
    //              EveryMin..EveryMax s — a random while, nothing by the clock. A moment that holds now (rain, cold, night) is Special times as
    //              likely as an «idle» one; never the same move twice running while there is another
    //   how        through the gestures' director (Gestures, Ambient: the lightest — anything else cuts it, a quiet while after the last gesture):
    //              others see it by your data, as every gesture of yours
    // Nothing per frame: asked from Pet's look four times a second, a compare of two numbers; the list of such clips is made when the clips
    // change (BodyClips.Changes), the pick is a walk over it at the moment of a move.
    internal static class Moments
    {
        internal static readonly string[] Words = { "idle", "rain", "cold", "night" }; // (tools/clipcheck.js keeps the same words)
        internal static float StillAfter = 40f, EveryMin = 80f, EveryMax = 200f, Special = 3f;

        private static readonly List<PAnimClip> Ours = new List<PAnimClip>();
        private static int _made = -1;
        private static float _next = -1f;
        private static string _last = "";

        // you moved (or are busy): the wait starts over
        internal static void Moved() { _next = -1f; }

        // you have stood calm this long (Pet.Tick, four times a second)
        internal static void Still(Player p, float seconds)
        {
            if (seconds < StillAfter) return;
            if (_next < 0f) { _next = Time.time + Random.Range(0f, EveryMin * 0.5f); return; }
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(EveryMin, EveryMax);
            PAnimClip c = Pick(p);
            if (c == null) return;
            if (Gestures.Play(p, false, c.Name, Gestures.Pri.Ambient, false)) _last = c.Name; // (refused — another gesture, a quiet while: the next one in its time, no try again at once)
        }

        internal static bool Holds(string word, Player p)
        {
            switch (word)
            {
                case "idle": return true;
                case "rain": return EnvMan.instance != null && EnvMan.IsWet() && !Senses.Indoors && !Senses.Underground;
                case "cold": { SEMan se = p.GetSEMan(); return se != null && (se.HaveStatusEffect(SEMan.s_statusEffectCold) || se.HaveStatusEffect(SEMan.s_statusEffectFreezing)); }
                case "night": return EnvMan.instance != null && EnvMan.IsNight() && !Senses.Indoors && !Senses.Underground;
            }
            return false;
        }

        // the clips the player makes of himself (parts of clips never: a part is its clip's)
        private static List<PAnimClip> List()
        {
            if (_made == BodyClips.Changes) return Ours;
            _made = BodyClips.Changes;
            Ours.Clear();
            foreach (PAnimClip c in BodyClips.Clips.Values) if (c.Hero.Length > 0 && !c.IsPart) Ours.Add(c);
            return Ours;
        }

        private static readonly List<PAnimClip> _fit = new List<PAnimClip>();
        private static readonly List<float> _worth = new List<float>();
        private static PAnimClip Pick(Player p)
        {
            _fit.Clear(); _worth.Clear();
            float sum = 0f;
            foreach (PAnimClip c in List())
            {
                float worth = 0f;
                foreach (string w in c.Hero) if (Holds(w, p)) worth = Mathf.Max(worth, w == "idle" ? 1f : Special);
                if (worth <= 0f) continue;
                _fit.Add(c); _worth.Add(worth); sum += worth;
            }
            if (_fit.Count == 0) return null;
            if (_fit.Count > 1) for (int i = 0; i < _fit.Count; i++) if (_fit[i].Name == _last) { sum -= _worth[i]; _worth[i] = 0f; } // (not the same twice running)
            float roll = Random.value * sum;
            for (int i = 0; i < _fit.Count; i++) { if (_worth[i] <= 0f) continue; roll -= _worth[i]; if (roll <= 0f) return _fit[i]; }
            return null;
        }

        // for the console (raven moment): what holds now, which clips may play, when the next one is
        internal static string Said(Player p)
        {
            List<string> holds = new List<string>(), names = new List<string>();
            foreach (string w in Words) if (Holds(w, p)) holds.Add(w);
            foreach (PAnimClip c in List()) names.Add(c.Name + " (" + string.Join(" ", c.Hero) + ")");
            return "now: " + string.Join(", ", holds.ToArray()) + " · clips: " + (names.Count > 0 ? string.Join(", ", names.ToArray()) : "none") + " · next " + (_next < 0f ? "not standing calm long enough" : "in " + Mathf.Max(0, Mathf.RoundToInt(_next - Time.time)) + " s");
        }

        // … and one now, whatever the wait (the author's look at it)
        internal static string Now(Player p)
        {
            PAnimClip c = Pick(p);
            if (c == null) return "no clip fits now";
            bool ok = Gestures.Play(p, false, c.Name, Gestures.Pri.Answer, false);
            if (ok) _last = c.Name;
            return ok ? "plays " + c.Name : c.Name + ": not now (your body or the gesture layer is busy)";
        }
    }
}
#endif

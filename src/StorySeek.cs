#if RAVEN_ITEM && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // A search of the story (the stage's word «seek: <place> <words>», the user 2026-11-14: Torvald gone — «буде зона велика і в цій зоні ворони кажуть
    // ближче або далі як при пошуку скарбів, тільки фрази під цей квест інші»). While this player's stage says so, the raven on his shoulder is the
    // finder of that place (HotCold, the same as its own cache): it caws within CawFrom m, and within Near m says warmer or colder in the words of
    // that search — the tags «seek_<words>_warmer», «seek_<words>_colder» (SeekPatterns); words with no tags of their own: the cache's
    // req_warmer / req_colder. The stage's ring on the map stands off the place (StoryQuests.SeekOff). A scene playing, inside, underground: quiet.
    // No raven of his awake on the shoulder (lost with a death, sold, asleep — the user 2026-11-15: «нехай мітка зменшується, але лише коли ворона нема на плечі»): the
    // ring narrows instead — each time he comes within Inside of its radius of the place, the next of Rings (never wider again in this search; RingOf
    // tells StoryQuests.MarkOne). And while a search is on, people within its ring of the place get no mark on the map (Hides → NpcMap.Hidden: the
    // user the same day — Torvald's icon showed where he hid).
    // Nothing kept: a finder's timers and the ring's step (this game's run only). Cost: twice a second, a pass over the active quests (a few); while a
    // search is on NpcMap asks Hides of each person twice a second — one distance.
    internal static class StorySeek
    {
        internal static float CawFrom = 110f, Near = 80f;
        internal static readonly float[] Rings = { 1f, 0.66f, 0.4f, 0.22f };
        internal static float Inside = 0.55f, HideLeast = 20f;
        private static readonly HotCold _finder = new HotCold();
        private static string _for = "", _ringFor = "";
        private static int _ring;
        private static bool _on;
        private static Vector3 _spot;
        private static float _hideR;

        // the ring's share of the stage's radius for a search of this place (1: not narrowed)
        internal static float RingOf(string place) { return _ringFor.StartsWith(place + "|", System.StringComparison.Ordinal) ? Rings[_ring] : 1f; }
        // within the search's ring of the place (the one sought, or anyone by him): no mark on the map
        internal static bool Hides(Vector3 at) { return _on && Utils.DistanceXZ(at, _spot) < _hideR; }

        // twice a second (StoryQuests.Each)
        internal static void Look(Player p)
        {
            string place, words; float radius;
            bool now = Now(p, out place, out words, out radius);
            Vector3 at = Vector3.zero;
            _on = now && StoryWhere.Find(place, out at);
            if (_on) { _spot = at; _hideR = Mathf.Max(radius, HideLeast); }
            if (!now) _ringFor = "";
            else if (_ringFor != place + "|" + words) { _ringFor = place + "|" + words; _ring = 0; }
            if (StoryScenes.Playing.Length > 0 || !_on || p.InInterior() || Senses.Underground) { _finder.Reset(); _for = ""; return; } // (inside, under the ground: quiet — the crypt hangs over its barrow, and there the raven cawed «hot» on and on: the user 2026-11-15)
            if (place + words != _for) { _finder.Reset(); _for = place + words; }
            bool munin = !Shoulder.Present(p, false) && (Shoulder.Present(p, true) || Shoulder.ModelOf(p, false) == null); // (the one on the shoulder — Hugin first; none there: the one out near you)
            ItemDrop.ItemData raven = Trust.Worn(munin);
            float d = Utils.DistanceXZ(p.transform.position, at);
            if (Shoulder.ModelOf(p, munin) == null || raven == null || !Trust.Active(raven) || Trust.Stranger(raven, p)) // (another's raven worn does not look for yours: its words would be a stranger's; one asleep finds nothing — as it reads no runes: the condition «raven … awake»)
            {
                if (radius > 0f && _ring < Rings.Length - 1 && d < radius * Rings[_ring] * Inside) _ring++; // (no raven of his: the ring narrows as he comes)
                return;
            }
            if (d > CawFrom) { _finder.Reset(); return; }
            HotCold.Hold(); // (within its ring the story's search is the one finder: the cache's waits)
            _finder.Caw(p, munin, d, CawFrom);
            if (d > Near) { _finder.Reset(); return; }
            int word = _finder.Word(d);
            if (word == 0 || !Shoulder.Present(p, munin)) return;
            string line = Say(p, munin, "seek_" + words + (word > 0 ? "_warmer" : "_colder"));
            if (line.Length == 0) line = Say(p, munin, word > 0 ? "req_warmer" : "req_colder");
            if (line.Length > 0) RavenSkills.SayFromShoulder(p, munin, line);
        }

        // this player's stage that searches now: its place and the words of the search
        private static bool Now(Player p, out string place, out string words, out float radius)
        {
            place = words = ""; radius = 0f;
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s == null || s.Seek.Length == 0) continue;
                string[] w = Words(s.Seek);
                if (w.Length < 2) continue;
                place = w[0]; words = w[1].ToLowerInvariant();
                radius = s.MarkAt == place ? s.MarkRadius : 0f; // (the ring the stage draws round it; a mark elsewhere — nothing to narrow)
                return true;
            }
            return false;
        }

        // a stage's «seek:» in its words: the place, the words of the search (one reading: StoryQuests.MarkOne asks the place here too)
        internal static string[] Words(string seek) { return (seek ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries); }

        private static string Say(Player p, bool munin, string tag)
        {
            if (Plugin.Instance == null) return "";
            Composer.Extra.Clear();
            try { return Plugin.Instance.ComposeTagged(p, munin, tag) ?? ""; }
            finally { Composer.Extra.Clear(); }
        }
    }
}
#endif

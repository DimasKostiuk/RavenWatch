#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The raven as a finder (the one way of it: its own cache — Requests; a search of the story — StorySeek, the stage's «seek:», the user 2026-11-14:
    // «як при пошуку скарбів тільки фрази під цей квест інші»). Two things, each asked twice a second by its owner with how far you are from the spot:
    //   Caw    within «from» m it caws from the shoulder, the faster the nearer (every 7 s far off, about twice a second on the spot)
    //   Word   within the near ring: once you have gone WordStep m nearer or farther since its last word — +1 warmer, −1 colder, 0 nothing yet
    //          (its first word waits FirstWord s, then never two in WordEvery s); the owner says it in its own words
    // A finder holds only its own timers (a session's: nothing to keep); Reset when you are out of its ring. Cheap: a few numbers a call.
    // One search at a time (the review of 2026-11-14: a raven's cache wish during the story's search cawed on two clocks for two spots): the story's
    // search holds the finder (Hold, renewed each look while you are within its ring); the cache's finder is quiet while Held.
    internal sealed class HotCold
    {
        internal static float WordStep = 7f, WordEvery = 12f, FirstWord = 8f;
        private float _cawAt, _wordAt, _wordDist = -1f;
        private static float _heldUntil;
        internal static void Hold() { _heldUntil = Time.time + 1.5f; } // (the story's search, each look: it lapses by itself a moment after the search ends)
        internal static bool Held { get { return Time.time < _heldUntil; } }

        internal void Reset() { _wordDist = -1f; }

        // the finder's caw (the raven with you: on the shoulder in any pose, or out flying near — its caw comes from you anyway). True: it cawed now
        internal bool Caw(Player p, bool munin, float d, float from)
        {
            if (d > from || Time.time < _cawAt) return false;
            float k = Mathf.Clamp01(d / from);
            _cawAt = Time.time + Mathf.Lerp(0.45f, 7f, k * k);
            ScreamFx.Caw(p, munin, (munin ? 0.9f : 1.25f) + (1f - k) * 0.15f, Mathf.Lerp(0.6f, 0.35f, k));
            return true;
        }

        // warmer (+1) or colder (−1) since its last word; 0 — nothing to say now
        internal int Word(float d)
        {
            if (Time.time < _wordAt) return 0;
            if (_wordDist < 0f) { _wordDist = d; _wordAt = Time.time + FirstWord; return 0; }
            float moved = _wordDist - d;
            if (Mathf.Abs(moved) < WordStep) return 0;
            _wordAt = Time.time + WordEvery;
            _wordDist = d;
            return moved > 0f ? 1 : -1;
        }
    }
}
#endif

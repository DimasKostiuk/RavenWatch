#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A raven's bath at the water's edge (WorldVisit «Bathe», Bath): on every game the same few seconds — crouched into
    // the water, its wings beating, the body shivering, the head dipping now and then, small splashes; at the end it
    // shakes itself off (the rain's «Обтрушується»). Started by the spot's count (SpotFollow → Bathe).
    internal partial class PerchLife
    {
        internal static float BathFlapDeg = 55f, BathFlapHz = 5.5f, BathCrouch = 12f, BathShiver = 3f, BathSplashEvery = 1.1f, BathDipEvery = 1.6f;
        private float _bathFrom = -1f, _bathLen, _nextSplash, _nextDip;

        internal void Bathe(float seconds)
        {
            _bathFrom = Time.time; _bathLen = Mathf.Max(2f, seconds);
            _nextSplash = Time.time + 0.2f; _nextDip = Time.time + 0.8f;
        }

        // 0..1: how far into the bath (in and out of it softly)
        private float BathW()
        {
            if (_bathFrom < 0f) return 0f;
            float t = Time.time - _bathFrom;
            return t >= _bathLen ? 0f : Mathf.Clamp01(Mathf.Min(t, _bathLen - t) / 0.5f);
        }

        private void BathTick()
        {
            if (_bathFrom < 0f) return;
            float now = Time.time;
            if (now - _bathFrom >= _bathLen)
            {
                _bathFrom = -1f;
                if (Composes) _sitDir.React(this, SitTemplates.ShakeOffName, 0.1f); // the water off it
                return;
            }
            if (now >= _nextSplash) { _nextSplash = now + BathSplashEvery * Random.Range(0.7f, 1.3f); WorldVisit.SplashSmall(transform.position, Random.value < 0.35f); }
            if (now >= _nextDip && _peckFrom < 0f) { _nextDip = now + BathDipEvery * Random.Range(0.7f, 1.4f); Peck(0.9f); } // the head under, and up
        }

        private void BathPose(Vector3 up, Vector3 fwd, Vector3 side)
        {
            float w = BathW();
            if (w <= 0f) return;
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * BathFlapHz * Mathf.PI * 2f);
            Wing(_wingL, _handL, BathFlapDeg * beat * w, up, fwd, 1f);
            Wing(_wingR, _handR, BathFlapDeg * beat * w, up, fwd, -1f);
            if (_spine != null) _spine.T.rotation = Quaternion.AngleAxis((BathCrouch + Mathf.Sin(Time.time * 13f) * BathShiver) * w, side) * _spine.T.rotation; // crouched into it, shivering
        }
    }
}
#endif

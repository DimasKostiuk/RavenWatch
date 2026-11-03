#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // A raven's bath at the water's edge (WorldVisit «Bathe», Bath): on every game the same few seconds — crouched into
    // the water, its wings beating, the body shivering, the head dipping now and then, small splashes; at the end it
    // shakes itself off (the rain's «Обтрушується»). Started by the spot's count (SpotFollow → Bathe).
    internal partial class PerchLife
    {
        internal static float BathFlapDeg = 55f, BathFlapHz = 5.5f, BathCrouch = 12f, BathShiver = 3f, BathSplashEvery = 1.55f, BathDipEvery = 1.6f, PaddleHz = 1.4f;
        // a paddling leg (the user, 2026-10-28): the knee a little forward, the bones below it kicking back, round and
        // round, not in and out: each joint its middle, its swing and its lag (s of a stroke), the toes spread on the push
        // and curled on the way back (studio: set PerchLife.PadKnee0 …)
        internal static float PadHip0 = 10f, PadHipA = 10f, PadKnee0 = -20f, PadKneeA = 14f, PadKneeLag = 0.15f, PadAnkle0 = 30f, PadAnkleA = 28f, PadAnkleLag = 0.3f, PadToes0 = 15f, PadToesA = 30f;
        private float _bathFrom = -1f, _bathLen, _nextSplash, _nextDip, _nextRing, _nextSpray;

        internal void Bathe(float seconds)
        {
            if (_bathFrom >= 0f && Time.time - _bathFrom < 1.5f) return; // (begun here already the moment it landed: the spot's count only catches up)
            _bathFrom = Time.time; _bathLen = Mathf.Max(2f, seconds);
            _nextSplash = Time.time + 0.2f; _nextDip = Time.time + 0.8f;
            WaveBegin(ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(transform.position) : -10000f); // (riding the waves, on the bottom in the shallows)
        }

        // 0..1: how far into the bath (in and out of it softly)
        private float BathW()
        {
            if (_bathFrom < 0f) return 0f;
            float t = Time.time - _bathFrom;
            return t >= _bathLen ? 0f : Mathf.Clamp01(Mathf.Min(t / 0.25f, (_bathLen - t) / 0.5f)); // (in quickly, out softly)
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
            if (now >= _nextRing || now >= _nextSpray)
            {
                Player me = Player.m_localPlayer;
                bool near = me != null && (me.transform.position - transform.position).sqrMagnitude < RavenFx.HearRange * RavenFx.HearRange;
                Vector3 face = WaterFace(WaterFx.FaceAt(transform.position));
                if (now >= _nextRing) { _nextRing = now + (_waveCalm ? Random.Range(1f, 1.6f) : Random.Range(0.5f, 0.85f)); if (near) WaterFx.Ring(face, _waveCalm ? Random.Range(0.5f, 0.7f) : Random.Range(0.8f, 1.1f)); } // the rings off it (in a tub: a few small ones)
                if (now >= _nextSpray) { _nextSpray = now + Random.Range(0.18f, 0.32f); if (near && !_waveCalm) WaterFx.Spray(face, Random.Range(3, 7), 0.8f); } // its wings throwing the water up (not in a tub: too much there)
            }
            if (now >= _nextSplash && !_waveCalm) { _nextSplash = now + BathSplashEvery * Random.Range(0.7f, 1.3f); WorldVisit.SplashSmall(transform.position, Random.value < 0.35f); }
            if (now >= _nextDip && _peckFrom < 0f) { _nextDip = now + BathDipEvery * Random.Range(0.7f, 1.4f); Peck(0.9f); } // the head under, and up
        }

        // its feet paddling under it, in turn, as a swimming bird's: from the bath's start until it flies off the water (the
        // bath over, it still paddles while it shakes and sets off — never a sudden stop), eased in and out
        private float _legsW, _paddleUntil = -1f;
        private void Legs()
        {
            if (Pose == null) return;
            if (_bathFrom >= 0f) _paddleUntil = Time.time + 3f;
            float want = Time.time < _paddleUntil ? 1f : 0f;
            _legsW = Mathf.MoveTowards(_legsW, want, Time.deltaTime * (want > _legsW ? 4f : 1.5f));
            if (_legsW <= 0f) { if (Pose.LegsFree > 0f) { Pose.LegsFree = 0f; System.Array.Clear(Pose.LegAdd, 0, 8); } return; }
            Pose.LegsFree = _legsW;
            Paddle(0, Time.time, _legsW);
            Paddle(4, Time.time + 0.5f / PaddleHz, _legsW); // (the other half a stroke behind)
        }

        // one leg's turns at this moment of its stroke (from: 0 left, 4 right)
        private void Paddle(int from, float t, float w)
        {
            float a = t * PaddleHz * Mathf.PI * 2f, lag = PaddleHz * Mathf.PI * 2f;
            Pose.LegAdd[from] = w * (PadHip0 + PadHipA * Mathf.Sin(a));
            Pose.LegAdd[from + 1] = w * (PadKnee0 + PadKneeA * Mathf.Sin(a - PadKneeLag * lag));
            Pose.LegAdd[from + 2] = w * (PadAnkle0 + PadAnkleA * Mathf.Sin(a - PadAnkleLag * lag));
            Pose.LegAdd[from + 3] = w * (PadToes0 + PadToesA * Mathf.Max(0f, -Mathf.Cos(a - PadAnkleLag * lag))); // (curled while the foot comes forward)
        }

        private void BathPose(Vector3 up, Vector3 fwd, Vector3 side)
        {
            float w = BathW();
            Legs();
            if (w <= 0f) return;
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * BathFlapHz * Mathf.PI * 2f);
            Wing(_wingL, _handL, BathFlapDeg * beat * w, up, fwd, 1f);
            Wing(_wingR, _handR, BathFlapDeg * beat * w, up, fwd, -1f);
            if (_spine != null) _spine.T.rotation = Quaternion.AngleAxis((BathCrouch + Mathf.Sin(Time.time * 13f) * BathShiver) * w, side) * _spine.T.rotation; // crouched into it, shivering
        }
    }
}
#endif

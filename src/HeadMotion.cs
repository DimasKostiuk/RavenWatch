using UnityEngine;

namespace HuginWhispers
{
    // A bird's head does not glide from one look to the next: it moves there and holds still, then moves again. Small
    // changes of where it looks (breathing, a slow drift, a mood) it follows slowly while it holds. Each raven its own
    // way: Munin, the wild one, jumps (quick, a touch past and back, short holds); Hugin, the thoughtful one, turns
    // (only for a real change of look, unhurried, soft at both ends, long holds). One per head: the shoulder's raven
    // (ShoulderLife) and the perch's (PerchLife).
    internal sealed class Saccade
    {
        internal sealed class Style
        {
            public float MinJump;    // ° the aim moved this far from the held look: a move
            public float Base;       // s a move takes: a base …
            public float PerDeg;     // … and a little for each degree
            public float Overshoot;  // its ease past the aim (0: none, soft at both ends; 0.9 ≈ 4% past and back)
            public float Hold;       // s it holds still after a move at least (×0.6..1.4 each time)
            public float Drift;      // s: a held look follows small changes this slowly
            public float NervesMin, NervesMax; // how far a nervous / a calm raven's pace goes (× the moves, ÷ the holds)
        }
        internal static readonly Style Munin = new Style { MinJump = 7f, Base = 0.07f, PerDeg = 0.0011f, Overshoot = 0.9f, Hold = 0.22f, Drift = 0.6f, NervesMin = 0.8f, NervesMax = 1.35f };
        internal static readonly Style Hugin = new Style { MinJump = 12f, Base = 0.17f, PerDeg = 0.0024f, Overshoot = 0f, Hold = 0.45f, Drift = 0.9f, NervesMin = 0.9f, NervesMax = 1.05f };
        internal static float MuninPace = 1.5f;    // Munin's moves quicker and holds shorter on top of his style
        internal static float BigJump = 35f;       // ° this far: at once, even while it still holds

        private Quaternion _from = Quaternion.identity, _to = Quaternion.identity, _now = Quaternion.identity;
        private float _t = -1f, _len = 0.1f, _holdUntil, _over;

        internal Quaternion Now { get { return _now; } }

        // the look this frame, aiming at 'want'; nerves: the raven's restlessness (ShoulderLife / PerchLife Nerves);
        // pace: × on top (a take-off's look off: a touch quicker)
        internal Quaternion Step(Quaternion want, float dt, bool munin, float nerves, float pace = 1f)
        {
            Style st = munin ? Munin : Hugin;
            float speed = Mathf.Max(0.2f, Mathf.Clamp(nerves, st.NervesMin, st.NervesMax) * (munin ? MuninPace : 1f) * pace);
            float d = Quaternion.Angle(want, _to);
            bool free = _t < 0f && Time.time >= _holdUntil;
            if ((free && d > st.MinJump) || d > BigJump)
            {
                _from = _now; _to = want; _t = 0f; _over = st.Overshoot;
                _len = (st.Base + st.PerDeg * Quaternion.Angle(_from, want)) / speed;
            }
            if (_t >= 0f)
            {
                _t += dt;
                float s = Mathf.Clamp01(_t / Mathf.Max(0.01f, _len));
                float e;
                if (_over > 0.001f) { float x = s - 1f; e = 1f + (_over + 1f) * x * x * x + _over * x * x; } // out, a touch past, back
                else e = s * s * s * (s * (6f * s - 15f) + 10f);                                              // soft in and out
                _now = Quaternion.SlerpUnclamped(_from, _to, e);
                if (s >= 1f) { _t = -1f; _now = _to; _holdUntil = Time.time + st.Hold * Random.Range(0.6f, 1.4f) / speed; }
            }
            else
            {
                _to = Quaternion.Slerp(_to, want, 1f - Mathf.Exp(-dt / st.Drift)); // held: only the small drift
                _now = _to;
            }
            return _now;
        }

        // a look set from outside (an eased one while it slept, drooped, dozed): the moves go on from there
        internal void Reset(Quaternion q) { _from = _to = _now = q; _t = -1f; }

        // yaw / pitch (°, about up / the side) for a head that keeps them as numbers (the perch's)
        internal static Quaternion FromAngles(float yaw, float pitch) { return Quaternion.Euler(pitch, yaw, 0f); }
        internal static void ToAngles(Quaternion q, out float yaw, out float pitch)
        {
            Vector3 f = q * Vector3.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(-f.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}

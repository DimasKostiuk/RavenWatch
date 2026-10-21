#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The adaptive wing beat: on top of the wing keys, each stroke is a little different, from what the flight does.
    // Decided once a stroke (at its top) and eased in over its first part, so nothing jumps:
    //   depth   — working hard (climbing, speeding up, braking) the wings go higher and lower; gliding down, shallower
    //   turn    — banked into a turn, the inner wing beats smaller and is held a little back, the outer one wider
    //             (this one follows the bank all the time, turns change mid-stroke)
    //   variety — every stroke a few degrees off the last, so it never looks like a film loop
    //   nature  — Hugin's strokes broad and even, Munin's narrower and more uneven
    // The strengths are multipliers (0 = off) set in the wing panel; the degrees are at strength 1.
    internal static class WingMods
    {
        internal static bool On = true; // on top of the user's beat (WingKeys.Base): the base, then these, then a template
        internal static float Depth = 1f, Turn = 1f, Variety = 1f, Nature = 1f;

        internal const float DepthDeg = 10f;   // full effort: the top this much higher, the bottom this much lower
        internal const float ThrustDeg = 2.5f; // accelerating opens the stroke; braking softens it
        internal const float ShallowDeg = 6f;  // gliding down: this much less
        internal const float TurnDeg = 12f;    // full bank: inner wing this much smaller, outer this much wider
        internal const float TuckDeg = 10f;    // full bank: inner wing swept back this much
        internal const float VarietyDeg = 3f;  // each stroke's own lift and sweep, up to this much
        internal const float HuginDeg = 3f;    // Hugin's broader stroke
        internal const float MuninDeg = 2f;    // Munin's narrower stroke

        // what the flight tells the wings (FlightBody.Wings sets it each frame)
        internal struct State
        {
            internal float Effort; // 0..1: climbing, speeding up, braking
            internal float Climb;  // −1..1: up or down
            internal float Thrust; // −1..1: slowing down or accelerating along the flight path
            internal float Dive;   // 0..1: fast descent; folds the wings back
            internal float Bank;   // degrees, + = right wing down (a right turn)
            internal float MaxBank;
        }

        // one raven's (or the panel's) running stroke: the decided values, eased from the last stroke's
        internal sealed class Runner
        {
            private int _stroke = -1;
            private float _depthFrom, _depth;
            private readonly Vector3[] _varFrom = new Vector3[WingKeys.Bones * 2], _var = new Vector3[WingKeys.Bones * 2];
            private float _bank, _dive, _lastW = 1f;
            internal bool Settled; // the panel's still picture: the stroke shown as decided, now (no easing in)

            internal void Reset() { _lastW = 1f; _stroke = -1; _depthFrom = _depth = 0f; _bank = _dive = 0f; for (int i = 0; i < _var.Length; i++) _varFrom[i] = _var[i] = Vector3.zero; }

            // u: point of the loop (0..1); x: key coordinate (0..10); adds into addL/addR (per bone: sweep, lift, twist)
            internal void Step(float u, float x, State st, bool munin, float dt, Vector3[] addL, Vector3[] addR)
            {
                for (int i = 0; i < WingKeys.Bones; i++) { addL[i] = Vector3.zero; addR[i] = Vector3.zero; }
                if (!On) return;
                int s = WingKeys.StrokeAt(u);
                if (s != _stroke)
                {
                    // a new stroke: from where the last one was to this one's values
                    float w0 = _lastW; // how far the last stroke had come in
                    _depthFrom = Mathf.Lerp(_depthFrom, _depth, w0);
                    for (int i = 0; i < _var.Length; i++) _varFrom[i] = Vector3.Lerp(_varFrom[i], _var[i], w0);
                    _stroke = s;
                    _depth = DepthOf(st, munin);
                    for (int i = 0; i < _var.Length; i++) _var[i] = new Vector3(Random.Range(-1f, 1f) * 0.6f, Random.Range(-1f, 1f), 0f); // scaled when used (the slider works at once)
                }
                if (Settled) _depth = DepthOf(st, munin);
                float w = Settled ? 1f : Ease(x);
                _lastW = w;
                float depth = Mathf.Lerp(_depthFrom, _depth, w);
                _dive = Mathf.Lerp(_dive, Mathf.Clamp01(st.Dive), Mathf.Clamp01(dt * 3f));
                float v = Variety * VarietyDeg * (munin ? 1.5f : 1f);
                // the bank, smoothed (a turn changes mid-stroke; it eases, never snaps)
                _bank = Mathf.Lerp(_bank, st.Bank, Mathf.Clamp01(dt * 6f));
                float bank01 = st.MaxBank > 1f ? Mathf.Clamp(_bank / st.MaxBank, -1f, 1f) : 0f;
                // the stroke's shape: +1 at the top (keys 1 and 11), −1 at the bottom (key 6)
                float shape = Mathf.Cos(Mathf.PI * Mathf.Repeat(x, WingKeys.Keys) / WingKeys.Half);
                float turn = Turn * TurnDeg * Mathf.Abs(bank01), tuck = Turn * TuckDeg * Mathf.Abs(bank01);
                bool rightInner = bank01 > 0f; // right wing down: a right turn, the right wing inside
                for (int side = 0; side < 2; side++)
                {
                    bool right = side == 1;
                    bool inner = bank01 != 0f && right == rightInner;
                    float amp = depth + (inner ? -turn : turn);
                    Vector3[] add = right ? addR : addL;
                    for (int i = 0; i < WingKeys.Bones; i++)
                    {
                        float share = i == 0 ? 1f : i == 1 ? 0.5f : 0.35f; // most from the shoulder, the rest follows
                        Vector3 var = Vector3.Lerp(_varFrom[side * WingKeys.Bones + i], _var[side * WingKeys.Bones + i], w) * v;
                        add[i] = new Vector3((inner && i == 0 ? -tuck : 0f) + var.x * share, amp * shape * share + var.y * share, 0f);
                    }
                }
            }

            private static float DepthOf(State st, bool munin)
            {
                float effort = Mathf.Clamp01(st.Effort), down = Mathf.Clamp01(-st.Climb);
                float thrust = Mathf.Clamp(st.Thrust, -1f, 1f);
                return Depth * (DepthDeg * effort + ThrustDeg * thrust - ShallowDeg * down * (1f - effort)) + Nature * (munin ? -MuninDeg : HuginDeg);
            }

            // the new stroke's values come in over its first keys (top to about half way down)
            private static float Ease(float x)
            {
                float t = Mathf.Clamp01(Mathf.Repeat(x, WingKeys.Keys) / 2.5f);
                return t * t * (3f - 2f * t);
            }
        }
    }
}
#endif

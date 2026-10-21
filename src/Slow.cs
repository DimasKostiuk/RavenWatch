#if RAVEN_ITEM
using System.Diagnostics;
using UnityEngine;

namespace HuginWhispers
{
    // Finding what makes the game stutter: every step of ours that takes 8 ms or more is written to the log by name,
    // and, while "raven perf" measures, so is every frame of the game that takes a quarter of a second or more
    // (whoever caused it), so the two can be laid side by side. Cheap: a clock read around a handful of rare steps, one comparison a frame.
    internal static class Slow
    {
        internal static float StepMs = 8f, FrameMs = 250f;
        private static float _ours; // ms of our slow steps in this frame

        internal static long Start() { return Stopwatch.GetTimestamp(); }

        internal static void End(long t0, string what)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms < StepMs) return;
            _ours += (float)ms;
            if (Plugin.Instance != null) Plugin.Instance.Log("Slow: " + what + " " + ms.ToString("0") + " ms (frame " + Time.frameCount + ")");
        }

        // every frame: a long one is told, with how much of it was ours
        internal static void Frame()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (Perf.On && ms >= FrameMs && Time.realtimeSinceStartup > 20f && Plugin.Instance != null)
                Plugin.Instance.Log("Slow: game frame " + ms.ToString("0") + " ms (frame " + Time.frameCount + "), ours in the frame before " + _ours.ToString("0") + " ms");
            _ours = 0f;
        }
    }
}
#endif

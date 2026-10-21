#if RAVEN_ITEM
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A short line in the top-left corner (like "picked up wood") when a raven's strength or trust grows:
    // "Хугін +3 сили, +0,5 довіри · робота". Gains are gathered for a few seconds, one line per raven at most
    // that often, so a fight or a forest of trees does not flood the corner.
    internal static class GainNote
    {
        internal static bool On = true;
        internal static float Seconds = 3f;

        private sealed class Pending { public float Strength, Trust, Since = -1f; public string Why; public bool Mixed; }
        private static readonly Pending[] Notes = { new Pending(), new Pending() };

        internal static void Strength(bool munin, float amount, string why) { Add(munin, amount, 0f, why); }
        internal static void Trust(bool munin, float amount, string why) { Add(munin, 0f, amount, why); }

        private static void Add(bool munin, float strength, float trust, string why)
        {
            if (!On || (strength <= 0f && trust <= 0f)) return;
            Pending n = Notes[munin ? 1 : 0];
            if (n.Since < 0f) n.Since = Time.time;
            n.Strength += Mathf.Max(0f, strength);
            n.Trust += Mathf.Max(0f, trust);
            if (string.IsNullOrEmpty(why)) return;
            if (n.Why == null) n.Why = why;
            else if (n.Why != why) n.Mixed = true; // several reasons at once: the first one stands for them
        }

        // twice a second (Power.Tick)
        internal static void Tick(Player p)
        {
            for (int i = 0; i < 2; i++)
            {
                Pending n = Notes[i];
                if (n.Since < 0f || Time.time - n.Since < Seconds) continue;
                string line = Line(i == 1, n);
                n.Strength = n.Trust = 0f; n.Since = -1f; n.Why = null; n.Mixed = false;
                if (line != null && p != null) p.Message(MessageHud.MessageType.TopLeft, line, 0, RavenItem.Icon(i == 1));
            }
        }

        internal static void Reset()
        {
            foreach (Pending n in Notes) { n.Strength = n.Trust = 0f; n.Since = -1f; n.Why = null; n.Mixed = false; }
        }

        private static string Line(bool munin, Pending n)
        {
            int strength = Mathf.RoundToInt(n.Strength);
            bool trust = n.Trust >= 0.05f;
            if (strength <= 0 && !trust) return null;
            string s = Plugin.RavenName(munin);
            if (strength > 0) s += Lang.F(" +{0} сили", strength);
            if (trust) s += Lang.F("{0} +{1} довіри", (strength > 0 ? "," : ""), Num(n.Trust));
            if (n.Why != null) s += " · " + Grammar.Fix(n.Why) + (n.Mixed ? "…" : "");
            return s;
        }

        private static string Num(float v)
        {
            return v >= 10f ? Lang.Num(Mathf.Round(v), "0") : Lang.Num(v, "0.#");
        }
    }
}
#endif

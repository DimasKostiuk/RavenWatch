using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The game's raven bringing our words: how it came and went, in the log. The game flies it in and off, or makes it
    // appear and vanish in a puff of feathers: under a roof, within 6 s of its last puff, or when it is told to
    // (another text waiting, nothing to stand on). One line each way, only for our texts.
    internal static class RavenVisitLog
    {
        private static readonly FieldInfo SinceTeleport = AccessTools.Field(typeof(Raven), "m_timeSinceTeleport");
        private static readonly FieldInfo Ground = AccessTools.Field(typeof(Raven), "m_groundObject");
        private static readonly MethodInfo UnderRoof = AccessTools.Method(typeof(Raven), "IsUnderRoof");
        private static readonly MethodInfo BestText = AccessTools.Method(typeof(Raven), "GetBestText");

        internal static bool Ours(Raven.RavenText t) { return t != null && t.m_key != null && t.m_key.StartsWith(Plugin.KeyPrefix); }

        internal static string Why(Raven r)
        {
            try
            {
                float since = SinceTeleport != null ? (float)SinceTeleport.GetValue(r) : -1f;
                bool roof = UnderRoof != null && (bool)UnderRoof.Invoke(r, null);
                bool ground = Ground == null || Ground.GetValue(r) as GameObject != null;
                Raven.RavenText best = BestText != null ? BestText.Invoke(r, null) as Raven.RavenText : null;
                return "under roof " + roof + ", " + since.ToString("0.0") + " s since last puff, stands on something " + ground
                    + ", next text " + (best != null ? best.m_key : "none");
            }
            catch (Exception e) { return "(" + e.Message + ")"; }
        }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }

    [HarmonyPatch(typeof(Raven), "Spawn")]
    internal static class RavenVisitSpawnLog
    {
        // a raven that cannot finish flying in is spawned again by the game every second (seen: 87 tries in a row by a
        // crypt): from the third try in a row it comes in a puff, as the copies of the shared ravens do
        private static string _key;
        private static float _last;
        private static int _tries;

        private static void Prefix(Raven __instance, Raven.RavenText text, ref bool forceTeleport)
        {
            if (!RavenVisitLog.Ours(text)) return;
            if (text.m_key == _key && Time.time - _last < 3f) _tries++; else _tries = 1;
            _key = text.m_key; _last = Time.time;
            if (_tries >= 3) forceTeleport = true;
            if (_tries <= 3 || _tries % 20 == 0)
                RavenVisitLog.Log("Game raven arrives with " + text.m_key + ": " + (forceTeleport ? (_tries >= 3 ? "in a puff (try " + _tries + ": could not fly in)" : "in a puff (told to)") : "flying in, unless static and under a roof") + " — " + RavenVisitLog.Why(__instance));
        }
    }

    [HarmonyPatch(typeof(Raven), "FlyAway")]
    internal static class RavenVisitFlyAwayLog
    {
        private static Raven _last;
        private static float _at;

        private static void Postfix(Raven __instance, bool forceTeleport, bool __runOriginal)
        {
            if (!__runOriginal || !RavenVisitLog.Ours(Plugin.CurrentText(__instance))) return;
            if (__instance == _last && Time.time - _at < 5f) return; // the game asks every frame until it is off
            _last = __instance; _at = Time.time;
            RavenVisitLog.Log("Game raven leaves: " + (forceTeleport ? "in a puff (told to)" : "flying off, unless under a roof") + " — " + RavenVisitLog.Why(__instance));
        }
    }
}

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's scream frightens: the enemies it reaches (not bosses) run from you for a few seconds, with the game's
    // own fleeing (as animals run, or monsters from fire). A creature's mind runs on the game that owns it, so the
    // screamer tells every game which creatures are scared; each one then runs on whichever game drives it.
    internal static class Fear
    {
        internal static float Seconds = 3f;
        private const string Rpc = "RavenWatch_Fear";
        private sealed class Scare { public float Until; public Vector3 From; }
        private static readonly Dictionary<ZDOID, Scare> Scared = new Dictionary<ZDOID, Scare>();
        private static readonly MethodInfo FleeMethod = AccessTools.Method(typeof(BaseAI), "Flee", new Type[] { typeof(float), typeof(Vector3) });

        private static float _nextPrune;
        private static readonly List<ZDOID> Over = new List<ZDOID>();

        // anyone scared now; the marks of creatures driven by other games are pruned here (they never run on this one)
        internal static bool Any
        {
            get
            {
                if (Scared.Count == 0) return false;
                if (Time.time >= _nextPrune)
                {
                    _nextPrune = Time.time + 1f;
                    Over.Clear();
                    foreach (KeyValuePair<ZDOID, Scare> kv in Scared) if (Time.time >= kv.Value.Until) Over.Add(kv.Key);
                    foreach (ZDOID id in Over) Scared.Remove(id);
                }
                return Scared.Count > 0;
            }
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnScare));
        }

        // the screamer's game: these run, here and wherever they are driven
        internal static void Frighten(Vector3 from, List<Character> who)
        {
            if (who.Count == 0 || Seconds <= 0f) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(from);
            pkg.Write(Seconds);
            pkg.Write(who.Count);
            foreach (Character c in who) { ZDOID id = c.GetZDOID(); Mark(id, from, Seconds); pkg.Write(id); }
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnScare(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return; // marked already
            Vector3 from = pkg.ReadVector3();
            float seconds = Mathf.Clamp(pkg.ReadSingle(), 0f, 10f);
            int n = Mathf.Min(pkg.ReadInt(), 64);
            for (int i = 0; i < n; i++) Mark(pkg.ReadZDOID(), from, seconds);
        }

        private static void Mark(ZDOID id, Vector3 from, float seconds)
        {
            if (id == ZDOID.None) return;
            Scared[id] = new Scare { Until = Time.time + seconds, From = from };
        }

        // from the monster's own thinking (only on the game that drives it): run instead of thinking, while scared
        internal static bool Runs(MonsterAI ai, float dt)
        {
            Character c = ai.GetComponent<Character>();
            if (c == null) return false;
            Scare s;
            ZDOID id = c.GetZDOID();
            if (!Scared.TryGetValue(id, out s)) return false;
            if (Time.time >= s.Until || c.IsDead()) { Scared.Remove(id); return false; }
            if (FleeMethod == null) return false;
            try { FleeMethod.Invoke(ai, new object[] { dt, s.From }); }
            catch (Exception) { Scared.Remove(id); return false; } // the game changed: no fear rather than a stuck monster
            return true;
        }

        internal static void Reset() { Scared.Clear(); }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class FearPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        { try {
            if (!Fear.Any || !Fear.Runs(__instance, dt)) return true; // nobody scared: nothing to do, the cheapest way out
            __result = true;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("FearPatch", e); return true; } }
    }
}
#endif

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
        internal static float Seconds = 4f, SecondsTop = 8f; // the scream: at the trust level it opens on and at the top one (the others scare for Seconds)
        internal static float ScreamSeconds() { return Seconds <= 0f ? 0f : Mathf.Floor(Trust.LerpGift("scream", Seconds, SecondsTop) + 0.5f); }
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
        internal static void Frighten(Vector3 from, List<Character> who) { Frighten(from, who, Seconds); }

        internal static void Frighten(Vector3 from, List<Character> who, float seconds)
        {
            if (who.Count == 0 || seconds <= 0f) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(from);
            pkg.Write(seconds);
            pkg.Write(who.Count);
            foreach (Character c in who) { ZDOID id = c.GetZDOID(); Mark(id, from, seconds); pkg.Write(id); }
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

    // Dazed (the dive of «Крила Одіна» III): the creature stands where it is, aims at nobody and reels again and again,
    // with Hugin's little lights going round its head (DecoyMark). Told to every game like the fear; the game that
    // drives it holds it. Not bosses.
    internal static class Daze
    {
        internal static float Seconds = 5f, ReelEvery = 1.6f;
        private const string Rpc = "RavenWatch_Daze";
        private sealed class Daz { public float Until, NextReel; }
        private static readonly Dictionary<ZDOID, Daz> Dazed = new Dictionary<ZDOID, Daz>();
        private static readonly MethodInfo StopM = AccessTools.Method(typeof(BaseAI), "StopMoving");
        internal static bool Any { get { return Dazed.Count > 0; } }
        internal static bool Is(ZDOID id) { Daz d; return Dazed.TryGetValue(id, out d) && Time.time < d.Until; }

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnDaze)); }

        internal static void Put(List<Character> who) { Put(who, Seconds); }

        internal static void Put(List<Character> who, float seconds)
        {
            if (who.Count == 0 || seconds <= 0f || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(seconds);
            pkg.Write(Mathf.Min(who.Count, 32));
            for (int i = 0; i < who.Count && i < 32; i++) pkg.Write(who[i].GetZDOID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg); // (every game, this one too)
        }

        private static void OnDaze(long sender, ZPackage pkg)
        {
            float secs = Mathf.Clamp(pkg.ReadSingle(), 0f, 10f);
            int n = Mathf.Min(pkg.ReadInt(), 32);
            if (Dazed.Count > 64) { List<ZDOID> over = new List<ZDOID>(); foreach (KeyValuePair<ZDOID, Daz> kv in Dazed) if (Time.time >= kv.Value.Until) over.Add(kv.Key); foreach (ZDOID id in over) Dazed.Remove(id); }
            for (int i = 0; i < n; i++)
            {
                ZDOID id = pkg.ReadZDOID();
                Character c = Decoy.Find(id);
                if (c == null || c.IsDead() || c.IsBoss()) continue;
                Dazed[id] = new Daz { Until = Time.time + secs, NextReel = Time.time + ReelEvery };
                DecoyMark.On(c, id); // the lights over its head
            }
        }

        // from the monster's own thinking (only on the game that drives it): nothing but reeling while dazed
        internal static bool Holds(MonsterAI ai)
        {
            Character c = ai.GetComponent<Character>();
            if (c == null) return false;
            ZDOID id = c.GetZDOID();
            Daz d;
            if (!Dazed.TryGetValue(id, out d)) return false;
            if (Time.time >= d.Until || c.IsDead()) { Dazed.Remove(id); return false; }
            try
            {
                AiTarget.Set(ai, null);
                if (StopM != null) StopM.Invoke(ai, null);
                if (Time.time >= d.NextReel) { d.NextReel = Time.time + ReelEvery; c.Stagger(-c.transform.forward); }
            }
            catch (Exception) { Dazed.Remove(id); return false; } // (the game changed: free rather than stuck)
            return true;
        }

        internal static void Reset() { Dazed.Clear(); }
    }

    // first of our creature minds (before the decoy): a scare is short, the lure goes on after it — one mind a frame,
    // whoever's gift it is (another player's Hugin may lure the very monster your Munin scared)
    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    [HarmonyPriority(Priority.High)]
    internal static class FearPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result, bool __runOriginal)
        { try {
            if (!__runOriginal) return false; // (driven already this frame)
            if (Daze.Any && Daze.Holds(__instance)) { __result = true; return false; } // (dazed: before a fear, it cannot even run)
            if (!Fear.Any || !Fear.Runs(__instance, dt)) return true; // nobody scared: nothing to do, the cheapest way out
            __result = true;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("FearPatch", e); return true; } }
    }
}
#endif

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Our copies of the game's raven are quiet: the game's appearing (its feather puff fx_raven_despawn thrown by the
    // animator's state behaviour as it enters a state, the clips' Effect events, the model's own odinsmoke) never
    // plays by itself whenever we wake or restart an animator. Where a raven really appears out of nowhere (the
    // shoulder's fly-in), it is let through for that flight (Allow). Our own puffs are made where we mean them.
    // (fireEvents does not reach the state behaviour: it is for the clips' events only.)
    internal sealed class QuietAppear : MonoBehaviour
    {
        internal float AllowUntil = -1f;
    }

    internal static class AppearMute
    {
        private static readonly EffectList None = new EffectList();

        // a raven copy of ours: quiet from now on
        internal static void Quiet(GameObject go)
        {
            if (go != null && go.GetComponent<QuietAppear>() == null) go.AddComponent<QuietAppear>();
        }

        // it really appears now (the game's fly-in): its appearing shows for this long
        internal static void Allow(GameObject go, float seconds)
        {
            QuietAppear q = go != null ? go.GetComponentInParent<QuietAppear>() : null;
            if (q != null) q.AllowUntil = Time.time + seconds;
        }

        // a particle system of the game's raven model itself (its odinsmoke and the like), not one of ours (the aura)
        // nor a worn piece's (an outfit's or a Workshop model's own effects keep going)
        private static HashSet<string> _own;
        internal static bool OwnSmoke(ParticleSystem ps)
        {
            if (ps == null) return false;
            if (_own == null)
            {
                Raven h = Shoulder.Template(false), m = Shoulder.Template(true);
                if (h == null || h.m_visual == null) return ps.name == "odinsmoke"; // (not loaded yet: the one it is known to have)
                _own = new HashSet<string>();
                foreach (Raven r in new[] { h, m })
                    if (r != null && r.m_visual != null)
                        foreach (ParticleSystem p in r.m_visual.GetComponentsInChildren<ParticleSystem>(true)) _own.Add(p.name);
            }
            return _own.Contains(ps.name);
        }

        internal static bool Muted(Component c)
        {
            if (c == null) return false;
            QuietAppear q = c.GetComponentInParent<QuietAppear>();
            return q != null && Time.time >= q.AllowUntil;
        }

        // the state's own puff: swapped for nothing while it enters (its parts' hiding and showing go on), and the
        // smoke its showing lit put out at once
        [HarmonyPatch(typeof(StateController), "OnStateEnter")]
        private static class StateEnter
        {
            private static void Prefix(StateController __instance, Animator animator, out EffectList __state)
            {
                __state = null;
                if (__instance == null || !Muted(animator)) return;
                __state = __instance.m_enterEffect;
                __instance.m_enterEffect = None;
            }
            private static void Postfix(StateController __instance, Animator animator, EffectList __state)
            {
                if (__state == null || __instance == null) return;
                __instance.m_enterEffect = __state;
                foreach (ParticleSystem ps in animator.GetComponentsInChildren<ParticleSystem>(true))
                    if (OwnSmoke(ps) && (ps.isPlaying || ps.particleCount > 0)) { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(false); }
            }
        }

        // a clip's Effect event (a puff, a voice as it appears)
        [HarmonyPatch(typeof(AnimationEffect), "Effect")]
        private static class ClipEffect
        {
            private static bool Prefix(AnimationEffect __instance) { return !Muted(__instance); }
        }
    }
}

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's fury: the attack animations run faster for a while, and a hit lands when its animation reaches it,
    // so the blows come faster. Valheim has no attack speed stat; this is how such mods do it.
    internal static class Fury
    {
        internal const string EffectName = "RavenWatch_Fury";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        internal static float Multiplier = 1.35f;
        internal static float Seconds = 10f, SecondsTop = 20f; // at trust level 1 and at the top level
        internal static float Duration() { return Mathf.Floor(Trust.Lerp(true, Seconds, SecondsTop) * GearFx.FuryMul() * GiftUp.Power("fury") + 0.5f); } // (Кігтясте намисто: longer)
        private static SE_Stats _template;
        private static float _set = -1f;   // the speed we wrote last
        private static float _before = 1f; // what it was before we sped it up

        internal static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Лють");
            se.m_tooltip = Lang.F("+{0}% до швидкості атаки, натягу лука й перезарядки арбалета", Mathf.RoundToInt(Percent()));
            se.m_icon = AbilityIcons.ForEffect("fury", true);
            se.m_ttl = Mathf.Max(1f, Duration());
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // how much faster now, in percent (grows with Munin's trust, as Speed counts it)
        internal static float Percent() { return (Multiplier - 1f) * Trust.Mul(true, 0.1f) * 100f; }

        internal static bool On(Character c)
        {
            return c != null && c == Player.m_localPlayer && c.GetSEMan().HaveStatusEffect(Hash);
        }

        // the wearer tells everyone the fury is on (Munin's wings spread for it on every game)
        private const string NetKey = "RavenWatch_Fury";
        internal static void Publish(Player p) { NetFlag.Set(p, NetKey, p.GetSEMan().HaveStatusEffect(Hash)); }
        internal static bool Shown(Player p) { return p != null && !p.IsDead() && NetFlag.Get(p, NetKey); }

        // how much faster the attacks go now: Fury and Munin's blood fury multiply
        internal static float Speed(Character c)
        {
            float m = 1f;
            if (On(c)) m *= 1f + (Multiplier - 1f) * Trust.Mul(true, 0.1f);
            if (BloodFury.On(c)) m *= 1f + Mathf.Max(0f, BloodFury.SpeedPercent) / 100f;
            return m;
        }

        // The animation speed is set by the game and, each frame, by mods such as Path of Valheiman
        // (its attack speed). Fury multiplies whatever they set, and only once: when the speed is still
        // the value we wrote last frame, nobody reset it, and multiplying again would stack
        // (combo attacks like the one-handed axe's keep the speed between their swings).
        internal static void Tick(Character c, Animator anim)
        {
            if (!ReferenceEquals(c, Player.m_localPlayer) || c == null || anim == null) return; // every creature, every physics step: the cheapest test first
            bool attacking = c.InAttack();
            bool reloading = !attacking && Reloading && c.InMinorAction(); // a crossbow reload is a "minor action"
            float mult = Speed(c);
            if ((attacking || reloading) && mult > 1.0001f)
            {
                float cur = anim.speed;
                if (Mathf.Abs(cur - _set) > 0.0001f)
                {
                    _before = cur;
                    _set = cur * mult;
                    anim.speed = _set;
                }
                return;
            }
            // fury ran out in the middle of an attack: back to what the game and the other mods wanted
            if (_set > 0f && (attacking || reloading) && Mathf.Abs(anim.speed - _set) <= 0.0001f) anim.speed = _before;
            _set = -1f;
        }

        internal static bool Reloading;

        internal static void Reset() { _set = -1f; _before = 1f; _gestureUntil = 0f; Reloading = false; }

        // ---------------------------------------------------------------- the look
        // Fury borrows the boss power gesture. At its peak the game would use the real boss power (and start its
        // cooldown); that is stopped, and the boss power's activation effect is shown instead, in raven grey.

        internal const string RpcFx = "RavenWatch_FuryFx", RpcSound = "RavenWatch_FurySound";
        // its sound: the goblin brute's taunt, backwards and half as long again (the boss power's own sound is silenced)
        internal const string Sound = "sfx_goblinbrute_taunt";
        internal static float SoundStretch = 1.5f, SoundFadeIn = 1f; // swells in over a second
        internal static Color FxColor = new Color(0.74f, 0.76f, 0.8f);
        private static float _gestureUntil;

        internal static void Register() { RavenFx.Register(RpcFx, Spawn); RavenFx.Register(RpcSound, SpawnSound); }

        // the sound starts on the press itself (it swells in for a second), so it is full by the gesture's peak
        internal static void PlaySound(Player p) { RavenFx.Broadcast(RpcSound, p, SpawnSound); }
        private static void SpawnSound(Player p) { if (p != null) SoundBack.PlayNamed(Sound, p.transform, 0f, SoundStretch, SoundFadeIn); }

        internal static void Gesture() { _gestureUntil = Time.time + 4f; }
        internal static void CancelGesture() { _gestureUntil = 0f; }

        // the gesture reached its peak: ours, not the boss power
        internal static bool TakeGesture(Player p)
        {
            if (p != Player.m_localPlayer || Time.time >= _gestureUntil) return false;
            _gestureUntil = 0f;
            PlayFx(p);
            return true;
        }

        internal static void PlayFx(Player p) { RavenFx.Broadcast(RpcFx, p, Spawn); }

        private static void Spawn(Player p)
        {
            try
            {
                StatusEffect look = BossPower(p);
                if (look == null || look.m_startEffects == null) return;
                GameObject[] made = RavenFx.Own(look.m_startEffects, p.transform.position, p.transform.rotation, p.transform, 1f, -1);
                if (made != null)
                    foreach (GameObject g in made)
                    {
                        if (g == null) continue;
                        RavenLight.Tint(g, FxColor, 1f);
                        foreach (ZSFX z in g.GetComponentsInChildren<ZSFX>(true)) z.enabled = false;
                        foreach (AudioSource a in g.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; } // the look only: our own sound below
                    }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Fury effect failed: " + e.Message); }
        }

        // the wearer's own boss power, or any: they share the activation look
        private static StatusEffect BossPower(Player p)
        {
            if (ObjectDB.instance == null) return null;
            string mine = p != null ? p.GetGuardianPowerName() : null;
            StatusEffect any = null;
            foreach (StatusEffect se in ObjectDB.instance.m_StatusEffects)
            {
                if (se == null || !se.name.StartsWith("GP_") || se.m_startEffects == null || se.m_startEffects.m_effectPrefabs == null || se.m_startEffects.m_effectPrefabs.Length == 0) continue;
                if (!string.IsNullOrEmpty(mine) && se.name == mine) return se;
                if (any == null) any = se;
            }
            return any;
        }

        // timers that do not follow the animation: a bow's (or a charged staff's) draw, a crossbow's reload
        internal static float Extra(Player p, float dt)
        {
            return p == Player.m_localPlayer ? dt * (Speed(p) - 1f) : 0f;
        }
    }

    // the boss power gesture of Fury: no real boss power and no cooldown for it, the grey effect instead
    [HarmonyPatch(typeof(Player), "ActivateGuardianPower")]
    internal static class FuryGuardianPatch
    {
        private static bool Prefix(Player __instance, ref bool __result)
        {
            if (!Fury.TakeGesture(__instance)) return true;
            __result = false;
            return false;
        }
    }

    // the real boss power key: never mistaken for Fury's gesture
    [HarmonyPatch(typeof(Player), "StartGuardianPower")]
    internal static class FuryGuardianKeyPatch
    {
        private static void Prefix() { Fury.CancelGesture(); }
    }

    // drawing a bow (and staffs that charge the same way) goes faster
    [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
    internal static class FuryDrawPatch
    {
        private static void Postfix(Player __instance, float dt, ref float ___m_attackDrawTime)
        {
            if (___m_attackDrawTime > 0f) ___m_attackDrawTime += Fury.Extra(__instance, dt);
        }
    }

    // reloading a crossbow goes faster
    [HarmonyPatch(typeof(Player), "UpdateActionQueue")]
    internal static class FuryReloadPatch
    {
        private static void Prefix(Player __instance, float dt, List<Player.MinorActionData> ___m_actionQueue)
        {
            if (__instance != Player.m_localPlayer) return;
            bool reload = ___m_actionQueue != null && ___m_actionQueue.Count > 0 && ___m_actionQueue[0].m_type == Player.MinorActionData.ActionType.Reload;
            Fury.Reloading = reload;
            if (reload) ___m_actionQueue[0].m_time += Fury.Extra(__instance, dt);
        }
    }

    // after Path of Valheiman's own attack speed (same place), so the two multiply instead of one overwriting the other
    [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
    [HarmonyAfter("treextr.pathofvalheiman")]
    [HarmonyPriority(Priority.Last)]
    internal static class FuryAttackPatch
    {
        private static void Postfix(Character ___m_character, Animator ___m_animator) { Fury.Tick(___m_character, ___m_animator); }
    }
}
#endif

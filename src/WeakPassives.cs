#if RAVEN_ITEM
using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The small passives of every awake raven from trust level WeakUnlock (the strong ones from StrongUnlock):
    //   Munin's anger: you got hit, he gets angry — for a while you hit harder.
    //   Hugin's light step: you run with no enemy near — for a while your stamina comes back faster.
    // Each takes a little strength and can start once in a while (the wait is kept in the character, in world time).
    internal static class WeakPassive
    {
        internal static float Cost = 10f;
        internal static float CooldownMinutes = 30f;
        internal static float Seconds = 300f;
        internal static float AngerPercent = 15f, AngerPercentTop = 35f; // more damage: at the level it opens on and at the top one
        internal static float StepPercent = 15f, StepPercentTop = 40f; // faster stamina regeneration: at the level it opens on and at the top one
        internal static bool Enabled = true;

        internal const string AngerName = "RavenWatch_Anger";
        internal const string StepName = "RavenWatch_LightStep";
        private const string AngerKey = "RavenWatch_angerAt";
        private const string StepKey = "RavenWatch_stepAt";
        private static SE_Stats _anger, _step;
        private static float _nextStepCheck;

        private static float Left(Player p, string key) { return p != null ? WorldTime.Left(p.m_customData, key) : 0f; }

        internal static float Cooldown() { return CooldownMinutes * 60f; }
        // as they are now for this raven (its trust; the golden runes), the very numbers Anger and Step use
        internal static float CooldownOf(bool munin) { return Trust.Sooner(munin, Cooldown()); }
        internal static float SecondsOf(bool munin) { return Mathf.Max(1f, Seconds * Trust.Mul(munin, 0.05f)) * (munin && Facets.On("anger") ? 2f : 1f); } // («Злість» III: twice as long)
        internal static float PercentOf(bool munin) { return (munin ? Trust.LerpFrom(true, RavenAbilities.WeakUnlock, AngerPercent, AngerPercentTop) : Trust.LerpFrom(false, RavenAbilities.WeakUnlock, StepPercent, StepPercentTop)) * GiftUp.Power(munin ? "anger" : "lightstep"); }
        internal static string ReadyText(bool munin) { return RavenAbilities.ReadyMark(Left(Player.m_localPlayer, munin ? AngerKey : StepKey), Lang.T("готовий")); }
        internal static string Status(bool munin) { return Lang.F("−{0} сили{1} (є {2}) · {3}", Mathf.RoundToInt(Cost * GiftUp.CostMul(munin ? "anger" : "lightstep") * Satiety.CostMul(munin)), Satiety.CostNote(munin), Power.Current(munin), ReadyText(munin)); }

        internal static void ResetCooldown()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            p.m_customData.Remove(AngerKey);
            p.m_customData.Remove(StepKey);
        }

        // the raven is on the shoulder, awake, its passive is free and there is strength for it
        private static bool Ready(Player p, bool munin, string key)
        {
            if (!Enabled || p == null || p != Player.m_localPlayer || p.IsDead()) return false;
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (raven == null || Trust.SettledLevel(raven) < RavenAbilities.WeakUnlock || Trust.Away(raven) || Trust.Hostile(raven) || Left(p, key) > 0f || !Shoulder.Arrived(p, munin)) return false;
            if (Power.Current(munin) < Mathf.Round(Cost * GiftUp.CostMul(munin ? "anger" : "lightstep") * Satiety.CostMul(munin))) return false; // hungry: dearer
            return true;
        }

        private static void Begin(Player p, bool munin, string key, string tag, string fallback)
        {
            string id = munin ? "anger" : "lightstep";
            if (!Power.Spend(p, munin, Cost * GiftUp.CostMul(id))) return;
            WorldTime.Set(p.m_customData, key, Mathf.Max(10f, Trust.Sooner(munin, Cooldown() * GiftUp.CooldownMul(id))));
            if (!RavenSkills.PassiveSpeaks()) return; // it works every time, but says something only now and then
            Say(p, munin, tag, fallback);
        }

        private static void Say(Player p, bool munin, string tag, string fallback)
        {
            string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, tag) : null;
            RavenSkills.SayFromShoulder(p, munin, string.IsNullOrEmpty(line) ? fallback : line);
        }

        // the test panel: the passive as it happens, without its conditions, strength or wait (and always a word)
        internal static void TestAnger(Player p) { if (p == null) return; Say(p, true, "anger", AngerLine); Anger(p); }
        internal static void TestStep(Player p) { if (p == null) return; Say(p, false, "lightstep", StepLine); Step(p); }
        private static readonly string AngerSrc = Lang.K("Ах ти ж падло! Тепер я злий. Бий їх!");
        private static string AngerLine { get { return Lang.T(AngerSrc); } }
        private static readonly string StepSrc = Lang.K("Дихай рівно, я підкажу ритм. Ноги втомлюватимуться менше.");
        private static string StepLine { get { return Lang.T(StepSrc); } }

        // ---------------------------------------------------------------- Munin: anger

        // after the game set the health: it went down (a blow, a fall, poison...)
        // the last real blow on you: damage taken, not health cut down because the max went down (a raven or food
        // taken off lowers the max)
        internal static float LastHit = -10f;
        internal static bool JustHit { get { return Time.time - LastHit < 0.5f; } }

        // a blow, not a tick of damage that goes on by itself (poison, burning, cold, smoke, lava, a mod's ailment with
        // nobody behind it): those do not startle the ravens or anger Munin
        internal static bool IsBlow(HitData hit)
        {
            switch (hit.m_hitType)
            {
                case HitData.HitType.EnemyHit: case HitData.HitType.PlayerHit: case HitData.HitType.Impact: case HitData.HitType.Cart:
                case HitData.HitType.Tree: case HitData.HitType.Structural: case HitData.HitType.Turret: case HitData.HitType.Boat:
                case HitData.HitType.Stalagtite: case HitData.HitType.Catapult: case HitData.HitType.DrawBridge:
                    return true;
                case HitData.HitType.Undefined:
                    return hit.GetAttacker() != null; // (another mod's blow: only with someone behind it)
                default:
                    return false;
            }
        }

        internal static void Hurt(Character c, float before)
        {
            Player p = c as Player;
            if (p == null || c.GetHealth() >= before || !JustHit) return;
            Owe(p, before - c.GetHealth()); // (in an anger already on: the blow goes into the debt)
            if (!Ready(p, true, AngerKey)) return;
            Begin(p, true, AngerKey, "anger", AngerLine);
            Anger(p);
            Twists.ShareAnger(p, PercentOf(true), SecondsOf(true)); // («Злість» II: it catches)
        }

        // «Злість» II: a friend's Munin got angry next to you — you are too, for a while
        internal static void AngerFrom(Player p, float pct, float seconds, string from)
        {
            SE_Stats se = RavenFx.Template(ref _anger, AngerName);
            se.m_name = Lang.W("Злість Муніна");
            se.m_tooltip = Lang.F("+{0}% шкоди", Mathf.RoundToInt(pct));
            se.m_icon = AbilityIcons.ForEffect("anger", true);
            se.m_ttl = seconds;
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = 1f + pct / 100f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Злість Муніна гравця {0} передалась і тобі: +{1}% шкоди", from, Mathf.RoundToInt(pct)));
        }

        // «Злість» III, the blood debt: while the anger lasts Munin counts every blow you take, and once it has grown
        // big (DebtTrigger of your health) or the blows stop for a moment (DebtQuiet), he flies at the last one who
        // struck you and pays it all back on top of his blow (× DebtShare). Not paid by the end of the anger: gone.
        internal static float DebtShare = 1.5f, DebtTrigger = 0.2f, DebtQuiet = 4f;
        internal static Character LastFoe; // who dealt the last real blow (RavenRealHitPatch)
        private static float _debt, _debtAt, _nextPay;
        private static readonly int AngerHash = AngerName.GetStableHashCode();

        internal static bool AngerOn(Player p) { return p != null && p.GetSEMan().HaveStatusEffect(AngerHash); }

        private static void Owe(Player p, float lost)
        {
            if (lost <= 0f || !AngerOn(p) || !Facets.On("anger")) return;
            if (_debt <= 0f) RavenMsg.Center(p, Lang.T("Мунін рахує кожен удар по тобі"));
            _debt += lost;
            _debtAt = Time.time;
        }

        // every frame: the debt paid when it is due and Munin can fly
        private static void Pay(Player p)
        {
            if (_debt <= 0f) return;
            if (p == null || p.IsDead() || !AngerOn(p)) { _debt = 0f; return; }
            if (Time.time < _nextPay || (_debt < p.GetMaxHealth() * DebtTrigger && Time.time - _debtAt < DebtQuiet)) return;
            _nextPay = Time.time + 0.5f; // (he cannot fly just now: tried again in a moment)
            Character foe = LastFoe != null && !LastFoe.IsDead() && Strike.Prey(p, LastFoe) ? LastFoe : Mastery.Nearest(p, p.transform.position, 15f, null);
            if (foe == null) return;
            float owed = _debt * DebtShare;
            if (!RavenSkills.FreeStrike(p, true, foe, owed)) return;
            _debt = 0f;
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Кривавий борг: Мунін повертає {0} шкоди", Mathf.RoundToInt(owed)));
            if (RavenSkills.PassiveSpeaks()) Say(p, true, "anger_debt", Lang.T(DebtSrc));
        }
        private static readonly string DebtSrc = Lang.K("Ти мені винен не був. А от він — винен. Тримай здачу, падло!");

        private static void Anger(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _anger, AngerName);
            float pct = PercentOf(true);
            se.m_name = Lang.W("Злість Муніна");
            se.m_tooltip = Lang.F("+{0}% шкоди", Mathf.RoundToInt(pct));
            se.m_icon = AbilityIcons.ForEffect("anger", true);
            se.m_ttl = SecondsOf(true);
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = 1f + pct / 100f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Злість Муніна: +{0}% шкоди, {1}", Mathf.RoundToInt(pct), RavenAbilities.Span(se.m_ttl)));
        }

        // ---------------------------------------------------------------- Hugin: light step

        // every 2 s at most: running, no enemy near
        internal static void Tick(Player p)
        {
            Pay(p);
            if (Time.time < _nextStepCheck) return;
            _nextStepCheck = Time.time + 2f;
            if (p == null || !p.IsRunning() || !Ready(p, false, StepKey)) return;
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsPlayer() && !c.IsDead() && !c.IsTamed() && BaseAI.IsEnemy(p, c)
                    && Vector3.Distance(c.transform.position, p.transform.position) < 25f) return; // a fight is near
            Begin(p, false, StepKey, "lightstep", StepLine);
            Step(p);
        }

        private static void Step(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _step, StepName);
            float pct = PercentOf(false);
            se.m_name = Lang.W("Легкий крок");
            se.m_tooltip = Lang.F("+{0}% до відновлення витривалості", Mathf.RoundToInt(pct));
            se.m_icon = AbilityIcons.ForEffect("lightstep", false);
            se.m_ttl = SecondsOf(false);
            se.m_staminaRegenMultiplier = 1f + pct / 100f;
            se.m_runStaminaDrainModifier = Facets.On("lightstep") ? -0.15f : 0f; // («Легкий крок» III: running drains 15% less)
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("Легкий крок: витривалість відновлюється на {0}% швидше, {1}", Mathf.RoundToInt(pct), RavenAbilities.Span(se.m_ttl)));
        }
    }

    // a real blow landed on you (after armour): what the anger and the raven's flinch wait for
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class RavenRealHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (hit != null && ReferenceEquals(__instance, Player.m_localPlayer) && hit.GetTotalDamage() > 0.01f && WeakPassive.IsBlow(hit)) { WeakPassive.LastHit = Time.time; Character a = hit.GetAttacker(); if (a != null && a != __instance) WeakPassive.LastFoe = a; }
        } catch (System.Exception e) { PatchGuard.Fail("RavenRealHitPatch", e); } }
    }

    // Every change of health of every creature passes here: only the local player's is looked at.
    // Before: the last chance may keep you alive. After: Munin's anger (you got hurt) and the blood fury (health low).
    [HarmonyPatch(typeof(Character), "SetHealth")]
    internal static class RavenHealthPatch
    {
        private static void Prefix(Character __instance, ref float health, out float __state)
        { __state = default(float); try {
            __state = -1f;
            if (__instance == null || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            __state = __instance.GetHealth();
            health = LastChance.Guard(__instance, health);
        } catch (System.Exception e) { PatchGuard.Fail("RavenHealthPatch", e); } }

        private static void Postfix(Character __instance, float __state)
        { try {
            if (__state < 0f) return;
            WeakPassive.Hurt(__instance, __state);
            float lost = __state - __instance.GetHealth();
            if (lost > 0.5f && WeakPassive.JustHit)
                foreach (bool munin in new bool[] { false, true }) { ShoulderLife life = Shoulder.LocalLife(munin); if (life != null) life.Hurt(lost, __instance.GetMaxHealth()); }
            BloodFury.Check(__instance);
        } catch (System.Exception e) { PatchGuard.Fail("RavenHealthPatch", e); } }
    }
}
#endif

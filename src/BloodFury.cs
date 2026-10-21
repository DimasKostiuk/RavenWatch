#if RAVEN_ITEM
using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's blood fury (passive): when your health falls low, Munin goes wild — for a while you hit harder
    // and faster. It takes Munin's strength and comes again only after a while (kept in the character, in
    // world time, like Hugin's last chance). Its speed adds to Fury's when both are on (see Fury.Speed).
    internal static class BloodFury
    {
        internal const string EffectName = "RavenWatch_BloodFury";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        internal const string RpcFx = "RavenWatch_BloodFuryFx";
        internal static bool Enabled = true;
        internal static float Cost = 50f;
        internal static float CooldownMinutes = 30f, CooldownMinutesTop = 10f; // at the level it opens and at the top one
        internal static float Seconds = 15f;
        internal static float Threshold = 0.3f;     // share of health
        internal static float DamagePercent = 30f;
        internal static float SpeedPercent = 20f;
        private const string Key = "RavenWatch_bloodFury";
        private static SE_Stats _template;
        private static bool _busy;

        private static float Left(Player p) { return p != null ? WorldTime.Left(p.m_customData, Key) : 0f; }

        internal static float Cooldown() { return Trust.RoundTime(Trust.LerpFrom(true, RavenAbilities.StrongUnlock, CooldownMinutes, CooldownMinutesTop) * 60f); }
        internal static float Duration() { return Mathf.Floor(Seconds * Trust.Mul(true, 0.05f) * GearFx.FuryMul() * GiftUp.Power("bloodfury") + 0.5f); } // (Кігтясте намисто: longer)
        internal static void ResetCooldown() { Player p = Player.m_localPlayer; if (p != null) p.m_customData.Remove(Key); }

        internal static string ReadyText()
        {
            float left = Left(Player.m_localPlayer);
            return RavenAbilities.ReadyMark(left, Lang.T("готовий"));
        }

        internal static string Status() { return Lang.F("−{0} сили{1} (є {2}) · {3}", Mathf.RoundToInt(Cost * GiftUp.CostMul("bloodfury") * Satiety.CostMul(true)), Satiety.CostNote(true), Power.Current(true), ReadyText()); }

        internal static bool On(Character c) { return c != null && c == Player.m_localPlayer && c.GetSEMan().HaveStatusEffect(Hash); }

        // after the game set the health: fallen low, Munin on the shoulder, ready and strong enough?
        internal static void Check(Character c)
        {
            if (!Enabled || _busy || c == null || c != Player.m_localPlayer || c.IsDead()) return;
            float hp = c.GetHealth();
            if (hp <= 0f || hp >= c.GetMaxHealth() * Threshold || On(c)) return;
            Player p = (Player)c;
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            if (munin == null || Trust.Away(munin) || Trust.SettledLevel(munin) < RavenAbilities.StrongUnlock || Left(p) > 0f || !Shoulder.Arrived(p, true)) return;
            _busy = true;
            try
            {
                if (!Power.Spend(p, true, Cost * GiftUp.CostMul("bloodfury"))) return;
                WorldTime.Set(p.m_customData, Key, Mathf.Max(10f, Cooldown() * GiftUp.CooldownMul("bloodfury")));
                Fire(p, false);
                Nicks.OnFury(p); // a deed for Munin's nickname
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Blood fury failed: " + e.Message); }
            finally { _busy = false; }
        }

        // what the blood fury is: the effect, the red burst every game sees, a word, the call in the middle
        private static void Fire(Player p, bool test)
        {
            Start(p);
            RavenFx.Broadcast(RpcFx, p, Spawn);
            if (test || RavenSkills.PassiveSpeaks())
            {
                string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "bloodfury") : null;
                RavenSkills.SayFromShoulder(p, true, string.IsNullOrEmpty(line) ? Lang.T("КРОВ! ТЕПЕР ВОНИ ЗДОХНУТЬ!") : line);
            }
            RavenMsg.Center(p, Lang.F("Кривава лють Муніна!\nРви їх {0}!", RavenAbilities.Span(Duration())));
        }

        // the test panel: without low health, strength or wait
        internal static void Test(Player p) { if (p != null && !p.IsDead()) Fire(p, true); }

        private static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Кривава лють");
            se.m_tooltip = Lang.F("+{0}% шкоди, +{1}% до швидкості атаки", Mathf.RoundToInt(DamagePercent), Mathf.RoundToInt(SpeedPercent));
            se.m_icon = AbilityIcons.ForEffect("bloodfury", true);
            se.m_ttl = Mathf.Max(1f, Duration());
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = 1f + Mathf.Max(0f, DamagePercent) / 100f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        internal static void Register() { RavenFx.Register(RpcFx, Spawn); }

        // a blood-red flash, a ring of dark red feathers and Munin's low scream
        private static void Spawn(Player p)
        {
            try
            {
                Vector3 chest = p.transform.position + Vector3.up * 1.2f;
                LastChance.Flash(p, chest, new Color(1f, 0.15f, 0.1f));
                LastChance.Ring(chest, new Color(0.75f, 0.05f, 0.05f, 1f), new Color(0.3f, 0.02f, 0.02f, 1f));
                ScreamFx.Caw(p, true, 0.6f, 1f);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Blood fury effect failed: " + e.Message); }
        }
    }

}
#endif

#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // Munin's healing: works like a healing mead, over a few seconds, and brings you exactly to your full health.
    // Each tick heals a share of what is missing right now (more if you get hit meanwhile), never more than is missing,
    // so nothing ever spills over the maximum (no overheal for other mods to turn into anything).
    internal class RavenHeal : SE_Stats
    {
        internal const string EffectName = "RavenWatch_Heal";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        internal static float Seconds = 10f;
        private const float Interval = 0.5f;
        private const string LookLike = "MeadHealthMedium"; // its drinking effects
        internal static float SoundStretch = 1.5f;          // their sound half as long again

        private float _nextTick;
        private static RavenHeal _template;

        internal static void Start(Player p) { Start(p, -1f); }

        // seconds: how long it takes (a friend's Munin sends it, «Зцілення Муніна» III); -1: as your own raven makes it
        internal static void Start(Player p, float seconds)
        {
            RavenHeal se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Зцілення Муніна");
            se.m_icon = AbilityIcons.ForEffect("heal", true);
            se.m_ttl = seconds > 0f ? seconds : Mathf.Max(1f, Seconds / Trust.Mul(true, 0.05f) / GearFx.GuardMul(true) / GiftUp.Power("heal")); // more trust (and «Спіраль захисту»), faster healing
            se.m_tooltip = Lang.F("Відновлює здоров'я до повного за {0} с", Mathf.RoundToInt(se.m_ttl)); // (its real time, not the base)
            se.m_startEffects = new EffectList(); // the look is made here instead, to slow its sound
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            Look(p);
            if (seconds <= 0f) Facets.HealFriends(p, se.m_ttl);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            Character c = m_character;
            if (c == null || c != Player.m_localPlayer || c.IsDead()) return;
            if (m_time < _nextTick) return;
            _nextTick = m_time + Interval;
            float missing = c.GetMaxHealth() - c.GetHealth();
            if (missing <= 0.01f) return;
            float left = Mathf.Max(Interval, m_ttl - m_time);
            float amount = Mathf.Min(missing, missing * Mathf.Min(1f, Interval / left));
            if (amount > 0.01f) c.Heal(amount, true);
        }

        // the mead's drinking effects on you, the sound slowed
        private static void Look(Player p)
        {
            try
            {
                EffectList look = MeadEffects();
                if (look == null || p == null) return;
                GameObject[] made = RavenFx.Own(look, p.transform.position, p.transform.rotation, p.transform, 1f, -1);
                if (made != null) foreach (GameObject g in made) RavenFx.Stretch(g, SoundStretch);
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Heal look failed: " + e.Message); }
        }

        private static EffectList MeadEffects()
        {
            GameObject mead = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(LookLike) : null;
            ItemDrop drop = mead != null ? mead.GetComponent<ItemDrop>() : null;
            StatusEffect se = drop != null ? drop.m_itemData.m_shared.m_consumeStatusEffect : null;
            return se != null ? se.m_startEffects : null;
        }
    }
}
#endif

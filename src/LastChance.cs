#if RAVEN_ITEM
using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's last chance (passive): a blow that would kill you leaves you 1 health instead, and for a moment
    // nothing hurts you, so you can get away. It takes Hugin's strength and comes again only after a while.
    // Every way of dying ends in the game's SetHealth with 0 or less before the death itself is checked,
    // so that is where it is caught: blows, falls, poison, fire, drowning alike.
    internal static class LastChance
    {
        internal const string RpcFx = "RavenWatch_LastChanceFx";
        internal static bool Enabled = true;
        internal static float Cost = 50f;
        internal static float CooldownMinutes = 10f;
        internal static float SafeSeconds = 2f;

        private static float _safeUntil;
        // when Hugin can save you again: kept in the character, in world time, so leaving the game does not reset it
        private const string Key = "RavenWatch_lastChance";
        private static float Left(Player p) { return p != null ? WorldTime.Left(p.m_customData, Key) : 0f; }
        private static bool _busy;
        private static SE_Stats _shield;

        internal static void Reset() { _safeUntil = 0f; }
        internal static void ResetCooldown() { Player p = Player.m_localPlayer; if (p != null) p.m_customData.Remove(Key); }

        internal static string Status() { return Lang.F("−{0} сили{1} (є {2}) · {3}", Mathf.RoundToInt(Cost * GiftUp.CostMul("lastchance") * Satiety.CostMul(false)), Satiety.CostNote(false), Power.Current(false), ReadyText()); }

        // "готовий" or "відпочиває 7:30"
        internal static string ReadyText()
        {
            float left = Left(Player.m_localPlayer);
            return RavenAbilities.ReadyMark(left, Lang.T("готовий"));
        }

        // its wait as it is now: trust shortens it (and «Спіраль захисту»); the golden runes on top (RavenAbilities, Guard)
        internal static float Cooldown() { return Trust.Sooner(false, CooldownMinutes * 60f * GearFx.GuardCooldownMul(false)); }
        internal static float Safe() { return SafeSeconds * GearFx.GuardMul(false) * GiftUp.Power("lastchance"); } // (Спіраль захисту: longer)

        // the game is about to set this health; returns the health it should set instead
        internal static float Guard(Character c, float health)
        {
            if (!Enabled || _busy || c == null || c != Player.m_localPlayer || c.IsDead()) return health;
            float now = c.GetHealth();
            if (Time.time < _safeUntil && health < now) return now; // the safe moment: nothing hurts
            if (health > 0f || now <= 0f) return health;
            Player p = (Player)c;
            ItemDrop.ItemData hugin, munin;
            RavenItem.Worn(p, out hugin, out munin);
            if (hugin == null || Trust.Away(hugin) || Trust.SettledLevel(hugin) < RavenAbilities.StrongUnlock || Left(p) > 0f || !Shoulder.Arrived(p, false)) return health;
            _busy = true;
            try
            {
                if (!Power.Spend(p, false, Cost * GiftUp.CostMul("lastchance"))) return health;
                float wait = Mathf.Max(10f, Trust.RoundTime(Cooldown() * GiftUp.CooldownMul("lastchance"))); // the line tells the real wait
                WorldTime.Set(p.m_customData, Key, wait);
                Fire(p, false);
                Nicks.OnSaved(p); // a deed for Hugin's nickname
                p.Message(MessageHud.MessageType.TopLeft, Lang.F("Останній шанс: Хугін відпочиватиме {0}", RavenAbilities.Span(wait)));
                return Facets.On("lastchance") ? Mathf.Max(1f, p.GetMaxHealth() * 0.25f) : 1f; // («Останній шанс» III: a quarter of health)
            }
            catch (Exception e)
            {
                if (Plugin.Instance != null) Plugin.Instance.Log("Last chance failed: " + e.Message);
                return health;
            }
            finally { _busy = false; }
        }

        // the save itself: the safe moment, the flash every game sees, a word, the shield icon, the call in the middle
        private static void Fire(Player p, bool test)
        {
            _safeUntil = Time.time + Mathf.Max(0f, Safe());
            Play(p);
            if (test || RavenSkills.PassiveSpeaks())
            {
                string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, false, "lastchance") : null;
                RavenSkills.SayFromShoulder(p, false, string.IsNullOrEmpty(line) ? Lang.T("Не сьогодні! Тікай, поки я тримаю!") : line);
            }
            Shield(p);
            RavenMsg.Center(p, Lang.T("Хугін врятував твою дупу. Але більше не буде!"));
        }

        // the test panel: as if a blow was about to kill you (the health stays as it is)
        internal static void Test(Player p) { if (p != null && !p.IsDead()) Fire(p, true); }

        // the icon among your effects while nothing can hurt you
        private static void Shield(Player p)
        {
            if (SafeSeconds <= 0f) return;
            SE_Stats se = RavenFx.Template(ref _shield, LastChanceLook.ShieldName);
            se.m_name = Lang.W("Захист Хугіна");
            se.m_tooltip = Lang.T("Хугін вирвав тебе зі смерті: ніщо не ранить, поки діє. Тікай!");
            se.m_icon = AbilityIcons.ForEffect("lastchance", false);
            se.m_ttl = Safe();
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        internal static void Flash(Player p, Vector3 at, Color color)
        {
            GameObject g = new GameObject("RavenWatch_LastChanceFlash");
            g.transform.position = at;
            g.transform.SetParent(p.transform, true);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 4f;
            l.range = 9f;
            LastChanceFade fade = g.AddComponent<LastChanceFade>();
            fade.Light = l;
            fade.Seconds = 1.2f;
        }

        // golden feathers bursting outwards in a ring
        internal static void Ring(Vector3 at, Color from, Color to)
        {
            GameObject g = new GameObject("RavenWatch_LastChanceRing");
            g.transform.position = at;
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 48) });
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.3f;
            sh.rotation = new Vector3(90f, 0f, 0f); // flat, around you
            Gradient fade = new Gradient();
            fade.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.6f;
            limit.dampen = 0.15f; // they burst, then drift
            Material m = LeapFeathers.Material();
            if (m != null) g.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            ps.Play();
        }

        // ---------------------------------------------------------------- the look, for everyone nearby

        internal static void Register() { RavenFx.Register(RpcFx, Spawn); }
        private static void Play(Player p) { RavenFx.Broadcast(RpcFx, p, Spawn); }

        private static void Spawn(Player p)
        {
            try
            {
                Raven hugin = Shoulder.Template(false);
                Vector3 chest = p.transform.position + Vector3.up * 1.2f;
                if (hugin != null && hugin.m_despawnEffect != null) RavenFx.Own(hugin.m_despawnEffect, chest, p.transform.rotation, null, 2.5f, -1);
                Flash(p, chest, new Color(1f, 0.9f, 0.6f));
                Ring(chest, new Color(1f, 0.95f, 0.75f, 1f), new Color(1f, 0.78f, 0.3f, 1f));
                ScreamFx.Caw(p, false, 1.1f, 1f);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Last chance effect failed: " + e.Message); }
        }
    }

    // the look of the save: a white-gold flash that fades, a ring of golden feathers, a shield icon while it holds
    internal static class LastChanceLook
    {
        internal const string ShieldName = "RavenWatch_HuginShield";
    }

    // fades a light out and takes the object away
    internal class LastChanceFade : MonoBehaviour
    {
        internal Light Light;
        internal float Seconds = 1.2f;
        private float _start, _from;

        private void Start() { _start = Time.time; _from = Light != null ? Light.intensity : 0f; }

        private void Update()
        {
            float t = (Time.time - _start) / Mathf.Max(0.1f, Seconds);
            if (Light != null) Light.intensity = Mathf.Lerp(_from, 0f, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

}
#endif

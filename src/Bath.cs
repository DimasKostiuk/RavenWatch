#if RAVEN_ITEM
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Купання» (the user, 2026-10-27): now and then a raven flies off to wash at the water's edge near you — when, by its
    // character and by how dirty it is. In the water it crouches, beats its wings, dips its head, the water splashing;
    // then back on your shoulder it shakes itself off — onto you (you are wet a while). A little trust for it.
    //   - The dirt (0–100, hidden, kept in the raven): the swamp, a fight, a dig in the bones, Munin's kills soil it; rain
    //     washes a little; a bath, all of it. A dirty raven wants the water sooner and grumbles about it now and then.
    //   - The visit itself is WorldVisit's (kind Bathe, its own moment like the fish): the spot by the shore, the flight
    //     there and back seen by all; the bath on the spot (PerchLife.Bathe) by a count on the spot's ZDO, as the pecking.
    //   - Not at night (but a «Нічна пташка»), not in the cold (but the breed «Жаровий»), not hungry, not in a fight.
    internal static class Bath
    {
        internal const string DirtKey = "RavenWatch_dirt";
        internal static float SwampPerMin = 2f, FightPerMin = 1.5f, RainPerMin = -1f, BonesDirt = 15f, KillDirt = 1.5f;
        internal static float AgainMin = 25f, AgainMax = 40f;  // min between two baths (clean, an eager character: see Pause)
        internal static float TrustGain = 1.5f, DirtySay = 80f, ShakeAfter = 3.5f, ShakeWindow = 20f, WetSeconds = 60f;
        private const float SampleEvery = 10f;

        internal static float DirtOf(ItemDrop.ItemData item)
        {
            string raw; float v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(DirtKey, out raw) && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static void Set(ItemDrop.ItemData item, float v)
        {
            if (item == null || item.m_customData == null) return;
            v = Mathf.Clamp(v, 0f, 100f);
            if (v < 0.5f) item.m_customData.Remove(DirtKey);
            else item.m_customData[DirtKey] = v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        // more dirt on it (a dig, a kill): over DirtySay for the first time, a grumble
        internal static void Soil(ItemDrop.ItemData item, float by)
        {
            if (item == null || by == 0f) return;
            float was = DirtOf(item), now = Mathf.Clamp(was + by, 0f, 100f);
            Set(item, now);
            bool munin;
            if (was < DirtySay && now >= DirtySay && RavenItem.IsRaven(item, out munin)) PerkFx.Say(munin, "bath_dirty", 0.7f, 10f);
        }

        internal static void Killed() { ItemDrop.ItemData m = With(true); if (m != null) Soil(m, KillDirt); } // (Munin: the blood)

        // ---------------------------------------------------------------- what makes it want the water

        // its character's eagerness (the neat ones often, the bold one seldom)
        internal static float CharEager(ItemDrop.ItemData item)
        {
            Norov.Character c = Norov.CharOf(item);
            string id = c != null ? c.Id : "";
            return id == "homebody" || id == "calm" || id == "fidget" ? 1.3f : id == "bold" ? 0.5f : 1f;
        }

        // ...and with its dirt: which of the two goes
        internal static float Eager(ItemDrop.ItemData item)
        {
            float d = DirtOf(item);
            return CharEager(item) * (d >= 80f ? 4f : d >= 50f ? 2f : d < 15f ? 0.5f : 1f);
        }

        // dirty enough to go before its pause is over (but never two baths within MinGap)
        internal static float DirtyGo = 50f, MinGap = 300f;
        internal static bool Dirty(ItemDrop.ItemData item) { return item != null && DirtOf(item) >= DirtyGo; }

        // the pause before the next bath after this one, by its character (it comes clean out of the water) (min → s)
        internal static float Pause(ItemDrop.ItemData item) { return Random.Range(AgainMin, AgainMax) * 60f / Mathf.Max(0.2f, CharEager(item)); }

        // how long it washes: the calm and the homebody long, the fidget a quick splash
        internal static float StayOf(ItemDrop.ItemData item)
        {
            Norov.Character c = Norov.CharOf(item);
            string id = c != null ? c.Id : "";
            return id == "calm" || id == "homebody" ? Random.Range(10f, 12f) : id == "fidget" ? Random.Range(5f, 6f) : id == "bold" ? Random.Range(6f, 7f) : Random.Range(7f, 9f);
        }

        // may it go now (the weather and the hour: the rest is WorldVisit's)
        internal static bool Weather(ItemDrop.ItemData item)
        {
            if (EnvMan.instance == null) return false;
            if (EnvMan.IsNight() && !Habits.Has(item, "night")) return false;
            if ((EnvMan.IsCold() || EnvMan.IsFreezing()) && !BreedTraits.ColdProof(item)) return false;
            return true;
        }

        // ---------------------------------------------------------------- the bath over: clean, a little trust; the shake on you

        private static readonly float[] ShakeAt = { -1f, -1f };
        internal static void Done(Player p, bool munin, ItemDrop.ItemData item)
        {
            Set(item, 0f);
            Trust.Played(munin, Lang.T("купання"));
            ShakeAt[munin ? 1 : 0] = Time.time + ShakeAfter;
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven world: " + Plugin.RavenName(munin) + " had a bath");
        }

        // ---------------------------------------------------------------- twice a second (RavenSkills)
        private static float _nextSample;
        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead()) return;
            float now = Time.time;
            for (int k = 0; k < 2; k++)
            {
                if (ShakeAt[k] < 0f || now < ShakeAt[k]) continue;
                bool munin = k == 1;
                if (now > ShakeAt[k] + ShakeWindow) { ShakeAt[k] = -1f; continue; } // (never sat back down: no shake)
                ShoulderLife life = Shoulder.LocalLife(munin);
                if (life == null || !life.SitNew || life.Hovering || !Shoulder.Arrived(p, munin)) continue;
                ShakeAt[k] = -1f;
                life.SitDirectorNow.React(life, SitTemplates.ShakeOffName, 0.2f);
                Wet(p);
                PerkFx.Say(munin, "bath_shake", 0.6f, 0f);
            }
            if (now < _nextSample) return;
            _nextSample = now + SampleEvery;
            float min = SampleEvery / 60f;
            bool swamp = WorldGenerator.instance != null && WorldGenerator.instance.GetBiome(p.transform.position) == Heightmap.Biome.Swamp;
            bool rain = EnvMan.IsWet() && !Senses.Indoors;
            float by = (swamp ? SwampPerMin : 0f) + (Senses.Fight ? FightPerMin : 0f) + (rain ? RainPerMin : 0f);
            if (by == 0f) return;
            foreach (bool m in new[] { false, true }) { ItemDrop.ItemData it = With(m); if (it != null) Soil(it, by * min); }
        }

        // the spray of its shake: you are wet a while
        private static void Wet(Player p)
        {
            SEMan se = p.GetSEMan();
            if (se == null) return;
            StatusEffect w = se.AddStatusEffect("Wet".GetStableHashCode(), true);
            if (w != null) w.m_ttl = Mathf.Max(w.m_ttl - w.GetRemaningTime(), 0f) + WetSeconds; // (a short wetting, not a soaking)
        }

        private static ItemDrop.ItemData With(bool munin)
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(munin);
            return p != null && item != null && Trust.Active(item) && !Trust.Away(item) && !Trust.Stranger(item, p) ? item : null;
        }

        // the console: raven bath [h|m] [dirt <0-100>]
        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            int at = a.Length > 1 && (a[1] == "h" || a[1] == "m" || a[1] == "hugin" || a[1] == "munin") ? 2 : 1;
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
            float v;
            if (a.Length > at + 1 && a[at] == "dirt" && float.TryParse(a[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Set(item, v);
            else if (a.Length > at && a[at] == "now") return WorldVisit.BathNow(p, munin);
            return Plugin.RavenName(munin) + ": dirt " + Mathf.RoundToInt(DirtOf(item)) + ", eager ×" + Eager(item).ToString("0.0", CultureInfo.InvariantCulture) + ". raven bath [h|m] [dirt <n> | now]";
        }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // How an errand's kill was made (kill … <how>, filled by the slot «manner»): your own blows, kept a few seconds per
    // creature and read when it dies. bow, magic, fire, frost, stagger: the last blow of yours (stagger: it fell on one
    // reeling — after a parry or a heavy blow); sneak: a blow on one that had not seen you yet, any time in that fight.
    // Kept only while the running step asks for a way (Wanted): the hits of a whole fight pass here otherwise.
    internal static class ErrandHow
    {
        internal static readonly string[] All = { "bow", "magic", "sneak", "stagger", "fire", "frost" };
        private const int Bow = 1, Magic = 2, Stagger = 4, Fire = 8, Frost = 16, Sneak = 32;
        private const float Keep = 8f; // s after your last blow that it still counts (a burn, a bleed, a fall)

        private sealed class Hit { public ZDOID Id; public string Prefab; public float At; public int Last, Ever; }
        private static readonly List<Hit> Hits = new List<Hit>();

        internal static ZDOID Dying = ZDOID.None; // the creature dying here now (Diary): looked up by itself, not by its kind

        internal static bool Is(string w) { return Array.IndexOf(All, w) >= 0; }

        // the count asked for: fewer, the harder the way
        internal static float Factor(string how)
        {
            switch (how)
            {
                case "sneak": return 0.3f;
                case "stagger": return 0.4f;
                case "fire": case "frost": return 0.5f;
                case "bow": case "magic": return 0.6f;
                default: return 1f;
            }
        }

        // the ways that make sense at your stage (magic needs the staffs, frost the mountain's glands)
        internal static bool Open(Player p, string how)
        {
            switch (how)
            {
                case "magic": return Stage.Has(p, "defeated_goblinking");
                case "frost": return Stage.Has(p, "defeated_bonemass");
                default: return Is(how);
            }
        }

        private static bool Wanted()
        {
            ErrandRun r = Errands.Of(Player.m_localPlayer);
            return r != null && r.Def != null && r.Current != null && r.Current.How.Length > 0;
        }

        // a blow of yours on a creature (Diary's OutgoingHitPatch, before it lands)
        internal static void OnHit(Character c, HitData hit)
        {
            if (c == null || hit == null || !Wanted()) return;
            int f = 0;
            switch (hit.m_skill)
            {
                case Skills.SkillType.Bows: case Skills.SkillType.Crossbows: f |= Bow; break;
                case Skills.SkillType.ElementalMagic: case Skills.SkillType.BloodMagic: f |= Magic; break;
            }
            if (hit.m_damage.m_fire > 0f) f |= Fire;
            if (hit.m_damage.m_frost > 0f) f |= Frost;
            if (c.IsStaggering()) f |= Stagger;
            BaseAI ai = c.GetBaseAI();
            if (ai != null && !ai.IsAlerted() && hit.m_backstabBonus > 1f) f |= Sneak; // (as the game's own sneak attack)
            ZDOID id = c.GetZDOID();
            Hit h = null;
            float now = Time.time;
            for (int i = Hits.Count - 1; i >= 0; i--)
            {
                if (Hits[i].Id == id) h = Hits[i];
                else if (now - Hits[i].At > Keep) Hits.RemoveAt(i); // (fights long over)
            }
            if (h == null) { h = new Hit { Id = id, Prefab = Utils.GetPrefabName(c.gameObject) }; Hits.Add(h); if (Hits.Count > 24) Hits.RemoveAt(0); }
            if (now - h.At > Keep) h.Ever = 0; // (a new fight with the same one)
            h.At = now; h.Last = f; h.Ever |= f;
        }

        // the kill just made (prefab) was made this way: the creature itself when it died here, else (it died on another
        // PC) your latest blow on one of its kind
        internal static bool Fits(string how, string prefab)
        {
            if (how.Length == 0) return true;
            float now = Time.time;
            Hit h = null;
            foreach (Hit x in Hits)
            {
                if (now - x.At > Keep) continue;
                if (Dying != ZDOID.None ? x.Id == Dying : string.Equals(x.Prefab, prefab, StringComparison.OrdinalIgnoreCase) && (h == null || x.At > h.At)) h = x;
            }
            if (h == null) return false;
            switch (how)
            {
                case "bow": return (h.Last & Bow) != 0;
                case "magic": return (h.Last & Magic) != 0;
                case "fire": return (h.Last & Fire) != 0;
                case "frost": return (h.Last & Frost) != 0;
                case "stagger": return (h.Last & Stagger) != 0;
                case "sneak": return (h.Ever & Sneak) != 0;
                default: return false;
            }
        }

        // the step's line: what way
        internal static string Note(string how)
        {
            switch (how)
            {
                case "bow": return Lang.T(" (останній удар — з лука чи арбалета)");
                case "magic": return Lang.T(" (останній удар — чарами з палиці)");
                case "fire": return Lang.T(" (останній удар — вогнем)");
                case "frost": return Lang.T(" (останній удар — морозом)");
                case "stagger": return Lang.T(" (добий, поки хитається: після відбиття чи важкого удару)");
                case "sneak": return Lang.T(" (почни бій зненацька, поки не помітив)");
                default: return "";
            }
        }

        // the errand's words: the raven who brought it says what way it wants
        internal static string Line(string how, bool munin)
        {
            switch (how)
            {
                case "bow": return munin ? Lang.T("Мунін хоче стрілами. Каже, так красивіше падають.") : Lang.T("Хугін просить здалеку. Хай не бачать, звідки летить.");
                case "magic": return munin ? Lang.T("Мунін хоче чарами. Щоб іскри летіли.") : Lang.T("І чарами, каже Хугін. Хай знають, що палиця не для краси.");
                case "fire": return munin ? Lang.T("Мунін хоче, щоб пахло смаленим.") : Lang.T("Хугін каже: вогнем. Він любить, як воно горить.");
                case "frost": return munin ? Lang.T("Мунін хоче льоду. Каже, мерзлі гарно тріщать.") : Lang.T("Хугін каже: морозом. Хай застигнуть.");
                case "stagger": return munin ? Lang.T("Мунін хоче, щоб ти спершу зби{в|ла} їх з ніг, а тоді доби{в|ла}. Без поспіху.") : Lang.T("Хугін хоче бачити голову, а не самі м'язи: спершу хай захитаються.");
                case "sneak": return munin ? Lang.T("Ззаду, — каже Мунін. — Хай навіть злякатися не встигнуть.") : Lang.T("Тихо, — каже Хугін. — Хто тебе помітив, той не рахується.");
                default: return "";
            }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Милість Одіна»: gifts laid in the nest's bowl. Each thing belongs to one of four groups and adds favour to it by
    // its worth; when Odin gives an errand, the favour leans his choice that way (and the group that won is spent).
    //   Кров    trophies, meat, blood      → Munin's hunts and champions
    //   Ремесло ore, metal, wood, hides     → Hugin's work (bring, smelt, gather)
    //   Шлях    mead, food, feathers        → errands that send you somewhere
    //   Золото  coins, amber, gems          → a little more chance of a golden errand
    // Kept in the character (customData RavenWatch_favour "blood=12.5;craft=…"): the nest is only where it is given.
    internal enum FavourGroup { Blood = 0, Craft = 1, Road = 2, Gold = 3 }

    internal static class Favour
    {
        internal const string Key = "RavenWatch_favour";
        internal const float Max = 100f;                    // per group
        internal static float LeanPer = 25f;                // favour that doubles a group's weight (100 → ×5)
        internal static float GoldPctAtMax = 10f;           // a full gold favour: +10 percentage points of golden chance
        internal static readonly string[] Ids = { "blood", "craft", "road", "gold" };
        internal static readonly string[] Names = Lang.Keys("Кров", "Ремесло", "Шлях", "Золото");
        internal static readonly string[] Leads = Lang.Keys("мисливські доручення й ватажки", "робота: принести, переплавити, зібрати", "мандри: дійти до місця", "трохи частіше золоте доручення");
        internal static readonly Color[] Colors = { new Color(0.72f, 0.2f, 0.16f), new Color(0.55f, 0.45f, 0.3f), new Color(0.3f, 0.5f, 0.35f), new Color(0.85f, 0.66f, 0.25f) };

        // ---------------------------------------------------------------- the character's record
        internal static float[] Of(Player p)
        {
            float[] v = new float[4];
            string raw;
            if (p == null || !p.m_customData.TryGetValue(Key, out raw)) return v;
            foreach (string part in raw.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                int i = System.Array.IndexOf(Ids, part.Substring(0, eq));
                float f;
                if (i >= 0 && float.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) v[i] = Mathf.Clamp(f, 0f, Max);
            }
            return v;
        }

        private static void Save(Player p, float[] v)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 4; i++) { if (v[i] <= 0.001f) continue; if (sb.Length > 0) sb.Append(';'); sb.Append(Ids[i]).Append('=').Append(v[i].ToString("0.##", CultureInfo.InvariantCulture)); }
            if (sb.Length == 0) p.m_customData.Remove(Key); else p.m_customData[Key] = sb.ToString();
            Version++;
        }

        internal static int Version;

        // ---------------------------------------------------------------- what a thing is worth
        private static readonly HashSet<string> BossTrophies = new HashSet<string> { "TrophyEikthyr", "TrophyTheElder", "TrophyBonemass", "TrophyDragonQueen", "TrophyGoblinKing", "TrophySeekerQueen", "TrophyFader" };
        private static readonly Dictionary<string, float> Gold = new Dictionary<string, float> { { "Coins", 0.02f }, { "Amber", 1f }, { "AmberPearl", 2f }, { "Ruby", 3f }, { "SilverNecklace", 3f } };
        private static readonly string[] Metals = { "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal", "FlametalNew" };
        private static readonly Dictionary<string, float> Craft = new Dictionary<string, float> { { "Wood", 0.15f }, { "FineWood", 0.3f }, { "RoundLog", 0.3f }, { "ElderBark", 0.4f }, { "YggdrasilWood", 0.6f }, { "Blackwood", 0.6f }, { "Resin", 0.2f }, { "Coal", 0.15f }, { "LeatherScraps", 0.25f }, { "DeerHide", 0.4f }, { "TrollHide", 1f }, { "WolfPelt", 1f }, { "LoxPelt", 1.5f }, { "ScaleHide", 1.5f }, { "Stone", 0.05f }, { "Flint", 0.1f } };
        private static readonly Dictionary<string, float> Blood = new Dictionary<string, float> { { "Bloodbag", 0.5f }, { "Entrails", 0.3f }, { "Ooze", 0.4f } };

        // its group and worth per piece; false when Odin does not want it
        internal static bool Worth(ItemDrop.ItemData it, out FavourGroup g, out float w)
        {
            g = FavourGroup.Blood; w = 0f;
            if (it == null || it.m_dropPrefab == null || it.m_equipped) return false;
            string n = it.m_dropPrefab.name;
            bool raven; if (RavenItem.IsRaven(it, out raven)) return false;
            float f;
            if (n.StartsWith("Trophy")) { if (BossTrophies.Contains(n)) return false; g = FavourGroup.Blood; w = 4f; return true; }
            if (Blood.TryGetValue(n, out f)) { g = FavourGroup.Blood; w = f; return true; }
            if (n.EndsWith("Meat") || n == "NeckTail") { g = FavourGroup.Blood; w = 1f; return true; }
            if (Gold.TryGetValue(n, out f) || n.StartsWith("Gemstone")) { g = FavourGroup.Gold; w = f > 0f ? f : 4f; return true; }
            foreach (string m in Metals)
            {
                if (n == m) { g = FavourGroup.Craft; w = 2f; return true; }
                if (n == m + "Ore" || n == m + "Scrap") { g = FavourGroup.Craft; w = 1f; return true; }
            }
            if (n.EndsWith("Ore") || n.EndsWith("Scrap")) { g = FavourGroup.Craft; w = 1f; return true; }
            if (Craft.TryGetValue(n, out f)) { g = FavourGroup.Craft; w = f; return true; }
            if (n.StartsWith("Mead")) { g = FavourGroup.Road; w = 2f; return true; }
            if (n == "Feathers") { g = FavourGroup.Road; w = 0.2f; return true; }
            ItemDrop.ItemData.SharedData s = it.m_shared;
            if (s != null && s.m_itemType == ItemDrop.ItemData.ItemType.Consumable && (s.m_food > 0f || s.m_foodStamina > 0f)) { g = FavourGroup.Road; w = 1f; return true; }
            return false;
        }

        // the things in the bag Odin would take, one entry per kind (for the bowl's grid)
        internal static List<ItemDrop.ItemData> Giftable(Player p)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            HashSet<string> seen = new HashSet<string>();
            if (p == null) return l;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                FavourGroup g; float w;
                if (!Worth(it, out g, out w) || !seen.Add(it.m_dropPrefab.name)) continue;
                l.Add(it);
            }
            l.Sort((a, b) => { FavourGroup ga, gb; float wa, wb; Worth(a, out ga, out wa); Worth(b, out gb, out wb); int c = ((int)ga).CompareTo((int)gb); return c != 0 ? c : wb.CompareTo(wa); });
            return l;
        }

        // what n pieces of this kind would add (capped by the group's room); -1 group when Odin does not want it
        internal static float Would(Player p, string prefab, int n, out FavourGroup g)
        {
            g = FavourGroup.Blood;
            ItemDrop.ItemData it = Find(p, prefab);
            float w;
            if (it == null || !Worth(it, out g, out w)) return 0f;
            return Mathf.Min(Max - Of(p)[(int)g], n * w);
        }

        // how many pieces of this kind fill its group (at least one)
        internal static int ToFill(Player p, string prefab)
        {
            ItemDrop.ItemData it = Find(p, prefab);
            FavourGroup g; float w;
            if (it == null || !Worth(it, out g, out w)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt((Max - Of(p)[(int)g]) / Mathf.Max(0.001f, w)));
        }

        internal static ItemDrop.ItemData Find(Player p, string prefab)
        {
            if (p == null) return null;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab && !it.m_equipped) return it;
            return null;
        }

        // laid in the bowl from its slots: n pieces of this kind; null when given, else why not
        internal static string Give(Player p, string prefab, int n)
        {
            ItemDrop.ItemData it = Find(p, prefab);
            if (it == null) return Lang.T("У сумці цього вже нема.");
            FavourGroup g; float w;
            if (!Worth(it, out g, out w)) return Lang.T("Одіну це не потрібне.");
            float[] v = Of(p);
            int gi = (int)g;
            if (v[gi] >= Max - 0.01f) return Lang.F("Милість «{0}» уже повна.", Lang.T(Names[gi]));
            n = Mathf.Min(n, Trust.Have(p, prefab), ToFill(p, prefab));
            if (n <= 0) return Lang.T("У сумці цього вже нема.");
            p.GetInventory().RemoveItem(Trust.FindName(p, prefab), n, -1, false);
            float was = v[gi];
            v[gi] = Mathf.Min(Max, v[gi] + n * w);
            Nest.Gifted(p, v[gi] - was); // (toward Odin answering the next call)
            Save(p, v);
            Errands.Log("gift to Odin: " + n + "× " + prefab + " → " + Ids[gi] + " " + v[gi].ToString("0.#", CultureInfo.InvariantCulture));
            return null;
        }

        // ---------------------------------------------------------------- what it does
        // an errand's group: its template's "group:", else by its first step (kill → blood, go → road, else craft)
        internal static FavourGroup GroupOf(ErrandDef d)
        {
            if (d == null) return FavourGroup.Craft;
            int i = System.Array.IndexOf(Ids, d.Group);
            if (i >= 0 && i < 3) return (FavourGroup)i;
            string first = d.Steps.Count > 0 && d.Steps[0].Length > 0 ? d.Steps[0][0] : "";
            if (first == "kill" || first == "kill_champion" || first == "clear") return FavourGroup.Blood;
            if (first == "go" || first == "read") return FavourGroup.Road;
            if (first == "do" && d.Steps[0].Length > 1 && d.Steps[0][1].ToLowerInvariant() == "parry") return FavourGroup.Blood;
            return FavourGroup.Craft;
        }

        // how much more this errand is wanted now (×1 … ×5)
        internal static float Mul(Player p, ErrandDef d)
        {
            float[] v = Of(p);
            return 1f + v[(int)GroupOf(d)] / Mathf.Max(1f, LeanPer);
        }

        // more chance of a golden errand (0–0.1); only where golden ones may come at all
        internal static float GoldBonus(Player p) { return Of(p)[(int)FavourGroup.Gold] / Max * GoldPctAtMax / 100f; }

        // which raven flies: blood leans to Munin, craft to Hugin (0.2–0.8)
        internal static float MuninChance(Player p)
        {
            float[] v = Of(p);
            return Mathf.Clamp(0.5f + (v[(int)FavourGroup.Blood] - v[(int)FavourGroup.Craft]) / (2f * Max) * 0.6f, 0.2f, 0.8f);
        }

        // an errand came: the favour that leaned to it is spent (a golden one spends the gold)
        internal static void Spent(Player p, ErrandDef d)
        {
            if (p == null || d == null) return;
            float[] v = Of(p);
            int i = d.Golden ? (int)FavourGroup.Gold : (int)GroupOf(d);
            if (v[i] <= 0f) return;
            v[i] = 0f;
            Save(p, v);
        }

        // the group Odin leans to most now (−1: none)
        internal static int Leaning(Player p)
        {
            float[] v = Of(p);
            int best = -1; float top = 0.5f;
            for (int i = 0; i < 4; i++) if (v[i] > top) { top = v[i]; best = i; }
            return best;
        }

        internal static string Cmd(Player p, string[] a)
        {
            if (a.Length > 2)
            {
                int i = System.Array.IndexOf(Ids, a[1].ToLowerInvariant());
                float f;
                if (i < 0 || !float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return "raven favour [blood|craft|road|gold n]";
                float[] v = Of(p); v[i] = Mathf.Clamp(f, 0f, Max); Save(p, v);
            }
            float[] s = Of(p);
            return "Милість: кров " + s[0].ToString("0.#") + ", ремесло " + s[1].ToString("0.#") + ", шлях " + s[2].ToString("0.#") + ", золото " + s[3].ToString("0.#") + "\nraven favour <група> <число>";
        }
    }
}
#endif

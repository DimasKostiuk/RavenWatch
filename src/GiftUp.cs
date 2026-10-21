#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Golden runes into gifts: a raven holds up to five, a gift takes up to three (levels I–III), for good. Kept in
    // the raven item (customData RavenWatch_gold = "scout:2,fury:1"), so they go with the raven (given away, returned).
    // What a level gives, by kind of change — every gift reads these through one call where it already counts:
    //   I   — stronger: its main thing (how long it lasts, how far, how hard) × Power;
    //   II  — cheaper and sooner: strength cost and pause × CostMul / CooldownMul;
    //   III — its own facet (Facets: one per gift, said in the book; the code in Facets.cs and in the gift itself).
    // The numbers are the first proposal (to be tuned with the user).
    internal static class GiftUp
    {
        internal const string Key = "RavenWatch_gold";
        internal const int MaxPerRaven = 5, MaxLevel = 3;
        internal static float[] PowerByLevel = { 1f, 1.25f, 1.25f, 1.25f };
        internal static float[] CostByLevel = { 1f, 1f, 0.8f, 0.8f };
        internal static float[] CooldownByLevel = { 1f, 1f, 0.8f, 0.8f };

        // the gifts a golden rune can go into (the thoughts and the loyalty at death: no)
        internal static readonly HashSet<string> Upgradable = new HashSet<string>
        {
            "scout", "light", "leap", "shadow", "dome", "hstrike", "hdecoy", "hflight", "lightstep", "lastchance",
            "mstrike", "anger", "bloodfury", "scream", "fury", "wings", "heal", "ward", "mberserk",
        };

        // what level III will add (its own behaviour; shown in the book as what is coming)
        internal static readonly Dictionary<string, string> Facets = new Dictionary<string, string>
        {
            { "scout", Lang.K("позначає ще скрині, а ворогів із зірками — з їхніми зірками") }, { "light", Lang.K("нежить поруч тікає від світла") },
            { "leap", Lang.K("затисни стрибок у падінні — і ширяєш") }, { "shadow", Lang.K("удар із тіні ще вдвічі сильніший") },
            { "dome", Lang.K("під куполом лікує тебе: 2% здоров'я за секунду") }, { "hstrike", Lang.K("засліплення вдвічі довше, і удар його не знімає") },
            { "hdecoy", Lang.K("обдурений б'є сусідніх ворогів") }, { "hflight", Lang.K("на 15% швидше й швидший розгін") },
            { "lightstep", Lang.K("ще й біг витрачає на 15% менше витривалості") }, { "lastchance", Lang.K("лишає не 1, а 25% здоров'я") },
            { "mstrike", Lang.K("кровотеча вдвічі сильніша") }, { "anger", Lang.K("злість триває вдвічі довше") },
            { "bloodfury", Lang.K("кожен твій удар у люті лікує на 2% здоров'я") }, { "scream", Lang.K("ще й відкидає ворогів") },
            { "fury", Lang.K("ще й біжиш на 15% швидше") }, { "wings", Lang.K("ще й пливеш на 30% швидше") },
            { "heal", Lang.K("лікує й гравців поруч (до 10 м)") }, { "ward", Lang.K("стріли й закляття, що влучили в заслону, б'ють у стрільця") },
            { "mberserk", Lang.K("без втоми після звіра") },
        };

        // ---------------------------------------------------------------- the raven's record
        internal static Dictionary<string, int> Of(ItemDrop.ItemData item)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return d;
            foreach (string part in raw.Split(','))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                int lv = Mathf.Clamp(Errands.I(part.Substring(c + 1), 0), 0, MaxLevel);
                if (lv > 0) d[part.Substring(0, c)] = lv; // (an id this version does not know: kept, counted)
            }
            return d;
        }

        private static void Save(ItemDrop.ItemData item, Dictionary<string, int> d)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in d) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append(':').Append(kv.Value); }
            if (sb.Length == 0) item.m_customData.Remove(Key); else item.m_customData[Key] = sb.ToString();
        }

        internal static int Used(ItemDrop.ItemData item) { int n = 0; foreach (int v in Of(item).Values) n += v; return n; }
        // read often (every cost, pause and duration the circle and the book draw): parsed once per record
        private static readonly Dictionary<string, Dictionary<string, int>> Parsed = new Dictionary<string, Dictionary<string, int>>();
        internal static int LevelIn(ItemDrop.ItemData item, string id)
        {
            string raw;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return 0;
            Dictionary<string, int> d;
            if (!Parsed.TryGetValue(raw, out d)) { if (Parsed.Count > 64) Parsed.Clear(); d = Of(item); Parsed[raw] = d; }
            int v;
            return d.TryGetValue(id, out v) ? v : 0;
        }

        // the level of this gift on the raven worn now (yours)
        internal static int Level(string id)
        {
            RavenAbility a = RavenAbilities.Find(id);
            return a != null ? LevelIn(Trust.Worn(a.Munin), id) : 0;
        }

        internal static float Power(string id) { RavenAbility a = RavenAbilities.Find(id); return PowerByLevel[Mathf.Clamp(Level(id), 0, MaxLevel)] * (a != null ? RuneStars.NightMul(Trust.Worn(a.Munin)) : 1f); } // («Ніч»: stronger at night)
        internal static float PowerOf(ItemDrop.ItemData item, string id) { return PowerByLevel[Mathf.Clamp(LevelIn(item, id), 0, MaxLevel)] * RuneStars.NightMul(item); } // (a given raven: a strike counts its own)
        internal static float CostMul(string id) { return CostByLevel[Mathf.Clamp(Level(id), 0, MaxLevel)]; }
        internal static float CooldownMul(string id) { return CooldownByLevel[Mathf.Clamp(Level(id), 0, MaxLevel)]; }

        // ---------------------------------------------------------------- putting one in
        // null when it may go into this gift of this raven now; else why not
        internal static string CanInvest(Player p, bool munin, string id)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (RuneBook.Of(p).Gold <= 0) return Lang.T("Золотої руни нема.");
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return Lang.F("{0} не на плечі.", Plugin.RavenName(munin));
            if (Trust.Stranger(item, p)) return Lang.F("{0} не твій.", Plugin.RavenName(munin));
            string off = Shoulder.NotOnShoulder(p, munin); // (things change only while it sits on your shoulder)
            if (off != null) return off;
            RavenAbility a = RavenAbilities.Find(id);
            if (a == null || a.Munin != munin || !Upgradable.Contains(id)) return Lang.T("Цей дар не покращується рунами.");
            if (!RavenAbilities.Unlocked(a)) return Lang.T("Цей дар ще не відкритий.");
            if (LevelIn(item, id) >= MaxLevel) return Lang.T("Дар уже на найвищому рівні.");
            if (Used(item) >= MaxPerRaven) return Lang.F("У {0} уже {1} золотих рун — більше не вміщає.", munin ? Lang.T("Муніна") : Lang.T("Хугіна"), MaxPerRaven);
            return null;
        }

        internal static string Invest(Player p, bool munin, string id)
        {
            string why = CanInvest(p, munin, id);
            if (why != null) return why;
            ItemDrop.ItemData item = Trust.Worn(munin);
            Dictionary<string, int> d = Of(item);
            int lv;
            d.TryGetValue(id, out lv);
            d[id] = lv + 1;
            Save(item, d);
            RuneBook.SpendGold(p);
            Backup.Save(p, item); // the raven's copy keeps it at once (a raven returned at the stones has it too)
            Errands.Log("golden rune into " + id + " of " + (munin ? "Munin" : "Hugin") + ": level " + (lv + 1));
            RavenFx.Play("sfx_raven_kaw", p.transform.position, Quaternion.identity);
            return null;
        }

        // ---------------------------------------------------------------- words
        internal static string Roman(int lv) { return lv <= 0 ? "" : lv == 1 ? "I" : lv == 2 ? "II" : "III"; }

        // what each level gives this gift (the book)
        internal static string LevelsText(string id)
        {
            string facet;
            Facets.TryGetValue(id, out facet);
            return Lang.F("I — сильніший: {0} +{1}%\nII — дешевший і швидше готовий: −{2}% сили й перезарядки\nIII — {3}",
                Lang.T(MainThing(id)), Mathf.RoundToInt((PowerByLevel[1] - 1f) * 100f), Mathf.RoundToInt((1f - CostByLevel[2]) * 100f),
                facet != null ? Lang.T(facet) : Lang.T("своя грань"));
        }

        // what level I makes stronger
        internal static string MainThing(string id)
        {
            switch (id)
            {
                case "scout": return Lang.K("радіус розвідки");
                case "hstrike": case "mstrike": return Lang.K("шкода удару");
                case "scream": return Lang.K("радіус крику");
                case "heal": return Lang.K("швидкість лікування");
                case "lastchance": return Lang.K("час без шкоди");
                case "anger": case "lightstep": return Lang.K("сила ефекту");
                default: return Lang.K("тривалість");
            }
        }
    }
}
#endif

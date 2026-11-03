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
    //   II  — a move of its own (Twists: one per gift, said in the book), and a little cheaper and sooner: × CostMul / CooldownMul;
    //   III — its own facet (Facets: one per gift, said in the book; the code in Facets.cs and in the gift itself).
    // The numbers are the first proposal (to be tuned with the user).
    internal static class GiftUp
    {
        internal const string Key = "RavenWatch_gold";
        internal const int MaxPerRaven = 5, MaxLevel = 3;
        internal static float[] PowerByLevel = { 1f, 1.25f, 1.25f, 1.25f };
        internal static float[] CostByLevel = { 1f, 1f, 0.9f, 0.9f }; // (II brings its own move too: the discount is smaller)
        internal static float[] CooldownByLevel = { 1f, 1f, 0.9f, 0.9f };

        // the gifts a golden rune can go into (the thoughts and the loyalty at death: no)
        internal static readonly HashSet<string> Upgradable = new HashSet<string>
        {
            "scout", "light", "leap", "shadow", "dome", "hstrike", "hdecoy", "hflight", "lightstep", "lastchance",
            "mstrike", "anger", "bloodfury", "scream", "fury", "wings", "heal", "ward", "mberserk", "mtame",
            "pwhirl", "pclear", "pload", // (the paired ones: a rune in either raven, their levels add up)
        };

        // what level II brings besides the price (its own move; Twists) — shown in the book
        internal static readonly Dictionary<string, string> Twists = new Dictionary<string, string>
        {
            { "scout", Lang.K("Хугін розвідує на пів радіуса попереду, туди, куди ти дивишся") }, { "light", Lang.K("погаснувши, світляк спалахує: вороги поруч сліпнуть на 3 с") },
            { "leap", Lang.K("у повітрі ще раз стрибок — другий помах крил, і змінюєш напрям") }, { "shadow", Lang.K("тінь накриває й найближчого друга поруч") },
            { "dome", Lang.K("ворогів, що опинились під куполом, викидає за край") }, { "hstrike", Lang.K("часом Хугін вихоплює в ураженого ворога одну річ з його здобичі") },
            { "hdecoy", Lang.K("Хугін веде обдуреного туди, куди ти дивився: у воду, зі скелі, під стріли") }, { "hflight", Lang.K("у польоті можна стріляти з лука, арбалета й посохів") },
            { "lightstep", Lang.K("друге дихання: коли витривалість скінчиться, Хугін раз повертає половину") }, { "lastchance", Lang.K("той, хто мало тебе не вбив, 10 с б'є тебе вдвічі слабше") },
            { "mstrike", Lang.K("з того, кого вбив удар Муніна, він часом вириває ще один трофей") }, { "anger", Lang.K("злість передається друзям поруч") },
            { "bloodfury", Lang.K("у люті тебе ніщо не хитає") }, { "scream", Lang.K("через 2 с крик лунає вдруге з того самого місця") },
            { "fury", Lang.K("ритм: кожен третій удар поспіль, поки тебе не вдарили, на 50% сильніший") }, { "wings", Lang.K("слід вітру: друзі поруч біжать із половиною твого прискорення") },
            { "heal", Lang.K("змиває отруту, горіння, мороз і мокрість") }, { "ward", Lang.K("удар упритул, що взяла заслона, наполовину вертається нападникові") },
            { "mberserk", Lang.K("звір на бігу збиває з ніг дрібних ворогів на шляху") }, { "mtame", Lang.K("тварина, приручена з Муніном, отримує на п'яту частину менше шкоди") },
            { "pwhirl", Lang.K("вихор потроху затягує ворогів до тебе") }, { "pclear", Lang.K("під ясним небом рослини ростуть у півтора раза швидше") },
            { "pload", Lang.K("ворони самі підбирають речі, що лежать довкола") },
        };

        // what level III will add (its own behaviour; shown in the book as what is coming)
        internal static readonly Dictionary<string, string> Facets = new Dictionary<string, string>
        {
            { "scout", Lang.K("позначає ще скрині, ворогів із зірками — з їхніми зірками, а над позначеними 10 с горять руни") }, { "light", Lang.K("нежить поруч тікає від світла, а ворогів у твоєму світлі ти б'єш на 10% сильніше") },
            { "leap", Lang.K("затисни стрибок у падінні — і ширяєш, а в ширянні відновлюється витривалість") }, { "shadow", Lang.K("удар із тіні ще вдвічі сильніший, а дар, натиснутий у тіні, — крок тінню: двічі за тінь переносить на 15 м туди, куди дивишся") },
            { "dome", Lang.K("під куполом лікує тебе: 2% здоров'я за секунду, а наприкінці купол вибухає рунами й валить тих, хто кружляв біля краю") }, { "hstrike", Lang.K("засліплення вдвічі довше, і удар його не знімає") },
            { "hdecoy", Lang.K("обдурений б'є сусідніх ворогів, а наприкінці Хугін дзьобає його в очі — 5 с сліпоти") }, { "hflight", Lang.K("на 15% швидше й швидший розгін, а затиснувши «присісти» високо в польоті, падаєш у піке: земля б'є ворогів довкола й оглушує їх на 5 с, і що вище ти був, то сильніше") },
            { "lightstep", Lang.K("ще й біг витрачає на 15% менше витривалості") }, { "lastchance", Lang.K("лишає не 1, а 25% здоров'я") },
            { "mstrike", Lang.K("кровотеча вдвічі сильніша") }, { "anger", Lang.K("злість триває вдвічі довше, а кожен удар, що ти отримав у злості, Мунін повертає кривдникові — у півтора раза сильніше") },
            { "bloodfury", Lang.K("кожен твій удар у люті лікує на 2% здоров'я, а той, хто вкинув тебе в лють, стікає кров'ю") }, { "scream", Lang.K("ще й відкидає ворогів, а ті, хто почув крик, 10 с не можуть блокувати") },
            { "fury", Lang.K("ще й біжиш на 15% швидше, а кожне вбивство продовжує лють на 3 с (до 15 с)") }, { "wings", Lang.K("ще й пливеш на 30% швидше, а корабель із тобою на борту йде під вітрилом на 20% швидше") },
            { "heal", Lang.K("лікує й гравців поруч (до 10 м), а коли здоров'я вже повне — відновлює витривалість") }, { "ward", Lang.K("стріли й закляття, що влучили в заслону, б'ють у стрільця, а коли заслона розбивається, її руни летять уперед і б'ють ворогів удвічі сильніше за удар Муніна (зникла сама — вполовину слабше)") },
            { "mberserk", Lang.K("без втоми після звіра, а рик при перевтіленні розганяє слабких ворогів") }, { "mtame", Lang.K("поки Мунін сидить, звір ситий — годувати не треба") },
            { "pwhirl", Lang.K("удари холодять — вороги довкола сповільнені") }, { "pclear", Lang.K("ясно ще й у радіусі вдвічі ширшому, а вночі світить ясний місяць") },
            { "pload", Lang.K("ще й без перевантаження, а падіння м'якше: −75% шкоди") },
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
            if (a != null && a.Pair) return Mathf.Min(MaxLevel, LevelIn(Trust.Worn(false), id) + LevelIn(Trust.Worn(true), id)); // (both ravens' runes)
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
            if (a == null || (a.Munin != munin && !a.Pair) || !Upgradable.Contains(id)) return Lang.T("Цей дар не покращується рунами.");
            if (!RavenAbilities.Unlocked(a)) return Lang.T("Цей дар ще не відкритий.");
            if (LevelIn(item, id) >= MaxLevel || (a.Pair && Level(id) >= MaxLevel)) return Lang.T("Дар уже на найвищому рівні.");
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

        // what each level gives this gift (the book): the levels it has in gold, the one it is at in bold with a mark,
        // the ones still to come greyed
        internal static string LevelsText(string id, int lv)
        {
            string[] rows = LevelsText(id).Split('\n');
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) b.Append('\n');
                int at = i + 1;
                if (at == lv) b.Append("<color=#F2CF6E><b>▸ ").Append(rows[i]).Append("</b></color>");
                else if (at < lv) b.Append("<color=#D8B45A>").Append(rows[i]).Append("</color>");
                else b.Append("<color=#8C8270>").Append(rows[i]).Append("</color>");
            }
            return b.ToString();
        }

        // what its golden runes give now, in one short line for the circles' info bar («радіус крику +25%, −20% сили й
        // перезарядки»); null: none
        internal static string ShortText(string id, int lv)
        {
            if (lv <= 0) return null;
            List<string> parts = new List<string>();
            parts.Add(Lang.F("{0} +{1}%", Lang.T(MainThing(id)), Mathf.RoundToInt((PowerByLevel[1] - 1f) * 100f)));
            string also = Also(id, Mathf.Min(lv, 2));
            if (also != null) parts.Add(also);
            string twist;
            if (lv >= 2 && Twists.TryGetValue(id, out twist)) parts.Add(Lang.T(twist));
            if (lv >= 2) parts.Add(Lang.F("−{0}% сили й перезарядки", Mathf.RoundToInt((1f - CostByLevel[2]) * 100f)));
            string facet;
            if (lv >= 3 && Facets.TryGetValue(id, out facet)) parts.Add(Lang.T(facet));
            return string.Join(", ", parts.ToArray());
        }

        internal static string LevelsText(string id)
        {
            string facet, twist;
            Facets.TryGetValue(id, out facet);
            Twists.TryGetValue(id, out twist);
            string[] rows = Lang.F("I — сильніший: {0} +{1}%\nII — {2}; і −{3}% сили й перезарядки\nIII — {4}",
                Lang.T(MainThing(id)), Mathf.RoundToInt((PowerByLevel[1] - 1f) * 100f), twist != null ? Lang.T(twist) : Lang.T("дешевший і швидше готовий"),
                Mathf.RoundToInt((1f - CostByLevel[2]) * 100f), facet != null ? Lang.T(facet) : Lang.T("своя грань")).Split('\n');
            for (int i = 0; i < 2 && i < rows.Length; i++) { string also = Also(id, i + 1); if (also != null) rows[i] += ", " + also; }
            return string.Join("\n", rows);
        }

        // what a level gives this gift besides the usual (null: nothing more)
        internal static string Also(string id, int lv)
        {
            if (id == "shadow" && lv > 0) return Lang.F("удар із тіні ×{0}", Shadow.StrikeAt(lv).ToString("0.#"));
            return null;
        }

        // what level I makes stronger
        internal static string MainThing(string id)
        {
            switch (id)
            {
                case "scout": return Lang.K("радіус розвідки");
                case "hstrike": case "mstrike": case "pwhirl": return Lang.K("шкода удару");
                case "pload": return Lang.K("вага, яку беруть ворони");
                case "scream": return Lang.K("радіус крику");
                case "heal": return Lang.K("швидкість лікування");
                case "mtame": return Lang.K("швидкість приручення");
                case "lastchance": return Lang.K("час без шкоди");
                case "anger": case "lightstep": return Lang.K("сила ефекту");
                default: return Lang.K("тривалість");
            }
        }
    }
}
#endif
